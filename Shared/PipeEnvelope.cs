using MessagePack;

namespace CbmrWebAdmin.Shared;

public enum PipeEnvelopeKind : byte
{
    Request,
    Response,
    Error
}

public enum PipeMessageType : byte
{
    Ack,
    FixElevator,
    Players
}

[MessagePackObject]
public sealed class PipeEnvelope
{
    public const byte CurrentVersion = 1;

    [Key(0)]
    public byte Version { get; init; }

    [Key(1)]
    public Guid RequestId { get; init; }

    [Key(2)]
    public PipeEnvelopeKind Kind { get; init; }

    [Key(3)]
    public PipeMessageType MessageType { get; init; }

    [Key(4)]
    public byte[]? Payload { get; init; }

    [Key(5)]
    public string? Error { get; init; }

    public static PipeEnvelope CreateRequest<T>(PipeMessageType messageType, T payload)
    {
        return new PipeEnvelope
        {
            RequestId = Guid.NewGuid(),
            Version = CurrentVersion,
            Kind = PipeEnvelopeKind.Request,
            MessageType = messageType,
            Payload = MessagePackSerializer.Serialize(payload)
        };
    }

    public static PipeEnvelope CreateRequest(PipeMessageType messageType)
    {
        return new PipeEnvelope
        {
            RequestId = Guid.NewGuid(),
            Version = CurrentVersion,
            Kind = PipeEnvelopeKind.Request,
            MessageType = messageType
        };
    }

    public static PipeEnvelope CreateResponse(PipeEnvelope request)
    {
        return CreateResponse(request, payload: null);
    }

    public static PipeEnvelope CreateResponse<T>(PipeEnvelope request, T payload)
    {
        return CreateResponse(request, MessagePackSerializer.Serialize(payload));
    }

    public static PipeEnvelope CreateError(PipeEnvelope request, string error)
    {
        return new PipeEnvelope
        {
            RequestId = request.RequestId,
            Version = CurrentVersion,
            Kind = PipeEnvelopeKind.Error,
            MessageType = request.MessageType,
            Error = error
        };
    }

    public T DeserializePayload<T>()
    {
        if (Payload is null)
        {
            throw new InvalidDataException($"Pipe envelope '{MessageType}' does not contain a payload.");
        }

        return MessagePackSerializer.Deserialize<T>(Payload);
    }

    private static PipeEnvelope CreateResponse(PipeEnvelope request, byte[]? payload)
    {
        return new PipeEnvelope
        {
            RequestId = request.RequestId,
            Version = CurrentVersion,
            Kind = PipeEnvelopeKind.Response,
            MessageType = request.MessageType,
            Payload = payload
        };
    }
}