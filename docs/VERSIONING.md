# Versies en releases

De enige bron voor het actuele versienummer is `<Version>` in `Heimora.csproj`.
De app leest haar assemblyversie voor titel, splashscherm en Over deze app.
`build.ps1` controleert de gepubliceerde versie en geeft hetzelfde nummer aan Inno Setup door.
De CI weigert een tag die niet exact overeenkomt met `v<Version>`.

## Ophogen

- `0.8.0` naar `0.8.1`: bugfixes en kleine visuele correcties zonder nieuwe functie.
- `0.8.x` naar `0.9.0`: nieuwe functies of bewust gewijzigde contracten tijdens de previewfase.
- `1.0.0`: expliciete beslissing dat de eerste stabiele versie klaar is; daarna verhogen incompatibele wijzigingen het eerste nummer.

Verhoog eenmaal per release, niet per build of commit. Leg wijzigingen vast in `CHANGELOG.md`.
Publiceer previewversies als GitHub pre-release. Tags hebben de vorm `v0.8.0` en blijven onveranderlijk; vervang nooit een bestaande tag of download met andere bytes.

## Releasevolgorde

1. Verhoog de projectversie, werk changelog en actuele README-downloadnaam bij.
2. Bouw en voer de volledige zelftest uit. Noteer ontbrekende controles en fouten; een deelrun is geen volledige goedkeuring.
3. Maak de installer met `build.ps1`, of geef bij een expliciete aparte compilerstap `/DAppVersion=<projectversie>` mee. Bewaar buildlog en SHA256.
4. Commit en push broncode. Controleer CI op precies die commit.
5. Maak de bijbehorende tag en GitHub pre-release op die commit. Voeg installer, `SHA256SUMS.txt` en release notes met testbeperkingen toe.

De metadata-indeling en secretopslag hebben hun eigen formaatversies. Wijzig die alleen wanneer hun contract verandert; een hogere applicatieversie verandert ze niet automatisch. Gebruik dezelfde Heimora-versie op clients die een kluis delen: oude clients kennen nieuwe metadata zoals accountmappen niet.
