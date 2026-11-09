namespace Slipstream.Core.Tests;

public class SessionClockTests
{
    [Fact]
    public void ToSessionTime_ReturnsElapsedSinceStart()
    {
        var start = TimestampParser.ParseUtc("2026-10-31T17:22:35.8900000Z");
        var clock = new SessionClock(start);

        var t = clock.ToSessionTime(TimestampParser.ParseUtc("2026-10-31T17:33:04.2542375Z"));

        Assert.Equal(TimeSpan.Parse("00:10:28.3642375"), t);
    }
}