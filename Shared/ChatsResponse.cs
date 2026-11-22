using MessagePack;

namespace CbmrWebAdmin.Shared;

[MessagePackObject]
public sealed class ChatsResponse
{
    [Key(0)] public Guid StreamId { get; init; }
    [Key(1)] public List<WebChat> Messages { get; set; } = [];
}
