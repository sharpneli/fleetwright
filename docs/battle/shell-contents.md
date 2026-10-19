# What's inside: contents in a shell's path

Proposed 2026-10-18. The research landed 2026-10-19
([damage/09](../../research/simulation/damage-research/09-contents-in-the-path.md), with `contents_ref.py`); the plan
below is drafted from it 2026-10-19 and waits for agreement. Found from a test: a 15" AP shell through a destroyer's engine
room, its fuze started at the side plating, passed out of the far side and burst beyond her.

## Today

- Only plates resist a shell: the hull's plating, armour, the decks and bulkheads between cells, each part's skin, and
  the sea outside (`WaterRun`). Nothing inside a room or a part slows, turns or stops it.
- A room is empty space between its walls: crossing into one is a `CellIn` with no plate
  ([ShipTracer.cs](../../src/Fleetwright.Sim.Ships/ShipTracer.cs) `CellChange`). A part is a hollow skin of its
  `PlateMm` or `ArmourMm`, and a turret's floor isn't a plate at all (`ShipTracer`, the part crossing).
- A base fuze's burst is fixed as a distance when it starts, `delay × v` ([Terminal.cs](../../src/Fleetwright.Sim.Combat/Terminal.cs)
  `Start`). A plate that slows the shell later doesn't bring the burst in, though the fuze runs on time.
- Machinery is two rolls on the same cell that know nothing of each other. `MachineryArmChance` (0.45, rolled for each
  machinery cell entered) starts the fuze in `Terminal.Resolve`. `RoomVital` damages the room on the line of flight in
  `Effects`. So a shell can wreck the turbines with its fuze not started, or start on machinery it never damaged. A
  room split into more cells also arms more often.
- So a delay fuze passes through a destroyer even when it has armed: 0.025-0.035 s at about 450 m/s is 11-16 m, against
  a beam of about 9 m. That's right for a shell that misses the machinery (Samar's destroyers were holed through by AP
  that didn't burst), but not for one that strikes the plant.

## The mechanism

One general "struck the contents" event, with data by room kind (and part kind) rather than a rule for each space:

- **Solid contents.** Each kind has a mean free path λ: the chance that a line crossing L metres of the room meets
  something is 1 − e^(−L/λ). That depends on the length crossed, not on how the room is cut into cells. What it meets
  is a steel-equivalent thickness (with a scatter) and a material, put through the plate model like any plate:
  Thompson's limit, the turn, break-up, stopped, the fuze started.
- **Liquids.** A decay length by density, like the sea's (`UnderwaterDecayCalibres`), over the length the path runs in
  liquid. How full the space is comes from the ship's state: fuel left, a TDS layer's fill, and the water the flooding
  model has put in the cell.
- **What a strike does.** It damages what was struck (the room's node: this replaces the `RoomVital` roll for these
  kinds), and starts a base fuze the way a plate does (this replaces `MachineryArmChance`).
- **The fuze runs on time.** The burst comes from the delay time left, spent over the shell's speed along the path, so
  every slowing brings it in, at plates as well as contents.
- **Parts.** A turret's guns, a barbette's hoists and a shaft's solid core take the same data by part kind.

The rolls go on the hit's own stream, and nothing allocates (the terminal model's rules).

## The list

Most important first.

| # | Space or part | What's in it | Why it matters | Data |
|---|---|---|---|---|
| 1 | Boiler room | Drums, tube banks full of water, uptakes | A large share of the hull amidships; a shell striking them should be slowed, turned or stopped, and burst there | R1 |
| 2 | Engine room | Turbine rotors and casings; reciprocating cylinders, crankshafts; condensers, gearing | The same; the case that found this | R1 |
| 3 | Bunker (coal) | Coal, part full, less as it's burnt | Coal-era ships, 1890-1915, the first cut; a protection the designers counted on | [03-materials.md:771](../../research/simulation/armor/03-materials.md) has "2 ft ≈ 1 in of armour" ([INF]); [damage-model-research.md:113](../../research/simulation/damage-model-research.md) calls its value against shells low; R3. Needs a fuel state |
| 4 | Fuel tank, double bottom, TDS liquid layers, cargo tank, flooded cells | Oil or water | The sea outside slows a shell, the same water inside doesn't. Diving shells under the belt meet exactly these layers | R2 |
| 5 | Magazine, handling room | Stacked shells (dense steel), charges, hoists | Moves where it bursts: the handling room or the magazine | R4 |
| 6 | Turret and barbette interiors | Guns, slides, breeches, rammers; hoists, the trunk | A shell through the face crosses an empty box today | R5 |
| 7 | Shaft, shaft alley | A solid steel shaft, 0.3-0.6 m across | Small target, but solid | R6 |
| 8 | Steering gear | Steering engines, tiller, quadrant | Small, heavy | R6 |
| 9 | Torpedo room, tubes | Torpedoes | Little resistance; striking a warhead is its own question, later | — |

