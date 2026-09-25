using System.Text.Json;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public record WorkspaceEntry(string FolderName, Workspace? Workspace, string? Error);

public class WorkspaceService(WlPaths paths)
{
    public const string SharedDirName = WlPaths.SharedDirName;

    public string GetWorkspacesRoot() => paths.WorkspacesRoot;

    public string GetSharedDirPath() => paths.SharedDir;

    public string? GetSharedDirIfExists()
        => Directory.Exists(paths.SharedDir) ? paths.SharedDir : null;

    public string GetSharedCopilotDirPath() => paths.SharedCopilotDir;

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
        var workspaces = new List<Workspace>();
        foreach (var entry in ListEntries())
        {
            if (entry.Workspace is not null)
                workspaces.Add(entry.Workspace);
            else
                Console.Error.WriteLine($"Warning: {entry.Error}");
        }
        return workspaces.OrderBy(w => w.Name).ToList();
    }

    public List<WorkspaceEntry> ListEntries()
    {
        var root = paths.WorkspacesRoot;
        var workspaces = new List<WorkspaceEntry>();
        if (!Directory.Exists(root)) return workspaces;

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

            try
            {
                workspaces.Add(new WorkspaceEntry(Path.GetFileName(dir), LoadWorkspaceFromPath(dir, jsonPath), null));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
            {
                workspaces.Add(new WorkspaceEntry(Path.GetFileName(dir), null, ex.Message));
            }
        }

        return workspaces.OrderBy(w => w.FolderName).ToList();
    }

    public string GetWorkspaceFolder(string name) => paths.WorkspaceFolder(name);

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

    private static Workspace LoadWorkspaceFromPath(string folderPath, string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out var version) || version != Workspace.CurrentSchemaVersion)
                throw new InvalidDataException($"{jsonPath}: requires \"schemaVersion\": 2.");

            var ws = JsonSerializer.Deserialize(json, WlJsonContext.Default.Workspace)
                ?? throw new InvalidDataException($"{jsonPath}: expected a workspace object.");
            if (string.IsNullOrWhiteSpace(ws.Name) || string.IsNullOrWhiteSpace(ws.PrimaryRepo) ||
                ws.AdditionalDirs is null || ws.AdditionalDirs.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"{jsonPath}: name and primaryRepo must be non-empty strings; additionalDirs must be an array of non-empty paths.");
            if (ws.CopilotArgs is null || ws.CopilotArgs.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"{jsonPath}: copilotArgs must be an array of non-empty strings.");

            ws.FolderPath = folderPath;
            return ws;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{jsonPath}: invalid workspace JSON ({ex.Message}).", ex);
        }
    }
}