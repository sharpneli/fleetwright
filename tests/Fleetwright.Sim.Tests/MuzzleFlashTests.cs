using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The gun's flash, blast and smoke (docs/visuals/muzzle-flashes.md F1) against research/vfx/muzzle_flash_ref.py's
/// <c>derive()</c> (RH 0.7, 15 °C), and the charges the placeholders give by navy and era.</summary>
public class MuzzleFlashTests(ITestOutputHelper output)
{
    static ChargeRow Charge(double kg, PropellantRow p, double igniter, double salt = 0, double liner = 0) =>
        new() { MassKg = kg, Propellant = p, Igniter = igniter, Salt = salt, Liner = liner };

    static double Fit(double calibreMm, double calibreLength) => 4000 * Math.Pow(calibreMm / 1000, 3) * calibreLength / 50;

    /// <summary>The reference's numbers: λ_b, the expected λ_f, x_M, P_sec, t_s (of the expected λ_f), t_e, A_p, A_w.</summary>
    public static TheoryData<string, double, double, int, double, string, double, double, double, double[]> Ladder => new()
    {
        { "16\"/50 SPD", 406, 50, 1, 297, "single", 0.01, 0, 0, [17.1138, 26.8081, 8.59128, 0.972588, 0.390789, 0.0678667, 10023.3, 997.92] },
        { "12\"/45 twin", 305, 45, 2, Fit(305, 45), "double", 0.01, 0, 0, [16.1001, 22.4842, 6.74155, 0.991088, 0.327758, 0.04595, 5987.12, 539.307] },
        { "12\"/35 twin, brown powder", 305, 35, 2, Fit(305, 35), "black", 0.01, 0, 0, [12.5482, 14.3626, 5.25969, 0.62942, 0.209367, 0.0357833, 199530, 38.1328] },
        { "4\"/45 cased", 102, 45, 1, Fit(102, 45), "double", 0.003, 0, 0, [4.27352, 5.9595, 2.25455, 0.984336, 0.0868731, 0.0155, 45.4652, 10.0857] },
        { "155 mm howitzer, salted", 155, 39, 1, 12, "triple", 0.01, 0.01, 0.005, [5.76757, 5.85042, 3.08021, 0.020829, 0.0852831, 0.02035, 1257.58, 43.2] },
    };

    [Theory]
    [MemberData(nameof(Ladder))]
    public void The_model_matches_the_reference(string name, double mm, double cal, int guns, double kg, string family, double igniter, double salt,
        double liner, double[] want)
    {
        var p = family switch
        {
            "single" => Propellants.SingleBase,
            "double" => Propellants.DoubleBase,
            "black" => Propellants.BlackPowder,
            _ => Propellants.TripleBase,
        };
        var f = MuzzleFlash.Of(mm, cal, guns, Charge(kg, p, igniter, salt, liner));
        double lf = f.ExpectedFlashLengthM;
        double[] got = [f.BlastLengthM, lf, f.MachDiskM, f.SecondaryP, MuzzleFlash.SecondaryS(lf), f.EmptyS, f.SmokeM2, f.FogM2];
        output.WriteLine($"{name}: λ_b {got[0]:F2} m, λ_f {got[1]:F2} m (fireball {1.6 * lf:F1} m), x_M {got[2]:F2} m, P_sec {got[3]:F3}, " +
                         $"t_s {got[4] * 1000:F0} ms, t_e {got[5] * 1000:F1} ms, smoke {got[6]:F0} m², fog {got[7]:F0} m²");
        string[] names = ["λ_b", "λ_f", "x_M", "P_sec", "t_s", "t_e", "A_p", "A_w"];
        for (int i = 0; i < want.Length; i++)
            Assert.True(Math.Abs(got[i] - want[i]) <= 1e-3 * Math.Abs(want[i]) + 1e-9, $"{names[i]}: {got[i]} against {want[i]}");
    }

    /// <summary>Without its secondary flash a shot's fireball is the blast's length alone; with it, longer.</summary>
    [Fact]
    public void The_secondary_flash_makes_the_fireball()
    {
        var f = MuzzleFlash.Of(305, 45, 2, Charge(Fit(305, 45), Propellants.DoubleBase, 0.01));
        Assert.Equal(f.BlastLengthM, f.FlashLengthM(0), 9);
        Assert.True(f.FlashLengthM(1) > 1.3 * f.FlashLengthM(0));
    }

    /// <summary>The placeholders (decision 4): cordite for the Great War's RN and Germans, single-base for the USN,
    /// brown powder only for a Victorian short gun, the mass 4000·d³·L/50 per gun, and no charge for torpedo mounts.</summary>
    [Theory]
    [InlineData(305, 45, Era.GreatWar, "portsmouth", "double")]
    [InlineData(305, 50, Era.GreatWar, "kiel", "double")]
    [InlineData(356, 45, Era.GreatWar, "brooklyn", "single")]
    [InlineData(305, 40, Era.Victorian, "kure", "double")]
    [InlineData(305, 35, Era.Victorian, "portsmouth", "black")]
    [InlineData(380, 52, Era.Wwii, "kiel", "diglycol")]
    public void Charges_by_navy_and_era(double mm, double cal, Era era, string navy, string family)
    {
        var c = DesignerPlaceholders.Charge(mm, cal, era, navy);
        var want = family switch
        {
            "single" => Propellants.SingleBase,
            "black" => Propellants.BlackPowder,
            "diglycol" => Propellants.Diglycol,
            _ => Propellants.DoubleBase,
        };
        Assert.Equal(want, c.Propellant);
        Assert.Equal(Fit(mm, cal), c.MassKg, 9);
        Assert.Equal(0.01, c.Igniter);
    }

    [Fact]
    public void A_class_has_a_charge_for_each_gun_type()
    {
        var cls = Fleet.Get("dreadnought").Class!;
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            ref readonly var t = ref cls.TurretTypes[cls.Mounts[m].Type];
            var c = cls.Charges[cls.Mounts[m].Type];
            if (t.Shape is TurretShape.Torp or TurretShape.Tube)
            {
                Assert.Equal(0, c.MassKg);
                continue;
            }
            var f = MuzzleFlash.Of(cls, m);
            output.WriteLine($"mount {m}: {t.Barrels} × {t.CalibreMm:F0} mm/{t.CalibreLength:F0}, {c.MassKg:F1} kg a gun: fireball {1.6 * f.FlashLengthM(1):F1} m, " +
                             $"{MuzzleFlash.SecondaryS(f.FlashLengthM(1)) * 1000:F0} ms, P_sec {f.SecondaryP:F2}, smoke {f.SmokeM2:F0} m²");
            Assert.InRange(c.MassKg, 0.5 * Fit(t.CalibreMm, t.CalibreLength), 2 * Fit(t.CalibreMm, t.CalibreLength));
            Assert.True(f.SecondaryP > 0.5);
        }
    }
}
