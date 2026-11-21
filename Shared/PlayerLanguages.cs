namespace CbmrWebAdmin.Shared;

public static class PlayerLanguages
{
    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly(new[] { "en", "zh-Hans" });
}
