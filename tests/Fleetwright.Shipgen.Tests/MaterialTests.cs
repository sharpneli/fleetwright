using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The sprites' materials (docs/visuals/ship-lighting.md, L4): a colour carries its palette key to the draw,
/// the drawing's own key goes over it, and the looks' tables layer default, era and look.</summary>
public class MaterialTests
{
    [Fact]
    public void A_keyed_colour_keeps_its_key_through_a_shade_and_parses_as_its_hex()
    {
        string c = Rgb.Keyed("#808080", "deck");
        Assert.Equal("deck", Rgb.KeyOf(Painter.Shade(c, 0.5)));
        Assert.Equal(Rgb.Parse("#404040"), Rgb.Parse(Painter.Shade(c, 0.5)));
        Assert.Null(Rgb.KeyOf(Painter.Shade("#808080", 0.5)));
    }

    [Fact]
    public void Lower_takes_the_fill_s_key_the_stroke_s_key_and_an_explicit_material_over_both()
    {
        var sc = new Scene(-5, -5, 10, 10, 10);
        sc.Root.Items.Add(new RectNode(-4, -4, 3, 3).Fill(Rgb.Keyed("#808080", "deck")).Stroke(Rgb.Keyed("#101010", "line"), 0.2));
        var g = new Group().Mat("brass");
        g.Items.Add(new CircleNode(2, 2, 1).Fill(Rgb.Keyed("#808080", "fitting")));
        sc.Root.Items.Add(g);
        sc.Root.Items.Add(new CircleNode(-2, 2, 1).Fill("#808080"));
        var mats = Lower.Run(sc).Ops.Where(o => o.Kind == OpKind.Fill).Select(o => o.Mat).ToList();
        Assert.Equal(["deck", "line", "brass", null], mats);
    }

    [Fact]
    public void A_look_s_materials_are_the_defaults_under_its_era_s()
    {
        var wwii = Render.Looks.Materials(new Design { Look = new LookInput { Navy = "generic", Era = "wwii" } });
        var victorian = Render.Looks.Materials(new Design { Look = new LookInput { Navy = "generic", Era = "victorian" } });
        Assert.True(victorian["turret"].Roughness < wwii["turret"].Roughness);   // gloss enamel
        Assert.Equal(wwii["chain"], victorian["chain"]);                         // the era leaves bare steel alone
        Assert.Equal(1, wwii["brass"].Metallic);
        Assert.Equal(wwii[Materials.Paint], wwii["no such key"]);
        Assert.Equal(wwii[Materials.Paint], wwii[null]);
    }

    [Fact]
    public void The_material_layer_carries_each_fill_s_alpha()
    {
        var sc = new Scene(-5, -5, 10, 10, 10);
        sc.Root.Items.Add(new RectNode(-4, -4, 3, 3).Fill(Rgb.Keyed("#808080", "chain")).FillOp(0.5));
        var table = new Materials(new() { ["chain"] = (0.4, 1.0) });
        var op = Lower.Run(sc).WithMaterials(table).Ops.Single(o => o.Kind == OpKind.Fill);
        Assert.Equal(new System.Numerics.Vector4(0.2f, 0.5f, 0, 0.5f), op.Colour);
    }
}
