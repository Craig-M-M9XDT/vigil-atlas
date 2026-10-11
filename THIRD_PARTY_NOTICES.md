# Third-party notices

Vigil Atlas is Copyright (C) 2026 Craig Magee and released under the GNU General Public License v3
(see [LICENSE](LICENSE)). It is assembled from the third-party software, services and data below, which
keep their own copyright and licenses. Data-source terms are detailed in [ATTRIBUTION.md](ATTRIBUTION.md).
Last reviewed: 2026-10-11.

## GPL-family software
No GPL/AGPL-licensed source code is copied into or linked with Vigil Atlas. GPL-family software appears only
in these ways:

| Item | License | How it relates |
|---|---|---|
| GNU General Public License v3 text (`LICENSE`) | © Free Software Foundation, Inc. (verbatim copying permitted) | The license this project is released under |
| Overpass API server software (overpass-api.de and mirrors) | AGPL-3.0 | We call public instances over HTTP as a *service*. No Overpass code is included or distributed |
| Git | GPL-2.0 | Development tool used for version control; not part of the build output |
| Debian Linux base layer in the optional Docker image | Mixed, includes GPL packages | Only if you build and distribute the Docker image; the base image's license notices and source offers then apply to that image |

## Software included or loaded at runtime

| Component | License | Copyright / supplier |
|---|---|---|
| .NET 8 runtime and ASP.NET Core | MIT | © .NET Foundation and Contributors |
| MapLibre GL JS 4.7.1 (loaded from unpkg CDN) | BSD-3-Clause | © MapLibre contributors; includes code derived from Mapbox GL JS v1.x (BSD-3-Clause, © Mapbox) |
| unpkg CDN | service | Delivers the MapLibre files |
| GitHub CLI (`gh`) | MIT | © GitHub, Inc. Used only to create the repository |

The project has no NuGet package dependencies beyond the .NET SDK itself.

## Map and font services
| Service | Terms |
|---|---|
| OpenFreeMap (vector tiles and style) | Free public service, see https://openfreemap.org/tos/ |
| OpenMapTiles schema/design | © OpenMapTiles (BSD-3 code, CC BY 4.0 design), data © OpenStreetMap contributors (ODbL) |
| Noto Sans glyphs (served through the map style) | SIL Open Font License 1.1, © The Noto Project Authors |

## Data suppliers (licensing summary)
| Supplier | Layer | License / terms |
|---|---|---|
| OpenStreetMap contributors | nuclear, spaceports, bases, cables, basemap | ODbL 1.0, https://www.openstreetmap.org/copyright |
| GDELT Project | conflicts | Free, citation and link required, https://www.gdeltproject.org/ |
| Uppsala Conflict Data Program | ucdpEvents | CC BY 4.0; cite Davies, Pettersson & Öberg |
| adsb.lol community | military | ODbL 1.0 |
| U.S. Geological Survey | quakes | US Government work (public domain) |
| U.S. Treasury OFAC | sanctions | US Government work (public domain) |
| Bundesamt für Strahlenschutz | radiationWatch | German open data, attribution (exact licence to be confirmed) |
| RainViewer | weather | Personal/educational API use, credit requested |
| mledoze/countries | country centroids | ODbL 1.0 |

## Compatibility note
Permissive (MIT, BSD) components are compatible with distribution under GPL v3. ODbL, CC BY and similar
data licenses cover the *data*, not the program, and do not change the program's GPL v3 license. If you
publicly redistribute a database built from ODbL data, that database must stay under ODbL.

If you believe a notice here is missing or wrong, please open an issue and it will be corrected promptly.
