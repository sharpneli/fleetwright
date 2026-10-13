using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The world's index of where its ships are (docs/battle/fighting.md, "One index of where things are"): every
/// live ship, on any clock tier, in a <see cref="CubeIndex"/> by where it was when the index was built. Built only when
/// asked, and again when it's older than <see cref="SimSettings.ShipIndexMaxAgeMs"/> or the ships have changed, so a
/// world without fighting never pays for it; a query widens its reach by how far the fastest ship can have gone since.
/// Its first user is the shells' danger window (4.2a); torpedoes and collisions (4.6) come next.</summary>
public sealed class ShipIndex(ShipSystem ships, SimSettings settings)
{
    readonly CubeIndex cubes = new();
    long builtMs = long.MinValue;
    int builtCount = -1, builtNewest = -1;
    double fastest;
    int[] near = new int[16];

    /// <summary>How often it was built (a probe for the benches).</summary>
    public int Builds { get; private set; }

    /// <summary>The live ships within a distance of a point now (the world's time), by slot, in slot order.</summary>
    public ReadOnlySpan<int> Near(Vec3d p, double radius)
    {
        var store = ships.Store;
        long now = store.NowMs;
        double reach = radius + fastest * (now - builtMs) / 1000;
        if (builtMs == long.MinValue || now - builtMs > settings.ShipIndexMaxAgeMs || now < builtMs || store.Count != builtCount
            || ships.NewestEntity != builtNewest || reach > cubes.Cell)
        {
            Build(now, radius);
            reach = radius;
        }
        int found = cubes.Near(p), n = 0;
        if (near.Length < found)
            near = new int[Math.Max(found, near.Length * 2)];
        foreach (int slot in cubes.Found(found))
            if (store.IsAlive(slot) && (store[slot].Position - p).LengthSquared <= radius * radius)
                near[n++] = slot;
        return near.AsSpan(0, n);
    }

    void Build(long now, double radius)
    {
        var store = ships.Store;
        cubes.Clear(store.Slots, Math.Max(settings.ShipIndexCellM, radius + 1));
        fastest = 0;
        for (int i = 0; i < store.Slots; i++)
            if (store.IsAlive(i))
            {
                var s = store[i];
                cubes.Add(i, s.Position);
                fastest = Math.Max(fastest, Math.Abs(s.Speed));
            }
        (builtMs, builtCount, builtNewest) = (now, store.Count, ships.NewestEntity);
        Builds++;
    }
}
