# Plan: ships and the land

2026-10-12. Status: agreed (the user, 2026-10-12): the open questions are answered ("Decisions", at the end), and a
review against the code added "Performance" and moved T4 ahead of the Jutland test. Each step still agrees its own
details at its start.

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

**How close (the user, 2026-10-12):** close. Rule the Waves' ships slide along touching the coast, and "if it was
good enough for Rule the Waves it's good enough for us". The game is about fleet engagements, not coastal ones (even
with MTBs in them), so the standoff is tens to hundreds of metres, not kilometres. It can grow later.

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
  - `FirstContact(leg, clearanceM)`: along a great circle, a rhumb line or a turn from a point, the distance at which
    it first comes within `clearanceM` of the coast. Far from the coast it steps by the cells' distance bounds; where
    the bound is near the clearance (a track along a coast, where sphere tracing crawls) it walks the cells the track
    passes through, with exact segment tests in each. Only great circles are straight on a face; rhumb lines and
    turns are marched. `ArcClear(a, b, clearanceM)` is the yes/no form.
- **The cells' details:** gnomonic cells are about half as wide at a face's corners as at its centre, so the bound
  is stored in metres (2 bytes at 100 m a unit), not in cells. The distance transform crosses the faces' seams. The
  data's edges lying on ±180° (Chukotka, Fiji) and Antarctica's closing edges aren't coast and are left out.
- **Build:** ~400k segments into cells, then the distance transform, deterministic on any number of threads. ~13 MB
  at 2 bytes a cell; a sparse layout if that matters. It counts against the Jutland's "under 1 s from the cache" load
  target, so it's cached to disk from the start, or built on a thread at start-up before the first spawn needs it.
  The tests share one instance, built once per run.
- **One shared copy:** the face projection now in the exe (`LandMesh`, `CapRetraction`) moves to `Sim.Core`, so
  `Shore` and the map's tiles use one cube.
- **The routing graph** needs more than 10 km near the coast (the Bosporus is 1.3 km wide), so coast cells split
  further for it, in quadrants down to ~250 m where the water needs it ("Routing").
- **Tests:** `Shore.IsLand` against `Land.IsLand` at every probe in `LandTests` and at random points (a sample in
  the suite; a million as a bench, since `Land.IsLand` at 1:10M is slow); `Clearance` against brute force over all
  segments; the straits above open at the clearance a destroyer needs; no allocation.

### 2. Standoff: how close a ship goes

Two distances per ship, worked out once per class in `Handling` (from `MotionFacts`: length, beam, draught):

- **The minimum** is the line the backstop holds: half the beam plus `CoastMinClearanceM` (a few tens of metres).
  Nothing ever comes closer.
- **The standoff** is where routes and the pilot keep a ship: a little more than the minimum, growing a little with
  length and draught, within settings. Close, as in Rule the Waves (the user, 2026-10-12): a starting guess of ~100 m
  for a torpedo boat, ~200 m for a destroyer, ~300 m for a battleship, tuned by eye with the overlay. It's soft: a
  strait narrower than twice the standoff is still open down to the minimum, and a ship takes it down the middle.

Without bathymetry the standoff stands in for shoal water. At these distances it's inside Natural Earth's own error
at 1:10M (its median segment is 1.6 km), which is fine: nobody sees the difference between the data's coast and the
real one, only a ship sitting on the drawn coast.

**Formations:** only the guide is plotted ("The pilot"), so its track must keep the whole formation clear. The
helm's clearance (2.6b) is the formation's real extent toward the land, plus the outermost ship's own standoff: a
column hugs the coast like one ship; a line abreast keeps its real width off. It isn't a kilometres-wide margin for
its own sake. Today's `AiCommand.Clearance` gives the farthest station abeam on either side; T3 may split it into port
and starboard, so a line abreast along a coast clears only its landward half.

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
- **Commands:** a new `GoTo` (a unit order since battle plan 2.6, routed by the unit's commander) routes from the
  ship's position. A routed shift-click would route from the last waypoint to the new point and append the legs. `Course` isn't routed: a
  raw course is the pilot's job. On the map, right-click stays a course and shift+right-click a plain waypoint (the
  user, 2026-10-12, "On the map"); `GoTo` gets its own control when this step comes. `RouteCapacity` (32) grows, or routes move to a pooled array, since a route round
  Africa has more legs than that.
- **Formations:** an admiral's route for the guide uses the standoff plus the formation's extent toward the land, so ships
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
- **The horizon** depends on the tier ("Performance"). On the fine tick a plot runs only as far as the ship can need
  it: its look-ahead plus its turning circle, a few km, so a captain re-ordering every few seconds near a coast
  doesn't trace hundreds of km of it each time. On rails (T4) it runs long (`PilotHorizonM`, a few hundred km, or the
  route's end), since there plots are rare. Reaching its end is an event that plots the next piece from there. Still on rails: a re-plot is an event, not a
  derail. A course held along a whole continent is a chain of plots, one every few hours of game time.