Left empty, rightly or close enough: accommodation, stores, the transmitting station, the hangar, superstructure,
funnel and uptake interiors, masts. A merchant's hold depends on her cargo: data on the design, not a rule.

## Plan

Steps in order. Each step is committed when its bench and tests pass. Damage/09's numbers go in as they are, with the
[EST] rows flagged in the data, and are tuned only against its anchors (rough anchors are enough).

**Words.** Damage/09 uses "fill" three ways. Here:
- **packing** is the share of a room's band that solids take (09's f);
- an object's **interior** is the medium inside it (09's object fill: a boiler's water and tubes);
- a room's **level** is how full it is of liquid or coal (09's fill share).

The TDS design key `fill` (liquid or void) keeps its name: it says what's in a layer, and the level says how much.

### C0 The fuze on time

- `Terminal.Resolve` keeps the fuze's **time left** in place of a burst distance fixed when it starts (`Start`:
  `burstT = t + delay × v`). Between crossings the shell runs at its speed, or decays in a medium (`WaterRun.TimeS`,
  `RunInM`), and it bursts where the time runs out. So every slowing brings the burst in: plates now, contents and media
  from C1.
- The water run already counts time (`spentS`). It joins the same clock and nothing changes there.
- Trace: the Shot window shows the time left at each step.
- Measure: the `hits` bench before and after, outcome shares by target. Expect fewer `BurstBeyond` behind thick plates.
- Tests: a slowed shell bursts sooner than an unslowed one with the same fuze; still zero-alloc.

### C1 Solids, machinery first

**C1a The plate step as one function.**
- Lift the plate case out of `Resolve` into one method that takes the plate as met: thickness, material, obliquity and
  normal. The method does the cap, shatter, glance, three limits, turn and fuze start.
- A struck solid will call the same method; there is no second copy.
- A pure refactor: the `hits` bench and `ShotRangeTests` stay identical hit by hit.

**C1b The data.**
- **A contents table** in Sim.Ships, keyed by room kind and variant, and by part kind. A row has a band, a packing, a
  chord and a mix. Each object in the mix has:
  - a share;
  - a skin {mm, spread, material, round or flat};
  - an interior {density, share of the chord, medium kind};
  - a core {chance, mm, material, shape}.

  The rows are damage/09 §2-§7 as written.
- **Cast iron** joins the material catalogue at Q 0.35 [EST] (material-templates.md). Forged parts take "high-tensile
  steel"; casings and boiler plate take "mild steel".
- **The plant's kinds become design facts** (looks are decorative only).
  - The machinery tech block gains `boilers` (cylindrical, locomotive, large_tube, small_tube, express) and `engines`
    (compound, vte, direct_turbine, geared_turbine, diesel, petrol).
  - The templates (plant-templates.md) state them. Format 10 fills them in on older designs from the template whose
    name the block matches, else from `part_load`.
  - Locomotive boilers take the cylindrical row at destroyer size, and compound engines the VTE row [EST]. Diesel and
    petrol take the auxiliary-machinery row for now.
- **Ship size.** Damage/09 has destroyer rows and large-ship rows. A general rule replaces the switch: thicknesses and
  chords fall between the two rows by power per shaft (open question 5).
- **Compiled into `ShipClass`.** Each room with contents gets its row resolved: the band as heights from the room's
  `Base`/`Top`, and λ = chord / packing. Each part kind gets its row the same way. Read without allocation.
- **A `contents` readout in the CLI:** for each room, the chance that a beam crossing at a random height meets a solid.
  The anchors from 09 §2.3:

  | Room | Meet chance |
  |---|---|
  | Destroyer turbine room | 0.53 |
  | Destroyer small-tube boiler room | 0.75 |
  | Battleship VTE wing engine room | 0.77 |
  | Battleship Scotch boiler room | 0.84 |

