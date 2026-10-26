# Input: the map's mouse, camera and clock keys (agreed 2026-10-26)

The battle map's controls reworked to the usual RTS grammar: **the left button selects, the right button acts**, the
camera on WASD and the middle button, the clock on a few keys the left hand finds blind. Builds on the research's input
map (`docs/battle/command-ui.md`, Default input map) and the strip's key table (`strip.md`, The keys), which this
supersedes where they differ.

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

6–0 are free.

## Steps

| Step | What | State |
|---|---|---|
| **I0** | The camera: WASD (⇧ faster), middle drag and click (centre, follow); the four orders off WASD | **Done 2026-10-26** (`MapScene.Input.cs`; the keys held by their SDL down and up, so `-input=F:hold:D:60` drives them) |
| **T0** | The gears on 1–5, § held fast-forward, mouse 4 and 5 | **Done 2026-10-26** (`MapScene.Clock.cs`: `Gears`, `FastForward`; § is the scancode left of 1, ` on a US keyboard; the side buttons step a paused clock without running it, as + −) |
| **I1** | The left button selects only: the drag a box (units wholly inside as units) | |
| **I2** | The right button on the release with the order drawn while held; armed verbs aimed by the right button | |
| **T1** | Run until it matters: the interest list in the sim, ⇧Space | |
| **I3** | Right drag shapes: the heading on arrival, a force's deploy line | |

## Decisions (the user, 2026-10-26)

1. **Armed verbs on the right button**, not StarCraft's left-commits right-cancels: consistency. May change after
   playtesting.
2. **Ctrl+click** adds to the selection (not Shift, which queues and pans fast). Playtesting will tell.
3. **Withdraw loses its key.** Keys are for things used often. Later: instrument which elements players use, so the
   playtests show what earns a key.
4. **The number keys stay time**, as gears; the left hand: Space, 1–5, § held, ⇧Space, mouse 4 and 5. The five gears
   are the speeds the user used most in testing.
5. **§ on the fine tier is the fine tier uncapped** (2026-10-26, for trying out; likely to change once the coarse tier
   is whole). Measured: the squadrons' fight ran to its end in under a second; the full Jutland makes about 288×.
