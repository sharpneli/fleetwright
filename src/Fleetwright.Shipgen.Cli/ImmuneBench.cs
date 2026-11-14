using System.Diagnostics;
using System.Globalization;
using System.Text;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen immune`: a design's immune zones (docs/designer-ui/immune-zone.md) against one of her own batteries'
/// shells, at its calibre or another (her own shells at that calibre): the share of the shells at the magazines and at
/// the machinery that get in through the side and through the deck, by range and target angle; the edges by angle and
/// the plates that let the most in; and what it cost.</summary>
static class ImmuneBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(bool Main, int Index, double CalibreMm, string? Kind, string? Vitals, int Shots, int Threads, int Seed, bool Passes, string? Trace = null,
        int Anger = 0, int PerShip = 10, double SettleMin = 30);

    public static int Run(string target, Func<string, Design> load, SimSettings settings, Options o)
    {
        var design = load(target);
        var sw = Stopwatch.StartNew();
        var cls = ShipClassCompiler.Compile(ShipDesign.Build(design));
        double compileMs = sw.Elapsed.TotalMilliseconds;
        var list = (o.Main ? design.Main : design.Secondary) ?? throw new ArgumentException($"{target} has no {(o.Main ? "main" : "secondary")} battery");
        if (o.Index < 0 || o.Index >= list.Count)
            throw new ArgumentException($"{target} has no {(o.Main ? "main" : "secondary")} battery {o.Index}");
        double own = list[o.Index].CalibreMm ?? throw new ArgumentException("the battery has no calibre");
        double cal = double.IsNaN(o.CalibreMm) ? own : o.CalibreMm;
        var shells = Loadout.ShellsAt(design, o.Main, o.Index, cal);
        string want = (o.Kind ?? "ap").ToLowerInvariant();
        var pick = shells.FirstOrDefault(s => s.Kind == want || want == "common" && s.Kind == "sap");
        if (pick.Name == null)
            throw new ArgumentException($"the battery carries no {want} ({string.Join(", ", shells.Select(s => s.Kind))})");
        // her mounts' elevation: the class's battery of this gun
        double maxElev = 0;
        foreach (ref readonly var b in cls.Batteries)
            if (b.Kind == (o.Main ? Battery.Main : Battery.Secondary) && Math.Abs(b.CalibreMm - own) < 0.5)
                foreach (int m in cls.Links(b.Mounts))
                    maxElev = Math.Max(maxElev, cls.Mounts[m].MaxElevationDeg);
        if (maxElev <= 0)
            throw new ArgumentException("no mounts for the battery");

        if (o.Anger > 0)
            return Anger(cls, pick, cal, maxElev, settings, o);
        var zone = new ImmuneZone(cls, pick.Shell, maxElev, settings, (ulong)o.Seed);
        if (o.Trace is { } tr)
        {
            // one cell's shells, walked on one thread: vitals:km:angle
            var parts = tr.Split(':');
            var tv = parts[0].StartsWith("mach", StringComparison.OrdinalIgnoreCase) ? Vitals.Machinery : Vitals.Magazines;
            int cellAt = zone.Index(tv, (int)Math.Round(double.Parse(parts[1]) * 1000 / ImmuneZone.RangeStepM) - 1, Array.IndexOf(ImmuneZone.Angles, double.Parse(parts[2])));
            int shown = 0;
            zone.Each = (i, path, t, tin) =>
            {
                if (i != cellAt || shown++ >= 6)
                    return;
                Console.WriteLine($"shell: {t.Outcome}, stop {t.StopT:0.00}, burst {t.BurstT:0.00}, plate {t.Plate}, vitals at {tin:0.00}, strike {t.StrikeMs:0}");
                foreach (var x in path.Crossings)
                    Console.WriteLine($"   {x.T,7:0.00} {x.Kind,-8} {x.Plate,-14} [{x.Index}] {x.Mm,6:0} mm  n=({x.Normal.X:0.00},{x.Normal.Y:0.00},{x.Normal.Z:0.00}) " +
                                      (x.Kind is CrossingKind.CellIn && x.Index >= 0 ? $"{cls.Cells[x.Index].Room} {(cls.Cells[x.Index].Room >= 0 ? cls.Rooms[cls.Cells[x.Index].Room].Kind : default)}" : ""));
            };
            zone.Fill(8, 1);
            return 0;
        }
        Console.WriteLine($"{cls.Name}: against her own {(o.Main ? "main" : "secondary")} battery at {cal:0} mm ({pick.Name}, {pick.Shell.MassKg:0} kg, " +
                               $"{pick.Shell.MuzzleVelocityMs:0} m/s), guns to {maxElev:0}°: reach {zone.ReachM / 1000:0.0} km; compiled in {compileMs:0} ms");
        int threads = o.Threads > 0 ? o.Threads : Environment.ProcessorCount;
        int[] passes = o.Passes ? [.. new[] { 16, 64, o.Shots }.Where(x => x <= o.Shots).Distinct()] : [o.Shots];
        foreach (int p in passes)
        {
            sw.Restart();
            zone.Fill(p, threads);
            long drawn = 0, shots = 0, traced = 0, hidden = 0;
            foreach (var v in new[] { Vitals.Magazines, Vitals.Machinery })
                for (int r = 0; r < zone.RangesM.Length; r++)
                    for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                        (drawn, shots, traced, hidden) = (drawn + zone[v, r, a].Drawn, shots + zone[v, r, a].Shots, traced + zone[v, r, a].Traced,
                            hidden + (zone[v, r, a].Hidden ? 1 : 0));
            Console.WriteLine($"  filled to {p} a cell on {threads} threads: {sw.Elapsed.TotalMilliseconds:0} ms in all " +
                                   $"({shots} shells walked, {drawn} lines drawn, {traced} traced; {hidden} cells hidden)");
        }
        // one thread's cost a shell, for the designer's budget
        if (o.Threads != 1)
        {
            var one = new ImmuneZone(cls, pick.Shell, maxElev, settings, (ulong)o.Seed);
            sw.Restart();
            one.Fill(16, 1);
            long shots = 0;
            foreach (var v in new[] { Vitals.Magazines, Vitals.Machinery })
                for (int r = 0; r < one.RangesM.Length; r++)
                    for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                        shots += one[v, r, a].Shots;
            Console.WriteLine($"  one thread, 16 a cell: {sw.Elapsed.TotalMilliseconds:0} ms, {1000 * sw.Elapsed.TotalMilliseconds / Math.Max(shots, 1):0} µs a shell");
        }

        foreach (var v in new[] { Vitals.Magazines, Vitals.Machinery })
        {
            if (o.Vitals != null && !v.ToString().StartsWith(o.Vitals, StringComparison.OrdinalIgnoreCase))
                continue;
            Console.WriteLine();
            if (!zone.Has(v))
            {
                Console.WriteLine($"{v}: she has none");
                continue;
            }
            Console.WriteLine($"{v}: in through the side / the deck, % of the shells at them (b: broken on the way, of all)");
            var head = new StringBuilder("   km ");
            foreach (double a in ImmuneZone.Angles)
                head.Append(Inv, $"{a,9:0}°");
            Console.WriteLine(head);
            for (int r = 0; r < zone.RangesM.Length; r++)
            {
                var line = new StringBuilder(string.Format(Inv, "{0,5:0} ", zone.RangesM[r] / 1000));
                for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                {
                    var c = zone[v, r, a];
                    line.Append(c.Shots == 0 ? "        - " : string.Format(Inv, "{0,4:0}/{1,-4:0}{2}", 100 * c.SideShare, 100 * c.DeckShare,
                        c.SideShare >= ImmuneZone.Edge || c.DeckShare >= ImmuneZone.Edge ? ' ' : '*'));
                }
                Console.WriteLine(line);
            }
            Console.WriteLine("   (* under half both ways)");
            Console.WriteLine("  angle  immune            through the side most by          through the deck most by");
            for (int a = 0; a < ImmuneZone.Angles.Length; a++)
            {
                var (inner, outer) = zone.Edges(v, a);
                string z = inner < outer ? string.Format(Inv, "{0,5:0.0}-{1,-5:0.0} km", inner / 1000, outer / 1000) : "none          ";
                int ri = Math.Clamp((int)Math.Round(inner / ImmuneZone.RangeStepM) - 1, 0, zone.RangesM.Length - 1);
                int ro = Math.Clamp((int)Math.Round(outer / ImmuneZone.RangeStepM) - 1, 0, zone.RangesM.Length - 1);
                Console.WriteLine($"  {ImmuneZone.Angles[a],4:0}°  {z}    {Name(zone.Most(v, ri, a, false)),-30}  {Name(zone.Most(v, ro, a, true))}");
            }
        }
        return 0;
    }

    /// <summary>Shells fired in anger: the sweep by range band, its cost, and each band's ships and hits.</summary>
    static int Anger(ShipClass cls, (string Name, string Kind, ShellTypeRow Shell) pick, double cal, double maxElev, SimSettings settings, Options o)
    {
        int threads = o.Threads > 0 ? o.Threads : Environment.ProcessorCount;
        var sw = Stopwatch.StartNew();
        var run = new Sim.AngerRun(cls, pick.Shell, maxElev, settings, (ulong)o.Seed, o.PerShip, o.SettleMin);
        Console.WriteLine($"{cls.Name}: shells fired in anger, her own {cal:0} mm ({pick.Name}); {run.Bands.Length} bands of " +
                          $"{Sim.AngerRun.BandM / 1000:0} km to {run.ReachM / 1000:0.0} km, {o.PerShip} hits a ship, settled {o.SettleMin:0} min; set up in {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        run.Fill(o.Anger, threads, progress: k =>
        {
            if (k == 1 || k % 4 == 0 || k == o.Anger)
                Console.WriteLine($"  {k} ships a band: {sw.ElapsedMilliseconds} ms");
        });
        long ships = run.Bands.Sum(b => (long)b.Report.Ships);
        Console.WriteLine($"  {ships} ships in all on {threads} threads, {sw.Elapsed.TotalMilliseconds / ships * threads:0} ms a ship-thread");
        Console.WriteLine();
        Console.WriteLine("    km  ships  lost (mag/fndr/caps/broke)  guns out  <½ speed   per 100 hits: fires  mags  killed  holes m²   in/thru/dud/armour %");
        foreach (var b in run.Bands)
        {
            var r = b.Report;
            double per = 100.0 / Math.Max(r.Hits, 1);
            int lost = r.Lost;
            string by = $"{r.LostBy[(int)LossKind.Magazine]}/{r.LostBy[(int)LossKind.Foundered]}/{r.LostBy[(int)LossKind.Capsized]}/{r.LostBy[(int)LossKind.BrokeInTwo]}";
            var cls4 = new int[4];
            foreach (var d in b.Dots)
                cls4[d.Class]++;
            string classes = string.Join("/", cls4.Select(x => (100.0 * x / Math.Max(b.Dots.Count, 1)).ToString("0")));
            Console.WriteLine($"  {b.FromM / 1000,2:0}-{b.ToM / 1000,-4:0.#} {r.Ships,4}  {lost,4} ({by,-11})    {r.MainSilenced,4}     {r.UnderHalf,4}      " +
                              $"{r.Fires * per,16:0}  {r.Magazines * per,4:0.0}  {r.Killed * per,6:0}  {r.BreachM2 * per,8:0.0}   {classes}");
        }
        return 0;
    }

    static string Name(ZonePlate? p) => p is { } z ? string.Format(Inv, "{0}[{1}] {2:0} mm", z.Kind, z.Index, z.Mm) : "-";
}
