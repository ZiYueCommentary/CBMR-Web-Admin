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
    private static async Task<PipeEnvelope> HandleListBansAsync(
        PipeEnvelope request, string databasePath, CancellationToken cancellationToken)
    {
        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        DateTime utcNow = DateTime.UtcNow;
        List<Ban> bans = await database.Bans.AsNoTracking().OrderByDescending(ban => ban.Id).ToListAsync(cancellationToken);
        return PipeEnvelope.CreateResponse(request, bans
            .Where(ban => ban.Duration is null || ban.StartedAt + ban.Duration.Value > utcNow)
            .Select(ban => new BanRecord
            {
                Id = ban.Id,
                SteamId = ban.SteamId,
                Reason = ban.Reason,
                StartedAtUtc = DateTime.SpecifyKind(ban.StartedAt, DateTimeKind.Utc),
                ExpiresAtUtc = ban.Duration is null ? null : DateTime.SpecifyKind(ban.StartedAt + ban.Duration.Value, DateTimeKind.Utc)
            }).ToList());
    }

    private static async Task<PipeEnvelope> HandleListMutesAsync(
        PipeEnvelope request, string databasePath, CancellationToken cancellationToken)
    {
        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        DateTime utcNow = DateTime.UtcNow;
        List<Mute> mutes = await database.Mutes.AsNoTracking().OrderByDescending(mute => mute.Id).ToListAsync(cancellationToken);
        return PipeEnvelope.CreateResponse(request, mutes
            .Where(mute => mute.Duration is null || mute.StartedAt + mute.Duration.Value > utcNow)
            .Select(mute => new MuteRecord
            {
                Id = mute.Id,
                SteamId = mute.SteamId,
                Reason = mute.Reason,
                StartedAtUtc = DateTime.SpecifyKind(mute.StartedAt, DateTimeKind.Utc),
                ExpiresAtUtc = mute.Duration is null ? null : DateTime.SpecifyKind(mute.StartedAt + mute.Duration.Value, DateTimeKind.Utc),
                Kind = mute.Kind == MuteKind.Voice ? MuteRecordKind.Voice : MuteRecordKind.Chat
            }).ToList());
    }

    private static async Task<PipeEnvelope> HandleUpdateModerationAsync(
        PipeEnvelope request, string databasePath, bool isMute, CancellationToken cancellationToken)
    {
        UpdateModerationRequest update = request.DeserializePayload<UpdateModerationRequest>();
        if (string.IsNullOrWhiteSpace(update.Reason))
            throw new InvalidDataException("A reason is required.");
        if (update.ExpiresAtUtc is { } expiry && (expiry.Kind != DateTimeKind.Utc || expiry <= DateTime.UtcNow))
            throw new InvalidDataException("The expiration must be a future UTC date and time.");

        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        if (isMute)
        {
            Mute? mute = await database.Mutes.SingleOrDefaultAsync(mute => mute.Id == update.Id, cancellationToken);
            if (mute is null) return PipeEnvelope.CreateError(request, "Mute record not found.");
            mute.Reason = update.Reason.Trim();
            mute.Duration = update.ExpiresAtUtc - mute.StartedAt;
        }
        else
        {
            Ban? ban = await database.Bans.SingleOrDefaultAsync(ban => ban.Id == update.Id, cancellationToken);
            if (ban is null) return PipeEnvelope.CreateError(request, "Ban record not found.");
            ban.Reason = update.Reason.Trim();
            ban.Duration = update.ExpiresAtUtc - ban.StartedAt;
        }

        await database.SaveChangesAsync(cancellationToken);
        return PipeEnvelope.CreateResponse(request);
    }

    private static async Task<PipeEnvelope> HandleRemoveModerationAsync(
        PipeEnvelope request, string databasePath, bool isMute, CancellationToken cancellationToken)
    {
        long id = request.DeserializePayload<RemoveModerationRequest>().Id;
        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        int removed = isMute
            ? await database.Mutes.Where(mute => mute.Id == id).ExecuteDeleteAsync(cancellationToken)
            : await database.Bans.Where(ban => ban.Id == id).ExecuteDeleteAsync(cancellationToken);
        return removed == 0
            ? PipeEnvelope.CreateError(request, "Moderation record not found.")
            : PipeEnvelope.CreateResponse(request);
    }
}
