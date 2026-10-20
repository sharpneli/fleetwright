using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A boundary's closures and penetrations that a load loosens (damage/17 §4's "fittings"; closures for short, as
/// shipgen's fittings are how gear is mounted), by the settings' columns.</summary>
public enum Closure : byte
{
    DoorDogs,
    HatchDogs,
    StuffingTube,
    VentClosure,
    ReachRod,
    ShaftGland,
    PipeFlange,
}

/// <summary>LEAKS WITH STRUCTURE (docs/battle/damage-control.md DC1; damage/17 §7): what a load leaves in a boundary that
/// held. Its seams spring by r(D) (<see cref="Blast.Leak"/>) and its closures loosen, each by its own chance (17 §4); the
/// flooding then opens the seams with the head and the working (<see cref="Flooding"/>). A boundary's closures come from
/// what's on either side until the designer emits them: a watertight door in a main transverse bulkhead above the
/// waterline (the user's rule, 2026-10-17), a hatch in a deck between spaces that aren't tanks, stuffing tubes by the
/// area (more round machinery and magazines), a vent closure between spaces that breathe, a reach rod and a pipe flange
/// in a main bulkhead, a gland for each shaft through both. Its watertight doors left open are drawn once, at her first
/// damage, by her readiness.</summary>
public static class Leaks
{
    const int Kinds = 7;

    /// <summary>The edge's entry the flooding reads (the lower cell's, <see cref="FloodEdge.Pool"/>) for an entry seen
    /// from a cell.</summary>
    public static int Entry(ShipClass c, int from, int poolIndex)
    {
        ref readonly var e = ref c.Neighbour(poolIndex);
        if (from < e.Cell)
            return poolIndex;
        var back = c.Cells[e.Cell].Neighbours;
        for (int j = back.Start; j < back.End; j++)
            if (c.Neighbour(j).Cell == from)
                return j;
        return poolIndex;
    }

    /// <summary>Whether a boundary has a watertight door (the user's default, 2026-10-17, until the designer emits them): a
    /// main transverse bulkhead between two spaces above the waterline, or below it too where the design has doors below
    /// (subdivision.doors_below: merchant hulls by default).</summary>
    public static bool HasDoor(ShipClass c, in Neighbour e, int from) =>
        e.Join == JoinKind.Bulkhead && c.Bulkheads[e.Through].Kind is BulkheadKind.Main &&
        (c.DoorsBelow || !c.Cells[from].BelowWaterline && !c.Cells[e.Cell].BelowWaterline);

    /// <summary>A boundary's closures by kind (17 §4's defaults by what's either side).</summary>
    public static void Count(ShipClass c, int from, int poolIndex, double areaM2, Span<int> n, SimSettings s)
    {
        n.Clear();
        ref readonly var e = ref c.Neighbour(poolIndex);
        if (e.Join == JoinKind.Open)
            return;
        RoomKind a = Kind(c, from), b = Kind(c, e.Cell);
        bool tankA = Tank(a), tankB = Tank(b);
        if (HasDoor(c, e, from))
            n[(int)Closure.DoorDogs] = 1;
        if (e.Join == JoinKind.Deck && !tankA && !tankB)
            n[(int)Closure.HatchDogs] = 1;
        if (!tankA || !tankB)
            n[(int)Closure.StuffingTube] = (int)Math.Round(areaM2 / (Wired(a) || Wired(b) ? s.ClosureTubeM2 : s.ClosureTubeOtherM2));
        if (!tankA && !tankB)
            n[(int)Closure.VentClosure] = 1;
        if (e.Join == JoinKind.Bulkhead && c.Bulkheads[e.Through].Kind is BulkheadKind.Main)
            (n[(int)Closure.ReachRod], n[(int)Closure.PipeFlange]) = (1, 1);
        var ta = c.Links(c.Cells[from].Through);
        var tb = c.Links(c.Cells[e.Cell].Through);
        int glands = 0;
        foreach (int x in ta)
            foreach (int y in tb)
                glands += x == y ? 1 : 0;
        n[(int)Closure.ShaftGland] = glands;
    }

    static RoomKind Kind(ShipClass c, int cell) => c.Rooms[c.Cells[cell].Room].Kind;

    /// <summary>A space nothing passes through and nobody works in: tanks and voids.</summary>
    static bool Tank(RoomKind k) => k is RoomKind.DoubleBottom or RoomKind.Tds or RoomKind.FuelTank or RoomKind.CargoTank;

