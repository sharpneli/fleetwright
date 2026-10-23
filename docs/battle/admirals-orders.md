# Plan: the admiral's orders, the player's and the AI's

2026-10-23. Status: **drafted** for the user. O1 (the split screen) and O2 (Deploy as an order), the AI's side,
**built 2026-10-23**; the UI steps (O3 on) wait for the user's answers at the end, and go mockup first, as the battle
HUD did (`docs/map-ui/battle-hud.md`).

Step 5.2's rest (`docs/battle/command.md` "5.2 The command UI on the map") grows out of this: the orders the player gives
as an admiral, and how the map offers them.

## The aim

The player commands as an admiral does: **orders about intent to squadrons and forces**, which the unit carries out by
itself. "Screen the battle fleet", "scout ahead of the battlecruisers", "the flotillas attack", "deploy on course
south-east", "intercept that contact": the squadron's admiral works out the stations, the turns, the counter-attack and
when it's done, and reports how it stands. The player can still steer anything by hand (the helm and the conn stay as
they are), but at the Jutland's scale that is the exception (decision 14 of command.md: the AI commands everything the
player doesn't take).

They are **the same orders the AI's side commander gives** (command.md's principle: every order is a unit order, one
way in). So the work is mostly the player's reach, not the AI's: the sim's vocabulary is largely built.

## What there is (checked 2026-10-23)

| Order | What the unit does | Built | The player gives it |
|---|---|---|---|
| Fight | closes to doctrine's band, holds the broadside, turns away when outgunned | 4.2c, 5.1b, 5.1g | the strip's task: Fight |
| Engage (concentrate) | the main batteries on one enemy; a group's spread over his line | 4.2c | right-click an enemy; the enemy card |
| Follow, Run from, Hold | | 2.4, 2.6 | the strip: Follow…, Run from…, Hold |
| Course, speed, route | the helm | 2.4 | the strip's helm; right-click the sea, Shift for waypoints |
| Search | a scouting line spaced by the visibility, a ladder about a point | 5.1c | the strip: Search… |
| Withdraw | breaks off and goes home | 5.1c | the strip, at a unit's level |
| Form | formation, spacing, a line of bearing | 2.4, 5.1b | the strip's formation popup |
| Turn together, battle turn | every ship at once; about, rear first | 5.1b | placeholder buttons in the squadron popup |
| Attack | a flotilla's torpedo attack: gain, run in, fire on a signal, away | 5.1b | placeholder "torpedo attack" (the biggest in sight only) |
| Screen | stations off a unit toward the enemy, counter-attacks torpedo craft | 5.1b, **O1** | placeholder: ctrl+click a ship, then "Screen X" in the squadron popup |
| Join | closes and joins a squadron or force | 5.1c | the same placeholder, "Join X" |
| Scout, Cover, Intercept, Shadow, GoTo, Patrol | the tasks | 5.1c | **none**: scenario files and the side's commander only |
| Deploy | a force from its cruising order into one line of battle | 5.1c (doctrine only), **O2** | placeholder "deploy" (O2) |
| Doctrine | the standing rules, per side and unit | 5.1a | the dev panel's aggression slider only |
| Leave it to the admiral | the unit to the side's commander, or taken back | 5.1d | the rail's AI tag; the strip's AI |
| The conn | nothing but the player's helm moves the unit | 2026-10-23 | the strip's Conn, C |

So: nearly every order an admiral of the era gave has its AI; half of them the player can't reach, and those they can
are spread over the strip, two popups and ctrl+click.

## The vocabulary, as an admiral thinks of it

Grouped by what an order is about, since that's what the player points at:

- **About one of ours (dispositions):** Screen, Scout, Cover, Join, Follow, Station (take station on a unit at a bearing
  and distance: built as a unit order, never offered).
- **About the enemy:** Fight, Engage (concentrate fire), Intercept, Shadow, Attack (torpedo craft), Run from.
- **About a place:** Go to, Patrol, Search, Withdraw (home, or to a point).
- **Manoeuvres** (one-off changes to a line, not tasks): turn together by points, the battle turn-away, alter course in
  succession (a plain course), Deploy, Form.
