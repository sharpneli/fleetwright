# Plan: damage control (a 4.5 deepening)
Status: **agreed with the user 2026-10-20** ("Decided" below); DC0-DC4 done 2026-10-20, DC5 next.    Updated: 2026-10-20

The fine-tune pass for damage control: what the crew does about a hit once it has landed. Firefighting came first, as
this pass's first slice (`fire.md`, decision 1). This plan adds the water and the repairs, and both draw on the same
men. Its research:
- `09_damage_control.md`, flooding: holes, leaks, bulkheads, pumps, counterflooding, the parties, with
  `damage_control_ref.py`;
- `10_damage_control_holistic.md`: every threat, how they couple, priorities and steering, with `dc_holistic_ref.py`;
- `damage/17_structural_leakage.md`: sprung seams, loosened fittings, head and working, with `leakage_ref.py`;
- `research/simulation/damage-research/06-service-networks.md` §5, the repair tasks.

As in the other plans, the reference code is something to check against, never a second engine.

## The aim

The user, 2026-10-20: *"we don't necessarily want the full detail system as that's not the core focus of the game, we
just want the system to act realistically to outside observer so simplified models are appreciated."*

The record splits the fight against water in two (09 §0):
- **The first minutes are decided by design.** The holed space fills in seconds to minutes and nobody stops it.
- **The hours after are decided by the crew.** Water creeps through sprung seams, loosened doors and glands; pumps beat
  leaks but never holes; speed drives water in. A ship that lives through the first minutes usually lives for hours.

Seen from outside, after this pass:

1. **A hit ship lists, then the list comes off in minutes** if her navy counterfloods (North Carolina: 5.5° gone in
   6 min). When the tanks on the high side run out, the list starts to grow again.
2. **Small holes stop mattering where there's a crew; big ones never do.** Thirty splinter holes: a drilled crew holds
   about 120 t, nobody gets ~3,000 t and an 18° list (09 §8). A torpedo's hole is a step in weight that nobody fights.
3. **An undrilled crew loses its ship one compartment at a time, over hours** (Lützow, Audacious, Shinano). A drilled
   one holds the boundary around the hole.
4. **Speed costs water.** A ship holed in the bow slows down or floods. Seydlitz ran astern; Bismarck kept her speed and
   gave up her repair. A ship that slows falls out of the line.
5. **Things come back.** Steering is taken over aft or by hand, a jammed turret is freed, a boiler is relit. Other damage
   stays until the dockyard.

## What there is today (checked 2026-10-20)

- **Flooding v0** (`Sim.Combat/Flooding.cs`, `fighting.md` 4.3b):
  - **Holes:** summed per cell (`ShipCondition.Hole`: `BreachM2`, the mean `BreachY`, `BreachZ`). The sea comes in by
    the orifice law and runs out over a hole's lower edge (fire B3).
  - **Edges:** water crosses the cell graph's edges by the same law, through flat areas: intact bulkheads and decks
    5 cm², torn 0.05 m², open 1 m², a sunk deck 0.3 m². On top of that comes `JoinOpenM2`: E5's ruptures, blown doors
    and r(D) seam leaks, all summed into one number.
  - **Pumps:** `PumpTPerHourPer1000T` (50) × `PowerCap`, only on cells with no hole of their own.
  - **Settling:** linear, with the free surface's pocket, a 30 s heel lag, capsize at 25°, and founder at the reserve
    buoyancy or when the deck at the centre of flotation goes under.
- **Missing from flooding:** speed, bulkhead strength, watertight doors (the readiness's doors are fire's vents),
  counterflooding, and any plugging, patching or shoring.
- **The fire parties** (fire B4):
  - `FireParty`: 12% of the complement in 2-8 parties, made at the first fire and sent every 30 s. They muster, travel
    30 s a section, and are thinned by hits; the gun crews turn to 15 min after the last hit.
  - `FirefightingDoctrine` is the side's doctrine and `FireReadiness` the ship's orders. B2 floods a threatened
    magazine.
- **Parts:**
  - `PartState` only gets worse (Ok, Damaged, Disabled, Destroyed).
  - E4's timed outages (`Down`, `DownUntil`) end on their own, as do mount jams (`MountHarm.JamUntil`, some for the
    whole battle: `NonPen.Battle`) and director and eye outages. Machinery abandoned to smoke comes back (B2).
  - `Steering` (Free, Jammed, Lost) never recovers.
- **Falling out:** the captain falls out by the water's share of her reserve buoyancy, her list and her steering
  (`Fitness`, `FallOutRule`).
- **Baselines today, with no damage control** (2026-10-20):

  | Case | After | Water | List | Lost |
  |---|---|---|---|---|
  | `super_dreadnought`, 4 torpedoes 4.5 m down along one side | 120 min | 11,268 t | 21° | 53% capsized; power 16% |
  | `iron_duke`, 15 of Derfflinger's AP hits at 12 km | 180 min | 482 t | — | 3.7%, all magazines |
  | `destroyer`, 6 of Arethusa's HE hits at 5 km | 180 min | 145 t | — | 9% capsized, 11.9% magazines |
  | `destroyer`, 1 torpedo | 120 min | 49 t | 0.1° | 75% (73% broke in two) |

## What's wrong, against the research

| Gap | Today | The research | Why it matters |
|---|---|---|---|
| **Nobody fights the water** | pumps on steam only | parties close, plug, patch, caulk, shore and rig pumps (09 §4) | small leaks sink ships nobody fights; the record's survivals can't happen |
| **One leak number per edge** | `JoinOpenM2` sums ruptures, doors and seams | seams (caulking leaves 15%), fittings (re-dogging leaves 20%) and ruptures (an opening) differ (17 §4, §7) | damage control can't tell what it can fix |
| **Fittings never loosen** | — | at D 0.3-0.5, doors, stuffing tubes and glands leak 10-60 cm² each: 61% of Crossroads' secondary flooding (17 §4) | Houston's magazine, Lützow's chain |
| **Head doesn't open leaks; bulkheads never give way** | flat areas | seam × (1 + Δh/rating), collapse at the rating, shoring ×1.6 (09 §2.4, 17 §5.1) | the choice between running and slowing; Lützow |
| **Speed doesn't matter** | — | ram head k·v²/2g forward; working ∝ (1 + v/15 kn)²; hull caulking loosens above 15 kn (09 §2.2, 17 §5.2) | Seydlitz, Bismarck, Oslyabya |
| **Holes summed per cell** | one area | a hole's size decides: plug ≤ 150 cm², patch ≤ 0.3 m², mat ≤ ~5 m² only when slow, beyond that the dockyard (09 §4) | 30 splinter holes against one torpedo |
| **Pumps by displacement, and never on a holed cell** | 50 t/h per 1,000 t on steam | fixed drainage on power; portable pumps without it; pump where the pump beats the inflow (09 §5.2) | dewatering after plugging; pumping in a blackout |
| **No counterflooding** | — | DC Central fills one tank at a time to full; NC put in 480 t in 6 min; capacity is finite (09 §2.5) | list off in minutes; the doom clock; how often torpedoes capsize |
| **Watertight closures not modelled** | 5 cm² everywhere | doors open by discipline (NC, Audacious, Shinano); tightness ×1-25 (09 §5.3) | the state before battle matters as much as skill (09 §0.7) |
| **Nothing comes back** | parts only get worse; steering never returns | the steering ladder (10 §3); jams cleared, steam cross-connected (06 §5.1) | ships circle for ever; a turret lost for good to a jam |
| **Water isn't the fire parties' work** | parties fight fires only | one pool of men, fire first or flooding first (10 §5.1, §8) | the choice when there aren't enough men |

