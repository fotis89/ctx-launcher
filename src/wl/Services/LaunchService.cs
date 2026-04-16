using System.Diagnostics;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService
{
    public (List<string> Args, List<string> SkippedDirs, string? NewSessionId) BuildClaudeArgs(Workspace ws, string? prompt = null, bool yolo = false, string? resumeSessionId = null, string? sharedDirPath = null)
    {
        var args = new List<string>();
        var skippedDirs = new List<string>();
        string? newSessionId = null;

        if (resumeSessionId is not null)
        {
            args.Add("--resume");
            args.Add(resumeSessionId);
        }
        else
        {
            newSessionId = Guid.NewGuid().ToString();
            args.Add("--session-id");
            args.Add(newSessionId);
            args.Add("--name");
            args.Add(ws.Name);
        }

        if (yolo)
        {
            args.Add("--dangerously-skip-permissions");
        }

        foreach (var dir in ws.AdditionalDirs)
        {
            var (exists, resolved) = PathHelper.ValidatePath(dir);
            if (exists)
            {
                args.Add("--add-dir");
                args.Add(resolved);
            }
            else
            {
                skippedDirs.Add(dir);
            }
        }

        if (sharedDirPath is not null)
        {
            args.Add("--add-dir");
            args.Add(sharedDirPath);
        }

        args.Add("--add-dir");
        args.Add(ws.FolderPath);

        if (File.Exists(ws.InstructionsPath))
        {
            args.Add("--append-system-prompt-file");
            args.Add(ws.InstructionsPath);
        }

        if (!string.IsNullOrEmpty(prompt))
        {
            args.Add(prompt);
        }

        return (args, skippedDirs, newSessionId);
    }

    public string BuildCommandString(Workspace ws, string? prompt = null, bool yolo = false, string? resumeSessionId = null, string? sharedDirPath = null)
    {
        var (args, _, _) = BuildClaudeArgs(ws, prompt, yolo, resumeSessionId, sharedDirPath);
        var quoted = args.Select(a => a.StartsWith("--") ? a : PathHelper.QuotePath(a));
        return $"claude {string.Join(" ", quoted)}";
    }

    public static string? LoadLastSession(Workspace ws)
    {
        var path = Path.Combine(ws.FolderPath, ".last-session");
        if (!File.Exists(path))
            return null;
        var value = File.ReadAllText(path).Trim();
        return Guid.TryParse(value, out _) ? value : null;
    }

    public static void SaveLastSession(Workspace ws, string sessionId)
    {
        File.WriteAllText(Path.Combine(ws.FolderPath, ".last-session"), sessionId);
    }

    public void Launch(Workspace ws, List<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            WorkingDirectory = PathHelper.ResolveTilde(ws.PrimaryRepo),
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
