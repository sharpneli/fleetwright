# Armour penetration: research base and synthesis (1860–1945)
Status: draft    Updated: 2026-10-16    Request: —

*Compiled 2026-10-16 for the Naval project. This is the foundational research step for the armour-penetration model. It is **not** the model. It gathers what a model needs: what "penetration" means, the formulas that existed and what each was fitted to, armour materials and their parameters by era and nation, shells and caps, fuzing ("priming"), obliquity, decks and multiple layers, and calibration data to fit against. §8 sketches one reasonable model as a suggestion. The game's own fit comes later.*

**How this relates to what the project already had.** `../damage-research/05-mechanics-and-games.md` §A1–A4 and `../damage-research/08-gunfire-effects.md` §3 already hold a working penetration pipeline: Garzke & Dulin plus fitted cosine exponents, Okun's spaced-plate rule, fuze arming and decapping thresholds. This research checks all of those, keeps most of them, and replaces some (§10). Nothing here changes the hit-location or burst-effects material in `08`.

**Files** (all in ``):

| File | Scope | Size |
|---|---|---|
| `00-overview.md` | This synthesis | — |
| `01-physics-formulas.md` | Ballistic-limit definitions, failure physics vs T/D, the full formula catalogue (Fairbairn → Thompson → Okun M79 → Lambert), residual velocity, decks, scatter | ~8k words |
| `02-facehardened-caps.md` | How cemented armour works, scaling, Okun's FACEHARD, shatter and the shatter gap, caps, decapping, shell quality by navy, FH data | ~9k words |
| `03-materials.md` | 35 armour materials 1859–1945 (composition, density, hardness, face depth, thickness range, quality), nation timelines, non-steel layers, weights | ~10k words |
| `04-shells-fuzes.md` | Projectile types by era, caps, fillers, fuze mechanics (arming, start threshold, delay), burst location, multi-plate fuzing, dud statistics | ~10k words |
| `05-obliquity-multilayer.md` | Obliquity laws, normalisation, ricochet, decks, spaced and laminated plates, decapping gap, slope-behind-belt, joints, underwater hits, barbettes | ~8k words |
| `06-early-era.md` | 1860–1910: period formulas, "inches of wrought iron", plate equivalences, trials (Spezia → Beardmore), combat evidence | ~7k words |
| `07-calibration-notes.md` + `07-calibration-tables.csv`, `07-okun-facehard-tables.csv`, `07-table-sources.csv`, `07-trials.csv` | Calibration dataset: 41 guns of published penetration tables (303 rows of official/book data + 357 rows × 5 plate types of Okun FACEHARD output), 225 trial rows including 30 measured NPG ballistic limits | data |
| `armor_pen_ref.py` | Test bench: every formula coded as published, the parameter tables below, a candidate resolver and plate stepper, and checks against all the data. Every **[CALC §x]** number in this file comes from its section *x* | ~800 lines |

Tags: **[CALC §x]** = computed by `armor_pen_ref.py` section x. **[INFERRED]** = my reasoning or tuning. **[UNCERTAIN]** = sources conflict or are thin. Detailed citations are in files 01–07. Only the key ones are repeated here.

---

## 0. The ten findings that matter most

