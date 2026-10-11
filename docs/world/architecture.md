# The world: how the game is organised around a living planet

2026-10-11. Status: an investigation, proposed to the user; nothing here is agreed yet except what
`docs/battle/plan.md` "The world" already settled (one sphere, Earth-centred doubles, flat only locally). The open
questions are at the end.

## What has to hold

- **One world, always alive.** The planet, its navies, ships, ports and yards exist from the start of a game to its
  end. Other screens (the designer, a ship card, a port's dockyard) come and go on top; the world under them keeps
  its state, and its clock runs or pauses by the game's rules, not because a screen opened.
- **Nothing of the designer lives in the game.** It's opened with a design and what the world allows, hands a design
  back, and is gone (it already works that way: `DesignerScene(device, design, path, onClose)` with its own worker).
- **Absurd time compression out of combat:** a day in a few real seconds is ~30,000x. At 60 fps that's 8 game
  minutes a frame.
- **A real battle when it matters:** fixed small ticks, full ship models (fire control, damage, flooding), up to
  ~2,000 ships in the world and a few hundred in one fight.
- **Deterministic and headless:** the same start and the same orders give the same world (replays, tests, saves).

## The core problem: one fixed tick can't do both

The tech-stack plan has one fixed 20-30 Hz tick for everything. At 30,000x that's ~900,000 ticks per real second;
with 2,000 ships it can't be done and wouldn't be worth doing: a ship crossing the Atlantic at 15 kn is a great
circle and a fuel curve, not a million updates.

The shells already showed the answer (`docs/battle/plan.md` "Shells"): **what can be known in closed form isn't
stepped, it's evaluated; the sim jumps between events.** Applied to the whole world, that gives tiers of time on one
clock. Kerbal Space Program is the well-known prior art: craft far from the player are "on rails" (analytic orbits),
time warp is only allowed while nothing needs physics, and it drops when something does. Command: Modern Operations
does the same at sea: compression up to days, dropped on contact.

### Three tiers, one clock

| Tier | What's in it | How it advances | Cost |
|---|---|---|---|
| **Rails** | Ships and groups in transit, at anchor, building in a yard; weather, the sun, tides of supply | Each thing is an analytic function of time from an anchor (position, forward, speed, fuel at t₀). Changes come as events, which re-anchor it. | Per event, not per tick |
| **Coarse** | What has slow state that won't stay analytic: a damaged ship still flooding or burning in transit, repair work, a formation manoeuvring | Fixed steps of seconds to a minute, models written to be stable at that step | Per ship in it, cheap |
| **Tactical** | Ships in an encounter (below) | The fixed battle tick (20 Hz, or larger if the analytic shells and arc motion allow) with every model | The real cost; bounds the speed |

- **One world clock.** Every tier is on it; nothing runs on a clock of its own, so a reinforcement steaming on rails
  arrives at the battle at the right moment and causality is never in doubt.
- **The speed the player asks for is clamped by the costliest live tier.** Rails alone: as fast as asked. A battle
  on: what the CPU can do (a 4 ms tick at 20 Hz gives ~12x). The UI shows asked and achieved.
- **Ticks nest.** Tactical steps fall inside coarse ones, coarse ones on whole multiples, events at exact times; at
  equal times the order is fixed (time, tier, entity id), so the result never depends on thread timing.
- **Rails models must be analytic or event-driven; anything else must say what step it needs.** A model that can't
  take a large step moves its ship into a finer tier while it's active (a flooding ship goes coarse until it's
  stable or pumped out). That's the rule that makes the speed-up safe: no model is silently integrated at 8 minutes
  a step.

### Movement on rails

- A leg is a great circle at constant speed: position(t) is the anchor rotated by speed·(t - t₀) / R about the leg's
  axis. A route is legs between waypoints; reaching one is an event. Turns are instantaneous on rails (at 8 game
  minutes a frame, a 2-minute turn is invisible); the tactical tier has real turning circles.
- Fuel, stores and wear are linear in time for a leg (a rate from speed and the plant), so "fuel at t" is a formula
  and "bingo fuel" is an event solved when the leg is anchored.
- What changes a rate (an order, a speed change, weather getting up, entering a current) is an event that
  re-anchors: the state at that moment becomes the new t₀.
