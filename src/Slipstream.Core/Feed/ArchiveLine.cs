using System.Text.Json.Nodes;

namespace Slipstream.Core;

public record ArchiveLine(TimeSpan Offset, JsonNode Payload);