# Plan: the battle module's data, from designs to ships at sea

2026-10-11. Status: the shape is agreed with the user (2026-10-11); the names and the open questions at the end are not.
Each step is its own session and starts with an investigation of its own: read the code it touches, check this plan
against it, agree the step's details with the user, then build. Update this file as steps finish (what was done, what
changed from the plan).

## Goal

A battle holds many ships. Each ship is of one design; several ships often share a design, and several designs are in
memory at once. The game draws ships with the sprite model and computes damage with the hitbox model, and soon ships
need to be damaged: each one carries a condition table tied to the hitbox model. This plan sets out how the data is
laid out so that the shared, per-design data and the per-ship state stay apart.

The battle is not an arena: the world is the whole planet at real scale (`research/naval/realtime-tech-stack-plan.md`),
and a battle is ships that happen to be near each other on it. Nothing here may assume a flat map; see "The world".

## Where we are (the review of 2026-10-11)

What already fits:

- `Ship` (`Fleetwright.Shipgen/Model/Ship.cs`) is per design: the design, the report, the hitboxes and the dressing.
  The sprites are drawn from the hitboxes (`TopView`), and both use one frame: ship-local metres, origin at the ship
  and sprite centre, +x to the bow, +y to starboard, heights over the main deck.
- The hitbox model is small: Bismarck has 93 components, 220 cells, 87 rooms and 43 bulkheads; Yamato is about the
  same, Des Moines smaller (63, 85, 55, 18). A condition table is a few hundred entries a ship: a few MB for 2,000
  ships. Dense arrays per ship are fine.
- The cell graph is already the flooding graph: `Cell.Neighbours` says what joins two cells ("open", a deck or a
  bulkhead), and cells carry volume, permeability, crew and the armour above. The flooding demo (`sinking.py`,
  DECISIONS 2026-10-06) ran on it.
- `HullField` is the pattern to follow: the exported form compiled into an allocation-free query object with an exact
  raycast. `Tools/ShellTest.cs` goes further and compiles the armour, barbettes and uptakes into solids for ray tests.
  That is the first hit query, living in a tool.
- Sprite mounts and hitbox components share ids (`TopView` makes a mount of each component with `Local`), and
  `ShipSpriteRenderer.Render` takes bearings in the mounts' order.
- The look is apart from the physics: `DesignWorker` keys its cache on design + look and reuses the built ship for a
  new look, so one class drawn in several navies' colours shares its physics.

What doesn't fit a battle:

1. **The hitbox model is JSON-shaped.** Links are strings (cell ids like `"5 second C"`, `Neighbours` as `string[]`
   pairs, `Room.Cells`, `Component.Magazine` / `Barbette` / `Funnel`). `Component` is one class of about 50 nullable
   fields told apart by a string `Kind`. A condition table needs int indices to stand beside, and hot paths (rays,
   flooding ticks) shouldn't chase strings and lists.
2. **The shared data is mutable.** `Cell`, `Component`, `Room`, `Bulkhead` and `SubDeck` have public setters (the
   builder fills them in). Shared by 30 ships and read from the sim's worker threads, one stray write (`cell.Crew -= 3`)
   corrupts every sister ship.
3. **Some design fields are starting conditions, not constants:** `Component.Rounds` / `ReadyRounds` / `Torpedoes`,
   `Room.Tonnes` (fuel), `Cell.Crew` / `BattleCrew`; and what becomes dynamic once a ship settles or trims:
   `Cell.BelowWaterline`, `Tier.Submerged`, `Vertical.Waterline`, `Hydrostatics`.
4. **Naming:** Shipgen's `Ship` is a built design, a class of ships. The battle wants "ship" for the vessel at sea.
5. **Design identity:** `Design.Id` isn't unique (edited copies keep it). `DesignWorker` keys its cache on the whole
   design JSON as a string: fine for four entries, not as a battle registry's key or a save's reference.
6. **`DesignWorker` is the designer's:** one pending request, the newest wins (it drops requests on purpose). A battle
   must build all its classes and drop none, on several threads. Each `DesignResult` keeps the CPU mip chains after
   the upload (a few MB a design) and a `HitboxMesh` only the hitbox view needs.
7. **`ShipSpriteRenderer` draws one ship:** one design's textures, and it clears the target in a pass of its own.
8. **The mount ↔ component link is implicit:** it holds because the sprite and the hitbox side filter the components
   the same way.

## The shape

