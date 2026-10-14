using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The clock tiers (docs/battle/plan.md 2.5 and 2.8, docs/world/architecture.md "The speed picks the tier"):
/// every rule for which tier a ship is in, when it goes on rails, how, and what takes it off, here and nowhere else. A
/// ship on rails is a function of time, not stepped; the player shouldn't see it happen, and the more ships are on
/// rails the faster the clock runs, so these rules are tuned often. Off rails, the asked speed picks the tier: coarse
/// at <see cref="SimSettings.CoarseMinSpeed"/> and up, fine below (<see cref="CoarseNow"/>). The numbers are
/// <see cref="SimSettings"/>' (Rails*, Coarse*, Encounter*, <see cref="SimSettings.PolarCapDeg"/>); the logic is
/// this file's.
/// <list type="bullet">
/// <item><b>Coarse.</b> A coarse ship is on segments of kinematic motion (CoarseMotion), laid from its orders as each
/// ends and as it's ordered (<see cref="PlanCoarse"/>); a group's ships ride their guide while they're in station, and
/// steer back to it by their captains when they're not (<see cref="RideCheck"/>); as the guide turns, those abeam of its
/// track come off to turn together, and only its line ahead rides on (<see cref="RidesThroughTurn"/>). Everything the fine tick looks at
/// every few seconds is looked at once a coarse step (<see cref="CoarseStepEvent"/>). The asked speed crossing the
/// threshold moves every ship off rails across at that moment, where it is (<see cref="SpeedChanged"/>); coming off
/// rails, a ship goes to the tier the speed asks for (<see cref="DerailSlot"/>).</item>
/// <item><b>Units.</b> Ships go on and off rails in units: the line of the outermost group a ship moves with (its guide
/// first, on rails of its own; the rest riding it at their offsets), or a ship alone (or detached) by itself
/// (<see cref="UnitLine"/>, <see cref="RailsCheck"/>).</item>
/// <item><b>When.</b> Every <see cref="SimSettings.RailsCheckMs"/> of the fine tick (or each coarse step), a unit off
/// rails goes on when it's <see cref="Ready"/>. Where nobody would see a turn (<see cref="Placing"/>: the clock at
/// <see cref="SimSettings.RailsSnapMinSpeed"/> or more, and fast enough that its line ahead's turn in succession takes
/// about a frame), it's placed as its guide's orders say
/// (<see cref="Placeable"/>); else it waits until it's steaming steady (<see cref="Steady"/>), every ship with it
/// (<see cref="InStation"/>), so a formation is never seen to snap into place. Either way, not near an opposing ship (<see cref="NearOpposing"/>,
/// ShipSystem.Contacts.cs) nor in an encounter (<see cref="InEncounter"/>, held until it closes), no ship of it with a battery engaged
/// (<see cref="Engaged"/>), and no ship of it sliding along a coast (<see cref="Sliding"/>: the slide is the fine
/// tier's, docs/world/terrain-plan.md; rails don't know the land until its step S2).</item>
/// <item><b>Nothing here reads the map.</b> The sim is independent of display state, a hard rule with no exceptions
/// (docs/world/architecture.md, "Terms"): the map's scale used to place a unit drawn as one marker at any speed, and
/// went once coarse made forming up cheap at speed (docs/battle/plan.md step 2.8e). Don't bring it back.</item>
/// <item><b>How.</b> <see cref="Lay"/>: placed, the guide is made what its orders say and its ships put on their
/// stations; steady, they're taken on as they are.</item>
/// <item><b>Orders on rails.</b> While its unit is placing, a movement order to a unit's guide is carried out at once on rails
/// (<see cref="CanSnap"/>, <see cref="Snap"/>, which lays it again the same way); any other order takes the unit off
/// (<see cref="TakesOffRails"/>), as do changes to groups (<see cref="DerailFor"/>) and contacts.</item>
/// <item><b>The land</b> (docs/world/terrain-plan.md S2, S3). When a unit's rails are laid or anchored again, its track
/// is traced for the coast (<see cref="WatchLand"/>). Where nobody sees a turn (<see cref="SlidesOnRails"/>: where it's
/// <see cref="Placing"/>; the clock only, never the map's scale) its rails slide along
/// the coast as the fine tick's helmsman would (<see cref="SlideStep"/>: the same feeler, at the rails' pose, legs of
/// it with instant turns), only its guide kept off the land (a formation is its marker there, the user's 2026-10-13).
/// Slower, it comes off short of the coast (a <see cref="LandAhead"/> event) so the fine tick's slide takes it along,
/// and goes on rails only with its track clear for a while (<see cref="LandClear"/>) and no ship of it sliding
/// (<see cref="Sliding"/>).</item>
/// <item><b>Off.</b> <see cref="Derail"/>, the one way off for a unit, exactly where its rails had it; it wakes
/// whoever watches ships on the fine tick (the AI).</item>
/// </list>
/// <para>The store (<see cref="ShipStore"/>) carries out what's decided here (laying, re-anchoring at waypoints and
/// polar caps, taking off); the AI only reads whether a ship is on rails (captains don't steer a ship riding its
/// guide's rails; layers whose ships are all on rails think once a minute).</para></summary>
public sealed partial class ShipSystem
{
    // ------------------------------------------------------------------ the rules

