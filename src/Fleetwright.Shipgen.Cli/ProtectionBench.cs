using System.Globalization;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen protection`: what stands between the sea and each magazine (docs/shipgen/protection-plan.md H0),
/// through the game's tracer, with no damage model: each magazine's crown against the waterline, the layers a level line
/// from the side and a plumb line from above cross to reach it, and shells at falls of 5° to 35° from the beam aimed at
/// points inside it: the share that meets the sea first (and how far they'd run in it), the steel they cross before it,
/// and the share that gets in from above the water through the ship's skin alone, no deck or wall inside it ("open").</summary>
static class ProtectionBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly double[] Falls = [5, 10, 20, 35];
    const double Far = 400;

    sealed record Line(string Name, double CrownM, bool AtSide, double[] Water, double[] Open, double[] SteelMm, double VolumeM3);

    public static int Run(IReadOnlyList<string> targets, Func<string, Design> load, int shells, int seed, bool quiet)
    {
        var summary = new List<(string Ship, List<Line> Lines)>();
        foreach (var t in targets)
        {
            var c = ShipClassCompiler.Compile(ShipDesign.Build(load(t)));
            var lines = Ship(c, shells, seed, quiet);
            summary.Add((c.Name, lines));
        }
        if (summary.Count > 1 || quiet)
        {
            Console.WriteLine();
            Console.WriteLine("ship                                      crown  at side  open 5°/10°/20°/35°     sea first 5°/10°/20°/35°   steel 10° (median)");
            foreach (var (ship, lines) in summary)
            {
                if (lines.Count == 0)
                {
                    Console.WriteLine(string.Format(Inv, "{0,-40}  no magazines", Trim(ship, 40)));
                    continue;
                }
                double v = lines.Sum(l => l.VolumeM3);
                string Mean(Func<Line, double[]> f) => string.Join("/", Enumerable.Range(0, Falls.Length)
                    .Select(i => string.Format(Inv, "{0,3:0}", 100 * lines.Sum(l => f(l)[i] * l.VolumeM3) / v)));
                Console.WriteLine(string.Format(Inv, "{0,-40} {1,6:+0.0;-0.0;0.0} {2,5}/{3,-3} {4,-22} {5,-26} {6,5:0}", Trim(ship, 40), lines.Max(l => l.CrownM),
                    lines.Count(l => l.AtSide), lines.Count, Mean(l => l.Open) + " %", Mean(l => l.Water) + " %",
                    lines.Sum(l => l.SteelMm[1] * l.VolumeM3) / v));
            }
        }
        return 0;
    }

    static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    static List<Line> Ship(ShipClass c, int shells, int seed, bool quiet)
    {
        var v = c.Vertical;
        double wl = v.Waterline;
        var water = Waterplane.Level(c);
        var path = new HitPath();
        var rng = new Random(seed);
        if (!quiet)
            Console.WriteLine(string.Format(Inv, "{0}: draught {1:0.00} m, waterline {2:0.00} m under the main deck", c.Name, v.Draught, -wl));
        var lines = new List<Line>();
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            ref readonly var r = ref c.Rooms[room];
            if (r.Kind != RoomKind.Magazine)
                continue;
            var cells = new List<int>();
            foreach (int ci in c.Links(r.Cells))
                if (c.Cells[ci].Room == room)
                    cells.Add(ci);
            if (cells.Count == 0)
                continue;
            double y0 = cells.Min(i => c.Cells[i].Box.Y0), y1 = cells.Max(i => c.Cells[i].Box.Y1);
            double top = cells.Max(i => c.Cells[i].Box.Top), bottom = cells.Min(i => c.Cells[i].Box.Base);
            double xm = (r.X0 + r.X1) / 2, zm = (bottom + top) / 2, ym = (y0 + y1) / 2;
            int mid = cells.MinBy(i => Dist(c.Cells[i].Box, xm, ym, zm));
            var mb = c.Cells[mid].Box;
            (xm, zm) = ((mb.X0 + mb.X1) / 2, (mb.Base + mb.Top) / 2);

            // a level line in from the starboard side, a plumb line down from above
            ShipTracer.Trace(c, new P3(xm, Far, zm), new P3(xm, ym, zm), [], water, path);
            var side = Layers(c, path, room, out bool atSide);
            ShipTracer.Trace(c, new P3(xm, ym, v.Freeboard + 60), new P3(xm, ym, zm), [], water, path);
            var above = Layers(c, path, room, out _);

            // shells from the starboard beam at each fall, aimed at points in it (by the cells' volume)
            double vol = cells.Sum(i => c.Cells[i].VolumeM3);
            var wet = new double[Falls.Length];
            var open = new double[Falls.Length];
            var steel = new double[Falls.Length];
            var runs = new double[Falls.Length];
            for (int f = 0; f < Falls.Length; f++)
            {
                double a = double.DegreesToRadians(Falls[f]);
                var dir = new P3(0, -Math.Cos(a), -Math.Sin(a));
                var mms = new List<double>();
                var wetRuns = new List<double>();
                for (int n = 0, tries = 0; n < shells && tries < 20 * shells; tries++)
                {
                    double pick = rng.NextDouble() * vol;
                    int ci = cells[^1];
                    foreach (int i in cells)
                        if ((pick -= c.Cells[i].VolumeM3) <= 0)
                        {
                            ci = i;
                            break;
                        }
                    var b = c.Cells[ci].Box;
                    var p = new P3(b.X0 + rng.NextDouble() * (b.X1 - b.X0), b.Y0 + rng.NextDouble() * (b.Y1 - b.Y0), b.Base + rng.NextDouble() * (b.Top - b.Base));
                    if (p.Z < c.Hull.Keel || Math.Abs(p.Y) >= c.Hull.HalfWidth(p.X, p.Z))
                        continue;   // a cell is a box: its corners stand out of the hull's round bilge
                    ShipTracer.Trace(c, new P3(p.X - dir.X * Far, p.Y - dir.Y * Far, p.Z - dir.Z * Far), p, [], water, path);
                    if (!Shot(c, path, room, out double mm, out double run, out int inner))
                        continue;
                    n++;
                    mms.Add(mm);
                    if (run > 0.05)
                        wetRuns.Add(run);
                    else if (inner == 0)
                        open[f]++;
                }
                wet[f] = (double)wetRuns.Count / Math.Max(mms.Count, 1);
                open[f] /= Math.Max(mms.Count, 1);
                steel[f] = Median(mms);
                runs[f] = wetRuns.Count > 0 ? Median(wetRuns) : 0;
            }
            double crown = top - wl;
            lines.Add(new Line(c.RoomId(room), crown, atSide, wet, open, steel, vol));
            if (quiet)
                continue;
            Console.WriteLine(string.Format(Inv, "  {0}: x {1:0.0} to {2:0.0}, crown {3:+0.00;-0.00} m on the waterline, floor {4:0.00}, {5:0.0} m of the beam{6}",
                c.RoomId(room), r.X0, r.X1, crown, bottom - wl, y1 - y0, atSide ? ", against the side" : ""));
            Console.WriteLine("    from the side:  " + side);
            Console.WriteLine("    from above:     " + above);
            for (int f = 0; f < Falls.Length; f++)
                Console.WriteLine(string.Format(Inv, "    fall {0,2:0}°: sea first {1,3:0}% (run {2:0.0} m), steel before it {3,4:0} mm (median), open {4,3:0}%",
                    Falls[f], 100 * wet[f], runs[f], steel[f], 100 * open[f]));
        }
        if (!quiet && lines.Count == 0)
            Console.WriteLine("  no magazines");
        return lines;
    }

    static double Dist(in Box b, double x, double y, double z)
    {
        double dx = x - Math.Clamp(x, b.X0, b.X1), dy = y - Math.Clamp(y, b.Y0, b.Y1), dz = z - Math.Clamp(z, b.Base, b.Top);
        return dx * dx + dy * dy + dz * dz;
    }

    static double Median(List<double> xs)
    {
        xs.Sort();
        return xs.Count == 0 ? 0 : xs[xs.Count / 2];
    }

    static bool Into(ShipClass c, in Crossing x, int room) => x.Kind == CrossingKind.CellIn && c.Cells[x.Index].Room == room;

    /// <summary>The steel a shot crosses before it gets into the room, the metres it ran in the sea before the ship, and
    /// the plates inside the hull it crossed (decks, walls, armour behind the skin); false if it never got in.</summary>
    static bool Shot(ShipClass c, HitPath path, int room, out double mm, out double run, out int inner)
    {
        (mm, run, inner) = (0, 0, 0);
        var xs = path.Crossings;
        bool inShip = false;
        for (int i = 0; i < xs.Length; i++)
        {
            ref readonly var x = ref xs[i];
            if (Into(c, x, room))
                return inShip;
            if (!inShip && x.Kind is CrossingKind.HullIn or CrossingKind.PartIn)
            {
                inShip = true;
                run = WaterRun.RunM(path, i);
            }
            if (x.Kind is CrossingKind.Plate or CrossingKind.HullIn or CrossingKind.HullOut or CrossingKind.PartIn or CrossingKind.PartOut && x.Mm > 0
                && x.Plate != PlateKind.Backing)   // (wood or cement: not steel)
                mm += x.Mm;
            if (x.Kind == CrossingKind.Plate && x.Plate is PlateKind.Deck or PlateKind.Bulkhead or PlateKind.ArmourDeck or PlateKind.ArmourBulkhead or PlateKind.Grating
                or PlateKind.Inclined)
                inner++;
        }
        return false;
    }

    /// <summary>The layers a line crossed into the room, outside in: plates with their mm, spaces with the metres in them.</summary>
    static string Layers(ShipClass c, HitPath path, int room, out bool atSide)
    {
        var parts = new List<string>();
        var xs = path.Crossings;
        double inT = double.NaN;
        string? space = null;
        int spaces = 0;
        atSide = false;
        for (int i = 0; i < xs.Length; i++)
        {
            ref readonly var x = ref xs[i];
            if (Into(c, x, room))
            {
                atSide = spaces == 0;
                parts.Add(c.RoomId(room));
                break;
            }
            switch (x.Kind)
            {
                case CrossingKind.HullIn:
                    parts.Add(string.Format(Inv, "{0} {1:0.#}", x.Plate switch { PlateKind.HullDeck => "deck", PlateKind.HullEnd => "end", _ => "shell" }, x.Mm));
                    break;
                case CrossingKind.WaterIn:
                    space = null;   // the sea outside: the shots' "sea first" says what it does
                    break;
                case CrossingKind.WaterOut:
                    break;
                case CrossingKind.PartIn:
                    parts.Add(string.Format(Inv, "{0} {1:0.#}", c.ComponentId(x.Index), x.Mm));
                    break;
                case CrossingKind.CellIn:
                    if (space != null)
                        parts.Add(string.Format(Inv, "[{0} {1:0.0} m]", space, x.T - inT));
                    inT = x.T;
                    int rr = c.Cells[x.Index].Room;
                    space = rr >= 0 ? c.RoomId(rr) : c.CellId(x.Index);
                    spaces++;
                    break;
                case CrossingKind.CellOut:
                    if (space != null && !double.IsNaN(inT))
                        parts.Add(string.Format(Inv, "[{0} {1:0.0} m]", space, x.T - inT));
                    space = null;
                    break;
                case CrossingKind.Plate when x.Mm > 0:
                    parts.Add(string.Format(Inv, "{0} {1:0.#}", Plate(c, x), x.Mm));
                    break;
            }
        }
        return string.Join(" | ", parts);
    }

    static string Plate(ShipClass c, in Crossing x) => x.Plate switch
    {
        PlateKind.SideArmour => c.SidePlateId(x.Index).ToLowerInvariant(),
        PlateKind.ArmourDeck => "armour deck",
        PlateKind.ArmourBulkhead => "armour bulkhead",
        PlateKind.Inclined => c.InclinedId(x.Index).ToLowerInvariant(),
        PlateKind.Grating => "grating",
        PlateKind.Deck => c.DeckId(x.Index).ToLowerInvariant(),
        PlateKind.Bulkhead => c.Bulkheads[x.Index].Kind.ToString().ToLowerInvariant() + " bulkhead",
        _ => x.Plate.ToString().ToLowerInvariant(),
    };
}
