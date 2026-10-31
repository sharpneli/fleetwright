using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>What a captain is doing (docs/battle/plan.md steps 2.3b and 2.6): plain orders he hands the helm once, or a
/// behaviour he watches.</summary>
public enum CaptainOrder : byte
{
    /// <summary>Told nothing yet: the ship keeps what its helm and engines were last given.</summary>
    None,

    /// <summary>Steer a course (and ring a speed): handed to the helm once.</summary>
    Course,

    /// <summary>Steam a route: handed to the helm, which turns at the waypoints.</summary>
    Route,

    /// <summary>Keep station astern of another unit at a distance, matching its speed.</summary>
    Follow,

    /// <summary>Turn away from another unit and run at flank speed.</summary>
    Run,

    /// <summary>Keep a station on another unit: a distance astern and a distance to starboard (negative: to port) of it,
    /// in its own frame. What an admiral gives his captains to hold a formation.</summary>
    Station,

    /// <summary>Keep another unit in sight at a range, on the bearing he's on (docs/battle/plan.md step 3.2f).</summary>
    Shadow,

    /// <summary>Close to a range band and hold it with the main battery bearing; turn away when outgunned
    /// (docs/battle/fighting.md 4.2c).</summary>
    Fight,

    /// <summary>Out of the fight (docs/battle/command.md 5.1a, falling out): away from the enemy in sight, else for his
    /// side's home, at what she can make; stopped once there.</summary>
    Home,

    /// <summary>A group's: screen another unit (docs/battle/command.md 5.1b), its admiral's; a captain never holds it.</summary>
    Screen,
}

/// <summary>Why a captain does what he does, when it isn't simply his order (docs/battle/command.md "Reports and
/// reasons"): shown on the ship's label, and read by his superiors.</summary>
public enum CaptainReason : byte
{
    None,

    /// <summary>Fallen out of the line: under doctrine's share of her speed.</summary>
    OutSpeed,

    /// <summary>Fallen out: under doctrine's share of her main guns.</summary>
    OutGuns,

    /// <summary>Fallen out: listing past doctrine's limit.</summary>
    OutList,

    /// <summary>Fallen out: past doctrine's share of her reserve buoyancy flooded.</summary>
    OutWater,

    /// <summary>Fallen out: her steering lost or jammed.</summary>
    OutSteering,

    /// <summary>Out of the fight: her main magazines empty and no torpedoes left (docs/battle/command.md 5.1g).</summary>
    OutAmmunition,

    /// <summary>Fallen out and home.</summary>
    Home,

    /// <summary>Combing a torpedo's track her lookouts saw coming at her.</summary>
    Combing,

    /// <summary>Turned away from a light on her, her own lights out (docs/battle/night.md Q7: doctrine's WhenLit).</summary>
    Lit,

    /// <summary>In line ahead in the dark, her next ahead lost from sight: she holds the course and speed she had until she
    /// sees her again (docs/battle/night.md Q7).</summary>
    LostTouch,
}

