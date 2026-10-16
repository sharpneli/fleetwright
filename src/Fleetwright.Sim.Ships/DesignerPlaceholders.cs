namespace Fleetwright.Sim.Ships;

/// <summary>DESIGNER PLACEHOLDERS: facts a class needs that the designer (shipgen) doesn't give yet. Each is a rough
/// stand-in, here and nowhere else, so they're easy to find and to replace when shipgen derives them (the user,
/// 2026-10-12: "if you need something from the designer and it ain't there, put in a placeholder and flag it"). The
/// list is in docs/battle/plan.md, "Needed from the designer". A replaced placeholder is deleted from here.</summary>
public static class DesignerPlaceholders
{
    /// <summary>PLACEHOLDER (docs/battle/plan.md "Needed from the designer for sighting"): a lookout at each masthead,
    /// this far under its top, m. Shipgen has no spotting or fighting top as a part, and its masts are all the funnels'
    /// top + 6 m (a destroyer's as high as Bismarck's over the deck); the designer should place tops by era and size.</summary>
    public const double MastLookoutUnderTopM = 1.0;

    /// <summary>PLACEHOLDER: a rangefinder's magnification from its base, m (the designer gives only the base): 10 + 4 per
    /// metre, kept to 12-30 (Barr &amp; Stroud's 9 ft FQ ~15-20x, 15 ft ~25x; the 1930s' long bases 25-30x).</summary>
    public static double RangefinderMagnification(double baseM) => Math.Clamp(10 + 4 * baseM, 12, 30);

    /// <summary>Fills what the design didn't give (moving).</summary>
    public static MotionFacts Fill(MotionFacts m) => m with
    {
        TacticalDiameterM = m.TacticalDiameterM > 0 ? m.TacticalDiameterM : TacticalDiameter(m),
        RudderRateDegS = m.RudderRateDegS > 0 ? m.RudderRateDegS : Math.Clamp(2.3 * 100 / m.LengthM, 2.3, 10),
        HardOverDeg = m.HardOverDeg > 0 ? m.HardOverDeg : 35,
        PowerRampS = m.PowerRampS > 0 ? m.PowerRampS : 1.93 * Math.Pow(m.FullDisplacementT, 0.47),
        PowerCutS = m.PowerCutS > 0 ? m.PowerCutS : 15,
    };

    // ------------------------------------------------------------------ the weapons (docs/battle/fighting.md 4.0)

    /// <summary>PLACEHOLDER: working a gun, from its calibre and length, its mount's shape and the era.
    /// <list type="bullet">
    /// <item>reload 7·(d/102 mm)^1.5 s, at most 36 s: the 4"'s ~8 rounds a minute, the 8"'s ~3, the heavy guns' ~2
    /// (research/simulation/manual-gunnery-research.md §4, fire-control-research/early-hit-rates.md: 12" about 2 a
    /// minute in 1906, the KGV's 14" cycles 26-30 s); ×1.1 in the Great War, 0.9 in the Second, 0.5 for the Cold War's
    /// automatic guns; before 1906 heavy guns are slower (12"/35 70-100 s, the muzzle-loaders minutes): up to ×1.3,
    /// ×2.5 under 40 calibres and ×4 under 30, growing from 150 to 300 mm;</item>
    /// <item>training 2·(381 mm/d)^1.5 °/s (fire-control-research.md §4's mount rates: RN 15" 2, 8" 6.7, 6" 10), ×0.7
    /// before 1906, ×1.2 between the wars, ×1.5 in the Second (USN 16" 4, 38 cm 5), ×3 in the Cold War, ×1.6 for a
    /// dual-purpose mount from the treaty years on (5"/38 25); elevating twice as fast, 2-20 °/s;</item>
    /// <item>the highest elevation: 13° before 1906, 18° in the Great War (RN 13.5" and 15" 20°, German 13.5-16°), 30°
    /// between the wars, 40° in the Second, 45° after and on dual-purpose mounts (from the treaty years: Shipgen draws
    /// some earlier light guns in a dp shape).</item>
    /// </list>
    /// The designer should give these per mount type from its machinery (hydraulic or electric, all-angle loading).</summary>
    public static (double ReloadS, double TrainDegS, double ElevateDegS, double MaxElevationDeg) Gun(double calibreMm, double calibreLength,
        TurretShape shape, Era era)
    {
        double reload = Math.Min(7 * Math.Pow(calibreMm / 102, 1.5), 36);
        if (era == Era.Victorian)
        {
            double slow = calibreLength < 30 ? 4 : calibreLength < 40 ? 2.5 : 1.3;
            reload *= 1 + (slow - 1) * Math.Clamp((calibreMm - 150) / 150, 0, 1);
        }
        else
            reload *= era switch { Era.GreatWar => 1.1, Era.Treaty => 1.0, Era.Wwii => 0.9, _ => 0.5 };
        bool dp = shape == TurretShape.Dp && era >= Era.Treaty;
        double train = Math.Clamp(2 * Math.Pow(381 / calibreMm, 1.5)
            * era switch { Era.Victorian => 0.7, Era.GreatWar => 1.0, Era.Treaty => 1.2, Era.Wwii => 1.5, _ => 3.0 } * (dp ? 1.6 : 1), 0.5, 60);
        double maxElev = dp ? 45 : era switch { Era.Victorian => 13, Era.GreatWar => 18, Era.Treaty => 30, Era.Wwii => 40, _ => 45 };
        return (Math.Max(reload, 0.5), train, Math.Clamp(2 * train, 2, 20), maxElev);
    }

