using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>The smoke field's store (docs/battle/smoke.md Z1): Gaussian puffs (<see cref="SmokePuff"/>, closed form in
/// their age, <see cref="SmokeModel"/>), laid by ships off rails, carried by the air's frame, merged along each ship's
/// trail and retired when they've thinned to nothing. Behind <see cref="SimSettings.Smoke"/>, off.
/// <list type="bullet">
/// <item><b>A tick on a lattice of world time</b> (<see cref="SimSettings.SmokeTickMs"/>, the coarse tier's
/// <see cref="SimSettings.SmokeCoarseTickMs"/>): the air carried on, each ship's smoke since her last tick laid, a slice of
/// the trails merged. Nothing depends on the speed within a tier (the user's core rule, 2026-10-21); the coarse tier lays
/// the same smoke in fewer, wider puffs.</item>
/// <item><b>One trail a ship:</b> her funnels' plumes as one (<see cref="SmokeSources.Funnel"/>, their output summed;
/// where they leave, how high they rise, A-weighted; their spread over her length in the puff's σ). A tick's run through
/// the air is cut into puffs at most <see cref="SimSettings.SmokeSpacingM"/> long, each laid where she was then.</item>
/// <item><b>Off rails only:</b> sight reads smoke only in encounters. A ship coming off rails lays her trail back
/// <see cref="SimSettings.SmokeBackfillS"/> along her course at her speed, aged as if laid then, in wider puffs the older
/// they are; a ship going down makes none.</item>
/// <item><b>Merging</b> (moment-preserving, <see cref="SmokeModel.Merge"/>): neighbours along a trail, both settled,
/// centres within <see cref="SimSettings.SmokeMergeSigma"/> of the larger σ; a trail every
/// <see cref="SimSettings.SmokeMergeEveryMs"/>. Over <see cref="SimSettings.SmokeCap"/>, twice as far and every trail.</item>
/// </list>
/// Grown, never shrunk: nothing allocates once the arrays have grown to the battle. Always registered seventh
/// (<see cref="Id"/>).</summary>
public sealed class SmokeSystem(SimSettings settings) : IWorldSystem
{
    public const byte Id = 7;
    const ushort TickEvent = 1;

    /// <summary>A ship's trail: its puffs oldest to newest (linked), how many, the world time she last smoked to, whether
    /// she was off rails and afloat at the last tick (a dormant trail only ages); and her plume as last worked out (every
    /// <see cref="SimSettings.SmokePlumeMs"/>): its output, m²/s, where it leaves over the sea and how high it rises, m,
    /// over how long, s, and where it leaves in her frame (along, across), m, and the funnels' spread about it, m².</summary>
    struct Source
    {
        public int Entity, Oldest, Newest, Puffs;
        public long LastMs, PlumeMs;
        public bool Active, Gone;
        public double Rate, BaseM, Rise, RiseS, X, Y, Spread2;
    }

    SmokePuff[] puffs = new SmokePuff[1024];
    int[] owner = new int[1024], prev = new int[1024], next = new int[1024], free = new int[1024];
    int high, freeCount, live;
    readonly List<Source> sources = new(64);
    readonly Dictionary<int, int> sourceOf = new(64);

    AirFrame frame = AirFrame.Identity;
    Vec3d frameAt;
    bool frameSet, scheduled;
    long frameMs, nextMs;
    SmokeAir air;

    /// <summary>Puffs held now; trails; puffs laid, merged away and retired so far.</summary>
    public int Count => live;
    public int Trails => sources.Count;
    public long Laid { get; private set; }
    public long Merged { get; private set; }
    public long Retired { get; private set; }

    /// <summary>The air as of the last tick, and its frame.</summary>
    public SmokeAir Air => air;
    public AirFrame Frame => frame;

    /// <summary>Slots in use or freed: a puff is <see cref="IsLive"/> or not.</summary>
    public int Slots => high;
    public bool IsLive(int i) => i >= 0 && i < high && owner[i] >= 0;
    public ref readonly SmokePuff Puff(int i) => ref puffs[i];

    /// <summary>The entity whose trail puff i is in.</summary>
    public int EntityOf(int i) => sources[owner[i]].Entity;

    /// <summary>Puff i at a world time, s (by the last tick's air).</summary>
    public PuffState State(int i, double nowS) => SmokeModel.State(puffs[i], nowS, frame, air);

    /// <summary>The bytes the store holds (its arrays as grown).</summary>
    public long Bytes => (long)puffs.Length * (System.Runtime.CompilerServices.Unsafe.SizeOf<SmokePuff>() + 4 * sizeof(int));

