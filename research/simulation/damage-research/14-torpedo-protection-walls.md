# 14 — Torpedo Protection Walls: What They Held, How Far They Bent, How Much a Hit Flooded
Status: draft    Updated: 2026-11-07    Request: [REQUESTS.md "Torpedo protection walls"](../../REQUESTS.md) (the code: [docs/battle/explosions.md](../../../docs/battle/explosions.md) EX5, `GasNetwork.EdgeRupture`, `SimSettings.TdsMembraneK`)

*Four questions: the walls' steel, joints, spans and the charges they held in trials; how far a holding bulkhead bent before it tore; how long the protection's own compartments were and what one hit flooded; and whether liquid against the holding bulkhead passed the blast through. The best primary material found is the Admiralty's own summary of the Job 74 trials (ADM 324/4, 1935-37), transcribed on the NavWeaps forum, and the USN war damage reports. The US caisson figures come through a secondary summary of an INRO article.*

Tags as in the other damage docs: **[SRC]** sourced, **[INFERRED]** my reasoning or a fit, **[EST]** an engineering estimate with no direct source, **[UNCERTAIN]** thin or conflicting sources.

**Companion code:** [`tds_walls_ref.py`](tds_walls_ref.py) (pure Python, under a second). It covers the steels, the membrane law by system against `TdsMembraneK`, the static load that leaves each measured set, the bend thresholds as strain, two charge-held laws, Job 74's damage length against W^⅓, North Carolina's cells, the tonnes per hit, and the water-hammer pulse through a full tank. Every derived number below comes out of it.

---

## Summary

