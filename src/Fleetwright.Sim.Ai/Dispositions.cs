namespace Fleetwright.Sim.Ai;

/// <summary>How a commander disposes the units under him about his main body (docs/battle/admirals-orders.md O7, the
/// chain of command): the one rule the side's commander (for his top units) and every force's admiral (for his members)
/// share. A unit attends the body by its role: scouts scout ahead of it, spread off its bow; flotillas and screens screen
/// it. What a commander does with the rest (another heavy unit, an auxiliary) is his own.</summary>
public static class Dispositions
{
    // a scouting line off the body's bow: ahead, then a broad and a fine bow either side
    static readonly double[] ScoutBearings = [0, 45, -45, 90, -90];

    /// <summary>The n-th scout's bearing off the body's bow (n from 0).</summary>
    public static double ScoutBearing(int n) => ScoutBearings[n % ScoutBearings.Length];

    /// <summary>Whether a role attends a body rather than being one.</summary>
    public static bool Attends(UnitRole role) => role is UnitRole.Scout or UnitRole.Flotilla or UnitRole.Screen;

    /// <summary>The task a unit of an attending role gets: scout ahead of the body (the n-th scout on its bearing), or
    /// screen it.</summary>
    public static Command Attend(in UnitOrders o, UnitRole role, Unit unit, Unit body, int scout, OrderSource by) => role switch
    {
        UnitRole.Scout => o.Scout(unit, body, 0, ScoutBearing(scout), by),
        UnitRole.Flotilla or UnitRole.Screen => o.Screen(unit, body, 0, by),
        _ => throw new ArgumentException($"{role} doesn't attend a body"),
    };

    /// <summary>The task kind <see cref="Attend"/> gives a role.</summary>
    public static Tasks.TaskKind KindFor(UnitRole role) => role == UnitRole.Scout ? Tasks.TaskKind.Scout : Tasks.TaskKind.Screen;
}
