using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A mount's flash, blast and smoke when it fires (docs/visuals/muzzle-flashes.md F1): the port of
/// research/vfx/muzzle_flash_ref.py's <c>derive()</c>, one implementation for the screens and, later, the smoke system
/// and night sighting. Every number comes from the gun (bore, length, barrels), its charge and the air:
/// <list type="bullet">
/// <item>the blast's energy 0.45·m·Q and length λ_b = (E/p₀)^⅓, summed over the mount's guns (the water's frost disc);</item>
/// <item>the Mach disk's distance x_M ≈ d·√(p_stag/p₀), ~20 calibres for every gun: the dark gap between the muzzle
/// and the fireball;</item>
/// <item>the secondary flash: its chance per shot (a logistic of the propellant, the bore, the barrel's length, salt and
/// the muzzle device), the afterburn's energy, the fireball's length λ_f = ((E_blast + E_ab)/p₀)^⅓ when it burns and
/// its life 5·λ_f/c₀;</item>
/// <item>the gun's emptying time t_e (the primary flash and the Mach disc's glow);</item>
/// <item>the smoke's extinction areas, m²: the particles' A_p and the water fog's A_w, which evaporates over τ_w.</item>
/// </list>
/// Per mount of <see cref="Guns"/> guns firing together: the energies and the smoke are the mount's, the geometry
/// (x_M, t_e) and the flash's chance each gun's.</summary>
public readonly record struct MuzzleFlash
{
    /// <summary>The air's pressure, Pa, and the speed of sound, m/s.</summary>
    public const double P0 = 101_325, C0 = 343;

    public double BoreM { get; init; }
    public double CalibreLength { get; init; }
    public int Guns { get; init; }
    /// <summary>The mount's charge, kg (every gun's).</summary>
    public double ChargeKg { get; init; }
    /// <summary>The blast's energy and the afterburn's, J, the mount's.</summary>
    public double BlastJ { get; init; }
    public double AfterburnJ { get; init; }
    /// <summary>The blast's length λ_b, m.</summary>
    public double BlastLengthM { get; init; }
    /// <summary>The Mach disk's distance from the muzzle, m.</summary>
    public double MachDiskM { get; init; }
    /// <summary>The gun's emptying time, s: the primary flash and the Mach disc's glow.</summary>
    public double EmptyS { get; init; }
    /// <summary>The chance a shot's secondary flash burns, and its peak temperature, K.</summary>
    public double SecondaryP { get; init; }
    public double SecondaryK { get; init; }
    /// <summary>The smoke's extinction areas, m² (natural): the particles', and the water fog's at its birth, which
    /// evaporates over <see cref="FogS"/>.</summary>
    public double SmokeM2 { get; init; }
    public double FogM2 { get; init; }
    public double FogS { get; init; }
    /// <summary>The smoke's tint near the muzzle, linear RGB.</summary>
    public double TintR { get; init; }
    public double TintG { get; init; }
    public double TintB { get; init; }

    /// <summary>The fireball's length λ_f, m: with the secondary flash burning at <paramref name="scale"/> of its
    /// afterburn (a shot's own scatter), or without it (0).</summary>
    public double FlashLengthM(double scale) => Math.Cbrt((BlastJ + scale * AfterburnJ) / P0);

    /// <summary>The expected fireball length (the research's ladder: the afterburn weighted by its chance).</summary>
    public double ExpectedFlashLengthM => FlashLengthM(SecondaryP);

    /// <summary>The secondary flash's life, s, for a fireball of length λ_f.</summary>
    public static double SecondaryS(double flashLengthM) => 5 * flashLengthM / C0;

    /// <summary>A shot of this gun (<see cref="FlashGlow"/>): its secondary flash burning at a scatter, or not.</summary>
    public FlashGlow.Shot Shot(bool burns, double scatter = 1)
    {
        double lf = FlashLengthM(burns ? scatter : 0);
        return new FlashGlow.Shot(BoreM, MachDiskM, EmptyS, SecondaryK, lf, burns ? SecondaryS(lf) : 0, scatter);
    }

    /// <summary>The mount's salvo as a steady light to the eye, cd (docs/battle/night.md Q2): each way the secondary flash
    /// goes (it burns or not, at its chance) by the Blondel-Rey law, its expected value.</summary>
    public double EffectiveCd
    {
        get
        {
            var dark = Shot(false);
            var lit = Shot(true);
            double without = FlashGlow.EffectiveCd(FlashGlow.LightCdS(dark, Guns), FlashGlow.Life(dark));
            double with = FlashGlow.EffectiveCd(FlashGlow.LightCdS(lit, Guns), FlashGlow.Life(lit));
            return (1 - SecondaryP) * without + SecondaryP * with;
        }
    }

    /// <summary>A mount of <paramref name="guns"/> guns of bore <paramref name="calibreMm"/> and
    /// <paramref name="calibreLength"/> calibres, firing <paramref name="charge"/>, in air of relative humidity
    /// <paramref name="humidity"/> (0-1) at <paramref name="airC"/> °C.</summary>
    public static MuzzleFlash Of(double calibreMm, double calibreLength, int guns, in ChargeRow charge, double humidity = 0.7, double airC = 15)
    {
        var p = charge.Propellant;
        double d = calibreMm / 1000, m1 = charge.MassKg, n = Math.Max(guns, 1), m = m1 * n;
        double q = p.EnergyMJkg * 1e6;
        double blast = 0.45 * m * q;
        // the muzzle pressure from the gas energy left in the bore, the stagnation at a sonic exit, the Mach disk
        // (k = 1 lands the big guns on NavWeaps' 20-25 calibres)
        double bore = Math.PI * d * d / 4 * calibreLength * d * 1.15;
        double pm = 0.25 * 0.6 * (0.45 * m1 * q) / Math.Max(bore, 1e-12);
        double xm = d * Math.Sqrt(1.8 * pm / P0);
        // the secondary flash's propensity
        double logit = p.Flash0 + 0.6 * Math.Log10(Math.Max(d, 1e-6) / 0.02) - Math.Log10(Math.Max(calibreLength, 1e-6) / 50) - charge.Salt / 0.006
            + charge.Device switch { MuzzleDevice.Hider => -1.5, MuzzleDevice.Brake => 0.3, _ => 0 };
        double chance = 1 / (1 + Math.Exp(-2 * logit));
        // the smoke: the particles take up water; the water fog condenses in damp, cold air
        double rh = Math.Clamp(humidity, 0, 1);
        double solids = p.Solids + charge.Salt + 0.56 * charge.Igniter + 0.8 * charge.Liner;
        double smoke = m * 1000 * solids * p.ExtinctionM2g * (1 + 2 * Math.Pow(rh, 4));
        double damp = Math.Clamp((rh - 0.5) / 0.5, 0, 1);
        double condensed = Math.Min(0.15 * damp * damp * (1 + Math.Max(0, (10 - airC) / 20)), 0.4);
        return new MuzzleFlash
        {
            BoreM = d, CalibreLength = calibreLength, Guns = (int)n, ChargeKg = m, BlastJ = blast, AfterburnJ = m * p.AfterburnMJkg * 1e6,
            BlastLengthM = Math.Cbrt(blast / P0), MachDiskM = xm, EmptyS = calibreLength * d / 300 + 2e-4, SecondaryP = chance,
            SecondaryK = p.SecondaryK, SmokeM2 = smoke, FogM2 = m * 1000 * p.Water * condensed * 0.5, FogS = 2 / Math.Max(1 - rh, 0.03),
            TintR = p.TintR, TintG = p.TintG, TintB = p.TintB,
        };
    }

    /// <summary>A class's mount, its turret type's guns and charge.</summary>
    public static MuzzleFlash Of(ShipClass cls, int mount, double humidity = 0.7, double airC = 15)
    {
        int type = cls.Mounts[mount].Type;
        ref readonly var t = ref cls.TurretTypes[type];
        return Of(t.CalibreMm, t.CalibreLength, t.Barrels, cls.Charges[type], humidity, airC);
    }
}
