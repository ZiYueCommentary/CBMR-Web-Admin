using CbmrWebAdmin.Shared;

namespace CbmrWebAdmin.WebPortal;

public class PipeGateway(PipeMessageQueue queue)
{
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

        await queue.Channel.Writer.WriteAsync(new PipeRequest(envelope, completion), cancellationToken);
        PipeEnvelope response = await completion.Task.WaitAsync(cancellationToken);

        if (response.Kind == PipeEnvelopeKind.Error)
        {
            throw new InvalidOperationException(response.Error ?? $"Pipe request '{response.MessageType}' failed.");
        }

        return response;
    }
}