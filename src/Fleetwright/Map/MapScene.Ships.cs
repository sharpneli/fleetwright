using Fleetwright.Map.Wakes;
using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The world tab's ships (docs/battle/plan.md step 2.3a): drawn by <see cref="ShipBatchRenderer"/> (sprites
/// close in, icons further out), selected by a click, ordered from a strip (course, telegraph) or by right-clicking the
/// sea (steer for it; with shift, add a waypoint). Routes, the selection and short tracks behind each ship are lines.
/// A dev panel spawns ships where you click, one or a group already in formation, of the designer's design (the
/// default) or a shipped one: the stand-in for missions placing forces; pressed and dragged, they head the way of the
/// drag, shown as they'd lie until the release places them. All of it
/// reads the snapshot and changes the world only by commands.</summary>
public sealed unsafe partial class MapScene
{
    readonly ShipArtLibrary art;
    readonly ShipBatchRenderer shipRenderer;
    readonly Func<int, ShipArt?> artOf;
    readonly Func<int, double> lengthOf;
    readonly Func<int, ShipClass?> classOf;
    readonly string[] designNames;
    int selected;   // the selected ship's entity, or -1
    bool horizonRing;   // dev: the selected ship's horizon from her highest eye
    bool devOpen;   // the dev panel (F1)

    /// <summary>The dev panel (F1) open at start (-dev).</summary>
    public bool DevOpen { init => devOpen = value; }
    double pressX, pressY;
    bool pressed;

    // the dev spawn: the design picked, its class id once requested, whether the next click places, and what it places:
    // how many (more than one a group, each ship on its station), the formation, heading and speed. From the designer,
    // the class follows the design: asked for again whenever it changes (a class is its design's hash, so an edit is a
    // new class and ships already out keep theirs), from the first Place on, so a world that never spawns builds none
    const int FromDesigner = -1;
    readonly DesignSession designer;
    int designPick = FromDesigner, placeClass = -1, placeVersion = -1;
    bool placing, keepPlacing;
    // a placement being aimed: pressed on the sea while armed, heading the way the cursor has been dragged
    bool placeDrag;
    Vec3d placeAt;
    double placeX, placeY, placeHeading;
    const double ClickPx = 5;   // a press that moves less than this is a click
    // the side is the player's too: a change sets it, and a new world sets the field to its player's (MapScene.Scenario)
    int spawnCount = 1, spawnFormation, spawnSpacing = 600, spawnHeading, spawnKn = 10, spawnSide, maxSide = 3;
    int courseEdit;
    bool hardOver;
    int followMetres = 800;

    static readonly int[] CourseSteps = [-90, -10, 10, 90];
    // each ship's trail (Wakes.WakeTrail): a line on the table, the foam lane in the world view (docs/visuals/wakes.md K4)
    readonly Dictionary<int, WakeTrail> tracks = [];
    readonly List<int> gone = [];
    long frame;

    static readonly Vector4 TrackColor = new(0.85f, 0.9f, 1f, 0.35f), RouteColor = new(1f, 0.85f, 0.4f, 0.55f),
        RouteSelected = new(1f, 0.8f, 0.3f, 0.95f), SelectColor = new(1f, 0.78f, 0.25f, 0.95f), CourseColor = new(1f, 1f, 1f, 0.6f), TargetColor = new(1f, 0.78f, 0.25f, 0.45f),
        LandRing = new(0.55f, 0.85f, 0.6f, 0.5f), TorpedoRing = new(0.63f, 0.84f, 1f, 0.3f), HorizonColor = new(0.6f, 0.85f, 1f, 0.6f), SlideColor = new(0.55f, 1f, 0.6f, 0.95f), PlaceColor = new(0.6f, 0.9f, 1f, 0.85f);

    // ------------------------------------------------------------------ lines over the globe

