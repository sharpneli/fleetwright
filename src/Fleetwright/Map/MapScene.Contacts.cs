using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The player's picture on the map (docs/battle/plan.md step 3.2e; docs/map-ui/README.md "Uncertainty on the
/// map"): each contact where their navy reckons it, a marker and its course line, inside its error ellipse (2 sigma),
/// in its side's colour once its type is made out; solid while in sight, fading once it's out of sight and dead reckoned.
/// Its label says what it's taken for: a ship, its type, its class (with a "?": it may be wrong), its name. Smoke on the
/// horizon (docs/battle/smoke.md Z6) is a smudge where it's reckoned, with the bearing it was taken on while it's in
/// sight: no ship's marker or course until she's seen. A light in the dark (docs/battle/night.md Q2: a salvo's flash, a
/// funnel's glow) is a small spark where it's reckoned, with its bearing while it's in sight (placeholder marks until the
/// night's UI, Q9).</summary>
public sealed partial class MapScene
{
    const double ContactFadeMs = 30 * 60_000;   // out of sight, it fades to its faintest over this
    const double ContactCourseS = 300;          // the course line: five minutes' steaming
    const float ContactMarkerPx = 5;

    static Vector4 ContactColour(ContactsView c, int i, long now)
    {
        var col = c.Side[i] == 255 ? MapPalette.Unknown : ShipDrawList.EnemyTint;   // the player's picture: the enemy's
        float a = c.InSight[i] || c.SmokeInSight[i] || c.LightInSight[i] ? 0.95f : (float)Math.Max(0.3, 0.85 - 0.55 * (now - c.SeenMs[i]) / ContactFadeMs);
        return col with { W = a };
    }

    void DrawContacts(in MapView view, Snapshot s)
    {
        var c = s.Contacts;
        long now = world.DrawMs;
        for (int i = 0; i < c.Count; i++)
        {
            var p = c.Position[i];
            var col = ContactColour(c, i, now);
            // the ellipse, 2 sigma, at least a few pixels across
            double min = 6 * view.MetresPerPixel;
            double major = Math.Max(2 * c.MajorM[i], min), minor = Math.Max(2 * c.MinorM[i], min);
            var ax = Earth.Forward(p, c.AxisDeg[i]);
            var up = p.Normalized();
            var across = Vec3d.Cross(up, ax);
            int segs = overlay.Segments(major / view.MetresPerPixel);
            overlay.BeginStrip(col with { W = col.W * 0.6f }, 1f);
            for (int k = 0; k <= segs; k++)
            {
                double a = k * Math.PI * 2 / segs;
                ClipPoint(view, (p + ax * (Math.Cos(a) * major) + across * (Math.Sin(a) * minor)).Normalized() * Earth.Radius);
            }
            overlay.EndClosed();
            if (c.SmokeInSight[i] || c.LightInSight[i])
            {
                // the bearing it was taken on: from where she looked, through it, out past its range's error
                var far = (p + (p - c.From[i]).Normalized() * (2 * c.MajorM[i])).Normalized() * Earth.Radius;
                if (view.Project(c.From[i], out double fx, out double fy) && view.Project(far, out double tx, out double ty))
                {
                    overlay.BeginStrip(col with { W = col.W * 0.45f }, 1f);
                    overlay.Point(fx, fy);
                    overlay.Point(tx, ty);
                    overlay.End();
                }
            }
            if (!view.Project(p, out double x, out double y))
                continue;
            if (c.SmokeInSight[i] || c.Level[i] == ContactLevel.Smoke)
            {
                double r = Math.Clamp(2 * c.MinorM[i] / view.MetresPerPixel, 28, 64);
                overlay.Disc(x, y, r, MapPalette.Smoke with { W = MapPalette.Smoke.W * col.W }, 0.7f);
            }
            if (c.LightInSight[i] || c.Level[i] == ContactLevel.Light)
                Spark(x, y, MapPalette.Light with { W = MapPalette.Light.W * col.W });
            if (c.Level[i] <= ContactLevel.Smoke)
                continue;   // smoke or a light alone: no ship's marker, no course
            // the marker: a diamond, filled while she's in sight, hollow once dead reckoned; an unknown (made out, not known
            // for friend or foe: docs/battle/night.md Q6, Q9) APP-6's quatrefoil; and the course line ahead
            symbols.Add(c.Side[i] == 255 ? SymbolShape.Quatrefoil : SymbolShape.Diamond, x, y, ContactMarkerPx * Rp,
                c.InSight[i] ? col with { W = col.W * 0.85f } : default, col, 1.5f);
            var ahead = (p + c.Velocity[i] * ContactCourseS).Normalized() * Earth.Radius;
            if (view.Project(ahead, out double ax2, out double ay2) && Math.Abs(ax2 - x) + Math.Abs(ay2 - y) > 2)
            {
                overlay.BeginStrip(col, 1.2f);
                overlay.Point(x, y);
                overlay.Point(ax2, ay2);
                overlay.End();
            }
        }
    }

