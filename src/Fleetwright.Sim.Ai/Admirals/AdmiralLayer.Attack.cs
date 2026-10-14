using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>Where a flotilla's attack stands (docs/battle/command.md 5.1b).</summary>
public enum AttackPhase : byte
{
    None,

    /// <summary>Gaining a position ahead of the target's beam.</summary>
    Gain,

    /// <summary>Running in together, torpedoes held for the signal.</summary>
    Run,

    /// <summary>The signal made: every boat fires as her tubes bear, for <see cref="SimSettings.AttackFireS"/>.</summary>
    Fire,

    /// <summary>Fired, and turned away.</summary>
    Away,
}

/// <summary>The flotilla's attack (docs/battle/command.md 5.1b): a squadron of torpedo craft that's told to Attack, or
/// fights with doctrine's TorpedoAttack "together", attacks as one under its admiral, each boat's torpedoes held for his
/// signal. It gains a point <see cref="SimSettings.AttackGainDeg"/> off the target's bow, on its side of him, at
/// <see cref="SimSettings.AttackGainShare"/> of its torpedoes' fast range, at full speed; runs in from there (its guide
/// fighting the target, the boats on station about him); fires every boat's spread on one signal when the guide's run is
/// within <see cref="SimSettings.TorpedoFireShare"/> of the fast range; runs from the target for
/// <see cref="SimSettings.TorpedoTurnAwayMs"/>; and attacks again while any boat has torpedoes, then fights with its
/// guns. Past doctrine's AttackBreakOffShare of its boats gone from its line it's beaten off, and runs. A boat alone, or a
/// flotilla by doctrine's "alone", attacks as 4.6 has it, each captain on his own.</summary>
public sealed partial class AdmiralLayer
{
    /// <summary>Where a group's attack stands, and on whom.</summary>
    public (AttackPhase Phase, int Target) AttackOf(int groupId) =>
        Find(groupId) is int i and >= 0 ? (admirals[i].Attack, admirals[i].AttackTarget) : (AttackPhase.None, 0);

