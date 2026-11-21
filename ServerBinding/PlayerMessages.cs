using System.Globalization;
using System.Resources;

namespace CbmrWebAdmin.ServerBinding;

public static class PlayerMessages
{
    private static readonly ResourceManager Resources = new("CbmrWebAdmin.ServerBinding.ServerBinding", typeof(PlayerMessages).Assembly);

    public static string Get(CultureInfo culture, string key, params object[] arguments) =>
        string.Format(culture, Resources.GetString(key, culture) ?? throw new MissingManifestResourceException(key), arguments);
}
