namespace Slipstream.Core.Tests;

public class SampleWindowTests
{
    // Builds a sample at a given number of seconds. Only the time matters in these tests.
    private static ChannelSample ChannelAt(double seconds) =>
        new(TimeSpan.FromSeconds(seconds), "44", "speed", 100, "km/h");

    private static PositionSample PositionAt(double seconds) =>
        new(TimeSpan.FromSeconds(seconds), "44", 100.0, -200.0, 300.0, "OnTrack");

    private static readonly TimeSpan From = TimeSpan.FromSeconds(-10);
    private static readonly TimeSpan To   = TimeSpan.FromSeconds(100);

    [Fact]
    public void Within_KeepsChannelSamplesInsideTheRangeIncludingBothEnds()
    {
        var samples = new[] { ChannelAt(-50), ChannelAt(-10), ChannelAt(0), ChannelAt(100), ChannelAt(101) };

        var kept = SampleWindow.Within(samples, From, To).ToList();

        // -50 is too early and 101 is too late. The other three stay.
        Assert.Equal(new[] { -10.0, 0.0, 100.0 }, kept.Select(s => s.SessionTime.TotalSeconds));
    }

    [Fact]
    public void Within_KeepsPositionSamplesInsideTheRangeIncludingBothEnds()
    {
        var samples = new[] { PositionAt(-50), PositionAt(-10), PositionAt(0), PositionAt(100), PositionAt(101) };

        var kept = SampleWindow.Within(samples, From, To).ToList();

        Assert.Equal(new[] { -10.0, 0.0, 100.0 }, kept.Select(s => s.SessionTime.TotalSeconds));
    }
}