# 05 — Ship Manoeuvring Performance (1900–1990) for a Nomoto-style Steering/Speed Model
Status: final    Updated: 2026-10-09    Request: -

Purpose: quantitative turning, speed-change and rudder data per ship class, to drive a first-order (Nomoto) yaw model with a rudder-rate limit, speed loss in turns, and asymmetric acceleration/deceleration, and to estimate how far a target can move off its predicted straight track during a shell's 10–90 s time of flight.

Tags: **[INFERRED]** = derived or calibrated by me. **[UNCERTAIN]** = thin, conflicting or OCR-garbled source. Everything else has a source URL next to it.

Bottom line:
- Hard data is scarce. Published turning trials survive mainly as one number per class: the **tactical diameter (TD) at full speed**.
- I calibrated a Nomoto + speed-loss model to those TDs. It reproduces the few time-history anchors I found: Titanic needs about 37 s to turn 22.5°; Iowa and Ticonderoga need about 2 min to turn 180°.
- **For shell flight times of 30 s or less, no ship larger than a destroyer can move more than about 1–1.5 beams off its predicted track by turning.** Big deviations need 45–90 s.
- Small craft (PT boats, fast attack craft) can complete a whole circle inside one shell's flight time.

---

## 1. Hard sourced anchors (the data the model is calibrated to)

