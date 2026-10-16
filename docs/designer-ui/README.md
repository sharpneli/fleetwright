# Ship designer UI mockup (2026-10-09)

This is the visual reference for the in-game ship designer. The reasoning is in `designer-ui-research.md` (also in the cloud project as claude/designer-ui-research.md). The current proposal is §15.6–15.7, roles and matchups are §14, and the engine asks are §12 and §14.4.

**It is a mockup, not code to port.** Take the layout, hierarchy, colours and interactions, not the HTML.

## Open it

Open `follow.html` in a browser; it works offline. Click around, because the transitions are the point. It needs `dc-lite.js`, a tiny template runtime that is not part of the design, and `assets/`.

## The design: one screen

The screen has four fixed parts:

- **Left rail:** sections 0–8. Pick one by clicking it, pressing its number key, or clicking a part of the ship, a Legend line or a remark.
- **Centre:** the ship sprite is pinned at the top and never moves. The space under it rearranges itself for the section.
- **Right:** the Legend. It holds the particulars, the weight bar with the request line, a mini "catches · escapes · beats" chart and the remarks.
- **Top bar:** sketches A/B, Compare, the request badge (amber/green, never red) and the classified role.

### Sections

**0 · Overview** is the default and opens first. It is a trim sheet for last-minute weight tuning:

- a medium side profile with the armour thicknesses on the plates
- a lever row each for speed, range, belt, armour deck, turret faces, rounds per gun and secondaries per side. Each row shows what one step saves.
- standard displacement vs the request, with the margin
- full load, length and GM
- role scores with trend arrows
- Reset and "Fit with speed".

Everything reacts live.

**1–8** are for detail work. Picking one **animates its widget into the space under the ship**:

- **Protection:** the side profile grows into the armour editor.
- **Hull:** the profile grows half-way.
- **Role:** the catches · escapes · beats chart flies out of the Legend into the centre.
- **Main battery, Speed, Fire control, Crew:** the widget grows out of the dock row at the bottom while the previous one shrinks back.

Each widget is drawn at its big size and moved with a translate + scale transform between a docked rect and a focused rect (0.55 s ease-out). The section's panel fades in after the widget lands. Only one thing grows at a time, everything returns to where it came from, and the ship and the Legend numbers never move.

## Variant: game-UI look (`bridge.html`)

`bridge.html` is the same design as `follow.html`: the same layout, sections, Overview trim sheet and follow-the-focus transitions. Only the vibe changes, from clean dashboard to naval game:

- **Centre:** the ship lies on a **blueprint sheet** with a grid, length and beam dimension lines, and a drawing number. The side profile and the editors are drawn in chalk-white blueprint lines, and the armour plates are hatched.
- **Left rail:** the sections are a **signal hoist** of simplified International Code numeral pennants (0–8). The open section's flag runs out, and its values are on embossed label tape.
- **Panels:** **riveted steel** with brass controls.
- **Overview:**
  - an **engine-order telegraph** for speed
  - **brass slide levers** for range, belt, deck, turrets, rounds and secondaries, with the opened value marked and a paper tag showing what one step saves
  - a **displacement gauge** with an amber arc past the request
  - an odometer readout and a signal lamp (amber when over, green when within).
- **Legend:** a typed **dockyard report on a clipboard**, with a rubber stamp ("970 T OVER" or "WITHIN LIMIT").
- **Catches · escapes · beats:** a **plotting sheet** pinned to the clipboard. Picking Role takes it to the table.
- **Other touches:** the top bar is a brass nameplate with coin-style sketch tokens; the history bar is a typed log; and "Accept" becomes "LAY DOWN".

Fonts (OFL, bundled):
- Saira Stencil One (stencil headings)
- Barlow Condensed (UI)
- Share Tech Mono (readouts)
- Special Elite (typewriter).

| File | State |
|---|---|
| `png/bridge-overview.png` | Overview as opened: amber lamp, "970 T OVER" stamp |
| `png/bridge-overview-trimmed.png` | after −0.5 kn, −0.5 in belt, −20 rounds: needles moved, green lamp, "WITHIN LIMIT" |
| `png/bridge-protection-mid.png`, `png/bridge-protection.png` | the profile growing into the armour editor |
| `png/bridge-role-mid.png`, `png/bridge-role.png` | the plotting sheet lifted off the clipboard onto the table |
| `png/bridge-main.png` | the turret arrangement grown from the dock |
| `png/bridge-speed.png` | the speed curve |

