using System.Numerics;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Map;

/// <summary>The launch screen's opening (docs/map-ui/launch.md "The opening"), the screen's one work surface drawn as a
/// technical drawing (docs/ui-language.md): the scenario's start on the plan ground, each top unit at its place with its
/// ships on their stations (<see cref="ScenarioLoader.Stations"/>, as the world places them), its course for 30 minutes
/// dashed, the player's side blue rings and the other red diamonds, the coast from the 1:50M land in plan lines, a
/// ruler, the labels and the range between the sides as tags; its overlays (C courses, R range, L labels, G the grid)
/// toggled in its title strip. Made once per pick, side and frame (the land brought into the view's cap, as
/// <see cref="LandMesh"/> does a face's, and filled by rows by its winding number, which the retraction keeps: its rings
/// aren't simple polygons, so no triangulation of them is), then drawn from the arrays every frame. And the sides'
/// ensigns, drawn.</summary>
public sealed unsafe partial class LaunchScene
{
    readonly Land land;
    readonly CapRetraction cap = new();
    OpeningView? opening;
    bool showCourses = true, showRange = true, showLabels = true, showGrid = true;

    sealed class UnitMark
    {
        public Vector2 Head, Lead;
        public Vector2[] Ships = [];
        public bool Ring;   // a ring (the player's, or side 0 when watching), else a diamond
        public Vector4 Colour;
        public string Label = "";
        public Vector2 LabelAt;
        public bool LabelShown;
    }

    sealed class OpeningView
    {
        public Vector2 At, Size;
        // the projection: gnomonic on the tangent plane at C, framed about (Mx, My) at Mpp metres a pixel
        public Vec3d C, East, North;
        public double Mx, My, Mpp;
        public readonly List<(ScenarioUnit Unit, Vec3d Head, Vec3d[] Ships)> Placed = [];

        public (double X, double Y) Plane(Vec3d p)
        {
            var q = p.Normalized();
            double k = Vec3d.Dot(q, C);
            return (Vec3d.Dot(q, East) / k * Earth.Radius, Vec3d.Dot(q, North) / k * Earth.Radius);
        }

        public Vector2 Of(Vec3d p)
        {
            var (x, y) = Plane(p);
            return new((float)(At.X + Size.X / 2 + (x - Mx) / Mpp), (float)(At.Y + Size.Y / 2 - (y - My) / Mpp));
        }

        /// <summary>The point on the sea under a pixel.</summary>
        public Vec3d From(Vector2 s)
        {
            double x = (s.X - At.X - Size.X / 2) * Mpp + Mx, y = -(s.Y - At.Y - Size.Y / 2) * Mpp + My;
            return (C + East * (x / Earth.Radius) + North * (y / Earth.Radius)).Normalized() * Earth.Radius;
        }
        public readonly List<Vector2[]> Coast = [], Grid = [];
        public readonly List<(Vector2 A, Vector2 B)> Fill = [];   // the land's spans, a pixel row each
        public readonly List<UnitMark> Units = [];
        public float BarPx;
        public string BarLabel = "", NorthTag = "", Range = "";
        public Vector2 RangeA, RangeB;
        public (string Text, Vector4 Colour, bool Ring)[] Keys = [];
    }

    const string OpeningNote = "each top unit at its start, its ships astern";
    const string KeyCourse = "dashed: 30 min";

    /// <summary>The opening's pane: the title strip with the overlays' toggles, the plan under it (the placing view's bar
    /// over it while a unit is placed).</summary>
    void OpeningPane(Vector2 a, Vector2 size)
    {
        float right = Pane(a, size, placing != null ? "PLACING A UNIT" : "THE OPENING", placing != null ? placing.Title : OpeningNote);
        if (Toggle("##ovg", ref right, a.Y, "G", "Grid", showGrid))
            showGrid = !showGrid;
        if (Toggle("##ovl", ref right, a.Y, "L", "Labels", showLabels))
            showLabels = !showLabels;
        if (Toggle("##ovr", ref right, a.Y, "R", "Range", showRange))
            showRange = !showRange;
        if (Toggle("##ovc", ref right, a.Y, "C", "Courses", showCourses))
            showCourses = !showCourses;
        Opening(a + new Vector2(1, TitleH + 1), size - new Vector2(2, TitleH + 2));
    }

