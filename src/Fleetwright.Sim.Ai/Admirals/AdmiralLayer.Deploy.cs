using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>A force's deployment (docs/battle/command.md 5.1c): a force in a cruising order (its squadrons in columns
/// side by side, or any line that isn't one line ahead) with doctrine's Deploy, whose guide comes within
/// <see cref="SimSettings.DeployM"/> of the enemy's main body (the tactical picture's strongest group with a capital ship),
/// forms one line of battle: the wing column furthest along the course it deploys on leads (the force taken in the reverse order when
/// that's its far wing), the force in line ahead, its squadrons as far apart as their own ships, the rest following it
/// in succession; the leading column steers across the enemy's course, ahead of him (of the two ways, the nearer its
/// heading; across his bearing if he's stopped).
/// It's deployed when every ship is within <see cref="SimSettings.DeployFormedM"/> of her station in the line (or after
/// <see cref="SimSettings.DeployMaxMs"/>); until then it takes up no fight and its task waits; then doctrine's Engage
/// takes the line into the fight. Deployed once an encounter: re-armed when no main body is within twice the range.</summary>
public sealed partial class AdmiralLayer
{
    /// <summary>The enemy as the admirals see him: groups and their strength (set by the AI system).</summary>
    internal TacticalPicture? Tactical { get; set; }

    /// <summary>Whether a force is deploying into its line of battle.</summary>
    public bool Deploying(int groupId) => Find(groupId) is int i and >= 0 && admirals[i].DeployMs != 0;

    /// <summary>The deployment, each think; whether the think is to give no fight (and, at its start, no stations: the
    /// line it gives them for is made by the commands just sent).</summary>
    bool DeployStep(in AiContext ctx, GroupSystem.Group g, Admiral a, int lead)
    {
        if (a.Whose == Whose.Player || !g.IsForce || groups.InForceLine(g) || Tactical == null)
            return false;
        var s = ctx.Settings;
        var guide = ctx.Ships[lead];
        if (doctrine.OfGroup(g, guide.Side, DoctrineField.Deploy) == 0)
            return false;
        if (a.DeployMs != 0)
        {
            if (Formed(ctx, g, guide) || ctx.NowMs - a.DeployMs > s.DeployMaxMs)
                (a.DeployMs, a.Deployed) = (0, true);
            return a.DeployMs != 0;
        }
        bool near = Tactical.MainBody(ctx.World, guide.Side, guide.Position, s.DeployM, out var main);
        if (a.Deployed)
        {
            if (!near && !Tactical.MainBody(ctx.World, guide.Side, guide.Position, 2 * s.DeployM, out _))
                a.Deployed = false;
            return false;
        }
        if (!near || OneLine(g) || !BattleForce(ctx, g))
            return false;

        // the course: square across the enemy's course toward his track, so the line lies across his way ahead of him
        // (crossing his T); square across his bearing, the way nearer the force's heading, when he's stopped
        int first = groups.Get(g.Line[0]) is { } f0 ? groups.GuideShip(f0) : 0, last = groups.Get(g.Line[^1]) is { } fl ? groups.GuideShip(fl) : 0;
        if (ctx.Ships.SlotOf(first) is not (int fs and >= 0) || ctx.Ships.SlotOf(last) is not (int ls and >= 0))
            return false;
        double course;
        if (main.Velocity.Length > 1)
        {
            var his = main.Velocity * (1 / main.Velocity.Length);
            var hisStarboard = Vec3d.Cross(his, main.Centre.Normalized());
            double heading = Earth.Heading(main.Centre, his);
            course = Wrap360(Vec3d.Dot(guide.Position - main.Centre, hisStarboard) >= 0 ? heading - 90 : heading + 90);
        }
        else
        {
            double bearing = Earth.Bearing(guide.Position, main.Centre);
            double right = Wrap360(bearing + 90), left = Wrap360(bearing - 90);
            course = Math.Abs(Wrap180(right - guide.HeadingDeg)) <= Math.Abs(Wrap180(left - guide.HeadingDeg)) ? right : left;
        }

        IntoLine(ctx, g, Earth.Forward(guide.Position, course));
        (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, course, 0, 0, false);
        (a.DeployMs, a.HandedTo, a.Met) = (ctx.NowMs, 0, false);   // the new guide is handed the course next think
        return true;
    }

