namespace Fleetwright.Sim.Core;

/// <summary>A seeded stream of random numbers: xoshiro256** (Blackman and Vigna), our own so its sequence is pinned
/// (System.Random's seeded algorithm is legacy and not ours). A struct: no allocation, and its state is a few ulongs, so
/// it saves. Every random draw in the sim comes from a stream named for its system (docs/world/architecture.md);
/// <see cref="Derive"/> gives a substream per key (a ship, a shot) that depends only on the stream's seed and the key,
/// never on how far the stream has run or which thread asks, which is what keeps parallel work independent of the
/// thread count.</summary>
public struct RandomStream
{
    ulong s0, s1, s2, s3;

    /// <summary>The seed this stream started from; substreams derive from it.</summary>
    public ulong Seed { get; private set; }

    /// <summary>The stream named <paramref name="name"/> in a world seeded <paramref name="worldSeed"/>.</summary>
    public static RandomStream Named(ulong worldSeed, string name) => FromSeed(Mix(worldSeed, StableHash(name)));

    public static RandomStream FromSeed(ulong seed)
    {
        var r = new RandomStream { Seed = seed };
        ulong x = seed;
        r.s0 = SplitMix(ref x);
        r.s1 = SplitMix(ref x);
        r.s2 = SplitMix(ref x);
        r.s3 = SplitMix(ref x);
        return r;
    }

    /// <summary>A stream from raw state, for the reference vectors and loading saves.</summary>
    internal static RandomStream FromState(ulong a, ulong b, ulong c, ulong d) => new() { s0 = a, s1 = b, s2 = c, s3 = d };

    /// <summary>The substream for a key: the same for the same seed and key, whatever this stream has drawn.</summary>
    public readonly RandomStream Derive(ulong key) => FromSeed(Mix(Seed, key));

    public ulong NextULong()
    {
        ulong result = BitRotate(s1 * 5, 7) * 9, t = s1 << 17;
        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;
        s2 ^= t;
        s3 = BitRotate(s3, 45);
        return result;
    }

    /// <summary>Uniform in [0, 1), from the top 53 bits.</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform in [min, max).</summary>
    public double NextDouble(double min, double max) => min + (max - min) * NextDouble();

    /// <summary>Uniform in [0, n), unbiased (Lemire's multiply and reject).</summary>
    public int NextInt(int n)
    {
        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n));
        ulong m = (ulong)(uint)NextULong() * (uint)n;
        if ((uint)m < (uint)n)
        {
            uint floor = (uint)(-n) % (uint)n;
            while ((uint)m < floor)
                m = (ulong)(uint)NextULong() * (uint)n;
        }
        return (int)(m >> 32);
    }

    /// <summary>Mixes the stream's state into a hash (the world's state hash).</summary>
    public readonly void Hash(ref StateHash h)
    {
        h.Add(s0);
        h.Add(s1);
        h.Add(s2);
        h.Add(s3);
        h.Add(Seed);
    }

    /// <summary>FNV-1a over the name's UTF-16 code units: stable across processes and runs, unlike
    /// string.GetHashCode.</summary>
    public static ulong StableHash(string name)
    {
        ulong h = 14695981039346656037;
        foreach (char c in name)
        {
            h ^= c;
            h *= 1099511628211;
        }
        return h;
    }

    /// <summary>Two values into one well-mixed seed (SplitMix64's finaliser over both).</summary>
    public static ulong Mix(ulong a, ulong b)
    {
        ulong x = a ^ Finalise(b + 0x9E3779B97F4A7C15);
        return Finalise(x);
    }

    static ulong SplitMix(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15;
        return Finalise(x);
    }

    static ulong Finalise(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }

    static ulong BitRotate(ulong x, int k) => (x << k) | (x >> (64 - k));
}
