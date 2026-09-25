using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.tests;

[Collection("StderrCapture")]
public class CopilotServiceTests : IDisposable
{
    // Per-test-class temp root so the adapter's SharedCopilotDir resolves
    // somewhere empty (and predictable), instead of touching the
    // developer's real ~/.wl-workspaces/.shared.
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wl-test-root-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly CopilotService _adapter;

    public CopilotServiceTests()
    {
        Directory.CreateDirectory(_root);
        _adapter = new CopilotService(new WlPaths(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static LaunchSpec MakeSpec(
        List<string>? copilotArgs = null,
        string? resumeSessionId = null,
        List<string>? additionalDirs = null,
        string? sharedDir = null,
        string? folderPath = null,
        bool temporary = false,
        IReadOnlyList<string>? passThroughArgs = null)
    {
        var ws = new Workspace
        {
            Name = "test",
            PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
            AdditionalDirs = [],
            CopilotArgs = copilotArgs ?? [],
            FolderPath = folderPath ?? Path.Combine(Path.GetTempPath(), "wl-test-ws"),
        };

        return new LaunchSpec(
            Workspace: ws,
            PrimaryDirectory: ws.PrimaryRepo,
            SessionNameSlug: string.IsNullOrEmpty(Path.GetFileName(ws.FolderPath)) ? "wl" : Path.GetFileName(ws.FolderPath),
            CopilotArgs: ws.CopilotArgs,
            ResolvedAdditionalDirs: additionalDirs ?? [],
            ResolvedSharedDir: sharedDir,
            ResumeSessionId: resumeSessionId,
            TemporarySession: temporary,
            PassThroughArgs: passThroughArgs);
    }

    [Fact]
    public void NewSession_EmitsUuidAndReadableName_NoResume()
    {
        var spec = MakeSpec(folderPath: Path.Combine(Path.GetTempPath(), "sei"));
        var result = _adapter.BuildArgs(spec);

        Assert.NotNull(result.NewSessionId);
        Assert.True(Guid.TryParseExact(result.NewSessionId, "D", out var id));
        Assert.Contains($"--session-id={id}", result.Args);
        Assert.Contains($"--name=sei-{id.ToString("N")[..8]}", result.Args);
        Assert.DoesNotContain(result.Args, a => a.StartsWith("--resume"));
        Assert.NotEqual(result.NewSessionId, _adapter.BuildArgs(spec).NewSessionId);
    }

    [Fact]
    public void NewSession_NoFolderName_FallsBackToWlPrefix()
    {
        // Workspaces created with FolderPath that ends in a separator
        // (Path.GetFileName returns "") still need a valid --name.
        var spec = MakeSpec(folderPath: Path.GetTempPath());
        var result = _adapter.BuildArgs(spec);

        Assert.NotNull(result.NewSessionId);
        Assert.True(Guid.TryParse(result.NewSessionId, out _));
        Assert.Contains(result.Args, a => a.StartsWith("--name=wl-"));
    }

    [Fact]
    public void TemporarySession_EmitsTempNameAndSessionId()
    {
        var spec = MakeSpec(folderPath: Path.Combine(Path.GetTempPath(), "sei"), temporary: true);

        var result = _adapter.BuildArgs(spec);

        Assert.NotNull(result.NewSessionId);
        Assert.True(Guid.TryParseExact(result.NewSessionId, "D", out var id));
        Assert.Contains($"--session-id={id}", result.Args);
        Assert.Contains($"--name=sei-temp-{id.ToString("N")[..8]}", result.Args);
        Assert.DoesNotContain(result.Args, a => a.StartsWith("--resume"));
    }

    [Theory]
    [InlineData("sei-a1b2c3d4")]
    [InlineData("0cb916db-26aa-40f2-86b5-1ba81b225fd2")]
    public void ResumeSession_EmitsExistingReference_NoNewNameOrId(string sessionId)
    {
        var spec = MakeSpec(resumeSessionId: sessionId);
        var result = _adapter.BuildArgs(spec);

        Assert.Null(result.NewSessionId);
        Assert.Contains(result.Args, a => a == $"--resume={sessionId}");
        Assert.DoesNotContain(result.Args, a => a.StartsWith("--name"));
        Assert.DoesNotContain(result.Args, a => a.StartsWith("--session-id"));
    }

    [Fact]
    public void CopilotArgs_EmitsArgsInOrder()
    {
        var result = _adapter.BuildArgs(MakeSpec(copilotArgs: ["--yolo", "--model", "gpt-test"]));

        var yoloIndex = result.Args.IndexOf("--yolo");
        Assert.True(yoloIndex > result.Args.LastIndexOf("--add-dir"));
        Assert.Equal("--model", result.Args[yoloIndex + 1]);
        Assert.Equal("gpt-test", result.Args[yoloIndex + 2]);
    }

    [Fact]
    public void CopilotArgsAndPassThrough_KeepExpectedOrder()
    {
        var result = _adapter.BuildArgs(MakeSpec(
            copilotArgs: ["--model", "configured"],
            passThroughArgs: ["--model", "runtime"]));

        var configuredIndex = result.Args.IndexOf("configured") - 1;
        var runtimeIndex = result.Args.LastIndexOf("--model");
        Assert.True(configuredIndex < runtimeIndex);
        Assert.Equal("runtime", result.Args[runtimeIndex + 1]);
    }

    [Fact]
    public void NoCopilotArgs_AddsNothing()
    {
        var result = _adapter.BuildArgs(MakeSpec());

        Assert.DoesNotContain("--yolo", result.Args);
    }

    [Fact]
    public void NoPassThrough_NoDashI()
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
    public void PrepareLaunch_PreservesUserAgentsFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var agentsPath = Path.Combine(tempDir, "AGENTS.md");
            const string userContent = "# Workspace instructions";
            File.WriteAllText(agentsPath, userContent);

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            Assert.Equal(userContent, File.ReadAllText(agentsPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_NoAgentsFile_DoesNotWriteAgentsFile()
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

        var env = _adapter.GetEnvironment(ws, inheritedInstructionDirs: null);

        Assert.True(env.ContainsKey("COPILOT_CUSTOM_INSTRUCTIONS_DIRS"));
        Assert.Equal("/path/to/workspace-folder", env["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"]);
    }

    [Theory]
    [InlineData("", "/workspace")]
    [InlineData("  , , ", "/workspace")]
    [InlineData("/personal", "/personal,/workspace")]
    [InlineData("/personal,/team", "/personal,/team,/workspace")]
    [InlineData(" /personal, /team ,/personal ", "/personal,/team,/workspace")]
    [InlineData("/personal,/workspace", "/personal,/workspace")]
    public void GetEnvironment_PreservesInheritedDirectoriesWithoutDuplicates(string inherited, string expected)
    {
        var ws = new Workspace { FolderPath = "/workspace" };
        Assert.Equal(expected, _adapter.GetEnvironment(ws, inherited)["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"]);
    }

    [Fact]
    public void GetEnvironment_WithSharedAgentsMd_AddsSharedDirBeforeWorkspace()
    {
        var sharedDir = Path.Combine(_root, ".shared");
        Directory.CreateDirectory(sharedDir);
        File.WriteAllText(Path.Combine(sharedDir, "AGENTS.md"), "shared");
        var ws = new Workspace { FolderPath = "/workspace" };

        var env = _adapter.GetEnvironment(ws, "/personal");

        Assert.Equal($"/personal,{sharedDir},/workspace", env["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"]);
    }

    [Fact]
    public void GetEnvironment_SharedDirWithoutAgentsMd_IsNotAdded()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".shared", ".copilot"));
        var ws = new Workspace { FolderPath = "/workspace" };

        Assert.Equal("/workspace", _adapter.GetEnvironment(ws, null)["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"]);
    }

    [Fact]
    public void GetEnvironment_UsesPlatformPathComparison()
    {
        var ws = new Workspace { FolderPath = "/workspace" };
        var expected = OperatingSystem.IsWindows() ? "/WORKSPACE" : "/WORKSPACE,/workspace";
        Assert.Equal(expected, _adapter.GetEnvironment(ws, "/WORKSPACE")["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"]);
    }

    [Fact]
    public void DescribeLaunchPrep_WithAgentsMd_HasNoAgentsEntry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "AGENTS.md"), "user content");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            var prep = _adapter.DescribeLaunchPrep(ws).ToList();

            Assert.DoesNotContain(prep, line => line.Contains("writes", StringComparison.OrdinalIgnoreCase)
                && line.Contains("AGENTS.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(prep, line => line.Contains("deletes", StringComparison.OrdinalIgnoreCase)
                && line.Contains("AGENTS.md", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PrepareLaunch_WithSkills_WritesPluginManifestInWorkspaceCopilotDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            // Layout: <ws>/.copilot/skills/my-skill/SKILL.md
            var skillDir = Path.Combine(tempDir, ".copilot", "skills", "my-skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: my-skill\n---\nbody");

            var ws = new Workspace
            {
                Name = "test",
                PrimaryRepo = Path.Combine(Path.GetTempPath(), "wl-test-repo"),
                FolderPath = tempDir,
            };

            _adapter.PrepareLaunch(ws);

            var manifestPath = Path.Combine(tempDir, ".copilot", "plugin.json");
            Assert.True(File.Exists(manifestPath), "plugin.json should be written when skills exist");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            Assert.Matches($"^wl-{Path.GetFileName(tempDir)}-[0-9a-f]{{16}}$", json["name"]!.GetValue<string>());
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

            Assert.False(File.Exists(Path.Combine(tempDir, ".copilot", "plugin.json")));
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
        var skillDir = Path.Combine(wsFolder, ".copilot", "skills", "ws-skill");
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
            Assert.Contains(Path.Combine(wsFolder, ".copilot"), values);
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
        var skillDir = Path.Combine(wsFolder, ".copilot", "skills", "x");
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

            var manifest = Path.Combine(wsFolder, ".copilot", "plugin.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
            var name = json["name"]!.GetValue<string>();
            Assert.Matches("^wl-my-workspace-[0-9a-f]{16}$", name);
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
        var skillDir = Path.Combine(wsFolder, ".copilot", "skills", "x");
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

            var manifest = Path.Combine(wsFolder, ".copilot", "plugin.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
            Assert.Matches("^wl-homelab-[0-9a-f]{16}$", json["name"]!.GetValue<string>());
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
        var skillDir = Path.Combine(wsFolder, ".copilot", "skills", "x");
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

            var manifest = Path.Combine(wsFolder, ".copilot", "plugin.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
            Assert.Matches("^wl-workspace-[0-9a-f]{16}$", json["name"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private string ReadPluginName(string folderName)
    {
        var ws = new Workspace { Name = "test", FolderPath = Path.Combine(_root, folderName) };
        var skill = Directory.CreateDirectory(Path.Combine(ws.SkillsPath, "example")).FullName;
        File.WriteAllText(Path.Combine(skill, "SKILL.md"), "---\nname: example\ndescription: Example\n---\nExample");
        _adapter.PrepareLaunch(ws);
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(WlPaths.PluginManifest(ws.FolderPath)));
        return manifest.RootElement.GetProperty("name").GetString()!;
    }

    [Theory]
    [InlineData(44)]
    [InlineData(45)]
    [InlineData(61)]
    [InlineData(62)]
    [InlineData(100)]
    public void WorkspacePluginName_RespectsDocumentedLengthLimit(int folderLength)
    {
        var folder = new string('a', folderLength);
        var name = ReadPluginName(folder);
        Assert.Equal(64, name.Length);
        Assert.Matches("^wl-[a-z0-9]+(?:-[a-z0-9]+)*$", name);
        Assert.Equal(name, ReadPluginName(folder));
    }

    [Fact]
    public void WorkspacePluginName_TruncationDoesNotLeaveDoubleHyphens()
    {
        var name = ReadPluginName(new string('a', 43) + "-long-suffix");
        Assert.InRange(name.Length, 1, 64);
        Assert.DoesNotContain("--", name);
    }

    [Theory]
    [InlineData("my workspace", "my-workspace")]
    [InlineData("~~~", "___")]
    public void WorkspacePluginName_SlugCollisionsRemainDistinct(string first, string second)
        => Assert.NotEqual(ReadPluginName(first), ReadPluginName(second));

    [Fact]
    public void WorkspacePluginName_TruncatedNamesRemainDistinct()
    {
        var prefix = new string('a', 100);
        Assert.NotEqual(ReadPluginName(prefix + "b"), ReadPluginName(prefix + "c"));
    }

    [Fact]
    public void WorkspacePluginName_DoesNotCollideWithSharedPlugin()
        => Assert.NotEqual(CopilotService.SharedPluginName, ReadPluginName("shared"));
}