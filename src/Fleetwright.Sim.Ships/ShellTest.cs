using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ships;

/// <summary>shell-test on a ShipClass: an empirical check that the citadel has no holes. Shells are aimed at random
/// points in the citadel's cells between the inner bottom and its roof (the highest armour deck over the citadel that
/// the side armour reaches) from random bearings, at fall angles from direct fire (0°) to plunging (50°), and flown as
/// straight lines over their last stretch. Each is traced back from its target out of the ship: it's stopped if on the
/// way in it crossed armour or water, and a leak if it reached the citadel through neither.
/// <para>Only the class is read, the way the game reads it: the side armour lies on the hull's side
/// (<see cref="HullField"/>) over its x0..x1 and bottom..top; an armour deck is the plane z over x0..x1 across the hull,
/// with holes where a barbette (inside its wall) or an uptake passes through it, unless the uptakes have gratings; a
/// bulkhead is the plane x over bottom..top across the hull. A barbette is a tube, and the other armoured parts are
/// solids (<see cref="ArmourSolids"/>). Water is anything outside the hull below the waterline.</para>
/// <para>The shots are drawn up front, one stream per shot (<see cref="Draw"/>), so a run doesn't depend on how many
/// threads trace it. A trace steps out along the ray at 5 cm and bisects where it crosses a surface, as the first
/// version (Shipgen's, on the hitboxes) did; step 4.1's hit query replaces the stepping with exact
/// crossings.</para></summary>
public sealed class ShellTest
{
    public const double MaxFall = 50.0;
    const double Step = 0.05, Eps = 1e-3;

    readonly ShipClass c;
    readonly ArmourSolids a;
    readonly HullField hull;
    readonly bool gratings;
    readonly double keel, wl, xMax, yMax, zMax;
    readonly int[] citadel;
    readonly double[] cum;
    readonly double holeX0, holeX1, holeTop;

    /// <summary>The citadel's roof: the box's top (null: no armour deck over the citadel, nothing to test).</summary>
    public double? Roof { get; }
    /// <summary>The cells shot at: the citadel's over the inner bottom and under the roof.</summary>
    public ReadOnlySpan<int> CitadelCells => citadel;

    public ShipClass Class => c;

    /// <summary>gratings: the uptakes' openings in the armour decks are closed by armoured gratings, as on real ships;
    /// otherwise a shell can fall down an unarmoured uptake through the decks.</summary>
    public ShellTest(ShipClass c, bool gratings = false)
    {
        (this.c, a, hull, this.gratings) = (c, c.Armour, c.Hull, gratings);
        (keel, wl) = (c.Vertical.Keel, c.Vertical.Waterline);
        (xMax, yMax, zMax) = (c.Length / 2 + 2, c.Beam / 2 + 2, a.ZMax);

        // the box: the citadel's cells over the inner bottom (the double bottom isn't vital, and a falling shell can't
        // climb out of it) and under its roof, the highest armour deck over the whole citadel that the side armour
        // reaches up to (or the lowest, if none is reached); the tiers above it aren't inside the armour
        var cit = new List<int>();
        for (int i = 0; i < c.Cells.Length; i++)
            if (c.Cells[i].Citadel)
                cit.Add(i);
        citadel = [];
        if (cit.Count > 0)
        {
            double cx0 = cit.Min(i => c.Cells[i].Box.X0), cx1 = cit.Max(i => c.Cells[i].Box.X1);
            double side = double.NegativeInfinity;
            foreach (int s in a.Sides)
                if (c.SidePlates[s].X0 < cx1 && c.SidePlates[s].X1 > cx0)
                    side = Math.Max(side, c.SidePlates[s].Top);
            double? reached = null, lowest = null;
            foreach (int d in a.Decks)
            {
                var dk = c.ArmourDecks[d];
                if (dk.X0 <= cx0 + 0.01 && dk.X1 >= cx1 - 0.01)
                {
                    lowest = lowest is double l ? Math.Min(l, dk.Z) : dk.Z;
                    if (dk.Z <= side + 0.01)
                        reached = reached is double r ? Math.Max(r, dk.Z) : dk.Z;
                }
            }
            Roof = reached ?? lowest;
            if (Roof is double roof)
                citadel = cit.Where(i => c.Cells[i].Box.Top <= roof + 0.01 && c.Tiers[c.Cells[i].Tier].Floor >= 0).ToArray();
        }
        cum = new double[citadel.Length];
        double acc = 0;
        for (int i = 0; i < cum.Length; i++)
            cum[i] = acc += Math.Max(c.Cells[citadel[i]].VolumeM3, 0.01);
        if (citadel.Length > 0)
            (holeX0, holeX1, holeTop) = (citadel.Min(i => c.Cells[i].Box.X0), citadel.Max(i => c.Cells[i].Box.X1), citadel.Max(i => c.Cells[i].Box.Top));
    }

