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

    /// <summary>A querier's scratch: one per worker lane (docs/battle/workers.md W13).</summary>
    sealed class Lane
    {
        public int[] Found = new int[64];
        public readonly CubeIndex.Query Wide = new(), Fine = new();
        public double Wider;   // the widest question past the cubes asked during a dispatch (0: none)
    }

    Lane[] lanes = [new()];
    bool frozen;   // made ready for a dispatch: every side built, nothing rebuilt or grown until Thaw

    /// <summary>Before a dispatch of thinks on the workers (docs/battle/workers.md W13): a querier's scratch for each of
    /// <paramref name="lanes"/>, and no widening until <see cref="Thaw"/> (a question past the cubes looks at every
    /// contact). A side is built by the first to ask at a moment, the rest waiting for it.</summary>
    public void Prepare(int lanes)
    {
        if (this.lanes.Length < lanes)
        {
            int had = this.lanes.Length;
            Array.Resize(ref this.lanes, lanes);
            for (int i = had; i < lanes; i++)
                this.lanes[i] = new Lane();
        }
        frozen = true;
    }

    /// <summary>After the dispatch: built and grown when asked again, wider for a question that was past the cubes.</summary>
    public void Thaw()
    {
        frozen = false;
        double wider = 0;
        foreach (var l in lanes)
            (wider, l.Wider) = (Math.Max(wider, l.Wider), 0);
        if (wider > cell)
            Widen(wider);
    }

    void Widen(double radiusM)
    {
        cell = radiusM * 1.25;   // a wider question than the cubes: every side's grid is rebuilt wider
        foreach (var s in sides)
            if (s != null)
                s.BuiltMs = long.MinValue;
    }

    /// <summary>The contacts a side holds in sight within a radius of a point now, by their index in its picture, in
    /// index order; their positions from <see cref="Position"/>.</summary>
    public ReadOnlySpan<int> Near(World world, byte side, Vec3d p, double radiusM)
    {
        if (radiusM > cell && frozen)
            return Scan(world, side, p, radiusM);   // wider than the cubes during a dispatch: every contact looked at
        if (radiusM > cell)
            Widen(radiusM);
        var v = Build(world, side);
        if (v == null)
            return [];
        return Filter(v, v.Wide, p, radiusM, wide: true);
    }

    ReadOnlySpan<int> Scan(World world, byte side, Vec3d p, double radiusM)
    {
        var v = Build(world, side);
        if (v == null)
            return [];
        var lane = lanes[Workers.Lane];
        lane.Wider = Math.Max(lane.Wider, radiusM);
        var found = lane.Found;
        int m = 0;
        double r2 = radiusM * radiusM;
        for (int i = 0; i < v.Picture!.Count; i++)
            if (v.InSight[i] && (v.Position[i] - p).LengthSquared <= r2)
                found[m++] = i;
        return found.AsSpan(0, m);
    }

    /// <summary>As <see cref="Near"/>, within <see cref="SimSettings.FireLineGapM"/> (the fine grid).</summary>
    public ReadOnlySpan<int> Close(World world, byte side, Vec3d p)
    {
        var v = Build(world, side);
        return v == null ? [] : Filter(v, v.Fine, p, world.Settings.FireLineGapM, wide: false);
    }

    /// <summary>Where a side's picture puts its contact (by index) now, and its velocity; built by a
    /// <see cref="Near"/> or <see cref="Close"/> for that side at this moment.</summary>
    public Vec3d Position(byte side, int i) => sides[side]!.Position[i];

    public Vec3d Velocity(byte side, int i) => sides[side]!.Velocity[i];

    ReadOnlySpan<int> Filter(Side v, CubeIndex grid, Vec3d p, double radiusM, bool wide)
    {
        var lane = lanes[Workers.Lane];
        var q = wide ? lane.Wide : lane.Fine;
        int n = grid.Near(p, q), m = 0;
        if (lane.Found.Length < n)
            lane.Found = new int[Math.Max(n, lane.Found.Length * 2)];
        var found = lane.Found;
        var all = q.Found(n);
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
        long now = world.Time.Ms;
        var v = Volatile.Read(ref sides[side]);
        if (v != null && Volatile.Read(ref v.BuiltMs) == now && v.Picture == pic && v.Cell == cell)
            return v;
        lock (sides)
        {
            v = sides[side] ??= new Side();
            if (v.BuiltMs == now && v.Picture == pic && v.Cell == cell)
                return v;   // built meanwhile by another thread
            Fill(world, v, pic, now);
            return v;
        }
    }

    void Fill(World world, Side v, Picture pic, long now)
    {
        (v.Picture, v.Cell) = (pic, cell);
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
            v.InSight[i] = pic[i].InSight && !pic[i].Out;   // one seen going under or struck is no target, and no threat (striking.md SK3)
            if (!v.InSight[i])
                continue;
            var (p, vel, _) = pic.EstimateAt(i, now, world.Settings);
            (v.Position[i], v.Velocity[i]) = (p, vel);
            v.Wide.Add(i, p);
            v.Fine.Add(i, p);
        }
        Volatile.Write(ref v.BuiltMs, now);   // built: read from here on
    }
}
