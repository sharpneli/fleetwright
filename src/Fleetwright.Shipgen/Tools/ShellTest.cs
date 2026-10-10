namespace Fleetwright.Shipgen.Tools;

/// <summary>shell-test: an empirical check that the citadel has no holes. Shells are aimed at random points in the
/// citadel's cells under its roof (the highest armour deck over the citadel that the side armour reaches) from random
/// bearings, at fall angles from direct fire (0°) to plunging (50°), and flown as straight
/// lines over their last stretch. Each is traced back from its target out of the ship: it's stopped if on the way in it
/// crossed armour or water, and a leak if it reached the citadel through neither.
/// <para>Only the hitbox model is read, the way a game would read it: the belt and strakes lie on the hull side
/// (HullForm.HalfWidth) over their x0..x1 and bottom..top; an armour deck is the plane z over x0..x1 across the hull, with
/// holes where a barbette (inside its wall) or an uptake passes through it, unless the uptakes have gratings; a bulkhead is the plane x over bottom..top across the hull. A
/// barbette is a tube: its wall is armour, its open top isn't (the turret on it is). Turrets, the conning tower and
/// armoured casings and directors are armoured solids. Water is anything outside the hull below the waterline.</para></summary>
public static class ShellTest
{
    public const double MaxFall = 50.0;
    const double Step = 0.05, Eps = 1e-3;

    /// <summary>A shell that got in. Cause: what let it in, named from the path ("open top of A barbette", "down Funnel 1
    /// uptakes", "under Forward bulkhead"), else the citadel face it came through.</summary>
    public sealed record Leak(string Cause, string Cell, double BearingDeg, double FallDeg, double[] Target, double[] Hole, string HoleFace,
        double[]? Entry, string EntryFace);

    public sealed record Band(double Lo, double Hi)
    {
        public int Shells, Armour, Water, Leaks;
    }

    /// <summary>Roof: the box's roof (null: no armour deck over the citadel, nothing tested); Cells: the cells shot at.</summary>
    public sealed record Result(int Shells, List<Band> Bands, List<Leak> Leaks, double? Roof = null, int Cells = 0);

    abstract class Solid
    {
        public double X0, X1, Y0, Y1, Base, Top;
        public required string Id;
        public bool InBox(double x, double y, double z) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1 && z >= Base && z <= Top;
        public abstract bool InPlan(double x, double y);
        public bool Inside(double x, double y, double z) => InBox(x, y, z) && InPlan(x, y);
    }

    sealed class Cylinder : Solid
    {
        public double Cx, Cy, R, Wall;
        public override bool InPlan(double x, double y) => (x - Cx) * (x - Cx) + (y - Cy) * (y - Cy) < R * R;
        /// <summary>Inside the wall: the opening a barbette makes in the deck it stands on or passes through.</summary>
        public bool InBore(double x, double y) => (x - Cx) * (x - Cx) + (y - Cy) * (y - Cy) < (R - Wall) * (R - Wall);
    }

    sealed class Prism : Solid
    {
        public required List<Pt> Pts;
        public override bool InPlan(double x, double y) => Geometry.PointInPolygon(x, y, Pts);
    }

    sealed class Ship
    {
        public required Hitboxes Hb;
        public double Keel, Wl, ZMax, XMax, YMax;
        public List<RaisedReport> Raised = [];
        public List<(double X0, double X1, double Bottom, double Top)> Sides = [];
        public List<ArmourDeckReport> Decks = [];
        public List<ArmourBulkheadReport> Bulkheads = [];
        public List<Solid> Armoured = [];   // solids: entering one anywhere is armour
        public List<Cylinder> Barbettes = [];   // tubes: only the wall is armour
        public List<Prism> Uptakes = [];
        public List<Cell> Citadel = [];
        public double? Roof;

        public double DeckTop(double x)
        {
            double top = 0.0;
            foreach (var r in Raised)
                if (x >= r.X0 && x <= r.X1)
                    top = Math.Max(top, r.Top);
            return top;
        }

        public double Hw(double x, double z) => Hb.HullForm.HalfWidth(x, z);

        public bool InHull(double x, double y, double z) => z >= Keel && z <= DeckTop(x) && Math.Abs(y) < Hw(x, z);

