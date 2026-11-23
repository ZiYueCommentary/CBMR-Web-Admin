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
