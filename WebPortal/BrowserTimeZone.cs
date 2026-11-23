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

namespace CbmrWebAdmin.WebPortal;

public sealed class BrowserTimeZone(TimeZoneInfo zone)
{
    public (string Name, string Id, string Offset, bool IsDaylight) GetDisplayDetails()
    {
        DateTime now = DateTime.UtcNow;
        bool isDaylight = zone.IsDaylightSavingTime(now);
        string name = isDaylight ? zone.DaylightName : zone.StandardName;

        TimeSpan offset = zone.GetUtcOffset(now);
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        TimeSpan magnitude = offset.Duration();
        string offsetLabel = magnitude.Minutes == 0
            ? FormattableString.Invariant($"UTC{sign}{magnitude.Hours}")
            : FormattableString.Invariant($"UTC{sign}{magnitude.Hours}:{magnitude.Minutes:00}");

        return (string.IsNullOrEmpty(name) ? zone.DisplayName : name, zone.Id, offsetLabel, isDaylight);
    }

    public DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    public DateTime? ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return null;
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public DateTime StartOfDayUtc(DateOnly date)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue);
        // A midnight transition can skip or repeat the start of a local day.
        while (zone.IsInvalidTime(local))
            local = local.AddMinutes(1);
        if (zone.IsAmbiguousTime(local))
            return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime;

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public DateTime? ToFutureUtc(DateOnly date, TimeOnly time)
    {
        DateTime local = date.ToDateTime(time);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return null;

        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return utc > DateTime.UtcNow ? utc : null;
    }
}
