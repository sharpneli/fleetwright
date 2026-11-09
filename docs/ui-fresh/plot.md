# The battle map as the admiral's plot (mockup 2026-11-09)

`plot.html`: a functional proposal for the battle map, not a restyle. Keys in the mockup: `1` an order being
composed, `2` watching, `3` on the conn; `0` shows numbered notes; `N` swaps the skin. Screenshots: `png/plot-*.png`.
The scene is the run to the south at 16:02, at the zoom where an admiral works: squadrons, not ships.

## What it is against

The HUD as built has a rail of ships, a strip of five panes and about 35 buttons sorted by department (helm, guns,
task, formation), and a ship card that opens on every click. `battle/command-ui.md` concluded the opposite: command
by intent, the map is the interface, preview before commit. The panels took about 45 % of the screen and the
player still reads the present only.

## Four changes

| # | Change | In place of | Why |
|---|---|---|---|
| 1 | **The rail lists fights.** Each fight is a row: who against whom, the range, guns bearing and hits each way, a word on how it goes. Units not in action follow, each with its order in words; then the enemy not engaged | A tree of every ship with her bar | The admiral's question is "which fight is going wrong", not "which ship". It scales: Jutland is a dozen rows, not 150. A unit opens to its ships on a click, as now |
| 2 | **An order is one sentence, and its words are the controls.** "Engage / the rear of 1st Scouting Group / closing to 14 km / at 24 kn". Click a word to change it; a right-click or right-drag on the map writes the sentence for you. The verbs are listed once under it, grouped as `admirals-orders.md` groups them (the enemy, one of ours, a place, a manoeuvre), greyed with a reason when they can't apply | The strip's five panes, the task menu, the Orders-to pane | One place for every order at every level; the sentence is also what the unit's label says on the map, so what you wrote is what you read. The level (fleet, squadron, ship) is the breadcrumb above it, Q and Shift+Q as now |
| 3 | **Looking ahead.** A ribbon of the next twenty minutes with what is due on it (a flotilla fires, a squadron comes into range, his boats can reach you). Drag it, or hold Ctrl and turn the wheel, and the plot is drawn that far on: your ships on their orders, an order being composed in amber, the enemy as a fan of three courses since he may turn. Let go and it is now again | The dashed line to the target | Every order is judged by where it puts you: "at 14 km by 16:10; as now, 17 km". It is dead reckoning on courses and speeds, no combat, so it is cheap. Ctrl is the research's "preview without committing" |
| 4 | **The helm only on the conn; the card only when asked.** Taking the conn (C) swaps the sentence for the compass tape and the telegraph. The selection shows one line of fitness (ships, speed, guns, afloat, shell); V opens the full card | The helm pane always there; the card opening with the selection | Direct steering is the exception by your own decision 14, so its controls should not sit on screen. The map gets the room |

On the map itself, as `admirals-orders.md` O9b began: every unit's label carries its order and, in amber, the reason
for what it is about to do by itself ("will turn away 2 points at 16:04: outgunned"); a fight is marked between the
two lines with its range and rate; an order with a shape (a flotilla's run in, firing point and turn away) is drawn
as that shape.

## Kept from your decisions

- No decision queue, and the camera never moves by itself (command.md decision 15). The rail is ordered by
  structure, not by time, and a click on it only selects.
- The player's orders are instant (decision 6). Nothing here needs signals; an order in transit, if they come,
  is one more state of the sentence.
- Left selects, right acts, armed verbs complete on the left, Q and Shift+Q for the level, the same letters.
- The player's hand holds; the conn is absolute.

## Open questions

1. Does the rail by fights need the ship tree behind it as a second tab, or is a unit opening in place enough?
2. Looking ahead needs the enemy's estimated course and speed; at what contact level is a fan honest, and when
   should nothing be drawn?
3. Should a plain right-click still send at once (as now) with the sentence shown after, and only a held or
   dragged right button, a verb key or a clicked word compose? The mockup assumes yes.
4. The fight's verdict ("losing the exchange") is a judgement the sim's tactical picture already makes for the AI
   (outgunned). Is showing it to the player wanted, or too much help?
