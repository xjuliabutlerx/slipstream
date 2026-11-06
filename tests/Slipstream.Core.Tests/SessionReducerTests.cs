using System.Reflection;
using System.Text.Json.Nodes;
using Slipstream.Core;

namespace Slipstream.Core.Tests;

public class SessionReducerTests
{
    [Fact]
    public void SessionReducer_Feed_FirstMessageBecomesState()
    {
        var reducer = new SessionReducer();
        var message = JsonNode.Parse("""{"Lines": {"1": {"Position": "3"}}}""")!;

        reducer.Feed("TimingData", message);

        var state = reducer.GetState("TimingData");
        Assert.Equal(message.ToJsonString(), state!.ToJsonString());
    }

    [Fact]
    public void SessionReducer_Feed_MergeTwoMessagesIntoExistingState()
    {
        var reducer = new SessionReducer();
        var messageA = JsonNode.Parse("""{"Lines": {"1": {"Position": "3", "GapToLeader": "+1.2"}}}""")!;
        var messageB = JsonNode.Parse("""{"Lines": {"1": {"Position": "4"}}}""")!;
        var expected = JsonNode.Parse("""{"Lines": {"1": {"Position": "4", "GapToLeader": "+1.2"}}}""")!;

        reducer.Feed("TimingData", messageA);
        reducer.Feed("TimingData", messageB);

        var state = reducer.GetState("TimingData");
        Assert.Equal(expected.ToJsonString(), state!.ToJsonString());
    }

    [Fact]
    public void Session_Reducer_Feed_MergeTwoMessagesIntoExistingStateArray()
    {
        var reducer = new SessionReducer();
        var messageA = JsonNode.Parse("""{"Stint": [{"Car": 1, "Compound": "SOFT", "Laps": 2}, {"Car": 2, "Compound": "MEDIUM", "Laps": 2}]}""")!;
        var messageB = JsonNode.Parse("""{"Stint": {"0": {"Compound": "HARD", "Laps": 0}}}""")!;
        var expected = JsonNode.Parse("""{"Stint": [{"Car": 1, "Compound": "HARD", "Laps": 0}, {"Car": 2, "Compound": "MEDIUM", "Laps": 2}]}""")!;
        
        reducer.Feed("TireStints", messageA);
        reducer.Feed("TireStints", messageB);

        var state = reducer.GetState("TireStints");
        Assert.Equal(expected.ToJsonString(), state!.ToJsonString());
    }

    [Fact]
    public void SessionReducer_TwoTopics()
    {
        var reducer = new SessionReducer();
        var messageA = JsonNode.Parse("""{"Stint": [{"Car": 1, "Compound": "SOFT", "Laps": 2}, {"Car": 2, "Compound": "MEDIUM", "Laps": 2}]}""")!;
        var messageB = JsonNode.Parse("""{"Lines": {"1": {"Position": "4"}}}""")!;
        var messageC = JsonNode.Parse("""{"Stint": {"2": {"Car": 3, "Compound": "HARD", "Laps": 1}}}""")!;

        reducer.Feed("TireStints", messageA);
        reducer.Feed("TimingData", messageB);
        reducer.Feed("TireStints", messageC);

        var timingExpected = JsonNode.Parse("""{"Lines": {"1": {"Position": "4"}}}""")!;
        var tireExpected = JsonNode.Parse("""{"Stint": [{"Car": 1, "Compound": "SOFT", "Laps": 2}, {"Car": 2, "Compound": "MEDIUM", "Laps": 2}, {"Car": 3, "Compound": "HARD", "Laps": 1}]}""")!;

        var timingState = reducer.GetState("TimingData");
        var tireState = reducer.GetState("TireStints");

        Assert.Equal(timingExpected.ToJsonString(), timingState!.ToJsonString());
        Assert.Equal(tireExpected.ToJsonString(), tireState!.ToJsonString());
    }

    [Fact]
    public void SessionReducer_FirstMessageIsDelta()
    {
        var reducer = new SessionReducer();
        var first = JsonNode.Parse("""{"PitTimes": {"_deleted": ["41"]}}""")!;
        var expected = JsonNode.Parse("""{"PitTimes": {}}""")!;

        reducer.Feed("PitTiming", first);

        var state = reducer.GetState("PitTiming");
        Assert.Equal(expected.ToJsonString(), state!.ToJsonString());
    }

}