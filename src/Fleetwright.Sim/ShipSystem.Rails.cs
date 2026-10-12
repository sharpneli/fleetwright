using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The rails clock tier (docs/battle/plan.md 2.5): every rule for when ships go on rails, how, and what takes
/// them off, here and nowhere else. A ship on rails is a function of time, not stepped; the player shouldn't see it
/// happen, and the more ships are on rails the faster the clock runs, so these rules are tuned often. The numbers are
/// <see cref="SimSettings"/>' (Rails*, Encounter*, <see cref="SimSettings.PolarCapDeg"/>); the logic is this file's.
/// <list type="bullet">
/// <item><b>Units.</b> Ships go on and off rails in units: the line of the outermost group a ship moves with (its guide
/// first, on rails of its own; the rest riding it at their offsets), or a ship alone (or detached) by itself
/// (<see cref="UnitLine"/>, <see cref="RailsCheck"/>).</item>
/// <item><b>When.</b> Every <see cref="SimSettings.RailsCheckMs"/> of the fine tick, a unit off rails goes on when it's
/// <see cref="Ready"/>. With the clock at <see cref="SimSettings.RailsSnapMinSpeed"/> or more (<see cref="Placing"/>)
/// nobody watches a turn, so a unit is placed as its guide's orders say (<see cref="Placeable"/>); below it, it waits
/// until it's steaming steady (<see cref="Steady"/>), every ship with it (<see cref="InStation"/>). Either way, not
/// near an opposing ship (<see cref="NearOpposing"/>, ShipSystem.Contacts.cs).</item>
/// <item><b>How.</b> <see cref="Lay"/>: placed, the guide is made what its orders say and its ships put on their
/// stations; steady, they're taken on as they are.</item>
/// <item><b>Orders on rails.</b> While placing, a movement order to a unit's guide is carried out at once on rails
/// (<see cref="CanSnap"/>, <see cref="Snap"/>, which lays it again the same way); any other order takes the unit off
/// (<see cref="TakesOffRails"/>), as do changes to groups (<see cref="DerailFor"/>) and contacts.</item>
/// <item><b>Off.</b> <see cref="Derail"/>, the one way off for a unit, exactly where its rails had it; it wakes
/// whoever watches ships on the fine tick (the AI).</item>
/// </list>
/// <para>The store (<see cref="ShipStore"/>) carries out what's decided here (laying, re-anchoring at waypoints and
/// polar caps, taking off); the AI only reads whether a ship is on rails (captains don't steer a ship riding its
/// guide's rails; layers whose ships are all on rails think once a minute).</para></summary>
public sealed partial class ShipSystem
{
    // ------------------------------------------------------------------ the rules

    /// <summary>Whether rails are entered by placing, and orders on rails snap: the clock fast enough that nobody
    /// watches a turn play out.</summary>
    bool Placing(World world) => world.Speed >= settings.RailsSnapMinSpeed;

    /// <summary>Whether a command to a ship takes it, and its unit, off rails: every movement order, but not a
    /// setting.</summary>
    static bool TakesOffRails(ShipCommand code) => code != ShipCommand.Clearance;

    /// <summary>Whether an order is carried out at once on rails: a course, a speed ahead or a route, to a ship on rails
    /// of its own (alone, or a group's guide), while placing.</summary>
    bool CanSnap(World world, int slot, ShipCommand code, double x) =>
        code is ShipCommand.Course or ShipCommand.Speed or ShipCommand.ClearRoute or ShipCommand.AddWaypoint or ShipCommand.LoopRoute &&
        (code != ShipCommand.Speed || x >= 0) && Store.OnRails(slot) && !Store.RidesAnother(slot) && Placing(world);

    /// <summary>Whether the unit in <see cref="line"/> (its guide off rails) may go on rails now: placing, as its
    /// guide's orders say; else steaming steady, every ship with it. And no opposing ship near it, as far as its ships
    /// reach (placed, as far as their stations do: they jump there).</summary>
    bool Ready(int guide, bool placing)
    {
        if (placing ? !Placeable(guide) : !Steady(guide) || !InStation(guide))
            return false;
        return !NearOpposing(guide, placing ? Math.Max(GroupRadius(guide), FormationRadius()) : GroupRadius(guide));
    }

    /// <summary>Whether a ship may be put on rails as its orders say, steady or not: steered on a course or a route,
    /// its engines ahead or stopped. (Damage, when it comes, will keep a ship off: a flooding or burning ship is
    /// coarse, 4.8.)</summary>
    bool Placeable(int slot)
    {
        var s = Store[slot];
        return (s.Helm == Helm.Course || s.Helm == Helm.Route && s.RouteNext < s.RouteCount) && s.PowerOrder >= 0;
    }

