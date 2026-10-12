using System.Diagnostics;

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
    volatile bool stop;
    double pendingReal;
    long sequence;
    string? error;

    // the achieved speed, measured over windows of wall time
    readonly Stopwatch wall = new();
    double windowStart, achieved;
    long windowGameMs;

    public SimHost(World world)
    {
        this.world = world;
        commands = new CommandQueue(world.Settings.CommandCapacity);
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
                wake.WaitOne(100);
                Drain();
                double lastPublish = wall.Elapsed.TotalMilliseconds;
                while (!stop && !world.Advance(world.Settings.EventsPerSlice))
                {
                    if (wall.Elapsed.TotalMilliseconds - lastPublish >= publishMs)
                    {
                        Publish();
                        Drain();
                        lastPublish = wall.Elapsed.TotalMilliseconds;
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
        var s = snapshots.Back;
        world.Fill(s);
        s.Sequence = ++sequence;
        s.AchievedSpeed = achieved;
        s.SimAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        s.Error = error;
        snapshots.Publish();
    }
}
