# Wakes: the ships' wave field on the sea, and the trail as foam (plan proposed 2026-10-15)

Research: `research/vfx/wake-vfx-research.md` (§3 the bake and the stamp, §6 the v2 thin-ship forcing, §7.2 the foam
lane on the path). It builds on the sea (`docs/visuals/sea.md`), whose "Later" list names wakes "through the shared
channels (mean slope, variance, foam)".

## What this first version is

- **The wave field.** Each ship carries its steady Kelvin pattern: a height field baked once per class and speed
  bucket, its slopes added to the FFT sea's own. The sea's shading lights both together, so the wake is the sea's
  surface and not a decal over it.
- **No foam at the hull.** The bow sheet, the breaking crests and the stern's whitewater are left out (research §6's
  foam tiers). They come later, through the same target.
- **The trail as foam.** The track already drawn behind each ship (`MapScene.Ships.cs`, `Track`: a line, 10 minutes)
  becomes the foam lane in the world view: a ribbon at its true width that widens and fades with age, white water
  broken up by world-anchored noise. On the table it stays the line it is today. The two cross over the world view's
  cross-fade, like the grid.

## Where it's drawn: the ships' layer, into the sea

The wakes are built where the ships are, in `ShipDrawList` (one stamp per ship placed, one ribbon per track), and they
show only where the ships are sprites: in the world view, faded in by `WorldView.Factor`. They lie under the hull,
the shadows and the turrets.

They can't be blended over the sea as sprites are, though. A wake changes the sea's slopes, and the colour follows
from those slopes through the glint and the Fresnel term, which isn't linear. So:

- **A wake target**, screen-sized and RGBA16F: slope x and y in the camera's axes (blended additively, so two wakes
  crossing superpose as linear waves do), the slope variance the pixel can't resolve (also added), and foam
  coverage (max blend in alpha). The ships' layer draws into it in a pre-pass before the globe's main pass. This is
  the pattern the land's coverage target already follows (written before the pass, read per pixel with
  `texelFetch`).
- **`seaShade` reads it**: the slope goes into `m`, the variance into `var`, and the foam is mixed over the colour at
  the end, lit by the same lights. The sea is still shaded once, in `globe.frag`'s one pass. The new GPU work is the
  pre-pass alone, and it runs only while the world view shows and something is in it.
- `IMapLayer` gets a third hook beside `Copy` and `Draw`: the draw into the wake target. `GlobeRenderer` owns the
  target and the pass, as it does the coverage target.

`sea.md`'s "no new target" was about the sea's shading, and the shading stays in one pass. The wake target is a new
target all the same, and the decision list below names it.

## Restrictions

- **Display only.** The sim never reads a wake (plan.md: "effects are local and flat... looks, read from the sim,
  never fed back into it"). The bake reads the class (`ShipClass`, `ShipSolids.Waterline`) and the snapshot (speed,
  yaw rate, pose), nothing more.
- **One physical model.** The bake's hull is the hitbox model's level waterline (`ShipSolids.Waterline`, half-breadths
  aft to fore), its length, beam and draught. The deck drawn over it is bigger than the waterline, so the wake seems
  to start just aft of the stem and outboard of the deck edge, as photos show (research §3.2 item 7).
- **Lightweight**, measured at each step: the pre-pass and the shading together under 0.15 ms at 1080p in the world
  view on the 4090 (`-gputime`), with ~20 ships in view. A bake under ~30 ms on a worker thread, never on the render
  thread. Zero per-frame allocation: the ribbon's vertices and the stamps go into grown arrays, and the bake's scratch
  is pooled per worker.
- **Realistic, then tune**: Noblesse's bow-wave height for the amplitude, the research's lane widths and lifetimes for
  the foam. Knobs go in `SeaView` (`-sea=wake:G,foam:F`) for side-by-side captures.

## The steps

### K1. The bake (CPU, pure, tested)

`Map/Wakes/WakeBake.cs` ports the height-field half of `wake_bake_ref_v2.py`'s `bake2`. The foam half is skipped.

1. **The domain**, per research §2.4: span = max(L, λ), λ = 2πU²/g. Ahead 0.5L + 0.6 span, behind 0.5L + 4 span,
   half-width 0.45·behind + B. A power-of-two grid, at most 1024 a side, dx ≥ L/80.
