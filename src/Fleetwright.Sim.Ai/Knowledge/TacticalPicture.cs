using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>One enemy group as a navy sees it (docs/battle/command.md "The tactical picture").</summary>
public struct EnemyGroup
{
    /// <summary>Where its ships are on the average, and how they go.</summary>
    public Vec3d Centre, Velocity;

    /// <summary>Its tonnage, as its ships are taken (<see cref="Matchups.TonnageOf"/>).</summary>
    public double TonnageT;

    /// <summary>How many ships.</summary>
    public int Count;

    /// <summary>Its van: the ship furthest along its course (the nearest the centre, stopped), and where it is.</summary>
    public int Van;
    public Vec3d VanPosition;

    /// <summary>Its strength against the side's fleet, and whether its ships are of his line (<see cref="ForceMeasures.Enemy"/>,
    /// docs/battle/allocation.md AL5); the strongest of those is his main body.</summary>
    public double StrengthT;
    public bool Line;

    /// <summary>Its contacts: a run of the picture's indices (<see cref="TacticalPicture.Members"/>).</summary>
    public int First;
}

/// <summary>What an admiral sees of the enemy (docs/battle/command.md "The tactical picture"): his navy's contacts in sight
/// chained into groups, each within <see cref="SimSettings.TacticalGapM"/> of the next whatever its type (a battle line
/// with its screen is one group), with their centre, course, tonnage, capital ships, van and strength. Built for a side the first
/// time it's asked at a moment, through the contacts' grid (each contact's neighbours, never pairs); not world state.
/// Nothing allocated once grown.</summary>
public sealed class TacticalPicture(SeenContacts seen, Matchups matchups)
{
    sealed class Side
    {
        public long BuiltMs = long.MinValue;
        public Picture? Picture;
        public int Version;
        public int[] Parent = [], Slot = [], Members = [];
        public EnemyGroup[] Groups = [];
        public int Count;
        public double[] VanKey = [];
    }

    readonly Side?[] sides = new Side?[256];
    readonly MatchupSide scratch = new();

    /// <summary>What measures a group's strength (set by the AI system; without it, the old rule: a capital ship and tonnage).</summary>
    internal ForceMeasures? Measures { get; set; }

    /// <summary>A group's contacts, by their index in the side's picture.</summary>
    public ReadOnlySpan<int> Members(byte side, in EnemyGroup g) => sides[side]!.Members.AsSpan(g.First, g.Count);

    /// <summary>The enemy groups a side sees now.</summary>
    public ReadOnlySpan<EnemyGroup> Of(World world, byte side)
    {
        var v = Build(world, side);
        return v == null ? [] : v.Groups.AsSpan(0, v.Count);
    }

    /// <summary>The enemy's main body near a point (docs/battle/allocation.md AL5): the strongest group of his line's ships
    /// (<see cref="EnemyGroup.Line"/>) whose van or centre is within a radius; false if there's none.</summary>
    public bool MainBody(World world, byte side, Vec3d near, double radiusM, out EnemyGroup main)
    {
        main = default;
        bool any = false;
        foreach (ref readonly var g in Of(world, side))
            if (g.Line && (Math.Min((g.Centre - near).Length, (g.VanPosition - near).Length) <= radiusM) && (!any || g.StrengthT > main.StrengthT))
                (main, any) = (g, true);
        return any;
    }

    /// <summary>A group's contacts into a side of a matchup, as they're taken.</summary>
    public void AddTo(World world, byte side, in EnemyGroup g, MatchupSide into)
    {
        var pic = world.Sightings.Of(side)!;
        foreach (int i in Members(side, g))
            into.AddContact(world, matchups, side, pic[i]);
    }

    Side? Build(World world, byte side)
    {
        var pic = world.Sightings.Of(side);
        if (pic == null)
            return null;
        var v = sides[side] ??= new Side();
        long now = world.Time.Ms;
        if (v.Picture == pic && v.Version == pic.Version && now - v.BuiltMs < world.Settings.AdmiralPeriodMs)
            return v;   // the picture as it was built from, within a think's period (docs/battle/workers.md W14)
        (v.BuiltMs, v.Picture, v.Version, v.Count) = (now, pic, pic.Version, 0);
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
            g.TonnageT += matchups.TonnageOf(world, pic.Side, pic[i]);
            g.Count++;
        }
        for (int k = 0, at = 0; k < v.Count; k++)
        {
            ref var g = ref v.Groups[k];
            g.Centre = g.Centre.Normalized() * Earth.Radius;
            g.Velocity *= 1.0 / g.Count;
            (g.First, at) = (at, at + g.Count);
        }
        // each group's contacts in a run, and its strength
        if (v.Members.Length < n)
            v.Members = new int[v.Parent.Length];
        Span<int> fill = v.Count <= 256 ? stackalloc int[v.Count] : new int[v.Count];
        for (int i = 0; i < n; i++)
            if (InSight(pic, i))
            {
                int k = v.Slot[Find(v.Parent, i)];
                v.Members[v.Groups[k].First + fill[k]++] = i;
            }
        for (int k = 0; k < v.Count; k++)
        {
            ref var g = ref v.Groups[k];
            if (Measures == null)
            {
                (g.StrengthT, g.Line) = (g.TonnageT, true);   // no measures (no AI system round it): every group a line
                continue;
            }
            scratch.Clear();
            foreach (int i in v.Members.AsSpan(g.First, g.Count))
                scratch.AddContact(world, matchups, side, pic[i]);
            (g.StrengthT, _, g.Line) = scratch.Count > 0 ? Measures.Enemy(side, scratch) : default;
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

    static bool InSight(Picture pic, int i) => pic[i].InSight && !pic[i].Out;

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
