using System.Text.Json.Nodes;

namespace Slipstream.Core;

public class SessionReducer
{
    private readonly Dictionary<string, JsonNode> _states = new();
    public IEnumerable<string> Topics => _states.Keys;

    public void Feed(string topic, JsonNode payload)
    {
        var isKeyframe = payload is JsonObject obj && (bool?) obj["_kf"] == true;
        var existing = isKeyframe
            ? new JsonObject()
            : _states.GetValueOrDefault(topic) ?? new JsonObject();
        _states[topic] = Reducer.Apply(existing, payload);
    }

    public JsonNode? GetState(string topic)
    {
        return _states.GetValueOrDefault(topic);
    }

}