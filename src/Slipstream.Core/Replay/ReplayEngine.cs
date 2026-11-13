using System.Runtime.CompilerServices;

namespace Slipstream.Core;

public sealed class ReplayEngine(
    ReplayClock clock,
    IReadOnlyList<ChannelSample> channels,
    IReadOnlyList<PositionSample> positions,
    TimeProvider? timeProvider = null)
{
    public ReplayBatch Tick(TimeSpan realElapsed)
    {
        var (from, to) = clock.Advance(realElapsed);
        var channelList = ReplaySlice.Between(channels, from, to);
        var positionList = ReplaySlice.Between(positions, from, to);
        return new ReplayBatch(from, to, channelList, positionList);
    }

    public async IAsyncEnumerable<ReplayBatch> RunAsync(TimeSpan interval, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var time = timeProvider ?? TimeProvider.System;
        using var timer = new PeriodicTimer(interval, time);
        var last = time.GetTimestamp();

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = time.GetTimestamp();
            yield return Tick(time.GetElapsedTime(last, now));
            last = now;

            if (clock.IsFinished) yield break;
        }
    }
}