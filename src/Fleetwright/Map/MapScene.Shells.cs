using System.Globalization;
using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Shells, splashes and torpedoes on the map (docs/battle/fighting.md 4.2a, 4.6), as the table's marks
/// (docs/visuals/battle-marks.md): symbols at a pixel size, sized by calibre and timed by the physics, kept in the world
/// view too until it has effects of its own. What moves is a streak, a head and a tail fading to nothing: a shell's short
/// and hot in its side's tint, swelling toward its apex; a torpedo's track long and white on the water where it was
/// laid, fading with age. What happens is a pop, then a trace: a gun's flash at the muzzle; a splash's column as a white
/// glow over its rise and fall, then a foam ring for the snapshot's while; a hit's warm flash and sparks; a torpedo's
/// tall white column. The selected ship's fire is bright, the others' dimmer. Pops have a floor in real time, so they
/// still blink at speed. Each shell is looked up in its ballistic table at the frame's time, so it flies smoothly at any
/// rate with nothing but its record in the snapshot. Fire control (4.2b): a line from each engaged battery's ship to its
/// target. The strip's fire row (4.2c): the commanded unit's fire order (at will, hold fire, engage: a right-click on a
/// ship or a contact), each of the selected ship's batteries with its target and state, and the dev's salvo "at" a point
/// of the sea; the torpedo row (loaded and left, "torpedoes at" a ship).</summary>
public sealed partial class MapScene
{
    static readonly Vector4 ShellHead = new(1f, 0.93f, 0.72f, 1f), ShellTail = new(1f, 0.8f, 0.45f, 0.8f), GunFlash = new(1f, 0.82f, 0.5f, 0.9f),
        Water = new(0.92f, 0.96f, 1f, 0.95f), HitCore = new(1f, 0.92f, 0.7f, 1f), HitColour = new(1f, 0.5f, 0.12f, 1f),
        HitSmoke = new(0.18f, 0.18f, 0.2f, 0.5f), TargetColour = new(1f, 0.35f, 0.25f, 0.55f), TrackWhite = new(0.9f, 0.96f, 1f, 0.8f);

    // the shell: its tail covers this much of its flight, within these lengths on the screen; its head, and its swell at
    // the apex (seen from above it rises toward the eye)
    const double TailS = 1.5, TailMinPx = 8, TailMaxPx = 40, HeadPx = 2.4, ApexSwell = 0.6;
    // the gun's flash: physically a few hundredths of a second, held a little so a broadside reads
    const double FlashS = 0.35, FlashPx = 7;
    // the splash: a column H = 180 x calibre (research/vfx/shell-splashes.md), rising in sqrt(2H/g) and down by twice
    // that and a second; the column's glow and the foam ring, px at a 12"
    const double ColumnPerM = 180, ColumnPx = 3.2, FoamPx = 7;
    // a hit: the flash, its sparks and the smoke after it
    const double HitFlashS = 1.2, HitPx = 7, SparkS = 0.7, SparkPx = 16, HitSmokeS = 6;
    // a torpedo's hit: its column, px, and how long it stands (the track itself is Torpedoes.TorpedoTrack's)
    const double TorpedoColumnPx = 11, TorpedoColumnS = 9;
    // a pop's least time on the screen, in real seconds, whatever the clock's rate
    const double PopFloorS = 0.2;
    const double RefCalibreMm = 305;
    bool fireArmed, engageArmed, torpedoArmed;
    int fireBattery;   // the dev's salvo "at": which battery
    readonly Dictionary<int, int> shipIndex = [];   // a frame's ships by entity, for the target lines
    readonly Dictionary<int, byte> sideOf = [];     // a frame's known shooters' sides, by entity

    /// <summary>A pop's life in world seconds: its own, or the real-time floor at the clock's rate.</summary>
    static double Floor(Snapshot s, double lifeS) => s.Paused ? lifeS : Math.Max(lifeS, PopFloorS * s.AchievedSpeed);

    /// <summary>The tint of whoever fired: its side's, as its icon is, or <paramref name="unknown"/> when not known.</summary>
    Vector4 ShooterTint(int shooter, Vector4 unknown)
    {
        if (!sideOf.TryGetValue(shooter, out byte side) || side >= ShipDrawList.SideTints.Length)
            return unknown;
        return ShipDrawList.SideTints[side];
    }

    /// <summary>The sides of the frame's ships and of the contacts made out.</summary>
    void Sides(Snapshot s)
    {
        sideOf.Clear();
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            for (int k = 0; k < v.Count; k++)
                sideOf[v.Entity[k]] = v.Side[k];
        var c = s.Contacts;
        for (int i = 0; i < c.Count; i++)
            if (c.Side[i] != 255)
                sideOf.TryAdd(c.Entity[i], c.Side[i]);
    }

