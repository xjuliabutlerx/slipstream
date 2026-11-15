using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class CarDataNormalizerTests
{
    [Fact]
    public void Normalize_ValidData()
    {
        var payload = JsonNode.Parse("""{"Entries":[{"Utc":"2026-10-31T17:32:49.3647304Z","Cars":{"44":{"Channels":{"2":280,"3":7}}}}]}""")!;
        var start = TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z");
        var clock = new SessionClock(start);

        var expected = new List<ChannelSample>
            {
                new(TimeSpan.FromTicks(3647304), "44", "speed", 280, "km/h"),
                new(TimeSpan.FromTicks(3647304), "44", "n_gear",  7,   "gear"),
            };

        var samples = CarDataNormalizer.Normalize(payload, clock).ToList();

        Assert.Equal(expected, samples);
    }

    [Fact]
    public void Normalize_OneUnknownChannel()
    {
        var payload = JsonNode.Parse("""{"Entries":[{"Utc":"2026-10-31T17:32:49.3647304Z","Cars":{"44":{"Channels":{"2":280,"45":7}}}}]}""")!;
        var start = TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z");
        var clock = new SessionClock(start);

        var expected = new List<ChannelSample>
            {
                new(TimeSpan.FromTicks(3647304), "44", "speed", 280, "km/h"),
            };

        var samples = CarDataNormalizer.Normalize(payload, clock).ToList();

        Assert.Equal(expected, samples);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 1)]
    [InlineData(104, 1)]
    public void Normalize_BrakeBecomesBinary(int brakeData, int expectedBrakeData)
    {
        var payload = JsonNode.Parse($$"""
            {
            "Entries": [
                {
                "Utc": "2026-10-31T17:32:49.3647304Z",
                "Cars": {
                    "44": { "Channels": { "5": {{brakeData}} } }
                }
                }
            ]
            }
            """)!;
        var start = TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z");
        var clock = new SessionClock(start);

        var expected = new List<ChannelSample>
            {
                new(TimeSpan.FromTicks(3647304), "44", "brake", expectedBrakeData, "on/off"),
            };

        var samples = CarDataNormalizer.Normalize(payload, clock).ToList();

        Assert.Equal(expected, samples);
    }

    [Theory]
    [InlineData(55, 55)]
    [InlineData(100, 100)]
    [InlineData(104, 100)]
    public void Normalize_ThrottleIsCapped(int throttleData, int expectedThrottleData)
    {
        var payload = JsonNode.Parse($$"""
            {
            "Entries": [
                {
                "Utc": "2026-10-31T17:32:49.3647304Z",
                "Cars": {
                    "44": { "Channels": { "4": {{throttleData}} } }
                }
                }
            ]
            }
            """)!;
        var start = TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z");
        var clock = new SessionClock(start);

        var expected = new List<ChannelSample>
            {
                new(TimeSpan.FromTicks(3647304), "44", "throttle", expectedThrottleData, "%"),
            };

        var samples = CarDataNormalizer.Normalize(payload, clock).ToList();

        Assert.Equal(expected, samples);
    }
}