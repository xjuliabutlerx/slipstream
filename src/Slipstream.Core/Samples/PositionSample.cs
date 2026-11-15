namespace Slipstream.Core;

public record PositionSample(TimeSpan SessionTime, string Car, double X, double Y, double Z, string Status) : ISessionTimed;