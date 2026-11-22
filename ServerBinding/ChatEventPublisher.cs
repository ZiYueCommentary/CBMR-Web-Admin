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
        if (Interlocked.Exchange(ref _chatPending, 1) == 0)
            PublishEvent(PipeEnvelope.CreateEvent(PipeMessageType.ChatUpdated));
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
                    PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken);
                Channel<PipeEnvelope> notifications = Channel.CreateUnbounded<PipeEnvelope>(new UnboundedChannelOptions
                {
                    SingleReader = true
                });
                Volatile.Write(ref _notifications, notifications);
                Interlocked.Exchange(ref _chatPending, 0);
                Publish();
                notifications.Writer.TryWrite(PipeEnvelope.CreateEvent(PipeMessageType.ReportsUpdated));
                await foreach (PipeEnvelope notification in notifications.Reader.ReadAllAsync(cancellationToken))
                {
                    if (notification.MessageType == PipeMessageType.ChatUpdated)
                        Interlocked.Exchange(ref _chatPending, 0);
                    await PipeProtocol.WriteAsync(pipe, notification, cancellationToken);
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
            finally
            {
                Interlocked.Exchange(ref _notifications, null)?.Writer.TryComplete();
            }
        }
    }
}
