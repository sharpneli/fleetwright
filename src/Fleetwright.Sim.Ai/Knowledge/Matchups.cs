using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>The matchup's sides from the world (docs/battle/command.md 5.1g, piece 2): each class's profile, made once
/// from the world's own range tables, and each side's classes at sea by type (the census), made once a moment it's
/// asked, from which a contact not yet made out to its class is taken as the average of what its side has of its type
/// (the user's pick: closer to the truth, though it knows which classes are at sea) that the navy hasn't accounted for
/// among its other contacts (docs/battle/allocation.md AL7a: good intelligence). Not world state: everything here
/// is made again from the world and the pictures. While the AI thinks on the workers (docs/battle/workers.md W13) its
/// profiles are only read, and the census is counted by the first to ask at a moment.</summary>
public sealed class Matchups
{
    readonly Dictionary<ShipClass, MatchupProfile> profiles = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<Loadout, MatchupProfile> given = new(ReferenceEqualityComparer.Instance);

    // the census: per side, the classes at sea and how many of each
    readonly List<(MatchupProfile Profile, int Count)>?[] census = new List<(MatchupProfile, int)>?[256];
    readonly int[] censusTotal = new int[256];
    long censusMs = long.MinValue;

    /// <summary>A class's profile, made the first time it's asked (its range tables are the world's, built now if new).</summary>
    public MatchupProfile Profile(World world, ShipClass c)
    {
        if (!profiles.TryGetValue(c, out var p))
        {
            if (frozen)
                throw new InvalidOperationException($"{c.Name}'s matchup profile made during a dispatch: Prepare warms them first");
            var reg = world.Shells.Ballistics;
            profiles[c] = p = new MatchupProfile(c, k => reg[reg.Ensure(k)], world.Settings);
        }
        return p;
    }

    /// <summary>An own ship's profile with her loadout (docs/battle/shells-armour-damage.md S5): her class's, unless she
    /// was given another. Others are judged by their class's own: nobody sees what's in another navy's magazines.</summary>
    public MatchupProfile Profile(World world, Loadout lo)
    {
        if (lo.IsDefault)
            return Profile(world, lo.Class);
        if (!given.TryGetValue(lo, out var p))
        {
            if (frozen)
                throw new InvalidOperationException("a loadout's matchup profile made during a dispatch: Prepare warms them first");
            var reg = world.Shells.Ballistics;
            given[lo] = p = new MatchupProfile(lo.Class, k => reg[reg.Ensure(k)], world.Settings, lo);
        }
        return p;
    }

    int warmed, warmedLoadouts;
    bool frozen;   // made ready for a dispatch (docs/battle/workers.md W13): only read until Thaw

    /// <summary>Before a dispatch of thinks on the workers (docs/battle/workers.md W13): every profile and table warmed,
    /// this moment's census counted; only read until <see cref="Thaw"/>.</summary>
    public void Prepare(World world)
    {
        Warm(world);
        frozen = true;
    }

    /// <summary>After the dispatch: made and counted when asked again.</summary>
    public void Thaw() => frozen = false;

    /// <summary>Every class the world's registry holds profiled, and every gun's table against every class (once, and again
    /// when it grows): a contact may be taken for any of them, and a profile or a table made then would be garbage in the
    /// middle of a fight.</summary>
    public void Warm(World world)
    {
        if (frozen)
            return;   // warmed by Prepare
        var reg = world.Ships.Registry;
        int n = reg.Count, loadouts = reg.LoadoutCount;
        if (n == warmed && loadouts == warmedLoadouts)
            return;
        for (; warmedLoadouts < loadouts; warmedLoadouts++)
            Profile(world, reg.LoadoutOf(warmedLoadouts)!);   // the loadouts given, as their ships' own
        for (int id = warmed; id < n; id++)
            if (reg.IsReady(id) && reg.Get(id) is { } c)
                Profile(world, c);
            else
                n = Math.Min(n, id);   // one still building: from it again next time
        warmed = n;
        // and every gun's table against every class and given loadout (a few hundred bytes a pair), so none is made in a
        // fight, nor while the AI thinks on the workers (an own ship with a loadout given is judged by it)
        foreach (var a in profiles.Values.Concat(given.Values))
            foreach (var g in a.Guns)
                foreach (var t in profiles.Values.Concat(given.Values))
                {
                    foreach (var k in g.Kinds)
                        k.Against(t, 0, world.Settings);
                    g.Chosen(t, 0, world.Settings);
                }
    }

