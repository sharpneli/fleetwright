using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>One enemy group as a navy sees it (docs/battle/command.md "The tactical picture").</summary>
public struct EnemyGroup
{
    /// <summary>Where its ships are on the average, and how they go.</summary>
    public Vec3d Centre, Velocity;

    /// <summary>Its tonnage, as its ships are taken (<see cref="Matchups.TonnageOf"/>).</summary>
    public double TonnageT;

    /// <summary>How many ships, and how many taken for capital ships.</summary>
    public int Count, Capitals;

    /// <summary>Its van: the ship furthest along its course (the nearest the centre, stopped), and where it is.</summary>
    public int Van;
    public Vec3d VanPosition;
}

/// <summary>What an admiral sees of the enemy (docs/battle/command.md "The tactical picture"): his navy's contacts in sight
/// chained into groups, each within <see cref="SimSettings.TacticalGapM"/> of the next whatever its type (a battle line
/// with its screen is one group), with their centre, course, tonnage, capital ships and van. Built for a side the first
/// time it's asked at a moment, through the contacts' grid (each contact's neighbours, never pairs); not world state.
/// Nothing allocated once grown.</summary>
public sealed class TacticalPicture(SeenContacts seen, Matchups matchups)
{
    sealed class Side
    {
        public long BuiltMs = long.MinValue;
        public Picture? Picture;
        public int[] Parent = [], Slot = [];
        public EnemyGroup[] Groups = [];
        public int Count;
        public double[] VanKey = [];
    }

    readonly Side?[] sides = new Side?[256];

    /// <summary>The enemy groups a side sees now.</summary>
    public ReadOnlySpan<EnemyGroup> Of(World world, byte side)
    {
        var v = Build(world, side);
        return v == null ? [] : v.Groups.AsSpan(0, v.Count);
    }

    /// <summary>The enemy's main body near a point: the biggest group (by tonnage) with a capital ship whose van or centre
    /// is within a radius; false if there's none.</summary>
    public bool MainBody(World world, byte side, Vec3d near, double radiusM, out EnemyGroup main)
    {
        main = default;
        bool any = false;
        foreach (ref readonly var g in Of(world, side))
            if (g.Capitals > 0 && (Math.Min((g.Centre - near).Length, (g.VanPosition - near).Length) <= radiusM) && (!any || g.TonnageT > main.TonnageT))
                (main, any) = (g, true);
        return any;
    }

    Side? Build(World world, byte side)
    {
        var pic = world.Sightings.Of(side);
        if (pic == null)
            return null;
        var v = sides[side] ??= new Side();
        long now = world.Time.Ms;
        if (v.BuiltMs == now && v.Picture == pic)
            return v;
        (v.BuiltMs, v.Picture, v.Count) = (now, pic, 0);
        int n = pic.Count;
        if (v.Parent.Length < n)
        {
            v.Parent = new int[Math.Max(n, 2 * v.Parent.Length)];
            v.Slot = new int[v.Parent.Length];
        }
        double gap = world.Settings.TacticalGapM;
        seen.Near(world, side, Vec3d.Zero, 0);   // the side's contacts placed for this moment
        for (int i = 0; i < n; i++)
            v.Parent[i] = i;
        for (int i = 0; i < n; i++)
        {
            if (!InSight(pic, i))
                continue;
            foreach (int j in seen.Near(world, side, seen.Position(side, i), gap))
                if (j > i)
                    Union(v.Parent, i, j);
        }

        // the groups: one per root of contacts in sight, summed
        Array.Fill(v.Slot, -1, 0, n);
        for (int i = 0; i < n; i++)
        {
            if (!InSight(pic, i))
                continue;
            int r = Find(v.Parent, i);
            if (v.Slot[r] < 0)
            {
                if (v.Count == v.Groups.Length)
                    Array.Resize(ref v.Groups, Math.Max(8, 2 * v.Groups.Length));
                v.Slot[r] = v.Count;
                v.Groups[v.Count++] = default;
            }
            ref var g = ref v.Groups[v.Slot[r]];
            g.Centre += seen.Position(side, i);
            g.Velocity += seen.Velocity(side, i);
            g.TonnageT += matchups.TonnageOf(world, pic[i]);
            g.Count++;
            if (pic[i].Type == ShipType.Capital)
                g.Capitals++;
        }
        for (int k = 0; k < v.Count; k++)
        {
            ref var g = ref v.Groups[k];
            g.Centre = g.Centre.Normalized() * Earth.Radius;
            g.Velocity *= 1.0 / g.Count;
        }
        // each group's van
        if (v.VanKey.Length < v.Count)
            v.VanKey = new double[Math.Max(8, 2 * v.Count)];
        var best = v.VanKey.AsSpan(0, v.Count);
        best.Fill(double.MaxValue);
        for (int i = 0; i < n; i++)
        {
            if (!InSight(pic, i))
                continue;
            ref var g = ref v.Groups[v.Slot[Find(v.Parent, i)]];
            var d = seen.Position(side, i) - g.Centre;
            double key = g.Velocity.Length > 1 ? -Vec3d.Dot(d, g.Velocity) : d.Length;
            int k = v.Slot[Find(v.Parent, i)];
            if (key < best[k])
                (best[k], g.Van, g.VanPosition) = (key, pic[i].Entity, seen.Position(side, i));
        }
        return v;
    }

    static bool InSight(Picture pic, int i) => pic[i].InSight && (pic[i].Seen & SeenFlags.GoingDown) == 0;

    static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
            i = parent[i] = parent[parent[i]];
        return i;
    }

    static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra != rb)
            parent[Math.Max(ra, rb)] = Math.Min(ra, rb);   // the lower index the root: the same on every run
    }
}
