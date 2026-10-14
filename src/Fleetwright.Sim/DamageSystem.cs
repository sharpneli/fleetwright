using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What the damage system understands (<see cref="DamageSystem"/>).</summary>
public enum DamageCommand : ushort
{
    SetPropellant = 1,
    Wreck,
}

/// <summary>Hits and what they do (docs/battle/fighting.md 4.3a): a hit comes in (<see cref="Strike"/>, from the shell
/// system as a shell lands; torpedoes, mines and coarse fire later), and at once, in the ship's own frame:
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
/// A magazine set off loses the ship at once (v0): it stops, its guns fall silent, and it goes under
/// <see cref="SimSettings.MagazineLossMs"/> later, dropped from the world as a command would. Fire, flooding and loss by
/// buoyancy are 4.3b's. Draws come from the stream "damage", one per hit. World state: replays, hashes. Registered fifth,
/// so its id is always <see cref="Id"/>.</summary>
public sealed class DamageSystem : IWorldSystem
{
    public const byte Id = 5;
    const ushort LossEvent = 1;

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly CombatLog log;
    readonly RandomStream stream;
    readonly HitPath path = new();
    readonly Dictionary<ShipClass, double[]> qualities = [];
    readonly double[] propellant = new double[256];
    double[] bearings = new double[16];
    long hits;
    bool propellantSet, touched;

    internal DamageSystem(SimSettings settings, ShipSystem ships, CombatLog log, ulong seed)
    {
        (this.settings, this.ships, this.log) = (settings, ships, log);
        stream = RandomStream.Named(seed, "damage");
        Array.Fill(propellant, 1.0);
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
        }
    }

    /// <summary>A class's materials' qualities and the tracer's room for it, made the first time it's in the world (not
    /// at its first hit).</summary>
    internal void Prepare(ShipClass cls)
    {
        if (!qualities.ContainsKey(cls))
            qualities[cls] = Terminal.Qualities(cls, settings);
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
        k.Damaged = touched = true;
        k.Hits++;

        // its own path: from just outside the point along the flight, through the whole ship
        Prepare(cls);
        int n = cls.Mounts.Length;
        for (int m = 0; m < n; m++)
            bearings[m] = CombatStore.BearingAt(store.Combat.Mount(slot, m), cls.Mounts[m], now);
        var d = hit.Direction;
        double far = 2 * cls.Solids.Radius + 10;
        var p0 = new P3(hit.Point.X - d.X, hit.Point.Y - d.Y, hit.Point.Z - d.Z);
        var p1 = new P3(hit.Point.X + d.X * far, hit.Point.Y + d.Y * far, hit.Point.Z + d.Z * far);
        ShipTracer.Trace(cls, p0, p1, bearings.AsSpan(0, n), HitQuery.WaterOf(cls, k.Attitude), path);

        var rng = stream.Derive((ulong)hits++);
        var q = qualities[cls];
        var t = Terminal.Resolve(cls, hit, path, q, settings, ref rng);
        var e = Effects.Apply(cls, k, hit, path, t, q, settings, propellant[ship.Side], ref rng);

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
        if (e.Jammed)
            fx |= HitEffect.Jammed;
        if (e.Changed && k.Status == ShipStatus.Afloat)
            fx |= Recompute(world, slot, k, ref rng);
        if (e.Magazine && k.Status == ShipStatus.Afloat)
        {
            fx |= HitEffect.Magazine;
            Lose(world, slot, k);
        }
        (LastTerminal, LastEffects, LastEffect) = (t, e, fx);
        log.Add(new CombatEntry(now, CombatEvent.Hit, hit.Shooter, hit.Battery, hit.Ship, hit.ShellType, e.Part, t.Outcome, fx, hit.Shell.Kind,
            (float)hit.Shell.CalibreMm));
        if (e.Magazine && fx.HasFlag(HitEffect.Magazine))
            log.Add(new CombatEntry(now, CombatEvent.Lost, hit.Shooter, hit.Battery, hit.Ship, 0));
    }

    /// <summary>What the ship can still do, worked out again; the steering lost at the gear or the rudders jams or
    /// trails; the store holds the ship to it. The effects it had.</summary>
    HitEffect Recompute(World world, int slot, ShipCondition k, ref RandomStream rng)
    {
        double cap = k.PowerCap;
        var steering = k.Steering;
        bool steers = Functions.Evaluate(k, settings.DraughtFloor);
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
            ships.Damaged(world, slot, k.PowerCap, k.Steering, k.JamRudderRad);
        return fx;
    }

    /// <summary>The ship lost at once (v0, a magazine): it stops, its guns are silent, and it goes under later.</summary>
    void Lose(World world, int slot, ShipCondition k)
    {
        var store = ships.Store;
        int entity = store[slot].Entity;
        k.Status = ShipStatus.GoingDown;
        k.PowerCap = 0;
        Array.Fill(k.MountWorks, false);
        Array.Fill(k.DirectorWorks, false);
        k.Version++;
        ships.Damaged(world, slot, 0, k.Steering == Steering.Free ? Steering.Lost : k.Steering, k.JamRudderRad);
        world.Issue(GunnerySystem.Cease(entity));
        world.Schedule(new WorldTime(world.Time.Ms + settings.MagazineLossMs), EventTier.Fine, entity, Id, LossEvent, store[slot].Generation);
    }

    public void Handle(World world, in SimEvent e)
    {
        if (e.Code != LossEvent)
            return;
        int slot = ships.Store.SlotOf(e.Entity);
        if (slot >= 0 && ships.Store[slot].Generation == e.Generation)
            world.Issue(ShipSystem.Drop(e.Entity));
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
                n++;
            }
        v.Count = n;
    }
}
