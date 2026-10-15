using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Damage seen on the map (docs/battle/fighting.md 4.4), as the table's marks (docs/visuals/battle-marks.md M4,
/// until the smoke field and the world view's own effects): on the ships shown (own, and enemies in sight: what an
/// observer sees), scorches on the icon at parts out of action or wrecked, a flickering glow at each burning space and
/// part with one smoke streak drifting downwind, a ring round a ship going down (the sea creeping over the icon is the
/// icon's own, <see cref="ShipDrawList"/>), a magazine's blast and pall; ships going down watched until they're gone,
/// then a wreck marker where they went (own ships always; an enemy's only when it was seen to go); each encounter's
/// losses as the player's side knows them.</summary>
public sealed partial class MapScene
{
    static readonly Vector4 FlameColour = new(1f, 0.55f, 0.12f, 0.95f), SmokeColour = new(0.5f, 0.5f, 0.52f, 1f),
        WreckColour = new(0.85f, 0.85f, 0.8f, 0.8f);

    /// <summary>A ship seen going down: where it was last, its name, the encounter it was in, and how and since when it's
    /// lost.</summary>
    record struct Going(Vec3d At, string Name, bool Own, int Encounter, long Ms, long Seen, LossKind Loss, long LossMs);

    /// <summary>Where a ship went down, as the player's side knows it.</summary>
    record struct Wreck(Vec3d At, string Name, bool Own, int Encounter, long Ms, LossKind Loss, long LossMs);

    readonly Dictionary<int, Going> going = [];
    readonly List<Wreck> wrecks = [];
    readonly HashSet<int> wrecked = [];
    readonly List<int> goneNow = [];

    void DrawDamage(in MapView view, Snapshot s)
    {
        long now = world.DrawMs;
        Fires(view, s.Ships, now, s);
        Fires(view, s.Sighted, now, s);
        Watch(s.Ships, own: true, now);
        Watch(s.Sighted, own: false, now);

        // the gone: own ships out of the snapshot, enemies whose contacts say they were seen to go
        goneNow.Clear();
        foreach (var (e, g) in going)
        {
            if (g.Seen == frame)
                continue;
            bool sunk = g.Own ? s.Ships.IndexOf(e) < 0 : SeenGone(s.Contacts, e);
            if (sunk && wrecked.Add(e))
                wrecks.Add(new Wreck(g.At, g.Name, g.Own, g.Encounter, now, g.Loss, g.LossMs));
            goneNow.Add(e);
        }
        foreach (int e in goneNow)
            going.Remove(e);
        // a magazine's blast outlives the ship: from where she went
        double windDeg = s.Weather.WindFromDeg + 180;
        foreach (var g in going.Values)
            if (g.Loss == LossKind.Magazine)
                Magazine(view, g.At, (now - g.LossMs) / 1000.0, s, windDeg);
        foreach (var w in wrecks)
            if (w.Loss == LossKind.Magazine)
                Magazine(view, w.At, (now - w.LossMs) / 1000.0, s, windDeg);

        const double r = 8;   // pixels, at any zoom
        foreach (var w in wrecks)
        {
            if (!view.Project(w.At, out double x, out double y) || x < -20 || y < -20 || x > view.Width + 20 || y > view.Height + 20)
                continue;
            overlay.BeginStrip(WreckColour, 1.5f);
            for (int k = 0; k <= 12; k++)
                overlay.Point(x + Math.Cos(k * Math.PI / 6) * r, y + Math.Sin(k * Math.PI / 6) * r);
            overlay.End();
            foreach (double a in (ReadOnlySpan<double>)[Math.PI / 4, -Math.PI / 4])
            {
                overlay.BeginStrip(WreckColour, 1.5f);
                overlay.Point(x - Math.Cos(a) * r * 0.7, y - Math.Sin(a) * r * 0.7);
                overlay.Point(x + Math.Cos(a) * r * 0.7, y + Math.Sin(a) * r * 0.7);
                overlay.End();
            }
        }
    }

    /// <summary>Whether the player's navy saw a ship go (its contact says so).</summary>
    static bool SeenGone(ContactsView c, int entity)
    {
        for (int i = 0; i < c.Count; i++)
            if (c.Entity[i] == entity)
                return (c.Seen[i] & SeenFlags.Gone) != 0;
        return false;
    }

