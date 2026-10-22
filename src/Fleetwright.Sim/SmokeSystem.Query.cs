using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>The smoke on a sight line (docs/battle/smoke.md Z2).
/// <list type="bullet">
/// <item><b>The index</b>, rebuilt every <see cref="SimSettings.SmokeIndexMs"/> of world time at a tick: every puff's
/// state then (its centre in the air's frame, where it stays), in trail order, in chunks of
/// <see cref="SimSettings.SmokeChunk"/> with a bounding sphere each (their centres and 3.5 σ along their long axes): smoke lies along trails,
/// and a trail is already in order along its run. The chunks are filed in a hash grid of cubes in the air's frame
/// (Earth-centred: no projection, anywhere on the globe) at levels <see cref="SimSettings.SmokeGridM"/> · 2^k, k 0-7,
/// each at the smallest level whose cells are as wide as its sphere, in the (at most eight) cells its box touches: old,
/// wide puffs high up, young ones low, never a chunk in hundreds of cells.</item>
/// <item><b>A query</b> is a pair: from an observer's eyes (up to four rows spanning them, each from its own place on
/// her) to three points along a target (the middles of her thirds) at three heights each (<see cref="SimSettings.SmokeBands"/>
/// of her silhouette's top), as optical depths. The line between their middles goes into the air's frame once and walks
/// the grid's cells along it at each level in use (Amanatides and Woo's 3D walk), each chunk met once; chunks whose
/// spheres miss it (by more than the lines spread) are skipped, the rest's puffs summed in the walk's order
/// (<see cref="SmokeModel.TauGrid"/>), stopping once every line is past <see cref="SmokeOpaqueTau"/>. A puff is worked
/// out on the one middle line unless the lines have spread apart at its place by more than
/// <see cref="SimSettings.SmokeLineSplit"/> of its width (<see cref="SmokeModel.Split"/>): near the ships, where a
/// ship's own young smoke lies, each line on its own.</item>
/// <item><b>The cache:</b> a pair's answer holds for <see cref="SimSettings.SmokeRefreshMs"/> of world time; pairs not
/// asked for a minute are let go when the index is rebuilt. World state (the looks and fire control read it, Z3), hashed.</item>
/// </list>
/// Nothing allocates once grown.</summary>
public sealed partial class SmokeSystem
{
    /// <summary>The most eye rows and bands a pair holds (<see cref="SmokeModel.PairEye"/>).</summary>
    public const int PairEyes = SmokeModel.PairEyes, PairPoints = SmokeModel.PairPoints, PairBands = SmokeModel.PairBands,
        PairTaus = PairEyes * PairPoints * PairBands;

    /// <summary>An optical depth past which more smoke changes nothing (T under 0.25%).</summary>
    public const double SmokeOpaqueTau = 6;

    // the index: puffs' states (centres in the air's frame), chunks with their spheres, and the grid of chunks: a bucket's
    // first entry, each entry's cell, chunk and next in its bucket; a chunk's stamp (the query that last met it)
    LinePuff[] ix = new LinePuff[1024];
    Vec3d[] chunkC = new Vec3d[64];
    double[] chunkR = new double[64], chunkLo = new double[64], chunkHi = new double[64];
    // the store laid out again in trail order at each rebuild (its walks run in memory order after)
    SmokePuff[] puffsB = new SmokePuff[1024];
    int[] ownerB = new int[1024], prevB = new int[1024], nextB = new int[1024];
    int[] chunkStart = new int[65], chunkStamp = new int[64];
    int ixCount, chunkCount, stamp;
    long indexMs = long.MinValue;
    const int Levels = 8;
    int gridBits = 14, levelsUsed;
    int[] gridHead = new int[1 << 14];   // 16k buckets: room for a thousand chunks before it grows (a Jutland's, once)
    long[] gridKey = new long[8192];
    int[] gridChunk = new int[8192], gridNext = new int[8192];
    int gridCount;

