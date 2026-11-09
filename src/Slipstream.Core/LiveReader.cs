using System.Text.Json;
using System.Text.Json.Nodes;

namespace Slipstream.Core;

public static class LiveReader
{
    private static IEnumerable<FeedMessage> ParseLine(string line)
    {
        JsonNode node;
        try
        {
            node = JsonNode.Parse(line) ?? throw new FormatException($"Line is not a valid JSON: {line}");
        }
        catch (JsonException e)
        {
            throw new FormatException($"Invalid JSON line: {line}", e);
        }

        switch((string?) node["kind"])
        {
            case "snapshot":
                foreach (var (topic, state) in node["result"]!.AsObject())
                {
                    yield return new FeedMessage(
                        topic,
                        state ?? throw new FormatException($"Snapshot topic {topic} has a null state"),
                        null
                    );
                }
                break;
            case "feed":
                var data = node["data"]!.AsArray();
                var topicName = (string) data[0]!;
                var topicData = data[1] ?? throw new FormatException($"Feed topic {topicName} has null data");
                var topicUtc = TimestampParser.ParseUtc((string) data[2]!);
                
                yield return new FeedMessage(
                    topicName,
                    topicData,
                    topicUtc
                );
                break;
            default:
                throw new FormatException($"Unknown record kind: {(string?) node["kind"]}");
        }
    }

    public static IEnumerable<FeedMessage> ReadFile(string path)
    {
        foreach (var line in File.ReadLines(path))   // lazy, like iterating a Python file
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            foreach (var message in ParseLine(line))
            {
                yield return message;
            }
        }
    }
}