| Ship (class) | L (m) | Max spd (kn) | Tactical diameter | Speed / rudder | Other sourced figures | Source |
|---|---|---|---|---|---|---|
| Iowa (BB) | 270 oa / 262 wl | 32.5–33 | **814 yd (744 m)** at 30 kn; **760 yd (695 m)** at 20 kn | twin semi-balanced rudders | — | https://en.wikipedia.org/wiki/Iowa-class_battleship |
| New Jersey (BB-62), 1988 | — | 30+ | **760 yd at 15 kn, 35° rudder**; does not grow much at higher speed | rudder limit 36.5° each side, independent; hard 35°, "full" 25° | 180° takes about 2 min at 15 kn, less at higher speed. Standard rudder for a turn over 30° at 15 kn loses **2–3 kn** even holding shaft turns. Slowing: **more than 200 yd per knot above 25 kn, about 120 yd per knot between 10 and 20 kn; 25→10 kn takes about 3,200 yd**. 3.7 shp/ton, "slow to accelerate". Practice rule: rudder° + knots ≤ 30. Without superheat (about 30 min to raise) top speed is 22 kn. | https://www.usni.org/magazines/proceedings/1988/april/handling-battleship |
| Iowa, crash stop | — | — | — | — | Full astern from full ahead: **a little over 1 nm** before going astern. "Barn-door" stop (rudders toed inboard plus backing) claimed at **about 600 ft from 33 kn** (Wisconsin only) | https://navweaps.com/index_tech/tech-054.php |
| Yamato (BB) | 263 oa / 256 wl | 27.46 (trial) | **640 m** | **26 kn, 35° rudder** | Advance **589 m**; **max heel 9.0°**; GM 2.6 m. The auxiliary rudder alone could not check the swing. | https://www.usni.org/magazines/proceedings/1953/october/design-and-construction-yamato-and-musashi |
| US "Standard type" BB (Nevada→Colorado) | ~177–190 | 21 | about **700 yd** "at that speed" (21 kn) | — | Wikipedia instead says *radius* 700 yd **[UNCERTAIN: diameter vs radius]** | https://www.navweaps.com/index_tech/tech-071.php ; https://en.wikipedia.org/wiki/Standard-type_battleship |
| Maryland (BB-46), 1924 trial | 190 | 21 | — | "standard half rudder" (exact angle unknown) | **12.4 → 9.5 kn after a 180° turn (−23%)**. Almost all of the loss happened in the first 90°, peaking around 70°. | https://www.usni.org/magazines/proceedings/1925/september/retardation-ships-speed-due-turning |
| Generic capital ship, 1928 | — | 15 | 1,000 yd (formation standard) | — | Coasting about **100 yd per knot** of speed reduction | https://www.usni.org/magazines/proceedings/1928/may/turning-track-guide |
| Alaska (CB) | 246 | 33 | "turning radius 800 yd" (likely TD) **[UNCERTAIN]** | single rudder | Described as not agile | https://www.navygeneralboard.com/the-alaska-class-americas-not-quite-battlecruisers/ |
| Ticonderoga (CG) | 173 | 30+ | **750 yd (686 m)** | **25 kn, 30° rudder** | 180° in "a little over 2 min". **Flank to dead in water: 2 ship lengths or 60 s.** Gas turbines: no-load idle to full power ≤30 s. "Lays over hard" in a full-rudder turn. Rule: rudder° + kn ≤ 30. | https://www.usni.org/magazines/proceedings/1987/january/handling-ticonderoga |
| Fletcher (DD) | 114.8 | 36–38 | **950 yd (869 m)** | at 30 kn | **single rudder** | https://destroyerhistory.org/fletcherclass/index.asp?pid=200 |
| Benson/Gleaves (DD) | 106 | 36.5–37 | **960 yd** | at 30 kn | — | https://destroyerhistory.org/benson-gleavesclass/index.asp?r=0&pid=220 |
| Allen M. Sumner (DD) | 114.8 | 34 | **700 yd (640 m)** | at 30 kn | **twin rudders** | https://destroyerhistory.org/sumner-gearingclass/index.asp?r=0&pid=10 |
| Gearing (DD) | 119 | 34 | **725 yd (663 m)** | at 30 kn | twin rudders | same |
| Mogador (French DD leader) | 137.5 | 39 | **800–850 m on trials**; about **double that in service** | at 25 kn | Rudder took **25–30 s to reach its 32° maximum** at speed | https://en.wikipedia.org/wiki/Mogador-class_destroyer |
| Arleigh Burke Flt I / IIA (DDG) | 154–155 | 31 | TD not published. Flt IIA vs Flt I at 30 kn, 15° rudder, 90° turn: advance −73 yd, transfer +96 yd | — | **Accel: stop→flank 74 s / 820 yd (Flt I); 111 s / 1,250 yd (IIA)**, nearly half of it spent going from 25 kn to top speed. **Flank→zero, back full: 61 s / 540 yd (3.2 L)** | https://www.usni.org/magazines/proceedings/2002/june/handling-arleigh-burkes-part-three |
| Arleigh Burke, 1994 | — | — | — | — | **Heel 8–10° at full speed, full rudder**. All stop → flank in under 90 s. Ahead full → dead in water about 1 min, under 600 yd. | https://www.usni.org/magazines/proceedings/1994/october/handling-arleigh-burkes |
| Spruance (DD) | 172 | 30+ | — | — | Responds in **about half the time of steam destroyers**. Slowing for station-keeping starts at 20–25 yd per knot of overspeed, versus the older 50 yd/kn | https://www.usni.org/magazines/proceedings/1979/october/handling-spruance-class-destroyer |
| O.H. Perry (FFG-7) | 136 | 29+ (2 GT), 25 (1 GT) | not given | rudder: standard 15°, full 30°, hard 35° | single controllable-pitch screw; flank 180 rpm | https://www.usni.org/magazines/proceedings/1990/january/handling-ffg-7 |
| Hypothetical 1964 ship (OOD teaching example) | — | 25 | standard rudder: **800 yd at 15 kn; 1,000 yd at 25 kn** | — | 90° in about 2 min at 15 kn and 1.5 min at 25 kn; 180° in 4 and 3 min. Speed decay in a standard-rudder turn about **half** the straight-line value. Coasting about 50 yd/kn. | https://www.usni.org/magazines/proceedings/1964/february/professional-notes-notebook-and-progress |
| Olympic/Titanic (liner, useful 1910s big-ship calibration) | 269 | 22.5 | **3,860 ft (1,177 m)** at 22.5 kn, 40° helm | helm hard over in about **10 s** (≈4°/s, telemotor) | **2 points (22.5°) in 37 s**. Steady speed 17.4 kn (**−23%**). 360° in 412 s. Drift angle 8°, heel 6°. Advance 2,746 ft; transfer at 90° 1,745 ft. | https://encyclopedia-titanica.org/articles/2_points_in_37_seconds.pdf |
| USS Edenton (ATS-1), 1971 trial (slow diesel twin-screw, CPP) | 86 | 16 | (plots only) | — | **0→16 kn in 160 s / 960 yd. Crash stop full/full from 16.1 kn: 67 s / 290 yd**. Half/full from 14.1 kn: 70 s / 280 yd | https://apps.dtic.mil/sti/pdfs/AD0904368.pdf |
| Higgins "Hellcat" PT (70 ft) | 21 | 46 | — | — | **Reversed course in 9 s**; a Higgins 78 ft took **22 s** | https://ibiblio.org/hyperwar/USN/CloseQuarters/PT-2.html |
| PT-487 Elcoplane (Elco 80 ft, stepped) | 24 | 54–56 | — | — | **180° in about 6 s** at top speed, finishing with sternway (very large speed loss) | https://www.navsource.net/archives/12/05487.htm |
| Elco 77/80 ft PT | 24 | 40–45 (trials), about 40 in service | — | — | 1941 tests: Huckins and Higgins boats had smaller turning circles than Elco PT-20 (no figures) | https://ibiblio.org/hyperwar/USN/CloseQuarters/PT-2.html |
| Brave-class FPB (UK, 1958) | 29 | 50+ | Staff requirement: TD at full speed about **5 boat lengths** (≈145 m) **[UNCERTAIN: OCR-garbled, may be "not more than"]** | — | — | https://bmpt.org.uk/other_boats_history/Brave%20class/index.htm |
| 16 m waterjet FAC (2014 trials) | 16 | 46 | — | — | **0→25 kn in 23.8 s; 0→35 kn in 34.4 s** | https://www.marinelog.com/news/mjp-waterjets-win-high-praise-in-proven-fast-attack-craft |
| S-boot S-26/S-38/S-100 | 34.9 | 43.5 sustained, 48 burst | — | — | Lürssen-effect side rudders (30° out, then 17°). On S-2 trials, **full rudder at high speed stopped producing a turn** | https://en.wikipedia.org/wiki/E-boat ; https://en.wikipedia.org/wiki/L%C3%BCrssen_effect |
| Osa I/II (missile FAC) | 38.6 | 38.5–42 | not found | — | — | https://en.wikipedia.org/wiki/Osa-class_missile_boat |
| Boghammar RL-130 | 12.8 | 45 | not found | — | — | https://www.globalsecurity.org/military/world/iran/boghammar-specs.htm |

