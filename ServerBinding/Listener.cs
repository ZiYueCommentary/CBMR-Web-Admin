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
using CbmrWebAdmin.Shared;
using CCB.Extensions;
using CCB.Internal;

namespace CbmrWebAdmin.ServerBinding;

public static class Listener
{
    public static async Task StartListenAsync(NamedPipeServerStream pipeServer)
    {
        while (await PipeProtocol.ReadAsync(pipeServer) is { } request)
        {
            PipeEnvelope response;
            try
            {
                if (request.Kind != PipeEnvelopeKind.Request)
                {
                    throw new InvalidDataException($"Expected a request envelope, received {request.Kind}.");
                }

                response = request.MessageType switch
                {
                    PipeMessageType.Ack => HandleAck(request),
                    PipeMessageType.FixElevator => HandleFixElevator(request),
                    PipeMessageType.Players => HandlePlayers(request),
                    _ => PipeEnvelope.CreateError(request, $"Unknown message type '{request.MessageType}'.")
                };
            }
            catch (Exception exception)
            {
                response = PipeEnvelope.CreateError(request, exception.Message);
            }

            await PipeProtocol.WriteAsync(pipeServer, response);
        }
    }

    private static PipeEnvelope HandleAck(PipeEnvelope request)
    {
        MainThreadContext.RunOnMainThread(() => GlobalProperties.Chat.Send("Hello World!"));
        return PipeEnvelope.CreateResponse(request);
    }

    private static PipeEnvelope HandleFixElevator(PipeEnvelope request)
    {
        MainThreadContext.RunOnMainThread(() =>
        {
            Door? door1 = null, door2 = null;
            Room gateAb = GlobalProperties.World.GetRoomByName("gate_a_b");
            if (gateAb.Handle.Pointer != 0)
            {
                door1 = gateAb.GetDoor(0);
                door2 = gateAb.GetDoor(1);
            }
            else
            {
                Room gateA = GlobalProperties.World.GetRoomByName("gate_a");
                Room gateB = GlobalProperties.World.GetRoomByName("gate_b");
                if (gateA.Handle.Pointer != 0) door1 = gateA.GetDoor(1);
                if (gateB.Handle.Pointer != 0) door2 = gateB.GetDoor(1);
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
}