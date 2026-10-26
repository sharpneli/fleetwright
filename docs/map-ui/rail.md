# The forces rail: the left column (mockup 2026-10-26)

The rail at the left of the battle map (`MapScene.Rail.cs`, battle-hud.md U4) and the ticker under it (`MapScene.Events.cs`,
U6) redrawn in the UI language, as the card at the right was (`card.md`). The mockup is `rail.html` (`png/rail-mock.png`;
`png/rail-mock-notes.png` with numbered notes, N toggles them), drawn over the game's capture of the Jutland at 16:16 with
Lion selected (`png/rail-before.png`); the scene's own capture of that frame (`png/rail-scene.png`) patches the sea where
the old ticker showed. The ships' state is made up.

## What was wrong with it

- **Not a pane**: a translucent window that grows with its content, no title strips, heads in grey capitals, two lines a
  row (33 px). The card beside it is now a docked workstation pane; the rail looked like the old dev panels.
- **A shut unit says nothing**: in the Jutland the Battlecruiser Fleet's squadrons show as names with "4 ships · Line of
  bearing"; which one has a ship falling out, burning or sinking shows only when it's opened, and opening is a second
  click on the head (no twisty).
- **The enemy nearest first**: the 25 contacts in sight are cut to 10, and the ten nearest are the destroyers in the
  melee between the lines, so Lützow, the ship Lion is fighting, isn't on the rail.
- **The ticker** floats under the rail at the bottom left, the strip passes under it, and both fight for that corner.

## The column

One column of three docked panes, 330 px wide, from the top bar down to the strip (912 px at 1080): the card dock's
twin on the other side. Opaque, title strips, panes touching. The panes size to their content: the forces take what
they need up to most of the column and scroll inside past that, the log keeps seven rows, the enemy has the rest.

### Forces

- **Title strip**: FORCES, "your side", the ship count and `Tab` (steps your ships). **Sub-header**: the side's
  commander, "fights the units you haven't taken", a segmented **On · Off** (was the "AI commands: on" chip).
- **A tree table**, a row each (20 px): `UNIT · SHIP | DOING | AFLOAT | KN | HITS | whose`. Units are group rows
  (`Panel2`, bright semibold) with a twisty that folds them; a click on the row still selects the unit and opens its card,
  Ctrl+click still picks it beside the selection, drag and drop as before. A unit's name spans the table with its task
  after it in muted ("1st Battlecruiser Squadron · fight"); its formation is on its card and in the strip.
- **Ships**: a friend's ring, the name (brass when selected, with the 3 px bar), a flame mark when burning; DOING is the
  target ("→ Lützow"), falling out and why in amber, going down in red, "no target" muted. A unit shut shows a flame and
  the number of its ships burning.