- **Standing rules:** doctrine (engage, range, outgunned, torpedoes, night, evasion, aggression), on 5.2's doctrine card.

Every one of them, given by the player, is by hand: it takes the unit from the side's commander (decision 14) and holds
it against doctrine's standing orders (command.md "The player's hand"). Inside a general order (a task, a manoeuvre) the
unit's admiral and captains still do the minute-to-minute: a screen still counter-attacks, a line still turns away from
torpedoes. Helm orders and the conn are the player's way to overrule that (battle-hud decision 6).

## The AI's part

What the vocabulary still lacked in the sim, and what's built:

### O1 The split screen (built 2026-10-23; as two squadrons, O1b)

A squadron screening a unit used to keep one station, its ships in their formation about it: a column 5 km off the
screened guide, toward the nearest enemy in sight or dead ahead. The era's screen split: a flotilla's divisions on
either bow of the fleet, at about four points, so the screened line had torpedo craft covering both bows and a
counter-attack ready on either side.

- **The split is two squadrons** (O1b, decision 1). A squadron of two ships or more told to screen is divided first
  (`GroupSystem.Divide`): it becomes a force of two divisions, two new squadrons ("1st Flotilla I" and "II": the
  first half of its members, its guide's, and the rest), keeping its own id, name, admiral, place in its own force and
  orders, in one line where it was (line ahead, else a line of bearing on its old line's bearing, the divisions
  chained). A force splits by squadron: the first half of its line (its guide's) on one side of the bearing it screens
  on, the rest on the other, each `ScreenSplitDeg` (45°) off it, spread on the arc `ScreenM` (5 km) out, the force's
  spacing apart from the bearing outward (no nearer to it than half the split). Each division keeps its own station on
  the screened guide, its ships in formation about its guide, so it sails and fights as one. A division stays one: the
  flotilla remains a force of two divisions after its screen.
- **The bearing screened on** is as before: the nearest enemy in sight of the screened guide, else dead ahead; placed
  again only when the guide's station would move more than `ScreenMoveM`. The guide's half takes the side its guide is
  on when the order comes, and keeps it, so the halves swing round together as the threat moves and never cross.
- **The counter-attack by divisions.** Enemy torpedo craft closing on the screened unit (the turn-away's own threat
  test) are fought by the division (or half of a force's squadrons) on their side of the bearing; the other keeps its
  station, so the far bow stays covered. Both back on station `TurnBackS` after the last.
- **In one body** when ordered so: `AiCommand.Screen`'s Y is the split (0: the settings'; negative: one body, as
  before). A scenario's screen task takes `"split"`. The side's commander's flotillas screen split.
- **Tests** (`AdmiralOrdersTests`, `ScreenTests`): four destroyers told to screen are a force of two divisions of two;
  screening a line ahead they end on either bow (-48/-53° and +55/+61°, 4.0-4.8 km: ships on station lag a little
  astern of a 15 kn line); in one body they keep a column dead ahead; a force of two squadrons splits by squadron;
  with enemy destroyers from the east-south-east only the division on that side counter-attacks, as a unit.
- **The commander** sends a divided flotilla in by its divisions, as any force of flotillas (`CommanderTests`).

### O2 Deploy as an order (built 2026-10-23)

The deployment (5.1c) was doctrine's alone: a battle force meeting the enemy's main body within `DeployM` deployed on
its own, and the player had no way to order it, or to choose its course: the era's most famous signal (Jellicoe's
"equal speed, Charlie London", deploying on the port wing column, south-east by east) had no order.

- **`AiCommand.Deploy`** (`UnitOrders.Deploy(unit, course)`): a force into one line of battle on the course ordered, the
  wing column furthest along it leading, the others following it in succession, as doctrine's deployment does it (one
  implementation: `StartDeploy`, `DeployCourse`). Without a course: doctrine's (square across the course of the enemy's
  main body in sight within `FightCompareM`, toward his track; across his bearing if he's stopped), else the guide's
  heading. A squadron forms line ahead and steers it.
