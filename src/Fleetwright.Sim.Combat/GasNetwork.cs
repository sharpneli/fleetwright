using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What the bench may vary while EX1's ladder is climbed (docs/battle/explosions.md): each rung one number, the
/// plain network at the defaults. They go when the ladder is done.</summary>
public struct GasKnobs
{
    /// <summary>The share of a belt's millimetres that hold as plate when pushed from inside (candidate 1).</summary>
    public double BeltShare;
    /// <summary>The propellant's heat kept in the gas (candidate 2).</summary>
    public double HeatKeep;
    /// <summary>The share of a boundary's area a rupture opens (candidate 4).</summary>
    public double TearShare;
    /// <summary>A panel's period, ms: how fast a boundary takes up its load (candidate 5).</summary>
    public double PanelMs;
    /// <summary>A plate's rupture, MPa a mm.</summary>
    public double RuptureMPaMm;
    /// <summary>What's vertical and in phase of an explosion's impulse on her hull (<see cref="HullGirder.Whip"/>) [EST: 0.2 gives Lion 0.2 of a design moment from 20 t of cordite in a second and about 0.6 from her bags' whole rush; to the research, EX5's bubble will test it].</summary>
    public double WhipShare;
    /// <summary>The gas's temperature that lights charges, K [EST: nitrocellulose ignites near 450-470 K].</summary>
    public double IgniteK;
    /// <summary>The solver's step, a scale on its longest and on how far pressures may move in one: for the convergence check.</summary>
    public double StepK;

    public static GasKnobs Of(SimSettings s) => new()
    {
        BeltShare = 1, HeatKeep = s.MagazineHeatKeep, TearShare = s.RupturedShare, PanelMs = s.PanelMs, RuptureMPaMm = s.RuptureMPaMm, StepK = 1, IgniteK = 470, WhipShare = 0.2,
    };
}

/// <summary>What an explosion's gas did (<see cref="GasNetwork"/>): the peak pressure at the source (kPa gauge) and the
/// highest anywhere after the source's own space gave, the propellant burned (kg), the spaces it reached and their gas (m³),
/// what gave: bulkheads, decks, watertight doors, the side, weather deck and bottom plates; the magazines alight, the
/// gunhouses vented and lifted; the first boundary to give and the first of the hull's own plates (s); how long it ran (s)
/// and in how many steps. What it did to the girder is <see cref="HullGirder"/>'s to read.</summary>
public struct GasResult
{
    public double PeakKPa, BurnedKg, VolumeM3, FirstS, HullS, EndS;
    public int Spaces, Holed, Bulkheads, Decks, Doors, Sides, WeatherDecks, Bottoms, Magazines, Vented, Lifted, Steps;
    /// <summary>The most it drove within the hull's period, its energy × its mass, J·kg: what can whip her.</summary>
    public double WhipEM;
    public double OpenM2, HoleM2;
}

/// <summary>The gas of any explosion through a ship (docs/battle/explosions.md EX1): one mass of gas and its energy in every
/// cell it reaches, flowing through what's open, each boundary taking up the difference across it as its panel responds and
/// giving at its own plate, by one rule. A detonation releases its energy in the first step; propellant by its burn law. That's
/// the only difference between them. The state lives here, one per ship, allocated once; a run allocates nothing.</summary>
public sealed class GasNetwork
{
    const int Faces = 4, MaxStows = 8, Cohorts = 32;
    const double DtMin = 1e-4, DtMax = 0.02, GaugeFloor = 10e3, RtAir = 84_000, AirR = 287, SeaRhoG = 1025 * 9.81, StepCellM3 = 10;

    /// <summary>A bench's view: what gave and when. Null in the game.</summary>
    public static Action<string>? Trace;