    /// <summary>The classes a side has at sea, and how many ships they are in all; made once a moment.</summary>
    public (List<(MatchupProfile Profile, int Count)>? Classes, int Total) Census(World world, byte side)
    {
        if (Volatile.Read(ref censusMs) != world.Time.Ms)
            lock (census)
                if (censusMs != world.Time.Ms)
                    Count(world);   // the first to ask at a moment counts it, the rest wait and read (W13)
        return (census[side], censusTotal[side]);
    }

    /// <summary>A contact's tonnage as a navy (<paramref name="viewer"/>) takes it: its class's once made out, else the
    /// mean of the classes its side has at sea that it may be, weighed by her estimated size (<see cref="Weigh"/>); 0
    /// before its type is made out.</summary>
    public double TonnageOf(World world, byte viewer, in Contact c)
    {
        if (c.Level >= ContactLevel.Class && c.TakenFor >= 0 && world.Ships.Registry.Get(c.TakenFor) is { } taken)
            return taken.Motion.FullDisplacementT;
        if (c.Level < ContactLevel.Type)
            return 0;
        var w = Weigh(world, viewer, c);
        if (w.Total <= 0)
            return 0;
        double t = 0;
        for (int k = 0; k < w.Pool!.Count; k++)
            t += w.Pool[k].Profile.DisplacementT * w.Of(k);
        return t / w.Total;
    }

    /// <summary>The classes a contact made out to her type may be, weighed (docs/battle/allocation.md AL7b): each class
    /// of the pool by its ships, times how near its size is to her estimated size (<see cref="Contact.SizeT"/>: a normal
    /// in the log of her displacement, of her estimate's error, never under <see cref="SimSettings.SightSizeFloor"/>), armed or
    /// not as she's made out. <see cref="Of"/> over <see cref="Total"/> is a class's share; no pool, no total.</summary>
    public readonly struct Weighing(List<(MatchupProfile Profile, double Count)>? pool, double sizeT, double sigma, int armed, bool flat, double total)
    {
        public List<(MatchupProfile Profile, double Count)>? Pool { get; } = pool;
        public double Total { get; } = total;

        /// <summary>The pool's k-th class's weight.</summary>
        public double Of(int k) => Weight(Pool![k].Profile, Pool[k].Count, sizeT, sigma, armed, flat);

        internal static double Weight(MatchupProfile p, double n, double sizeT, double sigma, int armed, bool flat)
        {
            if (n <= 1e-9 || armed >= 0 && ShipTypes.IsMerchant(p.Class) == (armed == 1))
                return 0;
            if (flat || sizeT <= 0 || p.DisplacementT <= 0)
                return n;
            double x = Math.Log(p.DisplacementT / sizeT) / sigma;
            return n * Math.Exp(-0.5 * x * x);
        }
    }

    /// <summary>What a contact a navy (<paramref name="viewer"/>) has made out to her type is one of
    /// (docs/battle/allocation.md AL7a, AL7b): her side's ships at sea less each the navy holds made out to her class (as
    /// taken, wrongly or not), as armed as she is, weighed by her estimated size. Good intel (the user, 2026-11-16):
    /// knowing what's at sea and which of them it's made out, a ship it hasn't is one of the others. With none left of
    /// her kind (more taken for it than are at sea), the side's whole census of it, then of any kind; with none near her
    /// size at all, by number alone. Its pools made once a moment and picture.</summary>
    public Weighing Weigh(World world, byte viewer, in Contact c) => Weigh(Ensure(world, viewer), c, world.Settings);

