# 13: Heavy hits that don't get through (turret jams, belt leaks, shock)

*This note is research for the damage model and follows `08_gunfire_effects.md`, `11_blast_confinement.md` and
`12_explosive_fillers.md`. It replaces three guesses in 08 §3.7 and R5 that `fighting.md` 4.3a carries into the sim:*

- *A turret or barbette hit that **doesn't get in jams the turret about 35% of the time**. 08 tagged this [INFERRED]. The
  sim gives 3.3% of all hits on QE.*
- *A heavy hit on the **belt at the waterline leaks 40% of the time, at 5–50 t/h**. 08 §8 calls this "a guess".*
- *A heavy hit causes **shock** with a 10% chance of damaging equipment within 10 m, and electrics trip for 1–5 min.*

*The brief adds that these effects "dominate what happens to battleships, since most heavy hits don't penetrate."
§4 tests that claim.*

*Tags follow the other damage docs. **[SRC]** means sourced. **[FIT]** means fitted here to the cases. **[EST]** means
judgement. **[UNCERTAIN]** means the sources are thin or conflict.*

**Companion code:** `nonpen_ref.py` (pure Python, runs in about 6 s, seeded). Every table in §1.4, §2.4, §3.4 and §4
comes out of it. It uses the same conventions as `filler_ref.py`:
- **r** is plate thickness divided by the shell's penetration at that point. A non-penetration has r > 1, and a hit
  that nearly got through has r ≈ 1.
- **Impulse** is I = shell mass × striking speed.

---

## 0. Short answer

1. **The 35% is right for one place on a turret and too high as an average over all of it.** I collected about 45
   non-penetrating or partly penetrating turret and barbette hits, from 1904 to 1943.
   - **Hits on the exposed barbette near the turret ring jam it in train about 35–40% of the time.** That is where
     08's examples came from: Von der Tann A, New Zealand X, Princess Royal X, Boise I and Vincennes I.
   - **Face hits** jam it only about 13% of the time. They mostly kill one gun, at about 33%.
   - **Roof hits** jam it about 7% of the time. They mostly take out sights and one gun.
   - **Barbette hits below the deck edge** jam it about 20% of the time, mostly by heaving the deck or glacis into the
     turret.
   - **Averaged over where hits actually land on a turret, a heavy non-penetration jams it in train about 18–20% of the
     time.** It costs it something lasting about 45–55% of the time. The two nearly complete samples, the USN War
     Damage Reports and Lützow's hit-by-hit list, give 27% train jams per recorded hit. The narrative sources give
     about 50%, but they over-report dramatic outcomes.
2. **How long a jam lasts depends on what jammed it, and the jams fall into three groups:**
   - **Debris** (spall, a loose piece of armour, or splinters in the gap, roller path or rack). The crew clears it in
     3–40 min: New Zealand X, Tiger X, Seydlitz.
   - **Deck** (the deck or glacis pushed into the turret). It has to be cut free, which takes 1–10 h: Von der Tann C
     took 3.5 h and Jean Bart took 9 h 18 min.
   - **Ring** (the barbette ring, roller path or training gear distorted or broken). This is a dockyard job: Von der
     Tann A, Princess Royal X, Boise I, Scharnhorst A, Bismarck C and Ajax Y.

   **About 44% of jams last the rest of the battle.** The sim's "half repairable in 10–60 min" is too kind.
3. **A "stiff" turret is a real state.** South Dakota's Turret III was hit on the barbette by a 14″ shell and stayed
   "functional, if somewhat slow to move in train" (Okun). Give it about 40% of normal training speed.
4. **Belt leaks are a regime, not one rate.** About half of the heavy hits that fail to penetrate within ±1 m of the
   waterline leak (range 0.4–0.65). What leaks is the opening into the cell behind the belt, sized by how far the
   plate was driven in:
   - **Seep, about 55% of leaks.** Rivets, bolts or a hairline seam: 2–20 cm², which is 3–30 t/h at 2 m. North
     Carolina's loosened rivets leaked "about 50 gal per hour". One sheared 22 mm rivet at 3 m lets in about 6.5 t/h.
   - **Moderate, about 30%.** A plate dished 5–20 cm: 30–250 cm², which is 40–350 t/h. Examples are Moltke's
     forecastle plate (pushed in 20 cm, with "a little" leakage through broken-off rivets next door) and Markgraf's
     14″ plates.
   - **Major, about 12%.** A plate driven in 30 cm or more, or a joint opened: 0.05–0.2 m², which is 0.7–2.8 kt/h.
     Grosser Kurfürst had a plate pushed in 33 cm over 7.9 m, took about 800 t, and had about 3,000 t aboard by morning.
     Lion's plate went 2 ft in.
   - **Lost, about 3%.** The plate fell off: 0.5–2 m². This happened to Seydlitz, to Derfflinger twice, and to Moltke
     aft.

   08's 5–50 t/h is the seep band. The tail is what matters, and it is what sank Oslyabya once her belt had been
   loosened.
5. **What a belt leak costs is the cell behind it, the list and the time, not the tonnage.** Inflow stops when the
   bunker, wing void or tank behind the plate is full. That is 150–600 t, at about 1° of list per 100–250 t. Wedges and
   mats work loose at speed: Grosser Kurfürst overnight, Bismarck's collision mat, Seydlitz. One flood in 6–7 hits the
   ship's feed water or oil (Lion's port engine, Malaya's A boiler room).
