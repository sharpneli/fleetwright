using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>What a commander holds about another ship: where it was, which way it was heading, how fast, and when.
/// From a picture it's an estimate with an age (docs/world/architecture.md "Knowledge"); of his own side's, exact.</summary>
public readonly record struct Sighting(int Entity, Vec3d Position, Vec3d Forward, double SpeedMs, long AtMs);

/// <summary>What a side knows of other ships: the only way an AI layer sees anything but its own ships
/// (docs/world/architecture.md "What the ship simulation must already respect": captains act on pictures). A layer
/// asks the picture, never the world.</summary>
public interface IPicture
{
    /// <summary>What a side knows of a ship now, if anything.</summary>
    bool TryGet(byte side, int entity, out Sighting s);
}

/// <summary>Each navy's picture as its commanders hold it (docs/battle/plan.md step 3.2f): ships of sides not at war
/// with it exactly (its own by its own reports; others' at peace as their movements are reported, for now), the enemy
/// as its navy's picture has them, the estimate carried on to now (World.Sightings). A ship it holds no contact on, it
/// doesn't know.</summary>
public sealed class NavyPicture(World world) : IPicture
{
    public bool TryGet(byte side, int entity, out Sighting s)
    {
        var ships = world.Ships.Store;
        int slot = ships.SlotOf(entity);
        if (slot >= 0 && !world.Relations.Hostile(side, ships[slot].Side))
        {
            var x = ships[slot];
            s = new Sighting(entity, x.Position, x.Forward, x.Speed, x.PoseMs);
            return true;
        }
        var pic = world.Sightings.Of(side);
        int i = pic?.IndexOf(entity) ?? -1;
        if (i < 0)
        {
            s = default;
            return false;
        }
        var (p, v, _) = pic!.EstimateAt(i, world.Time.Ms, world.Settings);
        double speed = v.Length;
        s = new Sighting(entity, p, speed > 1e-6 ? v * (1 / speed) : Earth.Forward(p, 0), speed, world.Time.Ms);
        return true;
    }
}
