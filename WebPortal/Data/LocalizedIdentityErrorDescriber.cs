using CbmrWebAdmin.WebPortal.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace CbmrWebAdmin.WebPortal.Data;

public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> localizer) : IdentityErrorDescriber
{
    private IdentityError Error(string code, params object[] arguments) =>
        new() { Code = code, Description = localizer[code, arguments] };

    public override IdentityError DefaultError() => Error(nameof(DefaultError));
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure));
    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = localizer["UsernameRequired"] };
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), userName);
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken));
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = localizer["PasswordMinimumLength", length] };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), uniqueChars);
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric));
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit));
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower));
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper));
}
