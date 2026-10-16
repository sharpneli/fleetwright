using Fleetwright.Designer;
using Fleetwright.Shipgen;
using Part = Fleetwright.Designer.ProtectionEdits.Part;
using PE = Fleetwright.Designer.ProtectionEdits;

namespace Fleetwright.Tests;

/// <summary>The Protection tab's layers (docs/designer-ui/protection-layers.md): the parts behind the side, a belt's or
/// deck's zone and a deck's slopes added and removed validly, the layout templates put on and read back, and the
/// half-section drawn from the build at each of its stops.</summary>
public class ProtectionLayersTests
{
    public ProtectionLayersTests() => Templates.Folder = Path.Combine(Paths.Root, "Content", "Templates");

    static Design Load(string name) => Design.Load(Paths.Shipgen("designs", name + ".json"));

    static IEnumerable<(string Name, Design D)> All() =>
        Directory.GetFiles(Paths.Shipgen("designs"), "*.json").Select(p => (Path.GetFileNameWithoutExtension(p), Design.Load(p)));

    static List<ProtectionSchemeInput> Schemes() => Templates.Protection.Select(t => t.Value).ToList();

    [Fact]
    public void Inboard_parts_start_from_the_belt()
    {
        var d = PE.Remove(PE.Remove(Load("yamato"), Part.Inner), Part.Box);   // belt 410
        var inner = PE.Add(d, Part.Inner).Armour!.InnerBulkhead!;
        Assert.Equal(40, inner.Mm);
        Assert.Equal(55, inner.MagazinesMm);
        var box = PE.Add(d, Part.Box).Armour!.MagazineBox!;
        Assert.Equal(165, box.SideMm);
        Assert.Equal(100, box.CrownMm);
        Assert.Null(PE.Add(PE.Remove(d, Part.Wings), Part.Wings).Armour!.MagazineWingM);   // auto: 0.1 of the beam
    }

    [Fact]
    public void Zones_and_slopes_add_and_remove_validly_on_every_design()
    {
        foreach (var (name, d) in All())
        {
            if (!PE.Has(d, Part.Belt))
                continue;
            var z = PE.AddBeltZone(d);
            Assert.NotNull(z.Armour!.BeltMagazinesMm);
            Assert.Empty(ShipDesign.Validate(z, limits: false).Select(e => $"{name} belt zone: {e}"));
            Assert.Null(PE.RemoveBeltZone(z).Armour!.BeltMagazinesMm);
            var decks = d.Armour?.Decks ?? [];
            for (int i = 0; i < decks.Count; i++)
            {
                if (!PE.CitadelDeck(d, i))
                    continue;
                var s = PE.AddSlopes(PE.RemoveSlopes(d, i), i);
                Assert.True(s.Armour!.Decks![i].SlopeMm > 0, $"{name} deck {i} slopes");
                Assert.Empty(ShipDesign.Validate(s, limits: false).Select(e => $"{name} deck {i} slopes: {e}"));
                var dz = PE.AddDeckZone(d, i);
                Assert.Empty(ShipDesign.Validate(dz, limits: false).Select(e => $"{name} deck {i} zone: {e}"));
                Assert.Null(PE.RemoveDeckZone(dz, i).Armour!.Decks![i].MagazinesMm);
            }
        }
    }

    [Fact]
    public void A_layout_reads_back_as_itself_and_applying_it_twice_is_once()
    {
        var schemes = Schemes();
        Assert.NotEmpty(schemes);
        foreach (var name in new[] { "bismarck", "dreadnought", "nelson", "kent", "kgv", "yamato" })
        {
            var d = Load(name);
            for (int i = 0; i < schemes.Count; i++)
            {
                var x = PE.ApplyLayout(d, schemes[i]);
                Assert.Empty(ShipDesign.Validate(x, limits: false).Select(e => $"{name} + {Templates.Protection[i].Name}: {e}"));
                Assert.Equal(PE.LayoutKey(x), PE.LayoutKey(PE.ApplyLayout(x, schemes[i])));
                var (k, exact) = PE.LayoutOf(x, schemes);
                Assert.True(exact, $"{name} + {Templates.Protection[i].Name} reads as {k}");
                Assert.Equal(PE.LayoutKey(x), PE.LayoutKey(PE.ApplyLayout(x, schemes[k])));
                var plain = PE.Plain(x);
                Assert.False(PE.HasLayout(plain), $"{name} + {Templates.Protection[i].Name}, plain");
                Assert.Empty(ShipDesign.Validate(plain, limits: false).Select(e => $"{name} plain: {e}"));
            }
        }
    }

    [Fact]
    public void An_unarmoured_ship_reads_plain()
    {
        var d = Load("destroyer");
        Assert.False(PE.HasLayout(d));
        Assert.Equal(-1, PE.LayoutOf(d, Schemes()).Index);
    }

    [Fact]
    public void The_section_is_drawn_from_the_build_at_its_stops()
    {
        foreach (var name in new[] { "bismarck", "nelson", "kent", "destroyer" })
        {
            var ship = ShipDesign.Build(Load(name));
            var hb = ship.Hitboxes;
            var stops = hb.Rooms.Where(r => r.Kind is "magazine" or "engine_room" or "boiler_room" && r.Cells.Count > 0)
                .Select(r => (r.Kind, X: (r.X0 + r.X1) / 2)).ToList();
            Assert.NotEmpty(stops);
            foreach (var (kind, x) in stops)
            {
                var s = ProtectionSection.Build(ship, x, (_, _) => -1);
                Assert.NotEmpty(s.Side);
                Assert.NotEmpty(s.Cells);
                Assert.Contains(s.Cells, c => c.Kind == kind);
                Assert.All(s.Cells, c => Assert.True(c.Y0 >= 0 && c.Y1 > c.Y0, $"{name} at {x:0.0}: {c.Text}"));
            }
            var mach = stops.First(s => s.Kind != "magazine").X;
            var at = ProtectionSection.Build(ship, mach, (_, _) => -1);
            switch (name)
            {
                case "bismarck":
                    Assert.Contains(at.Armour, p => p.Kind == ProtectionSection.Kind.Belt);
                    Assert.Contains(at.Armour, p => p.Kind == ProtectionSection.Kind.Slope);
                    Assert.NotNull(at.Holding);
                    break;
                case "nelson":   // inside the shell, leaning, and its lower belt to the inner bottom
                    Assert.Contains(at.Armour, p => p.Kind == ProtectionSection.Kind.Belt);
                    Assert.Contains(at.Armour, p => p.Kind == ProtectionSection.Kind.LowerBelt);
                    Assert.True(at.Belt is { Inside: true, Lean: > 0 });
                    break;
                case "kent":   // the magazines boxed
                    var mag = ProtectionSection.Build(ship, stops.First(s => s.Kind == "magazine").X, (_, _) => -1);
                    Assert.Contains(mag.Armour, p => p.Kind == ProtectionSection.Kind.Box);
                    break;
                case "destroyer":
                    Assert.Empty(at.Armour);
                    break;
            }
        }
    }
}
