using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>What kind of work an event is: at equal times, events run in this order (docs/world/architecture.md,
/// "Four tiers, one clock"). <see cref="Ships"/> is the ships' fixed tick: every ship at sea, fully simulated.</summary>
public enum EventTier : byte
{
    World,
    Rails,
    Coarse,
    Ships,
}

/// <summary>Something that happens at a moment: plain data, so it saves, and no delegate, so it doesn't allocate.
/// The scheduler runs events in (time, tier, entity, seq) order; seq is the scheduler's own count, the last tiebreak
/// for two events of one entity at one moment. <see cref="System"/> and <see cref="Code"/> say who handles it and
/// what it means to them. <see cref="Generation"/> cancels: an entity that re-anchors bumps its generation, and its
/// system skips events still carrying an older one.</summary>
public readonly record struct SimEvent(
    WorldTime Time, EventTier Tier, int Entity, long Seq, byte System, ushort Code, int Generation, long A)
{
    /// <summary>The run order: time, tier, entity, then the order they were scheduled in.</summary>
    public static int Compare(in SimEvent a, in SimEvent b)
    {
        int c = a.Time.Ms.CompareTo(b.Time.Ms);
        if (c != 0)
            return c;
        c = a.Tier.CompareTo(b.Tier);
        if (c != 0)
            return c;
        c = a.Entity.CompareTo(b.Entity);
        return c != 0 ? c : a.Seq.CompareTo(b.Seq);
    }
}
