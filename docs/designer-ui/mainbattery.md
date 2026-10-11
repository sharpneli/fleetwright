# Main battery picker (mockup, 2026-10-11)

Mockup: `mainbattery.html`. Open it in a browser; it is interactive and works offline. Screenshots are in
`png/mainbattery-*.png`. It shows the **Main battery tab's pane only** (1280 × 578, the size it gets in the app at
1920 × 1080). The plan, the outliner and the ledger stay as they are.

The mockup uses the engine's own arrangement rules (see "What the engine already decides"), so a click lands where
the shipgen would put the turret. The arcs and tonnes in it are illustrative.

## Why the current tab is slow

Today each battery is a 14-row knob list: calibre, length, guns, four group counts, echelon, two superfiring counts,
faces and rounds, plus cross-deck, "stands on" and material behind Details (`png/` has no shot of it, so run
`-ship=shipgen/designs/lord_nelson.json -section=1`). Lord Nelson's three batteries fill the whole pane. The cost is
in the mapping, not in the number of rows:

- **Positions are counts, not places.** To add a rear superfiring turret you raise "Turrets aft", then check
  "Superfiring aft" (which counts the outermost turret, so the value you want is not obvious). That is two spinners in
  different rows, and you only see the result in the plan.
- **Superfiring is a count per battery per end, but the engine treats it as one run per end, across batteries.**
  Setting it right on a mixed end means doing arithmetic over several batteries' lists.
- **Battery order matters, but nothing shows it.** At each end, the first battery's turrets are outboard.
- **Wing settings are spread out.** Echelon is a main row, cross-deck is a Details row that only applies once echelon
  is on, and "stands on" is another Details row. That is three places and a fold.
- **You can't see the batteries side by side.** You compare A against C by scrolling.

## The design

There are three bands and an inspector. Every band edits the same `BatteryInput` list, so nothing new is stored.

### 1. Battery table: what each gun is

One row per battery, with all of them visible at once:

▴▾ order · colour + letter · calibre · length · guns per turret (1–4 segmented) · positions · turrets · guns ·
faces · rounds (auto) · material · t · duplicate / remove

- **Clicking a row makes it the active battery:** the one the `+` slots below add. This is the only mode in the tab.
- **Guns per turret is a segmented 1|2|3|4**, not a spinner, so changing it is one click.
- **Positions is a read-only summary in the turret names the plan uses:** `A · X↑ Y`, `W2–3 abreast`,
  `A · W1 ech.×deck · Y`. ↑ marks a superfiring turret. Being able to read the whole armament as one line is the
  point.
- **▴▾ moves a battery outboard or inboard.** The column header says "order = outboard first at each end", because
  that is what the engine does.
- **⧉ duplicates a battery** (same gun, no turrets). This is how you get a second group of the same gun that sits
  elsewhere in the end order.
- Wheel over any spinner steps it, as everywhere in the designer.

### 2. Arrangement: where the turrets are

A schematic plan with the bow to the right, matching the real plan above it. Each turret is a chip in its battery's
colour, named as on the plan (A B C forward, Y X W aft, Q R midships, W1P/W1S wings), with its barrels drawn.

