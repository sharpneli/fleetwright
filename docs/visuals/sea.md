# The sea: an FFT ocean in the world view (plan agreed 2026-10-15; W0-W4 done the same day)

The sea of `docs/visuals/sea-state-lab.html` (research: `research/vfx/ocean-surface-research.md`) brought into the
globe's pass, shown only where the map is the world and not the planning table. Foam is left out of the first
version.

## The idea: the table and the world

Zoomed out, the map is a **planning table**: a chart, icons, the lat/lon grid, symbols. Zoomed in, it is **the world
seen from above**: the sea as water, ships as themselves. There is one boundary between the two, and every element
crosses it at the same zoom and over the same cross-fade. Nothing is half chart and half world.

Today each ship decides on its own: sprite once its length on screen passes 16-28 px (`ShipDrawList.FadeFromPx`,
`FadeToPx`). A 200 m battleship becomes a sprite at ~10 m/px and a 50 m torpedo boat at ~2.5 m/px, so between those
zooms the map shows the world and the table at once. That rule goes and the shared one replaces it.

## Restrictions

- **One pass for the shading.** The sea is shaded in `globe.frag`, which already ray-casts every pixel. There is no
  new screen pass, no new target, and no change to the engine or the blit (the same rule as
  `docs/visuals/ship-lighting.md`). The only new GPU work is the wave simulation, a few small compute dispatches,
  and they run only while the world view shows.
- **Lightweight.** Targets on the user's machine (DevRelease, `-frametimes`): sim under 0.1 ms, shading under
  0.3 ms at 1080p, zero per-frame allocation. Measured at each step, not assumed.
- **Shared light.** Sun, moon, sky and the sea's colour come from `ShipLights` and `LightTable`, so the ships and the
  water are lit by the same light. The output is display-referred and uses the ships' soft shoulder (no tonemap).
- **Display only.** The sim never reads the sea's drawing. The sea state and its spectrum are pure values the sim
  could read later (see "Where the numbers live").
- **Visuals stay light.** Each step is visible on its own and could be the last.

## Is the lab lightweight, and is it a single pass?

The shading is. The simulation is not, as built.

| Part | The lab | Here |
|---|---|---|
| Shading | One full-screen pass, ~22 fetches and ~150 ALU per pixel; 0.34-0.37 ms at 2.2 Mpx on a 4090 (WebGL) | Folded into `globe.frag`, so no pass of its own; skipped on land pixels and at table zoom |
| Wave sim | Per cascade: spectrum, 14 FFT passes, combine, 2 mip builds. That is 48 fragment passes a frame, 0.59 ms | Compute, all three cascades as layers of one array: spectrum plus row FFT (dispatch 1), column FFT plus combine (dispatch 2), then the mips |
| Foam | Ping-pong textures, and a GPU readback every 12 frames for calibration (a stall) | Gone in v1: no ping-pong, no readback, no foam fetches or breakup noise |
| Gust patches | 1-9 km noise for theatre zoom (10-50 m/px) | Gone: theatre zoom is the table now |
| Runs | Every frame, always | Only while the world factor (below) is above 0 |

Per pixel, v1 keeps the following:

- hex-tiled moments and displacement for cascades 0-1 (12 fetches);
- cascade 2 and its ripple copy (3 fetches);
- the displacement inversion (7 fetches), faded out above 1.5 m/px as in the lab;
- the LEAN slope moments with the Cox-Munk top-up;
- the anisotropic Gaussian glint for the sun, plus the same lobe for the moon.

## The steps

### W0. One switch: `WorldView`

A single place (like `ShipSystem.Rails.cs`) that turns the zoom into a world factor from 0 (table) to 1 (world).
Every element reads it.

- **The unit.** Metres per *reference* pixel: m/px scaled by window height / 1080, the same scale the UI uses. The
  boundary is then the same stretch of sea on a 1080p screen and on a 4K one.
- **Proposed numbers.** World at ≤ 4 m/ref-px, table at ≥ 6 m/ref-px, a smoothstep between them (about two wheel
  notches). At 1080p, 4 m/px is a view about 7.7 x 4.3 km, inside the Tactical band.