## Variant: dense PC strategy UI (`bureau.html`)

This is the Aurora / HoI4 / RTW tier. Nothing animates and nothing hides: everything is on one screen at 12 px text, and the interaction is tables, spinners, tabs, hotkeys and tooltips.

- **Menu bar** and **resource bar**: standard, full load, cost, build time, speed, range, GM and crew, each with its Δ since the design was opened. Hovering one gives a breakdown tooltip in CK3 style, where underlined terms open their own tooltips.
- **Left:**
  - a **design outliner** tree: hull, armament, protection, machinery and so on, down to plates and batteries, with tonnes and a % bar on every node. Clicking a node opens its tab.
  - **Technology in this design**: each template with its year, plus markers for newer tech ("oil ’08", "triple ’10") and [U] to modernise.
- **Centre top:** the plan and armour profile, with hotkeyed overlay toggles ([A]rcs, a[R]mour, [M]ags, [S]moke, crew [Q], [H]orizon, [T]ags) and callout tags.
- **Centre:** a **tabbed detail pane** (F2–F9) of dense tables:
  - **Overview:** the trim table. Each row is a ◂ ▸ spinner with its step, Δ t per step, Δ since open, Δ vs your in-service class (Lord Nelson) and vs the enemy (République), and what the lever drives. Below it: **"cheapest ways to save 1,000 t"** ranked by the loss in role score, the margin meter, and Goal-seek / Revert (G / R).
  - **Protection:** a plate table (face, lower edge, extent, area, t, t per inch, material) plus a **penetration-vs-range grid** that marks the immune zone.
  - **Role:** the full matchup table (catch / escape / beat / survive cells, verdict, intel confidence) plus fitness by role with the reason.
- **Right:**
  - the **weight ledger** (group, t, %, bar, Δ)
  - **performance** with HoI4-style pip meters
  - the **matchup pip grid** (C·E·B·S squares per enemy class)
  - **remarks** with numbered fix hotkeys.
- **Status bar:** engine state (build time, probes), history breadcrumbs, and hotkey hints.

Fonts: Archivo Narrow (UI) and IBM Plex Mono (numbers), bundled. The penetration and cost figures are illustrative.

| File | State |
|---|---|
| `png/bureau-overview.png` | Overview as opened: 970 t over the request |
| `png/bureau-trimmed-tooltip.png` | after −0.5 kn, −0.5 in belt, −20 rounds, with the hover breakdown tooltip on Standard |
| `png/bureau-protection.png` | Protection tab: plate table and penetration grid |
| `png/bureau-role.png` | Role tab: matchup table and fitness by role |

## Main battery picker (`mainbattery.html`, 2026-10-11)

This mockup covers the Main battery tab's pane only (1280 × 578). It replaces the per-battery knob lists with three
parts:

- a **battery table** with one row per battery
- a clickable **arrangement schematic**: `+` slots add a turret, and the ▲ between two turrets toggles superfiring
- a **guns-bearing rose**.

Below them are **arrangement presets** and a turret inspector.

The reasoning, click counts, engine rules and port plan are in `mainbattery.md`. Open the page in a browser to try
it. `?ship=invincible` loads the echelon example.

| File | State |
|---|---|
| `png/mainbattery-opened.png` | Lord Nelson as opened: three batteries, A active |
| `png/mainbattery-ghost-tip.png` | hovering the aft `+`: "+ X, superfiring" |
| `png/mainbattery-add-x.png` | one click later: X superfires over Y, astern 2 → 4 guns; X selected |
| `png/mainbattery-menu.png` | right-click on Y: change battery, split off, move, remove |
| `png/mainbattery-echelon.png` | Invincible: echelon + cross-deck in the wing cluster, 8-gun broadside |

## Secondary · AA · TT tab (`secondary.html`, 2026-10-11; in the game since the same day)

This mockup covers the tab's pane only (1280 × 578). It has four parts:

- a **secondary battery table** with a one-click position control (deckhouse · deck | upper · lower casemate) and a
  placed `n/m` column
- a **broadside strip**: a half-plan over four tracks, one per position, with amber ghosts for the pairs that don't
  fit and `+` at the head of each track
- a **guns-bearing rose** for the secondaries
- **AA**, **torpedo** and **selected-battery** panels.

The reasoning, engine mapping, engine asks and port plan are in `secondary.md`. `?ship=kongo|babel|deutschland`
loads the other examples.