## The principles

- **Rough where it doesn't pay, physical where it decides** (the user's words above; memory: rough for non-core).
  - **Kept physical:** the water's side, which decides who sinks: the orifice law, the head on a boundary, its rating,
    the ram head, counterflood tonnes.
  - **Coarse:** the crew's side. Parties are units doing jobs measured in man-minutes from a catalogue. There are no
    sailors, no positions in a room, and no hose, pump or valve as a part.
  - **Judged by the end result:** what a watcher sees (list, speed, how long she lives).
- **One pool of men, one implementation.** The fire parties become repair parties. Renamed on the spot (one word, one
  meaning): `FireParty` → `RepairParty`, `FirefightingDoctrine` → `DamageControlDoctrine` (scenario key `firefighting`
  → `damage_control`), `FireReadiness` → `Readiness` (key `readiness` as before).
- **Design, doctrine and orders split as in fire.**
  - **Design:** what's built in: fixed pumps, the counterflood system, the hull's tightness, portable pumps carried.
    It goes in a design block with templates by navy and year (a format step).
  - **Doctrine:** how the crew works: skill, priorities, when to counterflood, how long to hold speed. It is the side's.
  - **Orders:** closures and readiness are the ship's orders before action.
  - No look, navy or year sets a number.
- **History is now the yardstick for flooding too.** Survivals and losses by flooding have waited for this pass (memory:
  no historical tuning without damage control). DC9 judges them against the record; rough anchors are enough.
- **Physics, no guard rules.** Nothing like "a torpedo can't sink a battleship", and no rescue by fiat.
- **No hard switches, no size cut-offs.** Eras differ by their numbers; parties scale with the complement.
- **Allocation-free, dt-proof, cheap when quiet.**
  - The working is a seeded hazard against a drawn threshold, so a 1 s and a 15 s step agree.
  - Jobs go in fixed arrays sized by the class.
  - Only a wet or damaged ship costs anything.
- **The sim adds data; the UI pass shows it** (memory: UI pass after the damage plan). Log lines go in symptom → cause
  form (09 §7.4) so the UI can use them later.

## The yardstick: the historical record

The first game is 1890-1925, so its cases lead. The WWII cases check the mechanisms for later cuts. Cases marked (?)
come from general knowledge and wait on the research request (DC0). **This version is built against the existing
research only** (decision 7): 09, 10 and 17's mechanism checks and sourced cases are what DC9 tunes to; the (?) rows
are read but not tuned to until the research is back.

| Case | Record | Wanted |
|---|---|---|
| Seydlitz, Jutland: bow flooded, ~5,300 t aboard | slowed, ran astern to ease her forward bulkheads, reached harbour (09 §2.2) | survives in most runs when she slows; loses her forward bulkheads if she holds speed |
| Lützow, Jutland: heavy hits forward, low | 2,400 t at 22:15 grew to 8,350 t at 02:00 at a few knots, then scuttled (17 §0.1) | thousands of tonnes spread forward over hours; lost, or close to it, in many runs |
| Oslyabya, Tsushima: waterline holes forward, kept her speed (?) | sank within about an hour of the first hits (?) | at speed with holes forward at the waterline: lost within about an hour in most runs |
| Marlborough, Jutland: one torpedo (?) | listed ~7°, kept her place for hours, then slowed and went home (?) | survives; a few degrees of list after counterflooding |
| Audacious, 1914: one mine under the port engine room, not at action stations | engine rooms abandoned ~2 h, power lost, capsized ~12 h after (17 §1.3) | unprepared closures: lost over hours; closed up and drilled: survives |
| Pre-dreadnoughts torpedoed, 1915: Majestic, Triumph, Formidable (?) | capsized minutes to hours after one or two torpedoes (?) | capsize or founder after 1-2 torpedoes, within minutes to hours |
| Borodino, Alexander III at Tsushima: overloaded, shell holes at the waterline (?) | capsized under heavy fire (?) | some capsize under heavy shellfire; the clutter's weight is fire B5's open item |
| Destroyers under shellfire | most survive unless the boilers or a magazine go | small holes held by the crew |
| Mechanism checks (09 §8, 17 §6) | — | 30 splinter holes: ~120 t drilled against ~3,000 t and 18° unfought; a holding bulkhead at D ≤ 1.5 held at 0-50 t with a crew, filled in 2-8 h without; a bow shell at 28 kn ~10,000 t against ~1,900 t slowed to 12 kn |
| North Carolina, one torpedo (later cuts) | 970 t, 5.5° list off in 6 min with ~480 t | ~1,000 t; the list off within ~10 min |
| Shinano-like, 4 torpedoes, unsealed, weak crew (later cuts) | capsized after ~7 h | lost after 4-8 h |

## Decided

1. **Parts are repaired, not just water** (the user, 2026-10-20: "water + steering + parts"):
   - the steering ladder;
   - a share of disabled parts (mounts, boilers, engines, directors, eyes) brought back from disabled to damaged over
     10-60 min where a party works on them and the cause can be fixed at sea;
   - destroyed parts stay destroyed.
2. **The captain slows by doctrine** (the user, 2026-10-20). Damage control works out a safe speed and the AI captain
   keeps to it, falling out of line if he must. A doctrine number sets how long a navy holds speed first. The player
   gets an override in the UI pass.

3. **Before E8** (the user, 2026-10-20: "DC goes in before E8, we do the anchoring pass probably after this"). E8
   then anchors once, with damage control in; its turret-minutes lost per 100 heavy hits read the repairs.
4. **Watertight closures are their own order**, apart from fire's doors (the user, 2026-10-20: fine). Fire's "doors open" stays as the non-watertight
   doors and hatches in the upper hull (vents for a fire). Watertight doors are shut at action stations, except a share
   left open by the readiness (in use, or by poor discipline), which the first response shuts if its men are there.
   Tying them to fire's doors would flood every "prepared" ship through open watertight doors.
5. **Power and hydraulics are left alone, unless something today disables them for good** (the user, 2026-10-20).
   Routes, casualty power and cable splicing are `06`'s Model B, for the systems pass. Checked 2026-10-20: every power
   outage today is timed (E4's and E7's trips and cascades, 1-90 min), so they stay as they are. Power is lost for good
   only through parts: a boiler or engine room disabled by a hit, or **flooded out of action** (`RoomFloodedShare`),
   which never comes back even when pumped dry. Both are DC7's (parts, and rooms dewatered).
