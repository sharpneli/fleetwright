using System.Globalization;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen contents`: what stands in each room in a shell's path (docs/battle/shell-contents.md C1b), and the
/// chance a crossing meets a solid there, against damage/09 §2.3's anchors: a beam crossing at a random height meets one
/// with the band's share of the room's height × (1 − e^(−L/λ)), L the room's width.</summary>
static class ContentsBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IReadOnlyList<string> names, Func<string, ShipClass> classOf, SimSettings s)
    {
        Media(s);
        foreach (var n in names)
        {
            var c = classOf(n);
            Console.WriteLine(string.Format(Inv, "{0}: {1:0} t, size {2:0.00} between the small rows and the large; boilers {3}, engines {4}",
                c.Name, c.Hydrostatics.DisplacementT, Contents.SizeOf(c.Hydrostatics.DisplacementT), c.Boilers, c.Engines));
            for (int i = 0; i < c.Rooms.Length; i++)
            {
                ref readonly var k = ref c.ContentsOf(i);
                if (!k.Some)
                    continue;
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity, y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
                foreach (int cell in c.Links(c.Rooms[i].Cells))
                {
                    var b = c.Cells[cell].Box;
                    (lo, hi, y0, y1) = (Math.Min(lo, b.Base), Math.Max(hi, b.Top), Math.Min(y0, b.Y0), Math.Max(y1, b.Y1));
                }
                double width = y1 - y0, band = (k.ZHi - k.ZLo) / (hi - lo), meet = band * (1 - Math.Exp(-width / k.LambdaM));
                Console.WriteLine(string.Format(Inv, "  {0,-28} {1}: {2:0.0} m wide, {3:0.0} m high, band {4:0.0}-{5:0.0} m, λ {6:0.0} m: meets a solid {7:0.00}",
                    c.RoomId(i), k.Name, width, hi - lo, k.ZLo - lo, k.ZHi - lo, k.LambdaM, meet));
                foreach (var o in c.ContentObjects(k.Mix))
                    Console.WriteLine(string.Format(Inv, "      {0,-32} {1}{2}{3}", o.Name, Solid(c, o.Skin),
                        o.InteriorRho > 0 ? string.Format(Inv, ", {0:0} kg/m³ over {1:0.00} of it", o.InteriorRho, o.InteriorShare) : "",
                        o.CoreChance > 0 ? string.Format(Inv, ", then {0} with {1:0.00}", Solid(c, o.Core), o.CoreChance) : ""));
            }
            // what the rooms stow, at the default fuel share and full (C2-C3)
            var level = new double[c.Rooms.Length];
            var full = new double[c.Rooms.Length];
            Stowage.Levels(c, s.FuelShareDefault, s.ProtectiveBunkersLast, level);
            Stowage.Levels(c, 1, s.ProtectiveBunkersLast, full);
            for (int i = 0; i < c.Rooms.Length; i++)
            {
                ref readonly var st = ref c.Stowages[i];
                if (!st.Some)
                    continue;
                double h = st.Top - st.Floor;
                Console.WriteLine(string.Format(Inv, "  {0,-28} {1} ({2:0} kg/m³{3}{4}): {5:0.0} m high, {6:0.0} m deep at {7:0.##} of a load, {8:0.0} m full",
                    c.RoomId(i), st.Name, st.Rho, st.Fuel ? ", fuel" : "", st.Protective ? ", protective" : "", h,
                    double.IsNaN(level[i]) ? 0 : level[i] - st.Floor, s.FuelShareDefault, double.IsNaN(full[i]) ? 0 : full[i] - st.Floor));
            }
        }
        return 0;
    }

    /// <summary>Damage/09's anchors for the media (§3-4): 2 ft of coal against shells of 4-15" at 500 m/s, as the speed it
    /// costs and the mild steel that costs as much (the plate's limit V_L with v_out² = v² − V_L², q 0.7); 4 m of liquid
    /// against a 15", 3 m of oil against a 12".</summary>
    static void Media(SimSettings s)
    {
        Console.WriteLine("the media (damage/09 §3-4), at 500 m/s:");
        foreach (double inch in (ReadOnlySpan<double>)[4, 6, 8, 12, 15])
        {
            double cal = inch * 25.4, mass = 0.4 * 0.45359237 * inch * inch * inch;
            var shell = new ShellTypeRow { CalibreMm = cal, MassKg = mass };
            double v = 500, run = 0.6096, l = WaterRun.DecayM(shell, s, Stowage.CoalRho, MediumKind.Granular);
            double kept = Math.Exp(-run / l), vl = v * Math.Sqrt(1 - kept * kept);
            double mild = Terminal.PenMm(vl, cal, mass, 0) / 0.7;
            Console.WriteLine(string.Format(Inv, "  2 ft of coal, {0}\": keeps {1:0.0}% of its speed, as {2:0.00} in of mild steel (09: about 1 in)", inch,
                100 * kept, mild / 25.4));
        }
        foreach (var (inch, metres, rho, what) in new[] { (15.0, 4.0, WaterRun.SeaRho, "a liquid layer"), (12.0, 3.0, Stowage.OilRho, "oil") })
        {
            var shell = new ShellTypeRow { CalibreMm = inch * 25.4 };
            Console.WriteLine(string.Format(Inv, "  {0} m of {1}, {2}\": keeps {3:0}% of its speed", metres, what, inch,
                100 * Math.Exp(-metres / WaterRun.DecayM(shell, s, rho, MediumKind.Liquid))));
        }
    }

    static string Solid(ShipClass c, in SolidAt s) => string.Format(Inv, "{0} {1:0}-{2:0} mm {3} ({4})", s.Name, s.LoMm, s.HiMm,
        c.Material(s.Material), s.Shape.ToString().ToLowerInvariant());
}
