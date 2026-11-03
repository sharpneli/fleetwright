using System.Globalization;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen roll</c> (docs/battle/balance.md BL1a): ships' roll in a sea (<see cref="RollSea"/>), by heading
/// to the waves and speed: the significant amplitude, the peak roll rate (2 sigma) and the mean period, beside each
/// class's GM, natural period and damping. The sea is a weather's (a wind and a swell), or a sea state's: the wind that
/// raises its height fully developed (Pierson-Moskowitz: H_s = 0.21 U²/g), no swell, as the research's table has it
/// (research/simulation/ship-motion/ship-motion-gunnery-research.md §3.2). <c>--developed</c> lets the wind blow over an
/// unlimited fetch (the spectrum capped at Pierson-Moskowitz, the research's own seas) where the world's is fetch-limited
/// at 150 km under a gale (docs/battle/balance.md BL1k: a heavy sea is compared with the record like for like).
/// <c>--wet</c> adds the sea coming aboard (docs/battle/error-sources.md ES4, <see cref="SeaAboard"/>): the share of the
/// time each of her guns is out to green water and each glass (a layer's sight, an instrument) to green water or spray,
/// the points that are ever out.</summary>
static class RollBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The research's sea states' significant heights, m (§3.2's table), from 3.</summary>
    static readonly double[] SeaStateHs = [0.9, 1.9, 3.25, 5, 7.5];

    public static int Run(IReadOnlyList<string> names, Func<string, ShipClass> cls, SimSettings settings, int seaState, double windKn, double windFrom,
        double swellM, double swellS, double swellFrom, double[] speedsKn, bool developed = false, bool wet = false, bool held = false)
    {
        WeatherHere w;
        if (seaState >= 3)
        {
            double hs = SeaStateHs[Math.Min(seaState, 7) - 3], u = Math.Sqrt(hs * SeaSpectrum.G / 0.21);
            w = new WeatherHere(25_000, 0, u, SwellHsM: 0);
            Console.WriteLine(string.Format(Inv, "sea state {0}: a wind of {1:0.0} m/s ({2:0} kn) from 0°, no swell", seaState, u, u / 0.514444));
        }
        else
        {
            w = new WeatherHere(25_000, windFrom, windKn * 0.514444, SwellHsM: swellM, SwellTpS: swellS, SwellFromDeg: swellFrom);
            Console.WriteLine(string.Format(Inv, "a wind of {0:0} kn from {1:0}°, a swell of {2:0.0} m at {3:0} s from {4:0}°", windKn, windFrom, swellM, swellS, w.SwellFrom));
        }
        var state = SeaState.Of(w);
        var spectrum = new SeaSpectrum(developed ? state with { FetchM = 1e12 } : state);
        var sea = new RollSea(spectrum);
        double waveFrom = seaState >= 3 ? w.WindFromDeg : w.SwellFrom;
        Console.WriteLine(string.Format(Inv, "  significant height {0:0.00} m; the wind sea's peak {1:0.0} s; headings below are to the {2} (0 head on, 90 beam on, 180 following)",
            sea.HsM, 2 * Math.PI / spectrum.PeakW, seaState >= 3 ? "wind sea" : "swell") + (developed ? "; fully developed" : ""));
        int[] rel = [0, 30, 60, 90, 120, 150, 180];
        foreach (var n in names)
        {
            var c = cls(n);
            var m = c.Motion;
            Console.WriteLine();
            Console.WriteLine(string.Format(Inv, "{0}: {1:0} m x {2:0.0} m, GM {3:0.00} m, natural period {4:0.0} s, damping {5:0.00}", c.Name, m.LengthM, m.BeamM, m.GmFullM,
                m.RollPeriodS, m.RollDamping));
            Console.WriteLine($"  {"speed",6}  " + string.Join("", rel.Select(h => $"{h + "°",18}")));
            foreach (double kn in speedsKn)
            {
                var cells = rel.Select(h =>
                {
                    // heading h off the waves' coming: the ship steers toward where they come from at 0
                    var r = sea.At((waveFrom + h) % 360, kn * 0.514444, m.LengthM, m.BeamM, m.RollPeriodS, m.RollDamping, settings.RollExcitation);
                    return string.Format(Inv, "{0,5:0.0}° {1,4:0.0}°/s {2,4:0.0}s", double.RadiansToDegrees(r.AmplitudeRad), double.RadiansToDegrees(2 * r.RateSigmaRadS), r.PeriodS);
                });
                Console.WriteLine(string.Format(Inv, "  {0,4:0} kn  ", kn) + string.Concat(cells.Select(x => $"{x,18}")));
                // her pitch at the same headings (docs/battle/balance.md BL1h)
                var pitch = rel.Select(h =>
                {
                    var r = sea.Pitch((waveFrom + h) % 360, kn * 0.514444, m.LengthM, m.BeamM, settings.PitchPeriodK * Math.Sqrt(Math.Max(m.DraughtM, 0.5)), settings.PitchDamping);
                    return string.Format(Inv, "{0,5:0.0}° {1,4:0.0}°/s {2,4:0.0}s", double.RadiansToDegrees(r.AmplitudeRad), double.RadiansToDegrees(2 * r.RateSigmaRadS), r.PeriodS);
                });
                Console.WriteLine("    pitch " + string.Concat(pitch.Select(x => $"{x,18}")));
                if (wet)
                    Wet(c, settings, sea, w, waveFrom, kn, rel);
                if (held)
                    Held(c, settings, sea, waveFrom, kn, rel);
            }
        }
        Console.WriteLine();
        Console.WriteLine("each cell: the significant roll (single amplitude), the peak roll rate (2 sigma), the mean period; the pitch the same below" +
                          (wet ? "; wet: the share of the time out, a gun's to green water, a glass's to green water or spray" : ""));
        return 0;
    }

    /// <summary>Her rangefinders' hold on a target abeam at a speed, by heading (docs/battle/error-sources.md ES5,
    /// <see cref="FireControl.Held"/>): the share of the time each make on her (its battery's fit, a turret's own) keeps her
    /// in its field as she rolls and pitches.</summary>
    static void Held(ShipClass c, SimSettings settings, RollSea sea, double waveFrom, double kn, int[] rel)
    {
        var m = c.Motion;
        var states = rel.Select(h =>
        {
            double hdg = (waveFrom + h) % 360;
            return (Roll: sea.At(hdg, kn * 0.514444, m.LengthM, m.BeamM, m.RollPeriodS, m.RollDamping, settings.RollExcitation),
                Pitch: sea.Pitch(hdg, kn * 0.514444, m.LengthM, m.BeamM, settings.PitchPeriodK * Math.Sqrt(Math.Max(m.DraughtM, 0.5)), settings.PitchDamping));
        }).ToArray();
        var makes = c.Batteries.ToArray().SelectMany(b => new[] { b.Fit.Optics, b.TurretOptics }).Where(o => o.Reads).Distinct();
        foreach (var o in makes)
        {
            var cells = states.Select(st => string.Format(Inv, "{0,5:0}%", 100 * Sim.Combat.FireControl.Held(o, st.Roll.RateSigmaRadS, st.Pitch.RateSigmaRadS, 90,
                settings.InstrumentFollowS, settings.InstrumentFieldDeg, 1)));
            Console.WriteLine(string.Format(Inv, "    held, x{0:0}{1,-27}", o.Magnification, o.Gyro ? " gyro" : "") + string.Concat(cells.Select(x => $"{x,18}")));
        }
    }

    /// <summary>Her points out to the sea at a speed, by heading (<see cref="SeaAboard"/>): each gun's share of the time out
    /// to green water and each glass's (its layer's sight on the gunhouse roof, an eye) to green water or spray.</summary>
    static void Wet(ShipClass c, SimSettings settings, RollSea sea, WeatherHere w, double waveFrom, double kn, int[] rel)
    {
        int n = SeaAboard.Points(c);
        double[] x = new double[n], y = new double[n];
        SeaAboard.Place(c, x, y);
        var m0 = new double[rel.Length][];
        var m2 = new double[rel.Length][];
        double v = kn * 0.514444;
        for (int i = 0; i < rel.Length; i++)
        {
            (m0[i], m2[i]) = (new double[n], new double[n]);
            sea.Relative((waveFrom + rel[i]) % 360, v, SeaAboard.Hull(settings, c, 1), x, y, m0[i], m2[i]);
        }
        var up = Attitude.Upright;
        string Cell(int p, double z, bool gun, int i)
        {
            double hdg = (waveFrom + rel[i]) % 360;
            double rate = SeaAboard.Green(settings, c, up, v, x[p], y[p], m0[i][p], m2[i][p], z) +
                          (gun ? 0 : SeaAboard.Spray(settings, c, up, v, hdg, w, m0[i][0], m2[i][0], x[p], y[p], z));
            return string.Format(Inv, "{0,5:0}%", 100 * SeaAboard.Out(rate, gun ? settings.GreenWaterClearS : settings.SprayClearS));
        }
        void Row(string what, int p, double z, bool gun)
        {
            var cells = Enumerable.Range(0, rel.Length).Select(i => Cell(p, z, gun, i)).ToArray();
            if (cells.Any(s => double.Parse(s.TrimEnd('%'), Inv) >= 0.5))
                Console.WriteLine(string.Format(Inv, "    {0,-34}", what + string.Format(Inv, " {0:0.0} m", SeaAboard.OverSea(c, up, x[p], y[p], z))) +
                                  string.Concat(cells.Select(s => $"{s,18}")));
        }
        Console.WriteLine(string.Format(Inv, "    wet (stem {0:0.0} m over the sea, sd {1}):", SeaAboard.OverSea(c, up, x[0], 0, c.Hull.DeckTop(x[0])),
            string.Join(" ", Enumerable.Range(0, rel.Length).Select(i => string.Format(Inv, "{0:0.00}", Math.Sqrt(m0[i][0]))))));
        for (int j = 0; j < c.Mounts.Length; j++)
        {
            int p = SeaAboard.MountPoint(j);
            string at = string.Format(Inv, "{0} {1} x{2:+0;-0} y{3:+0;-0}", c.Mounts[j].Kind, j, x[p], y[p]);
            Row(at + " guns", p, SeaAboard.GunAxis(c, j), true);
            Row(at + " sight", p, c.Components[c.Mounts[j].Component].Bounds.Top, false);
        }
        for (int e = 0; e < c.Eyes.Length; e++)
        {
            int p = SeaAboard.EyePoint(c, e);
            Row(string.Format(Inv, "{0} {1} x{2:+0;-0}", c.Eyes[e].Kind, e, x[p]), p, c.Eyes[e].HeightM + c.Vertical.Waterline, false);
        }
    }
}
