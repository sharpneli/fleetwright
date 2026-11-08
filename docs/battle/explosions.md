# Plan: one explosion (a 4.5 deepening)

Status: **decided 2026-11-05** ("Decided" at the end). EX0-EX3 are built and EX2 wires the network and girder into the game (2026-11-06); reviewed the same day, its bugs fixed and its code unified ("The review"). **Pick up at "Where to pick up", next.**

The user, 2026-11-05: "Explosion is an explosion. The code should basically not have a special case for magazine
explosion in on itself and just run all the explosions through the same path. We avoid special cases and try to build an
unified physics model. This becomes very important later on when we'll introduce bombs. And 5.4ton Tallboy will be let's
just say interesting to have. We don't want to introduce new physics at that point."

**This is the one place for the physics of an explosion:** what a release of energy does to a ship. How a flame gets to
the charges stays in `magazines.md` (the trains, doors, discipline, valves), how a shell gets to its burst point in
`shells-armour-damage.md` (the terminal walk), fragments in E1.

## Where to pick up

**EX5 is built and merged (2026-11-07, "EX5: the water"): torpedoes on the one path, the bubble's whip by research 13's law, a
boundary's mass, liquid that passes its push on and runs out, a protection's walls that stretch. Open there: a held protection
floods about three times the record; a destroyer is lost to one torpedo about half the time; research asked on protection walls.
Not yet run: the panel, `damage-history`, `dc-history` (EX7's). **EX6 is done (2026-11-07, "EX6a, and research 14", "EX6b: propellant by what reaches it", "EX6c: high-explosive stores", "EX6d: the anchors"): nothing silly in the panel. Next: EX7, the whole game (`damage-history`, `dc-history`, `fire-history`, `coarse-parity`, the AI's estimates).**
**EX4 is built (2026-11-07, "EX4: one near field"): one reach and one hole for every detonation, walking outwards through what it opens, hull holes in her section. The stow's bed is out (the research said it isn't physical; the user agreed), so magazines are back to breaking no ship in the panel, as the user expected, to be seen after EX4. Next: the user reads EX4's panel, then EX5.** See also "EX2: what it showed". **The destroyer the plan began with no longer sits on the "research" figures:** part of that match was a bug (the review's first finding), and she now reads worse than them at 5 and 9 hits, nothing fitted. **The user accepted that, 2026-11-06: those figures aren't a research result but an educated guess,** so they're no target. The two rules still keyed on the source wait for EX6 ("The review").

**Open, in order of what they decide:**

1. **Magazines at the margin.** The bed is out ("What the research found"), so it's back where EX3 left it: on the BCF's handling Lion's forward magazines read 0.82 in calm water, Tiger 0.49, Invincible 0.60. EX4 didn't touch a magazine's own race (a burn has no near field). Left to try: shell rooms' bursters (EX6's chain, now that the near field can carry a detonation through her), what a real magazine's boundaries are like. **Not tuned, and decision 3 says no balancing yet.**
2. **Whether a break's end is a switch.** `GirderEndShare` (0.12) still reads an end apart: Lion's fore magazines break her at 0.88, right at that line, so she's lost where a little further forward she'd lose her bow and live. The way out is to take off the smaller piece at any break and let the flooding say. Bigger than this step.
3. **The near field:** built (EX4). A Tallboy's charge in Lion's aft engine room still leaves her whole: her bottom holed, at 0.31 of her length where the moment is small. In a destroyer it now takes her whole section.
4. **A flooded magazine next to a dry one.** Q 95% flooded is contained (293 kPa, 1.5 t). A and B 95% flooded still light the rest (71 t) as hot gas reaches them; 470 K [EST] is the rule, and EX6's.
5. **cases and brass read alike,** with the research (R 32 against 28). The bed separates bags from both by more than before.
6. **12″ and 6″ HE never rupture a destroyer's side.** Friedrich Eckoldt isn't met; with the research.
7. **A magazine's cost:** 60-700 ms a race (Release; Bismarck's slow burns the worst, 90-170 s of gas in 4,500-8,600 steps), against about 20 wanted; a shell's 10-230 µs is on budget, but a shell whose gas lights a magazine (EX6a) pays the race. Over budget since EX5 at least. A longer step isn't the way, and exact tuning of the loop gave 19%: see "Cost and convergence". Threads were the first answer (workers.md W16, built 2026-11-07: a second later, off the sim thread); the user then had the gas take its own time in the world instead of one moment (workers.md "Explosions in time", drafted 2026-11-08).
8. **Estimates:** the whip's `k` 0.2, buckling 0.85, a wing's floor 0.5 m, a room's least half-width 1.5 m, ignition 470 K.
9. **The research request** "Big explosions in and beside ships" is open and bears on 1, 5 and 6.
10. **The game's step isn't converged on every race** ("Cost and convergence"): Iron Duke's S aft breaks her at the game's step and not at an eighth of it, Queen Mary's B and Dreadnought's Y light one magazine fewer.

## Why now

Yamato's nine HE at 1 km on the Fleet Destroyer (`shipgen hits destroyer --shooter yamato --kind he --range 1000`, two
hours of flooding and fire after; the research's figures from `gunfire_ref.py`):

| Hits | Crippled, ours / research | Lost, ours / research | Mean water, ours / research | Ships run |
|---|---|---|---|---|
| 1 | 41% / 36% | 1% / 7% | 239 t / 114 t | 240 |
| 5 | 92% / 89% | 23% / 44% | 1,443 t / 580 t | 48 |
| 9 | 99% / 99% | 47% / 79% | 2,487 t / 1,050 t | 200 |

The fighting ship is right; the hull isn't. A shell can't break a ship: `res.Broken` is set only in `Underwater`, and only
a magazine has a girder check of its own. Nine bursts of 62 kg along 115 m leave her with some compartments broken, and
the water stops once they're full.

## What there is today: four explosions

| | Shell burst (`Effects.Burst`, `Blast`) | Near miss (`NearMiss`) | Torpedo (`Underwater`) | Magazine (`Magazines.SetOff`) |
|---|---|---|---|---|
| **Gas pressure** | `0.28·η·Q·W/V` | none | none | `0.5·impetus·m/V` |
| **The gas leaves** | a blowdown's load factor (`Felt`) | | | choked flow, stepped (`Flow`) |
| **A boundary gives** | 0.06 MPa a mm, ×0.85 riveted | shock's D ≥ 5 | within the reach | a table scaled by plate: 0.031, 0.026, 0.014, 0.051 MPa a mm |
| **What it opens** | ¼ of the area | the contact hole | `1.4·W^⅔` m² | a table scaled by the face |
| **Wreck reach** | `1.0·W^⅓` | | `0.7·W^⅓` | its own `Aftermath` |
| **Hole near it** | `0.5·W^⅓`, `0.45·W^⅓·√(6/mm)` | `0.45·W^⅓·√(6/mm)` | see above, the same `W^⅔` | |
| **Hull girder** | none | none | a chance on `2·reach/depth` | `K·(1−d)` against the moment |
| **Spaces joined** | at once, one volume | | | the magazine's and its handling room's only |

Most of this is one physics written several times:

- **The gas is one equation.** Both are P = (γ−1)·E/V: `BlastHeatK` is γ−1, `η·Q` the filler's energy, and a propellant's
  impetus already is (γ−1)·e. A detonation is the limit of the race where everything is released in the first step.
- **The torpedo's hole is the contact rule's scaling.** `π·0.45²·6/mm·W^⅔` is 1.4·W^⅔ only at 2.7 mm of plate: the
  difference is the water tamping it, which belongs to where the charge is, not to what it was.
- **`Magazines.Girder` is the general check,** with `d` fed only by a magazine's own table.

**Found on the way:** `Blast.Cascade` never reads `JoinOpenM2`, it only adds to it. The second shell of a salvo finds every
bulkhead the first one tore still whole for its gas, and tearing it again adds another quarter of its area.

## The exception this retires, and why it was there

`magazines.md` decision 6 (the user, 2026-10-20): the race's boundaries go by the research's table, not E5's rules on the
ship's own plating. On her plating "the gas spreads into the ship faster than it builds, and every packing plateaus at
0.4-0.5 MPa", so it never reached the girder. Five versions were tried: tears as vents, a fill model, two zones, the second
zone loaded, a panel's delay. **This is the plan's main risk, so it's tested first (EX1).**

Why it may go differently this time, and why it may not:

- **For:** `Cascade` merges every space it reaches into one volume at once (11 §6 names this as an uncertainty). All five
  attempts were cuts at this; a network of real volumes joined by real openings hasn't been tried.
- **Against (an estimate, not measured):** hot gas crosses a 200 m hull in about 0.3-0.4 s, and bags burn for seconds. If
  so, even a right network mostly equalises, and the hull's whole volume is what the gas has to fill. Then gas pressure
  alone won't break a side counted as 73 mm, and something else in the physics is missing.

**Candidates for what's missing, to test one at a time** (`isolate-before-tuning`):

1. **A belt isn't a membrane.** `RuptureMPaMm` counts the belt's millimetres as if it held like plate. A belt is hung on
   thin plating and frames, and pushed from inside it may just come off with them. So the side's 73 mm may hold far less
   than its millimetres say.
2. **Hot near the fire.** One `MagazineHeatKeep` 0.5 for the whole ship; the gas by the burning stow hasn't lost its heat yet.
3. **What burns isn't only propellant.** Shell rooms' bursters and warheads going off beside the fire (07 §2.5): a real
   detonation inside the magazine's own boundaries.
4. **The flow's inertia and the tears' size.** How much a torn bulkhead really lets through in the first 100 ms.
5. **Loading rate.** A panel's 15 ms against a pressure rise over a second.

## The model

An `Explosion` is data, and says nothing of what set it off:

```
Explosion
  Energy    kg, explosive or propellant from the catalogue (Q_d, Q_c, air, η, RE; or impetus)
  Release   detonation (all at once) | burn law (β, α, form, surface, stow)   // the one difference that is physics
  Casing    its fragments, E1's (none for a bare charge)
  Point     in the ship's frame
```

**One path, `Explosions.Apply`.** Where the charge is decides what follows, from the geometry and never from its source:

1. **Where it is:** in a space, in a part, on a deck, in the air over water, at the hull under water, or in the water off it.
2. **The near field:** what's wrecked and holed by the shock, by `W^⅓`; tamped by water where water backs it.
3. **The fragments:** E1's, as they are.
4. **The gas** (below), where it's confined.
5. **The water:** the shock factor (`NearMiss.Nsf`) and the bubble, where it's in the sea.
6. **The hull girder** (below), after every explosion and as she floods.
7. **What it lights:** fire, a flash at a train's station; charges and warheads reached start an `Explosion` of their own.

### The gas: a network

Every cell is a volume with its own gas and pressure. Openings join them, the flow through each choked or not (`Flow`, as
the race has it). In the stepped solver:

- the source adds its gas to its cell: all in the first step for a detonation, by the burn law for propellant;
- gas moves through what's open now: doors, trunks, vents, **and what earlier explosions tore** (`JoinOpenM2`, the hull's
  holes), so a salvo's shells share one damaged ship;
- each boundary is loaded by the pressure difference across it. **A boundary gives at its own plate, by one rule, with
  no table,** and adds its tear to the network;
- a gunhouse lifts at its weight over its bore, a vent like any other.

Only cells with gas in them are stepped, with steps that lengthen as the pressure falls. It's linear in the edges reached
and allocates nothing (its state lives on the ship's condition, as `Cascade`'s does). It runs to the end at the moment of
the event, as the race does today, so replays hold.