    public void Apply(World world, in Command c) => throw new ArgumentException($"the smoke system has no command {c.Code}");

    public void Applied(World world, in Command c) => Wake(world);

    public void OffRails(World world) => Wake(world);

    /// <summary>The ticks start when a ship is off rails (and stop when no trail is left).</summary>
    void Wake(World world)
    {
        if (!settings.Smoke || scheduled)
            return;
        var store = world.Ships.Store;
        for (int s = 0; s < store.Slots; s++)
            if (store.IsAlive(s) && !store[s].OnRails)
            {
                Schedule(world);
                return;
            }
    }

    int TickMs(World world) => world.Speed >= settings.CoarseMinSpeed ? settings.SmokeCoarseTickMs : settings.SmokeTickMs;

    void Schedule(World world)
    {
        long step = TickMs(world);
        nextMs = (world.Time.Ms / step + 1) * step;
        world.Schedule(new WorldTime(nextMs), EventTier.Fine, 0, Id, TickEvent);
        scheduled = true;
    }

    public void Handle(World world, in SimEvent e)
    {
        if (e.Code != TickEvent)
            return;
        scheduled = false;
        long t = e.Time.Ms;
        int step = TickMs(world);
        var store = world.Ships.Store;
        if (!frameSet)
            for (int s = 0; s < store.Slots && !frameSet; s++)
                if (store.IsAlive(s) && !store[s].OnRails)
                    (frameAt, frameMs, frameSet) = (store[s].Position, t, true);
        air = SmokeAir.Of(world.Weather.At(frameAt, world.Time));
        if (frameSet)
            frame.Advance(frameAt, air.WindToDeg, air.DriftMs, (t - frameMs) / 1000.0);
        frameMs = t;

        for (int k = 0; k < sources.Count; k++)
        {
            var src = sources[k];
            src.Active = false;
            sources[k] = src;
        }
        bool smoking = false;
        for (int s = 0; s < store.Slots; s++)
            smoking |= Emit(world, s, t, step);

        bool over = live > settings.SmokeCap;
        int slices = Math.Max(settings.SmokeMergeEveryMs / step, 1), slice = (int)(t / step % slices);
        for (int k = 0; k < sources.Count; k++)
            if (over || k % slices == slice)
                Walk(k, t / 1000.0, over ? 2 * settings.SmokeMergeSigma : settings.SmokeMergeSigma);
        Forget();
        if (smoking || live > 0)
            Schedule(world);
    }

    /// <summary>Ship slot s's smoke since her last tick, laid; true when she smokes.</summary>
    bool Emit(World world, int s, long t, int step)
    {
        var store = world.Ships.Store;
        if (!store.IsAlive(s))
            return false;
        var ship = store[s];
        if (ship.OnRails || store.Combat.Condition(s).Status == ShipStatus.GoingDown)
            return false;
        var c = ship.Handling.Class;
        if (c.Funnels.Length == 0)
            return false;
        double speed = ship.Speed, kn = Math.Abs(speed) / Handling.MsPerKnot;
        store.PoseNow(s, out var pos, out var fwd);
        pos += fwd * (speed * (t - ship.PoseMs) / 1000.0);   // a fine ship's pose is its last tick's
        var here = air;   // the weather is one for the world (UniformWeather); a ship's own when it varies by place
        var wind = Earth.Forward(pos, here.WindToDeg) * here.WindMs;
        var vShip = fwd * speed;
        int k = SourceOf(ship.Entity, t, out bool fresh);
        var src = sources[k];
        src.Active = true;
        if (fresh || t - src.PlumeMs >= settings.SmokePlumeMs)
            Plume(ref src, c, kn, (wind - vShip).Length, here, t);
        long last = src.LastMs;
        src.LastMs = t;
        sources[k] = src;
        if (!(src.Rate > 0))
            return true;   // stopped: no smoke, her trail kept
        double rate = src.Rate, baseM = src.BaseM, rise = src.Rise, riseS = src.RiseS, x = src.X, y = src.Y, spread2 = src.Spread2;
        var up = pos.Normalized();
        var at = pos + fwd * x + Vec3d.Cross(fwd, up).Normalized() * y;
        var drift = Earth.Forward(pos, air.WindToDeg) * air.DriftMs;
        var vAir = vShip - drift;   // her way through the air
        var tint = c.Smoke.Fuel == Fuel.Coal ? SmokeTint.Coal : SmokeTint.Oil;
        long from = fresh ? t - (long)(settings.SmokeBackfillS * 1000) : Math.Max(last, t - (long)(settings.SmokeBackfillS * 1000));
        // what's still to lay, newest first in chunks: one tick, then wider the older (a quarter of their age), so a
        // backfilled trail is a few dozen puffs, not thousands
        long end = t;
        int chunks = 0;
        Span<long> starts = stackalloc long[64];
        while (end > from && chunks < starts.Length)
        {
            long len = Math.Max(step, (t - end) / 4);
            long start = Math.Max(end - len, from);
            starts[chunks++] = start;
            end = start;
        }
        // laid oldest first, so the trail's links run oldest to newest
        for (int j = chunks - 1; j >= 0; j--)
            Lay(k, starts[j], j > 0 ? starts[j - 1] : t, t, at, vAir, rate, baseM, rise, riseS, spread2, tint);
        return true;
    }

