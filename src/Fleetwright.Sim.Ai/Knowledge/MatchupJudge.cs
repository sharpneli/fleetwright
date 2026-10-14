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
    readonly Dictionary<int, (long Ms, MatchupResult Result, bool Any)> units = [];

    /// <summary>A ship's unit against the enemy in sight within <see cref="SimSettings.FightCompareM"/> of her: false if
    /// there's none. Judged once a moment a unit.</summary>
    public bool Near(in AiContext ctx, ShipState own, out MatchupResult r)
    {
        var unit = groups.UnitOf(own.Entity);
        int key = unit != null ? Unit.Group(unit.GroupId).Id : Unit.Ship(own.Entity).Id;
        if (units.TryGetValue(key, out var c) && c.Ms == ctx.NowMs)
        {
            r = c.Result;
            return c.Any;
        }
        Matchups.Warm(ctx.World);
        this.own.Clear();
        AddUnit(ctx.World, own.Entity, this.own);
        theirs.Clear();
        AddNear(ctx.World, own.Side, own.Position, ctx.Settings.FightCompareM, theirs);
        bool any = theirs.Count > 0 && this.own.Count > 0;
        r = any ? Judge(ctx, own.Entity, own.Side, own.Position, this.own, theirs) : default;
        units[key] = (ctx.NowMs, r, any);
        return any;
    }

    /// <summary>A group's matchup against the enemy in sight within <see cref="SimSettings.FightCompareM"/> of a ship (one
    /// falling back on it: is it a match for them?); infinite with none.</summary>
    public double GroupAgainstNear(in AiContext ctx, GroupSystem.Group g, ShipState of)
    {
        this.own.Clear();
        AddGroup(ctx.World, g, this.own);
        theirs.Clear();
        AddNear(ctx.World, of.Side, of.Position, ctx.Settings.FightCompareM, theirs);
        if (theirs.Count == 0)
            return double.PositiveInfinity;
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
        double sight = SightModel.LightReachM(Light.SunElevationDeg(at, ctx.World.Time), ctx.Settings);
        return Matchup.Judge(a.Entries, b.Entries, share, share, ctx.Settings, sight);
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
