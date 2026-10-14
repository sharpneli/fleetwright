using System.Globalization;
using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Shells and splashes on the map (docs/battle/fighting.md 4.2a), in the map's style (decision 10: no effects
/// yet). Each shell is looked up in its ballistic table at the frame's time, so it flies smoothly at any rate with
/// nothing but its record in the snapshot: a dot, and a short tracer behind it once it's a few pixels long. A splash is
/// a ring that widens and fades; a hit, an orange flash; the selected ship's bright, the others' dim. Tracers, rings and
/// flashes scale with the shell's calibre (<see cref="CalibreScale"/>), so a secondary's fire reads apart from the main's. Fire control (4.2b):
/// a line from each engaged battery's ship to its target. The strip's fire row (4.2c): the commanded unit's fire order
/// (at will, hold fire, engage: a right-click on a ship or a contact), each of the selected ship's batteries with its
/// target and state, and the dev's salvo "at" a point of the sea. Torpedoes (4.6): each running one's wake, a pale line
/// behind it; a hit, a tall flash; the strip's torpedo row (loaded and left, "torpedoes at" a ship).</summary>
public sealed partial class MapScene
{
    static readonly Vector4 ShellColour = new(1f, 0.95f, 0.6f, 0.95f), SplashColour = new(0.9f, 0.95f, 1f, 0.9f),
        HitColour = new(1f, 0.55f, 0.15f, 1f), TargetColour = new(1f, 0.35f, 0.25f, 0.55f), WakeColour = new(0.85f, 0.95f, 1f, 0.75f);
    const double WakeM = 400, WakeMinPx = 14, TorpedoHitM = 30;   // a torpedo's wake behind it; a hit's column
    const double TracerS = 0.25;      // the tracer's length, seconds of flight
    const double SplashWideM = 30, SplashFadeMs = 8000, HitFadeMs = 4000;   // a 12" shell's; others by CalibreScale
    const double RefCalibreMm = 305;
    bool fireArmed, engageArmed, torpedoArmed;
    int fireBattery;   // the dev's salvo "at": which battery
    readonly Dictionary<int, int> shipIndex = [];   // a frame's ships by entity, for the target lines

    void DrawShells(in MapView view, Snapshot s)
    {
        var v = s.Shells;
        if (v.Ballistics is not { } tables)
            return;
        long now = world.DrawMs;
        double mpp = view.MetresPerPixel;
        for (int i = 0; i < v.Count; i++)
        {
            double t = (now - v.FiredMs[i]) / 1000.0;
            if (t < 0 || t > v.EndS[i] || v.Table[i] >= tables.Count)
                continue;
            var table = tables[v.Table[i]];
            var p = table.Position(v.Muzzle[i], v.Forward[i], v.ElevationDeg[i], v.Speed[i], t, out _);
            if (!view.Project(p, out double x, out double y) || x < -10 || y < -10 || x > view.Width + 10 || y > view.Height + 10)
                continue;
            double f = CalibreScale(table.Key.CalibreMm);
            var back = table.Position(v.Muzzle[i], v.Forward[i], v.ElevationDeg[i], v.Speed[i], Math.Max(0, t - TracerS * f), out _);
            bool own = selected < 0 || v.Shooter[i] == selected;
            overlay.BeginStrip(own ? ShellColour : ShellColour with { W = 0.35f }, (float)((own ? 2 : 1.5) * f));
            if (view.Project(back, out double bx, out double by) && Math.Abs(bx - x) + Math.Abs(by - y) > 3)
                overlay.Point(bx, by);
            else
                overlay.Point(x - 1, y);
            overlay.Point(x, y);
            overlay.End();
        }

        DrawTargets(view, s);
        DrawTorpedoes(view, s);

        var w = s.Splashes;
        for (int i = 0; i < w.Count; i++)
        {
            double age = now - w.Ms[i];
            bool hit = w.Ship[i] >= 0;
            double f = CalibreScale(w.CalibreMm[i]);
            double fade = (hit ? HitFadeMs : SplashFadeMs) * (0.5 + 0.5 * f);
            if (age < 0 || age > fade || !view.Project(w.Point[i], out double x, out double y))
                continue;
            double grow = Math.Min(1, age / 1500);
            double r = Math.Max((hit ? 12 : SplashWideM * (0.4 + 0.6 * grow)) * f / mpp, (hit ? 4 : 2.5) * Math.Sqrt(f));
            bool own = selected < 0 || w.Shooter[i] == selected;
            var col = (hit ? HitColour : SplashColour) with { W = (float)(1 - age / fade) * (hit ? 1f : 0.9f) * (own || hit ? 1f : 0.4f) };
            overlay.BeginStrip(col, (float)((hit ? 2.5 : 1.5) * Math.Sqrt(f)));
            for (int k = 0; k <= 16; k++)
            {
                double a = k * Math.PI * 2 / 16;
                overlay.Point(x + Math.Cos(a) * r, y + Math.Sin(a) * r);
            }
            overlay.End();
        }
    }

