using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>What a unit brings to its admiral's allocation (docs/battle/allocation.md AL1), every figure relative to its
/// own fleet or the enemy's, never a type or a size line.</summary>
/// <param name="TonnageT">Its ships' full displacement afloat.</param>
/// <param name="StrengthT">What it's worth in a gunfight: how it fares in one against as much tonnage of the enemy's order
/// of battle as its own across the ranges a battle may be fought at, times its tonnage (tonnes of the enemy's mix it
/// matches).</param>
/// <param name="SpeedKn">Its slowest ship's speed as she is now.</param>
/// <param name="Attacker">Its torpedoes beat its guns against the enemy's main body, in half its tonnage or more.</param>
/// <param name="Screening">How it holds its own against as much tonnage of the enemy's attackers as its own, in a gunfight
/// across the guns' ranges (0: the enemy has none).</param>
/// <param name="Scouting">How it holds its own against as much tonnage of the enemy's scouts (his units faster than his
/// line) as its own, in a gunfight across the guns' ranges (0: he has none).</param>
/// <param name="Armed">It has a main battery (a merchant or a tender has none: kept back).</param>
/// <param name="ShipT">Its ships' tonnage-weighted mean displacement (Σt² / Σt): the size of ship its tonnage is in, so
/// a battlecruiser squadron's destroyers don't make its ships small.</param>
/// <param name="Boat">An attacker of boats: half its ships or more under <see cref="SimSettings.RoleScoutSizeRatio"/> times
/// the typical boat's size (the most numerous class among both sides' attackers; a flotilla its light cruiser leads is
/// one). A light cruiser and a destroyer are alike against a battleship; what sets the cruiser apart is her size
/// (admirals-orders.md O7c), relative, so in any era.</param>
public readonly record struct UnitMeasure(double TonnageT, double StrengthT, double SpeedKn, bool Attacker, double Screening, double Scouting,
    bool Armed, double ShipT = 0, bool Boat = false)
{
    /// <summary>Its strength a tonne: how well it fights for its size.</summary>
    public double Quality => TonnageT > 0 ? StrengthT / TonnageT : 0;

    /// <summary>Its strength a ship (its strength a tonne at the size of ship its tonnage is in): what one of its ships is
    /// worth in the line, whatever the unit's size (a fleet's twenty-four battleships in one force, or a squadron of four).</summary>
    public double ShipStrengthT => Quality * ShipT;
}

/// <summary>The measures an admiral allocates his units by (docs/battle/allocation.md AL1), from the matchup
/// (<see cref="Matchup"/>), against the enemy as the side knows him. Before contact that's good intelligence (decision 2):
/// the enemy's order of battle as it is, his ships but not where they are; with no enemy, the side's own fleet. A side's references are worked out once a
/// moment, from his squadrons (and ships in none), whatever forces gather them: the enemy's whole fleet; his line (his
/// strong squadrons, by the same measure against the side's whole fleet, those at about the slowest's speed:
/// <see cref="Line"/>) and its strongest, his main body; his scouts (his squadrons faster than his line by
/// <see cref="SimSettings.AllocationSpareKn"/>) and their strength; his attackers (his squadrons whose torpedoes beat their
/// guns against the side's strongest squadron that isn't one); and the typical boat. Pure of the world's state at
/// the moment; nothing hashed; nothing allocated once grown.</summary>
public sealed class ForceMeasures(World world, MatchupJudge judge, Doctrine doctrine)
{
    sealed class SideRef
    {
        public long Ms = long.MinValue;
        public readonly MatchupSide Enemy = new(), Main = new(), Attackers = new(), Fast = new();
        public double EnemyT, AttackersT, FastT, FastStrengthT, BoatT;
    }

    readonly SideRef?[] refs = new SideRef?[256];
    readonly Dictionary<int, (long Ms, UnitMeasure M)> memo = [];
    readonly MatchupSide own = new(), other = new(), scaled = new(), fleet = new(), boats = new();
    readonly List<Unit> tops = new(64);
    readonly List<(double Ship, double V, double S)> units = new(64);
    readonly object gate = new();

