namespace CbmrWebAdmin.WebPortal;

public sealed class PipeConnectionState
{
    private int _isConnected;

    public bool IsConnected
    {
        get => Volatile.Read(ref _isConnected) != 0;
        internal set => Volatile.Write(ref _isConnected, value ? 1 : 0);
    }
}
