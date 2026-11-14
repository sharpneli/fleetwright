using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>The spaces an immune zone is of (docs/designer-ui/immune-zone.md decision 2): the magazines, or the machinery
/// (boiler and engine rooms), each a zone from its own shells.</summary>
public enum Vitals : byte
{
    Magazines,
    Machinery,
}

/// <summary>One cell of an immune zone, a range and a target angle: the shells aimed at the vitals (lines that, unstopped,
/// would enter one) and what came of them: in whole through the side or through the deck (by the heaviest plate each
/// beat), or in only as pieces.</summary>
public struct ZoneCell
{
    public int Shots, Side, Deck, Broken;
    /// <summary>The shells that met her main belt (her thickest plate lying as the side) and her main deck (her thickest
    /// lying as a deck), and of them those that came whole through it, whether they then reached the vitals or not: the
    /// armour beaten, the classic (USN) edges' measure.</summary>
    public int BeltMet, BeltBeaten, DeckMet, DeckBeaten;
    /// <summary>Lines drawn, the misses of the vitals among them; and the shells for which no line reached them (the
    /// vitals hidden from there: below the sea's reach for a flat fall, or behind one another).</summary>
    public int Drawn, Unseen;
    /// <summary>The lines traced through her (the rest were sure misses), for the cost.</summary>
    public int Traced;

    /// <summary>Whether no shell from here could reach the vitals at all: out of sight, as good as immune.</summary>
    public readonly bool Hidden => Shots == 0 && Unseen > 0;

    public readonly double SideShare => Shots > 0 ? (double)Side / Shots : 0;
    public readonly double DeckShare => Shots > 0 ? (double)Deck / Shots : 0;
    public readonly double BrokenShare => Shots > 0 ? (double)Broken / Shots : 0;
    public readonly double BeltBeatenShare => BeltMet > 0 ? (double)BeltBeaten / BeltMet : 0;
    public readonly double DeckBeatenShare => DeckMet > 0 ? (double)DeckBeaten / DeckMet : 0;
}

/// <summary>A plate that let shells into the vitals: its kind and index (as the path's crossings name it), and its
/// thickness where they met it.</summary>
public readonly record struct ZonePlate(PlateKind Kind, int Index, double Mm);

/// <summary>An immune zone (docs/designer-ui/immune-zone.md): a shell fired at a class at each range out to its gun's
/// reach and each target angle, aimed at her magazines or her machinery, each shell walked through her by
/// <see cref="Terminal.Resolve"/> on an intact ship, upright on her waterline, with the walk's own rolls. A shell
/// reached them if it entered one of their spaces whole (its filler in it), or burst inside one; broken up on the way it
/// counts apart. Each is put down to the side or the deck by the heaviest plate it beat on the way (a plate within 45° of
/// level is the deck). The inner edge is where half the shells at the vitals get in through the side; the outer edge,
/// where half get in through the deck (decision 3).
/// <para>Each cell draws its shells from a stream of its own and each shell from its own substream, so the zone is the
/// same on any number of threads, and filled in passes (<see cref="Fill"/>) it's the same as filled at once.</para></summary>
public sealed class ImmuneZone
{
    /// <summary>The target angles, degrees off her bow the shells come from (one side: she's near enough symmetric).</summary>
    public static readonly double[] Angles = [0, 15, 30, 45, 60, 75, 90, 105, 120, 135, 150, 165, 180];

    /// <summary>The ranges' step, m (the first range is one step).</summary>
    public const double RangeStepM = 1000;

    /// <summary>The muzzle's height over the sea for the flight, m (GivenHits's).</summary>
    public const double MuzzleM = 10;

    /// <summary>Half the shells at the vitals getting in: an edge (decision 3).</summary>
    public const double Edge = 0.5;

    /// <summary>The most lines drawn for a shell before it's given up as unseen (the vitals hidden from it behind the sea
    /// or another space of them).</summary>
    const int DrawsPerShot = 24;

    /// <summary>A cell whose first this many shells all went unseen is hidden from there and drawn no more.</summary>
    const int HiddenAfter = 8;

    public ShipClass Class { get; }
    public ShellTypeRow Shell { get; }
    public double ReachM { get; }
    /// <summary>Each range's, m, and the shell's fall there (degrees under the horizontal) and striking speed (m/s).</summary>
    public double[] RangesM { get; }
    public (double FallDeg, double StrikeMs)[] Falls { get; }
    /// <summary>The shells a cell has been filled to.</summary>
    public int ShotsPerCell { get; private set; }

