using System.IO.Pipes;
using CbmrWebAdmin.Shared;

namespace CbmrWebAdmin.WebPortal;

public sealed class ServerEventsBackgroundService(
    ServerEvents events,
    IConfiguration configuration,
    ILogger<ServerEventsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan reconnectDelay = TimeSpan.FromSeconds(
            Math.Max(1, configuration.GetValue("PipeReconnectDelaySeconds", 5)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using NamedPipeClientStream pipe = new(".",
                    (configuration["PipeName"] ?? "CbmrWebAdmin") + PipeEnvelope.EventsPipeSuffix,
                    PipeDirection.In, PipeOptions.Asynchronous);
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                {
                    timeout.CancelAfter(PipeGateway.RequestTimeout);
                    await pipe.ConnectAsync(timeout.Token);
                }
                events.NotifyChatUpdated();
                while (await PipeProtocol.ReadAsync(pipe, stoppingToken) is { } envelope)
                {
                    if (envelope.Kind == PipeEnvelopeKind.Event && envelope.MessageType == PipeMessageType.ChatUpdated)
                        events.NotifyChatUpdated();
                    else
                        logger.LogDebug("Ignoring events pipe envelope {Kind}/{MessageType}.", envelope.Kind, envelope.MessageType);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Events pipe connection failed; retrying in {ReconnectDelay}.", reconnectDelay);
            }

            try
            {
                await Task.Delay(reconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
