using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The world tab's groups (docs/battle/plan.md step 2.4): ctrl+click picks more ships beside the selected one
/// and the strip forms them into a group (the selected ship its guide); a ship in a group shows the group's row
/// (formation, spacing, leave, disband), and its course and speed orders go to the guide, which the rest follow.
/// Every group is labelled at its guide; a group too small on screen to tell its ships apart is drawn as one marker
/// with its label instead of its ships' icons (temporary, like the icons: the zoom bands' symbols come later).</summary>
public sealed unsafe partial class MapScene
{
    readonly List<int> picked = [];   // ships picked beside the selected one (ctrl+click)
    bool[] hidden = new bool[64];     // per snapshot ship: drawn as part of a group's marker instead
    int spacingEdit = 600;

    /// <summary>Where each group's label goes this frame, target pixels (a frame late in the UI: fine).</summary>
    struct Label
    {
        public double X, Y;
        public int Group;
        public bool Collapsed;
    }

    Label[] labels = new Label[16];
    int labelCount;

    static readonly string[] FormationNames = ["Line ahead", "Line abreast", "Echelon to starboard", "Echelon to port"];
    static readonly Vector4 GroupColor = new(0.95f, 0.92f, 0.82f, 0.9f), GroupLine = new(0.95f, 0.92f, 0.82f, 0.25f), PickColor = new(1f, 0.78f, 0.25f, 0.6f);

    /// <summary>A group collapses into one marker when its ships span fewer pixels than this on screen.</summary>
    const double CollapsePx = 30;

