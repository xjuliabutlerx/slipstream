using System.Text.Json.Nodes;

namespace Slipstream.Core;

public record FeedMessage(string Topic, JsonNode Payload, DateTimeOffset? Utc);