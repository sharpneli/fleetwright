using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>A class's armour as solids for ray tests, as shell-test first compiled them from the hitboxes: the side
/// armour (belt and strakes) on the hull's side, armour decks and bulkheads as planes, barbettes as tubes (only the
/// wall is armour, the open top isn't), uptakes as the openings they make in the decks, and every other armoured part
/// as a solid (entering it anywhere is armour). "Armoured": armour_mm over 0, and every gun mount with turret armour
/// (main and secondary, even at 0 mm).</summary>
public sealed class ArmourSolids
{
    /// <summary>A prism (Outline, in this object's Points) or a cylinder (Cx, Cy, R; Wall: a tube's wall) between
    /// Box.Base and Box.Top.</summary>
    public readonly record struct Solid(int Component, bool Cylinder, double Cx, double Cy, double R, double Wall, Box Box, Slice Outline);

    readonly Solid[] armoured, barbettes, uptakes;
    readonly int[] sides, decks, bulkheads;
    readonly Pt[] pts;

    /// <summary>The highest armour or deck top, plus a margin: a ray above it has left the ship's armour.</summary>
    public double ZMax { get; }

    /// <summary>The armoured solids, in component order.</summary>
    public ReadOnlySpan<Solid> Armoured => armoured;
    /// <summary>The armoured barbettes, as tubes.</summary>
    public ReadOnlySpan<Solid> BarbetteTubes => barbettes;
    /// <summary>The uptakes (with 3 points or more): where they pass through a deck it has a hole.</summary>
    public ReadOnlySpan<Solid> Uptakes => uptakes;
    /// <summary>The side plates (ShipClass.SidePlates) more than 0 mm thick: the belt first, then the strakes.</summary>
    public ReadOnlySpan<int> Sides => sides;
    /// <summary>The armour decks (ShipClass.ArmourDecks) and bulkheads (ArmourBulkheads) more than 0 mm thick.</summary>
    public ReadOnlySpan<int> Decks => decks;
    public ReadOnlySpan<int> Bulkheads => bulkheads;

    public ReadOnlySpan<Pt> Points(Slice s) => pts.AsSpan(s.Start, s.Count);

    internal ArmourSolids(ShipClass c)
    {
        var p = new List<Pt>();
        var arm = new List<Solid>();
        var bar = new List<Solid>();
        var up = new List<Solid>();
        Solid Prism(int i, ReadOnlySpan<Pt> outline, double bse, double top)
        {
            double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
            foreach (var q in outline)
                (x0, x1, y0, y1) = (Math.Min(x0, q.X), Math.Max(x1, q.X), Math.Min(y0, q.Y), Math.Max(y1, q.Y));
            var s = new Slice(p.Count, outline.Length);
            foreach (var q in outline)
                p.Add(q);
            return new Solid(i, false, 0, 0, 0, 0, new Box(x0, x1, y0, y1, bse, top), s);
        }
        static Solid Cyl(int i, in ComponentRow r) => new(i, true, r.X, r.Y, r.R, Math.Max(r.ArmourMm / 1000, 0.05),
            new Box(r.X - r.R, r.X + r.R, r.Y - r.R, r.Y + r.R, r.Bounds.Base, r.Bounds.Top), Slice.Empty);

        double zMax = Math.Max(c.Hull.Top, 0.0);
        var comps = c.Components;
        for (int i = 0; i < comps.Length; i++)
        {
            ref readonly var r = ref comps[i];
            bool mount = r.Kind is ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo;
            bool armoured = r.ArmourMm > 0 || mount && c.Mounts[r.Detail].Turret;
            if (r.Kind == ComponentKind.Barbette && armoured)
                bar.Add(Cyl(i, r));
            else if (r.Kind == ComponentKind.Uptake && r.Outline.Count >= 3)
                up.Add(Prism(i, c.Points(r.Outline), r.Bounds.Base, r.Bounds.Top));
            else if (!armoured)
                continue;
            else if (mount)
            {
                // the turret's body at rest, as Geometry.RotateTranslate turns it
                ref readonly var m = ref c.Mounts[r.Detail];
                var local = c.Points(m.Body);
                double cs = Math.Cos(double.DegreesToRadians(m.RestDeg)), sn = Math.Sin(double.DegreesToRadians(m.RestDeg));
                var turned = new Pt[local.Length];
                for (int k = 0; k < local.Length; k++)
                    turned[k] = new Pt(m.X + local[k].X * cs - local[k].Y * sn, m.Y + local[k].X * sn + local[k].Y * cs);
                arm.Add(Prism(i, turned, r.Bounds.Base, r.Bounds.Top));
            }
            else if (r.Shape == ComponentShape.Circle && !double.IsNaN(r.R))
                arm.Add(Cyl(i, r));
            else if (r.Outline.Count >= 3)
                arm.Add(Prism(i, c.Points(r.Outline), r.Bounds.Base, r.Bounds.Top));
            else
                continue;
            zMax = Math.Max(zMax, r.Bounds.Top);
        }
        ZMax = zMax + 0.5;
        (armoured, barbettes, uptakes, pts) = (arm.ToArray(), bar.ToArray(), up.ToArray(), p.ToArray());

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
    }

    /// <summary>Is (x, y) inside the solid's plan (a cylinder's circle, strictly; a prism's polygon by crossings)?</summary>
    public bool InPlan(in Solid s, double x, double y) =>
        s.Cylinder ? (x - s.Cx) * (x - s.Cx) + (y - s.Cy) * (y - s.Cy) < s.R * s.R : PointInPolygon(x, y, Points(s.Outline));

    public bool Inside(in Solid s, double x, double y, double z) => s.Box.Contains(x, y, z) && InPlan(s, x, y);

    /// <summary>Inside a tube's wall: the opening a barbette makes in a deck it stands on or passes through.</summary>
    public static bool InBore(in Solid s, double x, double y) =>
        (x - s.Cx) * (x - s.Cx) + (y - s.Cy) * (y - s.Cy) < (s.R - s.Wall) * (s.R - s.Wall);

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
}
