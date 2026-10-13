# Plan: fighting (battle module step 4)

2026-10-13. Status: **agreed with the user 2026-10-13** ("Decisions" at the end, folded into the steps), and iterated
as its steps are built. It details step 4 of `docs/battle/plan.md` ("Fighting"), keeping its numbers (other docs cite
4.1, 4.3, 4.6, 4.7 and 4.9) and their subjects, and adds 4.0 (the scaffold) and 4.10 (scale).

## The aim

**Every link from seeing a target to sinking it is in place and works, each one simple.** Fire control is
rudimentary, the damage model is coarse and the flooding is linear, but each is a piece with its own inputs and
outputs. Later sessions deepen one piece at a time (a better tracker, penetration proper, a power network, free
surface) without touching the others.

**At the end it's playable:** two navies' ships, under the AI or the player, fire at each other, straddle, hit,
lose guns and speed, burn, flood, list, blow up and sink. It's not yet as realistic as the game will be, but every
number that will make it so already has a place to go.

What the user said (2026-10-13): the scaffolding matters most. Fire control will be rudimentary and later expanded,
the same for the damage model and the kinds of shell; as long as the pieces are in place, each can be iterated alone.

## The principle: links, contracts, one model behind each

The chain is a pipeline of **links**. Each link has:

- **a contract:** the plain structs it takes and gives (a `Hit`, a `Path`, a `Solution`), allocation-free, indices
  not strings;
- **one model behind it now, "v0":** the simplest version that behaves the right way, usually a short port of the
  research's reference code;
- **its numbers in `SimSettings`**, and the ship facts it needs from the class, with **flagged placeholders** for
  what the designer doesn't give yet (`DesignerPlaceholders.cs`, listed under "Needed from the designer");
- **a bench and a few anchors:** a headless way to measure the link alone (below, "The instruments"), and kept
  tests with wide bands taken from the research.

Deepening a link means replacing the model behind its contract. The contract may grow a field; its callers don't
change. A link is a file or class boundary with a struct contract, not an interface for its own sake: the hot paths
stay static and allocation-free. A C# interface appears only where two models must live side by side at runtime (the
fire-control trackers by era, later).

**Visuals stay light** (decision 10). What's drawn in step 4 keeps the style of the map so far (sprites, icons,
lines, markers), enough to see and read what the sim does, and no session gets stuck on effects: a separate VFX pass
comes far later, once the gameplay systems are in. Looks read the sim's state and never feed it, so that pass can
replace any of them without touching the sim.

## The chain

```
the navy's picture (3.2) ─► TARGETING   who fires at what: the player's and the AI's orders (4.2c)
                                │
the director's eye ────────► MEASURE    readings of the engaged target: range and bearing, with errors
                                │
                             TRACK      the target's course and speed as the fire control holds them
                                │
                             SOLVE      where it'll be after the time of flight; elevation and train; biases, spots
                                │
the mounts ────────────────► LAY, FIRE  salvo: one common error, plus each shell's dispersion
                                │
                             FLIGHT     each shell a track on its trajectory table; an event at its danger window
                                │
                             HIT QUERY  the shell's last metres against the ships there, each in its own frame
                         miss ──┤
                     SPLASH ─► SPOT ─► (back to SOLVE: the fall of shot corrects the next salvo)
                                │ hit
                             TERMINAL   penetration, fuze, where it bursts: the outcome class
                                │
                             EFFECTS    parts wrecked, holes, fires, crew
                                │
                             CONDITION  the ship's state: parts, cells, crew
                                │
                             FUNCTIONS  what the ship can still do: steam, steer, fire, see, command
                                │
                             FLOODING   water in, settle, trim, heel; sink or capsize
                                │
                             LOSS       sunk, dropped, recorded

torpedoes (4.6), mines (4.7), collisions (4.6) and coarse fire's sampled hits (4.9) enter at TERMINAL as more Hits
```

Each box is a link. The research's five causes of a miss (`fire-control-research.md` §0: range measurement, rate
error, ballistic bias, salvo-to-salvo error, dispersion) are each one line of v0, in MEASURE, TRACK, SOLVE and LAY.
That's on purpose: each era fixes a different cause, and later iterations deepen one cause at a time.

## What there is to build on (checked 2026-10-13)

- **`ShipClass`** (`Fleetwright.Sim.Ships`):
  - **mounts:** calibre and length, barrels, rest angle, arcs, traverse, turret armour (face, side, rear, roof), the
    magazine they draw from, the barbette, rounds, ready rounds, torpedoes and warheads;
  - **directors:** battery, rangefinder base, radar;
  - **eyes** with blind sectors, and the **silhouette** (3.2a);
  - **the hull and its insides:** cells (volume, permeability, belt, TDS depth, citadel, crew, the armour above,
    neighbours by bulkhead or deck), rooms (13 kinds: magazine, boiler and engine rooms, steering, bunkers, TDS...;
    links to the mounts, tubes, shafts and rudders they serve), bulkheads, decks;
  - **the links damage needs:** funnels to their boiler rooms, shaft lines to their engine rooms, rudders to the
    steering room;
  - **armour and form:** the armour report and `ArmourSolids`, `HullField`, the full-load `Hydrostatics`, and
    `MotionFacts` (GM, roll period).
