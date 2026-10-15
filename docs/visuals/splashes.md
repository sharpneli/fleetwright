# Shell splashes in the world view (plan 2026-10-15; P0-P5 done the same day)

Research: `research/vfx/shell-splashes.md` (anatomy, sizes and timings by calibre, angle of fall, dye, the LOD tiers),
`docs/visuals/shell-impacts.md` (outcomes, the top-down reads). The table already has its splash marks
(`docs/visuals/battle-marks.md`: a pop and a foam ring at a pixel size). This plan covers the **world view** only, where
ships are sprites at their true size and the sea is the FFT water. The table's marks stay as they are.

Scope: a shell that **misses**, from a far miss to a near miss alongside, and nothing on the ships (decision 6). Hits
(flash, smoke, fire), near misses' effects on a ship (wet decks, the hull's shudder) and a torpedo hit's column belong
to the later hit-effects pass.

## How the research changes for this camera

The research assumes a 3/4 RTS camera, because from straight above a 75 m column collapses into a white dot. Ours
looks **straight down** (`MapCamera`: north up, a 40° vertical FOV, perspective), and the map stays that way. Tall
effects get a 3/4 view of their own instead.

### The effects camera (decision 1)

Splashes, and later explosions and other tall effects, may break realism for the look: they don't affect results. So
the **rising column alone** is drawn as if seen from a **3/4 view looking north**, inside the top-down map. The
projection is oblique:

