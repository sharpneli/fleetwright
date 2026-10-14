using Fleetwright.Map;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Render;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>The ships' draw list on the CPU (docs/battle/plan.md step 2.3): a sprite close in, the stand-in icon when
/// the ship is too small for its mips or its art isn't up, nothing on the far side; one draw per class and layer; no
/// allocation per frame.</summary>
public class ShipDrawListTests
{
    static readonly Lazy<Fleetwright.Shipgen.Ship> Built = new(() => ShipDesign.Build(Design.Load(Paths.Shipgen("designs", "dreadnought.json"))));
    static readonly Lazy<Fleetwright.Sim.Ships.ShipClass> Class = new(() => Fleetwright.Sim.Ships.ShipClassCompiler.Compile(Built.Value));

    static readonly Lazy<ShipArt> Art = new(() =>
    {
        var ship = Built.Value;
        var sprites = ShipSprites.Build(ship, ShipArtLibrary.SpriteScale, ShipArtLibrary.MipLevels);
        var meta = sprites.Meta;
        var types = meta.TurretTypes.Keys.ToArray();
        return new ShipArt
        {
            Meta = meta, Hull = 1, Height = 2, HullNormal = 3, HullMaterial = 4,   // stand-in handles: nothing here touches a GPU
            TurretTypes = types, Turrets = [.. types.Select((_, i) => (nint)(10 + i))], TurretNormals = [.. types.Select((_, i) => (nint)(20 + i))],
            TurretMaterials = [.. types.Select((_, i) => (nint)(30 + i))],
            MountType = [.. meta.Mounts.Select(m => Array.IndexOf(types, m.Type))],
            DrawOrder = [.. Enumerable.Range(0, meta.Mounts.Count).OrderBy(i => meta.Mounts[i].Z)],
            ClassMount = ShipArt.ClassMounts(meta, Class.Value),
            MipLevels = ShipArtLibrary.MipLevels,
        };
    });

    const double Lat = 56.5, Lon = 4.0;
    static readonly Vec3d Noon = Earth.Up(20, 10);   // the sun high over the North Sea

    static ShipsView One(double lat, double lon)
    {
        var v = new ShipsView();
        v.EnsureCapacity(1);
        var p = Earth.Position(lat, lon);
        (v.Count, v.Entity[0], v.Class[0], v.Position[0], v.Forward[0], v.PoseMs[0]) = (1, 1, 0, p, Earth.Forward(p, 45), 0);
        return v;
    }

    static ShipDrawList Build(double mpp, ShipsView v, bool art = true)
    {
        var list = new ShipDrawList();
        var view = new MapView(MapCamera.At(Lat, Lon, mpp), 1600, 900);
        list.Build(view, v, 0, Noon, _ => art ? Art.Value : null, _ => Class.Value, 99, -1);
        return list;
    }

    [Fact]
    public void Close_in_a_ship_is_its_sprite_with_turrets_and_shadows()
    {
        var list = Build(0.5, One(Lat, Lon));
        Assert.Equal(2, list.DrawnAs[0]);
        int mounts = Art.Value.Meta.Mounts.Count;
        // the hull, each mount's shadow and turret, the sun's shadow; no icon
        Assert.Equal(1 + 2 * mounts + 1, list.Quads.Length);
        Assert.DoesNotContain(list.Draws.ToArray(), d => d.Tex0 == 99);
        Assert.Contains(list.Draws.ToArray(), d => d.Kind == ShipDrawKind.Shadow);
    }

    [Fact]
    public void Far_out_or_without_art_it_is_an_icon_and_on_the_far_side_nothing()
    {
        foreach (var list in new[] { Build(60, One(Lat, Lon)), Build(0.5, One(Lat, Lon), art: false) })
        {
            Assert.Equal(1, list.DrawnAs[0]);
            var d = Assert.Single(list.Draws.ToArray());
            Assert.Equal(99, d.Tex0);
        }
        var far = Build(5000, One(-Lat, Lon + 180));
        Assert.Equal(0, far.DrawnAs[0]);
        Assert.Empty(far.Draws.ToArray());
    }

    [Fact]
    public void Between_the_two_it_cross_fades()
    {
        // 160 m over 22 px: between FadeFromPx and FadeToPx
        double mpp = 160 / ((ShipDrawList.FadeFromPx + ShipDrawList.FadeToPx) / 2);
        Assert.Equal(3, Build(mpp, One(Lat, Lon)).DrawnAs[0]);
    }

    [Fact]
    public void Fifty_ships_of_a_class_are_a_handful_of_draws_and_a_frame_allocates_nothing()
    {
        var v = new ShipsView();
        v.EnsureCapacity(50);
        for (int i = 0; i < 50; i++)
        {
            var p = Earth.Position(Lat + 0.001 * (i / 10), Lon + 0.0015 * (i % 10));
            (v.Entity[i], v.Class[i], v.Position[i], v.Forward[i], v.PoseMs[i]) = (i + 1, 0, p, Earth.Forward(p, 30), 0);
        }
        v.Count = 50;
        var list = new ShipDrawList();
        var view = new MapView(MapCamera.At(Lat + 0.004, Lon + 0.007, 1.5), 1600, 900);
        Func<int, ShipArt?> artOf = _ => Art.Value;
        Func<int, Fleetwright.Sim.Ships.ShipClass?> classOf = _ => Class.Value;
        list.Build(view, v, 0, Noon, artOf, classOf, 99, 3);
        int types = Art.Value.Turrets.Length;
        Assert.True(list.Draws.Length <= 2 + 2 * types + 1, $"{list.Draws.Length} draws");
        long before = GC.GetAllocatedBytesForCurrentThread();
        list.Build(view, v, 1000, Noon, artOf, classOf, 99, 3);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