    void BuildOverlay(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        var cap = ViewCap(view);
        // every ship's route only from the operations band in (under 1,000 km across): further out, only the
        // selection's (2.7c: 1,750 tramps' routes over the Atlantic were a tangle, and 1.4 million vertices a frame)
        bool allRoutes = view.MetresPerPixel * view.Width < 1_000_000;
        overlay.Begin(width, height);
        overlay.WidthScale = overlay.ChordScale = Rp;
        frame++;
        DrawEncounters(view, s);
        DrawContacts(view, s);
        DrawShells(view, s);
        DrawDamage(view, s);
        for (int k = 0; k < v.Count; k++)
        {
            var (p, f) = v.PoseAt(k, world.DrawMs);
            int e = v.Entity[k];

            // the trail: sampled here, drawn last (TrackLines); at the stern, so the foam lane starts at the transom
            if (!tracks.TryGetValue(e, out var tr))
                tracks[e] = tr = new WakeTrail();
            tr.Seen = frame;
            if (!tr.Known && classOf(v.Class[k]) is { } tc && shipRenderer.Wakes.HullOf(v.Class[k], tc) is { } hull)
                (tr.SternX, tr.Beam, tr.Wash, tr.Known) = (hull.X0, hull.BeamM, FoamTrails.WashOf(tc.Motion.Shafts), true);
            tr.Sample(p, f, v.Speed[k], world.DrawMs);

            // the route still to steam
            bool sel = e == selected;
            if (v.RouteCount[k] > 0 && (sel || allRoutes && overlay.Vertices.Length < RouteVertexBudget))
            {
                // only the legs that pass through the view (2.7c: 1,750 tramps' ocean routes, all off screen, were
                // ~500,000 vertices a frame)
                overlay.BeginStrip(sel ? RouteSelected : RouteColor, sel ? 2f : 1.2f);
                var from = p;
                bool open = false;
                int legs = v.RouteCount[k] + (v.RouteLoop[k] ? 1 : 0);   // a patrol: round to the first again
                for (int i = 0; i < legs; i++)
                {
                    var to = v.Waypoints[v.RouteStart[k] + i % v.RouteCount[k]];
                    if (ArcInView(cap, from, to))
                    {
                        if (!open)
                            ClipPoint(view, from);
                        GreatCircle(view, from, to, cap);
                        open = true;
                    }
                    else if (open)
                    {
                        overlay.ClipBreak();
                        open = false;
                    }
                    from = to;
                }
                overlay.End();
                for (int i = 0; i < v.RouteCount[k]; i++)
                    if (InView(cap, v.Waypoints[v.RouteStart[k] + i]))
                        Ring(view, v.Waypoints[v.RouteStart[k] + i], 4 * Rp, sel ? RouteSelected : RouteColor, 1.5f);
            }

            if (sel && view.Project(p, out double x, out double y))
            {
                double len = lengthOf(v.Class[k]) * view.Altitude / view.MetresPerPixel / Math.Max(Vec3d.Dot(p - view.Eye, view.Forward), 1e-3);
                Ring(view, p, Math.Max(len / 2, IconLengthOf(v.Class[k]) / 2) + 7 * Rp, SelectColor, 2f);
                if (world.Classes.TryGet(v.Class[k]) is { Launchers.Length: > 0 } tc2 && v.TorpedoesLeft[k] > 0)
                {
                    // her torpedoes' reach, fast and slow (docs/map-ui/battle-hud.md U5)
                    var torp = tc2.Torpedoes[tc2.Launchers[0].Torpedo];
                    RingM(view, p, torp.FastRangeM, TorpedoRing, 1f);
                    RingM(view, p, torp.SlowRangeM, TorpedoRing with { W = TorpedoRing.W * 0.6f }, 1f);
                }
                if (horizonRing && world.Classes.TryGet(v.Class[k]) is { Eyes.Length: > 0 } hc)
                {
                    // dev (fire-control.md F4d): the sea's horizon from her highest eye, and the reach to a ship as tall as she is
                    double eye = hc.Eyes[0].HeightM;
                    RingM(view, p, SightModel.HorizonM(eye, world.Settings), HorizonColor, 1.5f);
                    RingM(view, p, SightModel.ReachM(eye, hc.Silhouette.TopM, world.Settings), HorizonColor with { W = 0.3f }, 1f);
                }
                int t = v.Captain[k] != 0 ? v.IndexOf(v.CaptainTarget[k]) : -1;
                if (t >= 0)
                {
                    // the ship its captain follows or runs from
                    var (tp, _) = v.PoseAt(t, world.DrawMs);
                    overlay.BeginStrip(TargetColor, 1f);
                    Point(view, p);
                    Point(view, tp);
                    overlay.End();
                }
                if (v.StandoffM[k] / view.MetresPerPixel >= 3)
                {
                    // ships and the land: the minimum and the standoff it keeps from the coast, and the feeler's reach
                    RingM(view, p, v.MinClearanceM[k], LandRing, 1f);
                    RingM(view, p, v.StandoffM[k], LandRing, 1f);
                    var reach = p;
                    var rf = f;
                    Earth.Advance(ref reach, ref rf, v.LookaheadM[k]);
                    overlay.BeginStrip(v.LandSide[k] != 0 ? SlideColor : LandRing, 1f);
                    Point(view, p);
                    Point(view, reach);
                    overlay.End();
                }
                if (v.Helm[k] == Helm.Course)
                {
                    // the ordered course, a short line on from the bow
                    var bow = p + f * (lengthOf(v.Class[k]) / 2);
                    var dir = Earth.Forward(bow, v.CourseOrderDeg[k]);
                    double ahead = Math.Max(len / 2, 30) * view.MetresPerPixel;
                    overlay.BeginStrip(CourseColor, 1.5f);
                    Point(view, bow);
                    Point(view, bow + dir * ahead);
                    overlay.End();
                }
            }
        }
        DrawGroups(view, s);
        PlacePreview(view);
        TrackLines(view, s);   // last: the most lines and the least needed, so a full buffer drops them first

        // tracks of ships no longer there
        gone.Clear();
        foreach (var (e, tr) in tracks)
            if (tr.Seen != frame)
                gone.Add(e);
        foreach (int e in gone)
            tracks.Remove(e);
        overlay.End();
    }

