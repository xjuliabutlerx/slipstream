using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class ScrubConsistencyTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static TimedDelta Delta(string topic, string json, double seconds) =>
        new(S(seconds), topic, JsonNode.Parse(json)!);

    private static IReadOnlyList<TimedDelta> Deltas() => new[]
    {
        Delta("DriverList", """
            { "1": { "Tla": "AAA", "TeamColour": "FF0000" }, "2": { "Tla": "BBB", "TeamColour": "00FF00" } }
            """, -50),
        Delta("TimingData", """
            { "Lines": {
                "1": { "Position": "1", "GapToLeader": "",     "IntervalToPositionAhead": { "Value": "" },     "InPit": false },
                "2": { "Position": "2", "GapToLeader": "+1.5", "IntervalToPositionAhead": { "Value": "+1.5" }, "InPit": false } } }
            """, -40),
        Delta("TimingData", """{ "Lines": { "1": { "Position": "2" }, "2": { "Position": "1" } } }""", 6),
        Delta("TimingData", """{ "Lines": { "1": { "GapToLeader": "+9.9" } } }""", 8),
    };

    private static ReplayEngine EngineFor(ReplayClock clock, IReadOnlyList<TimedDelta> deltas) =>
        new(clock, Array.Empty<ChannelSample>(), Array.Empty<PositionSample>(), deltas: deltas);

    [Fact]
    public void Streaming_MatchesAt_AfterEveryTick()
    {
        var deltas = Deltas();
        var clock = new ReplayClock(start: S(0));      // speed 1
        clock.Play();
        var engine = EngineFor(clock, deltas);

        var state = new TimingState();
        state.Apply(deltas.Where(d => d.SessionTime <= TimeSpan.Zero));   // catch-up

        for (var i = 0; i < 3; i++)                    // ticks cover (0,3], (3,6], (6,9]
        {
            state.Apply(engine.Tick(S(3)).Deltas);
            Assert.Equal(TimingState.At(deltas, clock.SessionTime).Leaderboard(), state.Leaderboard());
        }

        // The board really did change along the way (guards against a test that compares two empty things).
        var final = state.Leaderboard();
        Assert.Equal("2", final[0].Car);
        Assert.Equal("+9.9", final[1].Gap);
    }

    [Fact]
    public void ScrubBack_ThenStreaming_MatchesAt()
    {
        var deltas = Deltas();
        var clock = new ReplayClock(start: S(0));
        clock.Play();
        var engine = EngineFor(clock, deltas);
        var state = new TimingState();
        state.Apply(deltas.Where(d => d.SessionTime <= TimeSpan.Zero));
        for (var i = 0; i < 3; i++) state.Apply(engine.Tick(S(3)).Deltas);   // now at 9 s

        clock.Seek(S(2));                                   // scrub back before the swap
        state = TimingState.At(deltas, clock.SessionTime);
        Assert.Equal(new[] { "1", "2" }, state.Leaderboard().Select(r => r.Car));

        state.Apply(engine.Tick(S(3)).Deltas);              // (2, 5]
        state.Apply(engine.Tick(S(3)).Deltas);              // (5, 8]: includes the swap at 6 and the gap at 8

        Assert.Equal(S(8), clock.SessionTime);
        Assert.Equal(TimingState.At(deltas, S(8)).Leaderboard(), state.Leaderboard());
        Assert.Equal("2", state.Leaderboard()[0].Car);
    }
}