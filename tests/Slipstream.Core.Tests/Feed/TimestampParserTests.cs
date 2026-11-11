using System.Reflection;
using System.Text.Json.Nodes;
using Slipstream.Core;

namespace Slipstream.Core.Tests;

public class TimestampParserTests
{
    [Theory]
    [InlineData("00:10:19.460", 619460)]
    [InlineData("1.01:57:54.673", 93474673)]
    [InlineData("00:00:00.000", 0)]
    public void Parse_ReturnsOffset(string text, long expectedMilliseconds)
    {
        var result = TimestampParser.Parse(text);

        Assert.Equal(expectedMilliseconds, result.TotalMilliseconds);
    }

    [Fact]
    public void Parse_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => TimestampParser.Parse("notATimestamp"));
    }

    [Fact]
    public void ParseUtc_ReadsSevenFractionDigits()
    {
        var result = TimestampParser.ParseUtc("2026-10-31T17:29:10.5801881Z");

        var expected = new DateTimeOffset(2026, 10, 31, 17, 29, 10, TimeSpan.Zero).AddTicks(5801881);
        Assert.Equal(expected, result);
    }

}