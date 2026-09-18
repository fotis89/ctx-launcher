using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.tests;

[Collection("StderrCapture")]
public class WorkspaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wl-ws-test-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
    {
        Directory.CreateDirectory(_root);
        _service = new WorkspaceService(new WlPaths(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void SaveWorkspace_LeavesNoTmpFile_OnSuccess()
    {
        // Atomic write: tmp + Move. After a successful save the .tmp must
        // not linger and the target must exist with the serialized content.
        var ws = new Workspace
        {
            Name = "demo",
            PrimaryRepo = Path.Combine(_root, "repo"),
            AdditionalDirs = [],
        };

        _service.SaveWorkspace(ws, "demo");

        var jsonPath = Path.Combine(_root, "demo", "workspace.json");
        Assert.True(File.Exists(jsonPath));
        Assert.False(File.Exists(jsonPath + ".tmp"));
    }

    [Fact]
    public void SaveWorkspace_OverwritesExisting()
    {
        // Re-saving the same workspace must overwrite atomically — no
        // partial writes, no .tmp leftover.
        var ws = new Workspace
        {
            Name = "first",
            PrimaryRepo = Path.Combine(_root, "repo"),
            AdditionalDirs = [],
        };
        _service.SaveWorkspace(ws, "demo");

        ws.Name = "second";
        _service.SaveWorkspace(ws, "demo");

        var loaded = _service.LoadWorkspace("demo");
        Assert.NotNull(loaded);
        Assert.Equal("second", loaded.Name);
        Assert.False(File.Exists(Path.Combine(_root, "demo", "workspace.json.tmp")));
    }

    [Fact]
    public void GetLastUsed_LockedFile_ReturnsNullAndWarns()
    {
        // .last is a convenience pointer; if it's locked we should fall
        // back to "no last-used" rather than crashing the launch flow.
        var lastFile = Path.Combine(_root, ".last");
        File.WriteAllText(lastFile, "demo");

        var stderr = new StringWriter();
        var prev = Console.Error;
        using var locker = new FileStream(lastFile, FileMode.Open, FileAccess.Read, FileShare.None);
        try
        {
            Console.SetError(stderr);
            var name = _service.GetLastUsed();
            Assert.Null(name);
        }
        finally
        {
            Console.SetError(prev);
            locker.Dispose();
        }

        Assert.Contains("cannot read", stderr.ToString());
    }

    [Fact]
    public void ListWorkspaces_MissingRoot_ReturnsEmptyWithoutCreatingIt()
    {
        var bogus = Path.Combine(Path.GetTempPath(), "wl-ws-not-there-" + Guid.NewGuid().ToString("N")[..8]);
        var paths = new WlPaths(bogus);
        Directory.CreateDirectory(paths.WorkspacesRoot);
        Directory.Delete(paths.WorkspacesRoot);
        var service = new WorkspaceService(paths);

        var stderr = new StringWriter();
        var prev = Console.Error;
        try
        {
            Console.SetError(stderr);
            var list = service.ListWorkspaces();
            Assert.Empty(list);
        }
        finally
        {
            Console.SetError(prev);
        }

        Assert.Equal("", stderr.ToString());
        Assert.False(Directory.Exists(bogus));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{broken")]
    [InlineData("{\"schemaVersion\":2,\"name\":42,\"primaryRepo\":\"repo\"}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":3}")]
    [InlineData("{\"schemaVersion\":null}")]
    [InlineData("{\"schemaVersion\":2,\"tool\":\"copilot\"}")]
    [InlineData("{\"schemaVersion\":2,\"name\":\"test\",\"primaryRepo\":\"repo\",\"additionalDirs\":null}")]
    [InlineData("{\"schemaVersion\":2,\"name\":\"test\",\"primaryRepo\":\"repo\",\"additionalDirs\":[null]}")]
    [InlineData("{\"schemaVersion\":2,\"name\":\"test\",\"primaryRepo\":null}")]
    public void InvalidWorkspace_IsRejectedAndStillListed(string json)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "invalid")).FullName;
        var config = Path.Combine(folder, "workspace.json");
        File.WriteAllText(config, json);
        Assert.Throws<InvalidDataException>(() => _service.LoadWorkspace("invalid"));
        var entry = Assert.Single(_service.ListEntries());
        Assert.Equal("invalid", entry.FolderName);
        Assert.NotNull(entry.Error);
        Assert.Null(entry.Workspace);
        Assert.Equal(json, File.ReadAllText(config));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("copilot")]
    public void LegacyDefaultTool_IsRejectedWithoutMutation(string tool)
    {
        var config = Path.Combine(_root, ".config.json");
        var json = $"{{\"defaultTool\":\"{tool}\"}}";
        File.WriteAllText(config, json);
        Assert.Throws<InvalidDataException>(() => _service.ValidateEnvironment());
        Assert.Equal(json, File.ReadAllText(config));
        Assert.False(Directory.Exists(Path.Combine(_root, ".shared")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData(".shared")]
    public void InvalidWorkspaceName_IsRejected(string name)
        => Assert.Throws<ArgumentException>(() => _service.GetWorkspaceFolder(name));
}