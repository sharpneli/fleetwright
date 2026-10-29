# The night in the world view (night.md Q10; plan agreed 2026-10-29)

The look of a night action from close in: the dark itself, searchlight beams, star shell, the moon on the water, and
the small lights that make a night fight feel like one. The **world view** only, as the other visuals plans: what it
draws × the world's share (`WorldView.Factor`), the table's marks (`night.md` Q9) × (1 − it). The table is a chart and
keeps its night.

The user's brief (2026-10-29): **make the nights quite dark again and go back on the amplified starlight**; the effects
**lightweight but looking good**, **based on realism with emphasis on the feeling of night fighting**. As always:
realistic first, then tune by eye (`realistic-then-tune`); looks never feed the sim and never say more than it knows
(`sim-crunch-vfx-fluff`); nothing changes shape with the clock's speed; nothing allocates per frame.

## What the feeling is

From the record (`research/simulation/night-fighting.md`): a night action was a few minutes of blinding light in a
great deal of dark. Ships close to a mile before anyone sees anything; then a challenge's coloured lamps, a dozen beams
snapping on at once, gun flashes, a ship lit white from end to end while her enemy stays black; a burning ship a beacon
for miles; then the dark again, worse than before because every eye is dazzled. The Russians at Tsushima found their
beams made "only a luminous mist" in the haze. The bursting shells were "brighter than the searchlights".

So the look's job: **a dark that is really dark**, so that every light means something (a beam is a ship giving
herself away, a flare is someone looking for you), and **lights that are real lights**: they light what they reach,
throw shadows, glow in the haze and smoke, and keep their colour while the moonlit world goes grey-blue.

## What there is today (checked 2026-10-29)

- **The night is amplified.** `SeaView`'s night light (`StarNightRad` 0.08 0.10 0.15, `MoonNightTint`, `MoonNightGain`
  1.2, relief 7: `sea.md` "Low sun and night") gives the water a light of its own so its waves read; the ships' ambient
  (`LightTable.SkyNight` 0.055 0.065 0.09) is about 0.3 of the day's, "the readability floor". A moonless world sea is
  median 7 21 39 (8-bit RGB), a full moon's 10 27 46: a navy, not a night. Physically a moonless sea from above is black
  (1 mlx starlight, ~2% reflected) and a full moon's (0.25 lx) is ~250× brighter than that.
- **The haze at night** sinks into the night sea's colour (`haze.md` decision 5); it reads the sea's night light, so it
  follows whatever the sea does.
- **Flashes are photometric** (`muzzle-flashes.md`, "Brightness without HDR"): display = the deck's level × L_flash /
  L_bg × gain, the night ratio "applied to the brighter sea". They light the sea's glints and foam, the ships, the
  splashes (`FlashLights`: 8 flashes + 8 fires in the `Lights` block). **No adaptation**: a salvo never dims the scene
  (`no-exposure-dimming`).
- **The fires' light** (`hits-and-fires.md` I4) is in the same block.
- **Beams and flares in the world view are placeholders** (`MapScene.Contacts.cs`): a thin line along the beam's
  bearing, 4 km; a small disc over the flare. They light nothing.
- **The moon**: its light on the ships (with shadows, by the stronger of sun and moon), on the sea's body and in its
  glints (`seashade.glsl`); not dimmed by the cloud yet (`night.md` "Open after Q1").
- **What the sim gives:** `Snapshot.Beams` (lamp, bearing, cd, ship) and `Snapshot.Flares` (place, cd). The sim also
  knows a beam's spread, its iris (`SearchlightOpenShare`), a flare's burst time, burn, fall and drift (closed form,
  `StarShellSystem`), a challenge's lamps (`ChallengeSystem`) and a funnel's glow when forcing (`SightFunnelGlowCd`).

## The model

### 1. The dark: one eye for the night

