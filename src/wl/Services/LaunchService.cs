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
                args.Add($"--add-dir {PathHelper.QuotePath(resolved)}");
            }
            else
            {
                skippedDirs.Add(dir);
            }
        }

        args.Add($"--add-dir {PathHelper.QuotePath(ws.FolderPath)}");

        if (File.Exists(ws.InstructionsPath))
        {
            args.Add($"--append-system-prompt-file {PathHelper.QuotePath(ws.InstructionsPath)}");
        }

        if (!string.IsNullOrEmpty(prompt))
        {
            args.Add(PathHelper.QuotePath(prompt));
        }

        return (args, skippedDirs);
    }

    public string BuildCommandString(Workspace ws, string? prompt = null, bool yolo = false)
    {
        var (args, _) = BuildClaudeArgs(ws, prompt, yolo);
        return $"claude {string.Join(" \\\n       ", args)}";
    }

    public void Launch(Workspace ws, string? prompt = null, bool yolo = false)
    {
        var (args, _) = BuildClaudeArgs(ws, prompt, yolo);

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = string.Join(" ", args),
            WorkingDirectory = PathHelper.ResolveTilde(ws.PrimaryRepo),
            UseShellExecute = false,
        };

        var process = Process.Start(psi);
        process?.WaitForExit();
    }
}