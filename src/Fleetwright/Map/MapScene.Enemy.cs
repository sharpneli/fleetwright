using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using Fleetwright.Ui;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The enemy's card and strip, and a unit's card (docs/map-ui/battle-hud.md U4). An enemy is shown only as the
/// player holds her (her contact: identity, estimated course and speed, what's seen of her, the book's figures once her
/// class is made out), with the player's fire on her and hers, and the orders against her for the ship the player last
/// chose and her unit. A unit's card: its ships in a table, its formation and task, whose it is.</summary>
public sealed partial class MapScene
{
    void EnemyCard(Snapshot s, int e)
    {
        var c = s.Contacts;
        int i = ContactIndex(c, e);
        if (i < 0)
            return;
        var cls = c.Level[i] >= ContactLevel.Class ? world.Classes.TryGet(c.TakenFor[i]) : null;
        var sub = c.Level[i] switch
        {
            ContactLevel.Name => cls != null ? U8.W($"{cls.Name}: identified") : "identified"u8,
            ContactLevel.Class => "her class made out by her silhouette, not her name"u8,
            ContactLevel.Type => U8.W($"a {TypeWord(c.Type[i])}: her class not made out"),
            ContactLevel.Ship => "a ship, not made out"u8,
            _ => "smoke on the horizon"u8,
        };
        var seen = new WorldTime(c.SeenMs[i]).ToUtc();
        Head(U8.W($"{Identity(c, i)}"), T.Hostile, sub, "enemy"u8, ChipKind.Hostile,
            c.InSight[i] ? "in sight"u8 : U8.W($"last seen {seen.Hour:00}:{seen.Minute:00}"), ChipKind.Plain,
            c.SmokeInSight[i] ? "her smoke"u8 : default, ChipKind.Plain);

        // range and bearing from the player's ship; her course and speed as estimated
        int from = s.Ships.IndexOf(lastOwn);
        var at = c.Position[i];
        var row = new Vector2(cardX, cardY);
        float x = row.X, unit = cardW / 4;
        if (from >= 0)
        {
            var p = s.Ships.PoseAt(from, world.DrawMs).Position;
            FigureCell(row, ref x, unit, "RANGE", U8.W($"{Earth.Distance(p, at) / 1000:0.0} km"), ""u8, T.Bright);
            FigureCell(row, ref x, unit, "BEARING", U8.W($"{(Earth.Bearing(p, at) % 360 + 360) % 360:000}°"), ""u8, T.Bright);
        }
        else
        {
            FigureCell(row, ref x, unit, "RANGE", "-"u8, ""u8, T.Bright);
            FigureCell(row, ref x, unit, "BEARING", "-"u8, ""u8, T.Bright);
        }
        double ms = c.Velocity[i].Length;
        FigureCell(row, ref x, unit, "COURSE", ms > 0.5 ? U8.W($"~{(Earth.Heading(at, c.Velocity[i]) % 360 + 360) % 360:000}°") : "-"u8, U8.W($"±{c.MajorM[i]:0} m"), T.Bright);
        FigureCell(row, ref x, cardX + cardW - x, "SPEED", U8.W($"~{ms / Handling.MsPerKnot:0} kn"), "est."u8, T.Bright, last: true);
        EndFigures();

        float right = cardX + cardW - P(6);
        if (Strip("WHAT WE SEE", "a lookout's", ""u8,
                "Only what a lookout could see: fires, a list, low in the water, stopped, her guns silent. Her hits and flooding stay hers."))
        {
            if (c.Seen[i] == SeenFlags.None)
                NoneRow(c.InSight[i] ? "no damage seen"u8 : "nothing: she's out of sight"u8);
            else
            {
                int n = 0;
                var r = CardRow(ref n);
                Words(r, r.X + P(6), right, T.Amber, U8.W($"{SeenWords(c.Seen[i])}"));
            }
        }
        if (cls != null && Strip("FROM THE BOOK", "not what she has left", U8.W($"{cls.Name}")))
        {
            int n = 0;
            for (int b = 0; b < cls.Batteries.Length; b++)
            {
                var r = CardRow(ref n);
                Words(r, r.X + P(6), right, T.Text, U8.W($"{BatteryLabel(cls, b)} {(cls.Batteries[b].Kind == Battery.Main ? "main" : "secondary")}"));
            }
            var q = CardRow(ref n);
            Words(q, q.X + P(6), right, T.Text, U8.W($"{cls.Motion.MaxSpeedKn:0.0} kn · {cls.Length:0} m long"));
            if (cls.Launchers.Length > 0)
            {
                q = CardRow(ref n);
                Words(q, q.X + P(6), right, T.Text, U8.W($"torpedo tubes: {cls.Launchers.Length}"));
            }
        }
        if (Strip("OUR FIRE ON HER", null, ""u8))
        {
            bool any = false;
            var b = s.Batteries;
            int n = 0;
            for (int j = 0; j < b.Count; j++)
                if (b.Target[j] == e)
                {
                    any = true;
                    var r = CardRow(ref n);
                    float fx = right;
                    fx = Figure(r, fx, T.Bright, U8.W($"{b.Hits[j]}"));
                    fx = Figure(r, fx - P(3), T.Muted, "hits"u8, UiFonts.SansSmall);
                    fx = Figure(r, fx - P(6), T.Bright, U8.W($"{b.Straddles[j]}"));
                    fx = Figure(r, fx - P(3), T.Muted, "str"u8, UiFonts.SansSmall);
                    fx = Figure(r, fx - P(6), T.Bright, U8.W($"{b.Salvos[j]}"));
                    fx = Figure(r, fx - P(3), T.Muted, "salvos"u8, UiFonts.SansSmall);
                    float wx = Words(r, r.X + P(6), fx - P(8), T.Bright, U8.W($"{ShipName(s, b.Shooter[j])}"));
                    Words(r, wx + P(6), fx - P(8), T.Muted, U8.W($"{StateText(b.State[j], b.Phase[j])}"));
                }
            if (!any)
                NoneRow("none of ours on her"u8);
        }
        if (Strip("HER FIRE", null, ""u8))
        {
            int on = HerTarget(e);
            if (on > 0)
            {
                int n = 0;
                var r = CardRow(ref n);
                float wx = Words(r, r.X + P(6), right, T.Muted, "on"u8);
                Words(r, wx + P(4), right, T.Bright, LabelOf(s, on));
            }
            else
                NoneRow("not seen firing at ours"u8);
        }
        if (Strip("ORDERS", null, ""u8))
        {
            ImGui.SetCursorScreenPos(new Vector2(cardX + P(6), cardY + P(5)));
            EnemyOrders(s, e, from);
            cardY = ImGui.GetCursorScreenPos().Y + P(5);
        }
    }