        public bool InCitadel(double x, double y, double z)
        {
            if (!InHull(x, y, z))
                return false;
            foreach (var c in Citadel)
                if (x >= c.X0 - Eps && x <= c.X1 + Eps && y >= c.Y0 - Eps && y <= c.Y1 + Eps && z >= c.Base - Eps && z <= c.Top + Eps)
                    return true;
            return false;
        }

        /// <summary>Is the deck at z pierced at (x, y): inside a barbette that stands on or through it, or an uptake.</summary>
        public bool Pierced(double x, double y, double z)
        {
            foreach (var b in Barbettes)
                if (b.Base <= z + 0.01 && b.Top > z && b.InBore(x, y))
                    return true;
            foreach (var u in Uptakes)
                if (u.Base < z + 0.01 && u.Top > z - 0.01 && u.InPlan(x, y))
                    return true;
            return false;
        }

        public bool SideArmour(double x, double z)
        {
            foreach (var s in Sides)
                if (x >= s.X0 - Eps && x <= s.X1 + Eps && z >= s.Bottom - Eps && z <= s.Top + Eps)
                    return true;
            return false;
        }
    }

    static Ship Prepare(Hitboxes hb)
    {
        var v = hb.Vertical;
        var s = new Ship
        {
            Hb = hb, Keel = v.Keel, Wl = v.Waterline, XMax = hb.Length / 2 + 2, YMax = hb.Beam / 2 + 2, Raised = v.Raised ?? [],
            Decks = hb.Armour.Decks?.Where(d => d.ThicknessMm > 0).ToList() ?? [],
            Bulkheads = hb.Armour.Bulkheads?.Where(b => b.ThicknessMm > 0).ToList() ?? [],
        };
        if (hb.Armour.Belt is { ThicknessMm: > 0 } belt)
            s.Sides.Add((belt.X0, belt.X1, belt.Bottom, belt.Top));
        foreach (var st in hb.Armour.Strakes ?? [])
            if (st.ThicknessMm > 0)
                s.Sides.Add((st.X0, st.X1, st.Bottom, st.Top));
        // the box: the citadel's cells under its roof, the highest armour deck over the whole citadel that the side
        // armour reaches up to (or the lowest, if none is reached); the tiers above it aren't inside the armour
        var cit = hb.Cells.Where(c => c.Citadel == true).ToList();
        if (cit.Count > 0)
        {
            double cx0 = cit.Min(c => c.X0), cx1 = cit.Max(c => c.X1);
            double side = s.Sides.Where(t => t.X0 < cx1 && t.X1 > cx0).Select(t => t.Top).Append(double.NegativeInfinity).Max();
            var over = s.Decks.Where(d => d.X0 <= cx0 + 0.01 && d.X1 >= cx1 - 0.01).ToList();
            s.Roof = over.Where(d => d.Z <= side + 0.01).Select(d => (double?)d.Z).Max() ?? over.Select(d => (double?)d.Z).Min();
            s.Citadel = s.Roof is double roof ? cit.Where(c => c.Top <= roof + 0.01).ToList() : [];
        }
        static T Bound<T>(T sol, IEnumerable<Pt> pts) where T : Solid
        {
            (sol.X0, sol.X1, sol.Y0, sol.Y1) = (pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.Y));
            return sol;
        }
        Cylinder Cyl(Component c) => new()
        {
            Id = c.Id, Cx = c.X ?? 0, Cy = c.Y ?? 0, R = c.R ?? 0, Wall = Math.Max((c.ArmourMm ?? 0) / 1000, 0.05), Base = c.Base, Top = c.Top,
            X0 = (c.X ?? 0) - (c.R ?? 0), X1 = (c.X ?? 0) + (c.R ?? 0), Y0 = (c.Y ?? 0) - (c.R ?? 0), Y1 = (c.Y ?? 0) + (c.R ?? 0),
        };
        double zMax = s.Raised.Select(r => r.Top).Append(0.0).Max();
        foreach (var c in hb.Components)
        {
            bool armoured = (c.ArmourMm ?? 0) > 0 || c.Armour != null;
            if (c.Kind == "barbette" && armoured)
                s.Barbettes.Add(Cyl(c));
            else if (c.Kind == "uptake" && c.Points is { Count: >= 3 } up)
                s.Uptakes.Add(Bound(new Prism { Id = c.Id, Pts = up, Base = c.Base, Top = c.Top }, up));
            else if (!armoured)
                continue;
            else if (c.Local is { } local)
            {
                var pts = Geometry.RotateTranslate(local.Body, c.RestDeg ?? 0, c.X ?? 0, c.Y ?? 0);
                s.Armoured.Add(Bound(new Prism { Id = c.Id, Pts = pts, Base = c.Base, Top = c.Top }, pts));
            }
            else if (c.Shape == "circle" && c.R != null)
                s.Armoured.Add(Cyl(c));
            else if (c.Points is { Count: >= 3 } pts)
                s.Armoured.Add(Bound(new Prism { Id = c.Id, Pts = pts, Base = c.Base, Top = c.Top }, pts));
            else
                continue;
            zMax = Math.Max(zMax, c.Top);
        }
        s.ZMax = zMax + 0.5;
        return s;
    }

    /// <summary>A random point in a citadel cell, picked by volume, inside the hull.</summary>
    static (Cell, double, double, double) Target(Ship s, Random rng, double[] cum)
    {
        while (true)
        {
            int i = Array.BinarySearch(cum, rng.NextDouble() * cum[^1]);
            var c = s.Citadel[Math.Min(i < 0 ? ~i : i, s.Citadel.Count - 1)];
            for (int k = 0; k < 50; k++)
            {
                double x = c.X0 + (c.X1 - c.X0) * rng.NextDouble(), y = c.Y0 + (c.Y1 - c.Y0) * rng.NextDouble();
                double z = c.Base + (c.Top - c.Base) * rng.NextDouble();
                if (s.InHull(x, y, z))
                    return (c, x, y, z);
            }
        }
    }

    enum Outcome { Armour, Water, Leak }

    /// <summary>Trace one shell back from its target along -dir (u: the unit vector from the target toward the gun).
    /// leak: filled in when nothing stopped it.</summary>
    static Outcome Trace(Ship s, double x0, double y0, double z0, double ux, double uy, double uz, out (string Cause, double[] Hole,
        string HoleFace, double[]? Entry, string EntryFace) leak)
    {
        leak = default;
        // the planes, exactly
        if (uz > 1e-9)
            foreach (var d in s.Decks)
            {
                double t = (d.Z - z0) / uz;
                if (t < 0)
                    continue;
                double x = x0 + ux * t, y = y0 + uy * t;
                if (x >= d.X0 - Eps && x <= d.X1 + Eps && Math.Abs(y) <= s.Hw(x, d.Z) + Eps && !s.Pierced(x, y, d.Z))
                    return Outcome.Armour;
            }
        if (Math.Abs(ux) > 1e-9)
            foreach (var b in s.Bulkheads)
            {
                double t = (b.X - x0) / ux;
                if (t < 0)
                    continue;
                double y = y0 + uy * t, z = z0 + uz * t;
                if (z >= b.Bottom - Eps && z <= b.Top + Eps && z <= s.DeckTop(b.X) + Eps && Math.Abs(y) <= s.Hw(b.X, z) + Eps)
                    return Outcome.Armour;
            }
        // the hull side, the tubes and the solids, by stepping out
        bool hull = true;
        bool[] tube = new bool[s.Barbettes.Count];
        for (int i = 0; i < tube.Length; i++)
            tube[i] = s.Barbettes[i].Inside(x0, y0, z0);
        double[]? entry = null;
        string entryFace = "none";
        string? cause = null;
        for (int n = 1; ; n++)
        {
            double t = n * Step, x = x0 + ux * t, y = y0 + uy * t, z = z0 + uz * t;
            if (Math.Abs(x) > s.XMax || Math.Abs(y) > s.YMax || z > s.ZMax || z < s.Keel - 1)
                break;
            bool h = s.InHull(x, y, z);
            if (!h && z < s.Wl)
                return Outcome.Water;
            if (h != hull)
            {
                double tc = Cross(t - Step, t, hull, tt => s.InHull(x0 + ux * tt, y0 + uy * tt, z0 + uz * tt));
                double qx = x0 + ux * tc, qy = y0 + uy * tc, qz = z0 + uz * tc;
                // at the deck edge it's both: the side first, so a belt reaching the deck counts
                string face = s.Hw(qx, qz) - Math.Abs(qy) < 0.05 ? "side" : Math.Abs(s.DeckTop(qx) - qz) < 0.02 ? "deck" : "end";
                if (face == "side" && s.SideArmour(qx, qz))
                    return Outcome.Armour;
                if (hull)
                    (entry, entryFace) = ([R(qx), R(qy), R(qz)], face);
                hull = h;
            }
            for (int i = 0; i < tube.Length; i++)
            {
                var b = s.Barbettes[i];
                bool inb = b.Inside(x, y, z);
                if (inb == tube[i])
                    continue;
                double tc = Cross(t - Step, t, tube[i], tt => b.Inside(x0 + ux * tt, y0 + uy * tt, z0 + uz * tt));
                double qx = x0 + ux * tc, qy = y0 + uy * tc, qz = z0 + uz * tc;
                if (Math.Abs(Math.Sqrt((qx - b.Cx) * (qx - b.Cx) + (qy - b.Cy) * (qy - b.Cy)) - b.R) < 0.01)   // through the wall (its foot on the deck too), not an open end
                    return Outcome.Armour;
                if (qz >= b.Top - 0.02)
                    cause ??= $"open top of {b.Id}";
                tube[i] = inb;
            }
            foreach (var a in s.Armoured)
                if (a.Inside(x, y, z))
                    return Outcome.Armour;
            foreach (var u in s.Uptakes)
                if (u.Inside(x, y, z))
                    cause ??= $"down {u.Id}";
        }
        // where it got into the citadel: the first point out of it, going back
        double[] hole = [R(x0), R(y0), R(z0)];
        string holeFace = "?";
        for (int n = 1; n < 100000; n++)
        {
            double t = n * Step, x = x0 + ux * t, y = y0 + uy * t, z = z0 + uz * t;
            if (!s.InCitadel(x, y, z))
            {
                hole = [R(x), R(y), R(z)];
                var cs = s.Citadel;
                double cx0 = cs.Min(c => c.X0), cx1 = cs.Max(c => c.X1), ctop = cs.Max(c => c.Top);
                holeFace = x > cx1 ? "forward end" : x < cx0 ? "aft end" : z > ctop || z > s.DeckTop(x) ? "top" : "side";
                if (cause == null && holeFace.EndsWith(" end", StringComparison.Ordinal))
                {
                    double ex = x > cx1 ? cx1 : cx0, ez = Math.Abs(ux) > 1e-9 ? z0 + uz * (ex - x0) / ux : z;   // where it crossed the end
                    var bh = s.Bulkheads.Where(b => Math.Abs(b.X - ex) < 0.5).MinBy(b => b.Bottom);
                    cause = bh == null ? $"{holeFace}: no armoured bulkhead" : ez < bh.Bottom ? $"under {bh.Id}" : ez > bh.Top ? $"over {bh.Id}" : null;
                }
                break;
            }
        }
        leak = (cause ?? $"through the {holeFace}", hole, holeFace, entry, entryFace);
        return Outcome.Leak;
    }

    static double R(double v) => Math.Round(v, 2);

    /// <summary>Bisect the step [a, b] for where inside(t) stops being was.</summary>
    static double Cross(double a, double b, bool was, Func<double, bool> inside)
    {
        for (int k = 0; k < 20; k++)
        {
            double m = (a + b) / 2;
            if (inside(m) == was)
                a = m;
            else
                b = m;
        }
        return (a + b) / 2;
    }

    /// <summary>gratings: the uptakes' openings in the armour decks are closed by armoured gratings, as on real ships;
    /// otherwise a shell can fall down an unarmoured uptake through the decks.</summary>
    public static Result Run(Hitboxes hb, int shells, int seed, bool gratings = false)
    {
        var s = Prepare(hb);
        if (gratings)
            s.Uptakes.Clear();
        var bands = new List<Band> { new(0, 5), new(5, 10), new(10, 20), new(20, 30), new(30, 40), new(40, MaxFall) };
        var leaks = new List<Leak>();
        if (s.Citadel.Count == 0)
            return new Result(0, bands, leaks, s.Roof);
        double[] cum = new double[s.Citadel.Count];
        double acc = 0;
        for (int i = 0; i < cum.Length; i++)
            cum[i] = acc += Math.Max(s.Citadel[i].VolumeM3, 0.01);
        var gate = new object();
        Parallel.For(0, Environment.ProcessorCount, w =>
        {
            var rng = new Random(seed * 7919 + w);
            int share = shells / Environment.ProcessorCount + (w < shells % Environment.ProcessorCount ? 1 : 0);
            var mine = new List<Leak>();
            var counts = new int[bands.Count, 4];
            for (int k = 0; k < share; k++)
            {
                var (cell, x, y, z) = Target(s, rng, cum);
                double brg = 360 * rng.NextDouble(), fall = MaxFall * rng.NextDouble();
                double b = double.DegreesToRadians(brg), f = double.DegreesToRadians(fall);
                // from the target toward the gun: bearing clockwise from ahead (+y is starboard), rising at the fall angle
                var o = Trace(s, x, y, z, Math.Cos(b) * Math.Cos(f), Math.Sin(b) * Math.Cos(f), Math.Sin(f), out var lk);
                int bi = bands.FindIndex(bd => fall < bd.Hi);
                counts[bi, 0]++;
                counts[bi, 1 + (int)o]++;
                if (o == Outcome.Leak)
                    mine.Add(new Leak(lk.Cause, cell.Id, Math.Round(brg, 1), Math.Round(fall, 1), [R(x), R(y), R(z)], lk.Hole, lk.HoleFace, lk.Entry,
                        lk.EntryFace));
            }
            lock (gate)
            {
                for (int i = 0; i < bands.Count; i++)
                {
                    bands[i].Shells += counts[i, 0];
                    bands[i].Armour += counts[i, 1];
                    bands[i].Water += counts[i, 2];
                    bands[i].Leaks += counts[i, 3];
                }
                leaks.AddRange(mine);
            }
        });
        leaks.Sort((p, q) => string.CompareOrdinal(p.Cell, q.Cell) is var c and not 0 ? c : p.FallDeg.CompareTo(q.FallDeg));
        return new Result(shells, bands, leaks, s.Roof, s.Citadel.Count);
    }

    /// <summary>The run as text: the outcome by fall angle, then the leaks grouped by where they got into the citadel.</summary>
    public static string Text(string name, Result r, int show)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{name}: {r.Shells:N0} shells, {r.Leaks.Count:N0} leaks" +
                      (r.Roof is double roof ? $" ({r.Cells} citadel cells under the roof at {roof:F2} m)" : " (no armour deck over a citadel: nothing to test)"));
        sb.AppendLine("  fall°     shells   armour    water    leaks");
        foreach (var b in r.Bands)
            sb.AppendLine($"  {b.Lo,2:F0}-{b.Hi,-3:F0} {b.Shells,9:N0} {b.Armour,8:N0} {b.Water,8:N0} {b.Leaks,8:N0}");
        foreach (var g in r.Leaks.GroupBy(l => l.Cause).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {g.Count(),8:N0}  {g.Key}  (fall {g.Min(l => l.FallDeg):F0}-{g.Max(l => l.FallDeg):F0}°)");
        var groups = r.Leaks.GroupBy(l => (l.Cause, l.HoleFace, X: Math.Round(l.Hole[0] / 2) * 2, Y: Math.Round(Math.Abs(l.Hole[1])),
                Z: Math.Round(l.Hole[2]), l.EntryFace))
            .OrderByDescending(g => g.Count()).Take(show).ToList();
        if (groups.Count > 0)
            sb.AppendLine("  leaks by where they got in (hole x ±1 m, |y|, z ±0.5 m; hull entry face): count, falls, an example");
        foreach (var g in groups)
        {
            var e = g.First();
            string entry = e.Entry is { } en ? $"hull entry ({en[0]}, {en[1]}, {en[2]})" : "no hull entry";
            sb.AppendLine($"  {g.Key.Cause}: {g.Key.HoleFace,-11} x {g.Key.X,6:F0} |y| {g.Key.Y,3:F0} z {g.Key.Z,4:F0}  via {g.Key.EntryFace,-4} {g.Count(),6:N0}  " +
                          $"fall {g.Min(l => l.FallDeg):F0}-{g.Max(l => l.FallDeg):F0}°  e.g. bearing {e.BearingDeg}° fall {e.FallDeg}° " +
                          $"into {e.Cell} ({e.Target[0]}, {e.Target[1]}, {e.Target[2]}) through ({e.Hole[0]}, {e.Hole[1]}, {e.Hole[2]}), {entry}");
        }
        return sb.ToString();
    }
}
