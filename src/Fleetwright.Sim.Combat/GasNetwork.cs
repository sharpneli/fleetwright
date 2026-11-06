using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What an explosion's gas did (<see cref="GasNetwork"/>): the peak pressure at the source (kPa gauge) and what its
/// boundaries felt (MPa); the propellant burned (kg; and by the hull's first giving and a second after, kg); the spaces it
/// reached and their gas (m³); what gave: bulkheads, decks, watertight doors, the side, weather deck and bottom plates, a
/// part's walls; the boundaries it only sprang; the magazines alight, the gunhouses vented and lifted; the first boundary to give
/// and the first of the hull's own plates (s); when the first way up vented (s); how long it ran (s) and in how many steps.
/// What it left to the flooding: between spaces (torn and blown open, m²; sprung seams, m²) and to the sea or air (m²). Open: it
/// vented as fast as it formed (no gas load at all). What it did to the girder is <see cref="HullGirder"/>'s to read.</summary>
public struct GasResult
{
    public double PeakKPa, FeltMPa, BurnedKg, FastKg, Charges, VolumeM3, FirstS, HullS, VentS, VentM2, EndS;
    public int Spaces, Holed, Bulkheads, Decks, Doors, Deformed, Sides, WeatherDecks, Bottoms, PartWalls, Magazines, Vented, Lifted, Steps;
    /// <summary>The most it drove within the hull's period, its energy × its mass, J·kg: what can whip her.</summary>
    public double WhipEM;
    public double OpenM2, LeakM2, HoleM2;
    public bool Open;
    public readonly int Ruptured => Bulkheads + Decks + Sides + WeatherDecks + Bottoms + PartWalls;
}

/// <summary>The gas of any explosion through a ship (docs/battle/explosions.md EX1, EX2): one mass of gas and its energy in every
/// cell it reaches, flowing through what's open, each boundary taking up the difference across it as its panel responds and
/// giving at its own plate, by one rule. A detonation releases its energy in the first step; propellant by its burn law. That's
/// the only difference between them. A run ends with what it opened on the ship's condition (<see cref="ShipCondition.JoinOpenM2"/>,
/// the holes, <see cref="ShipCondition.EdgeLost"/>, <see cref="ShipCondition.FaceLost"/>), so a salvo's next shell finds it.
/// Its working state says nothing between runs, so it's one per thread (<see cref="For"/>), grown to the largest class it has met;
/// a run allocates nothing.</summary>
public sealed class GasNetwork
{
    const int Faces = 4, Cohorts = 32;
    const double DtMin = 1e-4, DtMax = 0.02, GaugeFloor = 40e3, RtAir = 84_000, AirR = 287, SeaRhoG = 1025 * 9.81, StepCellM3 = 10;
    /// <summary>The step's control, checked by <c>explode --step</c> (explosions.md EX2): the most a pressure that matters may
    /// move in one, or a burning stow put in, as a share of itself or of this share of the highest pressure anywhere. Halved it
    /// moves nothing much; doubled it starts to overshoot.</summary>
    const double StepMost = 0.8, StepShareOfPeak = 0.25;

    /// <summary>A bench's view: what gave and when. Null in the game.</summary>
    public static Action<string>? Trace;

    [ThreadStatic]
    static double stepK;

    /// <summary>The solver's step on this thread, a scale on how far pressures may move in one: 1 in the game, a bench's or a
    /// test's convergence check moves it.</summary>
    public static double StepK
    {
        get => stepK > 0 ? stepK : 1;
        set => stepK = value;
    }

    [ThreadStatic]
    static GasNetwork? mine;

    /// <summary>This thread's network, ready for a class. What <see cref="Reached"/> and the rest report is good until its
    /// next run on this thread.</summary>
    public static GasNetwork For(ShipClass c)
    {
        var g = mine ??= new GasNetwork();
        g.Bind(c);
        return g;
    }

    /// <summary>A class's edges as the gas sees them, shared: a pool entry's canonical entry (the lower cell's, what the flooding
    /// reads), its shared area and its plate.</summary>
    sealed class Edges
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ShipClass, Edges> Table = new();

        public readonly int[] Canon;
        public readonly double[] Area, Mm;

        Edges(ShipClass c)
        {
            int ne = c.NeighbourCount;
            (Canon, Area, Mm) = (new int[ne], new double[ne], new double[ne]);
            for (int i = 0; i < c.Cells.Length; i++)
            {
                var nb = c.Cells[i].Neighbours;
                for (int j = nb.Start; j < nb.End; j++)
                {
                    ref readonly var e = ref c.Neighbour(j);
                    Canon[j] = Leaks.Entry(c, i, j);
                    Area[j] = Blast.Area(c.Cells[i].Box, c.Cells[e.Cell].Box, e.Join);
                    Mm[j] = Blast.JoinMm(c, e, c.Cells[i].Box, c.Cells[e.Cell].Box);
                }
            }
        }