1. **The walls are no weaker in the code than in the record. The load on them looks too high.**
   - **What a holding bulkhead held, from the membrane law with real inputs** (ref §2): flow stress 345-680 MPa by grade (every grade's yield is above the code's 280 MPa), and a span equal to the wall's height between its supports, 5-9 m (not 4 m). It was riveted, so its seams opened at about a tenth of its span (§3 below).
     - That gives **1.1-3.6 MPa**, or **K 0.7-1.4** on `RuptureMPaMm`. The exception is North Carolina's 2 in STS abreast turret I, split by a platform: K 1.8-2.6.
     - A continuous (welded) wall held to a set of about 15% of its span: K 1.1-2.1.
     - Only a wall stretched to the plate's own 15% reaches K 1.7-3.3.
     - **Today's 2.2 sits at the top of that range.**
   - **What the walls actually took:** the static pressure that leaves each measured permanent set, at hits near the design charge (ref §3). For a holding bulkhead it is **0.7-1.5 MPa** [INFERRED from sets SRC]:
     - Job 74's 1,000 lb trial: 0.9 MPa
     - Caisson H: 0.74 MPa
     - Nevada's seams opening: 1.5 MPa
     - Tirpitz's 1,600 lb bomb in a fuel tank: 1.1 MPa
   - **So:** walls at K 2.2 that still give in 51-68% of one-torpedo hits (EX5's table) mean the gas puts several times 1 MPa on them. The first check is the load: `UnderwaterGasShare` 0.5 held as quasi-static pressure, while in a real hit the gas leaves through the hole in milliseconds. Raising K is not the fix. That check falls outside this request, but it is where the evidence points.
2. **The trials** [SRC unless tagged]:

   | Trial | Charge | System | Holding wall | Result |
   |---|---|---|---|---|
   | Chatham Float, 1914-15 | 400 lb models | tubes, plates | — | plates withstood "a 400lb charge of TNT" by 1915 [SRC via forum, book unnamed] |
   | Nelson full-scale, c.1923 | 1,000 lb | 12 ft, air-water | 2 × 0.75 in D | leakage "negligible" |
   | **Monarch, Aug 1923** | 2,084 lb (945 kg) at ~8 ft, below her bottom | fitted bulge | thick bulkhead | "considerable deflection in a fair curve, but remained completely intact"; +2,700 t (bulge, engine room by a cracked condenser cover) |
   | Tosa, June 1924 | 100-350 kg, 5 shots | 3-layer all-void | — | every shot breached; 726-1,203 lt each |
   | **Job 74 #4, 14 Jul 1936** | 1,000 lb contact, 15 ft deep | sandwich void-water-void, 12 ft 5 in | 2 × 35 lb D.1, riveted stiffeners inboard, **oil behind** | **18 in** set, no major rupture, small leaks; the oil's pulse broke the structure behind |
   | **Job 74 #5, 6 Apr 1937** | the same | the same, empty behind | 1 × 60 lb D.1, half welded stiffeners outboard, half riveted | torn 14 × 28 ft where welded; riveted half "slight"; a transverse bulkhead **4 ft** from the line stayed tight |
   | Pugliese, 1935 | up to 640 kg | drum in liquid | 40 mm ER | "satisfactory" [UNCERTAIN] |
   | Bismarck's system, 1937 | 300 kg S1 (~960 lb TNT jet), contact at ~4 m | — | 45 mm Ww | result not found [UNCERTAIN] |
   | Soviet tests, 1938 | 400 kg (US type), 500 kg (Pugliese) | — | — | inner bulkheads undamaged [UNCERTAIN] |
   | Caisson F (BB55), ½ scale | 700 lb equivalent | 5 layers | MS | **3 in** set, **6 in full scale**, no leak |
   | **Caisson H (BB57), 28 Sep 1939**, ½ scale | 128 lb = 700 lb by the 2.45 exponent | WWAA, lower belt as the 3rd wall | 25 lb STS | **11 in** set, **22 in full scale**, against 24 in of clearance; no appreciable leak in 2 h |

3. **How far a wall bent, and what gave** (ref §4) [SRC sets; INFERRED as strain]:

   | Set (w as a share of span a) | Strain | What happened | Cases |
   |---|---|---|---|
   | ~0.03 | 0.2% | riveted connections weep | Tirpitz: 6 in over 16 ft, rivets leaking; Job 74 #4 at wide rivet pitch |
   | ~0.06 | 1% | held tight, with a margin | Job 74 #4: 18 in; Caisson H: 22 in |
   | ~0.10 | 2.7% | riveted seams open: the boundary fails as a leak | Nevada: ~2 ft; North Carolina: seams 6-12 in open, the bulkhead to within 3 in of the turret stool |
   | ~0.10 | 2.7% | bottom edges pull loose | Saratoga's TB 2 and 3 at 18-36 in |
   | ~0.25 | 17% | plate torn and holed | Saratoga's TB 1 at ~5 ft |

   Welded stiffeners on thick D.1 tore earlier (Job 74 #5). The weak points were the joints: Yamato's belt joint, PoW's shelf, Pugliese's bottom joint, BB57's butt strap at the armour knuckle.

   The room given behind: the US wanted 24 in to machinery (Caisson H used 22 in of it). California's TB 4 took 2 ft into the inner void while her holding bulkhead moved 1 in. KGV had a ~3 ft void at A and B engine rooms and ~8 ft of auxiliary spaces behind the sandwich.

4. **The protection's compartments were short, and flooding was bounded by them.**
   - **Lengths:**
     - Ramillies' 1917 bulge: compartments of 18-20 ft [SRC].
     - PoW's flooded voids: frames 228-234, 24 ft [SRC].
     - North Carolina 1942: about 29 TDS cells flooded (15 voids and 14 tanks) for about 1,570 t of liquid in the spaces. That is ~41 t a cell, about 4.7 m (4 frames) [INFERRED].
     - German WWI and WWII: the wing bunkers and bulge were divided at the main compartments ("compartment XIII", "sections IX and X").
   - **Damage length:** Job 74's layers were destroyed over 36 ft (1.43·W^⅓ m) and partly flooded 15 ft beyond each end (2.6·W^⅓ m in all). The destroyed length matches the torpedo reach of explosions.md's table taken across (2 × 0.7·W^⅓).
   - **The code's cells:** 17 m long and full height, they hold 600-1,640 t a section on one side, and tearing into the next doubles it.
5. **Tonnes a hit** [SRC; classes INFERRED]:

   | Outcome | Cases | Range | Median |
   |---|---:|---|---:|
   | Holding wall held | 4 | 250-1,066 t | ~540 t |
   | Held, then leaked or partly failed | 3 | 500-1,100 t | ~970 t |
   | Breached | 8 | 740-3,000 t | ~1,200 t |

   - **Held:** Kronprinz, Westfalen, Grosser Kurfürst's mine, Littorio's 320 kg bomb (TDS only).
   - **Leaked or partly failed:** Ostfriesland, North Carolina, Saratoga's piping.
   - **Breached:** the five Tosa shots in a 1920s three-void system, Seydlitz's forward mine, Vittorio Veneto 2,700 t, Yamato 3,000 t.
   - **The code's held case (3,900-5,000 t) is above every breach in the record** bar the two WWII 3,000 t cases.
6. **Liquid against the holding bulkhead passed the blast through. It was known, tested and written down.**
   - **Saratoga, 1942:** every layer full bar one at ~70%; the innermost full tank carried the shock straight to the boiler room bulkhead. The bulkhead held, but its pipe spools broke, three firerooms flooded (1,100 t), and the report concluded "at least one void layer is essential".
   - **Job 74 #4:** oil behind the protective bulkhead carried its push to the next wall, a "potential source of danger", and the oil was taken out for #5.
   - **The US 1/16-scale tests after Caisson H:** no liquid against the armour bulkhead; counterflood WWAA, then WWAW, then WWWW.
   - **The Germans,** after Leipzig's torpedoing: liquid "behind the shelf" increased the damage further in.
   - **The physics** (ref §7): a wall moving at 10-20 m/s drives 12-31 MPa for 2 ms through a full tank, far above the 1 MPa a 25 lb wall behind it holds.
   - **The one counter-case:** PoW's frame 109 hit with her inner voids flooded, where the inboard bulkhead showed "no significant breach".

---

## 1. The walls: steel, joints and spans

### 1.1 Steel by grade

[SRC Okun's table, NavWeaps; Ducol from Wikipedia. Converted from ksi.]

| Grade | Yield MPa | UTS MPa | Elong. | Used as |
|---|---:|---:|---:|---|
| Mild/medium (all navies) | 240-310 | 365-470 | 16-25% | US "Standard" holding bulkheads (Nevada 1.5 in), BB55 amidships per Robertson [UNCERTAIN] |
| HTS | 324 | 538 | 22% | US layer walls (BB57's two outer 25 lb walls) |
| STS | 517-586 | 758-862 | 25% | US holding bulkheads: BB55 abreast the turrets (2 in, tapering to 3.75 in below the first platform), BB57 (25 lb) |
| German WWI nickel-steel torpedo bulkhead | 345 | 538 | 21% | 30-50 mm, Nassau to Bayern, overlapped joints (double thickness at the seam) |
| RN "D" / D.1 (Si-Mn HT) | 379 | 614 | 22% | Nelson (2 × 0.75 in), Job 74 (2 × 35 lb; 60 lb), KGV (1.5-1.75 in) |
| Ducol / IJN DS | ~412 (elastic limit) | 618-696 | 20-23% | Nagato, Yamato bulkheads; Pugliese's ER "similar Si-Mn" |
| German Ww | 469 | 634-807 | 22% | Bismarck's 45 mm torpedo bulkhead; Okun: chosen for ductility, so as not to snap at the joints |
| German St 52 | 352 | 510 | 18% | layer walls |

- **Grade mattered less than joints.** The US 1/16-scale tests found HTS to STS "did not materially affect the results".
- **Welding thick D and D.1 weakened it:** "welding thick D.1 quality material cannot, at present, be adopted for structure liable to severe shock" (Job 74 #5). Welding on thin plate was better than riveting, because a welded joint stays tight where rivets strain open. The Mogami class was rebuilt riveted after welded Ducol failed.

### 1.2 Joints

- **Riveted (to the late 1930s nearly everywhere):**
  - North Carolina's holding bulkhead had riveted scarph seams and "failure … was consistently restricted to rivet failure", with no perforations by fragments [SRC WDR 61]. Its designers wrote "the joints in such a heavy bulkhead could not be made full strength".
  - Scharnhorst's bulkhead was riveted on scarphs with angle pieces, "more susceptible for fails"; Bismarck's ran continuous, with stronger butt straps [UNCERTAIN, forum].
- **Butt straps and stiffeners:**
  - Job 74 #4: "the degree of stiffening, and the strength of the butt straps … appeared to have a margin of safety. The connections at top and bottom were satisfactory."
  - Rivets at wider than usual pitch pulled out and weeped.
  - Riveted stiffeners on the outboard side held; welded ones tore (#5).
  - Conclusion: stiffeners inboard when possible.
- **Weak points named in the record:**
  - BB57's butt strap at the knuckle between the lower belt and the triple bottom.
  - Yamato's belt joint, which "relied entirely on the shear strength of rivets".
  - PoW's shelf.
  - Pugliese's riveted bottom joint, which failed before the drum collapsed.
- **Japanese view (USNTMJ via Robertson):** joint efficiency matters much more in liquid-loaded systems than in void ones [UNCERTAIN].

### 1.3 Spans

No source gives spans as numbers.

- **Job 74 #5's rupture ran 28 ft deep,** so its bulkhead stood at least 8.5 m between supports.
- **North Carolina's was split by the first platform** abreast turret I.
- **Elsewhere the span is the height from the inner bottom or its bilge to the armour deck, shelf or third deck:** 5-9 m [EST].
- **Stiffener spacing** was the frame spacing, 4 ft in US and British practice [SRC frame numbers against feet in WDR 61; EST for the RN].

## 2. The charges they held

### 2.1 British

- **1914-15:** old pre-dreadnoughts and the Chatham Float. By 1915 shaped steel plates had been made "to withstand a 400lb charge of TNT", and tubes kept the inner bulkheads intact [SRC via forum, book unnamed].
- **Ramillies' 1917 bulge:** 220 ft long, 7 ft 3 in wide, ½ in plate, compartments 18-20 ft. Inner compartments split top and bottom and packed with 9 in and 8 in tubes of ¾ in steel [SRC via forum].
- **Nelson (G3's system):** air space, buoyancy (water) space, protective bulkhead of 2 × 0.75 in D steel 12 ft inboard.
  - 1922 Chatham Float models: "safe against a 750-pound charge".
  - A full-scale 1,000 lb shot: "a very large hole was made in the outer bottom … the leakage into the hull proper proved to be negligible".
  - The vent plates were later sealed: "ineffective during the time the maximum pressure was exerted" [SRC Raven & Roberts pp. 123-124 via forum].
- **Monarch, August 1923:** 2,084 lb TNT about 8 ft from her side and below her bottom, between the forward turrets, against a fitted starboard bulge [SRC Garzke & Dulin, *Allied Battleships* pp. 358-360, via forum].
  - The bulge was crushed flat against the thick bulkhead.
  - The bottom was driven up 2.17 ft at frame 62.
  - The joints between the old and new structure tore along one line of rivets.
  - "The main thick bulkhead took a considerable deflection in a fair curve, but remained completely intact."
  - Brackets from the armour to the torpedo bulkhead tore from the bulkhead.
  - Inside: the inner bottom bulged, hatches and doors were displaced, shell-room stanchions bent and punched through, and a cracked condenser cover flooded the centre engine room.
  - After: +2,700 t, 10.25° list, 11.42 ft by the bow. Most of that was the bulge and that engine room, not the holding wall.
- **Job 74 (1935-37):** a 72 ft float with a 100 ft beam, a 60 ft box, 4 in side armour 10 ft below the waterline, the existing bulge to port and the "sandwich" to starboard [SRC ADM 324/4 via forum].
  - **#3:** 440 lb TNT 5 ft under the flat bottom holed the outer bottom 40 × 30 ft and the inner 30 × 24 ft. Damage was "distinctly limited" by longitudinal bulkheads 15 ft from the line. The lower portions of bulkheads within 15 ft leaked "beyond the capacity of the ship's resources".
  - **#4:**
    - Shell ruptured 25 × 15 ft.
    - Both sandwich walls (17-12 lb D) destroyed over the central 36 ft, partly flooded 15 ft either side.
    - The protective bulkhead (2 × 35 lb D.1, 12 ft 5 in from the side) not majorly ruptured, deflected 18 in.
    - "Comparing this with the result of model experiments … the bulkhead still had a margin of safety."
    - Pressure behind it fell "to about 7.5% of the probable value in open water".
  - **#5:**
    - A single 60 lb D.1 bulkhead at 13 ft 10.5 in.
    - Torn 14 × 28 ft over the half with welded outboard stiffeners; the riveted half "slight".
    - The transverse bulkheads behind "virtually tight, although one of them was only 4 ft from the line of the explosion".
    - Conclusion: one thickness is as good as two against the pulse and better against fragments; 60 lb D.1 suffices "provided it is adequately supported".
  - **The sandwich adopted:** KGV's was "about 13 feet wide" with the holding bulkhead 1.5 in over machinery and 1.75 in at the magazines, rated 1,000 lb [SRC Wikipedia].

### 2.2 United States

[SRC INRO 57(3), summarised by a NavWeaps poster; UNCERTAIN as secondary.]

- **Model scaling:** the charge scales by the 2.45 power of the scale (earlier, the cube). Half scale therefore takes 0.18 of the full charge: 128 lb for 700 lb. The Japanese used 2.243. A 5% error in the exponent moves a half-scale test's equivalent by 8%.
- **Caisson F (BB55), half scale:** holding bulkhead 3 in, 6 in full scale.
- **Caisson H (BB57), 28 Sep 1939, half scale:**
  - Holding bulkhead 11 in, 22 in full scale, against 24 in of clearance to machinery.
  - "No appreciable leakage into the machinery spaces" in the two hours to docking.
  - The lower belt (the third wall) stayed whole but pushed the third deck and triple bottom back and wrinkled them.
  - The butt strap at its knuckle failed, though the weld held.
  - Brackets proved "actually detrimental".
- **North Carolina's own report:** the final model's holding bulkhead deflected only 3 in and did not leak [SRC WDR 61].
- **1/16 scale after Caisson H:** WWAA ≈ AWWA; no liquid against the armour bulkhead; HTS against STS made no material difference; under-bottom shots were worse than expected.
- **Duplicates (Jurens):** identical charges on identical caissons "could result in fairly significant case-to-case changes in result". Margins under ~10% mean nothing.

### 2.3 Italy, Germany, Japan, USSR

**Italy (Pugliese)** [devstrike's "Pugliese Revisited", a forum article; UNCERTAIN where it names no source]:

- 40 mm ER holding bulkhead; outer drum-cell wall 14-15 mm, inner 10 mm, drum 7 mm, last watertight bulkhead 7-9 mm.
- 1935 tests up to 640 kg.
- Littorio at Taranto: 176 kg breached the dry cells and cracked the 40 mm bulkhead 2.8 cm, with minor flooding.
- Giulio Cesare: ~130 kg at 4 m, nothing.
- Vittorio Veneto: 340 kg at 60% of the drum's diameter (toward an end), both bulkheads defeated, ~2,700 t.
- Littorio 1943: a 320 kg bomb held at the armoured bulkhead, 1,066 t in the TDS spaces.
- Impero 1944: 511 kg TNT-equivalent "seems" to have breached; three 459 kg charges at 17 m breached frames 123-129.

**Germany** [UNCERTAIN, forum]:

- Full-size targets "Sprengziel I and II" before and after WWI.
- Bismarck's system tested in 1937 with 300 kg S1 in contact at ~4 m depth: ~1.45 × TNT in jet effect, ~960 lb. Result not given.

**Japan:**

- Tosa, June 1924 [SRC Wikipedia, Kure trials]: a 100 kg mine flooded 23 compartments and 995 lt; torpedoes of 200, 300 and 350 kg let in 1,008, 1,203 and 1,160 lt; a 150 kg mine 726 lt. The three-void system was "an insufficient defense against modern torpedoes".
- The void-system formula T = 95.62·C^0.469·D^−⅓ (T the total plating in mm, D the depth in mm), valid for 200-400 kg [SRC USNTMJ S-01-9 via Robertson]. Ref §5: it under-rates liquid-loaded systems (Job 74 at 224 kg against 454 held) and is off its range once armour is counted.

**USSR, 1938** [UNCERTAIN]: 400 kg on a US-type system and 500 kg on a Pugliese left the inner bulkheads undamaged.

## 3. How far they bent before they tore

### 3.1 Sets measured

[SRC sets; t and a EST where marked in the ref.]

| Case | Wall | Set | Outcome |
|---|---|---:|---|
| Job 74 #4 | 2 × 35 lb D.1 | 18 in | held; weeps at widely pitched rivets |
| Caisson F (BB55) | MS | 6 in (full) | held, no leak |
| Caisson H (BB57) | 25 lb STS | 22 in (full) | held tight 2 h |
| California 1941, fwd hit | holding / TB 4 (0.75 in) | 1 in / ~2 ft | intact; "~9 ft of the system open to the sea" |
| Nevada 1941 | 1.5 in MS holding, 14 ft system | ~2 ft | seams opened, "fairly significant" flooding |
| North Carolina 1942 | 2 in STS | to within 3 in of the turret stool | seams opened 6 in at a scarph, 1 ft along the first platform; 4 in pulled from the third-skin shelf |
| Saratoga 1942 | TB 4 / 3 / 2 / 1 | 12 / 18 / 36 / ~60 in | tight bar a few rivets / bottom loose / held at the top, loose at the bottom / torn and holed 10 × 3 ft |
| Saratoga 1942 | holding | — | "deformed but virtually intact"; cast steel pipe spools broke |
| Tirpitz 1944, 1,600 lb AP bomb in a fuel tank | 45 mm | 6 in over 16 ft | riveted connections leaked |
| Monarch 1923 | "thick" | "considerable … fair curve" | intact |

Sources: Garzke & Dulin and ADM 324/4 via forum; WDR 61 and the Saratoga report; the California, Nevada and Tirpitz entries from NavWeaps posters quoting the BuShips reports and BR 1732(22) [UNCERTAIN as secondary].

### 3.2 Reading them

(ref §3-4) [INFERRED]

- **Riveted joints weep from a set of ~3% of the span.**
- **They hold tight with a margin to ~6%.** The best-documented holds: Job 74 #4 at 0.056, Caisson H at 0.078.
- **Seams open, and edges pull loose, at about 10%:** a boundary that fails as a leak, as Nevada's and North Carolina's did.
- **The plate itself tears at ~25%:** 17% strain, the plate's own limit.
- **Strain at these sets** (a parabola, ε = (8/3)(w/a)²): 0.2%, 1%, 2.7% and 17%. So **a riveted holding bulkhead failed at a few percent of strain,** not the 15% the code's estimate takes. Welded thick plate failed at its welds before that.
- **The static pressure that leaves each set** (8·σ_y·t·w/a²):
  - Holding walls at design-level hits: 0.7-1.5 MPa (Job 74 0.90, Caisson H 0.74, Nevada 1.52, Tirpitz 1.08).
  - Layer walls nearer the hit: Saratoga's TB 1-4 at 0.3-1.5 MPa.
- **The room behind:** the US asked for 24 in (0.6 m) to machinery. The record's holds sit at 0.15-0.56 m. The failures went to 0.6 m or more: Nevada, North Carolina, Saratoga's layers. So the inner void was sized at about twice the expected set, and a hit beyond the design charge used it up.

## 4. The protection's compartments, and what one hit flooded

### 4.1 Lengths

| Ship / system | Length of a TDS compartment | Height split | Source |
|---|---|---|---|
| Ramillies' bulge, 1917 | 18-20 ft (5.5-6.1 m) | inner tubes in top and bottom halves | [SRC via forum, book unnamed] |
| Job 74 sandwich, 1936 | not stated; transverse bulkheads behind the protective bulkhead, one 4 ft from the line, held tight | — | [SRC ADM 324/4] |
| North Carolina, 1942 | ~4.7 m (4 frames): 29 cells for ~1,570 t | first and second platforms | [INFERRED from WDR 61] |
| Prince of Wales, 1941 | voids between frames 228 and 234: 24 ft (7.3 m) | — | [SRC Garzke et al.] |
| German WWI (Seydlitz, Kaiser, König) | by main compartment, about 10-12 m [EST]: wing bunkers and passages "of compartment XIII" | the armour deck slope | [SRC warhistory.org; EST length] |
| Tirpitz' bulge, 1944 | "sections IX and X" (two main compartments) damaged by one 1,600 lb bomb | — | [SRC BR 1732(22) via forum] |
| Tosa, 1924 | 23 compartments flooded by a 100 kg mine, ~44 t each | — | [SRC] |
| Iowa | main transverse bulkheads closed to 32 ft in the machinery spaces after Caisson H | — | [UNCERTAIN] |

### 4.2 How far a hit reached along the side

- **Job 74 #4 and #5** (454 kg in contact): the layers were destroyed over 36 ft, 11 m. That is 1.43·W^⅓ m, about the torpedo reach of explosions.md's table taken across (2 × 0.7·W^⅓ = 10.8 m).
- **They were partly flooded 15 ft beyond each end:** 2.6·W^⅓ m in all, about 20 m at 454 kg and 17.5 m at 300 kg.
- **North Carolina** (~300 kg): the shell was demolished over frames 43-52, 36 ft, and distorted over frames 37-58, 84 ft. Bulkhead No. 1 was demolished over frames 46-50, and Nos. 3 and 4 torn between frames 45 and 46.
- **Seydlitz's 1916 mine:** frames 130-137.

### 4.3 Tonnes a hit

[SRC unless tagged; charges EST where the source gives none.]

| Case | Charge | Protection | Holding wall | Flooded |
|---|---|---|---|---|
| Kronprinz, J1, Nov 1916 | ~150 kg (18 in) | coal bunker and 40 mm TB, abreast A's magazines | held; riveted joints leaked, pumped | 250 t |
| Westfalen, E23, Aug 1916 | 18 in | the same | "the torpedo bulkhead held" | ~800 t |
| Ostfriesland, mine, Jun 1916 | ~150 kg | the same | slightly damaged, tore open when she turned | 500 t |
| Grosser Kurfürst, mine, 1917 | ~150 kg | the same | — | ~280 t |
| Seydlitz, Jutland torpedo | 18 in | the same, below the fore turret | "held firm"; compartment XIII below the armour deck filled slowly (no drain main) | not separable from 5,308 t |
| Seydlitz, mine, Apr 1916 | ~150 kg | forward, 90 m² hole | all three layers pierced | 1,400 short t |
| Moltke, E42, Apr 1918 | 18-21 in | amidships | held; water came by condensers after a breakdown | 1,800 t [UNCERTAIN] |
| Marlborough, Jutland | 21 in | coal, no continuous bulkhead | — | 28 ft hole, forty compartments damaged, a boiler room flooded; not given |
| Monarch trial, 1923 | 945 kg at 2.4 m | fitted bulge | intact | 2,743 t, mostly bulge and an engine room via a condenser |
| Tosa, 1924 (5 shots) | 100-350 kg | three voids | breached | 738-1,222 t each |
| California, Dec 1941 | ~200 kg | 5 bulkheads | no damage to the holding bulkhead | sank later through open manholes |
| Saratoga, Jan 1942 | ≤500 lb | every layer full bar one at ~70% | virtually intact; piping broke | 1,100 t (+320 counterflood) |
| North Carolina, Sep 1942 | ~660 lb | one bulkhead short abreast turret I | seams and butts opened | 970 t (+480 counterflood) |
| Vittorio Veneto, Mar 1941 | 340 kg | drum at 60% | breached | ~2,700 t [UNCERTAIN] |
| Littorio, 1943 bomb | 320 kg | Pugliese | held | 1,066 t, TDS only [UNCERTAIN] |
| Yamato, Skate, Dec 1943 | ~300 kg | void system, riveted belt joint | the joint failed | ~3,000 t |

Sources for the German cases: Wikipedia (Westfalen, Ostfriesland, Grosser Kurfürst, Seydlitz), warhistory.org (Seydlitz), and a NavWeaps poster for Kronprinz and Moltke.

**Counted by outcome** (ref §6) [INFERRED, small n]:

- **Held:** 250-1,066 t, median ~540 t.
- **Leaked or partly failed:** 500-1,100 t, median ~970 t.
- **Breached:** 738-3,000 t, median ~1,200 t.

The big breaches are WWII warheads on a narrowed system or a failed joint. A 1920s three-void system, breached by 100-350 kg, still took only ~1,000-1,200 t, because the next compartment held.

## 5. Liquid against the holding bulkhead

- **Known by 1936-42 in three navies:**
  - **Britain:** Job 74 #4 had oil 75% full behind the bulkhead. "The violent distortion of the protective bulkhead set up a pressure pulse in the water in the oil fuel spaces on the inboard side and this caused damage" to the float's main structure; in a ship the oil tank's inner wall, being lighter, "its failure … would therefore be certain". Hood's fuel tanks lay directly behind her torpedo bulkhead (Robertson: "loaded close to full would likely have been unhelpful").
  - **United States:** the post-Caisson-H tests found there should not be liquid against the armour bulkhead. Saratoga's report: the innermost full tank carried the shock straight to the boiler room bulkhead; "at least one void layer is essential"; and the innermost bulkhead has the best chance of holding when the tanks just outboard of it are empty.
  - **Germany:** "established by the event of torpedoing of Leipzig followed by respective experiments that liquid filling behind the shelf cause a increased depth effect" [UNCERTAIN, forum].
- **What failed was what lay behind it,** not the wall: the inner tank wall, the boiler room bulkhead's pipe spools and flanges, the connections.
- **Coal in the WWI German ships** lay against the torpedo bulkhead. It is granular: it compacts and slows fragments, but it is not a liquid.
- **Counter-case:** PoW's frame 109 hit came after her inner voids had been counterflooded, and the inboard bulkhead showed "no significant breach". One case, with a torpedo of unknown charge.
- **The physics** (ref §7) [INFERRED]: a wall setting 18 in in 20-40 ms moves at 11-23 m/s, and a full tank answers with ρ·c·v, 6-31 MPa for 2 ms. A 25 lb HTS wall behind a full tank opens its seams at 0.8-1.1 MPa static. Air answers with 0.004 MPa.

---

## Recommendations for this codebase

1. **Keep the walls where they are, or lower them a little; don't raise them** (`SimSettings.TdsMembraneK`, `GasNetwork.EdgeRupture`).
   - **If it stays one factor:** about **1.0 for riveted walls (to the late 1930s) and 1.5 for continuous welded ones (1938-45)** [EST from ref §2].
   - **If it's derived per wall:** σ_f = (yield + UTS)/2 by grade and year (table §1.1); t; a = the wall's height between its supporting decks or platforms; ε by joint: riveted 0.027 (w/a 0.10), continuous 0.06 (w/a 0.15).
   - The doc comment's inputs (280 MPa, 15%, 4 m) are each off in a direction that cancels the others.
2. **Check the load before the walls.** The record's static-equivalent load on a holding bulkhead at its design charge is 0.7-1.5 MPa, and riveted seams opened at 1.5-2.5 MPa. Two benches for `shipgen torpedo` or `explode`:
   - **Job 74 #4:** 454 kg in contact, 15 ft down; a 3.8 m sandwich (void, water, void); the bulkhead 2 × 22 mm D.1 with oil behind. Wanted: about 18 in, held, small leaks, the oil's back wall broken.
   - **Caisson H:** 318 kg, a 5.5 m system (liquid, liquid, void, void), 15.6 mm STS. Wanted: about 22 in, tight.

   If the network puts several MPa on the bulkhead there, look at how long the half of the products `UnderwaterGasShare` brings in stays as pressure once the shell is holed.
3. **The holding bulkhead fails as a leak first.** Most "failures" in the record were seams and pipes opening (North Carolina, Nevada, Kronprinz, Tirpitz, Saratoga), pumpable or slow. Use the seam leak (`LeakR1`…`LeakR5`) on its set: weeping from w/a 0.03, seams open at 0.10. Keep `RupturedShare`'s tear for a set past ~0.25, or a named weak joint: a riveted belt joint, a shelf, welded stiffeners on thick plate before ~1938.
4. **The protection's compartments: 5-7 m long, split at the platform where there is one** (shipgen; `armour.tds_*`).
   - The transverse walls between them are protection walls (already `Ductile`), and the record has them holding 1.2 m from the hit.
   - Expect the destroyed length to be the near field across (~1.4·W^⅓), and the flooded TDS ~2.6·W^⅓ m (17.5 m for 300 kg on one side).
   - Today's 17 m full-height cells, torn into the next, put 1,200-1,600 t into the TDS alone.
5. **Liquid against the holding bulkhead: the code's pass-on is right.** Send the push into what's behind it (a pulse of ρ·c·v for 2d/c against the next wall, the room behind and its pipes). A void behind takes nothing. Counterflooding the inner void (`tds_fill`) should make the next hit worse, as PoW's designers feared, though her one case held.
6. **Yardstick for EX5e** (`dc-history`, `damage-history`) [INFERRED]:
   - A design-level hit in a protection amidships floods ~250-1,100 t when the wall holds (median ~550), ~1,000 when it leaks, and ~1,000-3,000 when it breaches.
   - Held / leaked / breached at about 60 / 25 / 15 % for hits near the design charge (small n).
   - The 1913 super-dreadnought (no continuous wall) belongs with Marlborough and the Tosa breaches: 700-1,300 t and a machinery space when the hit is abreast one.
7. **Scatter:** identical caissons gave noticeably different results. A spread on the tearing set (lognormal, σ ≈ 0.3) [EST] is more honest than a sharp line.

## Not reached

- Brown's Job 74 and KGV pages, and the British layer widths beyond "about 13 ft".
- The US caisson reports themselves: Caisson F's charge and bulkhead, Caissons 74 and 75 (the inclined upper belt).
- North Carolina's inner-void depth abreast turret I.
- US TDS tank lengths from plans: 4.7 m is inferred.
- German WWI torpedo bulkhead spans, Sprengziel's results, and the 1937 Bismarck shot's outcome.
- Pugliese's ER figures.
- Moltke's E42 hit position, and Marlborough's tonnage.

## Sources

- ADM 324/4, "Job 74 - Summary of Trials Carried Out and Conclusions Reached … 1935 - April 1937", transcribed by Mattzo, with Monarch (Garzke & Dulin, *Allied Battleships* pp. 358-360) and Nelson (Raven & Roberts, *British Battleships of WW2* pp. 123-124) quoted in replies: [NavWeaps forum, "The Job 74 Trials - from ADM 324/4"](https://www.tapatalk.com/groups/warships1discussionboards/the-job-74-trials-from-adm-324-4-t43991.html)
- INRO 57(3) summary (Caissons F and H, the 1/16-scale tests, the 2.45 exponent), and the Japanese formula from USNTMJ S-01-9: [NavWeaps forum, "Torpedo protection system of BB57 and BB61"](https://www.tapatalk.com/groups/warships1discussionboards/torpedo-protection-system-of-bb57-south-dakota-and-t43805.html)
- Kronprinz, Moltke 1918, California and Nevada bulkheads, Tirpitz BR 1732(22), Ramillies' bulge, Leipzig: [NavWeaps forum, "German WW1 capital ship torpedo and mine damage"](https://www.tapatalk.com/groups/warships1discussionboards/german-ww1-capital-ship-torpedo-and-mine-damage-t50523.html)
- [USS North Carolina War Damage Report No. 61](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html)
- [USS Saratoga War Damage Report No. 19](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-saratoga-cv3-war-damage-report-no-19.html)
- [Summary of War Damage, 17 Oct 1941 to 7 Dec 1942](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/summary-of-war-damage-17-oct-1941-to-7-dec-1942.html)
- [Okun, metallurgy of armour and construction steels (NavWeaps)](http://www.navweaps.com/index_nathan/metalprpsept2009.php); [Ducol (Wikipedia)](https://en.wikipedia.org/wiki/Ducol)
- [Pugliese Revisited (DevStrike)](https://www.devstrike.net/topic/410-pugliese-revisited/)
- [Japanese battleship Tosa (Wikipedia)](https://en.wikipedia.org/wiki/Japanese_battleship_Tosa)
- Wikipedia: [SMS Seydlitz](https://en.wikipedia.org/wiki/SMS_Seydlitz), [SMS Westfalen](https://en.wikipedia.org/wiki/SMS_Westfalen), [SMS Ostfriesland](https://en.wikipedia.org/wiki/SMS_Ostfriesland), [SMS Grosser Kurfürst](https://en.wikipedia.org/wiki/SMS_Grosser_Kurf%C3%BCrst_(1913)), [SMS Moltke](https://en.wikipedia.org/wiki/SMS_Moltke_(1910)), [KGV class (1939)](https://en.wikipedia.org/wiki/King_George_V-class_battleship_(1939))
- [SMS Seydlitz – Operational History III (warhistory.org)](https://warhistory.org/article/sms-seydlitz-operational-history-iii); [Marlborough at Jutland (jutland1916.com)](https://www.jutland1916.com/?p=34113)
- [Garzke et al., "Death of a Battleship" (2012 update)](https://pacificwrecks.com/ship/hms/prince-of-wales/death-of-a-battleship-2012-update.pdf)
- [Naval Gazing, Underwater Protection Parts 1](https://www.navalgazing.net/Underwater-Protection-Part-1) [and 2](https://navalgazing.net/Underwater-Protection-Part-2)
