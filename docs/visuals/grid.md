# The map's grid: one ladder from the globe to the battle (2026-10-23)

The table's grid (`Map/Graticule.cs`) was a lat/lon graticule at one spacing at a time: the finest of 15 steps (30°,
10°, 5°, 2°, 1°, 30', 10', 5', 2', 1', 30", 10", 5", 2", 1") whose lines are 90 px apart. Every change swapped the
whole grid in a frame, and the ratio between steps wandered (3, 2, 2.5, 2, ...), so a zoom read as a run of
unrelated grids. This pass makes zooming one continuous story, and gives the battle's zooms a true square grid for
judging distance by eye. It is a visual aid only: nothing in the sim reads it.

## The two grids

**The graticule** (zoomed out): parallels and meridians on a ladder that is 10:1 but for one step, in degrees and
decimal minutes, the navigator's own notation (56°42.3'N):

| Level | Ratio | North-south |
|---|---|---|
| 90° | | 5,400 nm (the equator, the prime meridian and the quarters) |
| 10° | ×9 | 600 nm |
| 1° | ×10 | 60 nm |
| 10' | ×6 | 10 nm |
| 1' | ×10 | 1 nm |
| 0.1' | ×10 | 1 cable |
| 0.01' | ×10 | 0.1 cable |

A minute of latitude is a nautical mile, so every line is a round position and a round distance north-south.
East-west a level's cells narrow by cos(latitude); meridians are faded by their own spacing (the camera's latitude),
so near the poles they thin out a level before the parallels do.

