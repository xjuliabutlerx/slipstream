using System.Text.Json.Nodes;

namespace Slipstream.Core;

public static class PositionDataNormalizer
{
    public static IEnumerable<PositionSample> Normalize(JsonNode payload, SessionClock clock)
    {
        var entries = payload["Position"]!.AsArray();

        foreach (var entry in entries)
        {
            var time = TimestampParser.ParseUtc((string) entry!["Timestamp"]!);
            var positionTime = clock.ToSessionTime(time);

            foreach (var (car, positions) in entry["Entries"]!.AsObject())
            {
                var p = positions!;

                if ((double) p["X"]! == 0 && (double) p["Y"]! == 0 && (double) p["Z"]! == 0)
                {
                    continue;
                }

                yield return new PositionSample(positionTime, car, (double) p["X"]!/10.0, (double) p["Y"]!/10.0, (double) p["Z"]!/10.0, (string) p["Status"]!);
            }
        }
    }
}