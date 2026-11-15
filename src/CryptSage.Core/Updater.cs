using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace CryptSage.Core;

public sealed record UpdateInfo(string LatestVersion, string DownloadUrl, string AssetName);

/**
 * In-app update from the public GitHub releases, the same protocol as the
 * other Sage apps: read releases/latest, download the "-setup.exe" asset,
 * run it with no setup pages, and close the app.
 */
public static class Updater
{
    private const string LatestReleaseApi = "https://api.github.com/repos/rmirabelle/cryptsage/releases/latest";
    private const string ReleaseDownloadPrefix = "https://github.com/rmirabelle/cryptsage/releases/download/";
    private const string UserAgent = "CryptSage-Updater";

    /**
     * Inno Setup flags for an unattended upgrade: no wizard pages, no message
     * boxes, and no Windows restart. The [Run] section of CryptSage.iss starts
     * the app again after a silent install.
     */
    private static readonly string[] InstallerArgs = ["/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART"];

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    /** Returns the newer release, or null when the app is up to date or no release exists. */
    public static async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        var root = json.RootElement;

        var latest = (root.GetProperty("tag_name").GetString() ?? "").TrimStart('v');
        if (!IsNewer(latest, currentVersion)) return null;

        (string Name, string Url)? installer = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
            {
                installer = (name, url);
                break;
            }
            if (installer is null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                installer = (name, url);
        }
        if (installer is null)
            throw new InvalidOperationException("The latest release does not contain a Windows installer.");

        return new UpdateInfo(latest, installer.Value.Url, installer.Value.Name);
    }

    /** Downloads the installer to the temp folder and returns its path. Progress is 0 to 1. */
    public static async Task<string> DownloadAsync(
        UpdateInfo info,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (!info.DownloadUrl.StartsWith(ReleaseDownloadPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("The update URL is not an official Crypt Sage release.");
        if (info.AssetName.IndexOfAny(['/', '\\']) >= 0 ||
            !info.AssetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The update asset name is invalid.");

        var target = Path.Combine(Path.GetTempPath(), info.AssetName);

        using var client = CreateClient();
        using var response = await client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var file = File.Create(target))
        {
            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                if (total > 0) progress?.Report((double)downloaded / total);
            }
        }
        return target;
    }

    /** Starts the installer. The caller must close the app immediately after. */
    public static void RunInstaller(string installerPath)
    {
        var psi = new ProcessStartInfo { FileName = installerPath, UseShellExecute = false };
        foreach (var arg in InstallerArgs) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {installerPath}");
    }

    public static bool IsNewer(string latest, string current)
    {
        var a = ParseVersion(latest);
        var b = ParseVersion(current);
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    private static uint[] ParseVersion(string version)
    {
        var start = 0;
        while (start < version.Length && !char.IsAsciiDigit(version[start])) start++;
        return version[start..]
            .Split('.')
            .Take(4)
            .Select(part => new string(part.TakeWhile(char.IsAsciiDigit).ToArray()))
            .Select(digits => uint.TryParse(digits, out var n) ? n : 0)
            .ToArray();
    }
}