| File | State |
|---|---|
| `png/secondary-before-babel.png` | today's tab on Babel, for comparison |
| `png/secondary-opened.png` | Bismarck as opened: two deckhouse batteries, light AA |
| `png/secondary-short.png` | SB at 10 per side: 8/10 placed, ghosts on the track; 12 heavy AA push the light AA to the deck edge |
| `png/secondary-babel-menu.png` | Babel's five batteries; right-click on a lower casemate pair |
| `png/secondary-kongo-lane.png` | Kongō: casemates clear of Q's barbette; hovering the upper-casemate `+` |
| `png/secondary-deutschland.png` | Deutschland: torpedo pair placed, rose with the main battery, S2 selected |
| `png/secondary-game-babel-menu.png` | in the game: Babel, right-click on pair SB2 |
| `png/secondary-game-kongo-lane.png` | in the game: Kongō, hovering the upper-casemate `+` with SB selected |
| `png/secondary-game-deutschland.png` | in the game: Deutschland, S2 selected, rose with the main battery |

## Overview tab (`overview.html`, 2026-10-11; in the game since the same day)

This mockup covers the Overview tab's pane only (1280 × 578). It is a trim sheet for getting a ship under a limit with
broad strokes:

- **18 levers** in four groups. Each lever has real one-step − / + tonnage from probe builds, with ⚠ on steps where the
  engine's tonnage jumps.
- **Limits:** standard, full load, length, beam, draught and GM, with treaty and dock presets, a verdict line and a
  weight bar.
- **Fit:** one lever alone, or every unlocked lever evenly.

The reasoning, the engine finding (the lumpy tonnage), the port plan and the open questions are in `overview.md`.
`?ship=dreadnought|heavy_cruiser` loads the other examples.

| File | State |
|---|---|
| `png/ov-before.png` | today's Overview in the game, for comparison |
| `png/overview-opened.png` | Bismarck against Washington 1922: 4,203 t over |
| `png/overview-delta-tip.png` | hovering armour deck "one step −": +1,281 t, the hull settles deeper |
| `png/overview-fit-speed.png` | after Fit with speed: 30 → 26 kn, 432 t to spare |
| `png/overview-probing.png` | right after an edit: probes filling in |
| `png/overview-row-menu.png` | right-click on Belt |
| `png/overview-preset-menu.png` | the limit presets |
| `png/overview-dreadnought-dock.png` | Dreadnought against a 600 × 90 ft dock: length and beam over |
| `png/overview-cruiser-london.png` | Heavy Cruiser against London 1930, with speed and calibre locked |
| `png/overview-game-bismarck.png` | in the game: Bismarck against Washington 1922, the armour deck's ⚠ tooltip |
| `png/overview-game-dreadnought.png` | in the game: Dreadnought within Slipway No. 1 |
| `png/overview-game-cruiser.png` | in the game: Heavy Cruiser against London 1930 |

## Protection tab (`protection.html`, 2026-10-11; in the game since the same day)

This mockup covers the Protection tab's pane only (1280 × 578). It replaces the knob list with four parts:

- an **elevation**: the ship's side deck by deck, every plate where it is, `+` ghosts where a missing part would go
- a **midship section** to scale, with the belt's edges and the torpedo protection's bulkhead as drag handles
- a **plate table** of the parts the ship has: thickness, second value, where (deck, extent), material, weight and its
  share; `+` chips in the group headers add a missing part, `×` removes one
- the **selected plate**: what it covers and weighs, and the greyed immune-zone panel.

Coverage presets (All-or-nothing, Armoured ends, Complete belt) sit in the sub-bar. The reasoning, the review decisions,
the port plan and the open questions are in `protection.md`. `?ship=dreadnought|yamato` loads the other examples.

| File | State |
|---|---|
| `png/protection-before.png` | today's Protection tab in the game, for comparison |
| `png/protection-opened.png` | Bismarck as opened |
| `png/protection-ghost.png` | hovering the second deck's `+` over the citadel |
| `png/protection-added-deck.png` | after adding it and thinning the main deck 10 mm |
| `png/protection-dreadnought-menu.png` | right-click on Dreadnought's third deck, fore end |
| `png/protection-cover-tip.png` | Yamato, hovering the "Armoured ends" preset |
| `png/protection-dreadnought-aon.png` | Dreadnought after "All-or-nothing" |
| `png/protection-handle-reach.png` | Bismarck, parts removed, dragging the fore end belt's tip |
| `png/protection-game-bismarck.png` | in the game: Bismarck, hovering the second deck's `+` |
| `png/protection-game-dreadnought-menu.png` | in the game: right-click on Dreadnought's third deck, fore end |
| `png/protection-game-yamato-cover.png` | in the game: Yamato, hovering "Armoured ends" |

