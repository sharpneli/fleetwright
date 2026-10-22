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
| **U3b** | The own ship's card and the dock with pinned tabs; the plan from the ship's sprite (`ShipArt`) with the marks drawn over it; the profile baked once per class where the class is built | the profile bake (as `ProfileScene` does) in the class's `Built` hook |
| **U4** | The forces rail; the enemy card; the squadron card. Fix the two leaks first: the combat log goes out unfiltered, and sighted ships export their part states whole | `CombatLog` filtered by `ShownSides`; `Sighted` without `PartStates` |
| **U5** | Map marks: own labels with speed and flotation bar, contacts' seen flags, the selection's fire line in brass with its range, torpedo reach, orders sent and in transit, hover tooltip, friend blue and hostile red | — |
| **U6** | The ticker and the log (J), with filters; notices for falling out, a contact identified, a ship lost (the exe diffs the snapshot for these); "order her back" an order the captain obeys (decision 2) | the log's lines by kind (exists); falling out held off for a ship the player ordered |
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
