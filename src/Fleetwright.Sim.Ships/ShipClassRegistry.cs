using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>The classes in play, one per design hash (docs/battle/plan.md step 2.3): each design is built and compiled
/// once, on a few threads of its own, and no request is ever dropped (unlike the designer's worker, where the newest
/// wins). A design gets a small dense id when first asked for, the same id every later time; ids are what commands and
/// snapshots carry. <see cref="Get"/> is the one seam ("the class for this id"), so a disk cache can slot in behind it.
/// <para><see cref="Built"/> hands each freshly built <see cref="Ship"/> to whoever draws (the exe's art), on a
/// building thread, before the ship is let go: a design is built once for the class and the art both. Two queues
/// (docs/map-ui/launch.md L0): every class's build and compile goes before any drawing, since a world needs only the
/// classes, so a class is ready (<see cref="Get"/>) before its art; its ship is held until it's drawn
/// (<see cref="IsDrawn"/>), the preferred classes' first (<see cref="Prefer"/>). A registry holds no Ship
/// afterwards.</para>
/// <para>The threads: a few beside a running world, more while none runs (<see cref="Idle"/>).</para>
/// <para>References are counted (<see cref="Acquire"/>, <see cref="Release"/>): what reaches zero may be freed
/// (<see cref="Unused"/>). Thread-safe throughout; when a build finishes can never change what a world does, since
/// a world that needs a class waits for it.</para></summary>
public sealed class ShipClassRegistry : IDisposable
{
    sealed class Entry
    {
        public required ContentHash Hash;
        public Design? Design;
        public ShipClass? Class;
        public string? Error;
        public int Refs;
        public readonly ManualResetEventSlim Done = new(false);
        public readonly ManualResetEventSlim Drawn = new(false);
        public ClassTimings Timings;
        public Ship? Ship;   // built, waiting for the built hook (under the gate)
        public bool First;   // its drawing goes before the others' (under the gate)
    }

    /// <summary>What a class's build took, seconds on its building thread (docs/battle/plan.md step 2.7b, the load's
    /// timeline): shipgen's build, the compile, and the built hook (the art's drawing and bake, in the app).</summary>
    public readonly record struct ClassTimings(double Build, double Compile, double Hook)
    {
        public double Total => Build + Compile + Hook;
    }

    readonly object gate = new();
    // read without the lock: append-only, grown under it by a copy (adds are rare), since the sim thread reads a class
    // many times a moment and a lock once contended by the builders stays on its slow path
    volatile Entry[] entries = [];
    readonly Dictionary<ContentHash, int> byHash = [];
    // the work, under the gate: every build before any drawing, the preferred drawings first (one preferred while
    // already queued later is queued again first, and skipped at its later place)
    readonly Queue<int> builds = [], drawFirst = [], drawLater = [];
    readonly Thread[] workers;
    readonly int narrow;
    volatile int active;   // the threads that take work: the first `active` of them
    bool started;
    volatile bool closing;

    /// <summary>Called on a building thread with each design's id, its built ship and its class (null if the compile
    /// failed), before the ship is let go, after every build queued before it. Set it before the first request.</summary>
    public Action<int, Ship, ShipClass?>? Built { get; set; }

    /// <param name="threads">Building threads beside a running world; 0 picks half the cores, at most 4.</param>
    /// <param name="idleThreads">Building threads while no world runs (<see cref="Idle"/>); 0 picks all the cores but
    /// two, at most 8 (past 8 the art came no sooner at Jutland, and crowded the world's placing: docs/map-ui/launch.md
    /// L0). Never fewer than <paramref name="threads"/>.</param>
    public ShipClassRegistry(int threads = 0, int idleThreads = 0)
    {
        narrow = active = threads > 0 ? threads : Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        int wide = Math.Max(narrow, idleThreads > 0 ? idleThreads : Math.Min(Environment.ProcessorCount - 2, 8));
        workers = new Thread[wide];
        for (int i = 0; i < wide; i++)
        {
            int index = i;
            workers[i] = new Thread(() => Work(index)) { Name = $"Class builder {i}", IsBackground = true, Priority = ThreadPriority.BelowNormal };
        }
    }

