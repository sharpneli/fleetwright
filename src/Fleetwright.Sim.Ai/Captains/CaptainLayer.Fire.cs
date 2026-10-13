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
/// of the range table's): one that bears first, then (secondaries) the smallest type, then the nearest. It keeps its
/// target until it's unseen for <see cref="SimSettings.FireLostMs"/> or past <see cref="SimSettings.FireKeepShare"/> of its
/// reach, or another bears where it doesn't,
/// is smaller (secondaries), or is nearer than <see cref="SimSettings.FireSwitchShare"/> of its range: no flicking
/// between targets. With nothing to shoot at it ceases fire, so the ship can go back on rails.</item>
/// <item><b>Engage:</b> the main batteries on the contact while the navy holds it (then at will again); the secondaries
/// pick their own. <b>Hold fire:</b> every battery ceases.</item>
/// <item><b>Who gave it:</b> the player's order to the ship, or an admiral's to his tree (its group kept with it). An
/// admiral's order he hands down again (a redistribution) only reaches the ships still holding his; a fresh one, from a
/// group just given a fire order, reaches all of them.</item>
/// <item><b>Fight:</b> a behaviour, as follow and station are: close to the band, hold it with the main battery bearing,
/// turn away when outgunned (<see cref="Behaviours.Fight"/>).</item>
/// </list>
/// Guns are silent in the coarse tier (4.9): a coarse ship keeps its batteries' targets, and they fire again on the fine
/// tick.</summary>
public sealed partial class CaptainLayer
{
    readonly List<int> fighters = new(64);
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

