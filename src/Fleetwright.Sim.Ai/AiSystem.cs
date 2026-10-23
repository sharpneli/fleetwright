using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>The AI in the world (docs/battle/plan.md steps 2.3b, 2.4 and 2.6): one <see cref="IWorldSystem"/> holding the
/// chain of command, top-down (the sides' commanders, the units' tasks, admirals, then captains; admiralties later), each layer ticking at its own rate on a
/// grid of world time while it has anything to watch. A host registers it after making the world. Every order to a
/// unit comes in as one of its commands (<see cref="Orders"/>), the player's logged, a layer's to the layer below not:
/// a replay registers it again and it thinks the same.</summary>
public sealed class AiSystem : IWorldSystem
{
    readonly IAiLayer[] layers;   // top-down: thought in this order at a shared moment
    readonly long[] nextTick;
    readonly int[] tickGen;   // a tick rescheduled sooner leaves the old one stale
    readonly IPicture picture;
    readonly Probes probes;
    readonly int[] layerProbes;

    public byte Id { get; }
    public AdmiralLayer Admirals { get; }
    public CaptainLayer Captains { get; }

    /// <summary>The units' tasks (docs/battle/command.md 5.1c), the layer above the admirals.</summary>
    public TaskLayer Tasks { get; }

    /// <summary>The sides' commanders (docs/battle/command.md 5.1d), the top layer.</summary>
    public CommanderLayer Commanders { get; }

    /// <summary>Each navy's contacts in sight, gridded for the layers' questions (not world state).</summary>
    public SeenContacts Seen { get; }

    /// <summary>The enemy as each navy sees it: groups and their strength (docs/battle/command.md 5.1c; not world state).</summary>
    public TacticalPicture Tactical { get; }

    /// <summary>Every side's and unit's standing rules (docs/battle/command.md "Doctrine").</summary>
    public Doctrine Doctrine { get; }

    /// <summary>What each unit is for (docs/battle/admirals-orders.md O6).</summary>
    public Roles Roles { get; }

    /// <summary>Who can fight whom (docs/battle/command.md 5.1g): the judgements every layer asks (not world state).</summary>
    public MatchupJudge Judge { get; }

    /// <summary>The enemy groups each navy holds, in sight or not (not world state).</summary>
    public HeldGroups Held { get; }

    /// <summary>When a commander sends a flotilla in to attack: the side's and every force's admiral's one rule
    /// (docs/battle/admirals-orders.md O7b).</summary>
    public FlotillaAttacks Attacks { get; }

    // each side's home (docs/battle/command.md "home"): where its ships out of the fight go; null for none
    readonly Vec3d?[] homes = new Vec3d?[256];

    /// <summary>A side's home, if it has one.</summary>
    public Vec3d? HomeOf(int side) => homes[side & 255];

    /// <summary>Orders to units (ships and groups), as commands to send: the player's way in, and the layers'.</summary>
    public UnitOrders Orders => new(Id);

    public AiSystem(World world)
    {
        Seen = new SeenContacts();
        Doctrine = new Doctrine(world.Groups, world.Settings.AggressionShift);
        Judge = new MatchupJudge(world.Groups, Seen, Doctrine);
        Held = new HeldGroups(Judge.Matchups);
        Captains = new CaptainLayer(world.Groups, world.Encounters, Seen, Doctrine, homes) { Judge = Judge, TrailPoints = world.Settings.FollowTrackPoints };
        Roles = new Roles(world.Groups, world.Ships.Store);
        Admirals = new AdmiralLayer(world.Groups, Captains, Seen, Doctrine) { Judge = Judge, Roles = Roles };
        Captains.Admirals = Admirals;
        Tactical = new TacticalPicture(Seen, Judge.Matchups);
        Admirals.Tactical = Tactical;
        Tasks = new TaskLayer(world.Groups, Seen, homes) { Admirals = Admirals, Captains = Captains, Doctrine = Doctrine, Judge = Judge };
        (Admirals.Tasks, Captains.Tasks) = (Tasks, Tasks);
        Attacks = new FlotillaAttacks(Judge, Held, Doctrine, Roles) { Tasks = Tasks };
        Commanders = new CommanderLayer(world.Groups, world.Ships.Store, Seen, Doctrine) { Tasks = Tasks, Captains = Captains, Judge = Judge, Held = Held, Roles = Roles, Attacks = Attacks };
        (Admirals.Attacks, Admirals.Commanders) = (Attacks, Commanders);
        layers = [Commanders, Tasks, Admirals, Captains];
        nextTick = new long[layers.Length];
        tickGen = new int[layers.Length];
        Array.Fill(nextTick, -1);
        picture = new NavyPicture(world);
        probes = world.Probes;
        layerProbes = [.. layers.Select(l => probes.Register("AI: " + l.GetType().Name))];
        Id = world.Register(this);
    }

    AiContext Context(World world) => new(world, picture, Id);

