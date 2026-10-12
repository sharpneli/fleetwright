using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The world tab's groups (docs/battle/plan.md steps 2.4 and 2.6c; placeholders until the command UI of 5.2,
/// docs/battle/command-ui.md): ctrl+click picks more ships beside the selected one, and the strip forms them into a
/// squadron (the selected ship its guide), or their squadrons into a force. The strip's "orders to" picks the unit
/// orders go to: the ship, its squadron or a force above it (by default the highest it's in line with; alt+right-click
/// orders the ship alone). Every group is labelled at its guide; one too small on screen to tell its ships apart is
/// drawn as one marker. Ships and groups the player holds (out of their line) are marked, with a "!" when their
/// superior's orders have changed, and listed in "Held by you" with Resume.</summary>
public sealed unsafe partial class MapScene
{
    readonly List<int> picked = [];   // ships picked beside the selected one (ctrl+click)
    bool[] hidden = new bool[64];     // per snapshot ship: drawn as part of a group's marker instead
    int spacingEdit = 600, forceSpacingEdit = 3000;

    // the strip's "orders to": the selected ship's chain of groups (snapshot indices, its squadron first, then the
    // forces above it), and the level picked for this selection (-1: the default)
    readonly List<int> chain = [];
    int levelPick = -1, levelFor = -1;

    /// <summary>Where each group's label goes this frame, target pixels (a frame late in the UI: fine).</summary>
    struct Label
    {
        public double X, Y;
        public int Group, Depth;
        public bool Collapsed;
    }

    Label[] labels = new Label[16];
    int labelCount;

    // per snapshot group, this frame: collapsed into one marker, its guide ship on screen, how many line ships it has
    bool[] collapsedG = new bool[16];
    readonly List<int> lineShips = [];

    static readonly string[] FormationNames = ["Line ahead", "Line abreast", "Echelon to starboard", "Echelon to port"];
    static readonly Vector4 GroupColor = new(0.95f, 0.92f, 0.82f, 0.9f), GroupLine = new(0.95f, 0.92f, 0.82f, 0.25f), PickColor = new(1f, 0.78f, 0.25f, 0.6f),
        HeldColor = new(0.55f, 0.85f, 1f, 0.8f), AskColor = new(1f, 0.55f, 0.3f, 1f);

    /// <summary>A group collapses into one marker when its ships span fewer pixels than this on screen.</summary>
    const double CollapsePx = 30;

    /// <summary>The ships of group g's line, down the tree (a force's in-line groups' in-line ships), into a list.</summary>
    static void LineShips(GroupsView gv, int g, List<int> into)
    {
        for (int i = 0; i < gv.MemberCount[g]; i++)
        {
            int m = gv.MemberStart[g] + i;
            if (gv.Detached[m] != Detachment.InLine)
                continue;
            if (!gv.Force[g])
                into.Add(gv.Members[m]);
            else if (gv.IndexOfGroup(gv.Members[m]) is int sub and >= 0)
                LineShips(gv, sub, into);
        }
    }

    /// <summary>How many levels of force a group stands on (0 for a squadron): its label goes that many lines down.</summary>
    static int Depth(GroupsView gv, int g)
    {
        int d = 0;
        while (gv.Force[g] && gv.IndexOfGroup(gv.Guide[g]) is int sub and >= 0)
            (g, d) = (sub, d + 1);
        return d;
    }

    /// <summary>Group g's member detachment in its force (in line if it's in none).</summary>
    static Detachment InForce(GroupsView gv, int g)
    {
        if (gv.Parent[g] == 0 || gv.IndexOfGroup(gv.Parent[g]) is not (int p and >= 0))
            return Detachment.InLine;
        return DetachmentOf(gv, p, gv.Id[g]);
    }

