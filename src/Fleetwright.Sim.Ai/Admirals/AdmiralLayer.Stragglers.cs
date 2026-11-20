using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>Stragglers (docs/battle/command.md "Stragglers"; the user, 2026-11-20: "the rest of the ships should just
/// ignore the stalling ship"). A ship that stays in her line though she can't keep it (doctrine doesn't take her out, her
/// captain's brave, the player holds her in it, or what slowed her is back within its wait) held up every ship astern of
/// her, each keeping station on the one ahead, and left the van alone. Her admiral takes her out of the line's chain
/// (<see cref="GroupSystem.Straggle"/>) once she's more than <see cref="SimSettings.StragglerSpacings"/> of her station's
/// spacings astern of it, not closing it and unable to (<see cref="CanClose"/>), while another ship keeps station on her: the ships astern close up on the one
/// ahead of her, and she keeps station astern of the tail of the line she's in, turned out of its way to the side she's on
/// while it passes her (<see cref="OrderStraggler"/>). Once it has, and no ship would follow her at her squadron's end, she rejoins there: at the rear, for good.
/// Judged each think from where the ships are, nothing kept but the group's own state.</summary>
public sealed partial class AdmiralLayer
{
    /// <summary>His squadron's stragglers: who's out of the chain now, who's back.</summary>
    void Stragglers(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        var line = g.Line;
        int keeping = g.Keeping;
        for (int i = 0; i < line.Count; i++)
        {
            int slot = ctx.Ships.SlotOf(line[i]);
            if (slot < 0)
                continue;
            var own = ctx.Ships[slot];
            if (i >= keeping)
            {
                // back once the line has passed her, where nobody would follow her
                if (!groups.TrailFollowed(g) && StragglerStation(ctx, g, own.Entity) is { Target: > 0 } st &&
                    ctx.Ships.SlotOf(st.Target) is int on and >= 0 &&
                    Gap(own, ctx.Ships[on], st.AsternM, st.AbeamM).Behind > -0.25 * g.SpacingM)
                    ctx.Issue(GroupSystem.Straggle(own.Entity, false));
                continue;
            }
            bool followed = i < keeping - 1 ? i == 0 || g.Formation == Formation.Column : groups.TrailFollowed(g);
            if (!followed || groups.IsWing(own.Entity))
                continue;
            // her own station: on the ship ahead of her in her squadron, or (its guide) the one her force's admiral gave
            int target;
            double astern, abeam, spacing;
            if (i > 0)
            {
                (target, astern, abeam) = GroupSystem.StationOf(g, i);
                spacing = g.SpacingM;
            }
            else if (a is { Whose: Whose.Superior, Order: CaptainOrder.Station } && groups.Get(g.Parent) is { } force)
                (target, astern, abeam, spacing) = (new Unit(a.Target).ShipOf(groups), a.DistanceM, a.AbeamM, force.SpacingM);
            else
                continue;   // the unit's guide: the line's speed is hers
            if (ctx.Ships.SlotOf(target) is not (int t and >= 0))
                continue;
            var gap = Gap(own, ctx.Ships[t], astern, abeam);
            if (gap.Behind > ctx.Settings.StragglerSpacings * spacing && gap.Closing <= 0 && !CanClose(ctx, slot, own, ctx.Ships[t].Speed))
                ctx.Issue(GroupSystem.Straggle(own.Entity, true));
        }
    }

    /// <summary>Whether a ship could close a station on a ship making <paramref name="speedMs"/>: her steering answers, and what
    /// her plant makes now (her shafts out of the water make nothing) is over that by half the margin a line keeps in hand
    /// (<see cref="SimSettings.GroupSpeedMarginKn"/>). Not, whatever holds her back, if she's merely slow now: a ship that
    /// was held up astern of a straggler works up to her station again.</summary>
    static bool CanClose(in AiContext ctx, int slot, ShipState own, double speedMs)
    {
        var k = ctx.Ships.Combat.Condition(slot);
        if (k.Steering is Steering.Jammed or Steering.Lost or Steering.Engines)
            return false;
        var h = own.Handling;
        var c = h.Class;
        double screws = k.Attitude != Attitude.Upright ? Ploughing.Screws(c, HitQuery.WaterOf(c, k.Attitude)) : 1;
        double kn = c.SpeedAt(h.MaxPowerW / Handling.WattsPerShp * k.PowerCap * screws);
        return kn > speedMs / Handling.MsPerKnot + 0.5 * ctx.Settings.GroupSpeedMarginKn;
    }

    /// <summary>A straggler's orders: her station astern of the tail of her line (<see cref="StragglerStation"/>), but while
    /// she's ahead of it (the line still to pass her) out of its way, the side she's on: on its line still, she turns
    /// <see cref="SimSettings.StragglerTurnOutDeg"/> off its course (a station abeam of it she'd make only as she dropped back,
    /// too slowly for the line coming up at its speed); out by <see cref="SimSettings.StragglerHaulOutSpacings"/> of a spacing,
    /// her station that far abeam of it.</summary>
    void OrderStraggler(in AiContext ctx, GroupSystem.Group g, int ship)
    {
        var (target, astern, abeam) = StragglerStation(ctx, g, ship);
        if (target == 0 || ctx.Ships.SlotOf(ship) is not (int s and >= 0) || ctx.Ships.SlotOf(target) is not (int t and >= 0))
            return;
        var own = ctx.Ships[s];
        var tail = ctx.Ships[t];
        var gap = Gap(own, tail, astern, abeam);
        double haul = ctx.Settings.StragglerHaulOutSpacings * g.SpacingM;
        if (gap.Behind < -0.5 * g.SpacingM)
        {
            double side = gap.Across > 0 ? -1 : 1;   // the station to her starboard: she's to port of the line
            if (Math.Abs(gap.Across) < 0.9 * haul)
            {
                double course = tail.HeadingDeg + side * ctx.Settings.StragglerTurnOutDeg;
                course -= 360 * Math.Floor(course / 360);
                double off = own.CourseOrderDeg - course;
                if (captains.Of(ship) is not { Whose: Whose.Superior, Order: CaptainOrder.Course } || own.Helm != Helm.Course ||
                    Math.Abs(off - 360 * Math.Round(off / 360)) > 0.5)
                    ctx.Issue(ctx.Orders.Course(Unit.Ship(ship), course, 0, OrderSource.Superior));
                return;
            }
            abeam += side * haul;
        }
        if (!captains.Holds(ship, CaptainOrder.Station, target, astern, abeam))
            ctx.Issue(ctx.Orders.Station(Unit.Ship(ship), Unit.Ship(target), astern, abeam, OrderSource.Superior));
    }

    /// <summary>A straggler's station: astern of the tail of the line she's in, where the group's line has her
    /// (<see cref="GroupSystem.LineOffsets"/>), on the last ship of it that keeps the formation. 0 for none.</summary>
    (int Target, double AsternM, double AbeamM) StragglerStation(in AiContext ctx, GroupSystem.Group g, int ship)
    {
        groups.LineOffsets(groups.TopOf(g), line);
        int at = -1;
        for (int j = 0; j < line.Count; j++)
            if (line[j].Ship == ship)
                at = j;
        int b = at - 1;
        while (b >= 0 && groups.IsStraggling(line[b].Ship))
            b--;
        if (at < 0 || b < 0)
            return (0, 0, 0);
        return (line[b].Ship, line[at].AsternM - line[b].AsternM, line[at].AbeamM - line[b].AbeamM);
    }

    /// <summary>A ship against a station <paramref name="astern"/> astern of another and <paramref name="abeam"/> to its
    /// starboard, in its frame: how far astern of the station she is (negative: ahead), how far it lies to her starboard,
    /// and how fast she closes it (m/s; negative: she's dropping away).</summary>
    static (double Behind, double Across, double Closing) Gap(ShipState own, ShipState on, double astern, double abeam)
    {
        var lf = on.Forward;
        var up = on.Position.Normalized();
        var starboard = Vec3d.Cross(lf, up);
        var station = (on.Position - lf * astern + starboard * abeam).Normalized() * Earth.Radius;
        var gap = station - own.Position;
        gap -= up * Vec3d.Dot(gap, up);
        double off = gap.Length;
        var relative = own.Forward * own.Speed - lf * on.Speed;
        return (Vec3d.Dot(gap, lf), Vec3d.Dot(gap, starboard), off > 1 ? Vec3d.Dot(relative, gap) / off : 0);
    }
}
