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

    /// <summary>A side's home, if it has one.</summary>
    public Vec3d? HomeOf(int side) => Homes.OfSide(side);

    /// <summary>Every side's aims, and how they stand (docs/battle/aims.md).</summary>
    public AimBook Aims { get; } = new();

    /// <summary>Where each side's ships go out of the fight, and a ship's own while an aim wants her somewhere.</summary>
    public Homes Homes { get; }

    readonly World world;

    /// <summary>Orders to units (ships and groups), as commands to send: the player's way in, and the layers'.</summary>
    public UnitOrders Orders => new(Id);

    public AiSystem(World world)
    {
        this.world = world;
        Homes = new Homes(Aims);
        Seen = new SeenContacts();
        Doctrine = new Doctrine(world.Groups, world.Settings.AggressionShift);
        Judge = new MatchupJudge(world.Groups, Seen, Doctrine);
        Held = new HeldGroups(Judge.Matchups);
        Captains = new CaptainLayer(world.Groups, world.Encounters, Seen, Doctrine, Homes)
        {
            Judge = Judge, TrailPoints = world.Settings.FollowTrackPoints, Store = world.Ships.Store, Book = Aims,
        };
        Roles = new Roles(world, Judge);
        Admirals = new AdmiralLayer(world.Groups, Captains, Seen, Doctrine) { Judge = Judge, Roles = Roles };
        Captains.Admirals = Admirals;
        Tactical = new TacticalPicture(Seen, Judge.Matchups);
        Admirals.Tactical = Tactical;
        Tasks = new TaskLayer(world.Groups, Seen, Homes) { Admirals = Admirals, Captains = Captains, Doctrine = Doctrine, Judge = Judge };
        (Admirals.Tasks, Captains.Tasks) = (Tasks, Tasks);
        Attacks = new FlotillaAttacks(Judge, Held, Doctrine, Roles) { Tasks = Tasks };
        Commanders = new CommanderLayer(world.Groups, world.Ships.Store, Seen, Doctrine) { Tasks = Tasks, Captains = Captains, Judge = Judge, Held = Held, Roles = Roles, Attacks = Attacks, Book = Aims };
        (Admirals.Attacks, Admirals.Commanders) = (Attacks, Commanders);
        layers = [Commanders, Tasks, Admirals, Captains];
        nextTick = new long[layers.Length];
        tickGen = new int[layers.Length];
        Array.Fill(nextTick, -1);
        picture = new NavyPicture(world);
        probes = world.Probes;
        layerProbes = [.. layers.Select(l => probes.Register("AI: " + l.GetType().Name))];
        Id = world.Register(this);
        world.Torpedoes.Launched = WatchTracks;
    }

    // the coarse torpedo watch's one wake (docs/battle/coarse.md J3): its moment, -1 with none, and its generation
    const ushort TrackWatch = 100;
    long watchMs = -1;
    int watchGen;

    /// <summary>The coarse captains' torpedo watch: woken at the first moment before their next think that a running
    /// torpedo comes into a coarse fighting ship's sight, or a comb ends (<see cref="CaptainLayer.NextTrackSeen"/>). Worked
    /// out again after their think and when torpedoes enter the water; a wake due sooner stands.</summary>
    void WatchTracks(World world)
    {
        if (world.Ships.Store.CoarseCount == 0)
            return;
        int captains = Array.IndexOf(layers, Captains);
        long until = nextTick[captains] >= 0 ? nextTick[captains] : world.Time.Ms + world.Settings.CoarseStepMs;
        long next = Captains.NextTrackSeen(Context(world), until);
        if (next >= until || watchMs >= 0 && watchMs <= next)
            return;
        watchMs = next;
        watchGen++;
        world.Schedule(new WorldTime(next), EventTier.Fine, -layers.Length - 1, Id, TrackWatch, watchGen);
    }

    // the coarse fire watch's one wake (docs/battle/night.md Q11): its moment, -1 with none, and its generation
    const ushort FireWatch = 101;
    long fireMs = -1;
    int fireGen;

    /// <summary>The coarse fire watch: torpedoes fired as the tubes come round, between thinks a step apart. While a
    /// flotilla's signal to fire stands (<see cref="AdmiralLayer.WatchSignals"/>), or a captain's think left a ship with a
    /// target she couldn't fire at yet (<see cref="CaptainLayer.WatchTorpedoes"/>), woken every
    /// <see cref="SimSettings.CaptainPeriodMs"/> until that layer's next think, as the fine tier's thinks go. Nothing while
    /// the layer thinks at that rate itself (the fine tier).</summary>
    void WatchFire(World world)
    {
        long step = world.Settings.CaptainPeriodMs, next = (world.Time.Ms / step + 1) * step;
        int admirals = Array.IndexOf(layers, Admirals), captains = Array.IndexOf(layers, Captains);
        bool admiralsLater = nextTick[admirals] < 0 || nextTick[admirals] > next, captainsLater = nextTick[captains] < 0 || nextTick[captains] > next;
        if (!(Admirals.Signalling && admiralsLater || Captains.TorpedoesWaiting && captainsLater) || fireMs >= 0 && fireMs <= next)
            return;
        fireMs = next;
        fireGen++;
        world.Schedule(new WorldTime(next), EventTier.Fine, -layers.Length - 2, Id, FireWatch, fireGen);
    }

    AiContext Context(World world) => new(world, picture, Id);

    public void Apply(World world, in Command c)
    {
        if ((AiCommand)c.Code is < AiCommand.Course or > AiCommand.AimShip)
            throw new ArgumentException($"unknown AI command {c.Code}");
        var u = new Unit(c.Entity);
        var code = (AiCommand)c.Code;
        var from = OrderFrom.Of(c);
        var source = from.Source;
        switch (code)
        {
            case AiCommand.Aim:
            {
                int about = c.Entity >> 24 & 255;
                var (lat, lon) = Earth.UnpackLatLon(c.A);
                Aims.Set(c.Entity & 255, c.Entity >> 8 & 255, new Aim((AimKind)(c.Entity >> 16 & 255), c.Y)
                {
                    Zone = c.X > 0 ? new Zone(lat, lon, c.X) : default, Of = c.B, About = about == 255 ? -1 : about,
                });
                return;
            }
            case AiCommand.AimFigures:
            {
                int side = c.Entity & 255, i = c.Entity >> 8 & 255;
                if (i >= Aims.Count(side))
                    throw new ArgumentException($"no aim {i} of side {side}");
                Aims.Set(side, i, Aims[side, i] with { StayMs = c.A, Tonnes = c.X });
                return;
            }
            case AiCommand.AimShip:
                Aims.Name(c.B & 255, c.B >> 8 & 255, c.Entity);
                return;
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
        or AiCommand.Shape or AiCommand.Role or AiCommand.Shells or AiCommand.Beams or AiCommand.StarShell);

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
                Homes.Set(c.Entity, c.A == 0 ? null : Earth.Position(Earth.UnpackLatLon(c.A).Lat, Earth.UnpackLatLon(c.A).Lon));
                return;
            case AiCommand.Shells:
                if (c.A is < 0 or > (long)Ships.ShellKind.He + 1)
                    throw new ArgumentException($"no shell kind {c.A - 1}");
                HoldShells(world, u, (byte)c.A);
                WakeAll(world);
                return;
            case AiCommand.Beams:
                HoldBeams(world, u, c.X != 0);
                WakeAll(world);
                return;
            case AiCommand.StarShell:
                StarShell(world, u, c);
                WakeAll(world);
                return;
        }
        if (u.IsShip)
            Captains.Order(Context(world), c);
        else if (u.IsGroup)
            Admirals.Order(Context(world), c);
        WakeAll(world);
    }

    /// <summary>A shell order to every ship down a unit's tree (docs/battle/shell-choice.md A5).</summary>
    void HoldShells(World world, Unit u, byte kind)
    {
        if (u.IsShip)
            Captains.HoldShells(u.Id, kind);
        else if (u.IsGroup && world.Groups.Get(u.GroupId) is { } g)
            foreach (int m in g.Members)
                HoldShells(world, g.IsForce ? Unit.Group(m) : Unit.Ship(m), kind);
    }

    /// <summary>The player's beams to every ship down a unit's tree (docs/battle/night.md Q9).</summary>
    void HoldBeams(World world, Unit u, bool on)
    {
        if (u.IsShip)
            Captains.HoldBeams(Context(world), u.Id, on);
        else if (u.IsGroup && world.Groups.Get(u.GroupId) is { } g)
            foreach (int m in g.Members)
                HoldBeams(world, g.IsForce ? Unit.Group(m) : Unit.Ship(m), on);
    }

    /// <summary>The player's star shell (docs/battle/night.md Q9): to a ship, hers; to a group, one ship of its tree's, the
    /// nearest that carries star shell with the target in its reach, else the nearest that carries it. A 0 stops every
    /// ship's down the tree.</summary>
    void StarShell(World world, Unit u, in Command c)
    {
        bool point = c.X != 0;
        if (!point && c.A == 0)
        {
            StopStars(world, u);
            return;
        }
        int ship = u.Id;
        if (u.IsGroup)
        {
            var at = point ? Earth.Position(Earth.UnpackLatLon(c.A).Lat, Earth.UnpackLatLon(c.A).Lon) : default;
            (ship, double best) = (0, double.MaxValue);
            Nearest(world, u, ref ship, ref best, point, at, (int)c.A);
            if (ship == 0)
                return;   // nobody in it carries star shell
            StopStars(world, u);
        }
        Captains.HoldStar(Context(world), ship, point ? 0 : (int)c.A, point ? c.A : 0);
    }

    void StopStars(World world, Unit u)
    {
        if (u.IsShip)
            Captains.HoldStar(Context(world), u.Id, 0, 0);
        else if (u.IsGroup && world.Groups.Get(u.GroupId) is { } g)
            foreach (int m in g.Members)
                StopStars(world, g.IsForce ? Unit.Group(m) : Unit.Ship(m));
    }

    // the ship down a unit's tree that carries star shell nearest the target: those with it in reach first (their
    // distance), then the rest (past every one in reach)
    void Nearest(World world, Unit u, ref int ship, ref double best, bool point, Vec3d at, int contact)
    {
        if (u.IsGroup)
        {
            if (world.Groups.Get(u.GroupId) is { } g)
                foreach (int m in g.Members)
                    Nearest(world, g.IsForce ? Unit.Group(m) : Unit.Ship(m), ref ship, ref best, point, at, contact);
            return;
        }
        double reach = world.StarShells.ReachM(u.Id);
        if (reach <= 0 || world.Ships.Store.SlotOf(u.Id) is not (>= 0 and var slot))
            return;
        var own = world.Ships.Store[slot];
        var to = at;
        if (!point)
        {
            var pic = world.Sightings.Of(own.Side);
            if (pic?.IndexOf(contact) is not (>= 0 and var ci))
                return;
            to = pic[ci].Position;
        }
        double d = Earth.Distance(own.Position, to);
        double key = d <= reach ? d : 1e9 + d;
        if (key < best)
            (ship, best) = (u.Id, key);
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
        long t = Probes.Now;
        if (e.Code == TrackWatch)
        {
            if (e.Generation != watchGen)
                return;
            watchMs = -1;
            Captains.WatchTracks(Context(world));
            probes.Add(layerProbes[Array.IndexOf(layers, Captains)], t);
            WakeAll(world);
            WatchTracks(world);
            return;
        }
        if (e.Code == FireWatch)
        {
            if (e.Generation != fireGen)
                return;
            fireMs = -1;
            var ctx = Context(world);
            Admirals.WatchSignals(ctx);
            Captains.WatchTorpedoes(ctx);
            probes.Add(layerProbes[Array.IndexOf(layers, Captains)], t);
            WatchFire(world);
            return;
        }
        int layer = e.Code;
        if (e.Generation != tickGen[layer])
            return;   // replaced by a sooner one
        nextTick[layer] = -1;
        layers[layer].Think(Context(world));
        probes.Add(layerProbes[layer], t);
        WakeAll(world);   // a layer's orders can give the one below something to do (an admiral's first stations)
        if (layers[layer] == Captains)
            WatchTracks(world);
        if (layers[layer] == Captains || layers[layer] == Admirals)
            WatchFire(world);
    }

    public void Hash(ref StateHash h)
    {
        foreach (long t in nextTick)
            h.Add(t);
        foreach (int g in tickGen)
            h.Add(g);
        if (watchGen != 0)
        {
            h.Add(watchMs);   // only then, so a world never coarse in a fight hashes as it did before the watch
            h.Add(watchGen);
        }
        if (fireGen != 0)
        {
            h.Add(fireMs);   // only then, as the torpedo watch's
            h.Add(fireGen);
        }
        foreach (var l in layers)
            l.Hash(ref h);
        Doctrine.Hash(ref h);
        Roles.Hash(ref h);
        Aims.Hash(ref h);
        Homes.Hash(ref h);
    }

    public void Fill(Snapshot s)
    {
        for (int side = 0; side < s.Aggression.Length; side++)
            s.Aggression[side] = Doctrine.SideValue(side, DoctrineField.Aggression) is var a && double.IsNaN(a) ? 0 : a;
        foreach (var l in layers)
            l.Fill(s);
        Roles.Fill(s);
        Aims.Fill(world, s);
    }
}