    /// <summary>Every ship's trail, as cheaply as it reads (docs/battle/plan.md step 2.7b: 250 ships' tracks were
    /// ~60,000 segments a frame, past the overlay's buffer): none for a ship further off the screen than its trail
    /// reaches, and the points thinned to about three pixels apart. On the table a line; in the world view the foam lane
    /// instead (docs/visuals/wakes.md K4, the user's: it is the path's cue there), the two crossing with the grid.</summary>
    void TrackLines(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        double mpp = view.MetresPerPixel, w = WorldView.Factor(view);
        var foam = shipRenderer.Foam;
        foam.Begin();
        long now = world.DrawMs;
        for (int k = 0; k < v.Count; k++)
        {
            if (!tracks.TryGetValue(v.Entity[k], out var tr) || tr.Count == 0)
                continue;
            var (p, f) = v.PoseAt(k, now);
            double reachPx = (tr.Count > 0 ? tr.Newest.S - tr[0].S : 0) / mpp + 1000 / mpp;
            if (!view.Project(p, out double x, out double y) || x < -reachPx || y < -reachPx || x > width + reachPx || y > height + reachPx)
                continue;
            var head = tr.Head(p, f, v.Speed[k], now);
            if (w > 0 && SeaView.FoamGain > 0)
                foam.Add(view, tr, head, now, SeaView.FoamGain);
            if (w >= 1)
                continue;
            overlay.BeginStrip(TrackColor with { W = TrackColor.W * (float)(1 - w) }, 1.5f);
            var last = tr[0].P;
            Point(view, last);
            for (int i = 1; i < tr.Count; i++)
                if (Earth.Distance(tr[i].P, last) >= 3 * mpp)
                    Point(view, last = tr[i].P);
            Point(view, head.P);
            overlay.End();
        }
        // the torpedoes' bubble tracks, in the same draw (docs/visuals/torpedoes.md R2)
        if (w > 0 && Torpedoes.TorpedoLook.On)
            Torpedoes.TorpedoRibbons.Add(view, torpedoBook, foam, Torpedoes.TorpedoLook.DrawMs(torpedoBook, now), classOf);
    }

    void Point(in MapView view, Vec3d p)
    {
        if (view.Project(p, out double x, out double y))
            overlay.Point(x, y);
        else
            overlay.End();
    }

    /// <summary>What the view can see on the sphere, generously: a cap round the point under the camera, its angular
    /// radius (radians) the screen's half-diagonal at the centre's scale, doubled for the perspective, and never past
    /// the horizon.</summary>
    static (Vec3d Centre, double Radius) ViewCap(in MapView view)
    {
        double halfDiag = Math.Sqrt(view.Width * view.Width + view.Height * view.Height) / 2;
        double horizon = Math.Acos(Earth.Radius / (Earth.Radius + view.Altitude));
        return (-view.Forward, Math.Min(2 * halfDiag * view.MetresPerPixel / Earth.Radius + 0.002, horizon + 0.002));
    }

    static bool InView((Vec3d Centre, double Radius) cap, Vec3d p) =>
        Vec3d.Dot(cap.Centre, p.Normalized()) >= Math.Cos(cap.Radius);

    /// <summary>Whether the great-circle arc from a to b (shorter than half the way round) comes within the cap.</summary>
    static bool ArcInView((Vec3d Centre, double Radius) cap, Vec3d a, Vec3d b)
    {
        if (cap.Radius >= Math.PI / 2 || InView(cap, a) || InView(cap, b))
            return true;
        var an = a.Normalized();
        var bn = b.Normalized();
        var n = Vec3d.Cross(an, bn);
        double len = n.Length;
        if (len < 1e-12)
            return false;
        n /= len;
        double s = Vec3d.Dot(cap.Centre, n);   // the sine of the centre's angle off the arc's plane
        if (Math.Abs(s) >= Math.Sin(cap.Radius))
            return false;
        var q = (cap.Centre - n * s).Normalized();   // the circle's nearest point to the centre: is it on the arc?
        return Vec3d.Dot(Vec3d.Cross(an, q), n) >= 0 && Vec3d.Dot(Vec3d.Cross(q, bn), n) >= 0;
    }

    void GreatCircle(in MapView view, Vec3d a, Vec3d b, (Vec3d Centre, double Radius) cap)
    {
        // pieces about 24 px long on screen (at least one per 500 km, so a long one still bends), at most 64; a point
        // outside the view's cap, its neighbours too, isn't even projected (a dot product, not a projection)
        double d = Earth.Distance(a, b);
        int n = Math.Clamp((int)Math.Max(d / view.MetresPerPixel / 24, d / 500_000), 1, 64);
        double cosR = Math.Cos(cap.Radius);
        var prev = a.Normalized();
        bool prevIn = Vec3d.Dot(cap.Centre, prev) >= cosR;
        for (int i = 1; i <= n; i++)
        {
            double t = (double)i / n;
            var q = (a * (1 - t) + b * t).Normalized();
            bool qIn = Vec3d.Dot(cap.Centre, q) >= cosR;
            if (qIn || prevIn || cap.Radius >= Math.PI / 2)
            {
                if (!prevIn && !overlay.ClipHave)
                    ClipPoint(view, prev * Earth.Radius);   // the piece coming in from outside the cap
                ClipPoint(view, q * Earth.Radius);
            }
            else if (overlay.ClipHave)
                overlay.ClipBreak();
            (prev, prevIn) = (q, qIn);
        }
    }

