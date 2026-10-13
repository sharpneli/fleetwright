# Plan: fighting (battle module step 4)

2026-10-13. Status: **agreed with the user 2026-10-13** ("Decisions" at the end, folded into the steps), and iterated
as its steps are built. **4.0, 4.1, 4.2a and 4.2b done 2026-10-13; 4.2c, ordering fire, is next.** It details step 4 of `docs/battle/plan.md` ("Fighting"), keeping its numbers (other docs cite
4.1, 4.3, 4.6, 4.7 and 4.9) and their subjects, and adds 4.0 (the weapon facts) and 4.10 (scale).

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
| `Sim.Combat` | The pure models: `Ballistics` (tables), `HitQuery`, `FireControl` (measure, track, solve, spot), `Terminal`, `Effects`, `Functions`, `Flooding`; `CombatStore` (each ship's mounts and condition, beside `ShipStore`) |
| `Sim` (world) | The systems: `GunnerySystem` (batteries' fire control and firing), `ShellSystem` (shells in flight, splashes), `DamageSystem` (hits in, condition, flooding, losses); `CombatLog` (the record) |
| `Sim.Ai` | Fire orders: captains pick targets (fire at will), admirals distribute fire |
| the exe | Shells, splashes and hits drawn; turrets on their bearings; the weapons and condition rows; the damage report; damage on the sprites |
| `shipgen` CLI | The benches: `ballistics`, `hits`, `duel`; `shell-test --path` |

## One index of where things are

**The world keeps one acceleration structure for "what is near here"** (decision 11): the cube grid in Earth-centred
space the contacts got in 2.7c. It's brought up to date once, by its owner, and every system queries it: contacts and
encounters now, then the ships near a falling shell (4.2a), torpedoes' runs and collisions (4.6), and whatever comes
after. Systems that keep a grid of their own today (the encounters' cube sort, the sightings' grids in each
encounter's plane) move onto it where it fits, when a step touches them. 4.2a, its first new user, starts by checking
the grid's shape and when it's updated (it must hold every ship that can be hit, on any clock tier).

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
updates this doc and `plan.md`. **The order** (decision 8: shells first, baby steps): 4.0, 4.1, 4.2a, 4.2b, 4.2c,
4.3a, 4.3b, 4.4 (the first full pass, ending in the first playable fight), then 4.6, 4.7, 4.8, 4.9 and 4.10. **4.5
isn't a step in that sequence:** it's the list of deepenings, taken one at a time after the first pass, in whatever
order playing asks for.

**Each piece arrives with the step that first uses it** (the review, decision 12): a contract, a system, a column or a
command is made where something runs and is tested with it, never ahead of it as an empty shell.

### 4.0 The weapon facts

The ship data fighting needs, and nothing that runs yet (decision 12): the rest of the scaffold arrives with the step
that first uses it.

- **Weapon facts in `ShipClass`:**
  - `ShellTypeRow`: calibre, mass, muzzle velocity, form factor (drag), kind (AP, common or SAP, HE), filler mass,
    fuze (base with a delay, or nose), a quality row (an index into a settings table: duds, breaking up). Shell types
    are shared by name across classes, so one gun's table is built once.
  - `BatteryRow`: a battery's mounts, its directors, its fire-control level, and the shell types it carries with the
    mix of each. **v0 fires one kind per battery** (decision 12): AP from the main battery, HE (or common) from the
    secondaries. Choosing the shell by target is a deepening (4.5).
  - On each mount: reload time per gun, training and elevating rates, the highest elevation (the gun's range).
  - The plating (`HullPlates`) compiled: side plating amidships and at the ends, decks, bulkheads, superstructure.
  - **Placeholders** for all of it, from calibre, calibre length and the look's era, fitted to the research's gun
    tables (`fire_control_ref.py` `Gun`, `gunfire_ref.py` `Shell`): shell mass ~ d³, muzzle velocity by calibre
    length, reload and training by calibre (the research's mount-rate table), the shell mix by battery and era.
  - **The fire-control level is the design's** (decision 6): a choice in ship design, per battery. Until the designer
    offers it, a placeholder table reads it from today's fit (directors, rangefinder base, computer and radar
    tonnage; `fire-control-designer.md` §3.3). A design with no directors and no rangefinders is legal and must
    fight: every mount in local control, ranges by eye.
- **What comes later, and where:**
  - `Path` and the one tracer: 4.1;
  - shell records, `ShellSystem`, splashes, the dev `FireAt`, the stream `fire`: 4.2a;
  - `GunnerySystem`, the mounts' state in `CombatStore`, bearings in the snapshot, `CombatLog`'s first entries, the
    rails rule for engaged ships: 4.2b;
  - the fire orders: 4.2c;
  - `Hit`, the condition in `CombatStore`, `DamageSystem`, the stream `damage`: 4.3a;
  - fire, flooding, loss, the rails rule for wet and burning ships: 4.3b.
- **To look at:** each class's weapons printed by the CLI (a `weapons` listing beside `trials`), so the placeholders
  can be checked by eye against the research's gun tables.
- **Tests:**
  - every golden compiles with weapon facts, and every mount is in a battery with a shell type;
  - the placeholders for the research's guns (12"/45, 13.5"/45, 15"/42, 4"/45) land within a stated band of their
    sourced shell mass and muzzle velocity;
  - an eyeball-only design compiles, every battery at local control.

**Done 2026-10-13.** `ShipClass` has `Batteries`, `ShellTypes` (with ids), the `ShellLoads` pool and `Plating`, and
each `MountRow` its `Battery`, `ReloadS`, `TrainDegS`, `ElevateDegS` and `MaxElevationDeg`. The placeholders are in
`DesignerPlaceholders` (`Shell`, `ShellMix`, `Fires`, `Gun`, `FireControl`), read from the look's era and navy (`Era`
in `Kinds.cs`). `shipgen weapons DESIGN... [--research]` lists a class's batteries, shells and plating, and puts the
placeholders beside the research's sixteen guns. `WeaponFactsTests` holds the tests above, and a few more.

What it settled, beyond the plan:

- **A battery is the mounts of one kind (main or secondary) that fire one gun** (calibre and length): one range table,
  one set of shells, one fall of shot. KGV's quad and twin 14" turrets are one battery, Bismarck's 150s and 105s two.
  Its directors are the ship's directors of its kind, longest rangefinder first. Torpedo mounts aren't in a battery
  (4.6), and AA isn't (aircraft are out).
- **The shell kind goes by the gun's size, not the battery's kind:** 8" and up carry mostly AP and fire it, lighter
  guns mostly HE (common before 1906). A destroyer's main 4" fires as a battleship's secondary 4" does.
- **Shell quality is an enum named after the research's outcome rows** (`rn1916`, `km1916`, `rn1918`, `ww2`,
  `ijn_t91`). The numbers come with the terminal model, in 4.3a's settings. Victorian shells read as the worst row.
- **The fire-control level is the research's ladder, 0 to 8**, read from the fit (directors, the best rangefinder,
  computer and radar tonnage) and capped by the era's best (1 before 1906, 3 in the Great War, 4 between the wars, 6
  in the Second). Local control isn't a level: it's a battery without a director, at level 0.
