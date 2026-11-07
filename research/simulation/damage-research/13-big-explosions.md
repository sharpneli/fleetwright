# 13 — Big Explosions In and Beside Ships: Gas Through a Hull, the Girder, and Bombs
Status: draft    Updated: 2026-11-07    Request: [REQUESTS.md "Big explosions in and beside ships"](../../REQUESTS.md) (the plan: [docs/battle/explosions.md](../../../docs/battle/explosions.md))

*Six strands: how far an internal explosion's pressure reached, a belt pushed from inside, small ships broken, whipping, the largest charges, and bombs as projectiles. The record answers the first three. Whipping has no public full-scale data, so it is worked from physics in a reference script and checked against four cases.*

Tags as in the other damage docs: **[SRC]** sourced, **[INFERRED]** my reasoning or a fit, **[EST]** an engineering estimate with no direct source, **[UNCERTAIN]** thin or conflicting sources.

**Companion code:** [`big_explosions_ref.py`](big_explosions_ref.py) (pure Python, about 5 s). It covers: the gas's two clocks, a belt panel's hold and inertia, Cole's bubble, the hull's two-node mode, a whipping model and its one-cycle law, the gas whip against `HullGirder.Whip`, confined gas above 1 kg/m³, and bomb fall speeds. Every derived number below comes out of it.

---

## Summary

1. **Pressure stayed local. Gas and flame did not.**
   - **The record:**
     - In every wreck and trial found, what failed under pressure lay within one or two compartments of the seat, about 20-60 m.
     - Far structure stood: Arizona's bow forward of about frame 10, and her hull aft of No. 2 turret; Defence's mid-hull decks after her A magazine; Mutsu's 150 m forward piece; Vanguard's bow and stern; Invincible's two ends; Indefatigable's hull, cut just aft of X.
     - Low pressure and flame went much further. Baden's 15 in CPC smashed a storeroom bulkhead 100 ft (30 m) forward. Hood flashed from her engine-room vents. Defence's flame ran aft along her upper deck and lit the 7.5 in turrets.
   - **The physics** (ref §1): a pressure wave crosses a 200 m hull in 0.25-0.6 s, but what it does there depends on how much gas follows it. The fill time V/(Cd·A·c*) is 0.1-80 s for engine-room-sized spaces through 0.1-10 m² openings.
     - A release faster than the fill (a detonation, or a flash through a small opening) stays local. The far side sits at 0-7% of the source's peak.
     - A burn of a second or more through a large tear evens out before the source peaks.
   - **So the plan's "against" estimate** (gas crosses 200 m in 0.3-0.4 s, so it equalises) is the wave, not the gas. The network is the right model.
   - **No evidence was found of pressure equal through a hull.** [SRC cases; INFERRED physics]
2. **A belt pushed from inside is a heavy door, not a membrane.**
   - **What holds it:** the armour bolts tie plate and backing together, so they move as one panel. What holds the panel is its riveted edges at the decks. That comes to about 2.7-4.4 MPa for 229-343 mm strakes, about twice what the backing alone would hold as a membrane (1.8-2.1×) [EST joints]. It is nowhere near the armour's millimetres counted as plate (229 mm × 0.06 = 14 MPa).
   - **Its inertia:** its 2-3 t/m² hold it 20-70 ms before a joint opens 0.3 m (ref §2). So it is a wall to a shell's pulse and only a door to a magazine's burn.
   - **When it goes, the whole strake opens at once:** a plate's 10-25 m².
   - **Evidence:**
     - Hood's midships section is missing its starboard side down to the inner wall of the fuel tanks, plates curled outward [SRC].
     - At Arizona the belt "helped contain the blast and direct it upward", with plates splayed out 20 ft from her sides [SRC NPS].
     - On Baden, an internal burst displaced a belt plate, and the 1921 report asked for butt support between plates [SRC ADM 186/251].
   - **Not found:** the state of Queen Mary's and Invincible's belt plates. McCartney's surveys don't describe them.
   - **For the code:** candidate 1 ("a belt holds no pressure") is right to drop the armour's millimetres and about right on what is left. It is wrong on the opening's shape.
