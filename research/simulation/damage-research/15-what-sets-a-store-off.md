# 15 — What Sets an Explosive Store Off: Shock, Fragments, Heat, by Store
Status: draft    Updated: 2026-11-07    Request: [REQUESTS.md "What sets an explosive store off"](../../REQUESTS.md) (the code: [docs/battle/explosions.md](../../../docs/battle/explosions.md) EX6, `SimSettings.MagazineFlashChance`, `SimSettings.GasIgniteK`)

*Four questions: what blast and shock set off each store a warship carried, 1890-1945; what fragment does it; how long heat takes (cook-off, and propellant lit by hot gas); and whether a shell room goes up with its magazine. The record answers the last two best: the USN destroyer reports, Franklin's report and the Jutland wrecks. Lab sensitivity figures are thin for the period's fillers, so the thresholds are worked from a few anchors in a reference script and checked against the cases.*

Tags as in the other damage docs: **[SRC]** sourced, **[INFERRED]** my reasoning or a fit, **[EST]** an engineering estimate with no direct source, **[UNCERTAIN]** thin or conflicting sources.

**Companion code:** [`store_initiation_ref.py`](store_initiation_ref.py) (pure Python, under a second). It covers: air blast on a store's face against the gap tests (S1), shock in water by Cole (S2), fragment speeds by Gurney, case perforation by THOR and initiation by Held's v²d (F1-F4), propellant lit by hot gas, with a dose rule for the code (H1), and cook-off of a cased charge by its wall and its fire (H2). Every derived number below comes out of it.

---

## Summary

1. **Blast alone sets off nothing beyond contact.**
   - **What a filler needs:** a shock of 1-5 GPa. In lab gap tests, cast TNT needs 3.7 GPa and Comp B 2.0-2.1 [SRC NOLTR 62-41, ADP005315]. Big charges are about 3× more sensitive than the lab tests show: tritonal read 5.5 GPa in the standard test, 1.5 in the super gap test, and 1.1-1.2 GPa between full-scale Mk 82s [SRC].
   - **What air blast gives:** at 0.1 m/kg^⅓, 34 MPa incident and 0.3-0.7 GPa reflected; at 0.3 m/kg^⅓, 9 MPa and 0.07-0.19 GPa (ref S1). Even at 0.06 m/kg^⅓ that is at most a quarter of what cast TNT needs, and a tenth at ideal-gas reflection.
   - **So what passes a detonation to a neighbour is the donor's case or its fragments.**
     - Single Mk 82s: a tritonal acceptor detonated at 8 in (0.044 m/kg^⅓) and showed "essentially no explosive reaction" at 12 in (0.066) [SRC ADP004860]. Rounds in contact took about 6 GPa; with an air gap, about three times that, from the case wall striking the acceptor.
     - Stacks: Mk 84 stacks set each other off at 15-20 ft "mainly" by fragments, through the fuze wells and by orientation. Steel fuze-well plugs and nose-to-nose stowage stopped it [SRC ADP005352].
     - Storage practice: stacks in igloos survived 3-7 bar at K1.25-K2 (0.50-0.79 m/kg^⅓) "with no propagation to the acceptor munitions" [SRC DDESB IMD history].
     - 1900, wet guncotton warheads: 14 in of air "failed to offer any protection" between warheads, and ⅜ in of mild steel stopped the transfer [SRC USNI 1900].
   - **In water** (ref S2) a filler again needs contact. What fails further off is the firing gear. Depth-charge pistols came off their charges at 80-110 ft from a 300 lb charge [SRC Kirkby], about 6-9 MPa by Cole. That is countermining.
