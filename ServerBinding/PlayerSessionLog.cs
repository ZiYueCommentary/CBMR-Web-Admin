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

using System.Threading.Channels;
using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

internal sealed class PlayerSessionLog : IDisposable
{
    private readonly Channel<PlayerSessionEntry> _entries =
        Channel.CreateUnbounded<PlayerSessionEntry>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ILogger<Metadata> _logger;
    private readonly Task _writer;

    public PlayerSessionLog(string databasePath, ILogger<Metadata> logger)
    {
        _logger = logger;
        _writer = Task.Run(() => WriteAsync(databasePath));
    }

    public void Enqueue(PlayerSessionEvent sessionEvent, string steamId, string name, string ipAddress)
    {
        _entries.Writer.TryWrite(new PlayerSessionEntry
        {
            SteamId = steamId,
            Name = name,
            IpAddress = ipAddress,
            Event = sessionEvent,
            OccurredAtUtc = DateTime.UtcNow
        });
    }

    private async Task WriteAsync(string databasePath)
    {
        await foreach (PlayerSessionEntry entry in _entries.Reader.ReadAllAsync())
        {
            try
            {
                await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
                database.PlayerSessions.Add(entry);
                await database.SaveChangesAsync();
            }
            catch (Exception exception) when (exception is DbUpdateException or SqliteException or IOException or UnauthorizedAccessException)
            {
                _logger.LogError(exception, "Unable to save a player session event.");
            }
        }
    }

    public void Dispose()
    {
        _entries.Writer.TryComplete();
        if (!_writer.Wait(TimeSpan.FromSeconds(5)))
            _logger.LogWarning("The player session log writer is still draining queued events.");
    }
}