- **For 4.2b:** the Jutland's ships read as levels 0 (destroyers, local control), 1 (the pre-dreadnoughts), 2 (the
  light and armoured cruisers: under 1 t of computer, a clock) and 3 (the capital ships). So 4.2b's level table needs
  row 2 as well as 0, 1, 3 and L; it's in the research (`LEVELS`).
- **Shell type ids** are the gun, the kind, the era and the quality ("305/45 AP great_war rn1916"). A placeholder is a
  pure function, so one id is one row in every class (a test checks it over the fleet), and 4.2a's tables can key on
  it.
- **The placeholders against the research** (`weapons --research`): the Great War's guns within 10% in shell mass
  and muzzle velocity. The 1930s super-heavy shells come out ~11% light (16"), the 38 cm and 46 cm ~10% heavy, and
  the 5"/38's short barrel ~12% slow. Good enough for placeholders; the designer should give the real ones.

### 4.1 The hit query

As plan.md has it: a segment, Earth-centred, into each candidate ship's frame (pose, plus sinkage, trim and heel from
the condition, all 0 until 4.3), broadphase on the ship's radius. Then the turrets at their bearings, the
superstructure and other parts, the hull, and inside it the armour and the cells.

- **It returns a `Path`** (the contract arrives here): the crossings along the segment in order, in a fixed buffer.
  Each crossing has:
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

