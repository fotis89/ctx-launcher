using wl.Models;
using wl.Services;

namespace wl.tests;

[Collection("StderrCapture")]
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

    [Fact]
    public void ResolveTool_OverrideBeatsEverything()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaultTool": "copilot" }""");
        try
        {
            var svc = new ConfigService(file);
            var ws = new Workspace { Tool = "copilot" };
            Assert.Equal("claude", svc.ResolveTool(ws, overrideTool: "claude"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveToolWithSource_ReturnsCorrectSource()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaultTool": "copilot" }""");
        try
        {
            var svc = new ConfigService(file);

            var (_, overrideSrc) = svc.ResolveToolWithSource(new Workspace { Tool = "claude" }, overrideTool: "copilot");
            Assert.Equal(ToolSource.Override, overrideSrc);

            var (_, wsSrc) = svc.ResolveToolWithSource(new Workspace { Tool = "claude" });
            Assert.Equal(ToolSource.Workspace, wsSrc);

            var (_, configSrc) = svc.ResolveToolWithSource(new Workspace { Tool = null });
            Assert.Equal(ToolSource.Config, configSrc);
        }
        finally
        {
            File.Delete(file);
        }

        var emptySvc = new ConfigService(TempFile());
        var (_, defaultSrc) = emptySvc.ResolveToolWithSource(new Workspace { Tool = null });
        Assert.Equal(ToolSource.Default, defaultSrc);
    }

    [Fact]
    public void Load_UnknownKey_WarnsToStderr()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaulttool": "copilot" }""");
        var stderr = new StringWriter();
        var prev = Console.Error;
        try
        {
            Console.SetError(stderr);
            var svc = new ConfigService(file);
            _ = svc.DefaultTool;
        }
        finally
        {
            Console.SetError(prev);
            File.Delete(file);
        }

        var output = stderr.ToString();
        Assert.Contains("unknown key 'defaulttool'", output);
        Assert.Contains("did you mean 'defaultTool'", output);
    }

    [Fact]
    public void Load_UnknownUnrelatedKey_WarnsWithoutSuggestion()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "totallyMadeUp": "x" }""");
        var stderr = new StringWriter();
        var prev = Console.Error;
        try
        {
            Console.SetError(stderr);
            var svc = new ConfigService(file);
            _ = svc.DefaultTool;
        }
        finally
        {
            Console.SetError(prev);
            File.Delete(file);
        }

        var output = stderr.ToString();
        Assert.Contains("unknown key 'totallyMadeUp'", output);
        Assert.DoesNotContain("did you mean", output);
    }

    [Fact]
    public void Load_ValidConfig_PrintsNoWarnings()
    {
        var file = TempFile();
        File.WriteAllText(file, """{ "defaultTool": "copilot" }""");
        var stderr = new StringWriter();
        var prev = Console.Error;
        try
        {
            Console.SetError(stderr);
            var svc = new ConfigService(file);
            _ = svc.DefaultTool;
        }
        finally
        {
            Console.SetError(prev);
            File.Delete(file);
        }

        Assert.Empty(stderr.ToString());
    }
}
