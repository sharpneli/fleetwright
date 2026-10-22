using System.Diagnostics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>Runs a world on a thread of its own (docs/world/architecture.md, "The sim thread and the boundary"). Other
/// threads report real time passing (<see cref="Elapse"/>) and send commands (<see cref="Send"/>); one reader takes
/// the newest snapshot (<see cref="Latest"/>). The sim thread waits on a signal when it has caught up, advances toward
/// the world's target and never past it, and publishes at least every
/// <see cref="Core.SimSettings.PublishIntervalMs"/> while it's behind.
/// <para>The wall clock here only paces: it decides when commands land and when snapshots go out, never what the
/// world does with them (the commands are logged with their stamps). Once started, nothing else may touch the world
/// until the host is disposed.</para></summary>
public sealed class SimHost : IDisposable
{
    const double AchievedWindowSeconds = 0.25;   // the host's measuring window, not a sim number

    readonly World world;
    readonly CommandQueue commands;
    readonly TripleBuffer<Snapshot> snapshots = new(() => new Snapshot());
    readonly AutoResetEvent wake = new(false);
    readonly object gate = new();
    readonly Thread thread;
    volatile bool stop, truth;
    double pendingReal;

    /// <summary>Snapshots with the truth, every ship (a dev's view), rather than the player's (World.Fill). The host's
    /// choice, never the world's state: the sim can't depend on it.</summary>
    public bool Truth
    {
        get => truth;
        set => truth = value;
    }

    /// <summary>The ships the screens want detailed (<see cref="Snapshot.Details"/>; at most
    /// <see cref="Snapshot.MaxDetail"/>): the host's choice, like <see cref="Truth"/>. Set a new array to change it (the
    /// sim thread reads the reference at each publish).</summary>
    public int[] Detail
    {
        get => detail;
        set => detail = value;
    }
    volatile int[] detail = [];
    long sequence;
    string? error;

    // the achieved speed, measured over windows of wall time
    readonly Stopwatch wall = new();
    double windowStart, achieved;
    long windowGameMs;
    readonly int advanceProbe, publishProbe, waitProbe;

    public SimHost(World world)
    {
        this.world = world;
        commands = new CommandQueue(world.Settings.CommandCapacity);
        advanceProbe = world.Probes.Register("Host: advance");
        publishProbe = world.Probes.Register("Host: publish");
        waitProbe = world.Probes.Register("Host: idle");
        thread = new Thread(Run) { Name = "Sim", IsBackground = true };
    }

    public void Start() => thread.Start();

    /// <summary>Real time passed, seconds; the world turns it into game time at its asked speed.</summary>
    public void Elapse(double realSeconds)
    {
        lock (gate)
            pendingReal += realSeconds;
        wake.Set();
    }

    public void Send(in Command c)
    {
        commands.Enqueue(c);
        wake.Set();
    }

    /// <summary>The newest snapshot. One reader thread only; the instance is the reader's until its next call.</summary>
    public Snapshot Latest()
    {
        snapshots.Acquire();
        return snapshots.Front;
    }

    public void Dispose()
    {
        stop = true;
        wake.Set();
        if (thread.IsAlive)
            thread.Join();
        wake.Dispose();
    }

    void Run()
    {
        wall.Start();
        windowGameMs = world.Time.Ms;
        try
        {
            double publishMs = world.Settings.PublishIntervalMs;
            Publish();
            while (!stop)
            {
                long t = Probes.Now;
                wake.WaitOne(100);
                world.Probes.Add(waitProbe, t);
                Drain();
                // behind, the world is run in slices that end when the next snapshot is due, so the screen gets one
                // every publishMs however slow a stretch is (a slice of a fixed number of events took up to 0.8 s)
                long publishTicks = (long)(publishMs / 1000 * Stopwatch.Frequency);
                long due = Stopwatch.GetTimestamp() + publishTicks;
                while (!stop && !Advance(due))
                {
                    if (Stopwatch.GetTimestamp() >= due)
                    {
                        Publish();
                        Drain();
                        due = Stopwatch.GetTimestamp() + publishTicks;
                    }
                }
                Publish();
            }
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            Publish();
        }
    }

    bool Advance(long deadline)
    {
        long t = Probes.Now;
        bool done = world.Advance(world.Settings.EventsPerSlice, deadline);
        world.Probes.Add(advanceProbe, t);
        return done;
    }

    void Drain()
    {
        double real;
        lock (gate)
        {
            real = pendingReal;
            pendingReal = 0;
        }
        while (commands.TryDequeue(out var c))
            world.Apply(c);
        world.Elapse(real);
    }

    void Publish()
    {
        double now = wall.Elapsed.TotalSeconds;
        if (now - windowStart >= AchievedWindowSeconds)
        {
            achieved = (world.Time.Ms - windowGameMs) / 1000.0 / (now - windowStart);
            (windowStart, windowGameMs) = (now, world.Time.Ms);
        }
        long t = Probes.Now;
        var s = snapshots.Back;
        var want = detail;
        s.DetailWantedCount = Math.Min(want.Length, Snapshot.MaxDetail);
        want.AsSpan(0, s.DetailWantedCount).CopyTo(s.DetailWanted);
        world.Fill(s, Truth);
        s.Probes.CopyFrom(world.Probes);
        world.Probes.ResetMax();
        s.PublishedAt = Probes.Now;
        s.Sequence = ++sequence;
        s.AchievedSpeed = achieved;
        s.SimAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        s.Error = error;
        snapshots.Publish();
        world.Probes.Add(publishProbe, t);
    }
}
