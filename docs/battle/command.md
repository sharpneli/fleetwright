# Plan: command (battle module step 5)

2026-10-14. Status: **agreed with the user 2026-10-14** ("Decisions" at the end, folded into the text), and iterated
as its steps are built. It details step 5 of
`docs/battle/plan.md` ("Command"), keeping its numbers (other docs cite 5.1 for signals, rules of engagement, flotilla
attacks, breaking off and evasion, and 5.2 for the command UI) and splitting 5.1 into sessions. Each step starts with
the usual investigation, and this doc is updated as they're built.

## The aim

**At the end of step 5, a scenario plays itself.** With no player, both sides under the AI, the Jutland (or
Tsushima, or a generated mission later) runs from its first minute to an end:

- the scouts search, find the enemy and report;
- the commanders concentrate, close or evade, as their aims ask;
- the fleets deploy and fight, the flotillas attack and the lines turn away from the torpedoes;
- cripples fall out, the beaten break off, and at night or at the end each side goes home;
- the scenario says when it's over, and what happened: who lost what, who got where.

Headless, `shipgen scenario jutland --play` runs it to its end and prints the timeline and the result, over several
seeds to show the spread. In the app the same run can be watched, or one side commanded by the player while the AI
commands everything the player doesn't take.

**"Theoretically" self-playing:** it plays to an end without a human, using every order and every system the module
has, not that it plays well. Making the AI play well (good admirals, cunning ones, a navy's own doctrine) is a
fine-tune pass of its own, later, like fighting's 4.5. What step 5 owes is the scaffolding: every decision has a place
to live, its inputs and outputs, and a simple first rule behind it, so each can be improved alone.

This is also what the missions need (`docs/missions/plan.md`): "an enemy that wants something", and the generator's
"checked headless, AI against AI". Step 5 is where that becomes possible; step 6 adds the mission file, objectives with
weights, the UI and the debrief.

## What there is to build on (checked 2026-10-14)

- **The chain of command** (2.3b, 2.4, 2.6, `Fleetwright.Sim.Ai`): `AiSystem` holds the layers top-down, each thinking
  at its own rate only while it has something to watch. Captains (one per ordered ship, the only ones who work the
  helm and the guns) and admirals (one per group, at every level of nesting) are built. One way in: `AiCommand` unit
  orders, the player's logged, a layer's issued unlogged.
- **The vocabulary so far:** `Course`, `Speed`, `Hold`, route (`ClearRoute`, `AddWaypoint`, `LoopRoute`), `Follow`,
  `Station`, `Run`, `Shadow`, `Fight`, `Standing`, the fire orders (`FireAtWill`, `HoldFire`, `Engage`), and the
  chain's own (`SpeedLimit`, `Clearance`, `Detach`, `Resume`).
- **Who has a unit:** the player's order holds it (nudged, or pinned by `Detach`); a superior's order for a held unit
  raises a notice; a route's end hands it back. All of it at every level.
- **Groups** (`GroupSystem`): squadrons of ships, forces of groups, four formations (line ahead, line abreast,
  echelon to either side), one geometry (`LineOffsets`) that the captains, the admirals and the rails read.
- **Fighting behaviours** (4.2c, 4.6): fire at will, the admirals' fire distribution over the enemy's line, `Fight`
  (close to a band, hold it broadside, turn away when outgunned by heavy guns), the torpedo craft's attack, torpedoes
  at will, and a scenario's standing order to Fight on meeting the enemy.
- **Search patterns** (2.5, `SearchPatterns`): ladder, expanding square, sector, patrol line, as pure geometry. Only
  the map and the dev flags use them.
- **Knowledge** (3.2): each navy's picture, with the class as taken for (and mistaken), estimates with their
  uncertainty, and what the enemy can be seen to suffer (`SeenFlags`: low, listing, by the bow or stern, burning,
  stopped, silent). Own ships are known exactly.
- **A ship's condition** (4.3): parts, water, attitude, power (`PowerCap`), steering, mounts and directors working,
  fires, crew lost.
- **Encounters** (3.1b) as sets of ships, the player's interest table, and `Light` (the sun and the moon).
- **Scenarios** (`Fleetwright.Sim.Missions`): forces as a tree of units, a start, sides at war, the player's side,
  routes, and `"fight"` per side. `shipgen scenario` runs one headless and prints the fight.

**What's missing for a scenario to play itself:**

- **Nobody wants anything.** Ships steam their start courses until a standing order meets an enemy. The Jutland and
  ladder-2 don't fight in their first hours (4.2c): nothing turns Beatty at 14:32.
- **Nothing is above the top units.** A side's forces don't know of each other: the Battlecruiser Fleet and the Grand
  Fleet are strangers, so nobody concentrates them or sends one to support the other.
- **The standing order is one bit** (Fight, or nothing). There's no doctrine: when to fight, how close, when to break
  off, what to fire on.
- **The AI doesn't react** to damage (it fights to the end), to torpedoes (the `flotilla`'s line steamed on), to
  night, or to being outnumbered as a fleet (only `Fight`'s per-unit comparison).
- **No fleet tactics:** no deployment from cruising order into a line of battle, no turn together, no battle turn-away,
  no screens or scouts, no coordinated flotilla attack.
- **Orders are instant.** Signals are part of 5.1 in plan.md's era list.
- **No end and no result.** A scenario runs until it's stopped.
- **No home.** Nobody has anywhere to go when it's over.

## The principle: intent down, reports up, doctrine around

Taken from the command UI research (`docs/battle/command-ui.md`, "Command hierarchy and delegation"), which is about
the player but holds for the AI just as well:

- **Each layer gets intent and gives orders.** An order from above is a *task* (what to do), with its limits; the
  layer turns it into orders for the layer below, and never orders up or sideways (as 2.6 has it). A side's
  commander gets *aims*; a force's admiral gets a task (search, intercept, cover); a squadron's rear admiral gets a
  manoeuvre or a station; a captain gets a behaviour.
- **Each layer reports its state up:** what it's doing and why, as a small code (`Status`: on task, in contact,
  engaged, attacking, falling out, retiring, done, can't). The superior reads his units' states to decide; the UI
  shows the same codes as reason tags ("subordinates explain themselves"). Own reports are instant for now (3.2's
  decision 1: a navy knows at once).
- **Doctrine is set once and inherited.** A record of standing rules (when to fight, how close, when to break off,
  what to fire on, how to use torpedoes) per side, overridden per unit, read down the tree. The player's units and
  the AI's read the same doctrine.
- **Every new order is a unit order.** Tasks are more kinds of the one `AiCommand`, given to a unit (a ship or a
  group), through the one way in. The player can give any of them at any level, as the AI does.
- **Rules with hysteresis, not search.** Each decision is a rule over the layer's picture and its units' states, with
  a margin either way so it doesn't flick (as fire at will's reach does). Where there's a real choice among a few
  options (which wing to deploy on, which enemy group to go for), a pure scoring function picks, tested alone. No
  planning by simulating ahead.
