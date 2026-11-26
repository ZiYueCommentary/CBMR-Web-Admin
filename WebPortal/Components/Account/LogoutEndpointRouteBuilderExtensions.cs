using System.Security.Claims;
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
            ClaimsPrincipal user,
            HttpContext httpContext,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] AuditLogger auditLogger,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            if (user.Identity?.IsAuthenticated == true)
                await auditLogger.WriteAsync(AuditActions.Logout, userName: user.Identity.Name, httpContext: httpContext);
            return TypedResults.LocalRedirect($"~/{returnUrl}");
        });
    }
}