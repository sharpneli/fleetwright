using System.Text.Json.Nodes;
using Fleetwright.Shipgen.Golden;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The typed design reads every design file and writes it back as given (a lone secondary battery comes back
/// as a list of one).</summary>
public class DesignTests
{
    static readonly string Root = Paths.Shipgen();

    [Fact]
    public void EveryDesignRoundTrips()
    {
        var failures = new List<string>();
        foreach (var c in GoldenCases.Load(Root))
        {
            var text = File.ReadAllText(c.DesignPath);
            var given = JsonNode.Parse(text)!.AsObject();
            foreach (var k in new[] { "main", "secondary" })
                if (given[k] is JsonObject one)
                {
                    given.Remove(k);
                    given[k] = new JsonArray(one);
                }
            var back = JsonNode.Parse(Design.Parse(text).ToJson())!;
            if (!JsonNode.DeepEquals(Normalize(given), Normalize(back)))
                failures.Add($"{c.Name}:\n  given {given.ToJsonString()}\n  back  {back.ToJsonString()}");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(3)));
    }

    /// <summary>Every key the designs use is a property: none lands in a type's Extra (the unknown keys).</summary>
    [Fact]
    public void NoDesignHasUnknownKeys()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(object? o, string path)
        {
            switch (o)
            {
                case null or string or ValueType:
                    return;
                case System.Collections.IEnumerable list:
                    foreach (var x in list)
                        Walk(x, path + "[]");
                    return;
            }
            foreach (var p in o.GetType().GetProperties())
            {
                if (p.GetIndexParameters().Length > 0 || p.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true).Length > 0)
                    continue;
                var v = p.GetValue(o);
                if (p.Name == "Extra" && v is System.Collections.IDictionary { Count: > 0 } extra)
                    foreach (var k in extra.Keys)
                        found.Add($"{path}.{k}");
                else if (p.Name != "Extra")
                    Walk(v, path + "." + p.Name);
            }
        }
        foreach (var c in GoldenCases.Load(Root))
            Walk(Design.Load(c.DesignPath), "");
        Assert.True(found.Count == 0, "unknown keys: " + string.Join(", ", found));
    }

    /// <summary>Numbers as doubles (an int and the same float are one number), keys sorted.</summary>
    static JsonNode? Normalize(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => KeyValuePair.Create(kv.Key, Normalize(kv.Value)))),
        JsonArray a => new JsonArray(a.Select(Normalize).ToArray()),
        JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.Number => JsonValue.Create(v.GetValue<double>()),
        _ => n?.DeepClone(),
    };
}
