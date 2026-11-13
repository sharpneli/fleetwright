using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The rearguard (docs/battle/balance.md BL5; the user, 2026-11-10: "with running perhaps a captain or two should
/// salute, thank for the opportunity to serve and serve as a rearguard, allowing the rest of the formation to move away while
/// giving the ultimate sacrifice themselves"; 2026-11-11: only when it buys time). An outmatched force runs whatever its
/// speed; once the pursuit closes on the ships it covers, an escort's captain may stay behind and fight the pursuers:
/// Samar, Acasta and Ardent, the Jervis Bay.
/// <list type="bullet">
/// <item><b>When:</b> judged at each think of the running force's head (the commander while his side declines, an admiral
/// while his unit is turned away), and only while it buys time: a pursuer faster than the slowest ship covered is inside
/// the enemy's fighting range (its band) of one of them. A force getting away by its speed leaves nobody (the first build
/// stayed at the turn: 17 of 18 lost at the Dogger Bank, for nothing).</item>
/// <item><b>Who:</b> the force's escorts: with charges (merchants, or ships its side's aim is about: a convoy) every warship;
/// else the ships of no more than half its heaviest warship's tonnage, covering the heavier. The boldest, torpedo carriers
/// first at equal temper. A ship too slow to get away isn't a volunteer; she's left (Blücher).</item>
/// <item><b>Whether:</b> covering what her side's aim is about is her duty: she stays (the Jervis Bay, Rawalpindi).
/// Covering warships is courage: one draw a run (keyed to the force and the moment it turned to run, so judging it each
/// think doesn't make it near-certain), <see cref="SimSettings.RearguardChance"/> times e^(gain x the boldest escort's
/// temper). Someone was brave that day, or wasn't. One stays a run at most.</item>
/// <item><b>What:</b> out of her line, her own call: she fights the nearest pursuer, presses a torpedo attack while she has
/// torpedoes, makes smoke once under fire, and doesn't fall out.</item>
/// <item><b>Until:</b> the ship she covers is clear of the enemy (none she holds within
/// <see cref="SimSettings.FightCompareM"/> of her) or gone; then she goes home if she still can.</item>
/// </list>
/// Judged on the sim thread (the commander and the admirals think serially, docs/battle/workers.md W14) and handed to the
/// captain as a command.</summary>
public sealed partial class CaptainLayer
{
    readonly List<int> rgShips = new(32);

    /// <summary>Whether a ship's side holds to its Reach (doctrine's <see cref="DoctrineField.HoldReach"/>, its pursued aim a
    /// Reach still open): her force doesn't turn away from an enemy, it covers its charges (balance.md BL5).</summary>
    public bool HoldsReach(int entity, byte side) =>
        Book?.ReachOpen(side) == true && doctrine.Of(entity, side, DoctrineField.HoldReach) >= 0.5;

    /// <summary>Whether his ship is her captain's own call now, not her superiors': fallen out and home, or a rearguard.</summary>
    public bool OwnCall(int entity) => Find(entity) is int i and >= 0 &&
                                       (captains[i].Order == CaptainOrder.Home || captains[i].Reason == CaptainReason.Rearguard);

    /// <summary>A unit running since <paramref name="runMs"/> from an enemy chasing at <paramref name="chaseKn"/> who fights
    /// at <paramref name="bandM"/>: whether one of its escorts stays as its rearguard now
    /// (<see cref="JudgeRearguard(in AiContext, List{int}, double, double, long)"/>).</summary>
    internal void JudgeRearguard(in AiContext ctx, GroupSystem.Group unit, double chaseKn, double bandM, long runMs)
    {
        rgShips.Clear();
        LineShips(unit, rgShips);
        JudgeRearguard(ctx, rgShips, chaseKn, bandM, runMs);
    }

    /// <summary>A force running since <paramref name="runMs"/> (<paramref name="force"/> its ships in line: a unit's, or every
    /// unit in touch as a side declines, its flotillas with its heavy ships) from an enemy chasing at
    /// <paramref name="chaseKn"/> who fights at <paramref name="bandM"/>: whether one of its escorts stays as its rearguard
    /// now (<see cref="CaptainLayer"/>'s rearguard summary).</summary>
    internal void JudgeRearguard(in AiContext ctx, List<int> force, double chaseKn, double bandM, long runMs)
    {
        var s = ctx.Settings;
        if (force.Count < 2 || bandM <= 0 || chaseKn <= 0 || chaseKn >= double.MaxValue || double.IsInfinity(chaseKn))
            return;
        // what's covered: the force's charges (merchants, and the ships its side's aim is about), else its heavy ships
        // (more than half its heaviest warship's tonnage); the escorts are its other warships
        double heaviest = 0, chargeT = 0;
        int cover = 0, charge = 0;
        bool duty = false;
        foreach (int e in force)
        {
            if (ctx.Ships.SlotOf(e) is not (int k and >= 0))
                continue;
            var sh = ctx.Ships[k];
            double t = sh.Handling.Class.Motion.FullDisplacementT;
            if (Charge(sh, out bool aim))
            {
                duty |= aim;
                if (t > chargeT)
                    (chargeT, charge) = (t, e);
            }
            else if (t > heaviest)
                (heaviest, cover) = (t, e);
        }
        bool charges = charge != 0;
        if (charges)
            cover = charge;
        for (int i = 0; i < count; i++)
            if (captains[i].Cover != 0 && (captains[i].Cover == cover || force.Contains(captains[i].Cover)))
                return;   // one stays for this force already
        // does it buy time: a pursuer faster than the slowest covered, inside his fighting range of one of them?
        double slowest = double.MaxValue;
        bool caught = false;
        foreach (int e in force)
            if (ctx.Ships.SlotOf(e) is int k and >= 0 && Covered(ctx.Ships[k], charges, heaviest))
            {
                slowest = Math.Min(slowest, ctx.Ships[k].Handling.Class.Motion.MaxSpeedKn);
                caught |= Nearest(ctx, ctx.Ships[k], bandM, out _) != 0;
            }
        if (!caught || chaseKn <= slowest)
            return;
        // the boldest escort, torpedo carriers first at equal temper; ties by entity, so every run asks in one order
        int pick = 0;
        double temper = double.NegativeInfinity;
        bool torpedoes = false;
        foreach (int e in force)
        {
            if (ctx.Ships.SlotOf(e) is not (int k and >= 0) || e == cover)
                continue;
            var own = ctx.Ships[k];
            if (Covered(own, charges, heaviest) || Charge(own, out _) || ctx.Ships.Combat.Condition(k) is not { Status: ShipStatus.Afloat, IsGivenUp: false })
                continue;
            if (Find(e) is int ci and >= 0 && (captains[ci].Whose == Whose.Player || captains[ci].Order == CaptainOrder.Home || captains[ci].Conn))
                continue;   // the player's, or out of the fight already
            double a = doctrine.Of(e, own.Side, DoctrineField.Aggression);
            bool t = TorpedoesLeft(ctx, own, k);
            if (a > temper || a == temper && (t && !torpedoes || t == torpedoes && e < pick))
                (pick, temper, torpedoes) = (e, a, t);
        }
        if (pick == 0)
            return;
        // covering what the side's aim is about is her duty (the Jervis Bay); else one draw a run, the same each think of it
        double p = duty ? 1 : Math.Min(1, s.RearguardChance * Math.Exp(s.RearguardTemperGain * temper));
        if (ctx.World.Stream("rearguard").Derive((ulong)runMs).Derive((ulong)(uint)cover).NextDouble() < p)
            ctx.Issue(ctx.Orders.Rearguard(pick, cover));
    }

    /// <summary>Whether a ship is covered by her force's escorts: one of its charges if it has any, else one of its heavy ships.</summary>
    bool Covered(ShipState sh, bool charges, double heaviest) =>
        charges ? Charge(sh, out _) : sh.Handling.Class.Motion.FullDisplacementT > heaviest / 2;

    /// <summary>Whether a ship is one her force covers whatever its tonnage (<paramref name="aim"/>: one her side's aim is about,
    /// reaching for it): a merchant, or one named in her side's pursued Reach.</summary>
    bool Charge(ShipState sh, out bool aim)
    {
        aim = Book?.Reaching(sh.Side, sh.Entity) == true;
        return aim || Sightings.TypeOf(sh.Handling.Class) == ShipType.Merchant;
    }

    /// <summary>A group's ships in line, down its tree.</summary>
    internal void LineShips(GroupSystem.Group g, List<int> into)
    {
        if (!g.IsForce)
        {
            into.AddRange(g.Line);
            return;
        }
        foreach (int sub in g.Line)
            if (groups.Get(sub) is { } sg)
                LineShips(sg, into);
    }

    /// <summary>His ship stays behind as a rearguard, covering <paramref name="cover"/> (<see cref="AiCommand.Rearguard"/>):
    /// out of her line, her superior's limits gone, Fight on the nearest pursuer, torpedoes pressed home, firing at will.
    /// Not if the player holds her or she's already out of the fight.</summary>
    internal void Rearguard(in AiContext ctx, int entity, int cover)
    {
        int slot = ctx.Ships.SlotOf(entity);
        if (slot < 0 || ctx.Ships.Combat.Condition(slot) is not { Status: ShipStatus.Afloat, IsGivenUp: false })
            return;
        ref var cap = ref Get(entity);
        if (cap.Whose == Whose.Player || cap.Conn || cap.Order == CaptainOrder.Home || cap.Reason == CaptainReason.Rearguard)
            return;
        if (groups.Of(entity) != null)
            ctx.Issue(GroupSystem.Remove(entity));
        var own = ctx.Ships[slot];
        SetLimit(ctx, ref cap, own, 0);
        SetClearance(ctx, ref cap, entity, 0);
        (cap.Order, cap.Target, cap.Whose, cap.Watch, cap.Asked, cap.Reason, cap.Cover) =
            (CaptainOrder.Fight, 0, Whose.Own, false, false, CaptainReason.Rearguard, cover);
        (cap.DistanceM, cap.AbeamM, cap.FightSide, cap.FightEnemy, cap.FightAway, cap.AttackOrdered, cap.FleeMs) = (0, 0, 0, 0, false, true, 0);
        if (cap.Fire != FireOrder.Hold || cap.FireFrom != 0)
            (cap.Fire, cap.FireTarget, cap.FireFrom) = (FireOrder.AtWill, 0, 0);   // her admiral's fire order stays with his line
        Recount();
    }

    /// <summary>A rearguard's own watch, at each Fight think: once the ship she covers is gone or clear of the enemy (none her
    /// navy holds within <see cref="SimSettings.FightCompareM"/> of her), she's done her part and goes home if she can.
    /// True when she has.</summary>
    bool RearguardDone(in AiContext ctx, ref Captain cap, ShipState own)
    {
        int cs = ctx.Ships.SlotOf(cap.Cover);
        bool clear = cs < 0 || ctx.Ships.Combat.Condition(cs).Status != ShipStatus.Afloat || Nearest(ctx, ctx.Ships[cs], ctx.Settings.FightCompareM, out _) == 0;
        if (!clear)
            return false;
        cap.Cover = 0;
        GoHome(ctx, ref cap, own, CaptainReason.Home);
        return true;
    }
}