    /// <summary>Whether a unit goes on rails by placing, and orders to it on rails snap: where nobody would see the turn
    /// or the forming up that's skipped, the clock so fast that a manoeuvre takes about a frame or less. A ship's turn
    /// does from <see cref="SimSettings.RailsSnapMinSpeed"/>; a line ahead's turn in succession lasts until its last
    /// ship in the guide's wake reaches where the guide turned, so it must fit in <see cref="SimSettings.RailsFrameS"/>
    /// too (the user, 2026-10-13: a column's turns looked nice at 3,000x and snapped at 10,000x). Slower, it turns in
    /// succession coarse, riding its guide's track.</summary>
    bool Placing(World world, int guide)
    {
        if (world.Speed < settings.RailsSnapMinSpeed)
            return false;
        double wake = WakeM(world, guide);
        if (wake <= 0)
            return true;
        var s = Store[guide];
        double v = Math.Max(Math.Abs(s.Speed), s.Handling.SteadySpeedMs(Math.Max(s.PowerOrder, 0)));
        return v > 0 && wake / v <= world.Speed * settings.RailsFrameS;
    }

    /// <summary>How far astern of a unit's guide its line ahead reaches, metres: its farthest station in the guide's wake
    /// (as the coarse tier rides them through a turn, <see cref="RidesThroughTurn"/>); 0 for a ship alone.</summary>
    double WakeM(World world, int guide)
    {
        int entity = Store[guide].Entity;
        if (world.Groups.UnitOf(entity) is not { } u || world.Groups.GuideShip(u) != entity)
            return 0;
        world.Groups.LineOffsets(u, riders);
        double wake = 0;
        for (int i = 1; i < riders.Count; i++)
            if (Math.Abs(riders[i].AbeamM) <= settings.RailsGroupStationM)
                wake = Math.Max(wake, riders[i].AsternM);
        return wake;
    }

    /// <summary>Whether a ship riding its guide stays on it through the guide's turn below the snap speed: in its line
    /// ahead (within <see cref="SimSettings.RailsGroupStationM"/> of its track), where riding turns it in succession as
    /// a column turns. Abeam of it, riding would swing it round the guide as one rigid line (the outer wing at many
    /// times the guide's speed, the inner one backward): it comes off to turn together, as the fine tick's captains
    /// turn a line abreast, and rides again once steady in station (<see cref="RideCheck"/>, the rails check).</summary>
    bool RidesThroughTurn(int slot) => Store.RailAbeamM(slot) <= settings.RailsGroupStationM;

    /// <summary>Whether a command to a ship takes it, and its unit, off rails: every movement order, but not a
    /// setting.</summary>
    static bool TakesOffRails(ShipCommand code) => code != ShipCommand.Clearance;

    /// <summary>Whether an order is carried out at once on rails: a course, a speed ahead or a route, to a ship on rails
    /// of its own (alone, or a group's guide), while its unit is <see cref="Placing"/>.</summary>
    bool CanSnap(World world, int slot, ShipCommand code, double x)
    {
        if (code is not (ShipCommand.Course or ShipCommand.Speed or ShipCommand.ClearRoute or ShipCommand.AddWaypoint or ShipCommand.LoopRoute) ||
            code == ShipCommand.Speed && x < 0 || !Store.OnRails(slot) || Store.RidesAnother(slot))
            return false;
        return Placing(world, slot);
    }

    /// <summary>Whether the unit in <see cref="line"/> (its guide off rails) may go on rails now: placing, as its
    /// guide's orders say; else steaming steady, every ship with it. No ship of it sliding along a coast, and no
    /// opposing ship near it, as far as its ships reach (placed, as far as their stations do: they jump there).</summary>
    bool Ready(World world, int guide, bool placing)
    {
        if (placing ? !Placeable(guide) : !Steady(guide) || !InStation(guide))
            return false;
        if (!SlidesOnRails(world, guide) && (Sliding() || !LandClear(world, guide)))
            return false;
        if (InEncounter() || Engaged())
            return false;
        return !NearOpposing(guide, placing ? Math.Max(GroupRadius(guide), FormationRadius()) : GroupRadius(guide));
    }

