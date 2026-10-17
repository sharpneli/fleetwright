using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What the damage system understands (<see cref="DamageSystem"/>).</summary>
public enum DamageCommand : ushort
{
    SetPropellant = 1,
    Wreck,
    Breach,
    SideHole,
    FloodRooms,
    Jam,
    WreckKind,
    Burn,
    Sink,
}

/// <summary>What set a magazine off (<see cref="DamageSystem.Blast"/>).</summary>
public enum BlastCause : byte
{
    Shell,
    Torpedo,
    Fire,
    Dev,
}

/// <summary>Where along a ship a dev's side hole is (<see cref="DamageSystem.SideHole"/>): x in metres, the centre of
/// flotation, or a tenth of the length in from the bow or the stern.</summary>
public enum HoleAt : byte
{
    X,
    Mid,
    Bow,
    Stern,
}

/// <summary>Hits and what they do (docs/battle/fighting.md 4.3a): a hit comes in (<see cref="Strike"/>, from the shell
/// system as a shell lands, from the torpedo system as a torpedo strikes; mines and coarse fire later), and at once, in
/// the ship's own frame (a torpedo's explosion against the hull is <see cref="Underwater"/>'s, terminal and effects in
/// one):
/// <list type="bullet">
/// <item>its own path: the one tracer from just outside the hit point along the shell's flight, the ship's mounts at
/// their bearings and the ship at its attitude;</item>
/// <item>TERMINAL (<see cref="Terminal"/>): penetration, the fuze and where it bursts, the outcome class;</item>
/// <item>EFFECTS (<see cref="Effects"/>): parts wrecked, holes, fire, crew, jams, magazines, into the ship's
/// condition;</item>
/// <item>FUNCTIONS (<see cref="Functions"/>): what the ship can still do, worked out again when the condition changed:
/// its mounts, directors and eyes, its power (the ship store holds what's rung within it) and its steering (lost at the
/// gear or the rudders: jammed where it was, or trailing);</item>
/// <item>a line in the combat log: the shell, the part, the outcome, the effects.</item>
/// </list>
/// A ship with water in it or a fire is stepped every <see cref="SimSettings.FloodMs"/> (4.3b; less often once its water
/// has settled), in any clock tier: FLOODING (<see cref="Flooding"/>: the sea in, across the cell graph, the pumps; the
/// ship settled to it, its power held to what drives its added weight) and FIRE (<see cref="Fires"/>). It goes down by
/// its buoyancy, not hit points: foundered or capsized, or its magazine set off by a hit or a fire. Going down, it stops,
/// its guns fall silent and it leaves its group; it goes under a while later (by how it went), seen to go by those who
/// have it in sight, and is dropped from the world as a command would. Draws come from the stream "damage", one per hit
/// and per ship's step. World state: replays, hashes. Registered fifth, so its id is always <see cref="Id"/>.</summary>
public sealed class DamageSystem : IWorldSystem
{
    public const byte Id = 5;
    const ushort LossEvent = 1, StepEvent = 2, ClearEvent = 3;

    /// <summary>A magazine set off, for the screens (docs/visuals/magazine-explosions.md X0): when, the ship (an entity)
    /// and her class (a registry id), the magazine (a room of the class; -1 when none is linked to what went up), what
    /// set it off, where she was and her heading over the sea (unit), and the hit's point in her frame (the origin for a
    /// fire or the dev's).</summary>
    public readonly record struct Blast(long Ms, int Ship, int Class, int Room, BlastCause Cause, Vec3d At, Vec3d Forward, P3 Point);

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly CombatLog log;
    readonly RandomStream stream;
    /// <summary>Each ship's plates' lots (P7): derived by her entity, never advanced, so a plate keeps its quality.</summary>
    readonly RandomStream lotStream;
    readonly HitPath path = new();
    readonly PathBender bender = new();
    readonly Dictionary<ShipClass, FloodModel> floods = [];
    readonly double[] propellant = new double[256];
    double[] bearings = new double[16];
    readonly Blast[] blasts;
    int blastNext, blastCount;
    long hits;
    bool propellantSet, touched;

    readonly Sightings sightings;

    internal DamageSystem(SimSettings settings, ShipSystem ships, CombatLog log, Sightings sightings, ulong seed)
    {
        (this.settings, this.ships, this.log, this.sightings) = (settings, ships, log, sightings);
        stream = RandomStream.Named(seed, "damage");
        lotStream = RandomStream.Named(seed, "plate lots");
        Array.Fill(propellant, 1.0);
        blasts = new Blast[Math.Max(settings.BlastCapacity, 4)];
    }

    /// <summary>Hits taken in the world so far.</summary>
    public long Hits => hits;

