using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen torpedo` (docs/battle/fighting.md 4.6, "The instruments"), two benches:
/// <list type="bullet">
/// <item><b>the hit alone</b> (without --range): n torpedo hits at the set depth on the target's side (along it, or at
/// a place), K a ship, each through the world's damage system (<see cref="GivenHits.Torpedoes"/>), then MIN minutes of
/// flooding and fire: the outcomes, the spaces struck, the hole and the crew a hit, the parts most often put out of
/// action, and each ship after: water, list, speed, lost and how;</item>
/// <item><b>the attack</b> (--range M): the shooter at a range on a bearing off the target's bow, the target steaming on,
/// a spread of n fired through the torpedo system once its navy has the target in sight, run after run: hits, and how
/// long the torpedoes ran.</item>
/// </list></summary>
static class TorpedoBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(string? Shooter, double AtShare, double DepthM, int N, int PerShip, int Seed, double SettleMin, double RangeM, double AspectDeg,
        double TargetKn, int Spread, int Runs);

    public static int Run(string target, Func<string, Design> load, string settingsPath, Options o)
    {
        var settings = SimSettings.Load(settingsPath);
        var tc = ShipClassCompiler.Compile(ShipDesign.Build(load(target)));
        var sc = o.Shooter == null || o.Shooter == target ? tc : ShipClassCompiler.Compile(ShipDesign.Build(load(o.Shooter)));
        if (sc.Launchers.Length == 0)
            throw new ArgumentException($"{sc.Name} has no torpedoes: give --shooter a design that has");
        var torpedo = sc.Torpedoes[sc.Launchers[0].Torpedo];
        Console.WriteLine(string.Format(Inv, "{0}'s torpedo: {1:0} mm, {2:0} kg charge; {3:0} kn for {4:0} m or {5:0} kn for {6:0} m; {7} launcher(s), {8} torpedoes",
            sc.Name, torpedo.CalibreMm, torpedo.ChargeKg, torpedo.FastKn, torpedo.FastRangeM, torpedo.SlowKn, torpedo.SlowRangeM, sc.Launchers.Length,
            sc.Launchers.ToArray().Sum(l => l.Torpedoes)));
        return o.RangeM > 0 ? Attack(sc, tc, settings, o) : Given(tc, torpedo, settings, o);
    }

    static int Given(ShipClass tc, TorpedoRow torpedo, SimSettings settings, Options o)
    {
        double depth = o.DepthM > 0 ? o.DepthM : Math.Clamp(settings.TorpedoDepthShare * tc.Vertical.Draught, settings.TorpedoDepthMinM, settings.TorpedoDepthMaxM);
        var sw = Stopwatch.StartNew();
        var r = GivenTorpedoes(tc, torpedo, depth, o, settings);
        double secs = sw.Elapsed.TotalSeconds;
        int k = Math.Max(o.PerShip, 1);
        double reach = settings.TorpedoReachK * Math.Cbrt(torpedo.ChargeKg), hole = settings.TorpedoHoleK * Math.Pow(torpedo.ChargeKg, 2.0 / 3);
        Console.WriteLine(string.Format(Inv, "{0} ({1:0} t, {2:0} m, depth {3:0.0} m, draught {4:0.0} m): {5} hits {6:0.0} m down {7}, {8} a ship; reach {9:0.0} m, " +
            "hole {10:0} m²; {11:0.00} s", tc.Name, tc.Hydrostatics.DisplacementT, tc.Length, tc.Vertical.Depth, tc.Vertical.Draught, r.Hits, depth,
            double.IsNaN(o.AtShare) ? "along the side" : $"at {o.AtShare:0.##} of the length from the stern", k, reach, hole, secs));
        Console.WriteLine("  outcomes: " + string.Join(", ", Enum.GetValues<Outcome>().Where(x => r.Outcomes[(int)x] > 0)
            .Select(x => string.Format(Inv, "{0} {1:0.#}%", x, 100 * r.Share(x)))));
        Console.WriteLine("  the space struck: " + string.Join(", ", r.Struck.OrderByDescending(kv => kv.Value)
            .Select(kv => string.Format(Inv, "{0} {1:0}%", kv.Key, 100.0 * kv.Value / r.Hits))));
        Console.WriteLine(string.Format(Inv, "  a hit: holes {0:0} m², crew {1:0.0}, magazine {2:0.#}%", r.BreachM2 / r.Hits, r.CrewLost / r.Hits, 100.0 * r.Magazines / r.Hits));
        Console.WriteLine("  put out of action (per 100 hits):");
        foreach (var (node, count) in r.PartsOut.Select((c, i) => (i, c)).Where(x => x.c > 0).OrderByDescending(x => x.c).Take(10))
            Console.WriteLine(string.Format(Inv, "    {0,6:0.0}  {1}", 100.0 * count / r.Hits, Node(tc, node)));
        Console.WriteLine(string.Format(Inv, "  each ship after {0} hit(s) and {1:0} min: water {2:0} t, list {3:0.0}°, power {4:0}% (speed {5:0}%), steering lost {6:0}%; " +
            "lost {7:0}% (broke in two {8:0}%, magazine {9:0}%, foundered {10:0}%, capsized {11:0}%)", k, o.SettleMin, r.WaterT / r.Ships, r.ListDeg / r.Ships,
            100 * r.PowerCap / r.Ships, 100 * r.SpeedShare / r.Ships, 100.0 * r.SteeringLost / r.Ships, 100.0 * r.Lost / r.Ships,
            100.0 * r.LostBy[(int)LossKind.BrokeInTwo] / r.Ships, 100.0 * r.LostBy[(int)LossKind.Magazine] / r.Ships,
            100.0 * r.LostBy[(int)LossKind.Foundered] / r.Ships, 100.0 * r.LostBy[(int)LossKind.Capsized] / r.Ships));
        return 0;
    }

    static GivenHits.Report GivenTorpedoes(ShipClass tc, TorpedoRow torpedo, double depth, Options o, SimSettings settings) =>
        GivenHits.Torpedoes(tc, torpedo, depth, o.AtShare, o.N, Math.Max(o.PerShip, 1), (ulong)o.Seed, settings, o.SettleMin * 60);

    static int Attack(ShipClass sc, ShipClass tc, SimSettings settings, Options o)
    {
        Console.WriteLine(string.Format(Inv, "{0} fires {1} at {2} ({3:0} kn) from {4:0} m, {5:0}° off its bow; {6} runs", sc.Name, o.Spread, tc.Name, o.TargetKn,
            o.RangeM, o.AspectDeg, o.Runs));
        int fired = 0, hits = 0, anyHit = 0, sunk = 0;
        var runs = new List<double>();
        var sw = Stopwatch.StartNew();
        for (int k = 0; k < o.Runs; k++)
        {
            var start = WorldTime.FromUtc(1916, 5, 31, 14);
            var w = new World(start, (ulong)(o.Seed + k), settings);
            w.Apply(Command.SetWeather(25_000));
            int a = w.Ships.AddClass(sc), b = w.Ships.AddClass(tc);
            const double lat = 56.0, lon = 4.0;
            var p = Earth.Position(lat, lon);
            var f = Earth.Forward(p, o.AspectDeg);
            Earth.Advance(ref p, ref f, o.RangeM);
            var (sl, so) = Earth.LatLon(p);
            // the shooter beam-on to the target, so its tubes bear
            double back = Earth.Bearing(p, Earth.Position(lat, lon));
            w.Apply(ShipSystem.Spawn(b, lat, lon, 0, o.TargetKn, side: 1));
            w.Apply(ShipSystem.Spawn(a, sl, so, (back + 90) % 360, 0, side: 0));
            int shooter = w.Ships.NewestEntity, target = shooter - 1;
            // once the navy has it in sight, the spread
            var t = start;
            while (!InSight(w, 0, target) && t < start + 60_000)
                w.AdvanceTo(t += 1000);
            w.Apply(TorpedoSystem.Launch(shooter, target, o.Spread));
            w.AdvanceTo(t + 15 * 60_000);
            int h = w.Torpedoes.Hits;
            fired += (int)w.Torpedoes.Fired;
            hits += h;
            anyHit += h > 0 ? 1 : 0;
            int ts = w.Ships.Store.SlotOf(target);
            sunk += ts < 0 || w.Ships.Store.Combat.Condition(ts).Status != ShipStatus.Afloat ? 1 : 0;
            runs.Add((t - start) / 1000.0);
        }
        Console.WriteLine(string.Format(Inv, "  {0} torpedoes fired, {1} hits ({2:0.0}%); a hit in {3} of {4} runs, the target lost in {5}; {6:0.00} s",
            fired, hits, 100.0 * hits / Math.Max(fired, 1), anyHit, o.Runs, sunk, sw.Elapsed.TotalSeconds));
        return 0;
    }

    static bool InSight(World w, int side, int entity) => w.Sightings.Of(side) is { } pic && pic.IndexOf(entity) is int i and >= 0 && pic[i].InSight;

    static string Node(ShipClass c, int node) => node < 0 ? "-" : node < c.Components.Length
        ? $"{c.ComponentId(node)} ({c.Components[node].Kind.ToString().ToLowerInvariant()})"
        : $"{c.RoomId(node - c.Components.Length)} ({c.Rooms[node - c.Components.Length].Kind})";
}
