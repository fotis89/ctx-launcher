using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService(CopilotRunner runner, PathsService paths, CopilotService copilot)
{
    public (List<string> Args, List<string> SkippedDirs, string? NewSessionId) BuildLaunchArgs(
        Workspace ws, string? prompt = null,
        string? resumeSessionId = null, string? sharedDirPath = null, bool temporary = false,
        IReadOnlyList<string>? passThroughArgs = null)
    {
        var resolvedDirs = new List<string>();
        var skippedDirs = new List<string>();
        foreach (var dir in ws.AdditionalDirs)
        {
            var resolved = ResolveWorkspacePath(ws, dir);
            if (Directory.Exists(resolved))
                resolvedDirs.Add(resolved);
            else
                skippedDirs.Add(dir);
        }

        var spec = new LaunchSpec(ws, resolvedDirs, sharedDirPath, prompt, resumeSessionId, temporary, passThroughArgs);
        var result = copilot.BuildArgs(spec);
        return (result.Args, skippedDirs, result.NewSessionId);
    }

    public string BuildCommandString(Workspace ws, string? prompt = null,
        string? resumeSessionId = null, string? sharedDirPath = null, IReadOnlyList<string>? passThroughArgs = null)
    {
        var (args, _, _) = BuildLaunchArgs(ws, prompt, resumeSessionId, sharedDirPath, passThroughArgs: passThroughArgs);
        return "copilot " + string.Join(" ", args.Select(PathHelper.QuotePath));
    }

    public static string? LoadLastSession(Workspace ws)
    {
        if (!File.Exists(ws.LastSessionPath)) return null;
        var content = File.ReadAllText(ws.LastSessionPath).Trim();
        if (content.Length == 0) return null;
        if (content.Any(char.IsControl))
            throw new InvalidDataException(
                $"{ws.LastSessionPath}: expected a Copilot session ID. Delete this file to start a fresh session.");
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
        return runner.Run(ResolveWorkspacePath(ws, ws.PrimaryRepo), args, copilot.GetEnvironment(ws));
    }

    public string ResolveWorkspacePath(Workspace ws, string path)
        => PathHelper.ResolvePath(path, paths.Get, ws.FolderPath);
}