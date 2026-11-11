# Heimora 0.8.0 Preview

Verbindingen en inloggegevens hebben nu elk hun eigen navigatieonderdeel. Accounts kunnen in aparte mappen worden beheerd en via hun bestaande ID aan meerdere servers gekoppeld blijven. De boom is rustiger vormgegeven en heeft een toevoegknop.

Bestaande accounts blijven behouden. Mappen die voorheen verbindingen en accounts mengden, kunnen in beide onderdelen zichtbaar blijven; het verwijderen van zo'n gemengde map is geblokkeerd. Verplaats de items via Bewerken naar aparte mappen. Gebruik dezelfde appversie op alle clients die een kluis delen.

De applicatieversie staat voortaan centraal in Heimora.csproj. De schermen lezen de assemblyversie en het buildscript geeft dezelfde versie aan de installer door. Zie VERSIONING.md voor de regels.

## Download

- Heimora-Setup-0.8.0-win-x64.exe
- SHA256SUMS.txt

De installer is niet digitaal ondertekend. Bewaar bestaande profieldata en maak een back-up voordat je een preview bijwerkt.

## Validatie

Build en installercompilatie geslaagd. 148 controles geslaagd in een geïsoleerde deelrun, inclusief credentialopslag, accountmappen, navigatie en de versie in het hoofdvenster en splashscherm. De productietests zijn ongewijzigd: alleen in de testkopie is DataLocationTests weggelaten vanwege de junctionbeperking.

De volledige zelftest is lokaal geblokkeerd bij het maken van een Windows-junction. SSH-netwerktransport meldt NOT VALIDATED door een loopbacktimeout. Echte RDP-/SSH-authenticatie, live PostgreSQL en installatie/verwijderen op een schone pc zijn niet getest. Controleer de volledige GitHub Actions-run op de releasecommit; een deelrun vervangt die niet.