**Cost budget** (DevRelease): a shell's burst under about 200 µs on average, a magazine under about 20 ms (the race took
2-13 ms at EX2; 60-700 ms by EX6, "Cost and convergence"). A hit costs about 760 µs all told today.

### The hull girder: one check

As the research has it (07 §3.1; 03's two pools, buoyancy and hull-girder strength), for every station and from any cause:

```
Z_res(x) = the section's elements still standing: deck, sides, bottom, longitudinal bulkheads
M_cap(x) = K·Z_res(x)/Z(x)
M_load(x) = still water + flooding's moment + wave·sea + whip
broken when M_load(x) ≥ M_cap(x)
```

- **Section loss comes from what's actually torn,** recorded per station by every explosion's holes and ruptures, and adds
  up over a battle. The fixed shares (deck 0.35, sides 0.45, bottom 0.2) go; the elements' areas and heights come from the
  class (`HullReport.Girder` has the plating and armour decks' moments already), with armour deck counted as the designer
  counts it.
- **Flooding loads it,** so a ship can break later and in a seaway: Bristol 4 minutes after her torpedo, Strong 39
  minutes, Beatty 4 h 20, Meredith 29 h; Hambleton lived on about 15% of her section.
- **Whip** is an explosion's impulse on the hull (07 §3.1, 05 A6), small for a burst in the air and large for a bubble
  under the keel.
- **A section all gone near an end takes the end off,** and she may live. This comes out of the moment being small there;
  `MagazineEndShare` goes.
- **`LossKind`** says how she went for the log and the look. `Magazine` stays as a name when charges did it (the look's
  event). The tiers become a report of what happened, read by no logic.

### Bombs, and the test of it all

A bomb is a projectile through `Terminal` and then an `Explosion`. Its fall, fuze and case are data; aircraft are another
plan. **The test of this plan is that a new weapon needs a catalogue row and no new physics.** EX8 sets off a Tallboy's
charge (from memory: about 2.4 t of Torpex in a 5.4 t bomb, to source) in and beside designs as an acceptance bench.

It's also where the models get stretched: 11 §6 tested `Pressure` up to 1 kg/m³ only. A Tallboy in a 1,500 m³ engine room is
about 1.6, a magazine's stow far more.

## Steps

Every step keeps `golden-check`, replays and the no-allocation tests. Each ends with its numbers here. Nothing is tuned.

- **EX0. The bench and the baseline.** `shipgen explode DESIGN --kg K --explosive E --at ROOM|X,Y,Z|water:X,Y,DEPTH`:
  any charge anywhere, through what exists today, printing what gave. The anchors written down with today's numbers: 11
  §5.5's seven tests, 15's near misses, the table above, Lion's four magazine cases (`magazines.md` V3), `magazines.md`
  "Where it ends up", one torpedo on a destroyer and on a capital ship, `damage-history`, `dc-history`, `losses`. No change
  to the sim.
- **EX1. The network, on the bench only: the gate.** The solver, run on the ship's own plating with no table. The ladder
  of candidates above, each alone. **Reported to the user whatever it shows:** does Lion with bags reach her girder, does
  brass burn out, does a 5″ HE stay in one space? If it can't get there we look for the physics and decide together; the
  game keeps today's model until it can.
- **EX2. One gas path.** Shell bursts and magazines through the network. Going: `Cascade`, the race's own loop,
  `PartGas`, `Felt`, `MagazineGiveKPa/Mm/M2/FaceM2`, decision 6. `Magazines` keeps the stow and the burn law, as a source.
- **EX3. One girder.** Section elements per station, losses adding up, flooding's moment. Shells can break a ship.
  `Magazines.Girder`, `MagazineD`, `MagazineEnd*` go. **This step answers the destroyer above.**
- **EX4. One near field.** The wreck reach and the holes as one rule for all sources, tamped by water by where it is. **Built 2026-11-07** ("EX4: one near field").
- **EX5. One water.** Contact and non-contact underwater as one model: the shock factor, the tamped hole, the bubble's
  whip, a TDS from its own volumes and plates through the network. `Underwater`'s separate rules, `TorpedoBreakShare` and
  the `Held` switch go. Its anchors are the hard ones (one torpedo usually sinks a destroyer, 7 of 31 lived; capital ships
  take several; 4.6's broken back). Mines (4.7) then need only a row.
  Worked in sub-steps (the user, 2026-11-07, after research 13 and "EX5: the water", below):
  - **EX5a.** The gas held by slopes between spaces. Built.
  - **EX5b. A boundary moves by its mass.** Every plate the gas loads is a panel: its mass (the plate, its armour, half the
    liquid of a full space behind it) driven by the pressure across it against its resistance, rising with its deflection to
    its rupture pressure at its tearing deflection; it moves only outward (plastic), and its bulge (4/π² of its area times its
    deflection) adds to the space behind the gas and takes from the space in front, with the work done. It gives at its
    tearing deflection, not at a pressure. A space a moving plate pushes into joins the run, so what's behind a wall answers
    it. The lag (`PanelMs`) and the load factor (`BlastLoadFactor`, a dynamic factor the panel now has of itself) go from the
    boundaries. Research 13 §2's heavy door (a belt, 20-70 ms) and the liquid's in one rule.
  - **EX5c. Spaces hold liquid.** A cell's fill: oil or water to a share of its free space [EST 0.95: a tank isn't pressed
    full]. What the gas has is its ullage; what the sea can add is that and the weight the oil is short of sea water by. A
    TDS layer's fill by design (`armour.tds_fill`, outside in, `void` or `liquid`); oil-fuel bunkers and fuel tanks full.
    Closes TODO "TDS layers in detail" (the fill); the double bottom's contents stay open.
  - **EX5d. The protections as built.** The shipped designs with a TDS get research 01 §3.2's layers and fill (KGV
    void-liquid-void, the US layers, Bismarck's oil tanks, Littorio, Richelieu; Yamato's voids).
  - **EX5e. The torpedo on it.** Half a contact burst's products into her (the hemisphere facing her), the anchors, the
    panel, `dc-history` and `damage-history`; then EX5 lands.
  EX6 in sub-steps (2026-11-07; today only propellant goes off, and six triggers end in one flat roll, `MagazineFlashChance`):
  - **EX6a. The two rules keyed on the source.** Hot gas lights a magazine in any run, a shell's or a torpedo's as a magazine's
    (`Ignite` without "a stow already burns"), so a burst whose gas lights one becomes that magazine's race in the same run, with
    no roll after; and a magazine's scuttles and trunks are the ship's in every run, joined as its cell joins, so a shell's gas in a
    handling room vents up the trunk. The gas route's `MagazineFlashChance` goes.
  - **EX6b. Propellant by what reaches it** (research 15, recommendations 2-3). Hot gas lights charges by its heat dose, Σq²·dt ≥
    13,900 (kW/m²)²·s with q onto a surface at 480 K, in place of `GasIgniteK` read as the gas's temperature: a burst's own products
    light them in tens of ms, gas mixed down to 600-700 K takes minutes. A burst's fragments, its spall or a near field reaching the
    charges light them: their packaging stops nothing a burst throws. `MagazineFlashChance` goes everywhere.
  - **EX6c. High-explosive stores** (15, recommendations 1, 3-5; built 2026-11-07, "EX6c: high-explosive stores"). Torpedo warheads first (on deck, in torpedo rooms), by filler and
    case: set off by contact (R ≤ 0.06·W^⅓) or by a fragment that keeps v²d ≥ K through its case (Held), never by blast; cooked off
    by a timer by its wall and the fire. Each goes off as its own detonation through the one path. Shell rooms stay out: the record
    has them whole beside magazines that went (15 §4). Depth charges, mines and bombs then need only a row.
  - **EX6d. The anchors.** `magazine --runs` and `--race`, Lion's four, `losses`, the panel, and 15's table (Defence, Glatton,
    Rowan, Brownson, Suzuya, Chōkai, Kalk).
- **EX6. The chain.** What's reached goes off: charges, warheads, depth charges, shell rooms' bursters, each its own
  `Explosion` (Hood's 4″ to her 15″, Defence's passages). Flat rolls left over from before go where physics replaces them;
  the flash chain's doors stay `magazines.md`'s.
- **EX7. The whole game.** `losses`, `balance`, `damage-history`, `dc-history`, `fire-history` against the baseline,
  reported as they come out. The standard `coarse-parity` panel. The `Matchup`'s and AI's estimates refit to what's measured
  (`ai-expectation-not-optimism`). The look reads an explosion's energy and place, in the world view's own step.
- **EX8. The acceptance bench.** A Tallboy, a mine, an aircraft torpedo, a depth charge, a 250 kg SAP in a destroyer (11's
  test 3), by data alone. The bombs themselves stay out of this plan.

## EX0: the baseline (2026-11-05)

Today's game at commit `e04f2d05`, before any of this, to compare EX7 against. **Not a target:** these are the numbers the plan
expects to move. The full output is in `docs/battle/explosions-baseline/` (`balance`, 8 seeds, Jutland 4; `losses`, 4 seeds;
`damage-history`, `dc-history`, `fire-history`, 20 runs a case). Rerun the post-mortems from that commit if they're wanted.

| `balance`: lost a seed | | Record | | | Record | How they went |
|---|---:|---:|---|---:|---:|---|
| Dover Strait: Britain | 0.5 | 0 | Germany | 1.6 | 2 | broke in two 60%, magazine 20%, capsized 13%, foundered 7% |
| Coronel: Britain | 1.4 | 2 | Germany | 1.1 | 0 | magazine 50%, capsized 33%, foundered 17% |
| Cape Sarych: Russia | 2.8 | 0 | Ottoman Empire | 0.9 | 0 | foundered 48%, capsized 33%, magazine 19% |
| Heligoland Bight: Britain | 2.0 | 0 | Germany | 14.3 | 4 | capsized 63%, foundered 23%, broke in two 12%, magazine 2% |
| Dogger Bank: Britain | 7.3 | 0 | Germany | 8.4 | 1 | capsized 64%, foundered 28%, magazine 5%, broke in two 3% |
| Tsushima: Russia | 4.9 | 7 | Japan | 4.8 | 0 | foundered 49%, capsized 39%, magazine 10%, broke in two 1% |
| Jutland: Britain | 28.5 | 14 | Germany | 49.8 | 11 | capsized 55%, foundered 31%, broke in two 9%, magazine 5% |
| The prizes' war, 1920: France | 1.5 | - | Italy | 2.6 | - | capsized 72%, foundered 19%, broke in two 6%, magazine 3% |
| Åland, 1921: Finland | 6.0 | - | Sweden | 4.1 | - | foundered 53%, capsized 44%, broke in two 3% |

- **All together:** 5 of 14 sides within about twice the record; the record's loser lost more in 23 of 44 seeds; capsized 54%,
  foundered 32%, broke in two 8%, magazine 6%. Every ship broken in two today is a torpedo's work (a magazine's break logs as magazine), so EX3 should raise that.
- **Magazines at Jutland, a seed** (`losses`): 1,748 heavy hits, 82.8 of them lit charges in a train. Set off by a hit: contained 8.5,
  burned out 0.5, breached 2.0, severed 0.5, two groups 3.0. By a fire: contained 0.3, severed 0.3.
- **Mind the noise:** Jutland's seeds ran 65-90 lost and the Dogger Bank's 9-26, so a small move in EX7 means nothing here.

## EX1: what it showed (2026-11-05)

`shipgen explode` (`GasNetwork.cs`, the bench only): the anchors with no arguments; `DESIGN --magazine ROOM|all`, `--kg K
--explosive E --room KIND`, `--salvo N`; the rungs `--belt --keep --tear --panel --rupture`, `--step` the convergence check,
`--trace`, `--cells`. Water and points in the open wait for EX4-EX5. Nothing here is tuned; every number is what came out.