```
Design (json) ──ShipDesign.Build──► Ship (Hitboxes, Report)     shipgen, unchanged: the interchange format and the tools
                                       │ compiled once per design
                                       ▼
ShipClass        read-only, shared, int-indexed                  no GPU, deterministic
  components[]   kind as an enum, geometry, armour, links as indices
  cells[], rooms[], bulkheads[], decks[]; adjacency as CSR int arrays (each edge -> its bulkhead or deck)
  mounts[]       -> component index; the one table the sprite, the hit test and the state all use
  HullField and the compiled armour solids (ShellTest's, promoted)
  initial loads (ammunition, fuel, crew), hydrostatics
  string id <-> index maps (tools, saves, debugging)
                                       │ spawned per ship
                                       ▼
ShipState        one per ship: arrays parallel to the class's
  pose           position (Earth-centred, double), forward (a tangent unit vector), speed;
                 sinkage, trim, heel (derived from the water aboard)
  per mount      bearing, ammunition
  per component  state (ok / damaged / disabled / destroyed), integrity, flags (jammed, burning)
  per cell       water m³, fire, crew alive, hull breach area
  per bulkhead   integrity, leak area
  per room       contents left (fuel, propellant)
  sparse         armour holes; an event log for the damage report's symptom -> cause readout
                 (damage-model-research.md §12: the best model fails without one)

ShipClassArt     per (design, look): sprite layout, baked images -> GPU textures, refcounted
```

It mirrors the views' layering (CLAUDE.md "Views"): a CPU step that runs headless and deterministic (the sim and its
damage), a renderer that reads `ShipState` and `ShipClassArt` and knows nothing of the sim, and an `IScene` on top.

Rules:

- `Hitboxes` stays the export and interchange; nothing in the battle reads it after compiling. `ShipClass` is never
  written after it's made.
- Per-ship arrays are allocated once at spawn; ticks and hit queries allocate nothing (the game is soft real time).
- Indices are stable for one design and one shipgen version (the build is deterministic: the goldens), so the class's
  content hash goes with any stored index.

## The world: one sphere, local frames inside it (proposed 2026-10-11)

The question: at combat ranges, assume the sea is flat, or always work on the sphere? Proposed: **the sphere is the
one truth, everywhere**; flatness exists only inside frames small enough that it's exact to the millimetre (a ship's
own frame), never as a mode the sim switches into at "combat ranges".

Why not flat at combat ranges:

- **The error is not small where it matters.** The sea drops below the tangent plane by d²/2R: 8 m at 10 km, 31 m at
  20 km, 71 m at 30 km. At 20 km a shell falling at 15° lands about 115 m long against a flat target; at 30 km
  (falling at ~25°) about 150 m. A battleship is 30-40 m wide, so that's several beams, and about the size of a
  salvo's spread. The real fire-control computers corrected for it
  (`research/simulation/fire-control-research/computing.md`: 84 ft at 19,800 yd, ~100 yd of range); with a flat sim
  that correction would be a number with nothing to correct.
- **Curvature is a mechanic, not a detail.** The horizon decides who sees whom: from 10 m it's 11 km, from a 30 m
  foretop 20 km, and a 30 m mast is seen from another at about 39 km. Hull-down, masts over the horizon, smoke seen
  before ships: all of that falls out of the sphere and would have to be faked on a flat map.
- **Two models means a seam.** Where does "the combat area" end? Two groups 40 km apart, a shell in flight when
  the frame moves, a battle that drifts across a degree of longitude: each is a bug a single model doesn't have.
- **It costs nearly nothing.** Gravity toward the centre instead of -z is one normalise per shell per tick, and a few
  thousand shells in doubles is nothing.

How:

- **World positions are Earth-centred Cartesian, in doubles** (Earth-fixed, rotating with it). A double at 6.4e6 m
  resolves about a nanometre, so there's no precision cliff anywhere on the planet and no floating origin. Floats only
  after subtracting a nearby origin (the renderer's rule already: camera-relative).
- **A sphere, not the ellipsoid.** Mean radius 6,371 km; map data's latitudes are used as spherical ones. The ellipsoid
  changes nothing a player can see. Sea level is the sphere (tides: no).
- **Ships move on the sphere:** a position and a tangent forward vector, both rotated about their common axis by
  speed·dt / R each tick. No latitude, longitude or heading angle in the state, so nothing breaks at the poles or the
  date line; heading in degrees and lat/lon are derived for the UI and orders.
- **Local frames for local work.** A ship's frame (the one the sprites and hitboxes use: +x bow, +y starboard, up =
  away from the centre) is built from its pose each tick. Inside 250 m the sphere departs from the frame's plane by
  5 mm, so hit queries, flooding and everything in `ShipClass` stay flat and exact enough. A battle's broadphase is a
  3D hash on the Earth-centred position (no poles to special-case).
