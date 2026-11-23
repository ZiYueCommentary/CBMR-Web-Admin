namespace CbmrWebAdmin.WebPortal;

public sealed class BrowserTimeZone(TimeZoneInfo zone)
{
    public string Id => zone.Id;

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