    void DrawShells(in MapView view, Snapshot s)
    {
        Sides(s);
        var v = s.Shells;
        long now = world.DrawMs;
        double flashS = Floor(s, FlashS);
        // the gun's flash gives way to the world view's (docs/visuals/muzzle-flashes.md) over the cross-fade
        float tableFlash = Flashes.FlashLook.On ? (float)(1 - WorldView.Factor(view)) : 1f;
        if (v.Ballistics is { } tables)
            for (int i = 0; i < v.Count; i++)
            {
                double t = (now - v.FiredMs[i]) / 1000.0;
                if (t < 0 || t > v.EndS[i] || v.Table[i] >= tables.Count)
                    continue;
                var table = tables[v.Table[i]];
                double f = CalibreScale(table.Key.CalibreMm);
                float dim = selected < 0 || v.Shooter[i] == selected ? 1f : 0.45f;
                if (t < flashS && view.Project(v.Muzzle[i], out double mx, out double my))
                    overlay.Disc(mx, my, FlashPx * Math.Sqrt(f) * (1 - 0.5 * t / flashS), GunFlash with { W = GunFlash.W * dim * tableFlash * (float)(1 - t / flashS) });
                var p = table.Position(v.Muzzle[i], v.Forward[i], v.ElevationDeg[i], v.Speed[i], t, out _);
                if (!view.Project(p, out double x, out double y) || x < -TailMaxPx || y < -TailMaxPx || x > view.Width + TailMaxPx || y > view.Height + TailMaxPx)
                    continue;
                // the head swells toward the apex: a parabola's top is g T^2 / 8 over its ends
                double apex = 9.81 * v.EndS[i] * v.EndS[i] / 8, up = Math.Clamp((p.Length - Earth.Radius) / Math.Max(apex, 1), 0, 1);
                double head = HeadPx * Math.Sqrt(f) * (1 + ApexSwell * up);
                Streak(view, table, v, i, t, x, y, f, head, ShooterTint(v.Shooter[i], ShellTail) with { W = ShellTail.W * dim });
                overlay.Disc(x, y, head * 1.3, ShellHead with { W = dim });
            }

        DrawTargets(view, s);
        DrawTorpedoes(view, s);
        DrawSplashes(view, s);
    }

    /// <summary>A shell's tail: back along its flight for <see cref="TailS"/> (further while it's under
    /// <see cref="TailMinPx"/>, never past <see cref="TailMaxPx"/>), tapering and fading from the head to nothing.</summary>
    void Streak(in MapView view, BallisticTable table, ShellsView v, int i, double t, double x, double y, double f, double head, Vector4 tint)
    {
        const int Max = 12;
        Span<double> px = stackalloc double[Max * 2];
        double len = 0, maxPx = TailMaxPx * Math.Sqrt(f), step = TailS / 6;
        int n = 0;
        (px[0], px[1]) = (x, y);
        double lx = x, ly = y;
        for (int k = 1; k < Max && t - k * step > 0; k++)
        {
            var q = table.Position(v.Muzzle[i], v.Forward[i], v.ElevationDeg[i], v.Speed[i], t - k * step, out _);
            if (!view.Project(q, out double qx, out double qy))
                break;
            double d = Math.Sqrt((qx - lx) * (qx - lx) + (qy - ly) * (qy - ly));
            if (len + d > maxPx)
            {
                double a = (maxPx - len) / Math.Max(d, 1e-6);
                (qx, qy, len) = (lx + (qx - lx) * a, ly + (qy - ly) * a, maxPx);
            }
            else
                len += d;
            n = k;
            (px[2 * k], px[2 * k + 1], lx, ly) = (qx, qy, qx, qy);
            if (len >= maxPx || k * step >= TailS && len >= TailMinPx)
                break;
        }
        if (len < 1)
            return;
        double along = 0;
        for (int k = n; k >= 0; k--)
        {
            // from the tail's end (nothing) to the head (whole), by length, so it fades the same at any zoom
            double u = 1 - along / len;
            if (k > 0)
                along += Math.Sqrt(Math.Pow(px[2 * k] - px[2 * k - 2], 2) + Math.Pow(px[2 * k + 1] - px[2 * k - 1], 2));
            overlay.Point(px[2 * k], px[2 * k + 1], tint with { W = (float)(tint.W * (1 - u) * (1 - u)) }, (float)(head * 1.2 * (1 - u * 0.8)));
        }
        overlay.End();
    }

