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

using System.IO.Pipes;
using CCB.Abstractions;
using CCB.Attributes;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

[Injectable]
public class EntryPoint(ILogger<Metadata> logger, IConfigProvider<Config> configProvider) : ILoad, IUnload
{
    private readonly Config _config = configProvider.GetConfig();
    private CancellationTokenSource? _listenerCancellation;
    private Thread? _listenerThread;
    private ServerOutputWriter? _serverOutputWriter;

    private NamedPipeServerStream? _pipeServer;

    public void Load()
    {
        _serverOutputWriter = new ServerOutputWriter(Console.Out);
        Console.SetOut(_serverOutputWriter);

        _listenerCancellation = new CancellationTokenSource();
        _listenerThread = new Thread(() =>
                ListenForConnectionsAsync(_listenerCancellation.Token).GetAwaiter().GetResult())
        {
            IsBackground = true,
            Name = "CbmrWebAdmin.ServerBinding pipe listener"
        };
        _listenerThread.Start();
    }

    public void Unload()
    {
        _listenerCancellation?.Cancel();
        _pipeServer?.Dispose();
        _listenerThread?.Join(TimeSpan.FromSeconds(5));
        _listenerCancellation?.Dispose();

        if (_serverOutputWriter is not null && ReferenceEquals(Console.Out, _serverOutputWriter))
        {
            Console.SetOut(_serverOutputWriter.OriginalWriter);
        }

        _serverOutputWriter = null;
    }

    private async Task ListenForConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using NamedPipeServerStream pipeServer = new NamedPipeServerStream(
                "CbmrWebAdmin", //todo dyna
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            _pipeServer = pipeServer;

            try
            {
                await pipeServer.WaitForConnectionAsync(cancellationToken);
                logger.LogInformation("Server connected to Web Portal successfully.");
                await Listener.StartListenAsync(logger, pipeServer, cancellationToken);
                logger.LogInformation("Web Portal disconnected; waiting for a new pipe connection.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Pipe connection was damaged; waiting for a new connection.");
            }
            finally
            {
                if (ReferenceEquals(_pipeServer, pipeServer))
                {
                    _pipeServer = null;
                }
            }
        }
    }
}