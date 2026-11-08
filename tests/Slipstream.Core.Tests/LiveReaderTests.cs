using System.Text;

namespace Slipstream.Core.Tests;

public class LiveReaderTests
{
    [Fact]
    public void ReadFile_ValidFeed()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");

        try
        {
            var text = "{\"kind\":\"snapshot\",\"error\":null,\"result\":{\"DriverList\":{\"1\":{\"Tla\":\"VER\"}},\"Heartbeat\":{\"Utc\":\"2026-01-01T00:00:00.000Z\"}}}\n{\"kind\":\"feed\",\"data\":[\"TimingData\",{\"Lines\":{\"1\":{\"Position\":\"2\"}}},\"2026-01-01T00:00:01.500Z\"],\"t_recv\":1.0}\n{\"kind\":\"feed\",\"data\":[\"CarData.z\",\"AAAA+BBBB==\",\"2026-01-01T00:00:02.000Z\"],\"t_recv\":2.0}";
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var messages = LiveReader.ReadFile(path).ToList();

            Assert.Equal(
                new[] { "DriverList", "Heartbeat", "TimingData", "CarData.z" },
                messages.Select(m => m.Topic));

            Assert.Null(messages[0].Utc);
            Assert.Null(messages[1].Utc);
            Assert.Equal(TimestampParser.ParseUtc("2026-01-01T00:00:01.500Z"), messages[2].Utc);
            Assert.Equal("2", (string?) messages[2].Payload["Lines"]!["1"]!["Position"]);
            Assert.Equal("AAAA+BBBB==", (string?) messages[3].Payload);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFile_EmptySnapshot_YieldsOnlyLaterFeedMessages()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");
        try
        {
            var text =
                "{\"kind\":\"snapshot\",\"error\":null,\"result\":{}}\n" +
                "{\"kind\":\"feed\",\"data\":[\"TimingData\",{\"Lines\":{}},\"2026-01-01T00:00:01.000Z\"]}\n";
            File.WriteAllText(path, text);

            var messages = LiveReader.ReadFile(path).ToList();

            var message = Assert.Single(messages);
            Assert.Equal("TimingData", message.Topic);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFile_InvalidFeed()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");

        try
        {
            var text = "{\"kind\":\"mystery\"}";
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Assert.Throws<FormatException>(() => LiveReader.ReadFile(path).ToList());
        }
        finally
        {
            File.Delete(path);
        }
    }
}