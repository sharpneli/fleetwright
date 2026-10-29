using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Star shell in the fight (docs/battle/night.md Q5): a German ship of 1916 carries it in her light guns, one
/// mount lights a contact with it, its flares burst beyond her and over, burn, fall and drift, light her for every eye
/// and stand her dark against the sea behind for the firer; the same twice over.</summary>
public class StarShellTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static World Night(out int ship, out int dd, double apartLonDeg = 0.04)
    {
        var w = new World(WorldTime.FromUtc(1916, 1, 15, 23), 7, S);
        int c = w.Ships.AddClass(Fleet.Scenario("nassau_1916"));
        int d = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(d, 56, 4 + apartLonDeg, 180, 0, 2));
        (ship, dd) = (1, 2);
        return w;
    }

    /// <summary>Her 15 cm and 8.8 cm guns carry star shell, never fired by the magazine order.</summary>
    [Fact]
    public void A_German_ship_of_1916_carries_star_shell_in_her_light_guns()
    {
        var c = Fleet.Scenario("nassau_1916");
        var lo = c.Loadout;
        int found = 0;
        for (int b = 0; b < c.Batteries.Length; b++)
            foreach (var l in lo.Loads(b))
            {
                var t = lo.ShellTypes[l.ShellType];
                if (t.Kind != ShellKind.Star)
                    continue;
                found++;
                output.WriteLine($"{c.Batteries[b].CalibreMm} mm: star {t.MassKg:F1} kg, {t.StarCd:0.0e0} cd, {t.StarBurnS} s, {t.StarFallMs} m/s, {l.Share:P0}");
                Assert.True(c.Batteries[b].CalibreMm < 160);
                Assert.InRange(t.StarCd, 1e4, 1e6);
                Assert.Equal(Fuze.Time, t.Fuze);
            }
        Assert.True(found > 0);
        Assert.DoesNotContain(Fleet.Scenario("iron_duke").Loadout.ShellTypes.ToArray(), t => t.Kind == ShellKind.Star);
    }

    /// <summary>Ordered on a contact, one mount takes the task and its flares go up beyond her, a few hundred metres up,
    /// burning in turn; she's lit, and to the firer she stands dark against the sea it lights.</summary>
    [Fact]
    public void Star_shell_lights_a_contact_from_beyond()
    {
        var w = Night(out int ship, out int dd);
        w.AdvanceTo(w.Time + 30_000);
        Assert.True(w.Sightings.Of(1)!.IndexOf(dd) >= 0, "she holds the destroyer");
        w.Apply(StarShellSystem.Illuminate(ship, dd));
        var store = w.Ships.Store;
        int slot = store.SlotOf(ship), ds = store.SlotOf(dd);
        int up = 0, most = 0;
        double lux = 0, back = 0;
        for (int s = 0; s < 90; s++)
        {
            w.AdvanceTo(w.Time + 1_000);
            if (w.StarShells.FlareCount > 0)
                up++;
            most = Math.Max(most, w.StarShells.FlareCount);
            lux = Math.Max(lux, w.Lights.On(ds, store[ds].Position, 20_000).Lux);
            for (int f = 0; f < w.StarShells.FlareCount; f++)
            {
                var (at, cd) = w.StarShells.FlareAt(f, w.Time.Ms);
                double fromShip = Earth.Distance(store[slot].Position, at), fromHer = Earth.Distance(store[ds].Position, at);
                if (s % 10 == 0)
                    output.WriteLine($"{s} s: a flare {at.Length - Earth.Radius:F0} m up, {fromShip:F0} m from her firer, {fromHer:F0} m from her ({Earth.Distance(store[slot].Position, store[ds].Position):F0} apart), {cd:0.0e0} cd");
            }
            back = Math.Max(back, w.Lights.BacklightCdM2(store[slot].Position, store[ds].Position, 20_000, S));
        }
        var (target, mount) = w.StarShells.TaskOf(ship);
        output.WriteLine($"task on {target}, mount {mount}; a flare up {up} s of 90, at most {most}; on her at most {lux:F2} lux; " +
                         $"the sea behind her {back:0.0e0} cd/m² (the sky {NightLight.ClearSkyCdM2(-30):0.0e0})");
        Assert.Equal(dd, target);
        Assert.True(mount >= 0 && store.Combat.Illuminating(slot, mount));
        Assert.True(up > 45, "a flare up most of the time");
        Assert.True(lux > 0.05, "she's lit");
        Assert.True(back > 10 * NightLight.ClearSkyCdM2(-30), "the sea behind her lit for the firer");
        w.Apply(StarShellSystem.Illuminate(ship, 0));
        Assert.False(store.Combat.Illuminating(slot, mount));
    }

    /// <summary>A flare falls at its parachute's rate and drifts with the wind, the same however the clock is cut.</summary>
    [Fact]
    public void Flares_fall_and_drift_the_same_at_any_step()
    {
        (Vec3d At, double Cd) First(int stepMs)
        {
            var w = Night(out int ship, out int dd);
            w.Apply(Command.SetWeather(20_000, 270, 10));
            w.AdvanceTo(w.Time + 30_000);
            w.Apply(StarShellSystem.Illuminate(ship, dd));
            var end = w.Time + 40_000;
            while (w.Time < end)
                w.AdvanceTo(w.Time + stepMs);
            return w.StarShells.FlareCount > 0 ? w.StarShells.FlareAt(0, w.Time.Ms) : default;
        }
        var a = First(1_000);
        var b = First(250);
        Assert.True(a.Cd > 0);
        Assert.Equal(a.At.X, b.At.X, 6);
        Assert.Equal(a.At.Y, b.At.Y, 6);
        Assert.Equal(a.At.Z, b.At.Z, 6);
    }
}