/// <summary>The captains (docs/battle/plan.md steps 2.3b and 2.6): one per ship that has been given an order, kept in
/// entity order so they think in the same order on every run. A captain is the only one who orders his ship's helm
/// and engines. Plain orders (a course, a speed, a route) he hands the helm at once and then leaves alone, so a world
/// of ships on plain orders costs nothing a second. Behaviours (follow, station, run) he watches each second
/// (<see cref="SimSettings.CaptainPeriodMs"/>), ordering a course and speed when they differ from what's ordered by more
/// than a deadband, so the helm and engines aren't hounded every second.
/// <para><b>Who has a ship:</b> a ship in a group's line takes its admiral's orders. The player's order to it takes it
/// out of the line (nudged) and makes it the player's; its admiral's orders for it are dropped until it's handed back
/// (<see cref="AiCommand.Resume"/>), and he gives them again then. A route the player gave such a ship hands it back at
/// its last waypoint. An explicit <see cref="AiCommand.Detach"/> pins it out until resumed (the rules are
/// <see cref="Chain"/>'s, an admiral's the same). An order by the player's hand (<see cref="Whose.Player"/>) keeps
/// doctrine's standing orders off her (CaptainLayer.Fire.cs).</para>
/// <para><b>Limits</b> come from above as orders (<see cref="AiCommand.SpeedLimit"/>): what he rings is never more,
/// while what was asked is kept. So does the clearance from the coast his ship must keep (its formation's half-width,
/// <see cref="AiCommand.Clearance"/>), which he hands the helm.</para>
/// <para><b>Notices:</b> while the player holds a ship, an order its superior gives it is dropped, and the captain
/// raises a notice ("resume?") once; the admiral raises it too when his group's orders change. It's cleared when the
/// ship is handed back or let go.</para>
/// A captain whose ship is gone is dropped; one whose target is gone holds his course. His guns and Fight are in
/// CaptainLayer.Fire.cs.</summary>
public sealed partial class CaptainLayer(GroupSystem groups, Encounters encounters, SeenContacts seen, Doctrine doctrine, Homes homes) : IAiLayer
{
    struct Captain
    {
        public int Entity;
        public CaptainOrder Order;
        public Whose Whose;             // whose order he's on: his own, his superior's, the player's by hand
        public int Target;              // a unit (Unit.Id)
        public double DistanceM, AbeamM;
        public double AskedKn;          // the speed ordered (NaN: none, the ship keeps what's rung)
        public double LimitKn;          // the most he may ring, from above (0: none)
        public double ClearanceM;       // how far off the coast his ship must keep, from above (0: none)
        public bool Watch;              // held by the player on a route: its end hands the ship back
        public bool Asked;              // held by the player while his superior's orders for him changed: a notice
        public Trail? Track;            // following in line ahead: the track of the ship ahead (made once, reused)
        public FireOrder Fire;          // at will (the default), hold, or engage FireTarget
        public int FireTarget, FireFrom;   // the ship engaged; the group whose admiral gave the fire order (0: his own)
        public int FightSide, FightEnemy;  // fighting: the side he keeps the enemy on (0: not chosen yet), and whom
        public bool FightAway;          // fighting: outmatched, turned away
        public double FightBandM;       // fighting: the range he keeps (his unit's matchup's best, or ordered), m
        public bool AttackOrdered;      // fighting by an Attack order: a torpedo craft presses it home whatever the matchup
        public bool Met;                // doctrine's Engage taken up in this encounter
        public long TorpedoMs;          // when he last fired torpedoes (0: never)
        public bool TorpedoHeld;        // his flotilla attacking together: his torpedoes wait for its signal
        public bool TorpedoWait;        // on the coarse fire watch: a torpedo target found, his tubes not bearing yet
        public bool Attacking;          // fighting: pressing a torpedo attack (his searchlights dark, docs/battle/night.md Q4)
        public bool Finishing;          // fighting: in to finish an enemy that can hardly hurt him (docs/battle/balance.md BL5)
        public bool SmokeMade;          // going home under fire, making funnel smoke to cover it (docs/battle/balance.md BL2b)
        public CaptainReason Reason;    // why, when it isn't simply his order (fallen out)
        public bool Overruled;          // the player ordered him on after he fell out: he stays, at his ship's risk
        public bool Conn;               // the player has the conn: nothing but the player's helm orders moves her
        public sbyte Tracks;            // the conn's lookouts: a torpedo's track seen coming at her, its side (1 starboard, -1 port; 0 none)
        public int FitVersion;          // his ship's condition's version when he last judged its fitness
        public long HeldUntilMs;        // a course held until then (0: none): combing a torpedo's track, or away from a light (HeldFor)
        public double HeldDeg, HeldBackDeg;   // the course held, and the course ordered before it
        public CaptainReason HeldFor;   // why it's held: Combing or Lit
        public double ZigBaseDeg;       // coarse, zigzagging: the course his legs go either side of, as his last think had it (J4)
        public long ZigNextMs, ZigThinkMs;   // when his leg ends (0: not zigzagging coarse), and the think that set it
        public ShellJudged[]? Shells;   // by battery, what its shell was last judged on (made when first needed)
        public byte ShellHeld, ShellSpent;   // the player's kind for his main guns (+ 1, 0 none), and the one that ran out
        public bool BeamsHeld;          // the player's: his beams kept on in the dark (docs/battle/night.md Q9)
        public bool StarHeld;           // the player's: his star shell task (a contact or a point) is the player's
        public long FiredOnMs;          // when she or her squadron was last fired upon, darkened (0: never; docs/battle/night.md Q9)
        public bool Holding;            // darkened in the dark, not fired upon lately: his guns hold (as of his last think)
        public long EndLookMs, EndWindowMs;   // his last look at her (0: none yet; docs/battle/striking.md SK2), and when his window on her water and list began
        public double EndWaterT, EndHeelDeg;  // her water and list at its start
        public double WaterRate, HeelRate, WaterTrend, HeelTrend;   // their rise over the last window, and the lesser of the last two (t/s, °/s from upright)
        public long HopelessMs, CallMs; // since when she's been hopeless and cornered (0: she isn't), and when his call falls due
        public bool FightsOn;           // his call made: she fights on (abandoned still once she won't float)
        public long FleeMs;             // fleeing an enemy warship (docs/battle/aims.md A4): when one was last near (0: not fleeing)
        public double FleeBackDeg, FleeBackKn;   // and the course and speed she had before, for after
    }

    /// <summary>What a battery's shell was last judged on (docs/battle/shell-choice.md A3): its target as the picture had
    /// her, and the range's step; judged again when any of them changes, and its rounds looked at again from CheckMs.</summary>
    struct ShellJudged
    {
        public int Target, TakenFor, Step;
        public long CheckMs;
        public ContactLevel Level;
        public ShipType Type;
    }

    Captain[] captains = new Captain[16];
    int count, active, lightsHeld;   // lightsHeld: captains with the player's beams or star shell (docs/battle/night.md Q9)

    public int Count => count;

    /// <summary>The units' tasks: a withdrawing ship doesn't take up doctrine's fight (set by the AI system).</summary>
    internal Tasks.TaskLayer? Tasks { get; set; }

    /// <summary>The admirals: whether a ship's line is one the player has the conn of (set by the AI system).</summary>
    internal Admirals.AdmiralLayer? Admirals { get; set; }

    /// <summary>Whether the player has the conn of his ship (hers, or her line's): nothing but the player's helm orders
    /// moves her (docs/battle/command.md "The conn").</summary>
    bool Conned(in Captain cap, int entity) => cap.Conn || Admirals?.LineConned(entity) == true;

    /// <summary>How many points a captain's track of the ship ahead holds (<see cref="SimSettings.FollowTrackPoints"/>).</summary>
    internal int TrailPoints { get; set; } = 512;

    /// <summary>Who can fight whom (docs/battle/command.md 5.1g): Fight's judgement.</summary>
    internal Knowledge.MatchupJudge Judge { get; set; } = null!;

    /// <summary>Every side's aims (docs/battle/aims.md A3): what his side wants sunk first.</summary>
    internal Commanders.AimBook? Book { get; set; }

    /// <summary>How many notices captains have raised (a ship's notice going up): a UI shows the new ones.</summary>
    public int Notices { get; private set; }

    /// <summary>Whether a ship's captain has a notice up: held by the player, his superior's orders for him changed.</summary>
    public bool IsAsked(int entity)
    {
        int i = Find(entity);
        return i >= 0 && captains[i].Asked;
    }

