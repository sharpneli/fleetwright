# The map's symbols: from placeholder strokes to a finished look (2026-10-23)

The battle HUD (`battle-hud.md`, U0-U7) gave the map its panels; what it draws on the sea itself is still the dev
layer's: every ring, diamond and line is a polyline of a fixed number of points pushed through `MapLines`, every
label is ImGui text with a one-pixel drop shadow, placed on its own. Zoomed in, the encounter ring is a 32-sided
polygon with 50 px straight sides (`png/symbols-before-encounter.png`); at the Jutland the squadrons' diamonds and
labels pile into an unreadable knot (`png/symbols-before-jutland.png`). This plan makes the map's symbols the
finished product's: smooth at any zoom, one consistent symbology (the mockup's, `README.md` "Zoom bands" and
`battle.html`), labels that never pile, and a little motion where it helps the eye. The battle marks
(`docs/visuals/battle-marks.md`: shells, splashes, foam rings on the table) were approved as they are and only get the
shared fixes (Y1).

A word for this plan: **symbols** are the drawn shapes (rings, diamonds, waypoints), **strokes** the lines (routes,
fire lines, ellipses), **labels** the text. "Marks" stays the battle marks' word.

## What is drawn today

| What | Where | Drawn as | What's wrong |
|---|---|---|---|
| Encounter ring | `MapScene.Encounters.cs` (`RingM`) | 32 points round a circle on the sphere, amber 2 px / grey 1.5 px | a polygon at every battle zoom; solid where the mockup has a dashed ring |
| Encounter label | same | text, 1 px shadow, at the ring's top | no placement; shadow doubles the text on light ground |
| Selection ring | `MapScene.Ships.cs` (`Ring`, px) | 24 points, brass 2 px | pops on and off; no casing, lost over the brass fire line |
| Torpedo reach | `MapScene.Ships.cs` (`RingM`) | two 32-point rings, 1 px | faceted when large; the HUD plan says dashed, drawn solid |
| Routes and waypoints | `MapScene.Ships.cs` | great-circle strips, 4 px rings of 24 points | no direction; a waypoint is a tiny ring, no order |
| Course ordered, captain's target | `MapScene.Ships.cs` | a straight 1.5 px / 1 px line | no arrowhead: reads as a stray line |
| Contacts | `MapScene.Contacts.cs` | 2σ ellipse of 32 points; a diamond as a 4-point strip; course line; smoke smudge disc | ellipse faceted when large; "out of sight" only fades, the mockup dashes it; labels one per cell, no fallback place |
| Group markers | `MapScene.Groups.cs` | a diamond strip, 9 or 13 px | our own squadrons in the hostile diamond (the mockup: circle friend); no count in the symbol; pile at the Jutland |
| Held, asked, picked | `MapScene.Groups.cs` (`Ring`) | 24-point rings round the icon | fine shape, no casing; colours ad hoc |
| Fire lines | `MapScene.Shells.cs` (`DrawTargets`) | 9-point strips | others' solid where the mockup dashes them |
| Range on the fire line | `MapScene.Marks.cs` | a dark pill with brass mono text | good: the model for numbers on the map |
| Ship labels | `MapScene.Marks.cs` | name, speed, flotation bar; one per screen cell | a placer of its own: overlaps the group labels and the contacts' |
| Wrecks | `MapScene.Damage.cs` | a 12-point circle and two strokes; text label | a dodecagon; label not placed |
| Going down, magazine shock, splash foam, torpedo column rings | `MapScene.Damage.cs`, `MapScene.Shells.cs` | 8-20 point rings | battle marks: only the shared tessellation (Y1) |

Shared faults: segment counts fixed, never by size on screen; widths in the target's pixels (a 4K window draws the
1080p weights at half their heft); butt ends and mitred joins only; colours declared per file (eleven in
`MapScene.Ships.cs` alone) instead of the design tokens (`README.md` "Design tokens"); no casing, so a 1 px line
vanishes over smoke, land or a brass fire line; everything appears and vanishes in a frame.

## The finished look

- **Smooth at any zoom.** A curve is split by its chord error on the screen (under a quarter of a reference pixel),
  not a fixed count, and only the arc on the screen is built: a 2,000 px encounter ring costs what a 300 px one does.
- **Strokes have styles.** Solid, dashed, dotted, in reference pixels, with a dark casing under the stroke (the
  cartographer's halo) so a line reads on any ground. Dashes anchored along the stroke so they don't crawl.
- **Symbols are shapes, not polylines.** An analytic symbol batch: one quad a symbol, its shape a signed distance in
  the fragment shader (circle, diamond, square, ring, crossed circle, chevron, arrowhead, pip), fill, stroke and
  casing, crisp at any size and pixel-exact at every corner.
- **One symbology**, the mockup's (`README.md` "Rules that hold across bands"): frame is affiliation (circle friend,
  diamond hostile, square neutral), line is certainty (dashed for suspect or out of sight, faded with age); a group's
  symbol holds its ship count; strength pips under its label; fixed modifier slots for later (damage, orders in
  transit).
- **One palette** (`MapPalette`): the design tokens (brass, amber, friend, hostile, neutral, unknown, ground) and a
  few derived tints, used by every symbol, stroke and label; no colour literal left in the scene's files.
- **Labels never pile** (`README.md` again): one placer for all of them, by priority, each trying right, left, above,
  below and dropped (still on hover) when nothing fits; a halo instead of the offset shadow; numbers in the dark pill.
