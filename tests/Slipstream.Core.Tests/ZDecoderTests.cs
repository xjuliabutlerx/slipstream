using Slipstream.Core;

namespace Slipstream.Core.Tests;

public class ZDecoderTests
{
    // Synthetic payload (not F1 data), built in Python the same way the feed is encoded:
    // UTF-8 BOM + JSON -> raw deflate -> base64.
    private const string CarDataPayload =
        "e797f7WSa15JUWZqsZJVdLVSaEmykpWSkYGRma6BIRCFGBhYgZGegYFBlJKOknNiEVBltZIhiHDOSMzLS80BCxgoWRkC1RjoKBkpWRmDaGMlK4taIIitBQA=";

    private const string PositionPayload = "e797f7VSQH5xZklmfp6SVXS1UkhmbmpxSWJugZKVkpGBkZmugSEQhRgYWIGRnoGBQZSSjpJrXklRZmqxklW1kiGICC5JLCkFcpX880KKEpOzgUoilKwMDQx0lCKVrHSNQIwoJStTg9ra2thaAA==";

    [Fact]
    public void Decode_CarDataPayload_ReturnsParsedJson()
    {
        var json = ZDecoder.Decode(CarDataPayload);

        var entry = json["Entries"]![0]!;
        Assert.Equal("2026-01-01T00:00:00.000Z", (string?)entry["Utc"]);
        Assert.Equal(300, (int?)entry["Cars"]!["1"]!["Channels"]!["2"]);
    }

    [Fact]
    public void Decode_PositionPayload_ReturnsParsedJson()
    {
        var json = ZDecoder.Decode(PositionPayload);

        var entry = json["Position"]![0]!;
        Assert.Equal("OnTrack", (string?) entry["Entries"]!["1"]!["Status"]);
        Assert.Equal(100, (int?) entry["Entries"]!["1"]!["X"]);
        Assert.Equal(-200, (int?) entry["Entries"]!["1"]!["Y"]);
        Assert.Equal(50, (int?) entry["Entries"]!["1"]!["Z"]);
    }
}
