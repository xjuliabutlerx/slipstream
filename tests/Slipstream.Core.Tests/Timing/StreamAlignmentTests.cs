using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class StreamAlignmentTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static (TimeSpan, DateTimeOffset) Pair(double offsetSeconds, double jitterSeconds = 0)
        => (TimeSpan.FromSeconds(offsetSeconds),
            Start + TimeSpan.FromSeconds(offsetSeconds + jitterSeconds));

    [Fact]
    public void EstimateStreamStart_PerfectPairs_ReturnsStart()
    {
        var result = StreamAlignment.EstimateStreamStart(new[] { Pair(10), Pair(20), Pair(30) });
        Assert.Equal(Start, result);
    }

    [Fact]
    public void EstimateStreamStart_UsesMedian_NotMean()
    {
        // One wild outlier: a mean would move by seconds, the median does not.
        var result = StreamAlignment.EstimateStreamStart(new[]
        {
            Pair(10), Pair(20, jitterSeconds: 100), Pair(30), Pair(40), Pair(50),
        });
        Assert.Equal(Start, result);
    }

    [Fact]
    public void EstimateStreamStart_IgnoresFrozenTail()
    {
        // After offset 30 the offset stops advancing while Utc keeps going.
        var pairs = new[]
        {
            Pair(10), Pair(20), Pair(30),
            (TimeSpan.FromSeconds(30), Start + TimeSpan.FromSeconds(1000)),
            (TimeSpan.FromSeconds(30), Start + TimeSpan.FromSeconds(1010)),
            (TimeSpan.FromSeconds(30), Start + TimeSpan.FromSeconds(1020)),
            (TimeSpan.FromSeconds(30), Start + TimeSpan.FromSeconds(1030)),
        };
        Assert.Equal(Start, StreamAlignment.EstimateStreamStart(pairs));
    }

    [Fact]
    public void EstimateStreamStart_NoPairs_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => StreamAlignment.EstimateStreamStart(Array.Empty<(TimeSpan, DateTimeOffset)>()));
    }

    [Fact]
    public void PairsFrom_ReadsOffsetAndUtc()
    {
        var lines = new[]
        {
            new ArchiveLine(TimeSpan.FromSeconds(10), JsonNode.Parse("""{"Utc":"2030-01-01T12:00:10.0000000Z"}""")!),
            new ArchiveLine(TimeSpan.FromSeconds(20), JsonNode.Parse("""{"Utc":"2030-01-01T12:00:20.5000000Z"}""")!),
        };

        var pairs = StreamAlignment.PairsFrom(lines).ToList();

        Assert.Equal(new (TimeSpan, DateTimeOffset)[]
        {
            (TimeSpan.FromSeconds(10), new DateTimeOffset(2030, 1, 1, 12, 0, 10, TimeSpan.Zero)),
            (TimeSpan.FromSeconds(20), new DateTimeOffset(2030, 1, 1, 12, 0, 20, 500, TimeSpan.Zero)),
        }, pairs);
    }

    [Fact]
    public void PairsFrom_SkipsLinesWithoutUtc()
    {
        var lines = new[]
        {
            new ArchiveLine(TimeSpan.FromSeconds(10), JsonNode.Parse("""{"_kf":true}""")!),
            new ArchiveLine(TimeSpan.FromSeconds(20), JsonNode.Parse("""{"Utc":"2030-01-01T12:00:20.0000000Z"}""")!),
        };

        var pairs = StreamAlignment.PairsFrom(lines).ToList();

        Assert.Single(pairs);
        Assert.Equal(TimeSpan.FromSeconds(20), pairs[0].Offset);
    }

    [Fact]
    public void PairsFrom_AcceptsSixFractionDigits()
    {
        var lines = new[]
        {
            new ArchiveLine(TimeSpan.FromSeconds(10), JsonNode.Parse("""{"Utc":"2030-01-01T12:00:10.123456Z"}""")!),
        };

        Assert.Single(StreamAlignment.PairsFrom(lines));
    }
}