**C1c The walk meets solids.**
- **Where:** each gap between two crossings in a room with contents is clipped to the room's band. The next solid is an
  Exp(λ) draw on the hit's stream, one per gap. The draw is memoryless, so cutting a room into more cells changes
  nothing.
- **A strike**, in order:
  1. The object is drawn from the mix.
  2. Its skin goes through the plate step. For a round object, one draw of the impact offset gives both the chord and
     the obliquity (09 §1.3, one helper). A flat object is met at the path's obliquity to the room's axes.
  3. Its interior is a medium run. The granular decay by density is built here, ahead of C2.
  4. Its core is met with its own chance.
  5. The walk goes on, with a fresh draw for the next solid.
- **What a strike can do:** stops, glances, turns and break-up are the plate step's own. The path bends at the strike
  point (`PathBender`).
- **The fuze** starts on a solid as on a plate. `MachineryArmChance` is removed.
- **Effects:**
  - Strikes are kept on the result in a small fixed buffer, with no allocation.
  - Each strike damages its room's node. For kinds with contents, this replaces the `RoomVital` roll on the line of
    flight.
  - Kinds without contents keep `RoomVital`, and a burst's fragments use it as before.
- **Trace:** each strike is a step in the Shot window ("turbine rotor, 140 mm forged, round, 38°"), so `-hitshot`
  shows it.
- **Anchors on the `hits` bench:**
  - A 15" AP shell through a destroyer's turbine room bursts inside about half the time and passes through the rest
    (WDR 51, Johnston).
  - A Scotch boiler keeps a 6" shell at 75% of its speed.

  Report how far the matchup and the squadrons scenario move.

### C2 Liquids

- **Decay by density.** `WaterRun.DecayM` takes a density and a medium kind: L = calibre × C / ρ.
  - Liquids: C = 25 calibres × 1,025, or 50 × 1,025 for a diving shell.
  - Granular media: C = 96,000.
  - Both go in `SimSettings`. A test keeps the sea exactly as before.
- **Levels are state,** on `ShipCondition`: one level per room, flat at the ship's attitude.
  - Fuel tanks: from the load state below.
  - The double bottom: a design key for its use (void, feed water, oil). This closes TODO "Double-bottom contents".
  - TDS layers: the `fill` key (liquid or void) per layer, which protection-plan H3 left unbuilt. If C1b and C2 land
    close together, they share one format bump.
  - Flooded cells: the flooding model's `WaterM3` over the cell's floor area.
- **The load state.** A ship enters a battle with a fuel share, spread over her tanks and bunkers. The share is the
  scenario's, by default the design's normal load. No fuel burns during a battle: a battle lasts hours, and at
  300-500 t/day that is a few percent (rough for non-core systems). Flotation keeps its loads as today.
- **The fuze.** A liquid never starts one. A fuze already running keeps to its C0 clock, so the burst comes into the
  layer.
- **Anchors:**
  - A 15" shell through a 4 m liquid layer keeps 66% of its speed.
  - 3 m of oil keeps a 12" at 69%.
  - A diving shell under the belt against a liquid-loaded TDS: measured before and after.

### C3 Coal

- **C3a Bunkers.**
  - A bunker is a granular medium: 800 kg/m³, L = 120 calibres, its level from the load state.
  - A doctrine knob, "protective bunkers last", on by default: the side bunkers at the waterline stay full and the
    others are burnt first.
  - Anchor: the readout's table of 2 ft of coal ≈ 0.8-1.4 in of armour by calibre (09 §4).
- **C3b Splinters,** measured on its own because it moves E1's fragment counts. Coal along a fragment's way through a
  bunker stops it: the same medium at the fragment's own size, e-fold about 15 sizes.

### C4 The rest

- **C4a Magazines and handling rooms.**
  - A magazine has its shell band at the room's bottom. The stowed shells are round solids of the served mount's
    calibre, λ 2 m, and 1.4 d between shells inside a stack.
  - The charges are a light medium.
  - A handling room has hoists and cages.
  - The chance that a struck filled shell goes off belongs to the magazine model: a hook, set to 0 for now.