    // the pair cache: key (observer, target), until when, last asked, the depths (PairTaus each, eye-major)
    readonly Dictionary<long, int> pairOf = new(256);
    long[] pairKey = new long[256], pairUntil = new long[256], pairUsed = new long[256];
    double[] pairTau = new double[256 * PairTaus];
    int pairCount;

    /// <summary>Queries answered, and how many of them were worked out (the rest from the cache); puffs summed.</summary>
    public long Asked { get; private set; }
    public long Worked { get; private set; }
    public long PuffsSummed { get; private set; }
    public long PuffsTested { get; private set; }
    public long ChunksTested { get; private set; }

    /// <summary>The index's puffs and its chunks.</summary>
    public int Indexed => ixCount;
    public int Chunks => chunkCount;

    // each ship's smoke as seen from afar (Z6), summed from her trails' puffs at each rebuild: by entity, in the order
    // first met
    readonly Dictionary<int, int> plumeOf = new(64);
    PlumeSum[] plumeSum = new PlumeSum[64];
    SmokePlume[] plumes = new SmokePlume[64];
    int[] plumeEntity = new int[64];
    int plumeCount;

    /// <summary>A ship's smoke as seen from afar (docs/battle/smoke.md Z6), as of the index's last rebuild: every trail of
    /// hers (her funnels', her guns', her fires', a magazine's); false when she has none that shows.</summary>
    public bool PlumeOf(int entity, out SmokePlume plume)
    {
        if (plumeOf.TryGetValue(entity, out int k) && plumes[k].Shows)
        {
            plume = plumes[k];
            return true;
        }
        plume = default;
        return false;
    }

    /// <summary>The plumes of the last rebuild: how many, and each one's ship and summary.</summary>
    public int Plumes => plumeCount;
    public (int Entity, SmokePlume Plume) PlumeAt(int i) => (plumeEntity[i], plumes[i]);

    int PlumeSlot(int entity)
    {
        if (plumeOf.TryGetValue(entity, out int k))
            return k;
        if (plumeCount == plumes.Length)
        {
            Array.Resize(ref plumeSum, plumeCount * 2);
            Array.Resize(ref plumes, plumeCount * 2);
            Array.Resize(ref plumeEntity, plumeCount * 2);
        }
        plumeOf[entity] = plumeCount;
        plumeSum[plumeCount] = default;
        plumeEntity[plumeCount] = entity;
        return plumeCount++;
    }