- **The ground is exact.** Everything on the water stays where the true camera puts it, untilted: the splash's base,
  its foam and base surge (in the wake target, as the wakes' foam is), its dye stain and its shadow. They register
  with the sea and the ships.
- **Height goes up the screen.** A point h metres up is drawn h·tan φ further up the screen, where φ is the effects
  pitch (45°: one metre up is one metre on the screen). Every column stands up the screen, wherever it is on it. The
  true camera's outward lean at the screen's edges is dropped for effects.
- **One view direction.** The march and the lighting see each column along one parallel direction, from the south
  and φ above the horizontal. Seen that way the shaft, the crown and the curtain all show, at any place on the screen.
- **One fixed pitch** (decision 2), the same at every zoom, so a column's shape doesn't change as you zoom. It is a
  named constant beside `WorldView` (`EffectPitchDeg`, with a `-splash=pitch:` knob), chosen from P1's captures.
  Every tall effect to come uses the same one, so the convention is learned once.
- **What it buys.** A 12" column (55 m) is 55 px tall at 1 m/px and still 14 px at 4 m/px, where seen from above it
  would be a 3 px dot. That is the research's readability without moving the map's camera.

### The rest

- **The shadow is true.** It is cast by the column's real 3D shape under the real sun onto the sea, so it is a second
  height cue beside the column, and the one that agrees with the ships' shadows.
- **Ordering is a 3/4 view's.** A column covers what is north of it (further from the effects camera) and never what
  is south of it, so drawing all columns after the ships is right. The columns themselves are drawn north to south
  (back to front), sorted on the CPU without allocation. They draw before the gun smoke and the magazine explosions
  (user, 2026-10-15): a ship blows up while being fired on, and the splashes round her belong behind the boom, not
  through it. Every effect's shadow goes down first, before any column or cloud.
- **Overdraw is moderate.** A column covers a strip: its crown's width by its height up the screen. At 1 m/px a 12"
  is ~20 × 55 px; at 0.25 m/px ~80 × 220 px. A few hundred in view is a few hundred thousand pixels, so there is
  **no half-resolution buffer**, which would also have been a new target and an upsample pass (against `sea.md`'s
  rule). If the cost measurement says otherwise, it comes back as a named exception.
- **No tier buckets.** In the world view, zoom runs from 0.1 to ~6 m/px, so one shader can carry a continuous LOD. Noise
  octaves fade as their cells near a pixel, as the foam lane's do, and the march takes fewer steps as the column gets
  smaller. The research's T0 (stain) and T1 (pillar) are the table's marks and the floor below. No compute classify, no
  indirect draws.
- **Placement is CPU, per frame.** A GPU ring of absolute positions doesn't fit the globe: everything is placed
  eye-relative in double on the CPU (`MapView`). Each frame the splashes in view become instances in a grown array, as
  the ship quads do. That's a loop over at most a few thousand records, with no allocation. The animation itself
  stays a pure function of age in the shaders.

## The look, by what draws it

1. **The water.** Splashes stamp into the **wake target** in its pre-pass, flat on the sea:
   - **foam** (max-blend, the lane's encoding: aerated water 0-0.4, surface foam 0.4-1): the base surge ring, then a
     white disc of ~1.5 widths that ages into an aerated patch over 30-60 s;
   - later, **ring waves**: an expanding dispersive ring, added into the slopes.

   `seaShade` lights both as it lights the wakes, so this adds no new target and no new shading.
2. **The dye stain** (dyed shells only). The wake target has no channel for a colour, so the stain is its own
   instanced draw in the main pass, after the sea and before the ships: a soft, noise-broken disc over the foam's
   footprint that tints the shaded water toward the dye, fading over 30-60 s. With no dyed shells it draws nothing.
3. **The shadow.** One quad per splash, laid on the sea down-sun, drawn after the ships and before the columns
   (with the smoke's and the magazine explosions' shadows). Per
   pixel it marches the sun's ray through the same density as the column, and darkens the sea and the decks under it,
   as the ships' shadows darken by opacity. It follows the stronger light (the moon at night), as the ships' do.
4. **The column**, through the effects camera. One instanced, premultiplied-alpha draw after the ships' layer and
   before the gun smoke and the magazine explosions, north to south. A near miss south of a ship stands over her hull and deck; one north of her stands clear. Each splash is a
   quad from its base up the screen by its height × tan φ, as wide as its widest part. Per pixel:
   - the effects camera's parallel ray is marched through an analytic density (4-16 steps by screen size);
   - the density has a radius profile r(z, τ): the impact crown's thin cone, the shaft about 25 × calibre wide, and the
     top blossoming from 0.5 to 2 widths over τ 0.8-1.6;
   - its height is h(τ) = H·α(θ)·(1 − (1 − min(τ, 1))²), with H = 180 × calibre (the table's `ColumnPerM`);
   - it leans along the shell's travel at a shallow angle of fall (a real lean, in the world, which the oblique
     projection then draws like any other part);
   - 3D noise in world space, seeded per splash, gives the ragged edges and the falling curtain;
   - a dyed shell tints the upper column and the crown 30-50% toward its dye, more as the column thins; the base and
     the falling curtain stay mostly white.

   The column is lit by the shared lights (`LightUniforms`) with a high albedo, a wrap diffuse and a forward-scatter
   lobe, so a column against the sun glows at its rim and reads as water, not smoke. It goes through the ships'
   shoulder, display-referred. **One implementation:** the density and the march live in
   `Include/splash.glsl`, and the shadow and the column both include it.
5. **The cross-fade.** The table's splash marks are drawn × (1 − `WorldView.Factor`) and the world's × the factor,
   as with the track line and the foam lane. The column has a size floor: under ~2 px wide it is drawn at 2 px with its
   alpha scaled down by the same factor, so it fades with altitude and never pops.

## Dye (decision 3: built now, ready for the future)

Dye loads came in around 1930 (USN), so a 1916 fight is white. The whole chain is built now so a later cut only has
to hand out dyes:

- **A sim fact.** A ship's dye is a palette index (a byte, 0 for none) on the ship, and each shell carries its
  shooter's dye from launch onto its splash, so a ship sunk while her shells fly still colours them. It belongs in the
  sim, not the map: dye existed for spotting one's own fall of shot, and fire control may read it one day.
- **Who gets which.** For now the scenario file's per-ship `dye` and a dev flag (`-splash=dye:auto`, unique within each
  group, for captures). Navy doctrine, the era and "unique within a division" come with that later cut.
- **The colours are the display's.** A palette of ~7 dyes plus none (orange, red, pink, yellow, green, blue, black,
  after the research's table), checked for colour blindness, with a vivid toggle. Not neon: tinted water, not paint.
- **The table too.** Where a shooter has a dye, the table's foam ring takes it in place of the side's tint
  (battle-marks.md's "per-ship dye colours" left for later).

## Time compression (decision 4)

At 60x a 12" column's 8 s would be 0.13 s real: a blink. Stretching every column to a fixed real-time floor instead
piles them up, since splashes arrive 60 times as fast. The column gets a clock of its own, display only:

- **A sublinear playback rate.** The column plays at a rate r(s) of the clock's speed s: r = s up to 2x, then
  2·(s/2)^0.5. At 60x that's 11x: a 12" column is 0.73 s on the screen, a 5" 0.45 s; at 240x 22x (0.37 s). Its age is
  the world's age since impact × r/s, so it stays a pure function of the splash's own impact time.
- **Thinning, so the screen doesn't fill.** At s > 2, a splash keeps its column with share k·r/s (k = 2: at most twice
  the live columns of 1x; at 60x a 37% share). The choice is a hash of the **salvo**, so a salvo shows whole or not at
  all, and the selected ship's salvos always show. A salvo near the threshold fades rather than pops. Every splash
  keeps its foam and dye stain, in world time, so the fall of shot is complete on the water.
- **Speed changes don't jump.** Both clocks are accumulated frame by frame, and the share and the foam's strength
  are decided once, when a splash is first seen, so + and − never make a live column jump in age or pop in or out.
- **The foam's clock is the sea's** (the world's speed, capped at 8x; the user's note). Above the cap the foam outlives
  the world's time, so more patches lie on the water at once. Each is fainter by √(8/speed), at least 0.3.
- **The coarse tier shows none.** Above 240x there are no shells to splash; nothing is drawn.
- **Knobs:** the knee (2x), the exponent (0.5) and k (2), in `-splash=`.

## Ready for the salvo's ripple (decision 5)

The ripple (tens of ms between a salvo's guns, and the flight times' jitter) comes later with the fire-control and
firing rework. Nothing here assumes a salvo lands together:

- each splash animates from its own impact time (`Ms`), and the frame's interpolated time (`DrawMs`) shows a ripple
  of tens of ms at 1x;
- the thinning keys on the salvo's id, which the ripple doesn't change;
- the instance build makes no assumption about the snapshot's order beyond what it already sorts.

A test: splashes of one salvo landing apart animate apart, and are kept or dropped together.

## Restrictions

- **Display only.** The sim never reads a splash's drawing. The sim does gain a few facts on its splash record (P0):
  the angle of fall, the bearing and the shell's kind it already has at impact, and the dye.
- **Realistic, then tune:** the research's heights and timings first. The departures are named knobs in a `-splash=`
  flag, each recorded here: the effects camera's pitch (approved), the column's clock under compression (approved),
  the size floor, any exaggerated height or shadow.
- **Lightweight, measured:** columns, shadows, foam and stains together under 0.2 ms GPU at 1080p with ~300 live
  columns in view (`-gputime`). The CPU's instance build is under 0.05 ms. Nothing is allocated per frame.
- **Pipeline:** no new pass and no new target. The foam goes into the wake pre-pass; the stains, the shadows and the
  columns into the main pass (stains before the ships, the rest after).

## Steps

- **P0. The record.** `ShellSystem.Splash` gains the angle of fall and the travel bearing (from the ballistic table at
  `EndS`), the shell's kind (`ShellKind`) and its dye. `SplashesView` carries them and the salvo. A ship's dye, the
  scenario's `dye` field and the dev flag. The map keeps its own fixed ring of splashes it has seen, so foam and
  stains outlive the snapshot's 20 s (as torpedo tracks already do). Tests: the angle of fall against the table at a
  few ranges; a sunk shooter's shells keep their dye.
- **P1. The column.** `splash.glsl`, the instance build in the ships' layer, the sort and the draw. Captures (the
  dev's salvo "at" a point, `-screenshot`) at 0.25, 1 and 4 m/px, at noon, under a low sun and at night, at the
  screen's centre and edges, and the pitch at 30°, 45° and 60° side by side to choose it.
- **P2. The shadow.**
- **P3. The foam** in the wake target: the base surge, then the lingering disc.
- **P4. Dye:** the palette, the column's tint, the stain, the table's ring; captures with `-splash=dye:auto`.
- **P5. Time compression and the cross-fade:** the column's clock, the thinning and the easing, checked live at 1x,
  10x, 60x and 240x; the floor, zooming through the boundary in a live fight.
- **P6. Tuning by eye** against period photos, and the cost in a Jutland-sized exchange at 1x and 60x (`-gputime`,
  DevRelease `-frametimes`).

### Done (2026-10-15)

**P0-P5 built**; P6, tuning by the user's eye, is next.

Where it lives:
- **The sim:** `Dye` (Rows.cs), a ship's dye (`ShipStore`, `ShipSystem.Dye`), a scenario's `"dye"`/`"dyes"`. Each shell
  takes its shooter's dye at launch. `ShellSystem.Splash` and `SplashesView` carry the salvo, the angle of fall, the
  bearing, the kind and the dye.
- **The map:** `Map/Splashes/`:
  - `SplashBook`: the splashes seen, the two clocks, the thinning;
  - `SplashRenderer`: the instances and the four draws, called by the ships' layer;
  - `SplashLook`: the knobs, `-splash=`.
- **The shaders:** `Include/splash.glsl` (the one model), `splash.vert`, and `splash_column`, `_shadow`, `_foam`,
  `_stain` `.frag`.
- **The table:** its miss marks are drawn × (1 − the world's share). Its foam ring takes a splash's dye.
- **Tests:** `ShellSystemTests.A_splash_knows_how_it_fell_and_whose_dye_it_carries`,
  `ScenarioTests.Dyes_go_to_the_ships_they_are_given`, `SplashBookTests`.

Seen (captures at 0.5, 2 and 4 m/px; noon, 19:30 and 23:30):
- the impact's cone and the rising pillars, with their roots flared and their true shadows;
- the crown opening over the surge ring, then breaking into clumps as it falls, the curtain streaking under it;
- the foam's disc and ring flat on the water, fading;
- with `dye:auto`, red crowns over white bases, and red stains lingering after the foam;
- at night, grey moonlit columns;
- at 60x, whole salvos standing round the ships, and older fall of shot as faint foam.

Changed on the way:
- **Opaque, not translucent.** A noisy translucent volume, marched obliquely, smeared into vertical streaks. The water
  is now nearly opaque (about 6 optical depths across a shaft), and the noise shapes the outlines instead.
- **No mist.** As a smooth volume it read as grey domes; it waits for the drift downwind.
- **The shadows** fade out between 7° and 3° of sun and are no longer than 8 m a metre of height. Where the shared
  light gives no shadow (the ships cast none either), neither do the splashes.

**The user's first round (2026-10-15): "very beautiful", but clones.** A salvo's columns started together and reached
the same height, and the eye caught the repetition. The point-blank lean was too much.
- **Each splash is its own** (`SplashRenderer.Vary*`, from its seed, so the same from frame to frame; `-splash=vary:G`
  scales it, 0 for none): its height ±15%, its width ±10%, its rise and fall ±10% longer or shorter, and its start up to
  0.3 s after the impact (its foam waits with it). This is display-only variation on top of the sim's own spread; the
  salvo's ripple, when it comes, adds to it.
- **The lean eases away at point blank.** It's unchanged from ~6° of fall up, where it looked right, and fades to
  nothing at 1.5° (`SmoothStep(1.5, 6, fall)`). At 1.2 km the columns stand straight.
- Fixed on the way: before its delayed start, a splash's foam drew as a white disc (a negative age read as the
  impact's white). It now draws nothing until it starts.

Cost: a stress of 16 dreadnoughts engaged in one 1 m/px view at 60x, 1080p, DevRelease, about 1,500 splashes drawn a
frame (mostly foam). Three alternating runs:
- the GPU's scene p50 1.22-1.23 ms with splashes against 1.12-1.43 ms without (about 0.1 ms; the mean and p95 swing
  more between runs than between the two);
- the CPU's build 0.10 ms a frame, for a book of ~4,000;
- nothing allocated a frame (`SplashBookTests`).

## Decisions (the user, 2026-10-15)

1. **The effects camera.** Big explosions and splashes may break realism for the look, since they don't affect
   results. The rising column (and later other tall effects) gets a 3/4 view of its own, oblique and looking north,
   inside the top-down map. What lies on the water (foam, stains, the shadow) stays untilted, in the wake target or on
   the true ground.
2. **One fixed pitch** for every zoom.
3. **Dye is built now**, ready for a later cut; none in the 1890-1925 cut by default.
4. **Time compression:** something reasonable at the usual 60x, not linear in the compression. The coarse tier shows
   none.
5. **The salvo's ripple comes later**, with the fire-control and firing rework. This pass must work with changed shell
   timings, unchanged.
6. **Near misses:** the column standing over the ship is all of it this session. Effects on ships come with the hit
   effects in a later VFX pass.

## Later

- Ring waves in the wake target's slopes.
- Mist drifting downwind after the collapse.
- Ricochet skips at shallow angles of fall.
- HE's flash and grey smoke.
- The delayed underwater burst.
- Spray particles at the closest zoom.
- Dye handed out by navy, era and division.
- With the hit-effects pass: wet decks and the hull's shudder after a near miss, a torpedo hit's column through the
  same shader.
