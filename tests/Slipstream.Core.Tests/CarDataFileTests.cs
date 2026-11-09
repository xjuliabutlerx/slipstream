namespace Slipstream.Core.Tests;

public class CarDataFileTests
{
    [Fact]
    public void Read_DecodesEveryLineAndUsesUtcForSessionTime()
    {
        var lineA = TestData.ZArchiveLine("00:00:01.000",
            TestData.CarDataJson("2026-10-31T17:32:50.0000000Z", "44", ("2", 100), ("3", 2)));
        var lineB = TestData.ZArchiveLine("00:00:02.000",
            TestData.CarDataJson("2026-10-31T17:32:51.5000000Z", "44", ("2", 200), ("3", 3)));

        using var file = new TempFile(lineA, lineB);
        var clock = new SessionClock(TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z"));

        var expected = new List<ChannelSample>
            {
                new(TimeSpan.FromTicks(10000000), "44", "speed", 100, "km/h"),
                new(TimeSpan.FromTicks(10000000), "44", "n_gear",  2,   "gear"),
                new(TimeSpan.FromTicks(25000000), "44", "speed", 200, "km/h"),
                new(TimeSpan.FromTicks(25000000), "44", "n_gear",  3,   "gear"),
            };

        var samples = CarDataFile.Read(file.Path, clock).ToList();

        Assert.Equal(expected, samples);
    }
}