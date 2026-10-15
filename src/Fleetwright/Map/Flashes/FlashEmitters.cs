using System.Numerics;

namespace Fleetwright.Map.Flashes;

/// <summary>A flash's glowing parts at a moment (docs/visuals/muzzle-flashes.md "The look"; research/vfx/muzzle_flash_ref.py
/// <c>Shot.emitters</c>), each an ellipse along the bore at a temperature:
/// <list type="bullet">
/// <item><b>the primary core</b>, per barrel: hot gas at 0.35 x_M from the muzzle, 2000 → 1200 K over the gun's emptying
/// time t_e;</item>
/// <item><b>the Mach disc</b>, per barrel: a thin reddish disc at x_M, 1900 → 1400 K over t_e;</item>
/// <item><b>the secondary fireball</b>, the mount's: centred at x_M + 0.7 λ_f, 0.8 by 0.5 λ_f, growing from 35% over the
/// first half of its life t_s, rising over 0.12 t_s and falling as 1 − u², at 75-100% of its peak temperature; lifted by
/// its buoyancy as ½·0.6·g·t².</item>
/// </list>
/// Each part's luminance is its emissivity times the blackbody's at its temperature (cd/m²), its intensity that over its
/// ellipse (cd). Positions are metres along the bore from the muzzle (3D), and up for the lift.</summary>
public static class FlashEmitters
{
    public struct Part
    {
        /// <summary>Along the bore from the muzzle, m (its centre), and its lift, m.</summary>
        public float Along, Lift;
        /// <summary>Its semi-axes, m: along the bore and across it.</summary>
        public float A, B;
        public float Kelvin;
        /// <summary>Its luminous intensity seen from above, cd (0 when it isn't glowing).</summary>
        public float Intensity;
        /// <summary>The secondary's share of its life gone, 0-1 (for the noise's growth).</summary>
        public float Life;
    }

    const double Gravity = 9.81;

    /// <summary>The parts at <paramref name="t"/> seconds after firing (on the flash's clock).</summary>
    public static void At(in ShotBook.Entry e, double t, out Part core, out Part disc, out Part fire)
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
        double l = emissivity * FlashPhotometry.Luminance(kelvin) * envelope;
        return new Part
        {
            Along = (float)along, Lift = (float)lift, A = (float)a, B = (float)b, Kelvin = (float)kelvin,
            Intensity = (float)(l * Math.PI * a * b), Life = (float)life,
        };
    }

    /// <summary>The parts averaged over the frame's interval on the flash's clock, [<paramref name="t"/> −
    /// <paramref name="dt"/>, <paramref name="t"/>], by <see cref="Samples"/> samples: the intensities averaged, the shape
    /// and temperature at the middle. A flash shorter than a frame is neither missed nor flickered.</summary>
    public static void Averaged(in ShotBook.Entry e, double t, double dt, out Part core, out Part disc, out Part fire)
    {
        At(e, t - dt / 2, out core, out disc, out fire);
        if (dt <= 0)
            return;
        double ic = 0, id = 0, i_f = 0;
        for (int k = 0; k < Samples; k++)
        {
            At(e, t - dt * (k + 0.5) / Samples, out var c, out var d, out var f);
            ic += c.Intensity;
            id += d.Intensity;
            i_f += f.Intensity;
        }
        // a part that glowed during the frame but not at its middle takes its shape from its own moment
        if (core.Intensity <= 0 && ic > 0)
            At(e, Math.Max(t - dt, 0), out core, out disc, out _);
        if (fire.Intensity <= 0 && i_f > 0)
            At(e, Math.Max(t - dt, 0), out _, out _, out fire);
        core.Intensity = (float)(ic / Samples);
        disc.Intensity = (float)(id / Samples);
        fire.Intensity = (float)(i_f / Samples);
    }

    public const int Samples = 4;

    /// <summary>A part's colour, linear, unit luminance: the blackbody's chroma at its temperature, and for the
    /// secondary fireball the sodium line's yellow mixed in.</summary>
    public static Vector3 Colour(in Part p, bool secondary)
    {
        var c = FlashPhotometry.Chroma(p.Kelvin);
        if (secondary)
            c = Vector3.Lerp(c, FlashPhotometry.Sodium, FlashPhotometry.SodiumShare);
        return FlashPhotometry.UnitLuminance(c);
    }

    /// <summary>How long a shot glows, s on the flash's clock.</summary>
    public static double Life(in ShotBook.Entry e) => Math.Max(e.EmptyS, e.SecondaryS);
}
