using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>A ship's roll in a sea, as gunnery reads it (docs/battle/balance.md BL1a): the significant amplitude (radians,
/// single), the spread of her roll rate (1 sigma, rad/s) and her mean period (s), the zero crossings'.</summary>
public readonly record struct RollState(double AmplitudeRad, double RateSigmaRadS, double PeriodS)
{
    /// <summary>The roll rate as she passes upright on one roll, rad/s: Rayleigh about <see cref="RateSigmaRadS"/> (a
    /// narrow-band roll's rate at its zero crossings), drawn from the caller's stream.</summary>
    public double RateAtLevel(ref RandomStream r) => RateSigmaRadS * Math.Sqrt(-2 * Math.Log(1 - r.NextDouble()));
}

/// <summary>The sea a world's ships roll in (docs/battle/balance.md BL1a;
/// research/simulation/ship-motion/ship-motion-gunnery-research.md §3.2, its own method rather than its table): the
/// weather's directional spectrum (<see cref="SeaSpectrum"/>, the one the map draws) as wave-slope variance by frequency
/// and direction, built once per weather. A ship's roll is the linear one-degree-of-freedom response to it: each component
/// met at its encounter frequency (her heading and speed to it), its slope taken by her beam-on share (sin of the angle
/// between it and her heading) and an excitation coefficient (<see cref="SimSettings.RollExcitation"/>), magnified by her
/// resonance (natural period, damping). Short-crestedness comes with the spread; a swell near her period rolls her hard,
/// a short wind sea only a short-period ship. A pure function of the sea and her state, so nothing is stepped and it's the
/// same at any compression; a few µs a call, no garbage.</summary>
public sealed class RollSea
{
    /// <summary>The grid: wave frequencies log-spaced over periods of about 1.8 to 42 s (the swell's narrow peak and the
    /// short wind sea a destroyer feels), and directions every 5° (the swell's spread is about ±15°).</summary>
    public const int Frequencies = 32, Directions = 72;
    const double WLow = 0.15, WHigh = 3.5;

    readonly double[] w = new double[Frequencies], cos = new double[Directions], sin = new double[Directions];
    /// <summary>Wave-slope variance a cell, (ω²/g)²·S(ω, θ)·dω·dθ, by frequency then direction.</summary>
    readonly double[] slope = new double[Frequencies * Directions];

    /// <summary>The significant wave height, m (wind sea and swell).</summary>
    public double HsM { get; }

    /// <summary>A number of its own among the seas built (a cache's key: a battery's roll is worked out again for a new
    /// sea); it never enters a result.</summary>
    public int Id { get; } = Interlocked.Increment(ref built) & 0x7F_FFFF;
    static int built;

    /// <summary>The way the bigger of the wind sea and the swell comes from, degrees true (the seas a ship's casemates take
    /// on her weather side: <see cref="Awash"/>).</summary>
    public double FromDeg { get; }

    public RollSea(SeaSpectrum spectrum)
    {
        HsM = spectrum.HsTarget;
        var st = spectrum.State;
        FromDeg = spectrum.WindSeaM0 >= Math.Pow(Math.Max(st.SwellHsM, 0) / 4, 2) ? st.WindFromDeg : st.SwellFromDeg;
        double dt = 2 * Math.PI / Directions, ratio = Math.Pow(WHigh / WLow, 1.0 / Frequencies);
        for (int j = 0; j < Directions; j++)
            (cos[j], sin[j]) = (Math.Cos(j * dt), Math.Sin(j * dt));
        for (int i = 0; i < Frequencies; i++)
        {
            double lo = WLow * Math.Pow(ratio, i), hi = lo * ratio, wi = Math.Sqrt(lo * hi), k = wi * wi / SeaSpectrum.G;
            w[i] = wi;
            for (int j = 0; j < Directions; j++)
                slope[i * Directions + j] = k * k * spectrum.S(wi, j * dt) * (hi - lo) * dt;
        }
    }

    /// <summary>Whether a gun at a height over the sea (m, its axis) and a place in its ship (x to the bow and y to starboard,
    /// as shares of her half length and half beam, -1 to 1) is awash, on a heading (degrees true) at a speed (knots)
    /// (docs/battle/balance.md BL1f; research/simulation/ship-motion/ship-motion-gunnery-research.md §1.2's casemate rule):
    /// under H_s x its side (0.6 on the weather side to 0.35 on the lee) x its place (2 at the bow, 1 amidships and aft) x
    /// (1 + 0.5 (V / 25 kn)² h), h her heading to the sea (1 head on, 0.6 on the bow, 0.2 beam on and abaft). Coronel's
    /// main-deck casemates (about 3 m) go under on the weather side at about 5 m of sea; a turret's guns never do.</summary>
    public bool Awash(double heightM, double along, double across, double headingDeg, double kn)
    {
        if (HsM <= 0)
            return false;
        double rel = double.DegreesToRadians(FromDeg - headingDeg), c = Math.Cos(rel);
        double heading = 0.2 + 0.8 * Math.Pow(Math.Max(c, 0), 1.7);
        double side = 0.475 + 0.125 * Math.Sin(rel) * Math.Clamp(across, -1, 1);
        double place = 1 + Math.Clamp((along - 0.5) / 0.5, 0, 1);
        return heightM < HsM * side * place * (1 + 0.5 * Math.Pow(kn / 25, 2) * heading);
    }

