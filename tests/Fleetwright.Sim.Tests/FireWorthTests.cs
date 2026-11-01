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
        // has her once more (a salvo in the air when she's put out may straddle and bring it straight back) (BL1o)
        int weaved = 0, turned = 0, runs = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var w = Duel("light_cruiser", "dreadnought", 9_000, 12, Settings, seed);
            var salvos = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = salvos.Add;
            w.AdvanceTo(Start + 6 * 60_000);
            if (salvos.Count == 0 || salvos[^1].Phase != FirePhase.Effect)
                continue;
            runs++;
            int n = salvos.Count;
            for (int leg = 0; leg < 6; leg++)
            {
                w.Apply(ShipSystem.Course(1, leg % 2 == 0 ? 3 : 357));
                w.AdvanceTo(w.Time + 40_000);
            }
            weaved += salvos.Skip(n).Count(s => s.Phase == FirePhase.Ranging);
            n = salvos.Count;
            for (int leg = 0; leg < 4; leg++)
            {
                w.Apply(ShipSystem.Course(1, leg % 2 == 0 ? 12 : 348));
                w.AdvanceTo(w.Time + 60_000);
            }
            turned += salvos.Skip(n).Count(s => s.Phase == FirePhase.Ranging);
        }
        output.WriteLine($"{runs} runs firing for effect: ranging salvos in the 3° weave {weaved}, in the 12° turns {turned}");
        Assert.True(runs >= 3, "it came to fire for effect");
        Assert.Equal(0, weaved);
        Assert.True(turned > 0, "the turns put it out");
    }

    [Fact]
    public void Her_gunnery_officer_pares_his_fire_back_while_her_turns_spoil_it()
    {
        // a Yamato-like weaving 20° either way every minute at 8 km, turning too gently for her helm-free plot to range
        // again but enough for its own-turn kick: with the officer's judgement she holds her salvos for the steadier
        // moments, fires fewer shells and hits with more of them (BL1p; 8 seeds: 684 shells and 194 hits against 840 and 133)
        (int Shells, int Hits) Run(SimSettings s)
        {
            int shells = 0, hits = 0;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var w = Duel("yamato", "dreadnought", 8_000, 18, s, seed);
                for (int leg = 0; leg < 12; leg++)
                {
                    w.Apply(ShipSystem.Course(1, leg % 2 == 0 ? 20 : 340));
                    w.AdvanceTo(w.Time + 60_000);
                }
                var t = w.Gunnery.TallyOf(1, 0)!.Value;
                (shells, hits) = (shells + t.Shells, hits + t.Hits);
            }
            return (shells, hits);
        }
        var judged = Run(Settings);
        var blind = Run(Settings with { FireWorthSteadyShare = 0, FireWorthFloorPct = 0 });
        output.WriteLine($"weaving: judged {judged.Shells} shells, {judged.Hits} hits; firing whenever laid {blind.Shells} shells, {blind.Hits} hits");
        Assert.True(judged.Shells < 0.9 * blind.Shells, "fewer shells");
        Assert.True((double)judged.Hits / Math.Max(judged.Shells, 1) > 1.2 * blind.Hits / Math.Max(blind.Shells, 1), "a better shell's worth");
        Assert.True(judged.Hits >= blind.Hits, "and as many hits");
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
}
