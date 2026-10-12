# Plan: the battle module

2026-10-11, reframed 2026-10-12. Status: what the battle module is, that it's built from an empty world up, what's
out of it and the performance targets are the user's (2026-10-12, "Decisions"). The ships' data plan is agreed (2026-10-11), with its open questions settled on
2026-10-12. "Building it from the ground up" is the step order for all battle module work; its steps from 3.1 on are
proposed and are agreed one by one.

Each step is its own session and starts with an investigation of its own: read the code it touches, check this plan
against it, agree the step's details with the user, then build. Update this file as steps finish (what was done, what
changed from the plan). Steps 1.1 to 1.4 and 2.1 are done (2026-10-12); the next is 2.2, ships on the sphere.

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
- **Fighting:** fire control, guns and shells, torpedoes, mines, collisions, the hit query, penetration, damage,
  flooding and sinking, and damage drawn on the sprites.
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

### The whole era, released in cuts

**The game's goal is the whole era of the gun and steam warship, from the pre-dreadnoughts to the early missile
ships** (the user, 2026-10-12). It gets there one bite at a time: each release is a fully working cut, and each one
widens it.

1. **The battle module:** surface ships, guns, torpedoes and mines, in missions.
2. **The campaign of about 1890-1925:** the political system, manufacturing, research, Resolved fights, the world's
   merchants, then submarines (the U-boat war is in the era).
3. **Version 2.0, aviation,** and on toward the early missile ships.

So the first cut is built for its era, but **nothing in it may assume that era.** Every step checks against the later
ones:

- **Sensors:** sighting is one sensor writing into a navy's picture. Radar, hydrophones, sonar and direction finding
  are more sensors writing into the same picture, and nothing reads "can see" anywhere else.
- **Weapons:** each is a source of hits for the one damage model, and each is a track with events: shells, torpedoes
  and mines now; bombs, rockets and guided missiles later. A guided weapon is a track that re-anchors when its
  guidance changes it, as a ship's does when an order comes.
- **Entities off the surface:** positions are Earth-centred 3D already. Surface ships stay at sea level, but no code
  assumes every entity does, so an aircraft's altitude and a submarine's depth fit later.
- **Ranges:** the encounter range and the contact scan come from the longest sensor or weapon reach in play (in
  `SimSettings`), not one constant tuned to gun ranges. Aircraft and missiles reach hundreds of kilometres.
- **Numbers:** a WWII-era world has several times the ships of 1914 ("How many ships"). Rails and light entities
  hold that; the per-ship models must not grow past what the Jutland target allows.

### The first cut's era: 1890-1925

The first full game is a campaign of **about 1890-1925**, after the political system is in. So the battle module is
tuned for the pre-dreadnought, dreadnought and First World War navies. Later designs still work (the designer makes
them, and they fight), with what the first cut has. That era asks for things a WWII-shaped plan would leave out:

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
- **Mines are critical** (the user, 2026-10-12). Mines sank the dreadnought *Audacious* in 1914 and the three
  pre-dreadnoughts lost at the Dardanelles on 18 March 1915. Fields laid by minelayers, swept by sweepers, known or
  not in each navy's picture (step 4.7).
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

**The first version ships with Natural Earth's land** (1.3; the user, 2026-10-12: already better than Rule the
Waves'). A richer Earth model (ports, bathymetry, 1916 harbours) may come later, so nothing may assume the land is
final: routes are legs between waypoints, and line of sight and the contact scan ask `Land`, never its data.

### 1. A world of water

**1.1 The sim core, headless.** The projects `Fleetwright.Sim.Core` and `Fleetwright.Sim` (the world), and
`tests/Fleetwright.Sim.Tests` (details agreed 2026-10-12):
- **`Core`**, pure values and functions, nothing that passes time:
  - `Vec3d` (doubles; the BCL's vectors are float).
  - The sphere: R = 6,371 km (a physical constant, not a setting); lat/lon <-> Earth-centred (x through 0°N 0°E,
    z through the north pole); great-circle distance by `atan2(|a×b|, a·b)` (`acos` loses precision at combat
    ranges); a position and its forward vector rotated together about their common axis; the local east/north/up
    frame and the ship frame, with a fallback at the exact poles.
  - **The ship frame is left-handed** (+x bow, +y starboard, heights up): the naval body frame's x and y with its z
    (down) turned to up, because shipgen measures heights. It stays (changing it would change every golden). The
    frame builder returns three explicit world vectors, starboard = forward × up, and a ship point maps by components
    (pos + x·forward + y·starboard + h·up), so no right-handed cross product or `CreateLookAt` can mirror it. A test
    pins it: heading north, starboard is east.
  - `WorldTime`: a `long` of ms since 1850-01-01 00:00 UTC (JD 2396758.5), a uniform clock (no leap seconds, no
    ΔT); `DateTime` for the UI, the Julian date for the sun.
  - The sun: the Astronomical Almanac's low-precision formulas (declination, right ascension, Greenwich sidereal
    time), ~0.01°, giving a unit vector in Earth-centred coordinates. **A function of time, not state:** the
    world stores nothing for it, and each snapshot carries the vector at its own time.
  - Seeded streams: our own xoshiro256** as a struct (`System.Random`'s seeded algorithm is legacy and not ours to
    pin), four ulongs of state so it saves. A named stream's seed is SplitMix64 of the world seed and a stable hash
    of its name (`string.GetHashCode` is randomised per process). `Derive(key)` gives a substream per ship or per
    shot, which is what 2.1's shots and the job system's thread-count independence need.
  - `SimSettings`: immutable, its defaults in code as the one source. Loading it from data comes in 2.2, with the
    first real tuning numbers. Settings change results, so replays will record them.
