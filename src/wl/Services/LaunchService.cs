using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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

    public static string? LoadLastSession(Workspace ws, IToolAdapter adapter)
    {
        var path = ws.LastSessionPath;
        if (!File.Exists(path))
            return null;
        try
        {
            var content = File.ReadAllText(path).Trim();
            if (content.Length == 0 || !content.StartsWith('{'))
                return null;

            // .last-session is a JSON map of tool name → session id so a
            // workspace can hold a separate resume pointer per agent.
            // Pre-0.8.0 stored a bare UUID; that legacy format is
            // converted to JSON by SetupService.MigrateLastSessionFiles
            // during the first `wl launch` after install, so this load
            // path never has to handle it.
            var map = JsonSerializer.Deserialize(content, WlJsonContext.Default.DictionaryStringString);
            if (map is not null && map.TryGetValue(ToolKey(adapter), out var id) && Guid.TryParse(id, out _))
            {
                return id;
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
        {
            // .last-session is a convenience pointer; if it's unreadable
            // (locked by AV, permission issue, malformed JSON) fall back
            // to "no session" and let the launch start fresh.
            Console.Error.WriteLine($"Warning: cannot read {path} ({ex.GetType().Name}); starting a new session.");
            return null;
        }
    }

    public static void SaveLastSession(Workspace ws, IToolAdapter adapter, string sessionId)
    {
        var path = ws.LastSessionPath;
        try
        {
            var map = LoadSessionMap(path);
            map[ToolKey(adapter)] = sessionId;
            var json = JsonSerializer.Serialize(map, WlJsonContext.Default.DictionaryStringString);

            // Atomic write: tmp + Move so a process killed mid-write
            // can't leave .last-session half-written and unparseable.
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            try
            {
                File.Move(tmp, path, overwrite: true);
            }
            catch
            {
                try { File.Delete(tmp); } catch { /* best-effort cleanup */ }
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Best effort — failing here means subsequent --resume won't
            // find a session, but the launch already happened. Surface
            // the reason so the user can investigate (e.g. read-only
            // workspace folder).
            Console.Error.WriteLine($"Warning: cannot write {path} ({ex.GetType().Name}); next --resume will start fresh.");
        }
    }

    // Canonical key for the JSON map. Always use the adapter's
    // ExecutableName (lowercased) so user-supplied casing in
    // workspace.json / --tool / .config.json doesn't fragment entries.
    private static string ToolKey(IToolAdapter adapter)
        => adapter.ExecutableName.ToLowerInvariant();

    // Reads the existing JSON map for merging on save. Returns an empty
    // dict if the file is missing, empty, malformed, or in the legacy
    // bare-UUID format — the legacy value is dropped here because save
    // will replace it with an explicit entry for the saving tool.
    private static Dictionary<string, string> LoadSessionMap(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>();
        try
        {
            var content = File.ReadAllText(path).Trim();
            if (content.Length == 0 || !content.StartsWith('{'))
                return new Dictionary<string, string>();
            return JsonSerializer.Deserialize(content, WlJsonContext.Default.DictionaryStringString)
                   ?? new Dictionary<string, string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
        {
            return new Dictionary<string, string>();
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
