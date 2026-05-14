using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.Commands;

public class CreateCommand(WorkspaceService workspaces, ClaudeRunner claudeRunner, SetupService setup, ToolAdapterRegistry adapters, ConfigService config)
{
    public void Execute(string? name, bool basic = false, string? tool = null)
    {
        setup.EnsureInstalled();

        if (basic && name is null)
        {
            Console.Error.WriteLine("Name required with --basic.");
            return;
        }

        string? slug = null;
        if (name is not null)
        {
            slug = PathHelper.Slugify(name);
            if (slug == WorkspaceService.SharedDirName.TrimStart('.'))
            {
                Console.Error.WriteLine("'shared' is reserved. Choose a different name.");
                return;
            }

            if (workspaces.LoadWorkspace(slug) is not null)
            {
                Console.Error.WriteLine($"Workspace '{slug}' already exists.");
                return;
            }

            if (basic)
            {
                WriteBasicWorkspace(slug, tool);
                return;
            }
        }

        var resolvedTool = tool ?? DetectAvailableTool();
        if (!adapters.TryResolve(resolvedTool, out var adapter))
        {
            Console.Error.WriteLine(
                $"Error: unknown tool '{resolvedTool}'. " +
                $"Available: {string.Join(", ", adapters.Names)}.");
            return;
        }
        // Pass the validated slug, not the raw name, so the skill works
        // with the identifier we just checked is available and unreserved.
        adapter.InvokeCreateSkill("wl-create-workspace", slug, Directory.GetCurrentDirectory(), workspaces.GetSharedDirPath(), claudeRunner);
    }

    private string DetectAvailableTool()
    {
        // Respect the configured default if it's on PATH; otherwise
        // probe claude → copilot so a defaultTool pointing at a missing
        // CLI doesn't fail with "tool not found". Normalize casing for
        // case-sensitive Linux/macOS filesystems.
        var configDefault = config.DefaultTool?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(configDefault) && claudeRunner.TryGetVersion(configDefault, out _))
        {
            return configDefault;
        }
        if (claudeRunner.TryGetVersion("claude", out _)) return "claude";
        if (claudeRunner.TryGetVersion("copilot", out _)) return "copilot";
        return "claude"; // fall through; ClaudeRunner.Run will print a clear "not found" error
    }

    private void WriteBasicWorkspace(string slug, string? tool)
    {
        // Omit Tool when it equals the documented default ("claude") so
        // generated workspace.json stays minimal and matches the README.
        var normalizedTool = string.Equals(tool, "claude", StringComparison.OrdinalIgnoreCase) ? null : tool;
        var ws = new Workspace
        {
            Name = slug,
            PrimaryRepo = Directory.GetCurrentDirectory(),
            AdditionalDirs = [],
            Yolo = false,
            Resume = true,
            Tool = normalizedTool,
        };
        workspaces.SaveWorkspace(ws, slug);
        Console.WriteLine($"Created workspace '{slug}' at {ws.FolderPath}");
    }
}
