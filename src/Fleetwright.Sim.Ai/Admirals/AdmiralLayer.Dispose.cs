using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>A force's disposition (docs/battle/admirals-orders.md O7, the chain of command): its admiral keeps his main
/// body in his line (his members of the strongest role in it: the battle line, else scouting), carrying whatever the
/// force is told; the rest attend it by their roles (<see cref="Dispositions.Attend"/>): his scouts scout ahead of the
/// force, spread off its bow, and his flotillas and screens screen it, each a task he gives as its commander
/// (<see cref="OrderSource.Commander"/>); his auxiliaries keep their place in his line. A force with no battle line or
/// scouts (flotillas and screens only) keeps together. His flotillas he sends in to attack himself (O7b), by the rule
/// the side's commander sends in his own (<see cref="FlotillaAttacks"/>): by day on an enemy line his main body is
/// engaging, by night by doctrine, once to cover his side breaking off, as his side's commander sees the battle; the side's
/// commander never reaches into a force for them.
/// <para>Every <see cref="SimSettings.ForcePeriodMs"/>, with doctrine's Dispose, not while deploying nor on the conn.
/// A member the player holds by hand, or on a task someone else gave it (the side's commander sending a flotilla in, the
/// player's own), is left alone; one back with no task (its attack over, rejoined) is given its own again; one whose
/// role now puts it in the main body is called back into the line. Each force disposes its own members, so a fleet of
/// forces is disposed level by level.</para></summary>
public sealed partial class AdmiralLayer
{
    /// <summary>What each unit is for (docs/battle/admirals-orders.md O6; set by the AI system).</summary>
    internal Roles Roles { get; set; } = null!;

    /// <summary>When a flotilla is sent in, and how the side's commander sees the battle (set by the AI system).</summary>
    internal FlotillaAttacks Attacks { get; set; } = null!;
    internal Commanders.CommanderLayer? Commanders { get; set; }

    /// <summary>Where a fast wing goes in battle (set by the AI system).</summary>
    internal FastWing? Wings { get; set; }

    readonly MatchupSide wingRest = new(), wingSide = new();

    void DisposeStep(in AiContext ctx, GroupSystem.Group g, Admiral a, int lead)
    {
        if (!g.IsForce || a.Conn || g.Members.Count < 2 || Tasks == null || ctx.NowMs - a.DisposedMs < ctx.Settings.ForcePeriodMs ||
            doctrine.OfGroup(g, ctx.Ships[lead].Side, DoctrineField.Dispose) == 0)
            return;
        a.DisposedMs = ctx.NowMs;

        // the main body's role: the battle line if he has one, else his scouts
        var body = UnitRole.None;
        for (int i = 0; i < g.Members.Count && body != UnitRole.Heavy; i++)
            body = Roles.Of(Unit.Group(g.Members[i])) switch
            {
                UnitRole.Heavy => UnitRole.Heavy,
                UnitRole.Scout => UnitRole.Scout,
                _ => body,
            };
        if (body == UnitRole.None)
            return;   // flotillas and screens only: they keep together

        var force = Unit.Group(g.GroupId);
        byte side = ctx.Ships[lead].Side;
        bool battle = Wings != null && Commanders is { } cm && cm.Commands(Unit.Group(groups.TopOf(g).GroupId), side, ctx.World.PlayerSide) &&
                      cm.PhaseOf(side).Phase is Sim.Ai.Commanders.CommanderPhase.Contact or Sim.Ai.Commanders.CommanderPhase.Battle;
        if (battle)
        {
            wingRest.Clear();
            for (int i = 0; i < g.Members.Count; i++)
                if (Roles.Of(Unit.Group(g.Members[i])) is UnitRole.Heavy or UnitRole.Screen && groups.Get(g.Members[i]) is { } sub)
                    Judge.AddMembers(ctx.World, sub, wingRest);
        }
        int scouts = 0;
        for (int i = 0; i < g.Members.Count; i++)
        {
            int m = g.Members[i];
            var u = Unit.Group(m);
            var role = Roles.Of(u);
            bool attends = role != body && Dispositions.Attends(role);
            int scout = attends && role == UnitRole.Scout ? scouts++ : 0;
            if (Find(m) is int mi and >= 0 && admirals[mi].Whose == Whose.Player)
                continue;   // the player's
            bool live = Tasks.OnTask(u), mine = live && Gave(a, m) == Tasks.StartOf(u);
            if (live && !mine)
                continue;   // someone else's task: his commander's, or the player's
            if (battle && attends && role == UnitRole.Scout && Roles.IsFastWing(u) && Wing(ctx, a, m, side))
                continue;   // his fast wing where it does the most (AL5)
            if (!attends)
            {
                if (mine)
                    Give(ctx, a, m, ctx.Orders.Join(u, force, OrderSource.Commander));   // its role is the body's now: back into the line
                continue;
            }
            if (mine && Tasks.Of(u).Kind == TaskKind.Attack)
                continue;   // in on the attack he sent it on
            if (role == UnitRole.Flotilla && SendIn(ctx, a, m, ctx.Ships[lead].Side))
                continue;
            if (mine && Tasks.Of(u).Kind == Dispositions.KindFor(role) && Tasks.TargetOf(u) == force.Id)
                continue;   // at it already
            Give(ctx, a, m, Dispositions.Attend(ctx.Orders, role, u, force, scout, OrderSource.Commander));
        }
    }

