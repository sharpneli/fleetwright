using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Flashes;

/// <summary>The muzzle flashes' lights of a frame (docs/visuals/muzzle-flashes.md F4): point lights in the Lights block
/// every map shader reads (the sea's glints and foam, the ships, the splash columns). Flashes within reach of each other
/// (1.5 times the larger size) are summed at their intensity-weighted centre, so a light never sits between two turrets'
/// fireballs where nothing burns; past <see cref="LightUniforms.MaxFlashes"/> the dimmest goes. Nothing allocates.</summary>
public sealed class FlashLights
{
    readonly double[] rad = new double[LightUniforms.MaxFlashes], size = new double[LightUniforms.MaxFlashes];
    readonly Vec3d[] at = new Vec3d[LightUniforms.MaxFlashes];
    readonly Vector3[] colour = new Vector3[LightUniforms.MaxFlashes];
    LightUniforms block;

    public int Count { get; private set; }

    /// <summary>Light i: where (Earth-centred), its light (display units × m²) and its size, m.</summary>
    public (Vec3d At, double Rad, double Size) this[int i] => (at[i], rad[i], size[i]);

    /// <summary>The Lights block's flash fields (the rest unset), after <see cref="Finish"/>.</summary>
    public ref readonly LightUniforms Block => ref block;

    public void Clear()
    {
        Count = 0;
        block = default;
    }

    /// <summary>A flash's light: into one it's within reach of, or a new one, or in place of the dimmest when all are
    /// taken (unless it's dimmer still).</summary>
    public void Add(Vec3d where, double light, Vector3 rgb, double metres)
    {
        int k = -1;
        for (int j = 0; j < Count && k < 0; j++)
            if ((at[j] - where).Length < 1.5 * Math.Max(size[j], metres))
                k = j;
        if (k < 0)
        {
            if (Count < LightUniforms.MaxFlashes)
                k = Count++;
            else
            {
                k = 0;
                for (int j = 1; j < Count; j++)
                    if (rad[j] < rad[k])
                        k = j;
                if (rad[k] >= light)
                    return;
            }
            (rad[k], at[k], colour[k], size[k]) = (0, default, Vector3.Zero, 0);
        }
        double sum = rad[k] + light;
        at[k] = (at[k] * rad[k] + where * light) / sum;
        colour[k] = (colour[k] * (float)rad[k] + rgb * (float)light) / (float)sum;
        rad[k] = sum;
        size[k] = Math.Max(size[k], metres);
    }

    /// <summary>The lights into the block: eye-relative, the view's axes for shaders in a local frame.</summary>
    public void Finish(in MapView view)
    {
        var up = (-view.Forward).Normalized();
        block.FlashInfo = new Vector4(Count, 0, 0, 0);
        block.FlashEast = new Vector4(F(view.Right), 0);
        block.FlashNorth = new Vector4(F(view.Up), 0);
        block.FlashUp = new Vector4(F(up), 0);
        for (int k = 0; k < Count; k++)
        {
            var rel = at[k] - view.Eye;
            block.FlashPos[k] = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, (float)size[k]);
            block.FlashRad[k] = new Vector4(FlashPhotometry.UnitLuminance(colour[k]) * (float)rad[k], 0);
        }
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);
}
