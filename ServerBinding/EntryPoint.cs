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

using System.Diagnostics;
using System.IO.Pipes;
using CCB.Abstractions;
using CCB.Attributes;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

[Injectable]
public class EntryPoint(ILogger<Metadata> logger, IConfigProvider<Config> configProvider) : ILoad, IUnload
{
    private readonly Config _config = configProvider.GetConfig();
    public Process? Child;
    public NamedPipeServerStream? PipeServer;

    public void Load()
    {
        // if (_config.WebPortalPath is null)
        // {
            // logger.LogError("Config does not declare Web Portal's executable file path!");
            // return;
        // }

        int pid = Environment.ProcessId;
        string pipeName = $"CbmrWebAdmin"; // _{pid}

        PipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1);
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = _config.WebPortalPath,
            Arguments = $"--PipeName=\"CbmrWebAdmin\"", // _{pid.ToString()}
            UseShellExecute = false
        };

        //Child = Process.Start(startInfo);
        //if (Child is null)
        //{
        //    logger.LogError("Can't launch Web Portal, is the path valid? path: {}", _config.WebPortalPath);
        //}

        PipeServer.WaitForConnection();

        logger.LogInformation("Server connected to Web Portal successfully.");

        new Thread(() => Listener.StartListenAsync(PipeServer).GetAwaiter().GetResult()).Start();
    }

    public void Unload()
    {
        Child?.WaitForExit();
        PipeServer?.Disconnect();
    }
}