- Deploying by order is tracked as doctrine's is: deploying until every ship is on her place (or `DeployMaxMs`),
  then deployed. The player's is by hand, so no fight is taken up after it; it's a helm order (the conn keeps it).
- **Tests:** four columns abreast ordered to deploy on 090° are one line ahead steering 090°; ordered without a course,
  with the enemy line in sight, the line steers square across his course.
- **Placeholder:** "deploy" beside "turn together" in the squadron popup (forces only), across the enemy in sight.

### O5 Where the screen sits, by its admiral (built 2026-10-23)

Decision 2: the screen's place is its admiral's judgement, not a fixed geometry.

- **Nothing in sight** of the screened unit: on its bows (the bearing screened on dead ahead, the divisions 45° either
  side of it).
- **Torpedo craft closing:** the division on their side counter-attacks, as in O1b.
- **An enemy in sight:** the admiral judges his screen alone against the enemy in sight of the screened unit (the
  matchup, `MatchupJudge.GroupAgainstNear`). A match for him (at doctrine's bar), the screen sits toward him; no match,
  it goes to the **disengaged side**, its divisions at the van and the rear of the screened line, out of the line of fire
  and the enemy's secondaries, ready to go round the ends (the Grand Fleet's battle orders). `ScreenMarginShare` (25%)
  either way so it doesn't flick. So four battleships set as a screen stay toward the enemy, and a destroyer flotilla
  stands off behind its line against a battle line but keeps toward light cruisers.
- **Each half moves the least:** every time the screen is placed, its guide's division takes the side of the new bearing
  its guide is on, so the van division stays the van when the screen changes sides.
- **Tests** (`AdmiralOrdersTests`): four destroyers screening four dreadnoughts, an enemy line of four dreadnoughts
  abeam to port: half an hour later one division is on the starboard bow (+46/+51°), the other the starboard quarter
  (+134/+139°); four dreadnoughts screening against two light cruisers abeam to port keep to port.

### O6 A unit's role (built 2026-10-23)

Decision 4: the player says what a squadron is for.

- **`UnitRole`** (`Sim.Ai/Roles.cs`, moved from the commander): battle line (`Heavy`), scouting, screening (new),
  torpedo flotilla, auxiliary. **Worked out** from the unit's line when nobody set it, as the commander did before
  (capital ships: battle line; torpedo craft, half its ships or more: flotilla; a guide with no main battery: auxiliary;
  else scouting); screening is only ever set. **Set** by `AiCommand.Role` (a setting, as doctrine is: logged, hashed,
  it doesn't take the unit from its commander; `UnitRole.None` puts it back to worked out).
- **One place to ask:** `Roles.Of(unit)`, read by the commander now and the force admirals from O7; the snapshot's
  `Groups.Role`/`RoleSet` and `Ships.Role`/`RoleSet` (a ship alone) for the map: the smart verb by role and the role's
  mark come with O9.
- **The commander by role:** a screen is never his main body while there's another unit (as a flotilla), isn't sent to
  close an enemy group or in to attack, and screens the nearest heavy unit when idle. Four dreadnoughts set as a screen
  screen the main body (`CommanderTests.Battleships_set_as_a_screen_screen_the_main_body`).
- **Placeholder:** a role picker in the squadron popup's row ("worked out", then the five), until the task menu.

### O7 The chain of command (built 2026-10-23)

Decision 3: a unit given a task disposes what it has; ships make squadrons, squadrons forces, forces bigger forces,
each with an admiral.

- **A force's admiral disposes his members by role** (`AdmiralLayer.Dispose.cs`), every `ForcePeriodMs`, with
  doctrine's new `Dispose` (the default preset on; neutral off, so dev worlds and older tests keep every squadron in
  line): his **main body** is his members of the strongest role in his force (the battle line, else scouting), kept in
  his line, carrying whatever the force is told (a course, a search, an intercept, a fight); his **scouts** scout ahead
  of the force (20 km, spread 0°, ±45°, ±90° off its bow); his **flotillas and screens** screen it (and so divide, and
  sit where their own admirals judge, O1b and O5); his **auxiliaries** keep their place in his line. A force of
  flotillas and screens only keeps together.
