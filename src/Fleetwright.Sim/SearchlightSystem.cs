using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What a ship's searchlights are told (docs/battle/night.md Q4).</summary>
public enum SearchlightOrder : byte
{
    /// <summary>The arcs out.</summary>
    Off,
    /// <summary>The arcs struck and burning behind their shutters, ready to expose at once.</summary>
    Ready,
    /// <summary>Held on a ship (a contact of her navy's), so the guns can lay and range on her.</summary>
    Illuminate,
    /// <summary>Sweeping an arc off her bow, for torpedo craft.</summary>
    Search,
}

/// <summary>The searchlights in the fight (docs/battle/night.md Q4): each ship's lights under one order at a time (off,
/// ready, illuminate a contact, search an arc), and their beams exposed into the world's <see cref="Lights"/> at each look,
/// where they light what they're on, show their lamps, veil their own ship's view down them and glare in the eyes they're
/// on (<see cref="SearchlightModel"/>).
/// <list type="bullet">
/// <item><b>No tick:</b> a beam's bearing at a look is a function of the order, the time and the ships: ordered from off,
/// the arcs are struck in <see cref="SimSettings.SearchlightStrikeS"/>; a beam ordered onto a bearing is trained and
/// exposed <see cref="SimSettings.SearchlightExposeS"/> after that (or after the order, its arcs burning).</item>
/// <item><b>Illuminate:</b> <see cref="SimSettings.SearchlightsOnOne"/> of her working lights that bear on the contact
/// (by their blind arcs) are held on her: on where she is while her navy holds her in sight (the operator sees her in
/// his beam), else on where its picture has her; their light blurred by the control's pointing error. Nothing to hold
/// on while the contact's gone from the picture or seen going down.</item>
/// <item><b>Search:</b> her lights that bear split the arc, each sweeping its share back and forth at
/// <see cref="SimSettings.SearchlightSweepDegS"/>; a beam on a bearing it can't go along shows nothing then.</item>
/// <item><b>A light burns</b> only while it works (<see cref="ShipCondition.SearchlightWorks"/>: whole, powered, manned).</item>
/// </list>
/// The orders come from the captains (or the player): a doctrine's when to burn them is Q7's. World state: replays and
/// hashes. Always registered eighth (<see cref="Id"/>).</summary>
public sealed class SearchlightSystem : IWorldSystem
{
    public const byte Id = 8;
    const ushort OrderCode = 1;

    /// <summary>A ship's lights' order: whose (an entity), what, on whom (an entity), how many lights (0 the default), the
    /// arc's middle (degrees off her bow) and half its width, when it was given, when the arcs burn and when the beams
    /// are out on it.</summary>
    struct Lit
    {
        public int Ship, Target, Count;
        public SearchlightOrder Order;
        public double CentreDeg, HalfDeg;
        public long OrderMs, ReadyMs, OnMs;
    }

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly Sightings sightings;
    // the ships whose lights aren't off, by entity
    Lit[] lit = new Lit[16];
    int count;
    // a gather's lights already taken by a beam; the beams it put out, for the snapshot (not world state: remade at each
    // look): the lamp, the bearing, whose (an entity, her side) and its peak; for the look, the lamp in her frame and the
    // bearing off her bow, its spread, its sweep's rate, when it was exposed and whom it's held on (-1 sweeping), and the
    // look's time
    bool[] taken = new bool[16];
    Vec3d[] outLamp = new Vec3d[16], outLocal = new Vec3d[16];
    double[] outBearing = new double[16], outRelative = new double[16], outCd = new double[16], outSpread = new double[16], outRate = new double[16];
    long[] outOn = new long[16];
    int[] outShip = new int[16], outTarget = new int[16];
    byte[] outSide = new byte[16];
    int outCount;
    long outMs;

    internal SearchlightSystem(SimSettings settings, ShipSystem ships, Sightings sightings) =>
        (this.settings, this.ships, this.sightings) = (settings, ships, sightings);

