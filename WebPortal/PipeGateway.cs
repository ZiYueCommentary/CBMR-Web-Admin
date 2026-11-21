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

using CbmrWebAdmin.Shared;

namespace CbmrWebAdmin.WebPortal;

public class PipeGateway(PipeMessageQueue queue)
{
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public async Task SendAsync(PipeMessageType messageType, CancellationToken cancellationToken = default)
    {
        await SendEnvelopeAsync(PipeEnvelope.CreateRequest(messageType), cancellationToken);
    }

    public async Task<TResponse> SendAsync<TResponse>(
        PipeMessageType messageType,
        CancellationToken cancellationToken = default)
    {
        PipeEnvelope response = await SendEnvelopeAsync(PipeEnvelope.CreateRequest(messageType), cancellationToken);
        return response.DeserializePayload<TResponse>();
    }

    public async Task SendAsync<TRequest>(
        PipeMessageType messageType,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        PipeEnvelope request = PipeEnvelope.CreateRequest(messageType, payload);
        await SendEnvelopeAsync(request, cancellationToken);
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(
        PipeMessageType messageType,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        PipeEnvelope request = PipeEnvelope.CreateRequest(messageType, payload);
        PipeEnvelope response = await SendEnvelopeAsync(request, cancellationToken);
        return response.DeserializePayload<TResponse>();
    }

    private async Task<PipeEnvelope> SendEnvelopeAsync(
        PipeEnvelope envelope,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<PipeEnvelope> completion =
            new TaskCompletionSource<PipeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        using CancellationTokenRegistration registration = timeout.Token.Register(() => completion.TrySetCanceled(timeout.Token));

        PipeEnvelope response;
        try
        {
            await queue.Channel.Writer.WriteAsync(new PipeRequest(envelope, completion, timeout.Token), timeout.Token);
            response = await completion.Task;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Pipe request '{envelope.MessageType}' timed out.");
        }

        if (response.Kind == PipeEnvelopeKind.Error)
        {
            throw new InvalidOperationException(response.Error ?? $"Pipe request '{response.MessageType}' failed.");
        }

        return response;
    }
}