- **`World`:**
  - The scheduler: a binary heap over a preallocated array of event structs (no delegates: plain data saves and
    doesn't allocate), ordered by (time, tier, id, seq); the insertion sequence is the last tiebreak, for two events
    of one entity at one moment. Cancelling is by generation: re-anchoring bumps an entity's generation, and stale
    events are skipped when they're popped (rails, 2.5, leans on it).
  - **Partition invariance:** advancing to t1 and then t2 gives exactly what advancing straight to t2 gives. The
    clock may rest at any millisecond, but stepped models step only on their own grid of tick events.
  - Commands: a ring of structs behind a lock (`ConcurrentQueue` drops segments as garbage), applied at the sim's
    current time and stamped with it. **Determinism means:** the same start state, settings and stamped command log
    give the same world. The thread and the wall clock only pick when a command lands and when a snapshot is
    published. A replay feeds each logged command back as an event at its stamp.
  - **The speed is the sim's** (the user, 2026-10-12): asked speed and pause are world state, changed by commands
    (`SetSpeed`, `Pause`). The host only reports how much real time passed. The sim turns that into its target,
    capped so a slow sim drops the achieved speed instead of building a backlog, and a speed drop at an event (3.1)
    changes it at the exact event time with nothing to race against. The world doesn't know how it's used: an exe
    with a map, a headless test, or later a server streaming snapshots to clients. Co-op, if it comes, is one
    authoritative player whose sim this is and others who only view and send commands; the game isn't latency
    sensitive.
  - The snapshot: preallocated, triple-buffered, swapped lock-free (an index and a fresh bit). In 1.1: the clock, a
    sequence number, the sun, speed asked and achieved, paused, and the sim thread's own allocated bytes (so "no GC
    in play" can show live in F2). Tactical interpolation (2.3) puts both poses in one snapshot, so three buffers
    stay enough.
  - A world state hash (64-bit, over all state) for the determinism tests now and saves and replays later.
- **The sim thread:** waits on a signal instead of spinning, advances toward its target and never past it, publishes
  at least every few ms of wall time when behind, and catches an exception and reports it in the snapshot instead
  of freezing silently.
- **To run:** a `sim` command in the CLI printing the clock, speed asked and achieved, the subsolar point and the
  bytes allocated.

Tests:
- A live run on the thread, with commands arriving at random wall times, logs its commands. A headless replay of the
  log ends with the same state hash.
- Random partitions of one span end in the same state.
- Under random targets, the published clock only goes forward, never passes the target, and always reaches it.
- A step allocates nothing after warm-up (`GC.GetAllocatedBytesForCurrentThread`).
- The subsolar point against almanac values for dates from 1850 to 2050, including 1916-05-31 (Jutland), and the sun
  at local noon at 90° - |lat - δ|.
- The sphere's round trips, a full circle across a pole, and the frame's handedness.

**Done (2026-10-12).** As above, with what building it settled:
- **Where the clock may rest:** at R only when every event before R has run and none at or after R has
  (`World.Advance`: a slice that runs out of events finishes the last moment's events first). So a command always
  lands on a clean boundary, and a replay is just "advance to each stamp, apply": commands never go through the
  event queue. A halt at an event (`World.Halt`, a speed drop) lets that moment's other events run and rests the
  clock 1 ms after it. Two deliberately broken versions of the rule (resting mid-moment, running the events at the
  target) each fail `Commands_where_a_sliced_run_rests_replay_exactly`.
- **World systems** are `IWorldSystem`s, registered in order (ids from 1; 0 is the world). Events and commands are
  plain structs with a system and a code; tiers at equal times run World, Rails, Coarse, Tactical. Cancelling is by
  generation, as planned.
- **The snapshot** holds the clock and target, the sun, speed asked and achieved, paused, the sim thread's allocated
  bytes and a crash's error. `SimHost` measures the achieved speed over 0.25 s windows of wall time.
- **Measured:** the sun is within the Almanac's 1' of the NOAA/Meeus reference from 1850 to 2050. An empty world
  holds 30,000x with the sim thread at 0 B allocated (`shipgen sim`). Over a 40,000 km circle in 1 km steps, rounding
  walks the radius by micrometres, a random walk, not a drift.
- The `sim` command is in the `shipgen` CLI, which now references `Fleetwright.Sim` (a tool may depend on anything).

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

**Done (2026-10-12).** As above, with what building it settled:
- **The camera** looks straight down at a point, north up. The zoom is metres per pixel at that point (the bands'
  unit), from the whole globe (the eye at most 4 R up) to 0.1 m/px, through a fixed 40° field. `MapView` resolves
  it in double: projection, picking (the root solved without cancellation, |eye|² - R² as altitude·(2R + altitude))
  and everything that must stay put. Zooming at the cursor and dragging solve for the camera by Newton's method on
  where the grabbed point projects, with the step halved until it helps. A single "turn by the error" was a pixel off
  in the middle bands and diverged near the limb, because the north-up axes turn as the camera moves.
- **The globe** is ray-cast per pixel in Earth radii, with |eye|² - 1 handed over exactly from doubles, so the same
  shader holds from orbit to 150 m up. It shades plain water, day and night with a soft terminator, a glint, the
  air's glow at the limb and an anti-aliased limb. It places nothing: whatever must hold still is projected on the
  CPU.
- **The grid** (`Graticule`) is projected in double and drawn by `MapLines`: anti-aliased polylines with mitred
  joins, expanded on the CPU, the line renderer coastlines will use. The spacing runs from 30° to 1", the finest at
  least ~90 px apart. Only what's in view is sampled: the whole globe while the limb shows, else the screen border's
  span. No allocation per frame.
- **The world** is the app's (`WorldSession`): the engine's `FrameStarted` reports real time every frame, whatever
  tab is open and even minimized. The world tab is first and the default. A designer-only flag (`-section`,
  `-steps`, `-new`, `-designer-ui`, `-units`) opens the designer when no `-view` is given, so the designer's
  screenshot commands still work. New flags: `-map=lat,lon,mpp`, `-time=`, `-speed=`, `-paused`.
- **Proof:**
  - Readbacks at 12,000, 2,000, 200, 20, 1 and 0.1 m/px. Centred on 56°42'N 5°48'E, the grid's lines cross at the
    centre pixel at every zoom, and at 10 px/m the 1" lines are 308 px and 170 px apart, as they should be.
  - At 15:48 UTC on 31 May 1916 the sun is in the west over the North Sea. At 30,000x the clock kept 29,991x and
    reached night with the June pole still lit.
  - Tests (`MapCameraTests`): picking and projecting agree to 1e-5 px at every band; 1 m is 10 px at 0.1 m/px
    (including at the date line and near a pole); a 1 cm move of the camera is 0.1 px every time (no jitter, no
    steps); zoom and drag keep their point to 1e-3 px; the grid allocates nothing.

**1.3 A rough Earth** (the user, 2026-10-12: land as a concept now; then, the same day, the Earth the first version
ships with). **Done (2026-10-12):**
- **Data:** Natural Earth's land at 1:110M, 1:50M and 1:10M, from world-atlas's TopoJSON (as the map mockup used),
  baked by `tools/land_bake.py` into `Content/World/land-*.bin` (3.3 MB; attribution in `Content/World/README.md`).
  These are modern coastlines (no 1916 harbour works) and have no ports.
- **`Land`** (`Fleetwright.Sim.Core`): rings of unit vectors with bounding caps per ring and per 64-point chunk.
  `IsLand` counts crossings of the arc to a point of open sea (Point Nemo). It's not a winding sum around the point:
  Afro-Eurasia's and the Americas' rings span more than a hemisphere, so a Pacific point's antipode is inside
  Africa. Tested on 14 places at every level, the Caspian among the seas.
- **On the map** (`LandView`): each ring near the view is brought inside a cap around it, in double: the screen's
  corners pushed 5% outward, or the horizon when the limb shows. Points inside stay. Points outside are pulled in
  along their bearing onto the cap's edge, an edge crossing it is cut where it crosses, and between outside points
  the ring walks along the edge by the bearing the original arc swept. That moves nothing across a point inside, so
  land or sea there is the original's for rings of any size. The result is fanned into triangles and filled on the
  GPU by winding count into a coverage target, so nothing is triangulated, and the globe's shader colours land from
  that count. The coast is the same ring minus its walks, so fill and coast never disagree. A chunk whose cap is
  clear of the view enters as one point. The lines are rebuilt only when the view moves. No allocation.
- **The level** (2026-10-12, the user: more precise on screen before a level gives way) is chosen by the data, not
  the window: the coarsest level whose median coast segment (`Land.MedianSegmentMetres`: 63 km, 7.6 km, 1.6 km)
  is at most 10 px on screen. That puts 1:50M from ~6,300 m/px and 1:10M from ~760 m/px. The old width rule let a
  level run to 17-20 px segments. Points within 1.5 px of the last kept are dropped, so the work follows the
  coast's length on screen. Panning just inside 1:10M over Norway's fjords, the densest coast there is: ~2 ms
  DevRelease, ~8 ms Debug (p95). At 8 px, Debug went past 10 ms. The game sim uses the finest level always.
- **The first version clipped by planes through the Earth's centre**, as a GPU clips to its frustum. That's only
  valid for a ring inside a hemisphere: where a ring leaves and re-enters, the join along the plane's great circle
  can go the wrong way round. Afro-Eurasia and the Americas are bigger than a hemisphere, so whenever the screen was
  covered, land flickered on and off from one zoom step to the next (the user: "the LODs bounce").
  `LodSweepTests` zooms from the globe to 10 px/m at five places. At every step it checks the fill under a grid of
  probe pixels against that level's `IsLand`, and that the level and the grid only get finer. The old clipping
  fails all five; the cap passes. `IsLand` now counts coasts to the nearest of 15 open-sea references, with no trig
  in its culling, about 14x faster.
- **Speed** (an optimization pass, 2026-10-12, after a drag dropped frames). The grid samples each line's visible
  stretch only, solved in closed form from the horizon, and steps by curvature (a chord within 0.25 px), turning the
  last point instead of calling trig. Land culling compares cosines with no trig, rings inside the view skip the
  clip, and a drag is solved once a frame, not once per motion event. Panning at 2560x1440 (`-frametimes`, p95 CPU
  work per frame): Debug 7.0 -> 2.7 ms, DevRelease 0.66 ms. The rebuild alone is under 0.4 ms (Release).
- **Not yet:** the world doesn't hold the land (the map loads its own copy), and nothing in the sim asks it yet.
  The first users are routing and line of sight; the world will take the 1:10M `Land` when they come.

**1.4 The land as static meshes** (planned and done 2026-10-12). Since Natural Earth ships, the map should draw it the way a
game does: baked once, drawn with a few draw calls, no CPU per frame. Today the CPU rebuilds the visible land every
time the view moves (0.3-2 ms Release, up to ~8 ms Debug). The plan:

- **No triangulation.** The winding-count fill works with any set of fans. It only needs every fan triangle small
  enough that its sag below the sphere (L²/8R for an edge L) can't show at the zooms its level is drawn at. So the
  land is cut into tiles and each tile gets a fan of its own:
  - Tiles are a cube-sphere grid: six faces, each an N x N grid in its gnomonic plane, where great circles are
    straight lines.
  - Per face, a ring is brought inside a cap around the face (60°: inside a hemisphere, so the gnomonic projection
    holds), with the cap retraction `LandView` already uses (factored out to share). It's projected, clipped to the
    face square, then split into quadrants down to tile size (Sutherland-Hodgman, planar now). Clipping keeps the
    winding number of every point inside a tile, even for non-simple polygons.
  - Each tile's clipped polygon becomes a fan from its first point. A tile inside a continent is a square: two
    triangles. Polygons with no area (walks, slivers) are dropped.
- **Tile sizes, from the sag:** half a pixel at 20° off straight down, at the finest zoom a level is drawn at,
  allows a sag of 1.37 x mpp: ~8.6 km for 1:110M (from ~6,300 m/px), ~1 km for 1:50M (from ~760 m/px).
  - A fan triangle can span a tile's diagonal, and gnomonic tiles are largest at the face centre (2/N rad a side).
  - So one grid for both levels: N = 80, tiles of ~160 km and diagonals of ~230 km, which sag under 1.1 km.
  - The coast's own edges sag far less (110m p90 121 km is 290 m; 50m p90 20 km is 8 m).
- **The coast** is a static line mesh: two vertices per point, carrying the previous, current and next point and a
  side. The vertex shader projects them and mitres the join in screen space, as `MapLines` does on the CPU.
- **Precision:** floats are enough at these levels. A position at 6.4e6 m is good to ~0.5 m against pixels of
  760 m and more. The shader takes the eye as a float uniform and forms pos - eye before the rotation and
  perspective.
- **The far side:** the coverage pass gets a depth buffer. A full-screen pre-pass ray-casts a sphere 2 km smaller
  than the Earth and writes its depth, then fans draw with a depth test. Fans sag at most ~1 km, so front land
  passes and far-side land fails. The coast lines test against the same depth, written by the globe pass in the
  main target. A sliver of far-side land within ~1.4° of the limb can show through: a pixel or two at the limb.
- **The finest level stays on the CPU path.** 1:10M close in would need ~2 km tiles everywhere (millions of
  triangles). Its views are small, so `LandView` stays cheap. `MapScene` picks static or CPU by `LevelFor`.
- **Baking** happens at load, in C#, deterministic, in tens of ms for 59k points. It could later be cached to disk
  like compiled ships.
- **Proof:**
  - `LodSweepTests` checks the static fill too: fans projected through `MapView` on the CPU, the winding at probes
    against `IsLand`, with probes near the limb left out.
  - Screenshots at each band, against the CPU path's.
  - `-frametimes` while panning: no land work on the CPU above the 1:10M switch.

  **Done (2026-10-12)**, as planned, with one change: tiles are 2/128 rad a side at a face's centre (~100 km, a
  power of two for the quadrant split), not 2/80. Their diagonals sag under 0.4 km.
  - **The pieces:** `CapRetraction` is shared by the CPU path and the bake. `LandMesh` bakes 1:110M into 69k fill
    triangles and 1:50M into 126k, with their coast meshes: ~14 MB on the GPU, 30-90 ms a level once warm.
  - **The shaders:** `land3d.vert` for the fans, `coast3d.vert` for the coast mitred in screen space,
    `spheredepth.frag` for the depth pre-pass, and `globe.frag` now writes the masking sphere's depth.
  - **Checked:** `LodSweepTests` checks the baked fans at every step on the coarse levels; a bake that covers only
    a quarter of each face fails all five places. Screenshots at each band match the CPU path's.
  - **Panning in Debug** (p95 CPU work per frame, 2560x1440): globe 0.15 ms, theatre 1.1 ms, operations 0.7 ms,
    where the CPU path took 6-11 ms. Just inside 1:10M over Norway it's still ~8 ms: the CPU path, as planned.

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

**Done (2026-10-12).** As above, with what building it settled:
- **The shipgen fixes** (the user, at the start of the step): the hangar bay is `Hangar bay` on every carrier (the
  seaplane carrier's hangar block keeps `Hangar`), and a casemate gun carries `casemate: true`, so `mount` is only a
  barbette's link to its gun. Two golden commits (20 and 83 design cases, only those keys). `ShipDesign.Version` is
  1, bumped with every `golden-update` from now on.
- **`ShipClass`** (`Fleetwright.Sim.Ships`) is a table per kind of thing, each an array of immutable row structs
  (`Rows.cs`) with a string id per row: components, mounts, turret types, barbettes, funnels (with their uptakes),
  shaft lines (shaft, alley, propeller, engine room), rudders, directors, submerged tubes, cells, rooms, bulkheads,
  decks, tiers, sections, and the armour report (side plates, armour bulkheads, armour decks). A component's `Detail`
  is its row in its kind's table, and the side tables point back. Every list is a slice of a pool (links, points,
  numbers, neighbours, armour layers, deck plates), so a row holds no references. Absent numbers are NaN, absent
  links -1. The mount table is the components with a turret shape, in component order, as `TopView` draws them, and a
  test holds it to that. A mount keeps only its turret's body for now; the parts and barrels come with the hit query
  (4.1).
- **Kept from Shipgen:** the hull is `HullField` itself, and points are Shipgen's `Pt` (a plain struct). For the
  disk cache, `HullField` and the id maps are derived: written as the stations and the ids, rebuilt on load.
- **Moving** (the user: the class holds facts, the turning model is 2.2's): `MotionFacts` (length, beam, draught,
  Cb, displacements, the designed speed and power, the plant's rated, continuous and overload power, shafts, rudders
  and their area, GM, roll period), and a power curve from shipgen's own resistance model at full load every 0.5 kn to
  1.25 times the designed speed (`PowerAt`, `SpeedAt`). It meets the report's `PowerShp` within its rounding to
  100 shp. It isn't scaled through that number: a 3 kn fuzz carrier reports 0 shp. The tactical diameter is 2.2's,
  from these facts and `research/simulation/fire-control-research/manoeuvring.md`, with its numbers in `SimSettings`,
  so tuning it changes neither the class nor its hash.
- **The hashes:** the design hash is SHA-256 over the design as shipgen serializes it (its typed form, so the file's
  spelling doesn't matter). The layout hash is SHA-256 over the shipgen version and, in index order, every id, kind
  and link. Every design's layout hash is its own.
- **Strict:** an unknown kind, role, shape, band, fuel, side or extent, a link that doesn't resolve or points at the
  wrong kind, a duplicate id, a deck and bulkhead sharing a name, and links that don't agree both ways (a barbette
  and its gun, an uptake and its funnel, a shaft and its propeller and alley) are each a `ShipCompileException`.
- **The armour solids** (`ArmourSolids`) are shell-test's, compiled once into the class. "Armoured" keeps its rule:
  armour over 0 mm, and every main and secondary mount (turret armour, even at 0 mm).
- **shell-test** moved to `Fleetwright.Sim.Ships`, not the world: it's a query over a class, with no time. Shots are
  drawn up front, shot k from the run's stream derived by k, so a run is the same on any number of threads (tested).
  A trace allocates nothing. Before the old tool was deleted, the old tracer first took its deck tops from `HullField`
  (the same output on all 71 designs). Then the two agreed shot by shot on all 71 designs, 1,500 shots each, with and
  without gratings (outcome, cause, hole and entry). A port stepping 6 cm instead of 5 fails that test. The
  `shell-test` command runs the port: the same 51 designs leak without gratings, through the same causes.
- **Measured** (`shipgen bench`, Release): compiling takes 1-2 ms and allocates under 1 MB a class (Bismarck 2.1 ms,
  0.7 MB), next to ~0.1 s for the build. A class is never the cost; the build and the drawing are.
- Every golden build that builds (over 320) compiles. The world doesn't reference `Ships` yet; 2.2's `Combat` does.

**2.2 Ships on the sphere.** `ShipState`'s pose, made and dropped per entity ("Full state is made and dropped per
entity", architecture). Movement in the tactical tier at the fixed tick: acceleration and turning from the class.
Commands: a course, a speed, a route of waypoints.

*Proposed (2026-10-12), to agree at the start of the session; the choices are in "Open questions":*

- **Where it lives:**
  - `Fleetwright.Sim.Combat` (new; references `Core` and `Ships`):
    - `ShipStore`: the tactical tier's ships. Slots with a generation, a free list, and the pose in columns.
    - `ShipState`: a handle (slot, generation) with index accessors, never the arrays themselves.
    - `Handling`: what a class's numbers become for moving, worked out once per class from `MotionFacts` and
      `SimSettings`, held by the store beside the class, never in it.
    - The step: pure functions over the columns, taking `dt`.
  - `Fleetwright.Sim` (the world):
    - `TacticalTier`, an `IWorldSystem`: one tick event at `TacticalTickMs` while it has ships, none while it's empty,
      so the empty world stays free at 30,000x.
    - The ship commands, and the ships' part of the snapshot.
  - The world holds its classes in a table: a `ShipClass` comes in by API for now. In 2.3 it comes from the registry
    by design hash.
- **The pose per ship:**
  - position and forward (Earth-centred, double);
  - speed through the water, yaw rate, rudder angle, shaft power;
  - what's ordered: rudder, power, course, speed, and the route's next leg.

  No lat, lon or heading in it.
- **Turning: Nomoto, first order** (`research/simulation/fire-control-research/manoeuvring.md` §3):
  - `T·dr/dt + r = K·δ`, with `K = K'·U/L` and `T = T'·L/U`, so the steady circle doesn't depend on the speed
    (as New Jersey's didn't, much).
  - The rudder moves at a rate up to a hard-over angle. Both numbers are in `SimSettings`: 2.3°/s and 35° to start.
    Pre-1914 gear at 1-2°/s comes later, through technology.
  - K is set from the tactical diameter: steady radius ≈ 0.45 TD at hard over.
  - **TD** comes from the facts: TD/L as a function of L/B, the rudder area against L·T, and the number of rudders,
    with its coefficients in `SimSettings`. Twin rudders turn ~25% tighter (Sumner against Fletcher).
  - The fit is checked against the sourced anchors that have a golden near them: `yamato` (640 m at 26 kn, advance
    589 m), `colorado` (Maryland's 23% speed loss over 180°), `pt_boat`, and the destroyer bands for `fubuki` and
    `destroyer`.
  - No drift angle and no heel yet: heel goes with buoyancy (4.x), drift if the tracks ask for it.
- **Speed: forces from the class's own curve,** not fitted time constants:
  - thrust from shaft power, `η·P/U`, capped at low speed by a bollard limit;
  - resistance from `PowerAt(U)`, so `m·(1 + added mass)·dU/dt = η·(P - PowerAt(U))/U`;
  - astern at a fraction of the power;
  - power that changes at the plant's rate;
  - in a turn, the research's loss: drag that brings the steady speed to `U0·(1 - f·(r/r_ss)²)`.

  Coasting then decays quadratically, as the research says it should. Every number is in `SimSettings`, and the
  research's time constants (battleship 150-250 s to accelerate, and so on) become checks, not inputs. If physics
  moves a ship off a band, that's reported, not hidden.
- **Integration on arcs:**
  - Each tick, U and r are held, and the ship moves along the arc they make: forward turned by half of r·dt, then
    `Earth.Advance` by U·dt, then the other half.
  - That's second order, so the track barely depends on the tick. It's the architecture's "arc motion", the test for
    whether the tick can grow past 50 ms.
  - The rudder and r use exact exponential updates, stable at any dt.
- **The helmsman** (an order -> the rudder) is part of the ship, not a captain:
  - A course order steers by PD on the heading error, with counter-rudder, so a turn settles without overshoot.
  - It uses standard rudder (15°, `SimSettings`) unless the order says hard over: the research's practice (routine
    changes at 10-15°, hard over to evade).
  - The shortest way round unless the order says which.
  - A speed order sets the power to `PowerAt(speed)`.
  - A route is great-circle legs. The helmsman steers for the next waypoint and turns onto the next leg at the
    wheel-over point (R·tan(Δ/2) before it). Reaching the last waypoint holds its course.
  - Nothing checks for land yet. The world doesn't hold `Land` until it routes; grounding comes with it.
- **Commands** stay plain fixed-size data:
  - `Spawn`, `Drop`, `Course` (heading, rudder), `Speed` (knots, or power), `Rudder` (direct), and `ClearRoute` /
    `AddWaypoint`.
  - A route is several commands at one stamp.
  - A waypoint packs lat and lon into `A` as two int32 microdegrees (0.11 m).
  - `Command` grows by one double if `Spawn` needs it (lat/lon, heading, speed, class).
- **Entities:** the world gives a ship an id at spawn, in order. A dropped slot's generation goes up, so events and
  commands for a dropped ship are ignored (`SimEvent.Generation` is already there).
- **`SimSettings` from data** (decision 3 on 1.1):
  - The file is `Content/Sim/settings.json`. It's strict: an unknown key is an error, and missing keys keep the code's
    defaults.
  - A world records the settings it was made with, since they change results.
- **Something to look at:**
  - `shipgen trials <design>`: the ship's sea trials, run headless in the world. The table: TD, advance and transfer,
    times to 90/180/360°, speed loss, 0 -> full, coast down from full to half, crash stop, each with the research's
    band beside it. Plus the turning circle's track as an SVG.
  - Then a dev line in the world tab: a ship spawned from a golden design, drawn as a heading tick through
    `MapLines`, so a run shows it moving. The real drawing is 2.3's.
- **Tests (headless):**
  - Steaming a full circle of the globe across a pole, rudder amidships, it comes back where it started (at a 1 s
    tick, to keep the test fast).
  - A course of 000° held across a pole turns the ship round with no NaN.
  - A hard-over turn's measured TD matches its class's `Handling`, and the anchor goldens land in their bands.
  - The same turn at 50 ms, 250 ms and 1 s ticks differs by less than a stated amount. This decides the tick.
  - 50 ships of one class share one `ShipClass` and one `Handling`, and the class's layout hash is the same after.
  - A run twice gives the same hash; advancing in pieces equals advancing at once.
  - Ships spawned and dropped mid-run, with stale events ignored.
  - A tick allocates nothing.

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

*Proposed (2026-10-12), to agree at its start; it may take two sessions (2.3a one ship, 2.3b two):*

- **The snapshot's ships:**
  - Per ship: entity id and generation, the class, the pose at the last tick (position, forward, speed, yaw rate)
    and that tick's time.
  - Bearings get their slot now and stay at rest until fire control moves them.
  - The arrays are filled without allocating. They grow only on a spawn past their size.
- **Drawn at the snapshot's time, extrapolated along the arc** from the last tick with that tick's U and r, by the
  same function the sim moves by. That gives no lag, and the next tick's correction is second order (millimetres at
  50 ms). The other way is interpolating between two ticks, a tick late.
