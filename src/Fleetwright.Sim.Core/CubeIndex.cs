namespace Fleetwright.Sim.Core;

/// <summary>The world's one structure for "what is near here" (docs/battle/fighting.md decision 11): items by index in
/// a grid of cubes in Earth-centred space, the cubes hashed into a fixed number of buckets (a collision only adds
/// candidates, so no poles or faces to special-case). A query returns everything in the 27 cubes round a point, each
/// once, in index order: with cubes at least as wide as the query's reach, every item within it is among them. Grown
/// only to more items; a rebuild and a query allocate nothing.
/// <para>Its users bring their own question: the contacts index units on rails by where they were anchored (a cube
/// as wide as they can reach before the next scan); the world's ship index holds every ship where it is now.</para></summary>
public sealed class CubeIndex
{
    const int Buckets = 4096;   // a power of two
    readonly int[] head = new int[Buckets];   // index + 1 of the first item in a bucket, 0 for none
    int[] next = [], seen = [], found = [];
    int stamp;

    /// <summary>The cubes' width, metres.</summary>
    public double Cell { get; private set; }

    /// <summary>What the last <see cref="Near"/> found, in index order.</summary>
    public ReadOnlySpan<int> Found(int count) => found.AsSpan(0, count);

    /// <summary>Empties it for items with indices below <paramref name="capacity"/>, in cubes this wide.</summary>
    public void Clear(int capacity, double cell)
    {
        if (next.Length < capacity)
        {
            int n = Math.Max(capacity, next.Length * 2);
            Array.Resize(ref next, n);
            Array.Resize(ref seen, n);
            Array.Resize(ref found, n);
        }
        Array.Clear(head);
        Cell = cell;
    }

    public void Add(int index, Vec3d p)
    {
        int b = Bucket(Cube(p.X), Cube(p.Y), Cube(p.Z));
        next[index] = head[b];
        head[b] = index + 1;
    }

    long Cube(double x) => (long)Math.Floor(x / Cell);

    static int Bucket(long x, long y, long z) => (int)((x * 73856093 ^ y * 19349663 ^ z * 83492791) & (Buckets - 1));

    /// <summary>The items in the 27 cubes round a point, each once, in index order (<see cref="Found"/>); how many.</summary>
    public int Near(Vec3d p)
    {
        long cx = Cube(p.X), cy = Cube(p.Y), cz = Cube(p.Z);
        int n = 0;
        stamp++;
        for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
                for (long dz = -1; dz <= 1; dz++)
                    for (int j = head[Bucket(cx + dx, cy + dy, cz + dz)] - 1; j >= 0; j = next[j] - 1)
                        if (seen[j] != stamp)
                        {
                            seen[j] = stamp;
                            found[n++] = j;
                        }
        Array.Sort(found, 0, n);
        return n;
    }
}
