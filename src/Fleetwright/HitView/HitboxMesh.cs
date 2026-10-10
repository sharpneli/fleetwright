using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Shipgen;

namespace Fleetwright.HitView;

/// <summary>A mesh vertex: ship-local metres (x toward the bow, y to starboard, z above the main deck), the face's
/// normal, the kind's index in <see cref="HitKinds.All"/> and the prism's index (<see cref="HitboxMesh.NoPrism"/> for
/// guide lines).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct HitVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public uint Kind;
    public uint Prism;

    public const int SizeInBytes = 32;
}

/// <summary>One hitbox shape as drawn: a footprint extruded from Base to Top, or (Lofted) a solid whose outline changes
/// with height to follow the hull's side, its Footprint then the outline at mid-height. Its faces are
/// Indices[FirstIndex..+IndexCount] (triangles) and its outline EdgeIndices[FirstEdge..+EdgeCount] (lines).</summary>
public sealed class HitPrism
{
    public required string Id { get; init; }
    public required int Kind { get; init; }

    /// <summary>What it was made from: a <see cref="Component"/>, <see cref="Cell"/>, armour report or null (hull).</summary>
    public object? Source { get; init; }

    public required Vector2[] Footprint { get; init; }

    /// <summary>Not an extruded footprint: picked by its triangles.</summary>
    public bool Lofted { get; init; }

    public float Base { get; init; }
    public float Top { get; init; }
    public int FirstIndex, IndexCount, FirstEdge, EdgeCount;
    public Vector3 Min, Max;
}

/// <summary>
/// The hitbox model as triangles and lines, for any view that draws it: hitview.py's prisms() and faces() without the
/// rasteriser. Every hitbox is a footprint with a base and a top, so it extrudes to a prism; the hull is cut into
/// slices that narrow to the keel, cells are clipped to the hull form, the belt and strakes are thin slabs following
/// the flare. Turrets are at their rest angle. Reads nothing but <see cref="Hitboxes"/>, and needs no GPU.
/// </summary>
public sealed class HitboxMesh
{
    public const uint NoPrism = uint.MaxValue;
    const int CircleN = 16;
    const float SharpCos = 0.94f;   // a vertical edge is drawn where the outline turns by more than ~20 degrees

    public HitVertex[] Vertices { get; private init; } = [];
    public uint[] Indices { get; private init; } = [];
    public uint[] EdgeIndices { get; private init; } = [];
    public HitPrism[] Prisms { get; private init; } = [];
    public Vector3 Min { get; private init; }
    public Vector3 Max { get; private init; }

    /// <summary>The kinds present, by index (the legend shows these).</summary>
    public bool[] KindsPresent { get; private init; } = [];

    public static HitboxMesh Build(Hitboxes hb) => new Builder(hb).Run();

    /// <summary>The prism a ray meets first among those the state shows (its kind drawn, the hit inside the clip box),
    /// or <see cref="NoPrism"/>. The hull's slices are passed through, so pointing at the hull finds what is inside.
    /// Exact against the prisms' faces; no allocation.</summary>
    public uint Pick(Vector3 o, Vector3 d, in HitboxViewState state)
    {
        int hull = HitKinds.IndexOf("hull");
        float best = float.MaxValue;
        uint hit = NoPrism;
        for (int i = 0; i < Prisms.Length; i++)
        {
            var p = Prisms[i];
            if (p.Kind == hull || !state.Shows(p.Kind) || !state.Keeps(p.Min, p.Max) || !SlabHit(o, d, p.Min, p.Max, best))
                continue;
            float t = p.Lofted ? FirstTriangle(p, o, d, state, best) : FirstHit(p, o, d, state, best);
            if (t < best)
                (best, hit) = (t, (uint)i);
        }
        return hit;
    }

