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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeClientStream pipe = new NamedPipeClientStream(
                    ".",
                    configuration["PipeName"] ?? "CbmrWebAdmin",
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(stoppingToken);

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
                    catch (Exception exception)
                    {
                        request.Completion.TrySetException(exception);
                        throw;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Pipe connection failed; reconnecting in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}