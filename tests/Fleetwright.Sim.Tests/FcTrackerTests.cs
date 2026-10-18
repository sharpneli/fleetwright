using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The fire-control trackers on their own (docs/battle/fire-control.md F2), fed clean readings of a target
/// from a fixed observer: each fails where its machine failed, as fire_control_ref.py's mechanism checks have it. A rate
/// clock drifts on a crossing target and a true course doesn't; a straight-line machine keeps a standing error on a
/// circle and a turn filter doesn't.</summary>
public class FcTrackerTests(ITestOutputHelper output)
{
    const double Kn = 1852.0 / 3600;

    /// <summary>A target crossing at 25 kn at 15 km abeam of an observer steaming the other way at 20 kn (the
    /// reference's table 5), read every <paramref name="everyS"/>
    /// (range <paramref name="sigmaM"/>, bearing 0.5 mil): from <paramref name="turnAtS"/> it circles at
    /// <paramref name="rateDegS"/>. The tracker's miss over a 25 s flight, RMS m, over 120-240 s after the turn (and
    /// before it, 180-300 s), and the error in the velocity it holds before the turn, kn.</summary>
    static (double Before, double After, double RateKn) Circle(ComputerRow c, double rateDegS, double turnAtS = 300, double seconds = 600, int everyS = 2,
        double sigmaM = 10, double turnKn = 25)
    {
        var observer = Earth.Position(56, 4);
        var p = observer;
        var own = new Vec3d[(int)seconds + 30];
        double ownV = 20 * Kn;
        var o = observer;
        var of = Earth.Forward(o, 0);
        for (int t = 0; t < own.Length; t++)
        {
            own[t] = o;
            Earth.Advance(ref o, ref of, ownV);
        }
        Vec3d OwnVel(int t) => Earth.Forward(own[t], 0) * ownV;
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 15_000);
        double heading = 180, v = 25 * Kn;
        var rng = RandomStream.Named(7, "fc-tracker-test");
        var path = new Vec3d[(int)seconds + 30];
        var q = p;
        double h = heading;
        double speed = v;
        for (int t = 0; t < path.Length; t++)
        {
            path[t] = q;
            if (t >= turnAtS)
            {
                h += rateDegS;
                speed += (turnKn * Kn - speed) / 30;   // a ship losing way in the turn (as a ship does; the reference's don't)
            }
            var fw = Earth.Forward(q, h);
            Earth.Advance(ref q, ref fw, speed);
        }
        FcReading Read(int t)
        {
            double r = Earth.Distance(own[t], path[t]) + sigmaM * rng.NextGaussian(), b = Earth.Bearing(own[t], path[t]) + 0.5e-3 * 180 / Math.PI * rng.NextGaussian();
            var (z, r11, r12, r22) = Track.Reading(own[t], r, b, sigmaM, 0.5e-3);
            return new FcReading
            {
                Ms = t * 1000L, From = own[t], OwnVelocity = OwnVel(t), Z = z, RangeM = r, BearingDeg = b, SigmaRangeM = sigmaM, SigmaBearingRad = 0.5e-3, R11 = r11, R12 = r12, R22 = r22,
            };
        }
        var guess = Earth.Forward(path[0], heading) * v;
        var tr = FcTracker.Start(c, Read(0), guess, FcTracker.GuessSigma(c, v));
        double before = 0, after = 0, rate = 0;
        int nb = 0, na = 0;
        for (int t = 1; t < seconds; t++)
        {
            if (t % everyS == 0)
                tr.Fix(c, Read(t));
            tr.At(c, (t + 25) * 1000L, t * 1000L, own[t], OwnVel(t), out var pred, out _);
            tr.At(c, t * 1000L, t * 1000L, own[t], OwnVel(t), out _, out var vel);
            double miss = Earth.Distance(pred, path[t + 25]), dv = (vel - (path[t + 1] - path[t])).Length / Kn;
            if (t >= turnAtS - 120 && t < turnAtS)
                (before, rate, nb) = (before + miss * miss, rate + dv * dv, nb + 1);
            if (t >= turnAtS + 120 && t < turnAtS + 240)
                (after, na) = (after + miss * miss, na + 1);
        }
        return (Math.Sqrt(before / Math.Max(nb, 1)), Math.Sqrt(after / Math.Max(na, 1)), Math.Sqrt(rate / Math.Max(nb, 1)));
    }

    static ComputerRow Computer(int preset) => FireControlPresets.At(preset).Computer with { LatencyS = 0 };

    [Fact]
    public void A_turn_filter_follows_a_circle_and_a_straight_line_machine_lags_it()
    {
        foreach (int preset in new[] { 4, 7, 8, 11 })
        {
            var (before, after, _) = Circle(Computer(preset), 1.0);
            output.WriteLine($"{FireControlPresets.Name(preset),-14} straight {before,5:0} m, circling (+120-240 s) {after,5:0} m");
        }
        // with the bench's readings: one 15 ft rangefinder at 15 kyd, ~95 m a reading, at each preset's rate
        foreach (var (preset, every) in new[] { (4, 15), (7, 1), (8, 2), (11, 15) })
        {
            var (before, after, _) = Circle(Computer(preset), 1.0, everyS: every, sigmaM: 95);
            var (_, slowing, _) = Circle(Computer(preset), 1.0, everyS: every, sigmaM: 95, turnKn: 18);
            output.WriteLine($"{FireControlPresets.Name(preset),-14} a reading every {every,2} s, 95 m: straight {before,5:0} m, circling {after,5:0} m, slowing to 18 kn in it {slowing,5:0} m");
        }
        var (_, line, _) = Circle(Computer(4), 1.0);
        var (_, turn, _) = Circle(Computer(8), 1.0);
        Assert.True(line > 100, $"the true course lags a circle by {line:0} m");
        Assert.True(turn < 0.3 * line, $"the turn filter follows it to {turn:0} m");
    }

    [Fact]
    public void A_rate_clock_drifts_on_a_crossing_target_and_a_true_course_doesnt()
    {
        // the machines' cadence: a range and bearing every 30 s, 50 m in range
        var (clockMiss, _, clockKn) = Circle(Computer(3), 0, seconds: 400, everyS: 30, sigmaM: 50);
        var (lineMiss, _, lineKn) = Circle(Computer(4), 0, seconds: 400, everyS: 30, sigmaM: 50);
        output.WriteLine($"steady crossing target: rate clock {clockMiss:0} m, {clockKn:0.0} kn off; true course {lineMiss:0} m, {lineKn:0.0} kn off");
        Assert.True(clockKn > 2 * lineKn);
    }
}