    /// <summary>For benches: each shell walked (the cell's index, its path, the walk's result, where it entered the
    /// vitals or NaN). Called on the filling threads.</summary>
    public Action<int, HitPath, TerminalResult, double>? Each { get; set; }

    readonly SimSettings settings;
    readonly ulong seed;
    readonly bool[][] vital;
    readonly bool[] has;
    readonly ZoneCell[] cells;
    readonly Dictionary<ZonePlate, int>[] sidePlates, deckPlates;
    readonly double[] bearings;
    readonly double beltMm, deckMm;

    /// <summary>A zone of a shell on a class, its gun's elevation up to maxElevationDeg; empty until filled.</summary>
    public ImmuneZone(ShipClass c, in ShellTypeRow shell, double maxElevationDeg, SimSettings s, ulong seed)
    {
        (Class, Shell, settings, this.seed) = (c, shell, s, seed);
        var table = new RangeTable(BallisticKey.Of(shell, maxElevationDeg), MuzzleM);
        ReachM = table.ReachM;
        int n = Math.Max(0, (int)Math.Floor(ReachM / RangeStepM));
        RangesM = new double[n];
        Falls = new (double, double)[n];
        for (int i = 0; i < n; i++)
        {
            RangesM[i] = (i + 1) * RangeStepM;
            Falls[i] = table.At(RangesM[i]);
        }
        // the vital spaces' cells: a cell is of them if any room it holds is
        vital = new bool[2][];
        has = new bool[2];
        for (int v = 0; v < 2; v++)
        {
            vital[v] = new bool[c.Cells.Length];
            for (int i = 0; i < c.Cells.Length; i++)
            {
                ref readonly var cell = ref c.Cells[i];
                bool of = Of(c, cell.Room, (Vitals)v);
                foreach (int r in c.Links(cell.Also))
                    of |= Of(c, r, (Vitals)v);
                vital[v][i] = of;
                has[v] |= of;
            }
        }
        cells = new ZoneCell[2 * n * Angles.Length];
        sidePlates = new Dictionary<ZonePlate, int>[cells.Length];
        deckPlates = new Dictionary<ZonePlate, int>[cells.Length];
        // her main belt and main deck: the thickest plates lying as the side and as a deck
        foreach (ref readonly var p in c.SidePlates)
            beltMm = Math.Max(beltMm, p.ThicknessMm);
        foreach (ref readonly var d in c.ArmourDecks)
            deckMm = Math.Max(deckMm, d.ThicknessMm);
        foreach (ref readonly var p in c.Inclined)
        {
            double dz = p.Z1 - p.Z0, du = p.U1 - p.U0;
            if (Math.Abs(dz) > Math.Abs(du))
                beltMm = Math.Max(beltMm, p.ThicknessMm);
            else
                deckMm = Math.Max(deckMm, p.ThicknessMm);
        }
        bearings = new double[c.Mounts.Length];
        for (int i = 0; i < bearings.Length; i++)
            bearings[i] = c.Mounts[i].RestDeg;
    }

    static bool Of(ShipClass c, int room, Vitals v) => room >= 0 && (v == Vitals.Magazines
        ? c.Rooms[room].Kind == RoomKind.Magazine
        : c.Rooms[room].Kind is RoomKind.BoilerRoom or RoomKind.EngineRoom);

    /// <summary>Whether she has any of these spaces.</summary>
    public bool Has(Vitals v) => has[(int)v];

    public ref readonly ZoneCell this[Vitals v, int range, int angle] => ref cells[Index(v, range, angle)];

    public int Index(Vitals v, int range, int angle) => ((int)v * RangesM.Length + range) * Angles.Length + angle;

    /// <summary>The plate that let the most shells of a cell in through the side (or the deck); null for none.</summary>
    public ZonePlate? Most(Vitals v, int range, int angle, bool deck)
    {
        var d = (deck ? deckPlates : sidePlates)[Index(v, range, angle)];
        if (d == null || d.Count == 0)
            return null;
        var best = d.MaxBy(kv => (kv.Value, -(int)kv.Key.Kind, -kv.Key.Index));
        return best.Key;
    }