    /// <summary>Steaming steady: on a course or a route's leg and on it, rudder amidships, no swing, the engines at what
    /// was rung and the speed what they give.</summary>
    bool Steady(int slot)
    {
        var s = Store[slot];
        if (s.Helm is not (Helm.Course or Helm.Route) || !NotSwinging(s) || s.Power < 0 || s.Power != s.PowerOrder)
            return false;
        double want = s.Helm == Helm.Course ? s.CourseOrderDeg : Earth.Bearing(s.Position, s.Waypoint(s.RouteNext));
        if (Math.Abs(AngleDeg(want - s.HeadingDeg)) > settings.RailsMaxCourseErrDeg)
            return false;
        return Math.Abs(s.Speed - SteadySpeed(s)) <= settings.RailsMaxSpeedErrKn * Handling.MsPerKnot;
    }

    /// <summary>Every other ship of the unit in <see cref="line"/> with its guide: not swinging, on the guide's heading
    /// and speed near enough (captains re-order only past their deadbands, so a ship in station may sit a little off;
    /// one catching up runs knots faster, and isn't taken onto rails at the wrong offset), and in station (rails hold
    /// its offset as it is).</summary>
    bool InStation(int guide)
    {
        var gs = Store[guide];
        var gp = gs.Position;
        var starboard = Vec3d.Cross(gs.Forward, gp.Normalized());
        double speed = SteadySpeed(gs);
        for (int i = 1; i < line.Count; i++)
        {
            int m = Store.SlotOf(line[i].Ship);
            if (m < 0)
                return false;
            var ms = Store[m];
            var (_, astern, abeam) = line[i];
            if (!NotSwinging(ms) || Math.Abs(AngleDeg(ms.HeadingDeg - gs.HeadingDeg)) > settings.RailsGroupHeadingDeg ||
                Math.Abs(ms.Speed - speed) > settings.RailsGroupSpeedErrKn * Handling.MsPerKnot ||
                Earth.Distance(ms.Position, gp - gs.Forward * astern + starboard * abeam) > settings.RailsGroupStationM)
                return false;
        }
        return true;
    }

    bool NotSwinging(ShipState s) =>
        Math.Abs(s.YawRate) * 180 / Math.PI <= settings.RailsMaxYawRateDegS && Math.Abs(s.Rudder) * 180 / Math.PI <= settings.RailsMaxRudderDeg;

    static double AngleDeg(double d) => d - 360 * Math.Round(d / 360);

    /// <summary>The speed a ship's power holds it at, m/s: what it's taken onto rails at.</summary>
    static double SteadySpeed(ShipState s) => s.Handling.SteadySpeedMs(s.Power);

    // ------------------------------------------------------------------ on

