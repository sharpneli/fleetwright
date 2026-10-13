# Plan: ships and the land

2026-10-12, reworked 2026-10-13. Status: **the rework is agreed** (the user, 2026-10-13). T1 (`Shore`) is done and stands. The rest of the 2026-10-12 plan (a pilot plotting each ship's track
ahead, rails along the plot, a router) was too complex for the result wanted now (the user, 2026-10-13), so it's
replaced by a slide along the coast: Rule the Waves' behaviour, done a little better. What was dropped and why is in
"Dropped", so it can come back if it's ever needed.

## What we want

The world has land (`Land`, step 1.3 of `docs/battle/plan.md`), and since T1 an index the sim can ask (`Shore`). But
nothing in the sim asks it yet, so ships still steam straight through Norway.

**The result (the user, 2026-10-13):** Rule the Waves' near-shore avoidance. A ship that meets a coast slides along
it, close in, and goes back to its order once the coast falls away. No grounding, no beaching. Underneath, the
guarantee: a ship is never on land, in any clock tier, at any speed, deterministically.

**A little better than Rule the Waves**, where it's cheap: the ship turns onto the coast's line with its own rudder
and turning lag, a short way before it, instead of snapping its heading against the shore. Not a seaman's plot of the
whole passage.

**How close** (the user, 2026-10-12, kept): close. "If it was good enough for Rule the Waves it's good enough for
us." Tens to hundreds of metres. The game is about fleet engagements, not coastal ones.

**Kept in mind, not built now: guided areas.** Narrow passages where ships are led or held: canals (Kiel, Suez,
Panama, Corinth), and controlled transitions between seas, the Black Sea and the Mediterranean through the Bosporus
and the Dardanelles say. The slide must leave room for them ("Guided areas").

## Where the code is (2026-10-13)

- **`Shore`** (`Fleetwright.Sim.Core`, T1): immutable, built on a thread at app start (0.16-0.3 s, 17 MB kept).
  `IsLand` (0.3 µs), `BoundM` (one lookup: a lower bound on the distance to the coast, up to 300 km), `Clearance`
  (the nearest coast point within a distance: 1.2 µs at 1 km), `FirstContact` along a great circle and
  `FirstContactRhumb` along a rhumb line (~1 µs at sea, 4-5 µs for 20 km along a coast), `ArcClear`. None allocate.
- **`World.Shore`** is optional (null: a world of water, what every test but `ShoreTests` uses). The app's world has
  one, and the map draws its 1:10M land from it.
- **Nothing reads it.** `ShipMotion.Step` moves each fine-tier ship along `Earth.Arc` and never asks about land. The
  helmsman (`ShipMotion.HelmOrder`) has three modes: `Rudder` (held), `Course` (`Steer` for the ordered course) and
  `Route` (`RouteCourse` gives the bearing of the next waypoint, then `Steer`). Both of the last two end in one
  `Steer(course)` call, which is where the slide hooks in.
