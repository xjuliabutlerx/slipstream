namespace Slipstream.Core;

public sealed class ReplayClock
{
    public ReplayClock(TimeSpan start, TimeSpan? end = null)
    {
        SessionTime = start;
        End = end;
    }

    public TimeSpan SessionTime { get; private set; }
    public double Speed { get; set; } = 1.0;
    public bool IsPlaying { get; private set; }
    public TimeSpan? End { get; }              // null for live
    public bool IsFinished => End.HasValue && SessionTime >= End.Value;

    public void Play() => IsPlaying = true;
    public void Pause() => IsPlaying = false;
    public void Seek(TimeSpan sessionTime)
    {
        SessionTime = sessionTime;
    }

    public (TimeSpan From, TimeSpan To) Advance(TimeSpan realElapsed)
    {
        if (!IsPlaying || IsFinished)
        {
            return (SessionTime, SessionTime);
        }
        else
        {
            var from = SessionTime;
            var to = from + realElapsed * Speed;

            if (End != null)
            {
                to = End < to ? (TimeSpan) End : to;
            }

            SessionTime = to;
            return (from, to);
        }
    }
}