- **Nothing is idle without a reason.** Every task ends (done, failed, or by a timeout) and hands the unit back up;
  a commander with an idle unit gives it something to do, or sends it home. That's what keeps a self-playing scenario
  from stalling.
- **The usual rules:** deterministic, all state hashed, draws from named streams, every number in `SimSettings`,
  nothing O(n²) (enemy groups through the grid, no pairs of units), nothing allocated per think. Higher layers think
  more slowly, and on events (a contact, a loss, a task done, a signal received).

## The chain

```
SIDE COMMANDER     the side's aims ─► tasks for its top units          on events, and every CommanderPeriodMs (60 s)
  (new: Commanders/)  reads: the picture, his units' states, the aims, home
      │ task
FORCE ADMIRAL      task ─► the approach, deployment, tasks to its groups (scouts, screens), battle manoeuvres
  (AdmiralLayer)      reads: the tactical picture (enemy groups and strength)     every ForcePeriodMs (10 s)
      │ task, station, manoeuvre
REAR ADMIRAL       formation, turns, fire distribution, the flotilla's attack           every 1 s (as now)
  (AdmiralLayer)
      │ station, behaviour, fire order
CAPTAIN            ship handling, evasion, guns, torpedoes, falling out, going home      every 1 s (as now)
  (CaptainLayer)
      │ helm, engines, batteries, launchers
SHIP
```

The force admiral and the rear admiral stay one `AdmiralLayer` (2.6b): a group with groups thinks as a force, a group
of ships as a squadron, and both read the same doctrine. The new layer is the side commander's, a third `IAiLayer`
above the admirals: the "admiralties" folder 2.3b planned, in its battle form. In the campaign the admiralty grows
above it (strategy, daily); in a mission the commander is the C-in-C on the scene.

## The pieces

### Tasks: the vocabulary grows

Each is a kind of `AiCommand`, to a ship or a group, by the player or a superior. Each turns into orders the chain
already has, and each says how it ends.