    /// <summary>Whom an enemy last fired at among the player's ships (the feed's newest salvo of hers), or 0.</summary>
    int HerTarget(int e)
    {
        var lines = feed.Lines;
        for (int i = lines.Count - 1, n = 0; i >= 0 && n < 2000; i--, n++)
            if (lines[i].Shooter == e && lines[i].Kind == CombatEvent.Fired)
                return lines[i].Target;
        return 0;
    }

    /// <summary>The orders against an enemy: engage her with the player's ship or her unit, torpedoes at her.</summary>
    void EnemyOrders(Snapshot s, int e, int from)
    {
        if (from < 0)
        {
            ImGui.TextDisabled("choose one of your ships to give orders against her");
            return;
        }
        var gv = s.Groups;
        int g = gv.IndexOfShip(lastOwn);
        var o = world.Orders;
        if (g >= 0)
        {
            if (Seg(U8Label("Engage her with ", gv.Name[g]), first: true))
                world.Send(o.Engage(Unit.Group(gv.Id[g]), e));
            ImGui.SameLine(0, 4 * UiFonts.Scale);
        }
        if (Seg(U8Label(g >= 0 ? "or " : "Engage her with ", ShipName(s, lastOwn)), first: g < 0))
            world.Send(o.Engage(Unit.Ship(lastOwn), e));
        // torpedoes: loaded, and she's within their slow setting's reach
        var cls = world.Classes.TryGet(s.Ships.Class[from]);
        int ci = ContactIndex(s.Contacts, e);
        double reach = cls != null && cls.Launchers.Length > 0 ? cls.Torpedoes[cls.Launchers[0].Torpedo].SlowRangeM : 0;
        double range = ci >= 0 ? Earth.Distance(s.Ships.PoseAt(from, world.DrawMs).Position, s.Contacts.Position[ci]) : double.MaxValue;
        bool loaded = s.Ships.TorpedoesLoaded[from] > 0, inReach = range <= reach;
        ImGui.SameLine(0, 4 * UiFonts.Scale);
        if (Seg("Torpedoes##et", enabled: loaded && inReach, first: true))
            world.Send(TorpedoSystem.Launch(lastOwn, e, s.Ships.TorpedoesLoaded[from]));
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(!loaded ? "none loaded" : !inReach ? "out of their reach" : "fire what's loaded and bears");
    }

