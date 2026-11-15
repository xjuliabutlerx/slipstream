namespace Slipstream.Core;

public sealed record LeaderboardRow(
    int Position, string Car, string Tla, string TeamColour,
    string Gap, string Interval, bool InPit);