    public void Apply(World world, in Command c)
    {
        if ((AiCommand)c.Code is < AiCommand.Course or > AiCommand.Role)
            throw new ArgumentException($"unknown AI command {c.Code}");
        var u = new Unit(c.Entity);
        var code = (AiCommand)c.Code;
        var from = OrderFrom.Of(c);
        var source = from.Source;
        switch (code)
        {
            case AiCommand.Commander:
                Commanders.SetMode(c.Entity, (CommanderMode)c.A);
                WakeAll(world);
                return;
            case AiCommand.Automate:
                Commanders.Automate(u, c.X);
                WakeAll(world);
                return;
        }
        bool task = TaskLayer.IsTask(code, source);
        if (from.Hand && (task || EndsTask(code)))
            Commanders.PlayerOrdered(Context(world), u);   // the player's now, not the commander's (a scenario's opening orders never take it)
        if (task)
        {
            Tasks.Order(Context(world), c);
            WakeAll(world);
            return;
        }
        if (source is OrderSource.Task or OrderSource.Commander)
        {
            Route(world, c with { B = (int)OrderSource.Task }, u);   // a task's orders are the unit's own, the AI's (Detachment.Tasked)
            return;
        }
        if (source == OrderSource.Player && EndsTask(code))
            Tasks.Overridden(u, c);
        Route(world, c, u);
    }

    /// <summary>Whether the player's order to a unit ends its task: every order that moves it (a speed only changes the
    /// task's), not its guns, doctrine or detachment.</summary>
    static bool EndsTask(AiCommand code) => code is not (AiCommand.FireAtWill or AiCommand.HoldFire or AiCommand.Engage or AiCommand.Doctrine
        or AiCommand.SideDoctrine or AiCommand.Home or AiCommand.SpeedLimit or AiCommand.Clearance or AiCommand.Detach or AiCommand.Form
        or AiCommand.Role);

    void Route(World world, in Command c, Unit u)
    {
        switch ((AiCommand)c.Code)
        {
            case AiCommand.Doctrine or AiCommand.SideDoctrine:
                if (c.A is < 0 or >= Doctrine.FieldCount)
                    throw new ArgumentException($"no doctrine field {c.A}");
                if ((AiCommand)c.Code == AiCommand.Doctrine)
                    Doctrine.Set(u, (DoctrineField)c.A, c.X);
                else
                    Doctrine.SetSide(c.Entity, (DoctrineField)c.A, c.X);
                WakeAll(world);
                return;
            case AiCommand.Role:
                Roles.Set(u, (UnitRole)c.A);
                WakeAll(world);
                return;
            case AiCommand.Home:
                homes[c.Entity & 255] = c.A == 0 ? null : Earth.Position(Earth.UnpackLatLon(c.A).Lat, Earth.UnpackLatLon(c.A).Lon);
                return;
        }
        if (u.IsShip)
            Captains.Order(Context(world), c);
        else if (u.IsGroup)
            Admirals.Order(Context(world), c);
        WakeAll(world);
    }

    /// <summary>Starts the ticks of the layers that have something to watch. Groups and ships change by the world's
    /// commands, not the AI's, so this also runs after every command the world applies (<see cref="Applied"/>).</summary>
    void WakeAll(World world)
    {
        for (int i = 0; i < layers.Length; i++)
            Wake(world, i);
    }

    public void Applied(World world, in Command c)
    {
        if (c.System == ShipSystem.Id && (ShipCommand)c.Code == ShipCommand.Drop)
            Captains.Forget(c.Entity);
        WakeAll(world);
    }

    /// <summary>Ships came off rails (a contact): their captains and admirals think at their fine-tick rate again.</summary>
    public void OffRails(World world) => WakeAll(world);

    /// <summary>Schedules a layer's next think at its period now, on a grid of world time; a think already due sooner
    /// stands, one due later (a slow rails period, now a ship is back on the fine tick) is replaced.</summary>
    void Wake(World world, int layer)
    {
        if (!layers[layer].Active)
            return;
        long p = layers[layer].PeriodMs(Context(world));
        long next = (world.Time.Ms / p + 1) * p;
        if (nextTick[layer] >= 0 && nextTick[layer] <= next)
            return;
        nextTick[layer] = next;
        tickGen[layer]++;
        world.Schedule(new WorldTime(next), EventTier.Fine, layer - layers.Length, Id, (ushort)layer, tickGen[layer]);   // before the ship tick, the top layer first
    }

    /// <summary>When a layer (top-down: commanders 0, tasks 1, admirals 2, captains 3) next thinks, ms of world time, or -1 (tests).</summary>
    internal long NextThinkMs(int layer) => nextTick[layer];

    public void Handle(World world, in SimEvent e)
    {
        int layer = e.Code;
        if (e.Generation != tickGen[layer])
            return;   // replaced by a sooner one
        nextTick[layer] = -1;
        long t = Probes.Now;
        layers[layer].Think(Context(world));
        probes.Add(layerProbes[layer], t);
        WakeAll(world);   // a layer's orders can give the one below something to do (an admiral's first stations)
    }

    public void Hash(ref StateHash h)
    {
        foreach (long t in nextTick)
            h.Add(t);
        foreach (int g in tickGen)
            h.Add(g);
        foreach (var l in layers)
            l.Hash(ref h);
        Doctrine.Hash(ref h);
        Roles.Hash(ref h);
        for (int side = 0; side < homes.Length; side++)
            if (homes[side] is { } p)
            {
                h.Add(side);
                h.Add(p.X);
                h.Add(p.Y);
                h.Add(p.Z);
            }
    }

    public void Fill(Snapshot s)
    {
        for (int side = 0; side < s.Aggression.Length; side++)
            s.Aggression[side] = Doctrine.SideValue(side, DoctrineField.Aggression) is var a && double.IsNaN(a) ? 0 : a;
        foreach (var l in layers)
            l.Fill(s);
        Roles.Fill(s);
    }
}
