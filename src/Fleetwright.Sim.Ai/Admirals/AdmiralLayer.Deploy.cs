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
        if (!g.IsForce || groups.InForceLine(g))
            return false;
        var s = ctx.Settings;
        var guide = ctx.Ships[lead];
        if (a.DeployMs != 0)
        {
            if (Formed(ctx, g, guide) || ctx.NowMs - a.DeployMs > s.DeployMaxMs)
                (a.DeployMs, a.Deployed) = (0, true);
            return a.DeployMs != 0;
        }
        if (a.Whose == Whose.Player || Tactical == null || doctrine.OfGroup(g, guide.Side, DoctrineField.Deploy) == 0)
            return false;
        bool near = Tactical.MainBody(ctx.World, guide.Side, guide.Position, s.DeployM, out var main);
        if (a.Deployed)
        {
            if (!near && !Tactical.MainBody(ctx.World, guide.Side, guide.Position, 2 * s.DeployM, out _))
                a.Deployed = false;
            return false;
        }
        if (!near || OneLine(g) || !BattleForce(ctx, g))
            return false;
        int first = groups.Get(g.Line[0]) is { } f0 ? groups.GuideShip(f0) : 0, last = groups.Get(g.Line[^1]) is { } fl ? groups.GuideShip(fl) : 0;
        if (ctx.Ships.SlotOf(first) < 0 || ctx.Ships.SlotOf(last) < 0)
            return false;
        StartDeploy(ctx, g, a, DeployCourse(guide, main));
        return true;
    }

    /// <summary>The course a force deploys on against an enemy group: square across his course toward his track, so the
    /// line lies across his way ahead of him (crossing his T); square across his bearing, the way nearer the force's
    /// heading, when he's stopped.</summary>
    static double DeployCourse(ShipState guide, in EnemyGroup main)
    {
        if (main.Velocity.Length > 1)
        {
            var his = main.Velocity * (1 / main.Velocity.Length);
            var hisStarboard = Vec3d.Cross(his, main.Centre.Normalized());
            double heading = Earth.Heading(main.Centre, his);
            return Wrap360(Vec3d.Dot(guide.Position - main.Centre, hisStarboard) >= 0 ? heading - 90 : heading + 90);
        }
        double bearing = Earth.Bearing(guide.Position, main.Centre);
        double right = Wrap360(bearing + 90), left = Wrap360(bearing - 90);
        return Math.Abs(Wrap180(right - guide.HeadingDeg)) <= Math.Abs(Wrap180(left - guide.HeadingDeg)) ? right : left;
    }

    /// <summary>A force into its line of battle on a course: the line made (<see cref="IntoLine"/>), its guide's course
    /// handed on next think, deploying until it's formed.</summary>
    void StartDeploy(in AiContext ctx, GroupSystem.Group g, Admiral a, double course)
    {
        var guide = ctx.Ships[ctx.Ships.SlotOf(groups.GuideShip(g))];
        IntoLine(ctx, g, Earth.Forward(guide.Position, course));
        (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, course, 0, 0, false);
        (a.DeployMs, a.HandedTo, a.Met) = (ctx.NowMs, 0, false);   // the new guide is handed the course next think
    }

    /// <summary>Deploy by order (<see cref="AiCommand.Deploy"/>, docs/battle/admirals-orders.md O2): a force into its line
    /// of battle on the course ordered, or (NaN) on doctrine's course against the enemy's main body within
    /// <see cref="SimSettings.FightCompareM"/>, else on its guide's heading; a squadron into line ahead on it. Whoever
    /// orders it, it's deployed once formed, as doctrine's.</summary>
    void DeployOrdered(in AiContext ctx, GroupSystem.Group g, Admiral a, double course, int lead)
    {
        var guide = ctx.Ships[lead];
        if (double.IsNaN(course))
            course = Tactical != null && Tactical.MainBody(ctx.World, guide.Side, guide.Position, ctx.Settings.FightCompareM, out var main)
                ? DeployCourse(guide, main) : guide.HeadingDeg;
        course = Wrap360(course);
        if (g.IsForce)
        {
            StartDeploy(ctx, g, a, course);
            return;
        }
        ctx.Issue(GroupSystem.SetFormation(g.GroupId, Formation.Column, g.SpacingM));
        (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, course, 0, 0, false);
        a.HandedTo = 0;   // its guide is handed the course next think, in the line remade
    }

    /// <summary>A force, and every force in its line, into line ahead along a direction: in each, the member furthest along
    /// it leads (the one on the side the line turns to, as Jellicoe's port wing did), so the others turn after it into
    /// its wake, none ahead of it to drop back from; then the whole tree in one line (<see cref="LineShape.OneLine"/>).</summary>
    void IntoLine(in AiContext ctx, GroupSystem.Group g, Vec3d along)
    {
        if (!g.IsForce || g.Line.Count == 0)
            return;
        Lead(ctx, g, along);
        ctx.Issue(GroupSystem.Shape(g.GroupId, LineShape.OneLine));
    }

    /// <summary>A force's members, and each force's in its line, taken in the reverse order where its far end lies
    /// further along a direction.</summary>
    void Lead(in AiContext ctx, GroupSystem.Group g, Vec3d along)
    {
        if (!g.IsForce || g.Line.Count == 0)
            return;
        int first = groups.Get(g.Line[0]) is { } f0 ? groups.GuideShip(f0) : 0, last = groups.Get(g.Line[^1]) is { } fl ? groups.GuideShip(fl) : 0;
        if (ctx.Ships.SlotOf(first) is int fs and >= 0 && ctx.Ships.SlotOf(last) is int ls and >= 0 &&
            Vec3d.Dot(ctx.Ships[ls].Position - ctx.Ships[fs].Position, along) > 0)
            ctx.Issue(GroupSystem.Reverse(g.GroupId));
        for (int k = 0; k < g.Line.Count; k++)
            if (groups.Get(g.Line[k]) is { IsForce: true } sub)
                Lead(ctx, sub, along);
    }

    /// <summary>A force forming its line in a new shape (<see cref="AiCommand.Shape"/>) is formed when every ship is
    /// within <see cref="SimSettings.DeployFormedM"/> of her station, as a deployment is (or after
    /// <see cref="SimSettings.DeployMaxMs"/>); unlike one, it takes up a fight meanwhile.</summary>
    void FormingStep(in AiContext ctx, GroupSystem.Group g, Admiral a, int lead)
    {
        if (a.FormingMs != 0 && (Formed(ctx, g, ctx.Ships[lead]) || ctx.NowMs - a.FormingMs > ctx.Settings.DeployMaxMs))
            a.FormingMs = 0;
    }

    /// <summary>Whether a group, or a force whose line it's in, up the tree, is deploying or forming its line in a new
    /// shape: its groups make for their final places (<see cref="FinalStation"/>).</summary>
    bool Forming(GroupSystem.Group g)
    {
        for (var at = g; ;)
        {
            if (Find(at.GroupId) is int i and >= 0 && (admirals[i].DeployMs != 0 || admirals[i].FormingMs != 0))
                return true;
            if (!groups.InForceLine(at) || groups.Get(at.Parent) is not { } up)
                return false;
            at = up;
        }
    }

    /// <summary>While deploying or forming, the i-th group of the line keeps its place on the line's guide itself (its
    /// final place, astern of him by the line before it), not on the last ship of the group before, which is still
    /// finding its own: a chain of moving stations lags column by column.</summary>
    (int Target, double AsternM, double AbeamM) FinalStation(GroupSystem.Group g, int i)
    {
        groups.LineOffsets(g, line);
        int guide = groups.GuideShip(g), ship = groups.Get(g.Line[i]) is { } sub ? groups.GuideShip(sub) : 0;
        foreach (var (s, astern, abeam) in line)
            if (s == ship)
                return (guide, astern, abeam);
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