- **Ships.** Sprite alpha is the world factor, icon alpha is one minus it. Art that isn't up yet still falls back to
  the icon. In the world view a ship is its true size: at 4 m/px a torpedo boat is ~12 px, a destroyer ~25 px, a
  battleship ~50 px. Picking keeps its generous radius.
- **Grid.** It fades out with the world factor (alpha times 1 - w).
- **Scale without the grid:**
  - *Edge ticks*: lat/lon ticks with labels on the screen's border, at the graticule's spacing, like a chart's
    margin.
  - *A drawn scale bar* in the "where" panel, at a round length.
  - The waves and the ships also show scale on their own.
- **The battle stays visible in the world view, stylised**: shell dots, splashes, fires, contacts, group labels,
  selection, routes, order and fire lines keep today's icon style. Realism for those comes later, zoomed in
  (VFX). What crosses at the boundary is the ships (icon to sprite), the grid and, from W3, the sea.

**Done 2026-10-15.** `Map/WorldView.cs` holds the boundary (`BoundaryM`, 4 m per reference pixel; `-worldview=M`, or
live in the "where" panel while F2's stats show) and the factor; `ShipDrawList` takes sprite or icon from it, the
grid fades by it, `Graticule.BorderTicks` marks and labels the edges, and the "where" panel shows the state and a
scale bar.

### W1. The sea's numbers (CPU, shared)

The lab's `seaParams` and `buildH0` port to `Fleetwright.Sim.Core` as `SeaState` and `SeaSpectrum`:

- **`SeaState`**: Beaufort or U10, fetch, wind direction, swell Hs/Tp/direction.
- **`SeaSpectrum`**: JONSWAP with the Pierson-Moskowitz cap and the γ fade, Longuet-Higgins/Mitsuyasu spreading, the
  swell, and the seeded per-cascade h0 with stable phases.

They are deterministic and allocate only when the state changes. They live in Sim.Core so that later ship motion
(research §5.2: the K strongest bins on the CPU) uses the very same field ("single implementation").

The tests the research asks for:

- the realised variance against Σ P;
- the travel direction (wind from N moves the field south; the lab's sign bug);
- Hs against the research's table;
- the Cox-Munk slope variances.

The wind comes from the world's weather (`WeatherHere`, which scenarios already set; a world without a scenario
now has a moderate breeze, Beaufort 4 from 250°, `SimSettings.DefaultWind*`). The swell is a low one 50° off the
wind until the weather knows swell (`SeaState.Of`). A sea that varies by place and hour waits for weather that does.
When the state changes, h0 is rebuilt; the phases stay, so nothing pops.

**Done 2026-10-15.** `Sim.Core/SeaSpectrum.cs` (`SeaState`, `SeaSpectrum`), tested in `SeaSpectrumTests`: heights
against the research's table, the uploaded variance, waves travelling downwind, Cox-Munk, stable phases, no
allocation.

### W2. The wave sim on the GPU (compute)

The engine has no compute pipeline yet, so this step adds a small one (`GpuComputeBuilder` beside
`GpuPipelineBuilder`, and `.comp.glsl` in `compile_shaders.bat`).

- **Size.** N = 128, three cascades as layers of RGBA16F array textures (moments sx, sz, sx², sz²; displacement Dx,
  Dz, h), mipmapped.
- **Two dispatches**, each a row of N in groupshared memory (Stockham).
- **Mips.** `SDL_GenerateMipmapsForGPUTexture`, if it does array layers on our backend; otherwise a reduction
  dispatch of our own. To be checked first.
- **The sea's clock.** It advances by real frame time times the clock's achieved speed, capped at 8x (lower if the
  waves look choppy there), and stops when paused. It is integrated, so speed changes never jump the phase. The
  frequencies are quantised to multiples of 2π/1024 s (the lab's "loopable dispersion": invisible at 64 s already),
  so the clock wraps at 1024 s and the phases stay exact in float.
- **Cost.** Measured with `-gputime` (below). A Gpu-trait test reading back the realised height variance against the
  spectrum's (the lab's `heightStats`) is still to write.

**Done 2026-10-15** with W3 and W4: `Map/SeaSim.cs`, `sea_rows.comp.glsl`, `sea_columns.comp.glsl`. Six compute-written
textures (moments and displacement per cascade), each mipped by `SDL_GenerateMipmapsForGPUTexture`. **Lesson:** written
with `cycle = true`, the mip blits stalled every frame (8.3 → 15.6 ms at 120 Hz); written in place they cost nothing
measurable.

### W3. The sea in `globe.frag`

- **Where it runs.** Only when the world factor is above 0 and the pixel isn't land; otherwise the chart sea of
  today. Between the two zooms, the chart sea and the world sea are mixed by the factor.
- **Pixel position.** In metres relative to the view's centre, from the ray in eye space (t·d − altitude·forward,
  float, exact to millimetres at these altitudes). The sea frame's offset and rotation come from the CPU (W4).
