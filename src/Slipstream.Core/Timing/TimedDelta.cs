using System.Text.Json.Nodes;

namespace Slipstream.Core;

public record TimedDelta(TimeSpan SessionTime, string Topic, JsonNode Payload) : ISessionTimed;