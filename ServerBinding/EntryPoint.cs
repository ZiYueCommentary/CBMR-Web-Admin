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
using CCB.Abstractions;
using CCB.Attributes;
using CCB.Internal;
using Microsoft.Extensions.Logging;
using CbmrWebAdmin.ServerBinding.Data;

namespace CbmrWebAdmin.ServerBinding;

[Injectable]
public class EntryPoint(ILogger<Metadata> logger, IConfigProvider<Config> configProvider) : ILoad, IUnload
{
    private readonly Config _config = configProvider.GetConfig();
    private PlayerLanguageSettings _playerLanguage = null!;
    private ModerationCache _moderationCache = null!;
    private CancellationTokenSource? _listenerCancellation;
    private Thread? _listenerThread;
    private NamedPipeServerStream? _pipeServer;
    private const string PipeName = "CbmrWebAdmin";
    private Task? _eventsTask;
    internal static readonly ChatEventPublisher ChatEvents = new();
    internal static readonly ServerChatList ServerChatList = new();

    public void Load()
    {
        _playerLanguage = new PlayerLanguageSettings(_config.DatabasePath);
        if (!_playerLanguage.Load())
            logger.LogWarning("Saved player language is invalid; using the default {Culture}.", _playerLanguage.Culture.Name);
        using (WebPortalDbContext database = WebPortalDbContext.Create(_config.DatabasePath))
        {
            database.Initialize();
            _moderationCache = new ModerationCache(database);
        }

        _listenerCancellation = new CancellationTokenSource();
        _eventsTask = ChatEvents.RunAsync(PipeName, logger, _listenerCancellation.Token);
        _listenerThread = new Thread(() =>
                ListenForConnectionsAsync(_listenerCancellation.Token).GetAwaiter().GetResult())
        {
            IsBackground = true,
            Name = "CbmrWebAdmin.ServerBinding pipe listener"
        };
        _listenerThread.Start();
        EventRegistry.PlayerChat += OnPlayerChat;
        EventRegistry.PlayerVoice += OnPlayerVoice;
        EventRegistry.IncomingConnection += OnConnectionInComing;
    }

    public void Unload()
    {
        _listenerCancellation?.Cancel();
        _pipeServer?.Dispose();
        _listenerThread?.Join(TimeSpan.FromSeconds(5));
        _eventsTask?.GetAwaiter().GetResult();
        _listenerCancellation?.Dispose();
        EventRegistry.PlayerChat -= OnPlayerChat;
        EventRegistry.PlayerVoice -= OnPlayerVoice;
        EventRegistry.IncomingConnection -= OnConnectionInComing;
        _moderationCache?.Dispose();
        ServerChatList.Clear();
    }

    private void OnConnectionInComing(EventRegistry.IncomingConnectionEventArg eventArg)
    {
        DateTime utcNow = DateTime.UtcNow;
        ModerationCache.Entry? ban = _moderationCache.FindActive(eventArg.Conn.GetSteamID(), null, utcNow);
        if (ban is null)
        {
            eventArg.Conn.Accept();
            return;
        }
        var culture = _playerLanguage.Culture;
        string remaining = PlayerMessages.Get(culture, "Permanent");
        if (ban.ExpiresAt is { } expiresAt)
        {
            TimeSpan timeLeft = expiresAt - utcNow;
            remaining = PlayerMessages.Get(culture, "RemainingDuration", timeLeft.Days, timeLeft.Hours, timeLeft.Minutes);
        }
        eventArg.Conn.Cancel(PlayerMessages.Get(culture, "BanRejected", ban.Reason, remaining));
    }

    private void OnPlayerChat(EventRegistry.PlayerChatEventArg eventArg)
    {
        if (_moderationCache.FindActive(eventArg.Player.GetSteamID(), MuteKind.Chat, DateTime.UtcNow) is not null)
        {
            eventArg.EventResult = false;
            return;
        }
        ServerChatList.Add(eventArg.Player, eventArg.Text);
    }

    private void OnPlayerVoice(EventRegistry.PlayerVoiceEventArg eventArg)
    {
        if (_moderationCache.FindActive(eventArg.Player.GetSteamID(), MuteKind.Voice, DateTime.UtcNow) is not null)
            eventArg.EventResult = false;
    }

    private async Task ListenForConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using NamedPipeServerStream pipeServer = new NamedPipeServerStream(
                PipeName, //todo dyna
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            _pipeServer = pipeServer;

            try
            {
                await pipeServer.WaitForConnectionAsync(cancellationToken);
                logger.LogInformation("Server connected to Web Portal successfully.");
                await Listener.StartListenAsync(logger, pipeServer, _config.DatabasePath, _playerLanguage, _moderationCache, cancellationToken);
                logger.LogInformation("Web Portal disconnected; waiting for a new pipe connection.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Pipe connection was damaged; waiting for a new connection.");
            }
            finally
            {
                if (ReferenceEquals(_pipeServer, pipeServer))
                {
                    _pipeServer = null;
                }
            }
        }
    }
}