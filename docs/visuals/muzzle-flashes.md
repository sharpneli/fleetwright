# Muzzle flashes and gun smoke in the world view (plan 2026-10-15)

Research: `research/vfx/muzzle-flash-smoke-research.md` (the flash's anatomy, sizes and timings by calibre, propellants,
photometry, smoke), `research/vfx/muzzle-blast-water-vfx.md` (the blast's frost disc on the sea),
`research/vfx/blast-wave-visibles-research.md` (what else the pressure wave shows). Reference code:
`muzzle_flash_ref.py`, `muzzle_blast_ref.py`, `blast_wave_visibles_ref.py` beside them. The table already has its gun
flash (`docs/visuals/battle-marks.md` M2: a warm disc at the muzzle, held 0.35 s). This plan covers the **world view**
only, where ships are sprites at their true size, as `splashes.md` did.

Scope: a gun mount firing, from a 4" to a 15" twin, by day, dusk and night. What it makes: the flash at the barrels,
its glow, the light it throws on the sea and the ships, the blast's mark on the water, and the gun's smoke, drawn by
the map alone until the smoke system comes (decision 1, a logged exception).

## What the research asks for, and what we build instead

The research assumes an HDR target in physical units, a bloom pass, flipbooks, a half-resolution smoke buffer and a
GPU ring of shot events. We have none of those, and `sea.md`'s and `splashes.md`'s rule is no new pass and no new
target. Each costly part has an analytic stand-in that keeps what it was for:

| Research | Here | What is kept |
|---|---|---|
| HDR target in cd/m², exposure keyed to the background, eye adaptation | The flash's **contrast against the scene's background** (physical, from the sun and moon), laid onto the display's own light level (`LightUniforms.Sky.w`). Rolled to white on luminance in the flash's shader. No adaptation (decision 3) | Day: a warm blob a few times the sea's brightness. Night: white-hot, ~10⁸ over the sea, so the glow carries it. One model, two looks, no art sets |
| Bloom pass at three radii, on a compressed source | **An analytic halo.** Every emitter is a Gaussian, and a Gaussian blurred by a Gaussian is a Gaussian with the variances added. So each flash draws its own glow in its own quad: the same emitters with σ² + (r·mpp)² for r = 6 and 18 px, weighted by the compressed source `x/(1 + x/6)` | The bloom's look and its energy, exactly, at no pass and no target. The research's normalisation bug can't happen: nothing normalises by visible pixels |
| 2-3 baked turbulent-fireball flipbooks per element | **Procedural**: the secondary fireball's temperature field (hot core, cooler rim) broken by value noise in the bore's frame, advected outward as it grows, from `splash.glsl`'s noise | The ragged yellow-white ball over an orange rim, every shot its own. No new assets |
| 3-4 billboards per active flash | **One instanced quad per mount fired**: the barrels' cores and Mach discs (a loop of ≤ 4), one merged fireball (λ_f from the mount's summed charge, the research's N^⅓), the halo | One draw for all flashes, additive in display space |
| 4 sub-samples of I(t) per frame | **The envelope's frame average in closed form** (it is piecewise polynomial) | A 15 ms 4" flash is never missed or flickered by the frame rate |
| ShotEvent ring buffer on the GPU | **Placement on the CPU per frame**, eye-relative in double, into a grown instance array, as the splashes are | Fits the globe; nothing allocated per frame |
| 1-8 point lights at the fireballs | **Up to 8 flash lights a frame** in the shared light uniforms, one per firing ship (her mounts summed, at their intensity-weighted centre), read by the sea, the ships, the splash columns and the smoke | The night payoff: the sea flares orange under a broadside, the decks light up, the smoke glows for t_s and goes dark. Skipped by day, where they add nothing |
| Blast frost analytic in the water shader, with an arrival LUT | **One quad per mount fired into the wake target in its pre-pass**: the dark leading edge and the frost as *variance* (its additive channel: the edge negative, the frost positive), the near-muzzle scour as *foam* (max). The arrival time from a closed-form fit of the reference's Rankine-Hugoniot table | `seaShade` already turns variance into glitter and foam into foam: no new shading. Iowa's "heart" comes out of overlapping quads |
| Smoke puffs simulated, 6-way-lit flipbook billboards at half resolution | **Puffs whose state is closed-form in their age** (centre, rise, spread, A(t)), so the map only stores their birth; one soft quad per puff whose alpha is the research's `1 − e^(−τ_v)`, shaped by noise; a two-tap self-shadow toward the light; full resolution, the puff count held down by merging | The research's numbers (A, σ, rise) drive the look directly, so the smoke system can later take the same puffs over and sight lines will see what is drawn |
| Vortex rings, jet punch, Wilson haze, sun-shadow line, embers | Later (see *Later*) | — |

## The gun, once per turret type (CPU)

`MuzzleFlashModel` ports `muzzle_flash_ref.py`'s `derive()` (one implementation; the tools and tests call it). Per
turret type it computes λ_b, λ_f with and without the secondary flash, the Mach-disk distance x_M, the gun-emptying
time t_e, the secondary's t_s, P_sec, T_sec, the element sizes (research §2.3), and the smoke's extinction areas A_p and
A_w.

### What the model takes (decision 4)

The inputs are kept as plain data, so a propellant and ammunition system designed later fills the same fields. Until
then each is a flagged placeholder in `DesignerPlaceholders.cs`.

**The gun** (from the class's rows today):

| Input | Unit | Source now |
|---|---|---|
| Bore d | m | `TurretTypeRow.CalibreMm` |
| Barrel length | calibres | `TurretTypeRow.CalibreLength` |
| Barrels per mount, their spacing and reach from the pivot | —, m | `TurretTypeRow` |
| Muzzle device | none, hider, brake | none (no period naval gun has one) |

**The charge** (per gun and round; a later ammunition system's, the "night charge" choice among them):

| Input | Unit | Placeholder |
|---|---|---|
| Charge mass m | kg | `4000·d³·(L_cal/50)` (research §2.1; howitzers and mortars would carry 0.2-0.5× and give their own) |
| Propellant family, or its parameters below | — | by navy and era (designs carry an era, not a year): brown powder for a Victorian gun under 36 calibres (the brown-powder years' short guns); single-base for the USN, France (poudre B) and Russia (pyroxylin); diglycol for Germany from the treaty years; triple-base in the Cold War; double-base cordite otherwise |
| Flash-reducer salt fraction (K₂SO₄, KNO₃) | kg/kg | 0 (they came between the wars) |
| Black-powder igniter fraction | kg/kg | 0.01 bag guns, 0.003 cased |
| Wear-reducer fraction (TiO₂, wax, talc) | kg/kg | 0 |

**The propellant's parameters** (a family is a named set of these; research §3.1's table gives six: black powder,
single-base, double-base, cool double-base, triple-base, nitramine):

| Parameter | Unit | What it drives |
|---|---|---|
| Q, the propellant's energy | MJ/kg | the blast's length λ_b (with 0.45 of it in the blast) |
| e_ab, the afterburn energy | MJ/kg | the secondary fireball's size λ_f |
| flash0, the secondary flash's propensity | logit | P_sec, with the bore, the barrel's length, salt and the device |
| T_sec, the secondary's peak temperature | K | its brightness (≈ T¹⁵) and colour |
| Own solids | kg/kg | the smoke's particles (black powder 0.56, the smokeless ~0.001-0.004) |
| α, the particles' mass-extinction | m²/g | the smoke's A per gram (3 for salts, 1.5 for black powder) |
| Water in the gas | kg/kg | the white water-fog puff in damp, cold air |
| Smoke tint | sRGB | the smoke's colour near the muzzle |

**The weather** (the scenario's, today wind only): the wind (from the snapshot's `Weather`), the relative humidity and
the air's temperature (knobs, 0.7 and 15 °C, until the weather has them).

### What it gives for this cut's guns

The reference, double-base cordite unless named, 1% igniter:

| Mount | Charge kg | Fireball m | t_s | t_e | x_M m | P_sec | Smoke A m² |
|---|---|---|---|---|---|---|---|
| 4"/45 single (cased, 0.3% igniter) | 4 | 9.5 | 87 ms | 16 ms | 2.3 | 0.98 | 45 |
| 6"/45 single | 13 | 14 | 130 ms | 23 ms | 3.4 | 0.99 | 370 |
| 28 cm/50 twin | 181 | 35 | 315 ms | 47 ms | 6.3 | 0.99 | 5,300 |
| 12"/45 twin | 204 | 36 | 328 ms | 46 ms | 6.7 | 0.99 | 6,000 |
| 13.5"/45 twin | 291 | 41 | 369 ms | 52 ms | 7.6 | 0.99 | 8,500 |
| 15"/42 twin | 372 | 44 | 400 ms | 54 ms | 8.4 | 0.99 | 10,900 |
| 12"/35 twin, brown powder | 159 | 23 | 209 ms | 36 ms | 5.3 | **0.63** | **200,000** |

So in 1890-1925 every cordite gun flashes every round, and the propellant matters mainly for the brown-powder ships
at the cut's start: a weaker, erratic flash under a huge white cloud.

**Per shot** (display only, from a hash of the record, the same every frame): the secondary flash on or off
(Bernoulli of P_sec), a lognormal intensity scatter (σ 0.25), the fireball's shape seed, and a small start offset per
mount (0-40 ms, a knob), so a broadside's flashes are not clones (the splashes' lesson).

**Where:** the barrel tips. The sim's muzzle is the mount's axis point; the map adds the bearing times the barrel's
reach from the pivot, and ± the spacing for each barrel. The elevation and bearing are the shot's, from its record,
not the turret's drawn bearing a frame later. The flash stays where the gas was put: a ship at 20 kn leaves her
fireball 4 m behind over its 0.4 s, as a real one would.

## The effects camera for flashes and smoke (decision 2)

`splashes.md`'s decision 1 draws tall effects as from a 3/4 view, h metres up going h·tan φ up the screen, with the
**ground exact**. A flash isn't on the ground: it starts at a barrel tip 8-15 m up, and the sprite draws that tip at
its plan position. Lifted by its full height, every flash would float 10 m north of its guns.

So the anchor moves: **the barrel tip is exact; only the height gained above it goes up the screen** (the elevation's
lift along the bore, the smoke's buoyant rise). A splash is the same rule with its anchor on the sea. A gun at 15° puts
its fireball's centre ~7 m above the muzzle, so a big flash leans a little up the screen. A smoke puff keeps the
height it was born at as its anchor, so it leaves the guns without a jump and climbs up the screen as it rises.

The flash's light on the sea and the smoke's shadow use the true 3D positions: the glint pool is under the real
fireball, the shadow under the real cloud.

## The look, by what draws it

1. **The flash**, one instanced quad per mount fired, drawn last (after the smoke and the splash columns: the
   fireball is in front of the smoke it makes, and light wins over a column north of it, which the eye forgives in a
   0.4 s event). Blended additively in display space (or the screen blend, `ONE, ONE_MINUS_SRC_COLOR`, if additive
   clips; chosen from F2's captures). Per pixel, in the bore's frame:
   - **the primary cores**: per barrel, an ellipse at 0.35 x_M, white-orange, cooling 2000 → 1200 K over t_e;
   - **the Mach discs**: per barrel, a small reddish disc at x_M, dished toward the gun, 1900 → 1400 K over t_e;
   - **the secondary fireball** (when on): centred at x_M + 0.7 λ_f, 0.8 λ_f along the bore by 0.5 λ_f across,
     growing 35% → 100% over the first half of t_s, rising over 0.12 t_s and falling as 1 − u²; its temperature hot
     in the core and cooler at the rim, broken by noise;
   - **colour and brightness from temperature**: blackbody luminance (≈ T¹⁵ around 1500 K) and chroma, with 55% of
     the sodium line's yellow mixed in for the secondary (research §2.6: pure blackbody renders salmon). An analytic
     fit in the shader, no table;
   - **analytic normalisation** with σ ≥ 0.6 px, so a 4" flash at 6 m/px is a faint point of the right energy, and a
     15" is a bloomed star.
2. **The halo**, in the same quad: the emitters again with their σ widened by 6 and 18 px, on the compressed source.
   By day it is a faint warm skirt; at night it is most of what a distant flash is.
3. **The flash's light**: up to 8 lights a frame in `LightUniforms`, chosen by intensity and nearness to the screen.
   Each is a point light with I(t) and the fireball's colour, at its true height:
   - `seaShade`: a second specular lobe through the sea's own slope distribution (wind, wakes, frost), so the glint
     breaks up as the sun's does; and diffuse on foam;
   - the ships' shader: Lambert on the sprites' normals with inverse-square falloff, so her own decks and her
     neighbours' light up for t_s;
   - the splash columns and the smoke: through the shared lights, so at night a broadside's cloud glows orange for
     the flash's moment and then goes dark against the moonlit sea.

   By day a light is skipped when it adds under ~1% of the ambient at its foot, which is nearly always.
4. **The blast on the water**, into the wake target in its pre-pass, one quad per mount fired, flat on the sea:
   - the overpressure Δp(r, θ) from Fansler's fit with the directivity β(θ) (μ 0.78), the Mach stem's ×2^⅓ for low
     elevation (blast-wave research §0.7);
   - the front at its arrival time, a thin dark edge (variance −), then the frost (variance +), fading with
     τ_frost ≈ 2 s; p_vis ≈ 2.5 kPa (the photo's footprint) as the knob, raised with the sea state;
   - the scour foam where r < λ′ (the scaled-distance rule), and the jet-impingement lobe along the bore when the
     elevation is under ~10°, in the foam lanes' encoding, polar-streaked;
   - two mounts' fronts meeting make Iowa's bright seam by themselves (additive variance).
5. **The smoke** (see below): its shadow on the sea and the decks after the ships, its puffs after the ships and before
   the splash columns.
6. **The cross-fade**: the table's gun flash is drawn × (1 − `WorldView.Factor`), the world's flash and smoke × the
   factor, as with the splashes. The halo's pixel radii keep a far flash visible at the world view's far end, where
   the table's mark takes over at the same size.

## Brightness without HDR

The background's physical luminance, L_bg, comes from the sun's and moon's altitude and the sky (~1500 cd/m² sunlit
sea, ~300 overcast, ~0.01 moonlit, ~0.001 starlit; research §4.1), computed once a frame on the CPU beside
`ShipLights`. The display's background is the light level the shaders already have (`Sky.w`, linearised). A flash's
emitted value is then

```
display = Sky.w_linear · L_flash / L_bg · gain
```

rolled to white on luminance, not clipped per channel (the research's §8.3: per-channel clipping turns every night
flash flat yellow). `gain` is the one named departure, 1 to start. The night sea is drawn much brighter than physical
(`nightrelief`), so the night ratio is applied to that brighter sea: still far into the white, with the halo carrying
the rest. That is the readable night the realism-then-tune rule asks for.

**Each part's peak is compressed** (user, 2026-10-15: at night the parts blew out to their quads' edges, white
rectangles): a part's Gaussian is scaled so its peak is x/(1 + x/knee), `FlashLook.PeakKnee` = 40 display units, its
shape kept. A day flash's core (~25 over the deck) is only softened where it is already past white, the fireball not at
all; a night one's 10^6-10^8 is held near the knee, so its tails fall to nothing inside the quad. The halo and the light
keep their own compression from the uncompressed parts, so the night's glare stays. The shader also fades everything
to zero toward the quad's rim (a circle round its centre that holds every part's reach), so no brightness shows the
quad again.

**No flash adaptation** (decision 3): a salvo never dims the rest of the scene. The research's eye adaptation is left
out for good.

## Time

- **Within 1x:** the frame average and a floor of two frames' showing, with intensity scaled to keep the Blondel-Rey
  effective intensity (`stretch_for_frames`): a 4" flash held over 33 ms keeps the share of its peak the eye would
  have seen.
- **The flash** plays on the splashes' column clock (knee 2x, power 0.5: 11x at 60x) and is thinned by a hash of the
  **salvo** (a salvo flashes whole or not at all; the selected ship's always do). At 60x a 12" flash is 30 ms on the
  screen, so the floor matters: two frames, as at 1x. The clock and the thinning move out of `SplashLook` into one
  shared effects clock both use (one implementation).
- **The frost, the scour and the smoke** are on the sea's clock, as the splashes' foam is. Every shot keeps its smoke,
  thinned or not, so the gunfire's cloud is whole.
- **The coarse tier** shows none: no gunnery runs there.

## The gun smoke (decision 1: drawn by the map, a logged exception)

The smoke system (the smoke field: `docs/battle/plan.md`, `research/vfx/unified-smoke-system.md`) comes later as one
effort, because keeping funnel, gun and screen smoke performant for a Jutland is a lot of work. Until then the world
view draws gun smoke from the map alone. **It blocks no sight.** That breaks the plan's rule that drawn smoke reads the
sim's smoke; the exception is logged below.

**The puffs may stay when the smoke system comes** (the user, 2026-10-15). Its visibility will be low precision by
necessity, so the drawn puffs can remain the look over its coarser state, rather than be replaced by it. Either way
the puffs are the research's (A, σ_h, σ_z, rise, drift), their state a pure function of age, and the drawing reads only
that state, so the smoke system can feed them, take their totals, or leave them be.

- **A puff per mount fired**, born in the `ShotBook` from the shot's record and its gun's A_p and A_w. Its state is
  closed-form (research §5.3): the jet's forward carry `2.5 λ_f (1 − e^(−t/τ_m))` along the bore, the drift with the
  weather's wind, the buoyant rise `0.6 λ_f (1 − e^(−t/(3 + 0.05 λ_f)))`, the spread σ_h, σ_z = 0.8 σ_h, and A(t)
  ramping in after the flash (research §8.6: full A at t = 0 halos the young flash) with the water fog evaporating
  over τ_w. Nothing is simulated per frame.
- **Merging** holds the count down (research §5.5): a mount's puffs within one σ of each other merge (sum A,
  A-weighted centre, second-moment σ), so rapid fire is a trail, not a cloud of quads; and a ship's old puffs merge
  as they spread into each other. Live puffs scale with the ships firing, not the shots fired. A puff goes when its
  peak τ_v falls under 0.01.
- **The drawing**: one soft quad per puff, premultiplied alpha, sorted north to south without allocation, through the
  effects camera (anchor at its birth height). Per pixel, `alpha = 1 − e^(−τ_v)` from the Gaussian's τ_v, its edges
  broken by noise fixed to the puff and growing with it (the integral kept, so the coverage stays honest). Lit by the
  shared lights with a high albedo (0.9, salts and brown powder white, cordite a little warm near the muzzle for its
  first ~5 s), a wrap diffuse and a two-tap self-shadow toward the light (τ sampled one σ toward the sun), and lit by
  the flash lights. The water fog is pure white. The quad is cut where τ_v < 0.01.
- **The shadow**: per puff, a quad laid down-sun on the sea and the decks, darkening by `1 − e^(−τ)` of the puff
  projected along the light, as the ships' shadows darken by opacity; by day the strongest height cue a cloud has.
  It follows the stronger light and fades at low sun, as the splashes' shadows do.
- **Its clock** is the sea's, as the splashes' foam is (the world's speed, capped at 8x): above the cap the smoke
  outlives the world's time, so each puff is fainter by the foam's rule. Its drift runs on the same clock. The smoke
  system will put smoke on the world's time; this choice is the drawing's alone.
- **Smoke in front of a flash** does not dim it yet (later).

## Restrictions

- **Display only.** The sim never reads a drawing. The sim does gain a record (F0) of each mount fired, which the
  smoke system and night sighting by gun flashes will read later.
- **Realistic, then tune:** the research's sizes, timings, temperatures and smoke first. Departures are named knobs in
  a `-flash=` flag (`FlashLook`), each recorded here: the effects camera's anchor, the frame floor, the brightness
  gain, the start offsets, the halo radii, the smoke's clock.
- **Lightweight, measured:** in a Jutland-sized exchange at 60x and 1 m/px, 1080p (`-gputime`): flashes, halos, lights
  and blast marks under 0.15 ms GPU; smoke and its shadows under 0.3 ms. The CPU's builds together under 0.1 ms.
  Nothing allocated per frame.
- **Pipeline:** no new pass and no new target. The blast marks go into the wake pre-pass; the smoke's shadows, the
  smoke and the flashes into the main pass after the ships; the lights into the existing uniforms. If the smoke's
  overdraw at the far zooms beats the budget, a half-resolution smoke target comes back as a named exception, not
  before.

## Logged exceptions (approved 2026-10-15)

- **The smoke field's offscreen target** (approved by the user, 2026-10-21; docs/battle/smoke.md Z5), against the
  restriction above (no new pass, no new target): a half-resolution RGBA16F target with mips and two collapse chains,
  their passes before the main pass, and a resolve in it. The gun smoke drawn here is untouched by it.

- **Gun smoke drawn by the map alone** (decision 1), against `plan.md`'s "the smoke that's drawn is a look that reads
  that state". It blocks no sight. When the smoke system comes, the puffs may stay as the look over its coarser
  state (the user, 2026-10-15): the exception is then revisited, not simply ended.

## Steps

- **F0. The record.** The sim records each mount fired: shooter, class mount, time, bearing, elevation, barrels, the
  salvo, the mount's axis point. Written where `GunnerySystem.Fire` already calls `Combat.Fired`, kept for the
  snapshot's while like the splashes, read as a `ShotsView`. The map keeps its own ring of shots seen (`ShotBook`), as
  `SplashBook` does. Tests: a salvo of a twin and a triple makes two records with the right barrels and bearing; a
  ripple (records apart in time) animates apart.
- **F1. The gun.** `MuzzleFlashModel` (the port of `derive()`), the inputs above as data with the families as presets,
  the placeholders, a cache per turret type. Tests against the reference's ladder (research §6) to a few percent.
- **F2. The flash.** `Include/muzzleflash.glsl` (the one model), the instance build, the draw: cores, discs, the
  fireball, colour from temperature, brightness from the contrast. Captures (`-fire` at a point, `-screenshot`) at
  0.25, 1 and 4 m/px, at noon, 19:30 and 23:30, a 6" and a 12" twin, the screen's centre and edge.
- **F3. The halo and time.** The halo's radii; the frame average and the floor; the shared effects clock and the
  thinning; the cross-fade with the table's flash. Checked live at 1x, 10x, 60x and 240x, and zooming through the
  boundary.
- **F4. The light.** The flash lights in the uniforms, the sea's glint and the ships' and columns' lighting. Night
  captures of a broadside, then side by side with F2's.
- **F5. The blast on the water.** The variance front and frost, the scour foam. Captures of a 12" twin at 20°
  elevation at t ≈ 0.2 s against the Iowa photo's footprint; low-elevation fire for the jet lobe.
- **F6. The smoke.** The puffs' closed-form state in the `ShotBook`, the merging, `Include/gunsmoke.glsl`, the draw
  and the shadow, lit by the sun, the moon and the flash lights. Captures of a 12" twin's salvo at 0, 5, 30 and 120 s,
  a brown-powder broadside, a destroyer's 4" firing rapidly, day and night. Tests: merging keeps A, the centre and the
  second moment; nothing allocated a frame.
- **F7. Tuning by eye** against period photos and night footage (the rise first: research §5.3 calls it the most
  sensitive knob), and the cost in a Jutland-sized exchange at 1x and 60x (`-gputime`, DevRelease `-frametimes`).

### Done

**F0 (2026-10-15).** `ShellSystem.Shot`, written per mount by the dev's fire and by `GunnerySystem.Fire`, kept in a ring
(`ShotCapacity`) and read as `Snapshot.Shots` (`ShotsView`) for `SplashLifeMs`. Tests:
`ShellSystemTests.A_salvo_records_each_mount_that_fired`, and the gunnery path in
`GunneryTests.Turrets_train_onto_the_target_and_only_those_that_bear_fire`.

**F1 (2026-10-15).** `PropellantRow`, `ChargeRow` and `MuzzleDevice` (Rows.cs), the six families (`Propellants`),
`DesignerPlaceholders.Charge` and `ShipClass.Charges` (one per turret type), and `Sim.Combat.MuzzleFlash` (the port of
`derive()`). The designs carry an era, not a year, so "before ~1893" became a Victorian gun under 36 calibres; France
and Russia took single-base beside the USN (poudre B, pyroxylin), Germany diglycol from the treaty years. Tests
(`MuzzleFlashTests`): the reference's numbers to 0.1% for a 16"/50 SPD, a 12"/45 twin, a brown-powder 12"/35 twin, a
cased 4" and a salted howitzer; the charges by navy and era.

**F2 (2026-10-15).** `Map/Flashes/`:
- `ShotBook`: the shots seen, the flash's clock (the splashes' column clock), each shot's own draw decided once;
- `FlashEmitters`: the research's parts per shot and age, averaged over the frame;
- `FlashPhotometry`: blackbody luminance and colour, a lit white deck's luminance by the sun's and moon's elevations;
- `FlashRenderer`: the instances (all physics on the CPU, the shader only splats) and one screen-blended draw, last
  in the ships' layer;
- `FlashLook`: the knobs, `-flash=` (gain, halo, haloscale, knee, offset, scatter, rh, air, parts, at, on).

The shaders `muzzleflash.vert`/`.frag`. The turret type's row gained `TipM` (Shipgen's `Geometry.BarrelTip`, one
function for the drawn barrels and the flash), and the pipeline builder a screen blend. Tests (`FlashTests`): the
photometry against the research's table, the halo's closed form against a numeric integral, a frame's average keeping
a 15 ms flash, the book taking each mount once, nothing allocated a frame.

Seen (captures with `-engage` and `-flash=at:0.12`, a dev still at that age of the newest salvo):
- at noon, 0.5 m/px: ragged orange fireballs with white-hot middles, ~40 m, off the trained turrets' muzzles;
- at dusk: whiter, with deep orange rims;
- at night: white-hot blocks, the 6" casemates' flashes merged amidships;
- at 4 m/px: a small warm point by day, a glowing star by night (the halo).

The dev's `-fire` leaves the turrets at rest, so its flashes stand off empty barrels; captures use `-engage`.

**F3 (2026-10-15).** The halo was built with F2. The effects camera's pitch and the two display clocks moved out of
`SplashLook` into `Map/Effects.cs` (`Rate`, `SeaRate`, `KeepShare`, `SeaStrength`), which splashes and flashes both
read; `-splash=` still sets them. A shot's flash shows by its salvo's share, decided when it is first seen (the
selected ship's always). The floor (`FlashLook.FloorFrames`, 2, `-flash=frames:`): a flash shorter than two frames
plays over them at its Blondel-Rey intensity (`FlashRenderer.BlondelRey`). The table's gun flash is drawn × (1 − the
world's share). Tests: the floor against the research's 4 ms example (14% of the peak over 33 ms), a salvo flashing
whole at about the keep share at 60x, the selected ship's always. **Not yet checked live** at 10x-240x and through
the zoom boundary: for the user's eye.

**F4 (2026-10-15).** `LightUniforms` gained the flash lights (count, the view's axes, 8 positions eye-relative and 8
intensities); `shiplight.glsl` declares them with `flashAt()`. `FlashLights` gathers them (within reach of each other
summed, the brightest 8 kept); the scene sets them into the frame's light before the splashes, ships and sea build.
- the ships: diffuse on the sprites' normals (`shipShade`'s new extra light), the hull's tilt left out;
- the sea: the glints through its own slope distribution, and the foam;
- the splash columns: along their march.

Changed on the way, two departures recorded here:
- **The light thrown is the flash as drawn** (`FlashRenderer.Thrown`): its displayed brightness, compressed as the
  halo's source, times a·b. A surface d away sees the flash's drawn brightness × a·b/d², at any exposure. The physical
  light, with no eye adaptation (decision 3), lit the night sea white-green for kilometres.
- **No light into the water's body**: under the guns it came out a teal glow.
- **Clustered by distance, not by ship**: a ship's merged light sat between her turrets, its glint a bright spot on
  empty water.

Seen: at night the decks near the guns warm, nearby splash columns and foam take the orange; at noon a faint warmth on
the decks by the turrets. Tests: the thrown light's compression, the lights' gathering and cap.

**F5 (2026-10-15).** `BlastRenderer` and `muzzleblast.vert`/`.frag`: one quad per mount fired into the wake target in
its pre-pass, on the sea's clock (`ShotBook.SeaClock`, kept 25 s for the scour). The front's arrival is a 16-node table
integrated once on the CPU (`ArrivalAt`, the shader reads the same nodes). Knobs: `pvis`, `psat`, `frost`, `scour`,
`scourk`, and `blastat:S` for stills. Test: the arrival against the reference's LUT (within 0.02 of λ'/c₀).

