using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The sim's workers (docs/battle/workers.md W1): a dispatch fills the same slots and makes the same chunked sums
/// on any number of workers, the shuffled mode catches a job that depends on its chunks' order, a job's exception
/// reaches the dispatcher, nothing allocates after a job type's first dispatch, and a world of ships steps to the same
/// state on any count (W2).</summary>
public class WorkersTests
{
    /// <summary>Each item's slot from the item alone; each chunk's sum of floats that don't add associatively.</summary>
    struct Fill(double[] slots, double[] parts, int[] by, int chunk) : IJob
    {
        public readonly void Run(int from, int to, int worker)
        {
            double sum = 0;
            for (int i = from; i < to; i++)
            {
                slots[i] = Math.Sin(i * 0.37) * 1e3 + i;
                sum += slots[i] * 1e-3 + 1e10 * (i % 3 - 1);
                by[i] = worker;
            }
            parts[from / chunk] = sum;
        }
    }

    static (double[] Slots, double Sum, int[] By) Dispatch(Workers w, int n, int chunk)
    {
        var slots = new double[n];
        var parts = new double[(n + chunk - 1) / chunk];
        var by = new int[n];
        w.For(new Fill(slots, parts, by, chunk), n, chunk);
        double sum = 0;
        foreach (double p in parts)
            sum += p;   // the chunks' parts in chunk order
        return (slots, sum, by);
    }

    [Fact]
    public void A_dispatch_is_the_same_on_any_number_of_workers()
    {
        using var inline = new Workers(0);
        var (slots0, sum0, _) = Dispatch(inline, 1000, 32);
        foreach (int n in new[] { 1, 3, 8 })
        {
            using var w = new Workers(n);
            for (int r = 0; r < 50; r++)
            {
                var (slots, sum, by) = Dispatch(w, 1000, 32);
                Assert.Equal(slots0, slots);
                Assert.Equal(BitConverter.DoubleToInt64Bits(sum0), BitConverter.DoubleToInt64Bits(sum));
                Assert.All(by, b => Assert.InRange(b, 0, n));
            }
        }
        using var shuffled = new Workers(0, shuffleSeed: 11);
        var (s1, sum1, _) = Dispatch(shuffled, 1000, 32);
        Assert.Equal(slots0, s1);
        Assert.Equal(BitConverter.DoubleToInt64Bits(sum0), BitConverter.DoubleToInt64Bits(sum1));
    }

    /// <summary>A job that carries a running value from chunk to chunk: wrong on workers, and the shuffled mode shows
    /// it every time.</summary>
    struct Running(double[] slots, double[] carry) : IJob
    {
        public readonly void Run(int from, int to, int worker)
        {
            for (int i = from; i < to; i++)
                slots[i] = carry[0] = carry[0] * 0.5 + i;
        }
    }

    [Fact]
    public void The_shuffled_mode_catches_a_job_that_depends_on_its_chunks_order()
    {
        double[] Do(Workers w)
        {
            var slots = new double[256];
            w.For(new Running(slots, new double[1]), slots.Length, 16);
            return slots;
        }
        using var inline = new Workers(0);
        using var shuffled = new Workers(0, shuffleSeed: 5);
        Assert.NotEqual(Do(inline), Do(shuffled));
    }

    struct Throws(int at) : IJob
    {
        public readonly void Run(int from, int to, int worker)
        {
            if (at >= from && at < to)
                throw new InvalidOperationException($"item {at}");
        }
    }

    [Fact]
    public void A_jobs_exception_reaches_the_dispatcher_and_the_workers_go_on()
    {
        using var w = new Workers(4);
        var x = Assert.Throws<InvalidOperationException>(() => w.For(new Throws(700), 1000, 16));
        Assert.Equal("item 700", x.Message);
        var (slots, _, _) = Dispatch(w, 1000, 16);
        Assert.Equal(Math.Sin(999 * 0.37) * 1e3 + 999, slots[999]);
    }

