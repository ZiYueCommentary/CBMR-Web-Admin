// Copyright 2026 ZiYueCommentary
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using CbmrWebAdmin.Shared.Requests;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.ServerBinding;

public static partial class Listener
{
    private static async Task<PipeEnvelope> HandleQueryReportsAsync(PipeEnvelope request, string path, CancellationToken token)
    {
        ReportListRequest filter = request.DeserializePayload<ReportListRequest>();
        if (filter.Offset < 0 || filter.PageSize is < 1 or > 100 || !Enum.IsDefined(filter.SortColumn))
            throw new InvalidDataException("Invalid reports page or sort column.");
        if (filter.FromUtc is { Kind: not DateTimeKind.Utc } || filter.ToUtc is { Kind: not DateTimeKind.Utc } || filter.FromUtc > filter.ToUtc)
            throw new InvalidDataException("Invalid UTC date range.");
        await using WebPortalDbContext database = WebPortalDbContext.Create(path);
        IQueryable<Report> query = database.Reports.AsNoTracking().Where(report => report.Handled == filter.Handled);
        if (filter.FromUtc is { } from) query = query.Where(report => report.CreatedAtUtc >= from);
        if (filter.ToUtc is { } to) query = query.Where(report => report.CreatedAtUtc <= to);
        if (!string.IsNullOrWhiteSpace(filter.Reporter))
        {
            string pattern = ReportSearchPattern(filter.Reporter);
            query = query.Where(report => EF.Functions.Like(report.ReporterName, pattern, @"\") || EF.Functions.Like(report.ReporterSteamId, pattern, @"\"));
        }
        if (!string.IsNullOrWhiteSpace(filter.ReportedPlayer))
        {
            string pattern = ReportSearchPattern(filter.ReportedPlayer);
            query = query.Where(report => EF.Functions.Like(report.ReportedName, pattern, @"\") || EF.Functions.Like(report.ReportedSteamId, pattern, @"\"));
        }
        if (!string.IsNullOrWhiteSpace(filter.Content))
        {
            string pattern = ReportSearchPattern(filter.Content);
            query = query.Where(report => EF.Functions.Like(report.Content, pattern, @"\"));
        }
        IOrderedQueryable<Report> ordered = filter.SortColumn switch
        {
            ReportSortColumn.Reporter => SortReports(query, report => report.ReporterName, filter.SortAscending),
            ReportSortColumn.ReportedPlayer => SortReports(query, report => report.ReportedName, filter.SortAscending),
            ReportSortColumn.Content => SortReports(query, report => report.Content, filter.SortAscending),
            ReportSortColumn.Reply => SortReports(query, report => report.Reply, filter.SortAscending),
            _ => SortReports(query, report => report.CreatedAtUtc, filter.SortAscending)
        };
        ordered = filter.SortAscending ? ordered.ThenBy(report => report.Id) : ordered.ThenByDescending(report => report.Id);
        int total = await query.CountAsync(token);
        List<Report> reports = await ordered.Skip(filter.Offset).Take(filter.PageSize).ToListAsync(token);
        return PipeEnvelope.CreateResponse(request, new ReportListResponse { Records = reports.Select(ToRecord).ToList(), TotalCount = total });
    }

    private static string ReportSearchPattern(string value) =>
        "%" + value.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

    private static IOrderedQueryable<Report> SortReports<TKey>(IQueryable<Report> query, Expression<Func<Report, TKey>> key, bool ascending) =>
        ascending ? query.OrderBy(key) : query.OrderByDescending(key);

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

    private static async Task<PipeEnvelope> HandleCountUnhandledReportsAsync(PipeEnvelope request, string path, CancellationToken token)
    {
        await using WebPortalDbContext database = WebPortalDbContext.Create(path);
        int count = await database.Reports.CountAsync(report => !report.Handled, token);
        return PipeEnvelope.CreateResponse(request, count);
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
