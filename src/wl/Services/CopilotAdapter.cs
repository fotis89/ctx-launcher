using System.Text.Json.Nodes;

using wl.Models;

namespace wl.Services;

public class CopilotAdapter : IToolAdapter
{
    public string ExecutableName => "copilot";
    public string DisplayName => "copilot";

    public const string AgentsMdMarker = "<!-- managed by wl: this file is auto-generated from instructions.md on each launch — edits will be overwritten -->";

    public void PrepareLaunch(Workspace ws)
    {
        // Mirror instructions.md → AGENTS.md so Copilot's auto-discovery
        // picks up workspace context. COPILOT_CUSTOM_INSTRUCTIONS_DIRS
        // (in GetEnvironment) tells Copilot to also look in the workspace
        // folder for AGENTS.md. Marker header makes it obvious to anyone
        // who opens the file that it's auto-generated.
        var agentsPath = Path.Combine(ws.FolderPath, "AGENTS.md");
        if (File.Exists(ws.InstructionsPath))
        {
            var instructions = File.ReadAllText(ws.InstructionsPath);
            File.WriteAllText(agentsPath, AgentsMdMarker + Environment.NewLine + Environment.NewLine + instructions);
        }
        else if (File.Exists(agentsPath))
        {
            File.Delete(agentsPath);
        }

        // Register .claude/skills directories with Copilot via settings.json.
        // No mirror — Copilot reads SKILL.md straight from the workspace's
        // .claude/skills/ and the shared .shared/.claude/skills/. Single
        // source of truth, no auto-generated artifacts to gitignore.
        // CleanupAfterLaunch unregisters these when the session ends so the
        // global settings file stays bounded.
        var settingsPath = GetCopilotSettingsPath();
        foreach (var skillsDir in GetManagedSkillsDirs(ws))
        {
            if (HasSkills(skillsDir))
            {
                RegisterSkillsDir(skillsDir, settingsPath);
            }
        }
    }

    public void CleanupAfterLaunch(Workspace ws)
    {
        // Unregister the skill-directory entries we wrote in PrepareLaunch
        // so the user's ~/.copilot/settings.json reverts to its prior
        // state. LaunchService schedules this on a Timer 2s after spawn
        // (so Copilot has time to read settings) and also calls it once
        // more in the launch finally as a safety net — UnregisterSkillsDirs
        // is idempotent.
        var settingsPath = GetCopilotSettingsPath();
        UnregisterSkillsDirs(GetManagedSkillsDirs(ws), settingsPath);
    }

    private static IEnumerable<string> GetManagedSkillsDirs(Workspace ws)
    {
        yield return ws.SkillsPath;
        var workspacesRoot = Path.GetDirectoryName(ws.FolderPath);
        if (workspacesRoot is not null)
        {
            yield return Path.Combine(workspacesRoot, WorkspaceService.SharedDirName, ".claude", "skills");
        }
    }

    private static bool HasSkills(string skillsDir)
    {
        if (!Directory.Exists(skillsDir))
        {
            return false;
        }
        return Directory.GetDirectories(skillsDir)
            .Any(d => File.Exists(Path.Combine(d, "SKILL.md")));
    }

    public static string GetCopilotSettingsPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".copilot", "settings.json");
    }

    public static void RegisterSkillsDir(string skillsDir, string settingsPath)
    {
        var copilotDir = Path.GetDirectoryName(settingsPath)!;

        JsonObject settings;
        if (File.Exists(settingsPath))
        {
            try
            {
                settings = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? new JsonObject();
            }
            catch
            {
                // Don't overwrite a malformed user settings file — bail.
                return;
            }
        }
        else
        {
            settings = new JsonObject();
        }

        // Schema guard: if Copilot ever changes 'skillDirectories' to a
        // non-array shape, refuse to touch the file rather than silently
        // overwriting the user's data with a fresh empty array.
        if (settings.ContainsKey("skillDirectories") && settings["skillDirectories"] is not JsonArray)
        {
            Console.Error.WriteLine(
                $"Error: 'skillDirectories' in {settingsPath} has an unexpected shape (expected array). " +
                "Copilot CLI may have changed its config schema. " +
                "Refusing to modify the file — please file an issue at https://github.com/fotis89/ctx-launcher/issues.");
            return;
        }

        var dirs = settings["skillDirectories"] as JsonArray ?? new JsonArray();

        foreach (var item in dirs)
        {
            if (item is JsonValue v && v.TryGetValue<string>(out var existing)
                && string.Equals(existing, skillsDir, StringComparison.OrdinalIgnoreCase))
            {
                return; // already registered
            }
        }

        dirs.Add((JsonNode)JsonValue.Create(skillsDir)!);
        settings["skillDirectories"] = dirs;

        Directory.CreateDirectory(copilotDir);
        WriteSettingsAtomic(settingsPath, settings);
    }

    public static void UnregisterSkillsDirs(IEnumerable<string> skillsDirs, string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return;
        }

        JsonObject settings;
        try
        {
            settings = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return;
        }

        if (settings["skillDirectories"] is not JsonArray dirs)
        {
            return;
        }

        var toRemove = new HashSet<string>(skillsDirs, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        for (var i = dirs.Count - 1; i >= 0; i--)
        {
            if (dirs[i] is JsonValue v && v.TryGetValue<string>(out var existing) && toRemove.Contains(existing))
            {
                dirs.RemoveAt(i);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        WriteSettingsAtomic(settingsPath, settings);
    }

    private static void WriteSettingsAtomic(string settingsPath, JsonObject settings)
    {
        var tmp = settingsPath + ".tmp";
        File.WriteAllText(tmp, settings.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, settingsPath, overwrite: true);
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

    public void InvokeCreateSkill(string prompt, string cwd, string sharedDir, ClaudeRunner runner)
    {
        // /wl-create-workspace lives in <sharedDir>/.claude/skills/. Copilot
        // doesn't auto-discover skills outside cwd/git-root, so register
        // that dir in settings.json for the duration of this create flow,
        // the same way per-launch does for workspace skills. Cleanup runs
        // 2s after spawn (Timer) and again when the spawned process exits
        // (finally), idempotent.
        var skillsDir = Path.Combine(sharedDir, ".claude", "skills");
        var settingsPath = GetCopilotSettingsPath();

        RegisterSkillsDir(skillsDir, settingsPath);

        var cleanupOnce = 0;
        void Cleanup()
        {
            if (Interlocked.Exchange(ref cleanupOnce, 1) == 0)
            {
                UnregisterSkillsDirs([skillsDir], settingsPath);
            }
        }

        using var cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);

        try
        {
            runner.Run(ExecutableName, cwd, ["-i", prompt]);
        }
        finally
        {
            Cleanup();
        }
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

        // instructions.md is mirrored to AGENTS.md in PrepareLaunch and
        // discovered by Copilot via COPILOT_CUSTOM_INSTRUCTIONS_DIRS.
        // Skills are registered in ~/.copilot/settings.json's
        // skillDirectories by PrepareLaunch — no CLI flag needed.

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