    bool AttackStep(in AiContext ctx, GroupSystem.Group g, Admiral a, int lead)
    {
        var ships = ctx.Ships;
        var guide = ships[lead];
        bool together = a.AttackOrdered || (TorpedoAttack)doctrine.OfGroup(g, guide.Side, DoctrineField.TorpedoAttack) == TorpedoAttack.Together;
        if (g.IsForce || a.Order != CaptainOrder.Fight || !together || !CaptainLayer.TorpedoCraft(guide.Handling.Class) || !AnyTorpedoes(ctx, g))
        {
            if (a.Attack != AttackPhase.None || a.AttackOrdered && a.Order == CaptainOrder.Fight)
                StopAttack(ctx, g, a);
            return false;
        }
        var s = ctx.Settings;
        long now = ctx.NowMs;
        var to = GuideUnit(g);
        var o = ctx.Orders;
        const OrderSource Up = OrderSource.Superior;

        if (a.Attack != AttackPhase.None && g.Line.Count < a.AttackShips * (1 - doctrine.OfGroup(g, guide.Side, DoctrineField.AttackBreakOffShare)) - 1e-9)
        {
            // beaten off: away from the target, the attack given up
            int from = a.AttackTarget;
            StopAttack(ctx, g, a);
            (a.Order, a.Target, a.AttackOrdered) = (CaptainOrder.Run, Unit.Ship(from).Id, false);
            ctx.Issue(o.Run(to, Unit.Ship(from), Up));
            return false;
        }

        for (int i = 0; i < g.Line.Count; i++)
            captains.HoldTorpedoes(g.Line[i], true);   // for the signal

        if (a.Attack is AttackPhase.None or AttackPhase.Gain && !ctx.Picture.TryGet(guide.Side, a.AttackTarget, out _))
        {
            // the target: the one ordered while it's held, else the biggest in sight, the nearest first
            int ordered = new Unit(a.Target).Entity;
            a.AttackTarget = ordered != 0 && ctx.Picture.TryGet(guide.Side, ordered, out _) ? ordered : Biggest(ctx, guide);
            if (a.AttackTarget == 0)
            {
                a.Attack = AttackPhase.None;
                return false;   // nothing to attack yet: the guide's Fight closes the nearest
            }
            if (a.Attack == AttackPhase.Gain)
                a.AttackMs = now;
        }
        if (!ctx.Picture.TryGet(guide.Side, a.AttackTarget, out var t))
        {
            a.Attack = AttackPhase.None;   // lost: chosen again
            return false;
        }
        var (range, speed) = CaptainLayer.FastSetting(guide.Handling.Class);
        var target = Unit.Ship(a.AttackTarget);
        var guideOrder = captains.Of(guide.Entity).Order;

        if (a.Attack == AttackPhase.None)
        {
            var starboard = Vec3d.Cross(t.Forward, t.Position.Normalized());
            (a.Attack, a.AttackMs, a.AttackShips) = (AttackPhase.Gain, now, g.Line.Count);
            a.AttackSide = Vec3d.Dot(guide.Position - t.Position, starboard) >= 0 ? 1 : -1;
        }
        // where the boats are on the target: how far round his bow, and the run their torpedoes would need to meet him
        var rel = guide.Position - t.Position;
        double r = rel.Length;
        double offBow = r > 1 ? Math.Acos(Math.Clamp(Vec3d.Dot(rel, t.Forward) / r, -1, 1)) * 180 / Math.PI : 0;
        var pic = ctx.World.Sightings.Of(guide.Side)!;
        var (q, v, _) = pic.EstimateAt(pic.IndexOf(a.AttackTarget), now, s);
        double run = TorpedoAim.Intercept(guide.Position, q, v, speed, out _, out double meet) ? meet : double.PositiveInfinity;
        double gain = s.AttackGainShare * range;
        switch (a.Attack)
        {
            case AttackPhase.Gain:
            {
                var p = t.Position;
                var f = Earth.Forward(p, Earth.Heading(t.Position, t.Forward) + a.AttackSide * s.AttackGainDeg);
                Earth.Advance(ref p, ref f, gain);
                if (offBow <= 90 && run <= gain || Earth.Distance(guide.Position, p) < 1_500 || now - a.AttackMs > s.AttackGainMaxMs)
                {
                    // ahead of his beam with him in reach of a run in (or there, or no more time): in together
                    (a.Attack, a.AttackMs) = (AttackPhase.Run, now);
                    goto case AttackPhase.Run;
                }
                double course = Earth.Bearing(guide.Position, p);
                if (guideOrder != CaptainOrder.Course || Math.Abs(Wrap180(course - guide.CourseOrderDeg)) > s.CaptainCourseDeadbandDeg)
                    ctx.Issue(o.Course(to, course, 0, Up));
                double flank = guide.Handling.Class.Motion.MaxSpeedKn;
                if (guide.SpeedOrderKn < flank - s.CaptainSpeedDeadbandKn && !(captains.SpeedOf(guide.Entity).AskedKn >= flank))
                    ctx.Issue(o.Speed(to, flank, Up));
                break;
            }
            case AttackPhase.Run:
            {
                bool inReach = run <= s.TorpedoFireShare * range;
                if (!inReach && offBow > 110)
                {
                    (a.Attack, a.AttackMs) = (AttackPhase.Gain, now);   // he's turned away: abaft his beam, a stern chase; ahead of him again first
                    break;
                }
                if (inReach)
                {
                    // the signal: turn together through 8 points, the beam tubes to bear and every track clear of the
                    // line (square to the line of sight now), away from his path; each boat fires as hers bear
                    var starboard = Vec3d.Cross(guide.Forward, guide.Position.Normalized());
                    (a.Attack, a.AttackMs) = (AttackPhase.Fire, now);
                    StartTurn(ctx, g, a, Vec3d.Dot(t.Forward, starboard) > 0 ? -90 : 90, rearFirst: false, keep: true);
                    return true;
                }
                if (now - a.AttackMs > s.AttackGainMaxMs)
                {
                    (a.Attack, a.AttackMs) = (AttackPhase.Away, now);   // never in reach: given up, away
                    ctx.Issue(o.Run(to, target, Up));
                    break;
                }
                // in together, bow on, on the course that meets him
                double kn = guide.Handling.Class.Motion.MaxSpeedKn;
                double toward = TorpedoAim.Intercept(guide.Position, q, v, kn * Handling.MsPerKnot, out double c, out _) ? c : Earth.Bearing(guide.Position, q);
                if (guideOrder != CaptainOrder.Course || Math.Abs(Wrap180(toward - guide.CourseOrderDeg)) > s.CaptainCourseDeadbandDeg)
                    ctx.Issue(o.Course(to, toward, 0, Up));
                if (guide.SpeedOrderKn < kn - s.CaptainSpeedDeadbandKn && !(captains.SpeedOf(guide.Entity).AskedKn >= kn))
                    ctx.Issue(o.Speed(to, kn, Up));
                break;
            }
            case AttackPhase.Fire:
                FireTogether(ctx, g, a);
                if (now - a.AttackMs >= (long)(s.AttackFireS * 1000))
                {
                    (a.Attack, a.AttackMs) = (AttackPhase.Away, now);
                    ctx.Issue(o.Run(to, target, Up));
                }
                break;
            case AttackPhase.Away:
                if (guideOrder != CaptainOrder.Run)
                    ctx.Issue(o.Run(to, target, Up));
                if (now - a.AttackMs >= s.TorpedoTurnAwayMs)
                    a.Attack = AttackPhase.None;   // and in again, from a new position, while there are torpedoes
                break;
        }
        return false;
    }

