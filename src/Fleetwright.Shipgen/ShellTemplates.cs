namespace Fleetwright.Shipgen;

/// <summary>The shell templates (docs/battle/shells-armour-damage.md S1): ammunition blocks by navy and year, the
/// designer's templates and the game's loadout catalogue both; the one source of `Content/Templates/shells.json` and
/// docs/shipgen/shell-templates.md (`shipgen templates` writes both). A design copies the blocks it uses into its
/// `ammunition`, never references them by name.
///
/// Sources: research/simulation/armor/04-shells-fuzes.md §1.7 (the representative shell table: W/D³, caps, fillers,
/// fuzes), §3.4-3.5 (fuze thresholds and delays by navy), armor/00 §5.4 and armor/02 §4.6 (shell quality by family),
/// damage-research/gunfire_ref.py SHELLS, docs/battle/damage/13 §2.5 and §3.2 (casing and steel), 15 §6.2 (water).
/// The mass constant is the family's W/D³ in lb/in³ (the literature's unit), converted. Numbers no source gives are
/// estimates, marked [EST] in the note. Each family's light guns (under 200 mm) get their own blocks where the navy's
/// light shells ran lighter or differently filled; a family without them fires its heavy blocks from every gun.</summary>
public static class ShellTemplates
{
    /// <summary>A template: its name, family (group), sources and the block; Light marks a light guns' block (under 200 mm)
    /// of a family whose light shells differ from its heavy ones.</summary>
    public sealed record Template(string Name, string Group, string Note, AmmoInput Value)
    {
        public bool Light { get; init; }
    }

    static Template L(Template t) => t with { Light = true };

    const double LbIn3 = 27_679.9;

    static (double Size, double Speed) SteelOf(string s) => s switch
    {
        "cast_iron" => (0.60, 0.80),
        "cast_steel" => (0.85, 0.95),
        "nicr" => (1.25, 1.00),
        "hf1" => (0.65, 1.00),
        _ => (1.00, 1.00),
    };

    /// <summary>A kind's casing shares (13 §2.5: the nose and plug [SRC Okun], the rest [EST]).</summary>
    public static CasingInput CasingOf(string kind) => kind switch
    {
        "ap" => new() { Nose = 0.33, Ogive = 0, Body = 0.55, Plug = 0.08, Tail = 0.04 },
        "common" => new() { Nose = 0.25, Ogive = 0, Body = 0.63, Plug = 0.08, Tail = 0.04 },
        _ => new() { Nose = 0.02, Ogive = 0.20, Body = 0.63, Plug = 0.07, Tail = 0.08 },
    };

    /// <summary>A kind's share of the filler alongside the cylindrical body (13 §2.5 [EST]): a common or AP shell's cavity
    /// is mostly in the body, an HE shell's runs up into the ogive.</summary>
    public static double FillerBodyOf(string kind) => kind switch { "ap" => 0.90, "common" => 0.85, _ => 0.75 };

    /// <summary>A fuze: base with a delay, or nose (fires on first contact, so no start plate); on the water its time to fire
    /// (FuzeDefaults' 0.5 ms; damage/15 §2.3).</summary>
    sealed record F(string Position, double DelayS, double StartCal = 0.07, double Oblique = 0.45, string Plates = "single", double Scatter = 0.1,
        double Blind = 0.05, double WaterMs = FuzeDefaults.WaterMs);

    static F Base(double delay, double start = 0.07, double oblique = 0.45, string plates = "single", double scatter = 0.1, double blind = 0.05,
        double waterMs = FuzeDefaults.WaterMs) =>
        new("base", delay, start, oblique, plates, scatter, blind, waterMs);

    static readonly F Nose = new("nose", 0, 0, 1, "single", 0, 0.05);

    static Template T(string name, string group, string kind, double lbIn3, double crh, double formFactor, string cap, double capShare,
        bool windscreen, (double Ob, double TD, double Q) body, double filler, string explosive, string booster, F fuze, string steel,
        string note)
    {
        var (size, speed) = SteelOf(steel);
        return new Template(name, group, note, new AmmoInput
        {
            Kind = kind, MassKKgM3 = Math.Round(lbIn3 * LbIn3 / 10) * 10, NoseCrh = crh, FormFactor = formFactor,
            Cap = new CapInput { Kind = cap, Share = cap == "none" ? null : capShare }, Windscreen = windscreen,
            Body = new BodyInput { BreakObDeg = body.Ob, BreakTD = body.TD, Quality = body.Q },
            Filler = new FillerInput { Share = filler, Body = FillerBodyOf(kind), Explosive = new ExplosiveInput { Name = explosive }, Booster = booster },
            Steel = new SteelInput { Size = size, Speed = speed }, Casing = CasingOf(kind),
            Fuze = new FuzeInput
            {
                Position = fuze.Position, DelayS = fuze.DelayS, StartCal = fuze.StartCal, Oblique = fuze.Oblique, Plates = fuze.Plates,
                Scatter = fuze.Scatter, Blind = fuze.Blind, WaterMs = fuze.WaterMs, WaterFires = FuzeDefaults.WaterFires,
            },
        });
    }