    /// <summary>Raises a ship's notice (once while it's up): his superior's orders changed while the player holds him.</summary>
    public void Ask(int entity)
    {
        ref var cap = ref Get(entity);
        if (!cap.Asked)
        {
            cap.Asked = true;
            Notices++;
        }
    }

    /// <summary>The clearance from the coast a ship's captain was given (0: none).</summary>
    public double ClearanceOf(int entity)
    {
        int i = Find(entity);
        return i >= 0 ? captains[i].ClearanceM : 0;
    }
    public bool Active => active > 0 || Fighting || Hurt() || lightsHeld > 0;

    /// <summary>The world's ships, for her men's reliefs between fights (docs/battle/crew.md N3a: a captain thinks while
    /// his ship is hurt, whatever his order).</summary>
    public ShipStore? Store { get; init; }

    public int PeriodMs(in AiContext ctx)
    {
        int period = ctx.Settings.AiRailsPeriodMs;
        if (fightingFine)
            return ctx.Settings.CaptainPeriodMs;
        if (Fighting)
            for (int k = 0; k < ctx.Ships.OffRailsCount; k++)
            {
                int slot = ctx.Ships.OffRailsSlot(k);
                if (encounters.Of(slot, ctx.Ships[slot].Entity) != 0)
                {
                    if (ctx.Ships.TierOf(slot) == ClockTier.Fine)
                        return ctx.Settings.CaptainPeriodMs;
                    period = Math.Min(period, ctx.Settings.CoarseStepMs);
                }
            }
        for (int i = 0; i < count; i++)
            if (IsActive(captains[i]) && ctx.Ships.SlotOf(captains[i].Entity) is int slot and >= 0)
            {
                var tier = ctx.Ships.TierOf(slot);
                if (tier == ClockTier.Fine)
                    return ctx.Settings.CaptainPeriodMs;
                if (tier == ClockTier.Coarse)
                    period = Math.Min(period, ctx.Settings.CoarseStepMs);
            }
        return period;
    }

    // a behaviour he works at each think (not an order handed the helm once), or a route the player gave to watch
    static bool IsActive(in Captain c) => c.Order is not (CaptainOrder.None or CaptainOrder.Course or CaptainOrder.Route) || c.Watch || c.FleeMs != 0;

    /// <summary>A ship's captain's order, its target unit and whose it is; None if he has none.</summary>
    /// <summary>Whether a ship's captain is fleeing an enemy warship (docs/battle/aims.md A4): her task waits, as for a fight.</summary>
    public bool Fleeing(int entity) => Find(entity) is int i and >= 0 && captains[i].FleeMs != 0;

    public (CaptainOrder Order, int Target, Whose Whose) Of(int entity)
    {
        int i = Find(entity);
        return i >= 0 ? (captains[i].Order, captains[i].Target, captains[i].Whose) : (CaptainOrder.None, 0, Whose.Own);
    }

    /// <summary>A fighting captain's way of it (docs/battle/balance.md BL5, tools): pressing a torpedo attack, in to finish
    /// an enemy, or turned away from one.</summary>
    public (bool Attacking, bool Finishing, bool Away) FightOf(int entity) =>
        Find(entity) is int i and >= 0 ? (captains[i].Attacking, captains[i].Finishing, captains[i].FightAway) : default;

    /// <summary>The range a fighting captain keeps, m (his unit's matchup's best, or ordered; 0 before he's fought).</summary>
    public double BandOf(int entity) => Find(entity) is int i and >= 0 ? captains[i].FightBandM : 0;

    /// <summary>Whether a ship's captain is combing a torpedo's track his lookouts saw (his admiral reads it: tracks seen).</summary>
    public bool Combing(int entity, long nowMs) => Find(entity) is int i and >= 0 && captains[i].HeldUntilMs > nowMs && captains[i].HeldFor == CaptainReason.Combing;

    /// <summary>Why a ship's captain does what he does (fallen out, home), or None.</summary>
    public CaptainReason ReasonOf(int entity) => Find(entity) is int i and >= 0 ? captains[i].Reason : CaptainReason.None;

    /// <summary>The speed asked of a ship (NaN if none) and the most it may ring (0: no limit).</summary>
    public (double AskedKn, double LimitKn) SpeedOf(int entity)
    {
        int i = Find(entity);
        return i >= 0 ? (captains[i].AskedKn, captains[i].LimitKn) : (double.NaN, 0);
    }

    /// <summary>Whether a ship's captain already holds his superior's order to keep this station: for the admiral, so
    /// he doesn't give it again.</summary>
    public bool Holds(int entity, CaptainOrder order, int target, double astern, double abeam)
    {
        int i = Find(entity);
        if (i < 0)
            return false;
        ref readonly var c = ref captains[i];
        return c.Whose == Whose.Superior && c.Order == order && c.Target == target && c.DistanceM == astern && c.AbeamM == abeam;
    }

    /// <summary>The entity of the i-th captain (in entity order) if he holds an order from his superior, else 0.</summary>
    public int SuperiorsAt(int i) => captains[i].Whose == Whose.Superior ? captains[i].Entity : 0;

