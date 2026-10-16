namespace Fleetwright.Shipgen.Tests;

/// <summary>The shells as design data (docs/battle/shells-armour-damage.md S0): the ammunition table and the batteries'
/// outfits validated, and resolved into the report: k·D³ unless a battery gives its own mass, the gun's velocity with
/// its heaviest shell and the lighter ones faster by the fourth root, the filler's kilograms.</summary>
public class AmmunitionTests
{
    static readonly Design Dreadnought = Design.Load(Path.Combine(Paths.Shipgen(), "designs", "dreadnought.json"));

    static AmmoInput Block(string kind = "ap", double k = 15_000) => new()
    {
        Kind = kind, MassKKgM3 = k, FormFactor = 1.1, Filler = new FillerInput { Share = 0.025, Explosive = new ExplosiveInput { Re = 1 } },
        Fuze = new FuzeInput { Position = kind == "he" ? "nose" : "base", DelayS = kind == "he" ? 0 : 0.025 }, Outcomes = "rn1918",
    };

    static Design WithMain(params ShellEntryInput[] shells) => Dreadnought with
    {
        Ammunition = new() { ["heavy AP"] = Block(), ["light HE"] = Block("he", 12_000) },
        Main = [Dreadnought.Main![0] with { Shells = [.. shells], MuzzleVelocityMs = null }],
        Secondary = [Dreadnought.Secondary![0] with { Shells = null }],
    };

    [Fact]
    public void A_battery_resolves_its_outfit()
    {
        var design = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 0.7 }, new ShellEntryInput { Ammo = "light HE", Share = 0.3 });
        Assert.Empty(ShipDesign.Validate(design));
        var warnings = new List<string>();
        var all = Ammunition.Resolve(design, warnings);
        Assert.Empty(warnings);
        var main = Assert.Single(all);   // the secondary gives none: the sim's placeholder fills it
        Assert.Equal(("main", 305.0, 45.0), (main.Battery, main.CalibreMm, main.CalibreLength));
        var (ap, he) = (main.Shells[0], main.Shells[1]);
        double d3 = 0.305 * 0.305 * 0.305;
        Assert.Equal(15_000 * d3, ap.MassKg, 9);
        Assert.Equal(12_000 * d3, he.MassKg, 9);
        Assert.Equal(Ammunition.DefaultMuzzleVelocity(45), ap.MuzzleVelocityMs);
        Assert.Equal(ap.MuzzleVelocityMs * Math.Pow(15.0 / 12, 0.25), he.MuzzleVelocityMs, 9);
        Assert.Equal(0.025 * ap.MassKg, ap.FillerKg, 9);
        Assert.Equal(("nose", "none", "rn1918"), (he.Fuze, he.Cap, he.Outcomes));

        // a battery's own shell and the gun's own velocity
        design = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1, MassKg = 390 });
        design = design with { Main = [design.Main![0] with { MuzzleVelocityMs = 830 }] };
        var own = Ammunition.Resolve(design, warnings)[0].Shells[0];
        Assert.Equal((390.0, 830.0), (own.MassKg, own.MuzzleVelocityMs));
    }

    [Fact]
    public void Bad_ammunition_is_named()
    {
        static List<string> Errors(Design d) => ShipDesign.Validate(d);
        Assert.Contains(Errors(WithMain(new ShellEntryInput { Ammo = "nothing", Share = 1 })), e => e.Contains("main[0].shells[0].ammo"));
        Assert.Contains(Errors(WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 0.6 })), e => e.Contains("add up to 0.6"));
        var bad = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1 });
        bad = bad with { Ammunition = new() { ["heavy AP"] = Block() with { Kind = "shrapnel", Outcomes = null, Fuze = null } } };
        var errs = Errors(bad);
        Assert.Contains(errs, e => e.Contains("ammunition.\"heavy AP\".kind"));
        Assert.Contains(errs, e => e.Contains("ammunition.\"heavy AP\".outcomes"));
        Assert.Contains(errs, e => e.Contains("ammunition.\"heavy AP\".fuze"));
    }

    [Fact]
    public void Two_batteries_of_one_gun_fire_the_first_ones_shells()
    {
        var design = WithMain(new ShellEntryInput { Ammo = "heavy AP", Share = 1 });
        design = design with { Main = [design.Main![0], design.Main[0] with { Shells = [new ShellEntryInput { Ammo = "light HE", Share = 1 }] }] };
        var warnings = new List<string>();
        var all = Ammunition.Resolve(design, warnings);
        Assert.Equal("ap", Assert.Single(all).Shells[0].Kind);
        Assert.Contains(warnings, w => w.Contains("main[1]"));
    }
}
