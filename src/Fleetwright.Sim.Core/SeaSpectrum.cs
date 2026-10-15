namespace Fleetwright.Sim.Core;

/// <summary>The state of the sea at a place (docs/visuals/sea.md W1): the wind sea from the wind (its speed, the way it
/// blows from, the fetch it has blown over) and a swell from elsewhere (significant height, peak period, the way it
/// comes from). Pure values: the map's sea is drawn from it, and the sim can read the same field later.</summary>
public readonly record struct SeaState(double WindMs, double WindFromDeg, double FetchM, double SwellHsM, double SwellTpS, double SwellFromDeg)
{
    /// <summary>The sea for a weather: its wind, a fetch for that wind (<see cref="FetchFor"/>), and a low swell from
    /// 50° off the wind (the cross-sea that makes the surface read as ocean) until the weather knows its swell.</summary>
    public static SeaState Of(in WeatherHere w, double swellHsM = 1.0, double swellTpS = 12) =>
        new(w.WindMs, w.WindFromDeg, FetchFor(w.WindMs), swellHsM, swellTpS, (w.WindFromDeg + 50) % 360);

    /// <summary>A fetch for a wind (research/vfx/ocean-surface-research.md §2.2): 150 km tracks the WMO's probable
    /// heights up to a gale, and storms have blown longer and further, so it grows past that to 1,000 km.</summary>
    public static double FetchFor(double windMs) => Math.Clamp(150_000 * Math.Pow(Math.Max(windMs, 0.1) / 20.7, 4), 150_000, 1_000_000);
}

/// <summary>What one cascade's initial spectrum holds: its height variance and mean-square slopes along x and y (m², and
/// unitless), summed over its bins as uploaded.</summary>
public readonly record struct CascadeStats(double Variance, double MssX, double MssY);

/// <summary>The directional wave spectrum of a <see cref="SeaState"/> and its seeded initial amplitudes for the FFT
/// cascades (docs/visuals/sea.md W1; the reference is docs/visuals/sea-state-lab.html, the research
/// research/vfx/ocean-surface-research.md §2). The wind sea is JONSWAP, fetch-limited and capped at Pierson-Moskowitz
/// full development (the peak enhancement γ fading to 1 there), spread by Longuet-Higgins cos^2s(θ/2) with Mitsuyasu's
/// s; the swell is a narrow JONSWAP (γ 5, s 40) scaled to its height. Directions are in a local frame, x east and y
/// north, angles counter-clockwise from east; waves travel downwind, toward the way the wind blows. The phases are
/// fixed per cascade: a new state rescales the same random field, so the sea never pops. Built once per state; only
/// <see cref="BuildH0"/> fills a caller's buffer.</summary>
public sealed class SeaSpectrum
{
    public const double G = 9.81;

    /// <summary>The cascades' tiles, metres: deliberately incommensurate. Cascade c holds the waves with wavenumbers
    /// from <see cref="BandLow"/> to <see cref="BandHigh"/>, so each wave is in exactly one and has at least 4 texels.</summary>
    public static readonly double[] TileM = [1987, 251, 31.7];

    public const int Cascades = 3;
    static readonly ulong[] Seeds = [11, 23, 37];

    public static double BandLow(int c) => c == 0 ? 0 : 2 * Math.PI * 4 / TileM[c];
    public static double BandHigh(int c) => c == Cascades - 1 ? double.PositiveInfinity : 2 * Math.PI * 4 / TileM[c + 1];

    public SeaState State { get; }
    /// <summary>The wind sea's peak angular frequency, rad/s; Phillips' α; the peak enhancement; the spreading's s at
    /// the peak; whether the sea is fully developed (Pierson-Moskowitz) rather than fetch-limited.</summary>
    public double PeakW { get; }
    public double Alpha { get; }
    public double Gamma { get; }
    public double SpreadPeak { get; }
    public bool Developed { get; }
    /// <summary>The way the wind sea and the swell travel, radians from east, counter-clockwise.</summary>
    public double WindAngle { get; }
    public double SwellAngle { get; }
    /// <summary>The wind sea's height variance, m².</summary>
    public double WindSeaM0 { get; }
    readonly double swellA, swellW;

