using wl.Models;
using wl.Services;

namespace wl.tests;

public class LaunchServiceTests
{
    private readonly LaunchService _service = new(new ClaudeRunner(), new PathsService(Path.Combine(Path.GetTempPath(), $"wl-paths-test-{Guid.NewGuid():N}.json")));

    private static Workspace MakeWorkspace(
        string? folderPath = null,
        string? primaryRepo = null,
        List<string>? additionalDirs = null)
    {
        var folder = folderPath ?? Path.GetTempPath();
        return new Workspace
        {
            Name = "test",
            PrimaryRepo = primaryRepo ?? Path.Combine(Path.GetTempPath(), "wl-test-repo"),
            AdditionalDirs = additionalDirs ?? [],

            FolderPath = folder,
        };
    }

    [Fact]
    public void BuildClaudeArgs_NoAdditionalDirs_OnlyWorkspaceFolder()
    {
        var ws = MakeWorkspace();
        var (args, _, _) = _service.BuildClaudeArgs(ws);

        Assert.Single(args, a => a == "--add-dir");
        Assert.Contains(args, a => a.Contains(ws.FolderPath));
    }

    [Fact]
    public void BuildClaudeArgs_WithAdditionalDirs_AllIncluded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(additionalDirs: [tempDir]);
            var (args, _, _) = _service.BuildClaudeArgs(ws);

            Assert.Equal(2, args.Count(a => a == "--add-dir"));
            // additionalDirs entries go through ResolvePath → native separators on Windows,
            // forward slashes on Linux/macOS.
            var expectedDir = OperatingSystem.IsWindows() ? tempDir : tempDir.Replace('\\', '/');
            Assert.Contains(args, a => a.Contains(expectedDir));
            Assert.Contains(args, a => a.Contains(ws.FolderPath));
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void BuildClaudeArgs_MissingAdditionalDir_Skipped()
    {
        var ws = MakeWorkspace(additionalDirs: [@"C:\this\does\not\exist\at\all"]);
        var (args, _, _) = _service.BuildClaudeArgs(ws);

        Assert.Single(args, a => a == "--add-dir");
        Assert.Contains(args, a => a.Contains(ws.FolderPath));
    }

    [Fact]
    public void BuildClaudeArgs_WithPrompt_AppendsAsLastArg()
    {
        var ws = MakeWorkspace();
        var (args, _, _) = _service.BuildClaudeArgs(ws, "do the thing");

        Assert.Equal("do the thing", args.Last());
    }

    [Fact]
    public void BuildClaudeArgs_WithoutPrompt_NoTrailingPromptArg()
    {
        var ws = MakeWorkspace();
        var (args, _, _) = _service.BuildClaudeArgs(ws);

        // Last arg should be a path (the workspace folder), not a prompt string
        Assert.Contains(ws.FolderPath, args.Last());
    }

    [Fact]
    public void BuildClaudeArgs_WithInstructions_AppendsSystemPromptFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "instructions.md"), "test context");
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            var (args, _, _) = _service.BuildClaudeArgs(ws);

            Assert.Contains(args, a => a.Contains("--append-system-prompt-file"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildClaudeArgs_MissingInstructions_NoSystemPromptFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            var (args, _, _) = _service.BuildClaudeArgs(ws);

            Assert.DoesNotContain(args, a => a.Contains("--append-system-prompt-file"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildClaudeArgs_PathWithSpaces_RawUnquoted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl test dir " + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            var (args, _, _) = _service.BuildClaudeArgs(ws);

            var folderArg = args.First(a => a.Contains(tempDir));
            Assert.DoesNotContain("\"", folderArg);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildCommandString_PathWithSpaces_Quoted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl test dir " + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            var cmd = _service.BuildCommandString(ws);

            Assert.Contains($"\"{tempDir}\"", cmd);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildCommandString_StartsWithClaude()
    {
        var ws = MakeWorkspace();
        var cmd = _service.BuildCommandString(ws);
        Assert.StartsWith("claude ", cmd);
    }

    [Fact]
    public void BuildClaudeArgs_Default_IncludesSessionIdAndName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            var (args, _, _) = _service.BuildClaudeArgs(ws);

            Assert.Contains("--session-id", args);
            Assert.Contains("--name", args);

            var nameIdx = args.IndexOf("--name");
            Assert.Equal(ws.Name, args[nameIdx + 1]);

            var sidIdx = args.IndexOf("--session-id");
            Assert.True(Guid.TryParse(args[sidIdx + 1], out _));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildClaudeArgs_Resume_IncludesResumeWithSessionId()
    {
        var ws = MakeWorkspace();
        var sessionId = Guid.NewGuid().ToString();
        var (args, _, _) = _service.BuildClaudeArgs(ws, resumeSessionId: sessionId);

        Assert.Contains("--resume", args);
        Assert.DoesNotContain("--session-id", args);
        Assert.DoesNotContain("--name", args);

        var resumeIdx = args.IndexOf("--resume");
        Assert.Equal(sessionId, args[resumeIdx + 1]);
    }

    [Fact]
    public void SaveAndLoadLastSession_RoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            var sessionId = Guid.NewGuid().ToString();

            LaunchService.SaveLastSession(ws, sessionId);
            var loaded = LaunchService.LoadLastSession(ws);

            Assert.Equal(sessionId, loaded);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildClaudeArgs_WithSharedDir_IncludesAddDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-shared-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace();
            var (args, _, _) = _service.BuildClaudeArgs(ws, sharedDirPath: tempDir);

            Assert.Equal(2, args.Count(a => a == "--add-dir"));
            Assert.Contains(args, a => a == tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildClaudeArgs_NullSharedDir_NotIncluded()
    {
        var ws = MakeWorkspace();
        var (args, _, _) = _service.BuildClaudeArgs(ws, sharedDirPath: null);

        Assert.Single(args, a => a == "--add-dir");
    }

    [Fact]
    public void BuildClaudeArgs_SharedDir_BeforeWorkspaceFolder()
    {
        var sharedDir = Path.Combine(Path.GetTempPath(), "wl-test-shared-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(sharedDir);
        try
        {
            var ws = MakeWorkspace();
            var (args, _, _) = _service.BuildClaudeArgs(ws, sharedDirPath: sharedDir);

            var sharedIdx = args.IndexOf(sharedDir);
            var wsIdx = args.IndexOf(ws.FolderPath);
            Assert.True(sharedIdx < wsIdx, "Shared dir should appear before workspace folder");
        }
        finally
        {
            Directory.Delete(sharedDir, true);
        }
    }

    [Fact]
    public void BuildCommandString_WithSharedDir_IncludesPath()
    {
        var sharedDir = Path.Combine(Path.GetTempPath(), "wl-test-shared-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(sharedDir);
        try
        {
            var ws = MakeWorkspace();
            var cmd = _service.BuildCommandString(ws, sharedDirPath: sharedDir);

            Assert.Contains(sharedDir, cmd);
        }
        finally
        {
            Directory.Delete(sharedDir, true);
        }
    }

    [Fact]
    public void LoadLastSession_NoFile_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            Assert.Null(LaunchService.LoadLastSession(ws));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadLastSession_CorruptFile_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".last-session"), "not-a-guid");
            var ws = MakeWorkspace(folderPath: tempDir);
            Assert.Null(LaunchService.LoadLastSession(ws));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}