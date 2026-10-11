namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>A ship's profile (ShipProfile) baked on the GPU: the full hull and the silhouette as RGBA images.</summary>
public static class ProfileBake
{
    public static (Image8 Full, Image8 Silhouette) Bake(ShipProfile p, GpuBaker g) =>
        (g.Render(Lower.Run(p.Full)), g.Render(Lower.Run(p.Silhouette)));

    /// <summary>Writes profile.png and silhouette.png into dir.</summary>
    public static void Save(string dir, Image8 full, Image8 silhouette)
    {
        Directory.CreateDirectory(dir);
        Png.Save(Path.Combine(dir, "profile.png"), full);
        Png.Save(Path.Combine(dir, "silhouette.png"), silhouette);
    }
}