    Matchups Matchups => judge.Matchups;

    /// <summary>A unit's measures now (a ship alone, or a group with every ship down its tree).</summary>
    public UnitMeasure Of(Unit u)
    {
        lock (gate)
        {
            long now = world.Time.Ms;
            if (memo.TryGetValue(u.Id, out var m) && m.Ms == now)
                return m.M;
            var measure = Measure(u);
            memo[u.Id] = (now, measure);
            return measure;
        }
    }

    UnitMeasure Measure(Unit u)
    {
        if (SideOf(u) is not (>= 0 and var side))
            return default;
        var r = Ref((byte)side);
        own.Clear();
        Add(u, own);
        if (own.Count == 0)
            return default;
        double t = Tonnage(own), share = Share((byte)side);
        bool armed = false;
        double t2 = 0;
        foreach (var e in own.Entries)
        {
            armed |= e.Profile.Class.Silhouette.MainMounts > 0;
            t2 += e.Count * e.Profile.DisplacementT * e.Profile.DisplacementT;
        }
        bool attacker = AttackerShare(own, r.Main) >= 0.5;
        double ships = 0, boats = 0;
        foreach (var e in own.Entries)
        {
            ships += e.Count;
            if (e.Profile.DisplacementT < world.Settings.RoleScoutSizeRatio * r.BoatT)
                boats += e.Count;
        }
        return new UnitMeasure(t, Strength(own, t, r.Enemy, r.EnemyT, share), Matchup.Slowest(own.Entries), attacker,
            r.AttackersT > 0 ? Versus(own, t, r.Attackers, r.AttackersT, share) : 0, r.FastT > 0 ? Versus(own, t, r.Fast, r.FastT, share) : 0, armed,
            t > 0 ? t2 / t : 0, attacker && 2 * boats >= ships);
    }

    /// <summary>The strength of the enemy's scouts (his units faster than his line), against the side's fleet: what the
    /// side's own scouting force must be a match for (0: he has none).</summary>
    public double EnemyScoutsT(byte side)
    {
        lock (gate)
            return Ref(side).FastStrengthT;
    }

    /// <summary>Which of a set of units are a line (docs/battle/allocation.md "The allocation"): those whose ships are of
    /// <see cref="SimSettings.AllocationLineShare"/> of the strongest ships' strength or more (the strong: each unit by
    /// its strength a ship, <see cref="UnitMeasure.ShipStrengthT"/>, so a fleet's battleships in one force don't make a
    /// squadron of battlecruisers light), and of them those within <see cref="SimSettings.AllocationSpareKn"/> of the
    /// slowest strong one's speed (no speed to spare: the fast strong are what an admiral can send ahead). The line's
    /// speed (its slowest), and its strongest unit (<c>S</c>: the main body), -1 with none; <paramref name="inLine"/>
    /// marks them, <paramref name="strong"/> the strong.</summary>
    public static (double SpeedKn, int Main) Line(ReadOnlySpan<(double Ship, double V, double S)> units, Span<bool> strong, Span<bool> inLine, SimSettings s)
    {
        double max = 0;
        foreach (var u in units)
            max = Math.Max(max, u.Ship);
        double slowest = double.MaxValue;
        for (int i = 0; i < units.Length; i++)
        {
            strong[i] = max > 0 && units[i].Ship >= s.AllocationLineShare * max;
            if (strong[i])
                slowest = Math.Min(slowest, units[i].V);
        }
        int main = -1;
        double line = double.MaxValue;
        for (int i = 0; i < units.Length; i++)
        {
            inLine[i] = strong[i] && units[i].V <= slowest + s.AllocationSpareKn;
            if (!inLine[i])
                continue;
            line = Math.Min(line, units[i].V);
            if (main < 0 || units[i].S > units[main].S)
                main = i;
        }
        return (main >= 0 ? line : 0, main);
    }

