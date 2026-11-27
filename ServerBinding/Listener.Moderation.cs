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
        List<Ban> bans = await database.ActiveBans(utcNow).OrderByDescending(ban => ban.Id).ToListAsync(cancellationToken);
        return PipeEnvelope.CreateResponse(request, bans
            .Select(ban => new BanRecord
            {
                Id = ban.Id,
                SteamId = ban.SteamId,
                Reason = ban.Reason,
                StartedAtUtc = DateTime.SpecifyKind(ban.StartedAt, DateTimeKind.Utc),
                ExpiresAtUtc = ban.ExpiresAt
            }).ToList());
    }

    private static async Task<PipeEnvelope> HandleListMutesAsync(
        PipeEnvelope request, string databasePath, CancellationToken cancellationToken)
    {
        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        DateTime utcNow = DateTime.UtcNow;
        IQueryable<Mute> query = request.MessageType switch
        {
            PipeMessageType.ListExpiredMutes => database.ExpiredMutes(utcNow),
            PipeMessageType.ListRevokedMutes => database.RevokedMutes(),
            _ => database.ActiveMutes(utcNow)
        };
        List<Mute> mutes = await query.OrderByDescending(mute => mute.Id).ToListAsync(cancellationToken);
        return PipeEnvelope.CreateResponse(request, mutes
            .Select(mute => new MuteRecord
            {
                Id = mute.Id,
                SteamId = mute.SteamId,
                Reason = mute.Reason,
                StartedAtUtc = DateTime.SpecifyKind(mute.StartedAt, DateTimeKind.Utc),
                ExpiresAtUtc = mute.ExpiresAt,
                Kind = mute.Kind == MuteKind.Voice ? MuteRecordKind.Voice : MuteRecordKind.Chat,
                RevokedAtUtc = mute.RevokedAt
            }).ToList());
    }

    private static async Task<PipeEnvelope> HandleUpdateModerationAsync(
        PipeEnvelope request, string databasePath, ModerationCache moderationCache, bool isMute, CancellationToken cancellationToken)
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
            if (mute.RevokedAt is not null) return PipeEnvelope.CreateError(request, PipeEnvelope.ModerationRevokedError);
            mute.Reason = update.Reason.Trim();
            mute.Duration = update.ExpiresAtUtc - mute.StartedAt;
            mute.ExpiresAt = update.ExpiresAtUtc;
            await database.SaveChangesAsync(cancellationToken);
            moderationCache.Set(mute);
        }
        else
        {
            Ban? ban = await database.Bans.SingleOrDefaultAsync(ban => ban.Id == update.Id, cancellationToken);
            if (ban is null) return PipeEnvelope.CreateError(request, "Ban record not found.");
            if (ban.RevokedAt is not null) return PipeEnvelope.CreateError(request, PipeEnvelope.ModerationRevokedError);
            if (ban.ExpiresAt <= DateTime.UtcNow) return PipeEnvelope.CreateError(request, PipeEnvelope.BanExpiredError);
            ban.Reason = update.Reason.Trim();
            ban.Duration = update.ExpiresAtUtc - ban.StartedAt;
            ban.ExpiresAt = update.ExpiresAtUtc;
            await database.SaveChangesAsync(cancellationToken);
            moderationCache.Set(ban);
        }

        return PipeEnvelope.CreateResponse(request);
    }

    private static async Task<PipeEnvelope> HandleRemoveModerationAsync(
        PipeEnvelope request, string databasePath, ModerationCache moderationCache, bool isMute, CancellationToken cancellationToken)
    {
        long id = request.DeserializePayload<RemoveModerationRequest>().Id;
        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        if (isMute)
        {
            Mute? mute = await database.Mutes.SingleOrDefaultAsync(mute => mute.Id == id, cancellationToken);
            if (mute is null) return PipeEnvelope.CreateError(request, "Moderation record not found.");
            if (mute.RevokedAt is not null) return PipeEnvelope.CreateError(request, PipeEnvelope.ModerationRevokedError);
            mute.RevokedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
            moderationCache.Remove(mute);
        }
        else
        {
            Ban? ban = await database.Bans.SingleOrDefaultAsync(ban => ban.Id == id, cancellationToken);
            if (ban is null) return PipeEnvelope.CreateError(request, "Moderation record not found.");
            if (ban.RevokedAt is not null) return PipeEnvelope.CreateError(request, PipeEnvelope.ModerationRevokedError);
            DateTime utcNow = DateTime.UtcNow;
            if (ban.ExpiresAt <= utcNow) return PipeEnvelope.CreateError(request, PipeEnvelope.BanExpiredError);
            ban.RevokedAt = utcNow;
            await database.SaveChangesAsync(cancellationToken);
            moderationCache.Remove(ban);
        }
        return PipeEnvelope.CreateResponse(request);
    }
}
