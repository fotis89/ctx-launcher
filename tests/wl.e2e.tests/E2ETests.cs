using System.Text.RegularExpressions;

namespace wl.e2e.tests;

public class E2ETests
{
    private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+(-[\w\.]+)?(\+[0-9a-f]+)?$");

    [SkippableFact]
    public void Version_exits_zero_and_matches_semver()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "--version");
        Assert.Equal(0, result.ExitCode);
        Assert.Matches(VersionPattern, result.Stdout.Trim());
    }

    [SkippableFact]
    public void Help_exits_zero_and_prints_usage()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "--help");
        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Stdout));
        var text = result.Stdout + result.Stderr;
        Assert.True(
            text.Contains("Usage", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Commands", StringComparison.OrdinalIgnoreCase),
            $"Expected help output to contain 'Usage' or 'Commands'. Got:\n{text}");
    }

    [SkippableFact]
    public void Unknown_command_exits_nonzero()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "not-a-real-command");
        Assert.NotEqual(0, result.ExitCode);
    }

    [SkippableFact]
    public void List_in_empty_home_exits_zero()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "list");
        Assert.Equal(0, result.ExitCode);
    }

    [SkippableFact]
    public void Create_basic_exits_zero()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, result.ExitCode);
    }

    [SkippableFact]
    public void Which_known_workspace_exits_zero()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        var result = WlRunner.Run(home.Path, extraPathDir: null, "which", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(home.WorkspaceName, result.Stdout, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void List_after_create_shows_workspace()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        var result = WlRunner.Run(home.Path, extraPathDir: null, "list");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(home.WorkspaceName, result.Stdout, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Edit_unknown_workspace_reports_error()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "edit", "nonexistent-ws");
        Assert.Contains("not found", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public void Launch_invokes_claude_shim()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        var fakeBin = Path.Combine(home.Path, "fake-bin");
        var claudeLog = Path.Combine(home.Path, "claude.log");
        FakeClaude.Install(fakeBin, claudeLog);

        var result = WlRunner.Run(home.Path, extraPathDir: fakeBin, "launch", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(claudeLog), $"Expected fake claude to be invoked. stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(claudeLog)));
    }

    [SkippableFact]
    public void Setup_exits_zero_when_claude_on_path()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var fakeBin = Path.Combine(home.Path, "fake-bin");
        var claudeLog = Path.Combine(home.Path, "claude.log");
        FakeClaude.Install(fakeBin, claudeLog);

        var result = WlRunner.Run(home.Path, extraPathDir: fakeBin, "setup");
        Assert.Equal(0, result.ExitCode);
    }

    private sealed class TempHome : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("wl-e2e-").FullName;
        public string WorkspaceName { get; } = "e2e-" + Guid.NewGuid().ToString("N")[..8];

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }

            // On Windows, Environment.GetFolderPath(SpecialFolder.UserProfile) uses
            // SHGetKnownFolderPath(FOLDERID_Profile) and ignores the USERPROFILE env var,
            // so wl writes to the real profile. Clean up the unique-named workspace from there.
            if (OperatingSystem.IsWindows())
            {
                var realProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                if (!string.IsNullOrEmpty(realProfile))
                {
                    var wsDir = System.IO.Path.Combine(realProfile, ".wl-workspaces", WorkspaceName);
                    try { if (Directory.Exists(wsDir)) Directory.Delete(wsDir, recursive: true); } catch { }
                }
            }
        }
    }
}
