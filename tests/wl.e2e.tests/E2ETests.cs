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
        var json = File.ReadAllText(System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName, "workspace.json"));
        Assert.Contains("\"schemaVersion\": 2", json);
        Assert.DoesNotContain("\"tool\"", json);
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
        Assert.NotEqual(0, result.ExitCode);
    }

    [SkippableFact]
    public void Launch_invokes_copilot_shim()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        var fakeBin = Path.Combine(home.Path, "fake-bin");
        var copilotLog = Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(fakeBin, copilotLog);

        var result = WlRunner.Run(home.Path, extraPathDir: fakeBin, "launch", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(copilotLog), $"Expected fake copilot to be invoked. stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(copilotLog)));
    }

    [SkippableFact]
    public void Paths_set_and_list_shows_variable()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();

        var set = WlRunner.Run(home.Path, extraPathDir: null, "paths", "set", "MYREPOS", "/tmp/x");
        Assert.Equal(0, set.ExitCode);

        var list = WlRunner.Run(home.Path, extraPathDir: null, "paths", "list");
        Assert.Equal(0, list.ExitCode);
        Assert.Contains("MYREPOS", list.Stdout, StringComparison.Ordinal);
        Assert.Contains("/tmp/x", list.Stdout, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Paths_set_invalid_name_exits_nonzero_with_error()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();

        var result = WlRunner.Run(home.Path, extraPathDir: null, "paths", "set", "bad name", "/tmp/x");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Invalid variable name", result.Stderr, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Which_with_unset_variable_shows_warning()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();

        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        // Inject a $VAR reference into the scaffolded workspace.json.
        var wsPath = System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName, "workspace.json");
        var json = File.ReadAllText(wsPath);
        json = json.Replace("\"additionalDirs\": []", "\"additionalDirs\": [\"$E2E_UNSET/foo\"]");
        File.WriteAllText(wsPath, json);

        var result = WlRunner.Run(home.Path, extraPathDir: null, "which", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("$E2E_UNSET", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("unset", result.Stdout, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Setup_exits_zero_when_copilot_on_path()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var fakeBin = Path.Combine(home.Path, "fake-bin");
        var copilotLog = Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(fakeBin, copilotLog);

        var result = WlRunner.Run(home.Path, extraPathDir: fakeBin, "setup");
        Assert.Equal(0, result.ExitCode);
    }

    [SkippableFact]
    public void Fresh_resume_and_new_preserve_copilot_arguments_and_context()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var folder = System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName);
        File.WriteAllText(System.IO.Path.Combine(folder, "instructions.md"), "Project instructions");
        var skills = Directory.CreateDirectory(System.IO.Path.Combine(folder, ".copilot", "skills", "wl-review")).FullName;
        File.WriteAllText(System.IO.Path.Combine(skills, "SKILL.md"), "---\nname: wl-review\ndescription: Review code\n---\nReview this repo.");
        var prompts = Directory.CreateDirectory(System.IO.Path.Combine(folder, "prompts")).FullName;
        File.WriteAllText(System.IO.Path.Combine(prompts, "review.md"), "---\nlabel: Review\n---\nReview this project");
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        var log = System.IO.Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(bin, log);
        var fresh = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--yolo", "-p", "review");
        Assert.Equal(0, fresh.ExitCode);
        var sessionPath = System.IO.Path.Combine(folder, ".last-session");
        var firstSession = File.ReadAllText(sessionPath);
        var args = File.ReadAllText(log);
        Assert.Contains($"--name={firstSession}", args);
        Assert.Contains("--yolo", args);
        Assert.Contains("-i", args);
        Assert.Contains("Review this project", args);
        Assert.Contains("--plugin-dir", args);
        Assert.Contains(System.IO.Path.Combine(folder, ".copilot"), args);
        Assert.Equal(folder, File.ReadAllText(log + ".env").Trim());
        Assert.Contains("Project instructions", File.ReadAllText(System.IO.Path.Combine(folder, "AGENTS.md")));
        Assert.True(File.Exists(System.IO.Path.Combine(folder, ".copilot", "plugin.json")));

        File.Delete(log);
        var resumed = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--resume");
        Assert.Equal(0, resumed.ExitCode);
        Assert.Contains($"--resume={firstSession}", File.ReadAllText(log));
        Assert.DoesNotContain("--name=", File.ReadAllText(log));
        Assert.Equal(firstSession, File.ReadAllText(sessionPath));

        File.Delete(log);
        Assert.Equal(0, WlRunner.Run(home.Path, bin, "launch", "--new").ExitCode);
        Assert.NotEqual(firstSession, File.ReadAllText(sessionPath));
        Assert.Contains("--name=", File.ReadAllText(log));
        Assert.DoesNotContain("--resume=", File.ReadAllText(log));
    }

    [SkippableFact]
    public void Failed_copilot_does_not_replace_saved_pointers()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var pointer = System.IO.Path.Combine(root, home.WorkspaceName, ".last-session");
        File.WriteAllText(pointer, "previous-session");
        File.WriteAllText(System.IO.Path.Combine(root, ".last"), "previous-workspace");
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        FakeCopilot.Install(bin, System.IO.Path.Combine(home.Path, "copilot.log"), exitCode: 42);
        var result = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--new");
        Assert.Equal(42, result.ExitCode);
        Assert.Contains("exited with code 42", result.Stderr);
        Assert.Equal("previous-session", File.ReadAllText(pointer));
        Assert.Equal("previous-workspace", File.ReadAllText(System.IO.Path.Combine(root, ".last")));
    }

    [SkippableFact]
    public void Missing_copilot_fails_without_saving_a_session()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("'copilot' not found", result.Stderr);
        Assert.False(File.Exists(System.IO.Path.Combine(root, home.WorkspaceName, ".last-session")));
        Assert.False(File.Exists(System.IO.Path.Combine(root, ".last")));
        Assert.NotEqual(0, WlRunner.Run(home.Path, null, "setup").ExitCode);
    }

    [SkippableFact]
    public void Legacy_workspace_is_listed_but_cannot_launch_or_mutate_setup()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var folder = Directory.CreateDirectory(System.IO.Path.Combine(root, home.WorkspaceName)).FullName;
        var config = System.IO.Path.Combine(folder, "workspace.json");
        const string legacy = "{\"name\":\"legacy\",\"tool\":\"claude\"}";
        File.WriteAllText(config, legacy);
        var result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("schemaVersion", result.Stderr);
        Assert.Equal(legacy, File.ReadAllText(config));
        Assert.False(Directory.Exists(System.IO.Path.Combine(root, ".shared")));
        var list = WlRunner.Run(home.Path, null, "list");
        Assert.Contains(home.WorkspaceName, list.Stdout);
        Assert.Contains("[incompatible]", list.Stdout);
        Assert.NotEqual(0, WlRunner.Run(home.Path, null, "which", home.WorkspaceName).ExitCode);
    }

    [SkippableFact]
    public void Legacy_defaultTool_prevents_create_without_writing_files()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var root = Directory.CreateDirectory(System.IO.Path.Combine(home.Path, ".wl-workspaces")).FullName;
        var config = System.IO.Path.Combine(root, ".config.json");
        const string legacy = "{\"defaultTool\":\"copilot\"}";
        File.WriteAllText(config, legacy);
        var result = WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("defaultTool", result.Stderr);
        Assert.Equal(legacy, File.ReadAllText(config));
        Assert.Single(Directory.GetFileSystemEntries(root));
    }

    [SkippableFact]
    public void Legacy_skills_and_session_maps_require_manual_upgrade()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var folder = System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName);
        var legacySkills = Directory.CreateDirectory(System.IO.Path.Combine(folder, ".claude", "skills")).FullName;
        var result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Move skills", result.Stderr);
        Assert.False(File.Exists(System.IO.Path.Combine(folder, "AGENTS.md")));
        Directory.Delete(legacySkills);
        var session = System.IO.Path.Combine(folder, ".last-session");
        const string legacy = "{\"copilot\":\"old-name\",\"claude\":\"old-id\"}";
        File.WriteAllText(session, legacy);
        result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName, "--resume");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("plain Copilot session reference", result.Stderr);
        Assert.Equal(legacy, File.ReadAllText(session));
    }

    [SkippableFact]
    public void Explicit_resume_without_session_and_conflicting_flags_fail()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName, "--resume");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("No previous session", result.Stderr);
        result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName, "--resume", "--new");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Cannot use", result.Stderr);
    }

    [SkippableFact]
    public void Removed_tool_flag_is_rejected_and_help_is_copilot_only()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic", "--tool", "copilot");
        Assert.NotEqual(0, result.ExitCode);
        var help = WlRunner.Run(home.Path, null, "create", "--help");
        Assert.Contains("Copilot", help.Stdout);
        Assert.DoesNotContain("Claude", help.Stdout);
        Assert.DoesNotContain("--tool", help.Stdout);
    }

    [SkippableFact]
    public void Create_uses_copilot_skill_and_propagates_failure()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        var log = System.IO.Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(bin, log, exitCode: 7);
        var result = WlRunner.Run(home.Path, bin, "create", "My Workspace");
        Assert.Equal(7, result.ExitCode);
        var args = File.ReadAllText(log);
        Assert.Contains("wl-create-workspace", args);
        Assert.Contains("my-workspace", args);
        Assert.Contains("--plugin-dir", args);
        Assert.Contains("--add-dir", args);
    }

    [SkippableFact]
    public void Which_is_read_only_including_launch_preparation()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var folder = System.IO.Path.Combine(root, home.WorkspaceName);
        File.WriteAllText(System.IO.Path.Combine(folder, "instructions.md"), "Context");
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);
        var result = WlRunner.Run(home.Path, null, "which", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Launch prep:", result.Stdout);
        Assert.Contains("copilot --name=", result.Stdout);
        Assert.False(File.Exists(System.IO.Path.Combine(folder, "AGENTS.md")));
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order());
        foreach (var (path, content) in before) Assert.Equal(content, File.ReadAllText(path));
    }

    private sealed class TempHome : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("wl-e2e-").FullName;
        public string WorkspaceName { get; } = "e2e-" + Guid.NewGuid().ToString("N")[..8];

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }

        }
    }
}