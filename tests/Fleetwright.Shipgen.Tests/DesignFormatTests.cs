using System.Text.Json;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The design file's version: older files read as the current format, newer ones load with a warning.</summary>
public class DesignFormatTests
{
    static string Bismarck() => File.ReadAllText(Paths.Shipgen("designs", "bismarck.json"));

    static string WithVersion(string json, string? version)
    {
        var d = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        d.Remove("version");
        if (version != null)
            d["version"] = System.Text.Json.Nodes.JsonNode.Parse(version);
        return d.ToJsonString();
    }

    [Fact]
    public void EveryShippedDesignIsCurrent()
    {
        foreach (var p in Directory.GetFiles(Paths.Shipgen("designs"), "*.json"))
            Assert.True(JsonDocument.Parse(File.ReadAllText(p), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })
                .RootElement.TryGetProperty("version", out var v) && v.GetInt32() == DesignFormat.Current, p);
    }

    [Fact]
    public void AFileWithoutVersionIsUpgradedAndBuildsTheSame()
    {
        var old = Design.Parse(WithVersion(Bismarck(), null));
        var now = Design.Parse(Bismarck());
        Assert.Equal(DesignFormat.Current, old.Version);
        Assert.Equal(now.ToJson(), old.ToJson());
        Assert.Empty(DesignFormat.Warnings(old));
    }

    [Fact]
    public void AVersion2BlockLosesItsOutcomeRowAndKeepsItsDuds()
    {
        // version 2: each ammunition block named its outcome row; a block without a fuze blind takes the row's duds
        var d = System.Text.Json.Nodes.JsonNode.Parse(WithVersion(Bismarck(), "2"))!.AsObject();
        var blocks = d["ammunition"]!.AsObject().Select(kv => kv.Value!.AsObject()).ToList();
        foreach (var b in blocks)
            b["outcomes"] = "km1916";
        blocks[0]["fuze"]!.AsObject().Remove("blind");
        var now = Design.Parse(Bismarck());
        var old = Design.Parse(d.ToJsonString());
        Assert.Equal(DesignFormat.Current, old.Version);
        Assert.Empty(ShipDesign.Validate(old));
        string first = d["ammunition"]!.AsObject().First().Key;
        Assert.Equal(0.15, old.Ammunition![first].Fuze!.Blind);
        foreach (var (name, a) in old.Ammunition.Skip(1))
            Assert.Equal(now.Ammunition![name].Fuze!.Blind, a.Fuze!.Blind);
    }

    [Fact]
    public void ASavedDesignCarriesTheVersion()
    {
        var back = JsonDocument.Parse(Design.Parse(WithVersion(Bismarck(), null)).ToJson());
        Assert.Equal(DesignFormat.Current, back.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void ANewerDesignLoadsWithAWarning()
    {
        var d = Design.Parse(WithVersion(Bismarck(), (DesignFormat.Current + 1).ToString()));
        Assert.Equal(DesignFormat.Current + 1, d.Version);
        var ship = ShipDesign.Build(d);
        Assert.Contains(ship.Report.Warnings, w => w.Contains("newer Fleetwright"));
    }

    [Theory]
    [InlineData("\"one\"")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void ABadVersionIsRefused(string version) =>
        Assert.ThrowsAny<JsonException>(() => Design.Parse(WithVersion(Bismarck(), version)));
}
