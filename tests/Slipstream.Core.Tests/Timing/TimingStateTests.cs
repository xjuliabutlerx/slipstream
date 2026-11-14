using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class TimingStateTests
{
    private static TimedDelta Delta(string topic, string json, double seconds = 0) =>
        new(TimeSpan.FromSeconds(seconds), topic, JsonNode.Parse(json)!);

    private const string TwoDrivers = """
        {
        "1": { "Tla": "AAA", "TeamColour": "FF0000" },
        "2": { "Tla": "BBB", "TeamColour": "00FF00" }
        }
        """;

    private const string FirstTiming = """
        {
        "Lines": {
            "1": { "Position": "1", "GapToLeader": "",      "IntervalToPositionAhead": { "Value": "" },      "InPit": false },
            "2": { "Position": "2", "GapToLeader": "+1.5",  "IntervalToPositionAhead": { "Value": "+1.5" },  "InPit": false }
        }
        }
        """;

    // A real delta carries only what changed: here, just the positions.
    private const string SwapPositions = """
        { "Lines": { "1": { "Position": "2" }, "2": { "Position": "1" } } }
        """;

    [Fact]
    public void Leaderboard_OnlyDriverList_ReturnsEmpty()
    {
        var state = new TimingState();
        state.Apply(new[] { Delta("DriverList", TwoDrivers) });

        Assert.Empty(state.Leaderboard());
    }

    [Fact]
    public void Leaderboard_OnlyTimingData_ReturnsEmpty()
    {
        var state = new TimingState();
        state.Apply(new[] { Delta("TimingData", FirstTiming) });

        Assert.Empty(state.Leaderboard());
    }

    [Fact]
    public void Leaderboard_AfterDriverListAndTiming_ReturnsRows()
    {
        var state = new TimingState();
        state.Apply(new[] { Delta("DriverList", TwoDrivers), Delta("TimingData", FirstTiming) });

        var expected = new List<LeaderboardRow>
        {
            new(1, "1", "AAA", "FF0000", "",     "",     false),
            new(2, "2", "BBB", "00FF00", "+1.5", "+1.5", false),
        };
        Assert.Equal(expected, state.Leaderboard());
    }

    [Fact]
    public void Apply_MergesLaterDelta_KeepsEarlierFields()
    {
        var state = new TimingState();
        state.Apply(new[]
        {
            Delta("DriverList", TwoDrivers, 0),
            Delta("TimingData", FirstTiming, 1),
            Delta("TimingData", SwapPositions, 2),
        });

        // Positions swapped; gaps and intervals survive because the delta did not mention them.
        var expected = new List<LeaderboardRow>
        {
            new(1, "2", "BBB", "00FF00", "+1.5", "+1.5", false),
            new(2, "1", "AAA", "FF0000", "",     "",     false),
        };
        Assert.Equal(expected, state.Leaderboard());
    }

    [Fact]
    public void Apply_AccumulatesAcrossCalls()
    {
        var state = new TimingState();
        state.Apply(new[] { Delta("DriverList", TwoDrivers), Delta("TimingData", FirstTiming) });
        state.Apply(new[] { Delta("TimingData", SwapPositions, 2) });

        Assert.Equal(new[] { "2", "1" }, state.Leaderboard().Select(r => r.Car));
    }

    [Fact]
    public void Apply_UnrelatedTopic_LeavesLeaderboardUnchanged()
    {
        var state = new TimingState();
        state.Apply(new[] { Delta("DriverList", TwoDrivers), Delta("TimingData", FirstTiming) });
        var before = state.Leaderboard();

        state.Apply(new[] { Delta("Heartbeat", """{"Utc":"2030-01-01T12:00:00.0000000Z"}""") });

        Assert.Equal(before, state.Leaderboard());
    }

    [Fact]
    public void Leaderboard_NoData_ReturnsEmptyLeaderboard()
    {
        var timingState = new TimingState();
        Assert.Empty(timingState.Leaderboard());
    }    
}