- **Mip footprint.** One size for the whole view: a pinhole camera looking straight down at a level sea maps it
  affinely, so every pixel covers the same metres (the curvature is nothing at these widths).
- **What carries over from the lab:** the hex tiling on cascades 0-1, the choppy inversion (kept in v1; it and its
  like can become graphics settings later), the LEAN moments with the Cox-Munk top-up, the cascade-2
  pattern-to-statistics fade, and the free cascade-3 ripples.
- **Glint.** The perspective camera supplies the varying view vector the lab had to fake with its "glint eye".
- **Light.**
  - From `ShipLights`: sun, moon, sky, the sea's body colour.
  - The moon's path on the water is the same glint lobe, lit by the moon.
  - In time-lapse the glint fades to the day's mean, as the ships' shadows do.
  - `LightTable.SeaDay`/`SeaDusk`/`SeaNight` become the water's body colour, so the ships' "sea below" ambient and
    the sea agree.
- **Checks.** `-screenshot` captures at the lab's zoom presets, Beaufort 2 / 5 / 8, beside the lab. Frame times in
  DevRelease.

**Done 2026-10-15** (`Include/seashade.glsl`, `Map/SeaView.cs`): in `globe.frag`, after the chart sea and before the
land, mixed in by the world factor. Captured at 0.12, 0.5, 2 and 4.8 m/px (Beaufort 4 at noon): glitter at deck
zoom, the sun's glint path, no tiling seen, no flash through the cross-fade. Two frames half a second apart, phase
correlated: the pattern moves toward 63° (downwind of a wind from 250°) at ~4 m/s, the wind sea's short waves.

**Cost** (`-gputime -frametimes`: the scene alone on the GPU, RTX 4090, 1920x1080, 8x MSAA target; the GPU's clocks
at this light load make single numbers rough):

| View | Scene, p50 |
|---|---|
| Table, 8 m/px (no sea) | 0.60 ms |
| World, 2 m/px | 0.76 ms |
| World, 0.3 m/px (the choppy inversion on) | 1.01 ms |

So the sea, its sim included, adds ~0.15-0.4 ms here; a modest GPU, 6-8x slower, ~1-3 ms. The research's next savings
if needed: gate the hex tiling by tile size at close zoom, skip cascades that are pure statistics.

### W4. The sea frame on the sphere (done 2026-10-15, in `SeaView`)

The FFT field is flat and the Earth isn't, so it needs a frame. **Camera-relative (decided 2026-10-15):** whatever is
in view is consistent, and panning is seamless; scrolled out to the table and back, the water may be different.

- An anchor on the globe with its tangent plane (east and north at the anchor). On the table it follows the camera
  every frame; while the world view shows it stays put.
