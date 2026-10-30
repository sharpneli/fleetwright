namespace Fleetwright.Sim.Ships;

/// <summary>DESIGNER PLACEHOLDERS: facts a class needs that the designer (shipgen) doesn't give yet. Each is a rough
/// stand-in, here and nowhere else, so they're easy to find and to replace when shipgen derives them (the user,
/// 2026-10-12: "if you need something from the designer and it ain't there, put in a placeholder and flag it"). The
/// list is in docs/battle/plan.md, "Needed from the designer". A replaced placeholder is deleted from here.</summary>
public static class DesignerPlaceholders
{
    /// <summary>Fills what the design didn't give (moving).</summary>
    public static MotionFacts Fill(MotionFacts m) => m with
    {
        TacticalDiameterM = m.TacticalDiameterM > 0 ? m.TacticalDiameterM : TacticalDiameter(m),
        RudderRateDegS = m.RudderRateDegS > 0 ? m.RudderRateDegS : Math.Clamp(2.3 * 100 / m.LengthM, 2.3, 10),
        HardOverDeg = m.HardOverDeg > 0 ? m.HardOverDeg : 35,
        PowerRampS = m.PowerRampS > 0 ? m.PowerRampS : 1.93 * Math.Pow(m.FullDisplacementT, 0.47),
        PowerCutS = m.PowerCutS > 0 ? m.PowerCutS : 15,
        RollDamping = m.RollDamping > 0 ? m.RollDamping : BilgeKeelDamping,
    };

    /// <summary>PLACEHOLDER: every ship rolls as one with bilge keels, ζ 0.10 (Froude's keels halving the resonant roll,
    /// universal after the Royal Sovereigns of 1894-95; ship-motion-gunnery-research.md §3.3) [EST]. The designer's
    /// damping fit (none, bilge keels, Frahm tanks, a gyro, fins: docs/shipgen/TODO.md) should give it; the user's 1895
    /// cut waits for it, since a class carries no year and its look never sets a number.</summary>
    public const double BilgeKeelDamping = 0.10;

    /// <summary>PLACEHOLDER: a laying fit's timing lag at the roll, s (docs/battle/balance.md BL1b): a trained layer at the
    /// key 0.1 (USNI 1900's "corrected" interval), a stable vertical's contacts 0.02 (the USN Mk 41's continuous fire)
    /// [EST, ship-motion-gunnery-research.md §4.3]. The fire-control tech block should give it (`roll_tau_s`).</summary>
    public const double RollTauS = 0.1, StableRollTauS = 0.02;

    public static double RollTau(bool stableVertical) => stableVertical ? StableRollTauS : RollTauS;

    /// <summary>PLACEHOLDER: the share of the trunnions' cross-level a laying fit's sights correct (research §4.1): a
    /// pointer's horizon telescope 0.5, a director's cross-level telescope 0.7, a stable vertical 0.98 [EST]. The fire-control
    /// tech block should give it (`cross_level`).</summary>
    public const double PointerCrossLevel = 0.5, DirectorCrossLevel = 0.7, StableCrossLevel = 0.98;

    public static double CrossLevel(bool director, bool stableVertical) => stableVertical ? StableCrossLevel : director ? DirectorCrossLevel : PointerCrossLevel;

    // ------------------------------------------------------------------ the eyes' glass (docs/battle/night.md Q1)

    /// <summary>PLACEHOLDER: the glass an eye looks through, its exit pupil (mm) and the share of the light it passes,
    /// which tell in the dark: the lookouts' binoculars 7×50-type night glasses, uncoated (the objective
    /// <see cref="LookoutApertureMm"/> over the magnification, 0.6: some sixteen bare air-glass surfaces) [EST], coated
    /// 0.85; the conning tower's slits the naked eye; a rangefinder's or a director's telescope a narrow exit pupil
    /// (<see cref="TelescopeExitPupilMm"/>) and the coincidence prisms' loss, 0.35 uncoated and 0.55 coated [EST]. The
    /// designer should give each post its glass (the optics block; the lookouts' kit by navy and year).</summary>
    public static (double ExitPupilMm, double Transmission) Glass(EyeKind kind, bool ranges, bool coated, double magnification)
    {
        if (ranges)
            return (TelescopeExitPupilMm, coated ? 0.55 : 0.35);
        if (kind == EyeKind.ConningTower)
            return (double.PositiveInfinity, 1);
        return (LookoutApertureMm / Math.Max(magnification, 1), coated ? 0.85 : 0.6);
    }

