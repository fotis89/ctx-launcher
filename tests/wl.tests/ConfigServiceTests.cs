using wl.Models;
using wl.Services;

namespace wl.tests;

public class ConfigServiceTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"wl-config-{Guid.NewGuid():N}.json");

    [Fact]
    public void DefaultTool_MissingFile_ReturnsNull()
    {
        var svc = new ConfigService(TempFile());
        Assert.Null(svc.DefaultTool);
    }

    [Fact]
    public void DefaultTool_MalformedJson_ReturnsNullAndDoesNotThrow()
    {
        var file = TempFile();
        File.WriteAllText(file, "{ not valid json");
        try
        {
            var svc = new ConfigService(file);
            Assert.Null(svc.DefaultTool);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void DefaultTool_ValidJson_ReturnsValue()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaultTool": "copilot" }""");
        try
        {
            var svc = new ConfigService(file);
            Assert.Equal("copilot", svc.DefaultTool);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void DefaultTool_EmptyObject_ReturnsNull()
    {
        var file = TempFile();
        File.WriteAllText(file, "{}");
        try
        {
            var svc = new ConfigService(file);
            Assert.Null(svc.DefaultTool);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveTool_WorkspaceToolWins()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaultTool": "copilot" }""");
        try
        {
            var svc = new ConfigService(file);
            var ws = new Workspace { Tool = "claude" };
            Assert.Equal("claude", svc.ResolveTool(ws));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveTool_NullWorkspaceTool_FallsBackToConfig()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaultTool": "copilot" }""");
        try
        {
            var svc = new ConfigService(file);
            var ws = new Workspace { Tool = null };
            Assert.Equal("copilot", svc.ResolveTool(ws));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveTool_EmptyWorkspaceTool_FallsBackToConfig()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaultTool": "copilot" }""");
        try
        {
            var svc = new ConfigService(file);
            var ws = new Workspace { Tool = "" };
            Assert.Equal("copilot", svc.ResolveTool(ws));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveTool_NoConfigNoWorkspaceTool_DefaultsToClaude()
    {
        var svc = new ConfigService(TempFile());
        var ws = new Workspace { Tool = null };
        Assert.Equal("claude", svc.ResolveTool(ws));
    }
}