- The CPU, in double, gives the shader the view centre's position in the anchor's plane (gnomonic), wrapped on each
  cascade's and the hex lattice's periods (the lab's rule, so precision never decays), and the 2x2 turn from the
  camera's east/north to the anchor's axes (the meridians' convergence). Pixels add their offset from the centre in
  metres, from the ray in eye space.
- Far from the anchor the plane's scale drifts by (d/R)²: about a metre of creep over a 4 km pan 100 km from it. The
  world view is never that far from where it was entered.
- Not a function of place, so CPU ship motion (research §5.2) would sample the field in its own frame; it only needs
  the statistics to match, not this view's pattern.

### Low sun and night (found 2026-10-15)

"No waves in the Black Sea" was the time of day, not the place: point checks round the world (texture as the std of
a capture's grey levels, 2 m/px) gave the same sea everywhere at a high sun, and nearly none at a low one or at night.

| Where, when | Sun | Std |
|---|---|---|
| Black Sea, 10:00 local | ~57° | 14.7 |
| Black Sea, 16:00 local | ~35° | 6.9 |
| Arctic 70°N, noon | ~42° | 2.8 |
| Indian Ocean 40°S, noon (winter) | ~28° | 1.0 |
| 60°S, noon (winter) | ~8° | 0.35 |
| Black Sea, sunset / night / full moon | ≤ 2° | 0.0-0.5 |

Why: from straight above, slopes show mostly through the sun's glint, which needs a facet tilted halfway between the
eye and the sun; below ~30° of sun that's beyond the sea's slopes. The sky's reflection is ~2% at near-vertical
incidence and nearly uniform round the zenith, and at 2 m/px the resolved waves are gentle (the steep short ones are
roughness). That is close to the physics: a straight-down view at a low sun is low in contrast.

Done: the water body's sunlight and moonlight now follow the facet they fall on (its cosine through 1 - Fresnel,
`SeaView.FacetLight` 0.7 of it, the rest as a level sea): physical, a small gain (1.0 at 40°S). A look knob,
`SeaView.Relief` (`-sea=relief:3`), steepens the slopes for the body's light alone: 1 / 3 / 6 give 1.0 / 2.3 / 3.9 at
40°S and leave noon nearly as it is (8.3 / 8.6 / 9.2).

**Tuned (user, 2026-10-15: realistic is the starting rule, then tune where it fails):** all in `SeaView`, the water
body's light only (the glints and the ships stay physical).
- *Low sun:* the relief grows from 1 at a 40° sun to `LowSunRelief` 4 at 15°, where the glint stops showing the waves.
- *Twilight and night:* a light of the sea's own fades in as the sun goes from 12° to −2° (`NightFromDeg`). While the
  moon is up at all, it comes from the moon's bearing, a bit brighter than moonlight (`MoonNightRad`); with no moon
  it is dimmer, bluer starlight biased to the sun's side under the horizon (`StarNightRad`). Its height is kept where it
  shades (the moon's clamped to 25-60°, the stars' at 40°), its relief `NightRelief` 5. Dark and gloomy, the waves read.

| Black Sea / where | Before | Tuned |
|---|---|---|
| 10:00 local / 16:00 / ~18:15 (sun ~13°) | 14.7 / 6.9 / 1.2 | 14.8 / 7.1 / 2.6 |
| dusk / moonless night / full moon | 0.02 / 0.0 / 0.5 | 2.3 / 2.8 / 6.0 |
| 60°S noon / 40°S noon / North Sea noon | 0.01 / 0.2 / 8.3 | 1.7 / 1.9 / 8.3 |

Knobs for side-by-side captures: `-sea=lowsunrelief:4,nightrelief:5,moonnight:1,starnight:1` (scales).

### Later (not v1)

- **Foam:** the lab's whitecaps, with the cost listed above coming back.
- **Wakes:** `research/vfx/wake-vfx-research.md`, through the shared channels (mean slope, variance, foam).
- **Shallow water and the coast's look in the world view** (the land is still the chart's fill and coastline).
- **Gusts and wind streaks**, if the 4 m/px end looks too even.
- **Ship heave/pitch/roll** from the CPU bins.
- **Weather that varies in place and time** (lerp h0).
- **Range rings** round the selection as another scale cue.
- **Pointers to small ships** in the world view (a triangle pointing at a ship too small to see easily): the
  price of exact scale, which stays.

## Decisions (2026-10-15)

1. The boundary is one parameter, `WorldView.BoundaryM`, starting at 4 m per reference pixel, to iterate on.
2. Ships are always drawn at their true size in the world view. The world is realistic; small ships may get extra
   pointers later.
3. The battle's marks (shells, splashes, fires, contacts, orders) stay in the world view in today's stylised icon
   style; their realism comes zoomed in.
4. The sea's clock follows the game clock: frozen when paused, capped at 8x, capped lower if it looks choppy.
5. The sea frame is camera-relative: consistent in view and while panning, free to change between visits (a
   patch border would likely look jarring).
6. Choppy displacement is in v1; such costs can become graphics settings later.
