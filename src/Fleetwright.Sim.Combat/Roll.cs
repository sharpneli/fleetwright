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

/// <summary>What the sea's motion against her needs of a hull (docs/battle/error-sources.md ES4): her waterline length
/// and beam (m), her roll's natural period (s) and damping ratio, the share of the wave slope that heels her, her pitch's
/// (and her heave's, about the same in a ship) natural period and damping ratio; and the patch of her a point's wetting
/// takes the sea over, m (her flare, a gunhouse's front: a wave much shorter than it, met at speed, only ripples on it).</summary>
public readonly record struct Seakeeping(double LengthM, double BeamM, double RollPeriodS, double RollDamping, double Excitation,
    double PitchPeriodS, double PitchDamping, double PatchM);

/// <summary>The sea a world's ships roll in (docs/battle/balance.md BL1a;
/// research/simulation/ship-motion/ship-motion-gunnery-research.md §3.2, its own method rather than its table): the
/// weather's directional spectrum (<see cref="SeaSpectrum"/>, the one the map draws) as wave-slope variance by frequency
/// and direction, built once per weather. A ship's roll is the linear one-degree-of-freedom response to it: each component
/// met at its encounter frequency (her heading and speed to it), its slope taken by her beam-on share (sin of the angle
/// between it and her heading), the share of it her hull feels (averaged over her beam and her length, as the pitch is:
/// docs/battle/balance.md BL1k) and an excitation coefficient (<see cref="SimSettings.RollExcitation"/>), magnified by her
/// resonance (natural period, damping). Short-crestedness comes with the spread; a swell near her period rolls her hard,
/// a short wind sea only a short-period ship, and a big ship overtaking a short sea little. A pure function of the sea and her state, so nothing is stepped and it's the
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

    /// <summary>The relative motion's grid (<see cref="Relative"/>): the directions three to a cell (15°), the wave
    /// elevation's variance in each, S(ω, θ)·dω·dθ summed over its three.</summary>
    public const int WetDirections = Directions / 3;
    readonly double[] elev = new double[Frequencies * WetDirections], wcos = new double[WetDirections], wsin = new double[WetDirections];

    /// <summary>The significant wave height, m (wind sea and swell).</summary>
    public double HsM { get; }

    /// <summary>A number of its own among the seas built (a cache's key: a battery's roll is worked out again for a new
    /// sea); it never enters a result.</summary>
    public int Id { get; } = Interlocked.Increment(ref built) & 0x7F_FFFF;
    static int built;

    public RollSea(SeaSpectrum spectrum)
    {
        HsM = spectrum.HsTarget;
        double dt = 2 * Math.PI / Directions, ratio = Math.Pow(WHigh / WLow, 1.0 / Frequencies);
        for (int j = 0; j < Directions; j++)
            (cos[j], sin[j]) = (Math.Cos(j * dt), Math.Sin(j * dt));
        for (int i = 0; i < Frequencies; i++)
        {
            double lo = WLow * Math.Pow(ratio, i), hi = lo * ratio, wi = Math.Sqrt(lo * hi), k = wi * wi / SeaSpectrum.G;
            w[i] = wi;
            for (int j = 0; j < Directions; j++)
                slope[i * Directions + j] = k * k * spectrum.S(wi, j * dt) * (hi - lo) * dt;
            for (int j = 0; j < WetDirections; j++)
                elev[i * WetDirections + j] = (slope[i * Directions + 3 * j] + slope[i * Directions + 3 * j + 1] + slope[i * Directions + 3 * j + 2]) / (k * k);
        }
        for (int j = 0; j < WetDirections; j++)
            (wcos[j], wsin[j]) = (cos[3 * j + 1], sin[3 * j + 1]);
    }

    /// <summary>The sea's motion against points on a ship (docs/battle/error-sources.md ES4; the seakeeping law the
    /// research cites, Ochi, Nicholson and Price: research/simulation/ship-motion/ship-motion-gunnery-research.md §1.2): at
    /// each point (x to the bow, y to starboard, m from amidships) the variance of the sea's height against her there, m²,
    /// and of its rate, m²/s², at a heading (degrees true) and a speed (m/s through the water). Each wave met at its
    /// encounter frequency, linear: the sea's height at the point less her heave (the wave's mean over her waterplane,
    /// sin q / q · sin p / p, through her heave resonance), her pitch at x (the best fit of her waterplane to the wave's
    /// slope along her, as <see cref="Pitch"/>) and her roll at y (as <see cref="At"/>: the share of the slope that heels
    /// her, through her roll resonance), each with its phase: a long wave lifts her with it, a short one runs up her side.
    /// On the side a wave meets, what of it she doesn't ride stands against her side (reflected, as off a wall: her beam-on
    /// share of it), on her lee as much less (sheltered) [EST: the wall's limit, a short wave beam-on doubling, a long one
    /// passing]. The rate of the sea passing a height z over her point is then Rice's, √(m₂/m₀)/2π · exp(−z²/2m₀), and its
    /// rate as it passes Rayleigh about √m₂ (<see cref="SeaReach"/>). A few µs a point, no garbage.</summary>
    public void Relative(double headingDeg, double speedMs, in Seakeeping hull, ReadOnlySpan<double> x, ReadOnlySpan<double> y,
        Span<double> m0, Span<double> m2)
    {
        m0[..x.Length].Clear();
        m2[..x.Length].Clear();
        if (HsM <= 0 || !(hull.LengthM > 0))
            return;
        double psi = double.DegreesToRadians(90 - headingDeg), cp = Math.Cos(psi), sp = Math.Sin(psi);
        double half = 0.5 * hull.LengthM, halfBeam = 0.5 * Math.Max(hull.BeamM, 0.1);
        double wnP = hull.PitchPeriodS > 0 ? 2 * Math.PI / hull.PitchPeriodS : double.PositiveInfinity;
        double wnR = hull.RollPeriodS > 0 && !double.IsInfinity(hull.RollPeriodS) ? 2 * Math.PI / hull.RollPeriodS : 0;
        for (int i = 0; i < Frequencies; i++)
        {
            double wi = w[i], k = wi * wi / SeaSpectrum.G, doppler = k * speedMs;
            int row = i * WetDirections;
            for (int j = 0; j < WetDirections; j++)
            {
                double e = elev[row + j];
                if (e == 0)
                    continue;
                // the wave's travel against her heading: cos β along her (1 a following sea), sin β across (> 0 toward port)
                double cb = wcos[j] * cp + wsin[j] * sp, sb = wsin[j] * cp - wcos[j] * sp;
                double kx = k * cb, ky = k * sb, q = Math.Abs(kx) * half, pb = Math.Abs(ky) * halfBeam;
                double sq = q < 1e-3 ? 1 : Math.Sin(q) / q, spb = pb < 1e-3 ? 1 : Math.Sin(pb) / pb;
                double fit = q < 1e-3 ? 1 : 3 * (Math.Sin(q) - q * Math.Cos(q)) / (q * q * q);
                double we = wi - doppler * cb;
                // her responses to the wave's height amidships (1 + 0i): heave (m), pitch (rad, bow up), roll (rad,
                // starboard down), each through its resonance
                var (hr, hi) = Resonance(Math.Abs(we) / wnP, hull.PitchDamping, we < 0);
                double zr = sq * spb * hr, zi = sq * spb * hi;
                double pa = -kx * fit * spb, pr = -pa * hi, pi = pa * hr;   // the slope along her, −i·kx, fitted
                double rr = 0, ri = 0;
                if (wnR > 0)
                {
                    var (gr, gi) = Resonance(Math.Abs(we) / wnR, hull.RollDamping, we < 0);
                    double ra = ky * hull.Excitation * sq * spb;   // the slope across her, i·ky, the share that heels her
                    (rr, ri) = (-ra * gi, ra * gr);
                }
                double stands = Math.Abs(sb) * (1 - sq * spb), we2 = we * we;
                // the wave's height at a point is its mean over the point's patch of her (a ripple of a wave on her flare
                // throws nothing up, however fast she meets it)
                double ax = 0.5 * Math.Abs(kx) * hull.PatchM, ay = 0.5 * Math.Abs(ky) * hull.PatchM;
                double patch = (ax < 1e-3 ? 1 : Math.Sin(ax) / ax) * (ay < 1e-3 ? 1 : Math.Sin(ay) / ay);
                for (int n = 0; n < x.Length; n++)
                {
                    double px = x[n], py = y[n];
                    var (s, c) = Math.SinCos(-(kx * px - ky * py));
                    // starboard is the side a wave meets when it travels toward port (sb > 0)
                    double side = (1 + stands * Math.Clamp(py / halfBeam, -1, 1) * Math.Sign(sb)) * patch;
                    double re = c * side - (zr + px * pr + py * rr), im = s * side - (zi + px * pi + py * ri);
                    double v = e * (re * re + im * im);
                    m0[n] += v;
                    m2[n] += v * we2;
                }
            }
        }
    }

    /// <summary>How often the sea passes a height over a point (m; at or under the sea: always), a second, from its motion
    /// against it there (<see cref="Relative"/>'s variances): Rice's rate of up-crossings, √(m₂/m₀)/2π · exp(−z²/2m₀).</summary>
    public static double SeaReach(double m0, double m2, double z)
    {
        if (!(m0 > 0))
            return z <= 0 ? double.PositiveInfinity : 0;
        return z <= 0 ? double.PositiveInfinity : Math.Sqrt(m2 / m0) / (2 * Math.PI) * Math.Exp(-z * z / (2 * m0));
    }

    /// <summary>A linear oscillator's response at a frequency ratio and damping ratio, 1/(1 − l² + 2iζl); its conjugate
    /// for a wave met from behind faster than it runs (the encounter frequency negative).</summary>
    static (double Re, double Im) Resonance(double l, double damping, bool conj)
    {
        if (double.IsNaN(l) || l == 0)
            return (1, 0);
        double a = 1 - l * l, b = 2 * damping * l, d = a * a + b * b;
        return (a / d, conj ? b / d : -b / d);
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

    /// <summary>A ship's roll at a heading (degrees true) and speed (m/s through the water), with her waterline length and
    /// beam (m), her natural period (s; none, or 0 or less: she doesn't roll) and damping ratio; <paramref name="excitation"/>
    /// the share of the wave slope that heels her (<see cref="SimSettings.RollExcitation"/>). Each wave's slope is the
    /// share of it her hull feels (docs/battle/balance.md BL1k): averaged across her beam (sin p / p, p her half beam in
    /// the wave's across-ship wavenumber: a wave much shorter than her beam lifts both sides at once) and along her length
    /// (sin q / q, q her half length in its along-ship wavenumber: one met obliquely, short against her, meets her in
    /// many phases). Without them the short wind sea a fast ship overtakes, met near her roll period, rolled a 70,000 t
    /// battleship 7° in a 15 kn breeze (the record: "waves under 400 ft hardly affect a ship of 60 ft beam or more",
    /// research §3.2).</summary>
    public RollState At(double headingDeg, double speedMs, double lengthM, double beamM, double naturalPeriodS, double damping, double excitation)
    {
        if (!(naturalPeriodS > 0) || double.IsInfinity(naturalPeriodS))
            return default;
        // her heading in the spectrum's frame: x east, y north, counter-clockwise from east
        double psi = double.DegreesToRadians(90 - headingDeg), cp = Math.Cos(psi), sp = Math.Sin(psi);
        double wn = 2 * Math.PI / naturalPeriodS, z2 = 4 * damping * damping, e2 = excitation * excitation;
        double half = 0.5 * Math.Max(lengthM, 0), halfBeam = 0.5 * Math.Max(beamM, 0);
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
                // the wave's travel against her heading: cos β (0 a following sea), sin β her beam-on share
                double cb = cos[j] * cp + sin[j] * sp, sb = sin[j] * cp - cos[j] * sp;
                double q = k * Math.Abs(cb) * half, pb = k * Math.Abs(sb) * halfBeam;
                double felt = (q < 1e-3 ? 1 : Math.Sin(q) / q) * (pb < 1e-3 ? 1 : Math.Sin(pb) / pb);
                double we = Math.Abs(wi - doppler * cb), l = we / wn, d = 1 - l * l;
                double v = s * e2 * sb * sb * felt * felt / (d * d + z2 * l * l);
                m0 += v;
                m2 += v * we * we;
            }
        }
        return m0 > 0 ? new RollState(2 * Math.Sqrt(m0), Math.Sqrt(m2), 2 * Math.PI * Math.Sqrt(m0 / Math.Max(m2, 1e-30))) : default;
    }
}
