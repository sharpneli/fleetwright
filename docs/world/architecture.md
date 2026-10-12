# The world: how the game is organised around a living planet

2026-10-11. Status: agreed with the user (2026-10-11, "Decisions" at the end), diplomacy deferred. Amended
2026-10-12: peacetime routines, quick turbo/real-time transitions, the insane captain option. The battle module
comes first. It's a deliverable, not a part of the code (the user, 2026-10-12): the final game's whole ship
simulation on this world, with missions standing in for politics and manufacturing. `docs/battle/plan.md` says what
it holds and gives the build order, from an empty sphere of water up. "What the ship simulation must already respect"
lists what it keeps to so the rest can follow. `docs/battle/plan.md` "The world" settled the geometry (one sphere,
Earth-centred doubles, flat only locally).

## Terms: zoom bands and clock tiers are different things

Two unrelated ideas both used to be called "tiers", and "tactical" named one of each. They're orthogonal, so the
docs keep them apart (the user, 2026-10-12):

- **Zoom bands** are the map's, UI only: Globe, Theatre, Operations, **Tactical**, Ship (`docs/map-ui/README.md`
  "Zoom bands"). A band is how things are drawn at a zoom so the map doesn't clutter: ships as sprites or hulls,
  ship icons further out, fleet and cluster symbols above that. A band changes nothing in the sim. "Tactical" on its
  own always means the zoom band.
- **Clock tiers** are the sim's, for time: **Routine, Rails, Coarse, Fine** ("Four clock tiers, one clock" below).
  A clock tier is how a ship's (or a group's) state advances: a schedule, an analytic track, coarse steps, or the
  fine fixed tick with every model. **Fine** was called "Tactical" until 2026-10-12 and was renamed to end the clash.
  In code it's `EventTier.Fine`; at equal times events run World, Rails, Coarse, Fine.
- **Where they touch:** only through what the player watches. At 1x every ship is in the fine tier (merchants on
  their routes are the exception, later). Zooming into a combat keeps it out of the coarse tier, in the fine one, and
  that naturally limits how much time compression can be used. Otherwise the zoom is display only.
- **The sim is independent of display state: a hard rule.** What the world does mustn't depend on what a screen
  shows. **One exception, kept only because we have to** (the user, 2026-10-12): the map's scale reaches the rails
  (`Command.SetViewScale`), so a group drawn as one marker is snapped onto rails at any speed while one drawn ship by
  ship forms up first; a formation seen snapping is jarring, and the fine tick costs the clock. It's to go once the
  snap can be hidden better (docs/battle/plan.md, "The rails rules in one place"). **No more of these.**
