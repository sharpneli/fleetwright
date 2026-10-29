using System.Numerics;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The ship card's damage plate (docs/map-ui/card.md): her profile over her plan at one length scale (heights
/// ×2), drawn from her class in hairlines (the hull's side, the watertight bulkheads, the decks; the upper works as one
/// silhouette a run), the sea under her waterline now (the sim's own attitude, going down too), the water in her spaces
/// (by side in the plan), her mounts, wrecked parts, holes and fires; colour only where something is wrong. Under it
/// the readings and the clinometer, her midship section heeled to the true angle.</summary>
public sealed partial class MapScene
{
    /// <summary>What the plate draws of a class, made once a class (ship frame, metres), with the frame's scratch.</summary>
    internal sealed class PlateShape
    {
        public double X0, X1, ZHigh;
        /// <summary>The hull's side: at each station its bottom (each strip's step drawn through its middle) and deck top;
        /// the deck outline's half-breadth there.</summary>
        public double[] Xs = [], Bottom = [], Top = [], Half = [];
        public (double X0, double X1, double Z, bool Armour)[] Decks = [];
        public (double X, double Base, double Top)[] Bulkheads = [];
        /// <summary>The upper works' runs: each an outline of (x, z) from the deck round to the deck; the faint levels
        /// inside; the blocks (for their wrecks).</summary>
        public Vector2[][] Sky = [];
        public (double X0, double X1, double Z)[] Levels = [];
        public int[] Blocks = [], Profile = [], PlanBlocks = [], Funnels = [];
        public Vector2[] Section = [];
        public double SectionX;
        public int Tiers;
        // the frame's scratch: the fullest cell by section and tier (profile), by section and side (plan); the water by
        // section (counterflooding apart); her parts' states
        public int[] TierBest = [], SideBest = [];
        public float[] TierShare = [], SideShare = [];
        public bool[] TierCounter = [], SideCounter = [];
        public double[] Tonnes = [], Counter = [];
        public PartState[] Parts = [];

        public double BottomAt(double x) => Lerp(Bottom, x);
        public double TopAt(double x) => Lerp(Top, x);
        public double HalfAt(double x) => Lerp(Half, x);

        double Lerp(double[] v, double x)
        {
            if (Xs.Length == 0)
                return 0;
            if (x <= Xs[0])
                return v[0];
            if (x >= Xs[^1])
                return v[^1];
            int i = Array.BinarySearch(Xs, x);
            if (i >= 0)
                return v[i];
            i = ~i;
            double t = (x - Xs[i - 1]) / (Xs[i] - Xs[i - 1]);
            return v[i - 1] + (v[i] - v[i - 1]) * t;
        }
    }

    readonly Dictionary<ShipClass, PlateShape> plates = [];

    PlateShape PlateOf(ShipClass cls)
    {
        if (!plates.TryGetValue(cls, out var p))
            plates[cls] = p = BuildPlate(cls);
        return p;
    }

