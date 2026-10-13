using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>The one tracer (docs/battle/fighting.md 4.1, decision 3): a segment through a ship, ship-local, into a
/// <see cref="HitPath"/> of everything it crossed in order. The hit query (Sim.Combat's HitQuery) brings a shell's last
/// metres into the ship's frame and calls it; shell-test (<see cref="ShellTest"/>) and the tests are callers of it too.
/// <para>v0 is shell-test's first tracer, generalised from "did it reach the citadel" to "everything it crossed": the
/// armour decks and bulkheads are planes, crossed exactly; the hull, the parts and the water are found by stepping
/// along the segment at <see cref="Step"/> and bisecting each change; a cell's boundary is where the walk leaves its
/// box. The walk starts at the segment's end and steps toward its start, so its samples fall where shell-test's did
/// (from the shell's target back toward the gun). Exact crossings for every shape are a deepening (4.5) that replaces
/// the stepping here, checked against this version's recorded results.</para>
/// <para>Allocates nothing once the path has grown to the ship's number of parts.</para></summary>
public static class ShipTracer
{
    /// <summary>The walk's step, m.</summary>
    public const double Step = 0.05;
    const double Eps = 1e-3, Nudge = 1e-9;

    /// <summary>Trace the segment start to end (ship-local, heights over the main deck) through the class. bearingsDeg:
    /// each mount's bearing by its index (NaN or missing: at rest); water: the sea's surface in the ship's frame.</summary>
    public static void Trace(ShipClass c, P3 start, P3 end, ReadOnlySpan<double> bearingsDeg, in Waterplane water, HitPath path)
    {
        double dx = start.X - end.X, dy = start.Y - end.Y, dz = start.Z - end.Z, len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        path.Begin(start, end, len);
        if (!(len > 0))
            return;
        var w = new Walk(c, end, dx / len, dy / len, dz / len, len, water, path);
        w.Run(bearingsDeg);
        path.Sort();
    }

    /// <summary>The ray o + s·d inside lo .. hi along one axis: s0 and s1 narrowed to it; false if it misses.</summary>
    static bool Clip(double o, double d, double lo, double hi, ref double s0, ref double s1)
    {
        if (Math.Abs(d) < 1e-12)
            return o >= lo && o <= hi && s0 <= s1;
        double a = (lo - o) / d, b = (hi - o) / d;
        if (a > b)
            (a, b) = (b, a);
        s0 = Math.Max(s0, a);
        s1 = Math.Min(s1, b);
        return s0 <= s1;
    }

