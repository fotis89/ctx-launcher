using System.Diagnostics;

using wl.Services;

namespace wl.Commands;

public class CloneCommand(WorkspaceService workspaces, PathsService paths, SetupService setup)
{
    public void Execute(string gitUrl)
    {
        var destination = workspaces.GetWorkspacesRoot();

        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            Console.Error.WriteLine($"Workspaces directory is not empty: {destination}");
            Console.Error.WriteLine("Remove contents before cloning, or clone manually and run 'wl setup'.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"  Cloning {gitUrl} into {destination}...");

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("clone");
        psi.ArgumentList.Add(gitUrl);
        psi.ArgumentList.Add(destination);

        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
            if (process is null || process.ExitCode != 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"  git clone failed (exit {(process?.ExitCode ?? -1)}).");
                return;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Error: 'git' not found. Install Git and ensure it's on your PATH.");
            return;
        }

        setup.RunSetup();
        new PathsCommand(workspaces, paths).Init();
    }
}