    /// <summary>PLACEHOLDER: the lookouts' binoculars' objective, mm (<see cref="Glass"/>).</summary>
    public const double LookoutApertureMm = 50;

    /// <summary>PLACEHOLDER: a rangefinder's or a director's exit pupil, mm (<see cref="Glass"/>) [EST: 2-3 mm at their
    /// 15-25×].</summary>
    public const double TelescopeExitPupilMm = 2.5;

    // ------------------------------------------------------------------ the weapons (docs/battle/fighting.md 4.0)

    /// <summary>PLACEHOLDER: working a gun, from its calibre and length, its mount's shape and the design's gun block
    /// (gun_tech; the catalogue's blocks by period are in Shipgen's GunTemplates, which give the shares below).
    /// <list type="bullet">
    /// <item>reload 7·(d/102 mm)^1.5 s, at most 36 s: the 4"'s ~8 rounds a minute, the 8"'s ~3, the heavy guns' ~2
    /// (research/simulation/manual-gunnery-research.md §4, fire-control-research/early-hit-rates.md: 12" about 2 a
    /// minute in 1906, the KGV's 14" cycles 26-30 s), times the block's share (1.1 in the Great War, 0.9 in the Second,
    /// 0.5 for the Cold War's automatic guns); and a heavy gun slower by the block's slowing at 300 mm, growing from 150
    /// mm, by its length (before 1906, 12"/35 70-100 s, the muzzle-loaders minutes: ×1.3, ×2.5 under 40 calibres and ×4
    /// under 30);</item>
    /// <item>training 2·(381 mm/d)^1.5 °/s (fire-control-research.md §4's mount rates: RN 15" 2, 8" 6.7, 6" 10) times the
    /// block's share (0.7 before 1906, 1.2 between the wars, 1.5 in the Second: USN 16" 4, 38 cm 5, 3 in the Cold War),
    /// and its dual-purpose mount's on a dp shape (×1.6 from the treaty years: 5"/38 25); elevating twice as fast, 2-20
    /// °/s;</item>
    /// <item>the highest elevation, the block's (13° before 1906, 18° in the Great War: RN 13.5" and 15" 20°, German
    /// 13.5-16°; 30° between the wars, 40° in the Second, 45° after), its dual-purpose mount's on a dp shape (45° from the
    /// treaty years: Shipgen draws some earlier light guns in a dp shape).</item>
    /// </list>
    /// The designer should give these per mount type from its machinery (hydraulic or electric, all-angle loading).</summary>
    public static (double ReloadS, double TrainDegS, double ElevateDegS, double MaxElevationDeg) Gun(double calibreMm, double calibreLength,
        TurretShape shape, in GunTechRow g)
    {
        double reload = Math.Min(7 * Math.Pow(calibreMm / 102, 1.5), 36) * g.ReloadShare;
        double slow = calibreLength < 30 ? g.HeavySlowUnder30Cal : calibreLength < 40 ? g.HeavySlowUnder40Cal : g.HeavySlow;
        reload *= 1 + (slow - 1) * Math.Clamp((calibreMm - 150) / 150, 0, 1);
        bool dp = shape == TurretShape.Dp;
        double train = Math.Clamp(2 * Math.Pow(381 / calibreMm, 1.5) * g.TrainShare * (dp ? g.DpTrainShare : 1), 0.5, 60);
        double maxElev = dp ? g.DpMaxElevationDeg : g.MaxElevationDeg;
        return (Math.Max(reload, 0.5), train, Math.Clamp(2 * train, 2, 20), maxElev);
    }

