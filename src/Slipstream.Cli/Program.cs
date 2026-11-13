using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Slipstream.Core;

var mode = args[0];

if (mode == "replay")
{
    var folder = args[1];
    var speed   = args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 60;
    var minutes = args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 20;

    var (sessionClock, _, _) = LoadWindow(folder);
    var carSamples = SampleWindow.Within(
        CarDataFile.Read(Path.Combine(folder, "CarData.z.jsonStream"), sessionClock),
        TimeSpan.Zero, TimeSpan.FromMinutes(minutes)).ToList();
    var positionSamples = SampleWindow.Within(
        PositionDataFile.Read(Path.Combine(folder, "Position.z.jsonStream"), sessionClock),
        TimeSpan.Zero, TimeSpan.FromMinutes(minutes)).ToList();


    var replayClock = new ReplayClock(start: TimeSpan.Zero, end: TimeSpan.FromMinutes(minutes))
    {
        Speed = speed
    };
    replayClock.Play();

    var replayEngine = new ReplayEngine(replayClock, carSamples, positionSamples);
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    try
    {
        await foreach (var batch in replayEngine.RunAsync(TimeSpan.FromMilliseconds(100), cts.Token))
        {
            Console.WriteLine($"t = {batch.To.ToString(@"hh\:mm\:ss\.f")}   " +
                            $"channels: {batch.Channels.Count,5}   positions: {batch.Positions.Count,4}");
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("stopped");
    }

    return 0;
}

var input = args[1];
var outDir = args[2];
Directory.CreateDirectory(outDir);

var options = new JsonSerializerOptions { WriteIndented = true };

void WriteState(string topic, JsonNode state) =>
    File.WriteAllText(Path.Combine(outDir, topic + ".json"), state.ToJsonString(options));

(SessionClock Clock, TimeSpan From, TimeSpan To) LoadWindow(string archiveDir)
{
    // 1. Reduce SessionInfo.jsonStream to its final state.
    var reducer = new SessionReducer();
    foreach (var line in ArchiveReader.ReadTopic(Path.Combine(archiveDir, "SessionInfo.jsonStream")))
        reducer.Feed("SessionInfo", line.Payload);
    var info = reducer.GetState("SessionInfo")!;

    // 2. Clock and window from it.
    var clock = SessionWindow.ClockFrom(info);
    var (fromUtc, toUtc) = SessionWindow.WindowFrom(info, TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(30));
    var from = fromUtc - clock.StartUtc;
    var to   = toUtc   - clock.StartUtc;

    return (clock, from, to);
}

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
    var (clock, from, to) = LoadWindow(input);

    // Read the car data, filter, and report.
    var samples = SampleWindow.Within(CarDataFile.Read(Path.Combine(input, "CarData.z.jsonStream"), clock), from, to);

    // Write the car data into parquet files.
    var path = Path.Combine(outDir, "cardata.parquet");
    Stopwatch stopwatch = Stopwatch.StartNew();

    await ParquetChannelSamples.WriteAsync(path, samples);

    TimeSpan ts = stopwatch.Elapsed;
    double writeSeconds = ts.TotalSeconds;

    // var readBack = await ParquetChannelSamples.ReadAsync(path);

    Console.WriteLine($"Time Elapsed: {writeSeconds:F1} seconds");
    // Console.WriteLine($"Rows read back: {readBack.Count}");
    Console.WriteLine($"File Size: {new FileInfo(path).Length / 1_000_000.0:F1} MB");
}
else if (mode == "positions")
{
    var (clock, from, to) = LoadWindow(input);

    var samples = SampleWindow.Within(
        PositionDataFile.Read(Path.Combine(input, "Position.z.jsonStream"), clock), from, to);

    var path = Path.Combine(outDir, "position.parquet");
    var stopwatch = Stopwatch.StartNew();

    await ParquetPositionSamples.WriteAsync(path, samples);
    var readBack = await ParquetPositionSamples.ReadAsync(path);

    Console.WriteLine($"Time Elapsed: {stopwatch.Elapsed.TotalSeconds:F1} seconds");
    Console.WriteLine($"Rows read back: {readBack.Count}");
    Console.WriteLine($"File Size: {new FileInfo(path).Length / 1_000_000.0:F1} MB");
}
else
{
    Console.Error.WriteLine("Usage: <archive|live|carData|positions|replay> <input> <outDir>");
    return 1;
}

return 0;