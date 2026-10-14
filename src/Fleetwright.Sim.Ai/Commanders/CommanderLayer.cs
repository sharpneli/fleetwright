using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Commanders;

/// <summary>Whether a side has a commander (docs/battle/command.md 5.1d, decisions 4 and 5).</summary>
public enum CommanderMode : byte
{
    /// <summary>No commander: a world told nothing (a dev's, a test's) plays as before him.</summary>
    None,

    /// <summary>On unless it's the player's side: what a scenario gives every side. Watching (no player), every side's on.</summary>
    Auto,

    On,
    Off,
}

/// <summary>How a side's commander sees the battle (docs/battle/command.md "The commander, v0, rule by rule").</summary>
public enum CommanderPhase : byte
{
    None,

    /// <summary>No enemy held: the plan, and idle units given something to do.</summary>
    NoContact,
}

/// <summary>Why the commander stands as he does.</summary>
public enum CommanderReason : byte
{
    None,
}

/// <summary>What a top unit is to its commander, by what it's made of.</summary>
public enum UnitRole : byte
{
    /// <summary>Not his to use: fallen out, held by the player, or gone.</summary>
    None,

    /// <summary>The side's strongest unit with capital ships (else its strongest): what the rest support.</summary>
    MainBody,

    /// <summary>Another unit with capital ships.</summary>
    Heavy,

    /// <summary>Cruisers: a unit with guns and no capital ship that isn't torpedo craft.</summary>
    Scout,

    /// <summary>Torpedo craft, by its guide's design.</summary>
    Flotilla,

    /// <summary>No main battery (a seaplane carrier, a tender).</summary>
    Auxiliary,
}

/// <summary>Why the commander gave a unit the task he did (the timeline's, and the UI's reason tag).</summary>
public enum CommanderWhy : byte
{
    None,

    /// <summary>Its task ended and nothing came after it: its role's task with no enemy about.</summary>
    Idle,
}

/// <summary>The side's commander (docs/battle/command.md 5.1d), the layer above the tasks: one per side, the C-in-C on
/// the scene. He commands his side's top units (groups that are no force's, ships in no group) by tasks, the same unit
/// orders the player gives (<see cref="OrderSource.Commander"/>), and never anything below them: a force's admiral turns
/// its task into orders for his squadrons. He reads his units' states (their tasks' reports, through the task layer) and
/// his navy's picture, and decides by rules with a margin either way, never by searching ahead.
/// <para><b>Who he commands:</b> a world told nothing has no commanders (<see cref="CommanderMode.None"/>); a scenario
/// gives every side one that's on unless it's the player's (<see cref="CommanderMode.Auto"/>), and a command switches a
/// side's on or off. Once he's thought, the player's own order to one of his units (a move, or a task) takes it from
/// him, as an order to a held unit does from its admiral (2.6); <see cref="AiCommand.Automate"/> hands it back ("leave
/// it to the admiral") or takes it, on a side whose commander is off too.</para>
/// <para><b>Idle units:</b> a unit on the orders the scenario gave it is on its orders; one whose task ended with nothing
/// after it (its plan's run out, <see cref="TaskLayer.Idle"/>) is given its role's task (<see cref="IdleTask"/>), so
/// nothing is idle without a reason.</para>
/// <para>Thinks every <see cref="SimSettings.CommanderPeriodMs"/> while any side has a commander. His records are kept
/// in unit order; nothing allocated once grown.</para></summary>
public sealed class CommanderLayer(GroupSystem groups, ShipStore store) : IAiLayer
{
    sealed class SideState
    {
        public CommanderMode Mode;
        public bool Started;   // he's thought: from now on the player's orders take his units from him
        public CommanderPhase Phase;
        public CommanderReason Reason;
    }

    /// <summary>A unit he's given something, or whose switch is set.</summary>
    struct Rec
    {
        public int Unit;
        public sbyte Switch;        // 1 his (handed to him), 0 not (taken by the player), -1 as its side
        public TaskKind Given;      // what he last gave it
        public int Target;
        public CommanderWhy Why;
        public long Ms;
    }

    /// <summary>A top unit at this think.</summary>
    struct Top
    {
        public Unit Unit;
        public byte Side;
        public int Slot;            // its guide's
        public UnitRole Role;
        public double Strength;
    }