    /// <summary>Other ships' routes stop being drawn once the overlay holds this many vertices: a sea crowded past
    /// reading (2.7c's 1,750 tramps in one box of ocean) costs a bounded frame. The selection's is always drawn.</summary>
    const int RouteVertexBudget = 120_000;

    /// <summary>The next point of a strip clipped to the screen (<see cref="MapLines.ClipPoint"/>); one that doesn't
    /// project breaks it.</summary>
    void ClipPoint(in MapView view, Vec3d p)
    {
        if (view.Project(p, out double x, out double y))
            overlay.ClipPoint(x, y);
        else
            overlay.ClipBreak();
    }

    /// <summary>Target pixels a reference pixel, for the symbols' sizes (<see cref="SymbolLook.Px"/>).</summary>
    float Rp => SymbolLook.Px(height);

    /// <summary>A circle on the sphere round a point, metres: as smooth as its size on the screen needs, only its arc on
    /// the screen built.</summary>
    void RingM(in MapView view, Vec3d p, double radiusM, Vector4 colour, float widthPx)
    {
        double rPx = radiusM / view.MetresPerPixel;
        if (view.Project(p, out double cx, out double cy) && !overlay.CircleShows(cx, cy, rPx))
            return;
        var up = p.Normalized();
        var e = Earth.East(p);
        var n = Vec3d.Cross(up, e);
        int segs = overlay.Segments(rPx);
        overlay.BeginStrip(colour, widthPx);
        for (int i = 0; i <= segs; i++)
        {
            double a = i * Math.PI * 2 / segs;
            ClipPoint(view, (up * Earth.Radius + (e * Math.Cos(a) + n * Math.Sin(a)) * radiusM).Normalized() * Earth.Radius);
        }
        overlay.EndClosed();
    }

    /// <summary>A ring of a radius in target px round a point.</summary>
    void Ring(in MapView view, Vec3d p, double radiusPx, Vector4 colour, float widthPx)
    {
        if (view.Project(p, out double x, out double y))
            overlay.Circle(x, y, radiusPx, colour, widthPx);
    }

    // ------------------------------------------------------------------ the mouse

    /// <summary>A left click: selects the ship under the cursor (or none). Armed to place, a press on the sea is a
    /// placement (<see cref="BeginPlace"/>), and a click on land places nothing and selects nothing (the cursor line
    /// says it's land).</summary>
    void Click(double x, double y)
    {
        var view = View;
        if (Armed && view.Pick(x, y, out var at) && world.Shore.IsLand(at) || ArmedClick(x, y))
            return;
        int hit = PickShip(view, x, y);
        if (hit < 0)
        {
            // an enemy: her ship in sight, else her contact's marker (her card, as the player holds her)
            var s = world.Current;
            hit = PickShip(view, x, y, s.Sighted);
            if (hit < 0)
                hit = PickContact(view, s, x, y);
        }
        Pick(hit);
        DoubleClick(hit);
        squadronCardOf = -1;
        if (hit >= 0)
            cardOpen = true;   // a click opens her card (docs/map-ui/battle-hud.md decision 1)
    }

    /// <summary>Whether a press on the sea places ships: the dev panel armed and its class built.</summary>
    bool Armed => placing && placeClass >= 0 && world.Classes.IsReady(placeClass);

    /// <summary>A left press: armed and on the sea, the placement's guide goes here and the drag aims it (true); else
    /// false, and the press pans or clicks as ever.</summary>
    bool BeginPlace(double x, double y)
    {
        if (!Armed || !View.Pick(x, y, out var at) || world.Shore.IsLand(at))
            return false;
        (placeDrag, placeAt, placeX, placeY, placeHeading) = (true, at, x, y, spawnHeading);
        return true;
    }

    /// <summary>The cursor dragged: the ships head from where they were pressed toward it; back within a click of the
    /// press, the panel's heading again.</summary>
    void AimPlace(double x, double y)
    {
        if (Math.Abs(x - placeX) + Math.Abs(y - placeY) < ClickPx)
            placeHeading = spawnHeading;
        else if (View.Pick(x, y, out var to))
            placeHeading = (Earth.Bearing(placeAt, to) % 360 + 360) % 360;
    }

    /// <summary>The release: the ships placed on the heading aimed (a click: the panel's), which the panel keeps.</summary>
    void EndPlace(double x, double y)
    {
        AimPlace(x, y);
        placeDrag = false;
        if (!Armed)
            return;   // disarmed mid-drag
        spawnHeading = (int)Math.Round(placeHeading) % 360;
        Spawn(placeAt, spawnHeading);
        placing = keepPlacing;
    }