    /// <summary>Her funnels as one plume (<see cref="SmokeSources.Funnel"/>): output summed, the rest A-weighted, their
    /// spread over her length kept for the puffs' σ. Worked out every <see cref="SimSettings.SmokePlumeMs"/> (a boiler's
    /// output follows the power over tens of seconds).</summary>
    static void Plume(ref Source src, ShipClass c, double kn, double relativeMs, in SmokeAir air, long t)
    {
        double rate = 0, baseM = 0, rise = 0, riseS = 0, x = 0, y = 0, x2 = 0;
        for (int i = 0; i < c.Funnels.Length; i++)
        {
            var p = SmokeSources.Funnel(c, i, kn, relativeMs, air);
            if (!(p.RateM2s > 0))
                continue;
            var b = c.Components[c.Funnels[i].Component].Bounds;
            double fx = (b.X0 + b.X1) / 2, fy = (b.Y0 + b.Y1) / 2;
            rate += p.RateM2s;
            baseM += p.RateM2s * p.BaseM;
            rise += p.RateM2s * p.RiseM;
            riseS += p.RateM2s * p.RiseS;
            x += p.RateM2s * fx;
            y += p.RateM2s * fy;
            x2 += p.RateM2s * (fx * fx + fy * fy);
        }
        src.PlumeMs = t;
        src.Rate = rate;
        if (!(rate > 0))
            return;
        (src.BaseM, src.Rise, src.RiseS, src.X, src.Y) = (baseM / rate, rise / rate, riseS / rate, x / rate, y / rate);
        src.Spread2 = Math.Max(x2 / rate - src.X * src.X - src.Y * src.Y, 0) / 2;
    }

    /// <summary>The smoke made over (a, b], ms, cut into puffs at most the spacing long, each where she was at its end
    /// (she's at <paramref name="at"/> at <paramref name="t"/>, making way <paramref name="vAir"/> through the air);
    /// those thinned to nothing by now left out.</summary>
    void Lay(int k, long a, long b, long t, Vec3d at, Vec3d vAir, double rate, double baseM, double rise, double riseS, double spread2,
        SmokeTint tint)
    {
        double span = (b - a) / 1000.0;
        if (!(span > 0))
            return;
        double run = vAir.Length * span;
        int n = Math.Clamp((int)Math.Ceiling(run / settings.SmokeSpacingM), 1, settings.SmokePuffsPerTick);
        double piece = run / n, s0 = settings.SmokeSigma0M;
        double sigma = Math.Sqrt(s0 * s0 + spread2 + piece * piece / 24);   // the piece's length, half on each axis
        double area = rate * span / n, nowS = t / 1000.0;
        for (int i = 0; i < n; i++)
        {
            double born = (a + (b - a) * (i + 1.0) / n) / 1000.0, ago = nowS - born;
            var puff = SmokeModel.Born(at - vAir * ago, born, frame, baseM, rise, riseS, sigma, s0, area, 0, tint);
            if (ago > 0)
            {
                SmokeModel.Plan(puff, nowS, frame, air, out _, out double h2, out double left, out _);
                if (left / (2 * Math.PI * h2) < SmokeModel.GoneTau)
                    continue;   // a backfilled puff already thinned to nothing
            }
            Add(k, puff);
        }
    }

    int SourceOf(int entity, long t, out bool fresh)
    {
        if (sourceOf.TryGetValue(entity, out int k))
        {
            var s = sources[k];
            fresh = s.Gone;
            if (s.Gone)
            {
                s.Gone = false;
                sources[k] = s;
            }
            return k;
        }
        fresh = true;
        sources.Add(new Source { Entity = entity, Oldest = -1, Newest = -1, LastMs = t });
        sourceOf[entity] = sources.Count - 1;
        return sources.Count - 1;
    }

