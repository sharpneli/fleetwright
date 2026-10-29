using System.Numerics;
using Fleetwright.Sim.Combat;

namespace Fleetwright.Map.Flashes;

/// <summary>A flash's glowing parts at a moment, the sim's one model (<see cref="FlashGlow"/>, docs/battle/night.md Q2: the
/// night's sight sees the same light) on the screen's flash clock (docs/visuals/muzzle-flashes.md "The look";
/// research/vfx/muzzle_flash_ref.py <c>Shot.emitters</c>), each an ellipse along the bore at a temperature:
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
    /// <summary>The shot's flash as the sim's model has it (<see cref="FlashGlow"/>).</summary>
    static FlashGlow.Shot Shot(in ShotBook.Entry e) =>
        new(e.BoreM, e.MachDiskM, e.EmptyS, e.SecondaryK, e.FlashLengthM, e.SecondaryS, e.Scatter);

    /// <summary>The parts at <paramref name="t"/> seconds after firing (on the flash's clock).</summary>
    public static void At(in ShotBook.Entry e, double t, out FlashGlow.Part core, out FlashGlow.Part disc, out FlashGlow.Part fire) =>
        FlashGlow.At(Shot(e), t, out core, out disc, out fire);

    /// <summary>The parts averaged over the frame's interval on the flash's clock, [<paramref name="t"/> −
    /// <paramref name="dt"/>, <paramref name="t"/>], by <see cref="Samples"/> samples: the intensities averaged, the shape
    /// and temperature at the middle. A flash shorter than a frame is neither missed nor flickered.</summary>
    public static void Averaged(in ShotBook.Entry e, double t, double dt, out FlashGlow.Part core, out FlashGlow.Part disc, out FlashGlow.Part fire)
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
    public static Vector3 Colour(in FlashGlow.Part p, bool secondary)
    {
        var c = FlashPhotometry.Chroma(p.Kelvin);
        if (secondary)
            c = Vector3.Lerp(c, FlashPhotometry.Sodium, FlashPhotometry.SodiumShare);
        return FlashPhotometry.UnitLuminance(c);
    }

    /// <summary>How long a shot glows, s on the flash's clock.</summary>
    public static double Life(in ShotBook.Entry e) => FlashGlow.Life(Shot(e));
}
