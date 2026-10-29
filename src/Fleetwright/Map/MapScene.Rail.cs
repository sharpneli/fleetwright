using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using ImGuiNET;
using SDL;
using static SDL.SDL3;
using Fleetwright.Ui;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The forces rail (docs/map-ui/battle-hud.md U4, redrawn in docs/map-ui/rail.md): the left column of docked
/// panes, the card dock's twin. FORCES, the player's units and ships as a tree table (a unit's row folds on its twisty,
/// selects it for orders and opens its card; a ship's row selects her, twice centres the map on her), each ship with what
/// she's doing, her flotation, speed and hits, a unit with a segment a ship; ENEMY, the player's picture grouped by type,
/// nearest first in each from the ship the player last chose, with what's seen and how many of the player's ships fire at
/// each; LOG, the newest events (<c>MapScene.Events.cs</c>). Its text is written without garbage (<see cref="U8"/>) and its
/// lookups go through the frame's indices, not scans.
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
    /// <summary>The units folded open or shut by the player, or opened to show the selection (by group id); the rest
    /// open by <see cref="UnitOpen"/>'s rule. The enemy's groups the same, by kind.</summary>
    readonly Dictionary<int, bool> railFold = [], enemyFold = [];
    /// <summary>The player's ships this frame; with more than <see cref="RailAllOpen"/>, squadrons start shut.</summary>
    int railOwn;
    const int RailAllOpen = 12;
    /// <summary>The selection the rail last showed (a new one from the map opens its units and scrolls to her).</summary>
    int railRevealed = -1, railScrollTo = -1;
    /// <summary>What the rail's drag carries: a ship (an entity) or a group (an id); 0 for neither.</summary>
    int dragShip, dragGroup;
    const string RailPayload = "FW_RAIL";

    // the frame's indices: ships, conditions and groups by entity or id, each ship's squadron, the main guns' targets, and
    // how many of the player's ships fire at each target
    readonly Dictionary<int, int> railShip = [], railCond = [], railGroup = [], railSquadron = [], railMain = [], railOn = [];

    enum RailKind : byte { Unit, Ship, Loose, Drop }

    /// <summary>A forces row: a unit (its index), a ship (her index; her squadron's index and her place in it, -1 alone),
    /// the "on its own" head, the drop place while a drag is on.</summary>
    readonly record struct RailRow(RailKind Kind, int Index, int Depth, int Group, int At);

    readonly List<RailRow> railRows = [];
    /// <summary>The enemy's rows: a group's (its kind, <see cref="EnemyGroupRow"/> as the index) or a contact's, nearest
    /// first in each kind.</summary>
    readonly List<(int Kind, double Range, int Index)> enemyOrder = [];
    const int EnemyGroupRow = -1;

    static readonly string[] OnOff = ["On", "Off"];
    // the unknowns (made out, not known for friend or foe: docs/battle/night.md Q9) first
    static readonly string[] EnemyKinds = ["Unknown", "Heavy ships", "Cruisers", "Destroyers", "Small craft", "Merchants", "Not made out"];
    /// <summary>A ship's state in a unit's bar: sound, low, going down.</summary>
    static readonly Vector4[] ShipStates = [T.Sound, T.Amber, T.Hostile];
    const int RailMaxShips = 512;

    void Rail(Snapshot s)
    {
        var v = s.Ships;
        int side = s.PlayerSide;
        if (v.IndexOf(selected) is int ks and >= 0 && (side < 0 || v.Side[ks] == side))
            lastOwn = selected;
        if (v.IndexOf(lastOwn) < 0)
            lastOwn = -1;
        RailIndex(s);
        Reveal(s);
        RailRows(s);
        EnemyRows(s);

        // the column: the forces from the top, the enemy under them, the log at the foot, above the strip
        var vp = ImGui.GetMainViewport();
        float w = P(330), top = HudTop, bottom = StripTopY(vp) - P(1), gap = P(2), x = vp.Pos.X + P(8);
        bool commander = side >= 0 && s.CommanderMode[side] != (byte)CommanderMode.None;
        float logH = TitleH + 7 * RowH + 2;
        float forcesHead = (commander ? RowH : 0) + HeadH;
        float forcesWant = TitleH + forcesHead + Math.Max(railRows.Count, 1) * RowH + 2;
        float enemyWant = TitleH + HeadH + Math.Max(enemyOrder.Count, 1) * RowH + 2;
        float room = bottom - top - logH - 2 * gap;
        // the enemy gets up to 30 % past a long order of battle, all it needs beside a short one
        float forcesH = Math.Min(forcesWant, room - Math.Min(enemyWant, room * 0.3f));
        float enemyH = Math.Min(enemyWant, room - forcesH);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, T.Panel);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var at = new Vector2(x, top);
        if (BeginRailPane("##forces", at, new Vector2(w, forcesH), out var dl))
            ForcesPane(s, dl, at, new Vector2(w, forcesH), commander, forcesWant > forcesH);
        ImGui.End();
        at.Y += forcesH + gap;
        if (BeginRailPane("##enemy", at, new Vector2(w, enemyH), out dl))
            EnemyPane(s, dl, at, new Vector2(w, enemyH), enemyWant > enemyH);
        ImGui.End();
        at.Y = bottom - logH;
        if (BeginRailPane("##logpane", at, new Vector2(w, logH), out dl))
            LogPane(s, dl, at, new Vector2(w, logH));
        ImGui.End();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    static bool BeginRailPane(string id, Vector2 a, Vector2 size, out ImDrawListPtr dl)
    {
        ImGui.SetNextWindowPos(a);
        ImGui.SetNextWindowSize(size);
        bool open = ImGui.Begin(id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoBringToFrontOnFocus);
        dl = ImGui.GetWindowDrawList();
        return open;
    }

    // by its side, not its guide: a force whose every member is detached has no guide ship
    static bool UnitIsOurs(Snapshot s, int g) => s.PlayerSide < 0 || s.Groups.Side[g] == s.PlayerSide;

    bool OursAt(Snapshot s, int k) => s.PlayerSide < 0 || s.Ships.Side[k] == s.PlayerSide;

    /// <summary>The frame's indices, so no row scans the snapshot.</summary>
    void RailIndex(Snapshot s)
    {
        var v = s.Ships;
        var gv = s.Groups;
        var c = s.Conditions;
        railShip.Clear();
        railCond.Clear();
        railGroup.Clear();
        railSquadron.Clear();
        railMain.Clear();
        railOn.Clear();
        for (int k = 0; k < v.Count; k++)
            railShip[v.Entity[k]] = k;
        for (int i = 0; i < c.Count; i++)
            railCond[c.Entity[i]] = i;
        for (int g = 0; g < gv.Count; g++)
        {
            railGroup[gv.Id[g]] = g;
            if (!gv.Force[g])
                for (int i = 0; i < gv.MemberCount[g]; i++)
                    railSquadron[gv.Members[gv.MemberStart[g] + i]] = g;
        }
        var b = s.Batteries;
        for (int i = 0; i < b.Count; i++)
            if (b.Battery[i] == 0)
                railMain[b.Shooter[i]] = b.Target[i];
        railOwn = 0;
        for (int k = 0; k < v.Count; k++)
            if (OursAt(s, k))
            {
                railOwn++;
                if (TargetOf(s, k) is int t and > 0)
                    railOn[t] = railOn.GetValueOrDefault(t) + 1;
            }
    }

    /// <summary>What a ship fires at: her order's target, else her main guns'.</summary>
    int TargetOf(Snapshot s, int k) => s.Ships.FireTarget[k] is int t and > 0 ? t : railMain.GetValueOrDefault(s.Ships.Entity[k]);

    /// <summary>A new selection of the player's from outside the rail (the map, Tab, the log) opens the units she's in and
    /// scrolls to her; the rail's own clicks note theirs at once, so a unit's row doesn't open the unit.</summary>
    void Reveal(Snapshot s)
    {
        if (selected == railRevealed)
            return;
        bool ship = railShip.TryGetValue(selected, out int k);
        if (!ship && selected >= 0 && ContactIndex(s.Contacts, selected) < 0)
            return;   // not in the snapshot yet (a selection at start): wait for her
        railRevealed = selected;
        if (!ship || !OursAt(s, k))
            return;
        var gv = s.Groups;
        for (int g = railSquadron.GetValueOrDefault(selected, -1); g >= 0; g = gv.Parent[g] != 0 ? railGroup.GetValueOrDefault(gv.Parent[g], -1) : -1)
            railFold[gv.Id[g]] = true;
        railScrollTo = selected;
    }

    /// <summary>Whether a unit's members show: as the player or the selection left it, else open while the order of battle
    /// is small, and a force's always (its squadrons each a row).</summary>
    bool UnitOpen(Snapshot s, int g) => railFold.TryGetValue(s.Groups.Id[g], out bool open) ? open : railOwn <= RailAllOpen || s.Groups.Force[g];

    void RailRows(Snapshot s)
    {
        var v = s.Ships;
        var gv = s.Groups;
        railRows.Clear();
        for (int g = 0; g < gv.Count; g++)
            if (gv.Parent[g] == 0 && UnitIsOurs(s, g))
                UnitRows(s, g, 0);
        bool loose = false;
        for (int k = 0; k < v.Count; k++)
            if (OursAt(s, k) && !railSquadron.ContainsKey(v.Entity[k]))
            {
                if (!loose)
                    railRows.Add(new RailRow(RailKind.Loose, -1, 0, -1, -1));
                loose = true;
                railRows.Add(new RailRow(RailKind.Ship, k, 1, -1, -1));
            }
        unsafe
        {
            if (ImGui.GetDragDropPayload().NativePtr != null)
                railRows.Add(new RailRow(RailKind.Drop, -1, 0, -1, -1));
        }
    }

    void UnitRows(Snapshot s, int g, int depth)
    {
        var gv = s.Groups;
        railRows.Add(new RailRow(RailKind.Unit, g, depth, -1, -1));
        if (!UnitOpen(s, g))
            return;
        for (int i = 0; i < gv.MemberCount[g]; i++)
        {
            int m = gv.Members[gv.MemberStart[g] + i];
            if (gv.Force[g])
            {
                if (railGroup.TryGetValue(m, out int sub))
                    UnitRows(s, sub, depth + 1);
            }
            else if (railShip.TryGetValue(m, out int k))
                railRows.Add(new RailRow(RailKind.Ship, k, depth + 1, g, i));
        }
    }

    // ------------------------------------------------------------------ the forces

    /// <summary>The forces' columns from a row's right edge: doing, the bar, speed, hits, whose.</summary>
    static (float Doing, float BarL, float BarR, float Kn, float Hits, float Whose) ForcesColumns(float right)
    {
        float whose = right - P(34), hits = whose - P(2), kn = hits - P(26), barR = kn - P(26), barL = barR - P(36);
        return (barL - P(80), barL, barR, kn, hits, whose);
    }

    void ForcesPane(Snapshot s, ImDrawListPtr dl, Vector2 a, Vector2 size, bool commander, bool scroll)
    {
        float right = dl.Pane(a, size, "FORCES", "your side");
        float kw = dl.Kbd(new Vector2(right - Width(UiFonts.MonoSmall, "Tab") - P(6), a.Y + P(3.5f)), "Tab");
        dl.TextRight(UiFonts.SansSmall, right - kw - P(6), Mid(UiFonts.SansSmall, a.Y, TitleH), T.Muted, U8.W($"{railOwn} ships"));
        float w = size.X - 2, y = a.Y + TitleH + 1;
        if (commander)
        {
            CommanderRow(s, dl, new Vector2(a.X + 1, y), w);
            y += RowH;
        }
        var head = new Vector2(a.X + 1, y);
        dl.Head(head, w);
        var cols = ForcesColumns(head.X + w - (scroll ? ImGui.GetStyle().ScrollbarSize : 0));
        dl.HeadCell(head, head.X + P(6), "UNIT · SHIP");
        dl.HeadCell(head, cols.Doing, "DOING");
        dl.HeadCell(head, cols.BarR, "AFLOAT", right: true);
        dl.HeadCell(head, cols.Kn, "KN", right: true);
        dl.HeadCell(head, cols.Hits, "HITS", right: true);
        float rows = railRows.Count * RowH;
        if (BeginBody("##forcesbody", a, size, out var body, head: y + HeadH - (a.Y + TitleH + 1)))
        {
            var o = ImGui.GetCursorScreenPos();
            float rw = ImGui.GetContentRegionAvail().X;
            cols = ForcesColumns(o.X + rw);
            int cardUnit = squadronCardOf >= 0 && squadronCardOf == selected && railSquadron.ContainsKey(selected) ? UnitOfCard(s, selected) : -1;
            for (int i = 0; i < railRows.Count; i++)
            {
                var r = railRows[i];
                var p = new Vector2(o.X, o.Y + i * RowH);
                switch (r.Kind)
                {
                    case RailKind.Unit:
                        UnitRow(s, body, p, rw, r, cols, cardUnit);
                        break;
                    case RailKind.Ship:
                        ShipRow(s, body, p, rw, r, cols, i);
                        break;
                    case RailKind.Loose:
                        body.GroupBg(p, rw, RowH);
                        body.Text(UiFonts.SansSmall, new Vector2(p.X + P(6), Mid(UiFonts.SansSmall, p.Y, RowH)), T.Muted, "On its own");
                        break;
                    default:
                        DropRow(s, body, p, rw);
                        break;
                }
            }
            railScrollTo = -1;
            if (railRows.Count == 0)
                body.Text(UiFonts.SansSmall, new Vector2(o.X + P(6), Mid(UiFonts.SansSmall, o.Y, RowH)), T.Muted, "no ships");
        }
        EndBody(rows);
    }

    /// <summary>The side's commander, a sub-header under the forces' title (docs/battle/command.md decision 14): on (the
    /// default), he fights every unit the player hasn't taken; off, only those handed to him.</summary>
    void CommanderRow(Snapshot s, ImDrawListPtr dl, Vector2 a, float w)
    {
        int side = s.PlayerSide;
        bool on = s.CommanderOn[side];
        float right = dl.SubHeader(a, w, "COMMANDER", "fights the units you haven't taken");
        if (dl.Segmented("##commander", new Vector2(right - SegmentedWidth(OnOff), a.Y + P(2)), OnOff, on ? 0 : 1, out _) >= 0)
            world.Send(world.Orders.Commander(side, on ? CommanderMode.Auto : CommanderMode.On));
        if (ImGui.IsMouseHoveringRect(a, a + new Vector2(w, RowH)) && ImGui.IsWindowHovered())
            Tip(on ? "Your side's commander fights every unit you haven't taken. Any order of yours to a unit takes it (you); click its AI chip to take it, its you chip to give it back. Off: you command everything yourself."
                : "You command every unit but those you hand to the AI (click a unit's you chip). On: the AI commands every unit you haven't taken.", 360);
    }

    /// <summary>Whether a top unit shows whose it is: its side has a commander, or it's been handed to one.</summary>
    static bool Tagged(Snapshot s, byte commanded) =>
        commanded != 0 || s.PlayerSide >= 0 && s.CommanderMode[s.PlayerSide] != (byte)CommanderMode.None;

    /// <summary>Whose it is, at a row's right end: the conn (green), else on a top unit or a ship alone the AI's (quiet)
    /// or the player's (plain); a click on the AI's or the player's hands it over or takes it. True when clicked.</summary>
    static bool WhoseChip(ImDrawListPtr dl, Vector2 row, float right, bool conn, bool tagged, byte commanded)
    {
        if (!conn && !tagged)
            return false;
        var label = conn ? "conn"u8 : commanded == 1 ? "AI"u8 : "you"u8;
        var at = new Vector2(right - ChipWidth(label), row.Y + P(2.5f));
        dl.Chip(at, label, conn ? ChipKind.Ok : commanded == 1 ? ChipKind.Quiet : ChipKind.Plain);
        if (conn)
            return false;
        ImGui.SetCursorScreenPos(at);
        bool hit = ImGui.InvisibleButton("##whose", new Vector2(ChipWidth(label), P(15)));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(commanded == 1 ? "the side's commander fights it: click to take it" : "yours: click to leave it to the side's commander");
        return hit;
    }

    /// <summary>A unit's row: its twisty, name and task (its ships burning while it's shut), a segment a ship, its guide's
    /// speed, its hits, whose it is.</summary>
    void UnitRow(Snapshot s, ImDrawListPtr dl, Vector2 a, float w, in RailRow r, (float Doing, float BarL, float BarR, float Kn, float Hits, float Whose) cols, int cardUnit)
    {
        var gv = s.Groups;
        int g = r.Index, id = gv.Id[g];
        ImGui.PushID(id * 2 + 1);
        var (clicked, hover) = HitRow("##unit", a, new Vector2(w, RowH));
        DragFrom(s, 0, id);
        DropOn(s, g, -1, 0);
        bool chosen = g == cardUnit, also = pickedGroups.Contains(id);
        if (chosen || also)
            dl.RowBg(a, w, RowH, 0, selected: true);
        else
            dl.GroupBg(a, w, RowH, hover);
        bool open = UnitOpen(s, g);
        float x = a.X + P(6) + r.Depth * P(10);
        ImGui.SetCursorScreenPos(new Vector2(x - P(4), a.Y));
        if (ImGui.InvisibleButton("##twisty", new Vector2(P(16), RowH)))
            railFold[id] = !open;
        dl.Twisty(new Vector2(x + P(3), a.Y + RowH / 2), open, ImGui.IsItemHovered() ? T.Bright : T.Muted);
        x += P(12);

        Span<byte> states = stackalloc byte[RailMaxShips];
        int fires = 0, hits = 0;
        int n = UnitShips(s, g, states, 0, ref fires, ref hits);
        if (gv.Force[g])
            states[..n].Sort();   // sound, low, going down: one ship in trouble among fifty still shows
        float end = dl.TextClipped(UiFonts.SansBold, new Vector2(x, Mid(UiFonts.SansBold, a.Y, RowH)), cols.BarL - P(8), chosen ? T.Brass : T.Bright, U8.W($"{gv.Name[g]}"));
        if (fires > 0 && !open && end + P(20) < cols.BarL)
        {
            Flame(dl, new Vector2(end + P(8), a.Y + RowH / 2 + P(1)), id);
            end = dl.TextClipped(UiFonts.MonoSmall, new Vector2(end + P(13), Mid(UiFonts.MonoSmall, a.Y, RowH)), cols.BarL - P(8), T.Fire, U8.W($"{fires}"));
        }
        if (gv.Task[g] != 0)
            dl.TextClipped(UiFonts.SansSmall, new Vector2(end + P(4), Mid(UiFonts.SansSmall, a.Y, RowH)), cols.BarL - P(8), T.Muted,
                U8.W($"· {TaskText(gv.Task[g], gv.TaskStatus[g])}"));
        dl.SegmentBar(new Vector2(cols.BarL, a.Y + P(6.5f)), new Vector2(cols.BarR - cols.BarL, P(7)), states[..n], ShipStates);
        if (railShip.TryGetValue(gv.GuideShip[g], out int gk))
            dl.TextRight(UiFonts.Mono, cols.Kn, Mid(UiFonts.Mono, a.Y, RowH), T.Bright, U8.W($"{s.Ships.Speed[gk] / Handling.MsPerKnot:0}"));
        dl.TextRight(UiFonts.Mono, cols.Hits, Mid(UiFonts.Mono, a.Y, RowH), T.Bright, U8.W($"{hits}"));
        if (WhoseChip(dl, a, a.X + w - P(4), gv.Conn[g], gv.Parent[g] == 0 && Tagged(s, gv.Commanded[g]), gv.Commanded[g]))
            world.Send(world.Orders.Automate(Unit.Group(id), gv.Commanded[g] != 1));
        else if (clicked && (SDL_GetModState() & SDL_Keymod.SDL_KMOD_CTRL) != 0 && selected >= 0 && s.Ships.IndexOf(selected) >= 0)
        {
            // beside the selection: orders go to it too (docs/battle/admirals-orders.md O9)
            if (!pickedGroups.Remove(id))
                pickedGroups.Add(id);
        }
        else if (clicked && gv.LeadShip(g) is int lead and > 0)
        {
            // the unit: its guide selected with the orders at its level, its card open
            selected = railRevealed = lead;
            ClearPicks();
            Chain(s);
            if (chain.Contains(g))
                PickLevel(s, chain.IndexOf(g) + 1);
            else
                (levelFor, levelPick) = (selected, -1);
            squadronCardOf = selected;
            cardOpen = true;
        }
        ImGui.PopID();
    }

    /// <summary>A unit's ships' states (sound, low, going down) in line order into <paramref name="states"/> from
    /// <paramref name="n"/>, with how many burn and their hits; the count after.</summary>
    int UnitShips(Snapshot s, int g, Span<byte> states, int n, ref int fires, ref int hits)
    {
        var gv = s.Groups;
        for (int i = 0; i < gv.MemberCount[g] && n < states.Length; i++)
        {
            int m = gv.Members[gv.MemberStart[g] + i];
            if (gv.Force[g])
            {
                if (railGroup.TryGetValue(m, out int sub))
                    n = UnitShips(s, sub, states, n, ref fires, ref hits);
            }
            else if (railShip.ContainsKey(m))
            {
                var (state, _, f, h) = ShipState(s, m);
                states[n++] = state;
                fires += f > 0 ? 1 : 0;
                hits += h;
            }
        }
        return n;
    }

    /// <summary>A ship's state for the rail: sound (0), low (1, under 80 % of her reserve left) or going down (2); the
    /// share of her reserve left, her fires and hits.</summary>
    (byte State, float Afloat, int Fires, int Hits) ShipState(Snapshot s, int e)
    {
        if (!railCond.TryGetValue(e, out int ci))
            return (0, 1, 0, 0);
        var c = s.Conditions;
        double water = c.WaterT[ci], reserve = c.ReserveT[ci];
        float afloat = (float)(reserve / Math.Max(reserve + water, 1e-6));
        byte state = c.Status[ci] == ShipStatus.GoingDown ? (byte)2 : afloat < 0.8f ? (byte)1 : (byte)0;
        return (state, afloat, c.Fires[ci], c.Hits[ci]);
    }

    /// <summary>A ship's row: her name (a flame when burning), what she fires at or why she's falling out, her flotation,
    /// speed and hits; the conn, or whose she is when alone. In a squadron (<see cref="RailRow.Group"/>) she's its
    /// <see cref="RailRow.At"/>-th member.</summary>
    void ShipRow(Snapshot s, ImDrawListPtr dl, Vector2 a, float w, in RailRow r, (float Doing, float BarL, float BarR, float Kn, float Hits, float Whose) cols, int n)
    {
        var v = s.Ships;
        int k = r.Index, e = v.Entity[k];
        ImGui.PushID(e * 2);
        var (clicked, hover) = HitRow("##ship", a, new Vector2(w, RowH));
        DragFrom(s, e, 0);
        DropOn(s, r.Group, r.At, e);
        if (railScrollTo == e)
        {
            var wp = ImGui.GetWindowPos();
            float h = ImGui.GetWindowHeight();
            if (a.Y < wp.Y || a.Y + RowH > wp.Y + h)
                ImGui.SetScrollY(ImGui.GetScrollY() + a.Y - wp.Y - h / 2);
        }
        bool on = selected == e && squadronCardOf != e || picked.Contains(e);
        dl.RowBg(a, w, RowH, n, on, hover);
        var (state, afloat, fires, hits) = ShipState(s, e);
        bool going = state == 2;
        float x = a.X + P(6) + r.Depth * P(10);
        dl.AddCircle(new Vector2(x + P(4), a.Y + RowH / 2), P(3.5f), U(Friend), 0, P(1.4f));
        x += P(12);
        bool showing = v.BeamOn[k] != 0 || v.StarOn[k] != 0;   // a light of hers out (docs/battle/night.md Q9)
        float marks = (fires > 0 ? P(10) : 0) + (showing ? P(10) : 0);
        float end = dl.TextClipped(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, a.Y, RowH)), cols.Doing - P(4) - marks, on ? T.Brass : going ? T.Hostile : T.Text,
            U8.W($"{ShipName(s, e)}"));
        if (fires > 0)
            Flame(dl, new Vector2(Math.Min(end + P(8), cols.Doing - P(8)), a.Y + RowH / 2 + P(1)), e);
        if (showing)
            Spark(dl, new Vector2(Math.Min(end + P(fires > 0 ? 18 : 8), cols.Doing - P(8)), a.Y + RowH / 2));
        // what she's doing: going down, falling out and why, her target; darkened, holding her fire
        bool holding = ((NightFlags)v.Night[k] & NightFlags.Holding) != 0;
        var doing = going ? "going down"u8 : v.Reason[k] != 0 ? U8.W($"{ReasonText(v.Reason[k])}") : TargetOf(s, k) is int t and > 0 ? U8.W($"on {LabelOf(s, t)}")
            : holding ? "dark · holding"u8 : "no target"u8;
        var colour = going ? T.Hostile : v.Reason[k] != 0 ? T.Amber : TargetOf(s, k) > 0 ? T.Text : T.Muted;
        dl.TextClipped(UiFonts.SansSmall, new Vector2(cols.Doing, Mid(UiFonts.SansSmall, a.Y, RowH)), cols.BarL - P(6), colour, doing);
        var bar = new Vector2(cols.BarL, a.Y + P(6.5f));
        dl.Bar(bar, new Vector2(cols.BarR - cols.BarL, P(7)), afloat, ShipStates[state]);
        dl.AddRect(bar, bar + new Vector2(cols.BarR - cols.BarL, P(7)), U(T.Edge));
        dl.TextRight(UiFonts.Mono, cols.Kn, Mid(UiFonts.Mono, a.Y, RowH), T.Bright, U8.W($"{v.Speed[k] / Handling.MsPerKnot:0}"));
        dl.TextRight(UiFonts.Mono, cols.Hits, Mid(UiFonts.Mono, a.Y, RowH), T.Bright, U8.W($"{hits}"));
        if (WhoseChip(dl, a, a.X + w - P(4), v.Conn[k] == 1, r.Group < 0 && Tagged(s, v.Commanded[k]), v.Commanded[k]))
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
            selected = railRevealed = e;
            ClearPicks();
            squadronCardOf = -1;
            cardOpen = true;
        }
        ImGui.PopID();
    }

    /// <summary>While a drag is on: the row at the table's end that takes a ship out of her squadron, or a unit out of its
    /// force.</summary>
    void DropRow(Snapshot s, ImDrawListPtr dl, Vector2 a, float w)
    {
        var (_, hover) = HitRow("##alone", a, new Vector2(w, RowH));
        DropOn(s, AloneTarget, -1, 0);
        dl.RowBg(a, w, RowH, 0, hover: hover);
        dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(6), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Brass, "+ On its own");
    }

    // ------------------------------------------------------------------ organising by drag and drop

    /// <summary>The last row a drag can start from: a ship (an entity) or a group (an id), named under the cursor.</summary>
    void DragFrom(Snapshot s, int ship, int group)
    {
        if (!ImGui.BeginDragDropSource())
            return;
        (dragShip, dragGroup) = (ship, group);
        ImGui.SetDragDropPayload(RailPayload, IntPtr.Zero, 0);
        U8.Text(ship != 0 ? U8.W($"{ShipName(s, ship)}") : railGroup.TryGetValue(group, out int gi) ? U8.W($"{s.Groups.Name[gi]}") : "?"u8);
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
        ImGui.PushStyleColor(ImGuiCol.DragDropTarget, T.Brass);
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
            if (dragShip != 0 && railSquadron.ContainsKey(dragShip))
                (first, what) = (GroupSystem.Remove(dragShip), $"{ShipName(s, dragShip)} out of her squadron");
            else if (dragGroup != 0 && railGroup.TryGetValue(dragGroup, out int og) && gv.Parent[og] != 0)
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
                if (gv.Force[g] || ship == dragShip || at < 0 && railSquadron.GetValueOrDefault(dragShip, -1) == g)
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
        if (dragGroup == 0 || g < 0 || !railGroup.TryGetValue(dragGroup, out int dg))
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

    // ------------------------------------------------------------------ the enemy

    /// <summary>A contact's group: what she's made out to be, else "not made out".</summary>
    static int EnemyKind(ContactsView c, int i) => c.Side[i] == 255 && c.Level[i] > ContactLevel.Smoke ? 0 : c.Level[i] < ContactLevel.Type ? 6 : c.Type[i] switch
    {
        ShipType.Capital => 1,
        ShipType.Cruiser => 2,
        ShipType.Destroyer => 3,
        ShipType.Small => 4,
        ShipType.Merchant => 5,
        _ => 6,
    };

    /// <summary>Whether an enemy group shows its contacts: as the player left it, else open unless it's more than six
    /// smaller ships (destroyers, small craft, merchants, the not made out); the unknowns always.</summary>
    bool EnemyOpen(int kind, int count) => enemyFold.TryGetValue(kind, out bool open) ? open : kind <= 2 || count <= 6;

    /// <summary>The enemy's rows: each kind's head, then (open) its contacts nearest first.</summary>
    void EnemyRows(Snapshot s)
    {
        var c = s.Contacts;
        int from = s.Ships.IndexOf(lastOwn);
        Vec3d at = from >= 0 ? s.Ships.PoseAt(from, world.DrawMs).Position : default;
        enemyOrder.Clear();
        for (int i = 0; i < c.Count; i++)
            if ((c.Seen[i] & SeenFlags.Gone) == 0)
                enemyOrder.Add((EnemyKind(c, i), from >= 0 ? Earth.Distance(at, c.Position[i]) : i, i));
        enemyOrder.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Range.CompareTo(b.Range));
        // each kind's head before its first, its shut contacts out
        int n = enemyOrder.Count;
        for (int j = 0; j < n;)
        {
            int kind = enemyOrder[j].Kind, end = j;
            while (end < n && enemyOrder[end].Kind == kind)
                end++;
            enemyOrder.Add((kind, enemyOrder[j].Range, EnemyGroupRow));
            if (EnemyOpen(kind, end - j))
                for (int q = j; q < end; q++)
                    enemyOrder.Add(enemyOrder[q]);
            j = end;
        }
        enemyOrder.RemoveRange(0, n);
    }

    void EnemyPane(Snapshot s, ImDrawListPtr dl, Vector2 a, Vector2 size, bool scroll)
    {
        var c = s.Contacts;
        int from = s.Ships.IndexOf(lastOwn);
        float right = dl.Pane(a, size, "ENEMY", from >= 0 ? null : "in the picture");
        if (from >= 0)
            dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(17) + Width(UiFonts.Head, "ENEMY") + P(6), Mid(UiFonts.SansSmall, a.Y, TitleH)), T.Muted, U8.W($"from {ShipName(s, lastOwn)}"));
        int sighted = 0;
        for (int i = 0; i < c.Count; i++)
            sighted += c.InSight[i] ? 1 : 0;
        dl.TextRight(UiFonts.SansSmall, right, Mid(UiFonts.SansSmall, a.Y, TitleH), T.Muted, U8.W($"{sighted} in sight"));
        float w = size.X - 2;
        var head = new Vector2(a.X + 1, a.Y + TitleH + 1);
        dl.Head(head, w);
        var cols = EnemyColumns(head.X + w - (scroll ? ImGui.GetStyle().ScrollbarSize : 0));
        dl.HeadCell(head, head.X + P(6), "CONTACT");
        dl.HeadCell(head, cols.Seen, "SEEN");
        dl.HeadCell(head, cols.Firing, "FIRING", right: true);
        dl.HeadCell(head, cols.Km, "KM", right: true);
        dl.HeadCell(head, cols.Brg, "BRG", right: true);
        if (ImGui.IsMouseHoveringRect(new Vector2(cols.Firing - P(36), head.Y), new Vector2(cols.Firing, head.Y + HeadH)) && ImGui.IsWindowHovered())
            ImGui.SetTooltip("how many of your ships are firing at her");
        if (BeginBody("##enemybody", a, size, out var body, head: HeadH))
        {
            var o = ImGui.GetCursorScreenPos();
            float rw = ImGui.GetContentRegionAvail().X;
            cols = EnemyColumns(o.X + rw);
            Vec3d at = from >= 0 ? s.Ships.PoseAt(from, world.DrawMs).Position : default;
            int target = railShip.TryGetValue(selected, out int sk) && OursAt(s, sk) ? TargetOf(s, sk) : 0;
            for (int j = 0; j < enemyOrder.Count; j++)
            {
                var p = new Vector2(o.X, o.Y + j * RowH);
                var (kind, range, i) = enemyOrder[j];
                if (i == EnemyGroupRow)
                    EnemyGroup(s, body, p, rw, cols, j, kind, from >= 0);
                else
                    EnemyRow(s, body, p, rw, cols, j, i, range, at, from >= 0, target);
            }
            if (enemyOrder.Count == 0)
                body.Text(UiFonts.SansSmall, new Vector2(o.X + P(6), Mid(UiFonts.SansSmall, o.Y, RowH)), T.Muted, "nothing in sight");
        }
        EndBody(enemyOrder.Count * RowH);
    }

    /// <summary>The enemy's columns from a row's right edge: what's seen, your ships firing, range and bearing.</summary>
    static (float Seen, float Firing, float Km, float Brg) EnemyColumns(float right)
    {
        float brg = right - P(6), km = brg - P(40), firing = km - P(42);
        return (firing - P(40) - P(100), firing, km, brg);
    }

    /// <summary>A kind's head: its twisty and name, how many and how many in sight, your ships firing at them, the
    /// nearest.</summary>
    void EnemyGroup(Snapshot s, ImDrawListPtr dl, Vector2 a, float w, (float Seen, float Firing, float Km, float Brg) cols, int j, int kind, bool haveFrom)
    {
        var c = s.Contacts;
        int count = 0, sighted = 0, firing = 0;
        double nearest = double.MaxValue;
        for (int i = 0; i < c.Count; i++)
            if ((c.Seen[i] & SeenFlags.Gone) == 0 && EnemyKind(c, i) == kind)
            {
                count++;
                sighted += c.InSight[i] ? 1 : 0;
                firing += railOn.GetValueOrDefault(c.Entity[i]);
            }
        for (int q = j + 1; q < enemyOrder.Count && enemyOrder[q].Kind == kind; q++)
            nearest = Math.Min(nearest, enemyOrder[q].Range);
        ImGui.PushID(-1 - kind);
        var (clicked, hover) = HitRow("##kind", a, new Vector2(w, RowH));
        bool open = EnemyOpen(kind, count);
        if (clicked)
            enemyFold[kind] = !open;
        dl.GroupBg(a, w, RowH, hover);
        dl.Twisty(new Vector2(a.X + P(9), a.Y + RowH / 2), open, hover ? T.Bright : T.Muted);
        dl.Text(UiFonts.SansBold, new Vector2(a.X + P(18), Mid(UiFonts.SansBold, a.Y, RowH)), T.Bright, EnemyKinds[kind]);
        dl.TextClipped(UiFonts.SansSmall, new Vector2(cols.Seen, Mid(UiFonts.SansSmall, a.Y, RowH)), cols.Firing - P(26), T.Muted, U8.W($"{count} · {sighted} in sight"));
        if (firing > 0)
            dl.TextRight(UiFonts.Mono, cols.Firing, Mid(UiFonts.Mono, a.Y, RowH), T.Bright, U8.W($"{firing}"));
        if (haveFrom && nearest < double.MaxValue)
            dl.TextRight(UiFonts.Mono, cols.Km, Mid(UiFonts.Mono, a.Y, RowH), T.Muted, U8.W($"{nearest / 1000:0.0}"));
        ImGui.PopID();
    }

    /// <summary>An enemy's row: her identity as held (doubtful dashed), what's seen of her (faint out of sight); how many
    /// of yours fire at her (the selection's target in the accent); range and bearing from the player's ship.</summary>
    void EnemyRow(Snapshot s, ImDrawListPtr dl, Vector2 a, float w, (float Seen, float Firing, float Km, float Brg) cols, int j, int i, double range, Vec3d from,
        bool haveFrom, int target)
    {
        var c = s.Contacts;
        int e = c.Entity[i];
        ImGui.PushID(e * 2);
        var (clicked, hover) = HitRow("##enemy", a, new Vector2(w, RowH));
        if (clicked)
        {
            long now = Environment.TickCount64;
            if (railClickShip == e && now - railClickMs < 400)
                CentreOn(c.Position[i]);
            (railClickShip, railClickMs) = (e, now);
            selected = railRevealed = e;
            ClearPicks();
            squadronCardOf = -1;
            cardOpen = true;
        }
        bool on = selected == e;
        dl.RowBg(a, w, RowH, j, on, hover);
        bool sure = c.Level[i] >= ContactLevel.Class, seen = c.InSight[i];
        Diamond(dl, new Vector2(a.X + P(22), a.Y + RowH / 2), sure, seen, c.Side[i] == 255);
        dl.TextClipped(UiFonts.SansSmall, new Vector2(a.X + P(30), Mid(UiFonts.SansSmall, a.Y, RowH)), cols.Seen - P(6), on ? T.Brass : seen ? T.Text : T.Faint,
            U8.W($"{Identity(c, i)}"));
        var what = c.Seen[i] != SeenFlags.None ? U8.W($"{SeenWords(c.Seen[i])}") : seen ? "no damage seen"u8
            : U8.W($"last seen {new WorldTime(c.SeenMs[i]).ToUtc().Hour:00}:{new WorldTime(c.SeenMs[i]).ToUtc().Minute:00}");
        dl.TextClipped(UiFonts.SansSmall, new Vector2(cols.Seen, Mid(UiFonts.SansSmall, a.Y, RowH)), cols.Firing - P(26),
            !seen ? T.Faint : c.Seen[i] != SeenFlags.None ? T.Text : T.Muted, what);
        if (railOn.GetValueOrDefault(e) is int firing and > 0)
            dl.TextRight(UiFonts.Mono, cols.Firing, Mid(UiFonts.Mono, a.Y, RowH), e == target ? T.Brass : T.Bright, U8.W($"{firing}"));
        if (haveFrom)
        {
            double brg = (Earth.Bearing(from, c.Position[i]) % 360 + 360) % 360;
            var figure = seen ? T.Bright : T.Muted;
            dl.TextRight(UiFonts.Mono, cols.Km, Mid(UiFonts.Mono, a.Y, RowH), figure, U8.W($"{range / 1000:0.0}"));
            dl.TextRight(UiFonts.Mono, cols.Brg, Mid(UiFonts.Mono, a.Y, RowH), figure, U8.W($"{brg:000}°"));
        }
        ImGui.PopID();
    }

    /// <summary>What's seen of a ship, in words: "burning · low · by the bow".</summary>
    /// <summary>A contact's mark: the hostile's diamond, dashed while she isn't made out, faint out of sight.</summary>
    static void Diamond(ImDrawListPtr dl, Vector2 d, bool sure, bool seen, bool unknown = false)
    {
        float r = P(3.6f);
        if (unknown)
        {
            // not known for friend or foe (docs/battle/night.md Q9): APP-6's quatrefoil in the unknown's cream
            uint ink = U(T.Unknown with { W = seen ? 0.85f : 0.45f });
            for (int q = 0; q < 4; q++)
            {
                var o = q switch { 0 => new Vector2(0, -1), 1 => new Vector2(1, 0), 2 => new Vector2(0, 1), _ => new Vector2(-1, 0) };
                dl.AddCircleFilled(d + o * r * 0.48f, r * 0.55f, ink);
            }
            return;
        }
        uint col = U(T.Hostile with { W = seen ? 1 : 0.55f });
        if (sure)
        {
            dl.AddQuadFilled(d + new Vector2(0, -r), d + new Vector2(r, 0), d + new Vector2(0, r), d + new Vector2(-r, 0), U(T.Hostile with { W = seen ? 0.25f : 0.12f }));
            dl.AddQuad(d + new Vector2(0, -r), d + new Vector2(r, 0), d + new Vector2(0, r), d + new Vector2(-r, 0), col, P(1.3f));
        }
        else
            for (int q = 0; q < 4; q++)
            {
                var p0 = q switch { 0 => new Vector2(0, -r), 1 => new Vector2(r, 0), 2 => new Vector2(0, r), _ => new Vector2(-r, 0) };
                var p1 = q switch { 0 => new Vector2(r, 0), 1 => new Vector2(0, r), 2 => new Vector2(-r, 0), _ => new Vector2(0, -r) };
                dl.AddLine(d + p0, d + (p0 + p1) / 2, col, P(1.3f));
            }
    }

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
