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

        // 2. Build a Copilot plugin under <workspace>/.copilot/skills/ that
        //    mirrors the workspace's .claude/skills/ and the shared
        //    .shared/.claude/skills/. The plugin format is identical to
        //    Claude's (skills/<name>/SKILL.md), so we just copy the trees.
        //    BuildArgs adds --plugin-dir for this directory at launch.
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
        // discovered by Copilot via the workspace folder's --add-dir entry.

        // Skills from .claude/skills (workspace and shared) are mirrored
        // into <workspace>/.copilot/skills by PrepareLaunch. Pass that as
        // a Copilot plugin dir so the skills are loaded as real Copilot
        // skills (not just AGENTS.md text).
        var pluginSkillsDir = Path.Combine(ws.FolderPath, PluginDirName, "skills");
        if (Directory.Exists(pluginSkillsDir) && Directory.GetDirectories(pluginSkillsDir).Length > 0)
        {
            args.Add("--plugin-dir");
            args.Add(Path.Combine(ws.FolderPath, PluginDirName));
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
