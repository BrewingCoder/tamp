using System.Text.Json;
using Xunit;

namespace Tamp.Sarif.Tests;

/// <summary>
/// Structural JSON equality for tests. Used because SARIF is a JSON
/// document format — the round-trip contract is "same JSON", not "same
/// C# object graph" (record equality treats IReadOnlyList&lt;T&gt; as
/// reference equality, which is the wrong invariant here).
/// </summary>
internal static class JsonAssert
{
    public static void Equivalent(string expected, string actual)
    {
        using var docExpected = JsonDocument.Parse(expected);
        using var docActual = JsonDocument.Parse(actual);
        if (!DeepEquals(docExpected.RootElement, docActual.RootElement))
        {
            Assert.Fail($"JSON documents differ.\n--- expected ---\n{expected}\n--- actual ---\n{actual}");
        }
    }

    private static bool DeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        return a.ValueKind switch
        {
            JsonValueKind.Object => ObjectsEqual(a, b),
            JsonValueKind.Array => ArraysEqual(a, b),
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => false,
        };
    }

    private static bool ObjectsEqual(JsonElement a, JsonElement b)
    {
        var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        if (aProps.Count != bProps.Count) return false;
        foreach (var (key, valueA) in aProps)
        {
            if (!bProps.TryGetValue(key, out var valueB)) return false;
            if (!DeepEquals(valueA, valueB)) return false;
        }
        return true;
    }

    private static bool ArraysEqual(JsonElement a, JsonElement b)
    {
        if (a.GetArrayLength() != b.GetArrayLength()) return false;
        using var aIt = a.EnumerateArray().GetEnumerator();
        using var bIt = b.EnumerateArray().GetEnumerator();
        while (aIt.MoveNext() && bIt.MoveNext())
        {
            if (!DeepEquals(aIt.Current, bIt.Current)) return false;
        }
        return true;
    }
}
