namespace Fleetwright.Sim.Combat;

/// <summary>A shot's glowing gas (docs/visuals/muzzle-flashes.md "The look"; research/vfx/muzzle_flash_ref.py
/// <c>Shot.emitters</c>): the one model of a flash's light, which the screens draw and the night's sight sees
/// (docs/battle/night.md Q2). Each part is an ellipse along the bore at a temperature:
/// <list type="bullet">
/// <item><b>the primary core</b>, per barrel: hot gas at 0.35 x_M from the muzzle, 2000 → 1200 K over the gun's emptying
/// time t_e;</item>
/// <item><b>the Mach disc</b>, per barrel: a thin reddish disc at x_M, 1900 → 1400 K over t_e;</item>
/// <item><b>the secondary fireball</b>, the mount's: centred at x_M + 0.7 λ_f, 0.8 by 0.5 λ_f, growing from 35% over the
/// first half of its life t_s, rising over 0.12 t_s and falling as 1 − u², at 75-100% of its peak temperature; lifted by
/// its buoyancy as ½·0.6·g·t².</item>
/// </list>
/// Each part's luminance is its emissivity times the blackbody's at its temperature (cd/m²), its intensity that over its
/// ellipse (cd), as seen across the bore. Positions are metres along the bore from the muzzle, and up for the lift.</summary>
public static class FlashGlow
{
    /// <summary>One shot's flash: the gun's (<see cref="MuzzleFlash"/>) and this shot's fireball (its length λ_f and life
    /// t_s, 0 when the secondary flash doesn't burn) and scatter.</summary>
    public readonly record struct Shot(double BoreM, double MachDiskM, double EmptyS, double SecondaryK, double FlashLengthM, double SecondaryS,
        double Scatter);

    public struct Part
    {
        /// <summary>Along the bore from the muzzle, m (its centre), and its lift, m.</summary>
        public float Along, Lift;
        /// <summary>Its semi-axes, m: along the bore and across it.</summary>
        public float A, B;
        public float Kelvin;
        /// <summary>Its luminous intensity across the bore, cd (0 when it isn't glowing).</summary>
        public float Intensity;
        /// <summary>The secondary's share of its life gone, 0-1 (for the noise's growth).</summary>
        public float Life;
    }

    const double Gravity = 9.81;

    // the blackbody's luminance, cd/m², by temperature, K (log10 interpolated in ln T)
    static readonly (double T, double L)[] Luminances =
    [
        (1000, 2.73), (1200, 141), (1400, 2460), (1600, 2.15e4), (1800, 1.18e5), (2000, 4.67e5), (2200, 1.44e6), (2500, 5.65e6),
        (2800, 1.66e7), (3000, 3.03e7),
    ];

    /// <summary>A blackbody's luminance at <paramref name="kelvin"/>, cd/m².</summary>
    public static double Luminance(double kelvin)
    {
        double x = Math.Log(Math.Max(kelvin, 1));
        double x0 = Math.Log(Luminances[0].T), y0 = Math.Log10(Luminances[0].L);
        if (x <= x0)
            return Math.Pow(10, y0 - 40 * (x0 - x));
        for (int i = 1; i < Luminances.Length; i++)
        {
            double x1 = Math.Log(Luminances[i].T);
            if (x <= x1)
            {
                double y1 = Math.Log10(Luminances[i].L);
                return Math.Pow(10, y0 + (y1 - y0) * (x - x0) / (x1 - x0));
            }
            (x0, y0) = (x1, Math.Log10(Luminances[i].L));
        }
        return Luminances[^1].L;
    }

    /// <summary>The parts at <paramref name="t"/> seconds after firing.</summary>
    public static void At(in Shot e, double t, out Part core, out Part disc, out Part fire)
    {
        core = disc = fire = default;
        if (t < 0)
            return;
        double d = e.BoreM, xm = e.MachDiskM, te = e.EmptyS;
        if (t < te)
        {
            double f = t / te;
            double tc = 2000 - 800 * f, r = 1.5 * d + 0.12 * xm * Math.Min(1, 4 * f);
            core = Ellipse(0.35 * xm, 0, 0.35 * xm + r, r, tc, 1 - Math.Exp(-r / 2), 1, 0);
            double ti = 1900 - 500 * f, ri = 0.22 * xm;
            disc = Ellipse(xm, 0, 0.10 * xm, ri, ti, 1 - Math.Exp(-ri / 3), 1, 0);
        }
        double ts = e.SecondaryS;
        if (ts > 0 && t < ts)
        {
            double rise = 0.12 * ts;
            double env = Math.Min(1, t / rise) * (t > rise ? 1 - Math.Pow((t - rise) / (ts - rise), 2) : 1);
            double tk = e.SecondaryK * (0.75 + 0.25 * env) * Math.Pow(e.Scatter, 0.05);
            double lf = e.FlashLengthM, grow = Math.Min(1, 0.35 + 0.65 * t / (0.5 * ts));
            double a = 0.8 * lf * grow, b = 0.5 * lf * grow;
            fire = Ellipse(xm + 0.7 * lf * grow, 0.5 * 0.6 * Gravity * t * t, a, b, tk, 1 - Math.Exp(-b / 6), env, t / ts);
        }
    }

    static Part Ellipse(double along, double lift, double a, double b, double kelvin, double emissivity, double envelope, double life)
    {
        double l = emissivity * Luminance(kelvin) * envelope;
        return new Part
        {
            Along = (float)along, Lift = (float)lift, A = (float)a, B = (float)b, Kelvin = (float)kelvin,
            Intensity = (float)(l * Math.PI * a * b), Life = (float)life,
        };
    }

    /// <summary>How long a shot glows, s.</summary>
    public static double Life(in Shot e) => Math.Max(e.EmptyS, e.SecondaryS);

    /// <summary>A shot's light over its life, cd·s, with <paramref name="barrels"/> barrels' cores and discs and the
    /// mount's one fireball: the parts' intensity integrated over <see cref="Steps"/> steps.</summary>
    public static double LightCdS(in Shot e, int barrels)
    {
        double life = Life(e), dt = life / Steps, sum = 0;
        for (int k = 0; k < Steps; k++)
        {
            At(e, (k + 0.5) * dt, out var core, out var disc, out var fire);
            sum += (barrels * (core.Intensity + disc.Intensity) + fire.Intensity) * dt;
        }
        return sum;
    }

    public const int Steps = 200;

    /// <summary>The steady light the eye takes a brief flash for, cd (docs/battle/night.md Q2): the Blondel-Rey law at the
    /// threshold, its light over its life over (<see cref="BlondelReyS"/> + its life). What IALA rates flashing lights
    /// by.</summary>
    public static double EffectiveCd(double lightCdS, double lifeS) => lightCdS / (BlondelReyS + lifeS);

    /// <summary>Blondel and Rey's constant at night, s: the eye sums a flash's light over about this.</summary>
    public const double BlondelReyS = 0.2;
}
