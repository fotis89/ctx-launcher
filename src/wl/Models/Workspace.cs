using System.Text.Json.Serialization;

using wl.Helpers;

namespace wl.Models;

public class Workspace
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "";
    public string PrimaryRepo { get; set; } = "";
    public List<string> AdditionalDirs { get; set; } = [];
    public bool Yolo { get; set; }
    public bool Resume { get; set; }
    public string? Tool { get; set; }

    [JsonIgnore] public string FolderName => Path.GetFileName(FolderPath);
    [JsonIgnore] public string FolderPath { get; set; } = "";
    [JsonIgnore] public string InstructionsPath => WlPaths.Instructions(FolderPath);
    [JsonIgnore] public string AgentsPath => WlPaths.Agents(FolderPath);
    [JsonIgnore] public string PromptsPath => WlPaths.Prompts(FolderPath);
    [JsonIgnore] public string ClaudeDirPath => WlPaths.ClaudeDir(FolderPath);
    [JsonIgnore] public string SkillsPath => WlPaths.SkillsDir(FolderPath);
    [JsonIgnore] public string LastSessionPath => WlPaths.LastSession(FolderPath);
}