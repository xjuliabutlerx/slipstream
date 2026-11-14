namespace Slipstream.Core.Tests;

public class TimedDeltaFileTests
{
    private static readonly SessionClock Clock =
        new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void ReadStreamStart_UsesHeartbeatUtcMinusOffset()
    {
        // The stream started at 11:50:00. Offset 00:00:10 is therefore 11:50:10.
        using var file = new TempFile(
            "00:00:10.000" + """{"Utc":"2030-01-01T11:50:10.0000000Z"}""",
            "00:00:20.000" + """{"Utc":"2030-01-01T11:50:20.0000000Z"}""",
            "00:00:30.000" + """{"Utc":"2030-01-01T11:50:30.0000000Z"}""");

        var start = TimedDeltaFile.ReadStreamStart(file.Path);

        Assert.Equal(new DateTimeOffset(2030, 1, 1, 11, 50, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void Read_MapsLinesToSessionTime_WithTopic()
    {
        using var file = new TempFile(
            "00:15:00.000" + """{"a":1}""",
            "00:20:00.000" + """{"b":2}""");
        var streamStart = Clock.StartUtc - TimeSpan.FromMinutes(10);

        var result = TimedDeltaFile.Read(file.Path, "TimingData", streamStart, Clock).ToList();

        Assert.Equal(
            new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10) },
            result.Select(d => d.SessionTime));
        Assert.All(result, d => Assert.Equal("TimingData", d.Topic));
        Assert.Equal(1, (int) result[0].Payload["a"]!);
    }
}