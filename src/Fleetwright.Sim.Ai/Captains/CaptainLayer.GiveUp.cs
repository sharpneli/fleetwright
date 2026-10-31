using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The captain's call on his ship's end (docs/battle/striking.md SK2; research/simulation/striking-colours.md R1-R3),
/// at each think of a ship hurt and afloat, from what he knows of her exactly and what his navy sees:
/// <list type="bullet">
/// <item><b>Leave her</b> once his own reading of her water and list (a look every <see cref="SimSettings.GiveUpLookS"/>,
/// their trend over <see cref="SimSettings.GiveUpTrendS"/>) leaves less time than her men need to get off and
/// <see cref="SimSettings.AbandonMarginS"/>: abandoned, in any navy, an enemy near or not, struck or not, held by the player
/// or not (the water isn't an order he can overrule).</item>
/// <item><b>Give her up</b> once she's hopeless (she can't hurt, or she's sinking within <see cref="SimSettings.GiveUpSinkingS"/>),
/// can't get away, and a foe is in sight within <see cref="SimSettings.GiveUpEnemyM"/>; his call falls due a delay after
/// that (<see cref="SimSettings.GiveUpDelayS"/>, by his temper and a draw of his own), and the clock starts again if she
/// stops being so. Then he strikes if his draw is under his readiness (doctrine's <see cref="DoctrineField.StrikeColours"/>
/// moved by his temper, her men shaken, her sinking), else scuttles by doctrine's <see cref="DoctrineField.Scuttle"/>, else
/// fights on for good. Never a ship the player holds (decision 2: ordered by hand to fight on, she does).</item>
/// </list>
/// His draws are his own (the world's stream, by his ship), so a replay says the same; his temper's spread is for this
/// call only (the plan: every captain's temper drawn at load is a later choice).</summary>
public sealed partial class CaptainLayer
{
    /// <summary>Every ship hurt and afloat whose people haven't left her: her captain's call (a captain made for one with
    /// water aboard or one fighting).</summary>
    void EndPass(in AiContext ctx)
    {
        var store = ctx.Ships;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || store.Combat.Condition(slot) is not { Damaged: true, Status: ShipStatus.Afloat } k || k.Abandoned)
                continue;
            var own = store[slot];
            hurtAny |= k.WaterT > 0;   // her captain keeps thinking while she's wet (Hurt)
            int i = Find(own.Entity);
            if (i < 0 && k.WaterT <= 0 && encounters.Of(slot, own.Entity) == 0)
                continue;   // dry and out of the fight: nothing to judge, no captain made for it
            ref var cap = ref i >= 0 ? ref captains[i] : ref Get(own.Entity);
            Ending(ctx, ref cap, own, slot, k);
        }
    }

    void Ending(in AiContext ctx, ref Captain cap, ShipState own, int slot, ShipCondition k)
    {
        var s = ctx.Settings;
        long now = ctx.NowMs;
        var cls = own.Handling.Class;
        var flood = ctx.World.Damage.Flood(cls);
        if (!Look(ref cap, k, now, s) && !(cap.HopelessMs != 0 && now >= cap.CallMs && !k.IsGivenUp))
            return;   // judged at his looks, and when his call falls due
        double left = TimeLeftS(cap, k, flood.ReserveT, s);
        double men = Math.Max(flood.Complement - k.Killed, 0);
        if (left < s.AbandonLeadS + men * s.AbandonManS + s.AbandonMarginS)
        {
            ctx.Issue(DamageSystem.Abandon(own.Entity));
            Stand(ref cap);
            return;
        }
        if (k.IsGivenUp || cap.FightsOn || cap.Whose == Whose.Player || Conned(cap, own.Entity))
            return;
        bool sinking = left < s.GiveUpSinkingS;
        var fit = Fitness.Of(cls, k, flood.ReserveT);
        if (!(sinking || CantHurt(ctx, own, slot, fit, s)) || !Cornered(ctx, own, fit, s))
        {
            cap.HopelessMs = 0;   // the clock starts again
            return;
        }
        var r = ctx.World.Stream("give up").Derive((ulong)(uint)own.Entity);
        double temper = Math.Clamp(doctrine.Of(own.Entity, own.Side, DoctrineField.Aggression) + s.GiveUpTemperSpread * Gauss(ref r), -2, 2);
        double delayZ = Gauss(ref r), uStrike = r.NextDouble(), uScuttle = r.NextDouble();
        if (cap.HopelessMs == 0)
        {
            cap.HopelessMs = now;
            double delay = Math.Clamp(s.GiveUpDelayS * Math.Exp(s.GiveUpDelaySigma * delayZ) * Math.Pow(2, temper), s.GiveUpDelayMinS, s.GiveUpDelayMaxS);
            cap.CallMs = now + (long)(delay * 1000);
        }
        if (now < cap.CallMs)
            return;
        double strike = doctrine.Of(own.Entity, own.Side, DoctrineField.StrikeColours) - s.GiveUpTemperShift * temper +
                        s.GiveUpShakenShift * Shaken(cls, k) + (sinking ? s.GiveUpSinkingShift : 0);
        if (uStrike < strike)
            ctx.Issue(DamageSystem.StrikeColours(own.Entity));
        else if (uScuttle < doctrine.Of(own.Entity, own.Side, DoctrineField.Scuttle))
            ctx.Issue(DamageSystem.Scuttle(own.Entity));
        else
        {
            cap.FightsOn = true;   // Monmouth, Wiesbaden, Shark: till the water decides
            return;
        }
        Stand(ref cap);
    }

    /// <summary>Her captain stands her down: no orders from him again, her guns held.</summary>
    void Stand(ref Captain cap)
    {
        (cap.Order, cap.Target, cap.Fire, cap.FireTarget, cap.FireFrom, cap.Watch, cap.Asked) = (CaptainOrder.None, 0, FireOrder.Hold, 0, 0, false, false);
        Recount();
    }

    /// <summary>His look at her (every <see cref="SimSettings.GiveUpLookS"/>; whether he looked now), and her water's and
    /// list's trend: the rise over each window of <see cref="SimSettings.GiveUpTrendS"/>, the lesser of the last two. A hit's
    /// sudden water or list that then holds is no trend (one window has it, the next doesn't); water coming in steadily
    /// is.</summary>
    static bool Look(ref Captain cap, ShipCondition k, long now, SimSettings s)
    {
        if (cap.EndLookMs != 0 && now - cap.EndLookMs < s.GiveUpLookS * 1000)
            return false;
        cap.EndLookMs = now;
        double heel = Math.Abs(k.Attitude.HeelDeg);
        if (cap.EndWindowMs == 0)
        {
            (cap.EndWindowMs, cap.EndWaterT, cap.EndHeelDeg) = (now, k.WaterT, heel);
            return true;
        }
        double dt = (now - cap.EndWindowMs) / 1000.0;
        if (dt < s.GiveUpTrendS)
            return true;
        double water = (k.WaterT - cap.EndWaterT) / dt, list = (heel - cap.EndHeelDeg) / dt;
        (cap.WaterTrend, cap.HeelTrend) = (Math.Min(water, cap.WaterRate), Math.Min(list, cap.HeelRate));
        (cap.WaterRate, cap.HeelRate) = (water, list);
        (cap.EndWindowMs, cap.EndWaterT, cap.EndHeelDeg) = (now, k.WaterT, heel);
        return true;
    }

    /// <summary>The time she has by his trend, s: till her water reaches her reserve or her list
    /// <see cref="SimSettings.CapsizeHeelDeg"/>, the sooner (infinite while neither grows).</summary>
    static double TimeLeftS(in Captain cap, ShipCondition k, double reserveT, SimSettings s)
    {
        double left = double.PositiveInfinity;
        if (cap.WaterTrend > 0)
            left = Math.Max(reserveT - k.WaterT, 0) / cap.WaterTrend;
        if (cap.HeelTrend > 0)
            left = Math.Min(left, Math.Max(s.CapsizeHeelDeg - Math.Abs(k.Attitude.HeelDeg), 0) / cap.HeelTrend);
        return left;
    }

    /// <summary>Whether she can't hurt the enemy: no main battery at all (a merchant, docs/battle/aims.md A4: her light
    /// guns don't make her a match for a warship), or her chief guns' lasting share at or under
    /// <see cref="SimSettings.GiveUpGunsShare"/>, or her chief magazines empty; and no torpedo ready in a working tube.</summary>
    static bool CantHurt(in AiContext ctx, ShipState own, int slot, in Fitness fit, SimSettings s)
    {
        var cls = own.Handling.Class;
        var combat = ctx.Ships.Combat;
        if (Sightings.TypeOf(cls) != ShipType.Merchant && fit.MainGunsShare > s.GiveUpGunsShare && !Spent(cls, combat, slot))
            return false;
        var k = combat.Condition(slot);
        for (int l = 0; l < cls.Launchers.Length; l++)
            if (combat.Launcher(slot, l, cls.Launchers[l], ctx.NowMs).Loaded > 0 && k.LauncherWorks(l))
                return false;
        return true;
    }

    /// <summary>Whether every chief mount is out of the rounds it carries (a mount with none given never runs out).</summary>
    static bool Spent(ShipClass cls, CombatStore combat, int slot)
    {
        bool any = false;
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            ref readonly var mr = ref cls.Mounts[m];
            if (mr.Battery < 0 || !cls.Batteries[mr.Battery].Chief)
                continue;
            if (mr.Rounds <= 0 || combat.Mount(slot, m).Rounds > 0)
                return false;
            any = true;
        }
        return any;
    }

    /// <summary>Whether she can't get away from a foe in reach: one her navy holds in sight within
    /// <see cref="SimSettings.GiveUpEnemyM"/>, and she's stopped (under <see cref="SimSettings.DoneForKn"/>), her steering
    /// gone, or slower by what she can make than the fastest of them as seen.</summary>
    bool Cornered(in AiContext ctx, ShipState own, in Fitness fit, SimSettings s)
    {
        var pic = ctx.World.Sightings.Of(own.Side);
        if (pic == null)
            return false;
        double best = fit.SpeedShare * own.Handling.Class.Motion.MaxSpeedKn, fastest = -1;
        foreach (int i in seen.Near(ctx.World, own.Side, own.Position, s.GiveUpEnemyM))
        {
            var q = seen.Position(own.Side, i);
            if (!pic[i].InSight || pic[i].Out || !Opens(ctx, own, pic[i], q))
                continue;
            fastest = Math.Max(fastest, seen.Velocity(own.Side, i).Length / Handling.MsPerKnot);
        }
        if (fastest < 0)
            return false;
        return best <= s.DoneForKn || fit.Steering is Steering.Jammed or Steering.Lost || best < fastest - s.MatchupSpeedMarginKn;
    }

    /// <summary>The share of her men at their stations shaken or frozen (docs/battle/crew.md N3b).</summary>
    static double Shaken(ShipClass cls, ShipCondition k)
    {
        int out_ = 0, crew = 0;
        for (int n = 0; n < k.StationGroup.Length; n++)
        {
            int g = k.StationGroup[n];
            crew += g < cls.Components.Length ? cls.Components[g].BattleCrew : cls.Cells[g - cls.Components.Length].BattleCrew;
            out_ += k.Shaken[n] + k.Frozen[n];
        }
        return crew > 0 ? (double)out_ / crew : 0;
    }

    static double Gauss(ref RandomStream r)
    {
        double u1 = Math.Max(r.NextDouble(), 1e-300), u2 = r.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