Seen at noon, 1 m/px:
- at 0.2 s: radial scour streaks off each turret and a frosted disc spreading;
- at 1 s: the two turrets' discs a heart across the ship's side;
- at 4 s: the frost gone, the streaked scour fading.

Changed on the way:
- **The leading edge takes off at most half the wind's slope variance.** The wake target can't know the sea's own
  variance, and the clamp keeps a calmed ring from going mirror-flat.
- **A shader bug worth remembering:** `pow(x, 2.0)` of a negative x is undefined in GLSL. Its NaN, times a zero
  weight, still reached the wake target and shaded as white arcs at the front.
- **A process lesson:** `--no-build` runs the binaries' copy of the shaders, so recompiled `.spv` files only count
  after a build. Two of the arcs' tests had compared stale shaders.

Open, for tuning: from above the frost reads as a duller, less glittering patch, not the photo's silvery one. That
needs the sea's diffuse sky scatter to grow with the slopes' spread (`seaShade`), which it doesn't yet.

**F6 (2026-10-15).** `SmokeBook` (the puffs: spawned from the shot book's new shots, closed-form in their age on the
sea's clock, merged per ship by moments, swept when their peak τ falls under 0.01), `SmokeRenderer` and
`gunsmoke.vert`/`.frag`. One premultiplied draw for the shadows, then one for the puffs, after the ships and before
the splash columns, sorted north to south. The book entry gained the gun's smoke (A_p, A_w, τ_w, tint). A dev flag for
captures: `-weather=wind_from,kn[,visibility_km]`. Tests: a puff's spread against the research's §5.3 formula (67 m at
120 s in 5 m/s) and its drift (608 m north); ten calm puffs merging into two, their area kept; six windy ones apart;
nothing allocated a frame.

Seen (noon, 2 m/px, a 12-knot southerly):
- a 15" twin's salvo: two dense white puffs off the muzzles, then a plume widening downwind for over a kilometre,
  about 30 puffs after merging;
- Devastation's 12" muzzle-loaders on brown powder: a huge billowing white cloud each round, drifting off between her
  slow shots;
- at night: a faint dark-grey plume over the moonlit sea, lit orange by the flashes.

Changed on the way: the puff is shaded as a dome seen from above, with its far side from the light a little darker
by the share of its depth toward the light. A self-shadow by the depth itself darkened every thick puff's centre into a
ring.

**F7, the cost (2026-10-15).** The stress, as the splashes': 16 battleships at 1.1 km pairwise engaged in one 2 m/px
view at 60x, 1080p, DevRelease, a 12-knot wind, 2,400 frames, alternating runs with `-flash=on:0`. The first
measurement was far over budget: +0.8-1.5 ms GPU. At 60x the 6" secondaries fire hundreds of mount-shots a real
second, and the blasts and puffs piled up on the slower sea clock.

Fixed:
- the blasts are thinned by the salvo's share, as the flashes are; their quad sits over the mark's footprint (ahead
  of the bore); pixels with no mark are discarded before the noise;
- the smoke's side shading uses the smooth Gaussian; the second noise octave runs only on puffs big on the screen;
  puffs are cut at 5% opacity, faded by a radial window so no quad's edge shows; their shadows are drawn only where
  they fall clear of the puff; merging is within 1.5σ.

The last clean set (three runs each; a later set was disturbed by load outside the runs):

| | Effects off | Effects on | Added |
|---|---|---|---|
| GPU, scene, mean | 1.12 ms | 1.53 ms | +0.41 ms |
| GPU, scene, p50 | 1.13 ms | 1.41 ms | +0.28 ms |
| GPU, scene, p95 | 1.18 ms | 2.2 ms | +1.0 ms |
| CPU, the effects' build | 0.15 ms | 0.24 ms | +0.09 ms |

Nearly all of the GPU's share is the smoke (everything else together +0.04 ms): about 400 puffs covering 1.7 screens
at the peak. The mean is inside the budget (0.45 ms); the p95, in the heaviest smoke, is not. Nothing allocates a
frame. **Open, for the user:** accept it, or take the half-resolution smoke target as a logged exception
(the plan's fallback).

**F7, the user's first round (2026-10-15).**
- **"The gun blast's foam a tad more broken."** It is broken now as the splashes' foam is: patches, clumps and
  bubbles (4, 1.5 and 0.6 m), each faded as it nears a pixel, the radial streaks a quarter of the pattern, a sharp
  threshold over that, and the churned water under it patchy too. Before, it read as flat pale wedges.
- **"The original puff looks too much like an opaque ball."** Three named departures (`FlashLook.Smoke*`, `-flash=`
  `smokecap`, `youth`, `stretch`, `billow`):
  - the drawn depth capped, τ/(1 + τ/1.5), before the billows, so a young core is ~78% opaque with lobes through it;
  - stretched along the bore by up to 2.2 while young (over ~8 s, the gas jet's carry), its area kept;
  - its billows' contrast raised by 1.6 while young.

  Old smoke is unchanged. Small round holes read as dark dirt, so the billows never empty a spot below 30% of its
  depth, and a young puff's shadow is faint. Value noise at that contrast showed its grid, so two lookups turned 37°
  are averaged.

**F7, the user's second round (2026-10-15).** "The smoke has a fixed noise pattern, so it still looks like a brick
coming out of the barrel; as an effect it may sacrifice some realism." Three effect departures (`FlashLook.Smoke*`;
`-flash=churn`, `warp`, `stem`):
- **The billows churn.** The noise turns over by 2.2 ln(1 + age/0.4 s): fast at first, slowing as the puff ages. A
  second noise warps it, so the billows roll instead of sliding.
- **The billows grow slower than the puff while it's young** (cells 0.9 σ^0.8 against old smoke's σ/1.3), so it gains
  billows rather than magnifying them. They're strongest at the edge: lobes out of a cloud, only a mottling inside.
  The young contrast and warp are 1.0.
- **A stem of gas joins the puff back to the muzzle** while the jet runs, tapering toward the barrel and fading over
  ~1.5 s. The puff reads as expelled rather than a block leaving the gun.

Seen at 0.5 m/px over the first seconds: ragged puffs streaming back to their barrels, churning frame to frame, that
settle into the round cloud. The cost is unchanged in the stress (+0.34 ms GPU mean on a clean pair).

**Still to do in F7: tuning by the user's eye.** Things to look at:
- the flash's size and brightness by day, and the white-out of its core by night;
- the frost's look (duller, not silvery);
- the smoke's density over the ships (a broadside's own smoke hides much of the line in the stress);
- the rise and spread (research §5.3 calls the rise the most sensitive knob);
- live at 10x-240x.

