# Protection tab (mockup, 2026-10-11)

Mockup: `protection.html`. Open it in a browser; it is interactive and works offline. Like the other tab mockups it
shows **only the tab's pane** (1280 × 578, its size at 1920 × 1080). The plan, the outliner and the ledger stay as they
are.

URL options: `?ship=bismarck` (the default), `dreadnought` (armoured ends, decks over the fore and aft ends) or `yamato`
(all-or-nothing, steering gear box). For screenshots: `&sel=deck1` selects a plate, `&do=adddeck:1:citadel,deck0:-2`
edits, `&tip=step:deck0:1` or `&tip=ghost:gh-1-citadel` draws a tooltip, `&menu=deck1` opens a plate's context menu.

**What's real and what isn't:**

- **Real:** each ship's geometry (the belt, strakes, bulkheads, armour decks, barbettes, turrets, conning tower, the
  deck stack and the midship station), its weight lines and every one-step figure. They're `shipgen design` builds of the
  shipped design, and of the design with that one value moved one step, done on 2026-10-11: 30 probe builds for
  Bismarck, 30 for Dreadnought and 22 for Yamato. The step tooltips show each probe's biggest weight lines and what the
  yard re-planned.
- **Stand-in:** once you edit, the t column scales the opened build's line by area and thickness, and the step columns
  turn grey with a `≈`. The game builds and probes the edited ship instead, as the Overview does.

## Why the current tab is slow

![today, on Bismarck](png/protection-before.png)

Today the tab is the generic knob list: about 40 rows in two columns, with Side, Side details, Steering gear box and
Materials part by part folded away.

- **There's no picture of where the armour is.** The profile above the pane shows the plates, but small, and you can't
  click them. To see that Dreadnought's third deck is armoured over both ends but not under the citadel, you read three
  "Armour deck N" blocks.
- **An armour deck is four controls.** Thickness, "On deck" as a number (0 is the main deck, 2 the third), "Over" as a
  dropdown, and material. Adding one is: Add, then wheel the deck number, then pick the extent, then set the thickness.
- **Related values sit far apart.** The belt's lower edge, its height above and depth below the waterline, and its
  material are in three different folds.
- **What a value costs is invisible.** There's no weight per plate and no one-step figure, so you can't tell which
  plate is cheap to thicken.

## The design

The pane has four parts. The sub-bar names what the scheme reads as (all-or-nothing, incremental with armoured ends,
protected deck), the armour's weight and share of standard, and the armour scheme (the same template as in the
Templates panel).

### 1. Elevation (top left)

The ship's side, aft on the left as in the profile. The length is to scale and the heights are stretched (about ×2.4)
so that each deck is a lane, with its name in the margin.

- **Every plate is drawn where it is**, in the armour ramp's colour, labelled with its thickness: the belt (with its
  taper below the waterline), the upper belt, the end belts (root and tip), both citadel bulkheads (thinner below the
  belt), each armour deck on its own deck, the steering gear box, the barbettes (dashed inside the hull), the turrets
  and the conning tower. The torpedo protection is a hatched band under the roof deck.
- **Click** a plate to select it, **wheel** over it to step its thickness, **right-click** for its menu.
- **`+` ghosts:** hovering a bare stretch of a deck (aft end, citadel, fore end) shows a dashed `+` and what it would
  add ("+ armour deck on the second deck, citadel, 25 mm, ≈ +1,004 t"). One click adds it. The same goes for a missing
  upper belt, end belt or steering gear box.
- The citadel is bracketed and its length shown. The layout places it (between the outermost barbettes), so the tab
  shows it and doesn't edit it.

### 2. Midship section (top right)

The midship station from the hull form, **to scale**, with the plates drawn three times as thick so they show:

- belt and upper belt on both sides, tapering below the waterline;
- every armour deck that crosses the station;
- the torpedo protection: the holding bulkhead `tds_m` in from the shell, inner bottom to the roof deck, hatched.

The belt's two height values sit on the drawing as spinners at its top and bottom edges (↑ above the waterline,
↓ below it), and the torpedo protection's depth beside it. They're the values that matter most in the section and the
hardest to picture as numbers.

### 3. Plate table (bottom left)

One row per plate, in four groups: side and torpedo protection, armour decks, ends, guns and conning tower. A plate the
design doesn't have stays as a grey row with 0 mm, so you can spin it up.

| Column | What it is |
|---|---|
| Thickness | spinner: ◀ ▶, wheel (Shift ×4), click to type. The torpedo protection's depth in m |
| Lower · tip · roof | the second value: the belt's lower edge (auto: the belt), the end belt's tip, the steering box's roof |
| Where | the belt's heights; the upper belt's top deck (dropdown) and extent (segmented: cit · full · fore · aft · ends); an end belt's reach; an armour deck's deck (dropdown) and extent; the turret faces' side, rear and roof |
| Material | the scheme's material in grey, or the plate's own. A click picks one |
| t | the plate's weight from the build, with the weight lines in the tooltip |
| − step, + step | standard displacement one step down and up, from real probe builds. Amber when the step re-plans the ship (below) |

The armour decks' group header has "+ add an armour deck…" for the keyboard route; the elevation's `+` is the quick
one. The guns group notes that turret faces are also on the Main battery tab, and the secondary shields on the
Secondary tab.

### 4. Selected plate (bottom right)

