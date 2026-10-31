using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Commanders;

/// <summary>What a side wants (docs/battle/command.md "The side's commander, its aims and its home"; docs/battle/aims.md):
/// a small typed set that the missions' objectives share, so how one stands is read in one place (<see cref="AimBook"/>).
/// Hold, Protect a ship and Destroy narrowed come when a scenario needs them.</summary>
public enum AimKind : byte
{
    /// <summary>Sink every ship of every side at war with this one.</summary>
    DestroyEveryone,

    /// <summary>Get <see cref="Aim.Of"/> of the named ships into the zone (the breakout, the invasion, the way home).</summary>
    Reach,

    /// <summary>Sink, or make strike, <see cref="Aim.Tonnes"/> of the enemy's merchants (commerce raiding).</summary>
    Sink,

    /// <summary>The first aim of side <see cref="Aim.About"/> (not a Stop) lost: the convoy's escort, a raider's hunters.</summary>
    Stop,
}

/// <summary>How an aim stands.</summary>
public enum AimState : byte
{
    Open,
    Met,
    Lost,
}

/// <summary>A circle on the sea: a Reach's zone, a Sink's hunting ground. A radius of 0 is none.</summary>
public readonly record struct Zone(double LatDeg, double LonDeg, double RadiusM)
{
    public bool Has => RadiusM > 0;
    public Vec3d Centre => Earth.Position(LatDeg, LonDeg);
    public bool Holds(Vec3d p) => Has && Earth.Distance(p, Centre) <= RadiusM;
}

/// <summary>An aim and its figures (docs/battle/aims.md "The aims"): what each kind reads is its own, the rest left at
/// their defaults. <see cref="Weight"/> weighs it in the side's score.</summary>
public readonly record struct Aim(AimKind Kind, double Weight = 1)
{
    /// <summary>Reach: where to; Sink: the hunting ground, if any.</summary>
    public Zone Zone { get; init; }

    /// <summary>Reach: how many of the named ships must arrive.</summary>
    public int Of { get; init; }

    /// <summary>Reach: how long a ship stays afloat in the zone, without leaving it, to count as arrived (0: on entering).</summary>
    public long StayMs { get; init; }

    /// <summary>Sink: the merchants' tonnage (full load, as the results' tonnes).</summary>
    public double Tonnes { get; init; }

    /// <summary>Stop: the enemy side whose aim (-1: none).</summary>
    public int About { get; init; } = -1;
}

/// <summary>How an aim stands, as read now: its state, and how much of it is done of how much (a Reach's ships, a Sink's
/// tonnes, a destroy everyone's enemy tonnes; a Stop's 1 less its aim's score, of 1).</summary>
public readonly record struct AimReading(AimState State, double Done, double Of)
{
    /// <summary>From 0 to 1: how much of it was done.</summary>
    public double Score => State == AimState.Met ? 1 : Of > 0 ? Math.Clamp(Done / Of, 0, 1) : 0;
}

/// <summary>Every side's aims (docs/battle/aims.md), as the scenario gave them (by command, so a replay has them): each
/// read as the world stands now, for the judge, the commanders and the snapshot alike. What can't be read off the world
/// is kept here: a Reach's arrivals (a ship counts from her arrival on, whatever comes after) and the clock of each ship
/// staying in its zone, and the roster of every ship seen (a ship gone from the sea is still counted lost). Updated at
/// the commanders' think; AI state: hashed; nothing allocated once grown.</summary>
public sealed class AimBook
{
    sealed class Entry
    {
        public Aim Aim;
        public AimState State;                                // as read at the last update
        public readonly List<int> Ships = [];                 // a Reach's named ships, in the order named
        public readonly HashSet<int> Named = [];
        public readonly Dictionary<int, long> Since = [];     // a ship in the zone and not yet arrived: since when
        public readonly HashSet<int> Arrived = [];
    }

    readonly List<Entry>?[] sides = new List<Entry>?[256];
    // every ship seen, in the order seen: its side, tonnage (as the results') and whether she's a merchant
    readonly Dictionary<int, (byte Side, double Tonnes, bool Merchant)> roster = [];
    readonly List<int> seen = [];
    // by side, the kinds its pursued aim wants sunk (bits of 1 << ShipType), as of the last update
    readonly int[] wanted = new int[256];

    /// <summary>How many aims a side has.</summary>
    public int Count(int side) => sides[side & 255]?.Count ?? 0;