    /// <summary>Rebuilds the index at a tick when it falls due (a lattice of world time), and lets go of the pairs no one
    /// has asked for a minute.</summary>
    void Index(long t)
    {
        if (indexMs != long.MinValue && t / settings.SmokeIndexMs == indexMs / settings.SmokeIndexMs)
            return;
        long p0 = Probes.Now;
        indexMs = t;
        double nowS = t / 1000.0;
        int chunk = Math.Max(settings.SmokeChunk, 1);
        ixCount = chunkCount = 0;
        if (ix.Length < live)
            Array.Resize(ref ix, Math.Max(live, ix.Length * 2));
        if (puffsB.Length < puffs.Length)
        {
            puffsB = new SmokePuff[puffs.Length];
            (ownerB, prevB, nextB) = (new int[puffs.Length], new int[puffs.Length], new int[puffs.Length]);
        }
        plumeOf.Clear();
        plumeCount = 0;
        var contrast = settings.SightSmokeContrast;
        double showsTau = settings.SightSmokeTau;
        for (int k = 0; k < sources.Count; k++)
        {
            var src = sources[k];
            if (src.Puffs == 0)
            {
                (src.Oldest, src.Newest) = (-1, -1);
                sources[k] = src;
                continue;
            }
            int n = 0, first = ixCount, plume = PlumeSlot(src.Entity);
            for (int i = src.Oldest; i >= 0; i = next[i])
            {
                if (n % chunk == 0)
                {
                    EnsureChunks(chunkCount + 1);
                    chunkStart[chunkCount++] = ixCount;
                }
                var st = SmokeModel.State(puffs[i], nowS, frame, air);
                int tint = (int)puffs[i].Tint;
                plumeSum[plume].Add(st, tint < contrast.Length ? contrast[tint] : 0, showsTau);
                int j = ixCount++;
                ix[j] = new LinePuff(st, frame.ToAir(st.Centre.Normalized() * Earth.Radius), frame.ToAir(st.Axis));   // where it is in the air now
                (puffsB[j], ownerB[j], prevB[j], nextB[j]) = (puffs[i], k, j > first ? j - 1 : -1, j + 1);
                n++;
            }
            nextB[ixCount - 1] = -1;
            (src.Oldest, src.Newest) = (first, ixCount - 1);
            sources[k] = src;
        }
        chunkStart[chunkCount] = ixCount;
        for (int k = 0; k < plumeCount; k++)
            plumes[k] = plumeSum[k].Plume(showsTau);
        // the store compacted into trail order: the index's order (swapped, no copy back)
        (puffs, puffsB) = (puffsB, puffs);
        (owner, ownerB) = (ownerB, owner);
        (prev, prevB) = (prevB, prev);
        (next, nextB) = (nextB, next);
        Array.Fill(owner, -1, ixCount, owner.Length - ixCount);
        (high, freeCount) = (ixCount, 0);
        // the hash's buckets: at least twice the entries (eight a chunk at most)
        while (1 << gridBits < 16 * chunkCount && gridBits < 22)
            gridBits++;
        if (gridHead.Length != 1 << gridBits)
            gridHead = new int[1 << gridBits];
        Array.Fill(gridHead, -1);
        gridCount = 0;
        levelsUsed = 0;
        for (int c = 0; c < chunkCount; c++)
        {
            (chunkC[c], chunkR[c]) = Sphere(chunkStart[c], chunkStart[c + 1]);
            (chunkLo[c], chunkHi[c]) = (double.MaxValue, double.MinValue);
            for (int i = chunkStart[c]; i < chunkStart[c + 1]; i++)
            {
                double sz = Math.Sqrt(ix[i].ReachZ2);
                (chunkLo[c], chunkHi[c]) = (Math.Min(chunkLo[c], ix[i].Z - sz), Math.Max(chunkHi[c], ix[i].Z + sz));
            }
            chunkStamp[c] = 0;
            int level = 0;
            while (level < Levels - 1 && settings.SmokeGridM * (1 << level) < 2 * chunkR[c])
                level++;
            levelsUsed |= 1 << level;
            double g = settings.SmokeGridM * (1 << level);
            var lo = chunkC[c] - new Vec3d(chunkR[c], chunkR[c], chunkR[c]);
            var hi = chunkC[c] + new Vec3d(chunkR[c], chunkR[c], chunkR[c]);
            for (long x = Cell(lo.X, g); x <= Cell(hi.X, g); x++)
                for (long y = Cell(lo.Y, g); y <= Cell(hi.Y, g); y++)
                    for (long z = Cell(lo.Z, g); z <= Cell(hi.Z, g); z++)
                        File(Key(level, x, y, z), c);
        }
        stamp = 0;
        // the pairs no one has asked for a minute let go (the last moved into their place)
        for (int q = pairCount - 1; q >= 0; q--)
            if (t - pairUsed[q] > settings.SmokePairKeepMs)
            {
                pairOf.Remove(pairKey[q]);
                int last = --pairCount;
                if (q != last)
                {
                    (pairKey[q], pairUntil[q], pairUsed[q]) = (pairKey[last], pairUntil[last], pairUsed[last]);
                    Array.Copy(pairTau, last * PairTaus, pairTau, q * PairTaus, PairTaus);
                    pairOf[pairKey[q]] = q;
                }
            }
        probes.Add(indexProbe, p0);
    }

    /// <summary>The chunks' spheres are this much wider, m: their test is against the chord between the ends, which dips
    /// under the sea's curve (18 m mid-way on 30 km) where a puff's own test measures across the curved line, so a puff at
    /// the edge of its reach is never left out by its chunk.</summary>
    const double ChordSlackM = 25;