2. **Fragments set off thin-cased stores, not shells.**
   - **The rule:** Held's v²d over the filler's constant [SRC form; Comp B 16 and H-6 16.5 mm³/µs² SRC]. Apply it to the speed a fragment keeps after perforating the store's case (THOR) [INFERRED use]. The other fillers' constants are scaled from Comp B by their gap pressures squared [INFERRED], or estimated [EST]: cast TNT about 52, Type 97 and hexanite about 34, lyddite about 26, amatol about 95, Explosive D about 115.
   - **What the attackers bring** (ref F1; 13's fragments):
     - HE shell fragments: 750-950 m/s at about 25 g.
     - AP chunks: 370-580 m/s at kilograms.
     - GP bomb fragments: about 2,000 m/s.
     - A detonating warhead's or depth charge's own: 2,600-2,800 m/s.
   - **What it gives** (ref F3):

     | Attacker | AP and HE shells, 15-60 mm walls | Thin-cased stores: warheads, depth charges, mines, GP bombs, 5-10 mm |
     |---|---|---|
     | Shell fragments | never | only Torpex fillers |
     | Bomb fragments and a neighbour's own | only a 6 in HE's thin wall | yes (amatol depth charges: only a neighbour's) |

   - **The record agrees** [SRC USN destroyer reports]:
     - Warheads and depth charges "can be detonated by fragments of sufficient weight and velocity".
     - "The 5-inch AA projectile is the type most susceptible to mass detonations", being the thinnest-walled shell (0.6-0.9 of the threshold against a bomb's big pieces and a warhead's fragments in F3).
     - Brownson's bomb set off her after mount's warheads.
     - Suzuya's near-miss bomb set off her No. 1 torpedo mount [SRC combinedfleet].
     - 20 mm, 40 mm and 3 in rounds "merely detonate low order singly, or split open and burn".
   - **Propellant is lit, not detonated,** by any hot fragment that pierces its packaging. König's casemate charges, and the USN's magazine losses to "fragment attack or fire" [SRC].
3. **Heat: propellant lights in seconds in burst gas, but not in gas at 470 K.** Cased HE cooks off in minutes to tens of minutes.
   - **Propellant's surface lights at 190-227 °C (460-500 K),** almost whatever the heating rate [SRC AD0472135].
   - **What it takes the gas** (ref H1): 2,000 K gas lights a cordite surface in 0.04-0.16 s, 1,200 K in 0.4-6 s, 900 K in 1.4-40 s, 600 K in 15-800 s. Gas at 500 K takes hours.
   - **So `GasIgniteK` 470 K is the surface's temperature, not the gas's.** Read as a gas threshold, it lights charges far too easily. The dose rule in §3.1 matches the conduction solution within about 50%.
   - **Cook-off** (ref H2, an [EST] wall-heating model): checked against Forrestal's Comp B bomb at about 1.5 min (the model 1.6) and Franklin's "3-4 minutes, or not at all" [SRC]. In an engulfing fuel fire:

     | Store | Wall | Cook-off |
     |---|---:|---:|
     | Depth charges and warheads | 5-6 mm | 1-1.3 min |
     | GP bombs | 10 mm | 1.6-2.4 min |
     | 12 in HE | 30 mm | 6.5 min |
     | AP shells | 60 mm | 12 min |

     A compartment fire takes about 2.5× as long, hot smoke 8-13×.
   - **Aboard, in the record** [SRC]:
     - Depth charges went 23 and 26 min into deck fires (Mullany, Isherwood).
     - "No positive case of detonation of TNT loaded torpedo warheads by prolonged roasting". Air flasks burst instead (Kalk, Stormes).
     - Japanese oxygen torpedoes went within about an hour of fires: Mikuma by 13:58 after bomb hits from about 12:45; Suzuya's cascade at 10, then 70, minutes.
4. **Shell rooms didn't go up with their magazines.**
   - **The wrecks:**
     - **Defence:** unexploded rounds and cordite cases, some unopened, scattered in her stern, and unexploded rounds in her burned-out turrets [SRC McCartney].
     - **Invincible:** "several unexploded 12-inch rounds" in Q turret beside unburnt cordite, after a partial detonation that still broke her in two [SRC McCartney].
     - **Glatton:** her 6 in magazine went low order and burned. Her unflooded after magazines stood about two hours until her own side torpedoed her [SRC].
   - **The physics agrees:**
     - A cordite magazine gives MPa, not GPa.
     - Its debris is plating, not cased HE fragments at 2 km/s.
     - Its fire is out in seconds to a minute, against the 6-30 min a thick shell needs to cook off.
   - **Where shells did go in mass, the store was the exception:**
     - Thin-walled AA rounds: Chevalier's magazine, possibly a torpedo hit; Quincy's ready rounds.
     - A detonating neighbour: Rowan's depth-charge magazine, at once, by a torpedo.
     - Black powder in the chain: Arizona's theory.
   - **Hood's 4 in to 15 in was propellant to propellant by gas** (Jurens: 18.5 t, then X's 49 t and Y's 45 t), not shells [SRC summary].
   - **Not found:** what became of Queen Mary's, Hood's, Arizona's and Mutsu's shells.

---

## 1. Blast and shock

### 1.1 What fillers need [SRC where tagged]

| Filler | Navy, period, use | Gap test, 50% | Impact (2 kg) | Rifle bullet | 5 s explosion | Held K used (mm³/µs²) |
|---|---|---|---|---|---|---|
| Black powder | all navies' primers, igniters, saluting charges; 1890s shells | (not a detonating filler) | BM 32 cm [SRC] | ignites [EST] | 490 °C at 1 s, 356 °C at 10 s [SRC] | (lit by spark or flame) |
| Wet guncotton (15-25% water) | warheads and mines to ~1915 | won't detonate from a gap or bullet; its dry primer goes [SRC USNI 1900] | | 6-pdr shots through 1 in steel: "only the primer detonated" [SRC] | can't be lit while wet [SRC] | none |
| Picric acid (lyddite, Shimose, melinite) | RN common/HE 1896-1915, Japan to ~1930s, France | [EST] ~2.6 GPa, more sensitive than cast TNT | | | 322 °C [SRC recalled, AMCP 706-177] | 26 [INFERRED] |
| Shellite (picric with dinitrophenol) | RN AP from 1918 | "less sensitive" than lyddite [SRC] | | | | ~26-50 [EST] |
| Cast TNT | German Fp 02 1902-, USN and RN from ~1915, depth charges, mines, warheads | 129 cards, 3.7 GPa [SRC] | | | 475 °C [SRC recalled] | 52 [INFERRED] |
| Pressed TNT | boosters, small shells | 193-198 cards (≈ Comp B) [SRC] | | | | 17 [INFERRED] |
| Amatol 80/20, 60/40, 50/50 | RN and US WWI shells, bombs, depth charges, mines | booster sensitivity 0.60-0.83 in of wax, against Comp B's 1.40 [SRC] | BM 90-95 cm | 0 of 5 explode [SRC] | 265-280 °C, decomposes [SRC] | 95 [EST] |
| Explosive D (ammonium picrate) | USN AP shells 1906-45 and 5 in AA common | "insensitivity to shock" chosen for AP [SRC] | | | 318 °C [SRC recalled] | 115 [EST] |
| TNT/HND (Japan's Type 97 60/40; German hexanite) | IJN torpedoes, German torpedoes and mines | [EST] ~3 GPa | | | | 34 [EST] |
| Comp B, Torpex, H-6 | USN and RN WWII torpedoes, bombs, depth charges (Torpex, minol) | 201-213 cards, 2.0-2.1 GPa [SRC] | BM 75 cm | 3% explode, 13% partial, 4% burn [SRC] | 278 °C [SRC] | 16, 16.5 [SRC] |

- **Japan's Type 97 conflicts** [UNCERTAIN]. NavWeaps calls the 60/40 TNT/HND charge "very resistant to shock". Wikipedia calls it (as "Shimose type 97") "more prone to detonation from shock than TNT". HND is a tetryl-class booster explosive, so the mixture is put between TNT and tetryl.
- **Big charges are more sensitive than small ones** [SRC ADP004860, ADP005315]:
  - Between full-scale Mk 82s, tritonal went at 1.1-1.2 GPa, against 3-4 published.
  - The super gap test, made for 100-1,000 kg ordnance, reads tritonal at 1.5 GPa against the standard test's 5.5.
  - So a 300 kg warhead or a 500 lb depth charge needs about a third of its filler's lab pressure.

### 1.2 Air: blast doesn't reach a filler's threshold (ref S1)

| Z m/kg^⅓ | Incident MPa | Reflected MPa (ideal gas to real gas) | Against cast TNT's 3.7 GPa |
|---:|---:|---:|---:|
| 0.06 | 50 | 400-1,000 | 11-27% |
| 0.1 | 34 | 270-680 | 7-18% |
| 0.3 | 9 | 70-190 | 2-5% |
| 0.8 | 1.6 | 10-32 | <1% |
| 2.4 (K6) | 0.14 | | |

- **Kinney-Graham's fit runs through the contact region**, but the order of magnitude is what matters. Only the donor's case, flung across a gap of a few tens of cm, or its fragments carry GPa to a neighbour.
- **Mk 82s, as scaled distances:** detonated at 0.044 m/kg^⅓, no reaction at 0.066 [SRC distances]. That is a gap of about 1.5-2 of the bomb's own radii.
- **What blast does do to a store is move it, break it and open it.**
  - Franklin's unexploded bombs were found in the gallery's gun tubs. Her low-order bombs had consumed fillers and were missing base fuzes [SRC WDR 56].
  - Isherwood's bomb "opened two depth charge cases", and the fire then cooked them off in 26 min [SRC].

### 1.3 Water (ref S2)

| W kg TNT | 7 MPa | 20 MPa | 100 MPa |
|---:|---:|---:|---:|
| 30 | 18.5 m | 7.3 m | 1.8 m |
| 136 (a 300 lb DC) | 30.5 m | 12.1 m | 2.9 m |
| 300 | 39.8 m | 15.7 m | 3.8 m |
| 500 | 47.1 m | 18.6 m | 4.5 m |

- **Kirkby (IMechE):** a 300 lb pattern's charges lost their pistols "at ranges over about 110 ft" above the burst and about 80 ft elsewhere. By Cole that is 6-9 MPa. The upper layer was displaced sideways to stop countermining [SRC].
- **The filler's own GPa lies inside the formula's validity limit,** within about a charge radius. Underwater, as in air, a filler goes only at contact. What goes off at tens of metres is the firing gear.
- **For stores aboard a sinking ship, the hydrostatic pistol is the trigger.**
  - Strong broke in two. "Three or four of the 300-pound depth charges", set to 50 ft, went off and hurt survivors.
  - Abner Read's charges, "set and checked on 'safe'", didn't [SRC].
  - Hammann's underwater explosion as she sank killed many in the water: her depth charges and torpedoes are the likely cause [SRC Wikipedia; whether set safe not given].

### 1.4 What a struck store does when it doesn't go [SRC]

| Store | Struck, not set off |
|---|---|
| Shells in rooms and on hoists | nothing, or broken; AP chunks thrown (Defence's stern, Invincible's Q) |
| 20 mm-3 in fixed rounds | "detonate low order singly, or split open and burn" |
| 5 in powder tanks and cases | burn; "intense powder fires", scattered low-order shell bursts (Franklin's mount 7: "no high order single or mass detonations"; fire didn't travel down to the lower handling room) |
| Torpedoes | air flasks burst (Kalk, Stormes); a burning warhead's explosion "not indicative of a high order detonation" (Downes); a hit on a mount, nothing (Ralph Talbot) |
| Depth charges | cases opened, then burned and cooked off (Isherwood) |
| GP bombs | broken and burned: at least 40% of Franklin's went low order |
| Wet guncotton | nothing (1900: "without exploding or even igniting") |
| Black powder | lit |

## 2. Fragments

### 2.1 The rule (ref F2-F3)

A fragment of m g at v must first perforate the store's case: THOR, as in 13. What it keeps, v_r = √(v² − V50²) [EST, Lambert-Jonas with p 2], must give v_r²·d ≥ K, with d its size. Bare filler, the speed needed:

| Filler (K) | 1 g | 10 g | 100 g | 1 kg |
|---|---:|---:|---:|---:|
| Comp B, Torpex (16) | 1,780 m/s | 1,215 | 830 | 565 |
| Pressed TNT (17) | 1,840 | 1,250 | 855 | 580 |
| Lyddite (26) | 2,270 | 1,550 | 1,055 | 720 |
| Type 97, hexanite (34) | 2,600 | 1,770 | 1,210 | 820 |
| Cast TNT (52) | 3,215 | 2,190 | 1,490 | 1,015 |
| Amatol (95) | 4,345 | 2,960 | 2,015 | 1,375 |
| Explosive D (115) | 4,780 | 3,255 | 2,220 | 1,510 |

**Through its case** (ratio of v²d left to K; ≥ 1 sets it off):

| Store (case) | 6 in HE, 25 g at 900 | 6 in HE, 200 g at 800 | 15 in AP chunk, 2 kg at 500 | GP bomb, 20 g at 1,900 | GP bomb, 150 g at 1,700 | Warhead or DC, 30 g at 2,800 |
|---|---:|---:|---:|---:|---:|---:|
| AP shell, Explosive D or shellite (60 mm) | 0 | 0 | 0 | 0 | 0 | 0 |
| 12 in HE, lyddite (30 mm) | 0 | 0 | 0 | 0 | 0.81 | 0.69 |
| 6 in HE, TNT (15 mm) | 0 | 0.09 | 0.19 | 0.28 | **1.18** | **1.78** |
| 5 in AA common, Explosive D (10 mm) | 0 | 0.10 | 0.11 | 0.28 | 0.60 | 0.94 |
| Warhead, TNT (6 mm) | 0.11 | 0.31 | 0.28 | 0.82 | **1.43** | **2.25** |
| Warhead, Type 97 (6 mm) | 0.17 | 0.47 | 0.43 | **1.26** | **2.18** | **3.44** |
| Warhead, Torpex (6 mm) | 0.37 | **1.01** | 0.92 | **2.67** | **4.64** | **7.31** |
| Depth charge, amatol (5 mm) | 0.08 | 0.18 | 0.16 | 0.47 | 0.79 | **1.25** |
| Depth charge or mine, TNT (5 mm) | 0.15 | 0.32 | 0.29 | 0.86 | **1.44** | **2.28** |
| GP bomb, TNT (10 mm) | 0 | 0.23 | 0.25 | 0.63 | **1.34** | **2.08** |
| GP bomb, Comp B (10 mm) | 0 | 0.75 | 0.81 | **2.04** | **4.35** | **6.77** |
| SAP/AP bomb (25 mm) | 0 | 0 | 0.01 | 0 | 0.32 | 0.41 |

**Case walls** are [EST] except the depth charge's 18 in case (Kirkby). AP shells' walls are 0.15-0.3 calibre and HE's about 0.1. The table is a ranking with about ×2 in each cell (the Held constants, the walls, the residual law). What holds is the pattern:
- **Shells' fragments don't set off stores.**
- **Bombs' fragments, and a detonating neighbour's, set off thin-cased ones,** above all with Torpex, Comp B or Type 97 fillers.
- **Thick-walled shells, and Explosive D anywhere, resist everything short of contact.**

### 2.2 Against the record [SRC]

- **Brownson (1943):** a bomb "detonated high order either on or below the warheads in her after mount". Contact or near-contact, the girder's failure (13 §3.1).
- **Suzuya (Samar, 1944):**
  - 10:50, "a near-miss to starboard detonates SUZUYA's No. 1 torpedo mount". Bomb fragments at about 2 km/s on Type 97 (ratio 1.3-2.2).
  - 11:00, "additional torpedoes begin to explode": fire, and the first mount's own fragments.
  - Noon, "the remaining torpedoes and ammunition explode". Sank 13:22.
- **Chōkai (Samar, 1944):** believed then to have lost her torpedoes to the escort carriers' 5 in hits. The 2019 wreck has them "still intact" [SRC Wikipedia]. Shell fragments, ratio 0.2-0.5 in F3.
- **Furutaka (Cape Esperance, 1942):** about 90 shell hits; "some ignited her Type 93 'Long Lance' torpedoes, starting fires" (03 §2.5). Lit, not detonated.
- **Ralph Talbot:** a gunfire hit on her torpedo mount, "apparently no explosion of flasks or warheads".
- **Kalk and Stormes:** "several airflasks blew up", warheads didn't.
- **The 5 in AA common:** "most susceptible to mass detonations" (the destroyer report). Chevalier's magazine of them possibly went to a torpedo (the torpedo report). In F3 it is the one shell near the line.
- **Ready ammunition:**
  - Grant: "fragments of a hit on 40mm mount No. 1 detonated some 40mm ready service ammunition".
  - Aaron Ward's 40 mm clipping room, "by fragment attack".
  - Quincy: a barbette hit on turret II's shell deck set off ready ammunition (02 §1.5).
- **The standard modern tests** [SRC recalled: STANAG 4496 fragment, 18.6 g at 2,530 m/s; STANAG 4241 bullet, 12.7 mm at about 850 m/s] sit well above every period filler's line in F2. So WWII stores would fail the insensitive-munitions fragment test, and that is why the test was made.

### 2.3 Propellant and fragments

- **A fragment that pierces bag, case or tank lights propellant,** by its heat and friction. It doesn't detonate it [INFERRED from the record]:
  - König's two 15 cm casemate magazines were flooded "after splinters lit charges" (10 §4.3).
  - The USN: "explosions of propellant powder magazines have occurred apparently due to fragment attack or fire". Longshaw's forward 5 in powder went by a direct hit [SRC].
- **The packaging is the case to perforate** (ref F4: THOR on 1 mm stops 10 g only below 180 m/s). Brass (1-2 mm), tins and Clarkson cases (about 1 mm) and USN tanks stop nothing a shell burst throws within a few metres [EST thickness]. Bags out of their cases have no case.

## 3. Heat

### 3.1 Propellant lit by hot gas (ref H1)

- **The surface lights at 190-227 °C** (460-500 K), "largely independent of heating rate" [SRC AD0472135, radiant-heating tests of double-base propellant and NC]. The ref takes 480 K.
- **The stick's surface is heated by convection and the gas's radiation.** Seconds to reach 480 K:

| Gas K | h 20 (still) | h 60 (a moving layer) | h 200 (a jet through a door) |
|---:|---:|---:|---:|
| 500 | 24,700 | 3,850 | 396 |
| 600 | 816 | 142 | 15 |
| 700 | 220 | 43 | 5.0 |
| 900 | 40 | 10 | 1.4 |
| 1,200 | 6.1 | 2.3 | 0.41 |
| 1,600 | 0.83 | 0.45 | 0.12 |
| 2,000 | 0.16 | 0.11 | 0.04 |
| 2,500 | 0.03 | 0.02 | 0.01 |

[cordite k 0.23 W/m·K, ρ 1,580, c 1,500 EST; gas emissivity 0.3 EST]

- **The dose rule for the code** (§Recommendations 2):
  - The charge lights when Σq²·dt ≥ D = (π/4)·kρc·(480 − 300)² ≈ 13,900 (kW/m²)²·s.
  - q = h·(Tg − 480) + 0.3·σ·(Tg⁴ − 480⁴) is the flux onto a surface already at 480 K, so it is zero at or below 480 K.
  - It runs within about 50% of the conduction solution from 550 to 2,500 K (+53% at 550 K, slow; within 7% from 900 K up), and it adds up through a run's changing gas.
- **What this means:**
  - A burst's own products (2,000-3,000 K) light any charge they reach within tens of ms.
  - Gas mixed down to 600-700 K through a ship's spaces needs tens of seconds to minutes. It lights only where it lingers.
  - At 470 K it never does.
- **A silk bag is about 0.2-0.4 mm of cloth, lit as easily as the stick** [EST].
- **The record shows both** [SRC 02 §1.3, 10, Glatton]:
  - A flareback lit charges in Missouri's turret in 1904, and its fire went down into the handling room (10 §5).
  - Charges left in Lion's Q working chamber went 28 minutes after the hit.
  - Glatton's cork lagging smouldered against hot clinker until the cordite went.

### 3.2 Cook-off of cased high explosive (ref H2)

- **The model [EST]:** a steel wall (lumped) heated by a fire through convection and radiation, losing into its filler (semi-infinite, TNT's properties, melting included). Cook-off is when the filler at the wall reaches its critical temperature:
  - TNT 561 K, Comp B 487 K [SRC recalled, Rogers-type critical temperatures].
  - Picric, Explosive D and amatol 500-555 K [EST from their explosion temperatures].
- **Minutes to cook-off:**

| Store, wall | T crit | Engulfing fuel fire, 1,100 K | Compartment fire, 900 K | Hot smoke, 700 K |
|---|---:|---:|---:|---:|
| Depth charge or warhead, TNT, 5 mm | 561 K | 1.3 | 3.5 | 16 |
| Warhead, Torpex, 6 mm | 487 K | 1.0 | 2.6 | 10 |
| GP bomb, TNT, 10 mm | 561 K | 2.4 | 6.2 | 25 |
| GP bomb, Comp B, 10 mm | 487 K | 1.6 | 4.1 | 14 |
| 6 in HE, 15 mm | 561 K | 3.5 | 8.8 | 34 |
| 12 in HE, 30 mm | 555 K | 6.5 | 16 | 57 |
| AP shell, 60 mm | 550 K | 12 | 30 | 100 |

**Checks** [SRC]:

| Case | Store and fire | Recorded | Model |
|---|---|---|---|
| Forrestal 1967 (outside the period, the physics' check) | M117 750 lb, Comp B, in a JP-5 fire | "only ninety seconds" | 1.6 min (10 mm wall [EST]) |
| Franklin 1945 | GP bombs | "as little as 3 to 4 minutes, or not at all" | 1.6-6 min |
| Franklin | first heavy ammunition explosion | 1-4 min after the hits | |
| Franklin | | some bombs detonated high order, at least 40% low order; 60 of 66 500 lb and 7-8 of 10 250 lb went; Tiny Tims' motors lit at about 325 °F and flew before their heads went | |
| Mullany, Isherwood | depth charges in deck fires (gasoline) | 23 and 26 min | 3.5-16 |
| USN warheads, all cases | | "no positive case of detonation of TNT loaded torpedo warheads by prolonged roasting"; Hyman's "warhead detonation" 90 s into a gasoline fire is doubted (more likely a segment or airflask) | 1.3-16 min |
| Mikuma | Type 93s, oxygen | 13:58, after bomb hits from about 12:45 and "several fires amidships" | ≤ 75 min |
| Akagi | aircraft torpedoes and bombs | the 10:26 hit "set off explosions among the fully armed and fueled B5N torpedo bombers" at once | blast and fragments, not heat |

**Reading:**
- **The model is at the fast end,** as a wall heated on one face by a whole fire would be. Real stores sat in racks and mounts with one side to the fire, sometimes wet from hoses.
- **The record says the fire's reach, more than the store's sensitivity, decides.**
  - Depth charges in gasoline fires: about 25 min.
  - Warheads: rarely or never. Their flasks burst and their segments burned first.
  - Bombs on burning aircraft: 1.5-4 min.
- **Oxygen torpedoes are the exception** [INFERRED]: an oxygen flask holed or burst in a fire burns hotter than any fuel. The IJN's own cruisers went within about an hour (Mikuma), or went in a cascade once one had gone (Suzuya).
- **Shells don't cook off in a magazine fire.** A magazine's flash or burn is out in seconds to a minute (10 §1). A 12 in shell's wall needs 6-16 min in a full fire.

## 4. Does a shell room go up with its magazine?

| Case | What went | What didn't | Source |
|---|---|---|---|
| **Hood, 1941** | a 15 in shell burst in or near the aft 4 in magazine (18.5 t of cordite; the 4 in rounds fixed, no shell room of their own [UNCERTAIN]); its pressure forced aft into X's magazine (49 t), then Y's (45 t) | the 15 in shells: not reported; the 15 in shell rooms lay below the magazines [UNCERTAIN] | Jurens 1987, via a summary; Goodall's torpedo theory, Naval History 1993 |
| **Defence, 1916** | one major detonation in A magazine, taking P1 and S1; flame aft lit the other 7.5 in turrets' propellant ("certainly did burn") | "unexploded rounds and cordite cases, some open and some unopened" in her stern; unexploded rounds in her turrets; twin shell cases still in A's hoist | McCartney (IJNA) |
| **Queen Mary, 1916** | the forward magazines; then X, its damage "similar" to Defence's | not described | McCartney |
| **Invincible, 1916** | the P and Q centre magazine (50 t), a partial detonation: "still enough to cause the ship to break in half"; the shell room below "may also have been involved" | unburnt cordite in Q turret and X's barbette; "several unexploded 12-inch rounds" in Q | McCartney 2013 |
| **Mutsu, 1943** | No. 3 turret's magazine; she was cut in two | the Type 3 incendiary shells suspected of self-igniting would not do so in tests at Kamegakubi; survivors' brown smoke matched propellant, not the shells' white | Wikipedia; warhistory.org |
| **Arizona, 1941** | a 797 kg bomb (a converted 41 cm AP shell) by turret II; the forward magazines went about 7 s later, venting through her sides | which went first, black powder (a 1944 BuShips report: a hatch to it perhaps open) or smokeless; the 14 in shells not described | Wikipedia; NPS ch. III |
| **Fusō, 1944** | the "magazine explosion" at 03:38 that split her is doubted (Tully: upended and capsized in about 40 min); the wreck lies inverted in one piece, broken amidships by the bottom | | Wikipedia; Petrel 2017 |
| **Mikuma, 1942** | bombs from about 12:45; a hit near the bridge set off ready AA shells; "at 1358 several torpedoes explode", wrecking the catapult deck and mainmast and blowing out hull below the waterline | the magazines | combinedfleet TROM |
| **Suzuya, 1944** | 10:50 No. 1 mount (a near miss); 11:00 more; noon "remaining torpedoes and ammunition" | | combinedfleet TROM |
| **Chōkai, 1944** | not her torpedoes | "torpedoes still intact" on the wreck | Wikipedia (Petrel 2019) |
| **Glatton, 1918** | her midships 6 in magazine, low order, lit by smouldering cork lagging against clinker; flame out of Q turret's roof | the after magazines, unflooded (the flames barred the controls), about 2 h until her own side capsized her by torpedo at 20:15 | Wikipedia |
| **Seydlitz 1915, Derfflinger 1916** | charges in brass and tins burned (62 in Seydlitz's C and D) | the shell rooms; the ships | 10 §1.3 |
| **Lion 1916, Q turret** | 8 charges in the chain, 28 min after the hit | the flooded magazine; its buckled doors held | 02 §1.3; 10 |
| **Boise, Savannah, Killen, Stormes** | hits in or near magazines and handling rooms; brief fires | magazines: "inrushing water quickly extinguished the fire" | destroyer report |
| **Franklin, 1945** | flight-deck bombs and rockets | the lower handling room of mount 7; "no lower magazine spaces were involved" | WDR 56 |
| **Rowan, 1943** | a torpedo "abreast an after magazine" of spare depth charges: "mass detonation" at once | | destroyer torpedo report |
| **Longshaw, Cushing, Monssen, Abner Read, Mahan** | 5 in powder magazines: one by a direct hit, four "following fire" | | destroyer report |

**So:**
- **No case was found of shell rooms detonating with a cordite magazine.** In the two wrecks where shells can be seen beside a magazine that went (Defence, Invincible), they are there whole.
- **Every mass detonation of ammunition found had one of these:**
  - a detonating donor beside it (Rowan's depth charges, Brownson's warheads, Suzuya's first mount);
  - thin-walled rounds in mass (5 in AA);
  - a long fire (depth charges at 25 min, oxygen torpedoes within an hour).
- **Shells went singly, low order, or not at all.**
- **Not found:** whether Queen Mary's, Hood's, Arizona's or Mutsu's shells survived. Their wrecks have been surveyed but the accounts found don't say. Shell rooms that burned rather than flooded are also missing from the record.

---

## Recommendations for this codebase

1. **Stores (EX6b) take one of four responses, by filler:**
   - **Propellant** (bags, brass, tins, tanks) lights. It never detonates by itself; the race decides (10).
   - **Black powder** lights, and always explodes its stow (10 §1).
   - **High explosive** (shell rooms, warheads, depth charges, mines, bombs) detonates, or burns or breaks.
   - **Wet guncotton** (warheads and mines before about 1915) never goes from outside. Only its dry primer and detonator count, as a few kg of dry guncotton.

   A store's row is its filler (§1.1: gap pressure, Held K, critical temperature, Gurney) and its case (mm of steel), with the filler's mass and the case's C/M for what it throws when it goes.
2. **Propellant, by the dose, in place of `GasIgniteK` and the gas route's `MagazineFlashChance`:**
   - Each step, D += q²·dt with q = h·(Tg − 480) + 0.3·σ·(Tg⁴ − 480⁴) W/m² (none at or below 480 K).
   - The charge lights at D ≥ 13,900 (kW/m²)²·s [INFERRED, ref H1; 0-53% slow against the conduction solution from 550 K up].
   - h 20-60 W/m²·K in a space, 200 in a jet through a door or scuttle [EST]. For charges inside cases, D at the case's inner face, so only after the case is open or hot [EST].
   - **What changes:** a burst's products (2,000-3,000 K) light charges in tens of ms, as now. Gas cooled to 600-700 K by mixing needs tens of seconds to minutes, so a flooded magazine beside a dry one (open item 4) lights only if hot gas lingers there.
   - EX6a's hot-gas-lights-a-magazine rule stays, with this dose as its test.
3. **Fragments (EX6c), by Held through the case,** in place of the flat rolls:
   - A fragment of the burst's table (13) that reaches a store, perforating its case by THOR, sets off high explosive if v_r²·d ≥ K (§2.1).
   - It lights propellant if it perforates the packaging at all.
   - The tables of §2.1 are the anchors: shell fragments set off thin-cased Torpex only; bomb fragments and a detonating neighbour's set off warheads, depth charges, mines and GP bombs; nothing short of contact sets off an AP shell.
   - Settings: K by filler (§1.1), case mm by store, the residual law's p 2 [EST].
4. **Shock (EX6b), as a contact rule, not a pressure rule:**
   - A detonation sets off a high-explosive store if the store lies within its reach of R ≤ 0.06·W^⅓ m (cased donor, W kg TNT) [SRC Mk 82: 0.044 went, 0.066 didn't]. In practice that means touching or in the same rack, mount or stack.
   - Beyond that, only fragments (point 3) carry it.
   - The near field (EX4) can serve as the reach if its contact radius is about this. The gas's pressure never sets off a filler.
5. **Cook-off (EX6b) as a timer by exposure:**
   - A store in a burning space (the fire model's cell) cooks off after the H2 table's time for its wall and the fire's class: fuel or gasoline fire, compartment fire, hot smoke. With a spread of ×0.5-3 [EST], and reset by sprinkling or flooding.
   - What happens when it goes, by store [SRC as weights, EST as numbers]:

     | Store | Outcome |
     |---|---|
     | GP bombs | half high order, half low order (Franklin's ≥40% low) |
     | Depth charges | high order |
     | TNT warheads | burn (flasks burst, segments burn), high order rarely (≤10%) |
     | Oxygen warheads | high order |
     | Shells, singly | low order |
     | 5 in AA in mass | high order |
6. **Underwater (EX8 and the sinking):**
   - Mines' and depth charges' firing gear fails or fires at about 7 MPa (Cole: 30 m for 136 kg) [SRC distance]. That is a pattern's countermining and a mine's sweeping, not a filler's detonation.
   - Depth charges not set to safe fire at their depth setting as a ship sinks (Strong); set safe, they don't (Abner Read).
7. **EX6d's anchors:**

   | Case | Wanted |
   |---|---|
   | Defence and Invincible | a magazine detonation (or partial) leaves the shells in their rooms whole |
   | Glatton | a burning 6 in magazine doesn't set off the next magazine by itself within two hours |
   | Rowan | a torpedo at a depth-charge magazine: mass detonation |
   | Brownson | a bomb on the after mount sets off its warheads |
   | Suzuya | a near-miss GP bomb sets off a Type 93 mount; a fire takes the rest in about an hour |
   | Chōkai | 5 in hits on Type 93 mounts: fires, no detonation |
   | Kalk, Stormes | flasks burst, warheads hold |
   | Isherwood, Mullany | depth charges go 20-30 min into a deck fire |
   | Franklin | bombs on burning aircraft go at 1.5-4 min, half of them low order |

8. **Open item 1 ("shell rooms' bursters" to carry Lion's margin):** the record and the physics say shell rooms don't add a detonation to a magazine's burn. If the panel needs magazines to break more ships, look at the propellant (packaging R and the stow, 10), not at the shells.

## Open, not found

- Lab sensitivity for the period's own fillers: lyddite (cast and pressed), shellite, Type 91 (TNA), Type 97 and hexanite, Explosive D's gap. AMCP 706-177's TNT, picric acid, Explosive D and tetryl pages didn't come through the extraction, so their figures above are recalled.
- Held constants other than Comp B and H-6. The scaling by gap pressure squared stands in for them [INFERRED].
- Japanese tests of oxygen torpedoes under fire, and the jettisoning record (Chikuma at Santa Cruz did). USNTMJ O-01-1 and O-25 are scanned and weren't reachable.
- The Jutland and Hood wrecks' shells: whether McCartney's or Mearns' surveys describe them for Queen Mary and Hood.
- British cordite-ignition trials after Jutland (hot-gas and splinter tests on cordite in cases).
- Mines aboard: Princess Irene (1915) blew up while her mines were being primed "hurriedly and by untrained personnel". A faulty primer was blamed, and the whole cargo went [SRC]. How mines and their primers propagated is not known.

## Sources

**Sensitivity and tests**
- AMCP 706-177, *Properties of Explosives of Military Interest* (Comp B, amatols, black powder, nitrocellulose, nitroglycerin, pentolite read; others recalled): https://apps.dtic.mil/sti/tr/pdf/AD0764340.pdf
- *Gap Tests and How They Grow* (NOL and LANL LSGT, super gap): https://apps.dtic.mil/sti/pdfs/ADP005315.pdf
- NOLTR 62-41, *Safety Information from Propellant Sensitivity Studies*: https://apps.dtic.mil/sti/pdfs/AD0329756.pdf
- Mk 82 sympathetic detonation (AFATL, 1984): https://apps.dtic.mil/sti/pdfs/ADP004860.pdf
- Shopher & Jacobs, *Suppression of Propagation Between Stacks of Bombs* (1986): https://apps.dtic.mil/sti/pdfs/ADP005352.pdf
- Zehrt, *Development and Technical Bases of US DoD IMDs to ECMs*: https://www.denix.osd.mil/ddes/denix-files/sites/32/2026/03/Cleared-Paper-Development-and-Technical-Bases-of-US-DoD-IMDs-to-ECMs-Zehrt.pdf
- Held v²d values (Table 2): https://apps.dtic.mil/sti/pdfs/ADA554529.pdf ; TNO, *munition* models (Jacobs-Roslund inputs): https://publications.tno.nl/publication/34642503/4qm9UF/scholtes-2019-munition.pdf
- Propellant ignition by radiant heat (NYU for Picatinny, 1965): https://apps.dtic.mil/sti/pdfs/AD0472135.pdf
- *Torpedo Safety Devices*, USNI Proceedings, October 1900: https://www.usni.org/magazines/proceedings/1900/october/torpedo-safety-devices
- Kirkby, *Anti-submarine weapons*, IMechE: https://engineersatwar.ww2.imeche.org/wp-content/uploads/2019/08/imeche_proceedings_kirkby_anti_submarine_weapons.pdf
- Shellite: https://en.wikipedia.org/wiki/Shellite_(explosive) ; Type 93: https://en.wikipedia.org/wiki/Type_93_torpedo ; Japanese torpedoes: https://www.navweaps.com/Weapons/WTJAP_Main.php

**War damage**
- BuShips, *Destroyer Report, Gunfire, Bomb and Kamikaze Damage*: https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html
- BuShips, *Destroyer Report, Torpedo and Mine Damage*: https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-torpedo-mine-damage.html
- WDR 56, USS Franklin: https://ibiblio.org/hyperwar/USN/rep/WDR/WDR56/index.html
- USS Forrestal fire, 1967: https://www.insensitivemunitions.org/history/the-uss-forrestal-cva-59-fire-and-munition-explosions/
- USS Hammann: https://en.wikipedia.org/wiki/USS_Hammann_(DD-412)

**Cases**
- McCartney, HMS Defence (with Queen Mary): https://core.ac.uk/download/83943393.pdf ; HMS Invincible: https://eprints.bournemouth.ac.uk/30305/1/2013-Skyllis-12-Heft%202-MCCartney%20HMS%20Invincible.pdf
- Hood, Jurens 1987 summarised: https://utzoo.superglobalmegacorp.com/usenet/news100f1/b123/sci/military/1210.txt ; *Hood's Achilles Heel*, Naval History 1993: https://www.usni.org/magazines/naval-history-magazine/1993/june/technical-report-hoods-achilles-heel
- Mikuma TROM: https://www.combinedfleet.com/mikuma_t.htm ; Suzuya TROM: http://www.combinedfleet.com/suzuya_t.htm ; Chōkai: https://en.wikipedia.org/wiki/Japanese_cruiser_Ch%C5%8Dkai
- Fusō: https://en.wikipedia.org/wiki/Japanese_battleship_Fuso ; Mutsu: https://warhistory.org/2019/06/18/explosion-and-loss-of-the-battleship-mutsu/ ; Arizona: https://en.wikipedia.org/wiki/USS_Arizona_(BB-39) ; NPS Arizona study ch. III: https://corpora.tika.apache.org/base/docs/govdocs1/434/434703.html
- Glatton: https://en.wikipedia.org/wiki/HMS_Glatton_(1914) ; Princess Irene: https://en.wikipedia.org/wiki/HMS_Princess_Irene ; Akagi: https://warhistory.org/2020/05/27/sinking-of-akagi/