- **Fidelity** (an encounter's Full or Resolved, "Conflicts" below) is a third thing again: Full runs the encounter's
  ships in the fine clock tier, Resolved runs it in coarse steps with sampled hits.
- **The tactical diameter** is the naval term for a ship's turning circle, unrelated to either.

## What has to hold

- **One world, always alive.** The planet, its navies, ships, ports and yards exist from the start of a game to its
  end. Other screens (the designer, a ship card, a port's dockyard) come and go on top; the world under them keeps
  its state, and its clock runs or pauses by the game's rules, not because a screen opened.
- **Nothing of the designer lives in the game.** It's opened with a design and what the world allows, hands a design
  back, and is gone (it already works that way: `DesignerScene(device, design, path, onClose)` with its own worker).
- **Absurd time compression out of combat:** a day in a few real seconds is ~30,000x. At 60 fps that's 8 game
  minutes a frame. That's the wartime speed.
- **Decades of peace that pass quickly (the user, 2026-10-12):** a campaign runs for decades, most of them at peace,
  and at 30,000x a year is still ~18 real minutes of waiting. In peacetime the fleets are on fully automated routines
  (exercises, cruises, refits, station rotations) and the game is research, designing ships and pressuring the
  politicians for money to build them. The clock goes ~30x faster again, up to ~1,000,000x (a year in about half a
  real minute), with the world on routines ("Peacetime: routines, above rails").
- **A real battle when it matters:** fixed small ticks, full ship models (fire control, damage, flooding), up to
  ~2,000 ships in the world and a few hundred in one fight (Jutland: 250). `docs/battle/plan.md` "How many ships"
  checks these numbers against measured costs and sets targets.
- **Deterministic and headless:** the same start and the same orders give the same world (replays, tests, saves).
- **The whole era, in cuts (the user, 2026-10-12):** the goal is pre-dreadnoughts to the early missile ships. It's
  released in fully working cuts, each widening the last: the battle module, a campaign of about 1890-1925, then
  aviation in 2.0 and on. What's built first must not assume its era (`docs/battle/plan.md` "The whole era, released
  in cuts").
- **Independent components, no mixed concerns (the user's, 2026-10-12):** everything ships in the one executable, but
  each part stands alone. The designer starts with or without a world sim (a `DesignContext` or none). The sim,
  battle included, has no GPU, UI or engine dependency and runs headless. Renderers only read the sim's snapshot,
  and the sim knows nothing about them.

## The core problem: one fixed tick can't do both

The tech-stack plan has one fixed 20-30 Hz tick for everything. At 30,000x that's ~900,000 ticks per real second;
with 2,000 ships it can't be done and wouldn't be worth doing: a ship crossing the Atlantic at 15 kn is a great
circle and a fuel curve, not a million updates.

The shells already showed the answer (`docs/battle/plan.md` "Shells"): **what can be known in closed form isn't
stepped, it's evaluated; the sim jumps between events.** Applied to the whole world, that gives tiers of time on one
clock. Kerbal Space Program is the well-known prior art: craft far from the player are "on rails" (analytic orbits),
time warp is only allowed while nothing needs physics, and it drops when something does. Command: Modern Operations
does the same at sea: compression up to days, dropped on contact.

### Four clock tiers, one clock

Clock tiers, not zoom bands ("Terms" above).

| Tier | What's in it | How it advances | Cost |
|---|---|---|---|
| **Routine** | In peacetime, groups on a standing routine (exercises, cruises, refits, a station's rotation) and merchants on a regular service | A schedule: a repeating program, its position and its rates (fuel, money, wear, training) all functions of time. No events inside it; only leaving or changing it is one. | Per change of routine, almost nothing |
| **Rails** | Ships and groups in transit, at anchor, building in a yard; weather, the sun, tides of supply | Each thing is an analytic function of time from an anchor (position, forward, speed, fuel at t₀). Changes come as events, which re-anchor it. | Per event, not per tick |
| **Coarse** | What has slow state that won't stay analytic: a damaged ship still flooding or burning in transit, repair work, a formation manoeuvring | Fixed steps of seconds to a minute, models written to be stable at that step | Per ship in it, cheap |
| **Fine** | Ships in an encounter (below), and every ship at 1x | The fixed fine tick (20 Hz, or larger if the analytic shells and arc motion allow) with every model | The real cost; bounds the speed |

- **One world clock.** Every tier is on it; nothing runs on a clock of its own, so a reinforcement steaming on rails
  arrives at the battle at the right moment and causality is never in doubt.
- **The speed the player asks for is clamped by the costliest live tier.** Routine and rails alone: as fast as
  asked, up to the cap for what's live ("Peacetime" below). A battle on: what the CPU can do (a 4 ms tick at 20 Hz
  gives ~12x). The UI shows asked and achieved.
- **Ticks nest.** Fine steps fall inside coarse ones, coarse ones on whole multiples, events at exact times; at
  equal times the order is fixed (time, tier, entity id), so the result never depends on thread timing.
- **Rails models must be analytic or event-driven; anything else must say what step it needs.** A model that can't
  take a large step moves its ship into a finer tier while it's active (a flooding ship goes coarse until it's
  stable or pumped out). That's the rule that makes the speed-up safe: no model is silently integrated at 8 minutes
  a step.

### Movement on rails

- A leg is a great circle at constant speed: position(t) is the anchor rotated by speed·(t - t₀) / R about the leg's
  axis. A route is legs between waypoints; reaching one is an event. Turns are instantaneous on rails (at 8 game
  minutes a frame, a 2-minute turn is invisible); the fine tier has real turning circles.
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
  ~50 km, so the fine tier owns everything visible). Its ships go into the fine tier; encounters merge when they overlap
  and close when the sides have been apart for a while. Sighting proper (who sees whom: horizon, weather, night) is
  the fine tier's job; the encounter range only says when to start looking carefully.
- **Speed drops are the sim's decision, at the exact event time**, not the UI's a frame later: the sim stops at the
  contact tick, sets the speed by the player's rules (pause, 1x, ...) and tells the UI. Other events can do the same
  (a ship completed, a convoy arrived, fuel low). Deterministic, and the player never overshoots a contact by 8
  minutes.

### Peacetime: routines, above rails (the user, 2026-10-12)

30,000x is right for a war: contacts drop it at the exact time, and a day still takes a few seconds, so the player
sees a campaign unfold. Peace is different. Fleets exercise, cruise, refit and rotate on stations, all of it
automated, and nothing can meet anything that matters. A campaign of decades has to get through those years quickly,
so that the player's time goes to research, the designer and the budget, not to watching ships go round.

- **A routine is a schedule, not a route.** A group's standing routine (home fleet: a month in harbour, exercises
  off Portland, the autumn cruise, the annual refit; a foreign station's rotation) is a repeating program. Its
  position at t is the program evaluated at t (the legs come from the same great circles as rails), and its fuel,
  money, wear and crew training are rates over the program's cycle. Waypoints inside it aren't events, because
  nothing else needs them: a renderer evaluates the position, a report evaluates the totals. Leaving the routine,
  changing it, or a refit starting (a yard slot) are the only events.
- **Merchants in peacetime are services:** a ship sails a line between ports on its schedule, its position a
  function of t, and the trade it carries a rate. When the world leaves peace (below), each one is put on rails at
  where its schedule has it at that tick, as a light entity with a real track. Being a function of t, that's
  deterministic and costs nothing to keep for 9,000 ships.
- **Relations gate the contact scan.** Only pairs whose rules allow interaction are solved. At peace there are
  none, so the scan has no work, and ships just pass ("Conflicts", 1) without being looked at.
- **Exercises are the routine's training rate,** not fights: a gunnery program raises a crew's efficiency at a cost in
  fuel, ammunition and money, drawn from the budget. Accidents and breakdowns are events drawn from rates, from a
  seeded stream. No exercise is simulated in a finer tier unless the player opens one to watch (later).
- **The routine cap: ~1,000,000x** (`SimSettings`), allowed while nothing is coarse or fine and no pair can
  interact. At 60 fps that's ~4.6 game hours a frame and a year in ~32 real seconds; a quiet decade is about five
  minutes of the clock running unattended. The wartime cap (30,000x) applies as soon as any pair can interact, a
  group is on a non-routine order that can meet something, or anything is coarse or fine.
- **The drop is the sim's, at the exact event,** as with contacts: tension rising or war declared drops the clock
  to the wartime cap (or pauses it), and peacetime rows in the interest table do the rest: research completed, a
  ship launched or completed, trials, the estimates debated in parliament, intelligence of a foreign ship laid down.
  Same user-configurable table as encounters ("Conflicts", 3).
- **Routine is world state, not a speed.** Which groups are on routines and which relations hold is in the world;
  the speed only maps game time to real time. The same orders give the same world at 1x, 30,000x or 1,000,000x,
  which a test checks (a peacetime year at both caps, snapshots compared bit for bit).
- **Slow ship state is rates** (wear, fouling, stores, crew training): a year of routine is one evaluation, not a
  year of steps. A model that can't say its state at t from a rate takes the group off its routine while it's
  active, as a flooding ship goes coarse.

**Moving between turbo and real time has to be quick (the user, 2026-10-12).** Peace isn't only fast-forward: the
player can drop to 1x at any moment and do something real with a ship, then go back to the routine. None of it may
cost a loading pause:

- **Speed and tier are separate.** Dropping the speed changes nothing in the world: at 1x a routine group is still a
  schedule, drawn smoothly because its position is a function of t. Speed is the player's; the tier is per group and
  set by what it's doing.
- **Leaving a routine is one evaluation.** An order to a group on a routine (steam there, exercise off here, open
  for watching) evaluates the schedule and rates at that tick and re-anchors the group on rails from them; if it's
  watched at Full, its `ShipState` is made from the same values (full state is made per entity anyway). O(1) per
  group, applied at the next step, no catch-up.
- **Going back is one command.** "Resume routine" puts the group back on its program: it routes to where the
  program is heading, or the program's phase is shifted to start from where the group is (the player's choice per
  routine, a default in `SimSettings`). The state it built up off the routine (fuel burned, wear, damage from an
  accident) becomes the new anchor of its rates.
