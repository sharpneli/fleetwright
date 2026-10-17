# Armour and damage: what the armour research adds to the damage model
Status: draft    Updated: 2026-10-16    Request: —

*A first synthesis across [`armor/`](armor/00-overview.md) (armour penetration, 2026-10-16) and [`damage-research/`](damage-research/) with [`damage-model-research.md`](damage-model-research.md) (damage, 2026-10-02 to 10-06). The question: does the armour research let us simulate the rest of the damage research better? It's checked against the game as it stands (`Sim.Combat/Terminal.cs`, `Effects.cs`, `Content/Sim/settings.json`, `docs/battle/fighting.md` 4.3–4.5). This is a start, not a plan. Tags: **[CALC]** = computed this session (scripts in §8), **[INFERRED]** = my reasoning.*

---

## Summary

1. **Yes, but not through the penetration formula.** The game's formula (Garzke & Dulin with the cosine exponents fitted to the 16″ Mk 8) is within **1–4% of Thompson** for every 4″–16″ belt and deck case tried. The one exception is thin decks under small shells, at +13% **[CALC]**. Replacing the formula changes almost nothing downstream.
2. **What the armour research adds is the shell's state after each plate**: whole, decapped, nose-damaged or broken; fuze not started, started or blinded; and where it bursts. That state is what every later step of the damage research takes as its input: the burst point, the space it lands in, the fragments, the fire, the flash chain and the flooding. Today the game draws that state from a fixed per-era table, the `ShellOutcomes` rows. The table was fitted loosely to battle results, and it's applied the same way on any plate that starts the fuze.
3. **The armour research's finding no. 10 is the hinge.** It says battle results come mostly from the shell and its fuze failing, not from the formula. Both research sets agree here: Jutland 1 in 13, Bismarck ~3 belt holes in ~400 hits, the 1894–1905 battles almost no belt holed. The difference is that the damage research *rolls* those failures, while the armour research gives their *causes*:
   - shatter and break-up by plate type, obliquity, plate thickness over calibre (T/D) and cap state;
   - bursts while penetrating, by how sensitive the filler is;
   - the fuze's start threshold and delay, by fuze;
   - decapping, which needs a gap before the next plate to count.
   With causes, the same rates come out where history had them. They no longer come out where it didn't, such as a WWI British shell breaking up on a light cruiser's 3″ belt.
4. **The biggest concrete wins** (§3): break-up and shatter as a state, not a roll (this fixes an over-rate on thin plate that the matchup benches already show); a "holed but broken" outcome that sends fragments, and possibly flash, without a full burst; burst points from per-fuze start thresholds and delays, which decide which space a burst lands in; underwater hits whose fuze clock starts at the water; and a number for uptake gratings.
5. **Some things the armour research doesn't change**: hit location by danger space, the HE ceiling, effects radii, and everything after the burst (flooding, fire, magazines, systems). Those stay with the damage research.

---

## 1. One chain, two halves

```
fire control ──► hit: point, striking velocity, fall angle, target angle
                    │
   ARMOUR (armor/)  ▼   plate by plate along the path
                 obliquity ─► ricochet gate ─► effective thickness Q(material, D)·T
                 ─► limit V_L ─► decap? ─► shatter / break-up? ─► fuze started? ─► residual V, new direction
                    │
                    ▼   the shell's STATE: penetration × body × fuze × burst point
   DAMAGE (damage-research/)
                 burst (or fragments only, or nothing) in a space of the cell graph
                 ─► parts within radii, fragments through bulkheads (08 §3.6)
                 ─► holes ─► flooding (01, damage-model §8.4)
                 ─► fire ─► flash chain / magazine (02 §1, 07)
                 ─► functions and service networks (02, 06)
```

The seam between the two is the **outcome taxonomy**. The two sets name it differently, but it's one list:

| Damage research (08 §3.3) | Armour research (00 §1, 04 §8.4) | Game's `Outcome` |
|---|---|---|
| RIC: ricochet | penetration = ricochet | `Ricochet` |
| SHT: shatter, no penetration | incomplete + body shattered | `Defeated` (not told apart) |
| BRK: holed, shell broke up | **holing limit**: complete/partial + body nose-damaged or shattered; fragments only | `BrokeUp` (rolled) |
| PIB: burst while penetrating | burst in plate (sensitive filler, or fuze started on a stopped shell) | `BurstInPlate` (rolled) |
| PEN: through, then bursts after the delay | **effective**: complete + body intact + fuze started + filler effective | `BurstInside` |
| LO: low-order | burst order partial or deflagration | `LowOrder` (rolled) |
| DUD: through, no burst | fuze blinded, or filler inert | `Dud` (rolled) |
| OVR: fuze never started | fuze not started: no plate over the threshold on the path | `PassedThrough` (physics, already) |
| UW: diving hit | the water segment (05 §6) | the underwater flag, a flat speed share |

The game already decides **OVR** by physics (the fuze-start threshold walked along the real path) and **RIC/defeated** by physics. Everything in the middle (BRK, PIB, LO, DUD) is one roll on the era's row once the fuze starts. That middle is where the armour research does its work.

---

## 2. What the armour research does *not* change

Read this first, so the rest isn't oversold.

- **The penetration formula.** The game's `Terminal.PenMm` compared with Thompson (`armor_pen_ref.pen_thompson_mm`), mm of plate **[CALC]**:

  | Case | Game | Thompson | Ratio |
  |---|---|---|---|
  | 16″ Mk 8 belt, 20 kyd | 509 | 509 | 1.00 |
  | 16″ Mk 8 deck, 30 kyd | 175 | 169 | 1.04 |
  | 12″ 850 lb belt, 10 km / 30° | 356 / 290 | 354 / 282 | 1.01 / 1.03 |
  | 12″ deck, 18 km | 76 | 73 | 1.04 |
  | 15″ 1,920 lb belt, 16 km | 412 | 405 | 1.02 |
  | 6″ 100 lb belt, 6 km | 158 | 154 | 1.02 |
  | 6″ deck, 12 km (65°) | 32 | 28 | **1.13** |
  | 4″ 31 lb, normal | 106 | 104 | 1.02 |

  The overview's warning that "G&D × cos overshoots 6–28%" is about a *plain* cosine. The game's fitted exponents are Thompson's own local exponents, and they carry over to other calibres better than the overview feared. Thompson is still the better-founded engine (it is the USN's own, it reproduces the NPG plate grades, and it has the T/D term built in), but switching to it is a tidy-up, not a fix.
- **Where hits land.** The danger-space side/top split (08 §3.1) and the hit query are geometry; the armour research only adds compound obliquity (heel, barbette curvature), which feeds the plate step.
- **The HE ceiling** (08 R2, 0.2 calibre). The armour research confirms it: Okun's contact hole is 0.156 D, with cracking out to about 20% thicker plate.
- **Everything after the burst point**: effects radii, flooding, fire, magazines, systems and crew. The armour research hands over a better burst *point* and *kind*, and the damage research takes it from there.

---

## 3. What the damage research guessed that the armour research now explains

Each item gives: the damage research's rule, the armour mechanism, what it changes in simulated damage, and the game's state today.

### 3.1 Break-up and shatter are states, not rates

- **Damage research:** the `OUTCOME` rows (08 §3.3), **[INFERRED]** "fitted loosely to the cases". For example, RN 1916 is 40% broken and 35% burst in the plate, and a marginal penetration moves 15 points from PEN to BRK/PIB.
- **Armour research** (00 §5.3–5.4, 02, 04 §8.1, 05 §4g):
  - **Shatter** happens on face-hardened plate against an uncapped or decapped shell, gives about +30–40% effective thickness at normal impact, and fades to nothing by about 55°.
  - **Break-up** of a capped shell depends on obliquity and plate thickness. RN 1914–17 APC broke up at 15–20° and above on plate of 0.4 calibre or more. Greenboy was proofed at 20° on 0.67 cal. The odds of breaking near the limit peak at about 0.5 around 45°.
  - A **cap** works only up to about 15–20° when soft, and while it's still on when hard. It's lost on any face-hardened plate, or on homogeneous plate of 0.08–0.12 calibre.
