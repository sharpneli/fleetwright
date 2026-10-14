using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

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

    /// <summary>Fallen out and home.</summary>
    Home,

    /// <summary>Combing a torpedo's track her lookouts saw coming at her.</summary>
    Combing,
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
/// its last waypoint. An explicit <see cref="AiCommand.Detach"/> pins it out until resumed.</para>
/// <para><b>Limits</b> come from above as orders (<see cref="AiCommand.SpeedLimit"/>): what he rings is never more,
/// while what was asked is kept. So does the clearance from the coast his ship must keep (its formation's half-width,
/// <see cref="AiCommand.Clearance"/>), which he hands the helm.</para>
/// <para><b>Notices:</b> while the player holds a ship, an order its superior gives it is dropped, and the captain
/// raises a notice ("resume?") once; the admiral raises it too when his group's orders change. It's cleared when the
/// ship is handed back or let go.</para>
/// A captain whose ship is gone is dropped; one whose target is gone holds his course. His guns and Fight are in
/// CaptainLayer.Fire.cs.</summary>
public sealed partial class CaptainLayer(GroupSystem groups, Encounters encounters, SeenContacts seen, Doctrine doctrine, Vec3d?[] homes) : IAiLayer
{
    struct Captain
    {
        public int Entity;
        public CaptainOrder Order;
        public OrderSource Source;
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
        public bool FightAway;          // fighting: outgunned, turned away
        public bool Met;                // doctrine's Engage taken up in this encounter
        public long TorpedoMs;          // when he last fired torpedoes (0: never)
        public bool TorpedoHeld;        // his flotilla attacking together: his torpedoes wait for its signal
        public CaptainReason Reason;    // why, when it isn't simply his order (fallen out)
        public int FitVersion;          // his ship's condition's version when he last judged its fitness
        public long CombUntilMs;        // combing a torpedo's track until then (0: not)
        public double CombDeg, CombBackDeg;   // the comb's course, and the course ordered before it
    }

    Captain[] captains = new Captain[16];
    int count, active;

    public int Count => count;

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
    public bool Active => active > 0 || Fighting;

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

    static bool IsActive(in Captain c) => c.Order is CaptainOrder.Follow or CaptainOrder.Run or CaptainOrder.Station or CaptainOrder.Shadow or CaptainOrder.Fight or CaptainOrder.Home || c.Watch;

    /// <summary>A ship's captain's order, its target unit and who gave it; None if he has none.</summary>
    public (CaptainOrder Order, int Target, OrderSource Source) Of(int entity)
    {
        int i = Find(entity);
        return i >= 0 ? (captains[i].Order, captains[i].Target, captains[i].Source) : (CaptainOrder.None, 0, OrderSource.Player);
    }