    /// <summary>No world is running (the launch screen, a paused or empty world): the registry works on all its
    /// threads, else on the few it may take beside a world. A thread let go finishes the job it has.</summary>
    public bool Idle
    {
        get => active > narrow;
        set
        {
            lock (gate)
            {
                int n = value ? workers.Length : narrow;
                if (n == active)
                    return;
                active = n;
                Monitor.PulseAll(gate);
            }
        }
    }

    /// <summary>The building threads taking work now.</summary>
    public int Threads => active;

    /// <summary>Asks for a design's class: its id, now; the build runs in the background unless the design was asked
    /// for before.</summary>
    public int Request(Design design)
    {
        var hash = ContentHash.OfDesign(design);
        lock (gate)
        {
            if (byHash.TryGetValue(hash, out int id))
                return id;
            id = entries.Length;
            entries = [.. entries, new Entry { Hash = hash, Design = design }];
            byHash[hash] = id;
            builds.Enqueue(id);
            Monitor.PulseAll(gate);   // all: one woken past the active threads would wait on
            if (!started)
            {
                started = true;   // the threads start with the first build, so a registry of ready classes costs none
                foreach (var w in workers)
                    w.Start();
            }
            return id;
        }
    }

    /// <summary>Adds a class already compiled (tests, tools): its id, ready at once.</summary>
    public int Add(ShipClass c)
    {
        lock (gate)
        {
            if (byHash.TryGetValue(c.DesignHash, out int id))
                return id;
            id = entries.Length;
            var e = new Entry { Hash = c.DesignHash, Class = c };
            e.Done.Set();
            e.Drawn.Set();
            entries = [.. entries, e];
            byHash[c.DesignHash] = id;
            return id;
        }
    }

    volatile Loadout[] loadouts = [];   // as entries

    /// <summary>Adds a loadout (a ship's shells other than her design's, docs/battle/shells-armour-damage.md S5): its id,
    /// the one a <c>ShipSystem.Load</c> command carries; the same loadout of the same class gets the same id.</summary>
    public int AddLoadout(Loadout l)
    {
        lock (gate)
        {
            for (int i = 0; i < loadouts.Length; i++)
                if (ReferenceEquals(loadouts[i].Class, l.Class) && loadouts[i].Hash == l.Hash)
                    return i;
            loadouts = [.. loadouts, l];
            return loadouts.Length - 1;
        }
    }

    public int LoadoutCount => loadouts.Length;

    /// <summary>A loadout by its id, or null for none.</summary>
    public Loadout? LoadoutOf(int id)
    {
        var all = loadouts;
        return id >= 0 && id < all.Length ? all[id] : null;
    }

    public int Count => entries.Length;

    public ContentHash HashOf(int id) => EntryOf(id).Hash;

    /// <summary>Built (or failed) yet?</summary>
    public bool IsReady(int id) => EntryOf(id).Done.IsSet;

    /// <summary>Its built hook has run (its art drawn), or there was nothing to draw: no hook, or no ship built.</summary>
    public bool IsDrawn(int id) => EntryOf(id).Drawn.IsSet;

    /// <summary>The class's drawing goes before those not preferred (the player's side's art first).</summary>
    public void Prefer(int id)
    {
        lock (gate)
        {
            var e = EntryOf(id);
            if (e.First)
                return;
            e.First = true;
            if (e.Ship != null)
                drawFirst.Enqueue(id);
        }
    }

    /// <summary>The class, waiting for its build if it hasn't finished; null if the design didn't build (see
    /// <see cref="ErrorOf"/>).</summary>
    public ShipClass? Get(int id)
    {
        var e = EntryOf(id);
        e.Done.Wait();
        return e.Class;
    }

    /// <summary>The class if it's ready, else null, without waiting.</summary>
    public ShipClass? TryGet(int id)
    {
        var e = EntryOf(id);
        return e.Done.IsSet ? e.Class : null;
    }

    public string? ErrorOf(int id) => EntryOf(id).Error;

    /// <summary>What a class's build took (zeros until it's built, or for one added already compiled; the hook's once
    /// it's drawn).</summary>
    public ClassTimings TimingsOf(int id) => EntryOf(id).Timings;

    /// <summary>A class's name if it's built, else null.</summary>
    public string? NameOf(int id) => TryGet(id)?.Name;

