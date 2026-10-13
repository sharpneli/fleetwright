using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>How two sides stand (docs/battle/plan.md step 3.1a).</summary>
public enum Relation : byte
{
    War,
    Peace,
}

/// <summary>The war/peace table between sides (docs/battle/plan.md step 3.1a; docs/world/architecture.md "Relations gate
/// the contact scan"): world state, set by a scenario and changed by a logged command
/// (<see cref="Command.SetRelation"/>). Two different sides are at war unless set at peace; a side is never hostile to
/// itself. Everything that asks whether two ships oppose each other (the contact scan, the fine tick's check, the
/// rails' gate) asks <see cref="Hostile"/>.</summary>
public sealed class Relations
{
    readonly bool[] peace = new bool[256 * 256];
    int pairsAtPeace;

    /// <summary>Counts the changes: a reader caching anything derived from the table compares it.</summary>
    public int Version { get; private set; }

    public bool Hostile(byte a, byte b) => a != b && !peace[(a << 8) | b];

    public Relation Of(byte a, byte b) => a == b || peace[(a << 8) | b] ? Relation.Peace : Relation.War;

    /// <summary>Sets how two sides stand, both ways; a side with itself is left alone. True if it changed.</summary>
    internal bool Set(byte a, byte b, Relation r)
    {
        if (a == b || Of(a, b) == r)
            return false;
        bool p = r == Relation.Peace;
        peace[(a << 8) | b] = peace[(b << 8) | a] = p;
        pairsAtPeace += p ? 1 : -1;
        Version++;
        return true;
    }

    /// <summary>Nothing for the default table (all at war), so a world without peace hashes as it did before relations.</summary>
    public void Hash(ref StateHash h)
    {
        if (pairsAtPeace == 0)
            return;
        h.Add(pairsAtPeace);
        for (int i = 0; i < peace.Length; i++)
            if (peace[i] && (i >> 8) < (i & 255))
                h.Add(i);
    }
}
