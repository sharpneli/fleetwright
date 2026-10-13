using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>One encounter as it stands: its id, when it opened and when its sides were last within
/// <see cref="SimSettings.EncounterRangeM"/> and the margin of each other, its ships' count, where it is (its ships'
/// middle on the sphere) and how far its ships reach from there; whether the player's ships are in it, and the one it
/// split off (0 for none).</summary>
public readonly record struct Encounter(int Id, long OpenedMs, long LastHotMs, int Ships, Vec3d Centre, double RadiusM, bool Own = false, int From = 0);

/// <summary>Encounters (docs/battle/plan.md step 3.1b; docs/world/architecture.md "Contacts"): sets of ships, not
/// scenes. Ships on the fine tick near each other, of any side, are clustered (within the encounter range and its margin,
/// chained); a cluster with ships of two sides at war within the encounter range of each other opens one. It stays while
/// they're within the range and the margin, and closes <see cref="SimSettings.EncounterCloseMs"/> after they last were.
/// Clusters joining merge their encounters (the oldest id goes on), a cluster parting splits it (the part keeping its
/// oldest ships' id goes on, the other opens anew). Its ships are held in the fine tier (ShipSystem.Tiers.cs,
/// <c>Ready</c>).
/// <para>Updated every <see cref="SimSettings.RailsCheckMs"/> of the fine tick, and at once at a contact. The clusters
/// come from a grid, never a loop over pairs (the plan's rule, nothing past n log n): the ships sorted by cube, a cube's
/// diagonal the range and margin so its ships are all in reach of each other; joined within a cube, and between
/// neighbouring cubes by the first pair in reach, only while the two aren't joined already.</para></summary>
public sealed class Encounters
{
    readonly SimSettings settings;
    readonly Relations relations;
    readonly List<Encounter> open = new(16);
    int nextId = 1;

    // per slot: the encounter its ship was in at the last update, and that ship's entity (a slot is reused)
    int[] ofSlot = [], entityOfSlot = [];

    // an update's ships (the fine tick's), by index k: slot, position, side, cube key; sorted by cube
    int[] slots = [], order = [], parent = [], oldId = [];
    long[] keys = [];
    Vec3d[] pos = [];
    byte[] side = [];
    sbyte[] heat = [];   // per cluster root: 2 ships at war within the range, 1 within the range and margin, else 0
    // the occupied cubes: their keys (sorted), first ship in order, count, coordinates, and their one side (-1 mixed)
    long[] cubeKey = [];
    int[] cubeStart = [], cubeCount = [], cubeSide = [];
    long[] cubeX = [], cubeY = [], cubeZ = [];
    // per cluster root: its oldest old id and the id it takes; per ship its encounter's index; per encounter its ships,
    // their sum (then their middle) and how far they reach from it
    int[] rootOld = [], rootNew = [], encOf = [], encShips = [];
    bool[] encOwn = [];
    Vec3d[] encSum = [];
    double[] encFar = [];
    readonly List<int> roots = new(16);
    readonly List<long> rootsByOld = new(16);

    internal Encounters(SimSettings settings, Relations relations)
    {
        this.settings = settings;
        this.relations = relations;
    }

    /// <summary>The open encounters, by id (oldest first).</summary>
    public IReadOnlyList<Encounter> Open => open;

    public int Count => open.Count;

    /// <summary>How many have opened since the world began (ids are given in order: the newest is this).</summary>
    public int Opened => nextId - 1;

    /// <summary>The last encounter that was news to the player (opened, or the player's ships came into it), its kind
    /// and when; 0 for none yet.</summary>
    public int NewsId { get; private set; }
    public InterestKind NewsKind { get; private set; }
    public long NewsMs { get; private set; }

    /// <summary>The encounter a ship (by slot and entity) was in at the last update, 0 for none.</summary>
    public int Of(int slot, int entity) => slot >= 0 && slot < ofSlot.Length && entityOfSlot[slot] == entity ? ofSlot[slot] : 0;