- **Rails** (`ShipSystem.Rails.cs`, the one place for rails rules) lay a unit on a rhumb line (a course held) or a
  great circle (a route's leg), with events at waypoints and polar caps. `Ready` takes a unit on when it's placeable
  (placing) or steady and in station. Nothing on rails knows about land.
- **2.6b's clearance chain** (`AiCommand.Clearance`, `ShipCommand.Clearance`, `ShipState.ClearanceM`) carries a
  formation's half-width down to its guide, meant for the old pilot. Nothing reads it.
- **The map:** shift+right-click sends `AddWaypoint` (`MapScene.Ships.cs`), a click spawns a dev ship; neither
  checks for land.
- **`Handling`** has the length and the turning radii; the class's `MotionFacts` have the beam and draught for the
  standoffs.

## What the data gives (measured 2026-10-12)

| level | rings | points | median segment |
|---|---|---|---|
| 1:110M | 126 | 4,997 | 63 km |
| 1:50M | 1,421 | 59,208 | 7.6 km |
| 1:10M | 4,049 | 404,852 | 1.6 km |

- **The sim uses 1:10M only**; the coarser levels are for drawing. The Bosporus is ~1.3-1.7 km wide at 1:10M; the
  Dardanelles are shut at 1:110M and the Jade at 1:50M. Øresund, the Great Belt, Dover, Messina, Tsushima, Malacca
  and Hoxa Sound are open at every level (and at 50 m clearance in `Shore`, `ShoreTests`).
- **The canals are land at every level** (Suez, Kiel, Panama, Corinth). No coastline data will have them: they need
  data of their own ("Guided areas").
- **Lakes and the Caspian are holes,** so they're sea. Without a router that only matters to spawning.

## The slide

Four parts. The first three are in the fine tier and cover every ship; the fourth is one rule for rails.

### 1. Standoff and minimum (`Handling`, per class)

- **The minimum** is the line nothing crosses: half the beam plus `CoastMinClearanceM` (a few tens of metres).
- **The standoff** is where a sliding ship keeps: the minimum plus a little, growing with length, within settings.
  Starting guesses: ~100 m for a torpedo boat, ~200 m for a destroyer, ~300 m for a battleship, tuned by eye.
- Queries take the ship, so bathymetry can later turn "distance to the coast" into "distance to the contour at this
  draught" behind the same call.

### 2. The feeler: the helmsman slides (`Fleetwright.Sim.Combat`, `Slide.cs`)

A filter between the course the helmsman wants (the ordered course, or the bearing of the next waypoint) and `Steer`.
It changes what he steers, never the order.

- **Out at sea, one lookup:** if `Shore.BoundM` at the ship is more than its look-ahead plus its standoff, the course
  passes through. Nearly every ship, nearly every tick.
- **The look-ahead** is the ship's turning radius at standard rudder plus its speed times `SlideLookaheadS` (a few
  hundred metres to a couple of km): far enough that the turn onto the coast's line fits before the standoff.
- **Near the coast:** `FirstContact` along the wanted course over the look-ahead, at the standoff. Clear, and the
  ship isn't already inside its standoff: the wanted course passes through.
- **Sliding:** `Clearance` gives the nearest coast point, so the coast's normal and its line. Of the line's two
  directions, the ship takes the one nearer the wanted course (ties to starboard), and keeps that side until it's
  released, so it doesn't dither where the coast is square to its course. It steers along the coast's line, turned
  toward the sea by an amount that grows as it gets inside its standoff (capped at `SlideCorrectionDeg`): it follows
  the coast at the standoff. In a strait narrower than twice the standoff, the push off each side meets in the
  middle, so it goes down the middle.
- **Released** when the wanted course is clear over the look-ahead again: it steers its order, and the side is
  forgotten.
- **Boxed in** (the end of a fjord, a waypoint behind a peninsula): the ship keeps following the coast round on its
  side, even if that takes it round a bay and back (the user, 2026-10-13: for now). No stop. The strip says it's
  following the coast.
- **Who:** every ship steered by `Course` or `Route`, formation members included: at sea it costs one lookup, so
  there's no need to pick only guides. A ship stationed abeam that a coast comes too close to slides along it while
  the rest of the line holds station, as Rule the Waves' does. A raw `Rudder` order gets only the backstop.
- **Orders are untouched.** The ordered course stays what the player or captain gave, and captains compare their
  ship against their own order, so the slide never starts a re-order loop. The store keeps two small things per
  ship, hashed: the side it's sliding on (0, port, starboard). The snapshot carries them, so
  the strip can say "altered for land" with the order ghosted: never an unexplained deviation (the lesson from Rule
  the Waves in the fleet-command research).
- **Deterministic:** a pure function of the ship's state and `Shore`, so replays re-derive it.

### 3. The backstop: the step refuses land (`ShipMotion.Step`)

- **After the arc:** if `BoundM` at the new position is under the minimum (free at sea), `Clearance` says how far it
  really is. On land or inside the minimum, the ship is moved out along the coast's normal to the minimum, its heading
  turned onto the coast's line (the side nearer its heading), its speed kept only along that line, its swing stopped.
  Rule the Waves' slide, as the failure path under the feeler.
