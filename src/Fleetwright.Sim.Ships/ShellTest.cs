using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ships;

/// <summary>shell-test on a ShipClass: an empirical check that the citadel has no holes. Shells are aimed at random
/// points in the citadel's cells between the inner bottom and its roof (the highest armour deck over the citadel that
/// the side armour reaches) from random bearings, at fall angles from direct fire (0°) to plunging (50°), and flown as
/// straight lines over their last stretch. Each is traced through the ship by the one tracer (<see cref="ShipTracer"/>)
/// from outside it to its target: it's stopped by the first armour on the way in, or by water (at once, or once it's
/// deeper than a shell goes under water), and a leak if it reached the citadel through neither.
/// <para>What counts as armour: an armour deck anywhere on the way (unless the shell passes inside a barbette's bore;
/// over an uptake it meets a grating, which stops it only with gratings), an armoured bulkhead, side armour where it
/// crosses the hull's side, a barbette's wall, and any other armoured part entered. Water is anything outside the hull
/// below the waterline. Of what it passed on the way, the last barbette's open top or uptake before the target is what
/// let a leak in.</para>
/// <para>The shots are drawn up front, one stream per shot (<see cref="Draw"/>), so a run doesn't depend on how many
/// threads trace it. The tracer's walk starts at the target and steps back toward the gun at 5 cm, as the first
/// version (Shipgen's, on the hitboxes) did, so a shot's samples are where they always were.</para></summary>
public sealed class ShellTest
{
    public const double MaxFall = 50.0;
    const double Eps = 1e-3;

    readonly ShipClass c;
    readonly ShipSolids g;
    readonly HullField hull;
    readonly bool gratings;
    readonly double underwaterM;
    readonly double keel, far;
    readonly int[] citadel;
    readonly bool[] inCitadel;
    readonly double[] cum;
    readonly double holeX0, holeX1, holeTop;

    [ThreadStatic] static HitPath? scratch;

    /// <summary>The citadel's roof: the box's top (null: no armour deck over the citadel, nothing to test).</summary>
    public double? Roof { get; }
    /// <summary>The cells shot at: the citadel's over the inner bottom and under the roof.</summary>
    public ReadOnlySpan<int> CitadelCells => citadel;

    public ShipClass Class => c;

