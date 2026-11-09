using System.Text.Json.Nodes;

namespace Slipstream.Core;

public static class CarDataNormalizer
{
    public static IEnumerable<ChannelSample> Normalize(JsonNode payload, SessionClock clock)
    {
        var entries = payload["Entries"]!.AsArray();

        foreach (var entry in entries)
        {
            var time = TimestampParser.ParseUtc((string) entry!["Utc"]!);
            var sessionTime = clock.ToSessionTime(time);

            foreach (var (carNumber, car) in entry["Cars"]!.AsObject())
            {
                foreach (var (channel, value) in car!["Channels"]!.AsObject())
                {
                    switch (channel)
                    {
                        case "0":
                            yield return new ChannelSample(sessionTime, carNumber, "rpm", (double) value!, "rpm"); 
                            break;
                        case "2":
                            yield return new ChannelSample(sessionTime, carNumber, "speed", (double) value!, "km/h");
                            break;
                        case "3":
                            yield return new ChannelSample(sessionTime, carNumber, "n_gear", (double) value!, "gear");
                            break;
                        case "4":
                            yield return new ChannelSample(sessionTime, carNumber, "throttle", (double) Math.Min((double) value!, 100), "%");
                            break;
                        case "5":
                            yield return new ChannelSample(sessionTime, carNumber, "brake", (double) ((double) value! > 0 ? 1 : 0), "on/off");
                            break;
                        default:
                            continue;
                    }    
                }
            }
        }
    }    
}