    /// <summary>A force, and every force in its line, into line ahead along a direction: in each, the member furthest along
    /// it leads (the one on the side the line turns to, as Jellicoe's port wing did), so the others turn after it into
    /// its wake, none ahead of it to drop back from; its groups as far apart as the ships of its first squadron.</summary>
    void IntoLine(in AiContext ctx, GroupSystem.Group g, Vec3d along)
    {
        if (!g.IsForce || g.Line.Count == 0)
            return;
        int first = groups.Get(g.Line[0]) is { } f0 ? groups.GuideShip(f0) : 0, last = groups.Get(g.Line[^1]) is { } fl ? groups.GuideShip(fl) : 0;
        if (ctx.Ships.SlotOf(first) is int fs and >= 0 && ctx.Ships.SlotOf(last) is int ls and >= 0 &&
            Vec3d.Dot(ctx.Ships[ls].Position - ctx.Ships[fs].Position, along) > 0)
            ctx.Issue(GroupSystem.Reverse(g.GroupId));
        var squadron = g;
        while (squadron.IsForce && squadron.Line.Count > 0 && groups.Get(squadron.Line[0]) is { } down)
            squadron = down;
        ctx.Issue(GroupSystem.SetFormation(g.GroupId, Formation.Column, squadron.SpacingM));
        for (int k = 0; k < g.Line.Count; k++)
            if (groups.Get(g.Line[k]) is { IsForce: true } sub)
                IntoLine(ctx, sub, along);
    }

    /// <summary>Whether a group, or a force whose line it's in, up the tree, is deploying.</summary>
    bool InDeployment(GroupSystem.Group g)
    {
        for (var at = g; ;)
        {
            if (Find(at.GroupId) is int i and >= 0 && admirals[i].DeployMs != 0)
                return true;
            if (!groups.InForceLine(at) || groups.Get(at.Parent) is not { } up)
                return false;
            at = up;
        }
    }

    /// <summary>While deploying, the i-th group of the line keeps its place on the line's guide itself, on his track (its
    /// final place, astern of him by the line before it), not on the last ship of the group before, which is still
    /// finding its own: a chain of moving stations lags column by column.</summary>
    (int Target, double AsternM, double AbeamM) DeployStation(GroupSystem.Group g, int i)
    {
        groups.LineOffsets(g, line);
        int guide = groups.GuideShip(g), ship = groups.Get(g.Line[i]) is { } sub ? groups.GuideShip(sub) : 0;
        foreach (var (s, astern, _) in line)
            if (s == ship)
                return (guide, astern, 0);
        return groups.StationOfGroup(g, i);
    }

    /// <summary>Whether a force is a battle force that deploys: two capital ships or more in its line, and not on a task
    /// that keeps it apart (scouting, screening, shadowing, searching, withdrawing). Cruiser and flotilla forces don't.</summary>
    bool BattleForce(in AiContext ctx, GroupSystem.Group g)
    {
        if (Tasks?.Of(Unit.Group(g.GroupId)) is { Kind: Ai.Tasks.TaskKind.Scout or Ai.Tasks.TaskKind.Screen or Ai.Tasks.TaskKind.Shadow or Ai.Tasks.TaskKind.Search or Ai.Tasks.TaskKind.Withdraw })
            return false;
        groups.LineOffsets(g, line);
        int capitals = 0;
        foreach (var (ship, _, _) in line)
            if (ctx.Ships.SlotOf(ship) is int slot and >= 0 && Sightings.TypeOf(ctx.Ships[slot].Handling.Class) == ShipType.Capital)
                capitals++;
        return capitals >= 2;
    }

    /// <summary>Whether a force is one line ahead already: in column, and every group of it too.</summary>
    bool OneLine(GroupSystem.Group g)
    {
        if (g.Formation != Formation.Column)
            return false;
        for (int k = 0; k < g.Line.Count; k++)
            if (groups.Get(g.Line[k]) is { } sub && (sub.Formation != Formation.Column || sub.IsForce && !OneLine(sub)))
                return false;
        return true;
    }

    /// <summary>Whether every ship of a line is within <see cref="SimSettings.DeployFormedM"/> of her station from the
    /// guide as it steams, and heading with it.</summary>
    bool Formed(in AiContext ctx, GroupSystem.Group g, ShipState guide)
    {
        groups.LineOffsets(g, line);
        var up = guide.Position.Normalized();
        var starboard = Vec3d.Cross(guide.Forward, up);
        foreach (var (ship, astern, abeam) in line)
        {
            if (ctx.Ships.SlotOf(ship) is not (int m and >= 0))
                continue;
            var at = ctx.Ships[m];
            var station = guide.Position - guide.Forward * astern + starboard * abeam;
            if ((at.Position - station).Length > ctx.Settings.DeployFormedM || Math.Abs(Wrap180(at.HeadingDeg - guide.HeadingDeg)) > 20)
                return false;
        }
        return true;
    }
}
