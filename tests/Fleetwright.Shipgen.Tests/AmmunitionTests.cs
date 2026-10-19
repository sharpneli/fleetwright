namespace Fleetwright.Shipgen.Tests;

/// <summary>The shells as design data (docs/battle/shells-armour-damage.md S0): the ammunition table and the batteries'
/// loadouts validated, and resolved into the report: k·D³ unless a battery gives its own mass, the gun's velocity with
/// its heaviest shell and the lighter ones faster by the fourth root, the filler's kilograms.</summary>
public class AmmunitionTests
{
    static readonly Design Dreadnought = Design.Load(Path.Combine(Paths.Shipgen(), "designs", "dreadnought.json"));

    static AmmoInput Block(string kind = "ap", double k = 15_000) => new()
    {
        Kind = kind, MassKKgM3 = k, FormFactor = 1.1, Filler = new FillerInput { Share = 0.025, Explosive = new ExplosiveInput { Re = 1 } },
        Fuze = new FuzeInput { Position = kind == "he" ? "nose" : "base", DelayS = kind == "he" ? 0 : 0.025 },
    };

    static Design WithMain(params ShellEntryInput[] shells) => Dreadnought with
    {
        Ammunition = new() { ["heavy AP"] = Block(), ["light HE"] = Block("he", 12_000) },
        Main = [Dreadnought.Main![0] with { Shells = [.. shells], MuzzleVelocityMs = null }],
        Secondary = [Dreadnought.Secondary![0] with { Shells = null }],
    };