    internal static PlateShape BuildPlate(ShipClass cls)
    {
        var sh = new PlateShape();
        (sh.X0, sh.X1) = Extent(cls);
        var hull = cls.Hull;
        var vert = cls.Vertical;
        // the side: the lowest height with hull at each station, the deck top
        var raw = new List<(double X, double B, double T)>();
        const int n = 240;
        for (int i = 0; i <= n; i++)
        {
            double x = hull.X0 + (hull.X1 - hull.X0) * i / n, top = hull.DeckTop(x);
            for (double z = hull.Keel; z <= top; z += 0.1)
                if (hull.HalfWidth(x, z) > 0.05)
                {
                    raw.Add((x, z, top));
                    break;
                }
        }
        // the field steps by strips: a run of one bottom height is drawn through its middle, the ends kept
        var mids = new List<(double X, double B)>();
        for (int i = 0; i < raw.Count;)
        {
            int j = i;
            while (j + 1 < raw.Count && raw[j + 1].B == raw[i].B)
                j++;
            if (i == 0)
                mids.Add((raw[0].X, raw[0].B));
            if (i > 0 && j < raw.Count - 1)
                mids.Add(((raw[i].X + raw[j].X) / 2, raw[i].B));
            if (j == raw.Count - 1 && raw.Count > 1)
                mids.Add((raw[j].X, raw[j].B));
            i = j + 1;
        }
        sh.Xs = [.. raw.Select(r => r.X)];
        sh.Top = [.. raw.Select(r => r.T)];
        sh.Bottom = [.. raw.Select(r =>
        {
            int k = mids.FindIndex(m => m.X >= r.X);
            if (k <= 0)
                return mids.Count > 0 ? mids[Math.Max(k, 0)].B : r.B;
            var (a, b) = (mids[k - 1], mids[k]);
            return a.B + (b.B - a.B) * (r.X - a.X) / Math.Max(b.X - a.X, 1e-9);
        })];
        // the deck outline's half-breadth at each station
        var outline = cls.Points(cls.DeckOutline).ToArray();
        sh.Half = [.. sh.Xs.Select(x =>
        {
            double half = 0;
            for (int i = 0; i < outline.Length; i++)
            {
                var (p, q) = (outline[i], outline[(i + 1) % outline.Length]);
                if (p.X == q.X || (x - p.X) * (x - q.X) > 0)
                    continue;
                half = Math.Max(half, Math.Abs(p.Y + (q.Y - p.Y) * (x - p.X) / (q.X - p.X)));
            }
            return half > 0 ? half : cls.Beam / 2;
        })];
        // the decks inside her (the raised decks are her outline), the armour deck marked; the watertight bulkheads
        var decks = new List<(double, double, double, bool)>();
        foreach (var d in cls.Decks)
        {
            if (d.Kind == DeckKind.Raised)
                continue;
            int first = -1, last = -1;
            for (int i = 0; i < sh.Xs.Length; i++)
                if (sh.Bottom[i] < d.Z && sh.Top[i] > d.Z + 0.05)
                    (first, last) = (first < 0 ? i : first, i);
            if (first < 0)
                continue;
            double a = sh.Xs[first], b = sh.Xs[last];
            if (double.IsFinite(d.X0) && double.IsFinite(d.X1))
                (a, b) = (Math.Max(a, d.X0), Math.Min(b, d.X1));
            if (b > a)
                decks.Add((a, b, d.Z, d.ArmourMm > 0));
        }
        sh.Decks = [.. decks];
        sh.Bulkheads = [.. cls.Bulkheads.ToArray().Where(b => b.Kind is BulkheadKind.Main or BulkheadKind.Collision)
            .Select(b => (b.X, Math.Max(b.Base, sh.BottomAt(b.X)), Math.Min(b.Top, sh.TopAt(b.X))))];
        // the upper works: what stands on the hull (no masts: they'd double the plate), the blocks as one skyline a run
        var comps = cls.Components.ToArray();   // once a class
        var standing = Enumerable.Range(0, comps.Length).Where(i => Standing(comps[i], vert)).ToArray();
        var blocks = standing.Where(i => comps[i].Kind is ComponentKind.Superstructure or ComponentKind.ConningTower).ToArray();
        sh.Blocks = blocks;
        sh.Profile = [.. standing.Where(i => comps[i].Kind is ComponentKind.Funnel or ComponentKind.Main).OrderBy(i => comps[i].Bounds.Base)];
        sh.ZHigh = vert.Keel + vert.Depth;
        foreach (int i in standing)
            sh.ZHigh = Math.Max(sh.ZHigh, comps[i].Bounds.Top);
        var cuts = blocks.SelectMany(i => new[] { comps[i].Bounds.X0, comps[i].Bounds.X1 }).Distinct().Order().ToArray();
        var sky = new List<Vector2[]>();
        for (int i = 0; i + 1 < cuts.Length;)
        {
            double Height(int j)
            {
                double xm = (cuts[j] + cuts[j + 1]) / 2, top = double.NaN;
                foreach (int b in blocks)
                    if (comps[b].Bounds.X0 <= xm && comps[b].Bounds.X1 >= xm)
                        top = double.IsNaN(top) ? comps[b].Bounds.Top : Math.Max(top, comps[b].Bounds.Top);
                return top;
            }
            if (double.IsNaN(Height(i)))
            {
                i++;
                continue;
            }
            int last = i;
            while (last + 2 < cuts.Length && !double.IsNaN(Height(last + 1)))
                last++;
            double baseZ = blocks.Where(b => comps[b].Bounds.X1 > cuts[i] && comps[b].Bounds.X0 < cuts[last + 1]).Min(b => comps[b].Bounds.Base);
            var run = new List<Vector2> { new((float)cuts[i], (float)baseZ) };
            for (int j = i; j <= last; j++)
            {
                run.Add(new((float)cuts[j], (float)Height(j)));
                run.Add(new((float)cuts[j + 1], (float)Height(j)));
            }
            run.Add(new((float)cuts[last + 1], (float)baseZ));
            sky.Add([.. run]);
            i = last + 1;
        }
        sh.Sky = [.. sky];
        sh.Levels = [.. blocks.Where(b => comps[b].Bounds.Base > sh.TopAt((comps[b].Bounds.X0 + comps[b].Bounds.X1) / 2) + 0.1)
            .Select(b => (comps[b].Bounds.X0, comps[b].Bounds.X1, comps[b].Bounds.Base))];
        sh.PlanBlocks = [.. blocks.Where(b => comps[b].Kind == ComponentKind.ConningTower ||
            comps[b].Bounds.Base < sh.TopAt((comps[b].Bounds.X0 + comps[b].Bounds.X1) / 2) + 0.5)];
        sh.Funnels = [.. Enumerable.Range(0, comps.Length).Where(i => comps[i].Kind == ComponentKind.Funnel)];
        // her midship section (at the centre of flotation), starboard up the side and back down to port
        sh.SectionX = cls.Hydrostatics.Lcf;
        var half = new List<(double Y, double Z)>();
        for (double z = hull.Keel; z <= hull.DeckTop(sh.SectionX) + 1e-6; z += 0.25)
            half.Add((hull.HalfWidth(sh.SectionX, z), z));
        sh.Section = [.. half.Select(p => new Vector2((float)p.Y, (float)p.Z)), .. half.AsEnumerable().Reverse().Select(p => new Vector2(-(float)p.Y, (float)p.Z))];
        // the scratch
        sh.Tiers = Math.Max(cls.Tiers.Length, 1);
        int sections = Math.Max(cls.Sections.Length, 1);
        sh.TierBest = new int[sections * sh.Tiers];
        sh.TierShare = new float[sections * sh.Tiers];
        sh.TierCounter = new bool[sections * sh.Tiers];
        sh.SideBest = new int[sections * 3];
        sh.SideShare = new float[sections * 3];
        sh.SideCounter = new bool[sections * 3];
        sh.Tonnes = new double[sections];
        sh.Counter = new double[sections];
        sh.Parts = new PartState[comps.Length];
        return sh;
    }

    static int SideOf(Sim.Ships.Band b) => b switch { Sim.Ships.Band.Port => 0, Sim.Ships.Band.Starboard => 2, _ => 1 };