- **`+` slots** (dashed, in the active battery's colour) sit where the next turret would go: inboard at each end, a
  centreline Q and a wing pair in the waist. **One click adds a turret.** If you are adding to a battery that isn't
  last in the order, the ghost sits *in its true slot*, the other chips make room, and the tooltip says why.
- **New turrets superfire by default** when everything outboard of them does, which is the engine's default
  (`superfire` absent = all step). **Shift+click** adds one on the deck instead.
- **The ▲ / ═ button between two neighbouring chips** says whether the inboard turret fires over the outboard one.
  One click toggles it. The run stays contiguous: stepping up turret 3 steps up 2, and bringing 2 down brings 3 down
  too. The tooltip says so before you click.
- **Superfiring chips** get a shadow and a `↑n` tier badge.
- **Wheel over an end group or the waist** adds or removes the active battery's turrets there, like spinning the old
  count without finding its row.
- **Clicking a chip** selects it (inspector, plan highlight). **Del** removes it. **Right-click** gives: superfire /
  on the deck, change battery (lists the others), "New battery from this turret" (splits it off, the way to make a
  mixed end), move to fore / aft / midships / wings, and remove.
- **Wing cluster**, one per battery with waist turrets, laid over the bottom of the schematic:
  `[abreast | echelon] [cross-deck] [deck | deckhouse]`. Echelon is one click, and cross-deck is one more. The
  cross-deck button greys out unless echelon is on, because the engine warns about cross-deck without echelon.
  Midships-only batteries show only deck / deckhouse.

### 3. Guns bearing (right of the arrangement)

A polar rose: main-battery guns bearing at each bearing, bow up, with readouts for ahead · astern, broadside port ·
stbd, and the most guns at once and their bearing. This is the feedback that decides an arrangement: echelon with
cross-deck takes Invincible from 6 to 8 on the broadside, and X superfiring over Y takes astern from 2 to 4. In the
game it comes from the built ship's mount arcs (`MountMark.Arcs`, add the gun count). Like the ledger, it lags the
worker by one build.

### 4. Arrangements and the inspector (bottom)

- **Arrangement presets** apply to the active battery. Each keeps the gun and replaces only the positions, as one
  undo step. The presets: 1·1, 2·2 superfiring, 2·Q·2, 3 forward, 2·1, Dreadnought, Hexagonal, Echelon. Each has a
  pictogram and its shorthand. These are accelerators, not the model: you can hand-edit the result straight away.
- **Selected:** the turret's name, battery, place ("aft end, 2nd from the stern · superfiring, tier 1"), arcs, and
  Superfire / Battery ▾ / Remove buttons.

## Click counts

| Task | Now | Mockup |
|---|---|---|
| Add a rear superfiring turret to battery A | find A's "Turrets aft" ▶, then find "Superfiring aft" and work out the value | 1 (the aft `+`) |
| Put an existing aft turret on the deck | spin "Superfiring aft", counting the outermost | 1 (the ▲ between them) |
| Wings to echelon with cross-deck | echelon toggle, open Details, cross-deck toggle | 2 (`echelon`, `cross-deck`) |
| Triple turrets instead of twins | spinner ▶ | 1 (`3`) |
| A Dreadnought layout for a new battery | 5–6 spinners | 1 (preset) |
| Move a turret to another battery | edit the counts of both batteries | right-click → battery |
| Compare batteries A and C | scroll | both rows on screen |

## What the engine already decides (no engine change needed)

All of this maps onto today's `BatteryInput` fields: `fore`, `aft`, `mid`, `wing`, `superfire` {fore, aft},
`echelon`, `cross_deck` and `amidships_stands_on`. The rules the UI has to respect come from `Layout.EndGroup`
(`LayoutWarship.cs`):

1. **The order at an end is the battery order.** The first battery's turrets are outboard. A, B, A is not
   expressible with two batteries, so the UI either reorders the batteries (▴▾) or splits a battery off ("New battery
   from this turret").
2. **Superfiring is one contiguous run per end, from the outermost.** The outermost always counts. A turret steps
   only if every turret outboard of it does. The UI holds one integer per end, `run`, and writes it back per battery
   as "how many of my turrets at this end fall inside the run". A battery whose turrets are all inside it gets `null`,
   so that additions follow the default. An old `superfire: false` is normalised to counts on the first edit.
3. Adding a turret at slot `pos`: the run becomes `run + 1` if `pos == 0`, or if the run reached `pos` (and the
   click wasn't Shift). Otherwise it becomes `min(run, pos)`. Removing a turret inside the run shortens it by one.

The mockup's JS (`endGroup`, `setRun`, `addEnd`, `removeEnd`) is a direct transcription of these rules. I'd make the
end order public in Shipgen (for example `Layout.EndOrder(design, "fore")`), so the designer doesn't keep a second
copy of the rule.

## Porting to ImGui (`DesignerScene.Layout.cs`)

- **Section 1** gets its own body in place of the `Flow` of knob rows. Other sections keep `Flow`.
- **The battery table** is an `ImGui.BeginTable` of compact cell spinners. They are the same NumberRow logic (wheel,
  click to type, auto), just without the label column, and they still go through the `Knobs` (`main{i}_cal`, …), so
  `-steps=` and undo labels keep working. Segmented guns per turret is four small buttons.
- **The arrangement** is a custom draw-list widget, like the outliner: chips, links and ghosts are `InvisibleButton`s
  with hover tooltips. It needs new edits that each make one undo step with a readable label, such as "X added
  (battery A, superfiring)" or "Y on the deck": `AddTurret(d, k, group, flush)`, `RemoveTurret(d, group, idx)`,
  `SetRun(d, end, r)`, `MoveTurret` and `SplitBattery`.
- **The rose** comes from `Overlay.Mounts`. `MountMark` needs the gun count.
- **Plan link:** hovering a chip highlights that mount's tag and arcs on the plan, and clicking a main turret on the
  plan selects its chip. Mount tags already match the chip names.
- **Style gating stays:** midships and wing slots only show when the style has them (`HasMidshipsTurrets`,
  `HasWingTurrets`), and deck / deckhouse only shows with `HasRaisedMounts`. The "Add another battery" rule
  (`TakesMainList`) gates `+ Battery`.
- No deltas, as elsewhere in this UI.

## Open questions

1. **Mixed ends:** are battery order (▴▾) plus "New battery from this turret" enough, or should a right-click
   "change battery" quietly split and merge batteries to make any interleaving work?
2. **Presets:** gate them by era or technology (no superfiring before its year, for example), or always show them
   and let remarks complain?
3. **Rose:** main battery only, or a toggle to add the secondaries?
4. **Drag and drop:** dragging a chip along its end, or into another group, would replace most of the right-click
   menu. Is it worth having in the first port, or later?
5. Should the **secondary battery tab** get the same treatment (casemate and deck mounts per side on a strip), or is
   its table enough?
