using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The dark (docs/battle/night.md Q1): the night's light from the sun below the horizon, the moon and the stars
/// under the cloud, the eye's threshold rising with it, its glass, the twilight's arch, a bow wave, torpedo tracks; and the
/// day as it was.</summary>
public class NightSightTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A destroyer's lookout (7×50 at her highest eye) making out a ship side on in this sky, km.</summary>
    static double MadeOut(ShipClass target, double sky, double vis = 20_000, double silhouette = 0)
    {
        var dd = Fleet.Get("destroyer").Class!;
        return SightModel.MadeOutAtM(dd.Eyes[0].HeightM, S.SightLookoutMagnification, target, vis, 90, sky, silhouette, S,
            SightModel.LookoutGlass(SightModel.PupilMm(sky), S)) / 1000;
    }

    /// <summary>The light itself: a clear moonless night is the stars' 10⁻³ lux, an overcast takes most of it, a full
    /// moon high gives about a fifth of a lux, the sun 12° down still several times the stars (the northern June night);
    /// the moon down or the sky overcast, no silhouette toward it.</summary>
    [Fact]
    public void The_nights_light_comes_from_the_sun_the_moon_and_the_stars_under_the_cloud()
    {
        Assert.InRange(NightLight.Lux(Math.Sin(-30 * Math.PI / 180), 0, 0), NightLight.StarLux, 1.01 * NightLight.StarLux);
        // 15 Sep 1916 was a full moon: high over the North Sea at local midnight
        var p = Earth.Position(56.5, 5);
        var full = WorldTime.FromUtc(1916, 9, 12, 23, 30);
        var clear = NightLight.At(p, full, 0, S);
        var over = NightLight.At(p, full, 1, S);
        output.WriteLine($"full moon: {clear.Lux:0.000} lux (overcast {over.Lux:0.0000}), sky {clear.SkyCdM2:0.0e0} cd/m² (overcast {over.SkyCdM2:0.0e0}), silhouette {clear.MoonSilhouette:0.00}");
        Assert.InRange(clear.Lux, 0.05, 0.3);
        Assert.True(over.Lux < 0.3 * clear.Lux);
        Assert.True(over.SkyCdM2 < clear.SkyCdM2);
        Assert.Equal(0, over.MoonSilhouette);
        double twelve = NightLight.ClearSkyCdM2(-12), dark = NightLight.ClearSkyCdM2(-30);
        Assert.InRange(twelve / dark, 4, 15);
        // the twilight's arch: brighter toward the sun than across from it, its mean all round the mean
        var dusk = NightLight.At(Earth.Position(57, 5.5), WorldTime.FromUtc(1916, 5, 31, 23, 0), 0, S);
        double toward = dusk.Toward(dusk.SunBearingDeg), across = dusk.Toward(dusk.SunBearingDeg + 180), mean = 0;
        for (int b = 0; b < 360; b++)
            mean += dusk.Toward(b) / 360;
        output.WriteLine($"Jutland 23:00: toward the sun {toward:0.0e0}, across {across:0.0e0}, mean {mean:0.0e0} ({dusk.SkyCdM2:0.0e0})");
        Assert.True(toward > 5 * across);
        Assert.Equal(dusk.SkyCdM2, mean, dusk.SkyCdM2 * 1e-3);
        Assert.Equal(NightLight.At(Earth.Position(57, 5.5), WorldTime.FromUtc(1916, 5, 31, 23, 0), 1, S).SkyCdM2,
            NightLight.At(Earth.Position(57, 5.5), WorldTime.FromUtc(1916, 5, 31, 23, 0), 1, S).Toward(0), 1e-15);   // an overcast: all round
    }

    /// <summary>The record's night (docs/battle/night.md Q1): a destroyer made out by a destroyer's lookout at a few
    /// hundred metres on an overcast black night (the Dover Strait's ~600 yd), a couple of kilometres on a clear moonless
    /// one (Jutland's night actions at 700-2,000 yd), further against Jutland's midnight twilight and under a moon, a
    /// battleship further than a destroyer every time, the moon's path beyond her further still; and the day as it was.</summary>
    [Fact]
    public void The_dark_shortens_sight_as_the_light_falls()
    {
        var dd = Fleet.Get("destroyer").Class!;
        var lion = Fleet.Scenario("lion");
        double overcast = NightLight.ClearSkyCdM2(-30) * NightLight.Through(1, S), moonless = NightLight.ClearSkyCdM2(-30);
        double twilight = NightLight.ClearSkyCdM2(-12), moon = (NightLight.StarLux + NightLight.FullMoonLux * 0.5 * S.NightMoonSkyShare) / Math.PI;
        double[] skies = [overcast, moonless, twilight, moon];
        var ddAt = skies.Select(k => MadeOut(dd, k)).ToArray();
        var lionAt = skies.Select(k => MadeOut(lion, k)).ToArray();
        output.WriteLine($"a destroyer made out at {string.Join(", ", ddAt.Select(x => $"{x:F1}"))} km; Lion at {string.Join(", ", lionAt.Select(x => $"{x:F1}"))} km" +
                         " (overcast, moonless, the sun 12° down, a full moon 30° up)");
        Assert.InRange(ddAt[0], 0.2, 1.0);
        Assert.InRange(ddAt[1], 1.0, 3.5);
        for (int i = 0; i < skies.Length; i++)
        {
            Assert.True(lionAt[i] > ddAt[i]);
            if (i > 0)
                Assert.True(ddAt[i] > ddAt[i - 1]);
        }
        Assert.True(MadeOut(dd, moon, 20_000, 0.9) > ddAt[3], "against the moon's path");
        // the day: no dark at all, the threshold as it was
        double day = NightLight.ClearSkyCdM2(30);
        Assert.Equal(Dark.Day, SightModel.DarkOf(day * 0.05, S));
        Assert.Equal(SightModel.Threshold(1e-3, S), SightModel.Threshold(1e-3, SightModel.DarkOf(day, S), S));
        Assert.Equal(S.SightContrastDay, SightModel.InherentContrast(day, 0, S));
        Assert.Equal(S.SightContrastNight, SightModel.InherentContrast(moonless, 0, S));
        Assert.True(double.IsPositiveInfinity(SightModel.LightReachM(day, S)));
        Assert.Equal(12_000, SightModel.SeenToM(12_000, day, S));
        Assert.True(SightModel.SeenToM(12_000, moonless, S) < 5_000);
    }

    /// <summary>The eye's glass in the dark (Q1): a 7×50 fills a dark-adapted pupil, a 6×30 half fills it, a
    /// rangefinder's narrow exit pupil gives a dim image; by day only the transmission, which the day doesn't feel.</summary>
    [Fact]
    public void A_night_glass_beats_a_small_one_and_a_rangefinder_sees_dim()
    {
        double dark = SightModel.PupilMm(NightLight.ClearSkyCdM2(-30)), bright = SightModel.PupilMm(NightLight.ClearSkyCdM2(30));
        Assert.InRange(dark, 6.5, 8);
        Assert.InRange(bright, 1.9, 3.5);
        var lion = Fleet.Get("lion").Class!;
        var director = lion.Eyes.ToArray().First(e => e.Ranges);
        var bridge = lion.Eyes.ToArray().First(e => e.Kind == EyeKind.Bridge);
        double binoc = SightModel.GlassLight(bridge, dark, S), scope = SightModel.GlassLight(director, dark, S);
        double small = 0.6 * Math.Min(1, 5.0 * 5.0 / (dark * dark));   // a 6×30's 5 mm exit pupil
        output.WriteLine($"in the dark (pupil {dark:F1} mm): 7×50 {binoc:F2}, 6×30 {small:F2}, the director's {scope:F3}");
        Assert.True(binoc > 1.5 * small);
        Assert.True(scope < 0.2 * binoc);
        Assert.Equal(0.6, SightModel.GlassLight(bridge, bright, S), 1e-9);
    }

    /// <summary>A bow wave (Q1) finds a ship coming bow on fast in the dark, not one stopped or seen from astern, and says
    /// nothing of what she is (it isn't her margin); torpedo tracks are seen less far in the dark, their day's reach by day.</summary>
    [Fact]
    public void A_bow_wave_shows_a_fast_ship_and_tracks_fade_in_the_dark()
    {
        var dd = Fleet.Get("destroyer").Class!;
        double sky = NightLight.ClearSkyCdM2(-30), glass = SightModel.LookoutGlass(SightModel.PupilMm(sky), S);
        SightLine At(double d, double aspect, double kn) => new(d, aspect, 20_000, sky, 0, SpeedMs: kn * 0.514444, Glass: glass);
        double fast = SightModel.BowWaveMargin(20, 7, dd, At(1500, 0, 30), S);
        Assert.True(fast > SightModel.BowWaveMargin(20, 7, dd, At(1500, 0, 10), S));
        Assert.Equal(0, SightModel.BowWaveMargin(20, 7, dd, At(1500, 0, 0), S));
        Assert.True(SightModel.BowWaveMargin(20, 7, dd, At(1500, 180, 30), S) < 1e-6);   // from astern
        output.WriteLine($"bow on at 1.5 km, moonless: her hull {SightModel.Margin(20, 7, dd, At(1500, 0, 30), S):F2}, her bow wave at 30 kn {fast:F2}");
        Assert.True(fast > SightModel.Margin(20, 7, dd, At(1500, 0, 30), S), "a fast boat bow on shows her bow wave before her hull");
        Assert.Equal(S.TorpedoSeenM, SightModel.TrackSeenM(S.TorpedoSeenM, NightLight.ClearSkyCdM2(30), S));
        double night = SightModel.TrackSeenM(S.TorpedoSeenM, sky, S), twilight = SightModel.TrackSeenM(S.TorpedoSeenM, NightLight.ClearSkyCdM2(-8), S);
        output.WriteLine($"a track seen at {night:F0} m on a moonless night, {twilight:F0} m with the sun 8° down");
        Assert.InRange(night, 100, 700);
        Assert.True(twilight > night && twilight < S.TorpedoSeenM);
    }

    /// <summary>The cloud cover goes with the weather (Q1): a command's, a scenario's, written back by a setup.</summary>
    [Fact]
    public void The_cloud_cover_is_part_of_the_weather()
    {
        var w = new World(WorldTime.FromUtc(1917, 4, 20, 23), 3, S);
        ulong clear = w.ComputeHash();
        w.Apply(Command.SetWeather(20_000, 200, 2, 0, 0.7));
        var here = w.Weather.At(Earth.Position(51, 1.5), w.Time);
        Assert.Equal(new WeatherHere(20_000, 200, 2, 0, 0.7), here);
        Assert.NotEqual(clear, w.ComputeHash());
        Assert.Throws<ArgumentException>(() => w.Apply(new Command(0, (ushort)WorldCommand.SetWeather, A: 2_000_000_000L, X: 20_000)));
        var dover = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "dover-strait.json"));
        Assert.Equal(1, dover.Weather!.Value.Cloud);
        Assert.Throws<ScenarioException>(() => Scenario.Parse("""{ "scenario": 1, "start": "1916-05-31T14:00", "weather": { "visibility_km": 5, "cloud": 2 }, "units": [] }"""));
        Assert.Contains("cloud 6/10", SetWeather.Words(new WeatherHere(8_000, 225, 6, 0, 0.6)));
    }
}

