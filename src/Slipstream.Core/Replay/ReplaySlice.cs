namespace Slipstream.Core;

public static class ReplaySlice
{
    public static IReadOnlyList<T> Between<T>(IReadOnlyList<T> items, TimeSpan from, TimeSpan to) where T : ISessionTimed
    {
        if (from > to)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "`to` must not be earlier than `from`.");
        }

        var start = UpperBound(items, from);
        var end = UpperBound(items, to);

        var result = new List<T>();
        for (var i = start; i < end; i++)
        {
            result.Add(items[i]);
        }
        return result;
    }

    // First index whose SessionTime > `time` or items.Count if there is none
    private static int UpperBound<T>(IReadOnlyList<T> items, TimeSpan time) where T : ISessionTimed
    {
        var lo = 0;
        var hi = items.Count;

        // Binary search
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (items[mid].SessionTime <= time)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return hi;
    }
}