- **The cap follows the world, a frame later at most.** When the last group leaves rails or coarse and no pair can
  interact, the routine cap is allowed again; the player's asked speed is kept, so "back to turbo" is just setting
  the speed. The UI shows why the cap is lower (which groups hold it down), so the player can find and resume them.

**The insane captain (the user, 2026-10-12, an option).** With it on, the player may open fire in peacetime and
start a war, and then live with the politics:

- **Firing at peace is a command, not a contact.** The player orders a group off its routine to engage a target in
  the navy's picture; the command opens the encounter itself, so the contact scan stays gated by relations and
  needn't watch peaceful pairs. Without the option the command is refused (it's the world's check, like locked
  tech), with it there's a confirmation.
- **It's an incident, and incidents move relations:** the encounter is tagged with who fired first, and its result
  (ships hit, sunk, lives lost, where, and who saw it) goes to the relations interface as an event. With the
  war/peace table only, it declares war at once; tension and diplomacy, when they come, turn it into a crisis that
  may or may not end in war.
- **The consequences are politics, later:** the other nation's reaction, neutrals', and the player's own government
  (a court-martial, the budget cut or raised by a war scare). They read the same incident record, so the incident
  only has to be recorded fully now.
- **NPCs can do it too, rarely:** an admiralty's captains with a rate of incidents (the Dogger Bank incident of 1904,
  when the Russian fleet fired on British trawlers), drawn from a seeded stream. Same command, same incident record.

