using Fleetwright.Designer;
using Fleetwright.Shipgen;
using Part = Fleetwright.Designer.ProtectionEdits.Part;
using PE = Fleetwright.Designer.ProtectionEdits;

namespace Fleetwright.Tests;

/// <summary>The Protection tab's edits: a part added from the belt or removed by thickness 0, decks kept in order, and the
/// coverage presets, which move extents but keep every thickness. Every edit leaves the design valid.</summary>
public class ProtectionEditsTests
{
    static Design Load(string name) => Design.Load(Paths.Shipgen("designs", name + ".json"));

    static IEnumerable<(string Name, Design D)> All() =>
        Directory.GetFiles(Paths.Shipgen("designs"), "*.json").Select(p => (Path.GetFileNameWithoutExtension(p), Design.Load(p)));

    [Fact]
    public void Every_part_adds_and_removes_validly_on_every_design()
    {
        foreach (var (name, d) in All())
            foreach (var p in PE.Parts)
            {
                var added = PE.Has(d, p) ? d : PE.Add(d, p);
                Assert.True(PE.Has(added, p), $"{name} {p} added");
                Assert.Empty(ShipDesign.Validate(added, limits: false).Select(e => $"{name} + {p}: {e}"));
                var gone = PE.Remove(added, p);
                Assert.False(PE.Has(gone, p), $"{name} {p} removed");
                Assert.Empty(ShipDesign.Validate(gone, limits: false).Select(e => $"{name} - {p}: {e}"));
            }
    }

    [Fact]
    public void Added_parts_start_from_the_belt()
    {
        var d = PE.Remove(PE.Remove(PE.Remove(Load("yamato"), Part.Upper), Part.EndFore), Part.EndAft);   // belt 410
        Assert.Equal(210, PE.Add(d, Part.Upper).Armour!.UpperBelt!.Mm);
        Assert.Equal(160, PE.Add(d, Part.EndFore).Armour!.EndBelts!.Fore!.Mm);
        Assert.Equal(1, PE.Add(d, Part.EndAft).Armour!.EndBelts!.Aft!.Reach);
        Assert.Equal(290, PE.Add(d, Part.Bulkheads).Armour!.BulkheadMm);
    }

    [Fact]
    public void Removing_keeps_the_rest_of_the_entry()
    {
        var d = Load("dreadnought");
        var gone = PE.Remove(d, Part.Upper);
        Assert.Equal(d.Armour!.UpperBelt!.Extent, gone.Armour!.UpperBelt!.Extent);
        Assert.Equal(0, gone.Armour.UpperBelt.Mm);
    }

    [Fact]
    public void A_deck_goes_in_deck_order()
    {
        var d = Load("bismarck");   // main deck (0) and third deck (2)
        var x = PE.AddDeck(d, 1, "citadel", 25, out int i);
        Assert.Equal(1, i);
        Assert.Equal([0, 1, 2], x.Armour!.Decks!.Select(k => k.Deck ?? 0));
        Assert.Empty(ShipDesign.Validate(x, limits: false));
        Assert.Equal(2, PE.RoofDeck(x));
    }

    [Fact]
    public void Presets_read_back_as_themselves_and_keep_thicknesses()
    {
        foreach (var name in new[] { "bismarck", "dreadnought", "yamato", "lord_nelson", "heavy_cruiser", "kongo" })
        {
            var d = Load(name);
            foreach (var c in PE.Coverages)
            {
                var x = PE.Apply(d, c, 5);
                Assert.Equal(c, PE.Reads(x));
                Assert.Empty(ShipDesign.Validate(x, limits: false).Select(e => $"{name} {c}: {e}"));
                Assert.Equal(d.Armour?.BeltMm, x.Armour?.BeltMm);
                Assert.Equal(d.Armour?.BulkheadMm, x.Armour?.BulkheadMm);
                if (PE.Has(d, Part.Upper))
                    Assert.Equal(d.Armour!.UpperBelt!.Mm, x.Armour!.UpperBelt!.Mm);
                // the decks over the citadel keep their thickness
                foreach (var k in (d.Armour?.Decks ?? []).Where(k => (k.Extent ?? "citadel") == "citadel"))
                    Assert.Contains(x.Armour!.Decks!, y => y.Deck == k.Deck && y.Mm == k.Mm);
            }
        }
    }

    [Fact]
    public void All_or_nothing_drops_the_ends()
    {
        var x = PE.Apply(Load("dreadnought"), PE.Coverage.AllOrNothing, 5);
        Assert.False(PE.Has(x, Part.EndFore));
        Assert.False(PE.Has(x, Part.EndAft));
        Assert.DoesNotContain(x.Armour!.Decks!, k => k.Extent is "fore" or "aft" or "ends");
    }

    [Fact]
    public void Describe_names_what_a_preset_does()
    {
        var d = Load("yamato");
        var lines = PE.Describe(d, PE.Apply(d, PE.Coverage.ArmouredEnds, 5));
        Assert.Contains("adds the end belt, fore 160 mm", lines);
        Assert.Contains(lines, l => l.StartsWith("adds the third deck, both ends", StringComparison.Ordinal));
        Assert.Empty(PE.Describe(d, PE.Apply(d, PE.Coverage.AllOrNothing, 5)));
    }
}
