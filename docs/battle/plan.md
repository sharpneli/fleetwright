# Plan: the battle module

2026-10-11, reframed 2026-10-12. Status: what the battle module is, that it's built from an empty world up, what's
out of it and the performance targets are the user's (2026-10-12, "Decisions"). The ships' data plan is agreed (2026-10-11), with its open questions settled on
2026-10-12. "Building it from the ground up" is the step order for all battle module work; its steps from 3.1 on are
proposed and are agreed one by one.

Each step is its own session and starts with an investigation of its own: read the code it touches, check this plan
against it, agree the step's details with the user, then build. Update this file as steps finish (what was done, what
changed from the plan).

## What the battle module is

**The battle module is a deliverable, not a part of the code.** When it's done, the game's whole ship simulation is
in place, as the final game will have it. Ships move, see, fight, take damage and sink in the world, on the one map,
with the whole zoom from the globe down to a single ship's sprite. The final game will add things around that
simulation, not replace it.

The one part of the module that the final game won't keep is **the mission UI**. It places the forces, sets their
orders and the objectives, and judges the result. In the final game, the political system (who is at war, and why)
and manufacturing (what each navy has, and where) do that job, and neither exists yet. So the mission layer
(`docs/missions/plan.md`) stands in for them, and it's also the first shippable game.

There is no `Battle` project or namespace. The module is spread over the parts it's made of: `Fleetwright.Sim` (the
world, the ships' data, combat; "Inside `Fleetwright.Sim`"), the exe's map, renderers and scene stack, and the
missions on top (`docs/world/architecture.md` "The pieces and where they live").

### Done means

- **A world:** the planet as a sphere at real scale, all sea at first, then the user's Earth model with land, ports
  and routing. One clock, from paused to ~30,000x, dropping by the player's rules when something needs them.
- **Ships that move:** many ships of many designs, each compiled once to a `ShipClass`. They steam with real turning
  circles and acceleration, in groups and formations, on routes. Ships far from anything are on rails, and ships
  near something are in the tactical tier.
- **Seeing and knowing:** contacts and encounters, sighting (horizon, weather, night), and each navy's picture. The
  player sees what their navy knows.
- **Fighting:** fire control, guns and shells, torpedoes, the hit query, penetration, damage, flooding and sinking,
  and damage drawn on the sprites.
- **Command:** captains (the AI) following standing orders on both sides, and the player commanding through the same
  commands on the map at every zoom.
- **Missions:** a mission file, the mission UI (place forces, set orders and objectives, the designer for
  player-designed forces), briefing and debrief, and a generator of missions.
- **Throughout:** deterministic and headless. Commands go in and a snapshot comes out, with replays and saves of a
  mission. Every tuning number is in `SimSettings`. Within the targets in "How many ships".

### Not in it

These are the final game's, around the simulation: politics and diplomacy, manufacturing, yards and economy,
research (a mission's `TechSet` is a preset), NPC admiralties, and the world's merchant traffic (a mission's merchants
are just ships in it).

**Out by decision (the user, 2026-10-12):** Resolved fights, aircraft and submarines. The module keeps to naval
battles between surface ships. Later technology then gives unhistorical battles (no carriers, no U-boats), which is
accepted.

### The era it's built for

The first full game is a campaign of **about 1890-1925**, after the political system is in. Aviation is for version
2.0. So the battle module is built for the pre-dreadnought, dreadnought and First World War navies. Later designs
still work (the designer makes them, and they fight), but nothing in the module is shaped around them. That era
asks for things a WWII-shaped plan would leave out:

- **Sighting without radar.** Visibility, haze, night, searchlights and star shell. **Smoke is the big one:** funnel
  smoke, gun smoke and smoke screens decided what could be seen at Jutland. So smoke is sim state (puffs and screens
  with a position, an age and a drift with the wind), and sighting reads it. The smoke that's drawn is a look that
  reads that state, never the other way round. That needs wind in the world's weather from step 3.2.
- **Misidentification.** A contact's class is what the observer could tell, and it can be wrong (a battlecruiser
  taken for a battleship). The picture already holds a classification "as far as known"; this is that, with errors.
