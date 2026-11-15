namespace Slipstream.Core;

public static class SampleWindow
{
    public static IEnumerable<T> Within<T>(IEnumerable<T> samples, TimeSpan from, TimeSpan to) where T : ISessionTimed
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