    /// <summary>One trace: the ray from the segment's end (O) back toward its start (B, a unit vector), s metres along
    /// it; a crossing at s is at T = Length - s on the path.</summary>
    ref struct Walk(ShipClass c, P3 o, double bx, double by, double bz, double len, Waterplane water, HitPath path)
    {
        readonly ShipSolids g = c.Solids;
        readonly HullField hull = c.Hull;
        readonly double keel = c.Vertical.Keel;
        int nParts;
        double hull0, hull1;

        readonly double X(double s) => o.X + bx * s;
        readonly double Y(double s) => o.Y + by * s;
        readonly double Z(double s) => o.Z + bz * s;
        readonly P3 At(double s) => new(X(s), Y(s), Z(s));

        void Add(CrossingKind kind, PlateKind plate, int index, double s, double mm = 0, int material = -1, P3 normal = default) =>
            path.Add(new Crossing { T = len - s, Point = At(s), Kind = kind, Plate = plate, Index = index, Mm = mm, Material = material, Normal = normal });

        readonly bool InHull(double x, double y, double z) => z >= keel && z <= hull.DeckTop(x) && Math.Abs(y) < hull.HalfWidth(x, z);

        public void Run(ReadOnlySpan<double> bearingsDeg)
        {
            var box = g.Bounds;
            double s0 = 0, s1 = len;
            if (!Clip(o.X, bx, box.X0, box.X1, ref s0, ref s1) || !Clip(o.Y, by, box.Y0, box.Y1, ref s0, ref s1)
                || !Clip(o.Z, bz, box.Base, box.Top, ref s0, ref s1))
                return;

            // the parts whose boxes the walk passes through, and the stretch of it inside each
            var solids = g.Solids;
            path.Reserve(solids.Length);
            nParts = 0;
            for (int i = 0; i < solids.Length; i++)
            {
                ref readonly var s = ref solids[i];
                double a = s0 - Step, b = s1 + Step;
                if (!Clip(o.X, bx, s.Box.X0 - Eps, s.Box.X1 + Eps, ref a, ref b) || !Clip(o.Y, by, s.Box.Y0 - Eps, s.Box.Y1 + Eps, ref a, ref b)
                    || !Clip(o.Z, bz, s.Box.Base - Eps, s.Box.Top + Eps, ref a, ref b))
                    continue;
                path.parts[nParts] = i;
                (path.partIn[nParts], path.partOut[nParts]) = (a, b);
                path.partBearing[nParts] = s.Shape == ShipSolids.Shape.Turret ? g.BearingOf(s, bearingsDeg) : 0;
                nParts++;
            }
            (hull0, hull1) = (s0 - Step, s1 + Step);
            if (!Clip(o.X, bx, hull.X0, hull.X1, ref hull0, ref hull1) || !Clip(o.Y, by, -hull.YMax, hull.YMax, ref hull0, ref hull1)
                || !Clip(o.Z, bz, Math.Min(keel, hull.Keel), hull.Top, ref hull0, ref hull1))
                (hull0, hull1) = (1, 0);

            // the walk: from the sample before the box (or the segment's end) to the last one inside it
            int n0 = Math.Max(1, (int)Math.Ceiling(s0 / Step)), n1 = (int)Math.Floor(s1 / Step);
            double sp = (n0 - 1) * Step;
            bool inHull = HullAt(sp), inWater = !inHull && Z(sp) < water.At(X(sp), Y(sp));
            int cell = inHull ? g.CellAt(X(sp), Y(sp), Z(sp)) : -1;
            for (int k = 0; k < nParts; k++)
                path.partState[k] = PartAt(k, sp);
            for (int n = n0; n <= n1; n++)
            {
                double s = n * Step;
                bool h = HullAt(s);
                double sh = double.NaN;
                if (h != inHull)
                {
                    sh = Bisect(s - Step, s, inHull, -1);
                    HullCrossing(sh, forwardIn: !h);
                }
                bool wet = !h && Z(s) < water.At(X(s), Y(s));
                if (wet != inWater)
                    WaterCrossing(s, sh, h, forwardIn: !wet);
                int now = h ? cell >= 0 && c.Cells[cell].Box.Contains(X(s), Y(s), Z(s)) ? cell : g.CellAt(X(s), Y(s), Z(s)) : -1;
                if (now != cell)
                    Cells(s, sh, cell, now);
                for (int k = 0; k < nParts; k++)
                {
                    bool inside = PartAt(k, s);
                    if (inside != path.partState[k])
                    {
                        PartCrossing(k, Bisect(s - Step, s, path.partState[k], k), forwardIn: !inside);
                        path.partState[k] = inside;
                    }
                }
                (inHull, inWater, cell) = (h, wet, now);
            }
            // in the water already where the walk's box begins: it went in out there, or the segment starts in the sea
            // (a WaterIn at its start)
            if (inWater)
            {
                double f0 = o.Z - water.At(o.X, o.Y), f1 = bz - water.DzDx * bx - water.DzDy * by;
                double l = Math.Sqrt(water.DzDx * water.DzDx + water.DzDy * water.DzDy + 1);
                Add(CrossingKind.WaterIn, PlateKind.None, -1, f0 + f1 * len > 0 && Math.Abs(f1) > 1e-12 ? Math.Clamp(-f0 / f1, n1 * Step, len) : len,
                    normal: new P3(-water.DzDx / l, -water.DzDy / l, 1 / l));
            }
            // last, so that at a deck its armour comes first along the path
            Planes();
        }

        readonly bool HullAt(double s) => s >= hull0 && s <= hull1 && InHull(X(s), Y(s), Z(s));

        readonly bool PartAt(int k, double s)
        {
            if (s < path.partIn[k] || s > path.partOut[k])
                return false;
            return g.Inside(g.Solids[path.parts[k]], X(s), Y(s), Z(s), path.partBearing[k]);
        }

        /// <summary>Where in lo .. hi the hull (part -1) or part k changes from was, by bisection.</summary>
        readonly double Bisect(double lo, double hi, bool was, int part)
        {
            for (int i = 0; i < 20; i++)
            {
                double m = (lo + hi) / 2;
                bool at = part < 0 ? InHull(X(m), Y(m), Z(m)) : g.Inside(g.Solids[path.parts[part]], X(m), Y(m), Z(m), path.partBearing[part]);
                if (at == was)
                    lo = m;
                else
                    hi = m;
            }
            return (lo + hi) / 2;
        }

        // ------------------------------------------------------------------ the hull and the sea

        /// <summary>The hull's skin at s: its face (side, deck or end, as shell-test told them apart), its plating and
        /// the side armour on it.</summary>
        void HullCrossing(double s, bool forwardIn)
        {
            double x = X(s), y = Y(s), z = Z(s);
            var pl = c.Plating;
            bool mid = x >= pl.MidX0 && x <= pl.MidX1;
            PlateKind face;
            double mm;
            P3 n;
            // at the deck edge it's both: the side first, so a belt reaching the deck counts
            if (hull.HalfWidth(x, z) - Math.Abs(y) < 0.05)
            {
                (face, mm) = (PlateKind.HullSide, mid ? pl.ShellMm : pl.ShellEndMm);
                const double h = 0.05;
                double wx = (hull.HalfWidth(x + h, z) - hull.HalfWidth(x - h, z)) / (2 * h), wz = (hull.HalfWidth(x, z + h) - hull.HalfWidth(x, z - h)) / (2 * h);
                double l = Math.Sqrt(wx * wx + 1 + wz * wz);
                n = new P3(-wx / l, Math.Sign(y == 0 ? 1 : y) / l, -wz / l);
            }
            else if (Math.Abs(hull.DeckTop(x) - z) < 0.02)
                (face, mm, n) = (PlateKind.HullDeck, mid ? pl.StrengthDeckMm : pl.StrengthDeckEndMm, new P3(0, 0, 1));
            else
                (face, mm, n) = (PlateKind.HullEnd, pl.ShellEndMm, new P3(Math.Sign(x == 0 ? 1 : x), 0, 0));
            double armourMm = 0;
            int side = face == PlateKind.HullSide ? SidePlate(x, z, out armourMm) : -1;
            // added in the reverse of their order along the path: armour outside the plating
            if (forwardIn)
                Add(CrossingKind.HullIn, face, -1, s, mm, pl.Material, n);
            if (side >= 0)
                Add(CrossingKind.Plate, PlateKind.SideArmour, side, s, armourMm, c.SidePlates[side].Material, n);
            if (!forwardIn)
                Add(CrossingKind.HullOut, face, -1, s, mm, pl.Material, n);
        }

        /// <summary>The side plate at (x, z) on the hull's side, the belt first (-1: none), and its thickness there: a
        /// belt tapering to its bottom, an end strake to its tip.</summary>
        readonly int SidePlate(double x, double z, out double mm)
        {
            mm = 0;
            foreach (int i in g.Sides)
            {
                ref readonly var p = ref c.SidePlates[i];
                if (x < p.X0 - Eps || x > p.X1 + Eps || z < p.Bottom - Eps || z > p.Top + Eps)
                    continue;
                mm = p.ThicknessMm;
                if (!double.IsNaN(p.BottomMm) && !double.IsNaN(p.TaperFrom) && z < p.TaperFrom && p.TaperFrom > p.Bottom)
                    mm = p.BottomMm + (p.ThicknessMm - p.BottomMm) * Math.Clamp((z - p.Bottom) / (p.TaperFrom - p.Bottom), 0, 1);
                else if (!double.IsNaN(p.TipMm) && p.X1 > p.X0)
                {
                    // from the inner end (nearer amidships) to the tip at the ship's end
                    double f = Math.Abs(p.X1) > Math.Abs(p.X0) ? (x - p.X0) / (p.X1 - p.X0) : (p.X1 - x) / (p.X1 - p.X0);
                    mm = p.ThicknessMm + (p.TipMm - p.ThicknessMm) * Math.Clamp(f, 0, 1);
                }
                return i;
            }
            return -1;
        }

        /// <summary>Into or out of the sea in the step to s: where the hull changed (sh), just on the sea's side of it,
        /// or where the walk crosses the waterplane.</summary>
        void WaterCrossing(double s, double sh, bool hullNow, bool forwardIn)
        {
            double sw;
            if (!double.IsNaN(sh))
                sw = hullNow ? sh - Nudge : sh + Nudge;
            else
            {
                // z - W(x, y) is linear along the walk
                double f0 = o.Z - water.At(o.X, o.Y), f1 = bz - water.DzDx * bx - water.DzDy * by;
                sw = Math.Abs(f1) > 1e-12 ? Math.Clamp(-f0 / f1, s - Step, s) : s;
            }
            double l = Math.Sqrt(water.DzDx * water.DzDx + water.DzDy * water.DzDy + 1);
            Add(forwardIn ? CrossingKind.WaterIn : CrossingKind.WaterOut, PlateKind.None, -1, sw, normal: new P3(-water.DzDx / l, -water.DzDy / l, 1 / l));
        }

        // ------------------------------------------------------------------ the armour planes

        /// <summary>The armour decks (with their openings: inside a barbette's bore, none, even an unarmoured one's (the
        /// user, 2026-10-13: a hole, to be fixed in the designer); over an uptake, a grating)
        /// and the armoured bulkheads, crossed exactly, anywhere on the segment inside the hull.</summary>
        void Planes()
        {
            if (Math.Abs(bz) > 1e-9)
                foreach (int i in g.Decks)
                {
                    ref readonly var d = ref c.ArmourDecks[i];
                    double s = (d.Z - o.Z) / bz;
                    if (s < 0 || s > len)
                        continue;
                    double x = X(s), y = Y(s);
                    if (x < d.X0 - Eps || x > d.X1 + Eps || Math.Abs(y) > hull.HalfWidth(x, d.Z) + Eps)
                        continue;
                    var kind = PlateKind.ArmourDeck;
                    foreach (int t in g.Tubes)
                    {
                        ref readonly var b = ref g.Solids[t];
                        if (b.Box.Base <= d.Z + 0.01 && b.Box.Top > d.Z && ShipSolids.InBore(b, x, y))
                            kind = PlateKind.None;
                    }
                    if (kind == PlateKind.None)
                        continue;
                    foreach (int u in g.Uptakes)
                    {
                        ref readonly var up = ref g.Solids[u];
                        if (up.Box.Base < d.Z + 0.01 && up.Box.Top > d.Z - 0.01 && g.InPlan(up, x, y))
                            kind = PlateKind.Grating;
                    }
                    Add(CrossingKind.Plate, kind, i, s, d.ThicknessMm, d.Material, new P3(0, 0, 1));
                }
            if (Math.Abs(bx) > 1e-9)
                foreach (int i in g.Bulkheads)
                {
                    ref readonly var b = ref c.ArmourBulkheads[i];
                    double s = (b.X - o.X) / bx;
                    if (s < 0 || s > len)
                        continue;
                    double y = Y(s), z = Z(s), xh = b.X - Math.Sign(b.X) * 0.01;   // a plate on an end face: the hull just inside it
                    if (z < b.Bottom - Eps || z > b.Top + Eps || z > hull.DeckTop(xh) + Eps || Math.Abs(y) > hull.HalfWidth(xh, z) + Eps)
                        continue;
                    double mm = !double.IsNaN(b.LowerTop) && !double.IsNaN(b.LowerMm) && z < b.LowerTop ? b.LowerMm : b.ThicknessMm;
                    Add(CrossingKind.Plate, PlateKind.ArmourBulkhead, i, s, mm, b.Material, new P3(Math.Sign(b.X == 0 ? 1 : b.X), 0, 0));
                }
        }

        // ------------------------------------------------------------------ the cells

        /// <summary>From cell `from` (at s - Step) to `to` (at s): each box left on the way, where the walk leaves it
        /// (or the hull, sh), with the deck or bulkhead between neighbours.</summary>
        void Cells(double s, double sh, int from, int to)
        {
            double sa = s - Step;
            int cur = from;
            for (int guard = 0; cur != to && guard < 8; guard++)
            {
                double sx;
                int next;
                if (cur >= 0)
                {
                    var b = c.Cells[cur].Box;
                    double a0 = sa, a1 = s + Step;
                    sx = Clip(o.X, bx, b.X0, b.X1, ref a0, ref a1) && Clip(o.Y, by, b.Y0, b.Y1, ref a0, ref a1) && Clip(o.Z, bz, b.Base, b.Top, ref a0, ref a1)
                        ? Math.Clamp(a1, sa, s) : sa;
                    if (!double.IsNaN(sh) && sh >= sa && sh <= sx)
                        (sx, next) = (sh - Nudge, -1);   // out through the hull's skin
                    else
                    {
                        double sn = sx + 1e-6;
                        next = HullAt(sn) ? g.CellAt(X(sn), Y(sn), Z(sn)) : -1;
                        if (next == cur)
                            next = to;   // the box's edge, numerically: take the sample's word
                    }
                }
                else
                {
                    var b = c.Cells[to].Box;
                    double a0 = sa, a1 = s;
                    sx = !double.IsNaN(sh) ? sh + Nudge
                        : Clip(o.X, bx, b.X0, b.X1, ref a0, ref a1) && Clip(o.Y, by, b.Y0, b.Y1, ref a0, ref a1) && Clip(o.Z, bz, b.Base, b.Top, ref a0, ref a1)
                            ? a0 : s;
                    next = to;
                }
                CellChange(sx, cur, next);
                (cur, sa) = (next, Math.Max(sa, sx));
            }
        }

        /// <summary>The walk goes from cell a to b at s, so the path from b to a: out of b, through the plate between
        /// them, into a (added in reverse).</summary>
        void CellChange(double s, int a, int b)
        {
            if (a >= 0)
                Add(CrossingKind.CellIn, PlateKind.None, a, s);
            if (a >= 0 && b >= 0)
                foreach (var e in c.Neighbours(c.Cells[a].Neighbours))
                {
                    if (e.Cell != b || e.Join == JoinKind.Open)
                        continue;
                    if (e.Join == JoinKind.Deck)
                    {
                        ref readonly var d = ref c.Decks[e.Through];
                        Add(CrossingKind.Plate, PlateKind.Deck, e.Through, s, d.PlateMm, d.Material >= 0 ? d.Material : c.Plating.Material, new P3(0, 0, 1));
                    }
                    else
                    {
                        ref readonly var w = ref c.Bulkheads[e.Through];
                        var n = w.Side == Side.None ? new P3(Math.Sign(w.X == 0 || double.IsNaN(w.X) ? 1 : w.X), 0, 0)
                            : new P3(0, w.Side == Side.Port ? -1 : 1, 0);
                        Add(CrossingKind.Plate, PlateKind.Bulkhead, e.Through, s, w.PlateMm, c.Plating.Material, n);
                    }
                    break;
                }
            if (b >= 0)
                Add(CrossingKind.CellOut, PlateKind.None, b, s);
        }

        // ------------------------------------------------------------------ the parts

        /// <summary>Into or out of part k at s: which of its skins (a turret's face, side, rear or roof; a barbette's wall
        /// or its open top), how thick, and its normal.</summary>
        void PartCrossing(int k, double s, bool forwardIn)
        {
            ref readonly var sol = ref g.Solids[path.parts[k]];
            ref readonly var r = ref c.Components[sol.Component];
            double x = X(s), y = Y(s), z = Z(s);
            bool top = Math.Abs(z - sol.Box.Top) < 0.02, bottom = Math.Abs(z - sol.Box.Base) < 0.02;
            var plate = sol.Armoured && sol.Shape != ShipSolids.Shape.Turret ? PlateKind.PartArmour : r.PlateMm > 0 ? PlateKind.PartPlating : PlateKind.None;
            double mm = plate == PlateKind.PartArmour ? r.ArmourMm : r.PlateMm;
            P3 n;
            switch (sol.Shape)
            {
                case ShipSolids.Shape.Turret:
                {
                    ref readonly var m = ref c.Mounts[sol.Mount];
                    double a = double.DegreesToRadians(path.partBearing[k]), cs = Math.Cos(a), sn = Math.Sin(a);
                    var (lx, ly) = ShipSolids.ToTurret(sol, x, y, path.partBearing[k]);
                    var (nx, ny) = top || bottom ? (0.0, 0.0) : EdgeNormal(c.Points(sol.Outline), lx, ly);
                    n = top ? new P3(0, 0, 1) : bottom ? new P3(0, 0, -1) : new P3(nx * cs - ny * sn, nx * sn + ny * cs, 0);
                    (plate, mm) = !m.Turret ? (plate, mm)
                        : top ? (PlateKind.TurretRoof, m.RoofMm)
                        : bottom ? (PlateKind.None, 0.0)
                        : nx > 0.5 ? (PlateKind.TurretFace, m.FaceMm)
                        : nx < -0.5 ? (PlateKind.TurretRear, m.RearMm)
                        : (PlateKind.TurretSide, m.SideMm);
                    break;
                }
                case ShipSolids.Shape.Tube or ShipSolids.Shape.Cylinder:
                {
                    double ex = x - sol.Cx, ey = y - sol.Cy, d = Math.Sqrt(ex * ex + ey * ey);
                    bool wall = Math.Abs(d - sol.R) < 0.01;
                    n = wall && d > 0 ? new P3(ex / d, ey / d, 0) : new P3(0, 0, z > (sol.Box.Base + sol.Box.Top) / 2 ? 1 : -1);
                    if (sol.Shape == ShipSolids.Shape.Tube)
                        (plate, mm) = wall ? (PlateKind.Barbette, r.ArmourMm) : (PlateKind.None, 0.0);   // the open top
                    break;
                }
                case ShipSolids.Shape.Capsule:
                {
                    ShipSolids.SegmentDistance2(sol, x, y, z, out double u);
                    double ex = x - (sol.A.X + u * (sol.B.X - sol.A.X)), ey = y - (sol.A.Y + u * (sol.B.Y - sol.A.Y)), ez = z - (sol.A.Z + u * (sol.B.Z - sol.A.Z));
                    double d = Math.Sqrt(ex * ex + ey * ey + ez * ez);
                    n = d > 0 ? new P3(ex / d, ey / d, ez / d) : new P3(0, 0, 1);
                    break;
                }
                case ShipSolids.Shape.Prism when !top && !bottom:
                {
                    var (nx, ny) = EdgeNormal(c.Points(sol.Outline), x, y);
                    n = new P3(nx, ny, 0);
                    break;
                }
                default:
                {
                    // a box's nearest face (a prism's top or bottom)
                    var b = sol.Box;
                    double best = Math.Min(Math.Abs(z - b.Top), Math.Abs(z - b.Base));
                    n = new P3(0, 0, Math.Abs(z - b.Top) <= Math.Abs(z - b.Base) ? 1 : -1);
                    if (sol.Shape == ShipSolids.Shape.Box)
                        foreach (var (dd, cand) in (ReadOnlySpan<(double, P3)>)[(Math.Abs(x - b.X1), new P3(1, 0, 0)), (Math.Abs(x - b.X0), new P3(-1, 0, 0)),
                                     (Math.Abs(y - b.Y1), new P3(0, 1, 0)), (Math.Abs(y - b.Y0), new P3(0, -1, 0))])
                            if (dd < best)
                                (best, n) = (dd, cand);
                    break;
                }
            }
            Add(forwardIn ? CrossingKind.PartIn : CrossingKind.PartOut, plate, sol.Component, s, mm, r.Material, n);
        }

        /// <summary>The outward unit normal of the polygon's edge nearest (x, y).</summary>
        static (double X, double Y) EdgeNormal(ReadOnlySpan<Pt> pts, double x, double y)
        {
            double best = double.PositiveInfinity, nx = 1, ny = 0;
            int n = pts.Length;
            for (int i = 0; i < n; i++)
            {
                var (x1, y1) = pts[i];
                var (x2, y2) = pts[(i + 1) % n];
                double ex = x2 - x1, ey = y2 - y1, l2 = ex * ex + ey * ey;
                if (l2 <= 0)
                    continue;
                double u = Math.Clamp(((x - x1) * ex + (y - y1) * ey) / l2, 0, 1), px = x1 + u * ex - x, py = y1 + u * ey - y, d2 = px * px + py * py;
                if (d2 < best)
                {
                    double l = Math.Sqrt(l2);
                    (best, nx, ny) = (d2, ey / l, -ex / l);
                    // outward: a step along it leaves the polygon
                    double mx = x1 + ex / 2, my = y1 + ey / 2;
                    if (ShipSolids.PointInPolygon(mx + nx * 1e-4, my + ny * 1e-4, pts))
                        (nx, ny) = (-nx, -ny);
                }
            }
            return (nx, ny);
        }
    }
}
