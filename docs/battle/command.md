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
  raises a notice; a route's end hands it back. All of it at every level. A task's or the commander's order takes a
  unit out of its line as the AI's (`Detachment.Tasked`, 2026-10-15): never "held by you", no notice.
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
| **Outgunned** | 1 / it is the matchup under which a unit turns away, and a commander declines battle (5.1g) | 2 |
| **Seek** (5.1g) | The matchup at which a commander sends units after an enemy | 1 |
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

**Aggression** (added 2026-10-14 after 5.1d, for testing by hand): one more field, -1 (timid) to +1 (bold), 0 as the
doctrine is written, set and inherited as any other. It scales the fields `SimSettings.AggressionShift` lists as they're
read, each by (1 + shift x aggression): bold takes worse odds (`OutgunnedRatio` up), closes nearer (`RangeShare` down),
stays in the line longer (the fall-out thresholds), bears more losses before a flotilla or the side breaks off, and turns
less far from torpedoes. A dev slider per side on the world tab sets it (as the side's doctrine, logged). `squadrons`, 5
seeds, both sides alike: at -1 Britain loses 0.4 of 4 ships, at 0 2.2, at +1 3.6 (3 runs decided). Launch sliders and
captains' and admirals' personalities can move it (or traits like it) later; the commander's own judgement numbers
(`CommanderMarginShare` and the rest in `SimSettings`) aren't doctrine yet, so it doesn't reach them.

### Fitness: what a ship can still do

One small summary per ship from its condition (`Sim.Combat`, pure, cached by the condition's `Version`): the speed it
can make as a share of its designed speed, its firepower as a share of its main and secondary guns working (weighted
by calibre), its float margin (water against the reserve), its list, its steering, whether it's burning, whether its
torpedoes are spent. The captain reads his own ship's fitness exactly (own ships are known), against doctrine's fall
out. The admirals read their units' summed (a squadron's strength left, its speed). The same summary, made from
`SeenFlags` and the class as taken for, gives an enemy's fitness as it's seen: "she's low in the water and silent"
makes her a cripple to finish, not a threat.

### The tactical picture: enemy groups and who can fight them

What an admiral sees of the enemy, built from his navy's picture once a think per side (`Sim.Ai/Knowledge`, pure):

- **Enemy groups:** contacts chained within a gap (4.2c's `EnemyLine` is this for one line), each group's estimated
  centre, course and speed, its axis (a line ahead or abreast), its van, and its uncertainty. Through the world's grid,
  never pairs. In sight (`TacticalPicture`, the deployment's), and held, in sight or not (`HeldGroups`, the
  commander's, 5.1g).
- **Who can fight whom** (5.1g, replacing the one-number strength first planned here): a matchup of two sides, each
  its classes with counts, own ships exactly and the enemy as taken (so a mistaken class misleads, as it should); the
  share of the enemy's tonnage each side puts out of the fight for the share of its own (`Sim.Combat/Matchup`, the AI's
  `Knowledge/MatchupJudge`).
- **Threats:** enemy torpedo craft closing, and torpedo tracks seen (below, 5.1a).

`Fight`'s turn-away, the scout's shadowing, the flotilla's attack and the commander's targets, his declining and his
flotillas all read the matchup: one implementation.

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

1. **its time limit** passes (a scenario's `"until"`, else a default of a day). The limits are a testing aid for now
   (decision 11): in the release they're relaxed far, and a clock never ends a fight that's going on;
2. **every side is done:** each commander has met or given up his aims and his units are home or out of the area;
   (2026-10-20: given up, broken off or declining a stronger enemy, and every ship afloat home or in no encounter: every
   side going home and none of them in touch. A port hundreds of kilometres off doesn't hold it open);
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

**5.0 done 2026-10-14.** Where it lives:

- **The judge:** `Sim.Missions/ScenarioEnd.cs`, asked at whole game minutes (`Check`): over or not and why
  (`EndReason`: time, decided, quiet), the timeline (`PlayLine`: the sides meet, each side's first sight, salvo, hit
  and torpedoes, every loss, the end) and the result (`SideResult`: ships and tonnes, lost, damaged afloat, afloat,
  aims met). It reads the world and the combat log; it isn't world state, since the world never ends, only a
  scenario does.
- **The aims:** `Sim.Ai/Commanders/Aims.cs` (`AimKind.DestroyEveryone`, `Aims.Met`, `Aims.Afloat`: going down counts
  as lost from its first moment), where the commander (5.1d) will read them too.
- **The file:** `"until"` and `"aims"` (`Scenario.Until`, `AimsOf`; a side not listed wants to destroy everyone), both
  taken through `"from"`. The four scenarios have them: squadrons until 21:30, flotilla 23:30, Tsushima the next
  forenoon, the Jutland 04:00 on 1 June. `SimSettings.PlayLimitMs` (24 h) and `QuietEndMs` (2 h).
- **The bench:** `shipgen scenario X --play [--seeds N] [--fast 1000] [--fine 1]` (`ScenarioPlay.cs`). It sets no
  player side and the others' interest rule to slow to `--fine`, so an encounter opening drops to the fine tier at the
  event; `--fast` while none is open. A seed whose sim throws is reported with its stack, and the others run on.
- **The app:** the spawner's "watch" box (no side is the player's); the spawner's side takes one back.
- **Tests:** `ScenarioEndTests` (the fields read; decided, time and quiet ends), and `ShellSystemTests`' shell below.

What it settled, beyond the plan:

- **The end lives in the mission layer,** not in the world, and is asked on a grid of world time, so every run and
  replay says the same at the same moment. 6.1's objectives can move onto events when they come.
- **"Every side done" and "a side gone" are one rule in v0,** decided: every side still afloat has its aims. With
  destroy everyone that's one side left (or only sides at peace).
- **`"home"` waits for 5.1a,** its first user (each piece arrives with the step that first uses it).
- **Found on the way:** Tsushima threw on its first run (an event scheduled in the past). A 6" casemate gun 2.7 m over
  the sea at about 3° tops out at the shell's danger window's 60 m, where the climb is near nothing, and the solve
  for when it comes down through the window's top stepped back to before it was fired. Its whole flight is in the
  window now (`ShellSystem.Launch`; the shell as fired there is `ShellSystemTests.A_shell_topping_out_at_the_window_lands`).
  Hashes of runs that never met it are unchanged.

**The baseline** (Release, before any of 5.1; the AI as 4.2c and 4.6 left it: fire at will, the standing Fight):

| Scenario | Seeds | How it ended | Losses |
|---|---|---|---|
| `squadrons` | 5 | decided 3 (after 0:41-3:27), time 2 (6:00) | Britain 3.2 of 4, Germany 0.4 of 5 |
| `flotilla` | 5 | decided 2 (~1:15), time 2 (5:00), quiet 1 (4:05) | Britain 0.6 of 4, Germany 5.0 of 6 |
| `tsushima` | 5 | quiet 5 (8:05-19:59) | Russia 5.0 of 13, Japan 2.0 of 14 |
| the Jutland | 1 | time (14:00, played in 190 s) | Britain 25 of 151, Germany 22 of 99 |

- **Every run ends, none crashes.** Nothing decides Tsushima or the Jutland: with no one to close or pursue, the
  fleets drift apart (quiet) or the time runs out.
- **The Jutland's fleets meet only at 18:36** (the first salvo, 4.5 h in): the scouts sight each other at 14:15 and
  nobody turns toward what they've seen.
- **Own fire:** 7 of the Jutland's 47 losses were last hit by their own side. Shells and torpedoes hit whatever is in
  their way (the hit query is physical), and fire at will never asks whether its line of fire is clear of its own
  ships. For 5.1a: a captain holds a battery's or launcher's fire while one of his own is in its line (and near it at
  the fall).
- **Cost:** the small scenarios play in 0.3-22 s a seed; the Jutland's 14 hours in 190 s.

### 5.1 Captains and admirals

Bottom-up, so each layer is built on one that already behaves.

**5.1a Doctrine, fitness and the captain's judgement.**

- **Doctrine** as above, as world state with its command (`SetDoctrine(unit, field, value)`), its presets as values
  in `Content/Sim/doctrine.json` (`default` only, decision 7) and its scenario field (`"doctrine"`: a preset per side,
  fields over it per side and per unit). `Standing` and the `"fight"` list become doctrine's Engage.
- **Fitness** (`Sim.Combat`), and the seen fitness of enemies.
- **Home** in the scenario file (from 5.0, which left it to its first user): where a side's cripples go.
- **A clear line of fire** (found by 5.0's baseline: 7 of the Jutland's 47 losses were own fire): a battery or launcher
  holds its fire while one of its own ships is in its line, near the fall or the torpedo's run.
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

**5.1a done 2026-10-14,** built in five pieces, each committed when it played:

1. **Doctrine, presets, homes.** `Doctrine` is AI state (`Sim.Ai`, held by `AiSystem`; the table above put it in the
   world), read ship, squadron, forces, side, then a neutral default that behaves as before doctrine (carry on, never
   fall out), so dev worlds and tests are untouched. `Content/Sim/doctrine.json`'s "default" preset is the gameplay's
   (Engage: fight), given to every scenario side unless it names another; a scenario sets values per side and per unit,
   and each side's `"home"`. Engage replaces the `Standing` order and the scenarios' `"fight"` lists (a file with
   `"fight"` says it's gone); `FightRangeShare` and `FightOutgunnedRatio` moved from settings to doctrine. Proof: the
   15 seeds of `squadrons`, `flotilla` and Tsushima played the same as 5.0's baseline; the Jutland now fights by
   doctrine (its first salvo at 14:26, Galatea at B109: history's at 14:28).
2. **Fitness and falling out.** `Fitness` (`Sim.Combat`): speed, main guns, water against the reserve, list,
   steering. A fighting captain judges it when his ship's condition changes; past doctrine's fall-out he leaves his
   group (the line closes up) and goes `Home` (a captain's order): away from the enemy in sight, else for his side's
   home, stopped within `HomeRadiusM`. His reason (`CaptainReason`) is in the snapshot, on the map beside the ship and
   in the captain row, and on the bench's timeline. Steering lost counts only where doctrine falls out at all.
   - A fault the captain judges temporary (a blackout, a tripped breaker, a stunned crew) isn't to take her out of the
     line (the user, 2026-10-20): done 2026-10-20 with E7's fix (docs/battle/shells-armour-damage.md): `Fitness` reads
     the lasting state, the outages ending within `FallOutWaitMin` (15 min) over.
   - Found on the way: the sightings' per-encounter grid grew when a ship running off spread the encounter (48-72
     bytes, a fight's seconds); it's sized for 250 km now (4 KB a world), and the flotilla's allocation test skips the
     seconds a ship leaves a group, as it does those a ship goes.
   - After 1-2: `squadrons` ends by time 5 of 5 (Britain loses 2.6 of 4: cripples get away), `flotilla` 3 of 5,
     Tsushima quiet 4, time 1; the Jutland loses 75 ships with 65 fallen out (own fire 4).
   - Dev damage flags (`-wreck` and the rest) reach the dev world, not a `-scenario`'s (they're sent before it loads).
     Left as it is.
3. **A clear line of fire.** `LineOfFire` (`Captains/`): the ships off rails in a grid once a think, a segment checked
   a cube at a time. A battery doesn't take (or keep) a target with a friend within `FireFoulM` of the fall (from
   `FireFoulBeforeM` short of it to `FireFoulM` past it); torpedoes aren't fired with one within `TorpedoFoulM` of the
   track out to its run; neither counts the first `FireFoulNearM` off the shooter. A friend is his side, or one at peace
   with it. The Jutland's own-fire losses 4 → 1 (it plays in 107 s, from 124: fewer shells into its own lines);
   `squadrons`, `flotilla` and Tsushima much as after piece 2.
   - 2026-10-16: a torpedo's track is judged as the ships move: a friend fouls it if she'll come within `TorpedoFoulM`
     (400 → 200 m, two and a half destroyer lengths: the spread's fan, a degree's course error) of the torpedoes on
     their run, each going on as she is, the margin growing by as far as she could turn off her course by
     `TorpedoFoulTurnDeg` (20°) by then (a boat astern in the flotilla is passed in seconds, a ship in a mêlée minutes
     on). Seen from a flotilla turned together the tracks fall astern by the boats' speed against the torpedoes', clear
     of the boats ahead and onto those astern; the static check held every boat but the leading one. Without the
     growing margin the Jutland's own-fire losses went 1 → 5 (destroyers torpedoed by their own flotillas in the
     mêlée); with it, 0.
4. **Evasion under fire.** Doctrine's `Evasion` (steady or zigzag), `EvadeDeg` (30°), `EvadeLegS` (75 s) and
   `EvadeBeyondM` (10 km); the default preset zigzags. Who's under fire, and from how near, is read once a think from
   the batteries' targets (a ship knows she's shot at). A ship running from fire beyond the range (Fight turned away,
   falling out) zigzags about the course she wants, each ship on her own phase; a torpedo craft on her attack whatever
   the range, steady within `EvadeSteadyShare` of her attack range to fire. A ship on station keeps it (the line's
   evasion is 5.1b's). Weaving and chasing salvos are left for later: chasing needs a ship to know which splashes were
   meant for her. Measured: running from a battleship at 14 km for 15 minutes, a light cruiser took 0 hits zigzagging
   and 4 steady over 4 seeds (no statistic, the right way round). The flotilla's boats lose 3.4 of 6 (from 4.6) and hit
   more (Britain loses 1.2 of 4, from 0.8): the research's run-in weave.
5. **Torpedoes seen and combed.** `TorpedoWatch` (`Captains/`): the running torpedoes in a grid once a think
   (`TorpedoSystem.Where`, the torpedo's closed form, now public). A ship's own lookouts see a wake within
   `TorpedoSeenM` (2 km) by day, `TorpedoSeenNightM` (400 m) at night (the sun under `TorpedoDayElevationDeg`); one
   whose track passes within her half-length and `TorpedoCombMarginM` before its run ends is coming at her, and with
   doctrine's `TorpedoEvasion` at comb (the default preset; neutral: none, so 4.6's benches keep their steady targets)
   she turns parallel to it, bow or stern on, the smaller turn, until `TorpedoCombAfterS` after it's past; a plain
   course is given back then. Her reason says "combing". Deviation from the plan: the wakes are each ship's own
   lookouts', not entries in the navy's picture (it holds ships); tracks go there when 5.1b's turn-away needs them.
   Measured: a spread of 4 from 3 km on a battleship's beam, 6 seeds: 3 hits of 24 combing, 10 holding on. The
   flotilla's battleships now comb: in its allocation test's window 10 torpedoes and no hits (4.6's run had 7 hits on
   Monarch).

**After 5.1a** (Release, 5 seeds; the Jutland one): `squadrons` time 3, decided 2 (Britain loses 3.0 of 4); `flotilla`
time 4, quiet 1 (Britain 0.4 of 4, Germany 3.0 of 6); Tsushima quiet 5 (Russia 3.4 of 13, Japan 4.2 of 14); the Jutland
to its time, 15 British and 16 German ships lost, 22 fallen out, in 130 s. Every run ends; most by time, since nobody
pursues a cripple or a beaten enemy yet (the force and the commander, 5.1c-d).

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

**5.1b done 2026-10-14,** built in five pieces, each committed when it played (the last one doesn't cross the T: see
piece 2):

1. **Lines of bearing and turning together.** `Formation.Bearing` with `Group.BearingDeg` (degrees clockwise from the
   guide's heading where the line lies: line ahead 180, abreast 90, the echelons 135 and 225, `GroupSystem.BearingOf`);
   `OffsetFromGuide` is the one geometry still. A force whose line of bearing its squadrons share is one line, as line
   ahead is (`Chains`); every old formation places as before, and the bearing is hashed only for a line of bearing, so
   the hashes didn't move. `GroupSystem.TurnTogether(group, deg)` changes the formation to what the ships will lie in
   once round (the line stays where it is; one that would lie ahead of the guide's beam is taken in the reverse order,
   so after 16 points the old rear leads; one that comes out astern is line ahead again), through the whole tree. Unit
   orders `TurnTogether` (Y: rear ship first, `BattleTurn`) and `Form` (`SetFormation` through the chain);
   `AdmiralLayer.Manoeuvres.cs` gives every ship the new course (past 150°, a quarter at a time, so she turns the way
   the signal said; rear first, each `BattleTurnStepS` after the one astern), the guide's speed, and no stations in the
   tree until all are within `ManoeuvreDoneDeg` (or `ManoeuvreMaxMs`). A turn in succession is a course to a line ahead,
   as before. Measured (`ManoeuvreTests`, dreadnoughts at 15 kn): six in line ahead turned 90° are 18 m off their
   line of bearing at 5 minutes and back in line ahead within 42 m after turning back; the battle turn-away reverses a
   line of eight with every ship round in 5.4 minutes, never two closer than 500 m, the line stretched to 617 m off
   station (each turned 10 s after the one astern, as Scheer's did) and closed up to 46 m at 20 minutes; a force of
   two squadrons in one line turns as one line. Placeholders: the group row's "turn together" buttons, "Line of
   bearing" in its formation picker.
2. **The enemy's line, shared; the crossing measured and dropped.** `Knowledge/EnemyLines` is the one enemy line
   (chained contacts, van first), the fire distribution's. The bench prints the hits each side made (and, from piece 4,
   the torpedoes it fired). The squadron's Fight crossing ahead of the van was built three ways and measured against
   Fight as it was (`squadrons`, 10 seeds, hits Britain/Germany; and a line against one steaming on):
   - *the planned rule* (hold on the beam a point a share of the range ahead of the van): no crossing 170/400; both
     sides crossing 112/172; Britain alone 122/173; Germany alone 152/179. Fights get less decisive (the lines fight
     further out), and the side that crosses doesn't come out better. Against a German line steaming on, British hits
     rise 238 → 405 but German hits rise more, 400 → 729: the lines run parallel, the British a little ahead, and
     nobody's guns are masked.
   - *steering for the point* (straight across the enemy's bow): worse, 87/154 against the steady line: the line comes
     in bow on, with only its forward turrets bearing.
   - *the band to the van, the beam ahead of it*: no gain in position. In a test of four dreadnoughts against four
     steaming on 3 kn slower, plain Fight is round to 18° off the enemy van's bow at the hour, the crossing rules 32-55°.

   Why: these lines are short (4-5 ships, ~2 km) fighting at 13-18 km, where no turret is masked and there's no van to
   concentrate on; Fight already draws ahead when it has the speed. Crossing the T pays with long lines at shorter
   ranges, and against an enemy that holds his course. So the rule isn't kept (no doctrine knob that doesn't do what it
   says); it's for 5.1c's deployment with the Jutland's long lines, and the fine-tune pass.
3. **The turn-away from torpedoes.** Doctrine's `TurnAwayDeg` (the default preset 45: 4 points; neutral 0). A line
   that's no force's (its force turns) and isn't torpedo craft sees an attack coming
   (`AdmiralLayer.TorpedoThreat`: a contact taken for a torpedo craft within `TorpedoThreatM`, 8 km, of a ship of the
   line and closing on her faster than `TorpedoThreatClosingMs`, or a ship of it combing a track) and turns away
   together, from the side it comes from; its order (Fight, say) waits, the guide holding the course it came out on,
   until it's seen none for `TurnBackS` (3 minutes), when it turns back together and the order's handed on again. A
   contact is a torpedo craft by the class it's taken for once that's made out (no gun of 6" or more, and tubes), else
   by type: the golden "destroyer" is 2,700 t, so by type it reads as a cruiser. `TurnAwayTests`: a line steaming south
   turns to 225° when destroyers from the east are made out at 7.7 km, and is back on 180° at 21.7 minutes. `flotilla`,
   10 seeds: Britain loses 0.0 ships (0.3 before), the boats make 126 hits (190). 4.6's flotilla test's window is
   longer: the boats reach their range later against a line that turns away.
4. **The flotilla's attack.** Doctrine's `TorpedoAttack` ("together" in the default preset; neutral "alone", as 4.6)
   and `AttackBreakOffShare` (0.5), the unit order `Attack` (a target, or the biggest in sight), and
   `AdmiralLayer.Attack.cs`: a squadron of torpedo craft fighting (or told to Attack) gains a point `AttackGainDeg` (50°)
   off the target's bow at `AttackGainShare` (1.5) of its torpedoes' fast range; runs in once ahead of his beam with
   him within that run (or after `AttackGainMaxMs`), bow on, on the course that meets him; on the signal turns together
   through 8 points, so the beam tubes bear and every track is square to the line and clear of the next boat; each
   boat fires once as hers bear (`CaptainLayer.FireOnSignal`, within `AttackFireS`; the tubes train first, so the
   spreads are in the water within two minutes); runs from the target for `TorpedoTurnAwayMs`; and goes in again while
   any boat has torpedoes, then fights with its guns. A target that turns away so the boats fall abaft his beam sends
   them back to gaining. Past the break-off share of its boats gone it's beaten off and runs. The captains hold their
   torpedoes for the signal (`TorpedoHeld`, let go out of the line). Found on the way: a one-moment signal fired nothing,
   since the boats ran in on Fight's slant with the target along their line abreast, and every track was foul of a
   neighbour; the turn together is what the flotillas did. `FlotillaAttackTests`: gain, run, fire, away, gain again; 4
   torpedoes on the signal at 16.7 minutes. `flotilla`, 10 seeds, against a line holding on: together 5.8 torpedoes a
   run, alone 20.4 (each attack takes a quarter of an hour to set up under fire), Britain losing 0.2 ships either way
   (0.3 alone); with the turn-away, 1.9 torpedoes. Runs now end by their own rules: 7-10 of 10 quiet, where alone
   ended by time. Placeholder: the group row's "torpedo attack".
   - **Fixed 2026-10-16** (docs/battle/bugs.md, "On the Fire signal only one boat of eight launches"): a signal often
     fired nothing, for three reasons, none of them the crippled boats first blamed. The tubes rest on one beam and
     trained across from it after the turn (a V25's 120° took 75-95 s, past `AttackFireS`), so now the admiral trains
     every boat's tubes out on the beam the turn will bring toward the target as they run in (`TorpedoSystem.Train`;
     a boat holds her fire until the turn brings the aim to that side). A launch whose tubes no longer bore once
     trained was dropped while the captain counted the signal answered: it's answered now only once torpedoes have
     gone (`LauncherState.FiredMs`) or while one waits for its tubes. And the track's check counted the boats steaming
     beside her as on it: it's judged as the ships move now (`LineOfFire.TorpedoFoul`, below). `flotilla` with the
     line holding on, seeds 1-30: the signal made in 17 (as before); two boats or more firing on it in 6 → 15 (of the
     other two, one's signal comes as the 30 minutes end); torpedoes 85 → 116. The Jutland (one seed): torpedoes 263 →
     509, own-fire losses 1 → 0.
5. **The screen and its counter-attack.** The unit order `Screen` (a unit, a distance; `ScreenM` 5 km), a group's
   (`AdmiralLayer.Screen.cs`; the task proper, with its end and hand-back, is 5.1c's): it keeps station off the screened
   unit's guide toward the nearest enemy in sight (ahead while there's none), given anew only when that moves it more
   than `ScreenMoveM`; while enemy torpedo craft close on the screened unit (the turn-away's threat test) it fights the
   nearest, and goes back to its station `TurnBackS` after the last. A screen doesn't take up doctrine's Engage nor turn
   away. A ship told to Screen keeps station ahead of the unit. `ScreenTests`: two light cruisers from a mile astern of
   a battle line are 1.5 km out on the enemy's side in 5 minutes, counter-attack the destroyers closing, and go back to
   their station. Placeholder: "Screen" the picked ship's squadron.
- **Evasion together** wasn't built as its own piece: a line under fire running away zigzags through its guide (5.1a's
  evasion on the guide's course), the line following in succession; a line fighting holds steady, as the research
  says inside 10 km. A zigzag by turns together is for the fine-tune.

**After 5.1b** (Release, 5 seeds; the Jutland one): `squadrons` as after 5.1a (time 3, decided 2; Britain loses 3.0 of
4); `flotilla` quiet 5 of 5 (Britain 0.2 of 4, Germany 2.2 of 6); Tsushima quiet 4, time 1 (Russia 5.2 of 13, Japan 4.2
of 14); the Jutland to its time (04:00), 15 British and 19 German ships lost, in 199 s (130 s after 5.1a, with tests running beside it: to be measured clean in 5.1f). Allocation-free and replaying (`ManoeuvreTests`, 4.6's flotilla test).

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

**5.1c done 2026-10-14,** built in six pieces, each committed when it played:

1. **Tasks, and the first five.** `Sim.Ai/Tasks/TaskLayer`, a layer above the admirals (`AiSystem`'s layers are tasks,
   admirals, captains): a unit's task, its state (`TaskState`: on task, fighting, in contact, done, can't) and why
   (`TaskReason`), in the hash and the snapshot (groups' and ships' `Task`, `TaskStatus`; group labels show it). A
   task turns into orders the chain already has, given to the unit as its own: a new source, `OrderSource.Task`, which
   the AI system hands the chain as the player's, so the player's own order to the unit ends its task (a speed is the
   task's speed instead). A unit in a squadron's or force's line is detached (pinned) for its task. A fight doctrine
   takes up interrupts a task: it waits ("fighting") and goes on once no enemy has been in sight of the unit for
   `TaskResumeS`; falling out ends it. Done or can't, the unit holds what it was last given and its state is the report
   up (decided at the start of 5.1c: until 5.1d's commander, nobody gives the next). GoTo (done within `ArrivedM`),
   Patrol, Intercept (a lead course on the picture's estimate, re-solved each think; done once the contact is in sight
   within `FightCompareM`, then doctrine), Join (closes at all the unit makes; a ship into a squadron, a group into a
   force, a squadron alone forms one with the other, one detached from it back in; done within `JoinM`) and Withdraw
   (from any enemy in sight first, then its point or home; takes up no fight). `TaskTests`: a squadron at a point 20 km
   off in 34 minutes; intercepting an enemy its scout holds from 50 km, in contact after 43 minutes; a detached
   squadron rejoins its force and one alone joins it in 22 minutes; a ship withdraws from an enemy and then home.
2. **The scouting tasks, Screen and Cover.** Scout (a station `ScoutM`, 20 km, ahead of a unit; an enemy too strong for
   it, by `Strength` against doctrine's outgunned ratio, is shadowed, the task "in contact", no fight taken up, even
   one doctrine already took up; back to its station on losing him), Search (a group spread abreast at
   `SearchSpacingShare` of the visibility, in sight of each other, steams to the point and a ladder about it on the
   course it came in on, its tracks a line's width apart; done on contact or at its end), Shadow and Screen (the
   player's orders now tasks, with an end), Cover (a station `CoverM` off the unit toward the nearest enemy its navy
   holds anywhere). `Knowledge/Strength` is the one strength measure (heavy guns, as each contact is taken); Fight's
   outgunned test reads it, hashes unchanged. Found on the way: a captain's station dead ahead (abeam 0, a negative
   distance astern) took the line-ahead track and never got there; fixed. `ScoutingTests`: a scout 20 km ahead at 90
   minutes, shadowing battlecruisers it meets; a scouting line of four light cruisers finds an enemy crossing its area
   in 5 seeds of 5; a cover keeps 8 km toward the enemy.
3. **The tactical picture and the deployment.** `Knowledge/TacticalPicture`: a navy's contacts in sight chained into
   enemy groups within `TacticalGapM` of each other whatever their type, each with its centre, course, strength,
   capital ships and van; built once a moment a side through the grid. A force in a cruising order with doctrine's
   `Deploy` (the default preset on, neutral off) meeting the main body (the strongest group with a capital ship) within
   `DeployM` forms one line ahead, every level of its tree, its groups as far apart as their ships:
   - *the course:* square across the enemy's course, toward his track, so the line lies across his way ahead of him
     (his T); across his bearing when he's stopped.
   - *the leading wing,* changed from the plan's "nearer the enemy's van": the column furthest along that course,
     the one on the side the line turns to (Jellicoe's port wing). Led by the wing nearer the van the other columns lay
     ahead of the leader on his new course and had to drop back by speed (30 minutes and more for four columns).
   - *while it forms,* every column keeps its final place on the line's guide directly, along his track; a chain of
     stations on the column before lagged column by column. It takes up no fight, and its task waits, until every ship
     is within `DeployFormedM` of her place (or `DeployMaxMs`); then doctrine takes the line into the fight.
   `DeploymentTests`: four columns of three dreadnoughts in one line in 14 minutes (Jellicoe's six took about twenty),
   steering across the enemy's bow, the line's guide dead ahead of his van at 14 km.
4. **Falling back on the main body.** A unit Fight turns away from a stronger enemy runs for the nearest own force
   within `FallBackM` that's a match for him, if that's within `FallBackDeg` of straight away (Beatty's run to the
   north); else straight away. `FallBackTests`: two dreadnoughts outgunned by six to the south-west run north (5°) for
   their fleet; alone, north-east (52°).
5. **Opening tasks in the scenario** (agreed at the start of 5.1c: a slice of 5.1d's plan, built early): a top unit's
   `"task"` (`"do"`: go to, patrol, search, join, scout, screen, cover, shadow, withdraw; `"of"` a unit by its name at
   any level, `"at"`, `"distance_km"`, `"off_bow"`, `"kn"`), given once every unit is placed; one naming a unit that
   isn't there (a ladder took part of the Jutland) is a warning. The Jutland's: the 5th Battle Squadron covers the
   Battlecruiser Fleet, the flotillas screen their fleets, the cruisers, light cruisers, the 3rd BCS and the 2nd and 4th
   Scouting Groups scout ahead of theirs at their historical distances; the main bodies and Beatty's light cruisers
   steam as placed, so the first contact is history's. The bench's timeline tells forces deploying and tasks reporting.
6. **The Jutland,** with its opening tasks (Release, one seed): the light cruisers' fight from 14:24 as before; the 2nd
   Scouting Group in contact (shadowing) at 15:11, the 3rd BCS's light cruisers at 16:32, the cruiser screen and the
   3rd BCS at 18:43-44, the 4th LCS at 19:08; the Grand Fleet deploys into line of battle at 19:12 and the High Seas
   Fleet at 19:43; to its time, 18 British ships lost (225,000 t) and 13 German (95,000 t), in 169 s. Found by it: the
   deployment fired on every force in a cruising order (Beatty's light cruiser screen, the cruiser screen, both
   torpedo-boat forces, the 3rd BCS scouting); only a battle force deploys now (two capital ships or more in its line,
   not scouting, screening, shadowing, searching or withdrawing). With deployment but before the opening tasks, the
   Grand Fleet deployed at 18:13 (history's 18:15) and Britain lost 38 ships. Ships stationed on another unit (scouts,
   screens) stay on the fine tick, so the first hour runs at ~900x with ~200 ships fine: for 5.1f's cost.
   `squadrons`, `flotilla` and Tsushima play as after 5.1b (5 seeds each).

**Still open after 5.1c** (for 5.1d and the fine-tune): nobody gives a finished task its next one (5.1d's commander);
Intercept can't be given at the start (a contact must be held); the scouts shadow what's too strong for them but report
only by the picture (instant, decision 2); flotillas screening never attack the enemy's line (the commander sends them
in); crossing the T is now the deployment's course, but a deployed line's Fight still keeps the enemy on its beam and
doesn't hold the T.

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

**5.1d done 2026-10-14,** built in five pieces, each committed when it played (2-4 together):

1. **The layer, its switch, plans and idle units.** `Sim.Ai/Commanders/CommanderLayer`, a fourth layer above the tasks
   (`AiSystem`'s layers: commanders, tasks, admirals, captains), one commander per side, thinking every
   `CommanderPeriodMs` (60 s). He commands his side's top units (groups that are no force's, ships in no group) by tasks,
   given as the player's are but from `OrderSource.Commander`, by each unit's role: the main body (the strongest with heavy
   ships), other heavy units, scouts, flotillas (most of its ships torpedo craft) and auxiliaries (no main battery).
   - *Who he commands,* changed from the plan's "off for the player's side" alone: a world told nothing has no commander
     (`CommanderMode.None`), so dev worlds and every test before him play as they did; a scenario gives every side one
     that's on unless it's the player's (`Auto`: watching, every side's on), and the logged `Commander` command sets a
     side's mode. Once he's thought, the player's own order to one of his units (a move, or a task) takes it from him, as
     a held unit is from its admiral (2.6); `Automate` hands it back ("leave it to the admiral") or takes it.
   - *Plans:* tasks queue (`UnitOrders.Then`), each begun when the one before ends, done or not; a scenario's `"tasks"`
     list is a top unit's plan, its first given at once. A unit's plan is the task layer's, so it runs on the player's
     side too; a new task in place of a plan drops what was to come.
   - *Idle units:* one whose plan has run out gets its role's task (the main body searches ahead on its course, another
     heavy unit joins it, scouts scout ahead of it spread 0°, ±45°, ±90° off its bow, flotillas screen the nearest heavy
     unit, auxiliaries go home). A unit on the scenario's own orders is on its orders until the enemy's met; after that
     (they were for the approach) it counts as idle too.
   - Placeholders: "AI commands" beside the spawn panel's "watch" for the player's side; a top group's "leave it to the
     admiral" / "take it from the admiral", and "· AI" / "· yours" on its label.
2. **Contact and battle.** His target is the strongest fresh contact (`ContactFreshMs`, 30 minutes out of sight: older
   is lost, and Intercept now gives such a contact up too), the nearest his main body of those as strong, kept until it's
   lost or another is `CommanderRetargetShare` (1.5) times as strong. Every heavy unit not in contact closes it
   (Intercept), unless it's on a task with a unit (scouting, covering, screening); one in contact is doctrine's. Heavy
   means a capital ship or main guns of `HeavyGunMm` (8") or more: Tsushima's battleships are under 14,000 t, and
   Kamimura's armoured cruisers were a battle line; with only capital ships, both fleets there were "scouts" an hour in
   and nobody closed. In battle his flotillas with torpedoes are sent in (`TaskKind.Attack`, the flotilla's attack as a
   task: done once the torpedoes are spent, can't once beaten off, and then never sent again) on an enemy line worth
   `CommanderAttackStrength` (one 12" gun) that his heavy units engage, within `CommanderAttackM` (25 km); a force of
   flotillas under a light cruiser sends in its squadrons, which rejoin it afterwards. The player's Attack is a task now
   as well.
3. **The balance turns.** *Declined* (outmatched): the strength he holds fresh past doctrine's outgunned ratio of his own
   by `CommanderMarginShare` (25%), accepted again once it's under by as much; a side of light forces declines nothing
   (its flotillas attack by doctrine). *Broken off,* for good: losses past doctrine's new `BreakOffShare` (the default
   preset a third of the tonnage he came with; neutral never). Either way every unit goes home (Withdraw), a flotilla near
   the enemy sent in once to cover a break-off. *Lost:* the enemy met and no contact fresh: his main body searches about
   where he was last held (the picture's estimate) on `LostSearchLegM` (20 km) legs, and with nothing found he gives up:
   every unit home. A new end, `EndReason.Done`: every side afloat has its aims, or has given up or broken off with every
   ship afloat home.
4. **Night.** Doctrine's `NightAction` (default preset "avoid"; neutral "accept") and `NightAttack` (1; neutral 0): with
   the enemy held and the sun more than `NightSunElevationDeg` (6°) down at his main body, his heavy units are withdrawn
   (home, free to close again at dawn), his scouts keep their tasks, and his flotillas go in on any heavy enemy in
   reach, engaged or not.
5. **The Jutland's plans** (`"tasks"`): Beatty north to the rendezvous with Jellicoe in 57°45'N 4°15'E and into the
   Grand Fleet; Jellicoe to the rendezvous and on toward the Horns Reef; Hipper north into the Skagerrak, Scheer on the
   same track fifty miles astern. History's first contact (Galatea and B109 at 14:20) comes in the first quarter hour,
   so the plans give way at once to the commanders' rules: both main bodies close what their scouts hold.

**The bench** prints the commander's phases and orders on the timeline, a census of each side's top units at the end (on
a task, home, on their first orders, idle, fallen out), and the AI layers' cost.

Found on the way:

- A withdrawal waited on a fight taken up after it, and the commander gave it again every minute; a withdrawal now never
  waits on a fight, and gives its run or its route home again if one took over.
- A squadron that's its force's guide couldn't rejoin the force ("join can't: gone"); it resumes its place now.
- An admiral's first route allocated (his waypoints grew from nothing); room for eight is made with him.
- Regensburg and the other light cruisers with 4.1" guns are torpedo craft by 4.6's rule (no gun of 150 mm or more), so
  they're sent in to attack with their flotillas. Left as it is (a classification for the fine-tune).

**Checked** (Release; `CommanderTests`): who commands (none in a world told nothing; a scenario's every side but the
player's; the player's order takes a unit; "leave it to the admiral" hands it back); a plan of two go-tos and then the
main body's search; units through with their tasks given their roles; a side holding an enemy 60 km off closes him in
an hour, its scout left shadowing; a flotilla sent in on an enemy line its own line engages, torpedoes in the water in 18
minutes; an outmatched side declines and is 22 km nearer home an hour on; a side that loses a faster enemy intercepts,
searches where it was, gives up and goes home; losses past the share break a side off; by night ("avoid") the heavy
squadron is withdrawn and the flotilla sent in, and with "accept" it's a battle. Allocation-free (4.6's flotilla test) and
replaying (Tsushima, 4 hours with a commander on). The commanders cost 0.05% of the Jutland's run, 85 µs a think once a
game minute (the captains 23%, the admirals 4%).

**After 5.1d** (Release, 5 seeds each):

| Scenario | How it ended | Losses |
|---|---|---|
| `squadrons` | time 5 (6:00) | Britain 2.2 of 4, Germany 0.0 of 5 |
| `flotilla` | time 5 (5:00) | Britain 0.2 of 4, Germany 3.2 of 6 (6.0 torpedoes a run, from 1.6) |
| `tsushima` | quiet 5 (after 13:14) | Russia 7.0 of 13, Japan 5.0 of 14 |
| the Jutland | time 5 (04:00; 114-153 s a seed) | Britain 29.4 of 151 (19-35), Germany 27.0 of 99 (20-35); after 5.1c, one seed: 18 and 13 |

- **The Jutland meets in every seed,** the main bodies too: both commanders close from the first contact, the Grand Fleet
  deploys (20:09 in seed 1916) and the High Seas Fleet after it; both withdraw their heavy ships by night (21:12 to about
  02:00) and close again at first light. Germany doesn't decline the Grand Fleet: the British strength it holds is
  about twice its own, under the default preset's outgunned ratio with the margin. A German preset that avoids the whole
  Grand Fleet (history's aim) is the presets' fine-tune.
- **Nothing idle:** at the end of the Jutland neither side has a unit idle or still on its first orders (on a task, home,
  or fallen out).
- **The small scenarios now pursue:** the side that's won chases the beaten one's survivors and cripples (the 2nd Battle
  Squadron the limping torpedo boats; Hipper Beatty's survivors) and doesn't end quiet within the scenario's time.
  `squadrons` before 5.1d decided 2 of 5 by sinking all four British ships; now Britain breaks off after losing two and
  gets away. Ending by a scenario's own rules is 5.1f's bar.

**Still open after 5.1d** (for 5.1f and the fine-tune):

- Finishing cripples: a stopped or silent enemy in sight is fought at doctrine's range for hours (the Germans and a
  rudderless Tiger in `squadrons`); a commander's (or a Fight's) "finish her" from the enemy's seen fitness is a
  deepening.
- His target is one contact, the strongest; the enemy held out of sight isn't grouped (the tactical picture is in sight
  only), so the Grand Fleet closes a light cruiser until a battlecruiser is held.
- By night every heavy unit goes home; keeping between the enemy and his home (Jellicoe's night) is a fine-tune.
- Navy presets (the Germans' outgunned ratio against the whole Grand Fleet) and the aims Avoid and Reach.
- A flotilla whose torpedoes are spent stays in the gunfight while it's in contact, instead of falling back at once
  (for 5.1g's third piece).

**The order from here** (the user, 2026-10-14, after 5.1d): with something to play, the next work is **5.1g** (the
matchup, below), then the **UI rework** (5.2, from the command UI research; the placeholders so far are clunky), then
the **VFX pass**, then the **detail passes** on the systems (fighting.md's fine-tune tier, doctrine presets, the AI
playing well). **5.1e** (signals) and **5.1f** (the whole) come late, after those. 5.1g is numbered after them since it
was added last (as fighting.md's 4.11), and built first.

**5.1g The matchup: who can fight whom.** (Agreed with the user 2026-10-14; **done 2026-10-15**, below.)

*Why.* Two rules are absolute today. The commander's roles: a unit is heavy by main guns of `HeavyGunMm` (8") or more,
only heavy units close the enemy, and a side with no heavy unit declines nothing. And `Strength`, one number per ship
(main guns × (calibre / 12")³), which Fight's outgunned test, a scout's judgement and the commander's balance compare:
it ignores torpedoes, armour and who the opponent is. A light cruiser's 6" counts the same against a destroyer as
against a battleship, and a flotilla counts for nearly nothing, so torpedo craft press home only through a special case
(4.6: they close whatever guns they face). What the user asked for is relative:

- a light cruiser against a destroyer should be confident, and attack it;
- light cruisers without torpedoes should avoid battleships like no tomorrow;
- a pack of destroyers can challenge a battleship, and so can a squadron of light cruisers with torpedoes.

*The measure.* Not a strength but a matchup: how fast A can sink B, and B sink A, and the ratio of the two (how many of
B's ships A sinks for each of its own lost). One pure function, read by everything that judges a fight:

- **Guns,** A on B: per battery, guns × rounds a minute × the chance of a hit on B (B's size, A's fire-control level,
  at A's fighting range) × what a hit does to B (the shell's effect, times whether it gets through B's belt, deck and
  turret faces at that range: the penetration fit of `Terminal`), over B's toughness (the hits that sink her, an
  expectation from her displacement and reserve buoyancy; the damage research's warning stands: hit count is a poor
  predictor of one ship, an average over many is what this needs). A 6" that can't pass a battleship's belt does little to
  her and a great deal to a destroyer.
- **Torpedoes,** A on B: tubes and torpedoes × the chance of a hit (B's size and speed, the torpedo's speed and range) ×
  the warhead against B's toughness and torpedo protection, times the share of A's boats that get to firing range
  through the fire of B's guns that bear on them (secondaries at their full rate, the mains at less) over the run in.
  B's defence is shared among A's boats, so numbers saturate it: one destroyer against a battleship dies on the way in,
  a flotilla of eight gets there. Against small, fast ships torpedoes count for little, as in the era.
- **Groups:** summed over the classes on each side (a handful), each side's fire spread over the other's ships by
  tonnage, never ship against ship pairs. A class's profile (its batteries, protection, size, speed, torpedoes and
  defence against craft) is compiled once with the class; a pair of profiles is a few multiplications.
- **What's known:** own ships exactly; the enemy as the picture takes him (the class he's taken for, a type's profile
  until it's made out, replacing `FightTypeGuns`), so a mistaken class misleads, as it should.
- **Speed** is its own output, not folded into the ratio: who can force a fight and who can refuse it. Declining only
  works for the faster side; a slow unit that can't get away fights.
- **Confidence** is the ratio against doctrine's `OutgunnedRatio` (scaled by aggression, as now).

*v0 is analytic.* The coarse tier (fighting.md 4.8 and 4.9) is to build exactly this kind of table by class pair (hits to
sink, hit rates by range) by simulating the fine model. v0 is a formula over the class data compiled already; its
inputs are the ones the coarse tables will fill, so they can replace it later without changing its readers.

*What it replaces* (one implementation; `Strength` goes):

- Fight's outgunned test, and the turn-away when outgunned.
- A scout's "too strong for it" (Scout shadows instead of fighting).
- The commander: his target (the enemy group his side can best take on, or the gravest threat); who closes it (every
  unit confident against it, not "heavy" units: `HeavyGunMm` and the heavy role go, the main body stays as the
  strongest, for support); declining (his side against what he holds, and only if he can get away); sending a flotilla
  in (when its matchup against the enemy line says so: `CommanderAttackStrength` goes).
- The torpedo craft's exception in Fight: the matchup says when a pack presses in.
- Left as they are for now: the tactical picture's capital ships (the deployment's "main body") and the turn-away from
  torpedoes' threat test (torpedo craft closing).

*The pieces.*

1. **A class's profile and a pair.** `Sim.Combat/Matchup` (pure, beside `Terminal` and `Fitness`): profiles compiled with
   the class, and A against B: each side's rate on the other, the ratio, the terms behind them. A bench, `shipgen
   matchup A[:n] B[:n] [--range M]`, prints them. Checked on the cases above (a light cruiser and a destroyer; a light
   cruiser without torpedoes and a dreadnought; one destroyer and a dreadnought; eight destroyers and one; light cruisers
   with torpedoes and a dreadnought) and on even ones (a dreadnought and her sister about 1; Lion and Derfflinger; Queen
   Elizabeth and Iron Duke); the hit chances against `shipgen duel`'s, the toughness against `shipgen hits` and
   `shipgen torpedo` (the hits that sink, on average).
2. **Groups and what's known.** Group against group from their classes and counts, own exact, the enemy as taken for,
   types' profiles for contacts not made out; and the speed output. Tests: the saturation (n destroyers against one
   battleship, the ratio rising with n past one), a mistaken class misleading, the same answer both ways round.
3. **Onto its readers.** Fight, the scout, the commander (target, closing, declining with speed, flotillas sent in), the
   torpedo craft's exception; `Strength`, `HeavyGunMm`, `CommanderAttackStrength` and `FightTypeGuns` retired. Measured:
   `squadrons`, `flotilla`, Tsushima and the Jutland over 5 seeds against the numbers after 5.1d; the cases the user named
   played out (a light cruiser hunting a destroyer and running from a battleship; a flotilla going in on a battleship);
   allocation-free; replays; cost.

   Also here, found after 5.1d: **a spent flotilla lingers.** Once its torpedoes are spent, its Attack task is done and
   its admiral keeps fighting with its guns (4" against whatever's there). The commander gives an idle unit its next task
   only once it's out of contact (no enemy within `FightCompareM`, 30 km), so a flotilla with no torpedoes left stays in
   the gunfight until the battle moves off, instead of falling back to screen its heavy units (or rejoin its force) at
   once. The fix: an Attack done by its torpedoes (`TaskReason.Fired`) hands the flotilla straight back to its screen,
   withdrawing from the enemy in sight first, contact or not; and the matchup then says whether a flotilla with only its
   guns is worth a fight at all.

*Not in it:* the coarse tables themselves (4.8), so no simulating fights ahead; the matchup is a judgement of the
odds, not a forecast of the battle.

*Decided at the start* (the user, 2026-10-14, on the questions; folded over the text above where they differ):

- **The measure.** Guns: the fire-control research's quick hit chance (§8.3: the hitting space from the class's length,
  beam and height and the fall angle from the range table; the MPI error by the battery's fire-control level from §12.2,
  with the gun's dispersion), scaled to `shipgen duel`; a hit's effect is the shell's weight or filler times whether it
  passes the belt, deck and turret faces at that range (`Terminal.PenMm`). Toughness was to be the hits that sink;
  **measured, it's the hits that put a ship out of the fight** (lost, or past doctrine's fall-out), since the fine model
  almost never sinks a ship by gunfire (piece 1 below). From displacement, checked against `shipgen hits` and
  `shipgen torpedo`.
- **Confidence follows the square law:** the share of B's tonnage A puts out of the fight over the share of its own it
  loses in the engagement, so twice the ships is about four times the confidence; `OutgunnedRatio` is retuned to it.
- **The range is the faster side's:** the matchup is judged at a few candidate ranges (torpedo range, each side's
  doctrine range); the side faster by a margin gets the one best for it, and at about equal speed it's the judge's own
  doctrine range. Destroyers can force torpedo range on a battleship; a battleship can't hold a faster cruiser off.
- **Fire goes where it tells:** a group's batteries are spread over the enemy's classes by what each does to them, not
  by tonnage, so a light cruiser squadron's 6" goes to the destroyers in a mixed group.
- **Night shortens the run-in:** the defence's guns open on torpedo craft only within the night's sight range, so a
  night attack is cheap by the matchup itself.
- **Damage counts, and cripples are finished:** own ships by their fitness exactly, enemies by what's seen (silent: no
  guns; stopped: can't get away; low or listing: less toughness left). Fight closes in on an enemy that can barely hurt
  it, so a silent cripple is finished rather than fought at doctrine's range for hours (5.1d's open item).
- **Contacts not made out** are the average of the enemy side's own classes of that type in the registry (the user's
  pick over a reference design per type: closer to the truth, though it knows which classes are at sea).
  `FightTypeGuns` goes.
- **Two bars:** the commander sends units after an enemy only when they're confident (a new doctrine field,
  `SeekRatio`, 1 by default: expected to win); a unit that meets an enemy fights down to 1 / `OutgunnedRatio` before
  turning away. Both scaled by aggression.
- **Targets are assigned per enemy group:** the enemy groups ranked by threat, each given the nearest own top units
  confident against it until together they clear the bar; a unit confident against none stays with the main body (a
  scout shadows). Light cruisers go for the destroyers while the battle fleet goes for the battle fleet. The main body is
  still the top unit with the most capital ships; "a match for him" (falling back) and "the gravest threat" are matchups.
- **No escape:** an outmatched unit or side that can't get away doesn't decline. Fight runs only where running gains
  (opens the range, or buys time: night coming, supports near, as the fall-back already looks for), else holds
  doctrine's band and fights.
- **Ammunition counts** (the user, after the questions): a battery's rate on the enemy is capped by the rounds its
  mounts have left (what it can still deliver, not only how fast), own ships exactly, enemies at their class's full
  loadout (rounds fired aren't seen). And **a ship whose magazines are empty withdraws:** main batteries out of rounds and
  no torpedoes left, she leaves her group and goes home, as falling out does, with her own reason ("out of
  ammunition"); her secondaries fire at will on the way. A mount with no rounds given (0 in the class: unlimited) never
  runs out.
- **Left as they are:** the torpedo-craft classification (Regensburg's 4.1" counts as one, 5.1d), since the turn-away's
  threat test still reads it; `DesignerPlaceholders.HeavyGunMm` (the shell mix), which isn't the commander's.

**Piece 1 done 2026-10-15: a class's profile and the matchup** (`Sim.Combat/Matchup.cs`; the bench `shipgen matchup
A[:n][:t0][,...] B[:n][,...] [--range M] [--share 0.6] [--sight M]`; `MatchupTests`). Fitted to the fine model's own
benches first, so it judges the game's physics, not history's:

- **The measurements.** `shipgen hits` and `shipgen torpedo` print "out of the fight" now (lost, or past the default
  preset's fall-out after 30 minutes of flooding and fire). The fall-out test is one implementation for that and the
  captain (`FallOutRule` beside `Fitness`). Shellfire almost never sinks: three 13.5" hits leave a destroyer afloat, and
  nearly every loss is a magazine. What it does is cripple, slowly: four 6" HE hits put out a quarter of the V25s, five
  13.5" AP hits 13% of the Arethusas (they pass through or break up), sixteen 12" AP hits at 9 km 4% of the Iron Dukes.
  So toughness is a near-constant chance a hit puts her out, and a rate-based matchup suits it.
- **A hit's chance:** the research's quick formula as it stands (§8.3 with §12.2's errors by level, the gun's pattern,
  the target's length, beam and 1.3 x freeboard): within about 1.5x of `shipgen duel`'s 15-minute rates on every pair
  tried, with no fudge factor (Iron Duke on Derfflinger 4.1% at 14 km against the duel's 4.7%).
- **What a hit does:** 1 - exp(-28 x sqrt(shell kg) x share / displacement t), the share 1 inside, 0.15 kept out, 0.4
  through plating too thin to arm a base fuze, a shell through armour as its quality's outcome row has it (rn1916's
  break up 40%), and a burst where there's no armour (upperworks, ends) by its splinters through the deck under it,
  exp(-deck mm / 25). The shell's size goes as the square root of its mass, not its filler (an 88 mm and a 6" hit on a
  destroyer, 3.4% and 7.2%). Ten measured pairs within 2x, most within 0.6-1.0x; the weakest is 6" HE on a battleship
  (0.13% against 0.085%). Torpedoes: 1 - exp(-48 x charge kg / displacement) (one hit puts out 83% of the destroyers,
  71% of the light cruisers, 18% of the battleships); hit chance 900 m / range (at most 0.4) x sqrt(length / 150 m) x
  0.6 for combing x how much slower than the torpedo the target is, against an 18 kn one (`shipgen torpedo`: 35% at
  3 km, 18% at 5 km against a steady target).
- **The engagement:** the lines fight at a range for 30 minutes, every ship firing one salvo of torpedoes within their
  range; a side's ships built for torpedo attacks (0.5 tubes bearing a thousand tonnes or more: destroyers and torpedo
  light cruisers, not battleships with four submerged tubes) may attack: in from where the defence's guns open (their
  reach, or the sight by night) to their firing range at their speed, one salvo from the boats still in it, and out
  again, the defence's guns choosing between them and the line by what they do. Each battery fires where it does most,
  at the range between shooter and target, capped by its rounds. A's confidence is the share of B's tonnage out over
  the share of its own. The lines fight at the faster side's best gun range (else A's own); each side attacks if that's
  better for it.
- **The cases** (`MatchupTests`; A's confidence):

  | A against B | Confidence |
  |---|---|
  | Arethusa against a V25; the V25 against her | 3.3; 0.31 (the same answer both ways) |
  | Arethusa without torpedoes against Iron Duke; four of them | 0.07; 0.63 |
  | four Arethusas with their torpedoes against Iron Duke | 2.4 |
  | 1, 2, 4, 8, 16 V25s against Iron Duke | 0.11, 0.34, 0.91, 2.1, 4.4 |
  | one V25 against her by night (sight 2 km) | 0.26 |
  | Iron Duke against her sister; with four V25s each | 1.0; 1.0 |
  | Lion against Derfflinger; Iron Duke against Derfflinger | 1.09; 1.13 |
  | two Iron Dukes against one | 3.5 |
  | Iron Duke with her main magazines empty against her sister | 0.04 |

- **Not as hoped:** four gun-only light cruisers against a battleship aren't hopeless (0.63), since in the fine model a
  13.5" AP hit puts a light cruiser out only 3.5% of the time: the battleship fires AP only. Choosing the shell by target
  is fighting.md 4.5's, where this and the capital ships' toughness are noted.
- **Cost:** 11 µs a judgement one class against one, 82 µs three against two; nothing allocated. The readers (piece 3)
  judge per pair of units and want it cached: a captain's Fight weighs every second.

**Piece 2 done 2026-10-15: groups and what's known** (`Sim.Ai/Knowledge/Matchups.cs`; `MatchupSideTests`). Piece 1
already judges groups (classes with counts a side, each battery's fire over the other's classes) and gives both sides'
speeds (each its slowest ship's) and who can refuse. What's added is building the sides from the world:

- **`Matchups`:** each class's profile, made once from the world's own range tables, and each side's census (its
  classes at sea by type, made once a moment it's asked).
- **`MatchupSide`:** a reused list of entries. An own ship goes in exactly: guns working by barrels and rounds left by
  the loadout (main and the rest), torpedoes left in launchers that work, speed (`Fitness`), and toughness by the water
  in her (1 - her reserve's share flooded). A contact goes in as taken: the class it's taken for once made out, else
  spread over what its side has of its type as fractional ships (before its side is made out, v0 counts it as nothing);
  as seen, silent is no guns, stopped no speed, and low, listing, trimmed and burning take its toughness down
  (`MatchupSeen*Share`). One seen going down isn't there. Whole ships of a class are one entry, a damaged one her own.
- **Found on the way:** both sides' torpedo salvos are fired at once now (a mirror match came out 1.19: B's salvo had
  been fired from what survived A's), and a ship must be under way to make a torpedo attack (a stopped cripple "attacked"
  at walking pace for hours).
- **Checked:** an Iron Duke with her main magazines empty against her sister 0.04; Arethusa against a V25 taken rightly
  3.3, taken for a battleship 0.31; a capital-ship contact not made out among two Iron Dukes and a Derfflinger is 2/3 of
  an Iron Duke and 1/3 of a Derfflinger; Arethusa against a sister seen silent, stopped and low 73.

**Piece 3 done 2026-10-15: onto its readers.** `Strength`, `HeavyGunMm`, `FightTypeGuns` and `CommanderAttackStrength`
are gone; one judgement every layer asks (`Knowledge/MatchupJudge`: a unit against the enemy in sight round it, once a
moment a unit; or any two sides a layer builds), and the enemy held in groups in sight or not (`Knowledge/HeldGroups`).

- **Fight** turns away under doctrine's bar (1 / `OutgunnedRatio`), back once well over it (`FightRallyShare`). With no
  escape (the enemy as fast) it runs only to a force of its own that's a match for the enemy (the fall-back, judged
  the same way), else fights. A torpedo craft attacks when the judgement has its side attacking, or when it's ordered
  to, or in its flotilla's attack (its torpedoes held for the signal): whoever ordered it judged. One that can hardly be
  hurt (`FightFinishConfidence` 10, and under `FightFinishLostShare` 2% of its own tonnage lost) closes to
  `FightFinishShare` 0.4 of its band to finish a cripple.
- **The scout** shadows what it's no match for.
- **The flotilla's attack** taken up by doctrine (not ordered) is begun only if its matchup has it attacking at the bar;
  once begun it's seen through.
- **The commander:** roles by capital ships (`Sightings.TypeOf`), the main body the most capital tonnage. Each think:
  his side against all he holds (declined under the bar by his margin, and only if he can get away; accepted again over
  it, or caught); the held groups gravest first, each given those closing it and then the nearest free units not
  hopeless against it alone, until together they reach doctrine's `SeekRatio` (1: expected to win), else nobody
  closes it and those closing it are freed; his target (phases, the lost search) the biggest ship of the gravest group.
  Flotillas are sent in where their attack's matchup is at the bar (by day on a group his heavy units engage; by night
  any in reach). A flotilla whose torpedoes are spent goes back to its screen or its force at once.
- **Empty magazines:** a ship with every main mount out of the rounds it carries and no torpedo left goes home
  (`CaptainReason.OutAmmunition`, "her magazines empty" on the timeline).
- **Doctrine:** `SeekRatio` (default preset 1; aggression shifts it by -0.5); the tactical picture's groups carry their
  tonnage as taken (the deployment's main body is the biggest group with a capital ship).

Found on the way:

- A contact not yet made out to its type counted as nothing, so a lone dreadnought saw no enemy and closed four battleships:
  it's the average of everything the other sides have at sea now.
- The matchup's torpedo salvo was a ship's whole broadside; the captains fire `TorpedoSpread` (2) a firing, and so does
  it now. Attackers running in and out zigzag: the defence's fire on them hits `MatchupRunInHitShare` (0.4) as often.
  Six boats against four battleships came out at 0.21 before, 0.13 now: the fine model's flotilla loses about half its
  boats a run and hits little, so by day the matchup keeps it back.
- Finishing a cripple first fired on any far weaker enemy: a battle line closed on the torpedo boats attacking it and spoilt
  their attack. Hence the bar on the losses expected too.
- A captain's track of the ship ahead (512 points, about 13 KB) was made the first time he followed in line ahead,
  inside a think; it's made with the captain now. The flotilla's replay test warms up to its first attack (the first
  turn together grows a pool once).
- Tests whose premise the rules changed, rewritten to the rules: Fight's turn-away needs a faster ship (a battlecruiser
  from four battleships; a new test has a dreadnought with no escape fight); the fall-back alone, as fast as the enemy,
  fights (a faster pair runs); the commander declines with two battlecruisers against eight dreadnoughts (a new test:
  two dreadnoughts can't get away and fight); flotillas are sent in on a line of one dreadnought (a new test: kept back
  from four); by night the flotilla goes in on one battleship; the torpedo boat and flotilla benches order their attacks.
- **Ammunition runs out in long fights:** at Tsushima battleships and armoured cruisers on both sides empty their main
  magazines in 1.5-2 hours of fire at will (about 135 rounds a gun) and go home, so the battle thins out. The fine model's
  rate of fire (every gun at its reload, fighting at long range) is fire control's to tune (fighting.md 4.5).

- **Cost:** the judgements first cost the commander 32 ms a think (16% of the Jutland's run, 84 ms at the worst) and the
  captains 33%: the hit and out chances made again for every gun, target class and range. Each gun now keeps a table
  against each class by range step (the hit chance and the out exponent, linear between 250 m steps), made for every
  pair of classes at the scenario's load (`Matchups.Warm`): a judgement of three classes against two 82 µs to 13 µs.

Checked (`MatchupPlayTests`): Arethusa told to fight closes a V25 from 16 to 10.6 km in ten minutes, and opens from Iron
Duke from 16 to 24 km; eight V25s fighting a lone Iron Duke attack together (gain, run, fire, away); a commander sends his
battle squadron after three Derfflingers and his light cruisers after four V25s. Every sim test passes; allocation-free
and replaying (the flotilla's bench).

**After 5.1g** (Release, 5 seeds each; before the gun tables, which leave the Jutland's seed 1916 the same):

| Scenario | How it ended | Losses | After 5.1d |
|---|---|---|---|
| `squadrons` | time 5 | Britain 1.8 of 4, Germany 0.0 of 5 | 2.2, 0.0 |
| `flotilla` | time 5 | Britain 0.0 of 4, Germany 0.4 of 6 (4.4 torpedoes a run) | 0.2, 3.2 (6.0) |
| `tsushima` | quiet 5 (after 13:15) | Russia 4.2 of 13, Japan 3.8 of 14 (582 and 631 hits) | 7.0, 5.0 (1458, 1955) |
| the Jutland | time 5 | Britain 23.6 of 151 (20-29), Germany 26.6 of 99 (21-34) | 29.4, 27.0 |

- **Germany declines the Grand Fleet:** at 19:15 (seed 1916) its commander, holding the Grand Fleet, judges his side
  outmatched and faster, and every unit goes home: history's German aim, which 5.1d had left to a preset.
- **The flotilla is kept back:** six boats against four battleships by day are worth no attack, and the German commander
  declines.
- **Tsushima fights less:** its battle lines run out of main rounds (above).
- **Cost, the Jutland, seed 1916 with the tables:** played in 113 s (5.1d: 114-153 s); the commanders 3.1% of the run (4.2
  ms a think, at most 11 ms; 5.1d 85 µs), the captains 24.6% (554 µs a second; 5.1d 23%), the admirals 5.1%.

**Still open after 5.1g** (for 5.1f and the fine-tune):

- The commander's think is 4 ms at the Jutland (a judgement for every held group against every free unit, every minute):
  for 5.1f's cost, a cache of a unit's matchup against a group for a few thinks.
- The fine model's rate of fire empties magazines in two hours (fire control's tuning, fighting.md 4.5), and its
  capital ships are very hard to put out of the fight, and a battleship fires AP at light cruisers (both fighting.md 4.5).
- The matchup's own numbers are the fine model's as of 5.1g: when 4.5 retunes damage or fire, `shipgen matchup` against
  `shipgen duel`, `hits` and `torpedo` re-fits them.
- Doctrine presets by navy (the outgunned and seek ratios are where a navy's boldness goes) are the presets' fine-tune.

**The AI after the detail passes** (2026-10-20, at `e41ad571`: the shells, armour and damage plan to E8a, fire control
F0-F6, fire, shell contents, damage control and magazines since 5.1g; Release, the same 5 seeds each as after 5.1g; the
user: data to choose between 5.2, 5.1f and the matchup's re-fit):

| Scenario | How it ended | Losses | After 5.1g |
|---|---|---|---|
| `squadrons` | time 3, quiet 2 | Britain 1.6 of 4 (0-3), Germany 3.0 of 5 (2-4) | time 5; 1.8, 0.0 |
| `flotilla` | time 5 | Britain 0.0 of 4, Germany 1.4 of 6 (1.4 torpedoes a run) | time 5; 0.0, 0.4 (4.4) |
| `tsushima` | quiet 4 (after 13:53), time 1 | Russia 4.6 of 13 (2-6), Japan 3.6 of 14 (2-4, destroyers only; 818 and 1443 hits) | quiet 5 (after 13:15); 4.2, 3.8 (582, 631) |
| the Jutland | time 5 | Britain 28.6 of 151 (18-40, 85 kt), Germany 50.6 of 99 (33-60, 223 kt); 4110 and 4353 hits, 176 and 177 torpedoes | time 5; 23.6, 26.6 |

- **No Jutland reaches an end of its own** (5.1f's bar): every seed runs its 14 hours. In seed 1916 Britain gives up at
  01:30 and every unit goes home, but Germany is "retiring (outmatched)", which isn't one of the `Done` rule's ends,
  and ships still sink in the night (Tiger 00:41, Lapwing 03:07), so it's never quiet either. `squadrons` ends by its
  own rules in 2 seeds of 5, Tsushima in 4.
- **A commander who declined forgets it when contact drops** (bugs.md): withdraw, lost, search back towards the enemy,
  contact, outmatched, withdraw. Japan's commander at Tsushima (seed 1905) goes round it every ~35 minutes from 07:09 to
  the end at 02:00 (the seed that ran out of time); the High Seas Fleet four times between 16:48 and 21:09.
- **The same intercept is given again every think** (bugs.md): the High Seas Fleet "to intercept Valiant" every minute
  15:56-16:24 and "Lion" 19:46-20:03, the Battlecruiser Fleet "B97" 17:03-17:33.
- **Japan's battle line empties its magazines in 1.5-2.5 hours** at Tsushima (eleven ships between 06:13 and 07:09 in
  seed 1905, as after 5.1g), and then its commander, its heavy ships fallen out, is outmatched by the Russians it has
  beaten. It hits 1443 times a run and loses only destroyers; Russia loses 2-6 of 13.
- **Germany pays at the Jutland:** 50.6 ships lost (26.6 after 5.1g). Its commander declines at 16:48 in seed 1916
  (19:15 after 5.1g), but the whole 1st Scouting Group is lost (Britain's capital losses: Tiger alone, foundered at
  00:41). Destroyers falling out "too slow" are half of the falling out (27 of 54 in seed 1916, nearly all in the
  flotillas' melee 15:55-16:28).
- **The flotilla presses less:** 1.4 torpedoes a run against 4.4.
- **Cost** (the Jutland, seed 1916, two runs): played in 85-102 s (5.1g 113 s); the commanders 1.9% of the run (1.9-2.3
  ms a think, at most 9.8-16 ms; 5.1g 3.1% and 4.2 ms), the captains 18.6-19.2% (311-389 µs a second; 5.1g 24.6%), the
  admirals 3.6-4.0% (5.1%), the tasks 0.1%. Across the five seeds a run took 100-173 s, longer the bigger the fight.
  The same run's wall time moves ±20% between runs.
- Found on the way: a task ending after its group was gone (the IX Flotilla, its boats lost) was told as "?'s"; the
  timeline now keeps each group's side with its name (`ScenarioEnd`).

**After the decline holds** (2026-10-20, the user: the declining loop and the done end first, a piece of 5.1f):

1. **A decline holds.** Holding nobody, he keeps it (he goes on home and doesn't search back); and he remembers the
   enemy he declined (the gravest he's held since, `SideState.DeclinedFrom`): he accepts again only over the bar by the
   margin against both what he holds and what he declined, and fights if caught by what he can't beat. The first half
   alone wasn't enough: Japan then re-accepted against a lone destroyer, all of the Russian fleet it still held.
   `CommanderTests.A_decline_holds_out_of_sight_and_against_a_part_of_the_enemy_seen_later`.
2. **Done means going home and out of touch.** A side counts as done when it's given up, broken off or is declining,
   and each of its ships afloat is home or in no encounter ("The end"; `ScenarioEndTests.Sides_going_home_out_of_touch_end_it_done`).
3. **Declining by day, he still sends his flotillas in by night** (the user, asked when the flotilla scenario went
   silent): by doctrine (`NightAttack`), on a group in reach its attack is worth making on, and a flotilla going home
   from the declined battle counts as free for it (`CommanderTests.Declining_he_still_sends_his_flotillas_in_by_night`).
   Only a flotilla still in reach at dark: one gone home isn't sent back to look for him.
4. The bench prints what the done end waits on, for each side whose run didn't end done: its phase, its ships afloat
   home and not, in an encounter, stopped, the farthest from home.

| Scenario | How it ended | Losses | The baseline above |
|---|---|---|---|
| `squadrons` | done 2, quiet 1, time 2 | Britain 1.4 of 4, Germany 3.0 of 5 | time 3, quiet 2; 1.6, 3.0 |
| `flotilla` | quiet 5 (after 4:04) | Britain 0.0 of 4, Germany 0.2 of 6 (no torpedoes) | time 5; 0.0, 1.4 (1.4 torpedoes) |
| `tsushima` | quiet 3 (after 8:45), done 2 | Russia 4.2 of 13, Japan 3.4 of 14 | quiet 4, time 1; 4.6, 3.6 |
| the Jutland | done 1 (01:44), time 4 | Britain 28.2 of 151, Germany 48.8 of 99 | time 5; 28.6, 50.6 |

- **The loop is gone:** Japan declines once at Tsushima (07:09 in seed 1905) and goes home; the seed that ran out of
  time now ends quiet at 13:20. Every Tsushima seed ends by its own rules.
- **The Jutland's seeds still out of time are fights going on:** at 04:00 Britain is in battle or contact chasing
  Germany into the Bight (British ships up to 670 km from home), every ship of both sides not home in an encounter. No
  end should cut that off (decision 11); the limit is the bench's. Seed 1916 ends done at 01:44.
- **`squadrons`' two out of time** are the same: both sides broken off at 21:30 (seed 6) but still in touch, after 6 hours.
- **The flotilla scenario is silent:** Germany's commander declines the four battleships at 18:31 by day and its boats
  are ~100 km off by dark, out of the night rule's reach (`CommanderAttackM`, 25 km). Its torpedoes before came from the
  loop (declined, lost, searched back). The scenario wants a night start, an aim that asks for the attack, or a
  commander who sends flotillas out to find the enemy by night (the High Seas Fleet's after the Jutland): open.
- **Cost:** the Jutland's seed 1916, 87 s; the commanders 2.1% (2.6 ms a think), the captains 19.9%.

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
(as the designer's tabs were). Planned 2026-10-22 for the scenario battler in `docs/map-ui/battle-hud.md` (mockup
`docs/map-ui/battle.html`; steps U0-U7, U0 done). What step 5.1 leaves it to show:

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
| `Sim.Combat` | `Fitness` (a ship's, and an enemy's as seen), `FallOutRule`, `Matchup` (5.1g) |
| `Sim` (world) | Formations as lines of bearing in `GroupSystem`; signals in transit (world state: the AI's orders, on both sides) |
| `Sim.Ai` | `Doctrine` (AI state: only captains and admirals read it; built in 5.1a) and the sides' homes in `AiSystem`; `Commanders/` (the side's commander, aims, plans); the tasks in `AdmiralLayer` and `CaptainLayer`; `Knowledge/` the tactical picture, the held groups and the matchup's judge; `Captains/` evasion, falling out, combing tracks; `Admirals/` manoeuvres, the deployment, the flotilla's attack |
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
11. **Time limits are for testing the AI only** (the user, 2026-10-14, after 5.0). The release relaxes them a lot.
    Nothing is more annoying than nearly sinking the last enemy and being told "out of time": no admiralty ever
    ordered a captain back from finishing an enemy because a clock said so. So a scenario's end must never cut off a
    fight that's going on; ending by time is for runs nobody is watching to a conclusion.