    /// <summary>Fill every cell up to this many shells at the vitals, on up to threads threads; stops early (leaving the
    /// cells as they are, each whole) if cancelled.</summary>
    public void Fill(int shotsPerCell, int threads = 1, CancellationToken cancel = default)
    {
        if (shotsPerCell <= ShotsPerCell)
            return;
        int from = ShotsPerCell;
        var opts = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads), CancellationToken = cancel };
        try
        {
            Parallel.For(0, cells.Length, opts, () => new Walker(Class), (i, _, w) =>
            {
                FillCell(i, from, shotsPerCell, w);
                return w;
            }, _ => { });
            ShotsPerCell = shotsPerCell;
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>A thread's own tracer path, bender and intact condition.</summary>
    sealed class Walker
    {
        public readonly HitPath Path = new(), Probe = new();
        public readonly PathBender Bender = new();
        public readonly ShipCondition Intact = new();
        public readonly List<(int Cell, double U0, double U1, double V0, double V1, double Upto)> Rects = [];

        public Walker(ShipClass c)
        {
            Intact.Reset(c);
            Bender.Reserve(c.Solids.Solids.Length);
        }
    }

    void FillCell(int i, int from, int to, Walker w)
    {
        int v = i / (RangesM.Length * Angles.Length), range = i / Angles.Length % RangesM.Length, angle = i % Angles.Length;
        ref var cell = ref cells[i];
        if (!has[v] || double.IsNaN(Falls[range].FallDeg))
            return;
        var c = Class;
        var (fall, strike) = Falls[range];
        var dir = HitQuery.Flight(Angles[angle], fall);
        // two axes across the line of flight, and each vital space's outline on them (its box's, as a rectangle): a line is
        // drawn by picking a space by the area of its rectangle, then a point within it, kept if it meets the space's box
        // and then one time in as many of the spaces' boxes as it meets, so the lines fall evenly over the vitals as the
        // shell sees them
        var u = Normalize(Cross(dir, new P3(0, 0, 1)));
        if (Dot(u, u) < 0.5)
            u = new P3(0, 1, 0);
        var vv = Cross(u, dir);
        var rects = w.Rects;
        rects.Clear();
        double total = 0;
        for (int k = 0; k < c.Cells.Length; k++)
            if (vital[v][k])
            {
                var b = c.Cells[k].Box;
                double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = new P3((corner & 1) == 0 ? b.X0 : b.X1, (corner & 2) == 0 ? b.Y0 : b.Y1, (corner & 4) == 0 ? b.Base : b.Top);
                    double a = Dot(p, u), e = Dot(p, vv);
                    (u0, u1, v0, v1) = (Math.Min(u0, a), Math.Max(u1, a), Math.Min(v0, e), Math.Max(v1, e));
                }
                total += (u1 - u0) * (v1 - v0);
                rects.Add((k, u0, u1, v0, v1, total));
            }
        if (total <= 0)
            return;
        double far = 2 * c.Solids.Radius + 50, reach = WaterRun.ReachM(Shell, strike, settings);
        var bounds = c.Solids.Bounds;
        var water = Waterplane.Level(c);
        var stream = RandomStream.Named(seed, "immune zone").Derive((ulong)i);
        var lotStream = RandomStream.Named(seed, "immune zone lots").Derive((ulong)i);
        for (int shot = from; shot < to; shot++)
        {
            // a cell none of whose first shells could reach the vitals is hidden from there: they're out of its sight
            if (shot >= HiddenAfter && cell.Shots == 0)
                return;
            var rng = stream.Derive((ulong)shot);
            // a line through the vitals' outline that, unstopped, enters one of them, and its first meeting with her
            int first = -1;
            double run = 0;
            for (int draw = 0; draw < DrawsPerShot; draw++)
            {
                cell.Drawn++;
                double pick = rng.NextDouble() * total;
                int lo = 0, hi = rects.Count - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (rects[mid].Upto < pick)
                        lo = mid + 1;
                    else
                        hi = mid;
                }
                var q = rects[lo];
                double a = rng.NextDouble(q.U0, q.U1), e = rng.NextDouble(q.V0, q.V1);
                var at = new P3(u.X * a + vv.X * e, u.Y * a + vv.Y * e, u.Z * a + vv.Z * e);
                var from0 = Add(at, dir, -far);
                // sure misses before the trace: the line misses the space's box (it can't enter the space), or meets the
                // sea farther out from her than the shell runs under it
                if (!Slab(from0, dir, c.Cells[q.Cell].Box, out _, out _) || !Slab(from0, dir, bounds, out double tb, out double te))
                    continue;
                // a line through m of the spaces' boxes could have come from any of them: kept one time in m, so the lines
                // fall evenly over them all
                int m = 0;
                foreach (var o in rects)
                    m += Slab(from0, dir, c.Cells[o.Cell].Box, out _, out _) ? 1 : 0;
                if (m > 1 && rng.NextDouble() * m >= 1)
                    continue;
                double tw = dir.Z < 0 ? (from0.Z - water.Z) / -dir.Z : double.PositiveInfinity;
                if (tb - tw > reach)
                    continue;
                cell.Traced++;
                // the line from just before it meets the sea or her bounds to just past them
                ShipTracer.Trace(c, Add(from0, dir, Math.Max(0, Math.Min(tb, tw) - 2)), Add(from0, dir, te + 2), bearings, water, w.Probe);
                if (VitalAt(w.Probe, v) < 0)
                    continue;
                first = HitQuery.FirstIn(w.Probe);
                if (first < 0)
                    continue;
                run = WaterRun.RunM(w.Probe, first);
                if (run > reach)
                {
                    first = -1;   // the sea took it
                    continue;
                }
                break;
            }
            if (first < 0)
            {
                cell.Unseen++;
                continue;
            }
            var point = w.Probe.Crossings[first].Point;
            var hit = new Hit
            {
                Point = point, Direction = dir, SpeedMs = strike, WaterRunM = run, Source = HitSource.Shell, Shell = Shell, ShellType = -1,
            };
            // the hit's own path, as the damage system traces it: from just outside the point along the flight; a line that
            // met nothing before her (no sea) is that path already
            var path = w.Probe;
            if (first > 0)
            {
                Slab(point, dir, bounds, out _, out double exit);
                ShipTracer.Trace(c, new P3(point.X - dir.X, point.Y - dir.Y, point.Z - dir.Z), Add(point, dir, exit + 2), bearings, water, w.Path);
                path = w.Path;
            }
            w.Bender.Set(bearings, bearings.Length, water);
            var r = rng.Derive(1);
            var t = Terminal.Resolve(c, hit, path, w.Bender, lotStream.Derive((ulong)shot), settings, ref r, w.Intact);
            cell.Shots++;
            var xs = path.Crossings;
            int into = VitalAt(path, v);
            double tin = into >= 0 ? xs[into].T : double.NaN;
            Each?.Invoke(i, path, t, tin);
            // whole up to: where it broke, burst or stopped
            double whole = t.Outcome is Outcome.BrokeUp or Outcome.Holed && t.Plate >= 0 ? xs[t.Plate].T
                : t.Bursts && !double.IsNaN(t.BurstT) ? Math.Min(t.BurstT, t.StopT) : t.StopT;
            // the heaviest plate on its way in (to the vitals, or as far as it went)
            int heavy = -1;
            for (int k = 0; k < (into >= 0 ? into : xs.Length) && xs[k].T < t.StopT + HitPath.SameM; k++)
                if (xs[k].Plate is not (PlateKind.None or PlateKind.Contents or PlateKind.PartContents) && xs[k].Mm > 0 &&
                    (heavy < 0 || xs[k].Mm > xs[heavy].Mm))
                    heavy = k;
            bool deck = heavy >= 0 && Lies(xs[heavy].Normal);
            // the main belt and the main deck met on the way, and whether it came through each whole: the armour beaten,
            // the classic edges' measure, whether or not the shell then reached the vitals
            bool metBelt = false, beatBelt = false, metDeck = false, beatDeck = false;
            for (int k = 0; k < xs.Length && xs[k].T < t.StopT + HitPath.SameM; k++)
                if (xs[k].Plate is PlateKind.SideArmour or PlateKind.Inclined or PlateKind.ArmourDeck)
                {
                    bool flat = Lies(xs[k].Normal);
                    if (xs[k].Mm < (flat ? deckMm : beltMm) * 0.9)
                        continue;
                    // through it whole: past it unbroken, not glanced off it
                    bool through = xs[k].T < whole && t.Glance != k;
                    if (flat && !metDeck)
                        (metDeck, beatDeck) = (true, through);
                    else if (!flat && !metBelt)
                        (metBelt, beatBelt) = (true, through);
                }
            (cell.BeltMet, cell.BeltBeaten) = (cell.BeltMet + (metBelt ? 1 : 0), cell.BeltBeaten + (beatBelt ? 1 : 0));
            (cell.DeckMet, cell.DeckBeaten) = (cell.DeckMet + (metDeck ? 1 : 0), cell.DeckBeaten + (beatDeck ? 1 : 0));
            if (into < 0 || tin >= t.StopT)
                continue;
            if (tin >= whole)
            {
                cell.Broken++;
                continue;
            }
            if (deck)
                cell.Deck++;
            else
                cell.Side++;
            if (heavy >= 0)
            {
                var plates = deck ? deckPlates : sidePlates;
                var key = new ZonePlate(xs[heavy].Plate, xs[heavy].Index, Math.Round(xs[heavy].Mm));
                var d = plates[i] ??= [];
                d[key] = d.GetValueOrDefault(key) + 1;
            }
        }
    }

    /// <summary>The crossing at which a path first enters a vital space (the plates at that point come before it); -1 for
    /// never.</summary>
    int VitalAt(HitPath path, int v)
    {
        var xs = path.Crossings;
        for (int k = 0; k < xs.Length; k++)
            if (xs[k].Kind == CrossingKind.CellIn && xs[k].Index >= 0 && vital[v][xs[k].Index])
                return k;
        return -1;
    }

    /// <summary>The zone at an angle: the inner edge (out to which half the shells at the vitals get in through the side;
    /// 0 if they never do) and the outer (from which half get in through the deck; the reach if they never do), m, each
    /// between the ranges the share crosses half at. Immune between them if the inner is the nearer. With beaten, the
    /// classic edges instead: the armour beaten by a whole shell, whether it then reached the vitals or not.</summary>
    public (double InnerM, double OuterM) Edges(Vitals v, int angle, bool beaten = false)
    {
        int n = RangesM.Length;
        double inner = 0, outer = ReachM;
        double SideOf(int k) => beaten ? this[v, k, angle].BeltBeatenShare : this[v, k, angle].SideShare;
        double DeckOf(int k) => beaten ? this[v, k, angle].DeckBeatenShare : this[v, k, angle].DeckShare;
        for (int k = n - 1; k >= 0; k--)
            if (this[v, k, angle].Shots > 0 && SideOf(k) >= Edge)
            {
                inner = k + 1 < n && this[v, k + 1, angle].Shots > 0 ? Cross(k, k + 1, SideOf(k), SideOf(k + 1)) : RangesM[k];
                break;
            }
        for (int k = 0; k < n; k++)
            if (this[v, k, angle].Shots > 0 && DeckOf(k) >= Edge)
            {
                outer = k > 0 && this[v, k - 1, angle].Shots > 0 ? Cross(k - 1, k, DeckOf(k - 1), DeckOf(k)) : RangesM[k];
                break;
            }
        return (inner, outer);

        double Cross(int a, int b, double sa, double sb) => sa == sb ? RangesM[a] : RangesM[a] + (RangesM[b] - RangesM[a]) * (Edge - sa) / (sb - sa);
    }

    /// <summary>Where a line from o along d enters a box and leaves it (t along it), if it meets it at all.</summary>
    static bool Slab(P3 o, P3 d, in Box b, out double t0, out double t1)
    {
        double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
        Axis(o.X, d.X, b.X0, b.X1);
        Axis(o.Y, d.Y, b.Y0, b.Y1);
        Axis(o.Z, d.Z, b.Base, b.Top);
        (t0, t1) = (lo, hi);
        return lo <= hi && hi >= 0;

        void Axis(double p, double dd, double a, double bb)
        {
            if (Math.Abs(dd) < 1e-12)
            {
                if (p < a || p > bb)
                    (lo, hi) = (1, 0);
                return;
            }
            double ta = (a - p) / dd, tc = (bb - p) / dd;
            if (ta > tc)
                (ta, tc) = (tc, ta);
            (lo, hi) = (Math.Max(lo, ta), Math.Min(hi, tc));
        }
    }

    /// <summary>Whether a plate with this normal lies as a deck (within 45° of level) rather than as the side.</summary>
    static bool Lies(P3 normal) => Math.Abs(normal.Z) > Math.Sqrt(0.5);

    static P3 Add(P3 a, P3 d, double t) => new(a.X + d.X * t, a.Y + d.Y * t, a.Z + d.Z * t);
    static double Dot(P3 a, P3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    static P3 Cross(P3 a, P3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    static P3 Normalize(P3 a)
    {
        double l = Math.Sqrt(Dot(a, a));
        return l > 1e-12 ? new P3(a.X / l, a.Y / l, a.Z / l) : default;
    }
}
