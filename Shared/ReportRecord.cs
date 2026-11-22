using MessagePack;

namespace CbmrWebAdmin.Shared;

[MessagePackObject]
public sealed class ReportRecord
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public string ReporterSteamId { get; set; } = "";
    [Key(2)] public string ReporterName { get; set; } = "";
    [Key(3)] public string ReportedSteamId { get; set; } = "";
    [Key(4)] public string ReportedName { get; set; } = "";
    [Key(5)] public string Content { get; set; } = "";
    [Key(6)] public DateTime CreatedAtUtc { get; set; }
    [Key(7)] public bool Handled { get; set; }
    [Key(8)] public DateTime? HandledAtUtc { get; set; }
    [Key(9)] public string? Reply { get; set; }
    [Key(10)] public string? RepliedBy { get; set; }
    [Key(11)] public DateTime? ReplyDeliveredAtUtc { get; set; }
}

[MessagePackObject]
public sealed class ReplyReportRequest
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public string Reply { get; set; } = "";
    [Key(2)] public string AdminName { get; set; } = "";
}

[MessagePackObject]
public sealed class ReplyReportResponse
{
    [Key(0)] public bool PlayerOnline { get; set; }
}