    /// <summary>PLACEHOLDER (docs/battle/fire-control.md F6d): the hoists' sustained reload over the drill reload, once a
    /// mount's ready-use rounds are fired [EST from two cases: the Majestics' 12" at 70 s a round from ready rounds and
    /// 100 s after (1.43), HMS Delhi's 5"/38 at 25 rounds a minute from ready-use and 15 from the magazine (1.67);
    /// predreadnought-tech.md §3, emergency-eyeball.md]. The designer should give each mount's hoists.</summary>
    public const double HoistShare = 1.5;

    // ------------------------------------------------------------------ magazines going up (docs/visuals/magazine-explosions.md)

    /// <summary>PLACEHOLDER: the share of a magazine's propellant that burns in the cascade window, the fireball's M
    /// (research/vfx/magazine-explosion-vfx-research.md §2.2: "not the whole magazine"). Vidgen's tuning value: Queen
    /// Mary's forward group fits ~0.45, Invincible's ~0.17. The venting race (docs/battle/magazines.md V3) should give M.</summary>
    public const double MagazineFastShare = 0.3;

    /// <summary>PLACEHOLDER: the propellant's share of a round's weight when a magazine serves no gun with a charge
    /// (vidgen's 380/52's, 23%).</summary>
    public const double MagazinePropellantShare = 0.23;

    /// <summary>PLACEHOLDER: what a magazine holds, tonnes, when the design doesn't say: 0.5 t a cubic metre of its
    /// volume (charges and shells stowed with their racks and passages).</summary>
    public static double MagazineTonnes(double volumeM3) => 0.5 * Math.Max(volumeM3, 0);

    /// <summary>PLACEHOLDER (docs/visuals/muzzle-flashes.md, "What the model takes", decision 4): a gun's charge, from its
    /// calibre and length and the design's gun block.
    /// <list type="bullet">
    /// <item>its mass 4000·d³·(L/50) kg (d in m; research/vfx/muzzle-flash-smoke-research.md §2.1: the 20 mm to the 80 cm
    /// within ~15%: 5"/38 6.3 against 7, 16"/50 268 against 297);</item>
    /// <item>its propellant, the block's: black (brown) powder under its length (the Victorian blocks' 36 calibres: the
    /// short guns of the brown-powder years, the 12"/35, the 13.5"/30; the long guns came with smokeless powder), else
    /// its smokeless one (single-base for the USN, France's poudre B and Russia's pyroxylin; diglycol for Germany from
    /// the treaty years, RPC/38; triple-base in the Cold War; double-base cordite and its kin otherwise: the RN's MD and
    /// SC, the German RPC/12, Japan's and Italy's);</item>
    /// <item>no flash-reducer salt or wear reducer (they came between the wars and later), a black-powder igniter of 1%
    /// for a bag gun and 0.3% for a cased one (120 mm and under), and no muzzle device.</item>
    /// </list>
    /// The designer, or a later ammunition system, should give the charge per gun, and the flashless night charge as a
    /// choice.</summary>
    public static ChargeRow Charge(double calibreMm, double calibreLength, in GunTechRow g)
    {
        double d = calibreMm / 1000;
        return new ChargeRow
        {
            MassKg = 4000 * d * d * d * (calibreLength / 50), Propellant = calibreLength < g.BlackPowderUnderCal ? Propellants.BlackPowder : g.Propellant,
            Packaging = g.Packaging,
            Igniter = calibreMm <= 120 ? 0.003 : 0.01,
        };
    }

    /// <summary>PLACEHOLDER: a gun's full-salvo pattern, % of the range (fire-control-research.md §7, fire_control_ref.py
    /// GUNS), from the design's gun block: a heavy gun's (280 mm and up) the block's at 305 mm times (305 mm/d) to its
    /// power, kept to 1.5-3.5; a lighter one's the block's own. The Great War's heavy guns 3.3·(305 mm/d)³ (the 12"
    /// 3.3-3.5%, the 13.5" 2.5, the 15" 1.7, "the most accurate battleship gun"), its lighter guns 2.5; before 1906 3.5;
    /// from the treaty years 1.9 for the heavy guns (16" 1.9, 14" 2.0; delay coils and stiffer mounts) and 1.8 for the
    /// lighter (8" 2.0, 5"/38 1.7), the Cold War's 1.5. The designer should give it from the gun and its mount (wear,
    /// delay coils, mixed rifling).</summary>
    public static double Pattern(double calibreMm, in GunTechRow g) =>
        calibreMm >= 280 ? Math.Clamp(g.PatternPct * Math.Pow(305 / calibreMm, g.PatternExp), 1.5, 3.5) : g.PatternLightPct;

