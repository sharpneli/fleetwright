namespace Fleetwright.Sim.Core;

/// <summary>The land as the sim asks it (docs/world/terrain-plan.md, "Shore"): built once from the finest
/// <see cref="Land"/>, immutable after, so any thread may query it. Land or sea, how far to the coast, and where a
/// leg first comes within a clearance of it, none of which allocates.
/// <para>A grid of <see cref="CellsPerFace"/>² cells on each of the <see cref="CubeSphere"/>'s faces, uniform in the
/// gnomonic plane (~12 km at a face's centre, ~5 km at its corners). Each cell holds one signed short: 0 for a coast
/// cell (a coast segment touches it), else a lower bound on the distance from any point in it to the coast, in
/// <see cref="BoundUnitM"/> steps, plus one: positive at sea, negative on land. Out at sea that one lookup answers
/// nearly every query. Coast cells list their segments, sorted by cell, with an index per row of cells; each knows
/// whether its centre is land, so a point in it is answered by the crossings between the centre and the point.</para>
/// <para>Built deterministically on any number of threads: the segments are binned per face in parallel and sorted;
/// the kinds and the bounds are single passes. Edges lying on ±180° (Chukotka, Fiji) aren't coast and are left out;
/// they come in pairs, so leaving them out keeps every crossing count's parity.</para></summary>
public sealed class Shore
{
    /// <summary>Cells along a cube face's side.</summary>
    public const int CellsPerFace = 1024;

    /// <summary>The step of the stored distance bounds, metres.</summary>
    public const double BoundUnitM = 100;

    const int N = CellsPerFace, Shift = 10, FaceShift = 2 * Shift, FaceCells = N * N, Cells = CubeSphere.FaceCount * FaceCells;
    const double H = 2.0 / N;                   // a cell's side in the plane
    const double Pad = 1e-9;                    // a segment within this of a cell's edge is in both cells
    const int CapUnits = 3_000;                 // bounds stop at 300 km
    const double WidthSafety = 0.85;            // the bounds' cell widths, under the true ones
    const double MaxQueryAngle = 0.15;          // ~950 km: the most a query searches round a point
    const double Chunk = 0.001;                 // ~6.4 km: the exact tests' piece of a track
    const double SagM = 1;                      // a rhumb line's chords stay this close to it
    static readonly double FaceReach = Math.Acos(1 / Math.Sqrt(3)) + 1e-9;   // a face's corners from its centre

    public Land Land { get; }

    readonly Vec3d[] pts;           // the land's points
    readonly int[] next;            // segment k runs from pts[k] to pts[next[k]]
    readonly short[] cells;         // per cell: 0 coast, ±(1 + bound): + sea, - land
    readonly int[] rowStart;        // row r's (face·N + j) coast cells: coastCell[rowStart[r] .. rowStart[r + 1])
    readonly int[] coastCell;       // the coast cells' ids, ascending
    readonly int[] segStart;        // coast cell s's segments: segs[segStart[s] .. segStart[s + 1])
    readonly int[] segs;
    readonly bool[] centreLand;     // whether coast cell s's centre is land

    /// <summary>How many cells the coast touches.</summary>
    public int CoastCells => coastCell.Length;

    /// <summary>The index's memory, bytes (the land's own points aside).</summary>
    public long Bytes => 2L * cells.Length + 4L * (next.Length + rowStart.Length + coastCell.Length + segStart.Length + segs.Length)
        + centreLand.Length;

    public static Shore Build(Land land) => new(land);

    /// <summary>A hash of everything built, for the test that a build is the same whatever the threads.</summary>
    internal ulong ContentHash()
    {
        var h = new StateHash();
        foreach (short v in cells)
            h.Add(v);
        foreach (var a in (int[][])[rowStart, coastCell, segStart, segs])
            foreach (int v in a)
                h.Add(v);
        foreach (bool v in centreLand)
            h.Add(v);
        return h.Value;
    }