**Rung 0 failed exactly as the five attempts before it.** Lion's Q with bags plateaued near 300 kPa, blew 34 doors and 8
decks and never touched the hull. Belt, panel period and plate strength hardly moved it. A smaller tear did, by a lot:
25% to 5% took it from no hull damage to 95 bulkheads, 47 decks and 28 sides.

**The missing physics was a hard switch.** A boundary went from about 1% open just short of rupture to 25% at it
(17 §3.2 notes the jump: `r(D)` up to D 5, then 11's ¼). A plate only just past its limit has cracked; one far past it has gone.
**Running the same `r(D)` line on past D 5 adds no number** and reaches 25% at about D 7.2. So a slow burn cracks plates and
goes on building, and a detonation tears them out, by one rule. That's why the old model suited shells and failed magazines.

| Lion's Q, bags, the ship's own plating, no table | Peak | Burned | Gave | Hull first gave |
|---|---:|---:|---|---:|
| Today's race, on the research's table | 709 kPa | 42 t | decks, sides by table | |
| The research (10 §6) | ~1,000 kPa | | tier 3 | |
| The network, flat ¼ tear | ~300 kPa | 41 t | 8 decks, 34 doors, no hull | never |
| **The network, one curve** | **666 kPa** | 99 t | 74 bulkheads, 23 decks, 64 doors, 13 sides, 3 bottom | 1.27 s |

**The anchors, one curve:**

| Anchor (11 §5.5 and others) | Wanted | The network |
|---|---|---|
| 5″ in a destroyer's compartment | no bulkhead, no door | 60 kPa; nothing gave |
| 8″, the same | its doors blown | 169 kPa; 2 doors |
| 250 kg SAP in a destroyer | next bulkheads and decks, little past ~6 m | 969 kPa; 3 holed, 4 doors, 2 sides, the deck: **a little short** |
| 46 cm HE in her engine room | the compartment, often the next | 837 kPa; 2 bulkheads, 3 doors, 2 sides, the deck |
| 16″ AP in a battleship's spaces | little past its near zone | 56-107 kPa; nothing gave |
| Black powder against TNT | a sixth to an eighth | 27 against 239 kPa, a ninth |
| Lion's Q, 95% flooded | contained | 148 kPa, 1 t, nothing gave |
| Lion's Q by stow | bags over cases over brass | **peak 666, 487, 526; the hull at 1.3, ~8, ~9 s, all ending much alike: not separated** |
| A battleship's belt under nine 46 cm HE | holds | no side plate gave |
| Nine 46 cm HE along a destroyer, one ship | she breaks or founders | **d 0.51: load 0.57 in calm water, 1.06 in a moderate gale** |

**The destroyer is the plan's first case met,** and with nothing fitted to it: nine bursts took the weather deck amidships and 35%
of her sides. It lands in 07's band (d 0.5-0.8 breaks in a seaway). It's read through the old section shares, so EX3 will move it.

**Not there yet:**

1. **Lion's Q doesn't break: the worst load is 0.21** (0.40 with no belt and all the heat kept), where 1 is broken. The plates
   give but too few at one station, and the weather deck never does. Today's shares (deck, sides, bottom) are too crude to
   judge by, so this is really EX3's question. The heat kept helps some; the belt helps at the sides only.
2. **Packing doesn't separate outcomes,** only timing. Brass at R 28 is the catch already in `magazines.md` (to the research).
   Fire spreads along passages and hot gas to 4 of Lion's magazines and all 8 of Seydlitz's.
3. **Large charges stop at the cell's walls.** 2.4 t in a destroyer's engine room reaches 26 spaces: the near field only holes
   its own cell's boundaries. EX4's. Bursters going up in a magazine (candidate 3) can't be judged until then.
4. **A magazine's space isn't what its load needs** (found here; the diagnosis is under EX3, "Magazine volumes"). It's why A and Y peak near 2,700 kPa.
5. **The cost is over budget:** 190-1,000 µs a shell (about 200 wanted), 20-180 ms a magazine (about 20 wanted), measured with
   the baseline benches running beside it. Threads remain an option (the user).

**The solver:** each edge relaxes its pair exactly and in turn, so it's stable at any step; Lion's Q gave 666, 682, 684 kPa at
1, ¼, 1/20 of the step, and the burst 837 at all three. My first limiter wasn't converged (324 to 559 kPa from the step alone).

**The game is untouched:** `magazine --race` and the `hits` bench come out byte-identical to the frozen build.

## EX3: what it showed (2026-11-05)

Built on the bench before EX2 (the user: "It's easier to have EX3 before"). `HullGirder.cs`; `shipgen explode` reads it on every
case, `--girder` the sections intact. Tests: `HullGirderTests`. This supersedes the section shares EX1's numbers were read through.

**What it is, nothing set by hand but `k` and the buckling share:**

- **The section is the boundaries the gas tears:** weather deck, bottom, sides, decks and longitudinal bulkheads, cell by cell.
  What ruptures comes out of the section, with no shares kept apart (`MagazineD` isn't read). A belt carries nothing and an armour
  deck counts in its deck, as shipgen's sizing has it; a deck's slopes count and can't yet be lost.
- **Capacity is the steel's yield (2.1 × the design's allowable stress) on the plastic modulus,** Σ A·|z − z_p| about the height
  that parts the area in two, × 0.85 for buckling [EST]. A girder collapses when yield has spread through the section. To the extreme
  fibre, as first built, a sliver of keel set the whole section: Yamato broke undamaged in a gale and losing a plate could raise capacity.
- **The moment is shipgen's design moment,** Δ·g·L/40, as still water and waves in `MagazineGirder`'s shares, plus the flooding's
  own moment, integrated section by section. All go as sin²(πx/L): a free beam has no shear at its ends. (07's sin had the tip
  of the hull carrying a third of its moment.)
- **The whip follows the impulse.** E joules driving m kg give J = √(2Em); her bending takes J²/2 over her own mass. So it goes as E·m/Δ,
  times `k` 0.2 [EST] for what's vertical and in phase. A shell's whip is small and tonnes of cordite's large, on one law, and at EX5
  a bubble driving hundreds of tonnes of water gets its large one from the same law. An energy share (my first try) had one shell
  whip a destroyer 0.46 of a design moment.
- **A crack cuts by its length,** which goes as the root of its opening, so a boundary's strength falls with `r(D)`'s one curve, not
  at a step.
- **A belt holds no pressure** (candidate 1, now the default): it's hung on the side and comes away with it, as it carries nothing in
  the girder. It moved Lion's Q from 0.92 to 1.23 in a seaway and nothing else much: a Queen Elizabeth still takes shell bursts unhurt.
- **An end is read apart.** Within `MagazineEndShare` of an end, what breaks takes the end off and she may live (07 §3.2).
- **Her bottom is there at her ends.** `Blast.Outside` only sees a bottom face on the keel's tier, so past the double bottom a burst could
  never hole her bottom and the section had none. The girder finds it; `Blast.Outside` takes the fix at EX2, so the game stays as it is till then.

**Intact, all 131 designs hold in a seaway,** with no plating or weight changed (the user allowed a change; it wasn't needed): median load
0.45, Yamato 0.77, the worst Littorio 0.82. Her section beside shipgen's independent sizing:

| Design | I, m⁴ | Shipgen's | Capacity, design moments | Load calm | In a seaway | Ends in a seaway |
|---|---:|---:|---:|---:|---:|---:|
| Destroyer | 3.3 | 3.0 | 4.09 | 0.09 | 0.25 | 0.06 |
| V25 | 1.9 | 2.2 | 3.67 | 0.10 | 0.28 | 0.07 |
| Lion | 210 | 195 | 2.62 | 0.16 | 0.43 | 0.49 |
| Iron Duke | 68 | 77 | 2.19 | 0.20 | 0.54 | 0.33 |
| Seydlitz | 168 | 137 | 2.99 | 0.12 | 0.34 | 0.29 |
| Dreadnought | 71 | 75 | 2.16 | 0.17 | 0.46 | 0.12 |
| Super-dreadnought | 101 | 90 | 2.73 | 0.15 | 0.41 | 0.35 |
| Fast Battleship | 178 | 236 | 2.80 | 0.21 | 0.56 | 0.34 |
| Bismarck | 97 | 188 | 1.66 | 0.21 | 0.58 | 0.60 |
| Yamato | 216 | 356 | 2.56 | 0.28 | 0.77 | 0.56 |

Capacity lands in 07's "2-3 × design", small ships above it (minimum gauge is more than they need). The sloped and short-citadel WWII ships
read well under shipgen's I, and their capacity falls off a step where the armour deck stops (Yamato 2.01 to 0.33 between 0.24 and 0.17 of her
length): not a failure, but their weak place under damage.

**The load on the girder after each explosion (1 is broken; nothing is fitted to these):**

| Case | Calm | Seaway | Reading |
|---|---:|---:|---|
| Lion's Q, bags | 0.80 | **1.33** | breaks in a seaway, at 0.88 of her length |
| Lion's Q, cases | 0.30 | 0.58 | holds |
| Lion's Q, brass | 0.29 | 0.56 | holds, like cases: the known R 28 catch |
| Lion's Q, bags, 95% flooded | 0.16 | 0.43 | nothing happens |
| Lion, any magazine, bags | 0.26-0.82 | 0.54-1.35 | aft ones hold, forward and midships ones break her in a seaway |
| Iron Duke, each magazine (cases) | 0.53-0.77 | 0.90-**1.20** | breaks in a seaway from some |
| Seydlitz, each magazine (brass) | 0.27-0.35 | 0.48-0.56 | **holds: burns out and fights on, as the record has her** |
| Destroyer, V25, a magazine | 0.10-0.13 | 0.26-0.31 | stows of 0.3-0.9 t don't reach the hull; **check against the record** |
| 5″, 8″ in a destroyer's compartment | 0.09-0.10 | 0.25-0.26 | no different from intact |
| 250 kg SAP in her accommodation | 0.26 | 0.63 | section lost 0.70 aft of amidships |
| One 46 cm HE in her engine room | 0.55 | **1.31** | section lost 0.80 |
| Nine 46 cm HE along a destroyer | 0.75 | **1.54** | her bow off too |
| Eighteen 12″ or 6″ HE along a destroyer | 0.10-0.12 | 0.26-0.28 | no side plate ruptured; only the bow's tip goes |
| Nine 46 cm HE on a Queen Elizabeth, Iron Duke | 0.16-0.21 | 0.42-0.55 | hull unhurt |
| A Tallboy's charge in a destroyer | **3.29** | **4.07** | gone, whip 2.16 |
| The same in Yamato | 0.40 | 0.98 | section lost 0.53, her bottom gone |
| The same in Lion, Iron Duke | 0.20-0.33 | 0.48-0.71 | too little: the near field (EX4) |
**The destroyer from the question that began this:** one 46 cm burst inside already cuts the upper part of her section (deck and sides,
0.80 lost): she holds in calm water and breaks in a seaway. More shells add sections, hardly more load, because each section's loss is
its own. Then the flooding's moment should decide (built and tested, not yet run: the bench has no water in it), with what the shells do near
the waterline. The contact holes are vents only on this bench and aren't in the section yet (EX4).

**Magazine volumes (the user asked, 2026-11-05; fixed in shipgen, Version 58; `shipgen explode --volumes`).** Shipgen always sized a magazine by its load (`Ordnance.Stow`, tonnes over 0.6 t/m³), and the tonnes were right. The room it ended up with wasn't that size, for three reasons where the layout meets the cell grid: a room took the whole centre band whatever its own width; the wing bulkheads stood a set depth in from the side, so where the hull narrowed the band pinched to its 1 m floor; and a room smaller than any cell had no volume of its own, its stow in another room's cell, even a wing's stores.

**The rule now (`Subdivision.MagazineWall`):** at a magazine's section the wing wall stands where the room behind it, measured in the hull as it is at that height, holds what's stowed there, shared magazines included. The wing takes what's left: a full wing where the load leaves room, thinning to 0.5 m [EST] where the hull can't hold both. Between coal bunkers (Lion's Q) the same, the bunkers having the rest. A shared magazine shares a magazine's cell, never the wing's stores.

| Lion's | Holds | Needs | Before | Now |
|---|---:|---:|---:|---:|
| Magazine A | 203.8 t | 340 m³ | 52 m³, 1 m wide | 340 m³ |
| Magazine B | 203.8 t | 340 m³ | 296 m³ | 340 m³ |
| Magazine Q | 203.8 t | 340 m³ | 1,219 m³, 23 m wide | 340 m³ |
| Magazine Y, with S aft in it | 267 t | 446 m³ | 62 m³, 1 m wide | 437 m³ |

| The fleet | Before | Now |
|---|---:|---:|
| Median, of what the load needs | 1.14 | 1.01 |
| Within 0.7-1.5 | | 458 of 576 |
| Under half | 199 of 707 (131 of them with no room) | 6 of 576 |
| Over twice | 228 | 66 |

**What's left:** the TDS ships, whose wall is the holding bulkhead (Yamato's SB aft 4.7, Bismarck's B 1.8: the room takes all inside it, to a later step); small secondary magazines and lockers at the least width (PT boat, MTB, merchants' guns); and sterns the hull can't hold (Gangut's Y 0.01: the layout puts it at the extreme stern of her 368 m; a layout question).

