using MessagePack;

namespace CbmrWebAdmin.Shared;

[MessagePackObject]
public class WebPlayer
{
    [Key(0)]
    public int Index { get; set; }
    [Key(1)]
    public string SteamId { get; set; } = string.Empty;

    [Key(2)]
    public string Name { get; set; } = string.Empty;

}