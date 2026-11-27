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
                events.IsConnected = true;
                logger.LogInformation("Connected to server events pipe.");
                events.NotifyChatUpdated();
                events.NotifyReportsUpdated();
                while (await PipeProtocol.ReadAsync(pipe, stoppingToken) is { } envelope)
                {
                    if (envelope.Kind == PipeEnvelopeKind.Event && envelope.MessageType == PipeMessageType.ChatUpdated)
                        events.NotifyChatUpdated();
                    else if (envelope.Kind == PipeEnvelopeKind.Event && envelope.MessageType == PipeMessageType.ReportCreated)
                        events.NotifyReportsUpdated(envelope.DeserializePayload<ReportRecord>());
                    else if (envelope.Kind == PipeEnvelopeKind.Event && envelope.MessageType == PipeMessageType.ReportsUpdated)
                        events.NotifyReportsUpdated();
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
            finally
            {
                events.IsConnected = false;
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
