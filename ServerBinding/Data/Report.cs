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

namespace CbmrWebAdmin.ServerBinding.Data;

public sealed class Report
{
    public long Id { get; set; }
    public string ReporterSteamId { get; set; } = "";
    public string ReporterName { get; set; } = "";
    public string ReportedSteamId { get; set; } = "";
    public string ReportedName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public bool Handled { get; set; }
    public DateTime? HandledAtUtc { get; set; }
    public string? Reply { get; set; }
    public string? RepliedBy { get; set; }
    public DateTime? ReplyDeliveredAtUtc { get; set; }
    public bool? Accepted { get; set; }
}
