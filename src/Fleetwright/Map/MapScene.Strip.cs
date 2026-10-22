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
    /// <summary>What the next click on a ship does, armed from the strip (the dev's salvo is <see cref="fireArmed"/>).</summary>
    enum ClickPick : byte { None, Engage, Torpedoes, Follow, Run }

    ClickPick armedPick;
    long lastClickMs;
    int lastClickShip = -1;

    static readonly Vector4 SegOff = new(1, 1, 1, 0.05f), SegHover = new(1, 1, 1, 0.1f), ArmedBg = new(0.29f, 0.23f, 0.08f, 1),
        ArmedText = new(1f, 0.85f, 0.54f, 1);

    // the strip's strings, made when what they say changes
    (int Ship, int Course, int Kn) helmKey = (-1, -1, -1);
    string helmText = "", courseText = "";
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
            TorpedoesAndSmoke(s, k, cls);
            seps[n++] = ImGui.GetItemRectMax().X + gap / 2;
            ImGui.SameLine(0, gap);
            Task(s, unit, kc);

            var dl = ImGui.GetWindowDrawList();
            var wp = ImGui.GetWindowPos();
            float top = wp.Y + 6 * sc, bottom = wp.Y + ImGui.GetWindowHeight() - 6 * sc;
            for (int i = 0; i < n; i++)
                dl.AddLine(new Vector2(seps[i], top), new Vector2(seps[i], bottom), ImGui.GetColorU32(DesignerTheme.Line), 1);
        }
        ImGui.End();
        ImGui.PopStyleVar(3);
        StripHint(vp);
    }

    /// <summary>The line under the strip: what the mouse does, or what the armed pick waits for.</summary>
    void StripHint(ImGuiViewportPtr vp)
    {
        string hint = armedPick switch
        {
            ClickPick.Engage => "ENGAGE: click an enemy · Esc cancels",
            ClickPick.Torpedoes => "TORPEDOES: click an enemy · Esc cancels",
            ClickPick.Follow => "FOLLOW: click a ship to take station astern of · Esc cancels",
            ClickPick.Run => "RUN FROM: click a ship · Esc cancels",
            _ => "Right-click: steer · on an enemy: engage · on your ship: follow · Shift: waypoint · Alt: this ship only · Tab: the next ship",
        };
        var size = ImGui.CalcTextSize(hint);
        var at = new Vector2(StripCentre(vp) - size.X / 2, vp.Pos.Y + vp.Size.Y - 6 * UiFonts.Scale - size.Y);
        var dl = ImGui.GetBackgroundDrawList();
        dl.AddText(at + Vector2.One, 0xC0000000, hint);
        dl.AddText(at, ImGui.GetColorU32(armedPick != ClickPick.None ? DesignerTheme.Amber : DesignerTheme.Muted), hint);
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

    /// <summary>A segment of a row of buttons: on (brass), armed (amber), or not.</summary>
    static bool Seg(string label, bool on = false, bool armed = false, bool enabled = true, bool first = false)
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
        // what's become of her
        if (v.Reason[k] != 0)
            ImGui.TextColored(DesignerTheme.Amber, ReasonText(v.Reason[k]));
        else if (gv.IndexOfShip(selected) is int g and >= 0 && DetachmentOf(gv, g, selected) is var d && d != Detachment.InLine)
            U8.Text(U8.W($"{HeldText(d)}{(v.Asked[k] ? "; her orders changed" : "")}"), DesignerTheme.Amber);
        else if (v.LandSide[k] != 0)
            U8.Text(v.LandSide[k] > 0 ? "following the coast, land to port"u8 : "following the coast, land to starboard"u8, SlideColor);
        else if (v.Tier[k] != ClockTier.Fine)
            ImGui.TextDisabled(v.Tier[k] == ClockTier.Rails ? "on rails" : "coarse tier");
        ImGui.EndGroup();
    }

    /// <summary>The helm: the course (±10°, ±90°, typed), hard over, the route cleared; the telegraph's bells.</summary>
    void HelmColumn(Snapshot s, Unit unit, int kc)
    {
        var o = world.Ai.Orders;
        var v = s.Ships;
        ImGui.BeginGroup();
        Head("HELM", helmText, DesignerTheme.Bright);
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
    static readonly string[] FormationLabels = [.. GroupFormationNames.Select(n => $"{n}…##form")];

    /// <summary>The telegraph's bells: a share of full speed, or flank (her overload).</summary>
    static readonly (string Label, double Of, bool Flank)[] Bells =
        [("Astern", -1, false), ("Stop", 0, false), ("Slow", 0.25, false), ("Half", 0.5, false), ("Full", 1, false), ("Flank", 1, true)];

    /// <summary>The guns: fire at will, engage (armed: click an enemy), hold fire; under them each battery's state.</summary>
    void Guns(Snapshot s, Unit unit, int k, ShipClass? cls)
    {
        var o = world.Ai.Orders;
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

    /// <summary>Torpedoes (fire at: armed, click an enemy) and making smoke.</summary>
    void TorpedoesAndSmoke(Snapshot s, int k, ShipClass? cls)
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
        bool smoke = world.Settings.Smoke && cls != null && cls.Funnels.Length > 0;
        bool making = s.Ships.MakingSmoke[k];
        if (Seg("Make smoke", making, enabled: smoke, first: true))
            world.Send(SmokeSystem.MakeSmoke(selected, !making));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("her funnels burn rich: a screen");
        ImGui.EndGroup();
    }

    /// <summary>The task: the captain's (or the unit's): fight, follow, run from, hold, search; at a unit's level the
    /// formation, withdraw and its AI; the squadron's own business (detach, rejoin, forming, disbanding) in a popup.</summary>
    void Task(Snapshot s, Unit unit, int kc)
    {
        var v = s.Ships;
        var gv = s.Groups;
        var order = (CaptainOrder)v.Captain[kc];
        int level = Level(s);
        int g = level > 0 ? chain[level - 1] : -1;
        ImGui.BeginGroup();
        Head(g >= 0 ? "SQUADRON" : "CAPTAIN", TaskHead(s, order, g), DesignerTheme.Amber);
        if (Seg("Fight", order == CaptainOrder.Fight, first: true))
            Behaviour(3, unit, 0);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("the nearest enemy in sight; turns away when outgunned");
        if (Seg("Follow…", order == CaptainOrder.Follow && armedPick != ClickPick.Follow, armedPick == ClickPick.Follow))
            Arm(ClickPick.Follow);
        if (Seg("Run from…", order == CaptainOrder.Run && armedPick != ClickPick.Run, armedPick == ClickPick.Run))
            Arm(ClickPick.Run);
        if (Seg("Hold", order is CaptainOrder.Course or CaptainOrder.None))
            Behaviour(0, unit, 0);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("hold the course and speed ordered");
        if (Seg("Search…"))
            ImGui.OpenPopup("##search");
        if (ImGui.BeginPopup("##search"))
        {
            SearchRow(s, unit, kc);
            ImGui.EndPopup();
        }
        // the unit: its formation, withdraw, its AI; the squadron's business
        if (g >= 0)
        {
            if (Seg(FormationLabels[(int)gv.Formation[g]], first: true))
                ImGui.OpenPopup("##formation");
            if (ImGui.BeginPopup("##formation"))
            {
                ImGui.TextDisabled(gv.Name[g]);
                FormationEdit(gv, g, ref (g == chain[0] ? ref spacingEdit : ref forceSpacingEdit), "##fm");
                ImGui.EndPopup();
            }
            if (Seg("Withdraw"))
                world.Send(world.Ai.Orders.WithdrawHome(unit));
            if (gv.Parent[g] == 0)
            {
                bool his = gv.Commanded[g] == 1;
                if (Seg("AI##auto", his))
                    world.Send(world.Ai.Orders.Automate(unit, !his));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(his ? "the side's commander fights it: any order of yours takes it; click to take it" : "click to leave it to the side's commander");
            }
        }
        if (Seg("Squadron…##sq", first: g < 0))
            ImGui.OpenPopup("##squadron");
        if (ImGui.BeginPopup("##squadron"))
        {
            GroupRow(s);
            ImGui.EndPopup();
        }
        ImGui.EndGroup();
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

    /// <summary>A click with a pick armed: done with the ship under it (true), else false and the click selects.</summary>
    bool ArmedClick(double x, double y)
    {
        if (armedPick == ClickPick.None || selected < 0)
            return false;
        var s = world.Current;
        var (enemy, own, _, _) = Under(s, x, y);
        int target = armedPick is ClickPick.Engage or ClickPick.Torpedoes ? enemy : enemy > 0 ? enemy : own;
        if (target <= 0 || target == selected)
            return true;   // a miss keeps it armed
        var (unit, _) = Commanded(s);
        int k = s.Ships.IndexOf(selected);
        switch (armedPick)
        {
            case ClickPick.Engage:
                world.Send(world.Ai.Orders.Engage(unit, target));
                break;
            case ClickPick.Torpedoes:
                if (k >= 0)
                    world.Send(TorpedoSystem.Launch(selected, target, Math.Max(s.Ships.TorpedoesLoaded[k], 1)));
                break;
            case ClickPick.Follow:
                Behaviour(1, unit, target);
                break;
            case ClickPick.Run:
                Behaviour(2, unit, target);
                break;
        }
        armedPick = ClickPick.None;
        return true;
    }

    /// <summary>The right-click's order: an enemy engaged, one of hers followed (station astern), the sea steered for
    /// (with shift, a waypoint added). True when it gave one.</summary>
    bool SmartOrder(double x, double y, bool waypoint)
    {
        var s = world.Current;
        if (s.Ships.IndexOf(selected) < 0)
            return false;
        var (enemy, own, _, _) = Under(s, x, y);
        var (unit, ship) = Commanded(s);
        if (enemy > 0)
        {
            world.Send(world.Ai.Orders.Engage(unit, enemy));
            return true;
        }
        if (own > 0 && own != selected && own != ship && !waypoint)
        {
            Behaviour(1, unit, own);
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
            picked.Clear();
            armedPick = ClickPick.None;
        }
    }

    // the verb's text, kept while it says the same
    (int Kind, int Target, int Course, int Tenths, int Who) verbKey = (-1, 0, 0, 0, 0);
    string verbText = "";

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
        (int Kind, int Target, int Course, int Tenths, int Who) key = armedPick != ClickPick.None
            ? (10 + (int)armedPick, enemy > 0 ? enemy : own, 0, 0, unit.Id)
            : enemy > 0 ? (1, enemy, 0, tenths, unit.Id)
            : own > 0 && own != selected && own != ship && !shift ? (2, own, 0, 0, unit.Id)
            : sea && world.Shore.IsLand(at) ? (3, 0, 0, 0, unit.Id)
            : sea ? (shift ? 4 : 5, 0, course, tenths, unit.Id) : (0, 0, 0, 0, 0);
        if (key.Kind == 0 || own == selected && armedPick == ClickPick.None)
            return;
        if (key != verbKey)
        {
            verbKey = key;
            string who = unit.IsGroup && s.Groups.IndexOfGroup(unit.GroupId) is int g and >= 0 ? s.Groups.Name[g] : nameText;
            verbText = key.Kind switch
            {
                >= 10 => (armedPick switch { ClickPick.Engage => "ENGAGE", ClickPick.Torpedoes => "TORPEDOES AT", ClickPick.Follow => "FOLLOW", _ => "RUN FROM" }) +
                         (key.Target > 0 ? " " + TargetLabel(s, key.Target) : ": pick a ship"),
                1 => string.Format(CultureInfo.InvariantCulture, "{0}: ENGAGE {1} · {2:0.0} km", who, TargetLabel(s, key.Target), key.Tenths / 10.0),
                2 => $"{who}: FOLLOW {TargetLabel(s, key.Target)}",
                3 => "land",
                4 => string.Format(CultureInfo.InvariantCulture, "{0}: WAYPOINT {1:000}° · {2:0.0} km", who, key.Course, key.Tenths / 10.0),
                _ => string.Format(CultureInfo.InvariantCulture, "{0}: STEER {1:000}° · {2:0.0} km", who, key.Course, key.Tenths / 10.0),
            };
        }
        var dl = ImGui.GetForegroundDrawList();
        var size = ImGui.CalcTextSize(verbText);
        var pos = vp.Pos + mouse + new Vector2(16, -22) * UiFonts.Scale;
        dl.AddRectFilled(pos - new Vector2(5, 2) * UiFonts.Scale, pos + size + new Vector2(5, 2) * UiFonts.Scale, 0xD8110E0A, 2 * UiFonts.Scale);
        var colour = key.Kind switch { >= 10 => DesignerTheme.Amber, 1 => Bad, 2 => Friend, 3 => DesignerTheme.Muted, _ => DesignerTheme.Brass };
        dl.AddText(pos, ImGui.GetColorU32(colour), verbText);
    }
}