**What it cost:** weights hardly moved. Of 130 designs 3 moved at all: Kent −2.52% (her magazine boxes are narrower), Dunkerque −0.67%, Richelieu +0.02%. Goldens rewritten (273 cases), SVGs and sprites unchanged, no new warnings, `mono-check` unchanged at 30 reversals, 21 lumps. All 131 designs still hold intact in a seaway (worst 0.82).

**What it did:** today's race moves, as it reads these rooms (Lion's Q 709 to 1,023 kPa, her A 1,032 to 720). A's side steel falls from 73 to 51 mm in the race's table, as her lowest tier now meets the shell. **My forecast that the wrong rooms skewed which magazines shells set off was wrong:** measured, `hits` on Lion and Iron Duke at 14 km give 0.15% and 0.2% a hit before and after. The `balance` panel wasn't rerun: no balancing until EX3 is in the game (decision 3), and EX2 reruns it.
**Not there yet:**

Moved to "Where to pick up", at the top.
## EX2: what it showed (2026-11-06)

**What went:** `Blast.Cascade`, `Felt`, `PartGas`, the race's own loop, `MagazineGive*`, `MagazineD`, `MagazineEndD`, `Magazines.Girder`, `HotProductsM3Kg`, `GasKnobs`, and `magazines.md` decision 6. **What's one path now:** every shell burst and magazine goes through `GasNetwork`, a part being a space of its own there, and `HullGirder.Bear` reads her after every explosion and as she floods: shells break ships, flooding does (the moment is now run), an end comes off and she may live. A flooded section past the cut carries nothing across it. A tier is only a report. Torpedoes and near misses keep their own rules until EX4-EX5, and `Underwater`'s break chance with them.

**Where the state went:** what's torn is on the condition (`EdgeLost`, `FaceLost`, `FaceTornM2`, `GirderZ`); `HullGirder`, `HullFaces` and the edge table are one per class; the solver's working state says nothing between runs, so it's one per thread, not one per ship (plan said the condition; 250 copies would be about 25 MB for nothing).

**The destroyer from "Why now" (`hits destroyer --shooter yamato --kind he --range 1000`, 2 h on). Nothing fitted:**

| Hits | Crippled: before, now, research | Lost: before, now, research | Mean water: before, now, research |
|---:|---:|---:|---:|
| 1 | 41%, 48%, 36% | 1%, 0%, 7% | 239, 145, 114 t |
| 5 | 92%, 92%, 89% | 23%, 43%, 44% | 1,443, 781, 580 t |
| 9 | 99%, 99%, 99% | 47%, 75%, 79% | 2,487, 725, 1,050 t |

Nine hits' water is low because the ones that would have filled now break first.

**The panel (`explosions-ex2/`, beside `explosions-baseline/`; 8 seeds, Jutland 4):**

| Lost a seed | Before | Now | Record | | Before | Now | Record |
|---|---:|---:|---:|---|---:|---:|---:|
| Dover Strait: Britain | 0.5 | 0.4 | 0 | Germany | 1.6 | 1.8 | 2 |
| Coronel: Britain | 1.4 | **0.5** | 2 | Germany | 1.1 | 0.8 | 0 |
| Cape Sarych: Russia | 2.8 | 3.3 | 0 | Ottoman Empire | 0.9 | 1.0 | 0 |
| Heligoland Bight: Britain | 2.0 | 2.0 | 0 | Germany | 14.3 | 11.4 | 4 |
| Dogger Bank: Britain | 7.3 | 5.9 | 0 | Germany | 8.4 | 12.0 | 1 |
| Tsushima: Russia | 4.9 | **3.1** | 7 | Japan | 4.8 | 4.8 | 0 |
| Jutland: Britain | 28.5 | 20.8 | 14 | Germany | 49.8 | 46.8 | 11 |
| The prizes' war: France | 1.5 | 2.0 | - | Italy | 2.6 | 2.4 | - |
| Åland: Finland | 6.0 | 6.0 | - | Sweden | 4.1 | 2.3 | - |

- **All together:** 5 of 14 sides within about twice the record (5 before); the record's loser lost more in 22 of 44 seeds (23). Capsized 59% (54), foundered 22% (32), **broke in two 19% (8), magazine 0% (6)**. Mind the noise: EX0's warning stands.
- **Where it hurts:** Coronel and Tsushima, where the record's ships went up (from memory: Good Hope, Borodino); Britain's side there now reads LOW.
- **Magazines at Jutland, a seed:** 1,548 heavy hits, 95.8 lit charges in a train. Set off by a hit: contained 5.3, burned out 0.8, breached 8.3, broken 0 (before: 8.5, 0.5, 2.0, 3.5). By a fire: contained 1.0, burned out 0.5, breached 1.8, broken 0 (0.3, 0, 0, 0.3). Fire set-offs are up: ships now live with their fires.
- **The record's ships in `damage-history`** are the one place they go: Indefatigable under Von der Tann's five 11″ in 15 minutes, the record's own case, is lost in 20% of runs, all four by her magazine.

**The magazines on the settled solver (load calm / in a seaway; 1 is broken):**

| | Peak, kPa | Load |
|---|---:|---:|
| Lion, bags, forward magazines | 1,000-1,565 | 0.77-0.82 / **1.25-1.32** |
| Lion, cases, forward | 680-1,190 | 0.30-0.54 / 0.57-0.95 |
| Lion, aft | ~1,000-1,230 | 0.25-0.27 / 0.52-0.54 |
| Iron Duke, as designed | 950-1,680 | 0.56-**1.02** / 0.92-**1.52** |
| Seydlitz, brass | 630-970 | 0.25-0.37 / 0.46-0.58: burns out, as the record has her |
| Destroyer, V25 | 250-350 | 0.11-0.15 / 0.27-0.32: **check against the record** |

Iron Duke reading worse than Lion on cases is new, and unexplained so far.

**The solver (a lesson twice over).** EX1's convergence check (666, 682, 684 kPa) was done before magazines' rooms shrank to their load, and it no longer held: Lion's Q with cases read 473 kPa and 0.58 at the game's step, 813 and 0.93 at a tenth of it, enough to be wrong about which ships break. **Check again whenever the inputs move.** Three causes, none a tuning:

1. The stow's gas went in before each step's flows, so a long step read what was left after they had relaxed, not what a burning stow holds against its vents. Now half before and half after (Strang splitting).
2. The flame through a stow is a branching process, integrated by Euler while the pressure was too low to shorten the step. It now grows by its exact exponential.
3. The step followed every cell's own share, so a far cell's few kPa held it down while the source wasn't watched at all when gas in and gas out balanced. Now it follows what's put in at the source and what's within a quarter of the peak.

Down to 1/16 of the chosen step the loads settle (bags 0.80/1.29, cases 0.51/0.95, Iron Duke 0.91/1.41, Seydlitz 0.37/0.59 in the limit; the game's within 7% on peak and 8% on the loads that decide anything, up to 19% on Seydlitz's, far from breaking; `MagazineRaceTests.What_a_magazine_does_doesnt_hang_on_the_step`). Detonations were and are converged, and don't move at all.

**Found on the way:**

- **A magazine could go up again and again.** The ship's second set-off burned a full stow anew, about two a ship before and four once they lived. Now once (`MagazineSpent`).
- **The bench had drifted from the rule it replaced:** a blown door or a deck giving lit the whole passage group, so a flooded magazine set off the dry ones next door. Passages give with bulkheads, as before; hot gas has its own rule.
- **The first gas loop stopped before a panel had taken up its load,** so a small shell's felt read near nothing. Now it runs until they've caught up.
- **A door left open** was no way for the gas, and what a burst tore could be torn again past its area; both fixed.
- **A ship through her own torpedoes:** to `bugs.md`.

**Anchors moved in the tests, on the user to check:** Lion's Q on bags is lost in a seaway and not in calm water (open 1); a cases or brass Lion holds, in `The_propellant_and_its_packing_decide...`; a 16″ AP in a battleship's engine room felt 0.124 MPa against 11's under 0.1, with nothing ruptured.

**Cost,** steady-state (DevRelease, `DOTNET_TieredCompilation=0`): a shell 10-230 µs, a big magazine 70-165 ms, a destroyer's a few ms. The panel took 655 s against 675 s: the whole game is no slower to play.

## The review (2026-11-06)

A fresh read of EX0-EX3 at the user's asking: one path, one place for each thing, no long methods. **This corrects "EX2: what it showed" above, which is left as it was measured.** Fixed, each with its test, nothing tuned:

