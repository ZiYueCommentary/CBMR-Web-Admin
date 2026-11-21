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


using Microsoft.AspNetCore.Components.Authorization;

namespace CbmrWebAdmin.WebPortal.Data;

public sealed class AuditLogger(
    IServiceScopeFactory scopeFactory,
    IServiceProvider services,
    ILogger<AuditLogger> logger)
{
    public async Task WriteAsync(string action, string? target = null, string? details = null, string? userName = null)
    {
        try
        {
            userName ??= await GetCurrentUserNameAsync();
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.AuditLogs.Add(new AuditLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                UserName = string.IsNullOrWhiteSpace(userName) ? "?" : userName,
                Action = action,
                Target = target,
                Details = details
            });
            await dbContext.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not write audit log entry {Action} by {UserName}", action, userName);
        }
    }

    private async Task<string?> GetCurrentUserNameAsync()
    {
        AuthenticationStateProvider? provider = services.GetService<AuthenticationStateProvider>();
        if (provider is null) return null;
        AuthenticationState state = await provider.GetAuthenticationStateAsync();
        return state.User.Identity?.Name;
    }
}
