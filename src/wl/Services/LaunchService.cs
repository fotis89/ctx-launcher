using wl.Helpers;
using wl.Models;
using System.Text.Json;

namespace wl.Services;

public class LaunchService(CopilotRunner runner, PathsService paths, CopilotService copilot, WlPaths wlPaths)
{
    public (List<string> Args, List<string> SkippedDirs, string? NewSessionId) BuildLaunchArgs(
        Workspace ws, string? resumeSessionId = null, string? sharedDirPath = null, bool temporary = false,
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

        var spec = new LaunchSpec(ws, ResolveWorkspacePath(ws, ws.PrimaryRepo), SessionSlug(ws.FolderName), ws.CopilotArgs, resolvedDirs, sharedDirPath, resumeSessionId, temporary, passThroughArgs);
        var result = copilot.BuildArgs(spec);
        return (result.Args, skippedDirs, result.NewSessionId);
    }

    public (List<string> Args, string? NewSessionId) BuildFolderLaunchArgs(
        string folderPath, string? resumeSessionId = null, string? sharedDirPath = null, bool temporary = false,
        IReadOnlyList<string>? passThroughArgs = null)
    {
        var spec = new LaunchSpec(null, folderPath, SessionSlug(Path.GetFileName(folderPath)), [], [], sharedDirPath, resumeSessionId, temporary, passThroughArgs);
        var result = copilot.BuildArgs(spec);
        return (result.Args, result.NewSessionId);
    }

    public string BuildCommandString(Workspace ws,
        string? resumeSessionId = null, string? sharedDirPath = null, IReadOnlyList<string>? passThroughArgs = null)
    {
        var (args, _, _) = BuildLaunchArgs(ws, resumeSessionId, sharedDirPath, passThroughArgs: passThroughArgs);
        return "copilot " + string.Join(" ", args.Select(PathHelper.QuotePath));
    }

    public string BuildFolderCommandString(string folderPath, string? resumeSessionId = null, string? sharedDirPath = null, IReadOnlyList<string>? passThroughArgs = null)
    {
        var (args, _) = BuildFolderLaunchArgs(folderPath, resumeSessionId, sharedDirPath, passThroughArgs: passThroughArgs);
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

    public string? LoadFolderSession(string folderPath)
    {
        var sessions = LoadFolderSessions();
        return sessions.TryGetValue(FolderSessionKey(folderPath), out var session) && !string.IsNullOrWhiteSpace(session)
            ? session
            : null;
    }

    public void SaveFolderSession(string folderPath, string sessionId)
    {
        var sessions = LoadFolderSessions();
        sessions[FolderSessionKey(folderPath)] = sessionId;
        Directory.CreateDirectory(Path.GetDirectoryName(wlPaths.FolderSessionsFile)!);
        var tmp = wlPaths.FolderSessionsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(sessions, WlJsonContext.Default.DictionaryStringString));
            File.Move(tmp, wlPaths.FolderSessionsFile, overwrite: true);
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

    public int LaunchFolder(string folderPath, List<string> args)
    {
        copilot.PrepareLaunch(null);
        return runner.Run(folderPath, args, copilot.GetEnvironment(folderPath));
    }

    public string ResolveWorkspacePath(Workspace ws, string path)
        => PathHelper.ResolvePath(path, paths.Get, ws.FolderPath);

    public void EnsureWorkspaceVariables(Workspace ws)
        => paths.EnsureVariables(PathHelper.ExtractVariables(ws.PrimaryRepo)
            .Concat(ws.AdditionalDirs.SelectMany(PathHelper.ExtractVariables)));

    private Dictionary<string, string> LoadFolderSessions()
    {
        if (!File.Exists(wlPaths.FolderSessionsFile))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(wlPaths.FolderSessionsFile), WlJsonContext.Default.DictionaryStringString)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{wlPaths.FolderSessionsFile}: invalid folder sessions JSON ({ex.Message}).", ex);
        }
    }

    private static string FolderSessionKey(string folderPath)
    {
        var full = Path.GetFullPath(folderPath);
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    private static string SessionSlug(string name)
    {
        var slug = PathHelper.Slugify(name);
        return string.IsNullOrEmpty(slug) ? "wl" : slug;
    }
}