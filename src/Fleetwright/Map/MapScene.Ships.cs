using System.Numerics;
using Fleetwright.Sim;
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

    static readonly string[] CaptainNames = ["Player's orders", "Keep course", "Follow", "Run from"];

    /// <summary>A short wake of positions behind each ship, sampled in game time; made when a ship is first seen.</summary>
    sealed class Track
    {
        public readonly Vec3d[] Points = new Vec3d[TrackPoints];
        public int Count, Head;
        public long LastMs;
        public long Seen;
    }

    static readonly int[] CourseSteps = [-90, -10, 10, 90];
    const int TrackPoints = 240;
    const long TrackEveryMs = 10_000;   // 40 minutes of track
    readonly Dictionary<int, Track> tracks = [];
    readonly List<int> gone = [];
    long frame;

    static readonly Vector4 TrackColor = new(0.85f, 0.9f, 1f, 0.35f), RouteColor = new(1f, 0.85f, 0.4f, 0.55f),
        RouteSelected = new(1f, 0.8f, 0.3f, 0.95f), SelectColor = new(1f, 0.78f, 0.25f, 0.95f), CourseColor = new(1f, 1f, 1f, 0.6f), TargetColor = new(1f, 0.78f, 0.25f, 0.45f);

    // ------------------------------------------------------------------ lines over the globe

    void BuildOverlay(in MapView view, Snapshot s)
    {
        var v = s.Ships;
        overlay.Begin(width, height);
        frame++;
        for (int k = 0; k < v.Count; k++)
        {
            var (p, f) = v.PoseAt(k, s.Time.Ms);
            int e = v.Entity[k];

            // the track
            if (!tracks.TryGetValue(e, out var tr))
                tracks[e] = tr = new Track();
            tr.Seen = frame;
            if (tr.Count == 0 || s.Time.Ms - tr.LastMs >= TrackEveryMs)
            {
                tr.Points[tr.Head] = p;
                tr.Head = (tr.Head + 1) % TrackPoints;
                tr.Count = Math.Min(tr.Count + 1, TrackPoints);
                tr.LastMs = s.Time.Ms;
            }
            if (tr.Count > 0)
            {
                overlay.BeginStrip(TrackColor, 1.5f);
                for (int i = 0; i < tr.Count; i++)
                    Point(view, tr.Points[(tr.Head - tr.Count + i + TrackPoints) % TrackPoints]);
                Point(view, p);
                overlay.End();
            }

            // the route still to steam
            bool sel = e == selected;
            if (v.RouteCount[k] > 0)
            {
                overlay.BeginStrip(sel ? RouteSelected : RouteColor, sel ? 2f : 1.2f);
                var from = p;
                Point(view, from);
                for (int i = 0; i < v.RouteCount[k]; i++)
                {
                    var to = v.Waypoints[v.RouteStart[k] + i];
                    GreatCircle(view, from, to);
                    from = to;
                }
                if (v.RouteLoop[k])
                    GreatCircle(view, from, v.Waypoints[v.RouteStart[k]]);   // a patrol: round to the first again
                overlay.End();
                for (int i = 0; i < v.RouteCount[k]; i++)
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
                    var (tp, _) = v.PoseAt(t, s.Time.Ms);
                    overlay.BeginStrip(TargetColor, 1f);
                    Point(view, p);
                    Point(view, tp);
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

        // tracks of ships no longer there
        gone.Clear();
        foreach (var (e, tr) in tracks)
            if (tr.Seen != frame)
                gone.Add(e);
        foreach (int e in gone)
            tracks.Remove(e);
        overlay.End();
    }

    void Point(in MapView view, Vec3d p)
    {
        if (view.Project(p, out double x, out double y))
            overlay.Point(x, y);
        else
            overlay.End();
    }

    void GreatCircle(in MapView view, Vec3d a, Vec3d b)
    {
        int n = Math.Clamp((int)(Earth.Distance(a, b) / 20_000), 1, 64);
        for (int i = 1; i <= n; i++)
        {
            double t = (double)i / n;
            Point(view, (a * (1 - t) + b * t).Normalized() * Earth.Radius);
        }
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
            var (p, _) = v.PoseAt(k, s.Time.Ms);
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
        if (waypoint)
        {
            var (lat, lon) = Earth.LatLon(at);
            world.Send(ShipSystem.AddWaypoint(Commanded(s), lat, lon));
        }
        else
        {
            int c = Commanded(s), kc = s.Ships.IndexOf(c);
            var (p, _) = s.Ships.PoseAt(kc, s.Time.Ms);
            world.Send(ShipSystem.ClearRoute(c));
            world.Send(ShipSystem.Course(c, Earth.Bearing(p, at), hardOver ? 90 : 0));
        }
    }

    // ------------------------------------------------------------------ the panels

    /// <summary>The selected ship's captain: who gives its orders (the player, or its captain keeping course, following
    /// or running from another ship), the other ship, and the station's distance.</summary>
    void CaptainRow(Snapshot s, int k)
    {
        var v = s.Ships;
        var order = (CaptainOrder)v.Captain[k];
        int target = v.CaptainTarget[k];
        ImGui.Text("Captain:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(130 * UiFonts.Scale);
        int pick = (int)order;
        if (ImGui.Combo("##captain", ref pick, CaptainNames, CaptainNames.Length) && pick != (int)order)
        {
            var o = (CaptainOrder)pick;
            if (o is CaptainOrder.Follow or CaptainOrder.Run && (target == 0 || v.IndexOf(target) < 0))
                target = Nearest(s, k);
            if (o is CaptainOrder.Follow or CaptainOrder.Run && target == 0)
                o = CaptainOrder.None;   // nobody else at sea to follow or run from
            world.Send(world.Ai.Captain(selected, o, target, followMetres));
        }
        if (order is CaptainOrder.Follow or CaptainOrder.Run)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(170 * UiFonts.Scale);
            if (ImGui.BeginCombo("##target", ShipLabel(s, target)))
            {
                for (int j = 0; j < v.Count; j++)
                    if (j != k && ImGui.Selectable(ShipLabel(s, v.Entity[j]), v.Entity[j] == target))
                        world.Send(world.Ai.Captain(selected, order, v.Entity[j], followMetres));
                ImGui.EndCombo();
            }
        }
        if (order == CaptainOrder.Follow)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * UiFonts.Scale);
            if (ImGui.InputInt("m astern", ref followMetres, 100, 500, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                followMetres = Math.Clamp(followMetres, 100, 20_000);
                world.Send(world.Ai.Captain(selected, order, target, followMetres));
            }
        }
    }

    // the search row: the pattern, its first leg's course (or the ship's heading), its sizes in miles, its count of
    // legs (a sector's triangles), and which way it turns
    int searchPattern, searchAxis, searchLegNm = 20, searchSpacingNm = 5, searchLegs = 6;
    bool searchAlongHeading = true, searchStarboard = true;
    Vec3d[] searchWaypoints = [];
    static readonly string[] SearchNames = ["Ladder", "Expanding square", "Sector", "Patrol line"];

    /// <summary>A search pattern for the commanded ship (a group's guide: its group follows), from where it is: the
    /// route's waypoints sent as orders. With the clock fast enough a group on rails steams it on rails, turning at
    /// once at each waypoint.</summary>
    void SearchRow(Snapshot s, int cmd, int kc)
    {
        float field = 80 * UiFonts.Scale;
        var pattern = (SearchPattern)searchPattern;
        ImGui.Text("Search:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140 * UiFonts.Scale);
        ImGui.Combo("##search", ref searchPattern, SearchNames, SearchNames.Length);
        var (p, f) = s.Ships.PoseAt(kc, s.Time.Ms);
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
            SearchPatterns.Orders(cmd, searchWaypoints.AsSpan(0, n), SearchPatterns.Loops(pattern), c => world.Send(c));
        }
    }

    string ShipLabel(Snapshot s, int entity)
    {
        int j = s.Ships.IndexOf(entity);
        return j < 0 ? "-" : $"#{entity} {world.Classes.TryGet(s.Ships.Class[j])?.Name ?? "?"}";
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
            ImGui.TextDisabled($"Ships at sea: {s.Ships.Count}   (dev: spawn)");
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

        // the selected ship: bottom centre
        int k = s.Ships.IndexOf(selected);
        if (k < 0)
            return;
        var cls = world.Classes.TryGet(s.Ships.Class[k]);
        var (p, f) = s.Ships.PoseAt(k, s.Time.Ms);
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
            ImGui.Text($"{cls?.Name ?? "?"}  #{selected}  side {s.Ships.Side[k]}{(s.Ships.OnRails[k] ? "  (on rails)" : "")}");
            ImGui.SameLine();
            ImGui.TextDisabled($"heading {heading:000}°, {kn:0.0} kn, rudder {s.Ships.Rudder[k] * 180 / Math.PI:+0;-0;0}°  ·  " +
                               $"ordered: {orders}, {s.Ships.SpeedOrderKn[k]:0.#} kn");

            // orders: to the ship, or to its group's guide (one order moves the group)
            int cmd = Commanded(s), kc = s.Ships.IndexOf(cmd);
            if (cmd != selected)
                ImGui.TextDisabled($"orders go to the group's guide, #{cmd}");

            // course
            ImGui.SetNextItemWidth(70 * UiFonts.Scale);
            ImGui.InputInt("##course", ref courseEdit, 0);
            courseEdit = ((courseEdit % 360) + 360) % 360;
            ImGui.SameLine();
            if (ImGui.Button("Steer"))
                world.Send(ShipSystem.Course(cmd, courseEdit, hardOver ? 90 : 0));
            foreach (int d in CourseSteps)
            {
                ImGui.SameLine();
                if (ImGui.Button($"{d:+0;-0}°"))
                {
                    courseEdit = (int)Math.Round(((s.Ships.CourseOrderDeg[kc] + d) % 360 + 360) % 360);
                    world.Send(ShipSystem.Course(cmd, courseEdit, hardOver ? 90 : 0));
                }
            }
            ImGui.SameLine();
            ImGui.Checkbox("hard over", ref hardOver);
            if (s.Ships.RouteCount[kc] > 0)
            {
                ImGui.SameLine();
                if (ImGui.Button("Clear route"))
                    world.Send(ShipSystem.ClearRoute(cmd));
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
                        world.Send(ShipSystem.Speed(cmd, Math.Round(bells[i].Kn, 2)));
                    if (rung)
                        ImGui.PopStyleColor();
                }
            }
            SearchRow(s, cmd, kc);
            if (cmd == selected)
                CaptainRow(s, k);
            else
                ImGui.TextDisabled("Captain: keeping station in the group");
            GroupRow(s);
            ImGui.TextDisabled("right-click the sea: steer for it · shift+right-click: add a waypoint · click elsewhere: deselect");
        }
        ImGui.End();
    }
}