    /// <summary>The splashes and hits of the snapshot's while: a miss's column as a white glow over its rise and fall,
    /// then its foam ring, faintly in its dye or else the shooter's tint; a hit's flash, sparks and smoke. A miss's marks
    /// give way to the world view's splashes (docs/visuals/splashes.md) over the cross-fade, as the track line does to the
    /// foam lane.</summary>
    void DrawSplashes(in MapView view, Snapshot s)
    {
        var w = s.Splashes;
        long now = world.DrawMs;
        float table = Splashes.SplashLook.On ? (float)(1 - WorldView.Factor(view)) : 1f;
        double life = world.Settings.SplashLifeMs / 1000.0, hitS = Floor(s, HitFlashS), sparkS = Floor(s, SparkS);
        for (int i = 0; i < w.Count; i++)
        {
            double age = (now - w.Ms[i]) / 1000.0;
            if (age < 0 || !view.Project(w.Point[i], out double x, out double y) || x < -40 || y < -40 || x > view.Width + 40 || y > view.Height + 40)
                continue;
            double g = Math.Sqrt(CalibreScale(w.CalibreMm[i]));
            float dim = selected < 0 || w.Shooter[i] == selected ? 1f : 0.5f;
            if (w.Ship[i] >= 0)
            {
                Hit(x, y, age, g, hitS, sparkS, Seed(w.Point[i]), HitPx, HitSmokeS);
                continue;
            }
            // the column: up in sqrt(2H/g), down by twice that and a second (12": 3.3 s, 7.7 s)
            double rise = Math.Sqrt(2 * ColumnPerM * w.CalibreMm[i] / 1000 / 9.81), fall = Floor(s, 2 * rise + 1);
            if (table <= 0)
                continue;
            if (age < fall)
            {
                double up = Math.Min(age / rise, 1), r = ColumnPx * g * (0.5 + 0.5 * (1 - (1 - up) * (1 - up)));
                double a = age < rise ? 1 : 1 - (age - rise) / (fall - rise);
                overlay.Disc(x, y, r * 1.6, Water with { W = (float)(Water.W * a * (0.6 + 0.4 * dim)) * table }, 0.55f);
            }
            // the foam: a thin ring that widens a little and fades over the snapshot's while
            double fa = 1 - age / life;
            if (fa > 0)
            {
                var dye = w.Dye[i] != Dye.None ? new Vector4(Splashes.SplashLook.Palette[(int)w.Dye[i]], 1) : ShooterTint(w.Shooter[i], Water);
                var tint = Vector4.Lerp(Water, dye, 0.4f);
                Ring(x, y, FoamPx * g * (0.5 + 0.5 * Math.Min(1, age / 4)), tint with { W = (float)(0.35 * fa * fa * dim) * table }, 1.2f);
            }
        }
    }

    /// <summary>A hit's pop: a white-hot core going orange, sparks flying out and fading, then a grey puff.</summary>
    void Hit(double x, double y, double age, double g, double flashS, double sparkS, uint seed, double size, double smokeS, float fade = 1)
    {
        if (age < smokeS)
            overlay.Disc(x, y, size * g * (0.8 + age / smokeS), HitSmoke with { W = (float)(HitSmoke.W * (1 - age / smokeS)) * fade });
        if (age < flashS)
        {
            double a = age / flashS;
            overlay.Disc(x, y, size * g * (1.4 - 0.6 * a), Vector4.Lerp(HitCore, HitColour, (float)Math.Min(1, a * 2)) with { W = (float)(1 - a * a) * fade });
        }
        if (age >= sparkS)
            return;
        double sa = age / sparkS, reach = SparkPx * g * (1 - (1 - sa) * (1 - sa));
        for (int k = 0; k < 5; k++)
        {
            seed = seed * 1664525 + 1013904223;
            double ang = (seed >> 8) / (double)(1 << 24) * Math.PI * 2, rr = reach * (0.6 + 0.4 * ((seed & 255) / 255.0));
            double cx = Math.Cos(ang), cy = Math.Sin(ang);
            overlay.Point(x + cx * rr * 0.55, y + cy * rr * 0.55, HitColour with { W = 0 }, 0.5f);
            overlay.Point(x + cx * rr, y + cy * rr, HitCore with { W = (float)(1 - sa) * fade }, 1.6f);
            overlay.End();
        }
    }

