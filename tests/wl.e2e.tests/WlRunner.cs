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
        psi.Environment["WL_WORKSPACES_ROOT"] = Path.Combine(tempHome, ".wl-workspaces");
        psi.Environment["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"] = tempHome;
        psi.WorkingDirectory = tempHome;

        // Do not let an installed Copilot executable win over the test shim.
        psi.Environment["PATH"] = extraPathDir ?? "";

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(30_000))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException("wl did not exit within 30 seconds.");
        }
        return new WlResult(p.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}