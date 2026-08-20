using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace CbmrWebAdmin.WebPortal.Components.Account;

internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
    public void RedirectTo(string? uri)
    {
        uri ??= "";

        // Prevent open redirects.
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
        {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        navigationManager.NavigateTo(uri);
    }
}