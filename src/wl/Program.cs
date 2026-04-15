using System.CommandLine;
using System.CommandLine.Completions;

using wl.Commands;
using wl.Services;

var workspaceService = new WorkspaceService();
var promptService = new PromptService();
var launchService = new LaunchService();

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
var launchCmd = new Command("launch", "Launch a workspace") { launchNameArg, promptOpt, yoloOpt, resumeOpt };
launchCmd.SetAction(parseResult =>
{
    var name = parseResult.GetValue(launchNameArg);
    var prompt = parseResult.GetValue(promptOpt);
    var yolo = parseResult.GetValue(yoloOpt);
    var resume = parseResult.GetValue(resumeOpt);
    new LaunchCommand(workspaceService, promptService, launchService).Execute(name, prompt, yolo, resume);
});

// create
var createNameArg = new Argument<string>("name") { Description = "Workspace slug" };
var createCmd = new Command("create", "Create a new workspace") { createNameArg };
createCmd.SetAction(parseResult =>
{
    new CreateCommand(workspaceService).Execute(parseResult.GetValue(createNameArg)!);
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
    new WhichCommand(workspaceService, promptService, launchService).Execute(parseResult.GetValue(whichNameArg)!);
});

// setup
var setupCmd = new Command("setup", "Install tab completion for your shell");
setupCmd.SetAction(_ => new SetupCommand().Execute());

root.Add(launchCmd);
root.Add(createCmd);
root.Add(listCmd);
root.Add(editCmd);
root.Add(whichCmd);
root.Add(setupCmd);

return await root.Parse(args).InvokeAsync();