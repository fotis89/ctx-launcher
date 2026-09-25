using System.CommandLine;
using System.CommandLine.Completions;
using System.Text.Json;

using wl.Commands;
using wl.Helpers;
using wl.Services;

var paths = new WlPaths();
var workspaceService = new WorkspaceService(paths);
var runner = new CopilotRunner();
var pathsService = new PathsService(paths.PathsConfigFile);
var copilot = new CopilotService(paths);
var launchService = new LaunchService(runner, pathsService, copilot, paths);
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
var newOpt = new Option<bool>("--new", "-n") { Description = "Start a fresh session" };
var tempOpt = new Option<bool>("--temp") { Description = "Start a throwaway session without changing the saved session" };
var launchCmd = new Command("launch", "Launch a workspace") { launchNameArg, newOpt, tempOpt };
launchCmd.SetAction(parseResult =>
{
    var name = parseResult.GetValue(launchNameArg);
    var forceNew = parseResult.GetValue(newOpt);
    var temporary = parseResult.GetValue(tempOpt);
    return Run(() => new LaunchCommand(workspaceService, launchService, setupService).Execute(name, forceNew, temporary, passThroughArgs));
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

// which
var whichNameArg = new Argument<string?>("name") { DefaultValueFactory = _ => null, Description = "Workspace name" };
whichNameArg.CompletionSources.Add(WorkspaceCompletions);
var whichNewOpt = new Option<bool>("--new", "-n") { Description = "Show a fresh-session launch" };
var whichTempOpt = new Option<bool>("--temp") { Description = "Show a throwaway-session launch" };
var whichCmd = new Command("which", "Show launch command and validate paths") { whichNameArg, whichNewOpt, whichTempOpt };
whichCmd.SetAction(parseResult =>
{
    return Run(() => new WhichCommand(workspaceService, launchService, pathsService, copilot).Execute(
        parseResult.GetValue(whichNameArg),
        parseResult.GetValue(whichNewOpt),
        parseResult.GetValue(whichTempOpt),
        passThroughArgs));
});

// clone
var cloneUrlArg = new Argument<string>("git-url") { Description = "Git URL to clone" };
var cloneCmd = new Command("clone", "Clone a workspaces repo into ~/.wl-workspaces and initialize path variables") { cloneUrlArg };
cloneCmd.SetAction(parseResult =>
{
    return Run(() => new CloneCommand(workspaceService, pathsService, setupService).Execute(
        parseResult.GetValue(cloneUrlArg)!));
});

root.Add(launchCmd);
root.Add(createCmd);
root.Add(whichCmd);
root.Add(cloneCmd);

return await root.Parse(parseArgs).InvokeAsync();