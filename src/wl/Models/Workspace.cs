using System.Text.Json.Serialization;

namespace wl.Models;

public class Workspace
{
    public string Name { get; set; } = "";
    public string PrimaryRepo { get; set; } = "";
    public List<string> AdditionalDirs { get; set; } = [];
    public bool Yolo { get; set; }
    public bool Resume { get; set; }

    [JsonIgnore] public string FolderName => Path.GetFileName(FolderPath);
    [JsonIgnore] public string FolderPath { get; set; } = "";
    [JsonIgnore] public string InstructionsPath => Path.Combine(FolderPath, "instructions.md");
    [JsonIgnore] public string PromptsPath => Path.Combine(FolderPath, "prompts");
    [JsonIgnore] public string SkillsPath => Path.Combine(FolderPath, ".claude", "skills");
}