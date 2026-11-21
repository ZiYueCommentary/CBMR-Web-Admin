using System.Globalization;
using System.Text.Json;
using CbmrWebAdmin.Shared;

namespace CbmrWebAdmin.ServerBinding;

public sealed class PlayerLanguageSettings(string databasePath)
{
    private readonly object _lock = new();
    private readonly string _path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "player-language.json");
    private string _culture = "zh-Hans";

    public CultureInfo Culture
    {
        get { lock (_lock) return CultureInfo.GetCultureInfo(_culture); }
    }

    public void Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path)) return;
            string? culture = JsonSerializer.Deserialize<string>(File.ReadAllText(_path));
            Validate(culture);
            _culture = culture!;
        }
    }

    public void Set(string culture)
    {
        Validate(culture);
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(culture));
            File.Move(temporaryPath, _path, overwrite: true);
            _culture = culture;
        }
    }

    private static void Validate(string? culture)
    {
        if (culture is null || !PlayerLanguages.Supported.Contains(culture, StringComparer.Ordinal))
            throw new ArgumentException("Unsupported player language.", nameof(culture));
    }
}