    // the body's break-up limits and quality by family (armor/00 §5.4, armor/02 §4.6): (obliquity °, T/D, quality)
    static readonly (double, double, double) Chilled = (0, 0.3, 0.85), Uncapped = (15, 0.5, 0.95), Soft = (15, 0.5, 0.95),
        Rn1914 = (15, 0.4, 0.85), Krupp1911 = (20, 0.6, 0.95), Greenboy = (20, 0.67, 0.96), Ww2 = (30, 1.0, 1.0), Km1939 = (20, 0.8, 1.0),
        UsLate = (45, 1.2, 1.10), Common = (10, 0.33, 0.9), He = (0, 0.2, 1.0);

    /// <summary>The catalogue, by family, oldest first.</summary>
    public static readonly IReadOnlyList<Template> All =
    [
        // ---- 1867: chilled iron (Armstrong's and Woolwich's muzzle-loaders)
        T("Palliser shell (1867)", "1867 chilled iron", "ap", 0.41, 1.5, 1.8, "none", 0, false, Chilled, 0.015, "Black powder", "basic",
            Base(0, blind: 0.05), "cast_iron",
            "Palliser chilled cast iron: the 12\" RML's 714 lb shot, W/D³ 0.41. ~1.5% powder ignited by the impact, no fuze. Shatters on any steel face."),
        T("Common shell, cast iron (1867)", "1867 chilled iron", "common", 0.36, 1.5, 1.8, "none", 0, false, (0, 0.15, 0.85), 0.06, "Black powder",
            "basic", Nose, "cast_iron",
            "Cast iron, ~6% powder, a percussion nose fuze (instant: useless for penetration). W/D³ [EST]."),

        // ---- 1895: forged steel, the Makarov soft cap (a navy-neutral family)
        T("Forged steel APC, soft cap (1895)", "1895 forged steel", "ap", 0.47, 2, 1.8, "soft", 0.06, false, Soft, 0.025, "Black powder", "basic",
            Base(0.005), "nicr",
            "Water-hardened Ni steel under a Makarov soft cap (~+15% below 15°, gone above 20°). W/D³ between Russia's 0.42 and the RN's 0.50 [EST]; filler and the inertial tube's ~0.005 s [EST]."),
        T("Common shell, forged steel (1895)", "1895 forged steel", "common", 0.45, 2, 1.8, "none", 0, false, Common, 0.07, "Black powder", "basic",
            Base(0), "forged", "Forged steel common, ~7% powder, base percussion fuze. [EST] from the RN's and Japan's."),

        // ---- Russia 1895: the light 'old model' shells
        T("RU 1895 AP (pyroxylin)", "RU 1895", "ap", 0.42, 2, 1.8, "none", 0, false, Uncapped, 0.016, "Wet guncotton", "basic",
            Base(0.01, blind: 0.1), "forged",
            "12\" AP 'old model' 331.7 kg (W/D³ 0.42), uncapped, 5.3 kg wet pyroxylin, Brink delayed tube (delay [EST]). Light shells did little (Tsushima)."),
        T("RU 1895 HE (pyroxylin)", "RU 1895", "common", 0.42, 2, 1.8, "none", 0, false, (10, 0.25, 0.9), 0.037, "Wet guncotton", "basic",
            Base(0.005, blind: 0.1), "forged",
            "12\" HE 'old model': thin-walled, base-fuzed (the 1894 tube, ~0.005 s), 12.4 kg pyroxylin. A common shell by its fuze."),

        // ---- Japan 1904
        T("JP 1904 AP (Shimose)", "JP 1904", "ap", 0.49, 2, 1.8, "none", 0, false, Uncapped, 0.05, "Shimose", "basic",
            Base(0, start: 0.01, blind: 0.05, waterMs: 0.1), "nicr",
            "12\" 'Furoshiki' 386 kg (W/D³ 0.49), 19.3 kg Shimose, the sensitive Ijuin base fuze: bursts on contact (it starts on any plate, 0.01 calibre [EST]), on the water too (0.1 ms: Semenov's \"exploded the moment they touched its surface\", damage/15 §2.3). No 12\" known to pierce over ~6\"."),
        T("JP 1904 common (powder)", "JP 1904", "common", 0.49, 2, 1.8, "none", 0, false, Common, 0.10, "Black powder", "basic",
            Base(0, start: 0.01), "forged", "12\" common, 39 kg powder (10%): the upper-works fires of Tsushima. The Ijuin fuze too: it starts on any plate (0.01 calibre [EST], armor/04 \"designed to burst on contact\"), so it bursts in the upper works rather than passing through."),

        // ---- USN 1906
        T("US 1906 AP (Explosive D)", "US 1906", "ap", 0.50, 2, 1.5, "soft", 0.06, false, Soft, 0.025, "Explosive D", "basic",
            Base(0.003), "nicr",
            "12\"/45 870 lb (W/D³ 0.50). Explosive D from 1907, the pre-1920s non-delay TNT-boosted base fuze (~0.003 s). Cap and filler share [EST]."),
        T("US 1906 common (powder)", "US 1906", "common", 0.50, 2, 1.5, "none", 0, false, Common, 0.06, "Black powder", "basic",
            Base(0.003), "forged", "Common shell for 1/3-1/2 calibre plate (NOG). Filler [EST]."),

        // ---- Germany 1902: the light pre-dreadnought shells
        T("DE 1902 Psgr (TNT)", "DE 1902", "ap", 0.40, 2, 1.8, "soft", 0.06, false, Krupp1911, 0.027, "TNT", "basic",
            Base(0.003, 0.05, plates: "two", blind: 0.1), "nicr",
            "28 cm/40 240 kg (W/D³ 0.40). TNT from 1902; non-delay before the 1911 Bdz. Cap, filler and fuze [EST]."),
        T("DE 1902 Sprgr Bdz (TNT)", "DE 1902", "common", 0.40, 2, 1.8, "none", 0, false, Common, 0.05, "TNT", "basic",
            Base(0.003, 0.05, plates: "two", blind: 0.1), "forged", "Base-fuzed shell. Filler [EST]."),
        T("DE 1902 Sprgr Kz (TNT)", "DE 1902", "he", 0.40, 2, 1.8, "none", 0, false, He, 0.07, "TNT", "basic", Nose, "forged",
            "Nose-fuzed shell. Filler [EST]."),

        // ---- France 1893
        T("FR 1893 AP (powder)", "FR 1893", "ap", 0.41, 2, 1.8, "none", 0, false, Uncapped, 0.025, "Black powder", "basic",
            Base(0.003), "forged",
            "Light shells: the 305 Mle 1887's 292 kg (0.37), the 340's 490 kg (0.45). Forged steel, uncapped; filler and fuze [EST]."),
        T("FR 1893 common (Melinite)", "FR 1893", "common", 0.41, 2, 1.8, "none", 0, false, Common, 0.06, "Lyddite", "basic",
            Base(0), "forged", "Melinite (picric acid, catalogued as Lyddite) from 1887. Filler [EST]."),

        // ---- RN 1906: the 12" Mk X loadout
        T("RN 1906 APC (powder)", "RN 1906", "ap", 0.49, 2, 1.5, "soft", 0.086, false, Rn1914, 0.031, "Black powder", "basic",
            Base(0.003), "nicr",
            "12\" APC Mk VI 2crh 850 lb (W/D³ 0.49), Hadfield soft cap 73 lb (8.6%), powder-filled (lyddite from 1909-10), No. 16 non-delay. Lord Nelsons kept 2crh."),
        T("RN 1906 CPC (powder)", "RN 1906", "common", 0.49, 2, 1.5, "none", 0, false, Common, 0.094, "Black powder", "basic",
            Base(0.003), "forged", "12\" CPC Mk VIIa: 80 lb powder (9.4%), base fuze."),
        T("RN 1906 HE (lyddite)", "RN 1906", "he", 0.49, 2, 1.5, "none", 0, false, He, 0.125, "Lyddite", "basic", Nose, "forged",
            "12\" HE Mk IIa: 106.5 lb lyddite (12.5%), No. 13 instantaneous nose fuze."),
        L(T("RN 1906 light CP (powder)", "RN 1906", "common", 0.475, 2, 1.5, "none", 0, false, Common, 0.07, "Black powder", "basic",
            Base(0.003), "forged",
            "The 4\"-6\" guns' common pointed (4\"/50 31 lb, 6\" 100 lb: W/D³ 0.47-0.48). Filler [EST]. Also the RN 1909 and 1912 families' light guns.")),
        L(T("RN 1906 light HE (lyddite)", "RN 1906", "he", 0.475, 2, 1.5, "none", 0, false, He, 0.09, "Lyddite", "basic", Nose, "forged",
            "The 4\"-6\" guns' lyddite HE. Filler [EST]. Also the RN 1909 and 1912 families' light guns.")),

        // ---- RN 1909: 4crh and lyddite APC, the 12" generation (12", 9.2", 7.5")
        T("RN 1909 APC (lyddite)", "RN 1909", "ap", 0.50, 4, 1.5, "soft", 0.04, true, Rn1914, 0.032, "Lyddite", "basic", Base(0.003), "nicr",

            "12\" APC Mk VIa 4crh 859 lb (W/D³ 0.50), soft cap 4%, 27.3 lb lyddite, No. 16 non-delay. Breaks up at 15-20° on 0.4 calibre; bursts in the plate (Jutland)."),
        T("RN 1909 CPC (powder)", "RN 1909", "common", 0.49, 4, 1.5, "none", 0, true, Common, 0.094, "Black powder", "basic", Base(0.003),
            "forged", "12\" CPC Mk VIIa 4crh: 80 lb powder (9.4%)."),
        T("RN 1909 HE (lyddite)", "RN 1909", "he", 0.49, 4, 1.5, "none", 0, true, He, 0.125, "Lyddite", "basic", Nose, "forged",
            "12\" HE: lyddite 12.5%, No. 13 nose fuze (withdrawn from ships after Jutland)."),

        // ---- RN 1912: the 13.5"/15" generation, the same tech, heavier
        T("RN 1912 APC (lyddite)", "RN 1912", "ap", 0.57, 4, 1.5, "soft", 0.04, true, Rn1914, 0.032, "Lyddite", "basic", Base(0.003), "nicr",
             "15\" APC Mk Ia 4crh 1,920 lb (W/D³ 0.57; the 13.5\" heavy 1,400 lb the same), 60.5 lb lyddite (3.2%), No. 16 non-delay."),
        T("RN 1912 CPC (powder)", "RN 1912", "common", 0.57, 4, 1.5, "none", 0, true, Common, 0.067, "Black powder", "basic", Base(0.003),
            "forged", "15\" CPC Mk IIa 1,929 lb, 129.3 lb powder (6.7%)."),
        T("RN 1912 HE (lyddite)", "RN 1912", "he", 0.57, 4, 1.5, "none", 0, true, He, 0.10, "Lyddite", "basic", Nose, "forged",
            "15\" HE, lyddite. Filler [EST]."),

        // ---- Germany 1911: Krupp's hard cap, TNT and the Bdz delay fuze
        T("DE 1911 Psgr L/3,4 (TNT)", "DE 1911", "ap", 0.52, 3, 1.5, "hard2", 0.08, false, Krupp1911, 0.027, "TNT", "basic",
            Base(0.025, 0.05, plates: "two", scatter: 0.5, blind: 0.15), "nicr",
            "30.5 cm Psgr. L/3,4 405 kg (W/D³ 0.52), Krupp Type 2 cap (share [EST]), 10.8 kg TNT, Bdz ~0.025 s nominal: at most half came near it, many duds."),
        T("DE 1911 Spgr L/3,8 Bdz (TNT)", "DE 1911", "common", 0.52, 3, 1.5, "none", 0, false, Common, 0.061, "TNT", "basic",
            Base(0.025, 0.05, plates: "two", scatter: 0.5, blind: 0.15), "forged",
            "30.5 cm Spgr. L/3,8 Bdz 405 kg, 24.8 kg TNT: base-fuzed, bursts 2-6 m into unarmoured structure."),
        T("DE 1911 Spgr Kz (TNT)", "DE 1911", "he", 0.52, 3, 1.5, "none", 0, false, He, 0.066, "TNT", "basic", Nose, "forged",
            "Nose-fuzed Sprenggranate (the 15 cm's 45.3 kg, 3.0 kg TNT). German light shells ran 0.49-0.54, so the family's blocks serve every gun."),

        // ---- France 1910
        T("FR 1910 OPf (Melinite)", "FR 1910", "ap", 0.55, 3, 1.5, "soft", 0.06, false, Soft, 0.03, "Lyddite", "basic", Base(0.003), "nicr",
             "305 Mle 1906 OPf 432 kg (W/D³ 0.55), melinite. Cap, filler and fuze [EST]: French fuzes weren't found."),
        T("FR 1910 OEA (Melinite)", "FR 1910", "common", 0.55, 3, 1.5, "none", 0, false, Common, 0.06, "Lyddite", "basic", Base(0.003), "forged",
             "Semi-AP explosive shell. [EST]."),
        T("FR 1910 HE (Melinite)", "FR 1910", "he", 0.55, 3, 1.5, "none", 0, false, He, 0.10, "Lyddite", "basic", Nose, "forged",
            "[EST]."),

        // ---- 1912 standard: a navy-neutral Great War family (Italy, Austria-Hungary, generic designs)
        T("Standard APC (1912)", "1912 standard", "ap", 0.52, 3, 1.5, "soft", 0.06, false, Krupp1911, 0.025, "TNT", "basic", Base(0.02), "nicr",
             "A middling Great War APC: W/D³ 0.52, soft cap, TNT 2.5%, a short delay. [EST] throughout, from the RN's and Germany's."),
        T("Standard common (1912)", "1912 standard", "common", 0.52, 3, 1.5, "none", 0, false, Common, 0.05, "TNT", "basic", Base(0.02), "forged",
             "[EST]."),
        T("Standard HE (1912)", "1912 standard", "he", 0.52, 3, 1.5, "none", 0, false, He, 0.08, "TNT", "basic", Nose, "forged", "[EST]."),

        // ---- USN 1916 and 1921
        T("US 1916 AP (Explosive D)", "US 1916", "ap", 0.51, 4, 1.5, "soft", 0.06, false, Soft, 0.025, "Explosive D", "basic", Base(0.003),
            "nicr", "14\"/45 1,400 lb (W/D³ 0.51), contractor caps, non-delay TNT-boosted base fuze. Filler [EST]."),
        T("US 1916 common (Explosive D)", "US 1916", "common", 0.51, 4, 1.5, "none", 0, false, Common, 0.05, "Explosive D", "basic", Base(0.003),
            "forged", "[EST]."),
        T("US 1916 HE (TNT)", "US 1916", "he", 0.51, 4, 1.5, "none", 0, false, He, 0.07, "TNT", "basic", Nose, "forged", "[EST]."),
        L(T("US 1916 5in common (Explosive D)", "US 1916", "common", 0.40, 3, 1.5, "none", 0, false, Common, 0.03, "Explosive D", "basic",
            Base(0.003), "forged", "5\"/51's 50 lb (W/D³ 0.40). Filler [EST]. Also the 1921 family's.")),
        L(T("US 1916 5in HE (TNT)", "US 1916", "he", 0.40, 3, 1.5, "none", 0, false, He, 0.06, "TNT", "basic", Nose, "forged",
            "[EST]. Also the 1921 family's.")),
        T("US 1921 AP Mk 3 (Explosive D)", "US 1921", "ap", 0.52, 4, 1.15, "soft", 0.06, false, Soft, 0.027, "Explosive D", "basic",
            Base(0.003), "nicr", "16\"/45 Mk 3 2,110 lb (W/D³ 0.52), 57.5 lb Explosive D (2.7%), non-delay until the late 1920s."),
        T("US 1921 HC (Explosive D)", "US 1921", "he", 0.46, 4, 1.15, "none", 0, false, He, 0.07, "Explosive D", "basic", Nose, "forged",
            "[EST] from the later HC."),

        // ---- RN 1927: Greenboy's lessons, the light 16" and 8"
        T("RN 1927 APC (Shellite)", "RN 1927", "ap", 0.50, 4, 1.15, "hard", 0.116, true, Greenboy, 0.024, "Shellite", "basic", Base(0.025),
            "nicr",
            "Greenboy practice (hard cap 11.6%, Shellite 2.4%, No. 16D 0.025 s) at the light W/D³ of Nelson's 16\" (2,048 lb, 0.50) and the 8\" (256 lb, 0.50). CE booster only from the late 1920s."),
        T("RN 1927 HE (TNT)", "RN 1927", "he", 0.50, 4, 1.15, "none", 0, true, He, 0.07, "TNT", "basic", Nose, "forged", "[EST]."),
        L(T("RN 1927 light SAP (TNT/wax)", "RN 1927", "common", 0.47, 4, 1.15, "none", 0, true, Common, 0.033, "TNT/wax", "basic", Base(0.01),
            "forged", "6\" CPBC 100 lb (W/D³ 0.47), TNT/beeswax. Filler [EST].")),
        L(T("RN 1927 light HE (TNT)", "RN 1927", "he", 0.47, 4, 1.15, "none", 0, true, He, 0.06, "TNT", "basic", Nose, "forged", "[EST].")),

        // ---- Japan 1913 and 1925
        T("JP 1913 APC (Shimose)", "JP 1913", "ap", 0.51, 4, 1.5, "soft", 0.04, true, Soft, 0.03, "Shimose", "basic", Base(0.003), "nicr",
             "Vickers 14\"/45 1,400 lb (W/D³ 0.51). Shimose to the 1920s. Filler and fuze [EST]."),
        T("JP 1913 common (Shimose)", "JP 1913", "common", 0.51, 4, 1.5, "none", 0, true, Common, 0.06, "Shimose", "basic", Base(0.003),
            "forged", "[EST]."),
        T("JP 1913 HE (Shimose)", "JP 1913", "he", 0.51, 4, 1.5, "none", 0, true, He, 0.10, "Shimose", "basic", Nose, "forged", "[EST]."),
        T("JP 1925 APC (TNT/wax)", "JP 1925", "ap", 0.54, 4, 1.15, "hard", 0.09, true, Krupp1911, 0.025, "TNT/wax", "basic",
            Base(0.03, 0.05, plates: "two", blind: 0.1), "nicr",
            "APC Model 1925 on the German pattern (wax-TNT, buffered cavity, 1922): 41 cm 1,020 kg (W/D³ 0.54). Still prematures and low-order bursts. Cap and filler [EST]; delay 0.025-0.035 (Okun)."),
        T("JP 1925 HE (TNT)", "JP 1925", "he", 0.50, 4, 1.15, "none", 0, true, He, 0.06, "TNT", "basic", Nose, "forged", "[EST]."),
        L(T("JP 1925 light common (TNT/wax)", "JP 1925", "common", 0.43, 4, 1.15, "none", 0, true, Common, 0.03, "TNT/wax", "basic", Base(0.01),
            "forged", "12.7 cm 23 kg (W/D³ 0.41) and 14 cm 38 kg (0.46). Filler [EST].")),
        L(T("JP 1925 light HE (TNT)", "JP 1925", "he", 0.43, 4, 1.15, "none", 0, true, He, 0.08, "TNT", "basic", Nose, "forged", "[EST].")),

        // ---- Russia 1911
        T("RU 1911 AP (TNT)", "RU 1911", "ap", 0.60, 4, 1.15, "hard", 0.08, true, Krupp1911, 0.027, "TNT", "basic", Base(0.025), "nicr",
            "12\" AP mod 1911 470.9 kg (W/D³ 0.60, super-heavy), 12.84 kg. Cap, explosive and fuze [EST]."),
        T("RU 1911 HE (TNT)", "RU 1911", "he", 0.60, 4, 1.15, "none", 0, true, He, 0.125, "TNT", "basic", Nose, "forged",
            "12\" HE mod 1911 471 kg, 58.8 kg TNT."),

        L(T("RU 1911 light common (TNT)", "RU 1911", "common", 0.53, 4, 1.15, "none", 0, true, Common, 0.04, "TNT", "basic", Base(0.01), "forged",
             "120/50 28.97 kg (W/D³ 0.53). Filler [EST].")),
        L(T("RU 1911 light HE (TNT)", "RU 1911", "he", 0.53, 4, 1.15, "none", 0, true, He, 0.08, "TNT", "basic", Nose, "forged", "[EST].")),

        // ---- the Second World War
        T("RN 1938 APC (Shellite)", "RN 1938", "ap", 0.58, 6, 1.05, "hard", 0.10, true, Ww2, 0.025, "Shellite", "ce", Base(0.025, 0.0625),
            "nicr", "14\" APC Mk VIIb 6crh 1,590 lb (W/D³ 0.58), Shellite 2.5% with CE, No. 159 (1\" at normal on a 16\": 0.0625 cal), 0.025 s. Cap share [EST]."),
        T("RN 1938 HE (TNT)", "RN 1938", "he", 0.58, 6, 1.05, "none", 0, true, He, 0.067, "TNT", "ce", Nose, "forged",
            "15\" HE Mk VII 879 kg, 59 kg TNT (6.7%)."),
        L(T("RN 1938 light SAP (TNT/wax)", "RN 1938", "common", 0.53, 6, 1.05, "none", 0, true, Common, 0.03, "TNT/wax", "ce", Base(0.01),
            "forged", "6\" CPBC 112 lb (W/D³ 0.52), 5.25\" 80 lb (0.56). Filler [EST].")),
        L(T("RN 1938 light HE (TNT)", "RN 1938", "he", 0.53, 6, 1.05, "none", 0, true, He, 0.07, "TNT", "ce", Nose, "forged", "[EST].")),
        T("DE 1939 Psgr L/4,4 m.Hb (TNT/wax)", "DE 1939", "ap", 0.53, 4.4, 1.05, "hard", 0.10, true, Km1939, 0.025, "TNT/wax", "basic",
            Base(0.035, 0.05, plates: "two"), "nicr",
            "38 cm Psgr. L/4,4 m.Hb 800 kg (W/D³ 0.53), hard cap and 10-calibre ballistic cap, 20.3 kg Fp, Bd.Z. 38 0.035 s; a picric booster to 1945. Cap share [EST]."),
        T("DE 1939 Spgr L/4,5 Kz (TNT)", "DE 1939", "he", 0.53, 4.4, 1.05, "none", 0, true, He, 0.083, "TNT", "basic", Nose, "forged",
            "38 cm nose-fuzed HE 800 kg, 66.2 kg TNT."),
        L(T("DE 1939 15cm Spgr Bdz (TNT)", "DE 1939", "common", 0.48, 4.4, 1.05, "none", 0, true, Common, 0.05, "TNT", "basic",
            Base(0.015, 0.05, plates: "two"), "forged", "15 cm 45.3 kg (W/D³ 0.485), 10.5 cm 15.1 kg (0.47); Bd.Z. KV 0.015 s. Filler [EST].")),
        L(T("DE 1939 15cm Spgr Kz (TNT)", "DE 1939", "he", 0.48, 4.4, 1.05, "none", 0, true, He, 0.086, "TNT", "basic", Nose, "forged",
            "15 cm Sprgr. L/4,6 KZ, 3.9 kg TNT.")),
        T("IT 1940 APC (TNT)", "IT 1940", "ap", 0.58, 4, 1.05, "hard", 0.10, true, (25, 0.9, 0.95), 0.0115, "TNT", "basic", Base(0.03), "nicr",
            "381 mm APC 884.8 kg (W/D³ 0.58), cast TNT 10.16 kg (1.15%). Uneven makers: quality 0.95. Cap and fuze [EST]."),
        T("IT 1940 HE (TNT)", "IT 1940", "he", 0.54, 4, 1.05, "none", 0, true, He, 0.07, "TNT", "basic", Nose, "forged", "[EST]."),
        L(T("IT 1940 light SAP (TNT)", "IT 1940", "common", 0.50, 4, 1.05, "none", 0, true, Common, 0.03, "TNT", "basic", Base(0.01), "forged",
            "152/55 50 kg (W/D³ 0.51), 90/50 and 100/47 0.50. Filler [EST].")),
        L(T("IT 1940 light HE (TNT)", "IT 1940", "he", 0.50, 4, 1.05, "none", 0, true, He, 0.07, "TNT", "basic", Nose, "forged", "[EST].")),
        T("FR 1936 OPfK (TNT)", "FR 1936", "ap", 0.58, 4, 1.05, "hard", 0.095, true, Ww2, 0.025, "TNT", "basic", Base(0.03), "nicr",
            "380 mm OPfK Mle 1936 884 kg (W/D³ 0.58), cap 84 kg (9.5%), 21.9 kg filler (explosive [EST]). Fuze [EST]."),
        T("FR 1936 OEA (Melinite)", "FR 1936", "he", 0.55, 4, 1.05, "none", 0, true, He, 0.08, "Lyddite", "basic", Nose, "forged", "[EST]."),
        L(T("FR 1936 light SAP (TNT)", "FR 1936", "common", 0.53, 4, 1.05, "none", 0, true, Common, 0.03, "TNT", "basic", Base(0.01), "forged",
             "152/55 Mle 1930 54 kg (W/D³ 0.555), 130/45 32 kg (0.53), 100/45 13.5 kg (0.49). Filler [EST].")),
        L(T("FR 1936 light HE (TNT)", "FR 1936", "he", 0.53, 4, 1.05, "none", 0, true, He, 0.07, "TNT", "basic", Nose, "forged", "[EST].")),
        T("US 1941 AP Mk 8 (Explosive D)", "US 1941", "ap", 0.66, 9, 1.05, "hard", 0.12, true, UsLate, 0.015, "Explosive D", "ce",
            Base(0.033, 0.094, 0.25), "nicr",
            "16\"/50 Mk 8 2,700 lb (W/D³ 0.66, super-heavy; the 8\" and 6\" SH the same), 9crh under a windscreen, 40.9 lb Explosive D (1.5%), Mk 21 BDF 0.033 s (1.5\" at 0°, 0.375\" at 65°). Cap share [EST]."),
        T("US 1941 HC (Explosive D)", "US 1941", "he", 0.48, 9, 1.05, "none", 0, true, He, 0.081, "Explosive D", "ce", Nose, "forged",
            "16\" HC Mk 13 1,900 lb (W/D³ 0.46), 8\" 0.51, 6\" 0.49: 0.48 between. 8.1% Explosive D, PDF Mk 29."),
        L(T("US 1941 5in Common (Explosive D)", "US 1941", "common", 0.44, 4, 1.05, "none", 0, true, Common, 0.049, "Explosive D", "ce", Base(0.01),
            "forged", "5\"/38 Common 24.5 kg (W/D³ 0.44), 1.2 kg Explosive D, 0.01 s.")),
        L(T("US 1941 5in AAC (Explosive D)", "US 1941", "he", 0.44, 4, 1.05, "none", 0, true, He, 0.13, "Explosive D", "ce", Nose, "forged",
            "5\"/38 AAC 25 kg, 3.3 kg Explosive D.")),
        T("JP 1931 Type 91 AP (TNA)", "JP 1931", "ap", 0.54, 4, 1.05, "break_away", 0.10, true, Greenboy, 0.016, "TNA", "ce",
            Base(0.4, 0.05, plates: "two"), "nicr",
            "46 cm Type 91 1,460 kg (W/D³ 0.54), the break-away cap head (diving: off above 45°), 23.9 kg TNA with its liner, 0.4 s quoted [UNCERTAIN]. Cap share and booster [EST]."),
        T("JP 1931 Type 0 HE (TNA)", "JP 1931", "he", 0.51, 4, 1.05, "none", 0, true, He, 0.045, "TNA", "ce", Nose, "forged",
            "46 cm Type 0 HE 1,360 kg, 61.7 kg TNA."),
        L(T("JP 1931 light common (TNA)", "JP 1931", "common", 0.44, 4, 1.05, "none", 0, true, Common, 0.03, "TNA", "ce", Base(0.01), "forged",
             "15.5 cm and 12.7 cm (W/D³ 0.41-0.48). Filler [EST].")),
        L(T("JP 1931 light HE (TNA)", "JP 1931", "he", 0.44, 4, 1.05, "none", 0, true, He, 0.08, "TNA", "ce", Nose, "forged",
            "12.7 cm Type 0 HE 23 kg, 1.88 kg TNA.")),

        // ---- after the war: dual-purpose guns
        T("DP SAP (1950)", "1950 dual-purpose", "common", 0.56, 6, 1.0, "none", 0, true, Common, 0.04, "TNT", "ce", Base(0.01), "forged",
            "The post-war 4.5\"-6\" dual-purpose guns' shells (5\"/54 31.75 kg, W/D³ 0.56). [EST]."),
        T("DP HE (1950)", "1950 dual-purpose", "he", 0.56, 6, 1.0, "none", 0, true, He, 0.10, "Comp B", "ce", Nose, "forged",
            "Comp B filled, nose fuzed. [EST]."),
    ];

    static readonly Dictionary<string, Template> byName = All.ToDictionary(t => t.Name, StringComparer.Ordinal);

    public static Template? Get(string name) => byName.GetValueOrDefault(name);

    /// <summary>The default template's family: what a battery that gives no `shells` carries.</summary>
    public const string DefaultFamily = "1912 standard";

    /// <summary>The default loadout (decision 7): a main battery 70% AP and 30% HE, a secondary 30% AP and 70% HE, of the
    /// default family.</summary>
    public static IReadOnlyList<(AmmoInput Block, string Name, double Share)> DefaultLoadout(bool main)
    {
        var ap = All.First(t => t.Group == DefaultFamily && t.Value.Kind == "ap");
        var he = All.First(t => t.Group == DefaultFamily && t.Value.Kind == "he");
        return main ? [(ap.Value, ap.Name, 0.7), (he.Value, he.Name, 0.3)] : [(ap.Value, ap.Name, 0.3), (he.Value, he.Name, 0.7)];
    }
}
