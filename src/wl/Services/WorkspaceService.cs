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

        // EnumerateDirectories streams so a permission-denied entry can
        // be skipped without aborting `wl list` entirely.
        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine($"Warning: cannot enumerate {root} ({ex.GetType().Name}); returning no workspaces.");
            return workspaces;
        }

        using var enumerator = dirs.GetEnumerator();
        while (true)
        {
            string dir;
            try
            {
                if (!enumerator.MoveNext()) break;
                dir = enumerator.Current;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Console.Error.WriteLine($"Warning: enumeration of {root} hit {ex.GetType().Name}; some workspaces may not be listed.");
                break;
            }

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

        // Atomic write: tmp + Move so a process killed mid-write can't
        // leave workspace.json half-written and unparseable.
        var tmp = jsonPath + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, jsonPath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* swallowed */ }
            throw;
        }
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

        try
        {
            var name = File.ReadAllText(paths.LastWorkspaceFile).Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Read failure on the last-used pointer is non-fatal.
            Console.Error.WriteLine($"Warning: cannot read {paths.LastWorkspaceFile} ({ex.GetType().Name}); ignoring last-used pointer.");
            return null;
        }
    }

    public void SetLastUsed(string name)
    {
        try
        {
            File.WriteAllText(paths.LastWorkspaceFile, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine($"Warning: cannot update {paths.LastWorkspaceFile} ({ex.GetType().Name}); next bare `wl launch` may not find this workspace.");
        }
    }

    private static Workspace? LoadWorkspaceFromPath(string folderPath, string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var ws = JsonSerializer.Deserialize(json, WlJsonContext.Default.Workspace);
            if (ws is null)
            {
                Console.Error.WriteLine($"Warning: {jsonPath} deserialized to null; ignoring workspace.");
                return null;
            }

            ws.FolderPath = folderPath;
            return ws;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Warning: {jsonPath} is not valid JSON ({ex.Message}); ignoring workspace.");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Locked / permission denied — ignore this workspace rather
            // than fail every wl command that enumerates workspaces.
            Console.Error.WriteLine($"Warning: cannot read {jsonPath} ({ex.GetType().Name}); ignoring workspace.");
            return null;
        }
    }
}