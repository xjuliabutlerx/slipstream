using System.IO.Compression;
using System.Text.Json.Nodes;

namespace Slipstream.Core;

/// <summary>Decodes the payload of the compressed ".z" topics (CarData.z, Position.z).</summary>
public static class ZDecoder
{
    /// <summary>Takes the base64 string (without JSON quotes) and returns the parsed JSON.</summary>
    public static JsonNode Decode(string base64)
    {
        byte[] byteString = Convert.FromBase64String(base64);
        using var compressed = new MemoryStream(byteString);
        using var inflated = new DeflateStream(compressed, CompressionMode.Decompress);
        var node = JsonNode.Parse(inflated);
        return node ?? throw new InvalidDataException("Empty .z payload");
    }
}
