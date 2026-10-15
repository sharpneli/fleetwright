namespace Fleetwright.Sim.Ships;

/// <summary>The propellant families of research/vfx/muzzle-flash-smoke-research.md §3.1 (muzzle_flash_ref.py
/// PROPELLANTS), as <see cref="PropellantRow"/> presets. The numbers are the research's, inferred from the sources'
/// ordering: data to tune, not measurements.</summary>
public static class Propellants
{
    /// <summary>Black (and brown) powder: the guns before smokeless powder, igniters, saluting charges. A weak, erratic
    /// flash under a huge white cloud: more than half the charge leaves as solids.</summary>
    public static readonly PropellantRow BlackPowder = new()
    {
        EnergyMJkg = 2.8, AfterburnMJkg = 1.0, Flash0 = -0.6, SecondaryK = 1700, Solids = 0.56, ExtinctionM2g = 1.5, Water = 0.02,
        TintR = 0.90, TintG = 0.89, TintB = 0.86,
    };

    /// <summary>Single-base nitrocellulose: the USN's SPD, the French poudre B, the Russian pyroxylin, IMR, M1/M6.</summary>
    public static readonly PropellantRow SingleBase = new()
    {
        EnergyMJkg = 3.8, AfterburnMJkg = 5.0, Flash0 = 1.0, SecondaryK = 1800, Solids = 0.002, ExtinctionM2g = 3.0, Water = 0.28,
        TintR = 0.86, TintG = 0.80, TintB = 0.70,
    };

    /// <summary>Double-base: cordite MD and SC, the German RPC/12, M2. The biggest flash.</summary>
    public static readonly PropellantRow DoubleBase = new()
    {
        EnergyMJkg = 4.6, AfterburnMJkg = 3.6, Flash0 = 1.6, SecondaryK = 1900, Solids = 0.001, ExtinctionM2g = 3.0, Water = 0.22,
        TintR = 0.86, TintG = 0.82, TintB = 0.74,
    };

    /// <summary>Cool double-base: diglycol (RPC/38), Gudol.</summary>
    public static readonly PropellantRow Diglycol = new()
    {
        EnergyMJkg = 3.4, AfterburnMJkg = 4.4, Flash0 = 0.0, SecondaryK = 1700, Solids = 0.001, ExtinctionM2g = 3.0, Water = 0.26,
        TintR = 0.86, TintG = 0.83, TintB = 0.77,
    };

    /// <summary>Triple-base and picrite: Cordite N/NQ, SPCG, M30/M31. Nearly flashless.</summary>
    public static readonly PropellantRow TripleBase = new()
    {
        EnergyMJkg = 3.6, AfterburnMJkg = 3.4, Flash0 = -0.9, SecondaryK = 1650, Solids = 0.004, ExtinctionM2g = 3.0, Water = 0.30,
        TintR = 0.90, TintG = 0.89, TintB = 0.86,
    };

    /// <summary>Modern nitramine (LOVA).</summary>
    public static readonly PropellantRow Lova = new()
    {
        EnergyMJkg = 4.0, AfterburnMJkg = 4.0, Flash0 = 0.6, SecondaryK = 1800, Solids = 0.0005, ExtinctionM2g = 3.0, Water = 0.25,
        TintR = 0.90, TintG = 0.90, TintB = 0.88,
    };
}
