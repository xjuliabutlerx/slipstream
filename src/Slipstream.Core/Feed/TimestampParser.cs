using System.Globalization;

namespace Slipstream.Core;

public static class TimestampParser
{

    public static TimeSpan Parse(string text)
    {
        return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset ParseUtc(string text)
    {
        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
    }

}