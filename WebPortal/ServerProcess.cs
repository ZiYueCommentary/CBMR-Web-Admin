using System.ComponentModel;
using System.Diagnostics;

namespace CbmrWebAdmin.WebPortal;

internal static class ServerProcess
{
    public static bool MatchesPath(Process process, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        try
        {
            string? processPath = process.MainModule?.FileName;
            return processPath is not null && string.Equals(
                Path.GetFullPath(processPath).Trim(),
                Path.GetFullPath(targetPath).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException
            or NotSupportedException or UnauthorizedAccessException or ArgumentException or IOException)
        {
            return false;
        }
    }

    public static bool IsRunning(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException
            or UnauthorizedAccessException)
        {
            return false;
        }

        try
        {
            return processes.Any(process => MatchesPath(process, targetPath));
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }
}