## Protection tab: the layers (`protection-layers.html`, 2026-10-16, mockup for review)

H8b of `docs/shipgen/protection-plan.md`: the tab learns slopes, inclined and internal belts, zones, belt extent, the
inner bulkhead, magazine boxes and wings, TDS layers, platform decks, the layout templates and the compartment check.
The midship section becomes two half-sections, each cut at its own station (machinery and a magazine by default, flags
`1` `2` on the elevation), with the cells' contents tinted. The table gains sub-rows (zones, slopes) and an Inboard
group; the selected plate gains a Shape column; the sub-bar gains Layout. Real builds of seven designs
(`protection-layers-data.js`); `?ship=bismarck|dreadnought|nelson|kent|dunkerque|kgv|destroyer`. The reasoning, the
findings, the review questions and the port plan are in `protection-layers.md`.

| File | State |
|---|---|
| `png/protection-layers-bismarck.png` | Bismarck as opened: machinery and magazine A |
| `png/protection-layers-dreadnought.png` | the magazine screens on the wing walls, coal in the machinery cut |
| `png/protection-layers-nelson-comp.png` | internal belt and lower belt; Compartments on, the warned run hatched |
| `png/protection-layers-kent.png` | boxes and the machinery belt |
| `png/protection-layers-dunkerque.png` | lean, sloped lower deck, floor plate |
| `png/protection-layers-kgv.png` | zones, with the 2 m slivers at the secondary magazines (finding 1) |
| `png/protection-layers-bismarck-layers.png` | three TDS layers, cut at X and A |
| `png/protection-layers-destroyer-box.png` | a destroyer given a 25 mm box and 1.6 m wings |
| `png/protection-layers-layout-menu.png` | the Layout list, hovering German bulkhead |
| `png/protection-layers-dread-german.png` | Dreadnought after German bulkhead |

## Screenshots (`png/`)

| File | State |
|---|---|
| `follow-overview.png` | 0 · Overview as opened: 970 t over the request |
| `follow-overview-trimmed.png` | after −0.5 kn, −0.5 in belt, −20 rounds: 147 t under, Line of battle 0.84 → 0.81 ▼, badge green |
| `follow-protection-mid-transition.png` | 250 ms after picking Protection: the profile growing |
| `follow-protection.png` | Protection: armour editor with the thicknesses on the plates |
| `follow-back-to-overview-mid.png` | mid-transition back to Overview |
| `follow-role-mid-transition.png` | 250 ms after picking Role: the chart in flight from the Legend |
| `follow-role.png` | Role: big chart and fitness panel, with an empty slot in the Legend |
| `follow-main.png` | Main battery: arrangement strip grown from the dock |
| `follow-machinery.png` | Speed: the speed→displacement curve |
| `follow-hull.png` | Hull: profile half-grown with the forecastle and tower labels |

## Which numbers are real

**Real shipgen output:**

- the Dreadnought sprite and base particulars
- the speed curve (17–25 kn builds of `dreadnought.json`)
- the 20.47 kn goal-seek result.

**Illustrative:**

- the other tonnage-per-step figures in the trim sheet
- the enemy classes and positions in the matchup chart
- the role scores.

## Roles in one line

The class is an output. The player's assigned role is an order for their own fleet AI, while enemy AI and intel use only the assessed class. Gameplay limits are amber consequences, never red refusals.

## Design tokens

| Role | Value |
|---|---|
| Ground | `#141D24` |
| Panels | `#1B2730`, `#22313B`; inset `#15242E` |
| Lines | `#304350` |
| Text | `#E3E9EC`, muted `#93A6B0` |
| Accent / selection | brass `#E0AE4F` |
| Main battery | blue `#5B9BE0` |
| OK | `#9FDDB7` on `#1C3329` |
| Consequence (never red) | `#F1D79C` on `#2E2717` |
| Legend paper | `#EFE7D6`, ink `#26221C`, amber ink `#8A5A10` |
| Armour ramp (thin → thick) | `#F3E3A1` `#E9B66B` `#DE9455` `#CC6F43` `#B24E36` `#8E3328` |

Fonts (OFL, bundled in `assets/fonts`):

- UI: IBM Plex Sans Condensed
- Figures: IBM Plex Mono
- Legend: Libre Caslon Text

The mockup is fixed at 1600×960.
