using System.Text.Json.Nodes;

using wl.Models;

namespace wl.Services;

public class CopilotAdapter : IToolAdapter
{
    public string ExecutableName => "copilot";
    public string DisplayName => "copilot";

    public void PrepareLaunch(Workspace ws)
    {
        // Mirror instructions.md → AGENTS.md so Copilot's auto-discovery
        // picks up workspace context. COPILOT_CUSTOM_INSTRUCTIONS_DIRS
        // (in GetEnvironment) tells Copilot to also look in the workspace
        // folder for AGENTS.md.
        var agentsPath = Path.Combine(ws.FolderPath, "AGENTS.md");
        if (File.Exists(ws.InstructionsPath))
        {
            File.Copy(ws.InstructionsPath, agentsPath, overwrite: true);
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
        // Wait for Copilot to start and read settings.json before we
        // unregister. ClaudeRunner.Run no longer blocks on the child
        // process, so wl spawns Copilot, sleeps here long enough for the
        // initial settings load, then reverts the file. The user's
        // ~/.copilot/settings.json carries our wl-managed entries only
        // for this brief window.
        Thread.Sleep(TimeSpan.FromSeconds(2));

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

        foreach (var dir in spec.ResolvedAdditionalDirs)
        {
            args.Add("--add-dir");
            args.Add(dir);
        }

        if (spec.ResolvedSharedDir is not null)
        {
            args.Add("--add-dir");
            args.Add(spec.ResolvedSharedDir);
        }

        args.Add("--add-dir");
        args.Add(ws.FolderPath);

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
