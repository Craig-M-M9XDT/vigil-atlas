using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace VigilAtlas;

public sealed record LayerResult(string Json, string? Note = null);

public static class Geo
{
    public const string EmptyJson = "{\"type\":\"FeatureCollection\",\"features\":[]}";

    public static JsonObject Point(double lon, double lat, JsonObject props) => new()
    {
        ["type"] = "Feature",
        ["properties"] = props,
        ["geometry"] = new JsonObject { ["type"] = "Point", ["coordinates"] = new JsonArray(Math.Round(lon, 5), Math.Round(lat, 5)) },
    };

    public static string Collection(IEnumerable<JsonObject> features)
    {
        var arr = new JsonArray();
        foreach (var f in features) arr.Add(f);
        return new JsonObject { ["type"] = "FeatureCollection", ["features"] = arr }.ToJsonString();
    }

    public static double? D(JsonNode? n) => n is JsonValue v && v.TryGetValue(out double d) ? d : null;
}

/// <summary>Per-key TTL cache that serves stale data when the upstream fails.</summary>
public sealed class TtlCache
{
    private readonly ConcurrentDictionary<string, (DateTime At, LayerResult Value)> _items = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<LayerResult> GetAsync(string key, TimeSpan ttl, Func<Task<LayerResult>> factory)
    {
        if (_items.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < ttl) return hit.Value;
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (_items.TryGetValue(key, out hit) && DateTime.UtcNow - hit.At < ttl) return hit.Value;
            try
            {
                var fresh = await factory();
                _items[key] = (DateTime.UtcNow, fresh);
                return fresh;
            }
            catch when (_items.TryGetValue(key, out var stale))
            {
                return stale.Value with { Note = "stale: upstream unavailable" };
            }
        }
        finally { gate.Release(); }
    }
}