- **Each level its own:** a fleet of forces disposes its own members (its cruiser squadron scouts for the fleet), and
  each force in it its own (its destroyers screen it), as deep as they nest.
- **Who has a member:** a member the player holds by hand is left alone; so is one on a task someone else gave it (the
  side's commander sending a flotilla in to attack, the player's task). An admiral knows his own tasks by when they
  began (`TaskLayer.StartOf`). A member back with no task (its attack over, rejoined) is given its own again; one whose
  role now puts it in the main body (the player set it so) is called back into the line. Not while deploying, nor on
  the conn.
- **One implementation** (`Dispositions`): the side's commander's idle tasks for his top units by role (scouts ahead of
  the main body on the next bearing, flotillas and screens screening the nearest heavy unit) and the force admiral's for
  his members are the same rule. His tasks go as a commander's (`OrderSource.Commander`, now "a commander above the
  unit": the side's, or a force's admiral).
- **Found on the way:** a ship keeping a station far ahead of her guide closed at most `FollowMaxSpeedDiffKn` (6 kn)
  faster than him, so light cruisers sent to scout 20 km ahead of a 15 kn fleet took nearly two hours to get there. A ship
  more than `StationFarM` (2 km) astern of her station now rings all she makes (27 kn: 16 km ahead in 45 minutes), and
  the damped rule takes over within it.
- **Tests** (`AdmiralOrdersTests`): a force of four dreadnoughts, two light cruisers and four destroyers: the
  dreadnoughts alone in its line, the cruisers scouting dead ahead, the destroyers divided on either bow at 45°, 5.0 km;
  without the doctrine all three stay in its line; the player's course to the cruisers by hand holds; a fleet of a battle
  force and a cruiser squadron: the cruisers scout for the fleet, the destroyers screen the battle force.

### Still lacking (later steps, after the questions)

- **Battle decisions down the chain.** The side's commander still sends a force's flotillas in to attack himself
  (reaching into top forces for their squadrons of torpedo craft); with the chain, a force's admiral should send in his
  own, and decide his own force's fight. Next for the chain (O7b).
- **The scenarios' trees:** the Jutland's fleets are flat (the Grand Fleet's flotillas and cruisers are top units with
  opening tasks); nesting them as the fleets were organised lets the chain dispose them.
- **Screen against a sector.** The threat bearing is always the nearest enemy in sight. An admiral could say "screen
  against the south-east" (submarines, a reported flotilla); a bearing in the order, shaped by a drag on the map (O7).
- **Intercept of a contact not yet held** (5.1c's open item): wants a point and a time, which is Go to.

## The player's part: how the orders are offered

From the command UI research (`command-ui.md`): command by intent, one smart verb and the rest at the cursor, preview
before commit, orders as objects on the map, no hidden modes. And from the HUD as built: the strip's columns (helm,
guns, torpedoes, task, formation), armed picks completed by a click on the map, the cursor naming the verb.

1. **One task menu, by what the order is about.** The strip's task column keeps its most used segments (Fight, Follow…,
   Hold) and gains **Task ▾**: a popup of three columns, *one of ours* (Screen, Scout, Cover, Join, Follow, Station),
   *the enemy* (Fight, Intercept, Shadow, Attack, Run from) and *a place* (Go to, Patrol, Search, Withdraw). Each arms a
   pick completed by a click on what it's about, as Follow… does now. Orders that don't apply are greyed with the reason
   ("no torpedoes", "a ship alone can't screen in halves"), never removed, so their places are learnt. Each has a hotkey
   shown in its tooltip.
2. **The cursor names the order and its shape** before the click: "SCREEN Grand Fleet · 5 km · split 4 points",
   "SCOUT ahead of Lion · 20 km", "INTERCEPT Lützow · meets in 0:42", "ATTACK König · 6 boats", "GO TO · 34 km, 1:10".