    /// <summary>The last hit's outcome and effects, and the effects it had on what the ship can do (tools and benches:
    /// not world state).</summary>
    public TerminalResult LastTerminal { get; private set; }
    public EffectsResult LastEffects { get; private set; }
    public HitEffect LastEffect { get; private set; }

    /// <summary>The last hit's own path (tools: it's overwritten by the next hit).</summary>
    public HitPath LastPath => path;

    // ------------------------------------------------------------------ commands

    /// <summary>A side's propellant: how readily its magazines go up, times the settings' chances (1 by default; the
    /// Great War's cordite against the German RP C/12 is the knob's first use).</summary>
    public static Command SetPropellant(int side, double factor) => new(Id, (ushort)DamageCommand.SetPropellant, side, X: factor);

    public double Propellant(int side) => propellant[side & 255];

    /// <summary>The dev's wreck (and the tests'): a ship's part (a node of its class: a component, or a room after them,
    /// <see cref="ShipClass.RoomNode"/>) made at least this bad, and what the ship can do worked out again, as a hit
    /// would.</summary>
    public static Command Wreck(int entity, int node, PartState state = PartState.Disabled) =>
        new(Id, (ushort)DamageCommand.Wreck, entity, A: node, B: (int)state);

    /// <summary>The dev's hole (and the tests' and benches'): a hole of an area to the sea in a ship's cell, at a side
    /// (y) and height (z) in the ship's frame, as a hit would leave it; its flooding steps begin.</summary>
    public static Command Breach(int entity, int cell, double areaM2, double y, double z) =>
        new(Id, (ushort)DamageCommand.Breach, entity, A: cell, X: areaM2, B: (int)Math.Round(y * 1000), Y: z);

    /// <summary>The dev's hole in the side (the benches', tests' and the -hole flag's): at x along the ship (or the centre
    /// of flotation, the bow, the stern), z metres over the waterline, to starboard or port, in the cell behind it.</summary>
    public static Command SideHole(int entity, HoleAt at, double xM, double zOverWaterlineM, double areaM2, bool starboard = true) =>
        new(Id, (ushort)DamageCommand.SideHole, entity, A: (byte)at + (starboard ? 0 : 256), X: areaM2, B: (int)Math.Round(xM * 1000), Y: zOverWaterlineM);

    /// <summary>The dev's flooded spaces: the n rooms of a kind nearest the centre of flotation, each of their cells holed
    /// a square metre at its floor.</summary>
    public static Command FloodRooms(int entity, RoomKind kind, int n = int.MaxValue) =>
        new(Id, (ushort)DamageCommand.FloodRooms, entity, A: (long)kind, B: n);

    /// <summary>The dev's jammed mount (a mount's index in the class): it stops where it is, for some minutes (E4: debris,
    /// cleared then) or the battle (0).</summary>
    public static Command Jam(int entity, int mount, double minutes = 0) => new(Id, (ushort)DamageCommand.Jam, entity, A: mount, X: minutes);

    /// <summary>The dev's wreck by kind: the nth part (from 0, in the class's order) of a kind made at least this bad.</summary>
    public static Command WreckKind(int entity, ComponentKind kind, int nth, PartState state = PartState.Destroyed) =>
        new(Id, (ushort)DamageCommand.WreckKind, entity, A: (long)kind, B: nth, X: (double)state);

    /// <summary>The dev's sinking: the ship goes down now as if it had foundered, capsized or blown up (the views' check
    /// of a ship going under). A magazine is the one serving a mount (an index in the class), or by default the one that
    /// holds the most.</summary>
    public static Command Sink(int entity, LossKind how, int mount = -1) => new(Id, (ushort)DamageCommand.Sink, entity, A: (long)how, B: mount);

    /// <summary>The dev's fire: the highest cell at x along the ship (on the centreline) set burning.</summary>
    public static Command Burn(int entity, double xM) => new(Id, (ushort)DamageCommand.Burn, entity, X: xM);