3. **Small ships broken.**
   - **The USN's destroyer report** (gunfire, bombs, kamikazes 1941-45): 30 lost, 6 by structural failure, all to bombs or kamikazes [SRC].
     - Brownson: two bombs amidships, and probably her own torpedo warheads.
     - Bush, Abele, Pringle, Little: kamikazes, jack-knifed. Abele was hit by a 1,200 kg Ohka warhead a minute after her keel broke.
     - Colhoun: sagged by the stern.
     - The report's own words: losses came from "usually multiple Kamikaze attacks which severely damaged the upper flange of the hull girder", and "there was no instance … in which gunfire damage seriously weakened the main hull girder of our destroyers".
   - **Time to break:** about 1 minute (Abele), 6 (Pringle), 15 (Brownson), 73 (Little); Bush hours after her first hit, when a heavy swell came on (the time isn't given).
   - **Survived:** Shubrick, with half her girder section gone (a 250 kg bomb under her bottom), and Mayrant, with circumferential buckling (500 kg, 5 ft off).
   - **Gunfire:**
     - **S35 at Jutland:** two heavy shells, probably Iron Duke's 13.5 in, broke her in two [SRC Campbell via Wikipedia].
     - **Friedrich Eckoldt:** broke in two within 2 minutes under Sheffield at 4 km, from a number of hits and a cause not recorded.
     - **The 1921 trials on V44 and V82:** 22 rounds of 4-6 in, damage to strength and buoyancy "not vital" [SRC]. But a 6 in HE burst inside her (V44 round 14) tore an 11 × 6 ft hole in her side, about 6 m².
   - **So a destroyer's girder goes to:** a heavy shell amidships, her own warheads or magazine, or repeated hits on her upper flange.
4. **Whipping: a law, with its range.**
   - **Bubble period:** Cole's T = 2.11·W^⅓/(D+9.8)^⅚ s matches Cheonan's observed bubble (0.985 s computed against 0.990 s observed) and DST's quarry tests (K 2.12) [SRC].
   - **Hull period:** the two-node frequency is the free-free beam with Kumai's virtual mass. Destroyer and Cheonan 1.2 Hz, cruiser and Lion 1.0 Hz, Yamato 0.46 Hz (uniform beam; real hulls 10-30% lower). Bubbles of 100-1,000 kg at 5-25 m run at 0.6-1.6 Hz: tuned to destroyers and cruisers, too fast for Yamato.
   - **The law (ref §5b):** the bubble's pressure ρ·V̈/(4πr), less the surface's image, projected on the mode. Over one cycle it gives M = EI·κ·ρ·ω·Vmax·T·g(ωT)·Infl/(4π·M*), with g tabulated. It peaks at f₂·T ≈ 0.5-1. It falls to a half at 10 m off her keel line and a seventh at 20 m. It vanishes at the mode's node, 0.22 L from an end. It is within -33%/+14% of the full model's first cycle (-60% at worst, 5 t at 40 m), and a second cycle adds up to 40%.
   - **Valid from γ = (distance to keel)/Rmax ≈ 1 out.** Closer, the bubble meets her bottom and vents into her: a local wreck, not a whip. That was Shubrick, Uganda and Roma's first Fritz X: holed, not broken. A bubble whose Rmax reaches its depth blows out after one expansion.
   - **Against the cases:**

     | Case | Design moments | Capacity | Outcome |
     |---|---:|---:|---|
     | Cheonan | 18 | ~3.5 | broke |
     | A Mk 48 frigate | 13 | | broke |
     | Ostfriesland's 2,000 lb near misses | 1.3 | ~2.5 | held, foundered |
     | Lützow's Tallboy | 0.9 | ~2.5 | held, settled |

   - **By the model** [INFERRED]: about 100 kg TNT within about 10 m under a destroyer's keel amidships breaks her; Lion needs about 300 kg at 10 m, Yamato about 1 t at 10-15 m. It is uncertain by ×2-3: no hull reflection (×1-2), a uniform beam, an incompressible near field.
5. **The largest charges.**
   - **Tirpitz, 12 November 1944** [SRC US technical report via kbismarck]: 29 Tallboys, 2-3 hits and about 7 near misses inside the net. One hit burst in a wing tank outboard of her port engine room. Her outer skin was torn from frames 98 to 132, about 34 m if the frames are metres [INFERRED], and a third of her breadth lost its shell and waterline armour at the port turbine room. Not broken. She took up to 17,000 t and capsized; C turret's magazine went up at about 60° of list and threw 1,000 t 25 yards.
   - **Tirpitz, Paravane:** a Tallboy through her bow burst in the water beside it. Her forward 100 ft were destroyed and the bottom ruptured for 50 ft further; about 2,000 t.
   - **Lützow:** a Tallboy about 18 m off tore a 1 × 10 m hole, and she settled in shallow water.
   - **Earlier tests:**
     - Ostfriesland 1921: three 2,000 lb bombs very close; she sank about 20 minutes later, already flooded.
     - Monarch 1923: 944 kg TNT 12 m down and 2.3 m off a Nelson TDS replica; damaged and still watertight.
     - Washington 1924: three 1 t near misses, two torpedoes and 400 lb TNT aboard; she stayed afloat.
   - **No Grand Slam was dropped on a ship.**
   - **The blast fits above 1 kg/m³** (ref §7): a confined charge can afterburn fully only up to W/V = 0.37 kg/m³ (TNT), the room's oxygen. Above that, the extra energy is a fixed 3.9 MJ a m³ of air. Weibull's fit runs along that line within 25% from 0.3 to 10 kg/m³ (it holds above 1), then falls below it: by 1.5× at 30 and 2.8× at 300 kg/m³. The co-volume adds ×1.4 at 300.
6. **Bombs as projectiles:** a table by type (§6), with weight over diameter cubed (W/D³), filler share and the fuzes found. Fall speeds by release height come from a drag curve checked against Tallboy's 336 m/s (it reads -4%) and Grand Slam's ~320 m/s (+10%). Level releases at 3,000 m strike at 234-250 m/s and 73-74°; at 5,500 m, 295-327 m/s; a dive released at 600 m, about 150 m/s.

---

## 1. Gas through a hull

### 1.1 What the record shows

| Case | Charge, seat | What failed, how far | What stood | Source |
|---|---|---|---|---|
| **Baden, 1921 trials** (17 rounds, 15 in) | Round 3: 15 in CPC, 60/40 shellite, burst 6 ft past a casemate roof | ⅞ in casemate bulkhead blown down over 15 ft; bulkheads above and below the upper deck displaced; fans out; **a storeroom bulkhead smashed 100 ft forward** | the ship; "far-side damage from delay bursts not very important" | [SRC ADM 186/251 Art. 95-97] |
| Baden, round 11 | 15 in SAPC, 28 ft past 7 in armour | Upper deck torn 15 ft each way, a bulkhead blown down over 15 ft square, W/T room destroyed | blast "smaller than round 3's" | same |
| Baden, rounds 3 and 8 | | the upper deck between side and belt broken up | the report: she "could break her back on a long passage" | same |
| Baden, the report's rules | | "Riveted joints of great length cannot hold against a blast in a confined space"; a 13/16 in longitudinal bulkhead localised a non-delay shell, a ⅞ in one was pierced | | same |
| **Arizona, 1941** | forward magazines | interior destroyed from about frame 10 (40 ft from the bow) to No. 2 turret; forecastle and 5 in main deck gone; sides "splayed out over 20 feet"; No. 1 turret dropped 15 ft as its supports collapsed | bow forward of frame 10 "relatively intact"; little damage aft of the blast zone | [SRC NPS Submerged Cultural Resources Study, ch. III] |
| **Defence, 1916** | A magazine, one major detonation (McCartney) | bow torn off at the forward windlass | **mid-hull decking intact**; the flame ran aft along the upper deck and lit the 7.5 in turrets, four roofs blown | [SRC McCartney, IJNA] |
| **Invincible, 1916** | Q magazine, about 50 t; McCartney argues a partial detonation (unburned cordite in Q) | broke in two after 70 m more way | two ends whole, each standing out of the water | [SRC McCartney 2013] |
| **Indefatigable, 1916** | X magazine | stern blown off just aft of X; the fore part reduced to pieces later by A's magazine | the stern section, 500 m off | [SRC McCartney 2017] |
| **Mutsu, 1943** | No. 3 turret's magazine | cut in two | a 150 m forward piece and a 45 m stern that floated about 14 h | [SRC] |
| **Hood, 1941** | aft 4 in magazine (18.5 t), then X and Y (about 49 and 45 t) [SRC Jurens 1987 via a summary] | stern detached; gas out "through the sides … forward and upwards via the engine room vents" | bow (missing just forward of A, probably a bending failure later) | [SRC] |
| Vanguard, 1917 | P and/or Q | "midships almost completely gone" | bow and stern | 07 §1 |

**Job 74 and Washington.**
- **Monarch, 1923-24:** after the underwater test (§5), live bombs from DH.9As "had little effect", and bombs were set off inside her [SRC Wikipedia, citing Brown, *Nelson to Vanguard*, pp. 20, 23-24].
- **Job 74:** a forum thread "The Job 74 Trials - from ADM 324/4" exists but couldn't be opened. Brown pp. 20-24 and ADM 324/4 are where the details are.
- **Washington, 1924:** 400 lb of TNT set off aboard, and she stayed afloat. Where it was set isn't given online [SRC].

**None of these shows pressure equal through the hull.** Each shows plating and bulkheads failing near the seat. Low pressures reached 30-100 m: Baden's storeroom, Hood's vents, Defence's flame.

### 1.2 Why: two clocks [INFERRED, ref §1]

The pressure wave crosses a 200 m hull in 0.59 s in cold air, or 0.25 s in hot products. The gas fills what's beyond only as fast as the openings pass it: τ ≈ V/(Cd·A·0.58·c).

| V m³ \ A m² | 0.1 | 0.5 | 2 | 10 |
|---|---:|---:|---:|---:|
| 100 | 4.1 s | 0.82 | 0.21 | 0.04 |
| 500 | 21 | 4.1 | 1.0 | 0.21 |
| 2,000 | 82 | 16 | 4.1 | 0.82 |
| 10,000 | 410 | 82 | 21 | 4.1 |

**A worked pair:** 400 m³ lit (a magazine and its handling room), with 10 t of cordite's energy, and a closed 2,000 m³ beyond a torn bulkhead. Each cell gives the source's peak / the far side's pressure at that moment, in MPa, then the time until they're within 20%:

| Released over \ opening | 0.5 m² | 2 m² | 10 m² |
|---:|---|---|---|
| 0.01 s | 3.12 / 0.00, 2.5 s | 3.09 / 0.01, 0.6 s | 2.93 / 0.04, 0.13 s |
| 0.05 s | 3.07 / 0.01, 2.5 s | 2.93 / 0.04, 0.6 s | 2.29 / 0.17, 0.15 s |
| 0.3 s | 2.84 / 0.06, 2.6 s | 2.16 / 0.19, 0.8 s | 0.78 / 0.47, 0.3 s |
| 1 s | 2.29 / 0.17, 3.0 s | 1.10 / 0.40, 1.2 s | 0.53 / 0.52, 0.4 s |
| 3 s | 1.37 / 0.35, 4.2 s | 0.55 / 0.52, 1.9 s | 0.52 / 0.52, 0.6 s |

- **What decides is the release time against the fill time, not the hull's length.** A detonation, or a flash through doors and scuttles, loads its own boundaries long before the next space has gas to push with. A slow burn through a wide tear equalises.
- **What the wrecks show fits a release fast against the openings:** a magazine's bags all alight in a few tenths of a second, then the near boundaries going in tens of ms and venting to the sea and air before the far ones are reached. The network (EX1-EX2) is the right model. The cascade's old merge into one volume was not.
- **Long passages and vents are the exception.** They are small openings between large volumes, so they carry flame and low pressure a long way slowly: Defence, Hood, Baden's storeroom.

## 2. A belt pushed from inside

### 2.1 How it is held [INFERRED, ref §2]

A belt plate is bolted through its backing (the side plating and its frames), so the two move outward together. What holds the panel is its backing's riveted joints to the decks above and below and to the plating round it, in shear. Typical joints [EST] give:

| Strake | Height | Armour | Backing | Rivets | Holds | Mass | Backing as a membrane (0.06 MPa/mm) |
|---|---:|---:|---:|---|---:|---:|---:|
| Destroyer side, no belt | 3.0 m | — | 10 mm | 1 × 20/m, 16 mm | 0.80 MPa | 78 kg/m² | 0.60 MPa |
| Light cruiser | 3.0 m | 76 mm | 15 mm | 2 × 18/m, 22 mm | 2.74 | 714 | 0.90 |
| Battlecruiser | 3.5 m | 229 mm | 25 mm | 2 × 16/m, 25 mm | 2.69 | 1,994 | 1.50 |
| Battleship | 3.8 m | 305 mm | 30 mm | 3 × 16/m, 25 mm | 3.72 | 2,630 | 1.80 |
| Battleship | 4.0 m | 343 mm | 40 mm | 3 × 16/m, 28 mm | 4.43 | 3,007 | 2.40 |

**Its mass delays it.** The time to move 0.3 m, the joint well open:

| Mass \ net pressure | 0.3 MPa | 1 MPa | 3 MPa |
|---:|---:|---:|---:|
| 80 kg/m² (a destroyer's side) | 13 ms | 7 | 4 |
| 800 kg/m² | 40 | 22 | 13 |
| 2,000 kg/m² | 63 | 35 | 20 |
| 2,900 kg/m² | 76 | 42 | 24 |

- **A shell's burst:** its gas leaves in a few ms through its own holes, before a heavy belt moves.
- **A magazine's burn:** it holds for hundreds of ms, and the belt is then the strake's riveted edges.
- **When those edges go,** a plate's area opens at once: 2.5-4.5 m high and 4-6 m long, 10-25 m².

### 2.2 The evidence

- **Hood:** her midships section lies inverted, its starboard side gone "down to the inner wall of the fuel tanks", plates "curling outward" [SRC]. The side, bulge and belt went outward as a unit. The inner wall held.
- **Arizona:** "the armor belt … apparently helped contain the blast and direct it upward", with "hull plates … splayed out over 20 feet from the ship's sides" and the blister cracked at 80 and 112 ft from the stem [SRC NPS].
- **Baden, round 8:** an APC through the top of her 10 in belt, bursting 7 ft on, "displaced a belt plate". The report asks for butt support between plates [SRC]. The plate moved as a body.
- **Not found:** Queen Mary's or Invincible's belt plates on the seabed. Their surveys don't describe them. Queen Mary's multibeam shows "armor plates" among her debris, without saying which [SRC battle-of-jutland.com].

## 3. Destroyers and cruisers broken

### 3.1 The USN destroyer report: the six structural losses of thirty [SRC BuShips, Destroyer Report, Gunfire, Bomb and Kamikaze Damage]

| Ship | Date | What hit her | What failed | Time to break or sink | Source |
|---|---|---|---|---|---|
| Brownson | 26 Dec 1943 | Two Vals: a 250 kg and a 63 kg bomb (about 370 lb of explosive) amidships, abaft the after stack, and a near miss | Main deck carried away from frames 86 to 135; bulkheads 110, 130½ and later 148 gone; "the bow and stern whipped like a springboard" (8-10 in at the bow). The report blames "one or more of her own torpedo warheads" | bow under about 15 min after the hits | report |
| Bush | 6 Apr 1945 | Kamikazes at 15:15 (forward engine room), 17:25 (between the stacks, "nearly severing the ship") and 17:45 | "caved in amidships" in an unusually heavy swell | after the third; hours after the first (not given) | report; Wikipedia |
| Mannert L. Abele | 12 Apr 1945 | A kamikaze in her aft engine room (her keel broken), then an Ohka's 1,200 kg warhead at the forward fireroom a minute later | "broke in two, her midship section obliterated" | at once | Wikipedia |
| Pringle | 16 Apr 1945 | A kamikaze at the base of No. 1 stack, its bomb (1,000 lb, or two 500s) through the decks | keel buckled, split at the forward fireroom | 6 min | Wikipedia |
| Little | 3 May 1945 | Four kamikazes in 4 min | keel broken, midships demolished, three machinery spaces open | broke up 73 min later | Wikipedia |
| Colhoun | 6 Apr 1945 | Four kamikazes and their bombs; one bomb broke her keel and tore a 20 × 4 ft hole | "sagged by the stern" | scuttled by gunfire | report; Wikipedia |

**The report's own reading:**
- The six losses came from "usually multiple Kamikaze attacks which severely damaged the upper flange of the hull girder". Danger is critical with "extensive buckling or rupture in the mid-length of both sheer strakes and stringers", and then "survival or loss is … determined by the extent of flooding".
- Newer, deeper classes had more reserve.
- "There was no instance in the recent war in which gunfire damage seriously weakened the main hull girder of our destroyers."

**The ones that lived:**
- **Shubrick (1943):** a 250 kg GP bomb (about 300 lb of explosive) through her deckhouse, bursting 5-6 ft below her bottom. About half her girder's effective section destroyed, "progressive failure would likely have followed in heavy weather". She survived.
- **Mayrant (1943):** a 500 kg near miss about 5 ft off. "Circumferential buckling seriously impaired the strength of the hull". Towed in.
- **Aaron Ward (1943):** a 250 kg SAP in her after engine room. Foundered 6 h later with no clear girder failure.

### 3.2 Gunfire

- **S35, Jutland (19:15 GMT):** "two heavy shells, probably from a salvo fired by … Iron Duke's main armament" (13.5 in). "Broke in two and sank", with V29's survivors aboard [SRC Wikipedia, citing Campbell 1998].
- **Friedrich Eckoldt, 31 December 1942:** taken for Hipper and fired on by Sheffield at 4,000 m "with every gun she had". "Broke in two" and sank with all hands in under two minutes [SRC Wikipedia, citing Kappes]. The number of hits and the cause aren't recorded.
- **The British trials on V44 and V82, 1921** [SRC ADM 186/251 Art. 88-90]: 22 rounds of 4 in, 4.7 in and 6 in, heeled to represent 6,400-9,000 yd. Damage to strength and buoyancy "was not vital", the hits all above water.
  - The worst bursts were inside her: V44 round 14, a 6 in HE bursting 1 ft in, in a W/T compartment, tore an **11 × 6 ft (6.1 m²) hole in her side** and a 9 × 4 ft hole in the engine-room bulkhead, and flooded it.
  - V82 round 7, a 4.7 in HE in a wing compartment, tore her plating over 7 × 3 ft.
  - "Blast in wing compartments was greater than elsewhere", worse with the tanks full. "The 4 in. shell caused noticeably less damage than the 4.7 in."
- **The pattern** [INFERRED]: guns of 6 in and under hole a destroyer and flood her but don't break her girder. A heavy shell amidships can (S35). Eckoldt fits either a heavy share of 6 in hits or, by the USN's pattern, her own torpedo warheads or a magazine.

**Not found:** cruisers broken by gunfire or bombs alone, apart from magazine and torpedo cases (in 03 §2.6).

## 4. Whipping

### 4.1 The bubble [SRC]

- **Reid (DSTO-GD-0109) and Swisdak:** T = K·W^⅓/(D+9.8)^⅚ s, with K 2.11 for TNT (2.064 in Reid's table, 2.30 for HBX-1). Rmax = J·(W/(D+9.8))^⅓, J 3.50 (3.38-3.52).
- **DST's 12 m cylinder tests** measured K₁ 2.12. Similitude held for γ = R/Rmax > 1.67. Whipping was strongest with the charge amidships and the bubble's frequency at the hull's (7.2 against 6.8 Hz). It was 71% weaker with the charge off the mode's node.
- **Cheonan:** an observed bubble period of 0.990 s, against 0.985 s from Cole for 136 kg at 8 m (Kim & Gitterman's fit; the official report has 250 kg at 6-9 m).
- **Rayleigh's collapse gives Cole's law:** T = 1.83·Rmax·√(ρ/p∞) → 2.04·W^⅓·Z^-⅚ [INFERRED].

| W kg TNT | 5 m | 10 m | 20 m | 40 m |
|---:|---|---|---|---|
| 25 | 0.65 s / 4.2 m | 0.51 / 3.8 | 0.36 / 3.3 | 0.24 / 2.8 |
| 100 | 1.04 / 6.6 | 0.81 / 6.0 | 0.58 / 5.2 | 0.38 / 4.4 |
| 300 | 1.50 / 9.5 | 1.17 / 8.7 | 0.83 / 7.6 | 0.54 / 6.4 |
| 1,000 | 2.23 / 14.3 | 1.75 / 12.9 | 1.25 / 11.3 | 0.81 / 9.5 |
| 2,400 | 2.99 / 19.1 | 2.35 / 17.3 | 1.67 / 15.1 | 1.09 / 12.7 |
| 5,000 | 3.82 / 24.4 | 3.00 / 22.1 | 2.13 / 19.3 | 1.39 / 16.3 |

### 4.2 The hull's two-node mode

Kumai's N = 3.07×10⁶·√(I/(Δv·L³)) cpm, with Δv = Δ·(1.2 + B/3T) [SRC recalled: Kumai 1958; Kyushu Univ. paper on the general form]. This is exactly a uniform free-free Euler beam carrying her virtual mass (ref §4).

| Ship | L | Δ | Δv | I (the code's) | f₂ | Period |
|---|---:|---:|---:|---:|---:|---:|
| Cheonan [I EST] | 88 m | 1,220 t | 2,866 t | 1.1 m⁴ | 1.21 Hz | 0.83 s |
| Destroyer | 110 m | 2,000 t | 4,495 t | 3.3 m⁴ | 1.20 Hz | 0.83 s |
| Light cruiser [EST] | 170 m | 10,000 t | 22,000 t | 40 m⁴ | 0.98 Hz | 1.02 s |
| Lion | 213 m | 26,000 t | 59,057 t | 210 m⁴ | 0.98 Hz | 1.02 s |
| Yamato | 256 m | 65,000 t | 159,042 t | 216 m⁴ | 0.46 Hz | 2.17 s (0.59 Hz on shipgen's I of 356) |

Shear, thin ends and superstructure take 10-30% off a uniform beam's frequency [INFERRED]. The code's single "1 s whip period" fits Lion, but not a destroyer (0.8 s) or Yamato (2 s).

### 4.3 The model and its law [INFERRED, ref §5]

**The model:**
- The pressure on her bottom is the incompressible source's, p = ρ·V̈/(4πr), less the free surface's image.
- It is summed over her flat of bottom against her two-node mode φ, normalised to RMS 1, which gives Infl.
- The bubble is Rayleigh-Plesset sized to Cole's Rmax. It keeps 55% of its energy at each minimum (Cole's second period, K 1.73).
- 3% damping. No hull reflection; a rigid wide bottom would double the pressure.
- The pressure integrates to nearly nothing over a cycle. What bends her is the share of it at her own frequency.

**The one-cycle law, for the code:**

```
Infl  = Σ φ(x)·(1/r − 1/r′)·dA over her flat of bottom      (≈ φ_mid·B·2·ln(h′/h) when h, h′ ≪ L/4: up to 2-3× high)
J*    = ρ·ω²·Vmax·T·g(ω·T)·Infl/(4π)                        the mode's impulse
M     = E·I·κ_mid·J*/(M*·ω)                                 κ_mid = φ″_mid·(4.73/L)², M* her virtual mass
```

h is the charge's distance below her keel, lateral offset included, and h′ = D + T is the image's. g is the bubble's normalised volume history taken at her frequency:

| f₂·T | 0 | 0.25 | 0.5 | 0.75 | 1.0 | 1.25 | 1.5 | 2.0 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| g | 0.605 | 0.574 | 0.486 | 0.362 | 0.227 | 0.107 | 0.021 | 0.040 |
| g·ωT (what scales M) | 0 | 0.90 | 1.53 | 1.70 | 1.43 | 0.84 | 0.19 | 0.50 |

**Against the full model's first cycle** (design moments; law with Infl summed / law in the ln form / full model):

| Ship | Charge | Law (Infl summed) | Law (ln form) | Full |
|---|---|---:|---:|---:|
| Destroyer | 100 kg, 10 m | 12.9 | 17.4 | 12.2 |
| Destroyer | 300 kg, 10 m | 11.2 | 15.0 | 14.3 |
| Destroyer | 2,400 kg, 25 m | 9.6 | 19.3 | 12.6 |
| Lion | 300 kg, 10 m | 7.7 | 16.4 | 6.8 |
| Lion | 1,000 kg, 15 m | 5.2 | 7.9 | 7.2 |

The worst gap is 5,000 kg at 40 m under the destroyer: 2.7 against 6.7. The second cycle adds up to 40% where the hull is tuned to it.

**The full model, under her keel amidships.** Design moments for depths 6 / 10 / 15 / 25 / 40 m; v marks a bubble that breaks the surface (one expansion, no collapse):

| | 100 kg | 300 kg | 1,000 kg | 2,400 kg | 5,000 kg |
|---|---|---|---|---|---|
| Destroyer (capacity 4.1) | 10.8v / 14.9 / 6.8 / 1.9 / 0.5 | 19.9v / 13.0 / 15.5 / 6.9 / 1.4 | 34v / 22v / 22 / 8.9 / 5.5 | 52v / 34v / 22v / 17 / 5.6 | 78v / 48v / 30v / 22 / 8.0 |
| Light cruiser (2.8) | 4.3v / 5.3 / 2.5 / 1.0 / 0.2 | 9.2v / 12.7 / 9.1 / 3.0 / 0.9 | 16v / 12v / 8.4 / 9.9 / 3.3 | 24v / 17v / 13v / 7.3 / 7.6 | 34v / 24v / 18v / 19 / 5.7 |
| Lion (2.6) | 1.5v / 3.8 / 1.9 / 0.8 / 0.2 | 3.3v / 9.2 / 7.0 / 2.6 / 0.9 | 5.9v / 8.4v / 6.5 / 8.5 / 3.2 | 8.8v / 12v / 9.7v / 6.3 / 7.3 | 12v / 17v / 14v / 16 / 5.5 |
| Yamato (2.6) | 0.2v / 0.4 / 0.2 / 0.1 / 0.0 | 0.5v / 1.5 / 0.8 / 0.3 / 0.1 | 1.4v / 2.5v / 2.7 / 1.3 / 0.4 | 2.6v / 5.1v / 3.2v / 3.0 / 1.2 | 3.8v / 8.4v / 5.7v / 7.6 / 2.3 |

**Offset and station** (destroyer, 300 kg at 10 m):
- By offset from her keel line, 0 / 5 / 10 / 20 / 40 m: 13.0 / 10.7 / 6.4 / 1.9 / 0.3.
- Along her length, by station 0.5 / 0.4 / 0.3 / 0.22 / 0.15 / 0.05: 13.0 / 11.0 / 5.6 / 0.3 / 6.3 / 11.2.
- At an end the moment is still large in mode terms, but the section there carries little. The girder check reads it by station.

**The cases:**

| Case | γ | Design moments | Capacity | Outcome |
|---|---:|---:|---:|---|
| Cheonan 2010: 136 kg at 8 m, 5 m off [SRC] | 1.04 | 18 | ~3.5 | **broke in two** |
| Cheonan, the official 250 kg at 7 m, 3 m off | 0.59 | 18v | | |
| Mk 48 (295 kg PBXN-103, about 450 kg TNT in bubble energy [EST]) about 8 m under a 2,700 t frigate: Torrens 1999, Ingraham [SRC] | 0.81 | 13 | ~3.5 | **broke** |
| Ostfriesland 1921: 2,000 lb (about 500 kg) about 5 m off her side [EST place] | 1.73 | 1.3v | ~2.5 | **held**, foundered |
| Lützow 1945: Tallboy (about 3,400 kg TNT in bubble) 18 m off, in shallow water | 1.31 | 0.9v | ~2.5 | **held**, settled |
| Shubrick 1943: 136 kg 1.5-2 m under her bottom | 0.28 | (n/a) | | survived, half her section lost locally |
| Uganda 1943: Fritz X 320 kg just under her keel, 1,300 t in | 0.16 | (n/a) | | not broken |
| Roma 1943: the first Fritz X under her keel, "damaging the hull girder" [SRC] | ≪1 | (n/a) | | not broken by it |

**Reading:**
- **γ < 1:** the bubble meets her bottom and vents into the hole. The pulsation that whips is spent: a local wreck, as Shubrick's, Uganda's and Roma's were.
- **γ ≈ 1-2, under the keel:** the influence pistol's design point and the worst case. Both ships that broke sat there.
- **Well off her side:** holes and shock, the girder under capacity.
- **Contact against under-keel:** a contact torpedo or mine gets the near field and the gas (EX4-EX5's hole and its whip from the jet). An under-keel burst at γ ≈ 1-2 gets this law. The two meet at γ ≈ 1.

**Uncertainty, ×2-3 either way:**
- Hull reflection, ×1-2.
- The uniform beam.
- The incompressible near field at γ ~1, where the bubble also jets.
- The ships' I where no figure was given (Cheonan, frigates).
- Ostfriesland's and Lützow's distances.
- No public full-scale whipping measurement was found. FSST results are restricted.

### 4.4 The gas whip, for comparison (ref §6)

An internal burst in a closed space puts no net force on the hull. Only gas venting one way does: up through the decks, or down into the water. Taken as a kick J amidships through the same mode, the moment is 1.27-1.29× the code's `HullGirder.Whip` (J = √(2kEm) on a uniform moment), for every ship and charge. So the code's form has the mode's scaling. `k` 0.2 is the share of the gas's momentum that leaves vertically, in phase, and 1.28 is the mode's shape.

## 5. The largest charges

| Case | Charge | Where | What it did | Source |
|---|---|---|---|---|
| **Tirpitz, Paravane, 15 Sep 1944** | Tallboy (5,400 kg, 2,400 kg Torpex) | through her foredeck and bow, bursting in the water to starboard of it | forward 100 ft destroyed, bottom ruptured or buckled 50 ft further; about 2,000 t in; near misses "buckled some of her hull plates and bulkheads" | [SRC Wikipedia citing Bishop; kbismarck tech. report] |
| **Tirpitz, Catechism, 12 Nov 1944** | 29 Tallboys, about 46 t inside the net | No. 2 confirmed: through all decks and armour, burst in a wing tank outboard of the port engine room, by the torpedo bulkhead. Probables near Bruno and abaft Caesar. About 7 near misses to port, "2 or 3 very close" | outer skin torn away frames 98-132 [about 34 m if German frames are metres, INFERRED]; a third of her breadth lost its shell and waterline armour at the port turbine room; starboard and aft of frame 98 untouched; up to 17,000 t; Caesar's magazine at about 60° of list threw about 1,000 t about 25 yd; capsized to 135° | [SRC US report via kbismarck] |
| **Lützow, 16 Apr 1945** | Tallboys (7) and 1,000 lb bombs | a Tallboy between her and the shore, about 60 ft off | a 1 × 10 m hole; she settled upright against the shore, main deck 2 m above water | [SRC Wikipedia citing Prager; the 60 ft from kbismarck's report] |
| Admiral Scheer, 9 Apr 1945 | RAF bombs (5 Tallboy hits claimed) | at Kiel | capsized | [UNCERTAIN, uncited] |
| **Ostfriesland, 21 Jul 1921** | six 2,000 lb bombs, no hits | "three detonated very close to the hull" | sinking fast by the stern 11 min after, rolled over and sank about 20 min after; already 5 ft down by the stern | [SRC Wikipedia] |
| **Monarch, 1923** | 944 kg TNT (for a 4,000 lb bomb) | 12.2 m down, 2.3 m off a replica of Nelson's side protection | "damaged but stayed watertight" | [SRC Wikipedia citing Brown] |
| **Washington, Nov 1924** | three 1 t bombs (near misses), two 400 lb torpedoes, then 400 lb TNT aboard | | minor damage, 3° list; afloat | [SRC Wikipedia] |
| Warspite, Salerno 1943 | Fritz X (320 kg amatol) | through six decks into No. 4 boiler room, and a near miss holing her at the waterline | about 5,000 t, all power lost | [SRC] |
| Savannah, 1943 | Fritz X | through C turret's roof, burst in the lower handling room | 197 dead; saved | [SRC] |

### 5.1 Against the code's near field [INFERRED]

- **Tirpitz:** Torpex at about 1.3-1.5 TNT in blast [UNCERTAIN] makes a Tallboy about 3,100-3,600 kg TNT: W^⅓ ≈ 15. `NearField`'s water hole, 0.668·W^⅓, is a 10 m radius. A burst in a wing tank (water-backed) that takes 34 m of skin together with 2-3 very close near misses is the same order.
- **Lützow:** 18 m off is past the reach (0.7·W^⅓ ≈ 10.5 m), so the near field gives no hole. The record has a 1 × 10 m tear. Her shock factor √W/R ≈ √3,400/18 = 3.2 is far past any hull-rupture band. So her hole is the shock's (`NearMiss.Shock`'s), and the near-miss rules must make it for a Tallboy as they do for a shell.
- **Monarch:** 944 kg at 2.3 m is inside the water hole's radius (0.668·W^⅓ = 6.6 m), so the replica's outer layers holed. "Watertight" is the holding bulkhead's.

### 5.2 Do the blast fits hold above 1 kg/m³? (ref §7)

A confined TNT charge's quasi-static pressure is (γ−1) × (its detonation energy + the afterburn the room's oxygen allows) / V.
- **Full afterburn** needs 0.74 kg O₂ a kg. Air holds 0.276 kg/m³, so the afterburn is whole only up to **W/V = 0.37 kg/m³**.
- **Above that,** the extra is a fixed 3.9 MJ a m³ of air.
- **Weibull's fit,** psi = 2,410·(W/V lb/ft³)^0.72, against that line:

| W/V kg/m³ | Thermo line | Weibull | Ratio |
|---:|---:|---:|---:|
| 0.1 | 0.42 MPa | 0.43 | 0.97 |
| 0.37 | 1.54 | 1.10 | 1.40 |
| 1 | 2.37 | 2.26 | 1.05 |
| 1.6 | 3.14 | 3.16 | 0.99 |
| 3 | 4.95 | 4.97 | 0.99 |
| 10 | 14.0 | 11.8 | 1.18 |
| 30 | 39.7 | 26.1 | 1.52 |
| 100 | 130 | 62 | 2.09 |
| 300 | 387 | 137 | 2.83 |

- **So the fits hold to about 10 kg/m³** (a Tallboy in a 1,500 m³ engine room is 1.6-2.4). Above that, the power law reads low and the linear line is the physics. The co-volume (about 1 L/kg) adds ×1.03 at 30 and ×1.4 at 300 kg/m³.
- **The kink at 0.37 matters more than the high end:** the oxygen sets the afterburn there, not a fixed share η.
- **Measured:** confined detonations in reactive and inert atmospheres (Eriksson & Vretblad 1994, 0.1-4 kg/m³) say the pressure goes by the energy that burns, which depends on oxygen. Their curve wasn't readable online.

## 6. Bombs as projectiles

### 6.1 The table

W/D³ is mass over diameter cubed: a shell's is about 15,000-16,000 kg/m³ (0.55-0.6 lb/in³).

| Bomb | Period | Type | Mass | Filler | Share | Diameter | W/D³ kg/m³ | Fuzes | Source |
|---|---|---|---:|---|---:|---:|---:|---|---|
| US AN-M57 | 1940-45 | GP | 116-118 kg | 56-59 kg TNT/amatol | 49% | 277 mm | 5,400 | nose and tail; inst. or delay | [SRC ww2data, OP 1280 lineage] |
| US AN-M64 | 1940-45 | GP | 231-243 kg | 117-124 kg | 51% | 361 mm | 5,100 | nose impact, tail delay (M124/M132/M133), hydrostatic Mk 230 | [SRC; bulletpicker] |
| US AN-M65 | 1940-45 | GP | 438-472 kg | 240-270 kg | 55-57% | 478 mm | 4,100 | as above | [SRC] |
| US AN-M66 | 1940-45 | GP | 930-971 kg | 482-537 kg | 50-53% | 592 mm | 4,500 | nose, tail delay M125/M134, hydrostatic | [SRC] |
| US AN-M56 | 1940-45 | light case | 1,860-1,920 kg | 1,470-1,525 kg | 79% | 864 mm | 2,900 | | [SRC] |
| US AN-M58 | 1940-45 | SAP | 214-224 kg | 73 kg TNT | 33% | 300 mm | 8,100 | tail delay | [SRC] |
| US AN-M59 | 1940-45 | SAP | 451 kg | 133-146 kg | 30-32% | 384 mm | 8,000 | tail delay or impact; nose usually plugged | [SRC] |
| US AN-Mk 33 | 1940s | AP | 458 kg | 63.6 kg Expl. D | 14% | ~305 mm [EST] | ~16,000 | AN-Mk 228 tail | [SRC] |
| US AN-Mk 1 | 1940s | AP | 726 kg | 97.7 kg Expl. D/TNT | 13% | ~356 mm [EST] | ~16,000 | AN-Mk 228 tail only | [SRC] |
| German SC 250 | 1936-45 | thin case | 245-256 kg | 130 kg | 52% | 368 mm | 5,000 | electric side fuze | [SRC Wikipedia] |
| German SC 1000 | 1936-45 | thin case | 1,000 kg | — | | 660 mm | 3,500 | side | [SRC airpages] |
| German SC 1800 | 1940-45 | thin case | 1,800 kg | — | | 660 mm | 6,300 | side | [SRC] |
| German PC 1000 | 1939-45 | AP | 1,000 kg | 160 kg TNT | 16% | 508 mm | 7,600 | | [SRC Wikipedia] |
| German PC 1400 | 1940-45 | AP | 1,400 kg | 300 kg | 21% | 559 mm | 8,000 | | [SRC] |
| Fritz X (PC 1400 X) | 1943-44 | guided AP | 1,570 kg | 320 kg amatol | 20% | (body 559) | | | [SRC; pierces 130 mm] |
| Japanese Type 99 No. 25 | 1939-45 | ordinary | 250 kg | 60 kg | 24% | — | | | [SRC Wikipedia list] |
| Japanese Type 98 No. 25 | 1938-45 | land | 241 kg | 96 kg | 40% | — | | | [SRC] |
| Japanese No. 50 Model 2 | | ordinary | 490 kg | 207.5 kg | 42% | — | | | [SRC] |
| Japanese Type 99 No. 80 Mk 5 | 1939-45 | AP (from a 41 cm shell) | 744-797 kg | 22.8-30 kg | 3-4% | ~450 mm [EST] | ~8,700 | | [SRC] |
| Japanese Type 3 No. 150 Mk 5 | 1943-45 | AP | ~1,500 kg | ~91 kg | 6% | — | | | [SRC] |
| British Tallboy | 1944-45 | deep penetration | 5,400 kg | 2,360 kg Torpex D1 | 44% | 965 mm | 6,000 | three No. 58 tail pistols (inertia), delays as set: 30 s-30 min for land targets; No. 47 long delays | [SRC Wikipedia] |
| British Grand Slam | 1945 | deep penetration | 10,000 kg | 4,200-4,300 kg Torpex | 42-43% | 1.17 m (0.94 m in one passage) | 6,200 | 11 s; 30 min; 1 h; 6-144 h | [SRC] |
| 1921 US bombs on Ostfriesland | 1921 | GP | 100, 270, 450, 910 kg | — | | | | | [SRC] |

**Not reached:**
- The British SAP and AP series (250 lb, 500 lb, 2,000 lb AP). AP 1661B and TM 9-1985-1 are where they are.
- The German SD types' fillers.
- WWI naval bombs (RNAS 65-520 lb, the 1,650 lb SN; German PuW).
- The fuzes' delays in seconds. The pages name the fuzes, not their times.

### 6.2 Fall [EST drag curve, ref §8]

A streamlined bomb's drag on its section is 0.14-0.20 subsonic, rising to 0.45 through Mach 0.75-1.05. Level releases at 75 m/s, and a dive at 70° and 110 m/s released at 600 m:

| Bomb | From 3,000 m | From 5,500 m | From 6,700 m | Dive, 600 m |
|---|---|---|---|---|
| AN-M64 500 lb GP | 234 m/s, 74° | 295 m/s, 79° | 313 m/s, 80° | 151 m/s, 76° |
| AN-M59 1,000 lb SAP | 244, 74° | 313, 78° | 333, 79° | 153, 76° |
| AN-Mk 1 1,600 lb AP | 249, 73° | 324, 77° | 348, 79° | 154, 76° |
| No. 80 Mk 5 AP | 247, 73° | 319, 78° | 341, 79° | 153, 76° |
| SC 250 | 234, 74° | 296, 79° | 313, 80° | 151, 76° |
| PC 1400 | 247, 73° | 320, 78° | 343, 79° | 153, 76° |
| Tallboy | 249, 73° | 324, 77° | 349, 79° | 154, 76° |
| Grand Slam | 250, 73° | 327, 77° | 353, 79° | 154, 76° |

- **Checks:** Tallboy recorded 336 m/s (750 mph) from 18,000 ft, against 324 here. Grand Slam about 320 m/s at the Ashley trial from 22,000 ft, against 353 here; the trial's figure is "after release", which is ambiguous. So ±10%.
- **Heavy bombs' speed hardly depends on their mass** above about 3 km. Their limit is the transonic drag rise.

## Recommendations for this codebase

1. **Keep the network (`GasNetwork`), and add two anchors to the `explode` bench** (EX7):
   - Baden's round 3: a 15 in CPC in a 7 in casemate. Its storeroom bulkhead 30 m off goes, nothing structural far off.
   - A magazine's set-off: no structure fails past one or two spaces each way while she vents.

   The record supports locality by release time. A test that fails would point at an opening's size or a stow's release, not at a need to merge volumes.
2. **A belted side gives as one panel** (`Blast` / `Boundary.Take`, `HullFaces`):
   - Its rupture pressure is its backing's membrane value × about 2 [EST 1.3-3; 1.8-2.1 for heavy strakes]. The armour's millimetres never count as membrane, as now.
   - When it gives, it opens the strake's plate area at once (10-25 m², or the cell face's share of it), not `Opening`'s curve.
   - Before it gives, its mass holds it √(2·s·m″/P), 20-70 ms. Against a shell's pulse that keeps the belt whole. Against a magazine's burn it doesn't matter.
3. **Open item 6 ("12″ and 6″ HE never rupture a destroyer's side")** has a 1921 answer. A 6 in HE bursting inside her tore 6 m² of side, and a 4.7 in HE tore 2 m² from a wing compartment.
   - `NearField`'s contact rule, 0.45·W^⅓·√(6/t), gives about 2 m² for a few kg on 6-8 mm. So if the code never holes her side, look at where the burst lands, not the rule.
   - **Don't fit the girder to Eckoldt.** The USN's 6 of 30 losses structural, none by gunfire, and V44 and V82's "not vital" say 6 in gunfire shouldn't break destroyers by itself. EX4's 33% broken in two may be high for gun battles. Check `losses` by weapon.
4. **EX5's whip: the law in §4.3.**
   - It gives a moment in design moments, which `HullGirder.Read` already takes.
   - Its inputs: the charge, its depth and place, and her I, Δv, L and B, with g from its table or the ref's bubble.
   - γ < 1: no whip. The bubble vents into her hole, and that is the contact case's near field and gas.
   - Rmax ≥ depth: one expansion only [EST `VENT_SHARE` 1.0].
   - It retires `TorpedoBreakShare` and the `Held` switch.
   - Settings: the hull reflection factor 1 [EST 1-2], damping 3% [EST], the second cycle's 0.55 of its energy [SRC from Cole's K₂].
5. **Her two-node period from her class,** f₂ = 3.56·√(E·I/(m_v·L⁴)) Hz with m_v = 1000·Δv/L kg/m (§4.2), in place of the "1 s whip period" in `GasNetwork`'s fade. That is 0.8 s for a destroyer and 2 s for Yamato.
6. **A confined burst's afterburn by the cell's oxygen,** in place of a fixed share:
   - min(Q_ab·W, 14 MJ/kg O₂ × 0.276 kg/m³ × V_air), where V_air is the cell's air, not its stores.
   - The kink at 0.37 kg/m³ is a Tallboy's or a big shell's in a small space, and moves 11's fits by up to 40% there.
   - A co-volume term (about 1 L/kg) for a magazine's own gas above about 30 kg/m³.
7. **EX8's Tallboy bench:**

   | Case | Wanted |
   |---|---|
   | Tirpitz, Catechism: a hit in a wing tank at her port engine room | about 30 m of skin; not broken; capsizes by flooding with about 17,000 t, in her protected anchorage |
   | Tirpitz, Paravane: through her bow into the water beside it | her bow's 30 m wrecked, about 2,000 t |
   | Lützow: 18 m off in 7-8 m of water | a 10 m² tear from the shock; she settles |
   | Monarch: 944 kg at 2.3 m off a TDS | outer layers holed, the holding bulkhead whole |
   | Ostfriesland: three 2,000 lb within a few metres | she floods and goes in about 20 min |

8. **Bombs:** the table's rows are the catalogue: mass, filler and share, diameter, W/D³, fuze. Fall from release height and speed by the drag curve. A level bomb from 3-6 km strikes at 230-330 m/s and 73-79°, a dive bomb at about 150 m/s and 75°. That puts them in `Terminal` as slow, steep shells, AP bombs with a shell's W/D³ and GP bombs with a third of it.

## Open, not found

- **Job 74's internal tests on Monarch (1924):** the details are in Brown, *Nelson to Vanguard* pp. 20-24, and ADM 324/4. A Tapatalk thread transcribing it wouldn't open.
- **Washington's 400 lb:** where it was set.
- Queen Mary's and Invincible's belt plates.
- The fuzes' delays in seconds; British SAP and AP bombs; WWI bombs; German SD fillers.
- Any full-scale whipping measurement (FSST results are restricted); Cheonan's I and capacity; Ostfriesland's near misses' distances.
- Whether Kriegsmarine frame numbers were metres from the aft perpendicular (taken so for Tirpitz's 34 m).

## Sources

**Trials and wrecks**
- *Progress in Gunnery Material, 1921* (ADM 186/251): Baden Art. 94-97, V44 and V82 Art. 88-90, Nürnberg Art. 91-92: https://vif2ne.org/nvk/forum/files/Realswat/(160312234717)_adm186-251.pdf
- HMS Monarch (1911), citing Brown 2000: https://en.wikipedia.org/wiki/HMS_Monarch_(1911) ; the Job 74 thread (not opened): https://www.tapatalk.com/groups/warships1discussionboards/the-job-74-trials-from-adm-324-4-t43991.html
- USS Washington (BB-47): https://en.wikipedia.org/wiki/USS_Washington_(BB-47)
- NPS Submerged Cultural Resources Study, USS Arizona, ch. III: https://corpora.tika.apache.org/base/docs/govdocs1/434/434703.html
- McCartney, HMS Defence (IJNA): http://eprints.bournemouth.ac.uk/29393/3/HMS%20Defence%20ijna%20submission%20IJM%20final%20for%20BURO.pdf
- McCartney, Indefatigable and V4 (IJNA 2017): https://eprints.bournemouth.ac.uk/29391/5/ACCEPTED%20VERSION%20IJNA%20IJM%202017.pdf
- McCartney, HMS Invincible (Skyllis 2013): https://eprints.bournemouth.ac.uk/30305/1/2013-Skyllis-12-Heft%202-MCCartney%20HMS%20Invincible.pdf
- Queen Mary's wreck: https://battle-of-jutland.com/battleships/hms-queen-mary-wreck/ ; Divernet, "Big guns of Jutland": https://divernet.com/scuba-diving/general-wrecks/big-guns-of-jutland/
- HMS Hood, wreck and the Jurens/Garzke reading: https://exl.pages.dev/HMS_Hood_(51) ; Jurens 1987 summarised: https://utzoo.superglobalmegacorp.com/usenet/news100f1/b123/sci/military/1210.txt
- Mutsu: https://en.wikipedia.org/wiki/Japanese_battleship_Mutsu ; Roma: https://en.wikipedia.org/wiki/Italian_battleship_Roma_(1940)

**Small ships**
- BuShips, Destroyer Report, Gunfire, Bomb and Kamikaze Damage: https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html
- Mannert L. Abele: https://en.wikipedia.org/wiki/USS_Mannert_L._Abele ; Pringle: https://en.wikipedia.org/wiki/USS_Pringle_(DD-477) ; Bush: https://en.wikipedia.org/wiki/USS_Bush_(DD-529) ; Little: https://en.wikipedia.org/wiki/USS_Little_(DD-803) ; Colhoun: https://en.wikipedia.org/wiki/USS_Colhoun_(DD-801)
- Friedrich Eckoldt: https://en.wikipedia.org/wiki/German_destroyer_Z16_Friedrich_Eckoldt
- S35: https://en.wikipedia.org/wiki/SMS_S35

**Whipping**
- Reid 1996, DSTO-GD-0109, *The Response of Surface Ships to Underwater Explosions*: https://apps.dtic.mil/sti/pdfs/ADA326738.pdf
- DST Group RR-0451 (whipping of a free-free cylinder): https://www.dst.defence.gov.au/sites/default/files/publications/documents/DST-Group-RR-0451.pdf
- Cheonan's bubble (forensic seismology and BEM): https://link.springer.com/article/10.1007/s11804-013-1213-y
- Kumai's two-node formula (general form, Kyushu Univ.): https://catalog.lib.kyushu-u.ac.jp/opac_download_md/7157932/0309_p025.pdf
- HMAS Torrens: https://en.wikipedia.org/wiki/HMAS_Torrens_(DE_53) ; ex-Ingraham: https://www.twz.com/42129/watch-the-ex-uss-ingraham-frigate-get-its-back-broken-by-a-torpedo.md
- HMS Belfast's mine (keel broken, hogged 3 in): https://en.wikipedia.org/wiki/HMS_Belfast

**Large charges and bombs**
- Tirpitz technical report (US, via kbismarck): https://kbismarck.com/tirpitz-technical-report.html ; Operation Paravane: https://en.wikipedia.org/wiki/Operation_Paravane ; Operation Catechism: https://en.wikipedia.org/wiki/Operation_Catechism
- Deutschland/Lützow: https://en.wikipedia.org/wiki/German_cruiser_Deutschland ; Ostfriesland: https://en.wikipedia.org/wiki/SMS_Ostfriesland
- Tallboy: https://en.wikipedia.org/wiki/Tallboy_(bomb) ; Grand Slam: https://en.wikipedia.org/wiki/Grand_Slam_(bomb) ; Fritz X: https://en.wikipedia.org/wiki/Fritz_X
- US bombs: https://ww2data.blogspot.com/2018/08/american-projectiles-and-explosives.html ; https://bulletpicker.com/bomb_-1600-lb-ap_-an-mk-1.html ; https://www.bulletpicker.com/bomb_-1000-lb-ap_-an-mk-33.html ; https://www.bulletpicker.com/bomb_-1000-lb-sap_-an-m59_-an-.html ; https://www.bulletpicker.com/bomb_-2000-lb-gp_-an-m66.html ; https://www.bulletpicker.com/bomb_-500-lb-gp_-an-m64a1.html
- German bombs: https://airpages.ru/eng/lw/abg.shtml ; https://en.wikipedia.org/wiki/SC250_bomb ; https://en.wikipedia.org/wiki/PC_1000 ; https://en.wikipedia.org/wiki/PC_1400
- Japanese bombs: https://en.wikipedia.org/wiki/List_of_Japanese_World_War_II_navy_bombs
- Confined detonations at high loading density: Eriksson & Vretblad 1994: https://apps.dtic.mil/sti/tr/pdf/ADA529807.pdf ; Weibull's fit and UFC 3-340-02 as in 11's sources.
