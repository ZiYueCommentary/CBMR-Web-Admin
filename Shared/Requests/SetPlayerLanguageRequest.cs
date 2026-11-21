using MessagePack;

namespace CbmrWebAdmin.Shared.Requests;

[MessagePackObject]
public sealed class SetPlayerLanguageRequest
{
    [Key(0)]
    public required string Culture { get; init; }
}
