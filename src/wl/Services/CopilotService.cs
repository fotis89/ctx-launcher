using System.Security.Cryptography;
using System.Text;

using wl.Helpers;
using wl.Models;

namespace wl.Services;

public class CopilotService(WlPaths paths)
{
    public const string SharedPluginName = "wl-shared";
    public const string WorkspacePluginPrefix = "wl-";
    private const int MaxPluginNameLength = 64;
    private const int PluginNameHashLength = 16;

    public void PrepareLaunch(Workspace? ws)
    {
        if (ws is null)
        {
            PrepareSharedPluginManifest();
            return;
        }

        // Each .copilot/ dir is exposed as a local plugin via --plugin-dir;
        // ensure a plugin.json manifest exists. Per-dir try so one failure
        // doesn't block the others.
        foreach (var (copilotDir, name) in GetManagedCopilotDirs(ws))
        {
            if (!HasSkills(copilotDir)) continue;
            try
            {
                EnsurePluginManifest(copilotDir, name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Console.Error.WriteLine($"Warning: could not write {Path.Combine(copilotDir, WlPaths.PluginManifestFileName)} ({ex.GetType().Name}); skills in this directory may not load.");
            }
        }
    }

    private void PrepareSharedPluginManifest()
    {
        if (!HasSkills(paths.SharedCopilotDir)) return;
        try
        {
            EnsurePluginManifest(paths.SharedCopilotDir, SharedPluginName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine($"Warning: could not write {Path.Combine(paths.SharedCopilotDir, WlPaths.PluginManifestFileName)} ({ex.GetType().Name}); shared skills may not load.");
        }
    }

    private IEnumerable<(string CopilotDir, string PluginName)> GetManagedCopilotDirs(Workspace ws)
    {
        // Hash the disk identity so slugification and truncation cannot
        // collapse different workspace names into the same plugin name.
        var identity = string.IsNullOrEmpty(ws.FolderName) ? ws.Name : ws.FolderName;
        var slug = PathHelper.Slugify(ws.FolderName);
        if (string.IsNullOrEmpty(slug)) slug = PathHelper.Slugify(ws.Name);
        if (string.IsNullOrEmpty(slug)) slug = "workspace";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..PluginNameHashLength];
        var maxSlugLength = MaxPluginNameLength - WorkspacePluginPrefix.Length - PluginNameHashLength - 1;
        if (slug.Length > maxSlugLength) slug = slug[..maxSlugLength].TrimEnd('-');
        yield return (ws.CopilotDirPath, $"{WorkspacePluginPrefix}{slug}-{hash}");
        yield return (paths.SharedCopilotDir, SharedPluginName);
    }

    private static bool HasSkills(string copilotDir)
    {
        var skillsDir = Path.Combine(copilotDir, WlPaths.SkillsDirName);
        if (!Directory.Exists(skillsDir))
        {
            return false;
        }
        return Directory.GetDirectories(skillsDir)
            .Any(d => File.Exists(Path.Combine(d, WlPaths.SkillFileName)));
    }

    public static void EnsurePluginManifest(string copilotDir, string pluginName)
    {
        Directory.CreateDirectory(copilotDir);
        var manifestPath = Path.Combine(copilotDir, WlPaths.PluginManifestFileName);
        var content = $$"""
            {
              "name": "{{pluginName}}"
            }
            """;
        File.WriteAllText(manifestPath, content);
    }

    public IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws)
        => GetEnvironment(ws, Environment.GetEnvironmentVariable("COPILOT_CUSTOM_INSTRUCTIONS_DIRS"));

    public IReadOnlyDictionary<string, string> GetEnvironment(string folderPath)
        => GetEnvironment(folderPath, Environment.GetEnvironmentVariable("COPILOT_CUSTOM_INSTRUCTIONS_DIRS"));

    public IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws, string? inheritedInstructionDirs)
        => GetEnvironment(ws.FolderPath, inheritedInstructionDirs);

    public IReadOnlyDictionary<string, string> GetEnvironment(string folderPath, string? inheritedInstructionDirs)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var sharedDirs = File.Exists(WlPaths.Agents(paths.SharedDir)) ? [paths.SharedDir] : Array.Empty<string>();
        var directories = (inheritedInstructionDirs ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(sharedDirs)
            .Append(folderPath)
            .Distinct(comparer);
        return new Dictionary<string, string>
        {
            ["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"] = string.Join(",", directories),
        };
    }

    public IEnumerable<string> DescribeLaunchPrep(Workspace ws)
    {
        foreach (var (copilotDir, name) in GetManagedCopilotDirs(ws))
        {
            if (HasSkills(copilotDir))
            {
                yield return $"writes {Path.Combine(copilotDir, WlPaths.PluginManifestFileName)} (plugin name: {name})";
            }
        }
    }

    public int InvokeCreateSkill(string skillName, string? workspaceName, string cwd, string sharedDir, CopilotRunner runner)
    {
        // Load the shared skill for this invocation without registering
        // global plugins. A named-skill prompt keeps the request explicit.
        var sharedCopilotDir = WlPaths.CopilotDir(sharedDir);
        if (HasSkills(sharedCopilotDir))
        {
            EnsurePluginManifest(sharedCopilotDir, SharedPluginName);
        }
        var detail = workspaceName is null ? "" : $" for workspace '{workspaceName}'";
        var prompt = $"Use the {skillName} skill{detail}.";
        return runner.Run(cwd, ["--add-dir", sharedDir, "--plugin-dir", sharedCopilotDir, "-i", prompt]);
    }

    public LaunchArgs BuildArgs(LaunchSpec spec)
    {
        var args = new List<string>();
        string? newSessionId = null;
        var ws = spec.Workspace;

        if (spec.ResumeSessionId is not null)
        {
            args.Add($"--resume={spec.ResumeSessionId}");
        }
        else
        {
            var slug = spec.SessionNameSlug;
            var id = Guid.NewGuid();
            newSessionId = id.ToString();
            var name = spec.TemporarySession
                ? $"{slug}-temp-{id.ToString("N")[..8]}"
                : $"{slug}-{id.ToString("N")[..8]}";
            // Keep a readable picker label, but persist the immutable ID
            // so /rename cannot invalidate the saved resume pointer.
            args.Add($"--name={name}");
            args.Add($"--session-id={newSessionId}");
        }

        spec.AppendAddDirArgs(args);

        // Manifests are written by PrepareLaunch; emit --plugin-dir for
        // each dir that has skills.
        if (ws is not null)
        {
            foreach (var (copilotDir, _) in GetManagedCopilotDirs(ws))
            {
                if (HasSkills(copilotDir))
                {
                    args.Add("--plugin-dir");
                    args.Add(copilotDir);
                }
            }
        }
        else if (HasSkills(paths.SharedCopilotDir))
        {
            args.Add("--plugin-dir");
            args.Add(paths.SharedCopilotDir);
        }

        args.AddRange(spec.CopilotArgs);
        args.AddRange(spec.PassThroughArgs ?? []);

        return new LaunchArgs(args, newSessionId);
    }
}