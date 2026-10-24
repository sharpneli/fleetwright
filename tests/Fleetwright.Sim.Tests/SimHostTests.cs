using System.Diagnostics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The world on its own thread: a live run with commands arriving at random wall times replays exactly
/// headless; the published clock only goes forward, never passes its target and reaches it; the thread allocates
/// nothing in play; the achieved speed is measured; a crash is reported, not swallowed. Paced by the wall clock, so alone
/// after the rest (<see cref="WallClock"/>): under the suite's load the achieved speed read 637x of 1,000x (2026-10-24).</summary>
[Collection(WallClock.Name)]
public class SimHostTests
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14, 0);

    [Fact]
    public void A_live_run_replays_exactly_headless()
    {
        var world = new World(Start, 77);
        var m = new Metronome(world, 50);
        var rng = new Random(13);
        var violations = new List<string>();
        long seen = 0;
        using (var host = new SimHost(world))
        {
            host.Start();
            host.Send(Command.SetSpeed(500));
            var done = false;
            // the one reader: the clock it sees only goes forward and is never past the target
            var reader = new Thread(() =>
            {
                var last = Start;
                while (!Volatile.Read(ref done))
                {
                    var s = host.Latest();
                    if (s.Sequence == 0)
                        continue;   // nothing published yet: the buffer's blank, at 1850 (the sim thread starts late under load)
                    if (s.Time < last)
                        violations.Add($"went back from {last} to {s.Time}");
                    if (s.Time > s.Target)
                        violations.Add($"{s.Time} is past its target {s.Target}");
                    last = s.Time;
                    seen = s.Sequence;
                }
            });
            reader.Start();
            for (int k = 0; k < 300; k++)
            {
                host.Elapse(rng.NextDouble() * 0.03);
                switch (rng.Next(12))
                {
                    case 0: host.Send(new Command(m.Id, Metronome.SetPeriod, A: rng.Next(10, 200))); break;
                    case 1: host.Send(new Command(m.Id, Metronome.Nudge, X: rng.NextDouble())); break;
                    case 2: host.Send(Command.SetSpeed(rng.Next(1, 3000))); break;
                }
                if (rng.Next(3) == 0)
                    Thread.Sleep(rng.Next(3));
            }
            Volatile.Write(ref done, true);
            reader.Join();
        }
        Assert.True(violations.Count == 0, string.Join("; ", violations.Take(5)));
        Assert.True(seen > 10, $"{seen} snapshots");
        Assert.True(world.CommandLog.Count > 30);
        Assert.True(m.Ticks > 1000, $"{m.Ticks} ticks");

        // the same systems, the log at its stamps, to the same time
        var replay = new World(Start, 77);
        new Metronome(replay, 50);
        foreach (var c in world.CommandLog)
        {
            replay.AdvanceTo(c.At);
            replay.Apply(c.Command);
        }
        replay.AdvanceTo(world.Time);
        Assert.Equal(world.ComputeHash(), replay.ComputeHash());
    }

    [Fact]
    public void The_clock_reaches_the_target_and_stops_there()
    {
        var world = new World(Start, 1);
        new Metronome(world, 50);
        using var host = new SimHost(world);
        host.Start();
        host.Send(Command.SetSpeed(1000));
        host.Elapse(0.1);   // 100 game seconds
        var s = WaitFor(host, s => s.Time == Start + 100_000);
        Assert.Equal(s.Target, s.Time);
        Thread.Sleep(50);
        Assert.Equal(Start + 100_000, WaitFor(host, _ => true).Time);   // and stays there
    }

    [Fact]
    public void The_thread_allocates_nothing_in_play()
    {
        var world = new World(Start, 1);
        new Metronome(world, 50);
        using var host = new SimHost(world);
        host.Start();
        host.Send(Command.SetSpeed(2000));
        Frames(host, 0.3);   // warm up: the JIT, the first snapshots
        long before = WaitFor(host, _ => true).SimAllocatedBytes;
        Frames(host, 0.5);
        var s = WaitFor(host, _ => true);
        Assert.True(s.Time > Start + 1_000_000, $"only reached {s.Time}");
        Assert.Equal(0, s.SimAllocatedBytes - before);
    }

    [Fact]
    public void The_achieved_speed_is_measured()
    {
        var world = new World(Start, 1);
        new Metronome(world, 50);
        using var host = new SimHost(world);
        host.Start();
        host.Send(Command.SetSpeed(1000));
        Frames(host, 1.0);
        var s = WaitFor(host, _ => true);
        Assert.InRange(s.AchievedSpeed, 700, 1300);
        Assert.Equal(1000, s.Speed);
    }

    [Fact]
    public void A_crash_is_reported_and_the_world_stops()
    {
        var world = new World(Start, 1);
        new Thrower(world, Start + 1000);
        using var host = new SimHost(world);
        host.Start();
        host.Send(Command.SetSpeed(100));
        Frames(host, 0.2);   // 20 game seconds asked; the crash at 1 s stops it
        var s = WaitFor(host, s => s.Error != null);
        Assert.Contains("boom", s.Error);
        Assert.Equal(Start + 1000, s.Time);
    }

    /// <summary>Real frames at about 60 fps for a while, each reporting its real time.</summary>
    static void Frames(SimHost host, double seconds)
    {
        var sw = Stopwatch.StartNew();
        double last = 0;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            Thread.Sleep(16);
            double now = sw.Elapsed.TotalSeconds;
            host.Elapse(now - last);
            last = now;
            host.Latest();
        }
    }

    static Snapshot WaitFor(SimHost host, Func<Snapshot, bool> ok)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 5)
        {
            var s = host.Latest();
            if (s.Sequence > 0 && ok(s))
                return s;
            Thread.Sleep(1);
        }
        throw new TimeoutException($"the snapshot never got there: {host.Latest().Time}");
    }
}
