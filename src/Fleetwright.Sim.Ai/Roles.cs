using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>What a unit is for (docs/battle/admirals-orders.md O6, decision 4): what its commander (the side's, or its
/// force's admiral) uses it as. The player can set one (<see cref="Heavy"/>, <see cref="Scout"/>, <see cref="Screen"/>,
/// <see cref="Flotilla"/>, <see cref="Auxiliary"/>); else it's worked out from the unit's ships.</summary>
public enum UnitRole : byte
{
    /// <summary>Not to be used: fallen out, held by the player, or gone (a commander's own, at a think). As a set role:
    /// none set, worked out.</summary>
    None,

    /// <summary>The unit with the most capital ships' tonnage (else its biggest): what the rest support. A commander's
    /// pick at a think, never set.</summary>
    MainBody,

    /// <summary>The battle line: worked out for a unit with capital ships.</summary>
    Heavy,

    /// <summary>Scouting: worked out for a unit with guns and no capital ship that isn't torpedo craft (cruisers).</summary>
    Scout,

    /// <summary>A torpedo flotilla: screens the heavy units and is sent in to attack. Worked out for a unit of torpedo
    /// craft (half its ships or more).</summary>
    Flotilla,

    /// <summary>No main battery (a seaplane carrier, a tender): kept out of the fight.</summary>
    Auxiliary,

    /// <summary>Screening: screens the heavy units and counter-attacks, never sent in to attack. Only set, never worked
    /// out: four battleships told they're a screen are one.</summary>
    Screen,
}

/// <summary>The units' roles (docs/battle/admirals-orders.md O6): the one place every layer and the map ask what a unit
/// is for. A role set by the player (<see cref="AiCommand.Role"/>, logged) holds; else it's worked out from the unit's
/// line as it is (<see cref="Derived"/>). AI state: hashed, filled into the snapshot; nothing allocated once grown.</summary>
public sealed class Roles(GroupSystem groups, ShipStore store)
{
    readonly SortedDictionary<int, UnitRole> set = [];
    readonly List<(int Ship, double AsternM, double AbeamM)> line = new(32);

    /// <summary>The roles the player can set, in the order a picker offers them.</summary>
    public static readonly UnitRole[] Settable = [UnitRole.Heavy, UnitRole.Scout, UnitRole.Screen, UnitRole.Flotilla, UnitRole.Auxiliary];

    /// <summary>A unit's role set (<see cref="UnitRole.None"/>: back to worked out).</summary>
    internal void Set(Unit u, UnitRole role)
    {
        if (role == UnitRole.None)
            set.Remove(u.Id);
        else if (Array.IndexOf(Settable, role) < 0)
            throw new ArgumentException($"not a role to set: {role}");
        else
            set[u.Id] = role;
    }

    /// <summary>Whether the player set a unit's role.</summary>
    public bool IsSet(Unit u) => set.ContainsKey(u.Id);

    /// <summary>A unit's role: the one set, else worked out.</summary>
    public UnitRole Of(Unit u) => set.TryGetValue(u.Id, out var r) ? r : Derived(u);

    /// <summary>A unit's role as its ships make it: capital ships, the battle line; torpedo craft (half its ships or
    /// more), a flotilla; a guide with no main battery, an auxiliary; else scouting.</summary>
    public UnitRole Derived(Unit u)
    {
        int ships = 0, boats = 0, guide = u.IsShip ? u.Entity : 0;
        bool capital = false;
        if (u.IsGroup)
        {
            if (groups.Get(u.GroupId) is not { } g)
                return UnitRole.None;
            groups.LineOffsets(g, line);
            guide = groups.GuideShip(g);
            foreach (var (ship, _, _) in line)
                Count(ship, ref ships, ref boats, ref capital);
        }
        else
            Count(guide, ref ships, ref boats, ref capital);
        if (ships == 0 || store.SlotOf(guide) is not (int slot and >= 0))
            return UnitRole.None;
        return capital ? UnitRole.Heavy
            : 2 * boats >= ships ? UnitRole.Flotilla
            : Sightings.TypeOf(store[slot].Handling.Class) == ShipType.Merchant ? UnitRole.Auxiliary
            : UnitRole.Scout;
    }

    void Count(int ship, ref int ships, ref int boats, ref bool capital)
    {
        if (store.SlotOf(ship) is not (int m and >= 0))
            return;
        var cls = store[m].Handling.Class;
        ships++;
        capital |= Sightings.TypeOf(cls) == ShipType.Capital;
        if (CaptainLayer.TorpedoCraft(cls))
            boats++;
    }

    public void Hash(ref StateHash h)
    {
        if (set.Count == 0)
            return;   // a world with no roles set hashes as before them
        h.Add(set.Count);
        foreach (var (unit, role) in set)
        {
            h.Add(unit);
            h.Add((int)role);
        }
    }

    public void Fill(Snapshot s)
    {
        var gv = s.Groups;
        for (int k = 0; k < gv.Count; k++)
        {
            var u = Unit.Group(gv.Id[k]);
            (gv.Role[k], gv.RoleSet[k]) = ((byte)Of(u), IsSet(u));
        }
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
        {
            var u = Unit.Ship(v.Entity[k]);
            (v.Role[k], v.RoleSet[k]) = groups.Of(v.Entity[k]) != null ? ((byte)0, false) : ((byte)Of(u), IsSet(u));
        }
    }
}
