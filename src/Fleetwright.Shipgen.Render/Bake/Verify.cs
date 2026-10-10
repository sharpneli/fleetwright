using System.Text;
using System.Text.Json;

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

    public static Result Check(string dir)
    {
        var hb = JsonSerializer.Deserialize(JsonFile.ReadText(Path.Combine(dir, "hitboxes.json")), ShipgenJson.Default.Hitboxes)!;
        var sp = JsonSerializer.Deserialize(JsonFile.ReadText(Path.Combine(dir, "sprite.json")), RenderJson.Default.SpriteMeta)!;
        double S = sp.ScalePxPerM;
        int W = (int)sp.SizePx[0], H = (int)sp.SizePx[1];
        double ox = sp.OriginPx[0], oy = sp.OriginPx[1];
        var comps = hb.Components;
        var rows = new List<(string Id, string Kind, double V)>();
        double worst = 1.0;
        foreach (var c in comps.Where(c => c.Local != null))
        {
            var tm = sp.TurretTypes[c.Type!];
            var timg = Png.Load(Path.Combine(dir, tm.File));
            var local = c.Local!;
            var shapes = new[] { local.Body }.Concat(local.Parts).Concat(local.Barrels).ToList();
            double x = c.X!.Value, y = c.Y!.Value, rest = c.RestDeg!.Value, best = 1.0;
            foreach (double ang in new[] { rest, rest + 37, rest + 90, rest + 180 })
            {
                var rot = Preview.Rotate(timg, ang);
                double cx = ox + x * S, cy = oy + y * S;
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
                for (int yy = by0; yy < by1; yy++)
                    for (int xx = bx0; xx < bx1; xx++)
                    {
                        int tx = xx - px0, ty = yy - py0;
                        if (tx >= 0 && ty >= 0 && tx < timg.Width && ty < timg.Height)
                            sprite[(yy - by0) * (bx1 - bx0) + (xx - bx0)] = rot[(ty * timg.Width + tx) * 4 + 3] * 255 > 127;
                    }
                var hit = Mask(shapes.Select(s => Geometry.RotateTranslate(s, ang, x, y)), S, ox, oy, bx0, by0, bx1, by1);
                best = Math.Min(best, IoU(sprite, hit));
            }
            rows.Add((c.Id, c.Kind, best));
            worst = Math.Min(worst, best);
        }

        var hull = Png.Load(Path.Combine(dir, "hull.png"));
        bool Opaque(int x, int y) => hull.Data[(y * W + x) * 4 + 3] > 127;
        double fixedWorst = 1.0;
        foreach (var c in comps)
        {
            if (!(c.Kind == "funnel" || (c.Kind == "superstructure" && c.Base > 0)))
                continue;
            var m = Mask([c.Points!], S, ox, oy, 0, 0, W, H);
            int n = 0, ok = 0;
            for (int i = 0; i < m.Length; i++)
                if (m[i])
                {
                    n++;
                    ok += Opaque(i % W, i / W) ? 1 : 0;
                }
            double cov = n > 0 ? (double)ok / n : 1.0;
            fixedWorst = Math.Min(fixedWorst, cov);
            rows.Add((c.Id, c.Kind, cov));
        }
        // the hull image is the hull seen from above (wider than the deck with tumblehome) plus whatever overhangs it:
        // flight decks, sponsons, deck-edge elevators
        var seen = hb.HullForm.Stations.Count >= 2 ? hb.HullForm.Silhouette(hb.Length) : hb.Hull;
        var outline = new[] { seen }.Concat(comps.Where(c => c.Kind is "flight_deck" or "sponson").Select(c => c.Points!));
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
        var shared = hb.Rooms.Where(r => r.Shared == true).Select(r => r.Id).ToList();
        sb.AppendLine($"   subdivision: {hb.Sections.Count} sections, {hb.Cells.Count} cells, {hb.Rooms.Count} rooms" +
                      (shared.Count > 0 ? ", sharing a cell: " + string.Join(", ", shared) : ""));
        foreach (var p in probs.Take(20))
            sb.AppendLine($"   <-- {p}");
        return new Result(Math.Min(Math.Min(worst, fixedWorst), Math.Min(hullIoU, probs.Count > 0 ? 0.0 : 1.0)), sb.ToString().TrimEnd());
    }

    /// <summary>geometry.table_half_width over the exported hull_form stations.</summary>
    static double FormHalfWidth(Hitboxes hb, double x, double z)
    {
        var st = hb.HullForm.Stations;
        if (st.Count == 0)
        {
            var span = Geometry.PolygonYSpan(hb.Hull, x);
            return span is { } s ? Math.Max(-s.Lo, s.Hi) : 0.0;
        }
        if (x <= st[0].X || x >= st[^1].X)
            return 0.0;
        int i = Math.Max(0, Math.Min(st.Count - 2, Enumerable.Range(0, st.Count - 1).First(k => st[k + 1].X >= x)));
        double At(StationReport s)
        {
            var (zs, ys) = (s.Z, s.Y);
            if (z >= zs[^1])
                return ys[^1];
            if (z < zs[0])
                return 0.0;
            int j = Enumerable.Range(0, zs.Length - 1).First(k => zs[k + 1] >= z);
            double f = zs[j + 1] > zs[j] ? (z - zs[j]) / (zs[j + 1] - zs[j]) : 0.0;
            return ys[j] + (ys[j + 1] - ys[j]) * f;
        }
        var (a, b) = (st[i], st[i + 1]);
        double fx = b.X > a.X ? (x - a.X) / (b.X - a.X) : 0.0;
        return At(a) + (At(b) - At(a)) * fx;
    }

    static IEnumerable<string> Subdivision(Hitboxes hb, int samples = 3000)
    {
        var probs = new List<string>();
        var cells = hb.Cells.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var rooms = hb.Rooms.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var neigh = cells.ToDictionary(kv => kv.Key, kv => kv.Value.Neighbours.Select(n => n[0]).ToList(), StringComparer.Ordinal);
        foreach (var r in rooms.Values)
        {
            if (r.Cells.Count == 0)
                probs.Add($"room {r.Id} has no cell");
            foreach (var cid in r.Cells.Where(cid => !cells.ContainsKey(cid)))
                probs.Add($"room {r.Id} lists unknown cell {cid}");
        }
        foreach (var c in cells.Values)
        {
            if (!rooms.TryGetValue(c.Room, out var rr) || !rr.Cells.Contains(c.Id))
                probs.Add($"cell {c.Id}: owner {c.Room} doesn't list it");
            foreach (var nid in neigh[c.Id])
                if (!cells.ContainsKey(nid) || !neigh[nid].Contains(c.Id))
                    probs.Add($"cell {c.Id}: neighbour {nid} isn't mutual");
        }
        foreach (var comp in hb.Components)
            if (!string.IsNullOrEmpty(comp.Magazine) && !rooms.ContainsKey(comp.Magazine))
                probs.Add($"{comp.Id}: magazine {comp.Magazine} isn't a room");
        double L = hb.Length, keel = hb.Vertical.Keel;
        var raised = hb.Vertical.Raised ?? [];
        var rng = new Random(1);
        int bad = 0;
        var cl = cells.Values.ToList();
        for (int k = 0; k < samples; k++)
        {
            double x = -L / 2 + L * rng.NextDouble();
            double top = raised.Where(r => r.X0 <= x && x <= r.X1).Select(r => r.Top).Append(0.0).Max();
            double z = keel + (top - keel) * rng.NextDouble();
            double hw = FormHalfWidth(hb, x, z);      // the sections narrow toward the keel
            if (hw <= 0)                               // under a forefoot, a cut-up or a counter: outside the hull
                continue;
            double y = -hw + 2 * hw * rng.NextDouble();
            int n = cl.Count(c => c.X0 <= x && x < c.X1 && c.Y0 <= y && y < c.Y1 && c.Base <= z && z < c.Top);
            bad += n != 1 ? 1 : 0;
        }
        if (bad > samples * 0.002)      // points right at the bow tip may miss a zero-volume cell
            probs.Add($"{bad} of {samples} points inside the hull fall in no cell or several");
        return probs;
    }

    static bool InPolygonOf(Component o, double px, double py) =>
        o.Points is { Count: > 0 }
            ? Geometry.PointInPolygon(px, py, o.Points)
            : Geometry.PointInPolygon(px, py, Geometry.RotateTranslate(o.Local!.Body, o.RestDeg!.Value, o.X!.Value, o.Y!.Value));

    static string Show(double[]? v) => v is null ? "None" : $"[{string.Join(", ", v)}]";

    static IEnumerable<string> Traverse(Hitboxes hb, SpriteMeta sp)
    {
        static bool Inside(double lo, double hi, double a) => (lo - 1e-6 <= a && a <= hi + 1e-6) || (lo - 1e-6 <= a + 360 && a + 360 <= hi + 1e-6);
        var spTr = sp.Mounts.ToDictionary(m => m.Id, m => m.TraverseDeg, StringComparer.Ordinal);
        var comps = hb.Components;
        var probs = new List<string>();
        foreach (var c in comps)
        {
            if (c.ArcsDeg is not { } arcsDeg)
                continue;
            var (lo, hi) = c.TraverseDeg is { Length: > 0 } tr ? (tr[0], tr[1]) : (0.0, -1.0);
            var arcs = arcsDeg.Select(a => (a[0], a[1])).ToList();
            double rest = c.RestDeg!.Value, x = c.X!.Value, y = c.Y!.Value;
            if (!(0 <= hi - lo && hi - lo < 360 && Inside(lo, hi, Geometry.Normalize360(rest))
                  && arcs.All(a => Inside(lo, hi, Geometry.Normalize360(a.Item1)) && Inside(lo, hi, Geometry.Normalize360(a.Item2)) && a.Item2 - a.Item1 <= hi - lo)))
                probs.Add($"{c.Id}: traverse {Show(c.TraverseDeg)} doesn't hold rest {rest} and arcs");
            spTr.TryGetValue(c.Id, out var st);
            if (!(st ?? []).SequenceEqual(c.TraverseDeg ?? []))
                probs.Add($"{c.Id}: sprite.json traverse {Show(st)} differs");
            var barrels = c.Local?.Barrels ?? [];
            double axis = c.Base + 0.55 * (c.Top - c.Base);
            bool Tall(Component o) => !ReferenceEquals(o, c) && o.Top > axis + 1e-6 && o.Base < axis && (o.Points is { Count: > 0 } || o.Local != null);
            if (c.Kind == "secondary" && c.Rotating != false)
            {
                // stowed barrels lie clear of everything around them at their axis height
                var hits = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var o in comps.Where(Tall))
                    foreach (var b in barrels)   // past the root, which sits in the gun house or casemate face
                    {
                        double bmax = b.Max(p => p.X);
                        foreach (var (px, py) in Geometry.RotateTranslate(b.Where(p => p.X > 0.25 * bmax), rest, x, y))
                            if (InPolygonOf(o, px, py))
                                hits.Add(o.Id);
                    }
                if (hits.Count > 0)
                    probs.Add($"{c.Id}: stowed barrels at {rest} hit {string.Join(", ", hits)}");
            }
            if (c.Kind != "main")
                continue;
            double R = barrels.SelectMany(b => b).Max(p => p.X);
            var tall = comps.Where(Tall).ToList();
            var hit2 = new SortedSet<string>(StringComparer.Ordinal);
            for (int k = 0; k <= (int)(hi - lo); k++)
            {
                double a = double.DegreesToRadians(lo + k);
                foreach (double f in new[] { 0.5, 0.75, 1.0 })
                {
                    double px = x + f * R * Math.Cos(a), py = y + f * R * Math.Sin(a);
                    foreach (var o in tall)
                        if (InPolygonOf(o, px, py))
                            hit2.Add(o.Id);
                }
            }
            if (hit2.Count > 0)
                probs.Add($"{c.Id}: barrels swung through its traverse hit {string.Join(", ", hit2)}");
        }
        return probs;
    }
}