    static Weighing Weigh(Left l, in Contact c, SimSettings s)
    {
        double sigma = Math.Max(c.SizeSigma, s.SightSizeFloor);
        int armed = c.Armed ? 1 : 0;
        var pool = l.Typed[c.Side];
        double total = Sum(pool, c.SizeT, sigma, armed, false);
        if (total <= 0)
            total = Sum(pool = l.Full[c.Side], c.SizeT, sigma, armed, false);
        if (total <= 0)
            total = Sum(pool, c.SizeT, sigma, armed = -1, false);
        bool flat = total <= 1e-300;
        if (flat)
            total = Sum(pool, c.SizeT, sigma, armed, true);
        return new Weighing(pool, c.SizeT, sigma, armed, flat, total > 1e-300 ? total : 0);
    }

    static double Sum(List<(MatchupProfile Profile, double Count)>? pool, double sizeT, double sigma, int armed, bool flat)
    {
        double t = 0;
        for (int k = 0; pool != null && k < pool.Count; k++)
            t += Weighing.Weight(pool[k].Profile, pool[k].Count, sizeT, sigma, armed, flat);
        return t;
    }

    // what each navy hasn't made out yet (docs/battle/allocation.md AL7a), as fractional ships, by side: the census (Full),
    // the census less the ships made out to their class (Typed), and that less the ships made out to their type too (Ships)
    sealed class Left
    {
        public long Ms = long.MinValue;
        public int Version = -1;
        public readonly List<(MatchupProfile Profile, double Count)>?[] Full = new List<(MatchupProfile, double)>?[256];
        public readonly double[] FullTotals = new double[256];
        public readonly List<(MatchupProfile Profile, double Count)>?[] Typed = new List<(MatchupProfile, double)>?[256];
        public readonly double[] TypedTotals = new double[256];
        public readonly List<(MatchupProfile Profile, double Count)>?[] Ships = new List<(MatchupProfile, double)>?[256];
        public readonly double[] ShipsTotals = new double[256];
    }

    readonly Left?[] left = new Left?[256];

    /// <summary>What a contact a navy (<paramref name="viewer"/>) hasn't made out to her type could be of a side's
    /// (docs/battle/allocation.md AL7a): its ships at sea less each the navy holds made out to her class, and less each
    /// made out to her type (a share of each class she may be, <see cref="Weigh"/>). The prizes' war's commander took
    /// three destroyers seen only as ships for France's average ship, half a cruiser each, though he'd made out her three
    /// cruisers, and declined an even fight at 0.39. Exactly: none when every ship of the side is accounted for; else the
    /// whole census.</summary>
    public (List<(MatchupProfile Profile, double Count)>? Classes, double Total) Unmade(World world, byte viewer, byte side, bool exactly)
    {
        var l = Ensure(world, viewer);
        return exactly ? (l.Ships[side], l.ShipsTotals[side]) : (l.Full[side], l.FullTotals[side]);
    }

    Left Ensure(World world, byte viewer)
    {
        Census(world, viewer);   // this moment's census counted
        var pic = world.Sightings.Of(viewer);
        int version = pic?.Version ?? -2;
        var l = left[viewer];
        if (l == null)
            lock (left)
                l = left[viewer] ??= new Left();
        if (Volatile.Read(ref l.Ms) != world.Time.Ms || Volatile.Read(ref l.Version) != version)
            lock (l)
                if (l.Ms != world.Time.Ms || l.Version != version)
                    CountLeft(world, pic, version, l);
        return l;
    }

    void CountLeft(World world, Picture? pic, int version, Left l)
    {
        for (int side = 0; side < census.Length; side++)
        {
            Copy(census[side], censusTotal[side], ref l.Full[side], ref l.FullTotals[side]);
            Copy(census[side], censusTotal[side], ref l.Typed[side], ref l.TypedTotals[side]);
        }
        var reg = world.Ships.Registry;
        // made out to their class: one of that class off its side's
        for (int k = 0; pic != null && k < pic.Count; k++)
        {
            ref readonly var c = ref pic[k];
            if (c.Out || c.Level < ContactLevel.Class || c.TakenFor < 0 || reg.Get(c.TakenFor) is not { } cls || !profiles.TryGetValue(cls, out var p))
                continue;
            Take(l.Typed[c.Side], ref l.TypedTotals[c.Side], p, 1);
        }
        for (int side = 0; side < 256; side++)
            Copy(l.Typed[side], l.TypedTotals[side], ref l.Ships[side], ref l.ShipsTotals[side]);
        // made out to their type alone: a share of each class she may be off the side's
        for (int k = 0; pic != null && k < pic.Count; k++)
        {
            ref readonly var c = ref pic[k];
            if (c.Out || c.Level != ContactLevel.Type || l.Ships[c.Side] is not { } ships)
                continue;
            var w = Weigh(l, c, world.Settings);
            for (int i = 0; w.Total > 0 && i < w.Pool!.Count; i++)
                if (w.Of(i) is var x && x > 0)
                    Take(ships, ref l.ShipsTotals[c.Side], w.Pool[i].Profile, x / w.Total);
        }
        l.Version = version;
        Volatile.Write(ref l.Ms, world.Time.Ms);
    }

