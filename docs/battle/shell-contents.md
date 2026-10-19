# What's inside: contents in a shell's path

Proposed 2026-10-18. The research landed 2026-10-19
([damage/09](../../research/simulation/damage-research/09-contents-in-the-path.md), with `contents_ref.py`); the plan
below was drafted from it and agreed 2026-10-19. Found from a test: a 15" AP shell through a destroyer's engine
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
- **Done 2026-10-19.** `Terminal.Slowed` brings a running fuze's burst in by the speed lost, at a plate the shell gets
  through and at one it glances off. The trace's steps carry the delay left (`PlateStep.FuzeLeftS`, the Shot window's
  "fuze ms"). Test: `ShotRangeTests.The_fuze_runs_on_time` (Lion on Bismarck; 60 plates and 58 bursts checked).
  - The `hits` bench, 2,000 hits each (Iron Duke on M class, Wiesbaden and König; König on Iron Duke): the outcomes are
    the same to 0.1%, and the rest moves only in the third digit (König's shells burst 5.5 m behind the fuze start, was
    5.6). As expected: the delays are short (RN 1912 APC 0.003 s, about 1.3 m) and few plates stand behind a started
    fuze. The step matters once C1 puts contents in the way.

### C1 Solids, machinery first

**C1a The plate step as one function.**
- Lift the plate case out of `Resolve` into one method that takes the plate as met: thickness, material, obliquity and
  normal. The method does the cap, shatter, glance, three limits, turn and fuze start.
- A struck solid will call the same method; there is no second copy.
- A pure refactor: the `hits` bench and `ShotRangeTests` stay identical hit by hit.
- **Not needed (2026-10-19).** A struck solid is put on the hit's path as a crossing of its own
  (`CrossingKind.Struck`, `PlateKind.Contents`, Index the room: `HitPath.Insert`), so the walk's one plate code takes it
  like any plate, and everything after the walk that reads crossings (the stopping plate, the pieces, the Shot window)
  sees it too. Still one implementation, with no refactor.

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
- **Ship size.** Damage/09 has destroyer rows and large-ship rows. No switch between them: thicknesses and chords are
  interpolated between the two rows by ~~power per shaft~~ **displacement** (open question 5).
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

**Done 2026-10-19 (C1b, C1c).**
- **Data:** `Contents` (Sim.Ships) holds damage/09 §2.2-2.3's rows, each at a destroyer's size and a large ship's; [EST]
  marks the sizes the research didn't give. The compiler gives each boiler and engine room its row
  (`ShipClass.ContentsOf`): the band over the room's cells, λ = chord / packing, objects cumulated for the draw, and
  materials from the catalogue when the design's report lacks them. Cast iron is in the catalogue (q 0.35, brittle).
- **Plant kinds:** `boilers` and `engines` on the tech block (express boilers are `small_tube`; their size comes from
  the ship's). Format 10 reads them off the block's name, the template it was picked from; every shipped design and
  template states them. The goldens gained the two keys (shipgen 43).
- **Size by displacement, not power per shaft** (a change from the agreed text, flagged for the user): power per shaft
  doesn't tell a destroyer from a battleship (M class about 6.2 MW a shaft, Iron Duke about 5.4). The rows differ because
  the objects grow with the ship (a battleship's turbine about 2.4 m across, a destroyer's 1.2 m). So size runs on
  ln(Δ/1,000 t) / ln(20,000/1,000), held to 0..1: still continuous, no switch. This game's WW1 destroyers are heavy (about
  1,200-2,500 t: the destroyer crunch's open item), so they sit at 0.05-0.30, not 0.
- **The walk:** in a room with contents, each stretch between crossings, clipped to the band, gets one Exp(λ) draw.
  A strike puts the object's skin on the path, and its core by its chance (behind the interior, or halfway through the
  object). The interior is a granular run on the fuze's clock (`WaterRun.Through`, `SimSettings.GranularDecayC` 96,000).
  No other solid is met within the object's chord. Round solids: a cylinder lying fore and aft (sin θ uniform), a sphere
  (sin² θ), a bar (offset uniform: chord and angle from one draw; it goes in as a plate of chord · cos θ, since the
  plate model puts the 1/cos back). Flat ones are square to the room's axis nearest the flight. Contents have no plate
  lot. `MachineryArmChance` is gone; the fuze starts on a struck solid as on a plate (`FuzeCause.Contents`).