    /// <summary>Before the ships are drawn: which groups are too small to show ship by ship (their ships hidden), and
    /// where every group's label goes.</summary>
    void PlaceGroups(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        var gv = s.Groups;
        if (hidden.Length < v.Count)
            hidden = new bool[Math.Max(v.Count, hidden.Length * 2)];
        Array.Clear(hidden, 0, v.Count);
        labelCount = 0;
        for (int g = 0; g < gv.Count; g++)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, gx = 0, gy = 0;
            bool any = false, guideSeen = false;
            int inLine = 0;
            for (int i = 0; i < gv.MemberCount[g]; i++)
            {
                int m = gv.MemberStart[g] + i, k = v.IndexOf(gv.Members[m]);
                if (k < 0 || gv.Detached[m] != Detachment.InLine)
                    continue;   // a detached ship is drawn on its own, wherever it went
                inLine++;
                var (p, _) = v.PoseAt(k, s.Time.Ms);
                if (!view.Project(p, out double x, out double y))
                    continue;
                (x0, y0, x1, y1, any) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y), true);
                if (gv.Members[m] == gv.Guide[g])
                    (gx, gy, guideSeen) = (x, y, true);
            }
            if (!any || !guideSeen)
                continue;
            bool collapsed = inLine > 1 && Math.Max(x1 - x0, y1 - y0) < CollapsePx;
            if (collapsed)
                for (int i = 0; i < gv.MemberCount[g]; i++)
                    if (gv.Detached[gv.MemberStart[g] + i] == Detachment.InLine && v.IndexOf(gv.Members[gv.MemberStart[g] + i]) is int k and >= 0)
                        hidden[k] = true;
            if (labelCount == labels.Length)
                Array.Resize(ref labels, labels.Length * 2);
            labels[labelCount++] = new Label { X = gx, Y = gy, Group = g, Collapsed = collapsed };
        }
    }

    /// <summary>The groups' lines: a collapsed group's marker, and the selected group's ships joined in order.</summary>
    void DrawGroups(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        var gv = s.Groups;
        int mine = gv.IndexOfShip(selected);
        for (int l = 0; l < labelCount; l++)
        {
            ref var lb = ref labels[l];
            if (lb.Collapsed)
            {
                // the marker: a diamond at the guide
                const double R = 9;
                overlay.BeginStrip(lb.Group == mine ? SelectColor : GroupColor, 2f);
                overlay.Point(lb.X, lb.Y - R);
                overlay.Point(lb.X + R, lb.Y);
                overlay.Point(lb.X, lb.Y + R);
                overlay.Point(lb.X - R, lb.Y);
                overlay.Point(lb.X, lb.Y - R);
                overlay.End();
            }
            else if (lb.Group == mine)
            {
                overlay.BeginStrip(GroupLine, 1f);
                for (int i = 0; i < gv.MemberCount[lb.Group]; i++)
                    if (v.IndexOf(gv.Members[gv.MemberStart[lb.Group] + i]) is int k and >= 0)
                        Point(view, v.PoseAt(k, s.Time.Ms).Position);
                overlay.End();
            }
        }
        // the other picked ships
        foreach (int e in picked)
            if (v.IndexOf(e) is int k and >= 0)
                Ring(view, v.PoseAt(k, s.Time.Ms).Position, ShipDrawList.IconLengthPx / 2 + 5, PickColor, 1.5f);
    }

    /// <summary>The groups' labels, drawn under the UI's windows.</summary>
    void GroupLabels(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        var gv = s.Groups;
        for (int l = 0; l < labelCount; l++)
        {
            ref var lb = ref labels[l];
            if (lb.Group >= gv.Count)
                continue;
            var at = new Vector2(vp.Pos.X + (float)(lb.X * sx) + 12, vp.Pos.Y + (float)(lb.Y * sy) - 8);
            int inLine = 0;
            for (int i = 0; i < gv.MemberCount[lb.Group]; i++)
                if (gv.Detached[gv.MemberStart[lb.Group] + i] == Detachment.InLine)
                    inLine++;
            string text = lb.Collapsed ? $"{gv.Name[lb.Group]} ({inLine})" : gv.Name[lb.Group];
            dl.AddText(at + new Vector2(1, 1), 0xC0000000, text);
            dl.AddText(at, ImGui.GetColorU32(GroupColor), text);
        }
    }

    /// <summary>Where the selection's orders go (docs/battle/plan.md step 2.6): to its group, if it's in the group's
    /// line, or else to the ship itself; and the ship that steams them (the group's guide, or the ship).</summary>
    (Unit Unit, int Ship) Commanded(Snapshot s)
    {
        var gv = s.Groups;
        int g = gv.IndexOfShip(selected);
        if (g < 0 || gv.Guide[g] == 0 || DetachmentOf(gv, g, selected) != Detachment.InLine)
            return (Unit.Ship(selected), selected);
        return (Unit.Group(gv.Id[g]), gv.Guide[g]);
    }

    static Detachment DetachmentOf(GroupsView gv, int g, int entity)
    {
        for (int i = 0; i < gv.MemberCount[g]; i++)
            if (gv.Members[gv.MemberStart[g] + i] == entity)
                return gv.Detached[gv.MemberStart[g] + i];
        return Detachment.InLine;
    }

    /// <summary>A click picked a ship: with ctrl it's added to (or taken from) the picked ones beside the selection.</summary>
    void Pick(int hit)
    {
        bool ctrl = (SDL_GetModState() & SDL_Keymod.SDL_KMOD_CTRL) != 0;
        if (!ctrl || selected < 0)
        {
            selected = hit;
            picked.Clear();
            return;
        }
        if (hit < 0 || hit == selected)
            return;
        if (!picked.Remove(hit))
            picked.Add(hit);
    }

    /// <summary>The selected ship's group row, or forming a group of the picked ships.</summary>
    void GroupRow(Snapshot s)
    {
        var gv = s.Groups;
        for (int i = picked.Count - 1; i >= 0; i--)
            if (s.Ships.IndexOf(picked[i]) < 0)
                picked.RemoveAt(i);   // gone
        int g = gv.IndexOfShip(selected);
        if (g >= 0)
        {
            int id = gv.Id[g], guide = gv.Guide[g];
            var detached = DetachmentOf(gv, g, selected);
            ImGui.Text($"{gv.Name[g]}:");
            ImGui.SameLine();
            ImGui.TextDisabled(detached != Detachment.InLine
                ? $"{gv.MemberCount[g]} ships; this one detached{(detached == Detachment.Pinned ? "" : " (an order to the group gathers it in)")}"
                : $"{gv.MemberCount[g]} ships, guide #{guide}{(guide == selected ? " (this)" : "")}");
            ImGui.SameLine();
            if (detached != Detachment.InLine ? ImGui.Button("Rejoin") : ImGui.Button("Detach"))
                world.Send(detached != Detachment.InLine ? world.Ai.Orders.Resume(Unit.Ship(selected)) : world.Ai.Orders.Detach(Unit.Ship(selected)));
            ImGui.SameLine();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150 * UiFonts.Scale);
            int f = (int)gv.Formation[g];
            if (ImGui.Combo("##formation", ref f, FormationNames, FormationNames.Length))
                world.Send(GroupSystem.SetFormation(id, (Formation)f, gv.SpacingM[g]));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * UiFonts.Scale);
            if (!ImGui.IsItemActive())
                spacingEdit = (int)gv.SpacingM[g];
            if (ImGui.InputInt("m apart", ref spacingEdit, 100, 500, ImGuiInputTextFlags.EnterReturnsTrue))
                world.Send(GroupSystem.SetFormation(id, gv.Formation[g], Math.Clamp(spacingEdit, 100, 20_000)));
            ImGui.SameLine();
            if (ImGui.Button("Leave group"))
                world.Send(GroupSystem.Remove(selected));
            ImGui.SameLine();
            if (ImGui.Button("Disband"))
                world.Send(GroupSystem.Disband(id));
            if (picked.Count > 0)
            {
                ImGui.SameLine();
                if (ImGui.Button($"Add {picked.Count} picked"))
                {
                    foreach (int e in picked)
                        world.Send(GroupSystem.Add(id, e));
                    picked.Clear();
                }
            }
        }
        else if (picked.Count > 0)
        {
            if (ImGui.Button($"Form a group of {picked.Count + 1}, this ship guiding"))
            {
                world.Send(GroupSystem.Create(selected, Formation.Column, spacingEdit));
                foreach (int e in picked)
                    world.Send(GroupSystem.Join(e, selected));
                picked.Clear();
            }
        }
        else
            ImGui.TextDisabled("ctrl+click other ships to pick them and form a group");
    }
}
