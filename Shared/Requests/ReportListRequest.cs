using MessagePack;

namespace CbmrWebAdmin.Shared.Requests;

public enum ReportSortColumn { Time, Reporter, ReportedPlayer, Content, Reply }

[MessagePackObject]
public sealed class ReportListRequest
{
    [Key(0)] public int Offset { get; init; }
    [Key(1)] public int PageSize { get; init; }
    [Key(2)] public bool Handled { get; init; }
    [Key(3)] public DateTime? FromUtc { get; init; }
    [Key(4)] public DateTime? ToUtc { get; init; }
    [Key(5)] public string? Reporter { get; init; }
    [Key(6)] public string? ReportedPlayer { get; init; }
    [Key(7)] public string? Content { get; init; }
    [Key(8)] public ReportSortColumn SortColumn { get; init; }
    [Key(9)] public bool SortAscending { get; init; }
}

[MessagePackObject]
public sealed class ReportListResponse
{
    [Key(0)] public List<ReportRecord> Records { get; init; } = [];
    [Key(1)] public int TotalCount { get; init; }
}