- **Effects:** a strike damages its room. Rooms with contents no longer take the line-of-flight `RoomVital` roll (pieces
  of a broken shell still do, since the walk ends where it broke). A struck solid throws no plate debris or spall: its
  own splinters are later (open question 4).
- **Trace and readouts:** the Shot window names each struck solid and shows each interior's run. `shipgen contents`
  prints each room's rows and the chance a beam crossing at a random height meets a solid: destroyer small-tube boiler
  rooms 0.78 (09: 0.75 at 8.5 m beam; M class is 11.4 m), geared turbine rooms 0.60 (09: 0.53, direct turbines, 8.5 m),
  a Braunschweig-type VTE engine room 15 m wide 0.87 (09: 0.77, a 9 m wing room). The `hits` bench breaks out hits
  through boiler and engine rooms.
- **Anchors on the `hits` bench** (2,000 hits, 12 km, beam on):

  | Target, shooter | Through the boiler rooms: struck, burst there | Through the engine rooms: struck, burst there |
  |---|---|---|
  | M class, Revenge 15" APC | 68%, 20% | 40%, 31% |
  | M class, Iron Duke 13.5" | 66%, 25% | 44%, 34% |
  | V25, König 12" | 68%, 12% | 51%, 21% |
  | Wiesbaden, Iron Duke | 24%, 71% | 14%, 77% |

  - Engine rooms come out a little under 09's "about half": shells falling at 11° cross only part of the band. Shells
    plunging into a destroyer's engine room (`ContentsTests`, Lion from 20 km) strike almost always and burst on what
    they struck 80% of the time.
  - Boiler rooms: strikes are common, bursts rarer, because a small-tube boiler's 6-8 mm casing doesn't start a heavy
    fuze; only its drums (about 0.2 of strikes) do. That's 09 §2.2's "a water-tube boiler mostly arms a big shell
    (skin and drum) and rarely stops it". The old roll armed far more often (0.45 a cell, and the rooms are two cells
    deep), so over the whole ship M class's bursts inside fell from 12.7% to 7.2% under Revenge's fire.
  - The battleships (König, Iron Duke, Braunschweig under 13.5") don't move: few hits reach their machinery.
  - The Scotch boiler's 75% for a 6" was for a full-diameter crossing; at the mean chord its interior keeps about 85%.
- **Scenarios** (one run each, 120 min; chaotic): squadrons 71 → 69 hits, lost Queen Mary (magazine) → none; flotilla
  one destroyer lost either way; Tsushima 1,590 → 1,838 hits, 5 → 6 lost. The cost per hit is unchanged.

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

**Done 2026-10-19, with C3a** (the coal is the same mechanism with the granular rule).
- **What stands where follows what Shipgen stowed** (`Powerplant.Bunkers`), a change from the text above: no design keys
  yet. A coal plant fills its wing bunkers (their tonnes, 0.9 of them), then end bunkers (0.9), and its double bottom
  holds the crew's feed and fresh water (`Crew.WaterInDoubleBottomM3`). An oil plant fills the double bottom (0.6 of
  it), then the TDS layers (0.3 of them: Shipgen's half the protection's width at 0.6), then end bunkers (0.9), as far as
  the fuel goes, each group filled evenly. Fuel tanks hold their tonnes. Choosing otherwise per design (a TDS layer's
  `fill`, the double bottom's use) stays with protection-plan H3's open item and TODO "Double-bottom contents", since
  it moves the stowage and weights too. (`ShipClass.Stowages`, `RoomStowage`.)
- **A level, not whole layers:** a room fills from its floor, so a TDS layer at 0.3 is oil in its lower 30% (true to
  the tonnes; the real practice kept some layers full and some void). Levels are flat in the ship's frame, not her heel.
- **The load state:** `SimSettings.FuelShareDefault` 0.8, or the scenario's `"fuel": { "Britain": 0.6 }` per side
  (`DamageSystem.SetFuel`, hashed). `Stowage.Levels` spreads it: with `ProtectiveBunkersLast` (on) the protective side
  spaces (rooms off the centreline: wing bunkers, TDS layers) stay full before the rest get any. The ship's levels are on
  her condition (`ShipCondition.Level`), made when she's first hit.
- **The walk:** in each stretch of a cell, before any solid struck in it, what's below the room's level is a run
  through its medium, and the flooding's water above that up to the cell's own level (`WaterM3` over its capacity) a
  run through the sea's (`Terminal.Media`, `WaterRun.Through` on the fuze's clock). Liquids are the sea's 25 calibres
  by density (open question 6), coal 96,000 over 800 (120 calibres).
