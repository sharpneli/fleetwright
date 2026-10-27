# Input: the map's mouse, camera and clock keys (agreed 2026-10-26)

The battle map's controls reworked to the usual RTS grammar: **the left button selects, the right button acts**, the
camera on WASD and the middle button, the clock on a few keys the left hand finds blind. Builds on the research's input
map (`docs/battle/command-ui.md`, Default input map) and the strip's key table (`strip.md`, The keys), which this
supersedes where they differ.

## Everywhere

The controls are the same in every view (the user, 2026-10-26: "a critical usability cornerstone"): once learned, the
player expects identical behaviour everywhere. A view that pans takes `PanKeys` (WASD, one implementation), the middle
drag and click, the wheel at the cursor; the left button selects, the right acts. Done for the battle map and the launch
screen's opening (**U1**, 2026-10-26: placing a unit on the right button, the launch screen's S, A and D moved to V, N
and P). **U2** (2026-10-26, question 6): the ship viewer pans by WASD and the middle drag; the hitbox view's middle
button is the camera's (its drag orbits, with shift it pans), WASD pans, the left selects a prism, the right fires (a
ray, or a shell as the panel picks; shift+right a ray). The free camera of a scene-less engine (`Camera.cs`) is left
as it is: no player sees it.

## Before

- Left: a drag panned, a click selected, and with a verb armed (Engage…, Follow…, any task's pick) a click picked its
  target: three jobs on one button.
- Right: the smart order fired on the press (engage an enemy, follow or screen one of ours, steer for the sea; Shift a
  waypoint). No drag, no preview.
- Middle: nothing. WASD: four orders (W withdraw, A abreast, S shells, D deploy…).
- Clock: 1–0 ten of the thirteen speeds, + − a step, Space pause, the wheel over the bar.

## The grammar

| Input | Does |
|---|---|
| **W A S D** | pan, at a share of the screen a second whatever the zoom, paused too; a short ease so a tap nudges. Scancodes: they stay under the hand on any layout (the orders' letters stay keycodes, for their mnemonics) |
| ⇧ + WASD | pan faster (no ⇧ order lives on W, A, S or D) |
| Middle drag | pan, the point under the cursor held (the left drag's grab) |
| Middle click | the sea: the map centres there; a ship: the camera follows her until a pan |
| Wheel | zoom at the cursor (as before); the map stays north-up, so no rotate keys |
| **Left click** | select: a ship; double-click her squadron; Ctrl+click adds or takes (decision 2); the empty sea deselects |
| Left drag | box select: the ships inside; a squadron or force wholly inside goes as that unit |
| **Right click** | the order the cursor names, given on the release; while held the order is drawn as it would go |
| Right drag | shapes the order, never changes its verb: on the sea go here arriving on this heading; a force in columns deploys along the line |
| A verb's key | arms it: the next right click aims it, the cursor names it in amber; Esc disarms, a second Esc deselects |
| ⇧ right | queue (a waypoint), as before |
| Alt + an order | to this ship alone, as before |

The left button never does anything but select: an armed verb changes only what the right button does (decision 1).

## The keys that moved

| Order | Was | Now |
|---|---|---|
| Shells ▾ | S | **R** (rounds) |
| Abreast | A | **⇧L** (L line ahead, ⇧L line abreast) |
| Deploy… | D | **N** (`,` `.` as before); a force's right drag deploys too once I3 is in |
| Withdraw | W | **no key**: the button and the task menu. Keys are for what's used often (decision 3) |

The task menu keeps its letters (S screen, A attack, W withdraw, D divide among them): it is a menu you open with T and
that shuts on a pick, as StarCraft's build menus (B, then a letter). While it's open its letters win and the middle
button pans.

The left hand's cluster: Q (level up), E (engage), R (shells), F (follow), T (tasks) round WASD; the helm on the arrows
for the right hand (strip.md decision 1).

## The clock

The number keys stay the clock's (decision 4): a speed is changed far more often than a unit is recalled, and the rail
picks a unit in one click.

| Key | Does |
|---|---|
| Space | pause, resume (as before) |
| **1–5** | the gears: 1×, 10×, 60×, 240× (the fine tier's top), 3,000× (a passage). The bar keeps all thirteen for the mouse |
| **§ held** | fast-forward, back to the clock it found on release. On the fine tier: **the fine tier flat out** (decision 5), the clock as fast as the sim can with the tiers and rails as at 240× (`World.FineHeld`, one `SetSpeed` with the hold, so no ship crosses to coarse and back). On the coarse tier: the passage gear |
| **⇧Space** | run until it matters: the clock to the top and back to the watching speed, or paused, at the first thing the player wants to know (a new contact, an enemy in gun range, a hit on one of ours, a torpedo track sighted, a task done or can't). The encounter rule (`Interest`) grows into this list, each item pause, slow or nothing. No popup: the clock changes and the status bar says why |
| Mouse 4, 5 | a step slower, faster (the hand stays on the mouse) |
| + − | a step, as before |
| **6** (proposed, `coarse.md` J1) | a coarse step: a minute, the coarse tier held at the watching speed (a minute in 3 s, the menu's clock), paused at the next minute (`Command.Step`). A gear or + − lets the hold go; § and ⇧Space keep it |

7–0 are free.

## Steps

| Step | What | State |
|---|---|---|
| **I0** | The camera: WASD (⇧ faster), middle drag and click (centre, follow); the four orders off WASD | **Done 2026-10-26** (`MapScene.Input.cs`; the keys held by their SDL down and up, so `-input=F:hold:D:60` drives them) |
| **T0** | The gears on 1–5, § held fast-forward, mouse 4 and 5 | **Done 2026-10-26** (`MapScene.Clock.cs`: `Gears`, `FastForward`; § is the scancode left of 1, ` on a US keyboard; the side buttons step a paused clock without running it, as + −) |
| **I1** | The left button selects only: the drag a box (units wholly inside as units) | **Done 2026-10-26** (`MapScene.Input.cs`: `BoxSelect`): the selection the guide of the largest unit wholly inside, at its level; the rest picked beside it, so a cut squadron goes as ships alone (a picked ship at a unit's level goes as her unit at it, as a ctrl+click's); ctrl adds; an empty box deselects. Benches pan with `F:pan:…:2` now |
| **I2** | The right button on the release with the order drawn while held; armed verbs aimed by the right button | **Done 2026-10-26** (`MapScene.Input.cs`: `OrderPreview`, a dashed line from the ship that steams the order to what it's about, in the verb's colour, beside the cursor's verb; Esc while held drops it). A left click that changes the selection disarms; every "click …" of a pick now says "right-click …" |
| **T1** | Run until it matters: the interest list in the sim, ⇧Space | **Done 2026-10-26** (`World.RunBack`, set with the speed by one `SetSpeed`; `World.Matters` from the task layer when a plan the player gave by hand ends, the encounter rules dropping to the run's speed or theirs if slower; `MapScene.Clock.cs`: `Run`, the fast clock shared with § by `Fast`; ⇧Space again ends it). The draw clock no longer runs ahead after a drop from a fast clock (`WorldSession.Clock`: its rate capped by the asked speed, a clock far ahead jumps back) |
| **I3** | Right drag shapes: the heading on arrival, a force's deploy line | **Done 2026-10-26** (`MapScene.Input.cs`: `ShapedOrder`, `Shape`): pressed on the sea, a route to the press point and a 2 km leg on along the drag (past its last waypoint a ship holds the course she arrived on), ⇧ adds both to the route; a force in columns deploys on the drag's bearing; drawn while held (to the press point dashed, the drag an arrow) and named (THERE, THEN 056°; DEPLOY 045°). Pressed on a ship, or with a verb armed, a drag is the press's click |

## Decisions (the user, 2026-10-26)

1. **Armed verbs on the right button**, not StarCraft's left-commits right-cancels: consistency. May change after
   playtesting.
2. **Ctrl+click** adds to the selection (not Shift, which queues and pans fast). Playtesting will tell.
3. **Withdraw loses its key.** Keys are for things used often. Later: instrument which elements players use, so the
   playtests show what earns a key.
4. **The number keys stay time**, as gears; the left hand: Space, 1–5, § held, ⇧Space, mouse 4 and 5. The five gears
   are the speeds the user used most in testing.
5. **§ on the fine tier is the fine tier uncapped** (2026-10-26, for trying out; likely to change once the coarse tier
   is whole). Measured: the squadrons' fight ran to its end in under a second; the full Jutland makes about 288×. It may
   need tuning after all, perhaps as a setting (the user, 2026-10-26).

## Questions, answered (the user, 2026-10-26: as proposed, to iterate by playtesting)

1. **T1, which task endings drop the clock**: only tasks the player gave by hand, or any of the player's units' (the
   side's commander gives and ends tasks often)? Proposed: by hand only.
2. **T1, the run's speed**: the same as § (the fine tier flat out from the fine tier, 3,000× from the coarse), so § and
   ⇧Space differ only in held against latched and one setting tunes both? Proposed: yes.
3. **T1, where it drops to**: back to the speed it found (paused if an encounter rule pauses); the encounter rules as
   they are. Proposed: yes, no new rows in the clock panel.
4. **I3, a right drag on the sea**: steer for the press point, then hold the drag's heading on arrival (a waypoint, then
   the course)? Proposed: yes.
5. **I3, a force in columns**: a right drag deploys along it (the line's course), Deploy… (N) stays for the enemy's
   course? Proposed: yes.
6. **The 3D views** (the ship viewer, the hitbox view) orbit with a left drag today. Proposed: the middle button stays
   the camera's there too (its drag orbits, ⇧ with it pans), WASD moves, the wheel zooms, the left selects (a prism).