    /// <summary>An order to a ship's captain, as a command (<see cref="UnitOrders"/>).</summary>
    public void Order(in AiContext ctx, in Command c)
    {
        int e = c.Entity, slot = ctx.Ships.SlotOf(e);
        if (slot < 0)
            return;   // dropped or never made: an order that reached no ship
        if (ctx.Ships.Combat.Condition(slot).IsGivenUp)
            return;   // her colours down or her men off her: she takes no orders (docs/battle/striking.md)
        var code = (AiCommand)c.Code;
        var from = OrderFrom.Of(c);
        switch (code)
        {
            case AiCommand.FireAtWill or AiCommand.HoldFire or AiCommand.Engage:
                FireOrdered(ctx, c, from);   // the guns: not the helm, so he keeps his place in the line
                return;
            case AiCommand.Form or AiCommand.Shape or AiCommand.Deploy:
                return;   // a group's
            case AiCommand.Conn when c.X == 0:
                if (Find(e) is int ci and >= 0)
                    (captains[ci].Conn, captains[ci].Tracks) = (false, 0);   // the conn given back: his order stands, his judgement too
                return;
        }
        var g = groups.Of(e);
        var detached = g?.DetachmentOf(e) ?? Detachment.InLine;

        switch (code)
        {
            case AiCommand.Resume:
                if (g != null && detached != Detachment.InLine)
                    ctx.Issue(GroupSystem.Detach(e, Detachment.InLine));   // his admiral gives him his station
                if (Find(e) is int ri and >= 0)
                {
                    ref var back = ref captains[ri];
                    (back.Asked, back.Conn, back.Tracks) = (false, false, 0);
                    if (back.Whose == Whose.Player)
                        back.Whose = Whose.Own;   // let go: his superior's station comes next think
                }
                return;
            case AiCommand.Detach:
                if (g == null || Chain.Pin(from, detached) is not { } pin)
                    return;
                ctx.Issue(GroupSystem.Detach(e, pin));
                Release(ctx, e);   // he holds what he was doing, the player's (or his task's) now
                if (from.Hand)
                    Get(e).Whose = Whose.Player;
                return;
            case AiCommand.SpeedLimit:
                ref var lim = ref Get(e);
                SetLimit(ctx, ref lim, ctx.Ships[slot], c.X);
                return;
            case AiCommand.Clearance:
                ref var clr = ref Get(e);
                SetClearance(ctx, ref clr, e, c.X);
                return;
        }

        if (g != null)
        {
            // his superior's order waits while he's out of the line (the player has him: a notice; his task: none, it
            // waits for its end); the player's takes him out of the line or from his task, a task's out of the line
            if (Chain.Place(from, detached, out bool ask) is not { } place)
            {
                if (ask)
                    Ask(e);
                return;
            }
            if (place != detached)
                ctx.Issue(GroupSystem.Detach(e, detached = place));
        }

        ref var cap = ref Get(e);
        var own = ctx.Ships[slot];
        var whose = from.Whose;
        if (whose != Whose.Superior && cap.Whose == Whose.Superior)
        {
            SetLimit(ctx, ref cap, own, 0);   // his superior's limit and clearance go with his superior
            SetClearance(ctx, ref cap, e, 0);
        }
        if (whose == Whose.Superior)
            cap.Asked = false;   // in his superior's hands again
        cap.Whose = whose;
        // the conn (docs/battle/command.md "The conn"): taken by the player's hand, kept through the player's helm orders,
        // ended by any other order; taken while he combs a track, the comb's off and the helm as it is the player's
        cap.Conn = whose == Whose.Player && (code == AiCommand.Conn || cap.Conn && Chain.IsHelmOrder(code));
        if (!cap.Conn)
            cap.Tracks = 0;
        else
            cap.HeldUntilMs = 0;
        // the player's order to a ship fallen out keeps her in (docs/map-ui/battle-hud.md decision 2: the player may
        // overrule her captain; attention is what's rewarded): she won't fall out again this battle
        if (whose == Whose.Player && cap.Reason is CaptainReason.OutSpeed or CaptainReason.OutGuns or CaptainReason.OutList
            or CaptainReason.OutWater or CaptainReason.OutSteering)
            cap.Overruled = true;
        cap.Reason = CaptainReason.None;   // ordered: doing what he's told
        cap.FleeMs = 0;                    // and not fleeing (he's told again, if it's still for him to judge)
        switch (code)
        {
            case AiCommand.Course:
                (cap.Order, cap.Target) = (CaptainOrder.Course, 0);
                if (cap.HeldUntilMs > ctx.NowMs)
                    cap.HeldBackDeg = c.X;   // a course held (a track combed, a light turned from): steered once it's done
                else
                    ctx.Helm.Course(e, c.X, c.Y);
                break;
            case AiCommand.TurnTogether:
                (cap.Order, cap.Target) = (CaptainOrder.Course, 0);
                ctx.Helm.Course(e, ((own.HeadingDeg + c.X) % 360 + 360) % 360);
                break;
            case AiCommand.Speed:
                if (cap.Order is not (CaptainOrder.Course or CaptainOrder.Route))
                    (cap.Order, cap.Target) = (CaptainOrder.Course, 0);   // out of a behaviour: on the course ordered now
                cap.AskedKn = c.X;
                Ring(ctx, cap, own, always: true);   // an order reaches the helm, as a course does, even one it has
                break;
            case AiCommand.Hold or AiCommand.Conn:   // the conn taken: on the course and speed ordered now
                (cap.Order, cap.Target, cap.AskedKn) = (own.Helm == Helm.Route ? CaptainOrder.Route : CaptainOrder.Course, 0, own.SpeedOrderKn);
                if (own.Helm == Helm.Rudder)
                    ctx.Helm.Course(e, own.CourseOrderDeg);
                break;
            case AiCommand.ClearRoute:
                (cap.Order, cap.Target) = (CaptainOrder.Course, 0);
                ctx.Helm.ClearRoute(e);
                break;
            case AiCommand.AddWaypoint:
                (cap.Order, cap.Target) = (CaptainOrder.Route, 0);
                ctx.Helm.AddWaypoint(e, c.A);
                break;
            case AiCommand.LoopRoute:
                ctx.Helm.LoopRoute(e, c.X != 0);
                break;
            case AiCommand.Follow:
                (cap.Order, cap.Target, cap.DistanceM, cap.AbeamM) = (CaptainOrder.Follow, (int)c.A, c.X, 0);
                break;
            case AiCommand.Station:
                (cap.Order, cap.Target, cap.DistanceM, cap.AbeamM) = (CaptainOrder.Station, (int)c.A, c.X, c.Y);
                break;
            case AiCommand.Run:
                (cap.Order, cap.Target) = (CaptainOrder.Run, (int)c.A);
                break;
            case AiCommand.Shadow:
                (cap.Order, cap.Target, cap.DistanceM, cap.AbeamM) = (CaptainOrder.Shadow, (int)c.A, c.X, 0);
                break;
            case AiCommand.Screen:
                // a ship alone: a station that far ahead of the unit (a group's admiral screens it properly)
                (cap.Order, cap.Target, cap.DistanceM, cap.AbeamM) = (CaptainOrder.Station, (int)c.A, -(c.X > 0 ? c.X : ctx.Settings.ScreenM), 0);
                break;
            case AiCommand.Fight or AiCommand.Attack:
                (cap.Order, cap.Target, cap.DistanceM, cap.AbeamM) = (CaptainOrder.Fight, (int)c.A, code == AiCommand.Fight ? c.X : 0, 0);
                (cap.FightSide, cap.FightEnemy, cap.FightAway, cap.AttackOrdered) = (0, 0, false, code == AiCommand.Attack);
                break;
            default:
                throw new ArgumentException($"unknown AI command {c.Code}");
        }
        cap.Watch = cap.Order == CaptainOrder.Route && whose != Whose.Superior && detached == Detachment.Nudged;
        Recount();
    }

