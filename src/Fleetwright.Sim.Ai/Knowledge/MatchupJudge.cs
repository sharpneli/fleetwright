using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>The AI's judgements of who can fight whom (docs/battle/command.md 5.1g, piece 3): the one place every layer
/// asks. A unit (the outermost group a ship moves with, or a ship alone) against the enemy its navy holds in sight round
/// it, made by the unit's head and told to the rest of the chain (<see cref="Call"/>); or any two sides a layer builds (the commander's units against an enemy group). By its
/// doctrine's range share both ways (the enemy's own isn't known), in the light at the unit (by night the defence's guns
/// open, and the lines fight, within sight). Not world state, but for the band each unit holds (kept while it's among
/// its best: Matchup.Judge's keepA), which the next moment's judgement leans to; nothing allocated once grown.</summary>
public sealed class MatchupJudge(GroupSystem groups, SeenContacts seen, Doctrine doctrine)
{
    public Matchups Matchups { get; } = new();

    readonly MatchupSide own = new(), theirs = new();
    readonly List<(int Ship, double AsternM, double AbeamM)> line = new(32);
    readonly Dictionary<int, Judged> units = [], told = [];   // the heads' own, and as they last told them
    readonly List<int> written = new(64);                      // units whose call was made since the last Publish
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
            if (store.IsAlive(slot))
                Room(Unit.Ship(store[slot].Entity).Id);
        for (int k = 0; k < groups.Count; k++)
            Room(Unit.Group(groups[k].GroupId).Id);
        written.Capacity = Math.Max(written.Capacity, units.Count);
        var reg = ctx.World.Ships.Registry;
        int classes = reg.Count + reg.LoadoutCount;
        pairs.EnsureCapacity(2 * classes * classes);
        (unitsShips, unitsGroups) = (store.Count, groups.Count);
    }

    Judged Room(int key)
    {
        if (!units.TryGetValue(key, out var c))
            (units[key], told[key]) = (c = new Judged(), new Judged());
        return c;
    }

    /// <summary>A unit's judgement this moment, the classes of its own that attack in it, and its head's call on it.</summary>
    sealed class Judged
    {
        public long Ms = long.MinValue, BandMs = long.MinValue, CallMs = long.MinValue;   // judged; its bands thought out; called
        public MatchupResult Result;
        public bool Any;
        public readonly MatchupProfile?[] Attackers = new MatchupProfile?[Matchup.MaxClasses];
        public int AttackerCount;
        public bool Away;          // turned away under doctrine's bar (back over it by FightRallyShare)
        public int FallBack;       // the guide ship of the own force it falls back on (0: none)

        public void CopyTo(Judged to)
        {
            (to.Ms, to.BandMs, to.CallMs, to.Result, to.Any, to.AttackerCount, to.Away, to.FallBack) =
                (Ms, BandMs, CallMs, Result, Any, AttackerCount, Away, FallBack);
            Array.Copy(Attackers, to.Attackers, AttackerCount);
        }
    }

    /// <summary>A unit's call (docs/battle/workers.md W11): its head's judgement of it against the enemy in sight round it
    /// (none: <see cref="Any"/> false), whether he turns it away under doctrine's bar, and the own force he falls back on
    /// (its guide ship, 0 none).</summary>
    public readonly record struct UnitCall(bool Any, MatchupResult Result, bool Away, int FallBack);

    /// <summary>The unit's head makes its call (docs/battle/workers.md W11): the admiral of the outermost group a ship moves
    /// with, from its guide, or a ship's captain alone, once a think. Its judgement against the enemy in sight within
    /// <see cref="SimSettings.FightCompareM"/> of the guide (docs/battle/command.md 5.1g); away under doctrine's bar
    /// (1 / OutgunnedRatio), back once well over it (<see cref="SimSettings.FightRallyShare"/>); and away, the nearest own
    /// force a match for the enemy (<see cref="FallBackShip"/>). One that can't get away (the enemy as fast) and has no
    /// force to fall back on fights. The rest of the chain hear it as he last told it (<see cref="Told"/>).</summary>
    public UnitCall Call(in AiContext ctx, ShipState guide)
    {
        var c = JudgedNear(ctx, guide);
        if (c.CallMs != ctx.NowMs)
        {
            double accept = Bars(guide.Entity, guide.Side).Accept, conf = c.Any ? c.Result.Confidence : double.PositiveInfinity;
            bool away = c.Away ? conf < accept / ctx.Settings.FightRallyShare : conf < accept;
            int fallBack = away ? FallBackShip(ctx, guide, accept) : 0;
            if (away && fallBack == 0 && !c.Result.ACanRefuse(ctx.Settings))
                away = false;   // no escape: it fights
            (c.Away, c.FallBack, c.CallMs) = (away, fallBack, ctx.NowMs);
            written.Add(UnitKey(guide.Entity));
        }
        return new UnitCall(c.Any, c.Result, c.Away, c.FallBack);
    }

    /// <summary>A ship's unit's call as its head last told it (<see cref="Call"/>, published at the end of his layer's
    /// think): what the rest of the chain act on.</summary>
    public UnitCall Told(in AiContext ctx, ShipState own)
    {
        var c = told.TryGetValue(UnitKey(own.Entity), out var t) ? t : null;
        return c == null || c.CallMs == long.MinValue ? default : new UnitCall(c.Any, c.Result, c.Away, c.FallBack);
    }

    /// <summary>The calls made since the last time told to the chain (after each layer's think).</summary>
    public void Publish()
    {
        foreach (int key in written)
            units[key].CopyTo(told[key]);
        written.Clear();
    }

    int UnitKey(int entity) => groups.UnitOf(entity) is { } unit ? Unit.Group(unit.GroupId).Id : Unit.Ship(entity).Id;

    /// <summary>Falling back on the main body (docs/battle/command.md 5.1c): the guide ship of the nearest own force
    /// (another top unit of the guide's side) within <see cref="SimSettings.FallBackM"/> that's a match for the enemy in
    /// sight round the guide (its matchup against them at the bar or better); 0 if none.</summary>
    int FallBackShip(in AiContext ctx, ShipState guide, double accept)
    {
        var s = ctx.Settings;
        var mine = groups.UnitOf(guide.Entity);
        // the forces in reach nearest first (ties by index), so the first that's a match is the one
        fallCands.Clear();
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (g.Parent != 0 || mine != null && g.GroupId == mine.GroupId || ctx.Ships.SlotOf(groups.GuideShip(g)) is not (int gs and >= 0))
                continue;
            var head = ctx.Ships[gs];
            double d = Earth.Distance(guide.Position, head.Position);
            if (head.Side == guide.Side && d < s.FallBackM)
                fallCands.Add((d, k, gs));
        }
        fallCands.Sort();
        MatchupSide? near = null;   // the enemy round him, once
        foreach (var (_, k, gs) in fallCands)
            if (!(GroupAgainst(ctx, groups[k], guide, near ??= EnemyNear(ctx, guide)) < accept))
                return ctx.Ships[gs].Entity;
        return 0;
    }

    readonly List<(double D, int K, int Slot)> fallCands = new(32);

    /// <summary>Whether a ship's class is among her unit's attackers against the enemy in sight round her, by its call as
    /// told (<paramref name="mine"/>: as she, its head, just made it) (<see cref="Matchup.Attackers"/>,
    /// docs/battle/admirals-orders.md O7c): where her torpedoes do more than her guns. Not whether she has any left, nor
    /// whether the attack is worth making (the judgement's style).</summary>
    public bool Attacks(in AiContext ctx, ShipState own, bool mine = false)
    {
        var c = (mine ? units : told).TryGetValue(UnitKey(own.Entity), out var j) ? j : null;
        if (c == null || !c.Any || c.AttackerCount == 0 || ctx.Ships.SlotOf(own.Entity) is not (int slot and >= 0))
            return false;
        var p = Matchups.Profile(ctx.World, ctx.Ships.Combat.Loadout(slot));
        for (int i = 0; i < c.AttackerCount; i++)
            if (ReferenceEquals(c.Attackers[i], p))
                return true;
        return false;
    }

    Judged JudgedNear(in AiContext ctx, ShipState own)
    {
        int key = UnitKey(own.Entity);
        if (ctx.Ships.Count != unitsShips || groups.Count != unitsGroups)
            MakeUnits(ctx);
        var c = Room(key);   // (made by MakeUnits; a ship that came another way)
        if (c.Ms == ctx.NowMs)
            return c;
        Matchups.Warm(ctx.World);
        this.own.Clear();
        AddUnit(ctx.World, own.Entity, this.own);
        theirs.Clear();
        AddNear(ctx.World, own.Side, own.Position, ctx.Settings.FightCompareM, theirs);
        c.Ms = ctx.NowMs;
        // the band his unit holds (judged last moment), kept while still among its best; the bands are thought out again
        // only every MatchupBandThinkS (a captain doesn't re-weigh the range he fights at every second), the judgement at
        // them each moment
        double held = c.Any ? c.Result.BandA : 0;
        bool think = !c.Any || ctx.NowMs - c.BandMs >= (long)(ctx.Settings.MatchupBandThinkS * 1000);
        var (bandA, bandB) = think ? (0.0, 0.0) : (c.Result.BandA, c.Result.BandB);
        c.Any = theirs.Count > 0 && this.own.Count > 0;
        c.Result = c.Any ? Judge(ctx, own.Entity, own.Side, own.Position, this.own, theirs, NearestM(ctx.World, own), held, bandA, bandB) : default;
        if (think)
            c.BandMs = ctx.NowMs;
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
    /// in the light where it is; the fight opening at a range given (where they are), else where the first guns reach.</summary>
    public MatchupResult Judge(in AiContext ctx, int entity, byte side, Vec3d at, MatchupSide a, MatchupSide b, double openM = 0, double keepA = 0,
        double bandA = 0, double bandB = 0)
    {
        Matchups.Warm(ctx.World);
        double share = entity != 0 ? doctrine.Of(entity, side, DoctrineField.RangeShare) : doctrine.OfGroup(null, side, DoctrineField.RangeShare);
        var weather = ctx.World.Weather.At(at, ctx.World.Time);
        var light = new MatchupLight(weather.VisibilityM, NightLight.At(at, ctx.World.Time, weather.Cloud, ctx.Settings).SkyCdM2);
        return Matchup.Judge(a.Entries, b.Entries, share, share, ctx.Settings, light, openM, keepA, bandA, bandB);
    }

    /// <summary>The range to the nearest enemy a ship's side holds in sight within <see cref="SimSettings.FightCompareM"/>
    /// of her, m (0: none).</summary>
    double NearestM(World world, ShipState of)
    {
        double least = double.MaxValue;
        foreach (int i in seen.Near(world, of.Side, of.Position, world.Settings.FightCompareM))
            least = Math.Min(least, (seen.Position(of.Side, i) - of.Position).Length);
        return least == double.MaxValue ? 0 : least;
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