    static Vector4 MountInk(MountStatus s) => s switch
    {
        MountStatus.Out => Bad,
        MountStatus.Jammed or MountStatus.Down => T.Amber,
        _ => T.PlanLine,
    };

    /// <summary>The plate across <paramref name="w"/> from <paramref name="a"/>, scaled to <paramref name="scaleW"/>
    /// (centred in the rest): its height.</summary>
    float Plate(ImDrawListPtr dl, Vector2 a, float w, float scaleW, Snapshot s, int k, ShipClass cls, int di, in Attitude att)
    {
        var sh = PlateOf(cls);
        var v = s.Ships;
        var d = s.Details;
        var vert = cls.Vertical;
        var comps = cls.Components;
        var cells = cls.Cells;
        float mx = P(12) + (w - scaleW) / 2;
        double sc = (scaleW - 2 * P(12)) / Math.Max(sh.X1 - sh.X0, 1), zs = 2 * sc;   // heights ×2: PlateMap
        float pTop = a.Y + P(16), pBottom = pTop + (float)((sh.ZHigh - vert.Keel) * zs), sea = P(18);
        float planCy = pBottom + sea + P(12) + (float)(cls.Beam * sc / 2);
        float stripY = planCy + (float)(cls.Beam * sc / 2) + P(12);
        float h = stripY + P(22) - a.Y;
        var map = new PlateMap(a.X + mx, sh.X0, sc, pBottom, vert.Keel, planCy);
        float X(double x) => map.X(x);
        Vector2 PZ(double x, double z) => map.Z(x, z);
        Vector2 PY(double x, double y) => map.Y(x, y);
        uint line = U(T.PlanLine);
        uint Line(float alpha) => U(T.PlanLine with { W = alpha });
        dl.AddRectFilled(a, a + new Vector2(w, h), U(T.Plan));
        var wp = HitQuery.WaterOf(cls, att);
        double WaterAt(double x) => wp.At(x, 0);
        long now = s.Time.Ms;

        // her state this frame: parts, the fullest space by section and tier and by side, the water by section
        Array.Clear(sh.Parts);
        for (int i = 0; i < v.PartCount[k]; i++)
            if (v.Parts[v.PartStart[k] + i] is int part && part < sh.Parts.Length)
                sh.Parts[part] = v.PartStates[v.PartStart[k] + i];
        Array.Fill(sh.TierBest, -1);
        Array.Fill(sh.SideBest, -1);
        Array.Clear(sh.Tonnes);
        Array.Clear(sh.Counter);
        for (int i = 0; di >= 0 && i < d.FloodCount[di]; i++)
        {
            int j = d.FloodStart[di] + i, cell = d.FloodCell[j];
            var c = cells[cell];
            float share = (float)d.FloodShare[j];
            int sec = Math.Clamp(c.Section, 0, sh.Tonnes.Length - 1);
            int ti = sec * sh.Tiers + Math.Clamp(c.Tier, 0, sh.Tiers - 1), si = sec * 3 + SideOf(c.Band);
            if (sh.TierBest[ti] < 0 || sh.TierShare[ti] < share)
                (sh.TierBest[ti], sh.TierShare[ti], sh.TierCounter[ti]) = (cell, share, d.FloodCounter[j]);
            if (sh.SideBest[si] < 0 || sh.SideShare[si] < share)
                (sh.SideBest[si], sh.SideShare[si], sh.SideCounter[si]) = (cell, share, d.FloodCounter[j]);
            double t = c.VolumeM3 * c.Permeability * share * 1.025;
            if (d.FloodCounter[j])
                sh.Counter[sec] += t;
            else
                sh.Tonnes[sec] += t;
        }

        // ---------------------------------------------------------------- the profile
        // the sea under her waterline now, across the plate
        dl.PushClipRect(a, new Vector2(a.X + w, pBottom + sea), true);
        double xl = sh.X0 - mx / sc, xr = sh.X1 + mx / sc;
        dl.AddQuadFilled(PZ(xl, WaterAt(xl)), PZ(xr, WaterAt(xr)), new Vector2(X(xr), pBottom + sea), new Vector2(X(xl), pBottom + sea),
            U(T.Water with { W = 0.07f }));
        dl.PopClipRect();
        // the hull
        int nx = sh.Xs.Length;
        uint fill = U(T.DrawingFill);
        for (int i = 0; i + 1 < nx; i++)
            dl.AddQuadFilled(PZ(sh.Xs[i], sh.Top[i]), PZ(sh.Xs[i + 1], sh.Top[i + 1]), PZ(sh.Xs[i + 1], sh.Bottom[i + 1]), PZ(sh.Xs[i], sh.Bottom[i]), fill);
        // the water in her spaces, the fullest of each section and tier; counterflooding hatched
        for (int i = 0; i < sh.TierBest.Length; i++)
            if (sh.TierBest[i] >= 0)
            {
                var bx = cells[sh.TierBest[i]].Box;
                double top = bx.Base + (bx.Top - bx.Base) * sh.TierShare[i];
                WaterStrips(dl, sh, bx.X0, bx.X1, bx.Base, top, map, sh.TierCounter[i]);
                if (!sh.TierCounter[i] && sh.TierShare[i] < 0.999f)
                    dl.AddLine(PZ(bx.X0, top), PZ(bx.X1, top), U(T.Water), 1);
            }
        // the decks and the watertight bulkheads
        foreach (var (x0, x1, z, armour) in sh.Decks)
            dl.AddLine(PZ(x0, z), PZ(x1, z), Line(armour ? 0.38f : 0.13f), armour ? P(1.2f) : 1);
        foreach (var (x, lo, hi) in sh.Bulkheads)
            if (hi > lo)
                dl.AddLine(PZ(x, hi), PZ(x, lo), Line(0.32f));
        // the hull's outline: deck tops forward, the bottom back
        Span<Vector2> poly = stackalloc Vector2[2 * nx];
        for (int i = 0; i < nx; i++)
        {
            poly[i] = PZ(sh.Xs[i], sh.Top[i]);
            poly[2 * nx - 1 - i] = PZ(sh.Xs[i], sh.Bottom[i]);
        }
        if (nx > 1)
            dl.AddPolyline(ref poly[0], 2 * nx, Line(0.8f), ImDrawFlags.Closed, 1);
        // the upper works: one silhouette a run, their levels faint inside; wrecked blocks hatched
        foreach (var run in sh.Sky)
        {
            for (int i = 1; i + 2 < run.Length; i += 2)
                dl.AddRectFilled(PZ(run[i].X, run[i].Y), PZ(run[i + 1].X, run[0].Y), fill);
            var o = poly[..Math.Min(run.Length, poly.Length)];   // the hull's outline is drawn: its buffer is free
            for (int i = 0; i < o.Length; i++)
                o[i] = PZ(run[i].X, run[i].Y);
            if (o.Length >= 2)
                dl.AddPolyline(ref o[0], o.Length, Line(0.55f), ImDrawFlags.Closed, 1);
        }
        foreach (var (x0, x1, z) in sh.Levels)
            dl.AddLine(PZ(x0, z), PZ(x1, z), Line(0.16f));
        foreach (int b in sh.Blocks)
            if (sh.Parts[b] is PartState.Disabled or PartState.Destroyed)
                WreckBox(dl, PZ(comps[b].Bounds.X0, comps[b].Bounds.Top), PZ(comps[b].Bounds.X1, comps[b].Bounds.Base), sh.Parts[b]);
        // funnels and the main mounts' gunhouses, with their guns at rest
        var mounts = cls.Mounts;
        foreach (int i in sh.Profile)
        {
            var bx = comps[i].Bounds;
            Vector2 p0 = PZ(bx.X0, bx.Top), p1 = PZ(bx.X1, bx.Base);
            if (comps[i].Kind == ComponentKind.Funnel)
            {
                dl.AddRectFilled(p0, p1, U(T.DrawingDark));
                dl.AddRect(p0, p1, Line(0.65f));
                dl.AddLine(p0 + new Vector2(0, P(1.5f)), new Vector2(p1.X, p0.Y + P(1.5f)), Line(0.4f));
                if (sh.Parts[i] is PartState.Disabled or PartState.Destroyed)
                    WreckBox(dl, p0, p1, sh.Parts[i]);
                continue;
            }
            int m = MountOf(cls, i);
            var st = m >= 0 && di >= 0 ? d.Mount[d.MountStart[di] + m] : MountStatus.Ready;
            var ink = MountInk(st);
            if (m >= 0)
            {
                int dir = Math.Cos(mounts[m].RestDeg * Math.PI / 180) >= 0 ? 1 : -1;
                double gun = mounts[m].CalibreMm * mounts[m].CalibreLength / 1000, zc = (bx.Base + bx.Top) / 2, gx = dir > 0 ? bx.X1 : bx.X0;
                dl.AddLine(PZ(gx, zc), PZ(gx + dir * Math.Max(gun - (bx.X1 - bx.X0) / 2, 1), zc), U(ink with { W = 0.85f }), P(1.6f));
            }
            dl.AddRectFilled(p0, p1, U(st == MountStatus.Out ? Bad with { W = 0.28f } : T.Plan));
            dl.AddRect(p0, p1, U(st is MountStatus.Ready or MountStatus.Loading ? T.PlanLine with { W = 0.55f } : ink), 0, 0, st == MountStatus.Out ? P(1.3f) : 1);
        }
        // the waterlines: her design's dashed, now solid, past her ends a little
        double wl = vert.Waterline;
        for (double x = sh.X0 - 6 / sc; x < sh.X1 + 6 / sc; x += 7 / sc)
            dl.AddLine(PZ(x, wl), PZ(Math.Min(x + 4 / sc, sh.X1 + 6 / sc), wl), U(T.Water with { W = 0.55f }), 1);
        dl.PushClipRect(a, new Vector2(a.X + w, pBottom + sea), true);
        dl.AddLine(PZ(sh.X0 - 10 / sc, WaterAt(sh.X0 - 10 / sc)), PZ(sh.X1 + 10 / sc, WaterAt(sh.X1 + 10 / sc)), U(T.Water), P(1.5f));
        dl.PopClipRect();
        // holes and fires
        for (int i = 0; di >= 0 && i < d.HoleCount[di]; i++)
        {
            int j = d.HoleStart[di] + i;
            var bx = cells[d.HoleCell[j]].Box;
            Hole(dl, PZ((bx.X0 + bx.X1) / 2, d.HoleZ[j]), d.HoleM2[j]);
        }
        for (int i = 0; i < v.FireCount[k]; i++)
            if (v.FireCells[v.FireStart[k] + i] is int cell && cell >= 0 && cell < cells.Length)
            {
                var bx = cells[cell].Box;
                Flame(dl, PZ((bx.X0 + bx.X1) / 2, (bx.Base + bx.Top) / 2), i);
            }
        // one row under her keel: the draughts at her ends, the water under each flooded run
        float ay = pBottom + sea / 2 - TagSize("0"u8).Y / 2;
        double fwd = WaterAt(sh.X1) - vert.Keel, aft = WaterAt(sh.X0) - vert.Keel;
        var aftTag = U8.W($"A {aft:0.0} m");
        float left = X(sh.X0) + P(3) + TagSize(aftTag).X + P(4);
        dl.Tag(new Vector2(X(sh.X0) + P(3), ay), aftTag, T.Water);
        var fwdTag = U8.W($"F {fwd:0.0} m");
        float right = X(sh.X1) - P(3) - TagSize(fwdTag).X;
        dl.Tag(new Vector2(right, ay), fwdTag, T.Water);
        right -= P(4);
        var secs = cls.Sections;
        for (int pass = 0; pass < 2; pass++)
        {
            var tonnes = pass == 0 ? sh.Tonnes : sh.Counter;
            // sections run bow first: walk them stern first, a run of sections with water at a time
            for (int i = tonnes.Length - 1; i >= 0;)
            {
                if (tonnes[i] < 1)
                {
                    i--;
                    continue;
                }
                double t = 0, x0 = double.MaxValue, x1 = double.MinValue;
                for (; i >= 0 && tonnes[i] >= 1; i--)
                    (t, x0, x1) = (t + tonnes[i], Math.Min(x0, secs[i].X0), Math.Max(x1, secs[i].X1));
                var text = pass == 0 ? U8.W($"{t:#,0} t") : U8.W($"{t:#,0} t cf");
                float tw = TagSize(text).X, tx = Math.Max((X(x0) + X(x1) - tw) / 2, left);
                if (tx + tw > right)
                    continue;   // no room: the table under the plate has her water
                dl.Tag(new Vector2(tx, ay), text, pass == 0 ? T.TagText : T.Water);
                left = tx + tw + P(4);
            }
        }

        // ---------------------------------------------------------------- the plan
        var outline = cls.Points(cls.DeckOutline);
        Span<Vector2> deck = stackalloc Vector2[Math.Min(outline.Length, 512)];
        for (int i = 0; i < deck.Length; i++)
            deck[i] = PY(outline[i].X, outline[i].Y);
        if (deck.Length >= 3)
            dl.AddConvexPolyFilled(ref deck[0], deck.Length, fill);
        // the water by side: each section's port, centre and starboard at their fullest
        for (int i = 0; i < sh.SideBest.Length; i++)
            if (sh.SideBest[i] >= 0)
            {
                var bx = cells[sh.SideBest[i]].Box;
                double h0 = sh.HalfAt(bx.X0), h1 = sh.HalfAt(bx.X1);
                Vector2 q0 = PY(bx.X0, Math.Clamp(bx.Y0, -h0, h0)), q1 = PY(bx.X1, Math.Clamp(bx.Y0, -h1, h1)),
                    q2 = PY(bx.X1, Math.Clamp(bx.Y1, -h1, h1)), q3 = PY(bx.X0, Math.Clamp(bx.Y1, -h0, h0));
                if (sh.SideCounter[i])
                {
                    dl.AddQuadFilled(q0, q1, q2, q3, U(T.Water with { W = 0.22f }));
                    Hatch(dl, new Vector2(q0.X, Math.Min(q0.Y, q1.Y)), new Vector2(q2.X, Math.Max(q2.Y, q3.Y)), T.Water with { W = 0.9f });
                }
                else
                    dl.AddQuadFilled(q0, q1, q2, q3, U(T.Water with { W = 0.25f + 0.3f * sh.SideShare[i] }));
            }
        foreach (var (x, _, _) in sh.Bulkheads)
            dl.AddLine(PY(x, -sh.HalfAt(x)), PY(x, sh.HalfAt(x)), Line(0.2f));
        // the centreline, dash-dot
        for (double x = sh.X0 - 6 / sc; x < sh.X1 + 6 / sc; x += 16 / sc)
        {
            dl.AddLine(PY(x, 0), PY(Math.Min(x + 10 / sc, sh.X1 + 6 / sc), 0), Line(0.3f));
            dl.AddLine(PY(x + 12 / sc, 0), PY(x + 14 / sc, 0), Line(0.3f));
        }
        if (deck.Length >= 3)
            dl.AddPolyline(ref deck[0], deck.Length, Line(0.8f), ImDrawFlags.Closed, 1);
        else
            dl.AddRect(PY(sh.X0, -cls.Beam / 2), PY(sh.X1, cls.Beam / 2), line);
        // the upper works: the lowest blocks outlined, wrecks hatched, funnels as dark ovals
        foreach (int b in sh.PlanBlocks)
        {
            var bx = comps[b].Bounds;
            dl.AddRectFilled(PY(bx.X0, bx.Y0), PY(bx.X1, bx.Y1), U(T.DrawingMid with { W = 0.55f }));
            dl.AddRect(PY(bx.X0, bx.Y0), PY(bx.X1, bx.Y1), Line(0.4f));
        }
        foreach (int b in sh.Blocks)
            if (sh.Parts[b] is PartState.Disabled or PartState.Destroyed)
                WreckBox(dl, PY(comps[b].Bounds.X0, comps[b].Bounds.Y0), PY(comps[b].Bounds.X1, comps[b].Bounds.Y1), sh.Parts[b]);
        Span<Vector2> e = stackalloc Vector2[20];
        foreach (int f in sh.Funnels)
        {
            var bx = comps[f].Bounds;
            var c = PY((bx.X0 + bx.X1) / 2, (bx.Y0 + bx.Y1) / 2);
            var r = new Vector2((float)((bx.X1 - bx.X0) * sc / 2), (float)((bx.Y1 - bx.Y0) * sc / 2));
            for (int i = 0; i < e.Length; i++)
                e[i] = c + new Vector2(MathF.Cos(i * MathF.Tau / e.Length) * r.X, MathF.Sin(i * MathF.Tau / e.Length) * r.Y);
            bool wrecked = sh.Parts[f] is PartState.Disabled or PartState.Destroyed;
            dl.AddConvexPolyFilled(ref e[0], e.Length, U(T.DrawingDark));
            if (wrecked)
                WreckBox(dl, c - r, c + r, sh.Parts[f]);
            dl.AddPolyline(ref e[0], e.Length, wrecked ? U(sh.Parts[f] == PartState.Destroyed ? Bad : T.Amber) : Line(0.6f), ImDrawFlags.Closed, 1);
        }
        // the mounts: secondaries as small rings, main mounts as gunhouses with their guns, lettered; a loading clock
        for (int m = 0; m < mounts.Length; m++)
        {
            var st = di >= 0 ? d.Mount[d.MountStart[di] + m] : MountStatus.Ready;
            var ink = MountInk(st);
            var c = PY(mounts[m].X, mounts[m].Y);
            bool fine = st is MountStatus.Ready or MountStatus.Loading;
            if (mounts[m].Kind != ComponentKind.Main)
            {
                float rs = P(2.4f);
                dl.AddCircleFilled(c, rs, U(st == MountStatus.Out ? Bad with { W = 0.45f } : T.Plan), 12);
                if (st == MountStatus.Down)
                    Dashed(dl, c, rs, ink);
                else
                    dl.AddCircle(c, rs, U(fine ? T.PlanLine with { W = 0.75f } : ink), 12);
                continue;
            }
            var bx = comps[mounts[m].Component].Bounds;
            float r = Math.Max((float)((bx.Y1 - bx.Y0) / 2 * sc), P(3));
            float ang = (float)(mounts[m].RestDeg * Math.PI / 180), len = r + (float)(mounts[m].CalibreMm * mounts[m].CalibreLength / 1000 * sc * 0.75);
            var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
            var side = new Vector2(-dir.Y, dir.X) * P(1.4f);
            for (int g = -1; g <= 1; g += 2)
                dl.AddLine(c + dir * r + side * g, c + dir * len + side * g, U(ink with { W = 0.9f }), P(1.2f));
            dl.AddCircleFilled(c, r, U(st == MountStatus.Out ? Bad with { W = 0.28f } : T.Plan), 20);
            if (st == MountStatus.Down)
                Dashed(dl, c, r, ink);
            else
                dl.AddCircle(c, r, U(ink), 20, fine ? P(1.1f) : P(1.5f));
            if (st == MountStatus.Loading && mounts[m].ReloadS > 0)
            {
                float done = Math.Clamp(1 - (float)((d.MountUntilMs[d.MountStart[di] + m] - now) / (mounts[m].ReloadS * 1000)), 0, 1);
                dl.PathArcTo(c, r + P(2.2f), -MathF.PI / 2, -MathF.PI / 2 + done * MathF.Tau, 24);
                dl.PathStroke(line, ImDrawFlags.None, P(1.2f));
            }
            var label = MountLabel(cls, m);
            var size = new Vector2(Width(UiFonts.MonoSmall, label), UiFonts.MonoSmall.FontSize);
            dl.Text(UiFonts.MonoSmall, c - size / 2, fine ? T.TagText : ink, label);
        }
        // holes at her side, fires at their spaces
        for (int i = 0; di >= 0 && i < d.HoleCount[di]; i++)
        {
            int j = d.HoleStart[di] + i;
            var bx = cells[d.HoleCell[j]].Box;
            Hole(dl, PY((bx.X0 + bx.X1) / 2, d.HoleY[j]), d.HoleM2[j]);
        }
        for (int i = 0; i < v.FireCount[k]; i++)
            if (v.FireCells[v.FireStart[k] + i] is int cell && cell >= 0 && cell < cells.Length)
            {
                var bx = cells[cell].Box;
                Flame(dl, PY((bx.X0 + bx.X1) / 2, (bx.Y0 + bx.Y1) / 2) - new Vector2(0, P(1)), i);
            }

        // ---------------------------------------------------------------- the stations and the scale
        uint muted = U(T.Muted);
        dl.AddLine(new Vector2(X(sh.X0), stripY), new Vector2(X(sh.X1), stripY), Line(0.35f));
        for (int i = 0; i <= 20; i++)
        {
            float x = X(sh.X0 + (sh.X1 - sh.X0) * i / 20);
            bool big = i % 5 == 0;
            dl.AddLine(new Vector2(x, stripY), new Vector2(x, stripY + P(big ? 5 : 3)), Line(big ? 0.55f : 0.35f));
            if (big && i != 10)
            {
                var t = i == 0 ? "AP"u8 : i == 20 ? "FP"u8 : i == 5 ? "5"u8 : "15"u8;
                float tw = Width(UiFonts.MonoSmall, t);
                dl.Text(UiFonts.MonoSmall, new Vector2(i == 0 ? x : i == 20 ? x - tw : x - tw / 2, stripY + P(6)), T.Muted, t);
            }
        }
        // amidships: a circle crossed by its line
        var amid = new Vector2(X((sh.X0 + sh.X1) / 2), stripY + P(10));
        dl.AddCircle(amid, P(3.5f), muted, 12);
        dl.AddLine(amid - new Vector2(0, P(5.5f)), amid + new Vector2(0, P(5.5f)), muted);
        // the scale bar, top left: the roundest length under a quarter of hers
        double bar = cls.Length > 200 ? 50 : cls.Length > 80 ? 20 : 10;
        float b0 = a.X + mx, b1 = b0 + (float)(bar * sc), by = a.Y + P(7);
        dl.AddLine(new Vector2(b0, by - P(3)), new Vector2(b0, by), muted);
        dl.AddLine(new Vector2(b0, by), new Vector2(b1, by), muted);
        dl.AddLine(new Vector2(b1, by - P(3)), new Vector2(b1, by), muted);
        for (int i = 1; i < 5; i++)
            dl.AddLine(new Vector2(b0 + (b1 - b0) * i / 5, by - P(1.5f)), new Vector2(b0 + (b1 - b0) * i / 5, by), muted);
        dl.Text(UiFonts.MonoSmall, new Vector2(b1 + P(4), by - UiFonts.MonoSmall.FontSize + P(2)), T.Muted, U8.W($"{bar:0} m"));
        return h;
    }

