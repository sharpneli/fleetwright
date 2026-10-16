# 11 — Blast and confinement: what a burst does to structure, on a deck and in a closed space

*Research for the damage model, after `08_gunfire_effects.md`. It answers two open questions together, because they are
one question:*

- *`shells-armour-damage.md` open question 1, **confinement**. Today a burst is sized by its charge alone, with the same
  radii in a small closed room as on an open deck.*
- *The **burst constants**: `R_wreck`, `R_hole` and the bulkhead-breach rule (08 §3.6). They were fitted to 2–3 holes
  (08 §8), and every burst's reach depends on them.*

*It also feeds open question 2, explosives as parameters (§2.4).*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the cases, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion code:** `blast_confinement_ref.py` (pure Python, runs in 0.03 s). Every table in §3–§5 comes out of it.

---

## 0. Short answer

1. **A burst inside a closed space and one in the open are different events, not one event at two sizes.**
   - **In the open there is only the shock,** a pulse of about a millisecond that falls off fast. What it wrecks is
     what the fragments wreck, plus the shock's short reach.
   - **In a closed space the shock reflects off the walls, and then the hot gas stays.** It leaves a **quasi-static
     pressure (QSP)** that lasts tens of milliseconds, until the gas vents or the walls give way.
   - Wall for wall, the QSP's impulse is several times the shock's. Cabin trials name it "the major destroying
     element" [SRC]. A 1 kg charge in a 30 m³ room with one door open puts about 5× the impulse on its walls that the
     same charge does on a wall 2 m away in the open (§2.2).
2. **The QSP is set by the charge per volume, W/V, and the filler's chemistry:**

   `P_qs ≈ 0.28 · η · [Q_d + φ·(Q_c − Q_d)] · W / V`  (MPa, W in kg, V in m³)

   - **Q_d** is the heat of detonation.
   - **Q_c** is the heat of full combustion: TNT-type fillers are oxygen-poor, and their products **burn again in the
     room's air** (afterburning).
   - **φ** is the share of that second burn the air allows.
   - It matches the measured and published fits to ±15% from 0.01 to 1 kg/m³ (§2.1).
   - So TNT gets up to 3× its detonation energy in a room with air to spare. Amatol and black powder get none, and black
     powder is weak again because half its products are solids.
3. **Structure fails at its joints, by pressure:**
   - A boundary holds about **0.06 MPa per mm of plate** before it ruptures (riveted ×0.85).
   - It bulges and leaks from about **0.012 MPa per mm**.
   - A WT door or hatch goes at **half its bulkhead's rupture pressure**.
   - So a WWII destroyer's 5 mm bulkhead ruptures at about 0.3 MPa, and her doors go at 0.15.
4. **The model is a cascade over the cell graph the flooding already has:**
   - The QSP of the space the burst is in is set against each of its boundaries.
   - What ruptures opens into the next space, and the gas spreads into the larger volume, so the pressure drops.
   - What vents to the air or the sea shortens the pulse.
   - It repeats until nothing more fails.
   - It is cheap: a few rounds, a few µs, no allocation. It replaces the "`R_wreck > 0.4 × compartment length`" rule.
5. **What it gives on the cases** (§4):
   - **Bulkheads:** a 5″ burst in a destroyer compartment breaches nothing; 8″ HE blows the doors; a 250 kg SAP bomb
     carries away the next bulkheads, decks and the side.
   - **Crew:** a 16″ AP burst in a 150 m³ space disables nearly everyone in it, and the same shell on a deck disables out
     to 8 m.
   - **Equipment:** in an engine room of 1,500 m³ the same 16″ shell barely reaches beyond its own fragments
     (0.04 MPa: joiner work and fittings).

   This is the war-damage record's shape:
   - shell blast is "minor" against bomb blast (WDR 51);
   - a delayed-action burst inside is "the most destructive";
   - Exeter's CPO flat crew died "mostly by blast", and Lion's Q turret crew "from pressure".
6. **The constants:**
   - **`R_wreck = 1.6·W^⅓` was an *internal* number.** It was fitted to Kirishima's casemate hits. On an open deck the
     shock and fragments wreck to about **1.0·W^⅓**. Inside, the cascade replaces the radius.
   - **`R_hole = 0.45·W^⅓·√(6/t)` survives.** Against 13 measured contact holes it is unbiased (×0.93) with a scatter of
     ×1.4 (§3.1), as good as a refit. Keep it.
   - **`R_blast = 3.5·W^⅓` (people injured) agrees with the shock's own reach on a deck** (disabled to about 3.2·W^⅓).
     Keep it for the open.

---

## 1. The physics, in the order a burst happens

### 1.1 The shock (every burst)

**Hopkinson–Cranz scaling.** Everything goes by the scaled distance `Z = R / W^⅓` (m/kg^⅓, W in kg TNT-equivalent).
These values are for a hemispherical burst on a surface, the deck case: Kingery–Bulmash as Swisdak fitted it [SRC]. The
coefficients are in the code.

| Z | Incident overpressure (kPa) | Reflected (kPa) | Reflected impulse / W^⅓ (kPa·ms) |
|---|---|---|---|
| 0.5 | 4,890 | 39,400 | 2,370 |
| 1 | 1,352 | 8,155 | 884 |
| 2 | 284 | 1,059 | 364 |
| 3 | 116 | 331 | — |
| 5 | 43 | 101 | 126 |
| 10 | 14.9 | 31.6 | 59 |

**What the shock alone tears.** A plate tears at its supports when the impulse reaches Nurick's threshold,
`I* = v₀·√(ρ/σ) ≈ 2.2` [SRC]. For mild steel that is about **3 kPa·s per mm of plate**.
- Bare charges make small holes: 100 g of TNT in contact with 17 mm of steel made an 8.5 mm hole [SRC].
- So the shock alone tears 6 mm plate only within about 0.25·W^⅓, and 2 mm joiner work within about 0.5·W^⅓.
- **The large holes shells make come from the casing's fragments at contact plus the shock together.** That is why
  shell holes follow an empirical rule (§3.1), not the blast tables.

