# The battle HUD: the map's real UI for the scenario battler (2026-10-22)

The map's UI has been dev panels since step 2.3: a spawner, a clock window, and a strip at the bottom that grew a
row per step (helm, search, captain, guns, torpedoes, smoke, damage, squadron). This plan replaces it with the game's
UI for what ships first: **a scenario battler**, one fight from briefing to result, no research, yards or politics.
It is step 5.2 of `docs/battle/command.md` ("the command UI on the map"), narrowed to that game.

The mockup is `battle.html` (open it in a browser; `#ship`, `#enemy`, `#group`, `#pinned`, `#order`, `#arm`,
`#log`, `#menu`, `#brief`, `#result`, `#dev` open a state; screenshots `png/battle-*.png`). It takes its layout and
symbology from the v2 world mockup (`map.html`, `README.md`) and drops what the battler doesn't have: the screens,
lenses, sea areas, ports, the globe band's clusters. As for the designer: a mockup first, then the port, one piece a
session, each judged by captures against the mockup.

## What the player sees

| Piece | Where | Shows | Mocked |
|---|---|---|---|
| Top bar | top, 34 px | side and scenario; date and clock; pause and speed detents (the coarse ones greyed: guns silent); held speed; in action for how long; hits scored and taken, lost and sunk; the aim and the end; the menu | all states |
| Forces rail | left | your squadrons and their ships (flotation bar, fire, falling out, target, speed, hits), then the enemy in sight by what you hold of them, with range and bearing from the selected ship | all |
| Card dock | right, full height | the selection's card, opened by the click that selects; pinned cards as tabs above it | `#ship`, `#enemy`, `#group`, `#pinned` |
| Selection strip | bottom, beside the ticker | the orders: helm, guns, torpedoes and smoke, the task, the formation; to the squadron or this ship | `#ship`, `#order`, `#arm` |
| Notices | top centre | what a captain or admiral did that you'd want to know and might overrule (falling out) | all |
| Ticker | bottom left | the last five events, fading; a click jumps to the ship. J opens the whole log, filtered | `#log` |
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
| **U2** | The selection strip and the smart verb (`MapScene.Strip.cs`): who (the ship or a unit above her, as a segmented switch; falling out, held, altered for land), helm (±10°/±90°, typed, hard over, the telegraph), guns (fire at will, engage…, hold fire; each battery in the tooltip), torpedoes and smoke, the task (fight, follow…, run from…, hold, search…; the formation, withdraw, AI, the squadron's business in popups); the condition in a popup until U3's card; right-click on an enemy engages, on your ship follows; armed picks a click completes; Tab; a double-click hands the orders to her squadron. **Done 2026-10-22.** No shell-kind order yet: the strip shows none (the sim's loadout is the scenario's) | — (all orders exist) |
| **U3a** | The sim's side of the card: `Snapshot.Details` (`ShipDetailView`) for the ships the host asks for (`SimHost.Detail`, at most four: the selection and pinned cards; shown sides only): each mount's state (ready, loading, jammed, down, out) and until when, guns out, rounds by kind; the crew (complement, killed, wounded, out); damage control's parties (men, job, fire, man-minutes) and pumps; the spaces with water (share, counterflooded); the holes; the magazines flooded. `BatteriesView` gains each battery's range, plot error, straddles, salvos in the air, misses and whether it sees her. The snapshot carries the log's newest 256 lines (was 64), so the exe keeps each ship's hits whole by reading the new ones each frame. **Done 2026-10-22**; allocates nothing once grown (tested) | `DamageSystem.FillDetail`, `GunnerySystem.Fill` |
| **U3b** | The own ship's card (`MapScene.Card.cs`) in a dock at the right, opened by the click that selects (V shuts and opens it; P pins her as a tab, three at most; the strip centres left of it): name, class, squadron, who commands her, falling out; speed (of what she makes now), course, target and range, hits; her condition drawn from her class, not the sprite: the plan (the main deck's outline, upper works, her mounts ringed by state and lettered A, B, Q, X from their positions, wrecked parts outlined, fires, holes) and the profile (each space outlined, the water in it to its share, counterflooding green, holes, her design's waterline dashed and her waterline now); flotation, list and trim, fires, power and steering, safe speed, crew; each mount's state and rounds by kind, fore to aft; each battery's fire control (range, plot error, in sight, control, the phases lit, salvos, straddles, hits, in the air); damage control (the parties at work, the rest standing by in a line, pumps, counterflooding left, magazines flooded); the hits she took (`CombatFeed`: every log line the snapshots brought, by target); torpedoes. Text without garbage (`U8`: interpolation straight into UTF-8, drawn natively); the world's perf report gains the map UI's bytes a frame (295 with a card open, the labels' that U5 redoes). **Done 2026-10-22.** The sprite in the card was dropped for the class drawing: exact, and it shows the water | — |
| **U4** | The leaks first: the snapshot's combat log keeps only lines with a shown side in them, a hit on a ship not shown keeping what her enemy's watchers see (her fire, her magazine, broken in two: no part, no outcome), each line with its place in the whole log (`CombatLogView.Seq`); an enemy in sight shows only her outside wrecked and fires that show (no rooms, no jams, no outages). Tested. Then the rail (`MapScene.Rail.cs`: your units and ships with flotation, fires, target or falling out, speed and hits; a big order of battle scrolls, its squadrons' ships shown when open or holding the selection; the enemy nearest first with what's seen); the enemy card and strip (`MapScene.Enemy.cs`: her identity as held, range, bearing, estimated course and speed, what's seen, the book's figures once her class is made out, our fire on her and hers; engage her with your ship's squadron or the ship, torpedoes when in reach); the unit's card (its ships in a table, its formation and task, whose it is; formation, fight, withdraw, the commander). Contacts are named by one `Identity` everywhere (the strip, cards, cursor). **Done 2026-10-22** | `GunnerySystem.FillLog`, `ShipSystem.PutDamage` |
| **U5** | Map marks (`MapScene.Marks.cs`): your ships labelled at their left (name; speed, falling out or going down; a flotation bar), one a screen cell; the icons in friend blue and enemy red by the player's side (the side tints only while watching), contacts in enemy red (yellow until made out); the selected ship's fire line in brass with its range at the middle, your other fire in friend blue, the truth's in red, faint unless firing; her torpedoes' reach as two rings, labelled; a ship's tooltip under the cursor (hers: speed, hits, fire, falling out; an enemy's: identity, what's seen, range). The old reason labels went (the ship labels say it); the group, encounter, contact and wreck labels' text is made when it changes. The map UI makes 112 bytes a frame with a card open, the event-driven rest (a hit's lines; no GC over the run). **Done 2026-10-22.** Not done: orders in transit (signals are 5.1e), edge markers (5.2's rest) | — |
| **U6** | The events (`MapScene.Events.cs`): from the log the player may know (our hits, our first straddle on each target, the hits we took with what they did, torpedoes, losses, damage control's news) and from the snapshot's changes (falling out, a fire starting, a contact first seen, made out, identified), each made once; the ticker (the newest five at the bottom left, above the strip, fading; a click goes to her) and the log (J: newest first, filtered: all, ours, gunnery, damage, orders, seen); the top bar's tally (hits scored and taken, lost, sunk); a notice when one of yours falls out: show her, order her back into line, let her go. Decision 2 in the sim: the player's order to a ship fallen out sets her captain's `Overruled` (hashed), and she won't fall out again (tested, and the test fails without it). **Done 2026-10-22** | `CaptainLayer` (`Captain.Overruled`) |
| **U7** | The result: `ScenarioEnd` run on the sim thread at whole minutes, its result in the snapshot; the result modal, the truth on both sides (decision 5); the clock stops, the map stays to look round | `ScenarioEnd` out of the CLI |

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
