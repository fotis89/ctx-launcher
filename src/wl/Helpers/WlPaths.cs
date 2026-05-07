namespace wl.Helpers;

/// <summary>
/// Single source of truth for the wl folder layout. Owns the workspaces
/// root (~/.wl-workspaces by default) so callers don't have to pass it.
/// Pure path construction — the only IO is a single
/// Directory.CreateDirectory on first root resolution.
///
/// Layout:
///   &lt;WorkspacesRoot&gt;/
///     .version, .last, .last-session, .paths.json, .config.json, .gitignore
///     &lt;name&gt;/                   ← workspace folder
///       workspace.json
///       instructions.md
///       AGENTS.md                 (auto-generated for Copilot)
///       prompts/
///       .claude/
///         plugin.json             (auto-generated for Copilot)
///         skills/&lt;skill&gt;/SKILL.md
///     .shared/                    ← shared dir
///       .claude/
///         plugin.json
///         skills/&lt;skill&gt;/SKILL.md
/// </summary>
public class WlPaths
{
    public const string SharedDirName = ".shared";
    public const string ClaudeDirName = ".claude";
    public const string SkillsDirName = "skills";
    public const string PromptsDirName = "prompts";
    public const string SkillFileName = "SKILL.md";
    public const string PluginManifestFileName = "plugin.json";
    public const string InstructionsFileName = "instructions.md";
    public const string AgentsFileName = "AGENTS.md";
    public const string WorkspaceConfigFileName = "workspace.json";
    public const string PathsConfigFileName = ".paths.json";
    public const string ToolConfigFileName = ".config.json";
    public const string VersionFileName = ".version";
    public const string LastWorkspaceFileName = ".last";
    public const string LastSessionFileName = ".last-session";
    public const string GitignoreFileName = ".gitignore";

    private readonly string _rootPath;
    private string? _resolvedRoot;

    public WlPaths(string? rootPath = null)
    {
        _rootPath = rootPath ?? "~/.wl-workspaces";
    }

    public string WorkspacesRoot
    {
        get
        {
            // wl is a single-process CLI that exits after one command,
            // so this lazy init doesn't need locking.
            if (_resolvedRoot is not null) return _resolvedRoot;
            _resolvedRoot = PathHelper.ResolveTilde(_rootPath);
            Directory.CreateDirectory(_resolvedRoot);
            return _resolvedRoot;
        }
    }

    // Per-workspaces-root paths.
    public string SharedDir => Path.Combine(WorkspacesRoot, SharedDirName);
    public string SharedClaudeDir => ClaudeDir(SharedDir);
    public string SharedSkillsDir => SkillsDir(SharedDir);
    public string PathsConfigFile => Path.Combine(WorkspacesRoot, PathsConfigFileName);
    public string ToolConfigFile => Path.Combine(WorkspacesRoot, ToolConfigFileName);
    public string VersionFile => Path.Combine(WorkspacesRoot, VersionFileName);
    public string LastWorkspaceFile => Path.Combine(WorkspacesRoot, LastWorkspaceFileName);
    public string GitignoreFile => Path.Combine(WorkspacesRoot, GitignoreFileName);

    public string WorkspaceFolder(string name) => Path.Combine(WorkspacesRoot, name);

    // Per-workspace-folder paths (work for any folder, including the shared dir).
    public static string ClaudeDir(string folderPath) => Path.Combine(folderPath, ClaudeDirName);
    public static string SkillsDir(string folderPath) => Path.Combine(ClaudeDir(folderPath), SkillsDirName);
    public static string PluginManifest(string folderPath) => Path.Combine(ClaudeDir(folderPath), PluginManifestFileName);
    public static string Agents(string folderPath) => Path.Combine(folderPath, AgentsFileName);
    public static string Instructions(string folderPath) => Path.Combine(folderPath, InstructionsFileName);
    public static string Prompts(string folderPath) => Path.Combine(folderPath, PromptsDirName);
    public static string WorkspaceConfig(string folderPath) => Path.Combine(folderPath, WorkspaceConfigFileName);
    public static string LastSession(string folderPath) => Path.Combine(folderPath, LastSessionFileName);

    // Per-skills-dir paths.
    public static string Skill(string skillsDir, string skillName) => Path.Combine(skillsDir, skillName);
    public static string SkillFile(string skillsDir, string skillName) => Path.Combine(skillsDir, skillName, SkillFileName);
}
