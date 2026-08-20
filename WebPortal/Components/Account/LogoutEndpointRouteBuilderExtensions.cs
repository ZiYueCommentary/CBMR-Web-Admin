using CbmrWebAdmin.WebPortal.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CbmrWebAdmin.WebPortal.Components.Account;

internal static class LogoutEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapLogoutEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapPost("/Account/Logout", async (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect($"~/{returnUrl}");
        });
    }
}