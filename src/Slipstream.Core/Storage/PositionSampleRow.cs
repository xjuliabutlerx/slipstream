namespace Slipstream.Core;

public class PositionSampleRow
{
    public double SessionSeconds { get; set; }
    public string Car { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string Status { get; set; } = "";
}