    // ------------------------------------------------------------------ magazines going up (docs/visuals/magazine-explosions.md)

    /// <summary>PLACEHOLDER: the share of a magazine's propellant that burns in the cascade window, the fireball's M
    /// (research/vfx/magazine-explosion-vfx-research.md §2.2: "not the whole magazine"). Vidgen's tuning value: Queen
    /// Mary's forward group fits ~0.45, Invincible's ~0.17. The venting race (fighting.md 4.5) should give M.</summary>
    public const double MagazineFastShare = 0.3;

    /// <summary>PLACEHOLDER: the propellant's share of a round's weight when a magazine serves no gun with a charge
    /// (vidgen's 380/52's, 23%).</summary>
    public const double MagazinePropellantShare = 0.23;

    /// <summary>PLACEHOLDER: what a magazine holds, tonnes, when the design doesn't say: 0.5 t a cubic metre of its
    /// volume (charges and shells stowed with their racks and passages).</summary>
    public static double MagazineTonnes(double volumeM3) => 0.5 * Math.Max(volumeM3, 0);

    /// <summary>PLACEHOLDER (docs/visuals/muzzle-flashes.md, "What the model takes", decision 4): a gun's charge, from its
    /// calibre and length, the look's era and navy.
    /// <list type="bullet">
    /// <item>its mass 4000·d³·(L/50) kg (d in m; research/vfx/muzzle-flash-smoke-research.md §2.1: the 20 mm to the 80 cm
    /// within ~15%: 5"/38 6.3 against 7, 16"/50 268 against 297);</item>
    /// <item>its propellant: black (brown) powder for a Victorian gun under 36 calibres, the short guns of the
    /// brown-powder years (the 12"/35, the 13.5"/30; the designs carry an era, not a year, and the long guns came with
    /// smokeless powder); single-base for the USN (brooklyn), France (toulon, poudre B) and Russia (kronstadt,
    /// pyroxylin); diglycol for Germany (kiel) from the treaty years (RPC/38); triple-base in the Cold War; double-base
    /// cordite and its kin otherwise (the RN's MD and SC, the German RPC/12, Japan's and Italy's);</item>
    /// <item>no flash-reducer salt or wear reducer (they came between the wars and later), a black-powder igniter of 1%
    /// for a bag gun and 0.3% for a cased one (120 mm and under), and no muzzle device.</item>
    /// </list>
    /// The designer, or a later ammunition system, should give the charge per gun and the propellant by navy and
    /// year, and the flashless night charge as a choice.</summary>
    public static ChargeRow Charge(double calibreMm, double calibreLength, Era era, string navy)
    {
        double d = calibreMm / 1000;
        var propellant = era switch
        {
            Era.Victorian when calibreLength < 36 => Propellants.BlackPowder,
            Era.ColdWar => Propellants.TripleBase,
            _ when navy is "brooklyn" or "toulon" or "kronstadt" => Propellants.SingleBase,
            Era.Treaty or Era.Wwii when navy == "kiel" => Propellants.Diglycol,
            _ => Propellants.DoubleBase,
        };
        return new ChargeRow
        {
            MassKg = 4000 * d * d * d * (calibreLength / 50), Propellant = propellant, Igniter = calibreMm <= 120 ? 0.003 : 0.01,
        };
    }

