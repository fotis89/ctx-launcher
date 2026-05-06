using System.Diagnostics;

using wl.Helpers;

namespace wl.Services;

public class ClaudeRunner
{
    public static string ResolveExecutable(string command, string? pathEnv = null, string? pathExtEnv = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return command;
        }

        return PathHelper.FindCommandOnPath(command, pathEnv, pathExtEnv)
            ?? command;
    }

    public virtual void Run(string command, string workingDirectory, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveExecutable(command),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"Error: '{command}' not found.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Troubleshooting:");
            Console.Error.WriteLine($"  1. Open a new terminal and run: {command} --version");
            Console.Error.WriteLine("  2. If that works, restart this terminal (PATH may be stale)");
            Console.Error.WriteLine($"  3. If not, install {command} and ensure its CLI is in your PATH");
        }
    }

    public virtual bool TryGetVersion(string command, out string version)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveExecutable(command),
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add("--version");

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                version = "";
                return false;
            }

            version = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            version = "";
            return false;
        }
    }
}
