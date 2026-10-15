# The sea: an FFT ocean in the world view (plan, agreed 2026-10-15)

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

For v1 the state is one for the whole world. It comes from a dev flag (`-sea=5:250,1.2:13:300`, Beaufort and wind
from, swell Hs:Tp:from) and a scenario field. A sea state that varies by place and hour waits for weather in the sim.
When the state changes, h0 is rebuilt; the phases stay, so nothing pops.

### W2. The wave sim on the GPU (compute)

The engine has no compute pipeline yet, so this step adds a small one (`GpuComputeBuilder` beside
`GpuPipelineBuilder`, and `.comp.glsl` in `compile_shaders.bat`).

- **Size.** N = 128, three cascades as layers of RGBA16F array textures (moments sx, sz, sx², sz²; displacement Dx,
  Dz, h), mipmapped.
- **Two dispatches**, each a row of N in groupshared memory (Stockham).
- **Mips.** `SDL_GenerateMipmapsForGPUTexture`, if it does array layers on our backend; otherwise a reduction
  dispatch of our own. To be checked first.
- **The sea's clock.** It advances by real frame time times the clock's achieved speed, capped at 8x (lower if the
  waves look choppy there), and stops when paused. It is integrated, so speed changes never jump the phase.
- **Cost.** Measured with Tracy GPU zones. A Gpu-trait test reads back the realised height variance against the
  spectrum's (the lab's `heightStats`).

### W3. The sea in `globe.frag`

- **Where it runs.** Only when the world factor is above 0 and the pixel isn't land; otherwise the chart sea of
  today. Between the two zooms, the chart sea and the world sea are mixed by the factor.
- **Pixel position.** In metres relative to the view's centre, from the ray in eye space (t·d − altitude·forward,
  float, exact to millimetres at these altitudes). The sea frame's offset and rotation come from the CPU (W4).
- **Mip footprint.** Taken from each pixel's real size. The camera has perspective (40°), so a pixel at the corner
  covers ~13% more sea than one at the centre; the lab assumed one size.
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

### W4. The sea frame on the sphere

The FFT field is flat and the Earth isn't, so it needs a frame. Two choices:

- **(a) Fixed patches (chosen).** CubeSphere faces cut into ~600 km patches, each with its own tangent plane at
  its centre and its own seed. The wave scale is off by ≤ 0.3% at a patch's edge.
  - The CPU, in double, gives the shader the view centre's position in its patch's plane, wrapped on each cascade's
    and the hex lattice's periods (the lab's rule, so precision never decays), and the 2x2 turn from the camera's
    east/north to the patch's axes.
  - **v1: the patch under the view's centre frames the whole view.** No seam on screen; the sea changes once when
    the centre crosses a border (every ~600 km). This costs no more to build than (b).
  - **Later, only if that change shows:** a border inside the view blends across a ~500 m band (the second
    patch's lookups, only on those pixels). That is the part that isn't trivial.
  - The sea is then a pure function of place and time: the same everywhere you look, reproducible in captures, and
    the field CPU ship motion can sample later.
- **(b) Anchored to the camera.** Simpler, but not a function of place: the sea differs per visit, and the sim could
  never read it. Not recommended.

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
5. The sea frame is fixed patches, the view's centre picking the patch, since that is as cheap as anchoring to the
   camera. The border blend waits until it's needed.
6. Choppy displacement is in v1; such costs can become graphics settings later.