1. **A boundary that held kept what was left on it, not the most it felt.** Seams and closures were drawn from the load at the run's end, once the gas had gone (`Cascade` kept the peak). This one moves the numbers.
2. **A door blew out in every explosion:** six 10 kg bursts left 7.2 m² open at a 1.2 m² door. And a blown door then counted as torn plate when its bulkhead ruptured (`JoinMark.DoorBlown`).
3. **A boundary in the near zone never took load again,** so it couldn't rupture, and the plate nearest a charge could end up better off than the plate beside it. What the near zone cuts now counts as torn, and only that.
4. **A shield's or casemate's hoist counted as a gunhouse vented,** which `Trunks` never did: a contained fire read as burned out, which changes who it kills. On 37 of 72 designs. Lion's Q read 11 vented, now 3.
5. **A magazine room with no charge in it lost the ship,** with no gas and no girder read (four carriers; their bombs aren't in the model). It now does nothing and is spent. A fire's heat there would also have hidden her other magazines from the fire step.
6. **The chain stopped silently at eight stows,** on six designs that have more (Seydlitz 9, Gangut 24). "All 8 of Seydlitz's" under EX1 was this cap.
7. **Whether a magazine's bulkhead lit the passages hung on how her cells are numbered,** an edge being worked from its lower cell. Seydlitz's X lit 4 of her magazines one way.

Also a pooled stow's stale `MadePa` in the step control (latent: not reproduced in 35 magazines; a test guards it).

**The destroyer from "Why now" again (`hits destroyer --shooter yamato --kind he --range 1000`, 2 h on), nothing fitted. Lost is by the hits plus in the flooding after:**

| Hits (ships) | Crippled: EX2, now, research | Lost: EX2, now, research | Mean water: EX2, now, research |
|---:|---:|---:|---:|
| 1 (240) | 48%, 57%, 36% | 0%, 0%, 7% | 145, 187, 114 t |
| 5 (48) | 92%, 98%, 89% | 43%, 57%, 44% | 781, 909, 580 t |
| 9 (200) | 99%, 99%, 99% | 75%, 94%, 79% | 725, 815, 1,050 t |

Finding 1 does nearly all of it; finding 3 takes some back (5 hits: water 995 to 909 t, lost 61% to 57%). **So EX2's match on the 5 and 9 hit losses leaned on the bug.** Not tuned; the reading is left to the user. The 5-hit row is 48 ships, so about ±7 points on its own.

**The panel (`explosions-review/`, beside `explosions-ex2/`; 8 seeds, Jutland 4):**

| Lost a seed | EX2 | Now | Record | | EX2 | Now | Record |
|---|---:|---:|---:|---|---:|---:|---:|
| Dover Strait: Britain | 0.4 | 0.1 | 0 | Germany | 1.8 | 1.4 | 2 |
| Coronel: Britain | 0.5 | **1.3** | 2 | Germany | 0.8 | 1.0 | 0 |
| Cape Sarych: Russia | 3.3 | 3.9 | 0 | Ottoman Empire | 1.0 | 0.5 | 0 |
| Heligoland Bight: Britain | 2.0 | 0.8 | 0 | Germany | 11.4 | 7.8 | 4 |
| Dogger Bank: Britain | 5.9 | 5.8 | 0 | Germany | 12.0 | 8.4 | 1 |
| Tsushima: Russia | 3.1 | 3.0 | 7 | Japan | 4.8 | 3.9 | 0 |
| Jutland: Britain | 20.8 | **30.5** | 14 | Germany | 46.8 | 45.5 | 11 |
| The prizes' war: France | 2.0 | 1.6 | - | Italy | 2.4 | 2.5 | - |
| Åland: Finland | 6.0 | 7.6 | - | Sweden | 2.3 | 2.8 | - |

- **All together:** 7 of 14 sides within about twice the record (5); the record's loser lost more in 25 of 44 seeds (22). Capsized 51% (59), foundered 27% (22), broke in two 22% (19), **magazine 0% (0)**. EX0's warning on noise stands, the more so for Jutland's Britain: 4 seeds.
- **Magazines at Jutland, a seed:** 2,320 heavy hits (1,548, not looked into), 116.5 lit charges in a train. Set off by a hit: contained 3.0, burned out 0.3, breached 9.0, broken 0. By a fire: contained 1.0, burned out 0, breached 1.8, broken 0.
- **`damage-history`, lost after the case:** Tiger 10% to 20%, Bismarck 25% to 50%, Seydlitz and Derfflinger 0% to 5%, Mikasa 5% to 10%; Indefatigable's case still 20%, all four by her magazine. **`fire-history`:** Santiago 10% to 35% lost, toward the record's "beyond help in most runs". **`dc-history`:** Lützow 25% to 40%.
- **The magazines' own loads hardly moved** (Lion, Iron Duke, the destroyer and V25 within 0.01): open item 1 stands as it was. Seydlitz now lights all 9, 128 t against 111 t, and four of her sides give at about 10 s: 0.32 / 0.54 as before, so she still burns out.

**The unification (2026-11-06), the review's second step.** The proof is the game's own output, one set of benches and played scenarios (`explode`, `magazine --race` and `--runs`, `burst`, `hits`, `nearmiss`, `torpedo`, the three history benches, three scenarios played with `--hashes`), run twice first to show it's deterministic but for wall-clock times. `snapshot` only covers shipgen.

Byte-identical, hashes included:

1. **One rule for a boundary.** Edge, face and part wall go through one `Boundary.Take`. D, the rupture pressure, the opening's curve and the share of plate lost are `Blast`'s, each written once (`RuptureD`, `RuptureMPa`, `Opening`, `LostShare`); `Opening` and `LeakRatio` share `Curve`. `DeformMPaMm`, read by nothing, is gone. `HullGirder.Add` reads the faces from `HullFaces`. **The share lost keeps two forms on purpose:** a hole is all open and a crack is a narrow slot, so the same area cuts less plate as a hole (`HoleShare`, `LostShare`).
2. **One place for what gas does:** `Effects.GasFelt` takes men and gear wherever it went, for a shell's burst in a space or a part and a magazine's alike. `Crew` no longer fetches the thread's network again. `DamageSystem` sets a magazine off in one place. **`PartGas` under "What went" above:** its physics went, and the method lives on as `DetonateInPart`, which only says what the space is.
3. **The tier is a report,** as written above and in its summary: whether she's lost is the girder's (`RaceResult.Girder`, replacing its copies), and whether a fire was contained is the gas's.
4. **Long methods.** `Effects.Burst` 221 lines to 39, `Strike` 127 to 64, `Step` 85 to 67, and `Boundaries`, `Commit`, `Light`, `Sources` and `Run` each to 40 or less, with the steps named. `Effects.Apply` (149) and `Spall` (121) are older, weren't in this review and are left.
5. **Cost unmoved** within noise, three runs each against the build before: about 80 ms a magazine (Lion and Iron Duke, as this bench runs them) and about 3.2 ms a hit of Yamato's on a destroyer.

The state hash now reads `WhipEM`, `WhipMs`, the cuts and `FaceTornM2`: only hashes moved.

Behaviour, each with its test (the water's and the torn plate's fail without their fix):

6. **A magazine is what the hit did, and one reading a moment.** The girder is read once at that moment, with the shell's whip in it; before, a second read after the race could call the same blast broke in two. A fire-set magazine's tears take the sea from her next second, as a hit's always did (the water was 6 s off in the test's case, up to a settled step's in others). What its gas wrecked is reported and a hit's flags carry it; `Gave` has the weather deck. **The proof set didn't move:** too rare in it to show.
7. **A torn plate past rupture has given,** as any boundary has. A face that tore no further than before didn't count as given, and sprang fresh seams on what was already torn. **16 of the 17 outputs didn't move;** one Dogger Bank seed parts late. Neither of these calls for a new panel; `explosions-review/` stands.

**Not done, and why: the two rules still keyed on the source. They're EX6's.**

- `Ignite` only runs while a stow burns, so a magazine's hot gas lights the next for certain and a shell's rolls `MagazineFlashChance` after.
- A magazine's scuttles and trunks exist only when it's alight, so a shell in a handling room can't vent through the same scuttle, and a burst in a barbette never reaches her cells.

Taking either out isn't a refactor. It decides how often magazines go, which is open item 1, and a shell's gas in a train would count twice beside `Magazines.Light` and its rolls until EX6 says which of them stays. It's the plan's own "flat rolls left over... go where physics replaces them". Until then the source still shows in those two places.

**Smaller, physics or bookkeeping, not fixed:**

- **The girder:** a slope can't be lost. A cut end freezes the whole end zone, including sections still on her. Intact, Littorio, Yamato and the supercarrier read broken at sea 1.5 (44 kn; a hit is all it takes, nothing torn; the scenarios blow at most about 30 kn). `BucklingK` and the 1 s whip period aren't in settings; `?? 111.9`; `YieldPa` under-reads steels past the stress cap.
- **The gas:** a hole under water vents the next explosion, against what `Boundaries` says of a new one. `FaceTornM2` only grows, so a repaired face can't tear again up to its old size. A welded plate's opening jumps 2.5× at D 5 (the user's call under `no-hard-switches`; rupture itself is a physical step). A source under 10 m³ has no pressure control on its step, untested for convergence.
- **Housekeeping:** `Blast.Pressure` is only the tests'. `Modulus` works out the elastic moment on every read. The thread's network keeps its last ship alive.
- **Tests:** the raider test went from seed 3 to 4 in EX2 unremarked (`AimsTests`). `A_thin_hull_gives...` went with no replacement; nothing tests an end coming off through `SetOff`.
- **Not measured:** the cost findings, `Math.Pow` per edge per step and `Felt` over components × cells.
- **Found since:** the flotilla's no-garbage test now has a magazine go up inside its window; the smoke field's lists grow by a first entry there (2 kB, once). A loss counts as a one-off from the moment she's lost.

## The stow's own confinement (2026-11-07)

**The user chose to try the missing physics before EX4-EX6.** The first candidate, a bed of charges burning inside itself. Its setting is new and estimated; nothing is tuned.

### What the research found (the same day; the user's call open)

**[12-charge-bed.md](../../research/simulation/damage-research/12-charge-bed.md) goes against the rule below, and I checked and agree.** `BedPressure` solves a steady bed, its pressure held for the whole burn. Nothing holds a stack that long: 5 MPa on a 1.5 m column accelerates it at about 15,000 m/s², so it's in the aisle within 10-20 ms. Over a whole burn, inertia keeps 0.4-120 Pa. Racks don't matter (50 kPa of hold changes nothing). Only strong walls could keep it there, and that's a gun chamber, not a magazine. Racked cases aren't a bed at all: they're closed tubes, each with its own mouth. The code applies the bed to every stow.

**Checked by running [charge_bed_ref.py](../../research/simulation/damage-research/charge_bed_ref.py):** its tables come out as the note has them, and the steps converge (4, 2, 1 µs agree within 0.01%).

**One slip in the note, which matters to its proposed kick.** Its "burned" figures include what would have burned at the room's pressure anyway, and its 0.03% for that is really 0.2-0.4% (the bed's own burn law, over 15 ms). What the bed adds, 1.5 m deep unless stated:

| Lit over | Burned by the time it opens | At the room's pressure alone | Extra |
|---:|---:|---:|---:|
| At once | 2.4-2.8% | 0.2-0.8% | **2.0-2.2%** (1 m: 0.9-1.1%; 2.5 m: 4.6-4.8%) |
| 0.2 s | 0.5-1.1% | 0.3-1.0% | **0.1-0.2%** (2.5 m: 0.4-0.7%) |
| 0.5 s, the game's `MagazineBagSpreadS` | 0.4-1.1% | 0.3-1.0% | **0.03-0.06%** (2.5 m: 0.1-0.3%) |

So the kick lit at once is about 0.010·L^1.7 rather than the note's 0.016·L^1.3, and at the game's own spread it's nothing.

**What the game reads with the bed out (`--bed 0`), and the open charges' flame spread moved** (the note's recommendation 2: a thrown stack carries the flame; `MagazineBagSpreadS` 0.5 [EST 0.2-1], settings files in a scratch copy, nothing committed). BCF handling, calm water, highest load among a ship's magazines:

| | Bed in, 0.5 s | Bed out, 0.5 s | Bed out, 0.2 s | Bed out, 0.05 s |
|---|---:|---:|---:|---:|
| Lion | 1.10 | 0.82 | 0.93 | **1.03**, the forward four break |
| Tiger | 0.54 | 0.49 | 0.53 | 0.52 |
| Invincible | 0.69 | 0.60 | 0.64 | 0.64 |
| Seydlitz | 0.63 | 0.54 | 0.57 | 0.57 |

Seydlitz doesn't break at any setting, and destroyers stay at 0.10-0.19. 0.05 s is below the estimate's range. It's only there to show how far spread alone can go.

