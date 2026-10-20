using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen magazine</c>: the magazines alone (docs/battle/magazines.md V0): each design's magazines (what they
/// hold, where they lie against the waterline, the mounts they feed) and each mount's ammunition train, the stations a
/// flash runs along from the gunhouse down to the magazine. With <c>--runs N</c> (V2), each battery's first mount has
/// charges lit at each station of its train N times through the sim's own <see cref="Magazines.Light"/>: how often the
/// flame reaches the magazine, and how many stations burn. With <c>--race</c> (V3), each magazine is set off on a fresh
/// ship of the class, its rounds all aboard (a share of it flooded with <c>--flooded</c>), through the sim's own
/// <see cref="Magazines.SetOff"/>: the tier, the peak pressure, the propellant burned, what gave and when.</summary>
static class MagazineBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IReadOnlyList<string> designs, Func<string, Design> load, string settingsPath, int runs = 0, bool race = false,
        double flooded = 0, string? packaging = null, bool trace = false)
    {
        if (trace)
            Magazines.Trace = Console.WriteLine;
        var settings = SimSettings.Load(settingsPath);
        foreach (var name in designs.Count > 0 ? designs : ["iron_duke"])
        {
            var d = load(name);
            if (packaging != null)   // every gun block packed so: what the race does with bags alone, cases, tanks
                d = d with { GunTech = d.GunTech?.ToDictionary(kv => kv.Key, kv => kv.Value with { Packaging = packaging }) };
            var c = ShipClassCompiler.Compile(ShipDesign.Build(d));
            List(c, settings);
            if (runs > 0)
                Light(c, settings, runs);
            if (race)
                Race(c, settings, flooded);
        }
        return 0;
    }

    /// <summary>Each battery's first mount: charges lit at each station of its train, RUNS times, on one ship of the class
    /// (what a flash leaves behind doesn't change the doors' rolls).</summary>
    static void Light(ShipClass c, SimSettings s, int runs)
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, s);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 50, -20, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        var rng = RandomStream.Named(1, "magazine bench");
        Console.WriteLine(string.Format(Inv, "  charges lit at each station, {0} times (doors open {1}):", runs,
            string.Join(", ", s.StationDoorOpen.Select(v => v.ToString("0.##", Inv)))));
        var seen = new HashSet<int>();
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            var train = c.Train(m);
            if (train.Length == 0 || !seen.Add(c.Mounts[m].Battery))
                continue;
            var parts = new List<string>();
            for (int st = 0; st < train.Length - 1; st++)
            {
                int reached = 0, lit = 0;
                for (int i = 0; i < runs; i++)
                {
                    var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
                    reached += Magazines.Light(c, k, m, st, burning: false, s, ref rng, ref res) >= 0 ? 1 : 0;
                    lit += res.Stations;
                }
                parts.Add(string.Format(Inv, "from the {0}: magazine {1:0.#}%, {2:0.00} stations", Station(train[st].Kind), 100.0 * reached / runs,
                    (double)lit / runs));
            }
            Console.WriteLine($"    {c.ComponentId(c.Mounts[m].Component)}: {(parts.Count == 0 ? "the magazine only" : string.Join("; ", parts))}");
        }
    }

    static void List(ShipClass c, SimSettings s)
    {
        double wl = c.Vertical.Waterline;
        Console.WriteLine($"{c.Name}:");
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            ref readonly var rr = ref c.Rooms[room];
            if (rr.Kind != RoomKind.Magazine)
                continue;
            var fed = Enumerable.Range(0, c.Mounts.Length).Where(m => c.Mounts[m].Magazine == room).Select(m => c.ComponentId(c.Mounts[m].Component)).ToList();
            Console.WriteLine(string.Format(Inv, "  {0}: {1:0.#} t in {2:0} m³, x {3:0.0} to {4:0.0} m, {5:+0.0;-0.0} to {6:+0.0;-0.0} m on the waterline; feeds {7}",
                c.RoomId(room), rr.Tonnes, rr.VolumeM3, rr.X0, rr.X1, rr.Base - wl, rr.Top - wl, fed.Count == 0 ? "nothing" : string.Join(", ", fed)));
        }
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            var train = c.Train(m);
            if (train.Length == 0)
                continue;
            ref readonly var mr = ref c.Mounts[m];
            string housing = mr.Casemate ? "casemate" : c.TurretTypes[mr.Type].Shape switch
            {
                TurretShape.Bb => "barbette",
                TurretShape.Dp => "gunhouse",
                _ => "shield",
            };
            var stations = new List<string>();
            foreach (var st in train)
                stations.Add($"{Station(st.Kind)} ({(st.Part >= 0 ? c.ComponentId(st.Part) : c.RoomId(st.Room))})");
            Console.WriteLine(string.Format(Inv, "  {0} ({1}, {2:0} mm, {3}): {4}", c.ComponentId(mr.Component), mr.Kind, mr.CalibreMm, housing,
                string.Join(" > ", stations)));
        }
    }

    /// <summary>Each magazine set off on a fresh ship of the class (its rounds all aboard, a share flooded).</summary>
    static void Race(ShipClass c, SimSettings s, double flooded)
    {
        Console.WriteLine(string.Format(Inv, "  each magazine set off{0}:", flooded > 0 ? $", {flooded:0%} flooded" : ""));
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            if (c.Rooms[room].Kind != RoomKind.Magazine)
                continue;
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, s);
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 50, -20, 0, 0));
            int slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
            var k = w.Ships.Store.Combat.Condition(slot);
            if (flooded > 0)
                foreach (int cell in c.Links(c.Rooms[room].Cells))
                    k.WaterM3[cell] = flooded * c.Cells[cell].VolumeM3 * Math.Clamp(c.Cells[cell].Permeability, 0.05, 1);
            var rng = RandomStream.Named(1, "magazine race");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = Magazines.SetOff(c, k, room, w.Ships.Store.Combat.Mounts(slot), s, 0, ref rng);
            double ms = sw.Elapsed.TotalMilliseconds;
            var mr = Enumerable.Range(0, c.Mounts.Length).FirstOrDefault(m => c.Mounts[m].Magazine == room);
            var ch = c.Charges[c.Mounts[mr].Type];
            Console.WriteLine(string.Format(Inv, "    {0}: {1} — {2:0} charges of {3:0} kg ({4}), peak {5:0} kPa, burned {6:0.0} t ({7:0.0} t by the girder's failure + 1 s), " +
                "girder lost {8:0.00}, gunhouses vented {9} (lifted {10}){11}{12}, magazines {13}; vents {17:0.0} m² at the end, gave: {18}; " +
                "killed {14}, wounded {15} ({16:0.0} ms)", c.RoomId(room), r.Tier,
                r.Charges, ch.MassKg, ch.Packaging, r.PeakKPa, r.BurnedKg / 1000, r.FastKg / 1000, r.D, r.Vented, r.Lifted,
                double.IsNaN(r.VentS) ? "" : string.Format(Inv, " at {0:0.00} s", r.VentS),
                double.IsNaN(r.StructureS) ? "" : string.Format(Inv, ", the girder at {0:0.00} s", r.StructureS), r.Magazines, r.Killed, r.Wounded, ms,
                r.VentM2, r.Gave == 0 ? "nothing" : string.Join(", ", new[] { "bulkhead", "deck", "sides", "bottom" }.Where((_, i) => (r.Gave & (1 << i)) != 0))));
        }
    }

    static string Station(StationKind k) => k switch
    {
        StationKind.Gunhouse => "gunhouse",
        StationKind.Chamber => "working chamber",
        StationKind.Casemate => "casemate",
        StationKind.Handling => "handling room",
        _ => "magazine",
    };
}
