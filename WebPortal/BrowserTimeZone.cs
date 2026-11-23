namespace CbmrWebAdmin.WebPortal;

public sealed class BrowserTimeZone(TimeZoneInfo zone)
{
    public (string Name, string Offset, string? City) GetDisplayDetails()
    {
        DateTime now = DateTime.UtcNow;
        string name = zone.IsDaylightSavingTime(now) ? zone.DaylightName : zone.StandardName;

        TimeSpan offset = zone.GetUtcOffset(now);
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        TimeSpan magnitude = offset.Duration();
        string offsetLabel = magnitude.Minutes == 0
            ? FormattableString.Invariant($"UTC{sign}{magnitude.Hours}")
            : FormattableString.Invariant($"UTC{sign}{magnitude.Hours}:{magnitude.Minutes:00}");

        return (string.IsNullOrEmpty(name) ? zone.DisplayName : name, offsetLabel, GetCity(zone.Id));
    }

    private static string? GetCity(string id)
    {
        int firstSlash = id.IndexOf('/');
        if (firstSlash < 0) return null;

        return id[..firstSlash] switch
        {
            "Africa" or "America" or "Antarctica" or "Arctic" or "Asia" or "Atlantic" or "Australia" or "Europe" or "Indian" or "Pacific"
                => id[(id.LastIndexOf('/') + 1)..].Replace('_', ' '),
            _ => null
        };
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
