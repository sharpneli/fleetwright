# What's inside: contents in a shell's path

Proposed 2026-10-18; waits on the research at the end. Found from a test: a 15" AP shell through a destroyer's engine
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

## Order (proposed)

- **C0 the fuze on time.** The burst from the delay left over the speed along the path. Moves numbers a little; the
  hits bench before and after.
- **C1 the mechanism and machinery.** One strike event per room, replacing the arming roll and `RoomVital` for
  machinery. Anchor: the `hits` bench, big AP on a destroyer and on a cruiser's machinery.
- **C2 liquids.** Tanks, the double bottom, TDS layers and flooded cells, by fill.
- **C3 coal.** Bunkers by their fill, with a fuel state to read it from.
- **C4 the rest.** Magazines and handling rooms, turret and barbette interiors, shafts, steering.

## Open questions

1. The obliquity of what's struck: drums, rotors and stacked shells are round, and their facing is unknown. A draw
   from a distribution?
2. Whether a strike turns the shell (bending the path as a plate's turn does) and by how much.
3. How far the data splits: reciprocating engine against turbine, large-tube boilers against small-tube, diesel.
4. Whether struck contents throw their own splinters (E1 and E3's ground), later.

## Research requests

### 2026-10-18 - What inside a ship stops or slows a shell, 1890-1925 first
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
