namespace Slipstream.Core;

public static class TimedDeltas
{
    public static IEnumerable<TimedDelta> From(
        string topic,
        IEnumerable<ArchiveLine> lines,
        DateTimeOffset streamStart,
        SessionClock clock)
    {
        foreach (var line in lines)
        {
            yield return new TimedDelta(clock.ToSessionTime(streamStart + line.Offset), topic, line.Payload);
        }
    }
}