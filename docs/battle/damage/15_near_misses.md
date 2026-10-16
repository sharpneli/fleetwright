# 15 — Near misses: splinters, shock and short shells

*Research for the damage model, after `13_fragmentation.md` and `14_spall_behind_armour.md`. It answers the open item
in `08_gunfire_effects.md` §8 ("near-miss splinters") and the second half of step **E3** in `shells-armour-damage.md`:
a shell that bursts in the water close aboard becomes a new `Hit` source, a burst at a point outside the hull.*

*The question as asked: "Shells bursting in the water close aboard: splinters into the side and upper works, underwater
shock that springs plating, and short-falling shells that burst against the hull below the waterline. It matters most
for destroyers under heavy HE."*

*13 built the fragment field (Gurney, Mott, drag, THOR, the side band, the lean) and sketched near misses in its §5.2.
This note takes that field as given and adds what a miss needs: where the shell bursts, what the water does to the
spray, the ship's geometry above the water, underwater shock, the branch to P6's underwater hits, and how often it
happens in a salvo.*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the cases, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion code:** `near_miss_ref.py` (pure Python, about 90 s). It **imports** `fragmentation_ref.py` for the burst
(one implementation). Every table here comes out of it, section by section ("ref §n").

---

## 0. Short answer

1. **A near miss is a burst outside the hull, and the fuze decides which kind, not the calibre.**
   - A **nose fuze** (HE, HC, nose-fuzed common) fires as the shell meets the sea. It bursts *at the surface* and
     throws **splinters**, with little underwater shock.
   - A **base fuze with a delay** (AP, SAP, base-fuzed common) starts its clock on the water and fires metres *under*
     it. That makes **shock** and no splinters.
   - Or the base-fuzed shell **reaches the hull first**: P6's underwater hit, unchanged.

   This is Samar's record. The battleships' AP near misses did little to the destroyers. One 46 cm shell bursting under
   *White Plains* sprang her hull and machinery without touching her.
2. **The burst's depth decides the splinters, and 13 §5.2 left it out.** In water a fragment's speed falls 1/e every
   3 cm (1 g) to 30 cm (1 kg). So the spray must start within centimetres of the surface to fly at all.
   - **The test:** a 46 cm Type 0 burst 25 m short of 13's flat 8 mm destroyer side. Exactly at the surface it puts 97
     fragments through, as 13 found. If the fuze fires 0.1 ms after impact (1 cm down) it puts 56 through, at 0.5 ms
     (6 cm) 14, at 1 ms 4 (ref §3).
   - **So "a heavy HE burst 25–50 m short still puts tens of holes in her side" (13 §5.2) holds only for a very quick
     fuze.** With a WWII point-detonating fuze the side is holed from within about 15 m, and beyond that the spray hits
     the upper works and the people in the open.
   - **How fast the fuze fires on water** becomes a shell number (`fuze.water_ms`). Semenov wrote that the Japanese
     shells at Tsushima "exploded the moment they touched its surface" [SRC]. That is the 0.1 ms end.