6. **True shock is local, and the WWII ship is the fragile one.**
   - **Where the shock goes.** A 15″ shell carries about 4×10⁵ N·s. That is about 4 m/s for the struck plate and its
     bay, 1.3 m/s for a conning tower, 0.3 m/s for a turret and 0.01 m/s for the whole ship. So shock hits hard in the
     struck structure and much less one bulkhead away.
   - **Most "fire control lost" events are splinters and blast, not shock.** Bismarck, Scharnhorst, Tiger Q and Exeter
     were all this kind.
   - **The shock events on record are brief:**
     - breakers tripped for 10 s – 3 min (Sculpin, South Dakota);
     - a director or turret crew was stunned for seconds to a few minutes (New Zealand X, Achilles' DCT);
     - a gyro had to be restarted within minutes (Duke of York);
     - radar died, often from the ship's **own guns** (Bismarck's FuMO 23, perhaps KGV's 284).
   - **The one dangerous event is a cascade.** On South Dakota, shock from her own Turret III closed an automatic bus
     transfer switch onto an out-of-phase board. The generator breakers cleared the fault because the feeder breakers
     had been locked in, so all power aft was lost.
   - **This is an era effect.** Pre-1914 ships had little to trip, WWII ships of 1939–42 were the most fragile, and
     the 1942–43 fixes cut it back. North Carolina's torpedo hit tripped "no breaker" after the "fuse and switch
     securing alterations".
7. **Do these dominate? For turrets, yes; for water and fire control, no.** Take 100 heavy hits on a four-turret
   capital ship over 2 h (§4):
   - **Turrets.** Non-penetrations cost about **165 turret-minutes**. Penetrations cost about **150**. There are about
     five times more non-penetrating turret hits, and each costs about a fifth as much. So non-penetrations are half of
     all lost main-battery time, and they are the half that comes back.
   - **Water.** Belt non-penetrations put in about 1,000 t across about 7 leaks. Penetrations, ends and diving hits put
     in most of the rest. 08 R7 gives about 150–350 t per heavy hit over a whole beating.
   - **Fire control.** It is lost mainly to hits on unarmoured superstructure (08 R6), which are not this note's
     concern.

   So: **model these three effects properly, because they are most of what armour "does".** But don't raise their rates
   to make them dominant. In one or two places they should come down.

---

## 1. Turrets and barbettes

### 1.1 The cases (condensed; ~66 events collected)

**Non-penetrating and partial hits that did something:**

| Ship, action | Shell | Where | Pen | Effect | Out for | Src |
|---|---|---|---|---|---|---|
| Von der Tann, Jutland 17:20 | 13.5″ (Tiger) | A barbette | N/P | A plate broke loose inside and jammed the training gear at 120° | rest of battle | W-VdT |
| Von der Tann, Jutland 17:23 | 13.5″ | deck next to C | N (deck holed) | deck wreckage stopped traverse | ~3.5 h, cut away | W-VdT |
| New Zealand, Jutland 16:26 | 11″ | X barbette | N | spall from the plate's inner face into the trunk; "briefly jammed"; turret officer stunned | brief | W-NZ, USNI25 |
| Princess Royal, Jutland ~18:15 | 12″ (Markgraf) | X armour, "badly distorted" | P | a fragment killed the left gun's crew and cut the hydraulics; "turret jambed" | dockyard | DP-PR |
| Tiger, Dogger Bank | 11″ | Q roof | P | left breech damaged, training jammed | turret out; repaired 8 Feb | W-Tig |
| Tiger, Jutland 15:55 | 11″ (Moltke) | Q roof, sighting hood | P | sights, rangefinder, periscope and firing circuits wrecked; right cage jammed; **no train jam** | left gun kept firing | DP-Tig |
| Lion, Dogger Bank | 8.2″ (Blücher) | A roof | N (dented) | left gun out | 2 h | W-Lion |
| Seydlitz, Jutland ~17:10 | 15″ | wing turret face, 10″ | P | right gun's training and elevating gear wrecked; crew coupled to the left gun's gear; gas | ~3 min | GWF-Sey |
| Lützow, Jutland 20:15 / 20:17 | 13.5″ | A outside the gun port / B, 250 mm (plug) | N / P | A: right gun jammed. B: right hoist and loading gear lost, a charge burned; still trained to ~280° | gun out, rest of battle | NGB-Lut |
| Peresvet, Yellow Sea | 2 × 12″ | fore 10″ turret | N | jammed in training | rest of battle | GWPDA |
| Oryol, Tsushima | 8″ | rear 12″ roof | N | roof forced down; left gun's elevation limited | rest of battle | WH-Oryol |
| Boise, Cape Esperance | 8″ AP | barbette I, 6″, 15″ above the deck | N (broke up, base stuck) | jammed in train at 106° | dockyard | WDR 24 |
| Vincennes / Quincy, Savo | 8″ | barbette I / turret III face ("dislodged a large piece of armor") | N? / N-P | jammed in train | ships lost | WDR 29 |
| South Dakota, Guadalcanal | 14″ (cap probably stripped by a deck) | barbette III, 17″ below its top | N (1.5″ gouge) | "some difficulty training"; gas seal damaged | stayed usable (**stiff**) | WDR 57, tech-092 |
| Jean Bart, Casablanca 08:06 | 16″ (Massachusetts) | glacis by turret 1 pushed down | N | deformed steel jammed it | 9 h 18 min, cut free | W-JB |
| Bismarck, 27 May | 14″ (KGV, survivors) | Caesar face | N? | "shock disabled the turret elevating and training machinery" | rest of battle | INRO |
| Scharnhorst, North Cape | 14″ (DoY) | Anton | N? | "jammed the turret's training gears"; magazine flooded after a fire | rest of battle | W-Scharn |
| Ajax, River Plate | 11″ base fragment | Y barbette "close to the training rack" | N | jammed | not stated | LG |
| Gneisenau, Lofoten | 4.5″ or 15″ [UNCERTAIN] | A rangefinder cover | N | sea water through the gap flooded the turret | rest of action | NG |

**Hits that did nothing, which are under-reported:**
- Princess Royal B ("without internal damage").
- Boise III face (joints loosened; she kept firing) and II side.
- Vincennes II top and III.
- Oryol's 10″ hit ("resisted").
- San Francisco II: two 14″ HE hits, one deflected and one dented 1.5″.
- Lion A barbette at Dogger Bank (a small lobby fire only).

**Penetrations, for contrast:**
- About 90% put the turret out for good, and 30–40% start a charge fire (Lion Q, Seydlitz C/D, Derfflinger C/D,
  Invincible, Bismarck B/D, Kirishima 1).
- The partial survivals are the useful ones:
  - Tiger X: 11″ through the barbette, came to rest in the gunhouse; **firing again in 7 min**.
  - Fuji's left gun.
  - Dunkerque's left half-turret.

Full list with URLs: the source notes at the end. The agent tables are in this session's working notes and are
condensed here.

### 1.2 Mechanisms

| Mechanism | Cases | Typical outcome |
|---|---|---|
| Armour piece or spall knocked into the gap, roller path or rack | VdT A, NZ X, PR X, Quincy III, Ajax Y | Train jam: minutes if it is loose debris, dockyard if the plate is displaced |
| Deck or glacis heaved into the turret, or wreckage blocking traverse | VdT C, Jean Bart, Bismarck B (?) | Train jam lasting hours, cut free with oxy-acetylene |
| Ring or roller path distorted, or training gear broken | Boise I, Vincennes I, Peresvet, Scharnhorst A, Bismarck C | Rest of the battle |
| The same, mild | SD III | **Stiff**: trains, slowly |
| Face hit: cradle, slide, breech or gun port | Lion A, Lützow A/B, Seydlitz wing, Tiger Q (Dogger Bank) | One gun out |
| Roof driven in | Oryol | Elevation limited, for one gun or both |
| Roof or hood hit | Tiger Q (Jutland), Vincennes hoods, SD I sight port | Sights, rangefinder or periscope lost; the turret goes to director control or fires degraded |
| Hydraulics, power or cables cut nearby | PR X, Lützow D (hand training), Kirishima's pump rooms | Belongs to the service network (06), not the turret |

Design notes [SRC]:
- **NavWeaps tech-118.** A big burst in the upper hull can push the weather deck up under the guns. This was one
  reason for holding-down clips. BuShips held clip clearance to ≤ 1/8″.
- **Okun on SD III (tech-092).** With its cap on and a little higher, the hit "would most definitely have jammed the
  turret permanently".
- **Musser (USNI 1925).** Barbettes, decks and turret foundations are "the greatest weakness".

### 1.3 How long

| Given a jam (heavy AP, all locations) | Model | Cases |
|---|---|---|
| < 15 min | 0.26 | Seydlitz 3 min, Tiger X 7 min, NZ "brief" |
| 15 min – 4 h | 0.24 | Lion A gun 2 h, VdT C 3.5 h |
| 4 – 12 h | 0.06 | Jean Bart 9 h 18 min |
| Rest of battle / dockyard | **0.44** | VdT A, PR X, Peresvet, Boise I, Scharnhorst A, Bismarck C … |

Notes:
- The agent's case-weighted shares were 0.30 / 0.20 / 0.05 / 0.45. The model reproduces them through the cause draw
  (`JAM_CAUSE`) without being fitted to them directly.
- A one-gun loss lasts the battle about half the time; otherwise 30 min – 4 h.
- A stun pauses the turret for 10 s – 2 min. A stun also comes with the other outcomes about 60% of the time (§3.3);
  the table's "stun" column is a stun with nothing lasting.

### 1.4 The model (`turret_nonpen()`)

```
P(outcome | location) = base[location][outcome] · size(I) · margin(r)      (· HE row if it burst on the plate)
size(I)   = min(1.15, (I / 4e5 N·s)^0.5)        8″ ≈ 0.4, 12″ ≈ 0.7, 15″ ≈ 1.0, 16″ Mk 8 ≈ 1.15
margin(r) = clamp(1.3 − 0.3·r, 0.4, 1)           a near thing throws plate and spall; a shell far short only dents
```

| Location (share of turret hits) | jam | stiff | one gun | sights | stun only | none |
|---|---|---|---|---|---|---|
| Barbette, exposed, near the ring (0.25) | **0.38** | 0.06 | 0.05 | 0.03 | 0.10 | 0.38 |
| Barbette at or below the deck, glacis (0.15) | 0.22 | 0.10 | 0.03 | 0 | 0.05 | 0.60 |
| Face (0.25) | 0.14 | 0.04 | **0.33** | 0.08 | 0.15 | 0.26 |
| Side (0.20) | 0.12 | 0.04 | 0.12 | 0.12 | 0.08 | 0.52 |
| Roof (0.15) | 0.08 | 0.02 | 0.22 | **0.30** | 0.15 | 0.23 |

These are base values (at r ≈ 1.3 the multiplier is 0.91). What they give, mixed over where hits land:

| Case | jam | stiff | gun | sights | stun | none | jam > 4 h |
|---|---|---|---|---|---|---|---|
| 8″ AP on a 12″ turret (r 1.8) | 0.06 | 0.02 | 0.05 | 0.03 | 0.07 | 0.78 | 0.03 |
| 12″ AP, r 1.3 | 0.12 | 0.03 | 0.10 | 0.06 | 0.09 | 0.60 | 0.06 |
| **15″ APC, r 1.3** | **0.18** | 0.05 | 0.14 | 0.09 | 0.11 | 0.43 | 0.09 |
| 15″ APC, near thing (r 1.05) | 0.19 | 0.05 | 0.16 | 0.09 | 0.11 | 0.40 | 0.10 |
| 16″ AP on 17″, r 1.6 | 0.19 | 0.05 | 0.15 | 0.09 | 0.11 | 0.43 | 0.09 |
| 14″ HE (bombardment) | 0.11 | 0.04 | 0.12 | 0.14 | 0.12 | 0.48 | 0.05 |

Comparison with the earlier figures:
- **08's row "≥ 11″: jam 35%, one gun 15%, sights 25%, stunned 50%"** becomes jam 18% (35% on the exposed barbette),
  one gun 14%, sights 9% (30% on roofs), and stun about 60% on top.
- **08's "8″: 5%"** holds: the model gives 6%.
- **Against cruiser turrets (1–6″)**, an 8″ hit that doesn't penetrate is a penetration in all but name, by r. Savo
  and Boise give about 35% train jams. With r near 1 and thin plate the model gives about 0.16–0.2. **For thin
  turrets, raise the barbette base by about 1.5×** [FIT, Savo/Boise]. Otherwise treat a sub-6″ mount as the
  penetration case.

**The sim today:** "3.3% of hits" on QE at 16 km against "35% of non-pen turret hits" are two different
denominators. Non-penetrating turret and barbette hits are about 13% of all heavy hits (§4). So 13% × 0.18 ≈ 2.4% of
all hits jam a turret, against the sim's 3.3%. The level is about right. **The change is the shape:**
- split by location;
- scale by size and margin;
- set the duration by cause;
- add "stiff" and "one gun" as states.

---

## 2. The belt at the waterline

### 2.1 The cases (condensed; 37 collected)

| Ship, action | Shell | What the belt did | Water | Src |
|---|---|---|---|---|
| **Lion**, Dogger Bank 10:18 | 12″ (Derfflinger) | 9″ plate, 16 × 5.75 ft, **driven about 2 ft inboard** through its backing; port feed tank ruptured and bunkers flooded | 3,000 t, 10° list (all hits); **port engine lost to salt in the feed** | W-Lion, historyofwar |
| **Grosser Kurfürst**, Jutland 20:18 | 15″ | Burst on impact; plating **forced in 33 cm over 7.9 m** | **~800 t**, then DC "temporarily" stopped it; 4° list to under 1° by counterflooding; **~3,000 t by Helgoland** (~180 t/h net overnight) | W-GK; A-H report p.26 |
| Markgraf, Jutland 18:10 | 15″ | struck **on the joint**: "from both plates an equal piece was punched out" | ~400 t | A-H p.25 |
| Markgraf, Jutland 20:14 | 12″ dud | shattered on the upper belt; the 14″ plates below "slightly forced inward" | "minor" | W-Markgraf |
| **Moltke**, Jutland | 15″ | forecastle plate whole, **pushed in ~20 cm**; compartment behind filled; next one "leaked a little because of rivets that had broken off" | 1 compartment + seep | A-H p.23 |
| Moltke, Jutland | 15″ | ~1 m below the WL aft: "one plate was knocked out and missing" | "quite significant"; ship ~1,000 t with counterflooding | A-H p.23 |
| Derfflinger, Dogger Bank | 13.5″ | "two armor plates… forced inward", bunkers flooded | minor | W-Derf |
| Derfflinger / Seydlitz, Jutland | heavy | "two plates fell off" (D); "a belt armor plate is missing… has fallen off" (S) | "heavy water penetration" | A-H pp.21–22 |
| Von der Tann, Jutland 17:09 | 15″ | lower belt aft dislodged/penetrated; stern torpedo room half full | 600 t | W-VdT, A-H |
| Invincible, Falklands | 8.2″ | "armour plate dented… No. 17 bulkhead buckled… rivets sheared"; another hit fractured armour forward | 1 compartment | Admiralty report (tapatalk) |
| Tsesarevich / Sevastopol, Yellow Sea | heavy | "forced in an armor plate"; belt hit 3× and "shaken enough to cause leakage" | some | GWPDA |
| **Oslyabya**, Tsushima | 12″ | waterline belt forward "loosened", then shells got through; belt overloaded under water | capsized ~50 min after the main hits | GWPDA, W |
| Alexander III, Tsushima | heavy | flooding from "dishing the armor" | fatal with the rest | GWPDA |
| Oryol, Tsushima | 12″ + 3 × 6″ | all "repulsed"; no leak | 0 | GWPDA |
| South Dakota hit 4 | 8″ (short, rising) | internal belt "not indented"; outer tanks flooded | 0.75° list, trimmed out | WDR 57 |
| South Dakota hit 11 | 6–8″ | "rivets in 'N' strake were loose or missing" | 3 tanks | WDR 57 |
| North Carolina (torpedo analogue) | Type 95 | "belt was loosened and lifted" fr. 37–59; plate pushed in 6″; bolts pulled the shell out 0.75″; failures "consistently… rivet failure" | **"about 50 gal per hour"** through loosened rivets | WDR 61 |
| Yamato, Dec 1943 (torpedo analogue) | Mk 14 | "failure of the armor belt joint" between upper and lower belts | ~3,000 t, "far more than anticipated" | combinedfleet |

**Under-reporting.** A belt hit with no consequence is rarely written down. Oryol, Seydlitz at Dogger Bank and SD hit 4's
armour are the few recorded ones.

### 2.2 How a belt that holds still leaks

Belts were bolted from behind into blind holes:
- about **1 bolt per 0.65 m²**, through **50–100 mm of teak backing**, onto a **16–25 mm skin** on the frames
  (Bismarck: kbismarck; WWI: Brassey-era texts).

A shell that doesn't get through still delivers its whole impulse to the plate:
- The backing crushes and the plate moves in.
- Bolts pull through or drag the skin with them (NC: skin pulled out 0.75″).
- The skin's riveted seams and butts open, and its rivets shear. The Bureau noted "the relative weakness of the
  riveted seams and butts".
- Hits on a **joint** punch both plate edges (Markgraf). **Repeat** hits on a loosened plate open the way for the next
  shell (Oslyabya).
- Welded skins (late WWII USN) mostly avoided the rivet path.

The water goes into **whatever is behind the plate**:
- WWI German ships: coal bunkers and wing passages.
- RN: bunkers and feed tanks.
- WWII: wing voids, oil tanks, the bulge.
- Internal-belt ships (SoDak) always flood the outboard tanks from a hit near the waterline, but the amounts are small
  and symmetrical.

### 2.3 Rates

| Zone of a heavy non-pen belt hit | P(any leak) | Notes |
|---|---|---|
| Within ±1 m of the waterline | **0.50** (0.4–0.65) | Lion, GK, Moltke, Markgraf, Derfflinger leaked; Oryol and others didn't |
| Upper belt, > 1 m above | 0.25 | Matters only with list, trim or a seaway |
| Lower edge or just below | 0.80 | Often a penetration of thin plate in effect (08 §3.8 diving hits) |
| On a plate joint (~25% of hits) | ×1.5, and the regime shifts worse | Markgraf, Yamato |
| Repeat hit within ~3 m of an earlier one | ×2; the plate is "loosened" (defends as 0.85 of itself) | Oslyabya, Sevastopol |
| Welded skin | ×0.6 | NC report |

The leak regimes [FIT]. Areas are drawn uniformly in log space. Flows use `Q = 0.6·A·√(2gh)`, as in 09.

| Regime | Share | Opening | at 2 m: low / mid / high | DC result | Cases |
|---|---|---|---|---|---|
| Seep | 0.55 | 2–20 cm² | 3 / 9 / 28 t/h | caulked to ×0.15 in 10–40 min | NC rivets, Moltke's neighbour, Sevastopol, SoDak hit 11 |
| Moderate | 0.30 | 30–250 cm² | 42 / 120 / 347 t/h | wedged to ×0.3 in 20–60 min | Moltke fwd, Markgraf 14″, Derfflinger (Dogger Bank) |
| Major | 0.12 | 0.05–0.2 m² | 0.7 / 1.4 / 2.8 kt/h | mats and shores to ×0.5 in 30–90 min | GK, Lion, Markgraf joint |
| Lost | 0.03 | 0.5–2 m² | 7–28 kt/h: a hole | ×0.7 in 1–4 h, a collision mat at low speed only | Seydlitz, Derfflinger, Moltke aft |

The major and lost shares go up for a near thing, a joint or a repeat hit (`belt_leak()`).

**Under way.** Above about 15 kn a worked leak goes back toward its full area. The rate is 15% per 10 min [EST, from
GK, Bismarck's mat and Seydlitz's wedges].

**Feed water and fuel.** If the cell behind is a feed or oil tank, a boiler or engine falters 15% of the time (Lion's
port engine, Malaya's A boiler room).

What a heavy hit at the waterline puts in. The cell behind is 150–600 t, head 1.5–3 m, 20 kn for an hour then 12 kn.
These are runs of `belt_leak_mc()`:

| Shell | P(leak) | seep / mod / major / lost | water per leak: mean / median / p90 |
|---|---|---|---|
| 15″ APC, r 1.3 | 0.46 | 0.23 / 0.13 / 0.08 / 0.02 | 146 / 67 / 421 t |
| 15″ APC, near thing | 0.49 | 0.24 / 0.13 / 0.10 / 0.02 | 148 / 48 / 420 t |
| 12″ AP | 0.32 | 0.17 / 0.09 / 0.05 / 0.01 | 129 / 28 / 378 t |
| 8″ AP, r 1.8 | 0.15 | 0.08 / 0.04 / 0.02 / 0 | 120 / 22 / 366 t |

**Reading.**
- The median leak is small and gets fixed.
- The mean is set by the one leak in five that fills its whole cell.
- The ship is hurt by the cells' **asymmetry** (list), by **where** they are (forward cells at speed), and by what
  leaks *onward* from them. 09's link model already handles the onward leaking.
- **The 5–50 t/h figure should go.** The sim already has holes, cells and the orifice law, so a belt leak should be
  just another hole: an area into the cell behind the plate, with the regime's DC row.

---

## 3. Shock

### 3.1 What a heavy hit can shake

A heavy shell is stopped in about 2–4 ms, with a mean force of 1–2×10⁸ N. The velocity jump is the impulse divided by
the mass that takes it up [EST]:

| Participating mass | Δv from a 15″ APC (4×10⁵ N·s) |
|---|---|
| struck plate and its bay (~100 t) | **4.0 m/s** |
| conning tower (~300 t) | 1.3 m/s |
| turret (~1,500 t) | 0.27 m/s |
| a 20 m section (~4,000 t) | 0.10 m/s |
| whole ship (45 kt) | 0.01 m/s |

For scale:
- Shock-qualification drops are of order 1–4 m/s (the HI shock machines; MIL-S-901 from 1949).
- A ship's own 16″/50 recoil is about 1.0–1.3×10⁶ N·s per gun, *more* impulse, but spread over about 0.15 s. That is
  about 10× lower in peak force and much lower in frequency. It finds weak latches, contactors, mast fittings and
  filaments, and it finds them every salvo.
- An underwater near miss loads the whole bottom at once. Marblehead's and the British bomb summaries say near misses
  were worse than direct hits.

So a shell's shock is **as bad as a moderate near miss, but only in the struck structure**.

### 3.2 The cases (condensed; 40 collected)

| Ship | Cause | What failed | How long | Src |
|---|---|---|---|---|
| South Dakota, Guadalcanal | **own Turret III firing** | shock closed an ABT onto an out-of-phase board; contacts welded; feeder shorted; generator breakers tripped (the feeders were "locked in"); **all power aft** | ~1 min | WDR 57 §§44–49 |
| South Dakota | enemy hits | IC/FC circuits shorted, load moved onto a small emergency diesel, its fuses blew; **FC and IC ship-wide** | ~3 min | WDR 57 §43 |
| South Dakota | own blast | aircraft burned and blown overboard; 40 mm mount wrecked; deck dished 4″; doors and phone boxes wrecked | yard | WDR 57 |
| Bismarck, 23 May | **own guns** | FuMO 23 forward radar disabled | rest of the sortie | W-Bis |
| KGV, 27 May | 25 min of firing | Type 284 "broke down" (cause not stated; usually blamed on own shock) [UNCERTAIN] | — | tech-016 |
| Duke of York, North Cape | 2 × 11″ through the mast, no burst | 284 aerial knocked over; **gyro stabiliser restarted** | "within a few minutes" | W-NC |
| Achilles, River Plate | 11″ short burst | DCT: 4 killed, gunnery officer **stunned**; DCT undamaged | "a few minutes" | London Gazette |
| Exeter, River Plate | 11″ hits | gyro compass repeaters out; conned by a boat's compass | 12 min+ | Gazette |
| New Zealand X, Jutland | 11″ barbette, no pen | turret officer stunned ("at first thought he was dead") | seconds | USNI25 |
| Derfflinger (von Hase) | heavy hits on armour | "a vibration of the whole ship"; the CT "seemed to be hurled into the air"; phones and voice pipes shot away; **sighting gear still worked** | messengers | von Hase |
| Von der Tann, Jutland 20:19 | 15″ on the CT | fragments into the condenser cut **all the ship's lights** (splinters, not shock) | — | jutland1916 |
| Honolulu (near-miss bomb) | UNDEX | mercury thrown from both gyros; after rangefinder lost calibration (a prism seal failed) | forward director "at once"; others yard | WDR 1 |
| North Carolina (torpedo) | UNDEX | **"No breaker tripped"** after the securing alterations; radar mast bracing failed; director pinion burred | ship's force / yard | WDR 61 |
| Southampton 1939, Perth 1941, RN destroyers 1940–41 | bombs, near misses | power failed temporarily; "range-keeping instruments put out of action by shock"; cast-iron dynamo feet cracked | minutes to months | British bomb summary |
| Submarines (WDR 58) | depth charges | overload relays reset by hand in ~10 s; more than half the lamps broken; gyro mercury; radar tubes | 10 s – a day | WDR 58 |

Durations [SRC]:
- Sperry (1911): the gyro master compass settles in minutes if it starts near the meridian, and in about 2 h if it is
  90° off.
- NavWeaps tech-074: fire-control gyros need about 4 h to reach full speed; the Mk 1 stable vertical erects in
  ≤ 5 min after a 30–60 min spin-up.

### 3.3 Fragility by era, and what fixed it

| Era | Fragile | Robust | Fixes |
|---|---|---|---|
| Pre-1914 | magnetic compass (thrown off by gun shock), voice pipes, conning-tower crews | there was little to trip | gyro compass below decks, on springs (~1911) |
| WWI | dynamos and switchboards (mostly through flooding and splinters), turret sights, firing circuits, phones | **optics**: von Hase, "delicate mechanism… still in order" | duplicate control positions, armoured communication tubes, spread-out dynamos |
| WWII, 1939–42 | breakers, contactors and ABTs (trip *or* close spuriously), radar tubes and aerials, mercury gyros, rangefinder prisms, cast iron, lamps | — | RN 1939–41: breakers locked in, starter handles tied, fuses wired. USN 1942: "fuse and switch securing alterations" |
| WWII, 1943–45 | radar still | breakers, cast iron (now steel) | manual transfer switches for ABTs; time-delay generator relays; casualty power cables; shock-qualified gear; resilient mounts; battle lanterns |

**The trade-off.** Locking breakers in stops nuisance trips, but faults then clear at the generator, so outages are
wider (South Dakota). That is a player-facing knob worth having (§5.4).

### 3.4 Rates per heavy non-penetrating hit [EST]

These apply to hits on belt, barbette, turret or CT, scaled by (I / 4×10⁵)^0.7. Zones:
- **A** is the struck structure or its compartment group, about 10–15 m. A whole turret or CT counts as A.
- **B** is the adjacent space, 15–40 m or the next deck.
- **C** is ship-wide.

| Effect | Zone | pre-1914 | WWI | WWII 1939–42 | WWII 1943– | Outage |
|---|---|---|---|---|---|---|
| Local breakers trip, lamps fail | A | 0.30 | 0.40 | 0.50 | 0.25 | 90%: 10 s – 3 min; 10%: to 15 min |
| A section's power or lights | B | 0.03 | 0.08 | 0.15 | 0.06 | 1–5 min (20%: to 20) |
| **Ship-wide cascade** | C | 0 | 0.005 | **0.02** | 0.008 | 90%: 1–10 min; 10%: 30–90 min |
| Gyrocompass upset | C | — | 0.03 | 0.05 | 0.03 | standby or magnetic in 1–2 min; master 1–4 h |
| Director gyro / stable vertical | B | — | 0.08 | 0.12 | 0.06 | 5–60 min |
| Rangefinder out of adjustment | A | 0.15 | 0.20 | 0.20 | 0.12 | 50%: 5–20 min; 30%: 2–4 h; 20%: until port |
| Radar set fails | B | — | — | 0.25 | 0.12 | 60%: 5–20 min; 25%: 1–4 h; 15%: until port |
| Phones and FC circuits through the zone | A | 0.25 | 0.30 | 0.25 | 0.15 | rest of the action (messengers, sound-powered phones) |
| Cast iron, gauge glasses, small pipes | A | 0.20 | 0.20 | 0.20 | 0.06 | isolated in 1–10 min; a cracked foot means days |
| **Crew stunned** (turret or CT hit) | A | 0.6 / 0.7 | same | same | same | 10 s – 2 min; 10%: a key man out 5+ min |

Notes:
- The cascade rate was cut from the agent's 0.05–0.08 to 0.02. At 0.05, 35 armour hits would give a near-certain
  blackout, and nothing in the record looks like that. South Dakota's one hit-caused event (3 min) came from about
  26 hits, few of them on armour.
- **Own guns:** per 20 min of sustained main-battery fire, WWII before the fixes: radar fails 0.20, a breaker or ABT
  event 0.05. Hardened: 0.07 and 0.02.
- **Own blast** wrecks boats, aircraft, light shields and 20/40 mm mounts in the arc as a matter of course. That
  belongs to the arc limits, not to chance.

**Don't double-count.** A shell that bursts on the armour also throws splinters and blast at exposed aerials,
searchlights, rangefinder windows and cables within about 15 m. That is already the burst model's job (11). The table
above is only the mechanical channel through the structure.

---

## 4. Do they dominate? 100 heavy hits on a capital ship

`battle_mc()` sets up a four-turret ship that takes 100 heavy hits spread over 2 h. Hit locations and P(penetrates)
[EST]: turret 10% / 0.15, barbette 6% / 0.20, CT 1% / 0.15, belt at the WL 8% / 0.08, upper belt 7% / 0.12, lower belt
3% / 0.10, deck 10% / 0.20, unarmoured 55%. A one-gun loss counts as half a turret, sights out as a quarter, stiff as
0.3.

| Per 100 heavy hits | WWI | WWII 1939–42 | WWII 1943– |
|---|---|---|---|
| Non-pen / pen hits on turrets and barbettes | 13.3 / 2.7 | same | same |
| Train jams | 2.1 | 2.1 | 2.1 |
| **Turret-minutes lost to non-pens** | **166** | 167 | 167 |
| Turret-minutes lost to penetrations | 150 | 150 | 148 |
| Belt leaks (of which major or lost) | 6.7 (1.5) | 6.8 (1.6) | 6.7 (1.6) |
| Shock: power trips and outages | 15 | 20 | 10 |
| Rangefinder, director or radar knocked out by shock | 8.5 | 17 | 9 |
| P(≥ 1 ship-wide power cascade) | 0.15 | 0.47 | 0.21 |

What the numbers say:

- **Turrets: yes, non-penetrations are about half of all lost main-battery time.** There are five times more of them,
  and each costs about a fifth as much.
  - Their losses come back: a quarter of the jams clear inside 15 min. That gives fights a rhythm of turrets falling
    silent and returning, and the record shows that rhythm: Tiger X, Seydlitz, Von der Tann's three turrets back
    before the end.
  - Penetrations decide which turrets stay out for good.
- **Water: no.** Belt non-penetrations give about 7 leaks and about 1,000 t per 100 hits. Most of that is one or two
  major leaks and the cells they fill. The ends, diving hits and penetrations (08 R7) give several times more.
  - What the belt leaks add is **list early in a fight** (Lion 10°, GK 4°) and **leaks that reopen at speed**.
- **Fire control and power: not by shock, except in 1939–42.** Shock events are many but short.
  - What matters is the ship-wide cascade (0.47 per 100 hits before the fixes) and the radar failures.
  - The lost directors of the record come from splinters and hits on the superstructure (08 R6).
- **The brief's premise holds for the count, not the weight.** Most heavy hits on *armour* don't penetrate (here
  85–92%). But armour takes only about 45% of hits, and the other 55% land on unarmoured structure, which is where
  08's mission kills come from.

---

## 5. Proposed implementation

### 5.1 Where it plugs in

Terminal's outcome class **defeated** (and **ricochet**, and **burst on the plate**) hands `Effects` four things: the
plate the shell met, r, I = m·v, and the struck node. A new `NonPen` step, in `Effects`, does the following:

- **Turret or barbette node:**
  - Sub-location from the hit point: exposed barbette, barbette at the deck or glacis, face, side, roof. The class has
    the gunhouse box and the barbette cylinder, and the deck height is known.
  - `turret_nonpen()` gives one outcome and a cause.
  - The condition gets `jammed{cause, until}` (until = ∞ for ring), `stiff{until}`, `gunOut{n, until}`, `sightsOut`
    and `stunUntil`.
  - Functions: a jammed mount fires only on its held bearing. A stiff mount trains at 0.4×. A mount with one gun out
    fires at half. With sights out it needs a working director, or fires at a degraded level. A stunned mount skips
    salvoes until `stunUntil`.
- **Belt plate:**
  - Zone from the hit's height against the waterline. Use the *current* waterline (heel, trim, sinkage), since 4.3b has
    it.
  - `joint` comes from the hit point against the plate grid (plates of about 5 × 2.5 m, or from the class).
  - `repeat` comes from a list of earlier hits on the same plate. Keep a `loosened` flag per plate; a loosened plate
    defends at 0.85.
  - `belt_leak()` gives a **hole** into the cell behind the plate, with an area, a regime and a `reopensAtSpeed` flag.
    Everything after that is the existing flooding and DC: orifice law, cells, links, parties.
  - If the cell behind is a feed or oil tank: 15% chance of a boiler or engine casualty, lasting 10–60 min [EST]
    (Lion, Malaya).
- **Any armour hit (belt, barbette, turret, CT):**
  - `shock_events(era)` against the nodes in zones A, B and C. Zone A is the struck node's compartment group; B is the
    cells adjacent to it in the cell graph.
  - The output is **timed degradations**, not states: a node is "tripped until t". Functions read them like any other
    need. A ship-wide cascade sets every electrical consumer's power need to 0 until t.
  - Fire control: a director, rangefinder or gyro that is out for less than its solution's memory degrades the
    solution rather than resetting it (`fire_control_ref.py`). One that is out longer forces local control.
- **Own guns:** every 20 min of main-battery fire rolls `OWN_GUN[era]` against radar and the electrical plant.
  Mounts, boats and aircraft in a turret's blast arc are wrecked when it fires across them; the arc limits already
  forbid this unless overridden.

Cost: about five extra random draws per armour hit, and no allocation. Jams, leaks and trips sit in the existing
per-node and per-cell slots.

### 5.2 Log lines (cause → effect, as 4.3a)

- "15in APC · B barbette · defeated · **jammed in train** (splinters in the roller path: clearing)"
- "15in APC · A face · defeated · right gun out"
- "12in AP · belt, waterline, plate joint · defeated · **plate driven in, No. 3 port bunker flooding** (≈ 1,200 t/h)"
- "14in AP · conning tower · defeated · CT crew stunned · gyro compass upset: on the standby"

### 5.3 Tests (wide bands)

1. 15″ APC non-penetrations on turrets: 15–25% train jams. On exposed barbettes 30–45%; on roofs under 12%.
2. Of the jams, 35–55% last the battle and 20–35% clear inside 15 min.
3. 8″ AP on a battleship turret: under 10% jams.
4. A heavy non-penetration at the waterline: 40–55% leak, under 15% major. A hit on a joint or a repeat hit is
   noticeably worse.
5. A GK-type major leak at 2 m with no DC fills a 600 t cell in under 1 h. Worked and slowed to 12 kn, it is held; at
   20+ kn it reopens within 1–2 h.
6. WWII 1939–42: P(ship-wide cascade) per 100 heavy hits is 0.3–0.6, and the outage lasts under 10 min 90% of the
   time. Hardened ships come out lower.
7. Replays hold; no allocation per hit.

### 5.4 Knobs for the designer and the side

| Knob | Effect | Default |
|---|---|---|
| Turret ring protection: splinter skirt or ring guard | debris-jam share ×0.5 | from about 1930 |
| Barbette carried up to the gunhouse, no exposed ring | the "exposed" share goes to "deck" | by design |
| Welded hull skin | seep ×0.6 | USN about 1940; partial elsewhere |
| Shock-hardened equipment | the era column moves to "1943–" | USN late 1942, RN 1941 (breakers) |
| Electrical doctrine: breakers locked in | local trips ×0.5, cascade ×1.5 | RN and USN 1939–42 |

---

## 6. Open questions

- **Campbell's *Jutland*** is not online. It would give every turret and belt hit on both sides, and so the
  denominators. The turret base table's no-effect column is the softest number here.
- **Target trials**: Baden 1921, Monarch 1925, Emperor of India 1931, BB-47 1924, Tosa. No turret-jam or belt-leak data
  was found online.
- **Measured shock from a shell striking armour.** None found. The Δv table is momentum ÷ mass.
- **German shock practice** (mounting of rangefinders and radar). The only data point is FuMO 23 failing to the ship's
  own guns.
- **The jam sub-locations need the class geometry**: the barbette's exposed height and the deck edge. Classes without
  them fall back to the mixed row (jam 0.18).

---

## Sources

**Primary and near-primary**
- Austro-Hungarian naval attaché's report on German ships after Jutland: https://www.navweaps.com/index_tech/tech-124.pdf · https://dreadnoughtproject.org/tfs/index.php/Austro-Hungarian_Naval_Report_on_German_Warships_after_Jutland
- WDR 57 South Dakota: https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html
- WDR 61 North Carolina: https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html
- WDR 24 Boise: https://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCL47/WarDamageReportCL47.html
- WDR 29 Savo cruisers: https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-quincy-ca39-astoria-ca34-vincennes-ca44-war-damage-report-no29.html
- WDR 26 San Francisco: https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-sanfrancisco-ca38-war-damage-report-no26.html
- WDR 1 Honolulu: https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-honolulu-cl48-war-damage-report-no1.html
- WDR 58 submarines, App. I: https://ibiblio.org/hyperwar/USN/rep/WDR/WDR58/WDR58-I.html
- Marblehead, Saratoga and Yorktown WDRs, and the British bomb damage summary to Sept 1941: https://www.ibiblio.org/hyperwar/USN/rep/WDR/
- River Plate dispatch (London Gazette): https://fepow.family/Supplement/London_Gazette/River_Plate_Battle/html/part_ii.htm · https://kbismarck.com/river-plate-battle.html
- Von Hase, *Kiel and Jutland*: https://www.wtj.com/archives/hase_03.htm · Semenov: https://www.wtj.com/archives/semenov_01.htm
- Tiger and Princess Royal at Jutland (captains' reports): https://dreadnoughtproject.org/tfs/index.php/H.M.S._Tiger_at_the_Battle_of_Jutland · https://dreadnoughtproject.org/tfs/index.php/H.M.S._Princess_Royal_at_the_Battle_of_Jutland
- Invincible at the Falklands, Admiralty damage report (quoted): https://www.tapatalk.com/groups/alltheworldsbattlecruisers/viewtopic.php?p=23270
- US Patent 2,491,657 (shock-proof breaker trip, 1943): https://patents.google.com/patent/US2491657A/en
- Sperry, USNI 1911: https://www.usni.org/magazines/proceedings/1911/september/sperry-gyroscopic-compass-naval-vessels
- Musser, "Lessons of Jutland affecting turret armor", USNI 1925: https://www.usni.org/magazines/proceedings/1925/april/lessons-jutland-affecting-design-turret-armor

**Analyses**
- Lundgren, South Dakota: https://navweaps.com/index_lundgren/South_Dakota_Damage_Analysis.php · Kirishima: https://www.navweaps.com/index_lundgren/kirishimaDamageAnalysis.php
- NavWeaps tech-092 (Kirishima's hit on SoDak): https://navweaps.com/index_tech/tech-092.php · tech-118 (turret foundations, clips): https://navweaps.com/index_tech/tech-118.php · tech-074 (FC gyros): https://www.navweaps.com/index_tech/tech-074.php · tech-016: https://navweaps.com/index_tech/tech-016.php
- INRO, Bismarck's final battle: https://www.navweaps.com/index_inro/INRO_Bismarck.php · USNI 1991, "Who sank Bismarck?": https://www.usni.org/magazines/proceedings/1991/june/who-sank-bismarck · kbismarck: https://kbismarck.com/bismarck-last-battle.html · https://kbismarck.com/proteccioni.html · https://www.kbismarck.com/prince-of-wales.html
- Lützow hit by hit: https://www.navygeneralboard.com/?p=1083
- Russo-Japanese War damage notes (Wilson, Campbell): http://www.gwpda.org/naval/rjwargun.htm
- Naval Gazing: https://www.navalgazing.net/Turret-and-Barbette · https://www.navalgazing.net/Norway-Part-8 · https://www.navalgazing.net/German-Guided-Bombs-Part-3
- Oryol: https://warhistory.org/article/the-sad-eagle-oryol
- Seydlitz damage: https://www.greatwarforum.org/topic/11843-seydlitz-battle-damage-from-jutland/ · https://weaponsandwarfare.com/?p=20101
- Forums (leads only): https://www.tapatalk.com/groups/warships1discussionboards/turret-armor-performance-at-jutland-t33459.html · https://nws-online.proboards.com/thread/3476/turret-hits-common

**Wikipedia ship articles** (used for the case tables): Von der Tann, New Zealand, Princess Royal, Tiger, Lion, Seydlitz,
Lützow, Derfflinger, Moltke, Markgraf, Grosser Kurfürst, König, Malaya, Peresvet, Fuji, Oslyabya, Retvizan, Tsesarevich,
Jean Bart, Dunkerque, Richelieu, Scharnhorst, Gneisenau, Battle of the North Cape, Bismarck, Tosa, USS Washington
(BB-47), Emperor of India.

**Project docs:**
- `08_gunfire_effects.md` §3.7, R5 and §8: the guesses this replaces.
- `09_damage_control.md`: holes, links, orifice flow, DC tasks.
- `11_blast_confinement.md`: the burst's splinters and blast, kept separate from shock here.
- `06_service_networks.md`: hydraulics and power to turrets.
- `fighting.md` 4.3a: what the sim does today.
