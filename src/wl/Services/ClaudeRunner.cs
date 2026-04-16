using System.Diagnostics;

namespace wl.Services;

public class ClaudeRunner
{
    public virtual void Run(string workingDirectory, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
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
            Console.Error.WriteLine("Error: 'claude' not found.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Troubleshooting:");
            Console.Error.WriteLine("  1. Open a new terminal and run: claude --version");
            Console.Error.WriteLine("  2. If that works, restart this terminal (PATH may be stale)");
            Console.Error.WriteLine("  3. If not, install Claude Code and ensure 'claude' is in your PATH");
        }
    }
}