    // the orders' button labels, made once per name
    readonly Dictionary<(string, string), string> orderLabels = [];

    string U8Label(string verb, string name) =>
        orderLabels.TryGetValue((verb, name), out var l) ? l : orderLabels[(verb, name)] = verb + name;

    /// <summary>The strip for an enemy: who she is and how far, the orders against her.</summary>
    void EnemyStrip(Snapshot s, int e)
    {
        var c = s.Contacts;
        int i = ContactIndex(c, e);
        if (i < 0)
            return;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, pad = 8 * sc;
        ImGui.SetNextWindowPos(new Vector2(StripCentre(vp), vp.Pos.Y + vp.Size.Y - pad - ImGui.GetTextLineHeight() - 4 * sc), ImGuiCond.Always, new Vector2(0.5f, 1));
        ImGui.SetNextWindowBgAlpha(0.9f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 7) * sc);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(7, 3) * sc);
        if (ImGui.Begin("##estrip", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove))
        {
            ImGui.BeginGroup();
            ImGui.PushFont(UiFonts.Title);
            ImGui.TextColored(new Vector4(0.95f, 0.6f, 0.55f, 1), Identity(c, i));
            ImGui.PopFont();
            int from = s.Ships.IndexOf(lastOwn);
            if (from >= 0)
                U8.Text(U8.W($"enemy · {Earth.Distance(s.Ships.PoseAt(from, world.DrawMs).Position, c.Position[i]) / 1000:0.0} km from {ShipName(s, lastOwn)}"), Theme.Muted);
            else
                ImGui.TextDisabled("enemy");
            ImGui.EndGroup();
            ImGui.SameLine(0, 22 * sc);
            ImGui.BeginGroup();
            EnemyOrders(s, e, from);
            ImGui.EndGroup();
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
        var hint = "Select one of your ships and right-click her to engage · Esc: deselect"u8;
        var size = U8.Size(hint);
        var at = new Vector2(StripCentre(vp) - size.X / 2, vp.Pos.Y + vp.Size.Y - 6 * sc - size.Y);
        U8.Draw(ImGui.GetBackgroundDrawList(), at, ImGui.GetColorU32(Theme.Muted), hint);
    }

    // ------------------------------------------------------------------ a unit's card

    void SquadronCard(Snapshot s, int g)
    {
        var gv = s.Groups;
        var v = s.Ships;
        bool action = false;
        for (int i = 0; i < gv.MemberCount[g] && !gv.Force[g]; i++)
            action |= v.IndexOf(gv.Members[gv.MemberStart[g] + i]) is int k and >= 0 && v.Encounter[k] != 0;
        int top = TopOf(gv, g);
        var whose = gv.Commanded[top] == 1 ? "AI commander"u8 : gv.Commanded[top] == 2 ? "yours"u8 : "your orders"u8;
        var whoseKind = gv.Commanded[top] == 0 ? ChipKind.Amber : ChipKind.Plain;
        var task = gv.Task[g] != 0 ? U8.W($"{TaskText(gv.Task[g], gv.TaskStatus[g])}") : default;
        var sub = U8.W($"{GroupFormationNames[(int)gv.Formation[g]]}, {gv.SpacingM[g]:0} m · guide {(gv.GuideShip[g] != 0 ? ShipName(s, gv.GuideShip[g]) : "-")}");
        if (action)
            Head(U8.W($"{gv.Name[g]}"), T.Bright, sub, "in action"u8, ChipKind.Amber, whose, whoseKind, task, ChipKind.Plain);
        else
            Head(U8.W($"{gv.Name[g]}"), T.Bright, sub, whose, whoseKind, task, ChipKind.Plain);
        float right = cardX + cardW - P(6);
        if (Strip(gv.Force[g] ? "UNITS" : "SHIPS", null, U8.W($"{gv.MemberCount[g]}")))
        {
            int n = 0;
            if (gv.Force[g])
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    if (gv.IndexOfGroup(gv.Members[gv.MemberStart[g] + i]) is int sub2 and >= 0)
                    {
                        var r = CardRow(ref n);
                        Words(r, r.X + P(6), right - P(60), T.Bright, U8.W($"{gv.Name[sub2]}"));
                        Figure(r, right, T.Muted, U8.W($"{gv.MemberCount[sub2]} ships"), UiFonts.SansSmall);
                    }
                }
            else
            {
                // a row a ship: afloat, hits, mounts working, speed; a click selects her
                var a = new Vector2(cardX, cardY);
                float cHits = right - P(110), cGuns = right - P(36), cKn = right, bar = right - P(186);
                cdl.Head(a, cardW);
                cdl.HeadCell(a, a.X + P(6), "Ship");
                cdl.HeadCell(a, bar, "Afloat");
                cdl.HeadCell(a, cHits, "Hits", right: true);
                cdl.HeadCell(a, cGuns, "Guns", right: true);
                cdl.HeadCell(a, cKn, "kn", right: true);
                cardY += HeadH;
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    int e = gv.Members[gv.MemberStart[g] + i];
                    int k = v.IndexOf(e);
                    if (k < 0)
                        continue;
                    int ci = s.Conditions.IndexOf(e);
                    var r = new Vector2(cardX, cardY);
                    ImGui.PushID(e);
                    var (click, hover) = HitRow("##ship", r, new Vector2(cardW, RowH));
                    ImGui.PopID();
                    if (click)
                    {
                        selected = e;
                        squadronCardOf = -1;
                    }
                    cdl.RowBg(r, cardW, RowH, n++, hover: hover);
                    cardY += RowH;
                    Words(r, r.X + P(6), bar - P(8), T.Bright, U8.W($"{ShipName(s, e)}"));
                    double water = ci >= 0 ? s.Conditions.WaterT[ci] : 0, reserve = ci >= 0 ? s.Conditions.ReserveT[ci] : 1;
                    float afloat = (float)(reserve / Math.Max(reserve + water, 1e-6));
                    cdl.Bar(new Vector2(bar, r.Y + P(7)), new Vector2(P(44), P(6)), afloat, afloat > 0.8f ? Ok : afloat > 0.6f ? T.Amber : Bad);
                    int hits = ci >= 0 ? s.Conditions.Hits[ci] : 0;
                    Figure(r, cHits, hits > 0 ? T.Amber : T.Bright, U8.W($"{hits}"));
                    Figure(r, cGuns, T.Bright, ci >= 0 ? U8.W($"{s.Conditions.MountsWorking[ci]}/{s.Conditions.Mounts[ci]}") : "all"u8);
                    Figure(r, cKn, T.Bright, U8.W($"{v.Speed[k] / Handling.MsPerKnot:0}"));
                }
            }
        }
        if (Strip("ORDERS", null, ""u8))
        {
            ImGui.SetCursorScreenPos(new Vector2(cardX + P(6), cardY + P(5)));
            var unit = Unit.Group(gv.Id[g]);
            if (Seg(FormationLabels[(int)gv.Formation[g]], first: true, menu: true))
                ImGui.OpenPopup("##cardform");
            if (ImGui.BeginPopup("##cardform"))
            {
                FormationEdit(gv, g, ref spacingEdit, "##cf");
                ImGui.EndPopup();
            }
            if (Seg("Fight"))
                Behaviour(3, unit, 0);
            if (Seg("Withdraw"))
                world.Send(world.Orders.WithdrawHome(unit));
            if (gv.Parent[g] == 0)
            {
                bool his = gv.Commanded[g] == 1;
                if (Seg(his ? "Take command" : "Leave to the commander"))
                    world.Send(world.Orders.Automate(unit, !his));
            }
            cardY = ImGui.GetCursorScreenPos().Y + P(5);
        }
    }
}