The display's light level comes from the **physical light** at the view's centre, `NightLight.At` (the sim's own: sun's
twilight curve, moon by phase, height and distance, starlight, **under the scenario's cloud**), through one curve, the
**night eye**: a partly adapted eye, log in the illuminance, so the night's levels keep their order and their ratios
shrink.

| Light | Illuminance | White deck shown (share of the day's) [EST, first guesses] |
|---|---|---|
| Day | 10⁴-10⁵ lx | 1 (as now) |
| Sun −3°, civil twilight | ~60 lx | ~0.35 |
| Sun −6° | ~3 lx | ~0.15 |
| Full moon, high | ~0.25 lx | ~0.07 |
| Quarter moon | ~0.03 lx | ~0.035 |
| Moonless, clear | ~10⁻³ lx | ~0.012 |
| Moonless, overcast | ~3·10⁻⁴ lx | ~0.008 |

- **Everything takes its light from it**: the ships' ambient and moon (`ShipLights`), the sea's night light
  (`SeaView.NightLight`, which the haze reads), the table/world hand-over. The tuned amplification (`StarNightRad`,
  `MoonNightGain`, `SkyNight`'s floor) goes; the night relief stays, smaller, so a moonlit sea still shows its swell.
- **The flashes' L_bg is the same eye**: a flash is as many times brighter than the drawn deck as it is than the real
  one, with no "brighter sea" left to apply it to. One photometry for flashes, fires, beams and flares.
- **The night's colour (Purkinje):** what the night's own light shows is drawn desaturated and cool (the eye's rods
  see no colour; the film convention reads it as blue-grey). **Artificial light keeps its colour**: in a beam or under a
  flare the grey hull is grey, the fire orange, the foam white, the rest of the world blue-black. A per-light split in
  the shaders the lighting already has (the ambient and moon terms vs the point lights), no pass of its own.
- **Readability is the HUD's job, not the light's** (decision 1). On a moonless night an unlit ship is a faint dark
  shape against a black sea at close zoom. So the world view gets **night ticks**: four small corner ticks about each
  ship the player knows of (his own, and the contacts as the picture holds them, at the contact's place, never the
  truth), in the side's colour, a few pixels whatever the zoom, fading in as the night eye's level falls (none by day,
  full on a moonless night). Table marks, not light: they don't light anything and the haze doesn't take them.

### 2. Lights in the block: flares as high point lights, beams as spot lights

The `Lights` block gets a **third pool, the night's lights** (8 [EST]): a flare is a point light (as a flash, but high
and long), a beam is a **spot light** (its lamp, axis, the cosine of its half spread and a soft edge for the iris and
the beam's falloff, `SearchlightModel`'s shape). Kept like the fires' pool: past eight, by light over distance from the
view's centre. Every shader that reads the flashes reads these with one more loop (the spot's cone a single dot).

- **Under a flare** a pool of light on the sea, E = I·h / (h² + r²)^1.5 (300 m up: a broad pool, its edge where it falls
  into the night). The water's **body** takes it (the flashes light only glints and foam; at night the body is what
  shows a lit pool): ~2% reflected, so a 1 lx pool against a 1 mlx night reads as a grey-blue disc with the waves in it.
  Its glints come from the existing glint code: a glitter patch between the flare's ground point and the eye.
- **In a beam** the water glances (the beam comes in nearly flat, the sea reflects it away from the eye): little on the
  water's body, the foam, wakes and splashes caught bright, and the ship it holds lit white on the side facing the lamp
  (the sprites' normals already shade a side by its light's direction).
- **Shadows from the night's lights**: a ship under a flare throws her shadow away from its ground point, long, the
  flare beating the moon; a beam's target throws hers along the beam. Per ship, the dominant light's direction
  (sun, moon, or the strongest night light at her), into the existing shadow pass's per-instance direction.

### 3. Beams in the air

The beam seen from the side: light scattered out of the cone toward the eye. Straight down, the view ray crosses the
cone across its width, so the in-scattered radiance is closed form (single scattering, a thin cone):

    L(r) = E(r) · σ · p(θ) · w(r),   E(r) = I / r² · e^(−σ r),   w(r) the cone's height above the sea at r

