using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a hit did, for the log and the report: the part it's told by (a node: the part it burst in or was
/// stopped by, else the first it entered; -1 for none), how many parts it wrecked, put out of action or damaged, the
/// crew it hit, the holes it opened to the sea (m²), and whether it jammed a mount, started a fire, set off a magazine
/// or broke the ship's back.</summary>
/// <summary>How a hit set a magazine off: a burst in it, beside it (fragments through a wall or deck), charges lit in a
/// gunhouse, its working chamber or a casemate and the flame down its train (docs/battle/magazines.md V2), or an
/// explosion under water within its reach.</summary>
public enum MagazineVia : byte
{
    None,
    In,
    Beside,
    Turret,
    Underwater,
    /// <summary>E3: spall off the back of a plate that held, lighting charges where it reached them (a gunhouse, a working
    /// chamber, a handling room, a magazine).</summary>
    Spall,
    /// <summary>E5: a burst's gas reaching a magazine or a handling room through what it broke (Savannah's), and a burst in
    /// a handling room among the charges passing up.</summary>
    Gas,
    Handling,
}

public struct EffectsResult
{
    public int Part;
    public int Destroyed, Disabled, Damaged;
    public double CrewLost, BreachM2;
    /// <summary>E6: the men it killed and wounded (CrewLost: their sum, or a torpedo's rule's men), and whether propellant
    /// flashed through a crew (a magazine's train, a mount's ready charges).</summary>
    public int Killed, Wounded;
    public bool Flash;
    public bool Jammed, Fire, Magazine;
    /// <summary>The magazine set off (a room of the class), when <see cref="Magazine"/>; else -1.</summary>
    public int MagazineRoom;
    public MagazineVia MagazineVia;
    /// <summary>The stations of an ammunition train whose charges this hit lit, the magazine's own among them
    /// (docs/battle/magazines.md V2).</summary>
    public int Stations;
    /// <summary>The hull girder cut through (an underwater explosion's, 4.6): the ship breaks in two.</summary>
    public bool Broken;
    /// <summary>E3: a plate that held threw spall off its back.</summary>
    public bool Spall;
    /// <summary>E4: what a turret or barbette that held did to its mount, and whether the struck structure's crew were
    /// stunned (a turret's, a conning tower's).</summary>
    public TurretHarm Turret;
    public bool Stunned;
    /// <summary>E4: a belt that held leaked (its regime), and fouled a boiler or engine room's supply.</summary>
    public LeakRegime Leak;
    public bool Feed;
    /// <summary>E4: the shock put gear out for a while; the whole ship's power cascaded out.</summary>
    public bool Shock, Blackout;
    /// <summary>E5: what its gas did in a closed space (none: it burst outside, in the open, or held).</summary>
    public BlastResult Blast;
    /// <summary>E7: a near miss's splinters expected through the hull's side, and into the parts above it; its shock factor
    /// at the hull's nearest wetted point, and the seams and plating its shock opened to the sea (m², in BreachM2).</summary>
    public double SideThrough, UpperThrough, Nsf, ShockM2;

    public readonly bool Changed => Destroyed + Disabled + Damaged > 0 || BreachM2 > 0 || Jammed || Fire || Magazine || Broken ||
        Turret.Blow != TurretBlow.None || Stunned || Feed || Shock ||
        Blast.OpenM2 + Blast.LeakM2 > 0;
}

