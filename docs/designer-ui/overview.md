# Overview tab (mockup, 2026-10-11)

Mockup: `overview.html`. Open it in a browser; it is interactive and works offline. Like the main battery and secondary
mockups it shows **only the tab's pane** (1280 × 578, its size at 1920 × 1080). URL options: `?ship=bismarck` (the
default, against Washington 1922), `dreadnought` (against a 600 × 90 ft dock) or `heavy_cruiser` (against London 1930).

**What's real and what isn't:**

- **Real:** every "one step" figure on the opened ship, and the speed curve. Each is a `shipgen design` build of the
  shipped design with that one lever moved one step (or the speed set to each whole knot from 16 to 35), done on
  2026-10-11. That's 36 probe builds per ship.
- **Stand-in:** once you edit, the page adds up each lever's probed change on its own and extrapolates past one step.
  The game builds the real ship instead, so in the mockup the Fit table's targets are only roughly right.

## What the tab is for

You've built a ship and need it to come in under a limit: a treaty, a dock, or a tonnage you've set yourself. You
want to get there in a few clicks, by giving up a knot or 10 mm of belt, without opening the detail tabs. The Overview
is that trim sheet. It has three parts.

### 1. Trim sheet (left)

There are 18 levers in four groups, each group headed by its weight from the ledger. Click a heading to open its tab.

| Group | Levers (step) |
|---|---|
| Speed & machinery | speed (0.5 kn), range (500 nm), machinery rating (0.1) |
| Protection | belt (10 mm), upper belt (10), armour deck (5), citadel bulkheads (10), end belts, both (10), turret faces (10), **all armour, scale** (5 %) |
| Armament | main calibre (10 mm), main rounds per gun (10), secondaries per side (1), light AA mounts (2) |
| Hull & crew | freeboard (0.05 ×), hull form Cb (0.01), bridge tower (1 level), stores (5 days) |

Levers marked `new` aren't on today's trim sheet, which has 7. All of them are broad strokes: one number moves a
whole system. Placement, extents and materials stay on the tabs.

Each row has these columns:

- **◇ / ◆ lock:** a locked lever is left out of every Fit.
- **The value spinner:** wheel to step, Shift for ×4, click to type.
- **The step.**
- **One step − and one step +:** what that step does to the standard displacement, from a real probe build. Click a
  cell to take that step. Hover it for the whole probe result: standard, full load, length, beam, draught and GM.
- **What the lever drives.**
- **↺:** puts that lever back to its value as opened. It only appears on a lever you've changed, and the changed row
  is tinted.

**Lumpy steps are shown, not smoothed.** The engine's tonnage isn't smooth in the levers. When a step goes the
"wrong" way (a step down adds weight, or a step up saves it), the cell turns amber with ⚠. Its tooltip says what
happened, for example "the hull settles on another form: beam −0.08 m, draught +0.37 m". See "Engine finding" below.

The probes run on a background worker after each settled edit. The header shows `probing 16/36` with a bar, and the
rows fill in from the top. Cells still waiting show `…`.

### 2. Limits (top right)

Six rows: standard, full load, length, beam and draught (each ≤), and GM (≥). Each row has:

- a tick box
- a type-in limit
- the value now
- the margin: amber "over" or green "spare", never red
- a bar with a tick at the limit.

The **preset** menu fills the rows:

- Washington 1922 (35,560 t, which is 35,000 long tons)
- London 1930 cruiser (10,160 t)
- Panama locks
- a 600 × 90 ft dock
- "Stable" (GM ≥ 1.5 m)

Editing a row by hand turns the preset back to custom.

Under the rows:

- **The verdict:** the worst limit, for example "4,203 t standard over Washington 1922 · and beam". When every limit
  is met it turns green: "432 t to spare".
- **A weight bar:** the standard displacement by ledger group, with the standard limit as a white tick, so you can see
  what the tonnage is made of.

### 3. Fit (bottom right)

- **Fit with one lever:** for each unlocked lever, the value it alone would need to meet every limit, with the rest
  held. The table is sorted by least change, as a share of the lever's value. An **also** column gives the knock-on
  effects (length and GM), and each row has a **Fit** button. Levers that can't do it alone are listed on one line;
  hover it for how far each one gets. Cb is skipped, because the hull form isn't monotone in tonnage.
- **Fit evenly:** cuts every unlocked lever by the same share of its room above its floor, rounded to its step, and
  stops at the smallest share that fits. Its tooltip lists each change before you click. Lock (◆) whatever must not
  move.
- After a Fit, a line reads `last: speed 30.0 kn → 26.0 kn · 6 builds`. Ctrl+Z takes it back in one step.

The right-click menu on a row has: lock or unlock, reset to the opened value, "Fit with this lever: 26.0 kn", and
open its tab.

## Engine finding: the tonnage is lumpy

The probes on `bismarck.json` (standard 39,763 t):

| Step | Standard | Why |
|---|---|---|
| armour deck 50 → 45 mm | **+1,281 t** | draught +0.37 m, beam −0.08 m, GM −0.36 m |
| armour deck 50 → 55 mm | +1,901 t | the same jump |
| range 9,000 → 8,500 nm | **+1,528 t** | draught +0.33 m |
| machinery rating 0.4 → 0.3 | +2,581 t | expected: a heavier plant; but GM −0.58 m |
| bridge tower 6 → 7 levels | +1,760 t | draught +0.4 m |
| belt 320 → 310 mm | −129 t | smooth |

