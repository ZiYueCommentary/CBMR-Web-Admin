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

using System.Collections.Immutable;
using CbmrWebAdmin.ServerBinding.Data;

namespace CbmrWebAdmin.ServerBinding;

internal sealed class ModerationCache : IDisposable
{
    internal sealed record Entry(long Id, string Reason, DateTime? ExpiresAt)
    {
        public bool IsExpired(DateTime utcNow) => ExpiresAt <= utcNow;
    }

    private ImmutableDictionary<(string SteamId, MuteKind? Kind), ImmutableArray<Entry>> _entries;
    private readonly Timer _expiryTimer;

    public ModerationCache(WebPortalDbContext database)
    {
        DateTime utcNow = DateTime.UtcNow;
        _entries = database.ActiveBans(utcNow).AsEnumerable()
            .Select(ban => (Key: (ban.SteamId, (MuteKind?)null), Value: new Entry(ban.Id, ban.Reason, ban.ExpiresAt)))
            .Concat(database.ActiveMutes(utcNow).AsEnumerable()
                .Select(mute => (Key: (mute.SteamId, (MuteKind?)mute.Kind), Value: new Entry(mute.Id, mute.Reason, mute.ExpiresAt))))
            .GroupBy(pair => pair.Key, pair => pair.Value)
            .ToImmutableDictionary(group => group.Key, group => group.OrderByDescending(entry => entry.Id).ToImmutableArray());

        TimeSpan interval = TimeSpan.FromMinutes(1);
        _expiryTimer = new Timer(_ => PruneExpired(), null, interval, interval);
    }

    public Entry? FindActive(string steamId, MuteKind? kind, DateTime utcNow)
    {
        if (Volatile.Read(ref _entries).TryGetValue((steamId, kind), out var entries))
        {
            foreach (Entry entry in entries)
            {
                if (!entry.IsExpired(utcNow)) return entry;
            }
        }
        return null;
    }

    public void Set(Ban ban) => Set((ban.SteamId, null), new Entry(ban.Id, ban.Reason, ban.ExpiresAt));

    public void Set(Mute mute) => Set((mute.SteamId, mute.Kind), new Entry(mute.Id, mute.Reason, mute.ExpiresAt));

    public void Remove(Ban ban) => Remove((ban.SteamId, null), ban.Id);

    public void Remove(Mute mute) => Remove((mute.SteamId, mute.Kind), mute.Id);

    private void Set((string SteamId, MuteKind? Kind) key, Entry entry)
    {
        if (entry.IsExpired(DateTime.UtcNow))
        {
            Remove(key, entry.Id);
            return;
        }

        ImmutableInterlocked.Update(ref _entries, entries =>
        {
            var group = entries.GetValueOrDefault(key, ImmutableArray<Entry>.Empty);
            var updated = group.RemoveAll(existing => existing.Id == entry.Id).Add(entry);
            return entries.SetItem(key, updated.Sort((left, right) => right.Id.CompareTo(left.Id)));
        });
    }

    private void Remove((string SteamId, MuteKind? Kind) key, long id)
    {
        ImmutableInterlocked.Update(ref _entries, entries =>
        {
            if (!entries.TryGetValue(key, out var group)) return entries;
            var remaining = group.RemoveAll(entry => entry.Id == id);
            return remaining.IsEmpty ? entries.Remove(key) : entries.SetItem(key, remaining);
        });
    }

    private void PruneExpired()
    {
        DateTime utcNow = DateTime.UtcNow;
        ImmutableInterlocked.Update(ref _entries, entries =>
        {
            var result = entries;
            foreach (var (key, group) in entries)
            {
                var remaining = group.RemoveAll(entry => entry.IsExpired(utcNow));
                if (remaining == group) continue;
                result = remaining.IsEmpty ? result.Remove(key) : result.SetItem(key, remaining);
            }
            return result;
        });
    }

    public void Dispose() => _expiryTimer.Dispose();
}