- **Why it matters for damage:** in the game the row is rolled on **whichever plate starts the fuze**, which can be a 25 mm deck or a 3″ cruiser belt. On those, history and the armour research say the shell stays whole. Two examples from the code:
  - A 13.5″ British shell (`rn1916`) starts its fuze on a light cruiser's belt. Arethusa's is 1–3″, at most 0.22 calibre and below the 0.4 calibre where break-up began. It then breaks up 40% of the time and bursts in the plate 35%. The matchup benches found "a 13.5″ hit puts Arethusa out 3.5% of the time" (fighting.md 4.5, "Found by the matchup's benches"). Part of that is likely the row, not the ship. The bench would show how much.
  - Every Victorian-era shell uses that same worst row (`DesignerPlaceholders`). But the period's failure was **shatter on face-hardened plate of about 0.6 calibre or more** (06 §7.3) plus sensitive fillers bursting on contact. That's a different pattern: thin plate and unarmoured ends see whole shells, and thick Harvey or KC sees shattered ones.
- **Where it reaches:** the cruiser and destroyer anchors (08 §6.4), the "capital ships are very hard to put out of the fight" finding (fighting.md 4.5), and the 1894–1905 calibration (Yalu, Yellow Sea, Tsushima: belts never holed, yet the ships were wrecked above them).

### 3.2 "Holed but broken" is its own outcome, and it still does damage

- **Armour** (00 §1, 01 §1, 02 §2.2): there are three limits, not one: **holing** (a hole is cut, the shell breaks, fragments get through), the **Navy limit** (the whole shell gets through), and **effective** (the shell gets through whole with its fuze working). On face-hardened plate the holing limit is about 2–12% below the Navy limit in velocity, and Okun's effective thickness is about 10% below his holing thickness. Fragments from a broken shell penetrate about **0.15×** what the whole shell would.
- **Damage tie-ins:**
  - Jutland's "5 of 13 holed with little internal damage" (08 §3.3) is this state exactly.
  - **The flash chain** (02 §1.3, 07). A holed turret or barbette sends a plug, hot fragments and flash inboard even with no burst behind the plate. Seydlitz at Dogger Bank fits: 08 §4.3 records the barbette hit as "did not get through", yet it flashed into the working chamber and burned out both after turrets. Today the game sends only a burst, or fire, into the magazine roll. A holed-but-broken hit on a gunhouse or working chamber should count as a flash source.
  - **Spall and plug behind a plate that held** (01 §2, 02 §1.2, 08 §3.7). A brittle face can punch out a plug *below* the shell's own limit. The armour research says when: thin face-hardened plate, near-limit hits, and Tiger's roof. The damage research says what it does: a 30–45° spall cone, 3–5 m deep. Today a defeated plate is clean inside, except for the turret jam roll.

### 3.3 Fuzes decide the burst point, and the burst point decides the damage

- **Shared:** both sets use Okun's start threshold (0.07 calibre at normal, less at obliquity) and delay × residual velocity. The game does both.
- **What the armour research adds** (04 §3–5, 00 §6):
  - **The threshold depends on the fuze**, 0.06–0.10 calibre at normal: RN No. 159 is 1″ of plate for a 16″ shell; the US Mk 21 is 1.5″ at 0° and 0.375″ at 65°. It also falls off more steeply with obliquity than Okun's formula: 4:1 measured against 2.2:1 in the formula.
  - **Some fuzes add up several plates.** The "two-plate" fuzes, most German and Japanese of both wars, are started by several thin plates in quick succession. The "single-plate" ones, some RN and the US Mk 17–23, aren't. The game has one class.
  - **The delay scatters** by about 20–30%. German WWI fuzes reached near-nominal delay in at most half the shells. The game draws 0.7–1.1× on every navy.
  - **The face-hardened case.** Any face-hardened plate starts a base fuze, unless the fuze is blinded.
