using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Damage seen on the map (docs/battle/fighting.md 4.4), in the map's style (decision 10: simple markers until
/// the smoke field and the VFX pass): fires as a flickering flame with smoke drifting off it, at each burning space and
/// part of the ships shown (own, and enemies in sight: what an observer sees); ships going down watched until they're
/// gone, then a wreck marker where they went (own ships always; an enemy's only when it was seen to go); each
/// encounter's losses as the player's side knows them.</summary>
public sealed partial class MapScene
{
    static readonly Vector4 FlameColour = new(1f, 0.55f, 0.12f, 0.95f), SmokeColour = new(0.2f, 0.2f, 0.2f, 0.35f),
        WreckColour = new(0.85f, 0.85f, 0.8f, 0.8f);

    /// <summary>A ship seen going down: where it was last, its name, and the encounter it was in.</summary>
    record struct Going(Vec3d At, string Name, bool Own, int Encounter, long Ms, long Seen);

    /// <summary>Where a ship went down, as the player's side knows it.</summary>
    record struct Wreck(Vec3d At, string Name, bool Own, int Encounter, long Ms);

    readonly Dictionary<int, Going> going = [];
    readonly List<Wreck> wrecks = [];
    readonly HashSet<int> wrecked = [];
    readonly List<int> goneNow = [];

    void DrawDamage(in MapView view, Snapshot s)
    {
        long now = world.DrawMs;
        Fires(view, s.Ships, now);
        Fires(view, s.Sighted, now);
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
                wrecks.Add(new Wreck(g.At, g.Name, g.Own, g.Encounter, now));
            goneNow.Add(e);
        }
        foreach (int e in goneNow)
            going.Remove(e);

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
            going[e] = new Going(p, name, own, v.Encounter[k], now, frame);
        }
    }

    /// <summary>Each burning space and part of a view's ships: a flame, and its smoke drifting off downwind (a fixed
    /// north-easterly until the weather has a wind), flickering.</summary>
    void Fires(in MapView view, ShipsView v, long now)
    {
        double mpp = view.MetresPerPixel;
        for (int k = 0; k < v.Count; k++)
        {
            int parts = v.PartCount[k], fires = v.FireCount[k];
            if (parts == 0 && fires == 0 || classOf(v.Class[k]) is not { } c)
                continue;
            var (p, f) = v.PoseAt(k, now);
            var s = Vec3d.Cross(f, p.Normalized());
            bool small = c.Length / mpp < 40;   // an icon: one flame for the ship
            for (int j = v.FireStart[k]; j < v.FireStart[k] + fires; j++)
            {
                var b = c.Cells[v.FireCells[j]].Box;
                if (Flame(view, p + f * ((b.X0 + b.X1) / 2) + s * ((b.Y0 + b.Y1) / 2), now, v.Entity[k] * 31 + j) && small)
                    break;
            }
            for (int j = v.PartStart[k]; j < v.PartStart[k] + parts; j++)
                if ((v.PartFlags[j] & PartFlags.Burning) != 0)
                {
                    var b = c.Components[v.Parts[j]].Bounds;
                    if (Flame(view, p + f * ((b.X0 + b.X1) / 2) + s * ((b.Y0 + b.Y1) / 2), now, v.Entity[k] * 31 + j) && small)
                        break;
                }
        }
    }

    bool Flame(in MapView view, Vec3d at, long now, int seed)
    {
        if (!view.Project(at, out double x, out double y) || x < -50 || y < -50 || x > view.Width + 50 || y > view.Height + 50)
            return false;
        double mpp = view.MetresPerPixel, flicker = 0.75 + 0.25 * Math.Sin(now / 90.0 + seed * 1.7);
        double r = Math.Max(3 / mpp, 3) * flicker, smoke = Math.Max(60 / mpp, 12);
        // the smoke: a broad grey stroke drifting off to the north-east, widening
        var drift = Earth.Forward(at, 50);
        double reach = smoke * mpp;
        for (int i = 0; i < 3; i++)
        {
            var q = at + drift * (reach * (i + 1) / 3);
            if (!view.Project(q, out double qx, out double qy))
                break;
            overlay.BeginStrip(SmokeColour with { W = SmokeColour.W * (1 - i * 0.25f) }, (float)(r * (2 + i)));
            overlay.Point(x + (qx - x) * i / 3, y + (qy - y) * i / 3);
            overlay.Point(qx, qy);
            overlay.End();
        }
        overlay.BeginStrip(FlameColour, (float)r);   // a disc: a ring as wide as its radius
        for (int k = 0; k <= 10; k++)
            overlay.Point(x + Math.Cos(k * Math.PI / 5) * r / 2, y + Math.Sin(k * Math.PI / 5) * r / 2);
        overlay.End();
        return true;
    }

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