    /// <summary>The night ticks (docs/visuals/night-lights.md decision 1): in the world view at night, four corner ticks
    /// about each ship the player knows of, his own and the contacts in sight (at the contact's place, never the truth),
    /// a few pixels whatever the zoom, so a near-black night still shows where the ships are. They fade in with the dark
    /// (the night eye's light, over the rods' range: none by day, full on a moonless night) and with the world's share;
    /// table marks: they light nothing and the haze doesn't take them.</summary>
    void DrawNightTicks(in MapView view, Snapshot s)
    {
        double inWorld = WorldView.Factor(view);
        if (inWorld <= 0)
            return;
        var t = LightTable.Current;
        double lux = Math.Log10(Math.Max(shipRenderer.List.Lights.Lux, 1e-8));
        double k = Math.Clamp((lux - t.PurkinjeLogLux[0]) / (t.PurkinjeLogLux[1] - t.PurkinjeLogLux[0]), 0, 1);
        float dark = (float)(inWorld * k * k * (3 - 2 * k));
        if (dark <= 0.01f)
            return;
        var v = s.Ships;
        for (int i = 0; i < v.Count; i++)
        {
            var (p, f) = v.PoseAt(i, world.DrawMs);
            if (!view.Project(p, out double x, out double y) || !view.Project((p + f * 10).Normalized() * Earth.Radius, out double fx, out double fy))
                continue;
            double len = IconLengthAt(view, v.Class[i], p), beam = (classOf(v.Class[i]) is { } cls ? cls.Beam / cls.Length : 0.12) * len;
            Ticks(x, y, fx - x, fy - y, len, beam, ShipDrawList.FriendTint with { W = 0.8f * dark });
        }
        var c = s.Contacts;
        for (int i = 0; i < c.Count; i++)
            if (c.InSight[i] && c.Level[i] > ContactLevel.Smoke && view.Project(c.Position[i], out double x, out double y))
                Ticks(x, y, 1, 0, NightTickContactPx * Rp, NightTickContactPx * Rp, ContactColour(c, i, world.DrawMs) with { W = 0.8f * dark });
    }

    const double NightTickContactPx = 18;   // a contact's ticks: a square a few pixels round its marker (its size unknown)

    /// <summary>Four corner ticks about a ship <paramref name="lengthPx"/> by <paramref name="beamPx"/> at x, y, her bow
    /// toward (dx, dy) on the screen.</summary>
    void Ticks(double x, double y, double dx, double dy, double lengthPx, double beamPx, Vector4 col)
    {
        double n = Math.Sqrt(dx * dx + dy * dy);
        (dx, dy) = n > 1e-9 ? (dx / n, dy / n) : (1, 0);
        double a = 0.5 * lengthPx + 5 * Rp, b = Math.Max(0.5 * beamPx + 5 * Rp, 7 * Rp);
        double armA = Math.Clamp(0.25 * a, 3 * Rp, 9 * Rp), armB = Math.Clamp(0.6 * b, 3 * Rp, 9 * Rp);
        for (int q = 0; q < 4; q++)
        {
            double sa = q % 2 == 0 ? -1 : 1, sb = q < 2 ? -1 : 1;
            overlay.BeginStrip(col, 1.3f);
            Corner(sa * (a - armA), sb * b);
            Corner(sa * a, sb * b);
            Corner(sa * a, sb * (b - armB));
            overlay.End();
        }

        void Corner(double u, double w) => overlay.Point(x + dx * u - dy * w, y + dy * u + dx * w);
    }

