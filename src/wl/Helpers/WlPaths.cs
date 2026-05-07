namespace wl.Helpers;

/// <summary>
/// Single source of truth for the wl folder layout. Pure functions over
/// path strings — no IO, no state, no instantiation.
///
/// Layout reminder:
///   ~/.wl-workspaces/                 ← workspaces root
///     <name>/                         ← workspace folder
///       instructions.md
///       AGENTS.md                     (auto-generated for Copilot)
///       prompts/
///       .claude/
///         plugin.json                 (auto-generated for Copilot)
///         skills/<skill>/SKILL.md
///     .shared/                        ← shared dir
///       .claude/
///         plugin.json
///         skills/<skill>/SKILL.md
/// </summary>
public static class WlPaths
{
    public const string SharedDirName = ".shared";
    public const string ClaudeDirName = ".claude";
    public const string SkillsDirName = "skills";
    public const string PromptsDirName = "prompts";
    public const string SkillFileName = "SKILL.md";
    public const string PluginManifestFileName = "plugin.json";
    public const string InstructionsFileName = "instructions.md";
    public const string AgentsFileName = "AGENTS.md";

    // Per-workspace-folder (or per-shared-dir) paths.
    public static string ClaudeDir(string folderPath) => Path.Combine(folderPath, ClaudeDirName);
    public static string SkillsDir(string folderPath) => Path.Combine(ClaudeDir(folderPath), SkillsDirName);
    public static string PluginManifest(string folderPath) => Path.Combine(ClaudeDir(folderPath), PluginManifestFileName);
    public static string Agents(string folderPath) => Path.Combine(folderPath, AgentsFileName);
    public static string Instructions(string folderPath) => Path.Combine(folderPath, InstructionsFileName);
    public static string Prompts(string folderPath) => Path.Combine(folderPath, PromptsDirName);

    // Per-workspaces-root paths.
    public static string SharedDir(string workspacesRoot) => Path.Combine(workspacesRoot, SharedDirName);
}
