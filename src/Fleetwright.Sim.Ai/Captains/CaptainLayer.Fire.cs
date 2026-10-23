using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The captains' guns and fighting (docs/battle/fighting.md 4.2c). A captain keeps his ship's fire order (at
/// will by default, hold fire, or engage a contact) and is the only one who puts its batteries on a target
/// (<see cref="GunnerySystem.Engage"/>, <see cref="GunnerySystem.Cease"/>), as he's the only one who orders its helm.
/// <list type="bullet">
/// <item><b>Fire at will:</b> each second, for every ship on the fine tick that's in an encounter or has a battery
/// engaged, each battery picks from its navy's contacts in sight within its reach (<see cref="SimSettings.FireReachShare"/>
/// of the range table's): one that bears first, then (secondaries) the smallest type, then the nearest, by the range
/// she'll be at <see cref="SimSettings.FireLeadS"/> on and longer by <see cref="SimSettings.FireCrowdShare"/> for each
/// other ship's battery of the kind already on her (one closing before one running; the fire spread). It keeps its
/// target until it's unseen for <see cref="SimSettings.FireLostMs"/> or past <see cref="SimSettings.FireKeepShare"/> of its
/// reach, or another bears where it doesn't, is smaller (secondaries: by a type made out, never a contact still unknown,
/// or each ship coming out of the haze would take the fire off the one held), or is nearer so than
/// <see cref="SimSettings.FireSwitchShare"/> of it: no flicking between targets, as each shift starts the track again.
/// With nothing to shoot at it ceases fire, so the ship can go back on rails.</item>
/// <item><b>Engage:</b> the chief batteries (<see cref="BatteryRow.Chief"/>: the main, or with none the heaviest guns) on
/// the contact while the navy holds it (then at will again); the others pick their own. <b>Hold fire:</b> every battery
/// ceases.</item>
/// <item><b>Who gave it:</b> the player's order to the ship, or an admiral's to his tree (its group kept with it). An
/// admiral's order he hands down again (a redistribution) only reaches the ships still holding his; a fresh one, from a
/// group just given a fire order, reaches all of them.</item>
/// <item><b>Fight:</b> a behaviour, as follow and station are: close to the band (by its chief guns' reach), hold it with
/// them bearing, turn away when outgunned (<see cref="Behaviours.Fight"/>). A torpedo craft's (no gun of 6" or more, torpedoes left)
/// is the attack: close to <see cref="SimSettings.TorpedoAttackShare"/> of its torpedoes' fast setting whatever
/// guns it faces, fire, turn away for <see cref="SimSettings.TorpedoTurnAwayMs"/>, and again (docs/battle/fighting.md
/// 4.6).</item>
/// <item><b>Torpedoes at will</b> (4.6), beside the guns, unless holding fire: at the engaged ship, else the biggest
/// contact in sight, when its run for the fast setting is within <see cref="SimSettings.TorpedoFireShare"/> of its range
/// and the launchers bear; <see cref="SimSettings.TorpedoSpread"/> a firing, then <see cref="SimSettings.TorpedoRefireMs"/>
/// before the next.</item>
/// </list>
/// Guns are silent in the coarse tier (4.9): a coarse ship keeps its batteries' targets, and they fire again on the fine
/// tick.</summary>
public sealed partial class CaptainLayer
{
    readonly List<int> fighters = new(64);
    readonly LineOfFire lines = new();
    readonly TorpedoWatch torpedoWatch = new();
    double[] underFire = [];   // by slot: the range of the nearest enemy battery on her, m (0: none), as of this think
    bool anyUnderFire;
    readonly List<(int Ship, double AsternM, double AbeamM)> unitLine = new(16);
    int fighting;          // ships with a battery engaged after the last pass
    bool fightingFine;     // any of them on the fine tick

    /// <summary>A ship's fire order, its target (Engage) and the group it came from (0: the ship's own).</summary>
    public (FireOrder Order, int Target, int From) FireOf(int entity)
    {
        int i = Find(entity);
        return i >= 0 ? (captains[i].Fire, captains[i].FireTarget, captains[i].FireFrom) : (FireOrder.AtWill, 0, 0);
    }

    /// <summary>Whether he has anything to watch for the guns: an encounter open, or a battery engaged.</summary>
    bool Fighting => encounters.Count > 0 || fighting > 0;

    /// <summary>A fire order for his ship (a command): the player's always; an admiral's when it's fresh (X 1) or he holds
    /// that admiral's order already.</summary>
    void FireOrdered(in AiContext ctx, in Command c, OrderSource source)
    {
        int e = c.Entity;
        int from = source == OrderSource.Superior ? (int)c.Y : 0;
        ref var cap = ref Get(e);
        if (source == OrderSource.Superior && c.X == 0 && cap.FireFrom != from)
            return;   // the player's order, or another admiral's, stands
        cap.Fire = (AiCommand)c.Code switch
        {
            AiCommand.HoldFire => FireOrder.Hold,
            AiCommand.Engage => FireOrder.Engage,
            _ => FireOrder.AtWill,
        };
        (cap.FireTarget, cap.FireFrom) = (cap.Fire == FireOrder.Engage ? (int)c.A : 0, from);
        if (cap.Fire == FireOrder.Hold && ctx.Ships.SlotOf(e) is int slot and >= 0 && ctx.Ships.Combat.Engaged(slot) > 0)
            ctx.Issue(GunnerySystem.Cease(e));
    }

    /// <summary>Each fighting ship's batteries put on their targets (and a ship alone's doctrine taken up).</summary>
    void FirePass(in AiContext ctx)
    {
        var store = ctx.Ships;
        fighters.Clear();
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            if (encounters.Of(slot, store[slot].Entity) != 0 || store.Combat.Engaged(slot) > 0)
                fighters.Add(slot);
        }
        (fighting, fightingFine) = (0, false);
        CountOn(store);
        foreach (int slot in fighters)
        {
            var own = store[slot];
            if (own.Tier == ClockTier.Fine)
            {
                ref var cap = ref Get(own.Entity);
                if (cap.TorpedoHeld && !groups.InLine(own.Entity))
                    cap.TorpedoHeld = false;   // out of her flotilla's line: her torpedoes her own again
                FallOut(ctx, ref cap, own, slot);
                OutOfRounds(ctx, ref cap, own);
                Comb(ctx, ref cap, own);
                TakeUpStanding(ctx, ref cap, own, slot);
                Aim(ctx, ref cap, own, slot);
            }
            if (store.Combat.Engaged(slot) > 0)
            {
                fighting++;
                fightingFine |= own.Tier == ClockTier.Fine;
            }
        }
    }

    /// <summary>A ship alone whose doctrine is to fight (docs/battle/command.md "Doctrine": Engage) takes up Fight when
    /// it first meets an enemy in sight in an encounter; it's armed again once it's out of encounters. Not while the
    /// player holds her (<see cref="Captain.Held"/>): the player's order is what she does.</summary>
    void TakeUpStanding(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        if (cap.Held || cap.Order == CaptainOrder.Home || Tasks?.Avoids(Unit.Ship(own.Entity)) == true || (Engage)doctrine.Of(own.Entity, own.Side, DoctrineField.Engage) != Engage.Fight)
            return;
        if (encounters.Of(slot, own.Entity) == 0)
        {
            cap.Met = false;
            return;
        }
        if (cap.Met || groups.InLine(own.Entity) || seen.Near(ctx.World, own.Side, own.Position, ctx.Settings.FightCompareM).Length == 0)
            return;
        cap.Met = true;
        (cap.Order, cap.Target, cap.DistanceM, cap.AbeamM, cap.FightSide, cap.FightEnemy, cap.FightAway) = (CaptainOrder.Fight, 0, 0, 0, 0, 0, false);
        Recount();
    }

    // a ship's candidates, found once for all its batteries: each contact in sight within its widest reach, its entity
    // and slot, range (m), the range it'll be at FireLeadS on (m), bearing from ahead and the rank of its type for a
    // secondary battery
    int[] candEntity = new int[32], candRank = new int[32], candSlot = new int[32];
    double[] candRange = new double[32], candLead = new double[32], candBearing = new double[32];
    Vec3d[] candPos = new Vec3d[32];
    sbyte[] candFoul = new sbyte[32];   // its line of fire: -1 not asked yet, 0 clear, 1 foul
    int candCount;
    readonly Dictionary<Loadout, double[]> reaches = new(ReferenceEqualityComparer.Instance);
    // by target slot, the batteries on her (chief, and the rest), kept as this pass's orders go out; and the ship's own
    // batteries' targets as its captain's orders leave them
    int[] onChief = [], onOther = [];
    int[] ownTargets = new int[8];

    /// <summary>His ship's batteries on what his fire order wants.</summary>
    void Aim(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        var pic = ctx.World.Sightings.Of(own.Side);
        if (cap.Fire == FireOrder.Engage && (pic == null || pic.IndexOf(cap.FireTarget) < 0 || pic.Lost(cap.FireTarget)))
            (cap.Fire, cap.FireTarget) = (FireOrder.AtWill, 0);   // gone from the picture, or seen going down: at will again
        var reach = Reaches(ctx, ctx.Ships.Combat.Loadout(slot));
        candCount = -1;   // found when a battery first needs them
        if (ownTargets.Length < cls.Batteries.Length)
            ownTargets = new int[Math.Max(cls.Batteries.Length, ownTargets.Length * 2)];
        for (int b = 0; b < cls.Batteries.Length; b++)
            ownTargets[b] = ctx.Ships.Combat.Target(slot, b);
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            int current = ownTargets[b];
            int want = cap.Fire switch
            {
                FireOrder.Hold => 0,
                FireOrder.Engage when cls.Batteries[b].Chief => cap.FireTarget,
                _ => Pick(ctx, pic, own, cls, b, reach[b], reach[^1], current),
            };
            if (want == current)
                continue;
            ctx.Issue(want == 0 ? GunnerySystem.Cease(own.Entity, b) : GunnerySystem.Engage(own.Entity, b, want));
            // the next ships' picks see it at once, so a line doesn't all take the same one in the same second
            var on = cls.Batteries[b].Chief ? onChief : onOther;
            if (ctx.Ships.SlotOf(current) is int cs and >= 0 && cs < on.Length)
                on[cs] = Math.Max(on[cs] - 1, 0);
            if (ctx.Ships.SlotOf(want) is int ws and >= 0 && ws < on.Length)
                on[ws]++;
            ownTargets[b] = want;
        }
        Torpedoes(ctx, ref cap, own, slot, pic);
    }

    /// <summary>The batteries on each ship (by her slot), main and the rest, from the targets the last pass set.</summary>
    void CountOn(ShipStore store)
    {
        if (onChief.Length < store.Slots)
            (onChief, onOther) = (new int[Math.Max(store.Slots, onChief.Length * 2)], new int[Math.Max(store.Slots, onChief.Length * 2)]);
        else
        {
            Array.Clear(onChief);
            Array.Clear(onOther);
        }
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            if (store.Combat.Engaged(slot) == 0)
                continue;
            var cls = store[slot].Handling.Class;
            for (int b = 0; b < cls.Batteries.Length; b++)
                if (store.Combat.Target(slot, b) is int t and > 0 && store.SlotOf(t) is int ts and >= 0)
                    (cls.Batteries[b].Chief ? onChief : onOther)[ts]++;
        }
    }

    /// <summary>How many other ships' batteries of a battery's kind are on a target (her slot).</summary>
    int Crowd(ShipClass cls, int b, int target, int targetSlot)
    {
        bool chief = cls.Batteries[b].Chief;
        var on = chief ? onChief : onOther;
        if (targetSlot < 0 || targetSlot >= on.Length)
            return 0;
        int n = on[targetSlot];
        for (int k = 0; k < cls.Batteries.Length; k++)
            if (ownTargets[k] == target && cls.Batteries[k].Chief == chief)
                n--;   // his own aren't another ship's splashes
        return Math.Max(n, 0);
    }

    /// <summary>His torpedoes at will: at the engaged ship if it's within the fast setting's reach, else the biggest
    /// contact in sight within it, the nearest first, when his launchers bear on it.</summary>
    void Torpedoes(in AiContext ctx, ref Captain cap, ShipState own, int slot, Picture? pic)
    {
        var s = ctx.Settings;
        var cls = own.Handling.Class;
        long now = ctx.NowMs;
        if (pic == null || cls.Launchers.Length == 0 || cap.Fire == FireOrder.Hold || cap.TorpedoHeld || cap.TorpedoMs != 0 && now - cap.TorpedoMs < s.TorpedoRefireMs)
            return;
        bool onOrder = (TorpedoUse)doctrine.Of(own.Entity, own.Side, DoctrineField.Torpedoes) == TorpedoUse.OnOrder;
        if (onOrder && cap.Fire != FireOrder.Engage)
            return;   // only at a ship he's ordered to engage
        var (reach, speed) = FastSetting(cls);
        reach *= s.TorpedoFireShare;
        int target = 0;
        if (cap.Fire == FireOrder.Engage && pic.IndexOf(cap.FireTarget) is int ei and >= 0 && pic[ei].InSight && !pic[ei].Lost && Run(ctx, pic, own, ei, speed) <= reach)
            target = cap.FireTarget;
        if (target == 0 && !onOrder)
        {
            int bestRank = int.MaxValue;
            double bestRun = double.MaxValue;
            foreach (int i in seen.Near(ctx.World, own.Side, own.Position, reach))
            {
                double run = Run(ctx, pic, own, i, speed);
                int rank = TorpedoRank(pic[i].Type);
                if (run <= reach && (rank < bestRank || rank == bestRank && run < bestRun))
                    (target, bestRank, bestRun) = (pic[i].Entity, rank, run);
            }
        }
        if (target == 0 || ctx.World.Torpedoes.Ready(ctx.World, own.Entity, target) == 0)
            return;
        int ti = pic.IndexOf(target);
        var (tq, tv, _) = pic.EstimateAt(ti, now, s);
        if (!TorpedoAim.Intercept(own.Position, tq, tv, speed, out double toward, out _))
            return;
        var aim = own.Position;
        var fwd = Earth.Forward(aim, toward);
        Earth.Advance(ref aim, ref fwd, 1000);
        if (lines.TorpedoFoul(ctx, own, aim, FastSetting(cls).RangeM, speed))
            return;   // a friend on the track: not now
        ctx.Issue(TorpedoSystem.Launch(own.Entity, target, s.TorpedoSpread));
        cap.TorpedoMs = now;
    }

    /// <summary>His flotilla's admiral holds his torpedoes for its signal, or lets them go (docs/battle/command.md 5.1b).</summary>
    internal void HoldTorpedoes(int entity, bool held)
    {
        if (Find(entity) is int i and >= 0 ? captains[i].TorpedoHeld != held : held)
            Get(entity).TorpedoHeld = held;
    }

    /// <summary>His flotilla's signal to fire (docs/battle/command.md 5.1b): a spread at the target from his launchers that
    /// are ready, aimed where his navy's picture says it'll be, unless a friend is on the track; once for a signal made at
    /// <paramref name="signalMs"/>: once torpedoes have gone since, or while a launch waits for its tubes to train (one
    /// that no longer bears by then holds its fire, and he tries again). With his tubes trained out on a beam
    /// (<paramref name="tubesDeg"/>, from ahead; 0 none) he waits for the turn to bring the aim on that side of him,
    /// rather than swing them round. Whether he fired.</summary>
    internal bool FireOnSignal(in AiContext ctx, int entity, int target, long signalMs, double tubesDeg)
    {
        var s = ctx.Settings;
        int slot = ctx.Ships.SlotOf(entity);
        var pic = slot >= 0 ? ctx.World.Sightings.Of(ctx.Ships[slot].Side) : null;
        if (pic == null || pic.IndexOf(target) is not (int ti and >= 0) || pic[ti].Lost || FiredSince(ctx, slot, signalMs) || ctx.World.Torpedoes.Launching(entity) ||
            ctx.World.Torpedoes.Ready(ctx.World, entity, target) == 0)
            return false;
        var own = ctx.Ships[slot];
        var (range, speed) = FastSetting(own.Handling.Class);
        var (tq, tv, _) = pic.EstimateAt(ti, ctx.NowMs, s);
        if (!TorpedoAim.Intercept(own.Position, tq, tv, speed, out double toward, out _))
            return false;
        if (tubesDeg != 0 && Math.Abs(Wrap180(Wrap180(toward - own.HeadingDeg) - tubesDeg)) > 90)
            return false;   // the turn not yet round far enough
        var aim = own.Position;
        var fwd = Earth.Forward(aim, toward);
        Earth.Advance(ref aim, ref fwd, 1000);
        if (lines.TorpedoFoul(ctx, own, aim, range, speed))
            return false;
        ctx.Issue(TorpedoSystem.Launch(entity, target, s.TorpedoSpread));
        Get(entity).TorpedoMs = ctx.NowMs;
        return true;
    }

    static double Wrap180(double d) => d - 360 * Math.Round(d / 360);

    /// <summary>Whether a ship's launchers have fired since a moment.</summary>
    static bool FiredSince(in AiContext ctx, int slot, long ms)
    {
        var cls = ctx.Ships[slot].Handling.Class;
        for (int l = 0; l < cls.Launchers.Length; l++)
            if (ctx.Ships.Combat.Launcher(slot, l, cls.Launchers[l], ctx.NowMs).FiredMs >= ms)
                return true;
        return false;
    }

    /// <summary>The run a torpedo at a speed needs to meet a contact going on as its navy holds it, m (infinite when it
    /// can't).</summary>
    double Run(in AiContext ctx, Picture pic, ShipState own, int i, double speedMs)
    {
        var (q, v, _) = pic.EstimateAt(i, ctx.NowMs, ctx.Settings);
        return TorpedoAim.Intercept(own.Position, q, v, speedMs, out _, out double run) ? run : double.PositiveInfinity;
    }

    /// <summary>A class's torpedoes' fast setting: the longest range of its launchers' (m), and its speed (m/s).</summary>
    internal static (double RangeM, double SpeedMs) FastSetting(ShipClass cls)
    {
        double range = 0, speed = 0;
        foreach (ref readonly var l in cls.Launchers)
        {
            ref readonly var t = ref cls.Torpedoes[l.Torpedo];
            if (t.FastRangeM > range)
                (range, speed) = (t.FastRangeM, t.FastKn * Handling.MsPerKnot);
        }
        return (range, speed);
    }

    /// <summary>The gun that makes a ship more than a torpedo craft: a cruiser's 6".</summary>
    const double TorpedoCraftGunMm = 150;

    /// <summary>Whom torpedoes are for: the biggest first.</summary>
    internal static int TorpedoRank(ShipType t) => t switch
    {
        ShipType.Capital => 0,
        ShipType.Cruiser => 1,
        ShipType.Merchant => 2,
        ShipType.Unknown => 3,
        ShipType.Destroyer => 4,
        _ => 5,
    };

    /// <summary>Whether a ship fights with its torpedoes: one with no gun of <see cref="TorpedoCraftGunMm"/> or more (a
    /// torpedo boat, a destroyer: not a cruiser), with torpedoes left in launchers that work.</summary>
    internal static bool TorpedoCraft(in AiContext ctx, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        if (!TorpedoCraft(cls))
            return false;
        var k = ctx.Ships.Combat.Condition(slot);
        for (int l = 0; l < cls.Launchers.Length; l++)
        {
            var st = ctx.Ships.Combat.Launcher(slot, l, cls.Launchers[l], ctx.NowMs);
            if (k.LauncherWorks(l) && st.Loaded + st.Spare > 0)
                return true;
        }
        return false;
    }

    /// <summary>Whether a class is a torpedo craft by its design: no gun of <see cref="TorpedoCraftGunMm"/> or more, and
    /// torpedo tubes.</summary>
    internal static bool TorpedoCraft(ShipClass cls)
    {
        foreach (ref readonly var b in cls.Batteries)
            if (b.CalibreMm >= TorpedoCraftGunMm)
                return false;
        return cls.Launchers.Length > 0;
    }

    /// <summary>Each battery's reach for fire at will (<see cref="SimSettings.FireReachShare"/> of the range table's), and
    /// the widest last: worked out once a loadout (her class's own, or the one she was given).</summary>
    double[] Reaches(in AiContext ctx, Loadout lo)
    {
        if (reaches.TryGetValue(lo, out var r))
            return r;
        r = new double[lo.Class.Batteries.Length + 1];
        for (int b = 0; b < lo.Class.Batteries.Length; b++)
        {
            r[b] = ctx.World.Gunnery.ReachM(lo, b) * ctx.Settings.FireReachShare;
            r[^1] = Math.Max(r[^1], r[b]);
        }
        return reaches[lo] = r;
    }

    /// <summary>A battery's target at will: the current one while it's held and in reach, unless another is better by
    /// bearing, by size (secondaries, a type made out) or by much in range to come and in the fire on her; else the best
    /// in sight; 0 for none.</summary>
    int Pick(in AiContext ctx, Picture? pic, ShipState own, ShipClass cls, int b, double reach, double widest, int current)
    {
        if (pic == null)
            return 0;
        var s = ctx.Settings;
        long now = ctx.NowMs;
        bool secondary = !cls.Batteries[b].Chief;
        var p = own.Position;
        if (candCount < 0)
            Candidates(ctx, pic, own, widest);

        int ci = current != 0 ? pic.IndexOf(current) : -1;
        bool keep = ci >= 0 && !pic[ci].Lost && now - pic[ci].SeenMs <= s.FireLostMs;
        if (keep && lines.GunsFoul(ctx, own, pic.EstimateAt(ci, now, s).Position))
            keep = false;   // a friend in the way: not while he's there
        double curRange = 0, curBy = 0;
        bool curBears = false;
        int curRank = 0;
        if (keep)
        {
            var (q, v, _) = pic.EstimateAt(ci, now, s);
            curRange = (q - p).Length;
            keep = curRange <= reach * s.FireKeepShare / s.FireReachShare;   // held a little further than taken: no flicking at the edge
            curBears = Bears(cls, b, Earth.Bearing(p, q) - own.HeadingDeg);
            curRank = secondary ? Rank(pic[ci].Type) : 0;
            curBy = Lead(own, q, v, s) * (1 + s.FireCrowdShare * Crowd(cls, b, current, ctx.Ships.SlotOf(current)));
        }

        int best = 0, bestRank = int.MaxValue;
        bool bestBears = false;
        double bestBy = double.MaxValue;
        for (int k = 0; k < candCount; k++)
        {
            if (candRange[k] > reach || Foul(ctx, own, k))
                continue;
            bool bears = Bears(cls, b, candBearing[k]);
            int rank = secondary ? candRank[k] : 0;
            double by = candLead[k] * (1 + s.FireCrowdShare * Crowd(cls, b, candEntity[k], candSlot[k]));
            if (bears != bestBears ? bears : rank != bestRank ? rank < bestRank : by < bestBy)
                (best, bestBears, bestRank, bestBy) = (candEntity[k], bears, rank, by);
        }
        if (!keep)
            return best;
        if (best == 0 || best == current)
            return current;
        if (bestBears != curBears)
            return bestBears ? best : current;
        // by size only for a type made out: an unknown's smaller rank is a guess
        if (bestRank != curRank && !(bestRank < curRank && bestRank == Rank(ShipType.Unknown)))
            return bestRank < curRank ? best : current;
        return bestBy < s.FireSwitchShare * curBy ? best : current;
    }

    /// <summary>The range a contact (where and how his navy has her going) will be at <see cref="SimSettings.FireLeadS"/>
    /// on at the rate it's opening or closing now, m: never under a tenth of the range now.</summary>
    static double Lead(ShipState own, Vec3d q, Vec3d v, SimSettings s)
    {
        var d = q - own.Position;
        double range = d.Length;
        if (range < 1)
            return range;
        double rate = Vec3d.Dot(v - own.Forward * own.Speed, d) / range;
        return Math.Max(range + rate * s.FireLeadS, 0.1 * range);
    }

    /// <summary>The contacts his navy holds in sight within his widest reach: range now and to come, bearing from ahead,
    /// type's rank.</summary>
    void Candidates(in AiContext ctx, Picture pic, ShipState own, double widest)
    {
        var p = own.Position;
        double heading = own.HeadingDeg;
        var near = seen.Near(ctx.World, own.Side, p, widest);
        if (candEntity.Length < near.Length)
        {
            int n = Math.Max(near.Length, candEntity.Length * 2);
            (candEntity, candRank, candSlot, candRange, candLead, candBearing) = (new int[n], new int[n], new int[n], new double[n], new double[n], new double[n]);
            (candPos, candFoul) = (new Vec3d[n], new sbyte[n]);
        }
        candCount = 0;
        foreach (int i in near)
        {
            var q = seen.Position(own.Side, i);
            int e = pic[i].Entity;
            (candEntity[candCount], candSlot[candCount], candRange[candCount]) = (e, ctx.Ships.SlotOf(e), (q - p).Length);
            candLead[candCount] = Lead(own, q, seen.Velocity(own.Side, i), ctx.Settings);
            (candBearing[candCount], candRank[candCount]) = (Earth.Bearing(p, q) - heading, Rank(pic[i].Type));
            (candPos[candCount], candFoul[candCount]) = (q, -1);
            candCount++;
        }
    }

    /// <summary>Whether a candidate's line of fire is foul (a friend near the fall), asked once a think.</summary>
    bool Foul(in AiContext ctx, ShipState own, int k)
    {
        if (candFoul[k] < 0)
            candFoul[k] = (sbyte)(lines.GunsFoul(ctx, own, candPos[k]) ? 1 : 0);
        return candFoul[k] == 1;
    }

    /// <summary>Whether any mount of a battery has a relative bearing in its arcs.</summary>
    static bool Bears(ShipClass cls, int b, double relativeDeg)
    {
        foreach (int m in cls.Links(cls.Batteries[b].Mounts))
            if (CombatStore.InArcs(cls, cls.Mounts[m], relativeDeg))
                return true;
        return false;
    }

    /// <summary>A secondary battery's preference by what the contact is taken for: the smallest first (a merchant last).</summary>
    static int Rank(ShipType t) => t switch
    {
        ShipType.Small => 0,
        ShipType.Destroyer => 1,
        ShipType.Unknown => 2,
        ShipType.Cruiser => 3,
        ShipType.Capital => 4,
        _ => 5,
    };

    /// <summary>His Fight, each second (docs/battle/command.md 5.1g): the enemy (the one ordered, else the main battery's
    /// target, else the nearest in sight), and his unit's matchup against what his navy holds in sight round him. Under
    /// doctrine's bar (1 / OutgunnedRatio) he turns away, and comes back once he's well over it; but one that can't get
    /// away (the enemy as fast) runs only to a stronger force of his own, else he fights. A torpedo craft attacks when
    /// the judgement has its side attacking, or when it's ordered to (whoever ordered it judged): in to its torpedoes'
    /// range, fire, away, and in again. One that can hardly be
    /// hurt (<see cref="SimSettings.FightFinishConfidence"/>) closes in to finish the enemy. Nothing while there's nobody
    /// (he keeps what's ordered).</summary>
    void FightThink(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var s = ctx.Settings;
        var cls = own.Handling.Class;
        int main = ChiefBattery(cls);
        double reach = main >= 0 ? ctx.World.Gunnery.ReachM(ctx.Ships.Combat.Loadout(slot), main) : s.ShadowMinM;
        double band = cap.DistanceM > 0 ? cap.DistanceM : doctrine.Of(own.Entity, own.Side, DoctrineField.RangeShare) * reach;

        int enemy = new Unit(cap.Target).ShipOf(groups);
        if (enemy == 0 || !ctx.Picture.TryGet(own.Side, enemy, out var t) || ctx.World.Sightings.Of(own.Side)?.Lost(enemy) == true)
        {
            enemy = main >= 0 ? ctx.Ships.Combat.Target(slot, main) : 0;
            if (enemy == 0 || !ctx.Picture.TryGet(own.Side, enemy, out t))
                enemy = Nearest(ctx, own, s.FightCompareM, out t);
        }
        if (enemy == 0)
            return;
        if (enemy != cap.FightEnemy)
            (cap.FightEnemy, cap.FightSide) = (enemy, 0);

        bool judged = Judge.Near(ctx, own, out var m);
        double accept = Judge.Bars(own.Entity, own.Side).Accept, c = judged ? m.Confidence : double.PositiveInfinity;
        bool away = cap.FightAway ? c < accept / s.FightRallyShare : c < accept;
        double fallBack = double.NaN;
        if (away && !m.ACanRefuse(s) && double.IsNaN(fallBack = FallBack(ctx, own)))
            away = false;   // no escape: he fights

        // ordered to attack, or in his flotilla's attack (his torpedoes held for its signal): whoever ordered it judged
        if (TorpedoCraft(ctx, own, slot) && (cap.AttackOrdered || cap.TorpedoHeld || !away && (m.Terms.Style & MatchupStyle.AttackByA) != 0))
        {
            // the attack: in to the torpedoes' range, fire, away, and in again
            bool off = cap.TorpedoMs != 0 && ctx.NowMs - cap.TorpedoMs < s.TorpedoTurnAwayMs;
            double attack = s.TorpedoAttackShare * FastSetting(cls).RangeM;
            var (course0, kn0) = off ? Behaviours.Run(own, t) : Behaviours.Fight(own, t, attack, s.FightBandShare, s.FightCloseDeg, s.FightMinCloseKn, ref cap.FightSide);
            // under fire on the run-in, and away after: evading, but steady for the last of it (the research: hold for the launch)
            if (off || Earth.Distance(own.Position, t.Position) > s.EvadeSteadyShare * attack)
                course0 = Evade(ctx, own, slot, course0, attackRun: true);
            cap.FightAway = false;
            Give(ctx, own, course0, cap.LimitKn > 0 ? Math.Min(kn0, cap.LimitKn) : kn0);
            return;
        }

        cap.FightAway = away;
        if (!away && c >= s.FightFinishConfidence && m.Terms.LostA < s.FightFinishLostShare)
            band *= s.FightFinishShare;   // he can hardly be hurt (a cripple, not a torpedo boat): in to finish her
        var (course, kn) = away ? Behaviours.Run(own, t) : Behaviours.Fight(own, t, band, s.FightBandShare, s.FightCloseDeg, s.FightMinCloseKn, ref cap.FightSide);
        if (away)
        {
            if (double.IsNaN(fallBack))
                fallBack = FallBack(ctx, own);
            course = Evade(ctx, own, slot, FallBackCourse(course, fallBack, s), attackRun: false);
        }
        if (cap.LimitKn > 0)
            kn = Math.Min(kn, cap.LimitKn);
        Give(ctx, own, course, kn);
    }

    /// <summary>Falling back on the main body (docs/battle/command.md 5.1c): the bearing of the nearest own force (another
    /// top unit of his side) within <see cref="SimSettings.FallBackM"/> that's a match for the enemy in sight round him
    /// (its matchup against them at doctrine's bar or better); NaN if none. Judged once a moment for his unit.</summary>
    double FallBack(in AiContext ctx, ShipState own)
    {
        var s = ctx.Settings;
        var mine = groups.UnitOf(own.Entity);
        int key = mine != null ? Unit.Group(mine.GroupId).Id : Unit.Ship(own.Entity).Id;
        if (fallBacksMs != ctx.NowMs)
        {
            fallBacks.Clear();   // a moment's judgements only: at most one a captain
            fallBacksMs = ctx.NowMs;
        }
        if (fallBacks.TryGetValue(key, out double fb))
            return fb;
        double accept = Judge.Bars(own.Entity, own.Side).Accept, best = s.FallBackM, deg = double.NaN;
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (g.Parent != 0 || mine != null && g.GroupId == mine.GroupId || ctx.Ships.SlotOf(groups.GuideShip(g)) is not (int gs and >= 0))
                continue;
            var guide = ctx.Ships[gs];
            double d = Earth.Distance(own.Position, guide.Position);
            if (guide.Side != own.Side || d >= best || Judge.GroupAgainstNear(ctx, g, own) < accept)
                continue;
            (best, deg) = (d, Earth.Bearing(own.Position, guide.Position));
        }
        fallBacks[key] = deg;
        return deg;
    }

    /// <summary>This moment's fall-back bearings by unit, sized with the captains (<see cref="Insert"/>), so the first
    /// captain to fall back in a fight allocates nothing.</summary>
    readonly Dictionary<int, double> fallBacks = [];
    long fallBacksMs = -1;

    /// <summary>The course away: toward the force he falls back on if that's within <see cref="SimSettings.FallBackDeg"/>
    /// of straight away, else straight away.</summary>
    static double FallBackCourse(double away, double toward, SimSettings s)
    {
        if (double.IsNaN(toward))
            return away;
        double off = toward - away;
        return Math.Abs(off - 360 * Math.Round(off / 360)) <= s.FallBackDeg ? toward : away;
    }

    /// <summary>Whether his ship is past doctrine's fall-out (docs/battle/command.md 5.1a), judged when its condition
    /// changes: too slow, too few main guns, listing too far, too much water, or (when doctrine falls out at all) unable to
    /// steer. Then she leaves her group (the line closes up behind her: going-down ships leave the same way) and goes
    /// home, his own order, firing at will as she goes. Not once the player has ordered her on after it
    /// (<see cref="Captain.Overruled"/>), nor while the player holds her (<see cref="Captain.Held"/>; judged again once
    /// she's let go).</summary>
    void FallOut(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var k = ctx.Ships.Combat.Condition(slot);
        if (cap.Overruled || cap.Held || cap.Order == CaptainOrder.Home || !k.Damaged || k.Version == cap.FitVersion || k.Status != ShipStatus.Afloat)
            return;
        cap.FitVersion = k.Version;
        var cls = own.Handling.Class;
        var fit = Fitness.Of(cls, k, ctx.World.Damage.Flood(cls).ReserveT);
        double D(DoctrineField f) => doctrine.Of(own.Entity, own.Side, f);
        var rule = new FallOutRule(D(DoctrineField.FallOutSpeedShare), D(DoctrineField.FallOutGunsShare), D(DoctrineField.FallOutListDeg),
            D(DoctrineField.FallOutWaterShare));
        var why = rule.Of(fit) switch
        {
            FallOutCause.Steering => CaptainReason.OutSteering,
            FallOutCause.Speed => CaptainReason.OutSpeed,
            FallOutCause.Guns => CaptainReason.OutGuns,
            FallOutCause.List => CaptainReason.OutList,
            FallOutCause.Water => CaptainReason.OutWater,
            _ => CaptainReason.None,
        };
        if (why == CaptainReason.None)
            return;
        if (groups.Of(own.Entity) != null)
            ctx.Issue(GroupSystem.Remove(own.Entity));   // out of the line
        SetLimit(ctx, ref cap, own, 0);
        SetClearance(ctx, ref cap, own.Entity, 0);
        (cap.Order, cap.Target, cap.Source, cap.Watch, cap.Asked, cap.Reason) = (CaptainOrder.Home, 0, OrderSource.Player, false, false, why);
        Recount();
    }

    /// <summary>Whether his ship's magazines are empty (docs/battle/command.md 5.1g): every chief mount out of the rounds
    /// it carries (a mount with none given never runs out) and no torpedo left. Then she leaves her group and goes home,
    /// as one fallen out does, her secondaries firing at will as she goes. Not while the player holds her.</summary>
    void OutOfRounds(in AiContext ctx, ref Captain cap, ShipState own)
    {
        if (cap.Held || cap.Order == CaptainOrder.Home)
            return;
        var cls = own.Handling.Class;
        var combat = ctx.Ships.Combat;
        int slot = ctx.Ships.SlotOf(own.Entity);
        bool any = false;
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            ref readonly var mr = ref cls.Mounts[m];
            if (mr.Battery < 0 || !cls.Batteries[mr.Battery].Chief)
                continue;
            if (mr.Rounds <= 0 || combat.Mount(slot, m).Rounds > 0)
                return;   // rounds left, or no limit given
            any = true;
        }
        if (!any)
            return;
        for (int l = 0; l < cls.Launchers.Length; l++)
        {
            var st = combat.Launcher(slot, l, cls.Launchers[l], ctx.NowMs);
            if (st.Loaded + st.Spare > 0)
                return;
        }
        if (groups.Of(own.Entity) != null)
            ctx.Issue(GroupSystem.Remove(own.Entity));   // out of the line
        SetLimit(ctx, ref cap, own, 0);
        SetClearance(ctx, ref cap, own.Entity, 0);
        (cap.Order, cap.Target, cap.Source, cap.Watch, cap.Asked, cap.Reason) = (CaptainOrder.Home, 0, OrderSource.Player, false, false, CaptainReason.OutAmmunition);
        Recount();
    }

    /// <summary>Out of the fight, each second: away from the nearest enemy his navy holds in sight within
    /// <see cref="SimSettings.FightCompareM"/>, else for his side's home (great circle), at all she can make; stopped
    /// within <see cref="SimSettings.HomeRadiusM"/> of it. With no home and nobody near he keeps his course.</summary>
    void HomeThink(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var s = ctx.Settings;
        double flank = own.Handling.Class.Motion.MaxSpeedKn;
        if (Nearest(ctx, own, s.FightCompareM, out var t) != 0)
        {
            var (course, kn) = Behaviours.Run(own, t);
            Give(ctx, own, Evade(ctx, own, slot, course, attackRun: false), kn);
            return;
        }
        if (homes[own.Side] is not { } home)
            return;
        if (Earth.Distance(own.Position, home) <= s.HomeRadiusM)
        {
            if (own.SpeedOrderKn != 0)
                Give(ctx, own, own.CourseOrderDeg, 0);
            cap.Reason = CaptainReason.Home;
            return;
        }
        Give(ctx, own, Earth.Bearing(own.Position, home), flank);
    }

    /// <summary>A torpedo seen coming at her (doctrine's TorpedoEvasion: comb): she turns parallel to its track, at the
    /// speed rung, until <see cref="SimSettings.TorpedoCombAfterS"/> after it's past (her behaviour waits); then a plain
    /// course order is given back, and a behaviour steers on.</summary>
    void Comb(in AiContext ctx, ref Captain cap, ShipState own)
    {
        long now = ctx.NowMs;
        if (cap.CombUntilMs != 0 && now >= cap.CombUntilMs)
        {
            cap.CombUntilMs = 0;
            if (cap.Reason == CaptainReason.Combing)
                cap.Reason = CaptainReason.None;
            if (cap.Order is CaptainOrder.Course or CaptainOrder.None)
                ctx.Helm.Course(own.Entity, cap.CombBackDeg);
            else if (cap.Order == CaptainOrder.Route)
                ctx.Helm.ResumeRoute(own.Entity);   // the comb's course took the helm off it for the moment
        }
        if ((TorpedoEvasion)doctrine.Of(own.Entity, own.Side, DoctrineField.TorpedoEvasion) != TorpedoEvasion.Comb
            || !torpedoWatch.Threat(ctx, own, out double comb, out double past))
            return;
        if (cap.CombUntilMs == 0)
            cap.CombBackDeg = own.CourseOrderDeg;
        (cap.CombDeg, cap.CombUntilMs) = (comb, now + (long)((past + ctx.Settings.TorpedoCombAfterS) * 1000));
        if (cap.Reason == CaptainReason.None)
            cap.Reason = CaptainReason.Combing;
        Give(ctx, own, comb, own.SpeedOrderKn);
    }

    /// <summary>Which ships are under fire, and from how near: every fighting ship's batteries' targets, once a think (a
    /// ship knows when she's shot at: the flashes, the splashes). Before the behaviours, from the targets the last pass
    /// set.</summary>
    void MarkUnderFire(in AiContext ctx)
    {
        var store = ctx.Ships;
        if (underFire.Length < store.Slots)
            underFire = new double[Math.Max(store.Slots, underFire.Length * 2)];
        if (anyUnderFire)
            Array.Clear(underFire);
        anyUnderFire = false;
        if (fighting == 0)
            return;
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            if (store.Combat.Engaged(slot) == 0)
                continue;
            var shooter = store[slot];
            for (int b = 0; b < shooter.Handling.Class.Batteries.Length; b++)
                if (store.Combat.Target(slot, b) is int t and > 0 && store.SlotOf(t) is int ts and >= 0)
                {
                    double r = Earth.Distance(shooter.Position, store[ts].Position);
                    if (underFire[ts] == 0 || r < underFire[ts])
                        underFire[ts] = r;
                    anyUnderFire = true;
                }
        }
    }

    /// <summary>The course a ship under fire steers for one she wants (docs/battle/command.md 5.1a; the evasion research's
    /// styles): doctrine's zigzag, legs of <see cref="DoctrineField.EvadeLegS"/> either side by
    /// <see cref="DoctrineField.EvadeDeg"/>, each ship on her own phase. A running ship evades fire from beyond
    /// <see cref="DoctrineField.EvadeBeyondM"/> only; a torpedo craft on her attack whatever the range.</summary>
    double Evade(in AiContext ctx, ShipState own, int slot, double course, bool attackRun)
    {
        double near = slot < underFire.Length ? underFire[slot] : 0;
        if (near == 0 || (Evasion)doctrine.Of(own.Entity, own.Side, DoctrineField.Evasion) != Evasion.Zigzag)
            return course;
        if (!attackRun && near < doctrine.Of(own.Entity, own.Side, DoctrineField.EvadeBeyondM))
            return course;
        long leg = Math.Max(1, (long)(doctrine.Of(own.Entity, own.Side, DoctrineField.EvadeLegS) * 1000));
        long phase = own.Entity * 7_919L % leg;   // not every ship on the same leg
        double swing = doctrine.Of(own.Entity, own.Side, DoctrineField.EvadeDeg);
        return ((ctx.NowMs + phase) / leg % 2 == 0 ? course + swing : course - swing + 360) % 360;
    }

    /// <summary>The nearest contact his navy holds in sight within a radius, as a sighting; 0 if none.</summary>
    int Nearest(in AiContext ctx, ShipState own, double radiusM, out Sighting t)
    {
        var p = own.Position;
        int best = -1;
        double bestD = double.MaxValue;
        foreach (int i in seen.Near(ctx.World, own.Side, p, radiusM))
        {
            double d = (seen.Position(own.Side, i) - p).LengthSquared;
            if (d < bestD)
                (best, bestD) = (i, d);
        }
        if (best < 0)
        {
            t = default;
            return 0;
        }
        var q = seen.Position(own.Side, best);
        var v = seen.Velocity(own.Side, best);
        double speed = v.Length;
        int entity = ctx.World.Sightings.Of(own.Side)![best].Entity;
        t = new Sighting(entity, q, speed > 1e-6 ? v * (1 / speed) : Earth.Forward(q, 0), speed, ctx.NowMs);
        return entity;
    }

    /// <summary>A class's heaviest chief battery (<see cref="BatteryRow.Chief"/>: its main, else its heaviest guns), -1 for none.</summary>
    static int ChiefBattery(ShipClass cls)
    {
        int best = -1;
        for (int b = 0; b < cls.Batteries.Length; b++)
            if (cls.Batteries[b].Chief && (best < 0 || cls.Batteries[b].CalibreMm > cls.Batteries[best].CalibreMm))
                best = b;
        return best;
    }
}
