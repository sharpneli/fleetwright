using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>What an encounter opening is to the player (docs/battle/plan.md step 3.1c): one their ships are in (or come
/// into), or one between others.</summary>
public enum InterestKind : byte
{
    Own,
    Others,
}

/// <summary>What the clock does at it: nothing, pause, or slow to a speed (if it's faster).</summary>
public enum InterestAction : byte
{
    Nothing,
    Pause,
    Slow,
}

public readonly record struct InterestRule(InterestAction Action, double Speed = 1);

/// <summary>The player's interest table (docs/world/architecture.md "Contacts": the drop is the sim's, at the exact
/// event, by the player's rules): what the clock does when an encounter opens, by its kind. The player's preference, but
/// world state all the same, set by logged commands (<see cref="Command.SetInterest"/>), so a replay drops the clock
/// where the game did. With no player side (headless) only <see cref="InterestKind.Others"/> can apply.</summary>
public sealed class Interest
{
    readonly InterestRule[] rules = [new(InterestAction.Pause), new(InterestAction.Nothing)];

    public InterestRule this[InterestKind kind] => rules[(int)kind];

    internal void Set(InterestKind kind, InterestRule rule) => rules[(int)kind] = rule;

    /// <summary>The clock dropped by a rule, from an event's handler: paused (and slowed to the rule's speed if it's
    /// faster: 1x by default, so a fight opens on the fine tick, where the guns fire; docs/battle/fighting.md 4.2c), or
    /// slowed if it's faster.</summary>
    internal void Apply(World world, InterestKind kind)
    {
        var r = rules[(int)kind];
        if (r.Action == InterestAction.Pause)
            world.Halt(pause: true, world.Speed > r.Speed ? r.Speed : null);
        else if (r.Action == InterestAction.Slow && world.Speed > r.Speed)
            world.Halt(pause: false, r.Speed);
    }

    public void Hash(ref StateHash h)
    {
        foreach (var r in rules)
        {
            h.Add((int)r.Action);
            h.Add(r.Speed);
        }
    }
}
