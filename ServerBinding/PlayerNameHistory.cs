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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

internal sealed class PlayerNameHistory : IDisposable
{
    private readonly Channel<PlayerNameObservation> _observations =
        Channel.CreateUnbounded<PlayerNameObservation>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ILogger<Metadata> _logger;
    private readonly Task _writer;

    public PlayerNameHistory(string databasePath, ILogger<Metadata> logger)
    {
        _logger = logger;
        _writer = Task.Run(() => WriteAsync(databasePath));
    }

    public void Observe(string steamId, string name)
    {
        if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(name)) return;

        _observations.Writer.TryWrite(new PlayerNameObservation
        {
            SteamId = steamId,
            Name = name,
            FirstSeenAtUtc = DateTime.UtcNow
        });
    }

    private async Task WriteAsync(string databasePath)
    {
        await foreach (PlayerNameObservation observation in _observations.Reader.ReadAllAsync())
        {
            try
            {
                await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
                await database.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "player_name_history" ("steamid", "name", "first_seen_at_utc")
                    SELECT {observation.SteamId}, {observation.Name}, {observation.FirstSeenAtUtc}
                    WHERE {observation.Name} IS NOT (
                        SELECT "name" FROM "player_name_history"
                        WHERE "steamid" = {observation.SteamId}
                        ORDER BY "id" DESC LIMIT 1
                    )
                    """);
            }
            catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
            {
                _logger.LogError(exception, "Unable to save a player name observation.");
            }
        }
    }

    public void Dispose()
    {
        _observations.Writer.TryComplete();
        if (!_writer.Wait(TimeSpan.FromSeconds(5)))
            _logger.LogWarning("The player name history writer is still draining queued observations.");
    }
}