Bismarck seems to sit on an edge in the hull sizing. Almost any change flips it onto a deeper, narrower solution
that's about 1,300 to 2,600 t heavier. Dreadnought shows the same thing in its freeboard (+0.05 saves 52 t but costs
0.62 m of GM), armour deck (+5 mm saves 232 t) and Cb. The heavy cruiser is mostly smooth, but 29 → 30 kn *saves*
900 t.

The overview shows this honestly (the ⚠ cells), but it makes "what does one step save" noisier than it should be.
**It's worth a separate look in the hull sizing (beam and draught solve) before or alongside the port.** It isn't
part of this tab.

## What maps onto today's engine

| Lever | Knob | Fields |
|---|---|---|
| speed, range | `Speed`, `Range` | `speed_kn`, `range_nm` |
| machinery rating | `Stress` | `machinery.stress` |
| belt, upper belt, bulkheads | `Belt`, `UpperBelt`, `Bulkheads` | `armour.belt_mm`, `armour.upper_belt.mm`, `armour.bulkhead_mm` |
| armour deck | `ArmourDeck` | the first citadel or full-length deck (`MainDeckIndex`) |
| turret faces, rounds, main calibre | `MainArmour(i)`, `MainRounds(i)`, `MainCalibre(i)` | stepped on every main battery together (the trim sheet is broad strokes) |
| secondaries per side | `SecPerSide(0)` | the first battery |
| light AA | `AaLight` | `aa.light` |
| freeboard, Cb, tower, stores | `Freeboard`, `BlockCoefficient`, `TowerLevels`, `Endurance` | |
| **end belts, both** | new knob | steps `end_belts.fore.mm` and `.aft.mm` (and their tips) by the same mm; shows the mean |
| **all armour, scale** | new composite knob | multiplies every plate thickness (belt, strakes, end belts, bulkheads, decks, turret faces, barbettes) by the step ratio. Its value is a percentage of the armour as the tab last saw it, so it reads 100 % again after any other armour edit, and there's no drift from rounding twice |

## Porting to ImGui

- **`DesignerScene.Overview.cs`**, a new partial like `DesignerScene.Secondary.cs`. Section 0 gets its own body in
  place of `Flow` and `Standard`.
- **The trim table:** an `ImGui.BeginTable` with the shared compact spinners, `Knobs.TrimSheet` grown to the 18
  levers, a group header row per group, and a lock set in the scene (UI state, not saved).
- **Probe worker:** a second `DesignWorker` with build only (no mesh, draw or bake) and `limits: true`.
  - After the main build settles, it queues ±1 step for every lever on the settled design. A newer design cancels the
    queue.
  - It runs on N−1 threads: about 36 × 0.3 s, so a few seconds on 4 cores.
  - Results go into an array of `(std, full, L, B, T, GM)` per lever and direction. The snapshot is rebuilt when a
    batch lands, not every frame, and the strings are cached as elsewhere.
  - It only runs while the Overview tab is open.
- **Fit with one lever:** the table's targets come from the probes by linear extrapolation, shown as `≈`. The Fit
  button runs a real bisection on the probe worker (bracketed by the probe, usually 4 to 6 builds), then applies the
  result as one undo step, `Fit: Speed 26.0 kn`.
- **Fit evenly:** the same, with one share for all unlocked levers, bisected on the share.
- **Limits:** six optional numbers plus the preset name, stored with the design (`limits: {standard_t, full_t,
  length_m, beam_m, draught_m, gm_min_m}`). Shipgen ignores the field; `DesignDoc` round-trips it. Presets are a small
  table in the designer, and campaign treaties could fill it later.
  - When a limit is exceeded: an amber remark ("4,203 t standard over Washington 1922"), and the resource bar's figure
    turns amber. The design still saves and builds.
- **Weight bar:** from `Report.WeightGroupsT` without fuel, with the ledger's colours.
- **Dev hook:** `-designer-ui=ovtip:deck:-1` (the delta tooltip), `ovmenu:belt` (row menu), `ovpresets` (preset menu)
  and `ovlock:speed`. Verify the spinners, Δ cells, Fit buttons and limit fields with `-input=` clicks, as on the
  other tabs.
- No "since opened" deltas, as elsewhere. The one-step columns are forecasts, not history (see the open questions).

## Open questions

1. **The one-step columns:** this UI has a "no deltas" rule. These columns aren't "since opened"; they're what the
   next click will do. Are they acceptable, or should they go into a tooltip only?
2. **Probe cost:** 36 background builds after each edit, while the Overview is open. Is that acceptable, should it be
   fewer (visible rows only), or should it be on demand (a "probe" button)?
3. **Limits:** saved with the design (as mocked), or kept as a per-player setting that applies to every design?
4. **The Fit table:** probe estimates (`≈`, free) with the real bisection on click, as mocked? Or real targets for
   every row, built lazily after the probes (about 90 more builds)?
5. **The lever list:** anything to add or drop? Candidates left out: secondary calibre, main turret count (that's
   arrangement, not broad strokes), crew standard, TDS depth.
6. **The engine finding:** look at the hull sizing's lumpiness first, so the one-step figures are smooth, or port
   first and show it as mocked?
