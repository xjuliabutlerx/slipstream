namespace Slipstream.Core;

public static class ArchiveReader
{
    public static IEnumerable<ArchiveLine> ReadTopic(string path)
    {
        foreach (var line in File.ReadLines(path))   // lazy, like iterating a Python file
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return ArchiveLineParser.Parse(line);
        }
    }
}