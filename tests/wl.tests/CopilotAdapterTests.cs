using wl.Models;
using wl.Services;

namespace wl.tests;

public class CopilotAdapterTests
{
    private readonly CopilotAdapter _adapter = new();

    private static AdapterLaunchSpec MakeSpec(
        string? prompt = null,
        bool yolo = false,
        string? resumeSessionId = null,
        List<string>? additionalDirs = null,
        string? sharedDir = null,
        string? folderPath = null)
    {
        var ws = new Workspace
        {
            Name = "test",
            PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
            AdditionalDirs = [],
            FolderPath = folderPath ?? Path.GetTempPath(),
        };

        return new AdapterLaunchSpec(
            Workspace: ws,
            ResolvedAdditionalDirs: additionalDirs ?? [],
            ResolvedSharedDir: sharedDir,
            Prompt: prompt,
            Yolo: yolo,
            ResumeSessionId: resumeSessionId);
    }

    [Fact]
    public void NewSession_EmitsResumeUuidAndName()
    {
        var spec = MakeSpec();
        var result = _adapter.BuildArgs(spec);

        Assert.NotNull(result.NewSessionId);
        Assert.True(Guid.TryParse(result.NewSessionId, out _));

        Assert.Contains(result.Args, a => a == $"--resume={result.NewSessionId}");
        Assert.Contains("--name", result.Args);

        var nameIdx = result.Args.IndexOf("--name");
        Assert.Equal("test", result.Args[nameIdx + 1]);
    }

    [Fact]
    public void ResumeSession_EmitsResumeWithExistingId_NoName()
    {
        var sessionId = Guid.NewGuid().ToString();
        var spec = MakeSpec(resumeSessionId: sessionId);
        var result = _adapter.BuildArgs(spec);

        Assert.Null(result.NewSessionId);
        Assert.Contains(result.Args, a => a == $"--resume={sessionId}");
        Assert.DoesNotContain("--name", result.Args);
    }

    [Fact]
    public void Yolo_EmitsYoloFlag()
    {
        var result = _adapter.BuildArgs(MakeSpec(yolo: true));

        Assert.Contains("--yolo", result.Args);
        Assert.DoesNotContain("--dangerously-skip-permissions", result.Args);
    }

    [Fact]
    public void NoYolo_NoYoloFlag()
    {
        var result = _adapter.BuildArgs(MakeSpec(yolo: false));

        Assert.DoesNotContain("--yolo", result.Args);
    }

    [Fact]
    public void Prompt_EmitsDashIWithPrompt()
    {
        var result = _adapter.BuildArgs(MakeSpec(prompt: "do the thing"));

        Assert.Contains("-i", result.Args);
        var idx = result.Args.IndexOf("-i");
        Assert.Equal("do the thing", result.Args[idx + 1]);
    }

    [Fact]
    public void NoPrompt_NoDashI()
    {
        var result = _adapter.BuildArgs(MakeSpec());

        Assert.DoesNotContain("-i", result.Args);
    }

    [Fact]
    public void AdditionalDirs_EachEmitsAddDir()
    {
        var dirs = new List<string> { "/path/a", "/path/b" };
        var result = _adapter.BuildArgs(MakeSpec(additionalDirs: dirs));

        // Each additional + the workspace folder = 3 --add-dir flags
        Assert.Equal(3, result.Args.Count(a => a == "--add-dir"));
        Assert.Contains("/path/a", result.Args);
        Assert.Contains("/path/b", result.Args);
    }

    [Fact]
    public void SharedDir_EmitsAddDir_BeforeWorkspaceFolder()
    {
        var sharedDir = "/path/shared";
        var folderPath = "/path/workspace";
        var result = _adapter.BuildArgs(MakeSpec(sharedDir: sharedDir, folderPath: folderPath));

        var sharedIdx = result.Args.IndexOf(sharedDir);
        var wsIdx = result.Args.IndexOf(folderPath);
        Assert.True(sharedIdx >= 0 && wsIdx >= 0, "Both shared and workspace dirs should be present");
        Assert.True(sharedIdx < wsIdx, "Shared dir should come before workspace folder");
    }

    [Fact]
    public void WorkspaceFolder_AlwaysAdded()
    {
        var folderPath = "/path/ws";
        var result = _adapter.BuildArgs(MakeSpec(folderPath: folderPath));

        Assert.Contains(folderPath, result.Args);
    }