    /// <summary>A ship's searchlights ordered (docs/battle/night.md Q4): off, ready, held on a ship (an entity), or
    /// searching an arc (its middle, degrees off her bow, and half its width); with how many lights (0: <see cref="SimSettings.SearchlightsOnOne"/>
    /// on a ship, every one that bears searching).</summary>
    public static Command Order(int ship, SearchlightOrder order, int target = 0, int count = 0, double centreDeg = 0, double halfDeg = 0) =>
        new(Id, OrderCode, ship, A: target, B: (int)order | Math.Clamp(count, 0, 255) << 8, X: centreDeg, Y: halfDeg);

    public void Apply(World world, in Command c)
    {
        if (c.Code != OrderCode)
            throw new ArgumentException($"the searchlights have no command {c.Code}");
        var order = (SearchlightOrder)(c.B & 255);
        if (order > SearchlightOrder.Search)
            throw new ArgumentException($"no searchlight order {c.B & 255}");
        int i = Find(c.Entity);
        long now = world.Time.Ms;
        if (order == SearchlightOrder.Off)
        {
            if (i >= 0)
            {
                Array.Copy(lit, i + 1, lit, i, count - i - 1);
                count--;
            }
            return;
        }
        if (i < 0)
        {
            if (count == lit.Length)
                Array.Resize(ref lit, 2 * count);
            i = ~i;
            Array.Copy(lit, i, lit, i + 1, count - i);
            count++;
            lit[i] = new Lit { Ship = c.Entity, ReadyMs = now + (long)(settings.SearchlightStrikeS * 1000), Order = SearchlightOrder.Ready };
        }
        ref var l = ref lit[i];
        int target = order == SearchlightOrder.Illuminate ? (int)c.A : 0;
        int n = c.B >> 8;
        double centre = order == SearchlightOrder.Search ? c.X : 0, half = order == SearchlightOrder.Search ? Math.Clamp(Math.Abs(c.Y), 0, 180) : 0;
        if (l.Order == order && l.Target == target && l.Count == n && l.CentreDeg == centre && l.HalfDeg == half)
            return;   // as it is (a captain gives it again)
        bool moved = order != SearchlightOrder.Ready && (l.Order != order || l.Target != target || l.CentreDeg != centre || l.HalfDeg != half);
        (l.Order, l.Target, l.Count, l.CentreDeg, l.HalfDeg, l.OrderMs) = (order, target, n, centre, half, now);
        if (moved)
            l.OnMs = Math.Max(l.ReadyMs, now + (long)(settings.SearchlightExposeS * 1000));
    }

