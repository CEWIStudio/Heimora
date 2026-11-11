# Changelog

## 0.8.0 — 2026-11-11 (preview)

- Aparte navigatie voor Verbindingen en Inloggegevens, met zoeken en een toevoegknop.
- Eigen inloggegevensmappen; bestaande account-ID's, koppelingen en versleutelde inhoud blijven behouden.
- Rustigere boomweergave met neutrale tekst, ruimere rijen en zonder stippellijnen.
- Bescherming tegen het verwijderen van bestaande gemengde mappen met verborgen items uit het andere onderdeel.
- Appversie centraal in Heimora.csproj; schermen en installer gebruiken hetzelfde nummer. Versieafspraken staan in docs/VERSIONING.md.

Validatie en beperkingen: build en gerichte credential-/UI-tests worden bij deze release vastgelegd. De volledige zelftest is in de ontwikkelomgeving geblokkeerd bij het maken van een Windows-junction. Echte RDP-/SSH-authenticatie, live PostgreSQL en installatie op een schone pc zijn niet bewezen. Installers zijn nog niet digitaal ondertekend.
