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

using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.WebPortal.Data;

public static class IdentitySeeder
{
    private const string AdminUserName = "admin";
    private const string AdminEmail = "admin@localhost";
    private const int PasswordLength = 24;
    private const string PasswordCharacters =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%&*+-=?@";

    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser? admin = await userManager.FindByNameAsync(AdminUserName);
        if (admin is not null)
        {
            return;
        }

        string password = CreateRandomPassword();
        admin = new ApplicationUser
        {
            UserName = AdminUserName,
            Email = AdminEmail,
            EmailConfirmed = true
        };

        IdentityResult result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not create the initial admin account: {errors}");
        }

        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(IdentitySeeder));
        logger.LogWarning(
            "Initial admin account created. Username: {Username}; Password: {Password}. Store this password securely.",
            AdminUserName,
            password);
    }

    private static string CreateRandomPassword()
    {
        return string.Create(PasswordLength, PasswordCharacters, static (password, characters) =>
        {
            for (int index = 0; index < password.Length; index++)
            {
                password[index] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
        });
    }
}