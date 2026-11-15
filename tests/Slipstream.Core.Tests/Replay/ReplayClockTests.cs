namespace Slipstream.Core.Tests;

public class ReplayClockTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static readonly RedFlagPeriod Stoppage = new(S(100), S(400));

    private static ReplayClock ClockWithStoppage(double start)
    {
        var clock = new ReplayClock(start: S(start), stoppages: new[] { Stoppage }, hold: S(5)); // speed 1
        clock.Play();
        return clock;
    }

    [Fact]
    public void Advance_ReachingStoppage_StopsAtItsStart()
    {
        var clock = ClockWithStoppage(90);
        var (from, to) = clock.Advance(S(20));        // would reach 110
        Assert.Equal((S(90), S(100)), (from, to));
        Assert.Equal(S(100), clock.SessionTime);
    }

    [Fact]
    public void Advance_DuringHold_DoesNotMove()
    {
        var clock = ClockWithStoppage(90);
        clock.Advance(S(20));                          // now holding at 100
        var (from, to) = clock.Advance(S(2));
        Assert.Equal((S(100), S(100)), (from, to));
        Assert.False(clock.LastAdvanceSkipped);
    }

    [Fact]
    public void Advance_AfterHold_JumpsToRestart()
    {
        var clock = ClockWithStoppage(90);
        clock.Advance(S(20));
        clock.Advance(S(2));
        var (from, to) = clock.Advance(S(3));          // 2 + 3 = 5 s of hold elapsed
        Assert.Equal((S(100), S(400)), (from, to));
        Assert.Equal(S(400), clock.SessionTime);
        Assert.True(clock.LastAdvanceSkipped);
    }

    [Fact]
    public void Advance_AfterJump_IsNormalMovement_NotSkipped()
    {
        var clock = ClockWithStoppage(90);
        clock.Advance(S(20));     // reaches 100, starts holding
        clock.Advance(S(5));      // hold over: jumps to 400 (LastAdvanceSkipped is true here)

        var (from, to) = clock.Advance(S(10));   // speed 1, so 10 s of session time

        Assert.Equal((S(400), S(410)), (from, to));
        Assert.False(clock.LastAdvanceSkipped);
        Assert.False(clock.IsHolding);
    }

    [Fact]
    public void Advance_ClockWithEnd_NormalTickIsNotSkipped()
    {
        var clock = new ReplayClock(start: S(0), end: S(100));   // speed 1, no stoppages
        clock.Play();

        clock.Advance(S(10));

        Assert.False(clock.LastAdvanceSkipped);
    }

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

    [Fact]
    public void Seek_DuringHold_CancelsTheHold()
    {
        var clock = ClockWithStoppage(90);
        clock.Advance(S(20));                 // holding at 100
        Assert.True(clock.IsHolding);

        clock.Seek(S(50));

        Assert.False(clock.IsHolding);
        Assert.Equal((S(50), S(60)), clock.Advance(S(10)));   // normal movement, not a held tick
        Assert.False(clock.LastAdvanceSkipped);
    }

    [Fact]
    public void Seek_BackBeforeStoppage_HoldsAgainOnReachingIt()
    {
        var clock = ClockWithStoppage(90);
        clock.Advance(S(20));
        clock.Advance(S(5));                  // jumped to 400

        clock.Seek(S(90));
        var range = clock.Advance(S(20));

        Assert.Equal((S(90), S(100)), range);
        Assert.True(clock.IsHolding);
    }

    [Fact]
    public void Seek_InsideStoppage_PlaysNormally()
    {
        var clock = ClockWithStoppage(0);

        clock.Seek(S(200));
        var range = clock.Advance(S(10));

        Assert.Equal((S(200), S(210)), range);
        Assert.False(clock.IsHolding);
        Assert.False(clock.LastAdvanceSkipped);
    }

    [Fact]
    public void Seek_PastEnd_ClampsToEnd()
    {
        var clock = new ReplayClock(start: S(0), end: S(100));

        clock.Seek(S(500));

        Assert.Equal(S(100), clock.SessionTime);
        Assert.True(clock.IsFinished);
    }
}