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
        var topic = Path.GetFileName(path)[..^".jsonStream".Length];
        if (topic.EndsWith(".z")) continue;

        var reducer = new SessionReducer();   // one per topic keeps memory small
        var count = 0;

        foreach (var line in ArchiveReader.ReadTopic(path))
        {
            reducer.Feed(topic, line.Payload);
            count++;
        }

        if (reducer.GetState(topic) is { } state) WriteState(topic, state);
        Console.WriteLine($"{topic,-28} {count,7} messages");
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
else if (mode == "carData")
{
    // 1. Reduce SessionInfo.jsonStream to its final state.
    var reducer = new SessionReducer();
    foreach (var line in ArchiveReader.ReadTopic(Path.Combine(input, "SessionInfo.jsonStream")))
        reducer.Feed("SessionInfo", line.Payload);
    var info = reducer.GetState("SessionInfo")!;

    // 2. Clock and window from it.
    var clock = SessionWindow.ClockFrom(info);
    var (fromUtc, toUtc) = SessionWindow.WindowFrom(info, TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(30));
    var from = fromUtc - clock.StartUtc;
    var to   = toUtc   - clock.StartUtc;

    // 3. Read the car data, filter, and report.
    var samples = SampleWindow.Within(CarDataFile.Read(Path.Combine(input, "CarData.z.jsonStream"), clock), from, to);

    List<ChannelSample> sampleList = samples.ToList();
    
    Console.WriteLine($"Found {sampleList.Count} samples.");
    Console.WriteLine($"First Sample: {sampleList[0]}");
    Console.WriteLine($"Last Sample: {sampleList[sampleList.Count - 1]}");
}
else
{
    Console.Error.WriteLine("Usage: <archive|live> <input> <outDir>");
    return 1;
}

return 0;