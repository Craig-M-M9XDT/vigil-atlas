using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VigilAtlas;

/// <summary>
/// Keeps a rolling 24h window of geocoded violent-event records from the GDELT 2.0 event stream
/// (15-minute files, no API key) and serves them as the "conflicts" layer.
/// </summary>
public sealed partial class GdeltService(IHttpClientFactory http, ILogger<GdeltService> log) : BackgroundService
{
    private const int Files24h = 96;
    private const string Base = "https://data.gdeltproject.org/gdeltv2/";

    private sealed record Ev(long Id, DateTime T, string Name, double Lat, double Lon, int Root, int Mentions, string Url);

    private readonly object _lock = new();
    private readonly Dictionary<long, Ev> _events = new();
    private readonly HashSet<string> _files = new();
    private int _loadedFiles;
    private readonly Dictionary<int, (DateTime Expires, long Version, LayerResult Value)> _queryCache = new();
    private long _version;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var latest = await LatestStampAsync(ct);
                if (latest is not null)
                {
                    var stamps = Enumerable.Range(0, Files24h).Select(i => latest.Value.AddMinutes(-15 * i))
                                           .Where(s => !_files.Contains(Stamp(s))).ToList();
                    await Parallel.ForEachAsync(stamps, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                        async (s, c) => await LoadFileAsync(s, c));
                    Prune();
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { log.LogWarning("GDELT refresh failed: {Msg}", e.Message); }
            await Task.Delay(TimeSpan.FromMinutes(5), ct).ContinueWith(_ => { });
        }
    }

    private static string Stamp(DateTime t) => t.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private async Task<DateTime?> LatestStampAsync(CancellationToken ct)
    {
        var txt = await http.CreateClient("up").GetStringAsync(Base + "lastupdate.txt", ct);
        var m = StampRx().Match(txt);
        return m.Success ? DateTime.ParseExact(m.Groups[1].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal) : null;
    }

    private async Task LoadFileAsync(DateTime stamp, CancellationToken ct)
    {
        var key = Stamp(stamp);
        try
        {
            var bytes = await http.CreateClient("up").GetByteArrayAsync($"{Base}{key}.export.CSV.zip", ct);
            using var zip = new ZipArchive(new MemoryStream(bytes));
            using var rd = new StreamReader(zip.Entries[0].Open());
            var batch = new List<Ev>();
            string? line;
            while ((line = await rd.ReadLineAsync(ct)) is not null)
            {
                var c = line.Split('\t');
                if (c.Length < 61 || !int.TryParse(c[28], out var root) || root is < 18 or > 20) continue;
                if (c[51] is not ("3" or "4")) continue; // city-level geocodes only
                if (!double.TryParse(c[56], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(c[57], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;
                _ = long.TryParse(c[0], out var id);
                _ = int.TryParse(c[31], out var mentions);
                var added = DateTime.TryParseExact(c[59], "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) ? t : stamp;
                batch.Add(new Ev(id, added, c[52], lat, lon, root, Math.Max(1, mentions), c[60]));
            }
            lock (_lock)
            {
                foreach (var e in batch) _events[e.Id] = e;
                _files.Add(key);
                _loadedFiles++;
                _version++;
                _queryCache.Clear();
            }
        }
        catch (HttpRequestException) { lock (_lock) _files.Add(key); } // missing file: don't retry forever
    }

    private void Prune()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        lock (_lock)
        {
            foreach (var k in _events.Where(kv => kv.Value.T < cutoff).Select(kv => kv.Key).ToList()) _events.Remove(k);
            foreach (var f in _files.Where(f => DateTime.ParseExact(f, "yyyyMMddHHmmss", CultureInfo.InvariantCulture) < cutoff).ToList()) _files.Remove(f);
            _queryCache.Clear();
        }
    }

    private static string Kind(int root) => root switch { 20 => "Mass violence", 19 => "Fight", _ => "Assault" };

    public LayerResult Query(int hours, int max = 1500)
    {
        var cutoff = DateTime.UtcNow.AddHours(-Math.Clamp(hours, 1, 24));
        List<Ev> window;
        int files;
        lock (_lock)
        {
            if (_queryCache.TryGetValue(hours, out var cached) && cached.Expires > DateTime.UtcNow && cached.Version == _version)
                return cached.Value;
            window = _events.Values.Where(e => e.T >= cutoff).ToList();
            files = _loadedFiles;
        }

        var features = window.GroupBy(e => (Math.Round(e.Lat, 2), Math.Round(e.Lon, 2)))
            .Select(g =>
            {
                var top = g.MaxBy(e => e.Mentions)!;
                return (Score: g.Sum(e => e.Mentions), g, top);
            })
            .OrderByDescending(x => x.Score).Take(max)
            .Select(x => Geo.Point(x.top.Lon, x.top.Lat, new JsonObject
            {
                ["name"] = x.top.Name,
                ["kind"] = Kind(x.g.Max(e => e.Root)),
                ["count"] = x.g.Count(),
                ["mentions"] = x.Score,
                ["url"] = x.top.Url,
                ["latest"] = x.g.Max(e => e.T).ToString("u"),
            }));
        var note = files == 0 ? "GDELT warming up…" : $"GDELT {Math.Min(files, Files24h)}/{Files24h} files loaded";
        var result = new LayerResult(Geo.Collection(features), note);
        lock (_lock) _queryCache[hours] = (DateTime.UtcNow.AddSeconds(20), _version, result);
        return result;
    }

    [GeneratedRegex(@"(\d{14})\.export")] private static partial Regex StampRx();
}
