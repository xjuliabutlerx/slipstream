using System.Globalization;
using System.Text.Json.Nodes;

namespace Slipstream.Core;

public static class Leaderboard
{
    public static IReadOnlyList<LeaderboardRow> From(JsonNode timingData, JsonNode driverList)
    {
        var leaderboard = new List<LeaderboardRow>();

        foreach (var (car, line) in timingData["Lines"]!.AsObject())
        {
            var l = line!;

            if (driverList[car] == null || l["Position"] == null) continue;

            var position = int.Parse((string) l["Position"]!, CultureInfo.InvariantCulture);
            var gap = (string?) l["GapToLeader"] ?? "";
            var interval = (string?) l["IntervalToPositionAhead"]?["Value"] ?? "";
            var inPit = (bool?) l["InPit"] ?? false;

            var tla = (string?) driverList[car]!["Tla"] ?? "";
            var teamColor = (string?) driverList[car]!["TeamColour"] ?? "";

            var row = new LeaderboardRow(position, car, tla, teamColor, gap, interval, inPit);
            leaderboard.Add(row);
        }

        return leaderboard.OrderBy(r => r.Position).ToList();
    }
}