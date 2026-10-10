using Fleetwright.Designer;
using Fleetwright.Shipgen;

namespace Fleetwright.Tests;

/// <summary>The designer's edit model: knobs read and write the design faithfully, a step stays valid, the history
/// merges and undoes, the units parse, and the empty ship builds.</summary>
public class DesignerTests
{
    static IEnumerable<(string Name, Design Design)> Designs() =>
        Directory.GetFiles(Paths.Shipgen("designs"), "*.json").Order(StringComparer.Ordinal)
            .Select(p => (Path.GetFileNameWithoutExtension(p), Design.Load(p)));

    /// <summary>Writing back what a knob reads leaves the design as it was, in every shipped design.</summary>
    [Fact]
    public void Knobs_round_trip_every_design()
    {
        var failures = new List<string>();
        foreach (var (name, d) in Designs())
        {
            string json = d.ToJson();
            foreach (var k in Knobs.All(d))
            {
                // a knob that reads a value derived from defaults (superfire counts) writes it explicitly, which is
                // fine as long as the design builds the same: compare the knob's own reading instead
                Design back = k switch
                {
                    NumberKnob n when n.Get(d) is { } v => n.Set(d, v),
                    ChoiceKnob c when c.Get(d) is { } v && v != "custom" => c.Set(d, v),
                    ToggleKnob t when t.Get(d) is { } v => t.Set(d, v),
                    _ => d,
                };
                bool same = back.ToJson() == json || k is NumberKnob nk && nk.Get(back) == nk.Get(d) && k.Id.Contains("_sf_");
                if (!same)
                    failures.Add($"{name}: {k.Id}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)));
    }

    /// <summary>One step up or down on any knob never makes a valid design invalid by the input checks (the
    /// physics may still object; that's a build's job).</summary>
    [Fact]
    public void A_step_keeps_a_design_valid()
    {
        var failures = new List<string>();
        foreach (var (name, d) in Designs())
        {
            if (ShipDesign.Validate(d, limits: true).Count > 0)
                continue;
            foreach (var k in Knobs.All(d).OfType<NumberKnob>())
            {
                if (!k.AppliesTo(d) || k.Id.EndsWith("_deck"))   // moving a deck onto another's level clashes, and says so
                    continue;
                double v = k.Value(d, null) ?? k.Range(d).Lo;
                foreach (var s in new[] { 1, -1 })
                {
                    var next = k.Apply(d, v + s * k.Step(UnitSystem.Metric));
                    var errs = ShipDesign.Validate(next, limits: true);
                    if (errs.Count > 0)
                        failures.Add($"{name}: {k.Id} {(s > 0 ? "+" : "-")}: {errs[0]}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)));
    }

    [Fact]
    public void The_empty_ship_is_an_8_knot_hull_that_builds()
    {
        var d = DesignDoc.Empty();
        Assert.Equal(8, d.SpeedKn);
        Assert.Empty(ShipDesign.Validate(d, limits: true));
        var ship = ShipDesign.Build(d);
        Assert.InRange(ship.Report.Results.StandardDisplacementT, 100, 300);
        Assert.Equal(8, Knobs.Speed.Range(d).Lo);
        Assert.Equal(8, Knobs.Speed.Apply(d, 5).SpeedKn);   // the floor holds
    }

    [Fact]
    public void History_merges_a_knob_and_undoes()
    {
        var doc = new DesignDoc(DesignDoc.Empty());
        doc.Apply(Knobs.Speed.Apply(doc.Current, 9), "Speed", "speed");
        doc.Apply(Knobs.Speed.Apply(doc.Current, 10), "Speed", "speed");   // merges
        Assert.Equal(2, doc.History.Count);
        doc.Apply(Knobs.Belt.Apply(doc.Current, 50), "Belt", "belt");
        Assert.Equal(3, doc.History.Count);
        doc.Undo();
        Assert.Null(doc.Current.Armour);
        Assert.Equal(10, doc.Current.SpeedKn);
        doc.Undo();
        Assert.Equal(8, doc.Current.SpeedKn);
        Assert.False(doc.Dirty);
        doc.Redo();
        Assert.True(doc.Dirty);
        doc.Apply(Knobs.Speed.Apply(doc.Current, 12), "Speed", "speed");   // after an undo: a new entry, the redo dropped
        Assert.Equal(3, doc.History.Count);
        Assert.Equal(12, doc.Current.SpeedKn);
        doc.Reset();
        Assert.Equal(8, doc.Current.SpeedKn);
        Assert.False(doc.Dirty);
    }

    [Theory]
    [InlineData("12in", Quantity.Armour, UnitSystem.Metric, 304.8)]
    [InlineData("12", Quantity.Armour, UnitSystem.Imperial, 304.8)]
    [InlineData("305", Quantity.Calibre, UnitSystem.Imperial, 7747)]
    [InlineData("305mm", Quantity.Calibre, UnitSystem.Imperial, 305)]
    [InlineData("13.5\"", Quantity.Calibre, UnitSystem.Metric, 342.9)]
    [InlineData("6000nm", Quantity.Range, UnitSystem.Metric, 6000)]
    [InlineData("30ft", Quantity.Length, UnitSystem.Metric, 9.144)]
    public void Units_parse(string text, Quantity q, UnitSystem u, double metric)
    {
        Assert.True(Units.TryParse(text, q, u, out double v));
        Assert.Equal(metric, v, 3);
    }
}

/// <summary>The template catalogues load from Content/Templates, and every template, applied, leaves the designs valid
/// (and the plants and hulls build).</summary>
public class TemplateTests
{
    public TemplateTests() => Templates.Folder = Path.Combine(Paths.Root, "Content", "Templates");

    [Fact]
    public void Every_template_applies()
    {
        Assert.True(Templates.Plants.Count >= 50);
        Assert.True(Templates.Hulls.Count >= 8);
        Assert.Equal(6, Templates.Crew.Count);
        Assert.True(Templates.Armour.Count >= 20);
        var failures = new List<string>();
        foreach (var name in new[] { "dreadnought", "destroyer", "fleet_carrier" })
        {
            var d = Design.Load(Paths.Shipgen("designs", name + ".json"));
            void Check(string what, Design x, bool build)
            {
                var errs = ShipDesign.Validate(x, limits: true);
                if (errs.Count > 0)
                    failures.Add($"{name} {what}: {errs[0]}");
                else if (build)
                    try
                    {
                        ShipDesign.Build(x);
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{name} {what}: {e.Message}");
                    }
            }
            foreach (var t in Templates.Plants)
                Check(t.Name, d with { Machinery = (d.Machinery ?? new MachineryInput()) with { Tech = t.Value } }, build: name == "destroyer");
            foreach (var t in Templates.Hulls)
                Check(t.Name, d with { Hull = (d.Hull ?? new HullInput()) with { Construction = t.Value } }, build: true);
            foreach (var t in Templates.Crew)
                Check(t.Name, d with { Crew = (d.Crew ?? new CrewInput()) with { Standard = t.Value } }, build: false);
            foreach (var t in Templates.Armour)
                Check(t.Name, d with { Armour = (d.Armour ?? new ArmourInput()) with { Materials = Templates.MaterialsFor(d, t.Value) } }, build: false);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }
}