    /// <summary>A ship no longer his superior's (it left its group, or was detached): he holds the course and speed
    /// ordered now, and his superior's limit goes. Not an order: the captain finds he has nobody above him.</summary>
    public void Release(in AiContext ctx, int entity)
    {
        int i = Find(entity), slot = ctx.Ships.SlotOf(entity);
        if (i < 0 || slot < 0)
            return;
        ref var cap = ref captains[i];
        var own = ctx.Ships[slot];
        if (cap.Order is not (CaptainOrder.Course or CaptainOrder.Route))
            (cap.Order, cap.Target, cap.AskedKn) = (own.Helm == Helm.Route ? CaptainOrder.Route : CaptainOrder.Course, 0, own.SpeedOrderKn);
        SetLimit(ctx, ref cap, own, 0);
        SetClearance(ctx, ref cap, entity, 0);
        (cap.Whose, cap.Asked) = (Whose.Own, false);
        Recount();
    }

    /// <summary>A ship dropped from the world: its captain goes with it.</summary>
    public void Forget(int entity)
    {
        int i = Find(entity);
        if (i >= 0)
        {
            Remove(i);
            Recount();
        }
    }

    public void Think(in AiContext ctx)
    {
        thinkMs = ctx.NowMs;
        MarkUnderFire(ctx);
        for (int i = 0; i < count; i++)
        {
            ref var cap = ref captains[i];
            int slot = ctx.Ships.SlotOf(cap.Entity);
            if (slot < 0)
            {
                Remove(i--);   // the ship is gone
                continue;
            }
            if (!IsActive(cap) || ctx.Ships.Combat.Condition(slot) is not { Status: ShipStatus.Afloat, IsGivenUp: false })
                continue;   // nothing to do, his ship going down, or given up (docs/battle/striking.md)
            var own = ctx.Ships[slot];
            if (cap.SmokeMade && cap.Order != CaptainOrder.Home)
                CoverRetreat(ctx, ref cap, own, slot, near: false);   // another order: the smoke he made for his retreat stops
            if (cap.Watch)
            {
                // a route the player gave a ship out of its line: at its end, back to its admiral
                if (own.RouteLoop)
                    cap.Watch = false;   // a patrol has no end
                else if (own.Helm != Helm.Route && cap.HeldUntilMs == 0)   // off it while holding a course (a comb, a light) isn't its end
                {
                    (cap.Watch, cap.Order) = (false, CaptainOrder.Course);
                    if (groups.Of(cap.Entity)?.DetachmentOf(cap.Entity) == Detachment.Nudged)
                        ctx.Issue(GroupSystem.Detach(cap.Entity, Detachment.InLine));
                }
                continue;
            }
            if (ctx.Ships.RidesAnother(slot))
                continue;   // carried on its guide's rails, held exactly on station: an order would only derail the line
            if (cap.HeldUntilMs > ctx.NowMs)
                continue;   // combing a torpedo's track, or turned from a light: that first
            if (cap.FleeMs != 0)
            {
                FleeThink(ctx, ref cap, own);
                continue;
            }
            if (cap.Order == CaptainOrder.Fight)
            {
                FightThink(ctx, ref cap, own, slot);
                continue;
            }
            if (cap.Order == CaptainOrder.Home)
            {
                HomeThink(ctx, ref cap, own, slot);
                continue;
            }
            int target = new Unit(cap.Target).ShipOf(groups);
            if (target == 0 || target == cap.Entity || !ctx.Picture.TryGet(own.Side, target, out var t))
            {
                (cap.Order, cap.Target, cap.AskedKn) = (CaptainOrder.Course, 0, own.SpeedOrderKn);   // lost: he holds his course
                continue;
            }
            double course, kn;
            if ((cap.Order == CaptainOrder.Follow || cap.Order == CaptainOrder.Station && cap.AbeamM == 0 && cap.DistanceM > 0) &&
                ctx.Ships.SlotOf(target) is int ahead and >= 0)
            {
                // in line ahead in the dark: she keeps station only on a next ahead she makes out (docs/battle/night.md Q7)
                bool sees = SeesAhead(ctx, own, ctx.Ships[ahead]);
                if (!sees)
                {
                    if (cap.Reason == CaptainReason.None)
                        cap.Reason = CaptainReason.LostTouch;
                    continue;   // the course and speed she had
                }
                if (cap.Reason == CaptainReason.LostTouch)
                    cap.Reason = CaptainReason.None;
            }
            if (own.Tier == ClockTier.Coarse && cap.Order is CaptainOrder.Follow or CaptainOrder.Station or CaptainOrder.Shadow)
            {
                // coarse, deciding once a step: steer for where the station will be a step on (a trail recorded once
                // a minute is no track to steer by; the line ahead's turns are its guide's rails, ShipSystem.Tiers.cs)
                if (own.YawRate != 0)
                    continue;   // mid-turn (turning together with its guide, ShipSystem.Tiers.cs, or his own last order): done within a step or two, steered on from where it comes out
                var s = ctx.Settings;
                var (astern, abeam) = cap.Order switch
                {
                    CaptainOrder.Shadow => Behaviours.ShadowStation(own, t, cap.DistanceM > 0 ? cap.DistanceM : ShadowRange(ctx, own)),
                    CaptainOrder.Follow => (cap.DistanceM > 0 ? cap.DistanceM : s.FollowDistanceLengths * own.Handling.LengthM, 0.0),
                    _ => (cap.DistanceM, cap.AbeamM),
                };
                (course, kn) = Behaviours.Rendezvous(own, t, astern, abeam, s.CoarseRendezvousSteps * s.CoarseStepMs / 1000.0, s);
            }
            else if (cap.Order == CaptainOrder.Follow || cap.Order == CaptainOrder.Station && cap.AbeamM == 0 && cap.DistanceM > 0)
            {
                // in line ahead (a station dead astern; one dead ahead, a scout's, is a plain station): on the track the
                // ship ahead steamed, so he turns where it turned (laid straight astern
                // of it when he starts following it, or comes off rails, where the line steamed straight)
                var s = ctx.Settings;
                double distance = cap.DistanceM > 0 ? cap.DistanceM : s.FollowDistanceLengths * own.Handling.LengthM;
                var track = cap.Track ??= new Trail(s.FollowTrackPoints);
                if (track.Of != target || track.Count == 0 || ctx.NowMs - track.LastMs > 3 * s.CaptainPeriodMs)
                    track.Lay(target, t.Position, t.Forward, 1.5 * distance + 2000, s.FollowTrackStepM, ctx.NowMs);
                track.Record(t.Position, s.FollowTrackStepM, ctx.NowMs);
                if (!Behaviours.Track(own, t, track, distance, s, out course, out kn))
                    (course, kn) = Behaviours.Station(own, t, distance, 0, s);   // off the track: onto its line first
            }
            else if (cap.Order == CaptainOrder.Shadow)
                (course, kn) = Behaviours.Shadow(own, t, cap.DistanceM > 0 ? cap.DistanceM : ShadowRange(ctx, own), ctx.Settings);
            else
                (course, kn) = cap.Order == CaptainOrder.Station ? Behaviours.Station(own, t, cap.DistanceM, cap.AbeamM, ctx.Settings) : Behaviours.Run(own, t);
            if (cap.LimitKn > 0)
                kn = Math.Min(kn, cap.LimitKn);
            Give(ctx, own, course, kn);
        }
        HurtPass(ctx);   // first: it starts the hurt flag afresh, the fire pass's hurt ships add to it
        if (Fighting)
            FirePass(ctx);
        EndPass(ctx);   // the captains' call on their ships' ends (docs/battle/striking.md SK2)
        HeldLightsPass(ctx);
        Recount();
    }

