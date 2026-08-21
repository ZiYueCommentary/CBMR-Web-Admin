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

public sealed record PipeRequest(PipeEnvelope Envelope, TaskCompletionSource<PipeEnvelope> Completion);