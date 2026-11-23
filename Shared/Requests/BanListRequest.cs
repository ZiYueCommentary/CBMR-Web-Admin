using MessagePack;

namespace CbmrWebAdmin.Shared.Requests;

public enum BanSortColumn { StartedAt, Id, SteamId, Reason, ExpiresAt }
public enum BanStatus { All, Active, Expired }

[MessagePackObject]
public sealed class BanListRequest
{
    [Key(0)] public int Offset { get; init; }
    [Key(1)] public int PageSize { get; init; }
    [Key(2)] public DateTime? FromUtc { get; init; }
    [Key(3)] public DateTime? ToUtc { get; init; }
    [Key(4)] public string? SteamId { get; init; }
    [Key(5)] public string? Reason { get; init; }
    [Key(6)] public BanStatus Status { get; init; } = BanStatus.Active;
    [Key(7)] public BanSortColumn SortColumn { get; init; }
    [Key(8)] public bool SortAscending { get; init; }
}