- **σ is the sim's haze** (`SightModel.Extinction`, the lookouts' and the drawn haze's own), over a clear-air floor
  (`SearchlightClearAirPerM`, the sim's); **p(θ)** a marine aerosol's phase function (Henyey-Greenstein, g ≈ 0.7) at the
  scattering angle to the eye (~90° looking down). So a clear night's beam is a thin pale streak and a hazy night's a
  luminous bar, as Tsushima's: one model, the visibility decides.
- **Brightness** (a 36-inch, 8·10⁷ cd, 2.75°, a 5 km haze, moonless): ~0.01 cd/m² at 1 km, ~0.003 at 4 km, against a
  sea of ~10⁻⁵: white, falling off as 1/r along its length. Under a full moon (sea ~3·10⁻³) the same beam is a faint
  silvery bar. Realistic; the gain is the tuning knob.
- **In smoke** the beam blooms: the smoke field's column τ under the pixel (its collapsed half-resolution target,
  already made before the main pass) adds to σ by the share of the column inside the beam's height [EST]. A beam
  through a smoke screen or a burning ship's pall lights the smoke where it passes.
- **Stopped by her target**: past the ship it holds, the beam continues dark behind her (her shadow in the air) for a
  length, then fills in by the spread. Needs the beam's target in the snapshot (`night.md` "Open after Q9").
- **The lamp**: a hard white point at the projector, the mirror's spill; the beam's first metres brightest.
- **Time**: the iris opens over ~1 s (the record: "rapid"), the beam sweeps at the sim's rate, a doused beam closes
  in the same second; the arc stays struck behind the shutter (nothing shows).
- **Colour**: the carbon arc, white with a faint violet-blue against the fires' orange [EST: a crater at ~3,800 K].
- One quad per beam, drawn after the ships, additive, through the haze (`hazeThrough`). Tens of beams at most.

### 4. Star shell

A star shell bursts 300-370 m up, its star burns ~15 s (10-25), falling fast on its own or ~6 m/s on a parachute,
drifting with the wind: all in the sim (`StarShellSystem`, closed form in the age).

- **Drawn through the effects camera** (`effects-camera`): the ground under it exact (the lit pool, the shadows), its
  height up the screen. A flare hangs over the pool it lights.
- **The burst**: a pop of light and a small grey puff at the burst point, the puff drifting and fading (~30 s).
- **The star**: a blinding point (magnesium white, faintly green-yellow [EST]), flickering a little as it burns, with
  its **halo in the haze**: the air's in-scatter round a point light, closed form along each view ray
  (∝ σ·p·I / d·angle), so a hazy night wraps a flare in a glow and a clear one shows a hard point.
- **The trail**: the smoke it leaves above as it falls, a short ribbon lit from below by the star (brightest just over
  it, fading up), drifting with the wind and lingering after burn-out (ephemeral: under a minute).
- **Burn-out**: the last second dims and reddens; the pool shrinks with it.
- The halo serves the haze's open item too ("lights glowing in the haze"): the same term for the fires and flashes.

### 5. The moon

- **Dimmed by the cloud**: ships' moonlight, the sea's, the night eye all from `NightLight.At` under the cloud.
- **The moon path**: the sea's existing moon glint. From straight above it shows only for a high moon (a facet must
  tilt halfway between the eye and the moon), as it should; checked and tuned, not invented.
- **Moon shadows**: there (the stronger of sun and moon); kept, softened by the cloud.

### 6. The small lights (the feeling)

- **Funnel glow**: a dull red flicker at the funnel tops of a ship forcing her boilers (the sim's cue, `SightFunnelGlowCd`;
  the research: coal at forcing, oil-fired destroyers at high power), its reflection in her smoke. Needs the cue in the
  snapshot.
- **Challenge lamps**: the challenge's coloured lamps (a few seconds, the record's red, green and white groups) at the
  challenger's mast, from `ChallengeSystem`. Needs the challenge in the snapshot (the Q9 open item "the challenge lamp
  isn't marked").
