using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService(ClaudeRunner claudeRunner, PathsService paths, ToolAdapterRegistry adapters, ConfigService config)
{
    private Func<string, string?> Lookup => paths.Get;


    public (List<string> Args, List<string> SkippedDirs, string? NewSessionId) BuildClaudeArgs(Workspace ws, string? prompt = null, bool yolo = false, string? resumeSessionId = null, string? sharedDirPath = null, string? toolOverride = null)
    {
        if (!config.TryResolveValidatedTool(ws, toolOverride, adapters, out var tool))
        {
            return ([], [], null);
        }
        var adapter = adapters.Resolve(tool);
        var resolvedDirs = new List<string>();
        var skippedDirs = new List<string>();

        foreach (var dir in ws.AdditionalDirs)
        {
            var (exists, resolved) = PathHelper.ValidatePath(dir, Lookup);
            if (exists)
            {
                resolvedDirs.Add(resolved);
            }
            else
            {
                skippedDirs.Add(dir);
            }
        }

        var spec = new AdapterLaunchSpec(
            Workspace: ws,
            ResolvedAdditionalDirs: resolvedDirs,
            ResolvedSharedDir: sharedDirPath,
            Prompt: prompt,
            Yolo: yolo,
            ResumeSessionId: resumeSessionId);

        var result = adapter.BuildArgs(spec);
        return (result.Args, skippedDirs, result.NewSessionId);
    }

    public string BuildCommandString(Workspace ws, string? prompt = null, bool yolo = false, string? resumeSessionId = null, string? sharedDirPath = null)
    {
        if (!config.TryResolveValidatedTool(ws, overrideTool: null, adapters, out var tool))
        {
            return "(unable to build command — see error above)";
        }
        var adapter = adapters.Resolve(tool);
        var (args, _, _) = BuildClaudeArgs(ws, prompt, yolo, resumeSessionId, sharedDirPath);

        var groups = new List<string> { adapter.DisplayName };
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
        var path = ws.LastSessionPath;
        if (!File.Exists(path))
            return null;
        var value = File.ReadAllText(path).Trim();
        return Guid.TryParse(value, out _) ? value : null;
    }

    public static void SaveLastSession(Workspace ws, string sessionId)
    {
        File.WriteAllText(ws.LastSessionPath, sessionId);
    }

    public void Launch(Workspace ws, List<string> args, string? toolOverride = null)
    {
        if (!config.TryResolveValidatedTool(ws, toolOverride, adapters, out var tool))
        {
            return;
        }
        var adapter = adapters.Resolve(tool);

        adapter.PrepareLaunch(ws);
        claudeRunner.Run(
            adapter.ExecutableName,
            PathHelper.ResolvePath(ws.PrimaryRepo, Lookup),
            args,
            adapter.GetEnvironment(ws));
    }
}
