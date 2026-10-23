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

    // a group's: a line of bearing too (what a turn together leaves, docs/battle/command.md 5.1b)
    static readonly string[] GroupFormationNames = [.. FormationNames, "Line of bearing"];

    static string RoleName(UnitRole r) => r switch
    {
        UnitRole.Heavy => "battle line",
        UnitRole.Scout => "scouting",
        UnitRole.Screen => "screening",
        UnitRole.Flotilla => "flotilla",
        UnitRole.Auxiliary => "auxiliary",
        _ => "-",
    };
    static readonly (string Label, double Deg)[] TurnButtons =
        [("8 points to port##tt", -90), ("4 points to port##tt", -45), ("4 points to starboard##tt", 45), ("8 points to starboard##tt", 90)];

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
                if (v.IndexOf(e) is int k and >= 0 && view.Project(v.PoseAt(k, world.DrawMs).Position, out double x, out double y))
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
            if (Covered(gv, g) || v.IndexOf(gv.GuideShip[g]) is not (int k and >= 0) || !view.Project(v.PoseAt(k, world.DrawMs).Position, out double gx, out double gy))
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
                // the marker: a filled circle at the guide (ours: the circle, as the rail; the diamond is the enemy's
                // contact), a force's ringed again
                var col = lb.Group == mine || chain.Contains(lb.Group) ? MapPalette.Select : MapPalette.Unit;   // the selection's own
                symbols.Add(SymbolShape.Circle, lb.X, lb.Y, 6.5 * Rp, col with { W = col.W * 0.35f }, col, 2f);
                if (gv.Force[lb.Group])
                    symbols.Ring(lb.X, lb.Y, 10.5 * Rp, col, 1.5f);
            }
            else if (lb.Group == mine)
            {
                overlay.BeginStrip(MapPalette.UnitLine, 1f);
                for (int i = 0; i < gv.MemberCount[lb.Group]; i++)
                    if (v.IndexOf(gv.Members[gv.MemberStart[lb.Group] + i]) is int k and >= 0)
                        Point(view, v.PoseAt(k, world.DrawMs).Position);
                overlay.End();
            }
        }
        // ships the player holds out of their squadron's line
        for (int g = 0; g < gv.Count; g++)
            if (!gv.Force[g])
                for (int i = 0; i < gv.MemberCount[g]; i++)
                    if (Held(gv.Detached[gv.MemberStart[g] + i]) && v.IndexOf(gv.Members[gv.MemberStart[g] + i]) is int k and >= 0)
                    {
                        var p = v.PoseAt(k, world.DrawMs).Position;
                        Ring(view, p, IconLengthAt(view, v.Class[k], p) / 2 + 9 * Rp, v.Asked[k] ? MapPalette.Asked with { W = Pulse } : MapPalette.Held, 1.2f);
                    }
        // ships on the conn, their own or their line's: nothing saves them but the player, so they're never lost from sight
        for (int k = 0; k < v.Count; k++)
            if (v.Conn[k] != 0 && (s.PlayerSide < 0 || v.Side[k] == s.PlayerSide))
            {
                var p = v.PoseAt(k, world.DrawMs).Position;
                Ring(view, p, IconLengthAt(view, v.Class[k], p) / 2 + 13 * Rp, MapPalette.Conn, 1.6f);
            }
        // the other picked ships
        foreach (int e in picked)
            if (v.IndexOf(e) is int k and >= 0)
            {
                var p = v.PoseAt(k, world.DrawMs).Position;
                Ring(view, p, IconLengthAt(view, v.Class[k], p) / 2 + 5 * Rp, MapPalette.Picked, 1.5f);
            }
    }

    /// <summary>The groups' labels, asked of the placer (<see cref="MapLabels"/>): beside the guide, "held" and "!" on a
    /// group the player holds.</summary>
    // the group labels' text, made when what it says changes
    readonly Dictionary<int, ((int, int, Detachment, byte, byte, byte, bool, bool, int) Key, string Text)> groupTexts = [];

    void GroupLabels(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var gv = s.Groups;
        int mine = gv.IndexOfShip(selected);
        ImGui.PushFont(UiFonts.SansSmall);
        for (int l = 0; l < labelCount; l++)
        {
            ref var lb = ref labels[l];
            if (lb.Group >= gv.Count)
                continue;
            var size = ImGui.CalcTextSize(GroupText(gv, l));
            // the selection's own squadron and forces first, then the forces before their squadrons
            int priority = lb.Group == mine || chain.Contains(lb.Group) ? 2 : 3 + Math.Min(lb.Depth, 3);
            mapLabels.Add(LabelKind.Group, gv.Id[lb.Group], l, priority, vp.Pos.X + (float)(lb.X * sx), vp.Pos.Y + (float)(lb.Y * sy), size.X, size.Y,
                12 * sc, lb.Collapsed ? 8 * sc : 0, LabelPlace.Right, LabelPlace.Below, LabelPlace.Left, LabelPlace.Above);
        }
        ImGui.PopFont();
    }

    /// <summary>A group's label: its name in capitals (the mockup's, apart from the ships' names), its ships when
    /// collapsed, held, its task, whose it is, "!"; made when what it says changes.</summary>
    string GroupText(GroupsView gv, int l)
    {
        ref var lb = ref labels[l];
        int g = lb.Group, count = 0;
        if (lb.Collapsed)
        {
            lineShips.Clear();
            LineShips(gv, g, lineShips);
            count = lineShips.Count;
        }
        var held = InForce(gv, g);
        var key = (gv.Id[g], count, Held(held) ? held : Detachment.InLine, gv.Task[g], gv.TaskStatus[g], gv.Commanded[g], gv.Asked[g], gv.Conn[g], world.Generation);
        if (!groupTexts.TryGetValue(gv.Id[g], out var cached) || cached.Key != key)
        {
            string text = gv.Name[g].ToUpperInvariant();
            if (lb.Collapsed)
                text += $" ({count})";
            if (Held(held))
                text += $" · {HeldText(held)}";   // out on its task: the task says so
            if (gv.Task[g] != 0)
                text += $" · {TaskText(gv.Task[g], gv.TaskStatus[g])}";
            if (gv.Commanded[g] != 0)
                text += gv.Commanded[g] == 1 ? " · AI" : " · yours";   // its side's commander's, or taken from him (5.1d)
            if (gv.Conn[g])
                text += " · conn";
            if (gv.Asked[g])
                text += " !";
            groupTexts[gv.Id[g]] = cached = (key, text);
        }
        return cached.Text;
    }

    void DrawGroupLabel(Snapshot s, in MapLabels.Request r, Vector2 at, float alpha)
    {
        var gv = s.Groups;
        int l = r.Index;
        if (l >= labelCount || labels[l].Group >= gv.Count)
            return;
        ImGui.PushFont(UiFonts.SansSmall);
        MapLabels.Text(ImGui.GetBackgroundDrawList(), at, gv.Asked[labels[l].Group] ? MapPalette.Asked with { W = Pulse } : MapPalette.Unit, GroupText(gv, l), alpha);
        ImGui.PopFont();
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

    /// <summary>Whether the player has the conn of the unit itself (a ship's own, not her line's).</summary>
    static bool OwnConn(Snapshot s, Unit unit) => unit.IsShip
        ? s.Ships.IndexOf(unit.Entity) is int k and >= 0 && s.Ships.Conn[k] == 1
        : Array.IndexOf(s.Groups.Id, unit.GroupId, 0, s.Groups.Count) is int g and >= 0 && s.Groups.Conn[g];

    /// <summary>Takes the conn of where the selection's orders go, or gives it back (C, the strip's Conn).</summary>
    void ToggleConn()
    {
        var s = world.Current;
        if (s.Ships.IndexOf(selected) < 0)
            return;
        var (unit, _) = Commanded(s);
        world.Send(world.Orders.Conn(unit, !OwnConn(s, unit)));
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
            ClearPicks();
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
        var o = world.Orders;
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
                    ClearPicks();
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
            ? $"{gv.MemberCount[g]} ships; this one {(detached == Detachment.Nudged ? "held (an order to the squadron gathers it in)" : HeldText(detached))}{(s.Ships.Asked[s.Ships.IndexOf(selected)] ? "; its orders changed" : "")}"
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
                ClearPicks();
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
                ? $"{gv.MemberCount[f]} groups; this squadron {HeldText(inForce)}{(gv.Asked[g] ? "; its orders changed" : "")}"
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
                ClearPicks();
            }
        }
        else if (f < 0)
            ImGui.TextDisabled("ctrl+click ships of other squadrons to form a force of the squadrons");
    }

    /// <summary>A task and how it stands, for a label (docs/battle/command.md 5.1c): "intercept", "go to: there".</summary>
    static string TaskText(byte task, byte status) =>
        taskTexts.TryGetValue((task, status), out var t) ? t : taskTexts[(task, status)] = MakeTaskText(task, status);

    static readonly Dictionary<(byte, byte), string> taskTexts = [];

    static string MakeTaskText(byte task, byte status)
    {
        string what = ((Sim.Ai.Tasks.TaskKind)task).ToString().ToLowerInvariant();
        return (Sim.Ai.Tasks.TaskState)status switch
        {
            Sim.Ai.Tasks.TaskState.Engaged => what + ": fighting",
            Sim.Ai.Tasks.TaskState.InContact => what + ": in contact",
            Sim.Ai.Tasks.TaskState.Done => what + ": done",
            Sim.Ai.Tasks.TaskState.Cant => what + ": can't",
            _ => what,
        };
    }

    /// <summary>A group's formation and spacing, on the line so far.</summary>
    void FormationEdit(GroupsView gv, int g, ref int spacing, string id)
    {
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150 * UiFonts.Scale);
        int form = (int)gv.Formation[g];
        var o = world.Orders;
        var unit = Unit.Group(gv.Id[g]);
        double bearing = GroupSystem.BearingOf(gv.Formation[g], gv.BearingDeg[g]);
        if (ImGui.Combo($"{id}formation", ref form, GroupFormationNames, GroupFormationNames.Length))
        {
            // a force into line ahead makes one line: its squadrons as far apart as their own ships
            double gap = gv.Force[g] && (Formation)form == Formation.Column && gv.IndexOfGroup(gv.Guide[g]) is int sub and >= 0
                ? gv.SpacingM[sub] : gv.SpacingM[g];
            world.Send(o.Form(unit, (Formation)form, gap, bearing));
        }
        if (gv.Formation[g] == Formation.Bearing)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"{bearing:F0}°");
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * UiFonts.Scale);
        if (!ImGui.IsItemActive())
            spacing = (int)gv.SpacingM[g];
        if (ImGui.InputInt($"m apart{id}", ref spacing, 100, 500, ImGuiInputTextFlags.EnterReturnsTrue))
            world.Send(o.Form(unit, gv.Formation[g], Math.Clamp(spacing, 100, 50_000), gv.BearingDeg[g]));
    }

    /// <summary>"Held by you", top right: every ship and group the player holds out of its line (not those out on their
    /// tasks: the AI's), with what's become of its superior's orders, Select and Resume. Hidden while there are none.</summary>
    void HeldUi(Snapshot s)
    {
        var gv = s.Groups;
        var v = s.Ships;
        int held = 0;
        for (int m = 0; m < MembersEnd(gv); m++)
            if (Held(gv.Detached[m]))
                held++;
        if (held == 0)
            return;
        var vp = ImGui.GetMainViewport();
        float pad = 8 * UiFonts.Scale;
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X - pad, HudTop), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowBgAlpha(0.75f);
        if (ImGui.Begin("##held", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                  ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove))
        {
            ImGui.TextDisabled($"Held by you: {held}");
            for (int g = 0; g < gv.Count; g++)
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    int m = gv.MemberStart[g] + i;
                    if (!Held(gv.Detached[m]))
                        continue;
                    int member = gv.Members[m];
                    int sub = gv.Force[g] ? gv.IndexOfGroup(member) : -1;
                    int k = gv.Force[g] ? -1 : v.IndexOf(member);
                    string name = gv.Force[g] ? (sub >= 0 ? gv.Name[sub] : $"group {member}") : ShipLabel(s, member);
                    bool asked = gv.Force[g] ? sub >= 0 && gv.Asked[sub] : k >= 0 && v.Asked[k];
                    if (asked)
                        ImGui.TextColored(MapPalette.Asked, "!");
                    else
                        ImGui.TextDisabled(" ");
                    ImGui.SameLine();
                    ImGui.Text($"{name}");
                    ImGui.SameLine();
                    ImGui.TextDisabled($"{HeldText(gv.Detached[m])} from {gv.Name[g]}{(asked ? ", its orders changed" : "")}");
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Select##h{m}"))
                    {
                        selected = gv.Force[g] ? (sub >= 0 ? gv.GuideShip[sub] : selected) : member;
                        ClearPicks();
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Resume##h{m}"))
                        world.Send(world.Orders.Resume(gv.Force[g] ? Unit.Group(member) : Unit.Ship(member)));
                }
        }
        ImGui.End();
    }

    /// <summary>Out of its line by the player's hand (held or detached), not for its task (the AI's).</summary>
    static bool Held(Detachment d) => d is Detachment.Nudged or Detachment.Pinned;

    static string HeldText(Detachment d) => d switch { Detachment.Pinned => "detached", Detachment.Tasked => "out on its task", _ => "held" };

    static int MembersEnd(GroupsView gv) => gv.Count == 0 ? 0 : gv.MemberStart[gv.Count - 1] + gv.MemberCount[gv.Count - 1];
}