    /// <summary>Each fighting ship's batteries put on their targets (and a ship alone's standing order taken up).</summary>
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
        foreach (int slot in fighters)
        {
            var own = store[slot];
            if (own.Tier == ClockTier.Fine)
            {
                ref var cap = ref Get(own.Entity);
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

    /// <summary>A ship alone with a standing order to fight takes it up when it first meets an enemy in sight in an
    /// encounter; it's armed again once it's out of encounters.</summary>
    void TakeUpStanding(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        if (!cap.Standing)
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

    // a ship's candidates, found once for all its batteries: each contact in sight within its widest reach, its entity,
    // range (m), bearing from ahead and the rank of its type for a secondary battery
    int[] candEntity = new int[32], candRank = new int[32];
    double[] candRange = new double[32], candBearing = new double[32];
    int candCount;
    readonly Dictionary<ShipClass, double[]> reaches = [];

    /// <summary>His ship's batteries on what his fire order wants.</summary>
    void Aim(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        var pic = ctx.World.Sightings.Of(own.Side);
        if (cap.Fire == FireOrder.Engage && (pic == null || pic.IndexOf(cap.FireTarget) < 0))
            (cap.Fire, cap.FireTarget) = (FireOrder.AtWill, 0);   // gone from the picture: at will again
        var reach = Reaches(ctx, cls);
        candCount = -1;   // found when a battery first needs them
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            int current = ctx.Ships.Combat.Target(slot, b);
            int want = cap.Fire switch
            {
                FireOrder.Hold => 0,
                FireOrder.Engage when cls.Batteries[b].Kind == Battery.Main => cap.FireTarget,
                _ => Pick(ctx, pic, own, cls, b, reach[b], reach[^1], current),
            };
            if (want != current)
                ctx.Issue(want == 0 ? GunnerySystem.Cease(own.Entity, b) : GunnerySystem.Engage(own.Entity, b, want));
        }
    }

    /// <summary>Each battery's reach for fire at will (<see cref="SimSettings.FireReachShare"/> of the range table's), and
    /// the widest last: worked out once a class.</summary>
    double[] Reaches(in AiContext ctx, ShipClass cls)
    {
        if (reaches.TryGetValue(cls, out var r))
            return r;
        r = new double[cls.Batteries.Length + 1];
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            r[b] = ctx.World.Gunnery.ReachM(cls, b) * ctx.Settings.FireReachShare;
            r[^1] = Math.Max(r[^1], r[b]);
        }
        return reaches[cls] = r;
    }

    /// <summary>A battery's target at will: the current one while it's held and in reach, unless another is better by
    /// bearing, by size (secondaries) or by much in range; else the best in sight; 0 for none.</summary>
    int Pick(in AiContext ctx, Picture? pic, ShipState own, ShipClass cls, int b, double reach, double widest, int current)
    {
        if (pic == null)
            return 0;
        var s = ctx.Settings;
        long now = ctx.NowMs;
        bool secondary = cls.Batteries[b].Kind != Battery.Main;
        var p = own.Position;
        if (candCount < 0)
            Candidates(ctx, pic, own, widest);

        int ci = current != 0 ? pic.IndexOf(current) : -1;
        bool keep = ci >= 0 && now - pic[ci].SeenMs <= s.FireLostMs;
        double curRange = 0;
        bool curBears = false;
        int curRank = 0;
        if (keep)
        {
            var q = pic.EstimateAt(ci, now, s).Position;
            curRange = (q - p).Length;
            keep = curRange <= reach * s.FireKeepShare / s.FireReachShare;   // held a little further than taken: no flicking at the edge
            curBears = Bears(cls, b, Earth.Bearing(p, q) - own.HeadingDeg);
            curRank = secondary ? Rank(pic[ci].Type) : 0;
        }

        int best = 0, bestRank = int.MaxValue;
        bool bestBears = false;
        double bestRange = double.MaxValue;
        for (int k = 0; k < candCount; k++)
        {
            double range = candRange[k];
            if (range > reach)
                continue;
            bool bears = Bears(cls, b, candBearing[k]);
            int rank = secondary ? candRank[k] : 0;
            if (bears != bestBears ? bears : rank != bestRank ? rank < bestRank : range < bestRange)
                (best, bestBears, bestRank, bestRange) = (candEntity[k], bears, rank, range);
        }
        if (!keep)
            return best;
        if (best == 0 || best == current)
            return current;
        if (bestBears != curBears)
            return bestBears ? best : current;
        if (bestRank != curRank)
            return bestRank < curRank ? best : current;
        return bestRange < s.FireSwitchShare * curRange ? best : current;
    }

    /// <summary>The contacts his navy holds in sight within his widest reach: range, bearing from ahead, type's rank.</summary>
    void Candidates(in AiContext ctx, Picture pic, ShipState own, double widest)
    {
        var p = own.Position;
        double heading = own.HeadingDeg;
        var near = seen.Near(ctx.World, own.Side, p, widest);
        if (candEntity.Length < near.Length)
        {
            int n = Math.Max(near.Length, candEntity.Length * 2);
            (candEntity, candRank, candRange, candBearing) = (new int[n], new int[n], new double[n], new double[n]);
        }
        candCount = 0;
        foreach (int i in near)
        {
            var q = seen.Position(own.Side, i);
            (candEntity[candCount], candRange[candCount]) = (pic[i].Entity, (q - p).Length);
            (candBearing[candCount], candRank[candCount]) = (Earth.Bearing(p, q) - heading, Rank(pic[i].Type));
            candCount++;
        }
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

    /// <summary>His Fight, each second: the enemy (the one ordered, else the main battery's target, else the nearest in
    /// sight); away from him if his navy sees far heavier guns than his own unit's, else on the band; nothing while
    /// there's nobody (he keeps what's ordered).</summary>
    void FightThink(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var s = ctx.Settings;
        var cls = own.Handling.Class;
        int main = MainBattery(cls);
        double reach = main >= 0 ? ctx.World.Gunnery.ReachM(cls, main) : s.ShadowMinM;
        double band = cap.DistanceM > 0 ? cap.DistanceM : s.FightRangeShare * reach;

        int enemy = new Unit(cap.Target).ShipOf(groups);
        if (enemy == 0 || !ctx.Picture.TryGet(own.Side, enemy, out var t))
        {
            enemy = main >= 0 ? ctx.Ships.Combat.Target(slot, main) : 0;
            if (enemy == 0 || !ctx.Picture.TryGet(own.Side, enemy, out t))
                enemy = Nearest(ctx, own, s.FightCompareM, out t);
        }
        if (enemy == 0)
            return;
        if (enemy != cap.FightEnemy)
            (cap.FightEnemy, cap.FightSide) = (enemy, 0);

        var (theirs, ours) = Weigh(ctx, own);
        double ratio = s.FightOutgunnedRatio;
        cap.FightAway = cap.FightAway ? theirs > ours * ratio * s.FightRallyShare : theirs > ours * ratio;
        var (course, kn) = cap.FightAway ? Behaviours.Run(own, t) : Behaviours.Fight(own, t, band, s.FightBandShare, s.FightCloseDeg, ref cap.FightSide);
        if (cap.LimitKn > 0)
            kn = Math.Min(kn, cap.LimitKn);
        Give(ctx, own, course, kn);
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

    /// <summary>The heavy guns (12" guns' worth) his navy sees within <see cref="SimSettings.FightCompareM"/>, each
    /// contact as the class it's taken for or by its type, and his own unit's (his group's line, or his ship).</summary>
    (double Theirs, double Ours) Weigh(in AiContext ctx, ShipState own)
    {
        var s = ctx.Settings;
        var pic = ctx.World.Sightings.Of(own.Side);
        var registry = ctx.World.Ships.Registry;
        double theirs = 0, ours = 0;
        if (pic != null)
            foreach (int i in seen.Near(ctx.World, own.Side, own.Position, s.FightCompareM))
            {
                ref readonly var c = ref pic[i];
                theirs += c.Level >= ContactLevel.Class && c.TakenFor >= 0 && registry.Get(c.TakenFor) is { } taken
                    ? HeavyGuns(taken)
                    : s.FightTypeGuns[Math.Min((int)c.Type, s.FightTypeGuns.Length - 1)];
            }
        if (groups.UnitOf(own.Entity) is { } unit)
        {
            groups.LineOffsets(unit, unitLine);
            foreach (var (ship, _, _) in unitLine)
                if (ctx.Ships.SlotOf(ship) is int m and >= 0)
                    ours += HeavyGuns(ctx.Ships[m].Handling.Class);
        }
        else
            ours = HeavyGuns(own.Handling.Class);
        return (theirs, ours);
    }

    /// <summary>A class's main batteries in 12" guns' worth: guns × (calibre / 305 mm)³.</summary>
    static double HeavyGuns(ShipClass cls)
    {
        double w = 0;
        foreach (ref readonly var b in cls.Batteries)
            if (b.Kind == Battery.Main)
                w += b.Guns * Math.Pow(b.CalibreMm / 305, 3);
        return w;
    }

    /// <summary>A class's main battery (the heaviest gun of its main batteries), -1 for none.</summary>
    static int MainBattery(ShipClass cls)
    {
        int best = -1;
        for (int b = 0; b < cls.Batteries.Length; b++)
            if (cls.Batteries[b].Kind == Battery.Main && (best < 0 || cls.Batteries[b].CalibreMm > cls.Batteries[best].CalibreMm))
                best = b;
        return best;
    }
}
