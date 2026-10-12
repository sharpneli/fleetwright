using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>What a captain has been told to do (docs/battle/plan.md step 2.3b): the first, trivial behaviours.</summary>
public enum CaptainOrder : byte
{
    /// <summary>No captain: the ship keeps whatever the player last ordered.</summary>
    None,

    /// <summary>Hold the course and speed the ship had when told.</summary>
    KeepCourse,

    /// <summary>Keep station astern of another ship at a distance, matching its speed.</summary>
    Follow,

    /// <summary>Turn away from another ship and run at flank speed.</summary>
    Run,

    /// <summary>Keep a station on another ship: a distance astern and a distance to starboard (negative: to port) of it,
    /// in its own frame. What an admiral gives his captains to hold a formation.</summary>
    Station,
}

/// <summary>The captains: one per ship that has been given a <see cref="CaptainOrder"/>, kept in entity order so they
/// think in the same order on every run. Each second (<see cref="SimSettings.CaptainPeriodMs"/>) a captain works out
/// the course and speed his order wants from his own ship and the picture, and orders them when they differ from
/// what's ordered by more than a deadband; so the helm and engines aren't hounded with orders every second. A captain
/// whose ship is gone is dropped; one whose target is gone keeps his course.</summary>
public sealed class CaptainLayer : IAiLayer
{
    struct Captain
    {
        public int Entity, Target;
        public CaptainOrder Order;
        public double DistanceM, AbeamM, HoldCourseDeg, HoldSpeedKn;
        public bool ByAdmiral;   // given by his group's admiral, who takes it back when the ship leaves the group
    }

    Captain[] captains = new Captain[16];
    int count;

    public int Count => count;
    public bool Active => count > 0;
    public int PeriodMs(in AiContext ctx)
    {
        for (int i = 0; i < count; i++)
            if (ctx.Ships.SlotOf(captains[i].Entity) is int slot and >= 0 && !ctx.Ships.OnRails(slot))
                return ctx.Settings.CaptainPeriodMs;
        return ctx.Settings.AiRailsPeriodMs;
    }

    /// <summary>A ship's captain's order and target, or None.</summary>
    public (CaptainOrder Order, int Target) Of(int entity)
    {
        int i = Find(entity);
        return i >= 0 ? (captains[i].Order, captains[i].Target) : (CaptainOrder.None, 0);
    }

    /// <summary>The player's order to a captain, as a command: B the order, A the target, X the distance astern, Y the
    /// distance abeam.</summary>
    public void Order(in AiContext ctx, in Command c) => Set(ctx, c.Entity, (CaptainOrder)c.B, (int)c.A, c.X, c.Y, byAdmiral: false);

    /// <summary>Gives a captain an order (<see cref="CaptainOrder.None"/> takes his away). An order the same as the one
    /// he has changes nothing, so an admiral can repeat his every second.</summary>
    public void Set(in AiContext ctx, int entity, CaptainOrder order, int target, double astern, double abeam, bool byAdmiral)
    {
        int slot = ctx.Ships.SlotOf(entity);
        int i = Find(entity);
        if (order == CaptainOrder.None || slot < 0)
        {
            if (i >= 0)
                Remove(i);
            return;
        }
        if (i >= 0)
        {
            ref var c = ref captains[i];
            if (c.Order == order && c.Target == target && c.DistanceM == astern && c.AbeamM == abeam && c.ByAdmiral == byAdmiral)
                return;
        }
        else
            i = Insert(entity);
        var ship = ctx.Ships[slot];
        captains[i] = new Captain
        {
            Entity = entity, Order = order, Target = target, DistanceM = astern, AbeamM = abeam, ByAdmiral = byAdmiral,
            HoldCourseDeg = ship.HeadingDeg, HoldSpeedKn = ship.SpeedOrderKn,
        };
    }

    /// <summary>The entity of the i-th captain (in entity order) if he holds an admiral's order, else 0: for the admiral
    /// to take back what's no longer his.</summary>
    public int AdmiralsAt(int i) => captains[i].ByAdmiral ? captains[i].Entity : 0;

    /// <summary>Whether a ship's captain holds an admiral's order.</summary>
    public bool IsAdmirals(int entity)
    {
        int i = Find(entity);
        return i >= 0 && captains[i].ByAdmiral;
    }

    public void Think(in AiContext ctx)
    {
        for (int i = 0; i < count; i++)
        {
            ref var cap = ref captains[i];
            int slot = ctx.Ships.SlotOf(cap.Entity);
            if (slot < 0)
            {
                Remove(i--);   // the ship is gone
                continue;
            }
            var own = ctx.Ships[slot];
            double course = cap.HoldCourseDeg, kn = cap.HoldSpeedKn;
            Sighting t = default;
            bool seen = cap.Order != CaptainOrder.KeepCourse && ctx.Picture.TryGet(cap.Target, out t);
            if (cap.Order == CaptainOrder.Follow && seen)
                (course, kn) = Behaviours.Follow(own, t, cap.DistanceM, ctx.Settings);
            else if (cap.Order == CaptainOrder.Station && seen)
                (course, kn) = Behaviours.Station(own, t, cap.DistanceM, cap.AbeamM, ctx.Settings);
            else if (cap.Order == CaptainOrder.Run && seen)
                (course, kn) = Behaviours.Run(own, t);
            else if (cap.Order != CaptainOrder.KeepCourse)
                (cap.HoldCourseDeg, cap.HoldSpeedKn, course, kn) = (own.CourseOrderDeg, own.SpeedOrderKn, own.CourseOrderDeg, own.SpeedOrderKn);
            Give(ctx, own, course, kn);
        }
    }

    /// <summary>Orders the course and speed if they differ from what the ship has been ordered.</summary>
    static void Give(in AiContext ctx, ShipState own, double course, double kn)
    {
        double dc = course - own.CourseOrderDeg;
        dc -= 360 * Math.Round(dc / 360);
        if (own.Helm != Helm.Course || Math.Abs(dc) > ctx.Settings.CaptainCourseDeadbandDeg)
            ctx.Orders.Course(own.Entity, course);
        if (Math.Abs(kn - own.SpeedOrderKn) > ctx.Settings.CaptainSpeedDeadbandKn)
            ctx.Orders.Speed(own.Entity, kn);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(count);
        for (int i = 0; i < count; i++)
        {
            ref readonly var c = ref captains[i];
            h.Add(c.Entity);
            h.Add((int)c.Order);
            h.Add(c.Target);
            h.Add(c.DistanceM);
            h.Add(c.AbeamM);
            h.Add(c.ByAdmiral);
            h.Add(c.HoldCourseDeg);
            h.Add(c.HoldSpeedKn);
        }
    }

    public void Fill(ShipsView v)
    {
        for (int k = 0; k < v.Count; k++)
        {
            int i = Find(v.Entity[k]);
            (v.Captain[k], v.CaptainTarget[k]) = i >= 0 ? ((byte)captains[i].Order, captains[i].Target) : ((byte)0, 0);
        }
    }

    int Find(int entity)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int e = captains[mid].Entity;
            if (e == entity)
                return mid;
            if (e < entity)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return -1;
    }

    int Insert(int entity)
    {
        if (count == captains.Length)
            Array.Resize(ref captains, captains.Length * 2);
        int i = count;
        while (i > 0 && captains[i - 1].Entity > entity)
        {
            captains[i] = captains[i - 1];
            i--;
        }
        captains[i] = new Captain { Entity = entity };
        count++;
        return i;
    }

    void Remove(int i)
    {
        Array.Copy(captains, i + 1, captains, i, count - i - 1);
        captains[--count] = default;
    }
}
