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

        if (name is not null)
        {
            var slug = PathHelper.Slugify(name);
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
        adapter.InvokeCreateSkill("wl-create-workspace", name, Directory.GetCurrentDirectory(), workspaces.GetSharedDirPath(), claudeRunner);
    }

    private string DetectAvailableTool()
    {
        // Respect the machine-local default if it points at a CLI that's
        // actually on PATH. If the user set defaultTool=copilot but only
        // claude is installed (or vice versa), fall through to the probe
        // chain rather than fail with "tool not found".
        var configDefault = config.DefaultTool;
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
        // generated workspace.json matches the README convention. Users
        // who explicitly want to pin claude can edit workspace.json by
        // hand; --tool claude on the command line means "use the default".
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
