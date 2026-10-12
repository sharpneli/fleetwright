# Plan: the battle module's data, from designs to ships at sea

2026-10-11. Status: the shape is agreed with the user (2026-10-11), and the open questions were settled on 2026-10-12
("Decisions" at the end). "The code, checked again" holds what step 1 has to deal with.
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

### The code, checked again (2026-10-12)

This check read the code and surveyed the hitboxes of every golden build (325 builds: 71 designs plus the fuzz
cases). It found these issues for step 1:

- **Sizes are larger than above.** The largest build has 333 components, 984 cells, 212 rooms, 157 bulkheads, 20
  decks and 6,148 neighbour entries (each edge listed from both cells). That's still dense arrays per ship: a few
  thousand entries, tens of KB, ~100 MB for 2,000 of the largest. Typical warships are a quarter of that.
- **Component ids aren't always unique.** The seaplane carrier and four fuzz cases have two components named
  `Hangar`. Cells, rooms, bulkheads and decks are unique everywhere. Fix it in shipgen first (a golden change, its
  own commit). The compiler rejects duplicate ids instead of picking one.
- **`Component.Mount` means two things.** On a barbette it's the gun mount's id, a link. On a secondary gun it's
  `"casemate"`, a flag (`Hitbox.cs:72`). The compiler can tell them apart by kind, but the export should say what it
  means: a `Casemate` bool on the gun, and `Mount` only as a link. That's a golden change too, ideally in the same
  shipgen pass.
- **`Tier.Floor` of the bottom tier is `"Keel"`,** not a deck id. It's a sentinel the compiler maps to "no deck"
  (-1).