    public SeaSpectrum(in SeaState s)
    {
        State = s;
        double u = Math.Max(s.WindMs, 0), f = Math.Max(s.FetchM, 1000);
        double uu = Math.Max(u, 0.3);
        double x = G * f / (uu * uu);
        double wp = 22 * Math.Pow(G * G / (uu * f), 1 / 3.0), alpha = 0.076 * Math.Pow(x, -0.22);
        double wpPM = 0.855 * G / uu;
        if (wp < wpPM)
        {
            (wp, alpha, Developed) = (wpPM, 0.0081, true);
        }
        PeakW = wp;
        Gamma = 1 + 2.3 * Math.Clamp((wp / wpPM - 1) / 0.5, 0, 1);
        Alpha = Math.Max(alpha, 0.0081);
        SpreadPeak = Math.Min(11.5 * Math.Pow(wp * uu / G, -2.5), 40);
        WindAngle = TravelAngle(s.WindFromDeg);
        SwellAngle = TravelAngle(s.SwellFromDeg);
        swellW = 2 * Math.PI / Math.Max(s.SwellTpS, 1);
        double area = 0;
        for (double w = 0.05; w < 6; w += 0.001)
            area += Jonswap(w, swellW, 5) * 0.001;
        swellA = s.SwellHsM > 0 && area > 0 ? Math.Pow(s.SwellHsM / 4, 2) / area : 0;
        double m0 = 0;
        if (u > 0.3)
            for (double w = 0.02; w < 12; w += 0.0005)
                m0 += Alpha * G * G * Jonswap(w, wp, Gamma) * 0.0005;
        WindSeaM0 = m0;
    }

    /// <summary>The way waves travel for a compass direction they come from, radians from east counter-clockwise.</summary>
    static double TravelAngle(double fromDeg)
    {
        double to = (fromDeg + 180) * Math.PI / 180;   // compass: x = sin, y = cos
        return Math.Atan2(Math.Cos(to), Math.Sin(to));
    }

    /// <summary>The significant wave height the spectrum stands for, metres (wind sea and swell).</summary>
    public double HsTarget => 4 * Math.Sqrt(WindSeaM0 + Math.Pow(Math.Max(State.SwellHsM, 0) / 4, 2));

    /// <summary>Cox and Munk's (1954) clean-surface mean-square slope for the wind, along x and y (upwind 0.00316 U,
    /// crosswind 0.003 + 0.00192 U, turned into the frame): the slopes the eye sees, resolved or not.</summary>
    public (double X, double Y) CoxMunkMss
    {
        get
        {
            double u = Math.Max(State.WindMs, 0), up = 0.00316 * u, cross = 0.003 + 0.00192 * u;
            double c = Math.Cos(WindAngle), s = Math.Sin(WindAngle);
            return (up * c * c + cross * s * s, up * s * s + cross * c * c);
        }
    }

    /// <summary>The directional spectrum S(ω, θ), m²·s/rad², θ the way a wave travels.</summary>
    public double S(double w, double theta)
    {
        double v = 0;
        if (State.WindMs > 0.3)
        {
            double s = w < PeakW ? SpreadPeak * Math.Pow(w / PeakW, 5) : SpreadPeak * Math.Pow(w / PeakW, -2.5);
            v += Alpha * G * G * Jonswap(w, PeakW, Gamma) * Spread(Math.Max(s, 0.5), theta - WindAngle);
        }
        if (swellA > 0)
            v += swellA * Jonswap(w, swellW, 5) * Spread(40, theta - SwellAngle);
        return v;
    }

    /// <summary>The JONSWAP shape without its scale: ω⁻⁵ e^(-5/4 (ωp/ω)⁴) γ^r.</summary>
    static double Jonswap(double w, double wp, double gamma)
    {
        double sig = w <= wp ? 0.07 : 0.09;
        double r = Math.Exp(-(w - wp) * (w - wp) / (2 * sig * sig * wp * wp));
        return Math.Pow(w, -5) * Math.Exp(-1.25 * Math.Pow(wp / w, 4)) * Math.Pow(gamma, r);
    }