- **Determinism:** the plot is a pure function of the pose, the intent, the handling and the land. So it's the same
  on every replay, whether the ship is on rails or not, and whatever thread the map previews it on.
- **Boxed in** (a course into the end of a fjord, where the contour closes with nothing clear): the plot turns the
  ship round at hard over where that clears, or failing that ends in "stop" as a piece of the plot (ring stop, then
  astern). The pilot may stop a ship (the user, 2026-10-12). The last resort before the backstop, and it's plotted
  too, and reported in the strip.
- **Who it applies to:** a ship on its own (or detached) and a formation's guide, in every helm mode except a raw
  `Rudder` order, which gets only the backstop. **A formation's other ships aren't plotted** (the user, 2026-10-12):
  they keep station on the guide, whose plot already clears the formation ("Standoff"), and only the backstop
  watches them. At Jutland that's ~30 plotted units, not 250 ships. Captains' behaviours (follow, station, run) of
  ships on their own are unchanged: their courses are intents the pilot plots like the player's. A ship ordered to
  run, with a coast behind it, runs along the coast. A captain chasing a moving target changes course past his
  deadband at most every second or so; with the fine tick's short horizon a plot costs one cell lookup in open water
  and one short trace near a coast.
- **Captains compare against their own orders, never the plot.** A ship altering for land is off the course its
  captain gave, by design. If he read that as an error to correct, he'd re-order, the pilot would re-plot, and round
  again. 2.6 already says the alterations aren't orders; T3 tests it with each behaviour against a coast.
- **In the fine tier** the helmsman steers along the plot as he does a route: the plot's straights and turns, with
  his real Nomoto lag. That's a new controller (cross-track error, not only heading), and his error through a turn
  must stay inside the gap between the standoff and the minimum: tested per class, since the gap is small now. So a ship looks the same on either tier, and going on and off rails along a coast is seamless.
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
  - The guide's track is plotted at its standoff plus the formation's extent toward the land, so ships stationed abeam clear the
    coast the guide clears.
  - **Column:** a member rides the guide's plot itself, a distance astern along it (`s - astern`), so a column turns
    in succession round a headland, each ship at the guide's wheel-over point, exactly, on rails.
  - **Abreast and echelons** keep their offsets in the guide's frame, as `RailWith` does now. Through a turn that's a
    turn together, which is right for those formations. Turns are checked clear for the outermost ship at plot
    time.
- **Coming off rails** is only for what the plot couldn't know: a contact (3.1), a new order, damage. The land never
  derails a ship.
- **A held course into a continent** (agreed): the plot follows the coast at the standoff until the ordered course
  is clear, then resumes it, all on rails, and reports the alteration once when it's plotted ("land ahead: altering
  to 340° to follow the coast"). Following the coast doesn't cost the clock.
- **Into land only by the player's order** (the user, 2026-10-12): ideally nothing on rails heads for land at all
  unless the player ordered it. The AI's own movement (search patterns, patrols, later `GoTo`) shouldn't lay legs
  across land: a pattern's legs are checked with `ArcClear` when an admiral lays them, and clipped or shifted to sea.
  Coast-following on rails is then mostly the player's courses.
- **Ships on rails come off where their track meets land, until T4** (T2's stopgap): see "The backstop".

### 6. The backstop (the step)

- **After the step's arc:** if the new position is inside the minimum (the cell bound first, so it's free at sea),
  move the ship back out along the coast's normal to the minimum, turn its heading parallel to the coast and drop the
  speed into it. That's Rule the Waves' slide, but here it's the failure path. It's counted (a counter in
  `ShipStore`, and a dev overlay), and the scenario tests require zero. For a formation's other ships (not plotted)
  it's the only check, and it may fire if the guide's clearance is wrong; that's counted too.
