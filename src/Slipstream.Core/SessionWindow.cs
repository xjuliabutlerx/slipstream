using System.Globalization;
using System.Text.Json.Nodes;

namespace Slipstream.Core;

public static class SessionWindow
{
    public static SessionClock ClockFrom(JsonNode sessionInfo)
    {
        var startText = (string) sessionInfo["StartDate"]! ?? throw new FormatException("The StartDate is null");
        var offsetText = (string) sessionInfo["GmtOffset"]! ?? throw new FormatException("The GmtOffset is null");

        var localStart = DateTime.Parse(startText, CultureInfo.InvariantCulture);
        var offset = TimeSpan.Parse(offsetText.TrimStart('+'), CultureInfo.InvariantCulture);

        return new SessionClock(new DateTimeOffset(localStart, offset));
    }
}