    readonly SideState?[] sides = new SideState?[256];
    Rec[] recs = new Rec[16];
    int count;
    readonly List<Top> tops = new(64);
    readonly List<(int Ship, double AsternM, double AbeamM)> line = new(64);

    internal TaskLayer? Tasks { get; set; }
    internal CaptainLayer? Captains { get; set; }

    public bool Active
    {
        get
        {
            foreach (var s in sides)
                if (s is { Mode: not CommanderMode.None })
                    return true;
            return false;
        }
    }

    public int PeriodMs(in AiContext ctx) => ctx.Settings.CommanderPeriodMs;

    /// <summary>A side's commander's mode, and whether he commands now.</summary>
    public CommanderMode ModeOf(int side) => sides[side & 255]?.Mode ?? CommanderMode.None;

    public bool IsOn(int side, int playerSide) => On(sides[side & 255], side, playerSide);

    static bool On(SideState? s, int side, int playerSide) => s?.Mode switch
    {
        CommanderMode.Auto => side != playerSide,
        CommanderMode.On => true,
        _ => false,
    };

    /// <summary>How a side's commander sees the battle, and why.</summary>
    public (CommanderPhase Phase, CommanderReason Reason) PhaseOf(int side) =>
        sides[side & 255] is { } s ? (s.Phase, s.Reason) : (CommanderPhase.None, CommanderReason.None);

    /// <summary>What he last gave a unit, why and when; None if nothing.</summary>
    public (TaskKind Given, int Target, CommanderWhy Why, long Ms) GivenOf(Unit u) =>
        Find(u.Id) is int i and >= 0 ? (recs[i].Given, recs[i].Target, recs[i].Why, recs[i].Ms) : (TaskKind.None, 0, CommanderWhy.None, 0);

    /// <summary>How many units he has records of, and the i-th (in unit order): for reports (the play bench's timeline).</summary>
    public int Count => count;

    public (Unit Unit, TaskKind Given, int Target, CommanderWhy Why, long Ms) At(int i) =>
        (new Unit(recs[i].Unit), recs[i].Given, recs[i].Target, recs[i].Why, recs[i].Ms);

    /// <summary>Whether a unit is its side's commander's now: its switch, else its side's.</summary>
    public bool Commands(Unit u, int side, int playerSide)
    {
        var s = sides[side & 255];
        if (s == null || s.Mode == CommanderMode.None)
            return false;
        return Find(u.Id) is int i and >= 0 && recs[i].Switch >= 0 ? recs[i].Switch == 1 : On(s, side, playerSide);
    }

    /// <summary>A side's commander's mode (its command).</summary>
    internal void SetMode(int side, CommanderMode mode)
    {
        var s = sides[side & 255] ??= new SideState();
        s.Mode = mode;
    }

    /// <summary>A unit handed to its commander (1), taken from him (0), or as its side (NaN) (its command).</summary>
    internal void Automate(Unit u, double on)
    {
        ref var r = ref Get(u.Id);
        r.Switch = double.IsNaN(on) ? (sbyte)-1 : on != 0 ? (sbyte)1 : (sbyte)0;
    }

    /// <summary>The player's own order to a unit (a move, or a task): once its commander has thought, it's the player's.</summary>
    internal void PlayerOrdered(in AiContext ctx, Unit u)
    {
        int lead = u.ShipOf(groups);
        if (store.SlotOf(lead) is not (int slot and >= 0))
            return;
        byte side = store[slot].Side;
        if (sides[side] is { Started: true } && Commands(u, side, ctx.World.PlayerSide))
            Get(u.Id).Switch = 0;
    }

    public void Order(in AiContext ctx, in Command c) => throw new InvalidOperationException("the commander takes no unit orders");

    public void Think(in AiContext ctx)
    {
        int player = ctx.World.PlayerSide;
        Gather(ctx);
        for (int side = 0; side < sides.Length; side++)
        {
            var s = sides[side];
            if (s == null || s.Mode == CommanderMode.None)
                continue;
            s.Started = true;
            Command(ctx, s, (byte)side, player);   // his own units only: those of a side he's off for that were handed to him
        }
    }

