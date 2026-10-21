using System.Globalization;
using System.Text;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen smoke facts`: each class's smoke as the field's model makes it (docs/battle/smoke.md Z0): the plant's
/// facts, a funnel's output, gas and buoyancy at speeds, the plume's rise and downwash in relative winds, and a trail of
/// its smoke at full power after 1, 5 and 30 minutes (its column, across it at plume height, 500 m along it), in neutral
/// and stable air; and the research's one tuning (1,500 m²/s, research/vfx/unified-smoke-system.md) beside her own.</summary>
static class SmokeBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The weathers the facts are shown in: Jutland's light wind (8 kn), neutral, and warm air over a cold sea.</summary>
    static readonly (string Name, WeatherHere W)[] Airs =
        [("neutral", new WeatherHere(25_000, 225, 8 * 0.514444)), ("stable +2 K", new WeatherHere(25_000, 225, 8 * 0.514444, 2))];

    public static int Run(IReadOnlyList<string> args, Func<string, ShipClass> classOf)
    {
        if (args.Count < 2 || args[0] != "facts")
        {
            Console.Error.WriteLine("shipgen smoke facts DESIGN...");
            return 2;
        }
        var sb = new StringBuilder();
        void Line(string t) => sb.AppendLine(t);
        foreach (var name in args.Skip(1))
        {
            var c = classOf(name);
            var s = c.Smoke;
            double full = c.Motion.MaxSpeedKn;
            Line(string.Format(Inv, "{0}: {1}, {2:0} shp rated, {3:0.00} kg/s of fuel at it; smoke {4:0.0} m² a kg, gas {5:0.0} m³ a kg at {6:0} K, {7:0.0} m/s out at rated",
                c, s.Fuel, c.Motion.RatedShp, c.FuelKgS(c.Motion.RatedShp), s.M2PerKg, s.GasM3PerKg, s.GasTempK, s.ExitMs));
            if (c.Funnels.Length == 0)
            {
                Line("  no funnels");
                continue;
            }
            Line("  funnels: " + string.Join(", ", Enumerable.Range(0, c.Funnels.Length).Select(i => string.Format(Inv, "{0} {1:0.00} ({2:0.0} m² out, top {3:0.0} m)",
                i + 1, c.Funnels[i].Share, c.Funnels[i].ExitAreaM2, c.Components[c.Funnels[i].Component].Bounds.Top - c.Vertical.Waterline))));
            var neutral = SmokeAir.Of(Airs[0].W);
            Line("  speed    shp     ship m²/s   funnel 1: m²/s  gas m³/s  out m/s  F m⁴/s³");
            foreach (double kn in new[] { 10, 15, full })
            {
                double total = 0;
                for (int i = 0; i < c.Funnels.Length; i++)
                    total += SmokeSources.Funnel(c, i, kn, 10, neutral).RateM2s;
                var f = SmokeSources.Funnel(c, 0, kn, 10, neutral);
                Line(string.Format(Inv, "  {0,4:0.0} kn {1,7:0} {2,9:0} {3,15:0} {4,9:0.0} {5,8:0.0} {6,8:0.0}", kn, c.PowerAt(kn), total, f.RateM2s, f.GasM3s, f.ExitMs, f.Flux));
            }
            foreach (var (an, w) in Airs)
            {
                var air = SmokeAir.Of(w);
                Line(string.Format(Inv, "  {0} air (u* {1:0.00} m/s, 1/L {2:0.0000}/m, K_h {3:0.0} m²/s, K_z at 50 m {4:0.0} m²/s): funnel 1 at {5:0.0} kn",
                    an, air.FrictionMs, air.InvObukhovM, air.SigmaV * air.SigmaV * air.LagrangianH, air.Kz(50), full));
                foreach (double u in new[] { 5.0, 10, 20 })
                {
                    var f = SmokeSources.Funnel(c, 0, full, u, air);
                    Line(string.Format(Inv, "    relative wind {0,2:0} m/s: rise {1,5:0.0} m over {2,4:0} s, downwash {3:0.0} m, from {4:0.0} m to {5:0.0} m",
                        u, f.RiseM, f.RiseS, f.DownwashM, f.BaseM, f.BaseM + f.RiseM));
                }
                double rel = full * 0.514444, rate = 0;
                for (int i = 0; i < c.Funnels.Length; i++)
                    rate += SmokeSources.Funnel(c, i, full, rel, air).RateM2s;
                var p0 = SmokeSources.Funnel(c, 0, full, rel, air);
                Line(string.Format(Inv, "    her trail at full power, the air still ({0:0} m²/s, {1:0.0} m²/m): column / across at its height / 500 m along it", rate, rate / rel));
                foreach (double age in new[] { 60.0, 300, 1800 })
                {
                    var (col, across, along, z, sh, sz) = Trail(rate, rel, p0.BaseM, p0.RiseM, p0.RiseS, age, air);
                    Line(string.Format(Inv, "      {0,4:0} s (σ_h {1:0} m, σ_z {2:0} m, at {3:0} m): τ {4:0.00} / T {5:0.000} / τ {6:0.0}", age, sh, sz, z, col, Math.Exp(-across), along));
                }
            }
            Line(string.Format(Inv, "  the research's tuning, 1,500 m²/s at full power for coal: hers {0:0} m²/s ({1:0.00} of it)",
                Enumerable.Range(0, c.Funnels.Length).Sum(i => SmokeSources.Funnel(c, i, full, 10, neutral).RateM2s),
                Enumerable.Range(0, c.Funnels.Length).Sum(i => SmokeSources.Funnel(c, i, full, 10, neutral).RateM2s) / 1500));
            Line("");
        }
        Console.Write(sb.ToString());
        return 0;
    }

    /// <summary>A stretch of trail of one age laid straight along the east at 56.5° N, 4° E: the column over its axis, the
    /// optical depth across it at its height, and along 500 m of its axis there; its height and σs.</summary>
    internal static (double Column, double Across, double Along, double Z, double SigmaH, double SigmaZ) Trail(double rateM2s, double airMs, double z0,
        double rise, double riseS, double age, in SmokeAir air)
    {
        var frame = AirFrame.Identity;
        var probe = SmokeModel.State(SmokeModel.Born(Earth.Position(56.5, 4), 0, frame, z0, rise, riseS, 10, 10, 1, 0, SmokeTint.Coal), age, frame, air);
        double sh = Math.Sqrt(probe.SigmaH2), spacing = Math.Max(sh, 1), length = Math.Max(4000, 20 * sh);
        int n = (int)(length / spacing);
        double a = rateM2s / airMs * spacing;
        var mid = Earth.Position(56.5, 4);
        var east = Earth.East(mid);
        double col = 0, across = 0, along = 0;
        var cross = new SmokeLine(mid + Earth.North(mid) * 3000, mid - Earth.North(mid) * 3000, probe.Z, probe.Z, 7.0 / 6);
        var axis = new SmokeLine(mid - east * 250, mid + east * 250, probe.Z, probe.Z, 7.0 / 6);
        for (int i = 0; i < n; i++)
        {
            var at = mid + east * ((i - n / 2) * spacing);
            var p = SmokeModel.State(SmokeModel.Born(at, 0, frame, z0, rise, riseS, 10, 10, a, 0, SmokeTint.Coal), age, frame, air);
            col += SmokeModel.TauColumn(p, mid);
            across += SmokeModel.TauLine(cross, p);
            along += SmokeModel.TauLine(axis, p);
        }
        return (col, across, along, probe.Z, sh, Math.Sqrt(probe.SigmaZ2));
    }
}
