using Fleetwright.Sim.Combat;

namespace Fleetwright.Sim.Ai.Commanders;

/// <summary>What a side wants (docs/battle/command.md "The side's commander, its aims and its home"): a small typed set
/// that the missions' objectives share, so whether one is met is checked in one place (<see cref="Aims"/>). v0 has one
/// (decision 8); Reach, Protect, Hold and Avoid come when a scenario needs them.</summary>
public enum AimKind : byte
{
    /// <summary>Sink every ship of every side at war with this one.</summary>
    DestroyEveryone,
}

public readonly record struct Aim(AimKind Kind);

/// <summary>Whether a side's aim is met, as the world truly stands: the judge's question (the scenario's end,
/// docs/battle/command.md "The end"), and the side's commander's once he has one (5.1d), asked of his picture.</summary>
public static class Aims
{
    public static bool Met(World w, int side, Aim aim) => aim.Kind switch
    {
        AimKind.DestroyEveryone => !EnemyAfloat(w, side),
        _ => throw new ArgumentOutOfRangeException(nameof(aim)),
    };

    /// <summary>Whether a ship is still in the fight: at sea, not going down, and not given up (her colours down or her
    /// men off her: docs/battle/striking.md SK5). A ship going down counts as lost from the moment it starts (the combat
    /// log's <see cref="CombatEvent.Lost"/>), not when it's gone from the sea; one given up from her people's call.</summary>
    public static bool Afloat(ShipStore store, int slot) =>
        store.IsAlive(slot) && store.Combat.Condition(slot) is not ({ Status: not ShipStatus.Afloat } or { IsGivenUp: true });

    /// <summary>Whether any ship of a side at war with this one is afloat.</summary>
    public static bool EnemyAfloat(World w, int side)
    {
        var store = w.Ships.Store;
        for (int slot = 0; slot < store.Slots; slot++)
            if (Afloat(store, slot) && w.Relations.Hostile((byte)side, store[slot].Side))
                return true;
        return false;
    }
}
