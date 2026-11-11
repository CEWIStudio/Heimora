# Beveiligingscontrole — Heimora 0.7.1

Dit is een gerichte handmatige broncodecontrole met lokale tests. Er is geen formele gespecialiseerde scan of onafhankelijke penetratietest uitgevoerd. De gespecialiseerde scanvoorziening was niet beschikbaar. Productiegeschiktheid wordt niet geclaimd.

## Reikwijdte en dreigingsmodel

Gecontroleerd zijn de routes voor lokale opslag, herbruikbare credentials, bewerken/tonen, auditregistratie, RDP-/SSH-overdracht en databaseconfiguratie. Kernbestanden: `LocalDatabase.cs`, `Storage.cs`, `Credentials.cs`, `AuditLog.cs`, `PrivateFiles` in `AuditLog.cs`, `PasswordVisibility.cs`, `Dialogs.cs`, `MainForm.cs`, `Sessions.cs` en `TrustedSsh.cs`. Nieuwe regressietests staan in `SecurityTests.cs` naast de bestaande credential- en opstarttests.

De lokale Windows-gebruiker is de vertrouwensgrens. Andere gewone lokale accounts en kopieën van alleen de gegevensbestanden mogen geen leesbare wachtwoorden krijgen. De legitieme app moet inloggegevens kunnen ontsleutelen, tonen, wijzigen en aan de gekozen client doorgeven. Een volledig overgenomen Windows-account, een beheerder of malware in het eigen proces vallen buiten de garantie die deze lokale architectuur kan geven.

## Bevindingen en maatregelen

| Onderwerp | Beoordeling en wijziging |
| --- | --- |
| Versleuteling | Windows-DPAPI met `CurrentUser` blijft de basis. Geen zelfgekozen statische encryptiesleutel en geen zelfbedachte encryptie. Opvragen vereist ontsleuteling; alleen hashen zou deze functie onmogelijk maken. |
| Verwisselen van versleutelde waarden | De vorige opslag bond ciphertext niet aan een specifiek item. Formaat 2 gebruikt DPAPI-context op basis van audit-identiteit en secret-ID. De test waarin ciphertext van item A onder B wordt gezet, wordt nu geweigerd. Dit verhoogt de binding, niet de sterkte van een zwak wachtwoord op het doelsysteem. |
| Bestandsrechten | Wachtwoordbestand, back-up en beveiligde auditbestanden krijgen expliciete rechten voor het huidige Windows-account en SYSTEM. Dit is extra bescherming binnen Windows; de eigenaar of een beheerder blijft een bijzondere positie houden. |
| Ontbrekend auditspoor | Lezen, toevoegen, wijzigen, verwijderen en terugdraaien van secretwijzigingen worden geregistreerd. De app registreert ook expliciet tonen, itemwijzigingen, openen van de verbindingsclient en databasegebruik inclusief de gekozen TLS-modus. Geen wachtwoordvelden worden opgenomen. |
| Logmanipulatie | Records hebben een HMAC-SHA256-keten en een DPAPI-beveiligd controlepunt. Gewijzigde records, eenvoudige inkorting en ontbrekende auditbestanden worden gedetecteerd. Wachtwoordtoegang wordt bij een ongeldige of onbeschrijfbare auditvoorziening geblokkeerd. |
| Crash tussen log en controlepunt | Een volledig geschreven, geldig geauthenticeerd logdeel kan bij de volgende controle het controlepunt aanvullen. Een ongeldige of gedeeltelijke regel wordt niet stilzwijgend weggegooid. |
| Tonen van wachtwoorden | Standaard gemaskeerd; tonen wordt vooraf geregistreerd. Na 15 seconden en bij het verlaten van het venster wordt opnieuw gemaskeerd. Kopiëren/knippen via de standaard veldbediening is uitgeschakeld; plakken en selecteren blijven mogelijk. Er is geen extra Windows-Hello- of appwachtwoordcontrole. |
| SSH-client | De meegeleverde PuTTY-executable wordt vóór starten op een vaste SHA256 gecontroleerd en tijdens controle/start tegen schrijven/vervangen geopend. Een gewijzigde client wordt geweigerd. De wachtwoordpipe weigert remote clients en de ontvanger moet nog steeds de exacte gestarte proces-ID hebben. Geen wachtwoordbestand of wachtwoordargument. |
| Transport | RDP gebruikt CredSSP/NLA en waarschuwt bij mislukte serverauthenticatie. PuTTY verzorgt SSH-hostkeycontrole. PostgreSQL gebruikt standaard `VerifyFull`; expliciet uitschakelen voor een lab blijft mogelijk en wordt zichtbaar in het auditspoor. Gebruik die laboptie niet in productie. |

