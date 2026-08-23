using MessagePack;

namespace CbmrWebAdmin.Shared;

[MessagePackObject]
public class WebChat
{
    [Key(0)] public string SteamId { get; set; } = string.Empty;
    [Key(1)] public string PlayerName { get; set; } = string.Empty;
    [Key(2)] public string Message { get; set; } = string.Empty;
    [Key(3)] public DateTime When { get; set; }
}