    /// <summary>Where a placement's ships would lie, each a stroke its length (an icon's at least) on its station, and
    /// an arrow on from the guide's bow: at the press while aiming, else under the cursor while armed.</summary>
    void PlacePreview(in MapView view)
    {
        Vec3d at;
        double heading;
        if (placeDrag)
            (at, heading) = (placeAt, placeHeading);
        else if (Armed && mouseIn && !ImGui.GetIO().WantCaptureMouse && view.Pick(cursorX, cursorY, out at) && !world.Shore.IsLand(at))
            heading = spawnHeading;
        else
            return;
        var up = at.Normalized();
        var f = Earth.Forward(at, heading);
        var starboard = Vec3d.Cross(f, up);
        var formation = (Formation)spawnFormation;
        double half = Math.Max(lengthOf(placeClass), IconLengthOf(placeClass) * view.MetresPerPixel) / 2;
        for (int i = 0; i < spawnCount; i++)
        {
            var p = Station(at, f, starboard, formation, i);
            var pf = Earth.Forward(p, heading);
            overlay.BeginStrip(PlaceColor, i == 0 ? 3f : 2f);
            Point(view, p - pf * half);
            Point(view, p + pf * half);
            overlay.End();
        }
        // the arrow: as long as the spacing, or 60 px
        double ahead = Math.Max(spawnSpacing, 60 * view.MetresPerPixel);
        var bow = at + f * half;
        var tip = (bow + f * ahead).Normalized() * Earth.Radius;
        overlay.BeginStrip(PlaceColor, 1.5f);
        Point(view, bow);
        Point(view, tip);
        overlay.End();
        overlay.BeginStrip(PlaceColor, 1.5f);
        Point(view, tip + Earth.Forward(tip, heading + 150) * (ahead * 0.25));
        Point(view, tip);
        Point(view, tip + Earth.Forward(tip, heading - 150) * (ahead * 0.25));
        overlay.End();
    }

    /// <summary>A placement's ship i on its station behind the guide at a point (as the rails reckon them,
    /// GroupSystem.OffsetFromGuide).</summary>
    Vec3d Station(Vec3d at, Vec3d f, Vec3d starboard, Formation formation, int i)
    {
        var (astern, abeam) = GroupSystem.OffsetFromGuide(formation, spawnSpacing, i);
        return (at - f * astern + starboard * abeam).Normalized() * Earth.Radius;
    }

    /// <summary>From the designer: the class of the designer's design as it is now, asked for once a change (a frame's
    /// check is one int).</summary>
    void FollowDesigner()
    {
        if (placeVersion == designer.DesignVersion)
            return;
        placeVersion = designer.DesignVersion;
        placeClass = designer.Design is { } d ? world.Classes.Request(d) : -1;
    }

    string DesignerName() => designer.Design is { } d ? (d.Name ?? d.Id ?? "unnamed") + (designer.Edited ? " (edited)" : "") : "none";

    /// <summary>The dev panel's ships at a point: the guide there, the rest on their stations behind it, all on one
    /// heading and speed; more than one made a group. Then a look round at once (<see cref="ShipSystem.LookNow"/>):
    /// what they see, and who sees them, is in the pictures now, not at the next checks.</summary>
    void Spawn(Vec3d at, double heading)
    {
        var up = at.Normalized();
        var f = Earth.Forward(at, heading);
        var starboard = Vec3d.Cross(f, up);
        var formation = (Formation)spawnFormation;
        for (int i = 0; i < spawnCount; i++)
        {
            var (lat, lon) = Earth.LatLon(Station(at, f, starboard, formation, i));
            world.Send(ShipSystem.Spawn(placeClass, lat, lon, heading, spawnKn, (byte)spawnSide));
        }
        if (spawnCount > 1)
            world.Send(GroupSystem.CreateOfNewest(spawnCount, formation, spawnSpacing));
        world.Send(ShipSystem.LookNow());
    }

    /// <summary>The entity of the ship nearest the cursor within its drawn size (an icon's at least), or -1: of the
    /// snapshot's ships, or of another view of them (<see cref="Snapshot.Sighted"/>).</summary>
    int PickShip(in MapView view, double x, double y, ShipsView? ships = null)
    {
        var v = ships ?? world.Current.Ships;
        int best = -1;
        double bestD = double.MaxValue;
        for (int k = 0; k < v.Count; k++)
        {
            var (p, _) = v.PoseAt(k, world.DrawMs);
            if (!view.Project(p, out double px, out double py))
                continue;
            double len = lengthOf(v.Class[k]) * view.Altitude / view.MetresPerPixel / Math.Max(Vec3d.Dot(p - view.Eye, view.Forward), 1e-3);
            double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            if (d <= Math.Max(len / 2, Math.Max(IconLengthOf(v.Class[k]) / 2 + 4, MinPickPx)) && d < bestD)
                (best, bestD) = (v.Entity[k], d);
        }
        return best;
    }

