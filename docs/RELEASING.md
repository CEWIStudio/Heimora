# GitHub publiceren en een release maken

Deze werkwijze is op 11 november 2026 uitgevoerd voor Heimora 0.8.0. Raadpleeg dit bestand bij een volgende commit, push of release. Dit document geeft geen zelfstandige toestemming om te publiceren: volg de concrete gebruikersopdracht.

## Repository en scope

- Repository: https://github.com/CEWIStudio/Heimora; standaardbranch `main`.
- De inhoud van `Codebase/` staat op de root van de GitHub-repository, dus zonder extra map `Codebase`.
- Versiebron: `<Version>` in `Heimora.csproj`. Zie [VERSIONING.md](VERSIONING.md).
- Commit uitsluitend bedoelde broncode, tests, resources en projectdocumentatie. Geen Secrets, lokale kluizen, logs, NuGet-caches, bin/obj, dist-*, release/ of source-*-testkopieën.
- Behoud remote bestanden die lokaal ontbreken. Bij 0.8.0 betrof dit `docs/AUDIT-2026-11-11.md`.

## Commit en push

1. Lees de actuele branch-head en bijbehorende tree opnieuw. Controleer of de lokale map een echte checkout is; een losse bronmap heeft geen Git-historie. Begin daarin niet met een losstaande nieuwe geschiedenis om die over de bestaande repository heen te pushen.
2. Vergelijk de lokale bronbestanden met de remote tree en inspecteer de inhoudelijke verschillen. Normaliseer voor vergelijking uitsluitend tekstregelovergangen naar LF. Vergelijk binaire bestanden op bytes.
3. Gebruik bij een beschikbare checkout gewone Git-commando's. Als de terminal GitHub niet kan bereiken maar de verbonden GitHub-integratie wel werkt, kan de Git Data API dezelfde commit en branchupdate uitvoeren:
   - Lees `branches/main` en `git/trees/<tree-sha>?recursive=1`.
   - Maak een tree met de bestaande tree als `base_tree_sha` en alleen de bedoelde gewijzigde bestanden.
   - Verifieer de nieuwe tree: gewijzigde blob-SHA's moeten overeenkomen met de lokale inhoud en ongewijzigde/remote-only bestanden moeten behouden blijven.
   - Maak een commit met de gelezen head als parent.
   - Werk `main` bij met `force=false` en `expected_sha` gelijk aan de gelezen head. Bij een afwijkende head: opnieuw lezen en verschillen beoordelen, niet forceren.
4. Lees de branch opnieuw en controleer de werkelijk gepushte commit-SHA.
5. Controleer GitHub Actions voor precies die SHA. De runs op een gewone push zijn te vinden via `actions/runs?head_sha=<sha>`; een reader die alleen pull-requestruns retourneert bewijst geen push-CI.

## Build en release

1. Werk versie, changelog en actuele downloadverwijzingen bij. Appschermen gebruiken de assemblyversie. Het buildscript geeft dezelfde versie via `/DAppVersion=<versie>` aan Inno Setup door.
2. Voer build, volledige zelftest en installercompilatie uit. Gebruik `build.ps1 -Inno <volledig pad naar ISCC.exe>` als de compiler niet op PATH staat. Een installatie per gebruiker kan onder `%LOCALAPPDATA%/Programs/Inno Setup 6/` staan.
3. Als een lokale omgevingsbeperking een test blokkeert: noteer dat. Een geïsoleerde deelrun mag aanvullend bewijs leveren, maar verzwak de productietests niet. Controleer de volledige Windows-build op GitHub vóór publicatie.
4. Controleer dat `v<versie>` nog niet bestaat. Maak de tag op de exacte geteste commit, niet op een mogelijk inmiddels verschoven branch-head. Overschrijf bestaande tags of downloads niet.
5. Publiceer previewversies als GitHub pre-release, met concrete wijzigingen, testbewijs en beperkingen. Als de integratie geen release-write ondersteunt, gebruik de normale GitHub-releasepagina in de verbonden browser.
6. Voeg `Heimora-Setup-<versie>-win-x64.exe` en `SHA256SUMS.txt` toe. Bereken de checksum over precies de bijgevoegde installer; meng geen lokale installer met de checksum van een andere CI-build. Signing en installatieproeven zijn afzonderlijke controles.
7. Als browserupload om lokale bestandstoegang vraagt: wijzig die toestemming niet zelf. Laat de gebruiker de benodigde toegang instellen of de twee bestanden handmatig bijvoegen. Een gepubliceerde release zonder installerbijlagen is niet hetzelfde als een afgeronde distributie.
8. Verifieer na publicatie de release-status, pre-releasevlag, tagdoel, assetnamen en checksums via GitHub. Leg resultaat en eventuele ontbrekende stappen vast onder `Audit-en-testresultaten/Heimora/`.

## Uitgevoerde publicatie 0.8.0

- Broncommit: `49d3fe825dd712593c59400bc1a60fba9addcd10`, gepusht naar `main` via de verbonden GitHub-integratie. De terminal kon GitHub niet via DNS bereiken.
- Vergelijking: 18 gewijzigde/nieuwe bronbestanden; alle blob-SHA's gecontroleerd en ongewijzigde remote bestanden behouden. Geen buildbestanden of secrets gecommit.
- Windows-CI: https://github.com/CEWIStudio/Heimora/actions/runs/43025981534 — geslaagd, inclusief publiceren, volledige ingebouwde zelftest, installercompilatie en artifact/checksum-stappen.
- Aanvullende pushcontrole: https://github.com/CEWIStudio/Heimora/actions/runs/43025980965 — geslaagd.
- Tag `v0.8.0` wijst naar bovengenoemde broncommit.
- Gepubliceerde pre-release: https://github.com/CEWIStudio/Heimora/releases/tag/v0.8.0 (release-ID `441718724`).
- Lokale installer: `release/Heimora-Setup-0.8.0-win-x64.exe`; SHA256 `8f0da08abde6d270d910cd738be587157af9fd238baf62eb3a22de9d8995e2c3`.
- Afgerond: de gebruiker heeft installer en checksum aan de release toegevoegd. Op 11 november 2026 via de GitHub API geverifieerd: beide assets hebben status `uploaded`. De SHA256 van de installer is gelijk aan bovenstaande lokale installerhash. Ook de SHA256 van het checksum-bestand zelf komt overeen met de lokale versie: `59e7f0b744b5bd62258cef5cca86799cc13bf8b76ded4175ca3594f42cc90a3f`. Daarmee hoort de gepubliceerde checksum aantoonbaar bij de gepubliceerde installer. De eerdere browseruploadbeperking heeft geen ontbrekende releasebijlagen meer tot gevolg.

Deze registratie is een momentopname. Lees bij vervolgwerk altijd de actuele GitHub-status opnieuw.
