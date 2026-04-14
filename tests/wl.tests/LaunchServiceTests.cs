using wl.Models;
using wl.Services;

namespace wl.tests;

public class LaunchServiceTests
{
    private readonly LaunchService _service = new();

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
        var (args, _) = _service.BuildClaudeArgs(ws);

        Assert.Single(args, a => a.Contains("--add-dir"));
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
            var (args, _) = _service.BuildClaudeArgs(ws);

            var addDirArgs = args.Where(a => a.Contains("--add-dir")).ToList();
            Assert.Equal(2, addDirArgs.Count);
            Assert.Contains(addDirArgs, a => a.Contains(tempDir));
            Assert.Contains(addDirArgs, a => a.Contains(ws.FolderPath));
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
        var (args, _) = _service.BuildClaudeArgs(ws);

        var addDirArgs = args.Where(a => a.Contains("--add-dir")).ToList();
        Assert.Single(addDirArgs);
        Assert.Contains(ws.FolderPath, addDirArgs[0]);
    }

    [Fact]
    public void BuildClaudeArgs_WithPrompt_AppendsAsLastArg()
    {
        var ws = MakeWorkspace();
        var (args, _) = _service.BuildClaudeArgs(ws, "do the thing");

        Assert.Contains("\"do the thing\"", args.Last());
    }

    [Fact]
    public void BuildClaudeArgs_WithoutPrompt_NoTrailingArg()
    {
        var ws = MakeWorkspace();
        var (args, _) = _service.BuildClaudeArgs(ws);

        Assert.All(args, a => Assert.Contains("--", a));
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
            var (args, _) = _service.BuildClaudeArgs(ws);

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
            var (args, _) = _service.BuildClaudeArgs(ws);

            Assert.DoesNotContain(args, a => a.Contains("--append-system-prompt-file"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildClaudeArgs_PathWithSpaces_Quoted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl test dir " + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeWorkspace(folderPath: tempDir);
            var (args, _) = _service.BuildClaudeArgs(ws);

            var folderArg = args.First(a => a.Contains(tempDir));
            Assert.Contains("\"", folderArg);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void BuildCommandString_StartsWithClaude()
    {
        var ws = MakeWorkspace();
        var cmd = _service.BuildCommandString(ws);
        Assert.StartsWith("claude ", cmd);
    }
}