using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Shooting with judgement (docs/battle/balance.md BL1o-BL1q): a plain computer's Dumaresq put out by a few
/// degrees of her turn, not a weave; the gunnery officer paring his fire back while her turn spoils it and holding it when
/// a shell isn't worth firing; a captain in the line holding his course for his guns.</summary>
public class FireWorthTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { HitsDoDamage = false };
    const double Lat = 56.0, Lon = 4.0;

    static World Duel(string shooter, string target, double rangeM, double kn, SimSettings settings, ulong seed = 1)
    {
        var w = new World(Start, seed, settings);
        int a = w.Ships.AddClass(Fleet.Get(shooter).Class!), b = w.Ships.AddClass(Fleet.Get(target).Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, rangeM);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, kn, side: 0));
        w.Apply(ShipSystem.Spawn(b, tl, to, 0, kn, side: 1));
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        return w;
    }

    [Fact]
    public void A_plain_computer_ranges_again_after_a_few_degrees_of_turn_not_for_a_weave()
    {
        // a light cruiser's Dumaresq and Vickers clock (her course set on it by hand) firing for effect at 9 km: four minutes
        // of weaving 3° either way leave it be; four of 12° turns put it out, its battery ranging again until a straddle
        // has her once more (BL1o). A salvo in the air when she's put out may straddle and bring it straight back before a
        // ranging salvo goes (since balance.md BL1ac's tighter falls, nearly always), so the phase is watched second by second
        int weaved = 0, turned = 0, runs = 0;
        int Legs(World w, int legs, double degrees, long ms)
        {
            int ranging = 0;
            for (int leg = 0; leg < legs; leg++)
            {
                w.Apply(ShipSystem.Course(1, leg % 2 == 0 ? degrees : 360 - degrees));
                for (long end = w.Time.Ms + ms; w.Time.Ms < end;)
                {
                    w.AdvanceTo(w.Time + 1_000);
                    ranging += w.Gunnery.TallyOf(1, 0) is { Phase: FirePhase.Ranging } ? 1 : 0;
                }
            }
            return ranging;
        }
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var w = Duel("light_cruiser", "dreadnought", 9_000, 12, Settings, seed);
            var salvos = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = salvos.Add;
            w.AdvanceTo(Start + 6 * 60_000);
            if (salvos.Count == 0 || salvos[^1].Phase != FirePhase.Effect)
                continue;
            runs++;
            weaved += Legs(w, 6, 3, 40_000);
            turned += Legs(w, 4, 12, 60_000);
        }
        output.WriteLine($"{runs} runs firing for effect: seconds ranging in the 3° weave {weaved}, in the 12° turns {turned}");
        Assert.True(runs >= 3, "it came to fire for effect");
        Assert.Equal(0, weaved);
        Assert.True(turned > 0, "the turns put it out");
    }

    /// <summary>Her own turns cost a helm-free table only what its own-ship inputs bring it late (docs/battle/fc-ladder-check.md
    /// FL1; the own-turn kick, a draw a salvo, gone): her course comes by the gyro's repeat motor at once, her speed by log,
    /// and that costs little even in hard turns. The table reckons her readings and her gun orders from the same place, so
    /// where its dead reckoning has drifted cancels; what's left is her speed's error now times the flight and the plot's
    /// latency, bounded by the speed she sheds in the turn however late her log (at 18 kn some 1.5-3 m/s, 50-100 m in the
    /// line). Measured on the readings' rung with her own inputs on against exact (8 seeds, fc-ladder-check.md FL2): a Yamato
    /// weaving 20° either way every minute at 8 km 954 v 963 hits; a Lion in 90° legs every two minutes at 12 km, her Forbes
    /// log read and set by hand 30 s late [EST], 374 v 360, and 366 with it two minutes late. (FL1 had the Lion hit 126 v
    /// 156 with every error on: 8 duels' noise; 138 v 140 since.) The officer's paring (BL1p) has nothing to hold for in
    /// either.</summary>
    [Theory]
    [InlineData("yamato", 8_000, 20.0, 60)]
    [InlineData("lion", 12_000, 90.0, 120)]
    public void Her_own_turns_cost_a_helm_free_table_little(string ship, double rangeM, double deg, int legS)
    {
        (int Shells, int Hits) Run(SimSettings s)
        {
            int shells = 0, hits = 0;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var w = Duel(ship, "dreadnought", rangeM, 18, s, seed);
                for (int leg = 0; leg < 720 / legS; leg++)
                {
                    w.Apply(ShipSystem.Course(1, leg % 2 == 0 ? deg : 360 - deg));
                    w.AdvanceTo(w.Time + legS * 1000);
                }
                var t = w.Gunnery.TallyOf(1, 0)!.Value;
                (shells, hits) = (shells + t.Shells, hits + t.Hits);
            }
            return (shells, hits);
        }
        var fed = Run(Settings with { GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1") });
        var exact = Run(Settings with { GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1") });
        var allFed = Run(Settings);
        var allExact = Run(Settings with { GunneryErrors = GunneryErrorScale.Parse("own=0") });
        output.WriteLine($"{ship}, {deg}° legs every {legS} s, the readings only: {fed.Hits} hits of {fed.Shells} with her speed fed late, {exact.Hits} of {exact.Shells} exactly; " +
                         $"every error on: {allFed.Hits} of {allFed.Shells}, {allExact.Hits} of {allExact.Shells}");
        Assert.InRange(fed.Hits, 0.94 * exact.Hits, 1.06 * exact.Hits);
    }

    [Fact]
    public void A_shell_not_worth_firing_is_held()
    {
        // the floor raised past anything a salvo could do: the battery is laid and ready, and holds (BL1p)
        var w = Duel("dreadnought", "dreadnought", 10_000, 15, Settings with { FireWorthFloorPct = 100 });
        w.AdvanceTo(Start + 5 * 60_000);
        var t = w.Gunnery.TallyOf(1, 0)!.Value;
        Assert.Equal(BatteryState.Holding, t.State);
        Assert.Equal(0, t.Shells);
    }

    [Fact]
    public void A_captain_in_the_line_holds_his_course_for_his_guns()
    {
        // two Dreadnoughts in line, the leader weaving 3° either way every two minutes, the second following 600 m astern,
        // both on a Dreadnought 9 km abeam: firing for effect, the second holds her course for her guns through the small
        // bends where she'd steer each one, and keeps her station still (BL1q)
        (int Effect, double Turned, double Gap) Run(SimSettings s)
        {
            var w = new World(Start, 3, s);
            var ai = new AiSystem(w);
            int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, 180);
            Earth.Advance(ref p, ref f, 600);
            var (bl, bo) = Earth.LatLon(p);
            p = Earth.Position(Lat, Lon);
            f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, 9_000);
            var (tl, to) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 15, side: 0));
            w.Apply(ShipSystem.Spawn(c, bl, bo, 0, 15, side: 0));
            w.Apply(ShipSystem.Spawn(c, tl, to, 0, 15, side: 1));
            w.Apply(ai.Orders.Follow(Unit.Ship(2), Unit.Ship(1), 600));
            w.Apply(GunnerySystem.Engage(1, 0, 3));
            w.Apply(GunnerySystem.Engage(2, 0, 3));
            int effect = 0;
            double turned = 0;
            for (int sec = 0; sec < 24 * 60; sec++)
            {
                if (sec % 120 == 0)
                    w.Apply(ShipSystem.Course(1, sec / 120 % 2 == 0 ? 3 : 357));
                w.AdvanceTo(w.Time + 1000);
                int slot = w.Ships.Store.SlotOf(2);
                if (!w.Ships.Store.Combat.FiresForEffect(slot, 0))
                    continue;
                effect++;
                turned += Math.Abs(double.RadiansToDegrees(w.Ships.Store[slot].YawRate));   // degrees in the second
            }
            return (effect, turned, Earth.Distance(w.Ships.Store[w.Ships.Store.SlotOf(1)].Position, w.Ships.Store[w.Ships.Store.SlotOf(2)].Position));
        }
        var steady = Run(Settings);
        var not = Run(Settings with { SteadyForGunsDeg = 0 });
        output.WriteLine($"the second firing for effect, seconds and degrees turned in them: holding for the guns {steady.Effect} s, {steady.Turned:0}° " +
                         $"({steady.Gap:0} m astern of the leader); steering every correction {not.Effect} s, {not.Turned:0}° ({not.Gap:0} m)");
        Assert.True(steady.Effect > 120 && not.Effect > 120, "both fired for effect");
        Assert.True(not.Turned > 10, "the bends are steered");
        Assert.True(steady.Turned / steady.Effect < 0.5 * not.Turned / not.Effect, "she turns half as much for each second of fire");
        Assert.InRange(steady.Gap, 400, 900);   // her station kept
    }

    [Fact]
    public void A_helm_free_computer_keeps_her_through_a_snake_and_ladders_at_its_doubt()
    {
        // a Bismarck snaking 40° either way every minute on full rudder, firing by the German ladder at another 15 km off
        // (the user's playtest, 2026-11-01: her falls "pulsed" far, near, far, 1 km and more): her Rechenstelle takes her
        // motion in, so her turns don't send her back to ranging, and a ladder's rungs are at the ranging step, the
        // computer's doubt, never the doctrine's whole 366 m about a solution good to a pattern (BL1t)
        int effect = 0, ranging = 0, wild = 0, rungs = 0;
        double widest = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var w = Duel("bismarck", "bismarck", 15_000, 24, Settings, seed);
            w.Apply(GunnerySystem.SetDiscipline(0, 2));
            var salvos = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = salvos.Add;
            w.AdvanceTo(Start + 5 * 60_000);
            for (int leg = 0; leg < 12; leg++)
            {
                w.Apply(ShipSystem.Course(1, leg % 2 == 0 ? 40 : 320, 35));
                w.AdvanceTo(w.Time + 60_000);
            }
            var main = salvos.Where(s => s.Battery == 0).ToList();
            rungs += main.Count(s => s.LadderM != 0);
            widest = Math.Max(widest, main.Count == 0 ? 0 : main.Max(s => Math.Abs(s.LadderM)));
            var after = main.SkipWhile(s => s.Phase != FirePhase.Effect).ToList();
            (effect, ranging, wild) = (effect + after.Count, ranging + after.Count(s => s.Phase == FirePhase.Ranging), wild + after.Count(s => Math.Abs(s.MeanX) > 600));
        }
        output.WriteLine($"after the first fire for effect: {ranging} ranging salvos of {effect}, {wild} falling over 600 m off; ladder rungs {rungs}, the widest {widest:0} m");
        Assert.True(effect > 60, "she came to fire for effect");
        Assert.True(ranging < 0.25 * effect, "her turns don't put her out");
        Assert.True(wild < 0.08 * effect, "her falls stay near her");
        Assert.True(rungs > 0 && widest < 366, "the ladder at its doubt");
    }

    [Fact]
    public void A_ladder_that_has_bracketed_her_halves_into_the_bracket()
    {
        // an armoured cruiser's EU-Anzeiger on another 8 km off, both steady, by the German ladder: a group whose falls
        // cross her within a pattern has found her, and fire for effect's spots close in from there (BL1y); a group that
        // closed the bracket without is followed by one inside it at a quarter of its width (BL1u: at a fixed step the
        // ladder never got nearer than its step, and ranged on for minutes against a steady target)
        var steps = new List<double>();
        int effect = 0, ranging = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            var w = Duel("armoured_cruiser", "armoured_cruiser", 8_000, 16, Settings, seed);
            w.Apply(GunnerySystem.SetDiscipline(0, 2));
            var salvos = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = salvos.Add;
            w.AdvanceTo(Start + 14 * 60_000);
            var main = salvos.Where(s => s.Battery == 0).ToList();
            steps.AddRange(main.Where(s => s.LadderM > 0).Select(s => s.LadderM));
            var after = main.SkipWhile(s => s.Phase != FirePhase.Effect).ToList();
            (effect, ranging) = (effect + after.Count, ranging + after.Count(s => s.Phase == FirePhase.Ranging));
        }
        output.WriteLine($"ladder steps {string.Join(", ", steps.Select(s => s.ToString("0")))}; ranging {ranging} of {effect} after the first fire for effect");
        Assert.True(steps.Count >= 3, "she laddered");
        Assert.True(steps.Count <= 6 || steps.Min() < 0.5 * steps.Max(), "a group or two found her, or a later group closed in");
        Assert.True(ranging < 0.2 * effect, "and she came to fire for effect");
    }
}
