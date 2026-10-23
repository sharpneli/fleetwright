using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The forces rail (docs/map-ui/battle-hud.md U4), at the left: the player's units and their ships (a unit's
/// head selects it for orders and opens its card; a ship's row selects her, twice centres the map on her), each ship
/// with her flotation, fires, what she's firing at or why she's falling out, her speed and hits; then the enemy in the
/// player's picture, nearest first from the ship the player last chose, with what's seen of each. Its text is written
/// without garbage (<see cref="U8"/>).
/// <para>Organising by drag and drop (docs/battle/admirals-orders.md O8c): a ship onto a squadron joins it at its end,
/// onto a ship of one ahead of her (in her own, a move), onto a ship alone makes a squadron of the two; a squadron or a
/// force onto another unit goes under its admiral (<see cref="GroupSystem.Subordinate"/>); either onto "on its own" leaves
/// its squadron or force. What a drop does is told under the cursor before it's let go.</para></summary>
public sealed partial class MapScene
{
    /// <summary>The player's ship the enemy's ranges are from: the selection when it's hers, else the last one.</summary>
    int lastOwn = -1;
    /// <summary>The ship whose unit's card shows (the rail's unit head chose it), while she stays selected.</summary>
    int squadronCardOf = -1;
    long railClickMs;
    int railClickShip = -1;
    // the enemy rows' order: nearest first, made once a frame
    readonly List<(double Range, int Index)> enemyOrder = [];
    /// <summary>The units whose ships the rail lists (with many ships, only these and the selection's are open), and
    /// the player's ships this frame.</summary>
    readonly HashSet<int> railOpen = [];
    int railOwn;
    const int RailAllOpen = 12;
    /// <summary>What the rail's drag carries: a ship (an entity) or a group (an id); 0 for neither.</summary>
    int dragShip, dragGroup;
    const string RailPayload = "FW_RAIL";