    /// <summary>Whether a ship in line ahead makes out her next ahead (docs/battle/night.md Q7: a station-keeping look, one
    /// a ship at her next ahead only, never a sighting of friends): always where the dark adds nothing to the eye; in the dark,
    /// her bridge's lookout (else her first post) on the other's hull as it shows on its bearing
    /// (<see cref="SightModel.Margin"/>: end on from astern) or her wake's white water, taken as her bow wave seen bow on
    /// (<see cref="SightModel.BowWaveMargin"/>: across her beam, at the foam's contrast [EST]), at the distance between them.</summary>
    static bool SeesAhead(in AiContext ctx, ShipState own, ShipState ahead)
    {
        var s = ctx.Settings;
        var weather = ctx.World.Weather.At(own.Position, ctx.World.Time);
        var sky = NightLight.At(own.Position, ctx.World.Time, weather.Cloud, s);
        double pupil = SightModel.PupilMm(sky.SkyCdM2);
        if (!SightModel.DarkOf(sky.SkyCdM2 * SightModel.LookoutGlass(pupil, s), s).Any)
            return true;
        var eyes = own.Handling.Class.Eyes;
        if (eyes.Length == 0)
            return true;
        int e = 0;
        for (int k = 0; k < eyes.Length; k++)
            if (eyes[k].Kind == EyeKind.Bridge)
            {
                e = k;
                break;
            }
        double d = Earth.Distance(own.Position, ahead.Position), brg = Earth.Bearing(own.Position, ahead.Position);
        var line = new SightLine(d, Earth.Bearing(ahead.Position, own.Position) - ahead.HeadingDeg, weather.VisibilityM, sky.Toward(brg), 0, default,
            Math.Abs(ahead.Speed), SightModel.GlassLight(eyes[e], pupil, s), 0, 0);
        double mag = SightModel.Magnification(eyes[e], s), h = eyes[e].HeightM;
        var cls = ahead.Handling.Class;
        return SightModel.Margin(h, mag, cls, line, s) >= 1 || SightModel.BowWaveMargin(h, mag, cls, line with { AspectDeg = 0 }, s) >= 1;
    }

