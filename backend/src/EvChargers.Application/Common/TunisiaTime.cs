namespace EvChargers.Application.Common;

/// <summary>Local wall-clock time in Tunisia, where every station is.</summary>
public static class TunisiaTime
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static DateTime ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).DateTime;

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Tunisia has been UTC+1 without daylight saving since 2009
            return TimeZoneInfo.CreateCustomTimeZone("Tunisia", TimeSpan.FromHours(1), "Tunisia (UTC+1)", "Tunisia (UTC+1)");
        }
    }
}
