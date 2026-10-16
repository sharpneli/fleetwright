using Fleetwright.Shipgen;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Loadouts, ship by ship (docs/battle/shells-armour-damage.md S5, decision 6): a ship may carry other shells
/// than her design's, set outside the design; her guns fire them, her rounds are counted by them, and she takes one only
/// before she's fired (in port, until the campaign's ports at spawn).</summary>
public class LoadoutTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { SplashLifeMs = 3_600_000 };

    static ShipClass Dreadnought => Fleet.Get("dreadnought").Class!;

    static Loadout Load(ShipClass c, string spec)
    {
        var errors = new List<string>();
        var lo = c.Load(LoadoutSpec.Parse(spec), errors);
        Assert.True(lo != null, string.Join("; ", errors));
        return lo!;
    }

    /// <summary>A kind names the battery's own block, a template's name brings it in, and the rounds split by the
    /// shares; the classes' own stays as it was.</summary>
    [Fact]
    public void A_loadout_gives_a_battery_other_shells()
    {
        var c = Dreadnought;
        var he = Load(c, "main:he");
        var main = he.Loads(0).ToArray();
        Assert.Single(main);
        Assert.Equal(ShellKind.He, he.ShellTypes[main[0].ShellType].Kind);
        Assert.Equal("305/45 RN 1906 HE (lyddite)", he.ShellTypeId(he.Fires(0)));
        Assert.False(he.IsDefault);
        Assert.True(c.Loadout.IsDefault);
        Assert.NotEqual(c.Loadout.Hash, he.Hash);
        // the secondaries untouched: the same shells as the design's
        Assert.Equal(c.Loadout.Loads(1).ToArray().Select(l => c.Loadout.ShellTypeId(l.ShellType)),
            he.Loads(1).ToArray().Select(l => he.ShellTypeId(l.ShellType)));
        // every main mount's rounds all HE
        foreach (int m in c.Links(c.Batteries[0].Mounts))
            Assert.Equal(c.Mounts[m].Rounds, he.LoadRounds[he.MountLoadStart(m)]);

        // a template by name and shares: in the magazine order, AP first, the rounds near their shares
        var mix = Load(c, "main:RN 1912 HE (lyddite)@0.25+ap");
        var loads = mix.Loads(0).ToArray();
        Assert.Equal([(ShellKind.Ap, 0.75), (ShellKind.He, 0.25)], loads.Select(l => (mix.ShellTypes[l.ShellType].Kind, l.Share)));
        Assert.Equal("305/45 RN 1912 HE (lyddite)", mix.ShellTypeId(loads[1].ShellType));
        int m0 = c.Links(c.Batteries[0].Mounts)[0];
        int r = c.Mounts[m0].Rounds;
        Assert.Equal(r, mix.LoadRounds[mix.MountLoadStart(m0)] + mix.LoadRounds[mix.MountLoadStart(m0) + 1]);
        Assert.InRange(mix.LoadRounds[mix.MountLoadStart(m0) + 1], r / 4 - 1, r / 4 + 1);
        output.WriteLine($"{c.Name}: {string.Join(", ", loads.Select(l => $"{mix.ShellTypeId(l.ShellType)} {l.Share:P0}"))}, {r} rounds a mount");
    }

    /// <summary>The gun keeps its velocity with the design's heaviest shell: a heavier one leaves it slower by the fourth
    /// root of the masses, a lighter one faster, as the kinds do (decision 6).</summary>
    [Fact]
    public void A_heavier_shell_leaves_the_gun_slower()
    {
        var c = Dreadnought;
        var own = c.Loadout.ShellTypes[c.Loadout.Fires(0)];
        var heavy = Load(c, "main:US 1941 AP Mk 8 (Explosive D)");
        var s = heavy.ShellTypes[heavy.Fires(0)];
        output.WriteLine($"{c.Loadout.ShellTypeId(c.Loadout.Fires(0))}: {own.MassKg:0.#} kg at {own.MuzzleVelocityMs:0} m/s; " +
                         $"{heavy.ShellTypeId(heavy.Fires(0))}: {s.MassKg:0.#} kg at {s.MuzzleVelocityMs:0} m/s");
        Assert.True(s.MassKg > own.MassKg);
        Assert.Equal(own.MuzzleVelocityMs * Math.Pow(own.MassKg / s.MassKg, 0.25), s.MuzzleVelocityMs, 6);
    }

    [Theory]
    [InlineData("torpedoes:ap", "name a battery")]
    [InlineData("secondary[7]:ap", "no secondary[7]")]
    [InlineData("main:no such shell", "no shell \"no such shell\"")]
    [InlineData("main:ap@0.5+he@0.2", "add up to 0.7")]
    public void A_loadout_that_does_not_fit_says_why(string spec, string error)
    {
        var errors = new List<string>();
        Assert.Null(Dreadnought.Load(LoadoutSpec.Parse(spec), errors));
        Assert.Contains(errors, e => e.Contains(error));
    }

    /// <summary>A ship given HE fires HE (the dev's salvo by the magazine order), and her rounds are counted by her
    /// loadout's loads; once she's fired she takes no other.</summary>
    [Fact]
    public void A_ship_fires_her_loadout_and_takes_one_only_before_firing()
    {
        var w = new World(Start, 7, Settings with { DevFirePatternPct = 0 });
        _ = new AiSystem(w);
        var c = Dreadnought;
        int ci = w.Ships.AddClass(c);
        w.Apply(ShipSystem.Spawn(ci, 56.5, 4.0, 0, 0));
        w.Apply(ShipSystem.Spawn(ci, 56.5, 4.1, 0, 0));
        var he = Load(c, "main:he");
        int id = w.Ships.Registry.AddLoadout(he);
        Assert.Equal(id, w.Ships.Registry.AddLoadout(Load(c, "main:he")));   // the same loadout, the same id
        w.Apply(ShipSystem.Load(1, id));
        int slot = w.Ships.Store.SlotOf(1);
        Assert.Same(he, w.Ships.Store.Combat.Loadout(slot));
        Assert.Same(c.Loadout, w.Ships.Store.Combat.Loadout(w.Ships.Store.SlotOf(2)));

        var (lat, lon) = Earth.LatLon(Earth.Position(56.6, 4.0));
        w.Apply(ShellSystem.FireAt(1, 0, lat, lon));
        w.AdvanceTo(w.Time + 60_000);
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        Assert.True(snap.Splashes.Count > 0);
        for (int i = 0; i < snap.Splashes.Count; i++)
            Assert.Equal(ShellKind.He, snap.Splashes.Kind[i]);
        int k = snap.Ships.IndexOf(1);
        Assert.Same(he, snap.Ships.Loadout[k]);

        // fired: the design's own is refused now, and she keeps HE
        w.Apply(ShipSystem.Load(1, w.Ships.Registry.AddLoadout(Load(c, "main:ap"))));
        Assert.Same(he, w.Ships.Store.Combat.Loadout(slot));
    }

    /// <summary>A scenario's unit gives its ships a loadout, a ship entry its own over it; one that doesn't fit a design
    /// is a warning and the ship keeps her design's.</summary>
    [Fact]
    public void A_scenario_gives_its_ships_loadouts()
    {
        var s = Scenario.Parse("""
            { "scenario": 1, "name": "loadouts", "start": "1916-05-31T14:00", "sides": ["A", "B"],
              "units": [
                { "name": "line", "side": 0, "at": [56.5, 4.0], "course": 0, "kn": 10, "loadout": { "main": [{ "ammo": "he" }] },
                  "ships": [ { "design": "dreadnought", "count": 2 },
                             { "design": "dreadnought", "loadout": { "main": [{ "ammo": "ap", "share": 0.5 }, { "ammo": "he" }] } },
                             { "design": "dreadnought", "loadout": { "main[3]": [{ "ammo": "he" }] } } ] } ] }
            """, null);
        using var reg = new ShipClassRegistry(1);
        var w = new World(s.Start, s.Seed, Settings, reg);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, new Dictionary<string, int> { ["dreadnought"] = reg.Add(Dreadnought) });
        output.WriteLine(string.Join("\n", start.Warnings));
        var store = w.Ships.Store;
        ShellLoad[] Main(int e) => store.Combat.Loadout(store.SlotOf(e)).Loads(0).ToArray();
        Assert.Equal([1.0], Main(1).Select(l => l.Share));
        Assert.Equal([1.0], Main(2).Select(l => l.Share));
        Assert.Equal([0.5, 0.5], Main(3).Select(l => l.Share));
        Assert.True(store.Combat.Loadout(store.SlotOf(4)).IsDefault);
        Assert.Contains(start.Warnings, x => x.Contains("main[3]"));
    }
}