- **Signals, not radio orders.** Flags, searchlights and early wireless: an order takes time to pass down a line and
  can be missed or misread (Jutland's signal failures). The player's command goes in at once; the group executes it
  when the signal reaches it. The map mockup already shows orders in transit (◷). It's part of step 5.1.
- **Fire control of its time.** Rangefinders by base length, plotting and clocks (Dreyer table, Argo clock), local
  control before director firing. `research/simulation/fire-control-research/` covers the era.
- **Torpedo boats in numbers.** The 1890s navies built them by the hundred (the Jeune École), so a fight can have
  dozens of small, fast ships. They're cheap per ship, but there are many, and a torpedo attack by them must play well.
- **Collisions and ramming.** Ships in close formation at night collided (Jutland had several), and ramming was still
  in the 1890s' tactics. A hull-against-hull test (both `HullField`s, in their frames) is a small query beside the hit
  query (step 4.6).
- **Mines.** Fields laid by minelayers, and a ship crossing one. Mines sank the dreadnought *Audacious* in 1914 and
  the three pre-dreadnoughts lost at the Dardanelles on 18 March 1915. Open question 1 asks whether they're in.
- **Coal.** Endurance, coaling and the smoke that comes with it. Oil firing (from ~1912) makes less smoke and needs
  fewer stokers. This sits in rails fuel and in the smoke model, and isn't a step of its own.

## How many ships (checked 2026-10-12)

Two different numbers matter. One is how many ships are in one fight at Full, which costs the most per ship. The
other is how many are in the world, which costs the least per ship but there are many of them.

**In one fight:** Jutland is the largest surface action there was, and the module's design case. It had 250 ships
(151 British, 99 German) of roughly 50-60 classes. Later fights had more aircraft, but not more ships in one action.
So "a few hundred in one fight" (`docs/world/architecture.md`) holds. A mission should be able to stage a Jutland.

**In the world:** the end goal is the whole world of the First World War with every warship in it (the user,
2026-10-12). The architecture's figure is ~2,000 warships. That's about right for 1914's navies together, and torpedo
boats may push it toward 3,000 (to verify, `research/REQUESTS.md`). Merchants are ~9,000 for Britain alone in 1914,
and the world had a few times that. These are rails and light entities, so they cost events and contact scans, not
models. Not all of them fight at once. A world war has many fights, and in the campaign most go through Resolved,
which comes after the module. The world's numbers aren't the battle module's load, but its scale tests keep them in
view, so nothing built now has to be rebuilt for them.

**What one class costs, measured (Release, this machine):**

| | Bismarck | Notes |
|---|---|---|
| shipgen build (design -> `Ship`) | 0.12 s, 21 MB allocated | Yamato the same (`shipgen bench`) |
| sprite drawing (CPU) | ~0.6 s | `shipgen bake`: "build+draw 0.63 s" |
| GPU bake | 0.03 s | |
| hull sprite at 10 px/m | 2432 x 384 px | RGBA 3.7 MB, plus the height map; ~6 MB with mips, uncompressed |
| hitboxes | 93 components, 220 cells | the largest golden build: 333 and 984 |

- **Loading a Jutland:** ~55 classes at ~0.75 s each is ~40 s of CPU, or ~6 s on 8 threads. Most classes are much
  smaller than Bismarck (a destroyer's sprite is a tenth of the pixels), so the art is ~100-150 MB of textures,
  uncompressed. That's fine for the module without BC compression.
- **The drawing is the cost, not the physics.** A `ShipClass` needs only the build (0.12 s); the art can follow,
  coarse mips first. The compiled ships on disk ("Compiled ships on disk", below) make a second load instant, and the
  whole world's hundreds of classes will need that.
- **A fight at Full:** 250 ships at a 20 Hz tick, run at 10x, is 200 ticks per real second: 5 ms a tick, or 20 µs a
  ship if every model ran every tick. They don't need to. **Models in the tactical tier run at their own rates,** on
  whole multiples of the tick, set in `SimSettings`:
  - pose and turning every tick (~100 ns a ship);
  - sighting and fire-control solutions at ~1 Hz;
  - flooding at ~1 Hz, and only on ships with water in them;
  - shells only when fired and in their danger window ("Shells");
  - damage only on a hit.
- **Shells:** Jutland's heavy guns fired ~8,000 rounds (British 4,598, German 3,597). With the secondaries and the
  destroyers, that's a peak of perhaps 1,000-2,000 in flight. Each is a few numbers and two events.
- **The snapshot:** 250 ships at ~200 B (pose, bearings, damage looks) plus 2,000 shells at ~48 B is ~150 KB.
- **Drawing:** at the tactical band, 250 ships are a few pixels each (instanced, trivial). At the ship band, a few
  ships fill the screen at full resolution.

**Targets (agreed 2026-10-12, measured in step 2.6 and again at the end):**
- **The minimum: a Jutland at Full, butter smooth on a modern machine.** That means:
  - 60 fps at any zoom, judged by the worst frames, not the average (no frame over budget in a Tracy capture of a
    few minutes);
  - no GC pauses in play (gen 0 collections near zero while it runs);
  - the UI never waits for a tick (the sim has its own thread).
- The Jutland running at 10x or better on an 8-core desktop.
- 2,000 ships on rails at 30,000x.
- A Jutland's first load under 10 s, and under 1 s from the cache.

**If the Jutland misses its target:** the user's answer is brutal ECS tricks (data-oriented storage, hot and cold
data split, batched per-model passes over every ship). The plan is ready for it, because callers reach `ShipState`
through index-based accessors, never its arrays' layout ("The shape"). Moving to an ECS is then a refactor of
`ShipState` and the models' loops, not of the world.

### Compiled ships on disk (before release, not now)

Before release, compiled ships are kept on disk in an internal cache, so a mission or a saved world loads without
building and drawing every class again. It isn't built yet: 2.6 measures whether the module needs it sooner. What it
will be:

- **What's stored:** per design, the `ShipClass` (its arrays, which are plain int and float columns, written as they
  are); per design and look, the `ShipClassArt` (the drawn hull, height map and turret images with their mips, and
  the layout).