3. **The smart verb** (right-click with a unit selected) stays one verb per thing under the cursor, but by what's
   selected: over one of ours, **SCREEN** for a squadron of torpedo craft or light cruisers, **FOLLOW** for any other;
   over an enemy, ENGAGE as now (Attack for torpedo craft is the menu's). The cursor says which before the click.
4. **Manoeuvres in the formation column:** Turn ▾ (4 and 8 points either way; about, rear first), **Deploy…** (armed:
   the click gives the course to deploy on, the line steering toward it; over the enemy, across his course; the cursor
   shows the course and which wing column leads) and the formation picker. These leave the squadron popup, which keeps
   the squadron's business (detach, rejoin, forming, disbanding).
5. **Orders drawn on the map** for the selection (and all with Tab held): a screen's stations as faint marks on its
   arc with a thin line to the screened guide; a scout's station; an intercept's lead line to where it meets; a search's
   pattern; a deploy's line ahead of the guide on its course. The unit's label carries the task and how it stands ("screen
   Grand Fleet", "screen: counter-attacking", "scout: shadowing Lützow"), with who gave it in the card (you, the
   commander, the scenario). The sim side: the snapshot carries each ship's ordered station (her captain's target and
   offsets), so the map draws what the ships are steering for, not a copy of the rule.