    /// <summary>Whether a ship's captain is combing a torpedo's track his lookouts saw (his admiral reads it: tracks seen).</summary>
    public bool Combing(int entity, long nowMs) => Find(entity) is int i and >= 0 && captains[i].CombUntilMs > nowMs;

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
        return c.Source == OrderSource.Superior && c.Order == order && c.Target == target && c.DistanceM == astern && c.AbeamM == abeam;
    }

    /// <summary>The entity of the i-th captain (in entity order) if he holds an order from his superior, else 0.</summary>
    public int SuperiorsAt(int i) => captains[i].Source == OrderSource.Superior ? captains[i].Entity : 0;

    /// <summary>An order to a ship's captain, as a command (<see cref="UnitOrders"/>).</summary>
    public void Order(in AiContext ctx, in Command c)
    {
        int e = c.Entity, slot = ctx.Ships.SlotOf(e);
        if (slot < 0)
            return;   // dropped or never made: an order that reached no ship
        var code = (AiCommand)c.Code;
        var source = (OrderSource)c.B;
        switch (code)
        {
            case AiCommand.FireAtWill or AiCommand.HoldFire or AiCommand.Engage:
                FireOrdered(ctx, c, source);   // the guns: not the helm, so he keeps his place in the line
                return;
            case AiCommand.Form:
                return;   // a group's
        }
        var g = groups.Of(e);
        var detached = g?.DetachmentOf(e) ?? Detachment.InLine;

        switch (code)
        {
            case AiCommand.Resume:
                if (g != null && detached != Detachment.InLine)
                    ctx.Issue(GroupSystem.Detach(e, Detachment.InLine));   // his admiral gives him his station
                if (Find(e) is int ri and >= 0)
                    captains[ri].Asked = false;
                return;
            case AiCommand.Detach:
                if (g == null || detached == Detachment.Pinned)
                    return;
                ctx.Issue(GroupSystem.Detach(e, Detachment.Pinned));
                Release(ctx, e);   // he holds what he was doing, the player's now
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

        if (source == OrderSource.Superior && g != null && detached != Detachment.InLine)
        {
            Ask(e);   // the player has him: his superior's order waits for the hand-back, and is given again then
            return;
        }
        if (source == OrderSource.Player && g != null && detached == Detachment.InLine)
        {
            ctx.Issue(GroupSystem.Detach(e, Detachment.Nudged));   // the player takes him out of the line
            detached = Detachment.Nudged;
        }

        ref var cap = ref Get(e);
        var own = ctx.Ships[slot];
        if (source == OrderSource.Player && cap.Source == OrderSource.Superior)
        {
            SetLimit(ctx, ref cap, own, 0);   // his superior's limit and clearance go with his superior
            SetClearance(ctx, ref cap, e, 0);
        }
        if (source == OrderSource.Superior)
            cap.Asked = false;   // in his superior's hands again
        cap.Source = source;
        cap.Reason = CaptainReason.None;   // ordered: doing what he's told
        switch (code)
        {
            case AiCommand.Course:
                (cap.Order, cap.Target) = (CaptainOrder.Course, 0);
                if (cap.CombUntilMs > ctx.NowMs)
                    cap.CombBackDeg = c.X;   // combing a track: the course is steered once it's past
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
            case AiCommand.Hold:
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
                (cap.FightSide, cap.FightEnemy, cap.FightAway) = (0, 0, false);
                break;
            default:
                throw new ArgumentException($"unknown AI command {c.Code}");
        }
        cap.Watch = cap.Order == CaptainOrder.Route && source == OrderSource.Player && detached == Detachment.Nudged;
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
        if (cap.Order is CaptainOrder.Follow or CaptainOrder.Run or CaptainOrder.Station or CaptainOrder.Shadow or CaptainOrder.Fight or CaptainOrder.Home or CaptainOrder.None)
            (cap.Order, cap.Target, cap.AskedKn) = (own.Helm == Helm.Route ? CaptainOrder.Route : CaptainOrder.Course, 0, own.SpeedOrderKn);
        SetLimit(ctx, ref cap, own, 0);
        SetClearance(ctx, ref cap, entity, 0);
        (cap.Source, cap.Asked) = (OrderSource.Player, false);
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
            if (!IsActive(cap) || ctx.Ships.Combat.Condition(slot).Status != ShipStatus.Afloat)
                continue;   // nothing to do, or his ship going down
            var own = ctx.Ships[slot];
            if (cap.Watch)
            {
                // a route the player gave a ship out of its line: at its end, back to its admiral
                if (own.RouteLoop)
                    cap.Watch = false;   // a patrol has no end
                else if (own.Helm != Helm.Route)
                {
                    (cap.Watch, cap.Order) = (false, CaptainOrder.Course);
                    if (groups.Of(cap.Entity)?.DetachmentOf(cap.Entity) == Detachment.Nudged)
                        ctx.Issue(GroupSystem.Detach(cap.Entity, Detachment.InLine));
                }
                continue;
            }
            if (ctx.Ships.RidesAnother(slot))
                continue;   // carried on its guide's rails, held exactly on station: an order would only derail the line
            if (cap.CombUntilMs > ctx.NowMs)
                continue;   // combing a torpedo's track: that first
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
            else if (cap.Order == CaptainOrder.Follow || cap.Order == CaptainOrder.Station && cap.AbeamM == 0)
            {
                // in line ahead: on the track the ship ahead steamed, so he turns where it turned (laid straight astern
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
        if (Fighting)
            FirePass(ctx);
        Recount();
    }

    /// <summary>The range a shadower keeps when he's given none: a share of how far he can see now (the weather's
    /// visibility, or the dark's), kept between the settings' least and most.</summary>
    static double ShadowRange(in AiContext ctx, ShipState own)
    {
        var s = ctx.Settings;
        double vis = ctx.World.Weather.At(own.Position, ctx.World.Time).VisibilityM;
        double reach = Math.Min(vis, SightModel.LightReachM(Light.SunElevationDeg(own.Position, ctx.World.Time), s));
        return Math.Clamp(s.ShadowVisibilityShare * reach, s.ShadowMinM, s.ShadowMaxM);
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
        active = 0;
        for (int i = 0; i < count; i++)
            if (IsActive(captains[i]))
                active++;
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
            h.Add((int)c.Source);
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
            h.Add(c.Met);
            h.Add(c.TorpedoMs);
            if (c.TorpedoHeld)
                h.Add(true);   // only then: worlds without flotilla attacks hash as before
            h.Add((int)c.Reason);
            h.Add(c.FitVersion);
            h.Add(c.CombUntilMs);
            h.Add(c.CombDeg);
            h.Add(c.CombBackDeg);
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
            v.Asked[k] = i >= 0 && captains[i].Asked;
            v.Reason[k] = i >= 0 ? (byte)captains[i].Reason : (byte)0;
            (v.Fire[k], v.FireTarget[k]) = i >= 0 ? ((byte)captains[i].Fire, captains[i].FireTarget) : ((byte)0, 0);
        }
    }

    /// <summary>A ship's captain, made (told nothing yet) if it has none.</summary>
    ref Captain Get(int entity)
    {
        int i = Find(entity);
        if (i < 0)
        {
            i = Insert(entity);
            captains[i] = new Captain { Entity = entity, AskedKn = double.NaN };
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