    /// <summary>The sphere round index puffs [a, b): their mean centre, out to 3.5 σ_h past the furthest.</summary>
    (Vec3d, double) Sphere(int a, int b)
    {
        var centre = Vec3d.Zero;
        for (int i = a; i < b; i++)
            centre += ix[i].Centre;
        centre /= b - a;
        double radius = 0;
        for (int i = a; i < b; i++)
            radius = Math.Max(radius, (ix[i].Centre - centre).Length + ix[i].Reach);
        return (centre, radius + ChordSlackM);
    }

    void EnsureChunks(int n)
    {
        if (n + 1 <= chunkC.Length)
            return;
        int m = Math.Max(n + 1, chunkC.Length * 2);
        Array.Resize(ref chunkC, m);
        Array.Resize(ref chunkR, m);
        Array.Resize(ref chunkLo, m);
        Array.Resize(ref chunkHi, m);
        Array.Resize(ref chunkStamp, m);
        Array.Resize(ref chunkStart, m + 1);
    }

    static long Cell(double v, double g) => (long)Math.Floor(v / g);

    /// <summary>A cell's key: its level (3 bits) and its three coordinates, 20 bits each (the Earth is ±3,200 cells of
    /// 2 km across).</summary>
    static long Key(int level, long x, long y, long z) => (long)level << 60 | (x + (1 << 19)) << 40 | (y + (1 << 19)) << 20 | (z + (1 << 19));

    int Bucket(long key) => (int)((ulong)key * 0x9E3779B97F4A7C15UL >> (64 - gridBits));

    void File(long key, int c)
    {
        if (gridCount == gridKey.Length)
        {
            int m = gridKey.Length * 2;
            Array.Resize(ref gridKey, m);
            Array.Resize(ref gridChunk, m);
            Array.Resize(ref gridNext, m);
        }
        int b = Bucket(key), e = gridCount++;
        (gridKey[e], gridChunk[e], gridNext[e]) = (key, c, gridHead[b]);
        gridHead[b] = e;
    }

    /// <summary>The optical depths between an observer (slot) and a target (slot) now: from each of the observer's eye rows
    /// (<see cref="SmokeModel.PairEye"/>: her eyes, or four spanning them) to each of the target's
    /// <see cref="PairPoints"/> points at <see cref="PairBands"/> bands, row-major into <paramref name="tau"/> (zeros past
    /// her eyes); an eye's own by <see cref="SmokeModel.EyeTau"/>. From the cache while it holds, else worked out.</summary>
    public void Tau(World world, int observer, int target, Span<double> tau)
    {
        var store = world.Ships.Store;
        long now = world.Time.Ms, key = (long)store[observer].Entity << 32 | (uint)store[target].Entity;
        Asked++;
        if (pairOf.TryGetValue(key, out int q) && now < pairUntil[q])
        {
            pairUsed[q] = now;
            pairTau.AsSpan(q * PairTaus, PairTaus).CopyTo(tau);
            return;
        }
        long p0 = Probes.Now;
        Work(world, observer, target, tau);
        if (!pairOf.TryGetValue(key, out q))
        {
            q = pairCount++;
            if (q == pairKey.Length)
            {
                int m = pairKey.Length * 2;
                Array.Resize(ref pairKey, m);
                Array.Resize(ref pairUntil, m);
                Array.Resize(ref pairUsed, m);
                Array.Resize(ref pairTau, m * PairTaus);
            }
            pairOf[key] = q;
            pairKey[q] = key;
        }
        (pairUntil[q], pairUsed[q]) = (now + settings.SmokeRefreshMs, now);
        tau[..PairTaus].CopyTo(pairTau.AsSpan(q * PairTaus, PairTaus));
        Worked++;
        probes.Add(queryProbe, p0);
    }