    /// <summary>The ships of a view going down, remembered where they are each frame until they're gone.</summary>
    void Watch(ShipsView v, bool own, long now)
    {
        for (int k = 0; k < v.Count; k++)
        {
            if (v.Loss[k] == LossKind.None)
                continue;
            int e = v.Entity[k];
            var (p, _) = v.PoseAt(k, now);
            string name = world.NameOf(e) ?? (world.Classes.TryGet(v.Class[k])?.Name ?? $"#{e}");
            going[e] = new Going(p, name, own, v.Encounter[k], now, frame, v.Loss[k], v.LossMs[k]);
        }
    }

    /// <summary>Each ship's damage as the table shows it on the icon (docs/visuals/battle-marks.md M4), from what an
    /// observer sees: a dark scorch at each part out of action or wrecked, where it is along her length (on the icon
    /// only: a sprite has its own); a flickering glow at each burning space and part and one smoke streak drifting
    /// downwind from them, longer and darker the more is burning; a magazine's blast; a ring round a ship going down.</summary>
    void Fires(in MapView view, ShipsView v, long now, Snapshot s)
    {
        double icon = 1 - WorldView.Factor(view), mpp = view.MetresPerPixel;
        var wind = s.Weather.WindFromDeg + 180;   // smoke goes downwind
        for (int k = 0; k < v.Count; k++)
        {
            int parts = v.PartCount[k], fires = v.FireCount[k];
            bool lost = v.Loss[k] != LossKind.None;
            if (parts == 0 && fires == 0 && !lost || classOf(v.Class[k]) is not { } c)
                continue;
            var (p, f) = v.PoseAt(k, now);
            if (!view.Project(p, out double cx, out double cy) || cx < -80 || cy < -80 || cx > view.Width + 80 || cy > view.Height + 80)
                continue;
            var frame = ShipFrame(view, p, f, c, icon);
            int seed = v.Entity[k] * 31;

            if (icon > 0)
                for (int j = v.PartStart[k]; j < v.PartStart[k] + parts; j++)
                {
                    var st = v.PartStates[j];
                    if (st == PartState.Ok)
                        continue;
                    var b = c.Components[v.Parts[j]].Bounds;
                    var (x, y) = frame.At((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2);
                    float a = st == PartState.Destroyed ? 0.85f : st == PartState.Disabled ? 0.6f : 0.3f;
                    overlay.Disc(x, y, ScorchPx, ScorchColour with { W = a * (float)icon }, 0.6f);
                }

            // the fires: a glow at each, the smoke from where they are on the whole
            double fx = 0, fy = 0;
            int burning = 0;
            for (int j = v.FireStart[k]; j < v.FireStart[k] + fires; j++)
            {
                var b = c.Cells[v.FireCells[j]].Box;
                Flame(frame.At((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2), now, seed + j, ref fx, ref fy, ref burning);
            }
            for (int j = v.PartStart[k]; j < v.PartStart[k] + parts; j++)
                if ((v.PartFlags[j] & PartFlags.Burning) != 0)
                {
                    var b = c.Components[v.Parts[j]].Bounds;
                    Flame(frame.At((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2), now, seed + 7 + j, ref fx, ref fy, ref burning);
                }
            if (burning > 0)
                Smoke(view, p, fx / burning, fy / burning, wind, burning, now, seed, mpp);

            if (lost)
                GoingDown(v, k, cx, cy, now);
        }
    }

    static readonly Vector4 ScorchColour = new(0.12f, 0.1f, 0.09f, 1f), FlameCore = new(1f, 0.9f, 0.55f, 1f);
    // a scorch on the icon, px; a fire's glow; its smoke's reach on the table, px, and in the world, m
    const double ScorchPx = 2.6, FlamePx = 5, SmokePx = 40, SmokeM = 1500;

    /// <summary>Where a ship's own points show: on its icon (its length and beam made the icon's) or, in the world, its
    /// hull; by the view's mix of the two.</summary>
    readonly record struct Frame(double X, double Y, double Fx, double Fy, double Sx, double Sy)
    {
        public (double, double) At(double along, double across) => (X + Fx * along + Sx * across, Y + Fy * along + Sy * across);
    }

    static Frame ShipFrame(in MapView view, Vec3d p, Vec3d f, ShipClass c, double icon)
    {
        var s = Vec3d.Cross(f, p.Normalized());
        view.Project(p, out double x, out double y);
        view.Project(p + f * 100, out double ax, out double ay);
        view.Project(p + s * 100, out double bx, out double by);
        // px per ship metre along and across, true; and the icon's
        double fx = (ax - x) / 100, fy = (ay - y) / 100, sx = (bx - x) / 100, sy = (by - y) / 100;
        double fl = Math.Max(Math.Sqrt(fx * fx + fy * fy), 1e-9), sl = Math.Max(Math.Sqrt(sx * sx + sy * sy), 1e-9);
        double ia = ShipDrawList.IconLengthPx / c.Length / fl, ib = ShipDrawList.IconWidthPx * 0.8 / c.Beam / sl;
        double ka = 1 + icon * (ia - 1), kb = 1 + icon * (ib - 1);
        return new Frame(x, y, fx * ka, fy * ka, sx * kb, sy * kb);
    }

    /// <summary>A fire's flickering glow and its hot core; adds where it is to the ship's fires' sum.</summary>
    void Flame((double X, double Y) at, long now, int seed, ref double sumX, ref double sumY, ref int n)
    {
        if (n < MaxFlames)   // past a few, more glows only pile up into a blot; they still feed the smoke
        {
            double flicker = 0.7 + 0.3 * Math.Sin(now / 90.0 + seed * 1.7) * Math.Sin(now / 37.0 + seed);
            overlay.Disc(at.X, at.Y, FlamePx * (0.8 + 0.3 * flicker), FlameColour with { W = (float)(0.55 * flicker + 0.15) });
            overlay.Disc(at.X, at.Y, FlamePx * 0.4, FlameCore with { W = (float)flicker });
        }
        (sumX, sumY, n) = (sumX + at.X, sumY + at.Y, n + 1);
    }

    const int MaxFlames = 4;

    /// <summary>A ship's smoke: one streak from its fires drifting downwind, widening and fading to nothing, with a slow
    /// meander; longer and darker the more is burning.</summary>
    void Smoke(in MapView view, Vec3d ship, double x, double y, double headingDeg, int fires, long now, int seed, double mpp)
    {
        double reachPx = Math.Max(SmokeM / mpp, SmokePx) * (0.6 + 0.4 * Math.Min(fires, 6) / 6.0);
        if (!view.Project(ship, out double ox, out double oy) || !view.Project(ship + Earth.Forward(ship, headingDeg) * (reachPx * mpp), out double tx, out double ty))
            return;
        double dx = tx - ox, dy = ty - oy, len = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1e-6);
        (dx, dy) = (dx / len * reachPx, dy / len * reachPx);
        double nx = -dy / reachPx, ny = dx / reachPx;
        float dark = (float)(0.45 + 0.3 * Math.Min(fires, 6) / 6.0);
        const int N = 7;
        for (int i = 0; i <= N; i++)
        {
            double u = i / (double)N, wob = Math.Sin(now / 1400.0 + u * 4 + seed) * u * reachPx * 0.08;
            overlay.Point(x + dx * u + nx * wob, y + dy * u + ny * wob, SmokeColour with { W = (float)(dark * (1 - u) * Math.Min(1, u * 6 + 0.3)) },
                (float)(3 + 12 * u));
        }
        overlay.End();
    }

    /// <summary>A ship going down: a pale ring of foam and wreckage widening round her until she's gone.</summary>
    void GoingDown(ShipsView v, int k, double x, double y, long now)
    {
        double age = (now - v.LossMs[k]) / 1000.0, span = Math.Max((v.GoneMs[k] - v.LossMs[k]) / 1000.0, 1);
        if (age < 0)
            return;
        double gone = Math.Clamp(age / span, 0, 1);
        Ring(x, y, ShipDrawList.IconLengthPx * (0.55 + 0.45 * gone), WreckColour with { W = (float)(0.4 * Math.Min(1, age / 2) * (1 - gone * gone)) }, 1.5f);
    }

    /// <summary>A magazine's blast where a ship was, <paramref name="age"/> seconds after: a white-hot flash with sparks,
    /// its shock ring, and a dark pall drifting off downwind and spreading. Faded out where the world view draws the
    /// explosion itself (docs/visuals/magazine-explosions.md).</summary>
    void Magazine(in MapView view, Vec3d at, double age, Snapshot s, double windDeg)
    {
        float table = Magazines.MagazineLook.On ? (float)(1 - WorldView.Factor(view)) : 1f;
        if (table <= 0 || age < 0 || age > MagazinePallS || !view.Project(at, out double x, out double y) || x < -100 || y < -100 || x > view.Width + 100 || y > view.Height + 100)
            return;
        double mpp = view.MetresPerPixel, drift = age / MagazinePallS;
        if (view.Project(at + Earth.Forward(at, windDeg) * (Math.Max(SmokeM / mpp, SmokePx) * mpp * drift), out double px, out double py))
            overlay.Disc(px, py, MagazinePallPx * (0.4 + 0.6 * Math.Sqrt(drift)), PallColour with { W = (float)(0.75 * (1 - drift) * Math.Min(1, age / 1.5)) * table });
        Hit(x, y, age, 1.8, Floor(s, MagazineFlashS), Floor(s, 1.5), Seed(at), HitPx * 1.6, 0, table);
        double shock = Floor(s, 2);
        if (age < shock)
            Ring(x, y, MagazineRingPx * Math.Sqrt(age / shock), HitCore with { W = (float)(1 - age / shock) * table }, 2f);
    }

    static readonly Vector4 PallColour = new(0.4f, 0.38f, 0.37f, 1f);
    // a magazine: its flash, its shock ring's reach, px, and its pall: how long and how big, px
    const double MagazineFlashS = 3, MagazineRingPx = 45, MagazinePallS = 90, MagazinePallPx = 30;

    /// <summary>An encounter's losses as the player's side knows them: its own, and the enemy's seen to go.</summary>
    string LossesIn(int encounter)
    {
        int ours = 0, theirs = 0;
        foreach (var w in wrecks)
            if (w.Encounter == encounter)
            {
                if (w.Own)
                    ours++;
                else
                    theirs++;
            }
        return ours + theirs == 0 ? "" : $" · lost {ours}, seen sunk {theirs}";
    }

    /// <summary>The wrecks' names, when there's room on the screen for them.</summary>
    void WreckLabels()
    {
        if (wrecks.Count == 0 || camera.MetresPerPixel > 60)
            return;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        foreach (var w in wrecks)
        {
            if (!view.Project(w.At, out double x, out double y))
                continue;
            string text = $"{w.Name} · sunk {new WorldTime(w.Ms).ToUtc():HH:mm}";
            var at = new Vector2(vp.Pos.X + (float)(x * sx) + 10, vp.Pos.Y + (float)(y * sy) + 4);
            dl.AddText(at + new Vector2(1, 1), 0xC0000000, text);
            dl.AddText(at, ImGui.GetColorU32(WreckColour), text);
        }
    }

    /// <summary>Why a captain does what he does, as words (docs/battle/command.md "Reports and reasons"); "" for none.</summary>
    static string ReasonText(byte reason) => (CaptainReason)reason switch
    {
        CaptainReason.OutSpeed => "falling out: too slow",
        CaptainReason.OutGuns => "falling out: main guns out",
        CaptainReason.OutList => "falling out: listing",
        CaptainReason.OutWater => "falling out: flooding",
        CaptainReason.OutSteering => "falling out: steering gone",
        CaptainReason.OutAmmunition => "out of ammunition, home",
        CaptainReason.Home => "out of the fight, home",
        CaptainReason.Combing => "combing a torpedo's track",
        _ => "",
    };

    /// <summary>The player's ships' reasons (subordinates explain themselves: command-ui.md), beside each, when there's
    /// room on the screen for them.</summary>
    void ReasonLabels(Snapshot s)
    {
        if (camera.MetresPerPixel > 60)
            return;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
        {
            if (v.Reason[k] == 0 || !view.Project(v.PoseAt(k, world.DrawMs).Position, out double x, out double y))
                continue;
            string text = ReasonText(v.Reason[k]);
            var at = new Vector2(vp.Pos.X + (float)(x * sx) + 10, vp.Pos.Y + (float)(y * sy) - 16);
            dl.AddText(at + new Vector2(1, 1), 0xC0000000, text);
            dl.AddText(at, ImGui.GetColorU32(FlameColour), text);
        }
    }

    /// <summary>What a contact's observers make out of its state, as its label adds it.</summary>
    static string SeenText(SeenFlags f)
    {
        if (f == SeenFlags.None)
            return "";
        if ((f & SeenFlags.Gone) != 0)
            return " · seen to go down";
        if ((f & SeenFlags.GoingDown) != 0)
            return " · going down";
        string t = "";
        if ((f & SeenFlags.Burning) != 0)
            t += " · burning";
        if ((f & SeenFlags.Low) != 0)
            t += " · low in the water";
        if ((f & SeenFlags.Listing) != 0)
            t += " · listing";
        if ((f & SeenFlags.ByBow) != 0)
            t += " · down by the bow";
        if ((f & SeenFlags.ByStern) != 0)
            t += " · down by the stern";
        if ((f & SeenFlags.Stopped) != 0)
            t += " · stopped";
        if ((f & SeenFlags.Silent) != 0)
            t += " · guns silent";
        return t;
    }
}
