# Plan: ships and the land

2026-10-12. Status: proposed, nothing in it agreed with the user yet. "Open questions" at the end are the user's to
answer before the first step starts.

## The problem

The world has land (`Land`, `Fleetwright.Sim.Core`, step 1.3 of `docs/battle/plan.md`), and the map draws it. The
sim never asks it, so ships steam straight through Norway. The plans already say where this was left:

- `docs/battle/plan.md` 2.2: "Nothing checks for land yet." 2.5: "land on routes (rails don't check for it, by
  design: routing will)."
- `docs/world/architecture.md` "Movement on rails": "Routing around land is done once, when the order is given (on
  the Earth model's sea graph), and gives the legs. Nothing on rails checks for land per tick."
- 1.3: "the world doesn't hold the land (the map loads its own copy), and nothing in the sim asks it yet. The first
  users are routing and line of sight; the world will take the 1:10M `Land` when they come."

**What we want (the user, 2026-10-12):** no grounding, no beaching mechanic. Rule the Waves lets ships slide along
the coast: it works, but it looks silly. Here a ship should look conned by a seaman: it turns early, with its own
turning circle, keeps a sensible distance off, follows a coast at that distance, takes a strait down the middle, and
goes back to its order once it's clear. And underneath, a guarantee: a ship is never on land, in any clock tier, at
any speed, deterministically.

So there are two halves. **The look** is where the ships go long before they need the guarantee: routing and
pilotage. **The guarantee** is the step itself refusing land, which in a working game never fires.

## What the data gives (measured 2026-10-12)

