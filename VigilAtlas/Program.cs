using VigilAtlas;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("up", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("VigilAtlas/0.1 (+https://github.com/Craig-M-M9XDT/vigil-atlas)");
    c.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddResponseCompression();
builder.Services.AddSingleton<TtlCache>();
builder.Services.AddSingleton<CountryIndex>();
builder.Services.AddSingleton<Feeds>();
builder.Services.AddSingleton<OverpassService>();
builder.Services.AddSingleton<GdeltService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GdeltService>());

var app = builder.Build();
app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

const string NuclearQ = "[out:json][timeout:90];nwr[\"power\"=\"plant\"][\"plant:source\"=\"nuclear\"];out center tags;";
const string SpaceQ = "[out:json][timeout:90];nwr[\"aeroway\"=\"spaceport\"];out center tags;";
const string CablesQ = "[out:json][timeout:180];way[\"man_made\"=\"submarine_cable\"];out geom tags;";
const string BasesQ = "[out:json][timeout:120];nwr[\"military\"~\"^(base|airfield|naval_base)$\"][\"name\"](30,-12,72,60);out center tags;";

static int Hours(string? v) => v switch
{
    null or "" => 6,
    _ when v.EndsWith('d') && int.TryParse(v[..^1], out var d) => d * 24,
    _ when v.EndsWith('h') && int.TryParse(v[..^1], out var h) => h,
    _ => 6,
};

var layers = new Dictionary<string, Func<int, IServiceProvider, Task<LayerResult>>>
{
    ["conflicts"] = (h, sp) => Task.FromResult(sp.GetRequiredService<GdeltService>().Query(h)),
    ["ucdpEvents"] = (h, sp) => sp.GetRequiredService<Feeds>().Ucdp(h),
    ["military"] = (_, sp) => sp.GetRequiredService<Feeds>().Military(),
    ["quakes"] = (h, sp) => sp.GetRequiredService<Feeds>().Quakes(h),
    ["cables"] = (_, sp) => Task.FromResult(sp.GetRequiredService<OverpassService>().Get("cables", CablesQ, TimeSpan.FromDays(7), requireName: false)),
    ["radiationWatch"] = (_, sp) => sp.GetRequiredService<Feeds>().Radiation(),
    ["sanctions"] = (_, sp) => sp.GetRequiredService<Feeds>().Sanctions(),
    ["nuclear"] = (_, sp) => Task.FromResult(sp.GetRequiredService<OverpassService>().Get("nuclear", NuclearQ, TimeSpan.FromDays(7))),
    ["spaceports"] = (_, sp) => Task.FromResult(sp.GetRequiredService<OverpassService>().Get("spaceports", SpaceQ, TimeSpan.FromDays(7))),
    ["bases"] = (_, sp) => Task.FromResult(sp.GetRequiredService<OverpassService>().Get("bases", BasesQ, TimeSpan.FromDays(7))),
};
// Curated files shipped in wwwroot/data
var curated = new Dictionary<string, string>
{
    ["hotspots"] = "curated list", ["pipelines"] = "curated, approximate routes",
    ["irradiators"] = "no public data feed available",
};

app.MapGet("/api/layers/{name}", async (string name, string? hours, HttpContext ctx, IWebHostEnvironment env) =>
{
    LayerResult res;
    try
    {
        if (layers.TryGetValue(name, out var fn)) res = await fn(Hours(hours), ctx.RequestServices);
        else if (curated.TryGetValue(name, out var note))
        {
            var path = Path.Combine(env.WebRootPath, "data", name + ".geojson");
            res = new LayerResult(File.Exists(path) ? await File.ReadAllTextAsync(path) : Geo.EmptyJson, note);
        }
        else return Results.NotFound();
    }
    catch (Exception e)
    {
        app.Logger.LogWarning("layer {Name} failed: {Msg}", name, e.Message);
        return Results.Problem($"{name}: upstream failed", statusCode: 502);
    }
    if (res.Note is not null) ctx.Response.Headers["X-Layer-Note"] = Uri.EscapeDataString(res.Note);
    return Results.Content(res.Json, "application/json");
});

app.MapGet("/api/health", () => new { ok = true, time = DateTime.UtcNow });
app.Run();
