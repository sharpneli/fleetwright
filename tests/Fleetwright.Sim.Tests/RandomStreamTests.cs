using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The seeded streams: the reference algorithm, names and seeds that separate streams, substreams that don't
/// depend on the stream's position, uniform draws, and no garbage.</summary>
public class RandomStreamTests
{
    [Fact]
    public void Matches_the_xoshiro256starstar_reference()
    {
        // the published test vector: state {1, 2, 3, 4}
        var r = RandomStream.FromState(1, 2, 3, 4);
        Assert.Equal(11520UL, r.NextULong());
        Assert.Equal(0UL, r.NextULong());
        Assert.Equal(1509978240UL, r.NextULong());
        Assert.Equal(1215971899390074240UL, r.NextULong());
    }

    [Fact]
    public void Same_seed_and_name_give_the_same_stream_and_others_differ()
    {
        var a = RandomStream.Named(42, "sighting");
        var b = RandomStream.Named(42, "sighting");
        var c = RandomStream.Named(42, "gunnery");
        var d = RandomStream.Named(43, "sighting");
        for (int k = 0; k < 100; k++)
        {
            ulong x = a.NextULong();
            Assert.Equal(x, b.NextULong());
            Assert.NotEqual(x, c.NextULong());
            Assert.NotEqual(x, d.NextULong());
        }
        // FNV-1a's published vectors (ASCII names hash as their bytes), so names seed the same streams forever
        Assert.Equal(0xCBF29CE484222325UL, RandomStream.StableHash(""));
        Assert.Equal(0xAF63DC4C8601EC8CUL, RandomStream.StableHash("a"));
        Assert.Equal(0x85944171F73967E8UL, RandomStream.StableHash("foobar"));
    }

    [Fact]
    public void Derive_depends_on_the_seed_and_key_only()
    {
        var a = RandomStream.Named(7, "shells");
        var fresh = a.Derive(1234);
        for (int k = 0; k < 1000; k++)
            a.NextULong();
        var later = a.Derive(1234);
        for (int k = 0; k < 100; k++)
            Assert.Equal(fresh.NextULong(), later.NextULong());
        Assert.NotEqual(a.Derive(1).NextULong(), a.Derive(2).NextULong());
    }

    [Fact]
    public void Draws_are_uniform()
    {
        var r = RandomStream.Named(1, "test");
        const int N = 200_000, Bins = 10;
        var counts = new int[Bins];
        double sum = 0;
        for (int k = 0; k < N; k++)
        {
            double x = r.NextDouble();
            Assert.InRange(x, 0, 1 - 1e-17);
            sum += x;
            counts[r.NextInt(Bins)]++;
        }
        Assert.Equal(0.5, sum / N, 2);
        foreach (int c in counts)
            Assert.InRange(c, N / Bins * 0.97, N / Bins * 1.03);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.NextInt(0));
    }

    [Fact]
    public void Draws_allocate_nothing()
    {
        var r = RandomStream.Named(1, "test");
        double s = r.NextDouble() + r.NextInt(5) + r.Derive(3).NextDouble();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 10000; k++)
            s += r.NextDouble() + r.NextInt(17) + r.Derive((ulong)k).NextDouble();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(s > 0);
    }
}
