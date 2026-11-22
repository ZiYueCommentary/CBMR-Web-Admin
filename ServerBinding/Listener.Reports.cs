using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.ServerBinding;

public static partial class Listener
{
    internal static ReportRecord ToRecord(Report report) => new()
    {
        Id = report.Id, ReporterSteamId = report.ReporterSteamId, ReporterName = report.ReporterName,
        ReportedSteamId = report.ReportedSteamId, ReportedName = report.ReportedName,
        Content = report.Content, CreatedAtUtc = DateTime.SpecifyKind(report.CreatedAtUtc, DateTimeKind.Utc),
        Handled = report.Handled,
        HandledAtUtc = report.HandledAtUtc is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null,
        Reply = report.Reply, RepliedBy = report.RepliedBy,
        ReplyDeliveredAtUtc = report.ReplyDeliveredAtUtc is { } delivered ? DateTime.SpecifyKind(delivered, DateTimeKind.Utc) : null,
        Accepted = report.Accepted
    };

    private static async Task<PipeEnvelope> HandleListReportsAsync(PipeEnvelope request, string path, CancellationToken token)
    {
        await using WebPortalDbContext database = WebPortalDbContext.Create(path);
        var reports = await database.Reports.AsNoTracking().OrderByDescending(report => report.CreatedAtUtc)
            .ThenByDescending(report => report.Id).ToListAsync(token);
        return PipeEnvelope.CreateResponse(request, reports.Select(ToRecord).ToList());
    }

    private static async Task<PipeEnvelope> HandleReplyReportAsync(PipeEnvelope request, string path,
        PlayerLanguageSettings language, CancellationToken token)
    {
        ReplyReportRequest reply = request.DeserializePayload<ReplyReportRequest>();
        if (reply.Accepted is null || string.IsNullOrWhiteSpace(reply.Reply) || reply.Reply.Length > 2000 || string.IsNullOrWhiteSpace(reply.AdminName))
            return PipeEnvelope.CreateError(request, "Invalid report reply.");
        await using WebPortalDbContext database = WebPortalDbContext.Create(path);
        Report? report = await database.Reports.SingleOrDefaultAsync(report => report.Id == reply.Id, token);
        if (report is null || report.Handled) return PipeEnvelope.CreateError(request, "Report not found or already handled.");
        report.Handled = true;
        report.HandledAtUtc = DateTime.UtcNow;
        report.Reply = reply.Reply.Trim();
        report.RepliedBy = reply.AdminName.Trim();
        report.Accepted = reply.Accepted;
        await database.SaveChangesAsync(token);
        EntryPoint.ChatEvents.PublishEvent(PipeEnvelope.CreateEvent(PipeMessageType.ReportsUpdated));
        bool online = await ReportReplyDelivery.DeliverAsync(path, report.ReporterSteamId, language, token, report.Id);
        return PipeEnvelope.CreateResponse(request, new ReplyReportResponse { PlayerOnline = online });
    }
}
