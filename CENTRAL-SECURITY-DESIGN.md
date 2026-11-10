# Centrale beveiliging — ontwerp, nog niet geïmplementeerd

De huidige preview verbindt rechtstreeks met PostgreSQL voor metadata. Zij heeft geen appgebruikers, rollen, centrale secretopslag of zelfstandig loginbeleid. Onderstaande eisen beschrijven de volgende centrale fase.

## Beheer en rechten

De desktop meldt zich aan bij een centrale API; alleen de service benadert PostgreSQL. Eerste ingebruikname laat de eigenaar met een eenmalig, uniek bootstrap-token de eerste beheerder aanmaken. Er komt geen algemeen standaardaccount of vast wachtwoord in de installer. Bootstrap wordt na gebruik afgesloten.

De service controleert iedere opdracht. Rechten zijn standaard geweigerd en kunnen op mappen, verbindingen en credentials worden toegekend: bekijken van metadata, verbinden/gebruiken, secretinhoud bekijken, bewerken, verwijderen en rechten beheren. Mapovererving en expliciete beperkingen moeten op de server eenduidig worden afgedwongen. Een gebruiker mag zichzelf geen hogere rechten geven. Het verbergen van een knop in de desktop is geen autorisatie.

## Gebruiken zonder het wachtwoord te zien

Een rol kan de knop en API voor het uitlezen van secrets missen en toch een sessie mogen starten. Dat is pas een sterke technische scheiding als het wachtwoord de desktop nooit bereikt. Een desktop die het wachtwoord ontsleutelt om zelf RDP/SSH op te zetten kan dit niet garanderen tegen iemand die de pc of het proces beheerst.

Voor die sterkere eis start een centrale verbindingsgateway de sessie en gebruikt zij het geheim daar. De desktop krijgt alleen de toegestane sessiestroom, geen secret of decryptiesleutel. De gateway moet ook sessie-opties zoals klembord/bestandsoverdracht afzonderlijk kunnen beperken. Geheimen kunnen ook zichtbaar worden binnen een doelsessie; doelrechten blijven daarom relevant. Dit ontwerp is geen absolute garantie tegen een gecompromitteerde gateway of doelserver.

## Opslag en audit

Transport vereist geverifieerde TLS. Centrale secrets worden met authenticatie versleuteld; sleutelbeheer staat los van database en databaseback-ups, met herstel- en rotatieprocedure. Appwachtwoorden worden met een passend password-hashschema verwerkt, niet zoals opvraagbare verbindingssecrets. Individuele accounts, MFA, sessieverval en beperking van inlogpogingen horen bij de centrale authenticatie.

De service schrijft auditgebeurtenissen naar een afzonderlijk beheerde bestemming: actor, tijd, bron, actie, doel en resultaat, inclusief geweigerde pogingen en rechtenwijzigingen. Geen secretinhoud, tokens of volledige gevoelige URL's. Een gedeelde databasegebruiker en het huidige lokale HMAC-log voldoen niet als centrale persoonsgebonden autorisatie of onafhankelijk auditbewijs.

## Acceptatie vóór productie

Test toegang tussen gebruikers en mappen, overerving, directe API-aanroepen zonder UI, secret-endpoints voor een gebruiksrol, bootstrap-hergebruik, intrekken van rechten tijdens sessies, uitval van audit/sleutelbeheer, back-upherstel en sleutelrotatie. Voor de gatewayroute moet een test bevestigen dat desktopverkeer en desktopgeheugen het verbindingswachtwoord niet bevatten. Een onafhankelijke beveiligingsbeoordeling en echte RDP/SSH-integratietests blijven noodzakelijk.
