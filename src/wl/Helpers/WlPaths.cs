namespace wl.Helpers;

/// <summary>
/// Single source of truth for the wl folder layout. Owns the workspaces
/// root (~/.wl-workspaces by default) so callers don't have to pass it.
/// Pure path construction; reading paths never creates directories.
///
/// Layout:
///   &lt;WorkspacesRoot&gt;/
///     .version, .last, .paths.json, .gitignore
///     &lt;name&gt;/                   ← workspace folder
///       workspace.json
///       instructions.md
///       AGENTS.md                 (auto-generated for Copilot)
///       .last-session
///       prompts/
///       .copilot/
///         plugin.json             (auto-generated for Copilot)
///         skills/&lt;skill&gt;/SKILL.md
///     .shared/                    ← shared dir
///       .copilot/
///         plugin.json
///         skills/&lt;skill&gt;/SKILL.md
/// </summary>
public class WlPaths
{
    public const string SharedDirName = ".shared";
    public const string CopilotDirName = ".copilot";
    public const string SkillsDirName = "skills";
    public const string PromptsDirName = "prompts";
    public const string SkillFileName = "SKILL.md";
    public const string PluginManifestFileName = "plugin.json";
    public const string InstructionsFileName = "instructions.md";
    public const string AgentsFileName = "AGENTS.md";
    public const string WorkspaceConfigFileName = "workspace.json";
    public const string PathsConfigFileName = ".paths.json";
    public const string VersionFileName = ".version";
    public const string LastWorkspaceFileName = ".last";
    public const string LastSessionFileName = ".last-session";
    public const string GitignoreFileName = ".gitignore";

    private readonly string _rootPath;

    public WlPaths(string? rootPath = null)
    {
        _rootPath = rootPath ?? Environment.GetEnvironmentVariable("WL_WORKSPACES_ROOT") ?? "~/.wl-workspaces";
    }

    public string WorkspacesRoot => Path.GetFullPath(PathHelper.ResolveTilde(_rootPath));

    // Per-workspaces-root paths.
    public string SharedDir => Path.Combine(WorkspacesRoot, SharedDirName);
    public string SharedCopilotDir => CopilotDir(SharedDir);
    public string SharedSkillsDir => SkillsDir(SharedDir);
    public string PathsConfigFile => Path.Combine(WorkspacesRoot, PathsConfigFileName);
    public string VersionFile => Path.Combine(WorkspacesRoot, VersionFileName);
    public string LastWorkspaceFile => Path.Combine(WorkspacesRoot, LastWorkspaceFileName);
    public string GitignoreFile => Path.Combine(WorkspacesRoot, GitignoreFileName);

    public string WorkspaceFolder(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.') ||
            name.IndexOfAny(['/', '\\']) >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            Path.IsPathRooted(name))
            throw new ArgumentException("Workspace name must be a non-empty folder name, not a path.");
        return Path.Combine(WorkspacesRoot, name);
    }

    // Per-workspace-folder paths (work for any folder, including the shared dir).
    public static string CopilotDir(string folderPath) => Path.Combine(folderPath, CopilotDirName);
    public static string SkillsDir(string folderPath) => Path.Combine(CopilotDir(folderPath), SkillsDirName);
    public static string PluginManifest(string folderPath) => Path.Combine(CopilotDir(folderPath), PluginManifestFileName);
    public static string Agents(string folderPath) => Path.Combine(folderPath, AgentsFileName);
    public static string Instructions(string folderPath) => Path.Combine(folderPath, InstructionsFileName);
    public static string Prompts(string folderPath) => Path.Combine(folderPath, PromptsDirName);
    public static string WorkspaceConfig(string folderPath) => Path.Combine(folderPath, WorkspaceConfigFileName);
    public static string LastSession(string folderPath) => Path.Combine(folderPath, LastSessionFileName);

    // Per-skills-dir paths.
    public static string Skill(string skillsDir, string skillName) => Path.Combine(skillsDir, skillName);
    public static string SkillFile(string skillsDir, string skillName) => Path.Combine(skillsDir, skillName, SkillFileName);
}