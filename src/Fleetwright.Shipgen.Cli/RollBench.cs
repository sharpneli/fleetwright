using System.Globalization;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen roll</c> (docs/battle/balance.md BL1a): ships' roll in a sea (<see cref="RollSea"/>), by heading
/// to the waves and speed: the significant amplitude, the peak roll rate (2 sigma) and the mean period, beside each
/// class's GM, natural period and damping. The sea is a weather's (a wind and a swell), or a sea state's: the wind that
/// raises its height fully developed (Pierson-Moskowitz: H_s = 0.21 U²/g), no swell, as the research's table has it
/// (research/simulation/ship-motion/ship-motion-gunnery-research.md §3.2).</summary>
static class RollBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The research's sea states' significant heights, m (§3.2's table), from 3.</summary>
    static readonly double[] SeaStateHs = [0.9, 1.9, 3.25, 5, 7.5];

    public static int Run(IReadOnlyList<string> names, Func<string, ShipClass> cls, SimSettings settings, int seaState, double windKn, double windFrom,
        double swellM, double swellS, double swellFrom, double[] speedsKn)
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
        var spectrum = new SeaSpectrum(SeaState.Of(w));
        var sea = new RollSea(spectrum);
        double waveFrom = seaState >= 3 ? w.WindFromDeg : w.SwellFrom;
        Console.WriteLine(string.Format(Inv, "  significant height {0:0.00} m; the wind sea's peak {1:0.0} s; headings below are to the {2} (0 head on, 90 beam on, 180 following)",
            sea.HsM, 2 * Math.PI / spectrum.PeakW, seaState >= 3 ? "wind sea" : "swell"));
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
                    var r = sea.At((waveFrom + h) % 360, kn * 0.514444, m.RollPeriodS, m.RollDamping, settings.RollExcitation);
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
            }
        }
        Console.WriteLine();
        Console.WriteLine("each cell: the significant roll (single amplitude), the peak roll rate (2 sigma), the mean period; the pitch the same below");
        return 0;
    }
}
