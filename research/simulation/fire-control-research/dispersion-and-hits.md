# 04 — Dispersion, Danger Space, Hit Probability and Historical Hit Rates (1900–1990)
Status: final    Updated: 2026-11-08    Request: [sigma_D's n](../../REQUESTS.md) (§1.1, §1.2, §1.4)

Scope: the last stage of gunnery. Given the gun orders, how the shells spread, how often they hit, and how to turn fire-control error, dispersion and target size into a hit probability that matches history.

Tags:
- **[INFERRED]** marks my own derivation or calculation.
- **[UNCERTAIN]** marks thin or conflicting sourcing.

Every sourced fact has its URL next to it.

---

## 0. Model summary

1. **Two error sources.** Treat each salvo's mean point of impact (MPI) as carrying a fire-control error (bias), with σ_FC in range and in deflection. Each shell then spreads around the MPI with dispersion σ_D in range and in deflection.
2. **Per-shell hit probability.** For each axis, P = Φ((h − μ)/σ_D) − Φ((−h − μ)/σ_D), where h is half the "hitting space" and μ is the MPI error. Multiply the range and deflection terms.
3. **Hitting space.**
   - In range: danger space = target height × cot(angle of fall), plus the target's depth along the line of fire.
   - In deflection: the presented length.
4. **Scale with range.**
   - σ_D (range) ≈ 0.25–0.6% of range for modern guns.
   - σ_FC is set by rangefinding, rangekeeping and spotting quality, and falls after a straddle.
5. **Combat degradation.** Apply a factor of about 0.3–0.5× to practice results.
6. **Calibration.** Under good conditions the outputs should land near these figures:
   - About 3–5% at 15–20 kyd (WWI optical).
   - About 5–12% at 10–20 kyd (WWII radar or optical, good conditions).
   - About 1% for long-range or poor-visibility duels.
   - About 10–25% under 10 kyd against crippled targets.

---

## 1. Dispersion

### 1.1 Definitions and statistics (USN usage)

- **True mean dispersion D.** This is the mean absolute deviation from the MPI. For a 10-shot salvo, true D = 1.054 × the apparent (measured) D. https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html
- **50% zone.** ±0.846D contains 50% of shots, so 0.846D is the probable error (PE). [INFERRED] For a normal distribution, D = 0.798σ and PE = 0.674σ.
- **Wild shots.** ±4D contains 99.9% of shots. Anything outside it counts as a wild shot. https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html
- **Pattern vs. D.** The salvo "pattern" is the 100% spread from the shortest to the longest shot. Its ratio to D depends on the number of shells (USNI 1917): https://www.usni.org/magazines/proceedings/1917/june/estimate-value-accuracy-pointing-long-range-naval-gunnery

| Shots per salvo | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Pattern ÷ D | 1.69 | 2.43 | 2.89 (USNI 1917: 2.74) | 3.21 | 3.47 | 3.67 | 3.85 | 4.00 | 4.13 | 4.24 | 4.34 |

  3 and 5–12 are USNI 1917's; 2 and 4 are Jurens's (Warship International 1991, via NavWeaps INRO, https://www.navweaps.com/index_inro/INRO_BB-Gunnery.php), whose series is otherwise the same. USNI 1917 prints 2.74 for 4 shots; 2.89 is used because it fits the smooth run of the others. Each value is 6–15% above the normal-theory expected range ÷ D, falling steadily as the salvo grows (2.74 would break the run at 1.06) [INFERRED]. USNI 1917 calls the relation "very recently published" and does not say how it was derived. Jurens lists 1.00 for one gun, but one shell has no pattern, so the table starts at 2.

- **Converting pattern to σ.** [INFERRED] σ_range = pattern ÷ (0.798 × ratio(n)), where **n is the salvo the pattern was quoted for**. For a 9-gun salvo, σ ≈ pattern ÷ 3.2. For a 3-gun salvo, σ ≈ pattern ÷ 1.9. σ is the shell's. A smaller salvo fired from the same guns has a smaller pattern, not wider shells (§1.4). (Corrected 2026-11-08: this line used to read "pattern ÷ (1.25 × ratio)", i.e. ÷ 5.0 for 9 guns, which inverts the 1.25. `fire_control_ref.py` always divided by 0.798.)
- **Worked example (Naval Ordnance & Gunnery).** Ten 5"/38 guns at 8,500 yd:
  - Pattern: 260 yd in range × 130 yd in deflection.
  - D_r = 65 yd and D_d = 34 yd.
  - So range dispersion is about 0.76% of range as D. Deflection dispersion is about half the range dispersion.
  - Source: https://www.eugeneleeslover.com/USNAVY/CHAPTER-18.php
- **German proving-ground figures.**
  - Probable error at 15–30° elevation was 0.50–0.80% of range for single guns.
  - For a ship's battery, multiply by 1.5–1.7 with delay coils, which Germany adopted in 1939–40.
  - Without delay coils, the factor was about 2–3, and wild shots were common.
  - Source: https://kbismarck.com/german-fire-effect-tables.html

### 1.2 Pattern sizes by gun

Salvo = the shells the pattern was quoted for: [S] where the source says so, [EST] the game's working value where it doesn't. §1.4 explains why it matters.

| Gun / ship | Range | Pattern (100%) | % of range | Salvo (shells) | Source |
|---|---|---|---|---|---|
| British 12" (WWI) | 12,000 yd | ~400 yd | 3.3% | **not stated** [UNCERTAIN]; 8 [EST], the broadside of Dreadnought to St Vincent | https://navalgazing.net/Spotting (footnote 3: "typical pattern spreads in range"; no salvo, no source) |
| British 13.5" | 12,000 yd | ~300 yd | 2.5% | **not stated** [UNCERTAIN]; 8 [EST] (Lion class 8, Orion/KGV/Iron Duke 10) | same |
| British 15"/42 | 12,000 yd | ~200 yd | 1.7% | **not stated** [UNCERTAIN]; 8 [EST], the QE/R broadside | same. Postwar USN called it the "most accurate" BB gun of the war: https://en.wikipedia.org/wiki/BL_15-inch_Mk_I_naval_gun |
| 14" (12-gun USN example, 1917) | 18,000 yd | 1,000 yd | 5.6% | 12 [S]; mean dispersion ±230 yd | USNI 1917 (above) |
| USN 14"/45 and 14"/50, 1920s | extreme | 1,200–3,200 yd | — | 12 [S] | https://navweaps.com/Weapons/WNUS_14-50_mk4.php |
| USN 14"/50 Mk 11, after chamber fix | — | <700 yd | — | 12 [S] | https://navweaps.com/Weapons/WNUS_14-50_mk11.php |
| Tennessee / California 14" at Surigao, 1944 | 20,000 yd | 300–400 yd | 1.5–2% | 6–9 [S] | same |
| USN 16"/45 (old guns), expected | — | — | 1.8% | 8 [S] | https://navweaps.com/index_inro/INRO_BB-Gunnery.php |
| Same, actual, 1941 | — | — | ~2.2% | 7 [S] | same |
| 1944 expected values, small / full salvo: Iowa 16"/50 | — | — | 1.0 / 1.9 | 3 / 9 [S]; how the 3 were formed is not stated (one turret [INFERRED]) | same |
| 1944 expected: South Dakota 16"/45 | — | — | 1.0 / 1.9 | 3 / 9 [S] | same |
| 1944 expected: Colorado 16"/45 | — | — | 1.0 / 1.8 | 3 / 8 [S] | same |
| 1944 expected: New Mexico and Pennsylvania 14" | — | — | 1.2 / 2.4 | 3 / 12 [S] | same |
| 1944 expected: Nevada 14"/45 | — | — | 1.2 / 3.4 | 3 / 10 [S] (twin and triple turrets mixed) | same |
| 1944 expected: 5"/38 (South Dakota / Iowa) | — | — | 0.9 / 1.4 and 1.7 | small salvo unstated / 6 and 10 [S] | same |
| Iowa 16"/50, 1987 Crete test | 34,000 yd | 220 yd | 0.64%. "Shell-to-shell dispersion" 123 yd (0.36%) | 15 shells, five from the right gun of each turret [S]. Neither figure is defined, and "14 of 15 within 250 yd of the centre, 8 within 150 yd" does not fit a 220 yd spread [UNCERTAIN] | https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php |
| IJN 46 cm Type 94 (Yamato) | max range | 400–500 m | ~1–1.2% [INFERRED: max ≈ 42 km] | not stated; 9 [EST] | https://navweaps.com/Weapons/WNJAP_18-45_t94.php |
| IJN 20 cm (Takao), 1933 | 19,300 m | 483 m | 2.5% | not stated; 10 [EST] | https://www.navweaps.com/Weapons/WNJAP_8-50_3ns.php |
| IJN 20 cm (Myoko class, after hull stiffening), 1936 | 20–22 km | 280–330 m | 1.3–1.65% | not stated; 10 [EST] | same |
| USN 8"/55, early treaty cruisers | — | up to 2,000 yd | — | "full salvo" [S]: 9–10 by class | https://navweaps.com/Weapons/WNUS_8-55_mk9.php |
| 5"/38 twin | 10,000 / 12,000 yd | 175 / 335 yd | 1.75% / 2.8% | 10 rounds [S] | INRO, above |
| 5"/38, range pattern with delay coils | 11,700 yd | 92–100 yd | 0.8% | 2 [S] | same |
| 5"/38 single ship (Richard P. Leary wear test) | 6,000 / 12,000 yd | 260 / 470 yd | 4.3% / 3.9% | not stated [UNCERTAIN] | https://navweaps.com/Weapons/WNUS_5-38_mk12.php |
| West Virginia 5"/51 secondary | 14,212 yd (1925) / 10,258 yd (1929) | 643 / 329 yd | 4.5% / 3.2% | 6 [S] | INRO, above |

**Gaps.** No sourced pattern figures turned up for the 38 cm SK C/34, 4.7"/45, 3"/76 or 6"/47. For a model, assume these defaults [INFERRED from the scaling above]:

- **Heavy guns, modern with delay coils:** full-salvo pattern 1.0–1.5% of range, so σ_r ≈ 0.25–0.35% of range.
- **WWI heavy guns:** 2–3%, so σ_r ≈ 0.5–0.7%.
- **Cruiser guns, 6"–8":** 1.5–2.5%.
- **DP and destroyer guns, 4.7"–5":** 2–4%, worse from small, lively platforms.
- **3"/76 at short range:** treat as 2–4% [UNCERTAIN].
- **Deflection σ:** about 0.3–0.5 × range σ. A normal deflection pattern is about 4 mils (INRO, above).

### 1.3 What drives pattern size

- **Shell to shell.** Variation in charge weight and temperature, shell weight, and seating. One degree F of powder temperature changes muzzle velocity by about 2 ft/s, which moves a 16" shell about 55 yd at 40,000 yd. https://www.navalgazing.net/Ballistics
- **Gun to gun.** Barrel wear and calibration. A worn 16"/50 drops from 2,500 to 2,425 ft/s (same source).
  - Rodney's mixed Mk I/II rifling caused dispersion problems: https://navweaps.com/Weapons/WNBR_16-45_mk1.php
  - Propellant velocity spread wrecked New Jersey's accuracy off Lebanon in 1984: https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php
- **Salvo interference.** Shells leaving adjacent barrels at the same moment disturb each other in flight. Delay coils fixed this:
  - **USN:** about 0.06 s center-gun delay from the early 1930s. It roughly halved mean dispersion (around 1934).
    - https://navweaps.com/Weapons/WNUS_14-50_mk11.php
    - https://navweaps.com/index_inro/INRO_BB-Gunnery.php
  - **IJN:** the Type 98 device (1938) added a 0.03 s separation and cut dispersion by 10–15%. https://www.navweaps.com/Weapons/WNJAP_8-50_3ns.php
  - **RN (Rodney):** firing wing and center guns separately cut dispersion at a cost in rate of fire. https://navweaps.com/Weapons/WNBR_16-45_mk1.php
- **Mount and hull flex.** Stiffening the Myoko-class hulls cut patterns from about 2.5% to 1.5% of range: https://www.navweaps.com/Weapons/WNJAP_8-50_3ns.php
  - Hogging and twisting are worst at 45–90° wave encounter: https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/ACCURACY-OF-SHIPBOARD-GUN-FIRE.html
- **Long-run trend.** USN mean dispersion fell 66% between 1920 and 1945, while MPI (fire-control) error fell only 23%. https://navweaps.com/index_inro/INRO_BB-Gunnery.php
- **Wild shots.** About 10% of rounds in the USN's 1927–28 practices were wild shorts (same source).
- **Rule of thumb.** Pattern in yards grows about linearly with range, so the percentage of range stays roughly constant. One linked page says the pattern shrinks as elevation rises: https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/ACCURACY-OF-SHIPBOARD-GUN-FIRE.html [UNCERTAIN: no numbers given]

### 1.4 The pattern's own salvo (added 2026-11-08, request "sigma_D's n")

A quoted pattern mixes two things. One is the shell's scatter, σ_D: round to round, gun to gun, salvo interference, and mount and hull flex. That belongs to the gun and its battery. The other is how many shells the salvo held. The expected spread of n shells is ratio(n) · 0.798 σ_D, so it grows with n while σ_D stays the same. To recover σ_D, divide by the ratio at **the salvo the pattern was quoted for**. Dividing by the ratio at the salvo being fired is wrong. Before this fix, `fire_control_ref.py` `Gun.sigma_D(R, n)` (and the game's `FireControl.Dispersion`) took the ratio at the guns firing. The fewer guns fired, the wider each shell went. A single 12" gun came out at σ = 3.3 / 1.6 / 0.798 · 0.85 ≈ 2.2% of range. A 4-gun half salvo had 1.4× the shell scatter of an 8-gun broadside. The 1.6 and 1.9 used for 1 and 2 guns were extrapolations with no source.

**The record agrees with the fix [INFERRED from the sourced 1944 table, §1.2].** Per-shell σ worked out from each salvo size, without the 0.85:

| Battery | Small salvo | Full salvo | Small ÷ full |
|---|---|---|---|
| 16" (Iowa, South Dakota): 3 guns 1.0%, 9 guns 1.9% | 0.52% | 0.60% | 0.87 |
| 16"/45 Colorado: 3 guns 1.0%, 8 guns 1.8% | 0.52% | 0.59% | 0.88 |
| 14" New Mexico/Pennsylvania: 3 guns 1.2%, 12 guns 2.4% | 0.62% | 0.69% | 0.89 |
| 14" Nevada: 3 guns 1.2%, 10 guns 3.4% (mixed twin and triple turrets) | 0.62% | 1.03% | 0.60 |
| 5"/38: 6 guns 1.4% against 10 guns 1.7% | 0.51% | 0.52% | 0.98 |

The patterns nearly double from 3 shells to 9, but the shells scatter about the same. Under the old rule the 3-gun salvo's shells should have scattered 1.65× wider than the 9-gun salvo's. Instead they scatter about 0.87× as wide, which is what you'd expect if the 3 guns are one turret and so miss the scatter between turrets. Nevada is the exception that proves it: her mixed mounts add gun-to-gun scatter that a single turret doesn't see. Worn guns move σ_D directly: the old 16"/45s were expected at 0.59% (8 guns) and gave 0.75% (7 guns) in 1941.

**What salvo were the Great War patterns quoted for?** The source doesn't say. navalgazing's footnote ("at 12,000 yards, typical pattern spreads in range were 200 yards for 15" guns, 300 yards for 13.5" guns and 400 yards for 12" guns") gives no salvo size and no source, and the article's main text is about 3- and 4-shell spotting salvos. No other online source was found that ties these figures to a salvo: not the Dreadnought Project, Wikipedia, the Down report of 1917 (which says only that Americans thought British spreads too small), or USNI 1911–1921. The game's 8 [EST] is the full broadside of the 12" and 15" ships. The 13.5" ships fired 8 (Lion class) or 10 (Orion, KGV, Iron Duke). How much the choice matters (σ_D relative to n = 8):

| If the quoted salvo was | 4 (half salvo) | 5 | 6 | 8 | 10 |
|---|---|---|---|---|---|
| σ_D × | 1.33 | 1.20 | 1.11 | 1.00 | 0.93 |

The risk is one-sided. If the figures were for half salvos (the Grand Fleet's usual ranging and much of its action fire), each shell scattered up to a third more than the game now has. If they were 10-gun broadsides, about 7% less.

**Is a gun's round-to-round σ given directly anywhere?**
- **German proving-ground figures** [S: NavTecMisEu letter 0892, 21 Aug 1945, summarising the 1940 Fire Effect Tables, https://kbismarck.com/german-fire-effect-tables.html]. A single gun's "probable error" was 0.50–0.80% of range at 15–30° elevation. Curves for all German naval guns existed but are not reproduced. To convert to the battery's error, multiply by 1.5–1.7 with delay coils (1939–40) and about 2–3 without, plus "numerous wild shots". This is the only direct round-to-round source found. It is ambiguous [UNCERTAIN]:
  - Read as a probable error, σ is 0.74–1.19% of range, and a Bismarck 8-gun pattern would be 3.4–5.8%.
  - Read as the German 50% zone (its full width, 2 PE), σ is 0.37–0.59%, and the pattern would be 1.8–2.9%.
  - The second reading fits the USN's 0.5–0.6% per shell in 1944 and the inferred 1.5% for the 38 cm.
- **The battery factor is the real lesson.** The game wants the per-shell σ inside a ship's salvo. A proving ground's single-gun σ is 1.5–3× too small for that, because gun-to-gun scatter and interference are missing. Converting from a ship's quoted pattern is therefore the right route, as long as the salvo is known. A proving-ground figure needs the battery factor before it means anything. For Great War ships without delay coils, that factor is 2–3 [S, German].
- **Iowa 1987** (above): 15 shells from three guns. The counts within 150 and 250 yd of the centre give σ ≈ 0.4–0.6% of range [INFERRED; the figures are inconsistent].
- **USN battle practice c.1910** [S: USNI Proceedings Sept 1911, "Note on salvo dispersion", https://www.usni.org/magazines/proceedings/1911/september/note-salvo-dispersion]: one ship's mean dispersion D was 42 yd (12"), 38 yd (8") and 93 yd (6"). The range is not given in the text found.
- **Sims 1904** (`manual_gunnery_ref.py` `gun_err`): a gun's own scatter is ±3 arcmin, read as about 2 arcmin 1σ, in angle. It is pre-dreadnought and single-gun.
- **Range tables.** The British range tables' "50 per cent zone" columns and the USN's probable-error columns would be direct sources. None were found online.

**The 0.85 (open).** `sigma_D` and `FireControl.Dispersion` multiply σ by 0.85, commented "quoted patterns are full salvos with gun-to-gun scatter". Nothing in either note sources it. The ratio table already sits 6–8% above normal theory at 8–12 shells. With both, a modelled 8-shell salvo of normal shells spreads only 0.79 of its quoted pattern (0.93 without the 0.85). If the 0.85 is meant as "one turret's shells", the 1944 pairs above fit it (0.87–0.89). But a full salvo's shells come from every turret, so they should carry the full σ. Left unchanged here. A choice for the game: drop it (full salvos then match their quoted patterns to within 7%), or keep it as a deliberate tightening and say why.

**Fixed in `fire_control_ref.py` (2026-11-08).** `Gun.pattern_n` is the quoted salvo per gun, with tags in `GUNS`: 9 for the USN 16" [S], 10 for the 5"/38 [S], and [EST] elsewhere. `sigma_D(R)` takes no n. The new `pattern(R, n)` gives the expected spread of n shells. The ratio table is the full sourced series above. Callers changed: `engage()`, `quick_hit_probability()`, `evasion_ref.py` and `designer_ref.py`. Figures that moved:
- [fire-control-research.md](../fire-control-research.md) §11. Only the half-salvo cases moved: Tsushima 8.0 → 8.5, Jutland's British battlecruisers 2.7 → 3.1 (record 1.4), the German 1st Scouting Group 4.0 → 4.5 (record 4.0), Denmark Strait 11.4 → 12.1, LRBP 2.8 → 3.0, North Cape 6.0 → 6.9, Komandorski 1.0 → 1.1.
- Unchanged: the ladder, the MPI table and the 1944 study, which fire full salvos.
- [evasion-research.md](../evasion-research.md) tables 6–10 and 14–15. The single-gun digital mounts moved most: the 5″/54 and 76 mm about doubled at range.
- [fire-control-designer.md](../fire-control-designer.md) tables in §4 (noise-level changes).
- [manual-gunnery-research.md](../manual-gunnery-research.md) table 9's reference row.
- `fc_tables.py history`, which the game's `fc-history --ref` prints, now gives these §11 figures.

---

## 2. Danger space

### 2.1 Definition and formulas

- **Definition.** The greatest distance a target can move along the line of fire and still be struck by the trajectory.
- **USN range-table convention.** Column 7 gives danger space for a 20 ft target with no depth. Scale it linearly for other heights, then add the target's beam or depth.
  - Example (5"/38 at 10,000 yd): 18 yd for a 20 ft target. For a 30 ft target, 27 yd. Adding a 35 yd beam gives 62 yd.
  - At 3,500 yd: 157 yd.
  - Source: https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/DANGER-SPACE.html
- **Formulas.**
  - Simple: δ ≈ h·cot θ.
  - Exact: δ = h·cot θ·R/(R − δ).
  - USN citadel height was taken as 20 ft; RN used 30 ft.
  - Source: https://mathscinotes.com/2013/04/battleship-guns-and-danger-space/
- **Worked hitting-space example.** 8,500 yd, target 40 ft high and 90 ft beam:
  - Danger space 50 yd, plus 30 yd depth, gives 80 yd in range.
  - Deflection hitting space is 200 yd (a 600 ft ship).
  - Source: https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html
- **Battle ranges in 1917.** Danger space was about 50–100 yd. In the 14" example at 18,000 yd it was 49 yd. https://www.usni.org/magazines/proceedings/1917/june/estimate-value-accuracy-pointing-long-range-naval-gunnery

### 2.2 Angles of fall (sourced)

| Gun | 10 kyd | 15 kyd | 20 kyd | 30 kyd | 40 kyd |
|---|---|---|---|---|---|
| 16"/50 AP Mk 8 | 5.0° | — | 14.9° | 28.3° | 47.7° (45.5° in another table) |
| 12"/45 Mk X, 2crh | 9.1° | 20.1° | 33.4° | — | — |
| 12"/45 Mk X, 4crh | 7.0° | 14.2° | (22° at 19 kyd) | — | — |
| 5"/38 (2,600 ft/s) | 18.3° | (35° at 14 kyd) | — | — | — |

Sources:
- 16"/50: https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php
- 12"/45: https://navweaps.com/Weapons/WNBR_12-45_mk10.php
- 5"/38: https://navweaps.com/Weapons/WNUS_5-38_mk12.php

### 2.3 Danger space for 16"/50 (computed)

[INFERRED, δ = h·cot θ]

| Range | Battleship (h = 30 ft) | Battleship incl. beam (36 yd) | Destroyer (h = 15 ft) | Destroyer incl. beam (12 yd) |
|---|---|---|---|---|
| 10 kyd | 114 yd | 150 yd | 57 yd | 69 yd |
| 20 kyd | 38 yd | 74 yd | 19 yd | 31 yd |
| 30 kyd | 19 yd | 55 yd | 9 yd | 21 yd |
| 40 kyd | 9 yd | 45 yd | 5 yd | 17 yd |

What this shows:
- As fire becomes plunging, the beam (deck area) dominates the hitting space.
- At long range most hits land on decks or turret tops. At short range most hit the belt or side.
- Danger space for a destroyer is roughly a third to half of a battleship's.

---

## 3. Theoretical hit probability

### 3.1 Normal model and the textbook example

The textbook example is a 5"/38 salvo at 8,500 yd: https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html

| Case | P(range) | P(deflection) | P(hit) |
|---|---|---|---|
| MPI centered (h = 40 yd, D_r = 65 yd) | 0.379 | 0.981 | 0.372, about 4 hits per 10 shells |
| MPI 130 yd over (half a pattern) | 0.118 | 0.981 | 0.116 |

Key points:
- Range error dominates. Deflection probability is near 1 once the line is right.
- Shifting the MPI by half a pattern cuts hits by two-thirds.
- The Japanese rule of thumb was that a target at the edge of the 50% zone has about one-tenth the hit probability of one dead on the aim point. https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028

### 3.2 Optimum dispersion is not zero

- **USNI 1912.** Hits peak when dispersion is about 80% of the fire-control error.
  - The 1911–12 fleet practices at 10 kyd should have used 15–30% more vertical dispersion.
  - With fire-control errors unchanged, dispersion could in some cases nearly triple without losing hits.
  - The author's formula is D_v ≈ ½√(8.75·E_v² + s²).
  - Source: https://www.usni.org/magazines/proceedings/1912/december-0/dispersion-and-accuracy-fire
- **USNI 1917.** Perfecting the worst class of gun pointers adds only about 1 hit per 1,000 rounds at 18 kyd (5.6% to 5.7%). Fire-control error dominates. https://www.usni.org/magazines/proceedings/1917/june/estimate-value-accuracy-pointing-long-range-naval-gunnery

**My calculation [INFERRED].** Per-shell P(range hit) for a 60 yd hitting space (h = 30), integrating over a normal MPI error σ_FC:

| σ_FC \ σ_D (yd) | 25 | 50 | 100 | 150 | 200 | 300 |
|---|---|---|---|---|---|---|
| 0 | 0.77 | 0.45 | 0.24 | 0.16 | 0.12 | 0.08 |
| 100 | 0.23 | 0.21 | 0.17 | 0.13 | 0.11 | 0.08 |
| 200 | 0.12 | 0.12 | 0.11 | 0.10 | 0.08 | 0.07 |
| 400 | 0.06 | 0.06 | 0.06 | 0.06 | 0.05 | 0.05 |

For a 9-shell salvo, the probability of at least one hit per salvo peaks at a non-zero σ_D:

| σ_FC | Peak P(≥1 hit) | σ_D at the peak |
|---|---|---|
| 100 yd | 0.76 | ~100 yd |
| 200 yd | 0.54 | ~150 yd |
| 400 yd | 0.34 | ~200–300 yd |

This is the classic result: a tight pattern helps hits per shell, a wider pattern helps the chance of hitting per salvo, and both help the spotter.

### 3.3 Straddle probability [INFERRED]

- **Definition.** A straddle means the target lies inside the pattern, with at least one shot over and one short.
- **Probability.** P(straddle) ≈ P(|MPI error| < about 0.4–0.5 × pattern).
- **Shells per straddle.** In the textbook case a straddle carries about 0.37 × n hits. Historically the figure is lower:
  - Prince of Wales at Denmark Strait: 3 straddles in 18 salvos produced 3 hits (sources in §5).
  - Duke of York at North Cape: 31 straddles in 52 radar-controlled salvos (sources in §5).

### 3.4 Calibration check: July 1944 USN practice study

16" guns: https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php

| Range | Broadside target | End-on target |
|---|---|---|
| 10 kyd | 32.7% | 22.3% |
| 20 kyd | 10.5% | 4.1% |
| 30 kyd | 2.7% | 1.4% |

The author considered these optimistic for combat.

[INFERRED] The model reproduces this with:
- σ_D = 0.3% of range.
- σ_FC ≈ 1% of range (100 / 200 / 300 yd).
- The danger spaces from §2.3.

---

## 4. Spotting

- **RN ladder / bracket rule.**
  - Start with a 400 yd correction, repeated until the fall crosses the target.
  - Then reverse by 200 yd, then 100 yd, which should straddle.
  - That is at most about 3–4 corrections.
  - The rate correction is about half the range correction.
  - A salvo needs at least 3, ideally 4 or more, shells to spot a wild shot.
  - Source: https://navalgazing.net/Spotting
- **USN rules.**
  - Bracket and halve, but never spot below the pattern size.
  - Ladder steps are at least one pattern. A rocking ladder is +100 / 0 / −100.
  - After a straddle, add one pattern every 3rd–4th salvo to check for overs.
  - Deflection is spotted in mils, range in yards.
  - Source: https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html
- **Limits of optical spotting.**
  - Needs about 120 ft height of eye for good range spotting at 15 kyd.
  - A 500 yd error is obvious at 12 kyd but barely visible at 19 kyd.
  - Source: https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html
  - Optical spotting deteriorated beyond about 18 kyd.
- **Radar spotting.**
  - Mk 8 mod 0 spotted 16" splashes to about 20 kyd; mod 3 to at least 35 kyd.
  - Mk 13 spotted to within 100 yd, or 50 yd with experience.
  - Source: https://navweaps.com/index_inro/INRO_BB-Gunnery.php
  - Mk 3 splash spotting reached only about 20 kyd. https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028
- **Salvos to straddle and first hit (sourced).**

| Case | Salvos to straddle / first hit | Range | Source |
|---|---|---|---|
| Nevada, 1924–25 practice | straddles on salvos 1–4, hit on salvo 5 | — | INRO |
| New York, 1930–31 | hit on salvo 2 (about 1 min 50 s) | 12.7 kyd | INRO |
| Tennessee, 1939–40 | hits from salvo 4 (3 min 42 s); 16 hits in first 14 salvos | 19 kyd | INRO |
| Lion at Dogger Bank | first hit after about 10 salvos | 20 kyd | https://vmss.ca/big-guns-sink-ships/ |
| Prince of Wales at Denmark Strait | first 2 salvos 1,000 yd over; straddles on salvos 6, 9 and 13 | 26.5 kyd | https://en.wikipedia.org/wiki/HMS_Prince_of_Wales_(53) |
| Bismarck at Denmark Strait | hit Hood with the 5th salvo | ~18–20 kyd | https://kbismarck.com/denmark-strait-battle.html |
| Scharnhorst vs Glorious | hit with the 3rd salvo | ~26 kyd | https://navweaps.com/index_tech/tech-006.php |
| Warspite at Calabria | hit Cesare at 26.2–26.4 kyd; salvo count unclear [UNCERTAIN] | 26.2–26.4 kyd | https://en.wikipedia.org/wiki/BL_15-inch_Mk_I_naval_gun |
| Iowa vs Nowaki / Katori | first salvo straddled Nowaki at 37.5 kyd; no hits. All 8 salvos straddled Katori | 37.5 kyd | https://www.navalgazing.net/Iowa-Part-2 |
| Duke of York at North Cape | first salvo hit at 11.9 kyd; 31 of 52 salvos straddled | 11.9 kyd | https://en.wikipedia.org/wiki/Battle_of_the_North_Cape |

- **Salvo interval vs. time of flight.**
  - New York, 1930–31: 66 s salvo interval; 31 s loading.
  - Night practice: about 10 s salvo interval; 44 s to the first effective salvo.
  - Source: INRO.
  - Idaho, 1942: 84 s average. https://navweaps.com/Weapons/WNUS_14-50_mk11.php
  - Rodney: about 1.5 salvos per minute. https://navweaps.com/Weapons/WNBR_16-45_mk1.php
  - Time of flight: about 30 s at 20 km and about 60 s at 30 km. https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028
  - [INFERRED] Beyond about 20 kyd, a spotting correction cannot be applied to the very next salvo. Applying corrections before earlier salvos land ("spot pyramiding") is a known error; a time-of-flight buzzer was the fix. https://www.eugeneleeslover.com/USNAVY/CHAPTER-18.php
- **Model default [INFERRED].** At 15–20 kyd, reaching a straddle takes:
  - WWI optical: 3–8 salvos.
  - WWII optical: 3–6 salvos.
  - Radar with splash spotting: 1–3 salvos.

---

## 5. Historical hit rates

All counts are main-battery heavy shells unless noted.

| Action | Firing side / ship | Rounds | Hits | % | Range | Conditions / fire control | Source |
|---|---|---|---|---|---|---|---|
| Yellow Sea 1904 | Japanese 12" | 603 | ~30 | 4.7% | — | optical | https://navweaps.com/Weapons/WNJAP_12-40_EOC.php |
| Tsushima 1905 | Japanese 12" | 446 | ~40 | ~9% | mostly ≤6.5 kyd | Barr & Stroud FA3 rangefinder | same |
| Falklands 1914 | Invincible + Inflexible 12" | 513 + 661 = 1,174 | ~40 per armored cruiser, so ~80 | ~7% [INFERRED; hit counts UNCERTAIN] | long, closing | smoke interference, long chase | https://www.navweaps.com/index_oob/OOB_WWI/OOB_WWI_Falklands.php |
| Dogger Bank 1915 | Lion / Tiger / Princess Royal / New Zealand / Indomitable | 243 / 355 / 271 / 147 / 134 = 1,150 | 4 / 3 / 1 / 0 / 8 (Indomitable's were mostly on crippled Blücher) | 1.4% overall (Lion 1.6%, Tiger 0.8%) | opened at 20 kyd | clear; Germans hampered by their own smoke | https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915) |
| Dogger Bank 1915 | Seydlitz / Moltke / Derfflinger | 390 / 276 / 310 | 8 / 8 / 5–6 | 2.1% / 2.9% / ~1.8%; overall ≈2.5% | 17–18 kyd | — | same [table and text disagree: UNCERTAIN] |
| Jutland 1916 | British 1st/2nd BCS | 1,469 | 21 | 1.4% | 10–18 kyd | haze; optical | https://en.wikipedia.org/wiki/Damage_to_major_ships_at_the_Battle_of_Jutland |
| Jutland 1916 | British 3rd BCS (Hood's) | 373 | 16 | 4.3% | short | — | same |
| Jutland 1916 | British 5th BS (15") | 1,099 | 29 | 2.6% | to 19.5 kyd | — | same |
| Jutland 1916 | British Grand Fleet BS | 1,593 | 57 | 3.6% | 9–12 kyd | — | same |
| Jutland 1916 | British total | 4,534 | 123 | 2.7% | — | — | same |
| Jutland 1916 | German 1st Scouting Group | 1,670 | 67 | 4.0% | — | — | same |
| Jutland 1916 | German battleships | 1,927 | 57 | 3.0% | — | — | same |
| Jutland 1916 | German total | 3,597 | 124 | 3.4% | — | — | same |
| Jutland 1916 | New Zealand + Tiger | 723 | 6 | 0.8% | — | — | https://www.jutland1916.com/?p=33175 |
| River Plate 1939 | Graf Spee 11" | 414 | ~10 | ~2.4% | opened just under 20 kyd | optical | https://chuckhillscgblog.net/2016/12/16/the-mk38-gun-mount-and-ballistics-and-weapons-effectiveness-lessons-from-pursuit-of-the-graf-spee-part-1/ |
| River Plate 1939 | Exeter 8" | 193 | 3 | 1.6% | — | — | same |
| River Plate 1939 | Ajax + Achilles 6" | 2,064 | 17 | 0.8% | to ~8 kyd | — | same |
| Lofoten, Apr 1940 | Renown 15" | 230 | 3 | 1.3% | long | storm; heavy seas | https://en.wikipedia.org/wiki/Action_off_Lofoten |
| Lofoten, Apr 1940 | Scharnhorst + Gneisenau 11" | 182 + 54 | 0 + 2 | 0.8% | long | radar failing | same |
| Calabria, Jul 1940 | Warspite 15" | ? | 1 on Cesare | — | 26.2–26.4 kyd | longest-range BB-on-BB hit (tied with Scharnhorst vs Glorious) | https://navweaps.com/index_tech/tech-006.php |
| Calabria, Jul 1940 | Cesare | ? | 0 | — | opened at 26.4 km | — | https://en.wikipedia.org/wiki/Battle_of_Calabria |
| Spartivento, Nov 1940 | Vittorio Veneto 15" | 19 (7 salvos) | 0 (splinters only) | 0% | 27 km | — | https://en.wikipedia.org/wiki/Battle_of_Cape_Spartivento |
| Matapan (day), Mar 1941 | Vittorio Veneto | 94 (29 salvos) | 0 | 0% | ~25 kyd | — | https://en.wikipedia.org/wiki/Battle_of_Cape_Matapan |
| Matapan (night), Mar 1941 | Warspite / Valiant / Barham | n/a | most rounds hit; Zara and Fiume wrecked | very high | ~3.8 kyd | surprise; searchlights; radar | same |
| Denmark Strait, May 1941 | Bismarck 15" | 93 | ~5–6 (Hood 1–3, PoW 4) | ~5–6% [INFERRED] | 22 → 18 kyd | optical, Zeiss rangefinders | https://navweaps.com/Weapons/WNGER_15-52_skc34.htm, https://kbismarck.com/denmark-strait-battle.html |
| Denmark Strait, May 1941 | Prinz Eugen 8" | 157 | 4 (Hood 1, PoW 3) | ~2.5% | — | — | https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait, https://kbismarck.com/denmark-strait-battle.html |
| Denmark Strait, May 1941 | Prince of Wales 14" | ~55 in 18 salvos [UNCERTAIN] | 3 | ~5% | 26.5 kyd opening | spray on turret rangefinders; gun defects (lost 26% of output) | https://en.wikipedia.org/wiki/HMS_Prince_of_Wales_(53) |
| Denmark Strait, May 1941 | Hood | ? | 0 | 0% | — | — | same |
| Bismarck's last battle, May 1941 | Rodney 16" | 375–380 | ~40 | ~11% [INFERRED] | 20 → 3 kyd | optical | https://navweaps.com/index_tech/tech-016.php |
| Bismarck's last battle, May 1941 | King George V 14" | 339 | ~40 | ~12% [INFERRED] | from 25.1 kyd | Type 284 radar until 0913; first ranged on Rodney's splashes | same |
| Bismarck's last battle, May 1941 | Norfolk / Dorsetshire 8" | 527 / 254 | ? | — | — | — | same |
| Bismarck's last battle, May 1941 | All British, all calibers | 2,876 | 300–400 (?) | ~10–14% [UNCERTAIN] | — | target crippled and steering erratically | https://kbismarck.com/bismarck-last-battle.html |
| Guadalcanal, 14–15 Nov 1942 | Washington 16" | 75 | 8 (Lee's count) to 20 | 11–27% | 8.4 → 7.85 kyd | night; Mk 3 / Mk 8 radar plus star shell | https://www.navweaps.com/index_lundgren/kirishimaDamageAnalysis.php, https://en.wikipedia.org/wiki/Naval_Battle_of_Guadalcanal |
| Guadalcanal, 14–15 Nov 1942 | Washington 5" | 107 | 17–40 | 16–37% (40 judged too high) | ~8 kyd | — | same |
| Guadalcanal, 14–15 Nov 1942 | Japanese vs South Dakota | ? | 27 (3 × 14", 6 × 6", 16 × 8", 2 × 5") | — | ~5–6 kyd [UNCERTAIN] | searchlight illumination | Wikipedia (above) |
| Komandorski, Mar 1943 | Salt Lake City 8" | 832 | ~2–5 on Nachi [UNCERTAIN; Wikipedia credits Richmond with 4] | ~0.5% | 21 → 14 kyd | 3.5 h stern chase; smoke | https://en.wikipedia.org/wiki/Battle_of_the_Komandorski_Islands, https://history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-016/h-016-1.html |
| Komandorski, Mar 1943 | Nachi + Maya 20 cm | ~1,600+? [UNCERTAIN] | ~5 on Salt Lake City + 3 on destroyers | <1% | ~20 kyd | 200+ near misses within 50 yd | same |
| North Cape, Dec 1943 | Duke of York 14" | 446 (52 salvos) | ≥13 | ~3% | 12 kyd at start; long-range phase to ~20 kyd | night; Type 284 radar; 31 straddles | https://navalofficer.com.au/?p=463, https://en.wikipedia.org/wiki/German_battleship_Scharnhorst |
| North Cape, Dec 1943 | Duke of York 5.25" | 686 | ? | — | — | — | same |
| North Cape, Dec 1943 | Norfolk 8" / three 6" cruisers | 161 / 874 | "several" | — | — | — | same |
| Surigao, Oct 1944 | West Virginia / Tennessee / California / Maryland / Mississippi | 93 / 69 / 63 / 48 / 12 = 285 AP | unknown; Yamashiro badly hit | — | 19–21 kyd | night; Mk 8 radar vs Mk 3 radar | https://www.navweaps.com/index_tech/tech-079.php |
| Samar, Oct 1944 | Japanese force | ? | several on Gambier Bay, Johnston, Hoel (40+ hits), Samuel B. Roberts | very low ("not good") [UNCERTAIN] | 10–35 kyd | rain, smoke, evasive targets, AP passing through | https://history.navy.mil/browse-by-topic/wars-conflicts-and-operations/world-war-ii/1944/samar.html, https://destroyerhistory.org/actions/index.asp?pid=4583 |
| Samar, Oct 1944 | Johnston 5" | 30 rounds in 40 s | ≥15 on Kongo superstructure (claimed) | ~50% (claim) | ~7 kyd | radar | destroyerhistory.org (above) |

### 5.1 Shore bombardment (area fire; hit % is not meaningful)

| Campaign | Ship(s) | Rounds | Results | Source |
|---|---|---|---|---|
| Vietnam, 1968–69 | New Jersey | 5,688 × 16" + 14,891 × 5" | e.g. 182 structures and 54 bunkers destroyed in 2 days near Quang Ngai | https://nationalinterest.org/node/209660 |
| Gulf War, 1991 | Wisconsin + Missouri | 1,083 × 16" in 80 missions (about 13.5 per mission) | 52% of missions UAV-spotted. Of 68 assessed targets: 32% destroyed, 26% heavily damaged, 10% neutralized | https://www.gulflink.osd.mil/histories/db/navy/usnavy_178.html |
| Falklands, 1982 | Avenger, Yarmouth, Glamorgan, Arrow (night of 11–12 Jun) | 788 × 4.5" | one fuel dump, one 155 mm gun, some troop positions; judged unable to kill hard targets; value mainly suppression | https://www.usni.org/magazines/naval-history-magazine/2022/april/royal-navys-role-east-falkland-island-land-ops-1982 |

**Korea.** No rounds-per-target figures were found [UNCERTAIN]. Model shore fire as area coverage with about 10–20 heavy rounds per point target.

---

## 6. Practice vs. combat

**USN Long Range Battle Practice (LRBP)**
- 1932–33: Colorado, Maryland and West Virginia at 27.5–32.1 kyd scored 4.2%, 5.4% and 3.7% (average 4.4%). Average MPI error was 313 yd.
- 1939 era: 13 battleships fired 1,179 shots at a battle raft at 27,450 yd and scored 60 hits (5.1%). The raft is a small target.
- Reduced-charge practice at about 16 kyd was meant to simulate about 22 kyd with 14" and about 26 kyd with 16".
- 1927–28: 0 hits at about 25 kyd.
- Short Range Battle Practice: 80–90% hits.
- Night Battle Practice at about 5 kyd: 6.6–40.5%.
- Source: https://navweaps.com/index_inro/INRO_BB-Gunnery.php

**Hits per gun per minute (HPGPM)**
- WWI era: about 0.165.
- 1935 expectation: 0.32.
- 1940 standard: 0.75–0.84 for 14" batteries at 12.7 kyd.
- WWII gunners expected about 3.5× WWI's hits at 15 kyd.
- Source: same.

**IJN**
- Projected hit rate at 21,870 yd was 3% (1930–34) and 6% (1935–40). Wartime results were "much lower."
- In the 1932 Aso shoot, Nachi and Myoko repeatedly straddled at 15.8–22.3 km but scored zero hits.
- Source: https://www.navweaps.com/Weapons/WNJAP_8-50_3ns.php

**Combat degradation factor [INFERRED]**

Comparing practice expectations with combat at similar ranges:

| Comparison | Practice | Combat | Ratio |
|---|---|---|---|
| USN 16" at 20 kyd | 10.5% | Denmark Strait / North Cape: 3–5% | about 0.3–0.5 |
| IJN projected (6%) | 6% | Komandorski / Samar: under 1% | about 0.1–0.2 |
| WWI RN at 12–16 kyd | not quantified; pre-war hopes higher | Jutland: 2.7% | — |

Close-range night radar actions (Washington; the night phase at Matapan) can match or exceed practice rates. Bismarck's last battle reached about 11% because the target was crippled and the range was short.

**Suggested model multipliers [INFERRED]**

| Situation | Multiplier on practice rate |
|---|---|
| Clear day, first engagement, undamaged | 0.5 |
| Smoke, haze, high speed, target evading | 0.2–0.3 |
| Long stern chase | 0.1–0.2 |
| Target crippled or not maneuvering | 1.0 |

**Other degradation sources**
- **Gun defects.** Prince of Wales lost 26% of her output. Rodney achieved 77% of possible salvos.
  - https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait
  - https://navweaps.com/Weapons/WNBR_16-45_mk1.php
- **Fire-control errors in turns.** California averaged 212 yd of deflection error during a turning practice. INRO, above.

---

## 7. Target size table

[INFERRED] These are representative values from general naval-architecture knowledge, not a single cited source. Freeboard is the target height for hitting purposes, including superstructure only where it is armored or vital.

| Type | Example | Length (ft) | Beam (ft) | Freeboard amidships (ft) | Effective height (hull + lower superstructure, ft) |
|---|---|---|---|---|---|
| WWII battleship | Iowa / Bismarck / Yamato | 790–887 | 108–127 | 18–28 | 30–40 (USN citadel 20; RN 30) |
| WWI dreadnought / battlecruiser | Iron Duke / Lion / Derfflinger | 620–700 | 90–97 | 15–25 | 30 |
| Heavy cruiser | Myoko / Baltimore / Hipper | 600–675 | 62–71 | 15–25 | 25 |
| Light cruiser | Cleveland / Leander | 550–610 | 55–66 | 15–20 | 20–25 |
| Destroyer | Fletcher / Fubuki / Tribal | 340–380 | 36–40 | 10–15 | 12–15 |
| Escort / DE / sloop | John C. Butler / Flower | 200–310 | 33–37 | 8–12 | 10–12 |
| Torpedo boat / MTB / PT | Elco 80 / S-boat | 70–115 | 17–21 | 4–6 | 5–8 |

Citadel heights: https://mathscinotes.com/2013/04/battleship-guns-and-danger-space/

Hit-space notes:
- The deflection hitting space is the presented length: L·|sin(target angle)| + B·|cos(target angle)|.
- The range hitting space is the danger space plus the presented depth: L·|cos| + B·|sin|.
- An end-on target gains depth but loses width. The 1944 study shows end-on hits at about 40–70% of broadside rates (§3.4).

---

## 8. Key gaps / uncertainties

- No per-gun dispersion tables found for the 38 cm SK C/34, 4.7"/45, 6"/47 or 3"/76. Defaults in §1.2 are inferred.
- Hit counts at Falklands 1914, Samar, Surigao and Komandorski are poorly established.
- Bismarck's last battle and Washington vs Kirishima carry wide uncertainty ranges (Washington: 8 to 20 hits).
- The 1987 Iowa "shell-to-shell 123 yd" figure: unclear whether it is a σ or a mean deviation.
- Jutland totals differ between sources: Wikipedia's 4,534 / 123 versus figures from Campbell in other tallies.