    /// <summary>Whether a ship may be put on rails as its orders say, steady or not: steered on a course or a route,
    /// its engines ahead or stopped, its rudder answering the helm, dry and not burning (damage, docs/battle/fighting.md
    /// 4.3a and 4.3b: a jammed or lost rudder keeps it off, and so does water in it or a fire, whose steps change it).</summary>
    bool Placeable(int slot)
    {
        var s = Store[slot];
        return (s.Helm == Helm.Course || s.Helm == Helm.Route && s.RouteNext < s.RouteCount) && s.PowerOrder >= 0 && s.Steering == Steering.Free &&
               !Wet(slot);
    }

    /// <summary>Water in it or a fire aboard (docs/battle/fighting.md 4.3b): its flooding and fire step it, so it stays off
    /// rails.</summary>
    bool Wet(int slot) => Store.Combat.Condition(slot) is { Damaged: true } k && (k.NextStepMs >= 0 || k.WaterT > 0);

    /// <summary>Steaming steady: on a course or a route's leg and on it, rudder amidships, no swing, the engines at what
    /// was rung and the speed what they give; dry and not burning.</summary>
    bool Steady(int slot)
    {
        var s = Store[slot];
        bool coarse = s.Tier == ClockTier.Coarse;   // coarse: its manoeuvre done (its power isn't stepped)
        if (s.Helm is not (Helm.Course or Helm.Route) || s.Steering != Steering.Free || Wet(slot) || (coarse ? !Store.CoarseSteady(slot) || s.PowerOrder < 0 : !NotSwinging(s) || s.Power < 0 || s.Power != s.PowerOrder))
            return false;
        double want = s.Helm == Helm.Course ? s.CourseOrderDeg : Earth.Bearing(s.Position, s.Waypoint(s.RouteNext));
        if (Math.Abs(AngleDeg(want - s.HeadingDeg)) > settings.RailsMaxCourseErrDeg)
            return false;
        double steady = coarse ? s.Handling.SteadySpeedMs(s.PowerOrder) : SteadySpeed(s);
        return Math.Abs(s.Speed - steady) <= settings.RailsMaxSpeedErrKn * Handling.MsPerKnot;
    }

    /// <summary>Every other ship of the unit in <see cref="line"/> with its guide: not swinging, on the guide's heading
    /// and speed near enough (captains re-order only past their deadbands, so a ship in station may sit a little off;
    /// one catching up runs knots faster, and isn't taken onto rails at the wrong offset), and in station (rails hold
    /// its offset as it is).</summary>
    bool InStation(int guide)
    {
        var gs = Store[guide];
        double speed = gs.Tier == ClockTier.Coarse ? gs.Speed : SteadySpeed(gs);
        for (int i = 1; i < line.Count; i++)
        {
            int m = Store.SlotOf(line[i].Ship);
            if (m < 0 || Wet(m) || !MemberInStation(gs, Store[m], speed, line[i].AsternM, line[i].AbeamM))
                return false;
        }
        return true;
    }

    /// <summary>A group's ship with its guide: not swinging, on its heading and speed near enough, and within
    /// <see cref="SimSettings.RailsGroupStationM"/> of its station (metres astern and abeam).</summary>
    bool MemberInStation(ShipState gs, ShipState ms, double speed, double astern, double abeam)
    {
        var gp = gs.Position;
        var starboard = Vec3d.Cross(gs.Forward, gp.Normalized());
        return NotSwinging(ms) && Math.Abs(AngleDeg(ms.HeadingDeg - gs.HeadingDeg)) <= settings.RailsGroupHeadingDeg &&
               Math.Abs(ms.Speed - speed) <= settings.RailsGroupSpeedErrKn * Handling.MsPerKnot &&
               Earth.Distance(ms.Position, gp - gs.Forward * astern + starboard * abeam) <= settings.RailsGroupStationM;
    }

    /// <summary>Whether a ship of the unit in <see cref="line"/> has a battery engaged (docs/battle/fighting.md 4.2b): fire
    /// control is the fine tick's, so an engaged ship stays off rails.</summary>
    bool Engaged()
    {
        for (int i = 0; i < line.Count; i++)
            if (Store.SlotOf(line[i].Ship) is int m and >= 0 && Store.Combat.Engaged(m) > 0)
                return true;
        return false;
    }

    /// <summary>Whether a ship of the unit in <see cref="line"/> is sliding along a coast: placed on rails, it would be
    /// put back on its order, into the land.</summary>
    bool Sliding()
    {
        for (int i = 0; i < line.Count; i++)
            if (Store.SlotOf(line[i].Ship) is int m and >= 0 && Store[m].LandSide != 0)
                return true;
        return false;
    }