## Wat een lokaal auditlog niet bewijst

De actor is het Windows-account en de tijd komt van de lokale computer. Dit is geen onafhankelijke bevestiging van een persoon of tijdserver. Iemand met volledige controle over hetzelfde Windows-account kan DPAPI gebruiken of de lokale integriteitsleutel achterhalen. Ook terugzetten van een ouder log samen met het bijbehorende oudere controlepunt (bijvoorbeeld uit een back-up) kan lokaal niet betrouwbaar worden gedetecteerd; daarvoor is een externe vertrouwensbron nodig. Een geldige lokale keten is daarom geen onvervalsbaar of niet-afwijsbaar serverbewijs. Wijzigingen buiten de app, waaronder directe databasewijzigingen of toegang met oudere/aangepaste software, worden niet volledig door dit applog waargenomen.

Voor productie met meerdere gebruikers zijn een centrale service met eigen gebruikersauthenticatie/rechten en een afzonderlijke auditbestemming nodig. Die service moet de identiteit controleren en het log buiten de controle van de desktopgebruiker opslaan. Een directe PostgreSQL-verbinding alleen levert deze garanties niet.

## Gegevens, herstel en grenzen

Bij eerste gebruik worden oudere JSON-bestanden naar SQLite overgenomen en daarna versleuteld gearchiveerd. SQLite-recordinhoud wordt met DPAPI CurrentUser en recordcontext versleuteld; het databaseformaat, schema en recordnamen blijven zichtbaar. Dit is geen volledige bestandsversleuteling met SQLCipher. Geheime waarden hebben daarnaast de formaat-2 binding aan item en audit-identiteit. Een versleutelde back-up blijft bewaard. Maak vóór bijwerken een kopie van de volledige gegevensmap. Maak na migratie altijd samen een back-up van `heimora.db` en de map `security`; het auditcontrolepunt en log horen bij elkaar. Alleen een los wachtwoordbestand terugzetten is geen ondersteunde herstelroute. Teruggaan naar oudere appversies met de nieuwe opslag is niet ondersteund. Een kopie naar een andere pc is niet automatisch bruikbaar: de Windows-DPAPI-context moet ook beschikbaar zijn.

De lokale integriteitscontrole leest momenteel de volledige auditketen bij gebruik. Logrotatie en/of serveropslag zijn nodig vóór groot of langdurig intensief gebruik. Een bestandslease verhindert dat meerdere normale appinstanties dezelfde gegevensmap tegelijk beschrijven, ook over Windows-sessies heen. Een multi-file bewerking is geen databasebrede transactie: vooraf gelogde intenties kunnen bij een onderbreking zonder voltooiingsrecord blijven staan. Wachtwoorden zijn gedurende noodzakelijk gebruik aanwezig in proces-/clientgeheugen; zij zijn niet permanent onleesbaar in een ontgrendeld proces.

Migratie naar een lege PostgreSQL-kluis bewaart de lokale bron, maakt een SQLite-back-up en behoudt alle ID’s. De app weigert een gevulde kluis automatisch te overschrijven. Alleen metadata en koppelingen gaan naar PostgreSQL; wachtwoorden en het auditspoor blijven lokaal. De SQLite-back-up alleen is geen volledige herstelset: neem de complete gegevensmap inclusief security mee. De migratieroute is met een testopslag gecontroleerd, nog niet tegen een live PostgreSQL-server.

## Validatie en resterend productiewerk

### Opslag- en naamomzetting 0.7.1

De huidige lokale database heet heimora.db, de serverkluis public.heimora_vault. Nieuwe secretrecords gebruiken formaat 3 met Heimora-contextbinding. Bestaande records en auditcontrolepunten blijven leesbaar via onveranderlijke binaire formaat-ID's; secrets worden gecontroleerd opnieuw versleuteld met behoud van auditidentiteit. Deze formaat-ID's zijn geen sleutels of toegangsbewijzen.

Vorige gegevensprofielen worden onder neutrale herstelmappen bewaard. Een bestaande actieve kluis wordt niet samengevoegd of overschreven. De installer verwijdert eerdere programmamappen en snelkoppelingen niet automatisch. Een herkenbare executable bewijst niet dat alle overige bestanden bij de applicatie horen. Controleer oude installaties handmatig en bewaar eigen bestanden voordat je ze verwijdert.

