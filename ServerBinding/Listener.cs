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

using System.IO.Pipes;
using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using CbmrWebAdmin.Shared.Requests;
using CCB.Extensions;
using CCB.Internal;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

public static class Listener
{
    public static async Task StartListenAsync(
        ILogger<Metadata> logger,
        NamedPipeServerStream pipeServer,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        while (await PipeProtocol.ReadAsync(pipeServer, cancellationToken) is { } request)
        {
            PipeEnvelope response;
            try
            {
                if (request.Kind != PipeEnvelopeKind.Request)
                {
                    throw new InvalidDataException($"Expected a request envelope, received {request.Kind}.");
                }

                logger.LogInformation("Received Web Admin request: {RequestMessageType}", request.MessageType);

                response = request.MessageType switch
                {
                    PipeMessageType.WhereAreYou => HandleWhereAreYou(request),
                    PipeMessageType.FixElevator => HandleFixElevator(request),
                    PipeMessageType.Players => HandlePlayers(request),
                    PipeMessageType.KickPlayer => HandleKickPlayer(request),
                    PipeMessageType.Broadcast => HandleBroadcast(request),
                    PipeMessageType.Chats => HandleChats(request),
                    PipeMessageType.ClearItems => HandleClearItems(request),
                    PipeMessageType.RestartServer => HandleRestartServer(request),
                    PipeMessageType.BanPlayer => await HandleBanPlayerAsync(request, databasePath, cancellationToken),
                    _ => PipeEnvelope.CreateError(request, $"Unknown message type '{request.MessageType}'.")
                };
            }
            catch (Exception exception)
            {
                response = PipeEnvelope.CreateError(request, exception.Message);
            }

            await PipeProtocol.WriteAsync(pipeServer, response, cancellationToken);
        }
    }

    private static PipeEnvelope HandleWhereAreYou(PipeEnvelope request)
    {
        return PipeEnvelope.CreateResponse(request, new ServerLocation
        {
            Path = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName
        });
    }

    private static PipeEnvelope HandleFixElevator(PipeEnvelope request)
    {
        MainThreadContext.RunOnMainThread(() =>
        {
            Door? door1 = null, door2 = null;
            Room gateAb = GlobalProperties.World.GetRoomByName("gate_a_b");
            if (gateAb.Opaque.Pointer != 0)
            {
                door1 = gateAb.GetDoor(0);
                door2 = gateAb.GetDoor(1);
            }
            else
            {
                Room gateA = GlobalProperties.World.GetRoomByName("gate_a");
                Room gateB = GlobalProperties.World.GetRoomByName("gate_b");
                if (gateA.Opaque.Pointer != 0) door1 = gateA.GetDoor(1);
                if (gateB.Opaque.Pointer != 0) door2 = gateB.GetDoor(1);
            }

            door1?.SetOpen(true);
            door2?.SetOpen(true);
        });

        return PipeEnvelope.CreateResponse(request);
    }

    private static PipeEnvelope HandlePlayers(PipeEnvelope request)
    {
        List<WebPlayer> webPlayers = MainThreadContext.RunOnMainThread(() =>
            Player.List()
                .Select(player => new WebPlayer
                {
                    Index = player.GetIndex(),
                    Name = player.GetName(),
                    SteamId = player.GetSteamID()
                })
                .ToList());

        return PipeEnvelope.CreateResponse(request, webPlayers);
    }

    private static PipeEnvelope HandleKickPlayer(PipeEnvelope request)
    {
        KickPlayerRequest kickPlayerRequest = request.DeserializePayload<KickPlayerRequest>();
        MainThreadContext.RunOnMainThread(() =>
        {
            Player player = Player.List().FirstOrDefault(player => player.GetSteamID() == kickPlayerRequest.SteamId);
            if (player.Opaque.Pointer != 0) player.Kick(9, "");
        });
        return PipeEnvelope.CreateResponse(request);
    }

