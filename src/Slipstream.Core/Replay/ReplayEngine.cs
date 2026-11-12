namespace Slipstream.Core;

public sealed class ReplayEngine(
    ReplayClock clock,
    IReadOnlyList<ChannelSample> channels,
    IReadOnlyList<PositionSample> positions)
{
    public ReplayBatch Tick(TimeSpan realElapsed)
    {
        var (from, to) = clock.Advance(realElapsed);
        var channelList = ReplaySlice.Between(channels, from, to);
        var positionList = ReplaySlice.Between(positions, from, to);
        return new ReplayBatch(from, to, channelList, positionList);
    }
}