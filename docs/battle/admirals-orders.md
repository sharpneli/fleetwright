# Plan: the admiral's orders, the player's and the AI's

2026-10-23. Status: O1-O9 **built 2026-10-23** (the AI's side, the mockup, then the port: the task menu O9a and the
orders on the map O9b), and O7b (battle decisions down the chain); O10 (shaping by drag, the radial menu) later.

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

### O1c Screens share an arc; organising is apart (built 2026-10-23, decision 6)

The user, after the mockup: Screen held two things, splitting a squadron and keeping a distance off a unit. They're
separate now: **Screen only places**, and **organising a unit** (creating, splitting, merging squadrons) is its own set
of actions, the player's and the admirals'.

- **Every squadron screening a unit shares its arc** (`AdmiralLayer.Screen.cs`, `Arcs`, once a think, sorted by unit):
  one sits on the bearing screened on; two or more spread evenly to `ScreenArcDeg` (45°) either side of it: two take
  four points on either bow, three either bow and dead ahead, and so on. They take the stations in the order they lie
  about the bearing, so none crosses another. **Screen with several squadrons selected is several Screens on one unit**:
  they spread themselves.
- **One judgement an arc:** its bearing (ahead; toward the nearest enemy in sight; or the disengaged side when the
  screens together are no match for him: O5) is the arc's, kept between thinks with its margins, and the screen whose
  station is nearest an attack counter-attacks (and keeps at it while the attack lasts); the rest hold their stations.
- **A force told to screen** hands the order to each of its groups (`ScreenHanded`), which take their own stations on
  the arc.
- **Organising** (`GroupSystem`): `Divide` (a squadron into a force of two divisions under its admiral, in halves or from
  any ship on; it keeps its id, name, orders and place) and `Merge` (its inverse: a force of squadrons back into one
  squadron, the same id), beside the existing create, add, remove, form a force, enter, leave and disband. Placeholders
  in the squadron popup ("Divide in two" or "Divide from her", "Merge into one squadron") until the task menu's organise
  column (O8b).
- **The admirals organise too:** a squadron's admiral alone on the arc he screens on, with `ScreenDivideShips` (4) ships
  or more in his line, divides it into two divisions to cover both bows, once an order (a player who merges it back
  isn't overruled), and never when the order (or task) is the player's by hand: the player's screen is literal, one
  station on the bearing. A task now remembers it was given by hand (`TaskLayer.ByHand`).
- **Found on the way:** an admiral handing his order to a new guide re-sent the speed last asked of him after it, which
  turns any behaviour (a screen, a fight, a station) into a plain course; it's re-sent only with a course or a route now.
  It showed as a divided flotilla's first division steaming on instead of screening.
- **Tests** (`AdmiralOrdersTests`): a lone flotilla of four divided by its admiral, its divisions at -45°/+45°, 5.0 km;
  the player's screen by hand not divided, its column dead ahead; two squadrons at ±45°, three at -45°/0°/+45°; a force
  handing Screen to its squadrons; Divide and Merge keeping the unit and its order; only one screen counter-attacking at
  a time; two pairs of destroyers on the disengaged side at +45° and +135°, two pairs of dreadnoughts toward light
  cruisers at -45° and -135°.

### O1 The split screen (built 2026-10-23; as two squadrons, O1b; both replaced by O1c)

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

### O7b Battle decisions down the chain (built 2026-10-23)

Before, the side's commander reached into his top forces for their squadrons of torpedo craft and sent them in to attack
himself, then called them back into their force's line. Now each level decides for its own:

- **One rule, every commander's** (`FlotillaAttacks`, one implementation): a flotilla free to go in (torpedoes left, not
  on an attack, never again once one of its attacks was beaten off; screening, on its orders, or through with its task)
  is sent in, as its side's commander sees the battle: in **battle** by day on an enemy line its commander's own heavy
  units engage, its attack worth making (the matchup has it attacking, at doctrine's bar); by **night** (doctrine's
  NightAttack) on any heavy enemy in reach worth it; **declining** a stronger enemy, by night only; **broken off**, once,
  to cover the others breaking off; **gone home**, never. With no commander, its own commander judges by night as by day.
- **The side's commander** applies it to his top units only. A top force of flotillas (a flotilla divided by its own
  admiral) is sent in as one.
