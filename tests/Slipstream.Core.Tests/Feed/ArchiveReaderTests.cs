using System.Text;

namespace Slipstream.Core.Tests;

public class ArchiveReaderTests
{
    [Fact]
    public void ReadTopic_HandlesBomCrlfAndBlankLines()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonStream");

        try
        {
            var text = "00:00:01.000{\"a\":1}\r\n1.00:00:02.500{\"a\":2}\r\n\r\n";
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var lines = ArchiveReader.ReadTopic(path).ToList();

            Assert.Equal(2, lines.Count);
            Assert.Equal(1000, lines[0].Offset.TotalMilliseconds);
            Assert.Equal(86402500, lines[1].Offset.TotalMilliseconds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadTopic_ThrowsFormatException()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonStream");

        try
        {
            var text = "not a valid JSON line";
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Assert.Throws<FormatException>(() => ArchiveReader.ReadTopic(path).ToList());
        }
        finally
        {
            File.Delete(path);
        }
    }
}