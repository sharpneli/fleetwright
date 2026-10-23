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

**Y4. The symbology.** Today's shapes stay (decision 1), each reviewed against the clarity rules below ("Shape
review"): a shape changes only where it says something wrong or reads poorly. `MapPalette` takes every colour.

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
the radial menu. They build on Y3's symbols and Y5's placer.

## Decisions (the user, 2026-10-23)

1. **Today's shapes are fine**, to iterate on later; each gets a thought on whether the mockup's version or another
   reads better ("Shape review").
2. **Halo or pill**: left to the build (names haloed, numbers in the pill); iterated if it doesn't feel right in play.
3. **The ships' icons** are in this pass: a casing and a glow for the selected (Y3).
4. **Order**: Y0, Y1, then Y2 to Y5, Y7 and Y8 in turn; **Y6 only if playtesting shows it's needed**.

## Shape review (Y4)

The rules: one visual variable says one thing (shape whose and what, line style how sure, colour whose and what
state, size how much); shapes that mean different things differ in outline, not only in colour or size (a colour-blind
player, a dim screen, a sea under smoke); a symbol the eye follows over the sea (a thing that moves) is filled, a
symbol for an area or a reach is a thin line; direction is an arrowhead, not a line that stops.

| Symbol | Today | Verdict |
|---|---|---|
| Own squadron or force (collapsed) | diamond, 9 / 13 px, cream | **circle**: the diamond is the enemy contact's shape, and the rail already marks our ships with circles and the enemy with diamonds; a force a double circle |
| Enemy contact | diamond, 5 px, enemy red | **keep the diamond**, filled while in sight, hollow once dead reckoned (sure or reckoned at a glance, where today only the alpha changes); unknown side keeps its own colour |
| Contact's 2σ ellipse | thin line | keep: an area, so a thin line |
| Smoke on the horizon | soft smudge | keep: smoke has no outline |
| Encounter | solid ring | keep the solid ring (an action's bound matters more than a reach), thinner, with casing |
| Torpedo reach | solid rings | **dotted**: a reach, not a thing; dots keep it apart from the encounter's and the selection's solid rings |
| Selection | solid brass ring | keep, with casing and Y7's ease |
| Picked, held, asked | rings at their own radii | keep |
| Waypoint | 4 px hollow ring | **filled dot** with casing: a 4 px hollow ring reads as a smudge |
| Ordered course | a short line | **arrowhead** at its end |
| Captain's follow / run line | thin line | **dotted**: a relation, not a path or a shot (the fire lines are solid) |
| Fire lines | solid, faint unless firing | keep: dashing dozens of them at the Jutland is busier, not clearer |
| Routes | solid line | keep |
| Wreck | circle with a cross | keep (read everywhere as "lost"), drawn crisp |
| Ship icons | hull plan in the side's tint | keep, with a dark casing so they read over smoke and land; the selected's glow (decision 3) |

## Built

**Y0** (2026-10-23): `SymbolLook` (`-symbols=chord,width,casing,casingalpha,labelfade,halo,motion`). The baseline,
DevRelease, 1500 frames at 30x from the scenario's own camera, `-frametimes -gputime`:

| | Frame mean / p99 | CPU mean | GPU mean | Overlay CPU | Labels CPU | UI bytes a frame |
|---|---|---|---|---|---|---|
| Jutland | 8.55 / 11.4 ms | 1.98 ms | 1.27 ms | 0.230 ms | 0.041 ms | 44 |
| Squadrons | 8.42 / 9.1 ms | 2.43 ms | 2.12 ms | 0.072 ms | 0.018 ms | 44 |

**Y1** (2026-10-23): `MapLines.Segments` (chord error under `SymbolLook.Chord`, 0.25 reference px: about π√(r/2ε)
pieces), `MapLines.Circle` (only the arc on the screen; nothing for a ring off it or round all of it),
`MapLines.ClipPoint` (the routes' screen clip, moved in from the scene: one for every curve), `EndClosed` (a ring's
seam joined like its other corners: the diamonds' tops were butt-ended). Every ring goes through them: the encounter,
torpedo reach, the dev's horizon and land rings, the contacts' ellipses, the selection, waypoints, held and picked,
the wreck, and the battle marks' foam, column, going-down and shock rings. The overlay's widths and the symbols' sizes
are in reference px (`SymbolLook.Px`: the target's height over 1080 times the UI's factor); the battle marks' own
pixel sizes are left as they were. Round caps and joins were not needed: no stroke on the map is over 2 px.

**Y2** (2026-10-23): `LineVertex` grew to 56 bytes (the distance along the strip, the casing's width, a style: dash
period, share, casing alpha); `mapline.frag` draws dashes (anti-aliased at their ends), round dots as wide as the
line, and the casing (the ground's dark, `SymbolLook.CasingAlpha` of the stroke's) under the stroke; the baked coast's
vertex shader passes a plain style. `MapLines.BeginStrip(colour, width, Dash, cased)`; a ring's points carry their
distance along it (radius times angle) and its dashes are fitted to a whole number, so a ring cut by the screen's
edge keeps its dashes where they were as it pans. The line buffers grew with the vertex (the same vertex counts).
The torpedo reach went dotted and dashed (Y4 sets them), the encounter ring cased.

**Y3** (2026-10-23): `MapSymbols` (`mapsymbol.vert/frag`): one quad a symbol, its shape a signed distance in px
(circle, diamond, square, arrowhead, wreck: a ring and a cross), filled, stroked and cased, drawn after the overlay's
lines in the same pass (a pipeline and a 2 MB buffer, no new pass or target). The contacts' and groups' diamonds,
the selection, held and picked rings, the waypoints and the wreck moved onto it; the battle marks' rings stay soft
strokes. The icons (`shipmap.frag`, mode 2): the quad padded by the casing and the selected's glow, the hull's
coverage spread by eight taps into a dark rim (1.5 casing px) and, selected, a soft glow in its tint (two rings of
taps, 4 px); the texture's end columns aren't empty, so the hull is masked to its own rectangle (clamping smeared the
stern into the padding at first). Packed into fields the icon didn't use: roof and cover carry the padding's share,
soot the casing's px (plus 100 selected).

**Y4** (2026-10-23): `MapPalette` holds every symbol's and stroke's colour (the battle marks' and effects' stay with
them); values kept, but for the near-duplicates snapped to the tokens: routes to brass, the own encounter to amber,
the fire lines to brass, friend and hostile. The shape review's changes: our squadrons a filled circle (a force
ringed again), contacts filled while in sight and hollow once reckoned, torpedo reach dotted (both settings), the
captain's follow line dotted, waypoints filled dots, the ordered course cased with an arrowhead, the encounter ring
thinner (1.6 / 1.2 px, cased). One change beyond the review: the selected ship's land rings and feeler (a rails aid)
show only with the dev panel (F1) open; they were the green rings round every selection.