- **A force's admiral** applies it to the flotillas of his force, as part of his disposition (doctrine's Dispose), his
  main body's squadrons the heavy units that must be engaging; the attack is his task given as a commander, and once it's
  over the flotilla screens again. He reads how his side's commander sees the battle (night, declining, broken off).
- **An Attack to a force is handed down** (as Screen is, O1c): each of its groups attacks under its own admiral, the
  guide group too; a division without torpedoes fights; one beaten off isn't sent again. A force's attack is beaten off
  when every group of it with torpedoes left is. `AnyTorpedoes` reads a force's line down the tree.
- **A group through with its attack** (its torpedoes spent, or beaten off) is through for the force's order: it isn't
  sent in again, and keeps its station in the force (as a squadron sent in alone is called back to its screen); the
  force's attack is over once its guide group's is and every other group is through (`AttackOver`).
- **"Engaging" is the side's battle line** (`FlotillaAttacks.Heavy`, once a tick): its squadrons of the battle line at
  any level of the chain and its capital ships alone, for the side's commander and every admiral alike (not a force's
  own main body: a flotilla led by a light cruiser waits for the battle line to engage).
- **A force goes in whole only if it's flotillas alone** (a divided flotilla); a force with anything else in it is its
  admiral's, who sends in its flotillas.
- **Tests:** `AdmiralOrdersTests.A_force_sends_in_its_own_flotilla_on_a_line_its_battle_squadron_engages` (the O7
  force, an enemy dreadnought 14 km abeam: the force's admiral sends his divided destroyers in within a minute, they
  fire; the side's commander, on, gave neither them nor their divisions anything);
  `A_force_told_to_attack_hands_it_to_its_divisions` (both divisions gain, run in, fire and turn away, each its own,
  and the force's attack is over); `A_light_cruiser_leading_destroyers_is_not_sent_in_whole` (two light cruisers leading
  four destroyers beside an engaged battle squadron: the commander gives the force nothing, its admiral sends in the
  destroyers, the cruisers keep their place); the commander's own (`CommanderTests`) unchanged.

**Measured** (Release, at `be609e61` against O7b; 12 Jutland seeds 1916-1927, 10 Tsushima seeds; `squadrons` and
`flotilla` identical but for timings):

| Scenario | Before O7b | O7b |
|---|---|---|
| the Jutland (12 seeds) | time 11, quiet 1; Britain lost 31.6 of 151 (111 kt), Germany 53.4 of 99 (207 kt); hits 4,218 and 4,367; torpedoes 246 and 185 a run | time 12; Britain 41.3 (147 kt), Germany 59.0 (259 kt); hits 5,121 and 5,606; torpedoes 252 and 217 |
| `tsushima` (10 seeds) | Russia lost 4.8 of 13, Japan 3.9 of 14 | 4.5 and 4.0 (with the three corrections above: 4.1 and 3.8) |

- **Why the Jutland is bloodier:** the German "Scouting torpedo boats" (Regensburg leading the II, VI and IX
  Flotillas) is a top force whose every member works out as a flotilla: Regensburg (105 mm guns and tubes) counts as
  torpedo craft (`CaptainLayer.TorpedoCraft`: tubes and no gun of 150 mm), so her unit's role is a flotilla's. Before,
  the commander reached into the force and sent each flotilla in on its own judgement (Regensburg alone wasn't worth
  sending); now the force goes in whole, Regensburg with it, and the British commander turns the Grand Fleet, the
  Battlecruiser Fleet and Yarmouth on her (seed 1917 at 14:59); the fight runs on to the time limit in every seed.
- **None of the three corrections moves it:** spent divisions off the attack, the battle line as "engaging" and
  flotillas-only forces each leave the Jutland as O7b has it (12 seeds each: Britain 44.2 and 43.0, Germany 60.9 and
  60.3), since Regensburg's unit is a flotilla by role. A role by size (a destroyer's displacement band) would make
  her a scout, but the dev destroyer design (2,628 t) is a cruiser by size too: no clean physical line found. **For
  the user:** whether a light cruiser leading flotillas (the era's flotilla leaders did lead the attacks) goes in with
  them, or her unit is a scout's.

### Still lacking (later steps, after the questions)

- **A force's own fight.** Its flotillas are its admiral's now (O7b); whether a force inside a fleet closes an enemy
  group is still doctrine's and the fleet's task, not a judgement of its own admiral.
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

### O8b The mockup again: organising, and several squadrons at once (2026-10-23, for review)

After decision 6; `battle.html` `#organise` and `#multi` (screenshots `png/battle-organise.png`, `-multi`), `#screen` and
`#chain` redone:

- **`#organise`: the task menu's fourth column, Organise** (set apart, a lighter ground: these change the unit, they
  aren't orders to it): Divide in two (D), Divide from her… (V: click a ship of it; she and those astern the second
  division), Merge divisions (M), New squadron of selected (Q), Form a force with selected (K), Leave its force (L),
  each greyed with its reason when it doesn't apply ("no divisions to merge").