    readonly ShipClass c;
    readonly int[] canon;             // a pool entry's edge: the lower cell's entry, what the flooding reads
    readonly double[] area, mm;       // an edge's shared area and plate
    // the cells'
    readonly int[] stamp, region;
    readonly double[] v, m, u, p, rt, pStart, vent, peak;
    int n, id;
    // the edges', by canon
    readonly int[] edgeStamp;
    readonly double[] open, opened, felt, tornEdge;
    readonly byte[] fate;
    // the outside faces', cell x 4
    readonly int[] faceStamp;
    readonly double[] faceFelt, faceTorn;
    readonly byte[] faceFate;
    readonly double[] everFace, everEdge;
    // the links a magazine's train adds: from a cell to another or the air (-1), opening at a gauge pressure there
    readonly int[] linkFrom, linkTo, linkMount;
    readonly double[] linkAt, linkArea;
    readonly bool[] linkOpen, ventedMount, liftedMount;
    int links;
    readonly Stow[] stows = new Stow[MaxStows];
    int nStows;
    double burned, peakSource;
    GasKnobs g;
    SimSettings s = null!;
    ShipCondition k = null!;
    ReadOnlyMemory<MountState> mounts;
    FlashDiscipline flash;
    double time, chokeK, whipKg, whipJ, whipPeak;
    GasResult res;

    sealed class Stow
    {
        public int Room, Cell, Cohort;
        public PropellantRow Pr;
        public double Ck, Open, Cased, Flooded, Pkg, TBurn, LitOpen, LitCased, CohortT0, More, EnergyJKg;
        public readonly double[] Cnt = new double[Cohorts], Z = new double[Cohorts];
        public double Burning;
    }

    public GasNetwork(ShipClass cls)
    {
        c = cls;
        int nc = c.Cells.Length, ne = c.NeighbourCount, nm = c.Mounts.Length;
        (stamp, region) = (new int[nc], new int[nc]);
        (v, m, u, p, rt, pStart, vent, peak) = (new double[nc], new double[nc], new double[nc], new double[nc], new double[nc], new double[nc], new double[nc], new double[nc]);
        (canon, area, mm, edgeStamp, open, opened, felt, tornEdge, fate) = (new int[ne], new double[ne], new double[ne], new int[ne], new double[ne], new double[ne], new double[ne], new double[ne], new byte[ne]);
        (faceStamp, faceFelt, faceTorn, faceFate) = (new int[nc * Faces], new double[nc * Faces], new double[nc * Faces], new byte[nc * Faces]);
        (everFace, everEdge) = (new double[nc * Faces], new double[ne]);
        int lm = 2 * nm + 8;
        (linkFrom, linkTo, linkMount, linkAt, linkArea, linkOpen) = (new int[lm], new int[lm], new int[lm], new double[lm], new double[lm], new bool[lm]);
        (ventedMount, liftedMount) = (new bool[nm], new bool[nm]);
        for (int i = 0; i < MaxStows; i++)
            stows[i] = new Stow();
        for (int i = 0; i < nc; i++)
        {
            var nb = c.Cells[i].Neighbours;
            for (int j = nb.Start; j < nb.End; j++)
            {
                ref readonly var e = ref c.Neighbour(j);
                canon[j] = Leaks.Entry(c, i, j);
                area[j] = Blast.Area(c.Cells[i].Box, c.Cells[e.Cell].Box, e.Join);
                mm[j] = Blast.JoinMm(c, e, c.Cells[i].Box, c.Cells[e.Cell].Box);
            }
        }
    }

    double GammaK => s.BlastHeatK;
    double Gm => 1 + s.BlastHeatK;
    double P0 => s.AtmosphereMPa * 1e6;

    /// <summary>w kg of a filler bursting at a point in a space: all its energy in the first step, its afterburn as the
    /// space's air allows (11 §2.1); the near zone's holes in the space's boundaries (11 §4.1), and ventM2 more from the start
    /// (a near-wall burst's own hole).</summary>
    public GasResult Detonate(ShipCondition cond, int cell, P3 at, double w, in ShellTypeRow f, double ventM2, double nearM, double nearHoleM, in GasKnobs knobs, SimSettings settings)
    {
        Begin(cond, knobs, settings);
        Join(cell);
        vent[cell] += ventM2;
        // the space's own: what's open to it is one room
        for (int i = 0; i < n; i++)
            foreach (ref readonly var e in c.Neighbours(c.Cells[region[i]].Neighbours))
                if (e.Join == JoinKind.Open && stamp[e.Cell] != id)
                    Join(e.Cell);
        double gas = 0;
        for (int i = 0; i < n; i++)
            gas += v[region[i]];
        double phi = f.FillerAirM3Kg > 0 ? 1 / (1 + w * f.FillerAirM3Kg / gas) : 0;
        double joules = f.FillerEta * (f.FillerQDet + phi * Math.Max(f.FillerQComb - f.FillerQDet, 0)) * w * 1e6;
        m[cell] += w;
        u[cell] += joules;
        Refresh(cell);
        (whipKg, whipJ, whipPeak) = (w, joules, joules * w);
        Near(cell, at, nearM, nearHoleM);
        Run(cell, 2e-4);
        return Finish(cell);
    }

