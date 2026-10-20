namespace Fleetwright.Sim.Ships;

/// <summary>The propellant families of research/vfx/muzzle-flash-smoke-research.md §3.1 (muzzle_flash_ref.py
/// PROPELLANTS), as <see cref="PropellantRow"/> presets. The numbers are the research's, inferred from the sources'
/// ordering: data to tune, not measurements. Their burning in a magazine fire (docs/battle/magazines.md V3) is
/// research/simulation/damage-research/10 §1.1's (magazine_race_ref.py PROPELLANTS): double-base as cordite MD (a solid
/// cord), single-base and black powder as sourced, diglycol, triple-base and LOVA cooler double-base kin in tubes
/// [INFERRED].</summary>
public static class Propellants
{
    /// <summary>Black (and brown) powder: the guns before smokeless powder, igniters, saluting charges. A weak, erratic
    /// flash under a huge white cloud: more than half the charge leaves as solids.</summary>
    public static readonly PropellantRow BlackPowder = new()
    {
        EnergyMJkg = 2.8, AfterburnMJkg = 1.0, Flash0 = -0.6, SecondaryK = 1700, Solids = 0.56, ExtinctionM2g = 1.5, Water = 0.02,
        TintR = 0.90, TintG = 0.89, TintB = 0.86,
        // r = 12 mm/s (P/0.1 MPa)^0.21 [SRC: Trębiński 2022]; ~1 cm grains [EST]
        BetaMmS = 19.5, AlphaHi = 0.21, AlphaLo = 0.21, KneeMPa = 0.15, ImpetusJkg = 0.29e6, DensityKgM3 = 1750, SurfaceM2Kg = 6 / (1750 * 0.01),
        WebM = 0.005, Form = PropellantForm.Grain,
    };

    /// <summary>Single-base nitrocellulose: the USN's SPD, the French poudre B, the Russian pyroxylin, IMR, M1/M6.</summary>
    public static readonly PropellantRow SingleBase = new()
    {
        EnergyMJkg = 3.8, AfterburnMJkg = 5.0, Flash0 = 1.0, SecondaryK = 1800, Solids = 0.002, ExtinctionM2g = 3.0, Water = 0.28,
        TintR = 0.86, TintG = 0.80, TintB = 0.70,
        // strand 3.12 mm/s at 3.45 MPa, 11.05 at 13.8 [SRC: DTIC ADA261009]; multi-perforated grains [EST]
        BetaMmS = 1.0, AlphaHi = 0.88, AlphaLo = 0.4, KneeMPa = 0.15, ImpetusJkg = 0.95e6, DensityKgM3 = 1600, SurfaceM2Kg = 0.3, WebM = 0.002,
        Form = PropellantForm.Tube,
    };

    /// <summary>Double-base: cordite MD and SC, the German RPC/12, M2. The biggest flash.</summary>
    public static readonly PropellantRow DoubleBase = new()
    {
        EnergyMJkg = 4.6, AfterburnMJkg = 3.6, Flash0 = 1.6, SecondaryK = 1900, Solids = 0.001, ExtinctionM2g = 3.0, Water = 0.22,
        TintR = 0.86, TintG = 0.82, TintB = 0.74,
        // cordite MD: strand 4.6/6.1/7.9 mm/s at 3.4/4.9/6.9 MPa [SRC: DSJ]; α ~0.2 below ~0.15 MPa [SRC: Kubota]; MD45 cord
        BetaMmS = 1.8, AlphaHi = 0.78, AlphaLo = 0.25, KneeMPa = 0.15, ImpetusJkg = 1.06e6, DensityKgM3 = 1580, SurfaceM2Kg = 4 / (1580 * 0.0114),
        WebM = 0.0057, Form = PropellantForm.Cord,
    };

    /// <summary>Cool double-base: diglycol (RPC/38), Gudol.</summary>
    public static readonly PropellantRow Diglycol = new()
    {
        EnergyMJkg = 3.4, AfterburnMJkg = 4.4, Flash0 = 0.0, SecondaryK = 1700, Solids = 0.001, ExtinctionM2g = 3.0, Water = 0.26,
        TintR = 0.86, TintG = 0.83, TintB = 0.77,
        // cooler than its predecessors [INFERRED, 10 §1.1 "checks"]; tubes as RP C/12's
        BetaMmS = 1.3, AlphaHi = 0.75, AlphaLo = 0.25, KneeMPa = 0.15, ImpetusJkg = 0.95e6, DensityKgM3 = 1560, SurfaceM2Kg = 0.25, WebM = 0.0025,
        Form = PropellantForm.Tube,
    };

    /// <summary>Triple-base and picrite: Cordite N/NQ, SPCG, M30/M31. Nearly flashless.</summary>
    public static readonly PropellantRow TripleBase = new()
    {
        EnergyMJkg = 3.6, AfterburnMJkg = 3.4, Flash0 = -0.9, SecondaryK = 1650, Solids = 0.004, ExtinctionM2g = 3.0, Water = 0.30,
        TintR = 0.90, TintG = 0.89, TintB = 0.86,
        // cool picrite double-base [INFERRED]
        BetaMmS = 1.2, AlphaHi = 0.75, AlphaLo = 0.25, KneeMPa = 0.15, ImpetusJkg = 1.0e6, DensityKgM3 = 1660, SurfaceM2Kg = 0.25, WebM = 0.0025,
        Form = PropellantForm.Tube,
    };

    /// <summary>Modern nitramine (LOVA).</summary>
    public static readonly PropellantRow Lova = new()
    {
        EnergyMJkg = 4.0, AfterburnMJkg = 4.0, Flash0 = 0.6, SecondaryK = 1800, Solids = 0.0005, ExtinctionM2g = 3.0, Water = 0.25,
        TintR = 0.90, TintG = 0.90, TintB = 0.88,
        // insensitive by design [EST]
        BetaMmS = 1.0, AlphaHi = 0.8, AlphaLo = 0.3, KneeMPa = 0.15, ImpetusJkg = 1.1e6, DensityKgM3 = 1650, SurfaceM2Kg = 0.25, WebM = 0.0025,
        Form = PropellantForm.Tube,
    };

    /// <summary>A propellant by the name a design's gun block gives it (Shipgen's GunTemplates.Propellants), or null.</summary>
    public static PropellantRow? Of(string? name) => name switch
    {
        "black powder" => BlackPowder,
        "single-base" => SingleBase,
        "double-base" => DoubleBase,
        "diglycol" => Diglycol,
        "triple-base" => TripleBase,
        "lova" => Lova,
        _ => null,
    };
}
