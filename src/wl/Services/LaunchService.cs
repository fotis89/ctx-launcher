using System.Diagnostics;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService
{
    public (List<string> Args, List<string> SkippedDirs) BuildClaudeArgs(Workspace ws, string? prompt = null, bool yolo = false)
    {
        var args = new List<string>();
        var skippedDirs = new List<string>();

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

        return (args, skippedDirs);
    }

    public string BuildCommandString(Workspace ws, string? prompt = null, bool yolo = false)
    {
        var (args, _) = BuildClaudeArgs(ws, prompt, yolo);
        var quoted = args.Select(a => a.StartsWith("--") ? a : PathHelper.QuotePath(a));
        return $"claude {string.Join(" ", quoted)}";
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

        var process = Process.Start(psi);
        process?.WaitForExit();
    }
}
