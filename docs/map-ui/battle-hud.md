# The battle HUD: the map's real UI for the scenario battler (2026-10-22)

The map's UI has been dev panels since step 2.3: a spawner, a clock window, and a strip at the bottom that grew a
row per step (helm, search, captain, guns, torpedoes, smoke, damage, squadron). This plan replaces it with the game's
UI for what ships first: **a scenario battler**, one fight from briefing to result, no research, yards or politics.
It is step 5.2 of `docs/battle/command.md` ("the command UI on the map"), narrowed to that game.

The mockup is `battle.html` (open it in a browser; `#ship`, `#enemy`, `#group`, `#pinned`, `#order`, `#arm`,
`#log`, `#menu`, `#brief`, `#result`, `#dev` open a state; screenshots `png/battle-*.png`). The admiral's orders'
states (`#tasks`, `#screen`, `#deploy`, `#chain`: the task menu, roles, the chain of command) are
`docs/battle/admirals-orders.md` O8's. It takes its layout and
symbology from the v2 world mockup (`map.html`, `README.md`) and drops what the battler doesn't have: the screens,
lenses, sea areas, ports, the globe band's clusters. As for the designer: a mockup first, then the port, one piece a
session, each judged by captures against the mockup.

## What the player sees

| Piece | Where | Shows | Mocked |
|---|---|---|---|
| Top bar | top, 34 px | side and scenario; date and clock; pause and speed detents (the coarse ones greyed: guns silent); held speed; in action for how long; hits scored and taken, lost and sunk; the aim and the end; the menu | all states |
| Forces rail | left | your squadrons and their ships (flotation bar, fire, falling out, target, speed, hits), then the enemy in sight by what you hold of them, with range and bearing from the selected ship; redrawn as three docked panes with the log in `rail.md` (2026-10-26) | all |
| Card dock | right, full height | the selection's card, opened by the click that selects; pinned cards as tabs above it | `#ship`, `#enemy`, `#group`, `#pinned` |
| Selection strip | bottom, beside the ticker | the orders: helm, guns, torpedoes and smoke, the task, the formation; to the squadron or this ship | `#ship`, `#order`, `#arm` |
| Notices | (dropped 2026-10-22, distracting) | falling out is told in the ticker; the strip's "Back into line" overrules it | all |
| Ticker | bottom left | the last five events, fading; a click jumps to the ship. J opens the whole log, filtered. Since 2026-10-26 the rail's LOG pane (`rail.md`) | `#log` |
| Map marks | the map | own ships named with speed and a flotation bar; contacts by identity with what's seen of them; fire lines, the selection's in brass with the range; torpedo reach; orders sent, amber while the signal travels; the cursor's verb | all |
| Briefing | modal, at load (paused) | the situation, the aim, your side's orders before action, your forces and the enemy as reported | `#brief` |
| Result | modal, at the end | the count, every ship's fate, the truth about the enemy | `#result` |
| Menu | Esc with nothing open | resume, briefing, restart, scenarios, the auto-pause rules (from the clock panel today), UI scale, quit | `#menu` |
| Dev panel | F1 | everything that isn't the game: built (U0) | `#dev` |

## The ship card (the centre of it)

A click on a ship selects her and opens her card in the dock; the card follows the selection. ✕ or V closes it until
the next click; ⚲ (P) pins it as a tab, so Lion's card stays one click away while you look at her target.

