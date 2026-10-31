using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>The AI's judgements of who can fight whom (docs/battle/command.md 5.1g, piece 3): the one place every layer
/// asks. A unit (the outermost group a ship moves with, or a ship alone) against the enemy its navy holds in sight round
/// it, once a moment per unit; or any two sides a layer builds (the commander's units against an enemy group). By its
/// doctrine's range share both ways (the enemy's own isn't known), in the light at the unit (by night the defence's guns
/// open, and the lines fight, within sight). Not world state; nothing allocated once grown.</summary>
public sealed class MatchupJudge(GroupSystem groups, SeenContacts seen, Doctrine doctrine)
{
    public Matchups Matchups { get; } = new();

    readonly MatchupSide own = new(), theirs = new();
    readonly List<(int Ship, double AsternM, double AbeamM)> line = new(32);
    readonly Dictionary<int, Judged> units = [];
    readonly Dictionary<(MatchupProfile, MatchupProfile, double), bool> pairs = [];
    readonly MatchupEntry[] oneA = new MatchupEntry[1], oneB = new MatchupEntry[1];

    int unitsShips = -1, unitsGroups = -1;

    /// <summary>A judgement's room for every ship (as a unit of her own) and every group at sea, made when the ships or the
    /// groups change (a spawn, a loss, a detachment: when allocating is allowed), never at a unit's first judgement in a
    /// step (docs/battle/night.md Q2 found one: a ship judging alone for the first time minutes after she left her line);
    /// and the attackers' memo's for every two classes or loadouts, by either side's doctrine (Q3 found that one: a
    /// flotilla making out an attacker's class for the first time a quarter of an hour into the fight).</summary>
    void MakeUnits(in AiContext ctx)
    {
        var store = ctx.Ships;
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && !units.ContainsKey(Unit.Ship(store[slot].Entity).Id))
                units[Unit.Ship(store[slot].Entity).Id] = new Judged();
        for (int k = 0; k < groups.Count; k++)
            if (!units.ContainsKey(Unit.Group(groups[k].GroupId).Id))
                units[Unit.Group(groups[k].GroupId).Id] = new Judged();
        var reg = ctx.World.Ships.Registry;
        int classes = reg.Count + reg.LoadoutCount;
        pairs.EnsureCapacity(2 * classes * classes);
        (unitsShips, unitsGroups) = (store.Count, groups.Count);
    }

    /// <summary>A unit's judgement this moment, and the classes of its own that attack in it.</summary>
    sealed class Judged
    {
        public long Ms = long.MinValue;
        public MatchupResult Result;
        public bool Any;
        public readonly MatchupProfile?[] Attackers = new MatchupProfile?[Matchup.MaxClasses];
        public int AttackerCount;
    }

    /// <summary>A ship's unit against the enemy in sight within <see cref="SimSettings.FightCompareM"/> of her: false if
    /// there's none. Judged once a moment a unit.</summary>
    public bool Near(in AiContext ctx, ShipState own, out MatchupResult r)
    {
        var c = JudgedNear(ctx, own);
        r = c.Result;
        return c.Any;
    }

    /// <summary>Whether a ship's class is among her unit's attackers against the enemy in sight round her
    /// (<see cref="Matchup.Attackers"/>, docs/battle/admirals-orders.md O7c): where her torpedoes do more than her guns.
    /// Not whether she has any left, nor whether the attack is worth making (the judgement's style).</summary>
    public bool Attacks(in AiContext ctx, ShipState own)
    {
        var c = JudgedNear(ctx, own);
        if (!c.Any || c.AttackerCount == 0 || ctx.Ships.SlotOf(own.Entity) is not (int slot and >= 0))
            return false;
        var p = Matchups.Profile(ctx.World, ctx.Ships.Combat.Loadout(slot));
        for (int i = 0; i < c.AttackerCount; i++)
            if (ReferenceEquals(c.Attackers[i], p))
                return true;
        return false;
    }

    Judged JudgedNear(in AiContext ctx, ShipState own)
    {
        var unit = groups.UnitOf(own.Entity);
        int key = unit != null ? Unit.Group(unit.GroupId).Id : Unit.Ship(own.Entity).Id;
        if (ctx.Ships.Count != unitsShips || groups.Count != unitsGroups)
            MakeUnits(ctx);
        if (!units.TryGetValue(key, out var c))
            units[key] = c = new Judged();   // (made by MakeUnits; a ship that came another way)
        if (c.Ms == ctx.NowMs)
            return c;
        Matchups.Warm(ctx.World);
        this.own.Clear();
        AddUnit(ctx.World, own.Entity, this.own);
        theirs.Clear();
        AddNear(ctx.World, own.Side, own.Position, ctx.Settings.FightCompareM, theirs);
        c.Ms = ctx.NowMs;
        c.Any = theirs.Count > 0 && this.own.Count > 0;
        c.Result = c.Any ? Judge(ctx, own.Entity, own.Side, own.Position, this.own, theirs) : default;
        c.AttackerCount = 0;
        var entries = this.own.Entries;
        for (int i = 0; i < entries.Length && c.Any; i++)
            if ((c.Result.Terms.AttackersA >> i & 1) != 0)
                c.Attackers[c.AttackerCount++] = entries[i].Profile;
        return c;
    }

    /// <summary>Whether a class, whole, is an attacker against another, whole, by a side's doctrine in daylight
    /// (<see cref="Matchup.Attackers"/>): for what's judged without the enemy in front of it, a unit's role and a contact
    /// taken for a torpedo threat. Made once a pair.</summary>
    public bool AttackerAgainst(in AiContext ctx, MatchupProfile a, MatchupProfile b, byte side) =>
        AttackerAgainst(ctx.World, a, b, side);

    /// <inheritdoc cref="AttackerAgainst(in AiContext, MatchupProfile, MatchupProfile, byte)"/>
    public bool AttackerAgainst(World world, MatchupProfile a, MatchupProfile b, byte side)
    {
        double share = doctrine.OfGroup(null, side, DoctrineField.RangeShare);
        if (pairs.TryGetValue((a, b, share), out bool yes))
            return yes;
        (oneA[0], oneB[0]) = (new MatchupEntry(a, 1), new MatchupEntry(b, 1));
        pairs[(a, b, share)] = yes = (Matchup.Attackers(oneA, oneB, world.Settings) & 1) != 0;
        return yes;
    }

    /// <summary>The enemy a ship's side holds in sight within <see cref="SimSettings.FightCompareM"/> of her, as
    /// <see cref="GroupAgainst"/> takes them: built once for the many groups a captain's fall-back weighs. Valid until the
    /// next call.</summary>
    public MatchupSide EnemyNear(in AiContext ctx, ShipState of)
    {
        nearTheirs.Clear();
        AddNear(ctx.World, of.Side, of.Position, ctx.Settings.FightCompareM, nearTheirs);
        return nearTheirs;
    }

    readonly MatchupSide nearTheirs = new();

    /// <summary>A group's matchup against the enemy round a ship (<see cref="EnemyNear"/>: one falling back on it, is it a
    /// match for them?); infinite with none.</summary>
    public double GroupAgainst(in AiContext ctx, GroupSystem.Group g, ShipState of, MatchupSide theirs)
    {
        if (theirs.Count == 0)
            return double.PositiveInfinity;
        this.own.Clear();
        AddGroup(ctx.World, g, this.own);
        return this.own.Count == 0 ? 0 : Judge(ctx, of.Entity, of.Side, of.Position, this.own, theirs).Confidence;
    }

    /// <summary>The ships of a ship's unit (her outermost group's line, or herself), exactly.</summary>
    public void AddUnit(World world, int entity, MatchupSide into)
    {
        var store = world.Ships.Store;
        if (groups.UnitOf(entity) is not { } g)
        {
            if (store.SlotOf(entity) is int slot and >= 0)
                into.AddOwn(world, Matchups, slot);
            return;
        }
        AddGroup(world, g, into);
    }

    /// <summary>A group's ships (its line, down the tree), exactly.</summary>
    public void AddGroup(World world, GroupSystem.Group g, MatchupSide into)
    {
        var store = world.Ships.Store;
        groups.LineOffsets(g, line);
        foreach (var (ship, _, _) in line)
            if (store.SlotOf(ship) is int m and >= 0)
                into.AddOwn(world, Matchups, m);
    }

    /// <summary>Every ship of a group, down the tree, in its line or not (a force's destroyers alongside the cruisers
    /// leading them): what goes in when the group is sent in whole.</summary>
    public void AddMembers(World world, GroupSystem.Group g, MatchupSide into)
    {
        var store = world.Ships.Store;
        var members = g.Members;
        for (int i = 0; i < members.Count; i++)
            if (!g.IsForce)
            {
                if (store.SlotOf(members[i]) is int m and >= 0)
                    into.AddOwn(world, Matchups, m);
            }
            else if (groups.Get(members[i]) is { } sub)
                AddMembers(world, sub, into);
    }

    /// <summary>The contacts a side holds in sight within a radius of a point, as they're taken.</summary>
    public void AddNear(World world, byte side, Vec3d at, double radiusM, MatchupSide into)
    {
        var pic = world.Sightings.Of(side);
        if (pic == null)
            return;
        foreach (int i in seen.Near(world, side, at, radiusM))
            into.AddContact(world, Matchups, side, pic[i]);
    }

    /// <summary>A side's judgement of another: by the doctrine of the unit judging (a ship's, else its side's: entity 0),
    /// in the light where it is.</summary>
    public MatchupResult Judge(in AiContext ctx, int entity, byte side, Vec3d at, MatchupSide a, MatchupSide b)
    {
        Matchups.Warm(ctx.World);
        double share = entity != 0 ? doctrine.Of(entity, side, DoctrineField.RangeShare) : doctrine.OfGroup(null, side, DoctrineField.RangeShare);
        var weather = ctx.World.Weather.At(at, ctx.World.Time);
        var light = new MatchupLight(weather.VisibilityM, NightLight.At(at, ctx.World.Time, weather.Cloud, ctx.Settings).SkyCdM2);
        return Matchup.Judge(a.Entries, b.Entries, share, share, ctx.Settings, light);
    }

    /// <summary>Whether one side can get away from another: its slowest ship faster by
    /// <see cref="SimSettings.MatchupSpeedMarginKn"/> than the other's slowest that can chase (as the matchup takes a side's
    /// speed, <see cref="Matchup.Slowest"/>, less any seen stopped). True with nobody to chase, or nobody to get away.</summary>
    public static bool Outruns(MatchupSide mine, MatchupSide enemy, SimSettings s)
    {
        double chase = double.MaxValue;
        foreach (ref readonly var e in enemy.Entries)
            if (e.Count > 0 && e.SpeedKn > 0)
                chase = Math.Min(chase, e.SpeedKn);
        return mine.Count == 0 || chase == double.MaxValue || Matchup.Slowest(mine.Entries) > chase + s.MatchupSpeedMarginKn;
    }

    /// <summary>The bar a unit fights down to (1 / doctrine's outgunned ratio), and the one it seeks a fight at (its seek
    /// ratio): a ship's doctrine, else its side's (entity 0).</summary>
    public (double Accept, double Seek) Bars(int entity, byte side)
    {
        double ratio = entity != 0 ? doctrine.Of(entity, side, DoctrineField.OutgunnedRatio) : doctrine.OfGroup(null, side, DoctrineField.OutgunnedRatio);
        double seek = entity != 0 ? doctrine.Of(entity, side, DoctrineField.SeekRatio) : doctrine.OfGroup(null, side, DoctrineField.SeekRatio);
        return (1 / Math.Max(ratio, 1e-6), seek);
    }
}
