using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>The enemy's line through a contact (docs/battle/fighting.md 4.2c, docs/battle/command.md 5.1b): the contacts
/// a navy holds in sight chained to it, each within <see cref="SimSettings.FireLineGapM"/> of the next and of its type
/// if that's made out, van first (furthest along the contact's course; a stopped line, nearest the contact first). Just
/// the contact if it's held but not in sight; nothing if it's not held. What an admiral distributes his fire over and a
/// line's guide crosses ahead of: one implementation, until the tactical picture's enemy groups (5.1c) take it over.
/// Each user holds his own (its scratch); nothing allocated once grown.</summary>
public sealed class EnemyLines(SeenContacts seen)
{
    readonly List<int> queue = new(32);
    double[] keys = new double[32];
    int[] items = new int[32];
    int[] visited = new int[32];
    int visitStamp;

    /// <summary>The line through a contact (an entity, as the side's picture holds it), van first, into a list of
    /// entities.</summary>
    public void Of(World world, byte side, int target, List<int> into)
    {
        into.Clear();
        var pic = world.Sightings.Of(side);
        int ti = pic?.IndexOf(target) ?? -1;
        if (ti < 0)
            return;
        if (!pic![ti].InSight)
        {
            into.Add(target);
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
            foreach (int j in seen.Close(world, side, seen.Position(side, queue[q])))
                if (visited[j] != visitStamp && (type == ShipType.Unknown || pic[j].Type == type))
                {
                    visited[j] = visitStamp;
                    queue.Add(j);
                }

        var at = seen.Position(side, ti);
        var v = seen.Velocity(side, ti);
        bool moving = v.Length > 1;
        if (keys.Length < queue.Count)
        {
            keys = new double[Math.Max(queue.Count, keys.Length * 2)];
            items = new int[keys.Length];
        }
        for (int k = 0; k < queue.Count; k++)
        {
            var d = seen.Position(side, queue[k]) - at;
            (keys[k], items[k]) = (moving ? -Vec3d.Dot(d, v) : d.Length, pic[queue[k]].Entity);
        }
        Array.Sort(keys, items, 0, queue.Count);
        for (int k = 0; k < queue.Count; k++)
            into.Add(items[k]);
    }
}
