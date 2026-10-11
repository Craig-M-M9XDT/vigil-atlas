using System.Text.Json.Nodes;

namespace VigilAtlas;

/// <summary>Live feeds fetched on demand and cached in memory.</summary>
public sealed class Feeds(IHttpClientFactory http, TtlCache cache, CountryIndex countries, IConfiguration cfg, ILogger<Feeds> log)
{
    private HttpClient Client => http.CreateClient("up");

    /// <summary>Military aircraft broadcasting ADS-B (adsb.lol community feed).</summary>
    public Task<LayerResult> Military() => cache.GetAsync("mil", TimeSpan.FromSeconds(15), async () =>
    {
        var doc = JsonNode.Parse(await Client.GetStringAsync("https://api.adsb.lol/v2/mil"))!;
        var feats = new List<JsonObject>();
        foreach (var ac in doc["ac"]?.AsArray() ?? [])
        {
            var lat = Geo.D(ac?["lat"]); var lon = Geo.D(ac?["lon"]);
            if (lat is null || lon is null) continue;
            feats.Add(Geo.Point(lon.Value, lat.Value, new JsonObject
            {
                ["name"] = (ac!["flight"]?.ToString() ?? ac["r"]?.ToString() ?? ac["hex"]?.ToString() ?? "?").Trim(),
                ["type"] = ac["t"]?.ToString(),
                ["reg"] = ac["r"]?.ToString(),
                ["alt_ft"] = ac["alt_baro"]?.ToString(),
                ["speed_kt"] = Geo.D(ac["gs"]) is double gs ? Math.Round(gs) : null,
                ["track"] = Geo.D(ac["track"]) is double tr ? Math.Round(tr) : null,
            }));
        }
        return new LayerResult(Geo.Collection(feats), "adsb.lol (live, ADS-B only)");
    });

    public async Task<LayerResult> Quakes(int hours)
    {
        var all = await cache.GetAsync("quakes", TimeSpan.FromMinutes(10), async () =>
            new LayerResult(await Client.GetStringAsync("https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_day.geojson"), "USGS"));
        var cutoff = DateTimeOffset.UtcNow.AddHours(-Math.Clamp(hours, 1, 24)).ToUnixTimeMilliseconds();
        var feats = new List<JsonObject>();
        foreach (var f in JsonNode.Parse(all.Json)!["features"]!.AsArray())
        {
            var p = f!["properties"]!;
            if ((Geo.D(p["time"]) ?? 0) < cutoff) continue;
            var c = f["geometry"]!["coordinates"]!;
            feats.Add(Geo.Point(Geo.D(c[0])!.Value, Geo.D(c[1])!.Value, new JsonObject
            {
                ["name"] = p["place"]?.ToString(), ["mag"] = Geo.D(p["mag"]),
                ["note"] = "M" + Geo.D(p["mag"]), ["url"] = p["url"]?.ToString(),
            }));
        }
        return new LayerResult(Geo.Collection(feats), "USGS M2.5+");
    }

    /// <summary>Ambient gamma dose rate from the German BfS ODL network (EU-wide EURDEP needs separate access).</summary>
    public Task<LayerResult> Radiation() => cache.GetAsync("rad", TimeSpan.FromMinutes(10), async () =>
    {
        const string url = "https://www.imis.bfs.de/ogc/opendata/ows?service=WFS&version=2.0.0&request=GetFeature" +
                           "&typeNames=opendata:odlinfo_odl_1h_latest&outputFormat=application/json";
        var doc = JsonNode.Parse(await Client.GetStringAsync(url))!;
        var feats = new List<JsonObject>();
        foreach (var f in doc["features"]!.AsArray())
        {
            var p = f!["properties"]!; var c = f["geometry"]?["coordinates"];
            if (c is null || Geo.D(p["value"]) is not double v) continue;
            feats.Add(Geo.Point(Geo.D(c[0])!.Value, Geo.D(c[1])!.Value, new JsonObject
            {
                ["name"] = p["name"]?.ToString(), ["value"] = v, ["note"] = $"{v:0.000} µSv/h",
                ["measured"] = p["end_measure"]?.ToString(),
            }));
        }
        return new LayerResult(Geo.Collection(feats), "BfS ODL network (Germany)");
    });

    /// <summary>OFAC SDN list: distinct sanctioned entities per address country, placed at country centroids.</summary>
    public Task<LayerResult> Sanctions() => cache.GetAsync("sanctions", TimeSpan.FromHours(24), async () =>
    {
        var csv = await Client.GetStringAsync("https://www.treasury.gov/ofac/downloads/add.csv");
        var perCountry = new Dictionary<string, HashSet<string>>();
        foreach (var line in csv.Split('\n'))
        {
            var f = ParseCsvLine(line.TrimEnd('\r'));
            if (f.Count < 5) continue;
            var country = f[4].Trim();
            if (country.Length == 0 || country.StartsWith("-0-")) continue;
            if (!perCountry.TryGetValue(country, out var set)) perCountry[country] = set = new();
            set.Add(f[0]);
        }
        var feats = new List<JsonObject>();
        var missed = new List<string>();
        foreach (var (name, ents) in perCountry)
        {
            if (countries.Find(name) is not { } c) { missed.Add(name); continue; }
            feats.Add(Geo.Point(c.Lon, c.Lat, new JsonObject { ["name"] = c.Name, ["count"] = ents.Count, ["note"] = $"{ents.Count} OFAC SDN entries" }));
        }
        if (missed.Count > 0) log.LogInformation("Sanctions: unmatched countries: {List}", string.Join("; ", missed));
        return new LayerResult(Geo.Collection(feats), "OFAC SDN (address country)");
    });

    /// <summary>UCDP Georeferenced Event Dataset. The API requires a free access token (Ucdp:Token).</summary>
    public async Task<LayerResult> Ucdp(int hours)
    {
        var token = cfg["Ucdp:Token"];
        if (string.IsNullOrWhiteSpace(token))
            return new LayerResult(Geo.EmptyJson, "needs a UCDP access token (set Ucdp__Token)");
        var ver = cfg["Ucdp:Version"] ?? "25.1";
        return await cache.GetAsync("ucdp", TimeSpan.FromHours(6), async () =>
        {
            var from = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://ucdpapi.pcr.uu.se/api/gedevents/{ver}?pagesize=1000&StartDate={from}");
            req.Headers.Add("x-ucdp-access-token", token);
            using var res = await Client.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var doc = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
            var feats = new List<JsonObject>();
            foreach (var e in doc["Result"]?.AsArray() ?? [])
            {
                if (e is null || Geo.D(e["latitude"]) is not double lat || Geo.D(e["longitude"]) is not double lon) continue;
                feats.Add(Geo.Point(lon, lat, new JsonObject
                {
                    ["name"] = e["where_description"]?.ToString() ?? e["country"]?.ToString(),
                    ["note"] = $"{e["side_a"]} vs {e["side_b"]} — {e["best"]} deaths ({e["date_start"]?.ToString()?[..10]})",
                }));
            }
            return new LayerResult(Geo.Collection(feats), "UCDP GED (last 30 days)");
        });
    }

    private static List<string> ParseCsvLine(string line)
    {
        var res = new List<string>(); var sb = new System.Text.StringBuilder(); var q = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (q) { if (ch == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(ch); }
            else if (ch == '"') q = true;
            else if (ch == ',') { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        res.Add(sb.ToString());
        return res;
    }
}
