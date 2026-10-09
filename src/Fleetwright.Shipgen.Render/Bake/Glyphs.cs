using System.Numerics;
using System.Reflection;

namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>Text as outlines: DejaVu Sans Bold's glyphs for printable ASCII (Data/dejavu-sans-bold.glyphs.json,
/// extracted with fontTools; licence alongside), placed the way cairosvg placed the SVG's text: anchored at the
/// middle of the ink, the baseline lowered by half of ascent minus descent ("central"), each letter advanced by its
/// width.</summary>
public static class Glyphs
{
    /// <summary>A glyph: its advance, its ink's x extent and its outline as path operations (M x y, L x y, Q qx qy x y).</summary>
    sealed record Glyph(double Adv, double XMin, double XMax, List<(string Op, double[] Args)> Path);

    static readonly (double Upem, double Ascent, double Descent, Dictionary<char, Glyph> Map) Font = Load();

    static (double, double, double, Dictionary<char, Glyph>) Load()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("dejavu-sans-bold.glyphs.json")
                      ?? throw new InvalidOperationException("the glyphs are not embedded");
        var d = JsonFile.Parse(new StreamReader(s).ReadToEnd())!;
        var map = new Dictionary<char, Glyph>();
        foreach (var (k, g) in d["glyphs"]!.AsObject())
        {
            var path = g!["path"]!.AsArray().Select(op => op!.AsArray())
                .Select(op => ((string)op[0]!, op.Skip(1).Select(v => (double)v!).ToArray())).ToList();
            map[k[0]] = new Glyph((double)g["adv"]!, (double)g["xmin"]!, (double)g["xmax"]!, path);
        }
        return ((double)d["units_per_em"]!, (double)d["ascent"]!, (double)d["descent"]!, map);
    }

    /// <summary>The text's outlines, as closed contours through apply (user space to pixels); pxPerUnit sets the
    /// flattening.</summary>
    public static List<List<Vector2>> Outline(TextNode t, Func<double, double, Vector2> apply, double pxPerUnit)
    {
        double k = t.Size / Font.Upem;
        var glyphs = t.Text.Select(c => Font.Map.TryGetValue(c, out var g) ? g : Font.Map['?']).ToList();
        // the ink's extent along the line, from the pen start
        double pen = 0, ink0 = double.MaxValue, ink1 = double.MinValue;
        foreach (var g in glyphs)
        {
            if (g.Path.Count > 0)
            {
                ink0 = Math.Min(ink0, pen + g.XMin * k);
                ink1 = Math.Max(ink1, pen + g.XMax * k);
            }
            pen += g.Adv * k;
        }
        if (ink0 > ink1)
            return [];
        double x0 = t.X - ((ink1 - ink0) / 2 + ink0);          // text-anchor middle: x_align = -(width / 2 + x_bearing)
        double y0 = t.Y + (Font.Ascent - Font.Descent) / 2 * k;   // dominant-baseline central
        var out_ = new List<List<Vector2>>();
        double tolUnits = Lower.TolPx / Math.Max(1e-9, pxPerUnit * k);
        pen = x0;
        foreach (var g in glyphs)
        {
            List<Vector2>? cur = null;
            double cx = 0, cy = 0;
            Vector2 P(double fx, double fy) => apply(pen + fx * k, y0 - fy * k);
            foreach (var (op, a) in g.Path)
            {
                switch (op)
                {
                    case "M":
                        if (cur is { Count: >= 3 })
                            out_.Add(cur);
                        (cx, cy) = (a[0], a[1]);
                        cur = [P(cx, cy)];
                        break;
                    case "L":
                        (cx, cy) = (a[0], a[1]);
                        cur!.Add(P(cx, cy));
                        break;
                    case "Q":
                        {
                            double qx = a[0], qy = a[1], ex = a[2], ey = a[3];
                            double dev = Math.Sqrt(Math.Pow((cx + ex) / 2 - qx, 2) + Math.Pow((cy + ey) / 2 - qy, 2)) / 2;
                            int n = Math.Clamp((int)Math.Ceiling(Math.Sqrt(dev / tolUnits)), 1, 64);
                            for (int i = 1; i <= n; i++)
                            {
                                double s = (double)i / n, u = 1 - s;
                                cur!.Add(P(u * u * cx + 2 * u * s * qx + s * s * ex, u * u * cy + 2 * u * s * qy + s * s * ey));
                            }
                            (cx, cy) = (ex, ey);
                            break;
                        }
                    case "Z":
                        if (cur is { Count: >= 3 })
                            out_.Add(cur);
                        cur = null;
                        break;
                }
            }
            if (cur is { Count: >= 3 })
                out_.Add(cur);
            pen += g.Adv * k;
        }
        return out_;
    }
}
