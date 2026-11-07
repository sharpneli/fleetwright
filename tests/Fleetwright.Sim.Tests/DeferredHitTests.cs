using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>Explosions landing a second later (docs/battle/workers.md W16): in the fine tier a hit goes into her batch and lands on
/// the damage lattice at least <see cref="SimSettings.HitDelayMs"/> after it came, hits meanwhile in the batch after; in the coarse
/// tier at once; a command on her lands what's on its way first; what changes her condition waits behind the batch (the guard
/// throws if anything doesn't); and it all replays.</summary>
public class DeferredHitTests
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static World Make(out ShipClass cls)
    {
        var w = new World(Start, 3, Settings);
        cls = Class("dreadnought");
        int c = w.Ships.AddClass(cls);
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10));
        w.AdvanceTo(w.Time + 1000);
        return w;
    }

    static ShellTypeRow Shell() => Class("super_dreadnought").Loadout.ShellTypes[Class("super_dreadnought").Loadout.Fires(0)];

    static Hit HitOn(World w, ShipClass cls, ref RandomStream rng) => new()
    {
        Ship = 1, Ms = w.Time.Ms, Point = new P3(rng.NextDouble(-60, 60), cls.Beam / 2 + 1, rng.NextDouble(-6, 4)), Direction = new P3(0, -0.96, -0.28),
        SpeedMs = 450, Shell = Shell(), ShellType = 0, Source = HitSource.Shell,
    };

    static ShipCondition Her(World w) => w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));

    static long Lattice(long ms) => (ms + Settings.FloodMs - 1) / Settings.FloodMs * Settings.FloodMs;

    [Fact]
    public void A_hit_lands_on_the_lattice_a_second_on_and_not_before()
    {
        var w = Make(out var cls);
        w.AdvanceTo(w.Time + 250);   // off the lattice
        var rng = RandomStream.Named(1, "deferred");
        long t = w.Time.Ms, lands = Lattice(t + Settings.HitDelayMs);
        w.Damage.Take(w, HitOn(w, cls, ref rng));
        Assert.Equal(0, Her(w).Hits);
        w.AdvanceTo(new WorldTime(lands));   // the world runs a moment's events once its clock goes past it
        Assert.Equal(0, Her(w).Hits);
        w.AdvanceTo(new WorldTime(lands + 1));
        Assert.Equal(1, Her(w).Hits);
    }

    [Fact]
    public void In_the_coarse_tier_a_hit_lands_at_once()
    {
        var w = Make(out var cls);
        w.Apply(Command.SetSpeed(Settings.CoarseMinSpeed * 2));
        Assert.True(w.Coarse);
        var rng = RandomStream.Named(1, "coarse");
        w.Damage.Take(w, HitOn(w, cls, ref rng));
        Assert.Equal(1, Her(w).Hits);
    }

    [Fact]
    public void Hits_meanwhile_land_in_the_next_batch_and_a_command_lands_them_first()
    {
        var w = Make(out var cls);
        var rng = RandomStream.Named(1, "meanwhile");
        long lands = Lattice(w.Time.Ms + Settings.HitDelayMs);
        w.Damage.Take(w, HitOn(w, cls, ref rng));
        w.AdvanceTo(w.Time + 100);
        w.Damage.Take(w, HitOn(w, cls, ref rng));   // the first is on its way: this one waits for the next batch
        w.AdvanceTo(new WorldTime(lands + 1));
        Assert.Equal(1, Her(w).Hits);
        w.AdvanceTo(new WorldTime(lands + Settings.HitDelayMs));
        Assert.Equal(1, Her(w).Hits);
        w.AdvanceTo(new WorldTime(lands + Settings.HitDelayMs + 1));
        Assert.Equal(2, Her(w).Hits);

        w.Damage.Take(w, HitOn(w, cls, ref rng));
        Assert.Equal(2, Her(w).Hits);
        w.Apply(DamageSystem.Jam(1, 0, 1));   // a command on her: what's on its way lands first
        Assert.Equal(3, Her(w).Hits);
    }

    /// <summary>Hits every few hundred ms on a ship already flooding and burning: her steps and outages' clears wait behind her
    /// batches (the guard throws if anything changes her condition meanwhile), and every hit lands.</summary>
    [Fact]
    public void Under_steady_fire_her_steps_wait_and_every_hit_lands()
    {
        var w = Make(out var cls);
        w.Apply(DamageSystem.SideHole(1, HoleAt.Mid, 0, -1, 2));
        w.Apply(DamageSystem.Burn(1, 10));
        var rng = RandomStream.Named(1, "steady");
        int n = 0;
        for (int i = 0; i < 60 && Her(w).Status == ShipStatus.Afloat; i++, n++)
        {
            w.Damage.Take(w, HitOn(w, cls, ref rng));
            w.AdvanceTo(w.Time + 150 + (i % 5) * 110);
        }
        w.AdvanceTo(w.Time + 5000);
        Assert.True(n > 10);
        Assert.Equal(Her(w).Status == ShipStatus.Afloat ? n : Her(w).Hits, Her(w).Hits);
        Assert.True(Her(w).WaterT > 0);
    }

    [Fact]
    public void Deferred_hits_replay_and_hash_alike()
    {
        ulong Play(bool threaded)
        {
            var w = Make(out var cls);
            w.Damage.Threaded = threaded;
            w.Apply(DamageSystem.SideHole(1, HoleAt.Bow, 0, -1, 1));
            var rng = RandomStream.Named(9, "replay");
            ulong h = 0;
            for (int i = 0; i < 40; i++)
            {
                if (Her(w).Status == ShipStatus.Afloat)
                    w.Damage.Take(w, HitOn(w, cls, ref rng));
                w.AdvanceTo(w.Time + 170 + (i % 7) * 90);
                h = RandomStream.Mix(h, w.ComputeHash());
            }
            w.Damage.Threaded = false;
            return h;
        }
        ulong inline = Play(false);
        Assert.Equal(inline, Play(false));
        Assert.Equal(inline, Play(true));   // the thread works it out early; the world is the same (W16d)
    }

    /// <summary>The squadrons fight with their explosions worked on the damage thread and on the sim thread: the same world, minute by
    /// minute (W16d).</summary>
    [Fact]
    public void The_squadrons_fight_the_same_with_the_damage_thread()
    {
        ulong[] Play(bool threaded)
        {
            var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "squadrons.json"));
            var w = new World(s.Start, s.Seed, Settings);
            var ai = new Ai.AiSystem(w);
            var classes = ScenarioLoader.RequestClasses(s, w.Ships.Registry, [Path.Combine(Fleet.Shipgen, "designs")]);
            ScenarioLoader.Apply(s, w, ai, classes);
            w.Damage.Threaded = threaded;
            var hashes = new ulong[20];
            for (int m = 0; m < hashes.Length; m++)
            {
                w.AdvanceTo(s.Start + (m + 6) * 60_000);
                hashes[m] = w.ComputeHash();
            }
            Assert.True(w.Shells.Hits > 0);
            w.Damage.Threaded = false;
            return hashes;
        }
        Assert.Equal(Play(false), Play(true));
    }
}
