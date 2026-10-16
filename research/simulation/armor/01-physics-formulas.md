# 01 — Penetration of homogeneous (ductile) plate: physics, definitions, formula catalogue
Status: draft    Updated: 2026-10-16    Request: —

*Research input for the naval wargame armour-penetration model (period 1860–1945). This file holds the inputs, not a finished model. Every number has an inline source. **[INFERRED]** marks my own derivations or calculations. **[UNCERTAIN]** marks conflicts, OCR-degraded sources, or forms recalled from secondary literature that I could not verify on a fetched page.*

*Compiled 2026-10-16. Units follow the original sources (in, lb, ft/s unless stated). SI equivalents are given where useful. 1 in = 25.4 mm, 1 lb = 0.45359 kg, 1 ft/s = 0.3048 m/s.*

---

## 0. Headline findings

1. **The navweaps US 16"/50 penetration table is the Thompson F-formula with F_STD, applied directly.** I reproduced every side- and deck-armour value in the Mk 8 AP table to within ±0.07" by using obliquity = angle of fall for the belt and 90° − angle of fall for the deck (§6.2). The table is labelled "USN Empirical Formula for Armor Penetration", and the match shows that this formula is Thompson's F-formula ([navweaps WNUS 16"/50 Mk7](https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php); [Okun, Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)). The Garzke & Dulin power law the project already holds is only a 0°, muzzle-velocity curve fit to the same family of numbers ([navweaps tech-109](https://www.navweaps.com/index_tech/tech-109.php)). **[INFERRED from calculation]**
2. **De Marre in Okun's normalised form is verified against a 1937 USN textbook.** It reproduces both worked problems in *Naval Ordnance* (1937) to within 0.5%: wrought iron 23.1" (formula gives 23.2") and nickel steel 15.77" (formula gives 15.77") ([Naval Ordnance 1937 ch. XII](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-B.html)). **[INFERRED from calculation]**
3. **Exponents shift with T/D because the failure mode shifts.** Thin plates dish and petal. Medium plates (≈0.1–0.3 cal) show T rising roughly linearly with V. Thick plates fail by ductile hole enlargement, where energy is close to proportional to thickness. Okun's M79 energy-density curve quantifies this (§2.3) ([Okun M79 doc](https://navweaps.com/index_nathan/M79apdoc.php)).
4. **There is no single "penetration".** The USN Navy Ballistic Limit (NBL) means the whole projectile gets through, or 80% of its body weight if broken. The Army limit means a pinhole or crack, which can be 25–40% lower in velocity for small arms. The navweaps tables also use a separate "effective" limit: shell intact, fuze working ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php); [Zener 1942, ADA953652](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf)).
5. **Scatter:** a single ballistic-limit datum carries a probable error of about ±2.5%. Plate-to-plate quality varies by about ±10%. Real accepted plates were scored from about 82% to 113% of the standard NBL (§7).

---

## 1. Definitions: what "penetration" means

### 1.1 Ballistic limit (limit velocity) variants

