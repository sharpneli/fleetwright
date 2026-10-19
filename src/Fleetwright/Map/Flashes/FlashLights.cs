using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Flashes;

/// <summary>The point lights of a frame (docs/visuals/muzzle-flashes.md F4; docs/visuals/hits-and-fires.md decision 5) in
/// the Lights block every map shader reads (the sea's glints and foam, the ships, the splash columns), in two pools of
/// eight so the guns and the fires don't compete: the flashes' (muzzle flashes, bursts, magazines: <see cref="Add"/>) and
/// the fires' (<see cref="AddFire"/>). Within a pool, lights within reach of each other (1.5 times the larger size) are
/// summed at their intensity-weighted centre, so a light never sits between two turrets' fireballs where nothing burns;
/// past its eight the dimmest goes (a fire by its light over its distance from the view's centre: the brightest near
/// the eye kept). Nothing allocates.</summary>
public sealed class FlashLights
{
    const int Pool = LightUniforms.MaxFlashes;
    readonly double[] rad = new double[LightUniforms.MaxLights], size = new double[LightUniforms.MaxLights], score = new double[LightUniforms.MaxLights];
    readonly Vec3d[] at = new Vec3d[LightUniforms.MaxLights];
    readonly Vector3[] colour = new Vector3[LightUniforms.MaxLights];
    readonly int[] counts = new int[2];
    LightUniforms block;
    Vec3d centre;

    /// <summary>The flashes' lights, then the fires'.</summary>
    public int Count => counts[0] + counts[1];
    public int Fires => counts[1];

    /// <summary>Light i (the flashes' first, then the fires'): where (Earth-centred), its light (display units × m²) and its
    /// size, m.</summary>
    public (Vec3d At, double Rad, double Size) this[int i]
    {
        get
        {
            int k = i < counts[0] ? i : Pool + i - counts[0];
            return (at[k], rad[k], size[k]);
        }
    }

    /// <summary>The Lights block's light fields (the rest unset), after <see cref="Finish"/>.</summary>
    public ref readonly LightUniforms Block => ref block;

    public void Clear()
    {
        counts[0] = counts[1] = 0;
        block = default;
    }

    /// <summary>A flash's light: into one it's within reach of, or a new one, or in place of the dimmest when all are
    /// taken (unless it's dimmer still). A light of nothing adds nothing (its weighted place would be 0/0, and a NaN in
    /// the lights whitened every shader that reads them).</summary>
    public void Add(Vec3d where, double light, Vector3 rgb, double metres) => Into(0, where, light, rgb, metres, light);

    /// <summary>A fire's light (I4), into the fires' pool: kept by its light over its distance from the view's centre
    /// (<see cref="Centre"/>), so the eight that light what's seen most are the ones kept.</summary>
    public void AddFire(Vec3d where, double light, Vector3 rgb, double metres, double spanM)
    {
        double d = (where - centre).Length / Math.Max(spanM, 1);
        Into(1, where, light, rgb, metres, light / (1 + d * d));
    }

    /// <summary>The view's centre this frame (the fires' pool's choice by distance).</summary>
    public void Centre(Vec3d c) => centre = c;

    void Into(int pool, Vec3d where, double light, Vector3 rgb, double metres, double s)
    {
        if (!(light > 0))
            return;
        int o = pool * Pool, n = counts[pool], k = -1;
        for (int j = o; j < o + n && k < 0; j++)
            if ((at[j] - where).Length < 1.5 * Math.Max(size[j], metres))
                k = j;
        if (k < 0)
        {
            if (n < Pool)
                k = o + counts[pool]++;
            else
            {
                k = o;
                for (int j = o + 1; j < o + n; j++)
                    if (score[j] < score[k])
                        k = j;
                if (score[k] >= s)
                    return;
            }
            (rad[k], at[k], colour[k], size[k], score[k]) = (0, default, Vector3.Zero, 0, 0);
        }
        double sum = rad[k] + light;
        at[k] = (at[k] * rad[k] + where * light) / sum;
        colour[k] = (colour[k] * (float)rad[k] + rgb * (float)light) / (float)sum;
        rad[k] = sum;
        size[k] = Math.Max(size[k], metres);
        score[k] += s;
    }

    /// <summary>The lights into the block, the flashes' then the fires': eye-relative, the view's axes for shaders in a
    /// local frame.</summary>
    public void Finish(in MapView view)
    {
        var up = (-view.Forward).Normalized();
        block.FlashInfo = new Vector4(Count, 0, 0, 0);
        block.FlashEast = new Vector4(F(view.Right), 0);
        block.FlashNorth = new Vector4(F(view.Up), 0);
        block.FlashUp = new Vector4(F(up), 0);
        int i = 0;
        for (int pool = 0; pool < 2; pool++)
            for (int k = pool * Pool; k < pool * Pool + counts[pool]; k++, i++)
            {
                var rel = at[k] - view.Eye;
                block.FlashPos[i] = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, (float)size[k]);
                block.FlashRad[i] = new Vector4(FlashPhotometry.UnitLuminance(colour[k]) * (float)rad[k], 0);
            }
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);
}