- Routing around land is done once, when the order is given (on the Earth model's sea graph), and gives the legs.
  Nothing on rails checks for land per tick.

### Contacts: when rails stop being enough

The one thing that can't be scheduled ahead by a single ship is meeting another. So:

- **A contact scan** runs at a fixed game-time interval (say 10 minutes): every moving group's swept cap over the
  interval goes into a grid on the sphere (a 3D hash on the Earth-centred position, no poles); for opposing pairs
  that can close within range in the interval, the exact time the distance crosses the encounter range is solved
  (two great circles: relative motion is near-linear over 10 minutes, a quadratic). That time is an event. At
  30,000x it's ~50 scans per real second over 2,000 groups: nothing.
- **An encounter** opens when opposing forces come within the encounter range (beyond the longest sighting range,
  ~50 km, so the tactical tier owns everything visible). Its ships go tactical; encounters merge when they overlap
  and close when the sides have been apart for a while. Sighting proper (who sees whom: horizon, weather, night) is
  the tactical tier's job; the encounter range only says when to start looking carefully.
- **Speed drops are the sim's decision, at the exact event time**, not the UI's a frame later: the sim stops at the
  contact tick, sets the speed by the player's rules (pause, 1x, ...) and tells the UI. Other events can do the same
  (a ship completed, a convoy arrived, fuel low). Deterministic, and the player never overshoots a contact by 8
  minutes.

## The pieces and where they live

```
Fleetwright.Shipgen      Design -> Ship (hitboxes, report)                    unchanged
       ▲
Fleetwright.Sim          the world; no GPU, no SDL, no ImGui; deterministic; headless tests and a CLI
  World                  the one root: clock, stores, registries; written only by the sim thread
    Clock                world time as a long (ms since an epoch), the asked speed, pause
    Scheduler            the event queue (time, tier, id ordered) and the coarse and tactical steppers
    Earth                the sphere maths (double vectors, great circles, local frames), later the Earth model:
                         land, the sea graph for routing, ports; the sun and moon as functions of time
    Navies, Fleets       factions; groups with orders and routes (rails tracks)
    Designs              each navy's accepted designs, by content hash -> ShipClass (battle plan), compiled once
    Ships                ShipState stores (SoA): tier, track, condition tables (battle plan)
    Yards                construction as events: laid down, launched, completed
    Encounters           the tactical bubbles and their battle state (shells, fire control)
  Commands               the only way in: orders, designs submitted, ships laid down; stamped with the tick
  Snapshot               the only way out: what the screens need, published after a step
       ▲
Fleetwright (exe)        the app: engine, scene stack, screens, renderers
  App                    owns the World, the sim thread and the scene stack
  MapScene               the globe: the world's main screen (renderers: planet, ships batch, effects)
  DesignerScene          pushed on top with a design and a DesignContext, pops with a design or nothing
  dev scenes             the viewers and SceneSwitcher, unchanged, for shipgen work
```

This replaces the battle plan's open question 2 (a `Fleetwright.Battle` project): the battle is the tactical tier of
`Fleetwright.Sim`, and `ShipClass` / `ShipState` live there.

### The sim thread and the boundary

- **The world runs on a thread of its own** (with the job system's workers under it), not in the frame loop. The
  frame loop asks for a target time (now + real dt × speed); the sim thread advances toward it as fast as it can and
  never past it. A heavy battle tick doesn't stutter the UI, the designer stays at 60 fps over a running world, and
  at full speed the sim uses all the time the frame doesn't.
- **Commands in.** The UI never writes the world. It enqueues commands; the sim applies them at the start of its next
  step and stamps them with that tick. The command log plus the start state is a replay; it's also the seam a
  multiplayer game would use, if ever.
- **A snapshot out.** After a step the sim fills a preallocated snapshot (triple-buffered, swapped by pointer, no
  allocation): per ship what's drawn (track parameters or pose, bearings, damage looks), per shell its few numbers,
  the encounters, the clock, and events for the UI (a contact, a ship completed). 2,000 ships is about 100-200 KB.
  Renderers read only the snapshot, never `World`.
- **Rails ships draw exactly, not interpolated:** the snapshot carries their track, and the renderer evaluates it at
  the render time, as it does shells. Tactical ships are interpolated between the last two snapshots.
- **Deep looks (a ship's damage tables in a report screen)** go through a watch list: the UI says which ships it's
  showing, and the sim copies their full state into the snapshot. Nothing on the UI side holds a reference into the
  world.
- **Start without the thread if it helps:** with commands in and the snapshot out from the first day, running the sim
  on the main thread in a time slice is the same code, and moving it to its own thread is mechanical.

### Screens: a stack, not tabs

- The engine keeps one `IScene`; in the game that's a **scene stack**. The top scene gets input and builds its UI;
  the ones under it are kept (their GPU resources too) but not drawn unless the top one is an overlay. The map is
  always at the bottom.
- **The world doesn't belong to a scene.** `App` owns it; scenes get what they need when they're made (the snapshot
  source and the command queue for the map, a `DesignContext` for the designer). Closing every scene above the map
  leaves the world exactly as it was.
- **Into the designer and back:** the map pushes `DesignerScene(device, start, onClose)` with a `DesignContext` (the
  year, the navy, its technology, limits from gameplay, its existing designs for "developed from"), copied from the
  snapshot, read-only. On accept the callback enqueues `SubmitDesign(navy, design)`, and the stack pops; the designer
  and its worker are disposed. The world compiles the design to a `ShipClass` in the background (the build is
  deterministic, so when it finishes can't change the result; the first tick that needs the class, a launch months
  later, waits if it ever has to). Laying down ships of it is another command, on the map.
- Whether the clock runs while the designer is open is a game rule, not architecture (likely: opening it pauses, as
  a menu would). The architecture allows both.
- Escape-quits and F-keys move from the engine to the stack, so a screen can mean "back" by Escape.
- The dev exe keeps `SceneSwitcher` and `DesignSession` as they are: shipgen work doesn't need a world.

### Time, in numbers

- World time is a `long` of milliseconds since an epoch (1850-01-01 UTC, say): exact, ordered, no drift over a
  century of game; ticks are whole milliseconds (50 ms tactical). Converted to seconds as double only inside models.
- 30,000x at 60 fps: 8 game minutes a frame. 2,000 groups with a waypoint every few hours is ~4,000 events per real
  second, plus the contact scans: well under a millisecond.
- A battle at 20 Hz with a 4 ms tick runs at up to ~12x; analytic shells and arc motion may let the tactical tick
  grow to 0.25-1 s, which multiplies that. Decide in the tactical tier's step, by checking results don't change with
  the tick.

### Saves and determinism

- A save is the world at a tick: the stores, the event queue, the tracks, the commands not yet applied, and the
  designs (as JSON, with their content hashes; classes are recompiled on load and checked against the hash). The
  Earth model and shipgen versions go in the header.
- Determinism within one build and platform: fixed order of ticks and events, seeded RNG streams per system, no
  wall-clock reads in the sim, parallel work reduced in a fixed order. A test runs a year on rails and a battle twice,
  on 1 and on N threads, and compares the snapshots bit for bit.

## Steps (proposed, one per session, each with its own investigation)

1. **`Fleetwright.Sim` and the rails world.** The project; double vectors and great-circle motion; the clock;
   ships and groups on rails with routes (no land yet); commands and the snapshot. Tests: a ship around the globe
   over a pole comes back; 2,000 ships for a game year headless in seconds; the same run twice is bit-identical.
2. **The app, the stack and the map.** The scene stack in the exe; `App` owning the world and the sim thread; a
   first `MapScene`: a plain globe (lat/lon grid), ships as icons from the snapshot, time controls (pause, 1x to
   30,000x, asked vs achieved); the designer pushed and popped, a submitted design laid down and launched by event.
3. **Contacts and encounters.** The scan, the exact crossing times, encounters opening, merging and closing, speed
   drops at the event. Tactical ships just steam in it for now.
4. **The tactical tier.** `docs/battle/plan.md` steps 1-7 land here: `ShipClass`, `ShipState`, the hit query, the
   registry, the sprites, damage, shells.
5. **The Earth model** (when the user brings it): land, the sea graph and routing, ports, the globe's tiles.
6. **The coarse tier** when the first model needs it (flooding after a battle).

## Open questions (the user's)

1. **The sim on its own thread from step 2** (proposed), or in the frame loop until something needs more?
2. **The project name:** `Fleetwright.Sim` (proposed), `Fleetwright.World`?
3. **The epoch and the tick:** ms since 1850 and 50 ms tactical ticks?
4. **The encounter range and the scan interval:** ~50 km and 10 minutes as starting values?
5. **What drops the speed** by default: contacts only, or also arrivals, completions, fuel?
6. **The clock while the designer is open:** paused by default?