- **Where:** the game's internal cache under the user's local app data (`%LOCALAPPDATA%\Fleetwright\Cache`), not
  Documents. It's not the player's files, and deleting it costs only a slower load. A release can also ship the
  shipped designs precompiled.
- **The key:** the design hash, the look (for art), the shipgen version and the cache's own format version. Any
  change misses, and the class is rebuilt. Each entry also stores the layout hash.
- **The design JSON stays the truth.** Saves and missions hold designs, never cache entries. The cache is disposable,
  and a missing, stale or unreadable entry is rebuilt, never an error.
- **Loading must equal compiling:** a test compiles each golden design, writes it, reads it back, and compares the
  classes array by array (and the layout hash).
- **What it needs from now:** the class registry gets classes through one seam ("the class for this design hash"),
  so the cache slots in behind it (step 2.3). `ShipClass` stays plain arrays with nothing that can't be written
  (step 2.1). And shipgen gets a version: a constant bumped whenever the goldens change on purpose.

## Building it from the ground up

The order follows the user (2026-10-12): first a world, an empty sphere of water; then ships moving on it; then
everything else on top, one step at a time. Each step ends with something to run and look at, and with a test. The
missions plan's baby steps (one ship, two ships, shooting) are this order's 2.3 and 4.2.

The Earth model comes in when the user brings it, at any point after 1.2. Until then the world is all sea, and nothing
may assume it always will be: routes are legs between waypoints, and line of sight and the contact scan leave room
for a land test.

### 1. A world of water

**1.1 The sim core, headless.** The `Fleetwright.Sim` project and `tests/Fleetwright.Sim.Tests`:
- `Core`: double vectors, the sphere (great circles, local frames), world time (`long` ms since 1850),
  `SimSettings`, and seeded streams.
- `World`: the clock, the scheduler's event queue (ordered by time, tier, id), commands in, and the snapshot out
  (preallocated, triple-buffered).
- The sim thread, advancing toward the target time it's asked for.

The world holds nothing yet but its clock and the sun's position. Tests: the same commands give the same snapshots;
the thread reaches its target times and never passes them; a step allocates nothing; the subsolar point is right at
known dates.

**1.2 The globe.** In the exe:
- **the world is a tab** on the top bar (the user, 2026-10-12), the first one and the default, so launching the game
  opens the world. The designer, hitbox, ship and profile tabs stay beside it as they are (`-view=designer` opens
  the designer). The world and its sim thread are owned by the app, not the tab, so switching tabs leaves it running.
  The scene stack (the designer pushed over the map and popped) waits until a mission needs it (step 6.2);
- a first `MapScene`: the sphere of water, drawn at every zoom from orbit down to ~10 px/m (the sprites' finest
  level), with a double-precision camera and camera-relative floats;
- a lat/lon grid, and day and night from the snapshot's sun;
- time controls: pause, 1x to 30,000x, asked vs achieved.

