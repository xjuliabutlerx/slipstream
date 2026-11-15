namespace Slipstream.Core.Tests;

public class ArchiveLineParserTests
{
    [Theory]
    [InlineData("00:10:39.465{\"Lines\":{\"5\":{\"Stints\":{\"0\":{\"TotalLaps\":2}}}}}", 639465, "{\"Lines\":{\"5\":{\"Stints\":{\"0\":{\"TotalLaps\":2}}}}}")]
    [InlineData("1.01:57:54.673{\"PitTimes\":{\"_deleted\":[\"41\"]}}", 93474673, "{\"PitTimes\":{\"_deleted\":[\"41\"]}}")]
    [InlineData("\uFEFF00:10:39.465{\"a\":1}\r", 639465, "{\"a\":1}")]
    public void Parse_ReturnsOffsetAndPayload(string line, long expectedMilliseconds, string expectedPayloadJson)
    {
        var result = ArchiveLineParser.Parse(line);

        Assert.Equal(expectedMilliseconds, result.Offset.TotalMilliseconds);
        Assert.Equal(expectedPayloadJson, result.Payload.ToJsonString());
    }

    [Fact]
    public void Parse_QuotedZPayload_ReturnsStringNode()
    {
        var result = ArchiveLineParser.Parse("00:10:16.432\"AAAA+BBBB==\"\r");

        Assert.Equal(616432, result.Offset.TotalMilliseconds);
        Assert.Equal("AAAA+BBBB==", (string?)result.Payload);
    }

    [Fact]
    public void ParseInvalidLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ArchiveLineParser.Parse("notValidLine"));
    }
}