    /// <summary>The reach of a click round a ship, px, at least: a torpedo boat's icon is small, not hard to pick.</summary>
    const double MinPickPx = 9;

    /// <summary>A class's icon length on the table, px (by its tonnage, <see cref="ShipDrawList.IconPx"/>).</summary>
    double IconLengthOf(int cls) => ShipDrawList.IconPx(classOf(cls)).Length;

    /// <summary>A class's icon length at a point in the view, px: its tonnage's at the least, its true length once longer.</summary>
    double IconLengthAt(in MapView view, int cls, Vec3d p) =>
        ShipDrawList.IconPx(classOf(cls), view.Altitude / view.MetresPerPixel / Math.Max(Vec3d.Dot(p - view.Eye, view.Forward), 1e-3)).Length;

    /// <summary>A right click on the sea with a ship selected: steer for it, or with shift add it to the route.</summary>
    void OrderAt(double x, double y, bool waypoint)
    {
        if (FireAt(x, y) || SmartOrder(x, y, waypoint))
            return;
        var s = world.Current;
        int k = s.Ships.IndexOf(selected);
        if (k < 0 || !View.Pick(x, y, out var at))
            return;
        var (unit, ship) = Commanded(s);
        var o = world.Ai.Orders;
        if (waypoint)
        {
            if (world.Shore.IsLand(at))
                return;   // refused: the cursor says so (docs/world/terrain-plan.md)
            var (lat, lon) = Earth.LatLon(at);
            world.Send(o.AddWaypoint(unit, lat, lon));
        }
        else
        {
            var (p, _) = s.Ships.PoseAt(s.Ships.IndexOf(ship), world.DrawMs);
            world.Send(o.ClearRoute(unit));
            world.Send(o.Course(unit, Earth.Bearing(p, at), hardOver ? 90 : 0));
        }
    }

    // ------------------------------------------------------------------ the panels

    /// <summary>Sends a behaviour from the strip: 0 holds the course and speed ordered now, 1 follows, 2 runs
    /// from (with nobody else at sea, it holds), 3 fights (the nearest enemy, or the target).</summary>
    void Behaviour(int pick, Unit unit, int target)
    {
        var o = world.Ai.Orders;
        world.Send(pick == 3 ? o.Fight(unit, target != 0 ? Unit.Ship(target) : default)
                 : pick == 0 || target == 0 ? o.Hold(unit)
                 : pick == 1 ? o.Follow(unit, Unit.Ship(target), followMetres)
                 : o.Run(unit, Unit.Ship(target)));
    }

    // the search row: the pattern, its first leg's course (or the ship's heading), its sizes in miles, its count of
    // legs (a sector's triangles), and which way it turns
    int searchPattern, searchAxis, searchLegNm = 20, searchSpacingNm = 5, searchLegs = 6;
    bool searchAlongHeading = true, searchStarboard = true;
    Vec3d[] searchWaypoints = [];
    static readonly string[] SearchNames = ["Ladder", "Expanding square", "Sector", "Patrol line"];