    [Fact]
    public void PrepareLaunch_WithInstructions_MirrorsToAgentsFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var instructionsPath = Path.Combine(tempDir, "instructions.md");
            File.WriteAllText(instructionsPath, "workspace context goes here");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            var agentsPath = Path.Combine(tempDir, "AGENTS.md");
            Assert.True(File.Exists(agentsPath));
            Assert.Contains("workspace context goes here", File.ReadAllText(agentsPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_WithoutInstructions_NoAgentsFileWritten()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            Assert.False(File.Exists(Path.Combine(tempDir, "AGENTS.md")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetEnvironment_SetsCustomInstructionsDirToWorkspaceFolder()
    {
        var ws = new Workspace
        {
            Name = "test",
            PrimaryRepo = "/path/to/repo",
            FolderPath = "/path/to/workspace-folder",
        };

        var env = _adapter.GetEnvironment(ws);

        Assert.True(env.ContainsKey("COPILOT_CUSTOM_INSTRUCTIONS_DIRS"));
        Assert.Equal("/path/to/workspace-folder", env["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"]);
    }

    [Fact]
    public void PrepareLaunch_OverwritesStaleAgentsFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "instructions.md"), "fresh");
            File.WriteAllText(Path.Combine(tempDir, "AGENTS.md"), "stale");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            var contents = File.ReadAllText(Path.Combine(tempDir, "AGENTS.md"));
            Assert.Contains("fresh", contents);
            Assert.DoesNotContain("stale", contents);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_WithWorkspaceSkills_MirrorsToCopilotPluginDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var skillDir = Path.Combine(tempDir, ".claude", "skills", "wl-run-tests");
        Directory.CreateDirectory(skillDir);
        try
        {
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"),
                "---\nname: wl-run-tests\ndescription: Run the test suite\n---\n\nSteps...\n");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            var mirroredSkill = Path.Combine(tempDir, "wl-skills-plugin", "skills", "wl-run-tests", "SKILL.md");
            Assert.True(File.Exists(mirroredSkill));
            Assert.Contains("Run the test suite", File.ReadAllText(mirroredSkill));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_WithSharedSkills_AlsoMirroredIntoWorkspacePlugin()
    {
        var workspacesRoot = Path.Combine(Path.GetTempPath(), "wl-test-root-" + Guid.NewGuid().ToString("N")[..8]);
        var wsFolder = Path.Combine(workspacesRoot, "test-ws");
        var sharedSkillDir = Path.Combine(workspacesRoot, ".shared", ".claude", "skills", "do-code-review");
        Directory.CreateDirectory(wsFolder);
        Directory.CreateDirectory(sharedSkillDir);
        try
        {
            File.WriteAllText(Path.Combine(sharedSkillDir, "SKILL.md"),
                "---\nname: do-code-review\ndescription: Review a PR\n---\n\nReview steps...\n");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = wsFolder,
            };

            _adapter.PrepareLaunch(ws);

            var mirroredSkill = Path.Combine(wsFolder, "wl-skills-plugin", "skills", "do-code-review", "SKILL.md");
            Assert.True(File.Exists(mirroredSkill));
            Assert.Contains("Review steps", File.ReadAllText(mirroredSkill));
        }
        finally
        {
            Directory.Delete(workspacesRoot, true);
        }
    }

    [Fact]
    public void PrepareLaunch_RebuildsPluginDir_RemovingStaleSkills()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var staleSkill = Path.Combine(tempDir, "wl-skills-plugin", "skills", "removed-skill");
        Directory.CreateDirectory(staleSkill);
        File.WriteAllText(Path.Combine(staleSkill, "SKILL.md"), "stale skill body");
        try
        {
            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            // No source skills exist for the workspace, so plugin/skills should be empty.
            Assert.False(Directory.Exists(staleSkill));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildArgs_WithPluginSkills_AddsPluginDirFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var pluginSkillDir = Path.Combine(tempDir, "wl-skills-plugin", "skills", "some-skill");
        Directory.CreateDirectory(pluginSkillDir);
        File.WriteAllText(Path.Combine(pluginSkillDir, "SKILL.md"), "skill body");
        try
        {
            var result = _adapter.BuildArgs(MakeSpec(folderPath: tempDir));

            Assert.Contains("--plugin-dir", result.Args);
            var idx = result.Args.IndexOf("--plugin-dir");
            Assert.Equal(Path.Combine(tempDir, "wl-skills-plugin"), result.Args[idx + 1]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildArgs_WithoutPluginSkills_NoPluginDirFlag()
    {
        // No .copilot dir at folderPath → no plugin-dir flag
        var result = _adapter.BuildArgs(MakeSpec(folderPath: "/path/to/empty-ws"));

        Assert.DoesNotContain("--plugin-dir", result.Args);
    }
}
