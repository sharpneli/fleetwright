# 16 — Casualties per hit: killed and wounded, fitted to the war-damage record

*Research for the damage model, after `11_blast_confinement.md` and `13_fragmentation.md`–`15_near_misses.md`. The
question:*

> *Casualties per hit. Today it's crew density times the burst areas, with 80% and 20% shares that are guesses. The
> war-damage reports have killed and wounded per hit, which would fit it properly.*

*It takes 13's fragment field as given: `casualty_ref.py` imports `fragmentation_ref.py`. It leaves 14 (spall behind a
plate that held) and 15 (near misses) to their own notes. What it adds is people: who is in reach, what kills and
what wounds, and the record to fit it to.*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the record, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion files:**
- `casualty_ref.py`: pure Python, about 35 s (the fit is a grid search). It needs `fragmentation_ref.py` beside it.
  Every table in §2 and §6 comes out of it.
- `16_casualty_records.csv`: the record, 384 rows. There are 240 hit rows, 145 of them with a killed count, and 144
  ship rows; 62 ship rows are in the fitting groups. Every row has its source URL, a confidence and notes on
  conflicting figures.
- `16_casualty_records_notes.md`: the collectors' notes on sources, conflicts and what each period's reports say.

---

## 0. Short answer

1. **The war-damage reports almost never give killed and wounded per hit.** BuShips WDRs record each hit's place,
   calibre, burst, fire and flooding in detail. For personnel they say "severe casualties", or nothing. Boise is the
   one WDR with small per-hit numbers. The per-hit figures come from:
   - single-hit incidents, where the ship's total is the hit's (Colorado, North Carolina, Wichita, Murphy, O'Brien);
   - narratives (Japanese General Staff for the Yellow Sea, Kostenko for Oryol, USNI 1925 for the Jutland turrets,
     Surgeon Cussen for Exeter);
   - ship totals over a counted number of hits (Jellicoe's despatch table, Campbell via secondary sources, DANFS).

   So the fit has two layers: **127 classified hits with a stated count**, which show the *shape* (what kind of hit
   kills how many), and **62 ships over 668 hits**, which give the *level*.
2. **What the record says:**

   | | Killed / hit | Wounded / hit | Killed share |
   |---|---|---|---|
   | WWI capital ships, heavy APC (no flash fires) | 1.9 | 3.0 | 0.39 |
   | WWII capital ships, heavy AP | 1.4 | 2.1 | 0.41 |
   | WWII cruisers, 6–11 in | 2.3 | 3.5 | 0.43 |
   | Medium HE close in on crowded WWII ships | 2.0 | 5.6 | 0.27 |
   | Destroyers, 4.7–8 in | 2.2 | 4.5 | 0.32 |
   | WWI cruisers fighting from behind armour, 4.1 in | 0.17 | 0.33 | 0.35 |
   | WWI light cruisers with open-backed shields | 2.0 | 2.4 | 0.46 |

   - **Calibre hardly matters, placement does.** A 15 in hit on a battleship costs about what a 5 in hit on a
     destroyer costs. The same small shells cost 12× more against Chester's open guns than against Kent's casemates.
   - **The distribution is a low floor with spikes.** Most hits kill nobody: 88% of hits that passed through, 93% of
     duds and 68% of hits on armour. A burst in a manned station kills a median of 6, and a propellant flash kills 50–160.
   - **About a third of casualties die** (0.25–0.45). Fragments wound, and blast and flash kill: a flash kills 92% of a
     turret's crew.
3. **Why the current rule can't fit it:**
   - **Its areas grow as W^⅔,** so a 16 in AP hit gets 3× a 5 in's casualties and a 16 in HC hit 8×. The record has
     them about level.
   - **It has no walls and no cover.** Men in the next compartment count as if in the open.
   - **It has no killed/wounded split.**
   - **A shell that passes through gets the same thin line everywhere.** Saumarez's director lost 11 killed to a
     shell that never burst, and the rule gives 0.7.
   - **Its U(0.3, 1.7) scatter gives no zeros and no spikes.**
   - **On whole ships it runs about 2–4× low.** It gives about 2 per hit on a destroyer against the record's 6.8 (08
     §6.3; §6 M5 here).
4. **The proposed model counts men, not area.** Each burst, per group of men in reach (a cell's `battle_crew`):
   - **Fragments:** `λ = S · N(r) · g · A · T(t, r) · band / (4πr²)`, the man-stopping hits on one exposed man.
     - `N(r)` comes from **13's burst** (Gurney, Mott, drag): the pieces still carrying 80 J at r. Bigger shells
       break into bigger pieces, not more: a 16 in AP throws about 320, a 5 in AA shell about 1,000. That is the
       record's flat calibre curve, from the physics.
     - `T` is the share through the plate between (Okun, as now). `band` is 13's side-spray ring, coarsened to a room.
     - A share `cover` of the men are exposed, by kind of space. The rest take a tenth of the flux.
   - **Killed and wounded per man:** `P(cas) = 1 − e^(−λ)` and `P(killed) = 1 − e^(−qλ)`. Near the burst many hits
     kill; out at the edge one hit wounds. The killed share comes out by itself.
   - **A shell that perforates without bursting** drives the plate's own disc ahead of it in a cone. That kills a
     director's crew (Saumarez, Gneisenau) and nobody in an empty flat. A plate that holds is 14's spall cloud.
   - **Blast:** 11's QSP inside (Bowen lethality, eardrums and lungs). In the open, killed within 1.3·W^⅓.
   - **Propellant flash:** a share of everyone the flash reaches. An enclosed turret train is 0.92 killed and 0.06
     wounded (Derfflinger C, Seydlitz). An open casemate battery is 0.54 / 0.32 (Malaya).
5. **The fit.** Two constants are left, the people's flux scale `S` and the kill-per-fragment `q`, fitted to both
   layers:
   - The result is **S = 8.7** and **q = 0.04**.
     - 13's free field is S = 1, and it stays that for parts.
     - People inside a steel ship meet more than the free-field count: ricochets off the boundaries, debris from
       fittings and structure, and Mott's short count of small pieces [EST].
   - S and q trade against each other. S from 4 to 18 and q from 0.02 to 0.11 stay within 15% of the best error. The
     ship groups alone, the single hits alone and both together agree on **S·q ≈ 0.35–0.45**, the killed rate where
     the flux is thin.
   - The model meets 4 of the 9 ship groups within ×1.6 on both killed and wounded: Jutland, WWII capital ships,
     crowded WWII ships under HE, and destroyers. Three more come out about ×0.5 on killed: WWII cruisers, WWI light
     cruisers, and the Japanese ships of 1904.
   - It misses two groups by 3.5–8× too high: Russian ships in 1904–05 and the WWI armoured cruisers. Those ships
     fought with their men behind armour and their upper works empty. That is a placement question, and the stand-in
     ships here can't answer it; the engine's real ships can (§5.3).
6. **In the engine:**
   - The model needs the cell's `battle_crew` (shipgen already places it), a cover value by room kind, and 13's
     fragment table per shell.
   - It costs a few λ evaluations per cell within reach: microseconds.
   - **Recalibrate S and q with `shipgen hits` on real ships against the R2 targets** (§5.3). The stand-in ships in
     the reference are coarse.

---

## 1. The record

### 1.1 What was collected

Three searches, one per body of reports, merged into `16_casualty_records.csv`:

| Body | Hit rows (with killed) | Ship rows | Main sources |
|---|---|---|---|
| USN, WWII | 62 (38 shell hits with killed) | 26 | WDRs: South Dakota (57), San Francisco, Boise, Sterett, Salt Lake City, WDR 51. DANFS. ONI Combat Narratives. NHRC 91-1 |
| 1894–1918 | 90 (56) | 62 | Japanese General Staff (USNI 1914). Kostenko. Jellicoe's despatch table. USNI 1925, *Lessons of Jutland*. von Hase. naval-history.net |
| WWII, others | 88 (46) | 56 | Admiralty despatches. The Prince of Wales damage report extract (hmshood.org.uk). kbismarck. Cussen's diary (JMVH). combinedfleet |

The CSV adds:
- `class`: A–G for a hit, by what it did (§2.1);
- `group`: G1–G7 for a ship row in the fit (§2.2);
- `killed_n`, `wounded_n` and `hits_n`: the parsed numbers, ranges at their midpoint, with the group rows' choices
  in their notes.

### 1.2 What the reports do and don't hold

- **WDRs are about the ship, not the men.** They give burst modes very well: South Dakota's 26 hits, of which about
  13 never burst; Kalinin Bay's 7 of 14 passed through. Casualties are qualitative. The true per-hit splits for the
  big multi-hit ships are in the ships' own action reports, in casualty lists by battle station. Those are at NARA,
  not online.
- **The per-hit rows are biased toward big events.** A hit is written up when men die. "None stated" was recorded
  as 0 and flagged. Within the two fully itemised WDRs (Boise and Salt Lake City, 13 hits), 9 had no casualties.
- **"Wounded" isn't one thing.** Despatches often count the seriously wounded only. Asahi at Tsushima had 5 serious
  and 19 light; Malaya 33 or 68; Warspite 16 or 32; Kent's 8 were the serious ones. The ship groups use the despatch
  figure where two exist, so their wounded lean toward the serious. "Killed" sometimes includes died of wounds.
- **Hit counts are soft.** Graf Spee's 20 or 70; Oryol's 76, mostly 3–6 in; Lion's 13 or 14. The groups use the
  better-sourced counts, with the choice in each row's notes.
- **Conflicts are kept, not resolved,** in the notes column: South Dakota 38–40 killed and 60 or 180 wounded (the
  fit uses 39 / 60); Exeter 61–63 killed; Seydlitz at Dogger Bank 159 or 165.

---

## 2. What the record says

### 2.1 By what the hit did (R1)

Hits with a stated killed count (`casualty_ref.py` R1):

| What the hit did | Hits | Killed none | Median killed | Mean killed | Max | Mean killed / wounded (where both stated) | Killed share |
|---|---|---|---|---|---|---|---|
| A. Burst at a manned station: bridge, director, AA platform, gun, CIC | 27 | 0% | 6 | 8.6 | 40 | 9.2 / 17.1 | 0.35 |
| B. Burst in other spaces: flats, mess decks, machinery, upper deck | 29 | 38% | 2 | 3.4 | 15 | 2.0 / 6.3 | 0.24 |
| C. Passed through without bursting | 16 | 88% | 0 | 1.1 | 11 | 0.0 / 0.3 | – |
| D. Dud | 14 | 93% | 0 | 0.3 | 4 | 0.0 / 0.6 | – |
| E. On armour: defeated, or burst on the plate outside | 25 | 68% | 0 | 1.0 | 11 | 0.8 / 3.2 | 0.19 |
| F. Propellant flash or fire | 6 | 0% | 54 | 60.5 | 162 | 72.8 / 16.8 | 0.81 |
| G. Near miss | 10 | 80% | 0 | 1.2 | 8 | 0.6 / 0.6 | – |

- **Class A** is where the deaths are:
  - Colorado's air defense station, 5 in AAC: 20 killed, 48 wounded.
  - Cleopatra's bridge, 6 in: 15 / 5.
  - O'Brien's CIC area: 13 / 19.
  - Nachi's signal bridge: 11 / 21.
  - Achates: 40 killed by one 8 in hit.

  Losing the captain or the admiral recurs: Jintsu, Hiei, Aoba, Seydlitz's bridge, Prince of Wales.
- **Class B** splits by crowding:
  - an engine room's watch: Murphy 3 / 26;
  - empty flats: Wichita 0 / 14, Boise's cabin 0 / 3;
  - a battery at its guns: Matsushima 28 / 68, the worst non-flash hit in the set.
- **Class C** is zero unless the shot crosses a crowded control position:
  - Saumarez's director: 11 killed;
  - Gneisenau's foretop director: 6;
  - Prince of Wales's compass platform: most of her 13.

  Elsewhere: Boise's one man hurt by "linoleum fragments", and Bismarck's, Duke of York's and Renown's clean holes.
- **Class E** kills through spall and ports:
  - Tiger's Q roof: 3 killed and 6 wounded, which 14 reproduces;
  - Von der Tann's after CT: 3;
  - Markgraf's casemate: its gun crew;
  - Princess Royal's X turret: 11 / 31, from a fragment through a gun port.

  The burst outside also wounds whoever is on deck: Mikasa's aft turret hit, 1 / 18.
- **Class G** is 15's: Achilles' director 4 / 2, Havock 8 killed, Exeter's torpedo crews.

### 2.2 By ship (R2)

Ship totals over counted hits. Flash fires are left out, as a separate mechanism, and so are sinkings (drowning isn't
a hit's casualties).

| Group | Ships | Hits | Killed / hit | Wounded / hit | Killed share | Ships' spread, killed / hit |
|---|---|---|---|---|---|---|
| G1 WWI capital ships, heavy APC (Dogger Bank, Jutland) | 18 | 120 | 1.94 | 3.00 | 0.39 | 0–4.5 (median 1.8) |
| G2a Russian battleships under Japanese shimose, 1904–05 | 5 | 133 | 0.58 | 1.81 | 0.24 | 0.3–0.9 |
| G2b Japanese ships under Russian AP and common, 1904–05 | 5 | 32 | 1.69 | 4.69 | 0.26 | 0–4.7 |
| G3 WWII capital ships, heavy AP (South Dakota, Prince of Wales, Bismarck) | 3 | 36 | 1.44 | 2.06 | 0.41 | 0–1.9 |
| G4 Medium HE close in on crowded WWII ships (Colorado, Tennessee, Pensacola, San Francisco) | 4 | 76 | 2.03 | 5.62 | 0.27 | 1.9–2.8 |
| G5 WWII cruisers, 6–11 in AP and HE | 9 | 61 | 2.34 | 3.47 | 0.43 | 0.2–8.7 |
| G6a WWI cruisers fighting from behind armour, 4.1 in (Kent, Cornwall, Arethusa, Glasgow) | 5 | 98 | 0.17 | 0.33 | 0.35 | 0–0.5 |
| G6b WWI light cruisers, open-backed shields (Chester, Southampton) | 2 | 35 | 2.00 | 2.37 | 0.46 | 1.9–2.1 |
| G7 Destroyers, WWII, 4.7–8 in | 11 | 77 | 2.21 | 4.54 | 0.32 | 0.5–11 |

### 2.3 Five things to take from it

1. **Calibre barely moves it.**
   - A heavy-AP hit on a capital ship (G1, G3: 1.4–1.9 killed) costs no more than a 5 in hit on a destroyer
     (G7: 2.2).
   - Within G1, the German 30.5 cm and the British 15 in are indistinguishable once the flash fires are out.
   - The reason is physical (§4.1). Bigger shells break into bigger pieces, not more of them: a 16 in AP throws about
     a third of a 5 in AA shell's man-stopping pieces. And a burst's reach in a ship is bounded by its compartment.
2. **Where the men stand moves it 10×.**
   - G6a against G6b is the same German 10.5–15 cm shells on British cruisers. Kent took 38 hits for 6 killed, and
     Cornwall 18 for none. Chester and Southampton, with gun crews on the open deck behind open-backed shields, lost
     2 a hit. Chester's wounded lost legs below the shields.
   - G4 against G3: crowded WWII AA crews take 5.6 wounded a hit against 2.1.
3. **About a third die, more where it's close and closed.**
   - The killed share runs 0.24–0.46.
   - Open fragment bursts give many wounded per killed: North Carolina 3 / 44, Wichita 0 / 14, Mikasa's per-hit list
     34 / 94.
   - Small closed spaces and powder fires give killed ≥ wounded: Patterson 10 / 8, Fletcher 8 / 3, Duncan's fireroom
     "all killed", Boise about 100 of 107.
   - The NHRC study of 118 USN gunfire incidents has killed:wounded at 0.84. Of its 1,994 gunfire wounds, 59% were
     penetrating (fragments), 15% multiple, 7% burns and 2% concussion [SRC NHRC 91-1].
   - Cussen found fragment wounds in 63% of Exeter's wounded, 18 burns and no blast injuries. Blast killed in her CPO
     flat but left no wounded to count.
4. **Fine fragments wound, they don't kill.** Kostenko: Oryol's losses were behind armour, from splinters through
   ports and slits, with shallow wounds from the fine shimose fragments, plus concussion and fumes. G2a's killed share
   of 0.24 is the lowest.
5. **Propellant is its own mechanism.**
   - Seydlitz's after turrets: about 160 killed of about 165.
   - Derfflinger C: 73 killed and 5 wounded of 78.
   - Lion's Q: about 64.
   - Malaya's battery: 104 of 121 casualties, 65 dead with those who died of burns.
   - Fuji: 8 / 9. Kent's casemate: 10 of her 14.
   - Boise's magazines: about 100.

   These kill a share of everyone the flash reaches, not by distance.

---

## 3. Why the current rule can't be fitted

The rule (gunfire_ref `resolve()`, `Effects.cs`):

```
casualties = density × (0.8·πR_wreck² + 0.2·π(R_blast² − R_wreck²)) × U(0.3, 1.7),   R = (1.6, 3.5)·W^⅓
pass-through: density × depth × 1 m × 0.5
```

- **The areas go as W^⅔.** From 5 in AAC (3.1 kg) to 16 in HC (66 kg) the area grows 7.7×, and 16 in AP's 18 kg
  makes 3.2×. The record is flat (§2.3 point 1).
- **A disc on a density has no walls.** Men behind a bulkhead count like men in the open. A big burst in a small
  space counts men who aren't there. The 0.8 and 0.2 are blind to cover, posture and plate.
- **It gives one number,** with no killed/wounded split. The game's crew, damage-control parties (09, 10) and
  command need the dead and the out-of-action apart.
- **The pass-through line** gives Saumarez's director 0.7 casualties against 11 killed. It gives a passage through
  an empty flat the same thin line as one through a crowded one.
- **U(0.3, 1.7) is narrow and symmetric.** The record's per-hit distribution is mostly zeros with a long tail: in
  class A the median is 6 and the max 40, and over all hits more than half kill nobody.
- **The level is low.** 08 §6.3 has the reference's own E[casualties]: 2.3 a hit for 5 in AAC on a Fletcher, against
  6.8 for destroyers in the record; about 2 for 16 in AP on a South Dakota, against 3.5 for G3. On the stand-in ships
  here (M5) it gives 0.3–4.0 a hit where the record has 0.5–7.6.

The constants can't fix this, because the shape is wrong: which men are in reach is a question of cells, not of a
disc.

---

## 4. Proposed model

Per hit, per **group** of men: a cell's `battle_crew`, or the part of it within a distance band. A group has `n`
men between `r0` and `r1` from the burst, on a layer (a deck, `dim` 2) or in a volume (a room, `dim` 3), behind
`t` mm of plate, in a space of a **cover** kind.

### 4.1 Fragments

**Man-stopping pieces, from 13.** `fragmentation_ref.burst()` gives each spray's mass, launch speed and Mott
constant: the body's side spray, the ogive's nose spray, the base's tail spray. A piece is man-stopping at r if it
still carries **80 J** there after TP-12's drag (58 ft-lb, the classic criterion; 14 and 15 use the same [SRC]). So
`N(r)` is each spray's Mott count above the mass that keeps 80 J to r. The nose slug and the base plug are single
pieces and are left out.

| Shell | Side-spray speed | Mean piece | N at 5 m |
|---|---|---|---|
| 4.1 in HE | 795 m/s | 13 g | 830 |
| 5 in/38 AAC | 957 | 16 g | 1,030 |
| 6 in HE | 934 | 25 g | 1,270 |
| 8 in AP | 491 | 234 g | 280 |
| 8 in HE | 748 | 81 g | 1,090 |
| 12 in APC (1916) | 574 | 380 g | 550 |
| 15 in APC (WWII) | 497 | 0.9 kg | 530 |
| 16 in AP Mk 8 | 371 | 2.1 kg | 320 |
| 16 in HC Mk 13 | 741 | 0.4 kg | 1,660 |

- **Bigger shells break into bigger pieces, not more of them** (13 §0.5). For people, one 2 kg piece is one hit, as
  is one 16 g piece.
- **Drag barely matters inside a ship.** N falls by 2–15% from 1 m to 15 m (23% for a 12 pdr's small pieces).
- **The 80 J line barely matters either.** Mott puts few pieces below 1 g, so a 10 J line adds 2–13%.

**Flux on one exposed man at r:**

```
λ(r) = S · N(r) · g · A · T(t, r) · band / (4π r²)
```

- **A = 0.5 m²,** a standing man's presented area, as 15 has it (crouched 0.37, prone 0.1 [SRC Zecevic]).
- **g = 0.5** for a burst on a plate: half the fragments go into the plate. It is 1 for a burst inside a space.
- **T(t, r)** is the share through `t` mm of plate at r: logistic in log t through Okun's 50% and 10% points (the
  existing `FRAG_TABLE`, ×1.35 for HE). The engine should use 13's THOR check per mass class instead.
- **band:** 13's ring holds about 85% of the body's mass within ±7.5°, leaning forward with the shell's speed. Over a
  room it is coarsened [EST]: a group is in the ring with p = 0.26 and takes ×2.9, otherwise ×0.34 (mean 1). It is
  one draw per group per hit, and the main source of the scatter. The engine can use 13's ring per cell instead.
- **S** is the people's flux scale (§4.2).

**Cover:** a share `c` of the group is exposed, and the rest take `0.1·λ` [EST]:

| Kind of space | c | |
|---|---|---|
| Open: weather deck, AA platform, open bridge | 0.8 | |
| Station: bridge, CIC, director, signal bridge | 0.7 | |
| Flat, mess deck, cabins, passages | 0.6 | |
| Casemate battery, gun deck | 0.6 | |
| Gunhouse, handling room | 0.45 | breeches, cradles, rammers |
| Machinery | 0.3 | boilers and turbines between the burst and most of the watch |

**Per man:**

```
P(casualty) = c·(1 − e^(−λ)) + (1 − c)·(1 − e^(−0.1λ))
P(killed)   = c·(1 − e^(−qλ)) + (1 − c)·(1 − e^(−0.1qλ))
```

Both are averaged over the group's band of r (24 points, weights r^(dim−1)). Near the burst a man takes many hits
and dies; at the edge one piece wounds him.

### 4.2 The fit: S and q

- **The two constants** are fitted by least squares in log(x + 0.5) on killed and wounded per hit over the nine
  ship groups (R2), plus log(x + 1) over the 13 single hits (M1, the flashes excluded), weighted to count as much as
  the groups:
  - **S = 8.7.** 13's free field is 1.
  - **q = 0.04.** A man-stopping piece kills one time in 25. The killed share comes from the near-in men, who take
    dozens.
- **S belongs to people only.** 13's field is calibrated for plates (Okun's tables, WDR 51's splinter rule, Boise's
  piece) and stays as it is for parts. That a man in a steel compartment meets about 9× the free-field flux is
  plausible [EST]:
  - **Ricochets:** fragments that don't get through a boundary come back off it.
  - **Debris:** the burst and its fragments break up fittings, joiner work and structure. Boise's man was hurt by
    linoleum, and Exeter's "secondary splinters" are in Cussen.
  - **Small pieces:** Mott undercounts them, and the record's "wounded" include light wounds.
  - **Placement:** the stand-in ships put the men coarsely.

  The engine's refit (§5.3) will tell how much of S is the last.
- **They trade off:**
  - S from 4 to 18 and q from 0.02 to 0.11 stay within 15% of the best error.
  - The ship groups alone want S = 5.6, q = 0.08. The single hits alone want S = 18, q = 0.02; their men present
    are estimates.
  - S·q is 0.45, 0.36 and 0.35 in the three fits. That is the killed rate where the flux is thin. The error surface
    is flat over a wider band, 0.2–0.8, so S·q is a guide for the refit, not a pin.
- **No calibre or filler term on q.** Heavier pieces kill more often in a ballistics lab. The record doesn't want
  it: a q rising with fragment mass made the heavy-AP groups too deadly against the medium-HE ones. The fine
  shimose fragments' low killed share (G2a) comes out of their numbers: 3,100 pieces from a 12 in shimose shell,
  against 550 from a 12 in APC.

### 4.3 A shot that doesn't burst

- **Passing through.** Men in the shot's own path are killed. That is the share of the group in a tube of half a
  calibre plus a man's half-width, `path`.
- **The disc ahead of it.** Each plate perforated drives a disc of 1.5 calibres ahead in **10 g pieces**, in a
  **35° cone** (14's debris cone), with **q_sec = 0.35** [EST]. For men in the cone (`cone`, the share of the group in
  it):

  ```
  λ = S · N_sec · A / (Ω r²),    N_sec = ρ t π (0.75·cal)² / 0.010 kg
  ```

  - An 11 in shell through a 10 mm director wall drives about 1,100 pieces, nearly everyone in a 2 m box (Saumarez:
    11).
  - A 6 in AP through a 12 mm side into a near-empty flat gets a man or nobody (Boise hit 3: 0 / 1).
- **A dud** is the same as a shot passing through.
- **E1's holed-but-broken shell** (fragments at about 0.15× through the hole) is a weak burst at the plate, with 13's
  field scaled.

### 4.4 Armour that holds

That is **14**: the spall cloud and the plug for u in the spall band, and a contact burst holing a plate at an
opening or edge (Tiger's Q roof: 8.3 casualties by 14's model against the record's 9). The reference here carries a
crude stand-in only so its ships add up: 0.15 of the disc off the back on a quarter of armour hits, and 0.03 of the
outside burst's flux through ports and slits (Kostenko; Princess Royal's X turret) [EST]. The ports and slits are not
in 14 and should be added to it. The burst outside acts on the open deck as any contact burst does.

### 4.5 Blast

From 11, unchanged:
- **Confined:** the burst space's QSP (and the cascade's in every reached space). Killed: Bowen, 50% at 0.42 MPa.
  Out of action: 50% at 0.12 MPa.
- **Open:** killed within 1.3·W^⅓, half out of action to 3.2·W^⅓.
- Blast and fragments combine as independent risks. In the model as in the record, blast matters only for big
  charges in small spaces: 16 in HC in a 300 m³ flat kills all 6 men (M3), as Exeter's CPO flat did.

### 4.6 Propellant flash

- The magazine chain (07) decides whether the flash happens and where it reaches: gunhouse, working chamber, trunk,
  handling room, magazine, or the casemate battery.
- Its casualties are a share of the men in every space it reaches [FIT]:

  | Where | Killed | Wounded | Fitted to |
  |---|---|---|---|
  | Enclosed turret train | 0.92 | 0.06 | Derfflinger C 73 / 5 of 78; Seydlitz's after turrets ~160 of ~165; Lion's Q |
  | Open casemate battery | 0.54 | 0.32 | Malaya's 121: 65 dead (with those who died of burns), 39 wounded |

- The doors decide the rest. Malaya's survivors got out through a forward door; others broke jammed doors down with
  hoses.

### 4.7 One hit, start to end

```
for each group in reach (the burst cell, the cells fragments get through, the cells on the shot's path):
    draw band (in the ring: p 0.26)                         # per group
    pk, pc = combine(fragments, debris or spall, blast)     # independent risks
    for the n men: killed with pk, else wounded with pc − pk  # binomial
propellant flash (07): a share of the men it reaches, as 4.6
near misses: 15's exposed-crew hits, with the same P(killed) = 1 − e^(−qλ)
```

There is no random factor afterwards. The scatter is the hit point, the ring, the spall roll and the men.

### 4.8 Wounded: out of action or back at their station

- The groups' wounded lean toward the serious, but the per-hit counts include light wounds:
  - Asahi: 5 serious of 24;
  - Malaya and Warspite: about half serious, by the two counts.
- **Proposal [EST]:** 45% of the wounded are out for the action. The rest return to their station after first aid,
  15–45 min. A wounded man at a station still counts toward its crew while he's there.
- Killed is final. The record's killed include those who died of wounds where the source knew them.
- This is what 09 and 10 need. Parties are "a finite, located, killable resource". A party's `men` drops by the
  killed and the serious wounded, and its light wounded come back.

### 4.9 Posture: the player's lever

- Cover depends on what the men are doing. RN practice at Jutland was to clear the upper deck. Japanese AA crews
  stayed at their mounts. Chester's crews stood at open guns.
- **Proposal:** a ship-wide stance, *closed up* or *under cover*.
  - Under cover: the men at open stations and AA mounts go behind plating or lie down. Their cover drops to 0.2
    (prone A = 0.1 against 0.5), and the stations they leave stop working.
  - It is the trade a captain made. The record's G6a against G6b, 12× apart, is that trade.

---

## 5. In the engine

### 5.1 What it needs

- **Men per cell:** `battle_crew` on rooms and cells (shipgen README). Gun crews are at the mounts; 0.4 of a turret's
  crew is in its barbette; engineers are in the machinery by volume; the command party is on the bridge; the rest
  are parties in the hull's flats by volume. The model asks for nothing more about where men are. It asks that it be
  right: §6 shows the result is most sensitive to how many stand in the open.
- **Cover by room kind:** a table, as `RoomVital` is.
- **Shell:** 13's precompiled fragment table per shell (its mass classes and speeds). N(r) is that table summed over
  the classes that keep 80 J to r: a few distances, worked out at load.
- **The cells in reach:** already enumerated by `Effects`. They are the burst space, the spaces whose boundaries its
  fragments get through, and the cells on the shot's path. 11's cascade adds the gas's spaces.

### 5.2 Cost

- About 24 λ evaluations per group (a table would do with 8), times two bands, for a handful of groups.
- One exp each, no allocation: microseconds per hit.
- The binomial draw on n men is a loop of n uniforms, or a normal approximation above 30.

### 5.3 Calibration in the engine (the real fit)

The stand-in ships in `casualty_ref.py` §8 are coarse: seven or eight hit situations each, the men estimated from
shipgen's rules. The engine has the real cells. So:

1. **Pick one ship per group:**

   | Group | Ship | Shooter |
   |---|---|---|
   | G1 | a Lion or a Derfflinger | 13.5 in / 30.5 cm APC 1916 at 12–15 km |
   | G3 | a South Dakota | 8 in AP at 8–12 km, and 14 in |
   | G4 | a Colorado | 6 in HE at 5 km |
   | G5 | an Exeter | 28 cm |
   | G6a | a County | 10.5 cm at 10 km |
   | G6b | a Chester | 15 cm at 5 km |
   | G7 | a Fletcher | 5 in AAC |

2. **Run** `shipgen hits TARGET --shooter … --n 4000` (it already reports crew a hit) with the killed/wounded split
   added.
3. **Fit S,** in log(x + 0.5) against R2's killed and wounded per hit, with q starting from S·q ≈ 0.4. Refit q only
   if the killed shares are off by more than 0.1.
4. **Check the shape as well as the level:**
   - the share of hits killing nobody: more than 40% on capital ships, 15–30% on destroyers;
   - the 99th percentile on capital ships: 8–20 killed a hit, flashes aside;
   - M1's single hits within ×2.

   G2a and G6a are the hard ones: men behind armour, upper works empty. If the engine's placement puts men there,
   §4.9's stance is the fix, not the constants.

### 5.4 Tests (wide bands)

1. A 16 in AP burst in a battleship flat with 6 men, and a 5 in AAC burst in a destroyer flat with 6, are within ×1.5
   of each other in casualties.
2. A shot through an unmanned space without bursting causes 0. Through a manned 2 m director, at least 60% of its
   crew are killed.
3. A 5 in AAC on a crowded AA platform (40 within 5 m, 70 beyond): 10–30 killed, 25–60 wounded.
4. The killed share over a destroyer's hits is 0.25–0.4.
5. A turret flash kills ≥ 85% of the men it reaches; a battery flash, 45–65%.
6. A Jutland capital ship over 15 hits, without flash: 15–45 killed, 30–70 wounded.
7. Parts take 13's free-field fragments; only people take S.
8. No allocation per hit; replays hold.

---

## 6. What the model gives

From `casualty_ref.py`, at the fitted S and q. The men present in M1 are estimates from the ships' plans and the
reports' words.

### M1. Single hits

| Hit | Men | Model killed / wounded | Record | Current rule |
|---|---|---|---|---|
| Colorado 1945, 5 in AAC at the air defense station | 115 | 20.2 / 43.3 | 20 / 48 | 5.0 |
| North Carolina 1945, 5 in AAC at a 5 in director's base | 68 | 2.0 / 14.6 | 3 / 44 | 3.0 |
| Cleopatra 1942, 6 in on the after bridge | 28 | 8.9 / 15.9 | 15 / 5 | 7.4 |
| Nachi 1943, 6 in on the signal bridge | 50 | 8.2 / 18.7 | 11 / 21 | 3.1 |
| Tiger 1915, 11 in APC in the intelligence office | 32 | 12.8 / 7.5 | 7 / 9 | 10.3 |
| Mikasa 1904, 12 in on the forecastle | 65 | 3.3 / 19.2 | 7 / 16 | 2.3 |
| Murphy 1942, 5.5 in in the after engine room | 28 | 3.9 / 14.4 | 3 / 26 | 4.8 |
| Wichita 1942, 194 mm below decks | 32 | 2.3 / 12.1 | 0 / 14 | 3.9 |
| De Ruyter 1942, 8 in in the auxiliary motor room | 16 | 0.6 / 4.2 | 1 / 6 | 1.4 |
| Saumarez 1943, 11 in through the director | 11 | 9.6 / 0.0 | 11 / 0 | 0.7 |
| Gneisenau 1940, 15 in through the foretop director | 10 | 5.9 / 0.0 | 6 / 0 | 0.5 |
| Boise 1942 hit 3, through the side | 3 | 0.4 / 0.0 | 0 / 1 | 0.1 |
| Von der Tann 1916, 15 in on the after CT (stand-in for 14) | 6 | 3.2 / 1.0 | 3 / 0 | 0.7 |
| Derfflinger 1916, C turret flash | 78 | 71.8 / 4.7 | 73 / 5 | – |
| Malaya 1916, 6 in battery flash | 121 | 65.3 / 38.7 | 65 / 39 | – |

- **The current rule** gets the order right only where the burst is big and the space small (Tiger). It misses
  through-shots by 10×.
- **The model's misses:**
  - North Carolina's 44 wounded need more men in the open than I put there.
  - Cleopatra's 15 killed of 28 need a closer burst than the average.
  - Tiger's office needs fewer men in it.

  All three are geometry, not constants.

### M2. Whole ships against the record (per hit)

| Group | Ship and shell | Model killed / wounded | Record | No casualties | Killed none | Killed: median / 90th / 99th pct |
|---|---|---|---|---|---|---|
| G1 | Jutland capital ship, 13.5 in APC | 1.68 / 4.08 | 1.94 / 3.00 | 24% | 46% | 1 / 7 / 9 |
| G1 | Jutland capital ship, 30.5 cm APC | 1.34 / 3.15 | 1.94 / 3.00 | 27% | 55% | 0 / 5 / 8 |
| G2a | 1905 Russian battleship, shimose (12 in 0.25, 6 in 0.5, 12 pdr 0.25) | 2.08 / 4.18 | 0.58 / 1.81 | 18% | 44% | 1 / 8 / 15 |
| G2b | 1904 Japanese battleship, Russian common (12 in 0.4, 6 in 0.6) | 1.12 / 2.25 | 1.69 / 4.69 | 32% | 61% | 0 / 3 / 8 |
| G3 | WWII battleship, 8 in AP (South Dakota's mix) | 1.18 / 2.51 | 1.44 / 2.06 | 20% | 49% | 1 / 3 / 7 |
| G3 | WWII battleship, 15 in AP | 1.81 / 4.13 | 1.44 / 2.06 | 18% | 36% | 1 / 5 / 10 |
| G4 | WWII battleship, 6 in HE close in, AA closed up | 1.65 / 5.98 | 2.03 / 5.62 | 13% | 44% | 1 / 4 / 17 |
| G5 | WWII cruiser, 8 in AP | 1.00 / 3.75 | 2.34 / 3.47 | 16% | 48% | 1 / 3 / 5 |
| G5 | WWII cruiser, 11 in APC | 1.20 / 4.38 | 2.34 / 3.47 | 15% | 42% | 1 / 3 / 7 |
| G6a | WWI armoured cruiser, 4.1 in HE | 1.37 / 1.41 | 0.17 / 0.33 | 20% | 50% | 1 / 5 / 8 |
| G6b | WWI light cruiser, 5.9 in common, open guns | 1.07 / 2.78 | 2.00 / 2.37 | 18% | 52% | 0 / 3 / 6 |
| G7 | Destroyer, 5 in AAC | 2.34 / 6.05 | 2.21 / 4.54 | 6% | 20% | 2 / 6 / 11 |
| G7 | Destroyer, 8 in HE | 2.79 / 6.79 | 2.21 / 4.54 | 6% | 16% | 2 / 6 / 12 |

- **Within ×1.6 on both:** G1, G3, G4 and G7.
- **Low on killed, about ×0.5:**
  - G5, mostly Exeter's 61 killed in 7 hits: her bursts in crowded spaces are the tail.
  - G6b: Chester's and Southampton's gun crews, more of them in the open than the stand-in has.
  - G2b, also ×0.5 on wounded: Mikasa's and Asahi's open batteries and upper decks were manned.
- **Too high: G2a ×3.6 and G6a ×8.** Their crews were behind armour, with nobody on the upper works, and their hit
  counts include many small shells and splinter holes. The stand-ins don't know that, and the engine's ships might.
- **The shape is right:** 16–61% of hits kill nobody, the median is 0–2, and the 99th percentile is 5–17.

### M3. One burst, one space: the calibre curve

Men spread evenly. Killed / wounded:

| Shell | N at 5 m | Flat 300 m³, 6 men | Engine room 1,500 m³, 30 men | Open deck, 20 within 10 m | Bridge, 25 within 6 m |
|---|---|---|---|---|---|
| 4.1 in HE | 833 | 2.2 / 3.2 | 3.8 / 15.0 | 4.4 / 11.6 | 6.7 / 14.3 |
| 5 in/38 AAC | 1,026 | 2.4 / 3.1 | 4.4 / 15.6 | 5.4 / 11.6 | 8.2 / 14.0 |
| 6 in HE | 1,272 | 2.6 / 3.0 | 5.0 / 16.1 | 6.2 / 11.4 | 9.8 / 13.5 |
| 8 in AP | 282 | 1.2 / 3.4 | 1.7 / 10.9 | 2.8 / 10.0 | 4.8 / 13.5 |
| 8 in HE | 1,091 | 2.5 / 3.3 | 4.6 / 15.7 | 6.1 / 11.4 | 10.5 / 12.8 |
| 12 in APC (1916) | 547 | 1.7 / 4.0 | 2.8 / 13.7 | 5.0 / 11.2 | 9.5 / 12.9 |
| 15 in APC (WWII) | 531 | 1.8 / 4.2 | 2.8 / 14.9 | 5.4 / 11.6 | 12.1 / 10.7 |
| 16 in AP Mk 8 | 322 | 1.3 / 4.6 | 1.9 / 12.1 | 4.1 / 11.2 | 10.4 / 11.5 |
| 16 in HC Mk 13 | 1,658 | 6.0 / 0.0 | 5.9 / 22.3 | 10.0 / 9.2 | 21.4 / 3.3 |

- **Heavy AP is no worse than medium HE per man in reach,** and kills fewer in a big space. Its edge is reach:
  bigger pieces through bulkheads into the next cells (the flats' neighbours in M4), and a delayed burst deep
  inside.
- **Heavy HE is the exception.** 16 in HC kills everyone in a small space by blast, as 11 has it.

### M4. The current rule on the same men

A burst in a 300 m³ flat with 6 men, 18 more in the flats around behind 6 mm:

| Shell | Current rule | Model killed / wounded |
|---|---|---|
| 5 in/38 AAC | 1.6 | 2.7 / 6.7 |
| 6 in HE | 2.4 | 3.2 / 8.3 |
| 8 in HE | 3.3 | 3.3 / 9.8 |
| 12 in APC (1916) | 4.2 | 2.3 / 9.5 |
| 16 in AP Mk 8 | 5.1 | 1.7 / 9.5 |
| 16 in HC Mk 13 | 12.3 | 8.1 / 9.4 |

The rule rises 8× from 5 in to 16 in HC; the model rises 1.8×.

### M5. The current rule on the stand-in ships (per hit)

| Group | Ship and shell | Current rule | Model, killed + wounded | Record |
|---|---|---|---|---|
| G1 | Jutland capital ship, 13.5 in APC | 1.6 | 5.6 | 4.9 |
| G1 | Jutland capital ship, 30.5 cm APC | 1.1 | 4.4 | 4.9 |
| G2a | 1905 Russian battleship, shimose | 1.1 | 6.3 | 2.4 |
| G2b | 1904 Japanese battleship, Russian common | 0.4 | 3.4 | 6.4 |
| G3 | WWII battleship, 8 in AP | 0.4 | 3.7 | 3.5 |
| G3 | WWII battleship, 15 in AP | 1.6 | 5.9 | 3.5 |
| G4 | WWII battleship, 6 in HE close in | 1.2 | 7.5 | 7.6 |
| G5 | WWII cruiser, 8 in AP | 1.0 | 4.7 | 5.8 |
| G5 | WWII cruiser, 11 in APC | 1.6 | 5.5 | 5.8 |
| G6a | WWI armoured cruiser, 4.1 in HE | 0.3 | 2.8 | 0.5 |
| G6b | WWI light cruiser, 5.9 in, open guns | 0.7 | 3.8 | 4.4 |
| G7 | Destroyer, 5 in AAC | 2.0 | 8.4 | 6.8 |
| G7 | Destroyer, 8 in HE | 4.0 | 9.6 | 6.8 |

---

## 7. Uncertainties and next sources

- **The men present are the model's largest input, and the record's least known.** The fit can't separate "fewer
  men in reach" from "lower lethality". Hence S's wide band, and hence §5.3.
- **S's meaning.** About 9× the free-field flux is fitted, not shown. Ricochets and debris inside steel
  compartments are real (the WDRs' "secondary fragments"), but no source here measures them. A test of fragment
  ricochet in a closed steel box would settle it.
- **Cover and the ring** are [EST]. They set the zero share and the scatter more than the mean.
- **The no-burst disc** (1.5 calibres, 10 g, 35°) fits two directors and a cabin flat. 14's behind-armour-debris
  numbers are the better basis once its plug model is in.
- **"Wounded" mixes serious and light** across sources. G1's 3.0 a hit leans serious, and North Carolina's 44
  include the light.
- **Not found online:**
  - Campbell's *Jutland: An Analysis of the Fighting*: per-hit casualty notes for nearly every Jutland hit, which
    would double the per-hit set;
  - the ADM 267 damage reports (Prince of Wales, Exeter);
  - the USN ships' action reports with casualty lists by battle station (South Dakota, San Francisco, Colorado at
    Tinian, Pensacola), at NARA / Fold3.

  These three would turn the ship-level layer into per-hit data.
- **Left out on purpose:** drowning in a sinking ship; fire and smoke over time (09/10's fire model); steam (a burst
  in a boiler room should add a scald share to its machinery cells [EST, Duncan's fireroom "all killed"]); and near
  misses (15).

---

## Sources

**The record**
- USN War Damage Reports, index: http://www.ibiblio.org/hyperwar/USN/rep/WDR/ ; South Dakota (WDR 57):
  https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html
  ; Boise: https://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCL47/WarDamageReportCL47.html
- Lundgren, South Dakota: https://navweaps.com/index_lundgren/South_Dakota_Damage_Analysis.php
- ONI Combat Narrative, Cape Esperance: https://ibiblio.org/hyperwar/USN/USN-CN-Esperance/index.html
- Salt Lake City at Komandorski: https://ussslcca25.com/komadors.htm
- Blood (1991), NHRC 91-1, shipboard casualties by weapon: https://www.generalstaff.org/BBOW/LOV-DUP/ADA252892_NHRC_90-01.pdf
- Colorado, January 1945: https://usnamemorialhall.org/honorees/ens-john-p-bock-usn/ ; North Carolina:
  https://battleshipnc.com/friendly-fire/ ; Texas at Cherbourg: https://battleshiptexas.info/html/History/1944-06-25.html
- US ships damaged in WWII: https://en.wikipedia.org/wiki/List_of_U.S._Navy_ships_sunk_or_damaged_in_action_during_World_War_II
  ; Kalinin Bay (DANFS): https://history.navy.mil/research/histories/ship-histories/danfs/k/kalinin-bay.html
- Japanese General Staff, the Yellow Sea (USNI 1914): https://www.usni.org/magazines/proceedings/1914/september/battle-yellow-sea-official-version-japanese-general-staff
- Kostenko, *Na "Orle" v Tsusime*: https://militera.lib.ru/memo/russian/kostenko_vp/38.html ,
  https://militera.lib.ru/memo/russian/kostenko_vp/44.html
- Jutland casualties (Jellicoe's despatch, annexe): https://britishbattles.com/battle-of-jutland-part-v-annexe ;
  https://www.northeastmedals.co.uk/britishguide/jutland/jellicoe_dispatch_1916.htm ; damage to major ships:
  https://en.wikipedia.org/wiki/Damage_to_major_ships_at_the_Battle_of_Jutland
- *Lessons of Jutland affecting design of turret armor* (USNI 1925): https://www.usni.org/magazines/proceedings/1925/april/lessons-jutland-affecting-design-turret-armor
- Princess Royal at Jutland: https://www.dreadnoughtproject.org/tfs/index.php/H.M.S._Princess_Royal_at_the_Battle_of_Jutland
- Malaya's battery: https://www.mastermariners.org.au/stories-from-the-past/4442-hms-malaya-at-jutland ;
  https://www.tapatalk.com/groups/warships1discussionboards/hms-malaya-at-jutland-including-6-inch-battery-t51408.html
- Heligoland Bight and Dogger Bank: https://www.naval-history.net/WW1Battle1408HeligolandBight.htm ;
  https://www.naval-history.net/WW1Battle1501DoggerBank.htm
- Exeter at the River Plate: https://navyhistory.au/hms-exeter-at-the-battle-of-river-plate/2/ ; River Plate:
  https://kbismarck.com/river-plate-battle.html ; https://www.navweaps.com/index_oob/OOB_WWII_Atlantic/OOB_WWII_River-Plate.php
- Prince of Wales's damage, Denmark Strait: https://www.hmshood.org.uk/history/denmarkstrait/pofw_damage1.htm ;
  https://www.kbismarck.com/ds-barticle.pdf
- Per-ship pages cited row by row in `16_casualty_records.csv` (165 URLs): Wikipedia, DANFS, combinedfleet,
  naval-history.net.

**Fragments and wounding**
- `13_fragmentation.md` and its sources (Gurney, Mott, TP-12's drag, THOR, WDR 51's ring).
- Zecevic et al. (2008), lethal zones and presented areas: https://yadda.icm.edu.pl/baztech/element/bwmeta1.element.baztech-article-BAT1-0034-0051/c/httpwww_wydawnictwa_ipo_waw_plcejemvol-5-num-2008zecevic.pdf
- `14_spall_behind_armour.md` (the 79 J criterion, the debris cone); `15_near_misses.md` (exposed crew).
- Blast lethality: as 11 (Bowen 1968).
