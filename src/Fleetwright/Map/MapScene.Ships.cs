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
/// A dev panel spawns ships of a shipped design where you click, one or a group already in formation: the stand-in
/// for missions placing forces. All of it
/// reads the snapshot and changes the world only by commands.</summary>
public sealed unsafe partial class MapScene
{
    readonly ShipArtLibrary art;
    readonly ShipBatchRenderer shipRenderer;
    readonly Func<int, ShipArt?> artOf;
    readonly Func<int, double> lengthOf;
    readonly string[] designNames;
    int selected;   // the selected ship's entity, or -1
    double pressX, pressY;
    bool pressed;

    // the dev spawn: the design picked, its class id once requested, whether the next click places, and what it places:
    // how many (more than one a group, each ship on its station), the formation, heading and speed
    int designPick = -1, placeClass = -1;
    bool placing, keepPlacing;
    int spawnCount = 1, spawnFormation, spawnSpacing = 600, spawnHeading, spawnKn = 10, spawnSide;
    int courseEdit;
    bool hardOver;
    int followMetres = 800;

    static readonly string[] CaptainNames = ["Course and speed", "Follow", "Run from"];

    /// <summary>A short wake of positions behind each ship, sampled in game time; made when a ship is first seen.</summary>
    sealed class Track
    {
        // grows as it fills, up to TrackPoints (2.7c: 2,000 full-size tracks made at once on a scenario's first frame
        // were 11.5 MB and a collection)
        public Vec3d[] Points = new Vec3d[16];
        public int Count, Head;
        public long LastMs;
        public long Seen;
    }

    static readonly int[] CourseSteps = [-90, -10, 10, 90];
    const int TrackPoints = 60;
    const long TrackEveryMs = 10_000;   // 10 minutes of track (the user, 2026-10-13: 40 was too long)
    readonly Dictionary<int, Track> tracks = [];
    readonly List<int> gone = [];
    long frame;

    static readonly Vector4 TrackColor = new(0.85f, 0.9f, 1f, 0.35f), RouteColor = new(1f, 0.85f, 0.4f, 0.55f),
        RouteSelected = new(1f, 0.8f, 0.3f, 0.95f), SelectColor = new(1f, 0.78f, 0.25f, 0.95f), CourseColor = new(1f, 1f, 1f, 0.6f), TargetColor = new(1f, 0.78f, 0.25f, 0.45f),
        LandRing = new(0.55f, 0.85f, 0.6f, 0.5f), SlideColor = new(0.55f, 1f, 0.6f, 0.95f);

    // ------------------------------------------------------------------ lines over the globe

    void BuildOverlay(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        var cap = ViewCap(view);
        // every ship's route only from the operations band in (under 1,000 km across): further out, only the
        // selection's (2.7c: 1,750 tramps' routes over the Atlantic were a tangle, and 1.4 million vertices a frame)
        bool allRoutes = view.MetresPerPixel * view.Width < 1_000_000;
        overlay.Begin(width, height);
        frame++;
        DrawEncounters(view, s);
        DrawContacts(view, s);
        for (int k = 0; k < v.Count; k++)
        {
            var (p, f) = v.PoseAt(k, world.DrawMs);
            int e = v.Entity[k];

            // the track: sampled here, drawn last (TrackLines)
            if (!tracks.TryGetValue(e, out var tr))
                tracks[e] = tr = new Track();
            tr.Seen = frame;
            long now = world.DrawMs;
            if (tr.Count > 0)
            {
                // a jump no ship could have steamed (a unit snapped onto its stations on rails, or round to a new
                // course): the track starts again where it is, not drawn across the jump (a fan of lines to the new
                // stations, the user's 2026-10-13)
                var last = tr.Points[(tr.Head - 1 + tr.Points.Length) % tr.Points.Length];
                double could = (Math.Abs(v.Speed[k]) + 3) * Math.Abs(now - tr.LastMs) / 1000 + 100;
                if (Earth.Distance(p, last) > could)
                    (tr.Count, tr.Head) = (0, 0);
            }
            if (tr.Count == 0 || now - tr.LastMs >= TrackEveryMs)
            {
                if (tr.Count == tr.Points.Length && tr.Points.Length < TrackPoints)
                {
                    // full and never wrapped (it grows before it would), so in order from 0, the head back at 0: the
                    // next point goes after the old ones, not over the oldest (that left zeros read as points, every
                    // track starting at the globe's centre)
                    Array.Resize(ref tr.Points, Math.Min(tr.Points.Length * 2, TrackPoints));
                    tr.Head = tr.Count;
                }
                tr.Points[tr.Head] = p;
                tr.Head = (tr.Head + 1) % tr.Points.Length;
                tr.Count = Math.Min(tr.Count + 1, tr.Points.Length);
                tr.LastMs = now;
            }

            // the route still to steam
            bool sel = e == selected;
            if (v.RouteCount[k] > 0 && (sel || allRoutes && overlay.Vertices.Length < RouteVertexBudget))
            {
                // only the legs that pass through the view (2.7c: 1,750 tramps' ocean routes, all off screen, were
                // ~500,000 vertices a frame)
                overlay.BeginStrip(sel ? RouteSelected : RouteColor, sel ? 2f : 1.2f);
                clipHave = false;
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
                        overlay.End();
                        (open, clipHave) = (false, false);
                    }
                    from = to;
                }
                overlay.End();
                for (int i = 0; i < v.RouteCount[k]; i++)
                    if (InView(cap, v.Waypoints[v.RouteStart[k] + i]))
                        Ring(view, v.Waypoints[v.RouteStart[k] + i], 4, sel ? RouteSelected : RouteColor, 1.5f);
            }

