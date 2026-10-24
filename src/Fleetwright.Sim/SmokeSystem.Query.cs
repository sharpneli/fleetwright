using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>The smoke on a sight line (docs/battle/smoke.md Z2).
/// <list type="bullet">
/// <item><b>The index</b>, rebuilt every <see cref="SimSettings.SmokeIndexMs"/> of world time at a tick, and its young
/// part every <see cref="SimSettings.SmokeFreshMs"/> (docs/battle/smoke.md T2: the puffs laid since, and those under
/// <see cref="SimSettings.SmokeFreshS"/> old at the rebuild, unfiled and filed again as they are then): every puff's
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
    int[] chunkStart = new int[65];
    int ixCount, chunkCount;
    int indexVersion;   // bumped at every filing: the queries' chunk stamps start over
    long indexMs = long.MinValue, freshMs = long.MinValue;
    // where the young part starts: the full rebuild's entries, chunks, grid entries and levels; and its cut, s (puffs
    // born after it are the young part's)
    int fullIx, fullChunks, fullGrid, fullLevels;
    double youngCut;
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
    public long PuffsSummed => Sum(static q => q.PuffsSummed);
    public long PuffsTested => Sum(static q => q.PuffsTested);
    public long ChunksTested => Sum(static q => q.ChunksTested);

    long Sum(Func<Query, long> of)
    {
        long n = 0;
        foreach (var q in queries)
            n += of(q);
        return n;
    }

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

    /// <summary>Rebuilds the index at a tick when it falls due (a lattice of world time), and its young part when that
    /// does.</summary>
    void Index(World world, long t)
    {
        bool full = indexMs == long.MinValue || t / settings.SmokeIndexMs != indexMs / settings.SmokeIndexMs;
        if (full)
            Rebuild(world, t);
        if (full || t / settings.SmokeFreshMs != freshMs / settings.SmokeFreshMs)
            Fresh(world, t);
    }

    // the puffs' states at a rebuild, worked out on the workers (docs/battle/workers.md W4): each puff's state (the
    // eddies: the cost) into its slot of the compacted store's order, and its place on the lines if the full index files
    // it; the sums, the chunks and the filing stay on the sim thread in trail order
    PuffState[] stateB = new PuffState[1024];
    LinePuff[] lineB = new LinePuff[1024];
    int[] freshOf = new int[256];   // the young part's puffs, in the order it files them
    const int StateChunk = 64, StateMinPuffs = 512;

    /// <summary>Puffs <c>[from, to)</c> of <c>puffs</c> as they are at <c>nowS</c>: into <c>states</c> (if any) and, those
    /// born by <c>cut</c>, their lines into <c>lines</c>; or, with <c>of</c>, the puffs <c>of[k]</c> into slot k.</summary>
    readonly struct StateJob(SmokeSystem sys, SmokePuff[] puffs, int[]? of, PuffState[]? states, LinePuff[] lines, double nowS, double cut) : IJob
    {
        public void Run(int from, int to, int worker)
        {
            for (int k = from; k < to; k++)
            {
                ref readonly var p = ref puffs[of != null ? of[k] : k];
                var st = SmokeModel.State(p, nowS, sys.frame, sys.air);
                if (states != null)
                    states[k] = st;
                if (p.Born <= cut)
                    lines[k] = new LinePuff(st, sys.frame.ToAir(st.Centre.Normalized() * Earth.Radius), sys.frame.ToAir(st.Axis));   // where it is in the air now
            }
        }
    }

    /// <summary>The index rebuilt from every puff but the young (born after <see cref="youngCut"/>: the young part's),
    /// the store compacted into trail order, each ship's plume summed from all of hers; and the pairs no one has asked for
    /// a minute let go.</summary>
    void Rebuild(World world, long t)
    {
        long p0 = Probes.Now;
        indexMs = t;
        double nowS = t / 1000.0;
        youngCut = nowS - settings.SmokeFreshS;
        int chunk = Math.Max(settings.SmokeChunk, 1), sj = 0;
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
        // the store laid out in trail order, then every puff's state worked out on the workers, then the sums and the
        // index in that order
        for (int k = 0; k < sources.Count; k++)
        {
            var src = sources[k];
            if (src.Puffs == 0)
            {
                (src.Oldest, src.Newest) = (-1, -1);
                sources[k] = src;
                continue;
            }
            int first = sj;
            for (int i = src.Oldest; i >= 0; i = next[i])
            {
                int j = sj++;
                (puffsB[j], ownerB[j], prevB[j], nextB[j]) = (puffs[i], k, j > first ? j - 1 : -1, j + 1);
            }
            nextB[sj - 1] = -1;
            (src.Oldest, src.Newest) = (first, sj - 1);
            sources[k] = src;
        }
        if (stateB.Length < sj)
            (stateB, lineB) = (new PuffState[Math.Max(sj, 2 * stateB.Length)], new LinePuff[Math.Max(sj, 2 * lineB.Length)]);
        air.Eddies?.Turn(nowS);
        world.Workers.For(new StateJob(this, puffsB, null, stateB, lineB, nowS, youngCut), sj, StateChunk, StateMinPuffs);
        for (int k = 0; k < sources.Count; k++)
        {
            var src = sources[k];
            if (src.Puffs == 0)
                continue;
            int n = 0, plume = PlumeSlot(src.Entity);
            for (int j = src.Oldest; j <= src.Newest; j++)
            {
                int tint = (int)puffsB[j].Tint;
                plumeSum[plume].Add(stateB[j], tint < contrast.Length ? contrast[tint] : 0, showsTau);
                if (puffsB[j].Born > youngCut)
                    continue;   // young: the young part's
                if (n % chunk == 0)
                {
                    EnsureChunks(chunkCount + 1);
                    chunkStart[chunkCount++] = ixCount;
                }
                ix[ixCount++] = lineB[j];
                n++;
            }
        }
        chunkStart[chunkCount] = ixCount;
        for (int k = 0; k < plumeCount; k++)
            plumes[k] = plumeSum[k].Plume(showsTau);
        // the store compacted into trail order: the index's order (swapped, no copy back)
        (puffs, puffsB) = (puffsB, puffs);
        (owner, ownerB) = (ownerB, owner);
        (prev, prevB) = (prevB, prev);
        (next, nextB) = (nextB, next);
        Array.Fill(owner, -1, sj, owner.Length - sj);
        (high, freeCount) = (sj, 0);
        // the hash's buckets: at least twice the entries (eight a chunk at most)
        while (1 << gridBits < 16 * chunkCount && gridBits < 22)
            gridBits++;
        if (gridHead.Length != 1 << gridBits)
            gridHead = new int[1 << gridBits];
        Array.Fill(gridHead, -1);
        gridCount = 0;
        levelsUsed = 0;
        FileChunks(0, chunkCount);
        (fullIx, fullChunks, fullGrid, fullLevels) = (ixCount, chunkCount, gridCount, levelsUsed);
        indexVersion++;
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

    /// <summary>The index's young part (docs/battle/smoke.md T2): the last one's entries unfiled (in the reverse of their
    /// filing, so each bucket's head is as the full rebuild left it), then every trail's puffs born after the cut, newest
    /// back, as they are now, chunked and filed after the full rebuild's. A few hundred puffs: new smoke reaches the lines
    /// within <see cref="SimSettings.SmokeFreshMs"/> instead of <see cref="SimSettings.SmokeIndexMs"/>.</summary>
    void Fresh(World world, long t)
    {
        long p0 = Probes.Now;
        freshMs = t;
        for (int e = gridCount - 1; e >= fullGrid; e--)
            gridHead[Bucket(gridKey[e])] = gridNext[e];
        (ixCount, chunkCount, gridCount, levelsUsed) = (fullIx, fullChunks, fullGrid, fullLevels);
        double nowS = t / 1000.0;
        int chunk = Math.Max(settings.SmokeChunk, 1);
        // the young puffs in filing order, their lines worked out on the workers, then chunked in that order
        int young = 0;
        for (int k = 0; k < sources.Count; k++)
            for (int i = sources[k].Newest; i >= 0 && puffs[i].Born > youngCut; i = prev[i])
            {
                if (young == freshOf.Length)
                    Array.Resize(ref freshOf, 2 * young);
                freshOf[young++] = i;
            }
        if (lineB.Length < young)
            lineB = new LinePuff[Math.Max(young, 2 * lineB.Length)];
        air.Eddies?.Turn(nowS);
        world.Workers.For(new StateJob(this, puffs, freshOf, null, lineB, nowS, double.PositiveInfinity), young, StateChunk, StateMinPuffs);
        young = 0;
        for (int k = 0; k < sources.Count; k++)
        {
            int n = 0;
            for (int i = sources[k].Newest; i >= 0 && puffs[i].Born > youngCut; i = prev[i])
            {
                if (n % chunk == 0)
                {
                    EnsureChunks(chunkCount + 1);
                    chunkStart[chunkCount++] = ixCount;
                }
                if (ixCount == ix.Length)
                    Array.Resize(ref ix, ix.Length * 2);
                ix[ixCount++] = lineB[young++];
                n++;
            }
        }
        chunkStart[chunkCount] = ixCount;
        FileChunks(fullChunks, chunkCount);
        indexVersion++;
        probes.Add(indexProbe, p0);
    }

    /// <summary>Chunks [c0, c1) filed in the grid: each one's sphere and heights, at the smallest level whose cells are
    /// as wide as it, in the cells its box touches.</summary>
    void FileChunks(int c0, int c1)
    {
        for (int c = c0; c < c1; c++)
        {
            (chunkC[c], chunkR[c]) = Sphere(chunkStart[c], chunkStart[c + 1]);
            (chunkLo[c], chunkHi[c]) = (double.MaxValue, double.MinValue);
            for (int i = chunkStart[c]; i < chunkStart[c + 1]; i++)
            {
                double sz = Math.Sqrt(ix[i].ReachZ2);
                (chunkLo[c], chunkHi[c]) = (Math.Min(chunkLo[c], ix[i].Z - sz), Math.Max(chunkHi[c], ix[i].Z + sz));
            }
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
        if (Cached(world, observer, target, tau))
        {
            Answered(world, observer, target, tau, 0);
            return;
        }
        long p0 = Probes.Now;
        WorkOut(world, observer, target, tau, 0);
        Answered(world, observer, target, tau, Probes.Now - p0);
    }

    /// <summary><see cref="Tau"/> in its three parts, for a look's pairs asked on the workers (docs/battle/workers.md
    /// W3-W4): whether the cache holds the pair now (its depths into <paramref name="tau"/>), reading only; the pair
    /// worked out on a worker's own scratch (<see cref="WorkOut"/>); and, on the sim thread in the order the pairs were
    /// asked, the cache's bookkeeping as <see cref="Tau"/> does it (<see cref="Answered"/>), so the cache is the same
    /// whoever worked them out. A pair asked twice in a pass (two batteries of a ship on one target) is worked out twice,
    /// to the same depths, and the second's bookkeeping finds the first's answer held, as a hit.</summary>
    public bool Cached(World world, int observer, int target, Span<double> tau)
    {
        if (!pairOf.TryGetValue(PairKey(world.Ships.Store, observer, target), out int q) || world.Time.Ms >= pairUntil[q])
            return false;
        pairTau.AsSpan(q * PairTaus, PairTaus).CopyTo(tau);
        return true;
    }

    /// <summary>A pair worked out on participant <paramref name="worker"/>'s scratch (<see cref="PrepareQueries"/> first).</summary>
    public void WorkOut(World world, int observer, int target, Span<double> tau, int worker) => Work(world, observer, target, tau, queries[worker]);

    /// <summary>The cache's bookkeeping for a pair asked, its depths in <paramref name="tau"/>: held now, last asked now;
    /// else they go in, held for <see cref="SimSettings.SmokeRefreshMs"/> (the work <paramref name="ticks"/> long, for
    /// the probe).</summary>
    public void Answered(World world, int observer, int target, ReadOnlySpan<double> tau, long ticks)
    {
        long now = world.Time.Ms, key = PairKey(world.Ships.Store, observer, target);
        Asked++;
        bool known = pairOf.TryGetValue(key, out int q);
        if (known && now < pairUntil[q])
        {
            pairUsed[q] = now;
            return;
        }
        if (!known)
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
        probes.AddTicks(queryProbe, ticks);
    }

    /// <summary>A scratch for each of the workers' participants, made on the sim thread before a dispatch asks pairs.</summary>
    public void PrepareQueries(int participants)
    {
        if (queries.Length >= participants)
            return;
        int n = queries.Length;
        Array.Resize(ref queries, participants);
        for (int i = n; i < participants; i++)
            queries[i] = new Query();
    }

    static long PairKey(ShipStore store, int observer, int target) => (long)store[observer].Entity << 32 | (uint)store[target].Entity;

    /// <summary>The pairs in the cache (benches): how many; pair q's observer and target (entities) and when it was last
    /// asked, ms; its depths (<see cref="Tau"/>'s).</summary>
    public int Pairs => pairCount;
    public (int Observer, int Target, long UsedMs) PairAt(int q) => ((int)(pairKey[q] >> 32), (int)(uint)pairKey[q], pairUsed[q]);
    public ReadOnlySpan<double> PairDepths(int q) => pairTau.AsSpan(q * PairTaus, PairTaus);

    /// <summary>A pair worked out from the index now, the cache neither read nor written (benches, tests).</summary>
    public void TauUncached(World world, int observer, int target, Span<double> tau) => Work(world, observer, target, tau, queries[0]);

    /// <summary>A pair's scratch while it's worked out, one per worker (docs/battle/workers.md W4): the line between the
    /// ships' middles, and the fan about it (each eye row's start and each point's end, across it and along it, m); the
    /// heights; the depths so far on the middle line or with the eyes apart (row by band, the same at every point) and
    /// with the points apart (the pair's layout); each chunk's stamp (the query that last met it), good for one filing
    /// of the index; and what its queries tested and summed.</summary>
    sealed class Query
    {
        public readonly double[] RowSide = new double[PairEyes], RowAlong = new double[PairEyes], PointSide = new double[PairPoints],
            PointAlong = new double[PairPoints];
        public readonly double[] RowFrom = new double[PairEyes], BandTo = new double[PairBands];
        public readonly double[] OneTau = new double[PairEyes * PairBands], SplitTau = new double[PairTaus];
        public SmokeLine Middle;
        public int Rows;
        public double SpreadO, SpreadT, Split;
        public int[] ChunkStamp = new int[64];
        public int Stamp, Version = -1;
        public long PuffsSummed, PuffsTested, ChunksTested;
    }

    Query[] queries = [new()];

    /// <summary>A pair worked out from the index, on a scratch of its own.</summary>
    void Work(World world, int observer, int target, Span<double> tau, Query q)
    {
        tau[..PairTaus].Clear();
        if (ixCount == 0 || !Prepare(world, observer, target, settings.SmokeLineSplit, q, out var a, out var b))
            return;
        if (q.Version != indexVersion)
        {
            if (q.ChunkStamp.Length < chunkCount)
                q.ChunkStamp = new int[Math.Max(chunkCount, 2 * q.ChunkStamp.Length)];
            else
                Array.Clear(q.ChunkStamp, 0, chunkCount);
            (q.Stamp, q.Version) = (0, indexVersion);
        }
        if (++q.Stamp == int.MaxValue)
        {
            Array.Clear(q.ChunkStamp, 0, chunkCount);
            q.Stamp = 1;
        }
        // the heights the lines span: their ends', and down by the Earth's curve mid-way
        var eyesUsed = q.RowFrom.AsSpan(0, q.Rows);
        double lineLo = Math.Min(Min(eyesUsed), Min(q.BandTo)) - q.Middle.Length * q.Middle.Length / 4 * q.Middle.Curve,
            lineHi = Math.Max(Max(eyesUsed), Max(q.BandTo));
        long summed = 0;
        for (int level = Levels - 1; level >= 0; level--)
            if ((levelsUsed >> level & 1) != 0 && Walk(level, a, b, lineLo, lineHi, q, ref summed))
                break;
        q.PuffsSummed += summed;
        Finish(tau, q);
    }

    /// <summary>A pair worked out on the given puffs (in the air's frame), every one, no index or cache, with a share for
    /// splitting the lines (0: every line on its own): the index's own lines and sums, for tests and benches.</summary>
    public void TauOf(World world, int observer, int target, ReadOnlySpan<LinePuff> puffs, double splitShare, Span<double> tau)
    {
        tau[..PairTaus].Clear();
        var q = queries[0];
        if (!Prepare(world, observer, target, splitShare, q, out _, out _))
            return;
        foreach (ref readonly var p in puffs)
            OnLines(p, q);
        Finish(tau, q);
    }

    /// <summary>The pair's lines set up (the observer's eye rows from their places on her, the target's points along her),
    /// their depths cleared; the middles' ends in the air's frame. False when the observer has no eyes.</summary>
    bool Prepare(World world, int observer, int target, double splitShare, Query q, out Vec3d a, out Vec3d b)
    {
        (a, b) = (default, default);
        var store = world.Ships.Store;
        var os = store[observer];
        var ts = store[target];
        var eyes = os.Handling.Class.Eyes;
        q.Rows = Math.Min(eyes.Length, PairEyes);
        if (q.Rows == 0)
            return false;
        var tc = ts.Handling.Class;
        for (int j = 0; j < PairBands; j++)
            q.BandTo[j] = tc.Silhouette.TopM * settings.SmokeBands[j];
        var o = os.Position.Normalized() * Earth.Radius;
        var t = ts.Position.Normalized() * Earth.Radius;
        (a, b) = (frame.ToAir(o), frame.ToAir(t));
        q.Split = splitShare;
        q.Middle = new SmokeLine(a, b, eyes[0].HeightM, q.BandTo[0], settings.SightRefraction);
        // each row from its eye's place on her, each point along the target in hers: their offsets from the middle line
        (q.SpreadO, q.SpreadT) = (0, 0);
        var of = os.Frame;
        for (int e = 0; e < q.Rows; e++)
        {
            ref readonly var eye = ref eyes[SmokeModel.PairEye(e, eyes.Length)];
            q.RowFrom[e] = eye.HeightM;
            var at = frame.ToAir(of.ToWorld(eye.X, eye.Y, 0).Normalized() * Earth.Radius) - a;
            (q.RowSide[e], q.RowAlong[e]) = (Vec3d.Dot(at, q.Middle.Side), Vec3d.Dot(at, q.Middle.Along));
            q.SpreadO = Math.Max(q.SpreadO, Math.Sqrt(q.RowSide[e] * q.RowSide[e] + q.RowAlong[e] * q.RowAlong[e]));
        }
        var tf = ts.Frame;
        for (int k = 0; k < PairPoints; k++)
        {
            var at = frame.ToAir(tf.ToWorld(SmokeModel.PointX(k, tc.Hull.X0, tc.Hull.X1), 0, 0).Normalized() * Earth.Radius) - b;
            (q.PointSide[k], q.PointAlong[k]) = (Vec3d.Dot(at, q.Middle.Side), Vec3d.Dot(at, q.Middle.Along));
            q.SpreadT = Math.Max(q.SpreadT, Math.Sqrt(q.PointSide[k] * q.PointSide[k] + q.PointAlong[k] * q.PointAlong[k]));
        }
        Array.Clear(q.OneTau);
        Array.Clear(q.SplitTau);
        return true;
    }

    /// <summary>The pair's depths out: the middle line's and the split lines' together, in the pair's layout.</summary>
    void Finish(Span<double> tau, Query q)
    {
        for (int e = 0; e < q.Rows; e++)
            for (int p = 0; p < PairPoints; p++)
                for (int j = 0; j < PairBands; j++)
                {
                    int k = (e * PairPoints + p) * PairBands + j;
                    tau[k] = q.OneTau[e * PairBands + j] + q.SplitTau[k];
                }
    }

    /// <summary>A puff on the pair's lines (<see cref="SmokeModel.Split"/>, <see cref="SmokeModel.TauFan"/>): the middle
    /// line, or the fan its place asks for (each eye's start, each point's end, or both). Whether it added anything.</summary>
    bool OnLines(in LinePuff p, Query q)
    {
        var eyesUsed = q.RowFrom.AsSpan(0, q.Rows);
        double u = Math.Clamp(Vec3d.Dot(p.Centre - q.Middle.From, q.Middle.Along) / Math.Max(q.Middle.Length, 1), 0, 1);
        var (eyesApart, pointsApart) = SmokeModel.Split(u, p.SigmaH, q.SpreadO, q.SpreadT, q.Split);
        ReadOnlySpan<double> ss = eyesApart ? q.RowSide.AsSpan(0, q.Rows) : SmokeModel.NoOffset, sa = eyesApart ? q.RowAlong.AsSpan(0, q.Rows) : SmokeModel.NoOffset;
        double spread = Math.Max(q.SpreadO, q.SpreadT);
        return pointsApart
            ? SmokeModel.TauFan(q.Middle, p, ss, sa, eyesUsed, q.PointSide, q.PointAlong, q.BandTo, spread, q.SplitTau.AsSpan(0, q.Rows * PairPoints * PairBands))
            : SmokeModel.TauFan(q.Middle, p, ss, sa, eyesUsed, SmokeModel.NoOffset, SmokeModel.NoOffset, q.BandTo, spread, q.OneTau.AsSpan(0, q.Rows * PairBands));
    }

    /// <summary>Every line of the pair past <see cref="SmokeOpaqueTau"/>.</summary>
    static bool PairOpaque(Query q)
    {
        for (int e = 0; e < q.Rows; e++)
            for (int p = 0; p < PairPoints; p++)
                for (int j = 0; j < PairBands; j++)
                    if (q.OneTau[e * PairBands + j] + q.SplitTau[(e * PairPoints + p) * PairBands + j] < SmokeOpaqueTau)
                        return false;
        return true;
    }

    /// <summary>One level's cells along the chord between the ships' middles, a to b (Amanatides and Woo in 3D): their
    /// chunks' puffs onto the pair's lines; true once every line is opaque.</summary>
    bool Walk(int level, Vec3d a, Vec3d b, double lineLo, double lineHi, Query q, ref long summed)
    {
        double spread = Math.Max(q.SpreadO, q.SpreadT);
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
                if (gridKey[e] != key || q.ChunkStamp[c] == q.Stamp)
                    continue;
                q.ChunkStamp[c] = q.Stamp;
                q.ChunksTested++;
                if (chunkLo[c] > lineHi || chunkHi[c] < lineLo || Miss(chunkC[c], chunkR[c] + spread, a, b))
                    continue;   // over the lines or under them, or off to one side
                q.PuffsTested += chunkStart[c + 1] - chunkStart[c];
                for (int i = chunkStart[c]; i < chunkStart[c + 1]; i++)
                    if (OnLines(ix[i], q))
                        summed++;
                if (PairOpaque(q))
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
        h.Add(freshMs);
        h.Add(ixCount);
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
