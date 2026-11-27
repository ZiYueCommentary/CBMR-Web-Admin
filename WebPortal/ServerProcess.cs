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
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly Dictionary<string, int> ProcessIds = new(PathComparer);
    private static readonly SemaphoreSlim RestartLock = new(1, 1);

    private static bool MatchesPath(Process process, string targetPath, bool requireInspection = false)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        try
        {
            if (process.HasExited) return false;

            string? processPath = process.MainModule?.FileName;
            if (processPath is null && requireInspection && !process.HasExited)
                throw new InvalidOperationException("Could not inspect a process matching the server executable name.");

            return processPath is not null &&
                   PathComparer.Equals(Path.GetFullPath(processPath), Path.GetFullPath(targetPath));
        }
        catch (Exception exception) when ((exception is Win32Exception or InvalidOperationException
            or NotSupportedException or UnauthorizedAccessException or ArgumentException or IOException)
            && (!requireInspection || process.HasExited))
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

    public static async Task ForceRestartAsync(string executable, string? arguments, string? workingDirectory)
    {
        if (!RestartLock.Wait(0))
            throw new InvalidOperationException("A force restart is already in progress.");

        try
        {
            if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
                throw new InvalidOperationException("Force restart requires an existing absolute executable path.");

            executable = Path.GetFullPath(executable);
            workingDirectory ??= Path.GetDirectoryName(executable)!;
            if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
                throw new InvalidOperationException("The server working directory must be an existing absolute path.");

            ProcessStartInfo startInfo = new(executable, arguments ?? string.Empty)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            };

            bool canFilterByName = OperatingSystem.IsWindows();
            Process[] processes = canFilterByName
                ? Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable))
                : Process.GetProcesses();
            try
            {
                Process[] matches = processes.Where(process => MatchesPath(process, executable, canFilterByName)).ToArray();
                if (matches.Any(process => process.Id == Environment.ProcessId))
                    throw new InvalidOperationException("The portal cannot restart its own executable.");
                if (matches.Length > 1)
                    throw new InvalidOperationException("More than one process matches the configured server executable.");

                if (matches.Length == 1)
                {
                    Process previous = matches[0];
                    if (!previous.HasExited)
                    {
                        previous.Kill(entireProcessTree: true);
                        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
                        await previous.WaitForExitAsync(timeout.Token);
                    }
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }

            using Process started = Process.Start(startInfo)
                                    ?? throw new InvalidOperationException("No server process was started.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (started.HasExited)
                throw new InvalidOperationException($"The server process exited during startup with code {started.ExitCode}.");
        }
        finally
        {
            RestartLock.Release();
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
