using System.ComponentModel;
using System.Diagnostics;

namespace CbmrWebAdmin.WebPortal;

internal static class ServerProcess
{
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, int> ProcessIds = new(StringComparer.OrdinalIgnoreCase);

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

        lock (CacheLock)
        {
            return IsRunningCore(targetPath);
        }
    }

    private static bool IsRunningCore(string targetPath)
    {
        if (ProcessIds.TryGetValue(targetPath, out int processId))
        {
            try
            {
                using Process cachedProcess = Process.GetProcessById(processId);
                if (!cachedProcess.HasExited && MatchesPath(cachedProcess, targetPath)) return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                or Win32Exception or NotSupportedException)
            {
            }

            ProcessIds.Remove(targetPath);
        }

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
            foreach (Process process in processes)
            {
                if (!MatchesPath(process, targetPath)) continue;

                ProcessIds[targetPath] = process.Id;
                return true;
            }

            return false;
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