    /// <summary>gratings: the uptakes' openings in the armour decks are closed by armoured gratings, as on real ships;
    /// otherwise a shell can fall down an unarmoured uptake through the decks. underwaterM: how deep under the waterline a
    /// shell still goes on (0: water stops it at once, as shell-test always had it; research/simulation/damage-research/
    /// 08-gunfire-effects.md §3.8: an underwater hit strikes 1-4 m deep).</summary>
    public ShellTest(ShipClass c, bool gratings = false, double underwaterM = 0)
    {
        (this.c, g, hull, this.gratings, this.underwaterM) = (c, c.Solids, c.Hull, gratings, underwaterM);
        keel = c.Vertical.Keel;
        var b = g.Bounds;
        // far enough back that the shell starts outside the ship's box
        far = 2 * Math.Sqrt((b.X1 - b.X0) * (b.X1 - b.X0) + (b.Y1 - b.Y0) * (b.Y1 - b.Y0) + (b.Top - b.Base) * (b.Top - b.Base)) + 10;

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
            foreach (int s in g.Sides)
                if (c.SidePlates[s].X0 < cx1 && c.SidePlates[s].X1 > cx0)
                    side = Math.Max(side, c.SidePlates[s].Top);
            double? reached = null, lowest = null;
            foreach (int d in g.Decks)
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
        inCitadel = new bool[c.Cells.Length];
        foreach (int i in citadel)
            inCitadel[i] = true;
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

    /// <summary>Trace a shot from outside the ship to its target. leak: filled in when nothing stopped it. path: the
    /// tracer's buffer (null: this thread's own). Allocates nothing once the buffer has grown.</summary>
    public Outcome Trace(in Shot shot, out LeakPath leak, HitPath? path = null)
    {
        var (ux, uy, uz) = shot.Back();
        return Trace(shot.X, shot.Y, shot.Z, ux, uy, uz, out leak, path);
    }

    public Outcome Trace(double x0, double y0, double z0, double ux, double uy, double uz, out LeakPath leak, HitPath? path = null)
    {
        leak = default;
        path ??= scratch ??= new HitPath();
        ShipTracer.Trace(c, new P3(x0 + ux * far, y0 + uy * far, z0 + uz * far), new P3(x0, y0, z0), [], Waterplane.Level(c), path);
        var xs = path.Crossings;

        // from the gun to the target: the first stop, and the last thing before the target that let it in
        var entry = new P3(double.NaN, double.NaN, double.NaN);
        var entryFace = Face.None;
        var cause = Cause.Through;
        int causeIndex = -1;
        foreach (ref readonly var x in xs)
            switch (x.Kind)
            {
                case CrossingKind.Plate when x.Plate is PlateKind.ArmourDeck or PlateKind.ArmourBulkhead or PlateKind.SideArmour
                                             || x.Plate == PlateKind.Grating && gratings:
                    return Outcome.Armour;
                case CrossingKind.WaterIn when underwaterM <= 0:
                    return Outcome.Water;
                case CrossingKind.HullIn when entryFace == Face.None:
                    (entry, entryFace) = (x.Point, x.Plate switch { PlateKind.HullSide => Face.Side, PlateKind.HullDeck => Face.Deck, _ => Face.End });
                    break;
                case CrossingKind.PartIn or CrossingKind.PartOut:
                    ref readonly var s = ref g.Solids[x.Index];
                    if (s.Uptake)
                    {
                        if (!gratings)
                            (cause, causeIndex) = (Cause.Uptake, s.Component);
                    }
                    else if (s.Shape == ShipSolids.Shape.Tube)
                    {
                        if (!s.Armoured)
                            break;
                        if (x.Plate == PlateKind.Barbette)
                            return Outcome.Armour;
                        if (x.Point.Z >= s.Box.Top - 0.02)
                            (cause, causeIndex) = (Cause.OpenTop, s.Component);
                    }
                    else if (s.Armoured)
                        return Outcome.Armour;
                    break;
            }
        // under water deeper than a shell goes: stopped there (the walk's samples, as the water's crossings are)
        if (underwaterM > 0)
            for (int i = 0; i + 1 < xs.Length; i++)
                if (xs[i].Kind == CrossingKind.WaterIn && DeepestUnder(xs[i].T, NextOut(xs, i, path.Length), path) > underwaterM)
                    return Outcome.Water;

        // where it got into the citadel: the first sample out of it going back, after the crossing that leaves it
        var hole = new P3(x0, y0, z0);
        var holeFace = Face.Unknown;
        for (int i = xs.Length - 1; i >= 0; i--)
        {
            ref readonly var x = ref xs[i];
            bool leaves = x.Kind == CrossingKind.HullIn
                          || x.Kind == CrossingKind.CellIn && inCitadel[x.Index] && !(i > 0 && xs[i - 1].T == x.T && Into(xs, i - 1));
            if (!leaves)
                continue;
            double back = path.Length - x.T;
            double t = (Math.Floor(back / ShipTracer.Step) + 1) * ShipTracer.Step;
            double hx = x0 + ux * t, hy = y0 + uy * t, hz = z0 + uz * t;
            hole = new P3(hx, hy, hz);
            holeFace = hx > holeX1 ? Face.ForwardEnd : hx < holeX0 ? Face.AftEnd : hz > holeTop || hz > hull.DeckTop(hx) ? Face.Top : Face.Side;
            if (causeIndex < 0 && holeFace is Face.ForwardEnd or Face.AftEnd)
            {
                double ex = hx > holeX1 ? holeX1 : holeX0, ez = Math.Abs(ux) > 1e-9 ? z0 + uz * (ex - x0) / ux : hz;   // where it crossed the end
                int bh = -1;
                foreach (int k in g.Bulkheads)
                    if (Math.Abs(c.ArmourBulkheads[k].X - ex) < 0.5 && (bh < 0 || c.ArmourBulkheads[k].Bottom < c.ArmourBulkheads[bh].Bottom))
                        bh = k;
                if (bh < 0)
                    cause = Cause.NoBulkhead;
                else if (ez < c.ArmourBulkheads[bh].Bottom)
                    (cause, causeIndex) = (Cause.Under, bh);
                else if (ez > c.ArmourBulkheads[bh].Top)
                    (cause, causeIndex) = (Cause.Over, bh);
            }
            break;
        }
        leak = new LeakPath(cause, causeIndex, hole, holeFace, entry, entryFace);
        return Outcome.Leak;
    }

    /// <summary>Where the water entered at i is left again (the path's end if it isn't).</summary>
    static double NextOut(ReadOnlySpan<Crossing> xs, int i, double end)
    {
        for (int k = i + 1; k < xs.Length; k++)
            if (xs[k].Kind == CrossingKind.WaterOut)
                return xs[k].T;
        return end;
    }

    /// <summary>How far under the waterline the segment goes between t0 and t1 (it's straight: at an end).</summary>
    double DeepestUnder(double t0, double t1, HitPath path)
    {
        double wl = c.Vertical.Waterline;
        return Math.Max(wl - path.At(t0).Z, wl - path.At(t1).Z);
    }

    /// <summary>Do the crossings at one point, from i back, come out of a citadel cell (so the shell stays inside it)?</summary>
    bool Into(ReadOnlySpan<Crossing> xs, int i)
    {
        for (double t = xs[i].T; i >= 0 && xs[i].T == t; i--)
            if (xs[i].Kind == CrossingKind.CellOut)
                return inCitadel[xs[i].Index];
        return false;
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
    /// threads. outcomes: filled with each shot's outcome (for comparing two versions of the tracer shot by
    /// shot).</summary>
    public static Result Run(ShipClass c, int shells, ulong seed, bool gratings = false, Outcome[]? outcomes = null, double underwaterM = 0)
    {
        var st = new ShellTest(c, gratings, underwaterM);
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
            if (outcomes != null)
                outcomes[k] = o;
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
