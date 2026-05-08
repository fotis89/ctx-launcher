using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.tests;

public class CopilotAdapterTests : IDisposable
{
    // Per-test-class temp root so the adapter's SharedClaudeDir resolves
    // somewhere empty (and predictable), instead of touching the
    // developer's real ~/.wl-workspaces/.shared.
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wl-test-root-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly CopilotAdapter _adapter;

    public CopilotAdapterTests()
    {
        Directory.CreateDirectory(_root);
        _adapter = new CopilotAdapter(new WlPaths(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

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
            var contents = File.ReadAllText(agentsPath);
            Assert.Contains("workspace context goes here", contents);
            Assert.StartsWith(CopilotAdapter.AgentsMdMarker, contents);
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
    public void PrepareLaunch_NoInstructions_PreservesUserManagedAgentsFile()
    {
        // If AGENTS.md exists without the wl marker, it's user-managed (or
        // written by another tool). PrepareLaunch must not delete it just
        // because instructions.md is missing.
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var agentsPath = Path.Combine(tempDir, "AGENTS.md");
            const string userContent = "# My hand-written agent guide\n\nKeep this file.";
            File.WriteAllText(agentsPath, userContent);

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            Assert.True(File.Exists(agentsPath));
            Assert.Equal(userContent, File.ReadAllText(agentsPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_NoInstructions_DeletesWlManagedAgentsFile()
    {
        // Conversely, if AGENTS.md was previously written by wl (marker
        // present), PrepareLaunch should clean it up when instructions.md
        // disappears so we don't keep stale workspace context around.
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var agentsPath = Path.Combine(tempDir, "AGENTS.md");
            File.WriteAllText(agentsPath, CopilotAdapter.AgentsMdMarker + "\n\nold context");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            Assert.False(File.Exists(agentsPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_AgentsMdMatchesInstructionsNewlineStyle_LfInput()
    {
        // instructions.md is user-controlled and may use any newline
        // style. The generated AGENTS.md (marker + body) must use the
        // SAME style throughout — no mixed CRLF/LF.
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "instructions.md"), "line one\nline two\n");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            var contents = File.ReadAllText(Path.Combine(tempDir, "AGENTS.md"));
            Assert.DoesNotContain("\r\n", contents);
            Assert.Contains("\n", contents);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_AgentsMdMatchesInstructionsNewlineStyle_CrlfInput()
    {
        // CRLF-authored instructions.md should produce CRLF-only AGENTS.md
        // even on Linux runners.
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "instructions.md"), "line one\r\nline two\r\n");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            var contents = File.ReadAllText(Path.Combine(tempDir, "AGENTS.md"));
            Assert.Contains("\r\n", contents);
            Assert.DoesNotMatch(@"(?<!\r)\n", contents);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
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
    public void PrepareLaunch_WithSkills_WritesPluginManifestInWorkspaceClaudeDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            // Layout: <ws>/.claude/skills/my-skill/SKILL.md
            var skillDir = Path.Combine(tempDir, ".claude", "skills", "my-skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: my-skill\n---\nbody");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            var manifestPath = Path.Combine(tempDir, ".claude", "plugin.json");
            Assert.True(File.Exists(manifestPath), "plugin.json should be written when skills exist");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            Assert.Equal($"wl-{Path.GetFileName(tempDir)}", json["name"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_NoSkills_NoPluginManifestWritten()
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

            Assert.False(File.Exists(Path.Combine(tempDir, ".claude", "plugin.json")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildArgs_WithWorkspaceSkills_EmitsPluginDirFlag()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var wsFolder = Path.Combine(tempRoot, "myws");
        var skillDir = Path.Combine(wsFolder, ".claude", "skills", "ws-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: ws-skill\n---\nbody");
        try
        {
            var spec = MakeSpec(folderPath: wsFolder);
            var result = _adapter.BuildArgs(spec);

            var pluginDirIdxs = Enumerable.Range(0, result.Args.Count)
                .Where(i => result.Args[i] == "--plugin-dir")
                .ToList();
            Assert.NotEmpty(pluginDirIdxs);
            var values = pluginDirIdxs.Select(i => result.Args[i + 1]).ToList();
            Assert.Contains(Path.Combine(wsFolder, ".claude"), values);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void BuildArgs_NoSkills_NoPluginDirFlag()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var wsFolder = Path.Combine(tempRoot, "myws");
        Directory.CreateDirectory(wsFolder);
        try
        {
            var spec = MakeSpec(folderPath: wsFolder);
            var result = _adapter.BuildArgs(spec);

            Assert.DoesNotContain("--plugin-dir", result.Args);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void PrepareLaunch_FolderNameWithSpaces_PluginNameIsSlugified()
    {
        // Existing/hand-created workspaces may have folder names that
        // aren't kebab-case. Plugin manifest names must be kebab-case
        // per Copilot's plugin.json spec.
        var tempRoot = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var wsFolder = Path.Combine(tempRoot, "My Workspace");
        var skillDir = Path.Combine(wsFolder, ".claude", "skills", "x");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: x\n---\nbody");
        try
        {
            var ws = new Workspace
            {
                Name = "My Workspace",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = wsFolder,
            };

            _adapter.PrepareLaunch(ws);

            var manifest = Path.Combine(wsFolder, ".claude", "plugin.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
            var name = json["name"]!.GetValue<string>();
            Assert.Equal("wl-my-workspace", name);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void PrepareLaunch_FolderNameSlugifiesEmpty_FallsBackToWorkspaceName()
    {
        // If the folder name is all non-slug characters (e.g. "~~~"),
        // Slugify returns "" and we'd produce an invalid plugin name
        // ("wl-"). Fall back to ws.Name first.
        var tempRoot = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var wsFolder = Path.Combine(tempRoot, "~~~");
        var skillDir = Path.Combine(wsFolder, ".claude", "skills", "x");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: x\n---\nbody");
        try
        {
            var ws = new Workspace
            {
                Name = "Homelab",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = wsFolder,
            };

            _adapter.PrepareLaunch(ws);

            var manifest = Path.Combine(wsFolder, ".claude", "plugin.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
            Assert.Equal("wl-homelab", json["name"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void PrepareLaunch_FolderAndNameSlugifyEmpty_FallsBackToLiteralWorkspace()
    {
        // Last-resort fallback: if both FolderName and Name slugify to
        // empty, the plugin name must still be a valid kebab-case
        // identifier. Use the literal "workspace".
        var tempRoot = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        var wsFolder = Path.Combine(tempRoot, "~~~");
        var skillDir = Path.Combine(wsFolder, ".claude", "skills", "x");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: x\n---\nbody");
        try
        {
            var ws = new Workspace
            {
                Name = "___",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = wsFolder,
            };

            _adapter.PrepareLaunch(ws);

            var manifest = Path.Combine(wsFolder, ".claude", "plugin.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
            Assert.Equal("wl-workspace", json["name"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }
}