    void Rail(Snapshot s)
    {
        var v = s.Ships;
        var gv = s.Groups;
        int side = s.PlayerSide;
        if (s.Ships.IndexOf(selected) is int ks and >= 0 && (side < 0 || v.Side[ks] == side))
            lastOwn = selected;
        if (s.Ships.IndexOf(lastOwn) < 0)
            lastOwn = -1;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, pad = 8 * sc, w = 270 * sc;
        float maxH = vp.Size.Y * 0.62f;
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + pad, HudTop));
        ImGui.SetNextWindowSizeConstraints(new Vector2(w, 0), new Vector2(w, maxH));
        ImGui.SetNextWindowBgAlpha(0.86f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 6) * sc);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2) * sc);
        bool open = ImGui.Begin("##rail", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                          ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove);
        ImGui.PopStyleVar(2);
        if (!open)
        {
            ImGui.End();
            return;
        }
        // the player's units: the top ones (forces and lone squadrons), each with its ships
        int own = 0;
        for (int k = 0; k < v.Count; k++)
            own += side < 0 || v.Side[k] == side ? 1 : 0;
        railOwn = own;
        RailHead("YOUR FORCES", U8.W($"{own} ships"));
        CommanderSwitch(s);
        // a big order of battle scrolls in its own region, the enemy below it in view
        bool scroll = own > RailAllOpen;
        if (scroll)
            ImGui.BeginChild("##forces", new Vector2(0, vp.Size.Y * 0.38f));
        for (int g = 0; g < gv.Count; g++)
            if (gv.Parent[g] == 0 && UnitIsOurs(s, g))
                UnitRows(s, g, 0);
        bool loose = false;
        for (int k = 0; k < v.Count; k++)
            if ((side < 0 || v.Side[k] == side) && gv.IndexOfShip(v.Entity[k]) < 0)
            {
                if (!loose)
                {
                    ImGui.TextDisabled("not in a squadron");
                    loose = true;
                }
                ShipRow(s, k, -1, -1);
            }
        AloneZone(s);
        if (scroll)
            ImGui.EndChild();

        // the enemy in the picture
        var c = s.Contacts;
        if (c.Count > 0)
        {
            ImGui.Spacing();
            int from = s.Ships.IndexOf(lastOwn);
            Vec3d at = from >= 0 ? v.PoseAt(from, world.DrawMs).Position : default;
            enemyOrder.Clear();
            for (int i = 0; i < c.Count; i++)
                if ((c.Seen[i] & SeenFlags.Gone) == 0)
                    enemyOrder.Add((from >= 0 ? Earth.Distance(at, c.Position[i]) : i, i));
            enemyOrder.Sort((a, b) => a.Range.CompareTo(b.Range));
            int sighted = 0;
            for (int i = 0; i < c.Count; i++)
                sighted += c.InSight[i] ? 1 : 0;
            RailHead("ENEMY", U8.W($"{sighted} in sight{(from >= 0 ? " · from " : "")}{(from >= 0 ? ShipName(s, lastOwn) : "")}"));
            int shown = Math.Min(enemyOrder.Count, 10);
            for (int j = 0; j < shown; j++)
                EnemyRow(s, enemyOrder[j].Index, from >= 0 ? enemyOrder[j].Range : double.NaN, at, from >= 0);
            if (enemyOrder.Count > shown)
                U8.Text(U8.W($"and {enemyOrder.Count - shown} more"), DesignerTheme.Muted);
        }
        ImGui.End();
    }

    bool UnitIsOurs(Snapshot s, int g)
    {
        var gv = s.Groups;
        int guide = gv.GuideShip[g];
        int k = s.Ships.IndexOf(guide);
        return s.PlayerSide < 0 || k >= 0 && s.Ships.Side[k] == s.PlayerSide;
    }

    /// <summary>The side's commander, under the forces' head (docs/battle/command.md decision 14): on (the default), he
    /// fights every unit the player hasn't taken; off, only those handed to him.</summary>
    void CommanderSwitch(Snapshot s)
    {
        int side = s.PlayerSide;
        if (side < 0 || s.CommanderMode[side] == (byte)CommanderMode.None)
            return;
        bool on = s.CommanderOn[side];
        ImGui.PushFont(UiFonts.SansSmall);
        if (Seg(on ? "AI commands: on" : "AI commands: off", on, first: true))
            world.Send(world.Orders.Commander(side, on ? CommanderMode.Auto : CommanderMode.On));
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(on ? "Your side's commander fights every unit you haven't taken. Any order of yours to a unit takes it (YOURS);\nclick its AI tag to take it, its YOURS tag to give it back. Click here to command everything yourself."
                : "You command every unit but those you hand to the AI (click a unit's YOURS tag). Click here to let the AI command\nevery unit you haven't taken.");
    }

    /// <summary>Whether a top unit shows whose it is: its side has a commander, or it's been handed to one.</summary>
    static bool Tagged(Snapshot s, byte commanded) =>
        commanded != 0 || s.PlayerSide >= 0 && s.CommanderMode[s.PlayerSide] != (byte)CommanderMode.None;

    static ReadOnlySpan<byte> TagText(byte commanded) => commanded == 1 ? "AI"u8 : "YOURS"u8;

    /// <summary>A whose-is-it tag's box, its right end at <paramref name="topRight"/> (in the small font).</summary>
    static (Vector2 Min, Vector2 Max) TagRect(byte commanded, Vector2 topRight)
    {
        ImGui.PushFont(UiFonts.SansSmall);
        var size = U8.Size(TagText(commanded));
        ImGui.PopFont();
        float sc = UiFonts.Scale;
        var at = topRight - new Vector2(size.X, 0);
        return (at - new Vector2(3, 1) * sc, at + size + new Vector2(3, 1) * sc);
    }

    static bool InRect(Vector2 p, Vector2 min, Vector2 max) => p.X >= min.X && p.Y >= min.Y && p.X <= max.X && p.Y <= max.Y;

    /// <summary>A top unit's whose-is-it tag (the AI's in friend blue, the player's in brass), brighter and explained under
    /// the mouse: a click hands it to the AI or takes it.</summary>
    void Tag(ImDrawListPtr dl, byte commanded, Vector2 min, Vector2 max, bool hovered)
    {
        float sc = UiFonts.Scale;
        var colour = commanded == 1 ? Friend : DesignerTheme.Brass;
        dl.AddRectFilled(min, max, ImGui.GetColorU32(colour with { W = hovered ? 0.4f : 0.18f }), 2 * sc);
        U8.Draw(dl, min + new Vector2(3, 1) * sc, ImGui.GetColorU32(colour), TagText(commanded));
        if (hovered)
            ImGui.SetTooltip(commanded == 1 ? "the side's commander fights it: click to take it" : "yours: click to leave it to the side's commander");
    }

    static void RailHead(string title, ReadOnlySpan<byte> right)
    {
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.TextDisabled(title);
        if (right.Length > 0)
        {
            ImGui.SameLine();
            U8.Right(right, DesignerTheme.Muted);
        }
        ImGui.PopFont();
    }

    /// <summary>A unit's head (its name, formation, task, whose it is) and its members: a force's units, a squadron's ships.</summary>
    void UnitRows(Snapshot s, int g, int depth)
    {
        var gv = s.Groups;
        float sc = UiFonts.Scale;
        ImGui.PushID(gv.Id[g]);
        ImGui.Indent(depth * 8 * sc);
        bool chosen = squadronCardOf >= 0 && squadronCardOf == selected && gv.IndexOfShip(selected) is int sg and >= 0 && (sg == g || gv.Parent[sg] == gv.Id[g]);
        var p = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        bool tagged = gv.Parent[g] == 0 && Tagged(s, gv.Commanded[g]);
        var (tagMin, tagMax) = tagged ? TagRect(gv.Commanded[g], p + new Vector2(w - 8 * sc, 2 * sc)) : default;
        bool onTag = tagged && InRect(ImGui.GetMousePos(), tagMin, tagMax);
        bool clicked = ImGui.InvisibleButton("##unit", new Vector2(w, ImGui.GetTextLineHeight() * 2 + 2 * sc));
        bool hovered = ImGui.IsItemHovered();
        var size = ImGui.GetItemRectSize();
        DragFrom(s, 0, gv.Id[g]);
        DropOn(s, g, -1, 0);
        bool ctrl = (SDL_GetModState() & SDL_Keymod.SDL_KMOD_CTRL) != 0;
        if (clicked && onTag)
            world.Send(world.Orders.Automate(Unit.Group(gv.Id[g]), gv.Commanded[g] != 1));   // the tag: his, or the player's
        else if (clicked && ctrl && selected >= 0 && s.Ships.IndexOf(selected) >= 0)
        {
            // beside the selection: orders go to it too (docs/battle/admirals-orders.md O9)
            if (!pickedGroups.Remove(gv.Id[g]))
                pickedGroups.Add(gv.Id[g]);
        }
        else if (clicked && gv.GuideShip[g] != 0)
        {
            // the unit: its guide selected with the orders at its level, its card open
            selected = gv.GuideShip[g];
            ClearPicks();
            Chain(s);
            int level = chain.IndexOf(g) + 1;
            (levelFor, levelPick) = (selected, level > 0 ? level : -1);
            squadronCardOf = selected;
            cardOpen = true;
            if (!railOpen.Add(gv.Id[g]) && chosen)
                railOpen.Remove(gv.Id[g]);   // its head again: shut its list
        }
        var dl = ImGui.GetWindowDrawList();
        bool also = pickedGroups.Contains(gv.Id[g]);   // ctrl+clicked beside the selection
        if (chosen || also || hovered)
            dl.AddRectFilled(p, p + size, ImGui.GetColorU32(chosen || also ? DesignerTheme.BrassDarker : new Vector4(1, 1, 1, 0.05f)));
        if (chosen || also)
            dl.AddRectFilled(p, p + new Vector2(2 * sc, size.Y), ImGui.GetColorU32(DesignerTheme.Brass));
        ImGui.PushFont(UiFonts.SansBold);
        U8.Draw(dl, p + new Vector2(6 * sc, 0), ImGui.GetColorU32(DesignerTheme.Bright), U8.W($"{gv.Name[g]}"));
        ImGui.PopFont();
        string form = GroupFormationNames[(int)gv.Formation[g]], task = gv.Task[g] != 0 ? TaskText(gv.Task[g], gv.TaskStatus[g]) : "";
        var sub = gv.Force[g] ? U8.W($"{form}{(task.Length > 0 ? " · " : "")}{task}")
            : U8.W($"{gv.MemberCount[g]} ships · {form}{(task.Length > 0 ? " · " : "")}{task}");
        ImGui.PushFont(UiFonts.SansSmall);
        U8.Draw(dl, p + new Vector2(6 * sc, ImGui.GetTextLineHeight()), ImGui.GetColorU32(DesignerTheme.Muted), sub);
        if (tagged)
            Tag(dl, gv.Commanded[g], tagMin, tagMax, onTag && hovered);
        ImGui.PopFont();
        // its members: a force's units always, a squadron's ships when few, or open, or holding the selection
        bool ships = !gv.Force[g] && (railOwn <= RailAllOpen || railOpen.Contains(gv.Id[g]) || gv.IndexOfShip(selected) == g);
        for (int i = 0; i < gv.MemberCount[g] && (gv.Force[g] || ships); i++)
        {
            int m = gv.Members[gv.MemberStart[g] + i];
            if (gv.Force[g])
            {
                if (gv.IndexOfGroup(m) is int sub2 and >= 0)
                    UnitRows(s, sub2, depth + 1);
            }
            else if (s.Ships.IndexOf(m) is int k and >= 0)
                ShipRow(s, k, g, i);
        }
        ImGui.Unindent(depth * 8 * sc);
        ImGui.PopID();
    }

    /// <summary>A ship's row: her name (and flag), what she fires at or why she's falling out; her flotation, speed and hits.
    /// In a squadron (its index <paramref name="g"/>) she's its <paramref name="at"/>-th member; -1 for a ship alone.</summary>
    void ShipRow(Snapshot s, int k, int g, int at)
    {
        var v = s.Ships;
        int e = v.Entity[k];
        float sc = UiFonts.Scale, h = ImGui.GetTextLineHeight() * 2 + 2 * sc;
        ImGui.PushID(e);
        var p = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        bool tagged = s.Groups.IndexOfShip(e) < 0 && Tagged(s, v.Commanded[k]);
        var (tagMin, tagMax) = tagged ? TagRect(v.Commanded[k], p + new Vector2(w - 50 * sc, 0)) : default;   // left of her flotation
        bool onTag = tagged && InRect(ImGui.GetMousePos(), tagMin, tagMax);
        bool clicked = ImGui.InvisibleButton("##ship", new Vector2(w, h));
        bool hovered = ImGui.IsItemHovered();
        DragFrom(s, e, 0);
        DropOn(s, g, at, e);
        if (clicked && onTag)
            world.Send(world.Orders.Automate(Unit.Ship(e), v.Commanded[k] != 1));
        else if (clicked && (SDL_GetModState() & SDL_Keymod.SDL_KMOD_CTRL) != 0 && selected >= 0 && e != selected)
        {
            if (!picked.Remove(e))
                picked.Add(e);
        }
        else if (clicked)
        {
            long now = Environment.TickCount64;
            if (railClickShip == e && now - railClickMs < 400)
                CentreOn(v.PoseAt(k, world.DrawMs).Position);   // twice: the map goes to her
            (railClickShip, railClickMs) = (e, now);
            selected = e;
            ClearPicks();
            squadronCardOf = -1;
            cardOpen = true;
        }
        bool on = selected == e && squadronCardOf != e || picked.Contains(e);
        var dl = ImGui.GetWindowDrawList();
        if (on || hovered)
            dl.AddRectFilled(p, p + new Vector2(w, h), ImGui.GetColorU32(on ? DesignerTheme.BrassDarker : new Vector4(1, 1, 1, 0.05f)));
        if (on)
            dl.AddRectFilled(p, p + new Vector2(2 * sc, h), ImGui.GetColorU32(DesignerTheme.Brass));
        int ci = s.Conditions.IndexOf(e);
        bool going = ci >= 0 && s.Conditions.Status[ci] == ShipStatus.GoingDown;
        var name = ShipName(s, e);
        U8.Draw(dl, p + new Vector2(14 * sc, 0), ImGui.GetColorU32(going ? Bad : DesignerTheme.Bright), U8.W($"{name}"));
        if (ci >= 0 && s.Conditions.Fires[ci] > 0)
        {
            var nameW = ImGui.CalcTextSize(name).X;
            float pulse = 0.7f + 0.3f * MathF.Sin((float)ImGui.GetTime() * 5 + e);
            dl.AddCircleFilled(p + new Vector2(14 * sc + nameW + 7 * sc, ImGui.GetTextLineHeight() / 2), 3 * sc, ImGui.GetColorU32(Fire with { W = pulse }));
        }
        // the dot of her side: a friend's circle
        dl.AddCircle(p + new Vector2(6 * sc, ImGui.GetTextLineHeight() / 2), 3.5f * sc, ImGui.GetColorU32(Friend), 0, 1.4f * sc);
        // under her name: going down, falling out, her target
        ImGui.PushFont(UiFonts.SansSmall);
        int target = v.FireTarget[k];
        if (target == 0)
            for (int i = 0; i < s.Batteries.Count; i++)
                if (s.Batteries.Shooter[i] == e && s.Batteries.Battery[i] == 0)
                    target = s.Batteries.Target[i];
        var sub = going ? "going down"u8 : v.Reason[k] != 0 ? U8.W($"{ReasonText(v.Reason[k])}") : target > 0 ? U8.W($"on {LabelOf(s, target)}") : "no target"u8;
        U8.Draw(dl, p + new Vector2(14 * sc, ImGui.GetTextLineHeight()), ImGui.GetColorU32(going ? Bad : v.Reason[k] != 0 ? DesignerTheme.Amber : DesignerTheme.Muted), sub);
        // at the right: her flotation, speed and hits
        double water = ci >= 0 ? s.Conditions.WaterT[ci] : 0, reserve = ci >= 0 ? s.Conditions.ReserveT[ci] : 1;
        float afloat = (float)(reserve / Math.Max(reserve + water, 1e-6));
        float bw = 40 * sc, bx = p.X + w - bw - 2 * sc, by = p.Y + ImGui.GetTextLineHeight() / 2 - 2 * sc;
        dl.AddRectFilled(new Vector2(bx, by), new Vector2(bx + bw, by + 4 * sc), ImGui.GetColorU32(DesignerTheme.Inset), 1);
        dl.AddRectFilled(new Vector2(bx, by), new Vector2(bx + bw * afloat, by + 4 * sc), ImGui.GetColorU32(afloat > 0.8f ? Ok : afloat > 0.6f ? DesignerTheme.Amber : Bad), 1);
        if (tagged)
            Tag(dl, v.Commanded[k], tagMin, tagMax, onTag && hovered);
        var right = U8.W($"{v.Speed[k] / Handling.MsPerKnot:0} kn · {(ci >= 0 ? s.Conditions.Hits[ci] : 0)} hits");
        var rs = U8.Size(right);
        U8.Draw(dl, new Vector2(p.X + w - rs.X - 2 * sc, p.Y + ImGui.GetTextLineHeight()), ImGui.GetColorU32(DesignerTheme.Muted), right);
        ImGui.PopFont();
        ImGui.PopID();
    }

    // ------------------------------------------------------------------ organising by drag and drop

    /// <summary>The last row a drag can start from: a ship (an entity) or a group (an id), named under the cursor.</summary>
    void DragFrom(Snapshot s, int ship, int group)
    {
        if (!ImGui.BeginDragDropSource())
            return;
        (dragShip, dragGroup) = (ship, group);
        ImGui.SetDragDropPayload(RailPayload, IntPtr.Zero, 0);
        U8.Text(ship != 0 ? U8.W($"{ShipName(s, ship)}") : s.Groups.IndexOfGroup(group) is int gi and >= 0 ? U8.W($"{s.Groups.Name[gi]}") : "?"u8);
        ImGui.EndDragDropSource();
    }

    /// <summary>The last row a drag can end on: a unit's head (<paramref name="g"/>, <paramref name="at"/> -1), a ship of a
    /// squadron (its index and her place in it) or a ship alone (<paramref name="g"/> -1). What a drop does is told before
    /// it's let go; one that would do nothing isn't taken.</summary>
    void DropOn(Snapshot s, int g, int at, int ship)
    {
        if (!ImGui.BeginDragDropTarget())
            return;
        // what it does, made once a target (the drag hovers one at a time): no garbage while it's held there
        var key = (dragShip, dragGroup, g, at, ship, world.Generation);
        if (key != dropKey)
        {
            dropKey = key;
            dropOk = DropAction(s, g, at, ship, out dropWhat, out dropFirst, out dropSecond);
        }
        if (dropOk)
            Accept(dropWhat, dropFirst, dropSecond);
        ImGui.EndDragDropTarget();
    }

    (int, int, int, int, int, int) dropKey = (-1, -1, -1, -1, -1, -1);
    bool dropOk;
    string dropWhat = "";
    Command dropFirst;
    Command? dropSecond;
    const int AloneTarget = -2;   // DropOn's group for the "on its own" place

    /// <summary>The drag taken (the target outlined in brass, what it does under the cursor), and on its release the
    /// commands sent.</summary>
    void Accept(string what, in Command first, Command? second)
    {
        ImGui.PushStyleColor(ImGuiCol.DragDropTarget, DesignerTheme.Brass);
        var payload = ImGui.AcceptDragDropPayload(RailPayload, ImGuiDragDropFlags.AcceptBeforeDelivery);
        ImGui.PopStyleColor();
        unsafe
        {
            if (payload.NativePtr == null)
                return;
        }
        ImGui.SetTooltip(what);
        if (!payload.IsDelivery())
            return;
        world.Send(first);
        if (second is { } then)
            world.Send(then);
    }

    /// <summary>What dropping the drag on a row (or on "on its own", <see cref="AloneTarget"/>) does, as the group commands
    /// to send (one or two), and in words.</summary>
    bool DropAction(Snapshot s, int g, int at, int ship, out string what, out Command first, out Command? second)
    {
        var gv = s.Groups;
        (what, first, second) = ("", default, null);
        if (g == AloneTarget)
        {
            if (dragShip != 0 && gv.IndexOfShip(dragShip) >= 0)
                (first, what) = (GroupSystem.Remove(dragShip), $"{ShipName(s, dragShip)} out of her squadron");
            else if (dragGroup != 0 && gv.IndexOfGroup(dragGroup) is int og and >= 0 && gv.Parent[og] != 0)
                (first, what) = (GroupSystem.Exit(dragGroup), $"{gv.Name[og]} out of its force, on its own");
            else
                return false;
            return true;
        }
        if (dragShip != 0)
        {
            string name = ShipName(s, dragShip);
            if (g >= 0)
            {
                if (gv.Force[g] || ship == dragShip || at < 0 && gv.IndexOfShip(dragShip) == g)
                    return false;   // a force holds squadrons; onto herself, or her own squadron's head, does nothing
                (first, what) = at < 0
                    ? (GroupSystem.Add(gv.Id[g], dragShip), $"{name} to {gv.Name[g]}, at its end")
                    : (GroupSystem.Add(gv.Id[g], dragShip, at), $"{name} to {gv.Name[g]}, ahead of {ShipName(s, ship)}");
                return true;
            }
            if (ship == 0 || ship == dragShip)
                return false;
            string lead = ShipName(s, ship);
            (first, second, what) = (GroupSystem.Create(ship), GroupSystem.Join(dragShip, ship), $"a new squadron of {lead} and {name}, {lead} leading");
            return true;
        }
        if (dragGroup == 0 || g < 0 || gv.IndexOfGroup(dragGroup) is not (int dg and >= 0))
            return false;
        if (gv.Id[g] == dragGroup || gv.Parent[dg] == gv.Id[g] || Inside(gv, g, dragGroup))
            return false;   // onto itself, its own force, or something inside it
        (first, what) = (GroupSystem.Subordinate(dragGroup, gv.Id[g]), gv.Force[g]
            ? $"{gv.Name[dg]} under {gv.Name[g]}'s admiral"
            : $"{gv.Name[dg]} under {gv.Name[g]}'s admiral: a force of the two, {gv.Name[g]} guiding");
        return true;
    }

    /// <summary>Whether a group (an index) is a group (an id) or inside it.</summary>
    static bool Inside(GroupsView gv, int g, int groupId)
    {
        for (int at = g; at >= 0; at = gv.Parent[at] != 0 ? gv.IndexOfGroup(gv.Parent[at]) : -1)
            if (gv.Id[at] == groupId)
                return true;
        return false;
    }

    /// <summary>While a drag is on: a place to drop a ship out of her squadron, or a group out of its force.</summary>
    void AloneZone(Snapshot s)
    {
        unsafe
        {
            if (ImGui.GetDragDropPayload().NativePtr == null)
                return;
        }
        float sc = UiFonts.Scale;
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight() * 1.6f);
        ImGui.InvisibleButton("##alone", size);
        var dl = ImGui.GetWindowDrawList();
        dl.AddRect(p, p + size, ImGui.GetColorU32(DesignerTheme.Muted), 2 * sc);
        ImGui.PushFont(UiFonts.SansSmall);
        U8.Draw(dl, p + new Vector2(6, 3) * sc, ImGui.GetColorU32(DesignerTheme.Muted), "on its own"u8);
        ImGui.PopFont();
        DropOn(s, AloneTarget, -1, 0);
    }


    /// <summary>An enemy's row: her identity as held (doubtful dashed), what's seen of her; range and bearing from the
    /// player's ship.</summary>
    void EnemyRow(Snapshot s, int i, double range, Vec3d from, bool haveFrom)
    {
        var c = s.Contacts;
        int e = c.Entity[i];
        float sc = UiFonts.Scale, h = ImGui.GetTextLineHeight() * 2 + 2 * sc;
        ImGui.PushID(e);
        var p = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        if (ImGui.InvisibleButton("##enemy", new Vector2(w, h)))
        {
            long now = Environment.TickCount64;
            if (railClickShip == e && now - railClickMs < 400)
                CentreOn(c.Position[i]);
            (railClickShip, railClickMs) = (e, now);
            selected = e;
            ClearPicks();
            squadronCardOf = -1;
            cardOpen = true;
        }
        bool on = selected == e;
        var dl = ImGui.GetWindowDrawList();
        if (on || ImGui.IsItemHovered())
            dl.AddRectFilled(p, p + new Vector2(w, h), ImGui.GetColorU32(on ? DesignerTheme.BrassDarker : new Vector4(1, 1, 1, 0.05f)));
        bool sure = c.Level[i] >= ContactLevel.Class;
        float alpha = c.InSight[i] ? 1 : 0.55f;
        // the dot: a hostile's diamond, dashed while she isn't made out
        var d = p + new Vector2(6 * sc, ImGui.GetTextLineHeight() / 2);
        float r = 3.6f * sc;
        uint col = ImGui.GetColorU32(Bad with { W = alpha });
        if (sure)
            dl.AddQuad(d + new Vector2(0, -r), d + new Vector2(r, 0), d + new Vector2(0, r), d + new Vector2(-r, 0), col, 1.4f * sc);
        else
            for (int q = 0; q < 4; q++)
            {
                var a = q switch { 0 => new Vector2(0, -r), 1 => new Vector2(r, 0), 2 => new Vector2(0, r), _ => new Vector2(-r, 0) };
                var b = q switch { 0 => new Vector2(r, 0), 1 => new Vector2(0, r), 2 => new Vector2(-r, 0), _ => new Vector2(0, -r) };
                dl.AddLine(d + a, d + (a + b) / 2, col, 1.4f * sc);
            }
        U8.Draw(dl, p + new Vector2(14 * sc, 0), ImGui.GetColorU32(new Vector4(0.95f, 0.77f, 0.74f, alpha)), U8.W($"{Identity(c, i)}"));
        ImGui.PushFont(UiFonts.SansSmall);
        var seen = c.Seen[i] != SeenFlags.None ? U8.W($"{SeenWords(c.Seen[i])}") : c.InSight[i] ? "no damage seen"u8
            : U8.W($"last seen {new WorldTime(c.SeenMs[i]).ToUtc().Hour:00}:{new WorldTime(c.SeenMs[i]).ToUtc().Minute:00}");
        U8.Draw(dl, p + new Vector2(14 * sc, ImGui.GetTextLineHeight()), ImGui.GetColorU32(c.Seen[i] != SeenFlags.None ? new Vector4(0.94f, 0.7f, 0.66f, alpha) : DesignerTheme.Muted), seen);
        if (haveFrom)
        {
            double brg = (Earth.Bearing(from, c.Position[i]) % 360 + 360) % 360;
            var a = U8.W($"{range / 1000:0.0} km");
            var b = U8.W($"{brg:000}°");
            ImGui.PushFont(UiFonts.Mono);
            var sa = U8.Size(a);
            var sb = U8.Size(b);
            U8.Draw(dl, new Vector2(p.X + w - sa.X - 2 * sc, p.Y), ImGui.GetColorU32(DesignerTheme.Muted), a);
            U8.Draw(dl, new Vector2(p.X + w - sb.X - 2 * sc, p.Y + ImGui.GetTextLineHeight()), ImGui.GetColorU32(DesignerTheme.Muted), b);
            ImGui.PopFont();
        }
        ImGui.PopFont();
        ImGui.PopID();
    }

    /// <summary>What's seen of a ship, in words: "burning · low · by the bow".</summary>
    static string SeenWords(SeenFlags f) => seenWords.TryGetValue(f, out var s) ? s : seenWords[f] = string.Join(" · ", SeenParts(f));

    static readonly Dictionary<SeenFlags, string> seenWords = [];

    static IEnumerable<string> SeenParts(SeenFlags f)
    {
        if ((f & SeenFlags.GoingDown) != 0)
            yield return "going down";
        if ((f & SeenFlags.Burning) != 0)
            yield return "burning";
        if ((f & SeenFlags.Low) != 0)
            yield return "low in the water";
        if ((f & SeenFlags.Listing) != 0)
            yield return "listing";
        if ((f & SeenFlags.ByBow) != 0)
            yield return "by the bow";
        if ((f & SeenFlags.ByStern) != 0)
            yield return "by the stern";
        if ((f & SeenFlags.Stopped) != 0)
            yield return "stopped";
        if ((f & SeenFlags.Silent) != 0)
            yield return "guns silent";
    }

    /// <summary>The map's centre moved to a point, its zoom kept.</summary>
    void CentreOn(Vec3d at)
    {
        var (lat, lon) = Earth.LatLon(at);
        camera = MapCamera.At(lat, lon, camera.MetresPerPixel);
        clamped = false;
    }
}