    /// <summary>Blocks until every request so far is built or failed, and drawn.</summary>
    public void WaitAll()
    {
        int n = Count;
        for (int i = 0; i < n; i++)
            EntryOf(i).Drawn.Wait();
    }

    public void Acquire(int id)
    {
        lock (gate)
            EntryOf(id).Refs++;
    }

    public void Release(int id)
    {
        lock (gate)
        {
            var e = EntryOf(id);
            if (e.Refs == 0)
                throw new InvalidOperationException($"class {id} released more than acquired");
            e.Refs--;
        }
    }

    public int RefsOf(int id)
    {
        lock (gate)
            return EntryOf(id).Refs;
    }

    /// <summary>The ids nothing holds: what may be freed.</summary>
    public List<int> Unused()
    {
        lock (gate)
            return [.. Enumerable.Range(0, entries.Length).Where(i => entries[i].Refs == 0 && entries[i].Drawn.IsSet)];
    }

    Entry EntryOf(int id)
    {
        var all = entries;
        return id >= 0 && id < all.Length ? all[id] : throw new ArgumentOutOfRangeException(nameof(id), $"no class {id}");
    }

    /// <summary>Thread <paramref name="index"/>'s next job, under the gate: a build if one waits, else a drawing (its
    /// ship handed over); false once the registry is closing and nothing is left. A thread past the active ones waits,
    /// unless closing.</summary>
    bool Next(int index, out int id, out Ship? draw)
    {
        draw = null;
        while (true)
        {
            if (index < active || closing)
            {
                if (builds.TryDequeue(out id))
                    return true;
                while (drawFirst.TryDequeue(out id) || drawLater.TryDequeue(out id))
                    if (EntryOf(id).Ship is { } ship)   // else drawn from its other place in the queues
                    {
                        (draw, EntryOf(id).Ship) = (ship, null);
                        return true;
                    }
                if (closing)
                    return false;
            }
            Monitor.Wait(gate);
        }
    }

    void Work(int index)
    {
        while (true)
        {
            int id;
            Ship? draw;
            lock (gate)
                if (!Next(index, out id, out draw))
                    return;
            if (draw != null)
                Draw(id, EntryOf(id), draw);
            else
                Build(id, EntryOf(id));
        }
    }

    void Build(int id, Entry e)
    {
        if (closing)
        {
            e.Error = "the registry closed before it was built";   // what's queued at Dispose isn't built
            e.Design = null;
            e.Done.Set();
            e.Drawn.Set();
            return;
        }
        Ship? ship = null;
        try
        {
            var design = e.Design!;
            if (ShipDesign.Validate(design, limits: false) is { Count: > 0 } errs)
                e.Error = "invalid design: " + string.Join("; ", errs);
            else
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                ship = ShipDesign.Build(design);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    e.Class = ShipClassCompiler.Compile(ship);
                }
                catch (ShipCompileException x)
                {
                    e.Error = x.Message;
                }
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                double f = System.Diagnostics.Stopwatch.Frequency;
                e.Timings = new ClassTimings((t1 - t0) / f, (t2 - t1) / f, 0);
            }
        }
        catch (Exception x)
        {
            e.Error ??= $"{x.GetType().Name}: {x.Message}";
        }
        e.Design = null;
        bool draws = ship != null && Built != null;
        if (draws)
            lock (gate)
            {
                e.Ship = ship;
                (e.First ? drawFirst : drawLater).Enqueue(id);
                Monitor.PulseAll(gate);   // all: one woken past the active threads would wait on
            }
        e.Done.Set();   // the class, before its art
        if (!draws)
            e.Drawn.Set();
    }

    void Draw(int id, Entry e, Ship ship)
    {
        if (!closing)   // closing: let go undrawn
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                Built?.Invoke(id, ship, e.Class);
            }
            catch (Exception x)
            {
                Console.Error.WriteLine($"class registry: the built hook failed on {id}: {x}");   // the art's failure, not the class's
            }
            e.Timings = e.Timings with { Hook = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) / (double)System.Diagnostics.Stopwatch.Frequency };
        }
        e.Drawn.Set();
    }

    /// <summary>Waits for the jobs in flight; the builds still queued are let go unbuilt (failed, so no waiter hangs),
    /// the drawings undrawn.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            closing = true;
            Monitor.PulseAll(gate);
        }
        if (started)
            foreach (var t in workers)
                t.Join();
    }
}
