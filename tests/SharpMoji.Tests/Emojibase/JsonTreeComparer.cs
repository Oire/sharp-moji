using System.Text.Json.Nodes;

namespace Oire.SharpMoji.Tests.Emojibase;

/// <summary>
/// Compares two JSON trees by value, ignoring property order and formatting.
/// </summary>
/// <remarks>
/// Used by the round-trip test. A plain string comparison would fail on cosmetic differences —
/// upstream's property order is not part of the contract — while comparing deserialized models to
/// each other would prove nothing, since a field the model drops is absent from both sides. Only a
/// comparison against the original JSON can show that nothing was lost.
/// </remarks>
internal static class JsonTreeComparer {
    /// <summary>Yields a human-readable description of every difference found.</summary>
    public static IEnumerable<string> Compare(JsonNode? expected, JsonNode? actual, string path = "$") {
        if (expected is null || actual is null) {
            if (!ReferenceEquals(expected, actual)) {
                yield return $"{path}: expected {Describe(expected)}, found {Describe(actual)}";
            }

            yield break;
        }

        switch (expected) {
            case JsonObject expectedObject:
                if (actual is not JsonObject actualObject) {
                    yield return $"{path}: expected an object, found {Describe(actual)}";
                    yield break;
                }

                foreach (var difference in CompareObjects(expectedObject, actualObject, path)) {
                    yield return difference;
                }

                break;

            case JsonArray expectedArray:
                if (actual is not JsonArray actualArray) {
                    yield return $"{path}: expected an array, found {Describe(actual)}";
                    yield break;
                }

                if (expectedArray.Count != actualArray.Count) {
                    yield return $"{path}: expected {expectedArray.Count} items, found {actualArray.Count}";
                    yield break;
                }

                for (var i = 0; i < expectedArray.Count; i++) {
                    foreach (var difference in Compare(expectedArray[i], actualArray[i], $"{path}[{i}]")) {
                        yield return difference;
                    }
                }

                break;

            default:
                var expectedText = expected.ToJsonString();
                var actualText = actual.ToJsonString();

                if (expectedText != actualText) {
                    yield return $"{path}: expected {expectedText}, found {actualText}";
                }

                break;
        }
    }

    private static IEnumerable<string> CompareObjects(JsonObject expected, JsonObject actual, string path) {
        foreach (var (key, value) in expected) {
            if (!actual.TryGetPropertyValue(key, out var actualValue)) {
                yield return $"{path}.{key}: present upstream but missing after the round trip";
                continue;
            }

            foreach (var difference in Compare(value, actualValue, $"{path}.{key}")) {
                yield return difference;
            }
        }

        foreach (var (key, _) in actual) {
            if (!expected.ContainsKey(key)) {
                yield return $"{path}.{key}: introduced by the round trip but absent upstream";
            }
        }
    }

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "null";
}
