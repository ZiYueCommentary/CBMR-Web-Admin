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

using CCB.Internal;
using CbmrWebAdmin.ServerBinding.Data;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

public partial class EntryPoint
{
    private const int AnnouncementDialog = 28403;
    private readonly Dictionary<string, Queue<long>> _announcementQueues = new(StringComparer.Ordinal);

    private void DeliverAnnouncements(string steamId, Queue<long> queue, CancellationToken token)
    {
        if (token.IsCancellationRequested ||
            !_announcementQueues.TryGetValue(steamId, out var current) || !ReferenceEquals(current, queue)) return;
        using WebPortalDbContext database = WebPortalDbContext.Create(_config.DatabasePath);
        DateTime now = DateTime.UtcNow;
        var ids = database.Announcements
            .Where(item => item.ExpiresAtUtc > now &&
                !database.AnnouncementReads.Any(read => read.SteamId == steamId &&
                    read.AnnouncementId == item.Id && read.Revision == item.Revision))
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id).Select(item => item.Id).ToList();
        foreach (long id in ids) queue.Enqueue(id);
        ShowNextAnnouncement(steamId);
    }

    private void OnAnnouncementDialogAction(EventRegistry.PlayerDialogActionEventArg args)
    {
        if (args.Index != AnnouncementDialog) return;
        ShowNextAnnouncement(args.Player.GetSteamID());
    }

    private void ShowNextAnnouncement(string steamId)
    {
        if (!_announcementQueues.TryGetValue(steamId, out var queue)) return;
        try
        {
            Player player = Player.List().FirstOrDefault(player => player.GetSteamID() == steamId);
            if (player.Opaque.Pointer == 0)
            {
                _announcementQueues.Remove(steamId);
                return;
            }
            using WebPortalDbContext database = WebPortalDbContext.Create(_config.DatabasePath);
            while (queue.TryDequeue(out long id))
            {
                Announcement? announcement = database.Announcements.Find(id);
                if (announcement is null || announcement.ExpiresAtUtc <= DateTime.UtcNow) continue;
                AnnouncementRead? read = database.AnnouncementReads.Find(steamId, id);
                if (read?.Revision == announcement.Revision) continue;
                player.ShowDialog(MessageDialogType, AnnouncementDialog, announcement.Title, announcement.Content,
                    PlayerMessages.Get(_playerLanguage.Culture, "AnnouncementOK"), "");
                if (read is null)
                {
                    read = new AnnouncementRead { SteamId = steamId, AnnouncementId = id };
                    database.AnnouncementReads.Add(read);
                }
                read.Revision = announcement.Revision;
                read.PlayerName = player.GetName();
                read.ReadAtUtc = DateTime.UtcNow;
                database.SaveChanges();
                return;
            }
            _announcementQueues.Remove(steamId);
        }
        catch (Exception exception)
        {
            _announcementQueues.Remove(steamId);
            logger.LogError(exception, "Could not show announcement to player {SteamId}.", steamId);
        }
    }
}