    /// <summary>The pairs in the cache (benches): how many; pair q's observer and target (entities) and when it was last
    /// asked, ms; its depths (<see cref="Tau"/>'s).</summary>
    public int Pairs => pairCount;
    public (int Observer, int Target, long UsedMs) PairAt(int q) => ((int)(pairKey[q] >> 32), (int)(uint)pairKey[q], pairUsed[q]);
    public ReadOnlySpan<double> PairDepths(int q) => pairTau.AsSpan(q * PairTaus, PairTaus);

    /// <summary>A pair worked out from the index now, the cache neither read nor written (benches, tests).</summary>
    public void TauUncached(World world, int observer, int target, Span<double> tau) => Work(world, observer, target, tau);

    // a pair's lines while it's worked out: the one between the ships' middles, and the fan about it (each eye row's start
    // and each point's end, across it and along it, m); the heights; the depths so far on the middle line or with the
    // eyes apart (row by band, the same at every point) and with the points apart (the pair's layout)
    readonly double[] rowSide = new double[PairEyes], rowAlong = new double[PairEyes], pointSide = new double[PairPoints], pointAlong = new double[PairPoints];
    readonly double[] rowFrom = new double[PairEyes], bandTo = new double[PairBands];
    readonly double[] oneTau = new double[PairEyes * PairBands], splitTau = new double[PairTaus];
    SmokeLine middle;
    int rows;
    double spreadO, spreadT, split;

    /// <summary>A pair worked out from the index.</summary>
    void Work(World world, int observer, int target, Span<double> tau)
    {
        tau[..PairTaus].Clear();
        if (ixCount == 0 || !Prepare(world, observer, target, settings.SmokeLineSplit, out var a, out var b))
            return;
        if (++stamp == int.MaxValue)
        {
            Array.Clear(chunkStamp, 0, chunkCount);
            stamp = 1;
        }
        // the heights the lines span: their ends', and down by the Earth's curve mid-way
        var eyesUsed = rowFrom.AsSpan(0, rows);
        double lineLo = Math.Min(Min(eyesUsed), Min(bandTo)) - middle.Length * middle.Length / 4 * middle.Curve, lineHi = Math.Max(Max(eyesUsed), Max(bandTo));
        long summed = 0;
        for (int level = Levels - 1; level >= 0; level--)
            if ((levelsUsed >> level & 1) != 0 && Walk(level, a, b, lineLo, lineHi, ref summed))
                break;
        PuffsSummed += summed;
        Finish(tau);
    }

    /// <summary>A pair worked out on the given puffs (in the air's frame), every one, no index or cache, with a share for
    /// splitting the lines (0: every line on its own): the index's own lines and sums, for tests and benches.</summary>
    public void TauOf(World world, int observer, int target, ReadOnlySpan<LinePuff> puffs, double splitShare, Span<double> tau)
    {
        tau[..PairTaus].Clear();
        if (!Prepare(world, observer, target, splitShare, out _, out _))
            return;
        foreach (ref readonly var p in puffs)
            OnLines(p);
        Finish(tau);
    }

