using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>A class's parts and armour as solids for the tracer (<see cref="ShipTracer"/>, docs/battle/fighting.md 4.1):
/// every component as a solid of its own shape, indexed by the component; the side armour (belt and strakes) on the
/// hull's side, armour decks and bulkheads as planes; and the cells found by section. Built once with the class and
/// read-only after, so every thread traces through the same one.
/// <para>A component's solid: a mount's turret body (in turret space, so it turns with the mount's bearing); a
/// barbette's tube (only the wall is armour, the open top isn't); a polygon's prism; a circle's cylinder; a segment's
/// capsule (shafts, submerged tubes); anything else its box. "Armoured" is armour_mm over 0, and every gun mount with
/// turret armour (main and secondary, even at 0 mm).</para></summary>
public sealed class ShipSolids
{
    public enum Shape : byte
    {
        /// <summary>Its box (Box).</summary>
        Box,
        /// <summary>A polygon (Outline, in the class's Points) between Box.Base and Box.Top.</summary>
        Prism,
        /// <summary>A circle (Cx, Cy, R) between Box.Base and Box.Top.</summary>
        Cylinder,
        /// <summary>A barbette: a cylinder whose wall (Wall thick) is its armour, open at the top.</summary>
        Tube,
        /// <summary>A segment A to B, R round it.</summary>
        Capsule,
        /// <summary>A mount's turret body (Outline: the mount's Body, in turret space), turned to its bearing about
        /// (Cx, Cy); R reaches its farthest point, so Box holds it at any bearing.</summary>
        Turret,
    }

    public readonly record struct Solid(int Component, Shape Shape, Box Box, double Cx, double Cy, double R, double Wall, P3 A, P3 B,
        Slice Outline, int Mount, bool Armoured, bool Uptake);

    readonly ShipClass c;
    readonly Solid[] solids;
    readonly int[] tubes, uptakes, sides, decks, bulkheads;
    readonly double[] sectionX0;
    readonly int[] sectionOrder, sectionFirst, sectionCells;
    readonly Pt[] waterline;

    /// <summary>Every component's solid, at its component index.</summary>
    public ReadOnlySpan<Solid> Solids => solids;
    /// <summary>The barbettes (components), whose bores pierce the decks they stand on or pass through.</summary>
    public ReadOnlySpan<int> Tubes => tubes;
    /// <summary>The uptakes (components): where they pass through an armour deck, it has an opening (a grating).</summary>
    public ReadOnlySpan<int> Uptakes => uptakes;
    /// <summary>The side plates (ShipClass.SidePlates) more than 0 mm thick: the belt first, then the strakes.</summary>
    public ReadOnlySpan<int> Sides => sides;
    /// <summary>The armour decks (ShipClass.ArmourDecks) and bulkheads (ArmourBulkheads) more than 0 mm thick.</summary>
    public ReadOnlySpan<int> Decks => decks;
    public ReadOnlySpan<int> Bulkheads => bulkheads;

    /// <summary>The ship's box: every solid and the hull, with a margin (2 m round, 1 m under the keel, 0.5 m over the
    /// top). A ray outside it has left the ship.</summary>
    public Box Bounds { get; }
    /// <summary>How far the box reaches from the waterline amidships (0, 0, Waterline): the broadphase's radius.</summary>
    public double Radius { get; }
    /// <summary>The level waterline's starboard half at the hull's stations, aft to fore (x, half-breadth): where a
    /// miss fell beside the hull is measured from it.</summary>
    public ReadOnlySpan<Pt> Waterline => waterline;

