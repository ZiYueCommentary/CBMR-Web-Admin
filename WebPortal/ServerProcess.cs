// Copyright 2026 ZiYueCommentary
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
