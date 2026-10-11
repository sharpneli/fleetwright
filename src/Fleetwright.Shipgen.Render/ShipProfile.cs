namespace Fleetwright.Shipgen.Render;

/// <summary>A designed ship's profile from the starboard side, bow to the right (docs/shipgen/profile-plan.md): the full
/// hull, bottom and all, in the ship's look, and the recognition silhouette (what shows above the water, in one colour).
/// Display lists, as the sprites are; Bake.ProfileBake turns them into images. A still picture for the ship's card:
/// nothing turns and there are no mips.</summary>
public sealed class ShipProfile
{
    public required Scene Full;
    public required Scene Silhouette;
    public required double ScalePxPerM;
    /// <summary>The ship's centre on the main deck, in Full's pixels (x right, y down).</summary>
    public required double[] OriginPx;
    /// <summary>The waterline's row in Full and in Silhouette (whose bottom it is, but for a small margin).</summary>
    public required double WaterlinePx, SilhouetteWaterlinePx;

    /// <summary>The silhouette's margin under the waterline, metres.</summary>
    const double SilhouetteUnder = 0.5;

    /// <summary>Draw the profile at scale px/m (the sprites' usual is 10). look (navy, era; either may be left out)
    /// overrides the design's own look key by key, as ShipSprites.Build does. The canvas is as wide as the sprite's at
    /// the same scale (the hull's length plus HullArt.PadM, snapped alike) unless something stands past the hull's
    /// ends, so a plan and a profile stack with their ends lined up.</summary>
    public static ShipProfile Build(Ship ship, double scale, LookInput? look = null)
    {
        var design = ship.Design;
        if (look != null)
        {
            var own = Looks.LookOf(design);
            design = design with { Look = own with { Navy = look.Navy ?? own.Navy, Era = look.Era ?? own.Era } };
        }
        var pal = Looks.Palette(design);
        var shapes = Looks.Shapes(design);
        string turretLook = Looks.TurretLook(design);
        var v = SideView.Of(ship);

        var (bx0, bx1, bz0, bz1) = v.Bounds();
        double pad = HullArt.PadM;
        // half the width, snapped as HullArt.ShipExtent snaps it (align 2: a whole pixel either side of the centre)
        double hx = Math.Max(v.Length / 2, Math.Max(-bx0, bx1)) + pad;
        hx = Math.Ceiling(hx * scale) / scale;
        // heights snapped so the waterline lies on a pixel edge
        double wl = v.Waterline;
        double up = Math.Ceiling((bz1 + pad - wl) * scale) / scale;
        double down = Math.Ceiling((wl - bz0 + pad) * scale) / scale;
        double under = Math.Ceiling(SilhouetteUnder * scale) / scale;

        var full = SideArt.Build(v, pal, shapes, turretLook, scale, -hx, hx, wl + up, wl - down);
        var sil = SideArt.Build(v, pal, shapes, turretLook, scale, -hx, hx, wl + up, wl - under, above: true);
        Flatten(sil.Root, Rgb.Parse(pal["line"]));
        return new ShipProfile
        {
            Full = full, Silhouette = sil, ScalePxPerM = scale, OriginPx = [hx * scale, (wl + up) * scale],
            WaterlinePx = up * scale, SilhouetteWaterlinePx = up * scale,
        };
    }

    /// <summary>Paints a drawing in one colour: every fill and stroke becomes col, and the rim lights (outline-only
    /// strokes faded over a shape) go, as they would only thicken the outline by a hair.</summary>
    static void Flatten(Group g, Rgb col)
    {
        g.S.Fill = col;
        g.Items.RemoveAll(n => n.S.FillNone && n.S.Stroke != null && n.S.StrokeOpacity is < 1);
        foreach (var n in g.Items)
        {
            if (n.S.Fill != null)
                n.S.Fill = col;
            if (n.S.Stroke != null)
                n.S.Stroke = col;
            if (n is Group sub)
                Flatten(sub, col);
        }
    }
}
