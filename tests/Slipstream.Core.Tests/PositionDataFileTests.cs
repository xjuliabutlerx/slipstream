namespace Slipstream.Core.Tests;

public class PositionDataFileTests
{
    [Fact]
    public void Read_DecodesEveryLineAndUsesTimestampForSessionTime()
    {
        // The offsets (9 s, 3 s) deliberately disagree with the Timestamps:
        // the time must come from the payload, not from the line offset.
        var lineA = TestData.ZArchiveLine("00:00:09.000",
            TestData.PositionJson("2026-10-31T17:32:50.0000000Z", "44", 1000, -2500, 22270));
        var lineB = TestData.ZArchiveLine("00:00:03.000",
            TestData.PositionJson("2026-10-31T17:32:51.5000000Z", "1", 2000, 300, 22280, "OffTrack"));

        using var file = new TempFile(lineA, lineB);
        var clock = new SessionClock(TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z"));

        var expected = new List<PositionSample>
        {
            new(TimeSpan.FromSeconds(1.0), "44", 100.0, -250.0, 2227.0, "OnTrack"),
            new(TimeSpan.FromSeconds(2.5), "1", 200.0, 30.0, 2228.0, "OffTrack"),
        };

        var samples = PositionDataFile.Read(file.Path, clock).ToList();

        Assert.Equal(expected, samples);
    }
}