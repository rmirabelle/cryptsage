using System.Diagnostics;

namespace CryptSage.Core;

public sealed class VeraCryptLauncher
{
    private readonly string _mountExe;
    private readonly string _formatExe;

    public VeraCryptLauncher(string? installDir = null)
    {
        (_mountExe, _formatExe) = ResolveBinaries(installDir);

        if (!File.Exists(_mountExe))
            throw new FileNotFoundException("VeraCrypt mount executable not found", _mountExe);
        if (!File.Exists(_formatExe))
            throw new FileNotFoundException("VeraCrypt format executable not found", _formatExe);
    }

    private static (string mount, string format) ResolveBinaries(string? installDir)
    {
        // Use Environment.ProcessPath (not AppContext.BaseDirectory) for sibling lookups:
        // in single-file self-contained apps with IncludeAllContentForSelfExtract=true,
        // BaseDirectory is the extraction temp, not the exe's real location.
        if (installDir is null)
        {
            var exePath = Environment.ProcessPath;
            var exeDir = !string.IsNullOrEmpty(exePath)
                ? Path.GetDirectoryName(exePath)
                : AppContext.BaseDirectory;

            if (!string.IsNullOrEmpty(exeDir))
            {
                var bundled = Path.Combine(exeDir, "VeraCrypt");
                // Portable distribution uses arch-suffixed names; the driver (veracrypt-x64.sys)
                // sits next to VeraCrypt-x64.exe and is expected under that exact name.
                var portableX64 = Path.Combine(bundled, "VeraCrypt-x64.exe");
                if (File.Exists(portableX64))
                    return (portableX64, Path.Combine(bundled, "VeraCrypt Format-x64.exe"));
            }

            installDir = @"C:\Program Files\VeraCrypt";
        }

        // Installed variant: files get renamed to the generic names during install.
        var mount = Path.Combine(installDir, "VeraCrypt.exe");
        if (File.Exists(mount))
            return (mount, Path.Combine(installDir, "VeraCrypt Format.exe"));

        // Last resort: the portable naming used in the caller-supplied dir.
        return (
            Path.Combine(installDir, "VeraCrypt-x64.exe"),
            Path.Combine(installDir, "VeraCrypt Format-x64.exe"));
    }

    public async Task<LaunchResult> MountAsync(
        string volumePath,
        char driveLetter,
        string password,
        CancellationToken ct = default)
    {
        var args = new[]
        {
            "/v", volumePath,
            "/l", driveLetter.ToString(),
            "/p", password,
            "/hash", "sha512",
            "/q", "/s",
        };
        return await RunAsync(_mountExe, args, ct);
    }

    public async Task<LaunchResult> DismountAsync(char driveLetter, CancellationToken ct = default)
    {
        var args = new[] { "/u", driveLetter.ToString(), "/f", "/q", "/s" };
        return await RunAsync(_mountExe, args, ct);
    }

    public async Task<LaunchResult> DismountAllAsync(CancellationToken ct = default)
    {
        var args = new[] { "/u", "/f", "/q", "/s" };
        return await RunAsync(_mountExe, args, ct);
    }

    public async Task<LaunchResult> CreateAsync(
        string volumePath,
        long sizeBytes,
        string password,
        CancellationToken ct = default)
    {
        var args = new[]
        {
            "/create", volumePath,
            "/size", sizeBytes.ToString(),
            "/password", password,
            "/encryption", "AES",
            "/hash", "sha512",
            "/filesystem", "ExFAT",
            "/quick",
            "/silent",
        };
        return await RunAsync(_formatExe, args, ct);
    }

    private static async Task<LaunchResult> RunAsync(string exe, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {exe}");
        await proc.WaitForExitAsync(ct);
        return new LaunchResult(
            proc.ExitCode,
            await proc.StandardOutput.ReadToEndAsync(ct),
            await proc.StandardError.ReadToEndAsync(ct));
    }
}

public readonly record struct LaunchResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}
