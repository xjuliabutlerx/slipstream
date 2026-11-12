namespace Slipstream.Core.Tests;

public class ReplaySliceTests
{
    private static ChannelSample ChannelAt(double seconds) =>
        new(TimeSpan.FromSeconds(seconds), "44", "speed", 100, "km/h");

    private static ChannelSample ChannelAt(double seconds, string car) =>
        new(TimeSpan.FromSeconds(seconds), car, "speed", 100, "km/h");

    private static PositionSample PositionAt(double seconds) =>
        new(TimeSpan.FromSeconds(seconds), "44", 100.0, -200.0, 300.0, "OnTrack");

    private sealed class IndexOnlyList<T>(IReadOnlyList<T> inner) : IReadOnlyList<T>
    {
        public int Reads { get; private set; }
        public int Count => inner.Count;
        public T this[int index] { get { Reads++; return inner[index]; } }
        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException("must use the indexer");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static readonly TimeSpan From = TimeSpan.FromSeconds(-10);
    private static readonly TimeSpan To   = TimeSpan.FromSeconds(100);

    [Fact]
    public void Between_ExcludesFromAndIncludesTo_ForChannelSamples()
    {
        var samples = new[] { ChannelAt(-50), ChannelAt(-10), ChannelAt(0), ChannelAt(100), ChannelAt(101) };

        var kept = ReplaySlice.Between(samples, From, To).ToList();

        Assert.Equal(new[] { 0.0, 100.0 }, kept.Select(s => s.SessionTime.TotalSeconds));
    }

    [Fact]
    public void Between_ExcludesFromAndIncludesTo_ForPositionSamples()
    {
        var samples = new[] { PositionAt(-50), PositionAt(-10), PositionAt(0), PositionAt(100), PositionAt(101) };

        var kept = ReplaySlice.Between(samples, From, To).ToList();

        Assert.Equal(new[] { 0.0, 100.0 }, kept.Select(s => s.SessionTime.TotalSeconds));
    }

    [Fact]
    public void Between_ConsecutiveRanges_EmitEachSampleExactlyOnce()
    {
        var samples = new[] { ChannelAt(0), ChannelAt(1), ChannelAt(1.5), ChannelAt(2), ChannelAt(3) };
        var a = TimeSpan.FromSeconds(-1);
        var b = TimeSpan.FromSeconds(1.5);   // a sample sits exactly on the split point
        var c = TimeSpan.FromSeconds(3);     // and one exactly on the end

        var first  = ReplaySlice.Between(samples, a, b);
        var second = ReplaySlice.Between(samples, b, c);

        Assert.Equal(samples, first.Concat(second));
    }

    [Fact]
    public void Between_WhenFromEqualsTo_EmptyResult()
    {
        var samples = new[] { PositionAt(1) };
        var ts = TimeSpan.FromSeconds(1);
        
        var result = ReplaySlice.Between(samples, ts, ts);

        Assert.Empty(result);
    }

    [Fact]
    public void Between_WhenFromMoreThanTo_ThrowsError()
    {
        var samples = new[] { PositionAt(8) };
        var from = TimeSpan.FromSeconds(10);
        var to = TimeSpan.FromSeconds(5);

        Assert.ThrowsAny<ArgumentException>(() => ReplaySlice.Between(samples, from, to));
    }

    [Fact]
    public void Between_SameTimeAtTo_AllKept()
    {
        var samples = new[] { ChannelAt(4, "44"), ChannelAt(5, "44"), ChannelAt(5, "63"), ChannelAt(6, "44") };

        var result = ReplaySlice.Between(samples, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { samples[1], samples[2] }, result);
    }

    [Fact]
    public void Between_SameTimeAtFrom_AllExcluded()
    {
        var samples = new[] { ChannelAt(4, "44"), ChannelAt(5, "44"), ChannelAt(5, "63"), ChannelAt(6, "44") };

        var result = ReplaySlice.Between(samples, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6));

        Assert.Equal(new[] { samples[3] }, result);
    }

    [Fact]
    public void Between_OnLargeList_ReadsOnlyAFewItems()
    {
        var data = Enumerable.Range(0, 1_000_000).Select(i => ChannelAt(i)).ToList();
        var list = new IndexOnlyList<ChannelSample>(data);

        var result = ReplaySlice.Between(list, TimeSpan.FromSeconds(500_000), TimeSpan.FromSeconds(500_010));

        Assert.Equal(10, result.Count);
        Assert.True(list.Reads < 200, $"read {list.Reads} items");
    }
}