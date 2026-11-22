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


namespace CbmrWebAdmin.WebPortal.Data;

public static class AuditActions
{
    public const string ReplyReport = "ReplyReport";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string LockedOut = "LockedOut";
    public const string Logout = "Logout";
    public const string FixElevator = "FixElevator";
    public const string ClearItems = "ClearItems";
    public const string Broadcast = "Broadcast";
    public const string KickPlayer = "KickPlayer";
    public const string BanPlayer = "BanPlayer";
    public const string UnbanPlayer = "UnbanPlayer";
    public const string UnmutePlayer = "UnmutePlayer";
    public const string ModifyBan = "ModifyBan";
    public const string ModifyMute = "ModifyMute";
    public const string MuteVoice = "MuteVoice";
    public const string MuteChat = "MuteChat";
    public const string RestartServer = "RestartServer";
    public const string ForceRestartServer = "ForceRestartServer";
    public const string SetPlayerLanguage = "SetPlayerLanguage";
    public const string CreateAccount = "CreateAccount";
    public const string UpdateAccount = "UpdateAccount";
    public const string DeleteAccount = "DeleteAccount";
}