    /// <summary>Clusters the ships on the fine tick and opens, merges, splits and closes encounters by them. What was
    /// news to the player this time (<see cref="InterestKind"/>, or null): an encounter the player's ships are in that
    /// they weren't before (opened, or come into), else one opened between others; a part split off one isn't news.</summary>
    internal InterestKind? Update(ShipStore store, long nowMs, int playerSide)
    {
        int n = store.OffRailsCount;
        Ensure(store.Slots, n);
        double reach = settings.EncounterRangeM + settings.EncounterMarginM;
        double openAt = settings.EncounterRangeM + 1;   // a contact's derail comes within a metre of the range
        double cell = reach / Math.Sqrt(3);
        for (int k = 0; k < n; k++)
        {
            int s = store.OffRailsSlot(k);
            var ship = store[s];
            slots[k] = s;
            pos[k] = ship.Position;
            side[k] = ship.Side;
            oldId[k] = Of(s, ship.Entity);
            keys[k] = Key(Cube(pos[k].X, cell), Cube(pos[k].Y, cell), Cube(pos[k].Z, cell));
            order[k] = k;
            parent[k] = k;
            heat[k] = 0;
        }
        Array.Sort(keys, order, 0, n);   // the keys go with the order: keys[i] is order[i]'s

        // the cubes: runs of one key; their ships joined to the run's first (all within reach of each other)
        int cubes = 0;
        for (int i = 0; i < n; i++)
        {
            int k = order[i];
            if (i == 0 || keys[i] != keys[i - 1])
            {
                cubeKey[cubes] = keys[i];
                cubeStart[cubes] = i;
                cubeCount[cubes] = 0;
                cubeSide[cubes] = side[k];
                cubeX[cubes] = Cube(pos[k].X, cell);
                cubeY[cubes] = Cube(pos[k].Y, cell);
                cubeZ[cubes] = Cube(pos[k].Z, cell);
                cubes++;
            }
            int c = cubes - 1;
            cubeCount[c]++;
            if (cubeSide[c] != side[k])
                cubeSide[c] = -1;
            if (cubeCount[c] > 1)
                Union(order[cubeStart[c]], k);
        }
        // within a cube, ships of sides at war heat it; between neighbouring cubes (to two cubes over: a cube is the
        // reach over √3), the first pair in reach joins them, and pairs at war heat them
        for (int c = 0; c < cubes; c++)
        {
            if (cubeSide[c] < 0)
                Heat(c, c, reach, openAt);
            for (long dx = -2; dx <= 2; dx++)
                for (long dy = -2; dy <= 2; dy++)
                    for (long dz = -2; dz <= 2; dz++)
                    {
                        long key = Key(cubeX[c] + dx, cubeY[c] + dy, cubeZ[c] + dz);
                        if (key <= cubeKey[c])
                            continue;   // each pair once, from its lower key (and not the cube itself)
                        int d = Array.BinarySearch(cubeKey, 0, cubes, key);
                        if (d < 0)
                            continue;
                        if (Find(order[cubeStart[c]]) != Find(order[cubeStart[d]]))
                            Join(c, d, reach);
                        if (cubeSide[c] < 0 || cubeSide[d] < 0 || relations.Hostile((byte)cubeSide[c], (byte)cubeSide[d]))
                            Heat(c, d, reach, openAt);
                    }
        }
        Assign(store, n, nowMs);
        return News(n, nowMs, playerSide);
    }

    /// <summary>Marks the encounters the player's ships are in, and says what's news (the player's own first).</summary>
    InterestKind? News(int n, long nowMs, int playerSide)
    {
        int m = open.Count;
        for (int i = 0; i < m; i++)
            encOwn[i] = false;
        if (playerSide >= 0)
            for (int k = 0; k < n; k++)
                if (side[k] == playerSide && Find(open, ofSlot[slots[k]], m) is int at and >= 0)
                    encOwn[at] = true;
        InterestKind? news = null;
        for (int i = 0; i < m; i++)
        {
            var e = open[i];
            // a part split off one the player's ships were in was theirs already
            bool wasOwn = e.Own || e.From != 0 && Find(open, e.From, m) is int f and >= 0 && open[f].Own;
            bool fresh = e.OpenedMs == nowMs && e.From == 0;
            InterestKind? kind = encOwn[i] && !wasOwn ? InterestKind.Own : fresh && !encOwn[i] ? InterestKind.Others : null;
            if (kind is { } k && (news == null || k == InterestKind.Own && news != InterestKind.Own))
            {
                news = k;
                (NewsId, NewsKind, NewsMs) = (e.Id, k, nowMs);
            }
        }
        for (int i = 0; i < m; i++)
            if (open[i].Own != encOwn[i])
                open[i] = open[i] with { Own = encOwn[i] };
        return news;
    }

    /// <summary>Copies the open encounters into a snapshot's part; allocates only to grow it.</summary>
    internal void Fill(EncountersView v)
    {
        v.EnsureCapacity(open.Count);
        for (int i = 0; i < open.Count; i++)
        {
            var e = open[i];
            (v.Id[i], v.Ships[i], v.Centre[i], v.RadiusM[i], v.OpenedMs[i], v.Own[i]) = (e.Id, e.Ships, e.Centre, e.RadiusM, e.OpenedMs, e.Own);
        }
        v.Count = open.Count;
        (v.NewsId, v.NewsKind, v.NewsMs) = (NewsId, NewsKind, NewsMs);
    }