    /// <summary>The plate's mapping from her frame (metres) to the screen: lengths at one scale, heights at twice it on
    /// the profile, the plan about its centreline (starboard down).</summary>
    readonly record struct PlateMap(float Left, double X0, double Sc, float Keel, double KeelZ, float PlanCy)
    {
        public float X(double x) => Left + (float)((x - X0) * Sc);
        public Vector2 Z(double x, double z) => new(X(x), Keel - (float)((z - KeelZ) * 2 * Sc));
        public Vector2 Y(double x, double y) => new(X(x), PlanCy + (float)(y * Sc));
    }

    /// <summary>The main mount on a component, or -1.</summary>
    static int MountOf(ShipClass cls, int component)
    {
        var mounts = cls.Mounts;
        for (int m = 0; m < mounts.Length; m++)
            if (mounts[m].Component == component)
                return m;
        return -1;
    }

    /// <summary>Water in a space on the profile, from its floor (never under the hull's bottom) to its level.</summary>
    static void WaterStrips(ImDrawListPtr dl, PlateShape sh, double x0, double x1, double floor, double level, in PlateMap map, bool counter)
    {
        uint col = U(T.Water with { W = counter ? 0.22f : 0.5f });
        double xa = x0;
        int i = Array.BinarySearch(sh.Xs, x0);
        i = i < 0 ? ~i : i + 1;
        while (xa < x1)
        {
            double xb = i < sh.Xs.Length ? Math.Min(sh.Xs[i], x1) : x1;
            double ba = Math.Max(floor, sh.BottomAt(xa)), bb = Math.Max(floor, sh.BottomAt(xb));
            if (ba < level || bb < level)
                dl.AddQuadFilled(map.Z(xa, Math.Min(ba, level)), map.Z(xb, Math.Min(bb, level)), map.Z(xb, level), map.Z(xa, level), col);
            xa = xb;
            i++;
        }
        if (counter)
            Hatch(dl, map.Z(x0, level), map.Z(x1, Math.Max(floor, Math.Min(sh.BottomAt(x0), sh.BottomAt(x1)))), T.Water with { W = 0.9f });
    }

