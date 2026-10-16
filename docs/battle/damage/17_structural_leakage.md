# 17 — Leaks through damaged structure: sprung seams, loosened fittings, and bulkheads that leak after a burst

*Research for the damage model, after `11_blast_confinement.md`, `13_nonpenetrating_hits.md` and `15_near_misses.md`.
The question:*

> *Flooding through damaged structure: sprung seams and leaks past bulkheads after a burst (the leak rates are tuning
> values).*

*Three notes already hand this flooding a number, and each is a guess:*
- *11 §5.1: a boundary the blast **deformed** but didn't rupture "gets a seam leak (as the 4.3b leak rates, ×10 [EST])".
  That is a flat 50 cm², whatever the boundary's size and however hard it was hit.*
- *15 §6.4: underwater shock gives sprung seams of 5–50 cm² at NSF 0.2–0.45 and 50–500 cm² at 0.45–1.0 [EST].*
- *09 §2.3: a table of leak paths by kind (0.01–0.3 m² for a blast-sprung riveted seam), with no rule for which one a
  hit makes.*

*The sim (4.3b) has one leak area for every intact bulkhead and deck (`FloodLeakBulkheadM2`, `FloodLeakDeckM2`, 5 cm²;
20 cm² outran the pumps) and none for a damaged one. This note gives a rule that sets a damaged boundary's leak from
what loaded it, and says how that leak behaves with head and time and under damage control. It also sets out the
tuning values in one place.*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the cases, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion code:** `leakage_ref.py` (pure Python, about 1 min, seeded). Every table in §1–§6 comes out of it, section
by section ("ref §n"). All the tuning values are in its `KNOBS`.

---

## 0. Short answer

1. **Leaks past standing structure are how ships are lost slowly.** The holes decide the first few minutes. After
   that, the water moves through seams, glands, doors and stuffing tubes over hours, and the record shows it plainly:
   - **Lützow's bow** went from 2,400 t to 8,350 t between 22:15 and 02:00, compartment by compartment, through "not
     completely watertight" bulkhead joints and speaking tubes.
   - **Houston's magazines** were found full 3 h after the hit, flooded through the cable stuffing tubes of an intact
     bulkhead.
   - **Saratoga** at Crossroads had her bottom dished 3–6 in. and only one tear. With nobody aboard she sank in 7½ h.
   - **O'Brien** (DD) had her shell seams and rivet holes leaking, and the leak grew in a seaway for a month until a
     plate cracked and she broke up.
   - In the 1946 test, progressive sources were a third of all the flooding sources and gave about **half the
     flooding** [SRC, DTIC AD0366699].
2. **A damaged boundary leaks in two ways, and the model needs both:**
   - **Seams:** riveted laps and butts, boundary angles, scarphs. Their leak scales with **the area that took the
     load** and with **how hard** it was loaded.
   - **Fittings:** door and hatch dogs, cable stuffing tubes, vent closures, reach rods, pipe flanges, shaft glands.
     Their leak scales with **how many there are**, and they loosen at loads too low to spring a seam. In the 1946
     underwater test, loose doors, shaft glands, hatch dogs and reach rods gave 61% of the secondary flooding, and seams
     6% [SRC].
3. **One curve sets the seam leak:** `r(D)`, cm² of equivalent orifice per m² of loaded boundary, against a damage index
   `D` = load / the load at which the boundary starts to deform.
   - **Where D comes from:**
     - Blast (11): D = P_felt / P_deform, with 11's 0.012 MPa/mm.
     - Underwater shock (15): D = 5 × NSF.
     - 11's rupture is 5× its deformation, so **every boundary still standing has D < 5**, and D = 5 is 11's
       "ruptured".
   - **The curve:**
     - It is 0 below D = 0.3.
     - It is ~0.3·D² cm²/m² up to D = 1 (weeping: rivets, gaskets).
     - It runs log-linear from **0.3 cm²/m² at D = 1** to **100 cm²/m² just short of rupture**. That is 1% open; 11's
       ruptured boundary is 25% open.
   - **The record falls along it** (§3):

     | Case | What happened | D on the curve |
     |---|---|---|
     | Lexington | Holding bulkhead bulged 6–8 in. with "slight weeps" | ≈ 1 |
     | Houston | Stuffing tubes | ≈ 1.9 |
     | Saratoga | Dished bottom | ≈ 2.2 |
     | Helena | Non-WT bulkhead | ≈ 2.4 |
     | Grosser Kurfürst | Belt forced in 33 cm | ≈ 3.1 |
     | Titanic | Seams parted, rivets sheared | ≈ 5 |
     | North Carolina | Holding-bulkhead seam opened a foot | > 5, which 11 already calls ruptured |

   - It also gives 15's shock bands back: 9–55 cm² for a 30 m² patch at NSF 0.2–0.45, against 15's 5–50.
4. **The orifice law is right for anything that matters.** A seam narrower than ~0.2 mm is viscous and passes almost
   nothing (Cd_eff 0.01–0.2). By 0.5–1 mm it is an orifice again (Cd_eff 0.5–0.6). The curve's D² weeping band absorbs
   the viscous part, so the sim keeps one flow law, `Cd·A·√(2g·Δh)` with Cd 0.6, which FLOODSTAND calls "well
   justified" [SRC].
5. **A sprung boundary leaks more as the head on it grows, and then gives way.**
   - Area × (1 + Δh / rating): a sprung bulkhead at its rating leaks twice what it leaked at low head.
   - At the rating it collapses (09 §2.4).
   - The flow then goes up roughly as Δh^0.6–1.0 instead of Δh^0.5. Slowing down, steaming astern and counterflooding
     to lower the head all help twice: less flow now, and further from collapse.
6. **Leaks get worse with time unless they are fought:**
   - **Working.** A sprung seam opens in steps, ×1.6 a step, capped at ×4. The steps come as a Poisson process whose
     rate grows with the head over the rating, with (1 + v/15 kn)², and with the sea. This is O'Brien's steady
     increase, Minneapolis's deck seams "working", and Pensacola's tank seam weeping 70× faster after the ship was
     whipped.
   - **Caulking** (oakum, wedges, plugs, blank flanges) keeps 15% (13). Shoring halves the working and raises the rating
     (09).
   - Caulking on the **hull side** works loose at speed: 15% per 10 min above 15 kn (13). Caulking on an internal
     bulkhead doesn't.