- **Fires and flashes in the haze**: the halo above (fires already light the sea and ships).
- **Phosphorescence** (decision 5): a glowing bow wave, wake and torpedo track on nights the sea glows, blue-green in
  the fresh foam and turbulence (the wake target's foam, already there), fading as it settles. A weather field of the
  scenario's (`phosphorescence`, 0 to 1; none by default) the look reads now; the sim's sighting cue from it comes later
  (night.md, open after Q10). **An approved exception** to looks never saying more than the sim knows: until the cue
  is in, a glowing wake shows the player what the lookouts don't weigh. Only for what the world view draws anyway
  (own ships, contacts, torpedoes already shown), so it shows no ship the player can't see.

## Steps

**Q10a The dark.** Baseline captures (the night scenarios' nights: moonless clear, overcast, quarter, full moon, sun
−6° and −12°, at 0.3, 1 and 3 m/px; `-screenshot`, median 8-bit RGB). The night eye: one curve from `NightLight.At`'s
illuminance (under cloud) to the display's white deck, read by `ShipLights`, `SeaView`'s night light and the haze; the
amplification removed; the flashes' L_bg on the same eye; the Purkinje split (night light desaturated and cool, lights
in colour); the night ticks. The table/world hand-over measured (the table may stay brighter: decision 3). Captures side by side, before and after.

**Q10a done 2026-10-29.** `ShipLights.Compute` (`ShipLight.cs`): by day the light table as before; once the sun is
down (handing over through its last 2°), the real light at the view's centre, `NightLight`'s terms under the scenario's
cloud (twilight and stars through it, the moon direct through what's clear and diffuse through the overcast), times
the night eye's display level over the real lux (`LightTable.NightEyeLogLux`/`NightEyeLevel`, log-linear; its last row
meets the dusk's ambient at sunset). The moon, the sky's ambient and the sea's from below all from it; `SkyNight` and
`SeaNight` give only their chroma; the old floor (`MoonStrength`, the night sky's 0.3 of a day) went. `ShipLights.Lux`
carries the real light: the flashes' and magazines' background (`FlashRenderer.PerCandela`) now reads it, so they
stand on the same eye (by day it takes no cloud, as the day's model doesn't). The sea's night light
(`SeaView.NightLight`) is no longer a light of its own: `NightShare` 0.6 of the sky's ambient taken from all round and
given a direction for the swell (`seashade.glsl` keeps the rest all round), so the level is the real night's. The
Purkinje split: `LightUniforms.Night` (the rods' share by the lux, `PurkinjeLogLux` 10 lx to 0.03 lx, and their tint
`RodTint`), `shiplight.glsl`'s `nightSee` on the natural light in the ships, the sea (body, sky, sun and moon glints,
the foam's natural light), gun smoke, the smoke field, splash columns and stains, magazine bodies and trails,
torpedoes; the flashes' and fires' light added after it in colour; the haze's night colours on the CPU twin
(`ShipLights.NightSee`). The night ticks: `MapScene.DrawNightTicks`, oriented along each own hull (length by beam, a
few pixels out), a square about each contact in sight, faded in by the same lux range times the world's share.

Median 8-bit RGB, Dover, three ships, 0.8 m/px, 10 km visibility (before → after):

| Night | Before | After |
|---|---|---|
| Day | 49 70 89 | 49 70 89 |
| Sun −3° | — | 13 20 29 |
| Sun −6° | 7 21 39 | 12 19 29 |
| Full moon (7 Apr 1917, 23:30) | 11 29 49 | 7 9 15 |
| First quarter | 8 23 41 | 5 8 13 |
| Moonless | 7 21 39 | 3 4 7 |
| Moonless, overcast | 7 21 39 | 2 2 2 |

The night's order is back (a moonless night darker than a moonlit one, overcast darker still, twilight lighter than
all). The chart past the boundary keeps its navy (7 19 36, decision 3): the zoom darkens through the fade. A night
salvo lights its ship warm out of the black. Knobs: `-lights=eye:1,below:0.06,purkinje:1,moonsky:0.2`,
`-sea=nightshare:0.6,nightrelief:7`. The ship viewer's panel has the eye's gain and the rods' share. The world view's
land at night is still the chart's fill (`globe.frag`'s `NightLand`), lighter than the sea now: with the coast's look
(`sea.md`, later).

