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
    private static AnnouncementRecord ToRecord(Announcement announcement) => new()
    {
        Id = announcement.Id, Title = announcement.Title, Content = announcement.Content,
        ExpiresAtUtc = DateTime.SpecifyKind(announcement.ExpiresAtUtc, DateTimeKind.Utc),
        CreatedAtUtc = DateTime.SpecifyKind(announcement.CreatedAtUtc, DateTimeKind.Utc),
        UpdatedAtUtc = DateTime.SpecifyKind(announcement.UpdatedAtUtc, DateTimeKind.Utc),
        CreatedBy = announcement.CreatedBy, Revision = announcement.Revision
    };

    private static bool ValidAnnouncement(string title, string content, DateTime expiry) =>
        !string.IsNullOrWhiteSpace(title) && title.Length <= 200 &&
        !string.IsNullOrWhiteSpace(content) && content.Length <= 10000 &&
        expiry.Kind == DateTimeKind.Utc && expiry > DateTime.UtcNow;

    private static async Task<PipeEnvelope> HandleListAnnouncementsAsync(PipeEnvelope request, string path, CancellationToken token)
    {
        await using WebPortalDbContext database = WebPortalDbContext.Create(path);
        var announcements = await database.Announcements.AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id).ToListAsync(token);
        return PipeEnvelope.CreateResponse(request, announcements.Select(ToRecord).ToList());
    }

    private static async Task<PipeEnvelope> HandleCreateAnnouncementAsync(PipeEnvelope request, string path, CancellationToken token)
    {
        var input = request.DeserializePayload<CreateAnnouncementRequest>();
        if (!ValidAnnouncement(input.Title, input.Content, input.ExpiresAtUtc) ||
            string.IsNullOrWhiteSpace(input.AdminName) || input.AdminName.Length > 256)
            return PipeEnvelope.CreateError(request, "Invalid announcement.");
        await using WebPortalDbContext database = WebPortalDbContext.Create(path);
        Announcement announcement = new()
        {
            Title = input.Title.Trim(), Content = input.Content.Trim(), ExpiresAtUtc = input.ExpiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            CreatedBy = input.AdminName.Trim(), Revision = 1
        };
        database.Announcements.Add(announcement);
        await database.SaveChangesAsync(token);
        return PipeEnvelope.CreateResponse(request, ToRecord(announcement));
    }

    private static async Task<PipeEnvelope> HandleUpdateAnnouncementAsync(PipeEnvelope request, string path, CancellationToken token)
    {
        var input = request.DeserializePayload<UpdateAnnouncementRequest>();
        if (!ValidAnnouncement(input.Title, input.Content, input.ExpiresAtUtc))
            return PipeEnvelope.CreateError(request, "Invalid announcement.");
        await using WebPortalDbContext database = WebPortalDbContext.Create(path);
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        Announcement? announcement = await database.Announcements.SingleOrDefaultAsync(item => item.Id == input.Id, token);
        if (announcement is null || announcement.ExpiresAtUtc <= DateTime.UtcNow)
            return PipeEnvelope.CreateError(request, "Announcement not found or already expired.");
        announcement.Title = input.Title.Trim();
        announcement.Content = input.Content.Trim();
        announcement.ExpiresAtUtc = input.ExpiresAtUtc;
        announcement.UpdatedAtUtc = DateTime.UtcNow;
        if (input.ResendToReaders) announcement.Revision = checked(announcement.Revision + 1);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return PipeEnvelope.CreateResponse(request, ToRecord(announcement));
    }
}