What the plate covers (from–to along the ship, its length and share), its height (deck, or from–to on the waterline),
weight and share of standard, material, the taper, and a line on what the engine does with it ("Full thickness down to
the belt's lower edge, half below it"). Below that, **one step** in a small table: the standard displacement and the
biggest weight lines for − and +, and the citadel's change when the yard re-plans.

### Clicks

| Task | Today | Here |
|---|---|---|
| Add an armour deck on the second deck over the citadel | Add, wheel "On deck" to 1, pick "Over", set the thickness | one click on the `+`, then wheel |
| Move a deck down one | wheel "On deck" | right-click, "Down to the fourth deck" (or the deck dropdown) |
| Armour the ends of a deck | "Over" dropdown | one segment click |
| Raise the belt's top edge 0.3 m | open Side details, find the row, three wheel steps | three wheel steps on ↑ in the section |
| Find what 10 mm of belt costs | Overview tab | the row's step columns |

## What the engine already does

Every control maps onto today's design fields (`armour` in the design JSON), so the port needs no engine changes:

- `belt_mm`, `belt_bottom_mm`, `belt_depth_m`, `belt_height_m`, `bulkhead_mm`, `tds_m`
- `upper_belt` {`mm`, `to_deck`, `extent`, `material`}
- `end_belts.fore|aft` {`mm`, `tip_mm`, `reach`, `bulkhead_mm`, `material`}
- `decks[]` {`deck`, `mm`, `extent` citadel · full · fore · aft · ends, `material`}
- `steering_box` {`mm`, `deck_mm`, `bulkhead_mm`, `material`, `deck_material`}
- `materials` (per part, from the armour scheme), and `main[].armour_mm` for the turret faces.

These are the engine's and the tab only shows them, read-only:

- **The citadel's length** comes from the layout (the end barbettes).
- **The conning tower takes the belt's thickness**; it has no value of its own.
- **Barbettes are 0.8 × the turret faces**, and the gunhouse's side, rear and roof are fixed shares of the face (0.55,
  0.5, 0.4).
- **The citadel bulkheads are half thickness below the belt.** End belts are the belt's height.
- **The belt is a vertical plate on the shell.** There are no inclined belts or sloped deck edges.

## Engine finding: one step can re-plan the ship

Some one-step probes cost far more than the plate itself, because the step crosses a layout threshold and the yard
lays the ship out again:

| Ship | Step | Standard | The plate alone | What moved |
|---|---|---|---|---|
| Bismarck | main deck, −5 mm | **+1,281 t** | −171 t | citadel +12.4 m, depth +0.36 m |
| Bismarck | main deck, +5 mm | +1,901 t | +171 t | citadel +12.6 m, depth +0.39 m |
| Bismarck | fore end belt, +10 mm | +1,616 t | +33 t | citadel +12.3 m |
| Bismarck | torpedo protection, +0.5 m | +2,157 t | +131 t | citadel +14.9 m, length +0.5 m |
| Yamato | armour deck, +5 mm | **+4,054 t** | +180 t | citadel +20.4 m (137.6 → 158.1 m), draught +0.75 m |

"The plate alone" is the opened build's line scaled by thickness. Bismarck's third deck gives the same figures as its
main deck.

The Overview already shows Bismarck's deck figures and accepted them ("lumpy tonnage stays"; the UI follows the
physics). This tab makes the cause visible: an amber step cell, and the tooltip says "The yard re-plans the ship:
citadel +12.4 m…". Whether a 5 mm thinner deck *should* lengthen the citadel by 12 m is a question for the layout, not
for this tab.

## Port plan

- `Designer/DesignerScene.Protection.cs`: the four parts. The elevation and section draw from `Ship.Hitboxes`
  (`Armour`, `Decks`, the components and `HullForm` stations), never from the design, so they show what was built.
- The table's controls are the existing `Knobs` (Belt, BeltBottom, BeltDepth, BeltHeight, UpperBelt, UpperBeltToDeck,
  UpperBeltExtent, EndBelt*, Bulkheads, Tds, Deck*, Steering*, MainArmour, the material knobs). Adding a deck at a given
  deck and extent is one new helper beside `AddDeck`.
- The step columns run through `DesignProbes`, a batch per edit while the tab is open, as on the Overview.
- A dev hook: `-designer-ui=protsel:ID,protmenu,prottip:step:ID:±1,protghost:DECK:EXT`, and the `-input=` scripted
  clicks to check every control once ported.

## Open questions

1. **Heights in the elevation:** real heights stretched ×2.4, so the waterline and belt edges are true, or schematic
   deck lanes at an even pitch (the Secondary tab's choice)?
2. **The immune zone:** a greyed placeholder in the selected-plate panel (like Fit on the Overview), or leave it out
   until there's a penetration model?
3. **Engine asks**, if any: a conning tower thickness of its own; barbettes of their own thickness; inclined belts or
   sloped deck edges.
4. **Turret faces on two tabs** (here and on Main battery): keep both, or only show them here?

Screenshots: `png/protection-opened.png` (Bismarck as opened), `png/protection-ghost.png` (hovering the second deck's
`+` over the citadel), `png/protection-added-deck.png` (after adding it and thinning the main deck 10 mm: stand-in
figures), `png/protection-dreadnought-menu.png` (right-click on Dreadnought's third deck, fore end),
`png/protection-replan-tip.png` (Yamato, armour deck + 5 mm: +4,054 t, citadel +20.4 m).