7. **What it gives** (§6):
   - **A battleship holding bulkhead that stood at D ≤ 1.5:**
     - With a drilled crew (a 30 t/h pump at 10 min, caulking at 20, shores at 25) the space behind it takes **0–50 t
       and is held**.
     - With no damage control the same space fills in 2–8 h.
     - At D ≥ 3 it fills either way.
     - That is North Carolina's "negligible" slow flooding with the crew, and Audacious or Shinano without one.
   - **A Lützow-type bow chain:** 2,500–4,000 t gets aft of the holed space in 4–8 h with no damage control, against
     her 4,209 t below the armour deck at ~6½ h. A drilled crew halves it.
   - **A destroyer** compartment next to a holed burst space, behind a welded bulkhead at D 3–4: half-full to the
     waterline in **4–20 min** without damage control, and held by one pump and caulking with it.
8. **What the sim needs** (§7). Per link it needs three numbers: a seam area, a fittings area and a working multiplier.
   It also needs two state flags (caulked, shored), the boundary's area and rating, and its fittings counts.
   - **At the hit:** one lognormal draw per loaded boundary, plus a few per fitting.
   - **Each step:** one area product per leaky link, and the working's Poisson draw.
   - **The knobs:** about a dozen. The two that matter most are r1 and caulk (§8).

---

## 1. The record

### 1.1 Numbers: leak rates turned into equivalent areas

Each case is turned into the orifice area that passes its rate at its head, `A = Q / (Cd·√(2g·Δh))`. Where a report
gives a rate per boundary, it's also given per m² of the boundary that took the load (ref §2). Heads and floor areas
are **[EST]** where the reports give none.

| Case | Rate | Head | Area | Per m² | Source |
|---|---|---|---|---|---|
| NC: Turret I stool, loosened rivets | 50 gal/h = 0.19 t/h | 6 m | 0.08 cm² | point | WDR 61 V-G-4 |
| NC: residual after caulking ("15 minutes pumping per watch", 30 t/h pump [EST]) | ~1.9 t/h | 3 m | 1.1 cm² | point | WDR 61 V-C-9 |
| Pensacola: riveted tank bulkhead, before Test A | 1 in. / 3 weeks | 4 m | 0.0005 cm² | 1e-5 | AD0366710 |
| Pensacola: the same after Test A ("violent motion") | 1 ft 2 in. / 4 days (×70) | 4 m | 0.04 cm² | 1e-3 | AD0366710 |
| Ralph Talbot: rudder-port packing | 1 in./day | 3 m | 0.006 cm² | point | AD0366723 |
| Flying Fish: hull leaks at 300 ft | 8,000 lb/h | 91 m | 0.4 cm² | point | WDR 58 App. I |
| Houston CL-81: magazine through loosened cable stuffing tubes, full in ~3 h | ~40 t/h | 2.5 m | 26 cm² | ~1 | WDR 53 |
| Chester CA-27: after engine room via shaft and bulkhead | 20,000 gal/h ("must have been greater") | 5 m | 35 cm² | point | WDR CA-27 |
| Helena CL-50: boiler room 4 "within two hours" through a non-WT bulkhead | ~250 t/h | 4 m | 130 cm² | ~2 | WDR 43 |
| Grosser Kurfürst: belt forced in 33 cm over 7.9 m, overnight, net of pumps | ~180 t/h | 2 m | 130 cm² | ~6.5 | 13 §2.1 |
| Saratoga: bottom dished 3–6 in., frames 68–137, unattended | ~2,000 t/h to sink in 7.6 h [UNCERTAIN] | 8 m | 710 cm² | ~1.7 | AD0366725 |
| Titanic: "equivalent opening of 12 square feet", ¾ in. average over ~200 ft | ~30,000 t/h | 7.6 m | 1.1 m² | ~120 | Wilding, BoT inquiry |
| NC: holding bulkhead seam opened ~1 ft over frames 45–52 (ruptured) | — | 6 m | ~1.3 m² | ~250 | WDR 61 V-F-3 |

Read it as five decades:
- **0.001–0.1 cm²:** weeps (rivets, packings). They matter only to a ship left alone for days, like Crossroads' unmanned
  hulks.
- **1 cm²:** what a good crew leaves behind after caulking.
- **10–100 cm²:** a boundary's loosened fittings, or a sprung bulkhead. A magazine or boiler room fills in 1–3 h.
- **100–1,000 cm²:** an opened belt joint, or a long dished band of hull. These are hundreds to thousands of t/h.
- **≥ 1 m²:** a torn seam. That is 11's rupture, or the Titanic.

### 1.2 Where the leaks were: Crossroads 1946

The flooding-sources study is the only systematic count [SRC, DTIC AD0366699, OCR]:

**Before the tests,** a typical capital ship's air-tested compartments rated 28% excellent, 22% passable and **50%
unsatisfactory**. About half "would easily permit progressive flooding". The study warns that the figures "represent a
higher degree of watertightness than actually exists". The share of leakage by source:

| Source | Share |
|---|---|
| Bulkhead and deck bolt holes, poor welds, opened riveted seams | 30% |
| Doors, hatches and hoist covers fitting poorly or warped | 20% |
| Cable stuffing tubes not tight | 20% |
| Vent ducts, valves and fittings | 10% |
| Stuffing boxes on hatch and door dogs | 4% |
| Leaky rivets | 4% |
| Piping | 3% |
| Valves, reach rods, bolted plates, junction boxes | 2% each |

**Test B (underwater burst), secondary flooding:**

| Source | Share |
|---|---|
| Loose doors | 19.9% |
| Shaft glands | 15.2% |
| Loosened hatch dogs | 13.5% |
| Reach rods and bulkhead fittings | 12.5% |
| Electrical stuffing tubes | 4.7% |

Opened shell seams and holes were 4.9% of the primary sources but **19.4% of the primary flooding**: few, and big.

**Who to blame:** 47% of the secondary flooding was caused by the test, 28% could have been prevented by a crew, and
25% came from sources that weren't tight before it.

