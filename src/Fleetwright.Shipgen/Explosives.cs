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
/// the closed-space difference. The rows marked [EST] in Notes are estimates.</summary>
public static class Explosives
{
    public sealed record Entry(ExplosiveInput Value, string Note);

    static Entry X(string name, double re, double qDet, double qComb, double air, double eta, double gurney, double brisance, double inPlate,
        double basic, double ce, double fire, int from, string note) =>
        new(new ExplosiveInput
        {
            Name = name, Re = re, QDet = qDet, QComb = qComb, AirM3Kg = air, Eta = eta, GurneyMs = gurney, Brisance = brisance, InPlate = inPlate,
            Full = new FullInput { Basic = basic, Ce = ce }, Fire = fire, From = from,
        }, note);

    /// <summary>The catalogue, oldest first.</summary>
    public static readonly IReadOnlyList<Entry> All =
    [
        X("Black powder", 0.55, 2.9, 2.9, 0, 0.55, 640, 0.15, 0.15, 1.00, 1.00, 1.00, 1860,
            "Burns, never detonates: always 'full', its re prices it. Gurney 640 m/s (MSIAC L-295, 90th percentile). Few big slow pieces; the most fires."),
        X("Wet guncotton", 0.85, 3.0, 7.0, 1.0, 1, 2100, 0.90, 0.08, 0.55, 0.85, 0.20, 1890,
            "Nitrocellulose at 25-30% water (Russia 1890s-1905). Heats and air [EST]: dry NC's less the water. Under-initiated by weak primers: many duds."),
        X("Lyddite", 1.17, 4.4, 11.4, 1.63, 1, 2550, 1.15, 0.35, 0.95, 0.97, 0.40, 1888,
            "Picric acid (also Melinite). in_plate 0.35 sourced (armor/04 §6, Jutland). Fine splinters."),
        X("Shimose", 1.17, 4.4, 11.4, 1.63, 1, 2600, 1.20, 0.45, 0.95, 0.97, 0.50, 1893,
            "Picric acid undiluted, lacquer and wax lined (Japan). Bore prematures and bursts on the water are 12's hidden stats, not modelled."),
        X("TNT", 1.00, 4.5, 15.0, 2.66, 1, 2438, 1.00, 0.10, 0.60, 0.95, 0.25, 1902,
            "Trotyl, Fp 02. Heats measured (LLNL 4.57, 14.96 MJ/kg), Gurney IATG 01.80. Needs a strong booster."),
        X("TNT/wax", 0.90, 4.1, 17.7, 3.6, 1, 2300, 0.90, 0.04, 0.55, 0.93, 0.25, 1905,
            "TNT with ~10% wax (Germany's Fp 5-20, the RN's post-war TNT/beeswax). Heats and air [EST]: TNT's with the wax's combustion."),
        X("Explosive D", 0.95, 3.2, 11.5, 1.87, 1, 2350, 0.90, 0.02, 0.50, 0.92, 0.20, 1911,
            "Ammonium picrate (Dunnite, USN). re [EST, unsourced]; 11 has 0.90. The most impact-insensitive filler in quantity."),
        X("Shellite", 1.00, 4.2, 12.3, 2.00, 1, 2450, 1.05, 0.00, 0.60, 0.90, 0.35, 1918,
            "Picric acid with dinitrophenol (RN Greenboy on). Full 0.90 with CE sourced; low-order without it until the late 1920s."),
        X("TNA", 1.00, 4.4, 13.6, 2.25, 1, 2440, 1.00, 0.05, 0.60, 0.93, 0.25, 1931,
            "Trinitroanisole, Type 91 bakuyaku (IJN). re [EST, unsourced]. Needed a liner of 1/3-1/2 of the cavity."),
        X("Amatol 60/40", 1.00, 4.2, 9.0, 1.0, 1, 2150, 0.85, 0.10, 0.60, 0.93, 0.15, 1915,
            "TNT 60, ammonium nitrate 40: the shortage filler. Heats and air [EST] between TNT and 80/20."),
        X("Amatol 80/20", 0.95, 4.0, 4.0, 0, 1, 1900, 0.65, 0.12, 0.55, 0.90, 0.10, 1915,
            "Ammonium nitrate 80, TNT 20: about five times the shells per ton of TNT. Oxygen-balanced: no afterburn. Brisance 0.62 from NOL's fragments."),
        X("RDX/TNT", 1.35, 5.0, 12.0, 1.70, 1, 2700, 1.35, 0.30, 0.65, 0.97, 0.25, 1943,
            "Late-war HE (the RN's 15\" HE Mk VII TNT/RDX). Gurney, brisance, in_plate and full [EST]; too sensitive for AP unless phlegmatised."),
        X("Comp B", 1.33, 5.0, 12.0, 1.70, 1, 2774, 1.41, 0.30, 0.65, 0.97, 0.25, 1943,
            "RDX 60, TNT 40. Gurney 2,774 and brisance 1.41 (IATG) sourced; heats [EST] as RDX/TNT."),
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
        };
    }

    static readonly string[] Fields = ["name", "re", "q_det", "q_comb", "air_m3_kg", "eta", "gurney_ms", "brisance", "in_plate", "full", "fire", "from"];

    /// <summary>A block's errors (at: where it is, for the messages): every number given 0 or more, the rates shares.</summary>
    public static IEnumerable<string> Errors(ExplosiveInput x, string at)
    {
        foreach (var k in x.Extra.KeysOrEmpty())
            yield return $"{at}.{k}: not an explosive field ({string.Join(", ", Fields)})";
        foreach (var (k, v) in new[] { ("re", x.Re), ("q_det", x.QDet), ("q_comb", x.QComb), ("air_m3_kg", x.AirM3Kg), ("gurney_ms", x.GurneyMs), ("brisance", x.Brisance) })
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
