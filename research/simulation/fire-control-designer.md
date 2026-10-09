# Fire control in the ship designer: from tech values to hits
Status: final    Updated: 2026-10-09    Request: -

*Companion to [`fire-control-research.md`](fire-control-research.md), [`evasion-research.md`](evasion-research.md) and [`manual-gunnery-research.md`](manual-gunnery-research.md). Written 2026-10-09.*

*This note answers two questions:*
1. *How big a rangefinder should a cruiser carry, and why?*
2. *What raw physical values should the tech side hand over, so that the ship designer and the combat simulator can derive everything else without knowing what year it is?*

**Companion code:** [`designer_ref.py`](designer_ref.py) (imports [`fire_control_ref.py`](fire_control_ref.py)). Run `python3 designer_ref.py` for every table (about 12 minutes on 2 cores), or `physics` for the analytic tables only (seconds).

**Raw research appendix:** [`component-costs.md`](fire-control-research/component-costs.md) gives masses, sizes and crews of rangefinders, directors, computers and radars, each with sources.

**Tags:** [S] sourced, [INFERRED] my estimate or fit, [CAL] tuned so that the tech-built fits reproduce the calibrated levels of [`fire_control_ref.py`](fire_control_ref.py).

---

## TL;DR

- **Having a rangefinder matters enormously; its size matters moderately.**
  - Eye and stadimeter only, at 10–25 kyd: **10–20 %** of the hits of a 4.6 m (15 ft) set.
  - Going from 4.6 m to 15 m: **+2–10 % at 10 kyd, +10–20 % at 15 kyd, +20–30 % at 20 kyd, +20–40 % at 25 kyd** (Table 5).
  - In a race to three hits between two otherwise equal cruisers at 20–25 kyd, the 15 m ship wins about **60–68 %** of the time.
- **Sizing rule** (fits the Monte Carlo knee): pick the base whose clear-weather *unit of error* at the design range roughly equals the range sigma of your own salvo,
  - δ·R²/(B_eff·M) ≈ σ_pattern(R).
  - For an 8 in, 9-gun cruiser that means about **4 m for 10 kyd, 6 m for 15 kyd, 8–9 m for 20 kyd, 12 m for 25 kyd**.
  - Real cruisers carried 4.6–8 m, which is consistent with designing for 15–20 kyd.
- **The better the rest of the chain, the more a big rangefinder pays.**
  - A hand plot gains +10 % at 20 kyd from 15 m over 4.6 m; a stabilised rangekeeper gains +28 %.
  - Haze and a zigzagging target push the knee up by one size step.
- **Magnification has a sweet spot around 25–35×.**
  - ×15 costs 10–15 % of hits.
  - ×50 is no better than ×25 in this model: shimmer, vibration, a dim image and a narrow field win.
- **Placement matters less than base length.**
  - Height (20 vs 40 m) barely changes anything at these ranges; a light pole mast costs a few per cent.
  - A second or third instrument adds 7–10 % each, which is about the same as one step up in base.
- **A good fire-control radar makes rangefinder size irrelevant.**
  - With an X-band set, 0 m and 8 m rangefinders give the same hits (Table 9). This is what happened to the USN after 1943.
  - A metric, range-only radar replaces the rangefinder's range at 12–18 kyd but not at 24 kyd, and it still needs optics for bearing.
- **Weight alone will not stop a player from fitting a 15 m rangefinder on a cruiser.**
  - Each base step costs 1–3 t and 3–8 mm of GM on a 10,000 t ship.
  - The things that really limited it (director width, money, production, operator pool, exposure to splinters, vibration of tall light masts) must be costed explicitly (§7).

---

## 1. The pipeline

```
TECH  (raw physical values, no years)          OpticsTech, RadarTech, ComputerTech,
                                                MountTech, PropellantTech, CrewTech
  │
  ▼
DESIGN (player or AI choices)                   RFFit, DirectorFit, RadarFit → FCSystemFit
  │
  ├──► combat simulator: Sensor objects + FCLevel (exactly what fire_control_ref.engage consumes)
  └──► ship designer:    mass, height of centre of mass, rotating diameter, crew, power, volume
```

**Rules:**
- The combat simulator never sees a tech name or a year. It only sees the numbers in §3.
- The tech side never sees the combat model. It only promises physical values.
- Everything in between is a formula in [`designer_ref.py`](designer_ref.py) that you can port 1:1.

---

## 2. Raw values the tech side outputs

### 2.1 OpticsTech