- **Several squadrons selected:** ctrl+click a squadron in the rail (or on the map) adds it to the selection; the strip
  says "13th Flotilla + 1 more · 2 squadrons · orders go to each", the menu's head names them all, and its foot says
  an order goes to each.
- **`#multi`: Screen with two squadrons selected:** "SCREEN 1st BCS · 5 km · 2 squadrons, either bow · disengaged side";
  the map shows each squadron's station on the arc, numbered and named, taken in the order they lie (the sim's rule).
- **`#screen`: one squadron, by hand:** one station on the bearing, "yours, by hand: divide it first for both bows".
- **`#chain`:** the flotilla divided by its own admiral, its divisions I and II under it in the rail ("the van · 5 km",
  "the rear · 5 km").

### O8c Organising by drag and drop in the rail (built 2026-10-23, in the game)

The user, after O8b: the rail at the left is the chain of command as a tree, so reorganising is dragging in it. Built
straight into the game's rail (`MapScene.Rail.cs`), every drop one of the group system's commands:

- **A ship onto a squadron's head:** she joins it at its end. **Onto a ship of a squadron:** ahead of her (in her own
  squadron, a move: dragged onto its guide she leads). **Onto a ship alone:** a new squadron of the two, the one dropped
  on leading.
- **A squadron or a force onto another unit:** it goes under that unit's admiral (`GroupSystem.Subordinate`, new): into
  it if it's a force; if it's a squadron, that squadron becomes the guide of a new force in its own place (in its force,
  as detached as it was, "1st BCS force"), the dropped group astern of it in line ahead.
- **Onto "on its own"** (a place under the list, shown only while dragging): a ship out of her squadron, a group out of
  its force.
- What a drop does is told under the cursor before it's let go ("Tiger to 1st Battlecruiser Squadron, ahead of Lion"),
  the target outlined in brass; a drop that would do nothing (onto itself, its own force, something inside it; a ship
  onto a force) isn't taken. The words are made once a target, so a drag held over one makes no garbage.
- **The sim's side:** `GroupSystem.Add` takes a place (and moves a ship within her squadron); `Subordinate` (above);
  `GroupTests` cover both. Checked in the game with scripted drags (`-input=F:drag:...`): Tiger dragged onto Lion leads
  the 1st BCS, and Princess Royal dragged to "on its own" leaves it (`docs/map-ui/png/game-o8c-rail-drag.png`).
- Organising isn't an order: it doesn't take a unit from the side's commander, whose roles and tasks follow the new
  tree at his next think.

### O9a The task menu in the game (built 2026-10-23)

The mockup's `#tasks`, `#organise`, `#multi` and `#deploy` ported (`MapScene.Tasks.cs`, `MapScene.Strip.cs`; capture
`docs/map-ui/png/game-o9a-tasks.png`):

- **The strip's task column:** Fight, Follow…, Hold, **Task ▾** (or **T**); at a unit's level its formation ▾ (the
  formation and spacing), **Turn ▾** (4 and 8 points either way; about, the rear ship first, to port or starboard),
  **Deploy…**, Withdraw and its AI; "Squadron…" keeps the squadron's business (detach, rejoin, leave, disband, add the
  picked). The head says CAPTAIN, SQUADRON or FORCE; under the unit's name in the first column, what it's for ("flotilla ·
  worked out"). The squadron popup's placeholders (divide, merge, screen, join, deploy, torpedo attack, the role picker,
  turn together) are gone: they're the menu's and the strip's now. The UI font has no ▾: `Seg(menu: true)` draws it.
