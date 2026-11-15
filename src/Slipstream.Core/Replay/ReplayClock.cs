namespace Slipstream.Core;

public sealed class ReplayClock
{
    public ReplayClock(TimeSpan start, TimeSpan? end = null, IReadOnlyList<RedFlagPeriod>? stoppages = null, TimeSpan? hold = null)
    {
        SessionTime = start;
        End = end;
        _stoppages = stoppages ?? Array.Empty<RedFlagPeriod>();
        _hold = hold ?? TimeSpan.Zero;
    }

    private readonly IReadOnlyList<RedFlagPeriod> _stoppages;
    private readonly TimeSpan _hold;
    private RedFlagPeriod? _holdingAt;      // null = not holding
    private TimeSpan _holdElapsed;
    public TimeSpan SessionTime { get; private set; }
    public double Speed { get; set; } = 1.0;
    public bool IsPlaying { get; private set; }
    public TimeSpan? End { get; }              // null for live
    public bool IsFinished => End.HasValue && SessionTime >= End.Value;
    public bool LastAdvanceSkipped { get; private set; }
    public bool IsHolding => _holdingAt != null;

    public void Play() => IsPlaying = true;
    public void Pause() => IsPlaying = false;
    public void Seek(TimeSpan sessionTime)
    {
        SessionTime = End is { } end && sessionTime > end ? end : sessionTime;
        _holdingAt = null;
        _holdElapsed = TimeSpan.Zero;
        LastAdvanceSkipped = false;
    }

    public (TimeSpan From, TimeSpan To) Advance(TimeSpan realElapsed)
    {
        LastAdvanceSkipped = false;
        if (!IsPlaying || IsFinished)
        {
            // If paused or finished, return SessionTime objects set to 0
            return (SessionTime, SessionTime);
        }
        else if (_holdingAt is { } held)
        {
            // Already holding at a stoppage: count real time; when the hold is over, jump to the restart
            _holdElapsed += realElapsed;

            if (_holdElapsed < _hold)
            {
                return (SessionTime, SessionTime);
            }

            // Hold over: skip to the stoppage's End and flag it as a skip
            var jumpFrom = SessionTime;
            SessionTime = held.End;
            _holdingAt = null;
            LastAdvanceSkipped = true;
            return (jumpFrom, held.End);
        }
        else
        {
            var from = SessionTime;
            var to = from + realElapsed * Speed;

            // This tick crosses a stoppage's Start: stop the playhead at Start and begin holding
            var crossed = _stoppages.FirstOrDefault(s => from < s.Start && s.Start <= to);
            if (crossed != null)
            {
                to = crossed.Start;
                _holdingAt = crossed;
                _holdElapsed = TimeSpan.Zero;
            }

            // Never run past the end of the data
            if (End != null)
            {
                to = End < to ? (TimeSpan) End : to;
            }

            SessionTime = to;
            return (from, to);
        }
    }
}