- **Own ship**: name, class, squadron, captain; chips for in action, who commands her (AI captain and his task, or
  you), falling out and why. Four big numbers: speed (of what's left), course (ordered), target and range, hits.
  Then sections, each folding:
  - **Condition**: her plan (the map's own sprite) with the mounts ringed by state, the holes (penetrated, stopped,
    upper works), the fires; her profile with the waterline now against the design's and the flooded spaces filled.
    Flotation, list and trim, fires, power and steering, crew lost.
  - **Guns**: each mount: bearing, target, state (firing, loading, training, not bearing, jammed until, out and
    why), rounds by kind. The secondary by side.
  - **Fire control**: the target and how she's seen, range and the plot's error, control (director level or local),
    the phase (opening, ranging, effect), salvos, straddles, hits seen, rate; what hampers spotting.
  - **Damage control**: each party and its job, pumps, counterflooding, doctrine and safe speed.
  - **Hits taken**: newest first, where, what, what it did.
  - **Torpedoes**, **Crew**.
- **Enemy**: only what we hold. Identity and how it was made (the contact's level, by whom, when); range, bearing,
  course and speed as estimated; what a lookout sees (burning, low, listing, stopped, silent); the recognition
  book's figures once her class is known; our fire on her (who, salvos, straddles, hits seen) and hers; verbs:
  concentrate the squadron on her, engage with one ship, torpedoes.
- **Squadron**: its ships in a table (flotation, hits, turrets, target, speed), the fire distribution, the task and
  who gave it, doctrine; take command, turn, withdraw, leave it to the commander.

## Commanding

The strip carries every order the current strip does, regrouped: **helm** (course ±10/±90, the telegraph's bells),
**guns** (fire at will, engage…, hold fire; shells: the magazine order or a kind), **torpedoes** (fire at…) and
**make smoke**, the **task** (fight, follow…, run from…, hold; search… opens the pattern's popup) and the
**formation** (line ahead ▾, turn ▾, withdraw), with **AI ✓** showing the unit is its AI's: any order of yours takes
it, a click hands it back. "Squadron / This ship" picks who gets it; Alt does it for one order.

On the map, with your ship or squadron selected, the cursor names what a right-click does: STEER 152° · 3.4 km over
the sea, ENGAGE Lützow · 12.7 km over an enemy, FOLLOW Tiger over your own. Shift adds a waypoint. "Engage…" and
"Fire at…" arm the next left click on an enemy instead (Esc disarms). Tab steps through your ships.

## Steps

Each a session: the piece ported to ImGui (the designer's theme and fonts), checked by `-ui` captures beside the
mockup's, committed.

| Step | What | Needs from the sim |
|---|---|---|
| **U0** | The dev panel on F1 (`-dev` opens it): the spawner, scenario picker, sides, truth, horizon, the dev salvo. **Done 2026-10-22.** | — |
| **U1** | The designer's theme on the map; the top bar replaces the clock window and the menu bar (decision 3: the designer from a menu at its right, the viewers as the designer's own tabs); Esc never quits (decision 4): it shuts the topmost thing, else opens the app's menu (the switcher's, with the map's items: briefing, restart, scenarios, the clock's rules on contact); the briefing at load from the menu (`-brief` at start); the "where" panel down to the scale bar and position (the rest to the dev panel). **Done 2026-10-22.** The bar's tally (hits, lost, sunk) waits for U6's event stream | the scenario's brief, aims, orders before action: already in `WorldSession.Scenario` |
| **U2** | The selection strip and the smart verb (`MapScene.Strip.cs`): who (the ship or a unit above her, as a segmented switch; falling out, held, altered for land), helm (±10°/±90°, typed, hard over, the telegraph), guns (fire at will, engage…, hold fire; each battery in the tooltip), torpedoes and smoke, the task (fight, follow…, run from…, hold, search…; the formation, withdraw, AI, the squadron's business in popups); the condition in a popup until U3's card; right-click on an enemy engages, on your ship follows; armed picks a click completes; Tab; a double-click hands the orders to her squadron. **Done 2026-10-22.** The main guns' Shells ▾ (by target, or a kind held) since docs/battle/shell-choice.md A5 (2026-10-25). A redraw on the UI language's pieces mocked up 2026-10-26, every order with its key and a status bar under it: docs/map-ui/strip.md | — (all orders exist) |
| **U3a** | The sim's side of the card: `Snapshot.Details` (`ShipDetailView`) for the ships the host asks for (`SimHost.Detail`, at most four: the selection and pinned cards; shown sides only): each mount's state (ready, loading, jammed, down, out) and until when, guns out, rounds by kind; the crew (complement, killed, wounded, out); damage control's parties (men, job, fire, man-minutes) and pumps; the spaces with water (share, counterflooded); the holes; the magazines flooded. `BatteriesView` gains each battery's range, plot error, straddles, salvos in the air, misses and whether it sees her. The snapshot carries the log's newest 256 lines (was 64), so the exe keeps each ship's hits whole by reading the new ones each frame. **Done 2026-10-22**; allocates nothing once grown (tested) | `DamageSystem.FillDetail`, `GunnerySystem.Fill` |
| **U3b** | The own ship's card (`MapScene.Card.cs`) in a dock at the right, opened by the click that selects (V shuts and opens it; P pins her as a tab, three at most; the strip centres left of it): name, class, squadron, who commands her, falling out; speed (of what she makes now), course, target and range, hits; her condition drawn from her class, not the sprite: the plan (the main deck's outline, upper works, her mounts ringed by state and lettered A, B, Q, X from their positions, wrecked parts outlined, fires, holes) and the profile (each space outlined, the water in it to its share, counterflooding green, holes, her design's waterline dashed and her waterline now); flotation, list and trim, fires, power and steering, safe speed, crew; each mount's state and rounds by kind, fore to aft; each battery's fire control (range, plot error, in sight, control, the phases lit, salvos, straddles, hits, in the air); damage control (the parties at work, the rest standing by in a line, pumps, counterflooding left, magazines flooded); the hits she took (`CombatFeed`: every log line the snapshots brought, by target); torpedoes. Text without garbage (`U8`: interpolation straight into UTF-8, drawn natively); the world's perf report gains the map UI's bytes a frame (295 with a card open, the labels' that U5 redoes). **Done 2026-10-22.** The sprite in the card was dropped for the class drawing: exact, and it shows the water Redrawn 2026-10-26 on the UI language's pieces, the condition as a damage plate: docs/map-ui/card.md | — |
| **U4** | The leaks first: the snapshot's combat log keeps only lines with a shown side in them, a hit on a ship not shown keeping what her enemy's watchers see (her fire, her magazine, broken in two: no part, no outcome), each line with its place in the whole log (`CombatLogView.Seq`); an enemy in sight shows only her outside wrecked and fires that show (no rooms, no jams, no outages). Tested. Then the rail (`MapScene.Rail.cs`: your units and ships with flotation, fires, target or falling out, speed and hits; a big order of battle scrolls, its squadrons' ships shown when open or holding the selection; the enemy nearest first with what's seen); the enemy card and strip (`MapScene.Enemy.cs`: her identity as held, range, bearing, estimated course and speed, what's seen, the book's figures once her class is made out, our fire on her and hers; engage her with your ship's squadron or the ship, torpedoes when in reach); the unit's card (its ships in a table, its formation and task, whose it is; formation, fight, withdraw, the commander). Contacts are named by one `Identity` everywhere (the strip, cards, cursor). **Done 2026-10-22** | `GunnerySystem.FillLog`, `ShipSystem.PutDamage` |
| **U5** | Map marks (`MapScene.Marks.cs`): your ships labelled at their left (name; speed, falling out or going down; a flotation bar), one a screen cell; the icons in friend blue and enemy red by the player's side (the side tints only while watching), contacts in enemy red (yellow until made out); the selected ship's fire line in brass with its range at the middle, your other fire in friend blue, the truth's in red, faint unless firing; her torpedoes' reach as two rings, labelled; a ship's tooltip under the cursor (hers: speed, hits, fire, falling out; an enemy's: identity, what's seen, range). The old reason labels went (the ship labels say it); the group, encounter, contact and wreck labels' text is made when it changes. The map UI makes 112 bytes a frame with a card open, the event-driven rest (a hit's lines; no GC over the run). **Done 2026-10-22.** Not done: orders in transit (signals are 5.1e), edge markers (5.2's rest) | — |
| **U6** | The events (`MapScene.Events.cs`): from the log the player may know (our hits, our first straddle on each target, the hits we took with what they did, torpedoes, losses, damage control's news) and from the snapshot's changes (falling out, a fire starting, a contact first seen, made out, identified), each made once; the ticker (the newest five at the bottom left, above the strip, fading; a click goes to her) and the log (J: newest first, filtered: all, ours, gunnery, damage, orders, seen); the top bar's tally (hits scored and taken, lost, sunk); a ship of yours falling out is told in the ticker, and the strip offers "Back into line" while she's selected (the notices panel at the top went, 2026-10-22: the user found it distracting). Decision 2 in the sim: the player's order to a ship fallen out sets her captain's `Overruled` (hashed), and she won't fall out again (tested, and the test fails without it). **Done 2026-10-22** | `CaptainLayer` (`Captain.Overruled`) |
| **U7** | The result: `SimHost.Judge` runs the scenario's `ScenarioEnd` on the sim thread each time the clock crosses a minute (it reads the world, changes nothing); once it's over the host publishes an `EndView` in every snapshot after (why and when, each side's count, each ship's fate: the truth on both sides, decision 5). The map opens the result once (`MapScene.Result.cs`: the verdict, a victory by aims met, else the side that lost much less, else a draw; the count; every ship's fate), the clock stopped under it; Carry on runs it again (the end is a score, never a cut-off of a fight in progress), Play again, Scenarios; the menu opens it again. **Done 2026-10-22** | `SimHost.Judge`, `Snapshot.End` |

After these, the rest of 5.2 as `command.md` lists it: orders as editable objects, the radial menu, the doctrine
card, edge markers for what's off screen, and the rumble for a fight at speed.

## Decisions (the user, 2026-10-22)

1. **A click opens the card**, for now: one click selects and opens it; ✕ or V shuts it until the next click.
2. **The player may overrule**: captains' and admirals' own choices (falling out among them) give way to the
   player's order. It makes the player a little unfairly strong, but their attention is on one place at a time, and
   that attention is what's rewarded. So the notice's "Order her back into line" is a real order, and the captain
   obeys it at his ship's risk (`docs/battle/command.md` holds units the player has ordered; falling out joins the
   things a held unit doesn't do on its own).
3. **The tool tabs** leave the menu bar: the designer is one entry in a menu at the top right, and the hitbox,
   ship and profile viewers are tabs inside the designer. The map has the whole screen.
4. **Esc never quits**, anywhere: it shuts the topmost thing, and with nothing open it opens the menu (which has
   Quit). The same in the designer and its viewers.
5. **The result tells the truth**: both sides' real losses, for bragging rights and as the score. Limited knowledge
   matters more in the campaign; the battler shows everything at the end.
6. **Two ways to command, and the conn** (the user, 2026-10-23, after a Jutland playtest: a smoke-laying destroyer's
   captain kept combing tracks under fire; "sometimes one has to introduce gallantry by force of will"). General
   orders (the task column: Fight, Follow, Screen, Search, tasks, formations) leave the minute to minute to the unit's
   captain or admiral; helm orders (the HELM column, right-click on the sea, waypoints) hold the unit to the player's
   course, only combing torpedo tracks still taking it; **the conn** is absolute: a **Conn** switch at the HELM
   column's end and **C**, on the level the order picker has (this ship, her squadron, a force). The head reads
   "CONN · course · speed" in the conn's green (a ship in a line on the conn: "LINE ON CONN"), every ship on it is
   ringed green on the map (she won't save herself, so she's never lost from sight) and a squadron's label says
   "conn". Her lookouts still call tracks coming at her on the ticker ("torpedo tracks to starboard!"); the player
   decides. A general order, or a hand-back, gives the conn back; a helm order keeps it. The sim's side is
   `docs/battle/command.md` "The conn".

## For the review (U0-U7 built 2026-10-22, while the user was away)

**Try it:** `dotnet run --project src/Fleetwright -- -scenario=squadrons -brief` (the briefing first), or Esc, then a
scenario from the menu. Click a ship (her card), an enemy in the rail or on the map (what we hold of her), a squadron's
head in the rail (its card). Right-click with one of yours selected: steer, engage, follow. P pins a card, V shuts it,
Tab steps your ships, J the log, F1 the dev panel, Esc shuts the topmost thing or opens the menu. Captures beside the
mockup's: `png/game-u*.png`.

**Where the build departs from the mockup:**

- The card's plan and profile are drawn from the class (its deck outline, upper works, mounts, cells), not the sprite:
  exact, and the profile shows the water in each space against the waterline. Its heights are drawn at twice the
  lengths' scale so the spaces read; say if that should be true scale.
- Mounts are lettered by position (forward A, B; amidships Q; aft X, Y; secondaries S1, P1 from forward): the Royal
  Navy's habit, a label only. Other navies' habits could follow the look's navy later (looks never set a number).
- ~~No shell-kind order on the strip~~: the GUNS column's Shells ▾ since 2026-10-25 (docs/battle/shell-choice.md A5).
- The ticker sits above the strip at the bottom left (the strip passes under it when a card is open).
- The result's verdict: a side whose aims are met wins; else the side that lost much less (under half the other's
  share of tonnage) "had the better of it"; else a draw. A time end doesn't stop the fight: Carry on runs the clock.
- "Back into line" (the strip's, while she's selected; the top notices were dropped as distracting) sends her a Fight
  order (that's the overrule, `Captain.Overruled`) and adds her to her old squadron.

**Not done (the rest of 5.2, as planned):** orders in transit (signals are 5.1e), edge markers for what's off
screen, the radial menu, the doctrine card, the rumble for a fight at speed, orders as editable objects on the map.

**Rough edges seen:** hit lines name the design's parts as they are ("Quarters 6", "Bunker 1"); the dev world's ships
have no names ("Lion-like (1912) #2"); the dev panel, opened, covers the rail; enemy hulls in sight are still the
renderer's icons (only their tint changed); the card's "Her fire" reads the feed's newest salvo of hers at our ships.

**Garbage:** the map UI's bytes a frame are in the `-frametimes` report: 94 at the Jutland, 118 in Squadrons with a
card open; the rest event-driven (a hit's lines, the tally); no GC over either run.

## Playtest notes (2026-10-25 on)

Fixes from the user's playtests, each a small step on the built HUD (no mockup unless it grows).

### P1 Big formations in one click (built 2026-10-25)

**The note:** the Grand Fleet in columns abreast took many clicks to make one line. The formation picker set only the
level picked, so the Grand Fleet in line ahead still had its battle squadrons' two divisions abreast inside, one
popup each, and its spacing (the columns' mile) was lost on the way. The player usually commands the battle line and
one or two detachments (2-3 units, the rest the AI's), so a fleet must take orders as easily as a division.

- **Whole-line shapes** (`LineShape`, `GroupSystem.Shape`, the order `UnitOrders.Shape`): **Line ahead** (every
  squadron and force in one line, the squadrons as far apart as their own ships), **Columns** (the cruising order:
  each squadron a column, the columns abreast as far apart as the longest column is long, the era's rule that lets the
  columns turn in succession into one line) and **Abreast** (every ship abreast), each one order to the unit however
  deep it nests. The spacings come from the squadrons, so nothing is lost going from one shape to another and back.
- **Forming:** a fleet given a shape makes for its final places, each column steering for its place on the guide as a
  deployment's do (`AdmiralLayer.FinalStation`), not astern of a column still finding its own (which formed them one
  after another: 95 min for six columns of four). Unlike a deployment it takes up a fight meanwhile; formed, or after
  `DeployMaxMs`, the line's own stations again. A new order to the unit ends the forming, as it ends a deployment.
- **Deploy to port, to starboard:** the deathsnake's quick way, one click each: the deployment 8 points to that side
  of the guide's heading, the wing column on that side leading round (Jellicoe's "equal speed, Charlie London").
  `Deploy…` keeps the course or the enemy picked by a click. The deployment's `IntoLine` now ends in the same Shape
  command (one implementation).
- **The strip** at a unit's level: `Line ahead · Columns · Abreast` (Columns for a force) lit by the shape its whole
  line is in, then this level's formation ▾ (echelons, lines of bearing, the spacing), then `Turn ▾ · Deploy… · to
  port · to stbd · Withdraw · AI`. **Keys** with a unit picked: L line ahead, K columns, A abreast, comma and period
  deploy to port and starboard. Several units picked: each gets it.
- **Measured** (`LineShapeTests`: the Grand Fleet in little, three squadrons of two divisions of four, at 14 kn): one
  line ahead on the same course in 64 min, back into columns in 83 min; deploying (`DeploymentTests`) in 11. On the
  same course the far columns drop back a column's length each at most `FollowMaxSpeedDiffKn` (6 kn) slower, so the
  line is slow, as it was at sea; the button's tooltip says to deploy for a quick one. In the Jutland, the Grand Fleet
  deployed to port by one click is nearly one line 8 minutes later
  (`docs/map-ui/png/game-p1-deploy.png`).
- **Open:** ships dropping back to a far station overshoot it by 1-2 km and close again at 5 kn (`docs/battle/bugs.md`,
  the AI); a course change of a fleet in columns still wheels it about the guide (the far wing races), where the era
  turned columns in succession or together. Both for a playtest to rank.


### P2 A line that holds its course (built 2026-10-25)

**The note:** the Grand Fleet, the player's, kept turning away from small ships with no torpedoes in the water; a
brutal course couldn't be held.

- **The player's helm holds** (decision 6): the admiral's turn-away from torpedoes (`AdmiralLayer.TurnAway`) was off
  only on the conn; now also while the line steers by the player's helm order by hand (a course, a speed, a route, a
  deploy, holding on: `HelmByHand`). Under a general order of his (Fight) it still turns away from an attack, and
  captains still comb tracks they see.
- **An attack is boats running in:** the threat (`TorpedoThreat`) was torpedo craft within 8 km closing at 3 m/s,
  which counted the line's own speed, so a 14-knot line steaming at any destroyer ahead (lying still, screening,
  crossing) saw an attack. Now it's the craft's own speed toward a ship of the line, at least `TorpedoThreatRunInMs`
  (6 m/s: a 25-knot boat heading within some 60° of her; renamed from `TorpedoThreatClosingMs`).
- **Tests** (`TurnAwayTests`): a line on the player's helm holds on through the destroyers' run; a line steaming at
  boats lying still 7 km ahead holds on (both turned 45° and 49° under the old rules); the attack's turn-away to 225°
  and back as before.
- **Measured** (Release, AI on every side, against `0a794c9e`): the Jutland, 3 seeds: Britain lost 30.3 of 151
  (77 kt) against 23.0 (62 kt), Germany 37.0 of 99 (223 kt) against 46.0 (253 kt); hits 3,746 and 3,440 against 5,008
  and 3,916; torpedoes 191 and 124 against 218 and 138. The seeds spread wide (Britain 12 to 46 lost), so 3 say little
  beyond the direction: lines that hold on more. `tsushima`, 5 seeds: Russia 3.0 of 13 against 3.6, Japan 3.8 of 14
  against 4.4.