    /// <summary>A search pattern for the commanded unit (a ship, or a group: its guide steams it), from where it is: the
    /// route's waypoints sent as orders. With the clock fast enough a group on rails steams it on rails, turning at
    /// once at each waypoint.</summary>
    void SearchRow(Snapshot s, Unit unit, int kc)
    {
        float field = 80 * UiFonts.Scale;
        var pattern = (SearchPattern)searchPattern;
        ImGui.Text("Search:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140 * UiFonts.Scale);
        ImGui.Combo("##search", ref searchPattern, SearchNames, SearchNames.Length);
        var (p, f) = s.Ships.PoseAt(kc, world.DrawMs);
        if (searchAlongHeading)
            searchAxis = (int)Math.Round(Earth.Heading(p, f)) % 360;
        ImGui.SameLine();
        ImGui.BeginDisabled(searchAlongHeading);
        ImGui.SetNextItemWidth(field);
        if (ImGui.InputInt("°##axis", ref searchAxis, 0))
            searchAxis = ((searchAxis % 360) + 360) % 360;
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.Checkbox("ahead", ref searchAlongHeading);
        if (pattern != SearchPattern.ExpandingSquare)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(field);
            if (ImGui.InputInt(pattern == SearchPattern.Sector ? "nm radius" : "nm leg", ref searchLegNm, 1, 5))
                searchLegNm = Math.Clamp(searchLegNm, 1, 500);
        }
        if (pattern is SearchPattern.Ladder or SearchPattern.ExpandingSquare)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(field);
            if (ImGui.InputInt("nm apart", ref searchSpacingNm, 1, 5))
                searchSpacingNm = Math.Clamp(searchSpacingNm, 1, 100);
        }
        // as many legs as the route holds: a ladder's legs and the steps between them, a sector's three a triangle
        int cap = world.Settings.RouteCapacity;
        int most = pattern switch { SearchPattern.Ladder => (cap + 1) / 2, SearchPattern.Sector => cap / 3, _ => cap };
        if (pattern != SearchPattern.PatrolLine)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(field);
            if (ImGui.InputInt(pattern == SearchPattern.Sector ? "triangles" : "legs", ref searchLegs, 1, 2))
                searchLegs = Math.Clamp(searchLegs, 1, most);
            ImGui.SameLine();
            ImGui.Checkbox("to starboard", ref searchStarboard);
        }
        ImGui.SameLine();
        if (ImGui.Button("Search"))
        {
            if (searchWaypoints.Length < cap)
                searchWaypoints = new Vec3d[cap];
            int legs = Math.Clamp(searchLegs, 1, most);
            int n = SearchPatterns.Waypoints(pattern, p, searchAxis, searchLegNm * 1852.0, searchSpacingNm * 1852.0, legs, searchStarboard,
                searchWaypoints.AsSpan(0, cap));
            SearchPatterns.Orders(world.Ai.Orders, unit, searchWaypoints.AsSpan(0, n), SearchPatterns.Loops(pattern), c => world.Send(c));
        }
    }

    string ShipLabel(Snapshot s, int entity)
    {
        int j = s.Ships.IndexOf(entity);
        return j < 0 ? "-" : world.NameOf(entity) is { } name ? $"#{entity} {name}" : $"#{entity} {world.Classes.TryGet(s.Ships.Class[j])?.Name ?? "?"}";
    }

    /// <summary>The entity of the ship nearest ship k, or 0.</summary>
    static int Nearest(Snapshot s, int k)
    {
        var v = s.Ships;
        int best = 0;
        double bestD = double.MaxValue;
        for (int j = 0; j < v.Count; j++)
        {
            double d = j == k ? double.MaxValue : Earth.Distance(v.Position[j], v.Position[k]);
            if (d < bestD)
                (best, bestD) = (v.Entity[j], d);
        }
        return best;
    }

    // dev: each side's aggression as the slider shows it, while it's dragged (the snapshot's otherwise)
    readonly float[] aggressionEdit = new float[256];
    int aggressionDragged = -1;

    /// <summary>Dev: each side's doctrine's aggression (-1 timid to +1 bold; docs/battle/command.md "Doctrine"), the
    /// scenario's sides by name, else sides 0 and 1. Sent once a drag ends, as the side's doctrine.</summary>
    void AggressionRow(Snapshot s)
    {
        var names = world.Scenario?.Sides;
        int sides = names?.Length ?? 2;
        ImGui.TextDisabled("aggression (dev):");
        for (int side = 0; side < sides; side++)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(110 * UiFonts.Scale);
            if (aggressionDragged != side)
                aggressionEdit[side] = (float)s.Aggression[side];
            ImGui.SliderFloat($"{names?[side] ?? $"side {side}"}##aggr{side}", ref aggressionEdit[side], -1, 1, "%+.2f");
            if (ImGui.IsItemActive())
                aggressionDragged = side;
            else if (aggressionDragged == side)
                aggressionDragged = -1;
            if (ImGui.IsItemDeactivatedAfterEdit())
                world.Send(world.Ai.Orders.SideDoctrine(side, Sim.Ai.DoctrineField.Aggression, aggressionEdit[side]));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("-1 timid, +1 bold: scales the odds it takes (outgunned ratio), how close it fights, when ships fall out,\n" +
                                 "when flotillas and the side break off, and the turn from torpedoes (settings' AggressionShift).\n" +
                                 "Ctrl+click to type a value.");
        }
    }

    /// <summary>The dev panel's body (F1): the scenario picker, the dev views (truth, horizon), watching or playing a
    /// side and its commander, the sides' aggression, the spawner, and a salvo from the selected ship.</summary>
    void DevUi(Snapshot s)
    {
        DevWhere(s);
        ImGui.Separator();
        ScenarioRow();
        ImGui.TextDisabled($"Ships at sea: {s.Ships.Count}   (dev: spawn)");
        ImGui.SameLine();
        bool truth = world.Truth;
        if (ImGui.Checkbox("truth", ref truth))
            world.Truth = truth;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("dev: every ship as it is, not the player's picture");
        ImGui.SameLine();
        ImGui.Checkbox("horizon", ref horizonRing);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("dev: round the selected ship, the sea's horizon from her highest eye, and fainter,\n" +
                             "how far she could see a ship as tall as she is (docs/battle/fire-control.md F4d)");
        ImGui.SameLine();
        bool watch = s.PlayerSide < 0;
        if (ImGui.Checkbox("watch", ref watch))
            world.Send(Command.SetPlayerSide(watch ? -1 : spawnSide));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("dev: no side is yours, every side the AI's (docs/battle/command.md 5.0); the spawner's side takes one back.\n" +
                             "Encounters are then all others': set what they do to the clock in the clock's panel.");
        if (s.PlayerSide >= 0 && s.CommanderMode[s.PlayerSide] != (byte)Sim.Ai.Commanders.CommanderMode.None)
        {
            // the player's side's commander (docs/battle/command.md 5.1d, decision 5): off unless they automate it
            ImGui.SameLine();
            bool on = s.CommanderOn[s.PlayerSide];
            if (ImGui.Checkbox("AI commands", ref on))
                world.Send(world.Ai.Orders.Commander(s.PlayerSide, on ? Sim.Ai.Commanders.CommanderMode.On : Sim.Ai.Commanders.CommanderMode.Auto));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Your side's commander gives your top units their tasks; your own order to a unit takes it from him\n" +
                                 "(\"leave it to the admiral\" on its group row hands it back).");
        }
        AggressionRow(s);
        bool fromDesigner = designPick == FromDesigner;
        if (fromDesigner && placeClass >= 0)
            FollowDesigner();
        ImGui.SetNextItemWidth(170 * UiFonts.Scale);
        if (ImGui.BeginCombo("##design", fromDesigner ? "From designer" : designNames[designPick]))
        {
            if (ImGui.Selectable("From designer", fromDesigner) && !fromDesigner)
            {
                designPick = FromDesigner;
                placeVersion = -1;
                FollowDesigner();
                placing = false;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"the designer's design as it is now ({DesignerName()}); spawned after an edit, a new class");
            for (int i = 0; i < designNames.Length; i++)
                if (ImGui.Selectable(designNames[i], i == designPick))
                {
                    designPick = i;
                    placeClass = world.Classes.Request(Shipgen.Design.Load(
                        Path.Combine(AppContext.BaseDirectory, "Content", "Designs", designNames[i] + ".json")));
                    placing = false;
                }
            ImGui.EndCombo();
        }
        fromDesigner = designPick == FromDesigner;
        if (fromDesigner && ImGui.IsItemHovered())
            ImGui.SetTooltip(DesignerName());
        ImGui.SameLine();
        bool ready = placeClass >= 0 && world.Classes.IsReady(placeClass);
        string? error = ready ? world.Classes.ErrorOf(placeClass) : null;
        // from the designer the first Place asks for the class; after that, as for a shipped design, it waits
        // for the build
        bool canPlace = fromDesigner && placeClass < 0 ? designer.Design != null : ready && error == null;
        ImGui.BeginDisabled(!canPlace);
        if (ImGui.Button(placing ? "Click or drag on the sea..." : "Place"))
        {
            placing = !placing;
            if (placing && fromDesigner)
                FollowDesigner();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("click the sea: on the heading below · press and drag: they head the way you drag, placed on release");
        ImGui.SameLine();
        ImGui.Checkbox("keep placing", ref keepPlacing);

        float field = 110 * UiFonts.Scale;
        ImGui.SetNextItemWidth(field);
        if (ImGui.InputInt("ships", ref spawnCount, 1, 4))
            spawnCount = Math.Clamp(spawnCount, 1, 24);
        if (spawnCount > 1)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(170 * UiFonts.Scale);
            ImGui.Combo("##spawnformation", ref spawnFormation, FormationNames, FormationNames.Length);
            ImGui.SetNextItemWidth(field);
            if (ImGui.InputInt("m apart", ref spawnSpacing, 100, 500))
                spawnSpacing = Math.Clamp(spawnSpacing, 100, 20_000);
        }
        ImGui.SetNextItemWidth(field);
        if (ImGui.InputInt("° heading", ref spawnHeading, 15, 90))
            spawnHeading = ((spawnHeading % 360) + 360) % 360;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(field);
        if (ImGui.InputInt("kn", ref spawnKn, 1, 5))
            spawnKn = Math.Clamp(spawnKn, 0, 40);
        ImGui.SetNextItemWidth(field);
        if (ImGui.InputInt("side", ref spawnSide, 1, 1))
        {
            spawnSide = Math.Clamp(spawnSide, 0, maxSide);
            world.Send(Command.SetPlayerSide(spawnSide));   // the player plays the side they spawn
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(yours; different sides are opposing)");
        if (fromDesigner && designer.Design == null)
            ImGui.TextColored(new Vector4(1, 0.4f, 0.3f, 1), "the designer has no design");
        else if (placeClass >= 0 && !ready)
            ImGui.TextDisabled("building...");
        else if (error != null)
            ImGui.TextColored(new Vector4(1, 0.4f, 0.3f, 1), error.Split('\n')[0]);
        if (selected >= 0)
        {
            ImGui.Separator();
            DevFireRow(s);
        }
    }

    void ShipsUi()
    {
        var vp = ImGui.GetMainViewport();
        float pad = 8 * UiFonts.Scale;
        var s = world.Current;

        // the dev panel (F1): the scenario picker, the sides, the dev views and the spawner; none of it is the game's
        if (!devOpen)
            placing = placeDrag = false;
        else
        {
            ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + pad, HudTop), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(0.85f);
            if (ImGui.Begin("Dev (F1)##dev", ref devOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                                           ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing))
                DevUi(s);
            ImGui.End();
        }

        HeldUi(s);
        if (placeDrag)
            ImGui.SetTooltip($"heading {Math.Round(placeHeading) % 360:000}°");
    }
}