    /// <summary>Diagonal hatching in a box (counterflooding; a wreck's).</summary>
    static void Hatch(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 colour, float step = 4)
    {
        if (b.X <= a.X || b.Y <= a.Y)
            return;
        float st = P(step), hgt = b.Y - a.Y;
        uint col = U(colour);
        dl.PushClipRect(a, b, true);
        for (float x = a.X - hgt; x < b.X; x += st)
            dl.AddLine(new Vector2(x, b.Y), new Vector2(x + hgt, a.Y), col, P(1.1f));
        dl.PopClipRect();
    }

    /// <summary>A wrecked part (amber) or a destroyed one (red): hatched and outlined.</summary>
    static void WreckBox(ImDrawListPtr dl, Vector2 a, Vector2 b, PartState st)
    {
        var ink = st == PartState.Destroyed ? Bad : T.Amber;
        var lo = Vector2.Min(a, b);
        var hi = Vector2.Max(a, b);
        dl.AddRectFilled(lo, hi, U(T.Plan));
        Hatch(dl, lo, hi, ink with { W = 0.7f }, 3);
        dl.AddRect(lo, hi, U(ink), 0, 0, P(1.2f));
    }

    static void Dashed(ImDrawListPtr dl, Vector2 c, float r, Vector4 ink)
    {
        uint col = U(ink);
        for (int i = 0; i < 10; i++)
        {
            float t = i * MathF.Tau / 10;
            dl.PathArcTo(c, r, t, t + MathF.Tau / 20, 4);
            dl.PathStroke(col, ImDrawFlags.None, P(1.4f));
        }
    }