    static void Copy(List<(MatchupProfile Profile, int Count)>? src, int total, ref List<(MatchupProfile Profile, double Count)>? dst, ref double dstTotal)
    {
        if (src == null)
        {
            dst?.Clear();
            dstTotal = 0;
            return;
        }
        dst ??= new(src.Count);
        dst.Clear();
        if (total > 0)
            foreach (var (p, n) in src)
                dst.Add((p, n));
        dstTotal = Math.Max(total, 0);
    }

    static void Copy(List<(MatchupProfile Profile, double Count)>? src, double total, ref List<(MatchupProfile Profile, double Count)>? dst, ref double dstTotal)
    {
        if (src == null)
        {
            dst?.Clear();
            dstTotal = 0;
            return;
        }
        dst ??= new(src.Count);   // made with its source, empty or not: one filled later mid-fight would be garbage
        dst.Clear();
        if (total > 1e-9)
            dst.AddRange(src);
        dstTotal = total > 1e-9 ? total : 0;
    }

    static void Take(List<(MatchupProfile Profile, double Count)>? list, ref double total, MatchupProfile p, double n)
    {
        if (list == null)
            return;
        for (int k = 0; k < list.Count; k++)
            if (ReferenceEquals(list[k].Profile, p))
            {
                double off = Math.Min(n, list[k].Count);
                list[k] = (p, list[k].Count - off);
                total -= off;
                return;
            }
    }

    void Count(World world)
    {
        foreach (var l in census)
            l?.Clear();
        Array.Clear(censusTotal);
        var store = world.Ships.Store;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || store.Combat.Condition(slot).Status != ShipStatus.Afloat)
                continue;
            var ship = store[slot];
            var cls = ship.Handling.Class;
            var p = Profile(world, cls);
            Tally(ship.Side, p);
        }
        Volatile.Write(ref censusMs, world.Time.Ms);   // counted: read from here on
    }

    void Tally(int i, MatchupProfile p)
    {
        var l = census[i] ??= [];
        censusTotal[i]++;
        for (int k = 0; k < l.Count; k++)
            if (ReferenceEquals(l[k].Profile, p))
            {
                l[k] = (p, l[k].Count + 1);
                return;
            }
        l.Add((p, 1));
    }
}

/// <summary>One side of a matchup, as the AI builds it (docs/battle/command.md 5.1g, piece 2): own ships exactly (their
/// guns working, rounds and torpedoes left, speed, the water in them), enemies as the picture takes them (the class
/// they're taken for once made out, else spread over what their side has of their type, and what's seen of their
/// condition: silent, stopped, low, listing). Whole ships of a class are one entry; a damaged one is an entry of her
/// own, merged into her class's when the side has no room left. Reused: clearing and adding allocate nothing once it's
/// grown.</summary>
public sealed class MatchupSide
{
    readonly MatchupEntry[] entries = new MatchupEntry[Matchup.MaxClasses];
    int count;

    public ReadOnlySpan<MatchupEntry> Entries => entries.AsSpan(0, count);
    public int Count => count;

    public void Clear() => count = 0;

    /// <summary>Another side's entries, as they are now (a commander's memory of an enemy he declined).</summary>
    public void CopyFrom(MatchupSide other)
    {
        Array.Copy(other.entries, entries, other.count);
        count = other.count;
    }

