using System.CommandLine;
using System.CommandLine.Completions;
using System.Text.Json;

using wl.Commands;
using wl.Helpers;
using wl.Services;

var paths = new WlPaths();
var workspaceService = new WorkspaceService(paths);
var promptService = new PromptService();
var runner = new CopilotRunner();
var pathsService = new PathsService(paths.PathsConfigFile);
var copilot = new CopilotService(paths);
var launchService = new LaunchService(runner, pathsService, copilot);
var versionService = new VersionService(paths);
var setupService = new SetupService(versionService, paths);
var (parseArgs, passThroughArgs) = SplitPassThrough(args);

static int Run(Func<int> action)
{
    try
    {
        return action();
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
        System.Security.SecurityException or JsonException or ArgumentException)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static (string[] ParseArgs, List<string> PassThroughArgs) SplitPassThrough(string[] rawArgs)
{
    if (rawArgs.Any(arg => arg.StartsWith("[suggest:", StringComparison.Ordinal)))
    {
        return (rawArgs, []);
    }

    if (rawArgs.Length == 0 || rawArgs[0] is not ("launch" or "which"))
    {
        return (rawArgs, []);
    }

    var separator = Array.IndexOf(rawArgs, "--");
    if (separator < 0)
    {
        return (rawArgs, []);
    }

    return (rawArgs[..separator], rawArgs[(separator + 1)..].ToList());
}

IEnumerable<CompletionItem> WorkspaceCompletions(CompletionContext _) =>
    workspaceService.ListEntries().Select(ws => new CompletionItem(ws.FolderName));

var root = new RootCommand("wl — GitHub Copilot workspace launcher");

// launch
var launchNameArg = new Argument<string?>("name") { DefaultValueFactory = _ => null, Description = "Workspace name" };
launchNameArg.CompletionSources.Add(WorkspaceCompletions);
var promptOpt = new Option<string?>("-p") { Description = "Saved prompt slug or raw text" };
promptOpt.CompletionSources.Add(ctx =>
{
    var wsName = ctx.ParseResult.GetValue(launchNameArg);
    if (wsName is null)
    {
        return [];
    }

    var ws = workspaceService.ListEntries().FirstOrDefault(w => w.FolderName == wsName)?.Workspace;
    if (ws is null)
    {
        return [];
    }

    return promptService.ListPrompts(ws).Select(p => new CompletionItem(p.Slug));
});
var newOpt = new Option<bool>("--new", "-n") { Description = "Start a fresh session" };
var tempOpt = new Option<bool>("--temp") { Description = "Start a throwaway session without changing the saved session" };
var launchCmd = new Command("launch", "Launch a workspace") { launchNameArg, promptOpt, newOpt, tempOpt };
launchCmd.SetAction(parseResult =>
{
    var name = parseResult.GetValue(launchNameArg);
    var prompt = parseResult.GetValue(promptOpt);
    var forceNew = parseResult.GetValue(newOpt);
    var temporary = parseResult.GetValue(tempOpt);
    return Run(() => new LaunchCommand(workspaceService, promptService, launchService, setupService).Execute(name, prompt, forceNew, temporary, passThroughArgs));
});

// create
var createNameArg = new Argument<string?>("name") { DefaultValueFactory = _ => null, Description = "Workspace slug (optional — Copilot will propose one)" };
var basicOpt = new Option<bool>("--basic") { Description = "Write a minimal workspace.json without invoking an AI CLI" };
var createCmd = new Command("create", "Create a new workspace (via Copilot, or --basic for a minimal scaffold)") { createNameArg, basicOpt };
createCmd.SetAction(parseResult =>
{
    return Run(() => new CreateCommand(workspaceService, runner, setupService, copilot).Execute(
        parseResult.GetValue(createNameArg),
        parseResult.GetValue(basicOpt)));
});

// list
var listCmd = new Command("list", "List all workspaces");
listCmd.SetAction(_ => Run(() => { new ListCommand(workspaceService).Execute(); return 0; }));

// edit
var editNameArg = new Argument<string>("name") { Description = "Workspace name" };
editNameArg.CompletionSources.Add(WorkspaceCompletions);
var editCmd = new Command("edit", "Open workspace folder in file explorer") { editNameArg };
editCmd.SetAction(parseResult =>
{
    return Run(() => new EditCommand(workspaceService).Execute(parseResult.GetValue(editNameArg)!));
});

// which
var whichNameArg = new Argument<string>("name") { Description = "Workspace name" };
whichNameArg.CompletionSources.Add(WorkspaceCompletions);
var whichCmd = new Command("which", "Show launch command and validate paths") { whichNameArg };
whichCmd.SetAction(parseResult =>
{
    return Run(() => new WhichCommand(workspaceService, promptService, launchService, pathsService, copilot).Execute(parseResult.GetValue(whichNameArg)!, passThroughArgs));
});

// setup
var setupCmd = new Command("setup", "Install Copilot skills and show tab completion setup");
setupCmd.SetAction(_ => Run(() => new SetupCommand(setupService, runner).Execute()));

// paths (group)
var pathsCmd = new Command("paths", "Manage path variables used in workspace.json");

var pathsSetNameArg = new Argument<string>("name") { Description = "Variable name (e.g. REPOS_ROOT)" };
var pathsSetValueArg = new Argument<string>("value") { Description = "Value to assign" };
var pathsSetCmd = new Command("set", "Set a path variable") { pathsSetNameArg, pathsSetValueArg };
pathsSetCmd.SetAction(parseResult => Run(() =>
    new PathsCommand(workspaceService, pathsService).Set(
        parseResult.GetValue(pathsSetNameArg)!,
        parseResult.GetValue(pathsSetValueArg)!) ? 0 : 1));

var pathsListCmd = new Command("list", "List defined and referenced path variables");
pathsListCmd.SetAction(_ => Run(() => { new PathsCommand(workspaceService, pathsService).List(); return 0; }));

var pathsInitCmd = new Command("init", "Prompt for any path variables referenced but not defined");
pathsInitCmd.SetAction(_ => Run(() => { new PathsCommand(workspaceService, pathsService).Init(); return 0; }));

pathsCmd.Subcommands.Add(pathsSetCmd);
pathsCmd.Subcommands.Add(pathsListCmd);
pathsCmd.Subcommands.Add(pathsInitCmd);

// clone
var cloneUrlArg = new Argument<string>("git-url") { Description = "Git URL to clone" };
var cloneCmd = new Command("clone", "Clone a workspaces repo into ~/.wl-workspaces and run setup + paths init") { cloneUrlArg };
cloneCmd.SetAction(parseResult =>
{
    return Run(() => new CloneCommand(workspaceService, pathsService, setupService).Execute(
        parseResult.GetValue(cloneUrlArg)!));
});

root.Add(launchCmd);
root.Add(createCmd);
root.Add(listCmd);
root.Add(editCmd);
root.Add(whichCmd);
root.Add(setupCmd);
root.Add(pathsCmd);
root.Add(cloneCmd);

return await root.Parse(parseArgs).InvokeAsync();