    internal ShipSolids(ShipClass c)
    {
        this.c = c;
        var comps = c.Components;
        solids = new Solid[comps.Length];
        var tb = new List<int>();
        var up = new List<int>();
        var hull = c.Hull;
        double x0 = Math.Min(hull.X0, -c.Length / 2), x1 = Math.Max(hull.X1, c.Length / 2), y1 = Math.Max(hull.YMax, c.Beam / 2);
        double zLo = Math.Min(c.Vertical.Keel, hull.Keel), zHi = Math.Max(hull.Top, 0.0);
        for (int i = 0; i < comps.Length; i++)
        {
            ref readonly var r = ref comps[i];
            bool mount = r.Kind is ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo;
            bool armoured = r.ArmourMm > 0 || mount && c.Mounts[r.Detail].Turret;
            bool uptake = r.Kind == ComponentKind.Uptake;
            Solid s;
            if (mount)
            {
                ref readonly var m = ref c.Mounts[r.Detail];
                double reach = 0;
                foreach (var p in c.Points(m.Body))
                    reach = Math.Max(reach, Math.Sqrt(p.X * p.X + p.Y * p.Y));
                s = new Solid(i, Shape.Turret, new Box(m.X - reach, m.X + reach, m.Y - reach, m.Y + reach, r.Bounds.Base, r.Bounds.Top), m.X, m.Y,
                    reach, 0, default, default, m.Body, r.Detail, armoured, false);
            }
            else if (r.Kind == ComponentKind.Barbette)
            {
                s = new Solid(i, Shape.Tube, CircleBox(r), r.X, r.Y, r.R, Math.Max(r.ArmourMm / 1000, 0.05), default, default, Slice.Empty, -1, armoured,
                    false);
                tb.Add(i);
            }
            else if (r.Shape == ComponentShape.Circle && !double.IsNaN(r.R))
                s = new Solid(i, Shape.Cylinder, CircleBox(r), r.X, r.Y, r.R, 0, default, default, Slice.Empty, -1, armoured, uptake);
            else if (r.Shape == ComponentShape.Polygon && r.Outline.Count >= 3)
                s = new Solid(i, Shape.Prism, r.Bounds, 0, 0, 0, 0, default, default, r.Outline, -1, armoured, uptake);
            else if (r.Shape == ComponentShape.Segment && r.Detail >= 0 && SegmentOf(c, r) is var (a, b) && !double.IsNaN(a.X) && !double.IsNaN(r.R))
                s = new Solid(i, Shape.Capsule, new Box(Math.Min(a.X, b.X) - r.R, Math.Max(a.X, b.X) + r.R, Math.Min(a.Y, b.Y) - r.R,
                    Math.Max(a.Y, b.Y) + r.R, Math.Min(a.Z, b.Z) - r.R, Math.Max(a.Z, b.Z) + r.R), 0, 0, r.R, 0, a, b, Slice.Empty, -1, armoured, uptake);
            else
                s = new Solid(i, Shape.Box, r.Bounds, 0, 0, 0, 0, default, default, Slice.Empty, -1, armoured, uptake);
            solids[i] = s;
            if (uptake)
                up.Add(i);
            (x0, x1, y1) = (Math.Min(x0, s.Box.X0), Math.Max(x1, s.Box.X1), Math.Max(y1, Math.Max(-s.Box.Y0, s.Box.Y1)));
            (zLo, zHi) = (Math.Min(zLo, s.Box.Base), Math.Max(zHi, s.Box.Top));
        }
        (tubes, uptakes) = (tb.ToArray(), up.ToArray());
        Bounds = new Box(x0 - 2, x1 + 2, -y1 - 2, y1 + 2, zLo - 1, zHi + 0.5);
        double wl = c.Vertical.Waterline;
        Radius = Math.Sqrt(Math.Max(Bounds.X0 * Bounds.X0, Bounds.X1 * Bounds.X1) + Bounds.Y1 * Bounds.Y1
            + Math.Max((Bounds.Base - wl) * (Bounds.Base - wl), (Bounds.Top - wl) * (Bounds.Top - wl)));

        var sd = new List<int>();
        for (int i = 0; i < c.SidePlates.Length; i++)
            if (c.SidePlates[i].ThicknessMm > 0)
                sd.Add(i);
        var dk = new List<int>();
        for (int i = 0; i < c.ArmourDecks.Length; i++)
            if (c.ArmourDecks[i].ThicknessMm > 0)
                dk.Add(i);
        var bh = new List<int>();
        for (int i = 0; i < c.ArmourBulkheads.Length; i++)
            if (c.ArmourBulkheads[i].ThicknessMm > 0)
                bh.Add(i);
        (sides, decks, bulkheads) = (sd.ToArray(), dk.ToArray(), bh.ToArray());

        var wlPts = new List<Pt>();
        for (int i = 0; i <= hull.StripCount; i++)
        {
            double x = hull.StationX(i);
            wlPts.Add(new Pt(x, i == 0 || i == hull.StripCount ? 0 : hull.HalfWidth(x, wl)));
        }
        waterline = wlPts.ToArray();

        // the cells by section, the sections along x
        var secs = c.Sections.ToArray();
        sectionOrder = Enumerable.Range(0, secs.Length).OrderBy(i => secs[i].X0).ToArray();
        sectionX0 = sectionOrder.Select(i => secs[i].X0).ToArray();
        var bySection = new List<int>[secs.Length];
        for (int i = 0; i < secs.Length; i++)
            bySection[i] = [];
        for (int i = 0; i < c.Cells.Length; i++)
            bySection[c.Cells[i].Section].Add(i);
        sectionFirst = new int[secs.Length + 1];
        var cells = new List<int>();
        for (int k = 0; k < secs.Length; k++)
        {
            sectionFirst[k] = cells.Count;
            cells.AddRange(bySection[sectionOrder[k]]);
        }
        sectionFirst[secs.Length] = cells.Count;
        sectionCells = cells.ToArray();
    }

    static Box CircleBox(in ComponentRow r) => new(r.X - r.R, r.X + r.R, r.Y - r.R, r.Y + r.R, r.Bounds.Base, r.Bounds.Top);

