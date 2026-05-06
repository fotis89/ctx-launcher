using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class LaunchService(ClaudeRunner claudeRunner, PathsService paths, ToolAdapterRegistry adapters)
{
    private Func<string, string?> Lookup => paths.Get;


    public (List<string> Args, List<string> SkippedDirs, string? NewSessionId) BuildClaudeArgs(Workspace ws, string? prompt = null, bool yolo = false, string? resumeSessionId = null, string? sharedDirPath = null)
    {
        var adapter = adapters.Resolve(ws.EffectiveTool);
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
        var adapter = adapters.Resolve(ws.EffectiveTool);
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
        var adapter = adapters.Resolve(ws.EffectiveTool);
        adapter.PrepareLaunch(ws);

        // Cleanup runs once. The Timer fires 2s after spawn so any global
        // state PrepareLaunch wrote (e.g. Copilot's skillDirectories) is
        // reverted while wl is still blocking on the child — long enough
        // for the child to have read the state. The finally is a safety
        // net if the child exited in under 2s.
        var cleanupOnce = 0;
        void Cleanup()
        {
            if (Interlocked.Exchange(ref cleanupOnce, 1) == 0)
            {
                adapter.CleanupAfterLaunch(ws);
            }
        }

        using var cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);

        try
        {
            claudeRunner.Run(
                adapter.ExecutableName,
                PathHelper.ResolvePath(ws.PrimaryRepo, Lookup),
                args,
                adapter.GetEnvironment(ws));
        }
        finally
        {
            Cleanup();
        }
    }
}