    // ------------------------------------------------------------------ the torpedoes (docs/battle/fighting.md 4.6)

    /// <summary>PLACEHOLDER: a torpedo from its calibre, shipgen's warhead and the design's torpedo block (torpedo_tech; the
    /// catalogue's blocks by period are in Shipgen's TorpedoTemplates).
    /// <list type="bullet">
    /// <item>the settings, fast and slow, the block's for a 21" (30 kn for 1,000 m or 24 kn for 2,700 m before 1906, the
    /// 1890s' Whiteheads; 41 kn for 3,900 m or 28 kn for 9,200 m in the Great War, the RN's 21" Mk II 45 kn for 3,840 m or
    /// 29 kn for 9,100, the German G/7 37 kn for 4,000 m or 27 kn for 9,300; 43/5,000 or 30/11,000 between the wars;
    /// 45/6,000 or 32/12,500 in the Second, Mk 15 45 kn for 5,500 m, G7a 44 for 5,000; 45/9,000 or 35/18,000 after); the
    /// range by the calibre cubed (the air and fuel it holds), the speed by its 0.3 power (an 18" a little slower). Oxygen
    /// torpedoes (the Type 93's 20 km at 48 kn) aren't told apart;</item>
    /// <item>the charge, TNT's worth: shipgen's warhead (2·10⁻⁶·d³ kg, ~300 kg for a 21", the Second War's) times the
    /// block's share (0.45 before 1906, wet guncotton; 0.6 in the Great War, the G/7's 195 kg of hexanite, the 21" Mk II's
    /// ~110-130 kg; 0.85 between the wars, 1 after);</item>
    /// <item>its gyro's angle, its failures on the run and its duds, the block's.</item>
    /// </list>
    /// The designer should give the torpedo by type and year: its settings, warhead and explosive.</summary>
    public static TorpedoRow Torpedo(double calibreMm, double warheadKg, in TorpedoTechRow t)
    {
        double d = calibreMm > 0 ? calibreMm / 533 : 1, range = d * d * d, speed = Math.Pow(d, 0.3);
        double warhead = warheadKg > 0 ? warheadKg : 2e-6 * Math.Pow(calibreMm > 0 ? calibreMm : 533, 3);
        return new TorpedoRow
        {
            CalibreMm = calibreMm > 0 ? calibreMm : 533, ChargeKg = warhead * t.ChargeShare, FastKn = t.FastKn * speed, FastRangeM = t.FastRangeM * range,
            SlowKn = t.SlowKn * speed, SlowRangeM = t.SlowRangeM * range, GyroDeg = t.GyroDeg, FailShare = t.FailShare, DudShare = t.DudShare,
        };
    }

    /// <summary>PLACEHOLDER: a torpedo mount's training rate, °/s, the block's: trained by hand before the Second War (3),
    /// by power in it (6). Shipgen gives the arcs, not the gear.</summary>
    public static double TorpedoTrainDegS(in TorpedoTechRow t) => t.MountTrainDegS;

    /// <summary>PLACEHOLDER: how long a tube takes to load again, s, the block's: a submerged tube's ~3 minutes (the
    /// torpedo run in from the room by hand), a deck mount's reloads (when it carries any) ~10 (the IJN's reload gear ~5
    /// by 1940). Shipgen gives the torpedoes carried, not the gear.</summary>
    public static double TorpedoReloadS(LauncherKind kind, in TorpedoTechRow t) => kind == LauncherKind.Tube ? t.TubeReloadS : t.MountReloadS;

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