**People in the open.** A short pulse needs a lot of pressure:
- 50% lung lethality needs about 0.6–0.8 MPa of incident overpressure for a few-ms pulse, which is within about
  **1.3·W^⅓**.
- Eardrums and concussion, "out of action", go at about 0.1 MPa, within about **3.2·W^⅓** [SRC Bowen, FFI].
- Beyond that, fragments do the killing, out to tens of metres (Salt Lake City hit 3: a cone to 120 ft).

**Inside, the shock reflects.** In a box, the first reflected peak is 2–8× the incident one, and corners take 2–4× the
wall centres [SRC Hu 2011, Sci. Rep. 2026]. The total shock impulse on a wall is about **1.5–3× the first reflection**
[EST]. This matters for the wall nearest the burst. The whole room is dominated by the next phase.

### 1.2 The gas (only when confined)

After the shock, the detonation products, heated air and afterburning fill the space at a nearly uniform pressure:
- It rises over 2–20 ms [SRC].
- It lasts until it vents. A sealed box held it for 80 ms or more [SRC].

**Every ship structure feels it as a static load:**
- A stiffened ship panel has a natural period of about 10–30 ms.
- A load much longer than that acts statically, with a load factor of up to 2 for a sudden rise.
- The shock's 0.25–1 ms is impulsive to the same panel.
- In a steel-cabin test the two together bent the plate more than their sum: 298 mm against 184 mm from the shock alone
  and 85 mm from the QSP alone [SRC CJSR].

**Venting.** A space with an opening of area A to the outside blows down as [SRC Kinney–Sewell]:

```
P_abs(t) = P_abs,0 · exp(−725 · α·A · t / V)     α ≈ 0.6
```

- Measured blowdowns last about **2× this** because the afterburning goes on [SRC FRANG].
- With `α·A ≥ 0.6·V^⅔` no gas pressure builds at all: the space is open, only the shock acts [SRC Keenan & Tancreto].
- Below `A/V^⅔ ≈ 0.015` the vent changes nothing [SRC].

So, for the game:
- **An open mount, a bridge wing or a deck is "open".**
- **A casemate with its ports, or a boiler room with its uptakes, is partly vented.**
- **A magazine, a cabin, a passage or a closed turret is closed.**

---

## 2. The gas pressure

### 2.1 The formula and the fits

```
P_qs = 0.28 · η · [Q_d + φ·(Q_c − Q_d)] · W / V          (MPa gauge; W kg of filler, V m³)
φ    = 1 / (1 + W·air / V_air)                             (the share of the afterburn the air allows)
```

- **0.28** is the heat-loss share (0.7) × (γ − 1) (0.4). It is fitted to DDESB's fully confined TNT test: 0.21 MPa at
  0.0497 kg/m³ [FIT].
- **`air`** is the air (m³) needed to burn one kg's products fully: TNT 2.66 [SRC].
- **`V_air`** is the space's air: a flooded or packed space has less.

Against everything published, for TNT (MPa):

| W/V (kg/m³) | This model | UFC 3-340-02 | SwRI (177 tests) | Steel ship cabin (2024) | Detonation only, no afterburn |
|---|---|---|---|---|---|
| 0.003 | 0.013 | 0.034 | 0.018 | 0.029 | 0.004 |
| 0.01 | 0.041 | 0.082 | 0.049 | 0.064 | 0.013 |
| 0.03 | 0.119 | 0.180 | 0.123 | 0.135 | 0.038 |
| 0.05 | 0.193 | 0.260 | 0.189 | 0.191 | 0.063 |
| 0.1 | 0.358 | 0.429 | 0.339 | 0.306 | 0.126 |
| 0.3 | 0.869 | 0.946 | 0.852 | 0.643 | 0.378 |
| 1.0 | 2.06 | 2.25 | 2.34 | 1.45 | 1.26 |

**How the fits compare:**
- **UFC is known to be conservative.** It is 1.2–1.5× the DDESB and FRANG measurements [SRC].
- **The model tracks SwRI's fit of 177 tests** and the steel-cabin fit within their scatter.
- **The DDESB points** were 0.21, 0.122 and 0.104 MPa at 0.0497, 0.0256 and 0.0224 kg/m³, against the model's 0.19,
  0.10 and 0.09.

**Why the energy form rather than a power fit:**
- It carries the filler's chemistry.
- It carries the air: water mist cut the QSP by 10–44% in cabin tests [SRC], and a flooded space has less air to
  afterburn in.
- It is a multiply and a divide.

### 2.2 How much worse inside: one worked case

One kg of TNT in a 30 m³ compartment with a 2 m² door open:
- QSP about 0.14–0.19 MPa, lasting about 22 ms.
- About **1.8 kPa·s** of impulse on every wall.

The same charge in the open, on a wall 2 m away:
- 0.36 kPa·s in about 1 ms.

So inside: **about 5× the impulse, on every wall at once, at the period that bends ship panels**, plus 1.5–3× more shock
on the near wall. Closed, it is a sustained static load, limited only by what gives way [SRC-derived].

### 2.3 What the room's size does to one shell (`blast_confinement_ref.py`, the open-vs-closed table)

- **Open:** the radii are from the shock alone.
- **Closed:** the QSP, the share of the space's people killed (Bowen, long pulse) and put out of action (eardrums,
  lungs).
- "D" is Explosive D, "Lydd." is lyddite, "BP" is black powder.

