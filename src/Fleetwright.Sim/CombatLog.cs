using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>What a combat log line records (damage's kinds join in 4.3a).</summary>
public enum CombatEvent : byte
{
    /// <summary>A battery fired a salvo: Count guns.</summary>
    Fired,
    /// <summary>A shell hit a ship (Target): its shell type (the shooter's class's), what it hit comes with damage.</summary>
    Hit,
    /// <summary>A salvo's fall straddled its target (Count shells), as it truly fell.</summary>
    Straddle,
}

/// <summary>A line of the combat log: when, what, who fired (an entity) with which battery, at whom, and a count or a
/// shell type.</summary>
public readonly record struct CombatEntry(long Ms, CombatEvent Kind, int Shooter, int Battery, int Target, int Count);

/// <summary>The world's record of the fighting (docs/world/architecture.md: "who fired first and what each hit did";
/// docs/battle/fighting.md 4.2b): a ring of plain lines, the newest kept. Encounters, missions and the damage report
/// read it later. World state: hashed once anything is written.</summary>
public sealed class CombatLog(int capacity)
{
    readonly CombatEntry[] ring = new CombatEntry[Math.Max(capacity, 16)];
    int next;

    /// <summary>Lines written since the world began (the ring keeps the newest).</summary>
    public long Written { get; private set; }

    public int Count => (int)Math.Min(Written, ring.Length);

    /// <summary>The i-th newest line (0 the newest).</summary>
    public ref readonly CombatEntry Newest(int i) => ref ring[(next - 1 - i + 2 * ring.Length) % ring.Length];

    public void Add(in CombatEntry e)
    {
        ring[next] = e;
        next = (next + 1) % ring.Length;
        Written++;
    }

    public void Hash(ref StateHash h)
    {
        if (Written == 0)
            return;
        h.Add(Written);
        for (int i = 0; i < Count; i++)
        {
            ref readonly var e = ref Newest(i);
            h.Add(e.Ms);
            h.Add((int)e.Kind);
            h.Add(e.Shooter);
            h.Add(e.Target);
            h.Add(e.Count);
        }
    }

    /// <summary>The newest lines into a snapshot's part (up to its room); allocates only to grow it.</summary>
    public void Fill(CombatLogView v, int most)
    {
        int n = Math.Min(Count, most);
        v.EnsureCapacity(n);
        for (int i = 0; i < n; i++)
            v.Entries[i] = Newest(i);
        v.Count = n;
        v.Written = Written;
    }
}
