using System.Globalization;
using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The selection strip (docs/map-ui/battle-hud.md U2): the orders, at the bottom, in columns: who gets them
/// (the ship or a unit she's in), the helm, the guns, torpedoes and smoke, the captain's or the squadron's task (the
/// search pattern, the formation and the squadron's own business in popups). And the cursor's verb: what a right-click
/// does where it is (steer, engage, follow), and the armed picks ("Engage…", "Fire at…", "Follow…", "Run from…") a
/// click completes.</summary>
public sealed partial class MapScene
{
    /// <summary>What the next click on the map does, armed from the strip or the task menu (the dev's salvo is
    /// <see cref="fireArmed"/>): on a ship of ours, an enemy, or the sea (docs/battle/admirals-orders.md O9).</summary>
    enum ClickPick : byte
    {
        None, Engage, Torpedoes, Follow, Run,
        Screen, Scout, Cover, Join, Station, Fight, Intercept, Shadow, Attack, GoTo, Patrol, Search, Deploy, DivideFrom,
    }

    ClickPick armedPick;
    long lastClickMs;
    int lastClickShip = -1;

    static readonly Vector4 SegOff = new(1, 1, 1, 0.05f), SegHover = new(1, 1, 1, 0.1f), ArmedBg = new(0.29f, 0.23f, 0.08f, 1),
        ArmedText = new(1f, 0.85f, 0.54f, 1);

    // the strip's strings, made when what they say changes
    (int Ship, int Course, int Kn) helmKey = (-1, -1, -1);
    string helmText = "", courseText = "";
    string connText = "", connSource = "";   // the helm's head on the conn, made when the helm's text or the conn changes
    (bool, bool) connKey;
    (int Ship, int Target, byte Fire) gunsKey = (-1, -1, 255);
    string gunsText = "";
    (int Ship, int Loaded, int Left) torpKey = (-1, -1, -1);
    string torpText = "";
    int nameShip = -1, nameGeneration = -1;
    string nameText = "", classText = "";

