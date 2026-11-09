namespace Slipstream.Core;

public static class CarDataFile
{
    public static IEnumerable<ChannelSample> Read(string path, SessionClock clock)
    {
        foreach (var line in ArchiveReader.ReadTopic(path))
        {
            var decoded = ZDecoder.Decode((string) line.Payload!);
            foreach (var sample in CarDataNormalizer.Normalize(decoded, clock))
            {
                yield return sample;
            }
        }
    }
}