De PostgreSQL-omzetting gebruikt een transactie en advisory lock. Tabel en bijbehorende automatisch benoemde constraints worden hernoemd; twee gelijktijdig aanwezige kluistabellen blokkeren automatische omzetting. Eigenaarsrechten zijn vereist. Deze route is gecompileerd maar niet tegen een live PostgreSQL-server getest. Maak vóór de update een volledige databaseback-up.

### Aanvullende controle 0.7.1

Gerichte handmatige hercontrole van opslag, credentialresolutie en -weergave, URL-openen, SSH-proces/pipe, PostgreSQL-parameters en lokale audit. Geen formele scan: de gespecialiseerde scanvoorziening en bijbehorende workflowreferenties waren hier niet beschikbaar. Dit is geen volledige afhankelijkhedenaudit of onafhankelijke pentest.

Aanvullende hardening: een gedeelde kluis kan de bestemming van een verbinding wijzigen terwijl een lokaal wachtwoord aan hetzelfde item gekoppeld blijft. Voor het ophalen van RDP-/SSH-credentials vraagt de app nu toestemming voor een nieuw of gewijzigd doel, poort, protocol, eigen loginidentiteit of credentialkoppeling. Standaard is Nee geselecteerd. De goedkeuring wordt lokaal DPAPI-versleuteld bewaard. Dit beperkt stilzwijgende omleiding door metadatawijzigingen, maar vervangt geen certificaat-/hostkeycontrole, beschermt niet tegen misleidende toestemming en detecteert DNS-kaping niet zelfstandig. Ook eerder goedgekeurde servers blijven een vertrouwensgrens.

Onvolledige/null itemvelden, controltekens in namen/loginidentiteiten, dubbele ID's en excessief grote itemlijsten worden expliciet geweigerd voordat clientgebruik plaatsvindt. Tests controleren weigering zonder wachtwoordread, opnieuw vragen na host-/poort-/loginwijziging en behoud van een eerdere goedkeuring bij annuleren.

De gebundelde PuTTY is 0.85 en de native SQLite-engine is 3.53.4. De officiële releasebeschrijvingen zijn geraadpleegd; dit bewijst niet dat deze versies vrij zijn van onbekende kwetsbaarheden. Npgsql 10.0.3 valt buiten de getroffen bereiken van de geraadpleegde oudere advisory GHSA-x9vc-6hfv-hg8c. Dit is een beperkte controle, geen uitputtende CVE-verklaring.

Bronnen: https://www.chiark.greenend.org.uk/~sgtatham/putty/changes.html ; https://www.sqlite.org/releaselog/3_53_4.html ; https://github.com/npgsql/npgsql/security/advisories/GHSA-x9vc-6hfv-hg8c .

Het ontwerp voor individuele centrale gebruikers, eerste beheerder, map-/credentialrechten en gebruik zonder secret op de desktop staat in CENTRAL-SECURITY-DESIGN.md. Het is nog niet geïmplementeerd. Alleen een verborgen knop is geen bescherming tegen het uitlezen van een wachtwoord dat naar de desktop wordt gestuurd; voor de sterkere eis is een gateway nodig.

Getest zijn onder andere ciphertextverwisseling, oude opslag migreren en bewerken, private ACL's, gelijktijdige logschrijvers, wijziging/inkorting/verwijdering van logbestanden, ontbreken van beveiligde status, geweigerde logschrijfopdracht vóór secretmutatie, herstel van een geldig logdeel, weigering van een aangepaste SSH-client, gemaskeerd/expliciet/aflopend wachtwoord tonen en een alleen-lezen auditvenster.

Niet uitgevoerd: wachtwoordkrakingsbenchmark, onderzoek van een echt gebruikersgeheugendump, onafhankelijke penetratietest, daadwerkelijke RDP-/SSH-authenticatie, live PostgreSQL, SMB-aanval tegen de pipe, en volledige installatie/update/herstel op een schone pc. De eerdere SSH-loopback-protocoltest liep vast; ingebedde terminal en wachtwoordoverdracht slagen wel. De installer is nog niet digitaal ondertekend.

Microsoft beschrijft de grenzen van DPAPI en de betekenis van gebruikerscontext en extra entropy: [CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata). Zie ook [GetNamedPipeClientProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid).