    /// <summary>A ship's pitch at a heading (degrees true) and speed (m/s through the water), with her waterline length,
    /// m, her natural pitch period, s, and its damping ratio (docs/battle/balance.md BL1h; research §1.3): the wave slope
    /// along her (each component's cos of its angle to her heading), the share of it a hull of her length follows (the
    /// best-fit tilt of her waterplane to the wave over her length, 3 (sin q - q cos q) / q³ with q her half length in
    /// the wave's along-ship wavenumber: a long wave carries her bow up and down with it, one shorter than her rocks her
    /// little), averaged across her beam too (sin p / p, p her half beam in the wave's across-ship wavenumber: a short wave
    /// meeting her obliquely is many crests across her waterplane), met at the encounter frequency and magnified by her
    /// (heavily damped) pitch resonance. A short, fast ship meeting a head sea pitches hard and quickly; a long one in a
    /// short sea hardly at all.</summary>
    public RollState Pitch(double headingDeg, double speedMs, double lengthM, double beamM, double naturalPeriodS, double damping)
    {
        if (!(naturalPeriodS > 0) || !(lengthM > 0))
            return default;
        double halfBeam = 0.5 * Math.Max(beamM, 0);
        double psi = double.DegreesToRadians(90 - headingDeg), cp = Math.Cos(psi), sp = Math.Sin(psi);
        double wn = 2 * Math.PI / naturalPeriodS, z2 = 4 * damping * damping, half = 0.5 * lengthM;
        double m0 = 0, m2 = 0;
        for (int i = 0; i < Frequencies; i++)
        {
            double wi = w[i], k = wi * wi / SeaSpectrum.G, doppler = k * speedMs;
            int row = i * Directions;
            for (int j = 0; j < Directions; j++)
            {
                double s = slope[row + j];
                if (s == 0)
                    continue;
                double cb = cos[j] * cp + sin[j] * sp, sb = sin[j] * cp - cos[j] * sp, q = k * Math.Abs(cb) * half, pb = k * Math.Abs(sb) * halfBeam;
                double fit = (q < 1e-3 ? 1 : 3 * (Math.Sin(q) - q * Math.Cos(q)) / (q * q * q)) * (pb < 1e-3 ? 1 : Math.Sin(pb) / pb);
                double we = Math.Abs(wi - doppler * cb), l = we / wn, d = 1 - l * l;
                double v = s * cb * cb * fit * fit / (d * d + z2 * l * l);
                m0 += v;
                m2 += v * we * we;
            }
        }
        return m0 > 0 ? new RollState(2 * Math.Sqrt(m0), Math.Sqrt(m2), 2 * Math.PI * Math.Sqrt(m0 / Math.Max(m2, 1e-30))) : default;
    }

    /// <summary>A ship's roll at a heading (degrees true) and speed (m/s through the water), with her natural period (s;
    /// none, or 0 or less: she doesn't roll) and damping ratio; <paramref name="excitation"/> the share of the wave slope
    /// that heels her (<see cref="SimSettings.RollExcitation"/>).</summary>
    public RollState At(double headingDeg, double speedMs, double naturalPeriodS, double damping, double excitation)
    {
        if (!(naturalPeriodS > 0) || double.IsInfinity(naturalPeriodS))
            return default;
        // her heading in the spectrum's frame: x east, y north, counter-clockwise from east
        double psi = double.DegreesToRadians(90 - headingDeg), cp = Math.Cos(psi), sp = Math.Sin(psi);
        double wn = 2 * Math.PI / naturalPeriodS, z2 = 4 * damping * damping, e2 = excitation * excitation;
        double m0 = 0, m2 = 0;
        for (int i = 0; i < Frequencies; i++)
        {
            double wi = w[i], doppler = wi * wi * speedMs / SeaSpectrum.G;
            int row = i * Directions;
            for (int j = 0; j < Directions; j++)
            {
                double s = slope[row + j];
                if (s == 0)
                    continue;
                // the wave's travel against her heading: cos β (0 a following sea), sin β her beam-on share
                double cb = cos[j] * cp + sin[j] * sp, sb = sin[j] * cp - cos[j] * sp;
                double we = Math.Abs(wi - doppler * cb), l = we / wn, d = 1 - l * l;
                double v = s * e2 * sb * sb / (d * d + z2 * l * l);
                m0 += v;
                m2 += v * we * we;
            }
        }
        return m0 > 0 ? new RollState(2 * Math.Sqrt(m0), Math.Sqrt(m2), 2 * Math.PI * Math.Sqrt(m0 / Math.Max(m2, 1e-30))) : default;
    }
}