    void Add(int k, in SmokePuff p)
    {
        int i = freeCount > 0 ? free[--freeCount] : high++;
        if (i >= puffs.Length)
            Grow();
        var s = sources[k];
        (puffs[i], owner[i], prev[i], next[i]) = (p, k, s.Newest, -1);
        if (s.Newest >= 0)
            next[s.Newest] = i;
        else
            s.Oldest = i;
        s.Newest = i;
        s.Puffs++;
        sources[k] = s;
        live++;
        Laid++;
    }

    void Remove(int i)
    {
        int k = owner[i];
        var s = sources[k];
        if (prev[i] >= 0)
            next[prev[i]] = next[i];
        else
            s.Oldest = next[i];
        if (next[i] >= 0)
            prev[next[i]] = prev[i];
        else
            s.Newest = prev[i];
        s.Puffs--;
        sources[k] = s;
        owner[i] = -1;
        if (freeCount == free.Length)
            Array.Resize(ref free, free.Length * 2);
        free[freeCount++] = i;
        live--;
    }

    void Grow()
    {
        int n = puffs.Length * 2;
        Array.Resize(ref puffs, n);
        Array.Resize(ref owner, n);
        Array.Resize(ref prev, n);
        Array.Resize(ref next, n);
    }

    /// <summary>A trail walked oldest to newest: the thinned retired, settled neighbours close together merged.</summary>
    void Walk(int k, double nowS, double mergeSigma)
    {
        int p = sources[k].Oldest;
        bool known = false;   // p's plan view carried over from the last pair
        Vec3d cp = default;
        double hp = 0, ap = 0, agep = 0;
        while (p >= 0)
        {
            if (!known)
                SmokeModel.Plan(puffs[p], nowS, frame, air, out cp, out hp, out ap, out agep);
            known = false;
            if (ap / (2 * Math.PI * hp) < SmokeModel.GoneTau)
            {
                int n0 = next[p];
                Remove(p);
                Retired++;
                p = n0;
                continue;
            }
            int q = next[p];
            if (q < 0 || agep < settings.SmokeSettleS)
                break;   // the newer are younger still
            SmokeModel.Plan(puffs[q], nowS, frame, air, out var cq, out double hq, out double aq, out double ageq);
            if (ageq < settings.SmokeSettleS)
                break;
            double reach2 = mergeSigma * mergeSigma * Math.Max(hp, hq);
            if ((cp - cq).LengthSquared < reach2)
            {
                puffs[p] = SmokeModel.Merge(puffs[p], SmokeModel.State(puffs[p], nowS, frame, air), puffs[q],
                    SmokeModel.State(puffs[q], nowS, frame, air), nowS, frame, air);
                Remove(q);
                Merged++;
                continue;   // the merged puff against its next
            }
            (cp, hp, ap, agep, known) = (cq, hq, aq, ageq, true);
            p = q;
        }
    }

    /// <summary>Trails no ship smokes into and with no puffs left are forgotten (the last moved into their place).</summary>
    void Forget()
    {
        for (int k = sources.Count - 1; k >= 0; k--)
        {
            var s = sources[k];
            if (s.Active)
                continue;
            if (s.Puffs > 0)
            {
                if (!s.Gone)
                {
                    s.Gone = true;   // dormant: off smoking (on rails, stopped, gone down); her trail only ages
                    sources[k] = s;
                }
                continue;
            }
            sourceOf.Remove(s.Entity);
            int last = sources.Count - 1;
            if (k != last)
            {
                var moved = sources[last];
                sources[k] = moved;
                sourceOf[moved.Entity] = k;
                for (int i = moved.Oldest; i >= 0; i = next[i])
                    owner[i] = k;
            }
            sources.RemoveAt(last);
        }
    }

    public void Hash(ref StateHash h)
    {
        h.Add(scheduled);
        h.Add(nextMs);
        h.Add(frameSet);
        h.Add(frameMs);
        h.Add(frameAt);
        frame.Hash(ref h);
        h.Add(sources.Count);
        foreach (var s in sources)
        {
            h.Add(s.Entity);
            h.Add(s.Oldest);
            h.Add(s.Newest);
            h.Add(s.Puffs);
            h.Add(s.LastMs);
            h.Add(s.Gone);
        }
        h.Add(live);
        for (int k = 0; k < sources.Count; k++)
            for (int i = sources[k].Oldest; i >= 0; i = next[i])
            {
                ref readonly var p = ref puffs[i];
                h.Add(p.Origin);
                h.Add(p.Born);
                h.Add(p.Z0);
                h.Add(p.Rise);
                h.Add(p.RiseS);
                h.Add(p.S0h2);
                h.Add(p.S0z2);
                h.Add(p.A0);
                h.Add(p.LifeS);
                h.Add((int)p.Tint);
            }
    }
}
