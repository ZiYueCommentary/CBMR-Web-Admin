using MessagePack;

namespace CbmrWebAdmin.Shared;

[MessagePackObject]
public sealed class BanListResponse
{
    [Key(0)] public List<BanRecord> Records { get; init; } = [];
    [Key(1)] public int TotalCount { get; init; }
}
