using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen hits`: the damage model alone (docs/battle/fighting.md 4.3a, "The instruments"; gunfire_ref.py
/// <c>mc()</c>): n hits of one shell at a range on a design, spread over it as the falling shells see it, through the
/// world's damage system (<see cref="GivenHits"/>). It prints where they struck, the outcome classes, fires, crew and
/// holes per hit, the parts most often put out of action, and with --per-ship K the ships' state after K hits each.</summary>
static class HitsBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(string? Shooter, int Battery, string? Kind, double RangeM, double AspectDeg, int N, int PerShip, int Seed, int Trace, double SettleMin,
        string? Loadout = null);

    public static int Run(string target, Func<string, Design> load, string settingsPath, Options o)
    {
        var settings = SimSettings.Load(settingsPath);
        var tc = ShipClassCompiler.Compile(ShipDesign.Build(load(target)));
        var sc = o.Shooter == null || o.Shooter == target ? tc : ShipClassCompiler.Compile(ShipDesign.Build(load(o.Shooter)));
        if (o.Battery < 0 || o.Battery >= sc.Batteries.Length)
            throw new ArgumentException($"{sc.Name} has no battery {o.Battery}");
        var b = sc.Batteries[o.Battery];
        // the shooter's loadout: her design's, or one given (docs/battle/shells-armour-damage.md S5)
        var lo = sc.Loadout;
        if (o.Loadout != null)
        {
            var errors = new List<string>();
            lo = sc.Load(LoadoutSpec.Parse(o.Loadout), errors) ?? throw new ArgumentException($"--loadout: {string.Join("; ", errors)}");
        }
        int type = lo.Fires(o.Battery);
        if (o.Kind != null)
        {
            var kind = o.Kind.ToLowerInvariant() switch
            {
                "ap" => ShellKind.Ap,
                "common" or "sap" => ShellKind.Common,
                "he" => ShellKind.He,
                _ => throw new ArgumentException($"no shell kind {o.Kind} (ap, common, he)"),
            };
            type = -1;
            foreach (var l in lo.Loads(o.Battery))
                if (lo.ShellTypes[l.ShellType].Kind == kind)
                    type = l.ShellType;
            if (type < 0)
                throw new ArgumentException($"{sc.Name}'s battery {o.Battery} carries no {o.Kind}");
        }
        var shell = lo.ShellTypes[type];
        double maxElev = sc.Mounts[sc.Links(b.Mounts)[0]].MaxElevationDeg;
        int traced = 0;
        var sw = Stopwatch.StartNew();
        var r = GivenHits.Run(tc, shell, maxElev, o.RangeM, o.AspectDeg, o.N, Math.Max(o.PerShip, 1), (ulong)o.Seed, settings, (i, path, t, e) =>
        {
            if (traced >= o.Trace)
                return;
            traced++;
            Console.WriteLine(string.Format(Inv, "hit {0}: {1}, burst {2} at {3:0.00} m (force {4:0.00}), stopped at {5:0.00} m; part {6}; {7} out, {8} damaged; " +
                "crew {9:0.0}, holes {10:0.000} m²{11}{12}{13}", i, t.Outcome, t.Burst, t.BurstT, t.Force, t.StopT, Node(tc, e.Part), e.Destroyed + e.Disabled,
                e.Damaged, e.CrewLost, e.BreachM2, e.Fire ? ", fire" : "", e.Jammed ? ", jammed" : "", e.Magazine ? ", MAGAZINE" : ""));
            Console.Write(path.Text(tc));
        }, o.SettleMin * 60, FallOut(settingsPath));
        double secs = sw.Elapsed.TotalSeconds;

        Console.WriteLine(string.Format(Inv, "{0} on {1}: {2} ({3:0.#} kg, filler {4:0.##} kg, {5} {6}) at {7:0} m ({8:0.0} kyd) from {9:0}° off the bow",
            sc.Name, tc.Name, lo.ShellTypeId(type), shell.MassKg, shell.FillerKg, shell.Fuze, shell.Fuze == Fuze.Base ? $"{shell.FuzeDelayS:0.###} s" : "",
            o.RangeM, o.RangeM / 914.4, o.AspectDeg));
        Console.WriteLine(string.Format(Inv, "  falling at {0:0.0}°, striking at {1:0} m/s: {2} hits on {3} ship(s) in {4:0.00} s ({5:0} µs a hit)", r.FallDeg,
            r.StrikeMs, r.Hits, r.Ships, secs, secs * 1e6 / Math.Max(r.Hits, 1)));
        Console.WriteLine("  where they struck: " + string.Join(", ", r.Struck.OrderByDescending(kv => kv.Value)
            .Select(kv => string.Format(Inv, "{0} {1:0}%", kv.Key, 100.0 * kv.Value / r.Hits))));
        if (r.Underwater > 0)
            Console.WriteLine(string.Format(Inv, "  under water: {0:0.#}% of hits, after {1:0.0} m in the water at {2:0} m/s on average (reach {3:0.0} m)",
                100.0 * r.Underwater / r.Hits, r.UnderwaterRunM / r.Underwater, r.UnderwaterStrikeMs / r.Underwater, WaterRun.ReachM(shell, r.StrikeMs, settings)));
        Console.WriteLine("  outcomes: " + string.Join(", ", Enum.GetValues<Outcome>().Where(x => r.Outcomes[(int)x] > 0)
            .Select(x => string.Format(Inv, "{0} {1:0.#}%", x, 100 * r.Share(x)))));
        Console.WriteLine(string.Format(Inv, "  the shells on the plates: decapped {0:0.#}%, shattered {1:0.#}%, broken just over the limit {2:0.#}%; " +
            "on plate over 9\" {3:0.#}% of hits, of those through it and burst inside {4:0.#}% (1 in {5:0.#})", 100.0 * r.Decapped / r.Hits,
            100.0 * r.Shattered / r.Hits, 100.0 * r.BrokeNearLimit / r.Hits, 100.0 * r.Heavy / r.Hits, 100.0 * r.HeavyBurst / Math.Max(r.Heavy, 1),
            r.HeavyBurst > 0 ? (double)r.Heavy / r.HeavyBurst : double.PositiveInfinity));
        if (r.Spalled > 0)
            Console.WriteLine(string.Format(Inv, "  stopped in the spall band: {0:0.#}% of hits, severity {1:0.00} on average", 100.0 * r.Spalled / r.Hits,
                r.SpallS / r.Spalled));
        if (r.Glanced > 0 || r.Bent > 0)
            Console.WriteLine(string.Format(Inv, "  glanced off a plate {0:0.#}% of hits; turned by the plates they got through {1:0.#}%, by {2:0.0}° on average",
                100.0 * r.Glanced / r.Hits, 100.0 * r.Bent / r.Hits, r.TurnDeg / Math.Max(r.Bent, 1)));
        if (r.Delayed > 0)
            Console.WriteLine(string.Format(Inv, "  bursts after the delay: {0:0.#}% of hits, {1:0.0} m on average behind where the fuze started", 100.0 * r.Delayed / r.Hits,
                r.DelayedM / r.Delayed));
        Console.WriteLine(string.Format(Inv, "  a hit: fire {0:0}%, crew {1:0.0}, holes {2:0.000} m², jammed {3:0.#}%, magazine {4:0.##}%", 100.0 * r.Fires / r.Hits,
            r.CrewLost / r.Hits, r.BreachM2 / r.Hits, 100.0 * r.Jams / r.Hits, 100.0 * r.Magazines / r.Hits));
        if (r.Magazines > 0)
            Console.WriteLine("  magazines set off by: " + string.Join(", ", Enum.GetValues<MagazineVia>().Where(v => r.MagazinesBy[(int)v] > 0)
                .Select(v => string.Format(Inv, "{0} {1:0.##}%", Via(v), 100.0 * r.MagazinesBy[(int)v] / r.Hits))) + " of hits");
        Console.WriteLine("  put out of action (per 100 hits):");
        foreach (var (node, count) in r.PartsOut.Select((c, i) => (i, c)).Where(x => x.c > 0).OrderByDescending(x => x.c).Take(15))
            Console.WriteLine(string.Format(Inv, "    {0,6:0.0}  {1}", 100.0 * count / r.Hits, Node(tc, node)));
        int k = Math.Max(o.PerShip, 1);
        Console.WriteLine(string.Format(Inv, "  each ship after {0} hit(s): power {1:0}% (speed {2:0}%), main mounts {3:0.0} of {4:0}, directors {5:0.0} of {6:0}; " +
            "crippled {7:0}% (slowed under {8:0}% speed {9:0}%, steering {10:0}%, main battery silent {11:0}%, lost {12:0}%)", k, 100 * r.PowerCap / r.Ships,
            100 * r.SpeedShare / r.Ships, r.MainWorking / r.Ships, r.MainMounts / r.Ships, r.DirectorsWorking / r.Ships, r.Directors / r.Ships,
            100.0 * r.Crippled / r.Ships, 100 * GivenHits.SlowShare, 100.0 * r.Slowed / r.Ships, 100.0 * r.SteeringLost / r.Ships,
            100.0 * r.MainSilenced / r.Ships, 100.0 * r.Lost / r.Ships));
        Console.WriteLine(string.Format(Inv, "  then {0:0} min of flooding and fire: water {1:0} t a ship; lost to the magazine {2:0.#}%, foundered {3:0.#}%, capsized {4:0.#}%",
            o.SettleMin, r.WaterT / r.Ships, 100.0 * r.LostBy[(int)LossKind.Magazine] / r.Ships, 100.0 * r.LostBy[(int)LossKind.Foundered] / r.Ships,
            100.0 * r.LostBy[(int)LossKind.Capsized] / r.Ships));
        if (r.LostBy[(int)LossKind.Magazine] > 0)
            Console.WriteLine(string.Format(Inv, "    to the magazine: set off by a hit {0:0.#}%, by a fire later {1:0.#}%",
                100.0 * (r.LostBy[(int)LossKind.Magazine] - r.MagazineByFire) / r.Ships, 100.0 * r.MagazineByFire / r.Ships));
        Console.WriteLine(string.Format(Inv, "  out of the fight (lost, or past the default preset's fall-out) {0:0}%", 100.0 * r.Out / r.Ships));
        return 0;
    }

    /// <summary>The default doctrine preset's fall-out (Content/Sim/doctrine.json beside the settings): what "out of the
    /// fight" means to the benches.</summary>
    public static FallOutRule FallOut(string settingsPath)
    {
        var preset = DoctrinePresets.Load(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "doctrine.json"))[DoctrinePresets.DefaultName];
        double V(DoctrineField f) => preset.Any(p => p.Field == f) ? preset.First(p => p.Field == f).Value : Doctrine.Default(f);
        return new FallOutRule(V(DoctrineField.FallOutSpeedShare), V(DoctrineField.FallOutGunsShare), V(DoctrineField.FallOutListDeg), V(DoctrineField.FallOutWaterShare));
    }

    static string Via(MagazineVia v) => v switch
    {
        MagazineVia.In => "a burst in it",
        MagazineVia.Beside => "a burst beside it",
        MagazineVia.Turret => "the turret or barbette",
        MagazineVia.Underwater => "under water",
        _ => "-",
    };

    /// <summary>A node's name: a component's id, or a room's id and kind.</summary>
    static string Node(ShipClass c, int node) => node < 0 ? "-" : node < c.Components.Length
        ? $"{c.ComponentId(node)} ({c.Components[node].Kind.ToString().ToLowerInvariant()})"
        : $"{c.RoomId(node - c.Components.Length)} ({c.Rooms[node - c.Components.Length].Kind})";
}