    /// <summary>An own ship, exactly; one going down isn't counted.</summary>
    public void AddOwn(World world, Matchups m, int slot)
    {
        var store = world.Ships.Store;
        var k = store.Combat.Condition(slot);
        if (k.Status != ShipStatus.Afloat)
            return;
        var cls = store[slot].Handling.Class;
        var p = m.Profile(world, store.Combat.Loadout(slot));
        if (!k.Damaged && Full(store, slot, cls, world.Time.Ms))
        {
            Add(new MatchupEntry(p, 1));
            return;
        }
        var fit = Fitness.Of(cls, k, world.Damage.Flood(cls).ReserveT);
        var (mainGuns, otherGuns, mainRounds, otherRounds) = Guns(store, slot, cls, k);
        Add(new MatchupEntry(p, 1, mainGuns < 0 ? 1 : mainGuns, otherGuns < 0 ? 1 : otherGuns, mainRounds, otherRounds,
            Torpedoes(store, slot, cls, k, world.Time.Ms), fit.SpeedShare, Math.Clamp(1 - fit.WaterShare, 0.1, 1)));
    }

    /// <summary>A contact as a navy (<paramref name="viewer"/>) holds it: the class it's taken for, else the average of
    /// what its side has at sea of its type (of any type, if it has none of it), else (before its type and side are made
    /// out) of what every other side has, as fractional ships; and its condition as seen. One seen going down or gone
    /// isn't counted.</summary>
    public void AddContact(World world, Matchups m, byte viewer, in Contact c)
    {
        if (c.Out)
            return;
        var s = world.Settings;
        double guns = (c.Seen & SeenFlags.Silent) != 0 ? 0 : 1, speed = (c.Seen & SeenFlags.Stopped) != 0 ? 0 : 1, tough = 1;
        if ((c.Seen & SeenFlags.Low) != 0)
            tough *= s.MatchupSeenLowShare;
        if ((c.Seen & SeenFlags.Listing) != 0)
            tough *= s.MatchupSeenListingShare;
        if ((c.Seen & (SeenFlags.ByBow | SeenFlags.ByStern)) != 0)
            tough *= s.MatchupSeenTrimShare;
        if ((c.Seen & SeenFlags.Burning) != 0)
            tough *= s.MatchupSeenBurningShare;
        if (c.Level >= ContactLevel.Class && c.TakenFor >= 0 && world.Ships.Registry.Get(c.TakenFor) is { } taken)
        {
            Add(new MatchupEntry(m.Profile(world, taken), 1, guns, guns, Speed: speed, Toughness: tough));
            return;
        }
        if (c.Level < ContactLevel.Type)
        {
            // a ship, not yet a type: any of the others' at sea it hasn't made out (every one, if it's made them all out)
            bool exactly = false;
            for (int side = 0; side < 256 && !exactly; side++)
                exactly = side != viewer && m.Census(world, (byte)side).Total > 0 && m.Unmade(world, viewer, (byte)side, true).Total > 1e-9;
            double all = 0;
            for (int side = 0; side < 256; side++)
                if (side != viewer && m.Census(world, (byte)side).Total > 0)
                    all += m.Unmade(world, viewer, (byte)side, exactly).Total;
            for (int side = 0; side < 256 && all > 1e-9; side++)
                if (side != viewer && m.Census(world, (byte)side).Total > 0 && m.Unmade(world, viewer, (byte)side, exactly) is ({ } some, > 1e-9))
                    foreach (var (p, n) in some)
                        if (n > 1e-9)
                            Add(new MatchupEntry(p, n / all, guns, guns, Speed: speed, Toughness: tough));
            return;
        }
        var w = m.Weigh(world, viewer, c);
        if (w.Total <= 0)
            return;   // nothing at sea to take it for
        for (int k = 0; k < w.Pool!.Count; k++)
            if (w.Of(k) / w.Total is var n && n > 1e-9)
                Add(new MatchupEntry(w.Pool[k].Profile, n, guns, guns, Speed: speed, Toughness: tough));
    }

