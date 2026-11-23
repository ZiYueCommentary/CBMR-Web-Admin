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

    public bool Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path)) return true;
            try
            {
                string? culture = JsonSerializer.Deserialize<string>(File.ReadAllText(_path));
                Validate(culture);
                _culture = culture!;
                return true;
            }
            catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
            {
                return false;
            }
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
