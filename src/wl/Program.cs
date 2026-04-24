using System.CommandLine;
using System.CommandLine.Completions;

using wl.Commands;
using wl.Services;

var workspaceService = new WorkspaceService();
var promptService = new PromptService();
var claudeRunner = new ClaudeRunner();
var pathsService = new PathsService(Path.Combine(workspaceService.GetWorkspacesRoot(), ".paths.json"));
var launchService = new LaunchService(claudeRunner, pathsService);
var versionService = new VersionService(workspaceService);
var setupService = new SetupService(workspaceService, versionService);

IEnumerable<CompletionItem> WorkspaceCompletions(CompletionContext _) =>
    workspaceService.ListWorkspaces().Select(ws => new CompletionItem(ws.FolderName));

var root = new RootCommand("wl — AI context launcher for Claude Code");

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

    var ws = workspaceService.LoadWorkspace(wsName);
    if (ws is null)
    {
        return [];
    }

    return promptService.ListPrompts(ws).Select(p => new CompletionItem(p.Slug));
});
var yoloOpt = new Option<bool>("--yolo") { Description = "Skip Claude permission prompts" };
var resumeOpt = new Option<bool>("--resume", "-r") { Description = "Resume the previous session for this workspace" };
var newOpt = new Option<bool>("--new", "-n") { Description = "Start a fresh session (overrides resume: true)" };
var launchCmd = new Command("launch", "Launch a workspace") { launchNameArg, promptOpt, yoloOpt, resumeOpt, newOpt };
launchCmd.SetAction(parseResult =>
{
    var name = parseResult.GetValue(launchNameArg);
    var prompt = parseResult.GetValue(promptOpt);
    var yolo = parseResult.GetValue(yoloOpt);
    var resume = parseResult.GetValue(resumeOpt);
    var forceNew = parseResult.GetValue(newOpt);
    new LaunchCommand(workspaceService, promptService, launchService, setupService, pathsService).Execute(name, prompt, yolo, resume, forceNew);
});

// create
var createNameArg = new Argument<string?>("name") { DefaultValueFactory = _ => null, Description = "Workspace slug (optional — Claude will propose one)" };
var basicOpt = new Option<bool>("--basic") { Description = "Write a minimal workspace.json without invoking Claude" };
var createCmd = new Command("create", "Create a new workspace (via Claude, or --basic for a minimal scaffold)") { createNameArg, basicOpt };
createCmd.SetAction(parseResult =>
{
    new CreateCommand(workspaceService, claudeRunner, setupService).Execute(
        parseResult.GetValue(createNameArg),
        parseResult.GetValue(basicOpt));
});

// list
var listCmd = new Command("list", "List all workspaces");
listCmd.SetAction(_ => new ListCommand(workspaceService).Execute());

// edit
var editNameArg = new Argument<string>("name") { Description = "Workspace name" };
editNameArg.CompletionSources.Add(WorkspaceCompletions);
var editCmd = new Command("edit", "Open workspace folder in file explorer") { editNameArg };
editCmd.SetAction(parseResult =>
{
    new EditCommand(workspaceService).Execute(parseResult.GetValue(editNameArg)!);
});

// which
var whichNameArg = new Argument<string>("name") { Description = "Workspace name" };
whichNameArg.CompletionSources.Add(WorkspaceCompletions);
var whichCmd = new Command("which", "Show launch command and validate paths") { whichNameArg };
whichCmd.SetAction(parseResult =>
{
    new WhichCommand(workspaceService, promptService, launchService, pathsService).Execute(parseResult.GetValue(whichNameArg)!);
});

// setup
var setupCmd = new Command("setup", "Install Claude skills and show tab completion setup");
setupCmd.SetAction(_ => new SetupCommand(setupService, claudeRunner).Execute());

// paths (group)
var pathsCmd = new Command("paths", "Manage path variables used in workspace.json");

var pathsSetNameArg = new Argument<string>("name") { Description = "Variable name (e.g. REPOS_ROOT)" };
var pathsSetValueArg = new Argument<string>("value") { Description = "Value to assign" };
var pathsSetCmd = new Command("set", "Set a path variable") { pathsSetNameArg, pathsSetValueArg };
pathsSetCmd.SetAction(parseResult =>
    new PathsCommand(workspaceService, pathsService).Set(
        parseResult.GetValue(pathsSetNameArg)!,
        parseResult.GetValue(pathsSetValueArg)!) ? 0 : 1);

var pathsListCmd = new Command("list", "List defined and referenced path variables");
pathsListCmd.SetAction(_ => new PathsCommand(workspaceService, pathsService).List());

var pathsInitCmd = new Command("init", "Prompt for any path variables referenced but not defined");
pathsInitCmd.SetAction(_ => new PathsCommand(workspaceService, pathsService).Init());

pathsCmd.Subcommands.Add(pathsSetCmd);
pathsCmd.Subcommands.Add(pathsListCmd);
pathsCmd.Subcommands.Add(pathsInitCmd);

// clone
var cloneUrlArg = new Argument<string>("git-url") { Description = "Git URL to clone" };
var cloneCmd = new Command("clone", "Clone a workspaces repo into ~/.wl-workspaces and run setup + paths init") { cloneUrlArg };
cloneCmd.SetAction(parseResult =>
{
    new CloneCommand(workspaceService, pathsService, setupService).Execute(
        parseResult.GetValue(cloneUrlArg)!);
});

root.Add(launchCmd);
root.Add(createCmd);
root.Add(listCmd);
root.Add(editCmd);
root.Add(whichCmd);
root.Add(setupCmd);
root.Add(pathsCmd);
root.Add(cloneCmd);

return await root.Parse(args).InvokeAsync();