Qualitative handling notes:
- **Nelson/Rodney:** small TD, especially into the wind. Weathervaned strongly at low speed because the bridge sits aft. Awkward astern because the single central rudder sits outside the twin propeller races. https://en.wikipedia.org/wiki/Nelson-class_battleship
- **US flush-deckers transferred to the RN** were criticised for "excessive tactical diameter". https://www.navweaps.com/index_tech/tech-072.php ; Canadian Town class "very poor turning circles": https://hazegray.org/navhist/canada/ww2/town/
- **Bismarck:** steering by propellers alone barely worked, even with the outer shafts at full power in opposite directions. https://en.wikipedia.org/wiki/German_battleship_Bismarck . Triple screws reportedly gave about a third less turning power than an equivalent four-screw layout, and the ship showed directional instability on Baltic trials. https://www.navweaps.com/index_inro/INRO_Bismarck.php
- **Not found despite targeted searches:** numeric TDs for Bismarck/Tirpitz, KGV (1939), Hood, Queen Elizabeth, Nelson, Hipper, Myoko, Baltimore, Cleveland, Tribal, J/K, Fubuki and Kagero. The kbismarck.org forum thread "Bismarck's turning radius" probably has them but is blocked to my fetcher. The RN handbook OU 5274 *Handling Ships* (1934/41), https://globalmaritimehistory.com/wp-content/uploads/2023/06/OU5274_Handling_Ships_1934_1941_OCRd.pdf, is the best next source but would not download.

