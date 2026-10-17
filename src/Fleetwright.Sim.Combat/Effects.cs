using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a hit did, for the log and the report: the part it's told by (a node: the part it burst in or was
/// stopped by, else the first it entered; -1 for none), how many parts it wrecked, put out of action or damaged, the
/// crew it hit, the holes it opened to the sea (m²), and whether it jammed a mount, started a fire, set off a magazine
/// or broke the ship's back.</summary>
/// <summary>How a hit set a magazine off: a burst in it, beside it (fragments through a wall or deck), in a turret or
/// its barbette (the handling path down), or an explosion under water within its reach.</summary>
public enum MagazineVia : byte
{
    None,
    In,
    Beside,
    Turret,
    Underwater,
}

public struct EffectsResult
{
    public int Part;
    public int Destroyed, Disabled, Damaged;
    public double CrewLost, BreachM2;
    public bool Jammed, Fire, Magazine;
    /// <summary>The magazine set off (a room of the class), when <see cref="Magazine"/>; else -1.</summary>
    public int MagazineRoom;
    public MagazineVia MagazineVia;
    /// <summary>The hull girder cut through (an underwater explosion's, 4.6): the ship breaks in two.</summary>
    public bool Broken;

    public readonly bool Changed => Destroyed + Disabled + Damaged > 0 || BreachM2 > 0 || Jammed || Fire || Magazine || Broken;
}

