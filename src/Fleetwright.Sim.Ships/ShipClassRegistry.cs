using System.Collections.Concurrent;
using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>The classes in play, one per design hash (docs/battle/plan.md step 2.3): each design is built and compiled
/// once, on a few threads of its own, and no request is ever dropped (unlike the designer's worker, where the newest
/// wins). A design gets a small dense id when first asked for, the same id every later time; ids are what commands and
/// snapshots carry. <see cref="Get"/> is the one seam ("the class for this id"), so a disk cache can slot in behind it.
/// <para><see cref="Built"/> hands each freshly built <see cref="Ship"/> to whoever draws (the exe's art), on the
/// building thread, before the ship is let go: a design is built once for the class and the art both. A registry holds
/// no Ship afterwards.</para>
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
        public ClassTimings Timings;
    }

    /// <summary>What a class's build took, seconds on its building thread (docs/battle/plan.md step 2.7b, the load's
    /// timeline): shipgen's build, the compile, and the built hook (the art's drawing and bake, in the app).</summary>
    public readonly record struct ClassTimings(double Build, double Compile, double Hook)
    {
        public double Total => Build + Compile + Hook;
    }

    readonly object gate = new();
    readonly List<Entry> entries = [];
    readonly Dictionary<ContentHash, int> byHash = [];
    readonly BlockingCollection<int> queue = [];
    readonly Thread[] workers;
    bool started;
    volatile bool closing;

    /// <summary>Called on a building thread with each design's id, its built ship and its class (null if the compile
    /// failed), before the ship is let go. Set it before the first request.</summary>
    public Action<int, Ship, ShipClass?>? Built { get; set; }

    /// <param name="threads">Building threads; 0 picks half the cores, at most 4.</param>
    public ShipClassRegistry(int threads = 0)
    {
        int n = threads > 0 ? threads : Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        workers = new Thread[n];
        for (int i = 0; i < n; i++)
            workers[i] = new Thread(Work) { Name = $"Class builder {i}", IsBackground = true, Priority = ThreadPriority.BelowNormal };
    }

    /// <summary>Asks for a design's class: its id, now; the build runs in the background unless the design was asked
    /// for before.</summary>
    public int Request(Design design)
    {
        var hash = ContentHash.OfDesign(design);
        lock (gate)
        {
            if (byHash.TryGetValue(hash, out int id))
                return id;
            id = entries.Count;
            entries.Add(new Entry { Hash = hash, Design = design });
            byHash[hash] = id;
            queue.Add(id);
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
            id = entries.Count;
            var e = new Entry { Hash = c.DesignHash, Class = c };
            e.Done.Set();
            entries.Add(e);
            byHash[c.DesignHash] = id;
            return id;
        }
    }

    readonly List<Loadout> loadouts = [];

    /// <summary>Adds a loadout (a ship's shells other than her design's, docs/battle/shells-armour-damage.md S5): its id,
    /// the one a <c>ShipSystem.Load</c> command carries; the same loadout of the same class gets the same id.</summary>
    public int AddLoadout(Loadout l)
    {
        lock (gate)
        {
            for (int i = 0; i < loadouts.Count; i++)
                if (ReferenceEquals(loadouts[i].Class, l.Class) && loadouts[i].Hash == l.Hash)
                    return i;
            loadouts.Add(l);
            return loadouts.Count - 1;
        }
    }

    public int LoadoutCount
    {
        get
        {
            lock (gate)
                return loadouts.Count;
        }
    }

    /// <summary>A loadout by its id, or null for none.</summary>
    public Loadout? LoadoutOf(int id)
    {
        lock (gate)
            return id >= 0 && id < loadouts.Count ? loadouts[id] : null;
    }

    public int Count
    {
        get
        {
            lock (gate)
                return entries.Count;
        }
    }

    public ContentHash HashOf(int id) => EntryOf(id).Hash;

    /// <summary>Built (or failed) yet?</summary>
    public bool IsReady(int id) => EntryOf(id).Done.IsSet;

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

    /// <summary>What a class's build took (zeros until it's built, or for one added already compiled).</summary>
    public ClassTimings TimingsOf(int id) => EntryOf(id).Timings;

    /// <summary>A class's name if it's built, else null.</summary>
    public string? NameOf(int id) => TryGet(id)?.Name;

    /// <summary>Blocks until every request so far is built or failed.</summary>
    public void WaitAll()
    {
        int n = Count;
        for (int i = 0; i < n; i++)
            EntryOf(i).Done.Wait();
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
            return [.. Enumerable.Range(0, entries.Count).Where(i => entries[i].Refs == 0 && entries[i].Done.IsSet)];
    }

    Entry EntryOf(int id)
    {
        lock (gate)
            return id >= 0 && id < entries.Count ? entries[id] : throw new ArgumentOutOfRangeException(nameof(id), $"no class {id}");
    }

    void Work()
    {
        foreach (int id in queue.GetConsumingEnumerable())
        {
            var e = EntryOf(id);
            if (closing)
            {
                e.Error = "the registry closed before it was built";   // what's queued at Dispose isn't built
                e.Design = null;
                e.Done.Set();
                continue;
            }
            try
            {
                var design = e.Design!;
                if (ShipDesign.Validate(design, limits: false) is { Count: > 0 } errs)
                    e.Error = "invalid design: " + string.Join("; ", errs);
                else
                {
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    var ship = ShipDesign.Build(design);
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
                    try
                    {
                        Built?.Invoke(id, ship, e.Class);
                    }
                    catch (Exception x)
                    {
                        Console.Error.WriteLine($"class registry: the built hook failed on {id}: {x}");   // the art's failure, not the class's
                    }
                    long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                    double f = System.Diagnostics.Stopwatch.Frequency;
                    e.Timings = new ClassTimings((t1 - t0) / f, (t2 - t1) / f, (t3 - t2) / f);
                }
            }
            catch (Exception x)
            {
                e.Error ??= $"{x.GetType().Name}: {x.Message}";
            }
            e.Design = null;
            e.Done.Set();
        }
    }

    /// <summary>Waits for the builds in flight; those still queued are let go unbuilt (failed, so no waiter hangs).</summary>
    public void Dispose()
    {
        closing = true;
        queue.CompleteAdding();
        if (started)
            foreach (var t in workers)
                t.Join();
        queue.Dispose();
    }
}
