using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>The sea coming aboard (docs/battle/error-sources.md ES4, <see cref="RollSea.Relative"/>, <see cref="SeaAboard"/>):
/// the sea's motion against points on her by the seakeeping law (the research's §1.2: Ochi; Nicholson and Price): none in a
/// calm; a long swell lifting her with it and a short sea running up her; on her weather side more than her lee; met at
/// speed, more often. On a class in Coronel's sea: her main-deck casemates out on the weather side and not the lee, a
/// turret's guns never; her forward glass wetted heading into the sea at speed, more as she goes faster, never with it
/// astern. Through the gunnery: her weather casemates fire less in it; and no garbage.</summary>
public class SeaAboardTests(Xunit.Abstractions.ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };

    static RollSea Sea(double windKn, double windFrom, double swellM, double swellS, double swellFrom) =>
        new(new SeaSpectrum(SeaState.Of(new WeatherHere(25_000, windFrom, windKn * 0.514444, SwellHsM: swellM, SwellTpS: swellS, SwellFromDeg: swellFrom))));

    /// <summary>A cruiser's hull (m, s): Devonshire's, rounded.</summary>
    static readonly Seakeeping Hull = new(143, 22, 14.7, 0.1, 0.85, 2.4 * Math.Sqrt(7.5), 0.3, 7);

    static (double M0, double M2)[] At(RollSea sea, double headingDeg, double kn, params (double X, double Y)[] points)
    {
        var (x, y) = (points.Select(p => p.X).ToArray(), points.Select(p => p.Y).ToArray());
        double[] m0 = new double[x.Length], m2 = new double[x.Length];
        sea.Relative(headingDeg, kn * 0.514444, Hull, x, y, m0, m2);
        return [.. m0.Zip(m2)];
    }

    [Fact]
    public void The_sea_runs_up_her_weather_side_and_a_long_swell_lifts_her_with_it()
    {
        // a 5 m swell from the west, beam on, her heading north: her port side the weather side
        var beam = At(Sea(0, 270, 5, 11, 270), 0, 15, (0, -11), (0, 11), (0, 0));
        double port = Math.Sqrt(beam[0].M0), stbd = Math.Sqrt(beam[1].M0);
        output.WriteLine($"beam on: weather side sd {port:0.00} m, lee {stbd:0.00} m, the swell's own {5 / 4.0:0.00}");
        Assert.True(port > 1.3 * stbd, "the weather side's sea higher against her than the lee's");
        Assert.True(SeaAboard.Out(RollSea.SeaReach(beam[0].M0, beam[0].M2, 3), 10) > 0.2, "a casemate 3 m up on the weather side out a fifth of the time or more");
        Assert.True(SeaAboard.Out(RollSea.SeaReach(beam[1].M0, beam[1].M2, 3), 10) < 0.1, "the lee's casemate fighting");
        Assert.True(SeaAboard.Out(RollSea.SeaReach(beam[0].M0, beam[0].M2, 8), 10) < 0.01, "a turret's guns 8 m up never");

        // head on, amidships: a swell twice her length lifts her with it, a short wind sea doesn't
        var swell = At(Sea(0, 0, 3, 18, 0), 0, 15, (0, 0));
        var chop = At(Sea(24, 0, 0, 12, 0), 0, 15, (0, 0));
        double sw = Math.Sqrt(swell[0].M0) / (3 / 4.0), ch = Math.Sqrt(chop[0].M0) / (Sea(24, 0, 0, 12, 0).HsM / 4);
        output.WriteLine($"head on amidships, against the sea's own: an 18 s swell {sw:0.00}, the wind sea {ch:0.00}");
        // the swell's only half as high against her as it is (her heave lags it a little through its damped resonance: a
        // fifth of a radian at 15 kn), the short sea all of it
        Assert.True(sw < 0.6, "the long swell carries her");
        Assert.True(ch > 1.5 * sw, "the short sea runs up her");

        var calm = At(Sea(0, 0, 0, 12, 0), 0, 25, (70, 0), (0, -11));
        Assert.All(calm, m => Assert.Equal(0, m.M0));
        Assert.Equal(0, RollSea.SeaReach(calm[0].M0, calm[0].M2, 1));
    }

    [Fact]
    public void Into_a_sea_at_speed_her_bow_meets_it_more_often()
    {
        var sea = Sea(24, 0, 0, 12, 0);
        double Crossings((double M0, double M2) m) => Math.Sqrt(m.M2 / m.M0) / (2 * Math.PI);
        double slow = Crossings(At(sea, 0, 5, (70, 0))[0]), fast = Crossings(At(sea, 0, 25, (70, 0))[0]), astern = Crossings(At(sea, 180, 25, (70, 0))[0]);
        output.WriteLine($"her stem meets the sea {slow * 60:0} a minute at 5 kn, {fast * 60:0} at 25 kn into it, {astern * 60:0} at 25 kn with it astern");
        Assert.True(fast > 1.3 * slow);
        Assert.True(astern < slow);
    }

    [Fact]
    public void A_cruisers_casemates_and_glass_in_Coronels_sea()
    {
        // Coronel (Content/Scenarios/coronel.json): a 28 kn southerly, the Pacific swell 4 m at 12 s from 200°
        var cls = Fleet.Scenario("devonshire");
        var w = new WeatherHere(25_000, 180, 28 * 0.514444, SwellHsM: 4, SwellTpS: 12, SwellFromDeg: 200);
        var sea = new RollSea(new SeaSpectrum(SeaState.Of(w)));
        int n = SeaAboard.Points(cls);
        double[] x = new double[n], y = new double[n];
        SeaAboard.Place(cls, x, y);
        var up = Attitude.Upright;
        (double[] M0, double[] M2) Moments(double heading, double kn)
        {
            double[] m0 = new double[n], m2 = new double[n];
            sea.Relative(heading, kn * 0.514444, SeaAboard.Hull(Settings, cls, 1), x, y, m0, m2);
            return (m0, m2);
        }
        double Gun(int j, double heading, double kn)
        {
            var (m0, m2) = Moments(heading, kn);
            int p = SeaAboard.MountPoint(j);
            return SeaAboard.Out(SeaAboard.Green(Settings, cls, up, kn * 0.514444, x[p], y[p], m0[p], m2[p], SeaAboard.GunAxis(cls, j)), Settings.GreenWaterClearS);
        }
        double Glass(int j, double heading, double kn)
        {
            var (m0, m2) = Moments(heading, kn);
            int p = SeaAboard.MountPoint(j);
            double z = cls.Components[cls.Mounts[j].Component].Bounds.Top, v = kn * 0.514444;
            return SeaAboard.Out(SeaAboard.Green(Settings, cls, up, v, x[p], y[p], m0[p], m2[p], z) +
                                 SeaAboard.Spray(Settings, cls, up, v, heading, w, m0[0], m2[0], x[p], y[p], z), Settings.SprayClearS);
        }
        // her lowest mounts, a pair either side (the main-deck casemates), and her foremost turret
        var low = Enumerable.Range(0, cls.Mounts.Length).OrderBy(j => SeaAboard.GunAxis(cls, j)).First();
        int pair = Enumerable.Range(0, cls.Mounts.Length).Where(j => j != low && Math.Abs(cls.Mounts[j].X - cls.Mounts[low].X) < 1 && Math.Abs(cls.Mounts[j].Y + cls.Mounts[low].Y) < 1).Single();
        var (portC, stbdC) = cls.Mounts[low].Y < 0 ? (low, pair) : (pair, low);
        int fore = Enumerable.Range(0, cls.Mounts.Length).OrderByDescending(j => cls.Mounts[j].X).First();
        // heading 260: the swell from 200 on her port bow, 60° off
        double weather = Gun(portC, 260, 12), lee = Gun(stbdC, 260, 12), turret = Gun(fore, 200, 20);
        double ahead20 = Glass(fore, 200, 20), ahead12 = Glass(fore, 200, 12), astern = Glass(fore, 20, 20);
        output.WriteLine($"casemates (axis {SeaAboard.OverSea(cls, up, x[SeaAboard.MountPoint(low)], 0, SeaAboard.GunAxis(cls, low)):0.0} m): weather side out {weather:P0}, lee {lee:P0}; " +
                         $"the fore turret's guns head on at 20 kn {turret:P1}; its sight {ahead20:P0} at 20 kn, {ahead12:P0} at 12, {astern:P1} with it astern");
        Assert.True(weather > 0.25, "the weather casemates out much of the time");
        Assert.True(lee < weather / 3, "the lee's fighting");
        Assert.True(turret < 0.01, "a turret's guns not");
        Assert.True(ahead20 > 1.5 * ahead12 && ahead20 > 0.1, "her forward glass wetted into the sea at speed, more the faster");
        Assert.True(astern < 0.01, "and not with the sea astern");
    }

    [Fact]
    public void The_sea_against_her_costs_no_garbage()
    {
        var sea = Sea(24, 0, 2, 12, 30);
        Span<double> x = stackalloc double[4], y = stackalloc double[4], m0 = stackalloc double[4], m2 = stackalloc double[4];
        (x[0], x[1], x[2], x[3], y[1], y[2]) = (70, 30, -20, 0, -10, 10);
        sea.Relative(10, 8, Hull, x, y, m0, m2);
        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 20; i++)
        {
            sea.Relative(i * 18, 8, Hull, x, y, m0, m2);
            sum += m0[0] + RollSea.SeaReach(m0[1], m2[1], 3);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }
}