| Shell (filler) | Open: 6 mm torn / 2 mm torn / lethal / disabled (m) | Closed 50 m³ | 150 m³ | 600 m³ |
|---|---|---|---|---|
| 4″ HE (1.0 kg TNT) | 0 / 0.3 / 1.4 / 3.2 | 0.08 MPa (0% / 22%) | 0.03 (0 / 0) | 0.01 |
| 5″/38 AAC (3.3 kg D) | 0 / 0.5 / 1.9 / 4.6 | 0.20 (0 / 84%) | 0.07 (0 / 13%) | 0.02 |
| 6″ HC (6.0 kg D) | 0 / 0.7 / 2.4 / 5.6 | 0.34 (10 / 98%) | 0.12 (0 / 52%) | 0.03 |
| 8″ HC (9.7 kg D) | 0.5 / 0.9 / 2.8 / 6.6 | 0.50 (85 / 100%) | 0.19 (0 / 83%) | 0.05 (0 / 4%) |
| 15″ APC 1916 (27 kg Lydd.) | 0.9 / 1.8 / 4.2 / 10.1 | 1.24 (all) | 0.50 (84 / 100%) | 0.14 (0 / 62%) |
| 16″ AP Mk 8 (18.6 kg D) | 0.6 / 1.3 / 3.5 / 8.2 | 0.84 (all) | 0.34 (13 / 98%) | 0.10 (0 / 32%) |
| 16″ HC Mk 13 (69.7 kg D) | 1.3 / 2.7 / 5.4 / 12.8 | 2.15 (all) | 0.99 (all) | 0.33 (7 / 98%) |
| 46 cm Type 0 HE (61.7 kg TNA) | 1.3 / 2.7 / 5.3 / 12.7 | 2.36 (all) | 1.06 (all) | 0.34 (12 / 98%) |
| 15″ CPC (58.6 kg BP) | 0.8 / 1.6 / 3.9 / 9.2 | 0.52 (90 / 100%) | 0.17 (0 / 77%) | 0.04 (0 / 2%) |

**What this shows:**
- **A delayed burst inside is why AP was fuzed to burst inside.** A 16″ AP shell, with 1.5% filler, disables a
  150 m³ space's whole crew. On a deck the same shell disables out to 8 m and kills within 3.5 m. Proceedings (1909)
  already said bursts outside "are harmless except against men in such exposed positions as to be struck by
  fragments".
- **Size matters as much as charge.** The same shell does little to people in a battleship's 1,500 m³ engine room
  (0.04 MPa), and is lethal in a 50 m³ cabin.
- **Black powder is weak inside.** A 15″ CPC with 59 kg of black powder does only a little more than a 6″ HC. The British common
  shells at Jutland were weak for this reason as well as their fuzes.

### 2.4 The filler's numbers (for open question 2, the explosives catalogue)

The same 10 kg in 100 m³ and in 20 m³ (MPa), with the afterburn share at 20 m³:

| Filler | Q_d | Q_c | Air (m³/kg) | Blast RE | 100 m³ | 20 m³ | φ at 20 m³ |
|---|---|---|---|---|---|---|---|
| TNT | 4.5 [SRC] | 15.0 [SRC] | 2.66 | 1.00 | 0.36 | 1.26 | 0.43 |
| TNA (IJN) | 4.4 | 13.6 | 2.25 | 1.00 | 0.33 | 1.22 | 0.47 |
| Lyddite / shimose (picric acid) | 4.4 | 11.4 | 1.63 | 1.12 | 0.29 | 1.16 | 0.55 |
| Shellite | 4.2 | 12.3 | 2.00 | 1.05 | 0.31 | 1.16 | 0.50 |
| Explosive D (USN) | 3.2 | 11.5 | 1.87 | 0.90 | 0.29 | 1.05 | 0.52 |
| Amatol 80/20 | 4.0 | 4.0 | 0 | 1.00 | 0.11 | 0.56 | — |
| RDX/TNT | 5.0 | 12.0 | 1.70 | 1.35 | 0.31 | 1.23 | 0.54 |
| Black powder (η 0.55) | 2.9 | 2.9 | 0 | 0.40 | 0.05 | 0.22 | — |

- TNT's heats are measured (LLNL: 4.57 and 14.96 MJ/kg). Combustion heats and air come from oxygen balance. Others are
  from Meyer and are **[UNCERTAIN]** to about ±15%.
- **Q_d, Q_c, `air`, RE and η** are what an explosives catalogue needs for blast. Brisance (fragments), sensitivity and
  the failure rates are its other columns.
- **Fumes.** TNT-type fillers make about 0.35 m³ of CO per kg, and afterburning burns it. A big charge in a small space,
  short of air, leaves the most: the model gives 300–1,300 ppm in the reached region for the bomb cases.
  - It is lethal over minutes to hours in a closed space, and it travels through vent trunks. Boise's damage-control
    party was killed by gas through the trunks; Curacoa's plot was gassed through a supply fan.
  - Dunkerque's half-turret crew was asphyxiated. That was propellant fire smoke, which `07_magazine_explosions.md`
    owns.

---

## 3. Structure

### 3.1 Contact holes: the constant holds

`R_hole = 0.45·W^⅓·√(6/t)` (equivalent-circle radius, m; W TNT-eq kg; t mm), checked against 13 measured holes:

| Case | W (kg) | t (mm) | Measured R (m) | 0.45 rule |
|---|---|---|---|---|
| South Dakota hit 5, 8″ Type 0 on ¼″ | 8.2 | 6.4 | 0.91 | 0.88 |
| South Dakota hit 2, 14″ Type 0 on ¾″ STS | 28.8 | 19 | 0.77 | 0.78 |
| South Dakota hit 11, on 1¼″ STS | 28.8 | 32 | 0.42 | 0.60 |
| Ralph Talbot, 8″ on about 6 mm | 7.7 | 6 | 0.60 | 0.89 |
| Boise hit 1, 8″ just below 16 mm | 8.0 | 16 | 0.35 | 0.56 |
| Boise hit 2, on 12 lb (7.5 mm) | 8.0 | 7.5 | 0.77 | 0.80 |
| San Francisco hit 2, bow | 8.2 | ~11 | 1.22 | 0.67 |
| San Francisco, 6″ on 11 mm | ~3.5 | 11 | 0.42 | 0.50 |
| San Francisco, 5.5″ on 6.3 mm | ~2.2 | 6.3 | 0.30 | 0.57 |
| San Francisco hit 31, through 2 × 19 mm | 8.2 | 38 | 0.34 | 0.36 |
| Franklin, Tiny Tim head on 2½″ STS | 68 | 63 | 0.84 | 0.56 |
| Yorktown, 100 kg GP on teak over 3 mm | ~40 | ~4 | 1.83 | 1.88 |
| Tsushima, 12″ on about 10 mm | ~40 | ~10 | 1.11 | 1.19 |

- **Bias ×0.93, scatter ×1.4** (log standard deviation 0.33) [FIT].
- A free refit gives `0.30·W^0.45·(6/t)^0.45`, and one with the cube root kept gives `0.38·W^⅓·(6/t)^0.37`. Neither
  beats the old rule's scatter (0.31 against 0.33), so **keep it**.
- Its weak spots are thick plate (25 mm and up: the rule is about 1.4× too big) and charges lying on armour decks (too
  small). An exponent of 0.4 on the thickness would split the difference, if a later bench asks.

A blast-only derivation gives `0.15·W^0.51·(t/6)^−0.54`, a third the size [SRC-derived]. It is right for bare charges
and wrong for shells: the casing's fragments cut most of a shell's hole.

**A burst a metre or three from a boundary inside a space** (not in contact) holes it to **R ≈ 0.4–0.6·W^⅓**:
Yorktown's 2nd deck, Illustrious's hangar deck, Marblehead, Warspite's bottom. Savannah's was 0.73 [SRC-derived].

### 3.2 What a boundary holds

**Ship boundaries fail at their joints, not across the plate:**
- Formidable's deck failed at "a riveted connection… welded seams held" [SRC].
- North Carolina's holding bulkhead went by its rivets.
- A bare plate between stiffeners would hold several MPa by membrane action. Its rivets, welds and stiffener ends go
  long before that.

**The capacities, per mm of plate:**

