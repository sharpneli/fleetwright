using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen ballistics`: each battery's range table, from the shell it fires (docs/battle/fighting.md 4.2a,
/// "The instruments"): range, elevation, time of flight, angle of fall and striking velocity, from a muzzle 10 m up;
/// the table's size and build time. --research adds the research's 16"/50 against its sourced times and falls.</summary>
static class BallisticsBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IEnumerable<string> designs, bool research, Func<string, Design> load)
    {
        foreach (var p in designs)
        {
            var cls = ShipClassCompiler.Compile(ShipDesign.Build(load(p)));
            Console.WriteLine(cls);
            foreach (var b in cls.Batteries)
            {
                var shell = cls.ShellTypes[b.Fires];
                var key = BallisticKey.Of(shell, cls.Mounts[cls.Links(b.Mounts)[0]].MaxElevationDeg);
                Table($"{b.Kind.ToString().ToLowerInvariant()} {b.CalibreMm:0}/{b.CalibreLength:0}: {cls.ShellTypeId(b.Fires)}", key);
            }
        }
        if (research)
        {
            // fire-control-research.md §5.1 (navweaps): the form factor fitted on the sphere to 38,720 m at 45°
            Table("16\"/50 Mk 7 (fire_control_ref.py GUNS; sourced: 10 kyd 13.2 s 5.0°, 20 kyd 29.6 s 14.9°, 30 kyd 50.3 s 28.3°)",
                new BallisticKey(406, 1225, 762, 1.188, 45));
        }
        return 0;
    }

    static void Table(string name, BallisticKey key)
    {
        var sw = Stopwatch.StartNew();
        var t = new BallisticTable(key);
        double ms = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine(string.Format(Inv, "  {0}\n    {1:0.#} kg at {2:0} m/s, form factor {3:0.00}, to {4:0}°: table {5:0} KB, built in {6:0} ms",
            name, key.MassKg, key.MuzzleVelocityMs, key.FormFactor, key.MaxElevationDeg, t.Bytes / 1024.0, ms));
        var max = t.Fall(key.MaxElevationDeg, 10);
        Console.WriteLine(string.Format(Inv, "    reach {0:0} m ({1:0.0} kyd) at {2:0}°", max.Range, max.Range / 914.4, key.MaxElevationDeg));
        Console.WriteLine("        kyd      m   elev°   tof s   fall°   strike m/s");
        for (double kyd = 2; kyd * 914.4 <= max.Range; kyd += kyd < 10 ? 2 : 5)
        {
            double e = t.ElevationFor(kyd * 914.4, 10);
            var f = t.Fall(e, 10);
            Console.WriteLine(string.Format(Inv, "    {0,7:0} {1,6:0} {2,7:0.00} {3,7:0.0} {4,7:0.0} {5,10:0}", kyd, kyd * 914.4, e, f.Tof, f.FallDeg, f.StrikeMs));
        }
    }
}
