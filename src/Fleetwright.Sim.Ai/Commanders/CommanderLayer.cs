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

    /// <summary>On unless it's the player's side: the player's "AI commands" switched off (a scenario gives every side
    /// <see cref="On"/>, decision 14). Watching (no player), every side's on.</summary>
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

    /// <summary>An enemy held (a fresh contact): each enemy group closed by the units that can take it on.</summary>
    Contact,

    /// <summary>His units in a fight: as in contact, and his flotillas sent in on an enemy line his heavy units engage.</summary>
    Battle,

    /// <summary>The enemy met and lost (no contact fresh): his main body searches where he was last held.</summary>
    Lost,

    /// <summary>Night, the enemy held, and doctrine avoids action: his heavy units withdrawn from contact, his scouts
    /// keeping in touch, his flotillas sent in by doctrine.</summary>
    Night,

    /// <summary>Declining battle (outmatched) or broken off (his losses): every unit for home, his flotillas sent in to
    /// cover it.</summary>
    Retiring,

    /// <summary>He's given up (the enemy lost, and not found again): every unit for home.</summary>
    Home,
}

/// <summary>Why the commander stands as he does.</summary>
public enum CommanderReason : byte
{
    None,

    /// <summary>His side's matchup against all he holds is under doctrine's bar (with his margin), and he can get away.</summary>
    Outmatched,

    /// <summary>His losses past doctrine's break off share of what he came with.</summary>
    BrokenOff,

    /// <summary>No contact fresh, after he'd met the enemy.</summary>
    LostContact,

    /// <summary>His search where the enemy was last held found nothing.</summary>
    GaveUp,
}

/// <summary>Why the commander gave a unit the task he did (the timeline's, and the UI's reason tag).</summary>
public enum CommanderWhy : byte
{
    None,

    /// <summary>Its task ended and nothing came after it: its role's task with no enemy about.</summary>
    Idle,

    /// <summary>The enemy held: a unit sent to close an enemy group it can take on, with the others sent.</summary>
    Contact,

    /// <summary>A flotilla sent in on an enemy line his heavy units are engaging, its attack worth it.</summary>
    Battle,

    /// <summary>A flotilla's attack over: back into its force's line, or to its screen once its torpedoes are spent.</summary>
    Rejoin,

    /// <summary>The enemy lost: the main body searching where he was last held.</summary>
    Lost,

    /// <summary>Home: declining battle with a stronger enemy.</summary>
    Outmatched,

    /// <summary>Home: broken off.</summary>
    BrokenOff,

    /// <summary>Home: given up.</summary>
    GaveUp,

    /// <summary>A flotilla sent in to cover the others breaking off.</summary>
    Cover,

    /// <summary>Night: a heavy unit withdrawn from contact; a flotilla sent in on a heavy enemy in reach.</summary>
    Night,
}

/// <summary>The side's commander (docs/battle/command.md 5.1d), the layer above the tasks: one per side, the C-in-C on
/// the scene. He commands his side's top units (groups that are no force's, ships in no group) by tasks, the same unit
/// orders the player gives (<see cref="OrderSource.Commander"/>), and never anything below them: a force's admiral turns
/// its task into orders for his squadrons. He reads his units' states (their tasks' reports, through the task layer) and
/// his navy's picture, and decides by rules with a margin either way, never by searching ahead.
/// <para><b>Who he commands:</b> a world told nothing has no commanders (<see cref="CommanderMode.None"/>); a scenario
/// gives every side one that's on, the player's too (<see cref="CommanderMode.On"/>; docs/battle/command.md decision 14:
/// no one can steer every squadron of a fleet, so the player takes the ones they want), and a command switches a side's
/// on or off. The player's own order by hand to one of his units (a move, or a task; <see cref="UnitOrders.Hand"/>)
/// takes it from him at once, as an order to a held unit does from its admiral (2.6), never a scenario's opening orders;
/// <see cref="AiCommand.Automate"/> hands it back ("leave it to the admiral") or takes it, on a side whose commander is
/// off too.</para>
/// <para><b>Idle units:</b> a unit on the orders the scenario gave it is on its orders until the enemy's met (they were
/// for the approach); one whose task ended with nothing after it (its plan's run out, <see cref="TaskLayer.Idle"/>), or
/// on its first orders once the enemy's met, is given its role's task (<see cref="IdleTask"/>), so nothing is idle
/// without a reason.</para>
/// <para>Thinks every <see cref="SimSettings.CommanderPeriodMs"/> while any side has a commander. His records are kept
/// in unit order; nothing allocated once grown.</para></summary>
public sealed class CommanderLayer(GroupSystem groups, ShipStore store, SeenContacts seen, Doctrine doctrine) : IAiLayer
{
    sealed class SideState
    {
        public CommanderMode Mode;
        public CommanderPhase Phase;
        public CommanderReason Reason;
        public int Target;     // the biggest ship of the gravest enemy group he holds (0: none)
        public double Confidence;   // his side's matchup against all he holds
        public double TargetC;      // his side's matchup against the target's group
        public long LastAt;    // where the target was last held, a packed lat/lon (0: never)
        public double CameT;   // the tonnage he came with (at his first think)
        public int LastTarget;
        public bool Met, Declined, BrokenOff, Searched, GaveUp;
        public readonly MatchupSide DeclinedFrom = new();   // the gravest enemy he's held since he declined
        public double DeclinedC;    // his side's matchup against it, when he held it
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
        public double TonnageT, CapitalT;   // its ships', and its capital ships'
        public int Group;           // the enemy group it's sent after at this think (-1: none)
    }