So:
- The **fittings** are the many small leaks. They loosen at low shock and are a share of every damaged boundary.
- The **seams** are the few big ones, from a boundary or plating loaded close to its limit.
- **Tightness before the fight** (09's `tightness`) is a third of the answer.

### 1.3 Timelines

| Ship | What leaked | How it went | Source |
|---|---|---|---|
| **Lützow**, Jutland | Bow: torpedo flat holed; then bulkhead 249 "not completely watertight"; joints and speaking tubes into XIII, XII, XI | 1,038 t at 21:35, 2,395 t at 22:15, 8,351 t by ~02:00 (4,209 below the armour deck, 4,142 above); draught forward 12 m, then 15, then 17+; 3 kn "to ease pressure on the rear bulkhead of the torpedo flat"; forward pumps jammed | navygeneralboard (from German records); Campbell p.183 via Wikipedia [secondary] |
| **Seydlitz**, Jutland | Torpedo hole forward, then the forward bulkheads | 5,308 t; turned and steamed stern-first "to relieve the strained bulkheads"; pump steamers stabilised her | Staff; Wikipedia [secondary] |
| **Audacious**, 1914 | Mine under the port engine room; pipe and valve seals through bulkheads, broken pipes, hatches that wouldn't close; not at action stations | Port engine room at once; centre engine room slowly; engine rooms abandoned ~2 h; power lost; capsized 12 h after the hit | Wikipedia (Roberts, review board) |
| **Houston** CL-81 | Cable stuffing tubes in bulkhead 57; door leaks; bottom connections | After engine room full in ~30 min; magazines full at ~3 h; spaces with 6 in. to 3 ft held by shores, caulking and buckets; "innumerable small leaks proved to be a nuisance" with 8 portable pumps | WDR 53 |
| **Yorktown**, Midway | "Weakened bulkheads, piping and fittings" | Progressive flooding "throughout the night"; free surface; capsized ~16 h after the last hits | WDR, Yorktown |
| **O'Brien** (DD) | "Considerable leakage… through seams and rivet holes" into the forward engine room | Held near pump capacity; temporary repairs reduced it to one pump; in a seaway, step by step: "slow increase", one pump "at high speed", the circulator on bilge suction; then a plate "cracked open with the noise of a pistol shot", the leak jumped, and she broke up 34 days after the torpedo. The report: leakage "would have been much less had the shell been welded" | WDR 28 |
| **Saratoga**, Crossroads B | Bottom dished 3–6 in. over ~84 m; one tear at a strut | List 3° at +2.6 h, 6° at +3.4 h; sank at +7.6 h with nobody aboard | AD0366725 |
| **Nagato**, Crossroads B | Stern tubes and shell leaks, a 2 ft hole | List 5°, then 8°; sank after 4–5 days, unmanned | AD0366725 |
| **Hughes** (DD), Crossroads B | Broken small piping, a sea-chest flange, unplugged stuffing tubes | Forward engine room ~4 ft/h; "no shell leaks"; weeping rivets and butts minor; beached against progressive flooding | AD0801404 |
| **Shinano** | Compartments never air-tested; penetrations unsealed; doors missing or open | List 10°, then 13° after counterflooding; 20° at +2¾ h when a boiler room flooded; lost at +7¾ h; run at speed throughout | Wikipedia [secondary] |
| **Musashi** | "Popped rivets leak into boiler room 7" | One torpedo: ~3,000 t, held; then the hits added up over 9 h | combinedfleet TROM [secondary] |
| **North Carolina** | Holding bulkhead failed at rivets (ruptured); 14 doors and bulkheads shored | "The extent of slow flooding was negligible"; three spaces partly flooded by seepage, a few inches each, pumped by submersibles | WDR 61 |

**The pattern:**
- With a crew working, the seepage after a hit is held at tens of tonnes (North Carolina, Lexington, Houston's lesser
  spaces).
- Without one, or without power for the pumps, the same paths fill compartment after compartment over hours
  (Audacious, Yorktown, Saratoga, Shinano).
- Speed and head make it worse, and slowing was the standard answer (Lützow, Seydlitz).

### 1.4 Riveted against welded

The evidence is qualitative and points one way:
- **O'Brien:** "much less had the shell been welded".
- **Ralph Talbot:** "welds throughout the ship held while numerous riveted and bolted joints failed".
- **Formidable:** the deck failed at "a riveted connection… welded seams held" (11).
- **Arkansas:** "butts and seams… parted in many places due to rivets shearing", opening 12–15 in.
- **The Bureau** on "the relative weakness of the riveted seams and butts" (13).

A welded seam either holds or cracks. It doesn't open a little all along its length. The model takes **welded × 0.4 on
the seam leak** at a given D [EST]. The fittings stay the same, since a welded ship's doors and tubes loosen just the
same. 11 already makes riveted boundaries ×0.85 weaker, which raises their D.

---

## 2. Physics: is a seam an orifice?

A seam is a slot with gap g and a path of length L: the plate's thickness (15 mm) for a butt crack, or the lap (~3
rivet diameters, 75 mm) for a lap seam. Water through it loses head at the entry and exit, as an orifice does, and to
viscous shear along the slot (plane Poiseuille), in series:

`ρ·g·Δh = ½·ρ·u²/Cd² + 12·μ·L·u/g²`

The effective Cd of a slot (an orifice is 0.60) [ref §1]:

| Gap | Butt crack, L 15 mm (0.5 / 2 / 8 m head) | Lap seam, L 75 mm (0.5 / 2 / 8 m) |
|---|---|---|
| 0.05 mm | 0.02 / 0.04 / 0.08 | 0.00 / 0.01 / 0.02 |
| 0.1 mm | 0.08 / 0.15 / 0.27 | 0.02 / 0.03 / 0.07 |
| 0.2 mm | 0.27 / 0.39 / 0.48 | 0.07 / 0.13 / 0.23 |
| 0.5 mm | 0.52 / 0.56 / 0.58 | 0.31 / 0.42 / 0.50 |
| 1 mm | 0.58 / 0.59 / 0.59 | 0.50 / 0.55 / 0.57 |
| 3 mm | 0.60 | 0.59 / 0.59 / 0.60 |

