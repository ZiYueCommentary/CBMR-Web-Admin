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
using CCB.Extensions;
using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.ServerBinding;

internal static class ReportReplyDelivery
{
    private static readonly SemaphoreSlim DeliveryLock = new(1, 1);

    internal static async Task<bool> DeliverAsync(string path, string steamId, PlayerLanguageSettings language,
        CancellationToken token, long? reportId = null)
    {
        await DeliveryLock.WaitAsync(token);
        try
        {
            await using WebPortalDbContext database = WebPortalDbContext.Create(path);
            var query = database.Reports.Where(report => report.ReporterSteamId == steamId && report.Handled && report.Reply != null);
            if (reportId is { } id) query = query.Where(report => report.Id == id);
            else query = query.Where(report => report.ReplyDeliveredAtUtc == null);
            var reports = await query.OrderBy(report => report.Id).ToListAsync(token);
            bool delivered = false;
            foreach (Report report in reports)
            {
                if (report.ReplyDeliveredAtUtc is not null)
                {
                    delivered = true;
                    continue;
                }
                bool online = MainThreadContext.RunOnMainThread(() =>
                {
                    if (token.IsCancellationRequested) return false;
                    Player player = Player.List().FirstOrDefault(player => player.GetSteamID() == steamId);
                    if (player.Opaque.Pointer == 0) return false;
                    string admin = (report.RepliedBy ?? "").ReplaceLineEndings(" ");
                    string reply = report.Reply!.ReplaceLineEndings(" ");
                    string message = report.Accepted is { } accepted
                        ? PlayerMessages.Get(language.Culture, accepted ? "ReportAcceptedReply" : "ReportRejectedReply",
                            report.ReportedName.ReplaceLineEndings(" "), admin, reply)
                        : PlayerMessages.Get(language.Culture, "ReportReply", admin, reply);
                    GlobalProperties.Chat.SendPlayer(player, message);
                    return true;
                });
                if (!online) break;
                report.ReplyDeliveredAtUtc = DateTime.UtcNow;
                // Persist after sending even if unload was requested in the meantime.
                await database.SaveChangesAsync(CancellationToken.None);
                delivered = true;
                EntryPoint.ChatEvents.PublishEvent(PipeEnvelope.CreateEvent(PipeMessageType.ReportsUpdated));
            }
            return delivered;
        }
        finally { DeliveryLock.Release(); }
    }
}
