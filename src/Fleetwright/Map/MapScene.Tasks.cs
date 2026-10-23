using System.Globalization;
using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Combat;
using ImGuiNET;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The task menu (docs/battle/admirals-orders.md O9, the mockup's <c>#tasks</c>): the strip's Task ▾ or T opens
/// it above the strip. Its head is what the unit is for (its role, worked out or set); below, the orders by what they're
/// about (one of ours, the enemy, a place) and the organising of the unit itself, each with its key while the menu is
/// open. One that doesn't apply is greyed with its reason, never removed. Picking an order arms it, and a click on what
/// it's about gives it (<see cref="ArmedClick"/>); the organising ones are done at once. Orders go to each unit selected:
/// the one the strip's orders go to and those ctrl+clicked beside it (<see cref="OrderUnits"/>).</summary>
public sealed partial class MapScene
{
    /// <summary>The menu's items, in its columns' order.</summary>
    enum TaskItem : byte
    {
        Screen, Scout, Cover, Join, Follow, Station,
        Fight, Intercept, Shadow, Attack, Run,
        GoTo, Patrol, Search, Withdraw,
        Divide, DivideFrom, Merge, NewSquadron, FormForce, Leave,
    }

    /// <summary>An item: its label, column, key (and its letter), what a click on the map gives it (none: done at once),
    /// and its tooltip.</summary>
    readonly record struct TaskRow(string Label, int Col, uint Key, string Letter, ClickPick Pick, string Tip);

    static readonly TaskRow[] TaskRows =
    [
        new("Screen…##t", 0, SDLK_S, "S", ClickPick.Screen, "keep off it toward the threat, on a station of its arc every screen of it shares, its admiral choosing the side; counter-attack torpedo craft"),
        new("Scout…##t", 0, SDLK_O, "O", ClickPick.Scout, "ahead of it; shadow what it's no match for"),
        new("Cover…##t", 0, SDLK_C, "C", ClickPick.Cover, "off it on the side of the threat, to support it"),
        new("Join…##t", 0, SDLK_J, "J", ClickPick.Join, "close on it and take a place in its line"),
        new("Follow…##t", 0, SDLK_F, "F", ClickPick.Follow, "station astern of her"),
        new("Station…##t", 0, SDLK_N, "N", ClickPick.Station, "keep station on her where it is now: her bearing and distance held"),
        new("Fight…##t", 1, SDLK_B, "B", ClickPick.Fight, "close to doctrine's range and hold the broadside; turn away when outgunned"),
        new("Intercept…##t", 1, SDLK_I, "I", ClickPick.Intercept, "steer to meet it on what we hold of it, re-solved as the picture moves; done once in reach of a fight"),
        new("Shadow…##t", 1, SDLK_H, "H", ClickPick.Shadow, "keep it in sight and report, out of its reach"),
        new("Attack…##t", 1, SDLK_A, "A", ClickPick.Attack, "the flotilla's attack: gain a position ahead of its beam, run in, fire together on a signal, turn away"),
        new("Run from…##t", 1, SDLK_R, "R", ClickPick.Run, "away from it at all it makes"),
        new("Go to…##t", 2, SDLK_G, "G", ClickPick.GoTo, "steam there; done on arrival"),
        new("Patrol…##t", 2, SDLK_P, "P", ClickPick.Patrol, "up and down from here to there"),
        new("Search…##t", 2, SDLK_E, "E", ClickPick.Search, "a scouting line spaced by the visibility, a ladder about the point; done on contact"),
        new("Withdraw##t", 2, SDLK_W, "W", ClickPick.None, "break off and go home, away from any enemy first"),
        new("Divide in two##t", 3, SDLK_D, "D", ClickPick.None, "two divisions under its admiral, each its own squadron to sail and fight as one; the unit keeps its name and orders"),
        new("Divide from her…##t", 3, SDLK_V, "V", ClickPick.DivideFrom, "two divisions, split where you choose: click a ship of it, she and those astern of her the second"),
        new("Merge divisions##t", 3, SDLK_M, "M", ClickPick.None, "its divisions back into one squadron, the same unit"),
        new("New squadron of selected##t", 3, SDLK_Q, "Q", ClickPick.None, "this ship and those you ctrl+clicked out of their squadrons into a new one, this one guiding"),
        new("Form a force with selected##t", 3, SDLK_K, "K", ClickPick.None, "this unit and those you ctrl+clicked under one admiral, this one guiding"),
        new("Leave its force##t", 3, SDLK_L, "L", ClickPick.None, "out of its force (a ship out of her squadron): on its own"),
    ];

    static readonly (string Title, string How)[] TaskCols = [("ONE OF OURS", "click your ship"), ("THE ENEMY", "click an enemy"), ("A PLACE", "click the sea"), ("ORGANISE", "this unit")];

    bool taskOpen;
    float stripTop;   // the strip's top this frame: the menu sits on it

    // groups ctrl+clicked in the rail beside the selection (ids): orders go to each
    readonly List<int> pickedGroups = [];

    // the units the orders go to, this frame (OrderUnits)
    readonly List<Unit> orderUnits = [];

    /// <summary>The selection's picks cleared: the ships and the rail's groups ctrl+clicked beside it.</summary>
    void ClearPicks()
    {
        picked.Clear();
        pickedGroups.Clear();
    }

    /// <summary>The units the selection's orders go to: where the strip's go (<see cref="Commanded"/>), then for each ship
    /// ctrl+clicked beside it the unit she's in at the same level (herself at a ship's), then each group ctrl+clicked in
    /// the rail; none twice.</summary>
    List<Unit> OrderUnits(Snapshot s)
    {
        orderUnits.Clear();
        var (unit, _) = Commanded(s);
        orderUnits.Add(unit);
        var gv = s.Groups;
        int up = unit.IsGroup ? Math.Max(chain.IndexOf(gv.IndexOfGroup(unit.GroupId)), 0) : -1;
        foreach (int e in picked)
        {
            if (s.Ships.IndexOf(e) < 0)
                continue;
            var u = Unit.Ship(e);
            if (up >= 0 && gv.IndexOfShip(e) is int g and >= 0)
            {
                for (int i = 0; i < up && gv.Parent[g] != 0 && gv.IndexOfGroup(gv.Parent[g]) is int p and >= 0; i++)
                    g = p;
                u = Unit.Group(gv.Id[g]);
            }
            if (!orderUnits.Contains(u))
                orderUnits.Add(u);
        }
        foreach (int id in pickedGroups)
            if (gv.IndexOfGroup(id) >= 0 && !orderUnits.Contains(Unit.Group(id)))
                orderUnits.Add(Unit.Group(id));
        return orderUnits;
    }

    /// <summary>A unit's name: a group's, or a ship's.</summary>
    string UnitName(Snapshot s, Unit u) => u.IsGroup && s.Groups.IndexOfGroup(u.GroupId) is int g and >= 0 ? s.Groups.Name[g] : TargetLabel(s, u.Entity);

    /// <summary>The ship a unit stands for on the sea: a ship herself, a group's guide (0 if it has none).</summary>
    static int GuideOf(Snapshot s, Unit u) =>
        u.IsShip ? u.Entity : s.Groups.IndexOfGroup(u.GroupId) is int g and >= 0 ? s.Groups.GuideShip[g] : 0;

    /// <summary>The unit a ship stands for when one of ours is ordered about her (screened, scouted for, covered, joined):
    /// her squadron, and the force above it while she's in its line, but never a unit holding the one ordered (the
    /// flotilla of a fleet screens the fleet's battle squadron, not the fleet). A ship in no squadron is herself.</summary>
    static Unit OwnUnitOf(Snapshot s, int ship, Unit ordered)
    {
        var gv = s.Groups;
        int g = gv.IndexOfShip(ship);
        if (g < 0)
            return Unit.Ship(ship);
        while (InForce(gv, g) == Detachment.InLine && gv.Parent[g] != 0 && gv.IndexOfGroup(gv.Parent[g]) is int p and >= 0 && !Holds(gv, p, ordered))
            g = p;
        return Unit.Group(gv.Id[g]);
    }

    /// <summary>Whether group g holds a unit (a ship or a group), down its tree, or is it.</summary>
    static bool Holds(GroupsView gv, int g, Unit u)
    {
        if (u.IsGroup && gv.Id[g] == u.GroupId)
            return true;
        for (int i = 0; i < gv.MemberCount[g]; i++)
        {
            int m = gv.Members[gv.MemberStart[g] + i];
            if (gv.Force[g] ? gv.IndexOfGroup(m) is int sub and >= 0 && Holds(gv, sub, u) : u.IsShip && m == u.Entity)
                return true;
        }
        return false;
    }

    /// <summary>Whether a ship is in a unit (is it, or in its tree).</summary>
    static bool ShipIn(Snapshot s, int ship, Unit u) =>
        u.IsShip ? u.Entity == ship : s.Groups.IndexOfGroup(u.GroupId) is int g and >= 0 && Holds(s.Groups, g, Unit.Ship(ship));

    /// <summary>A unit's role (the AI's), and whether the player set it: a group's, a ship alone's; a ship in a squadron
    /// has none of her own (None).</summary>
    static (UnitRole Role, bool Set) RoleOf(Snapshot s, Unit u)
    {
        if (u.IsGroup)
            return s.Groups.IndexOfGroup(u.GroupId) is int g and >= 0 ? ((UnitRole)s.Groups.Role[g], s.Groups.RoleSet[g]) : default;
        int k = s.Ships.IndexOf(u.Entity);
        return k >= 0 && s.Groups.IndexOfShip(u.Entity) < 0 ? ((UnitRole)s.Ships.Role[k], s.Ships.RoleSet[k]) : default;
    }

    /// <summary>Why an item can't be given to the units selected (greyed with it, never removed), or null.</summary>
    string? WhyNot(Snapshot s, TaskItem item)
    {
        var gv = s.Groups;
        var unit = orderUnits[0];
        int g = unit.IsGroup ? gv.IndexOfGroup(unit.GroupId) : -1;
        switch (item)
        {
            case TaskItem.Attack:
                foreach (var u in orderUnits)
                    if (RoleOf(s, u).Role != UnitRole.Flotilla && !(u.IsShip && gv.IndexOfShip(u.Entity) >= 0))
                        return "a flotilla's: set it as one to send it in";
                return null;
            case TaskItem.Divide or TaskItem.DivideFrom:
                return g < 0 ? "a ship: divide her squadron" : gv.Force[g] ? "a force: divide one of its squadrons" : gv.MemberCount[g] < 2 ? "one ship" : null;
            case TaskItem.Merge:
                if (g < 0 || !gv.Force[g])
                    return "no divisions to merge";
                for (int i = 0; i < gv.MemberCount[g]; i++)
                    if (gv.IndexOfGroup(gv.Members[gv.MemberStart[g] + i]) is int sub and >= 0 && gv.Force[sub])
                        return "a force in it: merge that first";
                return null;
            case TaskItem.NewSquadron:
                return picked.Count == 0 ? "ctrl+click the ships" : null;
            case TaskItem.FormForce:
                if (g < 0)
                    return "a ship: pick her squadron";
                for (int i = 1; i < orderUnits.Count; i++)
                    if (orderUnits[i].IsGroup)
                        return null;
                return "ctrl+click the squadrons to form it with";
            case TaskItem.Leave:
                return g >= 0 ? (gv.Parent[g] == 0 ? "in no force" : null) : gv.IndexOfShip(unit.Entity) < 0 ? "in no squadron" : null;
            default:
                return null;
        }
    }

    /// <summary>An item picked: armed for its click, or done now.</summary>
    void PickItem(Snapshot s, TaskItem item)
    {
        if (WhyNot(s, item) != null)
            return;
        taskOpen = false;
        var row = TaskRows[(int)item];
        if (row.Pick != ClickPick.None)
        {
            Arm(row.Pick);
            return;
        }
        var gv = s.Groups;
        var o = world.Orders;
        var unit = orderUnits[0];
        int g = unit.IsGroup ? gv.IndexOfGroup(unit.GroupId) : -1;
        switch (item)
        {
            case TaskItem.Withdraw:
                foreach (var u in orderUnits)
                    world.Send(o.WithdrawHome(u));
                break;
            case TaskItem.Divide:
                world.Send(GroupSystem.Divide(unit.GroupId, (gv.MemberCount[g] + 1) / 2));
                break;
            case TaskItem.Merge:
                world.Send(GroupSystem.Merge(unit.GroupId));
                break;
            case TaskItem.NewSquadron:
                world.Send(GroupSystem.Create(selected, Formation.Column, spacingEdit));
                foreach (int e in picked)
                    world.Send(GroupSystem.Join(e, selected));
                ClearPicks();
                break;
            case TaskItem.FormForce:
                world.Send(GroupSystem.Form(unit.GroupId, Formation.LineAbreast, forceSpacingEdit));
                for (int i = 1; i < orderUnits.Count; i++)
                    if (orderUnits[i].IsGroup)
                        world.Send(GroupSystem.EnterAlongside(orderUnits[i].GroupId, unit.GroupId));
                ClearPicks();
                break;
            case TaskItem.Leave:
                world.Send(g >= 0 ? GroupSystem.Exit(unit.GroupId) : GroupSystem.Remove(unit.Entity));
                break;
        }
    }

    /// <summary>A key while the menu is open: its item's (true), or not one of them.</summary>
    bool TaskKey(uint key)
    {
        if (!taskOpen)
            return false;
        for (int i = 0; i < TaskRows.Length; i++)
            if (TaskRows[i].Key == key)
            {
                var s = world.Current;
                OrderUnits(s);
                PickItem(s, (TaskItem)i);
                return true;
            }
        return false;
    }

    // the menu's head, kept while it names the same
    (int Unit, int More, int Generation) taskHeadKey = (0, -1, -1);
    string taskHeadText = "", taskFootText = "";

    /// <summary>The menu, above the strip, while it's open and one of the player's ships is selected.</summary>
    void TaskMenu(Snapshot s)
    {
        if (!taskOpen)
            return;
        var gv = s.Groups;
        var units = OrderUnits(s);
        var unit = units[0];
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale;
        ImGui.SetNextWindowPos(new Vector2(StripCentre(vp), stripTop - 6 * sc), ImGuiCond.Always, new Vector2(0.5f, 1));
        ImGui.SetNextWindowBgAlpha(0.95f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 7) * sc);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 3) * sc);
        if (ImGui.Begin("##tasks", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                   ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove))
        {
            if ((unit.Id, units.Count, world.Generation) != taskHeadKey)
            {
                taskHeadKey = (unit.Id, units.Count, world.Generation);
                taskHeadText = units.Count > 1 ? $"{UnitName(s, unit)} + {units.Count - 1} more" : UnitName(s, unit);
                taskFootText = (units.Count > 1 ? $"An order goes to each of the {units.Count}: Screen spreads them over the arc, one station each. " : "") +
                    "Its admiral carries an order out: stations, turns, the counter-attack, when it's done, and reports how it stands. " +
                    "An order of yours holds the unit (yours, not the commander's) until you hand it back. Organising changes the unit itself, as its admirals may. " +
                    "Letters pick while this is open; T or Esc shuts it.";
            }
            // the head: what it's for
            ImGui.PushFont(UiFonts.SansBold);
            ImGui.TextColored(DesignerTheme.Bright, taskHeadText);
            ImGui.PopFont();
            ImGui.SameLine(0, 12 * sc);
            var (role, set) = RoleOf(s, unit);
            bool hasRole = unit.IsGroup || gv.IndexOfShip(unit.Entity) < 0;
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("WHAT IT'S FOR");
            ImGui.PopFont();
            ImGui.SameLine(0, 6 * sc);
            if (hasRole)
            {
                for (int i = 0; i < Roles.Settable.Length; i++)
                    if (Seg(RoleLabels[i], role == Roles.Settable[i], first: i == 0))
                        foreach (var u in units)
                            world.Send(world.Orders.Role(u, Roles.Settable[i]));
                ImGui.SameLine(0, 12 * sc);
                ImGui.PushFont(UiFonts.SansSmall);
                if (set)
                {
                    ImGui.TextColored(DesignerTheme.Brass, "set by you ·");
                    ImGui.SameLine(0, 4 * sc);
                    if (ImGui.SmallButton("work it out"))
                        foreach (var u in units)
                            world.Send(world.Orders.Role(u, UnitRole.None));
                }
                else
                    ImGui.TextDisabled("worked out from its ships");
                ImGui.PopFont();
            }
            else
                ImGui.TextDisabled("her squadron's (orders to it: double-click her)");

            // the columns
            ImGui.Separator();
            float colW = 210 * sc;
            if (ImGui.BeginTable("##taskcols", TaskCols.Length, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
            {
                for (int c = 0; c < TaskCols.Length; c++)
                    ImGui.TableSetupColumn(TaskCols[c].Title, ImGuiTableColumnFlags.WidthFixed, colW);
                ImGui.TableNextRow();
                for (int c = 0; c < TaskCols.Length; c++)
                {
                    ImGui.TableSetColumnIndex(c);
                    if (c == 3)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.025f)));
                    ImGui.PushFont(UiFonts.SansSmall);
                    ImGui.TextColored(c == 3 ? Friend : DesignerTheme.Brass, TaskCols[c].Title);
                    ImGui.SameLine(0, 6 * sc);
                    ImGui.TextDisabled(TaskCols[c].How);
                    ImGui.PopFont();
                    for (int i = 0; i < TaskRows.Length; i++)
                        if (TaskRows[i].Col == c)
                            TaskButton(s, (TaskItem)i, colW);
                    if (c == 2)
                    {
                        // the search patterns laid by hand (a route of the unit's own, not a task)
                        ImGui.PushFont(UiFonts.SansSmall);
                        if (ImGui.SmallButton("a pattern by hand…##pattern"))
                            ImGui.OpenPopup("##search");
                        ImGui.PopFont();
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("a ladder, a square, a sector or a patrol line from where it is, as its route");
                        if (ImGui.BeginPopup("##search"))
                        {
                            var (_, cmd) = Commanded(s);
                            int kc = s.Ships.IndexOf(cmd);
                            if (kc >= 0)
                                SearchRow(s, unit, kc);
                            ImGui.EndPopup();
                        }
                    }
                }
                ImGui.EndTable();
            }
            ImGui.Separator();
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + colW * TaskCols.Length);
            ImGui.TextDisabled(taskFootText);
            ImGui.PopTextWrapPos();
            ImGui.PopFont();
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    /// <summary>One of the menu's items: its label and key, greyed with its reason when it doesn't apply.</summary>
    void TaskButton(Snapshot s, TaskItem item, float width)
    {
        var row = TaskRows[(int)item];
        string? why = WhyNot(s, item);
        bool armed = row.Pick != ClickPick.None && armedPick == row.Pick;
        ImGui.BeginDisabled(why != null);
        if (armed)
            ImGui.PushStyleColor(ImGuiCol.Text, ArmedText);
        bool hit = ImGui.Selectable(row.Label, armed, ImGuiSelectableFlags.None, new Vector2(width - 8 * UiFonts.Scale, 0));
        if (armed)
            ImGui.PopStyleColor();
        var (min, max) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        ImGui.PushFont(UiFonts.Mono);
        var kw = ImGui.CalcTextSize(row.Letter).X;
        ImGui.GetWindowDrawList().AddText(new Vector2(max.X - kw - 4 * UiFonts.Scale, min.Y), ImGui.GetColorU32(DesignerTheme.Muted), row.Letter);
        ImGui.PopFont();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(row.Tip);
        if (why != null)
        {
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextDisabled(why);
            ImGui.PopFont();
        }
        if (hit)
            PickItem(s, item);
    }

    /// <summary>The role switch's labels, by <see cref="Roles.Settable"/>.</summary>
    static readonly string[] RoleLabels = [.. Roles.Settable.Select(r => $"{RoleName(r)}##role")];

    // ------------------------------------------------------------------ the picks given

    /// <summary>An armed task's click: given to each unit selected (true), or a miss (false: it stays armed). Own: the
    /// ship of ours clicked; enemy: the enemy; at: the sea's point.</summary>
    bool GiveTask(Snapshot s, ClickPick pick, int own, int enemy, bool sea, Vec3d at)
    {
        var o = world.Orders;
        var units = OrderUnits(s);
        var unit = units[0];
        var (lat, lon) = sea ? Earth.LatLon(at) : default;
        bool land = sea && world.Shore.IsLand(at);
        switch (pick)
        {
            case ClickPick.Screen or ClickPick.Scout or ClickPick.Cover or ClickPick.Join:
                if (own <= 0 || ShipIn(s, own, unit))
                    return false;
                foreach (var u in units)
                {
                    if (ShipIn(s, own, u))
                        continue;
                    var of = OwnUnitOf(s, own, u);
                    world.Send(pick switch
                    {
                        ClickPick.Screen => o.Screen(u, of),
                        ClickPick.Scout => o.Scout(u, of),
                        ClickPick.Cover => o.Cover(u, of),
                        _ => o.Join(u, of),
                    });
                }
                return true;
            case ClickPick.Station:
                if (own <= 0 || ShipIn(s, own, unit))
                    return false;
                foreach (var u in units)
                    if (StationOn(s, u, own) is { } st)
                        world.Send(o.Station(u, Unit.Ship(own), st.Astern, st.Abeam));
                return true;
            case ClickPick.Fight or ClickPick.Intercept or ClickPick.Shadow or ClickPick.Attack:
                if (enemy <= 0)
                    return false;
                foreach (var u in units)
                    world.Send(pick switch
                    {
                        ClickPick.Fight => o.Fight(u, Unit.Ship(enemy)),
                        ClickPick.Intercept => o.Intercept(u, enemy),
                        ClickPick.Shadow => o.Shadow(u, Unit.Ship(enemy)),
                        _ => o.Attack(u, enemy),
                    });
                return true;
            case ClickPick.GoTo or ClickPick.Patrol or ClickPick.Search:
                if (!sea || land || enemy > 0 || own > 0)
                    return false;
                foreach (var u in units)
                    world.Send(pick switch
                    {
                        ClickPick.GoTo => o.GoTo(u, lat, lon),
                        ClickPick.Patrol => o.Patrol(u, lat, lon),
                        _ => o.Search(u, lat, lon),
                    });
                return true;
            case ClickPick.Deploy:
                if (enemy <= 0 && (!sea || own > 0))
                    return false;
                foreach (var u in units)
                    world.Send(o.Deploy(u, enemy > 0 ? double.NaN : DeployCourse(s, u, at)));
                return true;
            case ClickPick.DivideFrom:
                if (own <= 0 || DivideAt(s, unit, own) is not (int first and > 0))
                    return false;
                world.Send(GroupSystem.Divide(unit.GroupId, first));
                return true;
        }
        return false;
    }

    /// <summary>Where a unit keeps station on a ship of ours, as it lies from her now: metres astern of her and to her
    /// starboard (null if either is gone).</summary>
    (double Astern, double Abeam)? StationOn(Snapshot s, Unit u, int on)
    {
        int ks = s.Ships.IndexOf(GuideOf(s, u)), kt = s.Ships.IndexOf(on);
        if (ks < 0 || kt < 0)
            return null;
        var (p, _) = s.Ships.PoseAt(ks, world.DrawMs);
        var (t, f) = s.Ships.PoseAt(kt, world.DrawMs);
        var d = p - t;
        var starboard = Vec3d.Cross(f, t.Normalized());
        return (-Vec3d.Dot(d, f), Vec3d.Dot(d, starboard));
    }

    /// <summary>The course a unit deploys on toward a point: the bearing from its guide.</summary>
    double DeployCourse(Snapshot s, Unit u, Vec3d at)
    {
        int k = s.Ships.IndexOf(GuideOf(s, u));
        if (k < 0)
            return double.NaN;
        var (p, _) = s.Ships.PoseAt(k, world.DrawMs);
        return (Earth.Bearing(p, at) % 360 + 360) % 360;
    }

    /// <summary>Where a squadron divides from one of its ships: her place in it (0: its guide, or not hers: -1).</summary>
    static int DivideAt(Snapshot s, Unit unit, int ship)
    {
        var gv = s.Groups;
        if (!unit.IsGroup || gv.IndexOfGroup(unit.GroupId) is not (int g and >= 0) || gv.Force[g])
            return -1;
        for (int i = 0; i < gv.MemberCount[g]; i++)
            if (gv.Members[gv.MemberStart[g] + i] == ship)
                return i;
        return -1;
    }

    // ------------------------------------------------------------------ the cursor's words for them

    /// <summary>A pick's verb at the cursor (its head), and what a click must find for it.</summary>
    static string PickVerb(ClickPick pick) => pick switch
    {
        ClickPick.Engage => "ENGAGE",
        ClickPick.Torpedoes => "TORPEDOES AT",
        ClickPick.Follow => "FOLLOW",
        ClickPick.Run => "RUN FROM",
        ClickPick.Screen => "SCREEN",
        ClickPick.Scout => "SCOUT",
        ClickPick.Cover => "COVER",
        ClickPick.Join => "JOIN",
        ClickPick.Station => "STATION",
        ClickPick.Fight => "FIGHT",
        ClickPick.Intercept => "INTERCEPT",
        ClickPick.Shadow => "SHADOW",
        ClickPick.Attack => "ATTACK",
        ClickPick.GoTo => "GO TO",
        ClickPick.Patrol => "PATROL",
        ClickPick.Search => "SEARCH",
        ClickPick.Deploy => "DEPLOY",
        ClickPick.DivideFrom => "DIVIDE FROM",
        _ => "",
    };

    static string PickClick(ClickPick pick) => pick switch
    {
        ClickPick.Engage or ClickPick.Torpedoes or ClickPick.Fight or ClickPick.Attack => "click an enemy",
        ClickPick.Intercept or ClickPick.Shadow => "click a contact, seen or reported",
        ClickPick.Run => "click a ship",
        ClickPick.Follow or ClickPick.Station => "click your ship",
        ClickPick.Screen or ClickPick.Scout or ClickPick.Cover => "click your ship: her squadron, or the force she's in",
        ClickPick.Join => "click a ship of the squadron or force to join",
        ClickPick.GoTo or ClickPick.Search => "click the sea",
        ClickPick.Patrol => "click the line's far end",
        ClickPick.Deploy => "click where the line should steer, or the enemy (across his course)",
        ClickPick.DivideFrom => "click a ship of it: she and those astern of her the second division",
        _ => "",
    };

    /// <summary>What an armed pick's click would give where the cursor is: the words, or null for a miss (the pick's
    /// own words then). Made only when <see cref="CursorVerb"/>'s key changes.</summary>
    string? TaskVerb(Snapshot s, ClickPick pick, int own, int enemy, bool sea, Vec3d at, int tenths, int course)
    {
        var units = OrderUnits(s);
        var unit = units[0];
        string verb = PickVerb(pick);
        var set = world.Settings;
        switch (pick)
        {
            case ClickPick.Screen or ClickPick.Scout or ClickPick.Cover or ClickPick.Join:
            {
                if (own <= 0 || ShipIn(s, own, unit))
                    return null;
                string of = UnitName(s, OwnUnitOf(s, own, unit));
                int screens = Screens(s, units);
                return pick switch
                {
                    ClickPick.Screen => string.Format(CultureInfo.InvariantCulture, "SCREEN {0} · {1:0.#} km · {2}", of, set.ScreenM / 1000,
                        screens == 1 ? "on the bearing" : screens == 2 ? "2 squadrons, either bow" : $"{screens} squadrons over the arc"),
                    ClickPick.Scout => string.Format(CultureInfo.InvariantCulture, "SCOUT for {0} · {1:0.#} km ahead", of, set.ScoutM / 1000),
                    ClickPick.Cover => string.Format(CultureInfo.InvariantCulture, "COVER {0} · {1:0.#} km off, toward the threat", of, set.CoverM / 1000),
                    _ => $"JOIN {of}",
                };
            }
            case ClickPick.Follow:
                return own > 0 && own != selected ? $"FOLLOW {TargetLabel(s, own)} · station astern" : null;
            case ClickPick.Station:
            {
                if (own <= 0 || ShipIn(s, own, unit) || StationOn(s, unit, own) is not { } st)
                    return null;
                double off = (Math.Atan2(st.Abeam, -st.Astern) * 180 / Math.PI + 360) % 360;
                return string.Format(CultureInfo.InvariantCulture, "STATION on {0} · {1:0.0} km, {2:000}° off her bow", TargetLabel(s, own),
                    Math.Sqrt(st.Astern * st.Astern + st.Abeam * st.Abeam) / 1000, off);
            }
            case ClickPick.Fight or ClickPick.Intercept or ClickPick.Shadow or ClickPick.Attack or ClickPick.Run or ClickPick.Engage or ClickPick.Torpedoes:
            {
                int target = pick == ClickPick.Run && enemy <= 0 && own != selected ? own : enemy;
                if (target <= 0)
                    return null;
                string what = string.Format(CultureInfo.InvariantCulture, "{0} {1} · {2:0.0} km", verb, TargetLabel(s, target), tenths / 10.0);
                return pick == ClickPick.Attack ? $"{what} · {BoatsOf(s, units)} boats" : what;
            }
            case ClickPick.GoTo or ClickPick.Patrol or ClickPick.Search:
            {
                if (!sea || own > 0 || enemy > 0)
                    return null;
                if (world.Shore.IsLand(at))
                    return "land";
                int k = s.Ships.IndexOf(GuideOf(s, unit));
                double ms = k >= 0 ? s.Ships.Speed[k] : 0;
                if (pick != ClickPick.GoTo || ms < 0.5)
                    return string.Format(CultureInfo.InvariantCulture, "{0} {1:000}° · {2:0.0} km", verb, course, tenths / 10.0);
                int minutes = (int)Math.Round(tenths * 100 / ms / 60);
                return string.Format(CultureInfo.InvariantCulture, "GO TO {0:000}° · {1:0.0} km · {2}:{3:00} at {4:0} kn", course, tenths / 10.0,
                    minutes / 60, minutes % 60, ms / Handling.MsPerKnot);
            }
            case ClickPick.Deploy:
                if (enemy > 0)
                    return $"DEPLOY across {TargetLabel(s, enemy)}'s course · the wing column furthest along it leads";
                if (!sea || own > 0)
                    return null;
                return string.Format(CultureInfo.InvariantCulture, "DEPLOY {0:000}° · the wing column furthest along it leads", course);
            case ClickPick.DivideFrom:
            {
                int at0 = own > 0 ? DivideAt(s, unit, own) : -1;
                if (at0 <= 0)
                    return null;
                int n = s.Groups.IndexOfGroup(unit.GroupId) is int g and >= 0 ? s.Groups.MemberCount[g] : 0;
                return $"DIVIDE FROM {TargetLabel(s, own)} · {at0} and {n - at0}";
            }
        }
        return null;
    }

    /// <summary>How many stations a screen by the units selected takes on its arc: a force hands it to each of its groups
    /// (docs/battle/admirals-orders.md O1c), a squadron or a ship takes one.</summary>
    static int Screens(Snapshot s, List<Unit> units)
    {
        int n = 0;
        foreach (var u in units)
            n += u.IsGroup && s.Groups.IndexOfGroup(u.GroupId) is int g and >= 0 && s.Groups.Force[g] ? s.Groups.MemberCount[g] : 1;
        return n;
    }

    /// <summary>How many ships the units selected have between them.</summary>
    static int BoatsOf(Snapshot s, List<Unit> units)
    {
        int n = 0;
        foreach (var u in units)
            n += u.IsShip ? 1 : s.Groups.IndexOfGroup(u.GroupId) is int g and >= 0 ? ShipsUnder(s.Groups, g) : 0;
        return n;
    }

    static int ShipsUnder(GroupsView gv, int g)
    {
        if (!gv.Force[g])
            return gv.MemberCount[g];
        int n = 0;
        for (int i = 0; i < gv.MemberCount[g]; i++)
            if (gv.IndexOfGroup(gv.Members[gv.MemberStart[g] + i]) is int sub and >= 0)
                n += ShipsUnder(gv, sub);
        return n;
    }
}