    /// <summary>The signal made: each boat of the line fires her spread at the target as her tubes bear, once (also while
    /// the flotilla is turning together, when the think gives no other orders).</summary>
    void FireTogether(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        if (a.Attack != AttackPhase.Fire)
            return;
        for (int i = 0; i < g.Line.Count; i++)
            captains.FireOnSignal(ctx, g.Line[i], a.AttackTarget, a.AttackMs);
    }

    /// <summary>The attack over (done, given up, or no longer the order): the boats' torpedoes their own again, and the
    /// guide handed his order afresh (Fight: the guns now).</summary>
    void StopAttack(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        (a.Attack, a.AttackTarget, a.AttackOrdered) = (AttackPhase.None, 0, false);
        for (int i = 0; i < g.Line.Count; i++)
            captains.HoldTorpedoes(g.Line[i], false);
        if (GuideUnit(g) is { Id: not 0 } to)
        {
            a.HandedTo = 0;
            HandOver(ctx, a, to);
        }
    }

    /// <summary>Whether any boat of the line still has torpedoes and launchers to fire them.</summary>
    /// <summary>Whether any boat of a group still has a torpedo in a launcher that works.</summary>
    internal static bool AnyTorpedoes(in AiContext ctx, GroupSystem.Group g)
    {
        for (int i = 0; i < g.Line.Count; i++)
            if (ctx.Ships.SlotOf(g.Line[i]) is int slot and >= 0 && CaptainLayer.TorpedoCraft(ctx, ctx.Ships[slot], slot))
                return true;
        return false;
    }

    /// <summary>The contact torpedoes are for: the biggest in sight within <see cref="SimSettings.FightCompareM"/>, the
    /// nearest of them first; 0 if none.</summary>
    int Biggest(in AiContext ctx, ShipState guide)
    {
        var pic = ctx.World.Sightings.Of(guide.Side);
        if (pic == null)
            return 0;
        int best = 0, bestRank = int.MaxValue;
        double bestD = double.MaxValue;
        foreach (int i in seen.Near(ctx.World, guide.Side, guide.Position, ctx.Settings.FightCompareM))
        {
            int rank = CaptainLayer.TorpedoRank(pic[i].Type);
            double d = (seen.Position(guide.Side, i) - guide.Position).Length;
            if (rank < bestRank || rank == bestRank && d < bestD)
                (best, bestRank, bestD) = (pic[i].Entity, rank, d);
        }
        return best;
    }
}