    /// <summary>The strip, while one of the player's ships (or, with truth, any ship) is selected.</summary>
    void Strip(Snapshot s)
    {
        int k = s.Ships.IndexOf(selected);
        if (k < 0)
        {
            armedPick = ClickPick.None;
            taskOpen = false;
            if (selected >= 0)
                EnemyStrip(s, selected);
            return;
        }
        var cls = world.Classes.TryGet(s.Ships.Class[k]);
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, pad = 8 * sc;
        ImGui.SetNextWindowPos(new Vector2(StripCentre(vp), vp.Pos.Y + vp.Size.Y - pad - ImGui.GetTextLineHeight() - 4 * sc), ImGuiCond.Always, new Vector2(0.5f, 1));
        ImGui.SetNextWindowBgAlpha(0.9f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 7) * sc);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(7, 3) * sc);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 4) * sc);
        bool open = ImGui.Begin("##strip", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                           ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove);
        if (open)
        {
            var (unit, cmd) = Commanded(s);
            int kc = s.Ships.IndexOf(cmd);
            if (kc < 0)
                kc = k;
            StripTexts(s, k, kc, cls);
            Span<float> seps = stackalloc float[5];
            int n = 0;
            float gap = 22 * sc;

            Who(s, k, cls);
            seps[n++] = ImGui.GetItemRectMax().X + gap / 2;
            ImGui.SameLine(0, gap);
            HelmColumn(s, unit, kc);
            seps[n++] = ImGui.GetItemRectMax().X + gap / 2;
            ImGui.SameLine(0, gap);
            Guns(s, unit, k, cls);
            seps[n++] = ImGui.GetItemRectMax().X + gap / 2;
            ImGui.SameLine(0, gap);
            TorpedoesAndSmoke(s, unit, k, cls);
            seps[n++] = ImGui.GetItemRectMax().X + gap / 2;
            ImGui.SameLine(0, gap);
            Task(s, unit, kc);

            var dl = ImGui.GetWindowDrawList();
            var wp = ImGui.GetWindowPos();
            float top = wp.Y + 6 * sc, bottom = wp.Y + ImGui.GetWindowHeight() - 6 * sc;
            for (int i = 0; i < n; i++)
                dl.AddLine(new Vector2(seps[i], top), new Vector2(seps[i], bottom), ImGui.GetColorU32(DesignerTheme.Line), 1);
            stripTop = wp.Y;
        }
        ImGui.End();
        ImGui.PopStyleVar(3);
        StripHint(vp, s);
        TaskMenu(s);
    }

    /// <summary>The line under the strip: what the mouse does, or what the armed pick waits for.</summary>
    void StripHint(ImGuiViewportPtr vp, Snapshot s)
    {
        string hint;
        if (armedPick != ClickPick.None)
        {
            if (armedPick != hintPick)
                (hintPick, hintText) = (armedPick, $"{PickVerb(armedPick)}: {PickClick(armedPick)} · Esc cancels");
            hint = hintText;
        }
        else
            hint = SmartScreens(s) ? "Right-click: steer · on an enemy: engage · on your ship: screen · Shift: waypoint · Alt: this ship only · C: the conn · T: tasks · Tab: the next ship"
                : "Right-click: steer · on an enemy: engage · on your ship: follow · Shift: waypoint · Alt: this ship only · C: the conn · T: tasks · Tab: the next ship";
        var size = ImGui.CalcTextSize(hint);
        var at = new Vector2(StripCentre(vp) - size.X / 2, vp.Pos.Y + vp.Size.Y - 6 * UiFonts.Scale - size.Y);
        var dl = ImGui.GetBackgroundDrawList();
        dl.AddText(at + Vector2.One, 0xC0000000, hint);
        dl.AddText(at, ImGui.GetColorU32(armedPick != ClickPick.None ? DesignerTheme.Amber : DesignerTheme.Muted), hint);
    }

    ClickPick hintPick;
    string hintText = "";

    /// <summary>Whether the right click on one of ours screens her (the orders' unit is a flotilla or a screen, decision 4
    /// of docs/battle/admirals-orders.md), else follows her.</summary>
    bool SmartScreens(Snapshot s)
    {
        var (unit, _) = Commanded(s);
        return RoleOf(s, unit).Role is UnitRole.Flotilla or UnitRole.Screen;
    }

    /// <summary>The strip's centre: the screen's, or what's left of the dock's when a card is open.</summary>
    float StripCentre(ImGuiViewportPtr vp) => (vp.Pos.X + Math.Min(dockLeft, vp.Pos.X + vp.Size.X)) / 2;

    void StripTexts(Snapshot s, int k, int kc, ShipClass? cls)
    {
        var v = s.Ships;
        if (selected != nameShip || world.Generation != nameGeneration)
        {
            (nameShip, nameGeneration) = (selected, world.Generation);
            nameText = world.NameOf(selected) ?? $"#{selected}";
            classText = cls?.Name ?? "?";
        }
        var (p, f) = v.PoseAt(kc, world.DrawMs);
        int course = (int)Math.Round(Earth.Heading(p, f)) % 360, kn = (int)Math.Round(v.Speed[kc] / Handling.MsPerKnot * 10);
        if ((kc, course, kn) != helmKey)
        {
            helmKey = (kc, course, kn);
            helmText = string.Format(CultureInfo.InvariantCulture, "{0:000}° · {1:0.0} kn", course, kn / 10.0);
        }
        courseText = CourseText(v.CourseOrderDeg[kc]);
        // the guns' target: the engaged one, else the main battery's
        int target = v.FireTarget[k];
        if (target == 0)
            for (int i = 0; i < s.Batteries.Count; i++)
                if (s.Batteries.Shooter[i] == selected && s.Batteries.Battery[i] == 0)
                    target = s.Batteries.Target[i];
        if ((selected, target, v.Fire[k]) != gunsKey)
        {
            gunsKey = (selected, target, v.Fire[k]);
            gunsText = (FireOrder)v.Fire[k] == FireOrder.Hold ? "holding fire" : target > 0 ? "on " + TargetLabel(s, target) : "no target";
        }
        if ((selected, v.TorpedoesLoaded[k], v.TorpedoesLeft[k]) != torpKey)
        {
            torpKey = (selected, v.TorpedoesLoaded[k], v.TorpedoesLeft[k]);
            torpText = cls == null || cls.Launchers.Length == 0 ? "none" : $"{v.TorpedoesLoaded[k]} loaded · {v.TorpedoesLeft[k]} left";
        }
    }

    // the course ordered, as text: kept per whole degree
    int courseKey = -1;
    string courseKeyText = "";

    string CourseText(double deg)
    {
        int d = ((int)Math.Round(deg) % 360 + 360) % 360;
        if (d != courseKey)
            (courseKey, courseKeyText) = (d, $"{d:000}°");
        return courseKeyText;
    }

    /// <summary>A column's head: its name, and its state beside it.</summary>
    static void Head(string title, string value, Vector4 colour)
    {
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.TextDisabled(title);
        ImGui.PopFont();
        ImGui.SameLine(0, 6 * UiFonts.Scale);
        ImGui.PushFont(UiFonts.Mono);
        ImGui.TextColored(colour, value);
        ImGui.PopFont();
    }

    /// <summary>A segment of a row of buttons: on (brass), armed (amber), or not. A menu's has a caret drawn at its end
    /// (the UI font has no ▾), in room its label leaves with trailing spaces.</summary>
    static bool Seg(string label, bool on = false, bool armed = false, bool enabled = true, bool first = false, bool menu = false)
    {
        if (!first)
            ImGui.SameLine(0, 1);
        ImGui.PushStyleColor(ImGuiCol.Button, armed ? ArmedBg : on ? DesignerTheme.BrassDarker : SegOff);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, armed ? ArmedBg : on ? DesignerTheme.BrassDark : SegHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, DesignerTheme.BrassDark);
        ImGui.PushStyleColor(ImGuiCol.Text, armed ? ArmedText : on ? DesignerTheme.Brass : DesignerTheme.Text);
        ImGui.BeginDisabled(!enabled);
        bool hit = ImGui.Button(label);
        ImGui.EndDisabled();
        if (menu)
        {
            float sc = UiFonts.Scale, h = 2.5f * sc;
            var max = ImGui.GetItemRectMax();
            var c = new Vector2(max.X - 10 * sc, (ImGui.GetItemRectMin().Y + max.Y) / 2 + 0.5f * sc);
            ImGui.GetWindowDrawList().AddTriangleFilled(c + new Vector2(-h, -h * 0.7f), c + new Vector2(h, -h * 0.7f), c + new Vector2(0, h * 0.8f),
                ImGui.GetColorU32(ImGuiCol.Text));
        }
        ImGui.PopStyleColor(4);
        return hit;
    }

    // ------------------------------------------------------------------ the columns

    /// <summary>Who: her name and class, who the orders go to (the ship, or each unit above her), and what's become of
    /// her: falling out, held, her orders changed, altered for land; the condition (until the card, U3).</summary>
    void Who(Snapshot s, int k, ShipClass? cls)
    {
        var v = s.Ships;
        var gv = s.Groups;
        ImGui.BeginGroup();
        ImGui.PushFont(UiFonts.Title);
        ImGui.TextColored(DesignerTheme.Bright, nameText);
        ImGui.PopFont();
        ImGui.SameLine(0, 6 * UiFonts.Scale);
        ImGui.TextDisabled(classText);
        // orders to: the ship, or a unit above her
        int level = Level(s);
        for (int i = 0; i <= chain.Count; i++)
        {
            string name = i == 0 ? "This ship" : gv.Name[chain[i - 1]];
            ImGui.PushID(i);
            if (Seg(name, level == i, first: i == 0))
                levelPick = i;
            ImGui.PopID();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(i == 0 ? "orders to her alone (alt does it for one order)" : level > i ? "" : "orders to the unit: its admiral hands them down");
        }
        // the units beside it the orders go to too, and what the unit is for
        var units = OrderUnits(s);
        var (role, set) = RoleOf(s, units[0]);
        if ((units.Count, role, set) != whoKey)
        {
            whoKey = (units.Count, role, set);
            string what = role == UnitRole.None ? "" : $"{RoleName(role)} · {(set ? "set by you" : "worked out")}";
            whoText = units.Count > 1 ? $"+ {units.Count - 1} more · orders go to each{(what.Length > 0 ? " · " : "")}{what}" : what;
        }
        if (whoText.Length > 0)
        {
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextColored(set ? DesignerTheme.Brass : DesignerTheme.Muted, whoText);
            ImGui.PopFont();
        }
        // what's become of her
        if (v.Reason[k] != 0)
        {
            ImGui.TextColored(DesignerTheme.Amber, ReasonText(v.Reason[k]));
            if (CanComeBack(s, k))
            {
                // the player overrules her captain (docs/map-ui/battle-hud.md decision 2)
                if (Seg("Back into line", first: true))
                    BackIntoLine(s, selected);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("order her back to her squadron: her captain stays, at his ship's risk");
            }
        }
        else if (gv.IndexOfShip(selected) is int g and >= 0 && DetachmentOf(gv, g, selected) is var d && d != Detachment.InLine)
            U8.Text(U8.W($"{HeldText(d)}{(v.Asked[k] ? "; her orders changed" : "")}"), DesignerTheme.Amber);
        else if (v.LandSide[k] != 0)
            U8.Text(v.LandSide[k] > 0 ? "following the coast, land to port"u8 : "following the coast, land to starboard"u8, MapPalette.DevSlide);
        else if (v.Tier[k] != ClockTier.Fine)
            ImGui.TextDisabled(v.Tier[k] == ClockTier.Rails ? "on rails" : "coarse tier");
        ImGui.EndGroup();
    }

    (int, UnitRole, bool) whoKey = (-1, default, false);
    string whoText = "";

    /// <summary>The helm: the course (±10°, ±90°, typed), hard over, the conn, the route cleared; the telegraph's bells.
    /// On the conn (the unit's own, or her line's) the head says so in its colour.</summary>
    void HelmColumn(Snapshot s, Unit unit, int kc)
    {
        var o = world.Orders;
        var v = s.Ships;
        bool own = OwnConn(s, unit), line = !own && unit.IsShip && v.Conn[kc] == 2;
        if (!ReferenceEquals(helmText, connSource) || (own, line) != connKey)
        {
            (connSource, connKey) = (helmText, (own, line));
            connText = own ? "CONN · " + helmText : line ? "LINE ON CONN · " + helmText : helmText;
        }
        ImGui.BeginGroup();
        Head("HELM", connText, own || line ? MapPalette.Conn : DesignerTheme.Bright);
        double ordered = v.CourseOrderDeg[kc];
        foreach (int d in CourseSteps)
        {
            if (d > 0 && d == CourseSteps[2])
            {
                ImGui.SameLine(0, 1);
                ImGui.SetNextItemWidth(46 * UiFonts.Scale);
                ImGui.PushFont(UiFonts.Mono);
                if (!ImGui.IsItemActive() && !courseTyping)
                    courseEdit = (int)Math.Round(ordered) % 360;
                ImGui.InputInt("##course", ref courseEdit, 0);
                courseTyping = ImGui.IsItemActive();
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    courseEdit = ((courseEdit % 360) + 360) % 360;
                    world.Send(o.Course(unit, courseEdit, hardOver ? 90 : 0));
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("the course ordered: type one and press Enter");
            }
            if (Seg(CourseLabels[Array.IndexOf(CourseSteps, d)], first: d == CourseSteps[0]))
                world.Send(o.Course(unit, ((ordered + d) % 360 + 360) % 360, hardOver ? 90 : 0));
        }
        ImGui.SameLine(0, 6 * UiFonts.Scale);
        if (Seg("Hard over##ho", hardOver, first: true))
            hardOver = !hardOver;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("the course orders put the rudder hard over");
        if (Seg("Conn##conn", own))
            world.Send(o.Conn(unit, !own));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(own ? "give the conn back (C): her captain judges again"
                : "take the conn (C): nothing but your helm orders moves her: no combing torpedo tracks (the lookouts call them), no turning away, no falling out; a task or a behaviour gives it back");
        if (v.RouteCount[kc] > 0)
        {
            ImGui.SameLine(0, 6 * UiFonts.Scale);
            if (Seg("Clear route", first: true))
                world.Send(o.ClearRoute(unit));
        }
        // the telegraph
        if (world.Classes.TryGet(v.Class[kc]) is { } guide)
        {
            double full = guide.Motion.MaxSpeedKn;
            double flank = Math.Max(full, guide.SpeedAt(guide.Motion.RatedShp * Math.Max(guide.Motion.OverloadMax, 1)));
            for (int i = 0; i < Bells.Length; i++)
            {
                double kn = Bells[i].Of * (Bells[i].Flank ? flank : full);
                if (Seg(Bells[i].Label, Math.Abs(v.SpeedOrderKn[kc] - kn) < 0.05, first: i == 0))
                    world.Send(o.Speed(unit, Math.Round(kn, 2)));
            }
        }
        ImGui.EndGroup();
    }

    bool courseTyping;

    /// <summary>The course buttons' labels, by <see cref="CourseSteps"/>.</summary>
    static readonly string[] CourseLabels = [.. CourseSteps.Select(d => d < 0 ? $"−{-d}°##c{d}" : $"+{d}°##c{d}")];

    /// <summary>The formation button's labels, by formation.</summary>
    static readonly string[] FormationLabels = [.. GroupFormationNames.Select(n => $"{n}    ##form")];

    /// <summary>The telegraph's bells: a share of full speed, or flank (her overload).</summary>
    static readonly (string Label, double Of, bool Flank)[] Bells =
        [("Astern", -1, false), ("Stop", 0, false), ("Slow", 0.25, false), ("Half", 0.5, false), ("Full", 1, false), ("Flank", 1, true)];

    /// <summary>The guns: fire at will, engage (armed: click an enemy), hold fire; under them each battery's state.</summary>
    void Guns(Snapshot s, Unit unit, int k, ShipClass? cls)
    {
        var o = world.Orders;
        var fire = (FireOrder)s.Ships.Fire[k];
        ImGui.BeginGroup();
        Head("GUNS", gunsText, fire == FireOrder.Hold ? DesignerTheme.Amber : DesignerTheme.Bright);
        bool any = cls != null && cls.Batteries.Length > 0;
        if (Seg("Fire at will", fire == FireOrder.AtWill, enabled: any, first: true))
            world.Send(o.FireAtWill(unit));
        if (Seg("Engage…", fire == FireOrder.Engage && armedPick != ClickPick.Engage, armedPick == ClickPick.Engage, any))
            Arm(ClickPick.Engage);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("then click an enemy (or right-click her without this)");
        if (Seg("Hold fire", fire == FireOrder.Hold, enabled: any))
            world.Send(o.HoldFire(unit));
        // each battery: a short line, the whole in the tooltip
        if (any)
        {
            int main = -1;
            for (int i = 0; i < s.Batteries.Count; i++)
                if (s.Batteries.Shooter[i] == selected && s.Batteries.Battery[i] == 0)
                    main = i;
            ImGui.PushFont(UiFonts.SansSmall);
            if (main < 0)
                ImGui.TextDisabled("main: no target");
            else
            {
                var b = s.Batteries;
                ImGui.TextDisabled(BatteryLine(b.State[main], b.Phase[main], b.Salvos[main], b.Hits[main]));
            }
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                var v = s.Batteries;
                for (int b = 0; b < cls!.Batteries.Length; b++)
                {
                    var row = cls.Batteries[b];
                    int i = 0;
                    while (i < v.Count && (v.Shooter[i] != selected || v.Battery[i] != b))
                        i++;
                    string kind = row.Kind == Battery.Main ? "main" : "secondary";
                    string label = BatteryLabel(cls, b);
                    U8.Text(i == v.Count ? U8.W($"{label} {kind}: no target")
                        : U8.W($"{label} {kind} on {LabelOf(s, v.Target[i])}: {StateText(v.State[i], v.Phase[i])}, {(v.Local[i] ? "local control" : FireControlPresets.Name(v.Level[i]))}; {v.Salvos[i]} salvos, {v.Hits[i]} hits of {v.Shells[i]}"));
                }
                ImGui.EndTooltip();
            }
        }
        ImGui.EndGroup();
    }

    // the main battery's line, kept while it says the same
    (BatteryState, FirePhase, int, int) batteryKey = ((BatteryState)255, default, -1, -1);
    string batteryText = "";

    string BatteryLine(BatteryState state, FirePhase phase, int salvos, int hits)
    {
        var key = (state, phase, salvos, hits);
        if (key != batteryKey)
            (batteryKey, batteryText) = (key, $"main: {StateText(state, phase)} · {salvos} salvos, {hits} hits");
        return batteryText;
    }

    /// <summary>Torpedoes (fire at: armed, click an enemy) and making smoke (the ship, or every ship of her squadron).</summary>
    void TorpedoesAndSmoke(Snapshot s, Unit unit, int k, ShipClass? cls)
    {
        ImGui.BeginGroup();
        Head("TORPEDOES", torpText, DesignerTheme.Bright);
        bool tubes = cls != null && cls.Launchers.Length > 0;
        if (Seg("Fire at…", armed: armedPick == ClickPick.Torpedoes, enabled: tubes && s.Ships.TorpedoesLoaded[k] > 0, first: true))
            Arm(ClickPick.Torpedoes);
        if (tubes && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var t = cls!.Torpedoes[cls.Launchers[0].Torpedo];
            ImGui.SetTooltip(string.Format(CultureInfo.InvariantCulture, "{0:0} mm: {1:0} kn to {2:0.0} km, or {3:0} kn to {4:0.0} km; then click an enemy",
                t.CalibreMm, t.FastKn, t.FastRangeM / 1000, t.SlowKn, t.SlowRangeM / 1000));
        }
        // smoke for her squadron (the lowest group she's in: a divided squadron's division), or her alone when the orders
        // go to the ship; never wider, whatever level the helm's orders go to; on while all of them make it
        smokers.Clear();
        var gv = s.Groups;
        if (unit.IsGroup && chain.Count > 0)
            for (int i = 0; i < gv.MemberCount[chain[0]]; i++)
                smokers.Add(gv.Members[gv.MemberStart[chain[0]] + i]);
        else
            smokers.Add(selected);
        bool funnels = false, making = true;
        foreach (int e in smokers)
            if (s.Ships.IndexOf(e) is int i and >= 0)
            {
                funnels |= world.Classes.TryGet(s.Ships.Class[i]) is { Funnels.Length: > 0 };
                making &= s.Ships.MakingSmoke[i];
            }
        if (Seg("Make smoke", making, enabled: world.Settings.Smoke && funnels, first: true))
            foreach (int e in smokers)
                world.Send(SmokeSystem.MakeSmoke(e, !making));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(unit.IsGroup && chain.Count > 0 ? $"every ship of {gv.Name[chain[0]]}: funnels burning rich, a screen" : "her funnels burn rich: a screen");
        ImGui.EndGroup();
    }

    readonly List<int> smokers = [];

    /// <summary>The task: the captain's (or the unit's): fight, follow, hold, and every other in the task menu (Task ▾,
    /// T); at a unit's level its manoeuvres (the formation, turn together, deploy), withdraw and its AI; the squadron's
    /// own business (detach, rejoin, forming, disbanding) in a popup.</summary>
    void Task(Snapshot s, Unit unit, int kc)
    {
        var v = s.Ships;
        var gv = s.Groups;
        var order = (CaptainOrder)v.Captain[kc];
        int level = Level(s);
        int g = level > 0 ? chain[level - 1] : -1;
        var o = world.Orders;
        ImGui.BeginGroup();
        Head(g < 0 ? "CAPTAIN" : gv.Force[g] ? "FORCE" : "SQUADRON", TaskHead(s, order, g), DesignerTheme.Amber);
        if (Seg("Fight", order == CaptainOrder.Fight, first: true))
            Behaviour(3, unit, 0);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("the nearest enemy in sight; turns away when outgunned (Fight… in the task menu: one you pick)");
        if (Seg("Follow…", order == CaptainOrder.Follow && armedPick != ClickPick.Follow, armedPick == ClickPick.Follow))
            Arm(ClickPick.Follow);
        if (Seg("Hold", order is CaptainOrder.Course or CaptainOrder.None))
            Behaviour(0, unit, 0);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("hold the course and speed ordered");
        if (Seg("Task    ##task", taskOpen, menu: true))
            taskOpen = !taskOpen;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("every order, by what it's about, and what the unit is for (T)");
        // the unit: its whole line's shape (or this level's formation), its manoeuvres, withdraw, its AI
        if (g >= 0)
        {
            int shape = ShapeOf(gv, g);
            if (Seg("Line ahead##shape", shape == (int)LineShape.OneLine, first: true))
                SendShape(s, LineShape.OneLine);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(gv.Force[g] ? "one line ahead, every squadron in it, on the course it steers (L); on the same course the far columns drop back a column's length each, so a fleet takes long: deploying turns it into line quicker"
                    : "line ahead (L)");
            if (gv.Force[g])
            {
                if (Seg("Columns##shape", shape == (int)LineShape.Columns))
                    SendShape(s, LineShape.Columns);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("the cruising order (K): every squadron a column in line ahead, the columns abreast as far apart as one is long, ready to deploy");
            }
            if (Seg("Abreast##shape", shape == (int)LineShape.Abreast))
                SendShape(s, LineShape.Abreast);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(gv.Force[g] ? "every ship abreast (A), each squadron at its own spacing" : "line abreast (A)");
            if (Seg(FormationLabels[(int)gv.Formation[g]], menu: true))
                ImGui.OpenPopup("##formation");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(gv.Force[g] ? "this level's formation alone, its spacing; echelons and lines of bearing" : "its formation and spacing; echelons and lines of bearing");
            if (ImGui.BeginPopup("##formation"))
            {
                ImGui.TextDisabled(gv.Name[g]);
                FormationEdit(gv, g, ref (g == chain[0] ? ref spacingEdit : ref forceSpacingEdit), "##fm");
                ImGui.EndPopup();
            }
            if (Seg("Turn    ##turn", first: true, menu: true))
                ImGui.OpenPopup("##turn");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("turn together: every ship at once; the line becomes a line of bearing on its old axis");
            if (ImGui.BeginPopup("##turn"))
            {
                TurnMenu(unit);
                ImGui.EndPopup();
            }
            if (Seg("Deploy…##deploy", armed: armedPick == ClickPick.Deploy))
                Arm(ClickPick.Deploy);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(gv.Force[g] ? "into one line of battle: click where the line should steer, or the enemy (across his course)"
                    : "into line ahead: click where it should steer, or the enemy (across his course)");
            if (Seg("to port##deploy"))
                DeployAbeam(s, -1);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(gv.Force[g] ? "deploy 8 points to port (,): one line of battle, the port wing column leading round, the rest in its wake"
                    : "into line ahead 8 points to port (,)");
            if (Seg("to stbd##deploy"))
                DeployAbeam(s, 1);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(gv.Force[g] ? "deploy 8 points to starboard (.): one line of battle, the starboard wing column leading round, the rest in its wake"
                    : "into line ahead 8 points to starboard (.)");
            if (Seg("Withdraw"))
                world.Send(o.WithdrawHome(unit));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("break off and go home, away from any enemy first");
            if (gv.Parent[g] == 0)
            {
                bool his = gv.Commanded[g] == 1;
                if (Seg("AI##auto", his))
                    world.Send(o.Automate(unit, !his));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(his ? "the side's commander fights it: any order of yours takes it; click to take it" : "click to leave it to the side's commander");
            }
        }
        // a ship in no squadron is a top unit too: hers to hand to the side's commander, or take
        bool alone = g < 0 && s.Groups.IndexOfShip(selected) < 0 && Tagged(s, s.Ships.Commanded[kc]);
        if (alone)
        {
            bool his = s.Ships.Commanded[kc] == 1;
            if (Seg("AI##autoship", his, first: true))
                world.Send(o.Automate(unit, !his));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(his ? "the side's commander fights her: any order of yours takes her; click to take her" : "click to leave her to the side's commander");
        }
        if (Seg("Squadron…##sq", first: g < 0 && !alone))
            ImGui.OpenPopup("##squadron");
        if (ImGui.BeginPopup("##squadron"))
        {
            GroupRow(s);
            ImGui.EndPopup();
        }
        ImGui.EndGroup();
    }

    /// <summary>Each unit the orders go to (a group) into a shape, its whole line (docs/map-ui/battle-hud.md P1).</summary>
    void SendShape(Snapshot s, LineShape shape)
    {
        foreach (var u in OrderUnits(s))
            if (u.IsGroup)
                world.Send(world.Orders.Shape(u, shape));
    }

    /// <summary>Each unit the orders go to (a group) deployed 8 points to port (-1) or starboard (+1) of its guide's
    /// heading: its wing column on that side leads round, as Jellicoe's port wing did.</summary>
    void DeployAbeam(Snapshot s, int side)
    {
        foreach (var u in OrderUnits(s))
            if (u.IsGroup && s.Ships.IndexOf(GuideOf(s, u)) is int k and >= 0)
            {
                var (p, f) = s.Ships.PoseAt(k, world.DrawMs);
                world.Send(world.Orders.Deploy(u, ((Earth.Heading(p, f) + 90 * side) % 360 + 360) % 360));
            }
    }

    /// <summary>The unit's keys, with one of ours selected at a unit's level: L line ahead, K columns, A abreast, comma
    /// and period deploy to port and starboard. Whether the key was one.</summary>
    bool UnitKey(uint key)
    {
        if (key is not (SDLK_L or SDLK_K or SDLK_A or SDLK_COMMA or SDLK_PERIOD))
            return false;
        var s = world.Current;
        if (s.Ships.IndexOf(selected) < 0 || Level(s) is not (int level and > 0))
            return false;
        if (key == SDLK_K && !s.Groups.Force[chain[level - 1]])
            return false;
        if (key is SDLK_COMMA or SDLK_PERIOD)
            DeployAbeam(s, key == SDLK_COMMA ? -1 : 1);
        else
            SendShape(s, key == SDLK_L ? LineShape.OneLine : key == SDLK_K ? LineShape.Columns : LineShape.Abreast);
        return true;
    }

    /// <summary>The Turn ▾ popup: together by 4 or 8 points either way, or about with the rear ship first (the battle
    /// turn-away), each to the unit's admiral.</summary>
    void TurnMenu(Unit unit)
    {
        var o = world.Orders;
        ImGui.TextDisabled("turn together");
        foreach (var (label, deg) in TurnButtons)
            if (ImGui.Selectable(label))
                world.Send(o.TurnTogether(unit, deg));
        ImGui.Separator();
        ImGui.TextDisabled("the battle turn: about, the rear ship first");
        if (ImGui.Selectable("about, to port##bt"))
            world.Send(o.BattleTurn(unit, toStarboard: false));
        if (ImGui.Selectable("about, to starboard##bt"))
            world.Send(o.BattleTurn(unit, toStarboard: true));
    }

    // the task's head, kept while it says the same
    (CaptainOrder, int, byte, byte, byte, int) taskKey = ((CaptainOrder)255, -2, 0, 0, 0, -1);
    string taskText = "";

    string TaskHead(Snapshot s, CaptainOrder order, int g)
    {
        var gv = s.Groups;
        var key = (order, g, g >= 0 ? gv.Task[g] : (byte)0, g >= 0 ? gv.TaskStatus[g] : (byte)0, g >= 0 ? gv.Commanded[g] : (byte)0, world.Generation);
        if (key == taskKey)
            return taskText;
        taskKey = key;
        string what = order switch
        {
            CaptainOrder.Fight => "fight",
            CaptainOrder.Follow => "follow",
            CaptainOrder.Run => "run",
            CaptainOrder.Station => "on station",
            CaptainOrder.Route => "route",
            CaptainOrder.Shadow => "shadow",
            CaptainOrder.Home => "home",
            CaptainOrder.Screen => "screen",
            _ => "hold",
        };
        if (g >= 0 && gv.Task[g] != 0)
            what = TaskText(gv.Task[g], gv.TaskStatus[g]);
        if (g >= 0 && gv.Commanded[g] != 0)
            what += gv.Commanded[g] == 1 ? " · AI" : " · yours";
        return taskText = what;
    }

    void Arm(ClickPick pick)
    {
        armedPick = armedPick == pick ? ClickPick.None : pick;
        fireArmed = false;
    }

    // ------------------------------------------------------------------ clicks and the cursor's verb

    /// <summary>What's under the cursor for an order: an enemy (in sight, a contact, or with truth one of another side's
    /// ships), one of hers, or the sea (with where on it).</summary>
    (int Enemy, int Own, bool Sea, Vec3d At) Under(Snapshot s, double x, double y)
    {
        var view = View;
        int enemy = PickShip(view, x, y, s.Sighted);
        if (enemy < 0)
            enemy = PickContact(view, s, x, y);
        int own = -1;
        if (enemy < 0 && PickShip(view, x, y) is int hit and >= 0)
        {
            int kh = s.Ships.IndexOf(hit), ks = s.Ships.IndexOf(selected);
            if (ks >= 0 && kh >= 0 && s.Ships.Side[kh] != s.Ships.Side[ks])
                enemy = hit;
            else
                own = hit;
        }
        bool sea = view.Pick(x, y, out var at);
        return (enemy, own, sea, at);
    }

    /// <summary>A click with a pick armed: done with what's under it (true), else false and the click selects. A miss on
    /// what the pick wants keeps it armed.</summary>
    bool ArmedClick(double x, double y)
    {
        if (armedPick == ClickPick.None || selected < 0)
            return false;
        var s = world.Current;
        var (enemy, own, sea, at) = Under(s, x, y);
        var (unit, _) = Commanded(s);
        int k = s.Ships.IndexOf(selected);
        bool done = true;
        switch (armedPick)
        {
            case ClickPick.Engage or ClickPick.Torpedoes:
                if (enemy <= 0)
                    return true;
                world.Send(armedPick == ClickPick.Engage ? world.Orders.Engage(unit, enemy)
                    : TorpedoSystem.Launch(selected, enemy, Math.Max(k >= 0 ? s.Ships.TorpedoesLoaded[k] : 1, 1)));
                break;
            case ClickPick.Follow or ClickPick.Run:
                int target = enemy > 0 ? enemy : own;
                if (target <= 0 || target == selected)
                    return true;
                if (armedPick == ClickPick.Run)
                    foreach (var u in OrderUnits(s))
                        Behaviour(2, u, target);
                else
                    foreach (var u in OrderUnits(s))
                        if (!ShipIn(s, target, u))
                            Behaviour(1, u, target);
                break;
            default:
                done = GiveTask(s, armedPick, own, enemy, sea, at);
                break;
        }
        if (done)
            armedPick = ClickPick.None;
        return true;
    }

    /// <summary>The right-click's order: an enemy engaged, one of hers screened (the unit a flotilla or a screen) or
    /// followed (station astern), the sea steered for (with shift, a waypoint added). True when it gave one. An order
    /// on one of hers goes to each unit selected.</summary>
    bool SmartOrder(double x, double y, bool waypoint)
    {
        var s = world.Current;
        if (s.Ships.IndexOf(selected) < 0)
            return false;
        var (enemy, own, _, _) = Under(s, x, y);
        var (unit, ship) = Commanded(s);
        if (enemy > 0)
        {
            foreach (var u in OrderUnits(s))
                world.Send(world.Orders.Engage(u, enemy));
            return true;
        }
        if (own > 0 && own != selected && own != ship && !waypoint)
        {
            if (SmartScreens(s))
                GiveTask(s, ClickPick.Screen, own, 0, false, default);
            else
                foreach (var u in OrderUnits(s))
                    if (!ShipIn(s, own, u))
                        Behaviour(1, u, own);
            return true;
        }
        return false;   // the sea: OrderAt steers
    }

    /// <summary>A left click on a ship twice within a moment: her squadron takes the orders.</summary>
    void DoubleClick(int ship)
    {
        long now = Environment.TickCount64;
        if (ship >= 0 && ship == lastClickShip && now - lastClickMs < 400)
        {
            Chain(world.Current);
            if (chain.Count > 0)
                (levelFor, levelPick) = (selected, 1);
        }
        (lastClickShip, lastClickMs) = (ship, now);
    }

    /// <summary>Tab: the player's next ship (shift: the one before), by entity.</summary>
    void NextShip(bool back)
    {
        var s = world.Current;
        var v = s.Ships;
        int side = s.PlayerSide, best = -1, wrap = -1;
        for (int k = 0; k < v.Count; k++)
        {
            if (side >= 0 && v.Side[k] != side || v.Loss[k] != LossKind.None)
                continue;
            int e = v.Entity[k];
            if (back ? e < selected && (best < 0 || e > best) : e > selected && (best < 0 || e < best))
                best = e;
            if (back ? wrap < 0 || e > wrap : wrap < 0 || e < wrap)
                wrap = e;
        }
        int next = best >= 0 ? best : wrap;
        if (next >= 0)
        {
            selected = next;
            ClearPicks();
            armedPick = ClickPick.None;
        }
    }

    // the verb's text, kept while it says the same; whether it's an armed pick's miss
    (int Kind, int Target, int Course, int Tenths, int Who) verbKey = (-1, 0, 0, 0, 0);
    string verbText = "";
    bool verbMiss;

    /// <summary>A target's distance from a point, tenths of a kilometre (0 if she's gone).</summary>
    int Dist(Snapshot s, Vec3d from, int ship)
    {
        int k = s.Ships.IndexOf(ship);
        if (k < 0 && s.Sighted.IndexOf(ship) is int j and >= 0)
            return (int)Math.Round(Earth.Distance(from, s.Sighted.PoseAt(j, world.DrawMs).Position) / 100);
        return k < 0 ? 0 : (int)Math.Round(Earth.Distance(from, s.Ships.PoseAt(k, world.DrawMs).Position) / 100);
    }

    /// <summary>An armed pick's colour at the cursor: an order about the enemy red, one of ours friend blue, the sea's and
    /// a manoeuvre's brass.</summary>
    static Vector4 PickColour(ClickPick pick) => pick switch
    {
        ClickPick.Engage or ClickPick.Torpedoes or ClickPick.Fight or ClickPick.Intercept or ClickPick.Shadow or ClickPick.Attack or ClickPick.Run => Bad,
        ClickPick.Follow or ClickPick.Screen or ClickPick.Scout or ClickPick.Cover or ClickPick.Join or ClickPick.Station or ClickPick.DivideFrom => Friend,
        _ => DesignerTheme.Brass,
    };

    /// <summary>The cursor's verb, beside it on the map: what a right-click (or the armed pick's click) would do.</summary>
    void CursorVerb(Snapshot s)
    {
        if (!mouseIn || selected < 0 || ImGui.GetIO().WantCaptureMouse || dragging || placing)
            return;
        int k = s.Ships.IndexOf(selected);
        if (k < 0)
            return;
        var vp = ImGui.GetMainViewport();
        double x = mouse.X * width / Math.Max(vp.Size.X, 1), y = mouse.Y * height / Math.Max(vp.Size.Y, 1);
        var (enemy, own, sea, at) = Under(s, x, y);
        var (unit, ship) = Commanded(s);
        int ks = s.Ships.IndexOf(ship);
        var (p, _) = s.Ships.PoseAt(ks >= 0 ? ks : k, world.DrawMs);
        int course = sea ? (int)Math.Round((Earth.Bearing(p, at) % 360 + 360) % 360) % 360 : 0;
        int tenths = sea ? (int)Math.Round(Earth.Distance(p, at) / 100) : 0;
        bool shift = ImGui.GetIO().KeyShift;
        int many = OrderUnits(s).Count;
        (int Kind, int Target, int Course, int Tenths, int Who) key = armedPick != ClickPick.None
            ? (10 + (int)armedPick, enemy > 0 ? enemy : own > 0 ? own : 0, enemy > 0 || own > 0 ? 0 : course, enemy > 0 || own > 0 ? Dist(s, p, enemy > 0 ? enemy : own) : tenths, unit.Id * 64 + many)
            : enemy > 0 ? (1, enemy, 0, tenths, unit.Id * 64 + many)
            : own > 0 && own != selected && own != ship && !shift ? (SmartScreens(s) ? 6 : 2, own, 0, 0, unit.Id * 64 + many)
            : sea && world.Shore.IsLand(at) ? (3, 0, 0, 0, unit.Id)
            : sea ? (shift ? 4 : 5, 0, course, tenths, unit.Id) : (0, 0, 0, 0, 0);
        if (key.Kind == 0 || own == selected && armedPick == ClickPick.None)
            return;
        if (key != verbKey)
        {
            verbKey = key;
            string who = unit.IsGroup && s.Groups.IndexOfGroup(unit.GroupId) is int g and >= 0 ? s.Groups.Name[g] : nameText;
            if (many > 1)
                who = $"{who} + {many - 1}";
            string? words = key.Kind >= 10 ? TaskVerb(s, armedPick, own, enemy, sea, at, key.Tenths, key.Course) : null;
            verbMiss = key.Kind >= 10 && words == null;
            verbText = key.Kind switch
            {
                >= 10 => words != null ? $"{who}: {words}" : $"{PickVerb(armedPick)}: {PickClick(armedPick)}",
                1 => string.Format(CultureInfo.InvariantCulture, "{0}: ENGAGE {1} · {2:0.0} km", who, TargetLabel(s, key.Target), key.Tenths / 10.0),
                2 => $"{who}: FOLLOW {TargetLabel(s, key.Target)}",
                6 => TaskVerb(s, ClickPick.Screen, own, 0, false, default, 0, 0) is { } screen ? $"{who}: {screen}" : $"{who}: FOLLOW {TargetLabel(s, key.Target)}",
                3 => "land",
                4 => string.Format(CultureInfo.InvariantCulture, "{0}: WAYPOINT {1:000}° · {2:0.0} km", who, key.Course, key.Tenths / 10.0),
                _ => string.Format(CultureInfo.InvariantCulture, "{0}: STEER {1:000}° · {2:0.0} km", who, key.Course, key.Tenths / 10.0),
            };
        }
        bool miss = verbMiss;
        var dl = ImGui.GetForegroundDrawList();
        var size = ImGui.CalcTextSize(verbText);
        var pos = vp.Pos + mouse + new Vector2(16, -22) * UiFonts.Scale;
        dl.AddRectFilled(pos - new Vector2(5, 2) * UiFonts.Scale, pos + size + new Vector2(5, 2) * UiFonts.Scale, 0xD8110E0A, 2 * UiFonts.Scale);
        var colour = key.Kind switch { >= 10 => miss ? DesignerTheme.Amber : PickColour(armedPick), 1 => Bad, 2 or 6 => Friend, 3 => DesignerTheme.Muted, _ => DesignerTheme.Brass };
        dl.AddText(pos, ImGui.GetColorU32(colour), verbText);
    }
}
