using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Orders drawn on the map (docs/battle/admirals-orders.md O9b, the mockup's <c>#chain</c> and <c>#screen</c>):
/// for the selection's unit, down its whole tree from the top, each unit's station, where its guide steers for (the
/// snapshot's, <see cref="ShipsView.StationAsternM"/>: what the ships do, not a copy of the rule), a mark dashed from
/// where it is, labelled with the unit and its task; a screen's arc about the unit it screens. An armed order shows its
/// shape before the click: the screen's or the cover's distance about the unit under the cursor, the scout's station,
/// the deploy's line, the way to a place.</summary>
public sealed partial class MapScene
{
    /// <summary>A unit's station mark this frame, target px (for its label, drawn by the UI a frame late: fine).</summary>
    struct TaskMark
    {
        public double X, Y;
        public int Group;
    }

    TaskMark[] taskMarks = new TaskMark[16];
    int taskMarkCount;
    readonly List<int> markGroups = [];
    readonly List<(int Target, double OffBow, double Dist)> screenArcs = [];

    /// <summary>The marks and the armed order's preview, inside the overlay's build.</summary>
    void DrawTaskMarks(in MapView view, Snapshot s)
    {
        taskMarkCount = 0;
        if (s.Ships.IndexOf(selected) < 0)
            return;
        var gv = s.Groups;
        markGroups.Clear();
        var units = OrderUnits(s);
        if (gv.IndexOfShip(selected) is int sg and >= 0)
            TreeOf(gv, sg);
        foreach (var u in units)
            if (u.IsGroup && gv.IndexOfGroup(u.GroupId) is int ug and >= 0)
                TreeOf(gv, ug);
        screenArcs.Clear();
        foreach (int g in markGroups)
            if (!gv.Force[g])   // a force's guide ship is its guide squadron's: one mark, the squadron's
                StationMark(view, s, g, gv.GuideShip[g]);
        if (markGroups.Count == 0)
            StationMark(view, s, -1, selected);   // a ship alone
        ScreenArcs(view, s);
        TaskPreview(view, s);
    }

    /// <summary>Every group of the tree a group is in, from its top, into <see cref="markGroups"/> (once each).</summary>
    void TreeOf(GroupsView gv, int g)
    {
        while (gv.Parent[g] != 0 && gv.IndexOfGroup(gv.Parent[g]) is int p and >= 0)
            g = p;
        AddTree(gv, g);
    }

    void AddTree(GroupsView gv, int g)
    {
        if (markGroups.Contains(g))
            return;
        markGroups.Add(g);
        if (gv.Force[g])
            for (int i = 0; i < gv.MemberCount[g]; i++)
                if (gv.IndexOfGroup(gv.Members[gv.MemberStart[g] + i]) is int sub and >= 0)
                    AddTree(gv, sub);
    }

    /// <summary>A unit's station (group g's, its guide's; -1: a ship alone): a ring where she steers for, dashed from where
    /// she is. Nothing for a station in the unit's own formation (on a ship of the line it's in) or none at all.</summary>
    void StationMark(in MapView view, Snapshot s, int g, int guide)
    {
        var v = s.Ships;
        var gv = s.Groups;
        int k = v.IndexOf(guide);
        if (k < 0 || double.IsNaN(v.StationAsternM[k]) || v.IndexOf(v.CaptainTarget[k]) is not (int kt and >= 0))
            return;
        int target = v.CaptainTarget[k];
        if (g >= 0)
        {
            // its formation: the line it's in, up to the unit it's in line with
            int root = g;
            while (InForce(gv, root) == Detachment.InLine && gv.Parent[root] != 0 && gv.IndexOfGroup(gv.Parent[root]) is int p and >= 0)
                root = p;
            if (Holds(gv, root, Unit.Ship(target)))
                return;
        }
        else if (gv.IndexOfShip(guide) >= 0)
            return;
        var (tp, tf) = v.PoseAt(kt, world.DrawMs);
        var at = (tp - tf * v.StationAsternM[k] + Vec3d.Cross(tf, tp.Normalized()) * v.StationAbeamM[k]).Normalized() * Earth.Radius;
        var (p0, _) = v.PoseAt(k, world.DrawMs);
        overlay.BeginStrip(MapPalette.TaskMarkFaint, 1.2f, Dash.Dashed);
        Point(view, p0);
        Point(view, at);
        overlay.End();
        RingM(view, at, 10 * Rp * view.MetresPerPixel, MapPalette.TaskMark, 1.4f, Dash.Dashed);
        if (g >= 0 && TaskOf(gv, g).Task == (byte)Sim.Ai.Tasks.TaskKind.Screen)
        {
            double off = ((Earth.Bearing(tp, at) - Earth.Heading(tp, tf)) % 360 + 540) % 360 - 180;
            screenArcs.Add((target, off, Earth.Distance(tp, at)));
        }
        if (g >= 0 && view.Project(at, out double x, out double y))
        {
            if (taskMarkCount == taskMarks.Length)
                Array.Resize(ref taskMarks, taskMarks.Length * 2);
            taskMarks[taskMarkCount++] = new TaskMark { X = x, Y = y, Group = g };
        }
    }

    /// <summary>A group's task and how it stands: its own, else its force's (a division carries the screen its force was
    /// handed), up the tree; 0 for none.</summary>
    static (byte Task, byte Status) TaskOf(GroupsView gv, int g)
    {
        while (gv.Task[g] == 0 && gv.Parent[g] != 0 && gv.IndexOfGroup(gv.Parent[g]) is int p and >= 0)
            g = p;
        return (gv.Task[g], gv.TaskStatus[g]);
    }

    /// <summary>Each screened unit's arc: through its screens' stations, a little past the end ones, dotted.</summary>
    void ScreenArcs(in MapView view, Snapshot s)
    {
        if (screenArcs.Count == 0)
            return;
        screenArcs.Sort(static (a, b) => a.Target != b.Target ? a.Target.CompareTo(b.Target) : a.OffBow.CompareTo(b.OffBow));
        int i = 0;
        while (i < screenArcs.Count)
        {
            int j = i;
            double dist = 0;
            while (j < screenArcs.Count && screenArcs[j].Target == screenArcs[i].Target)
                dist += screenArcs[j++].Dist;
            dist /= j - i;
            if (s.Ships.IndexOf(screenArcs[i].Target) is int kt and >= 0)
            {
                var (tp, tf) = s.Ships.PoseAt(kt, world.DrawMs);
                double heading = Earth.Heading(tp, tf), from = screenArcs[i].OffBow - 25, to = screenArcs[j - 1].OffBow + 25;
                int n = Math.Max(8, (int)((to - from) / 4));
                overlay.BeginStrip(MapPalette.TaskMarkFaint, 1f, Dash.Dotted);
                for (int q = 0; q <= n; q++)
                    Point(view, (tp + Earth.Forward(tp, heading + from + (to - from) * q / n) * dist).Normalized() * Earth.Radius);
                overlay.End();
            }
            i = j;
        }
    }

    /// <summary>An armed order's shape before the click, for what's under the cursor.</summary>
    void TaskPreview(in MapView view, Snapshot s)
    {
        if (armedPick == ClickPick.None || !mouseIn || ImGui.GetIO().WantCaptureMouse)
            return;
        var (enemy, own, sea, at) = Under(s, cursorX, cursorY);
        var unit = orderUnits[0];
        var v = s.Ships;
        int kg = v.IndexOf(GuideOf(s, unit));
        if (kg < 0)
            return;
        var (gp, _) = v.PoseAt(kg, world.DrawMs);
        var set = world.Settings;
        switch (armedPick)
        {
            case ClickPick.Screen or ClickPick.Cover or ClickPick.Scout:
            {
                if (own <= 0 || ShipIn(s, own, unit) || v.IndexOf(GuideOf(s, OwnUnitOf(s, own, unit))) is not (int kt and >= 0))
                    return;
                var (tp, tf) = v.PoseAt(kt, world.DrawMs);
                if (armedPick != ClickPick.Scout)
                {
                    // the distance its admiral keeps: where on it is his call (the threat, the matchup)
                    RingM(view, tp, armedPick == ClickPick.Screen ? set.ScreenM : set.CoverM, MapPalette.TaskMarkFaint, 1.2f, Dash.Dotted);
                    return;
                }
                var st = (tp + tf * set.ScoutM).Normalized() * Earth.Radius;   // dead ahead of it, as the order is given
                overlay.BeginStrip(MapPalette.TaskMarkFaint, 1.2f, Dash.Dashed);
                Point(view, gp);
                Point(view, st);
                overlay.End();
                RingM(view, st, 10 * Rp * view.MetresPerPixel, MapPalette.TaskMark, 1.4f, Dash.Dashed);
                return;
            }
            case ClickPick.Deploy:
            {
                if (!sea || own > 0 || enemy > 0)
                    return;
                // the line of battle from its guide on the course: as long as its ships in line at its spacing
                int ships = unit.IsGroup && s.Groups.IndexOfGroup(unit.GroupId) is int g and >= 0 ? ShipsUnder(s.Groups, g) : 1;
                double spacing = unit.IsGroup && s.Groups.IndexOfGroup(unit.GroupId) is int g2 and >= 0 && !s.Groups.Force[g2] ? s.Groups.SpacingM[g2] : 500;
                var dir = Earth.Forward(gp, Earth.Bearing(gp, at));
                var end = (gp + dir * Math.Max(ships * spacing, 2_000)).Normalized() * Earth.Radius;
                overlay.BeginStrip(MapPalette.RouteSelected with { W = 0.8f }, 1.4f, Dash.Dashed);
                Point(view, gp);
                Point(view, end);
                overlay.End();
                return;
            }
            case ClickPick.GoTo or ClickPick.Patrol or ClickPick.Search:
                if (!sea || own > 0 || enemy > 0 || world.Shore.IsLand(at))
                    return;
                overlay.BeginStrip(MapPalette.TaskMarkFaint, 1.2f, Dash.Dashed);
                Point(view, gp);
                Point(view, at);
                overlay.End();
                RingM(view, at, 6 * Rp * view.MetresPerPixel, MapPalette.TaskMark, 1.4f);
                return;
        }
    }

    // the marks' labels, made when what they say changes
    readonly Dictionary<int, ((byte, byte, int) Key, string Text)> markTexts = [];

    /// <summary>The station marks' labels: the unit and its task, beside each mark.</summary>
    void TaskMarkLabels(Snapshot s)
    {
        if (taskMarkCount == 0)
            return;
        var gv = s.Groups;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        ImGui.PushFont(UiFonts.SansSmall);
        for (int i = 0; i < taskMarkCount; i++)
        {
            int g = taskMarks[i].Group;
            if (g >= gv.Count)
                continue;
            var (task, status) = TaskOf(gv, g);
            var key = (task, status, world.Generation);
            if (!markTexts.TryGetValue(gv.Id[g], out var cached) || cached.Key != key)
                markTexts[gv.Id[g]] = cached = (key, task != 0 ? $"{gv.Name[g]} · {TaskText(task, status)}" : gv.Name[g]);
            var at = vp.Pos + new Vector2((float)taskMarks[i].X * sx + 12 * sc, (float)taskMarks[i].Y * sy - 7 * sc);
            MapLabels.Text(dl, at, MapPalette.TaskMark, cached.Text, 1);
        }
        ImGui.PopFont();
    }
}
