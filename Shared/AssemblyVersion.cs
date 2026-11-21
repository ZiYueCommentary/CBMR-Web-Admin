using System.Reflection;

namespace CbmrWebAdmin.Shared;

public static class AssemblyVersion
{
    public static string GetVersion(Assembly assembly)
    {
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(version))
        {
            version = assembly.GetName().Version?.ToString() ?? string.Empty;
        }

        int metadataIndex = version.IndexOf('+');
        return metadataIndex < 0 ? version : version[..metadataIndex];
    }
}
