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

internal sealed class MuteAttemptLog : IDisposable
{
    private const int BatchSize = 256;
    private const long VoiceLogIntervalMs = 10_000;
    private readonly Dictionary<string, long> _voiceLogDeadlines = new(StringComparer.Ordinal);
    private long _nextVoiceLogCleanup;
    private readonly Channel<MuteAttempt> _attempts = Channel.CreateUnbounded<MuteAttempt>(new UnboundedChannelOptions
    {
        SingleReader = true
    });
    private readonly ILogger<Metadata> _logger;
    private readonly Task _writer;
    private volatile bool _stopping;

    public MuteAttemptLog(string databasePath, ILogger<Metadata> logger)
    {
        _logger = logger;
        _writer = Task.Run(() => WriteAsync(databasePath));
    }

    public void Enqueue(MuteAttempt attempt) => _attempts.Writer.TryWrite(attempt);

    public bool ShouldLogVoiceAttempt(string steamId)
    {
        long now = Environment.TickCount64;
        if (now >= _nextVoiceLogCleanup)
        {
            foreach (var (playerId, expiresAt) in _voiceLogDeadlines)
            {
                if (now >= expiresAt) _voiceLogDeadlines.Remove(playerId);
            }
            _nextVoiceLogCleanup = now + VoiceLogIntervalMs;
        }

        if (_voiceLogDeadlines.TryGetValue(steamId, out long deadline) && now < deadline)
            return false;

        _voiceLogDeadlines[steamId] = now + VoiceLogIntervalMs;
        return true;
    }

    private async Task WriteAsync(string databasePath)
    {
        List<MuteAttempt> batch = new(BatchSize);
        while (await _attempts.Reader.WaitToReadAsync())
        {
            if (!_stopping) await Task.Delay(TimeSpan.FromMilliseconds(100));

            while (_attempts.Reader.TryRead(out MuteAttempt? attempt))
            {
                batch.Add(attempt);
                while (batch.Count < BatchSize && _attempts.Reader.TryRead(out attempt)) batch.Add(attempt);
                if (!await SaveBatchAsync(databasePath, batch)) return;
                batch.Clear();
            }
        }
    }

    private async Task<bool> SaveBatchAsync(string databasePath, List<MuteAttempt> batch)
    {
        bool reportedFailure = false;
        while (true)
        {
            try
            {
                await using WebPortalDbContext database = WebPortalDbContext.Create(databasePath);
                database.MuteAttempts.AddRange(batch);
                await database.SaveChangesAsync();
                return true;
            }
            catch (Exception exception) when (exception is DbUpdateException or SqliteException or IOException or UnauthorizedAccessException)
            {
                if (_stopping)
                {
                    _logger.LogError(exception, "Unable to save queued mute attempts before unloading.");
                    return false;
                }
                if (!reportedFailure)
                {
                    _logger.LogWarning(exception, "Unable to save mute attempts; retrying the queued batch.");
                    reportedFailure = true;
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _attempts.Writer.TryComplete();
        if (!_writer.Wait(TimeSpan.FromSeconds(5)))
            _logger.LogWarning("The mute-attempt writer is still draining queued attempts.");
    }
}
