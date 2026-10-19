using System.Globalization;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen contents`: what stands in each room in a shell's path (docs/battle/shell-contents.md C1b), and the
/// chance a crossing meets a solid there, against damage/09 §2.3's anchors: a beam crossing at a random height meets one
/// with the band's share of the room's height × (1 − e^(−L/λ)), L the room's width.</summary>
static class ContentsBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IReadOnlyList<string> names, Func<string, ShipClass> classOf)
    {
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
        }
        return 0;
    }

    static string Solid(ShipClass c, in SolidAt s) => string.Format(Inv, "{0} {1:0}-{2:0} mm {3} ({4})", s.Name, s.LoMm, s.HiMm,
        c.Material(s.Material), s.Shape.ToString().ToLowerInvariant());
}
