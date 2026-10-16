# Protection tab: the layers (H8b mockup, 2026-10-16)

**Status:** mockup for review. It is H8b of `docs/shipgen/protection-plan.md`: the Protection tab learns the parts that
H1-H8a added to the engine (platform decks, handling rooms, magazine wings, TDS layers, the inner bulkhead, zones,
belt extent, magazine boxes, sloped decks, inclined and internal belts, layout templates, the compartment check). The
tab as it is (`protection.md`, `DesignerScene.Protection.cs`) stays the base: what isn't mentioned here works as it does
today.

Mockup: `protection-layers.html` (data in `protection-layers-data.js`). Like the other mockups it shows only the tab's
pane, 1280 × 578 (its size at 1920 × 1080), and it's interactive and works offline.

## Where it goes

The new parts go on the **Protection tab**, mostly in its **midship section**, which becomes two half-sections. The
Hull & upperworks tab isn't the place:

- They are armour inputs. They carry weight, they sit in the armour scheme's templates, and they come up whenever you
  think about the belt and the decks. A slope is a deck's other half, and an internal belt is the belt.
- Almost all of them can only be seen in a section: a slope, a lean, a wing, a layer, the inner bulkhead, a box's
  sides. The elevation shows where they run along the ship, and the section shows what they are.
- Tumblehome shows anyway. The section is drawn from the hull form's stations, so a tumblehome or flared side is drawn
  as it is, and the belt and slopes follow it. When the Hull tab gets its rework, it can reuse the same section
  renderer to show the form (read-only, or with its own handles). The protection controls stay here.

## The layout

| | before | here |
|---|---|---|
| Elevation | 836 × 224 | 738 × 200 |
| Section | 426 × 224, one midship cut | **524 × 300**, two half-sections |
| Plate table | 836 × 304 | 738 × 328 (Material column narrower) |
| Selected plate | 426 × 304 | 524 × 228, facts on the left, **Shape** on the right |

The section gets the most room because most of the new parts live there. Bismarck draws at about 14 px a metre,
Kent at 18 and a destroyer at 27, against about 9 before.

## 1. Sections: two cuts, one half each

A ship is symmetric, so each half of the drawing can be its own station, as in a naval architect's "section at frame
120 | section at frame 160". The defaults are **1, the machinery** (the room nearest midships) on the left and **2, the
foremost main magazine** on the right. Side by side, they show the two places the protection is built for, and where
they differ: a zone, a box, the inner bulkhead's thicker plate beside the magazines, a wing, a platform deck.

- **The split is unmistakable.** A dark gutter divides two different cuts, with a brass break line (the drawing
  convention for a cut-away), and a numbered badge on each side carries that half's name, matching the flags on the
  elevation. Each half's x stands at its outer top corner. Without it, two stations read like a broken profile (review,
  2026-10-16): the deck steps where one cut has the forecastle and the other doesn't, and the finer bow side flares.
- **One whole section when you want it: `Whole section`.** A toggle in the header locks section 2 to section 1. The
  gutter closes to a plain centreline, the elevation shows one flag (`◆`), section 2's dropdown greys to "as 1", and
  dragging the flag or picking a station moves both halves together, so you can flip along the ship and stay
  symmetric. The handles draw once, on the starboard side. Toggling it off puts section 2 back where it was
  (`protection-layers-linked.png`; `protection-layers-bismarck-whole.png` is the same at the machinery). Decided at
  review, 2026-10-16: two cuts by default, with this toggle.
- **Choosing the cuts.** Each half has a dropdown in the header (Machinery · each magazine · fore end · aft end). On the
  elevation, two brass flags `1` `2` mark the cuts; you drag them along the ship and they snap to those stops within 3 m.
- **The cells,** from the build: each space tinted by what it holds (magazine, handling room, stores, TDS, machinery,
  coal, double bottom hatched), with its name when it fits. Hovering one says what it is, its size and why it's there:
  "the magazine's wing: stores between it and the side", "crown 1.1 m under the waterline, a platform deck over it".
