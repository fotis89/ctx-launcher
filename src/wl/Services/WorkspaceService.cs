using System.Text.Json;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class WorkspaceService(WlPaths paths)
{
    public const string SharedDirName = WlPaths.SharedDirName;

    public string GetWorkspacesRoot() => paths.WorkspacesRoot;

    public string GetSharedDirPath() => paths.SharedDir;

    public string? GetSharedDirIfExists()
        => Directory.Exists(paths.SharedDir) ? paths.SharedDir : null;

    public string GetSharedClaudeDirPath() => paths.SharedClaudeDir;

    public string GetSharedSkillsPath() => paths.SharedSkillsDir;

    public string EnsureSharedDir()
    {
        Directory.CreateDirectory(paths.SharedSkillsDir);
        return paths.SharedDir;
    }

    public static List<string> ListSkillNames(string skillsDir)
    {
        if (!Directory.Exists(skillsDir))
            return [];
        return Directory.GetDirectories(skillsDir)
            .Select(d => Path.GetFileName(d)!)
            .ToList();
    }

    public List<Workspace> ListWorkspaces()
    {
        var root = paths.WorkspacesRoot;
        var workspaces = new List<Workspace>();

        foreach (var dir in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(dir) == WlPaths.SharedDirName)
                continue;

            var jsonPath = WlPaths.WorkspaceConfig(dir);
            if (!File.Exists(jsonPath))
            {
                continue;
            }

            var ws = LoadWorkspaceFromPath(dir, jsonPath);
            if (ws is not null)
            {
                workspaces.Add(ws);
            }
        }

        return workspaces.OrderBy(w => w.Name).ToList();
    }

    public void SaveWorkspace(Workspace ws, string slug)
    {
        var folderPath = paths.WorkspaceFolder(slug);
        Directory.CreateDirectory(folderPath);
        var jsonPath = WlPaths.WorkspaceConfig(folderPath);
        var json = JsonSerializer.Serialize(ws, WlJsonContext.Default.Workspace);
        File.WriteAllText(jsonPath, json);
        ws.FolderPath = folderPath;
    }

    public Workspace? LoadWorkspace(string name)
    {
        var folderPath = paths.WorkspaceFolder(name);

        if (!Directory.Exists(folderPath))
        {
            return null;
        }

        var jsonPath = WlPaths.WorkspaceConfig(folderPath);
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        return LoadWorkspaceFromPath(folderPath, jsonPath);
    }

    public string? GetLastUsed()
    {
        if (!File.Exists(paths.LastWorkspaceFile))
        {
            return null;
        }

        var name = File.ReadAllText(paths.LastWorkspaceFile).Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    public void SetLastUsed(string name)
    {
        File.WriteAllText(paths.LastWorkspaceFile, name);
    }

    private static Workspace? LoadWorkspaceFromPath(string folderPath, string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var ws = JsonSerializer.Deserialize(json, WlJsonContext.Default.Workspace);
            if (ws is null)
            {
                return null;
            }

            ws.FolderPath = folderPath;
            return ws;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}