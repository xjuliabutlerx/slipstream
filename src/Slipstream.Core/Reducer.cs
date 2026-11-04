using System.Text.Json.Nodes;

namespace Slipstream.Core;

public static class Reducer
{
    public static JsonNode Apply(JsonNode state, JsonNode delta)
    {
        if (state is JsonArray stateArr && delta is JsonObject deltaArrObj)
        {
            foreach (var (indexText, itemDelta) in deltaArrObj)
            {
                var index = int.Parse(indexText);

                if (index >= 0 && index < stateArr.Count && itemDelta is JsonObject)
                {
                    Apply(stateArr[index]!, itemDelta);
                }
                else if (index == stateArr.Count)
                {
                    stateArr.Add(Apply(new JsonObject(), itemDelta!));
                }
                else if (index > stateArr.Count)
                {
                    throw new InvalidDataException($"Index {index} is out of range for a list of {stateArr.Count} items.");
                }
                else
                {
                    stateArr[index] = Apply(new JsonArray(), itemDelta?.DeepClone()!);
                }
            }
            return stateArr;
        }
        else if (state is JsonObject stateObj && delta is JsonObject deltaObj)
        {
            foreach (var (key, deltaValue) in deltaObj)
            {
                if (key == "_deleted" && deltaValue is JsonArray keysToRemove)
                {
                    foreach (var k in keysToRemove)
                    {
                        stateObj.Remove((string?) k);
                    }
                }
                else if (stateObj[key] is JsonObject or JsonArray && deltaValue is JsonObject)
                {
                    Apply(stateObj[key]!, deltaValue);
                }
                else
                {
                    stateObj[key] = Apply(new JsonObject(), deltaValue?.DeepClone()!);
                }
            }
            return stateObj;
        }

        return delta.DeepClone();
    }
}