using System.Text.Json;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class WorkspaceService
{
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

    public List<Workspace> ListWorkspaces()
    {
        var root = GetWorkspacesRoot();
        var workspaces = new List<Workspace>();

        foreach (var dir in Directory.GetDirectories(root))
        {
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

    public void CreateWorkspace(string slug, string primaryRepo, List<string> additionalDirs, string? displayName = null)
    {
        var root = GetWorkspacesRoot();
        var folderPath = Path.Combine(root, slug);

        Directory.CreateDirectory(folderPath);
        Directory.CreateDirectory(Path.Combine(folderPath, "prompts"));
        Directory.CreateDirectory(Path.Combine(folderPath, ".claude", "skills"));

        var ws = new Workspace
        {
            Name = displayName ?? slug,
            PrimaryRepo = primaryRepo,
            AdditionalDirs = additionalDirs
        };

        var json = JsonSerializer.Serialize(ws, WlJsonContext.Default.Workspace);
        File.WriteAllText(Path.Combine(folderPath, "workspace.json"), json);

        var template =
"""
# Project Context

## What this project does
(describe your project)

## Related directories
(what the additional folders contain and how they relate)

## Guidelines for the AI
(any preferences for how the AI should work in this project)
""";
        File.WriteAllText(Path.Combine(folderPath, "instructions.md"), template);
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