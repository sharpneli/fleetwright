using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Fleetwright.Shipgen.Render.Golden;

/// <summary>Compares two SVGs as drawings, not text: the element trees in order, each element's attributes with
/// clip-path references replaced by what they clip to, path data made absolute, colours canonical (#rrggbb) and
/// numbers within Tol (shipgen.f rounds to 3 decimals, so a last-digit flip is allowed). Python's SVG (golden/svg)
/// against SvgWriter's.</summary>
public static partial class SvgDiff
{
    public const double Tol = 1.5e-3;

    sealed record El(string Tag, Dictionary<string, List<object>> Attrs, List<El> Kids, string Text);

    [GeneratedRegex(@"[A-Za-z]|-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex Tok();

    static List<object> Tokens(string s) =>
        [.. Tok().Matches(s).Select(m => char.IsLetter(m.Value[0])
            ? (object)m.Value : double.Parse(m.Value, CultureInfo.InvariantCulture))];

    /// <summary>Path data as absolute commands: [letter, numbers..., letter, ...] (M L A C Z, relative l made
    /// absolute).</summary>
    static List<object> PathTokens(string d)
    {
        var t = Tok().Matches(d).Select(m => m.Value).ToList();
        var result = new List<object>();
        double cx = 0, cy = 0, sx = 0, sy = 0;
        int i = 0;
        string cmd = "";
        double N() => double.Parse(t[i++], CultureInfo.InvariantCulture);
        while (i < t.Count)
        {
            if (char.IsLetter(t[i][0]))
                cmd = t[i++];
            switch (cmd)
            {
                case "M" or "L":
                    (cx, cy) = (N(), N());
                    if (cmd == "M")
                        (sx, sy) = (cx, cy);
                    result.AddRange([cmd, cx, cy]);
                    if (cmd == "M")
                        cmd = "L";
                    break;
                case "l":
                    cx += N();
                    cy += N();
                    result.AddRange(["L", cx, cy]);
                    break;
                case "A":
                    {
                        double rx = N(), ry = N(), rot = N(), large = N(), sweep = N();
                        (cx, cy) = (N(), N());
                        result.AddRange(["A", rx, ry, rot, large, sweep, cx, cy]);
                        break;
                    }
                case "C":
                    {
                        double x1 = N(), y1 = N(), x2 = N(), y2 = N();
                        (cx, cy) = (N(), N());
                        result.AddRange(["C", x1, y1, x2, y2, cx, cy]);
                        break;
                    }
                case "Z" or "z":
                    (cx, cy) = (sx, sy);
                    result.Add("Z");
                    break;
                default:
                    throw new FormatException($"path command {cmd} in {d}");
            }
        }
        return result;
    }

    static string Colour(string v)
    {
        v = v.Trim();
        if (v.StartsWith("rgb(", StringComparison.Ordinal))
        {
            var p = v[4..^1].Split(',').Select(s => byte.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return new Rgb(p[0], p[1], p[2]).ToString();
        }
        return v.StartsWith('#') ? Rgb.Parse(v).ToString() : v;
    }

    static El Parse(XElement e, Dictionary<string, XElement> clips)
    {
        var attrs = new Dictionary<string, List<object>>(StringComparer.Ordinal);
        foreach (var a in e.Attributes())
        {
            string k = a.Name.LocalName, v = a.Value;
            if (k is "id" or "xmlns")
                continue;
            attrs[k] = k switch
            {
                "d" => PathTokens(v),
                "fill" or "stroke" => [Colour(v)],
                "clip-path" => [Clip(v, clips)],
                "font-family" or "font-weight" or "text-anchor" or "dominant-baseline" or "stroke-linejoin" or "stroke-linecap"
                    or "shape-rendering" => [v],
                _ => Tokens(v),
            };
        }
        string tag = e.Name.LocalName;
        if (tag is "circle" or "ellipse")    // the defaults Python left out
        {
            attrs.TryAdd("cx", [0.0]);
            attrs.TryAdd("cy", [0.0]);
        }
        var kids = e.Elements().Where(c => c.Name.LocalName is not ("defs" or "clipPath")).Select(c => Parse(c, clips)).ToList();
        return new El(tag, attrs, kids, e.Elements().Any() ? "" : e.Value);
    }

    static string Clip(string v, Dictionary<string, XElement> clips)
    {
        var id = v.Trim()[5..^1];   // url(#id)
        var c = clips[id];
        return "clip[" + string.Join(";", c.Elements().Select(k => Show(Parse(k, clips)))) + "]";
    }

    static string Show(El e) =>
        $"<{e.Tag} " + string.Join(" ", e.Attrs.OrderBy(a => a.Key, StringComparer.Ordinal)
            .Select(a => $"{a.Key}={string.Join(",", a.Value.Select(v => v is double d ? SvgWriter.F(d) : v.ToString()))}")) + ">";

    static El Load(string svg)
    {
        var doc = XDocument.Parse(svg);
        var clips = doc.Descendants().Where(e => e.Name.LocalName == "clipPath")
            .ToDictionary(e => (string)e.Attribute("id")!, StringComparer.Ordinal);
        return Parse(doc.Root!, clips);
    }

    /// <summary>Up to max differences between the golden SVG and ours, each naming the element path.</summary>
    public static List<string> Compare(string golden, string ours, int max = 10)
    {
        var result = new List<string>();
        Walk(Load(golden), Load(ours), "svg", result, max);
        return result;
    }

    static bool Same(List<object> a, List<object> b, bool clipped = false)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] is double x && b[i] is double y)
            {
                if (Math.Abs(x - y) > Tol + 1e-9 * Math.Abs(x))
                    return false;
            }
            else if (a[i] is string s && b[i] is string t && s.StartsWith("clip[", StringComparison.Ordinal) && !clipped)
            {
                if (!SameClip(s, t))
                    return false;
            }
            else if (!Equals(a[i], b[i]))
                return false;
        }
        return true;
    }

    /// <summary>Clip contents compare token by token: words exactly, numbers with the tolerance.</summary>
    static bool SameClip(string a, string b) => Same(Tokens(a), Tokens(b), clipped: true);

    static void Walk(El g, El o, string path, List<string> result, int max)
    {
        if (result.Count >= max)
            return;
        if (g.Tag != o.Tag)
        {
            result.Add($"{path}: element {g.Tag} vs ours {o.Tag}\n    golden {Clip(Show(g))}\n    ours   {Clip(Show(o))}");
            return;
        }
        foreach (var k in g.Attrs.Keys.Union(o.Attrs.Keys).Order(StringComparer.Ordinal))
        {
            bool hg = g.Attrs.TryGetValue(k, out var a), ho = o.Attrs.TryGetValue(k, out var b);
            if (!hg || !ho || !Same(a!, b!))
            {
                result.Add($"{path}@{k}: {(hg ? "" : "missing in golden")}{(ho ? "" : "missing in ours")}\n    golden {Clip(Show(g))}\n    ours   {Clip(Show(o))}");
                if (result.Count >= max)
                    return;
            }
        }
        if (g.Text.Trim() != o.Text.Trim())
            result.Add($"{path}: text {g.Text} vs ours {o.Text}");
        int n = Math.Min(g.Kids.Count, o.Kids.Count);
        for (int i = 0; i < n && result.Count < max; i++)
            Walk(g.Kids[i], o.Kids[i], $"{path}/{g.Kids[i].Tag}[{i}]", result, max);
        if (g.Kids.Count != o.Kids.Count && result.Count < max)
        {
            var extra = g.Kids.Count > n ? g.Kids[n] : o.Kids[n];
            result.Add($"{path}: {g.Kids.Count} children vs ours {o.Kids.Count}; first unmatched ({(g.Kids.Count > n ? "golden" : "ours")}): {Clip(Show(extra))}");
        }
    }

    static string Clip(string s) => s.Length > 400 ? s[..400] + "..." : s;
}
