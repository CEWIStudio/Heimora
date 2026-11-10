<p align="center"><img src="assets/app-icon.png" width="112" alt="Heimora logo"></p>
<h1 align="center">Heimora</h1>
<p align="center">Secure connections workspace · Windows preview</p>

Manage RDP, SSH, web pages and reusable credentials in one workspace, with a local encrypted vault and an optional PostgreSQL metadata store.

**Preview:** central users, server-enforced permissions and a session gateway are not available yet. Read the [security guidance](SECURITY.md) before using real credentials.

- **Languages:** choose Nederlands or English under Administration / Beheer → Settings / Instellingen and branding. Translation coverage is still being verified.
- **Report a problem:** Help → Report an issue / Probleem melden opens a GitHub form in your browser. Only the version is prefilled; review your report before submitting it. A GitHub account is required.
- **Downloads:** use only installers published by the repository owner. No GitHub release has been verified for this update.
- **Licence:** the repository's existing GPL-3.0 licence remains applicable; third-party component notices are in `vendor`.

---
# Heimora 0.7.1 - Windows-preview

Heimora beheert RDP, SSH, webpagina's, mappen en herbruikbare credentials. De Windows x64-installer bevat de benodigde runtime, SQLite-engine, PostgreSQL-clientbibliotheek en SSH-terminal.

## Installeren

Start Heimora-Setup-0.7.1-win-x64.exe. De installer vraagt beheerdersrechten en gebruikt C:\Program Files\Heimora. De dagelijkse app gebruikt je Windows-account. De installer is nog niet digitaal ondertekend.

## Gegevens en herstel

De actieve gegevensmap is %LOCALAPPDATA%\Heimora. Sluit de app voordat je van de volledige map een back-up maakt. De lokale database heet heimora.db. De inhoud van de records is DPAPI-versleuteld voor het huidige Windows-account; SQLite-header en schema zijn zichtbaar. Dit is geen SQLCipher-versleuteling van het hele bestand.

Gegevens uit een eerdere installatie worden gecontroleerd overgenomen. Een bestaande Heimora-kluis wordt niet overschreven. Een herstelkopie wordt onder compatibility-backups met een neutrale profielnaam bewaard. De wachtwoordopslag wordt naar het huidige Heimora-formaat omgezet; auditidentiteit en verbindings-ID's blijven behouden. Bewaar database, beveiligde auditstatus en log altijd samen.

## Gebruik

Maak mappen, verbindingen en credentials via Start. Webpagina's openen in de standaardbrowser; alleen HTTP en HTTPS zijn toegestaan. Bij verbindingen kies je eigen inloggegevens of een credentialkoppeling. Bewerk een credential om de opgeslagen gebruikersnaam, het domein en het wachtwoord aan te passen. Het wachtwoord op de doelserver zelf wordt daarmee niet gewijzigd.

Een nieuw of gewijzigd remote doel vraagt toestemming voordat opgeslagen credentials worden opgehaald. Controleer daarnaast RDP-certificaten en SSH-hostkeys. Wachtwoorden worden gemaskeerd, bij tonen gelogd en na vijftien seconden opnieuw gemaskeerd. Beheer > Auditlog toont het lokale auditspoor zonder wachtwoordvelden. Een lokaal log is geen onafhankelijk serverbewijs.

## PostgreSQL

Maak vooraf een database en een aparte databasegebruiker. De app maakt public.heimora_vault aan. Een aanwezige tabel uit het voorgaande opslagformaat wordt transactioneel hernoemd; daarvoor zijn eigenaarsrechten nodig. Bij twee aanwezige kluistabellen wordt automatische omzetting geweigerd. Er wordt niet stilzwijgend samengevoegd.

De standaardverbinding controleert TLS-certificaat en servernaam. Wachtwoorden en auditlogs blijven in deze preview lokaal. Een lege serverkluis kan lokale metadata en koppelingen overnemen met behoud van bron en ID's. Er is geen offline synchronisatie. Centrale gebruikers, rollen en gateway staan beschreven in [CENTRAL-SECURITY-DESIGN.md](docs/CENTRAL-SECURITY-DESIGN.md) en zijn nog niet gebouwd.

## Naam en help

F1 opent de doorzoekbare offline help. Onder Over Heimora staat onze combinatie van Heimdall en aurora uitgelegd. [NAME-AND-BRANDING.md](docs/NAME-AND-BRANDING.md) bevat de kleuren en achtergrondbronnen. Beheer > Instellingen past eigen branding aan. Het splashscherm blijft minimaal vijf seconden zichtbaar terwijl laden doorgaat.

## Bouwen en controleren

Gebruik .NET 10 SDK en Inno Setup 6 op de ontwikkel-pc. build.ps1 publiceert zelfstandig voor Windows x64, voert de zelftests uit en maakt de installer. Een optionele PackageSource kan naar een lokale NuGet-feed verwijzen. --data-dir geeft een geïsoleerde gegevensmap; --self-test voert lokale controles uit. HEIMORA_SSH_NETWORK_TEST=1 activeert de aanvullende SSH-protocoltest.

Echte RDP-/SSH-authenticatie, live PostgreSQL, een volledige installatie/update/herstel op een schone pc en een onafhankelijke pentest zijn nog niet volledig gevalideerd. [SECURITY-REVIEW.md](docs/SECURITY-REVIEW.md) beschrijft de beveiligingsgrenzen. Er is geen garantie op afwezigheid van kwetsbaarheden.

Licenties van meegeleverde componenten staan in vendor. Persoonlijke gegevens blijven bij verwijderen bewaard.

## Repository structure

src/ contains the application code, grouped into UI, Data, Security and Connections. tests/ contains the built-in self-tests, which remain part of the application and run with --self-test. docs/ contains design, branding and security documentation. assets/, languages/ and vendor/ contain runtime resources and third-party notices. Heimora.csproj, build.ps1 and installer.iss remain in the repository root.