## Conflicts: who fights, how closely it's simulated, and who's watching (agreed 2026-10-11)

Agreed with the user, except relations beyond war and peace (deferred: the battle module comes first, but with these
decisions shaping it, so later work refactors as little as possible).

Most fighting in the world isn't the player's: NPC navies at war with each other, a raider sinking merchants in the
Indian Ocean, a French cruiser shadowing a British one off Brest. Most of it must happen without anyone deciding
anything and without slowing the clock. The design splits an encounter into three independent questions.

### 1. Whether there's a fight at all: relations and rules of engagement

- **Relations are world state:** each pair of nations is at peace, in tension, or at war, and each side may treat
  third parties as neutrals (blockade, contraband). The diplomacy that moves them is its own system, later. **For now
  (agreed): a war/peace table only**, behind an interface ("may these two interact, and how") that tension,
  incidents and neutrals can grow into without touching the encounter code.
- **An encounter opens on contact between forces whose relations allow interaction**, not just "opposing": at war
  they may fight; in tension they shadow, warn and maybe fire (an incident, which can feed back into relations); a
  blockader stops neutrals. When rules say nothing can happen, ships just pass on rails. The exception is a
  deliberate order to fire at peace (the insane captain, "Peacetime"), which opens the encounter by command.
- **Every group carries standing orders (rules of engagement and doctrine):** engage, engage if superior, shadow,
  evade, run for port; how far to fight (break off at x% damage); what to do with merchants (sink, capture, let go).
  The player sets them per fleet, with navy-wide defaults. The same orders drive NPC groups. They decide what
  happens in an encounter nobody is commanding.

### 2. How closely the encounter is simulated: fidelity

Each encounter runs at one of two fidelities, and can switch at a step boundary:

- **Full:** the fine tier: the battle tick, sighting, fire control, every shell, the damage model. It's the only
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
- **Calibrated against Full:** the full fine tier runs headless, so the resolver is tuned by running the same
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
- **Importance per own group (agreed):** the player marks fleets and ships important or not. An important one
  engaged raises the interest (a speed drop, a pause, Full); an unimportant one, some poor sod on a distant station,
  is resolved and reported in a dispatch.