- **`ShellTest`**: a proven tracer through the armour (5 cm steps, bisected), 4.1's starting point.
- **Pictures** (3.2d): a constant-velocity Kalman filter per contact (`Cov4`), and fixes from eyes with their
  errors (a rangefinder by its base). **Encounters** (3.1b) as sets of ships, and the **interest table** for clock
  drops.
- **The chain of command** (2.6): one order path for the player and the AI, captains and admirals as layers.
- **The snapshot and the renderer:** turret instances per (ship, mount), drawn at their rest angles until bearings
  arrive.
- **The clock tiers:** a 50 ms fine tick and a one-minute coarse step (2.8).

What's missing, so 4.0 adds it or flags it:

- **No shell facts:** shipgen gives a gun its calibre, calibre length, barrels and rounds. There's no shell mass,
  muzzle velocity, kind of shell, filler or fuze.
- **Nothing on working the guns:** no reload time, training or elevating rate, or highest elevation.
- **No fire-control level:** the design's fire control is per battery (directors, rangefinder base, computer and
  radar tonnage), not a level.
- **The plating isn't compiled:** the hitboxes export it (`HullPlates`: shell plating amidships and at the ends,
  decks, bulkheads, superstructure), but `ShipClass` doesn't take it yet. Fuze arming against thin hulls needs it.