    /// <summary>Whether a unit's rails slide along coasts (S3) rather than come off short of them: where nobody would see
    /// the slide's turns, which are instant on rails: where it's <see cref="Placing"/> (else coarse or fine slides).</summary>
    bool SlidesOnRails(World world, int guide) => world.Shore != null && Placing(world, guide);

    /// <summary>Whether the track the unit in <see cref="line"/> would take on rails (its guide's orders: the course as a
    /// rhumb line, or the leg to its next waypoint) keeps its standoff and its widest station abeam off the coast for
    /// <see cref="SimSettings.RailsLandOnFactor"/> times the distance it would come off rails short of it.</summary>
    bool LandClear(World world, int guide)
    {
        if (world.Shore is not { } shore)
            return true;
        double abeam = 0;
        for (int i = 1; i < line.Count; i++)
            abeam = Math.Max(abeam, Math.Abs(line[i].AbeamM));
        var s = Store[guide];
        double c = LandContact(shore, guide, abeam, out _);
        return c >= settings.RailsLandOnFactor * LandMargin(s, SteadySpeed(s));
    }

    /// <summary>How far short of the coast a unit comes off rails, metres: its guide's look-ahead at a speed, and
    /// <see cref="SimSettings.RailsLandMarginM"/>.</summary>
    double LandMargin(ShipState s, double speed) => Slide.LookaheadM(s.Handling, speed, s.CourseRudder, settings) + settings.RailsLandMarginM;

    /// <summary>How far along its orders' track from where it is a ship first comes within its standoff and
    /// <paramref name="abeamM"/> of the coast, metres; +inf if not within the horizon or (a route) its next waypoint,
    /// <paramref name="whole"/> saying it looked all the way to that waypoint. A course is a rhumb line; a route's leg,
    /// or a course inside a polar cap, a great circle: as on rails.</summary>
    double LandContact(Shore shore, int slot, double abeamM, out bool whole)
    {
        var s = Store[slot];
        double clear = s.Handling.StandoffM + abeamM, horizon = settings.RailsLandHorizonM;
        var p = s.Position;
        whole = false;
        if (s.Helm == Helm.Route && s.RouteNext < s.RouteCount)
        {
            var wp = s.Waypoint(s.RouteNext);
            double len = Earth.Distance(p, wp);
            whole = len <= horizon;
            return len < 1 ? double.PositiveInfinity : shore.FirstContact(p, Toward(p, wp), Math.Min(len, horizon), clear);
        }
        if (s.Helm == Helm.Course && !Earth.InPolarCap(p, settings.PolarCapDeg))
            return shore.FirstContactRhumb(p, s.CourseOrderDeg, horizon, clear);
        return shore.FirstContact(p, s.Forward, horizon, clear);
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
            bool placing = Placing(world, guide);
            if (Ready(world, guide, placing))
                Lay(world, guide, placing);
        }
        for (int i = 0; i < Store.Slots; i++)
        {
            if (!Store.IsAlive(i) || Store.OnRails(i) || groups.InLine(Store[i].Entity))
                continue;
            Alone(i);
            bool placing = Placing(world, i);
            if (Ready(world, i, placing))
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
        for (int i = 0; i < line.Count; i++)
            if (Store.SlotOf(line[i].Ship) is int c and >= 0 && Store.TierOf(c) == ClockTier.Coarse)
                Store.Derail(c);   // from coarse: handed to the fine tick where it is, for rails to take from there
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
        WatchLand(world, guide);
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
        if (slot < 0 || !Store.Anchored(slot) || Store.RidesAnother(slot) || Store.RailGen(slot) != e.Generation)
            return;   // stale; a coarse ship of its own gets its polar caps' (it lays its waypoints itself)
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
        WatchLand(world, slot);
    }

