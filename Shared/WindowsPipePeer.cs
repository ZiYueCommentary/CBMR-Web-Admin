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
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace CbmrWebAdmin.Shared;

[SupportedOSPlatform("windows")]
public static class WindowsPipePeer
{
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;

    public static Process? TryOpenTrustedProcess(PipeStream pipe)
    {
        Process? process = null;
        try
        {
            bool found = pipe is NamedPipeServerStream
                ? GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint processId)
                : GetNamedPipeServerProcessId(pipe.SafePipeHandle, out processId);
            if (!found || processId == 0 || processId == Environment.ProcessId) return null;

            process = Process.GetProcessById((int)processId);
            if (!OpenProcessToken(process.SafeHandle, TokenQuery, out SafeAccessTokenHandle token)) return null;
            using (token)
            using (WindowsIdentity peer = new(token.DangerousGetHandle()))
            using (WindowsIdentity current = WindowsIdentity.GetCurrent())
            {
                if (current.User is not { } currentUser || peer.User is not { } peerUser ||
                    !peerUser.Equals(currentUser) || IsTokenRestricted(token) ||
                    !ReadIntegrityLevel(token).Equals(ReadIntegrityLevel(current.AccessToken)))
                    return null;
            }

            Process trusted = process;
            process = null;
            return trusted;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or Win32Exception or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static SecurityIdentifier ReadIntegrityLevel(SafeAccessTokenHandle token)
    {
        GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int length);
        if (length <= 0) throw new Win32Exception();

        IntPtr buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, length, out _))
                throw new Win32Exception();
            return new SecurityIdentifier(Marshal.ReadIntPtr(buffer));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int tokenInformationClass,
        IntPtr buffer, int bufferLength, out int returnLength);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsTokenRestricted(SafeAccessTokenHandle token);
}
