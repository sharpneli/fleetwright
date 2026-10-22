using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Smoke on the horizon (docs/battle/smoke.md Z6): a plume shows where it's dense, side on as a band or its
/// puffs' sum, end on its stack, none of it thin; a coal ship's smoke is made out before she is, as a contact at
/// <see cref="ContactLevel.Smoke"/> with a bearing on her and a rough range, raised to a ship once she's seen; the reach
/// grows by the smoke's top; none of it with the field off.</summary>
public class SmokeContactTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly WorldTime Start = WorldTime.FromUtc(new DateTime(1916, 5, 31, 14, 0, 0, DateTimeKind.Utc));

    /// <summary>A trail laid east along 56.5° N: <paramref name="n"/> puffs <paramref name="spacingM"/> apart, each
    /// stretched along it, its smoke <paramref name="area"/> m² a puff, at 60 m.</summary>
    static SmokePlume Trail(int n, double area, double spacingM = 50)
    {
        var sum = new PlumeSum();
        var at = Earth.Position(56.5, 4);
        var east = Earth.East(at);
        for (int i = 0; i < n; i++)
            sum.Add(new PuffState((at + east * (spacingM * i)).Normalized() * Earth.Radius, 60, 400, 400, area, 60, 2500, east), 0.8, 0.5);
        return sum.Plume(0.5);
    }

    [Fact]
    public void A_plume_shows_side_on_as_a_band_and_end_on_its_stack()
    {
        // a trail of overlapping puffs (50 m apart, each 54 m long): side on a band 5 km long, no puff dense alone; end on
        // its cross-section, smaller but darker
        var p = Trail(100, 2000);
        var (side, sideShade) = p.Showing(0, 1);
        var (end, endShade) = p.Showing(0, 0);
        var (oblique, _) = p.Showing(0, 0.5);
        output.WriteLine($"along {p.AlongM:F0} m across {p.AcrossM:F0} m; side on {side:F0} m² ({sideShade:F2}), at 30° {oblique:F0}, end on {end:F0} m² ({endShade:F2}); top {p.TopM:F0} m");
        Assert.True(Math.Abs(Vec3d.Dot(p.Axis, Earth.East(p.Centre))) > 0.999, "the trail's way");
        Assert.InRange(p.AlongM, 1300, 1600);   // 5 km of trail: its spread along it √(L²/12 + its puffs')
        Assert.Equal(0, p.Puffs.Side);
        Assert.InRange(side, 0.5 * 5000 * 40, 2 * 5000 * 40);   // a band ~5 km by ~40 m
        Assert.True(oblique <= side, "an oblique view shows no more than side on");
        Assert.True(end < side && endShade > sideShade, "end on: its cross-section, deeper and darker");
        // dense puffs far apart: their own regions add side on
        var q = Trail(10, 20_000, 2000);
        Assert.True(q.Puffs.Side > 0, "dense puffs apart show on their own");
        Assert.Equal(q.Puffs.Side, q.Showing(0, 1).AreaM2, q.Puffs.Side * 0.005);   // the horizon at the sea hides the Gaussian's tail
        // the horizon hides the share under it; thin smoke shows nothing side on, however long (end on, 5 km of it
        // stacked does)
        Assert.True(p.Showing(60, 1).AreaM2 < 0.6 * side && p.Showing(200, 1).AreaM2 < 0.01 * side);
        var thin = Trail(100, 50);
        Assert.True(thin.Showing(0, 1).AreaM2 == 0 && thin.Showing(0, 0).AreaM2 > 0);
    }

    /// <summary>An observer stopped at 56.5° N 4° E, a target at full speed from <paramref name="eastKm"/> east of her on
    /// a heading: each minute, the range, and whether her navy holds the target, at what level, in sight or by her smoke,
    /// and how far its estimate is from her.</summary>
    static (World W, List<(int Min, double Km, ContactLevel? Level, bool InSight, bool Smoke, double ErrKm)> Log) Pass(string target, double eastKm,
        double headingDeg, bool smoke, double minutes, double visibilityM)
    {
        var w = new World(Start, 3, Settings with { Smoke = smoke, RailsEnabled = false });
        int o = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        int t = w.Ships.AddClass(Fleet.Get(target).Class!);
        w.Apply(Command.SetWeather(visibilityM, 270, 8));
        w.Apply(ShipSystem.Spawn(o, 56.5, 4.0, 0, 0, 0));
        var tc = w.Ships.Registry.Get(t)!;
        double lon = 4.0 + eastKm / (111.32 * Math.Cos(56.5 * Math.PI / 180));
        w.Apply(ShipSystem.Spawn(t, headingDeg == 0 ? 56.3 : 56.5, lon, headingDeg, tc.Motion.MaxSpeedKn, 1));
        var log = new List<(int, double, ContactLevel?, bool, bool, double)>();
        for (int m = 0; m < minutes; m++)
        {
            w.AdvanceTo(w.Time + 60_000);
            var store = w.Ships.Store;
            var ts = store[store.SlotOf(2)];
            double km = Earth.Distance(store[store.SlotOf(1)].Position, ts.Position) / 1000;
            var pic = w.Sightings.Of(0);
            int i = pic?.IndexOf(2) ?? -1;
            if (i < 0)
                log.Add((m + 1, km, null, false, false, 0));
            else
            {
                var (p, _, _) = pic!.EstimateAt(i, w.Time.Ms, Settings);
                log.Add((m + 1, km, pic[i].Level, pic[i].InSight, pic[i].SmokeInSight, Earth.Distance(p, ts.Position) / 1000));
            }
        }
        return (w, log);
    }

    void Write(World w, List<(int Min, double Km, ContactLevel? Level, bool InSight, bool Smoke, double ErrKm)> log)
    {
        output.WriteLine($"reach {w.Reach.SightM / 1000:F1} km (the smoke's top {w.Reach.SmokeTopM:F0} m)");
        foreach (var (min, km, level, inSight, smoke, err) in log)
            output.WriteLine($"{min,3} min {km,6:F1} km: {(level?.ToString() ?? "-"),-6} {(inSight ? "in sight" : smoke ? "her smoke" : "")} {(level != null ? $"estimate {err:F1} km off" : "")}");
    }

    [Fact]
    public void A_coal_ships_smoke_is_seen_before_she_is()
    {
        // Lion-like closing from 48 km on a 40 km day: her smoke first, then the ship (bow on, she goes in and out of
        // sight behind her own funnel smoke: Z3's line ends at her middle)
        var (w, log) = Pass("lion", 48, 270, true, 25, 40_000);
        Write(w, log);
        int first = log.FindIndex(l => l.Level != null), seen = log.FindIndex(l => l.Level >= ContactLevel.Ship);
        Assert.True(first >= 0 && seen > first, "her smoke before her");
        Assert.Equal(ContactLevel.Smoke, log[first].Level);
        Assert.True(log[first].Smoke && !log[first].InSight);
        Assert.True(log[first].ErrKm < 0.75 * log[first].Km, "a bearing on her and a rough range");
        Assert.True(log[first].Km - log[seen].Km > 3, "kilometres sooner");
        Assert.True(w.Reach.SmokeTopM > 100, "the reach counts the smoke's top");
    }

    [Fact]
    public void A_smoke_bearing_keeps_her_track_while_she_is_hull_down()
    {
        // passing 46 km off on a 40 km day: never seen, followed by her smoke
        var (w, log) = Pass("lion", 46, 0, true, 40, 40_000);
        Write(w, log);
        Assert.DoesNotContain(log, l => l.InSight);
        var held = log.Where(l => l.Smoke).ToList();
        Assert.True(held.Count > 10, "her smoke in sight most of the pass");
        Assert.True(held.Skip(5).All(l => l.ErrKm < 8), "the bearings and the ranges together keep her within a few kilometres");
        Assert.All(log.Where(l => l.Level != null), l => Assert.Equal(ContactLevel.Smoke, l.Level));
    }

    /// <summary>For the yardstick (docs/battle/smoke.md Z6): how far a stopped dreadnought's navy first holds a ship closing
    /// at full speed from 75 km, by her smoke and by seeing her, in each visibility; head on (her trail end on) and 15°
    /// off the line (her trail at 15° to it, passing 19 km off). The wind from the west, 8 m/s.</summary>
    [Fact]
    public void Print_first_sightings()
    {
        output.WriteLine("target        vis km   course   smoke km   ship km");
        foreach (var target in new[] { "lion", "dreadnought", "destroyer" })
            foreach (double vis in new[] { 16_000.0, 25_000, 40_000, 60_000 })
                foreach (double course in new[] { 270.0, 255 })
                {
                    var (_, log) = Pass(target, 75, course, true, 120, vis);
                    int smoke = log.FindIndex(l => l.Level == ContactLevel.Smoke), ship = log.FindIndex(l => l.Level >= ContactLevel.Ship);
                    output.WriteLine($"{target,-12} {vis / 1000,6:F0} {course,8:F0} {(smoke >= 0 ? log[smoke].Km.ToString("F1") : "-"),10} {(ship >= 0 ? log[ship].Km.ToString("F1") : "-"),9}");
                }
    }

    [Fact]
    public void No_smoke_contacts_with_the_field_off()
    {
        var (w, log) = Pass("lion", 48, 270, false, 25, 40_000);
        Write(w, log);
        Assert.Equal(0, w.Reach.SmokeTopM);
        Assert.DoesNotContain(log, l => l.Level == ContactLevel.Smoke || l.Smoke);
        Assert.Contains(log, l => l.InSight);
        // the field on, she's first seen at the same look: her smoke's looks draw from a stream of their own
        var (_, on) = Pass("lion", 48, 270, true, 25, 40_000);
        Assert.Equal(log.FindIndex(l => l.Level >= ContactLevel.Ship), on.FindIndex(l => l.Level >= ContactLevel.Ship));
    }
}
