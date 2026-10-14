namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>A ship's baked layers: the hull, each turret type and the height map, at level 0 (the mip atlases are
/// made from these when saved); and for the lighting, the hull's and each turret type's relief (metres) and its normal
/// + AO (<see cref="ReliefMaps"/>).</summary>
public sealed class Baked
{
    public required Image8 Hull;
    public required Image8 Height;
    public required Dictionary<string, Image8> Turrets;
    public required ReliefImage HullRelief;
    public required Image8 HullNormal;
    public required Dictionary<string, ReliefImage> TurretRelief;
    public required Dictionary<string, Image8> TurretNormal;
}

/// <summary>Bakes ShipSprites on the GPU and writes what render.render_ship wrote: sprite.json, hull.png,
/// height.png, turrets/&lt;type&gt;.png and each layer's _mips.png atlas.</summary>
public static class ShipBake
{
    public static Baked Bake(ShipSprites sp, GpuBaker g)
    {
        double s = sp.Meta.ScalePxPerM;
        var hull = g.Render(Lower.Run(sp.Hull));
        var height = Lower.Run(sp.Height);
        var hullRelief = g.RenderRelief(height);   // the columns' plateaus
        var turrets = new Dictionary<string, Image8>(StringComparer.Ordinal);
        var tRelief = new Dictionary<string, ReliefImage>(StringComparer.Ordinal);
        var tNormal = new Dictionary<string, Image8>(StringComparer.Ordinal);
        foreach (var (tid, scene) in sp.Turrets)
        {
            var dl = Lower.Run(scene);
            turrets[tid] = g.Render(dl);
            tRelief[tid] = g.RenderRelief(dl);
            tNormal[tid] = ReliefMaps.NormalAo(tRelief[tid], s, turrets[tid]);
        }
        return new Baked
        {
            Hull = hull,
            Height = g.Render(height, heightMap: true),
            Turrets = turrets,
            HullRelief = hullRelief,
            HullNormal = ReliefMaps.NormalAo(hullRelief, s, hull),
            TurretRelief = tRelief,
            TurretNormal = tNormal,
        };
    }

    public static void Save(ShipSprites sp, Baked b, string dir)
    {
        var meta = sp.Meta;
        var rects = meta.MipRects;
        Directory.CreateDirectory(Path.Combine(dir, "turrets"));
        Png.Save(Path.Combine(dir, "hull.png"), b.Hull);
        Png.Save(Path.Combine(dir, meta.Layers.HullMips), Mips.Atlas(b.Hull, rects, max: false));
        Png.Save(Path.Combine(dir, "height.png"), b.Height);
        Png.Save(Path.Combine(dir, meta.Shadow.HeightMapMips), Mips.Atlas(b.Height, rects, max: true));
        Png.Save(Path.Combine(dir, "relief.png"), ReliefMaps.Preview(b.HullRelief));
        Png.Save(Path.Combine(dir, "normal.png"), b.HullNormal);
        Png.Save(Path.Combine(dir, "normal_mips.png"), NormalAtlas(b.HullNormal, b.Hull, rects));
        foreach (var (tid, tm) in meta.TurretTypes)
        {
            Png.Save(Path.Combine(dir, tm.File), b.Turrets[tid]);
            Png.Save(Path.Combine(dir, tm.MipsFile), Mips.Atlas(b.Turrets[tid], tm.MipRects, max: false));
            Png.Save(Path.Combine(dir, $"turrets/{tid}_relief.png"), ReliefMaps.Preview(b.TurretRelief[tid]));
            Png.Save(Path.Combine(dir, $"turrets/{tid}_normal.png"), b.TurretNormal[tid]);
        }
        JsonFile.Save(Path.Combine(dir, "sprite.json"), meta, RenderJson.Default.SpriteMeta, 2);
    }

    /// <summary>A normal layer's mips (<see cref="ReliefMaps.Chain"/>, by its colour's coverage) packed at rects.</summary>
    static Image8 NormalAtlas(Image8 normal, Image8 colour, IReadOnlyList<long[]> rects)
    {
        var cov = new List<Image8> { colour };
        while (cov.Count < rects.Count)
            cov.Add(Mips.Half(cov[^1], max: false));
        var chain = ReliefMaps.Chain(normal, cov);
        var r1 = rects.Count > 1 ? rects[1] : null;
        var atlas = new Image8(r1 != null ? (int)(r1[0] + r1[2]) : normal.Width, normal.Height, 4);
        for (int k = 0; k < rects.Count; k++)
        {
            var (l, r) = (chain[k], rects[k]);
            for (int y = 0; y < l.Height; y++)
                Buffer.BlockCopy(l.Data, y * l.Width * 4, atlas.Data, (((int)r[1] + y) * atlas.Width + (int)r[0]) * 4, l.Width * 4);
        }
        return atlas;
    }

    /// <summary>Intersection over union of two layers' coverage, both the same size. RGBA layers compare alpha as a
    /// fraction (sum of min over sum of max): a hard threshold would mostly measure edge pixels near half coverage,
    /// where 8x MSAA's steps of 1/8 and cairo's analytic coverage round apart. The height map compares where the
    /// grey is above 0.</summary>
    public static double CoverageIoU(Image8 a, Image8 b)
    {
        if (a.Width != b.Width || a.Height != b.Height || a.Channels != b.Channels)
            return 0;
        long inter = 0, uni = 0;
        int n = a.Width * a.Height;
        for (int i = 0; i < n; i++)
        {
            int ca, cb;
            if (a.Channels == 4)
                (ca, cb) = (a.Data[i * 4 + 3], b.Data[i * 4 + 3]);
            else
                (ca, cb) = (a.Data[i] > 0 ? 1 : 0, b.Data[i] > 0 ? 1 : 0);
            inter += Math.Min(ca, cb);
            uni += Math.Max(ca, cb);
        }
        return uni == 0 ? 1.0 : (double)inter / uni;
    }

    /// <summary>Mean absolute difference of the colour channels where both layers are covered, 0-255.</summary>
    public static double ColourDiff(Image8 a, Image8 b)
    {
        if (a.Width != b.Width || a.Height != b.Height || a.Channels != 4 || b.Channels != 4)
            return double.NaN;
        double sum = 0;
        long n = 0;
        for (int i = 0; i < a.Width * a.Height; i++)
        {
            if (a.Data[i * 4 + 3] < 255 || b.Data[i * 4 + 3] < 255)
                continue;
            for (int k = 0; k < 3; k++)
                sum += Math.Abs(a.Data[i * 4 + k] - b.Data[i * 4 + k]);
            n += 3;
        }
        return n == 0 ? 0 : sum / n;
    }
}