/// <summary>EFFECTS (docs/battle/fighting.md 4.3a): a port of the effects half of research/simulation/damage-research/
/// gunfire_ref.py <c>resolve()</c>, on the ship's own parts and cells (08-gunfire-effects.md §3.6-3.9):
/// <list type="bullet">
/// <item><b>the line of flight:</b> what the shell's body went through before it stopped (parts and spaces), and the
/// people in its lane when it didn't burst;</item>
/// <item><b>holes:</b> the hull's plating crossed near or under the waterline lets the sea into the cell behind it (a
/// contact burst's hole, else the shell's own), and a heavy shell defeated by a belt at the waterline may open its
/// seams: water comes in through them (<see cref="Flooding"/>);</item>
/// <item><b>the burst:</b> parts within the wreck radius (cube-root scaled) wrecked or put out of action by distance,
/// unless their armour keeps its fragments out, and those its fragments get into damaged (E1 of docs/battle/
/// shells-armour-damage.md: the shell's own spray, <see cref="Fragments"/>, through each part's armour); the space it
/// bursts in and those its fragments get into through the bulkheads and decks; the crew within the radii; and the
/// burst's two single pieces, the nose slug and the base plug, on through the ship (<see cref="Pieces"/>);</item>
/// <item><b>fire:</b> a chance by the shell's kind, the fire load where it bursts and the burst's size; fragments
/// reaching a mount's ready ammunition;</item>
/// <item><b>armour that held:</b> a heavy hit on a turret or barbette that didn't get in may jam the mount (on a conning
/// tower, blind it);</item>
/// <item><b>magazines:</b> a burst in a magazine, or in a turret or its barbette, may set it off.</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Effects
{
    public static EffectsResult Apply(ShipClass c, ShipCondition k, in Hit hit, HitPath path, in TerminalResult t,
        SimSettings s, double propellant, ref RandomStream r)
    {
        var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
        var shell = hit.Shell;
        double calM = shell.CalibreMm / 1000, w = Math.Max(shell.FillerKg * shell.FillerRe, 0), size = Math.Cbrt(w);
        var xs = path.Crossings;
        var water = HitQuery.WaterOf(c, k.Attitude);
        bool passed = !t.Bursts && t.Outcome is Outcome.PassedThrough or Outcome.Dud or Outcome.BrokeUp or Outcome.Holed or Outcome.BurstBeyond;

        // the line of flight: what its body went into before it stopped
        int cell = -1;
        double lane = 0, laneCrew = 0;
        for (int i = 0; i < xs.Length && xs[i].T < t.StopT; i++)
        {
            ref readonly var x = ref xs[i];
            switch (x.Kind)
            {
                case CrossingKind.PartIn:
                    if (res.Part < 0)
                        res.Part = x.Index;
                    Count(ref res, k, x.Index, k.Worsen(x.Index, Struck(c.Components[x.Index].Kind)) ? Struck(c.Components[x.Index].Kind) : PartState.Ok);
                    break;
                case CrossingKind.CellIn:
                    cell = x.Index;
                    foreach (int room in c.RoomsIn(x.Index))
                        if (r.NextDouble() < Vital(c, room, s))
                            Count(ref res, k, c.RoomNode(room), k.Worsen(c.RoomNode(room), PartState.Damaged) ? PartState.Damaged : PartState.Ok);
                    if (res.Part < 0 && c.Cells[x.Index].Room >= 0)
                        res.Part = c.RoomNode(c.Cells[x.Index].Room);
                    break;
                case CrossingKind.CellOut:
                    if (cell >= 0)
                    {
                        // the lane through the space, for the people in it
                        double len = x.T - Entered(xs, i, x.Index);
                        lane += len;
                        laneCrew += len * Density(c, cell);
                    }
                    cell = -1;
                    break;
            }
        }
        if (passed)
            res.CrewLost += laneCrew * s.PassLaneM * s.PassCasualtyShare;

        // holes: plating crossed near or under the waterline, behind it the cell the sea comes into
        for (int i = 0; i < xs.Length; i++)
        {
            ref readonly var x = ref xs[i];
            if (x.Kind is not (CrossingKind.HullIn or CrossingKind.HullOut) || x.Point.Z > water.At(x.Point.X, x.Point.Y) + s.HoleOverWaterlineM)
                continue;
            bool contact = t.Bursts && t.Burst != BurstKind.Outside && Math.Abs(t.BurstT - x.T) <= 2;
            if (!(x.T < t.StopT) && !contact)
                continue;
            int behind = CellAt(xs, i, x.Kind == CrossingKind.HullIn);
            if (behind < 0)
                continue;
            double hole = contact ? s.HoleRadiusK * size * Math.Sqrt(6 / Math.Max(x.Mm, 1)) : 0.5 * s.ShellHoleCalibres * calM;
            double a = Math.PI * hole * hole;
            k.Hole(behind, a, x.Point);
            res.BreachM2 += a;
        }

        // armour that held, or that a shell glanced off (P5): a heavy hit's shock on a turret, a barbette, a conning tower; a
        // belt's seams at the waterline
        int held = t.Glance >= 0 ? t.Glance : t.Outcome is Outcome.Defeated or Outcome.BurstOutside ? t.Plate : -1;
        if (held >= 0)
        {
            ref readonly var x = ref xs[held];
            double jam = shell.CalibreMm >= s.JamHeavyMm ? s.JamChance[0] : shell.CalibreMm >= s.JamMediumMm ? s.JamChance[1] : s.JamChance[2];
            if (x.Plate is PlateKind.TurretFace or PlateKind.TurretSide or PlateKind.TurretRear or PlateKind.TurretRoof or PlateKind.Barbette)
            {
                int m = c.MountOf(x.Index);
                res.Part = x.Index;
                if (m >= 0 && r.NextDouble() < jam)
                {
                    k.Flags[c.Mounts[m].Component] |= PartFlags.Jammed;
                    res.Jammed = true;
                }
            }
            else if (x.Plate == PlateKind.PartArmour && c.Components[x.Index].Kind == ComponentKind.ConningTower)
            {
                res.Part = x.Index;
                if (r.NextDouble() < jam)
                    Count(ref res, k, x.Index, k.Worsen(x.Index, PartState.Disabled) ? PartState.Disabled : PartState.Ok);
            }
            else if (x.Plate == PlateKind.SideArmour && shell.CalibreMm >= s.SeamLeakMinMm &&
                     x.Point.Z <= water.At(x.Point.X, x.Point.Y) + s.HoleOverWaterlineM && r.NextDouble() < s.SeamLeakChance)
            {
                int behind = CellAt(xs, held, inward: true);
                if (behind >= 0)
                {
                    k.Hole(behind, s.SeamLeakM2, x.Point);
                    res.BreachM2 += s.SeamLeakM2;
                }
            }
        }

        if (t.Bursts)
        {
            Burst(c, k, hit, path, t, s, propellant, w, size, ref res, ref r);
            if (t.Burst != BurstKind.Outside && shell.Fragments is { } f)
                Pieces(c, k, path, t, f.Of(t.Outcome == Outcome.LowOrder), shell, s, water, ref res, ref r);
        }
        k.CrewLost += res.CrewLost;
        return res;
    }

    /// <summary>A burst at its point along the path, with its force.</summary>
    static void Burst(ShipClass c, ShipCondition k, in Hit hit, HitPath path, in TerminalResult t, SimSettings s,
        double propellant, double w, double size, ref EffectsResult res, ref RandomStream r)
    {
        var shell = hit.Shell;
        var xs = path.Crossings;
        var p = path.At(t.BurstT);
        double force = t.Force, rw = s.WreckRadiusK * size * Math.Sqrt(force), rb = s.BlastRadiusK * size * Math.Sqrt(force);
        int cell = -1, part = -1;
        foreach (ref readonly var x in xs)
        {
            if (x.T > t.BurstT)
                break;
            if (x.Kind == CrossingKind.CellIn)
                cell = x.Index;
            else if (x.Kind == CrossingKind.CellOut && x.Index == cell)
                cell = -1;
            else if (x.Kind == CrossingKind.PartIn)
                part = x.Index;
            else if (x.Kind == CrossingKind.PartOut && x.Index == part)
                part = -1;
        }
        bool inHull = cell >= 0;
        double cellTop = inHull ? c.Cells[cell].Box.Top : double.PositiveInfinity;
        // the burst's fragments (E1): a low-order burst's are fewer, slower and larger; they fly with the shell's motion
        var frag = shell.Fragments?.Of(t.Outcome == Outcome.LowOrder);
        var fdir = path.LegOf(path.Leg(t.BurstT)).Direction;
        int magazine = -1, turret = -1;   // the magazine room it burst in or beside, the enclosed mount it burst in
        var via = MagazineVia.None;

        // the part it burst in
        if (part >= 0)
        {
            res.Part = part;
            Count(ref res, k, part, k.Worsen(part, PartState.Destroyed) ? PartState.Destroyed : PartState.Ok);
            if (c.MountOf(part) is int m and >= 0 && Enclosed(c, m))
                turret = m;
        }

        // parts: within the wreck radius wrecked or out of action, unless armour its fragments don't get through boxes
        // them; beyond it, damaged by the fragments that get into them: in the hull those in the burst's own space (a
        // space's next door, through its join, below), outside it those on deck
        var comps = c.Components;
        var cellBox = inHull ? c.Cells[cell].Box : default;
        for (int i = 0; i < comps.Length; i++)
        {
            if (i == part)
                continue;
            ref readonly var comp = ref comps[i];
            if (inHull ? comp.Bounds.Base >= cellTop - 0.05 : comp.Bounds.Top <= 0)
                continue;   // in the hull: only what reaches down into this space or below; outside it: only what stands above the deck
            double d = Distance(c, i, p);
            if (d > rw && inHull && !Overlaps(comp.Bounds, cellBox))
                continue;
            double raw = Armour(c, i);
            // the fragments into it, through its armour and its own plating: any at all and its armour doesn't box it; its
            // vitals' share of them damage it (one draw for both, the second a subset of the first)
            double into = frag?.Hits(p, fdir, t.BurstMs, comp.Bounds, PartMild(c, i, raw)) ?? 0;
            double u = into > 1e-4 ? r.NextDouble() : 1;
            bool pierced = u < 1 - Math.Exp(-into), struck = u < 1 - Math.Exp(-into * s.PartVital[Math.Min((int)comp.Kind, s.PartVital.Length - 1)]);
            if (raw > 0 && !pierced)
                continue;
            var state = d <= s.DestroyedShare * rw ? PartState.Destroyed : d <= rw ? PartState.Disabled : struck ? PartState.Damaged : PartState.Ok;
            if (state == PartState.Ok)
                continue;
            bool worse = state == PartState.Damaged ? k.Harm(i) : k.Worsen(i, state);
            Count(ref res, k, i, worse ? k.State[i] : PartState.Ok);
            if (res.Part < 0)
                res.Part = i;
            int m = c.MountOf(i);
            if (worse && m >= 0 && c.Mounts[m].Component == i && c.Mounts[m].ReadyRounds > 0 && state >= PartState.Disabled &&
                r.NextDouble() < s.ReadyAmmoFireChance * force)
            {
                k.Flags[i] |= PartFlags.Burning;
                res.Fire = true;
            }
        }

        // the space it burst in, and those its fragments get into through the bulkheads and decks
        if (inHull && t.Burst != BurstKind.Outside)
        {
            foreach (int room in c.RoomsIn(cell))
            {
                ref readonly var rr = ref c.Rooms[room];
                int node = c.RoomNode(room);
                if (res.Part < 0 || part < 0 && res.Part >= c.Components.Length)
                    res.Part = node;
                if (r.NextDouble() >= RoomChance(c, room, rw, force, s))
                    continue;   // its fragments missed what matters in it
                bool worse = rw >= s.RoomDisableShare * Math.Max(rr.X1 - rr.X0, 1) ? k.Worsen(node, PartState.Disabled) : k.Harm(node);
                Count(ref res, k, node, worse ? k.State[node] : PartState.Ok);
                if (magazine < 0 && rr.Kind == RoomKind.Magazine)
                    (magazine, via) = (room, MagazineVia.In);
            }
            foreach (var n in c.Neighbours(c.Cells[cell].Neighbours))
            {
                double d = BoxDistance(c.Cells[n.Cell].Box, p);
                if (d > rw)
                    continue;
                // its fragments through the join (as mild steel against them), square to its plate
                var there = c.Cells[n.Cell].Box;
                double join = n.Join switch
                {
                    JoinKind.Deck => DeckMild(c, n.Through, c.Cells[cell].Box, there),
                    JoinKind.Bulkhead => c.Bulkheads[n.Through].PlateMm + Fragments.MildMm(c.Bulkheads[n.Through].ArmourMm, FragmentClass.Ductile),
                    _ => 0,
                } + InclinedMild(c, p, Nearest(there, p));
                var normal = n.Join == JoinKind.Deck ? new P3(0, 0, 1) : Touch(c.Cells[cell].Box, there) ? new P3(0, 1, 0) : new P3(1, 0, 0);
                double into = frag?.Hits(p, fdir, t.BurstMs, there, join, normal) ?? 0;
                if (!(into > 1e-4 && r.NextDouble() < 1 - Math.Exp(-into)))
                    continue;
                if (n.Join != JoinKind.Open)
                    k.CellOpened[cell] = k.CellOpened[n.Cell] = true;
                // the parts in it (not in the burst's own space, nor within the wreck radius, which the parts above had):
                // damaged by the fragments through the join and their own plates
                for (int j = 0; j < comps.Length; j++)
                {
                    ref readonly var comp = ref comps[j];
                    if (j == part || !Overlaps(comp.Bounds, there) || Overlaps(comp.Bounds, cellBox) || Distance(c, j, p) <= rw)
                        continue;
                    double hits = frag?.Hits(p, fdir, t.BurstMs, comp.Bounds, join + PartMild(c, j, Armour(c, j)), normal) ?? 0;
                    if (hits > 1e-4 && r.NextDouble() < 1 - Math.Exp(-hits * s.PartVital[Math.Min((int)comp.Kind, s.PartVital.Length - 1)]))
                        Count(ref res, k, j, k.Harm(j) ? k.State[j] : PartState.Ok);
                }
                foreach (int room in c.RoomsIn(n.Cell))
                {
                    if (r.NextDouble() >= s.NeighbourShare * RoomChance(c, room, rw, force, s))
                        continue;
                    int node = c.RoomNode(room);
                    Count(ref res, k, node, k.Worsen(node, PartState.Damaged) ? PartState.Damaged : PartState.Ok);
                    if (magazine < 0 && c.Rooms[room].Kind == RoomKind.Magazine)
                        (magazine, via) = (room, MagazineVia.Beside);
                }
            }
        }

        // the crew within the radii
        double dens = part >= 0 ? PartDensity(c, part, s) : inHull ? Density(c, cell) : s.DeckCrewPerM2;
        res.CrewLost += dens * (s.CasualtyWreckShare * Math.PI * rw * rw + s.CasualtyBlastShare * Math.PI * (rb * rb - rw * rw)) * r.NextDouble(0.3, 1.7);

        // fire, by what burns where it burst
        double load = part >= 0 ? s.FireLoadSuperstructure : inHull ? (Terminal.IsMachinery(c, cell) ? s.FireLoadMachinery : s.FireLoadHull) : s.FireLoadHull;
        double era = s.FireLoadByEra[Math.Clamp((int)c.Era, 0, s.FireLoadByEra.Length - 1)];
        double chance = s.FireChance[Math.Clamp((int)shell.Kind, 0, s.FireChance.Length - 1)] * load * era * Math.Min(1.5, 0.5 + 0.5 * Math.Cbrt(w / 10)) *
                        (t.Burst == BurstKind.Outside ? s.FireOutsideShare : 1);
        if (r.NextDouble() < chance)
        {
            if (part >= 0)
                k.Flags[part] |= PartFlags.Burning;
            else if (inHull)
                k.CellFire[cell] = true;
            else if (res.Part >= 0 && res.Part < c.Components.Length)
                k.Flags[res.Part] |= PartFlags.Burning;
            res.Fire = true;
        }

        // the magazine: a burst in it, or in a turret or its barbette (the handling path down to it)
        if (t.Burst != BurstKind.Outside)
        {
            double flash = magazine >= 0 ? s.MagazineFlashChance : turret >= 0 ? s.TurretFlashChance : 0;
            if (flash > 0 && r.NextDouble() < flash * propellant)
                (res.Magazine, res.MagazineRoom, res.MagazineVia) = magazine >= 0 ? (true, magazine, via) : (true, c.Mounts[turret].Magazine, MagazineVia.Turret);
        }
    }

    /// <summary>A burst's single pieces (E1; damage/13 §2.5), on through the ship along the shell's line from the burst:
    /// an AP or common shell's nose slug, at the shell's speed plus its kick, and the base plug, at the shell's speed less
    /// its (an HE shell's plug is thrown back, into the tail's spray), each walked like a shell (<see cref="Piece"/>).</summary>
    static void Pieces(ShipClass c, ShipCondition k, HitPath path, in TerminalResult t, FragmentBurst b, in ShellTypeRow shell, SimSettings s,
        Waterplane water, ref EffectsResult res, ref RandomStream r)
    {
        if (shell.Kind != ShellKind.He && b.NoseKg > 0)
            Piece(c, k, path, t.BurstT, t.BurstMs + b.NoseKickMs, b.NoseKg, s.SlugCalibres * shell.CalibreMm, s, water, ref res, ref r);
        double plug = t.BurstMs - b.PlugKickMs;
        if (b.PlugKg > 0 && plug > s.PieceMinMs)
            Piece(c, k, path, t.BurstT, plug, b.PlugKg, s.PlugCalibres * shell.CalibreMm, s, water, ref res, ref r);
    }

    /// <summary>A solid piece walked along the path from T at v: each plate holds it under Thompson's limit for its mass and
    /// width, the plate as hard as <see cref="SimSettings.PieceQuality"/> of it is to a whole shell (blunt and broken), and
    /// it goes on at Lambert-Jonas's residual past one it beats; slower than <see cref="SimSettings.PieceMinMs"/>, or out
    /// into the sea, it's done. On the way it strikes the parts it enters, reaches a space's vitals as a shell passing
    /// through does, and holes the hull's plating it crosses near or under the waterline, its own width across.</summary>
    static void Piece(ShipClass c, ShipCondition k, HitPath path, double fromT, double v, double massKg, double widthMm, SimSettings s,
        Waterplane water, ref EffectsResult res, ref RandomStream r)
    {
        var xs = path.Crossings;
        bool inside = true;
        for (int i = 0; i < xs.Length && v > s.PieceMinMs; i++)
        {
            ref readonly var x = ref xs[i];
            if (x.T <= fromT + HitPath.SameM)
                continue;
            switch (x.Kind)
            {
                case CrossingKind.WaterIn when inside:
                    return;
                case CrossingKind.PartIn:
                    Count(ref res, k, x.Index, k.Worsen(x.Index, Struck(c.Components[x.Index].Kind)) ? Struck(c.Components[x.Index].Kind) : PartState.Ok);
                    break;
                case CrossingKind.CellIn:
                    foreach (int room in c.RoomsIn(x.Index))
                        if (r.NextDouble() < Vital(c, room, s))
                            Count(ref res, k, c.RoomNode(room), k.Worsen(c.RoomNode(room), PartState.Damaged) ? PartState.Damaged : PartState.Ok);
                    break;
                case CrossingKind.HullOut:
                    inside = false;
                    break;
                case CrossingKind.HullIn:
                    inside = true;
                    break;
            }
            if (x.Plate == PlateKind.None || !(x.Mm > 0))
                continue;
            double te = x.Mm * c.MaterialQ(x.Material, x.Mm, widthMm) / s.PieceQuality;
            double vl = Terminal.LimitMs(te, widthMm, massKg, Terminal.Obliquity(path.LegOf(path.Leg(x.T)).Direction, x.Normal));
            if (v < vl)
                return;
            v = Terminal.Residual(v, vl);
            if (x.Kind is CrossingKind.HullIn or CrossingKind.HullOut && x.Point.Z <= water.At(x.Point.X, x.Point.Y) + s.HoleOverWaterlineM)
            {
                int behind = CellAt(xs, i, x.Kind == CrossingKind.HullIn);
                if (behind >= 0)
                {
                    double a = Math.PI * Math.Pow(0.5 * widthMm / 1000, 2);
                    k.Hole(behind, a, x.Point);
                    res.BreachM2 += a;
                }
            }
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The chance a burst inside a space reaches what matters in it (gunfire_ref.py's system hit: the vitals'
    /// share of the floor, and the wreck radius's area over it, times the burst's force), at most 1.</summary>
    static double RoomChance(ShipClass c, int room, double rw, double force, SimSettings s)
    {
        double area = 0;
        foreach (int cell in c.Links(c.Rooms[room].Cells))
        {
            var b = c.Cells[cell].Box;
            area += (b.X1 - b.X0) * (b.Y1 - b.Y0);
        }
        return Math.Min(1, force * (Vital(c, room, s) + Math.PI * rw * rw / Math.Max(area, 1)));
    }

    /// <summary>A turret proper (a battleship's, with its hoists down to the magazine), not an open or shielded mount
    /// whose ammunition comes up by hand: the flash chain runs only from the one (07-magazine-explosions.md).</summary>
    static bool Enclosed(ShipClass c, int mount) => c.Mounts[mount].Turret && c.TurretTypes[c.Mounts[mount].Type].Shape == TurretShape.Bb;

    static double Vital(ShipClass c, int room, SimSettings s) => s.RoomVital[Math.Min((int)c.Rooms[room].Kind, s.RoomVital.Length - 1)];

    /// <summary>What a shell passing through a part does to it: a mount, a director, a shaft or a rudder is put out of
    /// action; plating, a funnel or a mast is damaged.</summary>
    internal static PartState Struck(ComponentKind kind) => kind switch
    {
        ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo or ComponentKind.Aa or ComponentKind.Shaft or ComponentKind.Propeller
            or ComponentKind.Rudder or ComponentKind.TorpedoTube => PartState.Disabled,
        _ => PartState.Damaged,
    };

    internal static void Count(ref EffectsResult res, ShipCondition k, int node, PartState s)
    {
        switch (s)
        {
            case PartState.Destroyed:
                res.Destroyed++;
                break;
            case PartState.Disabled:
                res.Disabled++;
                break;
            case PartState.Damaged:
                res.Damaged++;
                break;
        }
    }

    /// <summary>Where a crossing's cell was entered along the path (the CellIn before it), or the path's start.</summary>
    static double Entered(ReadOnlySpan<Crossing> xs, int i, int cell)
    {
        for (int j = i - 1; j >= 0; j--)
            if (xs[j].Kind == CrossingKind.CellIn && xs[j].Index == cell)
                return xs[j].T;
        return 0;
    }

    /// <summary>The cell on the inner side of a hull crossing (or behind a plate): the next cell entered at the same
    /// point going in, or the cell left there going out.</summary>
    static int CellAt(ReadOnlySpan<Crossing> xs, int i, bool inward)
    {
        double t = xs[i].T;
        if (inward)
        {
            for (int j = i; j < xs.Length && xs[j].T <= t + 0.05; j++)
                if (xs[j].Kind == CrossingKind.CellIn)
                    return xs[j].Index;
        }
        else
            for (int j = i; j >= 0 && xs[j].T >= t - 0.05; j--)
                if (xs[j].Kind == CrossingKind.CellOut)
                    return xs[j].Index;
        return -1;
    }

    /// <summary>People per m² of a cell's floor in battle.</summary>
    static double Density(ShipClass c, int cell)
    {
        var b = c.Cells[cell].Box;
        return c.Cells[cell].BattleCrew / Math.Max((b.X1 - b.X0) * (b.Y1 - b.Y0), 1);
    }

    static double PartDensity(ShipClass c, int part, SimSettings s)
    {
        ref readonly var comp = ref c.Components[part];
        var b = comp.Bounds;
        return comp.BattleCrew > 0 ? comp.BattleCrew / Math.Max((b.X1 - b.X0) * (b.Y1 - b.Y0), 1) : s.DeckCrewPerM2;
    }

    /// <summary>A part's armour against fragments: a turret's side or roof (the thinner), else its own armour.</summary>
    static double Armour(ShipClass c, int i)
    {
        int m = c.MountOf(i);
        if (m >= 0 && c.Mounts[m].Component == i && c.Mounts[m].Turret)
            return Math.Min(c.Mounts[m].SideMm, c.Mounts[m].RoofMm);
        return c.Components[i].ArmourMm;
    }


    /// <summary>A deck between two cells, where they meet, as the mild steel that stops fragments as it does (E1): its
    /// plate, and the armour on it there by its material (each piece of a deck armoured in several, a magazine box's
    /// crown or floor; none over a sloped deck's slopes, which InclinedMild finds).</summary>
    static double DeckMild(ShipClass c, int deck, in Box here, in Box beyond)
    {
        double mm = c.Decks[deck].PlateMm;
        double x = (Math.Max(here.X0, beyond.X0) + Math.Min(here.X1, beyond.X1)) / 2, y = (Math.Max(here.Y0, beyond.Y0) + Math.Min(here.Y1, beyond.Y1)) / 2;
        foreach (ref readonly var a in c.ArmourDecks)
            if (a.Deck == deck && a.Covers(x, y, c.Hull))
                mm += c.MaterialOf(a.Material).FragmentMildMm(a.ThicknessMm);
        return mm;
    }

    /// <summary>Whether two boxes overlap (by more than a few centimetres each way).</summary>
    static bool Overlaps(in Box a, in Box b) =>
        Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) > 0.05 && Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) > 0.05 &&
        Math.Min(a.Top, b.Top) - Math.Max(a.Base, b.Base) > 0.05;

    /// <summary>A part's own plates as mild steel against fragments (E1): its armour by its material, and the plating
    /// of a superstructure block, a funnel, an uptake's casing or a mast (the ship's superstructure plating).</summary>
    static double PartMild(ShipClass c, int i, double armourMm)
    {
        ref readonly var comp = ref c.Components[i];
        double mm = armourMm > 0 ? c.MaterialOf(comp.Material).FragmentMildMm(armourMm) : 0;
        if (comp.Kind is ComponentKind.Superstructure or ComponentKind.Funnel or ComponentKind.Casing or ComponentKind.Mast)
            mm += c.Plating.SuperstructureMm;
        return mm;
    }

    /// <summary>Whether two boxes meet side by side across the ship (a longitudinal bulkhead between them).</summary>
    static bool Touch(in Box a, in Box b) => Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) < 0.05 && Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) > 0.05;

    /// <summary>The inclined plates (a deck's slopes) between two points, as mild steel against fragments: those whose
    /// surface the line between them crosses, on the first point's side of the ship.</summary>
    static double InclinedMild(ShipClass c, P3 a, P3 b)
    {
        double mm = 0;
        int sgn = a.Y < 0 ? -1 : 1;
        foreach (ref readonly var p in c.Inclined)
        {
            // the plate's surface where the side is: G = sgn·y - (hw - u), its sign each end
            double ga = sgn * a.Y - p.YAt(c.Hull, a.X, a.Z), gb = sgn * b.Y - p.YAt(c.Hull, b.X, b.Z);
            if (ga * gb >= 0)
                continue;
            double f = ga / (ga - gb), x = a.X + f * (b.X - a.X), z = a.Z + f * (b.Z - a.Z);
            if (x >= p.X0 && x <= p.X1 && z >= p.Z0 && z <= p.Z1 && sgn * (a.Y + f * (b.Y - a.Y)) >= 0)
                mm += c.MaterialOf(p.Material).FragmentMildMm(p.MmAt(z));
        }
        return mm;
    }

    /// <summary>The point of a box nearest p.</summary>
    static P3 Nearest(in Box b, P3 p) => new(Math.Clamp(p.X, b.X0, b.X1), Math.Clamp(p.Y, b.Y0, b.Y1), Math.Clamp(p.Z, b.Base, b.Top));

    /// <summary>How far a point is from a part: a mount's turret (a cylinder about its pivot), else its box.</summary>
    internal static double Distance(ShipClass c, int i, P3 p)
    {
        ref readonly var comp = ref c.Components[i];
        int m = c.MountOf(i);
        if (m >= 0 && c.Mounts[m].Component == i)
        {
            ref readonly var mr = ref c.Mounts[m];
            double rad = c.TurretTypes[mr.Type].R;
            double dh = Math.Max(Math.Sqrt((p.X - mr.X) * (p.X - mr.X) + (p.Y - mr.Y) * (p.Y - mr.Y)) - rad, 0);
            double dz = p.Z < comp.Bounds.Base ? comp.Bounds.Base - p.Z : p.Z > comp.Bounds.Top ? p.Z - comp.Bounds.Top : 0;
            return Math.Sqrt(dh * dh + dz * dz);
        }
        return BoxDistance(comp.Bounds, p);
    }

    internal static double BoxDistance(in Box b, P3 p)
    {
        double dx = Math.Max(Math.Max(b.X0 - p.X, p.X - b.X1), 0), dy = Math.Max(Math.Max(b.Y0 - p.Y, p.Y - b.Y1), 0),
            dz = Math.Max(Math.Max(b.Base - p.Z, p.Z - b.Top), 0);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

}
