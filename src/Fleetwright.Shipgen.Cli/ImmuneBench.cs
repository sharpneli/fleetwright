using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
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
        int Anger = 0, int PerShip = 10, double SettleMin = 30, string? Json = null);

    /// <summary>The history checks (docs/designer-ui/immune-zone.md IZ1): bench-only designs with a real scheme, against
    /// the record's gun (her own), the record's immune zone beside ours on the beam: the classic edges (the main belt and
    /// deck beaten whole) and the vitals' own. Checks, never targets.</summary>
    static readonly (string Design, string Record, double InnerKm, double OuterKm)[] Anchors =
    [
        ("iowa_scheme", "USN, against 16\"/50 Mk 8 (armor/05 3.4, armor/00 9 row 12): 23.6-27.4 kyd", 23.6 * 0.9144, 27.4 * 0.9144),
        ("sodak_scheme", "research 08's own model, against 16\"/50 Mk 8 (damage/08 K)", 23.0, 26.5),
    ];

    public static int RunAnchors(string root, Func<string, Design> load, SimSettings settings, Options o)
    {
        Console.WriteLine("  design         record                                                             km           belt and deck held      vitals (magazines, machinery)");
        foreach (var (name, record, inner, outer) in Anchors)
        {
            var design = load(Path.Combine(root, "anchors", name + ".json"));
            var cls = ShipClassCompiler.Compile(ShipDesign.Build(design));
            var shell = Loadout.ShellsAt(design, true, 0, design.Main![0].CalibreMm!.Value).First(x => x.Kind == "ap").Shell;
            double elev = 0;
            foreach (ref readonly var m in cls.Mounts)
                elev = Math.Max(elev, m.MaxElevationDeg);
            var zone = new ImmuneZone(cls, shell, elev, settings, (ulong)o.Seed);
            zone.Fill(o.Shots, o.Threads > 0 ? o.Threads : Environment.ProcessorCount);
            int beam = Array.IndexOf(ImmuneZone.Angles, 90.0);
            string Z((double In, double Out) e) => e.In < e.Out ? $"{e.In / 1000:0.0}-{e.Out / 1000:0.0}" : "none";
            var classic = (zone.Edges(ZoneTarget.Belt, beam).InnerM, zone.Edges(ZoneTarget.Deck, beam).OuterM);
            Console.WriteLine($"  {name,-14} {record,-82} {inner:0.0}-{outer:0.0}    {Z(classic),-22}  " +
                              $"{Z(zone.Edges(ZoneTarget.Magazines, beam))}, {Z(zone.Edges(ZoneTarget.Machinery, beam))}");
        }
        return 0;
    }

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
        var targets = o.Vitals == null ? ImmuneZone.All : ImmuneZone.All.Where(t => t.ToString().StartsWith(o.Vitals, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (o.Trace is { } tr)
        {
            // one cell's shells, walked on one thread: target:km:angle
            var parts = tr.Split(':');
            var tt = ImmuneZone.All.First(t => t.ToString().StartsWith(parts[0], StringComparison.OrdinalIgnoreCase));
            int cellAt = zone.Index(tt, (int)Math.Round(double.Parse(parts[1]) * 1000 / ImmuneZone.RangeStepM) - 1, Array.IndexOf(ImmuneZone.Angles, double.Parse(parts[2])));
            int shown = 0;
            zone.Each = (i, path, t, tin) =>
            {
                if (i != cellAt || shown++ >= 6)
                    return;
                Console.WriteLine($"shell: {t.Outcome}, stop {t.StopT:0.00}, burst {t.BurstT:0.00}, plate {t.Plate}, glance {t.Glance}, target at {tin:0.00}, strike {t.StrikeMs:0}");
                foreach (var x in path.Crossings)
                    Console.WriteLine($"   {x.T,7:0.00} {x.Kind,-8} {x.Plate,-14} [{x.Index}] {x.Mm,6:0} mm  n=({x.Normal.X:0.00},{x.Normal.Y:0.00},{x.Normal.Z:0.00}) " +
                                      (x.Kind is CrossingKind.CellIn && x.Index >= 0 ? $"{cls.Cells[x.Index].Room} {(cls.Cells[x.Index].Room >= 0 ? cls.Rooms[cls.Cells[x.Index].Room].Kind : default)}" : ""));
            };
            zone.Fill(8, [tt], 1);
            return 0;
        }
        Console.WriteLine($"{cls.Name}: against her own {(o.Main ? "main" : "secondary")} battery at {cal:0} mm ({pick.Name}, {pick.Shell.MassKg:0} kg, " +
                               $"{pick.Shell.MuzzleVelocityMs:0} m/s), guns to {maxElev:0}°: reach {zone.ReachM / 1000:0.0} km; compiled in {compileMs:0} ms");
        int threads = o.Threads > 0 ? o.Threads : Environment.ProcessorCount;
        int[] passes = o.Passes ? [.. new[] { 16, 64, o.Shots }.Where(x => x <= o.Shots).Distinct()] : [o.Shots];
        foreach (int p in passes)
        {
            sw.Restart();
            zone.Fill(p, targets, threads);
            long drawn = 0, shots = 0, traced = 0, hidden = 0;
            foreach (var t in targets)
                for (int r = 0; r < zone.RangesM.Length; r++)
                    for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                        (drawn, shots, traced, hidden) = (drawn + zone[t, r, a].Drawn, shots + zone[t, r, a].Shots, traced + zone[t, r, a].Traced,
                            hidden + (zone[t, r, a].Hidden ? 1 : 0));
            Console.WriteLine($"  filled to {p} a cell on {threads} threads: {sw.Elapsed.TotalMilliseconds:0} ms in all " +
                                   $"({shots} shells walked, {drawn} lines drawn, {traced} traced; {hidden} cells hidden)");
        }
        // one thread's cost a shell, for the designer's budget: the armour, the zone's own view
        if (o.Threads != 1)
        {
            var one = new ImmuneZone(cls, pick.Shell, maxElev, settings, (ulong)o.Seed);
            sw.Restart();
            one.Fill(16, ImmuneZone.Armour, 1);
            long shots = 0;
            foreach (var t in ImmuneZone.Armour)
                for (int r = 0; r < one.RangesM.Length; r++)
                    for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                        shots += one[t, r, a].Shots;
            Console.WriteLine($"  one thread, the armour at 16 a cell: {sw.Elapsed.TotalMilliseconds:0} ms, {1000 * sw.Elapsed.TotalMilliseconds / Math.Max(shots, 1):0} µs a shell");
        }

        if (o.Json != null)
        {
            // for the mockup (docs/designer-ui/immune.html): every cell, the edges, the plates by name
            var all = new JsonArray();
            foreach (var t in ImmuneZone.All)
            {
                JsonArray cells = [], edges = [], sideBy = [], deckBy = [];
                for (int r = 0; r < zone.RangesM.Length; r++)
                {
                    JsonArray row = [], sb = [], db = [];
                    for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                    {
                        var c = zone[t, r, a];
                        row.Add((JsonNode?)Arr(c.Shots, c.Through, c.Side, c.Deck, c.Broken, c.Unseen));
                        sb.Add((JsonNode?)PlateName(cls, zone.Most(t, r, a, false)));
                        db.Add((JsonNode?)PlateName(cls, zone.Most(t, r, a, true)));
                    }
                    cells.Add((JsonNode?)row);
                    sideBy.Add((JsonNode?)sb);
                    deckBy.Add((JsonNode?)db);
                }
                for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                {
                    var e = zone.Edges(t, a);
                    edges.Add((JsonNode?)Arr(Math.Round(e.InnerM), Math.Round(e.OuterM)));
                }
                all.Add((JsonNode?)new JsonObject { ["target"] = t.ToString(), ["has"] = zone.Has(t), ["cells"] = cells, ["edges"] = edges, ["sideBy"] = sideBy, ["deckBy"] = deckBy });
            }
            var armour = new JsonArray();
            for (int a = 0; a < ImmuneZone.Angles.Length; a++)
            {
                var e = zone.ArmourEdges(a);
                armour.Add((JsonNode?)Arr(Math.Round(e.InnerM), Math.Round(e.OuterM)));
            }
            var falls = new JsonArray();
            foreach (var f in zone.Falls)
                falls.Add((JsonNode?)Arr(Math.Round(f.FallDeg, 2), Math.Round(f.StrikeMs)));
            var doc = new JsonObject
            {
                ["ship"] = cls.Name, ["calibre"] = cal, ["shell"] = pick.Name, ["massKg"] = Math.Round(pick.Shell.MassKg), ["mv"] = Math.Round(pick.Shell.MuzzleVelocityMs),
                ["reachM"] = Math.Round(zone.ReachM), ["angles"] = Arr(ImmuneZone.Angles), ["ranges"] = Arr(zone.RangesM), ["falls"] = falls, ["targets"] = all,
                ["armour"] = armour,
            };
            File.WriteAllText(o.Json, doc.ToJsonString());
            Console.WriteLine($"  written {o.Json}");
        }
        int beam = Array.IndexOf(ImmuneZone.Angles, 90.0);
        foreach (var t in targets)
        {
            Console.WriteLine();
            if (!zone.Has(t))
            {
                Console.WriteLine($"{t}: she has none");
                continue;
            }
            bool vitals = t >= ZoneTarget.Magazines;
            Console.WriteLine(vitals ? $"{t}: in through the side / the deck / b in pieces, % of the shells at them" : $"{t}: through it whole, % of the shells at it");
            var head = new StringBuilder("   km ");
            foreach (double a in ImmuneZone.Angles)
                head.Append((vitals ? string.Format(Inv, "{0,13:0}", a) : string.Format(Inv, "{0,6:0}", a)) + "°");
            Console.WriteLine(head);
            for (int r = 0; r < zone.RangesM.Length; r++)
            {
                var line = new StringBuilder(string.Format(Inv, "{0,5:0} ", zone.RangesM[r] / 1000));
                for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                {
                    var c = zone[t, r, a];
                    line.Append(c.Shots == 0 ? (vitals ? "            - " : "      -")
                        : vitals ? string.Format(Inv, "{0,4:0}/{1,-3:0}b{2,-3:0}{3}", 100 * c.SideShare, 100 * c.DeckShare, 100 * c.BrokenShare,
                            c.SideShare >= ImmuneZone.Edge || c.DeckShare >= ImmuneZone.Edge ? ' ' : '*')
                        : string.Format(Inv, "{0,6:0} ", 100 * c.ThroughShare));
                }
                Console.WriteLine(line);
            }
            Console.WriteLine("  angle  edges           most by");
            for (int a = 0; a < ImmuneZone.Angles.Length; a++)
            {
                var (inner, outer) = zone.Edges(t, a);
                string z = vitals ? (inner < outer ? string.Format(Inv, "{0,5:0.0}-{1,-5:0.0} km", inner / 1000, outer / 1000) : "none          ")
                    : t == ZoneTarget.Deck ? string.Format(Inv, "gives from {0,4:0.0} km", outer / 1000) : string.Format(Inv, "holds from {0,4:0.0} km", inner / 1000);
                int ri = Math.Clamp((int)Math.Round((t == ZoneTarget.Deck ? outer : inner) / ImmuneZone.RangeStepM) - 1, 0, zone.RangesM.Length - 1);
                Console.WriteLine($"  {ImmuneZone.Angles[a],4:0}°  {z,-16}  {PlateName(cls, zone.Most(t, ri, a))}");
            }
        }
        if (targets.Length == ImmuneZone.All.Length)
        {
            var (i0, o0) = zone.ArmourEdges(beam);
            Console.WriteLine();
            Console.WriteLine(i0 < o0 ? $"The armour's zone on the beam: {i0 / 1000:0.0}-{o0 / 1000:0.0} km" : "No armour zone on the beam");
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
        if (o.Json != null)
        {
            var bands = new JsonArray();
            foreach (var b in run.Bands)
            {
                var r = b.Report;
                var struck = new JsonObject();
                foreach (var (k, n) in r.Struck)
                    struck[k] = n;
                var dots = new JsonArray();
                foreach (var d in b.Dots)
                    dots.Add((JsonNode?)(Arr(Math.Round(d.Point.X, 2), Math.Round(d.Point.Y, 2), Math.Round(d.Point.Z, 2), d.Class)));
                bands.Add((JsonNode)new JsonObject
                {
                    ["from"] = b.FromM, ["to"] = b.ToM, ["ships"] = r.Ships, ["hits"] = r.Hits, ["lost"] = r.Lost,
                    ["lostBy"] = Arr(r.LostBy[(int)LossKind.Magazine], r.LostBy[(int)LossKind.Foundered], r.LostBy[(int)LossKind.Capsized], r.LostBy[(int)LossKind.BrokeInTwo]),
                    ["gunsOut"] = r.MainSilenced, ["underHalf"] = r.UnderHalf, ["fires"] = r.Fires, ["mags"] = r.Magazines, ["killed"] = Math.Round(r.Killed),
                    ["wounded"] = Math.Round(r.Wounded), ["holesM2"] = Math.Round(r.BreachM2, 1), ["worstWaterT"] = Math.Round(r.WorstWaterT),
                    ["worstListDeg"] = Math.Round(r.WorstListDeg, 1), ["struck"] = struck, ["dots"] = dots,
                    ["waterT"] = Math.Round(r.WaterT / Math.Max(r.Ships, 1)), ["speed"] = Math.Round(r.SpeedShare / Math.Max(r.Ships, 1), 3),
                    ["guns"] = Math.Round(r.MainWorking / Math.Max(r.MainMounts, 1), 3), ["crewOut"] = Math.Round(r.CrewAfter / Math.Max(r.Ships, 1)),
                });
            }
            var doc = new JsonObject
            {
                ["ship"] = cls.Name, ["calibre"] = cal, ["hitsPerShip"] = o.PerShip, ["settleMin"] = o.SettleMin, ["lengthM"] = cls.Length,
                ["waterline"] = cls.Vertical.Waterline, ["depth"] = cls.Vertical.Depth, ["draught"] = cls.Vertical.Draught, ["bands"] = bands,
            };
            File.WriteAllText(o.Json, doc.ToJsonString());
            Console.WriteLine($"  written {o.Json}");
        }
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

    /// <summary>A plate as the designer names it: the belt or its zone, an armour deck by its deck, a slope.</summary>
    static string PlateName(ShipClass c, ZonePlate? p) => p is not { } z ? "" : z.Kind switch
    {
        PlateKind.SideArmour => (c.SidePlates[z.Index].Kind switch { StrakeKind.Zone => "belt zone", var k => k.ToString().ToLowerInvariant() }) + $" {z.Mm:0} mm",
        PlateKind.ArmourDeck => $"{Decks.DeckNameLower(c.Decks[c.ArmourDecks[z.Index].Deck].Number)} {z.Mm:0} mm",
        PlateKind.Inclined => $"{c.Inclined[z.Index].Kind.ToString().ToLowerInvariant()} {z.Mm:0} mm",
        PlateKind.ArmourBulkhead => $"citadel bulkhead {z.Mm:0} mm",
        PlateKind.Barbette => $"barbette {z.Mm:0} mm",
        _ => $"{z.Kind.ToString().ToLowerInvariant()} {z.Mm:0} mm",
    };

    static JsonArray Arr(params double[] xs)
    {
        var a = new JsonArray();
        foreach (double x in xs)
            a.Add((JsonNode?)(x));
        return a;
    }

}
