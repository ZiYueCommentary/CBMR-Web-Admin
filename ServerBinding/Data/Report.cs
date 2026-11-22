namespace CbmrWebAdmin.ServerBinding.Data;

public sealed class Report
{
    public long Id { get; set; }
    public string ReporterSteamId { get; set; } = "";
    public string ReporterName { get; set; } = "";
    public string ReportedSteamId { get; set; } = "";
    public string ReportedName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public bool Handled { get; set; }
    public DateTime? HandledAtUtc { get; set; }
    public string? Reply { get; set; }
    public string? RepliedBy { get; set; }
}