    /// <summary>A side's i-th aim.</summary>
    public Aim this[int side, int i] => sides[side & 255]![i].Aim;

    /// <summary>A side's i-th aim set (a command): its index the next, or one already there replaced.</summary>
    internal void Set(int side, int i, in Aim aim)
    {
        var list = sides[side & 255] ??= [];
        if (i < 0 || i > list.Count)
            throw new ArgumentException($"aim {i} of side {side}: the next is {list.Count}");
        if (i == list.Count)
            list.Add(new Entry());
        list[i].Aim = aim;
    }

    /// <summary>A ship named for a side's i-th aim (a command).</summary>
    internal void Name(int side, int i, int ship)
    {
        if (i < 0 || i >= Count(side))
            throw new ArgumentException($"no aim {i} of side {side}");
        var e = sides[side & 255]![i];
        if (e.Named.Add(ship))
            e.Ships.Add(ship);
    }

    /// <summary>A side's i-th aim's named ships.</summary>
    public IReadOnlyList<int> Ships(int side, int i) => sides[side & 255]![i].Ships;

    /// <summary>Whether a ship has arrived for a side's i-th aim.</summary>
    public bool HasArrived(int side, int i, int ship) => sides[side & 255]![i].Arrived.Contains(ship);

    /// <summary>A side's first aim still open as of the last update (the one its commander pursues: docs/battle/aims.md
    /// "Wanted in order"), -1 for none.</summary>
    public int Pursued(int side)
    {
        for (int i = 0; i < Count(side); i++)
            if (sides[side & 255]![i].State == AimState.Open)
                return i;
        return -1;
    }

    /// <summary>The zone a ship of a side is wanted in: its side's first open Reach (as of the last update) naming her that
    /// she hasn't arrived for; else the last zone she arrived at (landed troops' transports stay at the beach, a breaker
    /// in the strait she made for, until another aim wants them elsewhere); null for none.</summary>
    public Zone? ZoneOf(int side, int ship)
    {
        if (sides[side & 255] is not { } list)
            return null;
        Zone? reached = null;
        foreach (var e in list)
        {
            if (e.Aim.Kind != AimKind.Reach || !e.Named.Contains(ship))
                continue;
            if (e.Arrived.Contains(ship))
                reached = e.Aim.Zone;
            else if (e.State == AimState.Open)
                return e.Aim.Zone;
        }
        return reached;
    }

    /// <summary>Whether a ship of a side is reaching for its pursued aim: named in it, a Reach, and not arrived.</summary>
    public bool Reaching(int side, int ship) =>
        Pursued(side) is int p and >= 0 && sides[side & 255]![p] is { Aim.Kind: AimKind.Reach } e && e.Named.Contains(ship) && !e.Arrived.Contains(ship);

    /// <summary>The kinds of enemy ship (bits of 1 &lt;&lt; <see cref="ShipType"/>) a side's pursued aim wants sunk before
    /// the rest (docs/battle/aims.md A3), as of the last update: a Stop of a Reach the named ships' still on their way (a
    /// convoy's transports, a breaker), as a ship's type reads; a Sink merchants; else none. The side knows what the
    /// enemy's after (the scenario's intelligence); his captains know a ship by what they take her for.</summary>
    public int Wanted(int side) => wanted[side & 255];

    /// <summary>Where a side's pursued aim has its main body search for the enemy (docs/battle/aims.md A3, A4), as of the
    /// last update: across the enemy's way (<c>Way</c>) to the zone his Reach makes for, for a Stop of it; the hunting
    /// ground of a Sink, the raider's own or, for a Stop of it, the one her hunters know she hunts; null for none (no
    /// such aim, or a Sink with no ground given).</summary>
    public (Zone Zone, bool Way)? SearchZone(int side)
    {
        if (Pursued(side) is not (int p and >= 0))
            return null;
        var a = this[side, p];
        if (a.Kind == AimKind.Stop)
        {
            if (a.About is not (>= 0 and < 256) || FirstNotStop(a.About) is not (int j and >= 0))
                return null;
            a = this[a.About, j];
            if (a.Kind is not (AimKind.Reach or AimKind.Sink))
                return null;
        }
        else if (a.Kind != AimKind.Sink)
            return null;
        return a.Zone.Has ? (a.Zone, a.Kind == AimKind.Reach) : null;
    }

