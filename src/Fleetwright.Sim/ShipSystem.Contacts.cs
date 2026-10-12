using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>Contacts (docs/battle/plan.md step 2.5, the first of 3.1's contact scan): opposing ships within the
/// encounter range of each other are in the fine tier, and nowhere else does the fine tier need to run. Ships on rails
/// are functions of time, so the moment two opposing rails come within range is solved from the rails themselves and
/// is an event that takes both (and their groups) off them. While any ship is on the fine tick, it checks its distance
/// to opposing rails as it goes; and nothing goes on rails within range (and a margin) of an opposing ship.
/// <para>A unit on rails is a ship on rails of its own: alone, or a group's guide with its ships riding it, which
/// counts as the guide's position grown by its farthest ship's offset. Two units' distance can't change faster than
/// their speeds together, so a solve steps safely toward the crossing (each step as far as the gap allows) and lands
/// on it to a millisecond, whatever shape their legs are. A solve looks ahead to the next scan, every
/// <see cref="SimSettings.ContactScanMs"/>; a unit's rails changing solves it again at once.</para>
/// <para>Every pair of opposing units is looked at, but a pair that can't close before the next scan (the chord
/// between where each was last anchored or scanned, less how far each can have steamed since) is passed over without
/// a solve: a subtraction and a length. A grid on the sphere can come with 3.1 if the numbers ask, with relations
/// (today any two different sides are opposing).</para></summary>
public sealed partial class ShipSystem
{
    const double ContactToleranceM = 1;

    readonly int[] sideCount = new int[256];
    int liveSides;
    long nextScanMs = -1;
    int scanGen;
    double[] unitRadius = new double[64];

    // each unit's pose at its last anchoring or scan: it's within its speed times the time since of there (a speed
    // changes only as its rails are anchored again), so a pair that can't close in time is passed over by a chord's length
    Vec3d[] refPos = new Vec3d[64];
    long[] refMs = new long[64];
    double[] refSpeed = new double[64];
    byte[] refSide = new byte[64];

    // a scan's units, compact
    int[] scanSlot = new int[64];
    byte[] scanSide = new byte[64];
    Vec3d[] scanPos = new Vec3d[64];
    double[] scanSlack = new double[64];

    /// <summary>Whether ships of more than one side are at sea: only then is there anything to look for.</summary>
    public bool Opposed => liveSides > 1;

    void SideCount(byte side, int delta)
    {
        int before = sideCount[side];
        sideCount[side] += delta;
        if (before == 0 && sideCount[side] > 0)
            liveSides++;
        else if (before > 0 && sideCount[side] == 0)
            liveSides--;
    }

    /// <summary>A unit on rails: a ship on rails of its own.</summary>
    bool IsUnit(int slot) => Store.IsAlive(slot) && Store.OnRails(slot) && !Store.RidesAnother(slot);

    /// <summary>A unit's rails were laid or changed: its spread and reference pose kept, and it's solved against every
    /// opposing unit up to the next scan. Its group's ships ride it already (they're railed first), and their offsets
    /// hold until it's anchored again.</summary>
    void Anchored(World world, int slot)
    {
        if (!IsUnit(slot))
            return;
        Remember(world, slot, world.Time.Ms);   // always: a side may put to sea later
        if (!Opposed)
            return;
        EnsureScan(world);
        long until = nextScanMs;
        var p = refPos[slot];
        byte side = refSide[slot];
        double sa = settings.EncounterRangeM + unitRadius[slot] + refSpeed[slot] * (until - refMs[slot]) / 1000;
        for (int j = 0; j < Store.Slots; j++)
        {
            if (j == slot || refSide[j] == side || !IsUnit(j))
                continue;
            // the chord between where each was last anchored or scanned (never more than the way round), less how
            // far each can have gone by then: beyond reach, they can't meet before the next scan
            double reach = sa + unitRadius[j] + refSpeed[j] * (until - refMs[j]) / 1000;
            if ((refPos[j] - p).LengthSquared > reach * reach)
                continue;
            SolvePair(world, slot, j, until);
        }
    }

    /// <summary>Room in the per-slot references for every slot; grows only with the store.</summary>
    void EnsureRefs()
    {
        if (refPos.Length >= Store.Slots)
            return;
        int n = Math.Max(Store.Slots, refPos.Length * 2);
        Array.Resize(ref refPos, n);
        Array.Resize(ref refMs, n);
        Array.Resize(ref refSpeed, n);
        Array.Resize(ref refSide, n);
        Array.Resize(ref unitRadius, n);
    }

    /// <summary>Keeps a unit's pose now, its speed, side and spread (its farthest riding ship's offset).</summary>
    void Remember(World world, int slot, long ms)
    {
        EnsureRefs();
        var s = Store[slot];
        (refPos[slot], refMs[slot], refSpeed[slot], refSide[slot]) = (s.Position, ms, Math.Abs(s.Speed), s.Side);
        double r = 0;
        if (world.Groups.Of(s.Entity) is { } g)
            for (int i = 1; i < g.Line.Count; i++)
                if (Store.SlotOf(g.Line[i]) is int m and >= 0 && Store.RidingOn(m) == slot)
                    r = Math.Max(r, Store.RailOffsetM(m));
        unitRadius[slot] = r;
    }

    void EnsureScan(World world)
    {
        if (nextScanMs >= 0)
            return;
        long period = settings.ContactScanMs;
        nextScanMs = (world.Time.Ms / period + 1) * period;
        scanGen++;
        world.Schedule(new WorldTime(nextScanMs), EventTier.Rails, 0, Id, ContactScan, scanGen);
    }

    /// <summary>Every pair of opposing units solved afresh, as far as the next scan.</summary>
    void Scan(World world, in SimEvent e)
    {
        if (e.Generation != scanGen)
            return;
        nextScanMs = -1;
        if (!Opposed)
            return;
        // the units in a compact list, each with how far it can close by the next scan (its spread and its run), so a
        // pair is a subtraction and a squared length
        EnsureScan(world);
        if (scanSlot.Length < Store.Slots)
        {
            int n = Math.Max(Store.Slots, scanSlot.Length * 2);
            Array.Resize(ref scanSlot, n);
            Array.Resize(ref scanSide, n);
            Array.Resize(ref scanPos, n);
            Array.Resize(ref scanSlack, n);
        }
        int units = 0;
        double run = (nextScanMs - world.Time.Ms) / 1000.0;
        for (int i = 0; i < Store.Slots; i++)
            if (IsUnit(i))
            {
                Remember(world, i, world.Time.Ms);
                var s = Store[i];
                (scanSlot[units], scanSide[units], scanPos[units], scanSlack[units]) =
                    (i, s.Side, refPos[i], unitRadius[i] + Math.Abs(s.Speed) * run);
                units++;
            }
        double range = settings.EncounterRangeM;
        for (int a = 0; a < units; a++)
        {
            var pa = scanPos[a];
            byte side = scanSide[a];
            double sa = range + scanSlack[a];
            for (int b = a + 1; b < units; b++)
            {
                if (scanSide[b] == side)
                    continue;
                double reach = sa + scanSlack[b];
                if ((scanPos[b] - pa).LengthSquared > reach * reach)
                    continue;   // the chord, never more than the way round, is beyond what they can close
                SolvePair(world, scanSlot[a], scanSlot[b], nextScanMs);
            }
        }
    }

    /// <summary>Schedules a look at a pair at the first moment they could be in range before a time, if there is one.
    /// The event carries both rails' generations: either changing makes it stale (and solves the pair again).</summary>
    void SolvePair(World world, int a, int b, long untilMs) => LookAt(world, a, b, Trace(a, b, world.Time.Ms, untilMs));

    void LookAt(World world, int a, int b, long t)
    {
        if (t < 0)
            return;
        long other = ((long)Store[b].Entity << 32) | (uint)Store.RailGen(b);
        world.Schedule(new WorldTime(t), EventTier.Rails, Store[a].Entity, Id, ContactCheck, Store.RailGen(a), other);
    }

    /// <summary>From a time, steps two units' rails toward the moment their gap closes to the encounter range: each
    /// step as long as the gap could take to close at their speeds together, so none passes it. The time it closes
    /// (to a metre), the time it got to if it ran out of steps (no contact before it: look again from there), or -1
    /// if it doesn't close before <paramref name="untilMs"/>.</summary>
    long Trace(int a, int b, long fromMs, long untilMs)
    {
        double reach = settings.EncounterRangeM + unitRadius[a] + unitRadius[b];
        double v = Math.Abs(Store[a].Speed) + Math.Abs(Store[b].Speed);
        long t = fromMs;
        for (int i = 0; i < settings.ContactTraceSteps; i++)
        {
            Store.PoseAt(a, t, out var pa, out _);
            Store.PoseAt(b, t, out var pb, out _);
            double gap = Earth.Distance(pa, pb) - reach;
            if (gap <= ContactToleranceM)
                return t;
            if (v <= 0)
                return -1;   // neither moves
            t += Math.Max((long)Math.Floor(gap / v * 1000), 1);
            if (t > untilMs)
                return -1;
        }
        return t;
    }

    /// <summary>A pair's look comes due: in range, both come off rails with their groups (a contact); not yet (the solve
    /// ran out of steps), it's solved on from here.</summary>
    void CheckPair(World world, in SimEvent e)
    {
        int a = Store.SlotOf(e.Entity), b = Store.SlotOf((int)(e.A >> 32));
        if (a < 0 || b < 0 || !IsUnit(a) || !IsUnit(b) || Store.RailGen(a) != e.Generation || Store.RailGen(b) != (int)e.A)
            return;   // either has left its rails since (and was solved again if it laid new ones)
        long until = nextScanMs >= 0 ? nextScanMs : world.Time.Ms + settings.ContactScanMs;
        long t = Trace(a, b, world.Time.Ms, until);
        if (t == world.Time.Ms)
        {
            Derail(world, Store[a].Entity);
            Derail(world, Store[b].Entity);
        }
        else
            LookAt(world, a, b, t);
    }

    /// <summary>From the fine tick: a ship on it that has come within range of an opposing unit on rails takes that
    /// unit off them (a ship on the fine tick has no rails to solve against; it's checked as it goes instead).</summary>
    void FineContacts(World world)
    {
        if (!Opposed)
            return;
        EnsureRefs();
        long now = world.Time.Ms;
        for (int k = 0; k < Store.FineCount; k++)   // the ships on the fine tick (one a contact takes off rails joins the end)
        {
            int i = Store.FineSlot(k);
            var p = Store[i].Position;
            byte side = Store[i].Side;
            for (int j = 0; j < Store.Slots; j++)
            {
                if (refSide[j] == side || !IsUnit(j))
                    continue;
                double reach = settings.EncounterRangeM + unitRadius[j];
                double far = reach + refSpeed[j] * (now - refMs[j]) / 1000;
                if ((refPos[j] - p).LengthSquared > far * far)
                    continue;   // it can't have come that far since it was anchored
                if (Earth.Distance(p, Store[j].Position) <= reach)
                    Derail(world, Store[j].Entity);
            }
        }
    }

    /// <summary>Whether an opposing ship is within the encounter range and its margin of a ship (grown by a radius: a
    /// group's spread about its guide): if so, it stays on the fine tick.</summary>
    bool NearOpposing(int slot, double radiusM)
    {
        if (!Opposed)
            return false;
        var p = Store[slot].Position;
        byte side = Store[slot].Side;
        double reach = settings.EncounterRangeM + settings.EncounterMarginM + radiusM;
        long now = Store.NowMs;
        EnsureRefs();
        for (int j = 0; j < Store.Slots; j++)
        {
            if (!Store.IsAlive(j) || Store[j].Side == side)
                continue;
            if (Store.OnRails(j))
            {
                // on rails: within its unit's run since it was anchored, and its spread, of where that was
                int u = Store.RidingOn(j) is int g and >= 0 ? g : j;
                double far = reach + unitRadius[u] + refSpeed[u] * (now - refMs[u]) / 1000;
                if ((refPos[u] - p).LengthSquared > far * far)
                    continue;
            }
            else if ((Store[j].Position - p).LengthSquared > reach * reach)
                continue;   // the chord is never more than the way round
            if (Earth.Distance(p, Store[j].Position) <= reach)
                return true;
        }
        return false;
    }
}