- **Counted** in `ShipStore`, apart for ships sliding on their own and for a formation's: scenario tests require
  zero for a lone ship steered by `Course` or `Route`, and report how often a formation's ships needed it.
- **Spawning on land** puts the ship at the nearest sea at its standoff (from `Clearance`), deterministically; the
  map refuses the click.
- **Waypoints on land:** the player's shift+right-click there is refused, and the cursor says so (the user,
  2026-10-12). The AI's or a mission's are moved to the nearest sea at the standoff.

### 4. Rails: off before land (`ShipSystem.Rails.cs`)

Rails don't step, so neither the feeler nor the backstop sees a ship on them. One rule in the rails' file:

- **When a unit's rails are laid or re-anchored,** its guide's track (the rhumb line or the great circle) is traced
  out to `RailsLandHorizonM` at the guide's standoff plus the unit's farthest station abeam. A contact schedules a
  `LandAhead` event a look-ahead and a margin short of it. One trace per laying, a microsecond or two.
- **`LandAhead` takes the unit off rails,** and the fine tick's feeler slides it along the coast.
- **`Ready` asks for the same trace clear** past the margin, so a placing unit doesn't go straight back on rails
  heading for land, and for no ship of the unit sliding. Once a ship is clear and back on its order, it goes on rails
  again as now.
- **The cost:** until S3, a unit following a coast stays on the fine tick, so a coast holds the clock while someone's
  on it. Allowed in between: "land never breaks time compression" is a rule for the end of development, not for every
  step on the way (the user, 2026-10-13). S3 restores it before the Jutland test.

### 5. Sliding on rails (S3, before the Jutland test)

At clock speeds where nobody sees a turn (`RailsSnapMinSpeed` and up; the clock only, not the map's scale, so the
rails' one display exception isn't leaned on further), `LandAhead` doesn't derail: the same slide rule, evaluated at
the event's point, re-anchors the rails along the coast's line (an instant turn, as rails turn at waypoints now),
with the next `LandAhead` along that leg and a release check every `SlideRailsCheckM`. The same function decides in
both tiers, so they agree. Only the guide's track is kept off land there: at those speeds a formation is its marker,
drawn at the guide, and it's enough that the marker stays off land (the user, 2026-10-13); its ships riding their
offsets may cross a headland on rails, and the fine tier's feeler and backstop sort them out when they come off.

## Guided areas (not built now)

What must stay possible, and how the slide leaves room for it. Data in `Content/World/passages.json` when it comes.

