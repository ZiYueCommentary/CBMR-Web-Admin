using System.IO.Pipes;
using System.Threading.Channels;
using CbmrWebAdmin.Shared;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

internal sealed class ChatEventPublisher
{
    private Channel<bool>? _notifications;

    public void Publish()
    {
        Volatile.Read(ref _notifications)?.Writer.TryWrite(true);
    }

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
                Channel<bool> notifications = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true
                });
                Volatile.Write(ref _notifications, notifications);
                notifications.Writer.TryWrite(true);
                await foreach (bool notification in notifications.Reader.ReadAllAsync(cancellationToken))
                {
                    await PipeProtocol.WriteAsync(pipe,
                        PipeEnvelope.CreateEvent(PipeMessageType.ChatUpdated), cancellationToken);
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
