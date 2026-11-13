using Microsoft.Extensions.Time.Testing;

namespace Slipstream.Core.Tests;

public class ReplayEngineTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static ChannelSample ChannelAt(double seconds) =>
        new(S(seconds), "44", "speed", 100, "km/h");

    private static PositionSample PositionAt(double seconds) =>
        new(S(seconds), "44", 100.0, -200.0, 300.0, "OnTrack");

    // A clock that is already playing at 3x: 1 s of real time covers 3 s of session time.
    private static ReplayClock PlayingClock(TimeSpan? end = null)
    {
        var clock = new ReplayClock(start: S(0), end: end) { Speed = 3 };
        clock.Play();
        return clock;
    }

    [Fact]
    public void Tick_ReturnsTheSamplesInTheRangeFromBothLists()
    {
        var channels  = new[] { ChannelAt(0), ChannelAt(1), ChannelAt(3), ChannelAt(4) };
        var positions = new[] { PositionAt(2), PositionAt(3.5) };
        var engine = new ReplayEngine(PlayingClock(), channels, positions);

        var batch = engine.Tick(S(1));                       // range (0, 3]

        Assert.Equal(S(0), batch.From);
        Assert.Equal(S(3), batch.To);
        Assert.Equal(new[] { 1.0, 3.0 }, batch.Channels.Select(s => s.SessionTime.TotalSeconds));
        Assert.Equal(new[] { 2.0 },      batch.Positions.Select(s => s.SessionTime.TotalSeconds));
    }

    [Fact]
    public void Tick_TwoTicksInARow_ReturnEachSampleExactlyOnce()
    {
        var channels = new[] { ChannelAt(1), ChannelAt(3), ChannelAt(4), ChannelAt(6), ChannelAt(7) };
        var engine = new ReplayEngine(PlayingClock(), channels, Array.Empty<PositionSample>());

        var first  = engine.Tick(S(1));                      // (0, 3]
        var second = engine.Tick(S(1));                      // (3, 6]

        // 3 sits exactly on the boundary and 7 is beyond both ranges.
        Assert.Equal(
            new[] { channels[0], channels[1], channels[2], channels[3] },
            first.Channels.Concat(second.Channels));
    }

    [Fact]
    public void Tick_WhilePaused_ReturnsEmptyBatches()
    {
        var clock = new ReplayClock(start: S(0));            // never played
        var engine = new ReplayEngine(clock, new[] { ChannelAt(0), ChannelAt(1) }, new[] { PositionAt(1) });

        var batch = engine.Tick(S(1));

        Assert.Equal(batch.From, batch.To);
        Assert.Empty(batch.Channels);
        Assert.Empty(batch.Positions);
    }

    [Fact]
    public void Tick_CrossingTheEnd_ReturnsOnlySamplesUpToTheEnd()
    {
        var channels  = new[] { ChannelAt(4), ChannelAt(5), ChannelAt(6) };
        var positions = new[] { PositionAt(6) };
        var engine = new ReplayEngine(PlayingClock(end: S(5)), channels, positions);

        var batch = engine.Tick(S(2));                       // would reach 6 s, clamped to 5 s

        Assert.Equal(S(5), batch.To);
        Assert.Equal(new[] { 4.0, 5.0 }, batch.Channels.Select(s => s.SessionTime.TotalSeconds));
        Assert.Empty(batch.Positions);
    }

    [Fact]
    public async Task RunAsync_YieldsOneBatchPerTimerTick()
    {
        var time = new FakeTimeProvider();
        var channels = new[] { ChannelAt(1), ChannelAt(4), ChannelAt(7) };
        var engine = new ReplayEngine(PlayingClock(), channels, Array.Empty<PositionSample>(), time);

        await using var batches = engine.RunAsync(interval: S(1)).GetAsyncEnumerator();

        // The iterator runs up to its first await when we call MoveNextAsync, so the
        // timer exists before we move the fake time.
        var next = batches.MoveNextAsync().AsTask();
        time.Advance(S(1));                                  // 1 s real = 3 s session: (0, 3]
        Assert.True(await next);
        Assert.Equal(new[] { channels[0] }, batches.Current.Channels);

        next = batches.MoveNextAsync().AsTask();
        time.Advance(S(1));                                  // (3, 6]
        Assert.True(await next);
        Assert.Equal(new[] { channels[1] }, batches.Current.Channels);

        next = batches.MoveNextAsync().AsTask();
        time.Advance(S(1));                                  // (6, 9]
        Assert.True(await next);
        Assert.Equal(new[] { channels[2] }, batches.Current.Channels);
    }

    [Fact]
    public async Task RunAsync_WhenTheClockFinishes_EndsTheStream()
    {
        var time = new FakeTimeProvider();
        var engine = new ReplayEngine(PlayingClock(end: S(5)),
            new[] { ChannelAt(1), ChannelAt(4) }, Array.Empty<PositionSample>(), time);

        await using var batches = engine.RunAsync(S(1)).GetAsyncEnumerator();

        var next = batches.MoveNextAsync().AsTask();
        time.Advance(S(1));                                  // (0, 3]
        Assert.True(await next);

        next = batches.MoveNextAsync().AsTask();
        time.Advance(S(1));                                  // would be (3, 6], clamped to (3, 5]
        Assert.True(await next);
        Assert.Equal(S(5), batches.Current.To);

        Assert.False(await batches.MoveNextAsync());         // the stream is over; no more time needed
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_StopsWithOperationCanceled()
    {
        var time = new FakeTimeProvider();
        var engine = new ReplayEngine(PlayingClock(), Array.Empty<ChannelSample>(), Array.Empty<PositionSample>(), time);
        using var cts = new CancellationTokenSource();

        await using var batches = engine.RunAsync(S(1), cts.Token).GetAsyncEnumerator();
        var next = batches.MoveNextAsync().AsTask();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }
}