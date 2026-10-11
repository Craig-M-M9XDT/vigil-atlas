using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VigilAtlas;

/// <summary>
/// Opens the dashboard in its own chrome-less app window (Edge/Chrome/Chromium "--app" mode) so the program
/// behaves like a desktop application. Closing that window shuts the server down.
/// Set Window=none (env var or --Window=none) for headless/service use, or Window=browser for the default browser.
/// </summary>
public static class WindowLauncher
{
    // Same layer set and view as the reference dashboard link.
    public const string DefaultQuery =
        "?lat=50&lon=15&zoom=3.5&view=eu&timeRange=6h&layers=hotspots,conflicts,bases,nuclear,irradiators,radiationWatch,spaceports,cables,pipelines,military,ucdpEvents,weather,sanctions";

    public static void Launch(string baseUrl, string mode, IHostApplicationLifetime lifetime, ILogger log)
    {
        if (mode.Equals("none", StringComparison.OrdinalIgnoreCase)) return;
        var url = baseUrl.TrimEnd('/') + "/" + DefaultQuery;
        try
        {
            if (!mode.Equals("browser", StringComparison.OrdinalIgnoreCase) && FindBrowser() is { } exe)
            {
                var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VigilAtlas", "profile");
                Directory.CreateDirectory(profile);
                var psi = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    ArgumentList =
                    {
                        $"--app={url}", $"--user-data-dir={profile}", "--window-size=1500,900",
                        "--no-first-run", "--no-default-browser-check",
                    },
                };
                var started = DateTime.UtcNow;
                var proc = Process.Start(psi);
                if (proc is not null)
                {
                    proc.EnableRaisingEvents = true;
                    // Only treat an exit as "user closed the window" if it lived a while (not a hand-off to another instance).
                    proc.Exited += (_, _) => { if (DateTime.UtcNow - started > TimeSpan.FromSeconds(8)) lifetime.StopApplication(); };
                    return;
                }
            }
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e) { log.LogWarning("Could not open a window ({Msg}); browse to {Url}", e.Message, url); }
    }

    private static string? FindBrowser()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string[] candidates =
            [
                Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
            ];
            return candidates.FirstOrDefault(File.Exists);
        }
        // Linux / Raspberry Pi: only if a desktop session exists
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) return null;
        foreach (var name in new[] { "chromium-browser", "chromium", "google-chrome", "microsoft-edge" })
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
                if (File.Exists(Path.Combine(dir, name))) return Path.Combine(dir, name);
        return null;
    }
}