| Boundary | Deforms (leaks, doors jam) | Ruptures (an opening) | Example |
|---|---|---|---|
| Joiner partition, 1.5–3 mm on light angles | ~0.01 MPa | **0.025 MPa/mm** (0.04–0.08) | Manila Bay: joiner bulkheads damaged to 40 ft |
| WT bulkhead, deck, side (welded) | **0.012 MPa/mm** | **0.06 MPa/mm** | Destroyer 5 mm: 0.06 / 0.30; cruiser 9 mm: 0.11 / 0.54 |
| The same, riveted | ×0.85 | ×0.85 | Pre-war and WWI ships |
| WT door or hatch | — | **½ its bulkhead's rupture**, at least 0.08 | Boise's doors dished 3–4 in; Marblehead's blown off two bulkheads away; Savannah's failed over 27 m |
| Armoured bulkhead or deck | by thickness | by thickness | 76 mm STS: 4.6 MPa (Marblehead's ruptured only in contact) |

[FIT, to the anchors below]
- **Boise's magazine bulkheads:** elastic to 0.09 MPa, held brief peaks of 0.41–0.48 MPa [SRC].
- **Hydrostatic design:** WT bulkheads are built for a head to the bulkhead deck, 0.05–0.14 MPa [SRC], so yield at
  about that and rupture at 3–5× it.
- **Laboratory thresholds:**
  - Clamped 3 mm boxes went from large dishing to central breach at about 1.7–3.6 kg/m³ of charge per volume [SRC].
  - Those are small boxes with no joints. Full-size panels tear sooner.

**What "deformed" means:**
- The bulkhead is dished and its seams are sprung: a leak, not an opening.
- Its doors jam, so damage control and people can't pass.
- **"Ruptured"** means torn from its boundary, blown out or holed: an opening of a good share of its area.

### 3.3 The open deck

**No QSP.** What a deck burst does to structure:
- the contact hole in the plate it hits (§3.1);
- the fragments (the fragment model);
- the shock, which bends and tears light plating only within 0.3–0.5·W^⅓ and wrecks fittings and equipment to about
  1·W^⅓.

**The record:**
- Indiana 1920: deck bursts' damage was "local", and crews in closed turrets would not have been incapacitated [SRC].
- Illustrious hit 3: deck dished 9 in, not holed; the gun crew killed by fragments.
- Salt Lake City hit 3: structure barely touched, and fragments to 120 ft.

**So on an open deck, `R_wreck` should be about 1.0·W^⅓, not 1.6.**

---

## 4. The cascade, against the cases

### 4.1 The rule

```
region = {the space the burst is in};  vents = its openings to the outside
repeat:
    P   = QSP(filler, W, volume(region), air(region))
    t_b = 2 · V / (725 · 0.6 · vents) · ln((P + P0) / P0)              # blowdown, ms (∞ when sealed)
    P_e = P · min(1.3, π · t_b / 15 ms)                                # what a panel feels; 0 if vents ≥ 0.6·V^⅔
    for each boundary of the region:
        P_e ≥ rupture  → ruptured: to air/sea adds a vent (and a hole for the flooding); to a space joins it to region
        P_e ≥ door     → door blown: 1.2 m² opening; the space beyond joins
        P_e ≥ deform   → deformed: leaks, jammed doors
until nothing new fails
then: people and parts in every space of the region by the P they saw (§4.3)
```

**Why it behaves:**
- Each failure adds volume, so the pressure falls. The cascade stops by itself, at the first ring of boundaries
  strong enough for the spread-out gas.
- Venting to the air or sea shortens the pulse, so what's left feels less.

**Two hit types it reproduces:**
- **An HE shell bursting on contact with a thin side:** most of its gas goes outboard through its own hole, since the
  burst point is in the wall. That is the near-wall case. Treat the burst as **outside** the space when it bursts in the
  plate (HE's "inside if t < 0.2 cal" decides it), with its fragments going in.
- **A delayed burst** (AP and SAP) is inside and gets the full cascade.

### 4.2 The cases

The ships are compartment chains in `blast_confinement_ref.py`: volume, bulkhead, deck and side gauges as in each
ship's report, or estimated where it gives none.
- "P / felt" is the first QSP and the load a panel feels.
- "Spaces reached" counts the burst space and every space the gas got into, with the final volume and pressure.

| Case | P / felt (MPa) | Spaces reached | Ruptured | Doors | Deformed | Dead / out in the burst space | History |
|---|---|---|---|---|---|---|---|
| 5″ AAC in a DD compartment (150 m³, 5 mm) | 0.07 / 0.09 | 1 | none | 0 | 3 | 0 / 13% | WDR 51: shell blast minor; Sterett: 11 hits, survived |
| 8″ HC, same | 0.19 / 0.25 | 5 | none | 4 | 2 | 0 / 83% | Ralph Talbot: compartments wrecked, survived |
| 14″ HC in a DD engine room (600 m³) | 0.23 / 0.30 | 5 | none | 4 | 2 | 0 / 90% | S. B. Roberts: the 40 × 10 ft hole was the side (contact) |
| 46 cm Type 0 HE in a DD engine room | 0.34 / 0.44 | 5 | 2 bulkheads, deck | 1 | 6 | 12 / 98% | 08: "wrecks the compartment and often the next" |
| 250 kg SAP (60 kg) in a DD space (200 m³) | 0.87 / 1.13 | 11 | 2 bulkheads, 2 decks, side above and below | 6 | 12 | all | WDR 51: "carried away several adjacent decks and bulkheads"; little beyond ~6 m |
| Decoy: 50 kg bomb (~25 kg) | 0.55 / 0.71 | 9 | 2 bulkheads, 2 decks, side | 4 | 14 | 94 / 100% | structural damage within ~3.7 m |
| Boise hit 6: 8″ AP in a magazine (riveted, 9 mm) | 0.08 / 0.10 | 1 | none | 0 | 0 | 0 / 18% | bulkhead deflected 2 in, doors dished (before the powder burned) |
| Marblehead A: 100 kg GP in the steering room | 1.02 / 1.33 | 11 | 2 bulkheads, 2 decks, side | 6 | 12 | all | decks holed and torn, lifted 6 ft; doors blown two bulkheads away |
| Savannah: Fritz X (270 kg amatol) in a handling room | 0.86 / 1.12 | 4 | 2 bulkheads, bottom, side | 1 | 7 | all | bulkheads demolished within 4.6–6 m, held at 6–8 m, bottom opened |
| Exeter: 28 cm HE in the CPO flat (150 m³) | 0.51 / 0.67 | 5 | 2 bulkheads, 2 decks, side | 0 | 10 | 88 / 100% | "nearly all… killed, mostly by blast"; mains fractured |
| 16″ AP Mk 8 in a BB engine room (1,500 m³) | 0.04 / 0.05 | 1 | none | 0 | 0 | 0 / 1% | Scharnhorst: 14″ in No. 1 boiler room; speed lost to steam, recovered |
| 6″ HC in a cabin flat cut by joiner rooms | 0.34 / 0.44 | 4 | the joiner, 2 bulkheads | 0 | 4 | 10 / 98% | Boise hit 2: cabin wrecked, a bulkhead 15 ft inboard demolished |
| Baden: 15″ CPC, black powder, in the end (300 m³) | 0.09 / 0.11 | 1 | none | 0 | 2 | 0 / 26% | upper deck lifted 4 ft 6 in, 43 ft torn from the side |
| Baden, the same shell TNT-filled | 0.62 / 0.81 | 5 | 2 bulkheads, 2 decks, side | 0 | 10 | 99 / 100% | (for contrast) |
| Kirishima: 16″ AP in a casemate (vented 4 m²) | 0.14 / 0.18 | 1 | none | 0 | 3 | 0 / 63% | "holes ~10 m across" (a survivor's sketch) |

**Where it fits:** the destroyer gradient (5″ nothing, 8″ doors, heavy HE the next compartments, bombs several), Boise,
Exeter, Marblehead, Savannah's first ring, Decoy and WDR 51's "about 20 ft".

**Where it doesn't, and why:**
- **Baden.** With black powder the model leaves the deck in place; the record has it lifted and torn. Either the 1921
  shells were lyddite or Shellite filled (the trials tested new shells, and the secondary source doesn't say) or the
  space was much smaller [UNCERTAIN]. With a high explosive the model gives what Baden shows.
- **Kirishima's 10 m holes** rest on a survivor's sketch, which Lundgren gives as about 30 ft. The model and every other
  16″ case say a 2–3 m hole. Treat it as an outlier.
- **Savannah's doors** failed over 27 m, and the model blows one. Her hoists, trunks and the 30 ft hole in the 2nd
  platform carried the gas where the chain model has no path. The real cell graph has those trunks (§5.3).
- **Princeton** (a 250 kg bomb, "minor" structural damage) is a counter-case the model can't match unless the bomb went
  low order or burst in the hangar's volume. WDR 56: at least 40% of Franklin's own bombs went low order.

### 4.3 People and parts in the gas

**People.** The share in every reached space, by the pressure it saw (a long pulse):
- **killed:** Bowen near a reflecting surface, 50% at 0.42 MPa, 1% at 0.28;
- **put out of action:** eardrums and lungs, 50% at 0.12 MPa, few below 0.035 [SRC Bowen, Richmond].
- **Plus the burst space's own fragment and fireball zone,** in the fragment model. The record's heavy casualties from
  internal bursts were blast: Exeter's CPO flat, Lion's Q turret, Yorktown's Repair V (37 killed).

**Parts.** Out of action by gas pressure, by class [EST, from the WDRs]:

| Class | MPa | Examples |
|---|---|---|
| Fittings | 0.03 | Joiner work, lockers, light ducts, fire curtains |
| Electrical | 0.10 | Switchboards, instruments, fire-control gear: South Dakota's breaker trips; even her own Turret III's blast cost a minute of power |
| Auxiliary | 0.25 | Pumps, light piping, uptakes, hoist trunks: Boise's hoists burst at about 0.48 |
| Machinery | 0.60 | Boilers, turbines, gearing |

In the burst space, what's within 1·W^⅓ is wrecked by the shock and fragments regardless (§5.1).

---

## 5. Proposed game model

### 5.1 Two cases

**Open: the burst point is outside the hull, on a weather deck, in an open mount, or in a space vented by 0.6·V^⅔ or
more.**
- Wreck **R_open = 1.0·W^⅓** (was 1.6), shock and fragments, unless armour stronger than its splinters boxes a part
  (as now).
- People: killed within 1.3·W^⅓ by blast, and `R_blast = 3.5·W^⅓` as now for injuries; fragments beyond, as now.
- The contact hole: `R_hole` as now.
- No bulkhead breach from the blast.

**Confined: the burst point is inside a space.**
- **The near zone** in the burst space: R_near = 1.0·W^⅓, everything wrecked as in the open, and a boundary within it
  holed to 0.5·W^⅓.
- **The cascade** (§4.1) over the space graph, from the burst space.
- **Its results:**
  - **Ruptured boundaries** become openings in the flooding graph: a WT bulkhead or deck gets about ¼ of its area; a
    door, 1.2 m²; shell plating, an opening to the sea or air.
  - **Deformed boundaries** get a seam leak (as the 4.3b leak rates, ×10 [EST]) and their doors jam.
  - **People and parts in every reached space** take damage by its pressure (§4.3). This replaces "`R_wreck > 0.4 ×
    compartment length`" and most of `RoomVital`'s burst force: the room's own share of vitals still decides what in
    it matters.
  - **Fumes** (§2.4) in the reached spaces, for later.
- **W:** the filler's own kilograms and its explosive's Q_d, Q_c, air and η. The shock and fragments use `filler.re`.

**What happens when the shell bursts in the plate.** HE bursts at the plate (`Terminal`: inside if t < 0.2 calibre).
- A burst *in* a thin plate (HE, nose fuze) vents through its own hole. Treat it as **near-wall**: the contact hole,
  fragments inward, and the cascade run with the hole as a vent of the hole's area. That gives HE on a destroyer's side
  its known effect: one compartment wrecked, its doors and partitions blown, rarely a bulkhead.
- A delayed burst gets the plain cascade.

### 5.2 Cost

**A burst:** one cascade over the cells around it, typically 1–3 rounds of a handful of boundaries each, with a log and
a divide each. Microseconds, nothing allocated: the region and failed boundaries fit a small fixed buffer on the
damage system, as `Effects`' are.

**It needs per cell or space:**
- the volume (the flooding's capacity has it), and the air share (permeability, the water in it);
- the openings to the outside (vents);
- per boundary (edge): its kind, thickness, area, riveted or welded, and whether it has a door. The plates are on the
  path already.

### 5.3 What the designer and the classes need

- **Vents** per space (a new number): uptake area for boiler rooms, ports for casemates, open hatches, skylights. A
  default by room kind until the designer emits them: a boiler room's share of its funnel's area; a casemate's ports.
- **Joiner partitions:** rooms within a cell (`RoomsIn`) are separated by joiner work (0.025 MPa/mm, 2 mm). The cascade
  meets them first, so a burst in a cabin reaches the whole cell almost at once, which is the record's "joiner work
  wrecked".
- **Doors** on WT bulkheads and hatches on decks: the flooding's `Open` joins are open doors already. Closed WT doors
  are boundaries with `door = true`.
- **Trunks:** hoists, uptakes, vent trunks and lift wells join spaces through decks. They carry gas and flame (Boise,
  Savannah, Yorktown's uptakes, Franklin's bomb-elevator trunk). An edge of kind `trunk` with a small area joins the
  region at a low pressure (0.02 MPa [EST]). The magazine flash chain (07) reads the same edges.
- **Riveted or welded** per ship: a design number (construction), 0.85 for riveted.

### 5.4 How it meets the other models

- **Terminal:** unchanged. It gives the burst point and whether it's in the plate.
- **Effects:** the near zone and the cascade replace the wreck radius and the breach rule. Fragments stay theirs, as
  does E3's THOR rework.
- **Flooding:** gains openings and leaks from the blast. Below the waterline that is how a burst inside sinks a ship:
  Savannah's bottom, Marblehead.
- **Fire:** a burst in a space with fuel, paint or ready ammunition, plus afterburning, raises the fire chance there.
  The opened boundaries help fire spread [EST].
- **Magazines (07):** gas reaching a handling room or magazine is a flash source, by the chain that's there.
- **Damage control (09/10):** jammed doors and deformed bulkheads are its work. The fumes keep its parties out until
  vented.
- **The matchup (`Matchup.cs`):** reads a burst's expected effect by the target's typical space volume: one more number
  per class profile.

### 5.5 Tests (wide bands)

1. A 5″ HE burst in a destroyer compartment breaches no WT bulkhead and blows no door.
2. 8″ HE in the same space blows its doors and disables most of its crew.
3. A 250 kg SAP bomb in a destroyer carries away the next bulkheads and decks, and little beyond about 6 m.
4. A 16″ AP burst in a 150 m³ space disables nearly all its crew. In a 1,500 m³ engine room it does little beyond its
   near zone and kills nobody by blast.
5. The same 16″ AP on an open deck wrecks to about 2.5 m, and nothing beyond its fragments.
6. A black-powder burst inside gives a sixth to an eighth of the same mass of TNT's pressure.
7. No allocation per burst; replays hold.

---

## 6. Uncertainties

- **The rupture and deform capacities** (0.06 and 0.012 MPa/mm) are fitted to a handful of anchors. Full-scale tearing
  depends on joints, steel toughness and span. Benches against the matchup and the destroyer cases should move them,
  inside 0.04–0.08.
- **The afterburn share** is a smooth stand-in for a gradual, mixing-limited burn. It fits the published fits between
  0.01 and 1 kg/m³ and is not tested beyond.
- **The fillers' heats** other than TNT's are within about ±15%.
- **Baden** (the shell's filler) and **Kirishima** (the hole size) are uncertain cases.
- **Job 74** (the RN's internal-burst trials on Monarch) and the **ex-Washington internal charge** (1924, 400 lb aboard,
  she stayed afloat) have no published details online. They are in D. K. Brown's *Nelson to Vanguard* and the ADM files.
- **Nurick–Martin's coefficients** came from memory in one source. The tearing threshold I* ≈ 2.2 was checked.
- **The cascade merges spaces at once.** Gas through a small door fills the next space over tens of ms, and a stiff
  boundary may fail after the pressure has started to fall. It errs toward reaching too far through doors, and the
  door rupture pressure absorbs that.

---

## Sources

**Blast physics and confined explosions**
- Kinney & Graham (1985), *Explosive Shocks in Air*; formulas as in Guzas & Earls 2010:
  https://bpb-us-w2.wpmucdn.com/sites.coecis.cornell.edu/dist/8/147/files/2019/08/Guzas_Earls_2010_SCS_10_5.pdf ;
  NUREG/CR-7201: https://www.nrc.gov/docs/ML1524/ML15245A640.pdf
- Swisdak (1994), simplified Kingery–Bulmash: https://apps.dtic.mil/sti/pdfs/ADA526744.pdf ; coefficients:
  https://github.com/fcento100/kingery-bulmash
- DDESB gas-pressure tests (2025): https://www.denix.osd.mil/ddes/denix-files/sites/32/2026/02/044p_IESSE-2025_Oswald_DDESB-2025-Gas-Pressure-Testing_REV-1.pdf
  ; improved methodology: https://www.denix.osd.mil/ddes/denix-files/sites/32/2026/02/045_Oswald_Improved-Gas-Pressure-Methodology.pdf
  ; Oswald (UFC venting): https://ndia.dtic.mil/wp-content/uploads/2018/intexpsafety/OswaldPaper.pdf
- FRANG vented tests: https://apps.dtic.mil/sti/tr/pdf/ADA500237.pdf
- SwRI/Baker QSP fits: https://apps.dtic.mil/sti/pdfs/ADP000440.pdf ; Baker & Oldham 1975 (blowdown):
  https://apps.dtic.mil/sti/pdfs/ADA025502.pdf ; PBX-9404: https://apps.dtic.mil/sti/pdfs/ADP004836.pdf
- UFC 3-340-02 change 1: https://stg.wbdg.org/FFC/DOD/UFC/ARCHIVES/ufc_3_340_02_2014_c1.pdf
- TNT heats, afterburning (Kuhl, LLNL): https://digital.library.unt.edu/ark:/67531/metadc870839/m2/1/high_res_d/1108886.pdf
- Air needed for afterburn (2025): https://www.fie.undef.edu.ar/ceptm/wp-content/uploads/2025/05/1-s2.0-S2214914725001564-main.pdf
- Hu et al. 2011, internal blast reflections: https://opus.lib.uts.edu.au/bitstream/10453/118537/1/2041-4196.2.1.21.pdf
- Ship-cabin reflected pressures (Sci. Rep. 2026): https://www.nature.com/articles/s41598-026-43574-w
- Duct and tunnel propagation: https://apps.dtic.mil/sti/pdfs/AD0725157.pdf ; https://apps.dtic.mil/sti/tr/pdf/ADA013134.pdf
- TNT equivalence: https://en.wikipedia.org/wiki/TNT_equivalent ; oxygen balance:
  https://man.fas.org/dod-101/navy/docs/es310/chemstry/chemstry.htm

**Ship structures under internal blast**
- Steel cabin QSP fit (Zhang 2024, JMSE 12(9):1650): https://www.mdpi.com/2077-1312/12/9/1650
- Large-cabin test, QSP the major element: https://www.bzycj.cn/en/article/doi/10.11883/bzycj-2023-0289 ;
  QSP impulse vs shock: https://bzycj.cn/en/article/doi/10.11883/1001-1455(2017)04-0719-08
- Shock and QSP on stiffened plate (CJSR): https://english.ship-research.com/fileZGJCYJ/journal/article/file/e016add1-e59c-4528-b58d-a9eee8995191.pdf
- Failure modes of closed boxes: https://www.mdpi.com/2076-3417/12/21/10974 ; chamber tests:
  https://www.mdpi.com/2075-4701/12/5/859 ; multi-cabin damage grades:
  https://www.bzycj.cn/article/doi/10.11883/bzyjc-2019-0438 ; SAP warhead in a double cabin:
  https://english.ship-research.com/en/article/doi/10.19693/j.issn.1673-3185.03000
- Contact charges on plate: https://brodogradnja.fsb.hr/wp-content/uploads/sites/25/2025/11/No2_8.pdf ;
  https://www.mdpi.com/1996-1944/16/8/2966
- Tearing thresholds (Nurick and successors): https://discovery-pp.ucl.ac.uk/10076832/1/Manuscript-IJIE-SQUARE-PLATE-2017-Revised.pdf
  ; https://discovery.ucl.ac.uk/10104834/1/2020_Failure%20and%20detachment%20path%20of%20impulsively%20loaded%20plates_TWS_Dario%20Schiano%20Moriello.pdf
  ; Menkes & Opat: https://apps.dtic.mil/sti/html/tr/AD0774707/index.html
- Watertightness (USNI 1931): https://www.usni.org/magazines/proceedings/1931/march/water-tightness

**Injury**
- Bowen et al. 1968: https://apps.dtic.mil/sti/pdfs/AD0693105.pdf ; FFI refit:
  https://publications.ffi.no/en/item/asset/dspace:4077/1340204.pdf ; eardrum:
  https://hearinghealthmatters.org/waynesworld/2012/eardrum-rupture-at-what-pressure/

**War damage and trials** (with those in 08)
- WDR 57 South Dakota; WDR 26 San Francisco; Boise; WDR 51 destroyers (all as in 08)
- Savannah: https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20SAVANNAH%20(CL-42),%20BOMB%20DAMAGE%20-%20Gulf%20of%20Salerno,%20Italy,%20September%2011,%201943.pdf
- Marblehead: https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20MARBLEHEAD%20(CL-2),%20BOMB%20DAMAGE%20-%20Java%20Sea,%20February%204,%201942.pdf
- Salt Lake City: https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20SALT%20LAKE%20CITY%20(CA-25),%20GUNFIRE%20DAMAGE%20-%20Bering%20Sea,%20March%2026,%201942.pdf
- Yorktown at Midway: https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20YORKTOWN%20(CV-5),%20BOMB%20AND%20TORPEDO%20DAMAGE%20-%20Midway,%20June%204%20to%207,%201942%20(LOST%20IN%20ACTION).pdf
  ; at Coral Sea: https://ibiblio.org/hyperwar/USN/ships/logs/CV/cv5-Coral.html
- WDR 56 Franklin: https://ibiblio.org/hyperwar/USN/rep/WDR/WDR56/index.html ; WDR 62 Princeton:
  https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-princeton-cvl23-war-damage-report-no-62.html
  ; WDR 16 Lexington: https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-lexington-cv2-war-damage-report-no16.html
- WDR 60 escort carriers: https://ibiblio.org/hyperwar/USN/rep/WDR/ESCORT%20CARRIERS%20GUNFIRE,%20BOMB%20AND%20KAMIKAZE%20DAMAGE%20AND%20LOSS%20During%20World%20War%20II.pdf
- Savo Island cruisers: https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20QUINCY%20(CA-39),%20U.S.S.%20ASTORIA%20(CA-34)%20and%20U.S.S.%20VINCENNES%20(CA-44),%20GUNFIRE%20AND%20TORPEDO%20D.pdf
- British bomb damage to Sept 1941 (BuShips summary): https://www.ibiblio.org/hyperwar/USN/rep/WDR/WAR%20DAMAGE%20TO%20BRITISH%20NAVAL%20VESSELS%20-%20Summary%20of%20Damage%20by%20Bombs%20to%20September%202,%201941.pdf
- Illustrious: https://www.armouredcarriers.com/adm26783/2014/10/16/illustrious-january-10-damage-report-bomb-shell ;
  Formidable: https://www.armouredcarriers.com/battle-damage-to-hms-formidable ; Indomitable:
  https://www.armouredcarriers.com/adm26784/2014/10/16/indomitable-august-12-damage-report-bomb-shell ;
  BuShips memo on Formidable: https://www.armouredcarriers.com/buships-97482-memorandum-hms-formidable
- Lundgren, South Dakota: https://www.navweaps.com/index_lundgren/South_Dakota_Damage_Analysis.php ; Kirishima:
  https://navweaps.com/index_lundgren/kirishimaDamageAnalysis.php
- Baden 1921 (secondary): https://weaponsandwarfare.com/?p=16494
- Indiana 1920 bombing: https://www.navsource.org/archives/01/pdf/010121f.pdf ; Ostfriesland:
  https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/n/the-naval-bombing-experiments/an-account-of-the-bombing.html
- Ex-Washington: https://en.wikipedia.org/wiki/USS_Washington_(BB-47) ; Tosa: https://en.wikipedia.org/wiki/Japanese_battleship_Tosa
  ; Monarch: https://en.wikipedia.org/wiki/HMS_Monarch_(1911)
- Exeter at the River Plate: https://navyhistory.au/hms-exeter-at-the-battle-of-river-plate/2/
- Jutland turrets (USNI 1925): https://www.usni.org/magazines/proceedings/1925/april/lessons-jutland-affecting-design-turret-armor
- Tsushima: https://www.scientificamerican.com/article/with-the-baltic-fleet-at-tsushima ; Proceedings 1909:
  https://www.usni.org/magazines/proceedings/1909/june/projectiles-naval-guns ; Proceedings 1899 (Santiago):
  https://www.usni.org/magazines/proceedings/1899/april/sketches-spanish-american-war
- Warspite and Fritz X: https://navyhistory.au/the-luftwaffe-anti-shipping-missile-threat-during-ww-ii/5/

**Project docs:** `08_gunfire_effects.md` (§3.6 the constants this revises), `07_magazine_explosions.md`,
`09_damage_control.md`, `10_damage_control_holistic.md`, `06_service_networks.md`, `damage-model-research.md`.
