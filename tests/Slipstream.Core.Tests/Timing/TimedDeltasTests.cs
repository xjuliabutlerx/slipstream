using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class TimedDeltasTests
{
    private static readonly SessionClock Clock =
        new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));

    // The stream started 10 minutes before the session clock's start.
    private static readonly DateTimeOffset StreamStart = Clock.StartUtc - TimeSpan.FromMinutes(10);

    [Fact]
    public void From_ConvertsOffsetsToSessionTime()
    {
        var lines = new[]
        {
            new ArchiveLine(TimeSpan.FromMinutes(15), JsonNode.Parse("{}")!),
            new ArchiveLine(TimeSpan.FromMinutes(20), JsonNode.Parse("{}")!),
        };

        var result = TimedDeltas.From("TimingData", lines, StreamStart, Clock).ToList();

        // offset 15 min -> 12:05 -> session time 00:05; offset 20 min -> 00:10
        Assert.Equal(
            new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10) },
            result.Select(d => d.SessionTime));
    }

    [Fact]
    public void From_BeforeClockStart_GivesNegativeSessionTime()
    {
        var lines = new[] { new ArchiveLine(TimeSpan.FromMinutes(4), JsonNode.Parse("{}")!) };

        var result = TimedDeltas.From("TimingData", lines, StreamStart, Clock).Single();

        Assert.Equal(TimeSpan.FromMinutes(-6), result.SessionTime);
    }

    [Fact]
    public void From_CarriesTopicAndPayload_InOrder()
    {
        var first = JsonNode.Parse("""{"a":1}""")!;
        var second = JsonNode.Parse("""{"b":2}""")!;
        var lines = new[]
        {
            new ArchiveLine(TimeSpan.FromMinutes(11), first),
            new ArchiveLine(TimeSpan.FromMinutes(12), second),
        };

        var result = TimedDeltas.From("DriverList", lines, StreamStart, Clock).ToList();

        Assert.All(result, d => Assert.Equal("DriverList", d.Topic));
        Assert.Same(first, result[0].Payload);
        Assert.Same(second, result[1].Payload);
    }

    [Fact]
    public void From_ConvertsOffsetsToSessionTime_DifferentStreamStart()
    {
        var lines = new[]
        {
            new ArchiveLine(TimeSpan.FromMinutes(15), JsonNode.Parse("{}")!),
            new ArchiveLine(TimeSpan.FromMinutes(20), JsonNode.Parse("{}")!),
        };

        var result = TimedDeltas.From("TimingData", lines, Clock.StartUtc - TimeSpan.FromMinutes(30), Clock).ToList();

        // offset 15 min -> 12:05 -> session time 00:05; offset 20 min -> 00:10
        Assert.Equal(
            new[] { TimeSpan.FromMinutes(-15), TimeSpan.FromMinutes(-10) },
            result.Select(d => d.SessionTime));
    }
}