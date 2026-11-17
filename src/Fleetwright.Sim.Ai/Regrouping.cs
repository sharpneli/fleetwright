using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>Worn squadrons regrouped (docs/battle/allocation.md AL6, decision 1), so a fleet that has lost ships isn't a
/// string of stragglers. A squadron is worn once it's down to <see cref="SimSettings.RegroupShare"/> of the most ships it
/// has had (ships go from a squadron sunk, given up, fallen out or staying behind as a rearguard; a squadron of one is a
/// lone ship from the start, never worn). Its admiral (the side's commander for his top units, a force's admiral for his
/// members) merges it into a squadron of his on the same job (<see cref="Roles"/>) in sight of it (within the visibility
/// at its guide), of ships alike (speeds within <see cref="SimSettings.AllocationSpareKn"/>, strength a ship within
/// <see cref="SimSettings.AllocationLineShare"/> of each other: the rules the allocation draws its line by), the nearest:
/// its ships go to the end of that one's line, in their order, and it's gone. The fewest first; each squadron once a pass.
/// Which squadrons are his to regroup (not the player's, not in on an attack) is the caller's. Merges allocate (a
/// reorganisation); a pass with none worn doesn't. AI state: the squadrons' most ships, hashed once one is worn.</summary>
public sealed class Regrouping(World world, ForceMeasures measures, Roles roles)
{
    readonly GroupSystem groups = world.Groups;
    int[] most = new int[64];   // each squadron's most ships so far, by group id
    readonly List<int> worn = new(16), used = new(16);
    readonly List<Merge> made = new(16);

    /// <summary>A merge made: when, whose side, the squadron gone and the one it went into (their names as they were),
    /// how many ships went.</summary>
    public readonly record struct Merge(long Ms, byte Side, int From, int Into, string FromName, string IntoName, int Ships);

    /// <summary>The merges made, oldest first.</summary>
    public int Count => made.Count;
    public Merge this[int i] => made[i];

    /// <summary>The most ships each squadron has had: at every think, before a layer regroups.</summary>
    internal void Note()
    {
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (g.IsForce)
                continue;
            if (g.GroupId >= most.Length)
                Array.Resize(ref most, Math.Max(2 * most.Length, g.GroupId + 1));
            most[g.GroupId] = Math.Max(most[g.GroupId], g.Members.Count);
        }
    }

    /// <summary>Whether a squadron is worn: down to <see cref="SimSettings.RegroupShare"/> of the most ships it has had.</summary>
    public bool Worn(GroupSystem.Group g)
    {
        if (g.IsForce || g.GroupId >= most.Length)
            return false;
        int n = g.Members.Count, top = most[g.GroupId];
        return n > 0 && n < top && n <= world.Settings.RegroupShare * top;
    }

    /// <summary>One admiral's pass over his squadrons free to regroup (group ids, a side's): each worn one, the fewest
    /// ships first, into the nearest like squadron in sight of it.</summary>
    internal void Pass(in AiContext ctx, byte side, List<int> squadrons)
    {
        worn.Clear();
        for (int i = 0; i < squadrons.Count; i++)
            if (groups.Get(squadrons[i]) is { } g && Worn(g))
                worn.Add(g.GroupId);
        if (worn.Count == 0)
            return;
        for (int a = 1; a < worn.Count; a++)   // the fewest first, ties by id (a handful: insertion)
        {
            int x = worn[a], b = a;
            for (; b > 0 && Before(x, worn[b - 1]); b--)
                worn[b] = worn[b - 1];
            worn[b] = x;
        }
        used.Clear();
        var s = ctx.Settings;
        foreach (int w in worn)
        {
            if (used.Contains(w) || groups.Get(w) is not { } wg || At(ctx, wg) is not { } at)
                continue;
            var u = Unit.Group(w);
            var role = roles.Of(u);
            var mw = measures.Of(u);
            double reach = ctx.World.Weather.At(at, ctx.World.Time).VisibilityM;
            int best = 0;
            for (int i = 0; i < squadrons.Count; i++)
            {
                int c = squadrons[i];
                if (c == w || used.Contains(c) || groups.Get(c) is not { IsForce: false } cg || roles.Of(Unit.Group(c)) != role ||
                    At(ctx, cg) is not { } there || (there - at).Length > reach || !Alike(mw, measures.Of(Unit.Group(c)), s))
                    continue;
                (best, reach) = (c, (there - at).Length);
            }
            if (best == 0 || groups.Get(best) is not { } into)
                continue;
            made.Add(new Merge(ctx.NowMs, side, w, best, wg.Name, into.Name, wg.Members.Count));
            for (int i = 0; i < wg.Members.Count; i++)
                ctx.Issue(GroupSystem.Add(best, wg.Members[i]));
            used.Add(w);
            used.Add(best);
        }
    }

    bool Before(int a, int b)
    {
        int na = groups.Get(a)!.Members.Count, nb = groups.Get(b)!.Members.Count;
        return na != nb ? na < nb : a < b;
    }

    /// <summary>Ships alike enough to sail as one: speeds within the spare, strength a ship within the line's share.</summary>
    static bool Alike(in UnitMeasure a, in UnitMeasure b, SimSettings s)
    {
        double lo = Math.Min(a.ShipStrengthT, b.ShipStrengthT), hi = Math.Max(a.ShipStrengthT, b.ShipStrengthT);
        return Math.Abs(a.SpeedKn - b.SpeedKn) < s.AllocationSpareKn && lo >= s.AllocationLineShare * hi;
    }

    Vec3d? At(in AiContext ctx, GroupSystem.Group g) =>
        ctx.Ships.SlotOf(groups.GuideShip(g)) is int slot and >= 0 ? ctx.Ships[slot].Position : null;

    public void Hash(ref StateHash h)
    {
        if (made.Count > 0)
            h.Add(made.Count);   // only then, and only the worn below: a world that has lost no ship hashes as before
        for (int k = 0; k < groups.Count; k++)
            if (Worn(groups[k]))
            {
                h.Add(groups[k].GroupId);
                h.Add(most[groups[k].GroupId]);
            }
    }
}
