namespace Slipstream.Core;

public class ChannelSampleRow
{
    public double SessionSeconds { get; set; }
    public string Car { get; set; } = "";
    public string Channel { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
}