Per metre of lap seam at 4 m: 0.1 mm passes 0.15 t/h, 0.2 mm 1.1 t/h, 0.5 mm 7.6 t/h, 1 mm 18 t/h.

**What follows:**
- A weeping seam (≤ 0.1 mm) is viscous, so its flow goes as Δh, not √Δh, and is tiny. That is why North Carolina's
  loosened rivets passed 50 gal/h and Pensacola's tank seam an inch in three weeks.
- From about 0.5 mm a seam is an orifice. Every leak that sinks ships is in this range.
- So the sim doesn't need a second flow law. The curve's weeping band (D < 1, ~D²) carries the viscous part as small
  equivalent areas, and above it the orifice law is the physics.
- FLOODSTAND's full-scale tests support Cd 0.6 for openings of all kinds but ducts and pipes [SRC].
- **What the curve means as a gap:** a 150 m² transverse bulkhead with its leak all in its ~62 m of edge angles has a
  0.07 mm gap at D 1, 0.3 mm at D 2, 1.3 mm at D 3, 6 mm at D 4 and 24 mm just short of rupture (ref §3). Edges are
  where the record puts the failures: North Carolina's "connecting angles… pulled out", Lützow's "bulkhead joints",
  the boundary angles caulked at Tonga.

---

## 3. The curve

### 3.1 The damage index D