    /// <summary>Each torpedo running, as its wake: a pale line from a little behind it (a few pixels at least) to where it
    /// is now, the selected ship's bright; and a torpedo's hit, a tall orange flash.</summary>
    void DrawTorpedoes(in MapView view, Snapshot s)
    {
        var v = s.Torpedoes;
        long now = world.DrawMs;
        double mpp = view.MetresPerPixel, wake = Math.Max(WakeM, WakeMinPx * mpp);
        for (int i = 0; i < v.Count; i++)
        {
            var head = v.At(i, now, out double run);
            if (!view.Project(head, out double x, out double y) || x < -50 || y < -50 || x > view.Width + 50 || y > view.Height + 50)
                continue;
            bool own = selected < 0 || v.Shooter[i] == selected;
            overlay.BeginStrip(own ? WakeColour : WakeColour with { W = 0.35f }, own ? 1.5f : 1f);
            for (int j = 0; j <= 4; j++)
            {
                var (p, f) = (v.Origin[i], v.Forward[i]);
                Earth.Advance(ref p, ref f, Math.Max(run - wake * (1 - j / 4.0), 0));
                Point(view, p);
            }
            overlay.End();
        }
        var e = s.TorpedoEnds;
        for (int i = 0; i < e.Count; i++)
        {
            double age = now - e.Ms[i];
            if (e.Ship[i] < 0 || age < 0 || age > HitFadeMs * 2 || !view.Project(e.Point[i], out double x, out double y))
                continue;
            double r = Math.Max(TorpedoHitM * Math.Min(1, 0.4 + age / 1000) / mpp, 5);
            overlay.BeginStrip(HitColour with { W = (float)(1 - age / (HitFadeMs * 2)) }, 3f);
            for (int k = 0; k <= 16; k++)
            {
                double a = k * Math.PI * 2 / 16;
                overlay.Point(x + Math.Cos(a) * r, y + Math.Sin(a) * r);
            }
            overlay.End();
        }
    }

    /// <summary>How big a shell's marks draw beside a 12" shell's: by calibre, bounded so a 4" still shows and an 18"
    /// doesn't swamp the sea.</summary>
    static double CalibreScale(double calibreMm) => Math.Clamp(calibreMm / RefCalibreMm, 0.3, 1.6);

    /// <summary>A line from each engaged battery's ship to its target (where the screen shows it: the ship, or its
    /// contact), faint unless it's firing.</summary>
    void DrawTargets(in MapView view, Snapshot s)
    {
        var b = s.Batteries;
        if (b.Count == 0)
            return;
        shipIndex.Clear();
        for (int k = 0; k < s.Ships.Count; k++)
            shipIndex[s.Ships.Entity[k]] = k;
        long now = world.DrawMs;
        for (int i = 0; i < b.Count; i++)
        {
            if (!shipIndex.TryGetValue(b.Shooter[i], out int k) || !TargetAt(s, b.Target[i], now, out var to))
                continue;
            var (from, _) = s.Ships.PoseAt(k, now);
            var col = b.State[i] == BatteryState.Firing ? TargetColour : TargetColour with { W = 0.2f };
            overlay.BeginStrip(col, 1f);
            for (int j = 0; j <= 8; j++)
                Point(view, (from + (to - from) * (j / 8.0)).Normalized() * Earth.Radius);
            overlay.End();
        }
    }

    /// <summary>Where the screen shows a ship: itself if it's in the snapshot, else its contact.</summary>
    bool TargetAt(Snapshot s, int entity, long now, out Vec3d at)
    {
        if (shipIndex.TryGetValue(entity, out int k))
        {
            (at, _) = s.Ships.PoseAt(k, now);
            return true;
        }
        var c = s.Contacts;
        for (int i = 0; i < c.Count; i++)
            if (c.Entity[i] == entity)
            {
                at = c.Position[i];
                return true;
            }
        at = default;
        return false;
    }

