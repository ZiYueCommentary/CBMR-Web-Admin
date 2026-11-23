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

using MessagePack;

namespace CbmrWebAdmin.Shared;

[MessagePackObject]
public sealed class ReportRecord
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public string ReporterSteamId { get; set; } = "";
    [Key(2)] public string ReporterName { get; set; } = "";
    [Key(3)] public string ReportedSteamId { get; set; } = "";
    [Key(4)] public string ReportedName { get; set; } = "";
    [Key(5)] public string Content { get; set; } = "";
    [Key(6)] public DateTime CreatedAtUtc { get; set; }
    [Key(7)] public bool Handled { get; set; }
    [Key(8)] public DateTime? HandledAtUtc { get; set; }
    [Key(9)] public string? Reply { get; set; }
    [Key(10)] public string? RepliedBy { get; set; }
    [Key(11)] public DateTime? ReplyDeliveredAtUtc { get; set; }
    [Key(12)] public bool? Accepted { get; set; }
}

[MessagePackObject]
public sealed class ReplyReportRequest
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public string Reply { get; set; } = "";
    [Key(2)] public string AdminName { get; set; } = "";
    [Key(3)] public bool? Accepted { get; set; }
}

[MessagePackObject]
public sealed class ReplyReportResponse
{
    [Key(0)] public bool PlayerOnline { get; set; }
}