    /// <summary>A segment component's ends: a shaft's or a submerged tube's.</summary>
    static (P3, P3) SegmentOf(ShipClass c, in ComponentRow r) => r.Kind switch
    {
        ComponentKind.Shaft => (c.ShaftLines[r.Detail].P0, c.ShaftLines[r.Detail].P1),
        ComponentKind.TorpedoTube => (c.Tubes[r.Detail].P0, c.Tubes[r.Detail].P1),
        _ => (new P3(double.NaN, double.NaN, double.NaN), default),
    };

    // ------------------------------------------------------------------ inside

    /// <summary>A mount's bearing: the one given, or its rest.</summary>
    public double BearingOf(in Solid s, ReadOnlySpan<double> bearingsDeg) =>
        s.Mount < bearingsDeg.Length && !double.IsNaN(bearingsDeg[s.Mount]) ? bearingsDeg[s.Mount] : c.Mounts[s.Mount].RestDeg;

    /// <summary>Is the point inside the solid (a turret turned to its bearing)?</summary>
    public bool Inside(in Solid s, double x, double y, double z, double bearingDeg = 0)
    {
        if (!s.Box.Contains(x, y, z))
            return false;
        switch (s.Shape)
        {
            case Shape.Prism:
                return PointInPolygon(x, y, c.Points(s.Outline));
            case Shape.Cylinder or Shape.Tube:
                return (x - s.Cx) * (x - s.Cx) + (y - s.Cy) * (y - s.Cy) < s.R * s.R;
            case Shape.Capsule:
                return SegmentDistance2(s, x, y, z, out _) <= s.R * s.R;
            case Shape.Turret:
                var (lx, ly) = ToTurret(s, x, y, bearingDeg);
                return PointInPolygon(lx, ly, c.Points(s.Outline));
            default:
                return true;
        }
    }

    /// <summary>Is (x, y) inside the solid's plan (a cylinder's circle, strictly; a prism's polygon by crossings)?</summary>
    public bool InPlan(in Solid s, double x, double y) => s.Shape switch
    {
        Shape.Cylinder or Shape.Tube => (x - s.Cx) * (x - s.Cx) + (y - s.Cy) * (y - s.Cy) < s.R * s.R,
        Shape.Prism => PointInPolygon(x, y, c.Points(s.Outline)),
        _ => x >= s.Box.X0 && x <= s.Box.X1 && y >= s.Box.Y0 && y <= s.Box.Y1,
    };

    /// <summary>Inside a tube's wall: the opening a barbette makes in a deck it stands on or passes through.</summary>
    public static bool InBore(in Solid s, double x, double y) =>
        (x - s.Cx) * (x - s.Cx) + (y - s.Cy) * (y - s.Cy) < (s.R - s.Wall) * (s.R - s.Wall);

    /// <summary>A point in a turret's own frame (pivot at 0, barrels along +x) at a bearing.</summary>
    public static (double X, double Y) ToTurret(in Solid s, double x, double y, double bearingDeg)
    {
        double a = double.DegreesToRadians(bearingDeg), cs = Math.Cos(a), sn = Math.Sin(a), dx = x - s.Cx, dy = y - s.Cy;
        return (dx * cs + dy * sn, -dx * sn + dy * cs);
    }

    /// <summary>The squared distance from a point to a capsule's axis, and the parameter of the nearest point on it.</summary>
    public static double SegmentDistance2(in Solid s, double x, double y, double z, out double u)
    {
        double ax = s.B.X - s.A.X, ay = s.B.Y - s.A.Y, az = s.B.Z - s.A.Z, l2 = ax * ax + ay * ay + az * az;
        u = l2 > 0 ? Math.Clamp(((x - s.A.X) * ax + (y - s.A.Y) * ay + (z - s.A.Z) * az) / l2, 0, 1) : 0;
        double dx = x - s.A.X - u * ax, dy = y - s.A.Y - u * ay, dz = z - s.A.Z - u * az;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>Geometry.PointInPolygon over a span: the even-odd rule by crossings.</summary>
    public static bool PointInPolygon(double x, double y, ReadOnlySpan<Pt> pts)
    {
        bool inside = false;
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            var (x1, y1) = pts[i];
            var (x2, y2) = pts[(i + 1) % n];
            if ((y1 > y) != (y2 > y))
                if (x < x1 + (y - y1) * (x2 - x1) / (y2 - y1))
                    inside = !inside;
        }
        return inside;
    }

    // ------------------------------------------------------------------ cells

    /// <summary>The cell holding the point (its box; the caller knows it's inside the hull), or -1.</summary>
    public int CellAt(double x, double y, double z)
    {
        int k = Array.BinarySearch(sectionX0, x);
        if (k < 0)
            k = ~k - 1;
        // a point on a section's boundary may be in the one before it
        for (int s = Math.Max(k, 0); s >= Math.Max(k - 1, 0) && s < sectionX0.Length; s--)
            for (int j = sectionFirst[s]; j < sectionFirst[s + 1]; j++)
                if (c.Cells[sectionCells[j]].Box.Contains(x, y, z))
                    return sectionCells[j];
        return -1;
    }
}
