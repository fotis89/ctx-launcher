using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService(ClaudeRunner claudeRunner, PathsService paths)
{
    private Func<string, string?> Lookup => paths.Get;


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

        foreach (var dir in ws.AdditionalDirs)
        {
            var (exists, resolved) = PathHelper.ValidatePath(dir, Lookup);
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

        if (yolo)
        {
            args.Add("--dangerously-skip-permissions");
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

        var groups = new List<string> { "claude" };
        var current = "";
        foreach (var arg in args)
        {
            if (arg.StartsWith("--"))
            {
                if (current.Length > 0) groups.Add(current);
                current = arg;
            }
            else
            {
                current += " " + PathHelper.QuotePath(arg);
            }
        }
        if (current.Length > 0) groups.Add(current);

        var continuation = OperatingSystem.IsWindows() ? " `" : " \\";
        return string.Join(continuation + Environment.NewLine + "      ", groups);
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
        => claudeRunner.Run(PathHelper.ResolvePath(ws.PrimaryRepo, Lookup), args);
}
