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

[MessagePackObject]
public sealed class BroadcastRequest
{
    [Key(0)] public string Message { get; init; } = string.Empty;

    [Key(1)] public string Title { get; init; } = string.Empty;
}