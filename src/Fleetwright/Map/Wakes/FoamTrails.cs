using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Wakes;

/// <summary>A corner of the foam lane's ribbon (wakeribbon.vert): metres from the eye; the lane's foam there (before
/// the Gaussian across it); the distance along the trail and the offset across it, m; the lane's width, m.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct FoamVertex
{
    public Vector3 Rel;
    public float Intensity;
    public Vector2 Lane;
    public float Width;
    float pad;
    public const uint SizeInBytes = 32;
}

/// <summary>The trails as foam lanes in the world view (docs/visuals/wakes.md K4; research §7.2's lane on the path):
/// a ribbon along each <see cref="WakeTrail"/> from the stern back, its width w = √(w0² + 2k²·B·distance back),
/// w0 = 0.3·B·√wash, its foam 1.6·wash·min(U/10, 1)·e^(−age/τ)·(w0/w)^¼ (U the speed when the point was laid), a
/// faint slick after it to the trail's end, ramped in over the first beam behind the transom (the dead-water hollow).
/// The shader takes it across the lane as a Gaussian and breaks it up. Built on the CPU into a grown array, one draw for
/// them all; nothing allocates once it has grown.</summary>
public sealed class FoamTrails
{
    /// <summary>The white water's life, s, and the slick's share and how long the trail keeps it (to its end).</summary>
    public const double WhiteS = 180, SlickShare = 0.12, SlickS = WakeTrail.KeepMs / 1000.0;
    const double K = 0.10, W0Share = 0.3, Peak = 1.6;

    /// <summary>The lane's half-width drawn, in lane widths (the Gaussian is nothing past it).</summary>
    const double Reach = 2;

    /// <summary>Points closer than this many pixels to the last one kept are skipped.</summary>
    const double MinPx = 3;

    FoamVertex[] verts = new FoamVertex[4096];
    int count;

    public ReadOnlySpan<FoamVertex> Vertices => verts.AsSpan(0, count);

    /// <summary>The wash of a ship's propulsors by its shafts (research §2.5: waterjets 2× twin screws): a look, not a
    /// sim fact.</summary>
    public static double WashOf(int shafts) => shafts <= 1 ? 0.8 : shafts == 2 ? 1 : 1.2;

    public void Begin() => count = 0;

    // the kept points this trail, oldest first, and their lateral unit vectors
    TrailPoint[] kept = new TrailPoint[64];
    Vec3d[] side = new Vec3d[64];

    /// <summary>One trail's ribbon, <paramref name="head"/> its stern now; <paramref name="gain"/> scales its foam.</summary>
    public void Add(in MapView view, WakeTrail trail, in TrailPoint head, long nowMs, double gain)
    {
        if (trail.Count == 0)
            return;
        double mpp = view.MetresPerPixel, B = trail.Beam, wash = trail.Wash;
        double w0 = W0Share * B * Math.Sqrt(wash);

        // the points, thinned to a few pixels apart, the head last; a point a beam behind the head for the ramp
        int n = 0;
        Keep(ref n, trail[0]);
        for (int i = 1; i < trail.Count; i++)
            if (Earth.Distance(trail[i].P, kept[n - 1].P) >= MinPx * mpp)
                Keep(ref n, trail[i]);
        double toHead = head.S - kept[n - 1].S;
        if (toHead > B)
        {
            double t = (toHead - B) / toHead;
            ref readonly var last = ref kept[n - 1];
            Keep(ref n, new TrailPoint
            {
                P = last.P + (head.P - last.P) * t, Ms = last.Ms + (long)((head.Ms - last.Ms) * t),
                Speed = (float)(last.Speed + (head.Speed - last.Speed) * t), S = head.S - B,
            });
        }
        else if (n > 1 && toHead < MinPx * mpp)
            n--;   // the newest too near the head: the head takes its place
        Keep(ref n, head);
        if (n < 2)
            return;

        // across: the tangent plane's normal to the trail at each point
        for (int j = 0; j < n; j++)
        {
            var a = kept[Math.Max(j - 1, 0)].P;
            var b = kept[Math.Min(j + 1, n - 1)].P;
            var up = kept[j].P.Normalized();
            var t = b - a;
            t -= up * Vec3d.Dot(t, up);
            double tl = t.Length;
            side[j] = tl > 1e-9 ? Vec3d.Cross(up, t * (1 / tl)) : Vec3d.Zero;
        }

        for (int j = 0; j + 1 < n; j++)
        {
            double i0 = Foam(kept[j], head, nowMs, B, wash, w0, out double wa) * gain;
            double i1 = Foam(kept[j + 1], head, nowMs, B, wash, w0, out double wb) * gain;
            if (i0 < 1e-3 && i1 < 1e-3)
                continue;
            if (count + 6 > verts.Length)
                Array.Resize(ref verts, verts.Length * 2);
            var a0 = Corner(view, kept[j], side[j], -Reach * wa, i0, wa);
            var a1 = Corner(view, kept[j], side[j], Reach * wa, i0, wa);
            var b0 = Corner(view, kept[j + 1], side[j + 1], -Reach * wb, i1, wb);
            var b1 = Corner(view, kept[j + 1], side[j + 1], Reach * wb, i1, wb);
            verts[count++] = a0;
            verts[count++] = a1;
            verts[count++] = b1;
            verts[count++] = a0;
            verts[count++] = b1;
            verts[count++] = b0;
        }
    }

    void Keep(ref int n, in TrailPoint p)
    {
        if (n == kept.Length)
        {
            Array.Resize(ref kept, kept.Length * 2);
            Array.Resize(ref side, side.Length * 2);
        }
        kept[n++] = p;
    }

    /// <summary>The lane's foam at a point (before the Gaussian across it), and its width there.</summary>
    static double Foam(in TrailPoint p, in TrailPoint head, long nowMs, double beam, double wash, double w0, out double width)
    {
        double back = Math.Max(head.S - p.S, 0), age = Math.Max(nowMs - p.Ms, 0) / 1000.0;
        width = Math.Sqrt(w0 * w0 + 2 * K * K * beam * back);
        double under = Math.Min(Math.Abs(p.Speed) / 10, 1) * wash;
        double white = Peak * Math.Exp(-age / WhiteS) * Math.Pow(w0 / width, 0.25);
        double slick = SlickShare * Math.Max(1 - age / SlickS, 0);
        double ramp = Math.Clamp(back / beam, 0, 1);
        return under * (white + slick) * ramp * ramp * (3 - 2 * ramp);
    }

    static FoamVertex Corner(in MapView view, in TrailPoint p, Vec3d side, double across, double intensity, double width)
    {
        var r = p.P + side * across - view.Eye;
        return new FoamVertex
        {
            Rel = new Vector3((float)r.X, (float)r.Y, (float)r.Z), Intensity = (float)intensity,
            Lane = new Vector2((float)p.S, (float)across), Width = (float)width,
        };
    }
}