    /// <summary>A hole to the sea: a red dot by its size, on a dark ring.</summary>
    static void Hole(ImDrawListPtr dl, Vector2 c, double m2)
    {
        float r = Math.Clamp((float)Math.Sqrt(m2) * P(2.2f), P(2.2f), P(4.5f));
        dl.AddCircleFilled(c, r + 1, U(T.Edge with { W = 0.7f }), 12);
        dl.AddCircleFilled(c, r, U(Bad), 12);
    }

    // a flame's outline and its core, unit size (pointing up from a round foot)
    static readonly Vector2[] FlameShape = MakeFlame(1), FlameInner = MakeFlame(0.45f);

    static Vector2[] MakeFlame(float s)
    {
        var p = new List<Vector2> { new(0, -6 * s) };
        for (int i = 0; i <= 10; i++)
        {
            float t = -0.15f * MathF.PI + i * 1.3f * MathF.PI / 10;
            p.Add(new Vector2(MathF.Cos(t) * 4 * s, 1.6f * s + MathF.Sin(t) * 4 * s));
        }
        return [.. p];
    }

    /// <summary>A fire: a small flame, its brightness flickering.</summary>
    static void Flame(ImDrawListPtr dl, Vector2 c, int seed)
    {
        float f = 0.8f + 0.2f * MathF.Sin((float)ImGui.GetTime() * 5.5f + seed * 1.7f), sc = P(1);
        Span<Vector2> o = stackalloc Vector2[FlameShape.Length];
        for (int i = 0; i < o.Length; i++)
            o[i] = c + FlameShape[i] * sc;
        dl.AddConvexPolyFilled(ref o[0], o.Length, U(T.Fire with { W = f }));
        dl.AddPolyline(ref o[0], o.Length, U(T.Edge with { W = 0.6f }), ImDrawFlags.Closed, 1);
        for (int i = 0; i < o.Length; i++)
            o[i] = c + new Vector2(0, P(1.2f)) + FlameInner[i] * sc;
        dl.AddConvexPolyFilled(ref o[0], o.Length, U(T.FireCore with { W = f }));
    }

