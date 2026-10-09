using System.Text;

namespace Fleetwright.Shipgen.Render;

/// <summary>The drawing's seeded RNG, one per feature ("&lt;id&gt;/clutter/&lt;kit&gt;", "&lt;id&gt;/dazzle", the id for
/// vents) so a refit doesn't reshuffle untouched parts (PORTING.md Decisions). It doesn't match Python's
/// random.Random, and needn't: FNV-1a 64 of the seed text, then SplitMix64. tools/golden.py's PortRandom is the same
/// generator, so the SVG goldens (golden/svg) come out the same on both sides, clutter and dazzle included.</summary>
public sealed class ShipRng
{
    ulong s;

    public ShipRng(string seed)
    {
        ulong h = 0xcbf29ce484222325;
        foreach (byte b in Encoding.UTF8.GetBytes(seed))
            h = (h ^ b) * 0x100000001b3;
        s = h;
    }

    ulong Next()
    {
        s += 0x9E3779B97F4A7C15;
        ulong z = s;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }

    /// <summary>A double in [0, 1).</summary>
    public double Random() => (Next() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>a + (b - a) * random(), as Python's uniform.</summary>
    public double Uniform(double a, double b) => a + (b - a) * Random();

    /// <summary>An int in [0, n).</summary>
    public int RandRange(int n) => (int)(Random() * n);

    public T Choice<T>(IReadOnlyList<T> seq) => seq[(int)(Random() * seq.Count)];
}
