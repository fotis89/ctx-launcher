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
        // picks up workspace context. COPILOT_CUSTOM_INSTRUCTIONS_DIRS
        // (in GetEnvironment) tells Copilot to also look in the workspace
        // folder for AGENTS.md. Marker header makes it obvious to anyone
        // who opens the file that it's auto-generated.
        var agentsPath = ws.AgentsPath;
        if (File.Exists(ws.InstructionsPath))
        {
            var instructions = File.ReadAllText(ws.InstructionsPath);
            File.WriteAllText(agentsPath, AgentsMdMarker + Environment.NewLine + Environment.NewLine + instructions);
        }
        else if (File.Exists(agentsPath) && IsWlManaged(agentsPath))
        {
            // Only delete AGENTS.md if wl wrote it (marker header is present).
            // A user-managed AGENTS.md or one created by another tool is left
            // alone — wl shouldn't clobber files it doesn't own.
            File.Delete(agentsPath);
        }

        // Each .claude/ dir is exposed to Copilot as a local plugin via
        // --plugin-dir (emitted in BuildArgs). A plugin needs a manifest;
        // ensure one exists at <dir>/plugin.json. Manifests are gitignored
        // and idempotently regenerated — no global state mutation, no
        // race against Copilot's startup read.
        foreach (var (claudeDir, name) in GetManagedClaudeDirs(ws))
        {
            if (HasSkills(claudeDir))
            {
                EnsurePluginManifest(claudeDir, name);
            }
        }
    }

    private IEnumerable<(string ClaudeDir, string PluginName)> GetManagedClaudeDirs(Workspace ws)
    {
        // Plugin names must be kebab-case per Copilot's plugin.json spec
        // (letters, numbers, hyphens only). Slugify the folder name first
        // because it's the disk identity and guaranteed unique within
        // ~/.wl-workspaces/. Fall through to ws.Name (user-friendly label
        // in workspace.json) and finally a literal "workspace" so the
        // plugin name is never empty for exotic hand-created folder names
        // like "~~~" or characters that produce an empty slug.
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

    public void InvokeCreateSkill(string skillName, string? workspaceName, string cwd, string sharedDir, ClaudeRunner runner)
    {
        // The shared skill lives under <sharedDir>/.claude/skills/. Expose
        // it via --plugin-dir so Copilot loads it for this single
        // invocation only — no global state mutation. Trigger by
        // description-match phrasing because Copilot reserves slash for
        // built-in commands (/init, /skills, /clear) — `/<skill-name>`
        // would not invoke a custom skill.
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

        // Copilot's --resume=<uuid> is idempotent: starts a new session if
        // the UUID doesn't exist, resumes if it does. Always emit it so wl
        // can track session IDs the same way it does for Claude.
        if (spec.ResumeSessionId is not null)
        {
            args.Add($"--resume={spec.ResumeSessionId}");
        }
        else
        {
            newSessionId = Guid.NewGuid().ToString();
            args.Add($"--resume={newSessionId}");
            args.Add("--name");
            args.Add(ws.Name);
        }

        spec.AppendAddDirArgs(args);

        // Load workspace + shared skills as local plugins. Manifests are
        // written by PrepareLaunch; here we just emit the flag for each
        // dir that has skills.
        foreach (var (claudeDir, _) in GetManagedClaudeDirs(ws))
        {
            if (HasSkills(claudeDir))
            {
                args.Add("--plugin-dir");
                args.Add(claudeDir);
            }
        }

        // instructions.md is mirrored to AGENTS.md in PrepareLaunch and
        // discovered by Copilot via COPILOT_CUSTOM_INSTRUCTIONS_DIRS.

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