1. **"Penetrates or not" is not one threshold. Use at least three outcome states.** Navies scored *holing*, *Navy ballistic limit* (whole shell through, or 80% of its body weight), and *effective* (through intact with a working fuze, i.e. fit to burst). For a big APC against face-hardened plate, the holing velocity sits about 2–12% below the intact one. Okun's "effective" thickness is about 10% below his holing thickness ([Penetration_index](https://www.navweaps.com/index_nathan/Penetration_index.php)). The damage model needs the difference: holed-but-broken means fragments and a weak burst, effective means the full burst behind the plate.
2. **The USN's own formula, Thompson's F-formula, reproduces the navweaps US 16″/50 table at every range**: within 0.07″ on the belt and 0.08″ on the deck **[CALC §A]**. It also reproduces every "standard NBL" in the 1946–47 NPG plate trials to within about 1.5% **[CALC §B]**. It was fitted c.1930–31 to Class B/STS plates hit by the 260-lb 8″ Mk 11 Mod 1 AP from 0° to 75°, and the USN was still grading WWII plates against WWII hard-capped shells on it in 1946–47. Its F term rises linearly with T/D, so the change of exponent with plate thickness is built in. **It is the best-documented single engine available**, and the overview uses it as the reference scale.
3. **The Garzke & Dulin fit the project uses is a 0°, muzzle-velocity power law.** Multiplying it by cos(Ob) overshoots by 6–28% at 15–45°. The project's fitted `cos^1.59` / `cos^1.125` exponents were right for the 16″ Mk 8 because they are exactly what Thompson implies there. For other shells and T/D the exponent should vary: about 1.1–1.25 for thin decks at 60–75°, 1.5–1.9 for thick belts at 15–45° **[CALC; 05 §1.3]**.
4. **Armour quality needs a single, explicit basis, and published "figures of merit" don't share one.** The period rated plates in "inches of wrought iron" (Tresidder). The USN used Gâvre/De Marre, Okun uses WWII Class A at 8″, and the materials literature mixes all three. Re-running 17 normal-impact historical trials through Thompson shows Tresidder-wrought-iron ≈ **1.77 ×** Thompson-STS thickness for the same shot (range 1.66–1.87) **[CALC §L]**. That one factor converts every period figure of merit onto the modern scale.
5. **On that common basis (WWII STS = 1.00, thickness multiplier against a good capped shell), the progression is:** wrought iron 0.53 → compound 0.72 → all-steel 0.78 → nickel steel 0.85 → Harvey 0.82 → early Krupp KC 0.85–0.88 → WWI homogeneous 0.92–0.95 → WWII face-hardened 0.89–1.09, depending on type and calibre (§4.2). So per inch, **late-WWII armour beats wrought iron ~1.85×, compound/steel ~1.3×, and early KC only ~1.15× against a modern shell.**
6. **Most of the face-hardened era's advantage was against bad shells, not good ones.** Against uncapped steel AP, a face-hardened plate shatters the shell and gains about **+30–40% effective thickness** at normal impact. The gain fades to zero by ~55° obliquity (Okun), and early trials put it at 1.2–1.7× **[CALC §L]**. Caps (1894 onward) erase most of it while they stay on. That is why the same KC plate reads FM 2.3–2.7 vs uncapped and ~2.0 vs capped shot in EB1911. **In a game this has to be a state change (shatter or not), not a smooth multiplier.**
7. **Face-hardened plate gets relatively weaker against bigger shells, and how much depends on face depth.** Comparing Okun's FACEHARD tables with Thompson on one criterion (Navy limit) **[CALC §D]**: US WWII Class A (55% face) is worth about 1.03× STS against an 8″ shell but about 0.92× against a 16″ shell. KC n/A (41%) stays ~1.0, and the thin-face British CA (30%) and Italian Terni rise slightly with calibre, to ~1.03 and ~1.09. The 1947 USN Class A acceptance *specs* show the same direction (6″ 104% → 12–16″ 85–91% of the homogeneous standard), though they are confounded by shell quality **[CALC §C]**. One quality number per nation is wrong by up to ~15% across calibres.
8. **Fuzes start on plate, not on contact.** A base fuze needs about **0.06–0.10 calibre** of plate at normal to start, much less at obliquity (Okun's 0.07·D formula; USN Mk 21 doc: 1.5″ at 0° and 0.375″ at 65° for a 16″ shell). Below that, the shell passes through intact and unexploded. Once started, it bursts **delay × residual velocity** later: 0.025 s (RN), 0.033–0.035 s (USN, KM WWII), or 0.08–0.4 s (IJN Type 91, disputed). A thin plate ahead of the armour (Iowa bomb deck, Littorio's 70 mm, Bismarck's 50 mm) starts the clock early and strips caps (§6).
9. **Multiple layers: step plate by plate, don't pre-combine.** Lambert–Jonas residual velocity with p = 2 (`Vr = √(V² − V_L²)`, which is also the German GKdos 100 rule) plus Okun's normalisation angle gets the Hood 1919 upper-deck residual right (1,234 vs Okun's 1,236 ft/s) but puts the thick main-deck limits 12–28% above Okun's own reconstruction. On the US spaced-deck datum (2+1+2″ at 68° = 0.74 of a solid 5″) it gives 0.60–0.64, i.e. it under-rates spaced decks by ~15%, while Okun's 1.4-rule gives 0.78 **[CALC §H]**. Stepping is still the right structure because it captures decapping, fuze start and deflection between plates; its deck constants need tuning. Okun's 1.4-power equivalence is a good cross-check, but it hides those state changes.
10. **Battle results are dominated by shell and fuze failures, not by the formula.** Belts ≥ 6″ were essentially never perforated in 1894–1905 combat. At Jutland 1 British hit in 13 on >9″ plate penetrated and burst. Bismarck took ~3 belt holes in ~400 hits. A model that is "right" on the ballistic limit will still overproduce citadel hits unless it carries era-specific shatter, break-up-at-obliquity and fuze-failure rates (§5.4, §6.5).

---

## 1. What "penetration" means (01 §1, 02 §2.2, 04 §4.3)

| Limit | Criterion | Typical relation | Use in game |
|---|---|---|---|
| Army / protection BL | Pinhole or crack, or a witness plate behind is holed | 25–40% below Navy in velocity (small arms) | Spall and fragments behind thin plates only |
| **Holing (HBL)** | Calibre-sized hole; shell broken; fragments pass | FH: 2–12% below NBL | Plate defeated, but the burst is weak, fragments only |
| **Navy BL (NBL)** | Whole shell through (or 80% of body weight if broken); condition undefined | Reference | "Perforated" |
| **Effective (EBL)** | Through with cavity, base plug and fuze intact | ≥ NBL; impossible if the shell shatters | Full burst behind the plate |
| Partial (PP) | Nose half through, rear rejected; above 45° the lower body can pass | — | Plate damaged, small effect |

**What published tables mean:**
- **USN / Garzke & Dulin / navweaps "US Empirical"** tables are Thompson NBL against Class B-quality plate, with obliquity = angle of fall **[CALC §A]**.
- **Okun FACEHARD tables** give PP / H / NL / EFF columns.
- **Krupp tables** gave an NBL (often for a broken shell) and an "effective intact" limit.
- **The 1947 USN acceptance tests** pass a plate if no more than half the broken body gets through. Those are plate grades, not "fit to burst" limits.

**Recommended outcome states for the resolver** (USN test codes, 04 §8.4):
- Penetration: {complete, partial, incomplete/stuck, ricochet}
- Body: {intact, nose damaged, shattered}
- Fuze: {not started, started, blinded}
- Burst: {none, in plate, behind plate at distance s, low order}

---

## 2. Physics in one page (01 §2, 02 §1, 05 §1)

**Homogeneous (ductile) plate.** The failure mode changes with thickness-to-calibre ratio T/D:

| T/D | Dominant failure | Penetration behaves like |
|---|---|---|
| < ~0.1 | Dishing, petalling | T grows faster than KE (exponent n > 1) |
| ~0.1–0.3 | Petalling, plugging | T ∝ V (n ≈ 0.5) |
| > ~0.75 | Ductile hole enlargement | T ∝ KE (n → 1) |

- Every single-exponent formula is a compromise over some T/D window. De Marre's 0.714 is good for 0.1–0.75 cal (Okun). Okun's M79 energy-density curve rises about 4× between T/D 0.085 and 0.465 (01 §2.3).
- Thompson handles this with F = 12,000·T/D + 34,600 (at 0°). Thicker plates need more velocity than KE alone would say.
- **Nose shape** is worth ±10–15%.
- **Plate hardness** helps when the plate is thick relative to the shell, and hurts when the shell over-matches the plate.
- **Ductility (elongation)** controls the size effect. Okun's penalty applies for shells above 8″ when elongation is under 25%. German Wotan Hart (18%) loses ~13% against 15″ shells; STS (25%) loses 1.4% (01 §2.5–2.6).

**Face-hardened plate** (02 §1) is three layers:
- a cemented skin at 575–700 BHN, about 1″ deep;
- a "decremental" deep face down to ~350 BHN at 20–55% depth;
- a tough back at 200–240 BHN.

The face is there to *break or deform the shell nose*, not to resist hole enlargement. The plate fails by punching a plug out of the back.
- Against a shell it breaks, the plate gains ~30–40% in effective thickness.
- Against a well-capped shell it does not break, it is only roughly equal to good homogeneous plate. The USN graded Class A and Class B on the same scale, within ±10%.
- Below about 3–4″ (or ~0.3 cal) face-hardening *weakens* plate, because it thins the tough back. Nobody made deep-face FH under 3.2–5″.
- At high obliquity the brittle face is counter-productive, which is why decks were homogeneous everywhere except *Richelieu*'s turret roofs.

**Scaling (02 §1.4).** Brittle fracture of the face scales with area; ductile tearing of the back scales with volume. So at the same T/D, FH plate does worse against larger shells, and markedly so once the soft back is under ~65% of the thickness.

**Obliquity (05 §1).** Resistance rises faster than path length (1/cos), because the nose rotates and the body bends.
- Below the "biting angle" (~65–67.5° proxy) the nose digs in.
- Above it the shell is pushed sideways. Pointed AP essentially cannot penetrate beyond 75–80°.
- A shell that gets through turns toward the plate normal. It turns a lot when barely penetrating (15–30°) and very little when it over-matches (2–4°) (05 §1.5).

---

## 3. The formula landscape (01 §3, 06 §1)

| Formula (date) | Form (T, D in; W lb; V ft/s) | V exponent | Fitted to | Verdict for the game |
|---|---|---|---|---|
| Fairbairn (c.1865) | T/D = 7.692e-4·[(W/D³)V²]^0.5 | 1.0 | Thin wrought iron | Historical only |
| **Tresidder** (1870s, RN standard to 1900s) | T/D = 3.798e-5·(W/D³)^0.5·V^1.5 | 1.5 | Wrought iron, 0.25–0.75 cal | **Consistent with period gun tables** (13.5″ 28.2″ WI; 12″ Mk VIII 33″; but the striking velocities were back-solved, so not an independent test). Matches EB1911's 11.8″ KC trial (26.1 vs 25.9″) and Spezia (22.2″ vs 22″ plate) **[CALC §G]**. The basis of every RN "figure of merit" |
| Gâvre (1870s) | T/D = 2.887e-5·D^0.429·[(W/D³)V²]^0.714 | 1.43 | Wrought iron | USN acceptance basis 1894. Its scale term makes USN FMs ~20% lower than RN FMs at small calibre |
| De Marre (1886/1890) | T/D = 5.021e-5·D^0.0714·[(W/D³)(V/C)²cos³Ob]^0.714 | 1.43 | Ni-steel C = 1; STS C ≈ 1.2–1.25 | Verified against the 1937 USN textbook. Needs C ≈ 1.34–1.39 and drifting to match the USN 16″ table: wrong exponent at T/D 1.2–2. **cos³ is too steep** (cos^2.14 in thickness). Safest used as a ratio between similar shells |
| Krupp KC vs uncapped (1895) | T/D = 6.659e-4·[(W/D³)V²]^0.5 | 1.0 | KC, uncapped shot | Historical. KC FM vs uncapped rises as √V |
| Krupp all-purpose (1930s) | T/D = 0.30386·D^0.25·[(W/D³)(V/C)²]^0.625 | 1.25 | C 525 mild steel, 655–694 Wh, ≤804 KC n/A | Normal impact only. Good for German homogeneous checks (G.Kdos.100 C table, 01 §3.3) |
| Davis Harvey capped (c.1900) | = Krupp all-purpose with C ≈ 691 | 1.25 | Harvey vs capped | ~22% too high vs FACEHARD on Harvey tests |
| **Thompson F** (USN 1930–31) | V_L = 0.024056·D·√(T/W)·F/cos Ob; **F = 6(T/D − 0.45)(Ob² + 2000) + 40000** | ~1.2–1.5, varies | 260-lb 8″ Mk 11 Mod 1 AP vs Class B/STS, 0–75° | **Reproduces USN tables and the NPG grading scale** **[CALC §A, §B]**. Used by the USN to grade Class A too. No scale term (pure similitude) |
| Garzke & Dulin (fit) | T = 4.689e-4·W^0.55·D^−0.65·V^1.1 | 1.1 | Thompson at 0°, muzzle, 11–18″ | **0° only.** Don't cos-scale |
| Okun FH law | T = C·W^0.2·V^1.21 | 1.21 | FH plate, weight changes at fixed calibre | C must carry calibre. Fits to FACEHARD range tables give V^~1.38 with W^0.2 fixed **[CALC §O]**; V and Ob are tied together in range tables |
| Okun M79 / HCWCALC | NBL = NC·J·SF·M(Ob,T/D)·[PQF·T/D]^TEXP/√(W/D³) | table | NPG homogeneous tests, T/D 0.005–3.5, ≤80° | Physically richest homogeneous model; internals unpublished. Disagrees with Thompson on decks by −20% to +16% (§7.4) |
| Lambert / THOR | V_L = 4000(L/D)^0.15·[f(z)D³/M]^0.5 … | — | Rods and fragments vs RHA | **Do not use constants for naval APC** (16″ case off by 60%). THOR is fine for fragments |
| Lambert–Jonas residual | V_r = a·(V^p − V_L^p)^(1/p), p ≈ 2 | — | Universal | **Use for stepping** (= Okun M79 = German GKdos 100 rule) |

**The basis problem, quantified [CALC §L].** For the same shot, Tresidder-wrought-iron / Thompson-STS = 1.77 on average (1.66–1.87 across 17 normal-impact trials, 6″–17.7″, 1,420–2,260 ft/s; oblique trials excluded because Tresidder has no obliquity term). So:
- **FM (inches of WI per inch, Tresidder basis) ≈ 1.77 × Q (STS basis).**
- The FM of a face-hardened plate also rises as √V against uncapped shot (Tresidder ∝ V^1.5 vs Krupp ∝ V^1.0). That is why EB1911 quotes KC at 2.7 for thin plates and 2.3 for thick ones.
- Never compare a USN (Gâvre) FM with an RN (Tresidder) FM without rebasing (06 §2.3).

---

## 4. Armour materials

### 4.1 The historical ladder (03 §1, 06 §2)

The equivalences the period itself published, as thickness ratios. Each is basis- and shot-dependent:

| Step | Claim | Notes |
|---|---|---|
| Wrought iron → compound or all-steel (1876–86) | ×1.25–1.3 | "10″ compound ≈ 12.6″ iron" (USN 1937) |
| → nickel steel (1889) | +5% (trials suggest more) | Its real gain was toughness: it didn't crack (Annapolis 1890) |
| → Harvey (1891) | +15–20% over Ni-steel; FM 1.8–2.1 vs uncapped | Thin face; big uncapped/capped split |
| → Krupp KC (1894) | +15% over Harvey (claims 9–30%) | FM 2.3–2.7 vs uncapped, ~2.0 vs capped |
| → "modern" (1937) | +10% over early Krupp | USN 1937 text |
| → best WWII | "up to +40%" over early Krupp (Naval Gazing) vs Okun's ~+20% (KC a/A = 0.83) | **[UNCERTAIN]** The spread depends on which shell is assumed |

### 4.2 Reconciled quality table (engine basis: WWII STS = 1.00, good capped shell)

Q is a **thickness multiplier**: a plate of thickness T resists like Q·T of standard US Class B/STS on the Thompson scale (the USN's own grading standard). Values come from three independent routes that agree within about ±5%:
1. 21 early trials re-run through Thompson **[CALC §L]**;
2. Okun's homogeneous "backing quality" ladder: WI 0.6, mild steel to WWI 0.7, HT/Ni 0.8, post-WWI HT/Ducol 0.9, STS 1.0 ([Miscarmr](http://www.navweaps.com/index_nathan/Miscarmr.php));
3. the NPG 1946–47 grades and Okun FACEHARD tables compared with Thompson on the Navy-limit criterion **[CALC §B, §D]**.

The FH columns are against a good *capped* shell. The shatter bonus against uncapped or decapped shells (§5.3) comes on top.

| id | Material | Years (new build) | ρ kg/m³ | FH | Q @8″ shell | Q @16″ shell | Usable thickness | Evidence / note |
|---|---|---|---|---|---|---|---|---|
| WI | Wrought iron (+teak) | 1859–1880 | 7,700 | – | 0.53 | 0.53 | 2–24″ | Spezia 1876: 22″ beaten with ~590 fps to spare → ~0.50; Tresidder/Thompson ratio → 0.56; Okun 0.6 |
| LAMWI | Laminated / sandwich iron | 1860–1880 | 7,700 | – | 0.43 | 0.43 | — | Solid beats laminated ~1.25× (Appleton 1880) |
| STL | All-steel (Schneider) | 1876–1890 | 7,850 | – | 0.78 | 0.78 | ≤22″ | Annapolis: held, so ≥ 0.76; cracks |
| CMP | Compound (steel-faced iron) | 1877–1891 | 7,750 | quasi | 0.72 | 0.72 | 4–20″ | Annapolis < 0.75–0.80; shatters *chilled* shot only; face delaminates |
| MS | Mild ship steel (structure) | 1880–1945 | 7,850 | – | 0.70 | 0.70 | — | Okun ladder |
| NIS | Nickel steel | 1889–1896 | 7,850 | – | 0.85 | 0.85 | 2–18″ | Annapolis > 0.77–0.83; Johnson limit 0.92 |
| HT | High-tensile (UK HT, Krupp low-Ni) | 1900–1925 | 7,850 | – | 0.80 | 0.80 | 0.25–2″ layers | Okun ladder; laminated decks |
| HRV | Harvey | 1891–1899 | 7,850 | thin | 0.82 | 0.82 | 3.2–18″ | NO 1937 limit 0.79; ×1.4 vs uncapped → 1.15 (trials 1.0–1.18) |
| KCA | Krupp KC a/A and licensees (CKC, Izhora, Terni pre-WWI) | 1894–1918 | 7,850 | ✓ 33–35% | 0.88 | 0.85 | 3.2–14″ | Capped trials 0.87–0.97; Okun 0.83 of WWII FH |
| BKC | British / Japanese VC / Italian WWI KC | 1900–1932 | 7,850 | ✓ ~30% | 0.85 | 0.85 | 4–13.5″ | "Inferior to KC a/A" (Okun) |
| WKC | Witkowitz KC (A-H) | ~1905–1918 | 7,850 | ✓ | 0.97 | 0.97 | 3.2–11″ | "~95% of WWII FH" (Okun) |
| MNC | Midvale non-cemented (US) | 1906–1910 | 7,850 | ✓ >80% | 1.15 | 0.84 | 4–14″ | 1.3× CKC vs 12″, ≈Harvey vs 16″: extreme scaling |
| KNC | Krupp-type homogeneous Ni-Cr | 1894–1925 | 7,850 | – | 0.92 | 0.86 | ≤4″ (roofs ~7″) | Okun 0.95 homogeneous to WWI |
| CB1 | US Class B / STS, WWI | 1910–1932 | 7,850 | – | 0.95 | 0.91 | — | |
| DS | Ducol / D-steel | 1925–1945 | 7,850 | – | 0.90 | 0.90 | ≤2″ | Okun ladder |
| **STS** | US STS / WWII Class B | 1930–1945 | 7,850 | – | **1.00** | **1.00** | ≤19.5″ | Reference; NPG Class B 91–106% |
| NCA | British non-cemented | 1926–1945 | 7,850 | – | 1.00 | 1.00 | <4″ (+roofs/decks) | |
| WH | Wotan Hart | 1925–1945 | 7,850 | – | 1.00 | 0.85 | ≤4.3″ (decks ~7″) | 18% elongation; NPG 87–106% |
| WW | Wotan Weich | 1925–1945 | 7,850 | – | 0.95 | 0.89 | ≤2″ | |
| PO | Italian homogeneous | 1929–1943 | 7,850 | – | 1.00 | 0.82 | <4″ | 16–17% elongation **[UNCERTAIN]** |
| NVNC / CNC | Japanese homogeneous | 1926–1945 | 7,850 | – | 0.97 | 0.93 | ≤12″ / ≤3.2″ | Okun M79 Q 0.97 |
| **USA** | US Class A, WWII (thick chill) | 1933–1945 | 7,850 | ✓ 55% | **1.03** | **0.92** | 5–17″ (16″+ faces were Class B) | FACEHARD (NL) and OS 751 trend **[CALC §C, §D]** |
| **CA** | British cemented | 1933–1945 | 7,850 | ✓ 30% | **0.99** | **1.03** | 4–15″ | Improves slightly with calibre |
| **KCNA** | Krupp KC n/A | 1928–1945 | 7,850 | ✓ 41% | **1.01** | **0.99** | 3.9–15″ | NPG 83–113% |
| **TKC** | Italian Terni WWII | 1929–1943 | 7,850 | ✓ ~45% | **1.03** | **1.09** | 4–15″ | Best of the five at 15–18″ in FACEHARD; little test data |
| FRFH | French 1930s FH | 1932–1945 | 7,850 | ✓ ~35% | 0.95 | 0.92 | 4–17″ | Okun assumed = US; German tests "inferior" **[UNCERTAIN]** |
| **VH** | Japanese Vickers Hardened | 1937–1945 | 7,850 | ✓ ~35% | **0.89** | **0.89** | >11–26″ only | FACEHARD NL 0.89–0.90. Shinano's 26″ plate tested at 0.84 *of US Class A* (Okun's basis), so ≈0.8 here: very thick VH was brittle |

Notes:
- **Q(D) for FH** = Q8·(D/8″)^−k for D > 8″, with k = 0.17 (US Class A), 0.05 (KC a/A, French), 0.03 (KC n/A), 0 (VH), −0.05 (CA), −0.08 (Terni), 0.45 (Midvale NC). Fitted on the NL criterion only; mixing criteria (holing for British tables, NL for German and Japanese) exaggerates the Class A trend. The 6″ column (only the German 15 cm table) makes every FH plate look 1.1–1.35× STS; it is treated as an anomaly and Q is held at Q8 below 8″.
- **Homogeneous below 25% elongation** loses Q against shells over 8″ by Okun's M79 penalty: `Q·{1 − [1 − √(El/25)]·min(D − 8, 8)/8}` (capped at 16″). Not applied to the pre-1900 materials, whose Q comes straight from big-calibre trials.
- **Thin FH plate** (< max(t_min, 0.3·D)) gets no FH advantage: ×0.9 [INFERRED].
- Code: `MATERIALS` and `plate_effective_T()`; full table with 6″/12″ columns in **[CALC §M]**.

**What it means per inch [CALC §J]:** the same 12″/850 lb shell at 2,000 ft/s and 10° defeats this much actual plate.

| Armour | Uncapped 1890s AP | Capped WWII AP | Weight per protection vs STS (capped) |
|---|---|---|---|
| Wrought iron | 27.7″ | 29.2″ | 1.85 |
| Compound | 20.4″ | 21.5″ | 1.37 |
| Nickel steel | 17.3″ | 18.2″ | 1.18 |
| Harvey | **13.5″** (shatters) | 18.9″ | 1.22 |
| KC a/A | **12.8″** (shatters) | 17.9″ | 1.16 |
| WWII US Class A | 11.5″ | 16.1″ | 1.04 |
| WWII British CA | 11.0″ | 15.3″ | 0.99 |
| WWII STS (homogeneous) | 14.7″ | 15.5″ | 1.00 |

So "late-WWII armour is better per inch" is ~1.85× over iron and ~1.35× over compound. Over KC it is only ~1.15× against a *good capped* shell. In the 1890s–1900s, shells were uncapped or soft-capped, which made Harvey and KC look 1.3–1.4× better than they were against later shells.

### 4.3 Why later armour was better (03 §6)

1. Nickel for toughness (1889): plates stopped cracking.
2. Cementation for a hard skin (Harvey 1891).
3. Chromium for deep decremental hardening (Krupp 1894).
4. Tempering and toughening control (Witkowitz pre-1918, KC n/A tempered).
5. Molybdenum (France 1912, everyone in the 1930s).
6. Cleaner steel (Si 0.25 → 0.05–0.1%).
7. Stronger backs (WWII 220–240 BHN at 22–25% elongation, against ~195–225 BHN in WWI).
8. Face-depth tuning to the *threat*: deep faces break small shells; thin faces with thick tough backs suit big capped shells. That is why British CA and Italian Terni come out best at battleship calibres.

### 4.4 Plate-to-plate scatter (01 §7, NPG data)

Accepted heavy plates of the same type graded **83–113%** of the standard ballistic limit:
- US Class A 87–105% (mean 95%)
- KC n/A 83–113% (99%)
- Class B 91–106% (98%)
- Wh 87–106% (95%)

**[CALC §B]**. A single measured limit carries a probable error of about ±2.5%. Suggested: draw plate quality per ship (or per armour lot) ~N(1, 0.06) on the limit, plus per-shot noise ~N(0, 0.03). That gives P(perforate) ≈ 7% at 0.90·V_L, 50% at V_L and 93% at 1.10·V_L **[CALC §K]**.

### 4.5 Density, backing and non-steel layers (03 §3, §7)

- **Steel armour alloys are all ~7,850 kg/m³** (40 lb/ft² per inch); nickel changes it by under 0.5%. Wrought iron is ~7,700, chilled cast iron ~7,550, compound ~7,750. Weight differences between metal armours are at most 2–4%, so **protection per ton is Q / (ρ/7,850)**.
- **Backing is a weight-only layer**:
  - Ironclad era: teak 18–36″, i.e. +35% on *Warrior*'s armour weight and +6% on *Inflexible*'s.
  - Steel era: 3–6% (teak 60 mm on *Richelieu*, oak 150 mm on *Littorio*, ~2″ concrete behind US Class A).
  - Okun counts teak at 1/100 and cement at 1/25 of its thickness, and metal backing within 0.3 cal at Q/2.
- **Coal:** "2 ft of coal ≈ 1 in of armour" (Dreadnought Project). That is ~2.5× the weight of the steel it replaces, and it depletes as fuel is burned.
- **Chilled cast iron** (Gruson) was used only in land forts. It shatters uncapped shot but cracks cumulatively.

### 4.6 Availability by nation (condensed from 03 §4)

| Years | UK | US | Germany | France / Italy | Russia | Japan | A-H |
|---|---|---|---|---|---|---|---|
| 1860–76 | WI | WI | WI | WI | WI | — | WI |
| 1876–90 | Compound | WI → steel (1887) | Compound | **Steel** (Schneider) | Compound/steel | imported | Compound |
| 1890–94 | Ni-steel → Harvey | Ni-steel → **Harvey** | Ni-steel/Harvey | Ni-steel → Harvey | Ni-steel → Harvey | Harvey (British-built) | Harvey |
| 1895–1918 | KC (licensed) 0.85 | CKC/Class A 0.85–0.88 (MNC/BNC variants 1906–10) | **KC a/A** 0.88 | KC (FR +Mo 1912), Terni KC | KC (Izhora 1899) | VC 0.85 | **Witkowitz** 0.97 |
| 1919–32 | KC, NC, Ducol | Class A (Bethlehem Thin Chill 1922–23) | Wotan from 1925; **KC n/A** 1928 | FH; Terni 1929 | — | VC/NVNC/CNC | — |
| 1933–45 | **CA / NCA** | **Class A / STS** | KC n/A / Wh / Ww | FH / Terni + PO | no data | **VH** (>11″), NVNC, MNC | (Czech: no data) |

---

## 5. Shells (02 §3–4, 04 §1–2, 06 §4)

### 5.1 What matters for penetration

| Parameter | Range | Effect |
|---|---|---|
| W/D³ (lb/in³) | light 0.42–0.50 (Russian 1895, RN pre-WWI 12″, IJN 1905); standard 0.50–0.58; super-heavy 0.60–0.66 (US Mk 8, Russian 1911) | Thompson: V_L ∝ 1/√W at fixed T and D |
| Nose | 2 crh (pre-1909 RN) → 4 crh → 6 crh → 9 crh US Mk 8 (blunter penetrating nose under a windscreen) | ±10–15% |
| Body | Palliser chilled iron (1867) → forged steel (1880s) → Ni-Cr differentially hardened → WWII decrementally hardened | Shatter resistance |
| Cap | none / soft (Makarov 1894, 4–9% of mass) / hard (Krupp 1911 Type 2, Greenboy 1918, all WWII, 9–20%) / IJN break-away cap head | See 5.2 |
| Filler % | AP 1.1–3.5; SAP/CPC 3.5–9.4; HE 6–15 | Bigger cavity = weaker body. A cavity shell lost a large share of penetration vs solid shot in the 1890s (Johnson trials) |

### 5.2 Caps and decapping (tech-085; 02 §3.5; 05 §4d)

| | Soft cap | Hard cap Type 1 (most) | Hard cap Type 2 (Krupp 1911+, some US 3″) |
|---|---|---|---|
| Effective to obliquity | ~15°, gone by ~20° | While it contacts first | Same |
| Decap plate (homogeneous, T/D) at 0° | 0.105–0.12 (50/50), certain above | 0.105–0.12 | 0.155–0.17 |
| Decap plate, 15–45° | 0.0805 | 0.0805 | 0.155–0.17 |
| Decap plate, >45° | 0.0805 | 0.0805 | 0.12–0.14 |

- **Any face-hardened plate or plate over ~400 BHN always decaps.**
- **Gap behind the decapping plate** for the bare nose to arrive bare: 1.6–3 cal at 0°, falling to **1 cal at ≥30°**. A cement fill roughly halves it.
- **Benefit of a cap**: ~+15% at <15° in the 1890s–1900s. Against KC, Raven & Roberts give +10–20% at low and +30–50% at high velocity. Caps were ineffective below ~1,700 ft/s (EB1911) [UNCERTAIN: the 1894 Johnson cap worked at 1,600].
- Decapping only helps the defender if the next plate is face-hardened (shatter returns) or the shell then fails. Against homogeneous plate the shell just loses the cap's mass.

### 5.3 Shatter (02 §3.1–3.2)

- **Bonus to the FH plate:** +30% (20–40%) at normal, fading linearly to 0 at ~55°. Above 55° shatter *helps* the shell dig in instead of ricocheting.
  - [UNCERTAIN] whether Okun's 30% is on velocity or thickness. The early trials **[CALC §L]** fit **×1.4 in thickness** (Harvey 1897 ≈1.44; Meppen ≥1.37; Beardmore 6″ KC 1.7, a thin plate).
- **When to apply it:**
  - uncapped shell;
  - decapped with enough gap;
  - soft cap above ~20°;
  - RN pre-1917 APC at ≥15–20° on ≥0.4 cal plate;
  - Japanese cap head above 45°;
  - poor-quality lots.
- **Shattered-but-holed** shells give fragments only. Okun gives fragment penetration ≈ 0.15× intact. No effective burst.
- **Shatter gap** (over-match failures at *higher* velocity) is documented for WWII tank guns. A usable three-state form exists: `Pc(V) = (1 − Φ_T)·Φ_I + Φ_T·Φ_S`. The naval evidence is weak. Use it only for poor or decapped shells near point blank.

### 5.4 Shell quality by navy and era (thickness multiplier on what the shell can defeat; 02 §4.6)

| Shell family | Factor | Extra behaviour |
|---|---|---|
| Palliser chilled iron (1867–c.1890) | ~0.85 vs WI; shatters on any steel face | |
| Uncapped forged steel (1886–1905) | 0.95 | Shatters on FH ≥ ~0.25–0.6 cal |
| Soft-capped (1895–1914) | 0.95 | Treat as uncapped above 20° |
| RN 1914–17 lyddite APC | 0.85 | Breaks up ≥15–20° on ≥0.4 cal; lyddite bursts in the plate (Jutland 6/17 on 6–9″) |
| German WWI APC (Type 2 cap, TNT) | 0.95 | Fuze unreliable (≤50% near-nominal delay) |
| Greenboy (1918), IJN Type 91 | 0.95–0.97 | Proofed at 20° on 0.67 cal; degrade above 20–30° |
| WWII standard (US Mods 1–5, RN Mk XVIIB, KM L/4.4) | 1.00 | KM breaks more above ~20° on heavy plate |
| US late-war (Mk 8 Mod 6, Mk 16 Mod 8, super-hard caps) | 1.08–1.11 | |

### 5.5 HE and common shell vs armour (04 §7)

- **Nose-fuzed HE** holes only **~0.16–0.2 cal** of homogeneous plate (~0.3 cal FH by burster action), then bursts outside.
- **Base-fuzed common / CPC** were designed for **⅓–½ calibre** at normal.
- EB1911: common shell punches about ⅔ as well as uncapped AP.
- Pre-dreadnought black-powder common shell did most of the damage at Tsushima, through fires and upperworks.

---

## 6. Fuzing and "priming" (04 §3–6)

### 6.1 How an AP base fuze works

1. **Arming (safety off).** Set-back on firing (~20,000 g) releases a lock. Spin (centrifugal bolts, shutters, threads) completes arming clear of the muzzle. Assume every post-WWI fuze is armed in flight.
2. **Start (firing).** On impact the shell decelerates. An inertia pellet or primer weight moves onto a fixed needle against a creep spring. A heavy shell hitting a thin plate does not decelerate enough to overcome the spring, so **the threshold is set by plate areal density relative to the shell's sectional density, which is why it is quoted in calibres.**
3. **Delay.** A black-powder pellet or train: 0.006–0.03 s (pellet), or ~0.003 s for the mechanical action alone.
4. **Burst.** Booster then filler. A weak booster on an insensitive filler gives low-order bursts or duds (German WWI TNT/picric booster; RN Shellite before the tetryl/CE booster of the late 1920s).

### 6.2 Start threshold

- **Okun (US WWII base fuzes):** `T_fz = Tfzmin·D·{(1 + cos 2Ob)/2 + 0.4537·sin^5.7019 Ob}`, Ob capped at 61°. Tfzmin = 0.07 for single-plate fuzes, 0.05 for fuzes that integrate several spaced thin plates.
- **Single documented values:**
  - US Mk 21 in the 16″ Mk 8: **1.5″ at 0°, 0.375″ at 65°** (0.094 D → 0.023 D).
  - RN No. 159 in 16″ APC: **1″ mild steel at normal** (0.0625 D; DNO 1942). That was the explanation for *Rodney*'s 16″ passing through *Bismarck*'s superstructure.
  - The USN ADF relay starts on ≥0.075 D at any obliquity.
- **Implication:** the documented values fall off more steeply with obliquity (4:1) than Okun's formula (2.2:1). Treat Tfzmin as **fuze-specific, 0.06–0.10 D at normal**, with a fuze-specific obliquity factor.
- **Face-hardened plate starts any base fuze** unless the fuze is blinded.
- **Multi-plate start:** most German and Japanese fuzes of both wars could be started by several thin spaced plates; some British fuzes and US Mk 17–23 could not. Model it as a "fuze impulse" that integrates over plates (two-plate class) or resets (single-plate class).

### 6.3 Delay and burst distance

| Navy / fuze | Delay | Burst distance at 1,000 / 1,500 ft/s residual |
|---|---|---|
| Mechanical only / "non-delay" (RN No. 16 pre-1917, Bd.Z. "none", US Mk 17–20 inst.) | ~0.003 s | 3 / 4.5 ft |
| KM KV variants, US Mk 23 thin-plate setting | 0.010–0.015 s | 10–15 / 15–22 ft |
| RN No. 16D (Greenboy, WWII), KM WWI nominal, US Mk 23 late | 0.025 s | 25 / 38 ft |
| US Mk 21 BDF | 0.033 s | 33 / 50 ft |
| KM Bd.Z. 38 (C/38) | 0.035 s (0 / 0.015 selectable on some) | 35 / 53 ft |
| IJN Type 91 "diving" | 0.08–0.4 s quoted **[UNCERTAIN]**; Okun also says 0.025–0.035 black-powder elements were used | 80–400 / 120–600 ft |
| Russian 1894 tube | ~0.005 s; too weak for pyroxylin | — |

**Measured:**
- Baden 1921: a 15″ Greenboy burst **11.6 m (38 ft)** behind a 25 cm belt.
- RN interwar fuze trials: mean delays of 6.4–15.8 ft by design variant.
- German WWI base-fuzed HE travelled 2–6 m in unarmoured structure.

The project's held figure for the Hood 1919 test (34–40 ft) was not re-found, but Baden is consistent with it. Suggested delay scatter σ ≈ 20–30% of nominal. For German WWI fuzes, about half fell far from nominal.

### 6.4 What the fuze model decides

- **Over-penetration:** no plate ≥ threshold on the path → two holes, no burst. Covers big AP through destroyers and superstructure, *Rodney* vs *Bismarck*, and German 12″ through British ships at Jutland.
- **Early start:**
  - A thin plate ahead of the armour starts the clock: Iowa's 1.5″ bomb deck (intended for AP bombs), Bismarck's 50 mm upper deck (0.12 D vs a 16″ shell), Littorio's 70 mm outer belt (0.18 D, which also decaps).
  - The burst can then fall *on or above* the main armour deck instead of below it.
  - A 16″ shell started on Iowa's bomb deck at ~1,300 ft/s travels ~43 ft (9–15 m slant) before bursting **[INFERRED, 04 §5]**.
- **Late burst:** a long delay plus a narrow target means the burst is beyond the far side (IJN Type 91 against South Dakota: ~15–18 of 26 hits passed through).
- **In-plate burst:** a stopped shell with a started fuze bursts in the plate. The effect behind is plug, spall and blast through the hole only.

### 6.5 Fuze and filler failure by era (04 §6; 08 §3.3 table already in the project)

| Era | Pattern |
|---|---|
| 1894–1905 | Japanese Shimose burst on contact or water and in the bore. Russian wet pyroxylin was under-initiated; light Russian shells did little. Yellow Sea: ≥10 Japanese AP hits on armour, **0 penetrations** |
| Jutland | British: 1/13 effective on >9″, 3/17 clean on 6–9″, 6/17 burst while penetrating. German: ≤50% correct delay; many duds and low-order bursts |
| 1920s+ | RN target ≥90% full detonation with the CE booster. US, RN and KM fuze failures ~5–10% |
| 1941 | *Bismarck*'s shells mostly failed to function on water (unusual for German shells) |

The `OUTCOME` rows in `../damage-research/gunfire_ref.py` (PEN / PIB / LO / DUD / BRK by era) remain the right structure. This research supports them.

---

## 7. Obliquity, ricochet, decks, multiple layers (05)

### 7.1 Obliquity on a plate

Obliquity is the angle between the shell path and the plate normal. It is a vector calculation using:
- fall angle,
- target angle,
- plate inclination,
- **heel or roll** (Hoyer's German method uses all four),
- barbette curvature (offset b on radius R adds asin(b/R): P > 30° is 50%, P > 60° is 13%).

Example: an Iowa-type belt with 19° top-out slope, broadside-on, has obliquity 24° at 5° fall and 44° at 25° fall. At 45° target angle those become 50° and 62° (05 §1.1; `compound_obliquity()`).

### 7.2 Obliquity law

| Source | Thickness ratio T(Ob)/T(0) at fixed V |
|---|---|
| Thompson at T/D 1.0 | 0.94 (15°), 0.78 (30°), 0.58 (45°), 0.38 (60°), 0.19 (75°), i.e. cos^1.9 → cos^1.24 |
| Thompson at T/D 0.25 | 0.95, 0.83, 0.65, 0.43, 0.20, i.e. cos^1.35 → cos^1.19 |
| **Italian official 1942 KC tables (381 mm, 19–26 km)** | 0.976 (10°), 0.913–0.920 (20°), 0.81–0.82 (30°), 0.63–0.69 (40°) = **cos^1.5 within 1–2% up to 30°**; at 40° the table scatters around cos^1.5 (0.67). Thompson is ~3–4% steeper at 20–30° **[CALC §F]** |
| De Marre | cos^2.14: too steep |
| USN 1937 "4th power of secant" | Probably a transcription error |
| Rule of thumb from both | n ≈ 1.1 + 0.8·(T/D)·cos Ob [INFERRED] for homogeneous plate; ~1.5 for FH at ≤40° |

### 7.3 Ricochet and normalisation

- **Historical evidence:**
  - Pointed AP penetrates nose-first to about **65–75°**; above ~**75–80°** it glances regardless.
  - Between 65° and 80° the outcome depends on V/V_L: well above the limit it penetrates, often base-first; near it, it ricochets.
  - Shattered shells resist ricochet at 45–60°.
  - Measured: in the Hood 1919 test a shell left a 7″ belt deflected 9° *upward* and ricocheted off a 3″ deck at 79°.
- **The WoWS values the project noted** (ricochet 45–60°, 6–10° normalisation) **are game values, more aggressive than history.**
- **Normalisation** (Okun M79), turn toward the normal on perforation:

  | V/V_L | Turn at Ob 30–45° |
  |---|---|
  | 1.01 | 24–30° |
  | 1.1 | 15–18° |
  | 1.5 | 6–7° |
  | 2.0 | 3–4° |

  Scale ×[1 + (Ob − 45)/45] above 45°. Thin upper decks (< 0.08 D) turn shells only ~2–4° (Hood 1919: ~2° on a 2″ deck).
- **Slope-behind-belt (Bismarck)** is strongest against barely-penetrating flat-trajectory hits, because the belt turns the shell toward horizontal and raises its obliquity on the 22° slope (68° − fall + turn). It is weakest at long range. GKdos 100 combined the two as Vh² = Vh1² + Vh2².

### 7.4 Decks

- USN/navweaps deck columns are Thompson at Ob = 90° − fall **[CALC §A]**. At fall < ~15° (Ob > 75°) they ignore ricochet, so treat them as an upper bound.
- **The homogeneous models disagree on decks.** Okun M79 / Thompson [CALC §N]:
  - US STS: 1.1–1.16 at 30–50°, then **0.80–0.88 at 50–90°**. M79 says steep decks resist ~15–20% more.
  - German Wh deck rows: 0.91–1.0 above 60°.
  - Either can be argued. The immune-zone outer edge (§9) moves by ~1–3 km depending on the choice.
- **Iowa** (1.5″ STS bomb deck + 4.75″ Class B on 1.25″ STS + 0.625″ splinter deck) ≈ 6.1″ by Okun's spaced rule. **Bismarck** (50 mm Wh + ~12 mm + 80/95 mm) ≈ 4.4/4.9″ against 5.4/6.0″ of metal.
- **Thin upper plates** only start fuzes and decap. Concentrate the metal in the lower plate: in a constant-weight Hood-vs-Bismarck split, 1+4.15″ holds 3,300 yd longer than 2+3.15″.

### 7.5 Multiple layers

| Rule | 5″ + 2″ | Basis |
|---|---|---|
| Sum | 7.00 | — |
| Okun laminated (plates in contact) = (sum + spaced)/2 | 6.48 | Okun's own unvalidated compromise; his M79 gives 5.55–5.98 |
| Okun spaced = (ΣT^1.4)^0.714 | 5.96 | Valid for each plate 0.2–1.1 cal |
| USN "0.7 rule" (upper × 0.7 + lower) | 5.50 | Order-dependent; reported differently by Okun and Naval Gazing **[UNCERTAIN]** |
| **Stepping** (Thompson + LJ p=2 + DF) | per shot | Captures decap, fuze start, deflection |

**Test data:**
- **Watertown 1950:** spacing saves 30–50% weight *only if the shell breaks up*; 10–20% if decapped or yawed onto a *higher* obliquity; otherwise it is *worse* than solid.
- **US 14″ at 68°** (forum report of a USN test): 2+1+2″ spaced perforated at 1,250 ft/s against 1,690 ft/s for solid 5″ (ratio 0.74). Stepping gives 0.60 (0.64 with deflection capped at 3° on thin plates), so it under-rates spaced decks by ~15%; Okun's 1.4-rule gives 0.78 **[CALC §H]**.
- **Hood 1919 deck series** (15″ at 1,350 ft/s, 58°): 2″ HT → Okun 1,236 ft/s, model 1,234. Then 3″ / 5.5″ / 6″ main deck → Okun 1,093 / 714 / 657, model 997 / 531 / 344. In limit-velocity terms the model's main-deck limits are 735 / 1,122 / 1,198 ft/s against Okun-implied 577 / 1,009 / 1,047: **12–28% high**, amplified in the residual near the limit **[CALC §H]**. Thin HT decks at ~55° are the weakest part of the calibration.
- **Yaw** ≤10°: add ½ yaw to obliquity. Larger yaw = tumbling, unpredictable.

### 7.6 Edges, joints, repeated hits, underwater

- **Edge loss:** 15%·(1.5 − d)/1.5 within d < 1.5 cal of an unsupported edge.
- **Straight joints** are single-line failures.
- **Repeated hits:** no quantitative law found. Treat a previous hole as a free edge; cracked FH plate ≈ Q × 0.85–0.9 [INFERRED]. USN acceptance rejected radial cracks longer than 2 cal.
- **Underwater:**
  - Tosa 1924: a 41 cm shell 25 m short struck 3.3 m below the waterline and penetrated 76 mm.
  - Velocity in water ≈ V₀·e^(−x/L), L ≈ 50 cal for a flat-nosed Type 91 head, ~150 cal for a stable ogive (but ogives tumble).
  - Normal AP fuzes burst within ~10–15 m of water entry. `08` §3.8 already covers the effects.

---

## 8. A reasonable model, as a suggestion (not a decision)

This is what `armor_pen_ref.py` implements as `resolve_plate()` and `plate_effective_T()`. It is the simplest structure that reproduces the calibration targets in §9. Every constant is a tuning knob.

**Data**
```
Material {id, rho, fh, Q8, scale_k, face_frac, t_min, elongation, shatter_vs, years, nations}
Plate    {t, material, normal vector (or obliquity), gap_after, backing}
Shell    {D, W, cap: none|soft|hard1|hard2|ijn_head, cap_frac, windscreen, quality_class,
          nose, filler%, filler_type, fuze: {pos, tfz, tfz_ob_factor, delay, multi_plate, dud_p}}
State    {V, direction, yaw, capped, integrity: intact|nose_damaged|broken, fuze_started_at, path}
```

**Per plate**
1. **Obliquity** from geometry (fall, target angle, incline, heel, curvature). Add ½ yaw.
2. **Hard ricochet gate:** Ob ≥ 80° → ricochet. For 65–80°, ricochet if V/V_L < 1 + 0.08·(Ob − 65) [INFERRED]. Suppressed for broken shells.
3. **Effective thickness:** `T_eff = Q(material, D) · T · (1 − edge loss) / shell_quality`. Multiply by `shatter_bonus(Ob)` (1.4 → 1 at 55°) if FH and the shell will shatter. Backing at Okun weights.
4. **Limit:** `V_L = Thompson(T_eff, D, W, Ob)` (cos-extend above 75°).
5. **Fuze start:** if T ≥ Tfz(D, Ob), or the plate is FH, or two-plate integration reaches threshold.
6. **Decap:** check against the cap table. FH always decaps. Record the gap to the next plate.
7. **Outcome:**
   - V < V_L → stopped (in-plate burst if the fuze started).
   - V ≥ V_L → perforated. Holing-only band ≈ 0.88–0.95·V_L_eff for FH. Break-up probability peaks around 45° (~0.5 near the limit).
   - Shattered → fragments only (0.15× penetration).
8. **Exit:**
   - `V_r = cos(DF) · √(V² − (E4·V_L)²)`, with E4 = Okun's energy-NBL factor above 45°.
   - Rotate the path toward the normal by DF(Ob, V/V_L).
   - Remove the cap mass if decapped.
9. **Repeat** until stopped, burst (fuze delay × V along the path), ricochet, or a vital space is reached.

**Scatter:** plate Q ~ N(1, 0.06) per ship or lot; per-shot V_L ~ N(1, 0.03); delay σ 20–30%; dud and low-order rates by era (§6.5).

**Cost:** a handful of square roots per plate. Thompson's inverse (thickness from V) needs a bisection, but the game only ever needs the forward V_L.

**What to fit for the game.** In order of influence on outcomes:
1. shatter and break-up rules (state changes), including how much credit a decapping outer plate gets (§9 row 12);
2. FH Q(D) per nation;
3. fuze start threshold and delay;
4. the deck-obliquity law above 60° (Thompson vs M79);
5. ricochet band;
6. Q of pre-1895 materials (matters only for ironclad scenarios).

---

## 9. Calibration targets (unit tests)

| # | Case | Target | Current reference model |
|---|---|---|---|
| 1 | USN 16″/50 Mk 8 belt & deck, 10–40 kyd | navweaps table ±0.1″ | ✓ ±0.07″ (one deck cell 0.08″) [§A] |
| 2 | NPG 1946–47 standard NBLs (8″, 12″, 14″ at 30–35°) | reported NBL | ✓ within ~1.5% [§B] |
| 3 | Italian 381 KC obliquity ratios 0–40° | ≈cos^1.5 | Thompson 2–4% steeper [§F] |
| 4 | FH scaling: US Class A vs STS (NL) | 1.03 (8″) → 0.92 (16″) | by construction [§D, §M] |
| 5 | Spezia 1876: 22″ WI perforated by the 100-ton gun at 1,446 ft/s, ~590 fps to spare | perforate | ✓ V_L 1,383 (spare ~400 fps; Q 0.50 would give 590) |
| 6 | Annapolis 1890: compound perforated; steel and Ni-steel stop 6″/100 lb at 2,075 | ✓ / ✗ / ✗ | ✓ all three (all-steel only just: V_L 2,117 vs 2,075) [§G] |
| 7 | Indian Head 1893: 14″ Harvey stops uncapped 10″ at 2,059 | stop | ✓ (with shatter) [§G] |
| 8 | Beardmore 1901: 6″ KC uncapped limit 2,261; capped perforates at 1,945 | 2,261 / perforate | 2,020 (−11%) / ✓ [§G] |
| 9 | Baden 1921: 15″ Greenboy at 1,550 perforates 350 mm KC at 18.5°, stopped at 30° | ✓ / ✗ | perforates both; needs Greenboy break-up >20–25° [§L] |
| 10 | Hood 1919 deck series | 1,236 → 1,093 / 714 / 657 | 1,234 → 997 / 531 / 344 (main-deck limit 12–28% high) ✗ [§H] |
| 11 | 2+1+2″ STS at 68° vs solid 5″ | ratio 0.74 | 0.60–0.64 ✗ (Okun rule 0.78) [§H] |
| 12 | Iowa immune zone vs 16″/50 Mk 8 | 23.6–27.4 kyd (USN) | Bare belt (no outer-hull credit): effective penetration out to 28 kyd. Deck (with ricochet gate) penetrated from 26 kyd, or 28.75 kyd as one Thompson plate. So no zone without outer-hull credit. With the 1.5″ hull decapping and the shell then shattering on the Class A, the belt is never effectively penetrated. **The USN's 23.6 kyd sits between the two, i.e. ~10–15% credit for the outer hull.** This decap/shatter credit is the most important IZ knob ✗/? [§P] |
| 13 | Combat 1894–1905: thick belts | ~0 perforations | needs uncapped shatter plus era fuzes |
| 14 | Jutland British APC on >9″ | ~1/13 effective | needs the RN 1916 break-up and fuze rows |
| 15 | Fuze: 16″ Mk 21 | starts on 1.5″ at 0°, 0.375″ at 65° | Okun formula gives 1.12″ / 0.50″ [§I] |

---

## 10. What changes in the existing project docs (recommendations)

| Doc / code | Current | Recommendation |
|---|---|---|
| `05 §A3` "game simplification": G&D curve × cos(Ob) | Overshoots 6–28% at 15–45° | Use Thompson V_L, or keep a per-gun curve but with n(T/D, Ob) |
| `08 §3.2` / `gunfire_ref.pen_mm` | G&D × 0.967·cos^1.59 (belt), 0.82·cos^1.125 (deck), fitted to the 16″ Mk 8 | Consistent with Thompson *for that shell* (the fits are Thompson's local exponents). Replace with `thompson_vl` so other calibres and T/D come out right |
| `08` HE rule (0.2 cal × √cos) | OK | Keep; Okun 0.156 D contact-hole + 20% crack zone |
| `08 §3.2` spaced plates pre-combined with the 1.4 rule | Hides decap, fuze start, deflection | Step plate by plate (§7.5). Keep 1.4 as a check |
| `05 §A2` ricochet 45–60° (WoWS) | Too aggressive | 65–80° band gated on V/V_L; certain above 80° |
| `05 §A1` fuze arming 0.07 D | OK as default | Make Tfzmin and the obliquity factor fuze-specific (0.06–0.10 D) |
| `damage-model-research §4` "armour quality per nation" | One multiplier | Q(material, D) from §4.2 plus the shatter state |
| `docs/designer-ui/designer-ui-research.md §5.4` immune-zone readout | Waiting for the penetration model | Can be built now on Thompson + Q(D). Show both deck laws and with/without outer-hull decap credit as a band: the inner edge moves several kyd (§9 row 12) |
| `ship-cost-model` (Naval project) armour $/t | Materials not distinguished | 03 §2 has costs: steel $536/t (1887), Harvey/KC $545–680/t (1895–99), US cap $300/t (1897–99) |

---

## 11. Known gaps and conflicts

- **WWII-over-1900-KC gain:** Okun ~20%, Naval Gazing up to 40%, trials re-run through Thompson ~10–15%. It depends on the reference shell.
- **IJN Type 91 fuze delay:** 0.4 s vs 0.08–0.4 s vs actually 0.025–0.035 s, all in Okun's writings.
- **Deck law at >60° obliquity:** Thompson vs M79 differ by 15–20%.
- **Okun FACEHARD internals** are unpublished. His "Q" (8″ basis) and his 16″ outputs rank plates differently, which is the scaling effect.
- **Not retrievable online:** Hood 1919 primary results, Tosa 1924 tables, Ochta and Meppen KC series, HMS *Edinburgh* plate results, Jane's early penetration tables, the USN projectile-lot acceptance rule.
- **Russian and French armour quality factors:** no published values. Czech interwar plate: no data.
- **Repeated hits on the same plate / belts pushed in:** qualitative only.
- **13″ French Okun table:** all five plates come out ~25% low against Thompson [CALC §D]. Probably a transcription or criterion issue; excluded from the fits.
- **Transcription risk:** the Okun tables were pulled through a page summariser. Spot-check a few cells against the live pages before a final fit (07 notes).

---

## 12. Key sources

- **Okun (navweaps):**
  - [Historical formulae](http://www.navweaps.com/index_nathan/Hstfrmla.php)
  - [Misc. armour formulae](http://www.navweaps.com/index_nathan/Miscarmr.php)
  - [M79 doc](https://navweaps.com/index_nathan/M79apdoc.php)
  - [HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)
  - [AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)
  - [Metallurgy table (2009)](https://www.navweaps.com/index_nathan/metalprpsept2009.php)
  - [US/British/German plate tests 1946–47](https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php)
  - [O.S. 751](https://navweaps.com/index_nathan/os751BalTests.php)
  - [Penetration tables index](https://www.navweaps.com/index_nathan/Penetration_index.php)
  - [Multi vs single plate](https://www.navweaps.com/index_nathan/multiVsSinglePlate.php)
  - [Hood 1919 tests](https://www.navweaps.com/index_nathan/HOOD%201919%20Armor%20Tests.pdf)
  - [British fuzes after Jutland](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php)
  - [German WWII fuzes](https://navweaps.com/index_nathan/germanWWIIFuzes.php)
  - Tech boards: [017](https://www.navweaps.com/index_tech/tech-017.php), [033](https://www.navweaps.com/index_tech/tech-033.php), [040](https://www.navweaps.com/index_tech/tech-040.php), [041](https://www.navweaps.com/index_tech/tech-041.php), [055](https://www.navweaps.com/index_tech/tech-055.php), [085](https://www.navweaps.com/index_tech/tech-085.php), [109](https://www.navweaps.com/index_tech/tech-109.php)
  - [Bismarck armour](https://www.combinedfleet.com/okun_biz.htm)
- **USN texts:**
  - *Naval Ordnance* 1937, ch. XII (armour): [A](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php), [B](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-B.html), [C](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)
  - *Naval Ordnance and Gunnery* 1957, ch. 4: [4](https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php), [4-B](https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html)
  - *History of Armor Plate*: [PDF](https://www.eugeneleeslover.com/AMMUNITION/History_of_Armor_Plate.pdf)
- **Period and trial sources:**
  - [EB1911 "Armour Plates"](https://gutenberg.org/cache/epub/34162/pg34162-images.html)
  - USNI Proceedings: [1890 Annapolis board](https://www.usni.org/magazines/proceedings/1890/december/report-board-competitive-trial-armor-plates), [1894 Johnson shot](https://www.usni.org/magazines/proceedings/1894/january/johnson-cast-steel-armor-piercing-shot), [1894 Harvey notes](https://www.usni.org/magazines/proceedings/1894/october/professional-notes)
  - [Appleton 1880 "Armor"](https://www.eugeneleeslover.com/ENGINEERING/Fullam/ARMOR_Article_Appletons_1880.pdf)
- **Naval Gazing:**
  - Armor: [Part 1](https://www.navalgazing.net/Armor-Part-1), [Part 2](https://www.navalgazing.net/Armor-Part-2), [Part 3](https://www.navalgazing.net/Armor-Part-3), [Part 4](https://www.navalgazing.net/Armor-Part-4)
  - Shells: [Part 2](https://www.navalgazing.net/Shells-Part-2), [Shells at Jutland](https://navalgazing.net/Shells-at-Jutland)
  - Fuzes: [Part 2](https://www.navalgazing.net/Fuzes-Part-2), [Part 3](https://www.navalgazing.net/Fuzes-Part-3)
- **DTIC:**
  - [Watertown spaced armour 1950](https://apps.dtic.mil/sti/pdfs/ADA954865.pdf)
  - [Zener 1942](https://apps.dtic.mil/sti/pdfs/ADA953652.pdf)
  - [NPG 1947](https://apps.dtic.mil/sti/tr/pdf/ADA955277.pdf)
  - [RD gaine 1952](https://apps.dtic.mil/sti/pdfs/ADA492806.pdf)
  - [ARL shatter gap 1997](https://apps.dtic.mil/sti/pdfs/ADA335629.pdf)
- **German practice (kbismarck):** [GKdos 100](https://kbismarck.com/gkdos100a.html), [Hoyer](https://kbismarck.com/hoyer-warship-armor.html)
- **Detailed per-claim citations** are in files 01–07.
