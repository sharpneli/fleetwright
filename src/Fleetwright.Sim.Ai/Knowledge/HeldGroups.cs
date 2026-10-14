using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>An enemy group a navy holds, in sight or not (docs/battle/command.md 5.1g): its contacts (a run of
/// <see cref="HeldGroups.Members"/>), where they are on the average and how they go by the picture's estimates, its
/// tonnage as its ships are taken, its capital ships, and its biggest ship (the one to close).</summary>
public struct HeldGroup
{
    public int First, Count;
    public Vec3d Centre, Velocity;
    public double TonnageT;
    public int Capitals;
    public int Biggest;
}

/// <summary>The enemy as a side's commander holds him (docs/battle/command.md 5.1g): every fresh contact
/// (<see cref="TaskLayer.Fresh"/>: in sight, or seen within <see cref="SimSettings.ContactFreshMs"/>), not seen going
/// down, where the picture's estimate puts it now, chained into groups within <see cref="SimSettings.TacticalGapM"/> of
/// each other through a grid (never pairs). The tactical picture (<see cref="TacticalPicture"/>) is the same for the
/// contacts in sight. Built for a side the first time it's asked at a moment; not world state; nothing allocated once
/// grown.</summary>
public sealed class HeldGroups(Matchups matchups)
{
    sealed class Side
    {
        public long BuiltMs = long.MinValue;
        public Picture? Picture;
        public int[] Parent = [], Slot = [], Index = [], Members = [];
        public Vec3d[] Position = [];
        public HeldGroup[] Groups = [];
        public double[] BiggestT = [];
        public int Count, Held;
        public readonly CubeIndex Grid = new();
    }

    readonly Side?[] sides = new Side?[256];

    /// <summary>The groups a side holds now.</summary>
    public ReadOnlySpan<HeldGroup> Of(in AiContext ctx, byte side)
    {
        var v = Build(ctx, side);
        return v == null ? [] : v.Groups.AsSpan(0, v.Count);
    }

    /// <summary>A group's contacts, by their index in the side's picture.</summary>
    public ReadOnlySpan<int> Members(byte side, in HeldGroup g) => sides[side]!.Members.AsSpan(g.First, g.Count);

    Side? Build(in AiContext ctx, byte side)
    {
        var world = ctx.World;
        var pic = world.Sightings.Of(side);
        if (pic == null)
            return null;
        var v = sides[side] ??= new Side();
        long now = ctx.NowMs;
        if (v.BuiltMs == now && v.Picture == pic)
            return v;
        (v.BuiltMs, v.Picture, v.Count, v.Held) = (now, pic, 0, 0);
        int n = pic.Count;
        if (v.Parent.Length < n)
        {
            int m = Math.Max(n, 2 * v.Parent.Length);
            (v.Parent, v.Slot, v.Index, v.Members, v.Position) = (new int[m], new int[m], new int[m], new int[m], new Vec3d[m]);
        }
        // the fresh contacts, where they are now
        double gap = ctx.Settings.TacticalGapM;
        v.Grid.Clear(n, gap);
        for (int i = 0; i < n; i++)
        {
            ref readonly var c = ref pic[i];
            if ((c.Seen & (SeenFlags.GoingDown | SeenFlags.Gone)) != 0 || !TaskLayer.Fresh(ctx, c))
                continue;
            int h = v.Held++;
            v.Index[h] = i;
            v.Position[h] = pic.EstimateAt(i, now, ctx.Settings).Position;
            v.Parent[h] = h;
            v.Grid.Add(h, v.Position[h]);
        }
        for (int h = 0; h < v.Held; h++)
        {
            int found = v.Grid.Near(v.Position[h]);
            foreach (int j in v.Grid.Found(found))
                if (j > h && (v.Position[j] - v.Position[h]).Length <= gap)
                    Union(v.Parent, h, j);
        }
        // the groups, in the order of their first contact
        Array.Fill(v.Slot, -1, 0, v.Held);
        for (int h = 0; h < v.Held; h++)
        {
            int r = Find(v.Parent, h);
            if (v.Slot[r] < 0)
            {
                if (v.Count == v.Groups.Length)
                {
                    Array.Resize(ref v.Groups, Math.Max(8, 2 * v.Groups.Length));
                    Array.Resize(ref v.BiggestT, v.Groups.Length);
                }
                v.Slot[r] = v.Count;
                v.BiggestT[v.Count] = -1;
                v.Groups[v.Count++] = default;
            }
            v.Groups[v.Slot[r]].Count++;
        }
        for (int k = 0, at = 0; k < v.Count; k++)
        {
            v.Groups[k].First = at;
            at += v.Groups[k].Count;
            v.Groups[k].Count = 0;
        }
        for (int h = 0; h < v.Held; h++)
        {
            int k = v.Slot[Find(v.Parent, h)];
            ref var g = ref v.Groups[k];
            int i = v.Index[h];
            v.Members[g.First + g.Count++] = i;
            g.Centre += v.Position[h];
            g.Velocity += pic.EstimateAt(i, now, ctx.Settings).Velocity;
            double t = matchups.TonnageOf(world, pic[i]);
            g.TonnageT += t;
            if (pic[i].Type == ShipType.Capital)
                g.Capitals++;
            if (t > v.BiggestT[k])
                (g.Biggest, v.BiggestT[k]) = (pic[i].Entity, t);
        }
        for (int k = 0; k < v.Count; k++)
        {
            ref var g = ref v.Groups[k];
            g.Centre = g.Centre.Normalized() * Earth.Radius;
            g.Velocity *= 1.0 / g.Count;
        }
        return v;
    }

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
            parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
    }
}
