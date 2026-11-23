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

using System.Text.Json;
using CbmrWebAdmin.Shared;
using CbmrWebAdmin.WebPortal.Components.Account;
using CbmrWebAdmin.WebPortal.Components;
using CbmrWebAdmin.WebPortal.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CbmrWebAdmin.WebPortal;

public class Program
{
    public static async Task Main(string[] args)
    {
        string bundledWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Directory.Exists(bundledWebRoot) ? bundledWebRoot : null
        });
        bool useStaticAssetEndpoints = ConfigureStaticWebAssets(builder);

        builder.Services.AddLocalization();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddAuthentication(options => { options.DefaultScheme = IdentityConstants.ApplicationScheme; })
            .AddIdentityCookies();

        string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                                  throw new InvalidOperationException(
                                      "Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));
        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<PrimaryAdminClaimsPrincipalFactory>();

        builder.Services.AddScoped<AuditLogger>();
        builder.Services.AddSingleton<PipeMessageQueue>();
        builder.Services.AddSingleton<ServerLocation>();
        builder.Services.AddSingleton<PipeConnectionState>();
        builder.Services.AddHostedService<PipeBackgroundService>();
        builder.Services.AddSingleton<ServerEvents>();
        builder.Services.AddHostedService<ServerEventsBackgroundService>();
        builder.Services.AddSingleton<PipeGateway>();
        builder.Services.AddBlazorBootstrap();

        string[] supportedCultures =
        [
            "en", "cs", "de", "fa", "fr", "hu", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "uk", "zh-Hans", "zh-Hant"
        ];
        RequestLocalizationOptions localizationOptions = new RequestLocalizationOptions().SetDefaultCulture("en")
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures)
            .AddInitialRequestCultureProvider(new AcceptLanguageHeaderRequestCultureProvider());

        WebApplication app = builder.Build();

        await DatabaseInitializer.InitializeAsync(app.Services);
        app.UseRequestLocalization(localizationOptions);

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        if (useStaticAssetEndpoints)
            app.MapStaticAssets();
        else
            app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        app.MapLogoutEndpoint();

        await app.RunAsync();
    }

    private static bool ConfigureStaticWebAssets(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
            return true;

        string manifestPrefix = Path.Combine(AppContext.BaseDirectory, builder.Environment.ApplicationName);
        string endpointsPath = manifestPrefix + ".staticwebassets.endpoints.json";
        if (!File.Exists(endpointsPath))
            return false;

        using (FileStream manifest = File.OpenRead(endpointsPath))
        using (JsonDocument document = JsonDocument.Parse(manifest))
        {
            if (document.RootElement.GetProperty("ManifestType").GetString() == "Publish")
                return true;
        }

        string runtimePath = builder.Configuration[WebHostDefaults.StaticWebAssetsKey] ??
                             manifestPrefix + ".staticwebassets.runtime.json";
        if (!File.Exists(runtimePath))
            return false;

        using FileStream runtimeManifest = File.OpenRead(runtimePath);
        using JsonDocument runtimeDocument = JsonDocument.Parse(runtimeManifest);
        if (!runtimeDocument.RootElement.GetProperty("ContentRoots").EnumerateArray()
                .All(root => Directory.Exists(root.GetString())))
            return false;

        builder.WebHost.UseStaticWebAssets();
        return true;
    }
}