    [Fact]
    public void A_dispatch_allocates_nothing_after_the_first()
    {
        using var w = new Workers(3, spinMicroseconds: 0);   // asleep between dispatches: the wake path too
        var slots = new double[500];
        var parts = new double[32];
        var by = new int[500];
        var job = new Fill(slots, parts, by, 16);
        for (int r = 0; r < 20; r++)
            w.For(job, 500, 16);
        // a worker writes its count as it leaves a dispatch, maybe after the dispatch returns: settled before each read
        Thread.Sleep(50);
        long before = GC.GetAllocatedBytesForCurrentThread() + w.AllocatedBytes;
        for (int r = 0; r < 200; r++)
            w.For(job, 500, 16);
        Thread.Sleep(50);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() + w.AllocatedBytes - before);
    }

    [Fact]
    public void Ships_step_to_the_same_world_on_any_number_of_workers()
    {
        // off rails (a world without them) and over the step's threshold, so the fine tick is a dispatch
        var settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { RailsEnabled = false };
        ulong Play(Workers workers, out int fine)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 7, settings) { Workers = workers };
            int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
            for (int k = 0; k < 120; k++)
                w.Apply(ShipSystem.Spawn(c, 56 + 0.05 * (k / 12), 3 + 0.08 * (k % 12), 37 * k % 360, 8 + k % 15));
            for (int e = 1; e <= 120; e += 3)
                w.Apply(ShipSystem.Rudder(e, e % 2 == 0 ? 15 : -20));
            w.AdvanceTo(w.Time + 90_000);
            fine = w.Ships.Store.FineCount;
            return w.ComputeHash();
        }
        using var inline = new Workers(0);
        ulong h0 = Play(inline, out int fine);
        Assert.True(fine >= 64, $"{fine} ships in the fine tier: the step runs inline");
        using var four = new Workers(4);
        Assert.Equal(h0, Play(four, out _));
        using var shuffled = new Workers(0, shuffleSeed: 3);
        Assert.Equal(h0, Play(shuffled, out _));
    }

    [Fact]
    public void Looks_and_the_smoke_are_the_same_on_any_number_of_workers()
    {
        // two lines of twelve in sight of each other making smoke: the looks (W3), the smoke's queries and its index (W4)
        // go wide; the pictures and the smoke cache are in the hash
        var settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = true };
        ulong Play(Workers workers, out int contacts, out long asked)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 5, settings) { Workers = workers };
            int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
            w.Apply(Command.SetWeather(25_000));
            for (int k = 0; k < 12; k++)
            {
                w.Apply(ShipSystem.Spawn(c, 56.5 + 0.006 * k, 4.0, 0, 18, side: 1));
                w.Apply(ShipSystem.Spawn(c, 56.5 + 0.006 * k, 4.09, 180, 18, side: 2));
            }
            for (int e = 1; e <= 24; e += 2)
                w.Apply(SmokeSystem.MakeSmoke(e, true));
            w.AdvanceTo(w.Time + 240_000);
            contacts = w.Sightings.Of(1)?.Count ?? 0;
            asked = w.Smoke.Asked;
            return w.ComputeHash();
        }
        using var inline = new Workers(0);
        ulong h0 = Play(inline, out int contacts, out long asked);
        Assert.True(contacts >= 10, $"side 1 holds {contacts} contacts");
        Assert.True(asked > 0, "no smoke queries");
        using var four = new Workers(4);
        Assert.Equal(h0, Play(four, out _, out _));
        using var shuffled = new Workers(0, shuffleSeed: 9);
        Assert.Equal(h0, Play(shuffled, out _, out _));
    }

    [Fact]
    public void Gunnery_is_the_same_on_any_number_of_workers()
    {
        // two lines of ten at 11 km, each ship's every battery on the ship opposite (W6: the batteries' seconds go wide,
        // their salvos scheduled after in battery order); smoke on, so two batteries of a ship share a pair
        var settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = true };
        ulong Play(Workers workers, out int hits)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, settings) { Workers = workers };
            var cls = Fleet.Get("dreadnought").Class!;
            int c = w.Ships.AddClass(cls);
            w.Apply(Command.SetWeather(25_000));
            for (int k = 0; k < 10; k++)
            {
                w.Apply(ShipSystem.Spawn(c, 56.5 + 0.007 * k, 4.0, 0, 15, side: 1));
                w.Apply(ShipSystem.Spawn(c, 56.5 + 0.007 * k, 4.18, 0, 15, side: 2));
            }
            w.AdvanceTo(w.Time + 5_000);
            for (int k = 0; k < 10; k++)
                for (int b = 0; b < cls.Batteries.Length; b++)
                {
                    w.Apply(GunnerySystem.Engage(2 * k + 1, b, 2 * k + 2));
                    w.Apply(GunnerySystem.Engage(2 * k + 2, b, 2 * k + 1));
                }
            w.AdvanceTo(w.Time + 300_000);
            hits = 0;
            for (int i = 0; i < w.CombatLog.Count; i++)
                hits += w.CombatLog.Newest(i).Kind == CombatEvent.Hit ? 1 : 0;
            return w.ComputeHash();
        }
        using var inline = new Workers(0);
        ulong h0 = Play(inline, out int hits);
        Assert.True(hits > 0, "no hits: the guns didn't fight");
        using var four = new Workers(4);
        Assert.Equal(h0, Play(four, out _));
        using var shuffled = new Workers(0, shuffleSeed: 2);
        Assert.Equal(h0, Play(shuffled, out _));
    }
}
