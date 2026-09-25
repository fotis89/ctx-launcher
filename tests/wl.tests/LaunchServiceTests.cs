using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.tests;

public class LaunchServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("wl-launch-").FullName;
    private readonly LaunchService _service;
    private readonly Workspace _ws;

    public LaunchServiceTests()
    {
        var paths = new WlPaths(_root);
        _service = new LaunchService(new CopilotRunner(), new PathsService(paths.PathsConfigFile), new CopilotService(paths));
        _ws = new Workspace { Name = "test", PrimaryRepo = _root, FolderPath = _root };
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void BuildLaunchArgs_NoAdditionalDirs_OnlyWorkspaceFolder()
    {
        var (args, _, id) = _service.BuildLaunchArgs(_ws);
        Assert.Single(args, a => a == "--add-dir");
        Assert.Contains(_root, args);
        Assert.True(Guid.TryParse(id, out _));
        Assert.Contains($"--session-id={id}", args);
        Assert.Contains(args, a => a.StartsWith("--name="));
    }

    [Fact]
    public void BuildLaunchArgs_ResolvesVariablesAndSkipsMissingDirectoriesAndFiles()
    {
        var paths = new WlPaths(_root);
        var vars = new PathsService(paths.PathsConfigFile);
        vars.Set("ROOT", _root);
        var file = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(file, "");
        _ws.AdditionalDirs = ["$ROOT", Path.Combine(_root, "missing"), file];
        var service = new LaunchService(new CopilotRunner(), vars, new CopilotService(paths));
        var (args, skipped, _) = service.BuildLaunchArgs(_ws);
        Assert.Equal(2, args.Count(a => a == "--add-dir"));
        Assert.Equal(_ws.AdditionalDirs.Skip(1), skipped);
    }

    [Fact]
    public void RelativePrimaryRepoAndAdditionalDirsResolveFromWorkspaceFolder()
    {
        var workspaceFolder = Directory.CreateDirectory(Path.Combine(_root, "workspace")).FullName;
        var repo = Directory.CreateDirectory(Path.Combine(workspaceFolder, "repo")).FullName;
        var additional = Directory.CreateDirectory(Path.Combine(workspaceFolder, "extras")).FullName;
        var otherCwd = Directory.CreateDirectory(Path.Combine(_root, "other-cwd")).FullName;
        var runner = new CapturingRunner();
        var paths = new WlPaths(_root);
        var service = new LaunchService(runner, new PathsService(paths.PathsConfigFile), new CopilotService(paths));
        var ws = new Workspace
        {
            Name = "test",
            PrimaryRepo = "repo",
            AdditionalDirs = ["extras"],
            FolderPath = workspaceFolder,
        };
        var oldCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(otherCwd);

            var (args, skipped, _) = service.BuildLaunchArgs(ws);
            service.Launch(ws, args);

            Assert.Empty(skipped);
            Assert.Contains(additional, args);
            Assert.Equal(repo, runner.WorkingDirectory);
        }
        finally
        {
            Directory.SetCurrentDirectory(oldCwd);
        }
    }

    [Fact]
    public void BuildLaunchArgs_PromptAndYolo()
    {
        var (args, _, _) = _service.BuildLaunchArgs(_ws, "do the thing", yolo: true);
        Assert.Contains("--yolo", args);
        Assert.Equal("-i", args[^2]);
        Assert.Equal("do the thing", args[^1]);
    }

    [Fact]
    public void BuildLaunchArgs_Resume_DoesNotCreateName()
    {
        var (args, _, id) = _service.BuildLaunchArgs(_ws, resumeSessionId: "test-12345678");
        Assert.Null(id);
        Assert.Contains("--resume=test-12345678", args);
        Assert.DoesNotContain(args, a => a.StartsWith("--name"));
        Assert.DoesNotContain(args, a => a.StartsWith("--session-id"));
    }

    [Fact]
    public void BuildLaunchArgs_SharedDir_BeforeWorkspaceFolder()
    {
        var shared = Path.Combine(_root, ".shared");
        var (args, _, _) = _service.BuildLaunchArgs(_ws, sharedDirPath: shared);
        Assert.True(args.IndexOf(shared) < args.IndexOf(_root));
    }

    [Fact]
    public void BuildCommandString_QuotesPathsWithoutQuotingActualArguments()
    {
        _ws.FolderPath = Path.Combine(_root, "with spaces");
        var (args, _, _) = _service.BuildLaunchArgs(_ws);
        Assert.Contains(_ws.FolderPath, args);
        Assert.DoesNotContain($"\"{_ws.FolderPath}\"", args);
        var command = _service.BuildCommandString(_ws);
        Assert.StartsWith("copilot ", command);
        Assert.Contains($"\"{_ws.FolderPath}\"", command);
    }

    [Theory]
    [InlineData("test-12345678")]
    [InlineData("0cb916db-26aa-40f2-86b5-1ba81b225fd2")]
    public void LastSession_RoundTripsPlainCopilotReference(string reference)
    {
        LaunchService.SaveLastSession(_ws, reference);
        Assert.Equal(reference, File.ReadAllText(_ws.LastSessionPath));
        Assert.Equal(reference, LaunchService.LoadLastSession(_ws));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void LastSession_Missing_ReturnsNull() => Assert.Null(LaunchService.LoadLastSession(_ws));

    [Fact]
    public void LastSession_Empty_ReturnsNull()
    {
        File.WriteAllText(_ws.LastSessionPath, "  ");
        Assert.Null(LaunchService.LoadLastSession(_ws));
    }

    [Theory]
    [InlineData("one\ntwo")]
    public void LastSession_WithControlCharacters_IsRejectedWithoutMutation(string content)
    {
        File.WriteAllText(_ws.LastSessionPath, content);
        Assert.Throws<InvalidDataException>(() => LaunchService.LoadLastSession(_ws));
        Assert.Equal(content, File.ReadAllText(_ws.LastSessionPath));
    }

    [Fact]
    public void LastSession_Locked_ThrowsInsteadOfStartingFresh()
    {
        File.WriteAllText(_ws.LastSessionPath, "test-12345678");
        using var locker = new FileStream(_ws.LastSessionPath, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.Throws<IOException>(() => LaunchService.LoadLastSession(_ws));
    }

    private sealed class CapturingRunner : CopilotRunner
    {
        public string? WorkingDirectory { get; private set; }

        public override int Run(string workingDirectory, IEnumerable<string> args, IReadOnlyDictionary<string, string>? environment = null)
        {
            WorkingDirectory = workingDirectory;
            return 0;
        }
    }
}