The water close up is plain shading; the wave sim is a look for later. Proof: screenshot readbacks at each zoom band,
from orbit to a few metres, with no jitter, and the clock keeping the asked speed.

### 2. Ships that move

**2.1 `ShipClass`, the compiler** (the data plan's work, "The ships' data" below). In shipgen first (its own commits,
goldens updated on purpose): unique component ids (the duplicate `Hangar`), and `Component.Mount` split into a link
and a `Casemate` flag ("The code, checked again"). Small, but the compiler is strict, so these go first.

Then `Fleetwright.Sim.Ships` compiles a `Ship`'s hitboxes and links into int-indexed, read-only arrays:
- component kinds and roles as enums, common columns plus side tables;
- CSR adjacency (each edge -> its bulkhead or deck);
- the mount table, the armour solids, the id <-> index maps;
- the design hash and the layout hash;
- what moving needs: length, the speed and power curve from the report, and a turning circle.

Strict: an unknown kind or a link that doesn't resolve is an error. Proofs:
- The compiler runs over every golden build without an error.
- ShellTest's shots are drawn up front (one stream per shot), and the old tracer and the port on `ShipClass` agree
  shot by shot on every design with a citadel.
- `shell-test` in the CLI runs the port. The old `ShellTest` goes once they match.

Nothing in it allocates per query, and nothing writes to a class once it's made.

**2.2 Ships on the sphere.** `ShipState`'s pose, made and dropped per entity ("Full state is made and dropped per
entity", architecture). Movement in the tactical tier at the fixed tick: acceleration and turning from the class.
Commands: a course, a speed, a route of waypoints. Headless tests:
- a ship steaming a full circle of the globe, across a pole, comes back where it started;
- a turning circle matches the class's;
- 50 ships of one class share one `ShipClass`, and none writes to it.

**2.3 Ships on the map: one, then two.** The snapshot carries poses and bearings, and the map draws ships at every
band: markers zoomed out, hulls to scale with a minimum size at the tactical band, sprites at the ship band. This
needs:
- **the batch sprite renderer:** per-class GPU textures (texture arrays by size class, per
  `research/naval/realtime-tech-stack-plan.md`), many ships drawn into an existing pass from their transforms and
  bearings;
- **the first class registry:** builds many designs in parallel keyed by the design hash, none dropped, refcounted;
  frees the CPU images after the GPU upload, and makes the `HitboxMesh` only on request. `DesignSession` stays the
  designer's one-ship view and may sit on top of it.

Then click a ship and give it a course and speed (missions stage 1). Then a second ship with a trivial captain: keep
a course, follow, run (stage 2).

**2.4 Groups and formations.** Groups with a leader and stations; one order moves the group; keeping station is the
first captain behaviour.

**2.5 Time compression: the rails tier.** Ships with nothing near them go on rails (great-circle legs, events at the
waypoints) and come back to the tactical tier when something needs them. The speed is clamped by the costliest live
tier, and rails ships are drawn exactly from their tracks. Tests:
- 2,000 ships for a game year, headless, in seconds;
- a ship that goes rails -> tactical -> rails ends where an all-tactical run puts it, within the rails tier's
  instantaneous turns;
- the same run twice is bit-identical.

**2.6 Scale: the Jutland test.** 250 ships of ~55 classes, loaded, steaming in formation in the tactical tier, drawn
at every zoom, with some hundreds more on rails. Measure load time, tick cost, frame time and VRAM against the targets
in "How many ships". Then do what the numbers ask for: the art disk cache, coarse mips first, the job system (the
architecture's own step: fixed workers, struct jobs, with the test that results don't depend on the thread count).

### 3. Seeing and knowing

**3.1 Contacts and encounters.**
- The contact scan and the exact crossing times.
- Relations as a war/peace table.
- Encounters opening, merging and closing; their ships held in the tactical tier.
- Speed drops by the player's interest table.

**3.2 Sighting and pictures.**
- Sighting: the horizon from the height of eye and of the target, weather, night.
- Each navy's picture; the player's snapshot built from it.
- The map draws contacts with their uncertainty.
- Captains act on pictures (shadow, evade).

### 4. Fighting

**4.1 The hit query.** A segment, Earth-centred, to ship-local (pose, sinkage, trim, heel), broadphase on
`BroadphaseR`, then turrets at the ship's bearings, the hull, armour and cells. Returns indices, not strings.
Headless, allocation-free, tested.

