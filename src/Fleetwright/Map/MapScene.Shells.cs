using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ships;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Shells and splashes on the map (docs/battle/fighting.md 4.2a), in the map's style (decision 10: no effects
/// yet). Each shell is looked up in its ballistic table at the frame's time, so it flies smoothly at any rate with
/// nothing but its record in the snapshot: a dot, and a short tracer behind it once it's a few pixels long. A splash is
/// a ring that widens and fades; a hit, an orange flash; the selected ship's bright, the others' dim. Fire control (4.2b):
/// a line from each engaged battery's ship to its target. The strip's fire row (4.2c): the commanded unit's fire order
/// (at will, hold fire, engage: a right-click on a ship or a contact), each of the selected ship's batteries with its
/// target and state, and the dev's salvo "at" a point of the sea.</summary>
public sealed partial class MapScene
{
    static readonly Vector4 ShellColour = new(1f, 0.95f, 0.6f, 0.95f), SplashColour = new(0.9f, 0.95f, 1f, 0.9f),
        HitColour = new(1f, 0.55f, 0.15f, 1f), TargetColour = new(1f, 0.35f, 0.25f, 0.55f);
    const double TracerS = 0.25;      // the tracer's length, seconds of flight
    const double SplashWideM = 30, SplashFadeMs = 8000, HitFadeMs = 4000;
    bool fireArmed, engageArmed;
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
            var back = table.Position(v.Muzzle[i], v.Forward[i], v.ElevationDeg[i], v.Speed[i], Math.Max(0, t - TracerS), out _);
            bool own = selected < 0 || v.Shooter[i] == selected;
            overlay.BeginStrip(own ? ShellColour : ShellColour with { W = 0.35f }, own ? 2f : 1.5f);
            if (view.Project(back, out double bx, out double by) && Math.Abs(bx - x) + Math.Abs(by - y) > 3)
                overlay.Point(bx, by);
            else
                overlay.Point(x - 1, y);
            overlay.Point(x, y);
            overlay.End();
        }

        DrawTargets(view, s);

        var w = s.Splashes;
        for (int i = 0; i < w.Count; i++)
        {
            double age = now - w.Ms[i];
            bool hit = w.Ship[i] >= 0;
            double fade = hit ? HitFadeMs : SplashFadeMs;
            if (age < 0 || age > fade || !view.Project(w.Point[i], out double x, out double y))
                continue;
            double grow = Math.Min(1, age / 1500);
            double r = Math.Max((hit ? 12 : SplashWideM * (0.4 + 0.6 * grow)) / mpp, hit ? 4 : 2.5);
            bool own = selected < 0 || w.Shooter[i] == selected;
            var col = (hit ? HitColour : SplashColour) with { W = (float)(1 - age / fade) * (hit ? 1f : 0.9f) * (own || hit ? 1f : 0.4f) };
            overlay.BeginStrip(col, hit ? 2.5f : 1.5f);
            for (int k = 0; k <= 16; k++)
            {
                double a = k * Math.PI * 2 / 16;
                overlay.Point(x + Math.Cos(a) * r, y + Math.Sin(a) * r);
            }
            overlay.End();
        }
    }

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
            fireArmed = false;
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
            engageArmed = false;
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
    }

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

    /// <summary>The armed right-click: engage the ship or contact there with the selected ship's battery (or all), or
    /// fire one salvo at the sea there.</summary>
    bool FireAt(double x, double y)
    {
        if (selected < 0)
            return false;
        if (engageArmed)
        {
            // an enemy in sight by its sprite, a ship, else a contact by its marker
            var s = world.Current;
            int target = PickShip(View, x, y, s.Sighted);
            if (target < 0)
                target = PickShip(View, x, y, s.Ships);
            if (target < 0)
                target = PickContact(View, s, x, y);
            if (target > 0 && target != selected)
                world.Send(world.Ai.Orders.Engage(Commanded(world.Current).Unit, target));
            engageArmed = false;
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