    const double BeamDrawnM = 4000;   // a beam drawn out to about its reach
    const double BeamMarkHalfDeg = 0.9;   // the table's wedge, either side of its axis (wider than the beam, to be seen)
    const double FlareLitLux = 1;     // a flare's patch on the table: the sea it lights to a lux or more [EST]

    /// <summary>A light seen in the dark (a gun's flash, a lamp, a fire): a spark of eight short rays.</summary>
    void Spark(double x, double y, Vector4 col)
    {
        for (int k = 0; k < 8; k++)
        {
            double a = k * Math.PI / 4, r0 = 2 * Rp, r1 = (k % 2 == 0 ? 7.5 : 5) * Rp;
            overlay.BeginStrip(col, 1.6f);
            overlay.Point(x + Math.Cos(a) * r0, y + Math.Sin(a) * r0);
            overlay.Point(x + Math.Cos(a) * r1, y + Math.Sin(a) * r1);
            overlay.End();
        }
    }

    /// <summary>The searchlights' beams out (docs/battle/night.md Q4, Q9). On the table, a mark: a narrow wedge of the
    /// light's colour from the lamp out to about its reach, fading along it, and a dashed ring about the ship one of
    /// the player's ships holds hers on (lit), faded out over the world's boundary, where the beam itself shows
    /// (docs/visuals/night-lights.md Q10c).</summary>
    void DrawBeams(in MapView view, Snapshot s)
    {
        var b = s.Beams;
        double table = 1 - WorldView.Factor(view);
        if (table <= 0)
            return;
        for (int i = 0; i < b.Count; i++)
        {
            var lamp = b.Lamp[i].Normalized() * Earth.Radius;
            var (end, along) = (lamp, Earth.Forward(lamp, b.BearingDeg[i]));
            Earth.Advance(ref end, ref along, BeamDrawnM);
            if (!view.Project(lamp, out double x0, out double y0) || !view.Project(end, out double x1, out double y1))
                continue;
            // the wedge: its width grows with the distance along it, its light fades out to the far end
            double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)), t = Math.Tan(BeamMarkHalfDeg * Math.PI / 180);
            overlay.BeginStrip(MapPalette.Light, 1);
            foreach (var (share, alpha) in BeamStops)
                overlay.Point(x0 + (x1 - x0) * share, y0 + (y1 - y0) * share, MapPalette.Light with { W = (float)(alpha * table) },
                    (float)((2 * len * share * t + 3) / Rp));
            overlay.End();
        }
        // lit: the ships the player's beams are held on
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
            if (v.BeamOn[k] > 0 && Where(s, v.BeamOn[k]) is { } at && view.Project(at, out double lx, out double ly))
            {
                if (v.IndexOf(selected) == k && view.Project(v.PoseAt(k, world.DrawMs).Position, out double ox, out double oy))
                {
                    // the selection's beam: a thin line to the ship it's held on
                    overlay.BeginStrip(MapPalette.Light with { W = (float)(0.7 * table) }, 1.2f);
                    overlay.Point(ox, oy);
                    overlay.Point(lx, ly);
                    overlay.End();
                }
                overlay.Circle(lx, ly, 15 * Rp, MapPalette.Light with { W = (float)(0.9 * table) }, 1.4f, Dash.Dashed);
            }
    }

    static readonly (double Share, double Alpha)[] BeamStops = [(0, 0.34), (0.3, 0.12), (1, 0)];

    /// <summary>Where the player holds a ship: one of hers, or a contact; null if neither.</summary>
    Vec3d? Where(Snapshot s, int entity)
    {
        if (s.Ships.IndexOf(entity) is int k and >= 0)
            return s.Ships.PoseAt(k, world.DrawMs).Position;
        return ContactIndex(s.Contacts, entity) is int i and >= 0 ? s.Contacts.Position[i] : null;
    }

    /// <summary>The star shells' flares burning (docs/battle/night.md Q5, Q9). On the table, a mark: a four-pointed star
    /// where each burns and the sea it lights as a faint disc (its edge where its light falls to <see cref="FlareLitLux"/>),
    /// a dashed ring about each ship under it. In the world view, a small disc until the night's look (Q10).</summary>
    void DrawFlares(in MapView view, Snapshot s)
    {
        var f = s.Flares;
        double inWorld = WorldView.Factor(view), table = 1 - inWorld;
        for (int i = 0; i < f.Count; i++)
        {
            var under = f.At[i].Normalized() * Earth.Radius;
            if (!view.Project(under, out double x, out double y))
                continue;
            if (inWorld > 0)
                overlay.Disc(x, y, 5 * Rp, MapPalette.Light with { W = (float)(0.85 * inWorld) }, 1f);
            if (table <= 0)
                continue;
            double h = f.At[i].Length - Earth.Radius;
            double litM = Math.Sqrt(Math.Max(f.Candela[i] / FlareLitLux - h * h, 0));
            double r = litM / view.MetresPerPixel;
            overlay.Disc(x, y, r, MapPalette.Light with { W = (float)(0.08 * table) }, 0.9f);
            overlay.Circle(x, y, r, MapPalette.Light with { W = (float)(0.3 * table) }, 1);
            Star(x, y, MapPalette.Light with { W = (float)(0.95 * table) });
            // lit: every ship of the player's and every contact under it
            var v = s.Ships;
            for (int k = 0; k < v.Count; k++)
                if (Earth.Distance(v.PoseAt(k, world.DrawMs).Position, under) <= litM && view.Project(v.PoseAt(k, world.DrawMs).Position, out double sx, out double sy))
                    overlay.Circle(sx, sy, 15 * Rp, MapPalette.Light with { W = (float)(0.9 * table) }, 1.4f, Dash.Dashed);
            var c = s.Contacts;
            for (int j = 0; j < c.Count; j++)
                if (c.Level[j] > ContactLevel.Smoke && Earth.Distance(c.Position[j], under) <= litM && view.Project(c.Position[j], out double cx, out double cy))
                    overlay.Circle(cx, cy, 15 * Rp, MapPalette.Light with { W = (float)(0.9 * table) }, 1.4f, Dash.Dashed);
        }
    }

    /// <summary>Star shell… armed over the sea (docs/battle/night.md Q9): the patch its flare would light there, ghosted
    /// (dashed), by the brightest star shell the selected ship carries.</summary>
    void StarPreview(in MapView view, Snapshot s)
    {
        if (armedPick != ClickPick.StarShell || !mouseIn || ImGui.GetIO().WantCaptureMouse || s.Ships.IndexOf(selected) is not (>= 0 and var k))
            return;
        var (enemy, own, sea, at) = Under(s, cursorX, cursorY);
        if (enemy > 0 || own > 0 || !sea || world.Shore.IsLand(at) || s.Ships.Loadout[k] is not { } lo)
            return;
        double cd = 0;
        foreach (var t in lo.ShellTypes)
            if (t.Kind == ShellKind.Star)
                cd = Math.Max(cd, t.StarCd);
        double h = world.Settings.StarBurstM, litM = Math.Sqrt(Math.Max(cd / FlareLitLux - h * h, 0));
        if (litM <= 0 || !view.Project(at, out double x, out double y))
            return;
        overlay.Circle(x, y, litM / view.MetresPerPixel, MapPalette.Light with { W = 0.45f }, 1, Dash.Dashed);
    }

    /// <summary>A flare: a four-pointed star, its rays tapering from the middle.</summary>
    void Star(double x, double y, Vector4 col)
    {
        for (int k = 0; k < 4; k++)
        {
            double a = k * Math.PI / 2;
            overlay.BeginStrip(col, 3.5f);
            overlay.Point(x, y, col, 3.5f);
            overlay.Point(x + Math.Cos(a) * 10 * Rp, y + Math.Sin(a) * 10 * Rp, col, 0.4f);
            overlay.End();
        }
        overlay.Disc(x, y, 6 * Rp, col with { W = col.W * 0.6f }, 0.5f);
    }

    /// <summary>What a contact is taken for, as its label says it.</summary>
    // the contacts' identities as the player holds them, made when one changes
    readonly Dictionary<(int, ContactLevel, int, ShipType), string> identities = [];

    /// <summary>A contact's identity as the player holds it (docs/map-ui/battle-hud.md U4): her name once identified,
    /// her class with a doubt once made out, her type, a ship, smoke.</summary>
    string Identity(ContactsView c, int i)
    {
        var key = (c.Entity[i], c.Level[i], c.TakenFor[i], c.Type[i]);
        if (identities.TryGetValue(key, out var text))
            return text;
        if (identities.Count > 2048)
            identities.Clear();
        return identities[key] = c.Level[i] switch
        {
            ContactLevel.Name => world.NameOf(c.Entity[i]) ?? ClassName(c.TakenFor[i]),
            ContactLevel.Class => ClassName(c.TakenFor[i]) + " class?",
            ContactLevel.Type => TypeWord(c.Type[i]),
            ContactLevel.Ship => "ship",
            ContactLevel.Smoke => "smoke",
            _ => "a light",
        };
    }

    static string TypeWord(ShipType t) => t switch
    {
        ShipType.Capital => "heavy ship",
        ShipType.Cruiser => "cruiser",
        ShipType.Destroyer => "destroyer",
        ShipType.Small => "small craft",
        ShipType.Merchant => "merchant",
        _ => "ship",
    };

    /// <summary>The row of a contact (an entity) in the player's picture, or -1.</summary>
    static int ContactIndex(ContactsView c, int entity)
    {
        for (int i = 0; i < c.Count; i++)
            if (c.Entity[i] == entity)
                return i;
        return -1;
    }

    string ContactText(ContactsView c, int i, long now)
    {
        string what = Identity(c, i);
        what += SeenText(c.Seen[i]);
        if (c.SmokeInSight[i] && c.Level[i] > ContactLevel.Smoke)
            what += " · her smoke";
        if (c.LightInSight[i] && c.Level[i] > ContactLevel.Light)
            what += " · her lights";
        return c.InSight[i] || c.SmokeInSight[i] || c.LightInSight[i] ? what : $"{what} · last seen {new WorldTime(c.SeenMs[i]).ToUtc():HH:mm}";
    }

    string ClassName(int id) => id >= 0 && world.Classes.TryGet(id) is { } cls ? cls.Name : "?";

    // the contacts' labels, made when what they say changes
    readonly Dictionary<int, ((int, ContactLevel, int, ShipType, SeenFlags, bool, bool, long) Key, string Text)> contactTexts = [];

    void ContactLabels(Snapshot s)
    {
        var c = s.Contacts;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        long now = world.DrawMs;
        for (int i = 0; i < c.Count; i++)
        {
            if (!view.Project(c.Position[i], out double x, out double y))
                continue;
            var size = ImGui.CalcTextSize(ContactLabel(c, i, now));
            mapLabels.Add(LabelKind.Contact, c.Entity[i], i, c.InSight[i] || c.SmokeInSight[i] ? 2 : 4, vp.Pos.X + (float)(x * sx), vp.Pos.Y + (float)(y * sy),
                size.X, size.Y, 9 * sc, 6 * sc, LabelPlace.Right, LabelPlace.Left, LabelPlace.Below, LabelPlace.Above);
        }
    }

    /// <summary>A contact's label, made when what it says changes.</summary>
    string ContactLabel(ContactsView c, int i, long now)
    {
        var key = (c.Entity[i], c.Level[i], c.TakenFor[i], c.Type[i], c.Seen[i], c.SmokeInSight[i], c.InSight[i], c.InSight[i] ? 0 : c.SeenMs[i] / 60_000);
        if (!contactTexts.TryGetValue(c.Entity[i], out var cached) || cached.Key != key)
        {
            if (contactTexts.Count > 2048)
                contactTexts.Clear();
            contactTexts[c.Entity[i]] = cached = (key, ContactText(c, i, now));
        }
        return cached.Text;
    }

    void DrawContactLabel(Snapshot s, in MapLabels.Request r, Vector2 at, float alpha)
    {
        var c = s.Contacts;
        int i = r.Index;
        if (i >= c.Count || c.Entity[i] != (int)(r.Key & 0xFFFFFFFF))
            return;
        long now = world.DrawMs;
        MapLabels.Text(ImGui.GetBackgroundDrawList(), at, ContactColour(c, i, now), ContactLabel(c, i, now), alpha);
    }
}
