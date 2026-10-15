# Wakes: the ships' wave field on the sea, and the trail as foam (plan agreed 2026-10-15)

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
target all the same: a logged exception, approved (decision 1).

## Restrictions

- **Display only.** The sim never reads a wake (plan.md: "effects are local and flat... looks, read from the sim,
  never fed back into it"). The bake reads the class (`ShipClass`, `ShipSolids.Waterline`) and the snapshot (speed,
  yaw rate, pose), nothing more.
- **One physical model.** The bake's hull is the hitbox model's level waterline (`ShipSolids.Waterline`, half-breadths
  aft to fore), its length, beam and draught. The deck drawn over it is bigger than the waterline, so the wake seems
  to start just aft of the stem and outboard of the deck edge, as photos show (research §3.2 item 7).
- **Lightweight**, measured at each step: the pre-pass and the shading together under 0.15 ms at 1080p in the world
  view on the 4090 (`-gputime`), with ~20 ships in view. A bake under ~30 ms on a worker thread (a few workers, one bake each), never on the render
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

**A look tool**: `-wakebake=destroyer:15,25,35 [-output=DIR]` (an exe flag: the CLI doesn't reference the game)
writes η and lit-slope PNGs, to compare with the research's images.

**Done 2026-10-15** (`Map/Wakes/`: `Fft`, `WakeBake` with `WakeHull` and `WakeField`, `WakeLook`; `WakeBakeTests`).
What changed from the research on the way:
- **A transom's dead water.** The destroyer's waterline ends in a transom 77% of its beam, a step in the forcing,
  and the linear stern wave came out at 4.7× the bow wave. The forcing alone (`WakeHull.ForcingAt`) now carries the
  run on behind a transom, closing at 12° in a cosine. The hull mask still uses the true waterline.
- **A soft limit** at 1.25 Zb (A·tanh(η/A)). At Fr 0.6 the linear trough beside the hull reached 10.8 m against a
  3 m bow wave. Research §4 suggested a clamp.
- **Damping by Froude number**: 0.12 g/U up to Fr 0.3, rising to 0.3 by 0.55. The research's 0.08 left the far
  wedge wrapping round the grid's back edge into streaks beside the bow at 39 kn. 0.3 throughout erased the
  Kelvin arms at 16 kn, where the grid is sized by the length and the wrap is far off.
- **Smoothing of at least 1.5 cells**, so waves near the grid's Nyquist don't alias into streaks.

Measured (Release, single thread):
- Bake times: 12-21 ms on a 512² grid (the destroyer to 26 kn, the dreadnought), 58-69 ms on 1024² (the destroyer at
  33-39 kn). Then the two symmetries (below): 5.6-8.1 ms and 23.8 ms, the same field to 10⁻⁶ in slope.
- The envelope's half-angle is 18.5° at Fr 0.25 (Kelvin's is 19.47°).
- The angle of the highest waves narrows from 17.5° at Fr 0.25 to 13.9° at 0.4 and 8.4° at 0.65. That is narrower
  than Darmon's 0.147/Fr (13° at 0.65); the constant depends on the hull.
- The highest water is 1.12 Zb on the dreadnought at Fr 0.3.

**The symmetries (2026-10-15, the user's choice over threads within a bake).** The waterplane is real, so its spectrum
is Hermitian: only the columns kx ≥ 0 are transformed, and two real rows share one complex FFT each way. It is even
across the track, as is the response, so the factor is computed once for ±ky and only the starboard half comes back,
mirrored to port. Threads go to whole bakes instead (K2): splitting one bake over cores would mostly be sync.

### K2. The library: bakes shared by size and speed

`Map/Wakes/WakeLibrary.cs`, beside `ShipArtLibrary`. Decided with the user, 2026-10-15, to keep the VRAM and the baking
from growing as classes times speeds:

- **Buckets in speed, ~8% apart** (from 3 kn: 34 to 40 kn). K3's crest-aligned blend draws any speed from the two
  nearest buckets, so the steps can be coarse: between them only the pattern's shape changes, and slowly.
- **Shared, not per class.** A request is met by any bake whose hull is within a few metres of the ship's waterline
  length and beam, at the same speed bucket (the thresholds are knobs, to set by eye). Classes of one type and era
  mostly share.
- **An accelerating ship doesn't bake a frame.** It asks only on entering a bucket nobody has baked. Real ships take
  minutes to work up (a destroyer crosses ~30 buckets from rest to full: a request every few seconds at 1x); the
  bucket of the ordered speed (`SpeedOrderKn`) goes first, and one merely passed through is served by its neighbours.
  Until a bucket lands, its nearest baked neighbour stands in (K3's scaling makes it fit). With none at all, the ship
  has no wake until the first bake arrives: it fades in over half a second.
- **One bake per thread**, a few workers, nearest requests first; a bake never splits over cores. The render thread
  uploads each finished bake to a pooled texture and mips it (`SDL_GenerateMipmapsForGPUTexture`, written in place,
  without `cycle`: sea.md's lesson).
- **Half the size, a quarter the channels.** The field is mirror-symmetric, so only the starboard half is stored
  (384×128) and the shader mirrors it. Per texel, RG16F: η, and |∇η|² for LEAN. The shader takes the mean slope from
  η by differences between neighbouring texels at the mip it reads (averaging and differencing commute), and the
  variance as the second channel's mip less that slope squared. About 260 KB a bake with mips, against ~1 MB.
- **The pool**: 32 textures, ~8 MB, least recently used out. A budget, never a function of the fleet.

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
- **The ribbon replaces the line** (decision 2): the line's alpha times (1 − world factor), the ribbon's times the
  world factor.
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

## Decisions (the user, 2026-10-15)

1. **The ships' layer builds the wakes, the sea shades them.** They are placed with the ships, under the hull, in the
   world view, and their slopes and foam reach the sea's shading through a screen-sized wake target. This is a logged
   exception to `sea.md`'s "no new target", approved: those rules ask that an exception be named and agreed, not
   that the design bend round them.
2. **In the world view the foam lane replaces the track line completely.** It is the visual cue for the ship's path.
3. **The foam lane lasts the track's 10 minutes**: white for ~3, then a faint slick.
4. **Turns use the cheap shear**; the spectral sim comes later.
5. **Wakes only ahead at 2 kn or more**; a ship going astern draws none.
