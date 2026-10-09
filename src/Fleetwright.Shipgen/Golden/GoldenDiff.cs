using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Golden;

/// <summary>One difference between a golden tree and ours, at a JSON path like $.build.report.results.length_m.</summary>
public sealed record Difference(string Path, string Golden, string Ours, string Why)
{
    public Difference(string path, JsonNode? golden, JsonNode? ours, string why) : this(path, Text(golden), Text(ours), why) { }

    static string Text(JsonNode? n) => n?.ToJsonString() ?? "null";

    public override string ToString() => $"{Path}: {Why}\n    golden {Clip(Golden)}\n    ours   {Clip(Ours)}";

    static string Clip(string s) => s.Length > 200 ? s[..200] + "..." : s;
}

/// <summary>The golden comparison rules (PORTING.md, Step 1): JSON trees, not bytes. Key sets equal, list lengths
/// equal, strings, booleans and null exact, integers exact; floats within a relative tolerance (an int against a float
/// counts as a float). A difference in anything discrete is a failure to investigate, never a reason to loosen the
/// tolerance.</summary>
public static class GoldenDiff
{
    public const double RelTol = 1e-9;
    public const double AbsTol = 1e-12;

    /// <summary>Every difference (up to max) between golden and ours.</summary>
    public static List<Difference> Compare(JsonNode? golden, JsonNode? ours, int max = 50, string path = "$")
    {
        var result = new List<Difference>();
        Walk(golden, ours, path, result, max);
        return result;
    }

    public static bool NumbersMatch(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
            return double.IsNaN(a) && double.IsNaN(b);
        if (a == b)
            return true;
        double d = Math.Abs(a - b);
        return d <= AbsTol || d <= RelTol * Math.Max(Math.Abs(a), Math.Abs(b));
    }

    static JsonValueKind Kind(JsonNode? n) => n?.GetValueKind() ?? JsonValueKind.Null;

    static void Walk(JsonNode? g, JsonNode? o, string path, List<Difference> result, int max)
    {
        if (result.Count >= max)
            return;
        var (gk, ok) = (Kind(g), Kind(o));
        if (gk != ok && !(gk is JsonValueKind.True or JsonValueKind.False && ok is JsonValueKind.True or JsonValueKind.False))
        {
            result.Add(new Difference(path, g, o, "type"));
            return;
        }
        switch (gk)
        {
            case JsonValueKind.Number:
                {
                    var (gv, ov) = (g!.AsValue(), o!.AsValue());
                    if (JsonFile.IsInteger(gv, out long gi) && JsonFile.IsInteger(ov, out long oi))
                    {
                        if (gi != oi)
                            result.Add(new Difference(path, g, o, "integer"));
                    }
                    else if (!NumbersMatch(gv.GetValue<double>(), ov.GetValue<double>()))
                        result.Add(new Difference(path, g, o, "number"));
                    return;
                }
            case JsonValueKind.True or JsonValueKind.False:
                if (gk != ok)
                    result.Add(new Difference(path, g, o, "boolean"));
                return;
            case JsonValueKind.String:
                if (!string.Equals(g!.GetValue<string>(), o!.GetValue<string>(), StringComparison.Ordinal))
                    result.Add(new Difference(path, g, o, "string"));
                return;
            case JsonValueKind.Object:
                {
                    var (gd, od) = (g!.AsObject(), o!.AsObject());
                    var missing = gd.Select(kv => kv.Key).Where(k => !od.ContainsKey(k)).ToList();
                    var extra = od.Select(kv => kv.Key).Where(k => !gd.ContainsKey(k)).ToList();
                    if (missing.Count > 0 || extra.Count > 0)
                        result.Add(new Difference(path, missing.Count > 0 ? string.Join(", ", missing) : "-",
                            extra.Count > 0 ? string.Join(", ", extra) : "-", "keys (golden only / ours only)"));
                    foreach (var (k, v) in gd)
                        if (od.TryGetPropertyValue(k, out var ov))
                            Walk(v, ov, $"{path}.{k}", result, max);
                    return;
                }
            case JsonValueKind.Array:
                {
                    var (gl, ol) = (g!.AsArray(), o!.AsArray());
                    if (gl.Count != ol.Count)
                    {
                        result.Add(new Difference(path, gl.Count.ToString(), ol.Count.ToString(), "list length"));
                        return;
                    }
                    for (int i = 0; i < gl.Count; i++)
                        Walk(gl[i], ol[i], $"{path}[{i}]", result, max);
                    return;
                }
        }
    }
}