- **Why it matters for damage:** the burst point decides *which space* the burst is in, and that decides the result. The strongest example is the **Iowa bomb deck** (04 §5). The 1.5″ upper deck starts a 16″ fuze, so the delay runs from there, and the burst lands on or above the armour deck, among crew, cables and secondary handling, instead of below it among machinery and magazines. That's the difference between a mission-kill hit and a vitals hit, the central contrast of the damage research (damage-model §1, items 3 and 10). The same goes for Bismarck's 50 mm upper deck and Littorio's 70 mm outer plate.
- **The Japanese delay question** (0.4 s against 0.025–0.035 s; the two sets disagree, and the armour research found Okun disagreeing with himself) matters less than it looks. South Dakota's 15–18 pass-throughs out of 26 hits were mostly 8″ shells through 10 mm structure, which never reaches the 14 mm start threshold. So the threshold explains that anchor whatever the delay. The delay matters for Type 91 shells that *do* start on a deck at long range (08 §6.3, 28 km: bursting beyond the citadel).

### 3.4 Decapping plates start fuzes; they don't add armour

- **Damage research** (01 §2.3 MODEL NOTE): a first plate of about 0.08 calibre or more strips the cap, "raise penetration difficulty of the next layer (×1.1–1.2)". 08 §3.3 sends a decapped shell to the shatter row.
- **Armour** (05 §4d): decapping helps only if **the next plate is face-hardened and far enough behind** (1.6–3 calibres at normal, 1 calibre from 30°, about half that with cement fill), or if the shell then fails. Against a homogeneous plate it *helps the shell*, because the cap's mass and drag are gone. Iowa's immune zone moves several kyd on this one credit (00 §9 row 12).
- **For damage:** a thin outer plate works mainly as a **fuze starter**, which moves the burst (§3.3). It adds resistance only in the case above. The 01 note should be read that way.

### 3.5 Underwater hits: the fuze clock starts at the water

- **Damage research** (08 §3.8) **[INFERRED]**: 8–15% of shells landing 0–30 m short become underwater hits (25–35% for Type 91); they strike at 30–50% of their velocity, defeat at most about 0.5 calibre, and 30% are duds. It also notes that Okun knows of only one penetrating underwater hit (Boise) while Lundgren's Kirishima has several. The game uses a flat 0.4 speed share and a 0.5 calibre cap.
- **Armour** (05 §6, 04 §3.5):
  - Velocity in water falls as `V₀·e^(−x/L)`, with L about 50 calibres for a flat-nosed Type 91 and about 150 for a stable ogive. Ogives tumble, though, so their L in practice is much shorter.
  - **A normal AP fuze starts on hitting the water** and bursts within about 10–15 m.
  - Tosa, 1924: a shell 25 m short struck 3.3 m down, went through 76 mm and burst in the engine room, letting in about 3,000 t.
- **For damage:** with the start at the water, the hull is reached only by shells landing *very* short, or by Type 91s. That reconciles Okun with Lundgren as a function of where each shell fell. The speed and cap become functions of the distance travelled, instead of a flat share.

### 3.6 Non-penetrating hits on turrets and barbettes: geometry decides how many there are

- **Damage research** (08 §3.7, R5): a non-penetrating heavy hit jams the turret about 35% of the time, puts one gun out 15% and stuns the crew 50% **[INFERRED]**.
- **Armour** (05 §7, §5):
  - **Barbette curvature** adds obliquity: half of all hits arrive above 30°, 13% above 60°.
  - **A back-sloped face** has an obliquity of about the slope minus the fall angle, so plunging fire meets it squarely.
  - **A roof's** obliquity is about 90° minus the fall angle.
  - **Edges and joints** lose up to 15% of the plate's quality within 1.5 calibres, and a straight joint is a single-line failure. Lion's Q turret was holed at the face–roof joint.
  - **The barbette below the deck** is a second, thinner plate behind the belt or deck: Iowa's is 3″, Bismarck's 220 mm.
