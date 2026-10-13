using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>Each navy's contacts in sight, where its picture puts them now, in grids (docs/battle/fighting.md 4.2c): what
/// captains pick targets from and admirals find the enemy's line in. Built for a side the first time it's asked at a
/// moment, so a think costs one pass over each fighting navy's contacts, and every question after it is local (a
/// cube's neighbours, never ships × contacts). Two grids: a wide one for "what's within reach" (its cube grows to the
/// widest question asked) and a fine one, <see cref="SimSettings.FireLineGapM"/> wide, for chaining a line. Not world
/// state: rebuilt from the pictures whenever it's asked at a new moment.</summary>
public sealed class SeenContacts
{
    sealed class Side
    {
        public long BuiltMs = long.MinValue;
        public double Cell;
        public Picture? Picture;
        public Vec3d[] Position = [];
        public Vec3d[] Velocity = [];
        public bool[] InSight = [];
        public readonly CubeIndex Wide = new(), Fine = new();
    }

    readonly Side?[] sides = new Side?[256];
    double cell = 30_000;
    int[] found = new int[64];

    /// <summary>The contacts a side holds in sight within a radius of a point now, by their index in its picture, in
    /// index order; their positions from <see cref="Position"/>.</summary>
    public ReadOnlySpan<int> Near(World world, byte side, Vec3d p, double radiusM)
    {
        if (radiusM > cell)
        {
            cell = radiusM * 1.25;   // a wider question than the cubes: every side's grid is rebuilt wider
            foreach (var s in sides)
                if (s != null)
                    s.BuiltMs = long.MinValue;
        }
        var v = Build(world, side);
        if (v == null)
            return [];
        return Filter(v, v.Wide, p, radiusM);
    }

    /// <summary>As <see cref="Near"/>, within <see cref="SimSettings.FireLineGapM"/> (the fine grid).</summary>
    public ReadOnlySpan<int> Close(World world, byte side, Vec3d p)
    {
        var v = Build(world, side);
        return v == null ? [] : Filter(v, v.Fine, p, world.Settings.FireLineGapM);
    }

    /// <summary>Where a side's picture puts its contact (by index) now, and its velocity; built by a
    /// <see cref="Near"/> or <see cref="Close"/> for that side at this moment.</summary>
    public Vec3d Position(byte side, int i) => sides[side]!.Position[i];

    public Vec3d Velocity(byte side, int i) => sides[side]!.Velocity[i];

    ReadOnlySpan<int> Filter(Side v, CubeIndex grid, Vec3d p, double radiusM)
    {
        int n = grid.Near(p), m = 0;
        if (found.Length < n)
            found = new int[Math.Max(n, found.Length * 2)];
        var all = grid.Found(n);
        double r2 = radiusM * radiusM;
        foreach (int i in all)
            if ((v.Position[i] - p).LengthSquared <= r2)
                found[m++] = i;
        return found.AsSpan(0, m);
    }

    Side? Build(World world, byte side)
    {
        var pic = world.Sightings.Of(side);
        if (pic == null)
            return null;
        var v = sides[side] ??= new Side();
        long now = world.Time.Ms;
        if (v.BuiltMs == now && v.Picture == pic && v.Cell == cell)
            return v;
        (v.BuiltMs, v.Picture, v.Cell) = (now, pic, cell);
        int n = pic.Count;
        if (v.Position.Length < n)
        {
            int size = Math.Max(n, v.Position.Length * 2);
            Array.Resize(ref v.Position, size);
            Array.Resize(ref v.Velocity, size);
            Array.Resize(ref v.InSight, size);
        }
        v.Wide.Clear(n, cell);
        v.Fine.Clear(n, world.Settings.FireLineGapM);
        for (int i = 0; i < n; i++)
        {
            v.InSight[i] = pic[i].InSight;
            if (!v.InSight[i])
                continue;
            var (p, vel, _) = pic.EstimateAt(i, now, world.Settings);
            (v.Position[i], v.Velocity[i]) = (p, vel);
            v.Wide.Add(i, p);
            v.Fine.Add(i, p);
        }
        return v;
    }
}
