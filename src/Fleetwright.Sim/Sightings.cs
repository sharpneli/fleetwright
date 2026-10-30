using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>Every navy's picture, and the looks that feed them (docs/battle/plan.md step 3.2d; docs/world/architecture.md
/// "Knowledge": sighting writes to a navy's picture, never to a global "visible" flag).
/// <para><b>A look</b> runs every <see cref="SimSettings.SightingMs"/> of the fine tick over the ships in encounters
/// (only there can anyone see anyone: the encounter range is past the longest sighting). For each ship and each side at
/// war with it in its encounter, the <see cref="SimSettings.SightObservers"/> nearest observers of that side are found
/// through a grid in the encounter's tangent plane (ring by ring, never observers × targets), and their highest
/// <see cref="SimSettings.SightEyes"/> eyes that bear on it are tried (<see cref="SightModel"/>). Their detection rates
/// add: independent lookouts find it sooner.</para>
/// <list type="bullet">
/// <item>A contact in sight that any of them can still make out gets a fix from the one that sees it best: a bearing and
/// a range with the eye's errors (a rangefinder by its base, the eye's ±20%), into its Kalman filter.</item>
/// <item>One not in sight (or not held) is found by a draw at the summed rate: 1 - e^(-rate·dt). The draws come from a
/// substream keyed by the side, the ship and the time, so they don't depend on the order ships are looked at in.</item>
/// <item>Made out better, it's made out further: its type at <see cref="SimSettings.SightTypeMargin"/>, its class at
/// <see cref="SimSettings.SightClassMargin"/> (taken for a similar class with <see cref="SimSettings.SightMisidentify"/>'s
/// chance), its name within <see cref="SimSettings.SightNameM"/>, where a wrong class is put right.</item>
/// <item>Out of sight, a contact is dead reckoned (<see cref="Picture.EstimateAt"/>) and forgotten after
/// <see cref="SimSettings.SightForgetMs"/>.</item>
/// <item><b>Smoke on the horizon</b> (docs/battle/smoke.md Z6): a ship not seen is looked for by her smoke
/// (<see cref="SmokeLook"/>), and a fix on it starts a contact at <see cref="ContactLevel.Smoke"/> or keeps her track.</item>
/// <item><b>The coarse tier's watch</b> (docs/battle/coarse.md J5): between its minute looks, the engaged batteries'
/// targets are looked for again every <see cref="SimSettings.SightWatchMs"/> (<see cref="Watch"/>); a look's draw covers
/// only the time since she was last looked for.</item>
/// </list></summary>
public sealed class Sightings
{
    readonly SimSettings settings;
    readonly SightReach reach;
    readonly Relations relations;
    readonly Encounters encounters;
    readonly Picture?[] bySide = new Picture?[256];
    readonly List<Picture> pictures = new(4);
    readonly RandomStream stream, smokeStream, lightStream;
    readonly Probes probes;
    readonly int smokeLookProbe;
    readonly Dictionary<int, int> similar = new(64);   // a class's most similar other class in the world, or -1
    int similarFor = -1;   // the count of classes in use it was worked out for
    // the coarse tier's watch (docs/battle/coarse.md J5): this second's targets of engaged batteries (by the side looking
    // and the entity), and whether the look is the watch's
    readonly HashSet<long> watch = new(64);
    bool watching;

    // a look's ships (those in encounters), sorted by encounter: slot, side, encounter, position, their place in the
    // encounter's grid
    int[] slot = [], enc = [], order = [], cellOf = [], cellShips = [];
    // an encounter's grid, per cell: sized for an encounter 250 km across (8 km cells), so one that spreads (a ship falling
    // out and running) doesn't grow it in a fight; a sprawling one grows it once
    int[] cellStart = new int[1024];
    byte[] side = [];
    double[] gx = [], gy = [];
    readonly byte[] sidesHere = new byte[256];

    // a look's items (docs/battle/workers.md W3): each (target, side looking) in the order they're looked at, worked out
    // on the workers (what each sees, reading only), then applied on the sim thread in that order (the draws, the
    // pictures, the smoke cache's bookkeeping): the same look on any number of workers
    struct LookRow
    {
        public int T, Found, Queries, FixObs, FixEye;
        public byte By;
        public double Rate, Best, Shape, Keep, FixD, FixBrg, FixMargin;   // Shape: the best margin on her shape (not her bow wave), what identifies her
        // Keep: a held contact's best margin as if her eyes weren't dazzled (docs/battle/night.md Q2: they know where she was)
    }

    /// <summary>A smoke query a look's compute made (<see cref="SmokeSystem.Cached"/>): the observer, and the work's
    /// time if the cache didn't have it.</summary>
    struct Asked
    {
        public int Observer;
        public long Ticks;
    }

    const int NearMax = 16;   // the most observers a look asks (SightObservers is capped to it)
    LookRow[] rows = [];
    int[] near = [];          // each item's nearest observers, NearMax apiece, closest first
    Asked[] asked = [];       // each item's smoke queries, NearMax apiece (one an observer at most)
    double[] askedTau = [];   // and their depths, PairTaus apiece
    double[] nearD = [];      // each worker's squared distances while it finds an item's nearest
    // the encounter being looked in: its grid, and the moment's light
    int gridW, gridH;
    double gridMinX, gridMinY, gridCell, lookVis, lookPupil, lookSunBrg;
    NightSky lookNight;
    long lookNow;

    // a look's items worked out on the workers; under a few, inline (the same code path)
    const int LookChunk = 4, LookMinItems = 16;

    readonly struct LookJob(Sightings sightings, World world, ShipStore store) : IJob
    {
        public void Run(int from, int to, int worker)
        {
            for (int i = from; i < to; i++)
                sightings.Compute(world, store, i, worker);
        }
    }

    internal Sightings(SimSettings settings, Relations relations, Encounters encounters, SightReach reach, ulong seed, Probes probes)
    {
        this.probes = probes;
        smokeLookProbe = probes.Register("Smoke looks");
        this.settings = settings;
        this.reach = reach;
        this.relations = relations;
        this.encounters = encounters;
        stream = RandomStream.Named(seed, "Sighting");
        smokeStream = RandomStream.Named(seed, "SmokeSighting");
        lightStream = RandomStream.Named(seed, "LightSighting");
    }

    /// <summary>The pictures that exist, in the order they were made.</summary>
    public IReadOnlyList<Picture> All => pictures;

    /// <summary>A side's picture (null if it has never seen anything).</summary>
    public Picture? Of(int side) => side is >= 0 and < 256 ? bySide[side] : null;

    Picture Get(byte s) => bySide[s] ??= Add(s);

    Picture Add(byte s)
    {
        var p = new Picture(s);
        pictures.Add(p);
        return p;
    }

    /// <summary>A look over dt seconds (<see cref="SimSettings.SightingMs"/> on the fine tick, the coarse step in the
    /// coarse tier): every ship in an encounter looked for by the sides at war with it there.</summary>
    internal void Look(World world, ShipStore store, IReadOnlyList<int> classesInUse, double dt)
    {
        Gather(world, store, classesInUse, dt);
        OutOfSight(world.Time.Ms);
        Forget(world.Time.Ms);
    }