    [Fact]
    public void A_battery_resolves_its_loadout()
    {
        var design = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 0.7 }, new ShellEntryInput { Ammo = "light HE", Share = 0.3 });
        Assert.Empty(ShipDesign.Validate(design));
        var warnings = new List<string>();
        var all = Ammunition.Resolve(design, warnings);
        Assert.Empty(warnings);
        Assert.Equal(2, all.Count);   // the secondary gives none: it carries the default loadout
        Assert.Equal(["Standard APC (1912)", "Standard HE (1912)"], all[1].Shells.Select(s => s.Ammo));
        Assert.Equal([0.3, 0.7], all[1].Shells.Select(s => s.Share));
        var main = all[0];
        Assert.Equal(("main", 305.0, 45.0), (main.Battery, main.CalibreMm, main.CalibreLength));
        var (ap, he) = (main.Shells[0], main.Shells[1]);
        double d3 = 0.305 * 0.305 * 0.305;
        Assert.Equal(15_000 * d3, ap.MassKg, 9);
        Assert.Equal(12_000 * d3, he.MassKg, 9);
        Assert.Equal(Ammunition.DefaultMuzzleVelocity(45), ap.MuzzleVelocityMs);
        Assert.Equal(ap.MuzzleVelocityMs * Math.Pow(15.0 / 12, 0.25), he.MuzzleVelocityMs, 9);
        Assert.Equal(0.025 * ap.MassKg, ap.FillerKg, 9);
        Assert.Equal(("nose", "none"), (he.Fuze, he.Cap));
        Assert.Equal(("TNT", 1.0, "basic"), (ap.Explosive, ap.FillerRe, ap.Booster));
        // the explosive's rates with the booster, and the fuze's numbers where the block gives none (P3)
        Assert.Equal((0.10, 0.60), (ap.FillerInPlate, ap.FillerFull));
        Assert.Equal((FuzeDefaults.StartCal, FuzeDefaults.Oblique, "single", FuzeDefaults.Scatter, FuzeDefaults.Blind),
            (ap.FuzeStartCal, ap.FuzeOblique, ap.FuzePlates, ap.FuzeScatter, ap.FuzeBlind));

        // a battery's own shell and the gun's own velocity
        design = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1, MassKg = 390 });
        design = design with { Main = [design.Main![0] with { MuzzleVelocityMs = 830 }] };
        var own = Ammunition.Resolve(design, warnings)[0].Shells[0];
        Assert.Equal((390.0, 830.0), (own.MassKg, own.MuzzleVelocityMs));
    }

    /// <summary>A battery's own fuze delay (the designer's FUZE cell) over its block's; the block's where it gives none. A
    /// nose fuze takes none, and a delay is 0 or more.</summary>
    [Fact]
    public void A_battery_sets_its_own_fuze_delay()
    {
        var design = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 0.7, FuzeDelayS = 0.008 }, new ShellEntryInput { Ammo = "light HE", Share = 0.3 });
        Assert.Empty(ShipDesign.Validate(design));
        var main = Ammunition.Resolve(design, [])[0].Shells;
        Assert.Equal((0.008, 0.0), (main[0].FuzeDelayS, main[1].FuzeDelayS));
        Assert.Equal(0.025, Ammunition.Resolve(WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1 }), [])[0].Shells[0].FuzeDelayS);
        Assert.Contains(ShipDesign.Validate(WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1, FuzeDelayS = -0.01 })),
            e => e.Contains("main[0].shells[0].fuze_delay_s: 0 or more"));
        Assert.Contains(ShipDesign.Validate(WithMain(new ShellEntryInput { Ammo = "light HE", Share = 1, FuzeDelayS = 0.01 })),
            e => e.Contains("main[0].shells[0].fuze_delay_s") && e.Contains("nose fuze"));
        // the file's key
        Assert.Contains("\"fuze_delay_s\":0.008", System.Text.Json.JsonSerializer.Serialize(design.Main![0].Shells![0], ShipgenJson.Default.ShellEntryInput));
    }

    [Fact]
    public void Bad_ammunition_is_named()
    {
        static List<string> Errors(Design d) => ShipDesign.Validate(d);
        Assert.Contains(Errors(WithMain(new ShellEntryInput { Ammo = "nothing", Share = 1 })), e => e.Contains("main[0].shells[0].ammo"));
        Assert.Contains(Errors(WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 0.6 })), e => e.Contains("add up to 0.6"));
        var bad = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1 });
        bad = bad with { Ammunition = new() { ["heavy AP"] = Block() with { Kind = "shrapnel", Fuze = null } } };
        var errs = Errors(bad);
        Assert.Contains(errs, e => e.Contains("ammunition.\"heavy AP\".kind"));
        Assert.Contains(errs, e => e.Contains("ammunition.\"heavy AP\".fuze"));
    }

    [Fact]
    public void Two_batteries_of_one_gun_fire_the_first_ones_shells()
    {
        var design = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1 });
        design = design with { Main = [design.Main![0], design.Main[0] with { Shells = [new ShellEntryInput { Ammo = "light HE", Share = 1 }] }] };
        var warnings = new List<string>();
        var all = Ammunition.Resolve(design, warnings);
        Assert.Equal("ap", all[0].Shells[0].Kind);
        Assert.Contains(warnings, w => w.Contains("main[1]"));
    }

    /// <summary>Every template and explosive in the catalogues is a valid block, the explosives its fillers name are in
    /// the catalogue, and a filler naming a design's own explosive takes its numbers, what it gives inline over them.</summary>
    [Fact]
    public void The_catalogues_are_valid()
    {
        var errs = ShellTemplates.All.SelectMany(t => Ammunition.BlockErrors(Dreadnought, t.Value, t.Name))
            .Concat(Explosives.All.SelectMany(e => Explosives.Errors(e.Value, e.Value.Name!))).ToList();
        Assert.True(errs.Count == 0, string.Join("\n", errs));
        Assert.Equal(ShellTemplates.All.Count, ShellTemplates.All.Select(t => t.Name).Distinct().Count());
        Assert.Contains(ShellTemplates.All, t => t.Group == ShellTemplates.DefaultFamily && t.Value.Kind == "ap");
        Assert.Contains(ShellTemplates.All, t => t.Group == ShellTemplates.DefaultFamily && t.Value.Kind == "he");

        var design = Dreadnought with { Explosives = new() { ["Ballistite"] = Explosives.Tnt with { Name = "Ballistite", Re = 0.7 } } };
        Assert.Equal(0.7, Explosives.Resolve(design, new ExplosiveInput { Name = "Ballistite" })!.Re);
        Assert.Equal((0.8, 4.5), Explosives.Resolve(design, new ExplosiveInput { Name = "Ballistite", Re = 0.8 }) is { } x ? (x.Re!.Value, x.QDet!.Value) : default);
        Assert.Equal(1.17, Explosives.Resolve(design, new ExplosiveInput { Name = "Lyddite" })!.Re);
        Assert.Null(Explosives.Resolve(design, new ExplosiveInput { Name = "Unobtainium" }));
        var named = Dreadnought with { Ammunition = new() { ["x"] = Block() with { Filler = new FillerInput { Share = 0.02, Explosive = new ExplosiveInput { Name = "Unobtainium" } } } } };
        Assert.Contains(Ammunition.BlockErrors(named, named.Ammunition!["x"], "x"), e => e.Contains("Unobtainium"));
    }
}
