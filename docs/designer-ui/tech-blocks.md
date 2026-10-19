# Designer UI pass: the design blocks with no control (plan, 2026-10-19)

The damage, fire-control, fire and looks steps (formats 4-9) added design data and left their designer rows for one UI
pass (memory: UI pass after the damage plan). This is that pass's plan: what a design can now say that the designer
can't set, and where each goes. Tech blocks are picked from templates in a drop-down (the user, 2026-10-19); their
numbers aren't edited here.

## What has no control today

Surveyed over all 125 shipped and scenario designs (`shipgen/designs`, `Content/Scenarios/Designs`).

| Block | In the design | Catalogue | The shipped designs |
|---|---|---|---|
| **Gun block** | `main[i].gun`, `secondary[i].gun`; `gun_tech` | `guns.json`: 11 (5 periods × propellant) | 268 batteries, all a catalogue block; main and secondary always the same |
| **Torpedo block** | `torpedoes.torpedo`; `torpedo_tech` | `torpedoes.json`: 5 periods | 57, all catalogue |
| **Fire-control fit** | `fire_control.{main,secondary,aa}.fit` (optics, computer, mount), `.sights`; `fire_control_tech` | `fire-control.json`: 11 optics, 18 computers, 10 mounts, 13 presets | 139 of 375 battery slots have a fit, the rest the default preset; every block catalogue |
| **Shock grade** | `fittings.shock_grade` | `fittings.json`: G0-G3 | G0 114, G3 9, G1 1, G2 1 |
| **Outfit** (fire loads) | `outfit` | `outfits.json`: 5 | 125, all catalogue |
| Turret rangefinder | `main[i].rangefinder_m` | - | armoured gunhouses only |
| Aft and CT rangefinders | `fire_control.aft_rangefinder_m`, `.conning_tower_rangefinder_m` | - | |
| Masts | `masts[]`: kind, top, height, director, tiers, top radius | - | 113 give them, 12 default |
| Hull joints | `hull.construction.shell`, `.internals` (riveted, welded) | inside the hull templates | set by the Construction pick |
| Doors below | `subdivision.doors_below` | - | none use it |