2. **The waterplane** from `ShipSolids.Waterline`, 4×4 supersampled, lightly blurred (σ ≈ 0.04 B). The last few
   percent of a transom run are softened, so the linear stern trough doesn't overshoot (research §4).
3. **The thin-ship source** q = U·∂m/∂x, one forward FFT, the steady response with Rayleigh damping and the draught's
   attenuation (1 − e^{−kT})/k, then one inverse FFT for η. Then the two sign checks: waves trail, and the stem's crest
   is positive.
4. **Calibration.** The entrance half-angle β comes from the waterline's slope over the forward ~8% of L (clamped
   6-30°). The stem crest is scaled to Noblesse's Zb = 2.2·(U²/g)·tan β / (cos β·(1 + Fr_D)).
5. **Shaping.** η is zeroed inside the hull, tapered at the domain's edges, and faded astern by
   1/(1 + behind/1.5 L).
6. **The output** is the near field cropped to the stamp's extent: stem + 0.3 span ahead, 3L behind the stern, and
   ±(0.36·behind + B) across. It is resampled to 384×256 as moments (∂η/∂x, ∂η/∂y, and their squares), so the mips
   give LEAN's variance: from altitude, a wake's short waves become a patch of different glitter, not aliasing.

The FFT is a small in-place radix-2 of our own (no package), rows in parallel. Allocation stays inside the bake, as at
a spawn.

**Tests** (`Fleetwright.Tests`):
- the stem's crest is positive and the energy is astern;
- the envelope's half-angle is about 19.5° at Fr_L 0.25, and it narrows by Fr_L 0.6 (research §2.2);
- the stem crest equals Zb;
- two bakes of the same input are identical;
- a destroyer's and a battleship's bake times.

**A look tool**: `shipgen wake destroyer --kn 15,25,35 --out out` writes η and slope PNGs, to compare with the
research's images.

### K2. The library: bakes by class and speed

`Map/Wakes/WakeLibrary.cs`, beside `ShipArtLibrary`.