    Shore(Land land)
    {
        Land = land;
        pts = land.Points;
        next = new int[pts.Length];
        var keep = new bool[pts.Length];
        for (int r = 0; r < land.RingCount; r++)
        {
            int s0 = land.RingStart(r), s1 = land.RingStart(r + 1);
            for (int k = s0; k < s1; k++)
            {
                int n = k + 1 < s1 ? k + 1 : s0;
                next[k] = n;
                keep[k] = !(OnAntimeridian(pts[k]) && OnAntimeridian(pts[n]));
            }
        }

        // the segments into cells, a face per thread, each face's (cell, segment) pairs sorted
        var perFace = new long[CubeSphere.FaceCount][];
        Parallel.For(0, CubeSphere.FaceCount, f => perFace[f] = Bin(f, keep));
        int pairs = 0, coast = 0;
        foreach (var face in perFace)
        {
            pairs += face.Length;
            for (int q = 0; q < face.Length; q++)
                if (q == 0 || face[q] >> 32 != face[q - 1] >> 32)
                    coast++;
        }
        coastCell = new int[coast];
        segStart = new int[coast + 1];
        segs = new int[pairs];
        rowStart = new int[CubeSphere.FaceCount * N + 1];
        int c = -1, e = 0;
        for (int f = 0; f < CubeSphere.FaceCount; f++)
        {
            var face = perFace[f];
            for (int q = 0; q < face.Length; q++)
            {
                if (q == 0 || face[q] >> 32 != face[q - 1] >> 32)
                {
                    coastCell[++c] = f * FaceCells + (int)(face[q] >> 32);
                    segStart[c] = e;
                    rowStart[coastCell[c] / N + 1]++;
                }
                segs[e++] = (int)face[q];
            }
        }
        segStart[coast] = e;
        for (int r = 0; r < CubeSphere.FaceCount * N; r++)
            rowStart[r + 1] += rowStart[r];

        var slotOf = new int[Cells];
        Array.Fill(slotOf, -1);
        for (int s = 0; s < coast; s++)
            slotOf[coastCell[s]] = s;
        var kind = Kinds(land, slotOf);
        centreLand = new bool[coast];
        for (int s = 0; s < coast; s++)
            centreLand[s] = kind[coastCell[s]] < 0;
        cells = Bounds(slotOf, kind);
    }

    static bool OnAntimeridian(Vec3d p) => p.X < 0 && Math.Abs(p.Y) < 1e-12;

    /// <summary>A face's (cell, segment) pairs, sorted: every cell a segment comes within <see cref="Pad"/> of, so
    /// the cells cover each segment, and its crossing of any arc inside a cell is in that cell's list.</summary>
    long[] Bin(int face, bool[] keep)
    {
        var (f, _, _) = CubeSphere.Face(face);
        var list = new List<long>();
        for (int k = 0; k < pts.Length; k++)
        {
            if (!keep[k])
                continue;
            Vec3d a = pts[k], b = pts[next[k]];
            if (Vec3d.Dot(a, f) < 0.5 || Vec3d.Dot(b, f) < 0.5)
                continue;   // coast segments are under a degree, and the face's square is within 55° of its centre
            var (ax, ay) = CubeSphere.Project(a, face);
            var (bx, by) = CubeSphere.Project(b, face);
            double sx0 = Math.Min(ax, bx), sx1 = Math.Max(ax, bx), sy0 = Math.Min(ay, by), sy1 = Math.Max(ay, by);
            if (sx1 < -1 - Pad || sx0 > 1 + Pad || sy1 < -1 - Pad || sy0 > 1 + Pad)
                continue;
            int i0 = Index(sx0 - Pad), i1 = Index(sx1 + Pad);
            for (int i = i0; i <= i1; i++)
            {
                // the segment's y over this column, padded
                double xa = Math.Clamp(-1 + i * H, sx0, sx1), xb = Math.Clamp(-1 + (i + 1) * H, sx0, sx1);
                double ya, yb;
                if (bx - ax == 0)
                    (ya, yb) = (sy0, sy1);
                else
                {
                    ya = ay + (xa - ax) / (bx - ax) * (by - ay);
                    yb = ay + (xb - ax) / (bx - ax) * (by - ay);
                }
                double y0 = Math.Min(ya, yb) - Pad, y1 = Math.Max(ya, yb) + Pad;
                if (y1 < -1 || y0 > 1)
                    continue;
                for (int j = Index(y0), j1 = Index(y1); j <= j1; j++)
                    list.Add(((long)(j * N + i) << 32) | (uint)k);
            }
        }
        var pairs = list.ToArray();
        Array.Sort(pairs);
        return pairs;
    }

