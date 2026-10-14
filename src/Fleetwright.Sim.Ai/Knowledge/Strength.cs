using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>Fighting strength (docs/battle/command.md "The tactical picture"), one measure both ways: heavy guns, 12"
/// guns' worth (guns × (calibre / 305 mm)³ of the main batteries). An own unit's is its line's, exactly; the enemy's is
/// what its navy sees, each contact as the class it's taken for once that's made out, else by its type
/// (<see cref="SimSettings.FightTypeGuns"/>), so a class mistaken misleads, as it should. What Fight's outgunned test
/// and a scout's judgement read.</summary>
public static class Strength
{
    /// <summary>A class's main batteries in 12" guns' worth.</summary>
    public static double Of(ShipClass cls)
    {
        double w = 0;
        foreach (ref readonly var b in cls.Batteries)
            if (b.Kind == Battery.Main)
                w += b.Guns * Math.Pow(b.CalibreMm / 305, 3);
        return w;
    }

    /// <summary>What a navy sees within a radius of a point: every contact in sight there, as it's taken.</summary>
    public static double Seen(World world, SeenContacts seen, byte side, Vec3d at, double radiusM)
    {
        var s = world.Settings;
        var pic = world.Sightings.Of(side);
        var registry = world.Ships.Registry;
        double theirs = 0;
        if (pic == null)
            return 0;
        foreach (int i in seen.Near(world, side, at, radiusM))
        {
            ref readonly var c = ref pic[i];
            theirs += c.Level >= ContactLevel.Class && c.TakenFor >= 0 && registry.Get(c.TakenFor) is { } taken
                ? Of(taken)
                : s.FightTypeGuns[Math.Min((int)c.Type, s.FightTypeGuns.Length - 1)];
        }
        return theirs;
    }

    /// <summary>An own ship's unit: the line of the outermost group she moves with, or herself alone.</summary>
    public static double OfUnit(GroupSystem groups, ShipStore ships, int entity, List<(int Ship, double AsternM, double AbeamM)> scratch)
    {
        if (groups.UnitOf(entity) is not { } unit)
            return ships.SlotOf(entity) is int slot and >= 0 ? Of(ships[slot].Handling.Class) : 0;
        return OfGroup(groups, ships, unit, scratch);
    }

    /// <summary>An own group's line, down the tree.</summary>
    public static double OfGroup(GroupSystem groups, ShipStore ships, GroupSystem.Group g, List<(int Ship, double AsternM, double AbeamM)> scratch)
    {
        groups.LineOffsets(g, scratch);
        double ours = 0;
        foreach (var (ship, _, _) in scratch)
            if (ships.SlotOf(ship) is int m and >= 0)
                ours += Of(ships[m].Handling.Class);
        return ours;
    }
}