public class FcTrackerOwnTurnTests(ITestOutputHelper output)
{
    const double Kn = 1852.0 / 3600;

    /// <summary>Own ship at 20 kn north turns 45° to starboard at 1°/s from 300 s (or holds on); the target crosses at 25
    /// kn at 15 km; readings every 30 s, 50 m, reaching the computer after its latency. The miss over a 25 s flight, RMS m
    /// over 24 seeds and the 150 s after the turn begins.</summary>
    static double OwnTurn(ComputerRow c, bool turn = true)
    {
        int n = 700;
        var own = new Vec3d[n];
        var ownV = new Vec3d[n];
        var o = Earth.Position(56, 4);
        double h = 0, v = 20 * Kn;
        for (int t = 0; t < n; t++)
        {
            own[t] = o;
            ownV[t] = Earth.Forward(o, h) * v;
            if (turn && t >= 300 && h < 45)
                h += 1;
            var f = Earth.Forward(o, h);
            Earth.Advance(ref o, ref f, v);
        }
        var path = new Vec3d[n];
        var p = Earth.Position(56, 4);
        var pf = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref pf, 15_000);
        for (int t = 0; t < n; t++)
        {
            path[t] = p;
            var f = Earth.Forward(p, 180);
            Earth.Advance(ref p, ref f, 25 * Kn);
        }
        double sum = 0;
        int k = 0;
        for (int seed = 0; seed < 24; seed++)
        {
            var rng = RandomStream.Named((ulong)seed, "own-turn-test");
            // a reading taken latency seconds before it reaches the computer at t
            FcReading Read(int t)
            {
                int m = Math.Max(t - (int)c.LatencyS, 0);
                double r = Earth.Distance(own[m], path[m]) + 50 * rng.NextGaussian(), b = Earth.Bearing(own[m], path[m]) + 0.5e-3 * 180 / Math.PI * rng.NextGaussian();
                var (z, r11, r12, r22) = Track.Reading(own[m], r, b, 50, 0.5e-3);
                return new FcReading
                {
                    Ms = m * 1000L, ArriveMs = t * 1000L, From = own[m], OwnVelocity = ownV[m], Z = z, RangeM = r, BearingDeg = b, SigmaRangeM = 50,
                    SigmaBearingRad = 0.5e-3, R11 = r11, R12 = r12, R22 = r22,
                };
            }
            var tr = FcTracker.Start(c, Read(0), Earth.Forward(path[0], 180) * 25 * Kn, FcTracker.GuessSigma(c, 25 * Kn));
            for (int t = 1; t < n - 30; t++)
            {
                if (t % 30 == 0)
                    tr.Fix(c, Read(t));
                tr.At(c, (t + 25) * 1000L, t * 1000L, own[t], ownV[t], out var pred, out _);
                if (t >= 300 && t < 450)
                {
                    double miss = Earth.Distance(pred, path[t + 25]);
                    (sum, k) = (sum + miss * miss, k + 1);
                }
            }
        }
        return Math.Sqrt(sum / k);
    }

    [Fact]
    public void A_helm_free_clock_takes_own_ships_turn_and_a_plain_one_must_relearn()
    {
        var clock = FireControlPresets.At(2).Computer with { MaxRangeRateMs = 0 };
        var free = clock with { HelmFree = true };
        double plain = OwnTurn(clock), helmFree = OwnTurn(free), plainSteady = OwnTurn(clock, false), freeSteady = OwnTurn(free, false);
        output.WriteLine($"own ship turning 45°: a plain clock {plain:0} m (holding on {plainSteady:0}), helm-free {helmFree:0} m (holding on {freeSteady:0})");
        // what the turn adds, in quadrature: a plain clock's stale rates against a helm-free one's
        static double Added(double a, double b) => Math.Sqrt(Math.Max(a * a - b * b, 0));
        Assert.True(Added(plain, plainSteady) > 40, "the turn costs the plain clock");
        Assert.True(Added(helmFree, freeSteady) < 0.5 * Added(plain, plainSteady), "and hardly the helm-free one");
    }
}
