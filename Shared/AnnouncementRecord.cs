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
public sealed class AnnouncementRecord
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public string Title { get; set; } = "";
    [Key(2)] public string Content { get; set; } = "";
    [Key(3)] public DateTime ExpiresAtUtc { get; set; }
    [Key(4)] public DateTime CreatedAtUtc { get; set; }
    [Key(5)] public DateTime UpdatedAtUtc { get; set; }
    [Key(6)] public string CreatedBy { get; set; } = "";
    [Key(7)] public int Revision { get; set; }
    [IgnoreMember] public bool Expired => ExpiresAtUtc <= DateTime.UtcNow;
}
