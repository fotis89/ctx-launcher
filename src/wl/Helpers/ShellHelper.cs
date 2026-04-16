using System.Diagnostics;

namespace wl.Helpers;

public static class ShellHelper
{
    public static void OpenFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", path);
            }
            else
            {
                Process.Start("xdg-open", path);
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.WriteLine($"  Open: {path}");
        }
    }
}