- **Buckets.** Geometric steps of 4% from 3 kn. A 30 kn ship has ~60 of them, and only the ones visited get baked.
- **Requests.** The draw list asks for (class, bucket) each frame for each ship it places. A worker thread of the
  library's own bakes the nearest requests first. The render thread uploads each finished bake to a pooled texture and
  mips it (`SDL_GenerateMipmapsForGPUTexture`, written in place, without `cycle`: sea.md's lesson).
- **The pool.** 32 textures of 384×256 RGBA16F, about 33 MB with mips, least recently used out.
- **While a bucket bakes**, its nearest baked neighbour stands in (K3's scaling makes it fit). With none at all, the
  ship has no wake until the first bake arrives: it fades in over half a second.

### K3. The stamps: the wave field in the world view

- **The quad.** One per ship moving at 2 kn or more ahead, in the world view. It reuses `ShipQuad` and
  `shipmap.vert` (eye-relative corners in double, the ship-local position per pixel). A new `wakesplat.frag` writes
  into the wake target, and the quad's spare fields carry the two bakes' lookups, the gains and the heading in the
  camera's axes. One draw per bake texture.
- **No ghosting between speeds.** The ship's speed falls between two buckets, and both are sampled at λ-normalised
  coordinates: about the stem, scaled by (U_b/U)², which lines their crests up. Each is weighted by where the speed
  falls between them, with slope gain (Zb(U)/Zb(U_b))·(U_b/U)². A speed change then never pops or ghosts, and a
  stand-in bucket fits the same way.
- **Turning.** The lookup is sheared along the curve, y' = y + ½κx² with κ = yaw rate / U (research §3.2), and the
  slope is turned by the curve's local angle. κ is capped so the shear stays within about L, and the quad grows to
  hold it. This is the cheap version. The research's spectral sim (§7.2) is the right one, and it waits for a later
  pass.
- **The edges** fade over their last 10% in both directions, so a stamp's border never shows.
- **Checks** (`-screenshot` captures, beside the research's images):
  - a destroyer at 15, 25 and 35 kn and a battleship at 21 kn;
  - at 0.5, 2 and 4 m/px;
  - at noon and with a 25° sun;
  - a ship in a hard turn;
  - two ships' wakes crossing;
  - the cross-fade to the table, with no pop.

  Then the cost with `-gputime` and DevRelease `-frametimes`.

### K4. The trail as foam

- **The track samples turns.** A point is laid every 10 s as today, and also whenever the heading has turned 3° since
  the last point, so a hard turn is a smooth curve, not 150 m chords. Each point holds:
  - where the stern was (the waterline's aft end, not the centre: in a turn the stern swings outboard);
  - when;
  - the speed;
  - the distance steamed since the track began.

  Points are kept for 10 minutes, at most 512 per track. The array grows as it fills, as today. The line on the table
  draws from the same points, so there is still one track.
- **The ribbon.** In the world view, each track in sight becomes a triangle strip from the stern now back along its
  points. It is built on the CPU into a grown array, with one draw. Its half-width spans ±2σ of the lane, after
  research §7.2:
  - width w = √(w0² + 2k²·B·distance back), w0 = 0.3·B·√wash, k = 0.10;
  - intensity 1.6·wash·min(U/10, 1)·e^{−age/τ}·(w0/w)^{1/4}, with U the speed when the point was laid;
  - wash is 1 for twin screws, scaled a little by the shaft count (a look knob, not a sim fact);
  - τ is ~3 min of white water, then a faint slick to the track's 10 minutes (research §2.5: real lanes last ~10 min).
- **The foam.** `wakeribbon.frag` writes foam coverage (max blend):
  - a Gaussian across the lane, with a ramp over the first 0.6 B behind the transom (the dead-water hollow);
  - broken up by research §6's soft threshold, over two octaves of streaky value noise in the ribbon's own (distance,
    lateral) metres, so it stays fixed to the water;
  - with noise cells ~2.5 m across and stretched ×4 along the track;
  - with the noise's contrast fading to its mean as a cell nears a pixel (by the pixel's footprint), so it never
    shimmers at 4 m/px.
- **Lit as the sea is lit.** In `seaShade`, the foam is mixed over the water. Its colour is a foam albedo (~0.8)
  times the sun's and the moon's light on a level surface, plus the sky's ambient and the night's tuned light, through
  the ships' shoulder. Foam hides the glint under it.
- **Line or ribbon.** The line's alpha times (1 − world factor), the ribbon's times the world factor.
- **Checks:**
  - captures at 1 and 4 m/px, noon and night;
  - a ship turning circles;
  - a ship stopping (the lane fades from where it slowed);
  - Jutland at 4 m/px for cost.

### K5. Tuning and the docs

- The knobs (`-sea=wake:1,foam:1`) set by eye against the research's photos, at Fr_L 0.25, 0.35 and 0.55.
- A debug view of the wake target: `-sea=debug:3` shows the slopes, `debug:4` the foam.
- `sea.md`'s "Later", CLAUDE.md's run lines, and this file, with what was done and measured.

## Later (not this version)

- **The foam at the hull**: the bow sheet, the crests' breaking and the stern's whitewater, from the same bake
  (research §6's three tiers), written into the wake target's foam.
- **Wakes that turn properly**: the ship-following spectral sim (research §7.2, §7.6), for the selected or nearest
  ships, with the rigid stamp further out.
- **Torpedo tracks** (`MapScene.Shells.cs`, a pale line today) as thin ribbons of the same foam.
- **High Froude craft** (Fr_L > 0.6, torpedo boats at speed): whiskers and a wider wash (research §4).
- **The turn slick and the knuckle** (research §7.3), and a bow wave lifted by the speed's history.

## Decisions to take

1. **"The ships' layer" means the wakes are built and placed with the ships, under the hull, in the world view.** The
   wave field goes into the sea's own slopes through a screen-sized wake target, not alpha-blended over the sea as a
   sprite is. Proposed, for the lighting's sake.
2. **In the world view the foam ribbon replaces the track line** (the line fades out with the world factor). The
   alternative is to keep a faint line for the selected ship.
3. **The foam lane lasts the track's 10 minutes**: white for ~3, then a faint slick.
4. **Turns use the shear for now**, and the spectral sim comes later.
5. **Wakes only ahead at 2 kn or more**; a ship going astern draws none.
