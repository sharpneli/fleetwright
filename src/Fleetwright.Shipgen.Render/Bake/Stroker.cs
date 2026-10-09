using System.Numerics;

namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>Expands a stroke into triangles: a quad per segment, a join piece at each inner vertex (miter, limit 4,
/// as SVG's default, else bevel; or a round disc), caps (butt, square, round) and dashes. The pieces overlap; every
/// triangle is wound the same way so they are drawn as a union (DrawOp.Union), each pixel once.</summary>
public static class Stroker
{
    const double MiterLimit = 4.0;

    public static void Stroke(List<Vector2> pts0, bool closed, double width, string join, string cap, double[]? dash,
        double dashOffset, double tol, List<Vector2> o)
    {
        var pts = Dedup(pts0, closed);
        if (pts.Count == 0)
            return;
        double hw = width / 2;
        if (dash != null && dash.Length > 0 && dash.Any(d => d > 0) && dash.All(d => d >= 0))
        {
            foreach (var piece in Dashes(pts, closed, dash, dashOffset))
                Run(piece, false, hw, join, cap, tol, o);
            return;
        }
        Run(pts, closed, hw, join, cap, tol, o);
    }

    static List<Vector2> Dedup(List<Vector2> pts, bool closed)
    {
        var result = new List<Vector2>(pts.Count);
        foreach (var p in pts)
            if (result.Count == 0 || Vector2.DistanceSquared(result[^1], p) > 1e-10f)
                result.Add(p);
        if (closed && result.Count > 1 && Vector2.DistanceSquared(result[0], result[^1]) <= 1e-10f)
            result.RemoveAt(result.Count - 1);
        return result;
    }

    /// <summary>The dash pieces of a polyline (closed: running on round the closing segment).</summary>
    static List<List<Vector2>> Dashes(List<Vector2> pts, bool closed, double[] pattern, double offset)
    {
        if (pattern.Length % 2 == 1)
            pattern = [.. pattern, .. pattern];
        double total = pattern.Sum();
        var path = closed ? [.. pts, pts[0]] : pts;
        var result = new List<List<Vector2>>();
        // where in the pattern the path starts
        double ph = ((offset % total) + total) % total;
        int idx = 0;
        while (ph >= pattern[idx])
        {
            ph -= pattern[idx];
            idx = (idx + 1) % pattern.Length;
        }
        double left = pattern[idx] - ph;     // what remains of the current dash or gap
        bool on = idx % 2 == 0;
        List<Vector2>? cur = on ? [path[0]] : null;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector2 a = path[i], b = path[i + 1];
            double segLen = Vector2.Distance(a, b), at = 0;
            while (segLen - at > left)
            {
                at += left;
                var p = Vector2.Lerp(a, b, (float)(at / segLen));
                if (on)
                {
                    cur!.Add(p);
                    result.Add(cur);
                    cur = null;
                }
                else
                    cur = [p];
                on = !on;
                idx = (idx + 1) % pattern.Length;
                left = pattern[idx];
            }
            left -= segLen - at;
            if (on)
                cur!.Add(b);
        }
        if (on && cur != null && cur.Count > 1)
            result.Add(cur);
        return result;
    }

    static void Tri(List<Vector2> o, Vector2 a, Vector2 b, Vector2 c)
    {
        // all counter-clockwise in a y-down frame (positive cross), so the stencil counts never cancel
        float cr = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (cr == 0)
            return;
        o.Add(a);
        if (cr > 0)
        {
            o.Add(b);
            o.Add(c);
        }
        else
        {
            o.Add(c);
            o.Add(b);
        }
    }

    static void Disc(List<Vector2> o, Vector2 c, double r, double tol)
    {
        int n = Math.Max(8, Lower.Segs(r));
        Vector2 prev = c + new Vector2((float)r, 0);
        for (int i = 1; i <= n; i++)
        {
            double t = 2 * Math.PI * i / n;
            var p = c + new Vector2((float)(r * Math.Cos(t)), (float)(r * Math.Sin(t)));
            Tri(o, c, prev, p);
            prev = p;
        }
    }

    static Vector2 Normal(Vector2 a, Vector2 b)
    {
        var d = Vector2.Normalize(b - a);
        return new Vector2(-d.Y, d.X);
    }

    static void Run(List<Vector2> pts, bool closed, double hw, string join, string cap, double tol, List<Vector2> o)
    {
        float h = (float)hw;
        if (pts.Count == 1)
        {
            if (cap == "round")     // a zero-length subpath with round caps is a dot
                Disc(o, pts[0], hw, tol);
            else if (cap == "square")
            {
                var p = pts[0];
                Tri(o, p + new Vector2(-h, -h), p + new Vector2(h, -h), p + new Vector2(h, h));
                Tri(o, p + new Vector2(-h, -h), p + new Vector2(h, h), p + new Vector2(-h, h));
            }
            return;
        }
        int n = pts.Count, segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % n];
            var nrm = Normal(a, b) * h;
            if (!closed && cap == "square")
            {
                var d = Vector2.Normalize(b - a) * h;
                if (i == 0)
                    a -= d;
                if (i == segs - 1)
                    b += d;
            }
            Tri(o, a + nrm, b + nrm, b - nrm);
            Tri(o, a + nrm, b - nrm, a - nrm);
        }
        // joins at the inner vertices (every vertex when closed)
        for (int i = closed ? 0 : 1; i < (closed ? n : n - 1); i++)
        {
            Vector2 p = pts[i], prev = pts[(i - 1 + n) % n], next = pts[(i + 1) % n];
            if (join == "round")
            {
                Disc(o, p, hw, tol);
                continue;
            }
            var n0 = Normal(prev, p);
            var n1 = Normal(p, next);
            float turn = n0.X * n1.Y - n0.Y * n1.X;
            if (MathF.Abs(turn) < 1e-7f && Vector2.Dot(n0, n1) > 0)
                continue;   // straight on
            float s = turn > 0 ? -1 : 1;      // the outer side
            Vector2 e0 = p + n0 * h * s, e1 = p + n1 * h * s;
            Tri(o, p, e0, e1);   // the bevel
            if (join != "bevel")
            {
                var bis = n0 + n1;
                float cosHalf = bis.Length() / 2;     // cos of half the angle between the normals
                if (cosHalf > 1e-6f && 1 / cosHalf <= MiterLimit)
                {
                    var tip = p + Vector2.Normalize(bis) * (h / cosHalf) * s;
                    Tri(o, e0, tip, e1);
                }
            }
        }
        if (!closed && cap == "round")
        {
            Disc(o, pts[0], hw, tol);
            Disc(o, pts[^1], hw, tol);
        }
    }
}
