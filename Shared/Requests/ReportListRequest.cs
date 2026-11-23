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

namespace CbmrWebAdmin.Shared.Requests;

public enum ReportSortColumn { Time, Reporter, ReportedPlayer, Content, Reply }

[MessagePackObject]
public sealed class ReportListRequest
{
    [Key(0)] public int Offset { get; init; }
    [Key(1)] public int PageSize { get; init; }
    [Key(2)] public bool Handled { get; init; }
    [Key(3)] public DateTime? FromUtc { get; init; }
    [Key(4)] public DateTime? ToUtc { get; init; }
    [Key(5)] public string? Reporter { get; init; }
    [Key(6)] public string? ReportedPlayer { get; init; }
    [Key(7)] public string? Content { get; init; }
    [Key(8)] public ReportSortColumn SortColumn { get; init; }
    [Key(9)] public bool SortAscending { get; init; }
}

[MessagePackObject]
public sealed class ReportListResponse
{
    [Key(0)] public List<ReportRecord> Records { get; init; } = [];
    [Key(1)] public int TotalCount { get; init; }
}