    /// <summary>The range a shadower keeps when he's given none: a share of how far he can see now (the weather's
    /// visibility, or the dark's), kept between the settings' least and most; never the least where he can't see that
    /// far (a dark night: he'd lose her, docs/battle/night.md Q12f).</summary>
    static double ShadowRange(in AiContext ctx, ShipState own)
    {
        var s = ctx.Settings;
        var weather = ctx.World.Weather.At(own.Position, ctx.World.Time);
        var ahead = new WorldTime(ctx.NowMs + (long)(s.ShadowAheadS * 1000));   // the light to come: closing as the dark falls
        double reach = Math.Min(SightModel.SeenToM(weather.VisibilityM, NightLight.At(own.Position, ctx.World.Time, weather.Cloud, s).SkyCdM2, s),
            SightModel.SeenToM(weather.VisibilityM, NightLight.At(own.Position, ahead, weather.Cloud, s).SkyCdM2, s));
        double keep = s.ShadowVisibilityShare * reach;
        return Math.Clamp(keep, Math.Min(s.ShadowMinM, keep), s.ShadowMaxM);
    }

    /// <summary>Orders the course and speed if they differ from what the ship has been ordered.</summary>
    static void Give(in AiContext ctx, ShipState own, double course, double kn)
    {
        double dc = course - own.CourseOrderDeg;
        dc -= 360 * Math.Round(dc / 360);
        if (own.Helm != Helm.Course || Math.Abs(dc) > ctx.Settings.CaptainCourseDeadbandDeg)
            ctx.Helm.Course(own.Entity, course);
        if (Math.Abs(kn - own.SpeedOrderKn) > ctx.Settings.CaptainSpeedDeadbandKn)
            ctx.Helm.Speed(own.Entity, kn);
    }

    /// <summary>Rings the speed asked within the limit (plain orders only: a behaviour works out its own speed): always
    /// for an order, and for a changed limit only if that changes what's rung.</summary>
    static void Ring(in AiContext ctx, in Captain cap, ShipState own, bool always = false)
    {
        if (double.IsNaN(cap.AskedKn) || cap.Order is not (CaptainOrder.Course or CaptainOrder.Route or CaptainOrder.None))
            return;
        double kn = cap.LimitKn > 0 ? Math.Min(cap.AskedKn, cap.LimitKn) : cap.AskedKn;
        if (always || kn != own.SpeedOrderKn)
            ctx.Helm.Speed(own.Entity, kn);
    }

    static void SetLimit(in AiContext ctx, ref Captain cap, ShipState own, double kn)
    {
        if (cap.LimitKn == kn)
            return;
        if (double.IsNaN(cap.AskedKn))
            cap.AskedKn = own.SpeedOrderKn;   // what's rung now is what he keeps asking for
        cap.LimitKn = kn;
        Ring(ctx, cap, own);
    }

    static void SetClearance(in AiContext ctx, ref Captain cap, int entity, double metres)
    {
        if (cap.ClearanceM == metres)
            return;
        cap.ClearanceM = metres;
        ctx.Issue(ShipSystem.Clearance(entity, metres));
    }

    void Recount()
    {
        (active, lightsHeld) = (0, 0);
        for (int i = 0; i < count; i++)
        {
            if (IsActive(captains[i]))
                active++;
            if (captains[i].BeamsHeld || captains[i].StarHeld)
                lightsHeld++;
        }
    }

    public void Hash(ref StateHash h)
    {
        h.Add(count);
        h.Add(Notices);
        h.Add(fighting);
        for (int i = 0; i < count; i++)
        {
            ref readonly var c = ref captains[i];
            h.Add(c.Entity);
            h.Add((int)c.Order);
            h.Add((int)c.Whose);
            h.Add(c.Target);
            h.Add(c.DistanceM);
            h.Add(c.AbeamM);
            h.Add(c.AskedKn);
            h.Add(c.LimitKn);
            h.Add(c.ClearanceM);
            h.Add(c.Watch);
            h.Add(c.Asked);
            h.Add((int)c.Fire);
            h.Add(c.FireTarget);
            h.Add(c.FireFrom);
            h.Add(c.FightSide);
            h.Add(c.FightEnemy);
            h.Add(c.FightAway);
            h.Add(c.AttackOrdered);
            h.Add(c.Met);
            h.Add(c.TorpedoMs);
            if (c.TorpedoHeld)
                h.Add(true);   // only then: worlds without flotilla attacks hash as before
            if (c.Attacking)
                h.Add(2);      // only then: worlds without a torpedo attack hash as before
            h.Add((int)c.Reason);
            if (c.Overruled)
                h.Add(true);   // only then: worlds without an overrule hash as before
            h.Add(c.FitVersion);
            h.Add(c.HeldUntilMs);
            h.Add(c.HeldDeg);
            h.Add(c.HeldBackDeg);
            if (c.HeldFor == CaptainReason.Lit)
                h.Add(0x117u);   // (only when lit: the hashes of worlds with none as they were)
            if (c.ZigNextMs != 0)
            {
                h.Add(c.ZigBaseDeg);   // only then: worlds without a coarse zigzag hash as before
                h.Add(c.ZigNextMs);
                h.Add(c.ZigThinkMs);
            }
            h.Add(c.Conn);
            h.Add((int)c.Tracks);
            if (c.ShellHeld + c.ShellSpent > 0)
            {
                h.Add(c.ShellHeld);   // only then: worlds without the player's shell orders hash as before
                h.Add(c.ShellSpent);
            }
            if (c.BeamsHeld || c.StarHeld || c.FiredOnMs != 0 || c.Holding)
            {
                h.Add(0x9u);   // only then: worlds without the night's orders hash as before
                h.Add(c.BeamsHeld);
                h.Add(c.StarHeld);
                h.Add(c.FiredOnMs);
                h.Add(c.Holding);
            }
            if (c.FleeMs != 0)
            {
                h.Add(0xF1EEu);   // only then: worlds where nobody flees hash as before
                h.Add(c.FleeMs);
                h.Add(c.FleeBackDeg);
                h.Add(c.FleeBackKn);
            }
            if (c.EndLookMs != 0 || c.FightsOn)
            {
                h.Add(0x5Cu);   // only then: worlds where no captain has judged his ship's end hash as before
                h.Add(c.EndLookMs);
                h.Add(c.EndWindowMs);
                h.Add(c.EndWaterT);
                h.Add(c.EndHeelDeg);
                h.Add(c.WaterRate);
                h.Add(c.HeelRate);
                h.Add(c.WaterTrend);
                h.Add(c.HeelTrend);
                h.Add(c.HopelessMs);
                h.Add(c.CallMs);
                h.Add(c.FightsOn);
            }
            c.Track?.Hash(ref h);
        }
    }

