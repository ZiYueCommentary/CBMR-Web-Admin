using MessagePack;

namespace CbmrWebAdmin.Shared.Requests;

[MessagePackObject]
public sealed class ChatsRequest
{
    [Key(0)] public long AfterSequence { get; init; }
    [Key(1)] public Guid StreamId { get; init; }
}
