namespace Fleetwright.Shipgen;

/// <summary>The explosives catalogue (docs/battle/shells-armour-damage.md decision 9): the shells' fillers by name,
/// the one source of `Content/Templates/explosives.json` and docs/shipgen/explosive-templates.md (`shipgen templates`
/// writes both). A design copies the ones its fillers name into its `explosives`; a filler naming one the design
/// doesn't carry takes the catalogue's.
/// <list type="bullet">
/// <item>re, in_plate, full and fire: docs/battle/damage/12_explosive_fillers.md §4.1 (re as armor/04 §2.1 where
/// sourced: black powder 0.55, picric 1.17, TNT 1);</item>
/// <item>the heats, air and eta: 11_blast_confinement.md §2.4 (TNT measured, the rest Meyer's, ±15%);</item>
/// <item>Gurney and brisance: 13_fragmentation.md §2.1 and §3.1 (TNT, Comp B and tetryl sourced, the rest scaled by
/// detonation velocity).</item>
/// </list>
/// Where 11 and 12 disagree on re (black powder, lyddite, Explosive D, Shellite), 12's is taken and 11's heats carry
/// the closed-space difference. The rows marked [EST] in Notes are estimates.
/// <para>As a store (docs/battle/explosions.md EX6c), research 15 §1.1 and §3.2: Held's constant (Comp B's and H-6's sourced, the
/// rest scaled by their gap pressures squared [INFERRED] or [EST]; 0: wet guncotton, which nothing from outside sets off, and
/// black powder, which is lit, not detonated) and the cook-off temperature (TNT's and Comp B's critical temperatures [SRC
/// recalled]; picric, Explosive D and amatol 500-555 K [EST]).</para></summary>
public static class Explosives
{
    public sealed record Entry(ExplosiveInput Value, string Note);

    static Entry X(string name, double re, double qDet, double qComb, double air, double eta, double gurney, double brisance, double inPlate,
        double basic, double ce, double fire, int from, double heldK, double cookK, string note) =>
        new(new ExplosiveInput
        {
            Name = name, Re = re, QDet = qDet, QComb = qComb, AirM3Kg = air, Eta = eta, GurneyMs = gurney, Brisance = brisance, InPlate = inPlate,
            Full = new FullInput { Basic = basic, Ce = ce }, Fire = fire, From = from, HeldK = heldK, CookK = cookK,
        }, note);