    /// <summary>Every few seconds, each unit off rails that's <see cref="Ready"/> goes on: groups' lines (with the
    /// outermost force whose line they're in), then ships alone or detached.</summary>
    void RailsCheck(World world)
    {
        bool placing = Placing(world);
        var groups = world.Groups;
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (groups.InForceLine(g))
                continue;   // carried with its force
            int guide = Store.SlotOf(groups.GuideShip(g));
            if (guide < 0 || Store.OnRails(guide))
                continue;
            groups.LineOffsets(g, line);
            if (Ready(guide, placing))
                Lay(world, guide, placing);
        }
        for (int i = 0; i < Store.Slots; i++)
        {
            if (!Store.IsAlive(i) || Store.OnRails(i) || groups.InLine(Store[i].Entity))
                continue;
            Alone(i);
            if (Ready(i, placing))
                Lay(world, i, placing);
        }
    }

    /// <summary>The unit a ship guides into <see cref="line"/>: its outermost group's line if it leads it, else the
    /// ship alone.</summary>
    void UnitLine(World world, int slot)
    {
        int entity = Store[slot].Entity;
        if (world.Groups.UnitOf(entity) is { } u)
        {
            world.Groups.LineOffsets(u, line);
            if (line.Count > 0 && line[0].Ship == entity)
                return;
        }
        Alone(slot);
    }

    void Alone(int slot)
    {
        line.Clear();
        line.Add((Store[slot].Entity, 0, 0));
    }

    /// <summary>The unit in <see cref="line"/> on rails, its guide on its own. Placed, the guide is made what its orders
    /// say (its course, or for its next waypoint, and the speed its power holds) and its ships put on their stations;
    /// steady, they're taken on as they are.</summary>
    void Lay(World world, int guide, bool placed)
    {
        if (placed)
            Store.Settle(guide);
        RailGuide(world, guide);
        for (int i = 1; i < line.Count; i++)
        {
            int m = Store.SlotOf(line[i].Ship);
            if (m < 0)
                continue;
            var (_, astern, abeam) = line[i];
            if (placed)
                Store.RailAt(m, guide, -astern, abeam);
            else
                Store.RailWith(m, guide);
        }
        Anchored(world, guide);
    }

    /// <summary>An order carried out at once on rails (the ship off them for the moment, its unit riding it as it is):
    /// laid again, placed, so it takes its new course (or heads for its next waypoint) and speed where it is, and its
    /// unit's ships jump to their stations on the new heading.</summary>
    void Snap(World world, int slot)
    {
        UnitLine(world, slot);
        Lay(world, slot, placed: true);
    }

    /// <summary>How far the stations in <see cref="line"/> reach from its guide, metres.</summary>
    double FormationRadius()
    {
        double r = 0;
        for (int i = 1; i < line.Count; i++)
        {
            var (_, astern, abeam) = line[i];
            r = Math.Max(r, Math.Sqrt(astern * astern + abeam * abeam));
        }
        return r;
    }

    /// <summary>How far the ships in <see cref="line"/> are from its guide, at most, metres.</summary>
    double GroupRadius(int guide)
    {
        double r = 0;
        var gp = Store[guide].Position;
        for (int i = 1; i < line.Count; i++)
            if (Store.SlotOf(line[i].Ship) is int m and >= 0)
                r = Math.Max(r, Earth.Distance(gp, Store[m].Position));
        return r;
    }

    // ------------------------------------------------------------------ the rails themselves: legs, waypoints, polar caps

    /// <summary>A ship on its own rails: along its ordered course, or, on a route, straight for its next waypoint, with
    /// the waypoint's arrival as an event.</summary>
    void RailGuide(World world, int slot)
    {
        var s = Store[slot];
        // a course held is a rhumb line at the ordered course exactly (the heading is within a fraction of a degree of
        // it: rails turn at once, and a small error held for days would be miles), and at the speed the engines settle
        // at (within a tenth of a knot: held for days, the rest would be miles); inside a polar cap, a great circle
        bool polar = Earth.InPolarCap(s.Position, settings.PolarCapDeg);
        Store.Rail(slot, s.Helm == Helm.Course && !polar ? s.CourseOrderDeg : double.NaN, SteadySpeed(s));
        if (s.Helm == Helm.Course)
            SchedulePolar(world, slot);
        if (s.Helm == Helm.Route && s.RouteNext < s.RouteCount)
        {
            var p = s.Position;
            var wp = s.Waypoint(s.RouteNext);
            Store.ReanchorLeg(slot, p, Toward(p, wp));
            ScheduleArrival(world, slot, wp);
        }
    }

    /// <summary>A ship's rails come to a waypoint or a polar cap's edge: re-anchored there, and its contacts solved
    /// again. Stale (derailed or re-anchored since) it's nothing.</summary>
    void RailsEvent(World world, in SimEvent e)
    {
        int slot = Store.SlotOf(e.Entity);
        if (slot < 0 || !Store.OnRails(slot) || Store.RailGen(slot) != e.Generation)
            return;
        if (e.Code == RailsWaypoint)
        {
            if (Store.PassWaypoint(slot, out var next))
                ScheduleArrival(world, slot, next);   // it turns there at once for the next
            else
                SchedulePolar(world, slot);   // past the last, it holds the course it arrived on
        }
        else
        {
            if (e.Code == PolarEntry)
                Store.EnterPolarCap(slot);
            else
                Store.LeavePolarCap(slot);
            SchedulePolar(world, slot);
        }
        Anchored(world, slot);
    }

    void ScheduleArrival(World world, int slot, Vec3d wp)
    {
        var s = Store[slot];
        if (s.Speed <= 0)
            return;
        long ms = (long)Math.Ceiling(Earth.Distance(s.Position, wp) / s.Speed * 1000);
        world.Schedule(world.Time + Math.Max(ms, 1), EventTier.Rails, s.Entity, Id, RailsWaypoint, Store.RailGen(slot));
    }

    /// <summary>For a ship on its own rails holding a course: when its rhumb line reaches a polar cap, or, inside one,
    /// when its great circle leaves it (solved in closed form), as an event.</summary>
    void SchedulePolar(World world, int slot)
    {
        var s = Store[slot];
        if (s.Speed <= 0 || s.Helm != Helm.Course)
            return;
        var p = s.Position;
        double cap = settings.PolarCapDeg * Math.PI / 180, dist;
        ushort code;
        if (Store.OnRhumb(slot))
        {
            // the latitude changes linearly along a rhumb line: d lat / ds = cos(course) / R
            double c = Math.Cos(s.CourseOrderDeg * Math.PI / 180), lat = Math.Asin(p.Z / p.Length);
            if (Math.Abs(c) < 1e-9)
                return;
            dist = ((c > 0 ? cap : -cap) - lat) / c * Earth.Radius;
            code = PolarEntry;
        }
        else
        {
            // on the great circle p cos u + f sin u (u = s/R), the height toward the pole is A cos(u - phi), with
            // A = |(pz, fz)| and phi = atan2(fz, pz); it falls back to the cap's edge at u = phi + acos(edge / A)
            var up = p.Normalized();
            var f = s.Forward;
            double sign = Math.Sign(up.Z), pz = up.Z * sign, fz = f.Z * sign, edge = Math.Sin(cap);
            double a = Math.Sqrt(pz * pz + fz * fz);
            dist = Earth.Radius * (Math.Atan2(fz, pz) + Math.Acos(Math.Min(edge / a, 1)));
            code = PolarExit;
        }
        if (!(dist > 0))
            return;
        long ms = (long)Math.Ceiling(dist / s.Speed * 1000);
        world.Schedule(world.Time + Math.Max(ms, 1), EventTier.Rails, s.Entity, Id, code, Store.RailGen(slot));
    }

    static Vec3d Toward(Vec3d from, Vec3d to)
    {
        var up = from.Normalized();
        var d = to - from;
        return (d - up * Vec3d.Dot(d, up)).Normalized();
    }

    // ------------------------------------------------------------------ off

    /// <summary>Any ship that isn't on rails: the fine tick runs only while there is.</summary>
    bool AnyFine() => Store.FineCount > 0;

    /// <summary>Takes a ship off rails, and the line of the outermost group it moves with (they ride together), back
    /// to the fine tick, exactly where the rails had them. A detached member rides rails of its own, so it comes off
    /// alone. Whoever watches ships on the fine tick wakes (a contact isn't a command).</summary>
    public void Derail(World world, int entity)
    {
        bool any = false;
        if (world.Groups.UnitOf(entity) is { } u)
        {
            world.Groups.LineOffsets(u, derailing);
            for (int i = 0; i < derailing.Count; i++)
                any |= DerailSlot(Store.SlotOf(derailing[i].Ship));
        }
        else
            any = DerailSlot(Store.SlotOf(entity));
        if (any)
        {
            EnsureTick(world);
            world.OffRails();
        }
    }

    bool DerailSlot(int slot)
    {
        if (slot < 0 || !Store.OnRails(slot))
            return false;
        Store.Derail(slot);
        return true;
    }

    /// <summary>What a change to groups takes off rails, before it's made: whatever changes comes off whole, the ship's
    /// unit, the group's own line and the force's it joins or leaves.</summary>
    internal void DerailFor(World world, in Command c)
    {
        var groups = world.Groups;
        var code = (GroupCommand)c.Code;
        if (c.Entity != 0)
            Derail(world, c.Entity);
        switch (code)
        {
            case GroupCommand.SetFormation or GroupCommand.Disband or GroupCommand.Add or GroupCommand.Form or GroupCommand.Exit or GroupCommand.DetachGroup:
                DerailGroup(world, groups.Get((int)c.A));
                break;
            case GroupCommand.Enter or GroupCommand.EnterAlongside:
                DerailGroup(world, groups.Get((int)c.A));
                DerailGroup(world, groups.Get((int)c.Y));
                break;
            case GroupCommand.Join when c.A != 0:
                Derail(world, (int)c.A);
                break;
            case GroupCommand.Detach when groups.Of(c.Entity) is { } g:
                DerailGroup(world, g);   // a ship rejoining is on rails of its own: its line comes off too
                break;
            case GroupCommand.CreateOfNewest:
                // the ships it takes in: those still at sea of the first one's side
                int side = -1;
                for (int e = Math.Max(NewestEntity - (int)c.A + 1, 1); e <= NewestEntity; e++)
                {
                    int slot = Store.SlotOf(e);
                    if (slot < 0 || side >= 0 && Store[slot].Side != side)
                        continue;
                    side = Store[slot].Side;
                    Derail(world, e);
                }
                break;
        }
    }

    void DerailGroup(World world, GroupSystem.Group? g)
    {
        if (g == null)
            return;
        var groups = world.Groups;
        if (groups.GuideShip(groups.TopOf(g)) is int s and > 0)
            Derail(world, s);
        if (g.Parent != 0 && groups.Get(g.Parent) is { } f && groups.GuideShip(groups.TopOf(f)) is int fs and > 0)
            Derail(world, fs);   // a detached group rejoining: the line it joins comes off too
    }
}
