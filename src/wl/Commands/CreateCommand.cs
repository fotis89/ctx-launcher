using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.Commands;

public class CreateCommand(WorkspaceService workspaces, ClaudeRunner claudeRunner, SetupService setup, ToolAdapterRegistry adapters)
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
                WriteBasicWorkspace(slug);
                return;
            }
        }

        var prompt = name is not null
            ? $"/wl-create-workspace {name}"
            : "/wl-create-workspace";

        var resolvedTool = tool ?? DetectAvailableTool();
        var adapter = adapters.Resolve(resolvedTool);
        adapter.InvokeCreateSkill(prompt, Directory.GetCurrentDirectory(), claudeRunner);
    }

    private string DetectAvailableTool()
    {
        if (claudeRunner.TryGetVersion("claude", out _)) return "claude";
        if (claudeRunner.TryGetVersion("copilot", out _)) return "copilot";
        return "claude"; // fall through; ClaudeRunner.Run will print a clear "not found" error
    }

    private void WriteBasicWorkspace(string slug)
    {
        var ws = new Workspace
        {
            Name = slug,
            PrimaryRepo = Directory.GetCurrentDirectory(),
            AdditionalDirs = [],
            Yolo = false,
            Resume = true,
        };
        workspaces.SaveWorkspace(ws, slug);
        Console.WriteLine($"Created workspace '{slug}' at {ws.FolderPath}");
    }
}