- **Promotion is the player's, never forced by NPCs:** whatever two NPC navies do, the clock only slows when the
  player's rules say so, and the player can open any visible encounter to watch it at Full (it slows the clock while
  it's watched).
- **Several encounters at Full** slow the clock to the slowest; the UI lists them, and the player can hand any back
  to the captains.

### Many small fights: raiders and real merchants (agreed)

Every merchant is a real track (Britain alone had ~9,000 ships in 1914), because only real positions let a raider
deplete an area as it really would, and let a navy route its merchants away from danger:

- **A merchant is a light entity:** a position on rails (a great-circle route from port to port), its nation, its
  tonnage and cargo, and a class drawn from the nation's merchant pool. No fuel, crew or condition tables while it's
  steaming: a merchant voyage is a few legs and a few events over weeks, so 9,000 of them cost less than the warships.
  The world's merchant fleet was a few times Britain's (to verify, `research/REQUESTS.md`); that's still events and
  scans, not models.
- **Merchants are in the contact scan like everything else**, and a raider finds them where they actually are: a
  raider that sinks three ships on a route leaves it thinner, owners and admiralties divert sailings (route orders
  to the merchants, issued by their navy's AI or the player), and traffic piles up at the ports that are still safe.
- **A merchant becomes a full ship only in an encounter:** its class's `ShipClass` and a `ShipState` are made when a
  raider (or anything) engages it, in a Resolved encounter run by both sides' orders (stop, sink or capture; run,
  scatter, signal). Afterwards it's sunk, captured (a prize sailed home: it keeps its state) or let go (it drops its
  state and goes back to being light, keeping only what the voyage needs to know: damaged, slower).
- **Convoys are groups of merchants with escorts**, ordered like any group.
- **The light/full split is per entity, not per kind:** anything numerous (submarines on patrol, later aircraft) can
  start light and take full state only when something engages it.

### Knowledge: each navy sees its own picture

Fog of war is **knowledge of entities, not a visual effect** (agreed): **each navy has its own picture**, a table of
what it knows about other entities. A contact is what was learned and when: an entity (or "unidentified"), its
classification as far as known (a ship, a cruiser, a *Gloire*-class, the *Montcalm*), a position and course with
their uncertainty, the time of the report and its source (sighting, wireless, a dispatch, intelligence). It ages:
the position estimate grows with time since the last report.

- **The player's snapshot is built from the player's navy's picture,** never straight from the world: an enemy ship
  the navy doesn't know of isn't in it at all, and one it knows of is where the picture says, not where it is.
  Own ships come from the world. The map draws contacts as what they are (a reported position with its uncertainty),
  not ships with a fog over them.
- **The NPC admiralties plan from their pictures** too, so they're fooled by the same things the player is.
- **An encounter far away reaches the player as a dispatch** when someone could have reported it (a wireless signal,
  a survivor, a neutral port), not when it happened.
- Inside an encounter, the fine tier's sighting feeds the pictures of the navies taking part.
- A picture is world state like any other: saved, deterministic, updated by events.

### NPC navies are command sources

An NPC admiralty is an AI that reads its navy's picture and issues the same commands the player does (orders, routes,
designs, ships laid down) at a coarse rate (daily for strategy, per step for its groups in encounters). It runs on the
job system, one navy per job, reading a frozen picture and writing commands that apply next step, so it's
deterministic and can't reach into the world. The player's navy and an NPC's are the same thing with a different
command source: the player could hand a theatre to an AI admiral, or watch an all-NPC world headless for tests.

## Technology: what a navy can build (agreed 2026-10-11)

Agreed with the user: a mission's technology is the set of templates it unlocks (a mission makes the templates for
its sides' techs available, nothing else); the designer never refuses; the world does.

A mission set in 1910 must not offer STS steel; the campaign's research module (later, but kept in mind) decides the
same question as research completes. Both are one thing: **a navy's set of unlocked technologies**, frozen for a
mission and growing in a campaign.

- **`TechSet`** is a set of tech ids a navy has: materials (wrought iron, compound, Harvey, Krupp cemented, STS),
  plants, hull construction, gun and mount types, fire control and radar levels, and navy-wide items that aren't
  parts of a design (director firing, doctrine, signals). It's world state, per navy, in `Fleetwright.Sim`.
