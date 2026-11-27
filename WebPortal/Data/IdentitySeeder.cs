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
    private const string DefaultAdminUserName = "admin";
    private const string DefaultCredentialsFileName = "initial-admin-credentials.txt";
    private const int PasswordLength = 24;
    private const string PasswordCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%&*+-=?@";

    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.Users.AnyAsync(user => user.IsPrimaryAdmin)) return;

        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        string? configuredUserName = configuration["InitialAdmin:UserName"];
        string adminUserName = string.IsNullOrWhiteSpace(configuredUserName)
            ? DefaultAdminUserName
            : configuredUserName.Trim();

        ApplicationUser? admin = await userManager.FindByNameAsync(adminUserName);
        if (admin is not null)
        {
            if (admin.IsPrimaryAdmin) return;
            admin.IsPrimaryAdmin = true;
            IdentityResult updateResult = await userManager.UpdateAsync(admin);
            if (updateResult.Succeeded) return;
            string errors = string.Join(", ", updateResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not mark the initial admin account: {errors}");
        }

        string? configuredPassword = configuration["InitialAdmin:Password"];
        bool hasConfiguredPassword = !string.IsNullOrWhiteSpace(configuredPassword);
        string password = hasConfiguredPassword ? configuredPassword! : GeneratePassword();
        admin = new ApplicationUser
        {
            UserName = adminUserName,
            IsPrimaryAdmin = true
        };

        IdentityResult result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not create the initial admin account: {errors}");
        }

        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(IdentitySeeder));

        if (hasConfiguredPassword)
        {
            logger.LogInformation(
                "Initial admin account '{Username}' created from configuration.",
                adminUserName);
            return;
        }

        IHostEnvironment environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        string credentialsPath = ResolveCredentialsFilePath(configuration, environment);
        try
        {
            await WriteCredentialsFileAsync(credentialsPath, adminUserName, password);
        }
        catch
        {
            await userManager.DeleteAsync(admin);
            throw;
        }

        logger.LogWarning(
            "Initial admin account '{Username}' created. One-time credentials written to '{CredentialsPath}'. Read the password, change it after signing in, and delete this file.",
            adminUserName,
            credentialsPath);
    }

    private static string ResolveCredentialsFilePath(IConfiguration configuration, IHostEnvironment environment)
    {
        string? configuredPath = configuration["InitialAdmin:CredentialsFilePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath.Trim(), environment.ContentRootPath);
        }

        return Path.Combine(environment.ContentRootPath, "Data", DefaultCredentialsFileName);
    }

    private static async Task WriteCredentialsFileAsync(string path, string userName, string password)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        FileStreamOptions options = new()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        await using FileStream stream = new(path, options);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        await using StreamWriter writer = new(stream);
        await writer.WriteLineAsync($"Username: {userName}");
        await writer.WriteLineAsync($"Password: {password}");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("Sign in, change this password, and delete this file.");
    }

    public static string GeneratePassword(int length = PasswordLength)
    {
        if (length < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be positive.");
        }

        return string.Create(length, PasswordCharacters, static (password, characters) =>
        {
            for (int index = 0; index < password.Length; index++)
            {
                password[index] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
        });
    }
}