using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The land as the map draws it at its finest level, rebuilt on the CPU in double whenever the view changes:
/// each ring near the view is brought inside a cap around it (<see cref="CapRetraction"/>), projected, and handed on as
/// a fan of triangles for the fill (filled by winding count on the GPU, so no triangulation) and as the coast's lines
/// (<see cref="MapLines"/>). The cap encloses what's on screen: the screen's corners pushed 5% outward close in, the
/// horizon when the limb shows. Walks along its edge are fill only, never coast. The coarser levels are baked once
/// instead (<see cref="LandMesh"/>): 1:10M close in would need tiles of ~2 km everywhere, and its views are small, so
/// this stays cheap.
/// <para>The level of detail follows the zoom, by the data alone: the coarsest level whose median coast segment is at
/// most <see cref="MaxSegmentPx"/> on screen (with Natural Earth: 1:110M above ~6,300 m/px, 1:50M above ~760 m/px,
/// else 1:10M), so the coast stays smooth before a level gives way, whatever the window's size. Points closer than
/// <see cref="MinStepPx"/> to the last one kept are dropped (a coast line is 1.2 px wide): a level just past its
/// switch has segments of about 2 px, and the work then follows the coast's length on screen, not the data's
/// density. Allocates nothing once its buffers have grown to the views it's shown.</para></summary>
public sealed class LandView(Land coarse, Land medium, Land fine)
{
    /// <summary>The longest a level's median coast segment may look before the next finer level takes over, pixels.</summary>
    public const double MaxSegmentPx = 10;

    /// <summary>Projected points nearer than this to the last one kept are dropped, pixels.</summary>
    public const double MinStepPx = 1.5;

    public static readonly Vector4 CoastColor = MapPalette.Coast with { W = 0.85f };
    public const float CoastWidthPx = 1.2f;

    readonly CapRetraction cap = new();
    Vector2[] fill = new Vector2[1 << 16];
    Vector2[] px = new Vector2[4096];
    int fillCount;

    public Land Coarse => coarse;
    public Land Medium => medium;
    public Land Fine => fine;

    /// <summary>The fill's triangles in NDC, wound either way: the GPU counts windings.</summary>
    public ReadOnlySpan<Vector2> Fill => fill.AsSpan(0, fillCount);

    /// <summary>The land at the view's level of detail: the coarsest whose median segment is at most
    /// <see cref="MaxSegmentPx"/> on screen.</summary>
    public Land LevelFor(in MapView v) => LevelFor(v.MetresPerPixel);

    public Land LevelFor(double metresPerPixel) =>
        coarse.MedianSegmentMetres <= MaxSegmentPx * metresPerPixel ? coarse
        : medium.MedianSegmentMetres <= MaxSegmentPx * metresPerPixel ? medium
        : fine;

    /// <summary>Rebuilds the fill and adds the coast to <paramref name="lines"/>.</summary>
    public void Build(in MapView v, MapLines lines) => Build(v, lines, LevelFor(v));

    /// <summary>Rebuilds at a given level of detail.</summary>
    public void Build(in MapView v, MapLines lines, Land land)
    {
        fillCount = 0;
        SetCap(v);
        double step = MinStepPx * v.MetresPerPixel / Earth.Radius, minStepCos = 1 - step * step / 2;
        for (int i = 0; i < land.RingCount; i++)
        {
            var (c, cos) = land.Cap(i);
            if (cap.Clear(c, cos))
                continue;
            int n = cap.Retract(land, i, minStepCos);
            if (n >= 3)
                Emit(v, n, lines);
        }
        lines.End();
    }

    /// <summary>Clears the fill (a view drawn from the baked levels has none of its own).</summary>
    public void Clear() => fillCount = 0;

    /// <summary>The cap for the view: through the farthest of the screen's corners pushed 5% outward, if all are on the
    /// globe, else the horizon (a hair inside it, so every point projects in front).</summary>
    void SetCap(in MapView v)
    {
        const double M = 0.05;
        var centre = -v.Forward;
        double horizon = Earth.Radius / (Earth.Radius + v.Altitude);
        double capCos = horizon + 1e-9 * (1 - horizon);
        if (v.Pick(-M * v.Width, -M * v.Height, out var c0) && v.Pick((1 + M) * v.Width, -M * v.Height, out var c1)
            && v.Pick((1 + M) * v.Width, (1 + M) * v.Height, out var c2) && v.Pick(-M * v.Width, (1 + M) * v.Height, out var c3))
            capCos = Math.Max(capCos, Math.Min(Math.Min(Cos(c0), Cos(c1)), Math.Min(Cos(c2), Cos(c3))));
        cap.Set(centre, capCos);

        double Cos(Vec3d p) => Vec3d.Dot(p.Normalized(), centre);
    }

    /// <summary>Projects the brought-in ring, fans it into the fill and draws its coast (not its walks along the cap's
    /// edge).</summary>
    void Emit(in MapView v, int n, MapLines lines)
    {
        var a = cap.Points;
        Grow(ref px, n);
        int kept = 0;
        for (int k = 0; k < n; k++)
        {
            v.Project(a[k].P * Earth.Radius, out double x, out double y);
            var q = new Vector2((float)x, (float)y);
            // drop a point within MinStepPx of the last kept, unless it starts or ends a walk along the cap's edge
            if (kept > 0 && a[k].Mask == a[kept - 1].Mask && Vector2.DistanceSquared(q, px[kept - 1]) < MinStepPx * MinStepPx
                && (k + 1 == n || a[k + 1].Mask == a[k].Mask))
                continue;
            a[kept] = a[k];
            px[kept++] = q;
        }
        n = kept;
        if (n < 3)
            return;
        int need = fillCount + 3 * (n - 2);
        if (need > fill.Length)
            Array.Resize(ref fill, Math.Max(fill.Length * 2, need));
        var p0 = Ndc(v, px[0]);
        for (int k = 1; k < n - 1; k++)
        {
            fill[fillCount++] = p0;
            fill[fillCount++] = Ndc(v, px[k]);
            fill[fillCount++] = Ndc(v, px[k + 1]);
        }
        // the coast: every edge but those along the cap's edge
        lines.BeginStrip(CoastColor, CoastWidthPx);
        int start = 0;
        for (int k = 0; k < n; k++)
            if ((a[k].Mask & a[(k + 1) % n].Mask) != 0)
            {
                start = (k + 1) % n;   // begin after a walk, so a strip never wraps across one
                break;
            }
        lines.Point(px[start].X, px[start].Y);
        for (int j = 1; j <= n; j++)
        {
            int prev = (start + j - 1) % n, k = (start + j) % n;
            if ((a[prev].Mask & a[k].Mask) != 0)
                lines.End();
            lines.Point(px[k].X, px[k].Y);
        }
        lines.End();
    }

    static Vector2 Ndc(in MapView v, Vector2 p) => new((float)(p.X / v.Width * 2 - 1), (float)(1 - p.Y / v.Height * 2));

    static void Grow<T>(ref T[] arr, int n)
    {
        if (arr.Length < n)
            Array.Resize(ref arr, Math.Max(arr.Length * 2, n));
    }
}
