using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;

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
        ImGui.PushFont(UiFonts.PosterFigure);
        ImGui.TextColored(new Vector4(0.95f, 0.6f, 0.55f, 1), Identity(c, i));
        ImGui.PopFont();
        ImGui.PushTextWrapPos(0);
        U8.Text(c.Level[i] switch
        {
            ContactLevel.Name => cls != null ? U8.W($"{cls.Name}: identified") : "identified"u8,
            ContactLevel.Class => "her class made out by her silhouette, not her name"u8,
            ContactLevel.Type => U8.W($"a {TypeWord(c.Type[i])}: her class not made out"),
            ContactLevel.Ship => "a ship, not made out"u8,
            _ => "smoke on the horizon"u8,
        }, DesignerTheme.Muted);
        ImGui.PopTextWrapPos();
        Chip("ENEMY"u8, Bad);
        if (c.InSight[i])
            Chip("IN SIGHT"u8, DesignerTheme.Text);
        else
        {
            var seen = new WorldTime(c.SeenMs[i]).ToUtc();
            Chip(U8.W($"LAST SEEN {seen.Hour:00}:{seen.Minute:00}"), DesignerTheme.Muted);
        }
        if (c.SmokeInSight[i])
            Chip("HER SMOKE"u8, DesignerTheme.Muted);
        ImGui.NewLine();

        // range and bearing from the player's ship; her course and speed as estimated
        int from = s.Ships.IndexOf(lastOwn);
        ImGui.Spacing();
        if (ImGui.BeginTable("##ebig", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            var at = c.Position[i];
            if (from >= 0)
            {
                var p = s.Ships.PoseAt(from, world.DrawMs).Position;
                Fig4("RANGE", U8.W($"{Earth.Distance(p, at) / 1000:0.0} km"), U8.W($"from {ShipName(s, lastOwn)}"));
                Fig4("BEARING", U8.W($"{(Earth.Bearing(p, at) % 360 + 360) % 360:000}°"), default);
            }
            else
            {
                Fig4("RANGE", "-"u8, default);
                Fig4("BEARING", "-"u8, default);
            }
            double ms = c.Velocity[i].Length;
            Fig4("COURSE", ms > 0.5 ? U8.W($"~{(Earth.Heading(at, c.Velocity[i]) % 360 + 360) % 360:000}°") : "-"u8, U8.W($"±{c.MajorM[i]:0} m"));
            Fig4("SPEED", U8.W($"~{ms / Handling.MsPerKnot:0} kn"), "estimated"u8);
            ImGui.EndTable();
        }

        if (Section("What we see", default))
        {
            if (c.Seen[i] == SeenFlags.None)
                ImGui.TextDisabled(c.InSight[i] ? "no damage seen" : "nothing: she's out of sight");
            else
                U8.Text(U8.W($"{SeenWords(c.Seen[i])}"), new Vector4(0.94f, 0.7f, 0.66f, 1));
            ImGui.PushTextWrapPos(0);
            ImGui.TextDisabled("Only what a lookout could see: fires, a list, low in the water, stopped, her guns silent. Her hits and flooding stay hers.");
            ImGui.PopTextWrapPos();
        }
        if (cls != null && Section("From the book", U8.W($"{cls.Name}")))
        {
            for (int b = 0; b < cls.Batteries.Length; b++)
                U8.Text(U8.W($"{BatteryLabel(cls, b)} {(cls.Batteries[b].Kind == Battery.Main ? "main" : "secondary")}"));
            U8.Text(U8.W($"{cls.Motion.MaxSpeedKn:0.0} kn · {cls.Length:0} m long"));
            if (cls.Launchers.Length > 0)
                U8.Text(U8.W($"torpedo tubes: {cls.Launchers.Length}"));
            ImGui.TextDisabled("The book's figures, not what she has left.");
        }
        if (Section("Our fire on her", default))
        {
            bool any = false;
            var b = s.Batteries;
            for (int j = 0; j < b.Count; j++)
                if (b.Target[j] == e)
                {
                    any = true;
                    U8.Text(U8.W($"{ShipName(s, b.Shooter[j])}: {StateText(b.State[j], b.Phase[j])} · {b.Salvos[j]} salvos, {b.Straddles[j]} straddles, {b.Hits[j]} hits"));
                }
            if (!any)
                ImGui.TextDisabled("none of ours on her");
        }
        if (Section("Her fire", default))
        {
            int on = HerTarget(e);
            if (on > 0)
                U8.Text(U8.W($"on {LabelOf(s, on)}"));
            else
                ImGui.TextDisabled("not seen firing at ours");
        }
        ImGui.Spacing();
        EnemyOrders(s, e, from);
    }

    static void Fig4(string label, ReadOnlySpan<byte> value, ReadOnlySpan<byte> sub)
    {
        ImGui.TableNextColumn();
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.TextDisabled(label);
        ImGui.PopFont();
        ImGui.PushFont(UiFonts.Mono);
        U8.Text(value, DesignerTheme.Bright);
        ImGui.PopFont();
        ImGui.PushFont(UiFonts.SansSmall);
        U8.Text(sub, DesignerTheme.Muted);
        ImGui.PopFont();
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
                U8.Text(U8.W($"enemy · {Earth.Distance(s.Ships.PoseAt(from, world.DrawMs).Position, c.Position[i]) / 1000:0.0} km from {ShipName(s, lastOwn)}"), DesignerTheme.Muted);
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
        U8.Draw(ImGui.GetBackgroundDrawList(), at, ImGui.GetColorU32(DesignerTheme.Muted), hint);
    }

    // ------------------------------------------------------------------ a unit's card

    void SquadronCard(Snapshot s, int g)
    {
        var gv = s.Groups;
        var v = s.Ships;
        ImGui.PushFont(UiFonts.PosterFigure);
        U8.Text(U8.W($"{gv.Name[g]}"), DesignerTheme.Bright);
        ImGui.PopFont();
        U8.Text(U8.W($"{GroupFormationNames[(int)gv.Formation[g]]}, {gv.SpacingM[g]:0} m · guide {(gv.GuideShip[g] != 0 ? ShipName(s, gv.GuideShip[g]) : "-")}"), DesignerTheme.Muted);
        bool action = false;
        for (int i = 0; i < gv.MemberCount[g] && !gv.Force[g]; i++)
            action |= v.IndexOf(gv.Members[gv.MemberStart[g] + i]) is int k and >= 0 && v.Encounter[k] != 0;
        if (action)
            Chip("IN ACTION"u8, DesignerTheme.Amber);
        int top = TopOf(gv, g);
        Chip(gv.Commanded[top] == 1 ? "AI COMMANDER"u8 : gv.Commanded[top] == 2 ? "YOURS"u8 : "YOUR ORDERS"u8, gv.Commanded[top] == 1 ? Friend : DesignerTheme.Brass);
        if (gv.Task[g] != 0)
            Chip(U8.W($"{TaskText(gv.Task[g], gv.TaskStatus[g])}"), DesignerTheme.Amber);
        ImGui.NewLine();
        if (Section(gv.Force[g] ? "Units" : "Ships", U8.W($"{gv.MemberCount[g]}")))
        {
            if (gv.Force[g])
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    if (gv.IndexOfGroup(gv.Members[gv.MemberStart[g] + i]) is int sub and >= 0)
                        U8.Text(U8.W($"{gv.Name[sub]} · {gv.MemberCount[sub]} ships"));
                }
            else if (ImGui.BeginTable("##sq", 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Ship", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Afloat", ImGuiTableColumnFlags.WidthFixed, 50 * UiFonts.Scale);
                ImGui.TableSetupColumn("Hits", ImGuiTableColumnFlags.WidthFixed, 32 * UiFonts.Scale);
                ImGui.TableSetupColumn("Guns", ImGuiTableColumnFlags.WidthFixed, 40 * UiFonts.Scale);
                ImGui.TableSetupColumn("kn", ImGuiTableColumnFlags.WidthFixed, 26 * UiFonts.Scale);
                ImGui.PushFont(UiFonts.SansSmall);
                ImGui.TableHeadersRow();
                ImGui.PopFont();
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    int e = gv.Members[gv.MemberStart[g] + i];
                    int k = v.IndexOf(e);
                    if (k < 0)
                        continue;
                    int ci = s.Conditions.IndexOf(e);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.PushID(e);
                    if (ImGui.Selectable(ShipName(s, e), false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        selected = e;
                        squadronCardOf = -1;
                    }
                    ImGui.PopID();
                    ImGui.TableNextColumn();
                    double water = ci >= 0 ? s.Conditions.WaterT[ci] : 0, reserve = ci >= 0 ? s.Conditions.ReserveT[ci] : 1;
                    float afloat = (float)(reserve / Math.Max(reserve + water, 1e-6));
                    Bar(afloat, afloat > 0.8f ? Ok : afloat > 0.6f ? DesignerTheme.Amber : Bad, 44);
                    ImGui.TableNextColumn();
                    U8.Right(U8.W($"{(ci >= 0 ? s.Conditions.Hits[ci] : 0)}"), DesignerTheme.Bright);
                    ImGui.TableNextColumn();
                    U8.Right(ci >= 0 ? U8.W($"{s.Conditions.MountsWorking[ci]}/{s.Conditions.Mounts[ci]}") : "all"u8, DesignerTheme.Bright);
                    ImGui.TableNextColumn();
                    U8.Right(U8.W($"{v.Speed[k] / Handling.MsPerKnot:0}"), DesignerTheme.Bright);
                }
                ImGui.EndTable();
            }
        }
        if (Section("Orders", default))
        {
            var unit = Unit.Group(gv.Id[g]);
            if (Seg(FormationLabels[(int)gv.Formation[g]], first: true))
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
        }
    }
}