    /// <summary>Every cell's centre, land (-1) or sea (1): a face per thread, its first cell's centre from
    /// <see cref="Land.IsLand"/>, then along each row (and down the first column), the parity flipping with each coast
    /// crossed between neighbouring centres (centre to the shared edge's midpoint against one cell's segments, on to
    /// the next centre against the other's). The order is fixed, so the result is whatever the threads.</summary>
    sbyte[] Kinds(Land land, int[] slotOf)
    {
        var kind = new sbyte[Cells];
        Parallel.For(0, CubeSphere.FaceCount, face =>
        {
            int f0 = face << FaceShift;
            kind[f0] = land.IsLand(Centre(face, 0, 0)) ? (sbyte)-1 : (sbyte)1;
            for (int j = 0; j < N; j++)
            {
                if (j > 0)
                    kind[f0 + j * N] = Step(face, 0, j - 1, 0, 1);
                for (int i = 1; i < N; i++)
                    kind[f0 + j * N + i] = Step(face, i - 1, j, 1, 0);
            }
        });
        return kind;

        // the kind of the cell (di, dj) on from (i, j), known
        sbyte Step(int face, int i, int j, int di, int dj)
        {
            int c = (face << FaceShift) + j * N + i, n = c + dj * N + di;
            int sc = slotOf[c], sn = slotOf[n];
            if (sc < 0 && sn < 0)
                return kind[c];
            var mid = CubeSphere.Unproject(face, -1 + (i + 0.5 + 0.5 * di) * H, -1 + (j + 0.5 + 0.5 * dj) * H);
            int x = 0;
            if (sc >= 0)
                x += Crossings(Centre(face, i, j), mid, sc);
            if (sn >= 0)
                x += Crossings(mid, Centre(face, i + di, j + dj), sn);
            return (x & 1) == 1 ? (sbyte)-kind[c] : kind[c];
        }
    }

