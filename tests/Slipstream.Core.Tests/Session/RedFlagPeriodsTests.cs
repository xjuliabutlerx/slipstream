using System.Text.Json.Nodes;

namespace Slipstream.Core.Tests;

public class RedFlagPeriodsTests
{
    private static TimedDelta Status(double seconds, string status) =>
        new(TimeSpan.FromSeconds(seconds), "SessionStatus",
            JsonNode.Parse($$"""{"Status":"{{status}}"}""")!);

    private static RedFlagPeriod Period(double start, double end) =>
        new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));

    [Fact]
    public void From_AbortedThenStarted_IsOnePeriod()
    {
        var result = RedFlagPeriods.From(new[] { Status(10, "Started"), Status(100, "Aborted"), Status(400, "Started") });
        Assert.Equal(new[] { Period(100, 400) }, result);
    }

    [Fact]
    public void From_NoRedFlag_IsEmpty()
    {
        var result = RedFlagPeriods.From(new[] { Status(10, "Started"), Status(500, "Finished"), Status(520, "Finalised") });
        Assert.Empty(result);
    }

    [Fact]
    public void From_TwoRedFlags_AreInOrder()
    {
        var result = RedFlagPeriods.From(new[]
        {
            Status(100, "Aborted"), Status(200, "Started"),
            Status(300, "Aborted"), Status(450, "Started"),
        });
        Assert.Equal(new[] { Period(100, 200), Period(300, 450) }, result);
    }

    [Fact]
    public void From_AbortedWithoutRestart_IsIgnored()
    {
        var result = RedFlagPeriods.From(new[] { Status(100, "Aborted"), Status(200, "Finished") });
        Assert.Empty(result);
    }

    [Fact]
    public void From_RepeatedAborted_StartsAtTheFirst()
    {
        var result = RedFlagPeriods.From(new[] { Status(100, "Aborted"), Status(150, "Aborted"), Status(400, "Started") });
        Assert.Equal(new[] { Period(100, 400) }, result);
    }

    [Fact]
    public void From_LineWithoutStatus_IsSkipped()
    {
        var other = new TimedDelta(TimeSpan.FromSeconds(120), "SessionStatus", JsonNode.Parse("""{"Other":1}""")!);
        var result = RedFlagPeriods.From(new[] { Status(100, "Aborted"), other, Status(400, "Started") });
        Assert.Equal(new[] { Period(100, 400) }, result);
    }
}