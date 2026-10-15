using System.Collections.Concurrent;
using System.Text.Json;

namespace VigilAtlas;

/// <summary>Server-side data proxies; API keys never reach the browser.</summary>
public sealed class MarketService(IHttpClientFactory factory, IConfiguration config)
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset Until, string Json)> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    private static readonly HashSet<string> Commodities = new(StringComparer.OrdinalIgnoreCase) { "WTI", "BRENT", "NATURAL_GAS" };

    private async Task<string> Cached(string cacheKey, TimeSpan ttl, Func<Task<string>> load)
    {
        if (_cache.TryGetValue(cacheKey, out var entry) && entry.Until > DateTimeOffset.UtcNow) return entry.Json;
        var gate = _gates.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (_cache.TryGetValue(cacheKey, out entry) && entry.Until > DateTimeOffset.UtcNow) return entry.Json;
            try
            {
                var json = await load();
                _cache[cacheKey] = (DateTimeOffset.UtcNow.Add(ttl), json);
                return json;
            }
            catch when (_cache.TryGetValue(cacheKey, out entry)) { return entry.Json; }
        }
        finally { gate.Release(); }
    }

    private async Task<string> Alpha(string function, string? symbol, TimeSpan ttl)
    {
        var key = config["AlphaVantage:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return JsonSerializer.Serialize(new { error = "Set AlphaVantage__ApiKey on the server.", configured = false });
        var id = function + ":" + symbol;
        return await Cached(id, ttl, async () =>
        {
            var uri = "https://www.alphavantage.co/query?function=" + Uri.EscapeDataString(function)
                    + (symbol is null ? "" : "&symbol=" + Uri.EscapeDataString(symbol))
                    + "&apikey=" + Uri.EscapeDataString(key);
            using var res = await factory.CreateClient("up").GetAsync(uri);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Note", out _) || doc.RootElement.TryGetProperty("Information", out _))
                throw new HttpRequestException("Data provider rate limit or subscription restriction.");
            if (doc.RootElement.TryGetProperty("Error Message", out _)) throw new HttpRequestException("Unknown ticker or unsupported feed.");
            return json;
        });
    }

    public Task<string> Quote(string ticker) => Alpha("GLOBAL_QUOTE", ticker, TimeSpan.FromMinutes(15));
    public Task<string> Analyst(string ticker) => Alpha("OVERVIEW", ticker, TimeSpan.FromHours(24));
    public Task<string> Commodity(string name) => Commodities.Contains(name)
        ? Alpha(name.ToUpperInvariant(), null, TimeSpan.FromHours(2))
        : Task.FromResult("{\"error\":\"Unsupported commodity\"}");

    public Task<string> Weather(double latitude, double longitude) => Cached(
        $"weather:{latitude:F2}:{longitude:F2}", TimeSpan.FromMinutes(15), async () =>
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m,relative_humidity_2m,precipitation,weather_code,wind_speed_10m&timezone=auto";
            return await factory.CreateClient("up").GetStringAsync(url);
        });
}
