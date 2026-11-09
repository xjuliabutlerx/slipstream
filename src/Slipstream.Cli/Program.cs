using System.Text.Json;
using System.Text.Json.Nodes;
using Slipstream.Core;

var mode = args[0];
var input = args[1];
var outDir = args[2];
Directory.CreateDirectory(outDir);

var options = new JsonSerializerOptions { WriteIndented = true };

void WriteState(string topic, JsonNode state) =>
    File.WriteAllText(Path.Combine(outDir, topic + ".json"), state.ToJsonString(options));

if (mode == "archive")
{
    foreach (var path in Directory.GetFiles(input, "*.jsonStream").Order())
    {
        // ...your existing loop body, with the write replaced by:
        // if (reducer.GetState(topic) is { } state) WriteState(topic, state);
    }
}
else if (mode == "live")
{
    var reducer = new SessionReducer();   // one for the whole file: topics are interleaved
    var counts = new Dictionary<string, int>();

    foreach (var message in LiveReader.ReadFile(input))
    {
        if (message.Topic.EndsWith(".z")) continue;
        reducer.Feed(message.Topic, message.Payload);
        counts[message.Topic] = counts.GetValueOrDefault(message.Topic) + 1;
    }

    foreach (var topic in reducer.Topics.Order())
    {
        if (reducer.GetState(topic) is { } state) WriteState(topic, state);
        Console.WriteLine($"{topic,-28} {counts[topic],7} messages");
    }
}
else
{
    Console.Error.WriteLine("Usage: <archive|live> <input> <outDir>");
    return 1;
}

return 0;