    // ------------------------------------------------------------------ shots

    /// <summary>A shell: its target (a point in a citadel cell), the bearing it comes from (degrees clockwise from
    /// ahead) and its fall below the horizontal.</summary>
    public readonly record struct Shot(int Cell, double X, double Y, double Z, double BearingDeg, double FallDeg)
    {
        /// <summary>The unit vector from the target back toward the gun: along the bearing (+y is starboard), rising
        /// at the fall angle.</summary>
        public (double Ux, double Uy, double Uz) Back()
        {
            double b = double.DegreesToRadians(BearingDeg), f = double.DegreesToRadians(FallDeg);
            return (Math.Cos(b) * Math.Cos(f), Math.Sin(b) * Math.Cos(f), Math.Sin(f));
        }
    }

    /// <summary>A shot from its own stream: a citadel cell picked by volume, a point in it inside the hull, a bearing
    /// and a fall.</summary>
    public Shot Draw(RandomStream rng)
    {
        if (citadel.Length == 0)
            throw new InvalidOperationException("no citadel to shoot at");
        while (true)
        {
            int i = Array.BinarySearch(cum, rng.NextDouble() * cum[^1]);
            int cell = citadel[Math.Min(i < 0 ? ~i : i, citadel.Length - 1)];
            var b = c.Cells[cell].Box;
            for (int k = 0; k < 50; k++)
            {
                double x = b.X0 + (b.X1 - b.X0) * rng.NextDouble(), y = b.Y0 + (b.Y1 - b.Y0) * rng.NextDouble();
                double z = b.Base + (b.Top - b.Base) * rng.NextDouble();
                if (InHull(x, y, z))
                    return new Shot(cell, x, y, z, 360 * rng.NextDouble(), MaxFall * rng.NextDouble());
            }
        }
    }

    // ------------------------------------------------------------------ the trace

    public enum Outcome : byte { Armour, Water, Leak }

    public enum Face : byte { None, Side, Deck, End, ForwardEnd, AftEnd, Top, Unknown }

    public enum Cause : byte
    {
        /// <summary>Through Face, with nothing else to blame.</summary>
        Through,
        /// <summary>Down the open top of a barbette (Index: the component).</summary>
        OpenTop,
        /// <summary>Down an uptake (Index: the component).</summary>
        Uptake,
        /// <summary>In at an end with no armoured bulkhead there.</summary>
        NoBulkhead,
        /// <summary>Under or over an armoured bulkhead (Index: ArmourBulkheads).</summary>
        Under,
        Over,
    }

    /// <summary>Where a leak got in: what let it in, the first point out of the citadel going back (Hole, on its
    /// HoleFace) and where it last entered the hull (Entry, on EntryFace; Face.None: never).</summary>
    public readonly record struct LeakPath(Cause Cause, int Index, P3 Hole, Face HoleFace, P3 Entry, Face EntryFace);

    bool InHull(double x, double y, double z) => z >= keel && z <= hull.DeckTop(x) && Math.Abs(y) < hull.HalfWidth(x, z);

    bool InCitadel(double x, double y, double z)
    {
        if (!InHull(x, y, z))
            return false;
        foreach (int i in citadel)
        {
            var b = c.Cells[i].Box;
            if (x >= b.X0 - Eps && x <= b.X1 + Eps && y >= b.Y0 - Eps && y <= b.Y1 + Eps && z >= b.Base - Eps && z <= b.Top + Eps)
                return true;
        }
        return false;
    }

    ReadOnlySpan<ArmourSolids.Solid> Uptakes => gratings ? [] : a.Uptakes;

