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

    /// <summary>Scouting: worked out for a unit with guns and no capital ship that isn't a flotilla (cruisers).</summary>
    Scout,

    /// <summary>A torpedo flotilla: screens the heavy units and is sent in to attack. Worked out for a unit half of whose
    /// ships or more are attackers against the heaviest class of their side (<see cref="Roles.Derived"/>).</summary>
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
public sealed class Roles(World world, Knowledge.MatchupJudge judge)
{
    readonly GroupSystem groups = world.Groups;
    readonly ShipStore store = world.Ships.Store;
    // the references (References), once a moment
    readonly Combat.MatchupProfile?[] heaviest = new Combat.MatchupProfile?[256];
    readonly bool[] sides = new bool[256];
    Combat.MatchupProfile? typical;
    readonly List<(Combat.MatchupProfile Profile, int Count)> tally = new(16);
    long referencesMs = long.MinValue;
    readonly SortedDictionary<int, UnitRole> set = [];

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

    /// <summary>A unit's role as its ships make it, a rough default (the player sets one at will, and the admirals commit
    /// forces by the matchup; docs/battle/admirals-orders.md O7c, decision 7: no ship is torpedo craft by her design):
    /// capital ships, the battle line; half its ships or more boats (<see cref="Count"/>), a flotilla; a merchant guide,
    /// an auxiliary; else scouting.</summary>
    public UnitRole Derived(Unit u)
    {
        int ships = 0, boats = 0, guide = u.IsShip ? u.Entity : 0;
        bool capital = false;
        if (u.IsGroup)
        {
            if (groups.Get(u.GroupId) is not { } g)
                return UnitRole.None;
            guide = groups.GuideShip(g);
            CountGroup(g, ref ships, ref boats, ref capital);
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

    /// <summary>Every ship of a group counted, down the tree: a force's members whether in its line or not (destroyers
    /// alongside the cruisers leading them).</summary>
    void CountGroup(GroupSystem.Group g, ref int ships, ref int boats, ref bool capital)
    {
        var members = g.Members;
        for (int i = 0; i < members.Count; i++)
            if (!g.IsForce)
                Count(members[i], ref ships, ref boats, ref capital);
            else if (groups.Get(members[i]) is { } sub)
                CountGroup(sub, ref ships, ref boats, ref capital);
    }

    /// <summary>A ship counted, and whether she's a boat, by two axes: her torpedoes her better weapon against her own
    /// side's heaviest class at sea (<see cref="Knowledge.MatchupJudge.AttackerAgainst(World, Combat.MatchupProfile, Combat.MatchupProfile, byte)"/>),
    /// and her size under <see cref="SimSettings.RoleScoutSizeRatio"/> times the typical boat's (<see cref="References"/>).
    /// A light cruiser and a destroyer are alike against a battleship, and alike against each other's boats (the sim's
    /// German boats outgun an M-class more than a C-class does); what sets the cruiser apart is her size, and she scouts.</summary>
    void Count(int ship, ref int ships, ref int boats, ref bool capital)
    {
        if (store.SlotOf(ship) is not (int m and >= 0))
            return;
        var cls = store[m].Handling.Class;
        byte side = store[m].Side;
        ships++;
        capital |= Sightings.TypeOf(cls) == ShipType.Capital;
        var p = judge.Matchups.Profile(world, cls);
        var (heaviest, typical) = References(side);
        if (judge.AttackerAgainst(world, p, heaviest ?? p, side) &&
            (typical == null || p.DisplacementT < world.Settings.RoleScoutSizeRatio * typical.DisplacementT))
            boats++;
    }

    /// <summary>The heaviest class a side has at sea, and the typical boat (the census): the most numerous class at sea,
    /// any side's, whose torpedoes are its better weapon against its own side's heaviest (the lighter of two as many),
    /// none if there's none. The references a ship's role is worked out against; found once a moment.</summary>
    (Combat.MatchupProfile? Heaviest, Combat.MatchupProfile? Typical) References(byte side)
    {
        if (referencesMs != world.Time.Ms)
        {
            referencesMs = world.Time.Ms;
            Array.Clear(heaviest);
            Array.Clear(sides);
            tally.Clear();
            typical = null;
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot))
                    sides[store[slot].Side] = true;
            for (int s = 0; s < sides.Length; s++)
                if (sides[s] && judge.Matchups.Census(world, (byte)s, ShipType.Unknown).Classes is { } classes)
                    foreach (var (p, _) in classes)
                        if (heaviest[s] == null || p.DisplacementT > heaviest[s]!.DisplacementT)
                            heaviest[s] = p;
            for (int s = 0; s < sides.Length; s++)
                if (sides[s] && judge.Matchups.Census(world, (byte)s, ShipType.Unknown).Classes is { } classes)
                    foreach (var (p, n) in classes)
                        if (judge.AttackerAgainst(world, p, heaviest[s]!, (byte)s))
                            Tally(p, n);
            int most = 0;
            foreach (var (p, n) in tally)
                if (n > most || n == most && typical != null && p.DisplacementT < typical.DisplacementT)
                    (typical, most) = (p, n);
        }
        return (heaviest[side], typical);
    }

    void Tally(Combat.MatchupProfile p, int n)
    {
        for (int i = 0; i < tally.Count; i++)
            if (ReferenceEquals(tally[i].Profile, p))
            {
                tally[i] = (p, tally[i].Count + n);
                return;
            }
        tally.Add((p, n));
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
