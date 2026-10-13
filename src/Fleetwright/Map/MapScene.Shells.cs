using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Shells and splashes on the map (docs/battle/fighting.md 4.2a), in the map's style (decision 10: no effects
/// yet). Each shell is looked up in its ballistic table at the frame's time, so it flies smoothly at any rate with
/// nothing but its record in the snapshot: a dot, and a short tracer behind it once it's a few pixels long. A splash is
/// a ring that widens and fades; a hit, an orange flash. Fire control (4.2b): a line from each engaged battery's ship
/// to its target. The strip's fire row (dev, until the fire orders of 4.2c): a battery (or all), then a right-click on a
/// ship or a contact engages it, or on the sea fires one salvo there ("at"); each engaged battery's state below.</summary>
public sealed partial class MapScene
{
    static readonly Vector4 ShellColour = new(1f, 0.95f, 0.6f, 0.95f), SplashColour = new(0.9f, 0.95f, 1f, 0.9f),
        HitColour = new(1f, 0.55f, 0.15f, 1f), TargetColour = new(1f, 0.35f, 0.25f, 0.55f);
    const double TracerS = 0.25;      // the tracer's length, seconds of flight
    const double SplashWideM = 30, SplashFadeMs = 8000, HitFadeMs = 4000;
    bool fireArmed, engageArmed;
    int fireBattery;   // -1: all of them (engage only)
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
            overlay.BeginStrip(ShellColour, 2f);
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
            var col = (hit ? HitColour : SplashColour) with { W = (float)(1 - age / fade) * (hit ? 1f : 0.9f) };
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

    /// <summary>The strip's fire row: which battery (or all), and what a right-click does with it: engage the ship or
    /// contact there, or fire one salvo at the sea there; cease fire; each engaged battery's state.</summary>
    void FireRow(Snapshot s, int k)
    {
        var cls = world.Classes.TryGet(s.Ships.Class[k]);
        if (cls == null || cls.Batteries.Length == 0)
            return;
        ImGui.Text("Fire:");
        ImGui.SameLine();
        fireBattery = Math.Clamp(fireBattery, -1, cls.Batteries.Length - 1);
        if (ImGui.RadioButton("all##fb", fireBattery < 0))
            fireBattery = -1;
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            var row = cls.Batteries[b];
            ImGui.SameLine();
            if (ImGui.RadioButton($"{row.Guns} x {row.CalibreMm:0}mm##fb{b}", fireBattery == b))
                fireBattery = b;
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("engage (right-click a ship)##engage", ref engageArmed) && engageArmed)
            fireArmed = false;
        ImGui.SameLine();
        if (ImGui.Checkbox("at (right-click the sea)##fire", ref fireArmed) && fireArmed)
            engageArmed = false;
        ImGui.SameLine();
        if (ImGui.Button("cease##fire"))
            world.Send(GunnerySystem.Cease(selected, fireBattery));
        if (s.Shells.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"{s.Shells.Count} in flight");
        }
        var v = s.Batteries;
        for (int i = 0; i < v.Count; i++)
        {
            if (v.Shooter[i] != selected || v.Battery[i] >= cls.Batteries.Length)
                continue;
            var row = cls.Batteries[v.Battery[i]];
            ImGui.TextDisabled($"  {row.Guns} x {row.CalibreMm:0}mm on #{v.Target[i]}: {StateText(v.State[i])}, " +
                               $"{(v.Local[i] ? "local control" : $"level {v.Level[i]}")}; {v.Salvos[i]} salvos, {v.Hits[i]} hits of {v.Shells[i]}");
        }
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
        if (selected == 0)
            return false;
        if (engageArmed)
        {
            int target = PickShip(View, x, y);
            if (target == 0)
                target = PickContact(View, world.Current, x, y);
            if (target != 0 && target != selected)
                world.Send(GunnerySystem.Engage(selected, fireBattery, target));
            return true;
        }
        if (!fireArmed || !View.Pick(x, y, out var at))
            return false;
        var (lat, lon) = Earth.LatLon(at);
        world.Send(ShellSystem.FireAt(selected, Math.Max(fireBattery, 0), lat, lon));
        return true;
    }

    /// <summary>The contact whose marker is under the cursor (within a few pixels), or 0.</summary>
    static int PickContact(in MapView view, Snapshot s, double x, double y)
    {
        var c = s.Contacts;
        int best = 0;
        double bestD = ContactMarkerPx * 3;
        for (int i = 0; i < c.Count; i++)
            if (view.Project(c.Position[i], out double cx, out double cy) && Math.Abs(cx - x) + Math.Abs(cy - y) < bestD)
                (best, bestD) = (c.Entity[i], Math.Abs(cx - x) + Math.Abs(cy - y));
        return best;
    }
}
