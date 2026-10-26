# The UI language: a drawing office, not a web page (2026-10-25)

Every screen in Fleetwright's UI is a **workstation**: panes docked edge to edge around one work surface, with figures in
ledgers, controls in tables and the keys in a status bar. It is the ship designer's look (`docs/designer-ui/bureau.html`,
ported in `Designer/`), and the launch screen's redesign (`docs/map-ui/launch-bureau.html`) shows it carried to a
second screen. New UI starts from here, not from the usual landing-page habits: a hero title, cards, badges, a big
call-to-action button. Playtesters spotted those at once on the first launch screen ("looks vibe coded"); the designer
never drew that remark.

The test for a new piece: **put it beside the designer. Does it look like another tab of the same program?**

## Where it lives

| What | Where |
|---|---|
| Colour tokens, ImGui style (rounding 0, padding, spacing), `Push`/`Pop` around a scene's windows | `src/Fleetwright/Ui/Theme.cs` |
| The pieces (below), one implementation for every screen | `src/Fleetwright/Ui/Pieces.cs` |
| Fonts and the UI scale (1080 px tall = 1.0) | `src/Fleetwright/UiFonts.cs`, `Content/Fonts/` |
| The reference mockups | `docs/designer-ui/bureau.html` (the designer), `docs/map-ui/launch-bureau.html` (a second screen, its CSS a ready token set) |
| As built | `docs/designer-ui/png/*-game-*.png`, `docs/map-ui/png/launch-game-*.png` (the launch screen); any screen via `-screenshot=N -ui` |
| The map's own colours (sea, land, symbols): not chrome, see "Exceptions" | `src/Fleetwright/Map/MapPalette.cs`, `docs/map-ui/symbols.md` |

A new scene's windows push `Theme` (the designer and the launch screen do) and are drawn with `Pieces`: a pane is
`dl.Pane(…)`, a table row `dl.RowBg(…)`, a number the player sets `dl.Stepper(…)`, and so on. No new screen draws its
own pieces or invents hex values for chrome: a missing colour goes into `Theme` with a comment saying what it means,
a missing piece into `Pieces` (and into the table below), so the next screen has it and a change of style is made
once.

## The layout grammar

The frame, top to bottom:

1. **Menu bar** (18 px): File, Edit, View, the screen's own menu; on the right, the F-key screen tabs (the current one
   in the accent on its tint).
2. **Resource bar** (32 px): the subject's name with its file name and a small kind label (`BB`, `1905 · RUSSIA v JAPAN`)
   on the left, then cells of headline figures, each a tiny uppercase label over a mono value, with a rule between
   cells. This takes the place of a page title: the name is read once, at 13 px semibold, and the figures do the rest.
