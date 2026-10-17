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
/// and the unarmoured ones in the blast's ring damaged, unless armour stronger than its fragments there boxes them; the
/// space it bursts in and those beyond bulkheads its fragments get through; the crew within the radii;</item>
/// <item><b>fire:</b> a chance by the shell's kind, the fire load where it bursts and the burst's size; fragments
/// reaching a mount's ready ammunition;</item>
/// <item><b>armour that held:</b> a heavy hit on a turret or barbette that didn't get in may jam the mount (on a conning
/// tower, blind it);</item>
/// <item><b>magazines:</b> a burst in a magazine, or in a turret or its barbette, may set it off.</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Effects
{
    /// <summary>Okun's fragments: at a distance in calibres from the burst, the plate (in calibres of STS) that half of
    /// them still get through (08 §3.6).</summary>
    static readonly double[] FragDist = [5, 20, 50, 100, 1000];
    static readonly double[] FragT50 = [0.080, 0.0213, 0.0158, 0.0113, 0.0006];

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

        // armour that held: a heavy hit's shock on a turret, a barbette, a conning tower; a belt's seams at the waterline
        if (t.Outcome is Outcome.Defeated or Outcome.Ricochet or Outcome.BurstOutside && t.Plate >= 0)
        {
            ref readonly var x = ref xs[t.Plate];
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
                int behind = CellAt(xs, t.Plate, inward: true);
                if (behind >= 0)
                {
                    k.Hole(behind, s.SeamLeakM2, x.Point);
                    res.BreachM2 += s.SeamLeakM2;
                }
            }
        }

        if (t.Bursts)
            Burst(c, k, hit, path, t, s, propellant, w, size, ref res, ref r);
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

        // parts within the radii, unless boxed by armour its fragments don't get through there
        var comps = c.Components;
        for (int i = 0; i < comps.Length; i++)
        {
            if (i == part)
                continue;
            ref readonly var comp = ref comps[i];
            if (inHull ? comp.Bounds.Base >= cellTop - 0.05 : comp.Bounds.Top <= 0)
                continue;   // in the hull: only what reaches down into this space or below; outside it: only what stands above the deck
            double d = Distance(c, i, p);
            if (d > rb)
                continue;
            double raw = Armour(c, i), armour = raw * c.MaterialQ(comp.Material, raw, 0);   // fragments: q as at 8"
            if (armour > 0 && armour >= FragPenMm(shell, d))
                continue;
            var state = d <= s.DestroyedShare * rw ? PartState.Destroyed : d <= rw ? PartState.Disabled : armour < 1 ? PartState.Damaged : PartState.Ok;
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
                double join = n.Join switch
                {
                    JoinKind.Deck => DeckMm(c, n.Through, c.Cells[cell].Box, c.Cells[n.Cell].Box),
                    JoinKind.Bulkhead => c.Bulkheads[n.Through].PlateMm + c.Bulkheads[n.Through].ArmourMm,
                    _ => 0,
                } + InclinedMm(c, p, Nearest(c.Cells[n.Cell].Box, p));
                if (join >= FragPenMm(shell, Math.Max(d, 0.1)))
                    continue;
                if (n.Join != JoinKind.Open)
                    k.CellOpened[cell] = k.CellOpened[n.Cell] = true;
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


    /// <summary>A deck between two cells, where they meet: its plate, and the armour on it there (each piece of a deck
    /// armoured in several, a magazine box's crown or floor; none over a sloped deck's slopes, which InclinedMm finds).</summary>
    static double DeckMm(ShipClass c, int deck, in Box here, in Box beyond)
    {
        double mm = c.Decks[deck].PlateMm;
        double x = (Math.Max(here.X0, beyond.X0) + Math.Min(here.X1, beyond.X1)) / 2, y = (Math.Max(here.Y0, beyond.Y0) + Math.Min(here.Y1, beyond.Y1)) / 2;
        foreach (ref readonly var a in c.ArmourDecks)
            if (a.Deck == deck && a.Covers(x, y, c.Hull))
                mm += a.ThicknessMm;
        return mm;
    }

    /// <summary>The inclined plates (a deck's slopes) between two points: those whose surface the line between them
    /// crosses, on the first point's side of the ship.</summary>
    static double InclinedMm(ShipClass c, P3 a, P3 b)
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
                mm += p.MmAt(z);
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

    /// <summary>The plate half a burst's fragments still get through at a distance, mm of STS (Okun, log-log between his
    /// points; HE's lighter, faster fragments 1.35 times as far).</summary>
    public static double FragPenMm(in ShellTypeRow shell, double distM)
    {
        double cal = shell.CalibreMm / 1000, d = Math.Max(5, distM / Math.Max(cal, 1e-3));
        var dist = FragDist;
        var t50 = FragT50;
        for (int i = 0; i + 1 < dist.Length; i++)
            if (d <= dist[i + 1])
            {
                double f = (Math.Log(d) - Math.Log(dist[i])) / (Math.Log(dist[i + 1]) - Math.Log(dist[i]));
                double v = Math.Exp(Math.Log(t50[i]) + f * (Math.Log(t50[i + 1]) - Math.Log(t50[i])));
                return v * (shell.Kind == ShellKind.He ? 1.35 : 1) * shell.CalibreMm;
            }
        return 0;
    }
}