| Field | Unit | Range | Meaning, and who consumes it |
|---|---|---|---|
| `delta_arcsec` | arcsec | 10–16 | Acuity of a trained rangetaker through these optics. USN design value 12″ [S]. **Combat:** the rangefinder error law. |
| `rigid_base` | m | 2–6 | Longest base whose tube keeps alignment under temperature and flexure. **Combat:** effective base. |
| `flex_exp` | – | 0.6–0.85 | Beyond `rigid_base`, B_eff = rigid·(B/rigid)^flex_exp. NDRC 1941 found long bases deliver less than geometry predicts [S qual., exponent INFERRED]. |
| `M_max` | × | 15–30 | Highest magnification at full image quality; beyond it the image dims. **Designer:** caps the magnification choice. **Combat:** penalty. |
| `stereo` | bool | | Stereo instruments available. **Designer:** option. Needs operators from `CrewTech.stereo_pool`. |
| `coated` | bool | | Anti-reflection coatings: excess haze penalty ×0.8 [INFERRED]. |
| `mass_k` | kg/m² | 25–30 | Bare instrument mass = mass_k·B² [INFERRED; anchors in §4.4]. **Designer.** |
| `gyro_stab` | bool | | Gyro-stabilised mountings: readings per minute ×1.5, vibration limit ×1.5 [INFERRED]. |

### 2.2 RadarTech

| Field | Unit | Range | Meaning |
|---|---|---|---|
| `lam_min` | m | 0.03–1.5 | Shortest wavelength with useful power. With antenna size it sets the beamwidth. |
| `P_max_kW` | kW | 2–250 | Peak power. |
| `tau_us` | µs | 0.1–2 | Shortest pulse. Sets range resolution and part of the range noise. |
| `NF_dB` | dB | 3–15 | Receiver noise figure [S typ.: WWII 10–15, 1970s 3–6]. |
| `bearing` | enum | none / lobing / conical / monopulse | How bearing is measured. Bearing sigma = beamwidth / K, with K = 4 / 10 / 15 / 25 [INFERRED]. `none` means bearing comes from optics. |
| `range_unit` | enum | scope / precision / digital | Range measurement circuit: noise floor 30 / 13 / 8 m [CAL]. |
| `auto_track` | bool | | 60 readings per minute. |
| `splash` | enum | none / range / both | Whether the display shows shell splashes in range only, or in range and bearing. |
| `set_mass_t` | t | 1–2.5 | Office equipment (transmitter, receiver, consoles) [S: Mk 3 1.7 t packed; SPG-53 2.3 t; Japanese Type 22 1.3 t]. |
| `ant_kg_m2` | kg/m² | about 40 | Antenna mass per m² of aperture [S: SK 2,400 lb / 27 m²; SPG-53 163 lb / 1.8 m², both about 40]. |

### 2.3 ComputerTech

This maps onto the tracker kinds of [`fire_control_ref.py`](fire_control_ref.py) §4.

| Field | Meaning |
|---|---|
| `tracker` | `polar` (rate-keeping: Dumaresq, Dreyer), `cart` (true course: Argo, AFCT, Ford), `ca` (acceleration), `ct` (coordinated turn). This decides **which target motions the computer can follow at all**. |
| `helm_free` | Own ship may manoeuvre without losing the solution. |
| `latency`, `q` | Reading-to-computer delay (s); manoeuvre allowance (m²/s³). |
| `max_rdot`, `max_bdot` | Clock limits (Dreyer ±1,200 yd/min, ±15°/min [S]). |
| `own_turn_kick` | Solution error while own ship turns (m). |
| `wander_pct` | Salvo-to-salvo generated-range and transmission wander, % of range [CAL]. |
| `resid`, `defl_bias_mil` | Residual ballistic bias before spotting (wind, density, drift corrections). |
| `detect`, `sig_a`, `q_turn`, `tau` | Manoeuvre detection and filter parameters. |
| `mass_t`, `crew`, `power_kW` | **Designer only:** sits below armour in the plotting room / transmitting station [S: Ford Mk 1A 1.4 t, 62×38×45 in; Dreyer table crew 7–8 plus helpers; mass INFERRED]. |

### 2.4 MountTech, PropellantTech, CrewTech

