namespace Slipstream.Core;

public sealed record ReplayBatch(
    TimeSpan From, TimeSpan To,
    IReadOnlyList<ChannelSample> Channels,
    IReadOnlyList<PositionSample> Positions,
    IReadOnlyList<TimedDelta> Deltas);