    /// <summary>Every top unit of every side, its role and strength: groups that are no force's, ships in no group.</summary>
    void Gather(in AiContext ctx)
    {
        tops.Clear();
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (g.Parent != 0 || store.SlotOf(groups.GuideShip(g)) is not (int slot and >= 0))
                continue;
            groups.LineOffsets(g, line);
            bool heavy = false;
            double strength = 0;
            foreach (var (ship, _, _) in line)
                if (store.SlotOf(ship) is int m and >= 0)
                {
                    var cls = store[m].Handling.Class;
                    heavy |= Sightings.TypeOf(cls) == ShipType.Capital;
                    strength += Strength.Of(cls);
                }
            tops.Add(new Top { Unit = Unit.Group(g.GroupId), Side = store[slot].Side, Slot = slot, Strength = strength, Role = RoleOf(slot, heavy) });
        }
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || groups.Of(store[slot].Entity) != null)
                continue;
            var cls = store[slot].Handling.Class;
            var role = Captains!.Of(store[slot].Entity).Order == CaptainOrder.Home ? UnitRole.None   // fallen out: the captain's
                : RoleOf(slot, Sightings.TypeOf(cls) == ShipType.Capital);
            tops.Add(new Top { Unit = Unit.Ship(store[slot].Entity), Side = store[slot].Side, Slot = slot, Strength = Strength.Of(cls), Role = role });
        }
    }

    UnitRole RoleOf(int slot, bool heavy)
    {
        var cls = store[slot].Handling.Class;
        return heavy ? UnitRole.Heavy
            : CaptainLayer.TorpedoCraft(cls) ? UnitRole.Flotilla
            : Sightings.TypeOf(cls) == ShipType.Merchant ? UnitRole.Auxiliary
            : UnitRole.Scout;
    }

    /// <summary>One side's think: its main body, then each of his units by its role and the phase.</summary>
    void Command(in AiContext ctx, SideState s, byte side, int player)
    {
        // the main body: the strongest with capital ships, else the strongest
        int main = -1;
        for (int i = 0; i < tops.Count; i++)
        {
            var t = tops[i];
            if (t.Side != side || t.Role is UnitRole.None or UnitRole.Auxiliary)
                continue;
            bool heavy = t.Role == UnitRole.Heavy, best = main >= 0 && tops[main].Role == UnitRole.Heavy;
            if (main < 0 || heavy && !best || heavy == best && t.Strength > tops[main].Strength)
                main = i;
        }
        if (main >= 0 && tops[main].Role == UnitRole.Heavy)
            tops[main] = tops[main] with { Role = UnitRole.MainBody };
        s.Phase = CommanderPhase.NoContact;
        s.Reason = CommanderReason.None;

        for (int i = 0; i < tops.Count; i++)
        {
            var t = tops[i];
            if (t.Side != side || t.Role == UnitRole.None || !Commands(t.Unit, side, player))
                continue;
            if (Tasks!.Idle(t.Unit))
                Give(ctx, t, IdleTask(ctx, t, main >= 0 && main != i ? tops[main] : null), CommanderWhy.Idle);
        }
    }

    /// <summary>A unit's task with no enemy about, by its role: the main body searches ahead on its course; another heavy
    /// unit joins it (covers it, if it's a ship alone); scouts scout ahead of it, spread off its bow; flotillas screen it;
    /// auxiliaries go home. A unit with no main body to support is one.</summary>
    Command IdleTask(in AiContext ctx, in Top t, Top? main)
    {
        var o = ctx.Orders;
        const OrderSource By = OrderSource.Commander;
        if (main is not { } m || t.Role == UnitRole.MainBody)
        {
            if (t.Role == UnitRole.Auxiliary)
                return o.WithdrawHome(t.Unit, By);
            var guide = store[t.Slot];
            var p = guide.Position;
            var f = guide.Forward;
            Earth.Advance(ref p, ref f, ctx.Settings.SearchLegM);
            var (lat, lon) = Earth.LatLon(p);
            return o.Search(t.Unit, lat, lon, 0, By);
        }
        return t.Role switch
        {
            UnitRole.Heavy => m.Unit.IsGroup ? o.Join(t.Unit, m.Unit, By) : o.Cover(t.Unit, m.Unit, 0, By),
            UnitRole.Scout => o.Scout(t.Unit, m.Unit, 0, ScoutBearing(m.Unit), By),
            UnitRole.Flotilla => o.Screen(t.Unit, m.Unit, 0, By),
            _ => o.WithdrawHome(t.Unit, By),
        };
    }

    // a scouting line off the main body's bow: ahead, then a broad and a fine bow either side
    static readonly double[] ScoutBearings = [0, 45, -45, 90, -90];

    /// <summary>The next scout's bearing off a unit's bow: past those he's already sent to scout for it.</summary>
    double ScoutBearing(Unit of)
    {
        int n = 0;
        for (int i = 0; i < count; i++)
            if (recs[i].Given == TaskKind.Scout && recs[i].Target == of.Id && Tasks!.Of(new Unit(recs[i].Unit)).Kind == TaskKind.Scout)
                n++;
        return ScoutBearings[n % ScoutBearings.Length];
    }

    /// <summary>A task given to a unit, and remembered with why.</summary>
    void Give(in AiContext ctx, in Top t, in Command task, CommanderWhy why)
    {
        ctx.Issue(task);
        ref var r = ref Get(t.Unit.Id);
        (r.Given, r.Target, r.Why, r.Ms) = (TaskLayer.KindOf((AiCommand)task.Code), (int)task.A, why, ctx.NowMs);
        if ((AiCommand)task.Code is AiCommand.GoTo or AiCommand.Patrol or AiCommand.Search or AiCommand.Withdraw)
            r.Target = 0;   // a point, not a unit
    }

    public void Hash(ref StateHash h)
    {
        for (int side = 0; side < sides.Length; side++)
            if (sides[side] is { } s)
            {
                h.Add(side);
                h.Add((int)s.Mode);
                h.Add(s.Started);
                h.Add((int)s.Phase);
                h.Add((int)s.Reason);
            }
        for (int i = 0; i < count; i++)
        {
            ref readonly var r = ref recs[i];
            h.Add(r.Unit);
            h.Add(r.Switch);
            h.Add((int)r.Given);
            h.Add(r.Target);
            h.Add((int)r.Why);
            h.Add(r.Ms);
        }
    }

    public void Fill(Snapshot s)
    {
        int player = s.PlayerSide;
        for (int side = 0; side < sides.Length; side++)
        {
            var st = sides[side];
            s.CommanderMode[side] = (byte)(st?.Mode ?? CommanderMode.None);
            s.CommanderOn[side] = On(st, side, player);
            s.CommanderPhase[side] = (byte)(st?.Phase ?? CommanderPhase.None);
            s.CommanderReason[side] = (byte)(st?.Reason ?? CommanderReason.None);
        }
        var gv = s.Groups;
        for (int k = 0; k < gv.Count; k++)
            gv.Commanded[k] = gv.Parent[k] != 0 ? (byte)0 : Mark(Unit.Group(gv.Id[k]), gv.GuideShip[k], player);
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
            v.Commanded[k] = groups.Of(v.Entity[k]) != null ? (byte)0 : Mark(Unit.Ship(v.Entity[k]), v.Entity[k], player);
    }

    /// <summary>A top unit's mark: his (1), held from him (2), or neither (0).</summary>
    byte Mark(Unit u, int lead, int player)
    {
        if (store.SlotOf(lead) is not (int slot and >= 0) || sides[store[slot].Side] is not { Mode: not CommanderMode.None })
            return 0;
        if (Commands(u, store[slot].Side, player))
            return 1;
        return Find(u.Id) is int i and >= 0 && recs[i].Switch == 0 ? (byte)2 : (byte)0;
    }

    ref Rec Get(int unit)
    {
        int i = Find(unit);
        if (i >= 0)
            return ref recs[i];
        if (count == recs.Length)
            Array.Resize(ref recs, recs.Length * 2);
        i = count;
        while (i > 0 && recs[i - 1].Unit > unit)
        {
            recs[i] = recs[i - 1];
            i--;
        }
        recs[i] = new Rec { Unit = unit, Switch = -1 };
        count++;
        return ref recs[i];
    }

    int Find(int unit)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int id = recs[mid].Unit;
            if (id == unit)
                return mid;
            if (id < unit)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return -1;
    }
}
