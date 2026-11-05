# Plan: one explosion (a 4.5 deepening)

Status: **decided 2026-11-05** ("Decided" at the end). EX0, EX1 and EX3 run on the bench, EX3 first at the user's word; the game
is untouched. Their results await the user ("EX3: what it showed"); EX2 wires them in next.

The user, 2026-11-05: "Explosion is an explosion. The code should basically not have a special case for magazine
explosion in on itself and just run all the explosions through the same path. We avoid special cases and try to build an
unified physics model. This becomes very important later on when we'll introduce bombs. And 5.4ton Tallboy will be let's
just say interesting to have. We don't want to introduce new physics at that point."

**This is the one place for the physics of an explosion:** what a release of energy does to a ship. How a flame gets to
the charges stays in `magazines.md` (the trains, doors, discipline, valves), how a shell gets to its burst point in
`shells-armour-damage.md` (the terminal walk), fragments in E1.

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

**Cost budget** (DevRelease): a shell's burst under about 200 µs on average, a magazine under about 20 ms (the race takes
2-13 ms now). A hit costs about 760 µs all told today.

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
- **EX4. One near field.** The wreck reach and the holes as one rule for all sources, tamped by water by where it is.
- **EX5. One water.** Contact and non-contact underwater as one model: the shock factor, the tamped hole, the bubble's
  whip, a TDS from its own volumes and plates through the network. `Underwater`'s separate rules, `TorpedoBreakShare` and
  the `Held` switch go. Its anchors are the hard ones (one torpedo usually sinks a destroyer, 7 of 31 lived; capital ships
  take several; 4.6's broken back). Mines (4.7) then need only a row.
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
| Lion's Q by stow | bags over Clarkson over brass | **peak 666, 487, 526; the hull at 1.3, ~8, ~9 s, all ending much alike: not separated** |
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
4. **A magazine's space can't hold its stow** (found here, not fixed): Lion's Magazine A is 203.8 t in 52 m³ and S aft 63.5 t
   in 0 m³, cells carved thin by the hull's ends and what shares them. It's why A and Y peak near 2,700 kPa. A Shipgen
   question: does the magazine get the volume its load needs? Belongs in `bugs.md` unless EX2 needs it.
5. **The cost is over budget:** 190-1,000 µs a shell (about 200 wanted), 20-180 ms a magazine (about 20 wanted), measured with
   the baseline benches running beside it. Threads remain an option (the user).

**The solver:** each edge relaxes its pair exactly and in turn, so it's stable at any step; Lion's Q gave 666, 682, 684 kPa at
1, ¼, 1/20 of the step, and the burst 837 at all three. My first limiter wasn't converged (324 to 559 kPa from the step alone).

**The game is untouched:** `magazine --race` and the `hits` bench come out byte-identical to the frozen build.

## EX3: what it showed (2026-11-05)

Built on the bench before EX2 (the user: "It's easier to have EX3 before"). `HullGirder.cs`; `shipgen explode` reads it on every
case, `--girder` the sections intact. Tests: `HullGirderTests`. This supersedes the section shares EX1's numbers were read through.

**What it is, none of it set by hand but `k`:**

- **The section is the boundaries the gas tears:** weather deck, bottom, sides, decks and longitudinal bulkheads, cell by cell.
  What ruptures comes out of the section, with no shares kept apart (`MagazineD` isn't read). A belt carries nothing and an armour
  deck counts in its deck, as shipgen's sizing has it; a deck's slopes count and can't yet be lost.
- **Capacity is steel on what's left:** yield (2.1 × the design's allowable stress) × the section modulus to its extreme fibre.
- **The moment is shipgen's design moment,** Δ·g·L/40, as still water and waves in `MagazineGirder`'s shares, plus the flooding's
  own moment, integrated section by section. All go as sin²(πx/L): a free beam has no shear at its ends. (07's sin had the tip
  of the hull carrying a third of its moment, and broke Lion intact at 0.10 of her length.)
- **The whip follows the impulse.** E joules driving m kg give J = √(2Em); her bending takes J²/2 over her own mass. So it goes as E·m/Δ,
  times `k` 0.2 [EST] for what's vertical and in phase. A shell's whip is small and tonnes of cordite's large, on one law, and at EX5
  a bubble driving hundreds of tonnes of water gets its large one from the same law. An energy share (my first try) had one shell
  whip a destroyer 0.46 of a design moment.
- **A crack cuts by its length,** which goes as the root of its opening, so a boundary's strength falls with `r(D)`'s one curve, not
  at a step.
- **An end is read apart.** Within `MagazineEndShare` of an end, what breaks takes the end off and she may live (07 §3.2).

**The section against shipgen's independent sizing, intact:**

| Design | I, m⁴ | Shipgen's | Capacity, design moments | Load calm | In a seaway | Ends in a seaway |
|---|---:|---:|---:|---:|---:|---:|
| Destroyer | 3.3 | 3.0 | 4.09 | 0.10 | 0.27 | 0.11 |
| V25 | 1.9 | 2.2 | 3.67 | 0.11 | 0.30 | 0.11 |
| Lion | 210 | 195 | 2.62 | 0.16 | 0.44 | 0.83 |
| Iron Duke | 68 | 77 | 2.19 | 0.22 | 0.59 | 0.31 |
| Seydlitz | 168 | 137 | 2.99 | 0.14 | 0.39 | 0.30 |
| Dreadnought | 71 | 75 | 2.16 | 0.19 | 0.52 | 0.14 |
| Super-dreadnought | 101 | 90 | 2.73 | 0.16 | 0.42 | 0.39 |
| Fast Battleship | 178 | 236 | 2.80 | 0.34 | 0.92 | 0.73 |
| Bismarck | 97 | 188 | 1.66 | 0.31 | 0.85 | 0.76 |
| **Yamato** | **216** | **356** | 2.56 | 0.43 | **1.16** | **1.02** |

Capacity lands in 07's "2-3 × design", with small ships above it (minimum gauge is more than they need). **Yamato breaks intact in a
seaway, and that has to be fixed before EX2** (below).

**The load on the girder after each explosion (1 is broken; nothing is fitted to these):**

| Case | Calm | Seaway | Reading |
|---|---:|---:|---|
| Lion's Q, bags | 0.89 | **1.47** | breaks in a seaway, at 0.88 of her length |
| Lion's Q, Clarkson | 0.53 | **1.01** | on the edge in a seaway |
| Lion's Q, brass | 0.64 | **1.25** | no better than Clarkson: the known R 28 catch |
| Lion's Q, bags, 95% flooded | 0.16 | 0.44 | nothing happens |
| Iron Duke, each magazine (Clarkson) | 0.66-**1.00** | **1.10-1.52** | B amidships on the edge in calm water |
| Seydlitz, each magazine (brass) | 0.31-0.43 | 0.56-0.67 | **holds: burns out and fights on, as the record has her** |
| Destroyer, V25, a magazine | 0.11-0.14 | 0.28-0.33 | stows of 0.3-0.9 t don't reach the hull; **check against the record** |
| 5″, 8″ in a destroyer's compartment | 0.10 | 0.27 | no different from intact |
| 250 kg SAP in her accommodation | 0.32 | 0.76 | section lost 0.75 aft of amidships |
| One 46 cm HE in her engine room | 0.68 | **1.61** | section lost 0.83 |
| Nine 46 cm HE along a destroyer | 0.92 | **1.89** | her bow off too |
| Eighteen 12″ or 6″ HE along a destroyer | 0.13 | 0.29 | no side plate ruptured; only the bow's tip goes |
| Nine 46 cm HE on Iron Duke, a Queen Elizabeth | 0.17-0.23 | 0.44-0.61 | the belt holds |
| A Tallboy's charge in a destroyer | **5.77** | **7.15** | gone, whip 2.16 |
| The same in Yamato | 0.60 | **1.44** | section lost 0.62 |
| The same in Lion, Iron Duke | 0.21-0.32 | 0.48-0.69 | too little: the near field (EX4) |

**The destroyer from the question that began this:** one 46 cm burst inside already cuts the upper part of her section (deck and sides,
0.83 lost), and she holds only in calm water. More shells add sections, hardly more load, because each section's loss is its own.
Whether she goes then depends on the sea, the flooding's moment (built, not yet run: the bench has no water in it) and what the shells
do near the waterline. The contact holes are vents only on this bench and aren't in the section yet (EX4).

**Not there yet:**

1. **Yamato's intact hull (before EX2).** Her I is 216 against shipgen's 356, and the worst is at 0.17. My reading, not yet checked: past
   the citadel her plating's only minimum gauge, because `HullWeight` credits the armour deck with her whole girder (`strength_t` 0 on
   WWII battleships) and nothing carries that strength on along her. The same, milder, on the Fast Battleship and Bismarck. If so, the
   plate model must size what's outside the armour, which moves weights and goldens. It could also be how the section reads her armour deck.
2. **Lion's thin ends** read 0.83 intact in a seaway at 0.10, where her capacity falls from 1.13 to 0.10 in one section; look at it with (1).
3. **12″ and 6″ HE never rupture a destroyer's side.** Near a 62 kg burst the plate goes and below about 20 kg it holds outright: a steep
   edge, though from the physics. Friedrich Eckoldt (6″ broadsides, broke in two in under two minutes) isn't met; the research is
   asked what broke her.
4. **`k` 0.2 is an estimate** (0.2 of a design moment on Lion from 20 t of cordite in a second), to be tested by EX5's bubble and the research.
5. **Lion's break lands at 0.88,** by her forward magazines, not at Q, wherever she's lit: the thin section there, and fire carried
   to A and B along the passages. Their cells can't hold their stow (EX1's item 4), so this reading may move once they can.

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
