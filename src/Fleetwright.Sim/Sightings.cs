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
/// </list></summary>
public sealed class Sightings
{
    readonly SimSettings settings;
    readonly SightReach reach;
    readonly Relations relations;
    readonly Encounters encounters;
    readonly Picture?[] bySide = new Picture?[256];
    readonly List<Picture> pictures = new(4);
    readonly RandomStream stream;
    readonly Dictionary<int, int> similar = new(64);   // a class's most similar other class in the world, or -1
    int similarFor = -1;   // the count of classes in use it was worked out for

    // a look's ships (those in encounters), sorted by encounter: slot, side, encounter, position, their place in the
    // encounter's grid
    int[] slot = [], enc = [], order = [], cellOf = [], cellShips = [];
    // an encounter's grid, per cell: sized for an encounter 250 km across (8 km cells), so one that spreads (a ship falling
    // out and running) doesn't grow it in a fight; a sprawling one grows it once
    int[] cellStart = new int[1024];
    byte[] side = [];
    double[] gx = [], gy = [];
    readonly int[] nearIdx = new int[16];
    readonly double[] nearD = new double[16];
    readonly byte[] sidesHere = new byte[256];

    internal Sightings(SimSettings settings, Relations relations, Encounters encounters, SightReach reach, ulong seed)
    {
        this.settings = settings;
        this.reach = reach;
        this.relations = relations;
        this.encounters = encounters;
        stream = RandomStream.Named(seed, "Sighting");
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
        long now = world.Time.Ms;
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
        OutOfSight(now);
        Forget(now);
    }

    /// <summary>Contacts in sight that no look was made for this time (their ships out of the encounters, or gone) go out
    /// of sight.</summary>
    void OutOfSight(long now)
    {
        foreach (var p in pictures)
            for (int i = 0; i < p.Count; i++)
                if (p[i].InSight && p[i].LookedMs != now)
                    p.At(i).InSight = false;
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
        for (int i = a; i < b; i++)
        {
            int t = order[i];
            for (int si = 0; si < sideCount; si++)
            {
                byte by = sidesHere[si];
                if (!relations.Hostile(by, side[t]))
                    continue;
                int found = Nearest(t, by, w, h, minX, minY, cell);
                if (found > 0)
                    Sight(world, store, t, by, found, now, dt, weather.VisibilityM, sunEl, lowSun ? sunBrg : double.NaN, classesInUse);
            }
        }
    }

    /// <summary>The nearest observers of a side to ship t, ring by ring through the grid, into nearIdx (closest first);
    /// how many (at most <see cref="SimSettings.SightObservers"/>).</summary>
    int Nearest(int t, byte by, int w, int h, double minX, double minY, double cell)
    {
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

    /// <summary>Side `by`'s look for ship t with its nearest observers: the summed rate, the best margin and who had it;
    /// then a fix, a detection, or the contact going out of sight.</summary>
    void Sight(World world, ShipStore store, int t, byte by, int found, long now, double dt, double vis, double sunEl, double sunBrg,
        IReadOnlyList<int> classesInUse)
    {
        var ts = store[slot[t]];
        var tc = ts.Handling.Class;
        double tTop = tc.Silhouette.TopM;
        // her fires as a light at night (fire.md B2)
        double glowMW = store.Combat.Condition(ts.Slot).FireShowMW, glowM = settings.FireGlowHeightShare * tTop;
        var pic = Get(by);
        int ci = pic.IndexOf(ts.Entity);
        bool inSight = ci >= 0 && pic[ci].InSight;
        // the rates add; the fix comes from the eye that ranges it best (a rangefinder before an eye's guess), and once
        // a held contact is seen by a rangefinder no more need look
        double rate = 0, best = 0, fixD = 0, fixBrg = 0, fixSigma = double.MaxValue, fixMargin = 0;
        int fixObs = -1, fixEye = -1;
        bool done = false;
        Span<double> smoke = stackalloc double[SmokeSystem.PairTaus];
        for (int m = 0; m < found && !done; m++)
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
            double aspect = Earth.Bearing(ts.Position, os.Position) - ts.HeadingDeg;
            bool sil = !double.IsNaN(sunBrg) && Math.Abs(Angle(brg - sunBrg)) <= settings.SightSilhouetteArcDeg;
            var line = new SightLine(d, aspect, vis, sunEl, sil);
            bool smoked = world.Smoke.On;
            if (smoked)
                world.Smoke.Tau(world, os.Slot, ts.Slot, smoke);   // the smoke on the lines (docs/battle/smoke.md Z3)
            int tried = 0;
            var works = store.Combat.Condition(slot[nearIdx[m]]).EyeWorks;   // damage: a wrecked post sees nothing
            for (int e = 0; e < eyes.Length && tried < settings.SightEyes && !done; e++)
            {
                if (!eyes[e].Sees(rel) || !works[e])
                    continue;
                tried++;
                if (smoked)
                    line = line with { Smoke = SmokeModel.EyeTau(smoke, eyes, eyes[e].HeightM) };
                double margin = SightModel.Margin(eyes[e].HeightM, SightModel.Magnification(eyes[e], settings), tc, line, settings);
                if (glowMW > 0)
                    margin = Math.Max(margin, SightModel.GlowMargin(eyes[e].HeightM, glowMW, glowM, d, vis, sunEl, settings,
                        SightModel.SmokeAt(line.Smoke, glowM, tTop, settings)));
                rate += SightModel.Rate(margin, SightModel.SearchFactor(eyes[e], settings), settings);
                best = Math.Max(best, margin);
                if (margin < 1)
                    continue;
                double sigma = SightModel.RangeSigmaM(eyes[e], d, margin, settings);
                if (sigma < fixSigma)
                    (fixSigma, fixD, fixBrg, fixObs, fixEye, fixMargin) = (sigma, d, brg, nearIdx[m], e, margin);
                done = inSight && eyes[e].RangefinderM > 0;
            }
        }
        if (ci >= 0)
            pic.At(ci).LookedMs = now;
        var r = stream.Derive(((ulong)by << 56) ^ ((ulong)(uint)ts.Entity << 24) ^ (ulong)(now / settings.SightingMs));
        if (inSight && best >= 1 || !inSight && rate > 0 && r.NextDouble() < SightModel.Chance(rate, dt))
        {
            var os = store[slot[fixObs]];
            Fix(pic, ci, ts, store.Combat.Condition(ts.Slot), os, os.Handling.Class.Eyes[fixEye], best, fixMargin, fixD, fixBrg, now, ref r, classesInUse);
        }
        else if (inSight && best < 1)
            pic.At(ci).InSight = false;
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
        con.Observer = os.Entity;
        con.Eye = eye.Kind;
        con.Seen = SeenOf(ts, tk) | con.Seen & SeenFlags.Gone;
        Identify(ref con, ts, margin, d, ref r, classesInUse);
    }

    /// <summary>What an observer makes out of a ship's condition (docs/battle/fighting.md 4.3b): settled low, a list, a
    /// trim, fire and smoke, stopped, its guns silent, going under. Never its water or what's left of its buoyancy.</summary>
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
        if (k.MountWorks.Length > 0 && k.MountsWorking == 0)
            seen |= SeenFlags.Silent;
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
            c.Type = TypeOf(c.TakenFor >= 0 && Registry?.Get(c.TakenFor) is { } taken ? taken : ts.Handling.Class);
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
            v.Side[i] = c.Level >= ContactLevel.Type ? c.Side : (byte)255;
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
