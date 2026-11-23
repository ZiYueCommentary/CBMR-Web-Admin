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
using System.Resources;

namespace CbmrWebAdmin.ServerBinding;

public static class PlayerMessages
{
    private const string ResourceBaseName = "CbmrWebAdmin.ServerBinding.Resources.ServerBinding";
    private const string ResourceExtension = ".resources";
    private static readonly IReadOnlyDictionary<string, ResourceManager> Resources = typeof(PlayerMessages).Assembly
        .GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourceBaseName + ".", StringComparison.Ordinal) && name.EndsWith(ResourceExtension, StringComparison.Ordinal))
        .Select(name => name[..^ResourceExtension.Length])
        .ToDictionary(name => name, name => new ResourceManager(name, typeof(PlayerMessages).Assembly), StringComparer.Ordinal);

    public static string Get(CultureInfo culture, string key, params object[] arguments)
    {
        for (var candidate = culture; ; candidate = candidate.Parent)
        {
            bool isNeutral = candidate.Name.Length == 0;
            string baseName = isNeutral ? ResourceBaseName : $"{ResourceBaseName}.{candidate.Name}";
            if (Resources.TryGetValue(baseName, out var resources) && resources.GetString(key, CultureInfo.InvariantCulture) is { } message)
                return string.Format(culture, message, arguments);

            if (isNeutral) throw new MissingManifestResourceException(key);
        }
    }
}