    readonly SideState?[] sides = new SideState?[256];
    Rec[] recs = new Rec[16];
    int count;
    readonly List<Top> tops = new(64);
    readonly List<(int Ship, double AsternM, double AbeamM)> line = new(64);

    internal TaskLayer? Tasks { get; set; }
    internal MatchupJudge Judge { get; set; } = null!;
    internal HeldGroups Held { get; set; } = null!;
    internal Roles Roles { get; set; } = null!;
    internal CaptainLayer? Captains { get; set; }
    internal FlotillaAttacks Attacks { get; set; } = null!;

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

    /// <summary>A side's top units as they stand (for reports: "nothing idle"): on a task, home, on the orders the scenario
    /// gave them (never tasked), idle (through with a task, nothing after it), fallen out (their captains').</summary>
    public (int Tasked, int Home, int OnOrders, int Idle, int FallenOut) Census(int side)
    {
        int tasked = 0, home = 0, orders = 0, idle = 0, fallen = 0;
        void Count(Unit u)
        {
            var (kind, state, _) = Tasks!.Of(u);
            if (Home(u))
                home++;
            else if (kind == TaskKind.None)
                orders++;
            else if (Tasks.Idle(u))
                idle++;
            else if (state is TaskState.OnTask or TaskState.Engaged or TaskState.InContact || Tasks.QueuedOf(u) > 0)
                tasked++;
            else
                idle++;
        }
        for (int k = 0; k < groups.Count; k++)
            if (groups[k].Parent == 0 && store.SlotOf(groups.GuideShip(groups[k])) is int slot and >= 0 && store[slot].Side == side)
                Count(Unit.Group(groups[k].GroupId));
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || store[slot].Side != side || groups.Of(store[slot].Entity) != null || !Aims.Afloat(store, slot))
                continue;
            if (Captains!.Of(store[slot].Entity).Order == CaptainOrder.Home)
                fallen++;
            else
                Count(Unit.Ship(store[slot].Entity));
        }
        return (tasked, home, orders, idle, fallen);
    }

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

    /// <summary>The player's own order to a unit by hand (a move, or a task): it's the player's, not its commander's.</summary>
    internal void PlayerOrdered(in AiContext ctx, Unit u)
    {
        int lead = u.ShipOf(groups);
        if (store.SlotOf(lead) is not (int slot and >= 0))
            return;
        byte side = store[slot].Side;
        if (Commands(u, side, ctx.World.PlayerSide))
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
            Command(ctx, s, (byte)side, player);   // his own units only: those of a side he's off for that were handed to him
        }
    }

    /// <summary>Every top unit of every side, its role and its tonnage: groups that are no force's, ships in no group. What's
    /// inside a force is its admiral's (docs/battle/admirals-orders.md O7b): he sends in his own flotillas.</summary>
    void Gather(in AiContext ctx)
    {
        tops.Clear();
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (store.SlotOf(groups.GuideShip(g)) is not (int slot and >= 0))
                continue;
            if (g.Parent != 0)
                continue;
            groups.LineOffsets(g, line);
            double tonnage = 0, capital = 0;
            foreach (var (ship, _, _) in line)
                if (store.SlotOf(ship) is int m and >= 0)
                {
                    var cls = store[m].Handling.Class;
                    tonnage += cls.Motion.FullDisplacementT;
                    if (Sightings.TypeOf(cls) == ShipType.Capital)
                        capital += cls.Motion.FullDisplacementT;
                }
            tops.Add(new Top { Unit = Unit.Group(g.GroupId), Side = store[slot].Side, Slot = slot, TonnageT = tonnage, CapitalT = capital,
                Role = Roles.Of(Unit.Group(g.GroupId)), Group = -1 });
        }
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || groups.Of(store[slot].Entity) != null)
                continue;
            var cls = store[slot].Handling.Class;
            bool capital = Sightings.TypeOf(cls) == ShipType.Capital;
            var role = Captains!.Of(store[slot].Entity).Order == CaptainOrder.Home ? UnitRole.None   // fallen out: the captain's
                : Roles.Of(Unit.Ship(store[slot].Entity));
            tops.Add(new Top { Unit = Unit.Ship(store[slot].Entity), Side = store[slot].Side, Slot = slot, TonnageT = cls.Motion.FullDisplacementT,
                CapitalT = capital ? cls.Motion.FullDisplacementT : 0, Role = role, Group = -1 });
        }
    }

    /// <summary>One side's think: his main body, the enemy as his navy holds him (in groups), how the balance stands, which
    /// of his units go after which group, how he sees the battle; then each of his units by its role.</summary>
    void Command(in AiContext ctx, SideState s, byte side, int player)
    {
        // the main body: the most capital ships' tonnage, else the biggest (torpedo craft and screens only if there's
        // nothing else)
        int main = -1;
        for (int i = 0; i < tops.Count; i++)
        {
            var t = tops[i];
            if (t.Side != side || t.Role is UnitRole.None or UnitRole.Auxiliary)
                continue;
            if (main < 0 || Bigger(t, tops[main]))
                main = i;
        }
        if (main >= 0 && tops[main].Role is UnitRole.Heavy or UnitRole.Scout)
            tops[main] = tops[main] with { Role = UnitRole.MainBody };
        var held = Held.Of(ctx, side);
        var at = main >= 0 ? store[tops[main].Slot].Position : Vec3d.Zero;
        Balance(ctx, s, side, held, at);
        Enemy(ctx, s, side, held, at);
        if (!(s.Declined || s.BrokenOff || s.GaveUp))
            Assign(ctx, side, player, held);
        bool battle = false;
        for (int i = 0; i < tops.Count && !battle; i++)
            battle = tops[i].Side == side && tops[i].Role != UnitRole.None && InContact(ctx, tops[i]);
        (s.Phase, s.Reason) = s.GaveUp ? (CommanderPhase.Home, CommanderReason.GaveUp)
            : s.BrokenOff ? (CommanderPhase.Retiring, CommanderReason.BrokenOff)
            : s.Declined ? (CommanderPhase.Retiring, CommanderReason.Outmatched)
            : s.Target != 0 && Night(ctx, side, main) ? (CommanderPhase.Night, CommanderReason.None)
            : battle ? (CommanderPhase.Battle, CommanderReason.None)
            : s.Target != 0 ? (CommanderPhase.Contact, CommanderReason.None)
            : s.Met ? (CommanderPhase.Lost, CommanderReason.LostContact)
            : (CommanderPhase.NoContact, CommanderReason.None);
        if (s.Phase == CommanderPhase.Lost && main >= 0 && Commands(tops[main].Unit, side, player) && Search(ctx, s, tops[main]))
            (s.GaveUp, s.Phase, s.Reason) = (true, CommanderPhase.Home, CommanderReason.GaveUp);

        for (int i = 0; i < tops.Count; i++)
        {
            var t = tops[i];
            if (t.Side != side || t.Role == UnitRole.None || !Commands(t.Unit, side, player))
                continue;
            if (t.Role == UnitRole.Flotilla && SendIn(ctx, s, side, held, t))
                continue;   // in to attack: by day on a line his heavy units engage, by night, or to cover a break off
            if (s.Phase is CommanderPhase.Retiring or CommanderPhase.Home)
            {
                Retire(ctx, s, t);
                continue;
            }
            if (s.Phase == CommanderPhase.Night && ByNight(ctx, t))
                continue;
            if (t.Group >= 0)
            {
                // sent after an enemy group: close its biggest ship, unless it's in a fight already (doctrine's)
                int target = held[t.Group].Biggest;
                if (!InContact(ctx, t) && FreeToClose(t.Unit, target))
                    Give(ctx, t, ctx.Orders.Intercept(t.Unit, target, 0, OrderSource.Commander), CommanderWhy.Contact);
                continue;
            }
            // a flotilla through with its torpedoes goes back to its screen at once, contact or not
            bool spent = t.Role == UnitRole.Flotilla && Tasks!.Of(t.Unit) is (TaskKind.Attack, TaskState.Done, TaskReason.Fired);
            bool firstOrders = Tasks!.Of(t.Unit).Kind == TaskKind.None;
            if (!spent && (!(Tasks.Idle(t.Unit) || firstOrders && s.Met) || InContact(ctx, t) || Home(t.Unit)))
                continue;   // on its task, or on the scenario's orders before the enemy's met, or fighting (doctrine's), or home
            Give(ctx, t, IdleTask(ctx, t, main >= 0 && main != i ? tops[main] : null), spent ? CommanderWhy.Rejoin : CommanderWhy.Idle);
        }
    }

    /// <summary>A flotilla of his sent in to attack, by the rule every commander shares (<see cref="FlotillaAttacks"/>):
    /// free to go (going home from a battle he declined counts, for the night), and worth it as he sees the battle. True if
    /// it was.</summary>
    bool SendIn(in AiContext ctx, SideState s, byte side, ReadOnlySpan<HeldGroup> held, in Top t)
    {
        bool declining = Tasks!.Of(t.Unit).Kind == TaskKind.Withdraw && GivenOf(t.Unit).Why == CommanderWhy.Outmatched;
        if (!Attacks.Free(ctx, t.Unit, t.Slot, declining))
            return false;
        var why = Attacks.SendIn(ctx, side, s.Phase, s.Reason, t.Unit, store[t.Slot].Position, held);
        if (why == CommanderWhy.None)
            return false;
        Give(ctx, t, ctx.Orders.Attack(t.Unit, 0, OrderSource.Commander), why);
        Attacks.Sent(t.Unit, why);
        return true;
    }

    static bool Bigger(in Top a, in Top b) =>
        Attending(a.Role) != Attending(b.Role) ? Attending(b.Role)
        : a.CapitalT != b.CapitalT ? a.CapitalT > b.CapitalT
        : a.TonnageT > b.TonnageT;

    /// <summary>Whether a role keeps a unit with the others (a flotilla, a screen): never the main body while there's
    /// another.</summary>
    static bool Attending(UnitRole r) => r is UnitRole.Flotilla or UnitRole.Screen;

    /// <summary>How the balance stands (docs/battle/command.md 5.1g): his losses against doctrine's break off (for good once
    /// past it); and his side's matchup against all he holds: under doctrine's bar by
    /// <see cref="SimSettings.CommanderMarginShare"/> and able to get away (faster than the enemy), he declines; over it by
    /// as much against what he holds and against what he declined (the gravest he's held since: a fleet seen in part later
    /// is still the fleet he declined), he accepts again; caught by what he can't beat, he fights. Holding nobody, he keeps
    /// what he decided: a decline lasts out of sight (he goes on home, and doesn't search back into the enemy he declined).</summary>
    void Balance(in AiContext ctx, SideState s, byte side, ReadOnlySpan<HeldGroup> held, Vec3d at)
    {
        double afloat = 0;
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && store[slot].Side == side && Aims.Afloat(store, slot))
                afloat += store[slot].Handling.Class.Hydrostatics.DisplacementT;
        if (s.CameT == 0)
            s.CameT = afloat;
        s.BrokenOff |= afloat < s.CameT * (1 - doctrine.OfGroup(null, side, DoctrineField.BreakOffShare));
        mine.Clear();
        foreach (var t in tops)
            if (t.Side == side && t.Role is not (UnitRole.None or UnitRole.Auxiliary))
                AddTop(ctx, t, mine);
        enemy.Clear();
        foreach (ref readonly var g in held)
            AddGroup(ctx, side, g, enemy);
        if (enemy.Count == 0 || mine.Count == 0)
        {
            s.Confidence = 0;
            return;   // a decline holds out of sight: he judges it again only on an enemy he holds, never searches back into it
        }
        var r = Judge.Judge(ctx, 0, side, at, mine, enemy);
        s.Confidence = r.Confidence;
        double bar = Judge.Bars(0, side).Accept, margin = ctx.Settings.CommanderMarginShare, low = bar / (1 + margin), high = bar * (1 + margin);
        if (!s.Declined)
        {
            if (r.Confidence < low && r.ACanRefuse(ctx.Settings))
                Decline(s, r.Confidence);
            return;
        }
        if (r.Confidence < s.DeclinedC)
            Decline(s, r.Confidence);   // graver than what he declined: it's what he remembers now
        if (r.Confidence < high && !r.ACanRefuse(ctx.Settings))
            s.Declined = false;   // caught by what he can't beat and can't get away from: he fights
        else if (r.Confidence > high && Judge.Judge(ctx, 0, side, at, mine, s.DeclinedFrom).Confidence > high)
            s.Declined = false;   // the balance has turned against what he held and what he declined alike: he accepts
    }

    /// <summary>He declines (or goes on declining) the enemy now in <see cref="enemy"/>, and remembers it: a decline is
    /// judged again against it, not against the part of it he holds later.</summary>
    void Decline(SideState s, double confidence)
    {
        s.Declined = true;
        s.DeclinedFrom.CopyFrom(enemy);
        s.DeclinedC = confidence;
    }

    /// <summary>Which of his units go after which enemy group (docs/battle/command.md 5.1g): the groups gravest first (the
    /// least his side as a whole is confident against); to each, those already closing it and then the nearest free units
    /// that aren't hopeless against it alone (at doctrine's bar), until together they're confident enough to seek a
    /// fight (doctrine's seek ratio). A group no set of his can take on is left alone, and those closing it are freed.
    /// Flotillas aren't sent this way: they're sent in to attack.</summary>
    void Assign(in AiContext ctx, byte side, int player, ReadOnlySpan<HeldGroup> held)
    {
        if (held.Length == 0)
            return;
        var pic = ctx.World.Sightings.Of(side)!;
        var (accept, seek) = Judge.Bars(0, side);
        // the groups, gravest first
        order.Clear();
        for (int k = 0; k < held.Length; k++)
        {
            enemy.Clear();
            AddGroup(ctx, side, held[k], enemy);
            order.Add((k, enemy.Count == 0 || mine.Count == 0 ? double.MaxValue : Judge.Judge(ctx, 0, side, held[k].Centre, mine, enemy).Confidence));
        }
        order.Sort((x, y) => x.C != y.C ? x.C.CompareTo(y.C) : x.K.CompareTo(y.K));
        foreach (var (k, _) in order)
        {
            var g = held[k];
            enemy.Clear();
            AddGroup(ctx, side, g, enemy);
            if (enemy.Count == 0)
                continue;
            set.Clear();
            sent.Clear();
            // those closing one of its ships already, then the free ones nearest it
            near.Clear();
            for (int i = 0; i < tops.Count; i++)
            {
                var t = tops[i];
                if (t.Side != side || t.Group >= 0 || t.Role is UnitRole.None or UnitRole.Auxiliary or UnitRole.Flotilla or UnitRole.Screen ||
                    !Commands(t.Unit, side, player))
                    continue;
                bool closing = Tasks!.Of(t.Unit) is (TaskKind.Intercept, TaskState.OnTask or TaskState.Engaged or TaskState.InContact, _) &&
                               Member(pic, side, g, Tasks.TargetOf(t.Unit));
                if (closing)
                {
                    sent.Add(i);
                    AddTop(ctx, t, set);
                }
                else if (FreeToClose(t.Unit, 0) && !InContact(ctx, t))
                    near.Add((i, (store[t.Slot].Position - g.Centre).LengthSquared));
            }
            near.Sort((x, y) => x.D != y.D ? x.D.CompareTo(y.D) : x.I.CompareTo(y.I));
            bool enough = set.Count > 0 && Judge.Judge(ctx, 0, side, g.Centre, set, enemy).Confidence >= seek;
            foreach (var (i, _) in near)
            {
                if (enough)
                    break;
                one.Clear();
                AddTop(ctx, tops[i], one);
                if (one.Count == 0 || Judge.Judge(ctx, 0, side, g.Centre, one, enemy).Confidence < accept)
                    continue;   // hopeless against it alone
                sent.Add(i);
                AddTop(ctx, tops[i], set);
                enough = Judge.Judge(ctx, 0, side, g.Centre, set, enemy).Confidence >= seek;
            }
            if (!enough)
            {
                // no set of his takes it on: those closing it are freed (their role's task)
                foreach (int i in sent)
                    if (Tasks!.Of(tops[i].Unit).Kind == TaskKind.Intercept)
                        Give(ctx, tops[i], IdleTask(ctx, tops[i], MainOf(side, i)), CommanderWhy.Idle);
                continue;
            }
            foreach (int i in sent)
                tops[i] = tops[i] with { Group = k };
        }
    }

    readonly List<(int K, double C)> order = new(16);
    readonly List<(int I, double D)> near = new(32);
    readonly List<int> sent = new(16);
    readonly MatchupSide mine = new(), enemy = new(), set = new(), one = new();

    /// <summary>His main body for a unit's idle task: the side's (none if it's the unit itself).</summary>
    Top? MainOf(byte side, int self)
    {
        for (int i = 0; i < tops.Count; i++)
            if (i != self && tops[i].Side == side && tops[i].Role == UnitRole.MainBody)
                return tops[i];
        return null;
    }

    /// <summary>Whether a contact is one of an enemy group's.</summary>
    bool Member(Picture pic, byte side, in HeldGroup g, int entity)
    {
        foreach (int i in Held.Members(side, g))
            if (pic[i].Entity == entity)
                return true;
        return false;
    }

    /// <summary>A top unit's ships into a side of a matchup, exactly.</summary>
    void AddTop(in AiContext ctx, in Top t, MatchupSide into)
    {
        if (t.Unit.IsGroup && groups.Get(t.Unit.GroupId) is { } g)
            Judge.AddGroup(ctx.World, g, into);
        else
            into.AddOwn(ctx.World, Judge.Matchups, t.Slot);
    }

    /// <summary>An enemy group's contacts into a side of a matchup, as they're taken.</summary>
    void AddGroup(in AiContext ctx, byte side, in HeldGroup g, MatchupSide into)
    {
        var pic = ctx.World.Sightings.Of(side)!;
        foreach (int i in Held.Members(side, g))
            into.AddContact(ctx.World, Judge.Matchups, side, pic[i]);
    }

    /// <summary>The enemy lost: his main body searches about where he was last held (the picture's estimate now, while
    /// it holds him), once, on legs <see cref="SimSettings.LostSearchLegM"/> long; true once that search is over and found
    /// nothing (he gives up).</summary>
    bool Search(in AiContext ctx, SideState s, in Top main)
    {
        if (!s.Searched)
        {
            var pic = ctx.World.Sightings.Of(main.Side);
            long at = s.LastAt;
            if (pic?.IndexOf(s.LastTarget) is int i and >= 0)
            {
                var (lat, lon) = Earth.LatLon(pic.EstimateAt(i, ctx.NowMs, ctx.Settings).Position);
                at = Earth.PackLatLon(lat, lon);
            }
            if (at == 0)
                return true;
            var (la, lo) = Earth.UnpackLatLon(at);
            Give(ctx, main, ctx.Orders.Search(main.Unit, la, lo, ctx.Settings.LostSearchLegM, OrderSource.Commander), CommanderWhy.Lost);
            s.Searched = true;
            return false;
        }
        var (kind, state, _) = Tasks!.Of(main.Unit);
        return kind != TaskKind.Search || state is TaskState.Done or TaskState.Cant;
    }

    /// <summary>Retiring or gone home: every unit of his for home, unless it's going or there (a flotilla sent in to cover
    /// the others breaking off, <see cref="SendIn"/>, after its attack).</summary>
    void Retire(in AiContext ctx, SideState s, in Top t)
    {
        var why = s.Phase == CommanderPhase.Home ? CommanderWhy.GaveUp : s.BrokenOff ? CommanderWhy.BrokenOff : CommanderWhy.Outmatched;
        var (kind, state, _) = Tasks!.Of(t.Unit);
        if (kind == TaskKind.Attack && state is TaskState.OnTask or TaskState.Engaged)
            return;   // its attack first
        if (Tasks.Of(t.Unit) is (TaskKind.Withdraw, TaskState.Cant, TaskReason.Nowhere))
            return;   // no home to go to: it holds what it has
        if (kind == TaskKind.Withdraw && (state is TaskState.OnTask or TaskState.Engaged or TaskState.InContact || Home(t.Unit)))
        {
            if (GivenOf(t.Unit).Why != why && Find(t.Unit.Id) is int r and >= 0 && recs[r].Given == TaskKind.Withdraw)
                recs[r].Why = why;   // still going home, for a new reason
            return;
        }
        Give(ctx, t, ctx.Orders.WithdrawHome(t.Unit, OrderSource.Commander), why);
    }

    /// <summary>The enemy as his navy holds him: his target (for his phases and his search when he's lost) is the biggest
    /// ship of the group he's least confident against, kept while he holds it and no group is graver; 0 with none held.</summary>
    void Enemy(in AiContext ctx, SideState s, byte side, ReadOnlySpan<HeldGroup> held, Vec3d at)
    {
        int best = 0;
        double bestC = double.MaxValue;
        bool kept = false;
        foreach (ref readonly var g in held)
        {
            enemy.Clear();
            AddGroup(ctx, side, g, enemy);
            if (enemy.Count == 0)
                continue;
            double c = mine.Count == 0 ? 0 : Judge.Judge(ctx, 0, side, at, mine, enemy).Confidence;
            if (c < bestC)
                (best, bestC) = (g.Biggest, c);
            kept |= s.Target != 0 && Member(ctx.World.Sightings.Of(side)!, side, g, s.Target);
        }
        if (!kept || best != 0 && best != s.Target && bestC < s.TargetC / ctx.Settings.CommanderRetargetShare)
            (s.Target, s.TargetC) = (best, bestC);
        var pic = ctx.World.Sightings.Of(side);
        if (s.Target != 0 && pic!.IndexOf(s.Target) is int ti and >= 0)
        {
            var (lat, lon) = Earth.LatLon(pic[ti].Position);
            (s.LastAt, s.LastTarget, s.Met, s.Searched) = (Earth.PackLatLon(lat, lon), s.Target, true, false);
        }
    }

    /// <summary>Whether it's night to him (the sun more than <see cref="SimSettings.NightSunElevationDeg"/> down at his main
    /// body) and his doctrine avoids action by night.</summary>
    bool Night(in AiContext ctx, byte side, int main) =>
        main >= 0 && (NightAction)doctrine.OfGroup(null, side, DoctrineField.NightAction) == NightAction.Avoid &&
        Light.SunElevationDeg(store[tops[main].Slot].Position, ctx.World.Time) < ctx.Settings.NightSunElevationDeg;

    /// <summary>By night: his heavy units withdrawn from contact (home, until the day; free to close again then); his
    /// flotillas are sent in by doctrine (<see cref="SendIn"/>). True if it dealt with the unit; the rest (his scouts,
    /// keeping in touch on their tasks) go by the day's rules, as idle units.</summary>
    bool ByNight(in AiContext ctx, in Top t)
    {
        if (t.Role is not (UnitRole.MainBody or UnitRole.Heavy))
            return false;
        var (kind, state, why) = Tasks!.Of(t.Unit);
        if (kind != TaskKind.Withdraw || state is not (TaskState.OnTask or TaskState.Engaged or TaskState.InContact) && !Home(t.Unit) && why != TaskReason.Nowhere)
            Give(ctx, t, ctx.Orders.WithdrawHome(t.Unit, OrderSource.Commander), CommanderWhy.Night);
        return true;
    }

    /// <summary>Whether a unit has gone home: its withdrawal done there.</summary>
    bool Home(Unit u) => Tasks!.Of(u) is (TaskKind.Withdraw, TaskState.Done, TaskReason.Home);

    /// <summary>Whether a unit has an enemy in sight within reach of a fight (<see cref="SimSettings.FightCompareM"/>):
    /// fighting, or about to, as doctrine has it.</summary>
    bool InContact(in AiContext ctx, in Top t) =>
        seen.Near(ctx.World, t.Side, store[t.Slot].Position, ctx.Settings.FightCompareM).Length > 0;

    /// <summary>Whether a heavy unit may be sent to close the enemy: on the orders it was given, through with its task, or
    /// on one of going somewhere (a go to, patrol, search or join: the plan's, before the enemy was found), closing another
    /// target, or going home from a battle he declined or from the night, and now accepts. Its tasks with a unit
    /// (scouting, covering, screening) keep it with that unit.</summary>
    bool FreeToClose(Unit u, int target)
    {
        var (kind, state, _) = Tasks!.Of(u);
        if (kind == TaskKind.None || Tasks.Idle(u))
            return true;
        if (state is not (TaskState.OnTask or TaskState.Engaged or TaskState.InContact))
            return false;   // through with it, its plan to come
        return kind is TaskKind.GoTo or TaskKind.Patrol or TaskKind.Search or TaskKind.Join || kind == TaskKind.Intercept && Tasks.TargetOf(u) != target ||
               kind == TaskKind.Withdraw && GivenOf(u).Why is CommanderWhy.Outmatched or CommanderWhy.Night;   // declined, or the night: now he accepts
    }

    /// <summary>A unit's task with no enemy about, by its role: the main body searches ahead on its course; another heavy
    /// unit joins it (covers it, if it's a ship alone); scouts scout ahead of it, spread off its bow; flotillas and screens
    /// screen the nearest heavy unit; auxiliaries go home. A unit with no main body to support is one.</summary>
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
            UnitRole.Scout => Dispositions.Attend(o, t.Role, t.Unit, m.Unit, Scouts(m.Unit), By),
            UnitRole.Flotilla or UnitRole.Screen => Dispositions.Attend(o, t.Role, t.Unit, NearestHeavy(t, m).Unit, 0, By),
            _ => o.WithdrawHome(t.Unit, By),
        };
    }

    /// <summary>The heavy unit of a side nearest a unit (the main body if none is nearer).</summary>
    Top NearestHeavy(in Top from, in Top main)
    {
        var at = store[from.Slot].Position;
        var best = main;
        double bestD = (store[main.Slot].Position - at).LengthSquared;
        foreach (var t in tops)
            if (t.Side == from.Side && t.Role == UnitRole.Heavy && (store[t.Slot].Position - at).LengthSquared is var d && d < bestD)
                (best, bestD) = (t, d);
        return best;
    }

    /// <summary>How many scouts he's sent to scout for a unit already (the next takes the next bearing,
    /// <see cref="Dispositions.ScoutBearing"/>).</summary>
    int Scouts(Unit of)
    {
        int n = 0;
        for (int i = 0; i < count; i++)
            if (recs[i].Given == TaskKind.Scout && recs[i].Target == of.Id && Tasks!.Of(new Unit(recs[i].Unit)).Kind == TaskKind.Scout)
                n++;
        return n;
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
                h.Add((int)s.Phase);
                h.Add((int)s.Reason);
                h.Add(s.Target);
                h.Add(s.Confidence);
                h.Add(s.TargetC);
                h.Add(s.LastAt);
                h.Add(s.LastTarget);
                h.Add(s.CameT);
                h.Add(s.Met);
                h.Add(s.Declined);
                h.Add(s.DeclinedC);
                h.Add(s.DeclinedFrom.Count);
                h.Add(s.BrokenOff);
                h.Add(s.Searched);
                h.Add(s.GaveUp);
            }
        Attacks.Hash(ref h);
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
            gv.Commanded[k] = gv.Parent[k] != 0 ? (byte)0 : Mark(Unit.Group(gv.Id[k]), gv.Side[k], player);
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
            v.Commanded[k] = groups.Of(v.Entity[k]) != null ? (byte)0 : Mark(Unit.Ship(v.Entity[k]), v.Side[k], player);
    }

    /// <summary>A top unit's mark: his (1), held from him (2), or neither (0).</summary>
    byte Mark(Unit u, int side, int player)
    {
        if (side < 0 || sides[side] is not { Mode: not CommanderMode.None })
            return 0;
        if (Commands(u, side, player))
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