| Group | Field | Meaning |
|---|---|---|
| Mount | `director` | Guns laid from one director. If false, each gun's laying error widens the pattern. |
| Mount | `stab_vertical` | Stable vertical or gyro firing: no sea-state penalty. |
| Mount | `lay_mil`, `defl_spot_mil` | Common laying error per salvo; deflection wander. |
| Propellant | `mv_gun_pct` | Round-to-round muzzle-velocity sigma, % [S typ.: black 1.0, brown 0.8, early smokeless 0.5, mature 0.3–0.35]. |
| Propellant | `smoke` | black / brown / smokeless: blinding times in the manual-gunnery model. |
| Crew | `operator` | Rangetaker multiplier: 0.85 elite, 1 trained, 1.5 average, 2 green. |
| Crew | `stereo_pool` | Share of men who pass the stereo test [S: under 5 %]. Limits how many stereo instruments a navy can man. |
| Crew | `spot_k` | Spotting difficulty multiplier. |

Pre-dreadnought sights and gear rates are already raw values in [`manual_gunnery_ref.py`](manual_gunnery_ref.py) (`Group.sight`, `elev_rate`, `powder`, `skill`). They can be tech outputs unchanged. For sights, a telescope's error fits σ ≈ √((4′/M)² + slop²), with slop 2.3′ for early mounts and 1′ for good ones [INFERRED]. That reproduces open 4′, early telescope 2.5′ and telescope 1.2′.

### 2.5 Example tiers

The labels are for humans only. Any combination is legal; the tech tree can interpolate.

| Tier | Key values | Analogue |
|---|---|---|
| O1 | δ 14″, rigid 2 m, M_max 20, coincidence only | B&S FA, c.1895 |
| O2 | δ 12″, rigid 4 m, M_max 28, coincidence only | FQ2 / FT24 |
| O3 | δ 12″, rigid 5 m, flex 0.75, M_max 30, stereo | Zeiss, interwar B&S |
| O4 | rigid 6 m, flex 0.8, coated, gyro-stabilised | USN 1943 |
| R1 | 40 cm, 20 kW, 2 µs, bearing from optics, scope ranging | Seetakt / 284 / Mk 3 |
| R2 | 10 cm, 25 kW, 1 µs, lobing | Mk 8 |
| R3 | 3 cm, 50 kW, 0.3 µs, lobing, splash in range and bearing | Mk 13 |
| R4 | 3 cm, 250 kW, conical scan, auto-track | Mk 56/35 |
| R5 | 3 cm, NF 5 dB, monopulse, digital | SPG-60, WM-25 |
| C0 … C6 | hand plot → rate clock → Dreyer → AFCT → Ford Mk 8 → Mk 1A-class → digital | |
| M0 … M4 | local laying → director → power drive → stable vertical with remote power control (RPC) → digital servo | |

---

## 3. Derivations: design choice → combat-model numbers

### 3.1 Optical rangefinder (`RFSensor`)

The designer picks **base B, magnification M, kind (stereo/coincidence), mount (open / hood / armoured / director / turret), what it stands on (turret / tower / tripod / pole / deck), height, armour and count**.

```
unit error (m)       u(R) = δ · R² / (B_eff · M_eff)                         δ in radians
effective base       B_eff = B                         if B ≤ rigid_base
                           = rigid · (B/rigid)^flex_exp  otherwise
effective mag.       M_lim = 1 / sqrt(1/M_atm² + 1/M_vib²)
                     M_eff = M / sqrt(1 + (M/M_lim)²) / 0.835               (0.835: M=28 nominal [CAL])
                     M_atm = 60 / k_optic                (clear 60, haze 40)  [INFERRED]
                     M_vib = stiffness · min(1, sqrt(25 m / h)) · (1.5 if gyro_stab)
                             stiffness: turret 70, tower 60, deck 55, tripod 50, pole 32  [INFERRED]
                     M > M_max: M_eff × sqrt(M_max/M)    (dim image)
one-reading sigma    σ = operator · combat · k_weather · spray · u(R)
                     k_weather: k_optic; stereo ×0.8 on the excess; coated ×0.8 on the excess;
                                coincidence ×1.5 at low contrast
                     spray = 1 + 0.15·max(0, sea−2)·max(0, (14−h)/8)          [INFERRED]
instrument bias      1.5 · u(R)  (fixed per instrument per engagement; spotting removes it)  [INFERRED]
readings/min         coincidence 3, stereo 4; ×1.5 if gyro_stab; ×sqrt(min(1, 30/M))  [INFERRED]
bearing sigma        director 0.7 mil, turret 1.5, otherwise 1.0
visibility           horizon 3.86(√h + √h_target) km, meteorological visibility, smoke (unchanged)
```