    /// <summary>A fast wing of his force (docs/battle/allocation.md AL5, <see cref="FastWing"/>), while his side holds the
    /// enemy and accepts battle, against the line of his force it fights beside (his members of the line and the screen):
    /// closing the ship chosen, and fighting her once she's in sight within <see cref="SimSettings.FightCompareM"/>. True
    /// if it had a choice.</summary>
    bool Wing(in AiContext ctx, Admiral a, int member, byte side)
    {
        var u = Unit.Group(member);
        if (groups.Get(member) is not { } wg || ctx.Ships.SlotOf(groups.GuideShip(wg)) is not (int slot and >= 0))
            return false;
        wingSide.Clear();
        Judge.AddMembers(ctx.World, wg, wingSide);
        var (fight, target, _) = Of(member);
        int on = Tasks!.Of(u) is (TaskKind.Intercept, TaskState.OnTask or TaskState.Engaged or TaskState.InContact, _) ? Tasks.TargetOf(u)
            : fight == CaptainOrder.Fight && target != 0 ? new Unit(target).ShipOf(groups) : 0;
        var w = Wings!.Choose(ctx, side, wingSide, wingRest, Wings.GroupOf(ctx, side, on));
        if (w.Choice == WingChoice.None)
            return false;
        if (!(Tasks.Of(u).Kind == TaskKind.Intercept && Tasks.TargetOf(u) == w.Target))
            Give(ctx, a, member, ctx.Orders.Intercept(u, w.Target, 0, OrderSource.Commander));
        var pic = ctx.World.Sightings.Of(side)!;
        if (pic.IndexOf(w.Target) is int ti and >= 0 && pic[ti].InSight &&
            (pic.EstimateAt(ti, ctx.NowMs, ctx.Settings).Position - ctx.Ships[slot].Position).Length <= ctx.Settings.FightCompareM &&
            !(fight == CaptainOrder.Fight && new Unit(target).ShipOf(groups) == w.Target))
            ctx.Issue(ctx.Orders.Fight(u, Unit.Ship(w.Target), 0, OrderSource.Commander));
        if (w.Choice == WingChoice.Line)
            Judge.AddMembers(ctx.World, wg, wingRest);
        return true;
    }

    /// <summary>A flotilla of his force sent in to attack (docs/battle/admirals-orders.md O7b), by the rule every commander
    /// shares; true if it was.</summary>
    bool SendIn(in AiContext ctx, Admiral a, int member, byte side)
    {
        var u = Unit.Group(member);
        if (groups.Get(member) is not { } fg || ctx.Ships.SlotOf(groups.GuideShip(fg)) is not (int slot and >= 0) || !Attacks.Free(ctx, u, slot))
            return false;
        var (phase, reason) = Commanders?.PhaseOf(side) ?? default;
        var why = Attacks.SendIn(ctx, side, phase, reason, u, ctx.Ships[slot].Position, Attacks.Held(ctx, side));
        if (why == Sim.Ai.Commanders.CommanderWhy.None)
            return false;
        Give(ctx, a, member, ctx.Orders.Attack(u, 0, OrderSource.Commander));
        Attacks.Sent(u, why);
        return true;
    }

    /// <summary>A task he gives a member, and when (its task's start, so he knows it's still his).</summary>
    void Give(in AiContext ctx, Admiral a, int member, in Command task)
    {
        ctx.Issue(task);
        for (int k = 0; k < a.GaveCount; k++)
            if (a.GaveTo[k] == member)
            {
                a.GaveMs[k] = ctx.NowMs;
                return;
            }
        if (a.GaveCount == a.GaveTo.Length)
        {
            Array.Resize(ref a.GaveTo, a.GaveCount * 2);
            Array.Resize(ref a.GaveMs, a.GaveCount * 2);
        }
        (a.GaveTo[a.GaveCount], a.GaveMs[a.GaveCount]) = (member, ctx.NowMs);
        a.GaveCount++;
    }

    /// <summary>When he last gave a member a task (-1: never).</summary>
    static long Gave(Admiral a, int member)
    {
        for (int k = 0; k < a.GaveCount; k++)
            if (a.GaveTo[k] == member)
                return a.GaveMs[k];
        return -1;
    }
}