    /// <summary>Before the ships are drawn: which groups are too small to show ship by ship (their ships hidden), and
    /// where every group's label goes. A group inside a collapsed one (in its line) has no label of its own.</summary>
    void PlaceGroups(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        var gv = s.Groups;
        if (hidden.Length < v.Count)
            hidden = new bool[Math.Max(v.Count, hidden.Length * 2)];
        Array.Clear(hidden, 0, v.Count);
        if (collapsedG.Length < gv.Count)
            collapsedG = new bool[Math.Max(gv.Count, collapsedG.Length * 2)];
        labelCount = 0;
        for (int g = 0; g < gv.Count; g++)
        {
            collapsedG[g] = false;
            lineShips.Clear();
            LineShips(gv, g, lineShips);
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            bool any = false;
            foreach (int e in lineShips)
                if (v.IndexOf(e) is int k and >= 0 && view.Project(v.PoseAt(k, s.Time.Ms).Position, out double x, out double y))
                    (x0, y0, x1, y1, any) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y), true);
            if (!any)
                continue;
            collapsedG[g] = lineShips.Count > 1 && Math.Max(x1 - x0, y1 - y0) < CollapsePx;
            if (collapsedG[g])
                foreach (int e in lineShips)
                    if (v.IndexOf(e) is int k and >= 0)
                        hidden[k] = true;
        }
        for (int g = 0; g < gv.Count; g++)
        {
            if (Covered(gv, g) || v.IndexOf(gv.GuideShip[g]) is not (int k and >= 0) || !view.Project(v.PoseAt(k, s.Time.Ms).Position, out double gx, out double gy))
                continue;
            if (labelCount == labels.Length)
                Array.Resize(ref labels, labels.Length * 2);
            labels[labelCount++] = new Label { X = gx, Y = gy, Group = g, Depth = Depth(gv, g), Collapsed = collapsedG[g] };
        }
    }

    /// <summary>Whether a group is inside a collapsed group whose line it's in: the outer one's marker stands for it.</summary>
    bool Covered(GroupsView gv, int g)
    {
        while (InForce(gv, g) == Detachment.InLine && gv.Parent[g] != 0 && gv.IndexOfGroup(gv.Parent[g]) is int p and >= 0)
        {
            if (collapsedG[p])
                return true;
            g = p;
        }
        return false;
    }

    /// <summary>The groups' lines: a collapsed group's marker, the selected squadron's ships joined in order, the ships
    /// the player holds ringed (with a "!" ring when their superior's orders changed), and the picked ships.</summary>
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
                // the marker: a diamond at the guide, a larger one for a force
                double r = gv.Force[lb.Group] ? 13 : 9;
                overlay.BeginStrip(lb.Group == mine || chain.Contains(lb.Group) ? SelectColor : GroupColor, 2f);   // the selection's own
                overlay.Point(lb.X, lb.Y - r);
                overlay.Point(lb.X + r, lb.Y);
                overlay.Point(lb.X, lb.Y + r);
                overlay.Point(lb.X - r, lb.Y);
                overlay.Point(lb.X, lb.Y - r);
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
        // ships out of their squadron's line: the player's
        for (int g = 0; g < gv.Count; g++)
            if (!gv.Force[g])
                for (int i = 0; i < gv.MemberCount[g]; i++)
                    if (gv.Detached[gv.MemberStart[g] + i] != Detachment.InLine && v.IndexOf(gv.Members[gv.MemberStart[g] + i]) is int k and >= 0)
                        Ring(view, v.PoseAt(k, s.Time.Ms).Position, ShipDrawList.IconLengthPx / 2 + 9, v.Asked[k] ? AskColor : HeldColor, 1.2f);
        // the other picked ships
        foreach (int e in picked)
            if (v.IndexOf(e) is int k and >= 0)
                Ring(view, v.PoseAt(k, s.Time.Ms).Position, ShipDrawList.IconLengthPx / 2 + 5, PickColor, 1.5f);
    }

    /// <summary>The groups' labels, drawn under the UI's windows: a force's below its guide squadron's, "held" and "!"
    /// on a group the player holds.</summary>
    void GroupLabels(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        var gv = s.Groups;
        float line = ImGui.GetTextLineHeight();
        for (int l = 0; l < labelCount; l++)
        {
            ref var lb = ref labels[l];
            if (lb.Group >= gv.Count)
                continue;
            int g = lb.Group;
            var at = new Vector2(vp.Pos.X + (float)(lb.X * sx) + 12, vp.Pos.Y + (float)(lb.Y * sy) - 8 + lb.Depth * line);
            string text = gv.Name[g];
            if (lb.Collapsed)
            {
                lineShips.Clear();
                LineShips(gv, g, lineShips);
                text += $" ({lineShips.Count})";
            }
            var held = InForce(gv, g);
            if (held != Detachment.InLine)
                text += held == Detachment.Pinned ? " · detached" : " · held";
            dl.AddText(at + new Vector2(1, 1), 0xC0000000, text);
            dl.AddText(at, ImGui.GetColorU32(gv.Asked[g] ? AskColor : GroupColor), gv.Asked[g] ? text + " !" : text);
        }
    }

    // ------------------------------------------------------------------ orders to

    /// <summary>Fills the selected ship's chain of groups, and returns the default level: the highest unit it's in line
    /// with (0: the ship itself, alone or held; 1: its squadron; 2 and up: the forces above).</summary>
    int Chain(Snapshot s)
    {
        var gv = s.Groups;
        chain.Clear();
        int g = gv.IndexOfShip(selected);
        if (g < 0)
            return 0;
        int level = DetachmentOf(gv, g, selected) == Detachment.InLine ? 1 : 0;
        while (g >= 0)
        {
            chain.Add(g);
            if (level == chain.Count && InForce(gv, g) == Detachment.InLine && gv.Parent[g] != 0)
                level++;
            g = gv.Parent[g] != 0 ? gv.IndexOfGroup(gv.Parent[g]) : -1;
        }
        return Math.Min(level, chain.Count);
    }

    /// <summary>The level orders go at: alt for the ship alone, else the one picked for this selection, else the default.</summary>
    int Level(Snapshot s)
    {
        int def = Chain(s);
        if (levelFor != selected)
            (levelFor, levelPick) = (selected, -1);
        if ((SDL_GetModState() & SDL_Keymod.SDL_KMOD_ALT) != 0)
            return 0;
        return levelPick >= 0 && levelPick <= chain.Count ? levelPick : def;
    }

    /// <summary>Where the selection's orders go (docs/battle/plan.md step 2.6c): the unit, and the ship that steams them
    /// (the ship, or the group's guide ship).</summary>
    (Unit Unit, int Ship) Commanded(Snapshot s)
    {
        int level = Level(s);
        if (level == 0)
            return (Unit.Ship(selected), selected);
        var gv = s.Groups;
        int g = chain[level - 1];
        return gv.GuideShip[g] != 0 ? (Unit.Group(gv.Id[g]), gv.GuideShip[g]) : (Unit.Ship(selected), selected);
    }

    /// <summary>The strip's "orders to" row: the ship and each group above it, the one orders go to picked.</summary>
    void LevelRow(Snapshot s)
    {
        var gv = s.Groups;
        int level = Level(s);
        ImGui.Text("Orders to:");
        for (int i = 0; i <= chain.Count; i++)
        {
            ImGui.SameLine();
            string name = i == 0 ? $"this ship #{selected}" : gv.Name[chain[i - 1]];
            if (ImGui.RadioButton($"{name}##level{i}", level == i))
                levelPick = i;
        }
        ImGui.SameLine();
        ImGui.TextDisabled(level < Chain(s) ? "(takes it out of the line above)" : "(alt+right-click: this ship only)");
    }

    static Detachment DetachmentOf(GroupsView gv, int g, int member)
    {
        for (int i = 0; i < gv.MemberCount[g]; i++)
            if (gv.Members[gv.MemberStart[g] + i] == member)
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

    // ------------------------------------------------------------------ rows

    /// <summary>The selected ship's squadron row and its force's row, or forming a squadron of the picked ships, or a
    /// force of their squadrons.</summary>
    void GroupRow(Snapshot s)
    {
        var gv = s.Groups;
        var o = world.Ai.Orders;
        for (int i = picked.Count - 1; i >= 0; i--)
            if (s.Ships.IndexOf(picked[i]) < 0)
                picked.RemoveAt(i);   // gone
        int g = gv.IndexOfShip(selected);
        if (g < 0)
        {
            if (picked.Count > 0)
            {
                if (ImGui.Button($"Form a squadron of {picked.Count + 1}, this ship guiding"))
                {
                    world.Send(GroupSystem.Create(selected, Formation.Column, spacingEdit));
                    foreach (int e in picked)
                        world.Send(GroupSystem.Join(e, selected));
                    picked.Clear();
                }
            }
            else
                ImGui.TextDisabled("ctrl+click other ships to pick them and form a squadron");
            return;
        }

        // the squadron
        int id = gv.Id[g], guide = gv.Guide[g];
        var detached = DetachmentOf(gv, g, selected);
        ImGui.Text($"{gv.Name[g]}:");
        ImGui.SameLine();
        ImGui.TextDisabled(detached != Detachment.InLine
            ? $"{gv.MemberCount[g]} ships; this one {(detached == Detachment.Pinned ? "detached" : "held (an order to the squadron gathers it in)")}{(s.Ships.Asked[s.Ships.IndexOf(selected)] ? "; its orders changed" : "")}"
            : $"{gv.MemberCount[g]} ships, guide #{guide}{(guide == selected ? " (this)" : "")}");
        ImGui.SameLine();
        if (detached != Detachment.InLine ? ImGui.Button("Rejoin##ship") : ImGui.Button("Detach##ship"))
            world.Send(detached != Detachment.InLine ? o.Resume(Unit.Ship(selected)) : o.Detach(Unit.Ship(selected)));
        FormationEdit(gv, g, ref spacingEdit, "##sq");
        ImGui.SameLine();
        if (ImGui.Button("Leave squadron"))
            world.Send(GroupSystem.Remove(selected));
        ImGui.SameLine();
        if (ImGui.Button("Disband##sq"))
            world.Send(GroupSystem.Disband(id));

        // picked ships: into this squadron, or their squadrons into a force with it
        int others = 0;
        foreach (int e in picked)
            if (gv.IndexOfShip(e) is int pg and >= 0 && pg != g)
                others++;
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

        // the force it's in
        int f = gv.Parent[g] != 0 ? gv.IndexOfGroup(gv.Parent[g]) : -1;
        if (f >= 0)
        {
            var inForce = InForce(gv, g);
            ImGui.Text($"{gv.Name[f]}:");
            ImGui.SameLine();
            ImGui.TextDisabled(inForce != Detachment.InLine
                ? $"{gv.MemberCount[f]} groups; this squadron {(inForce == Detachment.Pinned ? "detached" : "held")}{(gv.Asked[g] ? "; its orders changed" : "")}"
                : $"{gv.MemberCount[f]} groups, guide {(gv.IndexOfGroup(gv.Guide[f]) is int fg and >= 0 ? gv.Name[fg] : "-")}");
            ImGui.SameLine();
            if (inForce != Detachment.InLine ? ImGui.Button("Rejoin##sq") : ImGui.Button("Detach##sq"))
                world.Send(inForce != Detachment.InLine ? o.Resume(Unit.Group(id)) : o.Detach(Unit.Group(id)));
            FormationEdit(gv, f, ref forceSpacingEdit, "##force");
            ImGui.SameLine();
            if (ImGui.Button("Leave force"))
                world.Send(GroupSystem.Exit(id));
            ImGui.SameLine();
            if (ImGui.Button("Disband##force"))
                world.Send(GroupSystem.Disband(gv.Id[f]));
        }
        if (others > 0)
        {
            if (f >= 0)
                ImGui.SameLine();
            if (ImGui.Button(f >= 0 ? $"Add the picked ships' squadrons to {gv.Name[f]}" : "Form a force of the picked ships' squadrons, this one guiding"))
            {
                if (f < 0)
                    world.Send(GroupSystem.Form(id, Formation.LineAbreast, forceSpacingEdit));
                foreach (int e in picked)
                    if (gv.IndexOfShip(e) is int pg and >= 0 && pg != g)
                        world.Send(GroupSystem.EnterAlongside(gv.Id[pg], id));
                picked.Clear();
            }
        }
        else if (f < 0)
            ImGui.TextDisabled("ctrl+click ships of other squadrons to form a force of the squadrons");
    }

    /// <summary>A group's formation and spacing, on the line so far.</summary>
    void FormationEdit(GroupsView gv, int g, ref int spacing, string id)
    {
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150 * UiFonts.Scale);
        int form = (int)gv.Formation[g];
        if (ImGui.Combo($"{id}formation", ref form, FormationNames, FormationNames.Length))
            world.Send(GroupSystem.SetFormation(gv.Id[g], (Formation)form, gv.SpacingM[g]));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * UiFonts.Scale);
        if (!ImGui.IsItemActive())
            spacing = (int)gv.SpacingM[g];
        if (ImGui.InputInt($"m apart{id}", ref spacing, 100, 500, ImGuiInputTextFlags.EnterReturnsTrue))
            world.Send(GroupSystem.SetFormation(gv.Id[g], gv.Formation[g], Math.Clamp(spacing, 100, 50_000)));
    }

    /// <summary>"Held by you", top right: every ship and group the player holds out of its line, with what's become of
    /// its superior's orders, Select and Resume. Hidden while there are none.</summary>
    void HeldUi(Snapshot s)
    {
        var gv = s.Groups;
        var v = s.Ships;
        int held = 0;
        for (int m = 0; m < MembersEnd(gv); m++)
            if (gv.Detached[m] != Detachment.InLine)
                held++;
        if (held == 0)
            return;
        var vp = ImGui.GetMainViewport();
        float pad = 8 * UiFonts.Scale;
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X - pad, vp.WorkPos.Y + pad), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowBgAlpha(0.75f);
        if (ImGui.Begin("##held", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                  ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove))
        {
            ImGui.TextDisabled($"Held by you: {held}");
            for (int g = 0; g < gv.Count; g++)
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    int m = gv.MemberStart[g] + i;
                    if (gv.Detached[m] == Detachment.InLine)
                        continue;
                    int member = gv.Members[m];
                    int sub = gv.Force[g] ? gv.IndexOfGroup(member) : -1;
                    int k = gv.Force[g] ? -1 : v.IndexOf(member);
                    string name = gv.Force[g] ? (sub >= 0 ? gv.Name[sub] : $"group {member}") : ShipLabel(s, member);
                    bool asked = gv.Force[g] ? sub >= 0 && gv.Asked[sub] : k >= 0 && v.Asked[k];
                    if (asked)
                        ImGui.TextColored(AskColor, "!");
                    else
                        ImGui.TextDisabled(" ");
                    ImGui.SameLine();
                    ImGui.Text($"{name}");
                    ImGui.SameLine();
                    ImGui.TextDisabled($"{(gv.Detached[m] == Detachment.Pinned ? "detached" : "held")} from {gv.Name[g]}{(asked ? ", its orders changed" : "")}");
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Select##h{m}"))
                    {
                        selected = gv.Force[g] ? (sub >= 0 ? gv.GuideShip[sub] : selected) : member;
                        picked.Clear();
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Resume##h{m}"))
                        world.Send(world.Ai.Orders.Resume(gv.Force[g] ? Unit.Group(member) : Unit.Ship(member)));
                }
        }
        ImGui.End();
    }

    static int MembersEnd(GroupsView gv) => gv.Count == 0 ? 0 : gv.MemberStart[gv.Count - 1] + gv.MemberCount[gv.Count - 1];
}