- **For damage:** how often a hit *doesn't* get in comes from the plates. That sets how often the 35% jam is rolled, and which turret hits reach the working chamber, so the flash chain of §3.2 follows the ship's geometry.

### 3.7 Fragments and splinter protection

- **Damage research:** Okun's fragment table (08 §3.6), already in `Effects.FragPenMm`; RTW's "2 inches stops splinters"; splinter-proof tier C at 25–50 mm (02 §13); 25 mm in the matchup (`MatchupSplinterMm`). Two items are open: near-miss splinters (08 §8) and route protection for the service networks (06 §4: "if e.prot_tier stops this hit").
- **Armour** (01 §3.10):
  - **The THOR equations give fragments against steel**: the limit velocity and the residual velocity and mass for fragments of 5–825 grains against 0.03–1″ plate. There are separate constants for mild, hard and face-hardened steel. They scale with obliquity as about sec^1.3.
  - Material Q for **thin structural steels** (03 §8): high-tensile 0.75–0.8, Ducol 0.8–0.9, mild steel 0.65–0.75, STS 1.0, and Wotan Starrheit "~1.0 vs fragments".
- **For damage:** these are the missing pieces for **near-miss splinters**: fragment mass and velocity from the burst (08 §3.6 gives 6″ HC fragments at about 3,200 ft/s) against the plating's thickness, material and angle. Splinter protection for parts, decks and cable routes becomes the same calculation, not a fixed 25 mm.

### 3.8 Paths through several plates: the residual-velocity rule

