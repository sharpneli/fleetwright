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

### O1 The split screen (built 2026-10-23)

A squadron screening a unit used to keep one station, its ships in their formation about it: a column 5 km off the
screened guide, toward the nearest enemy in sight or dead ahead. The era's screen split: a flotilla's divisions on
either bow of the fleet, at about four points, so the screened line had torpedo craft covering both bows and a
counter-attack ready on either side.

- **The split.** A screen of two ships or more (a squadron's ships, or a force's squadrons) splits in two halves: its
  line's first half (its guide's) on one side of the bearing it screens on, the rest on the other, each
  `ScreenSplitDeg` (45°) off it. Each half lies on the arc `ScreenM` (5 km) out, its ships (or squadrons) the group's
  spacing apart from the bearing outward (no nearer to it than half the split). Each ship of a squadron keeps her own
  station on the screened guide; each squadron of a force its own, its ships in their formation about it.
- **The bearing screened on** is as before: the nearest enemy in sight of the screened guide, else dead ahead; placed
  again only when the guide's station would move more than `ScreenMoveM`. The guide's half takes the side its guide is
  on when the order comes, and keeps it, so the halves swing round together as the threat moves and never cross.
- **The counter-attack by halves.** Enemy torpedo craft closing on the screened unit (the turn-away's own threat test)
  are fought by the half on their side of the bearing; the other half keeps its station, so the far bow stays covered.
  Both back on station `TurnBackS` after the last.
- **In one body** when ordered so: `AiCommand.Screen`'s Y is the split (0: the settings'; negative: one body, as
  before). A scenario's screen task takes `"split"`. The side's commander's flotillas screen split.
- **Tests** (`AdmiralOrdersTests`, `ScreenTests`): four destroyers screening a line ahead end on either bow (two at
  about -45 to -53°, two at +53 to +63°, 4.2-4.7 km: ships on station lag a little astern of a 15 kn line); in one body
  they keep a column dead ahead; a force of two squadrons splits by squadron (one at -48/-53°, the other at +47/+52°);
  with enemy destroyers from the east-south-east only the half on that side counter-attacks.

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

### Still lacking (later steps, after the questions)

- **Screens in action.** A screen keeps to the side of the threat once its line is engaged. The Grand Fleet's battle
  orders put the flotillas at the van and the rear on the disengaged side: out of the line of fire and the enemy's
  secondaries, ready to attack or counter-attack round the ends. A doctrine field (engaged side, disengaged side), the
  bearing screened on flipped while the screened line fights (O5).
- **A force's own disposition.** Inside a force, squadrons keep formation stations; screens and scouts are top units
  tasked by the side's commander (by role) or the scenario. A fleet whose flotillas and light cruisers are in its tree
  should screen and scout by itself when cruising: its admiral gives his light squadrons the roles' tasks the commander
  gives top units (O6).
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

## The steps

| Step | What | Status |
|---|---|---|
| **O0** | This plan | 2026-10-23, for the user |
| **O1** | The split screen (AI) | **done 2026-10-23** |
| **O2** | Deploy as an order (AI), a placeholder button | **done 2026-10-23** |
| **O3** | The mockup: `docs/map-ui/battle.html` `#tasks`: the task menu, the cursor's verbs, a screen's arc and a deploy's line on the map | after the answers |
| **O4** | The port: the task menu and its picks, the cursor's verbs, the smart verb, the formation column's manoeuvres; the snapshot's stations and the map's task marks | after O3 |
| **O5** | Screens in action (doctrine: engaged or disengaged side) | if wanted |
| **O6** | A force's own disposition: its light squadrons screen and scout by themselves | if wanted |
| **O7** | Shaping by drag: a screen's distance and sector; orders as editable objects (5.2's) | later |

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

## Questions for the user

1. **The split's geometry:** each half 45° off the bearing screened on, ahead when no enemy is in sight, toward the
   nearest enemy in sight when one is. Is that the 45° split you meant, or should the halves keep to the screened
   unit's bows (±45° off its course) whatever the threat?
2. **Screens in action (O5):** stay toward the enemy (as now: between the lines, counter-attacking) or go to the van
   and the rear on the disengaged side once the line is engaged, as the Grand Fleet's orders had it?
3. **A force's own disposition (O6):** should a fleet with its light squadrons in its tree screen and scout by itself,
   or is that the side's commander's (and the scenario's) business, as now?
4. **The smart verb over one of ours:** SCREEN for light squadrons, FOLLOW for heavy, or FOLLOW always?
5. **The task menu:** one Task ▾ popup in three columns by what the order is about, or the research's radial marking
   menu at the cursor (eight fixed directions, flicks) now rather than later?