/// <summary>Lights (docs/battle/night.md Q2): a salvo's flash made out far on a dark night and seen as a light contact on
/// its bearing; the firer dazzled for a few seconds; a ship lit by a fire near her shows further; fires as they were.</summary>
public class NightLightsTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static double Moonless => NightLight.ClearSkyCdM2(-30);

    /// <summary>A mount's salvo as a light: big guns brighter than small, a broadside made out to the horizon on a dark
    /// night (the plan's 15-30 km), less far against a brighter sky.</summary>
    [Fact]
    public void A_salvos_flash_shows_far_on_a_dark_night()
    {
        var sd = Fleet.Get("super_dreadnought").Class!;
        var dd = Fleet.Get("destroyer").Class!;
        double Of(ShipClass c, int type) => MuzzleFlash.Of(c, Array.FindIndex(c.Mounts.ToArray(), m => m.Type == type)).EffectiveCd;
        double big = Of(sd, 0), small = Of(dd, 0);
        double Far(double cd, double sky) =>
            Enumerable.Range(1, 60).Where(km => SightModel.LightMargin(20, cd, 10, km * 1000, 20_000, sky, S) >= 1).DefaultIfEmpty(0).Max();
        output.WriteLine($"a 15\" twin's salvo {big:0.0e0} cd, made out to {Far(4 * big, Moonless)} km (four turrets); a destroyer's gun {small:0.0e0} cd, " +
                         $"{Far(small, Moonless)} km, with the sun 6° down {Far(small, NightLight.ClearSkyCdM2(-6))} km");
        Assert.True(big > 50 * small);
        Assert.InRange(Far(4 * big, Moonless), 15, 35);   // the horizon's limit for a flash 10 m up
        Assert.True(Far(small, Moonless) > 8);
        Assert.True(Far(small, NightLight.ClearSkyCdM2(-6)) < Far(small, Moonless) - 5);
    }

    /// <summary>Her own flash dazzles her eyes for a few seconds (Q2): a destroyer at 1 km on a moonless night all but
    /// lost right after a 15" twin fires, back within a tenth of her margin some 8 s on.</summary>
    [Fact]
    public void Her_own_flash_dazzles_her_for_a_few_seconds()
    {
        var sd = Fleet.Get("super_dreadnought").Class!;
        var dd = Fleet.Get("destroyer").Class!;
        double cd = MuzzleFlash.Of(sd, 0).EffectiveCd, glass = SightModel.LookoutGlass(SightModel.PupilMm(Moonless), S);
        double At(double ageS) => SightModel.Margin(25, 7, dd,
            new SightLine(1000, 90, 20_000, Moonless, 0, Glass: glass, AdaptedCdM2: SightModel.DazzleCdM2(cd, ageS, S)), S);
        double clear = SightModel.Margin(25, 7, dd, new SightLine(1000, 90, 20_000, Moonless, 0, Glass: glass), S);
        output.WriteLine($"margin {clear:F2} undazzled; after her salvo {At(0):F2} at once, {At(3):F2} at 3 s, {At(8):F2} at 8 s, {At(15):F2} at 15 s");
        Assert.True(At(0) < 0.2 * clear);
        Assert.True(At(0) < At(3) && At(3) < At(8));
        Assert.True(At(8) > 0.9 * clear);
        Assert.Equal(0, SightModel.DazzleCdM2(cd, -1, S));
    }

    /// <summary>A ship lit by a fire near her shows further (Q2), from the side the light falls on; the light from beyond
    /// her lights nothing the eye sees.</summary>
    [Fact]
    public void A_ship_lit_by_a_fire_near_her_shows_further()
    {
        var dn = Fleet.Get("dreadnought").Class!;
        double glass = SightModel.LookoutGlass(SightModel.PupilMm(Moonless), S);
        double fire = 5 * S.FireGlowCdPerMW, lux = fire / (300.0 * 300.0);   // 5 MW 300 m off her
        SightLine At(double d, double litLux) => new(d, 90, 20_000, Moonless, 0, Glass: glass, LitLux: litLux);
        double dark = SightModel.Margin(25, 7, dn, At(4000, 0), S);
        double lit = SightModel.Margin(25, 7, dn, At(4000, lux * Lights.Facing(0, 0)), S);
        double beyond = SightModel.Margin(25, 7, dn, At(4000, lux * Lights.Facing(0, 180)), S);
        output.WriteLine($"a dreadnought at 4 km, moonless: {dark:F2} dark, {lit:F2} lit by a 5 MW fire 300 m off on the eye's side, {beyond:F2} from beyond her");
        Assert.True(lit > 2 * dark);
        Assert.Equal(dark, beyond, 1e-12);
        var lights = new Lights();
        Assert.Equal(0, lights.On(0, Earth.Position(56, 4), 20_000).Lux);
    }

    /// <summary>A world at a winter midnight (Q2): two ships 15 km apart, out of each other's sight; one fires, and the
    /// other side holds her as a light on its bearing at once; quiet again, the light goes out of sight.</summary>
    [Fact]
    public void A_salvo_seen_gives_a_light_contact_on_its_bearing()
    {
        var w = new World(WorldTime.FromUtc(1916, 1, 15, 23), 4, S);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(c, 56, 4, 90, 0, 1));
        w.Apply(ShipSystem.Spawn(c, 56.135, 4, 270, 0, 2));
        w.AdvanceTo(w.Time + 30_000);
        var pic = w.Sightings.Of(2);
        Assert.True(pic is null || pic.IndexOf(1) < 0, "15 km apart in the dark, unseen");
        var store = w.Ships.Store;
        int slot = store.SlotOf(1);
        var cls = store[slot].Handling.Class;
        for (int m = 0; m < cls.Mounts.Length; m++)
            if (cls.TurretTypes[cls.Mounts[m].Type].CalibreMm >= 200)
                store.Combat.Fired(slot, cls, m, 0, cls.TurretTypes[cls.Mounts[m].Type].Barrels, w.Time.Ms);
        w.AdvanceTo(w.Time + 1_000);
        pic = w.Sightings.Of(2)!;
        int i = pic.IndexOf(1);
        Assert.True(i >= 0, "her salvo seen");
        var o = store[store.SlotOf(2)].Position;
        double err = Earth.Bearing(o, pic[i].Position) - Earth.Bearing(o, store[slot].Position);
        err -= 360 * Math.Round(err / 360);
        output.WriteLine($"a light at {Earth.Distance(o, pic[i].Position) / 1000:F1} km reckoned (15 km true), bearing off by {err:F2}°");
        Assert.Equal(ContactLevel.Light, pic[i].Level);
        Assert.True(pic[i].LightInSight && !pic[i].InSight);
        Assert.InRange(Math.Abs(err), 0, 1);
        w.AdvanceTo(w.Time + 5_000);
        Assert.False(pic[pic.IndexOf(1)].LightInSight);
    }
}