            if (sel && view.Project(p, out double x, out double y))
            {
                double len = lengthOf(v.Class[k]) * view.Altitude / view.MetresPerPixel / Math.Max(Vec3d.Dot(p - view.Eye, view.Forward), 1e-3);
                Ring(view, p, Math.Max(len / 2, ShipDrawList.IconLengthPx / 2) + 7, SelectColor, 2f);
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

    /// <summary>Every ship's track, as cheaply as it reads (docs/battle/plan.md step 2.7b: 250 ships' tracks were
    /// ~60,000 segments a frame, past the overlay's buffer): none for a ship further off the screen than its track
    /// reaches, and the points thinned to about three pixels apart at its speed.</summary>
    void TrackLines(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        double mpp = view.MetresPerPixel;
        for (int k = 0; k < v.Count; k++)
        {
            if (!tracks.TryGetValue(v.Entity[k], out var tr) || tr.Count == 0)
                continue;
            var (p, _) = v.PoseAt(k, world.DrawMs);
            double reachPx = (tr.Count * (double)TrackEveryMs / 1000 * Math.Max(v.Speed[k], 2) + 1000) / mpp;
            if (!view.Project(p, out double x, out double y) || x < -reachPx || y < -reachPx || x > width + reachPx || y > height + reachPx)
                continue;
            double spacingPx = Math.Max(v.Speed[k], 0.5) * TrackEveryMs / 1000 / mpp;
            int stride = Math.Max(1, (int)(3 / spacingPx));
            overlay.BeginStrip(TrackColor, 1.5f);
            for (int i = (tr.Count - 1) % stride; i < tr.Count; i += stride)   // ends on the newest
                Point(view, tr.Points[(tr.Head - tr.Count + i + tr.Points.Length) % tr.Points.Length]);
            Point(view, p);
            overlay.End();
        }
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
                if (!prevIn && !clipHave)
                    ClipPoint(view, prev * Earth.Radius);   // the piece coming in from outside the cap
                ClipPoint(view, q * Earth.Radius);
            }
            else if (clipHave)
            {
                overlay.End();
                clipHave = false;
            }
            (prev, prevIn) = (q, qIn);
        }
    }

    /// <summary>Other ships' routes stop being drawn once the overlay holds this many vertices: a sea crowded past
    /// reading (2.7c's 1,750 tramps in one box of ocean) costs a bounded frame. The selection's is always drawn.</summary>
    const int RouteVertexBudget = 120_000;

    // a route's line clipped to the screen (and a margin): off it the line breaks, keeping the point just past the edge;
    // a piece with both ends off is drawn alone if it may cross the screen
    bool clipHave, clipPrevIn;
    double clipX, clipY;

    void ClipPoint(in MapView view, Vec3d p)
    {
        if (!view.Project(p, out double x, out double y))
        {
            overlay.End();
            clipHave = false;
            return;
        }
        const double M = 64;
        bool inside = x > -M && y > -M && x < width + M && y < height + M;
        if (inside)
        {
            if (clipHave && !clipPrevIn)
                overlay.Point(clipX, clipY);
            overlay.Point(x, y);
        }
        else if (clipHave && clipPrevIn)
        {
            overlay.Point(x, y);
            overlay.End();
        }
        else if (clipHave && Math.Max(clipX, x) > -M && Math.Min(clipX, x) < width + M && Math.Max(clipY, y) > -M && Math.Min(clipY, y) < height + M)
        {
            overlay.Point(clipX, clipY);   // a long piece across the screen, both ends off it
            overlay.Point(x, y);
            overlay.End();
        }
        (clipHave, clipPrevIn, clipX, clipY) = (true, inside, x, y);
    }

    /// <summary>A circle on the sphere round a point, metres.</summary>
    void RingM(in MapView view, Vec3d p, double radiusM, Vector4 colour, float widthPx)
    {
        var up = p.Normalized();
        var e = Earth.East(p);
        var n = Vec3d.Cross(up, e);
        overlay.BeginStrip(colour, widthPx);
        for (int i = 0; i <= 32; i++)
        {
            double a = i * Math.PI * 2 / 32;
            Point(view, (up * Earth.Radius + (e * Math.Cos(a) + n * Math.Sin(a)) * radiusM).Normalized() * Earth.Radius);
        }
        overlay.End();
    }

    void Ring(in MapView view, Vec3d p, double radiusPx, Vector4 colour, float widthPx)
    {
        if (!view.Project(p, out double x, out double y))
            return;
        overlay.BeginStrip(colour, widthPx);
        for (int i = 0; i <= 24; i++)
        {
            double a = i * Math.PI * 2 / 24;
            overlay.Point(x + Math.Cos(a) * radiusPx, y + Math.Sin(a) * radiusPx);
        }
        overlay.End();
    }

    // ------------------------------------------------------------------ the mouse

    /// <summary>A left click: places a ship if the dev panel is armed, else selects the ship under the cursor (or
    /// none).</summary>
    void Click(double x, double y)
    {
        var view = View;
        if (placing && placeClass >= 0 && world.Classes.IsReady(placeClass) && view.Pick(x, y, out var at))
        {
            if (world.Shore.IsLand(at))
                return;   // no ships on land (the cursor line says it's land)
            Spawn(at);
            placing = keepPlacing;
            return;
        }
        Pick(PickShip(view, x, y));
    }

    /// <summary>The dev panel's ships at a point: the guide there, the rest on their stations behind it (as the rails
    /// reckon them, GroupSystem.OffsetFromGuide), all on one heading and speed; more than one made a group.</summary>
    void Spawn(Vec3d at)
    {
        var up = at.Normalized();
        var f = Earth.Forward(at, spawnHeading);
        var starboard = Vec3d.Cross(f, up);
        var formation = (Formation)spawnFormation;
        for (int i = 0; i < spawnCount; i++)
        {
            var (astern, abeam) = GroupSystem.OffsetFromGuide(formation, spawnSpacing, i);
            var (lat, lon) = Earth.LatLon(at - f * astern + starboard * abeam);
            world.Send(ShipSystem.Spawn(placeClass, lat, lon, spawnHeading, spawnKn, (byte)spawnSide));
        }
        if (spawnCount > 1)
            world.Send(GroupSystem.CreateOfNewest(spawnCount, formation, spawnSpacing));
    }

    /// <summary>The entity of the ship nearest the cursor within its drawn size (an icon's at least), or -1.</summary>
    int PickShip(in MapView view, double x, double y)
    {
        var s = world.Current;
        var v = s.Ships;
        int best = -1;
        double bestD = double.MaxValue;
        for (int k = 0; k < v.Count; k++)
        {
            var (p, _) = v.PoseAt(k, world.DrawMs);
            if (!view.Project(p, out double px, out double py))
                continue;
            double len = lengthOf(v.Class[k]) * view.Altitude / view.MetresPerPixel / Math.Max(Vec3d.Dot(p - view.Eye, view.Forward), 1e-3);
            double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            if (d <= Math.Max(len / 2, ShipDrawList.IconLengthPx / 2 + 4) && d < bestD)
                (best, bestD) = (v.Entity[k], d);
        }
        return best;
    }

    /// <summary>A right click on the sea with a ship selected: steer for it, or with shift add it to the route.</summary>
    void OrderAt(double x, double y, bool waypoint)
    {
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

    /// <summary>The commanded unit's behaviour: plain course and speed orders, or following or running from another
    /// ship, with the other ship and the station's distance. The same orders go to a ship or a group.</summary>
    void CaptainRow(Snapshot s, Unit unit, int k)
    {
        var v = s.Ships;
        var order = (CaptainOrder)v.Captain[k];
        int target = v.CaptainTarget[k];
        int shown = order switch { CaptainOrder.Follow => 1, CaptainOrder.Run => 2, _ => 0 };
        ImGui.Text(unit.IsGroup ? "Group:" : "Captain:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150 * UiFonts.Scale);
        int pick = shown;
        if (ImGui.Combo("##captain", ref pick, CaptainNames, CaptainNames.Length) && pick != shown)
        {
            if (pick != 0 && (target == 0 || v.IndexOf(target) < 0))
                target = Nearest(s, k);
            Behaviour(pick, unit, target);
        }
        if (shown != 0)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(170 * UiFonts.Scale);
            if (ImGui.BeginCombo("##target", ShipLabel(s, target)))
            {
                for (int j = 0; j < v.Count; j++)
                    if (j != k && ImGui.Selectable(ShipLabel(s, v.Entity[j]), v.Entity[j] == target))
                        Behaviour(shown, unit, v.Entity[j]);
                ImGui.EndCombo();
            }
        }
        if (shown == 1)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * UiFonts.Scale);
            if (ImGui.InputInt("m astern", ref followMetres, 100, 500, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                followMetres = Math.Clamp(followMetres, 100, 20_000);
                Behaviour(shown, unit, target);
            }
        }
    }

    /// <summary>Sends a behaviour from the captain row: 0 holds the course and speed ordered now, 1 follows, 2 runs
    /// from (with nobody else at sea, it holds).</summary>
    void Behaviour(int pick, Unit unit, int target)
    {
        var o = world.Ai.Orders;
        world.Send(pick == 0 || target == 0 ? o.Hold(unit)
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

    void ShipsUi()
    {
        var vp = ImGui.GetMainViewport();
        float pad = 8 * UiFonts.Scale;
        const ImGuiWindowFlags Hud = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize |
                                     ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
                                     ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;
        var s = world.Current;

        // dev: spawn ships, top left
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + pad, vp.WorkPos.Y + pad), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.75f);
        if (ImGui.Begin("##spawn", Hud))
        {
            ScenarioRow();
            ImGui.TextDisabled($"Ships at sea: {s.Ships.Count}   (dev: spawn)");
            ImGui.SameLine();
            bool truth = world.Truth;
            if (ImGui.Checkbox("truth", ref truth))
                world.Truth = truth;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("dev: every ship as it is, not the player's picture");
            ImGui.SetNextItemWidth(170 * UiFonts.Scale);
            if (ImGui.BeginCombo("##design", designPick >= 0 ? designNames[designPick] : "pick a design"))
            {
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
            ImGui.SameLine();
            bool ready = placeClass >= 0 && world.Classes.IsReady(placeClass);
            string? error = ready ? world.Classes.ErrorOf(placeClass) : null;
            ImGui.BeginDisabled(!ready || error != null);
            if (ImGui.Button(placing ? "Click the sea..." : "Place"))
                placing = !placing;
            ImGui.EndDisabled();
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
                spawnSide = Math.Clamp(spawnSide, 0, 3);
            ImGui.SameLine();
            ImGui.TextDisabled("(different sides are opposing)");
            if (placeClass >= 0 && !ready)
                ImGui.TextDisabled("building...");
            else if (error != null)
                ImGui.TextColored(new Vector4(1, 0.4f, 0.3f, 1), error.Split('\n')[0]);
        }
        ImGui.End();

        HeldUi(s);

        // the selected ship: bottom centre
        int k = s.Ships.IndexOf(selected);
        if (k < 0)
            return;
        var cls = world.Classes.TryGet(s.Ships.Class[k]);
        var (p, f) = s.Ships.PoseAt(k, world.DrawMs);
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X / 2, vp.WorkPos.Y + vp.WorkSize.Y - pad), ImGuiCond.Always, new Vector2(0.5f, 1));
        ImGui.SetNextWindowBgAlpha(0.8f);
        if (ImGui.Begin("##selected", Hud))
        {
            double heading = Earth.Heading(p, f), kn = s.Ships.Speed[k] / Handling.MsPerKnot;
            string orders = s.Ships.Helm[k] switch
            {
                Helm.Course => $"course {s.Ships.CourseOrderDeg[k]:000}°",
                Helm.Route when s.Ships.RouteLoop[k] => $"patrol round {s.Ships.RouteCount[k]} waypoints",
                Helm.Route => $"route, {s.Ships.RouteCount[k]} waypoint{(s.Ships.RouteCount[k] == 1 ? "" : "s")} to go",
                Helm.Rudder => $"rudder held {s.Ships.Rudder[k] * 180 / Math.PI:+0;-0}°",
                _ => "no helm orders",
            };
            string named = world.NameOf(selected) is { } nm ? $"{nm}, " : "";
            ImGui.Text($"{named}{cls?.Name ?? "?"}  #{selected}  side {s.Ships.Side[k]}{(s.Ships.OnRails[k] ? "  (on rails)" : "")}");
            ImGui.SameLine();
            ImGui.TextDisabled($"heading {heading:000}°, {kn:0.0} kn, rudder {s.Ships.Rudder[k] * 180 / Math.PI:+0;-0;0}°  ·  " +
                               $"ordered: {orders}, {s.Ships.SpeedOrderKn[k]:0.#} kn");
            // never an unexplained deviation: the order stands, the helm follows the coast (docs/world/terrain-plan.md)
            if (s.Ships.LandSide[k] != 0)
                ImGui.TextColored(SlideColor, $"altered for land: following the coast, land to {(s.Ships.LandSide[k] > 0 ? "port" : "starboard")}, until the ordered course is clear");
            if (s.Ships.Backstops[k] > 0)
                ImGui.TextDisabled($"put back off the land {s.Ships.Backstops[k]}x");

            // orders: to the ship, or to a group it's in (one order moves the group: its admiral hands it down)
            LevelRow(s);
            var (unit, cmd) = Commanded(s);
            int kc = s.Ships.IndexOf(cmd);
            var o = world.Ai.Orders;

            // course
            ImGui.SetNextItemWidth(70 * UiFonts.Scale);
            ImGui.InputInt("##course", ref courseEdit, 0);
            courseEdit = ((courseEdit % 360) + 360) % 360;
            ImGui.SameLine();
            if (ImGui.Button("Steer"))
                world.Send(o.Course(unit, courseEdit, hardOver ? 90 : 0));
            foreach (int d in CourseSteps)
            {
                ImGui.SameLine();
                if (ImGui.Button($"{d:+0;-0}°"))
                {
                    courseEdit = (int)Math.Round(((s.Ships.CourseOrderDeg[kc] + d) % 360 + 360) % 360);
                    world.Send(o.Course(unit, courseEdit, hardOver ? 90 : 0));
                }
            }
            ImGui.SameLine();
            ImGui.Checkbox("hard over", ref hardOver);
            if (s.Ships.RouteCount[kc] > 0)
            {
                ImGui.SameLine();
                if (ImGui.Button("Clear route"))
                    world.Send(o.ClearRoute(unit));
            }

            // the telegraph
            var guideCls = world.Classes.TryGet(s.Ships.Class[kc]);
            if (guideCls != null)
            {
                double full = guideCls.Motion.MaxSpeedKn;
                double flank = Math.Max(full, guideCls.SpeedAt(guideCls.Motion.RatedShp * Math.Max(guideCls.Motion.OverloadMax, 1)));
                (string Label, double Kn)[] bells =
                    [("Full astern", -full), ("Half astern", -full / 2), ("Stop", 0), ("Slow", full / 4), ("Half", full / 2), ("Full", full), ("Flank", flank)];
                for (int i = 0; i < bells.Length; i++)
                {
                    if (i > 0)
                        ImGui.SameLine();
                    bool rung = Math.Abs(s.Ships.SpeedOrderKn[kc] - bells[i].Kn) < 0.05;
                    if (rung)
                        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                    if (ImGui.Button($"{bells[i].Label}##bell"))
                        world.Send(o.Speed(unit, Math.Round(bells[i].Kn, 2)));
                    if (rung)
                        ImGui.PopStyleColor();
                }
            }
            SearchRow(s, unit, kc);
            CaptainRow(s, unit, kc);
            GroupRow(s);
            ImGui.TextDisabled("right-click the sea: steer for it · shift+right-click: add a waypoint · alt: this ship alone · click elsewhere: deselect");
        }
        ImGui.End();
    }
}