    /// <summary>The catalogue, oldest first.</summary>
    public static readonly IReadOnlyList<Entry> All =
    [
        X("Black powder", 0.55, 2.9, 2.9, 0, 0.55, 640, 0.15, 0.15, 1.00, 1.00, 1.00, 1860, 0, 0,
            "Burns, never detonates: always 'full', its re prices it. Gurney 640 m/s (MSIAC L-295, 90th percentile). Few big slow pieces; the most fires."),
        X("Wet guncotton", 0.85, 3.0, 7.0, 1.0, 1, 2100, 0.90, 0.08, 0.55, 0.85, 0.20, 1890, 0, 0,
            "Nitrocellulose at 25-30% water (Russia 1890s-1905; warheads and mines to ~1915). Heats and air [EST]: dry NC's less the water. Under-initiated by weak primers: many duds. " +
            "As a store nothing sets it off from outside, nor lights it while wet (15 §1.1: only its dry primer goes)."),
        X("Lyddite", 1.17, 4.4, 11.4, 1.63, 1, 2550, 1.15, 0.35, 0.95, 0.97, 0.40, 1888, 26, 530,
            "Picric acid (also Melinite). in_plate 0.35 sourced (armor/04 §6, Jutland). Fine splinters. Held 26 [INFERRED], cook-off 530 K [EST]."),
        X("Shimose", 1.17, 4.4, 11.4, 1.63, 1, 2600, 1.20, 0.45, 0.95, 0.97, 0.50, 1893, 26, 530,
            "Picric acid undiluted, lacquer and wax lined (Japan). Bore prematures and bursts on the water are 12's hidden stats, not modelled. Held and cook-off as lyddite's."),
        X("TNT", 1.00, 4.5, 15.0, 2.66, 1, 2438, 1.00, 0.10, 0.60, 0.95, 0.25, 1902, 52, 561,
            "Trotyl, Fp 02. Heats measured (LLNL 4.57, 14.96 MJ/kg), Gurney IATG 01.80. Needs a strong booster. Held 52 (cast) [INFERRED], cook-off 561 K [SRC recalled]."),
        X("TNT/wax", 0.90, 4.1, 17.7, 3.6, 1, 2300, 0.90, 0.04, 0.55, 0.93, 0.25, 1905, 52, 561,
            "TNT with ~10% wax (Germany's Fp 5-20, the RN's post-war TNT/beeswax). Heats and air [EST]: TNT's with the wax's combustion. Held and cook-off TNT's [EST]."),
        X("Explosive D", 0.95, 3.2, 11.5, 1.87, 1, 2350, 0.90, 0.02, 0.50, 0.92, 0.20, 1911, 115, 530,
            "Ammonium picrate (Dunnite, USN). re [EST, unsourced]; 11 has 0.90. The most impact-insensitive filler in quantity. Held 115, cook-off 530 K [EST]."),
        X("TNT/HND", 1.10, 4.6, 13.5, 2.3, 1, 2500, 1.10, 0.10, 0.60, 0.95, 0.25, 1912, 34, 540,
            "TNT 60, hexanitrodiphenylamine 40: Germany's hexanite (torpedoes and mines from about 1912) and the IJN's Type 97 (from 1937). " +
            "Between TNT and tetryl: re, heats, Gurney and brisance [EST]; Held 34 and cook-off 540 K [EST] (15 §1.1: the IJN's own sources disagree on its sensitivity)."),
        X("Shellite", 1.00, 4.2, 12.3, 2.00, 1, 2450, 1.05, 0.00, 0.60, 0.90, 0.35, 1918, 38, 530,
            "Picric acid with dinitrophenol (RN Greenboy on). Full 0.90 with CE sourced; low-order without it until the late 1920s. Held 38 (\"less sensitive\" than lyddite), cook-off 530 K [EST]."),
        X("TNA", 1.00, 4.4, 13.6, 2.25, 1, 2440, 1.00, 0.05, 0.60, 0.93, 0.25, 1931, 52, 561,
            "Trinitroanisole, Type 91 bakuyaku (IJN). re [EST, unsourced]. Needed a liner of 1/3-1/2 of the cavity. Held and cook-off TNT's [EST]."),
        X("Amatol 60/40", 1.00, 4.2, 9.0, 1.0, 1, 2150, 0.85, 0.10, 0.60, 0.93, 0.15, 1915, 95, 530,
            "TNT 60, ammonium nitrate 40: the shortage filler. Heats and air [EST] between TNT and 80/20. Held 95, cook-off 530 K [EST]."),
        X("Amatol 80/20", 0.95, 4.0, 4.0, 0, 1, 1900, 0.65, 0.12, 0.55, 0.90, 0.10, 1915, 95, 530,
            "Ammonium nitrate 80, TNT 20: about five times the shells per ton of TNT. Oxygen-balanced: no afterburn. Brisance 0.62 from NOL's fragments. Held 95, cook-off 530 K [EST]."),
        X("Torpex", 1.50, 5.6, 16.0, 2.0, 1, 2650, 1.30, 0.30, 0.65, 0.97, 0.30, 1942, 16, 487,
            "RDX 42, TNT 40, aluminium 18: the RN's and USN's torpedoes, mines and depth charges from 1942-43. re 1.5 under water; heats, Gurney and brisance [EST]. " +
            "Held 16 and cook-off 487 K as Comp B's [SRC]."),
        X("RDX/TNT", 1.35, 5.0, 12.0, 1.70, 1, 2700, 1.35, 0.30, 0.65, 0.97, 0.25, 1943, 16, 487,
            "Late-war HE (the RN's 15\" HE Mk VII TNT/RDX). Gurney, brisance, in_plate and full [EST]; too sensitive for AP unless phlegmatised. Held and cook-off Comp B's."),
        X("Comp B", 1.33, 5.0, 12.0, 1.70, 1, 2774, 1.41, 0.30, 0.65, 0.97, 0.25, 1943, 16, 487,
            "RDX 60, TNT 40. Gurney 2,774 and brisance 1.41 (IATG) sourced; heats [EST] as RDX/TNT. Held 16 [SRC], cook-off 487 K [SRC recalled]."),
    ];