    /// <summary>Is the deck at z pierced at (x, y): inside a barbette that stands on or through it, or an uptake.</summary>
    bool Pierced(double x, double y, double z)
    {
        foreach (ref readonly var b in a.BarbetteTubes)
            if (b.Box.Base <= z + 0.01 && b.Box.Top > z && ArmourSolids.InBore(b, x, y))
                return true;
        foreach (ref readonly var u in Uptakes)
            if (u.Box.Base < z + 0.01 && u.Box.Top > z - 0.01 && a.InPlan(u, x, y))
                return true;
        return false;
    }

    bool SideArmour(double x, double z)
    {
        foreach (int i in a.Sides)
        {
            ref readonly var s = ref c.SidePlates[i];
            if (x >= s.X0 - Eps && x <= s.X1 + Eps && z >= s.Bottom - Eps && z <= s.Top + Eps)
                return true;
        }
        return false;
    }

    /// <summary>Trace a shot back from its target out of the ship. leak: filled in when nothing stopped it. Allocates
    /// nothing.</summary>
    public Outcome Trace(in Shot shot, out LeakPath leak)
    {
        var (ux, uy, uz) = shot.Back();
        return Trace(shot.X, shot.Y, shot.Z, ux, uy, uz, out leak);
    }

    public Outcome Trace(double x0, double y0, double z0, double ux, double uy, double uz, out LeakPath leak)
    {
        leak = default;
        // the planes, exactly
        if (uz > 1e-9)
            foreach (int i in a.Decks)
            {
                ref readonly var d = ref c.ArmourDecks[i];
                double t = (d.Z - z0) / uz;
                if (t < 0)
                    continue;
                double x = x0 + ux * t, y = y0 + uy * t;
                if (x >= d.X0 - Eps && x <= d.X1 + Eps && Math.Abs(y) <= hull.HalfWidth(x, d.Z) + Eps && !Pierced(x, y, d.Z))
                    return Outcome.Armour;
            }
        if (Math.Abs(ux) > 1e-9)
            foreach (int i in a.Bulkheads)
            {
                ref readonly var b = ref c.ArmourBulkheads[i];
                double t = (b.X - x0) / ux;
                if (t < 0)
                    continue;
                double y = y0 + uy * t, z = z0 + uz * t, xh = b.X - Math.Sign(b.X) * 0.01;   // a plate on an end face: the hull just inside it
                if (z >= b.Bottom - Eps && z <= b.Top + Eps && z <= hull.DeckTop(xh) + Eps && Math.Abs(y) <= hull.HalfWidth(xh, z) + Eps)
                    return Outcome.Armour;
            }
        // the hull side, the tubes and the solids, by stepping out
        var tubes = a.BarbetteTubes;
        Span<bool> inTube = tubes.Length <= 256 ? stackalloc bool[tubes.Length] : new bool[tubes.Length];
        for (int i = 0; i < tubes.Length; i++)
            inTube[i] = a.Inside(tubes[i], x0, y0, z0);
        bool inHull = true;
        var entry = new P3(double.NaN, double.NaN, double.NaN);
        var entryFace = Face.None;
        var cause = Cause.Through;
        int causeIndex = -1;
        for (int n = 1; ; n++)
        {
            double t = n * Step, x = x0 + ux * t, y = y0 + uy * t, z = z0 + uz * t;
            if (Math.Abs(x) > xMax || Math.Abs(y) > yMax || z > zMax || z < keel - 1)
                break;
            bool h = InHull(x, y, z);
            if (!h && z < wl)
                return Outcome.Water;
            if (h != inHull)
            {
                double tc = CrossHull(t - Step, t, inHull, x0, y0, z0, ux, uy, uz);
                double qx = x0 + ux * tc, qy = y0 + uy * tc, qz = z0 + uz * tc;
                // at the deck edge it's both: the side first, so a belt reaching the deck counts
                var face = hull.HalfWidth(qx, qz) - Math.Abs(qy) < 0.05 ? Face.Side : Math.Abs(hull.DeckTop(qx) - qz) < 0.02 ? Face.Deck : Face.End;
                if (face == Face.Side && SideArmour(qx, qz))
                    return Outcome.Armour;
                if (inHull)
                    (entry, entryFace) = (new P3(qx, qy, qz), face);
                inHull = h;
            }
            for (int i = 0; i < tubes.Length; i++)
            {
                ref readonly var b = ref tubes[i];
                bool inb = a.Inside(b, x, y, z);
                if (inb == inTube[i])
                    continue;
                double tc = CrossTube(t - Step, t, inTube[i], b, x0, y0, z0, ux, uy, uz);
                double qx = x0 + ux * tc, qy = y0 + uy * tc, qz = z0 + uz * tc;
                if (Math.Abs(Math.Sqrt((qx - b.Cx) * (qx - b.Cx) + (qy - b.Cy) * (qy - b.Cy)) - b.R) < 0.01)   // through the wall (its foot on the deck too), not an open end
                    return Outcome.Armour;
                if (qz >= b.Box.Top - 0.02 && causeIndex < 0)
                    (cause, causeIndex) = (Cause.OpenTop, b.Component);
                inTube[i] = inb;
            }
            foreach (ref readonly var s in a.Armoured)
                if (a.Inside(s, x, y, z))
                    return Outcome.Armour;
            foreach (ref readonly var u in Uptakes)
                if (a.Inside(u, x, y, z) && causeIndex < 0)
                    (cause, causeIndex) = (Cause.Uptake, u.Component);
        }
        // where it got into the citadel: the first point out of it, going back
        var hole = new P3(x0, y0, z0);
        var holeFace = Face.Unknown;
        for (int n = 1; n < 100000; n++)
        {
            double t = n * Step, x = x0 + ux * t, y = y0 + uy * t, z = z0 + uz * t;
            if (!InCitadel(x, y, z))
            {
                hole = new P3(x, y, z);
                holeFace = x > holeX1 ? Face.ForwardEnd : x < holeX0 ? Face.AftEnd : z > holeTop || z > hull.DeckTop(x) ? Face.Top : Face.Side;
                if (causeIndex < 0 && holeFace is Face.ForwardEnd or Face.AftEnd)
                {
                    double ex = x > holeX1 ? holeX1 : holeX0, ez = Math.Abs(ux) > 1e-9 ? z0 + uz * (ex - x0) / ux : z;   // where it crossed the end
                    int bh = -1;
                    foreach (int i in a.Bulkheads)
                        if (Math.Abs(c.ArmourBulkheads[i].X - ex) < 0.5 && (bh < 0 || c.ArmourBulkheads[i].Bottom < c.ArmourBulkheads[bh].Bottom))
                            bh = i;
                    if (bh < 0)
                        cause = Cause.NoBulkhead;
                    else if (ez < c.ArmourBulkheads[bh].Bottom)
                        (cause, causeIndex) = (Cause.Under, bh);
                    else if (ez > c.ArmourBulkheads[bh].Top)
                        (cause, causeIndex) = (Cause.Over, bh);
                }
                break;
            }
        }
        leak = new LeakPath(cause, causeIndex, hole, holeFace, entry, entryFace);
        return Outcome.Leak;
    }

