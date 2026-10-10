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

/// <summary>One hitbox shape as drawn: a footprint extruded from Base to Top. Its faces are
/// Indices[FirstIndex..+IndexCount] (triangles) and its outline EdgeIndices[FirstEdge..+EdgeCount] (lines).</summary>
public sealed class HitPrism
{
    public required string Id { get; init; }
    public required int Kind { get; init; }

    /// <summary>What it was made from: a <see cref="Component"/>, <see cref="Cell"/>, armour report or null (hull).</summary>
    public object? Source { get; init; }

    public required Vector2[] Footprint { get; init; }
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

    sealed class Builder(Hitboxes hb)
    {
        readonly List<HitVertex> verts = [];
        readonly List<uint> tris = [];
        readonly List<uint> lines = [];
        readonly List<HitPrism> prisms = [];

        double HalfWidth(double x, double z) => hb.HullForm.Stations.Count >= 2 ? hb.HullForm.HalfWidth(x, z) : DeckHalfWidth(x);

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

        static List<Pt> Circle(double x, double y, double r) =>
            Enumerable.Range(0, CircleN).Select(k => new Pt(x + r * Math.Cos(2 * Math.PI * k / CircleN), y + r * Math.Sin(2 * Math.PI * k / CircleN))).ToList();

        static List<Pt> Box(double x0, double x1, double y0, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

        public HitboxMesh Run()
        {
            var vert = hb.Vertical;
            double keel = vert.Keel, wl = vert.Waterline;

            // the hull in slices, each its outline at the slice's middle: it narrows to the keel
            var zs = new[] { 0.0, 0.06, 0.15, 0.3, 0.5, 0.75, 1.0 }.Select(f => keel + (Math.Min(wl, 0.0) - keel) * f).ToList();
            if (wl < 0.0)
                zs.Add(0.0);
            for (int i = 0; i + 1 < zs.Count; i++)
                Add("hull", $"hull {zs[i]:F1} to {zs[i + 1]:F1} m", null, FormOutline((zs[i] + zs[i + 1]) / 2), zs[i], zs[i + 1]);

            foreach (var c in hb.Components)
                AddComponent(c);

            var rooms = hb.Rooms.ToDictionary(r => r.Id, StringComparer.Ordinal);
            foreach (var c in hb.Cells)
                Add(rooms.TryGetValue(c.Room, out var room) ? room.Kind : "other", c.Id, c, CellOutline(c), c.Base, c.Top);

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
                // a thin slab just outside the hull side over its stretch, split at the waterline: it follows the flare
                var zs = new List<double> { b.Bottom };
                if (b.Bottom < wl && wl < b.Top)
                    zs.Add(wl);
                zs.Add(b.Top);
                for (int i = 0; i + 1 < zs.Count; i++)
                {
                    double zm = (zs[i] + zs[i + 1]) / 2;
                    foreach (int side in new[] { 1, -1 })
                    {
                        var xs = Enumerable.Range(0, 13).Select(k => b.X0 + (b.X1 - b.X0) * k / 12).ToList();
                        var hw = xs.Select(x => HalfWidth(x, zm)).ToList();
                        var outer = xs.Select((x, k) => new Pt(x, side * (hw[k] + 0.15)));
                        var inner = xs.Select((x, k) => new Pt(x, side * Math.Max(hw[k] - 0.25, 0))).Reverse();
                        Add(b.Kind, b.Id, b.Src, [.. outer, .. inner], zs[i], zs[i + 1]);
                    }
                }
            }
            foreach (var bh in hb.Bulkheads)   // the citadel's armoured ends, across the hull
                if (bh is { Kind: "armoured", X: { } x, ArmourBottom: { } lo, ArmourTop: { } hi })
                {
                    double w = HalfWidth(x, (lo + hi) / 2) - 0.2;
                    Add("armoured_bulkhead", bh.Id, bh, Box(x - 0.15, x + 0.15, -w, w), lo, hi);
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

        /// <summary>Lines with no prism: the waterline, and the hull's edges (deck outline, keel line, stem and stern
        /// posts, a few sections).</summary>
        void AddGuides(double keel, double wl)
        {
            uint waterline = (uint)HitKinds.IndexOf("waterline"), hull = (uint)HitKinds.IndexOf("hull_lines");
            Polyline(FormOutline(wl).Select(p => new Vector3((float)p.X, (float)p.Y, (float)wl)), waterline, closed: true);
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

            // caps: the same triangulation at the top and the bottom
            var capTris = Triangulate(fp);
            uint top0 = (uint)verts.Count;
            foreach (var p in prism.Footprint)
                verts.Add(V(p, t, Vector3.UnitZ));
            uint bot0 = (uint)verts.Count;
            foreach (var p in prism.Footprint)
                verts.Add(V(p, b, -Vector3.UnitZ));
            foreach (int k in capTris)
                tris.Add(top0 + (uint)k);
            foreach (int k in capTris)
                tris.Add(bot0 + (uint)k);

            // sides: four vertices each, for the flat normal
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = prism.Footprint[i], p1 = prism.Footprint[(i + 1) % n], d = p1 - p0;
                var nrm = Vector3.Normalize(new Vector3(d.Y, -d.X, 0));
                uint s = (uint)verts.Count;
                verts.AddRange([V(p0, b, nrm), V(p1, b, nrm), V(p1, t, nrm), V(p0, t, nrm)]);
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
