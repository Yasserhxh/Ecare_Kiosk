using Ecare.Application.Common;

namespace Ecare.Application.Tests;

public class MoroccoClockTests
{
    [Fact]
    public void FromUtc_ConvertsToMoroccoWallClock()
    {
        // 2026-08-04 12:00 UTC => 13:00 au Maroc (GMT+1, hors Ramadan).
        var utc = new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

        var morocco = MoroccoClock.FromUtc(utc);

        Assert.Equal(new DateTime(2026, 8, 4, 13, 0, 0), morocco);
    }

    [Fact]
    public void FromUtc_UnspecifiedKind_IsTreatedAsUtc()
    {
        var unspecified = new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Unspecified);

        var morocco = MoroccoClock.FromUtc(unspecified);

        Assert.Equal(new DateTime(2026, 8, 4, 13, 0, 0), morocco);
    }

    [Fact]
    public void Now_MatchesConvertedUtcNow()
    {
        var before = MoroccoClock.FromUtc(DateTime.UtcNow);
        var now = MoroccoClock.Now;
        var after = MoroccoClock.FromUtc(DateTime.UtcNow);

        Assert.InRange(now, before, after);
    }
}