    /// <summary>The first pair of ships of two cubes within reach joins their clusters.</summary>
    void Join(int c, int d, double reach)
    {
        double r2 = reach * reach;
        for (int i = cubeStart[c]; i < cubeStart[c] + cubeCount[c]; i++)
            for (int j = cubeStart[d]; j < cubeStart[d] + cubeCount[d]; j++)
                if ((pos[order[i]] - pos[order[j]]).LengthSquared <= r2)
                {
                    Union(order[i], order[j]);
                    return;
                }
    }

    /// <summary>Ships of sides at war in two cubes (or one) within the range of each other heat their cluster (2),
    /// within the range and margin (1): a mark on its root (<see cref="Union"/> carries it). Stops at the first within
    /// the range; a cluster that's hot already isn't looked at again. Called after the cubes are joined, so both are
    /// one cluster if any pair is in reach.</summary>
    void Heat(int c, int d, double reach, double openAt)
    {
        int rc = Find(order[cubeStart[c]]);
        if (heat[rc] == 2 || rc != Find(order[cubeStart[d]]))
            return;
        double r2 = reach * reach, o2 = openAt * openAt;
        for (int i = cubeStart[c]; i < cubeStart[c] + cubeCount[c]; i++)
        {
            int a = order[i];
            for (int j = c == d ? i + 1 : cubeStart[d]; j < cubeStart[d] + cubeCount[d]; j++)
            {
                int b = order[j];
                if (!relations.Hostile(side[a], side[b]))
                    continue;
                double d2 = (pos[a] - pos[b]).LengthSquared;   // the chord: never more than the way round
                if (d2 > r2)
                    continue;
                heat[rc] = Math.Max(heat[rc], d2 <= o2 ? (sbyte)2 : (sbyte)1);
                if (heat[rc] == 2)
                    return;
            }
        }
    }

    /// <summary>Each cluster takes its encounter: the oldest id among its ships, held by the hottest cluster with it;
    /// the others with it stay in it while they're cool, and split off anew if they're hot. A cluster with none opens one
    /// if it's within the range. So an encounter may span clusters (its sides parting, it cools as one). Encounters no
    /// cluster holds (merged, or their ships gone), or cooled for <see cref="SimSettings.EncounterCloseMs"/>, close.
    /// One sort of the clusters, the rest linear (and a binary search by id).</summary>
    void Assign(ShipStore store, int n, long nowMs)
    {
        roots.Clear();
        for (int k = 0; k < n; k++)
            if (Find(k) == k)
            {
                roots.Add(k);
                rootOld[k] = int.MaxValue;
                rootNew[k] = 0;
            }
        for (int k = 0; k < n; k++)
            if (oldId[k] != 0)
            {
                int r = Find(k);
                rootOld[r] = Math.Min(rootOld[r], oldId[k]);
            }
        // by oldest id, the hottest first, then in root order: the first of each id's run holds it
        rootsByOld.Clear();
        foreach (int r in roots)
            rootsByOld.Add(((long)rootOld[r] << 32) | ((long)(2 - heat[r]) << 30) | (uint)r);
        rootsByOld.Sort();
        int before = open.Count;
        int held = 0;   // the id the run being walked kept, 0 if it closed
        for (int i = 0; i < rootsByOld.Count; i++)
        {
            int r = (int)(rootsByOld[i] & 0x3FFFFFFF);
            int old = rootOld[r];
            bool hot = heat[r] > 0;
            if (old != int.MaxValue && (i == 0 || (int)(rootsByOld[i - 1] >> 32) != old))
            {
                // the run's first: the hottest cluster with this id
                held = 0;
                int at = Find(open, old, before);
                if (at >= 0 && (hot || nowMs - open[at].LastHotMs < settings.EncounterCloseMs))
                {
                    held = old;
                    if (hot)
                        open[at] = open[at] with { LastHotMs = nowMs };
                }
                rootNew[r] = held;   // else cooled long enough (or gone): it closes
                continue;
            }
            if (old != int.MaxValue && !hot)
                rootNew[r] = held;   // a cool part of an encounter held by another: still in it
            else if (heat[r] == 2 || old != int.MaxValue)
            {
                // a new contact, or a hot part split off one held by another
                rootNew[r] = nextId++;
                open.Add(new Encounter(rootNew[r], nowMs, nowMs, 0, default, 0, From: old == int.MaxValue ? 0 : old));
            }
        }
        // the members, and where each encounter is: its ships' middle and how far they reach from it
        Array.Clear(ofSlot, 0, Math.Min(ofSlot.Length, store.Slots));
        int m = open.Count;
        EnsureEncounters(m);
        for (int i = 0; i < m; i++)
        {
            encShips[i] = 0;
            encSum[i] = default;
            encFar[i] = 0;
        }
        for (int k = 0; k < n; k++)
        {
            int id = rootNew[Find(k)];
            ofSlot[slots[k]] = id;
            entityOfSlot[slots[k]] = id == 0 ? 0 : store[slots[k]].Entity;
            encOf[k] = id == 0 ? -1 : Find(open, id, m);
            if (encOf[k] >= 0)
            {
                encShips[encOf[k]]++;
                encSum[encOf[k]] += pos[k];
            }
        }
        for (int i = 0; i < m; i++)
            encSum[i] = encSum[i].Normalized() * Earth.Radius;
        for (int k = 0; k < n; k++)
            if (encOf[k] >= 0)
                encFar[encOf[k]] = Math.Max(encFar[encOf[k]], Earth.Distance(encSum[encOf[k]], pos[k]));
        int w = 0;
        for (int i = 0; i < m; i++)
            if (encShips[i] > 0)   // held by some cluster (or closed: no ships)
                open[w++] = open[i] with { Ships = encShips[i], Centre = encSum[i], RadiusM = encFar[i] };
        open.RemoveRange(w, m - w);
    }