- **The walls:** wing and TDS bulkheads as thin plating. A platform deck is a cream line at a magazine's crown.
- **The armour,** three times as thick as it is, labelled in mm:
  - the belt on the side, tapering below the waterline, with a lean marked by an arc and its angle;
  - an internal belt inside the shell, leaning, carried down as the lower belt to the inner bottom (`107→25`);
  - the zone at the magazines in place of the belt where the cut is in one;
  - each armour deck's flat and its slopes down to the belt's lower edge (a ridge where they meet in a fine end);
  - the inner bulkhead on whichever wall carries it (the TDS's holding bulkhead, the wing bunkers' wall, the
    magazine's wing) or on its own;
  - a magazine box's sides on the wing wall, its crown on the platform deck and its floor on the inner bottom;
  - the barbettes the cut passes through, down to their deck.
- **Brass handles** for the selected part, as on the elevation. All of them drag, and in the game a drag edits once,
  on release:
  - the belt's top and bottom edges (up and down);
  - the belt's lean (the round handle: drag it out);
  - a sloped deck's knee, where the flat turns down: in and out, which sets the angle, while the foot stays on the
    belt's lower edge;
  - the TDS's holding bulkhead (depth);
  - the magazine wing's wall (its width; a box's sides stand on it);
  - an inner bulkhead's own wall (how far in).

## 2. Elevation: what runs where

The same drawing, plus:

