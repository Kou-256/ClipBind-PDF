using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace ClipboardPDF;

/// <summary>新しいバージョンの情報</summary>
sealed record UpdateInfo(Version Version, string PageUrl, string? InstallerUrl);

/// <summary>GitHub Releases の最新版を確認する (送るのはアプリ名とバージョンだけ)</summary>
static class Updater
{
    const string LatestApi = "https://api.github.com/repos/Kou-256/ClipBind-PDF/releases/latest";
    public const string ReleasesPage = "https://github.com/Kou-256/ClipBind-PDF/releases/latest";

    /// <summary>実行中のアプリのバージョン (例: 1.2.0)</summary>
    public static Version Current
    {
        get
        {
            var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return Parse(info) ?? Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
        }
    }

    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Math.Max(0, Current.Build)}";

    /// <summary>"v1.2.0" や "1.2.0+abc" を Version に</summary>
    static Version? Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().TrimStart('v', 'V');
        int cut = s.IndexOfAny(['+', '-', ' ']);
        if (cut >= 0) s = s[..cut];
        return Version.TryParse(s, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : null;
    }

    /// <summary>新しい版があればその情報、なければ null。通信に失敗したら例外</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"ClipBindPDF/{CurrentText}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        using var res = await http.GetAsync(LatestApi, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var latest = Parse(root.GetProperty("tag_name").GetString());
        if (latest is null || latest <= Current) return null;

        string page = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesPage : ReleasesPage;
        string? installer = null;
        if (root.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.StartsWith("ClipBindPDF-Setup", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    installer = a.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        return new UpdateInfo(latest, page, installer);
    }

    public static void Open(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}
