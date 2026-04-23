using System.Diagnostics;

namespace wl.e2e.tests;

public sealed record WlResult(int ExitCode, string Stdout, string Stderr);

public static class WlRunner
{
    public static WlResult Run(string tempHome, string? extraPathDir, params string[] args)
    {
        if (BinaryFixture.ExePath is null)
        {
            throw new InvalidOperationException(BinaryFixture.SkipReason);
        }

        var psi = new ProcessStartInfo(BinaryFixture.ExePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        psi.Environment["HOME"] = tempHome;
        psi.Environment["USERPROFILE"] = tempHome;

        if (!string.IsNullOrEmpty(extraPathDir))
        {
            var sep = OperatingSystem.IsWindows() ? ';' : ':';
            var existing = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.Environment["PATH"] = $"{extraPathDir}{sep}{existing}";
        }

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return new WlResult(p.ExitCode, stdout, stderr);
    }
}
