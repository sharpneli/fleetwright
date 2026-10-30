using Fleetwright.Shipgen;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Torpedoes (docs/battle/fighting.md 4.6): the facts (launchers, the placeholders' torpedo, a mount that trains);
/// a torpedo runs straight to its range and sinks; a spread at a steady ship close by hits; a hit at the same point and
/// moment whether the target is on the coarse tier or the fine tick; the anchors of the hit (one usually sinks a destroyer, a
/// capital ship takes 1,000-4,000 t and lives); a torpedo boat's Fight closes, fires and turns away; the flotilla
/// scenario replayed to the same hash, without garbage.</summary>
public class TorpedoTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.0, Lon = 4.0;

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>A world in clear weather (with the AI if asked) and two ships: the target at the origin, the shooter at a
    /// range on a bearing from it, beam-on to it.</summary>
    static World Pair(string shooter, string target, double rangeM, double bearingDeg, double targetKn, double speed = 1) =>
        Pair(shooter, target, rangeM, bearingDeg, targetKn, speed, out _, withAi: false);

    static World Pair(string shooter, string target, double rangeM, double bearingDeg, double targetKn, double speed, out AiSystem? ai, bool withAi = true,
        SimSettings? settings = null) =>
        Pair(Class(shooter), Class(target), rangeM, bearingDeg, targetKn, speed, out ai, withAi, settings);

    static World Pair(ShipClass shooter, ShipClass target, double rangeM, double bearingDeg, double targetKn, double speed, out AiSystem? ai,
        bool withAi = true, SimSettings? settings = null)
    {
        var w = new World(Start, 3, settings ?? Settings);
        ai = withAi ? new AiSystem(w) : null;
        w.Apply(Command.SetWeather(25_000));
        if (speed != 1)
            w.Apply(Command.SetSpeed(speed));
        int a = w.Ships.AddClass(shooter), b = w.Ships.AddClass(target);
        w.Apply(ShipSystem.Spawn(b, Lat, Lon, 0, targetKn, side: 1));
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, bearingDeg);
        Earth.Advance(ref p, ref f, rangeM);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(a, la, lo, (Earth.Bearing(p, Earth.Position(Lat, Lon)) + 90) % 360, 0, side: 0));
        return w;
    }

    [Fact]
    public void A_destroyer_has_launchers_with_torpedoes_and_its_mounts_train()
    {
        var c = Class("destroyer");
        Assert.NotEmpty(c.Launchers.ToArray());
        foreach (var l in c.Launchers)
        {
            Assert.True(l.Torpedoes > 0 && l.Tubes > 0);
            var t = c.Torpedoes[l.Torpedo];
            Assert.True(t.ChargeKg > 50 && t.FastKn > t.SlowKn && t.SlowRangeM > t.FastRangeM, $"{t}");
            if (l.Kind == LauncherKind.Mount)
                Assert.True(c.Mounts[l.Index].TrainDegS > 0);
        }
        var lion = Class("lion");
        Assert.Contains(lion.Launchers.ToArray(), l => l.Kind == LauncherKind.Tube);
    }

    [Fact]
    public void A_torpedo_runs_straight_to_its_range_and_sinks()
    {
        // fired at a ship beyond the fast setting's reach, on the slow one (once its mount has trained)
        var cls = Class("destroyer");
        var row = cls.Torpedoes[cls.Launchers[0].Torpedo];
        var w = Pair("destroyer", "super_dreadnought", row.FastRangeM + 1_500, 90, 0);
        w.Apply(TorpedoSystem.Launch(2, 1, 1, truth: true));
        for (int s = 1; s <= 120 && w.Torpedoes.Fired == 0; s++)
            w.AdvanceTo(Start + s * 1000L);
        Assert.Equal(1, w.Torpedoes.Running);
        int slot = -1;
        for (int i = 0; i < w.Torpedoes.Slots; i++)
            if (w.Torpedoes[i].Alive)
                slot = i;
        var t = w.Torpedoes[slot];
        Assert.Equal(row.SlowKn * Handling.MsPerKnot, t.SpeedMs, 6);
        Assert.InRange(t.EndS * t.SpeedMs, 0, row.SlowRangeM + 1e-6);   // its range, or less if it fails
        // the target out of the way: the run goes on to its end, and it sinks
        w.Apply(ShipSystem.Drop(1));
        w.AdvanceTo(w.Time + (long)(row.SlowRangeM / t.SpeedMs * 1000) + 10_000);
        Assert.Equal(0, w.Torpedoes.Running);
        Assert.Equal(1, w.Torpedoes.Ended);
        Assert.Equal(0, w.Torpedoes.Hits);
    }

    [Fact]
    public void A_spread_at_a_steady_ship_close_by_hits_and_it_floods()
    {
        var w = Pair("destroyer", "super_dreadnought", 2_500, 90, 12);
        w.Apply(TorpedoSystem.Launch(2, 1, 4, truth: true));
        w.AdvanceTo(Start + 5 * 60_000);
        output.WriteLine($"fired {w.Torpedoes.Fired}, hits {w.Torpedoes.Hits}");
        Assert.True(w.Torpedoes.Fired >= 2);
        Assert.True(w.Torpedoes.Hits >= 1);
        int slot = w.Ships.Store.SlotOf(1);   // -1: she's gone under already
        Assert.True(slot < 0 || w.Ships.Store.Combat.Condition(slot) is var k && (k.BreachTotalM2 > 10 || k.Status != ShipStatus.Afloat));
        Assert.Contains(Enumerable.Range(0, w.CombatLog.Count).Select(i => w.CombatLog.Newest(i)),
            e => e.Kind == CombatEvent.Hit && e.Source == HitSource.Torpedo && e.Target == 1);
    }

    [Fact]
    public void The_snapshot_tells_each_torpedo_apart_and_how_its_run_ended()
    {
        // docs/visuals/torpedoes.md R0: a spread's torpedoes each with a serial of their own, their depth, class and
        // launcher; each end with its serial, how it ended and (a hit's) the charge
        var w = Pair("destroyer", "super_dreadnought", 2_500, 90, 12);
        w.Apply(TorpedoSystem.Launch(2, 1, 4, truth: true));
        var snap = new Snapshot();
        var serials = new HashSet<long>();
        var ends = new Dictionary<long, (int Ship, TorpedoEnd How, double ChargeKg)>();
        var cls = Class("destroyer");
        for (int s = 1; s <= 300; s++)
        {
            w.AdvanceTo(Start + s * 1000L);
            w.Fill(snap);
            var v = snap.Torpedoes;
            for (int i = 0; i < v.Count; i++)
            {
                serials.Add(v.Serial[i]);
                Assert.Equal(w.Ships.Store[w.Ships.Store.SlotOf(2)].ClassIndex, v.Class[i]);
                Assert.InRange(v.Launcher[i], 0, cls.Launchers.Length - 1);
                Assert.InRange(v.DepthM[i], Settings.TorpedoDepthMinM, Settings.TorpedoDepthMaxM);
            }
            var e = snap.TorpedoEnds;
            for (int i = 0; i < e.Count; i++)
                ends[e.Serial[i]] = (e.Ship[i], e.How[i], e.ChargeKg[i]);
        }
        Assert.Equal(w.Torpedoes.Fired, serials.Count);
        Assert.Equal(serials.Order(), ends.Keys.Order());
        foreach (var (ship, how, charge) in ends.Values)
        {
            Assert.Equal(ship >= 0, how == TorpedoEnd.Hit);
            Assert.Equal(cls.Torpedoes[0].ChargeKg, charge);
        }
        Assert.Contains(ends.Values, x => x.How == TorpedoEnd.Hit);

        // every one failing (the design's torpedo block's): each ends short of its range, and says so
        var dd = Design.Load(Path.Combine(Fleet.Shipgen, "designs", "destroyer.json"));
        var block = TorpedoTemplates.Resolve(dd, dd.Torpedoes?.Torpedo)! with { Fail = 1 };
        dd = dd with { TorpedoTech = new() { ["Failing"] = block }, Torpedoes = dd.Torpedoes! with { Torpedo = "Failing" } };
        w = Pair(ShipClassCompiler.Compile(ShipDesign.Build(dd)), Class("super_dreadnought"), 2_500, 90, 12, 1, out _, withAi: false);
        w.Apply(TorpedoSystem.Launch(2, 1, 4, truth: true));
        var hows = new Dictionary<long, TorpedoEnd>();
        for (int s = 1; s <= 300; s++)
        {
            w.AdvanceTo(Start + s * 1000L);
            w.Fill(snap);
            for (int i = 0; i < snap.TorpedoEnds.Count; i++)
                hows[snap.TorpedoEnds.Serial[i]] = snap.TorpedoEnds.How[i];
        }
        Assert.Equal(w.Torpedoes.Fired, hows.Count);
        Assert.All(hows.Values, h => Assert.True(h is TorpedoEnd.Failed or TorpedoEnd.Hit));   // a failure may still meet her first
        Assert.Contains(hows.Values, h => h == TorpedoEnd.Failed);
    }

    [Fact]
    public void A_hit_does_not_depend_on_the_clock_tier()
    {
        // the same shot at a target steaming steady: on the fine tick at 1x, the coarse tier at 1,000x
        (long Ms, P3 Point) Shot(double speed)
        {
            var w = Pair("destroyer", "super_dreadnought", 2_500, 60, 15, speed);
            w.Apply(TorpedoSystem.Launch(2, 1, 1, truth: true));
            long hitMs = -1;
            P3 at = default;
            for (int s = 1; s <= 600 && hitMs < 0; s++)
            {
                w.AdvanceTo(Start + s * 1000L);
                for (int i = 0; i < w.Torpedoes.Slots; i++)
                    if (w.Torpedoes[i].Alive && w.Torpedoes[i].Hit >= 0)
                        (hitMs, at) = (w.Torpedoes[i].LaunchMs + (long)Math.Round(w.Torpedoes[i].EndS * 1000), w.Torpedoes[i].HitPoint);
            }
            output.WriteLine($"{speed}x: target {w.Ships.Store[w.Ships.Store.SlotOf(1)].Tier}, hit at {hitMs} ms, {at}");
            return (hitMs, at);
        }
        var fine = Shot(1);
        var coarse = Shot(1000);
        Assert.True(fine.Ms > 0 && coarse.Ms > 0, "both hit");
        Assert.InRange(Math.Abs(fine.Ms - coarse.Ms), 0, 300);
        Assert.InRange(Math.Abs(fine.Point.X - coarse.Point.X), 0, 6);
    }

    [Fact]
    public void One_torpedo_usually_sinks_a_destroyer_and_a_capital_ship_lives_through_one()
    {
        var torpedo = Class("destroyer").Torpedoes[0];
        var dd = GivenHits.Torpedoes(Class("destroyer"), torpedo, 2.1, double.NaN, 200, 1, 1, Settings, 3600);
        output.WriteLine($"destroyer: lost {100.0 * dd.Lost / dd.Ships:0}% (broken {100.0 * dd.LostBy[(int)LossKind.BrokeInTwo] / dd.Ships:0}%)");
        Assert.InRange((double)dd.Lost / dd.Ships, 0.55, 0.95);
        var bb = GivenHits.Torpedoes(Class("super_dreadnought"), torpedo, 4.5, double.NaN, 100, 1, 1, Settings, 3600);
        output.WriteLine($"battleship: water {bb.WaterT / bb.Ships:0} t, lost {100.0 * bb.Lost / bb.Ships:0}%");
        Assert.InRange(bb.WaterT / bb.Ships, 800, 4000);
        Assert.InRange((double)bb.Lost / bb.Ships, 0, 0.1);
    }

    [Fact]
    public void A_torpedo_boat_told_to_attack_closes_fires_and_turns_away()
    {
        // ordered to: by her own judgement a lone boat by day doesn't take on a battleship (docs/battle/command.md 5.1g).
        // The attack's steps are the test, not whether she lives through the run in: the battleship's hits do nothing
        // (since P3's fuzes she was often stopped short, 20 hits in, docs/battle/shells-armour-damage.md)
        var w = Pair("destroyer", "super_dreadnought", 12_000, 90, 12, 1, out var ai, settings: Settings with { HitsDoDamage = false });
        w.Apply(ai!.Orders.Attack(Unit.Ship(2), 1));
        long fired = 0;
        double closest = double.MaxValue;
        for (int s = 1; s <= 25 * 60 && fired == 0; s++)
        {
            w.AdvanceTo(Start + s * 1000L);
            fired = w.Torpedoes.Fired;
            if (w.Ships.Store.SlotOf(2) is int a and >= 0 && w.Ships.Store.SlotOf(1) is int b and >= 0)
                closest = Math.Min(closest, Earth.Distance(w.Ships.Store[a].Position, w.Ships.Store[b].Position));
        }
        output.WriteLine($"fired {fired} at {(w.Time.Ms - Start.Ms) / 1000} s, closest {closest:0} m");
        Assert.True(fired > 0, "it fired");
        var fast = Class("destroyer").Torpedoes[0].FastRangeM;
        Assert.True(closest < fast, "it closed to its torpedoes' range");
        // then away: the range opens over the next minutes
        int ts = w.Ships.Store.SlotOf(1), ss = w.Ships.Store.SlotOf(2);
        double at = Earth.Distance(w.Ships.Store[ss].Position, w.Ships.Store[ts].Position);
        w.AdvanceTo(w.Time + 120_000);
        if (w.Ships.Store.SlotOf(2) >= 0)
            Assert.True(Earth.Distance(w.Ships.Store[w.Ships.Store.SlotOf(2)].Position, w.Ships.Store[w.Ships.Store.SlotOf(1)].Position) > at,
                "turned away");
    }

    [Fact]
    public void The_flotilla_attacks_replays_and_allocates_nothing()
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "flotilla.json"));
        var designs = new[] { Path.Combine(Fleet.Shipgen, "designs") };
        // the test needs an attack made, and whether the boats get in is chance, moving with every change to the gunnery
        // (seed 7 did until the short-range bug hunt of 2026-10-18 let the battleships' secondaries hit close in): it takes
        // the first of four dozen seeds whose boats get their torpedoes away (seed 6 did until E7's fix of 2026-10-20: near
        // misses no longer black out the battleships and silence their secondaries; seed 34 then, seed 7 since the shell by
        // target of 2026-10-25)
        ulong seed = 0;
        World w = null!;
        AiSystem ai = null!;
        long warm = 0;
        while (++seed <= 48)
        {
            w = new World(s.Start, seed, Settings);
            ai = new AiSystem(w);
            var classes = ScenarioLoader.RequestClasses(s, w.Ships.Registry, designs);
            ScenarioLoader.Apply(s, w, ai, classes);
            // in a flat calm (docs/battle/balance.md BL1b): the attack's success by day is the balance round's (P10), this
            // test is its replay and its garbage
            var calm = w.Weather.At(default, w.Time);
            w.Apply(Command.SetSea(0, 12));
            // six boats against four battleships by day aren't worth an attack by their own judgement (docs/battle/command.md
            // 5.1g): ordered to, they make it, against a line that holds on (one turning away leaves them astern of its beam)
            w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
            // and a line firing by the magazine order: loading HE by target (docs/battle/shell-choice.md A3) its heavy
            // guns stopped the attack in all four dozen seeds (2026-10-25)
            w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Shells, (double)ShellUse.MagazineOrder));
            for (int k = 0; k < w.Groups.Count; k++)
                if (w.Groups[k].Name.Contains("Flotilla"))
                    w.Apply(ai.Orders.Attack(Unit.Group(w.Groups[k].GroupId)));
            // closing, the first attack made (the boats' first turn together, their first torpedoes): pools at their size
            warm = 0;
            while (w.Torpedoes.Fired == 0 && warm < 40 * 60)
                w.AdvanceTo(s.Start + ++warm * 1000);
            if (w.Torpedoes.Fired > 0)
                break;
        }
        Assert.True(w.Torpedoes.Fired > 0, "the boats fired in none of seeds 1-48");
        output.WriteLine($"seed {seed}");
        warm += 60;
        w.AdvanceTo(s.Start + warm * 1000);
        // nothing a second, but in the seconds a ship goes or falls out of her group (one-off events may allocate: the
        // user, 2026-10-14; falling out, docs/battle/command.md 5.1a)
        long allocated = 0;
        int Grouped()
        {
            int n = 0;
            for (int k = 0; k < w.Groups.Count; k++)
                n += w.Groups[k].Members.Count;
            return n;
        }
        for (long t = warm; t < warm + 34 * 60; t++)
        {
            int ships = w.Ships.Store.Count, grouped = Grouped();
            long before = GC.GetAllocatedBytesForCurrentThread();
            w.AdvanceTo(s.Start + (t + 1) * 1000);
            if (w.Ships.Store.Count == ships && Grouped() == grouped)
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        output.WriteLine($"first torpedoes at {(warm - 60) / 60.0:F1} min; torpedoes fired {w.Torpedoes.Fired}, hits {w.Torpedoes.Hits}; {allocated} bytes in 34 minutes");
        Assert.Equal(0, allocated);

        var r = new World(s.Start, seed, Settings, w.Ships.Registry);
        _ = new AiSystem(r);
        foreach (var c in w.CommandLog)
        {
            r.AdvanceTo(c.At);
            r.Apply(c.Command);
        }
        r.AdvanceTo(w.Time);
        Assert.Equal(w.ComputeHash(), r.ComputeHash());
    }
}
