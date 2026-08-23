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

using CbmrWebAdmin.Shared;
using CCB.Internal;

namespace CbmrWebAdmin.ServerBinding;

public class ServerChatList
{
    private readonly Queue<WebChat> _queue = [];
    private const int Size = 50;

    public void Add(Player player, string message)
    {
        _queue.Enqueue(new WebChat{SteamId = player.GetSteamID(), PlayerName = player.GetName(), Message = message, When = DateTime.Now});
        while (_queue.Count > Size) _queue.Dequeue();
    }

    public void Clear()
    {
        _queue.Clear();
    }

    public IEnumerable<WebChat> GetItems()
    {
        return _queue;
    }
}