    /// <summary>A magazine alight: its stow burns by the burn law in its space, the train's ways up open as the pressure
    /// stands, and any magazine its gas reaches lights in turn.</summary>
    public GasResult Burn(ShipCondition cond, int room, ReadOnlyMemory<MountState> mountStates, in GasKnobs knobs, SimSettings settings)
    {
        Begin(cond, knobs, settings);
        mounts = mountStates;
        flash = Magazines.Discipline(cond, settings);
        int cell = Light(room);
        if (cell < 0)
            return res;
        Run(cell, 2e-3);
        return Finish(cell);
    }

    void Begin(ShipCondition cond, in GasKnobs knobs, SimSettings settings)
    {
        (k, g, s) = (cond, knobs, settings);
        double gm = Gm;
        chokeK = Math.Sqrt(gm) * Math.Pow(2 / (gm + 1), (gm + 1) / (2 * (gm - 1)));
        id++;
        (n, links, nStows, burned, time, peakSource) = (0, 0, 0, 0, 0, 0);
        mounts = default;
        res = new GasResult { FirstS = double.NaN, HullS = double.NaN };
        (whipKg, whipJ, whipPeak) = (0, 0, 0);
        Array.Clear(ventedMount);
        Array.Clear(liftedMount);
        Blast.Prepare(c, k);
    }

    /// <summary>A cell joins: its air at the atmosphere's pressure, its vents.</summary>
    void Join(int i)
    {
        stamp[i] = id;
        region[n++] = i;
        v[i] = Blast.GasM3(c, k, i);
        m[i] = P0 * v[i] / RtAir;
        u[i] = P0 * v[i] / GammaK;
        vent[i] = Blast.VentM2(c, k, i, s);
        peak[i] = 0;
        p[i] = pStart[i] = P0;
        rt[i] = RtAir;
    }

    double EdgeOpen(int j)
    {
        int q = canon[j];
        if (edgeStamp[q] != id)
        {
            edgeStamp[q] = id;
            (fate[q], felt[q], opened[q], tornEdge[q]) = (0, 0, 0, 0);
            open[q] = c.Neighbour(j).Join == JoinKind.Open ? area[j] : Math.Min(k.JoinOpenM2[q], area[j]);   // what's torn already stays torn
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
            (open[q], opened[q], fate[q]) = (open[q] + hole, opened[q] + hole, Blast.Fate.Holed);
            res.Holed++;
        }
    }