- **Every catalogue entry names the tech that unlocks it.** The designer's templates (`Content/Templates`, written
  by `tools/designer_templates.py`) gain a `tech` id per entry, and each tech carries its historical year (per
  nation where it differed) as data. The plant templates are already by year; a design already carries its
  technology's values, not a year (`docs/shipgen/plant-templates.md`: a navy may research things earlier or later
  than history did).
- **Where the set comes from is the only difference:** a mission gives a year preset ("everything this nation had by
  1910") or an explicit list; the campaign's research adds to the set over time. The designer and the sim don't know
  which.
- **The designer offers what the set unlocks:** it's handed the set in its `DesignContext`, and filters the template
  catalogues by it (`Templates` already says "in the game the tech tree will filter them"). Numbers a tech bounds
  rather than unlocks (the largest calibre a navy can make, boiler pressure) come as caps alongside `DesignLimits`.
- **The designer still never refuses** (the limits rule): a design loaded from a file with locked tech builds and
  shows it in the remarks ("STS steel: not available, 1910"). **The world refuses:** such a ship can't be added to a
  mission, and a campaign yard can't lay it down. That the designer can edit something doesn't mean the world takes
  it; the check is the world's, on the command (commission, lay down), against the navy's `TechSet` and limits.
- **The sim reads the set too,** for what isn't in a design: a navy's fire-control doctrine, training, signals.

## The pieces and where they live

```
Fleetwright.Shipgen      Design -> Ship (hitboxes, report)                    unchanged
       ▲
Fleetwright.Sim          the world; no GPU, no SDL, no ImGui; deterministic; headless tests and a CLI
  World                  the one root: clock, stores, registries; written only by the sim thread
    Clock                world time as a long (ms since an epoch), the asked speed, the cap for what's live, pause
    Scheduler            the event queue (time, tier, id ordered) and the coarse and fine steppers
    Earth                the sphere maths (double vectors, great circles, local frames), later the Earth model:
                         land, the sea graph for routing, ports; the sun and moon as functions of time
    Navies, Fleets       factions; groups with orders, routines (schedules) and routes (rails tracks)
    Designs              each navy's accepted designs, by content hash -> ShipClass (battle plan), compiled once
    Ships                ShipState stores (SoA): tier, track, condition tables (battle plan)
    Yards                construction as events: laid down, launched, completed
    Encounters           the bubbles: fidelity (Full / Resolved), control, interest; their battle state
    Relations            peace / tension / war per pair of nations; neutrals
    Pictures             each navy's knowledge: contacts, dispatches
    Merchants            light entities on rails (route, nation, tonnage, cargo); full state only in an encounter
    Admiralties          NPC command sources (AI), reading their navy's picture
    Tech                 per navy, the TechSet (unlocked ids); a mission's preset or the campaign's research
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

This replaces the battle plan's open question 2 (a `Fleetwright.Battle` project): the battle is the fine tier of
`Fleetwright.Sim`, and `ShipClass` / `ShipState` live there. The list above is what the world holds. How the code is
layered inside the project (`Core`, `Ships`, `Combat`, then the world on top, one direction of dependency) is in
`docs/battle/plan.md` "Inside `Fleetwright.Sim`".

### The sim thread and the boundary

- **The world runs on a thread of its own** (with the job system's workers under it), not in the frame loop. The
  speed and pause are the world's own state, changed by commands; the host (the frame loop, a test, a server) only
  reports how much real time passed, and the sim makes its target from that (capped, so a slow sim drops the
  achieved speed instead of building a backlog) and advances toward it as fast as it can and never past it. The
  world doesn't know how it's used (`docs/battle/plan.md` step 1.1). A heavy battle tick doesn't stutter the UI, the designer stays at 60 fps over a running world, and
  at full speed the sim uses all the time the frame doesn't.
- **Commands in.** The UI never writes the world. It enqueues commands; the sim applies them at the start of its next
  step and stamps them with that tick. The command log plus the start state is a replay; it's also the seam a
  multiplayer game would use, if ever.
- **A snapshot out.** After a step the sim fills a preallocated snapshot (triple-buffered, swapped by pointer, no
  allocation): per ship what's drawn (track parameters or pose, bearings, damage looks), per shell its few numbers,
  the encounters, the clock, and events for the UI (a contact, a ship completed). 2,000 ships is about 100-200 KB.
  Renderers read only the snapshot, never `World`.
- **Rails ships draw exactly, not interpolated:** the snapshot carries their track, and the renderer evaluates it at
  the render time, as it does shells. Ships in the fine tier are carried on from their last tick along their arc
  (`docs/battle/plan.md` step 2.2).
- **Deep looks (a ship's damage tables in a report screen)** go through a watch list: the UI says which ships it's
  showing, and the sim copies their full state into the snapshot. Nothing on the UI side holds a reference into the
  world.
- **Threaded throughout (agreed):** the sim thread from the start, and the job system (later, in the battle module's
  scale step) under it
  for everything that's per ship, per group or per encounter: rails events in a batch, contact scans, the resolved
  encounters, the fine tier's models. Work is split so results don't depend on the thread count.
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
- **For now the world is a tab (the user, 2026-10-12):** the first tab on `SceneSwitcher`'s top bar and the default,
  so the game opens on the world, with the designer and the viewers beside it. The app owns the world and its sim
  thread, so switching tabs doesn't stop it. The stack described here comes in under the world tab when a mission
  first pushes the designer (`docs/battle/plan.md` step 6.2).

### Time, in numbers

- World time is a `long` of milliseconds since an epoch (1850-01-01 UTC, say): exact, ordered, no drift over a
  century of game; ticks are whole milliseconds (50 ms in the fine tier). Converted to seconds as double only inside models.
- 30,000x at 60 fps: 8 game minutes a frame. 2,000 groups with a waypoint every few hours is ~4,000 events per real
  second, plus the contact scans: well under a millisecond.
- 1,000,000x at 60 fps (peacetime, routines): ~4.6 game hours a frame, 11.6 game days a real second. Routines raise
  no events inside their programs, so the cost is the changes of routine, refits, launches and research: a few
  hundred events a real second, and no contact scan at peace. On rails at this speed the same 2,000 groups would be
  ~140,000 waypoint events a second; that's why peace is routines, not rails.
- A battle at 20 Hz with a 4 ms tick runs at up to ~12x; analytic shells and arc motion may let the fine tick
  grow to 0.25-1 s, which multiplies that. Decide in the fine tier's step, by checking results don't change with
  the tick.
- **Models in the fine tier run at their own rates,** on whole multiples of the tick (from `SimSettings`): pose
  every tick, sighting and fire-control solutions and flooding at about 1 Hz, shells and damage on events. That's
  what keeps a few hundred ships at Full inside a tick (`docs/battle/plan.md` "How many ships").

### Saves and determinism

- A save is the world at a tick: the stores, the event queue, the tracks, the commands not yet applied, and the
  designs (as JSON, with their content hashes; classes are recompiled on load and checked against the hash). The
  Earth model and shipgen versions go in the header.
- Determinism within one build and platform: fixed order of ticks and events, seeded RNG streams per system, no
  wall-clock reads in the sim, parallel work reduced in a fixed order. A test runs a year on rails and a battle twice,
  on 1 and on N threads, and compares the snapshots bit for bit.

## Build order

The battle module comes first, and `docs/battle/plan.md` "Building it from the ground up" is its step order: a world
of water, then ships that move (time compression and the job system included), seeing, fighting, command, and
missions. It replaces the step list this doc had (2026-10-11), and the missions plan's stages are steps in it.

After the battle module, the world grows around the ship simulation. Each item is its own plan when it comes:

- **Resolved fights** (out of the battle module, decided 2026-10-12): doctrine manoeuvre, sighting and
  expected hits per coarse step, sampled hits through the real hit query and damage, promotion and demotion, and the
  calibration test against Full.
- **The world's merchant traffic:** light entities on voyages between ports, diversion orders, full state in an
  encounter. This needs the Earth model's ports and sea graph.
- **NPC admiralties** as command sources, reading their navies' pictures.
- **Relations beyond war and peace,** then diplomacy.
- **Yards and manufacturing:** construction as events, ships laid down from submitted designs.
- **Research:** the `TechSet` growing over a campaign.
- **Peacetime routines** ("Peacetime: routines, above rails"): schedules for groups and merchant services, the
  routine cap, putting everything on rails when peace ends. Comes with the campaign; the battle module's missions
  start at war.
- **Budgets and politics:** the money a navy gets, and the player pressuring the politicians for more; with research
  and the designer, the main peacetime game.
- **Submarines,** before the campaign (decided 2026-10-12): its era (about 1890-1925) has the U-boat war.
- **Aviation** is version 2.0, after the campaign (the user, 2026-10-12).

The Earth model (land, the sea graph and routing, ports, the globe's tiles) isn't on this list. It comes when the user
brings it, inside the battle module if it's ready by then.

## Decisions (the user's, 2026-10-11)

1. **Threaded throughout:** the sim on its own thread from the start, a job system under it (its own step).
2. **The project is `Fleetwright.Sim`.**
3. **Time:** ms since 1850, 50 ms fine ticks; the rendering interpolates for display.
4. **The encounter range (~50 km) and scan interval (10 min)** are starting values, in the central `SimSettings`
   with every other tuning number; no magic values in the code.
5. **What drops the speed is user-configurable** (the interest table in "Conflicts").
6. **The clock while the designer is open is user-configurable;** dropping to 1x, which at these scales is as good as
   paused, is one choice.
7. **Two fidelities, Full and Resolved,** with Resolved calibrated on real game simulations at Full. The player marks
   own forces important or not, and an unimportant one is resolved.
8. **Merchants are real light tracks,** all ~9,000 of them: a position on a route between ports, no fuel or crew.
   Real positions let raiders deplete an area realistically and let navies route merchants away.
9. **Fog of war is knowledge of entities** (each navy's picture), not a visual effect.
10. **Diplomacy waits:** war and peace only for now. The battle module comes first, built to these decisions so that
    later work refactors as little as possible.
11. **Peacetime runs on routines (2026-10-12):** 30,000x is the wartime speed. In peace, fleets are on automated
    routines (schedules above rails), the clock goes up to ~1,000,000x, and the game is research, design and
    pressuring politicians for money. Tension or war drops it back. The player can drop to real time and take a
    group off its routine at any moment, and resume it, without a pause (speed and tier are separate). An "insane
    captain" option lets the player fire at peace, which is an incident that starts a war and its politics.

## What the ship simulation must already respect

The battle module (`docs/battle/plan.md`) comes first. So that the world can grow around it without a rewrite, the
ship simulation keeps to these from its first step:

- **Positions and time are the world's:** Earth-centred doubles, `long` ms world time. No battle-local origin or
  clock, no "the battle started at t = 0".
- **An encounter is a set of entities, not a scene:** it holds ship ids (which may join and leave), its fidelity and
  its control. Nothing assumes two sides, a fixed list of ships or a fight that begins and ends with the program.
  It can be opened by a contact or by a command, and it records who fired first and what each hit did, for whatever
  reads the result (a mission's objectives now, relations and politics later).
- **Full state is made and dropped per entity:** `ShipState` can be created for a ship already at sea (a merchant
  engaged, a ship joining) and a light entity can come back from it. Spawning isn't only "at the start of a battle".
- **Damage takes a hit, not a shell flight:** the hit query and damage model take a hit (a point, a direction, a
  shell type and its striking velocity) from whatever made it, the fine tier's shells or the resolver's sample. Shell
  flight is one source of hits.
- **Sighting writes to a navy's picture,** never to a global "visible" flag; fire control aims at a contact from the
  picture, not at the entity's true position.
- **Orders come in as commands;** the battle has no input handling of its own and reads no UI state. A captain (AI)
  and the player issue the same commands.
- **Every tuning number is in `SimSettings`.** Every random draw comes from a seeded stream named for its system.
- **Nothing assumes the first cut's era:** sensors other than the eye, weapons other than guns, torpedoes and mines,
  entities above or below the surface, and ranges far beyond gun range all come later, through the same pictures,
  hits, tracks and settings.
- **The step takes a `dt` from the scheduler** and allocates nothing; the battle never assumes it owns the clock or
  the thread.
