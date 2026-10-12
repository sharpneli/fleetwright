namespace Fleetwright.Sim.Core;

/// <summary>The land, as rings on the sphere: coasts and the holes in them (lakes, the Caspian), each ring a list of
/// unit vectors joined by great-circle arcs. Immutable once loaded, so the map and the world can share one. A rough
/// first Earth (Natural Earth via world-atlas, tools/land_bake.py) until the user's Earth model replaces it; anything
/// that asks "land or sea?" asks this, never the data's format.
/// <para>Each ring has a bounding cap (all its points within an angle of a centre; Afro-Eurasia's and the Americas'
/// are over 90°), so a query or a view only walks the rings it comes near.</para></summary>
public sealed class Land
{
    const int Magic = 0x444C5746, Version = 1;

    readonly Vec3d[] points;
    readonly int[] start;          // ring i is points[start[i] .. start[i + 1])
    readonly Vec3d[] capCentre;
    readonly double[] capCos;      // every point of ring i has dot(p, capCentre[i]) >= capCos[i]
    readonly int[] chunkStart;     // ring i's chunks are chunkStart[i] .. chunkStart[i + 1] - 1
    readonly Vec3d[] chunkCentre;
    readonly double[] chunkCos;    // chunk j: its points and the next chunk's first, within its cap

    /// <summary>Points per chunk: a run of a ring with a cap of its own, so work far from a place skips it.</summary>
    public const int ChunkSize = 64;

    public int RingCount => start.Length - 1;
    public int PointCount => points.Length;

    public ReadOnlySpan<Vec3d> Ring(int i) => points.AsSpan(start[i], start[i + 1] - start[i]);

    /// <summary>A ring's bounding cap: its centre, and the cosine of its angular radius.</summary>
    public (Vec3d Centre, double Cos) Cap(int i) => (capCentre[i], capCos[i]);

    /// <summary>Ring i's chunks: chunk j (from 0) is its points j·ChunkSize up to the next chunk's first, which the
    /// chunk's cap also holds (so the whole run of arcs is inside it).</summary>
    public int ChunkCount(int i) => chunkStart[i + 1] - chunkStart[i];

    public (Vec3d Centre, double Cos) ChunkCap(int i, int j) => (chunkCentre[chunkStart[i] + j], chunkCos[chunkStart[i] + j]);

    Land(Vec3d[] points, int[] start)
    {
        this.points = points;
        this.start = start;
        int n = start.Length - 1;
        capCentre = new Vec3d[n];
        capCos = new double[n];
        for (int i = 0; i < n; i++)
        {
            var ring = Ring(i);
            var sum = Vec3d.Zero;
            foreach (var p in ring)
                sum += p;
            var c = sum.Normalized();
            double cos = 1;
            foreach (var p in ring)
                cos = Math.Min(cos, Vec3d.Dot(p, c));
            capCentre[i] = c;
            capCos[i] = cos;
        }
        chunkStart = new int[n + 1];
        for (int i = 0; i < n; i++)
            chunkStart[i + 1] = chunkStart[i] + (Ring(i).Length + ChunkSize - 1) / ChunkSize;
        chunkCentre = new Vec3d[chunkStart[n]];
        chunkCos = new double[chunkStart[n]];
        for (int i = 0; i < n; i++)
        {
            var ring = Ring(i);
            for (int j = 0; j < ChunkCount(i); j++)
            {
                int from = j * ChunkSize, to = Math.Min(from + ChunkSize, ring.Length);   // and ring[to % length]
                var sum = Vec3d.Zero;
                for (int k = from; k <= to; k++)
                    sum += ring[k % ring.Length];
                var c = sum.Normalized();
                double cos = 1;
                for (int k = from; k <= to; k++)
                    cos = Math.Min(cos, Vec3d.Dot(ring[k % ring.Length], c));
                chunkCentre[chunkStart[i] + j] = c;
                chunkCos[chunkStart[i] + j] = cos;
            }
        }
    }

    /// <summary>Reads a land file (tools/land_bake.py's format).</summary>
    public static Land Load(Stream stream)
    {
        using var r = new BinaryReader(stream);
        if (r.ReadInt32() != Magic || r.ReadInt32() != Version)
            throw new InvalidDataException("not a land file, or another version");
        int rings = r.ReadInt32();
        var start = new int[rings + 1];
        for (int i = 0; i < rings; i++)
            start[i + 1] = start[i] + r.ReadInt32();
        var points = new Vec3d[start[rings]];
        for (int k = 0; k < points.Length; k++)
        {
            double lon = r.ReadInt32() * 1e-6, lat = r.ReadInt32() * 1e-6;
            points[k] = Earth.Up(lat, lon);
        }
        return new Land(points, start);
    }