- **The class registry** (`Fleetwright.Sim.Ships`, no GPU):
  - "The class for this design hash" is the one seam, so the disk cache slots in later.
  - It builds on a few threads, drops nothing, and counts references.
  - The build makes the `Ship` once and compiles it. A hook hands the `Ship` to the art before it's let go, so a
    design is built once for both.
  - `Spawn` names a design hash. The world takes the class from the registry and waits for it if it must, so when a
    build finishes never changes the result.
  - A replay or save carries the designs (as the architecture's saves do).
- **`ShipClassArt` on the exe's side,** keyed by design hash and look:
  - It draws (`TopView`), bakes and uploads, then frees the CPU images.
  - The `HitboxMesh` is made only on request.
  - Until a class's art is ready, its ships draw as markers.
  - `DesignWorker` and `DesignSession` stay the designer's.
- **The batch sprite renderer** draws into the map's main pass:
  - Each ship is a quad of its frame's corners, projected like the land (eye-relative floats), so it's right at every
    zoom and curves away at the limb. The far side fails the depth test against the masking sphere, as the coast does.
  - The sun comes in each ship's own frame, per instance, for the height-map shadows.
  - Instancing is per class: one draw per class per layer (shadows, hull, turrets). A turret instance is a (ship,
    mount) pair, with the class's mount table in a buffer and its turret images in one atlas.
  - A Jutland's 55 classes is ~165 draws, nothing for the GPU. **Texture arrays by size class wait for 2.6** and
    come in if its numbers ask for them (a change from the tech-stack plan, to agree).
- **By band** (`docs/map-ui/README.md`):
  - Globe to operations: a marker a fixed number of pixels across, pointing along the heading.
  - Tactical: the sprite at a coarse mip, to scale, never under a minimum length in pixels (a map
    setting, not the sim's).
  - Ship: the sprite in full, turrets and shadows.
  - Cross-fades between them.
  - Clustering into markers and group symbols waits for groups (2.4).
- **Stage 1, one ship:**
  - Dev spawn on the world tab: pick a golden design, click the sea. It's the mission UI's stand-in until 6.x.
  - Click a ship to select it. A strip shows its course, speed and ordered values.
  - Orders: a course box and dial; speed as a telegraph (stop, slow, half, full, flank, astern).
  - Right-click the sea: steer for it. Shift-right-click: add a waypoint.
  - Drawn: ordered course and route lines, and a short track behind the ship.
  - A `-screenshot` flag that spawns ships, to check it without screen grabs.
- **Stage 2, a second ship with a trivial captain:**
  - Captains are a world system, at ~1 Hz (`SimSettings`). They give the same commands the player does, through the
    same apply path. The captain's commands come from the sim itself, so a replay re-derives them rather than
    reading them from the log.
  - Behaviours: keep course; follow (a point astern of a ship at a distance, its speed matched); run (away, at full).
  - Until pictures exist (3.2) a captain reads true positions. That's the one exception the architecture's "acts on
    a picture" rule gets, and it's marked in the code.
- **Tests:**
  - The registry builds 10 designs on 4 threads and gets the same hashes as one thread. Every request is answered,
    and references count down to freeing.
  - The snapshot's poses are those of the world at that tick, and the extrapolation meets the next tick within the
    bound.
  - A follower ends at its station within a tolerance; a runner opens the range.
  - Screenshots at each band, by readback.
  - `-frametimes` with 50 ships in view, nothing allocated per frame.

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

**4.6 Torpedoes and collisions.** Tubes, and running torpedoes as tracks with events (like shells). Hits below the
waterline go into the same damage. Torpedo boat and destroyer attacks need them. Then hull against hull: collisions
and ramming, damage from where the stems struck.

**4.7 Mines.**
- **Fields** are static areas on the sphere with a density, a depth and an owner. They're laid by a mission's file or
  by minelayers at sea (mine stowage and rails are a shipgen export item, `docs/shipgen/TODO.md`), and thinned by
  sweepers.
- **Crossing a field is an event, in every tier.** Fields are static, so a rails leg that crosses one is solved when
  the leg is anchored, as land will be. A ship steaming at 30,000x hits a mine at the right moment without going
  tactical. The chance per crossing comes from the density, the ship's draught and beam against the mine's depth,
  and the seeded stream.
- **Knowledge:** a navy knows its own fields. It learns the enemy's from losses, sweeping and intelligence, as
  entries in its picture, and orders route around what it knows.
- **The hit** is an underwater explosion at a point on the hull, into the same damage as a torpedo.

**4.8 The coarse tier,** for damaged ships after a fight: still flooding, or burning, while the clock is compressed.

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

### Inside `Fleetwright.Sim` (agreed 2026-10-12)

Every part of the sim is in layers with one direction of dependency, as the projects are. **Each layer is a project
of its own** (the user, 2026-10-12), so the compiler enforces the direction. Namespaces in one project can't:
C# shows a root namespace's types to its child namespaces without a `using`, so `Core` could reach the world
unnoticed. In .NET this costs nothing at runtime. Every assembly ships as IL, and the JIT (or NativeAOT, if ever)
compiles the whole program and inlines across assemblies; a single exe is `PublishSingleFile` away. Lowest first;
each may use the ones above it in this list, never the ones below:

```
Fleetwright.Sim.Core      double vectors, the sphere (great circles, frames), world time, SimSettings, seeded streams
Fleetwright.Sim.Ships     ShipClass, its compiler from Shipgen's Ship, the class registry, content hashes
Fleetwright.Sim.Combat    ShipState, the hit query, damage, flooding and buoyancy, shells: the tactical tier
Fleetwright.Sim (World)   the world, the clock and scheduler, tiers, encounters, commands in, the snapshot out
```

- `Ships` is pure data and compilation: no time, no world, nothing that changes. `Combat` gets time and a `dt`
  from the scheduler and never owns either. Only `World` knows about threads, commands and the snapshot.
- The projects: `Fleetwright.Sim.Core`, `Fleetwright.Sim.Ships`, `Fleetwright.Sim.Combat` and `Fleetwright.Sim`
  (the world). `Ships` is the only one that references `Fleetwright.Shipgen`, which never references any of them.
- Tests: `tests/Fleetwright.Sim.Tests` (xUnit, headless, no GPU).
- Step 1.1 makes `Core` and the world with nothing in it; `Ships` came in 2.1 and `Combat` comes in 2.2.

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
battle on one build and platform (cross-platform lockstep isn't a goal yet). **Keep in mind (the user, 2026-10-12):**
on one machine it's exact, but the .NET maths library may pick a different code path per CPU, so `Math.Sin` and
friends can differ in the last bit between machines. A replay or save shared between machines, or a server and its
viewers, would need trig of our own. Keeping trig to frame building keeps that swap small. No FMA intrinsics in the
sim either: the JIT doesn't fuse a·b + c by itself, and the sim mustn't ask it to.

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
8. **Mines are in the module:** they're critical (step 4.7).
9. **Submarines come after the module and before the campaign,** not with aviation in 2.0.
10. **The whole era is the goal:** pre-dreadnoughts to early missile ships. The 1890-1925 focus is only what comes
    first. It's built one bite at a time, and each release is a fully working cut that the next one expands ("The
    whole era, released in cuts").

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

The user's, 2026-10-12, on step 1.1:

1. **The layers are projects** ("Inside `Fleetwright.Sim`"), as long as that costs no optimization and the game
   still ships as one program. In .NET it costs neither.
2. **The speed is part of the sim.** The world runs fully headless and doesn't know how it's used (a server
   streaming to clients is a future use). Co-op, if it comes, has one authoritative player and others who view and
   send commands.
3. **`SimSettings` from data in 2.2,** code defaults until then.
4. **Determinism across machines:** noted (the last-bit trig differences), kept in mind, not solved now.

The user's, 2026-10-12, on step 2.1:

1. **Unique ids:** the hangar bay is `Hangar bay` on every carrier, rather than renaming the seaplane carrier's
   hangar block.
2. **The casemate flag:** `casemate: true` on the gun, and `mount` only as a barbette's link, as planned.
3. **Turning:** `ShipClass` holds the physical facts; the tactical diameter is computed by 2.2's movement model, with
   its numbers in `SimSettings`, so tuning it doesn't change a class or its hash.

## Open questions

For steps 2.2 and 2.3 (proposed 2026-10-12, to settle at the start of 2.2):

1. **Speed from forces on the class's power curve,** with the research's time constants as checks, or the research's
   fitted time constants per type as the model? Proposed: forces.
2. **Tactical diameter from a formula over the facts** (L/B, rudder area, rudders), fitted to the anchors? Proposed:
   yes, its coefficients in `SimSettings`.
3. **What 2.2 shows:** `shipgen trials` and a dev heading tick on the world tab, with the sprites in 2.3? Proposed:
   yes, since the map already exists.
4. **Texture arrays wait for 2.6:** per-class textures with instancing per class in 2.3? Proposed: yes.
5. **Drawing a tactical ship:** extrapolated along the arc from the last tick, or interpolated a tick late? Proposed:
   extrapolated.
6. **Captains in the sim,** reading true positions until 3.2? Proposed: yes.
7. **2.3 in two sessions** (one ship, then two with the registry at scale)? Proposed: as it comes.

## Background

- `research/simulation/damage-model-research.md`: the synthesis; §12 the prior art and patterns that fit, §13 what the
  generator could emit. `damage-research/06-service-networks.md`: power, hydraulics and data as routes through
  compartments.
- `research/naval/realtime-tech-stack-plan.md`: the sim (fixed tick, deterministic, SoA stores, own job system) and
  the sprite renderer (instanced, texture arrays).
- `docs/shipgen/TODO.md` "Damage model and hitboxes": what the export still lacks (links and flags, pass-throughs,
  double-bottom contents, carrier parts).