- **C4b Turret and barbette interiors** (parts).
  - The gun-house rows scale with the mount's calibre. The guns' axis follows the mount's bearing at the moment of
    the hit.
  - The barbette has its trunk and its top band: the turntable and working-chamber floor. That top band also gives the
    turret the floor it lacks today.
- **C4c Shafts and steering.**
  - A shaft's diameter comes from the cube-root rule, from power per shaft and rpm (rpm by engine kind). It replaces
    the fixed `Propulsion.ShaftR`, so the hitbox and the drawings change with it; any weight change is reported. A
    shaft is struck as a round solid.
  - The steering room holds the rudder stock, the tiller and quadrant, and the steering engine.
- Torpedo rooms stay empty (item 9 of the list, later).

### After the plan

- The matchup takes the contents as shares in the final balance pass, as it does PM.
- The designer's tech-block picker already shows the plant's kinds through the templates. Nothing else in the designer
  until the UI pass after the damage plan.
- The hit view's playback draws struck solids and liquid runs (hit-view.md item 5).

## Open questions

1. ~~The obliquity of what's struck.~~ It comes from the shape: for a round object, one draw of the offset gives both
   the chord and the obliquity (09 §1.3).
2. ~~Whether a strike turns the shell.~~ No new rule: the plate step's glance and turn (09 §1.3).
3. ~~How far the data splits.~~ By boiler kind and engine kind (C1b).
4. Whether struck contents throw their own splinters. Later (09 B).
5. Destroyer rows against large-ship rows: interpolate by power per shaft (recommended), by displacement, or switch on
   the plant block (express boilers)?
6. Liquids at the sea's 25 calibres (recommended: one rule), or at a stable shell's ~170 scaled by density (liquid
   layers about 7× weaker)? (09 A)
7. Whether the shell room sits above or below the charges is a magazine design fact, by navy and period ([UNCERTAIN]
   in 09 §5). Shells below by default?
8. Plate lots for contents: none (a ship's lot draw is for her armour), only the shot's scatter. Recommended.

## Research requests

### 2026-10-18 - What inside a ship stops or slows a shell, 1890-1925 first
- **Answered 2026-10-19** (draft): [damage/09](../../research/simulation/damage-research/09-contents-in-the-path.md).
- Question: the contents of a ship's spaces as a shell meets them, as numbers a general model can take (a mean free
  path or share of the space filled, and the steel-equivalent thickness of what's struck), by type and period rather
  than class by class:
  - **R1 machinery:** boiler rooms (drums, tube banks with their water, by boiler type) and engine rooms (reciprocating
    engines' cylinders and crankshafts, turbine casings and rotors, condensers, gearing): how much of the space's cross
    section is solid, and what a shell meets there. War-damage and trial cases where a shell was stopped, turned or
    burst in machinery, or passed through it whole.
  - **R2 liquids inside the hull:** shells through fuel oil and water in tanks, the double bottom, liquid-loaded TDS
    layers and flooded compartments: the slowing against the sea's, and what it does to fuzes; any trials against
    diving shells.
  - **R3 coal:** a range for "2 ft of coal ≈ 1 in of armour" (by calibre, if it varies), how full bunkers were in
    service, and whether coal stops splinters and shells differently.
  - **R4 magazines and handling rooms:** stowage density of shells and charges, whether stacked shells stop or slow a
    shell, and cases.
  - **R5 turret and barbette interiors:** the share of a gun house's cross section that guns, slides and breeches
    fill, their equivalent thickness, the hoists and trunks in barbettes; cases of shells striking guns inside.
  - **R6 shafts and steering gear:** shaft diameters and steering gear by ship type and period.
- Context / relevant files: this file; `src/Fleetwright.Sim.Combat/Terminal.cs` (the plate walk, the fuze,
  `MachineryArmChance`), `src/Fleetwright.Sim.Combat/Effects.cs` (`RoomVital` on the line of flight),
  `src/Fleetwright.Sim.Combat/WaterRun.cs` (the sea's decay), `research/simulation/armor/03-materials.md` (COAL,
  TEAK), `research/simulation/damage-research/01-capital-subdivision.md` (coal bunkers). Not blocking: C0 goes ahead
  without it.
- Wanted output: for each of R1-R6, ranges by type and period with sources: a fill share or mean free path, a
  steel-equivalent thickness (with a spread) for what's struck, a decay length for the liquids, and the anchor cases.
