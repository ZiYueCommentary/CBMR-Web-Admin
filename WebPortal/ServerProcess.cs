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

internal sealed record ServerRestartTarget(int ProcessId, DateTime StartedAt, string Path,
    string? WorkingDirectory, string[]? Arguments);

internal static class ServerProcess
{
    private static readonly object CacheLock = new();
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly Dictionary<string, int> ProcessIds = new(PathComparer);
    private static readonly SemaphoreSlim RestartLock = new(1, 1);

    private static bool MatchesPath(Process process, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        try
        {
            string? processPath = process.MainModule?.FileName;
            return processPath is not null &&
                   PathComparer.Equals(Path.GetFullPath(processPath), Path.GetFullPath(targetPath));
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

    public static async Task<string> ForceRestartAsync(ServerRestartTarget? target)
    {
        if (!RestartLock.Wait(0))
            throw new InvalidOperationException("A force restart is already in progress.");

        try
        {
            if (target is null)
                throw new InvalidOperationException("The running server process could not be verified.");

            using Process previous = Process.GetProcessById(target.ProcessId);
            _ = previous.SafeHandle;
            if (previous.HasExited || previous.StartTime != target.StartedAt)
                throw new InvalidOperationException("The verified server process is no longer running.");

            string executable = previous.MainModule?.FileName
                                ?? throw new InvalidOperationException("The server executable could not be determined.");
            if (!Path.IsPathFullyQualified(target.Path) ||
                !PathComparer.Equals(Path.GetFullPath(executable), Path.GetFullPath(target.Path)))
                throw new InvalidOperationException("The server process does not match the reported executable.");

            using FileStream executableFile = File.Open(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
            string workingDirectory = target.WorkingDirectory ?? Path.GetDirectoryName(executable)!;
            if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
                throw new InvalidOperationException("The server working directory does not exist.");

            ProcessStartInfo startInfo = new(executable)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            };

            foreach (string argument in target.Arguments ?? [])
                startInfo.ArgumentList.Add(argument);

            previous.Kill(entireProcessTree: true);
            using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15)))
                await previous.WaitForExitAsync(timeout.Token);

            using Process started = Process.Start(startInfo)
                                    ?? throw new InvalidOperationException("No server process was started.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (started.HasExited)
                throw new InvalidOperationException($"The server process exited during startup with code {started.ExitCode}.");
            return executable;
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