`D` is the load a boundary took over the load at which it starts to deform. It takes the scales the other notes already
compute:
- **A burst inside (11's cascade):** D = P_felt / P_deform, with P_deform = 0.012 MPa per mm of plate (×0.85 riveted)
  and P_rupture = 5 × P_deform.
  - A boundary 11 ruptures has D ≥ 5 and gets 11's opening (¼ of its area).
  - The rest get the curve.
- **Underwater shock (15):** D = 5 × NSF at the boundary.
  - NSF 0.2, where 15's bands say "seams open, slow flooding", is D 1.
  - NSF 1.0, "plating split or holed", is D 5.
  - The affected area is the patch of plating, and the transverse bulkheads within it, where the local NSF is above
    0.06.
- **A heavy hit that didn't get through (13 §2):** 13's leak regimes already are areas. Seep (2–20 cm²) and moderate
  are the curve at D ≈ 1.5–3 for the 8–20 m² behind a plate. 13's `belt_leak()` stays as it is.
- **Splinter holes (13_fragmentation, 15 §6.4)** are holes, not seams. They are separate, as now.

### 3.2 r(D)

`r(D)`: cm² of leak per m² of the boundary that took the load [FIT, to §1.1; ref §3]

| D | State | r cm²/m² | 30 m² DD bulkhead | 150 m² BB bulkhead | BB bulkhead at 4 m |
|---|---|---|---|---|---|
| < 0.3 | intact | 0 (base tightness only) | — | — | — |
| 0.3 | weeping | 0.027 | 0.8 cm² | 4 cm² | 8 t/h |
| 0.5 | weeping | 0.075 | 2 cm² | 11 cm² | 22 t/h |
| 1 | sprung | 0.3 | 9 cm² | 45 cm² | 88 t/h |
| 1.5 | sprung | 0.62 | 19 cm² | 93 cm² | 180 t/h |
| 2 | sprung | 1.3 | 39 cm² | 190 cm² | 380 t/h |
| 3 | opened | 5.5 | 160 cm² | 820 cm² | 1,600 t/h |
| 4 | opened | 23 | 700 cm² | 0.35 m² | 6,900 t/h |
| → 5 | opened | 100 | 0.3 m² | 1.5 m² | 29,000 t/h |
| ≥ 5 | ruptured (11) | 2,500 | 7.5 m² | 37 m² | — |

- **Shape:** 0 below D_weep (0.3); r1·D² up to D = 1; then log-linear, r1·(r5/r1)^((D−1)/4), up to D = 5.
- **Spread:** each boundary's draw is lognormal about the curve, σ = 0.8 (×/÷ 2.2) [EST]. The record's scatter at a
  given load is wide: Oryol's belt held dry where Lion's was driven in.
- **Welded:** × 0.4 below rupture [EST, §1.4].

**The anchors on the curve** (the D whose r matches their per-m² area):

| Case | Per m² | D |
|---|---|---|
| Lexington's holding bulkhead, bulged 6–8 in., "slight weeps" | ~0.3 [EST] | ≈ 1 |
| Houston's bulkhead 57 (stuffing tubes) | 1.0 | 1.9 |
| Saratoga's dished bottom | 1.7 | 2.2 |
| Helena's non-WT bulkhead | 2.1 | 2.4 |
| Grosser Kurfürst's belt, forced in 33 cm | 6.5 | 3.1 |
| Titanic's seams and rivets | 120 | 5.1 (at rupture) |
| North Carolina's holding-bulkhead seam | 250 | > 5 (11 has it ruptured by its rivets) |

The order is the record's. A bulge with weeps is the bottom, plating forced in by a 15″ shell is the middle, and plates
against an iceberg or a torpedo's gas bubble are the top.

**Against 15's shock bands** (a 30 m² patch, ref §3b):

| NSF | D | This curve | 15 §6.4 said |
|---|---|---|---|
| 0.1 | 0.5 | 2 cm² | seams weep |
| 0.2 | 1 | 9 cm² | 5–50 cm² |
| 0.3 | 1.5 | 19 cm² | 5–50 cm² |
| 0.45 | 2.2 | 55 cm² | 5–50 / 50–500 |
| 0.7 | 3.5 | 340 cm² | 50–500 |
| 0.95 | 4.8 | 2,100 cm² | 50–500 |
| 1.0 | 5 | a hole | a hole |

- The two agree up to NSF ~0.7.
- Above that the curve is up to ~4× higher than 15's guess. That is the "plating badly buckled, moderate flooding"
  band, where Somali (NSF 0.34, "plating fractured… flooding") and Fame (1.0) sit.
- **The curve replaces 15's two bands.**

---

## 4. Fittings

Shock and blast loosen fittings at loads too low to spring a seam (§1.2). The model gives each boundary counts of
fittings by kind. Each one loosens with a chance that grows with D, and then leaks a log-uniform area [EST, checked
against Houston, Chester, Helena and North Carolina's vent]:

| Kind | Starts at D | Chance | Area each | The record |
|---|---|---|---|---|
| Door dogs loosened, door dished | 0.5 | 0.35 per unit D | 10–60 cm² | Mayrant's doors dished 2–4 in.; Houston's B-312-L to 3 ft; Crossroads B's 19.9% |
| Hatch dogs | 0.5 | 0.30 | 10–50 cm² | Crossroads B's 13.5% |
| Cable stuffing tube (≈ 1 per 5 m² of a machinery or magazine bulkhead) | 0.3 | 0.12 | 0.5–3 cm² | Houston's magazines |
| Vent closure | 0.5 | 0.25 | 5–50 cm² | North Carolina: 2 ft into turret I's lower handling room through a vent |
| Reach rod | 0.5 | 0.20 | 1–5 cm² | Crossroads B's 12.5% (with fittings) |
| Shaft gland | 0.3 | 0.50 | 10–100 cm² | Crossroads B's 15.2%; Chester; Prince of Wales |
| Pipe flange | 0.5 | 0.25 | 5–130 cm² | Helena: "squirting with some force from a flanged joint" |

**A battleship machinery bulkhead** (150 m²; 2 doors, 30 tubes, a vent, 2 rods, a pipe; ref §4):

| D | Fittings loosened | Their area, mean (p90) | Seam area | Fittings' share |
|---|---|---|---|---|
| 0.5 | 3 | 22 (55) cm² | 11 cm² | 66% |
| 1 | 6 | 42 (89) cm² | 45 cm² | 48% |
| 2 | 11 | 80 (140) cm² | 190 cm² | 29% |
| 3 | 16 | 110 (170) cm² | 820 cm² | 12% |

At low D the fittings *are* the leak, which is Crossroads B. At high D the seams are, which is Lützow's bow or a belt
forced in. Two consequences:
- **A closed, dogged door is a fitting, not a hole.** 11's "doors jam" is the same event seen from the damage-control
  side: the door now leaks *and* can't be opened to pass a party or a hose.
- **Fittings are the damage-control party's work.** Re-dogging and wedging a door, repacking a gland and plugging a
  tube are 09's "close and dog" and "caulk", 1–30 min each. The first minutes of good damage control fix most of the
  area at low D.

---

## 5. Head and time

### 5.1 Head

A sprung panel is held shut by what's left of its connections, and the head across it pushes it open. The model:
- **A sprung seam's area** × (1 + β·Δh / h_rating), β = 1 [EST]. h_rating is 09's collapse head, 1.3 × the height of
  the bulkhead over the leak, ×1.6 when shored.
- **At the rating** the leak is twice what it is at low head, and then the boundary collapses (09 §2.4) into a large
  opening.
- **The flow** goes as about Δh^0.6 at a fifth of the rating, rising to Δh^1.0 at the rating (ref §5).
- **Fittings don't open with head** (β = 0). A door seated by the pressure would even close, but that's not modelled.

**What it means for play:**
- Lowering the head pays twice: by slowing (the ram head, 09 §2.2), by counterflooding or trimming the other way
  (North Carolina trimmed 7 ft by the stern to pump A-310L), or by flooding the next space on purpose to balance a
  bulkhead (North Carolina flooded A-62V to shore its forward bulkhead).
- That is Lützow at 3 kn and Seydlitz astern.

### 5.2 Time: working, and what damage control does

**Working** [EST, shaped by O'Brien, Minneapolis and Pensacola]:
- A sprung seam opens a step (area ×1.6) at a Poisson rate:
  `λ = 0.1/h × (Δh / h_rating) × (1 + v / 15 kn)² × (1 + sea) × (½ if shored)`
  - `sea` is about 1 in a moderate sea and 3 in a gale.
- It saturates at ×4. Beyond that the seam is "opened", and the rating decides.
- At 18 kn and 0.9 of its rating, a sprung bulkhead works about once every 2½ h.
- This is the slow drift that turned O'Brien's held leak into a lost ship, and Pensacola's tank seam's ×70 after the
  test.

**Damage control** (09's catalogue; 13's numbers):
- **Caulking** a sprung seam (oakum, wedges, soft wood, blank flanges) keeps 15%. Fittings re-dogged, plugged or
  repacked keep 20%. Each task takes 5–30 min.
- **Shoring:** working ×0.5, rating ×1.6 (09).
- **Caulking on the hull** works loose at speed: 15% per 10 min back toward full above 15 kn (13; Bismarck's mat).
  Caulking on an internal bulkhead holds, except as its seam works.
- **Pumps** take what's left. Houston's eight portables and North Carolina's "15 minutes pumping per watch" are this
  end of the fight.

---

## 6. What it gives

### 6.1 A battleship holding bulkhead that stood (North Carolina's case)

A torpedo floods the side and TDS layers at once. The space behind the holding bulkhead (600 m³, floor 9 m under the
waterline, ~490 t to fill) is reached only through a 60 m² bulkhead that took load D, at 18 kn. Water in the space
(t), medians of 40 draws [ref §7.1]:

| D | Leak (seam + fittings) | No DC: 0.5 / 2 / 8 h | Drilled DC (30 t/h pump at 10 min, caulk at 20, shore at 25): 0.5 / 2 / 8 h |
|---|---|---|---|
| 0.5 | 5 cm² | 14 / 56 / 290 | 0 / 0 / 0 |
| 1 | 16 cm² | 34 / 140 / full | 15 / 0 / 0 |
| 1.5 | 42 cm² | 84 / 300 / full | 50 / 48 / 44 |
| 2 | 65 cm² | 145 / 410 / full | 100 / 105 / 170 |
| 3 | 240 cm² | 400 / full / full | 320 / 390 / full |
| 4 | 930 cm² | full | full |

- **D ≤ 1.5 is a fight the crew wins,** and only the crew: North Carolina's "negligible" slow flooding and "a few
  inches" in three spaces.
- **Without damage control, the same bulkhead loses the space in 2–8 h.** That is Audacious's centre engine room and
  Shinano's boiler rooms.
- **D ≥ 3 is lost either way,** unless more pumps come. Grosser Kurfürst lived overnight with ~180 t/h net coming in.

### 6.2 A Lützow-type bow

The holed space is the broadside torpedo flat, with a ram head of 0.5·v²/2g. Four 1,500 m³ compartments are in a chain
aft of it, behind 150 m² bulkheads. The first bulkhead took D. The others are undamaged but leak at their tightness,
which is 20 cm² for a good ship and 200 cm² for an untested or worked one: Lützow's "not completely watertight", her
joints and speaking tubes. Water aft of the torpedo flat (t, medians of 30) [ref §7.2]:

| First bulkhead D | Others | Speed | DC | 1 h | 2 h | 4 h | 8 h |
|---|---|---|---|---|---|---|---|
| 1.5 | 20 cm² | 7 kn | none | 400 | 700 | 1,080 | 1,440 |
| 1.5 | 200 cm² | 15 kn | none | 470 | 870 | 1,570 | 3,060 |
| 2.5 | 200 cm² | 15 kn | none | 1,210 | 1,840 | 2,750 | 4,030 |
| 2.5 | 200 cm² | 7 kn | none | 1,070 | 1,650 | 2,480 | 3,640 |
| 2.5 | 200 cm² | 3 kn | none | 1,040 | 1,610 | 2,420 | 3,560 |
| 2.5 | 200 cm² | 15 kn | drilled | 790 | 920 | 1,220 | 1,760 |
| 2.5 | 200 cm² | 7 kn | drilled | 680 | 790 | 1,030 | 1,460 |
| 3.5 | 200 cm² | 15 kn | none | 1,750 | 2,260 | 3,110 | 4,310 |

- **Against Lützow:** 4,209 t below the armour deck at ~6½ h after the torpedo-flat hits, with jammed pumps and three
  failed patches. The model gives 3,000–4,000 t by then for a worked first bulkhead, untight others and no effective
  damage control. That is the right size.
- **What decides it:** the tightness of the *undamaged* bulkheads matters as much as the sprung one. At 20 cm² the
  chain stops at the second compartment; at 200 cm² it walks aft. This is §1.2's third of the answer: tightness
  before the fight.
- **Speed matters less here than the history suggests,** because in this chain the ram head reaches only the holed
  space, and nothing in the chain collapsed. Lützow and Seydlitz slowed to save the bulkheads from **collapse**: the
  ram head on a forward bulkhead, plus the trim putting the bow deeper. At 28 kn the ram head (5 m, 09 §2.2) takes a
  forward bulkhead past its rating, as 09's "holds 28 kn" case shows.

### 6.3 Houston's magazine

A 150 m³ magazine (123 t) behind an intact bulkhead, next to a flooded space, through loosened stuffing tubes only
[ref §7.3]:

| Tubes loose | Total | 0.5 / 1 / 2 / 3 h | Full by |
|---|---|---|---|
| 6 × 1 cm² | 6 cm² | 8 / 16 / 32 / 47 t | not by 8 h |
| 12 × 1.5 cm² | 18 cm² | 24 / 47 / 87 / 121 t | 3 h |
| 20 × 2 cm² | 40 cm² | 52 / 95 / full | 2 h |

Houston's "completely flooded" at ~3 h needs about a dozen loosened tubes: the §4 table at D 1–1.5.

### 6.4 Saratoga's bottom

84 m × 5 m of bottom dished by shock. No holes, six 2,500 m³ spaces behind it (13,800 t), nobody aboard [ref §7.4]:

| D | Leak | 1 h | 2 h | 4 h | 8 h |
|---|---|---|---|---|---|
| 1.5 | 260 cm² | 800 | 1,570 | 3,040 | 5,700 |
| 2 | 540 cm² | 1,620 | 3,140 | 5,870 | 10,130 |
| 2.5 | 0.11 m² | 3,240 | 6,040 | 10,360 | all |

Saratoga's list came on over 2–3 h and she sank at 7.6 h, which is D ≈ 2–2.5. That is NSF ≈ 0.4–0.5 by the
D = 5·NSF rule, and fits a carrier 300 yd from Baker. The same band with a crew and power is a matter of a few pumps.

### 6.5 A destroyer's neighbour

A holed burst space, and its neighbour behind a 30 m² welded bulkhead at D (11's 8″ HC case leaves two bulkheads
"deformed"; heavy HE more). The neighbour can fill to the waterline (~78 t) [ref §7.5]:

| D | Leak | No DC: to 50% / 90% | DC (caulk at 5 min, one 25 t/h pump): t at 30 min |
|---|---|---|---|
| 1 | 4 cm² | > 2 h | 0 |
| 2 | 15 cm² | 79 min / > 2 h | 0 |
| 3 | 66 cm² | 19 / 48 min | 10 t |
| 4.2 | 375 cm² | 4 / 9 min | 64 t |

- A destroyer's next compartment is lost in minutes only when the bulkhead was hit near its limit.
- Otherwise it is a 20–80 minute fight that a crew with a pump wins.
- This is WDR 51's record: shell hits rarely flooded beyond the hit compartment where the crew was alive and the
  pumps had power.

---

## 7. Proposed game model

### 7.1 Data

Per **link** (bulkhead or deck), beside 4.3b's base tightness and the opening's height (09 §7.1 already names
`sprung_area` and `open_fittings_area`):

| Field | What |
|---|---|
| `area_m2`, `riveted`, `rating_m` | From the class: the boundary's size, its construction, and 09's collapse head (1.3 × its height over the leak) |
| `fit_counts` | Doors, hatches, tubes, vents, rods, pipes, glands by kind; a default by room kind until shipgen emits them (tubes ≈ area/5 on machinery and magazine bulkheads; 1 door per WT bulkhead with a passage; glands where a shaft passes) |
| `seam_m2` | The seam leak at the hit, from r(D) and the lognormal draw |
| `fit_m2` | The loosened fittings' area |
| `work` | Working multiplier, 1…4 |
| `caulked`, `fit_caulked`, `shored` | Damage control's flags |
| `D_max` | The highest D the link has taken (a second hit takes the max of the two, plus ½ of the lesser: repeat hits loosen more [EST]) |

Per **ship:** `tightness` (09: 1 air-tested … 25 unsealed), which multiplies the base leak of every link that has
taken no damage.

### 7.2 At the hit

For every boundary the hit loaded:
1. **D** from 11's cascade (P_felt/P_deform), 15's NSF (5 × NSF), or the contact rule.
2. **D ≥ 5:** 11's rupture, as now.
3. **Otherwise:**
   - `seam_m2 += r(D)·area·lognormal(σ 0.8)` (× 0.4 if welded);
   - each fitting rolls to loosen (§4) and adds its area;
   - doors that loosened are also **jammed** (11).
4. **The log gets** "bulkhead 12–13 sprung (≈ 90 cm²); 2 doors dished; 7 stuffing tubes loose".

### 7.3 Each flooding step

This is the existing `Flooding.Step`. The link's area becomes:

```
A = base·tightness
  + seam_m2 · work · (caulked ? 0.15 : 1) · (1 + Δh / (rating · (shored ? 1.6 : 1)))
  + fit_m2  · (fit_caulked ? 0.2 : 1)
```

- **Collapse:** Δh (+ ½ ram, forward) > rating × shoring → collapse (09), a large opening, and a log line.
- **Working:** draw the next working time from the Poisson rate (§5.2), or keep it as a deterministic hazard
  integrated per step and fire when it crosses a seeded threshold. One draw per sprung link per event, not per step,
  stays deterministic at any step size.
- **Hull links' caulking** creeps back above 15 kn (13).

**Cost:**
- At the hit: a few dozen floats per sprung link, 1 + (fittings) random draws.
- Each step: one extra multiply-add per leaky link.
- Nothing allocates; the fittings counts are on the class.

### 7.4 Damage control's tasks

These are 09 §4's, applied to these fields:

| Task | Acts on | Time (drilled crew, 09's multipliers) | Effect |
|---|---|---|---|
| Re-dog, wedge, plug | fittings | 1–5 min per door, ~1 min per tube or rod | `fit_caulked` |
| Caulk seams, boundary angles | seam | 10–40 min per bulkhead, from the dry side | `caulked` |
| Shore | the bulkhead | 10–30 min a set | `shored` |
| Rig a pump | the space | 5–15 min | removes 25–270 t/h |
| Slow, trim, flood to balance | the head | captain, DC Central | lower Δh: less flow, further from rating |

The **priority** follows 09 §3:
- Boundaries of the flooded region first, ordered by inflow × the value of what's behind them: a magazine, a boiler
  room, the next WT space of a chain.
- Then the slow ones.

### 7.5 What the player sees

- **On the flooding schematic (09 §7.4):** a sprung link glows by its inflow. Its tooltip gives the cause chain:
  "bulkhead 12–13 · sprung by the 14″ burst in 12 · 90 cm², 7 tubes loose · 180 t/h into the forward engine room ·
  shored, not caulked".
- **In the water budget:** "seepage" as its own line, beside holes and pumps, with a trend.
- **The doom clock** gains a cause. A list that grows 0.5° an hour, with no hole under water, is the working of sprung
  seams.

### 7.6 The coarse tier (4.8)

- A leak into a space that isn't full is a draining-tank problem with a closed form (Torricelli), so a minute's step
  stays exact.
- Working becomes an expected multiplier per minute.
- Ships off screen keep 09 §7.5's LOD: total seepage by link class, decaying with `dc_skill` and growing with
  `tightness` and sea.

---

## 8. The knobs

All are in `KNOBS` in the ref, and would become `SimSettings`:

| Knob | Value | What it is | Tag |
|---|---|---|---|
| `D_weep` | 0.3 | below this, no damage leak | [EST] |
| `r1` | 0.3 cm²/m² | leak at D = 1 (seams just sprung) | [FIT] |
| `r5` | 100 cm²/m² | leak just short of rupture (1% open) | [FIT] |
| `r_rupt` | 2,500 cm²/m² | ruptured (11's ¼) | [SRC 11] |
| `sigma_ln` | 0.8 | scatter of one boundary's draw | [EST] |
| `welded` | 0.4 | seam multiplier for welded construction | [EST] |
| `beta` | 1 | head opening at the rating | [EST] |
| `lam0` | 0.1 /h | working rate at the rating, stopped, calm | [EST] |
| `work_step`, `work_max` | ×1.6, ×4 | working step and cap | [EST] |
| `v0_kn` | 15 kn | speed scale of working | [EST] |
| `sea` | 0–3 | sea-state term | [EST] |
| `caulk`, fittings caulk | 0.15, 0.2 | what damage control leaves | [SRC 13] / [EST] |
| fittings table | §4 | thresholds, chances, areas | [EST] |

**Sensitivity** [ref §8]: §6.1's space behind a D 1.5 bulkhead at 18 kn, water in tonnes, 20 draws (so ±50% noise):

| Knob | Low → base → high | No DC at 2 h | Drilled DC at 8 h |
|---|---|---|---|
| r1 | 0.1 → 0.3 → 1.0 | 236 → 342 → full | 0 → 108 → 329 |
| r5 | 30 → 100 → 300 | 324 → 342 → 367 | 81 → 108 → 134 |
| beta | 0 → 1 → 2 | 258 → 342 → 387 | 37 → 108 → 150 |
| lam0 | 0 → 0.1 → 0.5 | 307 → 342 → 432 | 42 → 108 → 224 |
| sea | 0 → 1 → 3 | 342 → 355 → 408 | 108 → 125 → 224 |
| caulk | 0.05 → 0.15 → 0.4 | — | 8 → 108 → 283 |

- **r1 and caulk decide whether a crew holds a sprung bulkhead.** They are the two to tune against play.
- r5 hardly matters below D 3. It matters for the near-rupture cases, which the curve shares with 11's threshold.
- Working, β and sea are second-order over hours, and first-order over a night (O'Brien, Yorktown).

---

## 9. Where it's weak

- **Few true rates.** Most of §1.1's areas need an estimated head or floor area. Saratoga's needs an estimated
  tonnage to sink [UNCERTAIN]. The curve is fitted to the *order* of the record more than to its numbers, within a
  factor of ~2–3 at each anchor.
- **D for shock isn't calibrated against measured seam leaks.** D = 5·NSF is a mapping between 15's bands and 11's
  thresholds, not a fit. Crossroads gives the paths, not the rates, so this is the place to tune with play.
- **The fittings table is judgement.** The Crossroads shares and Houston's 3 h say the shape is right. The per-kind
  areas aren't measured.
- **Riveted × 1, welded × 0.4** rests on qualitative statements.
- **Welded ships crack instead.** O'Brien's last plate was riveted, but wartime welded hulls cracked in cold seas. A
  crack hazard for hull plating at D ≥ 3 in a seaway (area jump 0.02–0.1 m²) would be the next refinement [EST].
- **Directionality is ignored.** A dished panel or a door leaks more when the head pushes it off its seat than onto
  it. Doors on WT bulkheads are hung to seat against flooding from the likelier side.
- **Intact tightness stays 4.3b's,** a flat 5 cm² per link × `tightness`. Real tightness varies by compartment (§1.2:
  50% unsatisfactory). A per-link draw at spawn would give each ship its own weak links.
- **Not covered here:**
  - **Decks.** The same rule applies, with the head being the water's depth over the deck. Leaks downward through
    hatches, scuttles and trunks are fittings.
  - **Free surface, counterflooding and pumping:** 09.

---

## Sources

**Primary reports:**
- [USS North Carolina, War Damage Report 61](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html) [SRC]
- [USS Houston CL-81, War Damage Report 53](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-houston-cl81-war-damage-report-no-53.html)
- [USS O'Brien, War Damage Report 28](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-obrien-dd415-war-damage-report-no-28.html)
- [USS Lexington CV-2, War Damage Report 16](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-lexington-cv2-war-damage-report-no16.html)
- [USS Helena CL-50, War Damage Report 43](https://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCL50/WarDamageReportCL50.html)
- [USS Chester and Minneapolis, war damage reports](https://www.ibiblio.org/hyperwar/NHC/WarDamageReports/WarDamageReportCA24-CA33/WarDamageReportCA27-CA36.html)
- [USS Yorktown CV-5, Midway, war damage report](https://www.ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20YORKTOWN%20(CV-5),%20BOMB%20AND%20TORPEDO%20DAMAGE%20-%20Midway,%20June%204%20to%207,%201942%20(LOST%20IN%20ACTION).pdf)
- [USS Marblehead CL-2, war damage report](https://www.ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20MARBLEHEAD%20(CL-2),%20BOMB%20DAMAGE%20-%20Java%20Sea,%20February%204,%201942.pdf)
- [War Damage Report 58, submarines, Appendix I](https://ibiblio.org/hyperwar/USN/rep/WDR/WDR58/WDR58-I.html)
- [Destroyer war damage report: gunfire, bombs, kamikazes](https://ibiblio.org/hyperwar/NHC/WarDamageReports/WarDamageReportDDGunBombKamikaze/WarDamageReportDDGunBombKamikaze.html)
- [Summary of war damage to surface ships, Dec 1943 – Dec 1944](https://ibiblio.org/hyperwar/USN/WarDamageReports/SummWarDamageSurface-8DEC43-7DEC44/SummWarDamageSurface-8DEC43-7DEC44.html)

**Operation Crossroads (DTIC, scanned and OCR'd):**
- [Study of flooding sources](https://apps.dtic.mil/sti/pdfs/AD0366699.pdf)
- [Saratoga, Nagato, Arkansas, Pensacola, Prinz Eugen, Test B](https://apps.dtic.mil/sti/pdfs/AD0366725.pdf)
- [Pensacola, Test A](https://apps.dtic.mil/sti/pdfs/AD0366710.pdf)
- [Ralph Talbot](https://apps.dtic.mil/sti/pdfs/AD0366723.pdf)
- [Hughes](https://apps.dtic.mil/sti/pdfs/AD0801404.pdf)
- [Nevada](https://apps.dtic.mil/sti/pdfs/AD0367453.pdf)
- [Wainwright](https://apps.dtic.mil/sti/pdfs/AD0367466.pdf)
- [Sakawa](https://apps.dtic.mil/sti/pdfs/AD0366701.pdf)

**Flooding engineering:**
- [FLOODSTAND final report](https://cordis.europa.eu/docs/results/218/218532/final1-final-report-en-floodstand-218532-rev-20120828.pdf): Cd 0.6; door and panel leakage and collapse heads
- [Ruponen et al., STAB 2009](https://mail.shipstab.org/files/Proceedings/STAB/STAB2009/STAB_2009_s05-p6.pdf)
- [Rodrigues, JMSE 12/251 (2024), review of flooding modelling](https://www.mdpi.com/2077-1312/12/2/251)

**Lützow, Seydlitz, Audacious, Shinano, Yamato and Musashi** [secondary]:
- [navygeneralboard.com on Lützow](https://www.navygeneralboard.com/?p=1083)
- [jutland1916.com on Lützow](https://www.jutland1916.com/ship/lutzow)
- [Wikipedia: SMS Lützow](https://en.wikipedia.org/wiki/SMS_L%C3%BCtzow)
- [Wikipedia: SMS Seydlitz](https://en.wikipedia.org/wiki/SMS_Seydlitz)
- [Wikipedia: HMS Audacious](https://en.wikipedia.org/wiki/HMS_Audacious_(1912))
- [The Past on HMS Audacious](https://the-past.com/shorts/ideas/back-to-the-drawing-board-hms-audacious/)
- [Wikipedia: Shinano](https://en.wikipedia.org/wiki/Japanese_aircraft_carrier_Shinano)
- [combinedfleet: Yamato](https://www.combinedfleet.com/yamato.htm)
- [combinedfleet: Musashi](https://www.combinedfleet.com/musashi.htm)
- [Wikipedia: Yamato](https://en.wikipedia.org/wiki/Japanese_battleship_Yamato)

**Titanic:**
- [Encyclopedia Titanica, "Somewhere about 12 square feet"](https://encyclopedia-titanica.org/somewhere-about-12-square-feet.html)
- [Tim Maltin on Wilding's evidence](https://timmaltin.com/2019/03/30/titanic-iceberg-gash/)

**This project's notes:** `09_damage_control.md` §2.3–2.4, §4, §7; `11_blast_confinement.md` §3.2, §5.1;
`13_nonpenetrating_hits.md` §2; `15_near_misses.md` §3, §6.4; `fighting.md` 4.3b.