    /// <summary>A unit's rails were laid or anchored again: its track traced for the coast, at its guide's standoff and
    /// its widest station abeam. Where it comes within that, a <see cref="LandAhead"/> event takes it off rails short of
    /// it (at once, if it's that close already); a course traced only as far as the horizon is traced again from there
    /// (<see cref="LandRecheck"/>); a leg clear to its waypoint is looked at again there.</summary>
    void WatchLand(World world, int guide)
    {
        if (world.Shore is not { } shore || !Store.OnRails(guide) || Store.RidesAnother(guide))
            return;
        var s = Store[guide];
        if (s.Speed <= 0)
            return;
        bool slide = SlidesOnRails(world, guide);
        if (slide && s.LandSide != 0)
        {
            SlideStep(world, guide);   // sliding already (laid from the fine tick's slide): on along the coast
            return;
        }
        // sliding, only the guide is kept off the land, from where its feeler would start to slide; else the unit,
        // its widest station too, comes off a margin short of that
        double c = LandContact(shore, guide, slide ? 0 : UnitAbeam(world, guide), out bool whole), at;
        double margin = slide ? Slide.LookaheadM(s.Handling, s.Speed, s.CourseRudder, settings) : LandMargin(s, s.Speed);
        ushort code;
        if (c < double.PositiveInfinity)
        {
            if (c <= margin)
            {
                if (slide)
                    SlideStep(world, guide);
                else
                    Derail(world, s.Entity);
                return;
            }
            (code, at) = (LandAhead, c - margin);
        }
        else if (!whole)
            (code, at) = (LandRecheck, Math.Max(settings.RailsLandHorizonM - margin, margin));
        else
            return;
        long ms = (long)Math.Ceiling(at / s.Speed * 1000);
        world.Schedule(world.Time + Math.Max(ms, 1), EventTier.Rails, s.Entity, Id, code, Store.RailGen(guide));
    }

    /// <summary>A unit's rails come short of the coast (off rails, to the fine tick's slide) or to the end of what was
    /// traced (traced again). Stale (derailed or anchored again since) it's nothing.</summary>
    void LandEvent(World world, in SimEvent e)
    {
        int slot = Store.SlotOf(e.Entity);
        if (slot < 0 || !Store.OnRails(slot) || Store.RailGen(slot) != e.Generation)
            return;
        if (e.Code == LandRecheck)
            WatchLand(world, slot);
        else if (SlidesOnRails(world, slot))
            SlideStep(world, slot);
        else
            Derail(world, e.Entity);   // slower now (or it always was): the fine tick's slide takes it along
    }

    /// <summary>A unit on rails near the coast where it slides on rails (S3): its guide's feeler (<see cref="Slide.Feel"/>,
    /// the fine tick's own) asked at the rails' pose. Sliding, its rails are anchored again along the slide's course
    /// (an instant turn), as far as that's clear, half the way to where the leg would come within its minimum, and at
    /// most <see cref="SimSettings.SlideRailsStepM"/>; there it's asked again. Let go, its rails are laid on its orders
    /// again, and the land watched from there. A route's waypoint near the slide is passed as the fine tick passes it
    /// (inside the arrival radius, or abeam close by), without moving the ship onto it.</summary>
    void SlideStep(World world, int slot)
    {
        var shore = world.Shore!;
        var s = Store[slot];
        var h = s.Handling;
        Store.PoseNow(slot, out var p, out var f);
        double radius = h.RadiusFor(s.CourseRudder);
        while (s.Helm == Helm.Route && s.RouteNext < s.RouteCount)
        {
            var wp = s.Waypoint(s.RouteNext);
            double dist = Earth.Distance(p, wp);
            if (dist > settings.ArrivalRadiusM && (Vec3d.Dot(f, wp - p) > 0 || dist >= 2 * radius))
                break;
            if (!Store.PassWaypoint(slot, out _, anchor: false))
                break;
        }
        bool route = s.Helm == Helm.Route && s.RouteNext < s.RouteCount;
        double want = route ? Earth.Bearing(p, s.Waypoint(s.RouteNext)) : s.CourseOrderDeg;
        sbyte side = (sbyte)s.LandSide;
        double heading = Slide.Feel(shore, h, p, f, s.Speed, s.CourseRudder, want, ref side, settings, out _);
        long ms;
        if (side == 0)
        {
            // let go, or not sliding yet: on its orders again, and looked at again where it would start to slide
            if (route)
            {
                Store.ReanchorLeg(slot, p, Toward(p, s.Waypoint(s.RouteNext)));
                ScheduleArrival(world, slot, s.Waypoint(s.RouteNext));
            }
            else
            {
                Store.ReanchorRhumb(slot, p, s.CourseOrderDeg);
                SchedulePolar(world, slot);
            }
            Store.LetGoOfCoast(slot);
            Anchored(world, slot);
            double c = LandContact(shore, slot, 0, out bool whole), look = Slide.LookaheadM(h, s.Speed, s.CourseRudder, settings);
            ushort code;
            double at;
            if (c < double.PositiveInfinity)
                (code, at) = (LandAhead, Math.Max(c - look, settings.SlideRailsMinStepM));
            else if (!whole)
                (code, at) = (LandRecheck, Math.Max(settings.RailsLandHorizonM - look, look));
            else
                return;
            ms = (long)Math.Ceiling(at / s.Speed * 1000);
            world.Schedule(world.Time + Math.Max(ms, 1), EventTier.Rails, s.Entity, Id, code, Store.RailGen(slot));
            return;
        }
        var dir = Earth.Forward(p, heading);
        Store.ReanchorSliding(slot, p, dir, side);
        Anchored(world, slot);
        double near = shore.FirstContact(p, dir, settings.SlideRailsStepM, h.MinClearanceM);
        double leg = Math.Min(settings.SlideRailsStepM, Math.Max(0.5 * near, settings.SlideRailsMinStepM));
        ms = (long)Math.Ceiling(leg / s.Speed * 1000);
        world.Schedule(world.Time + Math.Max(ms, 1), EventTier.Rails, s.Entity, Id, LandAhead, Store.RailGen(slot));
    }