    /// <summary>PLACEHOLDER: a gun's full-salvo pattern, % of the range (fire-control-research.md §7, fire_control_ref.py
    /// GUNS): the Great War's heavy guns 3.3·(305 mm/d)³ (the 12" 3.3-3.5%, the 13.5" 2.5, the 15" 1.7, "the most
    /// accurate battleship gun"), its lighter guns 2.5; before 1906 3.5; from the treaty years 1.9 for the heavy guns
    /// (16" 1.9, 14" 2.0; delay coils and stiffer mounts) and 1.8 for the lighter (8" 2.0, 5"/38 1.7), the Cold War's 1.5.
    /// The designer should give it from the gun and its mount (wear, delay coils, mixed rifling).</summary>
    public static double Pattern(double calibreMm, Era era) => era switch
    {
        Era.Victorian => 3.5,
        Era.GreatWar => calibreMm >= 280 ? Math.Clamp(3.3 * Math.Pow(305 / calibreMm, 3), 1.5, 3.5) : 2.5,
        Era.Treaty or Era.Wwii => calibreMm >= 280 ? 1.9 : 1.8,
        _ => 1.5,
    };

    /// <summary>PLACEHOLDER (fighting.md decision 6: the level is the design's choice, per battery): the fire-control
    /// level read from today's fit, its directors, the best rangefinder among them, and the plotting room's computer
    /// and radar tonnage (research/simulation/fire-control-designer.md §3.3, fire_control_ref.py LEVELS):
    /// <list type="bullet">
    /// <item>no director: eyeball, the mounts in local control;</item>
    /// <item>radar: rangekeeper with radar ranging when there's a rangefinder too, auto-track without;</item>
    /// <item>no computer: a rangefinder (or eyeball without one); under 1 t a clock (Dumaresq, Vickers); under 5 t a
    /// Dreyer table and director; under 8 t the interwar table; more, a rangekeeper;</item>
    /// <item>never past the era's best: rangefinders before 1906, the Dreyer in the Great War, the table between the
    /// wars, radar spotting in the Second.</item>
    /// </list>
    /// The designer should offer the level (computer, mount and propellant tech) per battery.</summary>
    public static FireControlLevel FireControl(int directors, double rangefinderM, double computerT, bool radar, Era era)
    {
        var level = directors == 0 ? FireControlLevel.Eyeball
            : radar ? rangefinderM > 0 ? FireControlLevel.Rangekeeper : FireControlLevel.AutoTrack
            : computerT <= 0 ? rangefinderM > 0 ? FireControlLevel.Rangefinder : FireControlLevel.Eyeball
            : computerT < 1 ? FireControlLevel.Clock
            : computerT < 5 ? FireControlLevel.Director
            : computerT < 8 ? FireControlLevel.Table
            : FireControlLevel.Rangekeeper;
        var best = era switch
        {
            Era.Victorian => FireControlLevel.Rangefinder,
            Era.GreatWar => FireControlLevel.Director,
            Era.Treaty => FireControlLevel.Table,
            Era.Wwii => FireControlLevel.RadarSpotting,
            _ => FireControlLevel.Digital,
        };
        return level < best ? level : best;
    }

    // ------------------------------------------------------------------ the torpedoes (docs/battle/fighting.md 4.6)

