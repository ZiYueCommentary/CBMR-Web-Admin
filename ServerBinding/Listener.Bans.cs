using System.Linq.Expressions;
using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using CbmrWebAdmin.Shared.Requests;
using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.ServerBinding;

public static partial class Listener
{
    private static async Task<PipeEnvelope> HandleQueryBansAsync(
        PipeEnvelope request, string databasePath, CancellationToken cancellationToken)
    {
        BanListRequest filter = request.DeserializePayload<BanListRequest>();
        if (filter.Offset < 0 || filter.PageSize is < 1 or > 100)
            throw new InvalidDataException("Invalid bans page.");
        if (filter.FromUtc is { Kind: not DateTimeKind.Utc } || filter.ToUtc is { Kind: not DateTimeKind.Utc } ||
            filter.FromUtc > filter.ToUtc)
            throw new InvalidDataException("Invalid UTC date range.");
        if (!Enum.IsDefined(filter.Status))
            throw new InvalidDataException("Invalid ban status.");

        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        IQueryable<Ban> query = database.Bans.AsNoTracking();
        DateTime now = DateTime.UtcNow;
        if (filter.Status == BanStatus.Active)
            query = query.Where(ban => ban.ExpiresAt == null || ban.ExpiresAt > now);
        else if (filter.Status == BanStatus.Expired)
            query = query.Where(ban => ban.ExpiresAt != null && ban.ExpiresAt <= now);
        if (filter.FromUtc is { } from) query = query.Where(ban => ban.StartedAt >= from);
        if (filter.ToUtc is { } to) query = query.Where(ban => ban.StartedAt <= to);
        if (!string.IsNullOrWhiteSpace(filter.SteamId))
        {
            string pattern = BanSearchPattern(filter.SteamId);
            query = query.Where(ban => EF.Functions.Like(ban.SteamId, pattern, @"\"));
        }
        if (!string.IsNullOrWhiteSpace(filter.Reason))
        {
            string pattern = BanSearchPattern(filter.Reason);
            query = query.Where(ban => EF.Functions.Like(ban.Reason, pattern, @"\"));
        }
        IOrderedQueryable<Ban> ordered = filter.SortColumn switch
        {
            BanSortColumn.StartedAt => SortBans(query, ban => ban.StartedAt, filter.SortAscending),
            BanSortColumn.Id => SortBans(query, ban => ban.Id, filter.SortAscending),
            BanSortColumn.SteamId => SortBans(query, ban => ban.SteamId, filter.SortAscending),
            BanSortColumn.Reason => SortBans(query, ban => ban.Reason, filter.SortAscending),
            BanSortColumn.ExpiresAt => SortBans(query, ban => ban.ExpiresAt, filter.SortAscending),
            _ => throw new InvalidDataException("Invalid ban sort column.")
        };
        ordered = filter.SortAscending ? ordered.ThenBy(ban => ban.Id) : ordered.ThenByDescending(ban => ban.Id);
        int total = await query.CountAsync(cancellationToken);
        List<BanRecord> records = await ordered.Skip(filter.Offset).Take(filter.PageSize)
            .Select(ban => new BanRecord
            {
                Id = ban.Id, SteamId = ban.SteamId, Reason = ban.Reason,
                StartedAtUtc = DateTime.SpecifyKind(ban.StartedAt, DateTimeKind.Utc), ExpiresAtUtc = ban.ExpiresAt
            }).ToListAsync(cancellationToken);
        return PipeEnvelope.CreateResponse(request, new BanListResponse { Records = records, TotalCount = total });
    }

    private static string BanSearchPattern(string value) =>
        "%" + value.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

    private static IOrderedQueryable<Ban> SortBans<TKey>(
        IQueryable<Ban> query, Expression<Func<Ban, TKey>> key, bool ascending) =>
        ascending ? query.OrderBy(key) : query.OrderByDescending(key);
}
