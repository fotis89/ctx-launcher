using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class CopilotAdapter(WlPaths paths) : IToolAdapter
{
    public string ExecutableName => "copilot";
    public string DisplayName => "copilot";
    public bool SkillsAreSlashInvokable => false;

    public const string SharedPluginName = "wl-shared";
    public const string WorkspacePluginPrefix = "wl-";

    public const string AgentsMdMarker = "<!-- managed by wl: this file is auto-generated from instructions.md on each launch — edits will be overwritten -->";

    public void PrepareLaunch(Workspace ws)
    {
        // Mirror instructions.md → AGENTS.md so Copilot's auto-discovery
        // picks up workspace context (COPILOT_CUSTOM_INSTRUCTIONS_DIRS
        // points it at this folder). Marker header makes the
        // auto-generated nature obvious. Best-effort: a locked file
        // or unwritable folder warns and launches anyway.
        var agentsPath = ws.AgentsPath;
        try
        {
            if (File.Exists(ws.InstructionsPath))
            {
                var instructions = File.ReadAllText(ws.InstructionsPath);
                // Preserve the source file's newline style so AGENTS.md
                // isn't a mix of platform and user line endings.
                var newline = SetupService.DetectNewline(instructions);
                File.WriteAllText(agentsPath, AgentsMdMarker + newline + newline + instructions);
            }
            else if (File.Exists(agentsPath) && IsWlManaged(agentsPath))
            {
                // Only delete wl-managed files (marker present). User-managed
                // AGENTS.md is left alone.
                File.Delete(agentsPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine($"Warning: could not refresh {agentsPath} ({ex.GetType().Name}); launching without updated workspace instructions.");
        }

        // Each .claude/ dir is exposed as a local plugin via --plugin-dir;
        // ensure a plugin.json manifest exists. Per-dir try so one failure
        // doesn't block the others.
        foreach (var (claudeDir, name) in GetManagedClaudeDirs(ws))
        {
            if (!HasSkills(claudeDir)) continue;
            try
            {
                EnsurePluginManifest(claudeDir, name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Console.Error.WriteLine($"Warning: could not write {Path.Combine(claudeDir, WlPaths.PluginManifestFileName)} ({ex.GetType().Name}); skills in this directory may not load.");
            }
        }
    }

    private IEnumerable<(string ClaudeDir, string PluginName)> GetManagedClaudeDirs(Workspace ws)
    {
        // Plugin names must be kebab-case per Copilot's plugin.json spec.
        // Slugify the folder name (disk identity, guaranteed unique), then
        // ws.Name, then a literal "workspace" so exotic folder names that
        // slugify to empty (e.g. "~~~") still produce a valid plugin name.
        var slug = PathHelper.Slugify(ws.FolderName);
        if (string.IsNullOrEmpty(slug)) slug = PathHelper.Slugify(ws.Name);
        if (string.IsNullOrEmpty(slug)) slug = "workspace";
        yield return (ws.ClaudeDirPath, $"{WorkspacePluginPrefix}{slug}");
        yield return (paths.SharedClaudeDir, SharedPluginName);
    }

    private static bool HasSkills(string claudeDir)
    {
        var skillsDir = Path.Combine(claudeDir, WlPaths.SkillsDirName);
        if (!Directory.Exists(skillsDir))
        {
            return false;
        }
        return Directory.GetDirectories(skillsDir)
            .Any(d => File.Exists(Path.Combine(d, WlPaths.SkillFileName)));
    }

    private static bool IsWlManaged(string agentsPath)
    {
        try
        {
            using var reader = new StreamReader(agentsPath);
            var firstLine = reader.ReadLine();
            return firstLine is not null && firstLine.StartsWith(AgentsMdMarker, StringComparison.Ordinal);
        }
        catch
        {
            // Defensive: if we can't read, treat as user-managed (don't delete).
            return false;
        }
    }

    public static void EnsurePluginManifest(string claudeDir, string pluginName)
    {
        Directory.CreateDirectory(claudeDir);
        var manifestPath = Path.Combine(claudeDir, WlPaths.PluginManifestFileName);
        var content = $$"""
            {
              "name": "{{pluginName}}"
            }
            """;
        File.WriteAllText(manifestPath, content);
    }

    public IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws)
    {
        // Tell Copilot to also search the workspace folder for AGENTS.md
        // (default search is cwd + git root only).
        return new Dictionary<string, string>
        {
            ["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"] = ws.FolderPath,
        };
    }

    public IEnumerable<string> DescribeLaunchPrep(Workspace ws)
    {
        // Mirror what PrepareLaunch will do. Observational IO only.
        if (File.Exists(ws.InstructionsPath))
        {
            yield return $"writes {ws.AgentsPath} (mirror of instructions.md)";
        }
        else if (File.Exists(ws.AgentsPath) && IsWlManaged(ws.AgentsPath))
        {
            yield return $"deletes {ws.AgentsPath} (was wl-managed; instructions.md no longer present)";
        }

        foreach (var (claudeDir, name) in GetManagedClaudeDirs(ws))
        {
            if (HasSkills(claudeDir))
            {
                yield return $"writes {Path.Combine(claudeDir, WlPaths.PluginManifestFileName)} (plugin name: {name})";
            }
        }
    }

    public void InvokeCreateSkill(string skillName, string? workspaceName, string cwd, string sharedDir, ClaudeRunner runner)
    {
        // Expose the shared skill via --plugin-dir for this single
        // invocation only — no global state mutation. Trigger by
        // description-match phrasing: Copilot reserves slash for
        // built-ins (/init, /skills) so `/<skill-name>` wouldn't fire.
        var sharedClaudeDir = WlPaths.ClaudeDir(sharedDir);
        if (HasSkills(sharedClaudeDir))
        {
            EnsurePluginManifest(sharedClaudeDir, SharedPluginName);
        }
        var detail = workspaceName is null ? "" : $" for workspace '{workspaceName}'";
        var prompt = $"Use the {skillName} skill{detail}.";
        runner.Run(ExecutableName, cwd, ["--plugin-dir", sharedClaudeDir, "-i", prompt]);
    }

    public AdapterArgs BuildArgs(AdapterLaunchSpec spec)
    {
        var args = new List<string>();
        string? newSessionId = null;
        var ws = spec.Workspace;

        // Copilot's --resume=<uuid> is idempotent (creates if missing,
        // resumes if present), so we always emit it to track sessions
        // the same way as Claude. Copilot 1.0.43+ refuses --name
        // alongside --resume, so new sessions are unnamed; /resume
        // shows them by UUID. wl tracks the UUID in .last-session,
        // so the user never types it.
        if (spec.ResumeSessionId is not null)
        {
            args.Add($"--resume={spec.ResumeSessionId}");
        }
        else
        {
            newSessionId = Guid.NewGuid().ToString();
            args.Add($"--resume={newSessionId}");
        }

        spec.AppendAddDirArgs(args);

        // Manifests are written by PrepareLaunch; emit --plugin-dir for
        // each dir that has skills.
        foreach (var (claudeDir, _) in GetManagedClaudeDirs(ws))
        {
            if (HasSkills(claudeDir))
            {
                args.Add("--plugin-dir");
                args.Add(claudeDir);
            }
        }

        if (spec.Yolo)
        {
            args.Add("--yolo");
        }

        if (!string.IsNullOrEmpty(spec.Prompt))
        {
            args.Add("-i");
            args.Add(spec.Prompt);
        }

        return new AdapterArgs(args, newSessionId);
    }
}