**4.2 Guns and shells (missions stage 3).**
- The trajectory tables per shell type, and their builder, checked against direct integration.
- The shell record, the impact and danger-window solve, the events, and the window's sweep through 4.1's query.
- Splashes.
- A first fire control: a director with a solution on a contact from the picture, salvoes, corrections from the fall
  of shot.
- Shells drawn from their records.

Tests: hits and splashes are the same at a 0.1 s and a 5 s tick; a shell fired along a meridian and one along the
equator land where direct integration on the rotating sphere says.

**4.3 Damage: flooding and disabling.** Buoyancy starts simple (decision 4): first they float, then they float in a
more detailed way.
- **First:** water by cell (volume x permeability), inflow through a breach's area. The ship settles, trims and heels
  linearly from the exported full-load `Hydrostatics` (sinkage = added weight / TPC, trim from the moment over MCT,
  heel from GM_T). It sinks when the water aboard exceeds the reserve buoyancy (the intact volume above the
  waterline), or capsizes when heel passes a limit from `SimSettings`. No leaking bulkheads, free surface,
  counter-flooding or motion past the deck edge yet.
- Components disabled by hits.
- This will change once the damage model is in, because flooding and damage interact heavily: fire and flooding
  share cells, power drives pumps, a jammed rudder steers into it. So the first version's job is to be easy to change,
  not to be complete.

**4.4 Damage on the sprites** ("Damage on the sprites" below), and a damage report through the watch list.

**4.5 Penetration and the systems.** Penetration, and the systems' dependency graph
(`research/simulation/damage-model-research.md` §8-9). Then, area by area, each its own small step checked against
the previous one: leaks through bulkheads and decks, free surface, `sinking.py`'s non-linear settling, pumping and
counter-flooding, a plunge or capsize that plays out.

**4.6 Torpedoes, collisions, mines.** Tubes, and running torpedoes as tracks with events (like shells). Hits below
the waterline go into the same damage. Torpedo boat and destroyer attacks need them. Then hull against hull:
collisions and ramming, damage from where the stems struck. Mines, if they're in (open question 1): fields as static
areas, a track crossing one as an event, a hit into the same damage.

**4.7 The coarse tier,** for damaged ships after a fight: still flooding, or burning, while the clock is compressed.

### 5. Command

**5.1 Captains.** Standing orders for both sides: go to, keep station, shadow, evade, engage, break off at x% damage.
The player's orders are the same commands.

**5.2 The command UI on the map.** Orders at every band, with previews, the forces rail, edge markers and the
selection strip (`docs/battle/command-ui.md`, `docs/map-ui/README.md`).

### 6. Missions: the stand-in for politics and manufacturing

**6.1 The mission file.** Sides, forces, area, starting pictures, scripted events and reports, objectives and seeds
(`docs/missions/plan.md`). Objectives are checked on events.

**6.2 The mission UI.**
- Place groups on the globe, and set their orders and the objectives.
- The designer, pushed for player-designed forces, under the mission's limits and `TechSet`.
- Mission select, briefing, debrief.

**6.3 Saves and replays.** A mission saved and loaded at a tick. A replay is the mission file plus the command log.

**6.4 The generator.** Missions from templates, each checked headless, AI against AI.

## The ships' data

How a ship's data is laid out, from a design to a ship at sea (agreed 2026-10-11). Many ships share a design, and
several designs are in memory at once. The game draws ships with the sprite model and computes damage with the hitbox
model, and each ship at sea carries a condition table tied to the hitbox model. The plan keeps the shared, per-design
data apart from the per-ship state.

The battle is not an arena: the world is the whole planet at real scale (`research/naval/realtime-tech-stack-plan.md`),
and a battle is ships that happen to be near each other on it. Nothing here may assume a flat map; see "The world".

### Where we are (the review of 2026-10-11)

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
8. **The mount <-> component link is implicit:** it holds because the sprite and the hitbox side filter the components
   the same way.

### The code, checked again (2026-10-12)

