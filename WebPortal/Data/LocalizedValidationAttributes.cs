using System.ComponentModel.DataAnnotations;
using CbmrWebAdmin.WebPortal.Resources;
using Microsoft.Extensions.Localization;

namespace CbmrWebAdmin.WebPortal.Data;

public sealed class LocalizedRequiredAttribute(string resourceKey) : RequiredAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (IsValid(value)) return ValidationResult.Success;
        var localizer = validationContext.GetRequiredService<IStringLocalizer<SharedResource>>();
        return new ValidationResult(localizer[resourceKey]);
    }
}

public sealed class LocalizedStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    public required string ResourceKey { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (IsValid(value)) return ValidationResult.Success;
        var localizer = validationContext.GetRequiredService<IStringLocalizer<SharedResource>>();
        return new ValidationResult(localizer[ResourceKey, MinimumLength, MaximumLength]);
    }
}
