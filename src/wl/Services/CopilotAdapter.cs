using System.Text.Json.Nodes;

using wl.Models;

namespace wl.Services;

public class CopilotAdapter : IToolAdapter
{
    private const string PluginDirName = "wl-skills-plugin";

    public string ExecutableName => "copilot";
    public string DisplayName => "copilot";

    public void PrepareLaunch(Workspace ws)
    {
        // 1. Mirror instructions.md → AGENTS.md so Copilot's auto-discovery
        //    picks up workspace context. (COPILOT_CUSTOM_INSTRUCTIONS_DIRS
        //    in GetEnvironment ensures the workspace folder is searched.)
        var agentsPath = Path.Combine(ws.FolderPath, "AGENTS.md");
        if (File.Exists(ws.InstructionsPath))
        {
            File.Copy(ws.InstructionsPath, agentsPath, overwrite: true);
        }
        else if (File.Exists(agentsPath))
        {
            File.Delete(agentsPath);
        }

        // 2. Mirror Claude skills into <workspace>/wl-skills-plugin/skills/
        //    (full directory copy — SKILL.md plus references/, etc.) and
        //    register that path in Copilot's settings.json under
        //    skillDirectories. Copilot's --plugin-dir flag does not load
        //    raw skill directories; the working mechanism is the same one
        //    /skills add uses, which writes to skillDirectories.
        var pluginSkillsDir = Path.Combine(ws.FolderPath, PluginDirName, "skills");
        if (Directory.Exists(pluginSkillsDir))
        {
            Directory.Delete(pluginSkillsDir, recursive: true);
        }

        MirrorSkills(ws.SkillsPath, pluginSkillsDir);

        var workspacesRoot = Path.GetDirectoryName(ws.FolderPath);
        if (workspacesRoot is not null)
        {
            var sharedSkillsPath = Path.Combine(workspacesRoot, WorkspaceService.SharedDirName, ".claude", "skills");
            MirrorSkills(sharedSkillsPath, pluginSkillsDir);
        }

        if (Directory.Exists(pluginSkillsDir) && Directory.GetDirectories(pluginSkillsDir).Length > 0)
        {
            RegisterSkillsDir(pluginSkillsDir, GetCopilotSettingsPath());
        }
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
        var tmp = settingsPath + ".tmp";
        File.WriteAllText(tmp, settings.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, settingsPath, overwrite: true);
    }

    private static void MirrorSkills(string sourceSkillsDir, string targetSkillsDir)
    {
        if (!Directory.Exists(sourceSkillsDir))
        {
            return;
        }

        foreach (var skillSrcDir in Directory.GetDirectories(sourceSkillsDir))
        {
            var skillFile = Path.Combine(skillSrcDir, "SKILL.md");
            if (!File.Exists(skillFile))
            {
                continue;
            }

            var name = Path.GetFileName(skillSrcDir);
            var skillTargetDir = Path.Combine(targetSkillsDir, name);
            CopyDirectory(skillSrcDir, skillTargetDir);
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
        {
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var subdir in Directory.GetDirectories(src))
        {
            CopyDirectory(subdir, Path.Combine(dst, Path.GetFileName(subdir)));
        }
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