**Done 2026-10-13.** The contract is `HitPath` (`Sim.Ships`; "Path" would collide with `System.IO.Path`): a fixed
buffer of `Crossing`s in order along the segment. Each crossing has where it is (T, the point), what it is (plate, part
in or out, hull in or out, cell in or out, water in or out), which plate (side armour, armour deck, grating, armour
bulkhead, barbette, turret face, side, rear or roof, part armour or plating, hull side, end or deck, deck or bulkhead
between cells), its thickness, material and normal.

- **The one tracer:** `ShipTracer`, in `Sim.Ships` because `shell-test` must call it too. `ShipSolids` (replacing
  `ArmourSolids`) makes every component a solid: a turret in turret space, so it turns with its bearing; a barbette's
  tube; prisms, cylinders, capsules (shafts, tubes) and boxes. It also finds the cells by section.
- **The hit query:** `HitQuery` in `Sim.Combat`. It brings the world segment into the ship's frame with its
  `Attitude` (sinkage, trim, heel about the centre of flotation), checks the broadphase radius and traces. A miss's
  `Fall` says where it met the sea and how far short, over or beside the hull's waterline it fell.
- **The walk** starts at the segment's end and steps toward its start at 5 cm. That puts its samples where
  `shell-test`'s always were, so the refactor could be checked shot by shot (`shell-test --dump`, every shot's
  outcome).
- **Water is a crossing, not a stop** (the user: shells travel a bit underwater, which is why belts go below the
  waterline). A segment already in the sea where the walk begins gets its `WaterIn` out there, or at T = 0 if it
  starts in the sea. How far a shell gets underwater is the terminal model's (4.3a, diving shells in 4.5; the
  research: 1-4 m deep, 30-50% of its velocity, `08-gunfire-effects.md` §3.8).
- **`shell-test` is a caller now.** It reads the path forward from the gun: the first armour or water stops a shell,
  and the last open barbette top or uptake before the target is what let a leak in. `--underwater M` lets shells go
  on under water to M m deep (default 0, the old rule). `--path N` prints the first N shots' paths.
- **Against the recorded results** (70 designs × 20,000 shots, with and without gratings): the leak set is identical
  with gratings. Without gratings it's identical too, but 7 leaks now name the uptake they came down (their target
  lies in an uptake's last 2 cm, which the old walk, starting 5 cm back, never sampled), and 3 supercarrier shots are
  stopped by its armoured flight deck, which overhangs the hull beyond the old walk's box. The other ~14,500
  differences are labels on shots stopped twice: Armour or Water, now whichever comes first from the gun.
- **The numbers** (Release): 22.5 µs a query on Dreadnought, ~10 crossings each, so a Jutland's 8,000 heavy rounds
  take 0.18 s in all. Against `HullField`'s exact ray: 5,527 of 5,537 hull crossings found by both (the walk steps
  over slivers under 5 cm), all within 1 cm. Nothing allocates.
- **To look at:** in the hitbox tab, "click fires a ray" (or shift+click; `-hitray` starts with it on) traces a ray
  from the eye. The ray and its crossings are drawn over the view (armour red, the hull's skin orange, parts blue,
  the sea cyan), and the path is listed in the panel.

**Settled with the user (2026-10-13, after 4.1):**

- **A barbette with 0 mm armour is a hole** in the armour deck it stands on or passes through: a leak, to be fixed in
  the designer. The tracer pierces the deck inside any barbette's bore; `shell-test` names it ("down unarmoured S4S
  barbette"). Lord Nelson (143 of 20,000 shots) and Bouvet (78) leak through their secondaries' 0 mm barbettes, as
  the plan's "Needed from the designer for fighting" lists.
- **Shells under water:** `shell-test` gives the track, and the damage model decides what happens on it and whether
  some part stops the shell. A better model of shells slowing in the water comes later (with diving shells, 4.5).
  With `--underwater 4` nearly every design leaks under its belt (Bismarck's `belt_depth_m` is 2.0, and its leaks
  come in 2-3.4 m down): that's the track, not the verdict.

### 4.2 Guns and shells (missions stage 3)

**4.2a Ballistics and shells in flight.**

- **Trajectory tables per shell type,** built once and shared (`BallisticsRegistry`, read-only after building, so the
  renderer reads the same tables). They map elevation × time to the ground arc, the height over the sea, speed, path
  angle, and the integral the Coriolis term needs (plan.md "Shells"). Point mass, `Cd(Mach)` and the standard
  atmosphere, on the sphere, as `fire_control_ref.py` `_fly`. The tables' resolution is set by their error against
  direct integration.