3. **The lean makes shorts dangerous, overs harmless, and aspect matter** (13 §2.5's lean, applied to the sea).
   - The side band leans forward by the shell's speed: about 43° for a 46 cm HE striking at 530 m/s. The lean turns it
     into a cone about 47° around the line of flight. Its top edge rises at about 30° above the horizon.
   - **A short** sprays the side close in, and the upper works, bridge and directors farther out.
   - **An over** throws everything away from the ship: zero perforations at every distance.
   - **A miss beside a bow-on target** sprays along her side at grazing angles: 4 perforations from 10 m, against 54
     for a short on a broadside target (ref §5).
4. **What gets through** (13's field against a Fletcher-like destroyer: 8 mm side, 6.4 mm deckhouses, 4.8 mm
   funnels; ref §4):
   - **Medium HE (5″–8″) barely gets through the side.** A 20 cm common shell short by 3–7 m puts 5–8 fragments
     through, and a 5″/38 one. What medium HE hurts is **people in the open and thin fittings**: about 2–3 exposed
     men from a burst 7–15 m short. That is the record: *Exeter*'s torpedo crews, *Achilles*' director, *Sheffield*'s
     three dead.
   - **Heavy HE (14″–18″) is a different weapon.**
     - A 46 cm Type 0 short by 7 m puts about **130 fragments through a Fletcher's side**. Short by 15 m it puts 21
       through the side, 26 through the deckhouses and 13 through the funnels.
     - Within 1–3 m it **holes the waterline with 0.4–1.5 m²** of ragged holes, much as a contact burst would.
5. **Shock is indexed by a near-miss shock factor**:

   `NSF = √W_lb / R_ft · (1 + sin a)/2 · √(1 − e^(−2Δt/θ))`

   - **W** is the TNT-equivalent charge and **R** the slant distance to the nearest wetted hull.
   - **a** is the angle up from the burst to the hull: 0 alongside, 90° under the keel.
   - **Δt** is the time until the surface reflection cuts the pulse off, and **θ** is Cole's decay constant.

   It is banded against **ten British bomb near misses of 1939–41**, from "an asdic and some electrical gear" at 0.05
   to "plating split" at 1.0 (§3.2). Shell charges are small (3–70 kg, against a 250 kg bomb's 130 kg), so:
   - **A surface burst barely shocks a hull.** The surface reflection cuts its pulse within microseconds. A 46 cm HE 3 m
     off a carrier's side gives 0.17: castings and seams, no structure. Its structural shock (≥ 0.2) needs it within
     about 2.7 m, the same burst that holes the side with fragments.
   - **Gunfire's structural shock comes from the AP shell bursting under the hull.** *White Plains* took a 46 cm shell
     under the turn of the bilge. It "severely damaged her hull", damaged her starboard machinery and tripped most
     breakers, and power was back in three minutes [SRC]. A burst 5–8 m under her bilge gives NSF 0.33–0.53 (§3.3).
6. **How much near misses add.** Per salvo on a broadside Fletcher at 15 km, with a fire-control error of σ 150 m in
   range and 40 m in deflection (ref §11; ~0.7 hits a salvo for the heavy guns):

   | Salvo | Hits | Perforations: hull / upper works / funnels | Waterline holes | Exposed crew hit | Shock 0.08–0.2 / ≥0.2 | Underwater hits |
   |---|---|---|---|---|---|---|
   | Yamato, 9 × 46 cm HE | 0.71 | 39 / 9 / 4 | 830 cm² | 3.3 | 0.19 / 0.27 | — |
   | Yamato, 9 × 46 cm AP | 0.71 | — | — | — | 0.65 / 0.39 | 0.24 |
   | Iowa, 9 × 16″ AP | 0.70 | — | — | — | 0.83 / 0.08 | 0.23 |
   | Iowa, 9 × 16″ HC | 0.70 | 33 / 7 / 3 | 410 cm² | 2.7 | 0.19 / 0.28 | — |
   | IJN cruiser, 10 × 20 cm common | 0.52 | 2 / 0 / 0 | 15 cm² | 1.1 | 0.16 / 0.20 | — |
   | Destroyer, 5 × 5″/38 AAC | 0.37 | — | — | 0.3 | 0.04 / 0.08 | — |

   HE's "shock ≥ 0.2" is the burst within 1–3 m of the side, the same burst that holes her waterline. It is one
   close-alongside event, not two.
7. **So, does it matter as much as the hits?** For heavy HE against destroyers, yes, in a different currency.
   - **Per hit,** the near misses around it add about **70 perforations, 4–5 exposed men and 0.1 m² of waterline
     holes.**
     - That strips the topsides: the 40 mm and torpedo crews, the bridge, radar, aerials, cables.
     - It loads damage control with holes that flood once the side is awash or she heels or settles.
   - **It rarely stops her.** Machinery takes a near miss only from the burst close alongside, which is nearly a side
     hit.
   - **Medium-HE near misses** thin the exposed crews (about 2 men per 20 cm hit, 1 per 5″ hit) and little else.
   - **AP near misses** make no splinters. They spring a hull from underneath about **0.5 times per hit** for the
     46 cm (a *White Plains* every few hits). For the 16″ it is about 0.1.
   - 08's guess that near misses "could matter as much as direct hits" holds for crew and sensor attrition, not for
     crippling.
8. **The model** (§6):
   - **A new `Hit` source:** a burst point outside the hull, from the splash the shell system already makes.
   - **Splinters:** 13's fragment field per part, with the sea as a floor and the burst's depth as a lid. The lean is
     done analytically (a Galilean transform, checked against rays). THOR is applied per face, and holes go to the
     flooding.
   - **Shock:** NSF rolls on hull and machinery.
   - **Broadphase radius** about `max(15·W^⅓, 7.5·√W)` m.
   - **Cost:** a few µs a near miss.

---

## 1. What happens when a shell misses close

### 1.1 The branch at the water

| The shell | What it does at the sea | Effect on a ship close by | Here |
|---|---|---|---|
| Nose fuze (HE, HC, nose-fuzed common) | Fires on impact, 0.1–2 ms after the nose meets the water [EST] | Splinters above the water; weak shock | §2 |
| Base fuze with delay, `FuzeStartsOnWater` on | Runs under water `x_f = L·ln(1 + v·τ/L)` (P6's `WaterRun.FuzeRunM`), then bursts | Shock only (the sea stops its fragments) | §3 |
| The same, reaching the hull within `x_f` | Underwater hit | P6, unchanged | §4 |
| A base fuze that doesn't start (blind, or the switch off) | Sinks | Nothing | — |
| Any shell falling flatter than ~6–7° | May **ricochet**. The limit for a steel sphere is 18°/√SG = 6.4° (Johnson & Reid) [SRC] | A nose fuze fires on the graze; an AP shell skips on | §6.2 |

The ricochet row matters only at short range: a 16″ falls at 5° at 10 kyd (`dispersion-and-hits.md` §2.2). Night
actions are where it shows.

### 1.2 Where it bursts

- **Nose fuze:** at depth `z_b = v · t_f · sin(fall)`. At 480 m/s and 15° that is 1.2 cm at 0.1 ms, 6 cm at 0.5 ms and
  25 cm at 2 ms (ref §3).
- **Base fuze:** P6's run (L = 25 calibres pointed, 50 diving), straight at the angle of fall, as the sim keeps it
  (ref §9, a Fletcher with her sides at ±6 m):

| Shell (15 km) | Fuze run | Short by 2–10 m | Short by 20 m | Short by 40 m | Short by 60 m |
|---|---|---|---|---|---|
| 16″ AP Mk 8, 0.033 s | 11 m | Underwater hit, 0.5–2.5 m deep | Bursts 2.7 m deep, 9 m off the side: NSF 0.10 | 29 m off: 0.03 | Nothing |
| 46 cm Type 91 AP, 0.4 s **[UNCERTAIN delay]** | 55 m | Underwater hit | Passes under her, bursts 14 m down 21 m beyond the far side: 0.08 | 14 m down, under her far bilge: 0.26 | 14 m down, 7 m off the near side: 0.19 |

**The Type 91's under-keel bursts come from the straight path.** Its straight 55 m run takes it 14 m down, under or
beside a destroyer, where it bursts as a small mine. The real shell's underwater path flattened. The straight line
errs deep, and so toward under-keel bursts and away from hull hits. That is a P6 question (§4, §7).

### 1.3 The record

| Case | Shell | What the near miss did | Source |
|---|---|---|---|
| Tsushima 1905, Russian ships | Japanese 12″ HE (Shimose, Ijuin fuze) | Shells "exploded the moment they touched its surface". Splinters "jingled against the side and superstructure"; "the steel plates and superstructure on the upper deck were torn to pieces, and the splinters caused many casualties" | Semenov [SRC] |
| *Exeter*, River Plate 1939 | 28 cm HE | Burst "just short… starboard side amidships". Splinters "killed the crew of the starboard torpedo tubes", "cut gunnery communications and riddled the funnels and searchlights", and killed two of a party inside the ship | navyhistory.au [SRC] |
| *Achilles*, River Plate | 28 cm | A near miss sent splinters through the director control tower, killing four ratings; "Six heavy splinters had entered the D.C.T." | NZ History [SRC] |
| *Sheffield*, 26 May 1941 | 38 cm | Straddled: "shell splinters fell on Sheffield killing three men and wounding two others" | Wikipedia [SRC] |
| *Achates*, Barents Sea 1942 | 20.3 cm | A near miss early in the action "started her flooding"; later hit, capsized | [SRC, secondary] |
| *Saumarez*, North Cape 1943 | 28 cm / 15 cm | A near miss "damaged the forced lubrication system"; she reached Murmansk on one engine | Wikipedia [SRC] |
| *Heermann*, Samar 1944 | 8″–18″, mostly AP early | Straddled for an hour; her damage was "only fragments", apart from the 8″ hits forward | NHHC via 08 §8 [SRC] |
| *White Plains*, Samar | 46 cm (Yamato's third salvo, 07:04) | "Exploded beneath the turn of White Plains port bilge near frame 142". "The mining effect of the under-keel explosion severely damaged her hull", damaged her starboard machinery and "tripped most of the circuit breakers". Power was back in 3 min, and she kept station by overspeeding her port engine. Her black smoke made the Japanese think they had hit her, and they shifted fire | Wikipedia [SRC] |
| WWII bombs (WDR 51, via 13 §2.3) | Bombs bursting below the surface | Fragments "quickly lost velocity and were not a source of serious damage" | [SRC] |

**The record in short:**
- HE near misses kill people in the open and riddle thin things.
- AP near misses under the hull spring plating and machinery.
- Underwater bursts throw no useful fragments.
- A near miss that flooded a destroyer (*Achates*) is as likely to be splinter holes at the waterline as shock. The
  model gives both (§2.4, §3).

---

## 2. Splinters

### 2.1 The field is 13's

`near_miss_ref.py` imports `fragmentation_ref.py`. It uses `burst()`, the Mott bins (fragments of 1 g and up), the drag
law (L₁ = 247 m/kg^⅓ in air) and THOR. It also uses 13's spray groups:
- the **side band**, ±7.5° square to the path [SRC WDR 51 via 13];
- the **nose spray**, the ogive's 20% between 30° and 80° off the nose (HE) [EST in 13];
- the **tail spray** (140–180°) [EST].

The groups as the near miss sees them (ref §1):

| Shell | Side v₀ | Nose v₀ | Side ≥1 g | Nose ≥1 g | Mean fragment | W (TNT-eq) |
|---|---|---|---|---|---|---|
| 5″/38 AAC | 957 m/s | 883 | 553 | 175 | 16 g | 3.1 kg |
| 6″ HC | 934 | 862 | 657 | 209 | 25 g | 5.7 |
| IJN 20 cm common | 689 | 633 | 897 | 285 | 83 g | 8.2 |
| 28 cm HE (Graf Spee) **[UNCERTAIN filler]** | 753 | 693 | 1,159 | 368 | 150 g | 23.3 |
| 16″ HC Mk 13 | 741 | 682 | 1,208 | 383 | 413 g | 66.2 |
| 46 cm Type 0 HE | 572 | 526 | 1,048 | 333 | 780 g | 61.7 |
| 16″ AP Mk 8 | 370 | — | 312 | — | 2.1 kg | 17.6 |
| 46 cm Type 91 AP | 476 | — | 474 | — | 1.7 kg | 33.9 |

### 2.2 The reconciliation with 13 §5.2

Under 13's assumptions this model gives 13's numbers. Those assumptions are: a burst exactly at the surface, the shell
at its full striking speed, and a flat 8 mm side 7 m high and 110 m long. Holes through 8 mm (ref §2), with 13's T8
values after each:

| Shell (v, fall) | Beam 10 m | 25 m | 50 m | Short 10 m | 25 m | 50 m |
|---|---|---|---|---|---|---|
| 5″/38 AAC (300, 20°) | 11 (10) | 4 (4) | 1 (1) | 12 (13) | 4 (5) | 1 (1) |
| 6″ HC (400, 15°) | 19 (18) | 7 (7) | 2 (2) | 27 (28) | 10 (12) | 3 (3) |
| IJN 20 cm common (450, 15°) | 20 (18) | 8 (7) | 3 (2) | 65 (65) | 24 (25) | 7 (8) |
| 46 cm Type 0 (480, 15°) | 59 (54) | 24 (21) | 11 (10) | 302 (301) | 97 (94) | 39 (39) |
| 46 cm Type 0 (480, 35°) | 23 (24) | 15 (14) | 1 (0) | 211 (205) | 169 (172) | 50 (53) |

The two codes agree within the ray sampling. Everything below that differs from 13 differs because of what this note
adds.

### 2.3 The water: depth and the floor

Two things the sea does:

- **Down is gone** (13 §2.3). In water `L₁ ≈ 0.3 m/kg^⅓`: a fragment's speed falls 1/e every 3.0 cm at 1 g, 6.4 cm at
  10 g, 14 cm at 100 g and 30 cm at 1 kg.
  - So there are no splinter holes below the waterline unless the burst is within about a metre of the side.
  - The bomb report's "splinter holes above and below waterline" (*Firedrake*, a bomb 20 ft off) fits only very close
    bursts.
- **A burst a few cm down is choked.** Fragments going up must cross `z_b / sin(elevation)` of water first. The
  fast, low, plate-perforating ones lose most. The 46 cm Type 0 short of 13's flat side (ref §3):

| Fuze function time | Burst depth | Short 10 m | 25 m | 50 m |
|---|---|---|---|---|
| 0 (13's assumption) | 0 | 302 | 97 | 39 |
| 0.1 ms | 1.2 cm | 230 | 56 | 15 |
| **0.5 ms (default)** | 6.2 cm | 114 | 14 | 2 |
| 1 ms | 12 cm | 59 | 4 | 0 |
| 2 ms | 25 cm | 19 | 0 | 0 |
| 5 ms | 62 cm | 1 | 0 | 0 |

The default of 0.5 ms is a judgement for a WWII point-detonating fuze **[EST]**:
- **0.1 ms** fits the 1904–05 Japanese fuzes (Semenov) and the most sensitive superquick fuzes.
- **2–5 ms** fits a graze-insensitive or short-delay nose fuze, whose near misses throw almost nothing.

This is the most sensitive single number in the near-miss model (§7).

### 2.4 The ship above the water

The ship is a Fletcher-like destroyer, 114 × 12 m, freeboard 5 m [EST]:
- an 8 mm side with a waterline band 0–1 m above the waterline;
- 6.4 mm deckhouses forward (to 12 m) and aft (to 8 m);
- 4.8 mm funnels;
- open crew stations along both deck edges (about 100 men in the open at general quarters, 0.28 per m² of the deck-edge
  band [EST]), and the open bridge.

She is broadside, with fire from abeam, and the results are averaged along her length. A man is hit by a fragment of
80 J or more over his 0.5 m² [EST] (14 uses 79 J).

Ref §4, t_f 0.5 ms. Each cell gives perforations of hull / deckhouses / funnels, then the waterline holes, then the
exposed men hit:

| Shell | Distance | Short | Over | Off the bow |
|---|---|---|---|---|
| 46 cm Type 0 HE | 1 m | 133 / 0 / 0 · **1.47 m²** · 0 | 0 | 47 · 300 cm² |
| | 3 m | 133 / 0 / 0 · 0.38 m² · 0 | 0 | 18 · 190 cm² |
| | 7 m | 130 / 0 / 0 · 550 cm² · 0.3 | 0 | 0 |
| | 15 m | 21 / 26 / 13 · 33 cm² · 4.5 | 0 | 0 |
| | 30 m | 3 / 8 / 3 · 3.4 | 0 | 0 |
| | 60 m | 0 / 1 / 1 · 0.8 | 0 | 0 |
| 16″ HC Mk 13 | 3 m | 129 / 0 / 0 · 0.16 m² · 0.9 | 0 | 24 |
| | 7 m | 67 / 22 / 8 · 210 cm² · 6.3 | 0 | 1 |
| | 15 m | 12 / 16 / 6 · 5.4 | 0 | 0 |
| | 30 m | 2 / 4 / 2 · 2.2 | 0 | 0 |
| IJN 20 cm common | 3 m | 8 / 0 / 0 · 28 cm² · 0.3 | 0 | 1 |
| | 7 m | 5 / 1 / 1 · 3.3 | 0 | 0 |
| | 15 m | 0 / 1 / 1 · 2.2 | 0 | 0 |
| 5″/38 AAC | 3 m | 1 · 0 | 0.1 | 0 |
| | 7 m | 0 · 2.3 | 0.1 | 0 |
| | 15 m | 0 · 0.7 | 0 | 0 |

**The patterns:**
- **Distance moves the damage upward.** Close in, the leaning band strikes the side. Farther out it climbs to the
  deckhouses, the bridge and the funnels, then passes over. That is how a short burst 20–30 m off reaches a cruiser's
  director 20 m up.
- **Overs are harmless** to splinters at every distance. The band leans away from the ship, and only the slow tail
  spray comes back.
- **Aspect matters** (ref §5, 46 cm HE at 10 m):
  - a short on a broadside target: 54 perforations, 6.6 men;
  - a miss beside a bow-on target, fire from ahead: 4 perforations, 1.2 men;
  - an over: nothing.

  On a bow-on target the band runs along her side at grazing angles, where THOR's sec^1.29 stops most of it.
- **Medium HE against the side:** 13's flat side took 65 holes from a 20 cm common short by 10 m, with the burst at
  the surface. With the burst 6 cm down and a 5 m freeboard, 5 get through at 7 m and none at 15 m. The band clears a low side and hits the
  men above it.

### 2.5 What it means for the classes

- **Destroyers, torpedo boats, escorts** (6–10 mm sides, open mounts): heavy HE near misses perforate. Medium HE near
  misses hit the open crews. This is where the near miss earns its place.
- **Cruisers** (10–13 mm sides, more shielding): medium-HE near misses reach the upper works and the people in them
  (*Exeter*, *Achilles*). Heavy HE reaches the side close in.
- **Battleships:** the side is armour, and the upper works are 10–25 mm. Near misses do what medium hits do, more
  thinly: AA crews, radar, aerials, searchlights. 13 §5.4's splinter plating is the designer's answer.
- **Wooden and aluminium craft:** the THOR constants change with the material's `fragments` class. Splinters go
  through everything.

### 2.6 The River Plate check

A 28 cm HE short of a Leander-like cruiser, abreast her DCT (21–24 m up, 6.4 mm), ref §10:

| Short by | Hull | Upper works | DCT | Exposed men |
|---|---|---|---|---|
| 3 m | 9 | 0 | 0 | 0.1 |
| 10 m | 6 | 6 | 0 | 2.6 |
| 20 m | 0 | 10 | 0.8 | 1.1 |
| 30 m | 0 | 1 | 0.2 | 0.5 |

**Exeter's torpedo crews** fit the 10 m row: a few men at the deck edge. **Achilles' "six heavy splinters" in the DCT**
is about 8× the model's best case (0.8 at 20 m). Possible reasons:
- the burst was closer and better aligned than the few cases tried;
- the fuze was quicker than 0.5 ms;
- or the band is wider than ±7.5°.

The last fights 13's sourced band. **A bench should try the fuze time and the burst geometry against this case first.**

---

## 3. Shock

### 3.1 The physics

**The pulse.** An underwater burst sends a shock with an exponential tail (Cole's similitude [SRC]):
- **Peak pressure:** `P_m = 21,600·(W^⅓/R)^1.13` psi.
- **Decay constant:** `θ = 6.0×10⁻⁵·W^⅓·(R/W^⅓)^0.18` s (lb, ft).

**The index.** What damages a hull is the energy the pulse carries. The standard index is the **shock factor**, `√W/R`
(lb, ft), which follows from the energy flux, about W/R². The Wikipedia "Shock factor" table [SRC] gives 0.1–0.15 for
lights and pipes, 0.2 for general machinery damage, and ≥ 0.5 as usually lethal.

**Two corrections make it a near-miss index:**
- **The angle,** `(1 + sin a)/2`, the keel-shock form. A burst alongside loads the side at grazing incidence; one under
  the keel loads the bottom square on.
- **The surface cut-off.** The surface reflects the shock as tension. At the hull it arrives `Δt = (R_image − R)/c`
  after the direct wave and cancels the tail. The energy delivered goes as `1 − e^(−2Δt/θ)`.
  - **A surface burst is cut off almost at once.** A 46 cm HE 7 cm deep, 3 m off the side, keeps 29% of the factor
    (with the nearest wetted point taken 0.5 m down).
  - **A burst 2–3 m down keeps it all.**

### 3.2 The bands, from the bomb record

**The data.** The US summary of British war damage to September 1941 lists near misses with distances. Charges are
the bombs' usual fillings, TNT-equivalent: SC 50 25 kg, SC 250 130 kg, SC 500 220 kg [SRC typical fillings; which bomb
it was is the report's estimate]. The bursts are taken alongside at about 2 m (direct-action fuzes) (ref §6):

| Ship | Bomb, distance | NSF | Damage reported |
|---|---|---|---|
| *Fame* (DD) | 200 lb inst., 5 ft | 1.00 | Severe structural and splinter damage, flooding aft |
| *Kelvin* (DD) | 250 kg, 10 ft | 0.85 | Outer bottom badly buckled, port shaft distorted, turbine foot broke |
| *Napier* (DD) | 250 kg, 10 ft | 0.85 | Turbine feet fractured, fuel pumps broken, 24 kn |
| *Nizam* (DD) | 250 kg, 15 ft | 0.56 | Thrust block out of line, bulkheads buckled, turbine feet; 26 kn |
| *Somali* (DD) | 1,000 lb, 30 ft | 0.34 | Plating fractured, hull heavily indented, flooding; 11.5 kn for 1,000 miles |
| *Montrose* (DD) | 500 lb, 30 ft | 0.25 | Hull corrugated below the waterline, castings cracked, steering and radio out |
| *Quorn* (DD) | ~250 kg, 60 ft | 0.11 | Four turbine feet broke |
| *Furious* (CV) | 250 lb, 45 ft | 0.11 | Turbine blades cracked, a shaft out |
| *Sheffield* (CL) | 50–250 lb, 75 ft | 0.05 | An asdic, some machinery and electrical equipment |
| *Winchester* (DD) | ?, 50–100 yd | ~0.02 | "Severe machinery damage from cast iron failure" |

**The bands** [FIT, to the table; the boundaries are judgements between cases]:

| NSF | What happens (a destroyer's 8–10 mm bottom, 1940 fittings) |
|---|---|
| < 0.03 | Nothing (a shudder) |
| 0.03–0.08 | Fragile gear: lights, breakers trip, asdic, radar, gyro; cast-iron fittings may crack |
| 0.08–0.20 | Machinery mounts and castings: turbine and dynamo feet, blades, pipes; seams weep |
| 0.20–0.45 | Hull indented or corrugated, seams open, slow flooding; shafts strained |
| 0.45–1.0 | Plating and frames badly buckled, moderate flooding, shafts and thrust blocks out of line |
| ≥ 1.0 | Plating split or holed alongside: a hole, flooding as from a hit |

**The order is the record's:** equipment first, then machinery mounts, then the hull. **Winchester** is the outlier
that says the equipment thresholds depend on the fittings. The 1940 report blames cast iron again and again, and both
navies shock-hardened from 1941–42. So the equipment bands scale with a design's **shock grade** (§6.5).

### 3.3 White Plains

A 46 cm shell under the turn of a Casablanca-class bilge (ref §7). Yamato was firing AP, and a Type 91 short by tens
of metres runs on under water (§1.2).

| Burst | NSF | Band |
|---|---|---|
| 3 m under the bilge (33.85 kg filler) | 0.88 | Badly buckled, moderate flooding, shafts out of line |
| 5 m under | 0.53 | The same, at its lower edge |
| 8 m under | 0.33 | Indented, seams open, machinery strained |
| 5 m under, with 08's 23.9 kg | 0.44 | Indented |
| For contrast: a Type 0 HE at the surface, 3 m off the side | 0.17 | Castings and seams |

The record says the burst "severely damaged her hull… damaged her starboard machinery… tripped most of the circuit
breakers", and she stayed in formation. That sits at 0.3–0.6, a burst 5–8 m under her.

### 3.4 How far a shell's shock reaches

Horizontal distance from a destroyer's side for each band (ref §8). Nose fuzes burst at their function depth, base
fuzes at 3 m:

| Shell | Burst depth | NSF 0.03 | 0.08 | 0.2 | 0.45 |
|---|---|---|---|---|---|
| 5″/38 AAC | 6 cm | 4.3 m | 2.2 m | 1.2 m | 0.5 m |
| IJN 20 cm common | 8 cm | 6.0 | 3.2 | 1.7 | 0.9 |
| 16″ HC / 46 cm HE | 7 cm | 9.2 | 4.9 | 2.7 | 1.6 |
| 16″ AP | 3 m | 29.8 | 11.9 | 4.7 | 2.1 |
| 46 cm Type 91 AP | 3 m | 37.8 | 16.2 | 6.6 | 2.9 |

**What the reaches mean:**
- **HE's structural shock needs the burst within 1–3 m,** the same burst whose fragments hole the waterline, so the
  two are **one event**.
- **AP reaches 5–7 m for the hull and 12–16 m for the machinery's castings.**
- **Bombs are a different scale:** a 250 kg bomb reaches 0.2 at about 13 m. That is why the war-damage literature's
  near misses are mostly bombs.

**Bigger ships.** Hull damage needs impulse in proportion to plate thickness. So the structural bands (0.20 and up)
scale with `t_bottom / 9 mm` **[EST]**:
- A battleship's 16–25 mm bottom, behind bulges and a torpedo system, needs two to three times the factor.
- Gunfire near misses on battleships are then equipment shocks only.
- The bomb record agrees: *Valiant*'s blister was holed by a 250 kg bomb close aboard, and *Warspite*'s bulges were
  crushed by a 1,000 lb bomb.

---

## 4. Short shells that reach the hull

This is P6 as it stands (`WaterRun.cs`):
- the speed decays over the run, `e^(−x/L)`;
- the hit carries `Hit.WaterRunM`;
- `FuzeStartsOnWater`, once P3 turns it on, cuts the reach at `x_f`.

The near miss adds only the other branch: **when `x_f` ends short of the hull, the shell bursts there**, and §3 takes
it. For a Fletcher (ref §9), a 16″ AP short by up to about 10 m is an underwater hit. Short by 10–30 m it is a shock
near miss, NSF 0.03–0.10. Beyond that it is nothing.

**One question for P6: the straight underwater path.** A Type 91 run 55 m straight goes 14 m down, under or beside
any destroyer, and bursts there: NSF 0.19–0.26 for shells 40–60 m short. That is where most of the 46 cm AP salvo's
structural shock in §5 comes from. The real flattening path would keep it shallower: more hull hits, fewer under-keel
bursts. Until P6 revisits the path, the AP shock numbers for diving shells are an upper bound.

---

## 5. How much it matters: the salvo

**Ref §11.** Salvos at a broadside Fletcher, dispersion from the fire-control research's patterns:
- σ_range is about pattern ÷ 5 for nine guns (`dispersion-and-hits.md` §1.1), and σ_deflection 0.4× that.
- Two cases each: a perfect straddle (MPI on the target), and a **fire-control error** of σ 150 m in range and 40 m in
  deflection per salvo.
- A shell "hits" if it meets the side, deck or deckhouses (the danger space). Otherwise it is a short, an over or a
  miss past the ends, and §2.4 and §3 apply.

| Salvo | Fire control | Hits | Misses within 10 / 30 m | Perforations (hull / upper / funnels) | Waterline holes | Exposed men hit | Shock 0.08–0.2 / ≥0.2 | Underwater hits |
|---|---|---|---|---|---|---|---|---|
| 9 × 46 cm HE | Straddle | 3.15 | 0.95 / 3.05 | 143 / 37 / 16 | 3,940 cm² | 10.9 | 0.22 / 0.30 | — |
| | FC error | 0.71 | 0.79 / 1.95 | 39 / 9 / 4 | 830 cm² | 3.3 | 0.19 / 0.27 | — |
| 9 × 46 cm AP | Straddle | 3.15 | 0.95 / 3.05 | — | — | — | 1.36 / 0.71 | 1.17 |
| | FC error | 0.71 | 0.79 / 1.95 | — | — | — | 0.65 / 0.39 | 0.24 |
| 9 × 16″ AP | FC error | 0.70 | 0.83 / 2.04 | — | — | — | 0.83 / 0.08 | 0.23 |
| 9 × 16″ HC | Straddle | 2.25 | 0.70 / 2.19 | 71 / 23 / 10 | 1,340 cm² | 8.2 | 0.16 / 0.22 | — |
| | FC error | 0.70 | 0.83 / 2.04 | 33 / 7 / 3 | 410 cm² | 2.7 | 0.19 / 0.28 | — |
| 10 × 20 cm common | FC error | 0.52 | 0.92 / 2.44 | 2 / 0 / 0 | 15 cm² | 1.1 | 0.16 / 0.20 | — |
| 5 × 5″/38 AAC | FC error | 0.37 | 0.43 / 1.13 | — | — | 0.3 | 0.04 / 0.08 | — |

**Reading it per hit** (FC-error rows):

- **Heavy HE:** each hit comes with about **70 perforations (55 through the side), 4–5 exposed men and 0.1 m² of
  waterline holes** from the near misses around it.
  - A hit itself wrecks a 6 m sphere inside: 08's 30% crippled from one 46 cm HE hit.
  - The near misses add the topside attrition that makes a destroyer stop fighting: guns' crews, bridge, radar. They
    also add a flooding load at the waterline that grows as she settles or heels.
  - **For "the 18-inch broadside into a destroyer", near misses are a second weapon about the hit's size in crew and
    sensors, and a small one in buoyancy and speed.**
- **Medium HE:** near misses hit only the exposed crew, about **2 men per 20 cm hit and 1 per 5″ hit**, plus thin
  fittings. Real (*Exeter*, *Sheffield*), but secondary.
- **AP:** no splinters. What matters is the underwater hits (P6) and the bursts under the hull.
  - **46 cm Type 91:** 0.39 structural and 0.65 equipment-shock events a salvo, about one *White Plains* per two hits,
    with §4's caveat on the path.
  - **16″:** 0.08 and 0.83.
- **Close is what matters, not on.** A straddle raises the near misses within 10 m only slightly (0.95 against 0.79
  for 46 cm) and quadruples the hits. Near misses are the steady background of shooting that is close.

**The ammunition choice** (decision 6, the admiral's loadout) gains a real trade:
- **HE against small ships** hurts with its misses too.
- **AP** passes through (08) and shocks only from underneath.

That is exactly why the Japanese switched to HE at Samar.

---

## 6. Proposed game model

### 6.1 Where it enters

- **ShellSystem.** A miss already has its water point, time, shell and salvo (the splash). On a miss, the broadphase
  asks whether any ship's waterplane lies within `R_near` of it:

  `R_near = max(15·W^⅓, 7.5·√W)` m (W TNT-eq kg)

  - **Splinters:** 15·W^⅓ is where exposed-crew hits fall below about 0.3 a burst (ref §4: 60 m for 46 cm HE, 30 m
    for 20 cm, 22 m for 5″).
  - **Shock:** 7.5·√W is where an underwater burst's NSF falls below 0.03 (ref §8: 30 m for 16″ AP, 38–44 m for the
    Type 91).

  Only ships within it get a near miss.
- **The burst point** (§1.2):
  - nose fuze: at the water point, `z_b = v·t_f·sin(fall)` down;
  - base fuze: P6's run; if the run reaches the hull, it is P6's underwater hit, as now;
  - otherwise the point where `x_f` ends.
- **DamageSystem** takes a `Hit` with `Source = NearMiss`, the burst point in the ship's frame, the shell's velocity at
  the burst and the filler. **There is no terminal walk.** `Effects` starts from the burst.

### 6.2 The shell block (S1's templates)

Two fuze fields, beside `delay_s` and `blind`:

| Field | Default | Meaning |
|---|---|---|
| `fuze.water_ms` | 0.5 for nose fuzes | Function time on hitting the water: the surface burst's depth (§2.3). 0.1 for very sensitive fuzes (Ijuin, Shimose era), 2–5 for graze-insensitive or short-delay nose fuzes |
| `fuze.water_fires` | 0.95 nose, 1.0 base (with `FuzeStartsOnWater`) | Chance the fuze functions on water at all [EST] |

**Water ricochet:** below a fall of `18°/√(ρ_shell/ρ_water)` ≈ 6.4° (spheres [SRC]; shells similar [EST]), a shell
may skip. v0 treats it as "no burst" for base fuzes and "surface burst" for nose fuzes. Carrying the shell on is later.

### 6.3 Splinters, per part, without rays

The fragment table is 13 §5.1's, compiled per shell. For a burst outside the hull, `Effects` does the following for
each **part within R_near** (from the hit query's part boxes, in a fixed order):

1. **Direction and distance.** Take the part's outward faces seen from the burst. For each, find the direction ŝ to
   its centre, its distance R, its solid angle `ΔΩ = A·cos/R²` and the obliquity. A face behind the first plate along
   ŝ is shaded: the side shades the spaces behind it.
2. **The sea:** skip ŝ below the horizon. Otherwise every class's speed is cut by `e^(−z_b/(sin e · L₁w·m^⅓))`.
3. **The leaning spray, analytically, per group** (side band, nose spray, tail). With the shell's velocity V along
   axis û and the group's v₀:

   `λ = V(û·ŝ) + √(v₀² − V²(1 − (û·ŝ)²))`, `n = (λŝ − Vû)/v₀`, `ψ = acos(n·û)`

   `f_lab(ŝ) = [group count / band solid angle if ψ is in the group's band] · λ² / (v₀·(λ − V·û·ŝ))`

   λ is the fragments' lab speed. Checked against 60,000 rays a group, it agrees within 5–13% on the side band and the
   nose spray (ref §12).
4. **Count.** Expected perforations: `Σ_classes N_class · f_lab · ΔΩ · [v_class(R) > V₅₀(t, m, ob)]`. Exposed parts
   (no plate) count the classes with ½mv² ≥ 80 J.
5. **Draw** Poisson counts from the sim's RNG. Each perforating fragment is a fragment strike inside that part, with
   its residual speed. That is what an internal burst's fragments do, so crew, systems, cables (13 §5.3's line rule)
   and fires follow E3's rules.
6. **Waterline holes.** Perforations in the side's waterline band (0 to 1 m above the waterline, and the metre below
   for bursts within 1 m) become flooding `Hole`s at their height. Each is about 1.3× the fragment's presented area,
   3–60 cm² [EST]. The flooding and damage control take them as any hole (09: plug and wedge). They flood when the band
   is under water: waves, heel, trim, settling.

**Cost:** about 30 parts × 3 groups × (one direction + 6 classes) per near miss, a few µs, nothing allocated. Most
misses are outside every ship's `R_near` and cost one broadphase test.

### 6.4 Shock rolls

Per near miss with an underwater or surface burst:

1. **NSF** at the nearest wetted point of the hull, with the angle and the cut-off (§3.1). One number per ship, plus
   one per machinery space: its own R to the burst.
2. **Structure** (bands 0.20 and up, × `t_bottom/9 mm`):
   - 0.2–0.45: seams sprung on the nearest compartment's side, a leak area of 5–50 cm² [EST, as 09's sprung links];
   - 0.45–1.0: plating buckled, 50–500 cm², and the nearest transverse bulkhead's seams sprung;
   - ≥ 1.0: a hole of the contact rule's size (11 §3.1). For a surface burst this is already the fragments' hole: take
     the larger, not both.
3. **Machinery and gear** in each space, by its own NSF and the design's **shock grade** (§6.5):
   - 0.03–0.08: lighting and breakers trip, a 1–5 min outage (as 08 §3.7's electrical trip; *White Plains* 3 min); the
     fragile sensors (radar, sonar, gyro) roll a failure;
   - 0.08–0.2: castings and mounts roll a failure per item (turbine feet, auxiliaries, pipes);
   - above 0.2: the shafts and thrust blocks of that space also roll misalignment, a speed cap until docked (*Napier*
     24 kn, *Nizam* 26).
4. **Stern bursts.** A burst within about 0.15 L of the stern with NSF ≥ 0.2 at the rudder or shafts rolls a jammed
   rudder or bent shaft (shipgen research 01's rudder note; *Ilex*, *Fortune*).

### 6.5 What the designer and the classes need

- **Side plating thickness by zone**, with a **waterline band**.
- **Exposed stations.** Parts flagged `exposed` (open mounts, AA tubs, torpedo mounts, bridge wings, searchlight and
  lookout platforms), with their crew at general quarters.
- **Soft gear:** radar arrays, aerials, rangefinder optics, searchlights, boats and funnels (draught), as exposed parts
  with a small toughness. Any fragment of 80 J or more that strikes one damages it.
- **Shock grade** (a design number, like `machinery.tech`):
  - 1.0 for cast-iron-era fittings, the 1940 record the bands are fitted to;
  - about 2.0 for shock-hardened fittings (cast steel, resilient mounts, 1941–42 on) **[EST]**.

  It multiplies the equipment bands' thresholds, not the structural ones.
- **Bottom and side thickness below the waterline,** for the structural bands' scaling. Bulges and torpedo systems
  count as extra standoff.

### 6.6 The combat log and the screen

- **Log lines,** as 08 §7.3, tagged with 13 §6's source groups:
  - "46 cm HE · near miss 15 m short, starboard · 40 splinters through the side and forward deckhouse · No. 2 40 mm
    crew hit, SG radar out"
  - "46 cm AP · burst under the keel · hull sprung, starboard engine-room breakers tripped (2 min)"
- **Visuals** (`visuals/shell-impacts.md`): the column over the ship as now. NSF ≥ 0.03 plays the hull shudder, and
  perforations leave splinter decals.

### 6.7 Tests (wide bands)

1. A 5″ HE near miss at 7 m puts at most one fragment through a destroyer's 8 mm side, and hits a few exposed men.
2. A 46 cm HE short by 7–15 m puts tens to a hundred-odd fragments through a destroyer's side and upper works. An over
   at the same distance puts none.
3. With `water_ms` 0, near misses reproduce 13 §5.2's table within sampling.
4. A nose-fuzed burst within 1–3 m holes a destroyer's waterline (0.2–1.5 m² for heavy HE). Beyond 15 m there are no
   waterline holes.
5. No HE surface burst gives structural shock (NSF ≥ 0.2) beyond 3 m.
6. A 46 cm AP burst 5–8 m under a CVE's bilge springs her hull and trips her breakers, and doesn't sink her (*White
   Plains*).
7. The bomb table of §3.2 lands in its bands (bombs come later, but the shock model must reproduce them).
8. Determinism: the same seed gives the same draws; nothing is allocated per near miss.

---

## 7. Uncertainties

- **The nose fuze's function time on water** is the most sensitive single number (§2.3: 97 → 14 perforations at 25 m
  from 0 to 0.5 ms). It is a judgement. Fuze data would pin it down: USN Mk 29 PD, RN No. 44, Japanese Type 88 and
  Type 91 nose fuzes.
- **The River Plate DCT case** runs about 8× under the record (§2.6). It may be the fuze time, the geometry, or 13's
  band width. Check the first two before touching the band.
- **The shock bands** are fitted to bomb cases whose charge, depth and distance are the report's estimates. One shell
  case (*White Plains*) agrees.
- **The surface cut-off** uses the energy-flux form. For thin air-backed plates, Taylor's early-time momentum would
  make the cut-off matter less. A surface burst within a metre of the side is uncertain between "shock" and
  "fragments", hence the rule to take the larger.
- **The Type 91's straight underwater path** and its 0.4 s delay [UNCERTAIN] together decide how many diving shells
  burst under keels rather than in hulls (§4). The 46 cm AP's shock numbers are an upper bound until P6 settles the
  path.
- **Exposed crew density** (0.28 per m² of the deck-edge band, ~100 men in the open on a 1944 Fletcher) and the 0.5 m²
  presented area are judgements. Crouching, shields and splinter mats halve them.
- **The Fletcher's plating** (8 mm side, 6.4 mm deckhouses, 4.8 mm funnels) is an estimate for the example. The game
  uses each design's own.
- **The fragment field's own uncertainties** are 13's: THOR above 53 g, the casing shares, the nose-spray cone.
- **Not modelled:** water thrown on deck (weight, men knocked over), the splash's effect on rangefinders (fire
  control, `splashes.md`), and bomb near misses (bombs come later; §3 is ready for them).

---

## Sources

- *Exeter* at the River Plate: https://navyhistory.au/hms-exeter-at-the-battle-of-river-plate/2/
- *Achilles*' DCT, Washbourn's account: https://nzhistory.govt.nz/war/battle-of-river-plate/the-battle
- *White Plains* at Samar: https://en.wikipedia.org/wiki/USS_White_Plains_(CVE-66)
- *Gambier Bay*'s last hours (near misses "jarred the CVE's light hull"): https://www.usni.org/magazines/naval-history-magazine/2019/october/gambier-bays-final-hours
- *Heermann* at Samar: https://destroyerhistory.org/fletcherclass/ussheermann ; https://en.wikipedia.org/wiki/USS_Heermann
- *Sheffield* straddled by *Bismarck*: https://en.wikipedia.org/wiki/Last_battle_of_Bismarck ; https://en.wikipedia.org/wiki/HMS_Sheffield_(C24)
- *Saumarez* at North Cape: https://en.wikipedia.org/wiki/HMS_Saumarez_(G12)
- *Achates* at the Barents Sea: https://www.hampshireprints.co.uk/blogs/royal-navy-warship-and-submarine-classes-complete-guide/hms-achates-h12-the-destroyer-that-kept-making-smoke-until-she-couldnt
- Semenov, *Rasplata* / Tsushima (Japanese shells bursting on the water): https://www.wtj.com/archives/semenov/semenov_tsushima_01.htm
- War Damage to British Naval Vessels, Summary of Damage by Bombs to September 2, 1941 (US Navy): https://www.ibiblio.org/hyperwar/USN/rep/WDR/WAR%20DAMAGE%20TO%20BRITISH%20NAVAL%20VESSELS%20-%20Summary%20of%20Damage%20by%20Bombs%20to%20September%202,%201941.pdf
- Shock factor and its damage table: https://en.wikipedia.org/wiki/Shock_factor
- Cole's similitude equations (US Army Corps of Engineers' underwater blast monitoring guide): https://crawler.dep.state.pa.us/Mining/BureauOfMiningPrograms/BMPPortalFiles/Blasting_Research_Papers/Blast%20Monitoring%20Guidelines/COE%20Underwater%20Blast%20Monitoring.pdf
- Ricochet of spheres off water, 18°/√SG: https://physicstoday.aip.org/quick-study/water-skipping-stones-and-spheres
- Project docs:
  - `13_fragmentation.md` and `fragmentation_ref.py`: the fragment field, and its near-miss sketch in §5.2;
  - `14_spall_behind_armour.md`: the 79 J criterion;
  - `08_gunfire_effects.md`: §3.6, §3.8, §8;
  - `11_blast_confinement.md`: contact holes;
  - `12_explosive_fillers.md`: filler.re;
  - `09_damage_control.md`: holes and leaks;
  - `fire-control-research/dispersion-and-hits.md`: the patterns;
  - `shells-armour-damage.md`: E3 and P6.
