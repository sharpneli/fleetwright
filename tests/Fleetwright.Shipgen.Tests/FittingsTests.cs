using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The hull's joints and the fittings' shock grade (docs/battle/shells-armour-damage.md E0): read, validated,
/// reported, and set on old designs by the format's upgrade.</summary>
public class FittingsTests
{
    static string Bismarck() => File.ReadAllText(Paths.Shipgen("designs", "bismarck.json"));

    /// <summary>Bismarck as a version 3 file: no joints, no fittings; then edit.</summary>
    static JsonObject Version3(Action<JsonObject>? edit = null)
    {
        var d = JsonNode.Parse(Bismarck())!.AsObject();
        d["version"] = 3;
        d.Remove("fittings");
        var c = d["hull"]!["construction"]!.AsObject();
        c.Remove("shell");
        c.Remove("internals");
        edit?.Invoke(d);
        return d;
    }

    [Fact]
    public void A_version_3_design_takes_its_joints_from_its_name_and_its_grade_from_its_navy_and_year()
    {
        var old = Design.Parse(Version3().ToJsonString());
        var now = Design.Parse(Bismarck());
        Assert.Equal(now.ToJson(), old.ToJson());
        Assert.Equal("riveted", old.Hull!.Construction!.Shell);
        Assert.Equal("welded", old.Hull.Construction.Internals);
        Assert.Equal("G0", old.Fittings!.ShockGrade);   // the Kriegsmarine throughout
        Assert.Empty(DesignFormat.Warnings(old));

        static string? Grade(string navy, string tech, string? style = null) => Design.Parse(Version3(d =>
        {
            d["look"]!["navy"] = navy;
            d["machinery"]!["tech"]!["name"] = tech;
            if (style != null)
                d["style"] = style;
        }).ToJsonString()).Fittings?.ShockGrade;
        Assert.Equal("G1", Grade("portsmouth", "High-pressure geared turbines (1940)"));
        Assert.Equal("G0", Grade("portsmouth", "High-pressure geared turbines (1935)"));
        Assert.Equal("G0", Grade("brooklyn", "High-pressure geared turbines (1940)"));
        Assert.Equal("G2", Grade("brooklyn", "High-pressure geared turbines (1945)"));
        Assert.Equal("G2", Grade("generic", "High-pressure geared turbines (1945)"));
        Assert.Equal("G3", Grade("kure", "Very-high-pressure geared turbines (1200 psi) (1950)"));
        Assert.Equal("G0", Grade("brooklyn", "High-pressure geared turbines (1945)", "merchant"));
    }

    [Fact]
    public void What_the_upgrade_cant_tell_is_a_warning_and_the_default()
    {
        var d = Design.Parse(Version3(d =>
        {
            d["hull"]!["construction"]!["name"] = "Something steel";
            d["machinery"]!["tech"]!["name"] = "A plant of no year";
        }).ToJsonString());
        Assert.Null(d.Hull!.Construction!.Shell);
        Assert.Null(d.Fittings);
        var ship = ShipDesign.Build(d);
        Assert.Contains(ship.Report.Warnings, w => w.StartsWith("hull.construction: its name (\"Something steel\")"));
        Assert.Contains(ship.Report.Warnings, w => w.StartsWith("fittings.shock_grade: no plant year"));
        Assert.Equal(Joint.Welded, ship.Report.Hull.Shell);
        Assert.Equal(Joint.Welded, ship.Report.Hull.Internals);
        Assert.Equal(ShockGrade.G0, ship.Report.Fittings.ShockGrade);
    }

    [Fact]
    public void The_report_carries_the_design_s_joints_and_grade()
    {
        var d = Design.Parse(Bismarck());
        d = d with { Fittings = new FittingsInput { ShockGrade = "G2" } };
        var r = ShipDesign.Build(d).Report;
        Assert.Equal(Joint.Riveted, r.Hull.Shell);
        Assert.Equal(Joint.Welded, r.Hull.Internals);
        Assert.Equal(ShockGrade.G2, r.Fittings.ShockGrade);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(r, ShipgenJson.Default.Options.GetTypeInfo(typeof(Report)))).RootElement;
        Assert.Equal("riveted", json.GetProperty("hull").GetProperty("shell").GetString());
        Assert.Equal("G2", json.GetProperty("fittings").GetProperty("shock_grade").GetString());
    }

    [Theory]
    [InlineData("""{"shock_grade": "G4"}""", null, "fittings.shock_grade")]
    [InlineData("""{"shock_grade": "1"}""", null, "fittings.shock_grade")]
    [InlineData("""{"shock_grade": "g1"}""", null, "fittings.shock_grade")]
    [InlineData("""{"lock_in": true}""", null, "fittings.lock_in")]
    [InlineData(null, "glued", "hull.construction.shell")]
    public void Bad_values_are_named(string? fittings, string? shell, string key)
    {
        var d = JsonNode.Parse(Bismarck())!.AsObject();
        if (fittings != null)
            d["fittings"] = JsonNode.Parse(fittings);
        if (shell != null)
            d["hull"]!["construction"]!["shell"] = shell;
        var errs = ShipDesign.Validate(Design.Parse(d.ToJsonString()));
        Assert.Contains(errs, e => e.StartsWith(key + ":"));
    }

    /// <summary>Each hull template's joints are what its name says, as the upgrade reads names.</summary>
    [Fact]
    public void The_hull_templates_joints_match_their_names()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Paths.Root, "Content", "Templates", "hulls.json")));
        int n = 0;
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var v = e.GetProperty("value");
            var (shell, internals) = Fittings.JointsFromName(v.GetProperty("name").GetString())!.Value;
            Assert.Equal(shell.Name(), v.GetProperty("shell").GetString());
            Assert.Equal(internals.Name(), v.GetProperty("internals").GetString());
            n++;
        }
        Assert.True(n >= 8);
    }
}