    /// <summary>The index of an encounter by id among the first <paramref name="count"/> (they're in id order), or -1.</summary>
    static int Find(List<Encounter> list, int id, int count)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int m = list[mid].Id;
            if (m == id)
                return mid;
            if (m < id)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return -1;
    }

    int Find(int k)
    {
        while (parent[k] != k)
        {
            parent[k] = parent[parent[k]];
            k = parent[k];
        }
        return k;
    }

    /// <summary>Joins two clusters, the lower index the root: the same clusters whatever order they join in.</summary>
    void Union(int a, int b)
    {
        a = Find(a);
        b = Find(b);
        if (a == b)
            return;
        if (b < a)
            (a, b) = (b, a);
        parent[b] = a;
        heat[a] = Math.Max(heat[a], heat[b]);
    }

    static long Cube(double x, double size) => (long)Math.Floor(x / size);

    static long Key(long x, long y, long z) => ((x & 0x1FFFFF) << 42) | ((y & 0x1FFFFF) << 21) | (z & 0x1FFFFF);

    void Ensure(int slotCount, int n)
    {
        if (ofSlot.Length < slotCount)
        {
            int m = Math.Max(slotCount, ofSlot.Length * 2);
            Array.Resize(ref ofSlot, m);
            Array.Resize(ref entityOfSlot, m);
        }
        if (slots.Length >= n)
            return;
        int c = Math.Max(n, Math.Max(16, slots.Length * 2));
        Array.Resize(ref slots, c);
        Array.Resize(ref order, c);
        Array.Resize(ref parent, c);
        Array.Resize(ref oldId, c);
        Array.Resize(ref keys, c);
        Array.Resize(ref pos, c);
        Array.Resize(ref side, c);
        Array.Resize(ref heat, c);
        Array.Resize(ref cubeKey, c);
        Array.Resize(ref cubeStart, c);
        Array.Resize(ref cubeCount, c);
        Array.Resize(ref cubeSide, c);
        Array.Resize(ref cubeX, c);
        Array.Resize(ref cubeY, c);
        Array.Resize(ref cubeZ, c);
        Array.Resize(ref rootOld, c);
        Array.Resize(ref rootNew, c);
        Array.Resize(ref encOf, c);
    }

    /// <summary>Room for every encounter's sums: those open and as many new as there are clusters.</summary>
    void EnsureEncounters(int count)
    {
        if (encShips.Length >= count)
            return;
        int c = Math.Max(count, Math.Max(16, encShips.Length * 2));
        Array.Resize(ref encShips, c);
        Array.Resize(ref encSum, c);
        Array.Resize(ref encFar, c);
        Array.Resize(ref encOwn, c);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(nextId);
        h.Add(open.Count);
        foreach (var e in open)
        {
            h.Add(e.Id);
            h.Add(e.OpenedMs);
            h.Add(e.LastHotMs);
            h.Add(e.Ships);
            h.Add(e.Own);
            h.Add(e.From);
        }
        h.Add(NewsId);
        h.Add(NewsMs);
        for (int i = 0; i < ofSlot.Length; i++)
            if (ofSlot[i] != 0)
            {
                h.Add(i);
                h.Add(ofSlot[i]);
                h.Add(entityOfSlot[i]);
            }
    }
}