Magnification curves come out like this (Table D3; effective M):

| Nominal M | Tower, clear | Tower, haze | Pole mast, clear | Pole mast, haze |
|---|---|---|---|---|
| 15 | 16.9 | 16.3 | 15.6 | 15.2 |
| 25 | 25.5 | 23.7 | 21.7 | 20.6 |
| 32 | 29.1 | 26.4 | 23.6 | 22.0 |
| 50 | 29.5 | 26.1 | 22.9 | 21.2 |

Above about 30× you buy almost nothing, and less on a light mast or in haze. This is why high-power settings (Bismarck's 50×) were for clear days only.

Effective base by optics tier (D4):

| B | O1 | O2 | O3 | O4 |
|---|---|---|---|---|
| 4.6 m | 3.7 | 4.4 | 4.6 | 4.6 |
| 8.1 m | 5.7 | 6.8 | 7.2 | 7.6 |
| 15 m | 9.1 | 10.8 | 11.4 | 12.5 |

Early optics make long bases poor value: an O1 15 m set is worth about a 9 m set.

### 3.2 Radar (`RadarFit`)

The designer picks antenna width and height (and optionally wavelength, power and pulse within the tech limits) and the mounting height.

```
beamwidth            θ_az = 70·λ/W deg, θ_el = 70·λ/H deg
max range on a BB    R_max = 36.6 km · [ (P·τ·A²/λ²/NF) / (same for Mk 13) ]^(1/4)   [CAL on Mk 13]
                     then capped by the radar horizon 4.12(√h + √h_t) km and scaled by RCS^(1/4)
range sigma          rad_a = sqrt( (0.07 · c·τ/2)² + unit² ), unit = 30 / 13 / 8 m      [CAL]
                     rad_b = 0.001 (analog range units), 0 (digital)
bearing sigma        θ_az(mil) / K_BRG
minimum range        max(200 m, 1.5 · c·τ)
readings/min         auto-track 60; splash in range and bearing 20; lobing 10; none 6
splash spotting      tech.splash, out to 0.85 · R_max
```

Check against the sourced legacy constants (D1):

| Set | Model R_max (BB) | Legacy | Model range σ | Legacy |
|---|---|---|---|---|
| Mk 3 class (40 cm, 15 kW, 12×3 ft) | 23.8 km | 26 km | 37 m | 37 m |
| Mk 8 class (10 cm, 20 kW, 10×3.3 ft) | 36.9 km | 36.6 km | 17 m | 14 m |
| Mk 13 class (3 cm, 50 kW, 8×2 ft) | 36.6 km | 36.6 km | 13 m | 14 m |

The post-war sets come out longer than the legacy figures, but the horizon caps them anyway.

### 3.3 Computer, mount and propellant → FCLevel

Every field passes straight through except `salvo_pct`, which is assembled from physical parts:

```
salvo_pct = sqrt( (S · mv_gun_pct / sqrt(n_guns))² + wander_pct² )
S = range_sensitivity(gun, R) = (dR/R)/(dv/v), from the gun's own fitted ballistics
    8in/55: 1.79 at 5 kyd, 1.60 at 10, 1.44 at 15, 1.35 at 20–25 kyd (vacuum would give 2)
```

With 9 guns the muzzle-velocity term is small (0.17 % with mature smokeless powder). The salvo-to-salvo error is mostly the computer and transmission chain, which is why `wander_pct` sits in `ComputerTech`.

**Calibration (D1).** Same scenario as the legacy levels (8 in cruiser, 15 kyd, CA target, n=60):

| Legacy level | Hit % | Tech-built fit | Hit % |
|---|---|---|---|
| 3 Dreyer + director | 6.6 | C2/M1/O2, 2.74 + 4.57 + 2.74 m coincidence | 6.5 |
| 4 AFCT | 8.6 | C3/M2/O3, 4.57 m director + 9.1 m turret | 8.3 |
| 5 Ford + stable vertical | 10.3 | C4/M3/O4, 8.1 m stereo director | 10.0 |
| 6 + Mk 13 | 10.4 | the same + R3 radar | 10.2 |

First-hit times match too. So the tech-built path can replace the hand-written levels; the levels become presets.

### 3.4 Designer-side cost formulas

```
instrument mass        m_i = mass_k · B²                                  (t; mass_k ≈ 27 kg/m²)
open pedestal          3 · m_i
hood / turret hood     3 · m_i + box(W=B+0.6, D=1.6, H=1.9, t=max(armour, 6 mm))
armoured hood          3 · m_i + box(..., t=armour)
director               box(W=max(3, B+0.6), D=3.5, H=2.6; armour on 60 %, 6 mm on the rest)
                       + 8 t equipment + 2 · m_i + radar antenna
box(W,D,H,t)           (2WH + 2DH + WD) · t · 7.85 t/m³
radar                  aloft: 40 kg/m² · W·H at antenna height; office: set_mass_t low in the ship
crew                   RF 2 (armoured 3), director 6–8, radar 2–3, table 2–10 by tech
rotating diameter      ≈ B + 0.6 m   (the real constraint on a cruiser's bridge; §7)
```

**Director check:**
- Mk 37 with a 4.57 m RF: model 14.1 t at 12.7 mm and 21.6 t at 38 mm.
- Sourced: 16 t on destroyers (0.5 in) and 21 t on battleships (1.5 in) [S navweaps / NavPers].

Selected costs (D5):

| Fit | Mass t | Height m | t·m above waterline | Width m |
|---|---|---|---|---|
| 4.57 m director, 12.7 mm | 14.1 | 30 | 423 | 5.2 |
| 6.1 m director, 25 mm | 20.5 | 30 | 614 | 6.7 |
| 8.1 m director, 38 mm | 30.1 | 35 | 1,054 | 8.7 |
| 15 m director, 50 mm | 59.3 | 40 | 2,372 | 15.6 |
| 4.57 m armoured hood, 25 mm | 8.4 | 25 | 209 | 5.2 |
| 8.1 m turret RF, 50 mm | 26.1 | 10 | 261 | 8.7 |

---

## 4. The cruiser study

**Set-up:**
- **Shooter:** 9 × 8 in/55 in three triple turrets, 20 s salvos.
- **Target:** CA-size, 30 kn, steady or zigzagging (±25° every 150 s), on a parallel course so the range stays roughly constant.
- **Weather:** clear (vis 35 km) or haze (k_optic 1.5, vis 28 km).
- **Run:** 15-minute engagement, 100 engagements per cell. One rangefinder of base B at ×25 in the director, 30 m up. Base 0 means stadimeter plus the officer's eye.
- **Three tech sets:**
  - **plot:** C2 Dreyer-type, coincidence O2 optics, M1 director.
  - **table:** C3 AFCT-type, O3, M2.
  - **keeper:** C4 Ford-type, O4, M3 stable vertical.

### 4.1 Hits against base (D6, condensed)

Hits in 15 min, relative to 4.57 m = 100. Each cell is the mean of clear/haze × steady/zigzag. The figure in brackets is the probability of scoring 3 hits first against an identical ship with a 4.57 m rangefinder.

**keeper (C4/M3/O4).** Absolute hits at 4.57 m: 78 / 27 / 11.4 / 5.2.

| Base | 10 kyd | 15 kyd | 20 kyd | 25 kyd |
|---|---|---|---|---|
| none (eye) | 14 (0.09) | 10 (0.05) | 10 (0.05) | 11 (0.14) |
| 2.74 m | 91 (0.45) | 86 (0.41) | 80 (0.38) | 79 (0.40) |
| 3.66 m | 97 | 95 | 91 | 91 |
| 4.57 m | 100 (0.50) | 100 (0.50) | 100 (0.50) | 100 (0.50) |
| 6.1 m | 103 | 108 | 108 | 116 |
| 8.1 m | 106 (0.51) | 112 (0.56) | 116 (0.58) | 118 (0.57) |
| 10 m | 107 | 115 | 121 | 126 |
| 15 m | 110 (0.52) | 119 (0.59) | 128 (0.64) | 140 (0.68) |

**table (C3/M2/O3)**, in the same units:

| Base | 10 kyd | 15 kyd | 20 kyd | 25 kyd |
|---|---|---|---|---|
| 2.74 m | 91 | 88 | 83 | 82 |
| 8.1 m | 105 | 109 | 111 | 120 |
| 15 m | 107 (0.55) | 118 (0.57) | 122 (0.62) | 137 (0.63) |

**plot (C2/M1/O2):** 2.74 m 95 / 90 / 87 / 86; 15 m 107 / 114 / 110 / 119.

**What this says:**
1. **The step from nothing to any rangefinder is ×5–10.** Without one the computer never gets a range good enough to start a useful ladder. This is the pre-dreadnought lesson again ([`manual-gunnery-research.md`](manual-gunnery-research.md)).
2. **Beyond the knee, size buys tens of per cent, not multiples.** Spotting removes the instrument's bias after the first straddle. What remains is the quality of the range *rate* and how fast the plot recovers after the target turns.
3. **Value rises with range** (error ∝ R²), with haze, and against a zigzagging target (re-convergence after each turn). In haze, the 10 kyd knee moves from 3.7 to 4.6–5.5 m.
4. **Better computers make the rangefinder the bottleneck.** The hand plot cannot use a 15 m instrument's precision, because its own rate errors dominate. The rangekeeper can.

### 4.2 The knee and the rule (D7, D11)

Smallest base that reaches 90 % of the best base's hits (mean of steady and zigzag):

| Tech, weather | 10 kyd | 15 kyd | 20 kyd | 25 kyd |
|---|---|---|---|---|
| plot, clear / haze | 2.7 / 3.7 | 4.6 / 8.1 | 6.1 / 4.6 | 8.1 / 8.1 |
| table, clear / haze | 3.7 / 4.6 | 5.5 / 8.1 | 6.1 / 8.1 | 8.1 / 10 |
| keeper, clear / haze | 3.7 / 5.5 | 5.5 / 8.1 | 8.1 / 10 | 15 / 10 |
| **Rule, k = 1** | **4.0** | **6.3** | **9.2** | **12.3** |
| Rule, k = 0.7 / 1.4 | 5.9 / 2.9 | 10.0 / 4.3 | 14.7 / 5.9 | 19.8 / 7.9 |

**Sizing rule (for the AI designer and the player tooltip):**

> Choose B so that δ·R_design² / (B_eff·M) ≈ k·σ_pattern(R_design), with k ≈ 1 (0.7 for "best in class", 1.4 for "adequate").
>
> σ_pattern is the shooter's own salvo range sigma: `gun.sigma_D(R, n)` in the code. For the 8 in cruiser it is 49 m at 10 kyd and 97 m at 20 kyd.

The rule has a physical reading. When one reading's error equals the scatter of the salvo, the opening salvo lands as close as the guns can group it, and further base buys nothing until the ladder is done.

It also explains why bigger guns want bigger rangefinders. A battleship salvo's sigma in metres is similar to a cruiser's, but she fights at longer range: at 25 kyd the rule gives 12 m. That is about what Richelieu, Iowa and Yamato carried (12, 13.5 and 15 m [S combinedfleet]).

**Historical check:**
- Baltimore class: Mk 34 with an 18 ft (5.5 m) rangefinder [S].
- The following are from memory, not re-checked [UNCERTAIN]:
  - Hipper class: 7 m.
  - Myōkō class: 6 m, later 8 m.
  - Zara class: 5 m in the director, 7.2 m in the turrets.
  - Counties: 12–15 ft (3.7–4.6 m).

That is the 15–20 kyd design range this rule implies. The British were the low outlier, consistent with their expectation of shorter-range, often hazy North Sea fights.

### 4.3 What it costs and what each step buys (D10)

Table tech, director at 30 m, 12.7 mm director plating. ΔGM is for a 10,000 t cruiser with KG 6.5 m.

| Base | Director t | t·m above KG | ΔGM mm | Extra hits/15 min at 10 / 15 / 20 / 25 kyd (vs previous row) |
|---|---|---|---|---|
| 2.74 m | 12.1 | 285 | −28 | +45 / +16 / +6.3 / +3.1 (vs eye only) |
| 3.66 m | 13.1 | 307 | −31 | +3.1 / +1.3 / +1.0 / +0.7 |
| 4.57 m | 14.1 | 331 | −33 | +2.1 / +1.2 / +0.5 / +0.1 |
| 5.5 m | 15.2 | 358 | −36 | +1.1 / +0.9 / +0.4 / +0.5 |
| 8.1 m | 18.9 | 445 | −44 | +1.0 / +0.9 / +0.3 / +0.5 (vs 6.1 m) |
| 15 m | 32.3 | 758 | −76 | +0.9 / +0.9 / +0.6 / +0.5 (vs 10 m) |

**Weight versus a gun.** Going from 4.6 m to 8 m costs about 5 t and 11 mm of GM, and buys about 10–15 % more hits at 15–20 kyd. Ten per cent more guns would buy a similar gain and cost hundreds of tonnes of mounts, barbettes and magazines, two orders of magnitude more. **By weight alone the bigger rangefinder is always the better buy.** The real limits were elsewhere (§7), and the game has to model them or every player will fit the biggest tube.

### 4.4 Same budget, different arrangement (D8)

Table tech, 18 kyd, zigzagging target. Hits in 15 min (clear / haze). Reference: one 4.57 m ×25 in the director at 30 m = 11.4 / 9.5.

| Change | Clear | Haze | Reading |
|---|---|---|---|
| Director at 20 m | 11.4 | 9.5 | Height doesn't matter for precision at 18 kyd; it matters for horizon (beyond about 30 kyd on a CA) and for spotting. |
| Director at 40 m, tower | 11.5 | 9.9 | |
| Director at 40 m, pole mast | 11.1 | 9.2 | A light mast shakes. |
| ×15 | 9.7 | 8.3 | Too little magnification costs 15 %. |
| ×35 | 11.4 | 9.7 | Sweet spot. |
| ×50 | 10.0 | 9.2 | Shimmer, dim image, narrow field. |
| Coincidence instead of stereo | 9.6 | 8.5 | Fewer readings, worse in haze. |
| + second 4.57 m (after control) | 12.2 | 10.5 | +7–10 % each: averaging biases and more readings. |
| + third (turret) | 12.4 | 11.0 | |
| 8.1 m in the director | 12.8 | 11.6 | One step up in base ≈ two extra small instruments. |
| 4.57 m director + 8.1 m turret RF | 12.7 | 11.2 | The interwar battleship arrangement. Turret RFs suffer spray in sea state 4+. |
| 2.74 m director + 8.1 m turret RF | 13.0 | 10.5 | |

**Design implication:** a designer can trade one big instrument against several small ones, and either is defensible. The small-instrument route also survives damage: one hit no longer blinds the ship. The damage model should make that matter.

### 4.5 Radar changes the question (D9)

Keeper tech, haze, zigzagging target. Hits in 15 min / hits in first 5 min / median seconds to first hit.

| Radar | RF | 12 kyd | 18 kyd | 24 kyd |
|---|---|---|---|---|
| none | eye | 4.0 / 0.6 / 562 | 1.3 / 0.1 / – | 0.4 / 0.1 / – |
| none | 2.74 m | 34.4 / 6.4 / 179 | 10.2 / 1.9 / 234 | 3.6 / 0.5 / 438 |
| none | 8.1 m | 48.8 / 10.0 / 157 | 15.2 / 3.5 / 192 | 5.3 / 0.9 / 304 |
| metric (Mk 3 class) | eye | 40.8 / 9.8 / 157 | 15.0 / 3.4 / 189 | 1.3 / 0.1 / 813 |
| metric | 8.1 m | 48.4 / 11.2 / 157 | 17.2 / 3.7 / 189 | 5.5 / 1.0 / 286 |
| X-band (Mk 13 class) | eye | 53.5 / 11.9 / 156 | 19.3 / 4.3 / 171 | 7.8 / 1.7 / 214 |
| X-band | 8.1 m | 54.8 / 12.5 / 139 | 18.9 / 4.3 / 173 | 8.0 / 1.6 / 228 |

**What this says:**
- **With a centimetric FC radar the rangefinder becomes a backup.** Its size no longer moves the result. The designer should then spend the rangefinder budget on redundancy instead: a small optical set for jamming, radar failure and close-in work.
- **A metric radar** (range only, bearing from optics) is as good as an 8 m rangefinder inside about 18 kyd, but it falls off a cliff at 24 kyd (Mk 3 class R_max 24 km on a battleship, less on a cruiser). An optics fit is still needed for bearing and long range.

---

## 5. How the game should use this

**Ship designer UI:**
- Show the rule as a live readout: *"unit of error at 15 kyd: 62 m; your salvo's spread: 73 m → adequate"*, plus the D6-style curve for the ship's own guns.
- The player sees the knee directly.

**AI designer:**
- Pick `R_design` from doctrine (cruiser 15–20 kyd, battleship 20–28, destroyer 8–12).
- Pick `B` from the rule with k = 1, capped by the director width allowed by the superstructure (§7).
- Round up one step if haze is common in the theatre or the computer tier is C4 or higher.
- Fit 2–3 instruments on ships over about 8,000 t.
- Once an X-band FC radar is available, freeze B and add redundancy instead.

**Combat simulator:** consumes only `Sensor` and `FCLevel` objects (§3). It never needs the tech tree.

**Damage model hooks:**
- Each instrument is a component with presented area ≈ B × 1 m and an armour value.
- Losing the director rangefinder hands the solution to the next instrument (turret or after control), with the latency and spray of that position.
- D8 tells you what that is worth.

---

## 6. Recommended parameter set for a first implementation

| Object | Fields the combat model reads |
|---|---|
| `Sensor` (optical) | `kind, height, per_min, sig_brg_mil, blind`; `u(R)` from `delta, B_eff, M_eff(cond)`; bias factor 1.5 |
| `Sensor` (radar) | `height, per_min, sig_brg_mil, rad_a, rad_b, rad_max_bb, rad_min, splash, splash_max, bearing_from_optics, blind` |
| `FCLevel` | `tracker, helm_free, q, latency, max_rdot, max_bdot, resid, defl_bias_mil, lay_mil, director, stabilised, own_turn_kick, spot_gain, ladder0, h_spot, aircraft, salvo_pct, salvo_defl_mil, tau, sig_a, q_turn, detect` |
| Ship designer, per component | `mass, z (centre of mass), rotating diameter, crew, power_kW, armour_mm, presented area` |

`blind` (relative-bearing blind arcs) should be computed by the designer from the superstructure geometry: an instrument forward of the funnels cannot see aft through them. `h_spot` is the highest spotting position, normally the director.

---

## 7. Costs the game must add, or players will max the rangefinder

The model shows weight and stability are *not* what limited rangefinder size on cruisers. The plausible real limits, all [INFERRED] unless noted:

1. **Width and arcs.**
   - The director's body must be about B + 0.6 m wide; a 15 m director on a 20 m-beam cruiser bridge does not fit.
   - Long tubes in the open foul masts, funnels and other directors, giving blind arcs.
   - Turret rangefinders stick out the sides of the gunhouse.
   - **Game:** the director width must fit the superstructure deck it sits on, and long tubes generate blind arcs.
2. **Money and production.**
   - Optical works made a limited number of long tubes.
   - B&S had delivered 45 FT24s by 1915 [S].
   - **Game:** cost ∝ B² to B³, and a per-turn production cap per base class.
3. **Operators.**
   - Under 5 % of recruits qualify for stereo [S].
   - **Game:** stereo instruments draw from a limited pool; untrained operators have `operator` = 1.5–2.
4. **Vibration on light hulls.**
   - German trials blamed machinery vibration and gearing for poor rangefinder results [S].
   - **Game:** `M_vib` should depend on hull stiffness and shaft power as well as the support type. A fast, light cruiser should get a lower M_vib than the table's tower value.
5. **Exposure.**
   - A long tube on a high director is a large splinter target; the more instruments, the more redundancy.
   - Light cruiser designs often accepted a smaller base plus more instruments.
6. **Topweight in the aggregate.**
   - Weight is only a few tonnes per step, but it sits at 30–40 m.
   - Real cruisers were often already topweight-limited by AA and radar (Atlanta class, Japanese reconstructions removing rangefinders [S]).
   - It only bites when the ship is already marginal, and the designer's stability model will handle that once the director's t·m is passed in.

---

## 8. Sources and confidence

**Component masses, crews and radar parameters:** [`component-costs.md`](fire-control-research/component-costs.md) (sourced from navweaps, NavPers via eugeneleeslover.com, ibiblio Radar-2, navsource, kbismarck, combinedfleet, Science Museum Group, Admiralty Trilogy FI2024).

Two additional instrument weights were checked for the mass law:
- An NMM naval rangefinder, 1.63 m long, 73 kg: [RMG NAV1983](https://www.rmg.co.uk/collections/objects/rmgc-object-205478).
- A small B&S FT37, 5.6 kg: [Science Museum Group](https://collection.sciencemuseumgroup.org.uk/objects/co8057769/ft37-rangefinder-one-of-two).

**Weakest links, in order:**
1. **Instrument mass law** (mass_k·B², exponent 2–2.5). The public data points are few and mixed (field, AA and naval builds). Director and hood masses are dominated by plating, so this matters little for the decision.
2. **Magnification limits** (`M_atm`, stiffness values) [INFERRED]. They shape the ×15 / ×25 / ×50 result, but not the base result.
3. **Radar bearing constants** `K_BRG` [INFERRED]: Mk 8 comes out 3.9 mil against a legacy 2.0; Mk 13 comes out 1.5 against 2.0.
4. **Hit counts** carry about ±5 % Monte Carlo noise at n = 100. Differences under about 5 % in Tables D6 and D8 should not be read as real.
