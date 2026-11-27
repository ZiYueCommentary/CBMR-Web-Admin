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
using System.Threading.Channels;
using CbmrWebAdmin.Shared;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

internal sealed class ChatEventPublisher
{
    private Channel<PipeEnvelope>? _notifications;
    private int _chatPending;

    public void Publish()
    {
        Channel<PipeEnvelope>? notifications = Volatile.Read(ref _notifications);
        if (notifications is null) return;
        if (Interlocked.Exchange(ref _chatPending, 1) != 0) return;
        if (!notifications.Writer.TryWrite(PipeEnvelope.CreateEvent(PipeMessageType.ChatUpdated)))
            Interlocked.Exchange(ref _chatPending, 0);
    }

    public void PublishEvent(PipeEnvelope envelope) =>
        Volatile.Read(ref _notifications)?.Writer.TryWrite(envelope);

    public async Task RunAsync(string pipeName, ILogger logger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeServerStream pipe = new(
                    pipeName + PipeEnvelope.EventsPipeSuffix,
                    PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken);
                Channel<PipeEnvelope> notifications = Channel.CreateUnbounded<PipeEnvelope>(new UnboundedChannelOptions
                {
                    SingleReader = true
                });
                using CancellationTokenSource connectionCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task disconnectWatcher = WaitForDisconnectAsync(pipe, connectionCancellation);
                Volatile.Write(ref _notifications, notifications);
                Interlocked.Exchange(ref _chatPending, 0);
                Publish();
                notifications.Writer.TryWrite(PipeEnvelope.CreateEvent(PipeMessageType.ReportsUpdated));
                try
                {
                    await foreach (PipeEnvelope notification in notifications.Reader.ReadAllAsync(connectionCancellation.Token))
                    {
                        if (notification.MessageType == PipeMessageType.ChatUpdated)
                            Interlocked.Exchange(ref _chatPending, 0);
                        await PipeProtocol.WriteAsync(pipe, notification, connectionCancellation.Token);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug("Events pipe client disconnected; waiting for a new connection.");
                }
                finally
                {
                    Interlocked.Exchange(ref _notifications, null)?.Writer.TryComplete();
                    await connectionCancellation.CancelAsync();
                    try
                    {
                        await disconnectWatcher;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref _notifications, null)?.Writer.TryComplete();
                logger.LogDebug(exception, "Events pipe disconnected; waiting for a new connection.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private static async Task WaitForDisconnectAsync(
        NamedPipeServerStream pipe,
        CancellationTokenSource connectionCancellation)
    {
        byte[] buffer = new byte[1];
        try
        {
            while (await pipe.ReadAsync(buffer, connectionCancellation.Token) > 0)
            {
            }
        }
        catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        await connectionCancellation.CancelAsync();
    }
}
