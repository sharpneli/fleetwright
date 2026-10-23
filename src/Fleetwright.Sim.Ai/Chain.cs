using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>Whose the order a unit is on is (docs/battle/plan.md step 2.6, docs/battle/command.md "The player's hand"):
/// what a captain or admiral keeps of who gave it.</summary>
public enum Whose : byte
{
    /// <summary>The unit's own: a scenario's opening orders, a task's or its side's commander's, its captain's own
    /// judgement (fallen out), or nobody's (left without a superior). Doctrine's standing orders may take it.</summary>
    Own,

    /// <summary>Its superior's, in its line.</summary>
    Superior,

    /// <summary>The player's, by hand: doctrine's standing orders leave it alone.</summary>
    Player,
}

/// <summary>Who an order came from, as a layer reads its command (B: the <see cref="OrderSource"/>, with
/// <see cref="UnitOrders.Hand"/> over it) once the AI system has routed it (a commander's reaches a unit as a task's).</summary>
public readonly record struct OrderFrom(OrderSource Source, bool Hand)
{
    public static OrderFrom Of(in Command c)
    {
        var source = (OrderSource)(c.B & 0xFF);
        return new(source, source == OrderSource.Player && (c.B & UnitOrders.Hand) != 0);
    }

    /// <summary>Whose the unit's orders become.</summary>
    public Whose Whose => Source == OrderSource.Superior ? Whose.Superior : Hand ? Whose.Player : Whose.Own;

    /// <summary>The AI's own order (a task's): the unit's own, but it doesn't hold the unit out of its line as the
    /// player's does.</summary>
    public bool Tasked => Source == OrderSource.Task;
}

/// <summary>The chain's rules for a member's place in its group's line (docs/battle/plan.md step 2.6), the same for a
/// ship in a squadron (her captain's) and a squadron in a force (its admiral's).</summary>
public static class Chain
{
    /// <summary>Where an order leaves a member, or null when it waits: one from its superior while the member is out of
    /// the line waits for the hand-back (<paramref name="ask"/>: a notice, unless it's out on its task); the player's
    /// takes it out of the line (nudged) or from its task; a task's takes it out of the line for the task.</summary>
    public static Detachment? Place(OrderFrom from, Detachment now, out bool ask)
    {
        ask = false;
        switch (from.Source)
        {
            case OrderSource.Superior when now != Detachment.InLine:
                ask = now != Detachment.Tasked;
                return null;
            case OrderSource.Task when now == Detachment.InLine:
                return Detachment.Tasked;
            case OrderSource.Player when now is Detachment.InLine or Detachment.Tasked:
                return Detachment.Nudged;
            default:
                return now;
        }
    }

    /// <summary>Whether an order is the helm's (a course, a speed, a route, holding on, turning together, deploying), which the
    /// conn keeps (<see cref="AiCommand.Conn"/>); every other order to the unit's movement is a general one (a behaviour,
    /// a task) that hands the minute-to-minute back to its captain or admiral.</summary>
    public static bool IsHelmOrder(AiCommand code) => code is AiCommand.Course or AiCommand.Speed or AiCommand.Hold or AiCommand.ClearRoute
        or AiCommand.AddWaypoint or AiCommand.LoopRoute or AiCommand.TurnTogether or AiCommand.Deploy or AiCommand.Conn;

    /// <summary>Where a <see cref="AiCommand.Detach"/> pins a member, or null if it changes nothing: the player's pins
    /// it out until it's resumed; a task's takes it out for the task, only from the line.</summary>
    public static Detachment? Pin(OrderFrom from, Detachment now)
    {
        var pin = from.Tasked ? Detachment.Tasked : Detachment.Pinned;
        return now == pin || from.Tasked && now != Detachment.InLine ? null : pin;
    }
}