- **The range table** comes from it: range ↔ elevation, time of flight, angle of fall and striking velocity, for fire
  control (4.2b) and the terminal model (4.3a).
- **Shell records** (`ShellSystem`, registered here): fire time, muzzle position, azimuth, the elevation and muzzle
  velocity it left with, the type, and the salvo it belongs to. They make the events above ("A salvo's life"). The
  ships near the fall come from the world's one location index ("One index of where things are"). Draws come from
  the stream `fire`, keyed per salvo and shell (counter-based, so they don't depend on order or threads).
- **Splashes:** a short-lived ring of (point, time, shell, salvo), for spotting and the screen.
- **Drawn:** each shell evaluated from its table at the frame's time (smooth at any rate, nothing in the snapshot but
  its record), splashes as columns that rise and fall, a short tracer at the ship zoom band.
- **Dev:** the strip's "fire at" (a right-click on the sea) and `-fire=ship:lat,lon`: a battery fires at a point, so
  shells and splashes can be seen before there's any fire control.
- **Tests:**
  - **tick independence:** with the targets on rails or scripted tracks (so only the shell model is compared: ships
    stepped on the fine tick differ by metres between tick lengths), splashes and hits are the same at a 0.1 s and a
    5 s tick;
  - **the table:** within a metre of direct integration at the fall;
  - **the rotating sphere:** a shell along a meridian and one along the equator land where direct integration on
    the rotating sphere says (plan.md);
  - **the research's guns:** times of flight and angles of fall within a few per cent of `fire-control-research.md`
    §5.1 (16"/50: 13.2 s at 10 kyd ... 50.3 s at 30 kyd);
  - no allocation per shell.

**4.2a done 2026-10-13.**

- **The one grid:** `CubeIndex` (`Sim.Core`), lifted from the contacts unchanged (the Jutland's and ladder-2's hashes
  after an hour are identical). The contacts' grids index units on rails by their anchors, a cube as wide as they
  reach before the next scan: another question from "where is every ship now". So the world's `ShipIndex` is a second
  instance of the same structure. It holds every live ship on any tier, and is built only when a query comes (a world
  with no fighting never pays for it) and again when it's over `ShipIndexMaxAgeMs` old or the ships have changed.
- **Ballistics** (`Sim.Combat/Ballistics.cs`):
  - **The model:** `_fly` on the sphere, inverse-square gravity, in the flight's plane.
  - **The tables** are keyed by the ballistic numbers (calibre, mass, velocity, form factor, the gun's highest
    elevation), not the shell type's id, so all of a gun's Great War shells share one. Each covers elevation (0.5°
    from -3°) × muzzle velocity (0.96, 1, 1.04: the ship's own motion) × air density (0.95, 1, 1.05) × time (1 s).
    A lookup is Hermite in time, a cubic in elevation and quadratics across, about 1.8 µs.
- **Two things plan.md's "Shells" didn't have:**
  - **The density dimension.** A muzzle 25 m up meets 0.28% thinner air the whole way, worth 35 m at long range,
    so a muzzle h0 high reads the table at density e^(-h0/9000). The same dimension will take a day's weather.
  - **Coriolis with drag's damping.** The plain integral of the displacement missed by 6 m of the turning's 38 on a
    37 s flight: drag brakes a deflection's velocity as it brakes the shell. The table carries K = ∫e^-C ∫e^C v (C
    the drag's integral), and the deflection is still one cross product. Across the track that's exact. Along it,
    where drag brakes harder and a lifted shell meets thinner air, it's within ~3% of the turning's effect (4.9 m of
    149 m firing east on the equator). The full variational equations would close that for ~50% more memory: a
    deepening if fire control ever cares.
- **Shells** (`Sim/ShellSystem.cs`, system 3):
  - **Records** are pooled, with two events each: the window (where `ShellWindowTopM` is crossed) and the end.
  - **At the window,** the track to the water and `ShellUnderwaterM` on under it, in four chords, goes through
    `HitQuery` against the ships the index finds. A ship on the fine tick is carried on along its course for the
    window's fraction of a second; one on rails or coarse is where its own function puts it.
  - **At the end,** a splash or a hit goes into a ring the snapshot carries for `SplashLifeMs`.
  - **Tables** are built when a class first goes to sea (`ShipSystem.ClassAdded`), not at its first salvo.
- **The dev's "fire at":** the strip's Fire row (battery, "at (right-click)") and `-fire=ship[:battery]:lat,lon`.
  Each shell's error is drawn from the stream "fire" by salvo and shell (`DevFirePatternPct`, a placeholder until
  4.2b's chain).
- **Drawn:** each shell from its table at the frame's time, a dot and a short tracer. Splashes are rings that widen
  and fade; hits are orange.
- **The bench:** `shipgen ballistics DESIGN [--research]` prints each battery's range table, the research's 16"/50
  beside its sourced figures.
- **The numbers:**
  - **Against direct integration** at the fall: 0.14 m, 0.16 m in other air; the muzzle-height model 2.1 m from a
    true flight from 5-25 m up.
  - **The 16"/50:** times of flight within 2% and falls within 1°, as the research's own model (§5.1).
  - **The turning sphere:** 0.5 m north along a meridian, 1.8 m west at 60° N, 4.9 m east on the equator.
  - **Ticks:** 40 splashes and 23 hits identical at a 0.1 s and a 5 s tick.
  - **Size and build:** a 12" Great War table is 786 KB, built in 35 ms. A Jutland's guns are 24 tables, 16.5 MB,
    built as the classes go to sea (placing its 250 ships still takes ~0.4 s).
- **For 4.2b:** `ElevationFor` bisects through falls (~10 ms): fine for the dev's fire, too slow for 500 batteries a
  second. Fire control will want a range table per key, computed once from the tables.

**4.2b Mounts, fire control and spotting.** The port of `fire_control_ref.py` `engage()`, cut to its spine.

- **Mounts:** train toward the ordered bearing at their rate, within their arcs; elevate; load on their clocks. A mount
  bears when the target is in its arcs and it's trained within `LayToleranceDeg`. Their state is the first of each
  ship's combat columns (`CombatStore`, `Sim.Combat`): the bearing and elevation anchors, the reload clock and the
  rounds left, allocated at spawn and sized by the class (plan.md: "per-ship arrays are allocated once at spawn"),
  reached only through `ShipState`'s index accessors. Bearings go into the snapshot, so the sprites' turrets turn.
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
- **`CombatLog`** starts here: the architecture's "who fired first and what each hit did". A ring of plain structs per
  world: time, kind (fired, hit, splash near; damage's kinds join in 4.3), shooter, target, shell type, the part, the
  outcome, the consequence. Encounters point into it; missions read it later, the damage report in 4.4.
- **The rails rule** (`ShipSystem.Tiers.cs`, the one place): a ship with a battery engaged isn't `Placeable`.
- **Tests:**
  - **the calibration** (`shipgen duel`, 4.2b's bench): a steady battleship target, 8 × 15", level 3, at 16 kyd hits
    within a band around the research's 7.8% (table 11, steady), and the level ladder keeps its order;
  - **a Jutland-like pair** (13.5" and 12", level 3, ~15 kyd) within ×2 of the record;
  - replays give the same hash; no allocation; the fire control's cost per battery a second.

**4.2b done 2026-10-13.**

- **The range table** (`BallisticTable`): each elevation row's falls by speed and muzzle height (every 5 m to 60 m),
  found once on the flights, so `Fall` is a lookup (a cubic in elevation, quadratics across) and `ElevationFor` a
  regula falsi on it: ~5 µs from scratch, ~1 µs from last second's answer (it was ~10 ms). Within 0.9 m of the flights'
  own falls from 2 km; point-blank, where a shell all but level falls as √h, up to ~50 m in range, under half a metre of
  height at a target. `Descends` (a shell's window and end) starts from the table's time of flight and finishes with
  Newton's steps (the height is concave in time, so from the late side they can't jump to the climb).
- **One polar-fix model:** `Cov4` moved to Core with `Track`, a constant-velocity Kalman track on the sphere and a range
  and bearing as a fix. The pictures and fire control both use it (the pictures' hashes unchanged).
- **The mounts** (`CombatStore`, `Sim.Combat`, owned by the `ShipStore`, made at spawn): each mount's train and
  elevation as analytic anchors inside its traverse (shipgen's one non-wrapping interval: a bearing outside it parks the
  mount at the nearer end, and it doesn't fire), its loading clock and rounds. `ShipState.MountBearingDeg` reads them;
  the snapshot carries the bearings of ships whose mounts have moved, and the sprites' turrets turn
  (`ShipArt.ClassMounts` matches the sprite's mounts to the class's by component id).
- **`GunnerySystem`** (world system 4; the AI is 5 now), `engage()`'s spine, one cause of a miss a stage:
  - **measure:** the battery's directors' eyes as rangefinders (σ = δ·R²/(B·M), the level's magnification and rate, a
    bias each drawn at the start); without a director, the highest mount's layer by eye (σ 20%); a reading reaches the
    computer after the level's latency;
  - **track:** a `Track` started from the navy's picture (its velocity no surer than the level's first guess), fixed by
    the readings, the level's q;
  - **solve:** the target where it'll be after the time of flight (iterated through the range table, from last
    second's), the gun laid by `FireControl.Solve` with its ship's motion in (trained off the line by the motion across
    it, the elevation whose flight at the speed and angle the two give falls at the range), every second on a grid;
  - **lay, fire:** a salvo at the first instant the bearing mounts are loaded and laid; the level's salvo-to-salvo
    error times battle's 1.5, the director's common laying error (each gun's for levels 0-2 and local control, as
    dispersion), the engagement's residual biases, the own-turn kick while swinging; each shell laid for its own point
    (the gun's pattern by guns in the salvo, `FireControl.Dispersion`);
  - **spot:** when the salvo is down and 6 s on, if the highest eye sees the target: over, short or straddle with
    σ = k·R²/h, the deflection to a mil, the RN ladder against the correction the salvo was fired with.
  - Engaged ships stay off rails (`ShipSystem.Tiers.cs`, `Engaged`); batteries are silent off the fine tick
    (`BatteryState.Silent`); hits are counted and logged, nothing more (4.3a).
- **Settings:** the level table, rows 0-8 from `LEVELS` (the plan's 0, 1, 2, 3 and the rest, cheap as data; their
  trackers, radars and stabilisation are 4.5's), crew qualities (green, average, trained, elite: the rangetakers' and
  layers' errors), and the gunnery numbers. Crews per side: `GunnerySystem.SetCrew`, and a scenario's `"crews"`.
- **The gun's pattern** is a placeholder on `BatteryRow` (`DesignerPlaceholders.Pattern`: the Great War's heavy guns
  3.3·(305 mm/d)³ %, 12" 3.3, 13.5" 2.5, 15" 1.7; lighter 2.5; later 1.8-1.9).
- **`CombatLog`:** a ring of plain lines (fired, hit, straddle) in the world, hashed; the snapshot carries the newest 64.
- **On the map (dev, until 4.2c's orders):** the strip's fire row picks a battery or all; "engage" then a right-click
  on a ship or a contact engages it, "cease" stops; each engaged battery's state, level, salvos and hits; a line from
  each engaged ship to its target. `-engage=ship[:battery]:target`.
- **The bench:** `shipgen duel SHOOTER [TARGET] [--range M] [--level L] [--runs N] [--trace] ...`; `--trace` prints
  each salvo's fall against the target, the track's aim error and the correction it was fired with.
- **The numbers**, against `fire_control_ref.py` run on the same cases in the same clear weather (no haze, smoke or
  splash confusion, which the sim doesn't have yet):

  | Case (combat 1.5, steady target) | the reference | the sim |
  |---|---|---|
  | 8 × 15"/42 at level 3, 16 kyd, a QE-size target | 5.0% | 4.7% (QE on QE) |
  | the same on a 250 m battleship | 6.8% | 5.8% (on a 270 m one) |
  | levels 0, 1, 2 (the reference ×0.73 for the target's size) | 0.9, 2.9, 3.6% | 1.1, 2.8, 3.1% |
  | straddles at levels 0, 1, 3 | 16, 44, 24% | 17, 40, 23% |
  | Lion-like 13.5" on a battlecruiser, 15 km | 4.7% | 4.5% |
  | Seydlitz-like 11" on a battlecruiser, 15 km | 5.5% | 4.6% (the record 4.0%) |

  The British record at Jutland (1.4%) came in haze and smoke, with splashes confused and both sides zigzagging: the
  reference reaches it (2.7%) only with those conditions, which come with 4.5's deepenings (smoke, weather, splash
  confusion). `GunneryTests` keeps the ladder's order, level 3 at 2.5-8%, both pairs within ×2 (the German of its
  record), turrets that bear and those that don't, the rails and coarse rules, a replay, and no allocation.
- **The cost:** ~10 µs of the sim thread a battery a game second, half of it the shells' launches (Release, `duel`). A
  Jutland's ~500 engaged batteries would take ~5 ms a game second, ~5% of a core at 10x: more than the plan's guess of
  1 µs, inside 2.7's budget; 4.10 measures it at scale.
- **Found on the way:** the hit query's `HitPath` grew its per-part room on its first near miss (1.7 KB, once per
  bigger class); the shell system now makes the room when a class first goes to sea.

**Settled beyond the plan (to confirm with the user):**

- **Hashes moved:** the gunnery system takes id 4, so the AI is 5 and every world's hash changed (proved: with gunnery
  unregistered, ladder-1, ladder-2 and the Jutland hash as before). New baselines after an hour: ladder-1 `C48F8760`,
  ladder-2 `2C890F64`, Jutland `736454C1`.
- **One rangefinder a director:** the designs have one director with one rangefinder (QE's 15 ft); the reference's
  level 3 averages three (two 9 ft, one 15 ft), which is most of the remaining gap. Turret rangefinders aren't in the
  data; the designer could give them.
- **Spotting by eye at 15-16 kyd is mostly noise,** as in the reference: σ = 4e-5·1.5·R²/h is 400-500 m from a 25-35 m
  top, so most salvos read "straddle" and the ladder rarely moves. Faithful to the reference; tall spotting tops matter.
- **The Earth's turning isn't in the solution** (the era's computers left it out); the spot takes it off with the rest
  of the bias (up to ~150 m at long range).
- **The track starts from the picture** (as planned), so a well-watched contact is acquired with its course and speed
  already good; the level's first guess only floors the velocity's uncertainty.

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
- **A minimal fighting manoeuvre** (decision 12), so the AI doesn't just steam past: a captain's (or a group's
  admiral's, steering the guide) **Fight** order closes to a range band, holds it on a course that keeps the main
  battery bearing, and turns away when outgunned (v0: the heavy guns its navy sees against its own, by a ratio in
  settings). It's a unit order in the same vocabulary; a scenario gives it as an AI side's standing order when an
  encounter opens, and the player can give it too. Real tactics (crossing the T, torpedo attacks, breaking off at
  x% damage) are 5.1's.
- **On the world tab:**
  - the strip's weapons row: fire at will, hold fire, engage (a click on a contact);
  - per battery: target, state (no target, tracking, firing, not bearing, local control), salvos and hits;
  - target lines from the ship to what it engages;
  - the selected ship's shells and splashes.
- **In the coarse tier guns are silent until 4.9** (decision 4): a fight at 240x and up doesn't fire, and the clock
  panel says so. Encounters pause the clock at 1x by default, so the first fights are seen at 1x. That's a gameplay
  rule relaxed for a few steps and restored in 4.9 (end-goal rules).
- **Playable:** two squadrons close and engage each other, turrets train, salvos straddle, hits flash.

### 4.3 Damage v0

The first pass of the damage model (decision 4 of plan.md: start simple, area by area later; it will change a lot
once systems and flooding interact, so it's written to be changed), in two sessions (decision 12): 4.3a makes hits
matter, 4.3b makes ships burn, flood and go down.

**4.3a What a hit does.** The research's four steps per hit (`08-gunfire-effects.md` §0: where it lands, what plates
it meets, whether and where the fuze fires, what is within reach of the burst):

- **Arriving here:** the `Hit` contract (the ship, the time, the point and direction in the ship's frame, the striking
  velocity, the source: shell, torpedo, mine, collision or a coarse sample; the shell type; who fired), the
  architecture's "damage takes a hit, not a shell flight"; `DamageSystem`; the stream `damage`, keyed per hit; and
  each ship's **condition** in `CombatStore`:
  - per component: a state (ok, damaged, disabled, destroyed), flags (jammed, burning);
  - per cell: water (m³), breach area (m²), fire;
  - per ship: crew lost, sinkage, trim and heel, a status (afloat, going down, gone). There are no hit points
    anywhere.

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
    plate (recorded now; water comes in through it from 4.3b);
  - **fire:** a chance by shell kind and the era's fire load (the fire is set now; it burns from 4.3b);
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
- **The bench:** `shipgen hits <design> --shell <type> --range <m> --n 2000` (a port of `gunfire_ref.py` `mc()`):
  given hits on a design, spread over its silhouette as the fall of shot would put them. It reports outcome classes,
  parts lost and crippled shares now, water taken and lost shares from 4.3b.
- **Tests** (anchors with wide bands, `08-gunfire-effects.md` §7.4 put into the era):
  - a 12" AP mostly passes through a destroyer, and HE bursts;
  - ten 4" HE hits usually cripple a destroyer;
  - a battleship under medium HE loses its upper works and fire control, and keeps its speed and main turrets;
  - heavy AP at long range seldom gets through the belt, and turrets jam;
  - replays hold, and there's no allocation per hit.
- **Playable:** hits knock out guns, directors, speed and steering; a ship circles with its rudder jammed.

**4.3b Fire, flooding and going down.**

- **Fire v0:** a burning cell or part is damaged over time, spreads to a neighbour with a chance a minute, and is put
  out with a chance a minute by crew. Fire next to a magazine rolls for cooking off. That's all for now.
- **FLOODING v0** (plan.md's first version):
  - **inflow** into a breached cell is `Cd·A·√(2g·head)`, the head from the cell's depth under the present waterline,
    up to its volume × permeability;
  - **spread** over the cell graph (decision 12; without it one holed cell is too small against a big ship's reserve
    buoyancy, and only magazines would ever sink one): water flows freely across `Open` joins, leaks slowly across
    bulkheads and decks (`FloodLeak*` rates in settings), and floods fast through a deck that has sunk below the
    outside waterline (`damage-model-research.md` §3.6, the Lützow rule). No pumps or counter-flooding yet;
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
- **The rails rule** (`ShipSystem.Tiers.cs`): a ship with water in it or a fire isn't `Placeable`.
- **Tests** (anchors with wide bands, `damage-model-research.md` §11 and `08-gunfire-effects.md` §7.4):
  - ten 4" HE hits cripple a destroyer more often than they sink it, and a battleship under medium HE stays afloat;
  - a waterline hole amidships takes hundreds of tonnes and lists the ship a few degrees;
  - a hole in an end floods along it and trims the ship; a deck pushed under the waterline floods fast;
  - a destroyer with two or three machinery spaces flooded founders; a capital ship lives through 1,000-3,000 t;
  - flooding at a 1 s and a 5 s step agrees within a few per cent;
  - replays hold, and there's no allocation per flooding step.
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
4.3b's 1 s flooding runs on wet ships in any tier (correct, and slower at speed).

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
- **`shipgen hits <design> --shell T --range R --n N`:** given hits, the damage model alone (4.3a, 4.3b).
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

- **Shells** (in since 4.0): mass, muzzle velocity, drag form factor, kinds carried and the mix, filler, fuze, per
  gun, and a navy's shell quality by year. Today they come from the calibre, the calibre length and the look's era
  and navy; the look's era is a paint scheme, not a year, so a design drawn in the wrong era gets the wrong guns.
- **Working the guns** (in since 4.0): reload time, training and elevating rates, highest elevation, per mount type.
  Shipgen draws some Great War light guns in a dual-purpose shape; the placeholders ignore it before the treaty
  years.
- **Fire control** (in since 4.0): the level per battery from its fit (`fire-control-designer.md` §3), until the
  designer emits it.
- **A gun's dispersion** (in since 4.2b): its full-salvo pattern, % of range, from the calibre and era until the
  designer gives it from the gun and mount (wear, delay coils, mixed rifling).
- **Rangefinders** (4.2b): today one per director, the director's own; turret and spotting-top rangefinders (and their
  magnification) would give a battery several readings, as the era's ships had.
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
11. **One acceleration structure for location queries:** the contacts' cube grid becomes the world's one index of
    where things are, updated once and queried by every system that asks "what is near here".
12. **The plan review's fixes are in:** 4.0 is data only (each piece arrives with its first user); water spreads over
    the cell graph in the first flooding model; a minimal Fight manoeuvre for the AI in 4.2c; 4.3 split into 4.3a and
    4.3b; one shell kind per battery in v0; the tick-independence test runs its targets on rails or scripted tracks.
