using System.Numerics;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The launch screen's opening (docs/map-ui/launch.md "The opening"): the scenario's start on a framed map, each
/// top unit at its place with its ships on their stations (<see cref="ScenarioLoader.Stations"/>, as the world places
/// them), its course for 30 minutes dashed, the player's side blue rings and the other red diamonds, the coast from the
/// 1:50M land. Made once per pick, side and frame (the land brought into the view's cap, as <see cref="LandMesh"/>
/// does a face's, and filled by rows by its winding number, which the retraction keeps: its rings aren't simple
/// polygons, so no triangulation of them is), then drawn from the arrays every frame. And the sides' ensigns, drawn.</summary>
public sealed unsafe partial class LaunchScene
{
    readonly Land land;
    readonly CapRetraction cap = new();
    OpeningView? opening;

    sealed class UnitMark
    {
        public Vector2 Head, Lead;
        public Vector2[] Ships = [];
        public bool Ring;   // a ring (the player's, or side 0 when watching), else a diamond
        public Vector4 Colour;
        public string Label = "";
        public bool LabelLeft, LabelShown;
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
        public string BarLabel = "", Legend = "";
        public (string Text, Vector4 Colour, bool Ring)[] Keys = [];
    }

    void Opening(Vector2 at, Vector2 size)
    {
        if (current is not { } s)
            return;
        if (opening is not { } v || v.At != at || v.Size != size)
            opening = v = Build(s, at, size);
        var b = at + size;
        dl.AddRectFilled(at, b, U(Sea), 3 * sc);
        dl.PushClipRect(at + Vector2.One, b - Vector2.One, true);
        foreach (var g in v.Grid)
            dl.AddPolyline(ref g[0], g.Length, U(new Vector4(160 / 255f, 190 / 255f, 210 / 255f, 0.08f)), ImDrawFlags.None, 1);
        uint landCol = U(LandC);
        foreach (var (a, b2) in v.Fill)
            dl.AddRectFilled(a, b2, landCol);
        foreach (var c in v.Coast)
            dl.AddPolyline(ref c[0], c.Length, U(Hex(0x4A5240)), ImDrawFlags.None, 1);
        foreach (var u in v.Units)
        {
            uint col = U(u.Colour);
            foreach (var p in u.Ships)
                dl.AddCircleFilled(p, 1.6f * sc, col, 6);
            Dashed(u.Head, u.Lead, U(u.Colour with { W = 0.7f }));
            Mark(u.Head, u.Ring, u.Colour, 6.5f);
        }
        foreach (var u in v.Units)
            if (u.LabelShown)
            {
                float tw = Width(UiFonts.SansSmall, 11, u.Label);
                var p = u.LabelLeft ? u.Head + new Vector2(-10 * sc - tw, 4 * sc) : u.Head + new Vector2(10 * sc, 4 * sc);
                Txt(UiFonts.SansSmall, 11, p, Hex(0xDFE7EC), u.Label);
            }
        // the caption, the scale, the key
        Tracked(UiFonts.SansSmall, 10.5f, at + new Vector2(8 * sc, 6 * sc), Hex(0x9FB1BF), "THE OPENING", 0.8f);
        var bar0 = new Vector2(at.X + 12 * sc, b.Y - 12 * sc);
        uint scaleCol = U(Hex(0x9FB1BF));
        dl.AddLine(bar0, bar0 + new Vector2(v.BarPx, 0), scaleCol);
        dl.AddLine(bar0 - new Vector2(0, 3 * sc), bar0 + new Vector2(0, 3 * sc), scaleCol);
        dl.AddLine(bar0 + new Vector2(v.BarPx, -3 * sc), bar0 + new Vector2(v.BarPx, 3 * sc), scaleCol);
        Txt(UiFonts.SansSmall, 11, bar0 + new Vector2(v.BarPx + 6 * sc, -8 * sc), Hex(0x9FB1BF), v.BarLabel);
        float kx = b.X - 8 * sc - Width(UiFonts.SansSmall, 11, v.Legend);
        Txt(UiFonts.SansSmall, 11, new Vector2(kx, b.Y - 20 * sc), Hex(0x9FB1BF), v.Legend);
        for (int k = v.Keys.Length - 1; k >= 0; k--)
        {
            kx -= Width(UiFonts.SansSmall, 11, v.Keys[k].Text) + 22 * sc;
            Mark(new Vector2(kx + 5 * sc, b.Y - 12 * sc), v.Keys[k].Ring, v.Keys[k].Colour, 3.5f, 1);
            Txt(UiFonts.SansSmall, 11, new Vector2(kx + 12 * sc, b.Y - 20 * sc), v.Keys[k].Colour, v.Keys[k].Text);
        }
        if (placing != null)
            PlacingOverlay(v);
        dl.PopClipRect();
        dl.AddRect(at, b, U(placing != null ? Amber : EdgeC), 3 * sc);
    }

