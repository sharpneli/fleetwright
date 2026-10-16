# 13 — Fragmentation: how many fragments, what masses and speeds, in what pattern

*Research for the damage model, after `12_explosive_fillers.md`. It answers the fragmentation question: how many
fragments a burst throws, of what masses and speeds, and in what pattern (the side spray, the nose slug, the base plug).
Today that is one Okun table (08 §3.6, `Effects.FragPenMm`), which 08 marks **[UNCERTAIN]** because it came through a
page summariser. It drives splinter protection, near misses and service-network cuts. 12 covers the explosive, which is
half of fragmentation; this note adds the other half, the shell's wall and its steel, and joins the two.*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the anchors, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion code:** `fragmentation_ref.py` (pure Python, runs in 0.4 s). Every table in §4 comes out of it.

---

## 0. Short answer

1. **Fragmentation can be generated from numbers the shell block already carries,** instead of looked up in a table.
   It needs calibre, mass, filler mass, the explosive (12's catalogue) and one new field, the **wall steel**. Four
   standard engineering laws do the work, each with sourced constants:
   - **Gurney** gives the fragments' launch speed from the charge-to-metal ratio of the body.
   - **Mott** gives their number and mass distribution from the wall's thickness, its bore and the explosive.
   - **A drag law** says how fast they slow down: V = V₀·e^(−R / (247·m^⅓)), with R in m and m in kg.
   - **THOR** (already in armor/01 §3.10) says what plate each one gets through.
2. **The model reproduces the anchors without being fitted to them:**
   - A 6″ HC's side spray comes out at 3,070 ft/s. WDR 51 measured about 3,200 ft/s.
   - The largest body piece of an IJN 8″ AP comes out at ~3.7 kg. *Boise*'s recovered piece was "about 2 inches by 4
     inches", ~2.4 kg in that wall.
   - Okun's 8″ HE table: the median and the top 1% match within ~10% from 4 to 20 m. The 10% column runs ~35% low.
   - WDR 51's splinter rule: 25 lb STS is "the minimum… reasonably effective". The model lets 1–3% of 5″–6″ HC
     fragments through 25 lb, and 25–40% through 10 lb.
3. **08's uncertain summary was partly wrong.** A re-read of Okun's page (§1) shows:
   - **The "600 ft/s" is not a fragment speed.** It is an index in Okun's crude method, used with 0.15× a plate formula.
     Real side-spray speeds are 2,000–3,600 ft/s for HE and 1,200–1,900 ft/s for AP.
   - **"4,000 fragments for AP" is the wrong way round.** AP's thick walls and small charge give a few hundred heavy
     pieces, not more than HE's ~1,000. Okun's own note and *Boise* agree with the physics.
   - **The pattern holds.** About 85% of the mass flies in a thin ring square to the shell's path ("within an arc of 15
     degrees", WDR 51). The AP nose (25–33% of the shell) carries on as a slug. The base plug stays whole.
4. **The ring matters more than the radius.** A part 5 m away in the ring gets ~6× the fragments per m² it would get
   from an even spread. A part 5 m away and 30° off the ring gets almost none. The ring leans forward by the shell's
   own speed: ~30° for HE bursting on contact, up to ~45° for AP bursting while still moving.
5. **What the two halves decide:**
   - **The charge-to-metal ratio decides speed.** HC (C/M 0.1–0.18) throws fragments at 750–950 m/s, AP (0.025–0.05)
     at 370–580 m/s.
   - **The wall's thickness decides size.** A 16″ AP throws ~300 pieces averaging 2 kg; a 6″ HC throws ~800 ≥1 g,
     averaging 25 g.
   - **The explosive** (brisance) moves fragment count by about ±2× and speed by about ±15%. Black powder is the
     outlier: a few dozen slow chunks.
   - **The steel** moves size by about ±1.5×. Cast iron gives fine, slow pieces.
6. **For the game:**
   - **Precompute** a fragment table per shell when the design compiles: ~6 mass classes plus the two single pieces.
   - **At burst time,** a part's expected hits are density × presented area. A per-class THOR check through its box
     gives the penetrating hits.
   - **Near misses** are the same burst with the water as a floor (§5.2). **Cables and pipes** are line targets, and their
     cut chance needs no band width (§5.3).

---

## 1. What 08 had, re-read

08 §3.6's table and bullets came from Okun's *Miscellaneous Naval Armor Formulae* §6
([navweaps](https://navweaps.com/index_nathan/Miscarmr.php)) through a summarising fetch. This session re-read the
section with narrow questions. The table itself is confirmed. It is an 8″ HE test: a 240 lb, over 6.5%-TNT, WWII
US Army nose-fuzed shell, set on its base at the centre of a ring of RHA plates [SRC]. Distance is in calibres; each
column is the STS thickness defeated by the best 50%, 10% and 1% of the fragments:

| Distance (cal) | Feet | Fragments left | 50% defeat (cal) | 10% defeat (cal) | 1% defeat (cal) |
|---|---|---|---|---|---|
| 5 | 3.3 | 2,000 | 0.110 | 0.110 | 0.110 |
| 20 | 13.3 | 1,900 | 0.0213 | 0.093 | 0.106 |
| 50 | 33.3 | 1,760 | 0.0158 | 0.075 | 0.100 |
| 100 | 66.7 | 1,600 | 0.0113 | 0.057 | 0.091 |
| 500 | 333 | 1,040 | 0.0011 | 0.0186 | 0.0464 |
| 1,000 | 667 | 800 | 0.0006 | 0.012 | 0.023 |

At 5 calibres every column reads 0.110: that is the contact value (blast plus fragments), not a fragment spread.
Okun's contact rule is +142%, or 0.156 cal for HE [SRC].

What the re-read changes in 08's bullets:

| 08 said | The page says | Verdict |
|---|---|---|
| ~2,000 / 3,000 / 4,000 fragments for HE / SAP / AP | 2,000 middle-body fragments in the 8″ HE test; SAP and AP as +50% and +100% | **The HE count stands. The AP scaling contradicts Mott and *Boise*** (§4.1, §4.2) **[UNCERTAIN reading]** |
| Side spray in a thin ring | Within ~10° up and down, most within 5° [SRC]. WDR 51: "nearly all… within an arc of 15 degrees" [SRC] | **Confirmed, ±7.5°** |
| AP nose ≤33% carries on as a slug | Nose piece ~33% (AP) and ~25% (common/SAP); up to 38% / 30% when the shell doesn't detonate; HE/HC ~2% [SRC] | **Confirmed, with numbers** |
| Base plug shields a rear 90° cone | It stays whole, is "stopped or slowed by the blast pressure", falls, and does not get through decks or bulkheads [SRC] | **Confirmed in part** (§2.5: true only when the shell is slow at the burst) |
| Low-order / black powder: 60° forward cone at ~600 ft/s | Black-powder CPC middle-body pieces at up to ~600 ft/s, in a ~60° cone around the nose's path, ~1,000 ft/s forward with the shell's motion [SRC] | **Confirmed, and Gurney agrees** (§4.2: 146–254 m/s) |
| (implied) fragments at ~600 ft/s | The 600 / 540 / 478 ft/s "sideways velocities" (HE / SAP / AP) are inputs to a crude method. They are added to the shell's remaining speed by Pythagoras and fed into a homogeneous-plate formula × 0.15 [SRC] | **An index, not a speed.** Measured: 6″ HC ~3,200 ft/s (WDR 51) |

Okun's method is right in structure: fragment speed is the vector sum of the shell's motion and the burst's sideways
kick, applied plate by plate with linear subtraction. Its numbers are a fit for penetration, not physics. That is
why the table can be trusted while 08's "fragment speed" can't.

---

## 2. The physics

### 2.1 Speed: Gurney

A cylinder of metal mass M around charge C throws its pieces at

`V₀ = √(2E) · √[ (C/M) / (1 + C/(2M)) ]`

`√(2E)` is the explosive's Gurney constant, roughly a third of its detonation velocity [SRC]:

| Explosive | √(2E), m/s | Source |
|---|---|---|
| TNT | 2,438 | IATG 01.80 Table 21 [SRC] |
| Comp B | 2,774 | [SRC] |
| Tetryl (CE) | 2,499 | [SRC] |
| Black powder | 640 (90th percentile of fragments); 816 for the fastest | MSIAC L-295 [SRC] |
| Lyddite / Shimose / Shellite / TNA / Explosive D / TNT-wax / amatols / wet guncotton | 2,550 / 2,600 / 2,450 / 2,440 / 2,350 / 2,300 / 2,150 & 1,900 / 2,100 | Scaled by detonation velocity [EST] |

Two corrections:

- **Cast iron** reaches only ~80% of Gurney, because it breaks before the gas has finished pushing [SRC].
- **Only the cylindrical body counts.** The ogive and base are thick and hold little charge. Using the whole shell's
  C/M underestimates the side spray by ~10%. Using the body's own (80–90% of the filler against 55–65% of the
  metal; §3.2) reproduces WDR 51's 3,200 ft/s for the 6″ HC within 4% [FIT check, not fitted].

WDR 51's rule of thumb, `v ≈ 10,000·√(charge/weight)` ft/s [SRC], gives 3,550 ft/s here, ~10% high.

### 2.2 Number and size: Mott

Mott's distribution for a naturally fragmenting cylinder [SRC IATG 01.80 Tables 22–23]:

```
N(>m) = N₀ · exp(−√m / M_k)          N₀ = M / (2·M_k²)       mean mass = 2·M_k²
M_k  = B · t^(5/6) · d^(1/3) · (1 + t/d)       (lb^½, t = wall and d = bore in inches)
```

- **B is the explosive's constant, for mild/medium steel:** TNT 0.0779, Comp B 0.0554, tetryl 0.0682 lb^½ in^-7/6
  [SRC].
- **The TNT value checks out against NOL's cylinders.** For cast TNT in AISI 1045 tubes (2.0″ bore, 0.25″ wall), the
  measured mean of fragments over 1 grain was 17.5 grains. B_TNT gives 17 [SRC → check].
- **The same NOL work gives Comp B 12 grains, Tritonal 17 and 80/20 amatol 45** [SRC]. Amatol's fragments are ~2.6×
  heavier, so its B is ~1.6× TNT's.
- **The wall's thickness dominates.** Mean mass goes roughly as t^(5/3) and d^(2/3). That is why an AP shell (wall
  ~0.3 calibre) throws pieces ~100× heavier than an HC (wall ~0.13 calibre).
- **Mott undercounts the fines** (Okun's 8″ test counted 2,000; Mott gives ~800 of them heavier than 1 g). The fines
  carry little mass and little punch, so the model counts fragments of 1 g and up.

### 2.3 Flight: drag

`V(R) = V₀ · exp(−R / L)`, with `L = L₁ · m^⅓` and L₁ = 247 m/kg^⅓. That is C_D 1.28 with the shape factor of
forged-steel shell fragments, k = 2.6 g/cm³ [SRC DDESB TP-12]. A fragment loses 1/e of its speed in 53 m at 10 g,
115 m at 100 g and 247 m at 1 kg.

**In water** the same law gives L₁ ≈ 0.3 m/kg^⅓ [EST]: even a 1 kg piece is stopped within a metre. WDR 51: fragments
of near misses detonating in the water "quickly lost velocity and were not a source of serious damage" [SRC]. That is
bombs, which burst below the surface. A contact-fuzed shell bursts at the surface, and its upper half-spray flies
(§5.2).

### 2.4 Penetration: THOR

THOR's limit velocity for a compact steel fragment against plate (armor/01 §3.10) [SRC]:

`V₅₀ = 10^c₁ · (e·A)^0.906 · m^−0.963 · sec^1.286 θ`  (fps, in, in², grains; mild steel c₁ = 6.523)

- **Presented area** is A = (m/k)^⅔, with k = 660 grains/in³ [SRC TP-12].
- **STS** takes THOR's hard-vs-mild offset, about +19% on V₅₀ [EST].
- **Validity:** THOR was fitted to fragments of 5–825 grains (0.3–53 g). The heavy AP and black-powder chunks
  (0.2–20 kg) are an **extrapolation [UNCERTAIN]**. Sanity check: it puts a 16″ AP's top 10% through ~0.9″ of STS at
  5 m, close to what a 2–20 kg lump at 400 m/s does by a plugging-energy estimate.

### 2.5 The pattern: ring, lean, nose slug, base plug

The shell breaks into four groups:

| Group | AP | Common / SAP / CPC | HE / HC (nose fuze) | Flies |
|---|---|---|---|---|
| Nose, whole | 33% of the casing [SRC] | 25% [SRC] | 2% (fuze) [SRC] | Forward along the path |
| Ogive, broken (nose spray) | — | — | 20% [EST] | Forward cone, 30–80° off the axis [EST] |
| Cylindrical body (side spray) | 55% | 63% | 63% | Ring ±7.5° square to the path [SRC], leaning forward |
| Base plug, whole | 8% | 8% | 7% | Along the path, slowed |
| Base, broken (tail spray) | 4% | 4% | 8% | Backward cone, slow [EST] |

The body shares are [EST]. The War Thunder developers' version (nose ≤15%, middle ≤75%, rest at the rear, by count)
is the same shape ([forum](https://forum.warthunder.com/t/naval-projectile-fragmentation-damage-what-s-new/91679)).

**The lean.** A fragment's velocity is the shell's velocity at the burst plus its Gurney kick. The ring becomes a cone
around the path with half-angle atan(V₀ / v_shell). Okun's crude method has this structure, and he notes the cone is
"tilted forward slightly" [SRC].

| Burst | Shell's speed | Lean forward of square |
|---|---|---|
| 6″ HC, static | 0 | 0° |
| 6″ HC on contact | ~500 m/s | ~28° |
| 16″ AP after its delay, slowed by plates | ~150 m/s | ~22° |
| 16″ AP still fast | ~350 m/s | ~43° |

The detonation wave's own sweep (the Taylor angle, V₀/2D) adds only 2–4°. Ignore it.

**The nose slug (AP, SAP).** The nose piece is 40–400 kg of hardened steel. The burst gives it only +10–20 m/s
[Gurney end-plate, EST]. It carries on at the shell's speed at the burst, as a blunt, broken penetrator. The terminal
walk should run it as a shell with no filler, a blunt nose, quality ~0.6 [EST] and a diameter ~0.75 calibre.

**The base plug.** The plug (10–100 kg) is pushed back by 40–90 m/s for AP and ~240 m/s for HC [EST, Gurney end
plate].

- **AP burst after its delay, slowed by plates or machinery (v_r ≲ 100 m/s):** the plug ends at 0–60 m/s, "stopped or
  slowed", falls and damages locally. That is Okun's description [SRC].
- **AP still fast (v_r ~300 m/s):** the plug carries on at 200–260 m/s, enough to hole light bulkheads.
- **Evidence:** *Ralph Talbot*'s report identified 8″ hits by base plugs 5.6″ across, recovered inside the ship
  ([WDR 51](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html))
  [SRC]. The plugs survive whole.
- **HC:** the plug is thrown back. Treat it as a heavy fragment in the tail cone.

**Low-order and black powder.** Okun: "fewer, slower, larger pieces", in a ~60° forward cone [SRC]. The model makes a
low-order burst 0.4× Gurney with 3× B [EST], and black powder uses its own constants (§3.1).

---

## 3. The two halves

### 3.1 The explosive: brisance gets a definition

12's catalogue has a `brisance` column marked [EST]. This note gives it a meaning: **brisance = B_TNT / B_Mott**, so
mean fragment mass goes as 1/brisance². Two values check against independent data:

| Explosive | 12's brisance | From data | Data |
|---|---|---|---|
| Amatol 80/20 | 0.65 | 0.62 | NOL mean fragment 45 vs 17.5 grains [SRC] |
| Black powder | 0.15 | ~0.16 | L-295 Mott constant 3.6 g^½ cm^-5/6, strong confinement (units as printed; [UNCERTAIN] conversion) [SRC] |
| Comp B | — | 1.41 | IATG B 0.0554 [SRC] |

12's estimates land within 5% of both. Adopt the definition and keep the column.
**Add a Gurney constant** to 12's catalogue (§2.1 table). Fragment speed then comes from the catalogue too.

What changing only the filler does, in a 6″ HC body (T9 below):

| Filler | V₀ m/s | Fragments ≥1 g | Mean g | Through 10 lb STS at 5 m (count) |
|---|---|---|---|---|
| Black powder | 254 | 28 | 906 | 9 |
| Amatol 80/20 | 756 | 443 | 48 | 174 |
| Explosive D | 934 | 786 | 25 | 311 |
| TNT | 969 | 940 | 20 | 358 |
| Lyddite | 1,014 | 1,187 | 15 | 427 |
| Shimose | 1,034 | 1,272 | 14 | 450 |
| Comp B | 1,103 | 1,644 | 10 | 560 |
| Lyddite in a cast-iron body | 811 | 2,592 | 5.5 | 211 |
| Explosive D, low-order | 374 | 105 | 226 | 29 |

- **Picric fillers throw ~50% more fragments that get through light plate than TNT does,** and ~40% more than
  Explosive D. That is the "fine splinters" of Shimose at Tsushima (12 §3).
- **Amatol halves the effective splinters.** That is the cost of the 1915 shortage choice, beyond its blast (12 rated
  amatol's blast within 5–10%, but its fragments are clearly worse).
- **Black powder makes a handful of big slow chunks.** Its damage is fire and the wreck radius, not splinters.

### 3.2 The wall: its geometry and its steel

**The geometry comes free.** The body's charge-to-metal ratio fixes the bore: with x = (d/D)², x = (C/M·ρ_s) /
(ρ_e + C/M·ρ_s), using steel 7.85 and filler 1.6 g/cm³. The wall t = (D − d)/2 follows. No new design field is needed.

| Shell | Wall | Body C/M | V₀ | Fragments ≥1 g | ≥50 g | Mean | Top 1% ≥ | Nose kg | Plug kg |
|---|---|---|---|---|---|---|---|---|---|
| 5″/38 AAC Mk 35 | 20 mm | 0.181 | 957 m/s | 612 | 70 | 16 g | 0.17 kg | 0 | 2 |
| 6″/47 HC Mk 34 | 25 | 0.172 | 934 | 786 | 142 | 25 | 0.27 | 1 | 3 |
| 8″ Army HE (Okun's test) | 46 | 0.085 | 698 | 680 | 261 | 81 | 0.85 | 2 | 7 |
| IJN 20 cm common | 47 | 0.083 | 689 | 767 | 299 | 83 | 0.88 | 2 | 8 |
| IJN 20 cm Type 91 AP | 60 | 0.041 | 492 | 264 | 150 | 233 | 2.5 | 41 | 10 |
| USN 8″ AP Mk 21 | 68 | 0.025 | 370 | 182 | 120 | 422 | 4.5 | 49 | 12 |
| 14″ HC | 74 | 0.106 | 746 | 1,026 | 625 | 300 | 3.2 | 11 | 37 |
| 15″ APC Mk Ia (lyddite) | 104 | 0.053 | 580 | 701 | 497 | 626 | 6.6 | 278 | 67 |
| 16″ AP Mk 8 | 136 | 0.025 | 370 | 303 | 251 | 2,126 | 23 | 398 | 97 |
| 16″ HC Mk 13 | 85 | 0.105 | 741 | 1,126 | 738 | 413 | 4.4 | 16 | 55 |
| 46 cm Type 0 HE | 123 | 0.057 | 572 | 997 | 733 | 780 | 8.3 | 26 | 91 |
| 18″ CPC (black powder) | 95 | 0.107 | 204 | 45 | 42 | 19 kg | 205 | 349 | 112 |
| 6″ cast-iron common, powder (1880s) | 35 | 0.085 | 146 | 39 | 28 | 657 g | 7.0 | 11 | 3 |

**The steel needs one new field** (`shell.steel`; Mott's B and Gurney's V get multipliers):

| Steel | × B (size) | × V | Basis |
|---|---|---|---|
| Cast iron (to ~1890 common shell) | 0.60 | 0.80 | V [SRC]; size [EST]. Makins (1901): cast-iron pieces "granular… comparatively rounded" with "a lesser degree of penetrative power"; forged-steel lyddite pieces "sharp knifelike" ([Anglo-Boer War](https://angloboerwar.com/books/makins-surgical-experiences/makins-chapter-xii-on-shell-wounds)) [SRC] |
| Cast steel / semi-steel (wartime substitute) | 0.85 | 0.95 | [EST] |
| Forged carbon (~AISI 1045; HE, HC, common) | 1.00 | 1.00 | The Mott constants' own basis [SRC] |
| Ni-Cr heat-treated AP / SAP body | 1.25 | 1.00 | Tougher steel breaks coarser (Grady-type scaling with fracture toughness) [EST] |
| HF-1 high-fragmentation steel (1950s+) | 0.65 | 1.00 | [EST]; NSWC tested it against Armco iron (Mock & Holt 1981), numbers not reached |

The steel moves fragment size by ~±1.5×. The wall's thickness moves it by ~100×. **The kind of shell matters far
more than its metallurgy.** The steel field earns its place mainly for cast iron (the 1880s) and for "silly shells".

---

## 4. Results

### 4.1 Okun's table, regenerated

8″ HE, static burst. STS defeated by the best 50% / 10% / 1% of side-spray fragments, in calibres (T3):

| Distance | Model 50% | Model 10% | Model 1% | Okun 50% | Okun 10% | Okun 1% |
|---|---|---|---|---|---|---|
| 20 cal (4 m) | 0.023 | 0.061 | 0.106 | 0.021 | 0.093 | 0.106 |
| 50 cal (10 m) | 0.021 | 0.058 | 0.103 | 0.016 | 0.075 | 0.100 |
| 100 cal (20 m) | 0.018 | 0.054 | 0.098 | 0.011 | 0.057 | 0.091 |
| 500 cal (100 m) | 0.005 | 0.029 | 0.067 | 0.001 | 0.019 | 0.046 |
| 1,000 cal (200 m) | 0.001 | 0.014 | 0.042 | 0.0006 | 0.012 | 0.023 |

- **Good match:** with no fitting, the median and the top 1% agree within ~10% where it matters (4–20 m).
- **Where they differ:** the model's 10% column is ~35% low near the burst, and its small fragments keep their speed
  a little longer.
- **Verdict:** Okun's table is a fair summary of one shell. The generator reproduces it and extends it to every other
  shell, filler and steel.

### 4.2 Calibration status

| Anchor | Model | Status |
|---|---|---|
| 6″ HC fragment speed, WDR 51 ~3,200 ft/s | 3,066 ft/s | **−4%** |
| *Boise* hit 5, IJN 8″ AP: largest piece "about 2 inches by 4 inches" (~2.4 kg in its wall) ([WDR 24](https://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCL47/WarDamageReportCL47.html)) | Largest ~3.7 kg | Consistent |
| Black-powder CPC middle-body pieces, Okun ≤600 ft/s (183 m/s) | 146–254 m/s | Consistent |
| WDR 51: 25 lb STS minimum effective; 30 lb at important stations; 10 lb against distant bursts (beyond 50 ft) | 25 lb passes 1–3% of 5″–6″ HC fragments at 3–15 m; 10 lb passes 23–34% | Consistent |
| *Boise*: 30 lb STS "rejected all the fragments" (hit 2); 10 lb STS 19 ft from hit 1 "struck… not penetrated" | 30 lb passes ~3% of IJN 8″ common at 3–5 m. 10 lb passes ~40% *in the band* | Hit 2 fits. Hit 1's plate was 19 ft *above* the burst, likely outside the ring (§2.5) |
| Okun 8″ HE table | §4.1 | Within ~10% (two columns), −35% (one) |
| Okun's 3,000 / 4,000 fragments for SAP / AP | ~260–770 ≥1 g | **Rejected** (§1) |

### 4.3 Splinter plating

Share of side-spray fragments (≥1 g) getting through STS at normal incidence, static burst (T4; R in metres):

| Shell | R | 10 lb (6 mm) | 20 lb | 25 lb (16 mm) | 30 lb | 40 lb | 1.5″ |
|---|---|---|---|---|---|---|---|
| 5″/38 AAC | 5 | 32% | 5% | 1% | 1% | 0 | 0 |
| 5″/38 AAC | 15 | 23% | 3% | 1% | 0 | 0 | 0 |
| 6″ HC | 5 | 39% | 8% | 3% | 1% | 0 | 0 |
| IJN 8″ common | 5 | 44% | 12% | 5% | 3% | 1% | 0 |
| 14″ HC | 5 | 66% | 35% | 24% | 18% | 8% | 1% |
| 16″ HC | 5 | 71% | 42% | 30% | 23% | 12% | 2% |
| 16″ AP (static) | 5 | 66% | 35% | 24% | 18% | 8% | 1% |
| 18″ CPC (powder) | 5 | 71% (~30 pieces) | 45% | 33% | 26% | 15% | 4% |

What the table says:

- **Against destroyer and cruiser HE (5″–8″),** 25–30 lb STS is the knee. Below it, a third or more come through;
  above it, almost nothing. That is WDR 51's rule from first principles.
- **Against capital-ship shells,** splinter plating has to be 1–1.5″. The heavy pieces of a 14″–16″ burst go through
  anything thinner. That matches the 1–1.5″ splinter decks and bulkheads of 1930s–40s battleships.
- **The shares barely fall with distance inside a ship.** The heavy fragments keep their speed for hundreds of metres.
  Distance protects by spreading them (density ∝ 1/R²), not by slowing them.

### 4.4 Density, and cables and pipes

In the side band the density is `n / (4π R² sin 7.5°)`, about 6× an even spread. For a **line target crossing the
band** (a cable run, a pipe, a vent trunk), expected hits come out independent of the band's width:

`hits = n · w / (2π R)`  (w = the line's width)

Cut chance of a 5 cm cable run, counting fragments ≥5 g still above 150 m/s (T5) [EST criterion]:

| Shell | 2 m | 5 m | 10 m | 20 m |
|---|---|---|---|---|
| 5″/38 AAC | 0.79 | 0.47 | 0.27 | 0.15 |
| 6″ HC | 0.89 | 0.59 | 0.36 | 0.20 |
| IJN 8″ common | 0.92 | 0.64 | 0.40 | 0.22 |
| 16″ AP | 0.69 | 0.37 | 0.21 | 0.11 |
| 16″ HC | 0.98 | 0.80 | 0.55 | 0.33 |

That fits the record:

- *Boise*: "numerous cables were cut… by fragments from the various hits" [SRC].
- *Ralph Talbot*: firemains severed and wireways cut by 8″ fragments [SRC].
- *Johnston*: "fragments cut all the wireways on the port side" [SRC].
- High-pressure steam piping showed "surprising resistance to blast and fragments" [SRC]. Thick-walled pipe needs the
  THOR check, not just a hit.

### 4.5 The lean, and AP bursting on the move

Fragment speed and penetration at 5 m by the shell's speed at the burst (T6):

| Shell | v_shell | Fragment speed | Lean | 50% / 10% defeat, STS |
|---|---|---|---|---|
| 6″ HC | 0 | 934 m/s | 0° | 3.8 / 10.4 mm |
| 6″ HC | 500 | 1,060 | 28° | 4.4 / 12.0 |
| 16″ AP | 0 | 370 | 0° | 8.8 / 22.7 |
| 16″ AP | 350 | 510 | 43° | 12.5 / 32.3 |
| IJN 20 cm T91 AP | 300 | 576 | 31° | 5.8 / 15.2 |

**An AP shell that bursts before it has slowed throws a forward cone** whose fragments hit ~40% harder than a static
burst's. This is Okun's 60° forward arc for fragments against bulkheads and decks. A long-delay shell (IJN Type 91)
that bursts after it stops throws a square ring instead.

---

## 5. Uses in the game

### 5.1 Splinter protection (E3)

Precompute per shell at design compile:

- **Side spray:** ~6 Mott mass classes (count, mass), all at V₀, leaning by v_shell at the burst.
- **Nose spray:** HE only, the ogive's 20% at the ogive's own V₀, in a forward cone.
- **Tail spray:** small and slow.
- **Two single pieces:** the nose slug and the base plug, each with a mass and a kick.

At burst time, for each part in reach:

1. **Angle off the band:** within ±7.5°, the full band density. Outside, 0.15 of the fragments spread over the rest of
   the sphere [EST from "nearly all within 15°"].
2. **Expected hits per class** = density × the part's presented area, then a Poisson draw.
3. **Through its box:** each hit steps through the part's plates with THOR (`fragments` class per material, plan
   P0), linear subtraction (Okun).

This replaces `Effects.FragPenMm` and stays cheap: tens of multiplies per part.

### 5.2 Near misses (E3, the new `Hit` source)

**The physics.** A contact-fuzed shell bursting on the surface sends only its upper half-spray into the air. Fragments
going down stop within a metre (§2.3).

**Who throws splinters.** Base-fuzed AP and delay shells dive and burst under water: **no splinters, only underwater
effects**. Near-miss splinters are an HE / nose-fuze thing, which is the record (Samar, Barents Sea).

Expected fragment hits / holes (≥1 g) through an 8 mm mild-steel destroyer side (7 m high, 110 m long) (T8):

| Shell (striking v, fall) | Abeam 10 m | 25 m | 50 m | Short 10 m | 25 m | 50 m |
|---|---|---|---|---|---|---|
| 5″/38 AAC (300 m/s, 20°) | 69 / 10 | 31 / 4 | 14 / 1 | 119 / 13 | 37 / 5 | 16 / 1 |
| 6″ HC (400, 15°) | 82 / 18 | 35 / 7 | 16 / 2 | 162 / 28 | 59 / 12 | 17 / 3 |
| IJN 20 cm common (450, 15°) | 106 / 18 | 46 / 7 | 20 / 2 | 307 / 65 | 125 / 25 | 34 / 8 |
| 14″ HC (480, 15°) | 133 / 55 | 57 / 22 | 26 / 10 | 377 / 169 | 153 / 65 | 43 / 23 |
| 46 cm Type 0 (480, 15°) | 120 / 54 | 51 / 21 | 24 / 10 | 496 / 301 | 178 / 94 | 72 / 39 |
| 46 cm Type 0 (480, 35°) | 93 / 24 | 47 / 14 | 1 / 0 | 325 / 205 | 276 / 172 | 103 / 53 |

"Abeam" is a line error, the ship lying along the line of fire. "Short" is a burst short, the ship across the line.

- **A heavy HE burst 25–50 m short of a destroyer still puts tens of holes in her side.** 08 guessed 10–20 m. The
  holes are small (splinter-sized, 3–12 cm), so they feed 09's leak model (30 such holes along one side is 09's own
  benchmark) and crew and topside parts, not flooding by themselves.
- **Shorts beat line errors.** The forward lean throws the half-spray toward a ship lying across the line of fire. A
  plunging shell (35°) throws its ring steeply upward, missing a low hull abeam at 50 m but still lacing a ship beyond
  it.
- **The splash system already has what this needs:** the miss point, striking speed and fall angle.

### 5.3 Service networks

Cables, pipes and trunks are line targets: `hits = n · w / (2πR)`. It needs no band width, only "does the run cross the
band within R". A run that *lies in* the band takes many more hits: use its length in the band × density.

- **Cables:** cut on any qualifying hit.
- **Pipes:** need a THOR check through the wall (6 mm firemain, thick steam lines).
- **Armoured runs and splinter-boxed wireways:** take the plate check first.

### 5.4 What the player sees

- **Designer:** splinter plating thickness, now tied to shells by the T4 knee. 25–30 lb against light cruisers and
  destroyers, 1–1.5″ against capital-ship HE. It's a real trade-off with weight.
- **Shell design:** the filler choice now shows in splinters (picric +40–50% over Explosive D, amatol −45%), and shell
  steel at the extremes (cast iron).
- **Damage report:** "fragments cut the wireways", "splinters through the director", "near miss holed her side 14
  times".

---

## 6. Recommendations

1. **Generate, don't tabulate.** Implement §2 as a compile-time fragment table per shell (code: `burst()`,
   `mass_bins()`), and retire the Okun table as a constant. Keep it as a regression bench: §4.1's comparison is the
   test.
2. **Schema:**
   - Add `gurney` to 12's explosive catalogue.
   - Define `brisance` as B_TNT/B_Mott (§3.1).
   - Add `shell.steel` (default by kind: forged carbon for HE/common, Ni-Cr for AP).
   - The casing shares (nose, ogive, body, plug, tail) and the filler's share alongside the body are template data per
     kind (§2.5). They can be overridden, like the rest of the block (silly shells allowed).
3. **Runtime:** the band, the lean by v_shell at the burst, density × area, a Poisson draw, THOR through the box
   (§5.1). The nose slug and base plug go through the terminal walk as their own pieces.
4. **Near misses:** a contact burst at the surface for nose-fuzed shells only, upper half-spray, against the hull and
   superstructure boxes (§5.2).
5. **Service networks:** the line-target rule (§5.3).
6. **Tag every rolled-up number** in the damage report with its source group (side spray, nose spray, nose slug, base
   plug, near miss), so balance work can see which one does the damage.

## 7. Open questions

- [ ] **THOR above 53 g.** AP and black-powder pieces (0.2–20 kg) are extrapolated. Check against plugging energy or
      the game's own Thompson walk with a blunt-fragment quality, and pick one rule for pieces above ~0.5 kg.
- [ ] **Casing shares by kind** are [EST]. Navweaps' cross-section drawings of the 16″ Mk 8 / Mk 13 and the 6″ Mk 34
      would pin the body share and the bore within a session.
- [ ] **Steel multipliers** are [EST]. NSWC TR 80-238 (Mock & Holt, 1981) has Armco iron against HF-1 numbers (Tables
      15–17), not reached here.
- [ ] **Explosive D's and TNA's Gurney constants** (and the picric ones) are scaled, not sourced.
- [ ] **Okun's SAP / AP count scaling:** confirm the reading from the original page. If it is a count of *penetrating*
      fragments, the conflict goes away.
- [ ] **The near-miss geometry** uses a flat hull rectangle. Add superstructure boxes and topside crew when §5.2 is built.
- [ ] **Fragment holes into 09's leak model:** hole size ~ fragment presented area. Splinter holes of 3–12 cm match
      0.05–1 kg fragments.

## Sources

- [Okun, *Miscellaneous Naval Armor Formulae* §6 (navweaps)](https://navweaps.com/index_nathan/Miscarmr.php)
- [WDR 51, Destroyers: gunfire, bomb and kamikaze damage (NHHC)](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html)
- [WDR 24, USS Boise (hyperwar)](https://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCL47/WarDamageReportCL47.html)
- [IATG 01.80, Formulae for ammunition management (UNSaferGuard)](https://data.unsaferguard.org/iatg/en/IATG-01.80-Formulae-ammunition-management-IATG-V.3.pdf):
  Gurney constants, Mott constants
- [DDESB Technical Paper 12, Fragment and debris hazards](https://www.esd.whs.mil/Portals/54/Documents/FOID/Reading%20Room/Other/10-F-0806_Fragment_and_Debris_Hazards.pdf):
  drag law, shape factors, Gurney table
- [Fragment weight distributions from naturally fragmenting cylinders (NOL)](https://www.academia.edu/97058430/Fragment_Weight_Distributions_from_Naturally_Fragmenting_Cylinders_Loaded_with_Various_Explosives)
- [MSIAC L-295, TNT equivalence of black powder and its Gurney and Mott constants](https://www.msiac.nato.int/publication/l-295-tnt-equivalence-of-black-powder-and-its-gurney-and-mott-constants/)
- [Wikipedia, Gurney equations](https://en.wikipedia.org/wiki/Gurney_equations)
- [Mock & Holt, NSWC TR 80-238 (DTIC ADA110444)](https://apps.dtic.mil/sti/pdfs/ADA110444.pdf)
- [NPG Report 911, Terrier warhead fragmentation (DTIC AD0499452)](https://apps.dtic.mil/sti/pdfs/AD0499452.pdf): beam
  spray 60–120°, an example of a measured spray
- [Makins, *Surgical Experiences in South Africa*, ch. XII (shell wounds)](https://angloboerwar.com/books/makins-surgical-experiences/makins-chapter-xii-on-shell-wounds)
- [War Thunder, Naval projectile fragmentation damage](https://forum.warthunder.com/t/naval-projectile-fragmentation-damage-what-s-new/91679)
  (a game's model, for comparison only)
- Project notes: `08_gunfire_effects.md` §3.6 and §8, armor/01 §3.10 (THOR), `11_blast_confinement.md`,
  `12_explosive_fillers.md`, `09_damage_control.md`, `shells-armour-damage.md` (E3)