| Task | What the unit does | Turns into | Ends |
|---|---|---|---|
| **GoTo** (a point, by a time or at a speed) | Steams there | A route (straight legs; the slide keeps it off land) | Arrived |
| **Search** (an area) | Searches it for the enemy | A pattern (`SearchPatterns`), spaced by the visibility and the number of ships; a group of scouts spreads abreast into a scouting line | Contact, or the pattern's done |
| **Patrol** (a line) | Steams up and down it | A looping route | Never (until re-tasked) |
| **Scout** (ahead of a unit) | Spreads ahead of it on a bearing, in sight of each other | A station far ahead of the unit's guide, re-laid as its course changes | Re-tasked |
| **Screen** (a unit, a sector) | Keeps between it and the threat, close | A station on the unit at a bearing from the threat | Re-tasked |
| **Intercept** (a contact) | Steers to meet it | The lead course on the picture's estimate, re-solved as it updates | In contact (then doctrine: fight or shadow), or the contact's lost |
| **Shadow** (exists) | Keeps it in sight, reports | As 3.2f | Lost, or re-tasked |
| **Cover** (a unit) | Stays within reach of it to support | A station at a distance, on the side of the threat | Re-tasked |
| **Join** (a unit, or a point at a time) | Concentrates on it | A route to its estimated position, then a station in its formation | Joined |
| **Attack** (a contact; torpedo craft) | The flotilla's torpedo attack | The attack's legs: position ahead of the target's beam, run in, fire together, turn away | Fired, or beaten off |
| **Fight** (exists, deepened) | Fights at doctrine's range | 4.2c's band; deepened per level below | Enemy gone, or breaking off |
| **Withdraw** (to home, or away from a contact) | Breaks off and goes home | Run, then GoTo home | Home, or out of the area |

And the manoeuvres a squadron or force is told, which aren't tasks but one-off changes of its formation:

- **Turn in succession** (the line follows the guide's track; exists as line ahead's way of turning) and **turn
  together** (every ship turns at once and the line becomes a line of bearing on the old axis), both by an angle.