6. **Shaping by drag, later** (the research's "right-drag on an escortee sets screen radius"): from the screened unit,
   the drag's length sets the distance and its direction a sector to screen against (O7), the arc's stations previewed.

### O8 The mockup (2026-10-23, for the user's review)

`docs/map-ui/battle.html`, four new states (screenshots `docs/map-ui/png/battle-tasks.png`, `-screen`, `-deploy`,
`-chain`); in them the Battlecruiser Fleet is a force of the 1st BCS, the 13th Flotilla and the 1st LCS, paused (pause
and order):

- **`#tasks`, the task menu** (decision 5): the strip's **Task ▾** or **T** opens it above the strip. Its head is **what
  the unit is for**: the five roles as a switch, the one in force lit, "worked out from its ships" or "set by you · work
  it out". Below, three columns by what an order is about, each order with its key while the menu is open: *one of
  ours* (Screen S, Scout O, Cover C, Join J, Follow F, Station N), *the enemy* (Fight B, Intercept I, Shadow H, Attack
  A, Run from R), *a place* (Go to G, Patrol P, Search E, Withdraw W). One that doesn't apply is greyed with its reason
  (a battlecruiser squadron's Attack: "a flotilla's: set it as one to send it in"), never removed. Picking one arms it;
  a click on what it's about gives it. The strip's unit head carries its role ("flotilla · worked out").
- **`#screen`, an order shaped before it's given:** the 13th Flotilla with Screen armed, the cursor over Lion: "SCREEN
  1st BCS · 5 km · split 4 points · disengaged side", and on the map its two divisions' stations (I, II) on the arc
  where its admiral would put them (the disengaged side: no match for the 1st Scouting Group), dashed from where the
  boats are. The right click's default over one of ours is Screen for a flotilla or a screen (decision 4), Follow
  otherwise; the hint line says which.
- **`#deploy`:** the Battlecruiser Fleet with Deploy… armed (the formation column's new segment), the cursor over the
  enemy van: "DEPLOY 070° · across his course · the port wing leads", the line of battle drawn on that course from the
  guide.
- **`#chain`, the chain of command at work:** the rail shows the fleet as a tree, each unit with its role chip and its
  task ("main body · in line", "screen · disengaged side", "scout ahead · 20 km"), the flotilla's two divisions under
  it; on the map each squadron's station as a mark (the screen's arc and its I and II, the scouts' S 20 km ahead, dashed
  from where they are).

For the review: the column names and keys, Station's drag, whether the menu sits above the strip or at the cursor, and
the marks' look (placeholder green). The port (O9) follows the user's notes.

## The steps

| Step | What | Status |
|---|---|---|
| **O0** | This plan | 2026-10-23 |
| **O1** | The split screen (AI): halves of one squadron | done 2026-10-23; **replaced by O1b** (decision 1) |
| **O1b** | Divide: a squadron told to screen becomes a force of two divisions, each its own squadron | **done 2026-10-23** |
| **O2** | Deploy as an order (AI), a placeholder button | **done 2026-10-23** |
| **O5** | Where the screen sits, by its admiral (decision 2) | **done 2026-10-23** |
| **O6** | A unit's role: worked out, or set by the player; the commander and the map read it (decision 4) | **done 2026-10-23** |
| **O7** | The chain of command: a force's admiral disposes his members by role, one implementation with the commander's (decision 3) | **done 2026-10-23** |
| **O8** | The mockup: `docs/map-ui/battle.html` `#tasks`, `#screen`, `#deploy`, `#chain`: the task menu, the role, the cursor's verbs, a screen's stations, a deploy's line, the chain in the rail | **done 2026-10-23**, for review |
| **O9** | The port: the task menu and its picks, the cursor's verbs, the smart verb by role, the formation column's manoeuvres; the snapshot's stations and the map's task marks | after O8 |
| **O10** | Shaping by drag (a screen's distance and sector), orders as editable objects, the radial menu | later |

## Measured (O1-O2, Release, 5 seeds; the Jutland one)

Against the same build with `ScreenSplitDeg` 0 (every screen in one body, as before):

| Scenario | One body | Split |
|---|---|---|
| `squadrons`, `flotilla` (5 seeds) | identical: neither has a screen | |
| `tsushima` (5 seeds) | done 3, quiet 1, time 1; Russia lost 6.4 of 13, Japan 4.2 of 14 | done 1, quiet 2, time 2; Russia 6.2, Japan 4.2 |
| the Jutland (seeds 1916-1918) | time 3; Britain lost 39.0 of 151 (161 kt), Germany 48.3 of 99 (276 kt); torpedoes 270 and 210 a run | time 3; Britain 39.0 (144 kt), Germany 56.0 (200 kt); torpedoes 166 and 182 |

- **Germany loses more ships and fewer tonnes:** more destroyers and light cruisers, fewer capital ships. The split
  screens put a half of each flotilla on the threatened bow, nearer the enemy's light forces.
- **Britain fires fewer torpedoes** (166 a run against 270): a flotilla on a screen counter-attacks by halves now, and
  fewer boats are in reach when the commander sends it in. To watch in O5 (screens in action) and the AI's fine-tune.
- **Own fire**, seed 1916 alone: 3 destroyers lost to their own side with the split, 1 without (counter-attacking halves
  in a mêlée). One seed, so not yet a finding; the multi-seed bench doesn't count it.
- Seeds diverge from the first minutes (which contact the commander holds, which he intercepts), so one seed's
  timeline isn't comparable: seed 1916's Grand Fleet deploys at 17:43 in one body and not at all with the split.
- **Cost:** the Jutland 150-232 s a seed split, 147-215 s in one body; the admirals 2.3% and 3.0% of seed 1916's run.

## Decisions (the user, 2026-10-23, on the questions)

1. **A split screen is two squadrons.** Split, a squadron becomes two: its admiral divides it into two divisions that
   each sail and fight as a unit, so it's clear in a fight who keeps together, and no squadron is ever torn across two
   stations (the first O1 kept one squadron split in halves; replaced, O1b).
2. **Where the screen sits is its admiral's call** (questions 1 and 2): on the bows, toward the threat, the engaged or
   the disengaged side, by his judgement of the situation (O5).
3. **A chain of command, as deep as it nests.** Ships make squadrons, squadrons forces, forces bigger forces, each with
   an admiral. A unit is given a task ("search here"), and its admiral decides how to dispose what he has: his scouts
   scout, his screens screen, his main body carries the task (O7). The side's commander is the top of the same chain.
4. **The player says what a squadron is for.** A unit's role (battle line, scouting, screening, torpedo flotilla,
   auxiliary) is worked out from its ships by default and set by the player at will: four battleships told they're a
   screening squadron screen, and the right click's default over one of ours follows the role (screen for a screening
   squadron) (O6).
5. **A task menu**; the radial menu comes later, and is optional to use.

## Questions for the user

Answered 2026-10-23: see "Decisions".
