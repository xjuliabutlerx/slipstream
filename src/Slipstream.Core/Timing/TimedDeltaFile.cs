namespace Slipstream.Core;

public static class TimedDeltaFile
{
    // Heartbeat.jsonStream -> the stream's start time.
    public static DateTimeOffset ReadStreamStart(string heartbeatPath)
    {
        return StreamAlignment.EstimateStreamStart(StreamAlignment.PairsFrom(ArchiveReader.ReadTopic(heartbeatPath)));
    }

    // Any plain-topic .jsonStream -> time-stamped deltas on the session clock.
    public static IEnumerable<TimedDelta> Read(
        string path, string topic, DateTimeOffset streamStart, SessionClock clock)
    {
        return TimedDeltas.From(topic, ArchiveReader.ReadTopic(path), streamStart, clock);
    }
}