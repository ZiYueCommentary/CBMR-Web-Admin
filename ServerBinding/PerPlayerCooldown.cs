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

namespace CbmrWebAdmin.ServerBinding;

internal sealed class PerPlayerCooldown(long intervalMs)
{
    private readonly Dictionary<string, long> _deadlines = new(StringComparer.Ordinal);
    private long _nextCleanup;

    public bool TryStart(string steamId)
    {
        long now = Environment.TickCount64;
        if (now >= _nextCleanup)
        {
            foreach (var (playerId, expiresAt) in _deadlines)
            {
                if (now >= expiresAt) _deadlines.Remove(playerId);
            }
            _nextCleanup = now + intervalMs;
        }

        if (_deadlines.TryGetValue(steamId, out long deadline) && now < deadline)
            return false;

        _deadlines[steamId] = now + intervalMs;
        return true;
    }
}
