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

using CCB.Internal;
using CbmrWebAdmin.ServerBinding.Data;
using CbmrWebAdmin.Shared;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace CbmrWebAdmin.ServerBinding;

public partial class EntryPoint
{
    private const int ReportPlayerDialog = 28401;
    private const int ReportContentDialog = 28402;
    private const int ListDialogType = 1;
    private const int MessageDialogType = 0;
    private const int InputDialogType = 2;
    private readonly PerPlayerCooldown _reportCooldown = new(30_000);
    private readonly Dictionary<string, ReportForm> _reportForms = new(StringComparer.Ordinal);

    private sealed record ReportTarget(string SteamId, string Name);
    private sealed class ReportForm(Player player, List<ReportTarget> targets)
    {
        public Player Player { get; } = player;
        public List<ReportTarget> Targets { get; } = targets;
        public ReportTarget? Selected { get; set; }
    }

    private void ReportNotice(Player player, string key) =>
        GlobalProperties.Chat.SendPlayer(player, PlayerMessages.Get(_playerLanguage.Culture, key));

    private bool TryHandleReport(EventRegistry.PlayerChatEventArg args)
    {
        string text = args.Text.Trim();
        if (!text.Equals("/report", StringComparison.OrdinalIgnoreCase) &&
            !text.StartsWith("/report ", StringComparison.OrdinalIgnoreCase)) return false;
        args.EventResult = false;
        if (!text.Equals("/report", StringComparison.OrdinalIgnoreCase))
        {
            ReportNotice(args.Player, "ReportUsage");
            return true;
        }
        string steamId = args.Player.GetSteamID();
        CloseReportForm(steamId);
        var targets = Player.List().Where(player => player.GetSteamID() != steamId)
            .Select(player => new ReportTarget(player.GetSteamID(), player.GetName())).ToList();
        if (targets.Count == 0)
        {
            ReportNotice(args.Player, "ReportPlayerNotFound");
            return true;
        }
        _reportForms[steamId] = new ReportForm(args.Player, targets);
        var culture = _playerLanguage.Culture;
        args.Player.ShowDialog(ListDialogType, ReportPlayerDialog,
            PlayerMessages.Get(culture, "ReportSelectPlayer"),
            string.Join("\n", targets.Select(target => target.Name.ReplaceLineEndings(" "))),
            PlayerMessages.Get(culture, "ReportNext"), PlayerMessages.Get(culture, "ReportCancel"));
        return true;
    }

    private void OnReportDialogAction(EventRegistry.PlayerDialogActionEventArg args)
    {
        if (args.Index is not (ReportPlayerDialog or ReportContentDialog)) return;
        string steamId = args.Player.GetSteamID();
        if (!_reportForms.TryGetValue(steamId, out ReportForm? form)) return;
        if (!args.Response)
        {
            CloseReportForm(steamId);
            return;
        }
        if (args.Index == ReportPlayerDialog)
        {
            if (form.Selected is not null || args.Selecteditem < 0 || args.Selecteditem >= form.Targets.Count)
            {
                CloseReportForm(steamId);
                return;
            }
            form.Selected = form.Targets[args.Selecteditem];
            if (!Player.List().Any(player => player.GetSteamID() == form.Selected.SteamId))
            {
                CloseReportForm(steamId);
                ReportNotice(args.Player, "ReportPlayerNotFound");
                return;
            }
            ShowReportContent(form);
            return;
        }
        if (form.Selected is not { } target) return;
        string reason = args.Input.Trim();
        if (reason.Length == 0 || reason.Length > 2000)
        {
            ReportNotice(args.Player, "ReportInvalidContent");
            ShowReportContent(form);
            return;
        }
        if (!_reportCooldown.TryStart(steamId))
        {
            CloseReportForm(steamId);
            ReportNotice(args.Player, "ReportCooldown");
            return;
        }
        CloseReportForm(steamId);
        try
        {
            using WebPortalDbContext database = WebPortalDbContext.Create(_config.DatabasePath);
            Report report = new()
            {
                ReporterSteamId = steamId, ReporterName = args.Player.GetName(),
                ReportedSteamId = target.SteamId, ReportedName = target.Name,
                Content = reason, CreatedAtUtc = DateTime.UtcNow
            };
            database.Reports.Add(report);
            database.SaveChanges();
            ChatEvents.PublishEvent(new PipeEnvelope
            {
                Version = PipeEnvelope.CurrentVersion, RequestId = Guid.NewGuid(), Kind = PipeEnvelopeKind.Event,
                MessageType = PipeMessageType.ReportCreated,
                Payload = MessagePackSerializer.Serialize(Listener.ToRecord(report))
            });
            ReportNotice(args.Player, "ReportSubmitted");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not save player report.");
            ReportNotice(args.Player, "ReportFailed");
        }
    }

    private void ShowReportContent(ReportForm form)
    {
        var culture = _playerLanguage.Culture;
        form.Player.ShowDialog(InputDialogType, ReportContentDialog,
            PlayerMessages.Get(culture, "ReportContentTitle", form.Selected!.Name.ReplaceLineEndings(" ")),
            PlayerMessages.Get(culture, "ReportContentHint"),
            PlayerMessages.Get(culture, "ReportSubmit"), PlayerMessages.Get(culture, "ReportCancel"));
    }

    private void CloseReportForm(string steamId)
    {
        if (_reportForms.Remove(steamId, out ReportForm? form)) form.Player.HideDialog();
    }
}