    /// <summary>A light she shows (docs/battle/night.md Q9: her beams, her star shell): a spark of eight short rays.</summary>
    static void Spark(ImDrawListPtr dl, Vector2 c)
    {
        uint ink = U(T.Lamp);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4, r0 = P(1.5f), r1 = P(i % 2 == 0 ? 4.5f : 3.2f);
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            dl.AddLine(c + d * r0, c + d * r1, ink, P(1.2f));
        }
    }

    // ------------------------------------------------------------------ the clinometer

    /// <summary>Her midship section heeled to her list (going down too: a capsize rolls her over), the sea level, a
    /// needle along her centreline on a ±45° arc.</summary>
    void Clinometer(ImDrawListPtr dl, Vector2 a, Vector2 size, ShipClass cls, in Attitude att, LossKind loss)
    {
        var sh = PlateOf(cls);
        var vert = cls.Vertical;
        dl.AddRectFilled(a, a + size, U(T.Plan));
        dl.PushClipRect(a, a + size, true);
        float cx = a.X + size.X / 2, wy = a.Y + size.Y * 0.52f;
        double tall = vert.Keel + vert.Depth - vert.Keel;
        float k = (float)Math.Min((size.X - P(30)) / Math.Max(cls.Beam, 1), size.Y * 0.42 / Math.Max(tall, 1));
        float R = Math.Min(size.X / 2 - P(10), wy - a.Y - P(8));
        double zw = HitQuery.WaterOf(cls, att).At(sh.SectionX, 0);
        float heel = (float)(att.HeelDeg * Math.PI / 180);   // + starboard down: seen from aft, starboard at the right
        float co = MathF.Cos(heel), si = MathF.Sin(heel);
        Vector2 P2(float y, float z) => new(cx + (y * co + z * si) * k, wy - (z * co - y * si) * k);
        uint line = U(T.PlanLine with { W = 0.35f });
        for (int deg = -45; deg <= 45; deg += 5)
        {
            float t = deg * MathF.PI / 180, l = deg % 15 == 0 ? P(5) : P(3);
            var dir = new Vector2(MathF.Sin(t), -MathF.Cos(t));
            dl.AddLine(new Vector2(cx, wy) + dir * R, new Vector2(cx, wy) + dir * (R - l), U(T.PlanLine with { W = deg % 15 == 0 ? 0.55f : 0.35f }));
        }
        dl.PathArcTo(new Vector2(cx, wy), R, -MathF.PI / 2 - MathF.PI / 4, -MathF.PI / 2 + MathF.PI / 4, 24);
        dl.PathStroke(U(T.PlanLine with { W = 0.3f }), ImDrawFlags.None, 1);
        dl.AddRectFilled(new Vector2(a.X, wy), a + size, U(T.Water with { W = 0.07f }));
        var sec = sh.Section;
        Span<Vector2> poly = stackalloc Vector2[sec.Length];
        for (int i = 0; i < sec.Length; i++)
            poly[i] = P2(sec[i].X, (float)(sec[i].Y - zw));
        if (poly.Length >= 3)
        {
            dl.AddConvexPolyFilled(ref poly[0], poly.Length, U(T.DrawingFill));
            dl.AddPolyline(ref poly[0], poly.Length, U(T.PlanLine with { W = 0.8f }), ImDrawFlags.Closed, 1);
        }
        dl.AddLine(new Vector2(a.X, wy), new Vector2(a.X + size.X, wy), U(T.Water), P(1.5f));
        bool capsizing = loss == LossKind.Capsized;
        float list = Math.Abs((float)att.HeelDeg);
        var ink = capsizing || list >= 20 ? Bad : list >= 2 ? T.Amber : T.PlanLine;
        var up = new Vector2(si, -co);
        dl.AddLine(new Vector2(cx, wy) + up * P(6), new Vector2(cx, wy) + up * (R + P(2)), U(ink), P(1.3f));
        dl.Text(UiFonts.MonoSmall, a + new Vector2(P(4), P(3)), T.Muted, "P"u8);
        dl.TextRight(UiFonts.MonoSmall, a.X + size.X - P(4), a.Y + P(3), T.Muted, "S"u8);
        var text = capsizing ? "capsizing"u8 : list < 0.05f ? "upright"u8 : U8.W($"{list:0.0}° {(att.HeelDeg >= 0 ? "stbd" : "port")}");
        float tw = Width(UiFonts.MonoSmall, text);
        dl.Text(UiFonts.MonoSmall, new Vector2(cx - tw / 2, a.Y + size.Y - UiFonts.MonoSmall.FontSize - P(4)), ink == T.PlanLine ? T.Text : ink, text);
        dl.PopClipRect();
    }
}
