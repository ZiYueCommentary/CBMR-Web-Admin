namespace CbmrWebAdmin.WebPortal;

public sealed class ServerEvents(ILogger<ServerEvents> logger)
{
    public event Action? ChatUpdated;

    internal void NotifyChatUpdated()
    {
        if (ChatUpdated is not { } handlers) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Chat update subscriber failed.");
            }
        }
    }
}