    void Opening(Vector2 at, Vector2 size)
    {
        var b = at + size;
        dl.AddRectFilled(at, b, U(T.Plan));
        if (current is not { } s)
            return;
        if (opening is not { } v || v.At != at || v.Size != size)
            opening = v = Build(s, at, size);
        dl.PushClipRect(at, b, true);
        if (showGrid)
            foreach (var g in v.Grid)
                dl.AddPolyline(ref g[0], g.Length, U(T.PlanLine with { W = 0.09f }), ImDrawFlags.None, 1);
        uint landCol = U(T.PlanLine with { W = 0.10f });
        foreach (var (a2, b2) in v.Fill)
            dl.AddRectFilled(a2, b2, landCol);
        foreach (var c in v.Coast)
            dl.AddPolyline(ref c[0], c.Length, U(T.PlanLine with { W = 0.45f }), ImDrawFlags.None, 1);
        if (showRange && v.Range.Length > 0)
        {
            Dotted(v.RangeA, v.RangeB, U(T.PlanLine with { W = 0.55f }));
            var ts = TagSize(v.Range);
            Tag((v.RangeA + v.RangeB) / 2 - ts / 2, v.Range);
        }
        foreach (var u in v.Units)
        {
            uint col = U(u.Colour);
            foreach (var p in u.Ships)
                dl.AddCircleFilled(p, P(1.8f), col, 6);
            if (showCourses)
                Dashed(u.Head, u.Lead, U(u.Colour with { W = 0.8f }));
            Mark(u.Head, u.Ring, u.Colour, 6);
        }
        if (showLabels)
            foreach (var u in v.Units)
                if (u.LabelShown)
                    Tag(u.LabelAt, u.Label, u.Ring ? T.TagText : new Vector4(0.945f, 0.788f, 0.765f, 1));
        // the north and the grid's step at the top left, the ruler at the bottom left, the key at the bottom right
        Tag(at + new Vector2(P(8), P(8)), v.NorthTag);
        var bar0 = new Vector2(at.X + P(16), b.Y - P(18));
        uint ruler = U(T.PlanLine with { W = 0.7f });
        dl.AddLine(bar0, bar0 + new Vector2(v.BarPx, 0), ruler);
        dl.AddLine(bar0 - new Vector2(0, P(4)), bar0 + new Vector2(0, P(4)), ruler);
        dl.AddLine(bar0 + new Vector2(v.BarPx, -P(4)), bar0 + new Vector2(v.BarPx, P(4)), ruler);
        Txt(UiFonts.MonoSmall, bar0 + new Vector2(v.BarPx + P(8), -P(7)), T.PlanLine, v.BarLabel);
        Key(v, new Vector2(b.X - P(8), b.Y - P(8)));
        if (placing != null)
            PlacingOverlay(v);
        dl.PopClipRect();
        if (placing != null)
            dl.AddRect(at, b, U(T.Amber));
    }

    /// <summary>The key, a tag at the bottom right (<paramref name="br"/> its corner): each side's mark and name, and what
    /// the dashes are.</summary>
    void Key(OpeningView v, Vector2 br)
    {
        var f = UiFonts.MonoSmall;
        float w = P(6) + Width(f, KeyCourse);
        foreach (var k in v.Keys)
            w += P(14) + Width(f, k.Text) + P(10);
        float h = f.FontSize + P(2);
        var a = br - new Vector2(w, h);
        dl.AddRectFilled(a, br, U(T.TagBg));
        dl.AddRect(a, br, U(T.TagEdge));
        float x = a.X + P(3);
        foreach (var (text, colour, ring) in v.Keys)
        {
            Mark(new Vector2(x + P(5), a.Y + h / 2), ring, colour, 3.5f, 1);
            Txt(f, new Vector2(x + P(13), a.Y + P(1)), colour, text);
            x += P(14) + Width(f, text) + P(10);
        }
        Txt(f, new Vector2(x, a.Y + P(1)), T.TagText, KeyCourse);
    }