- **Gates:** a line across real water, in the Dardanelles, the Bosporus, the Sound, or across a canal's mouth. A gate
  closed to a ship (by its side, the date, a mission's rule) is coast to that ship's feeler and backstop: it slides
  along it like a shore and can't pass. Open, it's nothing. That's the controlled transition between the Black Sea
  and the Mediterranean: Turkey closing the Straits is two gates closed.
- **Canals:** a centreline through what the data says is land, with a gate at each end, an opening date (and a
  widening: Kiel 1914), limits on beam, draught and length (Panama's locks: 33.5 m beam), a speed and the transit's
  delays. A ship whose track meets an open end within its limits is guided: its helm follows the centreline as a
  route at the canal's speed, with the land checks off for it until it leaves the far gate. A route already goes on
  rails, so a transit does too, with its ends as events.
- **What to keep now so they fit:** the feeler and the backstop ask about land through one small place (the slide's
  own two queries), never `Shore` from many paths, so gates add their few segments there; and a ship's slide state
  has room for "guided" (land checks off) beside "sliding".
- **A router** (A\* over `Shore`'s cells, routes round continents) isn't needed for any of this. If a routed `GoTo`
  comes, passages are edges in it.

## On the map

- Right-click gives a course, shift+right-click a waypoint, as now (the user, 2026-10-12); a waypoint or a spawn on
  land is refused, and the cursor says so.
- The strip names an alteration ("altered for land", the ordered course ghosted) and a ship following a coast.
- **A dev overlay:** the standoff and minimum around selected ships, the feeler's line and its contact, the backstop's
  touches. The slide can't be tuned without seeing it.
- **The levels disagree:** the globe zoom draws 1:110M, where the Dardanelles are shut, so an icon may sit on drawn
  land in a narrow sea. At that zoom ships are icons; check screenshots in the Aegean, the Baltic and Norway at each
  LOD switch, and nudge icons off drawn land only if it reads wrong.

## Performance

- **At sea:** one `BoundM` lookup per fine-tier ship per tick for the feeler and one for the backstop, well under a
  microsecond each. 250 ships at 20 Hz: a few tens of microseconds a tick.
- **Near a coast:** a short `FirstContact` and a `Clearance` at a km or two, a few microseconds a ship a tick. Only
  ships within a few km of land pay it. If a Jutland beside the Danish coast shows it, the feeler can run every few
  ticks instead (the course it gives held between).
- **Rails:** one trace per laying, nothing per tick. The real cost is 4's: coast-followers on the fine tick hold the
  clock until S3. S2 measures it, as S3's baseline.
- **No garbage, deterministic:** nothing allocated in the slide; its state is in the store's arrays and hashed.
  Replays record the land's hash (`Land.Hash`), since the land now changes results.

## Steps

Each is its own session: read the code, agree the details, build, test, measure, update this file.

**T1 `Shore` and the world holding the land.** Done 2026-10-12.

*Agreed at its start (the user, 2026-10-12; "always keep performance in mind"):* queries `IsLand`, `Clearance`,
`FirstContact`/`ArcClear` along great circles and rhumb lines; only the cube (`CubeSphere`) moves to `Sim.Core`; one
signed short per cell; kinds by parity; `World` takes an optional `Shore`; the build measured, threaded if slow.

*Done (2026-10-12):*
- **`CubeSphere`** (`Sim.Core`): the faces and the gnomonic projection both ways; `LandMesh` tiles on it.
- **`Shore`** (`Sim.Core`, `Shore.Build(land)`): 1024² cells a face (~12 km at a face's centre, ~5 km at its
  corners); one signed short per cell (0 coast, ±(1 + bound in 100 m steps), capped at 300 km); the coast cells'
  segments sorted by cell with an index per row. The kinds are a scanline per face (a face per thread, seeded by one
  `Land.IsLand`); the bounds a shortest path over 8-neighbours with Dial's buckets, each step a cell's width under
  its true one by 15%. Queries: `IsLand`, `BoundM`, `Clearance`, `FirstContact` (great circles: steps by the bound,
  then closed-form caps and bands per segment over ~6 km pieces), `FirstContactRhumb` (chords within a metre of the
  rhumb line, solved a metre wider, so up to two metres early), `ArcClear`. `Land.Hash` for replays.
- **The world holds it:** `World(..., shore)` and `World.Shore` (null: a world of water). The app starts the build
  on a thread before the window opens (`WorldSession.LoadShore`) and hands it to the world; the map draws 1:10M from
  `WorldSession.Shore.Land`, one load instead of two.
- **Measured** (`shipgen shore`, Release): load 0.1 s, build 0.16-0.3 s (warm to first), 95 MB transient, 17 MB
  kept, 93,535 coast cells. `IsLand` 0.3 µs; `Clearance` 1.2 µs at 1 km, 13 µs at 50 km; `FirstContact` 200 km
  at sea 0.9 µs, 20 km along a coast 4-5 µs; rhumb lines about the same. Nothing allocates. Threaded at start-up,
  so no disk cache.
- **Tests** (`ShoreTests`, one shared build): `IsLand` against `Land.IsLand` at 10,000 points (uniform, within 5 km
  of the coast, on the cube's seams and corners); the bounds never over the brute-force distance; `Clearance` equal
  to brute force to 1 cm; first contacts against the track marched every 10 m; Bosporus, Dardanelles, Øresund,
  Great Belt, Dover, Messina, Hoxa Sound, Tsushima and Malacca open at 50 m (a flood whose every step is
  `ArcClear`); no allocation; the same build twice.

**S1 The slide in the fine tier.** Standoffs in `Handling`; the feeler (`Slide.cs`) between the helm's wanted
course and `Steer`; the backstop after the arc; its counters; spawning on land moved to sea; the map refusing
waypoints and spawns on land; the slide's side in the store, hashed, and in the snapshot; the strip's
notice; the overlay's first layer. `ShipMotion.StepAll` takes the world's `Shore` (null: nothing to do). Scenario
tests, each also a `-ships` dev command to look at:
- a course straight at the Norwegian coast at 10 and 28 kn, and one 20° off it: slides at the standoff, resumes once
  clear, never inside the minimum, no backstop touch;
- a destroyer and a battleship on the same coast (different standoffs);
- `Follow` through the Bosporus (down the middle);
- a course into the end of a fjord, and a waypoint behind a peninsula: it follows the coast round, never inside the
  minimum;
- a line abreast along the Jutland coast: the landward ship slides, the rest hold station; counted;
- each captain's behaviour against a coast, with no re-order loop;
- no allocation; the feeler's cost at sea and along Norway, measured.

*Done (2026-10-13):*
- **Standoffs** (`Handling.MinClearanceM`, `StandoffM`): half the beam + 30 m; that + 1.5 lengths within 80-400 m.
  The destroyer 36 m and 223 m, the dreadnought 44 m and 319 m.
- **The feeler** (`Slide.Course`, between the helm's wanted course and `Steer`): out at sea one `BoundM` lookup. Near
  a coast, `FirstContact` along the wanted course over the look-ahead (radius at the course rudder + 30 s of speed)
  at the standoff (within it, only coming closer than the ship already is counts). Sliding, it steers along the
  coast's line where its own heading would first come inside the standoff (a corner seen before it's reached), else a
  turning radius ahead, pushed seaward up to 30° by how far inside the standoff that is. Released when the wanted
  course is clear 1.5 look-aheads on *and* within 60° of the heading: found in testing, a ship let go on a course
  139° off swung through the coast turning back to it, so it follows the coast until the coast turns its way.
- **Hard over** where the coast closes in (a corner nearer 1.5 radii, or the coast nearer 0.75 standoffs), and the
  helmsman holds it while he's more than 20° off: found in testing, his lead (meeting the swing) eased the rudder
  into the coast at a tight corner, and a destroyer at 36 kn met corners its 15° rudder couldn't make.
- **The backstop** (`Slide.Backstop`, after the arc): out to the minimum along the coast's normal (`Slide.ToSea`, a
  few pushes for corners), heading onto the coast's line, speed along it, counted per ship (`ShipState.Backstops`).
- **Spawns and waypoints** on land or inside the standoff go to the nearest sea at the standoff; nowhere near the
  sea (deep inland), no ship and no waypoint. The map refuses spawning and shift+right-click on land, and the cursor
  line and a tooltip say so.
- **The order is untouched;** a new course more than 30° off the last forgets the side. The side and the counter are
  in the store, hashed, and in the snapshot; the strip says "altered for land: following the coast, land to port,
  until the ordered course is clear", and the selected ship's minimum, standoff and feeler are drawn on the map.
- **Rails:** a unit with a ship sliding doesn't go on rails (`ShipSystem.Rails.cs`, `Sliding`). Otherwise rails still
  don't know the land, and a steady ship goes on rails within seconds, so in the app the slide shows only for ships
  off rails until S2 (the screenshots were taken with `RailsEnabled` off).
- **The dev flag** `-ships=design[:count[:side]][@lat,lon,heading[,kn]]`, repeatable.
- **Tests** (`SlideTests`, real 1:10M land, rails off): onto Norway at 10, 28 and 15 kn (070°); round Bornholm and
  resuming 090°; a destroyer and a battleship on one coast (median 229 m against 435 m); a route through the
  Bosporus with a follower; up the Oslofjord, following it round; a line abreast run onto Jutland; a runner with the
  coast behind it; spawns and waypoints on land; determinism; no allocation. None comes inside its minimum, and the
  backstop never fires but once: the battleship at 28 kn follows the coast into an inlet narrower than its turning
  circle (~700 m) and needs it at the end, turning round there taking backing and filling the helmsman doesn't do.
- **Measured:** the feeler costs 0.01 µs at sea and 0.6 µs sliding along Norway.
- **Seen, for later:** a line abreast run onto a coast piles up. The ships whose stations are on land slide along the
  same standoff line as the guide, so all three end at one spot. Keeping formations off coasts is the admirals'
  (Later), or a captain whose station is on land falls in astern of the ship inboard of him.

**S2 Rails respect the land.** The `LandAhead` trace and event, `Ready`'s clear check, all in
`ShipSystem.Rails.cs`. The scale tests move to the Pacific (the user, 2026-10-12): `RailsTests`' 2,000 ships spread
from 40°S to 40°N, where there's room; a test that must cross land (the meridian over both poles) runs in a world of
water. Measure the clock (2.5's achieved speed against asked) for a ship and a squadron holding a course into
Norway at 10,000x, 100,000x and 1M×: S3's baseline.

**S3 Sliding on rails,** so a coast no longer holds the clock: the same scenarios at 30,000x on rails and on the fine
tick end close to each other, and S2's measure shows the clock kept along Norway.

**S4 Guided areas,** gates and canals: after the Jutland test, when a mission needs one. The missions' game loop
doesn't need them (the user, 2026-10-13), but S1-S3 must keep them possible ("Guided areas").

**Later:** a router and a routed `GoTo` (passages as its edges), bathymetry (GEBCO, behind the same clearance
queries: draught-aware), ports and 1916 harbour works (the "Earth model"), grounding as damage if it's ever wanted,
admirals keeping formations off coasts (2.6b's clearance chain is theirs to read, or to remove).

**In the battle plan's order:** S1, S2 and S3 come before 2.7 (the user, 2026-10-13), the Jutland test, which is fought
beside the Danish coast and Horns Reef, so it measures the slide too.

## Dropped from the 2026-10-12 plan

Kept here so it can come back if the slide isn't enough.

- **The pilot's plot** (old T3): contour tracing along the standoff's iso-line, string-pulling to cross bay mouths,
  fillets at the ship's radius, turns as plot pieces, a cross-track helmsman, the plot in the snapshot and on the map.
  The feeler gives the slide's look with none of it. What it loses: a ship follows a bay's shore rather than crossing
  its mouth (the release check cuts most short), and the player sees no track ahead, only the alteration as it
  happens.
- **Rails along the plot** (old T4): turn pieces on rails, columns riding the guide's track astern. S3 slides with
  instant turns instead.
- **Only guides plotted** (decision 7): moot. Every ship feels, since it's cheap at sea; a formation's outer ships
  slide on the feeler, with the backstop under it.
- **The router and `GoTo`** (old T5): to "Later".
- **AI search patterns' legs checked against land:** a leg into land now slides. Revisit if the AI
  is seen doing it.
- **2.6b's clearance chain** stays in place, unread by the sim.

## Decisions

The user, 2026-10-12, still standing:

1. **Right-click** gives a course, not a waypoint or a route. Shift+right-click adds a waypoint; on land it fails.
2. **Standoffs are close,** as in Rule the Waves: tens to hundreds of metres. They can grow later.
3. **Canals** aren't in the first version (now S4, with gates).
4. **The scale tests go to the Pacific.**

The user, 2026-10-13:

5. **The pilot's plot, plotted rails and the router are dropped** for a slide along the coast (this rework).
6. **"Land never breaks time compression" holds at the end:** a coast may hold the clock until S3. Gameplay rules
   like it are what the finished game must do, not invariants for every step on the way.
7. **Boxed in, a ship follows the coast round,** for now; it isn't stopped (replaces 2026-10-12's "the pilot may stop
   a ship").
8. **S1-S3 before the Jutland test; S4 after,** when a mission needs it, but kept possible.
9. **Every ship feels,** formation members included: the earlier bend of a line abreast and its outer ship out of
   station are acceptable in the fine and coarse tiers. On rails seen from afar, it's enough that the formation's
   marker stays off land (S3).
