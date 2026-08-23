using CbmrWebAdmin.Shared;
using CCB.Internal;

namespace CbmrWebAdmin.ServerBinding;

public class ServerChatList
{
    private readonly Queue<WebChat> _queue = [];
    private const int Size = 50;

    public void Add(Player player, string message)
    {
        _queue.Enqueue(new WebChat{SteamId = player.GetSteamID(), PlayerName = player.GetName(), Message = message, When = DateTime.Now});
        while (_queue.Count > Size) _queue.Dequeue();
    }

    public void Clear()
    {
        _queue.Clear();
    }

    public IEnumerable<WebChat> GetItems()
    {
        return _queue;
    }
}