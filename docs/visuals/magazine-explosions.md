# Magazine explosions in the world view (plan agreed 2026-10-15; built and approved the same day)

Research: `research/vfx/magazine-explosion-vfx-research.md` (what it looked like, the scalings from the mechanics,
the jet phase, rendering top-down, the budget), `research/simulation/damage-research/07-magazine-explosions.md` (the
mechanics and the outcome tiers), `docs/visuals/shell-impacts.md` ("Catastrophic"). Reference code:
`research/vfx/magazine_explosion_ref.py`, `magazine_jet_clip.py`. The offline demo is vidgen's `magazine.py`
(`shipgen/vidgen`, HANDOFF "Magazine explosions"): the reference ported onto exported ships, every opening and source
read from the ship's rooms and mounts, with its departures marked. This plan ports **its** choices, not the bare
reference's.

The table already has its mark (`MapScene.Damage.Magazine`: a white-hot flash with sparks, a shock ring and a 90 s pall
at pixel sizes). This plan covers the **world view** only, as `splashes.md` and `muzzle-flashes.md` did: drawn × the
world's share (`WorldView.Factor`), the table's mark × (1 − it).

Scope: a ship lost to her magazine, from a destroyer's 0.3 t to a battlecruiser's 20 t, by day, dusk and night. What
it makes: the precursor jets from the failed openings, the barbette's flame column, the fireball, the smoke cap and its
mushroom, the stem, steam, debris with smoke trails, sparks, the thrown gunhouse, the blast on the water, the debris
splashes, the fire's light on the sea and the ships, the shadow, and the pall drifting off. The ship's going down is
drawn as it is now (straight down over `MagazineLossMs`).

## What the sim gives today, and what it needs

`Effects` and `Fires.Step` roll a magazine and `DamageSystem.Lose(LossKind.Magazine)` loses the ship at once (v0,
`fighting.md` 4.3a). The snapshot knows the ship's `Loss` and `LossMs`, nothing more: not **which** magazine, nor
whether a shell or a fire set it off. The look needs the magazine (where the fireball is, which mounts' ports jet,
which barbette becomes a column, which boiler room steams, how much propellant) so the sim gains a record (X0), as F0
recorded each mount fired. It changes no result.

**Only tier 3 exists** (07 §6.3: severed, the ship lost). Tier 1 (the turret burns out, the ship fights on: Lion,
Seydlitz) needs the venting race, which is 4.5's and moved out (fighting.md decision 13). Tier 4 (two groups, Vanguard) also. So
every event here is drawn as tier 3 (decision 1). The barbette's flame column is still built in X3, as the last of the
jet phase, so tier 1 later is the same event with no fireball.

## What the research asks for, and what we build instead

