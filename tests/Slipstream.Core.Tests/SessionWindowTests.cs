using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class SessionWindowTests
{
    [Theory]
    [InlineData("2026-11-01T14:00:00", "-06:00:00", "2026-11-01T20:00:00Z")]
    [InlineData("2026-10-30T12:30:00", "-06:00:00", "2026-10-30T18:30:00Z")]
    [InlineData("2026-03-15T15:00:00", "+08:00:00", "2026-03-15T07:00:00Z")]
    public void ClockFrom_ConvertsLocalStartAndOffsetToUtc(string startDate, string gmtOffset, string expectedUtc)
    {
        var info = JsonNode.Parse($$"""
            {
            "Name": "Race",
            "StartDate": "{{startDate}}",
            "EndDate": "2026-11-01T16:00:00",
            "GmtOffset": "{{gmtOffset}}"
            }
            """)!;

        var clock = SessionWindow.ClockFrom(info);

        Assert.Equal(TimestampParser.ParseUtc(expectedUtc), clock.StartUtc);
    }

    [Fact]
    public void ClockFrom_MissingStartDate_ThrowsFormatException()
    {
        var info = JsonNode.Parse("""{ "GmtOffset": "-06:00:00" }""")!;
        Assert.Throws<FormatException>(() => SessionWindow.ClockFrom(info));
    }

    [Fact]
    public void ClockFrom_MissingOffset_ThrowsFormatException()
    {
        var info = JsonNode.Parse("""{ "StartDate": "2026-11-01T14:00:00" }""")!;
        Assert.Throws<FormatException>(() => SessionWindow.ClockFrom(info));
    }

    [Theory]
    [InlineData(0,  0,  "2026-11-01T20:00:00Z", "2026-11-01T22:00:00Z")]
    [InlineData(90, 30, "2026-11-01T18:30:00Z", "2026-11-01T22:30:00Z")]
    [InlineData(1500, 0, "2026-10-31T19:00:00Z", "2026-11-01T22:00:00Z")]   // 25 h back
    public void WindowFrom_AppliesRollsAroundScheduledTimes(int preMinutes, int postMinutes, string expectedFrom, string expectedTo)
    {
        var info = JsonNode.Parse("""
            {
            "StartDate": "2026-11-01T14:00:00",
            "EndDate": "2026-11-01T16:00:00",
            "GmtOffset": "-06:00:00"
            }
            """)!;

        var (from, to) = SessionWindow.WindowFrom(info, TimeSpan.FromMinutes(preMinutes), TimeSpan.FromMinutes(postMinutes));

        Assert.Equal(TimestampParser.ParseUtc(expectedFrom), from);
        Assert.Equal(TimestampParser.ParseUtc(expectedTo), to);
    }

    [Fact]
    public void WindowFrom_MissingEndDate_ThrowsFormatException()
    {
        var info = JsonNode.Parse("""{ "StartDate": "2026-11-01T14:00:00", "GmtOffset": "-06:00:00" }""")!;
        Assert.Throws<FormatException>(() => SessionWindow.WindowFrom(info, TimeSpan.Zero, TimeSpan.Zero));
    }
}