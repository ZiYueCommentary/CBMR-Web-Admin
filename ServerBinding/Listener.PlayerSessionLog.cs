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

        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        IQueryable<PlayerSessionEntry> query = database.PlayerSessions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string search = filter.Search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            string pattern = $"%{search}%";
            query = query.Where(entry =>
                EF.Functions.Like(entry.SteamId, pattern, @"\")
                || EF.Functions.Like(entry.Name, pattern, @"\")
                || EF.Functions.Like(entry.IpAddress, pattern, @"\"));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<PlayerSessionRecord> records = await query
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
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
}
