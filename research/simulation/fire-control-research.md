# Fire control: from MK1 Eyeball to automated gunnery radar
Status: final    Updated: 2026-10-09    Request: -

*Research for the gunnery model. This is the piece the damage docs keep pointing to: [`08-gunfire-effects.md`](damage-research/08-gunfire-effects.md) starts once a shell has hit, and this doc decides whether it hits. It covers the whole chain: seeing the target, measuring range and bearing, computing a solution, turning it into gun orders, laying the guns, shell dispersion, the hit test against the real hull, and spotting the fall of shot to correct the next salvo.*

*Earlier and degraded gunnery (1860–1912 gunlayers, firing on the roll, smoke, splash identification, and the MK1 eyeball in WWII emergencies) is in [`manual-gunnery-research.md`](manual-gunnery-research.md). Evasion and counter-prediction are in [`evasion-research.md`](evasion-research.md). How the tech tree's physical values become designer choices (rangefinder size, directors, radars) and combat-model inputs is in [`fire-control-designer.md`](fire-control-designer.md).*

*Scope runs from the 1890s (estimate by eye, fire by individual gunlayers) through dreadnought optical systems and WWII radar to 1970s digital directors. It covers surface fire against ships. AA fire control appears only where it shares hardware (Mk 37, Mk 56).*

**Tags**, as in the other research docs:
- **[S]** means sourced; the URL is beside the fact or in the source list.
- **[INFERRED]** marks my own derivation, fit or tuning value.
- **[UNCERTAIN]** marks thin or conflicting sources.

**Companion code:** [`fire_control_ref.py`](fire_control_ref.py) (numpy only, about 1,400 lines). `python3 fire_control_ref.py` prints every table in about 3 minutes; `quick` skips the Monte Carlo runs. It contains:
- point-mass ballistics fitted to each gun's maximum range;
- sensor models (eye, stadimeter, coincidence and stereo rangefinders, 11 radars, laser);
- four tracker types that stand in for plots, clocks, rangekeepers and post-war predictors, including ones that follow a turning target;
- 14 fire-control levels;
- a full engagement simulator with spotting;
- an exact box-hull hit test;
- the historical calibration cases.

**Raw research appendices**, each with full sourcing:
- [`computing.md`](fire-control-research/computing.md)
- [`radar.md`](fire-control-research/radar.md)
- [`dispersion-and-hits.md`](fire-control-research/dispersion-and-hits.md)

The rangefinding findings are folded into §3 below.

---

## 0. TL;DR for the model

1. **A miss has five independent causes. Model them separately, because each era fixes a different one.**

   | # | Cause | What drives it | What removes it |
   |---|---|---|---|
   | 1 | Range measurement error | Grows with range squared for optics; roughly constant for radar | — |
   | 2 | Rate error | The computer's wrong idea of target course and speed, multiplied by time of flight | Better computers |
   | 3 | Unknown ballistic bias | Wind, air density, muzzle velocity, drift. Before spotting it is about 0.3–2.5% of range | Spotting |
   | 4 | Salvo-to-salvo random error | Laying, roll, servo, salvo-mean muzzle velocity, about 0.4–2.5% of range | Nothing (spotting cannot remove it) |
   | 5 | Shell dispersion within the salvo | Pattern of about 1–3.5% of range for a full salvo | — |

   These five set the miss. The hull then sets the hit, through its danger space, depth and width.

2. **The decisive physics is the R² law of optical rangefinding.** One reading's error is:

   ```
   σ = δ·R² / (B·M)      with δ ≈ 12″ at the eye  [S, OP 1171]
   ```

   | Instrument | Error at 20 kyd |
   |---|---|
   | 9 ft coincidence rangefinder | ~280 yd |
   | 15 ft | ~170 yd |
   | 26.5 ft stereo (Mk 48) | ~120 yd |
   | 15 m (Yamato) | ~60 yd |
   | Mk 8 radar | ~35 yd, at any range in its envelope |

   Radar beats a 15 ft optical rangefinder beyond about 7–10 kyd, and the Type 284 beyond about 17 kyd (§3.6).

3. **The computer's job is rate, not range.** At 20 kyd a 16″ shell flies about 30 s, so 1 kn of rate error costs about 17 yd at once and keeps drifting about 34 yd a minute until corrected [S].
   - **Settled rate error:** humans with a plot hold about ±5–8 kn of target motion; AFCT/Mk 8 class about 1.5 kn; radar auto-track about 1 kn; digital about 0.7 kn (model, §5.4).
   - **After a 45° target turn:**
     - Plot-based systems need about 2–4 minutes to recover.
     - Mk 8 with optics needs about 90 s.
     - Mk 8 with Mk 13 radar needs about 60 s.
     - Auto-track systems barely notice.
   - **A target on a steady circle is a different problem from a target that turned once** (§5.6). Every WWII rangekeeper assumed a straight course, so a circling target leaves a standing error that no amount of tracking removes: about 700 yd for the AFCT and 300 yd for the Mk 8 with Mk 13 radar, over a 25 s flight at a 1°/s circle. Curved-course prediction arrived with 1950s analog predictors (about 100 yd) and digital filters (about 20 yd). Whether the AFCT's enemy-turn handling really existed is unconfirmed.
4. **Salvo-to-salvo error is the floor, and spotting cannot remove it.** The USN measured it directly: between 1920 and 1945 dispersion fell 66% but MPI error fell only 23% [S, INRO]. In the model this term is what separates WWII optical (≈1% of range per salvo in practice) from digital (≈0.4%). It is the main reason radar alone did not double hit rates in daylight.
5. **Calibration.** With combat stress applied as a single condition multiplier (1.5), the model reproduces the record within about ×2. The USN 1944 practice study (32.7 / 10.5 / 2.7% at 10 / 20 / 30 kyd) comes out at 34 / 11 / 4%. Komandorski gives 1.0% against about 0.5%; Jutland 2.7% British and 4.0% German against 1.4% and 4.0% (§11).
6. **For the game, two tiers.**
   - **Full tier:** run `engage()` logic per firing ship (sensors → tracker → aim → spot), about 0.02 s of CPU per 15-minute duel.
   - **Quick tier:** take the "MPI error by salvo number" table (§12.2) and the analytic `quick_hit_probability()` (§8.3). That gives expected hits per shell from range, target, aspect and fire-control level in a few lines.

---

## 1. The chain, and where each error enters

```
 TARGET VISIBLE?  -> horizon (masthead heights), met. visibility, night, smoke          §2
      |
 RANGE & BEARING  -> eye / stadimeter / optical RF (R^2 error) / radar / laser          §3, §4
      |              + per-instrument bias, + reading rate, + blind arcs
 COMPUTER         -> plot / clock / rangekeeper / auto-track / digital                  §5
      |              estimates target course & speed; predicts position at t + TOF
      |              adds ballistic corrections (residual bias stays)
 GUN ORDERS       -> elevation from range table, train = bearing + deflection           §6
      |              laying error (director vs local, stabilised or not), mount rates
 SALVO            -> salvo-to-salvo error + shell dispersion (pattern)                  §7
      |
 HIT TEST         -> danger space (height x cot fall) + depth, presented width           §8
      |              exact box hull: side or deck hit
 SPOTTING         -> optical over/short ladder, radar measured miss, aircraft           §9
      `--> correction applied to later salvos (after TOF + spotting delay)
