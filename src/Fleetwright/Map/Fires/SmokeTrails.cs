using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Fires;

/// <summary>Continuous smoke sources (docs/visuals/hits-and-fires.md I5, I6; research/vfx/unified-smoke-system.md: puffs
/// as the one smoke state, trails from moving sources, merged by age): each source lays a puff on the gun smoke's book
/// every so often on the sea's clock, carrying what it made since (its extinction area and water fog at their rates), so
/// a plume's strength never depends on the spacing. Left in the air where it was laid, it drifts with the wind while its
/// source steams on: the trail. The book merges a source's settled puffs by age (one key a source). A source not seen for
/// a while is forgotten; one seen again after a gap lays at most a few spacings' worth (no dump after a pause or a pan).
/// Allocates only for a new source.</summary>
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

    /// <summary>A source this frame, <paramref name="now"/> on the sea's clock: a puff into the book when its spacing has
    /// passed, with its rates (m²/s of particles' extinction area, of water fog) times the time since its last; its start
    /// at <paramref name="origin"/> (Earth-centred, at the height <paramref name="z"/> over the sea), rising
    /// <paramref name="rise"/> m over <paramref name="riseS"/> s (its buoyancy), its core <paramref name="lambda"/> m.
    /// False when it was its turn but the book had no room to spare (<paramref name="spare"/>: the share of the book it
    /// may fill to).</summary>
    public bool Emit(SmokeBook book, long key, int owner, double now, double every, Vec3d origin, double z, double aRate, double fogRate,
        double fogS, Vector3 tint, double lambda, double rise, double riseS, float seed, double spare = 1)
    {
        if (!sources.TryGetValue(key, out var s))
            s = new Source { Last = now - every };
        s.Frame = frame;
        double dt = now - s.Last;
        if (dt < every)
        {
            sources[key] = s;
            return true;
        }
        dt = Math.Min(dt, 3 * every);
        s.Last = now;
        sources[key] = s;
        if (book.Count >= spare * SmokeBook.Capacity)
            return false;
        book.Add(new SmokeBook.Puff
        {
            Born = now, Origin = origin, Z0 = (float)z, JetZ = (float)rise, JetS = (float)Math.Max(riseS, 0.1), Anchor = (float)z,
            Lambda = (float)lambda, Ap = (float)(aRate * dt), Aw0 = (float)(fogRate * dt), FogS = (float)Math.Max(fogS, 0.1), RampS = 0.5f,
            Tint = tint, Seed = seed, Shooter = owner, Keep = true,
        });
        Laid++;
        return true;
    }
}