Probed with the land files themselves (a planar crossing test in Python; the C# tests will redo these):

| level | rings | points | median segment |
|---|---|---|---|
| 1:110M | 126 | 4,997 | 63 km |
| 1:50M | 1,421 | 59,208 | 7.6 km |
| 1:10M | 4,049 | 404,852 | 1.6 km |

- **1:10M has ~920,000 km of coast.** Cells it touches (rough, planar): 144k at 8 km, 643k at 2 km, 1.36M at 1 km,
  2.8M at 500 m. Sizes for the index below.
- **Straits.** The Bosporus is open at 1:50M and 1:10M, about 1.3-1.7 km wide at 1:10M. The Dardanelles are shut at
  1:110M; the Jade off Wilhelmshaven is shut at 1:50M at one probe. Øresund, the Great Belt, Dover, Messina,
  Tsushima, Malacca and Scapa Flow's Hoxa Sound are open at every level. **So the sim uses 1:10M only**; the
  coarser levels are for drawing.
- **The canals are land at every level:** Suez (1869), Kiel (1895, widened 1914 for dreadnoughts), Panama (1914) and
  Corinth. All are in the game's era, and no coastline data will have them. They need data of their own
  ("Passages").
- **The antimeridian:** Chukotka is two rings sharing a 435 km edge along 180° (rings 4 and 27), and Fiji has short
  ones. These are inland, but they're not coast, and the index must skip edges lying on ±180°.
- **Lakes and the Caspian are holes,** so they're sea to `IsLand`. They're separate bodies of water, which routing
  must know.

## Principles

- **One land.** The world holds the 1:10M `Land` and its index (`Shore`, below), and the map shares the same
  immutable instances instead of loading its own copy. Every question about land goes through them, never through
  the data's format. That keeps the plan's rule that nothing may assume Natural Earth is final.
- **Land is static, so it's solved ahead.** As with the polar caps on rails and the mine fields of 4.7, everything
  land does to a ship is worked out when its order is given, as a plotted track, and followed after. Nothing checks
  for land per tick or per second, in any tier.
- **Land never breaks time compression (the user, 2026-10-12).** A ship near a coast stays on rails as readily as
  one in mid-ocean: its alterations for land are legs of its plotted track, and rails follow them analytically. Only
  what can't be known ahead (another ship, a new order) takes a ship off rails, as now.
- **Three layers, each catching what the one above misses:**
  1. **The navigator (routing):** once per order to go somewhere, a route of great-circle legs that keeps off the
     land.
  2. **The pilot (plotting):** once per intent (a course, a route, a captain's course), the track the ship will
     really steam: the intent's legs, the alterations a seaman would make for the land ahead, and the turns between
     them at the ship's own radius. Rails and the fine tier both follow this one track.
  3. **The backstop (the step):** the step never ends inside the minimum clearance. In tests it counts as a
     failure; in play it should never fire.
- **No beaching.** No grounding damage, no stranding. If that's ever wanted, it goes on top of the backstop.
- **Bathymetry slots in later.** Queries take the ship ("clearance for this ship"), not a fixed distance, so a depth
  model can later turn "distance to the coast" into "distance to the contour at this draught" behind the same call.
- **The sim's usual rules:** deterministic (ties broken by index, no thread-dependent results), nothing allocated in
  the tick, every number in `SimSettings`.

## The pieces

### 1. `Shore`: the land index (`Fleetwright.Sim.Core`)

`Land.IsLand` walks the arc to the nearest open-sea reference, which can be thousands of kilometres of chunk caps.
It's right, and it stays the reference, but it's not a per-ship, per-second query, and it can't answer "how far
is the coast?". `Shore` is built from `Land` at load and answers both.

- **A grid on the cube's gnomonic faces,** the same faces `LandMesh` tiles (there great circles are straight lines,
  so walking a leg across cells is a 2D line walk). A base of ~10 km cells (1024 per face side, ~6.3M cells) each
  holds:
  - its kind: sea, land or coast (a coast segment crosses it);
  - for coast cells, the segments crossing it (indices into `Land`'s points) and whether its centre is land;
  - for every cell, a lower bound on the distance to the nearest coast, in cells (a distance transform at build,
    capped at a few hundred km). Out at sea one lookup says "nothing within 300 km", and nearly every query in
    play stops there.
- **Queries,** all allocation-free:
  - `IsLand(p)`: the cell's kind; in a coast cell, the crossings of the short arc from the cell's centre (side
    known) to `p`, against only that cell's segments.
  - `Clearance(p, maxM)`: the distance to the nearest coast within `maxM`, and the nearest point on it (so the
    direction away from land).
  - `FirstContact(leg, clearanceM)`: along a great circle or a rhumb line from a point, the distance at which it
    first comes within `clearanceM` of the coast. Sphere tracing: step by the cells' distance bounds, exact segment
    tests only in coast cells. `ArcClear(a, b, clearanceM)` is the yes/no form.
- **Build:** ~400k segments into cells, then the distance transform. Expect a few hundred ms; measure, and cache
  to disk (like the art cache) only if it's slow. ~13 MB at 2 bytes a cell; a sparse layout if that matters.
- **The routing graph** needs more than 10 km near the coast (the Bosporus is 1.3 km wide), so coast cells split
  further for it, in quadrants down to ~250 m where the water needs it ("Routing").
- **Tests:** `Shore.IsLand` against `Land.IsLand` at a million random points and at every probe in `LandTests`;
  `Clearance` against brute force over all segments; the straits above open at the clearance a destroyer needs;
  no allocation.

### 2. Standoff: how close a ship goes

Two distances per ship, worked out once per class in `Handling` (from `MotionFacts`: length, beam, draught):

- **The minimum** is the line the backstop holds: half the beam plus `CoastMinClearanceM` (about 100 m). Nothing
  ever comes closer.
- **The standoff** is where routes and the pilot keep a ship: it grows with draught and length (a battleship well
  off, a torpedo boat close in), within settings. It's soft. A strait narrower than twice the standoff is still
  open down to the minimum, and a ship takes it down the middle, because the cost rises toward the shores.

Without bathymetry the standoff stands in for shoal water: Natural Earth plus a draught-dependent margin. It's the
number to tune by eye ("Open questions").

### 3. Routing: the navigator (`Fleetwright.Sim`, once per order)

- **`SeaRouter.Route(from, to, ship)` gives waypoints:** a pure function of the land, the passages and the ship's
  standoff. It runs inside the sim when a command applies, so the player and the AI use the same one, and a replay
  recomputes the same route. The map calls the same function on its own thread for previews (`Land` and `Shore` are
  immutable). It works in a reused workspace, so play doesn't allocate after warm-up.
- **The graph** is the sea cells, coarse out at sea (merged cells: an ocean is a few hundred nodes) and fine near
  coasts (the split cells). A cell is passable when its centre is sea and its clearance is at least the ship's
  minimum. Each edge costs its length times a penalty that rises inside the standoff.
- **A\* on that graph, then string-pulling:** drop each waypoint while the great circle that skips it is
  `ArcClear` at the standoff. What's left is a few great-circle legs that round headlands at the standoff, not a
  grid's zigzag. The helmsman already turns at the wheel-over point, so the ship's track is smooth.
- **Long hauls:** Scapa Flow to Singapore must route in a few ms. If the measured A\* doesn't, add the
  architecture's "sea graph" on top: chokepoints and ocean nodes precomputed, routed coarse first, refined at the
  ends.
- **Separate bodies of water:** the build labels them (oceans, the Caspian, lakes). An order to another body is
  refused, and the map says "no sea route".
- **Commands:** a new `GoTo` (a unit order since battle plan 2.6, routed by the unit's commander) routes from the ship's position. Shift-click
  (`AddWaypoint`) routes from the last waypoint to the new point and appends the legs. `Course` isn't routed: a
  raw course is the pilot's job. `RouteCapacity` (32) grows, or routes move to a pooled array, since a route round
  Africa has more legs than that.
- **Formations:** an admiral's route for the guide uses the standoff plus the formation's half-width, so ships
  stationed abeam aren't pushed onto the coast the guide clears.

### 4. The pilot: plotting the track ahead

The layer that makes it look right, and the one Rule the Waves lacks. It's a plot, not a watch: the land is static,
so what an officer of the watch would do at every point ahead can be worked out the moment the order is given.
Then nothing about land needs a ship in the fine tier, and the clock keeps its speed along any coast.

- **When:** the pilot plots when a ship's intent changes: a course order, a route (or `GoTo`'s), a captain's new
  course past his deadband, a group's new course for its guide. And again when the ship reaches the end of its plot
  (below). Never on a timer.
- **The plot** is a list of track pieces from the ship's pose, each analytic in distance along it:
  - a **straight**: a rhumb line at a course (a held course), or a great circle (a route's leg, or inside a polar
    cap);
  - a **turn**: an arc at constant speed and rate of turn, at the ship's radius for standard rudder (hard over where
    standard doesn't clear). `Earth.Arc` already moves a pose along such an arc exactly, for any distance, so a turn
    is as analytic as a straight.
- **How it's plotted** (a held course into a coast, say):
  1. Run the intent's straight out to the horizon and solve `FirstContact` at the standoff. No contact: the plot is
     that one straight, the common case, costing one sphere trace.
  2. A contact: choose the side, the one needing the smaller alteration from the ordered course (ties to
     starboard). Trace the standoff's contour along the coast from there: steps along the iso-line of `Clearance`,
     each using the nearest coast point's normal.
  3. At each step, test whether the ordered course is clear again for a look-ahead (the ship's speed times
     `PilotLookaheadS`, plus its turning radius). Where it is, the coast-following ends and the plot resumes the
     order.
  4. String-pull the traced contour into a few straights (drop points while the straight that skips them is
     `ArcClear` at the standoff). That's what stops a ship from exploring every fjord and bay: it crosses their mouths
     instead.
  5. Fillet the corners with turns at the ship's radius, placed so each turn starts at the wheel-over point the
     helmsman uses now. Check each turn's arc clear too; tighten to hard over or move the corner back if not.
- **The horizon:** a plot runs a bounded distance ahead (`PilotHorizonM`, say a few hundred km, or the route's end).
  Reaching its end is an event that plots the next piece from there. Still on rails: a re-plot is an event, not a
  derail. A course held along a whole continent is a chain of plots, one every few hours of game time.
- **Determinism:** the plot is a pure function of the pose, the intent, the handling and the land. So it's the same
  on every replay, whether the ship is on rails or not, and whatever thread the map previews it on.
- **Boxed in** (a course into the end of a fjord, where the contour closes with nothing clear): the plot turns the
  ship round at hard over where that clears, or failing that ends in "stop" as a piece of the plot (ring stop, then
  astern). The last resort before the backstop, and it's plotted too.
- **Who it applies to:** every helm mode except a raw `Rudder` order, which gets only the backstop. Captains'
  behaviours (follow, station, run) are unchanged: their courses are intents the pilot plots like the player's. A
  ship ordered to run, with a coast behind it, runs along the coast. For a captain chasing a moving target in the
  fine tier, his course changes past the deadband at most every second or so, and a plot costs one trace when the
  sea ahead is clear (up to a millisecond along a coast): affordable, and he's in the fine tier for his target
  anyway, not for the land.
- **In the fine tier** the helmsman steers along the plot as he does a route: the plot's straights and turns, with
  his real Nomoto lag. So a ship looks the same on either tier, and going on and off rails along a coast is seamless.
- **Visible:** the plot goes into the snapshot. The map draws it ahead of the ship, so the player sees where the
  ship will go before it goes, and the strip says why ("altered for land, 062°", with the ordered course ghosted).
  Never an unexplained deviation: the fleet-command research's lesson from Rule the Waves is that a system quietly
  overriding the player's intent reads as a bug.
- **Where it lives:** `Fleetwright.Sim.Combat` beside the helmsman, reading `Shore` (immutable, so safe for ships
  stepped in parallel). The plot is state in `ShipStore` (a pooled array of pieces per ship, like routes), hashed.
  The routing in 3 gives a route; the pilot turns any route or course into the track.

### 5. Rails along the plot

- **Rails follow the plot's pieces:** a ship's position at t is its piece at the distance run since the piece began
  (`speed · (t - t₀)`), each piece's end an event that anchors the next. That's today's route legs with turns added,
  so a ship goes round a headland on rails with a real turning circle, not a corner. At 30,000x the turns are
  invisible, but at 10x and 100x, where a player watches a coast, they're right.
- **Going on rails:** a ship steady on its plot goes on rails at any piece, a turn included. Today's check that a
  ship is steady (rudder amidships, no swing, on its course) becomes "on its plot within tolerance".
- **Groups:**
  - The guide's track is plotted at its standoff plus the formation's half-width, so ships stationed abeam clear the
    coast the guide clears.
  - **Column:** a member rides the guide's plot itself, a distance astern along it (`s - astern`), so a column turns
    in succession round a headland, each ship at the guide's wheel-over point, exactly, on rails.
  - **Abreast and echelons** keep their offsets in the guide's frame, as `RailWith` does now. Through a turn that's a
    turn together, which is right for those formations. Turns are checked clear for the outermost ship at plot
    time.
- **Coming off rails** is only for what the plot couldn't know: a contact (3.1), a new order, damage. The land never
  derails a ship.
- **A held course into a continent:** the plot follows the coast at the standoff until the ordered course is clear,
  then resumes it, all on rails, and reports the alteration once when it's plotted ("land ahead: altering to 340°
  to follow the coast"). Whether that's the behaviour or the ship should stop and wait is the user's call ("Open
  questions"), but following the coast no longer costs the clock.

### 6. The backstop (the step)

- **After the step's arc:** if the new position is inside the minimum (the cell bound first, so it's free at sea),
  move the ship back out along the coast's normal to the minimum, turn its heading parallel to the coast and drop the
  speed into it. That's Rule the Waves' slide, but here it's the failure path. It's counted (a counter in
  `ShipStore`, and a dev overlay), and the scenario tests require zero.
- **Spawning on land:** the command moves the ship to the nearest sea point (deterministically), and the map refuses
  the click at the cursor.
- **A waypoint on land** snaps to the nearest sea point at the standoff, and the map shows the snapped point.

### 7. Passages (the canals)

- `Content/World/passages.json`: each one is a polyline with its ends at sea, an opening date (and a widening date:
  Kiel 1914), limits on beam, draught and length (Panama's locks: 33.5 m beam), a speed and the transit's delays.
- **The router** adds an edge between a passage's ends, for ships within its limits and dates.
- **The transit** is on rails along the polyline at the passage's speed, with the land checks off (it's inside the
  land). The passage's ends are events.
- **It's a later step,** when a mission needs Kiel or Suez. The router just needs to allow edges that aren't
  geometry from the start.

### 8. On the map (the look)

- **Right-click** routes (`GoTo`) and draws the route. A modifier keeps the raw "steer for that point". A ghost of
  the route follows the cursor while shift is held.
- **On land, the cursor says so:** a waypoint marker snaps to the sea, spawning is refused.
- **The pilot's plot** is drawn ahead of the ship, and its alterations are named in the strip (see 4).
- **The levels disagree:** the sim uses 1:10M, but the globe zoom draws 1:110M, where the Dardanelles are shut and the
  coast can be tens of km off. A ship's icon may sit on drawn land in a narrow sea. At that zoom ships are icons and
  the land a sketch, so it's probably fine. Check with screenshots in the Aegean, the Baltic and the Norwegian fjords
  at each LOD switch. If it reads wrong, nudge the icons off drawn land, or use a finer level near selected ships.
- **A dev overlay** (like the hitbox viewer's clip boxes): `Shore`'s cells, clearance rings and standoffs, the pilot's
  traced contours, and the plot before and after string-pulling. The pilot can't be tuned without seeing it.

## Steps

Each is its own session, in the battle plan's way: read the code, check this plan against it, agree the details,
build, test, measure, update this file.

**T1 `Shore` and the world holding the land.** The index, its tests, the world taking the 1:10M `Land` and
`Shore`, and the map sharing the instance (one load, not two). Measure the build time and memory.

**T2 The backstop, spawning and waypoints.** From here no ship is ever on land, even if it still looks like Rule
the Waves. It's a small step, and it's the guarantee everything after builds on. Plus the counter, and the overlay's
first layer.

**T3 The pilot's plot, in the fine tier.** Plotting (straights, turns, contour tracing, string-pulling, fillets, the
horizon), the helmsman following a plot, the snapshot's plot and the map drawing it, the overlay's traced contours.
Scenario tests, each also a `-ships` dev command to look at:
- a course straight at the Norwegian coast, at 10 and 28 kn;
- `Run` from a ship with the Skagerrak behind the runner;
- `Follow` through the Bosporus;
- a column of six round the Skaw, and a line abreast plotted along the Jutland coast;
- a destroyer and a battleship on the same coast (different standoffs);
- a course into the end of a fjord (boxed in).

  Pass: no backstop touches, never inside the minimum, the order resumed once clear, fjord and bay mouths crossed
  rather than followed, and the plot's cost measured (one trace in clear water; along Norway's coast).

**T4 Rails along the plot.** Turn pieces on rails, the plot's events and horizon re-plots, "steady on its plot",
columns riding the guide's track astern. Tests: the same scenarios at 30,000x on rails and on the fine tick end
within metres of each other; a ship following the Norwegian coast for a day of game time stays on rails throughout
and doesn't hold the clock (the 2.5 measure: achieved speed against asked); no allocation.

**T5 The router and `GoTo`.** A\*, string-pulling, bodies of water, the map's previews. Measure route times for
North Sea hops, Scapa to Gibraltar and Scapa to Singapore (round the Cape until Suez exists), and add the coarse
sea graph only if they ask for it.

**T6 Passages,** when a mission needs one.

**Later:** bathymetry (GEBCO, behind the same clearance queries: draught-aware), 1916 harbour works and ports
(the "Earth model"), and grounding as damage if it's ever wanted.

**In the battle plan's order** (the user, 2026-10-12): T1-T3 come after 2.6 (one chain of command) and before 2.7
(the Jutland test). The Jutland test is in the North Sea, beside the Danish coast and Horns Reef, so it measures the
pilot's plots too. 2.6 comes first because it gives every intent one path to the helm, where the pilot hooks in. T4
belongs with the rails work, and T5 with 5.1 (captains' "go to") or earlier if the player's orders need it first.

**What 2.6 changes here** (`docs/battle/plan.md` 2.6, "Ready for the land"):
- `GoTo` is a unit order, not a `ShipSystem` command. The unit's commander routes it and hands the helm a `Route`.
- The formation's half-width reaches the pilot as a clearance carried by the helm order, so the pilot doesn't read
  groups.
- A column's station is a distance along its guide's track, ready for T4.
- The pilot's alterations aren't orders: they don't change who holds a unit.

## Open questions (the user's)

1. **A held course into a continent:** follow the coast at the standoff until the course is clear and report it
   (proposed; plotted, so it stays on rails), alter once to the nearest clear course and hold it, or stop and report
   (drop the clock by the interest table)?
2. **Right-click:** a routed "go to" by default with the raw "steer for" on a modifier (proposed), or the other way
   round?
3. **Standoffs:** how close looks right? A starting guess: a torpedo boat ~300 m, a destroyer ~500 m, a battleship
   ~2 km. Tune by eye with the overlay.
4. **Canals in the first version?** Kiel matters for the High Seas Fleet; Suez and Panama for anything global.
5. **May the pilot stop a ship** (boxed in: stop and astern), or must it always find a way round, even through the
   minimum?