    /// <summary>The pair's lines set up (the observer's eye rows from their places on her, the target's points along her),
    /// their depths cleared; the middles' ends in the air's frame. False when the observer has no eyes.</summary>
    bool Prepare(World world, int observer, int target, double splitShare, out Vec3d a, out Vec3d b)
    {
        (a, b) = (default, default);
        var store = world.Ships.Store;
        var os = store[observer];
        var ts = store[target];
        var eyes = os.Handling.Class.Eyes;
        rows = Math.Min(eyes.Length, PairEyes);
        if (rows == 0)
            return false;
        var tc = ts.Handling.Class;
        for (int j = 0; j < PairBands; j++)
            bandTo[j] = tc.Silhouette.TopM * settings.SmokeBands[j];
        var o = os.Position.Normalized() * Earth.Radius;
        var t = ts.Position.Normalized() * Earth.Radius;
        (a, b) = (frame.ToAir(o), frame.ToAir(t));
        split = splitShare;
        middle = new SmokeLine(a, b, eyes[0].HeightM, bandTo[0], settings.SightRefraction);
        // each row from its eye's place on her, each point along the target in hers: their offsets from the middle line
        (spreadO, spreadT) = (0, 0);
        var of = os.Frame;
        for (int e = 0; e < rows; e++)
        {
            ref readonly var eye = ref eyes[SmokeModel.PairEye(e, eyes.Length)];
            rowFrom[e] = eye.HeightM;
            var at = frame.ToAir(of.ToWorld(eye.X, eye.Y, 0).Normalized() * Earth.Radius) - a;
            (rowSide[e], rowAlong[e]) = (Vec3d.Dot(at, middle.Side), Vec3d.Dot(at, middle.Along));
            spreadO = Math.Max(spreadO, Math.Sqrt(rowSide[e] * rowSide[e] + rowAlong[e] * rowAlong[e]));
        }
        var tf = ts.Frame;
        for (int q = 0; q < PairPoints; q++)
        {
            var at = frame.ToAir(tf.ToWorld(SmokeModel.PointX(q, tc.Hull.X0, tc.Hull.X1), 0, 0).Normalized() * Earth.Radius) - b;
            (pointSide[q], pointAlong[q]) = (Vec3d.Dot(at, middle.Side), Vec3d.Dot(at, middle.Along));
            spreadT = Math.Max(spreadT, Math.Sqrt(pointSide[q] * pointSide[q] + pointAlong[q] * pointAlong[q]));
        }
        Array.Clear(oneTau);
        Array.Clear(splitTau);
        return true;
    }

    /// <summary>The pair's depths out: the middle line's and the split lines' together, in the pair's layout.</summary>
    void Finish(Span<double> tau)
    {
        for (int e = 0; e < rows; e++)
            for (int q = 0; q < PairPoints; q++)
                for (int j = 0; j < PairBands; j++)
                {
                    int k = (e * PairPoints + q) * PairBands + j;
                    tau[k] = oneTau[e * PairBands + j] + splitTau[k];
                }
    }

    /// <summary>A puff on the pair's lines (<see cref="SmokeModel.Split"/>, <see cref="SmokeModel.TauFan"/>): the middle
    /// line, or the fan its place asks for (each eye's start, each point's end, or both). Whether it added anything.</summary>
    bool OnLines(in LinePuff p)
    {
        var eyesUsed = rowFrom.AsSpan(0, rows);
        double u = Math.Clamp(Vec3d.Dot(p.Centre - middle.From, middle.Along) / Math.Max(middle.Length, 1), 0, 1);
        var (eyesApart, pointsApart) = SmokeModel.Split(u, p.SigmaH, spreadO, spreadT, split);
        ReadOnlySpan<double> ss = eyesApart ? rowSide.AsSpan(0, rows) : SmokeModel.NoOffset, sa = eyesApart ? rowAlong.AsSpan(0, rows) : SmokeModel.NoOffset;
        double spread = Math.Max(spreadO, spreadT);
        return pointsApart
            ? SmokeModel.TauFan(middle, p, ss, sa, eyesUsed, pointSide, pointAlong, bandTo, spread, splitTau.AsSpan(0, rows * PairPoints * PairBands))
            : SmokeModel.TauFan(middle, p, ss, sa, eyesUsed, SmokeModel.NoOffset, SmokeModel.NoOffset, bandTo, spread, oneTau.AsSpan(0, rows * PairBands));
    }

    /// <summary>Every line of the pair past <see cref="SmokeOpaqueTau"/>.</summary>
    bool PairOpaque()
    {
        for (int e = 0; e < rows; e++)
            for (int q = 0; q < PairPoints; q++)
                for (int j = 0; j < PairBands; j++)
                    if (oneTau[e * PairBands + j] + splitTau[(e * PairPoints + q) * PairBands + j] < SmokeOpaqueTau)
                        return false;
        return true;
    }