    public void Fill(Snapshot s)
    {
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
        {
            int i = Find(v.Entity[k]);
            (v.Captain[k], v.CaptainTarget[k]) = i >= 0 ? ((byte)captains[i].Order, new Unit(captains[i].Target).ShipOf(groups)) : ((byte)0, 0);
            (v.StationAsternM[k], v.StationAbeamM[k]) = i < 0 ? (double.NaN, double.NaN) : captains[i].Order switch
            {
                CaptainOrder.Station => (captains[i].DistanceM, captains[i].AbeamM),
                CaptainOrder.Follow when captains[i].DistanceM > 0 => (captains[i].DistanceM, 0),
                _ => (double.NaN, double.NaN),
            };
            (v.SpeedAskedKn[k], v.SpeedLimitKn[k]) = i < 0 ? (double.NaN, 0) : captains[i].Order is CaptainOrder.Course or CaptainOrder.Route or CaptainOrder.None
                ? (captains[i].AskedKn, captains[i].LimitKn) : (double.NaN, captains[i].LimitKn);   // a behaviour's speed is its own
            v.Asked[k] = i >= 0 && captains[i].Asked;
            v.Conn[k] = i >= 0 && captains[i].Conn ? (byte)1 : Admirals?.LineConned(v.Entity[k]) == true ? (byte)2 : (byte)0;
            v.Tracks[k] = i >= 0 ? captains[i].Tracks : (sbyte)0;
            v.Reason[k] = i >= 0 ? (byte)captains[i].Reason : (byte)0;
            (v.Fire[k], v.FireTarget[k]) = i >= 0 ? ((byte)captains[i].Fire, captains[i].FireTarget) : ((byte)0, 0);
            (v.ShellHeld[k], v.ShellSpent[k]) = i >= 0 ? (captains[i].ShellHeld, captains[i].ShellSpent) : ((byte)0, (byte)0);
            var night = doctrine.Of(v.Entity[k], v.Side[k], DoctrineField.Darkened) != 0 ? NightFlags.Darkened : NightFlags.None;
            if (i >= 0)
            {
                ref readonly var c = ref captains[i];
                night |= (c.BeamsHeld ? NightFlags.Beams : 0) | (c.StarHeld ? NightFlags.StarHeld : 0) |
                         (c.Holding && v.Encounter[k] != 0 ? NightFlags.Holding : 0);
            }
            v.Night[k] = (byte)night;
        }
    }

    /// <summary>The player's beams for a ship (docs/battle/night.md Q9): kept on in the dark against his doctrine, or back
    /// to it. Out of a fight his lights are seen to at once (<see cref="HeldLightsPass"/>); in one, at his next think.</summary>
    internal void HoldBeams(in AiContext ctx, int entity, bool on)
    {
        ref var cap = ref Get(entity);
        cap.BeamsHeld = on;
        if (ctx.Ships.SlotOf(entity) is int slot and >= 0 && !InFight(ctx, slot, entity))
            HeldLights(ctx, ref cap, slot);
        Recount();
    }

    /// <summary>The player's star shell for a ship (docs/battle/night.md Q9): one shell on a contact (an entity), or over a
    /// point (a packed lat/lon), or called off (both 0). The task starts at once and ends with its shell; his doctrine
    /// leaves it be until then.</summary>
    internal void HoldStar(in AiContext ctx, int entity, int contact, long point)
    {
        ref var cap = ref Get(entity);
        bool had = cap.StarHeld;
        cap.StarHeld = contact != 0 || point != 0;
        if (cap.StarHeld)
            ctx.Issue(point != 0 ? StarShellSystem.IlluminateAt(entity, point, once: true) : StarShellSystem.Illuminate(entity, contact, once: true));
        else if (had)
            ctx.Issue(StarShellSystem.Illuminate(entity, 0));
        Recount();
    }

    /// <summary>The player's shell order for a ship's main guns (docs/battle/shell-choice.md A5): a kind + 1 held against
    /// his choice, or 0 to hand it back (judged afresh); either clears what ran out.</summary>
    internal void HoldShells(int entity, byte kind)
    {
        ref var cap = ref Get(entity);
        (cap.ShellHeld, cap.ShellSpent) = (kind, 0);
        if (cap.Shells != null)
            Array.Clear(cap.Shells);
    }

    /// <summary>A ship's captain, made (told nothing yet) if it has none.</summary>
    ref Captain Get(int entity)
    {
        int i = Find(entity);
        if (i < 0)
        {
            i = Insert(entity);
            // his track of the ship ahead made with him, not the first time he follows (that may be in a think)
            captains[i] = new Captain { Entity = entity, AskedKn = double.NaN, Track = new Trail(TrailPoints) };
        }
        return ref captains[i];
    }

    int Find(int entity)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int e = captains[mid].Entity;
            if (e == entity)
                return mid;
            if (e < entity)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return -1;
    }

    int Insert(int entity)
    {
        if (count == captains.Length)
            Array.Resize(ref captains, captains.Length * 2);
        fallBacks.EnsureCapacity(captains.Length);
        int i = count;
        while (i > 0 && captains[i - 1].Entity > entity)
        {
            captains[i] = captains[i - 1];
            i--;
        }
        captains[i] = new Captain { Entity = entity };
        count++;
        return i;
    }

    void Remove(int i)
    {
        Array.Copy(captains, i + 1, captains, i, count - i - 1);
        captains[--count] = default;
    }
}