6. **No post-battle repair** (the user, 2026-10-20: "after battle is for the next game"). Tenders, docks and pumping the
   counterflood water back out.
7. **Built against the existing research** (the user, 2026-10-20): 09, 10, 17 and 06. The research request (DC0) is
   filed and folded in when it's back, not waited for.

## The model

### Leaks through standing structure (17 §7)

Per edge of the cell graph (the class's Neighbours pool entry, as `JoinOpenM2` is):

| State | What | Fixed by |
|---|---|---|
| `RupturedM2` | what E5 tore or blew open: an opening (moved out of `JoinOpenM2`) | nothing at sea; the boundary moves back to the next one |
| `SeamM2` | sprung seams, r(D) as now (E5, E7, the belt leaks) | caulking: leaves 15% |
| `FitM2` | loosened fittings: doors, tubes, vents, rods, glands, flanges (17 §4's table, a roll per fitting) | re-dogging, wedging, plugging: leaves 20% |
| `DoorOpen` | a watertight door left open (decision 4) | shutting it: 1 min |
| `Work` | how far the seams have worked open, ×1 to ×4 | shoring halves the rate |
| `Shored`, `Caulked`, `FitCaulked`, `Collapsed` | the work done, and whether it gave way | — |

- **Compiled once per class:** each edge's face area, its rating, and its fittings by kind. Until shipgen emits them,
  defaults by room kind [EST, 17 §7.1]:
  - stuffing tubes, one per 5 m² of machinery and magazine bulkheads;
  - one watertight door per bulkhead between passage spaces (quarters, stores, handling) below the bulkhead deck, and
    none in machinery, holds, TDS, double bottom or magazines (doors there were avoided);
  - a gland where a shaft passes.
- **The area each step:**

  ```
  A = base·tightness + ruptured + door·1.2 m²
    + seam · work · (caulked ? 0.15 : 1) · (1 + Δh / rating)
    + fit · (fitCaulked ? 0.2 : 1)
  ```

  The base is v0's 5 cm², times the design's tightness (×1 air-tested … ×25 unsealed).
- **The rating** [09 §2.4]: a watertight boundary takes the sea at its section's bulkhead deck, ×1.3.
  - Sprung: ×(1 − 0.1·D), at least ×0.6 [EST, from 09's 20-40%].
  - Shored: ×1.6 a set, at most two sets.
  - A light boundary above the bulkhead deck: 1.3 × its own height.
  - **The load** is the head across the boundary plus half the ram head on the forward boundaries of a flooded space.
  - **Past the rating it collapses:** a quarter of its area opens (11's ruptured), with a log line.
- **Working** [17 §5.2]: a sprung seam opens a step (×1.6, capped at ×4) at the hazard
  `λ = 0.1/h × Δh/rating × (1 + v/15 kn)² × (1 + sea)`, halved when shored. The hazard is summed each step against a
  threshold drawn at the hit.
- **Hull-side work loosens at speed:** caulking, plugs and patches go 15% back toward open per 10 min above 15 kn (17
  §5.2; Bismarck's mat).

### Holes by size (09 §4)

`ShipCondition.Hole` already sees each hole's own area. Each cell now keeps, beside the total, the count and area in
four classes:

| Class | A hole's area | Repair | Leaves | Needs |
|---|---|---|---|---|
| plug | ≤ 0.015 m² (≈ 15 cm) | plug and wedge, 3 min a hole, 2 men | 10% | the hole reachable: the cell's water below it, or under ~1 m deep |
| patch | ≤ 0.3 m² | soft or box patch, 20 min, 4 men | 8% with no flow, 30% against flow | the same; poor against active flow (NRL) |
| mat | ≤ 5 m², its top within ~2 m of the waterline | collision mat from outside, 90 min, 20 men on deck | 30% | speed ≤ 5 kn; the deck there not wrecked |
| beyond | anything bigger: torpedo holes, long tears | — | 100% | the dockyard |

The sea's inflow reads the effective area (each class × what it leaves). A hole above the outside waterline needs
nothing until the list, sinkage or rolling bring it within ~1 m.

### The ram head (09 §2.2)

- **At a hole forward:** the head gains k·v²/2g, with k = 0.5 at the stem falling to 0 at a quarter of her length [EST]:
  ~1 m at 12 kn, ~2.7 m at 20 kn, ~5.3 m at 28 kn.
- **Going astern** it's 0.
- **The speed** is through the water, from the ship store.

### Pumps (09 §5.2)

- **Fixed drainage** is a design number: t/h per 1,000 t, replacing `PumpTPerHourPer1000T`. It runs on `PowerCap` (as
  B4's main does) and goes to any cell, where it beats the inflow:
  - first, cells that are only seeping;
  - then, cells whose inflow it beats;
  - never a cell it can't beat.
  
  So "pumps are for leaks, not holes" comes out of the arithmetic, not a rule.
- **Portable pumps:** the design's count and rate.
  - By period [EST]: hand pumps in the 1890s ~5-10 t/h, a steam or petrol portable ~15-30 t/h by 1914, the handy-billy
    14 t/h and the P-500 115 t/h in later cuts.
  - A party rigs one in a cell in 10 min, and it works without power.

### Counterflooding: DC Central (09 §2.5, §3)

- **The tanks, compiled once:** cells below the waterline in the wings and bottom (double bottom, TDS layers, the empty
  share of tanks and wing bunkers), each with its side and its place along the ship.
- **The doctrine** decides whether at all (none for the unorganised) and the list and trim it acts at (1-3°).
- **The design** gives the rate: remote valves or hand; NC ~80 t/min.
- **Filling:** one tank at a time, to full, the one that cancels the most heel per tonne (low and outboard on the high
  side). Trim works the same at the other end.
- **What's left on each side** is the doom clock, kept as data for the UI pass. A log line says when it runs out.
- **DC Central's stand-in:** it works while she's conned (`Conned`) [EST, until shipgen has a DC station]. Without it,
  the valves are turned by hand, at a third of the rate after a few minutes.
- **B2's magazine flooding** moves here: one set of valves.

### Repair parties

- **The fire parties renamed** (`RepairParty`), in the same numbers. They are made at the first damage of any kind,
  not only the first fire.
- **Jobs are made from the damage** into fixed arrays (kind, target, man-minutes, most men, needs, effect). The
  catalogue [EST: a drilled crew's times, from 09 §4, 10 §6 and 06 §5.1]:

  | Job | Time | Men | Effect |
  |---|---|---|---|
  | Shut an open watertight door | 1 min | 2 | the door's area gone (the first response) |
  | Re-dog an edge's fittings | 3 min a door, 1 min a tube | 2 | `FitCaulked` |
  | Plug a cell's small holes | 3 min a hole | 2 | the plug class at 10% |
  | Patch a cell's medium holes | 20 min | 4 | the patch class at 8% or 30% |
  | Collision mat | 90 min | 20 | the mat class at 30%; needs ≤ 5 kn |
  | Caulk a sprung seam, from the dry side | 20 min | 3 | `Caulked` |
  | Shore an edge, from the dry side, when loaded past ~half its rating | 20 min a set | 4 | rating ×1.6 |
  | Rig a portable pump | 10 min | 4 | the pump into the cell |
  | Firefighting | B4's | — | B4's flow by men |
  | Steering aft, by hand or steam, rudder freed | see below | — | — |
  | Repair a part | see below | — | — |

- **Multipliers:**
  - the doctrine's skill;
  - darkness ×1.6 when power is under half;
  - list ×(1 + list/10);
  - smoke as in B4;
  - a jammed door on the way +1 min.
- **Priority by doctrine** (10 §5.1, 09 §3):
  1. DC Central's own jobs (magazines, counterflooding) don't take parties.
  2. Fires next to magazines.
  3. Open doors on the flooding boundary.
  4. The boundaries of the flooded region, ranked by inflow × what's behind them: a magazine ×3, machinery ×2,
     steering ×2, any watertight space ×1.
  5. Steering.
  6. Other fires.
  7. Holes and seams that only seep.
  8. Pumps.
  9. Parts.
  
  A flood-first doctrine swaps steps 2 and 4.
- **Planning** is every 30 s with B4's stickiness. Travel is 30 s a section. The turn-to comes 15 min after the last
  hit. A flooded or burning space can't be worked in, except on its boundary from the dry side.

### Steering and parts (decision 1)

- **The steering ladder** [10 §3, EST]:

  | Rung | Time to take over | Rudder speed | Needs |
  |---|---|---|---|
  | after steering, local | 3 min | full | the steering room working and manned |
  | by hand or steam | 15 min | a third | a party at the gear |
  | by engines | at once | turns at 30% | two shafts or more; no party |

- **A jammed rudder:** jammed by its gear or power, a party centres or frees it in ~30 min [EST]; jammed by a hit on
  the rudder itself, never.
- **Parts:** a disabled part (not destroyed) may come back to damaged.
  - Each part kind has a share that can be fixed at sea and a time [EST table in DC7]: mounts' training gear and
    hydraulics ~50% in 20-60 min, directors and their wiring ~60% in 15-30 min, eyes (spares) ~40% in 30 min, boilers
    and engines (steam cross-connected, relit) ~30% in 30-60 min; uptakes, funnels, masts, hull and armour never.
  - The draw is made at the damage, so a part either can be fixed or can't.
  - It waits while its space is flooded or burning.
- **Rooms flooded out of action** (decision 5): a room disabled by its water comes back to damaged once it's pumped
  below `RoomFloodedShare` and a party works it [EST: machinery 30-60 min to relight and restart, other rooms 10-20
  min]. Its parts' own damage is separate.
- **Battle-long jams:** E4's timed jams and outages already include the crew's own repair and stay as they are. A party
  may bring a battle-long one (`NonPen.Battle`) down to a time, when its cause can be fixed.

### Speed (decision 2)

- **The safe speed** is the highest speed at which:
  - every forward boundary under load stays under 0.8 of its rating with the ram head (a closed form in v);
  - hull-side repairs don't loosen (15 kn while there are any);
  - a mat being rigged can be finished (≤ 5 kn).
- It's kept on the condition (`SafeSpeedKn`).
- **The captain** keeps to it once the doctrine's hold time has run out [EST: a German 1916 crew slows at once; the RN
  holds longer to keep the line]. A ship slower than her group falls out by the fall-out rule's speed share, as a
  slowed ship does today.

### Readiness and closures (decision 4)

- **The readiness gains a share of watertight doors open in action** [EST: unprepared 0.5, prepared 0.1, closed up
  0.02]. Each door is drawn at spawn. The first response shuts one on the flooding boundary if men are there; if not,
  the sea gets through (NC).
- **Tightness** is the design's.

### The design's `drainage` block and the doctrine

- **The design** (format 10 → 11, a `ShipDesign` version step, goldens gaining the block): fixed pumps (t/h per
  1,000 t), portable pumps (count, t/h), counterflood rate (t/min per 10,000 t; remote or hand as a number), and
  tightness.
  - Templates by navy and year: Victorian 1890, 1905, RN 1914, German 1914, and 1939 and 1943 for later cuts.
  - Old designs take their template's block, as the outfit did.
- **The doctrine** (`DamageControlDoctrine`) gains:
  - skill;
  - fire first or flooding first;
  - the counterflood threshold (or none);
  - the first response's delay;
  - how long to hold speed;
  - whether it shores and rigs mats.

  Its templates keep B4's names.

## The steps

Each step reports its numbers on the hits and torpedo benches, the squadrons scenario and the matchup, and from DC0 on
on the yardstick bench.

### DC0 The bench, the baselines and the research request
- **`shipgen dc-history`**, built like `fire-history`. Each case is a ship on our designs given hits (`GivenHits`) or
  holes:
  - Seydlitz's bow on `seydlitz`;
  - Lützow's on `derfflinger`;
  - Oslyabya on `oslyabya`;
  - Marlborough on `iron_duke`;
  - Audacious on `king_george_v`;
  - the 1915 pre-dreadnoughts on `lord_nelson` or `mikasa`;
  - the destroyers;
  - the mechanism checks.
  
  Each reports water, list, speed, lost and when, and `--trace` follows one run.
- **The baselines** with today's model, before anything changes.
- **A research request** (`research/REQUESTS.md`): the first game's flooding record, 1890-1925, with times, tonnages,
  speeds and what the crews did. It covers the (?) rows above, plus who counterflooded then (Makarov's and Krylov's
  tables, the German navy) and portable pumps by navy and year.
- **Built (done 2026-10-20):**
  - `shipgen dc-history` (`DcHistory.cs`): fourteen cases, each a ship given a list of blows at their minutes (shell
    hits through `GivenHits.Aim`, torpedoes struck as the torpedo bench does on either side, holes and splinter holes
    through the dev's `SideHole`, orders of speed), run 20 times in one world and sampled every minute. It reports water
    afloat by the hour, the worst list, list, trim, speed and power at the end, and losses by kind and minute; `--trace`
    follows the first run.
  - The first game's cases: Seydlitz (`seydlitz`), Lützow (`derfflinger`), Audacious (`king_george_v`), Oslyabya (?),
    Marlborough (?, `iron_duke`), a pre-dreadnought at anchor and one under way (?, `lord_nelson`), the RN battleship
    under 15 heavy hits and a destroyer under 6 HE hits. The mechanism checks: 09 §8's splinters and bow shell (at 28 kn
    and slowed to 12). Later cuts: North Carolina and Shinano-like on `super_dreadnought`. The whole run takes 34 s.
  - The research request is filed. Audacious, Seydlitz and Lützow turn out to be in 17 §1.3 already (secondary sources),
    so only Oslyabya, Marlborough and the 1915 pre-dreadnoughts stay (?).
- **The baselines (20 runs a case, today's model, no damage control):**

  | Case | Water afloat (median) | Worst list | At the end | Lost | Record / wanted |
  |---|---|---|---|---|---|
  | Seydlitz, 24 kn then 7 | 1,470 t at ½ h, 2,560 at 2 h, 4,260 at 8 h | 1.1° | 10 h: trim +1.6°, power 72% | 20%, magazines | 5,308 t, survived |
  | Lützow, 22 kn down to 3 | 460 t at ½ h, 700 at 4 h, 955 at 8 h | 0° | trim +0.3° | 25%, magazines | 1,038 t at 3 h, 8,351 by 7½ h |
  | Audacious, then stopped | 4,390 t at ½ h, 4,950 at 12 h | 0.5° | 13 h: power 46% | 0% | capsized at ~12 h |
  | Oslyabya (?), 9 kn | 69 t | 0° | — | 15%, magazines | capsized within the hour |
  | Marlborough (?), 17 kn | 3,150 t at ½ h, 4,040 at 8 h | 1.9° | power 46% | 0% | ~7° list, survived |
  | Pre-dreadnought, one torpedo at anchor (?) | 3,610 t at ½ h, 3,790 at 2 h | 8.8° | power 55% | 5% | capsized in minutes |
  | Pre-dreadnought, two torpedoes (?) | (survivors) 2,700-3,500 t | 23° | — | 85%, 80% capsized (median 36 min) | sank in ~2¾ h |
  | RN battleship, 15 heavy hits | 0 t | 0° | power 76% | 5%, magazine | came home |
  | Destroyer, 6 HE hits | 15-35 t | 0° | power 49% | 15% (10% magazines, 5% capsized at 92 min) | most survive |
  | 30 splinter holes, 18 kn | 38 t at ½ h, 156 at 2 h, 307 at 8 h | 4.1° | list 3.4° | 5% capsized | the research's model: ~2,950 t, 18° unfought |
  | Bow shell, holds 28 kn | 222 t at ½ h, 316 at 4 h | 0° | — | 0% | the research's model: bulkheads collapse, ~10,400 t |
  | Bow shell, slowed to 12 kn | identical | — | — | 0% | ~1,900 t |
  | North Carolina-type torpedo, 19 kn | 1,060 t, no growth | 2.1° | — | 0% | 970 t, 5.5° off in 6 min |
  | Shinano-like, 4 torpedoes, unprepared | 11,400 t at ½ h, 16,200 at 8 h | 11.5° | power 3% | 10%, magazines | lost at 7¾ h |

- **What the baselines say:**
  - **Water doesn't spread.** A holed space fills and the water stops there. The intact 5 cm² edges and the pumps keep
    it in, and nothing loosens, works open or collapses. Lützow ends with a ninth of the record's water, the 28 kn bow
    shell with a thirtieth of the research's. Speed changes nothing (the two bow-shell runs are identical). That's DC1.
  - **The first minutes are already about right in size.** Torpedoes and the mine put in 1,000-4,400 t at once, close to
    the record (North Carolina-type 1,060 t against 970). But lists stay small (Marlborough 1.9°, North Carolina-type
    2.1°, against 5.5-7°), so counterflooding (DC5) will have little to do on these until DC1's leaks and the wing
    flooding put more water on one side. `fighting.md`'s open item 8 is still open.
  - **Unfought small leaks already look like a fought ship.** The 30 splinter holes reach 307 t and 3.4° at 8 h, close to
    the research's *drilled* crew (~120 t) and a tenth of its unfought case. Today's pumps (50 t/h per 1,000 t) clear
    every cell without a hole, and nothing else spreads the water. DC1 gives the unfought case its water; DC3 and DC4
    take it back with the crew.
  - **Pre-dreadnoughts already capsize from torpedoes** (two torpedoes: 80% within the hour). One torpedo at anchor
    leaves 8.8° and she lives, where Majestic and Triumph went over in minutes (?).
  - **Most losses in the shellfire cases are magazines**, not water: E8's and the magazines pass's, not this plan's.

### DC1 Leaks with structure
- `JoinOpenM2` split into ruptured, seam and fittings; fittings by room kind and their rolls (17 §4); the design's
  tightness, watertight doors and readiness closures.
- The head on seams, ratings and collapse, the working, the ram head at holes and on boundaries.
- No crew yet, so ships flood more (expected: first we break, then we fix).
- **Tests:** 17 §6's tables (a holding bulkhead by D with no damage control; the Lützow-type bow; Houston's magazine);
  dt 1 s against 15 s.
- **Built (done 2026-10-20):**
  - **The state per edge** (`ShipCondition`, on the entry the flooding reads):
    - `JoinOpenM2` stays the openings: E5's ruptures and blown doors, and now a collapse.
    - Beside it: `JoinSeamM2` (sprung seams), `JoinClosureM2` (loosened closures), the working (`JoinWork`, its hazard
      and threshold), the highest load (`JoinD`), and `JoinMarks` (door open, door jammed, collapsed, and DC3's caulked,
      re-dogged, shored).
    - **Renamed on the way:** 17's "fittings" are *closures* in the code (`Closure`, the `Closure*` settings), since
      shipgen's fittings are how gear is mounted.
  - **`Leaks`** (`Sim.Combat/Leaks.cs`):
    - A boundary's closures, by what's on either side:
      - doors by the user's 2026-10-17 rule, now shared with the blast (`Leaks.HasDoor`);
      - hatches between non-tanks;
      - stuffing tubes, one per 5 m² round machinery, magazines, handling rooms, steering and the transmitting
        station, one per 20 m² elsewhere;
      - a vent closure;
      - a reach rod and a pipe flange in a main bulkhead;
      - a gland for each shaft through both spaces.
    - `Sprung`: the seam from r(D), drawn by the caller. Each closure rolls at min(0.9, per·(D − start + ½)), and a
      loosened door is jammed. D is kept as the highest plus half the lesser, and the working's threshold is drawn.
    - `DrawDoors`: the doors left open, drawn once by the readiness at her first step.
    - `SpringCell`, for the benches.
  - **What springs boundaries:**
    - the blast's deformed boundaries (E5) and the near miss's internal ones (E7), which now go through `Sprung`, so
      closures roll as well as seams;
    - **torpedoes**: the boundaries of every cell they flood, at D = 2 × reach / distance, at most 4.9
      (`TorpedoSprungD` [EST, 17 §6.1]). v0 sprang nothing.
  - **The flooding step** (`Flooding.Step`, now given her speed and a stream):
    - **An edge's area** is v0's base + its openings + an open door's 1.2 m² + seams × work × (caulked 0.15) ×
      (1 + Δh/rating) + closures × (re-dogged 0.2).
    - **The rating** is 1.3 × the height of water from the edge's lowest point to its top (`FloodModel.BuiltM`: a
      bulkhead's own top, a deck's section's top, at least the waterline). It is × max(0.6, 1 − 0.1·D) when sprung and
      × 1.6 for each set of shores. Past it the edge collapses: a quarter of its face opens, with a `CombatEvent.Flood`
      log line.
    - **The working** steps ×1.6, up to ×4, at 0.1/h × Δh/rating × (1 + v/15 kn)² × (1 + sea), with sea 1. It's a
      summed hazard against a drawn threshold, so the step size doesn't matter.
    - **The ram head** (k 0.5 at the stem, falling to 0 at a quarter of her length: `Flooding.Ram`) adds to the inflow
      at a hole and to the outflow's floor, and half of it adds to a holed space's pressure on its boundaries.
  - **Fixed on the way:**
    - A holed space whose leaks drained it a little each step never counted as full, so it never pressed on its
      boundaries with the sea's head. Now a holed space with its water within 0.1 m of its ceiling presses as full.
    - B3's outflow ignored what drives water in under way, so it cancelled the ram head.
  - **Readiness:** `WtDoorsOpen` [EST]: unprepared 0.5, prepared and cluttered 0.1, closed up 0.02.
  - **Tests** (`LeakTests`):
    - a machinery bulkhead's closures by D;
    - the rating's cuts and shores;
    - the ram head: 5.29 m at the stem at 28 kn, none aft or astern;
    - an open door passes 311 m³ in 10 min, against 1.2 m³ shut;
    - a working seam gives the same at a 1 s and a 15 s step, 176 m³ against 19 m³ for one that never works.
    
    `FloodingTests`' subdivision cases now run with every door shut (the doors are `LeakTests`'), and the torpedo test
    allows for a ship already gone.
  - **Tightness waits for DC4's design block.** The base leak stays 5 cm² × 1, so the Shinano-like case's unsealed hull
    and Lützow's untight other bulkheads aren't in yet.
- **What moved (dc-history, 20 runs; DC0 → DC1):**

  | Case | Water at 2 h / 8 h | Worst list | Lost |
  |---|---|---|---|
  | Seydlitz | 2,560 → 4,730 t / 4,260 → 7,230 t | 1.1 → 1.6° | 20 → 15% (magazines) |
  | Lützow | 580 → 1,530 t / 955 → 2,170 t | 0° | 25 → 30% (one capsized at 6 h) |
  | Audacious, unprepared | 4,470 → 5,370 t / 4,770 → 5,700 t | 0.5° | 0% |
  | Marlborough (?) | 3,340 → 5,370 t / 4,040 → 8,080 t | 1.9 → 17° | 0 → 5% |
  | Pre-dreadnought, one torpedo at anchor (?) | 3,790 → 4,400 t | 8.8 → 9.3° | 5 → 20% (capsized ~80 min) |
  | Pre-dreadnought, two torpedoes (?) | — | 23 → 21° | 85 → 100% (capsized, median 11 min) |
  | Destroyer, 6 HE hits | 8 → 35 t | 0 → 0.8° | 15 → 25% |
  | 30 splinter holes | 156 → 200 t / 307 → 373 t | 4.1 → 5.5° | 5 → 20% (capsized ~6 h) |
  | Bow shell, 28 kn / slowed to 12 | 264 → 1,470 t / 1,470 t (4 h: 1,790 / 1,780) | 0.3° | 0% |
  | North Carolina-type | 1,060 → 2,660 t (4 h: 3,570) | 2.1 → 4.5° | 0% |
  | Shinano-like, unprepared | 12,830 → 20,000 t | 11.5 → 21° | 10 → 70% (foundered ~2⅓ h) |

  - **Bench changes:**
    - The bow shell moved to 12% from the bow and +0.8 m. It had landed in Lion's fine stem, where the spaces hold
      1-100 m³.
    - The benches' holes now spring their space's boundaries at the D their shell's burst would (`Blow.Hole(.., d:)`).
    - A Seydlitz holding 24 kn is a new case.
  - **Hits bench, 180 min:**
    - Iron Duke under Derfflinger: 482 → 559 t.
    - A destroyer under Arethusa's HE: 145 → 264 t; capsized 9 → 10%.
  - **Torpedo bench:** QE with 4: 11,270 → 13,370 t; capsized 53 → 80%.
  - **Squadrons, 60 min:** the damage system 40 µs a call (46-47 at fire B5); no ship lost to water.
- **What it says:**
  - **The slow battle is in.** Unfought, water now walks from compartment to compartment for hours:
    - Lützow 1,000 → 2,200 t over 8 h, against 17 §6.2's model 1,400-3,000;
    - the North Carolina-type 1,060 → 3,570 t in 4 h.
    
    Damage control now has something to fight.
  - **Open doors are the big new killer.** Unprepared ships (half their doors open) and even prepared ones (one in ten)
    flood across bulkheads in minutes once the water reaches the doors.
    - The Shinano-like case is lost in 70% of runs.
    - One flooded boiler room capsizes a destroyer with prepared's doors, which is why the subdivision tests now shut
      them.
    
    DC3's first response (shut the door in a minute) is the cure.
  - **Speed barely matters yet.** Seydlitz holding 24 kn takes 7,010 t at 8 h against 7,230 t slowed; the bow shell
    1,790 t against 1,780 t.
    - Our bulkheads are rated to their real tops, so a sprung one holds even under 28 kn's ram head.
    - 09's model collapses its bow bulkheads because its compartment tops are lower. Not copied.
    - The ram head does drive water through holes over the waterline and into the boundaries, but a hit's holed space
      fills either way.
    
    DC6 (the safe speed) revisits it; the bow wave and trim are the first suspects.
  - **Torpedoes are harsher now.** QE with 2 unfought torpedoes capsizes in 13% of runs within 5 min and 33% within the
    hour; with 4, 80% in 2 h. The record needed 3-5 to sink a capital ship, with counterflooding (DC5).

### DC2 Holes by size
- The four classes in `Hole`, the effective area in the inflow, reachability.
- Nothing is plugged yet, so outcomes shouldn't move (a parity check).
- **Built (done 2026-10-20):**
  - **`ShipCondition.Hole(cell, area, at, count, seam)`** keeps each cell's holes by `HoleClass`, by the size of one hole:
    seam, plug (≤ 150 cm²), patch (≤ 0.3 m²), mat (≤ 5 m²), beyond.
  - **Per class:** the area and count still open, their mean height, the area damage control has worked on, and the
    share of it that still leaks. `BreachM2` stays the sum as the sea sees it (open + worked × left), so every reader is
    unchanged.
  - **Seams:** the blast's deformed outside faces, the near miss's shell seams and the belt leaks' seep and moderate
    regimes are seams. The near miss's splinters come with their count.
  - The bench reports the holes open by class.
- **Parity:** dc-history's water and losses are identical to DC1's, and the sim tests pass (624).
- **What the classes say:**
  - The 30 splinter holes are all plug-class (0.14 m² together).
  - Torpedoes leave 4-9 holes beyond repair at sea (80-140 m²), plus mat-class tears in the cells around the hole.
  - The bench's bow shells are mat-class: a collision mat or nothing.
  - Shell hits on the RN battleship and the destroyer leave seams more than holes.

### DC3 Repair parties take water jobs
- The renames: `RepairParty`, `DamageControlDoctrine`, `Readiness`; the scenario keys.
- Jobs from the damage, the catalogue, the multipliers, the priority, the first response.
- **Tests:**
  - 09 §8's 30 splinter holes, drilled and with nobody;
  - 17 §6.1's drilled crew;
  - fire-first against flood-first on 10 §8's battleship case (the list the same, the fires different).
- **Built (done 2026-10-20), in two commits:**
  - **DC3a, the renames (parity):**
    - `FireParty` → `RepairParty`, `FirefightingDoctrine` → `DamageControlDoctrine`, `FireReadiness` → `Readiness`.
    - Their settings keys: `DamageControl`, `DamageControlDefault`, `Readiness`, `ReadinessDefault`.
    - The scenario key `"damage_control"`, the flags `-damage-control=` and `--damage-control`, and
      `DamageSystem.ReadinessOf` / `DamageControlOf`.
  - **`Repairs`** (`Sim.Combat/Repairs.cs`): one pool of men for fire and water.
    - **The parties** are made at her first damage of any kind (`Make`, B4's numbers).
    - **Planning** happens every 30 s (`PlanIfDue`; the fire's step asks too, for the tools that step fire alone) over
      every job by the doctrine's tiers:
      1. fires next to a magazine;
      2. doors left open on the flooding boundary;
      3. the water jobs, ranked by the flow through them (`JoinFlow`, `CellInflow`, now kept by the flooding step) ×
         what's behind (a magazine 3, machinery and steering 2, else 1); shoring past half the rating, weighed as 500 t/h ×
         value × load;
      4. other fires;
      5. holes within 1 m of the sea that don't leak yet.

      A flood-first doctrine puts the water above the magazine's fires. The nearest free party goes, as B4's do.
    - **The work** (`Work`): man-minutes from the catalogue (`RepairJobManMin`, `RepairJobMen` [EST, 09 §4's times ×
      crews]):

      | Job | Man-minutes |
      |---|---|
      | Door | 2 |
      | Re-dog | 6 |
      | Caulk | 60 |
      | Shore | 80 a set |
      | Plug | 6 a hole |
      | Patch | 80 a hole |
      | Hull plating (seams) | 60 |

      - A party works with its men, at most two crews on a job, at the doctrine's `Skill`: unorganised 0.4, 1894 and
        1905 0.6, rn-1916 0.8, german-1916 and usn-1942 0.9, usn-1944 1 [EST, 09 §5.3].
      - It goes ×1/1.6 in the dark (power under half) and ×1/(1 + 0.1·list°).
      - Edge jobs need their dry side: not full, the water under 1.2 m, not burning.
      - Holes need the water inside under 1.2 m, or under the holes.
    - **Done:** an edge is marked (door shut, re-dogged, caulked, shored once or twice). A cell's holes of the class are
      worked on (`ShipCondition.Worked`) and leave a share leaking: a plug 0.1, a patch 0.08, or 0.3 put on against
      the water still coming in, plating caulked 0.15. Its breach is counted again.
    - **The men on the spot** (`Watch`): a door left open with water at it is shut after the doctrine's watch delay
      (the fire watch's, rn-1916 180 s) if there's crew in either space and its dogs aren't jammed. An unorganised crew
      has no watch, so a party has to come.
    - **Fire's planner moved here unchanged in its weights.** B4's hits on parties now stop and thin a party at a water
      job too.
  - **Tests** (`RepairTests`):
    - 09 §8's thirty splinter holes on QE for 2 h: drilled 28 t with none still open under the waterline, nobody
      972 t and 9.5°;
    - a door left open with water at it: shut at 181 s by rn-1916's watch, 466 s by an unorganised crew's party;
    - a 20 m² hole is never worked on.

    The 1 s against 5 s step test now shuts the doors: the watch shuts one at a moment rounded to the step, and the water
    that got through differs by 7% on the destroyer. With the doors shut the two steps agree exactly.
- **What moved (dc-history, 20 runs; DC1 → DC3):**

  | Case | Water at 2 h / 8 h | Worst list | Lost | Record / wanted |
  |---|---|---|---|---|
  | Seydlitz | 4,730 → 3,600 t / 7,230 → 5,490 t | 1.6 → 1.3° | 15% (magazines) | 5,308 t ✓ |
  | Lützow | 1,530 → 1,230 t / 2,170 → 1,580 t | 0° | 30 → 35% (magazines, one capsized) | 8,351 t ✗ |
  | Audacious, unprepared | 5,370 → 4,930 t / 5,700 → 5,460 t | 0.5° | 0% | capsized at 12 h ✗ |
  | Marlborough (?) | 5,370 → 4,610 t / 8,080 → 5,620 t | 17 → 0.4° | 5 → 0% | survived ✓ |
  | Pre-dreadnought, one torpedo (?) | 4,400 → 3,980 t | 9.3 → 8.9° | 20 → 5% | capsized ✗ |
  | Pre-dreadnought, two torpedoes (?) | — | 21° | 100 → 80% (capsized ~22 min) | sank in 2¾ h |
  | Destroyer, 6 HE hits | 35 → 65 t | 0.8 → 0° | 25% (3 capsized at ~15 min) | most survive |
  | 30 splinter holes | 200 → 8 t / 373 → 7 t | 5.5 → 0.1° | 20 → 0% | the research's drilled ~120 t ✓ |
  | Bow shell, 28 kn / 12 kn | 1,470 → 1,130 / 1,070 t (4 h: 1,470 / 1,380) | 0.2° | 0% | — |
  | North Carolina-type | 2,660 → 1,060 t, held | 4.5 → 3.2° | 0% | 970 t ✓; the list is DC5's |
  | Shinano-like, unprepared | 20,000 → 23,000 t | 21° | 70 → 60% (foundered ~6.7 h) | lost at 7¾ h ✓ |

  - **Hits bench, 180 min:**
    - Iron Duke under Derfflinger: 559 → 321 t.
    - A destroyer under Arethusa's HE: 264 → 244 t; capsized 10 → 7.5%.
  - **Torpedo bench:** QE with 4 unchanged (12,820 t, 80% capsized): the holes are beyond repair, so counterflooding
    (DC5) is the answer.
  - **fire-history:** the first game's cases stay near B5's end (Santiago ablaze 80 → 85%, Suvorov 5%). Savo's 1942 fit
    is lost in 65% of runs (30 at B5; a third capsized), with half its doors open (unprepared) and the parties now
    split between water and fire.
  - **Squadrons, 60 min:** the damage system 49 µs a call (40 at DC1).
- **What it says:**
  - **The crew decides the slow battle, as the research has it** (09 §8: "party skill decides the small-leak battle;
    design and closure decide the big-hole battle"). The splinters, Seydlitz, Marlborough and the North Carolina-type
    now land on the record. The Shinano-like loss and the torpedo bench don't move: what floods there is beyond any
    party.
  - **Open:**
    - Lützow and Audacious don't go as the record has them. Lützow's crew had jammed pumps and three failed patches;
      Audacious listed and her after part filled through unclosed fittings. Ours hold.
    - The pre-dreadnoughts capsize from two torpedoes in minutes and live through one.
    - A destroyer capsizing in 15 minutes from 6 HE hits is faster than any party.

    These are DC5's (counterflooding, the list) and DC9's.

### DC4 Pumps
- The design's fixed drainage replaces the setting, put where it beats the inflow; portable pumps rigged by parties.
- The `drainage` block, its templates and the format step land here (or in DC1 if tightness needs them first).
- **Tests:** dewatering after plugging; pumping in a blackout; NC's "15 minutes a watch".
- **Built (done 2026-10-20):**
  - **The design's `drainage` block** (shipgen: `DrainageInput`, `DrainageTemplates`):
    - Fixed pumps, t/h per 1,000 t; portable pumps and each one's t/h; the counterflooding valves' rate, t/min per
      10,000 t (DC5's); tightness. The templates [EST, 09 §5, 10 §2.4]:

      | Template | Pumps | Portable | Counterflood | Tightness |
      |---|---|---|---|---|
      | Victorian (1890) | 30 | 4 × 5 | 0 | ×3 |
      | Pre-dreadnought (1905) | 40 | 4 × 8 | 8 | ×2 |
      | RN Great War (1914) | 50 | 4 × 15 | 10 | ×1.5 |
      | German Great War (1914) | 50 | 4 × 15 | 20 | ×1 |
      | Interwar (1939) | 60 | 6 × 30 | 18 | ×1 |
      | USN 1943 | 70 | 8 × 60 | 20 | ×1 |

    - **Format 10 → 11** wrote each of the 125 designs its navy's template in its plant's year.
    - `ShipDesign` 45. The goldens gained the block (326 rewritten); nothing else in shipgen's output moved; 372 of 372,
      svg and sprite checks too.
    - `shipgen templates` writes `Content/Templates/drainage.json` and `docs/shipgen/drainage-templates.md`.
    - No weight is booked for the pumps yet, and there are no designer rows (the UI pass).
  - **The class:** `ShipClass.Drainage` (`DrainageRow`, scaled to her displacement).
  - **The flooding:**
    - The intact leak is × the tightness.
    - The fixed pumps are the design's, × power (`PumpTPerHourPer1000T` retired). First they pump the cells with no hole
      of their own. Then, **where a damage-control organisation lines the valves up**, they pump holed cells whose holes
      they beat at full head (`Flooding.FullInflow`). Never a hole they can't.
    - Portable pumps rigged in a cell pump without power and go back to the store once it's dry (`PumpsIn`,
      `PumpsRigged`).
  - **Repairs:** a new job, `RigPump` (40 man-min, 4 men), in a new lowest tier above parts: where at least 5 m³
    stands, the cell's holes don't beat the pump, and the store has one left. The tiers are now: parts < pumps < near <
    fire < water < door < magazine fires.
  - **The door rule** honours the design's `subdivision.doors_below` (merchant hulls by default): doors below the
    waterline too. Warships unchanged.
  - **The bench:** a case may give its own tightness (`DcCase.Tightness`):
    - Lützow ×10, after her bulkheads "not completely watertight" (17 §1.3);
    - the Shinano-like case ×25, the research's worst case (09 §5.3).
  - **Tests** (`RepairTests`):
    - in a blackout, a drilled crew's portable pumps clear half a 92 m³ store (6 m³ left in puddles under the threshold,
      against 46 m³ with nobody);
    - the splinter test's nobody now gets no drainage on its holed cells (893 t, 8.9°).
- **What moved (dc-history; DC3 → DC4):**
  - **Lützow at ×10:** 1,580 → 2,570 t at 8 h; lost 35 → 45% (magazines, one capsized). The record has 8,351 t.
  - **Marlborough** (RN ×1.5): 5,620 → 5,660 t; capsized at ~8 h in 20% of runs (0 before).
  - **The 1915 pre-dreadnought at anchor** (×2): worst list 8.9 → 13°; lost 5 → 20%.
  - **The Shinano-like case at ×25:** lost 60% at ~6.7 h → 95% at ~49 min. The research's ×25 on our base 5 cm² (its
    own base was 20) is still too much for a Queen Elizabeth half Shinano's size taking the same four torpedoes. ×3 gives
    3 h and ×1 6.7 h. Kept at the research's number.
  - **On `yamato`,** four torpedoes at once capsize her in 2 minutes whatever the tightness: 6,800 t and 21.6° in the
    first minute, faster than any counterflooding. Noted in bugs.md (hull and protection, not damage control).
  - **Hits bench, 180 min:** Iron Duke 321 → 300 t; a destroyer 244 → 217 t.
  - **Torpedo bench:** QE with 4: capsized 80 → 87%.
  - **Squadrons:** the damage system 48 µs a call.

### DC5 DC Central: counterflooding
- The tanks, the doctrine's threshold, the design's rate, one tank at a time, capacity left, the conned stand-in.
- B2's magazine flooding moves here.
- **Tests:** the NC mechanism check; QE with 1-4 torpedoes on one side against the baseline.

### DC6 Speed
- The safe speed, the captain keeping to it after the hold time, the collision mat, and hull work loosening at speed.
- **Tests:** 09 §8's bow shell at 28 kn and slowed to 12 kn; Seydlitz and Lützow on the bench.

### DC7 Steering and parts
- The steering ladder; freeing a jammed rudder; the parts' repair table and its draw at the damage; battle-long jams;
  rooms flooded out of action brought back once dewatered (the plant's power with them).
- **Tests:** a ship that lost her steering takes it back by hand in ~15 min and circles no more; the share of disabled
  mounts back within the hour.

### DC8 What's seen and logged
- Log lines in symptom → cause form; the list and slowing as the enemy sees them (already from the attitude and speed).
- What the matchup and fall-out read: the doom clock's data for the UI pass. No UI here.

### DC9 Anchors and cost
- The yardstick above, the first game's cases first. Tune only within the [EST] numbers and log what moved.
- **The cost:**
  - the damage system's share of the squadrons scenario;
  - a burning, flooding ship-hour on the bench;
  - the longest call.
- **Open items carried:** the clutter's weight (fire B5), whether battleships still list too little from torpedoes
  (`fighting.md` open item 8).

## What stays out

- Sailors as agents, hoses, pumps and valves as parts, and the fire main and drainage main as networks (rough by design).
- The service networks: power and hydraulics routed through cells, casualty power, splicing (decision 5).
- Tenders, docks, pumping counterflood water back out, shifting fuel to restore buoyancy (decision 6).
- Ships alongside (pumps, hoses, the wounded), towing, abandoning and scuttling: the AI's and missions'.
- Hull-girder failure under flooding and whipping, beyond the broken back that's already there.
- Non-linear settling (`sinking.py`): only if the anchors need it.
- Fuel vapour, aviation fuel, foam, sprinklers: later cuts.
- The player's orders and the designer's rows: the UI pass.
- The coarse tier's damage control: with 4.8/4.9.

## Questions for the user

None open (2026-10-20).