    /// <summary>PLACEHOLDER: a torpedo from its calibre, shipgen's warhead and the look's era.
    /// <list type="bullet">
    /// <item>the settings, fast and slow, a 21"'s: 30 kn for 1,000 m or 24 kn for 2,700 m before 1906 (the 1890s'
    /// Whiteheads), 41 kn for 3,900 m or 28 kn for 9,200 m in the Great War (the RN's 21" Mk II 45 kn for 3,840 m or 29 kn
    /// for 9,100; the German G/7 37 kn for 4,000 m or 27 kn for 9,300), 43/5,000 or 30/11,000 between the wars, 45/6,000
    /// or 32/12,500 in the Second (Mk 15 45 kn for 5,500 m, G7a 44 for 5,000), 45/9,000 or 35/18,000 after; the range
    /// by the calibre cubed (the air and fuel it holds), the speed by its 0.3 power (an 18" a little slower). Oxygen
    /// torpedoes (the Type 93's 20 km at 48 kn) aren't told apart;</item>
    /// <item>the charge, TNT's worth: shipgen's warhead (2·10⁻⁶·d³ kg, ~300 kg for a 21", the Second War's) times the
    /// era's share: 0.45 before 1906 (wet guncotton), 0.6 in the Great War (the G/7's 195 kg of hexanite, the 21" Mk II's
    /// ~110-130 kg), 0.85 between the wars, 1 after.</item>
    /// </list>
    /// The designer should give the torpedo by type and year: its settings, warhead and explosive.</summary>
    public static TorpedoRow Torpedo(double calibreMm, double warheadKg, Era era)
    {
        var (fastKn, fastM, slowKn, slowM, share) = era switch
        {
            Era.Victorian => (30.0, 1_000.0, 24.0, 2_700.0, 0.45),
            Era.GreatWar => (41.0, 3_900.0, 28.0, 9_200.0, 0.6),
            Era.Treaty => (43.0, 5_000.0, 30.0, 11_000.0, 0.85),
            Era.Wwii => (45.0, 6_000.0, 32.0, 12_500.0, 1.0),
            _ => (45.0, 9_000.0, 35.0, 18_000.0, 1.0),
        };
        double d = calibreMm > 0 ? calibreMm / 533 : 1, range = d * d * d, speed = Math.Pow(d, 0.3);
        double warhead = warheadKg > 0 ? warheadKg : 2e-6 * Math.Pow(calibreMm > 0 ? calibreMm : 533, 3);
        return new TorpedoRow
        {
            CalibreMm = calibreMm > 0 ? calibreMm : 533, ChargeKg = warhead * share, FastKn = fastKn * speed, FastRangeM = fastM * range,
            SlowKn = slowKn * speed, SlowRangeM = slowM * range,
        };
    }

    /// <summary>PLACEHOLDER: a torpedo mount's training rate, °/s: trained by hand before the Second War (3), by power
    /// in it (6). Shipgen gives the arcs, not the gear.</summary>
    public static double TorpedoTrainDegS(Era era) => era >= Era.Wwii ? 6 : 3;

    /// <summary>PLACEHOLDER: how long a tube takes to load again, s: a submerged tube's ~3 minutes (the torpedo run in
    /// from the room by hand), a deck mount's reloads (when it carries any) ~10 (the IJN's reload gear ~5 by 1940).
    /// Shipgen gives the torpedoes carried, not the gear.</summary>
    public static double TorpedoReloadS(LauncherKind kind, Era era) =>
        kind == LauncherKind.Tube ? 180 : era >= Era.Wwii ? 300 : 600;

    /// <summary>PLACEHOLDER: TD/L = 0.341·(L/B)^1.3, times 0.77 for two rudders or more, kept to 2.5-8 (4-8
    /// under 60 m: planing craft turn in 3-6 lengths). A rough fit to
    /// research/simulation/fire-control-research/manoeuvring.md §1: a single-rudder ship of L/B 6 turns in ~3.5
    /// lengths, Fletcher's L/B 9.6 in 6-8 (Titanic's 4.4); twin rudders ~25% tighter (Sumner against Fletcher). It
    /// overstates fast battleships (Iowa 3.9 against a sourced 2.8). Shipgen's rudders all have 1.7% of L·T, so the
    /// rudder's size can't tell ships apart yet: the designer should derive TD from the rudders, the hull's form and
    /// the skeg. Rudder rate and hard over (2.3°/s to 35°, the SOLAS-type gear of manoeuvring.md §3, faster under
    /// 100 m: a PT boat's ~3 s to hard over) want the steering gear and its era; the power ramp (1.93·Δ^0.47 s from nothing to
    /// full, Δ the full displacement in tonnes: ~340 s for a 60,000 t battleship, ~100 s for a 4,400 t destroyer, ~20 s
    /// for a 115 t PT boat, which with the forces gives the research's times to full speed) and the cut (15 s from
    /// full to nothing) want the plant's kind and its boilers.</summary>
    static double TacticalDiameter(MotionFacts m)
    {
        double tdOverL = 0.341 * Math.Pow(m.LengthM / m.BeamM, 1.3) * (m.Rudders >= 2 ? 0.77 : 1);
        return Math.Clamp(tdOverL, m.LengthM < 60 ? 4 : 2.5, 8) * m.LengthM;
    }
}
