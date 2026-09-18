using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService(CopilotRunner runner, PathsService paths, CopilotService copilot)
{
    public (List<string> Args, List<string> SkippedDirs, string? NewSessionId) BuildLaunchArgs(
        Workspace ws, string? prompt = null, bool yolo = false,
        string? resumeSessionId = null, string? sharedDirPath = null)
    {
        var resolvedDirs = new List<string>();
        var skippedDirs = new List<string>();
        foreach (var dir in ws.AdditionalDirs)
        {
            var resolved = PathHelper.ResolvePath(dir, paths.Get);
            if (Directory.Exists(resolved))
                resolvedDirs.Add(resolved);
            else
                skippedDirs.Add(dir);
        }

        var spec = new LaunchSpec(ws, resolvedDirs, sharedDirPath, prompt, yolo, resumeSessionId);
        var result = copilot.BuildArgs(spec);
        return (result.Args, skippedDirs, result.NewSessionId);
    }

    public string BuildCommandString(Workspace ws, string? prompt = null, bool yolo = false,
        string? resumeSessionId = null, string? sharedDirPath = null)
    {
        var (args, _, _) = BuildLaunchArgs(ws, prompt, yolo, resumeSessionId, sharedDirPath);
        return "copilot " + string.Join(" ", args.Select(PathHelper.QuotePath));
    }

    public static string? LoadLastSession(Workspace ws)
    {
        if (!File.Exists(ws.LastSessionPath)) return null;
        var content = File.ReadAllText(ws.LastSessionPath).Trim();
        if (content.Length == 0 || content.StartsWith('{') || content.StartsWith('[') ||
            content.Any(char.IsControl))
            throw new InvalidDataException(
                $"{ws.LastSessionPath}: expected a plain Copilot session reference. " +
                "Copy only the old map's 'copilot' value into this file, or remove it and start fresh. Claude sessions are not supported.");
        return content;
    }

    public static void SaveLastSession(Workspace ws, string sessionId)
    {
        var tmp = ws.LastSessionPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, sessionId);
            File.Move(tmp, ws.LastSessionPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public int Launch(Workspace ws, List<string> args)
    {
        copilot.PrepareLaunch(ws);
        return runner.Run(PathHelper.ResolvePath(ws.PrimaryRepo, paths.Get), args, copilot.GetEnvironment(ws));
    }
}