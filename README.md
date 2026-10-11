# Vigil Atlas

A self-hosted geopolitical / OSINT map dashboard. ASP.NET Core (.NET 8) backend, MapLibre GL JS front-end
(GPU-rendered via WebGL). Runs on Windows and on a Raspberry Pi (linux-arm64, 64-bit OS).

Shareable URL state: `?lat=50&lon=15&zoom=3.5&view=eu&timeRange=6h&layers=conflicts,military,cables`

## Layers

| Layer | Source | Notes |
|---|---|---|
| conflicts | GDELT 2.0 event stream | Rolling 24h window, city-geocoded violent events, no key |
| ucdpEvents | UCDP GED API | Needs a free token: set `Ucdp__Token` (untested without a token) |
| military | adsb.lol | Live ADS-B military aircraft, refreshes every 20s |
| quakes | USGS | M2.5+ |
| cables | OpenStreetMap (Overpass) | `man_made=submarine_cable`; incomplete compared with commercial datasets |
| radiationWatch | BfS ODL | German gamma dose-rate network only |
| sanctions | OFAC SDN | Entries per address country at country centroids |
| nuclear, spaceports, bases, cables | OpenStreetMap (Overpass) | Cached on disk for 7 days; bundled seed data until first refresh. Bases cover Europe/Med/Middle East |
| weather | RainViewer | Radar tiles |
| hotspots, pipelines | Curated | Pipeline routes are schematic, not survey data |
| irradiators | none | No public feed available |

The time range (1h/6h/24h) filters conflicts, quakes and ucdpEvents.

## Run (Windows or Linux)
```
cd VigilAtlas
dotnet run --urls http://localhost:5080
```

## Raspberry Pi
```
.\deploy\publish-pi.ps1 -Pi pi@raspberrypi.local
```
then follow the printed steps (copies to `/opt/vigil-atlas` and installs `deploy/vigil-atlas.service`).
Or use Docker: `docker buildx build --platform linux/arm64 -t vigil-atlas .`

## Limits
GDELT is machine-coded news, so locations and event types are noisy; treat it as a signal, not ground truth.
ADS-B shows only aircraft that broadcast and are tagged military by the community feed.

## License and legal
Copyright (C) 2026 Craig Magee. Code is GPL v3, see [LICENSE](LICENSE).
Third-party software and suppliers are credited in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
Each data source has its own terms, see [ATTRIBUTION.md](ATTRIBUTION.md). Two matter before going public or commercial:
the free RainViewer API is personal/educational only, and the public Overpass servers are not for commercial use.
This is a personal research tool, not legal advice, and not suitable for sanctions screening or safety-critical decisions.
