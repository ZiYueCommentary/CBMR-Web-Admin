namespace CbmrWebAdmin.WebPortal;

public sealed class BrowserTimeZone(TimeZoneInfo zone)
{
    public string Id => zone.Id;

    public DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    public DateTime? ToFutureUtc(DateOnly date, TimeOnly time)
    {
        DateTime local = date.ToDateTime(time);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return null;

        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return utc > DateTime.UtcNow ? utc : null;
    }
}
