using System.Text.Json;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class WorkspaceService
{
    public const string SharedDirName = ".shared";

    private string? _root;

    public string GetWorkspacesRoot()
    {
        if (_root is not null)
        {
            return _root;
        }

        _root = PathHelper.ResolveTilde("~/.wl-workspaces");
        Directory.CreateDirectory(_root);
        return _root;
    }

    public string GetSharedDirPath()
        => Path.Combine(GetWorkspacesRoot(), SharedDirName);

    public string? GetSharedDirIfExists()
    {
        var path = GetSharedDirPath();
        return Directory.Exists(path) ? path : null;
    }

    public string GetSharedSkillsPath()
        => Path.Combine(GetSharedDirPath(), ".claude", "skills");

    public string EnsureSharedDir()
    {
        var path = GetSharedDirPath();
        Directory.CreateDirectory(Path.Combine(path, ".claude", "skills"));
        return path;
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
        var root = GetWorkspacesRoot();
        var workspaces = new List<Workspace>();

        foreach (var dir in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(dir) == SharedDirName)
                continue;

            var jsonPath = Path.Combine(dir, "workspace.json");
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

    public Workspace? LoadWorkspace(string name)
    {
        var root = GetWorkspacesRoot();
        var folderPath = Path.Combine(root, name);

        if (!Directory.Exists(folderPath))
        {
            return null;
        }

        var jsonPath = Path.Combine(folderPath, "workspace.json");
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        return LoadWorkspaceFromPath(folderPath, jsonPath);
    }

    public string? GetLastUsed()
    {
        var lastFile = Path.Combine(GetWorkspacesRoot(), ".last");
        if (!File.Exists(lastFile))
        {
            return null;
        }

        var name = File.ReadAllText(lastFile).Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    public void SetLastUsed(string name)
    {
        var lastFile = Path.Combine(GetWorkspacesRoot(), ".last");
        File.WriteAllText(lastFile, name);
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