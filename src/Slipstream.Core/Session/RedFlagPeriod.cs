using System.Text.Json.Nodes;

namespace Slipstream.Core;

public sealed record RedFlagPeriod(TimeSpan Start, TimeSpan End);

public static class RedFlagPeriods
{
    public static IReadOnlyList<RedFlagPeriod> From(IEnumerable<TimedDelta> sessionStatusDeltas)
    {
        var redFlagList = new List<RedFlagPeriod>();
        TimeSpan? open = null;

        foreach (var status in sessionStatusDeltas)
        {
            if (status!.Topic == "SessionStatus")
            {
                var data = status.Payload;
                if ((string?) data["Status"] == "Aborted")
                {
                    open ??= status.SessionTime;
                }
                else if ((string?) data["Status"] == "Started" && open != null)
                {
                    redFlagList.Add(new RedFlagPeriod(open.Value, status.SessionTime));
                    open = null;
                }
            }
        }

        return redFlagList;
    }
}