- **On rails the step doesn't run,** so the backstop can't see a ship there. Until T4 plots rails, T2 adds the
  stopgap: when a unit's rails are laid or re-anchored, its track (the guide's, and its outermost riding offsets) is
  traced with `FirstContact` at the minimum, and where it meets land an event takes the unit off rails, a few
  minutes' steaming short of it, so the fine tick's backstop (and T3's pilot) take over. One trace per laying. It
  costs the clock near coasts until T4 replaces it with plotted rails.
- **Placing and snapping** put ships straight onto their stations (`RailAt`, `Placeable`, `Snap`). A station on land
  or inside the minimum makes the unit not placeable, so it forms up by steaming instead.
- **Spawning on land:** the command moves the ship to the nearest sea point (deterministically), and the map refuses
  the click at the cursor.
- **A waypoint on land:** the player's is refused: shift+right-click on land does nothing, and the cursor says so
  (the user, 2026-10-12). One from the AI or a mission snaps to the nearest sea point at the standoff.

### 7. Passages (the canals)

- `Content/World/passages.json`: each one is a polyline with its ends at sea, an opening date (and a widening date:
  Kiel 1914), limits on beam, draught and length (Panama's locks: 33.5 m beam), a speed and the transit's delays.
- **The router** adds an edge between a passage's ends, for ships within its limits and dates.
- **The transit** is on rails along the polyline at the passage's speed, with the land checks off (it's inside the
  land). The passage's ends are events.
- **It's a later step,** when a mission needs Kiel or Suez (the user, 2026-10-12: a note for the future, not in the
  first version). The router just needs to allow edges that aren't geometry from the start.

### 8. On the map (the look)

- **Right-click** gives a course, as now, not a waypoint or a route (the user, 2026-10-12); the pilot plots it.
  Shift+right-click adds a waypoint, and on land it fails. A routed `GoTo` gets its own control in T5.
- **On land, the cursor says so:** waypoints and spawning there are refused.
- **The pilot's plot** is drawn ahead of the ship, and its alterations are named in the strip (see 4).
- **The levels disagree:** the sim uses 1:10M, but the globe zoom draws 1:110M, where the Dardanelles are shut and the
  coast can be tens of km off. A ship's icon may sit on drawn land in a narrow sea. At that zoom ships are icons and
  the land a sketch, so it's probably fine. Check with screenshots in the Aegean, the Baltic and the Norwegian fjords
  at each LOD switch. If it reads wrong, nudge the icons off drawn land, or use a finer level near selected ships.
- **A dev overlay** (like the hitbox viewer's clip boxes): `Shore`'s cells, clearance rings and standoffs, the pilot's
  traced contours, and the plot before and after string-pulling. The pilot can't be tuned without seeing it.

## Performance

What land costs, and where. Found reviewing this plan against the code (2026-10-12); each step measures its part.

- **Per tick, nothing worth counting.** The backstop is one cell lookup per ship on the fine tick (exact segment tests
  only in a coast cell): ~250 ships at 20 Hz is a few microseconds a tick.
- **The plot's frequency, not its price, is the risk.** A re-plot follows every change of intent, and a captain keeping
  station re-orders course past his 1° deadband every few seconds. Had every ship been plotted with a long horizon, a
  Jutland beside the Danish coast would trace that coast ~50 times a game second, ~1 ms each: half a core at 10x. So:
  - a formation's other ships aren't plotted (~30 units at Jutland, not 250 ships);
  - the fine tick's horizon is short (look-ahead plus turning circle), and the long horizon is only on rails, where
    plots are rare;
  - a plot whose cell bound clears its horizon plus standoff is one straight, with no trace: open water costs one
    lookup.
- **Tracing along a coast** is the slow case: sphere tracing crawls along a track parallel to a coast at the
  standoff, which is exactly coast-following. Walk the cells instead there, cap the contour's steps, and string-pull
  greedily (each anchor extends as far as `ArcClear` allows, breaking early), never every pair, which is O(n²)
  grazing traces. Budget: a plot along Norway's coast under a millisecond, measured in T3.
- **Rails** gain events at the plot's piece ends: along a string-pulled coast, one every ~20-30 minutes of game time
  per ship, far fewer out at sea. Nothing for 2,000 ships at 1M×. `PoseAt` (and the contact solver stepping it) finds
  the piece by the ship's current piece index, not a search.
- **Until T4 rails stop at the coast** (T2's stopgap, "The backstop"): a unit heading for land comes off rails short
  of it and steams the coast on the fine tick. That costs the clock near coasts, which is why T4 comes before the
  Jutland test.
- **Load:** `Shore`'s build (a few hundred ms, to measure) is cached to disk or built on a thread at start-up, so it
  doesn't eat the Jutland's "under 1 s from the cache". The tests share one instance.
- **No garbage:** the pilot, the tracer and the router work in reused workspaces, one per thread (the sim's, the map's
  previews); plots live in pooled arrays in `ShipStore`; routes grow past `RouteCapacity` by pooling, not per order.
- **Determinism:** `Shore` is built the same on any number of threads, and the plot is a pure function, so a replay
  re-derives it. Replays record the land data's hash, since the land now changes results.

## Steps

Each is its own session, in the battle plan's way: read the code, check this plan against it, agree the details,
build, test, measure, update this file.

**T1 `Shore` and the world holding the land.** The index, its tests, the world taking the 1:10M `Land` and
`Shore`, and the map sharing the instance (one load, not two). The cube's face projection moves to `Sim.Core`.
Measure the build time and memory, and cache or thread the build if it's slow.

**T2 The backstop, spawning and waypoints.** From here no ship is ever on land, even if it still looks like Rule
the Waves. It's a small step, and it's the guarantee everything after builds on. With it:
- the rails stopgap (a unit whose track meets land comes off rails short of it), and placing refused onto land;
- shift+right-click on land refused, and spawning on land;
- AI search patterns laid with their legs at sea;
- the counter, and the overlay's first layer;
- **the scale tests moved to the Pacific** (the user, 2026-10-12): `RailsTests`' 2,000 ships spread from 40°S to
  40°N round the whole globe, across every continent. They go where there's room to play; they can't circle the
  world any more, and that's accepted. A test that must cross land (the meridian over both poles) runs in a world
  without land.

**T3 The pilot's plot, in the fine tier.** Plotting (straights, turns, contour tracing, string-pulling, fillets, the
horizon), the helmsman following a plot, the snapshot's plot and the map drawing it, the overlay's traced contours.
Scenario tests, each also a `-ships` dev command to look at:
- a course straight at the Norwegian coast, at 10 and 28 kn;
- `Run` from a ship with the Skagerrak behind the runner;
- `Follow` through the Bosporus;
- a column of six round the Skaw, and a line abreast plotted along the Jutland coast;
- a destroyer and a battleship on the same coast (different standoffs);
- a course into the end of a fjord (boxed in: turned round, or stopped);
- each captain's behaviour against a coast, with no re-order loop.

  Pass: no backstop touches, never inside the minimum, the order resumed once clear, fjord and bay mouths crossed
  rather than followed, and the plot's cost measured (one lookup in clear water; along Norway's coast).