    /// <summary>The nearest t below <paramref name="tMax"/> where the ray meets one of the shape's triangles (inside the
    /// clip box when cutting).</summary>
    float FirstTriangle(HitPrism p, Vector3 o, Vector3 d, in HitboxViewState state, float tMax)
    {
        float best = tMax;
        var idx = Indices.AsSpan(p.FirstIndex, p.IndexCount);
        for (int i = 0; i + 2 < idx.Length; i += 3)
        {
            Vector3 a = Vertices[idx[i]].Position, e1 = Vertices[idx[i + 1]].Position - a, e2 = Vertices[idx[i + 2]].Position - a;
            var pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (MathF.Abs(det) < 1e-12f)
                continue;
            float inv = 1 / det;
            var tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv;
            if (u < 0 || u > 1)
                continue;
            var qv = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, qv) * inv;
            if (v < 0 || u + v > 1)
                continue;
            float t = Vector3.Dot(e2, qv) * inv;
            if (t <= 0 || t >= best)
                continue;
            var q = o + d * t;
            if (!state.Clip || !state.ClipCut || (q.X >= state.ClipMin.X && q.Y >= state.ClipMin.Y && q.Z >= state.ClipMin.Z
                                                  && q.X <= state.ClipMax.X && q.Y <= state.ClipMax.Y && q.Z <= state.ClipMax.Z))
                best = t;
        }
        return best;
    }

    static bool SlabHit(Vector3 o, Vector3 d, Vector3 min, Vector3 max, float tMax)
    {
        float t0 = 0, t1 = tMax;
        for (int a = 0; a < 3; a++)
        {
            float oa = a == 0 ? o.X : a == 1 ? o.Y : o.Z, da = a == 0 ? d.X : a == 1 ? d.Y : d.Z;
            float lo = a == 0 ? min.X : a == 1 ? min.Y : min.Z, hi = a == 0 ? max.X : a == 1 ? max.Y : max.Z;
            if (Math.Abs(da) < 1e-9f)
            {
                if (oa < lo || oa > hi)
                    return false;
                continue;
            }
            float ta = (lo - oa) / da, tb = (hi - oa) / da;
            if (ta > tb)
                (ta, tb) = (tb, ta);
            (t0, t1) = (Math.Max(t0, ta), Math.Min(t1, tb));
            if (t0 > t1)
                return false;
        }
        return true;
    }

    /// <summary>The nearest t below <paramref name="tMax"/> where the ray meets the prism's surface (inside the clip
    /// box when cutting: a cut prism is open, so its far side can be the first seen).</summary>
    static float FirstHit(HitPrism p, Vector3 o, Vector3 d, in HitboxViewState state, float tMax)
    {
        float best = tMax;
        bool Inside(Vector3 q, in HitboxViewState s) => !s.Clip || !s.ClipCut || (q.X >= s.ClipMin.X && q.Y >= s.ClipMin.Y && q.Z >= s.ClipMin.Z
                                                                    && q.X <= s.ClipMax.X && q.Y <= s.ClipMax.Y && q.Z <= s.ClipMax.Z);
        if (Math.Abs(d.Z) > 1e-9f)   // the caps
            for (int k = 0; k < 2; k++)
            {
                float t = ((k == 0 ? p.Top : p.Base) - o.Z) / d.Z;
                if (t <= 0 || t >= best)
                    continue;
                var q = o + d * t;
                if (Contains(p.Footprint, new Vector2(q.X, q.Y)) && Inside(q, state))
                    best = t;
            }
        var fp = p.Footprint;
        var o2 = new Vector2(o.X, o.Y);
        var d2 = new Vector2(d.X, d.Y);
        for (int i = 0; i < fp.Length; i++)   // the sides
        {
            Vector2 a = fp[i], e = fp[(i + 1) % fp.Length] - a;
            float den = d2.X * e.Y - d2.Y * e.X;
            if (Math.Abs(den) < 1e-12f)
                continue;
            var w = a - o2;
            float t = (w.X * e.Y - w.Y * e.X) / den, s = (w.X * d2.Y - w.Y * d2.X) / den;
            if (t <= 0 || t >= best || s < 0 || s > 1)
                continue;
            var q = o + d * t;
            if (q.Z >= p.Base && q.Z <= p.Top && Inside(q, state))
                best = t;
        }
        return best;
    }

    static bool Contains(Vector2[] poly, Vector2 q)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y)
                && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    sealed class Builder(Hitboxes hb)
    {
        readonly List<HitVertex> verts = [];
        readonly List<uint> tris = [];
        readonly List<uint> lines = [];
        readonly List<HitPrism> prisms = [];

        readonly HullField? field = hb.HullForm.Stations.Count >= 2 ? new HullField(hb.HullForm, hb.Vertical) : null;

        /// <summary>The hull's half-breadth; on an end station the end's own (a transom has width there, where
        /// HullField.HalfWidth, off the hull, has none).</summary>
        double HalfWidth(double x, double z) =>
            field is { } f ? f.HalfWidth(Math.Abs(x - f.X0) < 1e-6 ? f.X0 + 1e-6 : Math.Abs(x - f.X1) < 1e-6 ? f.X1 - 1e-6 : x, z) : DeckHalfWidth(x);

        /// <summary>The hull's skin, one prism per strip between stations: both sides through every row of the form
        /// (smooth normals across cells), the deck tops, the bottom where a strip starts with width, the flat ends
        /// and the raised stretches' end walls. Pushed out by a hair against the cells that lie on it.</summary>
        void AddHullSkin(HullField f)
        {
            const double Out = 0.01;
            int kind = HitKinds.IndexOf("hull");
            var breaks = f.DeckBreaks.ToArray();

            Vector3 SideNormal(double x, double z, int s)
            {
                const double Hx = 0.3, Hz = 0.05;
                double xa = Math.Max(f.X0 + 1e-6, x - Hx), xb = Math.Min(f.X1 - 1e-6, x + Hx), xm = Math.Clamp(x, xa, xb);
                double gx = xb > xa ? (f.HalfWidth(xb, z) - f.HalfWidth(xa, z)) / (xb - xa) : 0.0;
                double gz = (f.HalfWidth(xm, z + Hz) - f.HalfWidth(xm, z - Hz)) / (2 * Hz);
                return Vector3.Normalize(new Vector3((float)-gx, s, (float)-gz));
            }

            for (int i = 0; i < f.StripCount; i++)
            {
                double x0 = f.StationX(i), x1 = f.StationX(i + 1), lx = x1 - x0;
                var rows = f.RowHeights(i).ToArray();
                uint pi = (uint)prisms.Count;
                int firstIndex = tris.Count, firstEdge = lines.Count;
                HitVertex V(double x, double y, double z, Vector3 n) =>
                    new() { Position = new((float)x, (float)y, (float)z), Normal = n, Kind = (uint)kind, Prism = pi };
                // a side quad is twisted (the form is bilinear between rows and stations): split it along the diagonal
                // that bulges out, so the skin lies on or outside the form and the cells' walls (split to bend in, see
                // AddLoft) stay under it, however much the side twists (a tumblehome's does a lot)
                void Quad(HitVertex a, HitVertex b, HitVertex c, HitVertex d)
                {
                    uint v0 = (uint)verts.Count;
                    verts.AddRange([a, b, c, d]);
                    var n = a.Normal + b.Normal + c.Normal + d.Normal;
                    if (Vector3.Dot(a.Position + c.Position - b.Position - d.Position, n) >= 0)
                        tris.AddRange([v0, v0 + 1, v0 + 2, v0, v0 + 2, v0 + 3]);
                    else
                        tris.AddRange([v0, v0 + 1, v0 + 3, v0 + 1, v0 + 2, v0 + 3]);
                }
                void Wall(double x, Func<double, double> w, IReadOnlyList<double> hs, float nx)
                {
                    var n = new Vector3(nx, 0, 0);
                    double xo = x + nx * Out;
                    for (int k = 0; k + 1 < hs.Count; k++)
                    {
                        double za = hs[k], zb = hs[k + 1];
                        if (w(za) + w(zb) > 0)
                            Quad(V(xo, -w(za), za, n), V(xo, w(za), za, n), V(xo, w(zb), zb, n), V(xo, -w(zb), zb, n));
                    }
                }
                double wMax = 0, zLo = double.MaxValue, zHi = double.MinValue;

                // the strip cut where the deck's height changes
                var cuts = new List<double> { x0 };
                foreach (var bx in breaks)
                    if (bx > x0 + 1e-6 && bx < x1 - 1e-6)
                        cuts.Add(bx);
                cuts.Add(x1);
                for (int c = 0; c + 1 < cuts.Count; c++)
                {
                    double xa = cuts[c], xb = cuts[c + 1], top = f.DeckTop((xa + xb) / 2);
                    double fa = (xa - x0) / lx, fb = (xb - x0) / lx;
                    var zl = rows.Where(z => z < top - 1e-6).Append(top).ToList();
                    double Wa(double z) => f.InStrip(i, fa, z);
                    double Wb(double z) => f.InStrip(i, fb, z);
                    foreach (int s in new[] { 1, -1 })
                        for (int k = 0; k + 1 < zl.Count; k++)
                        {
                            double za = zl[k], zb = zl[k + 1];
                            if (Wa(za) + Wb(za) + Wa(zb) + Wb(zb) <= 0)
                                continue;
                            Quad(V(xa, s * (Wa(za) + Out), za, SideNormal(xa, za, s)), V(xb, s * (Wb(za) + Out), za, SideNormal(xb, za, s)),
                                V(xb, s * (Wb(zb) + Out), zb, SideNormal(xb, zb, s)), V(xa, s * (Wa(zb) + Out), zb, SideNormal(xa, zb, s)));
                        }
                    // the deck top, and the bottom where the strip starts with width (a stern cut up out of the water)
                    double tz = top + Out, bz = zl[0] - Out, z0 = zl[0];
                    Quad(V(xa, -Wa(top) - Out, tz, Vector3.UnitZ), V(xb, -Wb(top) - Out, tz, Vector3.UnitZ),
                        V(xb, Wb(top) + Out, tz, Vector3.UnitZ), V(xa, Wa(top) + Out, tz, Vector3.UnitZ));
                    if (Wa(z0) + Wb(z0) > 0)
                        Quad(V(xa, -Wa(z0), bz, -Vector3.UnitZ), V(xb, -Wb(z0), bz, -Vector3.UnitZ),
                            V(xb, Wb(z0), bz, -Vector3.UnitZ), V(xa, Wa(z0), bz, -Vector3.UnitZ));
                    // the flat ends (a transom), and a raised stretch's end wall where this piece starts
                    if (i == 0 && c == 0)
                        Wall(xa, Wa, zl, -1);
                    if (i == f.StripCount - 1 && c == cuts.Count - 2)
                        Wall(xb, Wb, zl, 1);
                    int bi = Array.IndexOf(breaks, xa);
                    if (bi > 0 && bi < breaks.Length - 1)
                    {
                        double before = f.DeckTops[bi - 1];
                        if (Math.Abs(before - top) > 1e-6)
                        {
                            double lo = Math.Min(before, top), hi = Math.Max(before, top);
                            Wall(xa, Wa, [.. Enumerable.Range(0, 5).Select(k => lo + (hi - lo) * k / 4)], top > before ? -1 : 1);
                        }
                    }
                    foreach (var z in zl)
                        wMax = Math.Max(wMax, Math.Max(Wa(z), Wb(z)));
                    zLo = Math.Min(zLo, z0);
                    zHi = Math.Max(zHi, top);
                }
                if (tris.Count == firstIndex)
                    continue;
                var fp = new[] { new Vector2((float)x0, (float)-wMax), new Vector2((float)x1, (float)-wMax), new Vector2((float)x1, (float)wMax),
                    new Vector2((float)x0, (float)wMax) };
                prisms.Add(new HitPrism
                {
                    Id = $"hull {x0:F1} to {x1:F1} m", Kind = kind, Footprint = fp, Base = (float)zLo, Top = (float)zHi, FirstIndex = firstIndex,
                    IndexCount = tris.Count - firstIndex, FirstEdge = firstEdge, EdgeCount = 0,
                    Min = new((float)x0, (float)-wMax, (float)zLo), Max = new((float)x1, (float)wMax, (float)zHi),
                });
            }
        }

        /// <summary>The deck outline's half-width at x (its widest crossing), where there is no exported form.</summary>
        double DeckHalfWidth(double x)
        {
            double best = 0;
            var h = hb.Hull;
            for (int i = 0; i < h.Count; i++)
            {
                var (a, b) = (h[i], h[(i + 1) % h.Count]);
                if ((a.X - x) * (b.X - x) <= 0 && a.X != b.X)
                    best = Math.Max(best, Math.Abs(a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X)));
            }
            return best;
        }

        /// <summary>The hull's outline at height z as a polygon (stations closer at the ends).</summary>
        List<Pt> FormOutline(double z, int n = 120)
        {
            double L = hb.Length;
            var pts = new List<(double X, double W)>();
            for (int k = 0; k <= n; k++)
            {
                double x = -L / 2 + L * (1 - Math.Cos(Math.PI * k / n)) / 2, w = HalfWidth(x, z);
                if (w > 0.01)
                    pts.Add((x, w));
            }
            return [.. pts.Select(p => new Pt(p.X, -p.W)), .. Enumerable.Reverse(pts).Select(p => new Pt(p.X, p.W))];
        }

        /// <summary>A cell's footprint: its box clipped to the hull's outline at the cell's middle height.</summary>
        List<Pt> CellOutline(Cell c, int n = 8)
        {
            var lo = new List<Pt>();
            var hi = new List<Pt>();
            double zm = (c.Base + c.Top) / 2;
            for (int k = 0; k <= n; k++)
            {
                double x = c.X0 + (c.X1 - c.X0) * k / n, w = HalfWidth(x, zm);
                double y0 = Math.Max(c.Y0, -w), y1 = Math.Min(c.Y1, w);
                if (y0 < y1)
                {
                    lo.Add(new Pt(x, y0));
                    hi.Add(new Pt(x, y1));
                }
            }
            hi.Reverse();
            return [.. lo, .. hi];
        }

        /// <summary>A cell: its box clipped to the hull. Where the side cuts it, a loft that follows the side up the
        /// cell's height (the hull narrows to the keel, and leans in above the waterline with tumblehome); else a prism.
        /// </summary>
        void AddCell(string kind, Cell c)
        {
            var xs = XsOver(c.X0, c.X1);
            var zs = ZsOver(c.X0, c.X1, c.Base, c.Top);
            bool cut = field != null && xs.Any(x => zs.Any(z => HalfWidth(x, z) < Math.Max(-c.Y0, c.Y1)));
            if (!cut)
            {
                Add(kind, c.Id, c, CellOutline(c), c.Base, c.Top);
                return;
            }
            // a level where the side crosses the cell's walls at each station, so the loft opens where the cell does
            var levels = new SortedSet<double>(zs);
            foreach (var x in xs)
                foreach (var e in new[] { Math.Abs(c.Y0), Math.Abs(c.Y1) })
                    for (int k = 0; k + 1 < zs.Count; k++)
                    {
                        double lo = zs[k], hi = zs[k + 1];
                        if ((HalfWidth(x, lo) - e) * (HalfWidth(x, hi) - e) >= 0)
                            continue;
                        bool rising = HalfWidth(x, hi) > e;
                        for (int it = 0; it < 30; it++)
                        {
                            double m = (lo + hi) / 2;
                            if ((HalfWidth(x, m) > e) == rising)
                                hi = m;
                            else
                                lo = m;
                        }
                        levels.Add(rising ? hi : lo);
                    }
            zs = Merge(levels, c.Base, c.Top);
            // at each station, the heights where the cell has width (the hull reaches its near wall): a ring point at a
            // height where it has none rides to the nearest such height, onto the hull's side, so nothing hangs outside
            double near = c.Y0 > 0 ? c.Y0 : c.Y1 < 0 ? -c.Y1 : 0.0;
            var span = xs.Select(x =>
            {
                var ok = zs.Where(z => HalfWidth(x, z) >= near - 1e-9 && HalfWidth(x, z) > 0).ToList();
                return ok.Count > 0 ? (Lo: ok.Min(), Hi: ok.Max()) : (Lo: double.NaN, Hi: double.NaN);
            }).ToList();
            // a station where the cell has no width at any height (it ends short of its box): its points stand at the
            // nearest station where it has some, so the loft closes there
            var has = Enumerable.Range(0, xs.Count).Where(k => !double.IsNaN(span[k].Lo)).ToList();
            if (has.Count == 0)
                return;
            xs = xs.Select((x, k) => double.IsNaN(span[k].Lo) ? xs[has.MinBy(j => Math.Abs(j - k))] : x).ToList();
            span = span.Select((sp, k) => double.IsNaN(sp.Lo) ? span[has.MinBy(j => Math.Abs(j - k))] : sp).ToList();
            List<P3> Ring(double z)
            {
                var lo = new List<P3>();
                var hi = new List<P3>();
                for (int k = 0; k < xs.Count; k++)
                {
                    double x = xs[k], zc = double.IsNaN(span[k].Lo) ? z : Math.Clamp(z, span[k].Lo, span[k].Hi), w = HalfWidth(x, zc);
                    lo.Add(new P3(x, Math.Clamp(-w, c.Y0, c.Y1), zc));
                    hi.Add(new P3(x, Math.Clamp(w, c.Y0, c.Y1), zc));
                }
                hi.Reverse();
                return [.. lo, .. hi];
            }
            AddLoft(kind, c.Id, c, zs, Ring);
        }

        /// <summary>The stations between x0 and x1, and both ends: where the hull's side bends along the length.</summary>
        List<double> XsOver(double x0, double x1)
        {
            var xs = new List<double> { x0 };
            if (field != null)
                for (int i = 0; i <= field.StripCount; i++)
                {
                    double x = field.StationX(i);
                    if (x > x0 + 0.05 && x < x1 - 0.05)
                        xs.Add(x);
                }
            else
                xs.AddRange(Enumerable.Range(1, 7).Select(k => x0 + (x1 - x0) * k / 8));
            xs.Add(x1);
            return xs;
        }

        /// <summary>The form's rows between bottom and top over x0 .. x1, and both ends: where the side bends up its height
        /// (rows closer than 2 cm merged).</summary>
        List<double> ZsOver(double x0, double x1, double bottom, double top)
        {
            var zs = new SortedSet<double> { bottom, top };
            if (field != null)
                for (int i = 0; i < field.StripCount; i++)
                    if (field.StationX(i + 1) > x0 && field.StationX(i) < x1)
                        foreach (var z in field.RowHeights(i))
                            if (z > bottom + 0.02 && z < top - 0.02)
                                zs.Add(z);
            return Merge(zs, bottom, top);
        }

        /// <summary>Heights from bottom to top in order, those closer than 2 cm to the one before merged (both ends kept).</summary>
        static List<double> Merge(IEnumerable<double> zs, double bottom, double top)
        {
            var o = new List<double> { bottom };
            foreach (var z in zs.Where(z => z > bottom && z < top))
                if (z - o[^1] >= 0.02)
                    o.Add(z);
            if (o.Count > 1 && top - o[^1] < 0.02)
                o.RemoveAt(o.Count - 1);
            o.Add(top);
            return o;
        }

        /// <summary>A solid stacked from rings, one per level in zs (lowest first), every ring the same number of points in
        /// the same order. A point usually lies at its ring's level, but may ride up or down (onto the hull's side, where
        /// a cell has no width at that level). Walls between neighbouring rings, caps on the lowest and highest, the top
        /// and bottom rings and its sharp corners as edges. Drawn grown by a hair like Add's prisms; picked by its
        /// triangles.</summary>
        void AddLoft(string kindName, string id, object? src, IReadOnlyList<double> zs, Func<double, List<P3>> ringAt)
        {
            if (zs.Count < 2)
                return;
            var rings = zs.Select(ringAt).ToList();
            int n = rings[0].Count;
            if (n < 3 || rings.Any(r => r.Count != n))
                return;
            var largest = rings.MaxBy(r => Math.Abs(SignedArea(Plan(r))))!;
            double area = SignedArea(Plan(largest));
            if (Math.Abs(area) < 1e-6)
                return;
            if (area < 0)
                rings = rings.Select(r => Enumerable.Reverse(r).ToList()).ToList();
            int kind = HitKinds.IndexOf(kindName);
            uint pi = (uint)prisms.Count;
            int firstIndex = tris.Count, firstEdge = lines.Count;
            float dz = 0.002f + 0.03f / MathF.Sqrt(1 + (float)Math.Abs(area));
            var drawn = rings.Select(r => Outset(r.Select(p => new Vector2((float)p.X, (float)p.Y)).ToArray(), dz)).ToList();
            float Zp(int k, int i) => (float)rings[k][i].Z + (k == 0 ? -dz : k == zs.Count - 1 ? dz : 0);
            HitVertex V(Vector2 p, float z, Vector3 nrm) => new() { Position = new(p, z), Normal = nrm, Kind = (uint)kind, Prism = pi };

            // the walls: a quad between each pair of neighbouring rings along each side, lit flat
            for (int k = 0; k + 1 < zs.Count; k++)
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Vector3 a = new(drawn[k][i], Zp(k, i)), b = new(drawn[k][j], Zp(k, j)), c = new(drawn[k + 1][j], Zp(k + 1, j)),
                        d = new(drawn[k + 1][i], Zp(k + 1, i));
                    var nrm = Vector3.Cross(b - a, d - a);
                    if (nrm.LengthSquared() < 1e-14f)
                        nrm = Vector3.Cross(c - b, d - b);
                    if (nrm.LengthSquared() < 1e-14f)
                        continue;   // collapsed: the side has closed this stretch of the ring
                    nrm = Vector3.Normalize(nrm);
                    uint v0 = (uint)verts.Count;
                    verts.AddRange([V(drawn[k][i], a.Z, nrm), V(drawn[k][j], b.Z, nrm), V(drawn[k + 1][j], c.Z, nrm), V(drawn[k + 1][i], d.Z, nrm)]);
                    // split along the diagonal that bends in, so a wall on the hull's side stays under the skin
                    if (Vector3.Dot(a + c - b - d, nrm) <= 0)
                        tris.AddRange([v0, v0 + 1, v0 + 2, v0, v0 + 2, v0 + 3]);
                    else
                        tris.AddRange([v0, v0 + 1, v0 + 3, v0 + 1, v0 + 2, v0 + 3]);
                }
            // the caps, and their rings as edges
            foreach (var (k, up) in new[] { (zs.Count - 1, true), (0, false) })
            {
                // the ring without repeated points (a point keeps its own height: the cap bends where points ride)
                var ring = rings[k];
                var keep = new List<int>();
                for (int i = 0; i < n; i++)
                    if (keep.Count == 0 || Math.Abs(ring[i].X - ring[keep[^1]].X) > 1e-6 || Math.Abs(ring[i].Y - ring[keep[^1]].Y) > 1e-6)
                        keep.Add(i);
                while (keep.Count > 1 && Math.Abs(ring[keep[0]].X - ring[keep[^1]].X) <= 1e-6 && Math.Abs(ring[keep[0]].Y - ring[keep[^1]].Y) <= 1e-6)
                    keep.RemoveAt(keep.Count - 1);
                var cap = keep.Select(i => new Pt(ring[i].X, ring[i].Y)).ToList();
                if (cap.Count >= 3 && Math.Abs(SignedArea(cap)) > 1e-6)
                {
                    var capDrawn = Outset(cap.Select(p => new Vector2((float)p.X, (float)p.Y)).ToArray(), dz);
                    uint c0 = (uint)verts.Count;
                    for (int i = 0; i < capDrawn.Length; i++)
                        verts.Add(V(capDrawn[i], Zp(k, keep[i]), up ? Vector3.UnitZ : -Vector3.UnitZ));
                    foreach (int t in Triangulate(cap))
                        tris.Add(c0 + (uint)t);
                    for (int i = 0; i < capDrawn.Length; i++)
                        lines.AddRange([c0 + (uint)i, c0 + (uint)((i + 1) % capDrawn.Length)]);
                }
            }
            // the sharp corners, up the whole height
            var bottom = drawn[0];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = bottom[(i + n - 1) % n], c = bottom[i], e = bottom[(i + 1) % n];
                if ((c - a).LengthSquared() < 1e-10f || (e - c).LengthSquared() < 1e-10f
                    || Vector2.Dot(Vector2.Normalize(c - a), Vector2.Normalize(e - c)) >= SharpCos)
                    continue;
                uint e0 = (uint)verts.Count;
                for (int k = 0; k < zs.Count; k++)
                    verts.Add(V(drawn[k][i], Zp(k, i), Vector3.UnitZ));
                for (int k = 0; k + 1 < zs.Count; k++)
                    lines.AddRange([e0 + (uint)k, e0 + (uint)k + 1]);
            }
            if (tris.Count == firstIndex)
                return;
            var all = drawn.SelectMany(r => r).ToList();
            var lo = all.Aggregate(new Vector2(float.MaxValue), Vector2.Min);
            var hi = all.Aggregate(new Vector2(float.MinValue), Vector2.Max);
            prisms.Add(new HitPrism
            {
                Id = id, Kind = kind, Source = src, Lofted = true,
                Footprint = rings[rings.Count / 2].Select(p => new Vector2((float)p.X, (float)p.Y)).ToArray(),
                Base = (float)zs[0], Top = (float)zs[^1], FirstIndex = firstIndex, IndexCount = tris.Count - firstIndex,
                FirstEdge = firstEdge, EdgeCount = lines.Count - firstEdge,
                Min = new(lo, (float)zs[0]), Max = new(hi, (float)zs[^1]),
            });
        }

        static List<Pt> Plan(List<P3> ring) => ring.Select(p => new Pt(p.X, p.Y)).ToList();

        static List<Pt> Circle(double x, double y, double r) =>
            Enumerable.Range(0, CircleN).Select(k => new Pt(x + r * Math.Cos(2 * Math.PI * k / CircleN), y + r * Math.Sin(2 * Math.PI * k / CircleN))).ToList();

        static List<Pt> Box(double x0, double x1, double y0, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

        public HitboxMesh Run()
        {
            var vert = hb.Vertical;
            double keel = vert.Keel, wl = vert.Waterline;

            if (field != null)
                AddHullSkin(field);
            else
            {
                // no exported form: the hull in slices of the deck's outline
                var zs = new[] { 0.0, 0.5, 1.0 }.Select(f => keel + (Math.Min(wl, 0.0) - keel) * f).ToList();
                if (wl < 0.0)
                    zs.Add(0.0);
                for (int i = 0; i + 1 < zs.Count; i++)
                    Add("hull", $"hull {zs[i]:F1} to {zs[i + 1]:F1} m", null, FormOutline((zs[i] + zs[i + 1]) / 2), zs[i], zs[i + 1]);
            }

            foreach (var c in hb.Components)
                AddComponent(c);

            var rooms = hb.Rooms.ToDictionary(r => r.Id, StringComparer.Ordinal);
            foreach (var c in hb.Cells)
                AddCell(rooms.TryGetValue(c.Room, out var room) ? room.Kind : "other", c);

            AddArmour(wl);
            AddGuides(keel, wl);

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var p in prisms)
                (min, max) = (Vector3.Min(min, p.Min), Vector3.Max(max, p.Max));
            var present = new bool[HitKinds.All.Length];
            foreach (var v in verts)
                present[v.Kind] = true;
            return new HitboxMesh
            {
                Vertices = [.. verts], Indices = [.. tris], EdgeIndices = [.. lines], Prisms = [.. prisms],
                Min = prisms.Count > 0 ? min : Vector3.Zero, Max = prisms.Count > 0 ? max : Vector3.Zero, KindsPresent = present,
            };
        }

        void AddComponent(Component c)
        {
            string kind = c.Kind;
            if (c.Local is { } local)
            {
                double rest = c.RestDeg ?? 0, x = c.X ?? 0, y = c.Y ?? 0;
                foreach (var p in local.Parts.Prepend(local.Body))
                    Add(kind, c.Id, c, Geometry.RotateTranslate(p, rest, x, y), c.Base, c.Top);
                double mid = c.Base + 0.55 * (c.Top - c.Base);
                foreach (var p in local.Barrels)
                    Add(kind, c.Id, c, Geometry.RotateTranslate(p, rest, x, y), mid - 0.25, mid + 0.25);
            }
            else if (c.Shape == "segment" && c.P0 is { } p0 && c.P1 is { } p1)   // a shaft: short boxes along it
            {
                double r = c.R ?? 0.3, y = p0[1];
                int n = Math.Max(1, (int)(Math.Abs(p1[0] - p0[0]) / 3.0));
                for (int k = 0; k < n; k++)
                {
                    double a = p0[0] + (p1[0] - p0[0]) * k / n, b = p0[0] + (p1[0] - p0[0]) * (k + 1) / n;
                    double zm = p0[2] + (p1[2] - p0[2]) * (k + 0.5) / n;
                    Add(kind, c.Id, c, Box(Math.Min(a, b), Math.Max(a, b), y - r, y + r), zm - r, zm + r);
                }
            }
            else if (c.Shape == "circle" && c.R is { } r)
                Add(kind, c.Id, c, Circle(c.X ?? 0, c.Y ?? 0, r), c.Base, c.Top);
            else if (c.Points is { Count: >= 3 } pts)
                Add(kind, c.Id, c, pts, c.Base, c.Top);
        }

        void AddArmour(double wl)
        {
            var arm = hb.Armour;
            var slabs = new List<(string Kind, string Id, object Src, double X0, double X1, double Bottom, double Top)>();
            if (arm.Belt is { } belt)
                slabs.Add(("belt", "belt", belt, belt.X0, belt.X1, belt.Bottom, belt.Top));
            foreach (var s in arm.Strakes ?? [])
                slabs.Add(("strake", s.Id, s, s.X0, s.X1, s.Bottom, s.Top));
            foreach (var b in slabs)
            {
                // a thin shell just outside the hull's side over its stretch, following the side up its height (in to the
                // keel, out at a flare, in again above a tumblehome's knuckle)
                var xs = XsOver(b.X0, b.X1);
                var zs = ZsOver(b.X0, b.X1, b.Bottom, b.Top);
                // where its stretch runs below the hull's bottom (a stern cut up out of the water), the plate ends on the
                // bottom: a point under it rides up to the lowest height where there is a side
                var floor = xs.Select(x => zs.Where(z => HalfWidth(x, z) > 0.05).Append(double.NaN).First()).ToList();
                if (floor.All(double.IsNaN))
                    continue;
                var has = Enumerable.Range(0, xs.Count).Where(k => !double.IsNaN(floor[k])).ToList();
                var px = xs.Select((x, k) => double.IsNaN(floor[k]) ? xs[has.MinBy(j => Math.Abs(j - k))] : x).ToList();
                var pz = floor.Select((f, k) => double.IsNaN(f) ? floor[has.MinBy(j => Math.Abs(j - k))] : f).ToList();
                foreach (int side in new[] { 1, -1 })
                    AddLoft(b.Kind, b.Id, b.Src, zs, z =>
                    {
                        var at = px.Select((x, k) => (X: x, Z: Math.Max(z, pz[k]))).ToList();
                        var hw = at.Select(p => HalfWidth(p.X, p.Z)).ToList();
                        var outer = at.Select((p, k) => new P3(p.X, side * (hw[k] + 0.15), p.Z));
                        var inner = at.Select((p, k) => new P3(p.X, side * Math.Max(hw[k] - 0.25, 0), p.Z)).Reverse();
                        return [.. outer, .. inner];
                    });
            }
            // a plate across the hull at x, inside its side at every height
            void Across(string id, object src, double x, double lo, double hi) =>
                AddLoft("armoured_bulkhead", id, src, ZsOver(x - 0.15, x + 0.15, lo, hi), z =>
                {
                    double w = Math.Max(HalfWidth(x, z) - 0.2, 0.0);
                    return [new(x - 0.15, -w, z), new(x + 0.15, -w, z), new(x + 0.15, w, z), new(x - 0.15, w, z)];
                });
            foreach (var bh in hb.Bulkheads)   // the citadel's armoured ends, across the hull
                if (bh is { Kind: "armoured", X: { } x, ArmourBottom: { } lo, ArmourTop: { } hi })
                    Across(bh.Id, bh, x, lo, hi);
            foreach (var p in arm.Bulkheads ?? [])   // an end belt's plate across a hull end face (a transom), just inside it
                if (Math.Abs(p.X) >= hb.Length / 2 - 0.01)
                {
                    double x = p.X - Math.Sign(p.X) * 0.15;
                    if (HalfWidth(x, (p.Bottom + p.Top) / 2) > 0.01)
                        Across(p.Id, p, x, p.Bottom, p.Top);
                }
            foreach (var d in arm.Decks ?? [])
            {
                // inset from the side; stations where the hull is too narrow there are left out (hitview let the two
                // sides cross into a bow tie)
                var row = Enumerable.Range(0, 13).Select(k => d.X0 + (d.X1 - d.X0) * k / 12)
                    .Select(x => (X: x, W: HalfWidth(x, d.Z) - 0.2)).Where(p => p.W > 0.01).ToList();
                var pts = row.Select(p => new Pt(p.X, p.W)).Concat(Enumerable.Reverse(row).Select(p => new Pt(p.X, -p.W))).ToList();
                Add("armour_deck", $"{d.Deck} armour", d, pts, d.Z - 0.15, d.Z);
            }
        }

        /// <summary>The guides: the waterline (a band, and a line with no prism), and the hull's edges (lines with no
        /// prism: deck outline, keel line, stem and stern posts, a few sections).</summary>
        void AddGuides(double keel, double wl)
        {
            uint waterline = (uint)HitKinds.IndexOf("waterline"), hull = (uint)HitKinds.IndexOf("hull_lines");
            // the waterline is what direct fire can't get under: a solid band around the hull, proud of the belt
            // (0.15 m out) so the armour never hides it, with the exact line along its face
            const double In = 0.2, Out = 0.35, Half = 0.4;
            var wlOutline = FormOutline(wl);
            int half = wlOutline.Count / 2;   // FormOutline: the port side aft to fore, then starboard fore to aft
            foreach (int side in new[] { 1, -1 })
            {
                var row = wlOutline.Take(half).Select(p => (p.X, W: -p.Y)).ToList();
                var outer = row.Select(p => new Pt(p.X, side * (p.W + Out)));
                var inner = Enumerable.Reverse(row).Select(p => new Pt(p.X, side * (p.W + In)));
                Add("waterline", side > 0 ? "waterline, starboard" : "waterline, port", null, [.. outer, .. inner], wl - Half, wl + Half);
            }
            Polyline(wlOutline.Select(p => new Vector3((float)p.X, (float)(p.Y + Math.Sign(p.Y) * (Out + 0.02)), (float)wl)), waterline, closed: true);
            Polyline(hb.Hull.Select(p => new Vector3((float)p.X, (float)p.Y, 0)), hull, closed: true);
            var st = hb.HullForm.Stations;
            if (st.Count >= 2)
            {
                Polyline(st.Select(s => new Vector3((float)s.X, 0, (float)s.Z[0])), hull, closed: false);
                foreach (var s in new[] { st[0], st[^1] })
                    Polyline([new((float)s.X, 0, (float)s.Z[0]), new((float)s.X, 0, 0)], hull, closed: false);
                for (int i = 4; i < st.Count - 4; i += 4)
                {
                    var s = st[i];
                    var sec = s.Y.Zip(s.Z, (y, z) => (Y: (float)y, Z: (float)z)).ToList();
                    var pts = Enumerable.Reverse(sec).Select(p => new Vector3((float)s.X, -p.Y, p.Z)).Concat(sec.Select(p => new Vector3((float)s.X, p.Y, p.Z)));
                    Polyline(pts, hull, closed: false);
                }
            }
            else if (hb.Hull.Count > 0)
            {
                float x0 = (float)hb.Hull.Min(p => p.X), x1 = (float)hb.Hull.Max(p => p.X);
                Polyline([new(x0, 0, (float)keel), new(x1, 0, (float)keel)], hull, closed: false);
            }
        }

        void Polyline(IEnumerable<Vector3> pts, uint kind, bool closed)
        {
            uint first = (uint)verts.Count;
            foreach (var p in pts)
                verts.Add(new HitVertex { Position = p, Normal = Vector3.UnitZ, Kind = kind, Prism = NoPrism });
            uint n = (uint)verts.Count - first;
            for (uint i = 0; i + 1 < n; i++)
                lines.AddRange([first + i, first + i + 1]);
            if (closed && n > 2)
                lines.AddRange([first + n - 1, first]);
        }

        /// <summary>Extrudes a footprint into a prism: top and bottom caps, a quad per side, and its outline (both
        /// rings, and the vertical edges at sharp corners).</summary>
        void Add(string kindName, string id, object? src, IReadOnlyList<Pt> poly, double baseZ, double topZ)
        {
            var fp = Clean(poly);
            if (fp.Count < 3 || topZ <= baseZ)
                return;
            if (SignedArea(fp) < 0)
                fp.Reverse();
            int kind = HitKinds.IndexOf(kindName);
            var hk = HitKinds.All[kind];
            uint pi = (uint)prisms.Count;
            float b = (float)baseZ, t = (float)topZ;
            int n = fp.Count;
            var prism = new HitPrism
            {
                Id = id, Kind = kind, Source = src, Base = b, Top = t,
                Footprint = fp.Select(p => new Vector2((float)p.X, (float)p.Y)).ToArray(),
                FirstIndex = tris.Count, FirstEdge = lines.Count,
            };
            HitVertex V(Vector2 p, float z, Vector3 nrm) => new() { Position = new(p, z), Normal = nrm, Kind = (uint)kind, Prism = pi };

            // prisms often share a face: a cap height (a turret's body and its parts, a barbette flush with a deckhouse
            // roof, an armour deck on the cells) or a wall (neighbouring cells). The prism is drawn grown by a hair,
            // more the smaller the footprint, so no two faces lie in one plane and the smaller sits proud instead of
            // z-fighting. Footprint, Base, Top and the bounds keep the true shape.
            float dz = 0.002f + 0.03f / MathF.Sqrt(1 + (float)SignedArea(fp));
            float tDraw = t + dz, bDraw = b - dz;
            var drawn = Outset(prism.Footprint, dz);

            // caps: the same triangulation at the top and the bottom
            var capTris = Triangulate(fp);
            uint top0 = (uint)verts.Count;
            foreach (var p in drawn)
                verts.Add(V(p, tDraw, Vector3.UnitZ));
            uint bot0 = (uint)verts.Count;
            foreach (var p in drawn)
                verts.Add(V(p, bDraw, -Vector3.UnitZ));
            foreach (int k in capTris)
                tris.Add(top0 + (uint)k);
            foreach (int k in capTris)
                tris.Add(bot0 + (uint)k);

            // sides: four vertices each, for the flat normal
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = drawn[i], p1 = drawn[(i + 1) % n], d = p1 - p0;
                var nrm = Vector3.Normalize(new Vector3(d.Y, -d.X, 0));
                uint s = (uint)verts.Count;
                verts.AddRange([V(p0, bDraw, nrm), V(p1, bDraw, nrm), V(p1, tDraw, nrm), V(p0, tDraw, nrm)]);
                tris.AddRange([s, s + 1, s + 2, s, s + 2, s + 3]);
            }

            if (hk.Edges)
            {
                for (int i = 0; i < n; i++)
                {
                    uint j = (uint)((i + 1) % n);
                    lines.AddRange([top0 + (uint)i, top0 + j, bot0 + (uint)i, bot0 + j]);
                    // a vertical edge where the outline turns sharply (not along a circle's facets)
                    Vector2 a = prism.Footprint[(i + n - 1) % n], c = prism.Footprint[i], e = prism.Footprint[(i + 1) % n];
                    if (Vector2.Dot(Vector2.Normalize(c - a), Vector2.Normalize(e - c)) < SharpCos)
                        lines.AddRange([bot0 + (uint)i, top0 + (uint)i]);
                }
            }

            prism.IndexCount = tris.Count - prism.FirstIndex;
            prism.EdgeCount = lines.Count - prism.FirstEdge;
            var lo = prism.Footprint.Aggregate(new Vector2(float.MaxValue), Vector2.Min);
            var hi = prism.Footprint.Aggregate(new Vector2(float.MinValue), Vector2.Max);
            prism.Min = new(lo, b);
            prism.Max = new(hi, t);
            prisms.Add(prism);
        }
    }

    /// <summary>A counter-clockwise polygon moved out by <paramref name="e"/>: each corner along its mitre, limited to
    /// 4e at sharp ones.</summary>
    static Vector2[] Outset(Vector2[] fp, float e)
    {
        var o = new Vector2[fp.Length];
        static Vector2 Out(Vector2 a, Vector2 b)
        {
            var d = b - a;
            float l = d.Length();
            return l > 1e-9f ? new Vector2(d.Y, -d.X) / l : Vector2.Zero;
        }
        for (int i = 0; i < fp.Length; i++)
        {
            Vector2 n0 = Out(fp[(i + fp.Length - 1) % fp.Length], fp[i]), n1 = Out(fp[i], fp[(i + 1) % fp.Length]);
            var m = n0 + n1;
            float k = 1 + Vector2.Dot(n0, n1);
            o[i] = fp[i] + (k > 1f / 8 ? m * (e / k) : (m.LengthSquared() > 1e-12f ? Vector2.Normalize(m) : n1) * (4 * e));
        }
        return o;
    }

    /// <summary>The polygon without repeated points (and without the closing point, if it repeats the first).</summary>
    static List<Pt> Clean(IReadOnlyList<Pt> poly)
    {
        var o = new List<Pt>(poly.Count);
        foreach (var p in poly)
            if (o.Count == 0 || Math.Abs(p.X - o[^1].X) > 1e-6 || Math.Abs(p.Y - o[^1].Y) > 1e-6)
                o.Add(p);
        while (o.Count > 1 && Math.Abs(o[0].X - o[^1].X) <= 1e-6 && Math.Abs(o[0].Y - o[^1].Y) <= 1e-6)
            o.RemoveAt(o.Count - 1);
        return o;
    }

    internal static double SignedArea(IReadOnlyList<Pt> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var (u, v) = (p[i], p[(i + 1) % p.Count]);
            a += u.X * v.Y - v.X * u.Y;
        }
        return a / 2;
    }

    /// <summary>Ear clipping of a counter-clockwise simple polygon: triangles as index triples into it. Footprints are
    /// small (a few hundred points at most); a polygon with no ear left (self-touching) is finished as a fan.</summary>
    internal static List<int> Triangulate(IReadOnlyList<Pt> p)
    {
        var idx = Enumerable.Range(0, p.Count).ToList();
        var o = new List<int>((p.Count - 2) * 3);
        static double Cross(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        int guard = 0;
        while (idx.Count > 3 && guard++ < 4 * p.Count)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                var (a, b, c) = (p[ia], p[ib], p[ic]);
                double cr = Cross(a, b, c);
                if (cr < 0)
                    continue;   // reflex
                if (cr < 1e-12)   // collinear: drop the middle point, no triangle
                {
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                bool ear = true;
                foreach (int k in idx)
                {
                    if (k == ia || k == ib || k == ic)
                        continue;
                    var q = p[k];
                    if (Cross(a, b, q) >= 0 && Cross(b, c, q) >= 0 && Cross(c, a, q) >= 0)
                    {
                        ear = false;
                        break;
                    }
                }
                if (!ear)
                    continue;
                o.AddRange([ia, ib, ic]);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped)
                break;
        }
        for (int i = 1; i + 1 < idx.Count; i++)   // the last triangle, or a fan over what no ear could take
            o.AddRange([idx[0], idx[i], idx[i + 1]]);
        return o;
    }
}

/// <summary>A point in ship space, metres (a loft's ring point).</summary>
readonly record struct P3(double X, double Y, double Z);