    /// <summary>Bisect the step [lo, hi] for where the ray leaves (or enters) the hull.</summary>
    double CrossHull(double lo, double hi, bool was, double x0, double y0, double z0, double ux, double uy, double uz)
    {
        for (int k = 0; k < 20; k++)
        {
            double m = (lo + hi) / 2;
            if (InHull(x0 + ux * m, y0 + uy * m, z0 + uz * m) == was)
                lo = m;
            else
                hi = m;
        }
        return (lo + hi) / 2;
    }

    double CrossTube(double lo, double hi, bool was, in ArmourSolids.Solid b, double x0, double y0, double z0, double ux, double uy, double uz)
    {
        for (int k = 0; k < 20; k++)
        {
            double m = (lo + hi) / 2;
            if (a.Inside(b, x0 + ux * m, y0 + uy * m, z0 + uz * m) == was)
                lo = m;
            else
                hi = m;
        }
        return (lo + hi) / 2;
    }

    // ------------------------------------------------------------------ text

    public static string FaceName(Face f) => f switch
    {
        Face.None => "none",
        Face.Side => "side",
        Face.Deck => "deck",
        Face.End => "end",
        Face.ForwardEnd => "forward end",
        Face.AftEnd => "aft end",
        Face.Top => "top",
        _ => "?",
    };

    /// <summary>What let a leak in, in words: "open top of A barbette", "down Funnel 1 uptakes", "under Forward
    /// bulkhead", else the citadel face it came through.</summary>
    public string CauseText(in LeakPath p) => p.Cause switch
    {
        Cause.OpenTop => $"open top of {c.ComponentId(p.Index)}",
        Cause.Uptake => $"down {c.ComponentId(p.Index)}",
        Cause.NoBulkhead => $"{FaceName(p.HoleFace)}: no armoured bulkhead",
        Cause.Under => $"under {c.ArmourBulkheadId(p.Index)}",
        Cause.Over => $"over {c.ArmourBulkheadId(p.Index)}",
        _ => $"through the {FaceName(p.HoleFace)}",
    };