- **The menu**, above the strip: its head the unit's name (several: "Group 2 + 1 more") and **what it's for**, the five
  roles as a switch (the one in force lit), "worked out from its ships" or "set by you · work it out"; four columns,
  *one of ours* (Screen S, Scout O, Cover C, Join J, Follow F, Station N), *the enemy* (Fight B, Intercept I, Shadow H,
  Attack A, Run from R), *a place* (Go to G, Patrol P, Search E, Withdraw W; and "a pattern by hand…", the old search
  row's ladder, square, sector and patrol line as the unit's route), *organise* (Divide in two D, Divide from her… V,
  Merge divisions M, New squadron of selected Q, Form a force with selected K, Leave its force L). Letters pick while
  it's open (over the map's own: J, V, P, C); T or Esc shuts it. One that doesn't apply is greyed with its reason
  (Attack: "a flotilla's: set it as one to send it in"; Divide on a force: "a force: divide one of its squadrons";
  Merge: "no divisions to merge"; the organising with nothing ctrl+clicked: "ctrl+click the ships").
- **Each order armed and given by a click,** the cursor naming it and its shape before the click: "SCREEN 1st BCS · 5 km
  · on the bearing" (2: "either bow", more: "over the arc"), "SCOUT for X · 20 km ahead", "COVER X · 20 km off, toward the
  threat", "STATION on Lion · 1.2 km, 135° off her bow" (the station held where the unit lies from her now: Station's
  drag waits for O10), "INTERCEPT X · 12.3 km", "ATTACK X · 6 boats", "GO TO 112° · 9.4 km · 0:19 at 15 kn", "DEPLOY 070° ·
  the wing column furthest along it leads" (over an enemy: across his course, doctrine's own reckoning), "DIVIDE FROM
  Tiger · 2 and 2". A miss (land, the wrong kind of thing) shows what the pick waits for, in amber, and keeps it armed.
