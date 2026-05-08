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
    public void ListWorkspaces_EnumerationFailure_ReturnsEmptyAndWarns()
    {
        // Use an instance pointing at a non-existent root so
        // EnumerateDirectories throws DirectoryNotFoundException
        // (a subclass of IOException). ListWorkspaces should warn and
        // return [] instead of crashing the calling command.
        var bogus = Path.Combine(Path.GetTempPath(), "wl-ws-not-there-" + Guid.NewGuid().ToString("N")[..8]);
        var paths = new WlPaths(bogus);
        // touch the root once so subsequent code thinks it exists, then
        // delete to force the enumeration failure.
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

        Assert.Contains("cannot enumerate", stderr.ToString());
    }
}