    public void Apply(World world, in Command c)
    {
        switch ((DamageCommand)c.Code)
        {
            case DamageCommand.SetPropellant:
                if (c.Entity is < 0 or > 255 || !(c.X >= 0))
                    throw new ArgumentException($"no propellant {c.X} for side {c.Entity}");
                propellant[c.Entity] = c.X;
                propellantSet = true;
                break;
            case DamageCommand.Wreck:
                int slot = ships.Store.SlotOf(c.Entity);
                if (slot < 0)
                    return;
                var k = ships.Store.Combat.Condition(slot);
                if (c.A < 0 || c.A >= k.State.Length || c.B is < 0 or > (int)PartState.Destroyed)
                    throw new ArgumentException($"no part {c.A} in state {c.B} on ship {c.Entity}");
                k.Damaged = true;
                k.Worsen((int)c.A, (PartState)c.B);
                if (k.Status == ShipStatus.Afloat)
                {
                    var rng = stream.Derive(0x3EC4u).Derive((ulong)world.Time.Ms).Derive((ulong)c.A);
                    Recompute(world, slot, k, ref rng);
                }
                touched = true;
                break;
            case DamageCommand.Breach:
                slot = ships.Store.SlotOf(c.Entity);
                if (slot < 0)
                    return;
                k = ships.Store.Combat.Condition(slot);
                if (c.A < 0 || c.A >= k.BreachM2.Length || !(c.X > 0))
                    throw new ArgumentException($"no hole of {c.X} m² in cell {c.A} of ship {c.Entity}");
                k.Damaged = touched = true;
                k.Hole((int)c.A, c.X, new P3(0, c.B / 1000.0, c.Y));
                if (k.Status == ShipStatus.Afloat)
                    Wake(world, slot, k);
                break;
            case DamageCommand.SideHole:
            case DamageCommand.FloodRooms:
            case DamageCommand.Jam:
            case DamageCommand.WreckKind:
            case DamageCommand.Burn:
            case DamageCommand.Sink:
                slot = ships.Store.SlotOf(c.Entity);
                if (slot >= 0)
                    Dev(world, slot, c);
                break;
        }
    }

