using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class PositionDataNormalizerTests
{
    [Fact]
    public void Normalize_ConvertsDecimetresToMetres()
    {
        var payload = JsonNode.Parse("""
            {
            "Position": [
                {
                "Timestamp": "2026-10-31T17:32:49.5000000Z",
                "Entries": {
                    "44": { "Status": "OnTrack", "X": 1000, "Y": -2500, "Z": 22270 }
                }
                }
            ]
            }
            """)!;
        var clock = new SessionClock(TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z"));

        var samples = PositionDataNormalizer.Normalize(payload, clock).ToList();

        var expected = new List<PositionSample>
        {
            new(TimeSpan.FromSeconds(0.5), "44", 100.0, -250.0, 2227.0, "OnTrack"),
        };
        Assert.Equal(expected, samples);
    }

    [Fact]
    public void Normalize_DropsOriginPlaceholder_KeepsOtherCars()
    {
        var payload = JsonNode.Parse("""
            {
            "Position": [
                {
                "Timestamp": "2026-10-31T17:32:49.5000000Z",
                "Entries": {
                    "1":  { "Status": "OnTrack", "X": 0,    "Y": 0,     "Z": 0 },
                    "2":  { "Status": "OnTrack", "X": 1000, "Y": 0,     "Z": 22270 },
                    "44": { "Status": "OnTrack", "X": 1000, "Y": -2500, "Z": 22270 }
                }
                }
            ]
            }
            """)!;
        var clock = new SessionClock(TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z"));

        var samples = PositionDataNormalizer.Normalize(payload, clock).ToList();

        var expected = new List<PositionSample>
        {
            new(TimeSpan.FromSeconds(0.5), "2", 100.0, 0.0, 2227.0, "OnTrack"),
            new(TimeSpan.FromSeconds(0.5), "44", 100.0, -250.0, 2227.0, "OnTrack"),
        };
        Assert.Equal(expected, samples);
    }

    [Fact]
    public void Normalize_OffTrack_MultipleEntries()
    {
        var payload = JsonNode.Parse("""
            {
            "Position": [
                {
                "Timestamp": "2026-10-31T17:32:49.5000000Z",
                "Entries": {
                    "1":  { "Status": "OnTrack",  "X": 0,    "Y": 0,     "Z": 0 },
                    "2":  { "Status": "OnTrack",  "X": 0,    "Y": 0,     "Z": 22270 },
                    "3":  { "Status": "OffTrack", "X": 0,    "Y": 500,   "Z": 0     },
                    "4":  { "Status": "OffTrack", "X": 1000, "Y": 0,     "Z": 0     },
                    "44": { "Status": "OnTrack",  "X": 1000, "Y": -2500, "Z": 22270 }
                }
                },
                {
                "Timestamp": "2026-10-31T17:32:50.0000000Z",
                "Entries": {
                    "1":  { "Status": "OnTrack",  "X": 0,    "Y": 0,     "Z": 0 },
                    "2":  { "Status": "OnTrack",  "X": 0,    "Y": 0,     "Z": 22270 },
                    "3":  { "Status": "OffTrack", "X": 0,    "Y": 500,   "Z": 0     },
                    "4":  { "Status": "OnTrack",  "X": 1000, "Y": 0,     "Z": 0     },
                    "44": { "Status": "OnTrack",  "X": 2000, "Y": 300,   "Z": 22280 }
                }
                }
            ]
            }
            """)!;
        var clock = new SessionClock(TimestampParser.ParseUtc("2026-10-31T17:32:49.0000000Z"));

        var samples = PositionDataNormalizer.Normalize(payload, clock).ToList();

        var expected = new List<PositionSample>
        {
            new(TimeSpan.FromSeconds(0.5), "2", 0.0, 0.0, 2227.0, "OnTrack"),
            new(TimeSpan.FromSeconds(0.5), "3", 0.0, 50.0, 0.0, "OffTrack"),
            new(TimeSpan.FromSeconds(0.5), "4", 100.0, 0.0, 0.0, "OffTrack"),
            new(TimeSpan.FromSeconds(0.5), "44", 100.0, -250.0, 2227.0, "OnTrack"),

            new(TimeSpan.FromSeconds(1.0), "2", 0.0, 0.0, 2227.0, "OnTrack"),
            new(TimeSpan.FromSeconds(1.0), "3", 0.0, 50.0, 0.0, "OffTrack"),
            new(TimeSpan.FromSeconds(1.0), "4", 100.0, 0.0, 0.0, "OnTrack"),
            new(TimeSpan.FromSeconds(1.0), "44", 200.0, 30.0, 2228.0, "OnTrack"),
        };
        Assert.Equal(expected, samples);
    }
}