    /// <summary>One level's cells along the chord between the ships' middles, a to b (Amanatides and Woo in 3D): their
    /// chunks' puffs onto the pair's lines; true once every line is opaque.</summary>
    bool Walk(int level, Vec3d a, Vec3d b, double lineLo, double lineHi, ref long summed)
    {
        double spread = Math.Max(spreadO, spreadT);
        double g = settings.SmokeGridM * (1 << level);
        var d = b - a;
        long cx = Cell(a.X, g), cy = Cell(a.Y, g), cz = Cell(a.Z, g);
        long ex = Cell(b.X, g), ey = Cell(b.Y, g), ez = Cell(b.Z, g);
        int sx = Math.Sign(d.X), sy = Math.Sign(d.Y), sz = Math.Sign(d.Z);
        double tx = Next(a.X, d.X, cx, sx, g), ty = Next(a.Y, d.Y, cy, sy, g), tz = Next(a.Z, d.Z, cz, sz, g);
        double dx = sx != 0 ? g / Math.Abs(d.X) : double.PositiveInfinity, dy = sy != 0 ? g / Math.Abs(d.Y) : double.PositiveInfinity,
            dz = sz != 0 ? g / Math.Abs(d.Z) : double.PositiveInfinity;
        for (int guard = 0; guard < 4096; guard++)
        {
            long key = Key(level, cx, cy, cz);
            for (int e = gridHead[Bucket(key)]; e >= 0; e = gridNext[e])
            {
                int c = gridChunk[e];
                if (gridKey[e] != key || chunkStamp[c] == stamp)
                    continue;
                chunkStamp[c] = stamp;
                ChunksTested++;
                if (chunkLo[c] > lineHi || chunkHi[c] < lineLo || Miss(chunkC[c], chunkR[c] + spread, a, b))
                    continue;   // over the lines or under them, or off to one side
                PuffsTested += chunkStart[c + 1] - chunkStart[c];
                for (int i = chunkStart[c]; i < chunkStart[c + 1]; i++)
                    if (OnLines(ix[i]))
                        summed++;
                if (PairOpaque())
                    return true;
            }
            if (cx == ex && cy == ey && cz == ez)
                break;
            if (tx <= ty && tx <= tz)
            {
                if (tx > 1)
                    break;
                cx += sx;
                tx += dx;
            }
            else if (ty <= tz)
            {
                if (ty > 1)
                    break;
                cy += sy;
                ty += dy;
            }
            else
            {
                if (tz > 1)
                    break;
                cz += sz;
                tz += dz;
            }
        }
        return false;
    }

    /// <summary>Along the chord (as a share of it), where it leaves its first cell on one axis.</summary>
    static double Next(double a, double d, long cell, int s, double g) =>
        s > 0 ? ((cell + 1) * g - a) / d : s < 0 ? (cell * g - a) / d : double.PositiveInfinity;

    static double Min(ReadOnlySpan<double> v)
    {
        double m = double.MaxValue;
        foreach (double x in v)
            m = Math.Min(m, x);
        return m;
    }

    static double Max(ReadOnlySpan<double> v)
    {
        double m = double.MinValue;
        foreach (double x in v)
            m = Math.Max(m, x);
        return m;
    }

    /// <summary>Whether a sphere misses the chord from a to b.</summary>
    static bool Miss(Vec3d c, double r, Vec3d a, Vec3d b)
    {
        var ab = b - a;
        double len2 = ab.LengthSquared, u = len2 > 0 ? Math.Clamp(Vec3d.Dot(c - a, ab) / len2, 0, 1) : 0;
        return (a + ab * u - c).LengthSquared > r * r;
    }

    void HashQuery(ref StateHash h)
    {
        h.Add(indexMs);
        h.Add(plumeCount);
        for (int k = 0; k < plumeCount; k++)
        {
            h.Add(plumeEntity[k]);
            h.Add(plumes[k].AreaM2);
            h.Add(plumes[k].Puffs.Side);
        }
        h.Add(pairCount);
        for (int q = 0; q < pairCount; q++)
        {
            h.Add(pairKey[q]);
            h.Add(pairUntil[q]);
            h.Add(pairUsed[q]);
            for (int j = 0; j < PairTaus; j++)
                h.Add(pairTau[q * PairTaus + j]);
        }
    }
}