    public static Land Load(string path)
    {
        using var f = File.OpenRead(path);
        return Load(f);
    }

    /// <summary>Open sea, hundreds of kilometres from any coast, spread over the oceans: Point Nemo first. IsLand counts
    /// coasts along the arc to the nearest, so the arc stays short (the answer is the same from any of them).</summary>
    internal static readonly Vec3d[] References =
    [
        Earth.Up(-48.876, -123.393), Earth.Up(-40, 100), Earth.Up(30, -40), Earth.Up(-30, -15), Earth.Up(0, -150),
        Earth.Up(35, 165), Earth.Up(15, -125), Earth.Up(10, 65), Earth.Up(-15, 75), Earth.Up(-62, 120),
        Earth.Up(70, 2), Earth.Up(85, 0), Earth.Up(-25, -100), Earth.Up(55, -140), Earth.Up(20, 135),
    ];

    /// <summary>True when the point (any length; its direction counts) is on land: the great-circle arc from it to the
    /// nearest point of open sea crosses the coasts an odd number of times. Only rings and chunks whose cap the arc
    /// comes near are walked, by tests with no trig.</summary>
    public bool IsLand(Vec3d p)
    {
        var a = p.Normalized();
        var b = References[0];
        foreach (var r in References)
            if (Vec3d.Dot(a, r) > Vec3d.Dot(a, b))
                b = r;
        return (Crossings(a, b) & 1) == 1;
    }

    /// <summary>How many times the arc a-b (unit vectors, under half a turn) crosses the coasts.</summary>
    internal int Crossings(Vec3d a, Vec3d b)
    {
        var ab = Vec3d.Cross(a, b);
        if (ab.LengthSquared == 0)
            return 0;
        var n = ab.Normalized();
        int crossings = 0;
        for (int i = 0; i < RingCount; i++)
        {
            if (!ArcNearCap(a, b, n, capCentre[i], capCos[i]))
                continue;
            var ring = Ring(i);
            for (int j = 0; j < ChunkCount(i); j++)
            {
                int c = chunkStart[i] + j;
                if (!ArcNearCap(a, b, n, chunkCentre[c], chunkCos[c]))
                    continue;   // its arcs are all inside its cap, which the arc a-b misses
                int from = j * ChunkSize, to = Math.Min(from + ChunkSize, ring.Length);
                for (int k = from; k < to; k++)
                    if (Cross(a, b, ab, ring[k], ring[(k + 1) % ring.Length]))
                        crossings++;
            }
        }
        return crossings;
    }

    /// <summary>Whether the arc a-b (its circle's unit normal n) comes within a cap (centre c, cos of its radius),
    /// without trig: the circle is within the radius r of c when |c·n| <= sin r; the nearest point of the circle to c
    /// is on the arc, or else the nearer end must be within r.</summary>
    static bool ArcNearCap(Vec3d a, Vec3d b, Vec3d n, Vec3d c, double cos)
    {
        if (cos <= 0)
            return true;
        double sin = Math.Sqrt(Math.Max(0, 1 - cos * cos)), cn = Vec3d.Dot(c, n);
        if (Math.Abs(cn) > sin + 1e-12)
            return false;
        var foot = c - n * cn;
        if (foot.LengthSquared > 1e-24 && OnArc(a, b, n, foot))
            return true;
        return Math.Max(Vec3d.Dot(c, a), Vec3d.Dot(c, b)) >= cos - 1e-12;
    }

    /// <summary>Whether x (any length), on the circle of the arc a-b (normal n), lies between a and b.</summary>
    static bool OnArc(Vec3d a, Vec3d b, Vec3d n, Vec3d x) =>
        Vec3d.Dot(Vec3d.Cross(a, x), n) >= 0 && Vec3d.Dot(Vec3d.Cross(x, b), n) >= 0;

    /// <summary>Whether the arcs a-b (ab = a × b) and c-d cross.</summary>
    static bool Cross(Vec3d a, Vec3d b, Vec3d ab, Vec3d c, Vec3d d)
    {
        double sc = Vec3d.Dot(c, ab), sd = Vec3d.Dot(d, ab);
        if ((sc > 0) == (sd > 0))
            return false;   // c and d on the same side of a-b's circle
        var cd = Vec3d.Cross(c, d);
        double sa = Vec3d.Dot(a, cd), sb = Vec3d.Dot(b, cd);
        if ((sa > 0) == (sb > 0))
            return false;
        // the circles meet at ±x: the crossing is the one on both arcs
        var x = Vec3d.Cross(ab, cd);
        if (Vec3d.Dot(x, a + b) < 0)
            x = -x;
        return Vec3d.Dot(x, c + d) > 0;
    }
}