The research and the demo run a CPU particle system (~3k particles: buoyancy, entrainment, the mushroom's circulation)
drawn with 6-way-lit flipbooks into a half-resolution target, with a planar reflection pass, bloom, heat haze, an
exposure flash and camera shake. The world view's rules are no new pass, no new target, the animation a pure function
of age, placement on the CPU eye-relative in double, nothing allocated a frame. Each costly part has a stand-in:

| Research / demo | Here | What is kept |
|---|---|---|
| Particle sim of the fireball, cap and stem, stepped each frame | **Analytic volumes, closed-form in age** (decision 2): the fireball a sphere growing to D and lifting off to ≈ D; the cap a buoyant thermal, `z ∝ t^½`, radius 0.25 z, a torus-weighted density for the mushroom; the stem a sheared column from the deck to the cap, `u(z) = u₁₀(z/10)^0.14`. Marched along the effects camera's rays, as the splash columns are | The research's sizes and timings drive the shape directly. A ship that blew up off screen or at 240x shows the right state when you zoom in; nothing is simulated per frame |
| The mushroom's poloidal circulation | **Noise advected round the torus**: the noise's domain turned about the cap's core ring at the circulation's rate, so the billows roll up the middle and down the outside | The rolling read, without particles |
| 2-4 fireball and 2-3 smoke flipbooks, temperature as a channel | **Procedural**, from the splashes' and gun smoke's noise: a temperature field per volume (hot core, the **soot skin** cooling the top and outside first, the demo's 1.8), broken by noise, through the flashes' blackbody (`FlashPhotometry`, the demo's 900 + 1100 T K) | Crimson under a dark skin, every event its own. No new assets |
| Jet phase: puffs spawned along each opening's axis | **One quad per opening**, an analytic cone (radius `0.45 d + 0.06 s L`, 2-8 % lateral noise), emissive, length `45 d √(P/p₀)` capped per opening, under the demo's stand-in P(t) and fail times | The staggered "venting fire from everywhere" before the fireball, which the demo found carries the first second |
| Debris particles with trail puffs ("octopus") | **Closed-form ballistic arcs** on the CPU per frame (30-80 pieces), drawn as small sprites with a trail ribbon over the arc's last few seconds, one instanced draw | The tendrils, the most structure for the least cost (demo) |
| Sparks | Closed-form ballistic points, additive, with the flashes' screen blend | |
| Thrown gunhouse | The **turret's own sprite** on a tumbling arc (the demo's 20-30 m/s up, 25-50 m off), its mount no longer drawn on the ship | Barham's lifted turret, Vanguard's 40 m |
| Smoke shadow: a 64³ density splat swept toward the sun | **The sun's ray marched through the same analytic density**, on a quad laid down-sun, as the splashes' shadows | The daylight height cue (a ~1 km shadow by 15 s) |
| 1-4 point lights at the hot particles' centroid | **1-2 lights** in the flash lights' uniforms (`FlashLights`), ahead of gun flashes, at the emission-weighted centre of the jets and fireball, its drawn brightness thrown (F4's rule) | The sea flares and the ships nearby warm; restrained at night (demo: strong light turns the sea beige) |
| Planar reflection of the VFX layer | **The sea's glint lobe from those lights**, which `seaShade` already breaks up through its slopes | A broken reflection under the fire. A true mirror image of the column is later |
| Bloom | The flashes' **analytic halo**, on the fireball's and jets' brightness | |
| Blast frost ring and scour foam | **`BlastRenderer` as it is**, one quad, λ from M (one implementation) | The shock ring, the best top-down read for scale (shell-impacts.md) |
| Debris splashes and foam dots | **Records into the `SplashBook`** where each piece lands, sized by the piece | Columns, foam and shadows for free |
| Stem fires, steam, the pall | **`SmokeBook` puffs** (decision 4): soot from fires along the magazine's span, white steam from the nearest boiler room, the cap's smoke handed on as it stalls; merged, lit and shadowed as gun smoke is | Minutes of pall drifting downwind, on the system already built |
| Exposure flash, camera shake | **None** (decision 5) | The light and the halo carry it |
| Heat haze | Later | — |

## The event, once per class and magazine (CPU)

`MagazineBlast` ports `magazine.plan` and its correlations (one implementation; tests and tools call it), cached per
class and magazine room. From the `ShipClass`:

- **M**, the propellant burned in the cascade window: the room's `Tonnes` × the propellant's share of a round (from
  `ShipClass.Charges`, F1's, against the shell's weight) × `F_FAST` 0.3 (the demo's departure, until the venting race
  gives M). A flagged placeholder in `DesignerPlaceholders.cs`.
- **The fireball** `D = 5.8 M^⅓`, `t_fb = 0.45 M^⅓` (M < 30 t) or `2.6 M^⅙`, lift-off ≈ D; **the blast** λ =
  (0.5 ΔH M / p₀)^⅓; the scale `k = max(0.25, D / 157 m)` that sizes vents, stem fires, debris and soot (the demo's
  `D_REF`), so a destroyer's 0.3 t doesn't throw a battleship's wreckage.
- **The openings**, each with position, direction, d and L_max: a port per barrel along the trained barrel, the
  sighting hood, deck hatches over the magazine (one per ~4 m), four side scuttles, the vents of machinery rooms
  sharing a bulkhead (Hood), then the barbette when the roof lifts (capped at 120 m). Fail times are the prototype's
  sequence (0 to 0.45 s after the first), the main event at 0.9 s.
- **The rest**: the barbette (its centre, radius, top), the nearest boiler room, the debris count
  `8 + 70(1 − e^(−M/15 t))`.

What it gives, from the research's table (the X1 tests pin the cut's own classes):

| M | Fireball D | t_fb | Blast λ | Like |
|---|---|---|---|---|
| 0.3 t | 39 m | 3.0 s | 18 m | a destroyer's |
| 2 t | 73 m | 5.7 s | 34 m | Invincible's "400 ft" |
| 20 t | 157 m | 12.2 s | 73 m | Queen Mary's "800 ft" |

**Per event** (display only, from a hash of the ship and the loss time): the ±30 % on D and the scatter on t_fb (the
research: use the correlations' spread), the debris' launches, the noise seeds.

## Time

- **Display timeline**, from the loss: the hit's flash and puff at 0, the first opening fails at +0.5 s (the demo's
  `HIT_LEAD`), the roof lifts at +0.95, the fireball at +1.4. The sim's loss is at the hit; the 1.4 s lead is the
  drawing's.
- **The fireball and its cloud, jets, debris and sparks** play on the effects clock (`Effects.Rate`: 11x at 60x), so a 12 s fireball
  still plays out on the screen. **Never thinned**: a magazine is the event the eye must catch (shell-impacts.md's
  "never culled").
- **The smoke puffs, frost, scour and debris foam** are on the sea's clock, as all smoke and foam are.
- At 60x the ship (on world time) is gone in about a second while the fireball plays on: the blast outlives the ship,
  as the table's mark already does.
- **The coarse tier** draws the table's mark only.

## The effects camera, and very tall things (decision 3)

The effects camera (`Effects.PitchDeg` 45°) puts a height h at h up the screen, as for the splashes and flashes. A
magazine's cap would climb ~1 km by a minute: a screen's height at 1 m/px, on a long stem. The plan first compressed
heights above a 300 m knee to keep it on the screen; the user rescinded that (2026-10-15): the cap on its stem read as
a flower on its stalk. **Heights are drawn true, and the body fades out early instead** (1 to 1.8 fireball lives after
the main event, `MagazineLook.FadeFrom`/`FadeTo`), while the cap is still a few hundred metres up; the stem's soot
plume (X6) carries the smoke on.

**The anchor is the deck** over the magazine (the demo measured lift from the deck): the fireball's base sits on the
ship, only the height gained goes up the screen. Jets anchor at their openings, as flashes at their barrel tips.

## The look, by what draws it

In draw order, all after the ships' layer:

1. **The shadow**: one quad laid down-sun on the sea and the decks, marching the sun's ray through the volumes'
   density; darkens to ~30 % under full shadow. Follows the stronger light, fades at low sun.
2. **The volumes** (`Include/magazineblast.glsl`, the one model): one quad per event, from the deck up the screen by
   the height of the cap, as wide as its widest part. Per pixel, the effects camera's parallel ray marched
   through the stem, fireball and cap (8-32 steps by screen size, the splashes' LOD), each step:
   - density from the analytic shapes, broken by noise fixed to the volume and growing with it, the cap's advected
     round its ring; soot albedo 0.07, a little warm;
   - temperature from the event's age and the point's place (the soot skin), emissive through the blackbody, rolled to
     white on luminance; brightness by contrast with the scene's background (`FlashPhotometry`, as the flashes);
   - lit by the sun or moon (a two-tap self-shadow toward the light, the gun smoke's) and **from inside and below**
     by the fire's own light, which makes the cap glow crimson at dusk and night;
   - premultiplied over; the analytic halo on what's hot.
3. **The jets**: one quad per live opening, emissive cones in the bore's frame, screen-blended (the flashes' blend).
   The barbette's column pulses (the demo's 0.7 and 0.23 Hz) until the fireball swallows it.
4. **Debris and the gunhouse**: sprites on their arcs, emissive while hot, their trails soot ribbons fading over the
   last 3-10 s of flight; the gunhouse is the turret type's sprite, scaled by its height for a little perspective
   (the sinking research's `1 + Z/H_cam`), tumbling. Each landing writes a splash record.
5. **Sparks**: additive points with the flashes.
6. **On the water**, in the wake pre-pass: `BlastRenderer`'s front, frost and scour at the event's λ.
7. **The light**: into `LightUniforms` with the flash lights, the magazine's first.
8. **The puffs**: stem fires along the span, decaying over ~40 s; the boiler room's steam for ~12 s; as the cap
   stalls (30-60 s), puffs take its smoke over while the volume fades out, so the pall drifts on as merged puffs.

## Restrictions

- **Display only.** The sim gains one record per magazine lost (X0) and reads no drawing.
- **Realistic, then tune:** the research's and demo's sizes, timings and colours first. Departures are named knobs in
  a `-magazine=` flag (`MagazineLook`), each recorded here; the demo's own departures come in as its defaults.
- **Lightweight, measured:** one event at 1 m/px, 1080p, at its largest on screen, under 1.0 ms GPU (`-gputime`); two
  at once under 1.5 ms. The CPU's build under 0.1 ms. Nothing allocated a frame (the event's start may, the frames
  never). If the volumes' overdraw beats it, a half-resolution target comes back as a named exception, not before.
- **Pipeline:** no new pass and no new target.

## Logged exceptions (approved 2026-10-15)

- **The pall is drawn by the map alone and blocks no sight**, as gun smoke (muzzle-flashes.md decision 1), until the
  smoke system. A real pall hid the line for minutes. Approved because the smoke system has a set plan to take it on.

## Decisions (the user, 2026-10-15)

1. **Tier 3 only, for now.** Every magazine loss draws the full event. Tier 1 (the column the ship survives) and
   tier 4 (two fireballs) come after 4.5's venting race (systems design is on a break); the column is built anyway,
   inside the jet phase.
2. **Analytic volumes, not a ported particle sim.** Reality is traded for the realtime budget: the game stays light,
   so the look comes from tricks that are cheap. The mushroom's circulation is imitated by advected noise. If X2's
   captures read as smooth balls (the demo's weak spot too), the fallback is a pool of ~200 closed-form puffs for the
   fireball and cap, before any particle sim.
3. ~~The height knee at 300 m with H_c 450 m~~ (rescinded 2026-10-15, after X7's first look: the cap on its stem read as
   a flower on its stalk). Heights are drawn true; the body fades early instead.
4. **The after-smoke on the `SmokeBook`**, which gains a soot albedo and a steam tint per puff. This is one more kind
   of smoke drawn by the map: the unification research (with the smoke system) takes it in later.
5. **No camera shake and no exposure flash**, as no flash adaptation (muzzle-flashes.md decision 3).

## Steps

- **X0. The record.** The sim records each magazine lost: ship, class, magazine room, time, cause (shell or fire),
  the hit's point. Carried from `Effects` and `Fires.Step` to `DamageSystem.Lose`, kept in a ring, read as a
  `Snapshot.Blasts` view; the map keeps a `MagazineBook` as `ShotBook` does. The dev's `-sink=SHIP:magazine[:MOUNT]`
  sets one off through the sim. Tests: a hit in a magazine and a fire into one each record the room; one record per loss.
- **X1. The event.** `MagazineBlast` (the port of `plan` and the correlations), the placeholder, a cache per class
  and room. Tests against `magazine.py` on the same ship (Bismarck's Y: M 22 t, D 163 m, t_fb 12.6 s, λ 76 m) and the
  research's table; the openings' count and fail order.
- **X2. The volumes.** The height knee in `Effects`; `magazineblast.glsl`, the instance build, the draw: stem,
  fireball, cap, the soot skin, the blackbody, the fire's light inside. Stills with `-magazine=at:S` and
  `-screenshot` at 1, 3, 8, 15, 30 and 60 s; noon, 19:30 and 23:30; 0.5, 1 and 4 m/px; a destroyer and a
  battlecruiser.
- **X3. The jets and the column.** The openings' cones, the stand-in P(t), the barbette's column; the hit's flash.
  Night stills at 0.2-1.4 s against the demo's `magx_jet_phase_night` frames.
- **X4. Debris, sparks, the gunhouse.** The arcs, the trails, the sparks, the turret sprite thrown and hidden on the
  ship, the landings into the `SplashBook`. Tests: an arc against the closed form; a landing makes one splash record.
- **X5. Water, light, shadow.** `BlastRenderer` at the event's λ, the light into the uniforms, the shadow quad. Noon
  and night stills, side by side with X2's.
- **X6. The after-smoke.** Stem fires, steam and the cap's handover into the `SmokeBook`; the volume's fade. Stills at
  30, 60, 120 and 300 s in a 12-knot wind. Test: the handover keeps the smoke's extinction area.
- **X7. Time, cost, tuning.** Live at 1x, 10x, 60x and 240x and through the zoom boundary (the table's mark × (1 −
  factor)); the cost (`-gputime`, DevRelease `-frametimes`) with the event filling the screen and two at once; tuning
  by eye against the anchors: Lion's 61 m column, Queen Mary's crimson flame to ~240 m and cap to ~1 km, Arizona's
  0.5 s precursor and < 7 s violent phase, Barham's lifted roof and smoke "thousands of feet".

### Done

**X0 (2026-10-15).** `EffectsResult.MagazineRoom` and `FireStep.MagazineRoom` (the room set off; a turret's flash takes
its mount's magazine), `Underwater` likewise; `DamageSystem.Blast` (ms, ship, class, room, `BlastCause`, where she was,
her heading, the hit's point), written by `Blew` before the loss, kept in a ring (`BlastCapacity` 64) for
`BlastLifeMs` (15 min, the pall's while) and read as `Snapshot.Blasts` (`BlastsView`). The dev's `-sink=` takes a
mount for a magazine's (default: the fullest). Tests (`DamageTests`): a hit records its magazine room once; a fire
spreading into one records it; the dev's records the mount's magazine, once, and the record outlives the ship for its
while, then goes.

**X1 (2026-10-15).** `Sim.Combat.MagazineBlast` (the port of `magazine.plan` and its correlations: M, the fireball,
λ, the scale k, the debris count, the openings with their fail times, the barbette, the mount over the magazine, the
nearest boiler room; the stand-in pressure and the jets' length) and its placeholders (`MagazineFastShare` 0.3,
`MagazinePropellantShare` 0.23, `MagazineTonnes` 0.5 t/m³ for a room without tonnes). The propellant's share comes from
the class's charge and shell rows, not vidgen's fits: Bismarck's aft magazines (321 t) give M 19.8 t, D 157 m for
12.2 s, λ 73 m against vidgen's 22 t, 163 m, 12.6 s, 76 m; the destroyer's 0.3 t, 39 m. Tests
(`MagazineBlastTests`): the research's table, Bismarck within 15% of vidgen, every design's magazines build, the
openings' order and a port's jet (74 m at the main event, gone 3 s after; the barbette capped at 120 m).

**X2 (2026-10-15).** `Map/Magazines/`: `MagazineBook` (the events seen, on the effects' and the sea's clocks,
back-dated by their age, each one's scatter and seed), `MagazineRenderer` (the state per frame on the CPU, two draws
after the gun smoke: the shadow, then the body), `MagazineLook` (`-magazine=`: scatterd, scatters, life, skin, cold,
hot, soot, opacity, mushroom, spin, stem, stemtau, stemfade, gain, whiten, steps, knee, kneehc, parts, at, on). The
body's laws are `MagazineBlast.BodyAt` (the model's, one implementation); the shapes `Include/magazine.glsl`, marched
by `magazine_body.frag` and `magazine_shadow.frag`. `Effects.Drawn` is the height knee (300 m, H_c 450 m). The table's
magazine mark fades out where the world view draws the event, as the gun flash's does.

Seen (Bismarck's fullest magazine, captures at 1.5 m/px, an 8 m/s westerly):
- noon: a white-yellow fireball at 2 s, orange billows by 4 s, dark soot patches over a crimson inside by 8 s; a dark
  cap lifting off on its stem by 15 s, ~1 km up and drifting ~300 m downwind by 60 s, its shadow on the sea;
- dusk: the same, the fireball white for longer against the dimmer sea;
- night: orange through crimson, the stem's fires glowing at the wreck, the cap dark against the sea;
- a destroyer's (0.3 t): a 39 m fireball gone in 3 s, a smaller cap.

Changed on the way, the departures recorded here and in `MagazineLook`:
- **The cap stays dark.** Its optical depth falls as the square root of its radius (8 at the fireball's end), not its
  area: the fires below feed it, and the research's caps stand for minutes. By the area it was a haze within a
  minute.
- **The fire's hue is kept past white** (`whiten` 0.7): the hue goes toward a warm white by the gas's temperature, not
  its brightness. Rolled to white as the flashes are, the night fireball (thousands of times the moonlit deck) was a
  flat white blob; whitened by brightness, it went salmon (the flashes' research's warning for a pure blackbody).
- **The young fireball is thinner** (its hot gas 0.3 of its soot's depth) and cools less toward its rim, so its
  white-hot core shows; soot patches form as its skin grows.
- **The mushroom's hollow is under the cap only**: from above the dome stays solid. The ring's turning noise is held to
  the rim: turned everywhere, the coordinates pinched toward the axis and the dome showed ribs.
- **Billows by a closed-form swirl** and a one-lookup relief toward the light, for cost.

The cost (DevRelease, 1080p, 1 m/px, the 60 s cap filling much of the screen, minimum p50 of three alternating runs;
the machine's other load made single runs swing by ±1 ms): **+0.6 ms GPU** (1.60 against 0.99), inside the 1.0 ms
budget. The first version cost +1.3 ms; marching only the stretch of each ray that can meet the body or the stem (the
knee inverted analytically), the noiseless shadow, the swirl and fewer steps brought it down.

**X3 (2026-10-15).** The jets (`magazine_jet.vert`/`.frag`, `MagazineRenderer.AddJets`): one quad per opening jetting,
its length `MagazineBlast.JetLength` under the stand-in pressure, from its fail time until the fireball swallows it
(faded over 0.5 s from the main event); the gun ports' along the guns as trained (the book keeps the mounts' bearings
from the ship's first sight, the rests otherwise), the hood's, the hatches' and the vents' up the screen, the scuttles'
sideways, the barbette's column (capped at 120 m) from the roof's lifting. Each a narrow cone (0.45 d at the root,
+0.06 of its length at the tip), hot on its axis at the root, cooler at its rim and tip, torn into tongues by noise
that runs out along it; screen-blended, as the flashes. The hit's flash comes first: 8 m, 0.35 s, where a shell hit, or
over the magazine on its barbette's top for a fire or the dev's. `-magazine=parts:4` toggles them.

Seen at 0.6 m/px on Bismarck's forward magazine: the flash on A turret at 0.1 s; the ports' tongues along the guns
and the hood's and hatches' rising from 0.5 s; the barbette's column ~120 m up the screen by 1.3 s; the fireball
swallowing them by 1.9 s. At night they're orange-white tongues; by day pale flames over the deck.

Changed on the way: **the jets' brightness is rolled off before their coverage weights it**, so they keep soft edges
however far past white the gas is (weighted first, every pixel of a night jet saturated: hard-edged sausages).

**X4 (2026-10-15).** `MagazineDebris` (each event's launches from its seed, drawn once: vidgen's pieces, 35-88° up at
up to √(2g·140 m·k), half burning, their trails' lives 4-10 s; the sparks; the gunhouse thrown 20-30 m/s up and 25-50 m
off, spinning and tumbling, coming down on the deck or in the sea), all closed-form arcs. Drawn by `MagazineRenderer`:
the burning pieces as hot flecks and the sparks through the jets' pipeline, the cold pieces and the trails as soot
ribbons (`magazine_trail.frag` on the jets' vertex shader: each trail eight moments of its arc, spreading, rising a
little and drifting downwind as they age), premultiplied before the body. Each piece's splash, and the gunhouse's when
it comes down in the sea, goes into the splash book once at the event's first sight, dated ahead to its landing
(`SplashBook.Add`: its column and foam wait for it). The gunhouse is the turret type's own sprite: the ships' draw list
asks the book (`IThrownTurrets`) and emits that mount's quad on its arc, turned, squashed by its tumble, raised up the
screen by its height and grown 1/150 per metre of it; gone once in the sea, or come to rest on the deck right way up or
upside down. `-magazine=parts:8` toggles the debris.

Seen (noon, 1 m/px, live): sparks round the young fireball; by 6 s the burning pieces' trails arcing out of it (the
"octopus"), cold flecks falling; from ~5 s the pieces' splashes coming down round the wreck as small columns and foam
rings, and the gunhouse's big one beside her. The cost at 6.5 s, debris and all: +0.1-0.2 ms GPU.

Changed on the way: **smaller debris splashes** (as shells of 60 mm per metre of the piece, 6-15 m columns, and the
gunhouse's as a 300 mm's): at 90 mm the pieces' columns stood round the wreck like candles.

**X5 (2026-10-15).** The water and the light (the shadow came with X2):
- **The blast on the water**: `BlastRenderer.AddSpherical`, the muzzle blasts' model as is (one implementation) with
  its bore straight up, so its directivity is a ring (its 0.62 aside undone in λ) and there's no jet lobe; at the event's
  λ from the deck's height, on the sea's clock from the main event. The blast's uniforms are now set even when no gun
  has fired.
- **The fire's light**: `MagazineRenderer.Light` into the frame's point lights with the flashes': while the jets burn,
  at the magazine 20 m up (their flame's area); then the fireball's at its centre by its heat, hidden as the soot skin
  grows. Thrown as the flashes' are (the drawn brightness compressed, times the area over pi) at
  `MagazineLook.LightShare` 0.3 (`-magazine=light:`). The scene now builds the blasts and the magazines before it sets
  the frame's lights, so the ships, sea, splashes and smoke all see them.

Seen: at noon a frosted disc with its dark leading edge racing out to ~300 m by 2.6 s round Bismarck's bow, the scour
churned white at the wreck, gone by 6 s. At night the deck and hull by the fire glow orange; at full share the whole
ship went orange (too much), so 0.3.

**A bug found on the way:** a light of zero added to `FlashLights` made its place 0/0, and the NaN whitened the whole
sea (every shader reading the lights). `Add` now ignores a light of nothing (test: `FlashTests.A_light_of_nothing_adds_nothing`).

Open, for X7: the fire has no reflection on the night sea. Its glint pool lies under the light, and the light is over
the wreck, so the ship hides it; the research's mirror image down the screen is the planar reflection (Later).

**X6 (2026-10-15).** The after-smoke on the gun smoke's book (decision 4): `SmokeBook.Add` (puffs from elsewhere than a
gun, merged and swept with the rest, each kind keyed apart) and `Puff.Keep` (its tint stays: the gun smoke's whitening
with age would bleach soot). `MagazineBook.SpawnSmoke` gives each puff once, back-dated to its moment on the smoke's
clock, so an event seen late has its whole plume:
- **the stem's fires**: a soot puff (linear 0.075, 0.068, 0.062) every 6 s while they burn (three of their 40 s
  fadings), 15,000 m² of extinction area a second at the start for the reference's event, × k²;
- **the boilers' steam**: four white water-fog puffs over the first 6 s from the nearest boiler room, 8,000 m² each × k,
  evaporating over 8 s.

Seen (noon, 2 m/px, live at 2x): the cap overhead and its stem, a dark-brown plume from the wreck laid downwind, the
steam a white burst at the wreck early on; by ~160 s the body is gone and the pall drifts as a brown wedge some 300 m
downwind of the wreck.

Changed on the way:
- **Puffs 6 s apart, from a 10 m core**: every 2 s, each new one lay within the merge's reach of the last and the
  fires' smoke was one blob dragging at the wreck.
- **Far less steam, gone faster** (8,000 m² a puff over 8 s, not 40,000 over 20): it sat at the waterline as a white
  ball for a minute.

**The cap's handover to puffs is not done.** A puff born now, back-dated to the age that gives the cap's spread (the
merging's trick), would appear at full strength at once: the handover popped. The body instead fades over its last
third (80-120 s on the effects' clock) while the stem's plume carries the pall on. For X7: a ramp for back-dated puffs,
if the fade reads wrong.

**X7, time and cost (2026-10-15).** `-magazine=at:` stills now hold every event of the newest 2 s, as the flashes'
do, so two can be measured together.
- **Cost** (DevRelease, 1080p, 1 m/px, minimum p50 of three alternating runs against `on:0`; this machine's other load
  swings single runs by ±1 ms):

  | | Added, p50 |
  |---|---|
  | One event, its 60 s cap filling much of the screen | +0.6 ms |
  | Two at once, their caps side by side | +1.1 ms |
  | One at 6.5 s: fireball, debris, trails, sparks, splashes | +0.1-0.2 ms |

  Inside the budget (1.0 for one, 1.5 for two). The two's mean has a heavier tail than its p50.
- **Time**: live at 60x the event plays through on the effects' clock (the fireball's 12 s in about a second) while the
  ship, on the world's, is gone; the after-smoke and foam on the sea's.
- **The zoom boundary**: at 3 and 5 m/px the world view draws it; by 8 m/px the table's mark has taken over.

**Still to do in X7: tuning by the user's eye**, against the anchors (Lion's 61 m column, Queen Mary's crimson flame to
~240 m and cap to ~1 km, Arizona's 0.5 s precursor and < 7 s violent phase, Barham's lifted roof and smoke "thousands
of feet"). Things to look at:
- the cap's darkness and size, and its early fade (1-1.8 fireball lives);
- the fireball's colour by day (it goes peach early) and by night (orange to crimson);
- the jets' width and brightness; the gunhouse's flight (mostly hidden in the fireball, as in vidgen);
- the debris splashes' count and size; the blast's frost ring;
- the fire's light (0.3 of its drawn brightness) and the missing reflection on the night sea;
- the stem's plume and the steam.

**X7, the user's first round (2026-10-15).** "It looks awesome, but I want to rescind my original 300 m thing. The smoke
starts to look like a flower stalk, so let's fade it out quite early." The knee is gone (`Effects.Drawn`, the shaders'
inverse and slope, the `knee`/`kneehc` knobs): every height is drawn true again. The body (fireball, cap and stem)
fades from 1 to 1.8 of its fireball's lives after the main event (`fadefrom`, `fadeto`; Bismarck's from 12 to 22 s,
the cap ~380 m up when it's gone), replacing the 120 s life and its last-third fade. The book keeps an event at least
130 s on the effects' clock, so the debris' trails and the after-smoke's spawning outlast the body. The cap's handover
to puffs (X6's open item) is moot: the cap is gone before it would stall.

**X7, the user's second round (2026-10-15).** "The initial formation looks great but at a certain moment it just starts
moving upwards and it looks like a growing flower." A departure from the research's lift-off and thermal, the user's
call: **the cloud stays down.** `MagazineBlast.BodyAt` keeps the formation (grown to its diameter over half its life)
but lifts it only 0.15 D by the fireball's end and 0.1 D more over the next life (`LiftD`, `LaterLiftD`), its centre
0.7 of its radius over the deck so its foot stays on the wreck, billowing 35% wider a life after (`Spread`) and
flattening to 0.7 of its width; it drifts downwind and fades as before (1-1.8 lives). Bismarck's as it goes: centre
~108 m over the deck, 200 m across, 145 m high. Gone with the lift: **the stem** (no stalk left to draw: its shader
parts, its quads and the `stem`/`stemtau` knobs) and **the mushroom's hollow underside** (`mushroom`); the noise still
rolls round the cloud's edge (`spin`). The wreck's fires' soot keeps its knobs, renamed `firefade`, `fireevery`,
`firesmoke` (they were the stem's). Seen at noon, 1 m/px: the fireball over the bow, the dark cloud billowing on the
wreck by 12 s, drifting off and gone by ~20 s.

**X7, the user's third round (2026-10-15).** "Have it rise somewhat, but not so much that the stalk would be needed.
Now the natural rise feels stopped; before it felt forcefully stretched, partly because it looked static while rising.
Rise it while it's still changing." Two causes found: the radius stopped growing at half the fireball's life and the
two lift phases met at zero speed at its end, so the cloud stood still in between; and its billows were fixed to it, so
it moved as a rigid lump. Now:
- **one motion**: it grows to its diameter over half its life and billows on 50% wider a life after
  (`MagazineBlast.Spread`), flattens to 0.7 over two lives, and rises half its diameter over two lives on a single
  smooth curve (`LiftD`), never pausing; Bismarck's half faded (1.4 lives): centre 139 m over the deck, 228 m across,
  its foot 47 m up (within a third of its diameter: no stalk needed);
- **the billows boil on** as it rises (`MagazineLook.Churn`, `-magazine=churn:`): the noise runs up through the cloud by
  1.5 cells × ln(1 + t / 0.3 life), fast at first and slowing, as the gun smoke's churn does.

Tests: the cloud rises at every step from its growth to its fading, and is wider than it stands with its foot near
the wreck when half faded.

**Approved (the user, 2026-10-15):** "Beautiful, you nailed it." X7's tuning is done as it stands.

## Later

- Tier 1 (the burn-out column the ship survives) and tier 4 (two fireballs along the hull), with the venting race.
- The hull broken at the magazine and its pieces sinking apart, with the sinking VFX (`sinking-vfx-research.md`).
- The open barbette and the scorch on a survivor, with the hit-effects pass.
- Debris landing on neighbours (a spark and a scorch on her), Tiger's and Bellerophon's.
- Burning oil on the water, boils as the pieces go down.
- A capsized ship's explosion partly underwater (Barham): a brown water dome, smoke bursting through it.
- The planar reflection of the fire, heat haze, the pall blocking sight (with the smoke system).
- The smoke unification research: gun smoke, the magazine's stem, steam and pall, funnel smoke and fires as one
  system (`research/vfx/unified-smoke-system.md` is its start).
