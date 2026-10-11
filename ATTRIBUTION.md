# Attribution and licensing notes

Copyright (C) 2026 Craig Magee. The application code is released under GPL v3 (see LICENSE).
It is an independent, original implementation and contains no code from other dashboard projects.

Data and services keep their own licenses. This table records what each one asks of us.
Last reviewed: 2026-10-11. Re-check before making the repo public or using it commercially.

| Layer | Source | Terms and what we do |
|---|---|---|
| Basemap | OpenFreeMap / OpenMapTiles, © OpenStreetMap contributors | Free service, ODbL data. Attribution shown by the map and in the in-app credits |
| conflicts | GDELT Project | Unrestricted use incl. commercial; **must cite GDELT and link https://www.gdeltproject.org/** (done in the in-app credits) |
| ucdpEvents | UCDP GED | CC BY 4.0; cite Davies, Pettersson & Öberg (credits panel). Free API token required, so check their API terms when you register |
| military | adsb.lol | ODbL 1.0. Attribution given. Sharing a database derived from it publicly would require ODbL share-alike; we only display live data |
| quakes | USGS | US Government work, public domain; credit requested |
| cables, nuclear, spaceports, bases | © OpenStreetMap contributors via Overpass | ODbL: attribution required. The public Overpass servers ask for caching and identified requests (done), and say **commercial use should use a self-hosted or paid instance** |
| radiationWatch | BfS ODL network | Data are unverified measurements. I could not confirm the exact license text (likely a German open-data licence requiring attribution, "Datenlizenz Deutschland"), so verify on odlinfo.bfs.de before public/commercial release |
| sanctions | US Treasury OFAC SDN list | US Government work, public domain. Not legal advice and not a compliance tool: do not use for screening |
| Country centroids | mledoze/countries | ODbL 1.0, attribution given; the trimmed copy in `VigilAtlas/Data/countries.json` stays under ODbL |
| weather | RainViewer | **Free API is for personal and educational use only**; credit and link requested. Do not use in a commercial or public product without a RainViewer licence |
| Map library | MapLibre GL JS (BSD-3-Clause), loaded from unpkg | Permissive |
| Backend | .NET 8 / ASP.NET Core (MIT) | Permissive |

## Removed on review
- **TeleGeography Submarine Cable Map:** the earlier build pulled cable geometry from the unofficial
  `submarinecablemap.com` API. TeleGeography says its public *maps* are CC BY-SA 4.0 but the underlying
  data is restricted to paying subscribers and the interactive map is not an embeddable resource, so that
  usage was not safely licensed. The cables layer now uses OpenStreetMap `man_made=submarine_cable` data.

## Curated data
Hotspot points, pipeline routes (schematic) and fallback seed points were written by hand from public
facts; no third-party database was copied.

## Not covered
Trademarks: "Vigil Atlas" returned no exact-match product in a web search, but no trademark database
search was done. Search USPTO/UKIPO/EUIPO before commercial use.
