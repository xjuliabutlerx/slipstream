namespace Slipstream.Core.Tests;

public class SampleWindowTests
{
    // Builds a sample at a given number of seconds. Only the time matters in these tests.
    private static ChannelSample At(double seconds) =>
        new(TimeSpan.FromSeconds(seconds), "44", "speed", 100, "km/h");

    [Fact]
    public void Within_KeepsSamplesInsideTheRangeIncludingBothEnds()
    {
        // Range: from -10 s to 100 s
        var from = TimeSpan.FromSeconds(-10);
        var to   = TimeSpan.FromSeconds(100);

        var samples = new[] { At(-50), At(-10), At(0), At(100), At(101) };

        var kept = SampleWindow.Within(samples, from, to).ToList();

        // -50 is too early and 101 is too late. The other three stay.
        var expectedTimes = new[] { -10.0, 0.0, 100.0 };
        Assert.Equal(expectedTimes, kept.Select(s => s.SessionTime.TotalSeconds));
    }
}