    /// <summary>Longuet-Higgins' cos^2s(θ/2), normalised over the circle.</summary>
    static double Spread(double s, double theta)
    {
        double ln = (2 * s - 1) * Math.Log(2) + 2 * LogGamma(s + 1) - LogGamma(2 * s + 1) - Math.Log(Math.PI);
        return Math.Exp(ln) * Math.Pow(Math.Abs(Math.Cos(theta / 2)), 2 * s);
    }

    static readonly double[] Lanczos = [0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313,
        -176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7];

    /// <summary>ln Γ(z), Lanczos (g = 7).</summary>
    static double LogGamma(double z)
    {
        var c = Lanczos;
        if (z < 0.5)
            return Math.Log(Math.PI / Math.Sin(Math.PI * z)) - LogGamma(1 - z);
        z -= 1;
        double x = c[0];
        for (int i = 1; i < 9; i++)
            x += c[i] / (z + i);
        double t = z + 7.5;
        return 0.5 * Math.Log(2 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(x);
    }

    /// <summary>Fills <paramref name="rgba"/> (n² texels of 4 floats, row m along y, column along x) with cascade
    /// <paramref name="cascade"/>'s initial amplitudes h0(k) and conj(h0(-k)) (re, im, re, im), as the GPU evolves
    /// them: h(k, t) = h0(k) e^(-iωt) + conj(h0(-k)) e^(iωt), which with the e^(+ik·x) inverse FFT travels along +k.
    /// h0(k) = (ξ₁ + iξ₂) ½ √P(k), P the variance in k's bin, so the realised height variance is ΣP. The gaussians are
    /// drawn for every bin whatever the state, so the phases never change.</summary>
    public CascadeStats BuildH0(int cascade, int n, Span<float> rgba)
    {
        if (rgba.Length < n * n * 4)
            throw new ArgumentException("too small for n² texels", nameof(rgba));
        double tile = TileM[cascade], dk = 2 * Math.PI / tile, lo = BandLow(cascade), hi = BandHigh(cascade);
        var rnd = RandomStream.FromSeed(Seeds[cascade] * 7919 + (ulong)n);
        double variance = 0, mx = 0, my = 0;
        // first pass: h0(k) into the first two channels
        for (int m = 0; m < n; m++)
            for (int j = 0; j < n; j++)
            {
                double u1 = Math.Max(rnd.NextDouble(), 1e-9), u2 = rnd.NextDouble(), r = Math.Sqrt(-2 * Math.Log(u1));
                double g1 = r * Math.Cos(2 * Math.PI * u2), g2 = r * Math.Sin(2 * Math.PI * u2);
                int i = (m * n + j) * 4;
                rgba[i] = rgba[i + 1] = 0;
                if (j == n / 2 || m == n / 2)
                    continue;
                double kx = (j < n / 2 ? j : j - n) * dk, ky = (m < n / 2 ? m : m - n) * dk, k = Math.Sqrt(kx * kx + ky * ky);
                if (k == 0 || k < lo || k >= hi)
                    continue;
                double w = Math.Sqrt(G * k), dwdk = G / (2 * w);
                double p = S(w, Math.Atan2(ky, kx)) * dwdk / k * dk * dk;
                if (!(p > 0))
                    continue;
                double a = Math.Sqrt(p) * 0.5;
                rgba[i] = (float)(g1 * a);
                rgba[i + 1] = (float)(g2 * a);
                variance += p;
                mx += kx * kx * p;
                my += ky * ky * p;
            }
        // second: conj(h0(-k)) beside it
        for (int m = 0; m < n; m++)
            for (int j = 0; j < n; j++)
            {
                int i = (m * n + j) * 4, o = (((n - m) % n) * n + (n - j) % n) * 4;
                rgba[i + 2] = rgba[o];
                rgba[i + 3] = -rgba[o + 1];
            }
        return new CascadeStats(variance, mx, my);
    }
}