- **Whom an order of ours is about:** the ship clicked stands for her squadron, and the force above it while she's in
  its line, but never a unit holding the one ordered (a fleet's flotilla screens its battle squadron, not the fleet).
- **The smart verb by role** (decision 4): a right-click on one of ours screens her unit when the unit ordered is a
  flotilla or a screen, else follows her; the cursor and the hint line say which.
- **Several units at once:** ctrl+click ships on the map (each stands for the unit she's in at the level the orders go
  at) or units' heads and ships in the rail (lit beside the selection); the strip says "+ 1 more · orders go to each",
  and the menu's orders and the right-click's go to each (Screen spreads them over the arc, the sim's O1c).
- **Checked in the game** (dev ships, scripted keys and clicks): a destroyer squadron's T, S and a click on a
  dreadnought gives Screen (the rail's "screen", the strip's head, the relation line); Deploy… over the sea names the
  course; T then D divides the squadron into a force of two divisions, the menu then greying Divide and offering Merge.
  Ctrl+clicks (SDL's modifier state) aren't scriptable: by hand.

### O9b Orders drawn on the map (built 2026-10-23)

The mockup's `#chain` and `#screen` marks, in the game (`MapScene.TaskMarks.cs`; capture
`docs/map-ui/png/game-o9b-marks.png`: a flotilla divided, its two divisions screening a battle squadron at ±45°, 5 km):

- **The snapshot carries each ship's station** (`ShipsView.StationAsternM`, `StationAbeamM`, filled by the captains):
  metres astern of her captain's target and to its starboard, in its frame, as `Behaviours.Station` steers for it (NaN:
  none). So the map draws what the ships steer for, not a copy of the admirals' rules.
- **For the selection's unit, down its whole tree from the top** (and each unit selected beside it): each squadron's
  station, its guide's, as a dashed ring with a dashed line from where the guide is, labelled with the squadron and its
  task (a division's is its force's: "Group 2 I · screen"). A station in the unit's own formation (on a ship of the line
  it's in) isn't drawn: only the unit's task shows. A ship alone selected: her own.
- **A screen's arc:** about the unit screened, dotted, through its screens' stations and a little past the end ones.
- **An armed order's shape before the click:** Screen and Cover, the distance kept about the unit under the cursor
  (the station on it is its admiral's call: the threat and the matchup, so it isn't guessed); Scout, the station dead
  ahead of it as the order gives it; Deploy…, the line from the guide on the course, as long as its ships in line;
  Go to, Patrol and Search, the way there.
- The unit's label already carried its task ("GROUP 2 · screen"); the card's "who gave it" waits (the snapshot holds no
  order's source yet).
- Placeholder green (`MapPalette.TaskMark`), as the mockup's; the look is for the user's review.

## The steps

| Step | What | Status |
|---|---|---|
| **O0** | This plan | 2026-10-23 |
| **O1** | The split screen (AI): halves of one squadron | done 2026-10-23; **replaced by O1b** (decision 1) |
| **O1b** | Divide: a squadron told to screen becomes a force of two divisions, each its own squadron | done 2026-10-23; **replaced by O1c** |
| **O1c** | Screens share an arc (Screen only places); Divide and Merge apart, the player's and the admirals' (decision 6) | **done 2026-10-23** |
| **O2** | Deploy as an order (AI), a placeholder button | **done 2026-10-23** |
| **O5** | Where the screen sits, by its admiral (decision 2) | **done 2026-10-23** |
| **O6** | A unit's role: worked out, or set by the player; the commander and the map read it (decision 4) | **done 2026-10-23** |
| **O7** | The chain of command: a force's admiral disposes his members by role, one implementation with the commander's (decision 3) | **done 2026-10-23** |
| **O7b** | Battle decisions down the chain: a force's admiral sends in his own flotillas, by the commander's rule (one implementation); an Attack to a force handed down | **done 2026-10-23** |
| **O8** | The mockup: `docs/map-ui/battle.html` `#tasks`, `#screen`, `#deploy`, `#chain`: the task menu, the role, the cursor's verbs, a screen's stations, a deploy's line, the chain in the rail | **done 2026-10-23**, for review |
| **O8b** | The mockup again: the organise column (new squadron, divide, merge, form a force, leave) and Screen with several squadrons selected | **done 2026-10-23**, for review |
| **O8c** | Drag and drop in the rail: ships between squadrons, a unit under another's admiral (in the game) | **done 2026-10-23** |
| **O9a** | The port: the task menu and its picks, the cursor's verbs, the smart verb by role, the formation column's manoeuvres, several units at once | **done 2026-10-23** |
| **O9b** | The snapshot's stations and the map's task marks: a screen's arc, a scout's station, a deploy's line, previews before the click, the label's task | **done 2026-10-23** |
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

**After O1b-O7** (Release; the same seeds; at `c47b180c`), against the O1-O2 split column above:

| Scenario | After O1b-O7 | O1-O2 split |
|---|---|---|
| `squadrons` (5 seeds) | done 2, quiet 1, time 2; Britain lost 1.2 of 4, Germany 2.2 of 5 | Britain 1.2, Germany 1.8 |
| `flotilla` (5 seeds) | quiet 5; Britain 0.0 of 4, Germany 0.2 of 6, no torpedoes | the same |
| `tsushima` (5 seeds) | quiet 3, done 2; Russia 4.8 of 13, Japan 5.0 of 14 | done 1, quiet 2, time 2; Russia 6.2, Japan 4.2 |
| the Jutland (seeds 1916-1918) | time 3; Britain lost 41.3 of 151 (135 kt), Germany 49.0 of 99 (129 kt); torpedoes 163 and 202 a run | Britain 39.0 (144 kt), Germany 56.0 (200 kt); 166 and 182 |

- `squadrons` and `flotilla` have no screens, scouts or mixed forces: they move only through `StationFarM` and seed
  noise (`flotilla` identical).
- **The Jutland, seed 1916:** the Grand Fleet deploys at 19:56 (it didn't in the O1-O2 run of the seed); own-fire losses
  1 (3 before). The Cruiser screen force deploys into a line of battle at 03:27 (its armoured cruisers count as capital
  ships to the deployment's test, once its scouting task is over): a classification for the fine-tune. At the end
  Germany has one top unit idle, the first since 5.1d: to look at in O7b.
- **Cost, seed 1916:** played in 224 s (150 s for O1-O2's run, on a machine running the tests beside it: to measure
  clean). The admirals 3.3% of the run (145 µs a think; at worst 41 ms against 21 ms), the commanders 2.7%, the
  captains 13.5%. The force admirals' disposition and the screens' matchup judgements are the admirals' new work; a
  cache of the screen's judgement for a few thinks is the obvious saving if it shows.

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
6. **Screen and splitting are separate** (the user, after the mockup, 2026-10-23): the player and the admirals create and
   split squadrons by their own actions; Screen with several squadrons selected puts each on its station (O1c).

## Questions for the user

Answered 2026-10-23: see "Decisions".