        public static Edges Of(ShipClass c) => Table.GetValue(c, static x => new Edges(x));
    }

    ShipClass c = null!;
    int[] canon = [];
    double[] area = [], mm = [];
    HullFaces hull = null!;
    /// <summary>The space a burst in a part is: the node after her cells.</summary>
    int partNode;
    // the cells', and the part's as the last
    int[] stamp = [], region = [];
    double[] v = [], m = [], u = [], p = [], rt = [], pStart = [], vent = [], lagged = [], felt = [], hottest = [];
    int n, id;
    // the edges', by canon
    int[] edgeStamp = [];
    double[] open = [], opened = [];
    Boundary[] edges = [];
    // the outside faces', cell x 4
    int[] faceStamp = [];
    double[] faceBack = [], faceDry = [];
    Boundary[] faces = [];
    // a part's walls
    double partM2, partMm;
    Boundary partWalls;
    // the links a magazine's train adds: from a cell to another or the air (-1), opening at a gauge pressure there
    int[] linkFrom = [], linkTo = [], linkMount = [], rounds = [];
    double[] linkAt = [], linkArea = [];
    bool[] linkOpen = [], ventedMount = [], liftedMount = [];
    int links;
    /// <summary>The stows alight: a pool, one for every magazine of the largest class this thread has met.</summary>
    Stow[] stows = [];
    int nStows;
    double burned, fastKg, peakSource;
    SimSettings s = null!;
    ShipCondition k = null!;
    Waterplane water;
    FlashDiscipline flash;
    double time, chokeK, whipKg, whipJ, whipPeak, lag;
    GasResult res;

    /// <summary>A boundary in a run, whichever it is (a plate between two spaces, one to the outside, a part's walls): by one rule
    /// its load takes up as its panel responds, what it opens goes up <see cref="Blast.Opening"/>'s curve, and it gives at
    /// <see cref="Blast.RuptureD"/>. What that opening is to (a neighbour, the air, the sea) is the caller's.</summary>
    struct Boundary
    {
        public double Felt, Peak, Torn;
        public byte Fate;

        /// <summary>A load of MPa on it for a step (lag: its panel's response), its plate's rupture pressure, joint and area: what it
        /// opened more this step, m², and the load as a D.</summary>
        public double Take(double load, double lag, double rupture, Joint joint, double areaM2, SimSettings s, out double d)
        {
            Felt += (load - Felt) * lag;
            Peak = Math.Max(Peak, Felt);
            d = Blast.RuptureD * Felt / rupture;
            double wants = Blast.Opening(d, joint, s) * areaM2;
            if (wants <= Torn)
                return 0;
            double more = wants - Torn;
            Torn = wants;
            return more;
        }

        /// <summary>The most it felt as a D: what a boundary that held is left with.</summary>
        public readonly double PeakD(double rupture) => Blast.RuptureD * Peak / rupture;
    }

    sealed class Stow
    {
        public int Room, Cell, Cohort;
        public PropellantRow Pr;
        public double Ck, Open, Cased, Flooded, Pkg, TBurn, LitOpen, LitCased, CohortT0, EnergyJKg;
        public readonly double[] Cnt = new double[Cohorts], Z = new double[Cohorts];
        public double Burning, Pending, MadePa;
    }

    void Bind(ShipClass cls)
    {
        if (ReferenceEquals(c, cls))
            return;
        c = cls;
        var e = Edges.Of(cls);
        (canon, area, mm) = (e.Canon, e.Area, e.Mm);
        hull = HullFaces.Of(cls);
        int nc = cls.Cells.Length, ne = cls.NeighbourCount, nm = cls.Mounts.Length;
        partNode = nc;
        if (stamp.Length < nc + 1)
        {
            // stamps start at 0, under every id to come: nothing carries over
            int size = nc + 1;
            (stamp, region) = (new int[size], new int[size]);
            (v, m, u, p, rt, pStart) = (new double[size], new double[size], new double[size], new double[size], new double[size], new double[size]);
            (vent, lagged, felt, hottest) = (new double[size], new double[size], new double[size], new double[size]);
            (faceStamp, faces, faceBack, faceDry) = (new int[size * Faces], new Boundary[size * Faces], new double[size * Faces], new double[size * Faces]);
        }
        if (edgeStamp.Length < ne)
            (edgeStamp, open, opened, edges) = (new int[ne], new double[ne], new double[ne], new Boundary[ne]);
        if (ventedMount.Length < nm)
        {
            (ventedMount, liftedMount, rounds) = (new bool[nm], new bool[nm], new int[nm]);
            int lm = 2 * nm + 8;
            (linkFrom, linkTo, linkMount, linkAt, linkArea, linkOpen) = (new int[lm], new int[lm], new int[lm], new double[lm], new double[lm], new bool[lm]);
        }
        // the rooms that can burn: her magazines, and any other a mount draws from
        int burnable = 0;
        foreach (ref readonly var room in cls.Rooms)
            burnable += room.Kind == RoomKind.Magazine ? 1 : 0;
        foreach (ref readonly var mr in cls.Mounts)
            burnable += mr.Magazine >= 0 && cls.Rooms[mr.Magazine].Kind != RoomKind.Magazine ? 1 : 0;
        if (stows.Length < burnable)
        {
            int had = stows.Length;
            Array.Resize(ref stows, burnable);
            for (int i = had; i < burnable; i++)
                stows[i] = new Stow();
        }
    }

    double GammaK => s.BlastHeatK;
    double Gm => 1 + s.BlastHeatK;
    double P0 => s.AtmosphereMPa * 1e6;

    /// <summary>w kg of a filler bursting at a point in a space: all its energy in the first step, its afterburn as the
    /// space's air allows (11 §2.1); the near zone's holes in the space's boundaries (11 §4.1), and ventM2 more from the start
    /// (a near-wall burst's own hole). A space with vents as big as its faces is open: nothing builds there, and only the near
    /// zone stays.</summary>
    public GasResult Detonate(ShipCondition cond, int cell, P3 at, double w, in ShellTypeRow f, double ventM2, double nearM, double nearHoleM, SimSettings settings, ref RandomStream r)
    {
        Begin(cond, settings);
        JoinSpace(cell);
        vent[cell] += ventM2;
        double gas = 0, vents = 0;
        for (int i = 0; i < n; i++)
            (gas, vents) = (gas + v[region[i]], vents + vent[region[i]]);
        Near(cell, at, nearM, nearHoleM);
        return Burst(cell, gas, vents, w, f, ref r);
    }

    /// <summary>w kg bursting in a part of her (a gunhouse, a conning tower, a superstructure block, a barbette; E5): its gas in
    /// the part's free space, vented by what its kind leaves open and its own hole, against its walls of a plate. Open as above.</summary>
    public GasResult Detonate(ShipCondition cond, double gasM3, double ventM2, double wallM2, double wallMm, double w, in ShellTypeRow f, SimSettings settings, ref RandomStream r)
    {
        Begin(cond, settings);
        Node(partNode, gasM3, ventM2);
        (partM2, partMm, partWalls) = (wallM2, wallMm, default);
        return Burst(partNode, gasM3, ventM2, w, f, ref r);
    }

    /// <summary>A charge going off in a space of so much gas and vent: all its energy in the first step, unless it's open.</summary>
    GasResult Burst(int source, double gasM3, double ventM2, double w, in ShellTypeRow f, ref RandomStream r)
    {
        if (Blast.Open(gasM3, ventM2, s))
            res.Open = true;
        else
        {
            Charge(source, w, f, gasM3);
            Run(source, 2e-4);
        }
        return Finish(source, ref r);
    }

    void Charge(int cell, double w, in ShellTypeRow f, double gasM3)
    {
        double joules = Blast.EnergyMJ(f, w, gasM3) * 1e6;
        m[cell] += w;
        u[cell] += joules;
        Refresh(cell);
        (whipKg, whipJ, whipPeak) = (w, joules, joules * w);
    }

    /// <summary>A magazine alight: its stow burns by the burn law in its space, the train's ways up open as the pressure
    /// stands, and any magazine its gas reaches lights in turn. Nothing lights (no charge known, nothing to burn): no result's
    /// <see cref="GasResult.Magazines"/>.</summary>
    public GasResult Burn(ShipCondition cond, int room, ReadOnlySpan<MountState> mounts, SimSettings settings, ref RandomStream r)
    {
        Begin(cond, settings);
        for (int mt = 0; mt < c.Mounts.Length; mt++)
            rounds[mt] = mt < mounts.Length ? mounts[mt].Rounds : c.Mounts[mt].Rounds;
        flash = Magazines.Discipline(cond, settings);
        int cell = Light(room);
        if (cell < 0)
            return res;
        Run(cell, 2e-3);
        return Finish(cell, ref r);
    }

    void Begin(ShipCondition cond, SimSettings settings)
    {
        (k, s) = (cond, settings);
        double gm = Gm;
        chokeK = Math.Sqrt(gm) * Math.Pow(2 / (gm + 1), (gm + 1) / (2 * (gm - 1)));
        id++;
        (n, links, nStows, burned, fastKg, time, peakSource) = (0, 0, 0, 0, 0, 0, 0);
        res = new GasResult { FirstS = double.NaN, HullS = double.NaN, VentS = double.NaN };
        (whipKg, whipJ, whipPeak) = (0, 0, 0);
        water = HitQuery.WaterOf(c, k.Attitude);
        Array.Clear(ventedMount);
        Array.Clear(liftedMount);
    }

    /// <summary>A cell and what's open to it joins: it's one room.</summary>
    void JoinSpace(int cell)
    {
        int first = n;
        if (stamp[cell] != id)
            Join(cell);
        for (int i = first; i < n; i++)
            foreach (ref readonly var e in c.Neighbours(c.Cells[region[i]].Neighbours))
                if (e.Join == JoinKind.Open && stamp[e.Cell] != id)
                    Join(e.Cell);
    }

    /// <summary>A cell joins: its air at the atmosphere's pressure, its vents.</summary>
    void Join(int i) => Node(i, Blast.GasM3(c, k, i), Blast.VentM2(c, k, i, s));

    void Node(int i, double gasM3, double ventM2)
    {
        stamp[i] = id;
        region[n++] = i;
        v[i] = gasM3;
        m[i] = P0 * gasM3 / RtAir;
        u[i] = P0 * gasM3 / GammaK;
        vent[i] = ventM2;
        (lagged[i], felt[i]) = (0, 0);
        p[i] = pStart[i] = P0;
        rt[i] = hottest[i] = RtAir;
    }

    /// <summary>An edge, the first time this run: what's open already stays open, a door left open and a seam already sprung with
    /// it; what's torn from before needs no tearing again, and a door blown before isn't plate torn.</summary>
    double EdgeOpen(int j)
    {
        int q = canon[j];
        if (edgeStamp[q] != id)
        {
            edgeStamp[q] = id;
            opened[q] = 0;
            ref var b = ref edges[q];
            b = default;
            ref readonly var e = ref c.Neighbour(j);
            if (e.Join == JoinKind.Open)
                (open[q], b.Torn) = (area[j], area[j]);
            else
            {
                double was = Math.Min(k.JoinOpenM2[q], area[j]);
                b.Torn = (k.JoinMarks[q] & JoinMark.DoorBlown) != 0 ? Math.Max(was - s.DoorM2, 0) : was;
                double doors = 0;
                if ((k.JoinMarks[q] & JoinMark.DoorOpen) != 0)
                {
                    doors = s.DoorM2;
                    b.Fate = Blast.Fate.Door;   // a door already open can't be blown
                }
                open[q] = Math.Min(was + k.JoinSeamM2[q] + doors, area[j]);
            }
        }
        return open[q];
    }

    /// <summary>The near zone: the space's boundaries within it holed through, by the shock and fragments.</summary>
    void Near(int cell, P3 at, double nearM, double nearHoleM)
    {
        var b0 = c.Cells[cell].Box;
        var pool = c.Cells[cell].Neighbours;
        for (int j = pool.Start; j < pool.End; j++)
        {
            ref readonly var e = ref c.Neighbour(j);
            if (e.Join == JoinKind.Open || Blast.FaceDistance(b0, c.Cells[e.Cell].Box, e.Join, at) > nearM)
                continue;
            EdgeOpen(j);
            int q = canon[j];
            double hole = Math.Min(Math.PI * nearHoleM * nearHoleM, area[j]);
            (open[q], opened[q], edges[q].Fate) = (Math.Min(open[q] + hole, area[j]), opened[q] + hole, Blast.Fate.Holed);
            edges[q].Torn = Math.Min(edges[q].Torn + hole, area[j]);   // what the gas tears from here on starts from this
            res.Holed++;
        }
    }

    /// <summary>The ship's own magazine lit: its stow, and the links of the mounts it feeds. Its largest cell, or -1.</summary>
    int Light(int room)
    {
        if (k.MagazineSpent[room])
            return -1;
        for (int i = 0; i < nStows; i++)
            if (stows[i].Room == room)
                return -1;
        ChargeRow charge = default;
        foreach (ref readonly var mr in c.Mounts)
            if (mr.Magazine == room && mr.Battery >= 0)
            {
                charge = c.Charges[mr.Type];
                break;
            }
        if (!(charge.Propellant.BetaMmS > 0) || !(charge.MassKg > 0))
            return -1;
        int cell = Largest(room);
        if (cell < 0)
            return -1;
        var st = stows[nStows++];
        LoadStow(st, room, cell, charge);
        JoinSpace(cell);
        LinkTrains(room, cell);
        res.Magazines = nStows;
        Trace?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture, "      {0:0.000} s: {1} alight ({2:0} open, {3:0} packed, {4:0} kg each, {5:0} m³ of gas)", time, c.RoomId(room), st.Open, st.Cased, st.Ck, v[cell]));
        return cell;
    }

    /// <summary>A pooled stow set to a magazine's charges as they lie: what's flooded, its burn law, the first flame.</summary>
    void LoadStow(Stow st, int room, int cell, in ChargeRow charge)
    {
        Magazines.Stow(c, room, rounds, flash, out double nOpen, out double nCased, out double mass);
        st.Room = room;
        st.Cell = cell;
        st.Pr = charge.Propellant;
        st.Ck = mass / Math.Max(nOpen + nCased, 1);
        (st.Open, st.Cased) = (nOpen, nCased);
        res.Charges += nOpen + nCased;
        st.Flooded = Math.Max(Magazines.Flooded(c, k, room), k.MagazineWet.Length > room ? k.MagazineWet[room] : 0);
        st.Pkg = s.MagazinePackageR[Math.Min((int)charge.Packaging, s.MagazinePackageR.Length - 1)] * st.Pr.Sensitivity;
        st.TBurn = st.Pr.WebM / Magazines.Rate(101_325, st.Pr);
        st.EnergyJKg = s.MagazineHeatKeep * st.Pr.ImpetusJkg / GammaK;
        (st.LitOpen, st.LitCased, st.Cohort, st.CohortT0, st.Burning, st.Pending, st.MadePa) = (0, 0, 0, -1, 0, 0, 0);
        if (nOpen * (1 - st.Flooded) < 1 && nCased * (1 - st.Flooded) >= 1)
        {
            st.LitCased = 1;
            (st.Cnt[0], st.Z[0], st.Cohort, st.CohortT0) = (1, 0, 1, time);
        }
    }

    /// <summary>The trains of the mounts a magazine feeds: the scuttle to the handling room, the way up and out.</summary>
    void LinkTrains(int room, int cell)
    {
        for (int mt = 0; mt < c.Mounts.Length; mt++)
        {
            ref readonly var mr = ref c.Mounts[mt];
            if (mr.Magazine != room || mr.Battery < 0 || links + 2 > linkFrom.Length)
                continue;
            int top = cell;
            foreach (var station in c.Train(mt))
                if (station.Kind == StationKind.Handling && Largest(station.Room) is var h and >= 0)
                {
                    Link(cell, h, s.MagazineScuttleKPa * 1e3, s.MagazineHoistM2, -1);
                    top = h;
                    break;
                }
            double lift = c.LiftKPa(mt);
            if (lift > 0)
                Link(top, -1, s.MagazineRoofShare * lift * 1e3, s.MagazineTrunkShare * c.BoreM2(mt), mt);
            else
                Link(top, -1, s.MagazineScuttleKPa * 1e3, s.MagazineHoistM2, -1);   // a shield's or casemate's hoist: a way out, but no gunhouse vented
        }
    }

    void Link(int from, int to, double atPa, double m2, int mount)
    {
        (linkFrom[links], linkTo[links], linkAt[links], linkArea[links], linkOpen[links], linkMount[links]) = (from, to, atPa, m2, false, mount);
        links++;
    }

    int Largest(int room)
    {
        int cell = -1;
        foreach (int ci in c.Links(c.Rooms[room].Cells))
            if (cell < 0 || c.Cells[ci].VolumeM3 > c.Cells[cell].VolumeM3)
                cell = ci;
        return cell;
    }

    void Run(int source, double dt0)
    {
        double dt = dt0, quiet = 0;
        int steps = 0;
        while (time < s.GasRunS)
        {
            lag = 1 - Math.Exp(-dt * Math.PI * 1000 / Math.Max(s.PanelMs, 1e-3));   // a panel's response: P·min(k, π·t/T) as a lag
            for (int a = 0; a < n; a++)
                pStart[region[a]] = p[region[a]];   // the step's control compares end with end
            // half the step's gas before its flows and half after, so what the boundaries feel is the middle of the step: else a long
            // step reads the pressure once it has leaked away, not what a burning stow holds against its vents
            Sources(dt);
            bool caught = TakeUp();
            peakSource = Math.Max(peakSource, p[source] - P0);
            bool changed = Boundaries(dt);
            Links(dt);
            SecondHalf();
            double most = Moved(out double gauge);
            time += dt;
            steps++;
            if (!double.IsNaN(res.HullS) && time <= res.HullS + 1)
                fastKg = burned;
            quiet = changed || most > 0.01 ? 0 : quiet + dt;
            if (Done(caught, gauge, quiet))
                break;
            dt = changed || most > StepMost * StepK ? Math.Max(dt / 2, DtMin) : most < StepMost / 4 * StepK ? Math.Min(dt * 1.25, DtMax) : dt;
        }
        res.Steps = steps;
        res.EndS = time;
    }

    /// <summary>Each space's gas as it stands: what its walls feel as their panels take it up, how hot it has been, and whether
    /// that lights what's there. Whether every panel has caught up with its load.</summary>
    bool TakeUp()
    {
        bool caught = true;
        double keep = s.BlastLoadFactor;
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            Refresh(i);
            double load = keep * Math.Max(p[i] - P0, 0) / 1e6;
            lagged[i] += (load - lagged[i]) * lag;
            felt[i] = Math.Max(felt[i], lagged[i]);
            caught &= load <= 1.02 * lagged[i] || load < 1e-3;
            hottest[i] = Math.Max(hottest[i], rt[i]);
            Ignite(i);
        }
        return caught;
    }

    /// <summary>How far this step moved what matters, as a share (the step's control), and the highest gauge pressure anywhere.
    /// The step follows the pressures that load something: a far cell's few kPa moving by a share of themselves don't hold it
    /// down while the source's hundreds do the work. And a burning stow, whose pressure can stand still while gas pours through
    /// it: what the step put in, against what's there.</summary>
    double Moved(out double gauge)
    {
        double most = 0;
        gauge = 0;
        for (int a = 0; a < n; a++)
            gauge = Math.Max(gauge, p[region[a]] - P0);
        double floor = Math.Max(GaugeFloor, StepShareOfPeak * gauge);
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            if (v[i] >= StepCellM3)
                most = Math.Max(most, Math.Abs(p[i] - pStart[i]) / Math.Max(Math.Abs(pStart[i] - P0), floor));
        }
        for (int t = 0; t < nStows; t++)
            most = Math.Max(most, stows[t].MadePa / Math.Max(Math.Abs(p[stows[t].Cell] - P0), floor));
        return most;
    }

    /// <summary>Done when every panel has taken up its load, nothing can burn and nothing more can give: no intact door or plate
    /// takes less than the door's own.</summary>
    bool Done(bool caught, double gauge, double quiet)
    {
        bool burning = false;
        for (int i = 0; i < nStows; i++)
            burning |= stows[i].Burning > 0 || stows[i].LitOpen < stows[i].Open * (1 - stows[i].Flooded) - 1e-6;
        bool low = s.BlastLoadFactor * gauge < s.DoorMinMPa * 1e6;
        return caught && (!burning && (low || quiet > 1) || burning && low && quiet > 1 && AllLit());
    }

    /// <summary>Whether the flame has been through every stow: what's left of them only burns out.</summary>
    bool AllLit()
    {
        for (int i = 0; i < nStows; i++)
        {
            var st = stows[i];
            if (st.LitOpen < 0.99 * st.Open * (1 - st.Flooded) || st.LitCased < 0.99 * st.Cased * (1 - st.Flooded))
                return false;
        }
        return true;
    }

    /// <summary>The stows' gas: half of what each makes this step into its space, the flame on through it.</summary>
    void Sources(double dt)
    {
        double released = 0, madeKg = 0;
        for (int t = 0; t < nStows; t++)
        {
            var st = stows[t];
            double made = BurnStow(st, dt);
            st.Pending = made / 2;
            st.MadePa = made * st.EnergyJKg * GammaK / v[st.Cell];
            m[st.Cell] += st.Pending;
            u[st.Cell] += st.Pending * st.EnergyJKg;
            Refresh(st.Cell);
            burned += made;
            released += made * st.EnergyJKg;
            madeKg += made;
            SpreadFlame(st, dt);
        }
        if (nStows == 0)
            return;
        // the energy within about the hull's period (07 §3.1: a spike of about a second): what comes slower doesn't whip her
        double fade = Math.Exp(-dt);
        (whipKg, whipJ) = (whipKg * fade + madeKg, whipJ * fade + released);
        whipPeak = Math.Max(whipPeak, whipKg * whipJ);
    }

    /// <summary>What a stow's lit charges make in a step, kg: r = β·P^α on each cohort's burning surface.</summary>
    double BurnStow(Stow st, double dt)
    {
        double rate = Magazines.Rate(p[st.Cell], st.Pr), gen = 0, burning = 0;
        for (int i = 0; i < st.Cohort; i++)
        {
            if (st.Z[i] >= 1)
                continue;
            double shape = st.Pr.Form switch
            {
                PropellantForm.Cord => Math.Sqrt(1 - st.Z[i]),
                PropellantForm.Grain => Math.Pow(1 - st.Z[i], 2.0 / 3),
                _ => 1,
            };
            double kg = Math.Min(st.Pr.DensityKgM3 * st.Cnt[i] * st.Ck * st.Pr.SurfaceM2Kg * shape * rate, st.Cnt[i] * st.Ck * (1 - st.Z[i]) / dt);
            gen += kg;
            st.Z[i] = Math.Min(1, st.Z[i] + kg * dt / (st.Cnt[i] * st.Ck));
            burning += st.Cnt[i];
        }
        st.Burning = burning;
        return gen * dt;
    }

    /// <summary>The flame on through a stow: what lies open catches at once, what's packed by what's burning beside it.</summary>
    void SpreadFlame(Stow st, double dt)
    {
        double openDry = st.Open * (1 - st.Flooded), casedDry = st.Cased * (1 - st.Flooded), d = 0;
        if (openDry - st.LitOpen > 0)
        {
            double dOpen = (openDry - st.LitOpen) * (1 - Math.Exp(-dt / s.MagazineBagSpreadS));
            (st.LitOpen, d) = (st.LitOpen + dOpen, d + dOpen);
        }
        if (casedDry - st.LitCased > 0 && st.Burning > 0)
        {
            // a branching process: what's burning lights its neighbours at a rate, so it grows as an exponential over the step,
            // whatever the step's length (Euler here under-read the race at any step the pressures allow)
            double growth = st.Pkg / st.TBurn * (casedDry - st.LitCased) / Math.Max(casedDry, 1);
            double dCased = Math.Min(casedDry - st.LitCased, st.Burning * (Math.Exp(Math.Min(growth * dt, 20)) - 1));
            (st.LitCased, d) = (st.LitCased + dCased, d + dCased);
        }
        if (!(d > 0))
            return;
        if (st.Cohort > 0 && (time - st.CohortT0 < 0.1 || st.Cohort == Cohorts))
        {
            int last = st.Cohort - 1;
            st.Z[last] = st.Cnt[last] * st.Z[last] / (st.Cnt[last] + d);
            st.Cnt[last] += d;
        }
        else
            (st.Cnt[st.Cohort], st.Z[st.Cohort], st.Cohort, st.CohortT0) = (d, 0, st.Cohort + 1, time);
    }

    /// <summary>The step's other half of the gas <see cref="Sources"/> made.</summary>
    void SecondHalf()
    {
        for (int t = 0; t < nStows; t++)
        {
            var st = stows[t];
            m[st.Cell] += st.Pending;
            u[st.Cell] += st.Pending * st.EnergyJKg;
            Refresh(st.Cell);
            st.Pending = 0;
        }
    }

    /// <summary>Every boundary of the cells the gas is in: its load, what gives, the flow through what's open. True when
    /// something gave.</summary>
    bool Boundaries(double dt)
    {
        double keep = s.BlastLoadFactor;
        bool changed = false;
        for (int a = 0, a0 = n; a < a0; a++)
        {
            int i = region[a];
            if (i == partNode)
                changed |= LoadPartWalls(keep);
            else
            {
                changed |= LoadEdges(i, keep, dt);
                changed |= LoadFaces(i, keep);
            }
            if (vent[i] > 0)
                Flow(i, -1, vent[i], dt);
        }
        return changed;
    }

    /// <summary>A cell's boundaries with her other cells, each from its lower cell when both have gas; the gas through what's
    /// open.</summary>
    bool LoadEdges(int i, double keep, double dt)
    {
        bool changed = false;
        var nb = c.Cells[i].Neighbours;
        for (int j = nb.Start; j < nb.End; j++)
        {
            ref readonly var e = ref c.Neighbour(j);
            int o = e.Cell;
            bool inside = stamp[o] == id;
            if (inside && o < i)
                continue;   // from the lower cell
            double a1 = EdgeOpen(j);
            if (e.Join != JoinKind.Open)
                changed |= LoadEdge(i, j, e, keep * Math.Abs(p[i] - (inside ? p[o] : P0)) / 1e6, ref a1);
            if (a1 <= 0)
                continue;
            if (!inside)
                Join(o);   // it steps from the next step on
            Flow(i, o, a1, dt);
        }
        return changed;
    }

    /// <summary>A plate between two spaces under a load: what it tears, then whether it gives or its door blows.</summary>
    bool LoadEdge(int i, int j, in Neighbour e, double load, ref double a1)
    {
        int q = canon[j];
        ref var b = ref edges[q];
        double rupture = Blast.RuptureMPa(mm[j], c.InternalsJoint, s);
        double more = b.Take(load, lag, rupture, c.InternalsJoint, area[j], s, out double d);
        if (more > 0)
        {
            (open[q], opened[q]) = (Math.Min(open[q] + more, area[j]), opened[q] + more);
            a1 = open[q];
        }
        if (d >= Blast.RuptureD && b.Fate != Blast.Fate.Ruptured)
        {
            b.Fate = Blast.Fate.Ruptured;
            if (e.Join == JoinKind.Deck)
                res.Decks++;
            else
                res.Bulkheads++;
            Gave(false, i, e.Join == JoinKind.Bulkhead, e.Cell);
            if (Trace != null)
                Say(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} to {2} ({3:0} mm) at {4:0} kPa felt", e.Join == JoinKind.Deck ? "deck" : "bulkhead", c.CellId(i), c.CellId(e.Cell), mm[j], b.Felt * 1000));
            return true;
        }
        if (b.Fate != Blast.Fate.None || b.Felt < Math.Max(s.DoorMinMPa, s.DoorShare * rupture) || !Leaks.HasDoor(c, e, i) || (k.JoinMarks[q] & JoinMark.DoorBlown) != 0)
            return false;
        b.Fate = Blast.Fate.Door;
        k.JoinMarks[q] |= JoinMark.DoorBlown;
        (open[q], opened[q]) = (Math.Min(open[q] + s.DoorM2, area[j]), opened[q] + s.DoorM2);
        a1 = open[q];
        res.Doors++;
        res.FirstS = double.IsNaN(res.FirstS) ? time : res.FirstS;   // a way for the gas, not the flame's: Ignite has that
        if (Trace != null)
            Say(string.Format(System.Globalization.CultureInfo.InvariantCulture, "door {0} to {1} at {2:0} kPa felt", c.CellId(i), c.CellId(e.Cell), b.Felt * 1000));
        return true;
    }

    /// <summary>A cell's outside faces: what they tear above the sea is a vent; under it the sea's in, and the gas stays.</summary>
    bool LoadFaces(int i, double keep)
    {
        bool changed = false;
        for (int f = 0; f < Faces; f++)
        {
            int x = i * Faces + f;
            double fa = hull.Area[x];
            if (!(fa > 0))
                continue;
            if (faceStamp[x] != id)
                FirstOnFace(x, i, f);
            ref var b = ref faces[x];
            double more = b.Take(keep * Math.Max(p[i] - P0 - faceBack[x], 0) / 1e6, lag, Blast.RuptureMPa(Math.Max(hull.HeldMm[x], 1), c.ShellJoint, s), c.ShellJoint, fa, s, out double d);
            if (!(more > 0))
                continue;
            vent[i] += more * faceDry[x];
            if (d < Blast.RuptureD || b.Fate == Blast.Fate.Ruptured)
                continue;
            b.Fate = Blast.Fate.Ruptured;
            if (f == 2)
                res.WeatherDecks++;
            else if (f == 3)
                res.Bottoms++;
            else
                res.Sides++;
            Gave(true, i, false, -1);
            if (Trace != null)
                Say(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} of {1} ({2:0} mm held) at {3:0} kPa felt, {4:0.0} m² so far, {5:0%} dry", f == 2 ? "weather deck" : f == 3 ? "bottom" : "side", c.CellId(i), hull.HeldMm[x], b.Felt * 1000, b.Torn, faceDry[x]));
            changed = true;
        }
        return changed;
    }

    /// <summary>A face, the first time this run: what's torn from before, and the sea behind what's under it, which presses back;
    /// a tear under the sea lets the sea in, not the gas out (07 §2.4).</summary>
    void FirstOnFace(int x, int i, int f)
    {
        faceStamp[x] = id;
        faces[x] = new Boundary { Torn = k.FaceTornM2[x] };
        var point = hull.Point[x];
        var box = c.Cells[i].Box;
        double wl = water.At(point.X, point.Y);
        faceBack[x] = f == 2 ? 0 : Math.Max(wl - point.Z, 0) * SeaRhoG;
        faceDry[x] = f == 2 ? 1 : f == 3 ? 0 : Math.Clamp((box.Top - wl) / Math.Max(box.Top - box.Base, 0.1), 0, 1);
    }

    /// <summary>A part's walls: what they open is a vent.</summary>
    bool LoadPartWalls(double keep)
    {
        vent[partNode] += partWalls.Take(keep * Math.Max(p[partNode] - P0, 0) / 1e6, lag, Blast.RuptureMPa(Math.Max(partMm, 1), c.ShellJoint, s), c.ShellJoint, partM2, s, out double d);
        if (d < Blast.RuptureD || partWalls.Fate == Blast.Fate.Ruptured)
            return false;
        partWalls.Fate = Blast.Fate.Ruptured;
        res.PartWalls++;
        Gave(false, partNode, false, -1);
        return true;
    }

    /// <summary>Gas hot enough to light charges (<see cref="SimSettings.GasIgniteK"/>) in a magazine's space lights it, as
    /// the air pushed ahead of the hot gas doesn't.</summary>
    void Ignite(int cell)
    {
        if (nStows == 0 || rt[cell] < s.GasIgniteK * AirR || cell == partNode)
            return;
        foreach (int room in c.RoomsIn(cell))
            if (c.Rooms[room].Kind == RoomKind.Magazine)
                Light(room);
    }

    void Gave(bool hullPlate, int cell, bool bulkhead, int other)
    {
        if (double.IsNaN(res.FirstS))
            res.FirstS = time;
        if (hullPlate && double.IsNaN(res.HullS))
            res.HullS = time;
        // along the passages: the group's magazines light when a bulkhead of one alight gives, the passages running through
        // them (07 §2.4). Either side of it may be hers: an edge is worked from its lower cell
        for (int t = 0; bulkhead && t < nStows; t++)
        {
            int group = c.PassageGroup(stows[t].Room);
            if (group < 0 || !InRoom(cell, stows[t].Room) && !InRoom(other, stows[t].Room))
                continue;
            for (int rm = 0; rm < c.Rooms.Length; rm++)
                if (c.Rooms[rm].Kind == RoomKind.Magazine && c.PassageGroup(rm) == group)
                    Light(rm);
        }
    }

    bool InRoom(int cell, int room)
    {
        foreach (int r in c.RoomsIn(cell))
            if (r == room)
                return true;
        return false;
    }

    void Say(string what) => Trace?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture, "      {0:0.000} s: {1}", time, what));

    void Links(double dt)
    {
        for (int l = 0; l < links; l++)
        {
            int from = linkFrom[l];
            if (stamp[from] != id)
                continue;
            if (!linkOpen[l])
            {
                if (p[from] - P0 < linkAt[l])
                    continue;
                linkOpen[l] = true;
                int mt = linkMount[l];
                if (mt >= 0 && !ventedMount[mt])
                {
                    ventedMount[mt] = true;
                    res.Vented++;
                    if (double.IsNaN(res.VentS))
                        res.VentS = time;
                }
                Trace?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture, "      {0:0.000} s: the way from {1} open at {2:0} kPa, {3:0.00} m²", time, c.CellId(from), (p[from] - P0) / 1000, linkArea[l]));
            }
            int to = linkTo[l];
            if (to >= 0 && stamp[to] != id)
                Join(to);
            Flow(from, to, linkArea[l], dt);
            int mount = linkMount[l];
            if (mount >= 0 && !liftedMount[mount] && p[from] - P0 >= c.LiftKPa(mount) * 1e3 && c.LiftKPa(mount) > 0)
            {
                liftedMount[mount] = true;
                res.Lifted++;
            }
        }
    }

    /// <summary>A cell's pressure and rt from its gas.</summary>
    void Refresh(int i)
    {
        p[i] = GammaK * u[i] / v[i];
        rt[i] = p[i] * v[i] / m[i];
    }

    /// <summary>Gas from the higher side to the lower through a m² (to -1: the air), taken at once and each edge in turn: the
    /// flow choked or not, carrying its enthalpy, relaxing the pair toward level with its own time constant, so the step's
    /// length doesn't change where they're going and a small cell with a big tear can't overshoot.</summary>
    void Flow(int i, int o, double a, double dt)
    {
        double pa = p[i], pb = o >= 0 ? p[o] : P0;
        int up = i, down = o;
        if (pb > pa)
        {
            if (o < 0)
                return;
            (up, down, pa, pb) = (o, i, pb, pa);
        }
        double diff = pa - pb;
        if (!(diff > 1) || !(a > 0))
            return;
        double r = rt[up], rho = pa / r, gm = Gm;
        double mdot = Math.Min(s.GasVentCd * chokeK * a * pa / Math.Sqrt(r), s.GasVentCd * a * Math.Sqrt(2 * rho * diff));
        double vu = v[up], share = down >= 0 ? v[down] / (vu + v[down]) : 1;
        double level = diff * vu * share / (gm * r);          // the kg that would level them, carrying this enthalpy
        double x = mdot * dt / Math.Max(level, 1e-12);
        double moved = level * (x < 1e-3 ? x : 1 - Math.Exp(-x));
        moved = Math.Min(moved, 0.9 * m[up]);
        double energy = moved * gm / (gm - 1) * r;
        m[up] -= moved;
        u[up] = Math.Max(u[up] - energy, 1e-3);
        Refresh(up);
        if (down >= 0)
        {
            m[down] += moved;
            u[down] += energy;
            Refresh(down);
            hottest[down] = Math.Max(hottest[down], rt[down]);
        }
    }

    GasResult Finish(int source, ref RandomStream r)
    {
        res.PeakKPa = peakSource / 1000;
        res.FeltMPa = felt[source];
        // what was lit and hadn't burned when the run stopped goes on burning, harmlessly, at no pressure
        for (int t = 0; t < nStows; t++)
        {
            var st = stows[t];
            for (int i = 0; i < st.Cohort; i++)
                burned += st.Cnt[i] * st.Ck * (1 - st.Z[i]);
        }
        res.BurnedKg = burned;
        res.FastKg = double.IsNaN(res.HullS) ? burned : fastKg;
        res.Spaces = n;
        double vol = 0, vents = 0;
        for (int a = 0; a < n; a++)
            (vol, vents) = (vol + v[region[a]], vents + vent[region[a]]);
        for (int l = 0; l < links; l++)
            vents += linkOpen[l] && linkTo[l] < 0 ? linkArea[l] : 0;
        (res.VolumeM3, res.VentM2) = (vol, vents);
        res.WhipEM = whipPeak;
        Commit(ref r);
        return res;
    }

    /// <summary>What this run left on the ship's condition: what it tore and blew open between spaces (never past a boundary's
    /// own area) and to the sea or air; a boundary that held, its seams sprung and closures loosened by the most it felt (not what's
    /// left on it once the gas has gone), drawn about the curve; and what the girder has lost of each boundary so far (<see cref="ShipCondition.EdgeLost"/>). Short of
    /// rupture its seams leak but it carries its load; past it, <see cref="Blast.LostShare"/>. No step: one curve from the first
    /// crack to torn out.</summary>
    void Commit(ref RandomStream r)
    {
        bool lost = false;
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            if (i == partNode)
                continue;
            lost |= CommitEdges(i, ref r);
            lost |= CommitFaces(i, ref r);
        }
        k.GirderStale |= lost;
        for (int t = 0; t < nStows; t++)
            k.MagazineSpent[stows[t].Room] = true;
    }

    bool CommitEdges(int i, ref RandomStream r)
    {
        bool lost = false;
        var nb = c.Cells[i].Neighbours;
        for (int j = nb.Start; j < nb.End; j++)
        {
            int q = canon[j];
            if (edgeStamp[q] != id || !(area[j] > 0))
                continue;
            ref var b = ref edges[q];
            if (b.Fate is Blast.Fate.Holed or Blast.Fate.Ruptured or Blast.Fate.Door)
            {
                if (opened[q] > 0)
                {
                    k.JoinOpenM2[q] = Math.Min(k.JoinOpenM2[q] + opened[q], area[j]);
                    res.OpenM2 += opened[q];
                    opened[q] = 0;
                }
                double share = b.Fate == Blast.Fate.Ruptured ? Blast.LostShare(b.Torn, area[j], s)
                    : b.Fate == Blast.Fate.Holed ? Math.Sqrt(Math.Min(b.Torn / area[j], 1)) : 0;
                if (share > k.EdgeLost[q])
                    (k.EdgeLost[q], lost) = (share, true);
            }
            else if (b.Fate == Blast.Fate.None && b.Peak > 0)
            {
                b.Fate = Blast.Fate.Deformed;   // once, from whichever side
                double d = b.PeakD(Blast.RuptureMPa(mm[j], c.InternalsJoint, s));
                double leak = Blast.Leak(d, area[j], c.InternalsJoint, s, ref r);
                leak += Leaks.Sprung(c, k, i, j, leak, d, area[j], s, ref r);   // its seams sprung and its closures loosened (DC1)
                if (leak > 0)
                    (res.LeakM2, res.Deformed) = (res.LeakM2 + leak, res.Deformed + 1);
            }
        }
        return lost;
    }

    bool CommitFaces(int i, ref RandomStream r)
    {
        bool lost = false;
        for (int f = 0; f < Faces; f++)
        {
            int x = i * Faces + f;
            if (faceStamp[x] != id)
                continue;
            double fa = hull.Area[x];
            ref var b = ref faces[x];
            if (b.Fate == Blast.Fate.Ruptured)
            {
                double more = b.Torn - k.FaceTornM2[x];
                if (more > 0)
                {
                    k.Hole(i, more, hull.Point[x]);
                    k.FaceTornM2[x] = b.Torn;
                    res.HoleM2 += more;
                }
                double share = Blast.LostShare(b.Torn, fa, s);
                if (share > k.FaceLost[x])
                    (k.FaceLost[x], lost) = (share, true);
            }
            else if (b.Fate == Blast.Fate.None && b.Peak > 0)
            {
                b.Fate = Blast.Fate.Deformed;
                double leak = Blast.Leak(b.PeakD(Blast.RuptureMPa(Math.Max(hull.HeldMm[x], 1), c.ShellJoint, s)), fa, c.ShellJoint, s, ref r);
                if (leak > 0)
                {
                    k.Hole(i, leak, hull.Point[x], seam: true);
                    (res.HoleM2, res.Deformed) = (res.HoleM2 + leak, res.Deformed + 1);
                }
            }
        }
        return lost;
    }

    /// <summary>What a cell's boundaries felt, MPa; its peak (0: the gas never got there).</summary>
    public double FeltMPa(int cell) => stamp[cell] == id ? felt[cell] : 0;

    /// <summary>Whether gas hot enough to light charges reached a cell (<see cref="SimSettings.GasIgniteK"/>): the burst's flame
    /// carried there, not the cold air pushed ahead of it.</summary>
    public bool Hot(int cell) => stamp[cell] == id && hottest[cell] >= s.GasIgniteK * AirR;

    /// <summary>The cells reached, by the order they joined.</summary>
    public ReadOnlySpan<int> Reached() => region.AsSpan(0, n);

    /// <summary>The magazines this run lit: how many, and each one's room.</summary>
    public int LitCount => nStows;
    public int LitRoom(int i) => stows[i].Room;

    /// <summary>Whether a mount's way up vented this run, and whether its gunhouse lifted.</summary>
    public bool VentedMount(int mount) => ventedMount[mount];
    public bool LiftedMount(int mount) => liftedMount[mount];
}