3. **Three columns of panes**:
   - **Left (about 300 px):** an **outliner**, the list of things to pick (design sections, scenarios), as a tree with
     collapsible group rows; below it a **templates-like** pane (templates, your setups).
   - **Centre:** the **work surface** on top (the plan and profile, the opening's map), a strip of **numbered tabs**
     under it (0-7, 1-3), and the tab's content below: tables, often with a "selected …" detail pane at the side.
   - **Right (about 340 px):** a **ledger** (weights, the sides' strength), **particulars**, **remarks**, and the screen's
     one **margin box** with its action.
4. **Status bar** (20 px): a state dot and a line of facts on the left (built in 948 ms, history), the keys on the right.

The rules that hold it together:

- **Panes touch.** 2 px of ground between them at most, a 1 px near-black edge (`Edge`) round each. No cards floating
  on a background, no drop shadows, no rounded corners (rounding 0; popups 1 px).
- **The screen is full.** No centred column with margins, no empty breathing room. A pane with little to say is
  smaller; it is never padded out. Long content scrolls inside its pane, never the screen.
- **Every pane has a title strip**: 20 px, a `TitleTop`→`TitleBottom` gradient, a rivet, the name in the accent's
  engraved capitals (`UiFonts.Head`), at most one muted note after it ("trim sheet", "the sides as written"), and the pane's toggles or count on
  the right.
- **Sub-headers** split a pane: 20 px on `HeaderBg`, a small uppercase label, a muted note, controls on the right.
- **Choices a screen offers once go in a table or a segmented control**, not in a row of cards. "Play as" is three
  table rows with a radio mark, not three cards.
- **One work surface per screen**, drawn as a technical drawing: `Plan` ground, `PlanLine` at low alpha for its grid,
  a ruler with its length (20 m, 5 km), labels as **tags** (mono 10 px, a dark translucent box, a 1 px `TagEdge`
  border), and its overlays as letter toggles in the title strip (`[A] Arcs`, `[C] Courses`).

## The pieces

| Piece | How it looks | Used for |
|---|---|---|
| Table | header row 18 px, uppercase 10 px muted on `HeaderBg`; rows 20 px, a `Line` rule under each, every other row 1.8 % lighter; figures right-aligned mono | nearly everything: levers, rules, conditions, particulars, ledgers |
| Group row | `Panel2`, bright semibold, a ▼ twisty, a weight-group swatch where there is one | an outliner's sections, a unit in an order of battle |
| Selected row | a warm tint and a 3 px brass bar on its left edge; its name in brass | the current section, scenario, class |
| Stepper | ◀ inset value ▶: small dark arrow boxes, the value right-aligned mono in an `Inset` well | any number the player sets: speed, belt, ships in a row, a temper |
| Drop-down | an `Inset` field and a square grey ▼ button at its end | a pick from a list: templates, crews, doctrine |
| Segmented | joined grey buttons, the chosen one `BrassDarker` with a brass border and brass text | two to four exclusive options: coverage, fight/carry on, presets |
| Filter flag | one segment on its own, brass when on | an on/off filter beside others (the design picker's sources, up to the year) |
| Button | 20 px, `Button` grey, a 1 px border, its key in a `kbd` chip after the word | actions: Change design… C, Save setup… Ctrl+S |
| Primary button | the same size, filled with the accent (`PrimaryTop`→`PrimaryBottom`), dark ink (`PrimaryInk`) | the screen's one main action (Begin); never larger than the others |
| `kbd` chip | mono 9.5 px, a 1 px border with a 2 px bottom | every key binding shown next to its control |
| Overlay toggle | a small boxed letter and a word; brass when on | what the work surface draws |
| Status chip | 10 px text in a 1 px border on a dark tint of its colour (ok, amber, plain, hostile) | a state on a row: `bold +0.20`, `in action`, `you`; hostile for the enemy and a ship going down |
| Ledger bar | a thin `Inset` well filled by colour per share, a 1 px edge | weights by group, the sides' share |
| Dock tab | a tab on a 22 px `Ground` strip: the live one brass on its tint with a brass border, the others `Panel2`; a pinned one's ✕ inside | the ship cards' live and pinned cards |
| Margin box | the one tinted box on a screen (`Ok` green on dark green, or amber), a big mono figure (`MonoBig`) and a muted line | the screen's verdict: 14,882 t to spare; Ready |
| Remarks | bullet lines in body text, a count in the title strip | what the player should know and might miss |

## Colour: what each one means

Colour says **state**, never decoration. The skin is **Gunmetal, natural map** (`docs/ui-themes/ui-style-gunmetal-natural.md`,
taken in 2026-10-26): warm graphite instruments with no blue in the chrome, one cadmium-orange accent, cream for friends,
over a plain physical atlas. The tokens are `Theme`'s (the accent keeps the old name `Brass`):

| Token | Hex | Meaning |
|---|---|---|
| `Ground` | `#0F0F0E` | between panes |
| `Panel`, `Panel2` | `#1B1B19`, `#22221F` | pane body; a group row, a tab at rest |
| `Inset` | `#0B0B0A` | wells: value fields, empty bars |
| `HeaderBg`, `Line`, `Edge` | `#141412`, `#272724`, `#050505` | column headers and title strips; the rule between rows; the edge round panes |
| `Bevel` | `#2E2E2A` | the light line inside a pane's edge: panels have a bevel, never a shadow |
| `Text`, `Bright`, `Muted`, `Faint` | `#D3CFC4`, `#F6F3EA`, `#8A867B`, `#55524B` | body; figures and names; labels and notes; disabled |
| `Brass` (the accent) | `#E8772E` | **the selection and the active**: the chosen tab, row, option, the primary action, pane titles. One thing per pane |
| `Amber` | `#E8B64A` | **a consequence, never a refusal**: over a limit, a warning (a hue apart from the accent). Nothing is ever red for "no" |
| `Ok` | `#8DBE6B` | ready, within limits |
| `Friend` | `#F1ECDD` | friend, everywhere (`MapPalette.Friend`): cream, never blue, which sinks into the sea |
| `Hostile` | `#E04A3C` | the enemy (`MapPalette.Hostile`), in figures and marks only |
| `Water`, `Arc` | `#7FB0CF`, the accent | on drawings: the waterline and liquid layers; arcs of fire and guns bearing (fills at about 6 %) |
| `Plan`, `PlanLine` | `#262724`, `#BDB8AA` | the work surface's graphite plate and its lines |
| `Group(…)`, `ArmourRamp` | see the file | weight groups' swatches and bars (the gunmetal set); armour thin to thick |

Blue belongs to the sea only: water on a drawing, the map's sea. A screen uses the accent for a handful of things and
grey for the rest. When everything is highlighted, nothing is.

## Type

Three roles, never mixed in the chrome:

- **Saira Extra Condensed** 700 for **title strips** only: `UiFonts.Head`, engraved capitals in the accent, an em of
  11.5 px with the tracking (.16 em) baked into the glyphs; write the title in capitals.
- **Barlow Condensed** for words. `Sans` 14, `SansSmall` 12, `SansBold` 13 (600), `Title` 16 (`UiFonts`, at scale 1).
  It has no ↑ ↓: Saira's are merged in.
- **Red Hat Mono** for **every figure**: right-aligned in its column, the unit after the number in the same cell
  (`279 mm`, `21.0 kn`), thousands with a comma (`25,118 t`). `MonoBig` 22 (500) for the margin box's figure only,
  `MonoSmall` 11 for a work surface's tags and the `kbd` chips.

The game's fonts are Latin subsets (`UiFonts`' ranges: Latin-1, general punctuation, ←↑→↓, −): the mockups' ▼ ◀ ▶ ▲ ✕ ○
◇ ⏎ ⇧ are no glyphs there. Draw the marks as shapes (a triangle, a cross, a circle) and write keys as words in their
chips (`Enter`, `Shift+Enter`, `Esc`).

Uppercase is for title strips, column headers and the resource bar's labels, nothing else: never a sentence, never a
button. No letter-spaced wordmarks. The poster fonts (`PosterTitle`, `PosterFigure`, `PosterText`, IBM Plex Sans Condensed) are the
ship card's (`ProfileScene`) and stay there. HTML mockups at 1920×1080 use 13 px Barlow body, 11 px Red Hat Mono, 11.5 px Saira titles, 10 px column
headers, close enough to the game to judge by.

## Words

- **Labels name the thing**: Belt, Readiness, Gun crews. No verbs on labels, no "Choose your…".
- **Notes say what drives what**, briefly and in the project's plain voice ("the side's + the admiral's + the
  captain's", "every plate above by the same share"). One note per title strip or sub-header at most. Help that would
  need a paragraph goes in a tooltip or the docs, not on the screen.
- **No sales voice**: no exclamation marks, no "Get started", no "Ready to set sail?". Begin is "Begin".
- **One word, one meaning** across the whole UI (a ladder is either the spotting ladder or the scale bench, never both).
- **Prose only where it is the content**: a scenario's brief, a remark. A brief sits in its own pane at body size, not
  under a hero title.

## Behaviour

- **Every tab has its number key, every overlay its letter, the primary action Enter**, and the status bar lists them.
  Esc closes what's open; with nothing open it opens the app menu.
- **Nothing modal** unless the action can't wait (a quit with unsaved work). Pickers open in place, over the panes
  they replace.
- **Live**: a change shows at once on the work surface, the ledger and the margin box. There is no Apply button.
- **Limits report, they never block** (amber, the margin), and every change can be undone (Ctrl+Z or ✕ on a change line).
- **Rows answer the pointer**: hover tints a row, clicking a row selects it and fills the "selected …" pane.

## The landing-page habits, and what to do instead

| Habit (don't) | Instead |
|---|---|
| A kicker in tracked gold caps over a big centred title | the name in the resource bar; the figures in its cells |
| Cards with rounded corners, shadows, gaps between them | panes docked edge to edge with title strips |
| Three cards in a row for a choice | table rows with a radio mark, or a segmented control |
| A YOU / AI / NEW pill badge on everything | a status chip only where the state matters, on its row |
| A big gold call-to-action slab | a primary button the same size as the others, in the margin box |
| A breadcrumb and a letter-spaced wordmark | the menu bar and the F-key screen tabs |
| Meta lines of dot-separated facts as prose | resource bar cells or a particulars table |
| Muted helper text in every corner | one note per title strip; the rest in tooltips |
| A dashed "+ Add …" drop zone | a brass "+ Add …" row at the end of the table |
| Generous padding, a centred column | fill the screen; size panes to their content |
| Decorative icons, emoji, gradients on content, glow | none; colour and shape only where they carry state |
| A spinner or a toast for progress | a ledger-style bar and a line in the status bar |

## Checklist for a new screen or panel

1. Which pane of the frame does it go in (outliner, work surface, tab, ledger, remarks, margin box)? A new screen keeps
   the frame: menu bar, resource bar, three columns, status bar.
2. Each pane has a title strip; each list is a table with a header row; each number is mono, right-aligned, with its unit.
3. The accent marks only the chosen and the one primary action; amber only consequences; no red for refusals; no
   blue outside water.
4. Every control is one of the pieces above, drawn by `Pieces`; a new kind of control needs a reason, and then goes in
   this table and in `Pieces`.
5. Every tab and toggle has a key, and the status bar shows it.
6. Mock it up at 1920×1080 by copying `launch-bureau.html`'s CSS. Screenshot it headless
   (`msedge --headless=new --window-size=1920,1080 --screenshot=out.png file:///…`), and set it beside a
   designer screenshot before porting.
7. Once ported, compare the game's `-screenshot=N -ui` with the mockup.

## Exceptions

- **The map's world**: sea, land, the grid, unit symbols and their labels follow `MapPalette` and
  `docs/map-ui/symbols.md`. The battle HUD's chrome around the map (the top bar, the strip, the ship card) follows
  this language.
- **The ship card's poster type** (`ProfileScene`).
- **Effects** (`docs/visuals/`): their own colours, never in the chrome.
- **The map's atlas colours** (`MapPalette.Sea`, `Land`, `Coast`, `Grid`, `Label`) are the natural map's, for a map
  drawn in the chrome (the launch screen's opening); the globe's chart takes its sea and land in `globe.frag`, lit by
  the sun.
