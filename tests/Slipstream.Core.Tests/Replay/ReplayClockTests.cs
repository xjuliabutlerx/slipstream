namespace Slipstream.Core.Tests;

public class ReplayClockTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Advance_AtNormalSpeed_ReturnsTheElapsedRangeAndMovesTime()
    {
        var clock = new ReplayClock(start: S(10));
        clock.Play();

        var (from, to) = clock.Advance(S(2));

        Assert.Equal(S(10), from);          // starts from the current time, not from 0
        Assert.Equal(S(12), to);
        Assert.Equal(S(12), clock.SessionTime);
    }

    [Fact]
    public void Advance_AtSixtyTimesSpeed_CoversSixtyTimesTheSessionTime()
    {
        var clock = new ReplayClock(start: S(10)) { Speed = 60 };
        clock.Play();

        var (from, to) = clock.Advance(S(1));

        Assert.Equal(S(10), from);
        Assert.Equal(S(70), to);
    }

    [Fact]
    public void Advance_AfterSpeedChange_UsesTheNewSpeedWithoutJumping()
    {
        var clock = new ReplayClock(start: S(10));
        clock.Play();

        clock.Advance(S(1));                // 10 -> 11 at 1x
        clock.Speed = 10;
        var (from, to) = clock.Advance(S(1));

        Assert.Equal(S(11), from);          // continues where it was
        Assert.Equal(S(21), to);            // 1 s of real time at 10x
    }

    [Fact]
    public void Advance_WhilePaused_ReturnsAnEmptyRangeAndDoesNotMove()
    {
        var clock = new ReplayClock(start: S(10));   // never played

        var (from, to) = clock.Advance(S(5));

        Assert.Equal(S(10), from);
        Assert.Equal(S(10), to);
        Assert.Equal(S(10), clock.SessionTime);
    }

    [Fact]
    public void Advance_AfterPause_StopsMoving()
    {
        var clock = new ReplayClock(start: S(0));
        clock.Play();
        clock.Advance(S(3));
        clock.Pause();

        var (from, to) = clock.Advance(S(5));

        Assert.Equal(S(3), from);
        Assert.Equal(S(3), to);
    }

    [Fact]
    public void Advance_ConsecutiveCalls_TileWithNoGapsOrOverlap()
    {
        var clock = new ReplayClock(start: S(0));
        clock.Play();

        var first  = clock.Advance(S(0.3));
        var second = clock.Advance(S(0.5));
        clock.Speed = 4;
        var third  = clock.Advance(S(0.2));

        Assert.Equal(first.To, second.From);
        Assert.Equal(second.To, third.From);
    }

    [Fact]
    public void Seek_MovesTheTimeAndTheNextAdvanceStartsThere()
    {
        var clock = new ReplayClock(start: S(0));
        clock.Play();

        clock.Seek(S(100));
        var (from, to) = clock.Advance(S(1));

        Assert.Equal(S(100), from);
        Assert.Equal(S(101), to);
    }
}