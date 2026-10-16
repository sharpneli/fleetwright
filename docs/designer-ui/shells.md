# Shell picker (mockup, 2026-10-16; in the game since the same day)

**Status:** the picker is in both battery tabs, built in `Designer/DesignerScene.Shells.cs` with its edits in
`Designer/ShellEdits.cs`. The user's answers to the questions below (2026-10-16):

1. **The turret material is hidden** at its default (the armour scheme's). "I don't really see any use for selecting
   turret armor in that detail, at least for now. We'll come back to it later, shells are more important."
2. **5% steps** are fine.
3. **Templates only.** Custom blocks come later as "copy and edit".
4. **The families sort by the design's navy and year, in a tree.** The navy is the look's dockyard (portsmouth RN,
   kiel DE, and so on) and the year is the look's era (victorian 1890, great war 1912, treaty 1928, WWII 1940, cold
   war 1955). The design's navy comes first and is open, then any navy's families, then the rest. Within each navy
   the families run oldest first, and "her era" marks the latest one not after the design's year. The block combo is
   the same tree one level deeper, navy then family then block, each block with its mass at this calibre and its fuze.

How the game differs from the mockup:

- The picker opens under the cell, right-aligned to it. The trees open under their combos.
- A family change that makes two kinds the same block merges them into one load.
- The read-outs are shipgen's (`Ammunition.ResolveBattery`). The rounds use the class's own split (`Loadout.Split`).
- `-designer-ui=shells:main:0` opens a battery's picker for screenshots. Add `:fam` for its family tree or `:blk:E`
  for entry E's block tree; use `sec` for a secondary.

The rest of this note is the mockup's reasoning.

Step S4 of `docs/battle/shells-armour-damage.md`: the designer picks each gun battery's shells from the shell
templates. This is the design's own outfit, so it decides what the magazines are weighed with and what a ship gets
unless her admiral loads another (S5).

Mockup: `shells.html`, with the real catalogue (`Content/Templates/shells.json`) and dreadnought's two batteries
embedded. Open it in a browser; it is interactive and works offline. Screenshots are in `png/shells-*.png`:

- `closed`: the table
- `open`: the picker
- `list`: a shell's block list
- `sec`: the secondary tab
- `tip`: the cell's hover

`?tab=secondary`, `?open=1` and `&list=0` open those states. Only the battery tables and the Templates panel change;
the arrangement, broadside, rose, presets and Selected panels stay as they are.

## The design

### The SHELLS column (both battery tables)

The column takes the material column's place, in the same combo style (user, 2026-10-16). The cell shows the loads as
kind chips with their shares (**AP 70 · HE 30**) in the magazine order, plus the family (**RN 1906**, or **mixed**).
Hovering shows the shells as fired: block, mass, muzzle velocity, fuze and rounds a gun. Clicking opens the picker
under the cell.

### The picker

- **Family:** one combo with the 26 families (`ShellTemplates`, oldest first). Picking one sets each kind's block to
  the family's. A gun under 200 mm gets the family's light block where there is one, the rule `ammo-templates` used.
  For a family without the kind, common stands in for HE, as S2 did. **Default outfit** puts back the 1912 standard,
  70/30 for a main battery and 30/70 for a secondary.
- **A row per load, in the magazine order:** a numbered badge for the order fired (① brass for the lead), the kind,
  the shell (a combo of that kind's templates by family), the share (± 5%, the others rescaling to 100), and these
  read-outs:
  - rounds a gun (largest remainder, as the class splits them)
  - mass (k·D³ at this calibre)
  - m/s (the gun's velocity with its heaviest shell; a lighter shell leaves faster by the ¼ power)
  - filler (share and explosive)
  - fuze (nose, or base with its delay)
  - × removes the load and gives its share to the largest.
- **+ AP / + common / + HE** adds a kind the battery lacks, from its family, at 20%.
- **The block list** groups by family and gives each block's mass at this calibre and its fuze. Light blocks are dimmed
  on a heavy gun but still pickable (memory: allow silliness). Hovering a block shows its template note (sources,
  [EST]).

### The Templates panel

A **Shells** row joins Plant, Hull, Crew and Armour (the plan's template column). It shows the family when every
battery shares one (else **mixed**), and picking a family sets every battery at once. That's one click to make a ship
"RN 1912".

## What it writes (no engine change)

- Picking a template copies its block into the design's `ammunition` under the template's name, as plant templates
  are copied.
- The battery's `shells` lists `{ammo, share}`.
- Blocks that no battery names are dropped on save.
- A calibre change keeps the outfit (S1: blocks scale with the calibre).
- Each pick is one undo step with a label ("Battery A shells: RN 1912 family", "Battery A HE share 40%").
- The magazine weight follows at the next build (S3).

## Porting to ImGui

- The cell is a combo-sized `InvisibleButton` with the chips drawn on it, opening an `ImGui.BeginPopup`. The popup is
  a `BeginTable` of combos and cell spinners, reusing the tabs' NumberRow logic: wheel to step, click to type.
- The read-outs come from `Ammunition.Resolve` on the design, the same as the report, so the picker and the build
  never disagree.
- The new knobs (`main{i}_shells`, `secondary{i}_shells`) go through `Knobs`, so `-steps=` and undo labels keep
  working. `-designer-ui=shells:K` opens the popup for screenshots.
- The rounds read-out takes the class's largest-remainder split from `Loadout`. One implementation, so it moves to a
  shared helper.

## Questions

1. **Where does the turret material go?** The material column leaves both tables. I suggest the Protection tab, whose
   plate table already lists the turret faces, and whose Materials template column the plan's phase P adds anyway.
   The other option is a small combo in each tab's Selected panel.
2. **Shares in 5% steps, or typed?** The mockup steps by 5. The game's spinners also take typing, so both are free.
3. **Custom blocks:** this pass picks templates only. Editing a block's numbers (k, filler, fuze), for a player's own
   or silly shell, could come later as "copy and edit" in the same popup. Is that right?
4. **Family list:** all 26 families show until the tech tree filters them, sorted oldest first. Should it sort by
   the design's navy or year instead?
