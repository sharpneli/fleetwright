# Plan: one explosion (a 4.5 deepening)

Status: **decided 2026-11-05** ("Decided" at the end). EX0 and EX1 run on the bench, the game untouched; EX1's results await
the user ("EX1: what it showed").

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
