namespace Slipstream.Core;

public static class SampleWindow
{
    public static IEnumerable<ChannelSample> Within(IEnumerable<ChannelSample> samples, TimeSpan from, TimeSpan to)
    {
        foreach (var sample in samples)
        {
            if (sample.SessionTime >= from && sample.SessionTime <= to)
            {
                yield return sample;
            }
        }
    }
}