    /// <summary>A thin ring of radius r px.</summary>
    void Ring(double x, double y, double r, Vector4 rgba, float widthPx)
    {
        overlay.BeginStrip(rgba, widthPx);
        int segs = r < 4 ? 8 : r < 10 ? 12 : 20;
        for (int k = 0; k <= segs; k++)
        {
            double a = k * Math.PI * 2 / segs;
            overlay.Point(x + Math.Cos(a) * r, y + Math.Sin(a) * r);
        }
        overlay.End();
    }

    /// <summary>A mark's own seed, from where it is (the same each frame, whatever its place in the list).</summary>
    static uint Seed(Vec3d p) => (uint)(BitConverter.DoubleToInt64Bits(p.X) ^ BitConverter.DoubleToInt64Bits(p.Y) >> 7 ^ BitConverter.DoubleToInt64Bits(p.Z) >> 13);

    /// <summary>The torpedoes seen, kept past their runs so their tracks fade where they lie.</summary>
    readonly Torpedoes.TorpedoBook torpedoBook = new();

    /// <summary>Each torpedo's track, running or run: a thin white line on the water as its bubbles surface and fade
    /// (<see cref="Torpedoes.TorpedoTrack"/>, the world view's model too: the head well behind the torpedo); a faint dot at
    /// the torpedo itself while it runs. A hit: a tall white column with a warm flash at its foot, and its ring.</summary>
    void DrawTorpedoes(in MapView view, Snapshot s)
    {
        long now = world.DrawMs;
        double life = Torpedoes.TorpedoTrack.LifeS;
        torpedoBook.Update(s.Torpedoes, s.TorpedoEnds, now, Torpedoes.TorpedoTrack.KeepS);
        for (int i = 0; i < torpedoBook.Count; i++)
        {
            ref readonly var t = ref torpedoBook[i];
            double age = (now - t.LaunchMs) / 1000.0;   // since launch
            if (age < 0 || age > t.EndS + life)
                continue;
            double end = Math.Min(age, t.EndS), start = Math.Max(0, age - life);   // the run's stretch still on the water, s
            bool dives = t.Class >= 0 && t.Launcher >= 0 && classOf(t.Class) is { } tc && t.Launcher < tc.Launchers.Length && tc.Launchers[t.Launcher].Kind == LauncherKind.Mount;
            float dim = selected < 0 || t.Shooter == selected ? 1f : 0.5f;
            const int N = 10;
            for (int j = 0; j <= N; j++)
            {
                double ts = start + (end - start) * j / N;
                double since = age - ts, fade = 1 - since / life;
                double strength = Torpedoes.TorpedoTrack.Strength(since, Torpedoes.TorpedoTrack.Depth(t.SpeedMs * ts, t.DepthM, dives));
                if (view.Project(t.At(ts), out double x, out double y))
                    overlay.Point(x, y, TrackWhite with { W = (float)(TrackWhite.W * dim * strength) }, (float)(0.8 + 0.9 * fade));
                else
                    overlay.End();
            }
            overlay.End();
            if (age < t.EndS && view.Project(t.At(age), out double hx, out double hy))
                overlay.Disc(hx, hy, 2.5, TrackWhite with { W = 0.45f * dim });
        }

        var e = s.TorpedoEnds;
        life = world.Settings.SplashLifeMs / 1000.0;
        double colS = Floor(s, TorpedoColumnS), flashS = Floor(s, HitFlashS), sparkS = Floor(s, SparkS);
        for (int i = 0; i < e.Count; i++)
        {
            double age = (now - e.Ms[i]) / 1000.0;
            if (e.Ship[i] < 0 || age < 0 || !view.Project(e.Point[i], out double x, out double y))
                continue;
            if (age < colS)
                overlay.Disc(x, y, TorpedoColumnPx * (0.6 + 0.4 * Math.Min(age / 2, 1)) * 1.8, Water with { W = (float)(1 - age / colS) }, 0.6f);
            Hit(x, y, age, 1, flashS, sparkS, Seed(e.Point[i]), HitPx * 0.8, HitSmokeS);
            double fa = 1 - age / life;
            if (fa > 0)
                Ring(x, y, TorpedoColumnPx * (1 + Math.Min(1, age / 6)), Water with { W = (float)(0.5 * fa * fa) }, 1.5f);
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
        if (ImGui.BeginCombo("##fb", cls.Batteries[fireBattery].Label))
        {
            for (int b = 0; b < cls.Batteries.Length; b++)
                if (ImGui.Selectable($"{cls.Batteries[b].Label}##fb{b}", fireBattery == b))
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
            string gun = $"  {row.Label} {(row.Kind == Battery.Main ? "main" : "secondary")}";
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
