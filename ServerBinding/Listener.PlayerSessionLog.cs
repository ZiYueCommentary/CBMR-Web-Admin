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

using System.Linq.Expressions;
using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using CbmrWebAdmin.Shared.Requests;
using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.ServerBinding;

public static partial class Listener
{
    private static async Task<PipeEnvelope> HandlePlayerSessionLogAsync(
        PipeEnvelope request, string databasePath, CancellationToken cancellationToken)
    {
        PlayerSessionLogRequest filter = request.DeserializePayload<PlayerSessionLogRequest>();
        if (filter.Offset < 0 || filter.PageSize is < 1 or > 100)
            throw new InvalidDataException("Invalid player session log page.");
        if (filter.FromUtc is { Kind: not DateTimeKind.Utc } || filter.BeforeUtc is { Kind: not DateTimeKind.Utc } ||
            filter.FromUtc > filter.BeforeUtc)
            throw new InvalidDataException("Invalid UTC date range.");
        if (filter.Event is { } filterEvent && !Enum.IsDefined(filterEvent))
            throw new InvalidDataException("Invalid player session event.");

        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        IQueryable<PlayerSessionEntry> query = database.PlayerSessions.AsNoTracking();
        if (filter.FromUtc is { } fromUtc)
            query = query.Where(entry => entry.OccurredAtUtc >= fromUtc);
        if (filter.BeforeUtc is { } beforeUtc)
            query = query.Where(entry => entry.OccurredAtUtc < beforeUtc);
        if (filter.Event is { } sessionEvent)
            query = query.Where(entry => entry.Event == sessionEvent);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string search = filter.Search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            string pattern = $"%{search}%";
            query = query.Where(entry =>
                EF.Functions.Like(entry.SteamId, pattern, @"\")
                || EF.Functions.Like(entry.Name, pattern, @"\")
                || EF.Functions.Like(entry.IpAddress, pattern, @"\"));
        }

        IOrderedQueryable<PlayerSessionEntry> ordered = filter.SortColumn switch
        {
            PlayerSessionLogSortColumn.Time => SortBy(query, entry => entry.OccurredAtUtc, filter.SortAscending),
            PlayerSessionLogSortColumn.Event => SortBy(query, entry => entry.Event, filter.SortAscending),
            PlayerSessionLogSortColumn.SteamId => SortBy(query, entry => entry.SteamId, filter.SortAscending),
            PlayerSessionLogSortColumn.Name => SortBy(query, entry => entry.Name, filter.SortAscending),
            PlayerSessionLogSortColumn.IpAddress => SortBy(query, entry => entry.IpAddress, filter.SortAscending),
            _ => throw new InvalidDataException("Invalid player session log sort column.")
        };

        ordered = filter.SortAscending ? ordered.ThenBy(entry => entry.Id) : ordered.ThenByDescending(entry => entry.Id);
        int totalCount = await query.CountAsync(cancellationToken);
        List<PlayerSessionRecord> records = await ordered
            .Skip(filter.Offset)
            .Take(filter.PageSize)
            .Select(entry => new PlayerSessionRecord
            {
                SteamId = entry.SteamId,
                Name = entry.Name,
                IpAddress = entry.IpAddress,
                Event = entry.Event,
                OccurredAtUtc = entry.OccurredAtUtc
            })
            .ToListAsync(cancellationToken);
        return PipeEnvelope.CreateResponse(request, new PlayerSessionLogResponse
        {
            Records = records,
            TotalCount = totalCount
        });
    }

    private static IOrderedQueryable<PlayerSessionEntry> SortBy<TKey>(
        IQueryable<PlayerSessionEntry> query, Expression<Func<PlayerSessionEntry, TKey>> key, bool ascending) =>
        ascending ? query.OrderBy(key) : query.OrderByDescending(key);
}
