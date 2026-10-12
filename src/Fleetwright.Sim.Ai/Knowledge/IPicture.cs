using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>What a commander holds about another ship: where it was, which way it was heading, how fast, and when.
/// From a picture it's an estimate with an age (docs/world/architecture.md "Knowledge"); from the truth it's exact.</summary>
public readonly record struct Sighting(int Entity, Vec3d Position, Vec3d Forward, double SpeedMs, long AtMs);

/// <summary>What one side knows of other ships: the only way an AI layer sees anything but its own ships
/// (docs/world/architecture.md "What the ship simulation must already respect": captains act on pictures). A layer
/// asks the picture, never the world, so swapping the truth for a navy's real picture (step 3.2) changes nothing
/// above this.</summary>
public interface IPicture
{
    /// <summary>What's known of a ship now, if anything.</summary>
    bool TryGet(int entity, out Sighting s);
}

/// <summary>EXCEPTION, until pictures (docs/battle/plan.md step 3.2): the true state of every ship. Every use of it is
/// the one exception the plan allows the AI: replace it with each navy's picture when sighting exists.</summary>
public sealed class TruthPicture(ShipStore ships) : IPicture
{
    public bool TryGet(int entity, out Sighting s)
    {
        int slot = ships.SlotOf(entity);
        if (slot < 0)
        {
            s = default;
            return false;
        }
        var x = ships[slot];
        s = new Sighting(entity, x.Position, x.Forward, x.Speed, x.PoseMs);
        return true;
    }
}
