using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The world headless: the event order, where the clock may rest (any slicing gives the same world), the
/// speed as the world's own state, drops at an event, the snapshot, and no garbage in a step.</summary>
public class WorldTests
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14, 0);

    [Fact]
    public void Scheduler_runs_events_in_time_tier_entity_seq_order()
    {
        var s = new Scheduler(16);   // small, so it grows
        var rng = new Random(3);
        var pushed = new List<SimEvent>();
        for (int k = 0; k < 5000; k++)
        {
            var t = new WorldTime(rng.Next(50));
            var tier = (EventTier)rng.Next(4);
            int entity = rng.Next(5);
            s.Push(t, tier, entity, 1, 0, 0, k);
            pushed.Add(new SimEvent(t, tier, entity, k, 1, 0, 0, k));
        }
        pushed.Sort((a, b) => SimEvent.Compare(a, b));
        foreach (var want in pushed)
            Assert.Equal(want, s.Pop());
        Assert.Equal(0, s.Count);
    }

    static (World World, Metronome M) Make(ulong seed = 11)
    {
        var w = new World(Start, seed);
        return (w, new Metronome(w, 50));
    }

    [Fact]
    public void Any_slicing_of_a_span_gives_the_same_world()
    {
        var end = Start + 2 * 3600 * 1000L;
        var (whole, _) = Make();
        whole.AdvanceTo(end);

        var rng = new Random(5);
        for (int trial = 0; trial < 5; trial++)
        {
            var (w, _) = Make();
            while (w.Time < end)
            {
                long step = rng.Next(4) == 0 ? rng.Next(5) : rng.Next(200_000);
                w.SetTarget(new WorldTime(Math.Min(end.Ms, w.Time.Ms + step)));
                // run it in small bites, sometimes leaving early with the target not yet reached
                while (!w.Advance(rng.Next(1, 40)) && rng.Next(8) != 0)
                {
                }
            }
            Assert.Equal(end, w.Time);
            Assert.Equal(whole.ComputeHash(), w.ComputeHash());
        }
        Assert.True(whole.PendingEvents > 0);
    }

    [Fact]
    public void Commands_where_a_sliced_run_rests_replay_exactly()
    {
        // a run cut into bites lands commands wherever it happens to rest, mid-run of a moment's events too if the
        // clock rested there wrongly; the replay lands each at its stamp in one go
        var end = Start + 3600 * 1000L;
        var rng = new Random(9);
        for (int trial = 0; trial < 5; trial++)
        {
            var (w, m) = Make();
            while (w.Time < end)
            {
                w.SetTarget(new WorldTime(Math.Min(end.Ms, w.Time.Ms + rng.Next(100_000))));
                while (!w.Advance(rng.Next(1, 6)))
                {
                    if (rng.Next(3) == 0)
                        w.Apply(rng.Next(6) == 0
                            ? new Command(m.Id, Metronome.SetPeriod, A: rng.Next(1, 80))
                            : new Command(m.Id, Metronome.Nudge, X: rng.NextDouble()));
                }
            }
            Assert.True(w.CommandLog.Count > 100);
            Assert.Equal(Replay(w, end), w.ComputeHash());
        }
    }

    /// <summary>A fresh world with the same systems, the log applied at its stamps, run to the end: its hash.</summary>
    static ulong Replay(World live, WorldTime end)
    {
        var (w, _) = Make(live.Seed);
        foreach (var c in live.CommandLog)
        {
            w.AdvanceTo(c.At);
            w.Apply(c.Command);
        }
        w.AdvanceTo(end);
        return w.ComputeHash();
    }

    [Fact]
    public void A_different_seed_gives_a_different_world()
    {
        var (a, _) = Make(1);
        var (b, _) = Make(2);
        a.AdvanceTo(Start + 60_000);
        b.AdvanceTo(Start + 60_000);
        Assert.NotEqual(a.ComputeHash(), b.ComputeHash());
    }

    [Fact]
    public void A_command_takes_effect_where_the_clock_rests_and_cancels_by_generation()
    {
        var (w, m) = Make();
        w.AdvanceTo(Start + 1025);
        Assert.Equal(20, m.Ticks);   // 50, 100, ... 1000
        w.Apply(new Command(m.Id, Metronome.SetPeriod, A: 1000));
        w.AdvanceTo(Start + 3000);
        Assert.Equal(21, m.Ticks);   // the old tick at 1050 is skipped; then 2025, and 3025 is still ahead
        Assert.Equal(Start + 1025, w.CommandLog[0].At);
    }

    [Fact]
    public void Elapse_runs_at_the_asked_speed_capped_ahead_of_the_clock()
    {
        var w = new World(Start, 1);
        w.Elapse(0.1);
        Assert.Equal(Start + 100, w.Target);   // 1x by default
        w.Advance();
        w.Apply(Command.SetSpeed(1000));
        w.Elapse(0.1);
        Assert.Equal(Start + 100 + 100_000, w.Target);
        w.Elapse(10);   // a long stall: the target runs at most 0.25 s of real time ahead
        Assert.Equal(Start + 100 + 250_000, w.Target);
        w.Apply(Command.SetSpeed(1));   // slowing down pulls the target in at once
        Assert.Equal(Start + 100 + 250, w.Target);
        w.Apply(Command.SetSpeed(1e9));
        Assert.Equal(w.Settings.MaxSpeed, w.Speed);
        w.Apply(Command.SetSpeed(0));
        Assert.Equal(w.Settings.MinSpeed, w.Speed);
    }

    [Fact]
    public void Pause_stops_where_the_clock_is()
    {
        var w = new World(Start, 1);
        w.Apply(Command.SetSpeed(100));
        w.Elapse(0.05);
        w.Apply(Command.Pause());
        Assert.Equal(Start, w.Target);
        w.Elapse(5);
        w.Advance();
        Assert.Equal(Start, w.Time);
        w.Apply(Command.Resume());
        w.Elapse(0.01);
        w.Advance();
        Assert.Equal(Start + 1000, w.Time);
    }

    [Fact]
    public void A_halt_at_an_event_rests_the_clock_just_after_it()
    {
        var w = new World(Start, 1);
        var m = new Metronome(w, 50);
        var at = Start + 10_000;
        new Halter(w, at, pause: true);
        w.Apply(Command.SetSpeed(1000));
        w.Elapse(0.25);
        w.Advance();
        Assert.True(w.Paused);
        Assert.Equal(at + 1, w.Time);
        Assert.Equal(200, m.Ticks);   // the tick at the halt's own moment ran too
        w.Elapse(1);
        w.Advance();
        Assert.Equal(at + 1, w.Time);
    }

    [Fact]
    public void A_halt_can_drop_the_speed_without_pausing()
    {
        var w = new World(Start, 1);
        new Halter(w, Start + 5000, pause: false, speed: 1);
        w.Apply(Command.SetSpeed(30_000));
        w.Elapse(0.016);
        w.Advance();
        Assert.Equal(Start + 5001, w.Time);
        Assert.Equal(1, w.Speed);
        Assert.False(w.Paused);
        w.Elapse(0.016);
        w.Advance();
        Assert.Equal(Start + 5017, w.Time);
    }

    [Fact]
    public void The_snapshot_carries_the_clock_and_the_sun()
    {
        var w = new World(Start, 1);
        w.AdvanceTo(Start + 3_600_000);
        var s = new Snapshot();
        w.Fill(s);
        Assert.Equal(Start + 3_600_000, s.Time);
        Assert.Equal(Sun.Direction(s.Time), s.Sun);
        Assert.Equal(1, s.Speed);
    }

    [Fact]
    public void Events_and_commands_are_checked()
    {
        var (w, m) = Make();
        w.AdvanceTo(Start + 100);
        Assert.Throws<ArgumentException>(() => w.Schedule(Start, EventTier.Rails, 0, m.Id, 0));
        Assert.Throws<ArgumentException>(() => w.Schedule(Start + 200, EventTier.Rails, 0, 9, 0));
        Assert.Throws<ArgumentException>(() => w.SetTarget(Start));
        Assert.Throws<ArgumentException>(() => w.Apply(new Command(0, 99)));
    }

    [Fact]
    public void A_step_allocates_nothing()
    {
        var (w, m) = Make();
        var s = new Snapshot();
        w.Apply(Command.SetSpeed(30_000));
        for (int k = 0; k < 100; k++)
        {
            w.Elapse(1 / 60.0);
            w.Advance();
            w.Fill(s);
        }
        w.ComputeHash();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 300; k++)   // 300 frames at 30,000x: 500 game seconds and 10,000 ticks each
        {
            w.Elapse(1 / 60.0);
            w.Advance(100_000);
            w.Fill(s);
        }
        w.ComputeHash();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(m.Ticks > 100_000, $"{m.Ticks} ticks");
    }
}