    /// <summary>The roster taken up to now, and each Reach's ships in their zones: arrived on entering, or once they've
    /// stayed (afloat, never leaving) the aim's time; then every aim's state as it stands, and what each side wants sunk
    /// first. At the commanders' think.</summary>
    internal void Update(World w)
    {
        var store = w.Ships.Store;
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && store[slot] is var x && !roster.ContainsKey(x.Entity))
            {
                roster[x.Entity] = (x.Side, x.Handling.Class.Hydrostatics.DisplacementT, Sightings.TypeOf(x.Handling.Class) == ShipType.Merchant);
                seen.Add(x.Entity);
            }
        long now = w.Time.Ms;
        foreach (var list in sides)
            if (list != null)
                foreach (var e in list)
                {
                    if (e.Aim.Kind != AimKind.Reach)
                        continue;
                    foreach (int ship in e.Ships)
                    {
                        if (e.Arrived.Contains(ship))
                            continue;
                        int slot = store.SlotOf(ship);
                        if (slot < 0 || !Aims.Afloat(store, slot) || !e.Aim.Zone.Holds(store[slot].Position))
                        {
                            e.Since.Remove(ship);
                            continue;
                        }
                        if (!e.Since.TryGetValue(ship, out long since))
                            e.Since[ship] = since = now;
                        if (now - since >= e.Aim.StayMs)
                        {
                            e.Arrived.Add(ship);
                            e.Since.Remove(ship);
                        }
                    }
                }
        for (int side = 0; side < sides.Length; side++)
            for (int i = 0; i < Count(side); i++)
                sides[side]![i].State = Read(w, side, i).State;
        for (int side = 0; side < sides.Length; side++)
            wanted[side] = Count(side) > 0 ? WantedNow(store, side) : 0;
    }

    int WantedNow(ShipStore store, int side)
    {
        if (Pursued(side) is not (int p and >= 0))
            return 0;
        var a = this[side, p];
        if (a.Kind == AimKind.Sink)
            return 1 << (int)ShipType.Merchant;
        if (a.Kind != AimKind.Stop || a.About is not (>= 0 and < 256) || FirstNotStop(a.About) is not (int j and >= 0))
            return 0;
        var e = sides[a.About]![j];
        int bits = 0;
        if (e.Aim.Kind == AimKind.Reach)
            foreach (int ship in e.Ships)
                if (!e.Arrived.Contains(ship) && store.SlotOf(ship) is int slot and >= 0 && Aims.Afloat(store, slot))
                    bits |= 1 << (int)Sightings.TypeOf(store[slot].Handling.Class);
        return bits;
    }

    /// <summary>How a side's i-th aim stands now.</summary>
    public AimReading Read(World w, int side, int i) => Read(w, side, i, 0);

    AimReading Read(World w, int side, int i, int depth)
    {
        var e = sides[side & 255]![i];
        var store = w.Ships.Store;
        switch (e.Aim.Kind)
        {
            case AimKind.DestroyEveryone:
            {
                var (lost, all) = Tonnes(w, side, merchants: false);
                return new(Aims.EnemyAfloat(w, side) ? AimState.Open : AimState.Met, lost, all);
            }
            case AimKind.Reach:
            {
                int arrived = e.Arrived.Count, going = 0;
                foreach (int ship in e.Ships)
                    if (!e.Arrived.Contains(ship) && store.SlotOf(ship) is int slot and >= 0 && Aims.Afloat(store, slot))
                        going++;
                var state = arrived >= e.Aim.Of ? AimState.Met : arrived + going < e.Aim.Of ? AimState.Lost : AimState.Open;
                return new(state, arrived, e.Aim.Of);
            }
            case AimKind.Sink:
            {
                var (lost, all) = Tonnes(w, side, merchants: true);
                var state = lost >= e.Aim.Tonnes ? AimState.Met : all < e.Aim.Tonnes || !SideAfloat(w, side) ? AimState.Lost : AimState.Open;
                return new(state, lost, e.Aim.Tonnes);
            }
            case AimKind.Stop:
            {
                int j = e.Aim.About is >= 0 and < 256 && depth == 0 ? FirstNotStop(e.Aim.About) : -1;
                if (j < 0)
                    return new(AimState.Open, 0, 1);
                var r = Read(w, e.Aim.About, j, depth + 1);
                var state = r.State switch { AimState.Met => AimState.Lost, AimState.Lost => AimState.Met, _ => AimState.Open };
                return new(state, 1 - r.Score, 1);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(i), $"aim kind {e.Aim.Kind}");
        }
    }

    /// <summary>A side's score, 0 to 1: its aims' scores weighted.</summary>
    public double Score(World w, int side)
    {
        double sum = 0, weights = 0;
        for (int i = 0; i < Count(side); i++)
        {
            double wt = this[side, i].Weight;
            sum += wt * Read(w, side, i).Score;
            weights += wt;
        }
        return weights > 0 ? sum / weights : 0;
    }

    /// <summary>A side's first aim that isn't a Stop, -1 for none.</summary>
    int FirstNotStop(int side)
    {
        for (int i = 0; i < Count(side); i++)
            if (this[side, i].Kind != AimKind.Stop)
                return i;
        return -1;
    }

    /// <summary>Of the ships of the sides at war with this one (merchants only, or all), the tonnage lost and the whole:
    /// lost from the roster (a ship gone from the sea, going down or given up), afloat as the store has them now (so a
    /// reading before the first roster has them all afloat).</summary>
    (double Lost, double All) Tonnes(World w, int side, bool merchants)
    {
        var store = w.Ships.Store;
        double lost = 0, afloat = 0;
        foreach (int entity in seen)
        {
            var (s, t, merchant) = roster[entity];
            if ((!merchants || merchant) && w.Relations.Hostile((byte)side, s) && !(store.SlotOf(entity) is int slot and >= 0 && Aims.Afloat(store, slot)))
                lost += t;
        }
        for (int slot = 0; slot < store.Slots; slot++)
            if (Aims.Afloat(store, slot) && store[slot] is var x && w.Relations.Hostile((byte)side, x.Side) &&
                (!merchants || Sightings.TypeOf(x.Handling.Class) == ShipType.Merchant))
                afloat += x.Handling.Class.Hydrostatics.DisplacementT;
        return (lost, lost + afloat);
    }

    static bool SideAfloat(World w, int side)
    {
        var store = w.Ships.Store;
        for (int slot = 0; slot < store.Slots; slot++)
            if (Aims.Afloat(store, slot) && store[slot].Side == side)
                return true;
        return false;
    }

    internal void Hash(ref StateHash h)
    {
        h.Add(seen.Count);
        foreach (int entity in seen)
            h.Add(entity);
        for (int side = 0; side < sides.Length; side++)
        {
            if (sides[side] is not { } list)
                continue;
            h.Add(side);
            foreach (var e in list)
            {
                h.Add((int)e.Aim.Kind);
                h.Add((int)e.State);
                foreach (int ship in e.Ships)
                {
                    h.Add(ship);
                    h.Add(e.Arrived.Contains(ship) ? 1 : 0);
                    h.Add(e.Since.TryGetValue(ship, out long since) ? since : -1);
                }
            }
        }
    }

    /// <summary>Every side's aims as they stand, into the snapshot.</summary>
    internal void Fill(World w, Snapshot s)
    {
        s.AimCount = 0;
        for (int side = 0; side < sides.Length; side++)
            for (int i = 0; i < Count(side) && s.AimCount < s.Aims.Length; i++)
            {
                var a = this[side, i];
                var r = Read(w, side, i);
                s.Aims[s.AimCount++] = new AimView((byte)side, (byte)a.Kind, (byte)r.State, r.Done, r.Of, a.Weight, a.About);
            }
    }
}

/// <summary>Whether a ship is in the fight, and whether a side's enemies are: the aims' and the judge's facts.</summary>
public static class Aims
{
    /// <summary>Whether a ship is still in the fight: at sea, not going down, and not given up (her colours down or her
    /// men off her: docs/battle/striking.md SK5). A ship going down counts as lost from the moment it starts (the combat
    /// log's <see cref="CombatEvent.Lost"/>), not when it's gone from the sea; one given up from her people's call.</summary>
    public static bool Afloat(ShipStore store, int slot) =>
        store.IsAlive(slot) && store.Combat.Condition(slot) is not ({ Status: not ShipStatus.Afloat } or { IsGivenUp: true });

    /// <summary>Whether any ship of a side at war with this one is afloat.</summary>
    public static bool EnemyAfloat(World w, int side)
    {
        var store = w.Ships.Store;
        for (int slot = 0; slot < store.Slots; slot++)
            if (Afloat(store, slot) && w.Relations.Hostile((byte)side, store[slot].Side))
                return true;
        return false;
    }
}
