# The orders strip: the bottom bar (mockup 2026-10-26)

The selection strip at the bottom of the battle map (`MapScene.Strip.cs`, battle-hud.md U2; the enemy's in
`MapScene.Enemy.cs`) and the hint line under it, redrawn in the UI language, as the card (`card.md`) and the rail
(`rail.md`) were. The mockup is `strip.html` (`png/strip-mock.png`; `png/strip-mock-1.png` to `-3.png` with numbered notes,
N toggles them, 1-3 switch the state), drawn over the game's capture of the Jutland at 16:27 with Lion selected
(`png/strip-before.png`); the old strip is painted out by the scene's own capture of that frame, shifted to match
(`png/strip-bg.png`). Three states: **1** orders to a force (the widest the strip gets), **2** orders to the ship alone,
falling out, with Engage… armed, **3** an enemy selected. The ships' state is made up where a state needs it.

## What was wrong with it

- **Not a pane**: a translucent window centred between the columns, grown to its content, heads in small grey capitals,
  no title strips, a hairline between columns. The rail and the card beside it are now docked workstation panes.
- **Keys hidden**: L, K, A, comma and period, C and T worked, but only the tooltips said so; the rest (the helm, the
  telegraph, fire at will, hold fire, smoke, fight, hold) had none.
- **It moves**: Clear route and Back into line appear and vanish, the unit's buttons come and go in one wrapped column, so
  nothing stays under the hand.
- **The hint line** floats under it, over the map's edge labels, beside a scale bar and position that float too.

## The strip

One row of docked panes from the rail's left edge (8 px), touching the rail above it (2 px of ground) and stopping short
of the card dock at most; the card dock now reaches down to the status bar. 105 px tall: a title strip and three rows of
20 px buttons, 5 px apart. The panes size to their content and the row ends where its last pane does (at a force's
level, ~1,450 px, just inside the card at 1,472). A status bar of 20 px runs the whole width under it.

### Orders to

- **Title strip**: ORDERS TO, the keys `Q` `⇧Q` on the right; "+2 more, each" as its note when units are ctrl+picked
  beside it (was "+ 2 more · orders go to each" under the buttons).
- **The chain as table rows with a radio mark**, as Play as on the launch screen: the top unit first, the ship last,
  indented as the rail's tree, the kind on the right (force, squadron, ship) and whose the top unit is as the rail's chip
  (`AI`, `you`, `conn`). A click picks the level, as the segmented buttons did; Q steps up toward the fleet, Shift+Q down
  toward the ship; a double-click on the map still hands the orders to her squadron.
- **The last row** is what's become of her: her place ("her squadron's guide · in line", muted), or in amber falling out
  with **Back into line** `⇧B` beside it, held, her orders changed, following the coast.

### Helm

- **Title**: HELM, the course ordered as its note (`conn` as the green chip instead when on the conn), the course and
  speed she makes on the right in mono.
- **Row 1, the course**: `−90°` `−10°`, the ordered course in an `Inset` well, `+10°` `+90°`. On the arrows: ← → ten
  degrees, Shift ninety; Enter puts the cursor in the well, Enter again sends the course (Esc leaves it).
- **Row 2, the telegraph**: Astern · Stop · Slow · Half · Full · Flank as one segmented control, the bell rung lit; ↓ ↑
  ring a bell slower or faster.
- **Row 3, the toggles**: Hard over `O`, Conn `C`, Clear route `Del` (greyed while there's no route, never hidden), Make
  smoke `M`. Smoke moved here from the torpedoes' column: it's the funnels, and it goes to her squadron as the helm does.

### Guns

- **Title**: GUNS, the main battery's line as its note ("main: tracking · 0 salvos"; the batteries in the tooltip, as
  now), her target on the right.
- **Row 1**: Fire at will `G` · Engage… `E` · Hold fire `X`, one segmented control (one of the three holds).
- **Row 2**: the main guns' shell, Shells ▾ `S` (lit while a kind is held).
- **Row 3**: Torpedoes at… `⇧E` (aimed as E aims the guns), then what's loaded and left, muted.

### Task

- **Title**: TASK, what the unit is for as its note ("battle line · worked out"; brass when set by you, as now), the task
  it's on on the right ("intercept: fighting").
- **Row 1**: Fight `B` · Follow… `F` · Hold `H` as one segmented control, then Task ▾ `T` (the task menu, as now).
- **Row 2**: Withdraw `W`, and AI `I` as a flag, lit while the side's commander has the unit (greyed with the reason in
  the tooltip where it can't be handed over: a ship in a squadron).
- No third row: the old "Squadron…" popup is gone (question 3). Its organising goes into the task menu's organise
  column, beside divide and merge: detach or rejoin, disband, the picked ships added to it (forming a squadron or a
  force of the picked, and leaving one, are there already); the formation and spacing are Formation's `Z`.

### Formation (a unit's only)

- **Title**: FORMATION, how many ships or units as its note, the guide on the right.
- **Row 1**: Line ahead `L` · Columns `K` · Abreast `A`, the whole line's shape (battle-hud.md P1); Columns greyed for a
  squadron (was absent).
