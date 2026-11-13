using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class LeaderboardTests
{
    [Fact]
    public void From_SortsByNumericPosition_AndJoinsDriverDetails()
    {
        // Cars are listed out of order, and "10" must come after "2" (not before it, as text would).
        var timing = JsonNode.Parse("""
            {
              "Lines": {
                "44": { "Position": "10", "GapToLeader": "+45.2",  "IntervalToPositionAhead": { "Value": "+3.1" },   "InPit": false },
                "1":  { "Position": "1",  "GapToLeader": "",       "IntervalToPositionAhead": { "Value": "" },       "InPit": false },
                "63": { "Position": "2",  "GapToLeader": "+1.234", "IntervalToPositionAhead": { "Value": "+1.234" }, "InPit": true }
              }
            }
            """)!;
        var drivers = JsonNode.Parse("""
            {
              "44": { "Tla": "BBB", "TeamColour": "00FF00" },
              "1":  { "Tla": "AAA", "TeamColour": "FF0000" },
              "63": { "Tla": "CCC", "TeamColour": "0000FF" }
            }
            """)!;

        var rows = Leaderboard.From(timing, drivers);

        var expected = new List<LeaderboardRow>
        {
            new(1,  "1",  "AAA", "FF0000", "",       "",       false),
            new(2,  "63", "CCC", "0000FF", "+1.234", "+1.234", true),
            new(10, "44", "BBB", "00FF00", "+45.2",  "+3.1",   false),
        };
        Assert.Equal(expected, rows);
    }

    [Fact]
    public void From_MissingInPitReturnsFalse()
    {
        // Car 44 has no InPit field; a missing flag must read as "not in the pit".
        var timing = JsonNode.Parse("""
            {
              "Lines": {
                "44": { "Position": "10", "GapToLeader": "+45.2",  "IntervalToPositionAhead": { "Value": "+3.1" }},
                "1":  { "Position": "1",  "GapToLeader": "",       "IntervalToPositionAhead": { "Value": "" },       "InPit": false },
                "63": { "Position": "2",  "GapToLeader": "+1.234", "IntervalToPositionAhead": { "Value": "+1.234" }, "InPit": true }
              }
            }
            """)!;
        var drivers = JsonNode.Parse("""
            {
              "44": { "Tla": "BBB", "TeamColour": "00FF00" },
              "1":  { "Tla": "AAA", "TeamColour": "FF0000" },
              "63": { "Tla": "CCC", "TeamColour": "0000FF" }
            }
            """)!;

        var rows = Leaderboard.From(timing, drivers);

        var expected = new List<LeaderboardRow>
        {
            new(1,  "1",  "AAA", "FF0000", "",       "",       false),
            new(2,  "63", "CCC", "0000FF", "+1.234", "+1.234", true),
            new(10, "44", "BBB", "00FF00", "+45.2",  "+3.1",   false),
        };
        Assert.Equal(expected, rows);
    }

    [Fact]
    public void From_PositionsContainsUnknownDriver()
    {
        // Car 44 has no InPit field; a missing flag must read as "not in the pit".
        var timing = JsonNode.Parse("""
            {
              "Lines": {
                "44": { "Position": "10", "GapToLeader": "+45.2",  "IntervalToPositionAhead": { "Value": "+3.1" }},
                "241":  { "Position": "1",  "GapToLeader": "",       "IntervalToPositionAhead": { "Value": "" },       "InPit": false },
                "63": { "Position": "2",  "GapToLeader": "+1.234", "IntervalToPositionAhead": { "Value": "+1.234" }, "InPit": true }
              }
            }
            """)!;
        var drivers = JsonNode.Parse("""
            {
              "44": { "Tla": "BBB", "TeamColour": "00FF00" },
              "1":  { "Tla": "AAA", "TeamColour": "FF0000" },
              "63": { "Tla": "CCC", "TeamColour": "0000FF" }
            }
            """)!;

        var rows = Leaderboard.From(timing, drivers);

        var expected = new List<LeaderboardRow>
        {
            new(2,  "63", "CCC", "0000FF", "+1.234", "+1.234", true),
            new(10, "44", "BBB", "00FF00", "+45.2",  "+3.1",   false),
        };
        Assert.Equal(expected, rows);
    }

    [Fact]
    public void From_MissingPositionForDriver()
    {
        // Car 44 has no InPit field; a missing flag must read as "not in the pit".
        var timing = JsonNode.Parse("""
            {
              "Lines": {
                "44": { "Position": "10", "GapToLeader": "+45.2",  "IntervalToPositionAhead": { "Value": "+3.1" }},
                "1":  { "Position": "1",  "GapToLeader": "",       "IntervalToPositionAhead": { "Value": "" },       "InPit": false },
                "63": { "GapToLeader": "+1.234", "IntervalToPositionAhead": { "Value": "+1.234" }, "InPit": true }
              }
            }
            """)!;
        var drivers = JsonNode.Parse("""
            {
              "44": { "Tla": "BBB", "TeamColour": "00FF00" },
              "1":  { "Tla": "AAA", "TeamColour": "FF0000" },
              "63": { "Tla": "CCC", "TeamColour": "0000FF" }
            }
            """)!;

        var rows = Leaderboard.From(timing, drivers);

        var expected = new List<LeaderboardRow>
        {
            new(1,  "1",  "AAA", "FF0000", "",       "",       false),
            new(10, "44", "BBB", "00FF00", "+45.2",  "+3.1",   false),
        };
        Assert.Equal(expected, rows);
    }
}