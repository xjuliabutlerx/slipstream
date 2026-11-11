using System.Globalization;
using System.Text.Json.Nodes;

namespace Slipstream.Core;

public static class SessionWindow
{
    public static SessionClock ClockFrom(JsonNode sessionInfo)
    {
        return new SessionClock(ToUtc(sessionInfo, "StartDate"));
    }

    public static (DateTimeOffset From, DateTimeOffset To) WindowFrom(JsonNode sessionInfo, TimeSpan preRoll, TimeSpan postRoll)
    {
        var startDateOffsetUtc = ToUtc(sessionInfo, "StartDate");
        var endDateOffsetUtc = ToUtc(sessionInfo, "EndDate");
        
        return (startDateOffsetUtc - preRoll, endDateOffsetUtc + postRoll);
    }

    private static DateTimeOffset ToUtc(JsonNode info, string field)
    {
        var infoText = (string?) info[field] ?? throw new FormatException($"The {field} is null");
        var offsetText = (string?) info["GmtOffset"] ?? throw new FormatException("The GmtOffset is null");
        
        var fieldDateTime = DateTime.Parse(infoText, CultureInfo.InvariantCulture);
        var offset = TimeSpan.Parse(offsetText.TrimStart('+'), CultureInfo.InvariantCulture);
        
        return new DateTimeOffset(fieldDateTime, offset);
    }
}