    /// <summary>A unit's mark (the map symbols' own): a ring for the player's side, a diamond for the other.</summary>
    void Mark(Vector2 at, bool ring, Vector4 colour, float r, float thick = 1.6f)
    {
        uint col = U(colour);
        if (ring)
            dl.AddCircle(at, P(r), col, 16, P(thick));
        else
            dl.AddQuad(at - new Vector2(0, P(r)), at + new Vector2(P(r), 0), at + new Vector2(0, P(r)), at - new Vector2(P(r), 0), col, P(thick));
    }

    void Dashed(Vector2 a, Vector2 b, uint col) => Dashes(a, b, col, P(4), P(4));

    void Dotted(Vector2 a, Vector2 b, uint col) => Dashes(a, b, col, P(1), P(3));

    void Dashes(Vector2 a, Vector2 b, uint col, float on, float off)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 1)
            return;
        d /= len;
        for (float t = 0; t < len; t += on + off)
            dl.AddLine(a + d * t, a + d * Math.Min(t + on, len), col);
    }

    OpeningView Build(Scenario s, Vector2 at, Vector2 size)
    {
        var v = new OpeningView { At = at, Size = size };
        // every top unit's ships on their stations and its course for half an hour, as points on the sphere (metres)
        var units = new List<(ScenarioUnit Unit, Vec3d Head, Vec3d Lead, List<Vec3d> Ships)>();
        foreach (var u in s.Units)
        {
            if (u.At is not { } p0 || u.ShipCount == 0)
                continue;
            var head = Earth.Position(p0.Lat, p0.Lon);
            var fwd = Earth.Forward(head, u.CourseDeg);
            var starboard = Vec3d.Cross(fwd, head.Normalized());
            var ships = ScenarioLoader.Stations(u).Select(st => (head - fwd * st.AsternM + starboard * st.AbeamM).Normalized() * Earth.Radius).ToList();
            var lead = (head + fwd * (u.Kn * 1852 * 0.5)).Normalized() * Earth.Radius;
            units.Add((u, head, lead, ships));
            v.Placed.Add((u, head, [.. ships]));
        }
        if (units.Count == 0)
            return v;
        // the view: the tangent plane at the units' middle, gnomonic, framing them all
        var c = Vec3d.Zero;
        foreach (var (_, _, _, ships) in units)
            foreach (var p in ships)
                c += p.Normalized();
        c = c.Normalized();
        var east = Vec3d.Cross(new Vec3d(0, 0, 1), c);
        east = east.Length < 1e-9 ? new Vec3d(1, 0, 0) : east.Normalized();
        var north = Vec3d.Cross(c, east);
        (v.C, v.East, v.North) = (c, east, north);
        var Plane = v.Plane;
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        void Grow((double X, double Y) q) => (x0, x1, y0, y1) = (Math.Min(x0, q.X), Math.Max(x1, q.X), Math.Min(y0, q.Y), Math.Max(y1, q.Y));
        foreach (var (_, head, lead, ships) in units)
        {
            Grow(Plane(lead));
            foreach (var p in ships)
                Grow(Plane(p));
        }
        double margin = P(placing != null ? 110 : 60);   // the placing view: room round the units to place one
        double mpp = Math.Max(Math.Max((x1 - x0) / (size.X - 2 * margin), (y1 - y0) / (size.Y - 2 * margin)), 15 / UiFonts.Scale);
        double mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
        (v.Mx, v.My, v.Mpp) = (mx, my, mpp);
        var Of = v.Of;

        // the land: each ring near the view brought into its cap, projected, clipped to the frame
        double halfDiag = Math.Sqrt(size.X * size.X + size.Y * size.Y) / 2 * mpp;
        var mid = (c * Earth.Radius + east * mx + north * my).Normalized();
        cap.Set(mid, Math.Cos(Math.Min(halfDiag * 1.5 / Earth.Radius + 0.01, 1.2)));
        double stepCos = Math.Cos(mpp * 1.5 / Earth.Radius);
        var edges = new List<(double X0, double Y0, double X1, double Y1)>();   // every ring's, screen pixels
        for (int i = 0; i < land.RingCount; i++)
        {
            var (rc, rcos) = land.Cap(i);
            if (cap.Clear(rc, rcos))
                continue;
            int n = cap.Retract(land, i, stepCos);
            if (n < 3)
                continue;
            var pts = cap.Points;
            var run = new List<Vector2>();
            var last = Of(pts[n - 1].P);
            for (int k = 0; k < n; k++)
            {
                var sp = Of(pts[k].P);
                edges.Add((last.X, last.Y, sp.X, sp.Y));
                last = sp;
                if (pts[k].Mask == 0)
                    run.Add(sp);
                else if (run.Count > 0)
                    Coast(v, run);
            }
            Coast(v, run);
        }
        // the fill: each pixel row's crossings, by the winding number (land where it isn't 0)
        var cross = new List<(double X, int Dir)>();
        for (float y = MathF.Floor(at.Y); y < at.Y + size.Y; y++)
        {
            double yc = y + 0.5;
            cross.Clear();
            foreach (var (ex0, ey0, ex1, ey1) in edges)
                if (ey0 <= yc != ey1 <= yc)
                    cross.Add((ex0 + (yc - ey0) / (ey1 - ey0) * (ex1 - ex0), ey1 > ey0 ? 1 : -1));
            cross.Sort((a, b) => a.X.CompareTo(b.X));
            int wind = 0;
            double from = 0;
            foreach (var (x, dir) in cross)
            {
                if (wind == 0)
                    from = x;   // the winding leaves 0: land from here
                wind += dir;
                if (wind != 0)
                    continue;
                double xa = Math.Max(from, at.X), xb = Math.Min(x, at.X + size.X);
                if (xb > xa)
                    v.Fill.Add((new Vector2((float)xa, y), new Vector2((float)xb, y + 1)));
            }
        }

        // the grid: a round step of latitude and longitude, some 80 px apart
        double degPx = 111_195 / mpp;
        double stepDeg = new[] { 1 / 6.0, 0.5, 1, 2, 5, 10 }.FirstOrDefault(d => d * degPx >= 70, 10);
        var (cl, cn) = Earth.LatLon(mid);
        double spanLat = halfDiag / 111_195 * 1.2, spanLon = spanLat / Math.Max(Math.Cos(cl * Math.PI / 180), 0.2);
        for (double la = Math.Floor((cl - spanLat) / stepDeg) * stepDeg; la <= cl + spanLat; la += stepDeg)
            v.Grid.Add([.. Enumerable.Range(0, 25).Select(k => Of(Earth.Position(la, cn - spanLon + 2 * spanLon * k / 24)))]);
        for (double lo = Math.Floor((cn - spanLon) / stepDeg) * stepDeg; lo <= cn + spanLon; lo += stepDeg)
            v.Grid.Add([.. Enumerable.Range(0, 25).Select(k => Of(Earth.Position(cl - spanLat + 2 * spanLat * k / 24, lo)))]);
        v.NorthTag = $"N ↑ · grid {(stepDeg < 1 ? $"{stepDeg * 60:0}′" : $"{stepDeg:0}°")}";

        // the units: the player's side rings, the other diamonds (watching: side 0 rings); their tags where they fit,
        // off their heads away from their courses
        var placed = new List<(Vector2 A, Vector2 B)>();
        var heads = units.Select(u => Of(u.Head)).ToList();
        foreach (var (u, head, lead, ships) in units)
        {
            bool own = side >= 0 ? u.Side == side : u.Side == 0;
            var m = new UnitMark
            {
                Head = Of(head), Lead = Of(lead), Ships = [.. ships.Select(Of)], Ring = own,
                Colour = own ? T.Blue : T.Hostile, Label = $"{u.Name} · {u.ShipCount}",
            };
            var ts = TagSize(m.Label);
            // the first of the four places round her head that's in the frame and clear of the others' tags and heads
            Vector2[] tries =
            [
                m.Head + new Vector2(P(10), P(4)), m.Head + new Vector2(-P(10) - ts.X, P(4)),
                m.Head + new Vector2(P(10), -P(4) - ts.Y), m.Head + new Vector2(-P(10) - ts.X, -P(4) - ts.Y),
            ];
            foreach (var a in tries)
            {
                var bb = a + ts;
                bool inside = a.X > at.X + P(4) && bb.X < at.X + size.X - P(4) && a.Y > at.Y + P(24) && bb.Y < at.Y + size.Y - P(24);
                bool clear = !placed.Any(r => a.X < r.B.X && bb.X > r.A.X && a.Y < r.B.Y && bb.Y > r.A.Y)
                             && !heads.Any(hp => hp.X > a.X - P(4) && hp.X < bb.X + P(4) && hp.Y > a.Y - P(4) && hp.Y < bb.Y + P(4));
                if (inside && clear)
                {
                    (m.LabelAt, m.LabelShown) = (a, true);
                    placed.Add((a, bb));
                    break;
                }
            }
            v.Units.Add(m);
        }

        // the ruler: a round length, at most 120 px
        double km = new[] { 200.0, 100, 50, 20, 10, 5, 2, 1 }.FirstOrDefault(k => k * 1000 / mpp <= P(120), 1);
        v.BarPx = (float)(km * 1000 / mpp);
        v.BarLabel = $"{km:0} km";
        // the range: between the nearest heads of sides at war
        double apart = double.MaxValue;
        foreach (var a in units)
            foreach (var bu in units)
                if (a.Unit.Side < bu.Unit.Side && !s.Peace.Contains((a.Unit.Side, bu.Unit.Side)) && !s.Peace.Contains((bu.Unit.Side, a.Unit.Side))
                    && Earth.Distance(a.Head, bu.Head) is var d && d < apart)
                    (apart, v.RangeA, v.RangeB) = (d, Of(a.Head), Of(bu.Head));
        v.Range = apart < double.MaxValue ? $"{apart / 1000:0.0} km" : "";
        int me = side >= 0 ? side : 0, them = s.Sides.Length > 1 ? (me == 0 ? 1 : 0) : -1;
        v.Keys = them < 0 ? [(s.Sides[me], T.Blue, true)] : [(s.Sides[me], T.Blue, true), (s.Sides[them], T.Hostile, false)];
        return v;
    }

    /// <summary>A run of the coast (the ring's own points, not the cap's edge) kept if it's long enough to show.</summary>
    static void Coast(OpeningView v, List<Vector2> run)
    {
        if (run.Count >= 2)
            v.Coast.Add([.. run]);
        run.Clear();
    }

    static Vector4 Hex(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);

    // ------------------------------------------------------------------ the ensigns

    static readonly Vector4 White = Hex(0xF4F4F0), EnsignBlue = Hex(0x2D56A8), EnsignRed = Hex(0xC9473C), UnionBlue = Hex(0x23306B),
        Black = Hex(0x15171A), ItalyGreen = Hex(0x2E8B4F), FinlandBlue = Hex(0x1F4A8A), ArmsGold = Hex(0xD4A537),
        SwedenBlue = Hex(0x1F5FA0), SwedenYellow = Hex(0xF2C230);

    /// <summary>The swallow-tailed ensign's outline, filled each draw (no garbage).</summary>
    readonly Vector2[] tails = new Vector2[8];

    /// <summary>A Nordic war ensign: the cross toward the hoist on the field, the fly cut in three tails (the middle
    /// one the cross's arm), outlined along the cut; with the arms, a red square rimmed gold at the crossing.</summary>
    void Tailed(Vector2 at, Vector2 size, Vector4 field, Vector4 cross, bool arms)
    {
        var b = at + size;
        float w = size.X, h = size.Y;
        float xr = at.X + w * 0.74f, t0 = at.Y + h * 0.35f, t1 = at.Y + h * 0.65f;
        dl.AddRectFilled(at, new Vector2(xr, b.Y), U(field));
        dl.AddTriangleFilled(new Vector2(xr, at.Y), new Vector2(b.X, at.Y), new Vector2(xr, t0), U(field));
        dl.AddRectFilled(new Vector2(xr, t0), new Vector2(b.X, t1), U(field));
        dl.AddTriangleFilled(new Vector2(xr, t1), new Vector2(b.X, b.Y), new Vector2(xr, b.Y), U(field));
        float cx = at.X + w * 0.3f, cy = at.Y + h * 0.5f, hb = h * 0.13f;
        dl.AddRectFilled(new Vector2(cx - hb, at.Y), new Vector2(cx + hb, b.Y), U(cross));
        dl.AddRectFilled(new Vector2(at.X, cy - hb), new Vector2(b.X, cy + hb), U(cross));
        if (arms)
        {
            var hs = new Vector2(h * 0.17f, h * 0.17f);
            dl.AddRectFilled(new Vector2(cx, cy) - hs, new Vector2(cx, cy) + hs, U(ArmsGold));
            dl.AddRectFilled(new Vector2(cx, cy) - hs * 0.7f, new Vector2(cx, cy) + hs * 0.7f, U(EnsignRed));
        }
        tails[0] = at; tails[1] = new Vector2(b.X, at.Y); tails[2] = new Vector2(xr, t0); tails[3] = new Vector2(b.X, t0);
        tails[4] = new Vector2(b.X, t1); tails[5] = new Vector2(xr, t1); tails[6] = b; tails[7] = new Vector2(at.X, b.Y);
        dl.AddPolyline(ref tails[0], tails.Length, U(new Vector4(0, 0, 0, 1)), ImDrawFlags.Closed, 1);
    }

    /// <summary>A side's ensign, small (a card's 26 × 16): drawn, not an image, for the navies the scenarios have
    /// (britain, germany, russia, japan, france, italy, ottoman, finland, sweden); a grey field for any other.</summary>
    void Ensign(string name, Vector2 at, Vector2 size)
    {
        var b = at + size;
        float w = size.X, h = size.Y;
        dl.PushClipRect(at, b, true);
        switch (name)
        {
            case "russia":   // St Andrew's: a blue saltire on white
                dl.AddRectFilled(at, b, U(White));
                dl.AddLine(at, b, U(EnsignBlue), h * 0.2f);
                dl.AddLine(new Vector2(at.X, b.Y), new Vector2(b.X, at.Y), U(EnsignBlue), h * 0.2f);
                break;
            case "japan":   // the rising sun: a red disc toward the hoist, sixteen rays
                {
                    dl.AddRectFilled(at, b, U(White));
                    var o = at + new Vector2(w * 0.42f, h * 0.5f);
                    for (int k = 0; k < 16; k += 2)
                    {
                        float a0 = k * MathF.PI / 8, a1 = (k + 1) * MathF.PI / 8, r = w * 1.2f;
                        dl.AddTriangleFilled(o, o + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r, o + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r, U(EnsignRed));
                    }
                    dl.AddCircleFilled(o, h * 0.32f, U(EnsignRed), 16);
                    break;
                }
            case "britain":   // the white ensign: St George's cross, the union in the canton
                {
                    dl.AddRectFilled(at, b, U(White));
                    var m = at + size / 2;
                    dl.AddLine(new Vector2(at.X, m.Y), new Vector2(b.X, m.Y), U(EnsignRed), h * 0.2f);
                    dl.AddLine(new Vector2(m.X, at.Y), new Vector2(m.X, b.Y), U(EnsignRed), h * 0.2f);
                    var cb = m;
                    dl.AddRectFilled(at, cb, U(UnionBlue));
                    dl.PushClipRect(at, cb, true);
                    dl.AddLine(at, cb, U(White), h * 0.1f);
                    dl.AddLine(new Vector2(at.X, cb.Y), new Vector2(cb.X, at.Y), U(White), h * 0.1f);
                    var cm = (at + cb) / 2;
                    dl.AddLine(new Vector2(at.X, cm.Y), new Vector2(cb.X, cm.Y), U(White), h * 0.14f);
                    dl.AddLine(new Vector2(cm.X, at.Y), new Vector2(cm.X, cb.Y), U(White), h * 0.14f);
                    dl.AddLine(new Vector2(at.X, cm.Y), new Vector2(cb.X, cm.Y), U(EnsignRed), h * 0.07f);
                    dl.AddLine(new Vector2(cm.X, at.Y), new Vector2(cm.X, cb.Y), U(EnsignRed), h * 0.07f);
                    dl.PopClipRect();
                    break;
                }
            case "germany":   // the imperial war ensign: a black cross and roundel on white, black-white-red in the canton
                {
                    dl.AddRectFilled(at, b, U(White));
                    var m = at + new Vector2(w * 0.5f, h * 0.5f);
                    dl.AddLine(new Vector2(at.X, m.Y), new Vector2(b.X, m.Y), U(Black), h * 0.14f);
                    dl.AddLine(new Vector2(m.X, at.Y), new Vector2(m.X, b.Y), U(Black), h * 0.14f);
                    dl.AddCircleFilled(m, h * 0.2f, U(Black), 12);
                    dl.AddCircleFilled(m, h * 0.12f, U(White), 12);
                    float cw = w * 0.36f, ch = h * 0.4f;
                    dl.AddRectFilled(at, at + new Vector2(cw, ch / 3), U(Black));
                    dl.AddRectFilled(at + new Vector2(0, ch / 3), at + new Vector2(cw, 2 * ch / 3), U(White));
                    dl.AddRectFilled(at + new Vector2(0, 2 * ch / 3), at + new Vector2(cw, ch), U(EnsignRed));
                    break;
                }
            case "france":   // the tricolour: blue, white, red from the hoist
                dl.AddRectFilled(at, at + new Vector2(w / 3, h), U(EnsignBlue));
                dl.AddRectFilled(at + new Vector2(w / 3, 0), at + new Vector2(2 * w / 3, h), U(White));
                dl.AddRectFilled(at + new Vector2(2 * w / 3, 0), b, U(EnsignRed));
                break;
            case "italy":   // the tricolour with Savoy's shield: a white cross on red, bordered blue
                {
                    dl.AddRectFilled(at, at + new Vector2(w / 3, h), U(ItalyGreen));
                    dl.AddRectFilled(at + new Vector2(w / 3, 0), at + new Vector2(2 * w / 3, h), U(White));
                    dl.AddRectFilled(at + new Vector2(2 * w / 3, 0), b, U(EnsignRed));
                    var m = at + new Vector2(w * 0.5f, h * 0.5f);
                    var hs = new Vector2(w * 0.11f, h * 0.24f);
                    dl.AddRectFilled(m - hs, m + hs, U(EnsignBlue));
                    dl.AddRectFilled(m - hs * 0.75f, m + hs * 0.75f, U(EnsignRed));
                    dl.AddLine(m - new Vector2(hs.X * 0.75f, 0), m + new Vector2(hs.X * 0.75f, 0), U(White), h * 0.07f);
                    dl.AddLine(m - new Vector2(0, hs.Y * 0.75f), m + new Vector2(0, hs.Y * 0.75f), U(White), h * 0.07f);
                    break;
                }
            case "ottoman":   // a white crescent and star on red
                {
                    dl.AddRectFilled(at, b, U(EnsignRed));
                    var o = at + new Vector2(w * 0.4f, h * 0.5f);
                    dl.AddCircleFilled(o, h * 0.3f, U(White), 20);
                    dl.AddCircleFilled(o + new Vector2(h * 0.08f, 0), h * 0.25f, U(EnsignRed), 20);
                    var s = o + new Vector2(h * 0.36f, 0);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = MathF.PI + k * MathF.Tau / 5, r = h * 0.14f;
                        var tip = s + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                        var l = s + new Vector2(MathF.Cos(a - MathF.PI / 2), MathF.Sin(a - MathF.PI / 2)) * r * 0.32f;
                        var rr = s + new Vector2(MathF.Cos(a + MathF.PI / 2), MathF.Sin(a + MathF.PI / 2)) * r * 0.32f;
                        dl.AddTriangleFilled(tip, l, rr, U(White));
                    }
                    break;
                }
            case "finland":   // the war ensign: a blue Nordic cross on white, the arms at the crossing, three tails
                dl.PopClipRect();
                Tailed(at, size, White, FinlandBlue, arms: true);
                return;
            case "sweden":   // the war ensign: a yellow Nordic cross on blue, three tails
                dl.PopClipRect();
                Tailed(at, size, SwedenBlue, SwedenYellow, arms: false);
                return;
            default:
                dl.AddRectFilled(at, b, U(Hex(0x2A3138)));
                break;
        }
        dl.PopClipRect();
        dl.AddRect(at - Vector2.One, b + Vector2.One, U(new Vector4(0, 0, 0, 1)));
    }
}
