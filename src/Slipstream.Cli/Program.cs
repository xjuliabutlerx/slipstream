using System.Text.Json;
using Slipstream.Core;

var archiveDir = args[0];
var outDir = args[1];
Directory.CreateDirectory(outDir);

var options = new JsonSerializerOptions { WriteIndented = true };

foreach (var path in Directory.GetFiles(archiveDir, "*.jsonStream").Order())
{
    var fileName = Path.GetFileName(path);
    var topic = fileName[..^".jsonStream".Length];
    if (topic.EndsWith(".z")) continue;

    var reducer = new SessionReducer();   // one per topic keeps memory small
    var count = 0;

    foreach (var line in ArchiveReader.ReadTopic(path))
    {
        reducer.Feed(topic, line.Payload);
        count++;
    }

    if (reducer.GetState(topic) is { } state)
    {
        File.WriteAllText(Path.Combine(outDir, topic + ".json"), state.ToJsonString(options));
    }

    Console.WriteLine($"{topic,-28} {count,7} messages");  
}
