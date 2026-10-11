using System.Text;
using System.Text.Json;

namespace VigilAtlas;

/// <summary>Country-name → centroid lookup (data © mledoze/countries, ODbL).</summary>
public sealed class CountryIndex
{
    private readonly Dictionary<string, (double Lat, double Lon, string Name)> _byName = new();

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["burma"] = "myanmar", ["korea north"] = "north korea", ["korea south"] = "south korea",
        ["congo democratic republic of the"] = "dr congo", ["congo republic of the"] = "republic of the congo",
        ["cote d ivoire"] = "ivory coast", ["czech republic"] = "czechia", ["gambia the"] = "gambia",
        ["bahamas the"] = "bahamas", ["turkey"] = "turkiye", ["russian federation"] = "russia",
        ["west bank"] = "palestine", ["gaza strip"] = "palestine", ["hong kong sar"] = "hong kong",
    };

    public CountryIndex(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "countries.json");
        if (!File.Exists(path)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var lat = c.GetProperty("lat").GetDouble();
            var lon = c.GetProperty("lon").GetDouble();
            var names = c.GetProperty("n").EnumerateArray().Select(n => n.GetString()!).ToList();
            foreach (var n in names) _byName.TryAdd(Norm(n), (lat, lon, names[0]));
        }
    }

    public static string Norm(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public (double Lat, double Lon, string Name)? Find(string name)
    {
        var n = Norm(name);
        if (Aliases.TryGetValue(n, out var a)) n = a;
        return _byName.TryGetValue(n, out var hit) ? hit : null;
    }
}