**The square grid** (the battle's zooms): straight lines a round distance apart, square to the eye, on a gnomonic
plane (`p = normalize(R·A + x·east + y·north)`) touching the globe at an anchor point A, north up at A. Its ladder is
exactly 10:1 in the map's unit: 1000, 100, 10, 1, 0.1, 0.01 nm (km later: one setting, `MapUnits`). Lines are great
circles, the "straight line on the sea". Within 100 nm of the anchor the grid's scale is off by under 0.3%; its north
turns from true north by Δlon·sin(lat) (about 0.8° per degree of longitude at 56°N).

## One look for every level

Levels nest (every 10' line is a 1' line), so each line's look is a smooth function of how far apart its **own**
level's lines are on the screen, `p`, in reference pixels (pixels at a 1080-tall window, as `WorldView` measures), a
line taking its coarsest level's:

- `p` < 8: not drawn;
- 8 to 30: fades in to the minor look (thin, faint);
- 30 to 120: minor;
- 120 to 500: grows to the major look;
- over 500: major.

At any zoom that is one major level, one minor level inside it and perhaps the next one arriving faintly. Zooming
in, the minor grid takes over the major look as the major cells grow off the screen. Nothing switches; the ×6 and ×9
steps just fade a little faster. The equator and the prime meridian keep a little extra weight. The numbers are
knobs (`GridLook`, `-grid=`), set by eye.

## From one grid to the other: lat/lon goes to the margin

As on a military map (a square grid in the field, the graticule in the margin):

- **Zoomed out** (at and above 300 m per reference pixel, about 300 nm across a 1080p screen): the graticule.
- **The blend band** (300 down to 100 m/ref px, within the Operations band): the graticule's lines fade out, the
  square grid fades in, and the edge ticks and labels (`Graticule.BorderTicks`, until now the world view's) fade in.
  Anchored on a whole-degree node, the 10 nm rows lie on the 10' parallels near the anchor (within ~80 m over a
  degree), so the rows mostly hold still and the columns change spacing.
- **Zoomed in** (below 100 m/ref px, the lower Operations band, Tactical): squares in the field, lat/lon at the
  margin. The world view keeps its rule (`sea.md` W0): the field's grid fades out with the world factor, the
  margin stays.

The margin's ticks follow the same levels: a tick's length and alpha come from its level's `p`, the labels only on
major ticks, faded with them, and as on a chart latitudes at the sides and longitudes at the top and bottom.

## The anchor (the global game has no fixed origin)

The anchor is the whole-degree node (latitude clamped to ±89°) nearest the camera:

- **While the square grid is faded out** it follows the camera freely: unseen.
- **While it shows**, it keeps its anchor until the grid's north is more than 1° off the view's (Δlon·sin(lat)) or
  the view's centre is over 100 nm away; then the new anchor fades in over the old in 0.4 s of real time (a display
  clock: the grid isn't the sim). A pan past another limit mid-fade drops the old anchor at once.

So the lines hold still on the sea; at battle zooms a re-anchor takes a long east-west pan (about 40 nm at 56°N).

## The legend

The "where" panel (bottom right) gets the grid's levels above the scale bar: a stroke in each level's look and its
spacing, the graticule's as angle and its north-south distance (`━ 1° · 60 nm`, `─ 10' · 10 nm`), the squares' as
distance (`━ 10 nm`, `─ 1 nm`), each faded with its grid. The two families share the two rows and hand over in the
middle of the blend band (one out by 0.5, the other in from 0.5), so their texts never overprint and the panel holds
its size. The scale bar and the dev line's distances move to `MapUnits` too.

## Steps

- **G0. The graticule's levels and the units.** The ladder and the look curve; meridians by their own spacing; the
  edge ticks by level (major labelled, every crossing found, not one per sample); `MapUnits` (nm, cables) for the
  scale bar and the legend; the legend for the graticule. Tests: no level's alpha jumps between zooms 2% apart, the
  build allocates nothing, ticks at multiples of their level. Captures at a zoom sweep.
- **G1. The square grid.** `SquareGrid`: the gnomonic plane, its ladder and look, the anchor and its fade; the blend
  band (squares in, graticule out, margin in); the legend's second family. Tests: the anchor rule (free while
  unseen, kept within the limits, a fade past them), rows on the 10' parallels at the anchor, no alpha jumps across
  the band, no allocation. Captures across the band and at battle zooms, at 56°N and near the equator.
- **G2. Cost and knobs.** Lines and vertices per build and the build's time in DevRelease across a zoom sweep (the
  old grid's for comparison); the band's two ends and the anchor limits live in the F2 "where" panel; then tuning
  by the user's eye.

## How it went

**G0 done 2026-10-23.** `Graticule` on the ladder, `GridLook` (the look curve, `-grid=`), `MapUnits` (nm and cables;
the scale bar reads in it), the legend, the edge ticks by level with decimal-minute labels. Found on the way: the
"where" panel rounded the degrees (56.7° read 57°42'), fixed.

**G1 done 2026-10-23.** `SquareGrid`: the gnomonic plane at a whole-degree anchor, the anchor rule, the blend band, the
margin from the band on, the legend's handover. Tests: the anchor (free, held, faded past 1° or 100 nm), rows within
100 m of the 10' parallels out to a degree, smooth takeover, no allocation. At the equator the squares fall on the
meridians as well.

**G2 done 2026-10-23.** The grid's lines are rebuilt only when the view (or a knob, `GridLook.Version`) changes, or
while an anchor fades. A rebuild's cost against the old grid (Release, 1920x1080, 56.7°N, the lines, squares and
ticks together), with lines fainter than 1/150 alpha skipped and chords within half a pixel:

| m/px | old µs | old vertices | new µs | new vertices |
|---|---|---|---|---|
| 8000 (the globe) | 56 | 9,840 | 385 | 68,928 |
| 3556 | 118 | 23,628 | 421 | 76,878 |
| 1053 | 50 | 8,898 | 175 | 31,314 |
| 312 (band's top) | 30 | 4,824 | 30 | 4,014 |
| 139 (in the band) | 38 | 6,312 | 203 | 11,184 |
| 62 (battle) | 34 | 5,568 | 108 | 3,504 |
| 12 | 32 | 5,448 | 105 | 5,238 |
| 2.4 (world view) | 38 | 432 | 48 | 720 |

The dearest views are near the whole globe, where the 1° level is a true minor grid now (~20 px apart over a
hemisphere): about 0.4 ms a rebuild, only while the camera moves. Battle zooms stay near 0.1 ms. If it shows in a
frame's budget, the faint levels can take coarser chords (a minor line at 1 px of sag).

The knobs are live in the F2 "where" panel (the dev panel's line): the blend band, the alphas, the fades' four
spacings, the anchor limits. Next: tuning by the user's eye.
