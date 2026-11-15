using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

// Guards the helpers themselves: a broken encoder would make every later test misleading.
public class TestDataTests
{
    [Fact]
    public void EncodeZ_RoundTripsThroughZDecoder()
    {
        var json = TestData.CarDataJson("2026-10-31T17:32:50.0000000Z", "44", ("2", 280), ("3", 7));

        var decoded = ZDecoder.Decode(TestData.EncodeZ(json));

        Assert.Equal(JsonNode.Parse(json)!.ToJsonString(), decoded.ToJsonString());
    }

    [Fact]
    public void TempFile_WritesBomCrlfLinesAndDeletesOnDispose()
    {
        string path;
        using (var file = new TempFile("first", "second"))
        {
            path = file.Path;
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            Assert.Equal("first\r\nsecond\r\n", System.Text.Encoding.UTF8.GetString(bytes.Skip(3).ToArray()));
        }
        Assert.False(File.Exists(path));
    }
}