**So taking the bed out most likely puts the panel's magazine losses back to none, and open item 1 back where it was.** Bed 0 is the old model exactly, so nothing else has to change for it to come out. Also from the note, none of it acted on:
- **Stowage:** bulk 500-630 kg/m³ cased, 340-500 USN tanks, 580-770 loose bags, so 600 is fair.
- **Naming:** `Packaging.Clarkson` named the wrong thing. Clarkson's was the transit case of the smaller calibres; heavy guns used a metal magazine case with lids at both ends [SRC: Harley]. Renamed `Packaging.MagazineCase` (design format 21; this document's older tables said "Clarkson" until then, and `explosions-*/` still do). The `MagazinePackageR` fit stands for what it was fitted to.
- **A package's own mouth:** ×1-2 on the rate for RN cases and USN tanks, ×5 for a German brass case lit inside, about 60 ms. Not recommended until the record asks for it.
- **Detonation:** no case for the propellant itself at a magazine's bulk and boundaries. The detonation tier stays with bursters and black powder, which points at shell rooms and EX4.
- **Not found:** magazine plans for Lion, Queen Mary, Iron Duke or Seydlitz, the RN case's size, any stack trial of 1916-30.

### The rule as built

**What was missing.** A charge burned at the pressure of the room round it. But gas made deep in a stack has to get out through the gaps between the charges, and that takes pressure. Burning goes as P^0.78, so the stack feeds itself. The research has the law and the feedback against the room's vents (07 §2.3, 10 §1.1, MSIAC's "quasi static pressure") but not this step inside the stack.

**The rule (`GasNetwork.BedPressure`, `Magazines.BedPa`).** A packed bed's inertial drop (Ergun), ΔP = K·G²/ρ, with K = 1.75·(1 − ε)/ε³ · depth / a charge's size:

- **G, the gas through the bed,** is what its lit charges make over the bed's face. A few alight in the rest get out through their own cluster, so the depth is the lesser of the bed's and the cluster's. **So a lone charge hardly confines itself and a whole bed does, with no switch.**
- **A charge's size** is its own volume's cube root (Lion's 133 kg: 0.44 m; a destroyer's: about 0.15 m).
- **Two estimates, both in settings:** `MagazineBedM` 1.5 m (from the gas's way out to an aisle or the overhead) and `MagazineBedKgM3` 600 (the stack's bulk). Ergun's coefficient is for spheres, and it's mine, not the research's. A request is in `research/REQUESTS.md`.
- **One thing it isn't:** the room's share. My first try spread the bed through the room, shells' space and all (Lion's Q: 29 t of propellant in 340 m³) and nothing moved. Charges are stacked at their own bulk, whatever the room.

