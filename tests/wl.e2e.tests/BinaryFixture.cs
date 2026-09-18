using System.Diagnostics;

namespace wl.e2e.tests;

public static class BinaryFixture
{
    public static readonly string? ExePath = Resolve();

    public static string SkipReason => $"wl binary not found; set WL_BINARY_PATH or run 'dotnet publish src/wl -c Release -r <rid>' first";

    private static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable("WL_BINARY_PATH");
        if (!string.IsNullOrEmpty(env))
        {
            if (!File.Exists(env))
                throw new InvalidOperationException($"WL_BINARY_PATH does not exist: {env}");
            return ValidateBinary(Path.GetFullPath(env));
        }

        var exeName = OperatingSystem.IsWindows() ? "wl.exe" : "wl";
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var publishRoot = Path.Combine(dir, "src", "wl", "bin", "Release", "net10.0");
            if (Directory.Exists(publishRoot))
            {
                foreach (var ridDir in Directory.EnumerateDirectories(publishRoot))
                {
                    var candidate = Path.Combine(ridDir, "publish", exeName);
                    if (File.Exists(candidate))
                    {
                        return ValidateBinary(candidate);
                    }
                }
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string ValidateBinary(string path)
    {
        // Never run mutating tests against an old binary that ignores the
        // workspace-root override and writes into the real user profile.
        var psi = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("--help");
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not inspect wl binary.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("wl --help timed out.");
        }
        if (process.ExitCode != 0 || !output.GetAwaiter().GetResult().Contains("GitHub Copilot workspace launcher", StringComparison.Ordinal))
            throw new InvalidOperationException($"Stale or incompatible wl binary at {path}. Rebuild before running E2E tests. {error.GetAwaiter().GetResult()}");
        return path;
    }
}