```

The reference code runs this loop at 1 s steps for both ships. Every block has the parameters listed in its section.

---

## 2. Seeing the target

- **Horizon.** Optical mutual visibility is d = 3.86(√h_obs + √h_tgt) km (h in m), using the standard 7/6 refraction factor [S: goodoldboat; mathscinotes navy tables agree within 0.3%].
  - From a 30 m top, a battleship's 40 m masthead is visible to about 46 km. A destroyer's 22 m mast is visible to about 39 km. A PT boat's 6 m is visible to about 31 km.
  - Radar uses the 4/3-earth horizon, 4.12(√h₁ + √h₂) km.
- **Historical first sightings:**
  - Jutland: about 24 km [S].
  - Denmark Strait: about 35 km [S].
  - Savo, at night: about 5 statute miles was the limit [S].
- **Night.** Optics work only on an illuminated target (starshell, searchlight, fires) or a silhouetted one. The model uses `night_vis` of about 2–9 km by scenario. Night rangefinder error multiplier is about ×2–3 [INFERRED].
- **Smoke, squalls and haze.** Modelled as a per-minute chance that optics see nothing (`p_obscured`) plus an error multiplier `k_optic`:

  | Condition | k_optic |
  |---|---|
  | Clear | 1 |
  | Haze, spray or vibration | 1.5–2 |
  | Smoke, dusk, or grey-on-grey | 2–3 |

  - Sturdee at the Falklands manoeuvred to windward to clear his own smoke.
  - At Jutland Beatty's funnel smoke drifted down the range [S].

> **MODEL NOTE.** Radar ignores smoke and night but has its own envelope: maximum range on a battleship, scaled by (relative RCS)^¼, plus a minimum range and the radar horizon.

---

## 3. Measuring range

### 3.1 MK1 Eyeball

- **Naked eye.** Early USN trials on a launch at 2–4 kyd found estimates spread by up to 500 yd, roughly ±12–25%. Practice tightened this a lot [S: USNI 1904].
  - Haze biases estimates long; clear air biases them short.
  - **Model:** σ ≈ 20% of range untrained, 10–15% practised.
  - The `eye` sensor also carries a 10% personal bias [INFERRED].
- **Reticle binoculars on a known height.** A modern field study (253 binoculars, 1,576 targets, 10.5 m eye height) gives a useful benchmark [S: JCRM 816]:
  - About 12% multiplicative standard error.
  - Unbiased out to about ⅓ of the horizon distance, then reading 6–16% short.
  - Useless beyond about ⅔ of the horizon distance.
- **Stadimeter (Fiske, 1895).** It reads the angle subtended by a known mast height.
  - A 1905 test gave about 0.5% average error at 6,580 yd [S].
  - The real limit is knowing the mast height: a 10% height error means a 10% range error [S].
  - **Model:** 1% random plus a 5% bias, usable to about 6–8 kyd [INFERRED].
  - It was used for fire control at Manila Bay in 1898, then mainly for station-keeping.

### 3.2 Optical rangefinders: the formula

```
σ_R (one reading) = δ · R² / (206265 · B · M)        R, B in the same unit; δ in arc-seconds at the eye
```

- **The standard δ.** The USN "unit of error" is 12″/M of object-space angle. This exact formula reproduces the OP 1171 Mk 58 table: 17.7 / 110.9 / 443.5 yd at 2 / 5 / 10 kyd for B = 1.5 m, M = ×8 [S: OP 1171; Admiralty Trilogy FI2024].
- **Operator quality, δ_op as a multiplier on 12″ [INFERRED from the sources below]:**

  | Operator | δ |
  |---|---|
  | Elite | 10″ (×0.85) |
  | Trained | 12″ (×1) |
  | Average | 18″ (×1.5) |
  | Green or exhausted | 25″ (×2) |

  - The German standard demanded 10″; fewer than 5% of men qualified [S: FI2024].
  - A US Army training study found probable error still falling after 4,000 rangings. Only 22.5% of operators met the strict standard [S: HumRRO, DTIC ADA021002].
- **Long bases deliver less than geometry predicts.** The NDRC 1941 trials found big and small instruments differed less than optics predicts [S]. The model applies B_eff = 5 m·(B/5 m)^0.75 above 5 m [INFERRED].
- **Systematic error per instrument** comes from calibration, temperature, refraction and the operator's habitual offset. The model draws it once per engagement, at 1.5 units of error 1σ [INFERRED].
  - It is doubled for low-contrast targets and multiplied by k_optic.
  - This is why British battlecruisers at Jutland all opened well over: a common bias that averaging cannot remove [S].
  - USNI 1930 notes calibration took at least 10 averaged readings against a known range [S].

### 3.3 Instruments

Random 1σ per reading under clear conditions with a trained operator (model output, table 2). The B and M values are sourced unless marked.

| Instrument | Era | B, M | 5 kyd | 10 kyd | 15 kyd | 20 kyd | 25 kyd | 30 kyd |
|---|---|---|---|---|---|---|---|---|
| Eye estimate | all | — | 1,000 | 2,000 | 3,000 | — | — | — |
| Stadimeter | 1895+ | — | 50 | — | — | — | — | — |
| B&S FA3 4.5 ft coincidence | 1900s | 1.37 m, ×24 | 40 | 162 | 364 | 647 | — | — |
| B&S FQ2 9 ft | 1906 | 2.74 m, ×28 | 17 | 69 | 156 | 277 | 433 | 624 |
| Zeiss 3 m stereo | WWI | 3 m, ×23 | 19 | 77 | 173 | 308 | 482 | 694 |
| B&S FT24 15 ft | 1913+ | 4.57 m, ×28 | 10 | 42 | 94 | 166 | 260 | 374 |
| USN Mk 48 26.5 ft stereo (Mk 38 director) | WWII | 8.1 m, ×25 | 7 | 30 | 67 | 119 | 185 | 267 |
| Bismarck 10.5 m stereo | 1940 | 10.5 m, ×23 [M INFERRED] | 7 | 27 | 60 | 106 | 166 | 239 |
| Iowa 46 ft turret (Mk 52) | WWII | 14 m, ×25 | 5 | 20 | 44 | 79 | 123 | 177 |
| Yamato 15 m (triplex coincidence + stereo) | 1941 | 15 m, ×30 [M INFERRED] | 4 | 16 | 35 | 62 | 97 | 140 |

Cross-checks:
- **Zeiss 3 m:** Lienau gives ±65 m at 10 km and 165 m at 16 km; the formula gives the same with δ = 10″ [S: navweaps tech-078].
- **FQ2 9 ft:** rated at 1% at 7 kyd [S: USNI 2024].
- **Iowa:** the 1980s "100 yd and 1 mil" figure at Ponape (§5) is an auto-tracked radar solution, not optical.

### 3.4 Coincidence vs stereoscopic

| Factor | Coincidence (B&S; RN, IJN partly) | Stereo (Zeiss; German, USN interwar) |
|---|---|---|
| Precision, good conditions | NDRC 1941: no important difference [S] | same |
| Haze, fuzzy targets, splashes, aircraft | Needs a sharp vertical edge (mast, funnel, bow) | Better: ranges blobs and splashes [S]. Model: stereo has 0.8 of the haze penalty |
| Hull-down, bow-on, low contrast | Struggles. Model ×1.5 | Unaffected by profile [S] |
| Who can do it | Most men | <5% meet the German standard [S] |
| Fatigue | Less eye strain | Germans reported accuracy falling off as Jutland went on; a US study found stereo robust. [UNCERTAIN] |

### 3.5 Degraders and ranging rate

- **Spray.** Prince of Wales at Denmark Strait lost her 42 ft and 30 ft turret rangefinders to spray and fell back on the 15 ft DCT [S]. Iowa's turret I rangefinder was removed because spray blinded it [S]. Low-mounted instruments in a seaway take k_optic of about 2 or are blinded.
- **Vibration and mast shake.** At the Falklands they made a stereo rangefinder useless at high speed [S].
- **Rate of ranging.** Pollen's gyro-stabilised mounting raised range-taking about fivefold [S]. Model readings per minute [INFERRED]:

  | Instrument | Readings / min |
  |---|---|
  | Unstabilised WWI | 2 |
  | Stabilised (Argo) | 8 |
  | WWII director | 4–6 |
  | Radar | 6–20 |
  | Auto-track | 60 |

- **Averaging N instruments.** Random error falls as 1/√N but bias does not: σ = √(σ²/N + σ_bias²) [INFERRED]. Ships carried many rangefinders:

  | Ship | Rangefinders |
  |---|---|
  | Lion | 4 × 9 ft |
  | Queen Elizabeth class | 5 × 15 ft + 1 × 9 ft |
  | Bismarck | ~15 |
  | Iowa | 9 |

  The Dreyer plot marked each instrument's cuts with its own symbol so the plotter could see per-instrument bias [S: 1918 handbook].

### 3.6 Radar and laser (range)

| Set | Year | Range error 1σ | Bearing | Max on BB | Min | Splash spotting |
|---|---|---|---|---|---|---|
| Japanese Type 22 | 1942–43 | ~200 m | ~3° | 34 km | — | none recorded |
| RN Type 284 | 1940 | ±120 yd | none until beam switching; optical bearing | ~20–26 kyd | — | range only |
| Type 284M/P | 1941–42 | ~60 m; resolution 150 m | lobe switching, blind fire | ~30 kyd (Duke of York) | — | range |
| German Seetakt / FuMO 23 | 1939–40 | 50–70 m | 5–6° resolution early; optical bearing; no blind fire | ~25 km | — | — |
| USN Mk 3 | 1941 | ±40 yd | ±2–4 mil (lobing) | 28 kyd BB, 16 kyd DD | 1,000 yd | 16″ splashes to ~20 kyd, range only |
| USN Mk 8 | 1942–43 | ±(15 yd + 0.1% R) | ~2 mil | 40 kyd BB/CA | 250 yd | Mod 3 to ≥35 kyd; deflection poor |
| USN Mk 13 | 1945 | ±(15 yd + 0.1% R) | ±2 mil, 0.9° beam | 40 kyd+ | 250 yd | individual 16″ splashes beyond 42 kyd, range **and** deflection |
| Mk 56 / Mk 35 | 1945–50s | 9 m | as accurate as optics; auto-track | 27 km | — | solution ~2 s after lock |
| HSA WM-20/25, Type 347G | 1970s | ~10 m | 0.8–1 mrad | 30+ km | — | B-scan; spots by hand |
| Laser rangefinder | 1965+ | ±5 m, any range in visibility | — | weather-limited | — | — |

Sources: navweaps WNUS_Radar_WWII and WNBR/WNGER pages; COMINCH P-08-03; CB 3213; radartutorial; Wikipedia Mk 56, Type 347; see [`radar.md`](fire-control-research/radar.md) for the per-set source key.

**Where radar becomes the better rangefinder** (model, random error per reading):

| Radar | vs 15 ft coincidence | vs 26.5 ft stereo | vs 10.5 m stereo |
|---|---|---|---|
| Mk 8 / Mk 13 | 7.4 kyd | 9.1 kyd | 9.7 kyd |
| Mk 3 | 10.0 kyd | 11.7 kyd | 12.4 kyd |
| Seetakt | 12.6 kyd | 15.0 kyd | 15.7 kyd |
| Type 284 | 17.1 kyd | 20.2 kyd | 21.3 kyd |

Radar's real advantage is larger than this table shows. It does not degrade in haze, smoke or night, and it has no operator stereo-acuity lottery.

Radar has its own weaknesses [S]:
- **Fragility.**
  - Bismarck's forward FuMO 23 was knocked out by her own gun blast.
  - Duke of York's 284 aerial was shot down and re-erected by hand.
  - KGV's 284 failed at 09:13 on 27 May.
  - Wichita's Mk 8 was oversensitive to the shock of her own guns.
- **Misidentified splashes.** KGV ranged on Rodney's splashes.
- **Receiver saturation.** Washington's Mk 3 saw no splashes at Guadalcanal.

---

## 4. Measuring bearing, and relative-bearing restrictions

- **Optical director bearing.** About 0.5–1 mil with ×12 trainer and pointer optics [INFERRED from USN Mk 38 specs]. The model uses 0.7–1.0 mil.
- **Dreyer era.** Bearings were plotted in ¼° steps, later 4′, and converted to true bearing by gyro. The bearing plot was the Dreyer table's weak point [S].
- **Gyrocompass.** The settled error, about 0.1–0.5°, matters only for true-course plotting [S: Sperry 1911; INFERRED].
- **Radar bearing.**
  - Lobed decimetric sets: ±2–4 mil.
  - Centimetric: 2 mil.
  - Post-war: about 0.8–1 mrad.
  - Metric and early German sets gave range only, so bearing had to come from optics. In the model `bearing_from_optics` sets give **no solution at night** unless the target is lit; table 6 shows this for level 5r at night.
- **Blind arcs.** Each sensor has relative-bearing sectors where it is masked: the forward director cannot see aft, and turret rangefinders see only their turret's arc. Bismarck's forward 7 m had a restricted field, while the foretop 10.5 m saw 360° [S].
- **Guns bearing by relative bearing** (model table 9; arcs ±150° about bow or stern):

  | Layout | 0° | 15° | 30° | 45°–150° | 165° | 180° |
  |---|---|---|---|---|---|---|
  | BB 3×3 (A, B forward; X aft) | 6 | 6 | 9 | 9 | 3 | 3 |
  | BB 4×2 | 4 | 4 | 8 | 8 | 4 | 4 |
  | Nelson (all forward) | 9 | 9 | 9 | 9 | 0 | 0 |
  | DD 5×1 (2 forward, 3 aft) | 2 | 2 | 5 | 5 | 3 | 3 |

- **Mount train rates** [S]:

  | Mount | Train rate (deg/s) |
  |---|---|
  | 46 cm | 2 |
  | RN 14–16″ | 2 |
  | USN 16″ | 4 |
  | 38 cm | 5 |
  | 8″ | 6.7 |
  | 6″ | 10 |
  | 5″/38 | 25 |
  | OTO 76 | 60 |

  The required bearing rate is v_cross/R: 30 kn crossing gives 0.97°/s at 1 kyd, 0.10°/s at 10 kyd and 0.05°/s at 20 kyd. Heavy turrets therefore keep up with ships except at very short range **or during own-ship turns**: a battleship turns at about 1°/s and a destroyer at about 3°/s, so a destroyer's hard turn exceeds a 2°/s turret.

> **MODEL NOTE.** Gate firing per turret by (a) arc, (b) |required train rate| ≤ mount rate, and (c) the director (or the turret's own sight in local control) being able to see the target in its arc.

---

## 5. Computing the solution

### 5.1 The problem

Gun order = present range and bearing, advanced by relative motion × time of flight, plus ballistic corrections, plus spots.

**16″/50 time of flight** [S navweaps; model in brackets]:

| Range | TOF |
|---|---|
| 10 kyd | 13.2 s [13.3] |
| 15 kyd | 21.0 s [21.2] |
| 20 kyd | 29.6 s [30.0] |
| 25 kyd | 39.3 s [39.9] |
| 30 kyd | 50.3 s [51.1] |

Model fall angles land within about 1° of the sourced values.

**Correction sizes around 20 kyd for a 15–16″ gun** [S: navalgazing Ballistics; FAS SWOS; coast-artillery 16″ table]:

| Correction | Size |
|---|---|
| 10 kn range wind | ≈200 yd |
| 10 kn cross wind | ≈126 yd |
| 1% air density | ≈135 yd at 25 kyd (5″/54) |
| 1 °F powder temperature | ≈2 ft/s ≈ 55 yd at 40 kyd |
| Worn 16″ gun | −75 ft/s |
| Shell drift | 10–15 mil |
| Earth rotation | ~1 mil + 47 yd |
| Director-to-turret parallax | uncorrected, about a 400 ft spread on Iowa |

What is left after the computer applies what it knows is the **residual bias** (`resid`):

| Era | Residual bias (% of range) |
|---|---|
| Eyeball, range tables only | 2.5 |
| WWI | 1.2–1.5 |
| Interwar | 0.6 |
| WWII USN | 0.4 |
| Digital (meteorological data still limits it) | 0.3 |

[INFERRED, consistent with the 1932–33 LRBP mean first-salvo aim error of 313 yd at 27–32 kyd, S INRO]

### 5.2 What a human can do

- **Estimating course and speed by eye.** Speed errors average 1–2 kn, about double in absolute terms, with occasional gross errors (86 kn estimated against 131 kn actual for a drone). Course errors run about 10–30° [S].
  - At 25 kn, a 15° course error alone is about 6.5 kn of crossing-rate error.
- **Dumaresq.** A slide-rule board that turns estimated courses and speeds into range rate and speed across. British tests found this "estimate and check" method beat rates taken from plotted ranges; by 1919 the RN judged the range plot alone unreliable for rate [S].
- **Hand plotting.** It needs 2.3–4 times the tracking time of an automatic computer for the same accuracy (1961 DTIC submarine study, used here as an analogue) [S].
- **Crews.**
  - A Dreyer transmitting station held 7–8 table operators and up to about 30 people.
  - The Japanese Type 92 needed 7 men plus 8 on the companion plot.
  - The US Mk 8 rangekeeper had one operator [S].
- **Spotting rules** (§9). One gunnery officer handles **one target**.

### 5.3 The machines

| Level in code | System | Adds | Limits [S] |
|---|---|---|---|
| 0 | MK1 Eyeball | — | Range by eye, rate by eye, each gun lays itself |
| 1 | Early rangefinder + stadimeter (Tsushima era) | A range | No rate machinery |
| 2 | Dumaresq + Vickers range clock (1906) | Rate from estimates; clock runs range on | Constant rate only; roller slips on rate changes; 2–14 kyd scales |
| 3 | Dreyer table Mk I–V (1911–22) + director | Range and bearing plots + clock; helm-free from Mk III | Range clock ±1,200 yd/min; bearing gear ±15°/min; 25 yd steps; weak bearing plot |
| 3b | Pollen Argo clock (1912–13) | True-course plot and gyro-stabilised rangefinder; follows a *changing* range rate | Only ~6 at Jutland |
| 4 | AFCT / interwar central table; German Rechenstelle | Clock + MPI plot, helm-free | AFCC Mk III on Haida limited to 16 kyd. Straight-course target model |
| 4t | AFCT with an enemy-turn setting | As 4, plus a hand-set target rate of turn | A forum report citing Friedman says the AFCT was the only big-gun computer able to handle a target in a constant turn. Not confirmed in any source I could open [UNCERTAIN]. Modelled as a turn-following tracker with a slowly adapting turn rate |
| 4j | Japanese Type 92/98 Shagekiban | Rate from plot | No gyro horizon reference; no remote power control on Yamato; exercise patterns over 1,000 m |
| 5 | Ford Mk 8 rangekeeper + Stable Vertical Mk 41 (gyro firing) | Continuous, helm-free; tilt, wind, drift and wear corrections; generated range fed to the radar | One operator. North Carolina 1945: held a solution through 450° and 100° turns, with the aim point shifting several hundred yards |
| 5r | + Mk 3 radar range | Radar range, optical bearing | — |
| 6 | + Mk 8 / Mk 13 radar spotting | Blind fire; spots by radar | Ponape 1944: solution regenerated blind for 15 min was off 100 yd and 1 mil |
| 7 | Mk 56 / Mk 68 auto-track (1950s) | Radar locks on and tracks; automatic rate; curved-course (acceleration) prediction | Mk 1A auto rate only for surface targets above 15 kn; time constant 2 s × R/3,000 yd. The Mk 1A had a "target just turned" button that sped convergence at some cost in steadiness [S]. Sperry's target course predictor (filed 1954) projects along a curve from straight line to parabola, from measured acceleration [S] |
| 8 | Digital Mk 86 / WM-25 + laser (1970s) | Multiple channels, meteorological inputs, muzzle-velocity measurement; modelled with a turn-following filter | Spots still entered by hand from the B-scan. The turning-target filter is [INFERRED]: no source opened gives the Mk 86 or WM-25 target model |
| L | Local control (WWII turret) | Turret rangefinder, turret computer | Low height of eye; spray |

### 5.4 How fast each settles (model table 5)

**Setup:** a steady battleship crossing at 25 kn at 15 kyd, own ship at 20 kn. The target turns 45° at 300 s. Values are the RMS error in the target's motion as the computer holds it, in knots.

| Level | 30 s | 60 s | 120 s | 240 s | turn +30 s | +60 s | +120 s | +240 s |
|---|---|---|---|---|---|---|---|---|
| 0 Eyeball | 18 | 16 | 17 | 23 | 13 | 12 | 11 | 12 |
| 1 Early RF | 12 | 12 | 12 | 10 | 9 | 9 | 6 | 5 |
| 2 Dumaresq + clock | 11 | 11 | 10 | 7 | 13 | 10 | 6 | 6 |
| 3 Dreyer | 8.0 | 6.7 | 8.8 | 5.8 | 14 | 10 | 5.3 | 5.6 |
| 3b Argo | 8.0 | 4.3 | 1.9 | 1.8 | 17 | 12 | 3.5 | 2.1 |
| 4 AFCT | 4.5 | 2.0 | 1.3 | 1.6 | 16 | 9.2 | 1.8 | 1.4 |
| 4t AFCT + turn setting | 8.0 | 8.0 | 2.8 | 2.1 | 15 | 4.3 | 7.3 | 2.5 |
| 4j Type 92/98 | 5.8 | 5.0 | 6.3 | 4.0 | 14 | 6.9 | 5.5 | 4.0 |
| 5 Mk 8 optical | 4.3 | 1.7 | 1.7 | 1.5 | 13 | 5.0 | 2.3 | 1.7 |
| 5r Mk 8 + Mk 3 | 3.0 | 1.6 | 1.6 | 1.5 | 11 | 2.3 | 1.6 | 1.6 |
| 6 Mk 8 + Mk 13 | 1.9 | 1.2 | 1.3 | 1.6 | 6.2 | 1.5 | 1.4 | 1.4 |
| 7 Auto-track analog | 1.7 | 1.6 | 1.6 | 1.9 | 3.8 | 1.7 | 1.7 | 1.7 |
| 8 Digital + laser | 1.1 | 0.7 | 0.7 | 0.7 | 3.4 | 0.8 | 0.7 | 0.6 |
| L Local control | 6.7 | 5.0 | 5.6 | 4.8 | 14 | 6.7 | 3.6 | 4.2 |

How to read it:
- **Human plot systems** (levels 1–3, 4j) never get much below 5 kn. Their measurements are too noisy for a plot to beat the eye estimate, and their constant-rate model is wrong for a crossing target.
- **Pollen's true-course clock** settles three times better than Dreyer's from comparable rangefinders. This is the Pollen-Dreyer argument, reproduced from first principles.
- **Radar ranging** cuts recovery from a turn from about 2 minutes to about 1. **Auto-track** removes the problem.
- **Turn-following trackers cost a little on a straight target.** Extra states mean extra noise: level 7 settles at about 1.7 kn against 1.0 for a straight-line tracker on the same data, and the AFCT turn setting (4t) settles more slowly than the plain AFCT. That is the price of handling the circle in §5.6.
- **Turn recovery times** match the sourced parameter ranges [S/INFERRED, computing appendix §8]:

  | System | Recovery |
  |---|---|
  | Dreyer / Japanese | 2–4 min |
  | AFCT / Mk 8 optical | 1–2 min |
  | Mk 8 + radar | 30–60 s |
  | Auto | 15–30 s |
  | Digital | 5–15 s |

**How the trackers work.** The historical machines were not Kalman filters. The code uses the cheapest model that captures what each one could and could not do:
- **`polar` tracker** (Dumaresq / clock / Dreyer / Japanese plot): constant range rate and bearing rate. It drifts for crossing targets.
- **`cart` tracker** (Argo, AFCT, Ford, Mk 8): tracks the target's true course and speed on a straight line, so its geometry is exact until the target turns.
- **`ca` tracker** (level 7, 1950s analog predictors): adds a decaying target acceleration, so it projects along a curve, like Sperry's 1954 predictor.
- **`ct` tracker** (level 8, digital; level 4t with a slow hand-set turn rate): coordinated-turn model whose state includes the target's rate of turn. It follows a steady circle exactly.
- **Manoeuvre detection** (levels 7, 8): two large surprises in a row open the filter up, standing in for the Mk 1A's "target just turned" button.
- **Tuning knobs:** the allowed target acceleration `q`, which is small for men who assume a steady target; the eye-estimate prior; latency; and measurement noise.
- **Own ship.** On a "helm-free" machine, own-ship course and speed changes are an exact input. On a plain clock they must be re-learned.

### 5.5 Manoeuvre effects [S]

- **Own turns.** Turns of 20–30° at 25 kyd (1927–28) put the aim point 400 yd off, with no hits.
  - California in 1941, after a 150° reversal at 23 kyd, had a 212 yd deflection error and one salvo 4,100 yd off.
  - **Model:** `own_turn_kick` is the extra error σ while own ship turns:

    | Era | own_turn_kick |
    |---|---|
    | Dreyer | 300 m |
    | Mk 8 | 150 m |
    | Digital | 20 m |

- **Chasing salvos.** At Komandorski, 200-plus Japanese salvos landed within 50 yd of Salt Lake City, but she was hit only 4–5 times in 3.5 h. Her own fire scored about 0.5%. The model's `chase` policy steers the target toward each salvo that lands within 600 m, limited to ±amp from its base course. Evasion is studied in depth in [`evasion-research.md`](evasion-research.md).

### 5.6 Target motion: straight, zigzag, circle

What each machine assumed about the target [S unless marked]:
- **Dumaresq, Vickers clock, Dreyer.** Constant rates. Pollen's Argo clock followed a *changing* range rate, but nothing I found says it modelled a curving target course (Naval Gazing, Rangekeeping Part 2).
- **USN rangekeepers (Ford Mk 1 to Mk 8, Mk 1A).** Straight-line target. NavPers Ch. 19-F says the solution assumes relative motion is constant over the short interval involved, and that a target changing course or speed gives inaccurate gun orders until rate control catches up.
- **Mk 1A.** It had a "target just turned" button to speed convergence at some cost in steadiness (Naval Gazing open thread 138).
- **AFCT.** A forum report citing Friedman says it was the only big-gun computer able to handle a target in a constant turn. The Wikipedia article and the other sources I opened say nothing about turning targets [UNCERTAIN]. Vanguard's AFCT Mk X was tachymetric.
- **1950s analog predictors.** Sperry's target course predictor (filed 1954, granted 1961) predicts along a path from a straight line to a parabola, using smoothed measured acceleration, for surface and air targets.
- **Digital (1970s on).** Acceleration and turn models in a Kalman-type filter are standard in the literature from the late 1970s [INFERRED: the Mk 86 and WM-25 target models were not found].

In short: curved-course prediction is real, but on present evidence it belongs to the post-war machines, with the AFCT as an unconfirmed WWII exception.

**What a circling target does to each machine** (model table 12). Steady battleship target, 25 kn at 15 kyd; at 300 s she puts the helm over and circles at 1°/s (about 800 yd radius). RMS predicted-position error over a 25 s time of flight, yd:

| Level | Straight, 240 s | Circle +30 s | +60 s | +120 s | +240 s |
|---|---|---|---|---|---|
| 3 Dreyer | 215 | 292 | 656 | 1,103 | 822 |
| 3b Argo | 182 | 425 | 722 | 924 | 796 |
| 4 AFCT | 113 | 394 | 650 | 716 | 713 |
| 4t AFCT + turn setting [UNCERTAIN] | 122 | 360 | 521 | 268 | 117 |
| 5 Mk 8 optical | 89 | 338 | 512 | 380 | 488 |
| 6 Mk 8 + Mk 13 radar | 43 | 264 | 297 | 234 | 281 |
| 7 Analog auto-track + curve predictor | 46 | 95 | 103 | 107 | 93 |
| 8 Digital, turn-following | 18 | 30 | 19 | 23 | 18 |

- **Straight-line machines never converge on a circle.** They chase a moving velocity and always lag. Their standing error is set by how fast they chase: 700 yd for the AFCT, about 300 yd for the radar-fed Mk 8.
- **A turn model turns a standing error into a transient.** With a hand-set turn rate (4t), the error falls to about 120 yd after 4 minutes. A digital turn filter stays at about 20 yd throughout.
- **The size of the problem grows with time of flight squared:** offset ≈ v·T²·ω/2. At 25 kn and 1°/s that is about 70 yd for a 25 s flight but about 300 yd for a 50 s flight. Turning matters most at long range.

**Effect on hits** (model table 11). 8 × 15″/42 at 16 kyd, combat conditions, circles start at 240 s. Each cell is hit % / RMS range MPI error, yd:

| Level | Steady | Zigzag ±20° / 4 min | Circle 0.5°/s | Circle 1.0°/s |
|---|---|---|---|---|
| 3 Dreyer | 7.8 / 440 | 6.8 / 484 | 2.6 / 867 | 1.3 / 1,027 |
| 3b Argo | 7.2 / 409 | 7.4 / 524 | 3.2 / 891 | 2.4 / 1,013 |
| 4 AFCT | 9.9 / 358 | 9.1 / 374 | 4.0 / 643 | 2.9 / 874 |
| 4t AFCT + turn setting | 9.5 / 361 | 9.3 / 355 | 7.1 / 404 | 7.4 / 386 |
| 4j Type 92/98 | 7.8 / 376 | 8.1 / 421 | 4.0 / 606 | 2.3 / 862 |
| 5 Mk 8 optical | 10.7 / 281 | 12.7 / 312 | 5.3 / 461 | 3.6 / 636 |
| 6 Mk 8 + Mk 13 | 10.6 / 300 | 10.6 / 309 | 6.5 / 367 | 3.5 / 390 |
| 7 Analog auto-track | 14.5 / 200 | 15.2 / 202 | 10.0 / 247 | 7.9 / 223 |
| 8 Digital | 24.2 / 125 | 23.2 / 126 | 21.5 / 136 | 24.4 / 128 |

What this means for the game:
- **A steady turn is the best evasion against WWII fire control**: it cuts hits by 2–4× for every straight-line machine. A zigzag hurts much less, because the computer re-converges between legs. This fits how WWII ships actually evaded: continuous helm while under fire, not just occasional course changes.
- **Against post-war turn-following computers, steady turning buys almost nothing.** Evasion against them has to be unpredictable: random changes of turn rate, speed changes and reversals.
- **If the AFCT turn capability is confirmed**, RN ships get a real edge against circling targets (7% against 3% for the Mk 8). If not, use level 4. The parameter is one flag on the level.
- **Not modelled:** speed changes (the Mk 1A and Mk 8 accepted them as rate changes; same lag as course changes), own ship circling (helm-free machines handle it, at the `own_turn_kick` cost), and a target whose aspect changes the hitting space during the circle (handled by the exact hit test, but not separately reported).

---

## 6. Gun orders and laying

- **Director firing** was on most RN capital ships by mid-1916 [S]. One director layer fires all guns, so the laying error is common to the salvo.
- **Local control.** Each gunlayer lays and fires his own gun, so laying error becomes extra dispersion. The model adds it to the pattern in quadrature.
  - This is why levels 0–2 and L show high straddle percentages but lower hit rates.
  - Iowa's turrets could fight alone with 46 ft rangefinders and Mk 3 computers [S].
- **Stabilisation.**
  - Scott's continuous aim worked only up to about 9.2″ and in elevation only.
  - Cross-level was not stabilised until the 1930s.
  - The USN Stable Vertical Mk 41 fires at a chosen roll angle (gyro firing). It needs 30–60 min to spin up and about 5 min to settle [S].
  - **Model:** if unstabilised, laying error × (1 + 0.25·(sea state − 3)).
- **Laying error, common per salvo, 1σ** [INFERRED]:

  | Era | Laying error (mil) |
  |---|---|
  | Eyeball | 3 |
  | Early | 2–2.5 |
  | Dreyer + director | 1.0 |
  | Interwar | 0.8 |
  | WWII USN | 0.5 |
  | Post-war | 0.3–0.4 |

  An elevation error turns into range through the range table. For the 16″/50 at 20 kyd, 1 mil ≈ 71 yd; at 10 kyd, 97 yd (table 1).
- **Remote power control (RPC).** It ties the turrets to the director. A 5° training error from a synchro fault once caused a 1,664 yd miss [S].
- **Salvo interval** [S]:

  | Battery | Interval |
  |---|---|
  | USN main battery | 30–84 s (minimum loading interval 24 s in 1938) |
  | Rodney | ~1.5 salvos/min |
  | Secondaries | 7–10 s |

  German and RN practice used half-salvos, alternating, to give the spotter more falls.
- **Gun output.** Prince of Wales lost 26% of her salvo output to gun defects; Rodney achieved 77% of possible salvos [S]. The model's `gun_output` is the fraction of guns that actually fire.

---

## 7. Dispersion

- **USN statistics** [S: NavPers Ch. 18]:
  - Mean dispersion D: ±0.846D holds 50% of shots and ±4D holds 99.9%; anything outside is a wild shot.
  - The pattern (100% spread) is a multiple of D that depends on shell count:

    | Shells | 3 | 4 | 6 | 8 | 9 | 10 | 12 |
    |---|---|---|---|---|---|---|---|
    | Pattern ÷ D | 2.43 | 2.74 | 3.47 | 3.85 | 4.00 | 4.13 | 4.34 |

    Source: USNI 1917.
  - For normal errors, D = 0.798σ.
  - Deflection dispersion is about 0.3–0.5 of range dispersion; a typical deflection pattern is about 4 mil.
- **Full-salvo patterns as % of range** (model `pattern_pct`):

  | Gun | Pattern | Source / note |
  |---|---|---|
  | 12″ WWI | 3.3–3.5% | [S] |
  | 13.5″ | 2.5% | [S] |
  | 15″/42 | 1.7% | [S]; called the most accurate battleship gun |
  | 16″/50 and 16″/45 | 1.9% | [S 1944 USN] |
  | 14″/45 RN | 2.0% | [INFERRED] |
  | 38 cm with delay coils | 1.5% | [INFERRED from German 0.5–0.8% single-gun PE × 1.5–1.7] |
  | 46 cm | 1.2% | [S: 400–500 m at maximum range] |
  | 8″ | 2.0% | [S: IJN 1.3–2.5%] |
  | 5″/38 | 1.7% | [S: 1.4–1.7%] |
  | Rodney 16″/45 | 2.2% | [S: mixed rifling] |

- **What sets pattern size** [S]:
  - Shell-to-shell variation in charge, temperature and seating.
  - Gun-to-gun differences from wear and calibration (New Jersey's propellant spread off Lebanon in 1984).
  - Salvo interference, cured by delay coils:
    - USN 0.06 s roughly halved dispersion around 1934.
    - IJN Type 98 0.03 s cut it 10–15%.
    - German coils took battery dispersion from 2–3× single-gun to 1.5–1.7×.
  - Mount and hull flex: Myoko stiffening cut 2.5% to 1.5%.
  - Iowa 1987: 220 yd at 34 kyd (0.64%), single gun per turret.
- **Optimum dispersion is not zero.** When fire-control error is large, a wider pattern raises the chance of at least one hit per salvo [S: USNI 1912]. Hits peak at dispersion ≈ 0.8 × fire-control error. Chance of ≥1 hit per 9-shell salvo [INFERRED]:

  | σ_FC | Peak chance | At σ_D |
  |---|---|---|
  | 100 yd | 0.76 | ~100 yd |
  | 200 yd | 0.54 | ~150 yd |
  | 400 yd | 0.34 | ~250 yd |

---

## 8. The hit

### 8.1 Hitting space

- **Range window** = danger space + presented depth, where danger space = h·cot(fall angle) [S: NavPers; mathscinotes].
- **Width** = presented length: L·|sin a| + B·|cos a|.
- **Effective height h** [INFERRED, §6.4 of the dispersion appendix]: USN citadel 20 ft, RN 30 ft [S]. Model uses BB 10 m, CA 7.5 m, DD 4.5 m, PT 2 m.

Model table 3, in yd (range window × width / bow-on range window):

| Target | 16″/50 at 10 kyd | 20 kyd | 30 kyd | 5″/38 at 5 kyd | 10 kyd |
|---|---|---|---|---|---|
| BB | 143 × 273 / 381 | 76 × 273 / 313 | 56 × 273 / 293 | 166 × 273 / 403 | 64 × 273 / 301 |
| CA | 102 × 208 / 288 | 52 × 208 / 237 | 37 × 208 / 222 | 119 × 208 / 305 | 42 × 208 / 228 |
| DD | 60 × 120 / 169 | 30 × 120 / 138 | 21 × 120 / 129 | 70 × 120 / 179 | 24 × 120 / 133 |
| PT | 28 × 27 / 49 | 14 × 27 / 35 | 10 × 27 / 31 | 32 × 27 / 53 | 12 × 27 / 33 |

What follows from this:
- At long range the beam dominates the range window, so hits are deck hits.
- A bow-on target is deeper but narrow. The 1944 study gives end-on hits at about 40–70% of broadside rates [S].

### 8.2 Exact hit test (in code)

`hit_test()` intersects the shell's last metres of flight with a box hull (L × B × h) rotated to the target's aspect. The shell's path is the segment from height h down to the water at the fall angle. The test returns `side` or `deck`, which feeds [`08-gunfire-effects.md`](damage-research/08-gunfire-effects.md)'s "where it lands" step directly.

### 8.3 Quick analytic layer

```
P(hit per shell) = [Φ(depth/2 / s_r) − Φ((−depth/2 − danger) / s_r)] · [2Φ(width/2 / s_d) − 1]
with s_r = √(σ_FC_r² + σ_D_r²),   s_d = √(σ_FC_d² + σ_D_d²)
```

This is exact in expectation for normal errors. Below is the 16″/50 9-gun salvo against a broadside target (model table 4).

| MPI error 1σ | BB 10k | BB 20k | BB 30k | CA 20k | DD 10k | DD 20k |
|---|---|---|---|---|---|---|
| 0 | 63% | 28% | 14% | 19% | 40% | 9.0% |
| 0.5% of R | 56% | 21% | 9.6% | 14% | 31% | 6.4% |
| 1% | 43% | 13% | 6.1% | 8.8% | 20% | 4.1% |
| 1.5% | 33% | 9.4% | 4.3% | 6.2% | 15% | 2.9% |
| 2% | 26% | 7.2% | 3.3% | 4.8% | 11% | 2.2% |
| 3% | 18% | 4.9% | 2.3% | 3.2% | 7.7% | 1.5% |

The 1944 study (32.7 / 10.5 / 2.7%) sits on the 1.5% row at 10–20 kyd and between the 2% and 3% rows at 30 kyd. So "WWII practice-level fire control" ≈ 1.5–2.5% of range total MPI error.

---

## 9. Spotting

- **Optical over/short.** The spotter judges each splash against the hull: a short hides the waterline, an over appears behind. What he perceives is the depression-angle difference, h·e/R².
  - **Model:** σ = 4·10⁻⁵ rad · R² / h_eye [INFERRED].
  - Calibration: a 500 yd error is obvious at 12 kyd but barely visible at 19 kyd from about 120 ft [S: NavPers Ch. 18-C]. Optical spotting degraded beyond about 18 kyd [S].

  | Height of eye | 10 kyd | 15 kyd | 20 kyd | 25 kyd |
  |---|---|---|---|---|
  | 10 m (turret) | 366 yd | 823 | 1,463 | 2,286 |
  | 30 m (foretop) | 122 | 274 | 488 | 762 |
  | 40 m (tower) | 91 | 206 | 366 | 572 |

  This is the case for tall masts: "height of eye" was the spotting range. Deflection is seen directly, to about 1 mil.
- **Ladder and bracket (RN)** [S: navalgazing Spotting]:
  - Correct 400 yd until the fall crosses the target.
  - Then reverse by 200, then 100, which should give a straddle.
  - At most about 3–4 corrections; a salvo needs at least 3–4 shells to spot a wild one.
- **USN** [S]:
  - Bracket and halve, but never spot below the pattern size.
  - Rocking ladder of +100 / 0 / −100.
  - After a straddle, add a pattern every 3rd–4th salvo to check for overs.
- **Radar spotting** [S]:
  - Mk 3 and 284: splash ranges to about 20 kyd, range only.
  - Mk 8 Mod 3: to at least 35 kyd; deflection poor.
  - Mk 13: both axes beyond 42 kyd, to within 50–100 yd.
  - Problems: pips merging near the target, receiver saturation, other ships' splashes.
  - US doctrine: average radar spots.
- **Aircraft spotting.** Plan view, so both axes are read directly. Model: σ ≈ 70 m with a 30 s radio delay [INFERRED].
- **Timing.** Beyond about 20 kyd a spot cannot reach the next salvo; time of flight exceeds the salvo interval.
  - The code applies every spot relative to the correction *the spotted salvo was fired with*, which avoids "spot pyramiding" [S: NavPers time-of-flight buzzer].
  - Magnitude spots use half gain with a dead band of half a pattern. Full corrections chase the random salvo error and make things worse; the model shows this directly [INFERRED].

---

## 10. Salvos to straddle, the record

| Case | Salvos to straddle / first hit | Range |
|---|---|---|
| Practice (USN 1924–40) | hits from salvo 2–5 | 12–19 kyd |
| Lion, Dogger Bank | ~10 salvos | 20 kyd |
| Bismarck vs Hood | hit with 5th salvo | ~18–20 kyd |
| Scharnhorst vs Glorious | 3rd salvo | ~26 kyd |
| Prince of Wales | straddles on salvos 6, 9 and 13 | 26.5 kyd |
| Duke of York | first salvo hit; 31 of 52 salvos straddled | 12 → 20 kyd |
| Iowa vs Nowaki | first salvo straddled; no hits | 37.5 kyd |

Sources: INRO, Wikipedia, kbismarck, navalgazing (see the dispersion appendix §4).

---

## 11. Calibration: model vs history

The model's 14 levels with their sensors and conditions, against the record (model table 8, 40 runs each). "Combat" scenarios use `combat = 1.5`, which multiplies salvo-to-salvo error, laying error, rangetaker error and spotting noise. The other inputs are historical: visibility, smoke, spray, gun output and target behaviour.

| Case | Level | Model hit % | Record | Model straddle % |
|---|---|---|---|---|
| Tsushima 1905, Mikasa 12″, 6 kyd | 1 | 8.0 | ~40 / 446 (≈9%) | 58 |
| Jutland Run to the South, British battlecruisers | 3 | 2.7 | 21 / 1,469 (1.4%) | 17 |
| Jutland, German 1st Scouting Group | 3 + Zeiss | 4.0 | 67 / 1,670 (4.0%) | 25 |
| Denmark Strait, Bismarck | 4 + 10.5 m | 11.4 | 5–7 / 93 | 35 |
| USN LRBP 1932, 30 kyd | 4 | 2.8 | 4.2–5.4% practice | 37 |
| USN 1944 study, 10 / 20 / 30 kyd | 6 | 34 / 11 / 4.2 | 32.7 / 10.5 / 2.7 (estimate) | 69 / 57 / 48 |
| North Cape, Duke of York, night | 4 + 284M | 6.0 | ≥13 / 446 (≥3%) | 28 (record 60) |
| Guadalcanal, Washington, night, 8.4 kyd | 5r | 30 | 9–20 / 75 (12–27%) | 57 |
| Komandorski, Salt Lake City, 3.5 h stern chase (target zigzags to unmask turrets) | 5r | 1.0 | 2–5 / 832 (~0.5%) | 14 |
| Bismarck's end, Rodney | 4 | 27 | ~40 / 375 counted (≥11%) | 51 |

Notes:
- **Practice cases match within about 15–30%.** LRBP runs low (2.8 against 4–5%); the 1932 USN Ford system was probably better than the generic level 4.
- **Combat cases fall within about ×2 of the record.**
  - The ordering is right: German better than British at Jutland; radar-at-night high; stern chases near zero.
  - Recorded hits are lower bounds. Counts at Bismarck's end and North Cape are of identified hits on a burning ship.
- **North Cape exposes a gap.** Duke of York straddled 60% of the time with few hits; the model gives 25% straddles and more hits. That suggests 14″ quad-mount dispersion was worse than modelled (2.0% of range), or that night hits went uncounted. [UNCERTAIN]
- **Denmark Strait comes out ×2 high.** Prinz Eugen's splashes on Hood, Bismarck's shift of target to Prince of Wales, and short firing windows are only partly modelled.

---

## 12. The ladder: what each step of technology buys

### 12.1 Same ship, same gun, different fire control

8 × 15″/42, a salvo every 40 s, 15 min, combat conditions; target a battleship at 25 kn on a parallel course (model table 6). Each cell is hit % / straddle % / median seconds from first range to first hit.

| Level | Day 16 kyd, target zigzags | Day 25 kyd, steady | Haze + smoke, 13 kyd | Night 12 kyd |
|---|---|---|---|---|
| 0 MK1 Eyeball | 0.9 / 15 / 400 | 0.3 / 6 / 360 | 1.1 / 16 / 280 | no solution |
| 1 Early RF + stadimeter | 3.2 / 40 / 240 | 0.8 / 15 / 480 | 4.1 / 40 / 240 | no solution |
| 2 Dumaresq + clock | 4.6 / 45 / 200 | 1.7 / 23 / 280 | 5.1 / 42 / 160 | no solution |
| 3 Dreyer + director | 6.6 / 21 / 240 | 2.2 / 17 / 300 | 7.3 / 20 / 240 | no solution |
| 3b Argo clock | 8.0 / 27 / 240 | 2.8 / 21 / 240 | 9.5 / 24 / 200 | no solution |
| 4 AFCT / interwar | 9.4 / 33 / 160 | 3.2 / 26 / 280 | 10.6 / 28 / 160 | no solution |
| 4t AFCT + turn setting | 9.4 / 32 / 160 | 3.3 / 26 / 280 | 9.0 / 25 / 160 | no solution |
| 4j Japanese Type 92/98 | 8.4 / 29 / 200 | 3.2 / 25 / 280 | 8.7 / 23 / 200 | no solution |
| 5 Mk 8 + SV, optical | 11.2 / 38 / 160 | 4.8 / 35 / 200 | 13.8 / 33 / 160 | no solution |
| 5r + Mk 3 radar range | 12.2 / 39 / 160 | 4.7 / 36 / 200 | 14.9 / 38 / 160 | no solution (needs optical bearing) |
| 6 + Mk 13, radar spotting | 10.7 / 39 / 160 | 4.6 / 33 / 200 | 17.4 / 41 / 120 | 20.4 / 47 / 160 |
| 7 Auto-track analog | 15.7 / 53 / 120 | 7.2 / 48 / 200 | 23.9 / 62 / 120 | 24.0 / 58 / 160 |
| 8 Digital + laser | 22.7 / 74 / 120 | 9.5 / 62 / 120 | 32.0 / 78 / 120 | 33.6 / 75 / 120 |
| L Local control | 6.7 / 48 / 160 | 2.9 / 28 / 240 | 6.9 / 43 / 160 | no solution |

What the ladder says:
- **The early jumps are the biggest:** eyeball → rangefinder → director gives about ×7 at 16 kyd.
- **True-course computing** (Argo, AFCT) is worth about ×1.2–1.4 over a Dreyer plot.
- **Radar barely matters on a clear day at 16 kyd**, because salvo-to-salvo error dominates. It matters enormously in haze, smoke and night (×1.3–∞). Historically that is where radar won: Guadalcanal, Surigao, North Cape.
- **Post-war gains come from removing salvo-to-salvo error** (stabilisation, servo and muzzle-velocity measurement), not from better ranging.
- **Local control** is about 60–70% of director-level hit rate in daylight, and nothing at night. This answers the [UNCERTAIN] in [`02-components.md`](damage-research/02-components.md) §4.

### 12.2 Drop-in numbers for a simple game model

RMS MPI error in range, as % of range, by salvo number (day 16 kyd, zigzag target, combat; model table 7). Use it with the §8.3 formula, adding shell dispersion from the gun's `pattern_pct`.

| Level | Salvo 1 | 2–3 | 4–8 | 9+ | Deflection 9+ (mil) |
|---|---|---|---|---|---|
| 0 Eyeball | 22.6 | 16.7 | 10.9 | 10.6 | 9.8 |
| 1 Early RF | 6.3 | 6.0 | 4.9 | 4.9 | 5.4 |
| 2 Dumaresq + clock | 4.1 | 3.2 | 3.7 | 3.9 | 5.4 |
| 3 Dreyer | 3.0 | 3.0 | 2.9 | 3.2 | 4.9 |
| 3b Argo | 3.1 | 3.3 | 2.8 | 3.0 | 4.5 |
| 4 AFCT | 2.3 | 2.3 | 2.3 | 2.4 | 3.8 |
| 4t AFCT + turn setting | 2.5 | 2.4 | 2.2 | 2.3 | 4.0 |
| 4j Type 92/98 | 2.4 | 2.4 | 2.5 | 2.6 | 4.0 |
| 5 Mk 8 optical | 1.7 | 1.9 | 2.0 | 2.0 | 2.8 |
| 5r + Mk 3 | 1.7 | 1.8 | 1.8 | 2.0 | 2.9 |
| 6 + Mk 13 | 1.6 | 1.8 | 1.8 | 1.9 | 3.1 |
| 7 Auto-track | 1.0 | 1.3 | 1.3 | 1.4 | 3.1 |
| 8 Digital | 0.8 | 0.8 | 0.9 | 0.8 | 2.0 |
| L Local | 2.6 | 2.5 | 2.7 | 3.0 | 4.7 |

Notes:
- With a zigzagging target, errors barely shrink after salvo 1: spotting keeps up with rangefinder bias but not with manoeuvres plus random salvo error.
- Against a steady target the WWI levels improve by about ⅓ after 3–4 salvos (spotting removes the bias).
- **Quick-tier modifiers [INFERRED]:**

  | Situation | Multiplier on these % |
  |---|---|
  | Practice | 0.67 |
  | Steady target | ×0.8 for salvo 4+ |
  | Night without radar | no solution beyond illumination range |
  | Haze or smoke, optical levels | ×1.3–1.6 |

---

## 13. Implementation notes for the game

1. **Per firing ship and target, keep a gunnery state**:
   - tracker (polar or cart);
   - spot corrections (range, deflection) and ladder step;
   - per-instrument rangefinder biases;
   - the engagement's ballistic bias draws;
   - salvos in flight.

   `engage()` in the reference code is a complete, readable template at about 250 lines.
2. **Damage hooks** ([`02-components.md`](damage-research/02-components.md) §4) map onto parameters:

   | Damage | Effect |
   |---|---|
   | Director lost | Fall back to `L` (local) for the turrets, or to the after director with lower h_spot and sensors |
   | Plot / transmitting station lost | Tracker replaced by level 1–2 behaviour (eye-estimate prior, high latency) |
   | Radar aerial lost | Remove the radar sensor; optical levels remain |
   | FC cables cut | `director = False` |
   | Spotting top hit | h_spot drops |
   | Own smoke, director aft of funnel | Raise p_obscured for that sensor |

3. **Night.** A sensor with `bearing_from_optics` yields no solution without illumination. Starshell and searchlights temporarily raise `night_vis`, and searchlights also reveal the illuminating ship.
4. **Chasing salvos and evasion** are worth exposing to the player as orders. The model shows the effect (Komandorski: 1.0%); [`evasion-research.md`](evasion-research.md) has the full study of evasion styles and counters.
5. **Several ships on one target.** Use `spot_confusion` (0.2–0.3) to model splashes being misattributed. Germans and Americans used dye-coloured shells; give dyed shells a lower confusion value [INFERRED].
6. **CPU.** The full simulation is about 1 s step × handful of float ops per ship pair; trackers update only on readings. A 15-minute duel takes about 0.02 s in pure Python.

---

## 14. Gaps and uncertainties

- Whether the AFCT (or any WWII computer) took a target rate of turn is unconfirmed; level 4t exists so the choice is one switch. The target models inside the Mk 86 and WM-25 were not found.

- No sourced target-speed limits or solution times were found for German, Japanese, Italian, French or Soviet surface computers. Those levels are inferred from crew counts, plot-based design and battle results.
- No sourced dispersion tables were found for the 38 cm SK C/34, 4.7″, 6″ or 3″/76. Pattern values for these are inferred.
- No post-war surface-gunnery hit probabilities were found. Levels 7–8 are extrapolations (better stabilisation, muzzle-velocity measurement, laser or radar ranging). Treat their ladder numbers as plausible, not calibrated.
- **Salvo-to-salvo error (`salvo_pct`) is the most influential tuning value and the least directly sourced.** It is fitted to the USN 1944 study and the 1932–33 MPI data, then scaled by era. If one number gets playtesting attention, make it this one.
- Historical hit counts are poor for the Falklands (1914), Samar, Surigao, Komandorski and North Cape. Washington's count runs 8–20 depending on source.
- Coincidence vs stereo fatigue effects conflict between sources.
- The optical spotting constant (4·10⁻⁵ rad) is calibrated to one NavPers statement.

---

## Sources (main)

**Rangefinding**
- Admiralty Trilogy, Development of Optical Rangefinders (FI2024): https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf
- OP 1171 optical equipment (Mk 58 unit of error): https://mathscinotes.com/wp-content/uploads/2013/08/OP1171_Optical_Equipment.pdf
- navweaps tech-078 (stereo rangefinders): https://navweaps.com/index_tech/tech-078.php
- Naval Gazing, Rangefinding: https://navalgazing.net/Rangefinding
- Naval Gazing, Spot 1: https://www.navalgazing.net/Spot-1
- Wikipedia, Coincidence rangefinder: https://en.wikipedia.org/wiki/Coincidence_rangefinder
- Wikipedia, Stereoscopic rangefinder: https://en.wikipedia.org/wiki/Stereoscopic_rangefinder
- USNI 1904, Estimating distances: https://usni.org/magazines/proceedings/1904/april/estimating-distances
- USNI 1905, Stadimeter: https://www.usni.org/magazines/proceedings/1905/october/stadimeter-fire-control
- USNI 1930, Calibrating range finders: https://www.usni.org/magazines/proceedings/1930/february/method-calibrating-range-finders-sea
- JCRM reticle binocular study: https://journal.iwc.int/index.php/jcrm/article/view/816
- HumRRO stereo rangefinder training (DTIC ADA021002): https://apps.dtic.mil/sti/pdfs/ADA021002.pdf
- mathscinotes, Battleship rangefinders and geometry: https://mathscinotes.com/2013/08/battleship-rangefinders-and-geometry/
- kbismarck fire control: https://kbismarck.com/controltiri.html
- combinedfleet, Japanese fire control: https://www.combinedfleet.com/b_fire.htm

**Computing**
- NavPers Ch. 19-F (rangekeeping assumptions, manoeuvring targets): https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html
- Sperry target course predictor, US 2,995,296 (filed 1954): https://patents.google.com/patent/US2995296
- Naval Gazing open thread 138 (Mk 1A "target just turned"; AFCT Mk X tachymetric): https://navalgazing.net/Open-Thread-138
- Handbook of Dreyer Fire Control Tables (1918): https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php
- Naval Gazing, Rangekeeping Part 2: https://navalgazing.net/Rangekeeping-Part-2
- Naval Gazing, Fire Control Part 2: https://www.navalgazing.net/Fire-Control-Part-2
- Naval Gazing, Ballistics: https://www.navalgazing.net/Ballistics
- NavPers (Naval Ordnance and Gunnery) Ch. 25-A: https://eugeneleeslover.com/USNAVY/CHAPTER-25-A.html
- NavPers Ch. 25-C: https://eugeneleeslover.com/USNAVY/CHAPTER-25-C.html
- navweaps INRO battleship gunnery: https://navweaps.com/index_inro/INRO_BB-Gunnery.php
- navweaps tech-086 (USN vs IJN fire control): https://www.navweaps.com/index_tech/tech-086.php
- Wikipedia, Rangekeeper: https://en.wikipedia.org/wiki/Rangekeeper
- Wikipedia, Dumaresq: https://en.wikipedia.org/wiki/Dumaresq
- Wikipedia, Vickers range clock: https://en.wikipedia.org/wiki/Vickers_range_clock
- Wikipedia, Admiralty Fire Control Table: https://en.wikipedia.org/wiki/Admiralty_Fire_Control_Table
- Wikipedia, Mark I Fire Control Computer: https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer
- USNI 2015, The revolutionary rangekeeper: https://www.usni.org/magazines/naval-history-magazine/2015/october/revolutionary-rangekeeper
- HMCS Haida transmitting station: https://www.jproc.ca/haida/transmitting_station.html

**Radar** — full key in [`radar.md`](fire-control-research/radar.md). Main sources:
- navweaps, USN WWII radar: https://www.navweaps.com/Weapons/WNUS_Radar_WWII.php
- COMINCH P-08-03 radar handbook: https://ibiblio.org/hyperwar/USN/ref/RADONEA/COMINCH-P-08-03.html
- CB 3213 / BR 2435 "Navy Radar": https://www.commsmuseum.co.uk/publications/chc/Navy%20Radar%20CB%203213%20-%20BR%202435.pdf
- NavSource, Mk 8 / Mk 13: https://www.navsource.org/archives/01/57r.htm
- Wikipedia, Mark 56 GFCS: https://en.wikipedia.org/wiki/Mark_56_Gun_Fire_Control_System
- radartutorial, WM-20: https://www.radartutorial.eu/19.kartei/11.ancient4/karte044.en.html

**Dispersion, hits and battles**
- NavPers Ch. 18: https://www.eugeneleeslover.com/USNAVY/CHAPTER-18.php
- NavPers Ch. 18-A: https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html
- NavPers Ch. 18-C: https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html
- Danger space (range tables): https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/DANGER-SPACE.html
- USNI 1912, Dispersion and accuracy of fire: https://www.usni.org/magazines/proceedings/1912/december-0/dispersion-and-accuracy-fire
- USNI 1917, Accuracy of pointing at long range: https://www.usni.org/magazines/proceedings/1917/june/estimate-value-accuracy-pointing-long-range-naval-gunnery
- navweaps 16″/50 Mk 7: https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php
- Naval Gazing, Spotting: https://navalgazing.net/Spotting
- kbismarck, German fire effect tables: https://kbismarck.com/german-fire-effect-tables.html
- Wikipedia, Damage to major ships at Jutland: https://en.wikipedia.org/wiki/Damage_to_major_ships_at_the_Battle_of_Jutland
- Wikipedia, Battle of the Denmark Strait: https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait
- Wikipedia, Battle of the North Cape: https://en.wikipedia.org/wiki/Battle_of_the_North_Cape
- navweaps, Kirishima damage analysis: https://www.navweaps.com/index_lundgren/kirishimaDamageAnalysis.php
- Wikipedia, Battle of the Komandorski Islands: https://en.wikipedia.org/wiki/Battle_of_the_Komandorski_Islands
- navweaps tech-016 (Bismarck's final battle): https://navweaps.com/index_tech/tech-016.php
- navweaps tech-079 (Surigao): https://www.navweaps.com/index_tech/tech-079.php
