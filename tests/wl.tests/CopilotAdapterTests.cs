using wl.Models;
using wl.Services;

namespace wl.tests;

[Collection("StderrCapture")]
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
    public void UnregisterSkillsDirs_RemovesOnlyMatchingPaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-settings-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(settingsPath, """
                {
                  "model": "claude-opus-4.7",
                  "skillDirectories": [
                    "C:\\user\\added\\path",
                    "C:\\wl\\managed\\workspace-a",
                    "C:\\wl\\managed\\workspace-b"
                  ]
                }
                """);

            CopilotAdapter.UnregisterSkillsDirs(
                new[] { "C:\\wl\\managed\\workspace-a", "C:\\wl\\managed\\workspace-b" },
                settingsPath);

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            var dirs = json["skillDirectories"]!.AsArray().Select(d => d!.GetValue<string>()).ToList();
            Assert.Single(dirs);
            Assert.Equal("C:\\user\\added\\path", dirs[0]);
            // Other settings keys preserved
            Assert.Equal("claude-opus-4.7", json["model"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void UnregisterSkillsDirs_NoMatch_FileUntouched()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-settings-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsPath = Path.Combine(tempDir, "settings.json");
            var original = """{"skillDirectories":["C:\\user\\path"]}""";
            File.WriteAllText(settingsPath, original);
            var beforeWrite = File.GetLastWriteTimeUtc(settingsPath);

            Thread.Sleep(50);
            CopilotAdapter.UnregisterSkillsDirs(new[] { "C:\\not\\registered" }, settingsPath);

            var afterWrite = File.GetLastWriteTimeUtc(settingsPath);
            Assert.Equal(beforeWrite, afterWrite);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RegisterSkillsDir_FreshSettings_AddsPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-settings-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsPath = Path.Combine(tempDir, "settings.json");
            var skillsDir = Path.Combine(tempDir, "ws", "wl-skills-plugin", "skills");

            CopilotAdapter.RegisterSkillsDir(skillsDir, settingsPath);

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            var dirs = json["skillDirectories"]!.AsArray();
            Assert.Contains(dirs, d => d!.GetValue<string>() == skillsDir);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RegisterSkillsDir_ExistingSettings_PreservesOtherKeys()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-settings-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(settingsPath, """
                {
                  "model": "claude-opus-4.7",
                  "trustedFolders": ["D:\\repos\\foo"],
                  "skillDirectories": ["C:\\old\\path"]
                }
                """);

            CopilotAdapter.RegisterSkillsDir("C:\\new\\path", settingsPath);

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.Equal("claude-opus-4.7", json["model"]!.GetValue<string>());
            Assert.Single(json["trustedFolders"]!.AsArray());

            var dirs = json["skillDirectories"]!.AsArray().Select(d => d!.GetValue<string>()).ToList();
            Assert.Equal(2, dirs.Count);
            Assert.Contains("C:\\old\\path", dirs);
            Assert.Contains("C:\\new\\path", dirs);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RegisterSkillsDir_AlreadyRegistered_NoDuplicate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-settings-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(settingsPath, """{"skillDirectories": ["C:\\my\\skills"]}""");

            CopilotAdapter.RegisterSkillsDir("C:\\my\\skills", settingsPath);

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.Single(json["skillDirectories"]!.AsArray());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RegisterSkillsDir_UnexpectedShape_RefusesAndWarns()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-copilot-settings-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        var stderr = new StringWriter();
        var prev = Console.Error;
        try
        {
            Console.SetError(stderr);

            // Simulate Copilot changing skillDirectories from array to object.
            var settingsPath = Path.Combine(tempDir, "settings.json");
            var originalContent = """{"skillDirectories": {"some": "object"}, "preserved": true}""";
            File.WriteAllText(settingsPath, originalContent);

            CopilotAdapter.RegisterSkillsDir("C:\\my\\skills", settingsPath);

            // File untouched.
            Assert.Equal(originalContent, File.ReadAllText(settingsPath));
            // Warning surfaced.
            var output = stderr.ToString();
            Assert.Contains("unexpected shape", output);
            Assert.Contains("Refusing to modify", output);
        }
        finally
        {
            Console.SetError(prev);
            Directory.Delete(tempDir, true);
        }
    }
}
