using System.Text;

namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>verify (shipgen's verify.py): hitboxes and sprites agree, pixel for pixel, on `shipgen design` output.
/// Every rotating mount's sprite, turned to four angles and placed at its mount, against its hitbox polygons (body,
/// parts, barrels) the same way: IoU. Raised blocks and funnels must be opaque inside their hitboxes, and the hull
/// image must match the hull outline (plus flight deck and sponsons). Also the subdivision (one owning room per cell,
/// points in the hull each in exactly one cell, mutual neighbours, magazines that are rooms) and the traverses (one
/// interval under a full turn holding rest and arcs, the same in sprite.json; a main turret's barrels swung through it
/// meet nothing taller than their axis; a secondary's stowed barrels lie clear). Pass mark: 0.85.</summary>
public static class Verify
{
    public sealed record Result(double Worst, string Text);

    /// <summary>Pixels in the box x0..x1, y0..y1 covered by any of the polygons (given in metres with origin (ox, oy)
    /// px and S px/m): those whose centres are inside, and those the outline runs through, as PIL's
    /// ImageDraw.polygon also draws the outline. verify.py's 0.85 pass mark was set with it: the sprites' outlines
    /// straddle the hitbox edge, and the polygon's outline takes them in.</summary>
    static bool[] Mask(IEnumerable<List<Pt>> polys, double S, double ox, double oy, int x0, int y0, int x1, int y1)
    {
        int w = x1 - x0, h = y1 - y0;
        var m = new bool[w * h];
        void Set(int x, int y)
        {
            if (x >= x0 && x < x1 && y >= y0 && y < y1)
                m[(y - y0) * w + (x - x0)] = true;
        }
        var cross = new List<double>();
        foreach (var poly in polys)
        {
            var px = poly.Select(p => new Pt(ox + p.X * S, oy + p.Y * S)).ToList();
            int n = px.Count;
            for (int y = y0; y < y1; y++)
            {
                double yc = y + 0.5;
                cross.Clear();
                for (int i = 0; i < n; i++)
                {
                    var (ax, ay) = px[i];
                    var (bx, by) = px[(i + 1) % n];
                    if ((ay > yc) != (by > yc))
                        cross.Add(ax + (yc - ay) * (bx - ax) / (by - ay));
                }
                cross.Sort();
                for (int k = 0; k + 1 < cross.Count; k += 2)
                    for (int x = Math.Max(x0, (int)Math.Ceiling(cross[k] - 0.5)); x <= Math.Min(x1 - 1, (int)Math.Floor(cross[k + 1] - 0.5)); x++)
                        Set(x, y);
            }
            for (int i = 0; i < n; i++)   // the outline: every pixel an edge passes through
            {
                var (ax, ay) = px[i];
                var (bx, by) = px[(i + 1) % n];
                int steps = (int)Math.Ceiling(Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)) * 4) + 1;
                for (int t = 0; t <= steps; t++)
                    Set((int)Math.Floor(ax + (bx - ax) * t / steps), (int)Math.Floor(ay + (by - ay) * t / steps));
            }
        }
        return m;
    }

    static double IoU(bool[] a, bool[] b)
    {
        int inter = 0, uni = 0;
        for (int i = 0; i < a.Length; i++)
        {
            inter += a[i] && b[i] ? 1 : 0;
            uni += a[i] || b[i] ? 1 : 0;
        }
        return uni == 0 ? 1.0 : (double)inter / uni;
    }

    static List<Pt> P(object? v) => Geometry.Pts(v);

    public static Result Check(string dir)
    {
        var hb = (PyDict)PyJson.Load(Path.Combine(dir, "hitboxes.json"))!;
        var sp = (PyDict)PyJson.Load(Path.Combine(dir, "sprite.json"))!;
        double S = sp.F("scale_px_per_m");
        var size = sp.L("size_px");
        int W = (int)Py.ToLong(size[0]), H = (int)Py.ToLong(size[1]);
        var origin = sp.L("origin_px");
        double ox = Py.ToDouble(origin[0]), oy = Py.ToDouble(origin[1]);
        var comps = hb.L("components").Cast<PyDict>().ToList();
        var rows = new List<(string Id, string Kind, double V)>();
        double worst = 1.0;
        foreach (var c in comps.Where(c => c.Has("local")))
        {
            var tm = sp.D("turret_types").D(c.S("type"));
            var timg = Png.Load(Path.Combine(dir, tm.S("file")));
            var local = c.D("local");
            var shapes = new[] { P(local["body"]) }.Concat(local.L("parts").Select(P)).Concat(local.L("barrels").Select(P)).ToList();
            double rest = c.F("rest_deg"), best = 1.0;
            foreach (double ang in new[] { rest, rest + 37, rest + 90, rest + 180 })
            {
                var rot = Preview.Rotate(timg, ang);
                double cx = ox + c.F("x") * S, cy = oy + c.F("y") * S;
                int px0 = (int)Math.Round(cx - timg.Width / 2.0, MidpointRounding.ToEven);
                int py0 = (int)Math.Round(cy - timg.Height / 2.0, MidpointRounding.ToEven);
                int r = Math.Max(timg.Width, timg.Height) / 2 + 2;
                int bx0 = Math.Max(0, (int)cx - r), by0 = Math.Max(0, (int)cy - r), bx1 = Math.Min(W, (int)cx + r), by1 = Math.Min(H, (int)cy + r);
                if (bx1 <= bx0 || by1 <= by0)   // off the canvas (an invalid layout): a mismatch
                {
                    best = 0;
                    continue;
                }
                var sprite = new bool[(bx1 - bx0) * (by1 - by0)];
                for (int y = by0; y < by1; y++)
                    for (int x = bx0; x < bx1; x++)
                    {
                        int tx = x - px0, ty = y - py0;
                        if (tx >= 0 && ty >= 0 && tx < timg.Width && ty < timg.Height)
                            sprite[(y - by0) * (bx1 - bx0) + (x - bx0)] = rot[(ty * timg.Width + tx) * 4 + 3] * 255 > 127;
                    }
                var hit = Mask(shapes.Select(s => Geometry.RotateTranslate(s, ang, c.F("x"), c.F("y"))), S, ox, oy, bx0, by0, bx1, by1);
                best = Math.Min(best, IoU(sprite, hit));
            }
            rows.Add((c.S("id"), c.S("kind"), best));
            worst = Math.Min(worst, best);
        }

        var hull = Png.Load(Path.Combine(dir, "hull.png"));
        bool Opaque(int x, int y) => hull.Data[(y * W + x) * 4 + 3] > 127;
        double fixedWorst = 1.0;
        foreach (var c in comps)
        {
            if (!(c.S("kind") == "funnel" || (c.S("kind") == "superstructure" && c.F("base", 0) > 0)))
                continue;
            var m = Mask([P(c["points"])], S, ox, oy, 0, 0, W, H);
            int n = 0, ok = 0;
            for (int i = 0; i < m.Length; i++)
                if (m[i])
                {
                    n++;
                    ok += Opaque(i % W, i / W) ? 1 : 0;
                }
            double cov = n > 0 ? (double)ok / n : 1.0;
            fixedWorst = Math.Min(fixedWorst, cov);
            rows.Add((c.S("id"), c.S("kind"), cov));
        }
        // the hull image is the hull plus whatever overhangs it: flight decks, sponsons, deck-edge elevators
        var outline = new[] { P(hb["hull"]) }.Concat(comps.Where(c => c.S("kind", null) is "flight_deck" or "sponson").Select(c => P(c["points"])));
        var hm = Mask(outline, S, ox, oy, 0, 0, W, H);
        var ha = new bool[W * H];
        for (int i = 0; i < ha.Length; i++)
            ha[i] = hull.Data[i * 4 + 3] > 127;
        double hullIoU = IoU(ha, hm);

        var sb = new StringBuilder();
        sb.AppendLine($"\n{Path.GetFileName(Path.GetFullPath(dir))} @ {S} px/m   hull IoU {hullIoU:F3}   worst turret IoU {worst:F3}   " +
                      $"worst fixed coverage {fixedWorst:F3}");
        foreach (var (id, kind, v) in rows)
            sb.AppendLine($"   {id,16} {kind,15}  {v:F3}{(v > 0.85 ? "" : "   <-- check")}");
        var probs = Subdivision(hb).Concat(Traverse(hb, sp)).ToList();
        var shared = hb.L("rooms").Cast<PyDict>().Where(r => r.B("shared")).Select(r => r.S("id")).ToList();
        sb.AppendLine($"   subdivision: {hb.L("sections").Count} sections, {hb.L("cells").Count} cells, {hb.L("rooms").Count} rooms" +
                      (shared.Count > 0 ? ", sharing a cell: " + string.Join(", ", shared) : ""));
        foreach (var p in probs.Take(20))
            sb.AppendLine($"   <-- {p}");
        return new Result(Math.Min(Math.Min(worst, fixedWorst), Math.Min(hullIoU, probs.Count > 0 ? 0.0 : 1.0)), sb.ToString().TrimEnd());
    }

    /// <summary>geometry.table_half_width over the exported hull_form stations (or the deck outline without one).</summary>
    static double FormHalfWidth(PyDict hb, double x, double z)
    {
        if (hb.Get("hull_form") is not PyDict hf || !Py.Truthy(hf))
        {
            var span = Geometry.PolygonYSpan(P(hb["hull"]), x);
            return span is { } s ? Math.Max(-s.Lo, s.Hi) : 0.0;
        }
        var st = hf.L("stations").Cast<PyDict>().ToList();
        var xs = st.Select(s => s.F("x")).ToList();
        if (x <= xs[0] || x >= xs[^1])
            return 0.0;
        int i = Math.Max(0, Math.Min(xs.Count - 2, Enumerable.Range(0, xs.Count - 1).First(k => xs[k + 1] >= x)));
        double At(PyDict s)
        {
            var zs = ((System.Collections.IEnumerable)s["z"]!).Cast<object?>().Select(Py.ToDouble).ToList();
            var ys = ((System.Collections.IEnumerable)s["y"]!).Cast<object?>().Select(Py.ToDouble).ToList();
            if (z >= zs[^1])
                return ys[^1];
            if (z < zs[0])
                return 0.0;
            int j = Enumerable.Range(0, zs.Count - 1).First(k => zs[k + 1] >= z);
            double f = zs[j + 1] > zs[j] ? (z - zs[j]) / (zs[j + 1] - zs[j]) : 0.0;
            return ys[j] + (ys[j + 1] - ys[j]) * f;
        }
        PyDict a = st[i], b = st[i + 1];
        double fx = b.F("x") > a.F("x") ? (x - a.F("x")) / (b.F("x") - a.F("x")) : 0.0;
        return At(a) + (At(b) - At(a)) * fx;
    }

    static IEnumerable<string> Subdivision(PyDict hb, int samples = 3000)
    {
        var probs = new List<string>();
        var cells = hb.L("cells").Cast<PyDict>().ToDictionary(c => c.S("id"), StringComparer.Ordinal);
        var rooms = hb.L("rooms").Cast<PyDict>().ToDictionary(r => r.S("id"), StringComparer.Ordinal);
        var neigh = cells.ToDictionary(kv => kv.Key, kv => ((System.Collections.IEnumerable)kv.Value["neighbours"]!)
            .Cast<System.Collections.IList>().Select(n => (string)n[0]!).ToList(), StringComparer.Ordinal);
        foreach (var r in rooms.Values)
        {
            var rc = r.L("cells").Cast<string>().ToList();
            if (rc.Count == 0)
                probs.Add($"room {r.S("id")} has no cell");
            foreach (var cid in rc.Where(cid => !cells.ContainsKey(cid)))
                probs.Add($"room {r.S("id")} lists unknown cell {cid}");
        }
        foreach (var c in cells.Values)
        {
            string id = c.S("id"), room = c.S("room");
            if (!rooms.TryGetValue(room, out var rr) || !rr.L("cells").Cast<string>().Contains(id))
                probs.Add($"cell {id}: owner {room} doesn't list it");
            foreach (var nid in neigh[id])
                if (!cells.ContainsKey(nid) || !neigh[nid].Contains(id))
                    probs.Add($"cell {id}: neighbour {nid} isn't mutual");
        }
        foreach (var comp in hb.L("components").Cast<PyDict>())
            if (comp.Get("magazine") is string mag && mag != "" && !rooms.ContainsKey(mag))
                probs.Add($"{comp.S("id")}: magazine {mag} isn't a room");
        double L = hb.F("length"), keel = hb.D("vertical").F("keel");
        var raised = (hb.D("vertical").Get("raised") as List<object?> ?? []).Cast<PyDict>().ToList();
        var rng = new Random(1);
        int bad = 0;
        var cl = cells.Values.ToList();
        for (int k = 0; k < samples; k++)
        {
            double x = -L / 2 + L * rng.NextDouble();
            double top = raised.Where(r => r.F("x0") <= x && x <= r.F("x1")).Select(r => r.F("top")).Append(0.0).Max();
            double z = keel + (top - keel) * rng.NextDouble();
            double hw = FormHalfWidth(hb, x, z);      // the sections narrow toward the keel
            if (hw <= 0)                               // under a forefoot, a cut-up or a counter: outside the hull
                continue;
            double y = -hw + 2 * hw * rng.NextDouble();
            int n = cl.Count(c => c.F("x0") <= x && x < c.F("x1") && c.F("y0") <= y && y < c.F("y1") && c.F("base") <= z && z < c.F("top"));
            bad += n != 1 ? 1 : 0;
        }
        if (bad > samples * 0.002)      // points right at the bow tip may miss a zero-volume cell
            probs.Add($"{bad} of {samples} points inside the hull fall in no cell or several");
        return probs;
    }

    static (double, double) Pair(object? v)
    {
        var l = (System.Collections.IList)v!;
        return (Py.ToDouble(l[0]), Py.ToDouble(l[1]));
    }

    static bool InPolygonOf(PyDict o, double px, double py) =>
        o.Has("points") && Py.Truthy(o["points"])
            ? Geometry.PointInPolygon(px, py, P(o["points"]))
            : Geometry.PointInPolygon(px, py, Geometry.RotateTranslate(P(o.D("local")["body"]), o.F("rest_deg"), o.F("x"), o.F("y")));

    static IEnumerable<string> Traverse(PyDict hb, PyDict sp)
    {
        static bool Inside(double lo, double hi, double a) => (lo - 1e-6 <= a && a <= hi + 1e-6) || (lo - 1e-6 <= a + 360 && a + 360 <= hi + 1e-6);
        var spTr = sp.L("mounts").Cast<PyDict>().ToDictionary(m => m.S("id"), m => m.Get("traverse_deg"), StringComparer.Ordinal);
        var comps = hb.L("components").Cast<PyDict>().ToList();
        var probs = new List<string>();
        foreach (var c in comps)
        {
            if (!c.Has("arcs_deg"))
                continue;
            var (lo, hi) = Py.Truthy(c.Get("traverse_deg")) ? Pair(c["traverse_deg"]) : (0.0, -1.0);
            var arcs = ((System.Collections.IEnumerable)c["arcs_deg"]!).Cast<object?>().Select(Pair).ToList();
            double rest = c.F("rest_deg");
            if (!(0 <= hi - lo && hi - lo < 360 && Inside(lo, hi, Geometry.Normalize360(rest))
                  && arcs.All(a => Inside(lo, hi, Geometry.Normalize360(a.Item1)) && Inside(lo, hi, Geometry.Normalize360(a.Item2)) && a.Item2 - a.Item1 <= hi - lo)))
                probs.Add($"{c.S("id")}: traverse {Py.Repr(PyJson.Plain(c.Get("traverse_deg")))} doesn't hold rest {Py.Repr(c["rest_deg"])} and arcs");
            spTr.TryGetValue(c.S("id"), out var st);
            if (!Py.Eq(PyJson.Plain(st), PyJson.Plain(c.Get("traverse_deg"))))
                probs.Add($"{c.S("id")}: sprite.json traverse {Py.Repr(PyJson.Plain(st))} differs");
            var barrels = c.Has("local") ? c.D("local").L("barrels").Select(P).ToList() : [];
            double axis = c.F("base") + 0.55 * (c.F("top") - c.F("base"));
            if (c.S("kind") == "secondary" && Py.Truthy(c.Get("rotating", true)))
            {
                // stowed barrels lie clear of everything around them at their axis height
                var hits = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var o in comps)
                {
                    if (ReferenceEquals(o, c) || !(o.F("top", 0) > axis + 1e-6 && o.F("base", 0) < axis))
                        continue;
                    if (!(o.Has("points") && Py.Truthy(o["points"])) && !o.Has("local"))
                        continue;
                    foreach (var b in barrels)   // past the root, which sits in the gun house or casemate face
                    {
                        double bmax = b.Max(p => p.X);
                        var out_ = b.Where(p => p.X > 0.25 * bmax);
                        foreach (var (px, py) in Geometry.RotateTranslate(out_, rest, c.F("x"), c.F("y")))
                            if (InPolygonOf(o, px, py))
                                hits.Add(o.S("id"));
                    }
                }
                if (hits.Count > 0)
                    probs.Add($"{c.S("id")}: stowed barrels at {Py.Repr(c["rest_deg"])} hit {string.Join(", ", hits)}");
            }
            if (c.S("kind") != "main")
                continue;
            double R = barrels.SelectMany(b => b).Max(p => p.X);
            var tall = comps.Where(o => !ReferenceEquals(o, c) && o.F("top", 0) > axis + 1e-6 && o.F("base", 0) < axis
                                        && ((o.Has("points") && Py.Truthy(o["points"])) || o.Has("local"))).ToList();
            var hit2 = new SortedSet<string>(StringComparer.Ordinal);
            for (int k = 0; k <= (int)(hi - lo); k++)
            {
                double a = double.DegreesToRadians(lo + k);
                foreach (double f in new[] { 0.5, 0.75, 1.0 })
                {
                    double px = c.F("x") + f * R * Math.Cos(a), py = c.F("y") + f * R * Math.Sin(a);
                    foreach (var o in tall)
                        if (InPolygonOf(o, px, py))
                            hit2.Add(o.S("id"));
                }
            }
            if (hit2.Count > 0)
                probs.Add($"{c.S("id")}: barrels swung through its traverse hit {string.Join(", ", hit2)}");
        }
        return probs;
    }
}
