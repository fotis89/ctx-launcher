using System.Diagnostics;
using System.Text.RegularExpressions;

using wl.Helpers;

namespace wl.Services;

public class CopilotRunner
{
    private static readonly Regex NpmShimScriptRegex = new(@"""%dp0%\\([^""]+)""", RegexOptions.IgnoreCase);
    private static readonly char[] CmdArgumentMetacharacters = ['"', '%', '!', '^', '&', '|', '<', '>', '\r', '\n'];

    public static string ResolveExecutable(string command, string? pathEnv = null, string? pathExtEnv = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return command;
        }

        return PathHelper.FindCommandOnPath(command, pathEnv, pathExtEnv)
            ?? command;
    }

    public static (string FileName, IReadOnlyList<string> PrefixArgs)? TryResolveNpmShim(
        string shimPath,
        string shimContent,
        Func<string, bool> fileExists,
        Func<string, string?> commandResolver)
    {
        var shimDir = Path.GetDirectoryName(shimPath);
        if (string.IsNullOrEmpty(shimDir))
        {
            return null;
        }

        var match = NpmShimScriptRegex.Matches(shimContent)
            .Cast<Match>()
            .LastOrDefault(m => !string.Equals(Path.GetFileName(m.Groups[1].Value), "node.exe", StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }

        var relativeScriptPath = match.Groups[1].Value;
        var scriptPath = Path.GetFullPath(Path.Combine([shimDir, .. relativeScriptPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)]));
        if (!fileExists(scriptPath))
        {
            return null;
        }

        var localNode = Path.Combine(shimDir, "node.exe");
        var node = fileExists(localNode)
            ? localNode
            : commandResolver("node") ?? "node";

        return (node, [scriptPath]);
    }

    public static bool ContainsCmdArgumentMetacharacter(string arg)
        => arg.IndexOfAny(CmdArgumentMetacharacters) >= 0;

    public static bool TryCreateProcessStartInfo(
        string command,
        string? workingDirectory,
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string>? environment,
        bool redirectStandardOutput,
        out ProcessStartInfo psi,
        out int exitCode)
    {
        var executable = ResolveExecutable(command);
        var argumentList = args.ToList();
        IReadOnlyList<string> prefixArgs = [];
        if (OperatingSystem.IsWindows() && IsBatchFile(executable))
        {
            (string FileName, IReadOnlyList<string> PrefixArgs)? shim = null;
            try
            {
                shim = TryResolveNpmShim(
                    executable,
                    File.ReadAllText(executable),
                    File.Exists,
                    commandName => PathHelper.FindCommandOnPath(commandName));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Fall back to the guarded cmd.exe path below.
            }

            if (shim is not null)
            {
                executable = shim.Value.FileName;
                prefixArgs = shim.Value.PrefixArgs;
            }
            else if (argumentList.Any(ContainsCmdArgumentMetacharacter))
            {
                Console.Error.WriteLine($"Error: '{command}' resolves to a batch file ({executable}); refusing to pass an argument containing cmd.exe metacharacters. Install Copilot CLI as an executable (e.g. winget) or remove these characters.");
                psi = new ProcessStartInfo();
                exitCode = 1;
                return false;
            }
        }

        psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = redirectStandardOutput,
        };
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }
        foreach (var arg in prefixArgs.Concat(argumentList))
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

        exitCode = 0;
        return true;
    }

    private static bool IsBatchFile(string executable)
        => Path.GetExtension(executable) is var extension
            && (string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase));

    public virtual int Run(string workingDirectory, IEnumerable<string> args, IReadOnlyDictionary<string, string>? environment = null)
    {
        const string command = "copilot";
        if (!TryCreateProcessStartInfo(command, workingDirectory, args, environment, redirectStandardOutput: false, out var psi, out var exitCode))
        {
            return exitCode;
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
        if (!TryCreateProcessStartInfo(command, workingDirectory: null, ["--version"], environment: null, redirectStandardOutput: true, out var psi, out _))
        {
            version = "";
            return false;
        }

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
