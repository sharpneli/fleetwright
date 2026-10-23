using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>A force's disposition (docs/battle/admirals-orders.md O7, the chain of command): its admiral keeps his main
/// body in his line (his members of the strongest role in it: the battle line, else scouting), carrying whatever the
/// force is told; the rest attend it by their roles (<see cref="Dispositions.Attend"/>): his scouts scout ahead of the
/// force, spread off its bow, and his flotillas and screens screen it, each a task he gives as its commander
/// (<see cref="OrderSource.Commander"/>); his auxiliaries keep their place in his line. A force with no battle line or
/// scouts (flotillas and screens only) keeps together.
/// <para>Every <see cref="SimSettings.ForcePeriodMs"/>, with doctrine's Dispose, not while deploying nor on the conn.
/// A member the player holds by hand, or on a task someone else gave it (the side's commander sending a flotilla in, the
/// player's own), is left alone; one back with no task (its attack over, rejoined) is given its own again; one whose
/// role now puts it in the main body is called back into the line. Each force disposes its own members, so a fleet of
/// forces is disposed level by level.</para></summary>
public sealed partial class AdmiralLayer
{
    /// <summary>What each unit is for (docs/battle/admirals-orders.md O6; set by the AI system).</summary>
    internal Roles Roles { get; set; } = null!;

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
            if (!attends)
            {
                if (mine)
                    Give(ctx, a, m, ctx.Orders.Join(u, force, OrderSource.Commander));   // its role is the body's now: back into the line
                continue;
            }
            if (mine && Tasks.Of(u).Kind == Dispositions.KindFor(role) && Tasks.TargetOf(u) == force.Id)
                continue;   // at it already
            Give(ctx, a, m, Dispositions.Attend(ctx.Orders, role, u, force, scout, OrderSource.Commander));
        }
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