/// <summary>EFFECTS (docs/battle/fighting.md 4.3a): a port of the effects half of research/simulation/damage-research/
/// gunfire_ref.py <c>resolve()</c>, on the ship's own parts and cells (08-gunfire-effects.md §3.6-3.9):
/// <list type="bullet">
/// <item><b>the line of flight:</b> what the shell's body went through before it stopped (parts and spaces), and the
/// people in its lane when it didn't burst;</item>
/// <item><b>holes:</b> the hull's plating crossed lets the sea into the cell behind it at its height (a
/// contact burst's hole, else the shell's own; <see cref="Flooding"/>);</item>
/// <item><b>the burst:</b> parts within the wreck radius (cube-root scaled) wrecked or put out of action by distance,
/// unless their armour keeps its fragments out, and those its fragments get into damaged (E1 of docs/battle/
/// shells-armour-damage.md: the shell's own spray, <see cref="Fragments"/>, through each part's armour); the space it
/// bursts in and those its fragments get into through the bulkheads and decks; the crew within the radii; and the
/// burst's two single pieces, the nose slug and the base plug, on through the ship (<see cref="Pieces"/>);</item>
/// <item><b>fire:</b> a chance by the shell's kind, the fire load where it bursts and the burst's size; fragments
/// reaching a mount's ready ammunition;</item>
/// <item><b>armour that held</b> (E4 of docs/battle/shells-armour-damage.md; damage/13): a turret or barbette a shell
/// didn't get through may be jammed in train, stiff, have a gun or its sights out, or its crew stunned, for a time by
/// cause (<see cref="NonPen"/>); a conning tower's crew stunned; a belt leaking into the cell behind it by where it
/// struck against the waterline now, by regime (<see cref="Belt"/>);</item>
/// <item><b>magazines:</b> a burst in a magazine may set it off; charges lit at a station of a mount's ammunition train
/// burn there, and the flame goes on down the train's open doors (<see cref="Magazines"/>).</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Effects
{
    public static EffectsResult Apply(ShipClass c, ShipCondition k, in Hit hit, HitPath path, in TerminalResult t,
        SimSettings s, ref RandomStream r)
    {
        var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
        var shell = hit.Shell;
        double calM = shell.CalibreMm / 1000, w = Math.Max(shell.FillerKg * shell.FillerRe, 0), size = Math.Cbrt(w);
        var xs = path.Crossings;
        var water = HitQuery.WaterOf(c, k.Attitude);
        Casualties.Begin(k);

        // the line of flight: what its body went into before it stopped; the men in its lane, and the disc each plate it
        // got through drives ahead of it (E6; 16 §4.3)
        int cell = -1, inPart = -1;
        double cellT = 0, partT = 0;
        for (int i = 0; i < xs.Length && xs[i].T < t.StopT; i++)
        {
            ref readonly var x = ref xs[i];
            switch (x.Kind)
            {
                case CrossingKind.PartIn:
                    if (res.Part < 0)
                        res.Part = x.Index;
                    Count(ref res, k, x.Index, k.Worsen(x.Index, Struck(c.Components[x.Index].Kind)) ? Struck(c.Components[x.Index].Kind) : PartState.Ok);
                    (inPart, partT) = (x.Index, x.T);
                    break;
                case CrossingKind.PartOut:
                    if (x.Index == inPart)
                        Casualties.Lane(c, k, Casualties.Part(inPart), path.At(partT), x.Point, calM, s);
                    inPart = x.Index == inPart ? -1 : inPart;
                    break;
                case CrossingKind.CellIn:
                    (cell, cellT) = (x.Index, x.T);
                    // a room with contents is damaged by what the shell struck in it (C1c), the rest by their vitals' share; a
                    // nose fuze's shell never walks among the contents (it fires on the first plate), so its rooms take the share
                    foreach (int room in c.RoomsIn(x.Index))
                        if ((shell.Fuze == Fuze.Nose || !c.ContentsOf(room).Some) && r.NextDouble() < Vital(c, room, s))
                            Count(ref res, k, c.RoomNode(room), k.Worsen(c.RoomNode(room), PartState.Damaged) ? PartState.Damaged : PartState.Ok);
                    if (res.Part < 0 && c.Cells[x.Index].Room >= 0)
                        res.Part = c.RoomNode(c.Cells[x.Index].Room);
                    break;
                case CrossingKind.CellOut:
                    if (cell >= 0)
                        Casualties.Lane(c, k, Casualties.Cell(c, cell), path.At(cellT), x.Point, calM, s);
                    cell = -1;
                    break;
                case CrossingKind.Struck when x.Plate == PlateKind.Contents:   // in a part: the part's struck already, as it was entered
                    Count(ref res, k, c.RoomNode(x.Index), k.Worsen(c.RoomNode(x.Index), PartState.Damaged) ? PartState.Damaged : PartState.Ok);
                    break;
            }
            // what it struck inside throws no plate's disc ahead of it (its own splinters are for later: shell-contents.md)
            if (x.Plate != PlateKind.None && !x.Plate.IsContents() && x.Mm > 0 && Into(c, xs, i) is int into and >= 0)
                Casualties.Debris(c, k, into, Debris(c, path, x, shell.CalibreMm, hit.SpeedMs), s);
        }
        if (!double.IsInfinity(t.StopT))
        {
            if (cell >= 0)
                Casualties.Lane(c, k, Casualties.Cell(c, cell), path.At(cellT), path.At(t.StopT), calM, s);
            if (inPart >= 0)
                Casualties.Lane(c, k, Casualties.Part(inPart), path.At(partT), path.At(t.StopT), calM, s);
        }

        // holes: the hull's plating crossed, behind it the cell the sea comes into; at any height, as the flooding takes
        // each at its own (over the waterline it lets the sea in once she settles, lists or trims, or her bow wave reaches it)
        for (int i = 0; i < xs.Length; i++)
        {
            ref readonly var x = ref xs[i];
            if (x.Kind is not (CrossingKind.HullIn or CrossingKind.HullOut))
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

        // armour that held, or that a shell glanced off (P5): what it shakes (E4), by the impulse it took and how near the
        // shell came to getting through
        int held = t.Glance >= 0 ? t.Glance : t.Outcome is Outcome.Defeated or Outcome.BurstOutside ? t.Plate : -1;
        if (held >= 0)
        {
            ref readonly var x = ref xs[held];
            double impulse = shell.MassKg * t.PlateMs;
            if (x.Plate is PlateKind.TurretFace or PlateKind.TurretSide or PlateKind.TurretRear or PlateKind.TurretRoof or PlateKind.Barbette)
            {
                // a turret or its barbette: jammed, stiff, a gun out, its sights out, its crew stunned (13 §1.4)
                int m = c.MountOf(x.Index);
                res.Part = x.Index;
                if (m >= 0)
                {
                    var spot = NonPen.Spot(x.Plate, x.Point.Z);
                    // a nose fuze bursting on contact pushes less than a shell stopped whole (13 §1.4's HE row); a base fuze
                    // that bursts after it stopped has given the plate its whole impulse already
                    bool contact = t.Burst == BurstKind.Outside && shell.Fuze == Fuze.Nose;
                    var h = NonPen.Turret(spot, impulse, NonPen.Margin(t.PlateU), contact,
                        spot <= TurretSpot.BarbetteDeck && x.Mm < s.ThinTurretMm, s, ref r);
                    k.Strike(m, h, hit.Ms);
                    (res.Turret, res.Stunned) = (h, h.StunMs > 0);
                    res.Jammed = h.Blow == TurretBlow.Jam;
                }
            }
            else if (x.Plate == PlateKind.PartArmour && c.Components[x.Index].Kind == ComponentKind.ConningTower)
            {
                // a conning tower: its crew stunned (13 §3.4)
                res.Part = x.Index;
                long stun = NonPen.Stun(impulse, s.TowerStunChance, s, ref r);
                k.Down(x.Index, hit.Ms, stun);
                res.Stunned = stun > 0;
            }
            else if (x.Plate == PlateKind.SideArmour)
                Belt(c, k, path, held, impulse, NonPen.Margin(t.PlateU), water, hit.Ms, s, ref res, ref r);
            // and the shock through the structure, on a belt, a barbette, a turret or a conning tower (13 §3.4)
            if (x.Plate is PlateKind.SideArmour or PlateKind.Barbette or >= PlateKind.TurretFace and <= PlateKind.TurretRoof ||
                x.Plate == PlateKind.PartArmour && c.Components[x.Index].Kind == ComponentKind.ConningTower)
                Shock(c, k, x.Point, x.Plate == PlateKind.SideArmour ? -1 : x.Index, impulse, hit.Ms, s, ref res, ref r);
        }

        // E3: behind a plate that held, the spall off its back: a shell it stopped within its band, or a heavy shell
        // glancing off a thin turret roof (Dunkerque); a burst on a face it doesn't hole throws next to nothing (14 §3.3)
        if (!double.IsNaN(t.Spall) && t.Plate >= 0 && !xs[t.Plate].Plate.IsContents())
            Spall(c, k, path, t.Plate, t.Spall, false, shell, t.PlateMs, s, ref res, ref r);
        else if (t.Glance >= 0 && xs[t.Glance].Plate == PlateKind.TurretRoof && xs[t.Glance].Mm <= s.SpallRoofTD * shell.CalibreMm &&
                 r.NextDouble() < s.SpallRoofChance)
            Spall(c, k, path, t.Glance, 0.5, true, shell, t.PlateMs, s, ref res, ref r);

        if (t.Bursts)
        {
            Burst(c, k, hit, path, t, s, w, size, ref res, ref r);
            if (t.Burst != BurstKind.Outside && shell.Fragments is { } f)
                Pieces(c, k, path, t, f.Of(t.Outcome == Outcome.LowOrder), shell, s, water, ref res, ref r);
        }
        else if (t.Broken && shell.Fragments is not null)
        {
            // E2: holed in pieces, which go on through the hole: a weak burst just behind the plate, no blast
            var weak = t;
            (weak.BurstT, weak.Force, weak.Burst) = (xs[t.Plate].T + 0.1, s.BrokenShare, BurstKind.Inside);
            Burst(c, k, hit, path, weak, s, w, size, ref res, ref r, broken: true);
        }
        // a magazine set off: its race and the flash through its trains are the damage system's to run
        // (Magazines.SetOff, docs/battle/magazines.md V3)
        if (res.Magazine)
            res.Flash = true;
        Casualties.Draw(c, k, hit.Ms, s, ref res, ref r);
        return res;
    }

    /// <summary>The debris a plate the shell got through throws behind it (E6; 14 §3.1): about the axis halfway between the
    /// plate's normal and the shot line, as spall's, at the shell's striking speed (what it had left there isn't kept: an
    /// upper bound).</summary>
    static SpallCloud Debris(ShipClass c, HitPath path, in Crossing x, double calibreMm, double v)
    {
        var d = path.LegOf(path.Leg(x.T)).Direction;
        var n = x.Normal;
        if (n.X * d.X + n.Y * d.Y + n.Z * d.Z < 0)
            n = new P3(-n.X, -n.Y, -n.Z);
        double ax = n.X + d.X, ay = n.Y + d.Y, az = n.Z + d.Z, al = Math.Sqrt(ax * ax + ay * ay + az * az);
        var axis = al > 1e-9 ? new P3(ax / al, ay / al, az / al) : d;
        var origin = new P3(x.Point.X + 0.05 * axis.X, x.Point.Y + 0.05 * axis.Y, x.Point.Z + 0.05 * axis.Z);
        ref readonly var mat = ref c.MaterialOf(x.Material);
        return Combat.Spall.Perforated(origin, axis, calibreMm, mat.SteelMm(x.Mm), v, mat.BigShare);
    }

    /// <summary>The group a plate's debris flies into (E6): the part a crossing enters through it, else the space or part the
    /// line enters next, within 2 m; -1 for the open.</summary>
    static int Into(ShipClass c, ReadOnlySpan<Crossing> xs, int i)
    {
        if (xs[i].Kind == CrossingKind.PartIn)
            return Casualties.Part(xs[i].Index);
        for (int j = i + 1; j < xs.Length && xs[j].T <= xs[i].T + 2; j++)
            if (xs[j].Kind == CrossingKind.CellIn)
                return Casualties.Cell(c, xs[j].Index);
            else if (xs[j].Kind == CrossingKind.PartIn)
                return Casualties.Part(xs[j].Index);
        return -1;
    }

    /// <summary>The shock of a heavy hit that held, through the structure (E4; damage/13 §3.4): by the ship's shock grade
    /// and the impulse, each row a draw, its outage one length for all it reaches. Power (with director fire control
    /// only): tripped in zone A (the struck part, and what's within <see cref="SimSettings.ShockZoneAM"/> of the hit), a
    /// section's in B (within <see cref="SimSettings.ShockZoneBM"/>), the whole ship's cascading out; mounts and directors
    /// are silent while it's off. A director's gyro in A or B; a rangefinder in A out of adjustment. Gear the sim doesn't
    /// carry yet (the gyrocompass, radar, phones, cast iron) can't fail.</summary>
    static void Shock(ShipClass c, ShipCondition k, P3 at, int struck, double impulse, long now, SimSettings s, ref EffectsResult res,
        ref RandomStream r)
    {
        double g = NonPen.ShockK(impulse, s) * s.ShockGradeK[(int)c.ShockGrade];
        int col = c.ShockGrade == ShockGrade.G0 ? 0 : 1;
        if (NonPen.Electrics(c))
        {
            if (r.NextDouble() < s.ShockLocalTrip[col] * g)
                res.Shock |= PowerOut(c, k, at, struck, -1, s.ShockZoneAM, NonPen.Minutes(s.LocalTripMin, ref r), now);
            if (r.NextDouble() < s.ShockSectionPower[col] * g)
                res.Shock |= PowerOut(c, k, at, struck, s.ShockZoneAM, s.ShockZoneBM, NonPen.Minutes(s.SectionPowerMin, ref r), now);
            if (r.NextDouble() < s.ShockCascade[col] * g)
            {
                res.Shock |= PowerOut(c, k, at, -1, -1, double.PositiveInfinity, NonPen.Minutes(s.CascadeMin, ref r), now);
                res.Blackout = true;
            }
            if (r.NextDouble() < s.ShockDirectorGyro[col] * g)
            {
                long ms = NonPen.Minutes(s.DirectorGyroMin, ref r);
                for (int d = 0; d < c.Directors.Length; d++)
                    if (c.Directors[d].Component == struck || Distance(c, c.Directors[d].Component, at) <= s.ShockZoneBM)
                    {
                        k.Out(k.DirectorDown, d, now, ms);
                        res.Shock = true;
                    }
            }
        }
        if (r.NextDouble() < s.ShockRangefinder[col] * g)
        {
            long ms = NonPen.Minutes(s.RangefinderMin, ref r);
            for (int e = 0; e < c.Eyes.Length; e++)
                if (c.Eyes[e].RangefinderM > 0 && (c.Eyes[e].Component == struck || Distance(c, c.Eyes[e].Component, at) <= s.ShockZoneAM))
                {
                    k.Out(k.EyeDown, e, now, ms);
                    res.Shock = true;
                }
        }
    }

    /// <summary>The power off for ms (E4's shock) to the mounts and directors from <paramref name="from"/> (exclusive; -1
    /// takes in the struck part's too) to <paramref name="to"/> metres of a point, and to the struck part's own (its
    /// mount, a conning tower's directors): mounts down, directors out. Whether it reached any.</summary>
    public static bool PowerOut(ShipClass c, ShipCondition k, P3 at, int struck, double from, double to, long ms, long now)
    {
        bool any = false;
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var mr = ref c.Mounts[m];
            double d = mr.Component == struck || mr.Barbette == struck ? 0 : Distance(c, mr.Component, at);
            if (d <= to && (from < 0 || d > from))
            {
                k.Down(mr.Component, now, ms);
                any = true;
            }
        }
        for (int i = 0; i < c.Directors.Length; i++)
        {
            double d = c.Directors[i].Component == struck ? 0 : Distance(c, c.Directors[i].Component, at);
            if (d <= to && (from < 0 || d > from))
            {
                k.Out(k.DirectorDown, i, now, ms);
                any = true;
            }
        }
        return any;
    }

    /// <summary>A belt that held still leaks (E4; damage/13 §2, <c>belt_leak</c>): the plate driven in, its bolts pulled,
    /// its skin's rivets sheared, by where it struck against the waterline now, a joint, a leak there before (the plate
    /// loosened) and a welded skin; the regime's opening into the cell behind it, a hole like any other; into a fuel tank
    /// or the double bottom, the nearest boiler or engine room's supply fouled for a while. A belt inside the hull leaks
    /// only with the sea already in front of it (the space before it holed).</summary>
    static void Belt(ShipClass c, ShipCondition k, HitPath path, int held, double impulse, double margin, in Waterplane water, long now,
        SimSettings s, ref EffectsResult res, ref RandomStream r)
    {
        var xs = path.Crossings;
        ref readonly var x = ref xs[held];
        ref readonly var row = ref c.SidePlates[x.Index];
        int behind = -1, before = -1;
        for (int j = held + 1; j < xs.Length && xs[j].T <= x.T + s.BeltBehindM; j++)
            if (xs[j].Kind == CrossingKind.CellIn)
            {
                behind = xs[j].Index;
                break;
            }
        for (int j = held - 1; j >= 0; j--)
            if (xs[j].Kind is CrossingKind.CellIn or CrossingKind.CellOut)
            {
                before = xs[j].Kind == CrossingKind.CellIn ? xs[j].Index : -1;
                break;
            }
        if (behind < 0 || before >= 0 && !(k.BreachM2[before] > 0))
            return;
        double wl = water.At(x.Point.X, x.Point.Y), dz = x.Point.Z - wl;
        int zone = dz > s.BeltWaterlineM ? 1 : x.Point.Z - row.Bottom <= s.BeltLowerEdgeM ? 2 : 0;
        double along = Math.Abs(x.Point.X - row.X0) % s.BeltPlateM[0], up = Math.Max(x.Point.Z - row.Bottom, 0) % s.BeltPlateM[1];
        bool joint = Math.Min(along, s.BeltPlateM[0] - along) <= s.BeltJointM || Math.Min(up, s.BeltPlateM[1] - up) <= s.BeltJointM;
        bool repeat = k.Loosened(x.Index, x.Point, s.BeltRepeatM);
        var (regime, a) = NonPen.BeltLeak((BeltZone)zone, impulse, margin, joint, repeat, c.ShellJoint == Joint.Welded, s, ref r);
        if (regime == LeakRegime.None)
            return;
        k.Hole(behind, a, x.Point, seam: regime is LeakRegime.Seep or LeakRegime.Moderate);   // rivets and seams; worse, the plate dished or loose
        k.Leaked(x.Index, x.Point);
        res.BreachM2 += a;
        res.Leak = regime;
        // feed water or oil behind it: the supply of the nearest boiler or engine room fouled for a while
        int room = c.Cells[behind].Room;
        if (room >= 0 && c.Rooms[room].Kind is RoomKind.FuelTank or RoomKind.DoubleBottom && r.NextDouble() < s.FeedCasualtyChance)
        {
            int near = -1;
            double best = double.MaxValue, mid = x.Point.X;
            for (int i = 0; i < c.Rooms.Length; i++)
                if (c.Rooms[i].Kind is RoomKind.BoilerRoom or RoomKind.EngineRoom &&
                    Math.Abs((c.Rooms[i].X0 + c.Rooms[i].X1) / 2 - mid) is double d && d < best)
                    (near, best) = (i, d);
            if (near >= 0)
            {
                double m0 = s.FeedCasualtyMin[0], m1 = s.FeedCasualtyMin[1];
                k.Down(c.RoomNode(near), now, (long)(60_000 * Math.Exp(Math.Log(m0) + (Math.Log(m1) - Math.Log(m0)) * r.NextDouble())));
                res.Feed = true;
            }
        }
    }

    /// <summary>A burst's wreck radius, m: parts within it are wrecked or put out of action by distance (cube-root
    /// scaled by its filler's TNT equivalent, times the root of its force).</summary>
    public static double WreckRadiusM(in ShellTypeRow shell, double force, SimSettings s) =>
        s.WreckRadiusK * Math.Cbrt(Math.Max(shell.FillerKg * shell.FillerRe, 0)) * Math.Sqrt(force);

    /// <summary>A burst at its point along the path, with its force. Broken (E2): a shell's pieces through a plate it
    /// holed, a weak burst of its fragments at the force's share and nothing else: no blast, no fire; the part they're in takes them, and an enclosed turret or barbette they're in is a flash source.</summary>
    static void Burst(ShipClass c, ShipCondition k, in Hit hit, HitPath path, in TerminalResult t, SimSettings s,
        double w, double size, ref EffectsResult res, ref RandomStream r, bool broken = false)
    {
        var shell = hit.Shell;
        var xs = path.Crossings;
        var p = path.At(t.BurstT);
        double force = t.Force, rw = broken ? 0 : WreckRadiusM(shell, force, s);
        double share = broken ? force : 1;   // the fragments' share of a full burst's
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
        var comps = c.Components;
        int magazine = -1, turret = -1, turretSt = -1;   // the magazine room it burst in or beside; the mount it burst in, its station
        int readyMount = -1, readySt = -1;   // a mount whose ready charges it lit at a station of its train
        var via = MagazineVia.None;

        // the part it burst in: wrecked; or the part its pieces got into, struck by them from inside its plates
        if (part >= 0)
        {
            res.Part = part;
            if (!broken)
                Count(ref res, k, part, k.Worsen(part, PartState.Destroyed) ? PartState.Destroyed : PartState.Ok);
            else if (frag is not null)
            {
                double inside = share * frag.Hits(p, fdir, t.BurstMs, comps[part].Bounds, 0);
                if (inside > 1e-4 && r.NextDouble() < 1 - Math.Exp(-inside * s.PartVital[Math.Min((int)comps[part].Kind, s.PartVital.Length - 1)]))
                    Count(ref res, k, part, k.Harm(part) ? k.State[part] : PartState.Ok);
            }
            if (c.MountOf(part) is int m and >= 0 && c.StationOfPart(m, part) is var st and >= 0)
                (turret, turretSt) = (m, st);   // a gunhouse, its working chamber, a casemate (docs/battle/magazines.md V2)
        }

        // parts: within the wreck radius wrecked or out of action, unless armour its fragments don't get through boxes
        // them; beyond it, damaged by the fragments that get into them: in the hull those in the burst's own space (a
        // space's next door, through its join, below), outside it those on deck
        var cellBox = inHull ? c.Cells[cell].Box : default;
        // burst inside a part (a gunhouse, a barbette, a superstructure block), its fragments leave through its own walls
        double around = part >= 0 ? PartMild(c, part, Armour(c, part)) : 0;
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
            double into = share * (frag?.Hits(p, fdir, t.BurstMs, comp.Bounds, around + PartMild(c, i, raw), default,
                inHull ? MediumDragM(c, k, cell, p, Nearest(comp.Bounds, p), s) : 0) ?? 0);
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
                // its ready charges burn as a propellant seat (fire.md B1): every one, by its own oxygen, in seconds
                double charges = c.Mounts[m].ReadyRounds * c.Charges[c.Mounts[m].Type].MassKg * s.FirePropellantMJkg;
                res.Fire |= Fires.Ignite(c, k, c.FireOfPart(i), s.ReadyAmmoSeatMW, s, charges);
                // its ready charges flash through its crew (16 §4.6): a gunhouse's as a turret's train, else as a battery's
                Casualties.Flash(k, Casualties.Part(i), Casualties.Enclosed(c, m), s);
                res.Flash = true;
                // charges burning at a station of its train: the flame's way down it (a shield's are on deck: none)
                if (readyMount < 0 && c.StationOfPart(m, i) is var st and >= 0)
                    (readyMount, readySt) = (m, st);
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
                    continue;   // its fragments missed what matters in it (the gas below may not)
                Count(ref res, k, node, k.Harm(node) ? k.State[node] : PartState.Ok);
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
                // the coal, oil or water they go through in the burst's own space on the way (C3b)
                double drag = MediumDragM(c, k, cell, p, Nearest(there, p), s);
                double into = share * (frag?.Hits(p, fdir, t.BurstMs, there, join, normal, drag) ?? 0);
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
                    double hits = share * (frag?.Hits(p, fdir, t.BurstMs, comp.Bounds, join + PartMild(c, j, Armour(c, j)), normal, drag) ?? 0);
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

        // the gas (E5): in a closed space it stays until it vents or the walls give way; on deck, in an open mount or
        // vented as fast as it forms there's only the shock and the fragments above
        int gasMagazine = -1, gasHandling = -1;
        bool inHandling = false;
        if (!broken && t.Burst != BurstKind.Outside && shell.FillerKg > 0)
        {
            double fill = shell.FillerKg * force, vent = NearWall(c, xs, t.BurstT, size, s);
            if (part >= 0)
                res.Blast = PartGas(c, k, part, fill, shell, vent, s, ref res);
            else if (inHull)
            {
                res.Blast = Blast.Cascade(c, k, cell, p, fill, shell, vent, rw, s.NearHoleK * size, s, ref r);
                res.BreachM2 += res.Blast.HoleM2;
                Gas(c, k, cell, part, fill * s.HotProductsM3Kg, s, ref res, ref gasMagazine, ref gasHandling, ref inHandling);
            }
        }

        // the people its pieces and gas reach (E6)
        Crew(c, k, path, t, part, cell, p, fdir, frag, share, around, broken ? 0 : w * force, broken, res.Blast, s);

        // fire, by what burns where it burst (docs/battle/fire.md B0): the part it burst in, its cell, or outside the part
        // its fragments reached first, else the weather deck it burst on (not one on the side below it); and a burst above
        // the hull, from just under the deck's edge to a few metres over it, also showers that section's weather deck with
        // its burning pieces and products, as one outside (B5: what's piled there)
        int space = part >= 0 ? c.FireOfPart(part) : inHull ? cell
            : res.Part >= 0 && res.Part < c.Components.Length && c.FireOfPart(res.Part) >= 0 ? c.FireOfPart(res.Part)
            : c.FireOfDeck(c.SectionAt(p.X));
        int deck = inHull ? -1 : c.FireOfDeck(c.SectionAt(p.X));
        double deckTop = deck >= 0 ? c.Cells[c.FireSpaces[deck].Cell].Box.Top : 0;
        if (deck >= 0 && (p.Z < deckTop - s.FireDeckBurstM || p.Z > deckTop + s.FireDeckReachM))
            deck = -1;
        if (space >= 0 && c.FireSpaces[space].Kind == FireSpaceKind.Deck && deck < 0)
            space = -1;
        if (!broken)
        {
            if (space >= 0)
                res.Fire |= Start(c, k, space, shell, w, t.Burst == BurstKind.Outside, s, ref r);
            if (deck >= 0 && deck != space)
                res.Fire |= Start(c, k, deck, shell, w, true, s, ref r);
        }

        // the magazine (docs/battle/magazines.md V2): a burst among its charges, or in it beside them, or its gas reaching one;
        // else charges lit at a station of a mount's train (ready charges set burning, a burst in a gunhouse, its working
        // chamber or a casemate, its gas in a handling room: E5's Savannah) and the flame down the train's open doors
        int reached = -1;
        if (t.Burst != BurstKind.Outside && magazine < 0 && gasMagazine >= 0)
            (magazine, via) = (gasMagazine, MagazineVia.Gas);
        if (t.Burst != BurstKind.Outside && magazine >= 0)
        {
            if (r.NextDouble() < s.MagazineFlashChance)
                (reached, res.Stations) = (magazine, res.Stations + 1);
        }
        else if (readyMount >= 0)
            (reached, via) = (Magazines.Light(c, k, readyMount, readySt, burning: true, s, ref r, ref res, hit.Ms), MagazineVia.Turret);
        else if (t.Burst != BurstKind.Outside && turret >= 0)
        {
            if (r.NextDouble() < s.StationLightChance[(int)c.Train(turret)[turretSt].Kind])
                (reached, via) = (Magazines.Light(c, k, turret, turretSt, burning: false, s, ref r, ref res, hit.Ms), MagazineVia.Turret);
        }
        else if (t.Burst != BurstKind.Outside && gasHandling >= 0 && c.MountOf(c.Rooms[gasHandling].Mount) is int hm and >= 0 &&
                 c.StationOfRoom(hm, gasHandling) is var hs and >= 0 && r.NextDouble() < s.StationLightChance[(int)StationKind.Handling])
            (reached, via) = (Magazines.Light(c, k, hm, hs, burning: false, s, ref r, ref res, hit.Ms), inHandling ? MagazineVia.Handling : MagazineVia.Gas);
        if (reached >= 0)
            (res.Magazine, res.MagazineRoom, res.MagazineVia) = (true, reached, via);
    }

    /// <summary>A burst's fire start in a space: 08's chance by kind as a rate (λ = -ln(1 - p)), times its filler's fire over
    /// the reference's (12 §4.1), the space's load and the burst's size (and the outside share for one outside it): 1 - e^-λ
    /// (fire_ref.py's p_fire), so the chances saturate (docs/battle/fire.md B5). Whether it lit a new fire.</summary>
    static bool Start(ShipClass c, ShipCondition k, int space, in ShellTypeRow shell, double w, bool outside, SimSettings s, ref RandomStream r)
    {
        int kind = Math.Clamp((int)shell.Kind, 0, s.FireChance.Length - 1);
        double load = Math.Min(s.FireStartLoadCap, Fires.Load(c.FireSpaces[space], k) / s.FireStartLoadMJm2);
        double filler = shell.FillerFire > 0 ? shell.FillerFire / s.FireFillerRef : 1;
        double lambda = -Math.Log(1 - s.FireChance[kind]) * filler * load * Math.Min(1.5, 0.5 + 0.5 * Math.Cbrt(w / 10)) *
                        (outside ? s.FireOutsideShare : 1);
        return r.NextDouble() < 1 - Math.Exp(-lambda) && Fires.Ignite(c, k, space, s.FireSeatMW[Math.Min(kind, s.FireSeatMW.Length - 1)], s);
    }

    /// <summary>The people a burst's pieces and gas reach (E6; damage/16 §4.1, §4.5): the men in the part or space it burst
    /// in, in the spaces next to it through their decks and bulkheads (and in the space behind the hull's plating a burst
    /// outside was on), and in the parts its pieces reach (in the hull those in its space, outside it those above the
    /// deck), each through what's between: a part's own plates and armour (an open or shielded mount's men stand open,
    /// their cover says how much the shield hides them). A burst on a plate throws half its pieces into it. Its gas on the
    /// men in every space it filled, or in the part it burst in; in the open, its blast by distance (TNT kg w).</summary>
    static void Crew(ShipClass c, ShipCondition k, HitPath path, in TerminalResult t, int part, int cell, P3 p, P3 fdir, FragmentBurst? frag,
        double share, double around, double w, bool broken, in BlastResult bl, SimSettings s)
    {
        var xs = path.Crossings;
        bool inHull = cell >= 0;
        double v = t.BurstMs, sh = share * (t.Burst is BurstKind.Outside or BurstKind.InPlate ? s.ContactShare : 1);
        double openW = broken || !(bl.Spaces == 0 || bl.Open) ? 0 : w;
        if (frag is not null)
        {
            if (part >= 0)
                Casualties.Pieces(c, k, Casualties.Part(part), frag, p, fdir, v, sh, 0, default, openW, s);
            if (inHull)
            {
                var here = c.Cells[cell].Box;
                Casualties.Pieces(c, k, Casualties.Cell(c, cell), frag, p, fdir, v, sh, around, default, part < 0 ? openW : 0, s);
                foreach (var n in c.Neighbours(c.Cells[cell].Neighbours))
                {
                    var there = c.Cells[n.Cell].Box;
                    double join = n.Join switch
                    {
                        JoinKind.Deck => DeckMild(c, n.Through, here, there),
                        JoinKind.Bulkhead => c.Bulkheads[n.Through].PlateMm + Fragments.MildMm(c.Bulkheads[n.Through].ArmourMm, FragmentClass.Ductile),
                        _ => 0,
                    } + InclinedMild(c, p, Nearest(there, p));
                    var normal = n.Join == JoinKind.Deck ? new P3(0, 0, 1) : Touch(here, there) ? new P3(0, 1, 0) : new P3(1, 0, 0);
                    Casualties.Pieces(c, k, Casualties.Cell(c, n.Cell), frag, p, fdir, v, sh, around + join, normal,
                        n.Join == JoinKind.Open && part < 0 ? openW : 0, s);
                }
            }
            else if (t.Burst == BurstKind.Outside && t.Plate >= 0 && xs[t.Plate].Kind == CrossingKind.HullIn && CellAt(xs, t.Plate, true) is int behind and >= 0)
            {
                // on the hull's plating: of the half going into it, those that get through into the space behind
                ref readonly var x = ref xs[t.Plate];
                Casualties.Pieces(c, k, Casualties.Cell(c, behind), frag, p, fdir, v, sh, c.MaterialOf(x.Material).FragmentMildMm(x.Mm), x.Normal, 0, s);
            }
            var comps = c.Components;
            var cellBox = inHull ? c.Cells[cell].Box : default;
            for (int i = 0; i < comps.Length; i++)
            {
                ref readonly var comp = ref comps[i];
                if (i == part || comp.BattleCrew <= 0 || (inHull ? comp.Bounds.Base >= cellBox.Top - 0.05 || !Overlaps(comp.Bounds, cellBox) : comp.Bounds.Top <= 0))
                    continue;
                int m = c.MountOf(i);
                double mild = around + PartMild(c, i, m >= 0 && c.Mounts[m].Component == i && !Casualties.Enclosed(c, m) ? 0 : Armour(c, i));
                Casualties.Pieces(c, k, Casualties.Part(i), frag, p, fdir, v, sh, mild, default, !inHull && mild <= 0 ? openW : 0, s);
            }
        }
        if (broken || !(bl.FeltMPa > 0))
            return;
        if (part >= 0)
            Casualties.Gas(k, Casualties.Part(part), bl.FeltMPa, s);
        else if (inHull)
            for (int i = 0; i < bl.Spaces; i++)
                Casualties.Gas(k, Casualties.Cell(c, k.BlastRegion[i]), k.BlastFelt[k.BlastRegion[i]], s);
    }

    /// <summary>A burst against the plating it came through (within <see cref="NearWallM"/> of it: a nose fuze just
    /// inside a thin side, a burst in the plate) vents through its own hole (11 §4.1, §5.1): the contact hole's area, m².</summary>
    static double NearWall(ShipClass c, ReadOnlySpan<Crossing> xs, double burstT, double size, SimSettings s)
    {
        for (int i = xs.Length - 1; i >= 0; i--)
        {
            ref readonly var x = ref xs[i];
            if (x.T > burstT)
                continue;
            if (burstT - x.T > NearWallM)
                break;
            if (x.Kind is CrossingKind.HullIn or CrossingKind.HullOut or CrossingKind.PartIn && x.Mm > 0)
            {
                double hole = s.HoleRadiusK * size * Math.Sqrt(6 / Math.Max(x.Mm, 1));
                return Math.PI * hole * hole;
            }
        }
        return 0;
    }

    const double NearWallM = 2;

    /// <summary>A burst inside a part (a gunhouse, a conning tower, a superstructure block, a barbette; E5): its gas in the
    /// part's free volume, vented by its kind's openings (an open mount, a mast, a funnel: open) and its own hole, its
    /// walls torn open if they can't hold it (a vent then); what's in the part with it takes the gas.</summary>
    static BlastResult PartGas(ShipClass c, ShipCondition k, int part, double fill, in ShellTypeRow shell, double vent, SimSettings s,
        ref EffectsResult res)
    {
        ref readonly var comp = ref c.Components[part];
        var b = comp.Bounds;
        double dx = b.X1 - b.X0, dy = b.Y1 - b.Y0, dz = b.Top - b.Base;
        double v = Math.Max(dx * dy * dz * s.PartAirShare, 1), face = Math.Pow(v, 2.0 / 3);
        int m = c.MountOf(part);
        double share = m >= 0 && c.Mounts[m].Component == part && !c.Enclosed(m) ? 1 : s.PartVentShare[Math.Min((int)comp.Kind, s.PartVentShare.Length - 1)];
        double a = share * face + vent;   // a share of 1 and over: open (α·A ≥ 0.6·V^⅔)
        var bl = new BlastResult { Spaces = 1, VolumeM3 = v, Rounds = 1 };
        double p = Blast.Pressure(shell, fill, v, s), felt = Blast.Felt(p, v, a, s);
        (bl.FirstMPa, bl.FeltMPa, bl.LastMPa, bl.Open) = (p, felt, p, felt <= 0);
        if (felt <= 0)
            return bl;
        // its walls: its armour, or its plating
        double wall = Armour(c, part) + PartMild(c, part, 0);
        if (felt >= s.RuptureMPaMm * Math.Max(wall, 1))
        {
            bl.Ruptured = 1;
            felt = Blast.Felt(p, v, a + 2 * (dx * dy + dx * dz + dy * dz), s);
            bl.Rounds = 2;
        }
        bl.FeltMPa = Math.Max(felt, 0);
        // what's in it with it: a director in a superstructure block, the conning tower's gear
        for (int i = 0; i < c.Components.Length; i++)
            if (i != part && Overlaps(c.Components[i].Bounds, b))
                Pressed(c, k, i, s.PartBlastMPa[Math.Min((int)c.Components[i].Kind, s.PartBlastMPa.Length - 1)], bl.FeltMPa, s, ref res);
        return bl;
    }

    /// <summary>What the gas does in the spaces it reached (E5; 11 §4.3): their rooms' gear and the parts in them take it by
    /// their pressure class (out of action from it, damaged from a share of it); a part behind armour the gas can't burst
    /// keeps it out. A magazine (other than the burst's own, the burst's roll) or a handling room it reached while still
    /// carrying flame (the burst in it, or its hot products, m³, filling the spaces reached before it) is a flash
    /// source.</summary>
    static void Gas(ShipClass c, ShipCondition k, int burstCell, int part, double hotM3, SimSettings s, ref EffectsResult res, ref int magazine,
        ref int handling, ref bool inHandling)
    {
        double best = 0;
        for (int i = 0; i < res.Blast.Spaces; i++)
        {
            int cell = k.BlastRegion[i];
            double felt = k.BlastFelt[cell];
            if (!(felt > 0))
                continue;
            foreach (int room in c.RoomsIn(cell))
            {
                var kind = c.Rooms[room].Kind;
                Pressed(c, k, c.RoomNode(room), s.RoomBlastMPa[Math.Min((int)kind, s.RoomBlastMPa.Length - 1)], felt, s, ref res);
                bool hot = cell == burstCell || k.BlastBefore[cell] < hotM3;
                if (kind == RoomKind.Magazine && cell != burstCell && hot && felt > best)
                    (magazine, best) = (room, felt);
                else if (kind == RoomKind.Handling && hot && handling < 0 && c.Rooms[room].Mount >= 0)
                    (handling, inHandling) = (room, cell == burstCell);
            }
        }
        var comps = c.Components;
        for (int j = 0; j < comps.Length; j++)
        {
            if (j == part)
                continue;
            double felt = 0;
            for (int i = 0; i < res.Blast.Spaces; i++)
            {
                int cell = k.BlastRegion[i];
                if (k.BlastFelt[cell] > felt && Overlaps(comps[j].Bounds, c.Cells[cell].Box))
                    felt = k.BlastFelt[cell];
            }
            if (felt > 0 && felt >= s.RuptureMPaMm * Armour(c, j))
                Pressed(c, k, j, s.PartBlastMPa[Math.Min((int)comps[j].Kind, s.PartBlastMPa.Length - 1)], felt, s, ref res);
        }
    }

    /// <summary>A node's gear under a gas pressure: out of action from its class's, damaged from a share of it.</summary>
    static void Pressed(ShipClass c, ShipCondition k, int node, double classMPa, double felt, SimSettings s, ref EffectsResult res)
    {
        var state = felt >= classMPa ? PartState.Disabled : felt >= s.BlastDamagedShare * classMPa ? PartState.Damaged : PartState.Ok;
        if (state != PartState.Ok)
            Count(ref res, k, node, k.Worsen(node, state) ? state : PartState.Ok);
    }

    /// <summary>The spall off the back of a plate that held (E3; damage/14 §6): its cloud (<see cref="SpallCloud"/>) thinned
    /// by the layers right behind the plate (backing, the inner skin), into the space behind it, and no further than its
    /// boundaries (5 mm stops the flakes; a chunk THOR lets through goes on into the space next door). In it, the parts
    /// and the spaces' vitals take the flakes over their fragility, the big piece the nearest part within 15° of the axis.
    /// Where it reaches charges (an enclosed turret's gunhouse or working chamber, a handling room, a magazine) it may
    /// light them (<see cref="SimSettings.SpallFlashChance"/>, 14 §6.3), a flash source for the magazine chain as E2's
    /// pieces are. How the charges are packed and handled (bags, cases open or closed, brass; flash discipline) isn't in
    /// the model yet: the magazine chain's session. People are E6's. Not a burst: no blast, no fire, no breach.</summary>
    static void Spall(ShipClass c, ShipCondition k, HitPath path, int plate, double severity, bool roof, in ShellTypeRow shell, double v,
        SimSettings s, ref EffectsResult res, ref RandomStream r)
    {
        var xs = path.Crossings;
        ref readonly var x = ref xs[plate];
        var d = path.LegOf(path.Leg(x.T)).Direction;
        var n = x.Normal;
        if (n.X * d.X + n.Y * d.Y + n.Z * d.Z < 0)
            n = new P3(-n.X, -n.Y, -n.Z);
        double ax = n.X + d.X, ay = n.Y + d.Y, az = n.Z + d.Z, al = Math.Sqrt(ax * ax + ay * ay + az * az);
        var axis = al > 1e-9 ? new P3(ax / al, ay / al, az / al) : n;
        var origin = new P3(x.Point.X + 0.05 * axis.X, x.Point.Y + 0.05 * axis.Y, x.Point.Z + 0.05 * axis.Z);
        double ob = Terminal.Obliquity(d, x.Normal);
        ref readonly var mat = ref c.MaterialOf(x.Material);
        var cloud = roof ? Combat.Spall.Roof(origin, axis, shell.CalibreMm, x.Mm, v, ob, mat.BigShare)
            : Combat.Spall.Stopped(origin, axis, shell.CalibreMm, x.Mm, v, ob, severity, mat.BigShare);
        // the layers right behind it, and the space it flies into
        double backing = 0;
        int cell = -1;
        for (int i = plate + 1; i < xs.Length && xs[i].T <= x.T + 1.5; i++)
        {
            if (xs[i].Kind == CrossingKind.CellIn)
            {
                cell = xs[i].Index;
                break;
            }
            if (xs[i].Plate != PlateKind.None && xs[i].Mm > 0)
                backing += c.MaterialOf(xs[i].Material).SteelMm(xs[i].Mm);
        }
        cloud = Combat.Spall.Backed(cloud, x.Mm, backing);
        if (!(cloud.Flakes > 0) && !(cloud.BigKg > 0))
            return;
        res.Spall = true;
        int open = -1, openSt = -1, magazine = -1;   // the mount and the station of its train whose charges the cloud reaches; a magazine it reaches
        var comps = c.Components;
        // a gunhouse, a working chamber, a conning tower: the whole cloud inside the part, its contents take it
        int space = cell < 0 && x.Plate is PlateKind.TurretFace or PlateKind.TurretSide or PlateKind.TurretRear or PlateKind.TurretRoof
            or PlateKind.Barbette or PlateKind.PartArmour ? x.Index : -1;
        // the men in it (E6; 14 §5.1)
        if (space >= 0 || cell >= 0)
            Casualties.Spall(c, k, space >= 0 ? Casualties.Part(space) : Casualties.Cell(c, cell), cloud, s);
        if (space >= 0)
        {
            if (res.Part < 0)
                res.Part = space;
            double fragility = PartFragility(c, space, s);
            double hits = Combat.Spall.Counted(cloud, fragility) * PartVital(c, space, s) + (cloud.BigJ >= fragility ? 1 : 0);
            if (hits > 1e-4 && r.NextDouble() < 1 - Math.Exp(-hits))
                Harm(ref res, k, space);
            if (c.MountOf(space) is int m and >= 0 && c.StationOfPart(m, space) is var st and >= 0)
                (open, openSt) = (m, st);
        }
        else if (cell >= 0)
        {
            var box = c.Cells[cell].Box;
            int big = -1;
            double bigR = double.PositiveInfinity, bigCone = double.DegreesToRadians(Combat.Spall.BigConeDeg);
            for (int i = 0; i < comps.Length; i++)
            {
                ref readonly var comp = ref comps[i];
                if (!Overlaps(comp.Bounds, box))
                    continue;
                double into = Combat.Spall.Hits(cloud, comp.Bounds, PartFragility(c, i, s)) * PartVital(c, i, s);
                if (into > 1e-4 && r.NextDouble() < 1 - Math.Exp(-into))
                    Harm(ref res, k, i);
                // the big piece's mark: the nearest part near the axis
                var b = comp.Bounds;
                double rx = (b.X0 + b.X1) / 2 - origin.X, ry = (b.Y0 + b.Y1) / 2 - origin.Y, rz = (b.Base + b.Top) / 2 - origin.Z;
                double rc = Math.Sqrt(rx * rx + ry * ry + rz * rz), dist = BoxDistance(b, origin);
                if (rc > 1e-6 && Combat.Spall.Theta(cloud, rx / rc, ry / rc, rz / rc) <= bigCone && dist < bigR)
                    (big, bigR) = (i, dist);
            }
            // the space's own vitals: the whole cloud lands in it
            double counted = Combat.Spall.Counted(cloud, s.RoomFragilityJ);
            foreach (int room in c.RoomsIn(cell))
            {
                ref readonly var rr = ref c.Rooms[room];
                int node = c.RoomNode(room);
                if (res.Part < 0)
                    res.Part = node;
                double into = counted * Vital(c, room, s) + (big < 0 && cloud.BigJ >= s.RoomFragilityJ ? Vital(c, room, s) : 0);
                if (into > 1e-4 && r.NextDouble() < 1 - Math.Exp(-into))
                    Harm(ref res, k, node);
                if (rr.Kind == RoomKind.Handling && rr.Mount >= 0 && c.MountOf(rr.Mount) is int m and >= 0 && c.StationOfRoom(m, room) is var st and >= 0)
                    (open, openSt) = (m, st);   // a shield's handling room too: its train starts there (decision 4)
                else if (rr.Kind == RoomKind.Magazine && magazine < 0)
                    magazine = room;
            }
            if (big >= 0 && cloud.BigJ >= PartFragility(c, big, s))
                Count(ref res, k, big, k.Worsen(big, PartState.Disabled) ? PartState.Disabled : PartState.Ok);
            // through the space's boundaries only what THOR lets through them (5 mm stops the flakes, 14 §4.3)
            foreach (var nb in c.Neighbours(c.Cells[cell].Neighbours))
            {
                double join = nb.Join switch
                {
                    JoinKind.Deck => DeckMild(c, nb.Through, box, c.Cells[nb.Cell].Box),
                    JoinKind.Bulkhead => c.Bulkheads[nb.Through].PlateMm + Fragments.MildMm(c.Bulkheads[nb.Through].ArmourMm, FragmentClass.Ductile),
                    _ => 0,
                };
                if (join >= s.SpallStopMm && Fragments.LimitMs(cloud.FlakeKg, join) >= cloud.AxialMs)
                    continue;
                foreach (int room in c.RoomsIn(nb.Cell))
                {
                    double into = s.NeighbourShare * counted * Vital(c, room, s);
                    if (into > 1e-4 && r.NextDouble() < 1 - Math.Exp(-into))
                        Harm(ref res, k, c.RoomNode(room));
                    if (c.Rooms[room].Kind == RoomKind.Magazine && magazine < 0)
                        magazine = room;
                }
            }
        }
        // charges in the cloud's way may light (14 §6.3: about 20% at s >= 0.5, less below; Dunkerque lit, Tiger didn't),
        // a flash source for the magazine chain as E2's pieces are: in a magazine its own chance, in a turret or handling
        // room the turret's
        double lights = s.SpallFlashChance * Math.Min(1, cloud.Severity / 0.5);
        if (magazine >= 0 && !res.Magazine && r.NextDouble() < lights * s.MagazineFlashChance)
            (res.Magazine, res.MagazineRoom, res.MagazineVia, res.Stations) = (true, magazine, MagazineVia.Spall, res.Stations + 1);
        if (open >= 0 && !res.Magazine && r.NextDouble() < lights * s.StationLightChance[(int)c.Train(open)[openSt].Kind] &&
            Magazines.Light(c, k, open, openSt, burning: false, s, ref r, ref res) is int reached and >= 0)
            (res.Magazine, res.MagazineRoom, res.MagazineVia) = (true, reached, MagazineVia.Spall);
    }

    internal static void Harm(ref EffectsResult res, ShipCondition k, int node) => Count(ref res, k, node, k.Harm(node) ? k.State[node] : PartState.Ok);

    internal static double PartVital(ShipClass c, int i, SimSettings s) => s.PartVital[Math.Min((int)c.Components[i].Kind, s.PartVital.Length - 1)];

    static double PartFragility(ShipClass c, int i, SimSettings s) => s.PartFragilityJ[Math.Min((int)c.Components[i].Kind, s.PartFragilityJ.Length - 1)];

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
            if (x.Kind is CrossingKind.HullIn or CrossingKind.HullOut)
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

    internal static double Vital(ShipClass c, int room, SimSettings s) => s.RoomVital[Math.Min((int)c.Rooms[room].Kind, s.RoomVital.Length - 1)];

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

    /// <summary>A part's armour against fragments: a turret's side or roof (the thinner), else its own armour.</summary>
    public static double Armour(ShipClass c, int i)
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
    internal static bool Overlaps(in Box a, in Box b) =>
        Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) > 0.05 && Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) > 0.05 &&
        Math.Min(a.Top, b.Top) - Math.Max(a.Base, b.Base) > 0.05;

    /// <summary>A part's own plates as mild steel against fragments (E1): its armour by its material, and the plating
    /// of a superstructure block, a funnel, an uptake's casing, a mast or its top (the ship's superstructure plating).</summary>
    public static double PartMild(ShipClass c, int i, double armourMm)
    {
        ref readonly var comp = ref c.Components[i];
        double mm = armourMm > 0 ? c.MaterialOf(comp.Material).FragmentMildMm(armourMm) : 0;
        if (comp.Kind is ComponentKind.Superstructure or ComponentKind.Funnel or ComponentKind.Casing or ComponentKind.Mast or ComponentKind.Top)
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
    /// <summary>What a burst's fragments go through in its own space from p to a point there (docs/battle/shell-contents.md
    /// C3b), as the air that slows them as much over the air they're counted in already (as a near miss's water, E7):
    /// the stretch below its room's level through what the room stows (coal: <see cref="SimSettings.CoalFragmentDragL1"/>,
    /// a liquid the sea's <see cref="SimSettings.WaterDragL1"/>, each by its density), and the flooding's water above that
    /// up to its own level.</summary>
    internal static double MediumDragM(ShipClass c, ShipCondition k, int cell, P3 p, P3 to, SimSettings s)
    {
        if (cell < 0)
            return 0;
        ref readonly var row = ref c.Cells[cell];
        int room = row.Room;
        double zRoom = room >= 0 && room < k.Level.Length ? k.Level[room] : double.NaN;
        double cap = row.VolumeM3 * row.Permeability, water = cell < k.WaterM3.Length ? k.WaterM3[cell] : 0;
        double zSea = water > 0 && cap > 0 ? row.Box.Base + Math.Min(water / cap, 1) * (row.Box.Top - row.Box.Base) : double.NaN;
        if (double.IsNaN(zRoom) && double.IsNaN(zSea))
            return 0;
        double dx = to.X - p.X, dy = to.Y - p.Y, dz = to.Z - p.Z, rr = Math.Sqrt(dx * dx + dy * dy + dz * dz), drag = 0;
        if (!double.IsNaN(zRoom))
        {
            ref readonly var st = ref c.Stowages[room];
            double l1 = st.Kind == MediumKind.Granular ? s.CoalFragmentDragL1 * Stowage.CoalRho / st.Rho : s.WaterDragL1 * WaterRun.SeaRho / st.Rho;
            drag += Within(p.Z, to.Z, double.NegativeInfinity, zRoom) * rr * (Fragments.DragL1 / l1 - 1);
        }
        if (!double.IsNaN(zSea) && !(zSea <= zRoom))
            drag += Within(p.Z, to.Z, double.IsNaN(zRoom) ? double.NegativeInfinity : zRoom, zSea) * rr * (Fragments.DragL1 / s.WaterDragL1 - 1);
        return drag;

        // the share of a straight way from height za to zb that lies between lo and hi
        static double Within(double za, double zb, double lo, double hi)
        {
            if (Math.Abs(zb - za) < 1e-9)
                return za >= lo && za <= hi ? 1 : 0;
            double a = (lo - za) / (zb - za), b = (hi - za) / (zb - za);
            if (a > b)
                (a, b) = (b, a);
            return Math.Max(Math.Min(b, 1) - Math.Max(a, 0), 0);
        }
    }

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
