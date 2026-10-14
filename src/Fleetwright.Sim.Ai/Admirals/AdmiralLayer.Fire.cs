using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>The admirals' fire orders and standing orders (docs/battle/fighting.md 4.2c).
/// <list type="bullet">
/// <item><b>A fire order to a group</b> goes to every ship of its tree, fresh: each takes it whatever it held (the
/// player's order to a group is to all of it). Hold fire and fire at will are given once.</item>
/// <item><b>Engage</b> is distributed over the enemy's line, and again each second over the ships still holding it (a
/// ship the player has since given its own keeps that, and drops out of the count). The enemy's line is the contacts in
/// sight chained to the target, each within <see cref="SimSettings.FireLineGapM"/> of the next and of its type if that's
/// made out, counted from its van along its course; his own ships are counted from the van by their stations. With n of
/// his against m, ship k takes enemy ⌊k·m/n⌋ when n ≥ m (the van doubles up, as Lion and Princess Royal did on
/// Lützow), else enemy k (the rear goes unengaged).</item>
/// <item><b>Doctrine's Engage</b> (docs/battle/command.md "Doctrine"): to fight, it becomes Fight when his guide first meets
/// an enemy in sight in an encounter, for a group that's no force's line; it's armed again once the guide is out of
/// encounters.</item>
/// </list></summary>
public sealed partial class AdmiralLayer
{
    readonly List<int> tree = new(32), own = new(32), enemy = new(32), queue = new(32);
    readonly List<(int Ship, double AsternM, double AbeamM)> offsets = new(32);
    double[] keys = new double[32];
    int[] items = new int[32];
    int[] visited = new int[32];
    int visitStamp;

    /// <summary>A fire order to a group, from the player (or a scenario).</summary>
    void FireOrdered(in AiContext ctx, GroupSystem.Group g, Admiral a, in Command c)
    {
        a.Fire = (AiCommand)c.Code switch
        {
            AiCommand.HoldFire => FireOrder.Hold,
            AiCommand.Engage => FireOrder.Engage,
            _ => FireOrder.AtWill,
        };
        a.FireTarget = a.Fire == FireOrder.Engage ? (int)c.A : 0;
        HandFire(ctx, g, a, fresh: true);
    }

    /// <summary>His fire order to the ships of his tree: all of them when it's fresh, else those still holding his.</summary>
    void HandFire(in AiContext ctx, GroupSystem.Group g, Admiral a, bool fresh)
    {
        Ships(g, tree);
        own.Clear();
        foreach (int ship in tree)
            if (fresh || captains.FireOf(ship).From == g.GroupId)
                own.Add(ship);
        if (own.Count == 0)
            return;
        if (a.Fire == FireOrder.Engage)
        {
            int slot = ctx.Ships.SlotOf(own[0]);
            byte side = slot >= 0 ? ctx.Ships[slot].Side : (byte)0;
            EnemyLine(ctx, side, a.FireTarget);
            if (enemy.Count == 0)
            {
                // the target's gone from the picture: at will
                (a.Fire, a.FireTarget) = (FireOrder.AtWill, 0);
            }
        }
        int n = own.Count, m = enemy.Count;
        for (int k = 0; k < n; k++)
        {
            int target = a.Fire != FireOrder.Engage ? 0 : enemy[n >= m ? (int)((long)k * m / n) : k];
            var (order, held, from) = captains.FireOf(own[k]);
            if (fresh || order != a.Fire || held != target || from != g.GroupId)
                ctx.Issue(ctx.Orders.Fire(own[k], a.Fire, target, g.GroupId, fresh));
        }
    }

    /// <summary>The ships of a group's tree: its line's in van order (by their stations astern of the guide), then the
    /// detached.</summary>
    void Ships(GroupSystem.Group g, List<int> into)
    {
        into.Clear();
        groups.LineOffsets(g, offsets);
        int n = offsets.Count;
        Ensure(n);
        for (int i = 0; i < n; i++)
            (keys[i], items[i]) = (offsets[i].AsternM, offsets[i].Ship);
        Array.Sort(keys, items, 0, n);
        for (int i = 0; i < n; i++)
            into.Add(items[i]);
        AddDetached(g, into, n);
    }

    void AddDetached(GroupSystem.Group g, List<int> into, int inLine)
    {
        for (int i = 0; i < g.Members.Count; i++)
        {
            int member = g.Members[i];
            if (g.IsForce)
            {
                if (groups.Get(member) is { } sub)
                    AddDetached(sub, into, inLine);
            }
            else if (into.IndexOf(member, 0, inLine) < 0)
                into.Add(member);
        }
    }

    /// <summary>The enemy's line through a contact (<see cref="enemy"/>): the contacts in sight chained to it, van first;
    /// just the contact if it's held but not in sight; empty if it's not held.</summary>
    void EnemyLine(in AiContext ctx, byte side, int target)
    {
        enemy.Clear();
        var pic = ctx.World.Sightings.Of(side);
        int ti = pic?.IndexOf(target) ?? -1;
        if (ti < 0)
            return;
        if (!pic![ti].InSight)
        {
            enemy.Add(target);
            return;
        }
        var type = pic[ti].Type;
        if (visited.Length < pic.Count)
            visited = new int[Math.Max(pic.Count, visited.Length * 2)];
        visitStamp++;
        queue.Clear();
        queue.Add(ti);
        visited[ti] = visitStamp;
        for (int q = 0; q < queue.Count; q++)
            foreach (int j in seen.Close(ctx.World, side, seen.Position(side, queue[q])))
                if (visited[j] != visitStamp && (type == ShipType.Unknown || pic[j].Type == type))
                {
                    visited[j] = visitStamp;
                    queue.Add(j);
                }

        // van first: furthest along the target's course; a stopped line, nearest the target first
        var at = seen.Position(side, ti);
        var v = seen.Velocity(side, ti);
        bool moving = v.Length > 1;
        Ensure(queue.Count);
        for (int k = 0; k < queue.Count; k++)
        {
            var d = seen.Position(side, queue[k]) - at;
            (keys[k], items[k]) = (moving ? -Vec3d.Dot(d, v) : d.Length, pic[queue[k]].Entity);
        }
        Array.Sort(keys, items, 0, queue.Count);
        for (int k = 0; k < queue.Count; k++)
            enemy.Add(items[k]);
    }

    void Ensure(int n)
    {
        if (keys.Length < n)
        {
            keys = new double[Math.Max(n, keys.Length * 2)];
            items = new int[keys.Length];
        }
    }

    /// <summary>A group whose doctrine is to fight takes up Fight when its guide first meets an enemy in sight in an
    /// encounter (and isn't in a force's line: its force fights).</summary>
    void TakeUpStanding(in AiContext ctx, GroupSystem.Group g, Admiral a, int lead)
    {
        if (lead < 0)
            return;
        var ships = ctx.Ships;
        var guide = ships[lead];
        if ((Engage)doctrine.OfGroup(g, guide.Side, DoctrineField.Engage) != Engage.Fight)
            return;
        if (ctx.World.Encounters.Of(lead, guide.Entity) == 0)
        {
            a.Met = false;
            return;
        }
        if (a.Met || groups.InForceLine(g) || seen.Near(ctx.World, guide.Side, guide.Position, ctx.Settings.FightCompareM).Length == 0)
            return;
        a.Met = true;
        (a.Order, a.Target, a.DistanceM, a.AbeamM) = (CaptainOrder.Fight, 0, 0, 0);
        var to = GuideUnit(g);
        if (to.Id != 0)
            ctx.Issue(ctx.Orders.Fight(to, default, 0, OrderSource.Superior));
    }
}