**Looked at by the user 2026-10-29: good, on an OLED display.** On an ordinary display the dark end may crush or wash
out; the eye's levels (`NightEyeLevel`, `EyeGain`) get tuned from playtest feedback on other displays.

**Q10b Lights in the block.** The night's pool (flares as point lights, beams as spot lights) in `LightUniforms` and
every shader that reads the flashes; the sea's body lit by the night pool; the per-ship shadow direction from the
dominant light. The snapshot's beams gain their spread, iris share and target (sim data only); its flares their burst
time, burn, fall and drift (so the renderer moves them closed form). Placeholders keep drawing the beam and flare
themselves until c and d.

**Q10b done 2026-10-29.** The snapshot carries the look's data (sim data only): a beam's spread, its sweep's rate,
when its iris opened and whom it's held on, with the look's time (`BeamsView`); a flare's burst point and time, drift,
fall and when it goes out (`FlaresView.Where`, the system's own motion). `Map/Night/NightLights` gathers them at the
frame's time before the ships are placed (a sweep carried on between looks by its rate, the iris opening over 1 s), then
puts them into a third pool of eight in the light block (`FlashLights.AddNight`; `LightUniforms.MaxLights` 24, a
`FlashDir` a light: a beam's axis and 1 / σ² of its Gaussian, the sim's `SearchlightModel` profile; `flashInfo.y` where
the night's start), in physical units through the frame's night eye (I cd × the display's level a lux, the flashes'
own photometry). Every world shader takes them through `flashAt`: the ships lit white on the side facing a beam or a
flare, the smoke and the splashes too. The sea's body takes the night's (and only the night's) lights, as a level sea:
a flare's pool with its glitter; a beam, grazing, lights the glints and the foam along its lane and hardly the body. The
sea's shoulder now keeps a surface's hue (`shoulderHue`: the brightest channel rolled off, the rest in proportion, to
white as it's overexposed): the pool had gone cyan. The ships' shadows: a night light that outshines the night at a ship
(`NightLights.Strongest` against `ShipLights.Lux`) casts her shadow away from it, its opacity by the frame's own rule;
a flat beam casts none (its shadow in the air is Q10c's). Knobs: `-night=on:1,gain:1,iris:1`. Cost: within the noise
(a flare, a beam and three ships at 1 m/px, p50 1.5-1.8 ms on or off, the machine shared). Tests: 207 and 875 pass.
The placeholders (a line for a beam, a disc for a flare) stay until Q10c and Q10d.

**Q10c Beams in the air.** The in-scatter quad (σ, phase, 1/r, the cone's height over the sea, the smoke field's τ),
the lamp's point, the target's shadow in the beam, the iris in time. The world view's placeholder line goes.

**Q10c done 2026-10-29.** `Map/Night/BeamRenderer` (`beam.vert`/`beam.frag`): a quad on the sea under each beam the
night's lights hold, from the lamp out to where its glow falls under 0.003 of a lit deck, as wide as 3.5 σ; the glow
closed form for a Gaussian beam crossed straight down and cut by the sea, L = I σ_s p √(2π) s / r · e^(−x²/2s²r²) ·
Φ(h / s r) · e^(−σ r), softened to the lamp's glare over its first 6 m. Its numbers are the sim's: the peak, the spread,
the air's scattering (`SearchlightModel.ScatterPerM`) and the share seen from beside (all round, as `ColumnCd`), so the
drawn beam is the lookouts' beam; through the night eye (`FlashRenderer.PerCandela`), rolled to white on luminance as
the flashes are, through the haze, screen-blended, after the gun smoke and the magazines and before the smoke field.
Past the ship it's held on, her shadow in the air (the share of the beam's height her top hides, widening with it):
faint for a destroyer under a 25 m lamp, as it should be. A clear night's beam and a 5 km haze's differ in how fast
they fade along their length. **The smoke field takes every point light** (`smokeresolve.frag`: the flashes, fires,
flares and beams, from the smoke's place under the pixel rebuilt from the view at `SmokeFieldRenderer.LitHeightM` 20 m
[EST], lifted by the effects camera): a beam crossing a destroyer's smoke blooms white there, a flare lights the smoke
under it, and the fires now light their own palls. The world view's placeholder line went (the table's wedge stays,
faded out over the boundary). Knob: `-night=beam:1`. Tests: 207 pass.

**Fixed 2026-10-29 (the user: beams stuttered at 1x close in).** The beam was drawn from the lamp and bearing at the
last look, so between looks the ship sailed on under a beam left behind, then it jumped. The snapshot now carries the
lamp in its ship's frame and the bearing off her bow (`BeamsView.LampLocal`, `RelativeDeg`), and `NightLights.Beam`
places it each frame from her pose as the map draws her (`ShipsView.PoseAt`, the sprite's own): a beam held on a ship
the map shows points at her as drawn, a sweep turns from her heading now. Only a contact's light seen with no ship
drawn falls back to the look's.

**Q10d Star shell.** The burst and its puff, the star with its flicker and burn-out, the halo in the haze (the shared
point-light halo, also on for fires and flashes), the trail. The world view's placeholder disc goes.

**Q10d done 2026-10-29.** `Map/Night/FlareRenderer` (`flare.vert`/`flare.frag`), through the effects camera (height
× tan 45° up the screen; the pool, the shadows and the light block at the true place):
- **The star**: its candle over a small core (0.6 m, never under 1.5 px) and its **glow in the haze**, the air's light
  scattered toward the eye along its ray past the star from above and down to the sea under it, L = σ p I (π/2 +
  atan(h/d)) / d · e^(−σd): σ the sim's (`SearchlightModel.ScatterPerM`), p a marine aerosol's at right angles
  (Henyey-Greenstein g 0.7, 0.022 /sr: no sim number stands behind the glow, so the physical one; all round would be
  3.6× brighter). Out to where it sinks under the night sea's own light (cap 10 km): a first cut capped at 3 km left a
  hard edge, the physical glow still far over the dark sea there. Through the night eye, rolled to white on luminance,
  added through the haze. In an 8 km haze on a moonless night it greys a wide patch of the night; in clear air far
  less.
- **Its life** (`FlareRenderer.Envelope`, shared with its light in `NightLights`, so the pool follows): a pop as it
  lights (×1.8 decaying over 0.6 s), an 8% flicker by the world's time, the last 1.5 s dimming and reddening.
- **The trail**: a Gaussian column from the star to the burst (it drifts with the wind as the star does, so it hangs
  straight over it), its half width 1.2 m at the star growing 0.25 m a second of its age, the burst's puff rounder at its
  top; lit by the night (the rods' view) and by every light in the block, the star's own from just under it, so it glows
  over the star and dims up its length. It outlives the star by 40 s, fading (a book in the renderer keeps flares the
  snapshot has dropped).
The world view's placeholder disc went (the table's star and patch stay). Knobs: `-flares=on,gain,halo,trail,width,grow`.
Tests: 207 pass.

**Q10e The moon and the small lights.** The cloud on the moon, the moon path checked; funnel glow and challenge lamps
(their cues into the snapshot); phosphorescence (the weather field, the foam's glow).

**Q10f Cost and tuning.** `-gputime` at the night scenarios (Dover 1917, Jutland's night, Tsushima, Åland's summer
night) with every light on; the gains by the user's eye from side-by-side captures.

## Budgets

Lights are tens: the night's pool 8 in the block, one quad per beam, a few per flare. Each pixel of a world shader
loops 8 more lights (the flashes' 16 today). The beam and halo terms are closed form; the only texture read added is
the smoke field's τ under a beam. No new pass, no new target. Nothing allocates per frame; nothing depends on the
clock's speed (the iris, sweep, fall and drift are the sim's time).

## Decided (the user, 2026-10-29)

1. **Near black**, with **night ticks** in the world view to show where the ships are (above, "Readability").
2. **The Purkinje split**: the night's own light desaturated and cool, artificial lights in colour.
3. **The table may stay brighter** than the world at night: it's for decisions, not mood. The hand-over darkens
   through the fade.
4. **Flares through the effects camera at 45°** for now (`Effects.PitchDeg`).
5. **Phosphorescence now**, as a look on a scenario weather field; the sim's cue later (an approved exception, above).