## Decisions (the user, 2026-10-15)

1. **Gun smoke now, drawn by the map** (a logged exception). The smoke system comes later as one effort, since keeping
   it performant is a lot of work.
2. **The barrel tip is the effects camera's anchor** for flashes: height above it goes up the screen.
3. **No flash adaptation**, now or later: dimming the scene doesn't fit this kind of game.
4. **The propellant placeholders as proposed** (double-base cordite for every navy, single-base for the USN, brown
   powder before ~1893), and the model's inputs documented as data (*What the model takes*), for the propellant and
   ammunition system designed later.

## Later

- With the smoke system: its coarse field for sight lines, funnel smoke and screens; the gun puffs kept as the look
  over it, or fed from it.
- The vortex ring, the jet punching a tunnel through the gun's own smoke, the flash dimmed by smoke in front of it.
- Night sighting by gun flashes (the research's Blondel-Rey and Allard ranges), read from the shot record by the sim.
- Embers and burning bag fragments from bag guns at the closest zoom.
- The sun-shadow line riding the front, and the shock's jolt through smoke.
- The Wilson haze shell, humid days only, the heaviest guns.
- Flashless and smoky charges as a later cut's player choice (the research's night charge).
- The salvo's ripple, with the fire-control rework: the record already takes a time per mount.