    /// <summary>A unit's mark (the map symbols' own): a ring for the player's side, a diamond for the other.</summary>
    void Mark(Vector2 at, bool ring, Vector4 colour, float r, float thick = 1.5f)
    {
        uint col = U(colour);
        if (ring)
            dl.AddCircle(at, r * sc, col, 16, thick * sc);
        else
            dl.AddQuad(at - new Vector2(0, r * sc), at + new Vector2(r * sc, 0), at + new Vector2(0, r * sc), at - new Vector2(r * sc, 0), col, thick * sc);
    }

    void Dashed(Vector2 a, Vector2 b, uint col)
    {
        var d = b - a;
        float len = d.Length(), step = 3 * sc;
        if (len < 1)
            return;
        d /= len;
        for (float t = 0; t < len; t += 2 * step)
            dl.AddLine(a + d * t, a + d * Math.Min(t + step, len), col);
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
        double margin = (placing != null ? 110 : 50) * sc;   // the placing view: room round the units to place one
        double mpp = Math.Max(Math.Max((x1 - x0) / (size.X - 2 * margin), (y1 - y0) / (size.Y - 2 * margin)), 15 / sc);
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

        // the units: the player's side rings, the other diamonds (watching: side 0 rings); labels where they fit
        var placed = new List<(Vector2 A, Vector2 B)>();
        foreach (var (u, head, lead, ships) in units)
        {
            bool own = side >= 0 ? u.Side == side : u.Side == 0;
            var m = new UnitMark
            {
                Head = Of(head), Lead = Of(lead), Ships = [.. ships.Select(Of)], Ring = own,
                Colour = side < 0 ? (u.Side == 0 ? Hex(0x9DBFE6) : Hostile) : own ? Friend : Hostile,
                Label = u.Name, LabelLeft = own,
            };
            float tw = Width(UiFonts.SansSmall, 11, m.Label), th = 14 * sc;
            var a = m.LabelLeft ? m.Head + new Vector2(-10 * sc - tw, 4 * sc) : m.Head + new Vector2(10 * sc, 4 * sc);
            var bb = a + new Vector2(tw, th);
            m.LabelShown = a.X > at.X && bb.X < at.X + size.X && !placed.Any(r => a.X < r.B.X && bb.X > r.A.X && a.Y < r.B.Y && bb.Y > r.A.Y);
            if (m.LabelShown)
                placed.Add((a, bb));
            v.Units.Add(m);
        }

        // the scale: a round length, at most 120 px
        double km = new[] { 200.0, 100, 50, 20, 10, 5, 2, 1 }.FirstOrDefault(k => k * 1000 / mpp <= 120 * sc, 1);
        v.BarPx = (float)(km * 1000 / mpp);
        v.BarLabel = $"{km:0} km";
        // the key: the sides' marks, and how far apart they open
        double apart = double.MaxValue;
        foreach (var a in units)
            foreach (var bu in units)
                if (a.Unit.Side < bu.Unit.Side)
                    apart = Math.Min(apart, Earth.Distance(a.Head, bu.Head));
        int me = side >= 0 ? side : 0, them = s.Sides.Length > 1 ? (me == 0 ? 1 : 0) : -1;
        v.Keys = them < 0 ? [(s.Sides[me], Friend, true)]
            : [(s.Sides[me], side < 0 ? Hex(0x9DBFE6) : Friend, true), (s.Sides[them], Hostile, false)];
        v.Legend = (apart < double.MaxValue ? $"{apart / 1000:0} km apart · " : "") + "dashed: 30 min";
        return v;
    }

    /// <summary>A run of the coast (the ring's own points, not the cap's edge) kept if it's long enough to show.</summary>
    static void Coast(OpeningView v, List<Vector2> run)
    {
        if (run.Count >= 2)
            v.Coast.Add([.. run]);
        run.Clear();
    }

    // ------------------------------------------------------------------ the ensigns

    static readonly Vector4 White = Hex(0xF4F4F0), EnsignBlue = Hex(0x2D56A8), EnsignRed = Hex(0xC9473C), UnionBlue = Hex(0x23306B),
        Black = Hex(0x15171A);

    /// <summary>A side's ensign, small (a card's 26 × 16): drawn, not an image, for the navies the scenarios have
    /// (britain, germany, russia, japan); a grey field for any other.</summary>
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
            default:
                dl.AddRectFilled(at, b, U(Hex(0x2A3138)));
                break;
        }
        dl.PopClipRect();
        dl.AddRect(at - Vector2.One, b + Vector2.One, U(new Vector4(0, 0, 0, 1)));
    }
}