This check read the code and surveyed the hitboxes of every golden build (325 builds: 71 designs plus the fuzz
cases). It found these issues for step 2.1:

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
- **Every other string link resolves:** cell <-> room, cell neighbours (by bulkhead, deck or "open"), cell
  `Through` -> shafts and alleys, `Also` -> rooms, the component links (magazine, barbette, funnel, smoke, boiler
  rooms, propeller, engine room, alley, shaft, steering, room) and the room links (cells, mounts, tubes, shafts,
  rudders). These are what the compiler turns into indices, so step 2.1 can be strict: any link that doesn't resolve
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

  So step 2.1's proof separates sampling from tracing. The shots (target, bearing, fall) are drawn up front from the
  seed, one stream per shot. Then the old tracer on `Hitboxes` and the port on `ShipClass` must agree shot by shot.
  Step 4.1 replaces stepping with exact intersections and is checked against 2.1 shot by shot: every disagreement
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

### The shape

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

### Inside `Fleetwright.Sim` (proposed 2026-10-12)

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
- Step 1.1 makes `Core` and the `World` root with nothing in it; `Ships` comes in 2.1 and `Combat` in 2.2. The
  layering is the same; only the order the layers fill in changed.

Rules:

- **The ship simulation lives in `Fleetwright.Sim`, in layers of its own** (above). It has no GPU, UI or engine
  dependency. `ShipClassArt` and the sprite renderer are on the exe's side and only read the sim's snapshot.
- Combat is the world's tactical tier (`docs/world/architecture.md`): every step keeps to that doc's "What the ship
  simulation must already respect" (world positions and time, encounters as sets of entities, `ShipState` made and
  dropped per entity, damage from a hit not a flight, sighting into a navy's picture, commands in, `SimSettings`).
- `Hitboxes` stays the export and interchange; nothing in the sim reads it after compiling. `ShipClass` is never
  written after it's made.
- Per-ship arrays are allocated once at spawn; ticks and hit queries allocate nothing (the game is soft real time).
- Indices are stable for one design and one shipgen version (the build is deterministic: the goldens). At runtime
  everything is by index. Save files hold string ids and are mapped back to indices on load (decision 3).

### The world: one sphere, local frames inside it (agreed 2026-10-11)

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

#### Shells: an analytic trajectory, tracked, not integrated

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

### Damage on the sprites

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

## Decisions

The user's, 2026-10-12, on the module:

1. **The battle module is a deliverable:** the final game's whole ship simulation on the world map, with the full
   zoom, plus a mission UI that places forces and handles objectives in place of the political system and
   manufacturing.
2. **Build from the ground up:** first a world (a sphere of water until the user brings the world map), then ships
   moving on it, then the rest.
3. **Keep the number of ships in mind** ("How many ships"). The end goal is the whole world of the First World War
   with all its warships; not all of them need to fight at once.
4. **Out of the module:** Resolved fights, aircraft and submarines. The focus is naval battles. Aviation is for
   version 2.0, after the political system and a full campaign of about 1890-1925 ("The era it's built for").
5. **Performance:** the targets in "How many ships" are fine. The absolute minimum is a Jutland going butter smooth
   on a modern machine; if we can't hit it, we'll do brutal ECS tricks.
6. **Compiled ships on disk,** in an internal cache, before release ("Compiled ships on disk").
7. **The world is a tab** on the top bar for now, the default one, so the game opens on the world (step 1.2).

The user's, 2026-10-12, on the ships' data:

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

1. **Mines: in the module or after?** My recommendation is in, late in step 4.6. They're surface warfare in this era
   and cheap: static areas, an event when a track crosses one, and the same damage as a torpedo.
2. **Submarines before the campaign.** They're out of the module, but the 1890-1925 campaign has the U-boat war. So
   I assume they come between the module and the campaign, not with aviation in 2.0. Confirm when the campaign is
   planned.
3. **The layering inside `Fleetwright.Sim`** is proposed, not agreed: check it at step 1.1.
4. **The shipgen fixes in step 2.1** (unique ids, the `Casemate` flag) change the export. Confirm them at the start of
   that step.

## Background

- `research/simulation/damage-model-research.md`: the synthesis; §12 the prior art and patterns that fit, §13 what the
  generator could emit. `damage-research/06-service-networks.md`: power, hydraulics and data as routes through
  compartments.
- `research/naval/realtime-tech-stack-plan.md`: the sim (fixed tick, deterministic, SoA stores, own job system) and
  the sprite renderer (instanced, texture arrays).
- `docs/shipgen/TODO.md` "Damage model and hitboxes": what the export still lacks (links and flags, pass-throughs,
  double-bottom contents, carrier parts).