- **AFLOAT** is a ledger bar: a ship's reserve buoyancy, or for a unit a segment a ship: a squadron's in line order (the
  third segment is the third ship in line on the map), a force's in state order (sound, low, going down, lost) so one
  lost ship among fifty still shows. Sound is neutral grey, as the card's healthy mounts are; amber low, red going down,
  an empty box edged red lost. KN is her speed (a unit's, its guide's), HITS her hits taken (a unit's, the sum).
- **Whose** as status chips on the top units and ships alone, as now: `AI` plain, `you` bright, `conn` in the conn's
  green. A click on the chip hands the unit over or takes it, as the tag does today.
- **Lost ships** stay in their squadron as faint rows: "Indefatigable · sunk 16:03". (Dropped: question 5.)
- **On its own**: the ships in no squadron under a plain row at the end; while a drag is on, a brass "on its own" row at
  the end takes a drop (the UI language's "+ Add" row, in place of the outlined box).

### Enemy

- **Title strip**: ENEMY, "from Lion" (the ship the ranges are from), the number in sight.
- **Grouped by type** as held (`ContactsView.Type`): heavy ships, cruisers, destroyers, small craft, then ships and smoke
  not yet made out; nearest first in each. A group row gives how many and how many in sight, how many of yours fire at
  them and the nearest range. Folded groups stay folded; destroyers start folded once there are more than six.
- **Rows**: the hostile diamond (dashed until made out), her identity as held, what's seen ("burning · by the bow",
  "no damage seen" muted), **ON** the number of your ships firing at her (the selected ship's target in the accent), KM
  and BRG from the ship in the title. Out of sight: faint, "last seen 16:14". No cut at ten: the pane scrolls.

### Log

The ticker becomes the column's last pane: LOG, "newest first", the line count and `J` (the whole log, filtered, as
now). Seven rows, newest at the top, time, a kind word (`hit`, `seen`, `gun`, `torp`, `capt`), the line; amber for what
happened to us, red for a loss. No fading: a new line comes in at the top. A click on a row goes to her, as the ticker's
does. The bottom left is left to the strip.

## For the port

Everything shown is in the snapshot or the map's own state, except the lost ships' rows: a sunk ship leaves
`ShipsView`, so the map keeps the player's losses (name, squadron, when) from the events it already makes (U6), in
order, and the rail reads them. The rest: `GroupsView` (members in line order, task, `Commanded`), `ConditionsView`
(`WaterT`, `ReserveT`, `Fires`, `Hits`, `Status`, `Conned`), `ShipsView` (`FireTarget`, `Reason`, `Speed`), `ContactsView`
(`Type`, `Level`, `Seen`, `InSight`, `SeenMs`); ON counts the player's ships' targets, made once a frame into an index by
entity (no scan per row). The panes are `Pieces`' title strip, sub-header, segmented, table rows, group rows, ledger bar
and chips; the twisty, the flame and the diamond as shapes. No allocation on a steady frame, as the card.

## Questions, answered (the user, 2026-10-26)

1. **The column stops above the strip** (the strip at a force's level is ~1,260 px, too wide between the columns): fine.
2. **The ticker into the column** as the log pane: yes, "looks better".
3. **The enemy by type**: yes.
4. **Whose chips** (`AI` quiet, `you` plain, `conn` green): fine.
5. **Lost ships**: dropped, not kept as rows; a lost ship's segment goes with her.
6. **ON** (your ships firing at each contact): asked what it was; kept, renamed **FIRING** with a tooltip on its header.
7. **Width and a key**: unclear in the question (it was the column's 330 px against the old 270 px, and whether a key
   should hide the column as V shuts the card); built at 330 px with no key.

The user took the build as is (2026-10-26): FIRING stays, 330 px, no key.

And: **the enemy pane smaller**, and **a unit folds on its twisty by the usual tree rules**, even while it holds the
selection.

## As built (2026-10-26)

- `MapScene.Rail.cs`: three windows, one a pane (so the map between them takes its clicks), drawn on `Pieces`. The frame's
  indices first (ships, conditions, groups by entity or id, each ship's squadron, the main guns' targets, how many of
  yours fire at each target), so no row scans the snapshot (the old rail did, per row); then the rows as a list, laid out
  once and drawn from it.
- **Heights**: the log seven rows at the foot; the forces from the top as tall as they need, leaving the enemy up to 30 %
  of the room past a long order of battle (all it needs beside a short one); each scrolls inside.
- **Folding**: a twisty is the only thing that opens or shuts a unit, and the choice holds (by group id). Untouched, a unit
  is open in a small order of battle (12 ships or fewer) and a force always; in a big one its squadrons start shut. A
  selection from outside the rail (the map, Tab, the log, a start-up `-select=`) opens the units she's in and scrolls to
  her, once; a click in the rail doesn't, so selecting a unit's row doesn't open it. The enemy's kinds fold on a click on
  their row; untouched, heavy ships and cruisers are open, the others when six or fewer.
- **A unit's row**: its name, its ships burning (a flame and a count) while shut, its task; its bar a segment a ship in
  line order (a force's by state: sound, low, going down); its guide's speed, its hits. Whose it is on a top unit or a
  ship alone; `conn` on any unit or ship the player holds by the conn.
- **A ship's row**: names and doing in the small font (the card's rows'), "on V27" for her target (the fonts have no →).
- **The log pane** (`MapScene.Events.cs` `LogPane`, the ticker gone): the kind word amber for a hit on us, red for a loss
  of ours, the rest muted; the title strip opens the whole log.
- `Pieces` gained `SegmentBar` and the quiet chip, `BeginBody` a fixed head under the title (a sub-header, a header row);
  `Theme` the sound ship's grey (`Sound`).

Checks: the map UI makes 489 bytes a frame over 3,000 frames of the Jutland at 240x with Lion's card open, against 482
before the change (the events'; no garbage from the rail itself); tests pass (207). Captures:
`png/rail-game-jutland.png` (Lion selected, her squadron opened for her), `png/rail-game-shut.png` (the same with the
1st Battlecruiser Squadron shut on its twisty, Lion still selected inside it), `png/rail-game-squadrons.png` (a short
order of battle: the panes at their content).
