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

namespace CbmrWebAdmin.WebPortal;

public sealed class ServerEvents(ILogger<ServerEvents> logger)
{
    private int _isConnected;

    public bool IsConnected
    {
        get => Volatile.Read(ref _isConnected) != 0;
        internal set => Volatile.Write(ref _isConnected, value ? 1 : 0);
    }

    public event Action? ChatUpdated;
    public event Action<CbmrWebAdmin.Shared.ReportRecord?>? ReportsUpdated;

    internal void NotifyReportsUpdated(CbmrWebAdmin.Shared.ReportRecord? report = null)
    {
        if (ReportsUpdated is not { } handlers) return;
        foreach (Action<CbmrWebAdmin.Shared.ReportRecord?> handler in handlers.GetInvocationList())
        {
            try { handler(report); }
            catch (Exception exception) { logger.LogDebug(exception, "Report subscriber failed."); }
        }
    }

    internal void NotifyChatUpdated()
    {
        if (ChatUpdated is not { } handlers) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Chat update subscriber failed.");
            }
        }
    }
}