    /// <summary>A side's references, once a moment.</summary>
    SideRef Ref(byte side)
    {
        var r = refs[side] ??= new SideRef();
        long now = world.Time.Ms;
        if (r.Ms == now)
            return r;
        r.Ms = now;
        Matchups.Warm(world);
        var store = world.Ships.Store;
        r.Enemy.Clear();
        fleet.Clear();
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot))
            {
                if (world.Relations.Hostile(side, store[slot].Side))
                    r.Enemy.AddOwn(world, Matchups, slot);   // good intelligence: his order of battle as it is
                else if (store[slot].Side == side)
                    fleet.AddOwn(world, Matchups, slot);
            }
        // no enemy known: his own fleet is the yardstick (its line, its fast units, its attackers against its own line), as
        // rough a reference as a side's own heaviest class was (admirals-orders.md decision 7)
        bool mirror = r.Enemy.Count == 0;
        if (mirror)
            r.Enemy.CopyFrom(fleet);
        r.EnemyT = Tonnage(r.Enemy);
        double fleetT = Tonnage(fleet);
        // his line and his scouts: his top units' strength against as much of the side's fleet, and their speed
        r.Main.Clear();
        r.Fast.Clear();
        r.FastStrengthT = 0;
        Tops(side, enemy: !mirror);
        units.Clear();
        foreach (var v in tops)
        {
            other.Clear();
            Add(v, other);
            if (other.Count == 0)
            {
                units.Add((0, 0, 0));
                continue;
            }
            double ot = Tonnage(other), str = Strength(other, ot, fleet, fleetT, Share((byte)SideOf(v)));
            units.Add((str / ot * ShipT(other), Matchup.Slowest(other.Entries), str));
        }
        Span<bool> strong = stackalloc bool[units.Count], inLine = stackalloc bool[units.Count];
        var (lineKn, main) = Line(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(units), strong, inLine, world.Settings);
        for (int i = 0; i < tops.Count; i++)
        {
            bool fast = main >= 0 && i != main && units[i].S > 0 && units[i].V > lineKn + world.Settings.AllocationSpareKn;
            if (i != main && !fast)
                continue;
            other.Clear();
            Add(tops[i], other);
            if (i == main)
                r.Main.CopyFrom(other);
            else
            {
                foreach (var e in other.Entries)
                    r.Fast.Add(e);
                r.FastStrengthT += units[i].S;
            }
        }
        r.FastT = Tonnage(r.Fast);
        // his attackers: his top units whose torpedoes beat their guns against the side's strongest unit that isn't one
        // (the strongest of all, if every one is)
        r.Attackers.Clear();
        r.AttackersT = 0;
        if (r.Main.Count == 0)
            return r;
        Tops(side, enemy: false);
        double bestOwn = -1;
        bool bestAttacks = true;
        foreach (var v in tops)
        {
            other.Clear();
            Add(v, other);
            if (other.Count == 0)
                continue;
            double s = Strength(other, Tonnage(other), r.Enemy, r.EnemyT, Share(side));
            bool attacks = AttackerShare(other, r.Main) >= 0.5;
            if (bestOwn < 0 || bestAttacks && !attacks || attacks == bestAttacks && s > bestOwn)
            {
                (bestOwn, bestAttacks) = (s, attacks);
                own.CopyFrom(other);
            }
        }
        Tops(side, enemy: !mirror);
        foreach (var v in tops)
        {
            other.Clear();
            Add(v, other);
            if (other.Count > 0 && AttackerShare(other, own) >= 0.5)
                foreach (var e in other.Entries)
                    r.Attackers.Add(e);
        }
        r.AttackersT = Tonnage(r.Attackers);
        // the typical boat: the most numerous class among his attackers and the side's own against his main body (the
        // lighter of two as many)
        boats.Clear();
        foreach (var e in r.Attackers.Entries)
            boats.Add(e);
        if (!mirror)
        {
            Tops(side, enemy: false);
            foreach (var v in tops)
            {
                other.Clear();
                Add(v, other);
                if (other.Count == 0)
                    continue;
                ulong bits = Matchup.Attackers(other.Entries, r.Main.Entries, world.Settings);
                var es = other.Entries;
                for (int i = 0; i < es.Length; i++)
                    if ((bits >> i & 1) != 0)
                        boats.Add(es[i]);
            }
        }
        r.BoatT = 0;
        double most = 0;
        foreach (var e in boats.Entries)
            if (e.Count > most || e.Count == most && e.Profile.DisplacementT < r.BoatT)
                (most, r.BoatT) = (e.Count, e.Profile.DisplacementT);
        return r;
    }

    /// <summary>A unit's strength: how it fares in a gunfight against as much tonnage of a fleet as its own, across the
    /// ranges a battle may be fought at (a fifth to four fifths of the longest main guns' reach between them: the ranges
    /// come from the guns, so it holds in any era), the share of the fleet it puts out over all of them against the share
    /// of its own it loses (a range where neither does much counts for little; one side keeping out of the other's reach
    /// is its speed, which isn't strength), times its tonnage. The torpedo threat to a line is what its screen is for.</summary>
    double Strength(MatchupSide unit, double unitT, MatchupSide against, double againstT, double share)
    {
        if (against.Count == 0 || againstT <= 0)
            return unitT;   // no fleet at all to judge by: a ton is a ton
        ulong key = Key(unit, against, share, 1);
        ref var hit = ref cache[key & CacheMask];
        if (hit.Key == key && !explaining)
            return hit.Value;
        scaled.Clear();
        foreach (var e in against.Entries)
            scaled.Add(e with { Count = e.Count * unitT / againstT });
        double reach = 0;
        foreach (var e in unit.Entries)
            reach = Math.Max(reach, e.Profile.MainReachM);
        foreach (var e in scaled.Entries)
            reach = Math.Max(reach, e.Profile.MainReachM);
        double lostA = 0, lostB = 0;
        for (int i = 1; i <= 4; i++)
        {
            var g = Matchup.Engage(unit.Entries, scaled.Entries, reach * i / 5, MatchupStyle.Gunfight, world.Settings);
            (lostA, lostB) = (lostA + g.LostA, lostB + g.LostB);
            if (i == 2)
                last = g;
        }
        double floor = world.Settings.MatchupFloor;
        last = last with { LostA = lostA, LostB = lostB };
        double strength = Math.Max(lostB, floor) / Math.Max(lostA, floor) * unitT;
        hit = new Cached(key, strength);
        return strength;
    }

    /// <summary>A cache of the measures' matchups (docs/battle/allocation.md AL3: an allocation thinks the same units over
    /// against the same enemy again and again, and only a ship's change of state changes a figure): direct-mapped by a hash
    /// of everything a figure is made of (each side's classes, counts and condition, the range share, which figure), its
    /// full key checked; a slot taken over by another figure is worked out again. Exact, and nothing allocated.</summary>
    readonly record struct Cached(ulong Key, double Value);
    const int CacheMask = 8191;
    readonly Cached[] cache = new Cached[CacheMask + 1];
    bool explaining;

    static ulong Key(MatchupSide a, MatchupSide b, double share, int figure)
    {
        var h = new StateHash();
        h.Add(figure);
        h.Add(share);
        Add(ref h, a);
        h.Add(-1);
        Add(ref h, b);
        return h.Value == 0 ? 1 : h.Value;

        static void Add(ref StateHash h, MatchupSide s)
        {
            foreach (var e in s.Entries)
            {
                h.Add(e.Profile.Number);
                h.Add(e.Count);
                h.Add(e.MainGuns);
                h.Add(e.OtherGuns);
                h.Add(e.MainRounds);
                h.Add(e.OtherRounds);
                h.Add(e.TorpedoesLeft);
                h.Add(e.Speed);
                h.Add(e.Toughness);
            }
        }
    }

    MatchupTerms last;

    /// <summary>The gunfights behind a unit's strength (the bench's): the second range, and each side's share lost over all.</summary>
    public MatchupTerms Explain(Unit u)
    {
        lock (gate)
        {
            if (SideOf(u) is not (>= 0 and var side))
                return default;
            var r = Ref((byte)side);
            own.Clear();
            Add(u, own);
            last = default;
            explaining = true;
            if (own.Count > 0)
                Strength(own, Tonnage(own), r.Enemy, r.EnemyT, Share((byte)side));
            explaining = false;
            return last;
        }
    }

    /// <summary>How a unit holds its own against as much tonnage of a set as its own: its strength against it a tonne
    /// (the same gunfight across the guns' ranges, so neither side's keeping away makes a figure of two near-nothings).</summary>
    double Versus(MatchupSide unit, double unitT, MatchupSide against, double againstT, double share) =>
        unitT > 0 ? Strength(unit, unitT, against, againstT, share) / unitT : 0;

    /// <summary>The share of a unit's tonnage in attackers against a set (<see cref="Matchup.Attackers"/>): a battlecruiser
    /// squadron with its destroyers isn't a flotilla.</summary>
    double AttackerShare(MatchupSide unit, MatchupSide against)
    {
        if (against.Count == 0 || unit.Count == 0)
            return 0;
        ulong key = Key(unit, against, 0, 2);
        ref var hit = ref cache[key & CacheMask];
        if (hit.Key == key)
            return hit.Value;
        ulong bits = Matchup.Attackers(unit.Entries, against.Entries, world.Settings);
        double all = 0, attackers = 0;
        var es = unit.Entries;
        for (int i = 0; i < es.Length; i++)
        {
            double t = es[i].Count * es[i].Profile.DisplacementT;
            all += t;
            if ((bits >> i & 1) != 0)
                attackers += t;
        }
        double share = all > 0 ? attackers / all : 0;
        hit = new Cached(key, share);
        return share;
    }

    double Share(byte side) => doctrine.OfGroup(null, side, DoctrineField.RangeShare);

    /// <summary>A side's tonnage-weighted mean displacement (Σt² / Σt).</summary>
    static double ShipT(MatchupSide s)
    {
        double t = 0, t2 = 0;
        foreach (var e in s.Entries)
        {
            t += e.Count * e.Profile.DisplacementT;
            t2 += e.Count * e.Profile.DisplacementT * e.Profile.DisplacementT;
        }
        return t > 0 ? t2 / t : 0;
    }

    static double Tonnage(MatchupSide s)
    {
        double t = 0;
        foreach (var e in s.Entries)
            t += e.Profile.DisplacementT * e.Count;
        return t;
    }

    /// <summary>A unit's ships into a side: every ship down a group's tree, or the ship.</summary>
    void Add(Unit u, MatchupSide into)
    {
        if (u.IsGroup)
        {
            if (world.Groups.Get(u.GroupId) is { } g)
                judge.AddMembers(world, g, into);
        }
        else if (world.Ships.Store.SlotOf(u.Entity) is int slot and >= 0)
            into.AddOwn(world, Matchups, slot);
    }

    int SideOf(Unit u)
    {
        int ship = u.IsGroup ? world.Groups.Get(u.GroupId) is { } g ? world.Groups.GuideShip(g) : 0 : u.Entity;
        if (ship == 0 && u.IsGroup && world.Groups.Get(u.GroupId) is { } force)
            return world.Groups.SideOf(force);
        return world.Ships.Store.SlotOf(ship) is int slot and >= 0 ? world.Ships.Store[slot].Side : -1;
    }

    /// <summary>The squadrons (groups that aren't forces) and ships in no group of a side, or of every side hostile to it:
    /// what a side's line, scouts and attackers are reckoned from, whatever forces they're gathered in.</summary>
    void Tops(byte side, bool enemy)
    {
        tops.Clear();
        var groups = world.Groups;
        var store = world.Ships.Store;
        bool Of(int s) => enemy ? world.Relations.Hostile(side, (byte)s) : s == side;
        for (int k = 0; k < groups.Count; k++)
            if (!groups[k].IsForce && SideOf(Unit.Group(groups[k].GroupId)) is >= 0 and var s && Of(s))
                tops.Add(Unit.Group(groups[k].GroupId));
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && groups.Of(store[slot].Entity) == null && Of(store[slot].Side))
                tops.Add(Unit.Ship(store[slot].Entity));
    }
}
