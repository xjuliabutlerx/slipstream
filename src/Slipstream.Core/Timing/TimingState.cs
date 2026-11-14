namespace Slipstream.Core;

public sealed class TimingState
{
    private readonly SessionReducer _reducer = new();

    public void Apply(IEnumerable<TimedDelta> deltas)
    {
        foreach (var delta in deltas)
        {
            _reducer.Feed(delta.Topic, delta.Payload);
        }
    }

    public IReadOnlyList<LeaderboardRow> Leaderboard()
    {
        var timingData = _reducer.GetState("TimingData");
        var driverList = _reducer.GetState("DriverList");

        if (timingData == null || driverList == null) return new List<LeaderboardRow>();

        return Slipstream.Core.Leaderboard.From(timingData, driverList);
    }
}