using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Slipstream.Core;

public static class ArchiveLineParser
{
    public static ArchiveLine Parse(string line)
    {
        var matchPattern = @"^\uFEFF?((?:\d+\.)?\d{2}:\d{2}:\d{2}\.\d+)(.*)$";
        Match match = Regex.Match(line, matchPattern);

        if (match.Success)
        {
            string timestamp = match.Groups[1].Value;
            string payload = match.Groups[2].Value;

            return new ArchiveLine(
                TimestampParser.Parse(timestamp),
                JsonNode.Parse(payload) ?? throw new FormatException($"Line has a null payload: {line}")
            );
        }
        else
        {
            throw new FormatException($"Unable to parse the line: {line}");
        }
    }
}