- **The game** steps plate by plate (good; this is what the armour research recommends), slowing the shell by `v·√(1 − t/T)`. *(Since 2026-10-17 Lambert-Jonas on Thompson's V_L: shells-armour-damage.md P1a. Plain, it gives the spaced deck 0.75.)*
- **Armour** recommends Lambert–Jonas, `√(V² − V_L²)`, with turning toward the plate normal.
- **[CALC]:** with the game's T ∝ V^1.1, the game's rule slows a shell 15–25% more per plate. For example, at t/T = 0.5 the shell keeps 0.71 of its speed under the game's rule against 0.85 under Lambert–Jonas.
  - On the one spaced-deck datum, 2+1+2″ STS at 68° held like **0.74** of a solid 5″. The game's rule gives **0.80** and Lambert–Jonas gives **0.66**. The armour research's own test found the same under-rating (0.60–0.64) and Okun's 1.4 rule gives 0.78.
  - So the game's rule is *closer* on resistance. But because it slows shells more, its burst distances (delay × residual velocity) come out shorter.
- **For damage:** don't swap the rule on its own. The armour research says the deck constants are its weakest calibration (Hood 1919: the limits 12–28% high). Whatever rule is kept, check the burst distance against Baden 1921 (38 ft behind a 25 cm belt) and against the deck series. Burst distance is what the damage side reads.

### 3.9 Materials, including the ones that aren't armour

- **Quality basis.** The game's `ArmourQuality` puts WWI Krupp cemented at **1.0**, the same as WWII plate. The armour research's reconciled table (00 §4.2: three routes agreeing within about ±5%) gives early KC **0.85–0.88** and WWII face-hardened plate **0.89–1.09 depending on calibre**. So the game's WWI belts are about 15% too strong against good shells. Against the shells the WWI navies actually fired, it's partly the other way, which is §3.1's point.
  - **The armour files disagree with each other for pre-Krupp materials.** 03 §8 chains period ratios: nickel steel 0.59, Harvey 0.70. 00 §4.2 re-runs 21 trials through Thompson: nickel steel 0.85, Harvey 0.82. 00 is the reconciled one.
  - The game's nickel steel 0.75 and Harvey 0.8 sit between the two.
- **Face-hardened plate gets relatively weaker against bigger shells.** For example, US Class A goes from 1.03 at 8″ to 0.92 at 16″, while British cemented plate rises a little with calibre. And thin face-hardened plate, under about 3–4″ or 0.3 calibre, is *weaker* than homogeneous plate. That matters for cruiser turrets and Richelieu's roofs.
- **Non-steel layers** (03 §3):
  - **Wood counts at 1/100 of its thickness and cement at 1/25.** A PT boat's or S-boat's wooden hull therefore never starts a base fuze. That gives a mechanism to 03 §6.1's **[UNCERTAIN]** "shells passed through without exploding".
  - **Coal**: "2 ft of coal ≈ 1 in of armour" (dreadnoughtproject). This settles the doubt in damage-model §14 about "2 ft ≈ 1 ft". It also ties the bunker's ballistic value (small, and gone as coal burns) to its flooding value (permeability about 0.4, 01 §1.4).
- **Gratings** (05 §5, O.S. 751): a 15″ holed armour grating stops about what a **6″ solid plate (≈40%)** does. The game's placeholder grating is "as thick as the deck" (`HitPath.cs`), so it overrates uptake gratings by about 2.5×. That's the first number for the open gratings design input.

### 3.10 Each era's characteristic failure

The damage research's era knobs (damage-model §10: shell quality, dud rate, fuze delay, armour quality per nation) become a parameter set per shell and per plate in the armour research (04 §8, 00 §8): body class, cap class, nose, W/D³, filler and how sensitive it is, fuze class with its threshold, obliquity factor, multi-plate class and delay; and material Q(D) with a shatter flag. Each era then fails the way it did:

| Era | Characteristic failure (armour) | What the damage side sees |
|---|---|---|
| 1860–1890 | Palliser chilled shot shatters on any steel face; common shell bursts on contact | Damage on the outside only; fires (Lissa, early trials) |
| 1890–1905 | Uncapped steel shatters on Harvey/KC ≥ ~0.6 cal; Shimose bursts on contact or water; wet pyroxylin under-initiated | Belts unholed; upper works, ends and fires wreck ships (Yalu, Tsushima: Oryol's 4 belt hits, none through) |
| 1914–17 | RN lyddite APC bursts in the plate or breaks up at ≥15–20° on ≥0.4 cal; German fuzes off nominal | Jutland's 1 in 13; German shells through British ships unburst, or bursting in the wrong place |
| 1918–30s | Greenboy and booster fixes; ≥90% full detonation | The design case becomes common |
| WWII | Good shells; fuze start threshold and delay dominate; IJN long delay | Over-penetration of light ships and superstructure (Samar, South Dakota); bomb-deck early starts |

---

## 4. Where the two sets disagree

| Topic | Damage research | Armour research | Follow |
|---|---|---|---|
| Penetration formula | De Marre as baseline (damage-model §8.1, 05 §A3) | De Marre's cos³ too steep; Thompson F is the reference | Armour (and the game already isn't De Marre) |
| Ricochet | WoWS 45–60°, game 45° + 20° span | Shells bite to 65–75°; 65–80° gated on V/V_L; certain ≥ 80°; broken shells don't ricochet at 45–60° | Armour. In the game it only relabels a defeated shell, so the effect is small. But nothing stops a "penetration" at 85° on a thin deck. |
| Spaced plates | Pre-combine with Okun's 1.4 rule (08 §3.2) | Step plate by plate; keep 1.4 as a check | Armour (the game already steps) |
| Decapping plate | ×1.1–1.2 on the next plate (01 §2.3) | Only if FH follows within the gap, else helps the shell | Armour |
| IJN Type 91 delay | 0.4 s | 0.08–0.4 s *or* 0.025–0.035 s; Okun inconsistent | Open; §3.3 says which anchors depend on it |
| Hood 1919 burst 34–40 ft | Held as measured | Not re-found; Baden 1921 38 ft is consistent | Use Baden as the anchor |
| Jutland ratio | "1 of 17" | 1 of 13 (17 less 1 HE and 3 ricochets) | Same data; use 1/13 for AP that met the plate |
| Coal | "2 ft ≈ 1 ft steel" flagged as too generous | 2 ft ≈ 1 in | Armour |
| Armour quality | One multiplier per nation (damage-model §10) | Q(material, calibre) + shatter state | Armour; 00 §4.2 over 03 §8 for pre-KC |

---

## 5. Calibration: two layers instead of one

Today the battle anchors (Jutland, Bismarck, South Dakota, the destroyer benches) are hit by tuning the `ShellOutcomes` rows. That mixes two things: the plate model and how often shells fail. The combined research supports splitting them:

1. **The plate model alone, against trials** (the armour research's §9 and the `07-*.csv` data): the USN 16″ table within ±0.1″; NPG 1946–47 limits within about 1.5%; Italian obliquity ratios; Annapolis 1890; Indian Head 1893; Beardmore 1901; Baden 1921; Hood 1919; the 2+1+2 spaced deck; and the fuze start of the Mk 21. **No fuze or quality roll is involved.** A `shipgen` subcommand that runs the game's own `Terminal` against these rows would test the port as it stands.
2. **The whole chain, against battles** (08 §7.4, damage-model §11, armour §9 rows 13–14): with the physics now producing shatter, break-up and fuze starts, the leftover roll covers only fuze and filler failure. That's 5–10% for WWII shells, at most about 50% for German WWI fuzes, and the lyddite bursts in the plate. Each number has a source in 04 §6, and none is fitted to the battle itself.

Anchors shared by both sets, so they test the seam: Jutland 1/13 on more than 9″ of plate; Bismarck about 3 belt holes in about 400 hits; South Dakota 6 belt hits with none through, and 15–18 of 26 hits passing through; Yalu, the Yellow Sea and Tsushima with belts unholed; Baden's burst 38 ft behind the plate; Iowa's immune zone at 23.6–27.4 kyd.

---

## 6. Recommendations for this codebase

All of these fall under fighting.md 4.5's **Terminal** deepening ("penetration proper: material qualities, capped and uncapped shells, spaced armour, edge effects; shell quality by navy and year; diving shells; near-miss splinters; a ricochet table"). They're ordered by how much they change damage outcomes. None is decided.

1. **Break-up and shatter as a state** (`Terminal`). Decide whether the body stays whole from the plate's material (the shatter flag), its T/D, the obliquity, the cap state and V/V_L, using 00 §5.3–5.4, 02 and 05 §4g. Take `Broken` out of `ShellOutcomes`, and let the rows carry only fuze and filler failures (DUD, LO, and the lyddite PIB). Re-run the matchup benches (Arethusa, Iron Duke) and the Jutland belt share.
2. **Holing vs effective** (`Terminal`, `Effects`). Add a holed-but-broken outcome: fragments at about 0.15× through the hole, no full burst. On a gunhouse, working chamber or barbette, it's a flash-chain source (02 §1.3). Spall behind defeated thin or face-hardened plate (08 §3.7).
3. **Fuze classes** (`ShellTypeRow`, `DesignerPlaceholders`). Give each fuze a start threshold, an obliquity factor and a single- or two-plate class, with delay scatter by navy and era (04 §8.3). Iowa-style bomb decks then move bursts up out of the vitals.
4. **Caps and decapping** (`Terminal`, weapon facts): the cap class with its obliquity window, decap thresholds by cap type, the gap test before a face-hardened plate, and the cap's mass lost (05 §4d).
5. **Material Q** (`settings.json` `ArmourQuality`): the 00 §4.2 values, with Q(D) for face-hardened plate and the thin-FH rule. WWI KC goes from 1.0 to about 0.85–0.88. Add Q for the structural steels (mild, HT, Ducol, STS), so hull plating and splinter decks aren't the 0.6 catch-all.
6. **Underwater hits** (`Terminal`, the hit query): water travel by distance with `e^(−x/L)`, the fuze clock starting at the water, and Type 91 the exception (05 §6).
7. **Gratings** (`HitPath`, the designer's later input): about 40% of a solid plate of the same thickness, until the designer gives gratings their own figure.
8. **The ricochet gate** at 65–80° on V/V_L, certain at 80° and above, and none for broken shells (05 §2, §8 step 1).
9. **Fragments**: THOR for fragments against plating (01 §3.10), shared by `Effects`' boxes, near-miss splinters (08 §8), and route protection in the service networks (06 §4).
10. **Residual velocity**: keep the game's rule for now (§3.8). Calibrate the burst distance against Baden and the deck series before changing it.

**Needed from the designer:** a material per plate (the materials map gives it), backing layers (teak, cement), grating thickness, and shell and fuze classes per battery. The tracer already gives each plate's thickness, obliquity and the gaps between plates.

---

## 7. Gaps neither set fills

- **Quantified belt-seam leaks** from hits that don't get through (08 guesses 5–50 t/h; the armour research found nothing either), and **repeated hits near a hole**. The armour research's stand-in is to treat the hole as a free edge (up to 15% loss) and cracked face-hardened plate at Q × 0.85–0.9. That ties to Oslyabya's plates loosened by repeated hits near the waterline.
- **Bombs against decks.** The armour research's deck physics applies to AP and SAP bombs (Iowa's bomb deck, armoured flight decks: 04 §1.4's "3″ stops a 250 kg SAP from ~7,000 ft"), but bomb shapes, W/D³ and fuzes aren't researched. The bomb document that 08 mentions as coming later would fill this.
- **Joints under underwater shock.** The armour research's joint rule (a straight joint fails along one line) and the damage research's TDS failures at riveted joints (Yamato's belt and bulge, PoW) describe the same weakness from two sides. Neither gives a number.
- **French and Russian armour quality, and Czech interwar plate**: no data.

---

## 8. Sources and checks

- Armour: [00-overview.md](armor/00-overview.md) §0–10, [01](armor/01-physics-formulas.md) §1, §3.10–3.11, §4, §7; [02](armor/02-facehardened-caps.md); [03](armor/03-materials.md) §3, §8; [04](armor/04-shells-fuzes.md) §1–8; [05](armor/05-obliquity-multilayer.md) §4–7; [06](armor/06-early-era.md) §5–7; [07-calibration-notes.md](armor/07-calibration-notes.md).
- Damage: [damage-model-research.md](damage-model-research.md); [01](damage-research/01-capital-subdivision.md) §2–3; [02](damage-research/02-components.md) §1–2, §13–14; [03](damage-research/03-small-vessels.md) §1, §3–6; [04](damage-research/04-carriers.md) §1; [05](damage-research/05-mechanics-and-games.md) §A1–A5, B11; [06](damage-research/06-service-networks.md) §2–4; [07](damage-research/07-magazine-explosions.md); [08](damage-research/08-gunfire-effects.md).
- The game: `src/Fleetwright.Sim.Combat/Terminal.cs` (`PenMm`, the residual rule, `Arm`), `Effects.cs` (`FragPenMm`), `src/Fleetwright.Sim.Ships/DesignerPlaceholders.cs` (shell quality by era), `HitPath.cs` (gratings), `Content/Sim/settings.json` (`ArmourQuality`, `ShellOutcomes`), `docs/battle/fighting.md` 4.3–4.5.
- **[CALC]** checks, run this session:
  - §2 table: `Terminal.PenMm` re-coded in Python against `armor_pen_ref.pen_thompson_mm`.
  - §3.8: a stepping check with T ∝ V^1.1 for both rules on 2+1+2 vs 5.
