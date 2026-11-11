using System.IO.Compression;
using System.Text;

namespace Slipstream.Core.Tests;

/// <summary>Helpers that build synthetic feed data (never real F1 lines).</summary>
public static class TestData
{
    /// <summary>
    /// Encodes JSON the way the feed encodes .z topics:
    /// UTF-8 BOM + JSON -> raw deflate -> base64. The inverse of ZDecoder.Decode.
    /// </summary>
    public static string EncodeZ(string json)
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(json))
            .ToArray();

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(output.ToArray());
    }

    /// <summary>Decoded CarData.z document with one entry and one car, e.g. CarDataJson("2026-10-31T17:32:50.0000000Z", "44", ("2", 280), ("3", 7)).</summary>
    public static string CarDataJson(string utc, string car, params (string Channel, int Value)[] channels)
    {
        var channelJson = string.Join(",", channels.Select(c => $"\"{c.Channel}\":{c.Value}"));
        return $"{{\"Entries\":[{{\"Utc\":\"{utc}\",\"Cars\":{{\"{car}\":{{\"Channels\":{{{channelJson}}}}}}}}}]}}";
    }

    /// <summary>One archive line for a .z topic: offset, then the base64 payload as a JSON string.</summary>
    public static string ZArchiveLine(string offset, string json) =>
        $"{offset}\"{EncodeZ(json)}\"";

    /// <summary>Decoded Position.z document with one entry and one car. Values are raw decimetres.</summary>
    public static string PositionJson(string timestamp, string car, int x, int y, int z, string status = "OnTrack") =>
        $"{{\"Position\":[{{\"Timestamp\":\"{timestamp}\",\"Entries\":{{\"{car}\":{{\"Status\":\"{status}\",\"X\":{x},\"Y\":{y},\"Z\":{z}}}}}}}]}}";

}

/// <summary>A temp file that is deleted when disposed (use with <c>using var</c>).</summary>
public sealed class TempFile : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".jsonStream");

    /// <summary>Writes lines the way the archive does: UTF-8 BOM, CRLF endings.</summary>
    public TempFile(params string[] lines)
    {
        File.WriteAllText(Path, string.Join("\r\n", lines) + "\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public void Dispose() => File.Delete(Path);
}
