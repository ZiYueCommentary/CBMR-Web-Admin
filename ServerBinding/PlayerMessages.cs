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
