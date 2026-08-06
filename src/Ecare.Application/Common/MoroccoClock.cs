namespace Ecare.Application.Common;

/// <summary>
/// Heure murale Maroc (GMT+1), indépendante du fuseau du serveur : les App Services Azure
/// tournent en UTC, donc DateTime.Now y vaut l'heure Maroc - 1h. Même convention que les
/// horodatages SQL « SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time' ».
/// </summary>
public static class MoroccoClock
{
    private static readonly TimeZoneInfo MoroccoTimeZone = ResolveMoroccoTimeZone();

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MoroccoTimeZone);

    public static DateTime FromUtc(DateTime utcValue)
    {
        var normalizedUtc = utcValue.Kind == DateTimeKind.Utc
            ? utcValue
            : DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, MoroccoTimeZone);
    }

    private static TimeZoneInfo ResolveMoroccoTimeZone()
    {
        // ID Windows puis ID IANA (Linux) — même stratégie que FleetDateTimeHelper côté portail.
        foreach (var timeZoneId in new[] { "Morocco Standard Time", "Africa/Casablanca" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
