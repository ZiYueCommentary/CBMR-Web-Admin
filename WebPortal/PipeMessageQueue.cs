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

using CbmrWebAdmin.Shared;

namespace CbmrWebAdmin.WebPortal;

public enum PipeQueueRejectReason
{
    None,
    Disconnected,
    Full
}

public readonly struct PipeQueueHandle
{
    internal LinkedListNode<PipeRequest>? Node { get; }

    internal PipeQueueHandle(LinkedListNode<PipeRequest> node)
    {
        Node = node;
    }
}

public class PipeMessageQueue(IConfiguration configuration)
{
    public const int DefaultCapacity = 64;

    private readonly object _sync = new();
    private readonly LinkedList<PipeRequest> _pending = new();
    private readonly int _capacity = Math.Max(1, configuration.GetValue("PipeQueueCapacity", DefaultCapacity));
    private TaskCompletionSource? _waiter;
    private bool _isConnected;

    public int Capacity => _capacity;

    public void MarkConnected()
    {
        lock (_sync)
        {
            _isConnected = true;
        }
    }

    public void MarkDisconnected(Exception? reason = null)
    {
        List<PipeRequest>? drained = null;
        lock (_sync)
        {
            _isConnected = false;
            if (_pending.Count > 0)
            {
                drained = new List<PipeRequest>(_pending.Count);
                for (LinkedListNode<PipeRequest>? node = _pending.First; node is not null; node = node.Next)
                {
                    drained.Add(node.Value);
                }

                _pending.Clear();
            }
        }

        if (drained is null)
        {
            return;
        }

        Exception failure = reason ?? new InvalidOperationException("Not connected to the game server.");
        foreach (PipeRequest request in drained)
        {
            request.Completion.TrySetException(failure);
        }
    }

    public bool TryEnqueue(PipeRequest request, out PipeQueueHandle handle, out PipeQueueRejectReason rejectReason)
    {
        TaskCompletionSource? waiterToRelease;
        lock (_sync)
        {
            if (!_isConnected)
            {
                handle = default;
                rejectReason = PipeQueueRejectReason.Disconnected;
                return false;
            }

            if (_pending.Count >= _capacity)
            {
                handle = default;
                rejectReason = PipeQueueRejectReason.Full;
                return false;
            }

            handle = new PipeQueueHandle(_pending.AddLast(request));
            rejectReason = PipeQueueRejectReason.None;
            waiterToRelease = _waiter;
            _waiter = null;
        }

        waiterToRelease?.TrySetResult();
        return true;
    }

    public bool Remove(PipeQueueHandle handle)
    {
        if (handle.Node is not { } node)
        {
            return false;
        }

        lock (_sync)
        {
            if (node.List != _pending)
            {
                return false;
            }

            _pending.Remove(node);
            return true;
        }
    }

    public async ValueTask<PipeRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                while (_pending.First is { } first)
                {
                    _pending.RemoveFirst();
                    if (!first.Value.CancellationToken.IsCancellationRequested)
                    {
                        return first.Value;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                _waiter ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                waitTask = _waiter.Task;
            }

            await waitTask.WaitAsync(cancellationToken);
        }
    }
}

public sealed record PipeRequest(PipeEnvelope Envelope, TaskCompletionSource<PipeEnvelope> Completion,
    CancellationToken CancellationToken);