    private static async Task<PipeEnvelope> HandleBanPlayerAsync(
        PipeEnvelope request, string databasePath, CancellationToken cancellationToken)
    {
        BanPlayerRequest banPlayerRequest = request.DeserializePayload<BanPlayerRequest>();
        DateTime startedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(banPlayerRequest.SteamId) || banPlayerRequest.SteamId.Length > 64)
            throw new InvalidDataException("A SteamID of at most 64 characters is required.");
        if (string.IsNullOrWhiteSpace(banPlayerRequest.Reason))
            throw new InvalidDataException("A ban reason is required.");
        if (banPlayerRequest.ExpiresAtUtc is not null &&
            (banPlayerRequest.ExpiresAtUtc.Value.Kind != DateTimeKind.Utc || banPlayerRequest.ExpiresAtUtc <= startedAt))
            throw new InvalidDataException("The ban expiration must be a future UTC date and time.");

        await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
        Ban ban = new Ban
        {
            SteamId = banPlayerRequest.SteamId.Trim(),
            Reason = banPlayerRequest.Reason.Trim(),
            StartedAt = startedAt,
            Duration = banPlayerRequest.ExpiresAtUtc - startedAt
        };
        database.Bans.Add(ban);
        await database.SaveChangesAsync(cancellationToken);

        MainThreadContext.RunOnMainThread(() =>
        {
            Player player = Player.List().FirstOrDefault(player => player.GetSteamID() == ban.SteamId);
            string playerName = player.Opaque.Pointer != 0
                ? player.GetName()
                : banPlayerRequest.PlayerName ?? ban.SteamId;
            string duration = ban.Duration is null
                ? "永久"
                : $"{ban.Duration.Value.Days}天{ban.Duration.Value.Hours}小时{ban.Duration.Value.Minutes}分钟{ban.Duration.Value.Seconds}秒";

            if (player.Opaque.Pointer != 0)
            {
                player.Kick(9, $"你已被服务器封禁！\n原因：{ban.Reason}\n封禁时长：{duration}");
            }

            GlobalProperties.Chat.Send("&colr[255 165 0]======================================================");
            GlobalProperties.Chat.Send($"&colr[255 165 0]玩家 {playerName.ReplaceLineEndings(" ")} 已被封禁");
            GlobalProperties.Chat.Send($"&colr[255 165 0]SteamID：{ban.SteamId}");
            foreach (string line in ban.Reason.ReplaceLineEndings("\n").Split('\n'))
            {
                GlobalProperties.Chat.Send($"&colr[255 165 0]原因：{line}");
            }
            GlobalProperties.Chat.Send($"&colr[255 165 0]封禁时长：{duration}");
            GlobalProperties.Chat.Send("&colr[255 165 0]======================================================");
        });

        return PipeEnvelope.CreateResponse(request);
    }

    private static PipeEnvelope HandleBroadcast(PipeEnvelope request)
    {
        BroadcastRequest broadcastRequest = request.DeserializePayload<BroadcastRequest>();
        string[] lines = broadcastRequest.Message.Split('\n');
        MainThreadContext.RunOnMainThread(() =>
        {
            GlobalProperties.Chat.Send("&colr[255 165 0]======================================================");
            GlobalProperties.Chat.Send($"&colr[255 165 0]{broadcastRequest.Title}: {lines[0]}");
            if (lines.Length > 1)
            {
                foreach (string line in lines[1..])
                {
                    GlobalProperties.Chat.Send($"&colr[255 165 0]{line}");
                }
            }

            GlobalProperties.Chat.Send("&colr[255 165 0]======================================================");
        });
        return PipeEnvelope.CreateResponse(request);
    }

    private static PipeEnvelope HandleChats(PipeEnvelope request)
    {
        return PipeEnvelope.CreateResponse(request, EntryPoint.ServerChatList.GetItems().ToList());
    }

    private static PipeEnvelope HandleClearItems(PipeEnvelope request)
    {
        MainThreadContext.RunOnMainThread(() =>
        {
            foreach (var item in Items.List().Where(item => !item.IsPicked()))
            {
                item.Remove();
            }
        });
        return PipeEnvelope.CreateResponse(request);
    }
    
    private static PipeEnvelope HandleRestartServer(PipeEnvelope request)
    {
        RestartServerRequest restartServerRequest = request.DeserializePayload<RestartServerRequest>();
        MainThreadContext.RunOnMainThread(() =>
        {
            foreach (Player player in Player.List())
            {
                player.Kick(12, restartServerRequest.Reason);
            }
            GlobalProperties.Server.Restart();
        });
        return PipeEnvelope.CreateResponse(request);
    }
}