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

        WebApplicationBuilder builder = WebApplication.CreateBuilder();

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
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager();

        builder.Services.AddSingleton<PipeMessageQueue>();
        builder.Services.AddHostedService<PipeBackgroundService>();
        builder.Services.AddSingleton<PipeGateway>();

        string[] supportedCultures = ["en", "zh-Hans"];
        RequestLocalizationOptions localizationOptions = new RequestLocalizationOptions().SetDefaultCulture("en")
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures)
            .AddInitialRequestCultureProvider(new AcceptLanguageHeaderRequestCultureProvider());

        WebApplication app = builder.Build();

        IdentitySeeder.SeedAdminAsync(app.Services).GetAwaiter().GetResult();
        app.UseRequestLocalization(localizationOptions);

        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.MapStaticAssets();

        app.UseAntiforgery();

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        app.MapLogoutEndpoint();

        await app.RunAsync();
    }
}