    /// <summary>The ship's place in the list, or its complement where it'd go.</summary>
    int Find(int ship)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (lit[mid].Ship == ship)
                return mid;
            if (lit[mid].Ship < ship)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return ~lo;
    }

    /// <summary>A ship's lights' order now (off when none), the contact they're on and whether their beams are out.</summary>
    public (SearchlightOrder Order, int Target, bool Exposed) OrderOf(int ship, long nowMs)
    {
        int i = Find(ship);
        if (i < 0)
            return (SearchlightOrder.Off, 0, false);
        ref readonly var l = ref lit[i];
        return (l.Order, l.Target, l.Order >= SearchlightOrder.Illuminate && nowMs >= l.OnMs);
    }

    /// <summary>A ship's searching arc (its middle, degrees off her bow, and half its width); (0, 0) unless searching.</summary>
    public (double CentreDeg, double HalfDeg) ArcOf(int ship)
    {
        int i = Find(ship);
        return i >= 0 && lit[i].Order == SearchlightOrder.Search ? (lit[i].CentreDeg, lit[i].HalfDeg) : (0, 0);
    }

    /// <summary>The beams out now into the lights (at a look, after the fires): each ship's under its order.</summary>
    internal void Gather(World world, Lights lights)
    {
        (outCount, outMs) = (0, world.Time.Ms);
        if (count == 0)
            return;
        var store = ships.Store;
        long now = world.Time.Ms;
        for (int i = 0; i < count; i++)
        {
            ref readonly var l = ref lit[i];
            if (l.Order < SearchlightOrder.Illuminate || now < l.OnMs || store.SlotOf(l.Ship) is not (>= 0 and var slot))
                continue;
            var ship = store[slot];
            var cls = ship.Handling.Class;
            var rows = cls.Searchlights;
            if (rows.Length == 0)
                continue;
            if (taken.Length < rows.Length)
                taken = new bool[Math.Max(rows.Length, 2 * taken.Length)];
            Array.Clear(taken, 0, rows.Length);
            var works = store.Combat.Condition(slot).SearchlightWorks;
            var frame = ship.Frame;
            if (l.Order == SearchlightOrder.Illuminate)
            {
                if (!AimAt(world, ship, l.Target, out var aim))
                    continue;
                int want = l.Count > 0 ? l.Count : settings.SearchlightsOnOne;
                for (int k = 0; k < rows.Length && want > 0; k++)
                {
                    ref readonly var row = ref rows[k];
                    if (!works[k])
                        continue;
                    var lamp = frame.ToWorld(row.X, row.Y, row.HeightM);
                    double brg = Earth.Bearing(lamp, aim);
                    if (!row.Sees(brg - ship.HeadingDeg))
                        continue;
                    lights.AddBeam(lamp, row.BeamCd, slot, brg, row.SpreadDeg, SearchlightModel.PointingDeg(row.Control, settings), row.MirrorM);
                    Out(lamp, new Vec3d(row.X, row.Y, row.HeightM), brg, ship, row.BeamCd, row.SpreadDeg, 0, l.OnMs, l.Target);
                    want--;
                }
            }
            else
            {
                // the arc split among the lights that bear on each share's middle, each sweeping its share
                int n = l.Count > 0 ? l.Count : rows.Length;
                double share = l.HalfDeg / n, t = (now - l.OnMs) / 1000.0 * settings.SearchlightSweepDegS;
                double swing = share > 0 ? t % (4 * share) : 0, at = swing < 2 * share ? swing - share : 3 * share - swing;
                for (int s = 0; s < n; s++)
                {
                    // the first free light that bears on the whole of its share, else on its middle
                    double middle = l.CentreDeg - l.HalfDeg + share * (2 * s + 1);
                    int k = -1;
                    for (int j = 0; j < rows.Length; j++)
                        if (!taken[j] && works[j] && rows[j].Sees(middle))
                        {
                            if (rows[j].Sees(middle - share) && rows[j].Sees(middle + share))
                            {
                                k = j;
                                break;
                            }
                            if (k < 0)
                                k = j;
                        }
                    if (k < 0)
                        continue;
                    taken[k] = true;
                    ref readonly var row = ref rows[k];
                    if (!row.Sees(middle + at))
                        continue;   // swept onto her own upper works for the moment
                    var lamp = frame.ToWorld(row.X, row.Y, row.HeightM);
                    lights.AddBeam(lamp, row.BeamCd, slot, ship.HeadingDeg + middle + at, row.SpreadDeg, 0, row.MirrorM);
                    Out(lamp, new Vec3d(row.X, row.Y, row.HeightM), ship.HeadingDeg + middle + at, ship, row.BeamCd, row.SpreadDeg,
                        share > 0 ? (swing < 2 * share ? 1 : -1) * settings.SearchlightSweepDegS : 0, l.OnMs, -1);
                }
            }
        }
    }

    /// <summary>Where a ship holds her lights on a contact: on her while her navy holds her in sight, else on where its
    /// picture has her now; false when it has none to hold on (gone, or seen going down).</summary>
    internal static bool AimAt(World world, ShipState ship, int target, out Vec3d aim)
    {
        aim = default;
        var pic = world.Sightings.Of(ship.Side);
        int ci = pic?.IndexOf(target) ?? -1;
        if (ci < 0 || pic![ci].Lost)
            return false;
        ref readonly var c = ref pic[ci];
        if (c.InSight && world.Ships.Store.SlotOf(target) is >= 0 and var ts)
            aim = world.Ships.Store[ts].Position;
        else
            aim = c.Position + c.Velocity * ((world.Time.Ms - c.EstMs) / 1000.0);
        return true;
    }

    public void Handle(World world, in SimEvent e) => throw new InvalidOperationException("the searchlights have no events");

    public void Hash(ref StateHash h)
    {
        if (count == 0)
            return;
        h.Add(count);
        for (int i = 0; i < count; i++)
        {
            ref readonly var l = ref lit[i];
            h.Add(l.Ship);
            h.Add((int)l.Order);
            h.Add(l.Target);
            h.Add(l.Count);
            h.Add(l.CentreDeg);
            h.Add(l.HalfDeg);
            h.Add(l.OrderMs);
            h.Add(l.ReadyMs);
            h.Add(l.OnMs);
        }
    }

    void Out(Vec3d lamp, Vec3d local, double bearingDeg, ShipState ship, double candela, double spreadDeg, double rateDegS, long onMs, int target)
    {
        if (outCount == outLamp.Length)
        {
            int n = 2 * outCount;
            Array.Resize(ref outLamp, n);
            Array.Resize(ref outLocal, n);
            Array.Resize(ref outBearing, n);
            Array.Resize(ref outRelative, n);
            Array.Resize(ref outCd, n);
            Array.Resize(ref outShip, n);
            Array.Resize(ref outSide, n);
            Array.Resize(ref outSpread, n);
            Array.Resize(ref outRate, n);
            Array.Resize(ref outOn, n);
            Array.Resize(ref outTarget, n);
        }
        (outLamp[outCount], outBearing[outCount], outShip[outCount], outSide[outCount], outCd[outCount]) = (lamp, bearingDeg, ship.Entity, ship.Side, candela);
        (outSpread[outCount], outRate[outCount], outOn[outCount], outTarget[outCount]) = (spreadDeg, rateDegS, onMs, target);
        (outLocal[outCount], outRelative[outCount]) = (local, bearingDeg - ship.HeadingDeg);
        outCount++;
    }

    /// <summary>The beams out at the last look, for the map (docs/battle/night.md Q9; docs/visuals/night-lights.md Q10b): the
    /// player's view's (<see cref="Snapshot.Truth"/>), the beams of the sides it shows and those of ships the player's
    /// navy has in sight, or her lights.</summary>
    public void Fill(Snapshot s)
    {
        var v = s.Beams;
        v.EnsureCapacity(outCount);
        var pic = s.Truth || s.PlayerSide < 0 ? null : sightings.Of(s.PlayerSide);
        int n = 0;
        for (int k = 0; k < outCount; k++)
        {
            if (!s.Truth && s.PlayerSide >= 0 && !s.ShownSides[outSide[k]])
            {
                int ci = pic?.IndexOf(outShip[k]) ?? -1;
                if (ci < 0 || !pic![ci].InSight && !pic[ci].LightInSight)
                    continue;
            }
            (v.Lamp[n], v.BearingDeg[n], v.Ship[n], v.Candela[n]) = (outLamp[k], outBearing[k], outShip[k], outCd[k]);
            (v.SpreadDeg[n], v.RateDegS[n], v.OnMs[n], v.Target[n]) = (outSpread[k], outRate[k], outOn[k], outTarget[k]);
            (v.LampLocal[n], v.RelativeDeg[n]) = (outLocal[k], outRelative[k]);
            n++;
        }
        (v.Count, v.LookMs) = (n, outMs);
        if (count == 0)
            return;
        var sv = s.Ships;   // each ship's beams out now (docs/battle/night.md Q9: her card and the strip)
        long now = s.Time.Ms;
        for (int k = 0; k < sv.Count; k++)
            if (Find(sv.Entity[k]) is >= 0 and var i && lit[i].Order >= SearchlightOrder.Illuminate && now >= lit[i].OnMs)
                sv.BeamOn[k] = lit[i].Order == SearchlightOrder.Illuminate ? lit[i].Target : -1;
    }
}
