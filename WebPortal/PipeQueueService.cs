using System.IO.Pipes;
using System.Threading.Channels;
using Serilog;

namespace CbmrWebAdmin.WebPortal;

public class PipeQueueService : IHostedService
{
    private readonly Channel<string> _msgChannel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    private CancellationTokenSource? _cts;
    private readonly string _pipeName;
    private Task? _sendTask;

    public PipeQueueService(IConfiguration configuration)
    {
        if (configuration["PipeName"] is null)
        {
            //Log.Error(
            //    "Missing Server Binding PID, please config Server Binding and let it launch Web Portal automatically.");
            //Environment.Exit(0);
        }

        _pipeName = "CbmrWebAdmin"; //configuration["PipeName"]!;
    }

    public void HelloWorld()
    {
        _msgChannel.Writer.TryWrite("ACK\n");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _sendTask = Task.Run(() => ProcessQueueAsync(_cts.Token), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);

                await pipeClient.ConnectAsync(5000, token);
                await using StreamWriter writer = new StreamWriter(pipeClient);
                writer.AutoFlush = true;

                while (await _msgChannel.Reader.WaitToReadAsync(token))
                {
                    while (_msgChannel.Reader.TryRead(out string? msg))
                    {
                        if (!pipeClient.IsConnected) break;

                        await writer.WriteLineAsync(msg);
                    }
                }
            }
            catch (TimeoutException)
            {
                await Task.Delay(2000, token);
            }
            catch (Exception)
            {
                await Task.Delay(1000, token);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts?.CancelAsync()!;
        _msgChannel.Writer.Complete();
        if (_sendTask != null)
        {
            await Task.WhenAny(_sendTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }
}