    /// <summary>An entry: whole ships with an entry of their class (whole too) go into it; others are entries of their own,
    /// merged (by count, their shares averaged) into their class's once the side is full.</summary>
    public void Add(in MatchupEntry e)
    {
        bool whole = Whole(e);
        int same = -1;
        for (int i = 0; i < count; i++)
            if (ReferenceEquals(entries[i].Profile, e.Profile))
            {
                if (whole && Whole(entries[i]))
                {
                    entries[i] = entries[i] with { Count = entries[i].Count + e.Count };
                    return;
                }
                same = i;
            }
        if (count < entries.Length)
        {
            entries[count++] = e;
            return;
        }
        if (same < 0)
            return;   // full, and no entry of its class to merge into: left out (a side of 64 classes)
        var x = entries[same];
        double n = x.Count + e.Count, wx = x.Count / n, we = e.Count / n;
        entries[same] = new MatchupEntry(x.Profile, n, wx * x.MainGuns + we * e.MainGuns, wx * x.OtherGuns + we * e.OtherGuns,
            wx * x.MainRounds + we * e.MainRounds, wx * x.OtherRounds + we * e.OtherRounds, wx * x.TorpedoesLeft + we * e.TorpedoesLeft,
            wx * x.Speed + we * e.Speed, wx * x.Toughness + we * e.Toughness);
    }

    static bool Whole(in MatchupEntry e) =>
        e.MainGuns == 1 && e.OtherGuns == 1 && e.MainRounds == 1 && e.OtherRounds == 1 && e.TorpedoesLeft == 1 && e.Speed == 1 && e.Toughness == 1;

    /// <summary>Whether an undamaged ship still has every round and torpedo she sailed with.</summary>
    static bool Full(ShipStore store, int slot, ShipClass cls, long ms)
    {
        for (int m = 0; m < cls.Mounts.Length; m++)
            if (cls.Mounts[m].Battery >= 0 && cls.Mounts[m].Rounds > 0 && store.Combat.Mount(slot, m).Rounds < cls.Mounts[m].Rounds)
                return false;
        for (int l = 0; l < cls.Launchers.Length; l++)
        {
            var st = store.Combat.Launcher(slot, l, cls.Launchers[l], ms);
            if (st.Loaded + st.Spare < cls.Launchers[l].Torpedoes)
                return false;
        }
        return true;
    }

    /// <summary>A ship's guns working by barrels and rounds left by the loadout, chief (<see cref="BatteryRow.Chief"/>) and the
    /// rest (-1: none of a kind).</summary>
    static (double MainGuns, double OtherGuns, double MainRounds, double OtherRounds) Guns(ShipStore store, int slot, ShipClass cls, ShipCondition k)
    {
        double mainB = 0, mainOk = 0, otherB = 0, otherOk = 0, mainR = 0, mainLeft = 0, otherR = 0, otherLeft = 0;
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            ref readonly var mr = ref cls.Mounts[m];
            if (mr.Battery < 0)
                continue;
            bool main = cls.Batteries[mr.Battery].Chief;
            int barrels = cls.TurretTypes[mr.Type].Barrels;
            double ok = k.MountWorks[m] ? k.Guns(m) : 0;
            double rounds = mr.Rounds > 0 ? mr.Rounds : 0, left = mr.Rounds > 0 ? store.Combat.Mount(slot, m).Rounds : 0;
            if (main)
                (mainB, mainOk, mainR, mainLeft) = (mainB + barrels, mainOk + ok, mainR + rounds, mainLeft + left);
            else
                (otherB, otherOk, otherR, otherLeft) = (otherB + barrels, otherOk + ok, otherR + rounds, otherLeft + left);
        }
        return (mainB > 0 ? mainOk / mainB : -1, otherB > 0 ? otherOk / otherB : -1, mainR > 0 ? mainLeft / mainR : 1, otherR > 0 ? otherLeft / otherR : 1);
    }

    /// <summary>A ship's torpedoes left (in launchers that work) as a share of all she sailed with; 1 with none.</summary>
    static double Torpedoes(ShipStore store, int slot, ShipClass cls, ShipCondition k, long ms)
    {
        double all = 0, left = 0;
        for (int l = 0; l < cls.Launchers.Length; l++)
        {
            all += cls.Launchers[l].Torpedoes;
            if (!k.LauncherWorks(l))
                continue;
            var st = store.Combat.Launcher(slot, l, cls.Launchers[l], ms);
            left += st.Loaded + st.Spare;
        }
        return all > 0 ? Math.Min(left / all, 1) : 1;
    }
}