    /// <summary>The dev's damage commands, resolved against the ship's class.</summary>
    void Dev(World world, int slot, in Command c)
    {
        var k = ships.Store.Combat.Condition(slot);
        var cls = k.Class;
        if (k.Status != ShipStatus.Afloat)
            return;
        k.Damaged = touched = true;
        switch ((DamageCommand)c.Code)
        {
            case DamageCommand.SideHole:
                double x = (HoleAt)(c.A & 255) switch
                {
                    HoleAt.Mid => cls.Hydrostatics.Lcf,
                    HoleAt.Bow => cls.Hull.X1 - 0.1 * cls.Length,
                    HoleAt.Stern => cls.Hull.X0 + 0.1 * cls.Length,
                    _ => c.B / 1000.0,
                };
                double z = cls.Vertical.Waterline + c.Y;
                var (cell, y) = FloodModel.SideCell(cls, x, z, (c.A & 256) == 0);
                if (cell < 0 || !(c.X > 0))
                    throw new ArgumentException($"no hull at {x:0.0} m, {c.Y:0.0} m over the waterline on ship {c.Entity}");
                k.Hole(cell, c.X, new P3(x, y, z));
                Wake(world, slot, k);
                break;
            case DamageCommand.FloodRooms:
                double mid = cls.Hydrostatics.Lcf;
                for (int done = 0; done < c.B; done++)
                {
                    int best = -1;
                    for (int r = 0; r < cls.Rooms.Length; r++)
                        if (cls.Rooms[r].Kind == (RoomKind)c.A && !Holed(cls, k, r) &&
                            (best < 0 || Math.Abs((cls.Rooms[r].X0 + cls.Rooms[r].X1) / 2 - mid) < Math.Abs((cls.Rooms[best].X0 + cls.Rooms[best].X1) / 2 - mid)))
                            best = r;
                    if (best < 0)
                        break;
                    foreach (int i in cls.Links(cls.Rooms[best].Cells))
                    {
                        var b = cls.Cells[i].Box;
                        k.Hole(i, 1, new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Base + 0.05));
                    }
                }
                Wake(world, slot, k);
                break;
            case DamageCommand.Jam:
                if (c.A < 0 || c.A >= cls.Mounts.Length)
                    throw new ArgumentException($"no mount {c.A} on ship {c.Entity}");
                k.Strike((int)c.A, c.X > 0 ? new TurretHarm(TurretBlow.Jam, JamCause.Debris, (long)(c.X * 60_000), 0)
                    : new TurretHarm(TurretBlow.Jam, JamCause.Ring, NonPen.Battle, 0), world.Time.Ms);
                var rj = stream.Derive(0x3EC5u).Derive((ulong)world.Time.Ms);
                Recompute(world, slot, k, ref rj);
                Remind(world, slot, k);
                break;
            case DamageCommand.WreckKind:
                for (int i = 0, n = 0; i < cls.Components.Length; i++)
                    if (cls.Components[i].Kind == (ComponentKind)c.A && n++ == c.B)
                    {
                        k.Worsen(i, (PartState)(int)c.X);
                        var rw = stream.Derive(0x3EC6u).Derive((ulong)world.Time.Ms).Derive((ulong)i);
                        Recompute(world, slot, k, ref rw);
                        return;
                    }
                throw new ArgumentException($"no {(ComponentKind)c.A} {c.B} on ship {c.Entity}");
            case DamageCommand.Sink:
                if (c.A is < (int)LossKind.Magazine or > (int)LossKind.BrokeInTwo)
                    throw new ArgumentException($"no way of going down {c.A}");
                if (c.A == (int)LossKind.Magazine)
                    Blew(world, slot, k, DevMagazine(cls, (int)c.B), BlastCause.Dev, default);
                else
                    Lose(world, slot, k, (LossKind)c.A);
                break;
            case DamageCommand.Burn:
                int top = -1;
                for (int i = 0; i < cls.Cells.Length; i++)
                {
                    var b = cls.Cells[i].Box;
                    if (b.X0 <= c.X && b.X1 >= c.X && b.Y0 <= 0 && b.Y1 >= 0 && (top < 0 || b.Top > cls.Cells[top].Box.Top))
                        top = i;
                }
                if (top < 0)
                    throw new ArgumentException($"no cell at {c.X:0.0} m on ship {c.Entity}");
                k.CellFire[top] = true;
                Wake(world, slot, k);
                break;
        }
    }

    /// <summary>The dev's magazine: the one serving a mount, else the one that holds the most (-1: none).</summary>
    static int DevMagazine(ShipClass cls, int mount)
    {
        if (mount >= 0)
            return mount < cls.Mounts.Length ? cls.Mounts[mount].Magazine : throw new ArgumentException($"no mount {mount}");
        int best = -1;
        for (int r = 0; r < cls.Rooms.Length; r++)
            if (cls.Rooms[r].Kind == RoomKind.Magazine && (best < 0 || cls.Rooms[r].Tonnes > cls.Rooms[best].Tonnes))
                best = r;
        return best;
    }

    /// <summary>Whether any of a room's cells is holed already.</summary>
    static bool Holed(ShipClass c, ShipCondition k, int room)
    {
        foreach (int i in c.Links(c.Rooms[room].Cells))
            if (k.BreachM2[i] > 0)
                return true;
        return false;
    }

    /// <summary>A class's flooding model (its cells as water sees them), made when it's first in the world.</summary>
    public FloodModel Flood(ShipClass cls)
    {
        if (!floods.TryGetValue(cls, out var f))
            floods[cls] = f = new FloodModel(cls);
        return f;
    }

    /// <summary>A class's flooding model and the tracer's room for it, made the first time it's
    /// in the world (not at its first hit).</summary>
    internal void Prepare(ShipClass cls)
    {
        Flood(cls);
        path.Reserve(cls.Solids.Solids.Length);
        if (bearings.Length < cls.Mounts.Length)
            bearings = new double[Math.Max(cls.Mounts.Length, bearings.Length * 2)];
    }

    // ------------------------------------------------------------------ a hit

    /// <summary>A hit lands, now: what it does to the ship, at once.</summary>
    public void Strike(World world, in Hit hit)
    {
        var store = ships.Store;
        int slot = store.SlotOf(hit.Ship);
        if (slot < 0)
            return;
        var ship = store[slot];
        var cls = ship.Handling.Class;
        var k = store.Combat.Condition(slot);
        long now = world.Time.Ms;
        bool near = hit.Source == HitSource.NearMiss;
        k.Damaged = touched = true;
        if (!near)
            k.Hits++;
        (k.LastShooter, k.LastBattery) = (hit.Shooter, hit.Battery);

        Prepare(cls);
        var rng = stream.Derive((ulong)hits++);
        TerminalResult t;
        EffectsResult e;
        if (hit.Source == HitSource.Torpedo)
        {
            // an underwater explosion against the hull (4.6): no path of its own
            e = Underwater.Apply(cls, k, hit, settings, propellant[ship.Side], ref rng, out var outcome);
            t = new TerminalResult { Outcome = outcome, Plate = -1 };
        }
        else if (near)
        {
            // a burst in the water outside the hull (E7): no path either
            e = NearMiss.Apply(cls, k, hit, settings, ref rng);
            t = new TerminalResult { Outcome = hit.Force < 1 ? Outcome.LowOrder : Outcome.BurstOutside, Burst = BurstKind.Outside, Plate = -1, Glance = -1,
                Spall = double.NaN, PlateU = double.NaN, BurstMs = hit.SpeedMs };
        }
        else
        {
            // its own path: from just outside the point along the flight, through the whole ship
            int n = cls.Mounts.Length;
            for (int m = 0; m < n; m++)
                bearings[m] = CombatStore.BearingAt(store.Combat.Mount(slot, m), cls.Mounts[m], now);
            var d = hit.Direction;
            double far = 2 * cls.Solids.Radius + 10;
            var p0 = new P3(hit.Point.X - d.X, hit.Point.Y - d.Y, hit.Point.Z - d.Z);
            var p1 = new P3(hit.Point.X + d.X * far, hit.Point.Y + d.Y * far, hit.Point.Z + d.Z * far);
            var water = HitQuery.WaterOf(cls, k.Attitude);
            ShipTracer.Trace(cls, p0, p1, bearings.AsSpan(0, n), water, path);

            bender.Set(bearings, n, water);
            var lots = lotStream.Derive((ulong)hit.Ship);
            t = Terminal.Resolve(cls, hit, path, bender, lots, settings, ref rng, k);
            e = Effects.Apply(cls, k, hit, path, t, settings, propellant[ship.Side], ref rng);
        }

        var fx = HitEffect.None;
        if (e.Destroyed > 0)
            fx |= HitEffect.Destroyed;
        if (e.Disabled > 0)
            fx |= HitEffect.Disabled;
        if (e.Damaged > 0)
            fx |= HitEffect.Damaged;
        if (e.BreachM2 > 0)
            fx |= HitEffect.Holed;
        if (e.Fire)
            fx |= HitEffect.Fire;
        fx |= e.Turret.Blow switch
        {
            TurretBlow.Jam => HitEffect.Jammed,
            TurretBlow.Stiff => HitEffect.Stiff,
            TurretBlow.GunOut => HitEffect.GunOut,
            TurretBlow.Sights => HitEffect.Sights,
            _ => HitEffect.None,
        };
        if (e.Jammed)
            fx |= HitEffect.Jammed;
        if (e.Stunned)
            fx |= HitEffect.Stunned;
        if (e.Leak != LeakRegime.None)
            fx |= HitEffect.Leak;
        if (e.Feed)
            fx |= HitEffect.Feed;
        if (e.Shock)
            fx |= HitEffect.Shock;
        if (e.Blackout)
            fx |= HitEffect.Blackout;
        if (e.Changed && k.Status == ShipStatus.Afloat)
        {
            fx |= Recompute(world, slot, k, ref rng);
            Remind(world, slot, k);
        }
        bool blew = e.Magazine && k.Status == ShipStatus.Afloat, broke = e.Broken && !blew && k.Status == ShipStatus.Afloat;
        if (blew)
            fx |= HitEffect.Magazine;
        if (broke)
            fx |= HitEffect.Broken;
        (LastTerminal, LastEffects, LastEffect) = (t, e, fx);
        if (!near || fx != HitEffect.None || e.Killed + e.Wounded > 0)   // a near miss that did nothing isn't news
            log.Add(new CombatEntry(now, near ? CombatEvent.NearMiss : CombatEvent.Hit, hit.Shooter, hit.Battery, hit.Ship, hit.ShellType, e.Part, t.Outcome,
                fx, hit.Shell.Kind, (float)(hit.Source == HitSource.Torpedo ? hit.Torpedo.CalibreMm : hit.Shell.CalibreMm), hit.Source));
        if (blew)
            Blew(world, slot, k, e.MagazineRoom, hit.Source == HitSource.Torpedo ? BlastCause.Torpedo : BlastCause.Shell, hit.Point);
        else if (broke)
            Lose(world, slot, k, LossKind.BrokeInTwo);
        else if (k.Status == ShipStatus.Afloat && (e.BreachM2 > 0 || e.Fire))
            Wake(world, slot, k);   // water or fire: its steps begin (or come sooner)
    }

    /// <summary>What the ship can still do, worked out again; the steering lost at the gear or the rudders jams or
    /// trails; the store holds the ship to it. The effects it had.</summary>
    HitEffect Recompute(World world, int slot, ShipCondition k, ref RandomStream rng)
    {
        double cap = k.PowerCap;
        var steering = k.Steering;
        bool steers = Functions.Evaluate(k, settings.DraughtFloor);
        Freeze(world, slot, k);
        Stiffen(world, slot, k);
        if (!steers && k.Steering == Steering.Free)
        {
            bool jam = rng.NextDouble() < settings.RudderJamChance;
            (k.Steering, k.JamRudderRad) = jam ? (Steering.Jammed, ships.Store[slot].Rudder) : (Steering.Lost, 0);
        }
        k.Version++;
        var fx = HitEffect.None;
        if (k.Steering != steering)
            fx |= HitEffect.Steering;
        if (k.PowerCap < cap)
            fx |= HitEffect.Power;
        if (fx != HitEffect.None)
            Hold(world, slot, k, force: true);
        return fx;
    }

    /// <summary>Its mounts out of action (or jammed) stop where they are: they train no more.</summary>
    void Freeze(World world, int slot, ShipCondition k)
    {
        var mounts = k.Class.Mounts;
        for (int m = 0; m < mounts.Length; m++)
            if (!k.MountWorks[m])
                ships.Store.Combat.Stop(slot, m, mounts[m], world.Time.Ms);
    }

    /// <summary>A main-battery salvo's own shock on the ship's breakers (E4; damage/13 §3.4, South Dakota's ABT): with
    /// director fire control, by its shock grade per 20 minutes of fire (a salvo standing for its mount's reload), a
    /// section's power out round the firing mount.</summary>
    internal void OwnGuns(World world, int slot, int mount, ref RandomStream r)
    {
        var k = ships.Store.Combat.Condition(slot);
        var c = k.Class;
        if (k.Status != ShipStatus.Afloat || !NonPen.Electrics(c, settings))
            return;
        double p = settings.OwnGunBreaker[c.ShockGrade == ShockGrade.G0 ? 0 : 1] * settings.ShockGradeK[(int)c.ShockGrade] *
                   c.Mounts[mount].ReloadS / 1200;
        if (r.NextDouble() >= p)
            return;
        ref readonly var m = ref c.Mounts[mount];
        var at = new P3(m.X, m.Y, c.Components[m.Component].Bounds.Base);
        long now = world.Time.Ms;
        if (!Effects.PowerOut(c, k, at, m.Component, -1, settings.ShockZoneBM, NonPen.Minutes(settings.SectionPowerMin, ref r), now))
            return;
        k.Damaged = touched = true;
        Recompute(world, slot, k, ref r);
        Remind(world, slot, k);
    }

    /// <summary>Its mounts train as stiffly as their hits left them, now (E4).</summary>
    void Stiffen(World world, int slot, ShipCondition k)
    {
        var mounts = k.Class.Mounts;
        long now = world.Time.Ms;
        for (int m = 0; m < mounts.Length; m++)
            ships.Store.Combat.SetStiff(slot, m, mounts[m], now, k.Stiff(m, now) ? 1 - settings.StiffTrain : 0);
    }

    /// <summary>Its next timed outage's end (E4) has its event: set, or set sooner (the one it replaces is cancelled by
    /// its generation).</summary>
    void Remind(World world, int slot, ShipCondition k)
    {
        if (k.NextClearMs < 0 || k.NextClearMs == k.ClearAtMs)
            return;
        k.ClearAtMs = k.NextClearMs;
        k.ClearGen++;
        world.Schedule(new WorldTime(Math.Max(k.NextClearMs, world.Time.Ms)), EventTier.Fine, ships.Store[slot].Entity, Id, ClearEvent,
            ships.Store[slot].Generation, k.ClearGen);
    }

    /// <summary>The store holds the ship to the power it can make, less what its water's weight costs (the power a
    /// speed needs goes as the displacement to the two-thirds); a change under 1% is let be, but for a hit's.</summary>
    void Hold(World world, int slot, ShipCondition k, bool force)
    {
        double d = Flood(k.Class).DisplacementT, cap = k.PowerCap * Math.Pow(d / (d + k.WaterT), 2.0 / 3);
        if (!force && Math.Abs(cap - k.HeldCap) < 0.01)
            return;
        k.HeldCap = cap;
        ships.Damaged(world, slot, cap, k.Steering, k.JamRudderRad);
    }

    /// <summary>A magazine set off: its record for the screens (<see cref="Blast"/>), and the ship lost.</summary>
    void Blew(World world, int slot, ShipCondition k, int room, BlastCause cause, P3 point)
    {
        var ship = ships.Store[slot];
        blasts[blastNext] = new Blast(world.Time.Ms, ship.Entity, ship.ClassIndex, room, cause, ship.Position, ship.Forward, point);
        blastNext = (blastNext + 1) % blasts.Length;
        blastCount = Math.Min(blastCount + 1, blasts.Length);
        Lose(world, slot, k, LossKind.Magazine);
    }

    /// <summary>The ship going down (its magazine, foundered, capsized): it stops, its guns are silent, it leaves its
    /// group, and it goes under a while later; the log's line says how.</summary>
    void Lose(World world, int slot, ShipCondition k, LossKind how)
    {
        var store = ships.Store;
        int entity = store[slot].Entity;
        (k.Status, k.Loss) = (ShipStatus.GoingDown, how);
        k.PowerCap = 0;
        k.NextStepMs = -1;
        k.StepGen++;   // no more steps: its water and attitude stay as they were
        Array.Fill(k.MountWorks, false);
        Array.Fill(k.TubeWorks, false);
        Array.Fill(k.DirectorWorks, false);
        Freeze(world, slot, k);
        k.Version++;
        k.HeldCap = 0;
        ships.Damaged(world, slot, 0, k.Steering == Steering.Free ? Steering.Lost : k.Steering, k.JamRudderRad);
        world.Issue(GunnerySystem.Cease(entity));
        world.Issue(GroupSystem.Remove(entity));
        log.Add(new CombatEntry(world.Time.Ms, CombatEvent.Lost, k.LastShooter, k.LastBattery, entity, (int)how));
        int ms = how switch
        {
            LossKind.Magazine => settings.MagazineLossMs,
            LossKind.Capsized => settings.CapsizeMs,
            LossKind.BrokeInTwo => settings.BrokenMs,
            _ => settings.FounderMs,
        };
        (k.LossMs, k.GoneMs) = (world.Time.Ms, world.Time.Ms + ms);
        world.Schedule(new WorldTime(k.GoneMs), EventTier.Fine, entity, Id, LossEvent, store[slot].Generation);
    }

    // ------------------------------------------------------------------ fire and flooding

    /// <summary>A ship's steps begin, or come sooner: at the next <see cref="SimSettings.FloodMs"/> of world time.</summary>
    void Wake(World world, int slot, ShipCondition k)
    {
        long now = world.Time.Ms, next = (now / settings.FloodMs + 1) * settings.FloodMs;
        if (k.NextStepMs >= 0 && k.NextStepMs <= next)
            return;
        bool starting = k.NextStepMs < 0;
        if (starting)
            k.StepMs = now;   // still since then
        k.NextStepMs = next;
        k.StepGen++;
        if (starting)
            Hold(world, slot, k, force: true);   // off rails: its steps change it (ShipSystem.Tiers.cs)
        world.Schedule(new WorldTime(next), EventTier.Fine, ships.Store[slot].Entity, Id, StepEvent, ships.Store[slot].Generation, k.StepGen);
    }

    /// <summary>One step of a ship's flooding and fire, from its last to now: the water, the attitude it settles to and
    /// whether that loses it, the fires; what it can do worked out again if a room flooded or burned out; its power held
    /// to its added weight. The time to its next step, ms, or -1 when it's dry, holed nowhere under the sea and not
    /// burning (or going down).</summary>
    long Step(World world, int slot, ShipCondition k, long now)
    {
        var cls = k.Class;
        var f = Flood(cls);
        double dt = (now - k.StepMs) / 1000.0, was = k.WaterT;
        k.StepMs = now;
        if (dt <= 0)
            return settings.FloodMs;
        var fl = Flooding.Step(cls, f, k, dt, k.PowerCap, settings);
        var how = Flooding.Settle(cls, f, k, settings, dt);
        var rng = stream.Derive(0xF100Du).Derive((ulong)ships.Store[slot].Entity).Derive((ulong)now);
        var fs = k.Fires > 0 ? Fires.Step(cls, f, k, dt, settings, propellant[ships.Store[slot].Side], ref rng) : default;
        touched = true;
        if (fs.Magazine)
        {
            Blew(world, slot, k, fs.MagazineRoom, BlastCause.Fire, default);
            return -1;
        }
        if (how != LossKind.None)
        {
            Lose(world, slot, k, how);
            return -1;
        }
        if (fl.RoomsFlooded || fs.Changed)
            Recompute(world, slot, k, ref rng);
        Hold(world, slot, k, force: false);
        if (k.Fires > 0 || Math.Abs(k.WaterT - was) >= settings.FloodIdleT)
            return settings.FloodMs;
        return k.WaterT > 0 || Holed(cls, k) ? settings.FloodIdleMs : -1;
    }

    /// <summary>Whether any hole is under the sea now.</summary>
    static bool Holed(ShipClass c, ShipCondition k)
    {
        var water = HitQuery.WaterOf(c, k.Attitude);
        var cells = c.Cells;
        for (int i = 0; i < cells.Length; i++)
            if (k.BreachM2[i] > 0 && k.BreachZ[i] < water.At((cells[i].Box.X0 + cells[i].Box.X1) / 2, k.BreachY[i]))
                return true;
        return false;
    }

    /// <summary>A tool's (the hits bench's, the tests'): a ship's flooding and fire run on for a while from now, in steps
    /// as the world would take them, without the world's clock moving, and the timed outages ending by then over. The
    /// ship's status after.</summary>
    public ShipStatus Settle(World world, int entity, double seconds)
    {
        int slot = ships.Store.SlotOf(entity);
        if (slot < 0)
            return ShipStatus.GoingDown;
        var k = ships.Store.Combat.Condition(slot);
        long t = world.Time.Ms, end = t + (long)(seconds * 1000);
        if (k.NextStepMs < 0 && k.Status == ShipStatus.Afloat)
            k.StepMs = t;
        k.StepGen++;   // the world's own steps for it are cancelled: these take their place
        k.NextStepMs = -1;
        long next = (t / settings.FloodMs + 1) * settings.FloodMs;
        while (k.Status == ShipStatus.Afloat && next <= end)
        {
            // the timed outages and the walking wounded (E6) ending by then over first, as the world's clear event has them
            if (k.NextClearMs >= 0 && k.NextClearMs <= next && k.Clear(next))
            {
                var rc = stream.Derive(0xC1EBu).Derive((ulong)entity).Derive((ulong)next);
                Recompute(world, slot, k, ref rc);
            }
            long d = Step(world, slot, k, next);
            if (d < 0)
                break;
            next = (next / d + 1) * d;
        }
        // the timed outages that end meanwhile (E4) over too
        if (k.Status == ShipStatus.Afloat && k.Clear(end))
        {
            var rc = stream.Derive(0xC1EBu).Derive((ulong)entity).Derive((ulong)end);
            Recompute(world, slot, k, ref rc);
        }
        return k.Status;
    }

    public void Handle(World world, in SimEvent e)
    {
        int slot = ships.Store.SlotOf(e.Entity);
        if (slot < 0 || ships.Store[slot].Generation != e.Generation)
            return;
        if (e.Code == LossEvent)
        {
            Gone(e.Entity);
            world.Issue(ShipSystem.Drop(e.Entity));
            return;
        }
        var k = ships.Store.Combat.Condition(slot);
        if (e.Code == ClearEvent)
        {
            // timed outages over (E4): what the ship can do worked out again, and the next one's end
            if (e.A != k.ClearGen || k.Status != ShipStatus.Afloat)
                return;
            k.ClearAtMs = -1;
            if (k.Clear(world.Time.Ms))
            {
                touched = true;
                var rc = stream.Derive(0xC1EAu).Derive((ulong)e.Entity).Derive((ulong)world.Time.Ms);
                Recompute(world, slot, k, ref rc);
            }
            Remind(world, slot, k);
            return;
        }
        if (e.A != k.StepGen || k.Status != ShipStatus.Afloat)
            return;
        long now = world.Time.Ms, d = Step(world, slot, k, now);
        if (d < 0 || k.Status != ShipStatus.Afloat)
        {
            k.NextStepMs = -1;
            return;
        }
        k.NextStepMs = (now / d + 1) * d;
        world.Schedule(new WorldTime(k.NextStepMs), EventTier.Fine, e.Entity, Id, StepEvent, e.Generation, k.StepGen);
    }

    /// <summary>A ship gone under: every navy that has it in sight saw it go (its contact says so until it's forgotten).</summary>
    void Gone(int entity)
    {
        var all = sightings.All;
        for (int p = 0; p < all.Count; p++)   // by index: a foreach over the interface boxes its enumerator
        {
            var pic = all[p];
            int i = pic.IndexOf(entity);
            if (i >= 0 && pic[i].InSight)
                pic.At(i).Seen |= SeenFlags.Gone;
        }
    }

    // ------------------------------------------------------------------ the rest

    public void Hash(ref StateHash h)
    {
        if (propellantSet)
            for (int s = 0; s < propellant.Length; s++)
                if (propellant[s] != 1)
                {
                    h.Add(s);
                    h.Add(propellant[s]);
                }
        if (!touched)
            return;   // a world never hit hashes as before there was damage
        h.Add(hits);
        var store = ships.Store;
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && store.Combat.Condition(slot) is { Damaged: true } k)
            {
                h.Add(slot);
                k.Hash(ref h);
            }
    }

    public void Fill(Snapshot snap)
    {
        var v = snap.Conditions;
        var store = ships.Store;
        int n = 0;
        if (touched)
            for (int slot = 0; slot < store.Slots; slot++)
            {
                if (!store.IsAlive(slot) || store.Combat.Condition(slot) is not { Damaged: true } k || !snap.ShownSides[store[slot].Side])
                    continue;
                v.EnsureCapacity(n + 1);
                int directors = 0;
                foreach (bool w in k.DirectorWorks)
                    if (w)
                        directors++;
                (v.Entity[n], v.Hits[n], v.PowerCap[n], v.Steering[n], v.Status[n]) = (store[slot].Entity, k.Hits, k.PowerCap, k.Steering, k.Status);
                (v.MountsWorking[n], v.Mounts[n], v.DirectorsWorking[n], v.Directors[n]) = (k.MountsWorking, k.MountWorks.Length, directors, k.DirectorWorks.Length);
                (v.CrewLost[n], v.Fires[n], v.BreachM2[n], v.Conned[n]) = (k.CrewLost, k.Fires, k.BreachTotalM2, k.Conned);
                (v.WaterT[n], v.ReserveT[n], v.Attitude[n], v.Loss[n], v.HeldCap[n]) = (k.WaterT, Flood(k.Class).ReserveT - k.WaterT, k.Attitude, k.Loss, k.HeldCap);
                n++;
            }
        v.Count = n;

        var b = snap.Blasts;
        b.EnsureCapacity(blastCount);
        long oldest = snap.Time.Ms - settings.BlastLifeMs;
        n = 0;
        for (int i = 0; i < blastCount; i++)
        {
            ref readonly var x = ref blasts[(blastNext - 1 - i + blasts.Length) % blasts.Length];
            if (x.Ms < oldest)
                break;
            (b.Ms[n], b.Ship[n], b.Class[n], b.Room[n], b.Cause[n]) = (x.Ms, x.Ship, x.Class, x.Room, x.Cause);
            (b.At[n], b.Forward[n], b.Point[n]) = (x.At, x.Forward, x.Point);
            n++;
        }
        b.Count = n;
    }
}