- **Zones**: the belt and the deck in their own thickness over the magazine stretches.
- **Belt extent**: the machinery belt is drawn only where it runs.
- **Internal belt**: the belt hatched (it's behind the shell), the lower belt as a faint band to the inner bottom, and a
  tag `inside · 18°`.
- **Slopes**: a band from the deck down to the belt's lower edge, with a tag `slopes 110 · 22°`.
- **Inner bulkhead**: a dashed outline from the inner bottom to the armour deck over the citadel, solid where it's
  thicker at the magazines.
- **Boxes**: each magazine run, with its crown, floor and ends as plates and its sides as a fill.
- **TDS**: the hatched band, now labelled with its layers.
- Two toggles in the header:
  - **Rooms** (on by default): magazines, handling rooms and machinery, tinted.
  - **Compartments** (off): the main watertight bulkheads. The chip shows the compartment standard (`2 ✓`, or amber
    `1 ⚠`). When a ship is warned, the run that sinks her is hatched amber, and its tooltip carries H7's warning.
- The citadel's brackets and length are left out on a ship with no armour.

## 3. The table: one row per part, sub-rows for a part's other zones

- **Side** keeps the belt, the upper belt, the end belts and the citadel bulkheads. A tag after the belt's name gives
  its shape: `inside · 18°`, `11°`, `machinery`.
- **Sub-rows** (`↳`) under their part, each with its own thickness, weight and `×`:
  - the belt's **↳ at the magazines** (`belt_magazines_mm`);
  - a deck's **↳ slopes** (`slope_mm`; the angle shown read-only, `at 22°`, and edited in Shape or by the knee handle);
  - a deck's **↳ at the magazines** (`magazines_mm`).
- A new group, **Inboard: behind the side**:
  - **Torpedo protection** (moved here): depth, `layers N`, "3 spaces · holding 45 mm".
  - **Inner bulkhead**: over the citadel mm, `mags N`, and what it stands on.
  - **Magazine boxes**: sides, `crown`, ends, floor.
  - **Magazine wings**: the width (auto 0.1 B, shown in grey), "stores between magazine and side". On a TDS ship the
    row is greyed: "the torpedo protection stands there".
- The group header has a `+` chip for each part the ship lacks: torpedo protection, inner bulkhead, magazine boxes,
  magazine wings (when set to 0). The starting values come from the belt: inner bulkhead 0.10 / 0.14 × the belt, a box
  0.4 × the belt with a 0.6 × crown and ends, wings auto.
- Right-click on the belt or a deck (row or drawing) offers its zone, slopes, inside/outside and machinery/citadel.

## 4. The selected plate: facts, and the part's Shape

Thicknesses live in the table only (review 2026-10-16: sub-rows kept, without duplication). Shape holds what changes a
plate's form: adding or removing a zone or slopes, a slope's angle, the belt's lean and position; a zone's or slope's
thickness shows there read-only, "374 mm, its row".

The panel splits in two. On the left are the facts (covers, height, weight, material, taper). On the right is
**Shape**, the controls that change what the part is rather than how thick it is. The material dropdown moves to
Shape's header.

| Part | Shape |
|---|---|
| Belt | Stands: on the side · inside the shell. Leans: ° (top outboard). Runs over: the citadel · the machinery. At the magazines: `+ zone`, or its mm (read-only) `×`. Warnings: an untapered internal belt ("the lower belt starts at full thickness"), a machinery belt without boxes ("the magazines' sides are bare") |
| Citadel deck | Slopes: `+ slopes`, or ° and its mm (read-only) `×`. Flat: "80 mm, stops 6.1 m in from the side". At the magazines: `+ zone`, or its mm (read-only) `×`. End decks: "flat, side to side" |
| Torpedo protection | Depth, layers, holding bulkhead mm (auto), layer walls mm (auto); "same weight however it's split"; amber while layers are holed at once (H3, open) |
| Inner bulkhead | Over the citadel mm, at the magazines mm, stands on (read-only), in from the side (only for its own wall) |
| Magazine boxes | Sides, crown, ends, floor; the runs it boxes |
| Magazine wings | Width (auto 0.1 B, with an `auto` chip once set); the stores rule |

The immune-zone placeholder shrinks to one line at the foot.

## 5. The sub-bar: Layout

`Coverage [All-or-nothing | Armoured ends | Complete belt]` stays. **Layout** is new: the protection scheme templates
(`Content/Templates/protection.json`) as a plain list of names, no groups and no descriptions (review 2026-10-16: a
cruiser's layout on a battleship is the player's call). Hovering one lists what it does to *this* ship ("Second deck: slopes 76 mm at 30°, flat 50 mm · inner
bulkhead 30 mm, 40 at the magazines"); a click applies it (`ProtectionSchemes.Apply`). **Plain** at the foot clears the
layout keys. The dropdown shows the template the design is on, "· own values" in amber where the design keeps its own
figures (Nelson's 18°), "· edited" after a hand edit, and **Custom** when no template fits. The old "Scheme" dropdown is
renamed **Materials**, since it is the armour material scheme. The "reads as" line gains the layout:
"reads as all-or-nothing · inclined, internal".

The Templates panel in the outliner gets a **Protection** row beside Armour (same list).

## The engine's inputs, and where each one is edited

| Input | Table | Shape | Drawing |
|---|---|---|---|
| `belt_position` | tag | Stands | elevation hatch, section |
| `belt_incline_deg` | tag | Leans | section's round handle |
| `belt_extent` | tag | Runs over | elevation |
| `belt_magazines_mm` | ↳ row | At the magazines | both |
| deck `slope_mm`, `slope_deg` | ↳ row, `at °` | Slopes | section's knee handle |
| deck `magazines_mm` | ↳ row | At the magazines | both |
| `tds_layers`, `tds_holding_mm`, `tds_layer_mm` | `layers`, where | TDS rows | section |
| `inner_bulkhead {mm, magazines_mm, inboard_m}` | row | its rows | section's handle (own wall) |
| `magazine_box {side, crown, end, floor}` | row | its rows | elevation, section |
| `magazine_wing_m` | row | Width | section's handle |
| the scheme templates | | | sub-bar Layout |
| `compartment_standard` (H7, read-only) | | | elevation toggle |

`subdivision.doors_below` isn't here: it's a crew and damage-control choice, for the Crew tab's session.

These stay engine rules the tab only shows (memory: engine rules read-only): a slope's foot on the belt's lower edge,
the internal belt's 0.3 m inset and 25 mm foot, the inner bulkhead riding on the wall the section already has, a box's
sides on the wing wall, wings never fuel.

## Findings from drawing the builds

1. **KGV's zones don't cover her secondary magazines.** The zones follow `Geo.MagazineSpans` (Stow's compartments),
   not the magazine rooms. The S aft magazine room runs −52.9 to −43.9 m, but its zone covers only −45.9 to −43.9, so
   7 m of a magazine keeps 348 mm instead of 374. The S fore magazine is the same. Drawn as built, they show as
   2 m slivers (`protection-layers-kgv.png`). Proposed fix: zones (and the inner bulkhead's `magazines_mm`) from the
   magazine rooms' cells. That changes only KGV's golden, a few tonnes. To fix before or in the port; it is the same
   root as the plan's open "box weight from Stow's compartments".
   **Fixed 2026-10-16** (protection-plan.md, H8): the zones and the weights now go by the magazine rooms; KGV's S
   aft zone runs −52.9 to −43.9 m with its room.
2. **Wings in the fine ends look wider than asked.** On the destroyer, the 1.2 m wing is 2.3 m wide in its forward
   magazine's cut. H2 measures the wing halfway up the magazine, and box cells can't follow the V. That is right, but
   the section shows it plainly. The wing handle sets the input, not the cell; its tooltip should say so.
3. **The three-times plate scale hides thin plates.** A 25 mm belt (Kent) is about 1 px. Labels carry it, but a thin
   plate's hit area should be at least 4 px in the port.

## What's real and what isn't

- **Real:** the geometry of seven shipped designs, built with `shipgen design --out` on 2026-10-16 (after H8a):
  bismarck (turtleback, TDS), dreadnought (belt and slope, magazine screens), nelson (inclined internal, warned
  one-compartment), kent (machinery belt and boxes), dunkerque (French: lean, sloped lower deck, floor plate), kgv
  (zones), destroyer (unarmoured, wings). That covers the hull stations, the cells and their rooms, the longitudinal
  walls, the armour report, the weight lines, the compartment standard and its warning, and the layout templates.
- **Stand-in:** after an edit, the drawings move the plates the way the engine would: armour is drawn from the edited
  inputs, and a wing or TDS wall moves to its new width. The tonnages scale the build's line by area and thickness.
  The game rebuilds instead. Coverage presets and Materials don't act in this mockup (see `protection.html`), and
  neither do the elevation's `+` ghosts and handles.

## Questions for the review

1. ~~Two half-sections or one full section?~~ Settled 2026-10-16: two cuts by default, the split made unmistakable (gutter, break line, badges), and a Whole section toggle that keeps 2 locked to 1.
2. ~~Sub-rows or Shape only?~~ Settled 2026-10-16: sub-rows, with thickness only in the table; Shape keeps form.
3. ~~The Compartments toggle here?~~ Settled 2026-10-16: here (a visual toggle, nothing lost).
4. ~~Layout where?~~ Settled 2026-10-16: both, the sub-bar and the Templates panel.

## Port plan

- `DesignerScene.Protection.cs`: the pane's new split; `ProtectionSection` (new file) draws a half-section at a
  station from `Ship.Hitboxes` (stations, cells and rooms, walls, `Armour` with `Inclined`, box decks and bulkheads),
  so the Hull tab can reuse it; cuts as UI state (`protCut0`, `protCut1`, snapped to the stops).
- `ProtectionEdits.cs`: add a zone, slopes, inner bulkhead, box, wings, with starting values; remove; layout apply
  through `ProtectionSchemes.Apply`; the layout's reading ("own values", "edited", "Custom").
- `Knobs`: a knob per new input where none exists yet (the scale knob already covers them, H8a).
- Handles: the lean (angle from the drag), the knee (angle from where it is dragged to, the foot fixed), the wing, the
  holding bulkhead, the inner wall; an edit on release, as now.
- Dev hook additions: `protcut:K:STOP`, `protsel:beltzone|slopeN|dzoneN|inner|box|wings`, `protlayout` (menu open),
  `protcomp` (the toggle); then verify every control with `-input=` clicks and drags, as for the other tabs.

## URL options

`?ship=bismarck|dreadnought|nelson|kent|dunkerque|kgv|destroyer`, `&sel=ID` (belt, beltzone, upper, end_fore,
end_aft, bulk, deckN, slopeN, dzoneN, tds, inner, box, wings, steer, main0, barb, ct), `&cut1=` and `&cut2=` (mach,
mag:A, fore, aft, or x in m), `&do=` edits, comma-separated `path:value` on the armour input
(`belt_incline_deg:15`, `tds_layers:3`, `magazine_box.side_mm:25`, `layout:german_bulkhead`, `KEY:del`),
`&link=1` locks section 2 to section 1 (Whole section); `&menu=layout|cut1|cut2|ID`, `&tip=layout:N|cell:K:N|handle|comp`, `&rooms=0`, `&comp=1`.

Screenshots (`png/protection-layers-*.png`):

- `bismarck`: as opened.
- `dreadnought`: the magazine screens on the wing walls; coal bunkers in the machinery cut.
- `nelson-comp`: the internal belt and lower belt; Compartments on, the warned run hatched.
- `kent`: boxes and the machinery belt.
- `dunkerque`: lean, the sloped lower deck, the floor plate.
- `kgv`: zones, showing finding 1.
- `bismarck-layers`: three TDS layers with a 45 mm holding bulkhead, cut at X and A.
- `destroyer-box`: a 25 mm box and 1.6 m wings added.
- `layout-menu`: the Layout list (names only), hovering German bulkhead.
- `dread-german`: German bulkhead applied.
- `bismarck-whole`: both cuts at the machinery, one whole section, the slopes selected.
- `cutmenu`: section 2's dropdown, the stops.
- `linked`: Whole section on, cut at magazine A, one flag on the elevation.