    /// <summary>The ship's own magazine lit: its stow, and the links of the mounts it feeds. Its largest cell, or -1.</summary>
    int Light(int room)
    {
        if (nStows >= MaxStows)
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
        var span = mounts.Span;
        Magazines.Stow(c, room, span, flash, out double nOpen, out double nCased, out double mass);
        st.Room = room;
        st.Cell = cell;
        st.Pr = charge.Propellant;
        st.Ck = mass / Math.Max(nOpen + nCased, 1);
        (st.Open, st.Cased) = (nOpen, nCased);
        st.Flooded = Math.Max(Magazines.Flooded(c, k, room), k.MagazineWet.Length > room ? k.MagazineWet[room] : 0);
        st.Pkg = s.MagazinePackageR[Math.Min((int)charge.Packaging, s.MagazinePackageR.Length - 1)] * st.Pr.Sensitivity;
        st.TBurn = st.Pr.WebM / Magazines.Rate(101_325, st.Pr);
        st.EnergyJKg = g.HeatKeep * st.Pr.ImpetusJkg / GammaK;
        (st.LitOpen, st.LitCased, st.Cohort, st.CohortT0, st.More, st.Burning) = (0, 0, 0, -1, 0, 0);
        if (nOpen * (1 - st.Flooded) < 1 && nCased * (1 - st.Flooded) >= 1)
        {
            st.LitCased = 1;
            (st.Cnt[0], st.Z[0], st.Cohort, st.CohortT0) = (1, 0, 1, time);
        }
        int first = n;
        if (stamp[cell] != id)
            Join(cell);
        // what's open to it is one room
        for (int i = first; i < n; i++)
            foreach (ref readonly var e in c.Neighbours(c.Cells[region[i]].Neighbours))
                if (e.Join == JoinKind.Open && stamp[e.Cell] != id)
                    Join(e.Cell);
        // the train: the scuttle to its handling room, the way up and out
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
                Link(top, -1, s.MagazineScuttleKPa * 1e3, s.MagazineHoistM2, mt);
        }
        res.Magazines = nStows;
        Trace?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture, "      {0:0.000} s: {1} alight ({2:0} open, {3:0} packed, {4:0} kg each, {5:0} m³ of gas)", time, c.RoomId(room), nOpen, nCased, st.Ck, v[cell]));
        return cell;
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
        while (time < s.MagazineRaceS)
        {
            for (int a = 0; a < n; a++)
            {
                int i = region[a];
                Refresh(i);
                pStart[i] = p[i];
                peak[i] = Math.Max(peak[i], p[i] - P0);
                Ignite(i);
            }
            peakSource = Math.Max(peakSource, p[source] - P0);
            Sources(dt);
            bool changed = Boundaries(dt);
            Links(dt);
            double most = 0, gauge = 0;
            for (int a = 0; a < n; a++)
            {
                int i = region[a];
                double g0 = Math.Max(Math.Abs(pStart[i] - P0), GaugeFloor);
                if (v[i] >= StepCellM3)
                    most = Math.Max(most, Math.Abs(p[i] - pStart[i]) / g0);
                gauge = Math.Max(gauge, p[i] - P0);
            }
            time += dt;
            steps++;
            bool burning = false;
            for (int i = 0; i < nStows; i++)
                burning |= stows[i].Burning > 0 || stows[i].LitOpen < stows[i].Open * (1 - stows[i].Flooded) - 1e-6 || stows[i].LitCased < stows[i].Cased * (1 - stows[i].Flooded) - 1e-6 && stows[i].Burning > 0;
            quiet = changed || most > 0.01 ? 0 : quiet + dt;
            // done when nothing can burn and nothing more can give: no intact door or plate takes less than the door's own
            bool low = s.BlastLoadFactor * gauge < s.DoorMinMPa * 1e6;
            if (!burning && (low || quiet > 1) || burning && low && quiet > 1 && AllLit())
                break;
            dt = changed || most > 0.2 * g.StepK ? Math.Max(dt / 2, DtMin) : most < 0.05 * g.StepK ? Math.Min(dt * 1.25, DtMax * g.StepK) : dt;
        }
        res.Steps = steps;
        res.EndS = time;
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

    /// <summary>The stows' gas: the same burn as the race's (r = β·P^α, cohorts of lit charges, the flame through the stow).</summary>
    void Sources(double dt)
    {
        double released = 0, madeKg = 0;
        for (int t = 0; t < nStows; t++)
        {
            var st = stows[t];
            double pa = p[st.Cell], rate = Magazines.Rate(pa, st.Pr), gen = 0, burning = 0;
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
            double made = gen * dt;
            m[st.Cell] += made;
            u[st.Cell] += made * st.EnergyJKg;
            Refresh(st.Cell);
            burned += made;
            released += made * st.EnergyJKg;
            madeKg += made;

            double openDry = st.Open * (1 - st.Flooded), casedDry = st.Cased * (1 - st.Flooded), d = 0;
            if (openDry - st.LitOpen > 0)
            {
                double dOpen = (openDry - st.LitOpen) * (1 - Math.Exp(-dt / s.MagazineBagSpreadS));
                (st.LitOpen, d) = (st.LitOpen + dOpen, d + dOpen);
            }
            if (casedDry - st.LitCased > 0 && (burning > 0 || st.More > 0))
            {
                double dCased = Math.Min(casedDry - st.LitCased, burning * st.Pkg / st.TBurn * dt * (casedDry - st.LitCased) / Math.Max(casedDry, 1) + st.More);
                (st.LitCased, d, st.More) = (st.LitCased + dCased, d + dCased, 0);
            }
            if (d > 0)
            {
                if (st.Cohort > 0 && (time - st.CohortT0 < 0.1 || st.Cohort == Cohorts))
                {
                    int last = st.Cohort - 1;
                    st.Z[last] = st.Cnt[last] * st.Z[last] / (st.Cnt[last] + d);
                    st.Cnt[last] += d;
                }
                else
                    (st.Cnt[st.Cohort], st.Z[st.Cohort], st.Cohort, st.CohortT0) = (d, 0, st.Cohort + 1, time);
            }
        }
        // the energy within about the hull's period (07 §3.1: a spike of about a second): what comes slower doesn't whip her
        double fade = Math.Exp(-dt);
        (whipKg, whipJ) = (whipKg * fade + madeKg, whipJ * fade + released);
        whipPeak = Math.Max(whipPeak, whipKg * whipJ);
    }

    /// <summary>Every edge and outside face of the cells the gas is in: its load as its panel takes it up, what gives, the
    /// flow through what's open. True when something gave.</summary>
    bool Boundaries(double dt)
    {
        double lag = 1 - Math.Exp(-dt * Math.PI * 1000 / Math.Max(g.PanelMs, 1e-3)), keep = s.BlastLoadFactor;   // a panel's response: Felt's P·min(k, π·t/T) as a lag
        bool changed = false;
        var water = HitQuery.WaterOf(c, k.Attitude);
        for (int a = 0, a0 = n; a < a0; a++)
        {
            int i = region[a];
            var nb = c.Cells[i].Neighbours;
            for (int j = nb.Start; j < nb.End; j++)
            {
                ref readonly var e = ref c.Neighbour(j);
                int o = e.Cell;
                bool inside = stamp[o] == id;
                if (inside && o < i)
                    continue;   // from the lower cell
                double a1 = EdgeOpen(j), po = inside ? p[o] : P0, diff = p[i] - po;
                int q = canon[j];
                if (e.Join != JoinKind.Open && fate[q] != Blast.Fate.Holed)
                {
                    felt[q] += (keep * Math.Abs(diff) / 1e6 - felt[q]) * lag;
                    double rupture = g.RuptureMPaMm * mm[j] * Rivets(c.InternalsJoint), d = 5 * felt[q] / rupture;
                    // what it opens goes up one curve: sprung seams, a crack just past rupture, torn out well past it
                    double wants = Opening(d, c.InternalsJoint) * area[j];
                    if (wants > tornEdge[q])
                    {
                        double more = wants - tornEdge[q];
                        tornEdge[q] = wants;
                        (open[q], opened[q]) = (open[q] + more, opened[q] + more);
                        a1 = open[q];
                    }
                    if (d >= 5 && fate[q] != Blast.Fate.Ruptured)
                    {
                        fate[q] = Blast.Fate.Ruptured;
                        if (e.Join == JoinKind.Deck)
                            res.Decks++;
                        else
                            res.Bulkheads++;
                        Gave(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} to {2} ({3:0} mm) at {4:0} kPa felt", e.Join == JoinKind.Deck ? "deck" : "bulkhead", c.CellId(i), c.CellId(o), mm[j], felt[q] * 1000), false, i);
                        changed = true;
                    }
                    else if (fate[q] == Blast.Fate.None && Leaks.HasDoor(c, e, i) && felt[q] >= Math.Max(s.DoorMinMPa, s.DoorShare * rupture))
                    {
                        fate[q] = Blast.Fate.Door;
                        (open[q], opened[q]) = (open[q] + s.DoorM2, opened[q] + s.DoorM2);
                        a1 = open[q];
                        res.Doors++;
                        Gave(string.Format(System.Globalization.CultureInfo.InvariantCulture, "door {0} to {1} at {2:0} kPa felt", c.CellId(i), c.CellId(o), felt[q] * 1000), false, i);
                        changed = true;
                    }
                }
                if (a1 <= 0)
                    continue;
                if (!inside)
                {
                    Join(o);   // it steps from the next step on

                }
                Flow(i, o, a1, dt);
            }
            for (int f = 0; f < Faces; f++)
            {
                double fa = Blast.Outside(c, k, i, f, out double plate, out var point);
                if (!(fa > 0))
                    continue;
                int x = i * Faces + f;
                if (faceStamp[x] != id)
                {
                    faceStamp[x] = id;
                    (faceFate[x], faceFelt[x], faceTorn[x]) = (0, 0, 0);
                }
                // the sea behind what's under it presses back; a belt holds as the rung says
                double wl = f < 2 ? water.At(point.X, point.Y) : f == 3 ? water.At(point.X, point.Y) : double.NegativeInfinity;
                double back = f == 2 ? 0 : Math.Max(wl - point.Z, 0) * SeaRhoG;
                faceFelt[x] += (keep * Math.Max(p[i] - P0 - back, 0) / 1e6 - faceFelt[x]) * lag;
                double held = plate;
                if (f < 2 && c.Cells[i].BeltMm > 0)
                    held -= (1 - g.BeltShare) * c.MaterialOf(c.Cells[i].BeltMaterial).SteelMm(c.Cells[i].BeltMm);
                double dFace = 5 * faceFelt[x] / (g.RuptureMPaMm * Math.Max(held, 1) * Rivets(c.ShellJoint));
                double hole = Opening(dFace, c.ShellJoint) * fa;
                if (hole <= faceTorn[x])
                    continue;
                // a tear under the sea lets the sea in, not the gas out (07 §2.4)
                var box = c.Cells[i].Box;
                double dry = f == 2 ? 1 : f == 3 ? 0 : Math.Clamp((box.Top - wl) / Math.Max(box.Top - box.Base, 0.1), 0, 1);
                vent[i] += (hole - faceTorn[x]) * dry;
                faceTorn[x] = hole;
                if (dFace < 5 || faceFate[x] == Blast.Fate.Ruptured)
                    continue;
                faceFate[x] = Blast.Fate.Ruptured;
                if (f == 2)
                    res.WeatherDecks++;
                else if (f == 3)
                    res.Bottoms++;
                else
                    res.Sides++;
                Gave(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} of {1} ({2:0} mm held) at {3:0} kPa felt, {4:0.0} m² so far, {5:0%} dry", f == 2 ? "weather deck" : f == 3 ? "bottom" : "side", c.CellId(i), held, faceFelt[x] * 1000, hole, dry), true, i);
                changed = true;
            }
            if (vent[i] > 0)
                Flow(i, -1, vent[i], dt);
        }
        return changed;
    }

    /// <summary>Gas hot enough to light charges (rt at <see cref="GasKnobs.IgniteK"/>) in a magazine's space lights it, as
    /// the air pushed ahead of the hot gas doesn't.</summary>
    void Ignite(int cell)
    {
        if (nStows == 0 || rt[cell] < g.IgniteK * AirR)
            return;
        foreach (int room in c.RoomsIn(cell))
            if (c.Rooms[room].Kind == RoomKind.Magazine)
                Light(room);
    }

    void Gave(string what, bool hull, int cell)
    {
        if (double.IsNaN(res.FirstS))
            res.FirstS = time;
        if (hull && double.IsNaN(res.HullS))
            res.HullS = time;
        Trace?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture, "      {0:0.000} s: {1}", time, what));
        // along the passages: the group's magazines light when a boundary of one alight gives (07 §2.4)
        for (int t = 0; t < nStows; t++)
        {
            int group = c.PassageGroup(stows[t].Room);
            if (group < 0 || stows[t].Cell != cell)
                continue;
            for (int rm = 0; rm < c.Rooms.Length; rm++)
                if (c.Rooms[rm].Kind == RoomKind.Magazine && c.PassageGroup(rm) == group)
                    Light(rm);
        }
    }

    double Rivets(Joint j) => j == Joint.Riveted ? s.RivetedK : 1;

    /// <summary>The share of a boundary a load of D has opened: damage/17's r(D), log-linear from D 1 on, run on past rupture
    /// (D 5, about 1% open) to what a torn-out boundary leaves, rather than jumping there: a plate just past its limit
    /// has cracked, and one far past it has gone. Under D 1 its seams only weep, for the flooding afterwards.</summary>
    double Opening(double d, Joint joint)
    {
        if (!(d >= 1))
            return 0;
        double ratio = s.LeakR1 * Math.Pow(s.LeakR5 / s.LeakR1, (d - 1) / 4) * 1e-4;
        return Math.Min(joint == Joint.Welded && d < 5 ? ratio * s.LeakWeldedK : ratio, g.TearShare);
    }

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
                }
                Trace?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture, "      {0:0.000} s: the way from {1} open at {2:0} kPa, {3:0.00} m²", time, c.CellId(from), (p[from] - P0) / 1000, linkArea[l]));
            }
            int to = linkTo[l];
            if (to >= 0 && stamp[to] != id)
            {
                Join(to);
            }
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
        double mdot = Math.Min(s.MagazineVentCd * chokeK * a * pa / Math.Sqrt(r), s.MagazineVentCd * a * Math.Sqrt(2 * rho * diff));
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
        }
    }

    GasResult Finish(int source)
    {
        res.PeakKPa = peakSource / 1000;
        res.BurnedKg = burned;
        res.Spaces = n;
        double vol = 0;
        for (int a = 0; a < n; a++)
            vol += v[region[a]];
        res.VolumeM3 = vol;
        res.WhipEM = whipPeak;
        Gone();
        return res;
    }

    /// <summary>The share of each boundary's tension gone on this ship so far, across its runs: by edge and by cell x 4 + face (<see cref="HullGirder"/>'s).</summary>
    public ReadOnlySpan<double> EdgesLost => everEdge;
    public ReadOnlySpan<double> FacesLost => everFace;

    /// <summary>A fresh ship: nothing gone.</summary>
    public void Forget()
    {
        Array.Clear(everEdge);
        Array.Clear(everFace);
    }

    /// <summary>What this run took out of the boundaries, added to the ship's. Past rupture a boundary's opening is a crack, its
    /// area going as its length squared, so the share of the plate it has cut goes as the root of how far open it is;
    /// short of rupture its seams leak but it carries its load. No step: one curve from the first crack to torn out.</summary>
    void Gone()
    {
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            var nb = c.Cells[i].Neighbours;
            for (int j = nb.Start; j < nb.End; j++)
            {
                int q = canon[j];
                if (edgeStamp[q] == id && fate[q] == Blast.Fate.Ruptured && area[j] > 0)
                    everEdge[q] = Math.Max(everEdge[q], Math.Sqrt(Math.Min(tornEdge[q] / (g.TearShare * area[j]), 1)));
                else if (edgeStamp[q] == id && fate[q] == Blast.Fate.Holed && area[j] > 0)
                    everEdge[q] = Math.Max(everEdge[q], Math.Sqrt(Math.Min(open[q] / area[j], 1)));   // the near zone's hole
            }
            for (int f = 0; f < Faces; f++)
            {
                int x = i * Faces + f;
                double fa = Blast.Outside(c, k, i, f, out _, out _);
                if (fa > 0 && faceStamp[x] == id && faceFate[x] == Blast.Fate.Ruptured)
                    everFace[x] = Math.Max(everFace[x], Math.Sqrt(Math.Min(faceTorn[x] / (g.TearShare * fa), 1)));
            }
        }
    }

    /// <summary>The highest gauge pressure a cell saw, kPa; its share of the gas reached.</summary>
    public double PeakKPaOf(int cell) => stamp[cell] == id ? peak[cell] / 1000 : 0;

    /// <summary>The cells reached, by the order they joined.</summary>
    public ReadOnlySpan<int> Reached() => region.AsSpan(0, n);

    /// <summary>What this run opened, onto the ship's condition: between spaces (<see cref="ShipCondition.JoinOpenM2"/>, never
    /// past a boundary's own area), and to the sea or air (<see cref="ShipCondition.Hole"/>). A salvo's next shell finds them.</summary>
    public void Commit()
    {
        for (int a = 0; a < n; a++)
        {
            int i = region[a];
            var nb = c.Cells[i].Neighbours;
            for (int j = nb.Start; j < nb.End; j++)
            {
                int q = canon[j];
                if (edgeStamp[q] != id || !(opened[q] > 0))
                    continue;
                k.JoinOpenM2[q] = Math.Min(k.JoinOpenM2[q] + opened[q], area[j]);
                res.OpenM2 += opened[q];
                opened[q] = 0;
            }
            for (int f = 0; f < Faces; f++)
            {
                int x = i * Faces + f;
                if (faceStamp[x] != id || !(faceTorn[x] > 0))
                    continue;
                Blast.Outside(c, k, i, f, out _, out var point);
                k.Hole(i, faceTorn[x], point);
                res.HoleM2 += faceTorn[x];
                faceTorn[x] = 0;
            }
        }
    }
}