Not designer data, so not in this pass: `ammunition`, `explosives` and `materials` (tables the pickers copy in),
readiness and firefighting doctrine (orders and the scenario's), the look (decorative, the View menu).

No shipped design carries its own values under a catalogue name or a name the catalogue lacks, so "own values" is a
case to keep (saved and hand-edited designs), not one to design around.

## One picker for every tech block

Every block above that has a catalogue works the same way, as the shells already do (`ShellEdits.With`):

- **A pick** sets the name where the design names it, copies the catalogue's block into the design's table
  (`gun_tech`, `torpedo_tech`, `fire_control_tech`), and drops blocks nothing names any more. The design stays whole
  on its own, and an unused block never lingers. `fittings` and `outfit` are single blocks, so the pick replaces them
  (the outfit keeps its `name` label).
- **What shows as picked:** the catalogue entry of the same name whose values match; else `NAME · own values` (its
  name, changed numbers) or `custom: NAME` (a name the catalogue lacks), listed after the catalogue so opening such a
  design never loses it. Nothing given shows `default: NAME` (`GunTemplates.Default`, `TorpedoTemplates.Default`, the
  default preset, G0).
- **The list:** the catalogue in its own order, under its groups (gun and torpedo periods, fire control's optics,
  computers and mounts). The hovered entry's note is the tooltip, with the numbers it gives *this* battery: a gun
  block's reload, training, elevation and pattern at the battery's calibre (`DesignerPlaceholders.Gun`, `Pattern`,
  the one implementation, called, not copied); a torpedo's speeds and ranges at the tubes' calibre; a preset's three
  blocks. No filter by the look's navy or era (memory: looks are decorative only); the tech tree filters later
  (`Templates`' note).
- **Code:** `Templates` loads the five catalogues as it does the others (fire control's `blocks` and `presets` both).
  A `TechEdits` (beside `ShellEdits`, tested the same way) holds set-copy-prune per kind; `PickRow` gains groups and
  tooltips, built in the snapshot, so a frame with no change allocates nothing.

## Where each goes

| Control | Place | Why there |
|---|---|---|
| Guns: every battery's block | Templates panel, a row under Shells | a design's guns share a period: one pick re-arms the ship |
| A battery's gun block | Main battery and Secondary tables: a GUN column beside SHELLS | it's per battery; the column shows the short form ("Great War · DB"), the drop-down the full names |
| Turret rangefinder | Main battery table, after FACES (armoured gunhouses only; greyed otherwise) | per battery, the turret's own part |
| Torpedo block | Secondary tab, the TORPEDO TUBES panel's first row; also a Templates row | its speeds and ranges go in the panel's header beside "533 mm" |
| Fire-control fit | Fire control tab, per battery: Preset ▾, then Optics ▾ Computer ▾ Mount ▾ (each from its group), Sights ▾ (mounts without director firing) | a preset fills the three; changing one shows "own fit". With no directors only Sights applies |
| Aft and CT rangefinders | Fire control tab, under the main battery | control positions for the main battery |
| Shock grade | Templates panel | one design-wide pick, no weight |
| Outfit | Templates panel; Crew tab shows its loads (fitted / stripped) read-only | what burns lives where the crew does |
| Masts | Hull & upperworks tab: one row a mast, Kind ▾ Top ▾ Height (Auto) Director ▾ | they are upperworks; F4's tops and eyes follow |
| Hull joints | Hull & upperworks details: Shell ▾ Internals ▾ (riveted, welded) | the Construction pick sets them; this overrides |
| Doors below | Protection tab's sub-bar, a toggle | subdivision |

The Templates panel grows from six rows to ten (Guns, Torpedoes, Shock grade, Outfit); the outliner above it
scrolls, so it fits.

**One word, one meaning:** "outfit" is the design's fire-load block, and also what the shells' code and picker call a
battery's shells and shares (`ShellEdits.Outfit`, `BatteryInput.Shells`' summary, the shells tooltip). With both on
one screen, T0 renames the shells' sense to "load" (the shells picker's own word already). The row says "Shock grade",
not "Fittings", which the drawing uses for its dressing. A fire-control row there ("every battery's preset") is left out: main and secondary fits rarely match.

## Order (one tab a session, mockup then port)

1. **T0, the picker and the Templates panel.** `TechEdits`, the catalogues, the grouped picker with tooltips; the
   four Templates rows; the "outfit" rename. Mockup: `tech-blocks.html`, the drop-down open and the panel. Tests: every pick validates and
   builds over the shipped designs, re-picking the current entry changes nothing, unnamed blocks go.
2. **T1, the battery tabs.** The GUN column in both tables, the turret rangefinder, the torpedo row and header.
3. **T2, the Fire control tab's fits.** The rows above in the generic flow (see question 1).
4. **T3, Hull & upperworks and Crew.** Masts, joints, the outfit's loads; doors below on Protection.

Each step verified with real clicks (`-input`) and `-ui` screenshots, committed on its own.

## The user's decisions (2026-10-19)

1. **Plain rows are fine for now**: the fits only need configuring for testing; the Fire control tab's layout stays the
   fire-control plan's last step.
2. **A single gun block**: one Guns picker names the same block on every battery and leaves only it in `gun_tech`, so
   a design saved from the designer carries one block. The format keeps a name per battery (the sim reads it); no GUN
   column. Hand-edited designs that differ show "mixed".
3. **Templates only**: a design's own values are kept and shown ("own values", "custom"), never edited here.

## Done

- **T0-T2 (2026-10-19).** `TechEdits` (set, copy in, prune; `Picked` for what a picker shows), tested by
  `TechEditsTests` over every shipped design. `PickRow` gained groups, tooltips and Applies; one `PickList` draws the
  Templates panel's popups and the tabs' combos (tall). The Templates panel's Guns, Torpedoes, Shock and Outfit rows
  (`DesignerScene.Tech.cs`): a gun block's tooltip gives each of the design's calibres its reload, training,
  elevation, pattern and propellant by `DesignerPlaceholders`; a torpedo's its 21" settings, warhead, gyro and
  failures. The Fire control tab per battery: directors, rangefinder, Preset, Optics, Computer, Mount (with
  directors), Sights (with the battery); the turret rangefinders (`Knobs.MainRangefinder`), aft control's and the
  conning tower's. The Crew tab: the outfit's picker and its fire loads. The torpedo picker went to the Templates panel
  only, and the "outfit" rename is still to do.
