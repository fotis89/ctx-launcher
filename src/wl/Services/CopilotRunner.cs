using System.Diagnostics;

using wl.Helpers;

namespace wl.Services;

public class CopilotRunner
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

    public virtual int Run(string workingDirectory, IEnumerable<string> args, IReadOnlyDictionary<string, string>? environment = null)
    {
        const string command = "copilot";
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
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                psi.Environment[key] = value;
            }
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("Error: could not start Copilot.");
                return 1;
            }
            process.WaitForExit();
            if (process.ExitCode != 0)
                Console.Error.WriteLine($"Error: Copilot exited with code {process.ExitCode}; saved session pointers were not changed.");
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.Error.WriteLine(ex.NativeErrorCode == 2
                ? $"Error: '{command}' not found."
                : $"Error: could not start '{command}': {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Troubleshooting:");
            Console.Error.WriteLine($"  1. Open a new terminal and run: {command} --version");
            Console.Error.WriteLine("  2. If that works, restart this terminal (PATH may be stale)");
            Console.Error.WriteLine($"  3. If not, install {command} and ensure its CLI is in your PATH");
            return 1;
        }
    }

    public virtual bool TryGetVersion(out string version)
    {
        const string command = "copilot";
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