| Term | Criterion | Source |
|---|---|---|
| **Ballistic limit / limit velocity (generic USN)** | "Striking velocity that lets a projectile pass fully through a plate and exit with zero residual velocity"; called the plate's true penetration resistance | *Naval Ordnance and Gunnery* Vol. 1 (NavPers 10797-A, 1957) §4B5, [Slover transcription](https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html) |
| **Navy Ballistic Limit (NBL)** | Complete penetration: the base gets through (perforation). An undamaged shell must put 100% of its body weight through, ignoring cap and windscreen. A broken shell must put 80% of its body weight through. The shell's condition afterwards is *undefined* | [Okun Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php); [navweaps Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php); [Okun HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php) |
| **NPG "limit velocity" (1947)** | "Minimum velocity required for the major portion of the projectile to pass completely through the plate". CP = major portion through; Inc. = major portion rejected | Glancy, NPG Memo Rpt 1-47 (1947), [ADA955277](https://apps.dtic.mil/sti/tr/pdf/ADA955277.pdf) |
| **Army Ballistic Limit** | Nose through, or a through-crack. Zener (1942): "the velocity at which a small pinhole forms" | [Okun Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php); [Zener 1942](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf) |
| **Protection Ballistic Limit** | Complete penetration is scored when a projectile or armour fragment perforates a thin witness plate behind the target. The usual MIL-STD-662 witness is 0.020" 2024-T3 aluminium, 6" behind the armour, with light passing through the hole | [MIL-STD-662F (18 Dec 1997), "V50 Ballistic Test for Armor"](https://everyspec.com/MIL-STD/MIL-STD-0500-0699/MIL-STD-662F_6718/). **[UNCERTAIN]** The witness-plate details are from my recollection of the standard's §3; the index page I fetched did not reproduce them |
| **V50** | Velocity at which the probability of complete penetration (by whichever criterion is chosen) is 50%. Computed as the mean of equal numbers of the highest partial and lowest complete penetrations within a velocity spread | [Wikipedia, Ballistic limit](https://en.wikipedia.org/wiki/Ballistic_limit); [MIL-STD-662F](https://everyspec.com/MIL-STD/MIL-STD-0500-0699/MIL-STD-662F_6718/) |
| **Holing limit (H / HBL)** | The shell cuts a caliber-wide hole, but not enough body weight passes to meet the NBL. Fragments can pass | [Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php); [Okun apShellVsArmor](http://www.navweaps.com/index_nathan/apShellVsArmor.php) |
| **Partial penetration (PP)** | The forward half of the shell goes through and the rear half is rejected. Above 45° obliquity, the nose and upper body may be rejected while the broken lower body penetrates | [Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php); 1957 textbook: "projectile breaks up and only part passes through" ([Slover](https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html)) |
| **Effective limit (EFF / EBL)** | The shell "usually keeps its explosive cavity, seated base plug, and working fuze". This limit is at or above the NBL/holing limit, or may be impossible at any velocity | [Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php) |
| **Krupp "Effective intact" limit** | The German Krupp tables gave, per projectile, both an NBL (often for a broken projectile) and an "Effective" intact limit | [Okun Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| **True / Energy / Nose-first NBL (Okun M79)** | *True NBL* is the minimum V_s for complete penetration. *Energy NBL* is a smaller, imaginary limit used above 45° to compute residual velocity and exit angle. *Nose-first NBL* is the limit for nose-first passage when base-first passage is possible | [Okun M79 doc](https://navweaps.com/index_nathan/M79apdoc.php) |

**"No-Break limit" (Okun):** I found no fetched source defining a "No-Break limit" by that name. The nearest documented concepts are the EFF limit and Okun's "intact" limit. In his face-hardened work Okun also uses a shatter threshold, which defaults to a minimum plate thickness of 0.66 cal at normal ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)). **[UNCERTAIN]**

**Magnitude of the Navy–Army gap.** For cal .50 AP against 3/8"–1" homogeneous plate, Zener fitted V_Army = V₁(e/d)^0.785 + C(1−cos θ) with V₁ ≈ 1150–1180 ft/s, and V_Navy = V₀(e/d)^0.50·f(θ) with V₀ ≈ 1600–1700 ft/s, both at e/d = 1. At e/d = 1 the Army limit is therefore about 28–30% below the Navy limit ([ADA953652](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf); OCR-degraded). The different exponents show the gap changes with T/D. **[UNCERTAIN on exact digits]**

### 1.2 How navies scored tests

- **USN acceptance (1937).** Armour was grouped in lots of 600–1,200 tons. One plate per group went to the Proving Ground and took two major-calibre AP rounds at normal, at velocities specified by gauge and projectile. If it passed, the whole group was accepted. If it failed, the group was rejected for retreatment. The ballistic limit was found by raising velocity "until penetration is just or nearly secured", often within one or two extra shots ([Naval Ordnance 1937 §1212](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-B.html)).
- **Three USN methods for finding the limit (1957).** (a) *Bracket*: average the closest complete and incomplete velocities. (b) *Residual velocity*: fire one round above the limit, measure V_r, and back-compute the limit from "established relationships". (c) *Penetration depth*: fire below the limit and back-compute ([1957 §4B5](https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html)).
- **Percent of standard.** NPG expressed results as "% Sk. 78841", a percentage of an empirical standard expression from a 1931 analysis. The report notes "a plate of average quality will necessarily have a limit of 100%" ([Glancy 1947](https://apps.dtic.mil/sti/tr/pdf/ADA955277.pdf)). Okun gives a worked example of "% NBL" using F_STD: with an F_STD NBL of 2000 ft/s, test shots at 1950 and 2150 ft/s are 97.5% and 107.5% ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)). **[INFERRED]** "Sketch 78841" is very probably the BuOrd document that defines Thompson's F_STD, which was fitted c.1930–31.
- **Bracket width.** Zener's cal .50 limits were each bracketed by two shots no more than 50 ft/s apart ([ADA953652](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf)).

### 1.3 What the navweaps tables mean

- **Belt (face-hardened) figures** come from Okun's FACEHARD v5.0, against British cemented armour. **Horizontal homogeneous** figures come from Okun's M79APCLC. That model is "more accurate for British and German shells than for Japanese and American ones". It assumes a 90° target angle, intact cap and windscreen, and new-gun MV ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)).
- **The figures are ballistic limits.** The tabulated thickness is the plate that the shell just defeats at that velocity, i.e. NBL-type: the shell gets through, but its condition after penetration is not defined ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)). Garzke & Dulin "maximum thickness" is not defined by the source. Tech-109 *assumes* it means the shell still detonates properly ([tech-109](https://www.navweaps.com/index_tech/tech-109.php)). **[UNCERTAIN]**
- **US gun pages** (e.g. 16"/50) print the "USN Empirical Formula" table. It reproduces exactly as Thompson F_STD with obliquity equal to the angle of fall (§6.2). It is therefore an NBL against standard Class B/STS-quality homogeneous plate.
- **Nose-fuzed HE.** These shells penetrate about 0.2 cal of homogeneous plate (0.3 cal of face-hardened), mostly by burster action ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)).

---

## 2. Physics of failure in ductile plate

### 2.1 Failure modes

| Mode | When it dominates | Sources |
|---|---|---|
| **Dishing** (plate bends and stretches as a membrane, then tears) | Thin targets, thinner than the projectile diameter. Also blunt projectiles at low velocity | Woodward, MRL-R-1036 (1986), [ADA180185](https://apps.dtic.mil/sti/pdfs/ADA180185.pdf); Akyürek, *Def. Sci. J.* 72(3) 2022, [DRDO](https://publications.drdo.gov.in/ojs/index.php/dsj/article/download/17422/7763/69226) |
| **Petalling** (radial cracks, then rear-bent petals) | Sharp or ogival noses on thin plates. Akyürek gives b/d < 0.1, with V_s near V₀. Bending is modelled with plastic hinges and tearing with Mode I fracture (Landkof & Goldsmith 1985) | [Akyürek 2022](https://publications.drdo.gov.in/ojs/index.php/dsj/article/download/17422/7763/69226); [Petalling review, utm.my](https://journals.utm.my/aej/article/download/15461/6970/47245) |
| **Plugging / shear** (cylindrical plug sheared out; adiabatic shear bands in high-strength plate) | Blunt or flat-nosed projectiles, intermediate-to-thick plates, V near the limit. Higher thickness and strength push the mode from shear plugging toward *adiabatic* shear plugging. Thin plates separate plugs by tensile fracture, thick plates and higher velocities by shear | [Akyürek](https://publications.drdo.gov.in/ojs/index.php/dsj/article/download/17422/7763/69226); [Eng. Trans. 60(1) 2012](https://et.ippt.pan.pl/index.php/et/article/view/129); [Woodward](https://apps.dtic.mil/sti/pdfs/ADA180185.pdf) |
| **Ductile hole enlargement** (material pushed radially aside, no plug) | Pointed noses, thick plates (b/d > 0.1). Analysed with spherical/cylindrical cavity-expansion theory | [Akyürek](https://publications.drdo.gov.in/ojs/index.php/dsj/article/download/17422/7763/69226); [Woodward](https://apps.dtic.mil/sti/pdfs/ADA180185.pdf) |
| **Discing / spall / scabbing** | Brittle or over-hard plate, and high-velocity stress-wave effects. "Fully hardened plate may crack and eject a plug, followed by the shell" | Backman & Goldsmith, *Int. J. Impact Eng.* 16 (1978) 1–99 (cited by the above, not fetched); [navalgazing Armor pt 2](https://www.navalgazing.net/Armor-Part-2) |
| **Projectile failure** | Bending (offset), upset (compression), and breakage. Nose damage matters most, then body, then base. Okun says a shattered hardened projectile roughly *doubles* the required striking velocity | [Okun Miscarmr §7](http://www.navweaps.com/index_nathan/Miscarmr.php); [HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php) |

**Energy comparison (Woodward 1986).** Simple energy estimates for each mode are ductile hole growth (Taylor), dishing (stretching plus bending work), and plug shear. Plugging "greatly underestimates" the critical velocity. Experiments fall between the ductile-hole and dishing solutions, and dishing is the most consistent with experiment for thin plates ([ADA180185](https://apps.dtic.mil/sti/pdfs/ADA180185.pdf)). Small-arms data show the same thing: Navy perforation energy varies about linearly with thickness ([Zener](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf)).

**Impact zone (1937).** A normal impact affects a circle about 3 calibres across. At 15° obliquity the affected area is about 3 cal wide and 4–5 cal long ([Naval Ordnance 1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)). This sets the edge-effect distance: Okun's quality loss starts within 1.5 cal of an edge ([Miscarmr §4](http://www.navweaps.com/index_nathan/Miscarmr.php)).

### 2.2 Why the exponent changes with T/D

All the classical formulas share the form T/D = k·D^d·[(W/D³)V²]^n ([Okun Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
- **n = 1:** penetration ∝ energy. Each layer resists independently, as in hole boring through thick plate.
- **n = 0.5:** T ∝ V. The whole plate resists from the start, as in plugging or petalling.
- **Pattern by thickness:** "very thin plates have k > 1.0, medium plates petal (k near 0.5), and thick plates are bored through (k near 1.0)" ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
- **De Marre's 0.71429 is a mid-range compromise.** It is good for 0.1–0.75 cal. Above 0.75 cal the exponent rises toward 1.0, and below 0.1 cal it exceeds 1.0 (same source).
- **Equivalent energy form.** De Marre is E ∝ D^1.5·t^1.4 and Krupp is E ∝ D^(5/3)·t^(4/3). In the general form E = C·D^a·t^b, a+b ≈ 3 ([Imperial College lecture](https://www.phase-trans.msm.cam.ac.uk/2015/Lach/Lecture_imperial_1.pdf)). I checked that the De Marre algebra gives D^1.5 t^1.4. **[INFERRED]**
- **Single-term formulas fail when the failure mode changes with thickness.** The project already records this from Okun.

### 2.3 Okun's M79 energy-density curve: the best quantitative T/D map

Okun defines AED = 0.01978·(W/D³)·V_s²/(T/D) as kinetic energy per minimum hole volume, and NED = AED·cos²(Ob). The curve for the standard M79 3" AP shot (1.667-CRH tangent ogive) at the NBL is ([M79 doc](https://navweaps.com/index_nathan/M79apdoc.php)):

| T/D (cal) | NED at NBL | Regime |
|---|---|---|
| 0 | ≈ 5,800 | membrane / dishing |
| 0.01 | ≈ 4,200 | dishing → petalling |
| 0.015 | ≈ 4,020 (minimum) | |
| 0.085 | ≈ 5,700 | start of "linear" regime (T ∝ √KE, i.e. T ∝ V) |
| 0.275 | ≈ 16,300 | end of linear regime |
| 0.35 | ≈ 20,400 | |
| 0.465 | ≈ 22,900 | |
| 3.457 | 38,936 | thick hole-boring (at the 3,500 ft/s cap) |

**[INFERRED]** A constant NED would mean energy ∝ volume, i.e. n = 1. A rising NED means each extra calibre of thickness costs more energy per unit volume. Between 0.085 and 0.465 cal, NED rises about fourfold, which is why low-T/D formulas look like T ∝ V. My check: the US 16" Mk 8 at 1,740 ft/s against 20.04" (T/D = 1.25) gives AED ≈ 29,400. That sits plausibly between the 0.465 and 3.457 values.

Other M79 terms ([M79 doc](https://navweaps.com/index_nathan/M79apdoc.php)):
- A sinusoidal correction J applies only for T/D ≤ 0.075 and for 0.2549 < T/D ≤ 0.47931.
- **Base-first (tumbling) penetration** becomes possible when Ob > 67.5° with T/D ≤ 0.1, or Ob > 65° with 0.1 < T/D < 0.25.
- **Sloped vs thickened plate.** The crossover at which a sloped 0.25-cal plate beats a thickened unsloped plate of equal weight is 51.3°.

### 2.4 Nose shape (CRH)

- Okun converts tangent-ogive noses to an "Equivalent Elliptical Nose": NH = √(CRH − 0.25), α = [(R/a)−1]/[(R/a)−0.5], b/a = √(2R/a − 1), EEN = (b/a)/√(1 − 1.2α + 0.6α²). The M79 (CRH 1.667) has NH = 1.19 cal and EEN = 3.68 cal ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).
- **Size of the effect** ([M79 doc](https://navweaps.com/index_nathan/M79apdoc.php)):
  - Sharper noses (≈7-cal ogive) have about 15% lower NED at the transition points.
  - Blunter noses raise NED, especially against thick plate.
  - An 85%-flat nose has a minimum NED of about 11,000 at T/D ≈ 0.1. A fully flat nose has a minimum of about 8,350 at T/D = 0.55.
  - Near T/D 0.2–0.25, nose shape changes NED by only about 10–15%.
- **Flat noses.** About 0.8 cal of STS is the thickest a 100%-flat projectile can penetrate unshattered at normal. Flat-nose plug thickness is 1.5·(flat%/100) − 0.5 cal ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).
- **Caps on homogeneous plate.** Caps help at obliquity: an "intact cap effect" of up to 8.2% near 55°, and a −12% NBL "cap edge effect" at ≥50°. Caps hurt at normal on thin plate. Plates of ≥0.0805 cal always decap (50/50 at 0.08 cal). Face-hardened plate, or any plate over about 400 BHN, always decaps ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php); [Miscarmr §3](http://www.navweaps.com/index_nathan/Miscarmr.php)).
- **Windscreens and hoods** add an NBL multiplier of (WS%)/5.1 and (Hood%)/5.2 ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).

### 2.5 Plate hardness, tensile strength, elongation: the "optimum hardness" relation

- **Over- versus under-match (WWII Army data, 1½" RHA, normal impact)** ([wargaming.info, data via Okun/Bird](https://wargaming.info/1998/effect-of-hardness-upon-resistance-to-penetration/)). Going from about 245 BHN to about 300 BHN:
  - 20 mm AP (under-matched, T/D = 1.9): limit +7.6% (1,850 → 1,990 ft/s).
  - 37 mm AP (matched, T/D ≈ 1): +3.0%.
  - 57 mm APC (over-matched, T/D = 0.67): −3.9% (1,290 → 1,240 ft/s).

  Rule: when the calibre is smaller than the thickness, resistance rises with hardness. When the calibre is larger than the thickness, resistance falls with hardness. Near 1:1 the effect is weak.
- **Okun (M79):** "Resistance rises with hardness until toughness is used up, plateaus, then falls as brittleness sets in. The plateau moves to softer values as T/D or OB rises" ([M79 doc](https://navweaps.com/index_nathan/M79apdoc.php)). **[UNCERTAIN]** This and the wargaming.info rule disagree on the direction of the T/D effect. The 20/37/57 mm data say thicker (higher T/D) plates favour hardness. Okun's sentence as summarised says the optimum moves *softer* as T/D rises. The British formula below agrees with the 20/37/57 mm data (optimum rises with T/D), so the summarised Okun sentence may be garbled or refer to obliquity only. Verify against the original text.
- **British hardness formula** (used by Okun only as a *ratio* to scale PQF from 225 BHN) ([M79 doc](https://navweaps.com/index_nathan/M79apdoc.php)):
  - B0 = 500 − 160·log10(D/1.5648)
  - B1 = 54000/(B0 − BHN)
  - FOB1 = 11800/(65 − Ob)
  - FOB2 = 43.4·PQF·(T/D)·√BHN / cos(1.5·Ob)
  - NBL = (929 − B1 − FOB1 + FOB2)/√(W/D³), in ft/s
  - It gives 1,561 ft/s for the standard 3" case, against M79's 1,583.

  **[INFERRED]** I swept BHN for D = 3": optimum ≈150 or below at T/D 0.25, 192 at 0.5, 255 at 1.0 and 287 at 1.5. The optimum therefore rises with T/D and falls as D grows, because B0 falls with D. At D = 16" the formula gives absurd absolute values (negative NBL at 300 BHN, T/D 0.25), so it must not be used outside small calibres except as Okun uses it.
- **BRL hardness programme (Grubbs, 1947).** BHN was varied from about 260 to 470. Raising homogeneous plate to about 450 BHN "has a very decided effect" on the ballistic limit, especially at high obliquity. Obliquity raises the homogeneous-plate limit more than the face-hardened-plate limit ([ADA954860](https://apps.dtic.mil/sti/pdfs/ADA954860.pdf)). These were small arms against thin plate, i.e. under-matched cases.
- **Naval homogeneous plate properties.**
  - US Class B (1937): 0.30 C, 3.85 Ni, 1.85 Cr; up to 115,000 psi UTS, 23% elongation in 2", 65% reduction of area ([1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)).
  - NPG 1947 3" Class B: 255–256 BHN, yield 98.6–102.5 ksi, UTS 120.1–121.7 ksi ([ADA955277](https://apps.dtic.mil/sti/tr/pdf/ADA955277.pdf)).
  - Okun standard: 225 BHN, 115 ksi, 25% El., PQF 1.00 ([M79](https://navweaps.com/index_nathan/M79apdoc.php)).
  - Navweaps M79 inputs: British and US Q = 1.0 with 25% El.; Japanese 0.97 with 23% ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)).
  - German Wh: 18% El. ([M79](https://navweaps.com/index_nathan/M79apdoc.php)).
- **Elongation (ductility) scale penalty.** It applies only for D > 8" and %El < 25: NBL(%EL) = NBL·{1 − [1 − √(%El/25)]·(D − 8)/8} ([M79](https://navweaps.com/index_nathan/M79apdoc.php)). **[INFERRED]** Example: German Wh (18%) against a 16" shell gives a factor of 1 − (1 − 0.849)·1 = 0.849, i.e. about 15% lower NBL.
- **Projectile hardness.** A soft area at the nose "probably accounts for the poor performance of the M62 projectiles" ([Glancy 1947](https://apps.dtic.mil/sti/tr/pdf/ADA955277.pdf)). Okun's homogeneous-plate programs take no explicit projectile BHN and assume a non-deforming hardened AP nose. Cap hardness enters only through the shatter multipliers, e.g. 1.4 for 650–680 BHN caps ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).

### 2.6 Scale effects

| Formula | Scale term | Effect from 3" to 16" (same T/D, same W/D³) |
|---|---|---|
| De Marre Ni-steel | D^0.07144 multiplies T/D | +12.7% T/D penetrated **[INFERRED]** |
| De Marre wrought iron | D^0.1542 | +29% |
| Gâvre | D^0.42857 | +105% (Okun suspects a quality drop with thickness at Gâvre) |
| Krupp all-purpose (1930s) | D^0.25 | +52% |
| Thompson F | none (similitude); D effects only appear empirically in fitted F values | 0 |
| Okun M79 | SF = √[1 − 0.04·ln(D/3)] multiplies NBL | NBL −3.4% at 16" **[INFERRED]**, plus the elongation penalty above |

Sources: [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php); [M79](https://navweaps.com/index_nathan/M79apdoc.php). Okun attributes the D^d scaling partly to surface cracking and partly to plate quality falling with thickness ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)). Physically, a size effect arises because fracture (crack and shear-band) energy scales with area while plastic work scales with volume. **[INFERRED]**

### 2.7 Obliquity

- **1937 USN.** The limit velocity varies "about as the fourth power of the secant" (formula 5, the "angle multiplier"). The relation is most reliable from 0–30°, useful for comparison from 30–60°, and different in character from 60–90° ([1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)). **[UNCERTAIN]** sec⁴ is implausibly steep: it gives ×4 at 45°. The formula image was not readable, and the original may be a fractional power.
- **Thompson F.** V_L ∝ F(T/D, Ob)/cos Ob, where F itself varies with Ob² ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
- **De Marre** carries cos³ inside the 0.714 power, i.e. V_L ∝ cos^−1.5. This term was later dropped and C used alone ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
- **Okun M79 multipliers** (M = M′/cos Ob, M′ tabulated):
  - M(42.5°) = 1.192 and M(45°) = 1.252.
  - At 67.5°: M = 3.972 at T/D = 0 and 3.58 at T/D = 0.9.
  - M(80°, T/D 0.25) = 4.722, against sec 80° = 5.759 ([M79](https://navweaps.com/index_nathan/M79apdoc.php)).
- **Small arms.** The cal .50 Navy limit at 20° is about 15% above that at 0°, "about five times what a simple path-length effect predicts", and attributed to core breakup ([Zener](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf)). Navalgazing likewise argues that nose rotation, not path length, drives obliquity effects. A FACEHARD run showed a 45° plate needed a shell about 22% faster than a 0° plate 1.414× as thick ([navalgazing pt 2](https://www.navalgazing.net/Armor-Part-2); that run was for face-hardened plate).

---

## 3. Formula catalogue

Notation: T plate thickness (in), D calibre (in), W projectile weight (lb), V striking velocity (ft/s), Ob obliquity (deg) unless noted.

### 3.1 Wrought-iron era (1860s–1880s)

| Formula | Form | Notes / fit | Source |
|---|---|---|---|
| **Fairbairn** (UK, c.1865) | T/D = 0.0007692·[(W/D³)V²]^0.5 | T ∝ V. Right for thin plates at normal impact or for projectiles that lose penetrating ability | [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| **Tresidder** (UK, early 1870s) | T/D = 0.00003798·(W/D³)^0.5·V^1.5 | Close to average for 0.25–0.75 cal at low Ob with non-deforming shot. The weight exponent is ⅓ of the velocity exponent, which fits hard, brittle plate but not ductile plate with tapered noses. No scale term | [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| **Krupp wrought iron** (DE, early 1870s) | T/D = 0.00004643·[(W/D³)V²]^0.75 | Tresidder-like, using total KE | [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| **De Marre wrought iron** (FR, late 1870s) | T/D = 0.00002778·D^0.1542·[(W/D³)V²]^0.7695 | **Verified [INFERRED]:** 10", 500 lb, 1,772 ft/s gives 23.2"; the 1937 USN textbook answer is 23.1" | [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php); [1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-B.html) |
| **Gâvre** (FR, 1870s) | T/D = 0.00002887·D^0.42857·[(W/D³)V²]^0.71429 | Large scale term | [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| **Krupp (Castner, 1896, as reported in USNI)** | p·v² = 5800·a·E² (p kg, v m/s, a and E cm) | Called "the oldest and simplest form of Fairbairn's equation". With W ∝ D³, Krupp and Tresidder reduce to t² ∝ d²v². Stone (1896): hard faces raise the required thickness by about 60% | [USNI Proceedings, July 1896 Professional Notes](https://www.usni.org/magazines/proceedings/1896/july/professional-notes) |

**Equivalences reported in period texts.**
- 10" of treated nickel steel ≈ 13" of iron; 10" all-steel or compound ≈ 12.6" iron; 13" Harvey ≈ 15.5" nickel steel; 11.9" Krupp ≈ 13" Harvey ([1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)).
- Compound ≈ 1.25× iron; nickel steel +5%; Harvey +15–20%; early Krupp +15% over Harvey; best WWII armours up to +40% over early Krupp ([navalgazing pt 1](https://www.navalgazing.net/Armor-Part-1); [1957](https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html)).

### 3.2 De Marre "universal" (nickel steel, c.1890) and its quality coefficient

**Normalised form:**

T/D = 0.00005021·D^0.07144·[(W/D³)(V/C)²·cos³Ob]^0.71429 ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php))

**Velocity (classic) form [INFERRED algebra]:**

V_L = 1021·C·D^0.75·T^0.7 / (W^0.5·cos^1.5 Ob)

The constant is (5.021×10⁻⁵)^−0.7 = 1021.99. Check: 10", 500 lb, T = 15.77" gives 1,770 ft/s; the textbook gives 1,772.

- **Verification.** *Naval Ordnance* 1937 Problem II (10", 500 lb, 1,772 ft/s, nickel steel) answers 15.77". The formula with C = 1 gives 15.772" ([1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)). **[INFERRED from calculation]**
- **Validity.**
  - USN 1937: most accurate for V = 1,400–2,000 ft/s and D/T = 0.7–1.2 (T/D ≈ 0.83–1.43); "erratic or misleading" outside ([1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-B.html)).
  - Okun: good for 0.1–0.75 cal with non-deforming pointed shot at fixed low Ob, usually ≤30° ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
  - **[UNCERTAIN]** The two windows hardly overlap. The 1937 text concerns AP against face-hardened plate around 1 cal; Okun's concerns ductile plate.
- **C values (de Marre coefficient / "factor of performance").**
  - Reference 1890 Ni-steel = 1.0. Later Cr-Ni homogeneous plate (STS) has normal-impact C ≈ 1.2–1.25 ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
  - Face-hardened, from 1937 problems: K = 1.23 (6" AP vs Class A); a 12" KC plate gave 1.1165 against an 870-lb shot; a 14" Class A gave ≈1.108; Harvey:Krupp = 1:1.216 ([1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)).
  - **[INFERRED] C implied by the USN 16" table:** fitting navweaps 0°-equivalent values for the 2,700-lb 16" APC needs C ≈ 1.34–1.39 against Class B. That is above Okun's 1.2–1.25, and it varies with V because the exponent is wrong for T/D ≈ 1.2–2.0, the regime where Okun says n climbs toward 1. Treat C as T/D-dependent if De Marre is used at all.
- **Jacob de Marre with K.** The tank-archives version is used relative to a reference shell. It "works best when a similar shell is chosen" ([tankarchives](https://www.tankarchives.com/2014/10/penetration-equations.html)). This relative use is the *safest* use of De Marre:

  T₂ = T₁·(D₂/D₁)^0.0714·…

  **[INFERRED]** In practice, compute the ratio of the bracketed terms between two shells.

### 3.3 Krupp formulas

- **Krupp KC vs uncapped AP (c.1895):** T/D = 0.0006659·[(W/D³)V²]^0.5 ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
- **Krupp "all-purpose" (1930s, normal impact only):** T/D = 0.30386·D^0.25·[(W/D³)(V/C)²]^0.625 ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
  - C ≈ 525 for mild steel with unbroken APC; 655–694 for Wh homogeneous; up to 804 for the weakest shells against KC n/A.
  - **Table of Wh (intact) C, from G.Kdos.100 (1940):** 690 (15 cm L/3.7), 660 (20.3 cm Hipper), 668/665 (28 cm), 658 (38 cm Bismarck), 663 (British 15"), 655 (British 16") (same source).
  - C barely changes with calibre for Wh, i.e. the D^0.25 term absorbs the scale effect.
  - Oblique impact used per-projectile tables giving both an NBL and an "Effective" intact limit.
- **[INFERRED] 16"/2,700 lb at 1,740 ft/s:** C = 660 gives 25.2", C = 690 gives 23.8", against the USN 0°-equivalent 21.5–21.9". This is consistent with Wh being somewhat weaker than Class B for big shells (the 18% elongation penalty) plus different shells.
- **Krupp energy form:** E ∝ D^(5/3)·t^(4/3) ([Imperial lecture](https://www.phase-trans.msm.cam.ac.uk/2015/Lach/Lecture_imperial_1.pdf)).
- **Krupp in decimetres (Soviet usage):** B (dm) with K ≈ 2400 for "standard quality armour used in Soviet calculations" ([tankarchives](https://www.tankarchives.com/2014/10/penetration-equations.html)). The form was image-only and not recovered. **[UNCERTAIN]**

### 3.4 Thompson F-formula (USN, 1930): the key US formula

**Forms:**

T/D = 1728.04·(W/D³)·[(V/F)·cos Ob]²

V_L = 0.024056·D·(T/W)^0.5·F/cos Ob ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php))

- NPG 1947 wrote it as F = 41.7·M^½·V_L·cos θ/(…e, d…) ([Glancy](https://apps.dtic.mil/sti/tr/pdf/ADA955277.pdf)). **[INFERRED]** 1/0.024056 = 41.57, so this is consistent.
- **Standard plate (F_STD), fitted to Class B and STS hit by the 260-lb 8" Mk 11 Mod 1 APC at 0–75°:**

  F_STD = 6·[(T/D) − 0.45]·[Ob² + 2000] + 40,000

  ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php); same expression in [britUSGermanArmorTests](https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php))
  - **[INFERRED]** At Ob = 0: F = 12,000·(T/D) + 34,600. Thicker plates need a disproportionately higher V, since F rises with T/D. That is the physical "exponent climbs toward 1" effect, built in as a linear F(T/D). For T/D < 0.45, F *falls* as obliquity rises, which partly offsets the 1/cos term for thin decks.
- **Character.** It comes from Thompson's similitude theory and conservation laws. It has no scale term, uses total KE, and is pure n = 1 when F is fixed. Okun warns that similitude breaks down when sound-speed effects matter, and that F_STD was misapplied to face-hardened plate for comparisons. Hershey's NPG analysis ran until the program moved to the Army in 1955 ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
- **Typical "F values per plate type".** Results were usually given as % of F_STD NBL rather than raw F.
  - German plates tested at NPG 1946–47 came in at 82% to 113% of NBL ([britUSGermanArmorTests](https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php)).
  - Example NBLs: 1,630 ft/s for a 7.6" US Class A; 1,793 ft/s for an 8.5" KC (8" shell); 2,256 ft/s for a 17.5" Class B (14" shell) (same source).
  - **[UNCERTAIN]** Reliable raw F tables per plate type (e.g. "F ≈ 50,000 for STS") were not found on fetched pages. Model plate quality as a multiplier on F (equivalently on NBL).
- **Accuracy.** The fit is "rather disappointing" if a single constant s is used, with RMSD 1.38". With F_STD it reproduces the navweaps table to ±0.07" (§6.2) ([tech-109](https://www.navweaps.com/index_tech/tech-109.php)). Against the M79 3" shot vs 3" STS at 0°, F_STD gives 1,504 ft/s and Okun's M79 gives 1,583 ft/s, a 5% difference attributable to nose shape **[INFERRED]**.

### 3.5 Garzke & Dulin fit (project already has it)

T = 0.0004689·W^0.55·D^−0.65·V^1.1, fitted to 14 US-style table values at range 0 and 0°, for APC vs Class B, D = 11.1–18.1". It fits its own data to ±0.01" ([tech-109](https://www.navweaps.com/index_tech/tech-109.php)).

**[INFERRED]** It is effectively a power-law fit of F_STD at 0° over T/D ≈ 1.2–2.2.
- It must *not* be used at range with a simple cos factor: it overshoots the table by 6–28% at 15–45° (§6.2).
- At 0° it gives 21.9" at 1,740 ft/s against F_STD's 21.5" at 0°. The navweaps 20.04" is at 14.9°.

### 3.6 Davis (USN Harvey) formulas, for completeness

- Uncapped vs Harvey: T/D = 0.00003466·D^0.3333·[(W/D³)V²]^0.6667.
- Capped: T/D = 0.00008582·D^0.25·[(W/D³)V²]^0.625. Equal to Krupp all-purpose with C = 690.6 ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php)).

### 3.7 British formulas

- **Tresidder** (§3.1). Tresidder also held an armour-hardening patent (US 529,050), relevant to the face-hardened file ([Google Patents](https://patents.google.com/patent/US529050)).
- **WWII British homogeneous-plate formula with explicit BHN** (§2.5) ([M79](https://navweaps.com/index_nathan/M79apdoc.php)). Its pedigree (which Admiralty or Ordnance Board document) is not given. **[UNCERTAIN]**
- British 1948 Admiralty meeting comparisons are in [britUSGermanArmorTests](https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php).

### 3.8 Japanese formula

No IJN penetration formula was found in fetched sources. The navweaps homogeneous tables model Japanese plate with Q = 0.97 and 23% elongation in M79 ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)). **[UNCERTAIN / gap]**

### 3.9 Okun M79APCLC / HCWCALC (modern reconstruction of NPG homogeneous data)

**Structure** ([M79 doc](https://navweaps.com/index_nathan/M79apdoc.php); [HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)):

NBL = NC·J·SF·M(Ob,T/D)·[PQF·(T/D)]^TEXP / √(W/D³)

- NC and TEXP come from a piecewise T/D table that was not published on the page. **[gap]** It can be approximated from the NED table in §2.3.
- **Inputs:** W, D, T, Ob (0–80°), V_s (≤3,500 ft/s), PQF (pre-adjusted for BHN), %El, windscreen %, hood %, cap % and type, optional user NBL.
- **Standard projectile:** 15-lb 3" M79 AP shot, 3 cal long, CRH 1.667, W/D³ = 0.556. Valid for W/D³ 0.148–0.89 and length 1–4 cal.
- **Data:** Hershey's NPG Dahlgren tests of 1942–55, M79 against STS and Class B; G.Kdos.100 for Wh; Thompson F for graphing.
- **Accuracy:** described by the author as "preliminary" and "ballpark". No error bounds are published.
- **PQF definition:** equivalent stopping power. A plate needing 1.2" where STS needs 1" has PQF = 1/1.1 ≈ 0.909. **[UNCERTAIN]** 1/1.2 would be 0.833; the source's 1/1.1 is presumably a typo or a sqrt-type definition.
- **Excludes:** projectile damage, plugs, yaw over 10°. For yaw under 10°, add half the yaw to Ob.
- **Navweaps caveat:** more accurate for British and German than for Japanese and US shells ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)).

### 3.10 Army/BRL-era formulas (small calibre and fragments; use for structure, not constants)

- **Zener (Watertown 1942, cal .50 AP vs RHA 3/8"–1"):** V_N = V₀(e/d)^0.50·f(θ), with f(20°) ≈ 1.15 and f(30°) ≈ 1.30 (≈ cos⁻²θ). V_A = V₁(e/d)^0.785 + C(1 − cos θ). V₀ = 1,600–1,700 ft/s across the BHN bands 260–280, 300–310 and 330–390 ([ADA953652](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf)). Navy energy is roughly linear in thickness.
- **Lambert (BRL 1978), as given by Zook et al. BRL-MR-3960 (1992):** constant 4000 for RHA and 1750 for aluminium; L, D, T in cm, M in g, V_L in m/s. Intended as an a-priori starting estimate, with the first shot about 250 m/s above it ([ADA246922](https://apps.dtic.mil/sti/pdfs/ADA246922.pdf)).
  - The widely quoted form is V_L = 4000·(L/D)^0.15·[f(z)·D³/M]^0.5, with z = (T/D)·sec^0.75 θ and f(z) = z + e^−z − 1. **[UNCERTAIN]** This exact form is from secondary recollection; BRL-MR-3960's OCR was garbled.
  - **[INFERRED]** With this form, the 16"/2,700 lb vs 20.04" at 0° comes out at 861 m/s, against 530 m/s at 14.9° from USN tables. The 3" M79 vs 3" gives 2,393 ft/s against Okun's 1,583. **Do not use Lambert constants for naval APC vs ductile Class B/STS.** They were fitted to long rods and fragments against harder RHA. The f(z) shape (≈z²/2 for small z, ≈z for large z) is, however, a nice smooth n = 1-at-thick / n = 0.5-at-thin interpolant.
- **THOR (Project THOR TR 47, JHU BAL, April 1961), for fragments vs steel** ([AD0322781](https://apps.dtic.mil/sti/pdfs/AD0322781.pdf)):
  - **Residual velocity:** V_r = V_s − 10^c·(eA)^α·m_s^β·(sec θ)^γ·V_s^λ (fps, in, in², grains).
    - Mild homogeneous steel: c = 6.399, α = 0.889, β = −0.945, γ = 1.262, λ = 0.019.
    - Hard homogeneous steel: c = 6.475, same exponents.
    - Face-hardened steel: c = 4.356, α = 0.674, β = −0.791, γ = 0.989, λ = 0.434.
  - **Limit:** V₀ = 10^c1·(eA)^α1·m^β1·(sec θ)^γ1. Mild homogeneous: c1 = 6.523, α1 = 0.906, β1 = −0.963, γ1 = 1.286.
  - **Validity:** e = 0.03–1.0", fragments 5–825 grains, V = 600–12,000 fps, compact fragments only. Some constants are OCR-uncertain.
  - The **residual-mass** equation m_r = m_s − 10^c·(eA)^α·m_s^β·(sec θ)^γ·V_s^λ is also given.
  - **[INFERRED]** THOR's γ ≈ 1.26–1.29 says limit velocity scales as about sec^1.3 θ for fragments, a useful sanity bound against the 1937 "sec⁴".

### 3.11 Residual-velocity laws

- **Recht–Ipson (1963), energy plus momentum with a plug:** V_r = [m/(m + m_p)]·√(V_s² − V_bl²) ([Raguraman et al., LAJSS 7 (2010) 63–76](https://www.lajss.org/index.php/LAJSS/article/download/239/210); Recht & Ipson, *J. Appl. Mech.* 30(3):384–390, 1963, per [arXiv 2207.11314](https://arxiv.org/pdf/2207.11314)).
- **Lambert–Jonas (1976):** V_r = a·(V_s^p − V_bl^p)^(1/p) for V_s > V_bl, else 0. p = 2 is a good approximation, and a is fitted by regression. The energy balance is ½m_P·V_I² = ½(m_res + m_plug)·V_res² + E_plug ([Imperial College lecture](https://www.phase-trans.msm.cam.ac.uk/2015/Lach/Lecture_imperial_1.pdf)). The original is Lambert & Jonas, BRL-R-1852 (1976); not fetched.
  - Example fit (Mg alloy plate): a = 0.916, p = 2.12, R² = 0.999 ([arXiv 2207.11314](https://arxiv.org/pdf/2207.11314)).
  - Lambert–Jonas and Recht–Ipson are "approximately the same" in accuracy ([Ben-Gurion Univ. abstract](https://cris.bgu.ac.il/en/publications/on-the-lambert-jonas-approximation-for-ballistic-impact/)).
- **Okun M79/HCWCALC for naval AP:** V_R = [W/(W + W_P)]·cos(Ob − Ex)·√(V_s² − V_min²), with W_P = 0 for the M79 nose. Above 45°, V_min is the smaller "Energy NBL" ([M79](https://navweaps.com/index_nathan/M79apdoc.php)):
  - E1 = 1 + (Ob − 45)/45
  - E2 = 2·sin Ob·cos Ob
  - E3 = E1/E2
  - E4 = √(2E3 − 1)/E3
  - Energy NBL = E4 × True NBL

  The penetrating portion of a broken shell keeps (penetrator weight ÷ total) of the energy ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).
- **Simple models.**
  - Cylinder punch / plugging: F_shear = (t − x)·πD·τ with τ = 0.5σ_b ([Imperial](https://www.phase-trans.msm.cam.ac.uk/2015/Lach/Lecture_imperial_1.pdf)). It underestimates the limit ([Woodward](https://apps.dtic.mil/sti/pdfs/ADA180185.pdf)).
  - Cavity-expansion: x = mv²/(2A·Y_c) with Y_c ≈ 3σ_y ([panoptesv](http://www.panoptesv.com/RPGs/Equipment/Weapons/Projectile_physics.php)).
  - Raguraman's regression: V_bl = [C·π·σ_m·d·t²/(m·cos²α)]^½, so V_bl ∝ t and energy ∝ t² for plugging-type mild steel ([LAJSS](https://www.lajss.org/index.php/LAJSS/article/download/239/210)).
  - Chen & Li: V_bl = 2·[2kχ(1+η)/√3]^½·√(σ_y/ρ) (same source).

---

## 4. Residual velocity, energy and mass loss for multi-layer stepping

**Recommended stepping law [INFERRED, from §3.11 sources]:**

V_r = a·√(V_s² − V_L²)·cos(Ob − Ex), with a = W/(W + W_plug) ≈ 1 for ogival AP against ductile plate (W_P = 0 in M79), and V_r = 0 if V_s ≤ V_L.

- Above about 45°, use an "Energy NBL" (< true NBL) in the root ([M79](https://navweaps.com/index_nathan/M79apdoc.php)).
- **Mass loss.**
  - Caps: removed by plates ≥ 0.0805 cal, or by any FH or >400 BHN plate.
  - Hoods: knocked off at 0.0805 cal, broken above 0.1 cal.
  - Windscreens: undamaged below 0.015–0.03 cal.
  - A broken shell counts as through at 80% body weight.

  Sources: [HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php); [Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php); [Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php). After decapping, re-enter the next plate with W reduced by the cap weight (often about 10–20% of the shell; e.g. the 6" Mk 35 cap is about 20%, the 76 mm M62 about 13.9%) and with the bare-nose penalty.
- **Plug mass (blunt or shattered noses only).** Flat-nose plug thickness is 1.5·(flat%/100) − 0.5 cal ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)). Plug mass ≈ ρ·(π/4)·D²·t_plug **[INFERRED]**, to be used in Recht–Ipson's a.
- **Spaced versus laminated plates.**
  - Okun spaced: T_eq = (ΣT_i^1.4)^(1/1.4).
  - Laminated (touching): T_lam = (ΣTe + T_spaced)/2, where the first plate's elongation governs ([Miscarmr §2.A](http://www.navweaps.com/index_nathan/Miscarmr.php)).
  - Zener: two separated thin plates outperform one plate of equal total thickness if spaced ≥1 projectile length apart ([ADA953652](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf)).
  - Navalgazing: stacked layers are weaker than a monolithic plate of the same total ([pt 1](https://www.navalgazing.net/Armor-Part-1)).
- **Worked stepping example [INFERRED, Thompson F_STD, a = 1, no deflection].** 16"/2,700 lb at 1,556 ft/s, deck obliquity 53.7° (35,000 yd):
  - 1.5" STS: V_L = 453, so V_r = 1,489 ft/s.
  - Then 5" Class B: V_L = 1,007, so V_r = 1,096 ft/s.
  - For comparison: single 6.5" plate V_L = 1,236; Okun-equivalent 5.65" single plate V_L = 1,105.

  Energy-subtraction stepping is "cheaper" than one monolithic plate (6.5" with V_L 1,236). It nearly reproduces Okun's 1.4-power rule, because with Thompson V_L² ∝ T·F² the stepping is near-additive in T·F². Pick one convention, not both.
- **No usable USN naval-calibre residual-velocity datasets were found.** The 1957 text confirms the USN found limits by measuring V_r above the limit and using "established relationships" ([1957](https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html)).

---

## 5. Thin and deck plates (low T/D, high obliquity)

- **Regime.** Below about 0.1 cal, n exceeds 1 (De Marre fails). The minimum energy density is at T/D ≈ 0.015, and dishing and petalling dominate ([Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php); [M79](https://navweaps.com/index_nathan/M79apdoc.php)).
- **US Class B / STS.**
  - Class B testing began in 1909, at 80° down to about 55° obliquity.
  - Rule of thumb: test angle of attack ≈ 4 × thickness (in); test calibre ≈ 2.5 × T for 4" plates, 2 × T for heavier; valid for plates of 4–9".
  - Plates under 4" were rolled and thicker ones forged.
  - Problem VIII: 5.5" Class B, 14" 1,400-lb shot at 68°, test velocity 1,860.3 ft/s ([1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)).
  - 1957: Class B ≤3" is called STS; Class B "relies on ricochet" ([1957](https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html)).
- **Deck penetration in the navweaps USN tables** = Thompson F_STD with Ob = 90° − angle of fall (§6.2).
- **Okun high-obliquity specifics** ([M79](https://navweaps.com/index_nathan/M79apdoc.php); [HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)):
  - The obliquity multiplier M is non-monotone in T/D: at 67.5° it is 3.97 at T/D 0 and 3.58 at 0.9.
  - Base-first penetration is possible above about 65–67.5° on thin plate.
  - Cap edge effect −12% at ≥50°.
  - Intact-cap bonus up to 8.2% near 55°.
  - Calculations stop above 80°, and V_r, Ex use the Energy NBL above 45°.
- **Fuze activation by thin plate** (relevant to whether a hit "counts") ([Miscarmr §5](http://www.navweaps.com/index_nathan/Miscarmr.php)):

  T_fuze = T_fzmin·D·{(1 + cos 2Ob)/2 + 0.4537·sin^5.7019 Ob}

  with Ob capped at 61°, and T_fzmin = 0.07 (single plate) or 0.05 (spaced-sensitive fuzes).
- **Face-hardened plate is poor as deck.** It is worse at high obliquity, and plates under about 3" are *easier* to penetrate if face-hardened ([navalgazing pt 2](https://www.navalgazing.net/Armor-Part-2)).
- **Bomb and HE contact.** Okun gives T_phe (STS) = 2.576×10⁻²⁰·D·V^5.6084·cos[2(Ob−45°)] + 0.156·D ([Miscarmr §6.E](http://www.navweaps.com/index_nathan/Miscarmr.php)), and fragment stop thicknesses of 0.08/0.095/0.11 cal at 5 cal standoff. These belong in a separate file.

---

## 6. Units sanity and worked examples

### 6.1 Reference shell: US 16"/50 AP Mk 8

D = 16" (406.4 mm); W = 2,700 lb (1,224.7 kg); L = 72.0" (1.829 m); MV 2,500 ft/s (762 m/s). At 20,000 yd: V = 1,740 ft/s (530.4 m/s), angle of fall 14.9°. The navweaps side figure is 20.04" (509 mm) ([WNUS 16"/50](https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php)). W/D³ = 0.659 lb/in³. In SI, m/D³ = 18,240 kg/m³ **[INFERRED]**.

**[INFERRED] SI form of the Thompson F-formula.** Converting the constant 1728.04 (in·lb·ft/s) to SI with consistent normalisation: T/D = 1728.04·(W/D³)[lb/in³]·(V/F)². Simplest practice is to compute in US units, or to convert F to SI-equivalent "F_SI" by keeping V in ft/s. I deliberately keep F in its historical units. Convert at the boundaries: T(m) = T(in)·0.0254, V(ft/s) = V(m/s)/0.3048.

| Formula | Input | Result | navweaps table |
|---|---|---|---|
| Thompson F_STD, Ob 14.9° | 1,740 ft/s | **20.04" (509 mm)**, F = 50,700 | 20.04" |
| Thompson F_STD, Ob 0° | 1,740 ft/s | 21.47" (545 mm) | — |
| Garzke–Dulin (0°) | 1,740 ft/s | 21.89" (556 mm) | — |
| De Marre, C = 1.0, 0° | 1,740 ft/s | 30.98" (787 mm) | — |
| De Marre, C = 1.35, cos³ at 14.9° | 1,740 ft/s | 18.75" (476 mm) | 20.04" |
| Krupp all-purpose, C = 660 (Wh), 0° | 1,740 ft/s | 25.17" (639 mm) | — |
| Lambert (RHA, form uncertain), 0°, T = 20.04" | → V_L | 861 m/s (2,825 ft/s) | 530 m/s |
| Thompson F_STD, 0° | 2,500 ft/s | 32.64" (829 mm) | 32.62" |
| Garzke–Dulin | 2,500 ft/s | 32.61" | 32.62" |

All values are **[INFERRED]** from my script using the cited formulas.

### 6.2 The navweaps US table *is* Thompson F_STD

**Side armour**, Ob = angle of fall:

| V (ft/s) | Ob | navweaps | F_STD | G&D × cos Ob | De Marre C = 1.35 |
|---|---|---|---|---|---|
| 2,280 | 2.5 | 29.39 | 29.41 | 29.44 | 29.62 |
| 2,074 | 5.7 | 26.16 | 26.17 | 26.42 | 25.65 |
| 1,893 | 9.8 | 23.04 | 23.05 | 23.67 | 22.05 |
| 1,740 | 14.9 | 20.04 | 20.04 | 21.15 | 18.75 |
| 1,632 | 21.1 | 17.36 | 17.37 | 19.03 | 15.87 |
| 1,567 | 28.25 | 14.97 | 14.97 | 17.19 | 13.24 |
| 1,556 | 36.27 | 12.97 | 12.92 | 15.61 | 10.84 |
| 1,607 | 45.47 | 11.02 | 11.02 | 14.07 | 8.42 |

**Deck armour**, Ob = 90° − angle of fall:

| V (ft/s) | Ob | navweaps | F_STD |
|---|---|---|---|
| 1,740 | 75.1 | 3.90 | 3.91 |
| 1,567 | 61.75 | 6.65 | 6.66 |
| 1,556 | 53.73 | 8.48 | 8.56 |
| 1,607 | 44.53 | 11.26 | 11.27 |
| 1,686 | 36.75 | 14.05 | 14.07 |

Sources: table values from [WNUS 16"/50](https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php); formula from [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php); computation **[INFERRED]**.

**Conclusions.**
1. The thickness is *actual plate thickness* at that obliquity, not line-of-sight thickness.
2. The table is an F_STD NBL, i.e. 100% standard-quality Class B/STS, shell condition undefined.
3. De Marre's cos³ obliquity term is too strong at 20–45° for this shell.
4. Garzke–Dulin is a 0° muzzle fit only.

The same comparison is a good unit test for any game implementation.

### 6.3 Other checks [INFERRED]

- **14" 1,500-lb APC at 2,000 ft/s (610 m/s), 0°:** F_STD gives 19.85" (504 mm); G&D 20.14"; De Marre (C = 1) 28.66". At 1,600 ft/s: F_STD 15.02", G&D 15.76". No navweaps 14" table was fetched (the page errored), so this is unverified against a table.
- **3" M79 (15 lb) vs 3" STS at 0°:** F_STD NBL is 1,504 ft/s, Okun M79 1,583 ft/s, British BHN formula 1,561 ft/s ([M79](https://navweaps.com/index_nathan/M79apdoc.php)). These agree within about 5%.
- **De Marre textbook checks:** see §3.1 and §3.2.

---

## 7. Statistical scatter

| Evidence | Magnitude | Source |
|---|---|---|
| Probable error of one ballistic-limit datum (cal .50 vs RHA) | ±39 ft/s on ≈1,600 (±2.4%) | [Zener 1942](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf) |
| Mean absolute deviation of formula vs data | 66 ft/s (≈4%); max 180 ft/s (≈11%) | same |
| Standard error of a 20-round BL (cal .30 vs ¼" RHA) | 5 ft/s on 1,222 (0.4%) | [Grubbs 1947](https://apps.dtic.mil/sti/pdfs/ADA954860.pdf) |
| Plate-to-plate quality, accepted heavy plates (German Wh/KC vs F_STD) | 82% to 113% of NBL (about ±15%) | [britUSGermanArmorTests](https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php) |
| Test bracket around a USN limit | 97.5–107.5% NBL example | [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| Bracketing shots in 1937 acceptance | 1,400 (fail) / 1,463 (pass), limit est. 1,420 (±2–3%) | [1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html) |
| Navweaps explicit caveat | "The tables are a probability measure, not absolutes, and identical hits can produce very different results" | [Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php) |
| Shell quality | Shattering roughly doubles the required V. Early Harvey-era tests showed "large scatter" from projectile quality | [HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php); [Hstfrmla](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| Edge effects | Up to 15% quality loss within 1.5 cal of an unsupported edge. Plate edges can be 50–100% thicker locally | [Miscarmr §4](http://www.navweaps.com/index_nathan/Miscarmr.php) |
| Hardness variation | ±5% in BHN can shift the BL by a few % (§2.5) | [wargaming.info](https://wargaming.info/1998/effect-of-hardness-upon-resistance-to-penetration/) |

**Sources of randomness:**
1. Plate heat-treatment and quality, which varies within a lot and with thickness (Okun, Gâvre note).
2. Projectile manufacture: hardness pattern and soft noses (M62 case).
3. Yaw at impact (Okun: add ½ yaw to Ob).
4. Local geometry: edges, bolts, supports.
5. Striking-velocity scatter (gun wear, charge temperature); out of scope here.
6. The shatter/no-shatter bifurcation near a threshold, which is a step change, not Gaussian.

**[INFERRED] Suggested model:**
- A per-plate quality factor ~ N(1.0, σ = 0.05–0.07) on NBL, drawn once per plate (or per ship and armour batch).
- A per-shot noise ~ N(0, σ = 0.03) on NBL.
- A Bernoulli shatter state where the shell type allows it.

---

## 8. What a model needs from this

### 8.1 Parameters

**Projectile**

| Parameter | Unit | Default / range | Use |
|---|---|---|---|
| D | in / m | — | all |
| W (body, cap, windscreen, hood separately) | lb / kg | cap 10–20% of W; windscreen ≤25%; hood ≤50% | mass after decap; Okun multipliers |
| W/D³ | lb/in³ | 0.15–0.89 (M79 validity); 16" Mk 8 = 0.659 | sectional density |
| Nose CRH / EEN, flat % | cal | CRH 1.667 (EEN 3.68) baseline; ±15% NED for sharper; blunt noses much worse | nose factor |
| Cap type (none / soft / hard) | — | decap at ≥0.0805 cal homogeneous or >400 BHN | obliquity bonus/penalty |
| Shatter threshold | cal at 0° | 0.66 (US Mk 35 cap) | penalty ≈ ×2 V_L when shattered |
| Hardness and quality grade | — | "good" = 1.0, "poor" up to ×1.1–1.3 on V_L | projectile quality factor |

**Plate**

| Parameter | Unit | Default / range |
|---|---|---|
| T | in / m | — |
| Type | — | homogeneous ductile vs face-hardened (separate file) |
| PQF (quality, multiplies NBL) | — | US Class B/STS 1.00; British NCA ≈1.0; Japanese 0.97; Wh ≈0.95–1.0 plus elongation penalty; 1890 Ni-steel ≈0.8 (C 1.2–1.25 ⇒ 1/1.22); mild steel ≈0.65–0.75 **[INFERRED from Krupp C 525 vs 660–690]**; wrought iron ≈0.65 (Ni-steel to iron ≈ 15.77/23.1 thickness, about 0.68) |
| %Elongation | % | 25 (US/UK), 23 (Japanese), 18 (German Wh); penalty only for D > 8" |
| BHN | — | 225–260 (Class B/STS). Small hardness shifts: ±3–8% V_L, sign depends on T/D (§2.5) |
| Edge distance | cal | Q loss 0→15% inside 1.5 cal |

**Impact**

| Parameter | Default / range |
|---|---|
| V_s | ≤3,500 ft/s for M79 validity |
| Ob | 0–80°; side Ob ≈ fall angle (+ target angle); deck Ob = 90° − fall |
| Yaw | add ½ yaw to Ob if <10° |

### 8.2 Suggested default formula skeleton [INFERRED]

1. **Core limit.** Thompson form:

   V_L = 0.024056·D·√(T/W)·F(T/D, Ob)/cos Ob

   with F = F_STD·PQF·nose_factor·scale_factor·cap_factor. F_STD reproduces the USN tables exactly. Replace or adjust F_STD with Okun-style NED(T/D) shape corrections:
   - for T/D < 0.1, raise the effective exponent (dishing/petalling);
   - for T/D > 1, check against M79 NED.
2. **Scale factor.** √[1 − 0.04·ln(D/3)] (Okun) multiplied by the elongation penalty for D > 8".
3. **Result classes.** For each hit, report NBL (through), holing (a fraction of V_L, e.g. ≈0.9–0.95 **[INFERRED]**), and an "effective" limit (shell intact and fuze working, e.g. V ≥ ≈1.1·V_L for APC at Ob < 45° **[INFERRED]**). Do not collapse them; navweaps and Krupp both distinguish them.
4. **Residual and stepping.** V_r = √(V_s² − V_L²)·cos(Ob − Ex) (a = 1 for intact ogival AP). Use the Energy-NBL substitution above 45°. Apply decap and mass loss between plates. Choose energy-stepping *or* the Okun 1.4-power equivalent, not both.
5. **Scatter.** Plate PQF ~ N(1, 0.06); per-shot V_L noise ~ N(0, 0.03); shatter as a discrete state.
6. **Validation targets.**
   - §6.2 table: belt and deck, ±0.1".
   - 3" M79 vs 3" STS: 1,583 ft/s (±5%).
   - *Naval Ordnance* 1937 De Marre problems (if using De Marre for the 1890s era: 15.77" Ni-steel, 23.1" wrought iron).
   - German plates at 82–113% of F_STD.

### 8.3 Known gaps

- Okun's NC/TEXP/M′ tables are not published in fetched text.
- The metallurgy page (`metalprpsept2001.php`) could not be fetched (client error), so per-nation BHN, UTS and quality tables come only from secondary pages.
- No IJN formula was found.
- The exact Lambert form and the MIL-STD-662F witness criteria are from recollection.
- The 1937 "sec⁴" obliquity rule is OCR/transcription-suspect.
- The tail of `Miscarmr.php` (projectile damage section) could not be read: the fetch tool refused it.
- There is no USN naval-calibre residual-velocity dataset.
