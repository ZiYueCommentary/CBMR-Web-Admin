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
using CCB.Extensions;
using CCB.Internal;

namespace CbmrWebAdmin.ServerBinding;

public static class Listener
{
    public static void StartListen(NamedPipeServerStream pipeServer)
    {
        using StreamReader reader = new StreamReader(pipeServer);
        using StreamWriter writer = new StreamWriter(pipeServer);
        writer.AutoFlush = true;
        string? request;
        while ((request = reader.ReadLine()) is not null)
        {
            switch (request)
            {
                case "ACK":
                {
                    MainThreadContext.RunOnMainThread(() => GlobalProperties.Chat.Send("Hello World!"));
                    writer.WriteLine("OK");
                    break;
                }
                case "FIXELEV":
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
                    writer.WriteLine("OK");
                    break;
                }
            }
            // writer.WriteLine();
        }
    }
}