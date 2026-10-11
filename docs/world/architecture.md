# The world: how the game is organised around a living planet

2026-10-11. Status: the tiers, the sim thread, the commands and the snapshot, and the screen stack are agreed with the
user (2026-10-11, "Decisions" at the end); "Conflicts" is proposed. `docs/battle/plan.md` "The world" settled the
geometry (one sphere, Earth-centred doubles, flat only locally).

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

## Conflicts: who fights, how closely it's simulated, and who's watching (proposed)

Most fighting in the world isn't the player's: NPC navies at war with each other, a raider sinking merchants in the
Indian Ocean, a French cruiser shadowing a British one off Brest. Most of it must happen without anyone deciding
anything and without slowing the clock. The design splits an encounter into three independent questions.

### 1. Whether there's a fight at all: relations and rules of engagement

- **Relations are world state:** each pair of nations is at peace, in tension, or at war, and each side may treat
  third parties as neutrals (blockade, contraband). The diplomacy that moves them is its own system, later.
- **An encounter opens on contact between forces whose relations allow interaction**, not just "opposing": at war
  they may fight; in tension they shadow, warn and maybe fire (an incident, which can feed back into relations); a
  blockader stops neutrals. When rules say nothing can happen, ships just pass on rails.
- **Every group carries standing orders (rules of engagement and doctrine):** engage, engage if superior, shadow,
  evade, run for port; how far to fight (break off at x% damage); what to do with merchants (sink, capture, let go).
  The player sets them per fleet, with navy-wide defaults. The same orders drive NPC groups. They decide what
  happens in an encounter nobody is commanding.

### 2. How closely the encounter is simulated: fidelity

Each encounter runs at one of two fidelities, and can switch at a step boundary:

- **Full:** the tactical tier: the battle tick, sighting, fire control, every shell, the damage model. It's the only
  thing that slows the clock.
- **Resolved:** the same encounter in coarse steps (a minute of game time, from `SimSettings`), on the job system,
  never slowing the clock. Per step:
  - **Manoeuvre** by the groups' doctrine: range and bearing change by what each side wants (close, hold, open, flee)
    within its speed, and the ships still move on the sphere, so a running fight drifts across the map and can be
    joined by others.
  - **Sighting** as a probability over the step (horizon, weather, night) from the same sighting models.
  - **Fire as expected hits:** from range, fire-control level, the guns and the target's size and aspect
    (`fire_control_ref.py` `quick_hit_probability` is this already), the step's hits are sampled.
  - **Each sampled hit goes through the real hit query and damage model:** a point and angle of fall on the target,
    penetration, components, cells, flooding. The shell's flight is skipped; its effect is not.

  So **damage is the same kind of damage** at both fidelities: a cruiser that took hits in a resolved fight off Brest
  sails home with the same condition tables (a flooded cell, a turret out) it would have had in a full one, and the
  player meets it in port or promotes the fight halfway and sees the holes. The cost scales with hits, not ticks: a
  2-hour fight is ~120 steps, and at 30,000x it's over in a quarter of a real second on one job.
- **Calibrated against Full:** the full tactical tier runs headless, so the resolver is tuned by running the same
  scenarios thousands of times both ways and comparing outcomes (hits, sinkings, duration). That's a test, kept: a
  change to either one that moves them apart fails it.
- **Switching:** promote Resolved -> Full at any step: positions and condition tables are already real; fire-control
  solutions start from the resolver's quality. Demote Full -> Resolved: shells in flight land at once (their impacts
  are known), solutions collapse to a quality number. Each switch is a command, so replays hold.

### 3. Who commands it, and whether the player hears of it: attention

- **Control is separate from fidelity:** an encounter is commanded by the player or by captains (the AI acting on
  the standing orders). The player can let captains fight at Full and watch, or command a Resolved fight through its
  orders (break off, close) without watching it.
- **Every encounter gets an interest for the player:** own ships involved and how valuable, the size of the forces,
  ships the player flagged, how near own waters, whether a neutral or an ally is being hit. A user-configurable table
  maps interest to what happens: nothing, a dispatch in the log, a notification, a speed drop (to 1x, effectively
  paused at these scales), a pause, or straight to Full. Defaults: NPC vs NPC -> a dispatch; own merchant sunk -> a
  notification; own warship engaged -> a speed drop with "take command" or "leave it to the captain"; own fleet
  engaged -> pause.
- **Promotion is the player's, never forced by NPCs:** whatever two NPC navies do, the clock only slows when the
  player's rules say so, and the player can open any visible encounter to watch it at Full (it slows the clock while
  it's watched).
- **Several encounters at Full** slow the clock to the slowest; the UI lists them, and the player can hand any back
  to the captains.

### Many small fights: raiders, merchants and flows

The thousands of merchant ships in the world (Britain alone had ~9,000 in 1914) shouldn't each be a ship on rails
waiting to be found:

- **Merchant traffic is a flow:** tonnage per lane per day, by nation, from the Earth model's trade routes. National
  economies, convoys and losses are counted on the flows.
- **Search is a rate, not a scan:** a raider (or a patrol) working a lane finds merchants at the rate random-search
  theory gives (Koopman: sweep width x speed x traffic density). Each find is an event.
- **A found merchant materialises:** a real ship of a class from the nation's merchant pool, at a real position, in
  a Resolved encounter with the raider, run by both sides' orders (stop, sink or capture; run, scatter, signal).
  After it, the merchant is sunk, captured or gone, and goes back into the flow as tonnage, or stays a ship if it
  matters (a prize sailed home).
- **Escorted convoys are real groups** (they're few and they matter), and their merchants materialise when the convoy
  is attacked.
- **The same pattern scales other numerous small things** (submarines on patrol, later aircraft searches): aggregate
  until something finds them, a real entity from then on.

### Knowledge: each navy sees its own picture

Fog of war follows from all of this: **each navy has its own picture** (contacts, their age and accuracy, what
dispatches reported). The player's snapshot is built from the player's navy's picture, not from the world; the NPC
admiralties plan from theirs. An encounter far away reaches the player as a dispatch when someone could have reported
it (a wireless signal, a survivor), not when it happened. A navy's picture is world state like any other: saved,
deterministic, updated by events.

### NPC navies are command sources

An NPC admiralty is an AI that reads its navy's picture and issues the same commands the player does (orders, routes,
designs, ships laid down) at a coarse rate (daily for strategy, per step for its groups in encounters). It runs on the
job system, one navy per job, reading a frozen picture and writing commands that apply next step, so it's
deterministic and can't reach into the world. The player's navy and an NPC's are the same thing with a different
command source: the player could hand a theatre to an AI admiral, or watch an all-NPC world headless for tests.

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
    Encounters           the bubbles: fidelity (Full / Resolved), control, interest; their battle state
    Relations            peace / tension / war per pair of nations; neutrals
    Pictures             each navy's knowledge: contacts, dispatches
    Flows                merchant traffic per lane, raiders' search rates, the merchant pools
    Admiralties          NPC command sources (AI), reading their navy's picture
    SimSettings          every tuning number, from data
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
- **Threaded throughout (agreed):** the sim thread from the start, and the job system (later, its own step) under it
  for everything that's per ship, per group or per encounter: rails events in a batch, contact scans, the resolved
  encounters, the tactical tier's models. Work is split so results don't depend on the thread count.
- **No magic values (agreed):** the encounter range, the scan interval, the tick sizes, the tiers' steps and every
  other tuning number live in one central settings table (`SimSettings`, loaded from data), never inline in the code.

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
3. **The job system.** Fixed workers, struct jobs, counters (the tech-stack plan's "Job system"), with the
   determinism-across-thread-counts test; the rails events and the snapshot fill moved onto it.
4. **Contacts, relations and encounters.** The scan, the exact crossing times, relations and standing orders deciding
   whether an encounter opens, encounters opening, merging and closing; interest and the user's table (dispatch,
   notify, speed drop, pause). Ships just steam in an encounter for now.
5. **The tactical tier (Full).** `docs/battle/plan.md` steps 1-7 land here: `ShipClass`, `ShipState`, the hit query,
   the registry, the sprites, damage, shells.
6. **The resolver (Resolved).** Doctrine manoeuvre, sighting and expected hits per coarse step, sampled hits through
   the real hit query and damage; promotion and demotion; the calibration test against Full.
7. **The Earth model** (when the user brings it): land, the sea graph and routing, ports, the globe's tiles; then
   trade lanes and merchant flows, search rates, materialising merchants.
8. **Pictures and admiralties:** each navy's knowledge and dispatches; NPC navies as command sources.
9. **The coarse tier** for ships when the first model needs it (flooding after a battle).

## Decisions (the user's, 2026-10-11)

1. **Threaded throughout:** the sim on its own thread from the start, a job system under it (its own step).
2. **The project is `Fleetwright.Sim`.**
3. **Time:** ms since 1850, 50 ms tactical ticks; the rendering interpolates for display.
4. **The encounter range (~50 km) and scan interval (10 min)** are starting values, in the central `SimSettings`
   with every other tuning number; no magic values in the code.
5. **What drops the speed is user-configurable** (the interest table in "Conflicts").
6. **The clock while the designer is open is user-configurable;** dropping to 1x, which at these scales is as good as
   paused, is one choice.

## Open questions (the user's)

1. **Two fidelities (Full / Resolved)**, with damage always through the real hit query and damage model, and the
   resolver calibrated against Full by a test?
2. **Merchants as flows**, materialised when found, rather than every merchant a ship on rails?
3. **Per-navy pictures** (fog of war in the sim itself, dispatches reaching the player late), from the start or
   later?
4. **Relations beyond war and peace** (tension, incidents, neutrals and blockade): wanted, and how much of it is the
   game's?