### Derived TD/L ratios [INFERRED from above]
| Type | TD/L at full speed, hard rudder |
|---|---|
| Battleships (Iowa 2.8, Yamato 2.5, Standard ≈3.5–3.9) | **2.5–3.5** |
| Titanic (fine, single centre rudder) | 4.4 |
| Ticonderoga (30° rudder) | 4.0 |
| Destroyers, twin rudder (Sumner 5.6, Gearing 5.7) | **5–6** |
| Destroyers, single rudder (Fletcher 7.6, Benson ≈8.3), Mogador trials 6.0 | **6–8** |
| Planing craft | ≈3–6 boat lengths, but steep speed loss |
| IMO merchant limit (for comparison) | TD ≤ 5 L, advance ≤ 4.5 L (https://www.shipuniverse.com/?p=18478) |

The pattern is that **long, fine, fast destroyers turn in large circles relative to their length, while short, beamy battleships turn tightly in ship lengths**. In absolute metres, a 30-knot destroyer and a battleship turn in similar circles of about 640–870 m.

---

## 2. Per-class parameter table for the game model

This combines the sourced figures with calibrated values. TD is sourced where a ship is named. **Times to 90/180/360°, steady turn rate, advance and transfer are [INFERRED]**: they come from my Nomoto + speed-loss simulation, calibrated so the TD matches the sourced value. Calibration checks: Titanic gives 37 s to 22.5° (source 37.5 s) and 403 s for 360° (source 412 s). Ticonderoga gives 128 s for 180° (source "a little over 2 min"). Yamato's advance comes out 553 m against a sourced 589 m.

| Class (reference ship) | L m | Vmax kn | TD m (speed) | Adv90 / Tr90 m | t90 / t180 / t360 s | Steady r at full rudder °/s | Rudder hard-over | Speed loss in steady hard turn | Nomoto T s (T′=TU/L) |
|---|---|---|---|---|---|---|---|---|---|
| BB fast (Iowa) | 262 | 31 | **744 (30 kn)** | 630 / 410 | 62 / 109 / 213 | 1.6–1.7 | 35° in ~14–15 s (2.3–2.5°/s) [INFERRED from SOLAS-type rate] | 30–40% [INFERRED]; 2–3 kn at 15 kn with standard rudder (sourced) | 26 (1.5) |
| BB slow (Yamato) | 256 | 27 | **640 (26 kn)**, adv 589 | 553 / 357 | 61 / 107 / 206 | 1.7 | ditto | ~35% [INFERRED]; heel 9° (sourced) | 28 (1.5) |
| BB old (US Standard, QE, KGV-1911) | 180–195 | 21–24 | ~640 (21 kn) [UNCERTAIN] | 500 / 340 | 70 / 128 / 251 | 1.4 | ditto | 23% after 180° (Maryland, sourced, partial rudder) → ~30% hard [INFERRED] | 24 (1.4) |
| BC / fast large (Hood, Alaska, Renown) | 230–262 | 29–33 | **≈730–1,100** [INFERRED: TD/L 3–4.5; Alaska "800 yd" UNCERTAIN] | ~650 / 450 | ~70 / 125 / 245 | 1.3–1.5 | ditto | 30–40% [INFERRED] | 28–32 |
| CA / CL (Baltimore, Cleveland, Hipper, Myoko, Town) | 180–205 | 32–35 | **≈700–900** [INFERRED: TD/L ≈4 like Ticonderoga] | 560 / 400 | 60 / 120 / 240 | 1.4–1.5 | 35°, 2.3–2.5°/s | 35–40% [INFERRED] | 12–16 (1.0–1.3) |
| Modern CG (Ticonderoga) | 173 | 30+ | **686 (25 kn, 30° rudder)** | 475 / 350 | 65 / 128 / 257 | 1.4 | 30–35° | ~35% [INFERRED] | 14 (1.0) |
| DD twin rudder (Sumner, Gearing) | 115–119 | 34 | **640–663 (30 kn)** | 410 / 320 | 53 / 107 / 218 | 1.6 | 35° in ~14 s | 40% [INFERRED] | 8–9 (1.1–1.2) |
| DD single rudder (Fletcher, Benson, Tribal/J/K?, Fubuki/Kagero?) | 106–119 | 35–38 | **869–878 (30 kn)** | 530 / 435 | 72 / 146 / 295 | 1.2 | 35° in ~14 s | 40% [INFERRED] | 9 (1.2) |
| DD leader, slow steering (Mogador) | 137 | 39 | **800–850 trial; ~1,600 service (25 kn)** | 460 / 370 (trial) | 69 / 138 / 277 (trial) | 1.3 | **32° in 25–30 s (≈1.1–1.3°/s)** (sourced) | 35% [INFERRED] | 10 |
| DE / sloop / corvette (Flower, Hunt, Buckley) | 62–94 | 16–24 | **≈300–450** [INFERRED: TD/L ≈4–5, lower speed] | ~250 / 170 | ~40 / 80 / 160 | ~2.0 | 35°, 2.5°/s | 25–30% [INFERRED] | 6–8 |
| Frigate (Perry FFG-7, Knox) | 133–136 | 27–29 | **≈650** [INFERRED: single rudder, single screw, TD/L ≈4.8] | 430 / 330 | 55 / 112 / 228 | 1.5 | 35° hard (sourced), 2.5°/s | 40% [INFERRED] | 10 (1.1) |
| Missile FAC (Osa, Komar, Kılıç, La Combattante) | 25–62 | 38–42 | **≈200–260** [INFERRED: 5–6 L] | 150 / 110 | 14 / 28 / 58 | 5–6 | 30–35° in ~6 s (fast electro-hydraulic) [INFERRED] | 40–50% [INFERRED] | 3 |
| MTB / PT / S-boot (Elco 80, Vosper 73, Fairmile D, S-38) | 22–35 | 40–45 | **≈100–200** [INFERRED: 4–6 L]; S-boot larger and stiffer | 85 / 65 | 7 / **9–22 (sourced, 180°)** / 30 | 8–12 | ~3–4 s (hand/hydraulic) [INFERRED] | 40–60%. The Elcoplane ended a 180° turn with sternway (sourced) | 1–2 |
| Interceptor (Boghammar) | 13 | 45 | **≈60–100** [INFERRED] | — | 180° in ~5–8 s [INFERRED, by analogy with Hellcat/Elcoplane] | 15–25 | outboard/drive, ~2 s | 50%+ | <1 |

### Speed change (acceleration and deceleration)

| Class | Accel 0→full (or slow→full) | Decel: engines stopped (coast) | Crash stop (full astern) | Source / tag |
|---|---|---|---|---|
| Iowa (steam BB) | Slow. 3.7 shp/t. 15→30 kn ≈ 4–6 min if superheat is up; ~30 min to raise superheat if steaming without it | >200 yd/kn above 25 kn; 120 yd/kn at 10–20 kn. **25→10 kn ≈ 3,200 yd ≈ 5–6 min** [time INFERRED] | **>1 nm (~1,900 m)**, time ≈ 4–5 min [INFERRED]. Barn-door 600 ft claim **[UNCERTAIN: implies ~0.3 g deceleration, physically implausible]** | Proceedings 1988; navweaps tech-054 |
| Battleship 27→15 kn | — | Using the Iowa rule: ≈2,000 yd (1,800 m), **≈3 min** by stopping engines [INFERRED] | Backing: ≈1.5–2 min [INFERRED] | derived |
| Steam DD (WWII) | 0→30 kn ≈ 2–3 min [INFERRED: Spruance responds "about half the time" of steam DDs, and Flight I DDG does 74 s] | ~50 yd/kn (1964 OOD rule) | ~4–5 L, 90–120 s from 30 kn [INFERRED] | Proceedings 1979, 1964 |
| Gas-turbine DDG/CG (1980s) | **0→31 kn in 74 s (Burke Flt I), 111 s (IIA)**; Ticonderoga idle→full power ≤30 s | 20–25 yd/kn station-keeping | **61 s / 540 yd (Burke); 60 s / 2 L (Ticonderoga)** | Proceedings 2002, 1987 |
| Diesel auxiliary | 0→16 kn in 160 s | — | 67 s / 290 yd from 16 kn | DTIC AD0904368 |
| Planing FAC / PT | **0→25 kn 24 s; 0→35 kn 34 s** (16 m waterjet) | Drops off the plane in a few seconds; speed halves in ~5–10 s [INFERRED] | ~2–4 L, ~10 s [INFERRED] | marinelog |

**First-order speed time constants for `dU/dt = (U_cmd − U)/τ`** [INFERRED]:

| Class | τ accelerate | τ coast (engines stopped) | τ backing |
|---|---|---|---|
| BB/BC | 150–250 s | ~350 s at 30 kn, from 200 yd/kn | ~90–120 s |
| CA/CL | 90–150 s | 200–300 s | 60–80 s |
| Steam DD | 50–70 s | 150–200 s | 35–45 s |
| Gas-turbine DD/FF | 25–35 s | — | 20–25 s, from 61 s to zero |
| FAC/PT | 8–12 s | 10–20 s | 5 s |

Use quadratic drag for coasting rather than a pure exponential; resistance rises steeply above hump speed, so high-speed coasting decays fast at first.

---

## 3. Nomoto implementation notes

- **Yaw:** `T·dr/dt + r = K·δ`, with `K` and `T` scaling as `K = K′U/L` and `T = T′L/U`.
  - Calibrated **T′ ≈ 1.0–1.5 for all displacement warships** [INFERRED]. That gives T ≈ 25–30 s for battleships at 25–30 kn, 12–16 s for cruisers and 8–10 s for destroyers.
  - Lewis/PNA-style K′ and T′ for merchant hulls vary enormously. Mariner-type values such as T′ ≈ 107 are unstable-hull artefacts, and first-order K and T depend on the manoeuvre used to fit them. Calibrate to TD rather than trust a literature K′. https://jmstt.ntou.edu.tw/cgi/viewcontent.cgi?article=2525&context=journal ; https://yadda.icm.edu.pl/baztech/element/bwmeta1.element.baztech-8a3f886e-12d7-4eb8-9390-7e0b14c5b3b9/c/Annual_25_Artyszuk.pdf
- **Choosing K:** pick K so the steady yaw rate at full rudder is `r_ss = U_ss / R_ss`. Use `R_ss ≈ 0.43–0.47 × TD`, because the steady diameter is about 0.85–0.95 of TD [INFERRED]. Saturate δ_eff for nonlinearity (see §4 on partial rudder).
- **Rudder rate:**
  - Merchant SOLAS standard: 35° one side to 30° the other in **28 s ≈ 2.3°/s** (https://laws-lois.justice.gc.ca/eng/regulations/sor-90-264/page-9.html). Use 2.3–2.5°/s for warships of 1920–1990.
  - Titanic's telemotor reached its 40° hard over in about 10 s (https://encyclopedia-titanica.org/articles/2_points_in_37_seconds.pdf).
  - Mogador's 25–30 s to 32° is a slow outlier.
  - Pre-1914 steam or hand gear: 1–2°/s [INFERRED].
- **Speed loss:** relax U toward `U0·(1 − f·(r/r_ss)²)`, with f = 0.25 for liners and old battleships, 0.35 for battleships and cruisers, 0.40 for destroyers and 0.5–0.6 for planing craft. Use a lag of about 1.5·T.
  - Sourced points: 23% for Titanic and Maryland; "about half" for the 1964 example; a 60% loss cited for a merchant ship's steady hard turn (https://www.jmr.unican.es/jmr/article/download/20/18/21); 2–3 kn of 15 kn for Iowa on standard rudder.
  - Most of the loss occurs in the first 90° of the turn (Maryland). Hold shaft rpm constant during the turn.
- **Heel** (cosmetic or gunnery penalty): steady 6° (Titanic), 9° maximum (Yamato), 8–10° (Burke at full speed). The transient outward heel at rudder application can be 2–4× the steady heel, and the IMO guidance limit is 10° (https://nippon.zaidan.info/seikabutsu/2003/00574/contents/0118.htm) **[UNCERTAIN: transient multiplier]**.

---

## 4. Partial rudder, rudder jam, twin vs single, quick reversals

**Partial rudder.** Trawler trials (Atlantic class, 13 kn; https://www.transnav.eu/html,205.html) give TD in cables of 2.35 at 15° rudder, 1.73 at 25° and 1.51 at 35°.
- 15° gives **≈1.55× the hard-over TD**, not 2×. 25° gives ≈1.15×.
- At slow speed 15° gives ≈1.6×.
- The 1964 standard-rudder example gives 1,000 yd at 25 kn, about 1.4× a typical destroyer hard-over figure.
- For the game: `TD(δ) ≈ TD(35°) × (35/δ)^0.5–0.6` [INFERRED fit].
- Doctrine at high speed: New Jersey and Ticonderoga officers kept rudder° + knots ≤ 30, so a ship cruising at 25 kn normally used 5° of rudder. Full rudder was an emergency or combat action (USNI 1987, 1988 above). In a game, AI ships should use 10–15° rudder for routine course changes and hard over only for evasion.

**Rudder jam.**
- Bismarck's rudders jammed at **12° port**. She circled uncontrollably until speed was cut, then could not hold any course away from the wind and sea (https://www.navweaps.com/index_inro/INRO_Bismarck.php ; https://en.wikipedia.org/wiki/German_battleship_Bismarck).
- Hood's wreck shows her rudder locked at 20° port, the setting in use when she exploded (https://en.wikipedia.org/wiki/HMS_Hood).
- Model: δ fixed at the jam angle. Differential shaft power can counter only a small fraction, roughly ≤3–5° of equivalent rudder on Bismarck-type triple screws [INFERRED from "only a slight turning ability"].
- Yamato carried an auxiliary rudder for this case, but trials showed it alone could not check the swing (USNI 1953 above).

**Twin vs single rudder.**
- At the same speed, twin-rudder Sumner and Gearing had a **TD 24–26% smaller** than single-rudder Fletcher (700/725 yd against 950 yd at 30 kn; destroyerhistory.org above).
- Battleships with twin rudders (Iowa, the South Dakotas, Yamato's main rudder) achieve TD/L ≈2.5–3. Single-rudder Alaska and Nelson are known as awkward, Nelson especially going astern.
- Rule [INFERRED]: single rudder gives ×1.25–1.35 TD and a slightly larger T.

**Quick reversal (full one way, then shift to full the other).**
- Titanic model: helm shifted at heading 10° → swing peaks at 18.5° at 37.5 s. Shifted at 14.2° → peaks at 22.9° at 45 s. Shifted at 23° → peaks at 31.9° at 55 s. It takes **17–18 s to check the swing** after the shift (encyclopedia-titanica above).
- My calibrated model [INFERRED]:

| Ship | Shift at heading | Peak heading (time) | Back through original heading | Max lateral offset of S-curve |
|---|---|---|---|---|
| Iowa (30 kn) | 10° | 16° (25 s) | 45 s | ~100 m |
| | 20° | 29° (35 s) | 64 s | ~250 m |
| | 30° | 41° (43 s) | 80 s | ~420 m |
| Fletcher (30 kn) | 10° | 14° (16 s) | 31 s | ~50 m |
| | 20° | 25° (24 s) | 46 s | ~130 m |
| | 30° | 35° (30 s) | 59 s | ~220 m |

- **Overshoot after the shift is ~6–11°.** A full S-curve "zig" for a battleship takes ~60–80 s and for a destroyer ~45–60 s.
- A full-rudder zig-zag can therefore put a big ship roughly ±100–400 m off its base track on a ~1–1.5 min cycle, while its mean advance continues.

---

## 5. Deviation from a predicted straight track within 10–90 s (hard over at t = 0)

Values are [INFERRED] from the calibrated simulation. Each cell gives **lateral offset / total miss distance** in metres from the dead-reckoned point. Total miss includes along-track shortfall from the curved path and speed loss. The rudder starts at 0°, so the first ~10–15 s are mostly rudder travel plus the yaw lag T.

| Ship / speed | 10 s | 20 s | 30 s | 45 s | 60 s | 90 s |
|---|---|---|---|---|---|---|
| Iowa 30 kn | 4 / 4 | 29 / 29 | 84 / 89 | 220 / 250 | 393 / 492 | 685 / 1130 |
| Yamato 26 kn | 3 / 3 | 24 / 25 | 72 / 75 | 191 / 214 | 343 / 424 | 596 / 982 |
| Standard BB 21 kn | 2 / 2 | 17 / 18 | 51 / 54 | 136 / 151 | 251 / 300 | 488 / 713 |
| Titanic-type liner 22.5 kn | 1 / 1 | 10 / 10 | 30 / 31 | 86 / 91 | 171 / 187 | 408 / 485 |
| Cruiser 32 kn | 7 / 7 | 41 / 47 | 106 / 130 | 243 / 323 | 403 / 585 | 684 / 1243 |
| Ticonderoga 25 kn | 5 / 5 | 30 / 34 | 78 / 93 | 182 / 235 | 308 / 429 | 550 / 930 |
| Sumner DD 30 kn | 10 / 13 | 52 / 74 | 120 / 175 | 249 / 387 | 389 / 655 | 604 / 1301 |
| Fletcher DD 30 kn | 7 / 10 | 38 / 57 | 90 / 137 | 197 / 306 | 327 / 524 | 600 / 1076 |
| Perry FFG 29 kn | 8 / 10 | 46 / 60 | 109 / 150 | 233 / 345 | 370 / 596 | 594 / 1209 |
| Osa FAC 38 kn | 62 / 85 | 177 / 320 | 215 / 607 | — | — | full circles: miss distance ≈ dead-reckoned run (~1,100–1,800 m) |
| Elco PT 40 kn | 94 / 160 | circling | — | — | — | miss ≈ DR distance |

**Speed change only (no turn)** [INFERRED]:
- Iowa stopping engines at 30 kn falls short of its dead-reckoned position by only **22 m at 30 s, 83 m at 60 s and 176 m at 90 s**.
- A gas-turbine destroyer crash-backing from 31 kn stops in 61 s / 494 m, against ~970 m of dead-reckoned run. That is **~480 m short at 60 s and ~960 m at 90 s**.
- So for modern destroyers and frigates, **crash-back is as effective an along-track dodge as a hard turn**. For steam battleships, speed change is nearly useless inside 90 s.

**Gunnery implications** [INFERRED]:
- For shell flight times of ≤20 s (≤~10 km), even destroyers move <75 m off track. Targets are effectively predictable, and dispersion dominates.
- At 30–45 s (15–25 km), battleships move 90–250 m and destroyers 140–400 m. That is comparable to or larger than a salvo pattern of ~200–400 m, so "chasing salvos" and zig-zag evasion become meaningful.
- At 60–90 s (30–40 km), every ship can be 400–1,300 m off the predicted point. Long-range hits on a manoeuvring target are largely luck, consistent with the historical very low hit rates.

---

## 6. Gaps and how to fill them
- RN OU 5274 *Handling Ships* (https://globalmaritimehistory.com/wp-content/uploads/2023/06/OU5274_Handling_Ships_1934_1941_OCRd.pdf) probably has tabulated RN battleship, cruiser and destroyer turning data. Fetching failed (client error / proxy 403).
- The US destroyer General Information Books at destroyerhistory.org/destroyers/records (scanned PDFs, no text layer) include tactical data tables. They need OCR, but downloading was blocked.
- I found no numeric data for Bismarck, KGV, Hood, Hipper, Myoko, Fubuki/Kagero, Tribal, S-boot, Osa or Boghammar turning. Their rows in §2 use TD/L analogues and are flagged.