Bed 0 is the old model, exactly (the table's every line). It only moves how fast charges burn. What the boundaries feel is still the room's: the bed's pressure on the plate beside it isn't modelled.

**Peak among the charges, kPa (`GasResult.BedKPa`):**

| Lion's Q on bags | Lion's Q on cases | Seydlitz, brass | Iron Duke | Destroyer, V25 |
|---:|---:|---:|---:|---:|
| 16,800 | 4,600 | 3,800 | 4,400 | 380-440 |

**The load on her girder (1 is broken), before and after, calm water and a seaway:**

| | Calm | Seaway |
|---|---:|---:|
| Lion on bags, forward magazines | 0.76-0.82 to **1.05-1.10** | 1.24-1.32 to 1.64-1.71 |
| Lion on bags, aft | 0.27 to 0.29 | 0.54 to 0.57 |
| Lion on cases, forward | 0.30-0.54 to 0.33-0.56 | 0.57-0.95 to 0.60-0.97 |
| Tiger on cases | 0.28-0.39 to 0.28-0.41 | 0.54-0.65 to 0.54-0.67 |
| Iron Duke | 0.56-1.02 to 0.59-1.02 | 0.92-1.52 to 0.96-1.51 |
| Seydlitz, brass | 0.32-0.38 to 0.35-0.40: **burns out, as the record has her** | 0.54-0.59 to 0.57-0.62 |
| Destroyer, V25 | 0.11-0.15, unchanged | 0.27-0.33, unchanged |

**On the BCF's own handling (`--doctrine "1916 BCF"`), before and after:** Lion's forward magazines 0.76-0.82 to 1.05-1.10; Tiger's 0.49 to 0.54; Invincible's 0.34-0.60 to 0.38-0.69 (0.48-0.75 to 0.52-0.84 at half a seaway).

**The rack depth is the sensitive number.** Pressure among the charges goes as about depth^6.8 by the algebra, and the girder's load settles past it. Lion's Q on bags:

| `MagazineBedM` | 0 | 0.5 | 1 | 1.5 | 2.5 |
|---:|---:|---:|---:|---:|---:|
| Among the charges, kPa | | 1,470 | 3,210 | 16,800 | 16,850 |
| Load, calm | 0.79 | 0.81 | 0.87 | 1.05 | 1.11 |

So 1.5 m sits at the top of the rise: a smaller estimate wouldn't break her in calm water, and a larger one changes little. Past it the pressure stops climbing, against the algebra: probably because at 17 MPa a charge burns out in about a third of a second, as fast as the flame spreads through the stack, so they never all burn at once (not traced; and the depth is capped at half the stack, 1.8 m here). **This is the reading to be careful with.** It's a real knife edge (07 §2.3's), and which side she's on hangs on an estimate.

**The panel (`explosions-bed/`, beside `explosions-review/`; 8 seeds, Jutland 4), against the review's:**

| Lost a seed | Before | Now | Record | | Before | Now | Record |
|---|---:|---:|---:|---|---:|---:|---:|
| Dover Strait: Britain | 0.1 | 0.1 | 0 | Germany | 1.4 | 1.4 | 2 |
| Coronel: Britain | 1.3 | 1.4 | 2 | Germany | 1.0 | 0.9 | 0 |
| Cape Sarych: Russia | 3.9 | 4.1 | 0 | Ottoman Empire | 0.5 | 0.6 | 0 |
| Heligoland Bight: Britain | 0.8 | 0.8 | 0 | Germany | 7.8 | 8.0 | 4 |
| Dogger Bank: Britain | 5.8 | 5.3 | 0 | Germany | 8.4 | 9.1 | 1 |
| Tsushima: Russia | 3.0 | 3.0 | 7 | Japan | 3.9 | 3.1 | 0 |
| Jutland: Britain | 30.5 | 26.3 | 14 | Germany | 45.5 | 39.3 | 11 |
| The prizes' war: France | 1.6 | 1.4 | - | Italy | 2.5 | 2.8 | - |
| Åland: Finland | 7.6 | 6.5 | - | Sweden | 2.8 | 4.3 | - |

- **All together:** 8 of 14 sides within about twice the record (7); the record's loser lost more in 27 of 44 seeds (25). Capsized 46% (51), foundered 31% (27), broke in two 22% (22), **magazine 1% (0)**: Tsushima 4%, Dogger Bank and Jutland 1%. EX0's warning on noise stands: most of the sides' moves are within it, and Jutland is 4 seeds.
- **Magazines at Jutland, a seed:** set off by a hit: contained 4.0, breached 5.3, **broken 0.6** (0 before). Those that break her are British: capital ships and a destroyer.
- **Jutland's heavy hits** read 1,544 a seed (2,320 in the review's run, 1,548 in EX2's), the review's rise "not looked into". Probably noise between runs of 4 seeds, as nothing near gunnery changed; not shown.
- **`damage-history`, lost, 20 runs a case:** Indefatigable's own case still 20%, all by her magazine, as it was before the bed. Only three cases moved at all, each by a few runs: Derfflinger 5% to 10%, Tiger 20% to 25%, Bismarck 50% to 35%.

**Convergence, checked again as the lesson says.** Lion's Q at 1, ¼ and 1/16 of the step: on bags load 1.05, 1.04, 1.04, peak 1,244, 1,140, 1,098 kPa; on cases load 0.48, 0.53, 0.51, peak 964, 944, 947 kPa. The loads are settled; bags' peak at the game's step reads 13% over its limit.

**Cost:** about what it was, 140 ms for Lion's Q on bags against 125, 105 on cases against 105 (one run each).

**Anchors moved in the tests, on the user to check:** Lion's Q on bags is now lost in calm water (`Bags_alone_break_a_battlecruiser`); the sea's margin moved to Iron Duke's Q (`The_sea_decides_at_the_margin...`). Tests: `A_bed_burns_above_its_room...`, `A_bed_is_a_whole_stow_alight`.

**Also (2026-11-07): the side plate counted twice.** Where the hull narrows, an inner cell's wall can stand within the 0.6 m the side test allows, so it read as side plate with a wing outboard of it. Now only what no other space stands outboard of is outside (`Blast.Outboard`; `A_side_belongs_to_one_space_at_a_height` over the fleet). Peaks and loads hardly moved, within 0.01: these sections aren't where magazines are read. It's off the review's list below.

## EX4: one near field (2026-11-07)

**What there was:** three hole rules (a shell's contact hole, a near zone's flat 0.5·W^⅓ in any plate, the torpedo's 1.4·W^⅔ m²), two reaches (1.0 and 0.7·W^⅓), and a near zone that stopped at the burst's own space. A shell's contact hole in the hull was the sea's way in and the gas's vent, but never in her section; a torpedo's and a near miss's holes weren't either.

**What there is (`NearField`):**

- **One charge:** its size W^⅓ (TNT's worth), how far water backs it (`Tamp`: 0 at the surface, whole `TampDepthK` 0.5·W^⅓ down [EST]; a burst in a flooded space by its depth under the water there), and its reach, 1.0·W^⅓ in air to 0.7 in water.
- **One hole:** in air 11 §3.1's contact rule, 0.45·W^⅓·√(6/t); in water 0.668·W^⅓ whatever the plate (the torpedo's 1.4·W^⅔ m²); between them by the tamp, both the constant and the plate's exponent. No switch.
- **Off the plate,** the hole is what the reach's sphere cuts of it, and a thick plate lies within less of the reach (its share √(6/t), at most all of it): a charge 2 m off 76 mm doesn't hole it, in contact it does (Marblehead's, 11 §3.2). This replaces the near zone's flat 0.5·W^⅓, which holed an armoured bulkhead as readily as a joiner partition.
- **A plate split between spaces is holed once,** a square of the hole's area clipped to each piece.
- **The walk:** from the charge's space outwards, every boundary within the reach, between spaces or to the outside, holed by the rule; it goes on only through what's open or it opened (not a sprung seam). So what a plate holds back stays behind it, and a big charge goes on past the next space.
- **Hull holes are in her section,** whatever made them: a burst's through the walk, a torpedo's and a near miss's through `NearField.Punch`. The sea's way in is the same hole.
- **The open-space shortcut** counts only what was open before the burst. What the charge opens, its gas goes out by in the run. Without this a Tallboy in a destroyer holed her so wide that its gas never ran, and it had no whip at all.

**Kept for EX5** (with the reason): the torpedo's inner cells' holes and its `Held` switch, `TorpedoBreakShare`, and the near miss's own shock rules. **One judgement:** a shell's own calibre hole where it passed the plate it burst against is kept only if larger than the near field's there, so they aren't counted twice.

**The charges (`explosions-ex4/charges.txt`), before and after:**

| | Before | After |
|---|---|---|
| 11 §5.5's seven, mid-space | | the same within a few %; 16" AP in a battleship's quarters reaches 1 space, not 16 (its bulkheads no longer holed regardless of their plate) |
| Tallboy's charge, destroyer | load 3.41 calm, lost 0.80 | **24.9, lost 0.97**: gone |
| The same, Iron Duke | 0.33 / 0.72, lost 0.12 | 0.36 / 0.79, lost 0.37, her bottom holed |
| The same, Yamato | 0.45 / 1.10 | 0.40 / 0.96 |
| The same, Lion (her engine room aft) | 0.20 / 0.48 | the same: holed aft, not where her moment is |
| Nine 46 cm HE along a destroyer | 0.75 / 1.54 | 0.84 / 1.72, 305 m² of holes (246) |
| Eighteen 12″ HE along a destroyer | 0.13 / 0.30 | 0.20 / 0.45, her section at 0.88 77% gone (18%), her bow nearly off |
| Eighteen 6″, Iron Duke or QE under 46 cm | | unchanged |

**On ships (`hits`, `torpedo`):** Yamato's HE on the destroyer, lost after 1, 5, 9 hits and two hours: 0%, 46%, 86% before; **4%, 60%, 94%** now (the guess's 7, 44, 79%). Torpedoes on a capital ship read the same, run for run; on the destroyer, 2.3 m down, the hole is 47 m² (63) and the reach 5.3 m (4.7), lost 74% to one (72%).

**The panel (`explosions-ex4/`, 8 seeds, Jutland 4; before is the bed out):**

| Lost a seed | Before | Now | Record | | Before | Now | Record |
|---|---:|---:|---:|---|---:|---:|---:|
| Dover Strait: Britain | 0.1 | 0.4 | 0 | Germany | 1.4 | 1.3 | 2 |
| Coronel: Britain | 1.1 | 1.6 | 2 | Germany | 1.0 | 0.6 | 0 |
| Cape Sarych: Russia | 3.9 | 4.9 | 0 | Ottoman Empire | 0.5 | 0.5 | 0 |
| Heligoland Bight: Britain | 0.8 | 1.5 | 0 | Germany | 8.5 | 13.1 | 4 |
| Dogger Bank: Britain | 5.9 | 6.8 | 0 | Germany | 11.3 | 9.6 | 1 |
| Tsushima: Russia | 3.3 | 3.0 | 7 | Japan | 4.0 | 5.1 | 0 |
| Jutland: Britain | 27.8 | 29.5 | 14 | Germany | 47.5 | 49.3 | 11 |
| The prizes' war: France | 1.5 | 1.8 | - | Italy | 3.0 | 2.5 | - |
| Åland: Finland | 6.8 | 5.8 | - | Sweden | 3.8 | 2.5 | - |

- **All together:** 5 of 14 sides within about twice the record (7); the record's loser lost more in 23 of 44 seeds (26); **broke in two 33% of losses (24)**, capsized 43% (44), foundered 23% (32), magazine 0% (0; Jutland 1%, one seed's). Most single sides move within EX0's noise; the shift to breaking is across every scenario but Tsushima, and is the step's: holes now count against the section.
- **`damage-history`:** five cases moved by a few runs of 20: Tiger 20% to 35%, Bismarck 50% to 60%, the 10 × 5″ destroyer 0% to 10%, the 40 × 5″ one 40% to 35%, Samar's 5% to 0%. **`dc-history`:** Lützow 40% to 25%, Seydlitz at 24 kn 5% to 0%, the rest the same.
- **Cost:** the panel ran in 686 s against 813, beside other runs both times; a Tallboy's charge 1-16 ms, a shell's burst as before (the 11 §5.5 charges 34-381 µs).

**Not tuned, by decision 3.** The panel reads worse against the record, and it's the record's losers losing more by breaking. Read it beside EX5, which owns the torpedo's switches and the water.

## EX5: the water (2026-11-07, on branch `ex5-water`)

**The framing (the user, 2026-11-07):** the generator's ships are inspired by real ones, never copies, so no rule is shaped to put
one ship in its record's band. History gives the direction; every change is a rule for every design.

**What's built:**
- **The bubble whips by research 13's law** (`HullGirder.Bubble`): Cole's bubble on her two-node mode (a free-free beam with
  Kumai's virtual mass, her own period), the surface's image, a second cycle as energies, a bubble that reaches the surface a step.
  None from a charge at her plate: its bubble vents into what it opened (γ, its distance over R_max, 0.5 to 1 [EST]). Whips add as
  moments squared and fade in her period; the gas's window is her period too. The C# law reads the research's reference within a
  few percent.
- **A torpedo on the one path:** the near field holes her and walks in (`GasNetwork.Strike`), half its products come in through
  what it opened (`UnderwaterGasShare` 0.5, the hemisphere facing her; the user's choice), its shock a near miss's. `TorpedoBreakShare`,
  `Held`'s switch, the inner cells' holes, the shock and power rolls are gone. The near miss's own hole is the near field's at its
  distance, at D 5 as before.
- **EX5b, a boundary's mass:** each takes its load up over a plain plate's 15 ms times √(its mass over its plate): a belt on its
  backing about 50 ms, liquid behind it longer (research 13 §2's heavy door); its bend, kept, takes room from the space in front
  past yield. A swinging panel was tried and dropped: which plate won a race hung on the solver's step.
- **EX5c, liquid:** a cell's fill as her plant stows its fuel (an oil-fired ship's double bottom 0.6, the TDS half, bunkers and tanks
  0.9) or a design's `armour.tds_fill`. It can't be squeezed: while its walls hold a liquid space passes its push on, as much as
  it's full, and relieves the wall it backs; burst, it runs out (`ShipCondition.Drained`) and the next hit finds a void (the user's
  physics). The gas has its ullage, the sea its ullage and what oil lacks of the sea's weight.
- **EX5d:** seven designs' TDS layers and fill, as design inputs (research 01 §3.2).
- **A protection's walls stretch** (`TdsMembraneK` 2.2, [EST] membrane; research asked): its holding bulkhead, its layers and the
  walls between its own spaces. One factor on the same check, no cost a hit (the user: keep it simple).
- **On `main` along the way:** the holding bulkhead's jog where the TDS's depth steps (shipgen, Version 59); the CLI's AOT
  compiler pinned past an advisory.

**One torpedo (the destroyer's, 303 kg), 60 min after (`shipgen torpedo`, 200 hits):**

| Class | Lost | Protection held | Water |
|---|---|---|---|
| Destroyer | 56% (broken 54%) | | 970 t |
| V25 | 80% | | 380 t |
| Light cruiser | 24% | | 1,550 t |
| Super-dreadnought, 1913 | 0% | | 5,185 t |
| KGV-like | 2% | 39% | 3,940 t |
| Fast battleship | 0% | 32.5% | 4,140 t |
| Yamato-like | 0% | 49% | 4,100 t |

**Open:** the water a held protection takes is about three times 01's 500-1,500 t: its cells are a section long and its full
height. A destroyer is lost to one torpedo about half the time (49-56% by depth), the record's three in four. Four tests pinned
one generated ship's figure (the 1913 super-dreadnought's water, Seydlitz's blackout rate, Lion's B in calm water at 0.99-1.00 of
her girder, a destroyer's heel with three machinery spaces flooded); rewritten to the behaviour by type, the user's call.

## EX6a, and research 14 (2026-11-07)

**EX6a built:** hot gas lights a magazine in any run (a shell's, a torpedo's), the run becoming that magazine's race
(`Magazines.Raced`; `DamageSystem` arms the network with the ship's rounds); a magazine's scuttles and trunks join every run with
its space. The gas route's `MagazineFlashChance` is gone. Derfflinger's 11″ on Lion at 14 km: a magazine set off by 6% of hits,
against 2.35% before, mostly the gas.

**What EX6a showed, and what it wasn't:** a torpedo's gas lit a magazine in 70% of hits on a destroyer and lost the 1913
super-dreadnought in 39%. Two candidates were measured:
- **The gas's heat to the steel** (h 300 W/m²K [EST]) moved the converged answer under 5%: the gas reaches the next spaces in
  tenths of a second and takes seconds to cool (the user's expectation). Operator-split with the flows it made the game's step
  wrong, so it was taken out.
- **Mixing** is already in: each space's gas one well-mixed mass. What it leaves out (afterburn with the air met, a hot front
  ahead of the mixing) would make the gas hotter, not cooler.

**Research 14 (torpedo protection walls) answered it:** the walls were at the top of the real range and **the load was too high**,
the half of the products that comes in held as pressure where in a real hit it leaves through the hole into the bubble within
milliseconds. Applied:
- **A contact charge's own holes in her hull open into its bubble** (`GasNetwork.Strike`): the gas that came in leaves by them.
- **The protection's walls:** `TdsMembraneK` 1.0 riveted, 1.5 continuous welded, on `RuptureMPaMm` (its joint in it), from 14's
  membrane with real steels, spans and seam strains.

One torpedo (the destroyer's), 60 min after:

| Class | Protection held | Water | Lost | A magazine set off by the hit |
|---|---|---|---|---|
| Destroyer | | 634 t | 41% | 59% |
| Light cruiser | | 1,344 t | 7% | 60% |
| 1913 super-dreadnought | | 6,136 t | 1% | 17% |
| Lion-like | | 6,863 t | 0% | 30% |
| KGV-like | 41.5% | 3,069 t | 2% | 1.5% |
| Fast battleship | 35% | 2,310 t | 0% | 0% |
| Bismarck-like | 55% | 3,018 t | 0% | 1.5% |
| Yamato-like | 49% | 4,187 t | 0% | 0% |

The protections now hold as often as with no gas in them; what a held one floods is its coarse compartments (shipgen's, later:
research 14 recommendation 4). 14's other recommendations wait: the holding bulkhead failing as a leak first (3), the scatter on
its tearing set (7).

## EX6b: propellant by what reaches it (2026-11-07)

From research 15 (recommendations 2-3), whose findings in short: blast never sets off a filler beyond contact; fragments set off
thin-cased stores, never shells; propellant lights by heat dose or by any fragment through its packaging; shell rooms didn't go
up with their magazines.

**Built:**
- **Hot gas lights charges by its heat dose** (`GasNetwork.Ignite`): each space's Σq²·dt, q = h·(T − 480 K) + εσ(T⁴ − 480⁴), lights
  a magazine there at 13,900 (kW/m²)²·s (`ChargeIgniteK`, `ChargeDose`, `ChargeGasW` 40 [EST], `GasEmissivity` 0.3 [EST]); `Hot`, a
  handling room's flash source, reads the same dose. `GasIgniteK` (470 K, the charge's surface read as the gas's) is gone. A
  step ends where an unlit magazine's dose would light it.
- **What reaches the charges lights them** (their packaging stops nothing a burst throws): a burst among them, a magazine station
  in a train, a spall cloud reaching them (damage/14's 20% of clouds kept: that's whether it reaches), a torpedo's near field
  walked into the magazine (whole within half its reach, fading to its edge, as the rooms it wrecks). `MagazineFlashChance` is gone.
- **The near field's walk is its own** (`GasNetwork.NearReached`): a space a liquid layer pushes on or a plate bends into is in
  the run but wasn't reached by the wreck.

**What it did:**
- **A magazine's race:** every magazine still goes, but the next ones light later, as the dose builds (Lion's bags: B at 6.9 s and
  A at 10.4 s after Q). Peaks fall in some cases (1,565 → 1,121 kPa) and Lion's girder with them: A's 0.98 calm to 0.57, her
  forward magazines no longer breaking her in calm water. Open item 1 (magazines at the margin) moves the other way from the
  record; research 15 recommendation 8 says to look at the propellant there, not the shells.
- **Torpedoes:** a magazine set off by 45% of hits on a destroyer (lost 43%), 9% on the KGV-like where her protection narrows by
  the end turrets (lost 9%), Bismarck-like 2.5%, Yamato-like 0.
- **Shells:** Lion's guns on Lion at 14 km set off a magazine in 0.3% of hits, as before.
- **Tests:** the step test checks what converges (peak, propellant burned, magazines lit), not the girder: late in Lion Q's run
  her side plates sit within about 2% of tearing, a knife-edge, not the step. Lion A across the bulkhead lights the next magazine
  and leaves her broken or breached.

## EX6c: high-explosive stores (2026-11-07)

From research 15 (recommendations 1, 3-5). The stores are a torpedo launcher's warheads, in its tubes and its spares beside them
(`Stores`; their place is `LauncherRow.StoreBox`): a deck mount's at its tubes' muzzle ends as it rests, over the cell under the
deck; a submerged tube's in its room. Shell rooms stay out (15 §4: whole beside magazines that went). Depth charges, mines and
bombs need only a row of the same kind when they come.

**The data** (design format 22, `ShipDesign.Version` 61):
- **An explosive says how it behaves as a store:** Held's constant (`held_k`, mm³/µs²: Comp B 16 [SRC], cast TNT 52 [INFERRED],
  Explosive D 115 [EST]...; 0, nothing from outside sets it off: wet guncotton) and its cook-off temperature (`cook_k`: TNT 561 K,
  Comp B 487 K). Two catalogue rows are new: TNT/HND (hexanite, Type 97) and Torpex. A design's own copy without them takes its
  catalogue namesake's.
- **A torpedo block names its warhead:** `filler`, `case_mm` (6 [EST]) and `oxygen` (its flask's). The catalogue's: Victorian wet
  guncotton; Great War, Treaty and Second War TNT; Cold War Torpex; a new "Oxygen torpedo" (the IJN's, Type 97, oxygen). Format 22
  gives a block of a template's name its template's, else TNT, 6 mm, air (with a warning).
- **The warhead is a charge like a shell's** (`TorpedoRow.Warhead`): its filler, as much as gives the charge over its re, in a
  cylinder of the calibre with the block's wall, its pieces by Gurney and Mott (`DesignerPlaceholders.Torpedo`).

**What sets one off** (each marks it, `ShipCondition.StoreLit`, for its own explosion after the one that did):
- **Contact:** a detonation within `ContactReachK` (0.06)·W^⅓ m of the store [SRC Mk 82s]. Blast beyond it never does.
- **Fragments, by Held through the case:** the pieces of any burst (a shell's, a store's own, a near miss's over the side, a
  torpedo's through her plating into what its near field walked into) that keep v_r²·d ≥ K, v_r² = v² − V₅₀² through what's between
  and the case (`FragmentBurst.HeldKg`, the same Mott count and THOR limit as every other fragment). Against 15 §2.1's table: a
  neighbour warhead's 30 g at 2,800 m/s sets off TNT; a 6 in HE's 25 g at 900 m/s doesn't; Torpex's line for a 200 g piece is at
  800 m/s (the table's 1.01).
- **Fire:** a store in a burning space heats toward its filler's cook-off temperature, `CookOffMin` [1.3, 3.5, 16] min for a 5 mm TNT
  case in an engulfing fire (charges burning there), a compartment's (developed) and hot smoke (growing, starved, in the open), by its
  own wall and filler as a lumped wall heats: t ∝ wall·ln((T_f − 300)/(T_f − T_c)), within 10% of H2's Torpex row; each store's limit
  drawn ×e^(0.6·N); water on the fire or the space flooded cools it (`CookOffCoolS` 600 s). When it goes, an oxygen flask's warhead
  detonates; an air flask's only `CookOffHighShare` (0.1) of the time, else it burns, its flask bursting first (Kalk, Stormes).

**How it goes off:** all the launcher's warheads still aboard together, as a burst at rest at their place through the steps a shell's
burst takes (`Effects.Detonate`, sharing `Effects.Site` with the shell's): in a room, its near field and gas there; on a deck, its near
field down through the deck at its own height over it and half its products into what that opened (`ContactGasShare`, the old
`UnderwaterGasShare`); its cases' pieces round the tubes' ring; the men, fires, magazines and other stores they reach. The damage
system runs it as a hit of its own (`HitSource.Store`, logged as `CombatEvent.Store`, not counted as a hit on her), then any store
it set off in turn.

**What it did:**
- **Shells:** in 5,000 hits of light HE and Lion's 13.5 in on destroyers, no warhead went: shell pieces through a 6 mm case fall short
  of Held's line (Chōkai, Furutaka, Ralph Talbot), and contact needs the burst on the warheads themselves. **Open:** a torpedo
  mount's hitbox is its 1.9 m body; the tubes, and the warheads at their muzzle ends 2.5-3.8 m out, aren't in it, so a shell can't
  strike them. That's shipgen's hitbox, later.
- **Torpedoes:** none of 200 on the destroyer set her warheads off (the near field rarely walks up to the deck under her mounts).
- **A mount going off** (`torpedo DESIGN --store L`): breaks the destroyer and Fubuki in two every time (Brownson's), and sets off the
  next mount by its pieces (10 of 10 on the destroyer). The light cruiser's 2 × 182 kg breaks her too: as the same 364 kg burst in
  her quarters does (`explode light_cruiser --kg 364`, 80% of her section lost at 0.48 of her length), the near field's and the
  girder's, not the store's. A warhead in Lion's torpedo room floods her 3,200 t and she lives.
- **Fire:** a 6 mm TNT case cooks off in 1.6/4.2/19 min by the class of fire; in the test's developed fire, 4.5 min on average.

## EX6d: the anchors (2026-11-07)

Read for silliness, not for the record's numbers (the user, 2026-11-07: "the panel results are not strict, we just need to know
if the result is silly or not"). Nothing silly.

- **The panel** (`balance --fc-runs 0`, kept in `explosions-ex6/balance.txt` beside `explosions-ex4/`): 8 of 14 sides within about
  twice the record (EX4: 5); the record's loser lost more in 26 of 44 seeds (23); losses capsized 50%, foundered 26%, broke in two
  24% (33%), magazine about 0 (Jutland 1%, the Dover Strait 7%). Jutland's Germany still loses about half her ships, as at EX4:
  the gunnery's gap (`error-sources.md`), not the explosions.
- **Lion's magazines** (`magazine lion --race`): each set off leaves her breached, every magazine going; her cases' race as EX6b
  left it (A 0.34, Q 0.30 of what breaks her calm), her bags' A 0.57 and B 0.83. The trains (`--runs`) light their magazine about
  10% of the time from any station, as before.
- **Research 15's table:**

  | Case | Wanted | Now |
  |---|---|---|
  | Defence, Invincible | shells left whole | yes: shell rooms aren't stores |
  | Brownson | a burst at the after mount takes its warheads and her | a mount going off breaks the destroyer in two, every time |
  | Chōkai, Furutaka, Ralph Talbot | shell hits on mounts: fires, no detonation | 5,000 HE hits on destroyers, no warhead went |
  | Kalk, Stormes | in a fire the flasks burst, the warheads hold | an air flask's warhead burns 90% of the time it cooks off; a deck fire by a mount burns out first (6 min, 0.26 of the way) |
  | Suzuya | a near miss sets off a mount; a fire takes the rest in an hour | the mount by a neighbour's pieces, yes; by a near miss, no bombs yet; the hour-long fire, no: her deck fires burn out in minutes |
  | Mikuma | oxygen torpedoes go within about an hour of fires | the same: an oxygen flask goes when it cooks off, but no fire of hers lasts |
  | Rowan, Isherwood, Mullany, Franklin | depth charges, bombs | not stores yet |
  | Glatton | a burning 6 in magazine doesn't set off the next within two hours | not run: her after magazines lay far aft; a magazine's race lights only what its gas reaches |

- **Fixed on the way:** a deck mount's warheads took their heat only from the mount's own fire space (lit only by a burst in the
  mount); they take the weather deck's round them too, and a store still warm keeps her steps going until it cools.
- **Open:** deck fires burn out in minutes, so no store aboard sees an hour-long fire (Suzuya's, Mikuma's: bombs and aviation fuel,
  which aren't in the game); the fire model's, when bombs come.

## Cost and convergence (2026-11-07)

A 2000 mm HE shell (13.6 t of lyddite) froze the map: 60-160 ms a hit on Dreadnought, Bismarck and a destroyer, against 2-3 ms for
a 12″ HE. Not the shell: its gas lights her magazines (EX6a) and it's their race. **Any magazine set off costs 60-700 ms** (`explode
DESIGN --magazine all`, Release): Bismarck's slow burns run 90-170 s of gas over 210-280 of her 294 cells in 4,500-8,600 steps; Iron
Duke's, Lion's, Seydlitz's 20-30 s in 4,000-6,000; a V25's 18 s in 2,000. Already so at EX5 (the bisect: EX2's commits don't build
alone now). The sim thread publishes nothing while one runs, so the ships stop and then jump.

- **Where it goes:** steps × the boundaries of the cells reached, about 17 ns a pool entry or face a step (48 µs a step for
  Bismarck). Caching what a step reads and the run doesn't move (each cell's liquid, each entry's panel mass and rupture, a plain
  plate's lag) took 19% off, results bit for bit the same. 20 ms for a 5,500-step race wants under 4 µs a step: no tuning of this
  loop gets there.
- **The longest step (`DtMax`, 20 ms) binds only on slow burns.** Against an eighth of the step and of the longest step, over 75
  races on eight designs, a longest step of 50, 100 and 200 ms took 11% of the steps off (Bismarck's halved, the violent races
  none), and read no worse than 20 ms up to 100 ms. Not done: too little for what it moves.
- **The convergence check didn't shorten the longest step** (`StepK` scaled only the step's control), so a slow burn held at 20
  ms was never checked. It does now (no change in play). **At the game's step not every race is converged:** against an eighth,
  peaks within 15%, burned within 29% (one race), seaway load within 0.25; Iron Duke's S aft breaks her at the game's step and holds
  at the fine one (1.02 against 1.00), Queen Mary's B lights 3 magazines against 4, Dreadnought's Y 2 against 3. Threshold cases,
  and they move whichever way the step does (at 100 ms those two magazines read right and another race was off): the step's control
  wants a look before EX7 reads the game.
- **Fixed on the way:** a lit store whose hit `Strike` passed over (a bench's `Spared`) would have held `GoOff`'s loop forever; the
  flag is taken before the store's hit. **Noted:** `StoreLimit` isn't in the state hash (a draw that decides; `StoreHeat` is); a
  store in a fire cooler than its filler neither heats nor cools while it burns.

## What stays out

- Aircraft, bomb sights, a bomb's flight.
- The pieces of a broken ship floating apart, and the look of the break (`sinking-vfx-research.md`, the sinking pass).
- A whole-hull finite-element anything: this stays a beam and a gas network.
- Non-ship targets (harbours, land).

## Yardstick

Physics first, history checks and never sets (`derive-not-set`, `physics-is-the-only-yardstick`). No guard rules
(`physics-no-guard-rules`): if nine 460 mm HE usually leaves her afloat, we ask why it happens and don't add a rule to
stop it. The other answer's "almost certainly breaks her back" was its own inference. Its case is Friedrich Eckoldt: 6″
broadsides at 4 km, broke in two in under two minutes, cause unknown.

## Decided (the user, 2026-11-05)

The risk of EX1 is accepted: "Let's see where unified system gets us."

1. **The first cut is EX0-EX3,** then EX4-EX6 in turn, with the whole plan as the end goal.
2. **If EX1 fails, we look at it together.** "Retrospective of Ex1 fail can give us important info." Looking for the
   missing physics is the first idea, not a fixed rule. Today's model stays in the game until the new one meets the anchors.
3. **No balancing pass until EX3 has landed.** "That's why I took this up now as it will invalidate rates which are built
   on top." The `Too safe` comparison is redone on the new physics.
4. **The cost budget stands** as a first figure: about 200 µs a shell, about 20 ms a magazine. "We can also thread them"
   (`workers.md`), if it comes to that.