    static readonly Dictionary<string, ExplosiveInput> byName = All.ToDictionary(e => e.Value.Name!, e => e.Value, StringComparer.Ordinal);

    public static ExplosiveInput? Get(string name) => byName.GetValueOrDefault(name);

    public static ExplosiveInput Tnt => byName["TNT"];

    /// <summary>A filler's explosive resolved: the named block (the design's, else the catalogue's; a nameless one
    /// starts from TNT's) with what the filler gives over it. Null when the name is in neither.</summary>
    public static ExplosiveInput? Resolve(Design design, ExplosiveInput? given)
    {
        if (given is null)
            return Tnt;
        var basis = given.Name is { } n ? design.Explosives?.GetValueOrDefault(n) ?? Get(n) : Tnt;
        if (basis is null)
            return null;
        return new ExplosiveInput
        {
            Name = given.Name ?? basis.Name, Re = given.Re ?? basis.Re, QDet = given.QDet ?? basis.QDet, QComb = given.QComb ?? basis.QComb,
            AirM3Kg = given.AirM3Kg ?? basis.AirM3Kg, Eta = given.Eta ?? basis.Eta, GurneyMs = given.GurneyMs ?? basis.GurneyMs,
            Brisance = given.Brisance ?? basis.Brisance, InPlate = given.InPlate ?? basis.InPlate,
            Full = new FullInput { Basic = given.Full?.Basic ?? basis.Full?.Basic, Ce = given.Full?.Ce ?? basis.Full?.Ce },
            Fire = given.Fire ?? basis.Fire, From = given.From ?? basis.From,
            // a store's two (EX6c) came after the designs' copies: a block without them takes its catalogue namesake's, else TNT's
            HeldK = given.HeldK ?? basis.HeldK ?? Get(basis.Name ?? "")?.HeldK ?? Tnt.HeldK,
            CookK = given.CookK ?? basis.CookK ?? Get(basis.Name ?? "")?.CookK ?? Tnt.CookK,
        };
    }

    static readonly string[] Fields =
        ["name", "re", "q_det", "q_comb", "air_m3_kg", "eta", "gurney_ms", "brisance", "in_plate", "full", "fire", "from", "held_k", "cook_k"];

    /// <summary>A block's errors (at: where it is, for the messages): every number given 0 or more, the rates shares.</summary>
    public static IEnumerable<string> Errors(ExplosiveInput x, string at)
    {
        foreach (var k in x.Extra.KeysOrEmpty())
            yield return $"{at}.{k}: not an explosive field ({string.Join(", ", Fields)})";
        foreach (var (k, v) in new[]
                 {
                     ("re", x.Re), ("q_det", x.QDet), ("q_comb", x.QComb), ("air_m3_kg", x.AirM3Kg), ("gurney_ms", x.GurneyMs), ("brisance", x.Brisance),
                     ("held_k", x.HeldK), ("cook_k", x.CookK),
                 })
            if (v is < 0 || v is double d && !double.IsFinite(d))
                yield return $"{at}.{k}: a number, 0 or more";
        foreach (var (k, v) in new[] { ("eta", x.Eta), ("in_plate", x.InPlate), ("fire", x.Fire), ("full.basic", x.Full?.Basic), ("full.ce", x.Full?.Ce) })
            if (v is < 0 or > 1)
                yield return $"{at}.{k}: a share, 0 to 1";
        if (x.Full is { } f)
            foreach (var k in f.Extra.KeysOrEmpty())
                yield return $"{at}.full.{k}: not a booster (basic, ce)";
    }
}