- **AA mounts aren't in `TurretTypes`** (`single20`, `quad40` are their own catalogue) and have no `Local` and no
  rotation. So the mount table (`TopView`'s filter: components with `Local`) is main, secondary and torpedo mounts.
  Some torpedo mounts don't rotate, and submerged tubes have arcs but no `Local`. AA bearings wait for aircraft.
- **Every other string link resolves:** cell ↔ room, cell neighbours (by bulkhead, deck or "open"), cell
  `Through` → shafts and alleys, `Also` → rooms, the component links (magazine, barbette, funnel, smoke, boiler
  rooms, propeller, engine room, alley, shaft, steering, room) and the room links (cells, mounts, tubes, shafts,
  rudders). These are what the compiler turns into indices, so step 1 can be strict: any link that doesn't resolve
  is an error.
- **Kinds:** 19 component kinds (`main`, `secondary`, `torpedo` and `aa` are mounts by battery; `barbette`,
  `superstructure`, `conning_tower`, `funnel`, `uptake`, `casing`, `sponson`, `mast`, `shaft`, `shaft_alley`,
  `propeller`, `rudder`, `torpedo_tube`, `flight_deck`, `hangar_bay`), 8 roles on superstructure and sponsons
  (`bridge`, `director`, `aft_control`, `deckhouse`, `casemate`, `island`, `hangar`, `elevator`), 13 room kinds, 6
  bulkhead kinds, 4 deck kinds. Directors are superstructure blocks with role `director`, a `Battery` and a
  `RangefinderM`. All are enums in `ShipClass`, read from their strings once. An unknown string is a compile error,
  so a new kind in shipgen is noticed.
- **`Component`'s ~50 nullable fields fit common columns plus side tables.** Every component gets kind, role,
  bounds, base and top, armour and crew. Mounts, funnels, the shaft line (shaft, alley, propeller, rudder),
  barbettes and directors each get a table of their own, indexed by component. No per-kind class hierarchy.
- **ShellTest isn't a fixed reference yet:**
  - It steps along the ray at 5 cm and bisects at crossings. It allocates per trace (a `bool[]`, strings).
  - It splits shells over `Environment.ProcessorCount` workers with a `Random` per worker, so its output depends on
    the machine's core count.
  - Its `DeckTop` duplicates `HullField.DeckTop`.

  So step 1's proof separates sampling from tracing. The shots (target, bearing, fall) are drawn up front from the
  seed, one stream per shot. Then the old tracer on `Hitboxes` and the port on `ShipClass` must agree shot by shot.
  Step 3 replaces stepping with exact intersections and is checked against step 1 shot by shot: every disagreement
  explained (near an edge), none in bulk.
- **Where the ported shell-test lives:** `ShellTest` is in `Fleetwright.Shipgen`, and Shipgen can't see `ShipClass`
  in the Sim. The port goes to `Fleetwright.Sim`, and `Fleetwright.Shipgen.Cli` references `Fleetwright.Sim` (a tool
  may depend on anything; the libraries may not). The old `ShellTest` is deleted once the port matches it.
- **There is no content hash and no shipgen version yet.** `DesignWorker` keys on the design JSON string, and
  `Design.Id` isn't unique. Two hashes are needed, for different jobs:
  - **The design hash:** the canonical design JSON (SHA-256 is in the BCL). It's the registry's key ("built this
    already?") and a mission's or save's reference to a design.
  - **The layout hash:** the compiled `ShipClass`'s structure (ids, kinds, links, in index order). Saves hold string
    ids (decision 3), so they don't need it to load. It tells a load, a mission file or a test that a rebuilt class
    differs from the one saved: shipgen changed it. State is still mapped by id, and parts that no longer exist are
    dropped and logged. Within a run, it's the key indices are valid under.

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
damage), a renderer that reads the snapshot's copy of what's drawn (pose, bearings, damage looks; never `ShipState`
itself, per `docs/world/architecture.md` "The sim thread and the boundary") with `ShipClassArt`, and knows nothing
of the sim, and an `IScene` on top.

`ShipState` is one ship's view of the data. Its storage may later move into world-wide stores per kind, or an
entity component system (the tech-stack plan leaves "hand-written SoA or Arch" for later). So callers reach it
through index-based accessors, never its arrays' layout. Moving the storage is then a refactor of `ShipState`
alone.

## Inside `Fleetwright.Sim` (proposed 2026-10-12)

Every part of the sim is in layers with one direction of dependency, as the projects are. Namespaces under
`Fleetwright.Sim`, lowest first; each may use the ones above it in this list, never the ones below:

```
Fleetwright.Sim.Core      double vectors, the sphere (great circles, frames), world time, SimSettings, seeded streams
Fleetwright.Sim.Ships     ShipClass, its compiler from Shipgen's Ship, the class registry, content hashes
Fleetwright.Sim.Combat    ShipState, the hit query, damage, flooding and buoyancy, shells: the tactical tier
Fleetwright.Sim (World)   the world, the clock and scheduler, tiers, encounters, commands in, the snapshot out
```

- `Ships` is pure data and compilation: no time, no world, nothing that changes. `Combat` gets time and a `dt`
  from the scheduler and never owns either. Only `World` knows about threads, commands and the snapshot.
- `Fleetwright.Sim` depends on `Fleetwright.Shipgen` only. `Fleetwright.Shipgen` never depends on it.
- Tests: `tests/Fleetwright.Sim.Tests` (xUnit, headless, no GPU).

Rules:

- **The battle lives in `Fleetwright.Sim`, in layers of its own** (see "Inside `Fleetwright.Sim`"). It has no GPU, UI
  or engine dependency. `ShipClassArt` and the sprite renderer are on the exe's side and only read the sim's snapshot.
- The battle is the world's tactical tier (`docs/world/architecture.md`): every step keeps to that doc's "What the
  combat module must already respect" (world positions and time, encounters as sets of entities, `ShipState` made
  and dropped per entity, damage from a hit not a flight, sighting into a navy's picture, commands in, `SimSettings`).
- `Hitboxes` stays the export and interchange; nothing in the battle reads it after compiling. `ShipClass` is never
  written after it's made.
- Per-ship arrays are allocated once at spawn; ticks and hit queries allocate nothing (the game is soft real time).
- Indices are stable for one design and one shipgen version (the build is deterministic: the goldens). At runtime
  everything is by index. Save files hold string ids and are mapped back to indices on load (decision 3).

## The world: one sphere, local frames inside it (agreed 2026-10-11)

The question: at combat ranges, assume the sea is flat, or always work on the sphere? Agreed: **the sphere is the one
truth for the sim, everywhere**; flatness exists only inside frames small enough that it's exact to what's being done
(a ship's own frame, an effect's patch), never as a mode the sim switches into at "combat ranges". It's a historical
naval game: everything happens somewhere on the Earth's surface. The Earth model itself (coastlines, bathymetry) comes
later, from the user; until then the sphere is all sea.

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
- **It costs nearly nothing.** The shells' trajectories are tables either way (below), and a sphere's are as cheap
  as a plane's.

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
- **Line of sight** is a ray against the sphere (refraction later: the usual trick is a larger radius, ~7/6 R).
- **Effects are local and flat.** The wave sim, wakes, splashes, smoke and particles run in a tangent plane at their
  emitter or the camera: the wave sim only shows at close zoom, and a 1 km patch departs from the sphere by 2 cm.
  They're looks, read from the sim, never fed back into it.
- **Mission areas are rules, not coordinate systems.** An early mission limits play to a region of the sphere (a cap
  or a polygon on it); the ships inside are still on the sphere, at a real place.

Determinism: the sim is fixed-tick doubles; trigonometry only where a frame is built, so the same inputs give the same
battle on one build and platform (cross-platform lockstep isn't a goal yet).

### Shells: an analytic trajectory, tracked, not integrated

The sim may run on large ticks (seconds), so a shell can't be stepped with a numerical integrator per tick: its
result would change with the tick length, and a long tick would chord straight through a ship. Instead each shell's
whole flight is known the moment it's fired, and the sim looks positions up on it.

- **The flight lies in one plane.** On a sphere with gravity toward the centre and drag along the velocity, the
  trajectory stays in the plane through the Earth's centre, the muzzle and the initial velocity (a great circle's
  plane), and depends only on the elevation (against the local horizontal) and the muzzle velocity. So per shell
  type, a **table: elevation × time -> ground arc, height over the sea, and velocity**, integrated once (point mass,
  the standard atmosphere by height, Cd(Mach): `research/simulation/fire_control_ref.py` `_fly` is the reference)
  and interpolated (Hermite, positions with velocities). Curvature is in the table, not a correction to it. The
  table's resolution is set by its error against direct integration: well under a metre at the fall.
- **A shell is a few numbers:** fire time, muzzle position, the plane (its azimuth), the elevation and muzzle velocity
  it actually left with (the ship's own motion, roll and pitch folded in; dispersion is a per-shell perturbation of
  these). Its position at any time is one lookup, so the renderer draws it smoothly at any frame rate for free.
- **Coriolis to first order, exactly from the table.** The frame rotates, so the shell also feels -2Ω × v. To first
  order the offset is δ(t) = -2Ω × ∫₀ᵗ (x(τ) - x₀) dτ: the table also stores that integral of the in-plane
  displacement, and the deflection is one cross product (~15 m at 15 kyd per the research). Wind, if ever, the same
  way.
- **Only the end of the flight can hit a ship.** Nothing at sea is taller than ~60 m, so a shell can hit one only
  while it's below that: the last ~150 m of track, a fraction of a second. At firing the sim solves for the impact
  time and that **danger window**, and schedules an event; until then the shell costs nothing per tick. At the window
  the hit query sweeps the track against the ships near it, each in its own frame at those instants (the ships move
  a few metres in the window). Results don't depend on the tick. Land (once the Earth model exists) and aircraft
  (later) are tested along the whole track the same way.
- **Splashes and near misses** come from the same solve: the impact point and time are known at firing.

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

0. **Before step 1, in shipgen** (its own commits, goldens updated on purpose): unique component ids (the duplicate
   `Hangar`), and `Component.Mount` split into a link and a `Casemate` flag ("The code, checked again"). Small, but
   the compiler is strict, so these go first.
1. **`ShipClass`, the compiler.** It creates the `Fleetwright.Sim` project (`Core` and `Ships` only, with
   `tests/Fleetwright.Sim.Tests`). It compiles a `Ship`'s hitboxes and links into int-indexed, read-only arrays:
   - component kinds and roles as enums, common columns plus side tables;
   - CSR adjacency (each edge → its bulkhead or deck);
   - the mount table, the armour solids, the id ↔ index maps;
   - the design hash and the layout hash.

   Strict: an unknown kind or a link that doesn't resolve is an error. Proofs:
   - The compiler runs over every golden build without an error.
   - ShellTest's shots are drawn up front (one stream per shot), and the old tracer and the port on `ShipClass`
     agree shot by shot on every design with a citadel.
   - `shell-test` in the CLI runs the port. The old `ShellTest` goes once they match.

   Nothing in it allocates per query, and nothing writes to a class once it's made.
2. **`ShipState` and spawning.** The condition arrays, filled from the class's initial loads, and the pose on the
   sphere ("The world": the double vector types, moving on the sphere, the ship frame). Test: 50 ships of one class
   share one `ShipClass` and none writes to it; a ship steaming a full circle of the globe, across a pole, comes back
   where it started.
3. **The hit query.** A segment, Earth-centred, to ship-local (pose, sinkage, trim, heel), broadphase on
   `BroadphaseR`, then turrets at the ship's bearings, the hull, armour and cells. Returns indices, not strings.
   Headless, allocation-free, tested.
4. **The class registry.** Builds many designs in parallel keyed by content hash, none dropped; refcounted; frees the
   CPU images after the GPU upload; the `HitboxMesh` only on request. `DesignSession` stays the designer's one-ship
   view and may sit on top of it.
5. **The batch sprite renderer.** Per-class GPU textures (texture arrays by size class, per
   `research/naval/realtime-tech-stack-plan.md`), many ships drawn into an existing pass from their transforms,
   bearings and damage values, sinkage from the height map. Then a test scene: a few ships, click to hit one and watch
   its state change.
6. **Damage rules.** Flooding first, and disabling components. Penetration and the systems' dependency graph
   (`research/simulation/damage-model-research.md` §8-9) after. Buoyancy starts simple (decision 4): first they
   float, then they float in a more detailed way.
   - **First:** water by cell (volume × permeability), inflow through a breach's area. The ship settles, trims and
     heels linearly from the exported full-load `Hydrostatics` (sinkage = added weight / TPC, trim from the moment
     over MCT, heel from GM_T). It sinks when the water aboard exceeds the reserve buoyancy (the intact volume above
     the waterline) or capsizes when heel passes a limit from `SimSettings`. No leaking bulkheads, free surface,
     counter-flooding or motion past the deck edge yet.
   - **Then, area by area:** leaks through bulkheads and decks, free surface, `sinking.py`'s non-linear settling,
     pumping and counter-flooding, a plunge or capsize that plays out. Each is its own small step, checked against
     the previous one.
   - This will change once the damage model is in, because flooding and damage interact heavily: fire and flooding
     share cells, power drives pumps, a jammed rudder steers into it. So the first version's job is to be easy to
     change, not complete.
7. **Shells in flight** ("Shells"). The trajectory tables per shell type and their builder (checked against direct
   integration), the shell record, the impact and danger-window solve, the event schedule, and the window's sweep
   through step 3's query. Test: hits and splashes are the same at a 0.1 s and a 5 s tick; a shell fired along a
   meridian and one along the equator land where direct integration on the rotating sphere says.

## Order against the missions plan

`docs/missions/plan.md` orders the game's stages: one ship on the map, then two, then shooting. Steps 0-1 don't
conflict with that. They're headless, they set up `Fleetwright.Sim`, and stage 1 needs a ship of a class anyway. So
the next session does steps 0-1. After that, the missions stages decide when steps 2-7 come in. Step 2's pose on the
sphere is part of stage 1.

## Decisions (the user's, 2026-10-12)

1. **Names:** `ShipState` for one ship at sea. It's easy to rename later, and its storage may become an entity
   component system (hence the accessors in "The shape"). `ShipClass` and `ShipClassArt` stay as named here.
2. **Where it lives:** `Fleetwright.Sim` (`docs/world/architecture.md`), in sub-layers of its own, and every part
   inside has a coherent architecture of its own too ("Inside `Fleetwright.Sim`"). Everything ships in one
   executable as independent components. The designer runs with or without the sim. The sim has no GPU dependency,
   and its renderers only read its state.
3. **Saves:** indices at runtime for performance, string ids in save files. Saving and loading may take a while;
   play may not.
4. **Buoyancy:** start simple and make it more detailed area by area, the way shipgen was built. First they float,
   then they float in a more detailed way. Expect to change it a lot once the damage model is in.

## Open questions

1. **The layering inside `Fleetwright.Sim`** ("Inside `Fleetwright.Sim`") is proposed, not agreed: check it at the
   start of step 1.
2. **The shipgen fixes of step 0** (unique ids, the `Casemate` flag) change the export. Confirm them at the start of
   step 0.

## Background

- `research/simulation/damage-model-research.md`: the synthesis; §12 the prior art and patterns that fit, §13 what the
  generator could emit. `damage-research/06-service-networks.md`: power, hydraulics and data as routes through
  compartments.
- `research/naval/realtime-tech-stack-plan.md`: the sim (fixed tick, deterministic, SoA stores, own job system) and
  the sprite renderer (instanced, texture arrays).
- `docs/shipgen/TODO.md` "Damage model and hitboxes": what the export still lacks (links and flags, pass-throughs,
  double-bottom contents, carrier parts).
