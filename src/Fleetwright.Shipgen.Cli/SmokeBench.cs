using System.Diagnostics;
using System.Globalization;
using System.Text;
using Fleetwright.Sim;
using Fleetwright.Sim.Missions;
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

    public static int Run(IReadOnlyList<string> args, Func<string, ShipClass> classOf, string scenario, string settingsPath, string designs,
        double minutes, double every, double speed, bool? smoke = null)
    {
        if (args.Count >= 1 && args[0] == "run")
            return Store(args.Count >= 2 ? args[1] : scenario, settingsPath, designs, minutes, every, speed);
        if (args.Count >= 1 && args[0] == "pileup")
            return Pileup(classOf(args.Count >= 2 ? args[1] : "dreadnought"), settingsPath, minutes);
        if (args.Count >= 1 && args[0] == "contacts")
            return Contacts(args.Count >= 2 ? args[1] : scenario, settingsPath, designs, minutes, smoke);
        if (args.Count >= 1 && args[0] == "screen")
            return Screen(classOf(args.Count >= 2 ? args[1] : "dreadnought"), classOf(args.Count >= 3 ? args[2] : "destroyer"), settingsPath, minutes);
        if (args.Count < 2 || args[0] != "facts")
        {
            Console.Error.WriteLine("shipgen smoke facts DESIGN...  |  shipgen smoke run [SCENARIO] [--minutes N] [--every N] [--speed X]  |  shipgen smoke pileup [DESIGN]  |  shipgen smoke screen [LINE] [SCREEN] [--minutes N]  |  shipgen smoke contacts [SCENARIO] [--minutes N]");
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
            // her guns' smoke a mount's salvo (Z4), and a fire's for scale
            for (int bi = 0; bi < c.Batteries.Length; bi++)
            {
                var mounts = c.Links(c.Batteries[bi].Mounts);
                if (mounts.Length == 0)
                    continue;
                var flash = MuzzleFlash.Of(c, mounts[0]);
                var g = SmokeSources.Gun(flash, flash.Guns, 10);
                var tt = c.TurretTypes[c.Mounts[mounts[0]].Type];
                var pr = c.Charges[c.Mounts[mounts[0]].Type].Propellant;
                Line(string.Format(Inv, "  battery {0} ({1:0} mm, {2} mounts; propellant solids {3:0.000}, {4:0.0} m²/g): a mount's salvo {5:0} m² ({6:0.0} kg of charges), its core σ {7:0.0} m, {8:0} m out along the bore, rising {9:0} m over {10:0.0} s",
                    bi, tt.CalibreMm, mounts.Length, pr.Solids, pr.ExtinctionM2g, g.AreaM2, flash.ChargeKg, g.SigmaM, g.AlongM, g.RiseM, g.RiseS));
            }
            foreach (double mw in new[] { 2.0, 10, 50 })
            {
                var f = SmokeSources.Fire(mw, 0, BurnPhase.Developed, 3, 8, neutral);
                Line(string.Format(Inv, "  a developed fire of {0:0} MW (any ship): {1:0} m²/s, flames {2:0.0} m, rising {3:0} m over {4:0} s in 8 m/s", mw, f.RateM2s, f.FlameM, f.RiseM, f.RiseS));
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

    /// <summary>`shipgen smoke pileup`: the pile-up case (docs/battle/smoke.md Z2): two sides of 20 ships in a 5 km box,
    /// each circling (rudder held), all in sight of the other side, the field on: the costs a game second once grown.</summary>
    static int Pileup(ShipClass c, string settingsPath, double minutes)
    {
        var settings = SimSettings.Load(settingsPath) with { Smoke = true, RailsEnabled = false };
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, settings);
        int k = w.Ships.AddClass(c);
        w.Apply(Command.SetWeather(25_000, 225, 4));
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < 20; i++)
            {
                double lat = 56.5 + (i % 5) * 0.009 + side * 0.002, lon = 4.0 + (i / 5) * 0.018 + side * 0.009;
                w.Apply(ShipSystem.Spawn(k, lat, lon, (i * 37 + side * 90) % 360, 15, (byte)side));
                w.Apply(ShipSystem.Rudder(side * 20 + i + 1, side == 0 ? 10 : -10));
            }
        w.AdvanceTo(w.Time + (long)(minutes * 60_000));   // grown
        long Ticks(string name)
        {
            int i = Array.IndexOf(w.Probes.Names, name);
            return i >= 0 ? w.Probes.Ticks[i] : 0;
        }
        long f0 = Ticks(nameof(SmokeSystem)), i0 = Ticks("Smoke index"), q0 = Ticks("Smoke queries"), l0 = Ticks("Sighting");
        long asked0 = w.Smoke.Asked, worked0 = w.Smoke.Worked, summed0 = w.Smoke.PuffsSummed, tested0 = w.Smoke.PuffsTested;
        const double game = 600;
        w.AdvanceTo(w.Time + (long)(game * 1000));
        long worked = w.Smoke.Worked - worked0;
        Console.WriteLine($"{c}: 2 x 20 circling in a 5 km box, {minutes:0} min grown then 10 min measured");
        Console.WriteLine(string.Format(Inv, "  {0} puffs in {1} trails, {2} chunks; {3:0} lines asked and {4:0} worked out a game s, {5:0.0} µs and {6:0} puffs tested, {7:0} summed each",
            w.Smoke.Count, w.Smoke.Trails, w.Smoke.Chunks, (w.Smoke.Asked - asked0) / game, worked / game,
            worked > 0 ? Probes.Ms(Ticks("Smoke queries") - q0) * 1000 / worked : 0, worked > 0 ? (double)(w.Smoke.PuffsTested - tested0) / worked : 0,
            worked > 0 ? (double)(w.Smoke.PuffsSummed - summed0) / worked : 0));
        Console.WriteLine(string.Format(Inv, "  ms a game s: store {0:0.000}, index {1:0.000}, queries {2:0.000}; the looks with them {3:0.000}",
            Probes.Ms(Ticks(nameof(SmokeSystem)) - f0 - (Ticks("Smoke index") - i0)) / game, Probes.Ms(Ticks("Smoke index") - i0) / game,
            Probes.Ms(Ticks("Smoke queries") - q0) / game, Probes.Ms(Ticks("Sighting") - l0) / game));
        return 0;
    }

    /// <summary>The lines the looks asked along in the last two seconds (docs/battle/smoke.md Z3), by the observer's side
    /// and the way she looks against the wind (down it: where the smoke goes; across; up it): how many, how many have smoke
    /// at the target's middle band (τ over 0.1, 1 and 3) and its mean transmission there, and for the observer's highest
    /// eye, of those it makes out without the smoke, how many it loses to it and the median of the margin it keeps.</summary>
    static IEnumerable<string> Sight(World w)
    {
        if (!w.Smoke.On || w.Smoke.Pairs == 0)
            yield break;
        var store = w.Ships.Store;
        var settings = w.Settings;
        long now = w.Time.Ms;
        double windTo = w.Smoke.Air.WindToDeg;
        var rows = new List<(int Side, int Way, double Tau, double Off, double On, double Km)>();
        var sample = new List<(int O, int T)>();
        // the lag (the index's states as of its rebuild, a pair's answer as of its working out): the same pair on every
        // puff as it is now, through the same lines
        var nowPuffs = new List<LinePuff>(w.Smoke.Count);
        for (int i = 0; i < w.Smoke.Slots; i++)
            if (w.Smoke.IsLive(i))
            {
                var st = w.Smoke.State(i, now / 1000.0);
                nowPuffs.Add(new LinePuff(st, w.Smoke.Frame.ToAir(st.Centre.Normalized() * Earth.Radius), w.Smoke.Frame.ToAir(st.Axis)));
            }
        var nowArray = nowPuffs.ToArray();
        var exact = new double[SmokeSystem.PairTaus];
        int lagLines = 0, lagFlips = 0, lagLost = 0, lagFound = 0;
        var lagErr = new List<double>();
        for (int q = 0; q < w.Smoke.Pairs; q++)
        {
            var (oe, te, used) = w.Smoke.PairAt(q);
            int o = store.SlotOf(oe), t = store.SlotOf(te);
            if (used < now - 2 * settings.SightingMs || o < 0 || t < 0)
                continue;
            var os = store[o];
            var ts = store[t];
            var eyes = os.Handling.Class.Eyes;
            if (eyes.Length == 0)
                continue;
            double d = Earth.Distance(os.Position, ts.Position), brg = Earth.Bearing(os.Position, ts.Position);
            double off = Math.Abs(brg - windTo) % 360, way = Math.Min(off, 360 - off);
            var line = new SightLine(d, Earth.Bearing(ts.Position, os.Position) - ts.HeadingDeg, w.Weather.At(os.Position, w.Time).VisibilityM,
                Light.SunElevationDeg(os.Position, w.Time), false);
            double mag = SightModel.Magnification(eyes[0], settings);
            var tau = SmokeModel.EyeTau(w.Smoke.PairDepths(q), eyes, eyes[0].X, eyes[0].Y, eyes[0].HeightM);
            rows.Add((os.Side, way < 45 ? 0 : way > 135 ? 2 : 1, tau.Mid, SightModel.Margin(eyes[0].HeightM, mag, ts.Handling.Class, line, settings),
                SightModel.Margin(eyes[0].HeightM, mag, ts.Handling.Class, line with { Smoke = tau }, settings), d / 1000));
            if (tau.Mid > 0.1 && sample.Count < 40)
                sample.Add((o, t));
            if (lagLines < 200)
            {
                w.Smoke.TauOf(w, o, t, nowArray, settings.SmokeLineSplit, exact);
                var fresh = SmokeModel.EyeTau(exact, eyes, eyes[0].X, eyes[0].Y, eyes[0].HeightM);
                bool seenLag = SightModel.Margin(eyes[0].HeightM, mag, ts.Handling.Class, line with { Smoke = tau }, settings) >= 1;
                bool seenNow = SightModel.Margin(eyes[0].HeightM, mag, ts.Handling.Class, line with { Smoke = fresh }, settings) >= 1;
                lagLines++;
                lagFlips += seenLag != seenNow ? 1 : 0;
                lagLost += !seenLag && seenNow ? 1 : 0;
                lagFound += seenLag && !seenNow ? 1 : 0;
                if (tau.Mid > 0.05 || fresh.Mid > 0.05)
                    lagErr.Add(Math.Abs(tau.Mid - fresh.Mid));
            }
        }
        string[] ways = ["down the wind", "across it", "up it"];
        yield return "           the looks' lines (the last 2 s), the observer's side and her look against the wind: lines, τ at the middle band over 0.1 / 1 / 3, mean T there; made out by her highest eye without the smoke, lost to it, the margin kept (median)";
        foreach (var g in rows.GroupBy(r => (r.Side, r.Way)).OrderBy(g => g.Key))
        {
            var seen = g.Where(r => r.Off >= 1).ToList();
            var kept = seen.Select(r => r.On / r.Off).OrderBy(x => x).ToList();
            yield return string.Format(Inv, "             side {0} {1,-14} {2,5} lines   {3,4:0}% {4,4:0}% {5,4:0}%   T {6:0.00}   made out {7,4}, lost {8,4} ({9,3:0}%), kept {10:0.00}",
                g.Key.Side, ways[g.Key.Way], g.Count(), 100.0 * g.Count(r => r.Tau > 0.1) / g.Count(), 100.0 * g.Count(r => r.Tau > 1) / g.Count(),
                100.0 * g.Count(r => r.Tau > 3) / g.Count(), g.Average(r => Math.Exp(-r.Tau)), seen.Count, seen.Count(r => r.On < 1),
                seen.Count > 0 ? 100.0 * seen.Count(r => r.On < 1) / seen.Count : 0, kept.Count > 0 ? kept[kept.Count / 2] : 1);
        }
        // by range (the record's: Jutland's climax at 9-15 kyd, 8-14 km): the lines her highest eye makes out in clear air,
        // and of them those she still does through the smoke
        double[] edges = [0, 8, 14, 20, double.MaxValue];
        var byRange = new System.Text.StringBuilder("           by range, made out in clear air / still through the smoke:");
        for (int r = 0; r < 4; r++)
        {
            var band = rows.Where(x => x.Km >= edges[r] && x.Km < edges[r + 1] && x.Off >= 1).ToList();
            byRange.Append(string.Format(Inv, " {0}-{1} km {2}/{3};", edges[r], r == 3 ? "" : edges[r + 1].ToString(Inv), band.Count, band.Count(x => x.On >= 1)));
        }
        yield return byRange.ToString();
        lagErr.Sort();
        yield return string.Format(Inv, "           the lag (the index {0:0} s, a pair's answer {1:0} s): of {2} lines, {3} ({4:0.0}%) seen otherwise with every puff as it is now ({5} lost to the lag's smoke, {6} seen through smoke since come); |Δτ| at her middle on the {7} smoky ones: median {8:0.00}, 90th {9:0.00}",
            settings.SmokeIndexMs / 1000.0, settings.SmokeRefreshMs / 1000.0, lagLines, lagFlips, lagLines > 0 ? 100.0 * lagFlips / lagLines : 0, lagLost, lagFound,
            lagErr.Count, lagErr.Count > 0 ? lagErr[lagErr.Count / 2] : 0, lagErr.Count > 0 ? lagErr[lagErr.Count * 9 / 10] : 0);
        // whose smoke it is, on a sample of the smoky lines: every puff summed on the highest eye's line to the middle band
        if (sample.Count == 0)
            yield break;
        var by = new double[5];
        double nowS = now / 1000.0;
        Span<double> one = stackalloc double[1];
        foreach (var (o, t) in sample)
        {
            var os = store[o];
            var ts = store[t];
            var a = w.Smoke.Frame.ToAir(os.Position.Normalized() * Earth.Radius);
            var b = w.Smoke.Frame.ToAir(ts.Position.Normalized() * Earth.Radius);
            double from = os.Handling.Class.Eyes[0].HeightM, to = settings.SmokeBands[1] * ts.Handling.Class.Silhouette.TopM;
            var line = new SmokeLine(a, b, from, to, settings.SightRefraction);
            for (int i = 0; i < w.Smoke.Slots; i++)
            {
                if (!w.Smoke.IsLive(i))
                    continue;
                int e = w.Smoke.EntityOf(i), slot = store.SlotOf(e);
                int k = e == os.Entity ? 0 : e == ts.Entity ? 1 : slot < 0 ? 4 : store[slot].Side == os.Side ? 2 : 3;
                var st = SmokeModel.State(w.Smoke.Puff(i), nowS, w.Smoke.Frame, w.Smoke.Air);
                st = st with { Centre = w.Smoke.Frame.ToAir(st.Centre.Normalized() * Earth.Radius) };
                one[0] = 0;
                SmokeModel.TauGrid(line, st, [from], [to], one);
                by[k] += one[0];
            }
        }
        double sum = Math.Max(by.Sum(), 1e-12);
        yield return string.Format(Inv, "           whose smoke, on {0} smoky lines (mean τ {1:0.00} at the middle band): her own {2:0}%, the target's {3:0}%, her side's {4:0}%, the target's side's {5:0}%, ships gone {6:0}%",
            sample.Count, sum / sample.Count, 100 * by[0] / sum, 100 * by[1] / sum, 100 * by[2] / sum, 100 * by[3] / sum, 100 * by[4] / sum);
    }

    /// <summary>`shipgen smoke screen`: the USNI case (docs/battle/smoke.md Z4): a battle line of three steaming north at
    /// 15 kn, a target of the same class 13 km to the east on the same course, and four destroyers of hers in line
    /// between, 5 km off the battle line, at 25 kn, the wind from the east at 5 m/s (blowing the screen toward the line).
    /// Run for so many minutes with the field off, on, and on with the destroyers making smoke: how long the target was
    /// in the line's navy's sight, and the destroyers; the depth on the line from the guide's highest eye to her middle at
    /// the end.</summary>
    static int Screen(ShipClass line, ShipClass screen, string settingsPath, double minutes)
    {
        var baseSettings = SimSettings.Load(settingsPath) with { RailsEnabled = false };
        Console.WriteLine(string.Format(Inv, "{0} x 3 against one 13 km east, {1} x 4 screening her 5 km off the line at 25 kn, the wind from the east at 5 m/s; {2:0} min",
            line, screen, minutes));
        Span<double> taus = stackalloc double[SmokeSystem.PairTaus];
        foreach (var (name, smoke, screening) in new[] { ("the field off", false, false), ("the field on", true, false), ("making smoke", true, true) })
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 7, baseSettings with { Smoke = smoke });
            int a = w.Ships.AddClass(line), b = w.Ships.AddClass(screen);
            w.Apply(Command.SetWeather(25_000, 90, 5));
            for (int i = 0; i < 3; i++)
                w.Apply(ShipSystem.Spawn(a, 56.5 - 0.005 * i, 4.0, 0, 15, 0));
            w.Apply(ShipSystem.Spawn(a, 56.5, 4.0 + 13_000 / (111_320 * Math.Cos(56.5 * Math.PI / 180)), 0, 15, 1));
            for (int i = 0; i < 4; i++)
                w.Apply(ShipSystem.Spawn(b, 56.5 + 0.01 - 0.009 * i, 4.0 + 5_000 / (111_320 * Math.Cos(56.5 * Math.PI / 180)), 0, 25, 1));
            if (screening)
                for (int e = 5; e <= 8; e++)
                    w.Apply(SmokeSystem.MakeSmoke(e, true));
            int target = 0, screens = 0, seconds = (int)(minutes * 60);
            for (int t = 0; t < seconds; t++)
            {
                w.AdvanceTo(w.Time + 1000);
                var pic = w.Sightings.Of(0);
                if (pic == null)
                    continue;
                target += pic.IndexOf(4) is int i and >= 0 && pic[i].InSight ? 1 : 0;
                for (int e = 5; e <= 8; e++)
                    screens += pic.IndexOf(e) is int j and >= 0 && pic[j].InSight ? 1 : 0;
            }
            double tau = double.NaN;
            if (smoke)
            {
                w.Smoke.TauUncached(w, w.Ships.Store.SlotOf(1), w.Ships.Store.SlotOf(4), taus);
                tau = taus[1];
            }
            Console.WriteLine(string.Format(Inv, "  {0,-14} the target in sight {1,4} s of {2}, the destroyers {3,5:0.0}% of theirs; τ the guide's highest eye to her middle {4:0.00}; {5} puffs",
                name, target, seconds, 100.0 * screens / (4 * seconds), tau, w.Smoke.Count));
        }
        return 0;
    }

    /// <summary>`shipgen smoke run`: a scenario headless with the field on (docs/battle/smoke.md Z1), in a world of water,
    /// the clock held at its speed through contact: every so many game minutes the puffs and trails, the store's memory, the
    /// puffs by age, what was laid, merged and retired, and the field's cost a game second beside the whole sim's.</summary>
    /// <summary>`shipgen smoke contacts`: smoke on the horizon on a scenario (docs/battle/smoke.md Z6), the AI on every
    /// side, a world of water, looked at every 10 s of game time: for each enemy ship a side comes to hold, how far her
    /// smoke was first made out (from that side's nearest ship) and how far she herself was first seen, by side; the
    /// contacts held by smoke alone over time; the ships off rails.</summary>
    static int Contacts(string path, string settingsPath, string designs, double minutes, bool? smoke)
    {
        var settings = SimSettings.Load(settingsPath);
        if (smoke is bool on)
            settings = settings with { Smoke = on };
        var s = Scenario.Load(path);
        using var registry = new ShipClassRegistry(0);
        var classes = ScenarioLoader.RequestClasses(s, registry, [designs]);
        registry.WaitAll();
        var w = new World(s.Start, s.Seed, settings, registry);
        var ai = new Fleetwright.Sim.Ai.AiSystem(w);
        ScenarioLoader.Apply(s, w, ai, classes);
        w.Apply(Command.SetInterest(InterestKind.Own, InterestAction.Nothing));
        w.Apply(Command.SetInterest(InterestKind.Others, InterestAction.Nothing));
        Console.WriteLine($"{s.Name}: {s.ShipCount} ships, the field {(settings.Smoke ? "on" : "off")}; reach {w.Reach.SightM / 1000:0.0} km (smoke's top {w.Reach.SmokeTopM:0} m)");
        // per (side, entity): first smoke (range, min) and first sight of her
        var smokeAt = new Dictionary<(byte, int), (double Km, double Min)>();
        var shipAt = new Dictionary<(byte, int), (double Km, double Min)>();
        var store = w.Ships.Store;
        double Nearest(byte side, int entity)
        {
            int t = store.SlotOf(entity);
            double best = double.MaxValue;
            for (int i = 0; i < store.Slots && t >= 0; i++)
                if (store.IsAlive(i) && store[i].Side == side)
                    best = Math.Min(best, Earth.Distance(store[i].Position, store[t].Position));
            return best / 1000;
        }
        var wall = Stopwatch.StartNew();
        int every = 10;
        for (int step = 1; step * every <= minutes * 60; step++)
        {
            w.AdvanceTo(new WorldTime(s.Start.Ms + step * every * 1000L));
            double min = step * every / 60.0;
            foreach (var pic in w.Sightings.All)
                for (int i = 0; i < pic.Count; i++)
                {
                    var key = (pic.Side, pic[i].Entity);
                    if (pic[i].Level == ContactLevel.Smoke && !smokeAt.ContainsKey(key) && !shipAt.ContainsKey(key))
                        smokeAt[key] = (Nearest(pic.Side, pic[i].Entity), min);
                    if (pic[i].Level >= ContactLevel.Ship && !shipAt.ContainsKey(key))
                        shipAt[key] = (Nearest(pic.Side, pic[i].Entity), min);
                }
            if (step * every % 600 == 0)
            {
                int offRails = store.OffRailsCount;
                var line = new StringBuilder(string.Format(Inv, "  {0,5:0} min: {1} ships off rails; held", min, offRails));
                foreach (var pic in w.Sightings.All)
                {
                    int bySmoke = 0, smokeOnly = 0, inSight = 0;
                    for (int i = 0; i < pic.Count; i++)
                    {
                        inSight += pic[i].InSight ? 1 : 0;
                        bySmoke += pic[i].SmokeInSight ? 1 : 0;
                        smokeOnly += pic[i].Level == ContactLevel.Smoke ? 1 : 0;
                    }
                    line.Append(string.Format(Inv, " | side {0}: {1} contacts, {2} in sight, {3} by their smoke, {4} smoke only", pic.Side, pic.Count, inSight, bySmoke, smokeOnly));
                }
                Console.WriteLine(line + string.Format(Inv, "; {0:0.0} s wall", wall.Elapsed.TotalSeconds));
            }
        }
        foreach (var side in w.Sightings.All.Select(p => p.Side).OrderBy(x => x))
        {
            var bySmoke = smokeAt.Where(kv => kv.Key.Item1 == side).ToList();
            var ships = shipAt.Where(kv => kv.Key.Item1 == side).ToList();
            var lead = bySmoke.Where(kv => shipAt.ContainsKey(kv.Key)).Select(kv => shipAt[kv.Key].Min - kv.Value.Min).ToList();
            Console.WriteLine(string.Format(Inv, "  side {0}: {1} ships first held by their smoke, at {2} km; {3} first seen as ships, at {4} km; smoke first by {5} min (of {6} seen after)",
                side, bySmoke.Count, Quartiles(bySmoke.Select(kv => kv.Value.Km)), ships.Count, Quartiles(ships.Select(kv => kv.Value.Km)), Quartiles(lead), lead.Count));
        }
        return 0;

        static string Quartiles(IEnumerable<double> xs)
        {
            var v = xs.OrderBy(x => x).ToList();
            if (v.Count == 0)
                return "-";
            double Q(double q) => v[(int)Math.Round(q * (v.Count - 1))];
            return string.Format(Inv, "{0:0.0} / {1:0.0} / {2:0.0}", Q(0.25), Q(0.5), Q(0.75));
        }
    }

    static int Store(string path, string settingsPath, string designs, double minutes, double every, double speed)
    {
        var settings = SimSettings.Load(settingsPath) with { Smoke = Environment.GetEnvironmentVariable("SMOKE_OFF") != "1" };
        var s = Scenario.Load(path);
        using var registry = new ShipClassRegistry(0);
        var classes = ScenarioLoader.RequestClasses(s, registry, [designs]);
        registry.WaitAll();
        var w = new World(s.Start, s.Seed, settings, registry);
        var ai = new Fleetwright.Sim.Ai.AiSystem(w);
        ScenarioLoader.Apply(s, w, ai, classes);
        w.Apply(Command.SetInterest(InterestKind.Own, InterestAction.Nothing));
        w.Apply(Command.SetInterest(InterestKind.Others, InterestAction.Nothing));
        w.Apply(Command.SetSpeed(speed));
        int probe = Array.IndexOf(w.Probes.Names, nameof(SmokeSystem));
        Console.WriteLine($"{s.Name}: {s.ShipCount} ships, at {speed:0.#}x, a world of water; the field on");
        Console.WriteLine("  game     puffs  trails  per trail    MiB   by age (<1, 1-5, 5-15, 15-30, 30+ min)        laid   merged  retired   field ms/game s   sim ms/game s");
        var ages = new int[5];
        long lastTicks = 0, lastMg = 0, lastIx = 0, lastQy = 0, lastLk = 0, lastAsked = 0, lastWorked = 0, lastSummed = 0, lastChunks = 0, lastTested = 0;
        long lastBytes = GC.GetAllocatedBytesForCurrentThread();
        var lastGc = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        var wall = Stopwatch.StartNew();
        double lastWall = 0;
        for (double m = every; m <= minutes + 1e-9; m += every)
        {
            w.AdvanceTo(new WorldTime(s.Start.Ms + (long)(m * 60_000)));
            Array.Clear(ages);
            double now = w.Time.Ms / 1000.0;
            for (int i = 0; i < w.Smoke.Slots; i++)
                if (w.Smoke.IsLive(i))
                {
                    double age = (now - w.Smoke.Puff(i).Born) / 60;
                    ages[age < 1 ? 0 : age < 5 ? 1 : age < 15 ? 2 : age < 30 ? 3 : 4]++;
                }
            long ticks = probe >= 0 ? w.Probes.Ticks[probe] : 0;
            double game = every * 60, field = Probes.Ms(ticks - lastTicks) / game, all = (wall.Elapsed.TotalMilliseconds - lastWall) / game;
            Console.WriteLine(string.Format(Inv, "  {0,5:0} min {1,7} {2,7} {3,10:0} {4,6:0.0}   {5,6} {6,6} {7,6} {8,6} {9,6}   {10,10} {11,8} {12,8}   {13,15:0.000} {14,15:0.00}",
                m, w.Smoke.Count, w.Smoke.Trails, w.Smoke.Trails > 0 ? (double)w.Smoke.Count / w.Smoke.Trails : 0, w.Smoke.Bytes / 1048576.0,
                ages[0], ages[1], ages[2], ages[3], ages[4], w.Smoke.Laid, w.Smoke.Merged, w.Smoke.Retired, field, all));
            (lastTicks, lastWall) = (ticks, wall.Elapsed.TotalMilliseconds);
            // the sight lines (Z2): asked and worked out a game second, their cost, the index's, and the looks' with them
            long ix = Ticks("Smoke index"), qy = Ticks("Smoke queries"), lk = Ticks("Sighting"), mg = Ticks("Smoke merge");
            long asked = w.Smoke.Asked - lastAsked, worked = w.Smoke.Worked - lastWorked, summed = w.Smoke.PuffsSummed - lastSummed;
            Console.WriteLine(string.Format(Inv, "           lines: {0,6:0} asked, {1,6:0} worked out a game s; {2,5:0.0} µs and {3,4:0} puffs each; merging {9:0.000} ({10} across so far), index {4:0.000}, queries {5:0.000}, the looks with them {6:0.000} ms/game s ({7} puffs indexed in {8} chunks)",
                asked / game, worked / game, worked > 0 ? Probes.Ms(qy - lastQy) * 1000 / worked : 0, worked > 0 ? (double)summed / worked : 0,
                Probes.Ms(ix - lastIx) / game, Probes.Ms(qy - lastQy) / game, Probes.Ms(lk - lastLk) / game, w.Smoke.Indexed, w.Smoke.Chunks,
                Probes.Ms(mg - lastMg) / game, w.Smoke.MergedAcross));
            lastMg = mg;
            Console.WriteLine(string.Format(Inv, "           tested a line: {0:0} chunks, {1:0} puffs", worked > 0 ? (double)(w.Smoke.ChunksTested - lastChunks) / worked : 0,
                worked > 0 ? (double)(w.Smoke.PuffsTested - lastTested) / worked : 0));
            (lastChunks, lastTested) = (w.Smoke.ChunksTested, w.Smoke.PuffsTested);
            (lastIx, lastQy, lastLk, lastAsked, lastWorked, lastSummed) = (ix, qy, lk, w.Smoke.Asked, w.Smoke.Worked, w.Smoke.PuffsSummed);
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            Console.WriteLine(string.Format(Inv, "           this thread allocated {0:0.0} KB a game s; collections {1} / {2} / {3}",
                (bytes - lastBytes) / 1024.0 / game, GC.CollectionCount(0) - lastGc[0], GC.CollectionCount(1) - lastGc[1], GC.CollectionCount(2) - lastGc[2]));
            (lastBytes, lastGc[0], lastGc[1], lastGc[2]) = (bytes, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            foreach (var line in Sight(w))
                Console.WriteLine(line);
        }
        // the drawing's copy (Z5): a snapshot's fill with it (the first, the field changed) and without (the next, within
        // the copy's interval), on the sim's thread
        if (w.Smoke.On)
        {
            var snap = new Snapshot();
            w.Fill(snap);   // warmed: the snapshot's arrays grown, the copy's too
            w.AdvanceTo(w.Time + 1000);
            System.Threading.Thread.Sleep(settings.SmokeCopyMs + 50);
            var sw0 = Stopwatch.StartNew();
            w.Fill(snap);
            double with = sw0.Elapsed.TotalMilliseconds;
            sw0.Restart();
            w.Fill(snap);
            double without = sw0.Elapsed.TotalMilliseconds;
            snap.SmokeCopies!.Acquire();
            Console.WriteLine(string.Format(Inv, "  the drawing's copy: {0} puffs, {1:0.00} ms on the sim's thread (a fill {2:0.00} ms with it, {3:0.00} without); every {4} ms of real time at most",
                snap.SmokeCopies.Front.Count, with - without, with, without, settings.SmokeCopyMs));
        }
        // one pair worked out again and again, uncached: the query's own cost
        var store = w.Ships.Store;
        int o = -1, tg = -1;
        for (int i = 0; i < store.Slots && tg < 0; i++)
            for (int j = 0; j < store.Slots && store.IsAlive(i); j++)
                if (store.IsAlive(j) && store[i].Side != store[j].Side && Earth.Distance(store[i].Position, store[j].Position) < 20_000)
                {
                    (o, tg) = (i, j);
                    break;
                }
        if (tg >= 0)
        {
            Span<double> tau = stackalloc double[SmokeSystem.PairTaus];
            long tested0 = w.Smoke.PuffsTested, summed0 = w.Smoke.PuffsSummed;
            for (int i = 0; i < 1000; i++)
                w.Smoke.TauUncached(w, o, tg, tau);
            var sw = Stopwatch.StartNew();
            const int N = 10_000;
            for (int i = 0; i < N; i++)
                w.Smoke.TauUncached(w, o, tg, tau);
            Console.WriteLine(string.Format(Inv, "  one pair {0:0.0} km apart, uncached: {1:0.00} µs a query, {2:0} puffs tested and {3:0} summed; τ eye 1 to the bands {4:0.000} {5:0.000} {6:0.000}",
                Earth.Distance(store[o].Position, store[tg].Position) / 1000, sw.Elapsed.TotalMilliseconds * 1000 / N,
                (double)(w.Smoke.PuffsTested - tested0) / (N + 1000), (double)(w.Smoke.PuffsSummed - summed0) / (N + 1000), tau[0], tau[1], tau[2]));
        }
        return 0;

        long Ticks(string name)
        {
            int i = Array.IndexOf(w.Probes.Names, name);
            return i >= 0 ? w.Probes.Ticks[i] : 0;
        }
    }
}