    // ------------------------------------------------------------------ a run

    /// <summary>A shell that got in, for the report.</summary>
    public sealed record Leak(int Shot, string Cause, string Cell, double BearingDeg, double FallDeg, double[] Target, double[] Hole, string HoleFace,
        double[]? Entry, string EntryFace);

    public sealed record Band(double Lo, double Hi)
    {
        public int Shells, Armour, Water, Leaks;
    }

    /// <summary>Roof: the box's roof (null: no armour deck over a citadel, nothing tested); Cells: the cells shot at.</summary>
    public sealed record Result(int Shells, List<Band> Bands, List<Leak> Leaks, double? Roof = null, int Cells = 0);

    /// <summary>Shot k is drawn from the run's stream derived by k, so the result is the same on any number of
    /// threads.</summary>
    public static Result Run(ShipClass c, int shells, ulong seed, bool gratings = false)
    {
        var st = new ShellTest(c, gratings);
        var bands = new List<Band> { new(0, 5), new(5, 10), new(10, 20), new(20, 30), new(30, 40), new(40, MaxFall) };
        var leaks = new List<Leak>();
        if (st.citadel.Length == 0)
            return new Result(0, bands, leaks, st.Roof);
        var stream = RandomStream.FromSeed(seed);
        var gate = new object();
        Parallel.For(0, shells, () => (Counts: new int[bands.Count, 4], Leaks: new List<Leak>()), (k, _, local) =>
        {
            var shot = st.Draw(stream.Derive((ulong)k));
            var o = st.Trace(shot, out var lk);
            int bi = bands.FindIndex(bd => shot.FallDeg < bd.Hi);
            local.Counts[bi, 0]++;
            local.Counts[bi, 1 + (int)o]++;
            if (o == Outcome.Leak)
                local.Leaks.Add(new Leak(k, st.CauseText(lk), c.CellId(shot.Cell), Math.Round(shot.BearingDeg, 1), Math.Round(shot.FallDeg, 1),
                    [R(shot.X), R(shot.Y), R(shot.Z)], [R(lk.Hole.X), R(lk.Hole.Y), R(lk.Hole.Z)], FaceName(lk.HoleFace),
                    lk.EntryFace == Face.None ? null : [R(lk.Entry.X), R(lk.Entry.Y), R(lk.Entry.Z)], FaceName(lk.EntryFace)));
            return local;
        }, local =>
        {
            lock (gate)
            {
                for (int i = 0; i < bands.Count; i++)
                {
                    bands[i].Shells += local.Counts[i, 0];
                    bands[i].Armour += local.Counts[i, 1];
                    bands[i].Water += local.Counts[i, 2];
                    bands[i].Leaks += local.Counts[i, 3];
                }
                leaks.AddRange(local.Leaks);
            }
        });
        leaks.Sort((p, q) => string.CompareOrdinal(p.Cell, q.Cell) is var o and not 0 ? o : p.FallDeg.CompareTo(q.FallDeg) is var f and not 0 ? f : p.Shot.CompareTo(q.Shot));
        return new Result(shells, bands, leaks, st.Roof, st.citadel.Length);
    }

    static double R(double v) => Math.Round(v, 2);

    /// <summary>The run as text: the outcome by fall angle, then the leaks grouped by where they got into the citadel.</summary>
    public static string Text(string name, Result r, int show)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{name}: {r.Shells:N0} shells, {r.Leaks.Count:N0} leaks" +
                      (r.Roof is double roof ? $" ({r.Cells} citadel cells under the roof at {roof:F2} m)" : " (no armour deck over a citadel: nothing to test)"));
        sb.AppendLine("  fall°     shells   armour    water    leaks");
        foreach (var b in r.Bands)
            sb.AppendLine($"  {b.Lo,2:F0}-{b.Hi,-3:F0} {b.Shells,9:N0} {b.Armour,8:N0} {b.Water,8:N0} {b.Leaks,8:N0}");
        foreach (var g in r.Leaks.GroupBy(l => l.Cause).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
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