    /// <summary>The coarse tier's watch (docs/battle/coarse.md J5), every <see cref="SimSettings.SightWatchMs"/> between its
    /// looks: each engaged battery's target looked for by the battery's side, as the fine tick's look does (lost when no
    /// eye makes her out, found again at the detection's rate). Fire control reads her being in sight every second; nothing
    /// else does between the coarse looks (the AI reads it at its thinks, torpedoes have their own watch,
    /// coarse.md J3), so the rest wait for the coarse look.</summary>
    internal void Watch(World world, ShipStore store, IReadOnlyList<int> classesInUse)
    {
        watch.Clear();
        for (int i = 0; i < world.Gunnery.Engagements; i++)
            if (world.Gunnery.Engagement(i, out int shooter, out int target) && store.SlotOf(shooter) is int s and >= 0)
                watch.Add(WatchKey(store[s].Side, target));
        if (watch.Count == 0)
            return;
        watching = true;
        Gather(world, store, classesInUse, settings.SightWatchMs / 1000.0);
        watching = false;
    }

    static long WatchKey(byte by, int entity) => (long)by << 32 | (uint)entity;

    /// <summary>The ships in encounters gathered, sorted by encounter, each encounter looked in.</summary>
    void Gather(World world, ShipStore store, IReadOnlyList<int> classesInUse, double dt)
    {
        long now = world.Time.Ms;
        world.Lights.Gather(store, settings);   // the fires that light what's near them (docs/battle/night.md Q2)
        world.Searchlights.Gather(world, world.Lights);   // and the beams out (Q4)
        world.StarShells.Gather(world, world.Lights, Math.Max((long)(dt * 1000) - settings.SightingMs, 0));   // and the flares (Q5; a coarse look's minute, Q11)
        world.Challenges.Gather(world, world.Lights);     // and the challenges' lamps (Q6)
        foreach (var p in pictures)
            p.Reserve(store.Count);
        // the ships in encounters, sorted by encounter
        int n = 0;
        Ensure(store.OffRailsCount);
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int s = store.OffRailsSlot(k);
            int e = encounters.Of(s, store[s].Entity);
            if (e == 0)
                continue;
            (slot[n], enc[n], side[n], order[n]) = (s, e, store[s].Side, n);
            n++;
        }
        Array.Sort(enc, order, 0, n);   // by encounter; within one, put back in the fine list's order below
        for (int i = 0; i < n;)
        {
            int j = i;
            while (j < n && enc[j] == enc[i])
                j++;
            Array.Sort(order, i, j - i);   // within an encounter, in the fine list's order
            LookIn(world, store, i, j, now, dt, classesInUse);
            i = j;
        }
    }

    /// <summary>Contacts in sight that no look was made for this time (their ships out of the encounters, or gone) go out
    /// of sight.</summary>
    void OutOfSight(long now)
    {
        foreach (var p in pictures)
            for (int i = 0; i < p.Count; i++)
                if ((p[i].InSight || p[i].SmokeInSight || p[i].LightInSight) && p[i].LookedMs != now)
                    (p.At(i).InSight, p.At(i).SmokeInSight, p.At(i).LightInSight) = (false, false, false);
    }

    /// <summary>One encounter's look: its ships (order[a..b)) in a grid in the tangent plane at its first ship, then each
    /// looked for by each side at war with it there.</summary>
    void LookIn(World world, ShipStore store, int a, int b, long now, double dt, IReadOnlyList<int> classesInUse)
    {
        var origin = store[slot[order[a]]].Position;
        var east = Earth.East(origin);
        var north = Earth.North(origin);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        int sideCount = 0;
        for (int i = a; i < b; i++)
        {
            int k = order[i];
            var d = store[slot[k]].Position - origin;
            (gx[k], gy[k]) = (Vec3d.Dot(d, east), Vec3d.Dot(d, north));
            (minX, minY, maxX, maxY) = (Math.Min(minX, gx[k]), Math.Min(minY, gy[k]), Math.Max(maxX, gx[k]), Math.Max(maxY, gy[k]));
            if (Array.IndexOf(sidesHere, side[k], 0, sideCount) < 0)
                sidesHere[sideCount++] = side[k];
        }
        if (sideCount < 2)
            return;
        // the items in the order they're looked at: each ship, by each side at war with her here (the watch's: the
        // batteries' targets)
        int items = 0;
        for (int i = a; i < b; i++)
            for (int si = 0; si < sideCount; si++)
                if (Looked(store, order[i], sidesHere[si]))
                    items++;
        if (items == 0)
            return;
        double cell = settings.SightGridM;
        while ((maxX - minX) / cell * ((maxY - minY) / cell) > 250_000)
            cell *= 2;   // a sprawling encounter: coarser, not huge
        int w = (int)((maxX - minX) / cell) + 1, h = (int)((maxY - minY) / cell) + 1;
        // the grid as CSR: per cell its ships, in order
        EnsureCells(w * h);
        Array.Clear(cellStart, 0, w * h + 1);
        for (int i = a; i < b; i++)
        {
            int k = order[i];
            cellOf[k] = (int)((gx[k] - minX) / cell) + (int)((gy[k] - minY) / cell) * w;
            cellStart[cellOf[k] + 1]++;
        }
        for (int c = 0; c < w * h; c++)
            cellStart[c + 1] += cellStart[c];
        for (int i = a; i < b; i++)
        {
            int k = order[i];
            cellShips[cellStart[cellOf[k]]++] = k;
        }
        for (int c = w * h; c > 0; c--)
            cellStart[c] = cellStart[c - 1];
        cellStart[0] = 0;

        var weather = world.Weather.At(origin, world.Time);
        double sunEl = Light.SunElevationDeg(origin, world.Time), sunBrg = Light.SunBearingDeg(origin, world.Time);
        bool lowSun = sunEl >= settings.SightSilhouetteLowDeg && sunEl <= settings.SightSilhouetteHighDeg;
        var sky = NightLight.At(origin, world.Time, weather.Cloud, settings);   // the night's light (docs/battle/night.md Q1)
        (gridW, gridH, gridMinX, gridMinY, gridCell) = (w, h, minX, minY, cell);
        (lookNow, lookVis, lookPupil, lookSunBrg) = (now, weather.VisibilityM, SightModel.PupilMm(sky.SkyCdM2), lowSun ? sunBrg : double.NaN);
        lookNight = sky;
        EnsureItems(items, world.Workers.Participants);
        world.Smoke.PrepareQueries(world.Workers.Participants);
        items = 0;
        for (int i = a; i < b; i++)
        {
            int t = order[i];
            for (int si = 0; si < sideCount; si++)
                if (Looked(store, t, sidesHere[si]))
                    (rows[items].T, rows[items++].By) = (t, sidesHere[si]);
        }
        world.Workers.For(new LookJob(this, world, store), items, LookChunk, LookMinItems);
        for (int i = 0; i < items; i++)
            if (rows[i].Found > 0)
            {
                double span = Span(store, i, now, dt);
                if (!Apply(world, store, i, classesInUse, span))
                {
                    SmokeLook(world, store, rows[i].T, rows[i].By, near.AsSpan(i * NearMax, rows[i].Found), now, span, lookVis);
                    LightLook(world, store, rows[i].T, rows[i].By, near.AsSpan(i * NearMax, rows[i].Found), now, span, lookVis);
                }
            }
    }

    /// <summary>The time item i's look covers, s: the look's, or the time since she was last looked for by the side if
    /// that's less (a coarse look after the watch's seconds, docs/battle/coarse.md J5: those seconds had their own draws).</summary>
    double Span(ShipStore store, int i, long now, double dt)
    {
        var pic = Of(rows[i].By);
        int ci = pic?.IndexOf(store[slot[rows[i].T]].Entity) ?? -1;
        return ci >= 0 && pic![ci].LookedMs < now ? Math.Min(dt, (now - pic[ci].LookedMs) / 1000.0) : dt;
    }

    /// <summary>Whether ship k (of the look's) is looked for by a side: one at war with her; on the watch, a target of
    /// the side's batteries.</summary>
    bool Looked(ShipStore store, int k, byte by) =>
        relations.Hostile(by, side[k]) && (!watching || watch.Contains(WatchKey(by, store[slot[k]].Entity)));

    void EnsureItems(int items, int participants)
    {
        if (rows.Length < items)
        {
            int n = Math.Max(items, 2 * rows.Length);
            rows = new LookRow[n];
            near = new int[n * NearMax];
            asked = new Asked[n * NearMax];
            askedTau = new double[n * NearMax * SmokeSystem.PairTaus];
        }
        if (nearD.Length < participants * NearMax)
            nearD = new double[participants * NearMax];
    }

    /// <summary>An item's look worked out, on a worker: its side's nearest observers, then what they make of her
    /// (<see cref="Sight"/>). Reads the world, writes only the item's rows.</summary>
    void Compute(World world, ShipStore store, int i, int worker)
    {
        ref var row = ref rows[i];
        row.Queries = 0;
        row.Found = Nearest(row.T, row.By, near.AsSpan(i * NearMax, NearMax), nearD.AsSpan(worker * NearMax, NearMax));
        if (row.Found > 0)
            Sight(world, store, i, worker);
    }

    /// <summary>The nearest observers of a side to ship t, ring by ring through the encounter's grid, into nearIdx (closest
    /// first; nearD the worker's scratch); how many (at most <see cref="SimSettings.SightObservers"/>).</summary>
    int Nearest(int t, byte by, Span<int> nearIdx, Span<double> nearD)
    {
        var (w, h, minX, minY, cell) = (gridW, gridH, gridMinX, gridMinY, gridCell);
        int want = Math.Min(settings.SightObservers, nearIdx.Length), found = 0;
        int cx = (int)((gx[t] - minX) / cell), cy = (int)((gy[t] - minY) / cell);
        int rings = (int)Math.Ceiling(reach.SightM / cell) + 1;
        for (int r = 0; r <= rings; r++)
        {
            // once the nearest wanted are found closer than this ring can hold, stop
            if (found == want && nearD[found - 1] <= Math.Pow(Math.Max(r - 1, 0) * cell, 2))
                break;   // (nearD holds squared distances until the end)
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= h)
                    continue;
                int step = dy == -r || dy == r ? 1 : 2 * r;   // the ring's edge only
                for (int dx = -r; dx <= r; dx += Math.Max(step, 1))
                {
                    int x = cx + dx;
                    if (x < 0 || x >= w)
                        continue;
                    int c = x + y * w;
                    for (int q = cellStart[c]; q < cellStart[c + 1]; q++)
                    {
                        int o = cellShips[q];
                        if (side[o] != by)
                            continue;
                        double ddx = gx[o] - gx[t], ddy = gy[o] - gy[t], d2 = ddx * ddx + ddy * ddy;
                        if (d2 > reach.SightM * reach.SightM)
                            continue;
                        // insert into the sorted few (ties by index: the same whatever order cells are met in)
                        int at = found;
                        while (at > 0 && (nearD[at - 1] > d2 || nearD[at - 1] == d2 && nearIdx[at - 1] > o))
                            at--;
                        if (at >= want)
                            continue;
                        for (int m = Math.Min(found, want - 1); m > at; m--)
                            (nearD[m], nearIdx[m]) = (nearD[m - 1], nearIdx[m - 1]);
                        (nearD[at], nearIdx[at]) = (d2, o);
                        found = Math.Min(found + 1, want);
                    }
                }
            }
        }
        for (int m = 0; m < found; m++)
            nearD[m] = Math.Sqrt(nearD[m]);
        return found;
    }

    /// <summary>Side `by`'s look for ship t with its nearest observers (item i), on a worker: the summed rate, the best
    /// margin and who had it, the fix's eye; the smoke on the lines from the cache or worked out, each pair asked kept
    /// for the apply. Reads the world (her contact in the side's picture too: only her own apply changes it).</summary>
    void Sight(World world, ShipStore store, int i, int worker)
    {
        ref var row = ref rows[i];
        int t = row.T;
        var nearIdx = near.AsSpan(i * NearMax, row.Found);
        var ts = store[slot[t]];
        var tc = ts.Handling.Class;
        double tTop = tc.Silhouette.TopM, vis = lookVis, speed = Math.Abs(ts.Speed);
        // her fires as a light at night (fire.md B2); the lights near her on her (docs/battle/night.md Q2)
        double glowMW = store.Combat.Condition(ts.Slot).FireShowMW, glowM = settings.FireGlowHeightShare * tTop;
        var lights = world.Lights;
        var (litLux, litFrom) = lights.On(ts.Slot, ts.Position, vis);
        bool beams = lights.Beams > 0, veiled = beams || lights.Flares > 0;
        var pic = Of(row.By);
        int ci = pic?.IndexOf(ts.Entity) ?? -1;
        bool inSight = ci >= 0 && pic![ci].InSight;
        // the rates add; the fix comes from the eye that ranges it best (a rangefinder before an eye's guess), and once
        // a held contact is seen by a rangefinder no more need look
        double rate = 0, best = 0, shape = 0, keep = 0, fixD = 0, fixBrg = 0, fixSigma = double.MaxValue, fixMargin = 0;
        int fixObs = -1, fixEye = -1;
        bool done = false;
        for (int m = 0; m < nearIdx.Length && !done; m++)
        {
            var os = store[slot[nearIdx[m]]];
            var oc = os.Handling.Class;
            var eyes = oc.Eyes;
            if (eyes.Length == 0)
                continue;
            double d = Earth.Distance(os.Position, ts.Position);
            if (d > SightModel.HorizonM(eyes[0].HeightM, settings) + SightModel.HorizonM(tTop, settings))
                continue;   // beyond the highest eye's and the target's highest point's horizons together
            double brg = Earth.Bearing(os.Position, ts.Position);
            double rel = brg - os.HeadingDeg;
            double back = Earth.Bearing(ts.Position, os.Position), aspect = back - ts.HeadingDeg;
            var line = new SightLine(d, aspect, vis, lookNight.Toward(brg), Silhouette(brg), SpeedMs: speed,
                LitLux: litLux > 0 ? litLux * Lights.Facing(litFrom, back) : 0, AdaptedCdM2: Dazzled(store, os.Slot));
            if (lights.Flares > 0)
                line = Backlit(line, lights.BacklightCdM2(os.Position, ts.Position, vis, settings));   // a flare beyond her (Q5)
            var works = store.Combat.Condition(slot[nearIdx[m]]).EyeWorks;   // damage: a wrecked post sees nothing
            // her searchlights toward this ship (docs/battle/night.md Q4): the lamp in the beam or forward of it, a point
            // to aim at that finds and holds her, but no more says what she is than her bow wave does
            var (lampCd, _, lampM) = beams ? lights.LampsOf(ts.Slot, os.Position, vis, settings) : default;
            // the smoke on the lines (docs/battle/smoke.md Z3), asked for only where it could matter: smoke only lowers
            // a margin, so where none of the eyes that will look makes her out in clear air (and her fires and lamps don't
            // show), her margins through it are under 1 too and nothing it says changes the look
            bool smoked = world.Smoke.On && (glowMW > 0 || lampCd > 0 || ClearlyMadeOut(eyes, works, rel, tc, line));
            Span<double> smoke = default;
            if (smoked)
            {
                int k = i * NearMax + row.Queries++;
                smoke = askedTau.AsSpan(k * SmokeSystem.PairTaus, SmokeSystem.PairTaus);
                ref var q = ref asked[k];
                (q.Observer, q.Ticks) = (os.Slot, 0);
                if (!world.Smoke.Cached(world, os.Slot, ts.Slot, smoke))
                {
                    long p0 = Probes.Now;
                    world.Smoke.WorkOut(world, os.Slot, ts.Slot, smoke, worker);
                    q.Ticks = Probes.Now - p0;
                }
            }
            int tried = 0;
            for (int e = 0; e < eyes.Length && tried < settings.SightEyes && !done; e++)
            {
                if (!eyes[e].Sees(rel) || !works[e])
                    continue;
                tried++;
                line = line with { Glass = SightModel.GlassLight(eyes[e], lookPupil, settings) };
                if (smoked)
                    line = line with { Smoke = SmokeModel.EyeTau(smoke, eyes, eyes[e].X, eyes[e].Y, eyes[e].HeightM) };
                if (veiled)   // the beams' light scattered back down this line, others' and the flares' glare in this eye (Q4, Q5)
                    line = line with { VeilCdM2 = lights.VeilCdM2(os.Slot, os.Frame.ToWorld(eyes[e].X, eyes[e].Y, eyes[e].HeightM), ts.Position, vis, settings, line.SkyCdM2) };
                double mag = SightModel.Magnification(eyes[e], settings);
                double margin = SightModel.Margin(eyes[e].HeightM, mag, tc, line, settings);
                if (glowMW > 0)
                    margin = Math.Max(margin, SightModel.GlowMargin(eyes[e].HeightM, glowMW, glowM, d, vis, line.SkyCdM2, settings,
                        SightModel.SmokeAt(line.Smoke, glowM, tTop, settings)));
                shape = Math.Max(shape, margin);
                // her bow wave finds her and keeps her (docs/battle/night.md Q1), but says nothing of what she is
                margin = Math.Max(margin, SightModel.BowWaveMargin(eyes[e].HeightM, mag, tc, line, settings));
                if (lampCd > 0)
                    margin = Math.Max(margin, SightModel.LightMargin(eyes[e].HeightM, lampCd, lampM, d, vis, line.SkyCdM2 + line.VeilCdM2, settings,
                        SightModel.SmokeAt(line.Smoke, lampM, tTop, settings), line.AdaptedCdM2));
                rate += SightModel.Rate(margin, SightModel.SearchFactor(eyes[e], settings), settings);
                best = Math.Max(best, margin);
                // dazzled by her own guns, her eyes take no fresh sighting but keep the one they hold: where they last saw her
                keep = Math.Max(keep, inSight && line.AdaptedCdM2 > 0 && margin < 1
                    ? Math.Max(SightModel.Margin(eyes[e].HeightM, mag, tc, line with { AdaptedCdM2 = 0 }, settings),
                        SightModel.BowWaveMargin(eyes[e].HeightM, mag, tc, line with { AdaptedCdM2 = 0 }, settings))
                    : margin);
                if (margin < 1)
                    continue;
                double sigma = SightModel.RangeSigmaM(eyes[e], d, margin, settings);
                if (sigma < fixSigma)
                    (fixSigma, fixD, fixBrg, fixObs, fixEye, fixMargin) = (sigma, d, brg, nearIdx[m], e, margin);
                done = inSight && eyes[e].RangefinderM > 0;
            }
        }
        (row.Rate, row.Best, row.Shape, row.Keep, row.FixD, row.FixBrg, row.FixObs, row.FixEye, row.FixMargin) =
            (rate, best, shape, keep, fixD, fixBrg, fixObs, fixEye, fixMargin);
    }

    /// <summary>Item i's look applied, on the sim thread in the look's order: the smoke cache's bookkeeping for the pairs
    /// it asked, then a fix, a detection (a draw over dt s), or the contact going out of sight. Whether she's in sight after it.</summary>
    bool Apply(World world, ShipStore store, int i, IReadOnlyList<int> classesInUse, double dt)
    {
        ref readonly var row = ref rows[i];
        var ts = store[slot[row.T]];
        long now = lookNow;
        var pic = Get(row.By);
        int ci = pic.IndexOf(ts.Entity);
        bool inSight = ci >= 0 && pic[ci].InSight;
        for (int k = i * NearMax; k < i * NearMax + row.Queries; k++)
            world.Smoke.Answered(world, asked[k].Observer, ts.Slot, askedTau.AsSpan(k * SmokeSystem.PairTaus, SmokeSystem.PairTaus),
                asked[k].Ticks);
        if (ci >= 0)
            pic.At(ci).LookedMs = now;
        var r = stream.Derive(((ulong)row.By << 56) ^ ((ulong)(uint)ts.Entity << 24) ^ (ulong)(now / settings.SightingMs));
        if (inSight && row.Best >= 1 || !inSight && row.Rate > 0 && r.NextDouble() < SightModel.Chance(row.Rate, dt))
        {
            var os = store[slot[row.FixObs]];
            Fix(pic, ci, ts, store.Combat.Condition(ts.Slot), os, os.Handling.Class.Eyes[row.FixEye], row.Shape, row.FixMargin, row.FixD, row.FixBrg,
                now, ref r, classesInUse);
            ref var con = ref pic.At(ci >= 0 ? ci : pic.IndexOf(ts.Entity));
            Quiet(ref con, inSight, store.Combat.LastFired(ts.Slot), now, dt);
            if (!con.Foe && GivesAway(world, store, ts, row.By, now, dt))
                con.Foe = true;
            return true;
        }
        if (inSight && row.Keep >= 1)
            return true;   // her eyes dazzled (Q2): held where she was, no fresh fix until they're back
        if (inSight && row.Best < 1)
            pic.At(ci).InSight = false;
        return false;
    }

    /// <summary>A line with the sea lit behind her by a flare beyond (docs/battle/night.md Q5, cd/m²): the background
    /// that much brighter, and she a silhouette against it by its share of it.</summary>
    internal static SightLine Backlit(in SightLine line, double backCdM2) =>
        backCdM2 <= 0 ? line : line with
        {
            SkyCdM2 = line.SkyCdM2 + backCdM2, Silhouette = Math.Max(line.Silhouette, backCdM2 / (line.SkyCdM2 + backCdM2)),
        };

    /// <summary>How far a ship on this bearing from the eye stands dark against a bright horizon beyond her (0-1): fully
    /// within the silhouette arc of a low sun. The moon's path is the sky's luminance on its bearing
    /// (<see cref="NightSky.Toward"/>, docs/battle/night.md Q1b), as the twilight's glow is.</summary>
    double Silhouette(double brg) =>
        !double.IsNaN(lookSunBrg) && Math.Abs(Angle(brg - lookSunBrg)) <= settings.SightSilhouetteArcDeg ? 1 : 0;

    /// <summary>Whether any of the eyes that look at her (the first <see cref="SimSettings.SightEyes"/> that bear and
    /// work) makes her out in clear air.</summary>
    bool ClearlyMadeOut(ReadOnlySpan<EyeRow> eyes, ReadOnlySpan<bool> works, double rel, ShipClass tc, in SightLine line)
    {
        int tried = 0;
        for (int e = 0; e < eyes.Length && tried < settings.SightEyes; e++)
        {
            if (!eyes[e].Sees(rel) || !works[e])
                continue;
            tried++;
            var eye = line with { Glass = SightModel.GlassLight(eyes[e], lookPupil, settings) };
            double mag = SightModel.Magnification(eyes[e], settings);
            if (SightModel.Margin(eyes[e].HeightM, mag, tc, eye, settings) > 1 || SightModel.BowWaveMargin(eyes[e].HeightM, mag, tc, eye, settings) > 1)
                return true;
        }
        return false;
    }

    /// <summary>Side `by`'s look for ship t's smoke (docs/battle/smoke.md Z6), she herself not in sight: each of its
    /// nearest observers' highest eye that bears on her plume (<see cref="SmokeSystem.PlumeOf"/>; the masthead's lookouts
    /// see furthest), its margin by <see cref="SightModel.SmokeMargin"/>; the rates add as a ship's do, and smoke in sight
    /// stays so while any of them still makes it out. A fix (<see cref="SmokeFix"/>) or the smoke going out of sight. Its
    /// own draws (a stream of its own), so the ships' are as they were.</summary>
    void SmokeLook(World world, ShipStore store, int t, byte by, ReadOnlySpan<int> nearIdx, long now, double dt, double vis)
    {
        var ts = store[slot[t]];
        var pic = Get(by);
        int ci = pic.IndexOf(ts.Entity);
        bool held = ci >= 0 && pic[ci].SmokeInSight;
        if (!world.Smoke.On || !world.Smoke.PlumeOf(ts.Entity, out var plume))
        {
            if (held)
                pic.At(ci).SmokeInSight = false;
            return;
        }
        long p0 = Probes.Now;
        // no plume, however big, beats the haze past where its darkest smoke fades under the eye's threshold for the
        // largest object (and none shows in the dark): those observers are skipped, their margins under 1 anyway
        double farM = SightModel.SmokeFarthestM(plume, vis, lookNight.Brightest, settings);
        double rate = 0, best = 0, fixSin = 0;
        int fixObs = -1, fixEye = -1;
        double top = SightModel.HorizonM(plume.TopM, settings);
        var up = plume.Centre.Normalized();
        for (int m = 0; m < nearIdx.Length; m++)
        {
            var os = store[slot[nearIdx[m]]];
            var eyes = os.Handling.Class.Eyes;
            if (eyes.Length == 0)
                continue;
            double d = Earth.Distance(os.Position, plume.Centre);
            if (d > farM || d > SightModel.HorizonM(eyes[0].HeightM, settings) + top)
                continue;
            double brg = Earth.Bearing(os.Position, plume.Centre), rel = brg - os.HeadingDeg;
            var works = store.Combat.Condition(slot[nearIdx[m]]).EyeWorks;
            int e = 0;
            while (e < eyes.Length && (!eyes[e].Sees(rel) || !works[e]))
                e++;
            if (e == eyes.Length)
                continue;
            // the view against her trail: the line of sight on the sea at the plume
            var los = plume.Centre - os.Position;
            los -= up * Vec3d.Dot(los, up);
            double len = los.Length, cos = len > 0 ? Vec3d.Dot(los, plume.Axis) / len : 0, sin = Math.Sqrt(Math.Max(1 - cos * cos, 0));
            double margin = SightModel.SmokeMargin(eyes[e].HeightM, SightModel.Magnification(eyes[e], settings), plume, sin,
                new SightLine(d, 90, vis, lookNight.Toward(brg), 0, Glass: SightModel.GlassLight(eyes[e], lookPupil, settings),
                    AdaptedCdM2: Dazzled(store, os.Slot)), settings);
            rate += SightModel.Rate(margin, SightModel.SearchFactor(eyes[e], settings), settings);
            if (margin > best)
                (best, fixObs, fixEye, fixSin) = (margin, nearIdx[m], e, sin);
        }
        if (ci >= 0)
            pic.At(ci).LookedMs = now;
        var r = smokeStream.Derive(((ulong)by << 56) ^ ((ulong)(uint)ts.Entity << 24) ^ (ulong)(now / settings.SightingMs));
        if (held && best >= 1 || !held && rate > 0 && r.NextDouble() < SightModel.Chance(rate, dt))
        {
            var os = store[slot[fixObs]];
            SmokeFix(pic, ci, ts, os, os.Handling.Class.Eyes[fixEye], plume.AcrossView(fixSin), now, ref r);
        }
        else if (held)
            pic.At(ci).SmokeInSight = false;
        probes.Add(smokeLookProbe, p0);
    }

    /// <summary>What her own guns' last flash leaves an observer's eyes adapted to now, cd/m² (docs/battle/night.md Q2,
    /// <see cref="SightModel.DazzleCdM2"/>).</summary>
    double Dazzled(ShipStore store, int observerSlot)
    {
        var (cd, ms) = store.Combat.LastFlash(observerSlot);
        return cd > 0 ? SightModel.DazzleCdM2(cd, (lookNow - ms) / 1000.0, settings) : 0;
    }

    /// <summary>Her funnels' glow while she forces coal fires (docs/battle/night.md Q2): <see cref="SimSettings.SightFunnelGlowCd"/>
    /// a funnel at her rated power, as the smoke's forcing grows (<see cref="SmokeSources.Forcing"/>: the square of the load
    /// over <see cref="SimSettings.SmokeForcingFrom"/>), at her highest funnel's top; none on oil. cd, and the height, m.</summary>
    (double Cd, double HeightM) FunnelGlow(ShipState ts)
    {
        var c = ts.Handling.Class;
        double cd = SmokeSources.FunnelGlowCd(c, ts.Speed, settings);
        if (!(cd > 0))
            return (0, 0);
        double top = 0;
        foreach (ref readonly var f in c.Funnels)
            top = Math.Max(top, c.Components[f.Component].Bounds.Top - c.Vertical.Waterline);
        return (cd, top);
    }

    /// <summary>Side `by`'s look for ship t's lights (docs/battle/night.md Q2), she herself not in sight: her latest salvo's
    /// flash if she fired within the look's dt s (its light at the mounts' height, halfway up her), and her funnels' glow
    /// while she forces her fires (<see cref="FunnelGlow"/>), and her searchlights' beams (docs/battle/night.md Q4: the
    /// lamp, and a beam seen from beside, <see cref="Lights.LampsOf"/>). Each of her side's nearest observers' highest eye
    /// that bears on her takes them by <see cref="SightModel.LightMargin"/> through the haze, against the sky on that
    /// bearing and its own guns' dazzle (no smoke asked of the field: a flash shows through more than a hull does). A
    /// flash is seen at once by its chance (<see cref="SightModel.FlashChance"/>), a glow or a beam at the rate a ship is;
    /// her lights stay in sight while a glow or a beam is still made out. A fix (<see cref="LightFix"/>) or the light going
    /// out of sight. Its own draws (a stream of its own), so the ships' and the smoke's are as they were.</summary>
    void LightLook(World world, ShipStore store, int t, byte by, ReadOnlySpan<int> nearIdx, long now, double dt, double vis)
    {
        var ts = store[slot[t]];
        var pic = Get(by);
        int ci = pic.IndexOf(ts.Entity);
        bool held = ci >= 0 && pic[ci].LightInSight;
        var (flashCd, flashMs) = store.Combat.LastFlash(ts.Slot);
        // (the window's start included: a salvo fired at a look's moment after that look ran is the next's)
        double flash = flashMs > 0 && flashMs >= now - (long)(dt * 1000) && flashMs <= now ? flashCd : 0;
        var (glow, glowM) = FunnelGlow(ts);
        var lights = world.Lights;
        bool beams = lights.HasBeam(ts.Slot);
        if (flash <= 0 && glow <= 0 && !beams)
        {
            if (held)
                pic.At(ci).LightInSight = false;
            return;
        }
        double flashM = settings.FireGlowHeightShare * ts.Handling.Class.Silhouette.TopM;
        double highest = Math.Max(Math.Max(flashM, glowM), beams ? ts.Handling.Class.Silhouette.TopM : 0);
        double miss = 1, rate = 0, best = 0, glowBest = 0;
        int fixObs = -1, fixEye = -1;
        for (int m = 0; m < nearIdx.Length; m++)
        {
            var os = store[slot[nearIdx[m]]];
            var eyes = os.Handling.Class.Eyes;
            if (eyes.Length == 0)
                continue;
            double d = Earth.Distance(os.Position, ts.Position);
            if (d > SightModel.HorizonM(eyes[0].HeightM, settings) + SightModel.HorizonM(highest, settings))
                continue;
            double brg = Earth.Bearing(os.Position, ts.Position), rel = brg - os.HeadingDeg;
            var works = store.Combat.Condition(slot[nearIdx[m]]).EyeWorks;
            int e = 0;
            while (e < eyes.Length && (!eyes[e].Sees(rel) || !works[e]))
                e++;
            if (e == eyes.Length)
                continue;
            double sky = lookNight.Toward(brg), adapted = Dazzled(store, os.Slot), h = eyes[e].HeightM;
            double mf = flash > 0 ? SightModel.LightMargin(h, flash, flashM, d, vis, sky, settings, 0, adapted) : 0;
            double mg = glow > 0 ? SightModel.LightMargin(h, glow, glowM, d, vis, sky, settings, 0, adapted) : 0;
            if (beams)
            {
                var (lamp, column, lampM) = lights.LampsOf(ts.Slot, os.Position, vis, settings);
                mg = Math.Max(mg, SightModel.LightMargin(h, lamp + column, lampM, d, vis, sky, settings, 0, adapted));
            }
            miss *= 1 - SightModel.FlashChance(mf, settings);
            rate += SightModel.Rate(mg, SightModel.SearchFactor(eyes[e], settings), settings);
            glowBest = Math.Max(glowBest, mg);
            if (Math.Max(mf, mg) > best)
                (best, fixObs, fixEye) = (Math.Max(mf, mg), nearIdx[m], e);
        }
        if (ci >= 0)
            pic.At(ci).LookedMs = now;
        var r = lightStream.Derive(((ulong)by << 56) ^ ((ulong)(uint)ts.Entity << 24) ^ (ulong)(now / settings.SightingMs));
        double seen = 1 - miss * (1 - SightModel.Chance(rate, dt));
        if (fixObs >= 0 && (held && glowBest >= 1 || r.NextDouble() < seen))
        {
            var os = store[slot[fixObs]];
            LightFix(pic, ci, ts, os, os.Handling.Class.Eyes[fixEye], now, ref r);
            ref var con = ref pic.At(pic.IndexOf(ts.Entity));
            if (!con.Foe && GivesAway(world, store, ts, by, now, dt))
                con.Foe = true;
        }
        else if (held)
            pic.At(ci).LightInSight = false;
    }

    /// <summary>Whether a ship seen (in sight or by her lights) gives herself away to side `by` as a foe over a look's dt s
    /// (docs/battle/night.md Q6): a challenge of hers (none is by's own: its ships challenge only what they don't know), her
    /// salvo fired within it at a ship of by's or a friend's, or her searchlights held on one. Her guns at others, her
    /// star shell (nobody's light) and her answer to a challenge say nothing of whose she is.</summary>
    bool GivesAway(World world, ShipStore store, ShipState ts, byte by, long now, double dt)
    {
        long from = now - (long)(dt * 1000);
        if (world.Challenges.Signalling(ts.Entity, from, now))
            return true;
        var (order, lit, exposed) = world.Searchlights.OrderOf(ts.Entity, now);
        if (exposed && order == SearchlightOrder.Illuminate && Friendly(store, lit, by))
            return true;
        long fired = store.Combat.LastFired(ts.Slot);
        if (fired <= 0 || fired < from || fired > now)
            return false;
        for (int b = 0; b < ts.Handling.Class.Batteries.Length; b++)
            if (Friendly(store, store.Combat.Target(ts.Slot, b), by))
                return true;
        return false;
    }

    /// <summary>Whether a ship (an entity) is of side `by` or a side at peace with it.</summary>
    bool Friendly(ShipStore store, int entity, byte by) =>
        entity != 0 && store.SlotOf(entity) is int s and >= 0 && !relations.Hostile(by, store[s].Side);

    /// <summary>A sighting of a ship's lights into the picture (docs/battle/night.md Q2): a bearing on her with the eye's
    /// error, and a rough range (<see cref="SimSettings.SightLightRangeShare"/> of it). A new contact is at
    /// <see cref="ContactLevel.Light"/>, its course and speed unknown; a held one keeps its level, and what was made out of
    /// her stays.</summary>
    void LightFix(Picture pic, int ci, ShipState ts, ShipState os, in EyeRow eye, long now, ref RandomStream r)
    {
        double d = Math.Max(Earth.Distance(os.Position, ts.Position), 100), brg = Earth.Bearing(os.Position, ts.Position);
        double sr = settings.SightLightRangeShare * d, sb = SightModel.BearingSigmaRad(eye);
        double mr = Math.Max(d + sr * Gauss(ref r), 100), mb = brg + sb * Gauss(ref r) * 180 / Math.PI;
        var (z, r11, r12, r22) = Track.Reading(os.Position, mr, mb, sr, sb);
        if (ci < 0)
        {
            ref var nc = ref pic.Add(ts.Entity);
            ci = pic.IndexOf(ts.Entity);
            nc.FirstMs = now;
            nc.Position = z;
            nc.Velocity = Vec3d.Zero;
            double sv = settings.SightLightSpeedSigmaMs * settings.SightLightSpeedSigmaMs;
            nc.P = new Cov4 { Ee = r11, En = r12, Nn = r22, VeVe = sv, VnVn = sv };
            nc.EstMs = now;
            nc.Level = ContactLevel.Light;
        }
        else
        {
            ref var c = ref pic.At(ci);
            var (p, v, cov) = pic.EstimateAt(ci, now, settings);
            Track.Update(ref p, ref v, ref cov, z, r11, r12, r22);
            (c.Position, c.Velocity, c.P, c.EstMs) = (p, v, cov, now);
        }
        ref var con = ref pic.At(ci);
        con.Side = ts.Side;
        con.SeenMs = now;
        con.LookedMs = now;
        con.LightInSight = true;
        con.Observer = os.Entity;
        con.Eye = eye.Kind;
        con.From = os.Position;
    }

    /// <summary>A sighting of a ship's smoke into the picture (docs/battle/smoke.md Z6): a bearing on her (the ships are at
    /// the smoke's head: the eye's error and <see cref="SimSettings.SightSmokeBearingShare"/> of the plume's spread across
    /// the line) and a rough range (<see cref="SimSettings.SightSmokeRangeShare"/> of it). The errors of one sighting
    /// after another of the same smoke go together: a held contact's readings count, over
    /// <see cref="SimSettings.SightSmokeRangeS"/>, for one (each taken looser by √(that / the time since its last
    /// fix)). A new contact is at <see cref="ContactLevel.Smoke"/>, its course and speed unknown; a held one keeps its
    /// level, and what was made out of her stays.</summary>
    void SmokeFix(Picture pic, int ci, ShipState ts, ShipState os, in EyeRow eye, double acrossM, long now, ref RandomStream r)
    {
        double d = Math.Max(Earth.Distance(os.Position, ts.Position), 100), brg = Earth.Bearing(os.Position, ts.Position);
        double k = ci >= 0 ? Math.Sqrt(Math.Max(settings.SightSmokeRangeS * 1000 / Math.Max(now - pic[ci].SeenMs, 1), 1)) : 1;
        double sr0 = settings.SightSmokeRangeShare * d;
        double sb0 = Math.Sqrt(Math.Pow(SightModel.BearingSigmaRad(eye), 2) + Math.Pow(settings.SightSmokeBearingShare * acrossM / d, 2));
        double mr = Math.Max(d + sr0 * Gauss(ref r), 100), mb = brg + sb0 * Gauss(ref r) * 180 / Math.PI;
        var (z, r11, r12, r22) = Track.Reading(os.Position, mr, mb, k * sr0, k * sb0);
        if (ci < 0)
        {
            ref var nc = ref pic.Add(ts.Entity);
            ci = pic.IndexOf(ts.Entity);
            nc.FirstMs = now;
            nc.Position = z;
            nc.Velocity = Vec3d.Zero;
            double sv = settings.SightSmokeSpeedSigmaMs * settings.SightSmokeSpeedSigmaMs;
            nc.P = new Cov4 { Ee = r11, En = r12, Nn = r22, VeVe = sv, VnVn = sv };
            nc.EstMs = now;
            nc.Level = ContactLevel.Smoke;
        }
        else
        {
            ref var c = ref pic.At(ci);
            var (p, v, cov) = pic.EstimateAt(ci, now, settings);
            Track.Update(ref p, ref v, ref cov, z, r11, r12, r22);
            (c.Position, c.Velocity, c.P, c.EstMs) = (p, v, cov, now);
        }
        ref var con = ref pic.At(ci);
        con.Side = ts.Side;
        con.SeenMs = now;
        con.LookedMs = now;
        con.SmokeInSight = true;
        if (con.Level < ContactLevel.Smoke)
            con.Level = ContactLevel.Smoke;   // a light's contact: now her smoke
        con.Observer = os.Entity;
        con.Eye = eye.Kind;
        con.From = os.Position;
    }

    /// <summary>A sighting into the picture: a fix of a held contact, or a new one; and how far it's made out (the best
    /// margin; the fix's eye's is its own).</summary>
    void Fix(Picture pic, int ci, ShipState ts, ShipCondition tk, ShipState os, in EyeRow eye, double margin, double eyeMargin, double d, double brg, long now,
        ref RandomStream r, IReadOnlyList<int> classesInUse)
    {
        // the reading: the bearing and range with the eye's errors in this air, as a point on the sphere
        double sr = SightModel.RangeSigmaM(eye, d, eyeMargin, settings);
        double sb = SightModel.BearingSigmaRad(eye) * (eye.Ranges ? Math.Sqrt(SightModel.RangingFactor(eyeMargin, eye.Optics, settings)) : 1);
        double mr = Math.Max(d + sr * Gauss(ref r), 100), mb = brg + sb * Gauss(ref r) * 180 / Math.PI;
        var (z, r11, r12, r22) = Track.Reading(os.Position, mr, mb, sr, sb);
        if (ci < 0)
        {
            ref var nc = ref pic.Add(ts.Entity);
            ci = pic.IndexOf(ts.Entity);
            nc.FirstMs = now;
            nc.Position = z;
            // the first report's course and speed, as judged by eye from the target's aspect and bow wave
            double cse = ts.HeadingDeg + settings.SightCourseSigmaDeg * Gauss(ref r);
            double spd = Math.Max(Math.Abs(ts.Speed) + settings.SightSpeedSigmaMs * Gauss(ref r), 0);
            nc.Velocity = Earth.Forward(z, cse) * spd;
            double sv = settings.SightSpeedSigmaMs * settings.SightSpeedSigmaMs + Math.Pow(spd * settings.SightCourseSigmaDeg * Math.PI / 180, 2);
            nc.P = new Cov4 { Ee = r11, En = r12, Nn = r22, VeVe = sv, VnVn = sv };
            nc.EstMs = now;
        }
        else
        {
            ref var c = ref pic.At(ci);
            var (p, v, cov) = pic.EstimateAt(ci, now, settings);
            Track.Update(ref p, ref v, ref cov, z, r11, r12, r22);
            (c.Position, c.Velocity, c.P, c.EstMs) = (p, v, cov, now);
        }
        ref var con = ref pic.At(ci);
        con.Side = ts.Side;
        con.SeenMs = now;
        con.LookedMs = now;
        con.InSight = true;
        con.SmokeInSight = false;
        con.LightInSight = false;
        con.Observer = os.Entity;
        con.Eye = eye.Kind;
        con.From = os.Position;
        if (con.Level < ContactLevel.Ship)
            con.Level = ContactLevel.Ship;   // her smoke's contact: now the ship herself
        con.Seen = SeenOf(ts, tk) | con.Seen & SeenFlags.Gone;
        con.Shape = margin;
        Identify(ref con, ts, margin, d, ref r, classesInUse);
    }

    /// <summary>Her guns as watched (docs/battle/crew.md N5b): her quiet starts again when she comes into sight or a salvo of
    /// hers was fired since the last look (dt s), now that she's in sight; silent once she shows damage (burning, low, a list,
    /// a trim) and has been quiet <see cref="SimSettings.SeenSilentS"/>. The quiet between a battery's salvos isn't
    /// silence, nor a ship holding her fire unhurt: a damaged one gone quiet is read as silenced.</summary>
    void Quiet(ref Contact con, bool wasInSight, long fired, long now, double dt)
    {
        if (!wasInSight || con.QuietMs == 0)
            con.QuietMs = now;
        if (fired > 0 && fired >= now - (long)(dt * 1000))
            con.QuietMs = Math.Max(con.QuietMs, fired);
        if ((con.Seen & (SeenFlags.Burning | SeenFlags.Low | SeenFlags.Listing | SeenFlags.ByBow | SeenFlags.ByStern)) != 0 &&
            now - con.QuietMs >= settings.SeenSilentS * 1000)
            con.Seen |= SeenFlags.Silent;
    }

    /// <summary>What an observer makes out of a ship's condition (docs/battle/fighting.md 4.3b): settled low, a list, a
    /// trim, fire and smoke, stopped, going under (silent: <see cref="Quiet"/>). Never its water or what's left of its
    /// buoyancy.</summary>
    SeenFlags SeenOf(ShipState ts, ShipCondition k)
    {
        var seen = SeenFlags.None;
        if (Math.Abs(ts.Speed) < settings.SeenStoppedKn * Handling.MsPerKnot)
            seen |= SeenFlags.Stopped;
        if (!k.Damaged)
            return seen;
        var a = k.Attitude;
        if (a.SinkageM >= settings.SeenLowShare * Math.Max(ts.Handling.Class.Vertical.Freeboard, 0.1))
            seen |= SeenFlags.Low;
        if (Math.Abs(a.HeelDeg) >= settings.SeenListDeg)
            seen |= SeenFlags.Listing;
        if (a.TrimDeg >= settings.SeenTrimDeg)
            seen |= SeenFlags.ByBow;
        else if (a.TrimDeg <= -settings.SeenTrimDeg)
            seen |= SeenFlags.ByStern;
        if (k.FireShowMW > 0)
            seen |= SeenFlags.Burning;   // a developed or open-deck fire shows; a starved one inside doesn't (fire.md B2)
        if (k.Status == ShipStatus.GoingDown)
            seen |= SeenFlags.GoingDown;
        return seen;
    }

    /// <summary>How far a sighting makes a contact out: its type, its class (taken for a similar one by chance), its name
    /// (and its class put right). Never less than it was.</summary>
    void Identify(ref Contact c, ShipState ts, double margin, double d, ref RandomStream r, IReadOnlyList<int> classesInUse)
    {
        int truth = ts.ClassIndex;
        if (d <= settings.SightNameM && margin >= settings.SightClassMargin)
        {
            c.Level = ContactLevel.Name;
            c.TakenFor = truth;
        }
        else if (margin >= settings.SightClassMargin && c.Level < ContactLevel.Class)
        {
            c.Level = ContactLevel.Class;
            int alike = Similar(truth, ts.Handling.Class, classesInUse);
            c.TakenFor = alike >= 0 && r.NextDouble() < settings.SightMisidentify ? alike : truth;
        }
        else if (margin >= settings.SightTypeMargin && c.Level < ContactLevel.Type)
            c.Level = ContactLevel.Type;
        if (c.Level >= ContactLevel.Type)
        {
            c.Type = TypeOf(c.TakenFor >= 0 && Registry?.Get(c.TakenFor) is { } taken ? taken : ts.Handling.Class);
            if (c.Level >= ContactLevel.Class || margin >= SideMargin(lookNight.SkyCdM2))
                c.Foe = true;   // her colours by day, her build in the dark: whose she is (docs/battle/night.md Q6, Q12e)
        }
    }

    /// <summary>The margin whose a ship is made out at under a sky (cd/m²): the type's by day, the class's in the dark,
    /// between on the log of its luminance across <see cref="SimSettings.SightColoursCdM2"/>.</summary>
    double SideMargin(double skyCdM2)
    {
        double day = settings.SightColoursCdM2[0], dark = settings.SightColoursCdM2[1];
        double t = Math.Clamp(Math.Log(day / Math.Max(skyCdM2, 1e-12)) / Math.Log(day / dark), 0, 1);
        return settings.SightTypeMargin + t * (settings.SightClassMargin - settings.SightTypeMargin);
    }

    /// <summary>The class in the world most like a class (by funnels, masts, main mounts and length), if any is near
    /// enough to be taken for it; -1 if none. Worked out once per class, as classes come into use.</summary>
    int Similar(int truth, ShipClass tc, IReadOnlyList<int> classesInUse)
    {
        if (similarFor != classesInUse.Count)
        {
            similar.Clear();   // classes came into use: worked out again
            similarFor = classesInUse.Count;
        }
        if (!similar.TryGetValue(truth, out int best))
        {
            best = -1;
            double bestScore = settings.SightSimilarScore;
            for (int k = 0; k < classesInUse.Count; k++)   // by index: a foreach over the interface boxes its enumerator
            {
                int id = classesInUse[k];
                if (id == truth || Registry?.Get(id) is not { } oc)
                    continue;
                double score = Score(tc.Silhouette, oc.Silhouette);
                if (score < bestScore || score == bestScore && best >= 0 && id < best)
                    (best, bestScore) = (id, score);
            }
            similar[truth] = best;
        }
        return best;
    }

    /// <summary>How unlike two silhouettes are: funnels and masts count one each, main mounts a half, and the length's
    /// difference five per its ratio.</summary>
    static double Score(SilhouetteRow a, SilhouetteRow b) =>
        Math.Abs(a.Funnels - b.Funnels) + Math.Abs(a.Masts - b.Masts) + 0.5 * Math.Abs(a.MainMounts - b.MainMounts) +
        5 * Math.Abs(a.LengthM / Math.Max(b.LengthM, 1) - 1);

    /// <summary>Where the classes are, for what a contact's taken for (set by the world).</summary>
    internal ShipClassRegistry? Registry { get; set; }

    /// <summary>What a class is reported as by type: a merchant (no main mounts), else by its full displacement.</summary>
    public static ShipType TypeOf(ShipClass c) =>
        c.Silhouette.MainMounts == 0 ? ShipType.Merchant
        : c.Motion.FullDisplacementT >= 14_000 ? ShipType.Capital
        : c.Motion.FullDisplacementT >= 2_500 ? ShipType.Cruiser
        : c.Motion.FullDisplacementT >= 400 ? ShipType.Destroyer
        : ShipType.Small;

    /// <summary>Contacts out of sight long enough forgotten: at each look, and at the hourly contact scan (so they age
    /// out with no encounter open).</summary>
    internal void Forget(long now)
    {
        foreach (var p in pictures)
            for (int i = p.Count - 1; i >= 0; i--)
                if (!p[i].InSight && now - p[i].SeenMs > settings.SightForgetMs)
                    p.RemoveAt(i);
    }

    static double Gauss(ref RandomStream r)
    {
        double u1 = Math.Max(r.NextDouble(), 1e-300), u2 = r.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    static double Angle(double d) => d - 360 * Math.Round(d / 360);

    void Ensure(int n)
    {
        if (slot.Length >= n)
            return;
        int c = Math.Max(n, Math.Max(16, slot.Length * 2));
        Array.Resize(ref slot, c);
        Array.Resize(ref enc, c);
        Array.Resize(ref order, c);
        Array.Resize(ref side, c);
        Array.Resize(ref gx, c);
        Array.Resize(ref gy, c);
        Array.Resize(ref cellOf, c);
        Array.Resize(ref cellShips, c);
    }

    void EnsureCells(int cells)
    {
        if (cellStart.Length < cells + 1)
            Array.Resize(ref cellStart, Math.Max(cells + 1, cellStart.Length * 2));
    }

    /// <summary>The player's navy's picture into a snapshot's part, carried on to now; allocates only to grow it.</summary>
    internal void Fill(ContactsView v, int playerSide, long now, ShipClassRegistry registry)
    {
        var pic = Of(playerSide);
        v.Count = 0;
        if (pic == null)
            return;
        v.EnsureCapacity(pic.Count);
        for (int i = 0; i < pic.Count; i++)
        {
            ref readonly var c = ref pic[i];
            var (p, vel, cov) = pic.EstimateAt(i, now, settings);
            var (major, minor, axis) = cov.Ellipse();
            (v.Entity[i], v.TakenFor[i], v.Position[i], v.Velocity[i]) = (c.Entity, c.TakenFor, p, vel);
            (v.MajorM[i], v.MinorM[i], v.AxisDeg[i]) = (major, minor, axis);
            (v.InSight[i], v.SeenMs[i], v.Level[i], v.Type[i]) = (c.InSight, c.SeenMs, c.Level, c.Type);
            (v.SmokeInSight[i], v.LightInSight[i], v.From[i]) = (c.SmokeInSight, c.LightInSight, c.From);
            v.Side[i] = c.Foe ? c.Side : (byte)255;
            v.Seen[i] = c.Seen;
        }
        v.Count = pic.Count;
    }

    public void Hash(ref StateHash h)
    {
        h.Add(pictures.Count);
        foreach (var p in pictures)
            p.Hash(ref h);
    }
}
