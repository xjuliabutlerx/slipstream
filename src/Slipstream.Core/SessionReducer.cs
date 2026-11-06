using System.Text.Json.Nodes;

namespace Slipstream.Core;

public class SessionReducer
{
    private readonly Dictionary<string, JsonNode> _states = new();

    public void Feed(string topic, JsonNode payload)
    {
        var existing = _states.GetValueOrDefault(topic) ?? new JsonObject();
        _states[topic] = Reducer.Apply(existing, payload);
    }

    public JsonNode? GetState(string topic)
    {
        return _states.GetValueOrDefault(topic);
    }

}