    /// <summary>The strip's fire row: the commanded unit's fire order (at will, hold fire, engage: armed, then a
    /// right-click on a ship or contact), and the dev's one salvo "at" the sea; then each of the selected ship's batteries:
    /// its target and state, salvos and hits.</summary>
    void FireRow(Snapshot s, Unit unit, int k)
    {
        var cls = world.Classes.TryGet(s.Ships.Class[k]);
        if (cls == null || cls.Batteries.Length == 0)
            return;
        var o = world.Ai.Orders;
        var fire = (FireOrder)s.Ships.Fire[k];
        ImGui.Text("Guns:");
        ImGui.SameLine();
        if (ImGui.RadioButton("fire at will##fire", fire == FireOrder.AtWill))
            world.Send(o.FireAtWill(unit));
        ImGui.SameLine();
        if (ImGui.RadioButton("hold fire##fire", fire == FireOrder.Hold))
            world.Send(o.HoldFire(unit));
        ImGui.SameLine();
        if (ImGui.Checkbox("engage (right-click a ship)##engage", ref engageArmed) && engageArmed)
            (fireArmed, torpedoArmed) = (false, false);
        if (fire == FireOrder.Engage)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"engaging {TargetLabel(s, s.Ships.FireTarget[k])}");
        }
        ImGui.SameLine();
        ImGui.TextDisabled("  dev:");
        ImGui.SameLine();
        fireBattery = Math.Clamp(fireBattery, 0, cls.Batteries.Length - 1);
        ImGui.SetNextItemWidth(120 * UiFonts.Scale);
        if (ImGui.BeginCombo("##fb", $"{cls.Batteries[fireBattery].Guns} x {cls.Batteries[fireBattery].CalibreMm:0}mm"))
        {
            for (int b = 0; b < cls.Batteries.Length; b++)
                if (ImGui.Selectable($"{cls.Batteries[b].Guns} x {cls.Batteries[b].CalibreMm:0}mm##fb{b}", fireBattery == b))
                    fireBattery = b;
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("salvo at (right-click the sea)##fire", ref fireArmed) && fireArmed)
            (engageArmed, torpedoArmed) = (false, false);
        if (s.Shells.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"{s.Shells.Count} in flight");
        }
        var v = s.Batteries;
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            var row = cls.Batteries[b];
            int i = 0;
            while (i < v.Count && (v.Shooter[i] != selected || v.Battery[i] != b))
                i++;
            string gun = $"  {row.Guns} x {row.CalibreMm:0}mm {(row.Kind == Battery.Main ? "main" : "secondary")}";
            if (i == v.Count)
                ImGui.TextDisabled($"{gun}: no target{(fire == FireOrder.Hold ? " (holding fire)" : "")}");
            else
                ImGui.TextDisabled($"{gun} on {TargetLabel(s, v.Target[i])}: {StateText(v.State[i])}, " +
                                   $"{(v.Local[i] ? "local control" : $"level {v.Level[i]}")}; {v.Salvos[i]} salvos, {v.Hits[i]} hits of {v.Shells[i]}");
        }
        TorpedoRow(s, k, cls);
        DamageRow(s, k, cls);
    }

    /// <summary>The selected ship's torpedoes (docs/battle/fighting.md 4.6): in its tubes and left, its torpedo (fast and
    /// slow settings), and "torpedoes at": armed, a right-click on a ship or contact fires what's loaded and bears.</summary>
    void TorpedoRow(Snapshot s, int k, ShipClass cls)
    {
        if (cls.Launchers.Length == 0)
            return;
        var t = cls.Torpedoes[cls.Launchers[0].Torpedo];
        ImGui.Text("Torpedoes:");
        ImGui.SameLine();
        ImGui.TextDisabled(string.Format(CultureInfo.InvariantCulture, "{0} loaded, {1} left; {2:0}mm, {3:0} kn to {4:0.0} km or {5:0} kn to {6:0.0} km",
            s.Ships.TorpedoesLoaded[k], s.Ships.TorpedoesLeft[k], t.CalibreMm, t.FastKn, t.FastRangeM / 1000, t.SlowKn, t.SlowRangeM / 1000));
        ImGui.SameLine();
        if (ImGui.Checkbox("torpedoes at (right-click a ship)##torpedo", ref torpedoArmed) && torpedoArmed)
            (engageArmed, fireArmed) = (false, false);
        int running = 0;
        for (int i = 0; i < s.Torpedoes.Count; i++)
            running += s.Torpedoes.Shooter[i] == selected ? 1 : 0;
        if (running > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"{running} running");
        }
    }

    /// <summary>The selected ship's condition, if it's been hit (docs/battle/fighting.md 4.4, damage-model-research.md §12: the
    /// best model fails without a report): one row of what it can still do (the speed its power drives it at with its
    /// water's weight, steering, guns and directors working, water and list, fires, crew lost), its water against what
    /// buoyancy it has left, and the report: its log's lines, cause to effect, newest first.</summary>
    void DamageRow(Snapshot s, int k, ShipClass cls)
    {
        var c = s.Conditions;
        int i = c.IndexOf(selected);
        if (i < 0)
            return;
        var inv = CultureInfo.InvariantCulture;
        double top = cls.Motion.MaxSpeedKn, kn = cls.SpeedAt(cls.PowerAt(top) * c.HeldCap[i]);
        string steer = c.Steering[i] switch
        {
            Steering.Jammed => "RUDDER JAMMED",
            Steering.Lost => "steering lost",
            _ => "steering",
        };
        var a = c.Attitude[i];
        string list = Math.Abs(a.HeelDeg) >= 0.5 ? string.Format(inv, ", list {0:0}° {1}", Math.Abs(a.HeelDeg), a.HeelDeg >= 0 ? "stbd" : "port") : "";
        string trim = Math.Abs(a.TrimDeg) >= 0.3 ? string.Format(inv, ", down by the {0}", a.TrimDeg >= 0 ? "bow" : "stern") : "";
        string down = c.Status[i] == ShipStatus.GoingDown ? c.Loss[i] switch
        {
            LossKind.Magazine => " · MAGAZINE: GOING DOWN",
            LossKind.Capsized => " · CAPSIZING",
            LossKind.BrokeInTwo => " · BROKEN IN TWO",
            _ => " · FOUNDERING",
        } : "";
        ImGui.TextColored(c.Status[i] == ShipStatus.GoingDown ? new Vector4(1f, 0.4f, 0.3f, 1) : new Vector4(1f, 0.85f, 0.6f, 1), string.Format(inv,
            "Condition: {0:0.0} of {1:0.0} kn · {2} · guns {3} of {4} · directors {5} of {6} · water {7:0} t{8}{9} · fires {10} · crew lost {11:0}{12}",
            kn, top, steer, c.MountsWorking[i], c.Mounts[i], c.DirectorsWorking[i], c.Directors[i], c.WaterT[i], list, trim, c.Fires[i], c.CrewLost[i], down));
        if (c.WaterT[i] > 0)
            ImGui.TextDisabled(string.Format(inv, "  {0:0} t aboard of {1:0} t reserve buoyancy; settled {2:0.00} m, list {3:0.0}°, trim {4:0.00}°; holes {5:0.00} m²; {6} hits",
                c.WaterT[i], c.WaterT[i] + c.ReserveT[i], a.SinkageM, a.HeelDeg, a.TrimDeg, c.BreachM2[i], c.Hits[i]));
        if (!ImGui.TreeNode($"Damage report ({c.Hits[i]} hits)##report"))
            return;
        var log = s.CombatLog;
        for (int j = 0, shown = 0; j < log.Count && shown < 12; j++)
        {
            ref readonly var e = ref log.Entries[j];
            if (e.Target != selected || e.Kind is not (CombatEvent.Hit or CombatEvent.Lost))
                continue;
            shown++;
            string when = new WorldTime(e.Ms).ToUtc().ToString("HH:mm:ss", inv);
            if (e.Kind == CombatEvent.Lost)
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.3f, 1), $"{when}  {(LossKind)e.Count switch { LossKind.Magazine => "magazine exploded", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", _ => "foundered" }}");
            else
                ImGui.TextDisabled($"{when}  {HitText(e, cls)}");
        }
        ImGui.TreePop();
    }

    /// <summary>A hit's line, cause to effect: "305mm AP · B (main) · defeated · jammed".</summary>
    static string HitText(in CombatEntry e, ShipClass target)
    {
        string part = e.Part < 0 ? "-" : e.Part < target.Components.Length
            ? $"{target.ComponentId(e.Part)}"
            : $"{target.RoomId(e.Part - target.Components.Length)}";
        string outcome = e.Outcome switch
        {
            Outcome.PassedThrough => "passed through",
            Outcome.BurstInside => "burst inside",
            Outcome.BurstInPlate => "burst in the plate",
            Outcome.LowOrder => "low-order burst",
            Outcome.BurstOutside => "burst outside",
            Outcome.BrokeUp => "broke up",
            Outcome.BurstBeyond => "burst beyond",
            var o => o.ToString().ToLowerInvariant(),
        };
        var fx = new List<string>();
        foreach (var (f, word) in HitWords)
            if (e.Effect.HasFlag(f))
                fx.Add(word);
        string kind = e.Shell switch { ShellKind.Ap => "AP", ShellKind.Common => "common", _ => "HE" };
        return string.Format(CultureInfo.InvariantCulture, "{0:0}mm {1} · {2} · {3}{4}", e.CalibreMm, kind, part, outcome, fx.Count > 0 ? " · " + string.Join(", ", fx) : "");
    }

    static readonly (HitEffect, string)[] HitWords =
    [
        (HitEffect.Magazine, "MAGAZINE"), (HitEffect.Broken, "BROKEN IN TWO"), (HitEffect.Steering, "steering"), (HitEffect.Power, "power cut"), (HitEffect.Jammed, "turret jammed"),
        (HitEffect.Destroyed, "wrecked"), (HitEffect.Disabled, "put out of action"), (HitEffect.Damaged, "damaged"), (HitEffect.Holed, "holed"),
        (HitEffect.Fire, "fire"),
    ];

    /// <summary>A target as the player knows it: a ship on the screen (own side, or all with "truth") by its label, else
    /// its contact (the class it's taken for, or "contact").</summary>
    string TargetLabel(Snapshot s, int entity)
    {
        if (s.Ships.IndexOf(entity) >= 0)
            return ShipLabel(s, entity);
        var c = s.Contacts;
        for (int i = 0; i < c.Count; i++)
            if (c.Entity[i] == entity)
                return c.TakenFor[i] >= 0 && world.Classes.TryGet(c.TakenFor[i]) is { } taken ? $"#{entity} {taken.Name}?" : $"#{entity} contact";
        return $"#{entity}";
    }

    /// <summary>Whether a battery the screen shows is silent for the coarse tier (the clock panel says why).</summary>
    static bool GunsSilent(Snapshot s)
    {
        var b = s.Batteries;
        for (int i = 0; i < b.Count; i++)
            if (b.State[i] == BatteryState.Silent)
                return true;
        return false;
    }

    static string StateText(BatteryState s) => s switch
    {
        BatteryState.NoContact => "no contact",
        BatteryState.Tracking => "tracking",
        BatteryState.Firing => "firing",
        BatteryState.NotBearing => "not bearing",
        BatteryState.OutOfRange => "out of range",
        _ => "silent (coarse tier)",
    };

    /// <summary>The armed right-click: engage the ship or contact there with the selected ship's battery (or all), fire
    /// its torpedoes at it, or fire one salvo at the sea there.</summary>
    bool FireAt(double x, double y)
    {
        if (selected < 0)
            return false;
        if (engageArmed || torpedoArmed)
        {
            // an enemy in sight by its sprite, a ship, else a contact by its marker
            var s = world.Current;
            int target = PickShip(View, x, y, s.Sighted);
            if (target < 0)
                target = PickShip(View, x, y, s.Ships);
            if (target < 0)
                target = PickContact(View, s, x, y);
            int k = s.Ships.IndexOf(selected);
            if (target > 0 && target != selected && torpedoArmed && k >= 0)
                world.Send(TorpedoSystem.Launch(selected, target, Math.Max(s.Ships.TorpedoesLoaded[k], 1)));
            else if (target > 0 && target != selected)
                world.Send(world.Ai.Orders.Engage(Commanded(world.Current).Unit, target));
            (engageArmed, torpedoArmed) = (false, false);
            return true;
        }
        if (!fireArmed || !View.Pick(x, y, out var at))
            return false;
        var (lat, lon) = Earth.LatLon(at);
        world.Send(ShellSystem.FireAt(selected, fireBattery, lat, lon));
        return true;
    }

    /// <summary>The contact whose marker is under the cursor (within a few pixels), or -1.</summary>
    static int PickContact(in MapView view, Snapshot s, double x, double y)
    {
        var c = s.Contacts;
        int best = -1;
        double bestD = ContactMarkerPx * 3;
        for (int i = 0; i < c.Count; i++)
            if (view.Project(c.Position[i], out double cx, out double cy) && Math.Abs(cx - x) + Math.Abs(cy - y) < bestD)
                (best, bestD) = (c.Entity[i], Math.Abs(cx - x) + Math.Abs(cy - y));
        return best;
    }
}
