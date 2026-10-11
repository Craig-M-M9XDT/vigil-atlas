using System.Text.Json.Nodes;

namespace VigilAtlas;

/// <summary>
/// Pulls static-ish layers (nuclear plants, spaceports, bases) from OpenStreetMap via Overpass.
/// Overpass is often busy, so results are cached on disk and refreshed in the background;
/// until the first success the bundled seed file is served.
/// </summary>
public sealed class OverpassService(IHttpClientFactory http, IWebHostEnvironment env, ILogger<OverpassService> log)
{
    private static readonly string[] Endpoints =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
    ];

    private readonly HashSet<string> _busy = new();

    private string CachePath(string key) => Path.Combine(env.ContentRootPath, "cache", key + ".geojson");
    private string SeedPath(string key) => Path.Combine(env.WebRootPath, "data", key + ".geojson");

    public LayerResult Get(string key, string query, TimeSpan ttl, bool requireName = true)
    {
        var path = CachePath(key);
        var exists = File.Exists(path);
        if (!exists || DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > ttl)
            StartRefresh(key, query, requireName);
        if (exists) return new LayerResult(File.ReadAllText(path), "OpenStreetMap (cached)");
        var seed = SeedPath(key);
        return new LayerResult(File.Exists(seed) ? File.ReadAllText(seed) : Geo.EmptyJson, "seed data; OSM refresh in progress");
    }

    private void StartRefresh(string key, string query, bool requireName)
    {
        lock (_busy) { if (!_busy.Add(key)) return; }
        _ = Task.Run(async () =>
        {
            try { await RefreshAsync(key, query, requireName); }
            catch (Exception e) { log.LogWarning("Overpass {Key} failed: {Msg}", key, e.Message); }
            finally { lock (_busy) _busy.Remove(key); }
        });
    }

    private async Task RefreshAsync(string key, string query, bool requireName)
    {
        var client = http.CreateClient("up");
        client.Timeout = TimeSpan.FromSeconds(120);
        foreach (var ep in Endpoints.Concat(Endpoints))
        {
            try
            {
                using var res = await client.PostAsync(ep, new FormUrlEncodedContent([new("data", query)]));
                if (!res.IsSuccessStatusCode) continue;
                var body = await res.Content.ReadAsStringAsync();
                if (!body.TrimStart().StartsWith('{')) continue; // HTML error page
                var json = Convert(JsonNode.Parse(body)!, requireName);
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath(key))!);
                await File.WriteAllTextAsync(CachePath(key), json);
                log.LogInformation("Overpass {Key} refreshed", key);
                return;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
            await Task.Delay(3000);
        }
        throw new InvalidOperationException("all Overpass endpoints failed");
    }

    private static string Convert(JsonNode root, bool requireName)
    {
        var feats = new List<JsonObject>();
        foreach (var el in root["elements"]?.AsArray() ?? [])
        {
            if (el is null) continue;
            if (el["geometry"] is JsonArray geom && geom.Count >= 2)
            {
                var coords = new JsonArray();
                foreach (var p in geom)
                    if (Geo.D(p?["lon"]) is double gx && Geo.D(p["lat"]) is double gy) coords.Add(new JsonArray(Math.Round(gx, 4), Math.Round(gy, 4)));
                feats.Add(new JsonObject
                {
                    ["type"] = "Feature",
                    ["properties"] = new JsonObject { ["name"] = el["tags"]?["name"]?.ToString() ?? "submarine cable", ["note"] = el["tags"]?["operator"]?.ToString() },
                    ["geometry"] = new JsonObject { ["type"] = "LineString", ["coordinates"] = coords },
                });
                continue;
            }
            var lat = Geo.D(el["lat"]) ?? Geo.D(el["center"]?["lat"]);
            var lon = Geo.D(el["lon"]) ?? Geo.D(el["center"]?["lon"]);
            if (lat is null || lon is null) continue;
            var tags = el["tags"];
            var name = tags?["name:en"]?.ToString() ?? tags?["name"]?.ToString();
            if (name is null && requireName) continue;
            feats.Add(Geo.Point(lon.Value, lat.Value, new JsonObject
            {
                ["name"] = name ?? "(unnamed)",
                ["note"] = tags?["operator"]?.ToString() ?? tags?["military"]?.ToString() ?? tags?["plant:output:electricity"]?.ToString(),
            }));
        }
        return Geo.Collection(feats);
    }
}
