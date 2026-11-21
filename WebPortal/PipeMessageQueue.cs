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
using CbmrWebAdmin.Shared;

namespace CbmrWebAdmin.WebPortal;

public class PipeMessageQueue
{
    public Channel<PipeRequest> Channel { get; } =
        System.Threading.Channels.Channel.CreateUnbounded<PipeRequest>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
}

public sealed record PipeRequest(PipeEnvelope Envelope, TaskCompletionSource<PipeEnvelope> Completion,
    CancellationToken CancellationToken);