namespace Slipstream.Core;

public static class StreamAlignment
{
    // Pairs of (archive offset, absolute Utc) taken from Heartbeat lines, in file order.
    public static DateTimeOffset EstimateStreamStart(
        IEnumerable<(TimeSpan Offset, DateTimeOffset Utc)> pairs)
    {
        var starts = new List<DateTimeOffset>();
        TimeSpan? previousOffset = null;

        foreach (var (offset, utc) in pairs)
        {
            if (previousOffset is null || offset > previousOffset)
            {
                starts.Add(utc - offset);
            }
            previousOffset = offset;
        }

        if (starts.Count == 0) throw new InvalidOperationException("There are no usable pairs of offsets.");

        starts.Sort();
        return starts[(starts.Count - 1)/2];
    }

    public static IEnumerable<(TimeSpan Offset, DateTimeOffset Utc)> PairsFrom(
        IEnumerable<ArchiveLine> heartbeatLines)
    {
        foreach (var line in heartbeatLines)
        {
            var offset = line.Offset!;
            var payload = line.Payload;

            if (payload == null) continue;

            var utc = (string) payload["Utc"]!;

            if (utc == null) continue;
            
            yield return (line.Offset, TimestampParser.ParseUtc(utc));
        }
    }
}