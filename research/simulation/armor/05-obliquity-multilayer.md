# 05 — Obliquity, Ricochet, Normalisation, Yaw, Decks and Multi-Layer / Spaced Armour
Status: draft    Updated: 2026-10-16    Request: —

*Research inputs for the naval armour-penetration model, covering about 1860–1945. This is not a finished model.*

**Tags.** **[INFERRED]** marks my own derivation or arithmetic from the cited inputs. **[UNCERTAIN]** marks a source that is garbled, contradictory, single-sample or recalled from memory. Most quantitative naval material traces back to Nathan Okun's NavWeaps essays. Those essays are one researcher's empirical reconstructions, not primary BuOrd documents. Where I could, I cross-check them against primary-derived material: O.S. 751, the Watertown Arsenal 1950 report, GKdos 100 via kbismarck, and the 1919 Hood trials.

**Computation script.** I wrote the numbers marked [INFERRED] with a small script. It was a throwaway and is not in the repo; the maintained checks are in `armor_pen_ref.py`.

---

## 0. Verification of what the project already holds

| Project assumption | Status | Evidence |
|---|---|---|
| Spaced plates: `Te_i = Q_i·T_i`, `Tspaced = (ΣTe_i^1.4)^0.71429` | **Confirmed** as Okun's rule. Okun says it is valid for plates of **0.2–1.1 cal** and is "probably as good an approximation as you are likely to get" for a pointed projectile | [Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php); [multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php) |
| Laminated plates = average of sum and spaced | **Confirmed** as Okun's rule, which he calls "split the difference". Okun admits it is his own compromise and not validated. His own M79 model gives *lower* values for 5"+2": **5.55" (14" shell) and 5.98" (8" shell)**, against 6.48" from the averaging rule | [multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php); [Multi-plate PDF](http://www.navweaps.com/index_nathan/Multi-plate_discussion.pdf) |
| Decapping ~0.075–0.12 cal | **Refine.** The threshold depends on cap type and obliquity: Type-1 hard cap **0.0805 cal at 15–45°**, about **0.105 cal at 0°**; Type-2 hard cap **0.155–0.17 cal**. A full table is in §4d | [tech-085](https://www.navweaps.com/index_tech/tech-085.php) |
| Decapping gap 1–3 cal | **Confirmed but conditional.** Gap needed at normal impact is **1.6–3 cal**. It falls linearly to **1 cal at ≥30°**. A cement filler roughly halves it. A gap under about 0.5 cal can leave the plate wrapping the nose like a soft cap | [tech-085](https://www.navweaps.com/index_tech/tech-085.php) |
| Edge effect −15% within 1.5 cal | **Confirmed** (linear loss). Detail and backing rules are in §5 | [Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php) |
| Ricochet 45–60°, normalisation 6–10° | **Game values (WoWS); not historical.** Historical evidence: pointed AP penetrates nose-first up to roughly **65–75°**, and every source treats **80°** as near-certain glance. Real "normalisation" is velocity-dependent: about **2–18°** per plate (§1.5) | [Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php); [M79](http://www.navweaps.com/index_nathan/M79apdoc.php); [tech-085](https://www.navweaps.com/index_tech/tech-085.php) |
| Fitted `cos^1.59` (vertical) and `cos^1.125` (horizontal) | **Consistent with the USN Thompson/FSTD law** [INFERRED]. FSTD gives an effective exponent of **n≈1.5–1.9 for thick plates at 15–45°** and **n≈1.12–1.25 for thin plates at 60–75°** (§1.3). This is a strong check that the project's fit is physically plausible | computed from [Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php) |
| de Marre `cos^(3·0.714)` ≈ cos^2.14 | **Confirmed.** de Marre nickel-steel has the form `T/D = 0.00005021·D^0.07144·[(W/D³)(V/C)²·cos³(Ob)]^0.71429`, so T ∝ cos^2.143. Okun notes Ob "usually restricted to 30°", and later homogeneous practice dropped the cos³ term altogether | [Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php) |

---

## 1. Obliquity physics

### 1.1 Convention and compound obliquity

**Convention.** Obliquity is measured from the plate normal along the centre-of-mass path, with yaw ignored ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php)). German GKdos 100 practice used the complementary angle α = 90° − ω for a vertical plate, so ω = 7° gives α = 83° ([kbismarck GKdos 100](https://kbismarck.com/gkdos100a.html)). **Beware of convention flips when importing German curves.**

**Compound obliquity.** For a shell with fall angle *f*, target angle *a* (90° = beam-on) and plate inclination *i*, the obliquity comes from the vector dot product **[INFERRED]**. A full derivation is in the script.

| Fall angle | TA 90° vertical belt | TA 60° | TA 45° | TA 90°, 19° top-out belt (Iowa) | TA 45°, Iowa belt |
|---|---|---|---|---|---|
| 5° | 5.0° | 30.4° | 45.2° | 24.0° | 50.4° |
| 15° | 15.0° | 33.2° | 46.9° | 34.0° | 55.8° |
| 25° | 25.0° | 38.3° | 50.1° | 44.0° | 62.1° |
| 35° | 35.0° | 44.8° | 54.6° | 54.0° | 68.8° |

For a flat deck, obliquity = 90° − fall, independent of target angle.

USN immune-zone practice assumed a **90° target angle for battleships and 60° for cruisers**. This is reported in reader comments on [navalgazing Armor Part 3](https://www.navalgazing.net/Armor-Part-3) [UNCERTAIN: secondary]. Hoyer's German treatment combines four angles into the "true impact angle": relative bearing, heel, armour inclination and fall angle ([kbismarck Hoyer](https://kbismarck.com/hoyer-warship-armor.html)). **A model should include heel/roll.**

### 1.2 Why resistance rises faster than 1/cos

Sources identify the following mechanisms:

1. **Path length (1/cos).** Okun's M79 obliquity multiplier is written `M(OB,T) = M'(OB,T)/cos(OB)`. The secant is "the geometric thickness increase for an undeflected projectile" ([M79](http://www.navweaps.com/index_nathan/M79apdoc.php)).
2. **Nose rotation and bending.** On oblique contact the nose rotates, which is much less efficient ([navalgazing Armor 2](https://www.navalgazing.net/Armor-Part-2)). Okun gives the bending threshold as falling "slowly at up to 40 degrees or so, but more rapidly above that". Bending forces decrease again at and above 60–65° ([Projectile Damage PDF](https://www.navweaps.com/index_nathan/Projectile%20Damage%20Due%20to%20Impact%20--%20An%20Overview.pdf)).
3. **Biting angle.** Okun defines it as "the highest obliquity where nose-first penetration occurs at the Navy Ballistic Limit".
   - Below the biting angle the nose digs in and ricochet is inhibited. The percentage velocity increase needed is nearly independent of plate thickness.
   - Above it the nose is pushed away and the projectile tries to push sideways. The required energy then "rises sharply with plate thickness".
   - Penetration becomes "essentially impossible above about 75–80°" for pointed projectiles, even against thin plates ([Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php)).
   - No explicit numeric biting angle is published. The M79 M' peak at **65–67.5°** for all T/D is the best proxy [INFERRED] ([M79](http://www.navweaps.com/index_nathan/M79apdoc.php)).
4. **Body breakage.** Middle-body breakage peaks around **45°**. Some high-quality projectiles stayed intact below 40° or above 50° but broke in the 40–60° band. Base slap flattens or splits the base at deflections of 30° or more ([Projectile Damage PDF](https://www.navweaps.com/index_nathan/Projectile%20Damage%20Due%20to%20Impact%20--%20An%20Overview.pdf)).
5. **Thickness-dependent reverse scaling.** GKdos 100 curves show the large-calibre advantage reversing above about **60°**. A 14" AP was observed to penetrate thin decks at high obliquity relatively *worse* than smaller calibres: "smaller calibre penetrates easier at high obliquity than larger cal" (Delcyros, quoted on the [warships1 forum](https://www.tapatalk.com/groups/warships1discussionboards/various-iz-graphs-t34037-s60.html)) [UNCERTAIN: forum].

Okun's own illustration: at 45°, a shell needs about **22% more velocity** than against a plate 1.414× as thick at 0° (equal weight per area) ([navalgazing Armor 2](https://www.navalgazing.net/Armor-Part-2)).

**[INFERRED]** That penalty converts as follows. With T ∝ V^1.1 (the USN empirical exponent, [tech-109](https://www.navweaps.com/index_tech/tech-109.php)), the effective thickness at 45° is ≈ 1.414 × 1.22^1.1 ≈ 1.76T, i.e. about **cos^-1.63**. With de Marre (T ∝ V^1.43) it is ≈ 1.88T, i.e. about **cos^-1.82**.

### 1.3 Published obliquity laws (numbers)

| Law | Form | Effective cosine exponent n (T ∝ cos^n at fixed V) | Source |
|---|---|---|---|
| de Marre (nickel steel, <30°) | `[(V cos^1.5)²]^0.714` | **2.14** | [Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php) |
| Thompson F (USN, Class B/STS, tested 0–75°) | `VL = 0.024056·D·(T/W)^0.5·F/cos(Ob)`, `F_STD = 6·(T/D − 0.45)·(Ob² + 2000) + 40000` (Ob in degrees) | **varies 1.1–1.9**; see the next table | [Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php); FSTD fitted to 8" Mk 11 tests on Class B/STS **0–75°** ([britUSGerman](https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php)) |
| British NCA (homogeneous) | `F_OB2 ∝ (T/D)·√BHN / cos(1.5·Ob)` | cos(1.5·Ob) gives a steeper fall-off. It reaches zero at 60°, so it is only usable well below that | [M79](http://www.navweaps.com/index_nathan/M79apdoc.php) |
| Okun M79 (homogeneous, T/D 0.005–3.5, Ob ≤ 80°) | `NBL ∝ M'(Ob,T/D)/cos(Ob) · (T/D)^TEXP` | Table-driven; see the M' values below | [M79](http://www.navweaps.com/index_nathan/M79apdoc.php) |
| Krupp (pre-WWI) | Normal impact only; a separate oblique table for each projectile | — | [Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php) |

**Thompson/FSTD, computed [INFERRED]: penetrable plate thickness ratio T(Ob)/T(0) at equal striking velocity.** Values are given as ratio (effective n).

| T/D at 0° | 15° | 30° | 45° | 60° | 70° | 75° |
|---|---|---|---|---|---|---|
| 0.1 | 0.961 (1.15) | 0.849 (1.14) | 0.677 (1.13) | 0.459 (1.12) | 0.294 (1.14) | 0.207 (1.16) |
| 0.25 | 0.954 (1.35) | 0.829 (1.31) | 0.647 (1.25) | 0.434 (1.20) | 0.281 (1.18) | 0.201 (1.19) |
| 0.5 | 0.946 (1.59) | 0.805 (1.50) | 0.616 (1.40) | 0.409 (1.29) | 0.267 (1.23) | 0.194 (1.21) |
| 1.0 | 0.937 (1.88) | 0.779 (1.74) | 0.582 (1.56) | 0.382 (1.39) | 0.251 (1.29) | 0.186 (1.24) |

The same law, expressed as ballistic-limit velocity ratio VL(Ob)/VL(0) for a given plate: T/D 0.25 gives **1.12 at 30°, 1.32 at 45°, 1.77 at 60°, 2.47 at 70°**. T/D 1.0 gives **1.23, 1.62, 2.51, 3.94**.

**Takeaway [INFERRED].** The exponent is *not* constant. It is larger for thick plates and low obliquity, and about 1.1–1.25 for thin plates at deck-like obliquities. The project's two fitted exponents (1.59 vertical, 1.125 horizontal) fall naturally out of FSTD. A better single law would be `n(T/D, Ob)`. A simple form is `n ≈ 1.1 + 0.8·(T/D)·cos(Ob)` [INFERRED, rough fit to the table above].

**Okun M79 M' table (homogeneous) [M79](http://www.navweaps.com/index_nathan/M79apdoc.php):**
- At Ob ≤ 45°, M' is independent of thickness. Its minimum is **0.879 at 42.5°** (M = 1.192); at 45°, M' = 0.885 (M = 1.252).
  - So for Ob ≤ 45° the NBL grows *slightly slower* than 1/cos. The "faster than cos" behaviour in thickness terms comes from the thickness exponent TEXP < 1.
- Above 45°, M' rises sharply for very thin and very thick plates:
  - M'(67.5°, T/D→0) = 1.52 (M = 3.97)
  - M'(67.5°, 0.9) = 1.37 (M = 3.58)
  - The low point is near T/D 0.25: M'(65°, 0.25) = 1.094 (M = 2.59)
  - Beyond the peak it falls again: M'(80°, 0.25) = 0.82 (M = 4.72)
- The cross-over angle at which a sloped 0.25-cal plate beats an unsloped thicker plate of equal weight is **51.3°**.

### 1.4 Face-hardened vs homogeneous; capped vs uncapped; nose shape

**Face-hardened (FH) plates and caps:**
- FH plates have a 30% "shatter bonus" in thickness at normal impact, falling to about zero at **~55°**. Above about 55°, shatter *suppresses* ricochet and can make penetration easier ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)).
- The apparent thickness gain from shatter is 20–40% at right angles (30% average), with one example of 27% at 20°. Shatter velocity falls to about **half** the right-angle value at ≥60° ([Projectile Damage PDF](https://www.navweaps.com/index_nathan/Projectile%20Damage%20Due%20to%20Impact%20--%20An%20Overview.pdf)).
- Soft caps tear free at about **15°** and are "virtually useless just over 20°". Hard caps (>300 Brinell; late-war US caps 650–680 BHN) stay effective above 20° ([tech-055](https://www.navweaps.com/index_tech/tech-055.php); [navalgazing Armor 2](https://www.navalgazing.net/Armor-Part-2)).
- The cap-shatter threshold thickness is `T/D = 0.515·cos^0.6265(Ob)`, from USN tests over 0–60°. It is **0.515 cal at 0°, about 0.41 at 45° and about 0.33 at 60°** ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)).

**Decks and FH:**
- FH is worse on decks: it thins the tough back layer and raises the chance of breaking and digging in. FH under about 3" makes penetration *easier* ([navalgazing Armor 2](https://www.navalgazing.net/Armor-Part-2)).
- Bean (in comments) says FH works best at **about 30–55°** ([navalgazing Armor 4](https://www.navalgazing.net/Armor-Part-4)).

**Japanese caps.** The Type 88/91 cap head is about 0.69 cal in diameter and is knocked off above 45°. It "has no function whatsoever at over 45° obliquity against homogeneous armor" ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php); [Type 91 data](https://www.navweaps.com/index_nathan/japType91ProjData.php)).

**Nose shape** ([M79](http://www.navweaps.com/index_nathan/M79apdoc.php)):
- Pointed (~7 cal ogive) noses have up to ~15% lower energy demand at transition points.
- Blunt noses do worse on thick plates and on thin plates, because they struggle to start a tear.
- "At high obliquity the difference between a blunt tapered nose and a pointed nose largely disappears."
- Hoyer gives the German view: a rounded head of radius 1.3 D was effective for 45–60° impacts. Sharp points suit vertical fire and blunt tips suit oblique fire ([kbismarck Hoyer](https://kbismarck.com/hoyer-warship-armor.html)).

### 1.5 Normalisation (turning toward the normal) and deflection

**Okun's M79 exit-angle model (homogeneous, Ob ≤ 45°)** ([M79](http://www.navweaps.com/index_nathan/M79apdoc.php)):

```
VR    = Vs / Vmin
K1    = VR² + VR·sqrt(VR² − 1)
K2    = sin(Ob)·cos(Ob) / K1
K3    = 1 − 4·K2²
DF    = atan[(1 − sqrt(K3)) / (2·K2)]      (deflection toward normal, never negative)
EX    = Ob − DF
Vres  = [W/(W+WP)]·cos(DF)·sqrt(Vs² − Vmin²)   (WP = plug weight; 0 in M79)
```

**Computed deflection [INFERRED, from the formula above].** Values are DF in degrees, with Vres/Vmin in brackets.

| Ob | Vs/Vmin 1.01 | 1.1 | 1.25 | 1.5 | 2.0 | 3.0 |
|---|---|---|---|---|---|---|
| 15° | 12.7 (0.14) | 8.5 (0.45) | 5.8 (0.75) | 3.7 (1.12) | 1.9 (1.73) | 0.8 (2.83) |
| 30° | 24.1 (0.13) | 15.2 (0.44) | 10.1 (0.74) | 6.4 (1.11) | 3.3 (1.73) | 1.4 (2.83) |
| 45° | 29.6 (0.12) | 17.8 (0.44) | 11.8 (0.73) | 7.4 (1.11) | 3.9 (1.73) | 1.6 (2.83) |

So the game's flat 6–10° corresponds to **Vs/Vmin ≈ 1.3–1.6 at 30–45°**. Barely-penetrating hits turn far more; overmatching hits barely turn.

**Above 45° (M79):**
- An "energy NBL" correction applies: `E1 = 1 + (Ob−45)/45`, `E2 = 2·sin·cos`, `E3 = E1/E2`, `E4 = sqrt(2E3 − 1)/E3`, Energy NBL = E4·True NBL.
- Base-first ("sideways") penetration is possible:
  - For T/D ≤ 0.1, it requires Ob > 67.5° and True NBL ≤ Vs < Nose-first NBL.
  - For 0.1 < T/D < 0.25, it requires Ob > 65°.
  - Base-first exit angle: `EX = Ob − DF·[1 + (Ob−45)/45]`.

**Measured deck/belt deflections, 1919 Hood trials.** Okun's analysis of the British trials ([HOOD 1919 PDF](https://www.navweaps.com/index_nathan/HOOD%201919%20Armor%20Tests.pdf)): a 15" shell (inert) at 32° fall (58° obliquity) and about 1350 ft/s on a 2" HT upper deck.
- It struck the main deck at about 56.2° (≈2° turned) and exited the main deck at a 41–46° path (≈11–13° turned on the 3"–6" main deck).
- Total calculated deflection was **11.9° / 15.8° / 17.4°** for main decks of 3" / 5.5" / 6". About **15° was measured** at the 6" main deck.
- All three configurations were completely penetrated, and the shells were recovered essentially intact minus cap and windscreen.

**Deflection after a thick belt can go *away* from the deck normal.** In the same Hood tests a 7" belt hit at 40° and 1430 ft/s gave a computed 634 ft/s exit with **9.22° upward deflection**. The shell then struck the 3" deck at **79.2°**, dented it and **ricocheted** (Test 3). Okun's model could not reproduce Tests 1–2 without dropping the belt quality to about 0.86–0.88 ([HOOD 1919 PDF](https://www.navweaps.com/index_nathan/HOOD%201919%20Armor%20Tests.pdf)).

**Bismarck deck pair (Okun):**
- A 2" upper deck over 3.15" turned the shell about **2.65°** (68.05° → 65.4°). A 2.5" upper deck gave about **3.6°** ([Multi-plate PDF](http://www.navweaps.com/index_nathan/Multi-plate_discussion.pdf)) [UNCERTAIN: extraction garbled which angle is fall and which obliquity; numbers consistent with obliquity ≈ 68°].
- Okun also says the 1.97" weather deck "can deflect a fully penetrating shell at high obliquity downward", i.e. toward the normal, *weakening* the lower deck. A 1.5" deck would not do this to battleship shells ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)).

**Deflections over 45°** are assumed to leave the projectile tumbling or unpredictable ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)).

---

## 2. Ricochet

### 2.1 Naval empirical thresholds

| Statement | Value | Source |
|---|---|---|
| Pointed AP, any thickness: penetration "essentially impossible" | **> 75–80°** | [Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php) |
| Decapping analysis assumes all projectiles glance | **> 80°** | [tech-085](https://www.navweaps.com/index_tech/tech-085.php) |
| M79 program maximum obliquity | 80° | [M79](http://www.navweaps.com/index_nathan/M79apdoc.php) |
| USN FSTD fitted range (Class B/STS, 8" Mk 11) | 0–75° | [britUSGerman](https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php) |
| USN acceptance tests on thin Class B **at 70° and 75°**, showing such hits do penetrate thin decks at modest velocities | e.g. 0.25" at 70° → 660 ft/s; 0.75"/1.00"/1.25"/1.50" at 70° → 885/1045/1205/1365 ft/s (projectile 4); 1.00" at 75° → 1820 ft/s | [O.S. 751](https://navweaps.com/index_nathan/os751BalTests.php) [UNCERTAIN: projectile calibres not extracted; these are acceptance velocities, not ballistic limits] |
| Hood 1919: 15" through 7" belt, then 3" deck at 79.2° and 634 ft/s | **ricochet** (dented plate) | [HOOD 1919](https://www.navweaps.com/index_nathan/HOOD%201919%20Armor%20Tests.pdf) |
| Shattered projectiles resist ricochet ("cannot drag the body through glancing") | 45–60° band | [tech-085](https://www.navweaps.com/index_tech/tech-085.php); [tech-055](https://www.navweaps.com/index_tech/tech-055.php) |

**Interpretation [INFERRED].** Historically, ricochet is a *combined* function of obliquity and Vs/V_BL(Ob). It is not a fixed angle:
- Below about 60°, a full-calibre AP shell either penetrates, embeds or breaks. Ricochet is rare.
- Between 60° and 75°, the outcome is ricochet when Vs is below or near the limit, and penetration (often base-first) when well above it.
- Above about 75–80°, ricochet is nearly certain.

The game's 45–60° ricochet band is far more aggressive than history.

### 2.2 Analytic ricochet criteria (non-naval, for form only)

**Tate (1979), long rods on semi-infinite targets:**

`tan³(β) < (2/3)·(ρp·v²/Yp)·(L/D + D/L)·(1 + ρp/ρt)`

Here β is the grazing angle (90° − Ob), and ricochet occurs when the inequality holds. Rosenberg's variant uses target resistance Rt instead: `tan²β < (ρp·v²/Rt)·(v+u)/(v−u)`. Both were built for semi-infinite targets and "do not carry over to finite-thickness plates" ([LAJSS review](https://www.scielo.br/j/lajss/a/Zz6JLMcBzFbXCngJ7PTPshM/?lang=en)).

**[INFERRED]** Plugging in steel shell values (ρp 7850, Yp ~1.5 GPa, L/D 4, v 500 m/s) gives a critical grazing angle of about 63°. That is absurd for naval plates, because it assumes the target cannot be perforated. **Do not use Tate for naval plates.**

**Better form: BGU/IJIE 2023.** A numerical-plus-experimental study found that the threshold ricochet angle of sharp-nosed rigid projectiles on finite ductile plates is a single function of **V/V_BL(0°)**. It holds across nose shapes, L/D, and steel or aluminium plates, and was checked against 7.62 mm AP data ([Ben-Gurion Univ. record](https://cris.bgu.ac.il/en/publications/on-the-ricochet-of-sharp-nosed-rigid-projectiles-from-metallic-pl/)). The functional form was not available to me [UNCERTAIN]. It supports the model structure `Ob_ric = g(Vs/V_BL0)`, rising with velocity ratio.

**Ricochet off water.** USNI 1921 calculated a 6" shell grazing at 3°40′ rebounding at about the same angle. The authors excluded entry angles below about 22.5° as too complex ([USNI 1921](https://www.usni.org/magazines/proceedings/1921/february/under-water-trajectories)).

### 2.3 Observed ricochet in action

I could not find a primary-sourced count of Bismarck shells "ricocheting off the 50 mm deck" [UNCERTAIN]. Okun's analysis implies that Rodney and KGV's close-range flat-trajectory hits struck the 50 mm Wh upper deck at more than 75° obliquity. Most such hits would have glanced or been deflected rather than penetrating to the armour deck ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)) [INFERRED].

Okun's Bismarck figures:
- The weather deck "can deflect even large shells hitting at shallow angles at close range".
- **A 64° exit angle stops lower-deck penetration**.
- Exit angles ≈ 62.7° allowed penetration at about 15,600 yd.

---

## 3. Deck penetration

### 3.1 How navweaps/USN deck columns are computed

**USN source.** The 16"/50 Mk 8 table on NavWeaps derives from Garzke & Dulin and the "USN Empirical Formula for Armor Penetration", "in substantial agreement with armor penetration curves issued in 1942" ([WNUS 16"/50](http://www.navweaps.com/Weapons/WNUS_16-50_mk7.php)).
- The deck column is evaluated at obliquity = 90° − angle of fall, with **no** stated plate class. It is presumably Class B/STS [UNCERTAIN].
- The USN side-armour formula, fitted by DiGiulian, is `T = 0.0004689·W^0.55·D^-0.65·V^1.1` at 0° on Class B ([tech-109](https://www.navweaps.com/index_tech/tech-109.php)).

**16"/50 Mk 8 (2700 lb), USN** ([WNUS 16"/50](http://www.navweaps.com/Weapons/WNUS_16-50_mk7.php)):

| Range (yd) | Fall | Vs (ft/s) | Deck obliquity [INFERRED] | Side | Deck |
|---|---|---|---|---|---|
| 10,000 | 5.7° | 2,074 | 84.3° | 26.16" | 1.71" |
| 20,000 | 14.9° | 1,740 | 75.1° | 20.04" | 3.90" |
| 25,000 | 21.1° | 1,632 | 68.9° | 17.36" | 5.17" |
| 30,000 | 28.25° | 1,567 | 61.75° | 14.97" | 6.65" |
| 35,000 | 36.27° | 1,556 | 53.7° | 12.97" | 8.48" |
| 40,000 | 45.47° | 1,607 | 44.5° | 11.02" | 11.26" |

**Note.** The USN tables give finite deck penetration even at 84° obliquity. That is above the 75–80° "ricochet regardless" boundary in §2.1. For the model, treat USN deck values below about 15° fall as an **upper bound** that ignores ricochet [INFERRED].

**Okun's own tables.** Okun's NavWeaps tables give PP/H/NL/EFF for Class A and German KC, plus "Deck" columns. Examples:
- 16" Mk 8 at 20 kyd (14.9°): 3.5" and 4.2"
- At 30 kyd: 5.3" and 6.3"
- 14"/50 Mk 16 at 30 kyd (33°): 4.5" and 5.2"

([Penetration_United_States](https://www.navweaps.com/index_nathan/Penetration_United_States.php)) [UNCERTAIN: the extraction placed deck columns under the belt-material headers; probably two deck materials (e.g. US STS vs German Wh)]. Okun's deck values run about 10–20% below the USN figures at short-to-medium range.

**GKdos 100 vs Okun.** For a 38 cm vs a 5.9" Wotan hart deck at 36,000 yd, GKdos 100 curves hold. Okun's formula needs 7.6", and the NavWeaps STS figure is 6.3". The outcome is sensitive to elongation: about 21–22% elongation is the pass/fail line ([warships1 forum](https://www.tapatalk.com/groups/warships1discussionboards/various-iz-graphs-t34037-s60.html)) [UNCERTAIN: forum]. Okun puts Wh elongation at **18–20%**, against **23–25%** for most foreign homogeneous armour (same source). M79 includes an elongation penalty for D > 8": `NBL·{1 − [1 − sqrt(%EL/25)]·(D−8)/8}` ([M79](http://www.navweaps.com/index_nathan/M79apdoc.php)).

### 3.2 Deck materials (quality inputs)

| Material | Data | Source |
|---|---|---|
| Wh n/A (German) | 85–95 kg/mm² UTS, 50–55 yield, 20% strain | [kbismarck](https://kbismarck.com/proteccioni.html) |
| Ww n/A | 65–75 UTS, 38–40 yield, 25% strain. Okun: 1" Ww ≈ 0.95" Wh | [kbismarck](https://kbismarck.com/proteccioni.html); [okun_biz](http://www.combinedfleet.com/okun_biz.htm) |
| St 52 (construction) | 52–64 UTS, 36–38 yield | [kbismarck](https://kbismarck.com/proteccioni.html) |
| M79 standard plate (STS-like) | 225 BHN, 115 ksi UTS, 25% elongation, PQF 1.00 | [M79](http://www.navweaps.com/index_nathan/M79apdoc.php) |
| Backing quality (for FH lamination) | wrought iron 0.6; mild steel to WWI 0.7; HT/Ni steel 0.8; post-WWI HT/Ducol 0.9; STS 1.0 | [Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php) |

### 3.3 Multiple decks: Iowa and Bismarck

**Iowa** ([navalgazing Armor 4](https://www.navalgazing.net/Armor-Part-4)):
- 60 lb (1.5") STS bomb deck, intended to detonate light-case HE bombs and start the fuzes of AP bombs.
- Second deck: **4.75" Class B on 50 lb (1.25") STS**.
- 25 lb (0.625") STS splinter deck 2–3 ft below.
- Navalgazing gives the main armour deck as about 5.625" (4.75 + 0.7×1.25), "somewhat compromised" vs a single plate.

**USN "0.7 rule" — conflicting descriptions [UNCERTAIN].** Okun states the rule as "the upper plate counts as 70%, then add the lower". On that reading 5"-over-2" gives 5.5", and 2"-over-5" gives 6.4" ([Multi-plate PDF](http://www.navweaps.com/index_nathan/Multi-plate_discussion.pdf)). Navalgazing instead discounts the thinner STS layer. The model should parameterise this.

**Iowa equivalents [INFERRED], by Okun's rules:**
- 4.75 + 1.25 spaced = 5.26"; laminated = 5.63".
- Bomb deck + armour deck + splinter deck, all spaced: **(1.5^1.4 + 4.75^1.4 + 1.25^1.4 + 0.625^1.4)^0.714 = 6.08"**. The real value is lower if the bomb deck decaps or normalises the shell.

**Bismarck** ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)):
- 1.97" Wh weather deck + ~0.5" battery deck + 3.15" (80 mm) armour deck → **4.4" effective**. Over the magazines (3.74") → **4.92"**.
- The same metal as one solid deck would give 5.37" and 5.96".
- Okun uses `T_total = (ΣT^1.42857)^0.7`.

**Spaced decks vs solid at 68°, the strongest test datum.** A 14" AP at 68° obliquity perforated a spaced **2"+1"+2" STS** array at **1,250 ft/s**. The through-crack limit for a **solid 5.0"** STS plate was **1,690 ft/s** ([warships1 forum, Delcyros](https://www.tapatalk.com/groups/warships1discussionboards/various-iz-graphs-t34037-s60.html)) [UNCERTAIN: secondary report of a USN test].
- **[INFERRED]** Okun's 1.4-rule predicts Te = 3.71". With V ∝ T^0.7, that gives a velocity ratio of 0.81, i.e. about 1,370 ft/s.
- The observed ratio is **0.74**, so at deck obliquities spaced plates are even *weaker* than the 1.4-rule predicts.
- Plausible cause: each upper plate normalises the shell, so the lower plates are hit at lower obliquity.

### 3.4 Immune zone (IZ)

- The USN introduced the IZ concept in **1929**. The inner edge is set by the belt and the outer edge by the deck ([navalgazing Armor 3](https://www.navalgazing.net/Armor-Part-3)).
- North Carolina studies vs 14"/50:
  - 12.25"/4.5" → 24.1–27 kyd
  - 14"/5.25" → 20.7–30 kyd
  - 12.75"/5.0" → 23.1–29.3 kyd
- Iowa vs 16"/50 2,700 lb: **23.6–27.4 kyd**. Vs 16"/45 2,700 lb: 20.4–26.7 kyd ([navalgazing Armor 4](https://www.navalgazing.net/Armor-Part-4)).
- The Iowa belt is 12.1" Class A sloped 19° (bottom-in). At 0° fall it is about 3% better than a 12.8" vertical belt. At 40° fall it presents about 40% more metal. The outer 60 lb STS hull "should be sufficient to remove AP caps", with an estimated belt gain of 30% or more (same source).
- **German practice (Hoyer).** The "zone of protection" was bounded by **Vg (shattered shell) and Vh (intact shell)** limits, with the Navy focusing on intact penetration. Example: at 0° bearing the zone runs 252–334 hm, roughly doubling at 20°. At 30° bearing the belt-side zone disappears. Calculations rested on Krupp's "Atlas of Penetration" ([kbismarck Hoyer](https://kbismarck.com/hoyer-warship-armor.html)).

---

## 4. Multi-layer armour

### 4a. Spaced plates: test data

**Watertown Arsenal TR 710/930-1, "Spaced Armor", 20 Nov 1950** ([DTIC ADA954865](https://apps.dtic.mil/sti/pdfs/ADA954865.pdf)). Hurlich is cited for it on Wikipedia.

| Configuration | Result |
|---|---|
| Two 1½" RHA in contact vs **separated by 6"**, APC M92, ~15° | BL ~1,980 ft/s in contact → **~1,610 ft/s spaced** (−19%), "because the projectile is deflected onto the second plate at a shallower angle" [UNCERTAIN: scan garbled] |
| 57 mm AP/APC, parallel plates | Projectile yawed and struck the main plate at *lower* effective obliquity. BL fell by about **200 ft/s ≈ 5–7° of obliquity**. Spaced < main plate alone |
| Plates sloped toward each other | 2½" spaced = 3" solid (17% weight saving) |
| ½" skirt 12" ahead of 6" at 30°, 90 mm T33 | Defeat range 2,900 → 350 yd; 6½" spaced ≡ 11½" solid |
| ¾" skirt on 3" at 40°, 90 mm APC | Made the armour **more** vulnerable |
| Summary | Breakup-dominated: saves **30–50%** weight. Decap/yaw only: saves **10–20%**, *only if* yaw raises effective obliquity. Otherwise worse than solid. "Equal-thickness split plates consistently performed worse than a single solid plate." |

**Okun's theory bounds** ([multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php)):
- Under a kinetic-energy (T ∝ V²) rule, spacing makes no difference.
- Under a linear-velocity rule, two spaced 1" plates need 1,414 ft/s vs 2,000 ft/s for a solid 2".
- Real homogeneous armour sits between these, with V exponent 1.43, hence the 1.4/0.714 rule.

### 4b. Laminated (in-contact) plates

**Okun's rule** is Tlam = (Tsum + Tspaced)/2. It is based on US Army .50-cal AP tests against laminated plates at 0–70° ([Multi-plate PDF](http://www.navweaps.com/index_nathan/Multi-plate_discussion.pdf)). His M79 computed 5"+2" at 5.55" (14" shell) and 5.98" (8"), versus 6.48" by the rule and 7" of metal. This suggests the rule is **optimistic for large shells (low T/D layers)** [INFERRED].

**Wartime examples:**
- Hood 1919: 3" laminated HT (0.75/1.25/1") main deck. With 2.5" NCA or 3" MS added on top, it still did not stop a 15" at 58° and 1,350 ft/s ([HOOD 1919](https://www.navweaps.com/index_nathan/HOOD%201919%20Armor%20Tests.pdf)).
- Okun's lamination for FH: face + 0.8 × backing × 0.5 + wood × 0.01 ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)).
- General FH backing sum: `T = Tcement/25 + Twood/100 + Tback·Qback/2 + TeFH`, counting metal backing within 0.3 cal of the last plate ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php)).

**Structural advantage.** Laminated arrays can be "stronger than solid plates against structural failure" through staggered joints, while weaker ballistically ([multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php)).

### 4c. Yaw induced by the first plate

**Small yaw.** Yaw under about 10° can be merged into obliquity as **+½·yaw** ([Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php); [M79](http://www.navweaps.com/index_nathan/M79apdoc.php)).
- Larger yaw is treated as damage and not modelled.
- Okun: yaw is "much more frequently a side-effect of highly-oblique impact"; its effect is statistical and "not always" beneficial ([multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php)).
- Projectiles that pass a decapping plate sideways "may tumble and yaw, making later-plate predictions difficult" ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)).

**Magnitude, small-arms analogue.** Yaw of 8–14° cut V50 from 735 to 685 m/s (−6.8%) on ceramic-faced armour. That is about 0.5–0.85% per degree [INFERRED] ([Argos Press abstract](https://www.argospress.com/articles/2010/the-effect-of-low-angle-yaw-on-the-armour-penetration-of-light-armour-piercing-projectiles)).

**Direction matters.** Watertown found yaw onto a *lower* effective obliquity reduced protection by about 200 ft/s ([DTIC ADA954865](https://apps.dtic.mil/sti/pdfs/ADA954865.pdf)).

### 4d. Decapping and the penalty on the next plate

**Thresholds** ([tech-085](https://www.navweaps.com/index_tech/tech-085.php); [Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php)):

| Cap | Obliquity | Decap thickness (T/D) |
|---|---|---|
| Soft cap/hood | 0–15° | mixed 0.105–0.12; certain above |
| Type 1 hard (most WWII) | 0° | ~0.105 (mixed 0.105–0.12) |
| Type 1 hard | 15–45° | **0.0805** (Miscarmr: 0.08 = 50/50; QA ignored) |
| Type 1 hard | >45° | thin, assumed constant to 80° |
| Type 2 hard (Krupp from 28 cm L/3.2; US 3" Mk 29/30) | 0–45° | mixed 0.155–0.17; certain ≥0.17 |
| Type 2 hard | ~45° | mixed ~0.12–0.14 |
| Any FH or >~400 BHN plate | any | always decaps |
| Hard caps at ≥45° | — | break up at ≥0.14 (one data point) |
| Cap survives (no decap) maximum | 30° → 45° | 0.35 → 0.25 cal; ≥0.25 at 45° always breaks cap |

**Gap needed for a decapped shell to fail on the FH plate:**
- Normal impact: DGN = 3 → 1.6 cal as the decap plate thickens from 0.105 to 0.26 cal. Okun's printed slope coefficient (0.111) is inconsistent with his endpoints; the endpoints imply about 9.0 [UNCERTAIN].
- At obliquity: `DG = DGN − (DGN−1)·Ob/30`, and **1 cal at ≥30°**.
- Soft or broken caps need at least 0.5 cal.
- A cement fill roughly halves the needed gap.
- British 9.2", 14" and 15" APC data: 1 cal was enough at ≥30°.

**Penalty.** Decapping only helps the defender if the uncapped nose then **shatters** on the FH main plate (so the FH shatter bonus is restored), or if the shell fails to penetrate ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)). There are also counter-effects:
- Okun: losing the cap *improves* penetration of later **homogeneous** plates at long range, because the cap's mass and drag are gone ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)).
- Hoyer treats cap loss as roughly self-cancelling in energy terms ([kbismarck Hoyer](https://kbismarck.com/hoyer-warship-armor.html)).

**Model suggestion [INFERRED].** On decap, switch the shell to "uncapped" for the next plate:
- On an FH plate within the effective-gap window, restore the FH shatter bonus: +30% effective thickness at 0°, tapering to 0 at 55°.
- On a homogeneous plate, apply about −5 to −10% to the needed velocity, scaled by cap mass fraction (about 10–16%, average 12%; up to 20% on US 6" Mk 35, per [tech-055](https://www.navweaps.com/index_tech/tech-055.php)).

**Littorio.**
- The decap plate was 70 mm, intended for up to 380 mm shells ([navygeneralboard](https://navygeneralboard.com/?p=1883)); the main belt was 280 mm ([Wikipedia Vittorio Veneto](https://en.wikipedia.org/wiki/Italian_battleship_Vittorio_Veneto)).
- One 1935 scaled mock-up test at 30° with a 32 cm Type II APC "worked perfectly" ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)).
- Navygeneralboard says the plate "should have been slightly thicker".

**[INFERRED] Littorio check against Okun's thresholds.** 70/381 = 0.18 cal, which exceeds the Type-1 threshold and even Type-2 certainty (0.17). The gap is **~250 mm cement-filled, recalled from Garzke & Dulin [UNCERTAIN: not verified this session]**, i.e. 0.66 cal. With the cement-halving rule that is about 1.3 cal air-equivalent, above the 1 cal needed at ≥30°. So the system is sound against 15" at oblique impact, and marginal near normal impact (needs about 1.6–3 cal).

**Iowa outer hull.** 1.5" STS = 0.094 cal vs a 16" shell, and 0.083 vs 18.1". It just clears Type-1 decap at oblique impact [INFERRED].

### 4e. Residual-velocity stepping

**Okun/M79.** `Vres = [W/(W+WP)]·cos(DF)·sqrt(Vs² − Vmin²)`. This is Lambert–Jonas with **p = 2** times a deflection cosine and a plug-mass term ([M79](http://www.navweaps.com/index_nathan/M79apdoc.php)).

**German GKdos 100.** Barbette or belt + glacis combinations use **Vh² = Vh1² + Vh2²**. Worked example: 395 and 245 → 465 m/s ([kbismarck GKdos 100](https://kbismarck.com/gkdos100a.html)) [INFERRED reconstruction from the garbled formula; arithmetic checks]. This is the same energy-subtraction (p = 2) rule.

**Okun's Bismarck estimate.** The 5.93" upper side hull costs **≥20% of striking velocity** for a 16" shell, and removes cap and windscreen ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)).

**Lambert–Jonas fraction Vres/Vs [INFERRED]:**

| Vs/Vbl | 1.05 | 1.2 | 1.5 | 2.0 |
|---|---|---|---|---|
| p = 2 | 0.31 | 0.55 | 0.75 | 0.87 |
| p = 2.5 | 0.42 | 0.67 | 0.84 | 0.93 |

**Hood 1919 check.** 1,350 ft/s → 1,236 ft/s after 2" HT at 58°, then → 1,093 / 714 / 657 ft/s after the 3" / 5.5" / 6" main deck ([HOOD 1919](https://www.navweaps.com/index_nathan/HOOD%201919%20Armor%20Tests.pdf)).

### 4f. Equivalent-thickness statements

| Rule | 5"+2" example | Source |
|---|---|---|
| Sum | 7.0" | — |
| Okun laminated avg | 6.48" | [Multi-plate PDF](http://www.navweaps.com/index_nathan/Multi-plate_discussion.pdf) |
| Okun spaced (1.4) | 5.96" | same |
| M79 computed, 8" / 14" shell | 5.98" / 5.55" | same |
| USN 0.7 rule (upper × 0.7 + lower) | 5.5" (reversed order 6.4") | same |
| Okun's validity window for the 1.4 rule | each plate 0.2–1.1 cal | [multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php) |

**Shot-trap warning.** A 3.65" upper deck over 1.5" means "the 1.5" lower deck has no effect whatsoever". "You obviously *can* make a plate too thick for optimum resistance" ([multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php)).

**Hood-vs-Bismarck deck-split ranges** (15" APC, total deck metal held constant) ([Multi-plate PDF](http://www.navweaps.com/index_nathan/Multi-plate_discussion.pdf)):

| Split | Range at which the deck is penetrated |
|---|---|
| 2+3.15 | 20,003 yd |
| 1+4.15 | 23,330 yd |
| 1.5+3.65 | 21,810 yd |
| 2.5+2.65 | 18,020 yd |
| 3.65+1.5 | 19,795 yd |

**Lesson:** concentrate the metal in the lower plate. A thin top plate is only for fuze-starting and decapping.

### 4g. Shell breakage in the first plate

- A broken shell can still penetrate "through the tear its nose made" at high obliquity ([Hstfrmla](https://www.navweaps.com/index_nathan/Hstfrmla.php)).
- If a shell breaks, the lower portion can keep penetrating through a plate weakened by the nose ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)).
- Shatter "can degrade full-power detonation" ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)).
- Okun's NavWeaps tables distinguish holing (H), Navy limit (NL) and effective, burst-capable (EFF) limits. EFF is about 10–12% below PP/H in thickness, e.g. 16" Mk 8 at 0 yd: Class A PP 37.7", EFF 33.6" ([Penetration_United_States](https://www.navweaps.com/index_nathan/Penetration_United_States.php)).
- Above 45° the nose and upper body may be "rejected", giving partial penetration ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)).
- The O.S. 751 Midvale 20.6" Class A plate at about 30°: a 16" Mk 8 at 2,011 and 2,084 ft/s broke its nose and cracked its lower body. The second hit left the cavity intact ([O.S. 751](https://navweaps.com/index_nathan/os751BalTests.php)).

**Model [INFERRED].** Treat projectile state as {intact-capped, intact-uncapped, nose-damaged (no effective burst), broken (fragments only, ~0.15× intact penetration)}. The 0.15 factor is Okun's fragment penetration factor ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php)).

### 4h. Slope-behind-belt geometry (Bismarck)

**Geometry.** The 110–120 mm Wh slopes run 22° from horizontal and meet the 320 mm KC belt near its lower edge. Obliquity on the slope can reach **68°** ([kbismarck](https://kbismarck.com/proteccioni.html); [okun_biz](http://www.combinedfleet.com/okun_biz.htm)).

**Computed obliquity on a 22° slope, beam-on, for an undeflected shell [INFERRED].** Obliquity = 68° − fall:

| Fall | 0° | 5° | 10° | 15° | 20° | 30° |
|---|---|---|---|---|---|---|
| Ob on slope | 68.0° | 63.0° | 58.0° | 53.0° | 48.0° | 38.0° |

**Adding belt deflection.** After the belt, the shell normalises toward the *belt* normal, i.e. toward horizontal. That *raises* its obliquity on the slope, by DF from the §1.5 table:
- The path angle after the belt is f_exit = f − DF_belt (it can go negative, i.e. rising, as in Hood Test 3).
- Ob_slope = 68° − f_exit, and Ob_slope ≥ 68° means near-certain ricochet or embedment.

So the belt+slope combination is strongest against barely-penetrating, flat-trajectory hits: large DF throws them into ricochet territory on the slope. It is weakest at long range.

**Okun's figures** ([okun_biz](http://www.combinedfleet.com/okun_biz.htm)):
- Through the upper side hull onto the slope: Ob 42° on the 4.33" plate, exit 30.5°, 860 ft/s, effective fall 37.5°.
- The near-side slope can be hit only at fall ≥26° (about 29,000 yd).
- The far-side slope gets an extra 22° and is effectively invulnerable.

**Hoyer.** A "scarp triangle of 300 mm KC and 210 mm Wh" is secure against a 40.6 cm 1,030 kg AP shell ([kbismarck Hoyer](https://kbismarck.com/hoyer-warship-armor.html)).

**GKdos example.** A 350 mm KC belt plus a 70 mm Wh 20° glacis at 100 hm, ω = 7°: Vh = sqrt(395² + 245²) = 465 m/s ([kbismarck GKdos 100](https://kbismarck.com/gkdos100a.html)).

---

## 5. Joints, edges, backing, support, repeated hits

**Edge effect** ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php)):
- Unsupported edge loss is `Qloss = 15%·(1.5 − d)/1.5` for d < 1.5 cal; 5% at d = 1.
- Losses at a corner do not add.

**Support thickness for zero loss:**
- Tfs = 0.75·Te at 0°; 0.3·Te at ≥45°; linear between (0.4·Te at 35°).
- FH plates lose support below Tzs = 0.35·Te·(45 − X)/45.
- Homogeneous plates lose support only at zero backing.
- Worked: 0.1·Te support at 35° gives max loss of 11.25% (homogeneous) and 11.47% (FH).

**Joints:**
- Straight, unsupported joints are single-line failures. Pearl Harbor bombs entered a turret through such a joint.
- Staggered and overlapping joints resist failure ([multiVsSinglePlate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php)).
- Reinforced outer hulls: up to 15% of plate area may be ≥50% thicker at joints and ribs, which increases local decap thickness ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)).

**Backing.** Teak counts at 1/100 of its thickness and cement at 1/25. Metal backing counts at Q/2 when within 0.3 cal ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php)). Bismarck: 50 mm teak on 16–25 mm side plating ([kbismarck](https://kbismarck.com/proteccioni.html)).

**Repeated hits and cracks:**
- USN acceptance fires **two** impacts per Class A plate and rejects radial cracks longer than **2 cal** from the hole. Cracks to the edge are especially unwanted ([O.S. 751](https://navweaps.com/index_nathan/os751BalTests.php)).
- About half of all KC plates tend to "lid formation", i.e. plug/disc, without changing the penetration limit ([kbismarck Hoyer](https://kbismarck.com/hoyer-warship-armor.html)).
- I found **no quantitative "hit near a previous hole" law** [UNCERTAIN]. Suggested [INFERRED]:
  - Treat a previous hole or crack as a free edge.
  - Apply the edge-effect formula with d = distance to the hole rim, plus crack length if radial cracks exist.
  - For FH plates already cracked through, use Q ≈ 0.85–0.9.

**Belts pushed in.** I found no numeric data. Treat it qualitatively (structural, not ballistic) [UNCERTAIN].

**Gratings.** A 15"-thick holed armour grating ≈ a 6" solid plate, about 40% ([O.S. 751](https://navweaps.com/index_nathan/os751BalTests.php)).

---

## 6. Underwater ("diving") hits

**Tosa trial, 1924.** A 41 cm shell fell about **25 m short**, travelled underwater and struck at frame 228, **3.3 m below the designed waterline**. It penetrated **76 mm** of armour and burst in the port engine room, admitting about 3,000 t ([Wikipedia Tosa](https://en.wikipedia.org/wiki/Japanese_battleship_Tosa)). The British-spec nominal fuze delay was 0.025 s, and the observed delay was abnormally long ([tech-041](https://www.navweaps.com/index_tech/tech-041.php)).

**Type 88/91:**
- The windscreen and cap head are knocked off by the water surface, leaving a flat face covering about 50% of the cross-section. The design reduces tumbling ([tech-041](https://www.navweaps.com/index_tech/tech-041.php); [Type 91 data](https://www.navweaps.com/index_nathan/japType91ProjData.php)).
- Pointed noses give overturning forces "a longer lever", so they tumble and lose velocity faster.
- Fuze delay was **0.4 s** vs 0.02–0.05 s for normal AP.
- The design was "somewhat less effective at penetrating armor".
- Combat: one hit only, the 8" round into **USS Boise**'s forward magazine at Cape Esperance ([navalgazing Shells 3](https://www.navalgazing.net/Shells-Part-3); [tech-041](https://www.navweaps.com/index_tech/tech-041.php)).
- Normal AP fuzes detonate the shell "very shortly after it hits the water" ([tech-041](https://www.navweaps.com/index_tech/tech-041.php)).

**Water drag.**
- USNI 1921 (Schuyler's method, 6" 105 lb shell, 3,000 ft/s): velocity fell to 148 ft/s over about **255 ft (78 m)** ([USNI 1921](https://www.usni.org/magazines/proceedings/1921/february/under-water-trajectories)).
- **[INFERRED]** With quadratic drag, V = V0·e^(−x/L), where L = 2m/(ρw·Cd·A):

| Shell | Cd 0.3 (stable, ogival) | Cd 0.8 (flat/cavitating, Type 91-like) |
|---|---|---|
| 16" 1,225 kg | L ≈ 62 m (≈150 cal) | L ≈ 23 m (≈57 cal) |
| 8" 125 kg | L ≈ 25 m | L ≈ 9.4 m (≈46 cal) |
| 18.1" 1,460 kg | — | L ≈ 21 m |

  The Tosa case is consistent: 25 m of travel at ~1 e-fold leaves about 35–40% of entry velocity.
- **Tumbling** (non-Type-91 shells) should dominate. Yaw after water entry multiplies Cd several-fold [INFERRED].
- **The fuze is the real limit.** At 0.035 s and ~400 m/s, a normal AP shell bursts within about 10–15 m [INFERRED].

---

## 7. Turret faces, roofs and barbettes

**Sloped faces.**
- The Yamato face plate (≈26" / 650 mm, leaned back 45° in service) was penetrated **at normal** by a 16" Mk 8 at 1,992 and 1,707 ft/s.
- At its 45° service slope, Okun/DiGiulian conclude it "will barely hole" at 40,000 yd (1,607 ft/s) or at point blank (2,500 ft/s), with no complete penetration at any realistic range ([tech-040](https://navweaps.com/index_tech/tech-040.php)).
- Iowa faces: 17" Class B on 2.7" backing, sloped back about 45°, so they are usually hit at low obliquity only by plunging fire ([navalgazing Armor 4](https://www.navalgazing.net/Armor-Part-4)).
- **[INFERRED]** For a face sloped back by angle s, beam-on obliquity = s − fall (it reaches 0 when fall = s). Plunging fire *reduces* obliquity on back-sloped faces and *increases* it on roofs.

**Roofs.**
- Roof obliquity ≈ 90° − fall, or more if the roof slopes.
- Richelieu's FH roofs were penetrated by the only battleship shell hit received ([navalgazing Armor 2](https://www.navalgazing.net/Armor-Part-2)).
- Iowa roofs: 7.25" ([navalgazing Armor 4](https://www.navalgazing.net/Armor-Part-4)).

**Barbette curvature [INFERRED].** A shell hitting a cylinder of radius R at lateral offset b has an extra local horizontal obliquity θ = asin(b/R). For uniformly distributed b, P(θ > x) = 1 − sin(x):

| x | 15° | 30° | 45° | 60° | 75° |
|---|---|---|---|---|---|
| P(θ > x) | 0.74 | 0.50 | 0.29 | 0.13 | 0.03 |

Combine θ with the fall angle by vector dot product, as in §1.1.

GKdos example: a 350 mm KC barbette at α = 83° (German convention) has Vg = 390 m/s ([kbismarck GKdos 100](https://kbismarck.com/gkdos100a.html)). German barbette calculations evaluated the limiting tangent case explicitly.

**Barbette thickness transitions.** Above the deck, barbettes are thick; below the deck, they are thin and screened by belt and deck. Examples:
- Bismarck: 340 mm above, 220 mm below ([kbismarck](https://kbismarck.com/proteccioni.html)).
- Iowa: 17.3–11.6" above, 3" below ([navalgazing Armor 4](https://www.navalgazing.net/Armor-Part-4)).

The model must treat the "below-deck barbette" as a second plate behind the belt or deck.

---

## What a model needs from this: plate-sequence algorithm outline

Shell state is S = {V, path direction **u**, yaw ψ, capped?, windscreen?, integrity ∈ {intact, nose-damaged, broken}, fuze armed t_arm, delay τ}. Defaults are [INFERRED] unless they cite a section above.

**Step 0. Geometry per plate.**
- Compute the obliquity Ob = acos(|**u**·**n**|) for the plate normal **n**, including target angle, heel/roll, plate inclination and curvature.
- Barbette curvature: sample θ = asin(b/R) (§7).
- Effective obliquity: Ob_eff = Ob + ½ψ for ψ ≤ 10°. For ψ > 10°, set integrity = "tumbling" (§4c).

**Step 1. Hard ricochet gate.**
- If Ob_eff ≥ 80°, ricochet (§2.1).
- If 65° ≤ Ob_eff < 80° and Vs/V_BL(Ob) < r(Ob), ricochet.
  - Default r(Ob) = 1 + 0.08·(Ob − 65). Equivalently, require about 1.0× V_BL at 65° and about 2.2× at 80°.
  - Tune this to the IJIE 2023 velocity-ratio form (§2.2).
- If the shell is broken or shattered at 45–60°, suppress ricochet (§1.4).

**Step 2. Effective plate thickness.**
- Te = Q·T·(1 − edge loss), with edge loss = 15%·(1.5 − d)/1.5 for d < 1.5 cal (§5).
- Add backing: wood/100, cement/25, metal·Q/2 within 0.3 cal (§3.2, §4b).
- Laminated layers in contact: Tlam = (Σ + spaced)/2. Optionally weight toward spaced for T/D < 0.2 (M79 5"+2" result, §4b).
- **Do not** pre-combine spaced plates. Step through them individually: that captures normalisation and yaw between plates (§3.3, §4a).

**Step 3. Ballistic limit at obliquity.**
- V_BL(T/D, Ob, material, cap, nose) = V_BL0(T/D) × G(Ob, T/D).
- Default G: Thompson FSTD, `G = [F(T/D,Ob)/F(T/D,0)]/cos(Ob)` (§1.3). This automatically yields n ≈ 1.1–1.9.
- Alternative: M'(Ob,T/D)/cos.
- For FH plates:
  - Uncapped shell: multiply by the shatter bonus, ×1.3^(exponent) in thickness at 0°, tapering to 1 at 55°.
  - Soft cap above 15–20°: treat as uncapped (§1.4).
  - Japanese cap above 45°: treat as uncapped.
- Large calibre at high obliquity: optional penalty above 60° (reverse scaling, §1.2) [UNCERTAIN].
- Elongation penalty for D > 8" (M79 formula, §3.1).

**Step 4. Decap check (before the penetration outcome).**
- If the shell is capped and Te/D ≥ the decap threshold for its cap type and Ob (Type 1: 0.105 at 0°, 0.0805 at 15–45°; Type 2: 0.17), set capped = false. Do this even if the plate is not penetrated.
- Record the decap location for the gap test on the next FH plate:
  - Required gap 1.6–3 cal at 0°, 1 cal at ≥30°.
  - Halve the gap requirement if the space is cement-filled (§4d).
  - If the gap is too short, the cap is still effectively "on".

**Step 5. Outcome.**
- If Vs < V_BL: stop (embed or bounce). If Vs is between V_BL and about 1.1 V_BL, set integrity to nose-damaged with probability p_break(Ob):
  - Peak about 0.5 at 45°.
  - Low (about 0.1) below 30° and above 60° (§1.2, §4g).
- If the shell is uncapped on FH: check shatter.
  - Shatter velocity drops to about 0.5× at ≥60°.
  - On shatter, the shell becomes broken: residual penetration 0.15× and no effective burst (§4g).

**Step 6. Residual velocity and new direction.**
- Vres = cos(DF)·sqrt(Vs² − V_BL²)·W/(W + W_plug). Lambert p = 2 by default; Okun and GKdos both imply it (§4e).
- DF: M79 formula, toward the normal. Defaults by Vs/V_BL at 30–45°:

| Vs/V_BL | DF |
|---|---|
| 1.1 | ≈15–18° |
| 1.5 | ≈6–7° |
| 2.0 | ≈3–4° |

- For Ob > 45°, scale DF by [1 + (Ob − 45)/45], the base-first rule (§1.5).
- If DF > 45°, set the shell to tumbling (§1.5).
- Thin-plate exception: if T/D < 0.08, cap DF at about 3° (Bismarck 2" deck example, §1.5).
- Update **u** by rotating it toward −**n** by DF in the plane of incidence.
- Optional yaw: ψ_new ≈ k·DF with k ≈ 0.3–0.5 [UNCERTAIN; no naval data].

**Step 7. Fuze.**
- Arm on any plate of T/D ≥ about 0.07 (single plate), or ≥0.05 if two spaced plates lie within about 20 cal ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php)).
- Burst after delay τ (0.025–0.035 s typical; Japanese 0.4 s, §6), measured along the path at the current V. This determines whether later plates are reached.

**Step 8. Water segment (if the shell lands short).**
- Normal AP bursts or tumbles almost immediately after water entry.
- Type 91-style shells: V(x) = V0·e^(−x/L), with L ≈ 45–60 cal (flat nose) and the path angle ≈ preserved. Cap and windscreen are removed at entry. Then proceed to the hull plates below the belt (§6).

**Step 9. Iterate** over the ordered plate list: hull skin → (decap) → belt → slope/backing → bulkhead/barbette below deck. For deck paths: upper deck → bomb deck → armour deck → splinter deck. Stop when V = 0, the shell ricochets, the shell bursts, or a vital volume is reached.

**Calibration targets.**
- Hood 1919 deck series (§1.5, §4e).
- 2+1+2 vs 5" STS at 68°: observed velocity ratio about 0.74 (§3.3).
- Watertown: 6" gap reduces 2×1.5" from ~1,980 to ~1,610 ft/s (§4a).
- Iowa IZ vs 16"/50: 23.6–27.4 kyd (§3.4).
- USN 16"/50 deck column (§3.1), treated as an upper bound below 15° fall.

**Suggested defaults table:**

| Parameter | Default | Range / note |
|---|---|---|
| Hard ricochet obliquity | 80° | 75–80° |
| Soft ricochet onset | 65° | 60–67.5° |
| Obliquity law | FSTD F(T/D,Ob)/cos | or cos^n with n = 1.1 + 0.8·(T/D)·cos(Ob) |
| Spaced combination exponent | 1.4 | each plate 0.2–1.1 cal |
| Residual exponent p | 2 | 2–2.5 |
| Yaw → obliquity | +½ψ (ψ ≤ 10°) | — |
| Decap T/D (Type 1, 15–45°) | 0.0805 | 0.105 at 0° |
| Decap gap (oblique) | 1 cal | 1.6–3 at 0°; halve with cement |
| Edge loss | 15% at edge, 0 at 1.5 cal | — |
| FH shatter bonus | +30% T at 0° → 0 at 55° | 20–40% |
| Fragment penetration | 0.15× intact | — |
| Water e-fold length | 50 cal (flat nose), 150 cal (ogive, but tumbles) | — |