    /// <summary>How far abeam of a unit's guide on rails its widest riding ship is, metres.</summary>
    double UnitAbeam(World world, int guide)
    {
        int entity = Store[guide].Entity;
        double r = 0;
        if (world.Groups.UnitOf(entity) is { } u && world.Groups.GuideShip(u) == entity)
        {
            world.Groups.LineOffsets(u, riders);
            for (int i = 1; i < riders.Count; i++)
                if (Store.SlotOf(riders[i].Ship) is int m and >= 0 && Store.RidingOn(m) == guide)
                    r = Math.Max(r, Store.RailAbeamM(m));
        }
        return r;
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
    /// when its great circle leaves it (solved in closed form), as an event; only if it's within
    /// <paramref name="withinMs"/> (a coarse ship's, looked at each step: it's laid again too often to leave an event
    /// days ahead each time).</summary>
    void SchedulePolar(World world, int slot, long withinMs = long.MaxValue)
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
        if (ms <= withinMs)
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

    /// <summary>Takes a ship off rails, and the line of the outermost group it moves with (they ride together), into
    /// the tier the speed asks for, exactly where the rails had them: the fine tick, or coarse (its guide on a segment
    /// of its own, the rest riding it still). A detached member rides rails of its own, so it comes off alone. With
    /// <paramref name="unride"/> (a change to groups) coarse ships riding their guide come off it too, to steer to
    /// their new stations. Whoever watches ships on the fine tick wakes (a contact isn't a command).</summary>
    public void Derail(World world, int entity, bool unride = false)
    {
        bool any = false;
        if (world.Groups.UnitOf(entity) is { } u)
        {
            world.Groups.LineOffsets(u, derailing);
            for (int i = 0; i < derailing.Count; i++)
                any |= DerailSlot(world, Store.SlotOf(derailing[i].Ship), unride);
        }
        else
            any = DerailSlot(world, Store.SlotOf(entity), unride);
        if (any)
        {
            if (!coarseMode)
                EnsureTick(world);
            world.OffRails();
        }
    }

    bool DerailSlot(World world, int slot, bool unride)
    {
        if (slot < 0)
            return false;
        if (Store.OnRails(slot))
        {
            if (coarseMode)
                ToCoarse(world, slot);
            else
                Store.Derail(slot);
            return true;
        }
        if (unride && Store.TierOf(slot) == ClockTier.Coarse && Store.RidesAnother(slot))
        {
            Store.Coarsen(slot);
            PlanCoarse(world, slot);
            return true;
        }
        return false;
    }

    /// <summary>What a change to groups takes off rails, before it's made: whatever changes comes off whole, the ship's
    /// unit, the group's own line and the force's it joins or leaves.</summary>
    internal void DerailFor(World world, in Command c)
    {
        var groups = world.Groups;
        var code = (GroupCommand)c.Code;
        if (c.Entity != 0)
            Derail(world, c.Entity, unride: true);
        switch (code)
        {
            case GroupCommand.SetFormation or GroupCommand.TurnTogether or GroupCommand.Reverse or GroupCommand.Disband or GroupCommand.Add or GroupCommand.Form or GroupCommand.Exit or GroupCommand.DetachGroup:
                DerailGroup(world, groups.Get((int)c.A));
                break;
            case GroupCommand.Enter or GroupCommand.EnterAlongside:
                DerailGroup(world, groups.Get((int)c.A));
                DerailGroup(world, groups.Get((int)c.Y));
                break;
            case GroupCommand.Join when c.A != 0:
                Derail(world, (int)c.A, unride: true);
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
                    Derail(world, e, unride: true);
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
            Derail(world, s, unride: true);
        if (g.Parent != 0 && groups.Get(g.Parent) is { } f && groups.GuideShip(groups.TopOf(f)) is int fs and > 0)
            Derail(world, fs, unride: true);   // a detached group rejoining: the line it joins comes off too
    }

    // ------------------------------------------------------------------ the coarse tier (docs/battle/plan.md step 2.8)

    bool coarseMode;   // ships off rails are coarse: the asked speed at CoarseMinSpeed or more, as of its last change
    long nextCoarseMs = -1;   // the coarse step scheduled, or -1

    /// <summary>Whether ships off rails are coarse: the asked speed at <see cref="SimSettings.CoarseMinSpeed"/> or
    /// more. The asked speed, never the achieved one (world state, so it replays), and never the zoom.</summary>
    bool CoarseNow(World world) => world.Speed >= settings.CoarseMinSpeed;

    /// <summary>The asked speed changed (a command, or a halt at an event): crossing the threshold, every ship off rails
    /// goes across at this moment, where it is.</summary>
    internal void SpeedChanged(World world)
    {
        bool coarse = CoarseNow(world);
        if (coarse == coarseMode)
            return;
        coarseMode = coarse;
        Normalize(world);
    }

    /// <summary>Every ship off rails into the tier the speed asks for. Coarse: the fine tick's ships stepped to now and
    /// put on segments of their own from where they are, steering as they were (a group's ships ride their guide again
    /// once they're in station, <see cref="RideCheck"/>). Fine: coarse ships handed to the fine tick as their segments
    /// have them, their swing carried on; whoever watches the fine tick wakes.</summary>
    void Normalize(World world)
    {
        if (coarseMode)
        {
            if (Store.FineCount == 0)
                return;
            ShipMotion.StepAll(Store, world.Time.Ms, settings, world.Shore);
            while (Store.FineCount > 0)
                ToCoarse(world, Store.FineSlot(Store.FineCount - 1));
            return;
        }
        if (Store.CoarseCount == 0)
            return;
        while (Store.CoarseCount > 0)
            Store.Derail(Store.CoarseSlot(Store.CoarseCount - 1));
        EnsureTick(world);
        world.OffRails();
    }

    /// <summary>What damage left a ship (docs/battle/fighting.md 4.3a): its power and steering into the store, the ship
    /// (and its unit) off rails first, and a coarse one laid again from where it is.</summary>
    internal void Damaged(World world, int slot, double powerCap, Steering steering, double jamRudderRad)
    {
        if (Store.OnRails(slot))
            Derail(world, Store[slot].Entity);
        Store.SetDamage(slot, powerCap, steering, jamRudderRad);
        if (Store.TierOf(slot) == ClockTier.Coarse)
            Recoarsen(world, slot);
    }

    /// <summary>A ship into the coarse tier, from rails or the fine tick: riding its guide on rails, it rides it coarse;
    /// else on a segment of its own from where it is.</summary>
    void ToCoarse(World world, int slot)
    {
        if (Store.OnRails(slot) && Store.RidesAnother(slot))
            Store.RideCoarse(slot);
        else
        {
            Store.Coarsen(slot);
            PlanCoarse(world, slot);
        }
        EnsureCoarseStep(world);
    }

    /// <summary>An order to a coarse ship, given: laid again from where it is (a ship riding its guide comes off it, on a
    /// segment of its own).</summary>
    void Recoarsen(World world, int slot)
    {
        if (Store.RidesAnother(slot))
            Store.Coarsen(slot);
        else
            Store.ReanchorCoarse(slot);
        PlanCoarse(world, slot);
    }

    /// <summary>Lays a coarse ship's next segment from its orders; its end is an event. Holding a course to no end, it
    /// gets the polar caps' events as rails do, once a step finds one coming (<see cref="CoarseStepEvent"/>).</summary>
    void PlanCoarse(World world, int slot)
    {
        Store.PoseNow(slot, out _, out var was);
        long end = CoarseMotion.Plan(Store, slot, settings);
        if (world.Shore is { } shore)
            end = Slide.CoarseCoast(Store, slot, shore, settings, end);   // short of the coast, or sliding along it
        if (end != long.MaxValue)
            world.Schedule(new WorldTime(end), EventTier.Coarse, Store[slot].Entity, Id, CoarseSegment, Store.RailGen(slot));
        else
            SchedulePolar(world, slot, settings.CoarseStepMs);
        TurnTogether(world, slot, was);
    }

    /// <summary>A unit's guide just laid a turn (an arc, or a heading changed at once: a slide's leg): its ships riding
    /// abeam of its track come off it and turn together with it (<see cref="RidesThroughTurn"/>), their captains woken
    /// to steer them back to their stations.</summary>
    void TurnTogether(World world, int guide, Vec3d was)
    {
        Store.PoseNow(guide, out _, out var f);
        if (Store.CurvatureNow(guide) == 0 && Vec3d.Dot(was, f) >= Math.Cos(settings.RailsGroupHeadingDeg * Math.PI / 180))
            return;
        int entity = Store[guide].Entity;
        if (world.Groups.UnitOf(entity) is not { } u || world.Groups.GuideShip(u) != entity)
            return;
        world.Groups.LineOffsets(u, turning);
        bool any = false;
        for (int i = 1; i < turning.Count; i++)
            if (Store.SlotOf(turning[i].Ship) is int m and >= 0 && Store.RidingOn(m) == guide && !RidesThroughTurn(m))
            {
                Store.TurnTogether(m, guide);
                PlanCoarse(world, m);
                any = true;
            }
        if (any)
        {
            EnsureCoarseStep(world);
            world.OffRails();
        }
    }

    /// <summary>A coarse ship's segment ends: the next laid from there. Stale (laid again, riding, or out of coarse
    /// since) it's nothing.</summary>
    void CoarseSegmentEnd(World world, in SimEvent e)
    {
        int slot = Store.SlotOf(e.Entity);
        if (slot < 0 || Store.TierOf(slot) != ClockTier.Coarse || Store.RidesAnother(slot) || Store.RailGen(slot) != e.Generation)
            return;
        Store.ReanchorCoarse(slot);
        if (world.Shore is { } shore)
            Slide.CoarseBackstop(Store, slot, shore, settings);
        PlanCoarse(world, slot);
    }

    void EnsureCoarseStep(World world)
    {
        if (nextCoarseMs >= 0)
            return;
        long step = settings.CoarseStepMs;
        nextCoarseMs = (world.Time.Ms / step + 1) * step;
        world.Schedule(new WorldTime(nextCoarseMs), EventTier.Coarse, 0, Id, CoarseStep);
    }

    /// <summary>The coarse step: what the fine tick looks at every few seconds (contacts, encounters, the rails check,
    /// sighting over the step), looked at once a step while no ship is on the fine tick (if any is, its tick does it
    /// for all); a group's ships back in station ride their guide again; a ship inside its minimum off the coast is put
    /// back out (Slide's backstop); and a polar cap coming within the step is an event.</summary>
    void CoarseStepEvent(World world)
    {
        nextCoarseMs = -1;
        if (Store.CoarseCount == 0)
            return;
        if (Store.FineCount == 0)
        {
            long t;
            if (settings.RailsEnabled)
            {
                t = Probes.Now;
                FineContacts(world);
                probes.Add(contactsProbe, t);
            }
            UpdateEncounters(world);
            sightings.Forget(world.Time.Ms);
            if (settings.RailsEnabled)
            {
                t = Probes.Now;
                RailsCheck(world);
                probes.Add(railsProbe, t);
            }
            if (encounters.Count > 0)
            {
                t = Probes.Now;
                sightings.Look(world, Store, used, settings.CoarseStepMs / 1000.0);
                probes.Add(sightingProbe, t);
            }
        }
        RideCheck(world);
        for (int k = 0; k < Store.CoarseCount; k++)
        {
            int slot = Store.CoarseSlot(k);
            if (world.Shore is { } shore && Slide.CoarseInside(shore, Store[slot].Handling, Store[slot].Position))
            {
                // inside its minimum (a turn's arc cut a corner, or a ship riding its guide's track abeam of it): off
                // its guide, put back out, laid again; its captain steers it back to its station
                if (Store.RidesAnother(slot))
                    Store.Coarsen(slot);
                else
                    Store.ReanchorCoarse(slot);
                Slide.CoarseBackstop(Store, slot, shore, settings);
                PlanCoarse(world, slot);
                continue;
            }
            if (Store.SegmentEndMs(slot) == long.MaxValue && !Store.RidesAnother(slot))
                SchedulePolar(world, slot, settings.CoarseStepMs);   // a polar cap reached within the step
        }
        if (Store.CoarseCount > 0)
            EnsureCoarseStep(world);
    }

    /// <summary>At the coarse step, a group's ships steering back to their stations (their captains' doing) ride their
    /// coarse guide again once they're in station, at their offsets as they are, on the guide's track (a column turns
    /// in succession, ShipStore): nothing jumps.</summary>
    void RideCheck(World world)
    {
        var groups = world.Groups;
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (groups.InForceLine(g))
                continue;   // carried with its force
            int guide = Store.SlotOf(groups.GuideShip(g));
            if (guide < 0 || Store.TierOf(guide) != ClockTier.Coarse || Store.RidesAnother(guide))
                continue;
            groups.LineOffsets(g, line);
            var gs = Store[guide];
            double speed = gs.Speed;
            for (int i = 1; i < line.Count; i++)
                if (Store.SlotOf(line[i].Ship) is int m and >= 0 && Store.TierOf(m) == ClockTier.Coarse && !Store.RidesAnother(m) &&
                    MemberInStation(gs, Store[m], speed, line[i].AsternM, line[i].AbeamM))
                    Store.RideCoarseWith(m, guide);   // where its guide's leg reaches back past it: else at a later step
        }
    }
}
