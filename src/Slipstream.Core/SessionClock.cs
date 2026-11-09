namespace Slipstream.Core;

public record SessionClock(DateTimeOffset StartUtc)
{
    public TimeSpan ToSessionTime(DateTimeOffset utc) => utc - StartUtc;
}