- **Row 2**: Turn ▾ `U` (together by 4 or 8 points, the battle turn), Deploy… `D`, to port `,`, to stbd `.`.
- **Row 3**: this level's formation ▾ `Z` ("This level: line abreast, 5,560 m": echelons, lines of bearing, spacing).

A ship alone has no Formation pane.

### An enemy selected

Two panes in the same place: **ENEMY** "as held", her particulars as rows (contact with the rail's diamond and her
identity as held, range and bearing from the ship you had, what's seen, how many of yours fire at her), and **ORDERS
AGAINST HER** "from Lion": Engage her with 1st Battlecruiser Squadron `E`, Engage her with Lion alone `Alt+E`, Torpedoes
at her `⇧E` (greyed with the reason beside it: out of reach, none loaded).

### Status bar

The hint line becomes the status bar the designer and the launch screen have: on the left a state dot and what the mouse
does ("Right-click: steer · on an enemy: engage · on your ship: follow · Shift: waypoint · Alt with any order: this ship
only"), or the armed pick in amber ("Engage: click an enemy · Esc cancels"); on the right the map's own keys and, at the
end, the scale bar and the position (they floated at the bottom right). The bar is there with nothing selected too.

## The keys

Every button wears its key, as the UI language asks. Kept to letters, the arrows, Enter, Del, comma and period: on a
Finnish keyboard `[ ] / ;` need AltGr. Keys are SDL keycodes, so they follow the layout.

| Where | Key | Does |
|---|---|---|
| The map (status bar) | Space, + −, 1–0, Tab ⇧Tab, T, J, V, P, Esc, F1 | as now: pause, speed, speeds, next ship, tasks, log, card, pin, menu, dev |
| Orders to | Q, ⇧Q | the next level up (toward the fleet), down (toward the ship) |
| | ⇧B | back into line (falling out) |
| Helm | ← →, ⇧← ⇧→ | course 10°, 90° |
| | Enter | type a course |
| | ↓ ↑ | the telegraph a bell slower, faster |
| | O, C, Del, M | hard over, the conn (as now), clear route, make smoke |
| Guns | G, E, X, S | fire at will, engage…, hold fire, shells ▾ |
| | ⇧E | torpedoes at… |
| Task | B, F, H, T, W, I | fight, follow…, hold, the task menu (as now), withdraw, AI |
| Formation | L, K, A (as now), Z, U, D, `,` `.` (as now) | line ahead, columns, abreast, this level's formation ▾, turn ▾, deploy…, deploy to port, to starboard |
| An enemy | E, Alt+E, ⇧E | engage her with the unit, with the ship alone, torpedoes |
| Any order | Alt + its key | to this ship alone (as Alt does for a right-click now) |

Where the task menu has the same order the key is the same (B fight, F follow, W withdraw). While the task menu is open its
own letters win, as now (its S, E, G, H, I, O, D, M, K, L, A, Q mean its own orders there).

## For the port

- **Pieces**: the strip's panes are `Pane`, `Button` (its key chip), `Flag` for the toggles and a **segmented control
  with a key chip a segment** (`Segmented` today takes no keys: one more argument), table rows with `Radio`, `Chip`,
  `StatusBarBg`. The well is the `Stepper`'s value well alone. Shift is drawn in a `Kbd` chip as a shape (an outlined
  up-arrow) before the key, as the UI language draws marks the fonts lack; the arrows are in the fonts.
- **One table of orders**: each button's label, key, tooltip and what it sends in one list the strip draws and `Key()`
  reads, so a key can't drift from its button; the task menu's `TaskRows` is the pattern.
- **Layout**: the rail already stops 2 px above the strip's top; the card dock's bottom moves down to the status bar;
  `StripCentre` goes (the strip starts at the rail's left edge).
- **No allocation on a steady frame**, as the rail and the card: the heads' strings made when what they say changes, as
  now.

## Questions, answered (the user, 2026-10-26)

1. **The helm on the arrows**: yes. WASD will pan the map in a later rework; an idea for later, for fun: a locked mode,
   the camera held on the ship and WASD her helm (a torpedo boat run by hand).
2. **Q for the chain**: fine.
3. **Organise ▾**: dropped. Popups interrupt the flow of combat: the default behaviours should be set, and the player
   not poked about decisions without the context for them at hand. The organising moves into the task menu.
4. **Make smoke under the helm**: fine.
5. **Left-aligned**: yes, so a pane doesn't move when the orders switch between the ship and a unit.
6. **The status bar**: stays, always there (it's small).

The questions as asked:

1. **The helm on the arrows**: ← → 10°, Shift 90°, ↑ ↓ the telegraph. Fine, or are the arrows wanted for panning the map
   later?
2. **Q for the chain** (up toward the fleet, Shift down), or another key?
3. **Organise ▾**: the old "Squadron…" popup (detach, rejoin, formation, leave). Its organising half is already in the
   task menu's last column; fold the rest (detach, rejoin) in there and drop the button?
4. **Make smoke under the helm** rather than beside torpedoes: fine?
5. **The strip left-aligned under the rail**, ending where its last pane does (the map shows past it at a ship's level),
   or stretched to the card dock?
6. **The status bar always there**, even with nothing selected, with the scale and position moved into it?