- **Anchors** (`shipgen contents`): 4 m of a liquid layer keeps a 15" at 66%, 3 m of oil a 12" at 69%: 09's own. 2 ft
  of coal costs a 4-15" shell at 500 m/s 1.3-4.9% of its speed, as 0.77-0.85 in of mild steel by Thompson's limit (09:
  0.76-1.41 in by DeMarre, rising with the calibre): "about 1 in", the right direction.
- **What it does:** little to outcomes, as 09 says. Coal and oil cost a heavy shell a few percent, and a half-empty
  bunker protects only its lower part. At 0.8 of a load Hessen's coal stands at −4.7 m, under her −3.8 m waterline, so
  a flat hit at the waterline crosses empty bunker; only a falling shell meets the coal (`ContentsTests`). The `hits`
  bench (Iron Duke on Hessen and Wiesbaden, König on Warrior and Iron Duke, Revenge on M class) moves only in the third
  digit. Scenarios (one run each, 120 min): squadrons the same (69 hits, none lost), flotilla the same boat lost 18 s
  later, Tsushima 6 lost either way (1,838 → 2,124 hits).

### C3 Coal

- **C3a Bunkers.**
  - A bunker is a granular medium: 800 kg/m³, L = 120 calibres, its level from the load state.
  - A doctrine knob, "protective bunkers last", on by default: the side bunkers at the waterline stay full and the
    others are burnt first.
  - Anchor: the readout's table of 2 ft of coal ≈ 0.8-1.4 in of armour by calibre (09 §4).
  - **Done 2026-10-19 with C2** (above).
- **C3b Splinters,** measured on its own because it moves E1's fragment counts. Coal along a fragment's way through a
  bunker stops it: the same medium at the fragment's own size, e-fold about 15 sizes.
  - **Done 2026-10-19.** A burst's fragments to the parts in its space and to the spaces next door
    (`Effects.Burst`) go through what its space stows below its level, and the flooding's water above that, counted
    as the air that slows them as much (`Effects.MediumDragM`, as E7 counts a near miss's water): coal e-folds them every
    0.75·m^⅓ m (`SimSettings.CoalFragmentDragL1`: 15 sizes of a steel splinter, 09's [EST]), a liquid the sea's 0.3·m^⅓
    by density, against the air's 247·m^⅓. 1.4 m of coal is 455 m of air to them: a 20 mm splinter keeps about 1% of
    its speed, so it's stopped within the metre 09 gives. Test: `ContentsTests.Coal_catches_fragments`.
  - **What it does:** little yet, since few bursts happen in coal. An observation: Shipgen sizes coal for the design's
    range, so Hessen's wing bunkers are only 66% full at full load (her coal tops out at −3.4 m against her −3.8 m
    waterline), and a waterline hit crosses the empty top of the bunker. Real coal-burners carried far more than their
    normal load. Left for the stowage (Shipgen), not tuned here.

### C4 The rest

- **C4a Magazines and handling rooms.**
  - A magazine has its shell band at the room's bottom, with the charges above, in every navy (open question 7). The
    stowed shells are round solids of the served mount's
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
5. ~~Destroyer rows against large-ship rows.~~ Interpolated (the user, 2026-10-19), by displacement rather than power
   per shaft, which doesn't separate them (C1 done, flagged). No hard
   switches: the only step changes allowed are the ones physics makes (a delay fuze bursting inside a wide ship and
   beyond a narrow one).
6. ~~Liquids' decay.~~ The sea's 25 calibres, scaled by density (the user, 2026-10-19). (09 A)
7. ~~Shell room above or below the charges.~~ Shells below, everywhere: no variation by navy (the user, 2026-10-19).
8. ~~Plate lots for contents.~~ None: only the shot's scatter (the user, 2026-10-19).

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
