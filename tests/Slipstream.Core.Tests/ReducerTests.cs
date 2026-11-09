using System.Reflection;
using System.Text.Json.Nodes;
using Slipstream.Core;

namespace Slipstream.Core.Tests;

public class ReducerTests
{
    [Fact]
    public void Reducer_ReturnsMergedJson()
    {
        var state = JsonNode.Parse("""{"a": {"x": 1, "y": 2}, "b": 5}""")!;
        var delta = JsonNode.Parse("""{"a": {"y": 3}}""")!;
        var expected = JsonNode.Parse("""{"a": {"x": 1, "y": 3}, "b": 5}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_DeleteOneKey()
    {
        var state = JsonNode.Parse("""{"PitTimes": {"41": {}, "11": {}}}""")!;
        var delta = JsonNode.Parse("""{"PitTimes": {"_deleted": ["41"]}}""")!;
        var expected = JsonNode.Parse("""{"PitTimes": {"11": {}}}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_DeleteTwoKeys()
    {
        var state = JsonNode.Parse("""{"PitTimes": {"23": {}, "55": {}, "44": {}}}""")!;
        var delta = JsonNode.Parse("""{"PitTimes": {"_deleted": ["23", "44"]}}""")!;
        var expected = JsonNode.Parse("""{"PitTimes": {"55": {}}}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_DeleteNonexistentKey()
    {
        var state = JsonNode.Parse("""{"PitTimes": {"23": {}, "55": {}, "44": {}}}""")!;
        var delta = JsonNode.Parse("""{"PitTimes": {"_deleted": ["63"]}}""")!;
        var expected = JsonNode.Parse("""{"PitTimes": {"23": {}, "55": {}, "44": {}}}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_DeleteOneKey_UpdateOther()
    {
        var state = JsonNode.Parse("""{"PitTimes": {"23": {}, "55": {}, "44": {}}}""")!;
        var delta = JsonNode.Parse("""{"PitTimes": {"_deleted": ["44"], "23": {"a": "b"}}}""")!;
        var expected = JsonNode.Parse("""{"PitTimes": {"23": {"a": "b"}, "55": {}}}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_DeleteKey_EmptyState()
    {
        var state = JsonNode.Parse("""{}""")!;
        var delta = JsonNode.Parse("""{"PitTimes": {"_deleted": ["41"]}}""")!;
        var expected = JsonNode.Parse("""{"PitTimes": {}}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_ReturnsMergedJsonArray()
    {
        var state = JsonNode.Parse("""{"Stints": [{"Compound": "SOFT", "TotalLaps": 1}]}""")!;
        var delta = JsonNode.Parse("""{"Stints": {"0": {"TotalLaps": 2}}}""")!;
        var expected = JsonNode.Parse("""{"Stints": [{"Compound": "SOFT", "TotalLaps": 2}]}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_ReturnsPopulatedJsonArray()
    {
        var state = JsonNode.Parse("""{"Stints": []}""")!;
        var delta = JsonNode.Parse("""{"Stints": {"0": {"TotalLaps": 2}}}""")!;
        var expected = JsonNode.Parse("""{"Stints": [{"TotalLaps": 2}]}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_InvalidIndex_ThrowsInvalidDataException()
    {
        var state = JsonNode.Parse("""{"Stints": [{"Compound": "SOFT", "TotalLaps": 1}]}""")!;
        var delta = JsonNode.Parse("""{"Stints": {"5": {"TotalLaps": 2}}}""")!;

        var ex = Assert.Throws<InvalidDataException>(() => Reducer.Apply(state, delta));
        Assert.Contains("5", ex.Message);
    }

    [Fact]
    public void Reducer_ReturnsPopulatedJsonArray_MultipleEntriesInState()
    {
        var state = JsonNode.Parse("""{"Stints": [{"Compound": "SOFT", "TotalLaps": 1}, {"Compound": "MEDIUM", "TotalLaps": 2}]}""")!;
        var delta = JsonNode.Parse("""{"Stints": {"1": {"TotalLaps": 3}}}""")!;
        var expected = JsonNode.Parse("""{"Stints": [{"Compound": "SOFT", "TotalLaps": 1}, {"Compound": "MEDIUM", "TotalLaps": 3}]}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

    [Fact]
    public void Reducer_AddToAlreadyPopulatedStateArray()
    {
        var state = JsonNode.Parse("""{"Stints": [{"Compound": "SOFT", "TotalLaps": 1}, {"Compound": "MEDIUM", "TotalLaps": 2}]}""")!;
        var delta = JsonNode.Parse("""{"Stints": {"2": {"Compound": "HARD", "TotalLaps": 1}}}""")!;
        var expected = JsonNode.Parse("""{"Stints": [{"Compound": "SOFT", "TotalLaps": 1}, {"Compound": "MEDIUM", "TotalLaps": 2}, {"Compound": "HARD", "TotalLaps": 1}]}""")!;

        var result = Reducer.Apply(state, delta);

        Assert.Equal(expected.ToJsonString(), result.ToJsonString());
    }

}