- **Shells fly in the Earth-centred frame**, gravity toward the centre, the air's density by height over the sphere,
  plus the Coriolis term (-2Ω × v; ~15 m of deflection at 15 kyd per the research) since the frame rotates. A hit test
  takes the shell's segment for the tick into the target's ship frame: that's step 3's "world ray to ship-local".
- **Line of sight** is a ray against the sphere (refraction later: the usual trick is a larger radius, ~7/6 R).

Determinism: the sim is fixed-tick doubles; trigonometry only where a frame is built, so the same inputs give the same
battle on one build and platform (cross-platform lockstep isn't a goal yet).

## Damage on the sprites

The rule stays "one physical model": what damage looks like comes from the hitboxes and the state, never from geometry
of its own.

- **Settling, trim and heel through the height map.** It already holds each pixel's height over the waterline. The
  shader compares it with the ship's sinkage at (x, y) (sinkage, trim, heel) and fades what goes under into the water.
  A stern-first sinking draws itself from the physics.
- **Turrets** are sprites of their own: a destroyed one gets a wreck tint or a wreck variant per turret type; a jammed
  one keeps its bearing.
- **What's baked into the hull texture** (funnels, superstructure, AA, directors, masts): an overlay drawn from the
  component's hitbox polygon (scorch, wreckage, a funnel gone).
- **Fire and smoke** emit from the components' and cells' positions and tops.

## Steps

Each step ends verified and committed, with its notes added here.

1. **`ShipClass`, the compiler.** Compile a `Ship`'s hitboxes and links into int-indexed, read-only arrays (component
   kinds as an enum, CSR adjacency, the mount table, the armour solids). Proof: ShellTest ported onto it gives the
   same results; `shell-test` stays the regression check. Decide where it lives first (open question 2).
2. **`ShipState` and spawning.** The condition arrays, filled from the class's initial loads, and the pose on the
   sphere ("The world": the double vector types, moving on the sphere, the ship frame). Test: 50 ships of one class
   share one `ShipClass` and none writes to it; a ship steaming a full circle of the globe, across a pole, comes back
   where it started.
3. **The hit query.** A shell's segment, Earth-centred, to ship-local (pose, sinkage, trim, heel), broadphase on
   `BroadphaseR`, then turrets at the ship's bearings, the hull, armour and cells. Returns indices, not strings.
   Headless, allocation-free, tested.
4. **The class registry.** Builds many designs in parallel keyed by content hash, none dropped; refcounted; frees the
   CPU images after the GPU upload; the `HitboxMesh` only on request. `DesignSession` stays the designer's one-ship
   view and may sit on top of it.
5. **The batch sprite renderer.** Per-class GPU textures (texture arrays by size class, per
   `research/naval/realtime-tech-stack-plan.md`), many ships drawn into an existing pass from their transforms,
   bearings and damage values, sinkage from the height map. Then a test scene: a few ships, click to hit one and watch
   its state change.
6. **Damage rules.** Flooding first (`sinking.py`'s approach on `ShipState`), and disabling components. Penetration and
   the systems' dependency graph (`research/simulation/damage-model-research.md` §8-9) after.

## Open questions (the user's)

1. **Names:** `ShipClass` / `ShipState` (or `Vessel`), `ShipClassArt`?
2. **Where the battle code lives:** a new `Fleetwright.Battle` project with no GPU, depending only on Shipgen's model
   (recommended), or inside Shipgen or the game exe?
3. **Saves:** condition tables by index (compact; breaks when shipgen changes a class's layout) or by string id
   (survives a rebuild)? Proposed: indices plus the class hash at runtime, string ids in save files.
4. **Buoyancy realism** (open since 2026-10-07, DECISIONS "Buoyancy"): step 6 needs it, steps 1-5 don't.
5. **The world frame** ("The world"): the sphere everywhere, flat only inside a ship's frame (proposed), or a flat
   battle area?

## Background

- `research/simulation/damage-model-research.md`: the synthesis; §12 the prior art and patterns that fit, §13 what the
  generator could emit. `damage-research/06-service-networks.md`: power, hydraulics and data as routes through
  compartments.
- `research/naval/realtime-tech-stack-plan.md`: the sim (fixed tick, deterministic, SoA stores, own job system) and
  the sprite renderer (instanced, texture arrays).
- `docs/shipgen/TODO.md` "Damage model and hitboxes": what the export still lacks (links and flags, pass-throughs,
  double-bottom contents, carrier parts).
