using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Fire discipline (docs/battle/fire-control.md F6): the spots' bracket in correction space, and a battery that
/// ranges with partial salvos, each waiting for the last one's fall, before it fires for effect.</summary>
public class FireDisciplineTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { HitsDoDamage = false };
    const double Lat = 56.0, Lon = 4.0;

    [Fact]
    public void The_bracket_takes_spots_in_any_order()
    {
        // the RN's 400-200-100 is the bracket's halving: over at 0 steps down 366, a short there splits it
        var b = FireControl.Bracket.Open(366);
        Assert.Equal(0, b.Aim(0, 0));
        b.Spot(FireControl.Sign.Over, 0, 0, 0, 100, 92);
        Assert.Equal(-366, b.Aim(0, 0));
        b.Spot(FireControl.Sign.Short, -366, 0, 0, 100, 92);
        Assert.Equal(-183, b.Aim(0, 0));
        // an older salvo's spot that says what's already known changes nothing
        b.Spot(FireControl.Sign.Over, 0, 0, 0, 100, 92);
        Assert.Equal(-183, b.Aim(0, 0));
        b.Spot(FireControl.Sign.Short, -183, 0, 0, 100, 92);
        Assert.Equal(-91.5, b.Aim(0, 0));
        // never narrower than the floor: a short at the middle pushes the high bound out
        b.Spot(FireControl.Sign.Short, -91.5, 0, 0, 100, 92);
        Assert.Equal(92, b.HiAt(0, 0) - b.LoAt(0, 0), 6);
        // a spot against a bound drops it: open below the over, a step beyond
        b.Spot(FireControl.Sign.Over, -500, 0, 0, 100, 92);
        Assert.False(b.Closed);
        Assert.Equal(-866, b.Aim(0, 0));
        // a straddle sets it to the pattern round the salvo; then it widens at the drift
        b.Spot(FireControl.Sign.Straddle, -700, 0, 0, 200, 92);
        Assert.Equal((-800.0, -600.0), (b.LoAt(0, 0), b.HiAt(0, 0)));
        Assert.Equal((-830.0, -570.0), (b.LoAt(60_000, 0.5), b.HiAt(60_000, 0.5)));
        Assert.Equal(-700, b.Aim(60_000, 0.5));
    }

    [Fact]
    public void A_battery_ranges_with_partial_salvos_each_after_the_last_fall_then_fires_for_effect()
    {
        // Bismarck's starboard 150 mm (director, ~11 s reload) on a destroyer at 11 km: a flight of ~20 s, so firing at
        // the loading rate would put two salvos in the air before the first one's spot
        var bismarck = Fleet.Get("bismarck").Class!;
        int bat = -1;
        for (int k = 0; k < bismarck.Batteries.Length; k++)
            if (bismarck.Batteries[k].Side == Side.Starboard && bismarck.Batteries[k].CalibreMm > 120)
                bat = k;
        var reload = bismarck.Mounts[bismarck.Links(bismarck.Batteries[bat].Mounts)[0]].ReloadS;
        int ranging = 0, effect = 0, partial = 0;
        double shortestRanging = double.MaxValue, effectGaps = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = new World(Start, (ulong)seed, Settings);
            int a = w.Ships.AddClass(bismarck), d = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, 11_000);
            var (tl, to) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 15, side: 0));
            w.Apply(ShipSystem.Spawn(d, tl, to, 0, 15, side: 1));
            var salvos = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = salvos.Add;
            w.Apply(GunnerySystem.Engage(1, bat, 2));
            w.AdvanceTo(Start + 12 * 60_000);
            int guns = bismarck.Batteries[bat].Guns;
            for (int i = 0; i < salvos.Count; i++)
            {
                var s = salvos[i];
                if (s.Phase == FirePhase.Ranging)
                {
                    ranging++;
                    partial += s.Shells < guns ? 1 : 0;
                    Assert.True(s.Shells >= Math.Min(Settings.FireDisciplines[0].GunsMin, guns), $"a ranging salvo of {s.Shells}");
                    if (i > 0 && salvos[i - 1].Phase == FirePhase.Ranging)
                        shortestRanging = Math.Min(shortestRanging, (s.FiredMs - salvos[i - 1].FiredMs) / 1000.0);
                }
                else if (i > 0 && salvos[i - 1].Phase == FirePhase.Effect)
                {
                    effect++;
                    effectGaps += (s.FiredMs - salvos[i - 1].FiredMs) / 1000.0;
                }
            }
        }
        double effectGap = effectGaps / Math.Max(effect, 1);
        output.WriteLine($"{ranging} ranging salvos ({partial} partial), the closest {shortestRanging:F0} s apart; {effect} in fire for effect, " +
                         $"{effectGap:F1} s apart on average; the reload {reload:F1} s");
        Assert.True(ranging >= 6 && effect >= 30, "it ranged, then fired for effect");
        Assert.True(partial * 2 >= ranging, "ranging salvos are partial");
        // each ranging salvo waited for the last one's fall and spot: its flight and the spot's delay, well over a reload
        Assert.True(shortestRanging > reload + Settings.SpotDelayMs / 1000.0, $"ranging salvos {shortestRanging:F0} s apart");
        Assert.True(effectGap < 2 * reload, $"fire for effect at the loading rate: {effectGap:F1} s");
    }

    [Fact]
    public void A_mount_fires_at_the_reload_while_its_ready_rounds_last_then_as_the_hoists_bring_them()
    {
        // a Queen Elizabeth's turret, fired each time it's loaded: its ready-use rounds (refilling as it fires) keep it at
        // the reload for a while, then the hoists set the pace
        var qe = Fleet.Get("super_dreadnought").Class!;
        var w = new World(Start, 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(qe), Lat, Lon, 0, 0, side: 0));
        var store = w.Ships.Store;
        int slot = store.SlotOf(1), m = qe.Links(qe.Batteries[0].Mounts)[0];
        var row = qe.Mounts[m];
        int guns = qe.TurretTypes[row.Type].Barrels;
        long t = Start.Ms;
        var gaps = new List<double>();
        for (int k = 0; k < 30; k++)
        {
            store.Combat.Fired(slot, qe, m, 0, guns, t);
            long next = store.Combat.Mount(slot, m).LoadedMs;
            gaps.Add((next - t) / 1000.0);
            t = next;
        }
        int atReload = gaps.TakeWhile(g => Math.Abs(g - row.ReloadS) < 0.01).Count();
        output.WriteLine($"{row.ReadyRounds} ready rounds, reload {row.ReloadS:F1} s, hoists {row.SupplyS:F1} s: {atReload} salvos at the reload, " +
                         $"then {gaps[^1]:F1} s");
        Assert.True(atReload >= row.ReadyRounds / guns, "the ready rounds at the reload");
        Assert.Equal(row.SupplyS, gaps[^1], 1);
    }

    [Fact]
    public void Close_in_the_layers_lay_by_sight_and_walk_their_fall_onto_her()
    {
        // Mikasa's 6" (early telescopes, continuous aim, ranges by eye) on a protected cruiser 800 m off, both steaming
        // north: point blank, laid by sight at her present range and walked on by their own falls, they hit with most
        // shells (the short-range bug hunt, 2026-10-18: a cruiser that close should be wrecked; 50% before the walk)
        var mikasa = Fleet.Get("mikasa").Class!;
        int bat = -1;
        for (int k = 0; k < mikasa.Batteries.Length; k++)
            if (mikasa.Batteries[k].Side == Side.Starboard && mikasa.Batteries[k].CalibreMm is > 140 and < 160)
                bat = k;
        int shells = 0, hits = 0, pointBlank = 0, salvos = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = new World(Start, (ulong)seed, Settings);
            int a = w.Ships.AddClass(mikasa), c = w.Ships.AddClass(Fleet.Scenario("aurora"));
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, 800);
            var (tl, to) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 15, side: 0));
            w.Apply(ShipSystem.Spawn(c, tl, to, 0, 15, side: 1));
            var reports = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = reports.Add;
            w.Apply(GunnerySystem.Engage(1, bat, 2));
            w.AdvanceTo(Start + 6 * 60_000);
            foreach (var s in reports)
                (shells, hits, salvos, pointBlank) = (shells + s.Shells, hits + s.Hits, salvos + 1, pointBlank + (s.PointBlank ? 1 : 0));
        }
        output.WriteLine($"{hits} hits of {shells} shells ({100.0 * hits / shells:F0}%), {pointBlank} of {salvos} salvos laid point blank");
        Assert.True(pointBlank * 10 >= salvos * 9, "laid point blank");
        Assert.True(hits * 100 >= shells * 70, $"{100.0 * hits / shells:F0}% hits at 800 m");
    }

    [Fact]
    public void After_a_hit_the_guns_hold_their_lay()
    {
        // Mikasa's 6" on a cruiser 3 km off (by eye, outside point blank) and Lion on Derfflinger at 15 km: after a salvo
        // that hit, the next is laid where it was (the user, 2026-10-18: "the target teleported 200 m!" isn't a reason to
        // toss shells over her). Before the eye's error wandered slowly, the spots held her and lone overs held the aim, the
        // laid point moved 67 and 192 m on average, and up to 700 m
        var cases = new (ShipClass Shooter, ShipClass Target, int Battery, double RangeM, double Bearing)[]
        {
            (Fleet.Get("mikasa").Class!, Fleet.Scenario("aurora"), -1, 3_000, 90),
            (Fleet.Get("lion").Class!, Fleet.Scenario("derfflinger"), 0, 15_000, 80),
        };
        foreach (var (shooter, target, battery, rangeM, bearing) in cases)
        {
            int bat = battery;
            if (bat < 0)
                for (int k = 0; k < shooter.Batteries.Length; k++)
                    if (shooter.Batteries[k].Side == Side.Starboard && shooter.Batteries[k].CalibreMm is > 140 and < 160)
                        bat = k;
            var moves = new List<double>();
            for (int seed = 1; seed <= 6; seed++)
            {
                var w = new World(Start, (ulong)seed, Settings);
                int a = w.Ships.AddClass(shooter), c = w.Ships.AddClass(target);
                var p = Earth.Position(Lat, Lon);
                var f = Earth.Forward(p, bearing);
                Earth.Advance(ref p, ref f, rangeM);
                var (tl, to) = Earth.LatLon(p);
                w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 15, side: 0));
                w.Apply(ShipSystem.Spawn(c, tl, to, 0, 15, side: 1));
                var reports = new List<GunnerySystem.SalvoReport>();
                w.Gunnery.Reported = reports.Add;
                w.Apply(GunnerySystem.Engage(1, bat, 2));
                w.AdvanceTo(Start + 15 * 60_000);
                reports.Sort((x, y) => x.FiredMs.CompareTo(y.FiredMs));
                for (int i = 1; i < reports.Count; i++)
                    if (reports[i - 1].Hits > 0 && reports[i].FiredMs - reports[i - 1].FiredMs <= 40_000)
                        moves.Add(Math.Abs(reports[i].LaidErrM - reports[i - 1].LaidErrM));
            }
            double mean = moves.Count > 0 ? moves.Average() : 0;
            output.WriteLine($"{shooter.Name} at {rangeM / 1000:F0} km: {moves.Count} salvos after a hit, laid {mean:F0} m from the last on average, " +
                             $"{moves.Count(m => m > 100)} over 100 m");
            Assert.True(moves.Count >= 5, "it hit");
            Assert.True(mean < 50, $"the lay moved {mean:F0} m on average after a hit");
        }
    }

    [Fact]
    public void A_rate_spot_moves_every_computers_prediction_by_the_correction()
    {
        // a target 15 km east of a ship at rest, steaming north at 10 m/s, read cleanly every 2 s for two minutes; then
        // "rate up 60 m a minute" along the line of fire: a minute on, every computer has her 60 m further east
        var own = Earth.Position(Lat, Lon);
        var start = own;
        var f0 = Earth.Forward(start, 90);
        Earth.Advance(ref start, ref f0, 15_000);
        FcReading Read(int t)
        {
            var p = start;
            var f = Earth.Forward(p, 0);
            Earth.Advance(ref p, ref f, 10.0 * t);
            double r = Earth.Distance(own, p), b = Earth.Bearing(own, p);
            var (z, r11, r12, r22) = Track.Reading(own, r, b, 5, 0.2e-3);
            return new FcReading
            {
                Ms = t * 1000L, From = own, Z = z, RangeM = r, BearingDeg = b, SigmaRangeM = 5, SigmaBearingRad = 0.2e-3, R11 = r11, R12 = r12, R22 = r22,
            };
        }
        foreach (int preset in new[] { 2, 4, 7, 8, 9 })   // rate clock, true course, acceleration, turn rate, judgement
        {
            var c = FireControlPresets.At(preset).Computer with { LatencyS = 0 };
            var guess = Earth.Forward(start, 0) * 10;
            var tr = FcTracker.Start(c, Read(0), guess, 0.5);
            for (int t = 2; t <= 120; t += 2)
                tr.Fix(c, Read(t));
            var corrected = tr;
            tr.At(c, 120_000, 120_000, own, default, out var p0, out _);
            var along = Earth.Forward(p0, Earth.Bearing(own, p0));
            corrected.CorrectRate(along, 1);
            tr.At(c, 180_000, 120_000, own, default, out var a, out _);
            corrected.At(c, 180_000, 120_000, own, default, out var b, out _);
            double moved = Vec3d.Dot(b - a, along);
            output.WriteLine($"preset {preset} ({c.Tracker}): {moved:F1} m along the line of fire");
            Assert.InRange(moved, 50, 70);
        }
    }

    [Fact]
    public void The_german_ladder_brackets_in_one_flight_and_the_halves_alternate()
    {
        // Seydlitz (five twin turrets) on Lion at 16.5 km: three salvos stepped 400 yd apart go before the first falls;
        // firing for effect, three turrets and two in turn, half a reload apart
        var seydlitz = Fleet.Get("seydlitz").Class!;
        var w = new World(Start, 3, Settings);
        int a = w.Ships.AddClass(seydlitz), l = w.Ships.AddClass(Fleet.Get("lion").Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 16_500);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 20, side: 0));
        w.Apply(ShipSystem.Spawn(l, tl, to, 0, 20, side: 1));
        int german = Array.FindIndex(Settings.FireDisciplines, d => d.Name == "german");
        w.Apply(GunnerySystem.SetDiscipline(0, german));
        var salvos = new List<GunnerySystem.SalvoReport>();
        w.Gunnery.Reported = salvos.Add;
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        w.AdvanceTo(Start + 10 * 60_000);
        var dis = Settings.FireDisciplines[german];
        var first = salvos.OrderBy(s => s.FiredMs).Take(dis.InFlight).ToArray();
        output.WriteLine(string.Join("; ", first.Select(s => $"{(s.FiredMs - first[0].FiredMs) / 1000.0:F0} s step {s.LadderM:+0;-0} {s.Shells} guns")));
        Assert.Equal([-dis.LadderStepM, 0, dis.LadderStepM], first.Select(s => s.LadderM).Order().ToArray());
        Assert.True(first.All(s => s.Phase == FirePhase.Ranging && s.Shells >= dis.GunsMin && s.Shells < seydlitz.Batteries[0].Guns));
        // the first two go together, before the first one's fall (its flight is ~30 s)
        Assert.True(first[1].FiredMs - first[0].FiredMs < 10_000);

        var effect = salvos.Where(s => s.Phase == FirePhase.Effect).OrderBy(s => s.FiredMs).Take(12).ToArray();
        output.WriteLine("fire for effect: " + string.Join(", ", effect.Select(s => $"{s.Shells}")));
        Assert.True(effect.Length >= 8);
        int alternating = 0;
        for (int i = 1; i < effect.Length; i++)
            alternating += effect[i].Shells != effect[i - 1].Shells ? 1 : 0;
        Assert.True(alternating * 4 >= 3 * (effect.Length - 1), "the halves alternate");
    }
}
