using System.Diagnostics.CodeAnalysis;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService(ClaudeRunner claudeRunner, PathsService paths, ToolAdapterRegistry adapters, ConfigService config)
{
    private Func<string, string?> Lookup => paths.Get;

    /// <summary>
    /// Validate the tool resolution chain (override → workspace.json →
    /// .config.json → default) and return the resolved adapter. Prints a
    /// labeled error to stderr and returns false if the chain lands on
    /// an unregistered tool. Callers should invoke this once at the top
    /// of their flow and bail on false; downstream service methods
    /// trust valid input and use <see cref="ToolAdapterRegistry.Resolve"/>.
    /// </summary>
    public bool TryResolveAdapter(Workspace ws, string? toolOverride, [NotNullWhen(true)] out IToolAdapter? adapter)
    {
        if (config.TryResolveValidatedTool(ws, toolOverride, adapters, out var tool))
        {
            adapter = adapters.Resolve(tool);
            return true;
        }
        adapter = null;
        return false;
    }

    public (List<string> Args, List<string> SkippedDirs, string? NewSessionId) BuildLaunchArgs(Workspace ws, string? prompt = null, bool yolo = false, string? resumeSessionId = null, string? sharedDirPath = null, string? toolOverride = null)
    {
        var adapter = adapters.Resolve(config.ResolveTool(ws, toolOverride));
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
        var adapter = adapters.Resolve(config.ResolveTool(ws));
        var (args, _, _) = BuildLaunchArgs(ws, prompt, yolo, resumeSessionId, sharedDirPath);

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
        try
        {
            var value = File.ReadAllText(path).Trim();
            return Guid.TryParse(value, out _) ? value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // .last-session is a convenience pointer; if it's unreadable
            // (locked by AV, permission issue) fall back to "no session"
            // and let the launch start fresh.
            Console.Error.WriteLine($"Warning: cannot read {path} ({ex.GetType().Name}); starting a new session.");
            return null;
        }
    }

    public static void SaveLastSession(Workspace ws, string sessionId)
    {
        try
        {
            File.WriteAllText(ws.LastSessionPath, sessionId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Best effort — failing here means subsequent --resume won't
            // find a session, but the launch already happened. Surface
            // the reason so the user can investigate (e.g. read-only
            // workspace folder).
            Console.Error.WriteLine($"Warning: cannot write {ws.LastSessionPath} ({ex.GetType().Name}); next --resume will start fresh.");
        }
    }

    public bool Launch(Workspace ws, List<string> args, string? toolOverride = null)
    {
        var adapter = adapters.Resolve(config.ResolveTool(ws, toolOverride));

        adapter.PrepareLaunch(ws);
        return claudeRunner.Run(
            adapter.ExecutableName,
            PathHelper.ResolvePath(ws.PrimaryRepo, Lookup),
            args,
            adapter.GetEnvironment(ws));
    }
}
