using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Fires;

/// <summary>Continuous smoke sources (docs/visuals/hits-and-fires.md I5, I6; research/vfx/unified-smoke-system.md: puffs
/// as the one smoke state, trails from moving sources, merged by age): each source lays puffs on the gun smoke's book on a
/// lattice of world times, every so often, each carrying what it made over its spacing (its extinction area and water fog
/// at their rates), so a plume's strength never depends on the spacing. A frame that spans several spacings (a fast
/// clock) lays all of them, each where the source was then, back along its track: the trail is the same whatever the
/// frame rate or the time compression (the user, 2026-10-21). The puffs age on the world's clock (<see
/// cref="SmokeBook.Puff.World"/>). Left in the air where it was laid, a puff drifts with the wind while its source steams
/// on: the trail. The book merges a source's settled puffs by age (one key a source). A source not seen for a while is
/// forgotten. Allocates only for a new source.
/// </summary>
public sealed class SmokeTrails
{
    struct Source
    {
        public double Last;
        public long Frame;
    }

    readonly Dictionary<long, Source> sources = [];
    readonly List<long> stale = new(64);
    long frame;

    /// <summary>Sources this frame laid at least one puff into the book; and how many it knows.</summary>
    public int Laid { get; private set; }
    public int Count => sources.Count;

    public void Clear() => sources.Clear();

    /// <summary>A moving source's spacing, s: a puff every <paramref name="spacingM"/> m it runs (so a trail behind a ship at
    /// speed reads as one plume, not a string of beads), never more often than every half second nor less than
    /// <paramref name="every"/>.</summary>
    public static double Spacing(double speedMs, double every, double spacingM = 12) =>
        Math.Clamp(spacingM / Math.Max(Math.Abs(speedMs), 0.1), Math.Min(0.5, every), every);

    /// <summary>A new frame; every so often the sources not seen for a while are forgotten.</summary>
    public void Begin()
    {
        frame++;
        Laid = 0;
        if (frame % 120 != 0)
            return;
        stale.Clear();
        foreach (var (k, s) in sources)
            if (frame - s.Frame > 600)
                stale.Add(k);
        foreach (long k in stale)
            sources.Remove(k);
    }

    /// <summary>The most puffs a source lays in one frame (its newest), filling in back along its track.</summary>
    public const int MaxBackfill = 48;

    /// <summary>A source this frame, <paramref name="now"/> world time, s: the puffs on its lattice since the last, with its
    /// rates (m²/s of particles' extinction area, of water fog) times the spacing; each laid where it was then, from
    /// <paramref name="origin"/> (Earth-centred, at the height <paramref name="z"/> over the sea) back along
    /// <paramref name="velocity"/> (m/s), rising <paramref name="rise"/> m over <paramref name="riseS"/> s (its buoyancy),
    /// its core <paramref name="lambda"/> m, its particles fading over <paramref name="fadeS"/> s (0: they only spread).
    /// What it made before its newest <see cref="MaxBackfill"/> puffs (or before a fading puff would be gone) is past
    /// drawing. Over its share of the book (<paramref name="spare"/>) its spacing is three times as long, each puff
    /// carrying all it made: its plume thins, never breaks. False when it was its turn but it waited for that.</summary>
    public bool Emit(SmokeBook book, long key, int owner, double now, double every, Vec3d origin, Vec3d velocity, double z, double aRate,
        double fogRate, double fogS, Vector3 tint, double lambda, double rise, double riseS, float seed, double spare = 1, double fadeS = 0)
    {
        bool tight = book.Count >= spare * SmokeBook.Capacity;
        double step = tight ? 3 * every : every;
        if (!sources.TryGetValue(key, out var s))
            s = new Source { Last = (Math.Floor(now / step + 1e-9) - 1) * step };   // on the world's lattice from the start
        s.Frame = frame;
        // the lattice's points to now, a hair's tolerance so rounding never moves a puff to another frame
        int n = (int)Math.Floor((now - s.Last) / step + 1e-9);
        if (n < 1)
        {
            sources[key] = s;
            return !tight;
        }
        int keep = Math.Min(n, MaxBackfill);
        if (fadeS > 0)
            keep = Math.Min(keep, (int)Math.Ceiling(fadeS / step) + 1);
        double first = s.Last + (n - keep + 1) * step;
        for (int j = 0; j < keep; j++)
        {
            double t = first + j * step;
            book.Add(new SmokeBook.Puff
            {
                Born = t, Origin = origin - velocity * (now - t), Z0 = (float)z, JetZ = (float)rise, JetS = (float)Math.Max(riseS, 0.1),
                Anchor = (float)z, Lambda = (float)lambda, Ap = (float)(aRate * step), Aw0 = (float)(fogRate * step),
                FogS = (float)Math.Max(fogS, 0.1), RampS = 0.5f, FadeS = (float)Math.Max(fadeS, 0),
                Tint = tint, Seed = seed, Shooter = owner, Keep = true, World = true,
            });
        }
        s.Last += n * step;
        sources[key] = s;
        Laid++;
        return true;
    }
}
