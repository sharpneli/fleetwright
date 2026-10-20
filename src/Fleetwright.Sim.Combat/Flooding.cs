using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How a ship went (or is going) down: its magazine, foundering (its buoyancy gone) or capsizing (its
/// stability gone).</summary>
public enum LossKind : byte
{
    None,
    Magazine,
    Foundered,
    Capsized,
    /// <summary>Its back broken by an underwater explosion (docs/battle/fighting.md 4.6): a small ship's hull girder
    /// cut through amidships.</summary>
    BrokeInTwo,
}

/// <summary>An edge of the cell graph, once (A &lt; B), as water sees it: what joins them, the height its opening
/// starts at (ship frame: a deck's height for cells one over the other, else the higher floor of two side by side),
/// which is above (-1 side by side), and its entry in the class's Neighbours pool (from A: where a burst's openings on it
/// are kept, <see cref="ShipCondition.JoinOpenM2"/>).</summary>
public readonly record struct FloodEdge(int A, int B, JoinKind Join, double Z, int Upper, int Pool);

/// <summary>A class's cells as water sees them (docs/battle/fighting.md 4.3b), worked out once per class: each cell's
/// capacity (its volume times its permeability), the area of its water's surface (the capacity over its height), its
/// centre, whether it's open to the weather above (nothing over it: its ceiling is the weather deck) and that deck's
/// area; the graph's edges once each; the reserve buoyancy (the watertight hull's volume over the displacement), the
/// deck's height at the centre of flotation, and the complement. Not settings: those are read at each step.</summary>
public sealed class FloodModel
{
    public readonly double[] Capacity, Area, Cx, Cy, DeckArea;
    public readonly bool[] Weather;
    public readonly FloodEdge[] Edges;
    /// <summary>Each edge's face (m², the area the two cells share) and the height of water from its lowest point to its
    /// top that it's built for (m: a bulkhead's own top, a deck's section's top, at least the waterline; infinite open):
    /// its rating is <see cref="SimSettings.FloodRatingK"/> times this (docs/battle/damage-control.md DC1).</summary>
    public readonly double[] FaceM2, BuiltM;
    /// <summary>The cells DC Central may counterflood (docs/battle/damage-control.md DC5): tanks, voids, the double bottom
    /// and wing bunkers below the waterline, off the centreline (for the list) or toward the ends (for the trim).</summary>
    public readonly int[] CounterTanks;
    public readonly double ReserveT, DisplacementT, DeckAtLcf, Complement;

    /// <summary>Tonnes a cubic metre of sea water.</summary>
    public const double SeaT = Weight.Seawater;

    public FloodModel(ShipClass c)
    {
        var cells = c.Cells;
        int n = cells.Length;
        (Capacity, Area, Cx, Cy, DeckArea, Weather) = (new double[n], new double[n], new double[n], new double[n], new double[n], new bool[n]);
        var edges = new List<FloodEdge>();
        double volume = 0, deck = double.NegativeInfinity, lcf = c.Hydrostatics.Lcf;
        for (int i = 0; i < n; i++)
        {
            ref readonly var x = ref cells[i];
            var b = x.Box;
            double h = Math.Max(b.Top - b.Base, 0.1);
            Capacity[i] = x.VolumeM3 * Math.Clamp(x.Permeability, 0, 1);
            Area[i] = Math.Max(Capacity[i] / h, 0.01);
            (Cx[i], Cy[i]) = ((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2);
            DeckArea[i] = (b.X1 - b.X0) * (b.Y1 - b.Y0);
            volume += x.VolumeM3;
            Weather[i] = true;
            for (int j = x.Neighbours.Start; j < x.Neighbours.End; j++)
            {
                ref readonly var e = ref c.Neighbour(j);
                var o = cells[e.Cell].Box;
                if (o.Base >= b.Top - 0.05)
                    Weather[i] = false;   // a cell over it
                if (e.Cell <= i)
                    continue;
                int upper = o.Base >= b.Top - 0.05 ? e.Cell : b.Base >= o.Top - 0.05 ? i : -1;
                double z = upper == e.Cell ? b.Top : upper == i ? o.Top : Math.Max(b.Base, o.Base);
                edges.Add(new FloodEdge(i, e.Cell, e.Join, z, upper, j));
            }
            if (b.X0 <= lcf && b.X1 >= lcf)
                deck = Math.Max(deck, b.Top);
        }
        Edges = [.. edges];
        // each section's top: what a deck in it is built to hold water up to
        var top = new double[Math.Max(c.Sections.Length, 1)];
        Array.Fill(top, double.NegativeInfinity);
        foreach (ref readonly var x in cells)
        {
            int sec = Math.Clamp(x.Section, 0, top.Length - 1);
            top[sec] = Math.Max(top[sec], x.Box.Top);
        }
        (FaceM2, BuiltM) = (new double[Edges.Length], new double[Edges.Length]);
        double wl = c.Vertical.Waterline;
        for (int i = 0; i < Edges.Length; i++)
        {
            ref readonly var e = ref Edges[i];
            ref readonly var nb = ref c.Neighbour(e.Pool);
            FaceM2[i] = Blast.Area(cells[e.A].Box, cells[e.B].Box, e.Join);
            double t = e.Join switch
            {
                JoinKind.Bulkhead when nb.Through >= 0 => c.Bulkheads[nb.Through].Top,
                JoinKind.Open => double.PositiveInfinity,
                _ => Math.Max(top[Math.Clamp(cells[e.A].Section, 0, top.Length - 1)], top[Math.Clamp(cells[e.B].Section, 0, top.Length - 1)]),
            };
            BuiltM[i] = double.IsPositiveInfinity(t) ? t : Math.Max(Math.Max(t, wl) - e.Z, 0.5);
        }
        var tanks = new List<int>();
        for (int i = 0; i < n; i++)
        {
            var kind = c.Rooms[cells[i].Room].Kind;
            if (!cells[i].BelowWaterline || kind is not (RoomKind.DoubleBottom or RoomKind.Tds or RoomKind.FuelTank or RoomKind.Bunker))
                continue;
            if (Math.Abs(Cy[i]) >= 1 || Math.Abs(Cx[i] - lcf) >= 0.25 * c.Length)
                tanks.Add(i);
        }
        CounterTanks = [.. tanks];
        DisplacementT = c.Hydrostatics.DisplacementT;
        ReserveT = Math.Max(volume * SeaT - DisplacementT, 0);
        DeckAtLcf = double.IsNegativeInfinity(deck) ? 0 : deck;
        double crew = 0;
        foreach (ref readonly var comp in c.Components)
            crew += comp.BattleCrew;
        foreach (ref readonly var x in cells)
            crew += x.BattleCrew;
        Complement = Math.Max(crew, 1);
    }

    /// <summary>The cell a point (ship frame) is in, the smallest box that holds it; -1 for none.</summary>
    public static int CellAt(ShipClass c, double x, double y, double z)
    {
        int best = -1;
        double size = double.MaxValue;
        var cells = c.Cells;
        for (int i = 0; i < cells.Length; i++)
        {
            var b = cells[i].Box;
            double v = (b.X1 - b.X0) * (b.Y1 - b.Y0) * (b.Top - b.Base);
            if (b.Contains(x, y, z) && v < size)
                (best, size) = (i, v);
        }
        return best;
    }

    /// <summary>A point on the hull's side (a hole's: x along the ship, z its height, ship frame; to starboard or port)
    /// and the cell behind it; -1 when there's no hull there.</summary>
    public static (int Cell, double Y) SideCell(ShipClass c, double x, double z, bool starboard)
    {
        double y = c.Hull.HalfWidth(x, z);
        if (!(y > 0.5))
            return (-1, 0);
        y = starboard ? y : -y;
        return (CellAt(c, x, y - Math.CopySign(0.3, y), z), y);
    }

    /// <summary>A cell's water's surface height, ship frame (its floor when dry).</summary>
    public double Level(ShipClass c, ShipCondition k, int i)
    {
        var b = c.Cells[i].Box;
        return b.Base + Math.Min(k.WaterM3[i] / Math.Max(Capacity[i], 1e-9), 1) * (b.Top - b.Base);
    }

    public bool Full(ShipCondition k, int i) => k.WaterM3[i] >= Capacity[i] * 0.999;
}

/// <summary>What a flooding step did: the water that came in from the sea and was pumped out, m³, and whether a room
/// was flooded out of action (what the ship can do must be worked out again).</summary>
public struct FloodStep
{
    public double InM3, OutM3, PumpedM3;
    public bool RoomsFlooded;
    /// <summary>Boundaries that gave way under the head this step (docs/battle/damage-control.md DC1).</summary>
    public int Collapsed;
}

/// <summary>FLOODING v0 (docs/battle/fighting.md 4.3b; damage-model-research.md §3.5, §3.6, §8.4): deliberately simple,
/// so the buoyancy model decides when a ship goes and deepening it (4.5) changes when ships go without changing anything
/// else.
/// <list type="bullet">
/// <item><b>Inflow</b> through a cell's holes: Cd·A·√(2g·head), the head from the sea outside down to the hole's lower
/// edge or the water inside, up to the cell's capacity; a cell open to the weather whose deck has gone under takes the
/// sea through its hatches and vents (<see cref="SimSettings.FloodWeatherShare"/> of the deck).</item>
/// <item><b>Spread</b> over the cell graph, by the same law from the higher water to the lower: freely across open
/// joins (one room), slowly through intact bulkheads and decks (their glands and pipes), fast through ones a burst tore
/// and through a deck that was over the waterline and has gone under the sea outside (the Lützow rule: the spaces over
/// it were never made tight).</item>
/// <item><b>Pumps</b> keep the leaks, not the holes: with steam they clear water from cells with no hole of their own,
/// at a rate by displacement.</item>
/// <item><b>Settling</b> is linear from the full-load hydrostatics: sinkage from TPC, trim from the moment over MCT,
/// heel from the off-centre moment by the wall-sided formula on GM (a loll when GM is gone), GM corrected for the water's
/// height and its free surface (damage-model-research.md §8.4: wide, partly flooded flats are the killers).</item>
/// </list>
/// Pure and allocation-free; heights compared are heights over the sea outside, so water finds its level in a trimmed,
/// heeled ship.</summary>
public static class Flooding
{
    const double G = 9.81;

    /// <summary>A step for the tools and tests that don't move the ship: stopped, its draws from a fixed stream.</summary>
    public static FloodStep Step(ShipClass c, FloodModel f, ShipCondition k, double dt, double pumpShare, SimSettings s)
    {
        var r = RandomStream.FromSeed(0xF100D);
        return Step(c, f, k, dt, pumpShare, 0, s, ref r);
    }

    /// <summary>One step of dt seconds at a speed through the water (m/s, ahead over 0): the sea in, the water across the
    /// graph (the leaks with structure, docs/battle/damage-control.md DC1: seams opening with the head and working open,
    /// loosened closures, open watertight doors, boundaries giving way past their rating), the pumps; then the rooms
    /// flooded out of action. The attitude is the one the step starts from (<see cref="Settle"/> after it).</summary>
    public static FloodStep Step(ShipClass c, FloodModel f, ShipCondition k, double dt, double pumpShare, double speedMs, SimSettings s,
        ref RandomStream rng)
    {
        var res = new FloodStep();
        var cells = c.Cells;
        var water = HitQuery.WaterOf(c, k.Attitude);
        double cd = s.FloodCd;
        var w = k.WaterM3;

        // the sea in: through holes, and over a weather deck gone under
        Array.Clear(k.CellInflow);
        Array.Clear(k.JoinFlow);
        for (int i = 0; i < cells.Length; i++)
        {
            double room = f.Capacity[i] - w[i];
            if (room <= 0)
                continue;
            double inside = f.Level(c, k, i), q = 0;
            if (k.BreachM2[i] > 0)
            {
                double bottom = k.BreachZ[i] - Math.Sqrt(k.BreachM2[i] / Math.PI);   // the sea comes in over the hole's lower edge
                double head = water.At(f.Cx[i], k.BreachY[i]) + Ram(c, f.Cx[i], speedMs, s) - Math.Max(bottom, inside);
                if (head > 0)
                    q += cd * k.BreachM2[i] * Math.Sqrt(2 * G * head);
            }
            if (f.Weather[i])
            {
                double head = water.At(f.Cx[i], f.Cy[i]) - cells[i].Box.Top;
                if (head > 0)
                    q += cd * s.FloodWeatherShare * f.DeckArea[i] * Math.Sqrt(2 * G * head);
            }
            double v = Math.Min(q * dt, room);
            k.CellInflow[i] = v / dt;
            w[i] += v;
            res.InM3 += v;
        }

        // and out: water standing over a hole's lower edge, with the sea outside lower (and the ram head at it, under way),
        // runs out through it (a fire's hose water high in the ship, a cell the ship's righting lifted; docs/battle/fire.md B3)
        for (int i = 0; i < cells.Length; i++)
        {
            if (w[i] <= 0 || k.BreachM2[i] <= 0)
                continue;
            double bottom = k.BreachZ[i] - Math.Sqrt(k.BreachM2[i] / Math.PI);
            double floor = Math.Max(bottom, water.At(f.Cx[i], k.BreachY[i]) + Ram(c, f.Cx[i], speedMs, s)), head = f.Level(c, k, i) - floor;
            if (head <= 0)
                continue;
            double v = Math.Min(Math.Min(cd * k.BreachM2[i] * Math.Sqrt(2 * G * head) * dt, head * f.Area[i]), w[i]);
            w[i] -= v;
            res.OutM3 += v;
        }

        // across the graph: from the higher water (over the sea outside) to the lower, down to the opening between
        var edges = f.Edges;
        double fast = 1 + Math.Abs(speedMs) / (s.FloodWorkKn * Kn);
        double work = s.FloodWorkPerHour / 3600 * dt * fast * fast * (1 + s.FloodWorkSea);
        for (int ei = 0; ei < edges.Length; ei++)
        {
            ref readonly var e = ref edges[ei];
            int a = e.A, b = e.B, p = e.Pool;
            if (w[a] <= 0 && w[b] <= 0)
                continue;
            double sa = water.At(f.Cx[a], f.Cy[a]), sb = water.At(f.Cx[b], f.Cy[b]), so = (sa + sb) / 2;
            double ha = Head(c, f, k, a, sa, speedMs, s), hb = Head(c, f, k, b, sb, speedMs, s), open = e.Z - so;
            int hi = ha >= hb ? a : b, lo = hi == a ? b : a;
            double hh = Math.Max(ha, hb), hl = Math.Min(ha, hb);
            double drive = hh - Math.Max(hl, open);
            if (drive <= 0 || w[hi] <= 0)
                continue;
            var marks = k.JoinMarks[p];
            double rating = Rating(f, k, ei, p, s);
            if (drive > rating && (marks & JoinMark.Collapsed) == 0)
            {
                // past its rating it gives way: a share of it open (11's ruptured)
                k.JoinMarks[p] = marks |= JoinMark.Collapsed;
                k.JoinOpenM2[p] += s.RupturedShare * f.FaceM2[ei];
                res.Collapsed++;
            }
            double area = e.Join switch
            {
                JoinKind.Open => s.FloodOpenM2,
                _ when k.CellOpened[a] && k.CellOpened[b] => s.FloodTornM2,
                JoinKind.Deck when open < 0 && e.Z > c.Vertical.Waterline => s.FloodSunkDeckM2,   // over the waterline once, under the sea now
                JoinKind.Deck => s.FloodLeakDeckM2 * c.Drainage.Tightness,   // her glands and penetrations (DC4)
                _ => s.FloodLeakBulkheadM2 * c.Drainage.Tightness,
            } + k.JoinOpenM2[p];   // and what a burst's gas opened there (E5), or the head broke
            if ((marks & JoinMark.DoorOpen) != 0)
                area += s.DoorM2;
            if (k.JoinSeamM2[p] > 0)
            {
                // its sprung seams, opened by the head and worked open over time (17 §5)
                double load = Math.Min(drive / rating, 1);
                area += k.JoinSeamM2[p] * Math.Max(k.JoinWork[p], 1) * ((marks & JoinMark.Caulked) != 0 ? s.CaulkSeamShare : 1) * (1 + s.FloodSeamBeta * load);
                if (k.JoinWork[p] < s.FloodWorkMax &&
                    (k.JoinWorkAcc[p] += work * load * ((marks & JoinMark.Shored) != 0 ? 0.5 : 1)) >= k.JoinWorkAt[p])
                {
                    k.JoinWork[p] = Math.Min(k.JoinWork[p] * s.FloodWorkStep, s.FloodWorkMax);
                    k.JoinWorkAcc[p] = 0;
                    k.JoinWorkAt[p] = -Math.Log(1 - rng.NextDouble());
                }
            }
            if (k.JoinClosureM2[p] > 0)
                area += k.JoinClosureM2[p] * ((marks & JoinMark.Redogged) != 0 ? s.RedogShare : 1);
            double v = cd * area * Math.Sqrt(2 * G * drive) * dt;
            // no more than lies over the opening, than the lower has room for, and than levels them
            double over = f.Full(k, hi) ? w[hi] : Math.Min(Math.Max(f.Level(c, k, hi) - e.Z, 0) * f.Area[hi], w[hi]);
            double level = drive * f.Area[a] * f.Area[b] / (f.Area[a] + f.Area[b]);
            v = Math.Min(Math.Min(v, over), Math.Min(f.Capacity[lo] - w[lo], f.Full(k, lo) ? 0 : level));
            if (v <= 0)
                continue;
            w[hi] -= v;
            w[lo] += v;
            k.JoinFlow[p] = (hi == a ? v : -v) / dt;
        }

        // the drainage pumps (the design's, on her power: DC4): first the cells with no hole of their own (the leaks), then,
        // where a damage-control organisation lines the valves up to them, the holed ones whose hole they beat at its full
        // head (a plugged or patched hole); never a hole they can't
        double pump = c.Drainage.PumpsTph / FloodModel.SeaT / 3600 * pumpShare * dt;
        int passes = k.DamageControl.Name is null ? 1 : 2;
        for (int pass = 0; pass < passes && pump > 0; pass++)
            for (int i = 0; i < cells.Length && pump > 0; i++)
            {
                if (w[i] <= 0 || (k.BreachM2[i] > 0) != (pass == 1) || k.Counterflooded[i])
                    continue;
                if (pass == 1 && FullInflow(c, f, k, water, i, cd) * dt >= pump)
                    continue;
                double v = Math.Min(w[i], pump);
                w[i] -= v;
                pump -= v;
                res.PumpedM3 += v;
            }
        // the portable pumps the parties rigged (no power: DC4), back to the store once their space is dry
        if (k.PumpsRigged > 0)
            for (int i = 0; i < cells.Length; i++)
            {
                if (k.PumpsIn[i] == 0)
                    continue;
                double v = Math.Min(w[i], k.PumpsIn[i] * c.Drainage.PortableTph / FloodModel.SeaT / 3600 * dt);
                w[i] -= v;
                res.PumpedM3 += v;
                if (w[i] < 1e-6)
                    (k.PumpsRigged, k.PumpsIn[i]) = (k.PumpsRigged - k.PumpsIn[i], 0);
            }
        for (int i = 0; i < cells.Length; i++)
            if (w[i] < 1e-9)
                w[i] = 0;

        // rooms flooded out of action
        var rooms = c.Rooms;
        for (int r = 0; r < rooms.Length; r++)
        {
            int node = c.RoomNode(r);
            if (k.State[node] >= PartState.Disabled)
                continue;
            double held = 0, cap = 0;
            foreach (int i in c.Links(rooms[r].Cells))
                (held, cap) = (held + w[i], cap + f.Capacity[i]);
            if (cap > 0 && held >= s.RoomFloodedShare * cap)
            {
                bool drowned = k.Worsen(node, PartState.Disabled);
                res.RoomsFlooded |= drowned;
                k.Drowned[node] |= drowned;   // to come back once pumped down (DC7)
            }
        }
        return res;
    }

    /// <summary>The water's pressure in a cell as a height over the sea outside: its surface while there's room; full,
    /// the sea's own level if it's holed under it, else its ceiling.</summary>
    static double Head(ShipClass c, FloodModel f, ShipCondition k, int i, double sea, double speedMs, SimSettings s)
    {
        double level = f.Level(c, k, i), top = c.Cells[i].Box.Top;
        bool holed = k.BreachM2[i] > 0 && k.BreachZ[i] < sea;
        // a holed space with its water at its ceiling is the sea's: its leaks into its neighbours drain it a little each
        // step and the hole fills it again, so it presses as a full one
        if (!f.Full(k, i) && !(holed && level >= top - PressedM))
            return level - sea;
        return holed ? Math.Max(top - sea, s.FloodRamLoad * Ram(c, f.Cx[i], speedMs, s)) : top - sea;
    }

    /// <summary>How near its ceiling a holed space's water is to press as a full one, m.</summary>
    const double PressedM = 0.1;

    const double Kn = 0.514444;

    /// <summary>What a cell's holes let in at their full head (the cell empty), m³/s: what a pump must beat to keep it dry.</summary>
    public static double FullInflow(ShipClass c, FloodModel f, ShipCondition k, in Waterplane water, int i, double cd)
    {
        if (k.BreachM2[i] <= 0)
            return 0;
        double bottom = k.BreachZ[i] - Math.Sqrt(k.BreachM2[i] / Math.PI);
        double head = water.At(f.Cx[i], k.BreachY[i]) - Math.Max(bottom, c.Cells[i].Box.Base);
        return head > 0 ? cd * k.BreachM2[i] * Math.Sqrt(2 * G * head) : 0;
    }

    /// <summary>The ram head at x along the ship under way ahead, m (09 §2.2): k·v²/2g, k <see cref="SimSettings.FloodRamK"/>
    /// at the stem, falling to 0 at <see cref="SimSettings.FloodRamReach"/> of her length aft of it; none astern.</summary>
    public static double Ram(ShipClass c, double x, double speedMs, SimSettings s)
    {
        if (!(speedMs > 0))
            return 0;
        double reach = s.FloodRamReach * c.Length, share = Math.Clamp((x - (c.Hull.X1 - reach)) / reach, 0, 1);
        return s.FloodRamK * share * speedMs * speedMs / (2 * G);
    }

    /// <summary>An edge's load now at rest (the head across it, m, as the step drives water through it) and the share of the
    /// ram head that adds to it under way (its pressed side's: half the ram head's k at that x; 0 when neither side is a
    /// holed space at its ceiling): the safe speed's (DC6).</summary>
    public static (double Static, double RamK) EdgeLoad(ShipClass c, FloodModel f, ShipCondition k, int ei, SimSettings s)
    {
        ref readonly var e = ref f.Edges[ei];
        var water = HitQuery.WaterOf(c, k.Attitude);
        int a = e.A, b = e.B;
        double sa = water.At(f.Cx[a], f.Cy[a]), sb = water.At(f.Cx[b], f.Cy[b]), so = (sa + sb) / 2;
        double ha = Head(c, f, k, a, sa, 0, s), hb = Head(c, f, k, b, sb, 0, s);
        int hi = ha >= hb ? a : b;
        double drive = Math.Max(ha, hb) - Math.Max(Math.Min(ha, hb), e.Z - so);
        if (drive <= 0 && Math.Max(ha, hb) <= 0)
            return (0, 0);
        double level = f.Level(c, k, hi), top = c.Cells[hi].Box.Top;
        bool pressed = k.BreachM2[hi] > 0 && k.BreachZ[hi] < (hi == a ? sa : sb) && (f.Full(k, hi) || level >= top - PressedM);
        double reach = s.FloodRamReach * c.Length, share = Math.Clamp((f.Cx[hi] - (c.Hull.X1 - reach)) / reach, 0, 1);
        return (Math.Max(drive, 0), pressed ? s.FloodRamLoad * s.FloodRamK * share / (2 * G) : 0);
    }

    /// <summary>An edge's rating now, the head across it it takes, m: built for its height of water × <see
    /// cref="SimSettings.FloodRatingK"/>, less for the load that sprung it, more for each set of shores.</summary>
    public static double Rating(FloodModel f, ShipCondition k, int edge, int pool, SimSettings s)
    {
        double r = s.FloodRatingK * f.BuiltM[edge], d = k.JoinD[pool];
        if (d > 0)
            r *= Math.Max(s.FloodRatingSprungMin, 1 - s.FloodRatingPerD * d);
        var m = k.JoinMarks[pool];
        if ((m & JoinMark.Shored) != 0)
            r *= (m & JoinMark.ShoredTwice) != 0 ? s.ShoreRatingK * s.ShoreRatingK : s.ShoreRatingK;
        return r;
    }

    /// <summary>The heel an off-centre moment (t·m over the displacement, m) gives, radians, by the wall-sided formula:
    /// GM·tanφ + BM/2·tan³φ = moment. With GM under 0 the ship lolls to the side the moment is on (starboard with none),
    /// at tanφ = √(-2·GM/BM) with no moment.</summary>
    static double Heel(double gm, double bm, double moment)
    {
        double a = Math.Max(bm, 0.01) / 2, c = Math.Abs(moment);
        if (c == 0 && gm >= 0)
            return 0;
        double lo = 0, hi = 60;   // tan 89°: g(t) = a·t³ + gm·t - c has one root over 0
        for (int i = 0; i < 60; i++)
        {
            double t = 0.5 * (lo + hi);
            if (a * t * t * t + gm * t - c > 0)
                hi = t;
            else
                lo = t;
        }
        return Math.CopySign(Math.Atan(0.5 * (lo + hi)), moment == 0 ? 1 : moment);
    }

    /// <summary>The ship settled to its water (linear, from the full-load hydrostatics): the water aboard (t), the attitude,
    /// GM with the water in (its weight's height and its free surfaces), the heel coming on toward what they give over
    /// <see cref="SimSettings.HeelLagS"/> (water rushing in doesn't flip a ship in a second), and whether that loses it: foundered when the water passes the reserve buoyancy or the sea
    /// closes over the deck at the centre of flotation, capsized past <see cref="SimSettings.CapsizeHeelDeg"/> (a moment, or a
    /// loll with its GM gone).</summary>
    public static LossKind Settle(ShipClass c, FloodModel f, ShipCondition k, SimSettings s, double dt)
    {
        var h = c.Hydrostatics;
        double wt = 0, mx = 0, my = 0, mz = 0, fs = 0;
        var cells = c.Cells;
        for (int i = 0; i < cells.Length; i++)
        {
            double v = k.WaterM3[i];
            if (v <= 0)
                continue;
            double t = v * FloodModel.SeaT, fill = Math.Min(v / Math.Max(f.Capacity[i], 1e-9), 1);
            var b = cells[i].Box;
            wt += t;
            mx += t * (f.Cx[i] - h.Lcf);
            my += t * f.Cy[i];
            mz += t * (b.Base + 0.5 * fill * (b.Top - b.Base));
            // its free surface: the water's plane's inertia about the centreline (the cell's mean breadth), less in a
            // cell nearly dry or nearly full, where the water can't run across as the ship heels
            double l = Math.Max(b.X1 - b.X0, 0.1), breadth = cells[i].VolumeM3 / (l * Math.Max(b.Top - b.Base, 0.1));
            double pocket = Math.Min(1, Math.Min(fill, 1 - fill) / s.FreeSurfacePocket);
            fs += pocket * cells[i].Permeability * l * breadth * breadth * breadth / 12;
        }
        k.WaterT = wt;
        if (wt <= 0)
        {
            (k.Attitude, k.GmM) = (Attitude.Upright, h.GmT);
            return LossKind.None;
        }
        double d = h.DisplacementT, d1 = d + wt;
        double sink = Flotation.SinkageM(wt, h.TpcT), trim = Flotation.TrimRad(mx, h.MctTm, c.Length);
        double kg1 = (d * h.Kg + mz) / d1;
        k.GmM = h.GmT + h.Kg - kg1 - fs * FloodModel.SeaT / d1;
        double heel = Heel(k.GmM, h.ITM4 * FloodModel.SeaT / d1, my / d1), was = double.DegreesToRadians(k.Attitude.HeelDeg);
        heel = was + (heel - was) * (1 - Math.Exp(-dt / Math.Max(s.HeelLagS, 1e-3)));   // the list comes on over a while
        k.Attitude = new Attitude(sink, double.RadiansToDegrees(trim), double.RadiansToDegrees(heel));
        if (Math.Abs(k.Attitude.HeelDeg) >= s.CapsizeHeelDeg)
            return LossKind.Capsized;
        if (wt >= f.ReserveT || HitQuery.WaterOf(c, k.Attitude).At(h.Lcf, 0) >= f.DeckAtLcf)
            return LossKind.Foundered;
        return LossKind.None;
    }
}
