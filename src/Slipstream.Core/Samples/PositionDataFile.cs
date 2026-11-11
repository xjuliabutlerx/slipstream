namespace Slipstream.Core;

public static class PositionDataFile
{
    public static IEnumerable<PositionSample> Read(string path, SessionClock clock)
    {
        foreach (var line in ArchiveReader.ReadTopic(path))
        {
            var decoded = ZDecoder.Decode((string) line.Payload!);
            foreach (var sample in PositionDataNormalizer.Normalize(decoded, clock))
            {
                yield return sample;
            }
        }
    }
}