    /// <summary>A space the ship's cables crowd into.</summary>
    static bool Wired(RoomKind k) => k is RoomKind.BoilerRoom or RoomKind.EngineRoom or RoomKind.Magazine or RoomKind.Handling or
        RoomKind.Steering or RoomKind.TransmittingStation;

    /// <summary>A boundary that held under a load D (11's felt over its deforming pressure, 15's 5·NSF), seen from a cell:
    /// its seams sprung by seamM2 (r(D)'s, drawn by the caller), its closures rolled (a loosened door is jammed too), its D
    /// kept as the highest plus half the lesser (repeat loads loosen more, 17 §7.1), its working started. The loosened
    /// closures' area, m².</summary>
    public static double Sprung(ShipClass c, ShipCondition k, int from, int poolIndex, double seamM2, double d, double areaM2, SimSettings s,
        ref RandomStream r)
    {
        int j = Entry(c, from, poolIndex);
        Span<int> n = stackalloc int[Kinds];
        Count(c, from, poolIndex, areaM2, n, s);
        double fit = 0;
        for (int kind = 0; kind < Kinds; kind++)
        {
            if (n[kind] == 0 || !(d >= s.ClosureStartD[kind]))
                continue;
            double p = Math.Min(s.ClosureCap, s.ClosurePerD[kind] * (d - s.ClosureStartD[kind] + 0.5));
            for (int i = 0; i < n[kind]; i++)
                if (r.NextDouble() < p)
                {
                    double lo = Math.Log(s.ClosureMinCm2[kind]), hi = Math.Log(s.ClosureMaxCm2[kind]);
                    fit += Math.Exp(lo + r.NextDouble() * (hi - lo)) * 1e-4;
                    if (kind == (int)Closure.DoorDogs)
                        k.JoinMarks[j] |= JoinMark.DoorJammed;
                }
        }
        if (seamM2 <= 0 && fit <= 0)
            return 0;
        k.JoinSeamM2[j] += seamM2;
        k.JoinClosureM2[j] += fit;
        k.JoinD[j] = Math.Max(k.JoinD[j], d) + 0.5 * Math.Min(k.JoinD[j], d);
        if (seamM2 > 0 && k.JoinWork[j] == 0)
        {
            k.JoinWork[j] = 1;
            k.JoinWorkAt[j] = -Math.Log(1 - r.NextDouble());
        }
        return fit;
    }

    /// <summary>A tool's (the benches'): every boundary of a cell (and of the cells joined open to it) sprung at a load D,
    /// as the burst of the shell that holed it would: r(D)'s seams drawn, the closures rolled.</summary>
    public static void SpringCell(ShipClass c, ShipCondition k, int cell, double d, SimSettings s, ref RandomStream r)
    {
        var cells = c.Cells;
        Span<int> room = stackalloc int[16];
        int n = 0;
        room[n++] = cell;
        for (int q = 0; q < n; q++)
            foreach (ref readonly var e in c.Neighbours(cells[room[q]].Neighbours))
                if (e.Join == JoinKind.Open && n < room.Length && room[..n].IndexOf(e.Cell) < 0)
                    room[n++] = e.Cell;
        for (int q = 0; q < n; q++)
        {
            int i = room[q];
            for (int j = cells[i].Neighbours.Start; j < cells[i].Neighbours.End; j++)
            {
                ref readonly var e = ref c.Neighbour(j);
                if (e.Join == JoinKind.Open)
                    continue;
                double area = Blast.Area(cells[i].Box, cells[e.Cell].Box, e.Join), seam = Blast.Leak(d, area, c.InternalsJoint, s, ref r);
                Sprung(c, k, i, j, seam, d, area, s, ref r);
            }
        }
    }

    /// <summary>Her watertight doors left open by her readiness, drawn once.</summary>
    public static void DrawDoors(ShipClass c, ShipCondition k, double share, ref RandomStream r)
    {
        k.DoorsDrawn = true;
        if (!(share > 0))
            return;
        var cells = c.Cells;
        for (int i = 0; i < cells.Length; i++)
            for (int j = cells[i].Neighbours.Start; j < cells[i].Neighbours.End; j++)
            {
                ref readonly var e = ref c.Neighbour(j);
                if (e.Cell > i && HasDoor(c, e, i) && r.NextDouble() < share)
                    k.JoinMarks[j] |= JoinMark.DoorOpen;
            }
    }
}