- **Battle turn-away** (Scheer's *Gefechtskehrtwendung*: turn together through 16 points, the rear ship first),
  as a kind of turn together.
- **Deploy** (into a single line of battle, on a wing column, toward a side): from a cruising order of columns
  abreast to one line ahead, the columns following the wing column in succession.
- **Form** (a formation and spacing; exists as `SetFormation`, a world command, made a unit order so it goes through
  the chain and its signals).

Formations generalise to **a line of bearing at any angle** (line ahead 180°, abreast 90°, the echelons 135° and 45°
are four of them), so a turn together leaves a valid formation and the geometry stays one (`LineOffsets`).

### Doctrine: the standing rules

A record per side (the scenario's), overridden per unit (a group's or a ship's), read down the tree: a unit takes its
own value, else its group's, else the side's. A command sets it, so it's logged and replays. v0's fields:

| Field | What it decides | v0 default |
|---|---|---|
| **Engage** | When a unit fights an enemy it meets: always, if not outgunned, only if attacked, never (evade) | If not outgunned |
| **Range** | The fighting range, as a share of the main battery's reach (4.2c's `FightRangeShare`) | 0.6 |
| **Outgunned** | The strength ratio at which it turns away (4.2c's `FightOutgunnedRatio`), and at which a commander declines battle | as 4.2c |
| **Fall out** | When a ship leaves the line: speed under a share of its designed speed, main guns under a share, list over a limit, or water over a share of its reserve | 60% speed, a third of the guns, 10°, half |
| **Break off** | When a unit gives up the fight: its losses (tonnage or capital ships) past a share of what it came with | A third |
| **Fire** | What fire at will takes: identified enemies only or any contact (4.2c fires on the unidentified: decision 1), and what secondaries go for (torpedo craft first, as their era's purpose) | Any; torpedo craft first |
| **Torpedoes** | At will, only on order, or never; how far into their range | At will, 4.6's shares |
| **Night** | Capital ships avoid action at night, or don't; torpedo craft attack at night, or don't | Avoid; attack |
| **Evasion** | Under fire: steady, zigzag, chase salvos (the evasion research's styles), from what range | Steady inside 10 km, zigzag beyond if outgunned |

**Presets, defined by values** (decision 7): a preset is a named set of these fields' values, as data
(`Content/Sim/doctrine.json`, beside the settings), never code. A side takes a preset by name in the scenario
(`"doctrine": { "Britain": "default" }`), and a side or a unit overrides single fields over it. v0 ships one preset,
`default`, with the values above. The fields are where a navy's character goes later (the British fight at long range
and turn away from torpedoes; the Germans turn away from the whole Grand Fleet; the Japanese at Tsushima close): more
presets by navy and year, from the research, are a fine-tune.

### Fitness: what a ship can still do

One small summary per ship from its condition (`Sim.Combat`, pure, cached by the condition's `Version`): the speed it
can make as a share of its designed speed, its firepower as a share of its main and secondary guns working (weighted
by calibre), its float margin (water against the reserve), its list, its steering, whether it's burning, whether its
torpedoes are spent. The captain reads his own ship's fitness exactly (own ships are known), against doctrine's fall
out. The admirals read their units' summed (a squadron's strength left, its speed). The same summary, made from
`SeenFlags` and the class as taken for, gives an enemy's fitness as it's seen: "she's low in the water and silent"
makes her a cripple to finish, not a threat.

### The tactical picture: enemy groups and their strength

What an admiral sees of the enemy, built from his navy's picture once a think per side (`Sim.Ai/Knowledge`, pure):

- **Enemy groups:** contacts chained within a gap (4.2c's `EnemyLine` is this for one line), each group's estimated
  centre, course and speed, its axis (a line ahead or abreast), its van, and its uncertainty. Through the world's grid,
  never pairs.
- **Strength:** one number per group, and one per own unit, the same function both ways: the weight of broadside
  (calibre cubed by guns, by what bears), speed, and the torpedo threat, from the class as taken for (so a mistaken
  class misleads the admiral, as it should). 4.2c's "heavy guns its navy sees" becomes this.
- **Threats:** enemy torpedo craft closing, and torpedo tracks seen (below, 5.1a).

`Fight`'s outgunned test, the fire distribution, the force's deployment and the commander's choice to accept or
decline battle all read it: one implementation.

### Reports and reasons

Every unit's layer keeps its `Status` and a reason (a small code: "falling out: speed", "turning away: torpedoes",
"holding fire: unidentified", "can't: lost contact"), in the hash and the snapshot. Superiors read the status; the
UI shows the reason on the ship's or group's label, and the decisions that are the player's (a captain asking to fall
out of the player's line, a squadron out of contact) become 2.6's notices, which 5.2 turns into the decision queue.

### Signals: orders take time

From plan.md's era list ("Signals, not radio orders"), changed by decision 6: **the player's orders are instant;
the AI's go by signal.** An order the player gives reaches the unit, and is handed down its tree, at once: it's
easier for the player, and it's to be looked at again in playtesting. An order an AI layer makes on its own (a
commander's task, an admiral's turn or attack, on either side, the player's own side's automated commander too) has a
sent time and a received time per unit, and between the two the unit carries on with what it had. The AI is the
slower of the two by this; playtesting says whether the player's orders should come to take time as well.

- **Each admiral flies his flag in a ship** (the guide by default; the scenario can name one). Signals go from the
  flagship.
- **Flags** reach a ship within sight of the flagship (or of a ship that repeats it down the line) after a hoist and
  read delay, by day; **searchlight** by night, slower; **wireless** to anything, after a coding and sending delay,
  if the flagship's wireless works. v0 takes the fastest path that works, with delays from `SimSettings`.
- **A signal can be missed** (a chance by distance, the visibility, and later smoke): the unit carries on as it was,
  until the signal's repeated (an admiral repeats an order his unit isn't following after a while). Misread signals
  (a different order received) are a later deepening.
- **A flagship lost** (sunk, or her signals wrecked) passes command to the next flag officer (the next squadron's
  admiral, or the next ship's captain), after a delay, during which the unit has no orders but its doctrine.
- **Reports up stay instant** for now (decision 2; 3.2's decision 1), but the architecture keeps room for them to
  take time: every report a superior reads (a unit's status, a contact) carries when it was made and by whom, as the
  picture's entries already do, and the commander and admirals read their units' states through one place, not
  straight from the captains. Delaying reports (and the navigational error in contact reports that bedevilled
  Jutland) then changes that one place: a deepening of the picture, with 3.2's open question on fixes.

### The side's commander, its aims and its home

The scenario gives each side, besides its forces:

- **Aims,** a small typed set (the same types the missions' objectives use, `docs/missions/plan.md`, so the checks are
  one implementation). **v0 has one: destroy everyone** (decision 8): sink every enemy ship. It's met when no enemy
  is left afloat in the area, and given up when the side breaks off. The rest come one at a time, when a scenario
  needs them, as more kinds of the same aim:
  - **Destroy** narrowed (a side's ships, a group, a type);
  - **Reach** (an area, or out of the map by an edge);
  - **Protect** (a unit);
  - **Hold** (an area until a time);
  - **Avoid** (battle with a superior force; an area).
- **Home:** where each side goes when it's over (a point, or a map edge with a bearing). Cripples and the beaten go
  there.
- **A plan,** optionally: each top unit's opening task (Hipper sweeps north, Scheer follows fifty miles astern; Beatty
  sweeps east and joins Jellicoe at a rendezvous). The commander starts from it and changes it when the picture does.
  Without one he makes his own from the aims.

**Who is the commander** (decisions 4 and 5): the player is their side's commander. The AI commander is off for the
player's side by default, and the player can switch it on, for the whole side or a unit ("automate"; "leave it to
the admiral"), and off again. The admirals and captains under the player carry out their tasks and doctrine either
way. For every other side the AI commander is on. For now, the dev can switch which side is the player's (the dev
toggle, `Command.SetPlayerSide`, and a scenario with no `"player"` to watch both).

The commander, v0, rule by rule:

- **No contact:** follow the plan; units without one search where the enemy is expected (the scenario's starting
  intelligence, or the line between the enemy's last estimate and its likely goal), the heavy units kept within
  supporting distance of the scouts.
- **Contact:** the scouts shadow and report; the nearest heavy force intercepts if the balance (strength, summed over
  what the picture holds) says fight, else the side concentrates (Join) and the threatened unit falls back on the
  others (Withdraw toward them, not home).
- **Battle:** forces Fight; flotillas Attack when the enemy is engaged and in reach; screens Screen.
- **The balance turns:** losses past doctrine's break off, or night for heavy ships that avoid it: Withdraw toward
  home, screened; flotillas may be sent in (Attack) to cover it.
- **Aims met or lost:** the side goes home.

### The end

A scenario ends when, in order:

1. **its time limit** passes (a scenario's `"until"`, else a default of a day);
2. **every side is done:** each commander has met or given up his aims and his units are home or out of the area;
3. **a side is gone** (sunk, or out of the area);
4. **nothing has happened for long:** no encounter open and none of a side's units in contact for `QuietEndMs`
   (hours), after the sides have met at least once. A stalemate is an end, and worth flagging.

The result, v0, is facts, not a grade (grading is 6.1's): each side's losses (ships, tonnage, crew), its ships
damaged, its aims met or not, and when the sides met, fought and parted. Read from the combat log and the world, so
6.1's debrief builds on it.

**The clock in self-play:** guns are silent in the coarse tier until 4.9 (fighting.md decision 17). So the headless
runner asks for full tilt while no encounter is hot, and drops to a fine speed (the "own" rule's 1x) for any hot
encounter, as the player's interest table does for their own. That's a gameplay rule relaxed until 4.9 restores
coarse fire (end-goal rules).

## The steps

Each is a session (or a few), ends with something to run and watch, and updates this doc and plan.md. Each piece
arrives with the step that first uses it (fighting.md decision 12): no empty shells ahead of their users. Each step
adds minimal world-tab placeholders to see and give its orders, as 2.6c did; the real UI is 5.2.

### 5.0 The bench: a scenario that ends

Before any AI is written, the instrument that says whether it works:

- **The end conditions** above, and the result (facts) from the combat log.
- **`shipgen scenario X --play`:** runs to the end (the clock rule above), prints a timeline (first contact, first
  sight, first salvo, each loss, each break-off, the end and why) and the result; `--seeds N` runs N seeds and prints
  the spread (losses, duration, how often each side's aims were met).
- **`"until"`, `"home"` and `"aims"`** in the scenario file (aims are read and checked here, and nothing acts on them
  yet); the four scenarios given them. v0's one aim is destroy everyone (decision 8), so `"aims"` is that for every
  side until a scenario needs another.
- **What it shows now** is the baseline: `squadrons`, `flotilla` and `tsushima` fight to a stalemate or a sinking and
  stop by time; the Jutland stops by time without meeting. Each later step moves those numbers.
- **The app:** a side may be "the AI's" (the player watching): the dev toggle to switch the player's side, and a
  scenario with no `"player"`.

### 5.1 Captains and admirals

Bottom-up, so each layer is built on one that already behaves.

**5.1a Doctrine, fitness and the captain's judgement.**

- **Doctrine** as above, as world state with its command (`SetDoctrine(unit, field, value)`), its presets as values
  in `Content/Sim/doctrine.json` (`default` only, decision 7) and its scenario field (`"doctrine"`: a preset per side,
  fields over it per side and per unit). `Standing` and the `"fight"` list become doctrine's Engage.
- **Fitness** (`Sim.Combat`), and the seen fitness of enemies.
- **Falling out:** a ship past doctrine's fall out tells its admiral; he detaches it from the line (the line closes
  up, as a nudged ship's does), and it steers away from the enemy and goes home at the speed it has, firing at will
  as it goes. A ship that can't steer (jammed rudder) is a cripple in place: its admiral leaves it.
- **Evasion under fire,** from `research/simulation/evasion-research.md` §2 and §10: the styles as captains' behaviours
  about a base course (steady, zigzag, chase salvos), chosen by doctrine, held steady for a torpedo launch's last 30 s.
  A ship on station evades only as its line does (a squadron zigzags together, 5.1b).
- **Torpedoes seen and avoided** (decision 10: captains try already): a torpedo's wake is seen from a ship within a short range by day (a placeholder,
  `TorpedoSeenM`, from the light and the sea), into the picture as a track; a captain who sees one bearing on his ship
  turns to comb it (bow or stern on). The admiral's turn-away from a whole attack is 5.1b.
- **Reasons** in the snapshot, shown on the ship's label.
- **Checked:** a ship crippled in `squadrons` falls out and steams home, and the line closes up; in `flotilla` ships
  that see tracks comb them and fewer torpedoes hit than in 4.6's run (the same seed); evasion styles measured against
  `fire_control_ref.py`'s hit rates on `shipgen duel` (wide bands); allocation-free; replays.

**5.1b The squadron's tactics.**

- **Formations as lines of bearing** (the generalisation above) and the manoeuvres: turn together, turn in
  succession, battle turn-away, as unit orders.
- **The squadron's Fight:** the line's Fight steers its guide so the line keeps its broadside bearing and, given the
  room, crosses ahead of the enemy's van (the T, v0: steer for a point ahead of the enemy group's van on its course,
  within the range band) rather than one ship's band.
- **Evasion together:** a line under fire zigzags or turns together by doctrine.
- **The turn-away from torpedoes:** a squadron that sees an attack coming (enemy torpedo craft closing into their
  range, or tracks seen) turns away together by doctrine's points, and back when the threat's past.
- **The flotilla's attack:** a group of torpedo craft given Attack (or by doctrine, on a heavy enemy engaged with its
  line) gains a position ahead of the target's beam, runs in together, fires its spread on one signal, and turns away
  together; it's beaten off (aborts) past doctrine's losses. 4.6's lone boat's attack stays a captain's.
- **The counter-attack:** a screen's light cruisers and destroyers given Screen engage enemy torpedo craft closing on
  the unit they screen (doctrine's secondaries' targets for the line itself).
- **Checked:** a column turns together 90° and back and keeps a line of bearing within metres; the battle turn-away
  reverses a line of eight in a few minutes without collisions of stations; in `flotilla` the boats attack together
  and the line turns away; in `squadrons` the side whose line crosses ahead gets the better of the hits over seeds.

**5.1c The force: its tasks, the approach and the deployment.**

- **The tasks** of the table above (GoTo, Search, Patrol, Scout, Screen, Intercept, Shadow, Cover, Join, Withdraw)
  for any unit, with their ends, and their handing back.
- **The tactical picture** (enemy groups and their strength), one implementation that `Fight` and the fire
  distribution move onto.
- **A force on task:** its members are on station (formation) or on a task of their own (a light cruiser squadron
  Scouting ahead, destroyers Screening the battle line), so a fleet's cruising disposition is one force.
- **The approach:** a force told to Intercept or Fight closes in its cruising order, its scouts reporting.
- **The deployment:** when the enemy's main body is in sight (or reported close), the force deploys into a single line
  of battle, on the wing column the scoring function picks (the side that crosses ahead of the enemy and keeps the
  force between him and his home, v0: the wing nearer the enemy's van), and then fights as a squadron does (5.1b).
- **Falling back on the main body:** a force that meets a superior enemy withdraws toward its supports, keeping in
  touch, rather than home (Beatty's run to the north, reflected: the decoy).
- **Checked:** a force of three columns abreast deploys into one line across a reported enemy's course in reasonable
  time (Jellicoe's deployment took about twenty minutes; the research to ask for has the numbers); scouts spread on a
  scouting line spaced by the visibility find a crossing enemy in most seeds; Intercept meets a target steering a
  steady course from 50 km.

**5.1d The side's commander.**

- **The layer** (`Sim.Ai/Commanders/`), one commander per side with forces, thinking slowly and on events.
- **Aims, home, plan** as above, the plan's opening tasks given at the start.
- **On or off per side:** off for the player's side by default, switched on (the whole side or a unit) and off by a
  logged command; the dev's side switch (decisions 4 and 5).
- **His rules** (no contact, contact, battle, the balance turns, done), each a state with hysteresis and a reason.
- **Night:** heavy ships avoid action by night (Withdraw from contact, keep in touch by scouts), flotillas Attack by
  doctrine. The sighting already shortens the night's reach (3.2c); the lights of night fighting (searchlights,
  star shell) aren't in yet.
- **The Jutland's plans:** Hipper's sweep north with Scheer astern, Beatty's sweep east and rendezvous, Jellicoe's
  approach, from the scenario's history, with both homes. Every side's aim is v0's destroy everyone (decision 8); the
  aims history had (Germany to destroy an isolated part of the Grand Fleet and avoid the whole of it; Russia at
  Tsushima to reach Vladivostok) come with Avoid and Reach, when a scenario asks for them.
- **Checked:** the Jutland meets (the scouts, then the battlecruisers) in most seeds; a side that's outmatched declines
  and goes home; every unit is on a task or home at the end (nothing idle); the commanders cost little (measured).

**5.1e Signals and flagships.**

- **Flagships,** flag delays, searchlights by night, wireless, and missed signals, as above; the order in transit in
  the snapshot (sent, received, executing) for the map's "◷".
- **Command passing** when a flagship is lost or her signals are wrecked (her bridge or wireless office, placeholders
  until the damage model names them).
- **The player's orders are instant** (decision 6): they skip the signals, down the whole tree. Only the AI's own
  orders travel, on both sides. Revisited in playtesting.
- **Tuned twice** (decision 9): the tactics of 5.1a-d are tuned without signals first, then again with them, since the
  delays are the trickier part.
- **Checked:** a column's turn signalled by flag reaches the rear ship after the hoist and the repeats; a missed signal
  leaves a squadron on its old course until it's repeated (the 5th Battle Squadron at Jutland, in a test); a sunk
  flagship's squadron carries on by doctrine and then takes the next admiral's orders; replays hold (draws from a
  named stream).

**5.1f The whole: scenarios that play themselves.**

- `squadrons`, `flotilla`, `tsushima` and the Jutland run with `--play` to an end over seeds, each with a timeline
  that reads like a battle (contact, deployment, the fight, cripples falling out, turn-aways, the break-off, home).
- **Kept tests** (decision 1: the bar is "plays to an end in most seeds", not a historical outcome): each reaches an
  end by its own rules (not the time limit) in most seeds; no unit idle; the sides meet and fight. A stalemate or a
  timeout is reported, so a change that breaks the AI shows.
- **Cost:** the AI at the Jutland within the sim thread's budget (plan.md "How many ships"), measured on the ladder,
  small first. Nothing allocates per think.
- **The app:** watching both sides play, and taking one side over at any moment (the player's orders hold units as
  2.6 has it; "leave it to the admiral" hands them back).

### 5.2 The command UI on the map

As plan.md has it: orders at every band, with previews, the forces rail, edge markers and the selection strip
(`docs/battle/command-ui.md`, `docs/map-ui/README.md`). Planned at its start, one tab per session with a mockup first
(as the designer's tabs were). What step 5.1 leaves it to show:

- **Orders as objects on the map:** each unit's task and its line (a route, a station, an intercept, a search area),
  sent against executing (signals), editable by dragging.
- **Control state on every symbol:** the player's, the AI's, or doctrine's; held and detached (2.6c's marks, redone);
  weapons free, tight or hold; the reason tags.
- **The doctrine card** per side and unit, and the tasks in the radial menu with the smart default.
- **Leave it to the admiral:** any unit, or the whole side, handed to the AI; and the commander's suggestion for the
  player's held units.
- **The decision queue and the event log** (the research's two streams): notices, captains asking to fall out, contact
  reports, losses; each item jumps the camera.
- **The rumble** for a fight at speed (fighting.md decision 4) is designed here.

## Where it lives

As the layers already are:

| Layer | What's new |
|---|---|
| `Sim.Combat` | `Fitness` (a ship's, and an enemy's as seen) |
| `Sim` (world) | Doctrine's store and command; formations as lines of bearing in `GroupSystem`; signals in transit (world state: the AI's orders, on both sides) |
| `Sim.Ai` | `Commanders/` (the side's commander, aims, plans); the tasks in `AdmiralLayer` and `CaptainLayer`; `Knowledge/` the tactical picture and strength; `Captains/` evasion, falling out, combing tracks; `Admirals/` manoeuvres, the deployment, the flotilla's attack |
| `Sim.Missions` | `"aims"`, `"home"`, `"plan"`, `"doctrine"`, `"until"`; the end and the result |
| the exe | Placeholders per step on the world tab; 5.2's UI |
| `shipgen` CLI | `scenario --play [--seeds N]` |

The aims' checks (is this ship sunk, is this unit in that area) are one implementation in `Sim.Ai`, which the
commander reads to know when he's done and 6.1's objectives read to judge.

## Performance

- Captains cost ~4 µs a fighting ship a second now (4.2c). Evasion and falling out add a few comparisons each.
- Admirals as forces think every 10 s, the commander every minute and on events: per side, not per ship.
- The tactical picture is built once per side a force think, through the grid: O(n log n) in contacts.
- Target: the Jutland's AI within its share of the sim thread at 10x, measured in 5.1f as 2.7c did.

## Needed from the designer

Flagged placeholders until then (`DesignerPlaceholders.cs`):

- **The signal bridge and the wireless office** as parts, so their loss cuts signals (5.1e).
- **Searchlights** as parts (they're render clutter now, plan.md 3.2's list), for night signalling and, later, night
  fighting.
- **Torpedo wakes** aren't the designer's, but the torpedo's propulsion (the era's wet heaters left a track; oxygen
  torpedoes didn't) is a fact of the torpedo row when the designer offers torpedoes.

## Research for the fine-tuning pass

In `research/REQUESTS.md` (2026-10-14). **Not needed for the first version** (decision 3): step 5 is built on basic,
guessed numbers and simple rules, and this research tunes them later, in a fine-tuning pass of its own:

1. **Signals of the era:** how long a flag hoist took to read and execute, how it was repeated down a line, the
   searchlight's rate, wireless delays and failures; the record of missed and misread signals (Jutland's 5th BS,
   the battlecruisers' "Engage the enemy more closely"). For 5.1e's delays and chances.
2. **Fleet tactics and doctrine, 1890-1925, by navy:** the Grand Fleet Battle Orders (cruising order, deployment, the
   turn away from torpedoes), the German battle instructions (the battle turn-away), Tsushima's Japanese tactics,
   the flotilla attack's doctrine, cruiser scouting lines (spacing, signalling). Timings: how long a deployment, a
   turn together or a battle turn-away took. For 5.1b-d and doctrine's defaults.
3. **When ships fell out and fleets broke off:** what damage made a captain leave the line, and what losses made an
   admiral break off, across the era's actions. For doctrine's thresholds.

## Not in step 5

- **Smoke screens:** with the smoke field (3.2's decision 3). A destroyer's smoke to cover a turn-away is a task to
  add then.
- **Night fighting's lights** (searchlights, star shell, gun flashes): night here is shorter sight and doctrine.
- **Towing, rescue and standing by a cripple.**
- **Mines and minesweeping tasks** (4.7), **collisions and avoiding them** (4.11), **coarse fire** (4.9: until then a
  self-played fight runs on the fine tier).
- **Merchants' rules** (stop, capture, let go), convoys' orders, NPC admiralties and the campaign's strategy.
- **Misread signals and delayed reports** (deepenings of 5.1e and of the picture).
- **An AI that plays well:** a later fine-tune pass.

## Decisions

The user's, 2026-10-14:

1. **The bar is "plays to an end in most seeds."** The AI isn't perfect and doesn't need to be; no historical outcome
   is asked of 5.1f. Its bands say "it played", and playing well is a later fine-tune.
2. **Reports up are instant for now,** kept simple but kept in mind in the architecture: reports carry their time and
   source, and superiors read them through one place, so delays can come later ("Signals").
3. **The research goes into `research/REQUESTS.md`,** but the first version is built with basic numbers and rules,
   without waiting for it. The research is for a fine-tuning pass.

The user's, 2026-10-14, on the questions:

4. **The side's commander is a layer of its own** (`Commanders/`). The player is their side's commander, and can also
   automate it; for now the dev can switch which side is the player's.
5. **The AI commander is off for the player's side by default,** and can be switched on.
6. **Signals are instant for the player** (a change from plan.md's era list): easier for the player for now, to iterate
   in playtesting. The AI's own orders still go by signal.
7. **Doctrine as presets, defined by values:** named sets of the fields' values, as data, overridden per side and unit.
8. **Aims in the scenario file, starting with "destroy everyone":** the other kinds come when scenarios need them.
9. **Tune twice:** signals (5.1e) come after the layers they delay, since the delay is the trickier part; the tactics
   are tuned without them, then with them.
10. **Captains attempt torpedo evasion already** in 5.1a (wakes seen at short range, combing the tracks).
