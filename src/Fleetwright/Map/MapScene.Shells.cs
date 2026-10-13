using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Shells and splashes on the map (docs/battle/fighting.md 4.2a), in the map's style (decision 10: no effects
/// yet). Each shell is looked up in its ballistic table at the frame's time, so it flies smoothly at any rate with
/// nothing but its record in the snapshot: a dot, and a short tracer behind it once it's a few pixels long. A splash is
/// a ring that widens and fades; a hit, an orange flash. The dev's "fire at" (until fire control, 4.2b): the strip's
/// fire row arms a right-click on the sea to fire the selected ship's battery there.</summary>
public sealed partial class MapScene
{
    static readonly Vector4 ShellColour = new(1f, 0.95f, 0.6f, 0.95f), SplashColour = new(0.9f, 0.95f, 1f, 0.9f),
        HitColour = new(1f, 0.55f, 0.15f, 1f);
    const double TracerS = 0.25;      // the tracer's length, seconds of flight
    const double SplashWideM = 30, SplashFadeMs = 8000, HitFadeMs = 4000;
    bool fireArmed;
    int fireBattery;

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

    /// <summary>The strip's fire row: which battery, and whether a right-click fires it.</summary>
    void FireRow(Snapshot s, int k)
    {
        var cls = world.Classes.TryGet(s.Ships.Class[k]);
        if (cls == null || cls.Batteries.Length == 0)
            return;
        ImGui.Text("Fire:");
        ImGui.SameLine();
        fireBattery = Math.Clamp(fireBattery, 0, cls.Batteries.Length - 1);
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            var row = cls.Batteries[b];
            if (b > 0)
                ImGui.SameLine();
            if (ImGui.RadioButton($"{row.Guns} x {row.CalibreMm:0}mm##fb{b}", fireBattery == b))
                fireBattery = b;
        }
        ImGui.SameLine();
        ImGui.Checkbox("at (right-click)##fire", ref fireArmed);
        if (s.Shells.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"{s.Shells.Count} in flight");
        }
    }

    /// <summary>The armed right-click: the selected ship's battery at the sea there.</summary>
    bool FireAt(double x, double y)
    {
        if (!fireArmed || selected == 0 || !View.Pick(x, y, out var at))
            return false;
        var (lat, lon) = Earth.LatLon(at);
        world.Send(ShellSystem.FireAt(selected, fireBattery, lat, lon));
        return true;
    }
}