- **A little motion, on the display clock**: the selection ring settles in, the symbol under the cursor lifts, an
  encounter's ring fades in when it opens; nothing tied to the sim's speed (the same at any compression), nothing that
  changes what the picture says.

## Steps

**Y0. The baseline.** A fixed capture set from the command line (Squadrons at the encounter and at the selection,
the Jutland at operations zoom, contacts with smoke, a wreck), the `png/symbols-before-*.png` and a few more;
`-frametimes`, `-gputime` and the overlay's vertex count at the Jutland and Squadrons, the bytes a frame. `SymbolLook`
with `-symbols=` knobs (as `GridLook`'s `-grid=`) for side-by-side captures in every later step.

**Y1. Smooth curves, real widths.** One tessellator for every circle, ellipse and arc, on the sphere and on the
screen: segments by chord error, clipped to the screen plus a margin, a budget so a bad frame stays bounded; it
replaces `RingM`, both `Ring`s, the contacts' ellipse loop and the wreck's circle (one implementation). Fire lines
and courses split by length on the screen. Widths in reference pixels (`WorldView.ReferenceHeightPx`); round caps and
round joins for strokes over 2 px. Fixes the encounter polygon on its own: the smallest step and the most visible.

**Y2. Stroke styles.** `LineVertex` carries the distance along the stroke and a casing width; `mapline.frag` draws
dashes and dots from it and the casing under the stroke, one draw still. `MapLines` gets a style per strip (solid,
dash, dot; casing on or off) and a soft fill for areas (the ellipse's faint inside, if Y4 wants it).

**Y3. The symbol batch.** `MapSymbols`: instanced quads in the overlay's pass (a new pipeline, no new pass, target or
texture), shapes by signed distance, fill, stroke and casing per instance; built per frame into kept arrays, no
garbage. Picking keeps reading the same positions and sizes. Replaces the diamond strips, the pixel rings and the
wreck's strokes; gives routes and course lines arrowheads.

**Y4. The symbology.** The mockup's on every symbol and stroke: own groups in a filled circle with their count, the
enemy's contacts in a diamond, unknowns in a square, dashed when out of sight or only suspected; the encounter as the
mockup's double ring (solid thin outer, dashed inner); fire lines solid brass for the selection, dashed and faint
for the rest; torpedo reach dashed with its label on the ring; routes dashed ahead of the ship with numbered
waypoints, course lines with an arrowhead; strength pips under group labels. `MapPalette` takes every colour.

**Y5. One label placer.** Every label on the map (ships, groups, contacts, encounters, wrecks, the range pill,
torpedo reach) goes through one placer: priorities (the selection, then own ships, enemy in sight, groups,
encounters, wrecks), four candidate places tried against one occupancy grid of the screen (linear in labels, no
pairs), hysteresis so a label keeps its place while it fits (no jumping between frames), a fade in and out over
~150 ms instead of popping. A halo (the text drawn under itself at eight one-pixel offsets in the ground's colour) in
place of the drop shadow; squadron labels in the mockup's tracked capitals. Written with `U8`, no garbage.

**Y6. Piles.** Where symbols overlap (the Jutland's squadrons forming up, a flotilla over its leader), they stack:
within a symbol's size, one symbol with the sum and the labels fanned on hover, or a neat offset column; picked by
captures in this step. Zooming in always splits a stack into what it was made of (`README.md` "The idea").

**Y7. Motion.** Display-clock eases, at most ~200 ms: the selection ring grows in and settles, the hovered symbol
brightens its casing, an encounter's ring fades in on opening and out on closing, the "!" of an asked group pulses
slowly. Off at `-symbols=motion:0` for captures.

**Y8. Cost and review.** The Y0 set again beside the baseline and the mockup's `png/battle-*.png`; frame and GPU
times at the Jutland and Squadrons against Y0, the bytes a frame unchanged; tuning by the user's eye with the
`-symbols=` knobs.

## Rules this keeps

- No garbage a frame (a game: soft real time): kept arrays, `U8` text, no closures in the draw.
- Nothing O(n²): the placer and the stacking use a screen grid.
- The sim never reads any of it; symbols follow the player's picture and never claim more than it holds (a dashed
  contact is one the picture is unsure of).
- No hard switches: segment counts, fades and stacking follow continuous quantities (size on screen, overlap).
- One implementation: the tessellator and the placer replace the per-file loops and grids, nothing kept beside them.

## Not in this plan

The rest of 5.2 (`battle-hud.md` "Not done"): edge markers for what's off the screen, orders as editable objects,
the radial menu. They build on Y3's symbols and Y5's placer. The ships' own icons (`ShipBatchRenderer`'s stand-in
hull) stay as they are unless the user asks (question 3).

## Questions for the user

1. **The symbology**: adopt the mockup's (circle friend, diamond hostile, dashed for suspect; our squadrons change
   from diamonds to circles) as Y4 says, or keep today's shapes and only make them smooth?
2. **Label halo or pill**: the plan halos names and keeps the dark pill for numbers (range, distances). A pill for
   everything reads stronger over busy ground but covers more sea.
3. **The ships' icons**: give them a casing and a selected glow in this pass (Y3), or leave them to a later pass?
4. **Order**: Y1 first (the jagged rings gone in one short step), then Y2 to Y8 in turn; Y6 could wait for the
   user's look at Y5 if the placer already tames the Jutland.