    /// <summary>Every cell's value: 0 on the coast, else ±(1 + a lower bound on the distance to it, in units). A
    /// shortest path from the coast cells over the cells' 8-neighbours (Dial's buckets: integer weights), each step
    /// the narrower cell's width (under its true one by <see cref="WidthSafety"/>). Any path from a point to the coast
    /// crosses whole rows or columns of cells as many times as its cell is steps from the coast, less one, so the
    /// path's length less the cell's own width bounds it from below.</summary>
    short[] Bounds(int[] slotOf, sbyte[] kind)
    {
        var width = new ushort[FaceCells];   // the same on every face
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
                width[j * N + i] = Width(i, j);
        int maxW = 0;
        foreach (var w in width)
            maxW = Math.Max(maxW, w);
        var g = new int[Cells];
        Array.Fill(g, int.MaxValue);
        int b = maxW + 1;
        var bucket = new int[b][];
        var count = new int[b];
        for (int q = 0; q < b; q++)
            bucket[q] = new int[1024];
        long pending = 0;
        foreach (int c in coastCell)
        {
            g[c] = 0;
            Push(0, c);
        }
        for (int at = 0; pending > 0 && at <= CapUnits + maxW; at++)
        {
            int q = at % b;
            for (int k = 0; k < count[q]; k++)
            {
                int c = bucket[q][k];
                pending--;
                if (g[c] != at)
                    continue;
                int face = c >> FaceShift, j = (c >> Shift) & (N - 1), i = c & (N - 1);
                int wc = width[c & (FaceCells - 1)];
                bool inside = i > 0 && i < N - 1 && j > 0 && j < N - 1;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0)
                            continue;
                        int n = inside ? c + dj * N + di : Neighbour(face, i, j, di, dj);
                        int ng = at + Math.Min(wc, width[n & (FaceCells - 1)]);
                        if (ng < g[n])
                        {
                            g[n] = ng;
                            Push(ng, n);
                        }
                    }
            }
            count[q] = 0;
        }
        var v = new short[Cells];
        for (int c = 0; c < Cells; c++)
        {
            if (slotOf[c] >= 0)
                continue;
            int bound = g[c] == int.MaxValue ? CapUnits : Math.Clamp(g[c] - width[c % FaceCells], 0, CapUnits);
            v[c] = (short)(kind[c] * (1 + bound));
        }
        return v;

        void Push(int at, int c)
        {
            int q = at % b;
            if (count[q] == bucket[q].Length)
                Array.Resize(ref bucket[q], 2 * bucket[q].Length);
            bucket[q][count[q]++] = c;
            pending++;
        }
    }

    /// <summary>A cell's width, in bound units: the lesser of its two heights (its sides through the plane's metric,
    /// times the sine of the grid lines' angle there), at whichever corner makes it smallest.</summary>
    static ushort Width(int i, int j)
    {
        double least = double.MaxValue;
        for (int cj = 0; cj <= 1; cj++)
            for (int ci = 0; ci <= 1; ci++)
            {
                double x = -1 + (i + ci) * H, y = -1 + (j + cj) * H;
                double rho2 = 1 + x * x + y * y;
                double sx = H * Math.Sqrt(1 + y * y) / rho2, sy = H * Math.Sqrt(1 + x * x) / rho2;
                double cos = Math.Abs(x * y) / Math.Sqrt((1 + x * x) * (1 + y * y));
                double sin = Math.Sqrt(1 - cos * cos);
                least = Math.Min(least, Math.Min(sx, sy) * sin);
            }
        return (ushort)Math.Max(1, Math.Floor(least * Earth.Radius * WidthSafety / BoundUnitM));
    }

    static int Index(double t)
    {
        int i = (int)Math.Floor((t + 1) * (N / 2.0));
        return i < 0 ? 0 : i >= N ? N - 1 : i;
    }

    static int CellOf(Vec3d p, out int face, out int i, out int j)
    {
        face = CubeSphere.FaceOf(p);
        var (x, y) = CubeSphere.Project(p, face);
        i = Index(x);
        j = Index(y);
        return face * FaceCells + j * N + i;
    }

    static Vec3d Centre(int face, int i, int j) => CubeSphere.Unproject(face, -1 + (i + 0.5) * H, -1 + (j + 0.5) * H);

    /// <summary>The cell one step (di, dj) from a cell; across a seam, the cell a quarter of a cell past the edge is
    /// in (the faces' grids meet cell to cell).</summary>
    static int Neighbour(int face, int i, int j, int di, int dj)
    {
        int ni = i + di, nj = j + dj;
        if ((uint)ni < N && (uint)nj < N)
            return face * FaceCells + nj * N + ni;
        var p = CubeSphere.Unproject(face, -1 + (i + 0.5 + 0.75 * di) * H, -1 + (j + 0.5 + 0.75 * dj) * H);
        return CellOf(p, out _, out _, out _);
    }

    /// <summary>A coast cell's slot, or -1.</summary>
    int Slot(int cell)
    {
        int row = cell / N, lo = rowStart[row], n = rowStart[row + 1] - lo;
        return n == 0 ? -1 : Math.Max(-1, Array.BinarySearch(coastCell, lo, n, cell));
    }

    /// <summary>The arc a-b's crossings with coast cell s's segments.</summary>
    int Crossings(Vec3d a, Vec3d b, int s)
    {
        var ab = Vec3d.Cross(a, b);
        if (ab.LengthSquared == 0)
            return 0;
        int x = 0;
        for (int e = segStart[s]; e < segStart[s + 1]; e++)
        {
            int k = segs[e];
            if (Land.Cross(a, b, ab, pts[k], pts[next[k]]))
                x++;
        }
        return x;
    }

    /// <summary>True when the point (any length; its direction counts) is on land. The same answer as
    /// <see cref="Land.IsLand"/>, in one lookup away from the coast and a few segment tests on it.</summary>
    public bool IsLand(Vec3d p)
    {
        var q = p.Normalized();
        int c = CellOf(q, out int face, out int i, out int j);
        short v = cells[c];
        if (v != 0)
            return v < 0;
        int s = Slot(c);
        return centreLand[s] ^ ((Crossings(Centre(face, i, j), q, s) & 1) == 1);
    }

    /// <summary>A lower bound on the distance from the point to the coast, metres: 0 in a cell the coast touches, up
    /// to 300 km. One lookup.</summary>
    public double BoundM(Vec3d p)
    {
        short v = cells[CellOf(p, out _, out _, out _)];
        return v == 0 ? 0 : (Math.Abs(v) - 1) * BoundUnitM;
    }

    /// <summary>The distance from the point to the nearest coast within <paramref name="maxM"/> (up to ~950 km),
    /// metres, and the nearest point of it (a unit vector); +∞ and zero if the coast is no nearer. The point may be
    /// on land or at sea (<see cref="IsLand"/> says which).</summary>
    public double Clearance(Vec3d p, double maxM, out Vec3d nearest)
    {
        nearest = default;
        var q = p.Normalized();
        short v = cells[CellOf(q, out _, out _, out _)];
        if (v != 0 && (Math.Abs(v) - 1) * BoundUnitM >= maxM)
            return double.PositiveInfinity;
        double theta = Math.Min(maxM / Earth.Radius, MaxQueryAngle), best = theta;
        var walk = new SlotWalk(this, q, theta);
        while (walk.MoveNext())
            for (int e = segStart[walk.Slot]; e < segStart[walk.Slot + 1]; e++)
            {
                int k = segs[e];
                double d = ArcDistance(q, pts[k], pts[next[k]], best, out var foot);
                if (d < best)
                {
                    best = d;
                    nearest = foot;
                }
            }
        return best < theta ? best * Earth.Radius : double.PositiveInfinity;
    }

    /// <summary>How far along the great circle from a point, heading along <paramref name="forward"/> (any vector
    /// with a part along the surface), the track first comes within <paramref name="clearanceM"/> of the coast,
    /// metres; +∞ if not within <paramref name="lengthM"/>; 0 if the start is on land or already that close. Out at
    /// sea it steps by the cells' bounds; near the coast it solves each segment exactly over pieces of the track.</summary>
    public double FirstContact(Vec3d from, Vec3d forward, double lengthM, double clearanceM)
    {
        var a = from.Normalized();
        if (IsLand(a))
            return 0;
        var t = (forward - a * Vec3d.Dot(forward, a)).Normalized();
        return Trace(a, t, lengthM / Earth.Radius, clearanceM / Earth.Radius) * Earth.Radius;
    }

    /// <summary>Whether the great circle from a to b (under half a turn) keeps <paramref name="clearanceM"/> off the
    /// coast all the way, and neither end is on land.</summary>
    public bool ArcClear(Vec3d a, Vec3d b, double clearanceM)
    {
        var u = a.Normalized();
        var w = b.Normalized();
        var t = w - u * Vec3d.Dot(w, u);
        if (t.LengthSquared < 1e-30)
            return !IsLand(u) && double.IsPositiveInfinity(Clearance(u, clearanceM, out _));
        return double.IsPositiveInfinity(FirstContact(u, t, Earth.Distance(u, w), clearanceM));
    }

    /// <summary>The <see cref="FirstContact"/> of a rhumb line (a held compass course, <see cref="Earth.Rhumb"/>):
    /// traced as chords short enough to stay within a metre of it, each solved as a great circle with the clearance
    /// grown by that metre. Stops within a degree of a pole, where the helm steers great circles instead.</summary>
    public double FirstContactRhumb(Vec3d from, double courseDeg, double lengthM, double clearanceM)
    {
        var a = from.Normalized();
        if (IsLand(a))
            return 0;
        double r = clearanceM / Earth.Radius, sinC = Math.Abs(Math.Sin(courseDeg * Math.PI / 180));
        double s = 0;
        while (s <= lengthM)
        {
            Earth.Rhumb(a, courseDeg, s, out var x, out _);
            if (Math.Abs(x.Z) > Math.Sin(89 * Math.PI / 180))
                break;
            short v = cells[CellOf(x, out _, out _, out _)];
            if (v < 0)
                return s;
            double free = v == 0 ? 0 : (v - 1) * BoundUnitM - clearanceM;
            if (free > Chunk * Earth.Radius / 4)
            {
                s += free;
                continue;
            }
            // a chord, short enough that its sagitta (Δ²κ/8, κ the rhumb line's geodesic curvature) stays under SagM
            double kappa = sinC * Math.Abs(x.Z) / Math.Sqrt(Math.Max(1e-12, 1 - x.Z * x.Z)) / Earth.Radius;
            double step = Math.Min(Chunk * Earth.Radius, kappa > 0 ? Math.Sqrt(8 * SagM / kappa) : double.MaxValue);
            double e = Math.Min(s + step, lengthM);
            Earth.Rhumb(a, courseDeg, e, out var y, out _);
            var t = y - x * Vec3d.Dot(y, x);
            double hit = t.LengthSquared < 1e-30
                ? (double.IsPositiveInfinity(Clearance(x, clearanceM, out _)) ? double.PositiveInfinity : 0)
                : ChunkContact(x, t.Normalized(), 0, Earth.Angle(x, y), r + SagM / Earth.Radius);
            if (hit < double.PositiveInfinity)
                return s + hit * Earth.Radius;
            if (e >= lengthM)
                break;
            s = e;
        }
        return double.PositiveInfinity;
    }

    /// <summary>Along x(s) = a cos s + t sin s (a, t orthonormal), the first s in [0, length] within the angle r of the
    /// coast, radians; +∞ if none. Steps by the bound where it's well over r, else solves a chunk exactly.</summary>
    double Trace(Vec3d a, Vec3d t, double length, double r)
    {
        const double unit = BoundUnitM / Earth.Radius;
        double s = 0;
        while (s <= length)
        {
            var x = a * Math.Cos(s) + t * Math.Sin(s);
            short v = cells[CellOf(x, out _, out _, out _)];
            if (v < 0)
                return s;   // only from a start on land: a step never crosses a coast
            double free = v == 0 ? 0 : (v - 1) * unit - r;
            if (free > Chunk / 4)
            {
                s += free;
                continue;
            }
            double e = Math.Min(s + Chunk, length);
            double hit = ChunkContact(a, t, s, e, r);
            if (hit < double.PositiveInfinity)
                return hit;
            if (e >= length)
                break;
            s = e;
        }
        return double.PositiveInfinity;
    }

    /// <summary>The first s in [s0, s1] where x(s) comes within r of any segment of the coast cells near the piece.</summary>
    double ChunkContact(Vec3d a, Vec3d t, double s0, double s1, double r)
    {
        double mid = (s0 + s1) / 2, best = double.PositiveInfinity;
        var walk = new SlotWalk(this, a * Math.Cos(mid) + t * Math.Sin(mid), (s1 - s0) / 2 + r);
        var trackNormal = Vec3d.Cross(a, t);
        while (walk.MoveNext())
            for (int e = segStart[walk.Slot]; e < segStart[walk.Slot + 1]; e++)
            {
                int k = segs[e];
                double h = ArcFirst(a, t, trackNormal, s0, Math.Min(s1, best), r, pts[k], pts[next[k]]);
                if (h < best)
                    best = h;
            }
        return best;
    }

    /// <summary>The first s in [s0, s1] (under a quarter turn long) where x(s) = a cos s + t sin s is within the angle
    /// r of the arc A-B; +∞ if none. The points within r of an arc are its ends' caps and the band about its circle
    /// beside it, and each is a sinusoid in s, so each is solved in closed form.</summary>
    static double ArcFirst(Vec3d a, Vec3d t, Vec3d trackNormal, double s0, double s1, double r, Vec3d A, Vec3d B)
    {
        if (s1 < s0)
            return double.PositiveInfinity;
        double mid = (s0 + s1) / 2, best = double.PositiveInfinity;
        CapFirst(a, t, trackNormal, s0, s1, mid, r, A, ref best);
        CapFirst(a, t, trackNormal, s0, s1, mid, r, B, ref best);
        var nab = Vec3d.Cross(A, B);
        double len = nab.Length;
        if (len < 1e-15)
            return best;
        var n = nab / len;
        // beside the arc: x·(n × A) >= 0 and x·(B × n) >= 0 (the foot on the circle between A and B)
        double lo = s0, hi = Math.Min(s1, best);
        if (!HalfTurn(a, t, Vec3d.Cross(n, A), mid, ref lo, ref hi) || !HalfTurn(a, t, Vec3d.Cross(B, n), mid, ref lo, ref hi))
            return best;
        // and within the band |x·n| <= sin r, about the zeros of x·n
        double sr = Math.Sin(r);
        var (m, phi) = Sinusoid(a, t, n);
        if (m <= sr)
            return Math.Min(best, lo);
        double half = Math.Asin(sr / m), z = phi + Math.PI / 2;
        z += Math.PI * Math.Round((mid - z) / Math.PI);
        for (int k = -1; k <= 1; k++)
        {
            double zk = z + k * Math.PI, from = Math.Max(lo, zk - half), to = Math.Min(hi, zk + half);
            if (from <= to && from < best)
                best = from;
        }
        return best;
    }

    /// <summary>The first s in [s0, s1] within r of the point A (a cap), into best. Its sinusoid x·A = m cos(s - φ),
    /// with 1 - m = d²/(1 + m) for d the track's normal on A, so the cap's half-width is exact at a few metres.</summary>
    static void CapFirst(Vec3d a, Vec3d t, Vec3d trackNormal, double s0, double s1, double mid, double r, Vec3d A, ref double best)
    {
        double d = Vec3d.Dot(trackNormal, A);
        double m = Math.Sqrt(Math.Max(0, 1 - d * d));
        double sh = Math.Sin(r / 2), gap = 2 * sh * sh - d * d / (1 + m);   // m - cos r
        if (gap < 0 || m == 0)
            return;
        double half = 2 * Math.Asin(Math.Min(1, Math.Sqrt(gap / m / 2)));
        double phi = Math.Atan2(Vec3d.Dot(t, A), Vec3d.Dot(a, A));
        phi += 2 * Math.PI * Math.Round((mid - phi) / (2 * Math.PI));
        double from = Math.Max(s0, phi - half), to = Math.Min(s1, phi + half);
        if (from <= to && from < best)
            best = from;
    }

    /// <summary>Narrows [lo, hi] (short) to where x(s)·w >= 0, a half turn about its sinusoid's peak; false if empty.</summary>
    static bool HalfTurn(Vec3d a, Vec3d t, Vec3d w, double mid, ref double lo, ref double hi)
    {
        var (m, phi) = Sinusoid(a, t, w);
        if (m < 1e-15)
            return lo <= hi;
        phi += 2 * Math.PI * Math.Round((mid - phi) / (2 * Math.PI));
        lo = Math.Max(lo, phi - Math.PI / 2);
        hi = Math.Min(hi, phi + Math.PI / 2);
        return lo <= hi;
    }

    /// <summary>x(s)·w = m cos(s - φ).</summary>
    static (double M, double Phi) Sinusoid(Vec3d a, Vec3d t, Vec3d w)
    {
        double ca = Vec3d.Dot(a, w), ct = Vec3d.Dot(t, w);
        return (Math.Sqrt(ca * ca + ct * ct), Math.Atan2(ct, ca));
    }

    /// <summary>The angle from q to the arc A-B, and its nearest point; a cheap early out when it can't beat best.</summary>
    static double ArcDistance(Vec3d q, Vec3d A, Vec3d B, double best, out Vec3d foot)
    {
        foot = default;
        var nab = Vec3d.Cross(A, B);
        double len = nab.Length;
        if (len > 1e-15)
        {
            var n = nab / len;
            double s = Vec3d.Dot(q, n);
            if (Math.Abs(s) >= Math.Sin(best))
                return double.PositiveInfinity;   // the whole circle is no nearer
            var f = q - n * s;
            if (f.LengthSquared > 1e-30 && Land.OnArc(A, B, n, f))
            {
                foot = f.Normalized();
                return Math.Asin(Math.Abs(s));
            }
        }
        double da = Earth.Angle(q, A), db = Earth.Angle(q, B);
        foot = da <= db ? A : B;
        return Math.Min(da, db);
    }

    /// <summary>The coast cells that may hold a segment within the angle theta of q: on each face the cap can reach,
    /// the plane's box about q's projection, wide enough for the projection's stretch (sec² of the angle from the
    /// face's centre), row by row through the sorted coast cells.</summary>
    ref struct SlotWalk
    {
        readonly Shore shore;
        readonly Vec3d q;
        readonly double theta, reach;
        int face, j, j1, i0, i1, k, end;

        public int Slot;

        public SlotWalk(Shore shore, Vec3d q, double theta)
        {
            this.shore = shore;
            this.q = q;
            this.theta = Math.Min(theta, MaxQueryAngle);
            reach = Math.Cos(FaceReach + this.theta);
            face = -1;
            j = j1 = k = end = 0;
        }

        public bool MoveNext()
        {
            while (true)
            {
                if (k < end)
                {
                    Slot = k++;
                    return true;
                }
                if (j < j1)
                {
                    j++;
                    Row();
                    continue;
                }
                if (!NextFace())
                    return false;
            }
        }

        bool NextFace()
        {
            while (++face < CubeSphere.FaceCount)
            {
                var (f, _, _) = CubeSphere.Face(face);
                double d = Vec3d.Dot(q, f);
                if (d < reach)
                    continue;
                var (x, y) = CubeSphere.Project(q, face);
                double c = Math.Cos(Math.Acos(Math.Min(1, d)) + theta);
                double r = theta / (c * c) * 1.001 + 1e-12;
                if (x + r < -1 || x - r > 1 || y + r < -1 || y - r > 1)
                    continue;
                (i0, i1) = (Index(x - r), Index(x + r));
                j = Index(y - r);
                j1 = Index(y + r);
                Row();
                return true;
            }
            return false;
        }

        void Row()
        {
            int row = face * N + j, lo = shore.rowStart[row], hi = shore.rowStart[row + 1];
            int first = face * FaceCells + j * N;
            k = LowerBound(shore.coastCell, lo, hi, first + i0);
            end = LowerBound(shore.coastCell, k, hi, first + i1 + 1);
        }

        static int LowerBound(int[] a, int lo, int hi, int value)
        {
            while (lo < hi)
            {
                int m = (lo + hi) >>> 1;
                if (a[m] < value)
                    lo = m + 1;
                else
                    hi = m;
            }
            return lo;
        }
    }
}
