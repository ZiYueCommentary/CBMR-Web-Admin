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

public class PipeBackgroundService(PipeMessageQueue queue, ILogger<PipeBackgroundService> logger, IConfiguration configuration) : BackgroundService
{
    protected internal static NamedPipeClientStream ServerBindingPipe;
    private const int DefaultReconnectDelaySeconds = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan reconnectDelay = GetReconnectDelay();

        while (true)
        {
            try
            {
                ServerBindingPipe = new NamedPipeClientStream(
                    ".",
                    configuration["PipeName"] ?? "CbmrWebAdmin",
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await ServerBindingPipe.ConnectAsync(stoppingToken);

                await ProcessRequestsAsync(ServerBindingPipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Pipe connection failed; retrying in {ReconnectDelay}.", reconnectDelay);
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

    private async Task ProcessRequestsAsync(NamedPipeClientStream pipe, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            PipeRequest request = await queue.Channel.Reader.ReadAsync(stoppingToken);
            try
            {
                await PipeProtocol.WriteAsync(pipe, request.Envelope, stoppingToken);
                PipeEnvelope response = await PipeProtocol.ReadAsync(pipe, stoppingToken)
                                        ?? throw new EndOfStreamException("The server closed the pipe without a response.");

                if (response.RequestId != request.Envelope.RequestId)
                {
                    throw new InvalidDataException(
                        $"Received response {response.RequestId} for request {request.Envelope.RequestId}.");
                }

                request.Completion.TrySetResult(response);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                request.Completion.TrySetException(exception);
                throw;
            }
        }
    }

    private TimeSpan GetReconnectDelay()
    {
        int seconds = configuration.GetValue("PipeReconnectDelaySeconds", DefaultReconnectDelaySeconds);
        return TimeSpan.FromSeconds(Math.Max(1, seconds));
    }
}