**T4 Rails along the plot.** Turn pieces on rails, the plot's events and horizon re-plots, "steady on its plot",
columns riding the guide's track astern, coming off rails mid-turn with the turn's rudder and swing. It replaces T2's
stopgap. Tests: the same scenarios at 30,000x on rails and on the fine tick end within metres of each other; a ship
following the Norwegian coast for a day of game time stays on rails throughout and doesn't hold the clock (the 2.5
measure: achieved speed against asked); no allocation.

**T5 The router and `GoTo`.** A\*, string-pulling, bodies of water, the map's previews. Measure route times for
North Sea hops, Scapa to Gibraltar and Scapa to Singapore (round the Cape until Suez exists), and add the coarse
sea graph only if they ask for it.

**T6 Passages,** when a mission needs one (a note for the future).

**Later:** bathymetry (GEBCO, behind the same clearance queries: draught-aware), 1916 harbour works and ports
(the "Earth model"), and grounding as damage if it's ever wanted.

**In the battle plan's order** (the user, 2026-10-12): T1-T4 come after 2.6 (one chain of command) and before 2.7
(the Jutland test). T4 moved ahead of the test (the user: turns on rails, so land never holds the clock). The
Jutland test is in the North Sea, beside the Danish coast and Horns Reef, so it measures the pilot's plots too. 2.6
came first because it gives every intent one path to the helm, where the pilot hooks in. T5 goes with 5.1
(captains' "go to") or earlier if the player's orders need it first.

**What 2.6 changes here** (`docs/battle/plan.md` 2.6, "Ready for the land"):
- `GoTo` is a unit order, not a `ShipSystem` command. The unit's commander routes it and hands the helm a `Route`.
- The formation's extent reaches the pilot as a clearance carried by the helm order (`AiCommand.Clearance`,
  `ShipState.ClearanceM`), so the pilot doesn't read groups.
- A column's station is a distance along its guide's track, ready for T4.
- The pilot's alterations aren't orders: they don't change who holds a unit.

## Decisions (the user, 2026-10-12)

1. **A held course into a continent:** the plot follows the coast at the standoff until the course is clear, and
   reports it. Plotted, so it stays on rails.
2. **Right-click** gives a course, not a waypoint or a route. Shift+right-click adds a waypoint; on land it fails.
3. **Standoffs are close,** as in Rule the Waves, whose ships slide touching the coast: tens to hundreds of metres,
   not kilometres. The game is about fleet engagements, not coastal ones. They can grow later.
4. **Canals:** not in the first version; a note for the future (T6).
5. **The pilot may stop a ship** when it's boxed in.
6. **Turns on rails before the Jutland test** (T4 moved up). Ideally nothing on rails heads for land unless the
   player ordered it.
7. **A formation's other ships aren't plotted:** the guide's plot clears the formation, and the backstop watches them.
8. **The scale tests go to the Pacific.**
