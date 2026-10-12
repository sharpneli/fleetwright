using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The clock tier an event belongs to: at equal times, events run in this order (docs/world/architecture.md,
/// "Four clock tiers, one clock"). Clock tiers are how the sim advances things; they have nothing to do with the
/// map's zoom bands ("Terms" there). <see cref="Fine"/> is the fixed fine tick with every model (it was called
/// Tactical, which now means only the zoom band).</summary>
public enum EventTier : byte
{
    World,
    Rails,
    Coarse,
    Fine,
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
        c = ((byte)a.Tier).CompareTo((byte)b.Tier);   // as bytes: an enum's CompareTo takes an object and boxes
        if (c != 0)
            return c;
        c = a.Entity.CompareTo(b.Entity);
        return c != 0 ? c : a.Seq.CompareTo(b.Seq);
    }
}
