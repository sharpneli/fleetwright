

namespace Fleetwright.Shipgen.Golden;

/// <summary>One difference between a golden tree and ours, at a JSON path like $.build.report.results.length_m.</summary>
public sealed record Difference(string Path, object? Golden, object? Ours, string Why)
{
    public override string ToString() =>
        $"{Path}: {Why}\n    golden {Clip(Py.Repr(Golden))}\n    ours   {Clip(Py.Repr(Ours))}";

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
    public static List<Difference> Compare(object? golden, object? ours, int max = 50, string path = "$")
    {
        var out_ = new List<Difference>();
        Walk(golden, ours, path, out_, max);
        return out_;
    }

    static bool IsNum(object? v) => v is long or int or double;

    public static bool NumbersMatch(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
            return double.IsNaN(a) && double.IsNaN(b);
        if (a == b)
            return true;
        double d = Math.Abs(a - b);
        return d <= AbsTol || d <= RelTol * Math.Max(Math.Abs(a), Math.Abs(b));
    }

    static void Walk(object? g, object? o, string path, List<Difference> out_, int max)
    {
        if (out_.Count >= max)
            return;
        if (IsNum(g) && IsNum(o))
        {
            if (g is double || o is double)
            {
                if (!NumbersMatch(Py.ToDouble(g), Py.ToDouble(o)))
                    out_.Add(new Difference(path, g, o, "number"));
            }
            else if (Py.ToLong(g) != Py.ToLong(o))
                out_.Add(new Difference(path, g, o, "integer"));
            return;
        }
        switch (g)
        {
            case null:
                if (o is not null)
                    out_.Add(new Difference(path, g, o, "type"));
                return;
            case bool gb:
                if (o is not bool ob || ob != gb)
                    out_.Add(new Difference(path, g, o, o is bool ? "boolean" : "type"));
                return;
            case string gs:
                if (o is not string os || !string.Equals(gs, os, StringComparison.Ordinal))
                    out_.Add(new Difference(path, g, o, o is string ? "string" : "type"));
                return;
            case PyDict gd:
                {
                    if (o is not PyDict od)
                    {
                        out_.Add(new Difference(path, g, o, "type"));
                        return;
                    }
                    var missing = gd.Keys.Where(k => !od.Has(k)).ToList();
                    var extra = od.Keys.Where(k => !gd.Has(k)).ToList();
                    if (missing.Count > 0 || extra.Count > 0)
                        out_.Add(new Difference(path, missing.Count > 0 ? string.Join(", ", missing) : null,
                            extra.Count > 0 ? string.Join(", ", extra) : null, "keys (golden only / ours only)"));
                    foreach (var kv in gd)
                        if (od.Has(kv.Key))
                            Walk(kv.Value, od[kv.Key], $"{path}.{kv.Key}", out_, max);
                    return;
                }
            case List<object?> gl:
                {
                    if (o is not List<object?> ol)
                    {
                        out_.Add(new Difference(path, g, o, "type"));
                        return;
                    }
                    if (gl.Count != ol.Count)
                    {
                        out_.Add(new Difference(path, gl.Count, ol.Count, "list length"));
                        return;
                    }
                    for (int i = 0; i < gl.Count; i++)
                        Walk(gl[i], ol[i], $"{path}[{i}]", out_, max);
                    return;
                }
            default:
                out_.Add(new Difference(path, g, o, "type"));
                return;
        }
    }
}