- **No propellant or service networks:** no propellant kind or sensitivity per magazine, and no generators,
  switchboards or networks (that's the systems iteration in 4.5, from `06-service-networks.md`).

## Where it lives

As the layers already are (plan.md "Inside `Fleetwright.Sim`"), models in `Combat`, systems in the world:

| Layer | What's new |
|---|---|
| `Sim.Ships` | Weapon facts: `ShellTypeRow`, `BatteryRow`, the mounts' working numbers, the plating; their placeholders |
| `Sim.Combat` | The pure models: `Ballistics` (tables), `HitQuery`, `FireControl` (measure, track, solve, spot), `Terminal`, `Effects`, `Functions`, `Flooding`; `DamageStore` (each ship's condition, beside `ShipStore`) |
| `Sim` (world) | The systems: `GunnerySystem` (batteries' fire control and firing), `ShellSystem` (shells in flight, splashes), `DamageSystem` (hits in, condition, flooding, losses); `CombatLog` (the record) |
| `Sim.Ai` | Fire orders: captains pick targets (fire at will), admirals distribute fire |
| the exe | Shells, splashes and hits drawn; turrets on their bearings; the weapons and condition rows; the damage report; damage on the sprites |
| `shipgen` CLI | The benches: `ballistics`, `hits`, `duel`; `shell-test --path` |

## A salvo's life, in events

How the links run in time, so nothing runs per tick that doesn't need to:

1. **A battery's fire-control event,** every `FireControlMs` (1 s) on a grid of world time, only for engaged
   batteries of ships in the fine tier: measure, track, solve. The solution gives the mounts their ordered bearing
   and elevation. **Mounts move analytically:** each holds (when, from, toward, rate), so its bearing at any instant
   is closed form, as rails are, and nothing steps per tick.
2. **A salvo event** at the moment the guns that will fire are loaded and on: the first instant both hold, solved
   from the mounts' anchors and reload clocks, scheduled by the fire-control event. It makes one shell record per
   gun, each with its errors drawn, sets the reload clocks, and writes "fired" to the log.
3. **A shell's window event:** at firing, each shell's water impact and its danger window (the last part of the
   flight below the tallest ship, `ShellWindowTopM`) are solved from its table, and one event is scheduled. Until
   then the shell costs nothing.
4. **At the window,** the hit query sweeps the shell's last metres against the ships near it, each in its frame at
   those instants. A hit goes straight to the damage system; a miss leaves a splash.
5. **A spot event** once a salvo's last shell has landed (plus `SpotDelayMs`): the spotter judges the fall of shot,
   and the correction goes into the next solve.
6. **Damage at the hit,** at once: terminal, effects, condition, functions. **Flooding** runs every `FloodMs` (1 s)
   only on ships with water in them, fires likewise on burning ships.

Shells are tier-independent: one fired in the fine tier lands where it lands, whatever the clock does next. Shells,
splashes and log entries live in preallocated pools, grown only past their size (as ships' columns are).

## The steps

Each step is a session (or a few), starts with the usual investigation, ends with something to run and look at, and
updates this doc and `plan.md`. **The order** (decision 8: shells first, baby steps): 4.0, 4.1, 4.2a, 4.2b, 4.2c, 4.3, 4.4 (the first full pass, ending
in the first playable fight), then 4.6, 4.7, 4.8, 4.9 and 4.10. **4.5 isn't a step in that sequence:** it's the list
of deepenings, taken one at a time after the first pass, in whatever order playing asks for.

### 4.0 The scaffold

The contracts, the ship facts and the plumbing, with nothing firing yet.

- **Weapon facts in `ShipClass`:**
  - `ShellTypeRow`: calibre, mass, muzzle velocity, form factor (drag), kind (AP, common or SAP, HE), filler mass,
    fuze (base with a delay, or nose), a quality row (an index into a settings table: duds, breaking up). Shell types
    are shared by name across classes, so one gun's table is built once.
  - `BatteryRow`: a battery's mounts, its directors, its fire-control level, and the shell types it carries with the
    mix of each.
  - On each mount: reload time per gun, training and elevating rates, the highest elevation (the gun's range).
  - The plating (`HullPlates`) compiled: side plating amidships and at the ends, decks, bulkheads, superstructure.
  - **Placeholders** for all of it, from calibre, calibre length and the look's era, fitted to the research's gun
    tables (`fire_control_ref.py` `Gun`, `gunfire_ref.py` `Shell`): shell mass ~ d³, muzzle velocity by calibre
    length, reload and training by calibre (the research's mount-rate table), the shell mix by battery and era.
  - **The fire-control level is the design's** (decision 6): a choice in ship design, per battery. Until the designer
    offers it, a placeholder table reads it from today's fit (directors, rangefinder base, computer and radar
    tonnage; `fire-control-designer.md` §3.3). A design with no directors and no rangefinders is legal and must
    fight: every mount in local control, ranges by eye.
- **Each ship's condition** (`DamageStore`, `Sim.Combat`), allocated at spawn and sized by its class (plan.md: "per-ship
  arrays are allocated once at spawn"). It's reached only through `ShipState`'s index accessors, never through the
  arrays' layout:
  - per component: a state (ok, damaged, disabled, destroyed), flags (jammed, burning);
  - per mount: the bearing and elevation anchors, the reload clock, rounds left;
  - per cell: water (m³), breach area (m²), fire;
  - per ship: crew lost, sinkage, trim and heel, a status (afloat, going down, gone). There are no hit points
    anywhere.
- **The contracts:** `Hit` (the ship, the time, the point and direction in the ship's frame, the striking velocity,
  the source: shell, torpedo, mine, collision or a coarse sample; the shell type; who fired), `Path` (4.1),
  `Solution` (4.2b), `Outcome` (4.3). `Hit` is the architecture's "damage takes a hit, not a shell flight".
- **The systems registered and the commands defined:**
  - `GunnerySystem`, `ShellSystem` and `DamageSystem`, registered after ships and groups;
  - the commands `Engage`, `HoldFire` and `FireAtWill` (as unit orders, 4.2c), and the dev command `FireAt` (a
    ship's battery at a point);
  - streams `fire` and `damage`, derived per salvo, per shell and per hit: counter-based keys, so draws don't depend
    on order or threads.
- **`CombatLog`:** the architecture's "who fired first and what each hit did". A ring of plain structs per world:
  time, kind (fired, hit, splash near, fire started, flooding, part lost, sunk), shooter, target, shell type, the part,
  the outcome, the consequence. Encounters point into it. Missions read it later, and the damage report reads it now.
- **The snapshot:** each mount's bearing (the sprites' turrets turn), and slots for shells, splashes and each ship's
  condition summary.
- **The rails rule** (`ShipSystem.Tiers.cs`, the one place): a ship with water in it, a fire, or a battery engaged
  isn't `Placeable` and doesn't go on rails.
- **Tests:** every golden compiles with weapon facts, and every mount has a battery and a shell type. The new state
  is in the hash. Spawning a ship with its condition, and dropping it, replays the same.

### 4.1 The hit query

As plan.md has it: a segment, Earth-centred, into each candidate ship's frame (pose, plus sinkage, trim and heel from
the condition, all 0 until 4.3), broadphase on the ship's radius. Then the turrets at their bearings, the
superstructure and other parts, the hull, and inside it the armour and the cells.

- **It returns a `Path`:** the crossings along the segment in order, in a fixed buffer. Each crossing has:
  - where along the segment it is, and the point;
  - what is crossed: a plate (side armour, armour deck, bulkhead, turret face or roof, barbette, hull plating,
    superstructure plating) with its thickness, material and normal;
  - or a part, or a cell, entered or left;
  - or the waterline.

  The first crossing gives the face (side or top) and the obliquity.
- **A miss returns where it met the water,** and how far short of, over or beside the hull it fell. Splashes,
  spotting and later near misses and diving shells read it.
- **v0 is `ShellTest`'s tracer, refactored into the one tracer** (decision 3): generalised from "did it reach the
  citadel" to "everything it crossed", and kept to the ship's box. **There's one implementation:** the hit query is
  the tracer, and `shell-test` and its tests become callers of it, not owners of a tracer of their own. Its results
  are already proven against the old tool shot by shot.
- **Exact crossings** (planes, prisms, tubes, the hull's own ray) are a 4.5 iteration that replaces the stepping
  inside the same tracer. They're checked against the stepping version's recorded results shot by shot, not against
  a second implementation kept alive beside it.
- **To look at:** `shipgen shell-test --path` prints paths. In the hitbox tab, a click fires a ray at the ship and
  draws what it crossed (the debugging view every later damage iteration will want).
- **Tests:**
  - the hull's own ray agrees with `HullField`;
  - one shot at two turret bearings differs where it should;
  - a ship heeled 10° is hit where the heel says;
  - no allocation per query;
  - a Jutland's worth of queries timed.

### 4.2 Guns and shells (missions stage 3)

**4.2a Ballistics and shells in flight.**

- **Trajectory tables per shell type,** built once and shared (`BallisticsRegistry`, read-only after building, so the
  renderer reads the same tables). They map elevation × time to the ground arc, the height over the sea, speed, path
  angle, and the integral the Coriolis term needs (plan.md "Shells"). Point mass, `Cd(Mach)` and the standard
  atmosphere, on the sphere, as `fire_control_ref.py` `_fly`. The tables' resolution is set by their error against
  direct integration.
- **The range table** comes from it: range ↔ elevation, time of flight, angle of fall and striking velocity, for fire
  control (4.2b) and the terminal model (4.3).
- **Shell records** (`ShellSystem`): fire time, muzzle position, azimuth, the elevation and muzzle velocity it left
  with, the type, and the salvo it belongs to. They make the events above ("A salvo's life"). The candidates at the
  window come through the grid the encounters already keep.
- **Splashes:** a short-lived ring of (point, time, shell, salvo), for spotting and the screen.
- **Drawn:** each shell evaluated from its table at the frame's time (smooth at any rate, nothing in the snapshot but
  its record), splashes as columns that rise and fall, a short tracer at the ship zoom band.
- **Dev:** the strip's "fire at" (a right-click on the sea) and `-fire=ship:lat,lon`: a battery fires at a point, so
  shells and splashes can be seen before there's any fire control.
- **Tests:**
  - **tick independence:** splashes and hits are the same at a 0.1 s and a 5 s tick;
  - **the table:** within a metre of direct integration at the fall;
  - **the rotating sphere:** a shell along a meridian and one along the equator land where direct integration on
    the rotating sphere says (plan.md);
  - **the research's guns:** times of flight and angles of fall within a few per cent of `fire-control-research.md`
    §5.1 (16"/50: 13.2 s at 10 kyd ... 50.3 s at 30 kyd);
  - no allocation per shell.

**4.2b Mounts, fire control and spotting.** The port of `fire_control_ref.py` `engage()`, cut to its spine.

- **Mounts:** train toward the ordered bearing at their rate, within their arcs; elevate; load on their clocks. A mount
  bears when the target is in its arcs and it's trained within `LayToleranceDeg`.
- **A battery's fire-control state** (per ship and battery, in `GunnerySystem`): the target contact, the director in
  use, the tracker, the engagement's biases, the spot corrections, the salvo count. **The director in use** is the
  battery's best working director that sees the target (its eye's blind sectors, its line of sight). With none,
  the battery falls back to **local control**: each mount aims for itself, by its layer's eye.
- **v0, one cause of a miss per stage** (the research's five):
  - **MEASURE** (range measurement): every fire-control event, while the navy's picture holds the contact in sight
    and the director sees it, a reading. The range error is `σ = δ·R²/(B·M)` (rangefinder base and magnification,
    δ by crew quality), plus one bias per instrument drawn at the start of the engagement. Without a rangefinder,
    the eye's `σ ≈ 15-20%`. The bearing error is in mil.
  - **TRACK** (rate error): a constant-velocity Kalman filter (`Cov4`, as the pictures use) started from the
    picture's estimate, so acquiring a target costs nothing new, and taking readings from there. Its process noise
    and latency come from the level. v0 has this one tracker for every level. The Dreyer's polar drift and the turn
    models are 4.5's.
  - **SOLVE** (ballistic bias): predict the target at fire time + latency + time of flight (iterated twice through
    the range table). Elevation comes from the range table, plus the engagement's residual bias (a % of range drawn
    once, by level) and the spot correction. Train is the predicted bearing plus the deflection correction.
  - **LAY, FIRE** (salvo error, dispersion): each salvo draws one common error (`salvo_pct` of range in range, mil in
    deflection, by level, times the combat factor 1.5). Each shell then adds its own dispersion (the gun's
    `pattern_pct`). The laying error's base comes by level. Own turns add `own_turn_kick` while the ship swings. Roll
    is 4.5's.
  - **SPOT:** when a salvo has landed, the spotter (the director's eye height) judges it over, short or straddling,
    with `σ = 4e-5·R²/h`, and sees the deflection. The correction follows one rule (the RN ladder: 400 yd until it
    crosses, then halve), applied against the correction that salvo was fired with, so spots don't pile up.
- **The levels:** a small table in settings: 0 (eyeball), 1 (early rangefinder), 3 (Dreyer and director) and L
  (local), with the research's numbers (`fire-control-research.md` §5.3, §12; `FCLevel` in the reference). More levels
  are rows added later.
- **The eyeball ship** (the user's test, once the module is ready: 72 secondary guns, eyeball only, going down in a
  blaze of glory). v0 spots each battery's splashes as if it were alone, which flatters many guns of one calibre
  firing independently. **Kept simple for now: that ship gets the advantage** (decision 9). Whose splash it is
  (`manual-gunnery-research.md` §5) stays on 4.5's list with the rest.
- **Crew quality,** one per side, given by a scenario (green, average, trained, elite; trained by default): it scales
  the rangetaker's δ and the laying error.
- **Hits are counted and shown but do nothing yet** (a flash, a log line), so fire control is calibrated on its own
  before damage is in.
- **Tests:**
  - **the calibration** (`shipgen duel`, 4.2b's bench): a steady battleship target, 8 × 15", level 3, at 16 kyd hits
    within a band around the research's 7.8% (table 11, steady), and the level ladder keeps its order;
  - **a Jutland-like pair** (13.5" and 12", level 3, ~15 kyd) within ×2 of the record;
  - replays give the same hash; no allocation; the fire control's cost per battery a second.

**4.2c Ordering fire (the player and the AI, minimal).** Fire orders are unit orders in 2.6's one path:

- **`Engage(unit, contact)`** puts a ship's batteries on a contact. Given to a group, its admiral distributes fire
  over the enemy's line near that contact: v0 counts from the van, ship k on enemy k. **`HoldFire(unit)`** and
  **`FireAtWill(unit)`** go with it.
- **Fire at will is the default** for every ship, the player's and the AI's (decision 1). The captain picks the
  nearest contact his navy holds in sight in each battery's arcs, and keeps it until it's lost or another is much
  closer (no flicking between targets). Secondaries pick their own, smallest first. Captains are already layers at
  1 Hz; this adds a few comparisons.
- **An unidentified contact is fired on** (decision 1): a ship not yet made out may be an enemy that really is there.
  Richer standing orders (rules of engagement per fleet: hold fire on the unidentified, on neutrals' waters, and so
  on) come later, when there are more sides and they start to matter (5.1).
- **A battery fires only at a contact** its navy's picture holds and its director (or a mount's eye) sees.
- **On the world tab:**
  - the strip's weapons row: fire at will, hold fire, engage (a click on a contact);
  - per battery: target, state (no target, tracking, firing, not bearing, local control), salvos and hits;
  - target lines from the ship to what it engages;
  - the selected ship's shells and splashes.
- **In the coarse tier guns are silent until 4.9** (decision 4): a fight at 240x and up doesn't fire, and the clock
  panel says so. Encounters pause the clock at 1x by default, so the first fights are seen at 1x. That's a gameplay
  rule relaxed for a few steps and restored in 4.9 (end-goal rules).
- **Playable:** two squadrons engage each other, turrets train, salvos straddle, hits flash.

### 4.3 Damage v0: what a hit does, and sinking

The first pass of the damage model (decision 4 of plan.md: start simple, area by area later; it will change a lot
once systems and flooding interact, so it's written to be changed). The research's four steps per hit
(`08-gunfire-effects.md` §0: where it lands, what plates it meets, whether and where the fuze fires, what is within
reach of the burst):

- **TERMINAL** (`Terminal`, a port of `gunfire_ref.py` `resolve()`), walking the `Path`:
  - **penetration:** at each plate, De Marre (`damage-model-research.md` §8.1) with a constant per material, at the
    plate's obliquity; the shell slows by a simple residual-velocity rule;
  - **the fuze:** it arms on plate over ~0.07 calibre (less at obliquity);
  - **HE** bursts at the first plate: inside if it's under 0.2 calibre, outside if not;
  - **AP** is one of four: defeated (stopped at a plate), ricochet (past ~45-65° of obliquity), passed through (never
    armed: "two holes"), or burst after its delay distance along the path; a heavy part in the way (machinery,
    barbette) can arm it;
  - **the quality row** gives duds and shells breaking up.

  The **outcome class** (defeated, ricochet, through, burst inside, burst outside, dud) is the explanation the damage
  report shows.
- **EFFECTS** (`Effects`):
  - **the burst:** parts within the wreck radius (from the filler, cube root) are destroyed or disabled by distance,
    unless armour stronger than its splinters boxes them;
  - **through-shots** damage what's on the line of flight;
  - **holes:** plating crossed near or under the waterline breaches that cell, by a hole radius from the shell and
    plate;
  - **fire:** a chance by shell kind and the era's fire load;
  - **crew:** casualties by the space's crew density and the burst's area;
  - **a heavy hit on a turret or barbette** that doesn't get in can still jam it (~35%, R5);
  - **fire or a burst in a magazine** or its turret's handling path rolls for an explosion (propellant sensitivity ×
    fill: a side's or navy's knob). v0's result is the ship lost at once; the venting race and the break at the
    magazine are 4.5's (`07-magazine-explosions.md`).
- **FUNCTIONS** (`Functions`), recomputed when the condition changes: the research's Model A
  (`06-service-networks.md` §4), direct dependencies over the links the class already has (decision 5). **Built to be
  extended:** the dependencies are data compiled into the class, a table of each function's needs (all of these
  parts, any one of those), evaluated by one generic pass, not `if`s in code. Later tiers add rows and kinds of
  node to the same table: generators, switchboards and the routes of power, hydraulics, steam and data (Model B), and
  the function states the coarse tier samples (Model C). What's needed now:
  - **a mount fires** if it's working, its barbette isn't jammed, its magazine isn't flooded or gone, and it has
    crew;
  - **a battery's fire control** uses its best working director, else local control;
  - **power:** each shaft line works if its engine room, shaft and propeller do; boiler rooms feed through their
    funnels and uptakes. The power fraction caps what the engines give (`ShipMotion` reads a cap from `ShipState`),
    and speed follows from the class's own curve;
  - **steering:** free, jammed at an angle (the helm can't move it: a ship that circles), or lost (no helm);
  - **sight:** an eye works while its part does (the sightings read it);
  - **command:** the bridge and conning tower are recorded now; the delay their loss costs comes with 5.1's signals.

  This is the seam for the service networks in 4.5: they become more needs in the same table, so nothing that reads
  a function changes.
- **Fire v0:** a burning cell or part is damaged over time, spreads to a neighbour with a chance a minute, and is put
  out with a chance a minute by crew. Fire next to a magazine rolls for cooking off. That's all for now.
- **FLOODING v0** (plan.md's first version):
  - **inflow** into a breached cell is `Cd·A·√(2g·head)`, the head from the cell's depth under the present waterline,
    up to its volume × permeability. No spread through bulkheads, no pumps;
  - **settling** is linear from the full-load hydrostatics: sinkage from TPC, trim from the moment over MCT, heel from
    GM_T and the off-centre moment;
  - **going down:** when the water aboard passes the reserve buoyancy it founders, and past `CapsizeHeelDeg` it
    capsizes;
  - it runs every `FloodMs` only on ships with water in them, and in any clock tier until 4.8.

  Ultra simple on purpose (decision 7): the buoyancy model decides when a ship goes, and deepening it (4.5) changes
  when ships go without changing anything else.
- **LOSS, by buoyancy, not hit points** (decision 7):
  - **A ship stays afloat until its buoyancy says otherwise.** One sinking slowly is just a ship settling deeper,
    listing and slowing, perhaps for hours, and still fighting with what works. Nothing marks it "sinking".
  - **The enemy sees only what can be seen:** a contact carries what its observers make out (low in the water, a
    list, trim by the bow or stern, fires, stopped, guns silent), and nothing else. Whether it's going down is the
    observer's judgement, so a navy may keep firing at a ship that's done for, or leave one that would have sunk and
    lives. The AI decides on the same picture.
  - **Its own side knows more:** the damage report has the water aboard and what's left of the reserve buoyancy (how
    much the crew can tell, and the captain's own estimate, can come later).
  - **Going down:** once it founders or capsizes, it goes under over a time from the model (quick for a capsize or a
    magazine, slower for a founder), stops firing and steaming, and leaves its group. A navy learns it's gone when an
    observer sees it go; unseen, its contact is dead-reckoned and forgotten as any other.
  - **Then it's dropped** from the store. The log keeps the truth: what each side claimed and what really sank can
    differ (the debrief's, later).
- **The bench:** `shipgen hits <design> --shell <type> --range <m> --n 2000` (a port of `gunfire_ref.py` `mc()`):
  given hits on a design, spread over its silhouette as the fall of shot would put them. It reports outcome classes,
  parts lost, water taken, crippled and lost shares.
- **Tests** (anchors with wide bands, `08-gunfire-effects.md` §7.4 put into the era):
  - a 12" AP mostly passes through a destroyer, and HE bursts;
  - ten 4" HE hits cripple a destroyer more often than they sink it;
  - a battleship under medium HE is a mission kill, not sunk;
  - heavy AP at long range seldom gets through the belt, and turrets jam;
  - a waterline hole amidships takes hundreds of tonnes and lists the ship a few degrees;
  - flooding at a 1 s and a 5 s step agrees within a few per cent;
  - replays hold, and there's no allocation per hit.
- **Playable: the first blood.** Two ships fight until one sinks.

### 4.4 Damage seen

What plan.md's "Damage on the sprites" section describes, kept light (decision 10), and the report:

- **On the sprites:**
  - sinkage, trim and heel through the height map, so a sinking draws itself;
  - wrecked turrets tinted, and a jammed turret keeping its bearing;
  - overlays for wrecked parts from their hitbox polygons;
  - fires and smoke as simple markers from burning cells and parts, until the smoke field and the VFX pass.
- **On the map:** hit flashes; enemy ships drawn as their contacts are seen (low, listing, burning), own ships as
  they are; a ship seen going under, and a marker where it went; the encounter's losses as each side knows them.
- **The damage report** (`damage-model-research.md` §12: the best model fails without one):
  - the selected ship's log lines, cause to effect ("12" AP · B turret face · defeated · turret jammed in train");
  - the strip's condition row: speed cap, steering, guns working of n, water in tonnes, list, fires, crew lost.
- **Proof:** screenshots by readback: a ship listing, a jammed turret, a stern-first sinking.

**The first pass ends here: the first playable fight.** A ladder scenario (ladder-2, Beatty against Hipper) plays
out at 1x to 100x, under the AI on both sides or the player on one: ships engage, hit, lose guns and speed, flood and
sink, and the log tells the story.

### 4.5 The deepenings (one link at a time, after the first pass)

Each is its own session. It reads its research section, replaces the model behind one link, and shows its bench's
numbers before and after against the anchors. The list, by link, with where the research is:

- **Fire control** (`fire-control-research.md`, `ship-motion/target-relative-speed.md`):
  - the trackers by era: the Dreyer's polar drift, the Argo's true course, the AFCT's turn setting;
  - the clocks' limits (the Dreyer's ±1,200 yd/min);
  - rate error growing with target speed;
  - the full ladder of levels (0 to 8 and L), radar as a sensor, night (no solution without light);
  - splash confusion with several ships on one target, and dyed shells (with whose splash it is, below);
  - the designer emitting levels and sensors (`fire-control-designer.md`).
- **Ship motion** (`ship-motion/ship-motion-gunnery-research.md` §7.7, in its order):
  - a roll phase per ship from `RollPeriodS` and the sea, with firing on the roll and a once-a-roll rate;
  - laying error `√(base² + (τ·ω)²)`;
  - wetness switching off forward mounts, casemates and turret rangefinders;
  - cross-level error from heel;
  - vibration;
  - the sea and swell in the weather.
- **Smoke:** own funnel and gun smoke on the directors' lines of sight. It comes with the smoke field (plan.md, 3.2's
  open items).
- **Evasion** (`evasion-research.md`): chasing salvos and zigzags as captain behaviours; reading the target's aspect.
- **The hit query:** exact crossings, and turret parts and barrels (plan.md 2.1 left them for 4.1).
- **Terminal** (`05-mechanics-and-games.md`, `08-gunfire-effects.md`):
  - penetration proper: material qualities, capped and uncapped shells, spaced armour, edge effects;
  - shell quality by navy and year;
  - diving shells and underwater hits;
  - near-miss splinters;
  - a ricochet table.
- **Systems** (`06-service-networks.md` Model B): power, hydraulics, steam and data as routes through the cells with
  fault states (tripped, severed, flooded), casualty power, damage-control parties and repair; Model C for the coarse
  tier. Needs the designer to emit the networks.
- **Flooding** (plan.md's old 4.5, `damage-model-research.md` §3.5, §8.4):
  - leaks through bulkheads and decks, and above versus below the armour deck (Lützow);
  - free surface;
  - `sinking.py`'s non-linear settling;
  - pumping and counter-flooding, and speed driving water in;
  - a plunge or capsize that plays out;
  - the hull girder (broken backs).
- **Magazines** (`07-magazine-explosions.md`): the venting race, the flash chain by doors, propellant by navy,
  flooding a magazine as an order, the hull broken at the magazine.
- **Fire:** spread on the cell graph and the vents, fire load by era, cook-off timers, fires lighting the ship at night.
- **Crew:** casualties by space, crew as the damage-control resource, the command node (a bridge hit delays orders).
- **Night:** searchlights, star shell, gun flashes and fires as light for the sighting model.
- **Whose splash it is** (`manual-gunnery-research.md` §5): identification falls as 1/(1 + 0.6·N) with N guns of one
  calibre firing independently; salvo firing fixes it. Until then many guns spotting alone are flattered (decision 9).

### 4.6 Torpedoes and collisions

- **Torpedoes as tracks:** a straight run at a set speed and depth for its range (placeholders by calibre and era:
  speed and range settings, the warhead from the export), with events at its end. Along the run it's checked against
  the ships near it in pieces of `TorpedoCheckMs`: each piece a swept segment against each hull's box in its frame,
  so a hit doesn't depend on the tick.
- **Firing:** above-water mounts (already mounts with arcs) and submerged tubes (`TubeRow`). The aim-off comes from
  the battery's estimate of the target's course and speed, with a spread of n torpedoes.
- **Captains:** a destroyer's v0 attack is close to range, fire, turn away. Flotilla attacks are 5.1's.
- **The hit** is a `Hit` from a torpedo: an underwater explosion at a hull point. The TDS (`CellRow.TdsM`) absorbs by
  its depth, the hole is sized by the charge, the cells there flood, and machinery in the cell can trip from shock.
- **Collisions and ramming:** a hull-against-hull test (both `HullField`s in their frames) for ships closer than their
  half-lengths together, through the grid. v0 breaches the struck ship at the stem's contact point, damages the
  rammer's bow, and costs both their speed by the closing energy.

### 4.7 Mines

As plan.md has it: static fields with a density, a depth and an owner, laid by a mission or by minelayers, thinned by
sweepers; crossing a field is an event in every clock tier, solved when a leg is anchored; a navy knows its own fields
and learns others'. The hit is a `Hit` from a mine, as a torpedo's.

### 4.8 Damage in the coarse tier

Flooding, fire and repair at the coarse step, written to hold at a minute (inflow integrated over the step in closed
form, not stepped), so a ship still flooding or burning goes on at speed and sinks there if it must. Until this step,
4.3's 1 s flooding runs on wet ships in any tier (correct, and slower at speed).

### 4.9 Coarse fire

As plan.md has it (agreed 2026-10-13): at the coarse step, expected hits from the range, the fire-control level, the
guns, and the target's size and aspect (`quick_hit_probability` and the MPI-by-salvo table, `fire-control-research.md`
§8.3, §12.2), sampled from the seeded stream, with no shells in flight. Each hit goes through 4.1 and 4.3 like a fine
one. Misses can be sampled as splashes for the screen. Guns fire at any speed from here on.

- **The tables come from the fine chain** (decision 2): the fine fire is run headless many times over (`shipgen duel`
  across ranges, levels, guns, targets and their manoeuvres) to build coarse fire's tables, rather than taking the
  research's tables as they are. The research's numbers are the cross-check.
- **It's calibrated against the fine fire:** the same scenarios run many times both ways, comparing hits, sinkings and
  duration, a kept test.
- **To work out at the step's start: evasion in coarse.** Coarse motion is kinematic arcs a minute long, so a ship's
  zigzags, chased salvos and steady turns aren't in its track the way the fine fire sees them. How evasive action
  enters the coarse tables (an order's style as an input to the table, the coarse track's own turns, or both) is open.
- **A fight at high compression gets a background rumble** (decision 4): some sound for a fight going on at speed, so
  the player knows of it without seeing salvos. Its design is open (with the UI, 5.2).

### 4.10 Scale: a Jutland that fights

The Jutland fights at 10x within the sim thread's quarter (plan.md 2.7's target, "with headroom for what 3 and 4
add"):

- **the expected load:** ~8,000 heavy rounds, a peak of 1,000-2,000 shells in flight, thousands of hits, ~500 engaged
  batteries at 1 Hz;
- **measured** on the ladder, small first, both builds (`docs/battle/plan.md` 2.7c's way);
- **fixed** only where the numbers ask.

## The instruments

Every iteration is measured the same way, so "better" is a number:

- **`shipgen ballistics <design>`:** each gun's range table: range, elevation, time of flight, angle of fall, striking
  velocity, against the research's anchors.
- **`shipgen hits <design> --shell T --range R --n N`:** given hits, the damage model alone (4.3).
- **`shipgen duel <a> <b> --range R --level L --runs N`:** two ships headless, the fire-control chain with or without
  damage. It reports hit %, straddle %, time to first hit, salvos to straddle, and time to sink.
- **The probes** (2.7b): each new system timed per game minute. `shipgen scenario` prints them.
- **The Python references stay the references:** each port is checked against its reference on a few points
  (as `sighting_ref.py` holds `SightModel`).

## Performance, as planned

From plan.md's "How many ships", for a Jutland at 10x:

| Model | When it runs | Rough cost |
|---|---|---|
| Fire control | each engaged battery, 1 Hz | a Kalman update and a few table lookups: ~1 µs; 500 batteries ≈ 0.5 ms a game second |
| Mounts | no per-tick work: analytic anchors | nothing between events |
| Shells | one event at the window, one hit query | the broadphase finds 0-1 ships near the fall; ~2 µs |
| Damage | at a hit | the path's plates and the burst's parts: tens of µs |
| Flooding and fire | 1 Hz, only on wet or burning ships | per cell with water: µs |

No allocation in any of it: shells, splashes, log lines and fire-control states are pooled. Condition arrays are
allocated at spawn.

## Needed from the designer (for fighting)

Placeholders in `DesignerPlaceholders.cs` until shipgen gives them, listed in plan.md's "Needed from the designer":

- **Shells:** mass, muzzle velocity, kinds carried and the mix, filler, fuze, per gun.
- **Working the guns:** reload time, training and elevating rates, highest elevation, per mount type.
- **Fire control:** the level per battery from its fit (`fire-control-designer.md` §3), until the designer emits it.
- **Magazines:** the propellant kind and its sensitivity (a navy's and an era's).
- **Torpedoes:** speed and range settings per calibre and era (the warhead is exported already).
- **For 4.5's systems:** generators, switchboards and the routes of power, hydraulics and data
  (`06-service-networks.md` §3.3).

## Questions to settle (for the user)

None open. New ones go here as the steps raise them.

## Decisions

The user's, 2026-10-13:

1. **Fire at will is the default** for now, for the player's ships and the AI's. More complex standing orders for
   fleets come later; they start to matter when there are more sides. An unidentified target is fired on: it may be
   a real enemy where none was expected (Japanese torpedo boats in the English Channel: maybe this time they really are
   there).
2. **The fine tier gets the full fire-control chain** (measure, track, solve, lay, spot). It's then simulated
   at length to build coarse fire's tables (4.9). How evasive action works in coarse is still to be worked out.
3. **The hit query is `ShellTest`'s tracer, refactored into the one tracer;** `shell-test` and the tests use it.
   **Single implementation is an important principle:** no second tracer kept beside it, even as a reference.
4. **Guns are silent in the coarse tier until 4.9.** Some rumbling background sound for fights at high time
   compression is to be worked out.
5. **Damage to systems starts with direct dependencies** (Model A). More tiers come later as they're needed, so the
   system is built to be extended.
6. **The fire-control level is the design's fit,** a choice in ship design. (The user's plan once the module is ready:
   a manual, eyeball-only ship of 72 secondary guns, going down in a blaze of glory.)
7. **Sinking depends on the buoyancy model, not hit points.** A ship sinking slowly should leave the enemy unsure
   whether it's going down. The first step may use an ultra simple model.
8. **Shells first,** torpedoes after. Baby steps.

The user's, 2026-10-13, for the implementation:

9. **Keep fire control simple now:** the 72-gun eyeball ship gets its advantage (no splash identification yet).
10. **Don't get stuck on visual effects:** a separate VFX pass comes far in the future, once the gameplay systems are
    in place. The style of the map so far is right and stays; this is a warning against spending time on looks, not
    a call to pare them down.
