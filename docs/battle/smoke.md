# Plan: the smoke field (a feasibility study)
Status: **drafted 2026-10-20**, decisions 1-4 from the user ("Decided" below); weather's one optical depth proposed;
nothing built    Updated: 2026-10-20

Smoke as sim state that sight reads, and the same state drawn. The research is `research/vfx/unified-smoke-system.md`
(Lagrangian Gaussian puffs as the one store, every grid a throw-away view of them), on top of
`research/vfx/muzzle-flash-smoke-research.md` (the puff's A, σ and rise, the line integral, §5.4) and
`research/simulation/powerplant-model.md` (k_smoke by plant).

**This is a feasibility study** (the user, 2026-10-20: "Nowadays one can do these things so quickly that checking it out
makes sense"). On 2026-10-19 the user decided there'd be no simulated smoke in the first game, and the world view's
smoke was built to that: short, dense, stylised plumes that block nothing. If the field proves good, that decision may
be reverted. So the plan runs in gated steps: two gates (cost, Z2; the look, Z5) where it stops if the answer is no,
and the field stays behind a setting, off, until the user takes it in.

## The aim

The record says smoke was a first-order factor in the first game's battles (the research's "Problem framing"): own
funnel smoke blinded the Grand Fleet's view ahead, visibility at Jutland depended on the bearing and on where the wind
carried each fleet's smoke, signals failed along a smoky column beyond the next ship, and destroyers' screens hid a
battlecruiser from Iron Duke and covered the German turn-away. The plan makes:

1. **Smoke a thing in the world:** puffs from funnels, guns, fires, magazines and screens, drifting with the wind,
   spreading and thinning, in the world's state (hashed, replayed).
2. **Sight lines through it, fast:** every look and every fire-control reading takes the smoke on its line, inside a
   budget that keeps the fine tier's speeds.
3. **The drawing from the same puffs, cheaply:** what blocks sight is what's drawn, on the table and in the world view.
4. **Smoke as a contact** (decision 4): a navy sees smoke on the horizon before it sees the ships under it.

## What there is today (checked 2026-10-20)

- **Sighting** (`Sightings.cs`, plan.md 3.2d): a look every `SightingMs` (1 s) on the fine tick, the coarse step in the
  coarse tier, over the ships in encounters. Per encounter a grid in its tangent plane; per target and hostile side the
  `SightObservers` (6) nearest observers and their `SightEyes` (4) highest eyes that bear. Each eye's margin is
  `SightModel.Margin`: the area over the horizon at the aspect, its contrast C₀·e^(−σd) (Koschmieder, σ from the
  visibility), the threshold for its solid angle. Jutland: 270 µs a look.
- **Fire control** reads the same margin (`GunnerySystem.MarginOf`) for each sensor's reading and for the spotter seeing
  the fall; `SightModel.RangingFactor` makes a reading worse as the margin falls. Smoke put into the margin reaches
  readings, ranging and spotting with no other change.
- **Weather** (`Weather.cs`): `IWeather.At(position, time)` gives the visibility, the wind's direction and speed;
  `UniformWeather` only (Jutland's 25 km, SW 8 kn).
- **The reach** (`SightReach`): the tallest eye's horizon plus the tallest silhouette's; opposing ships within it (and
  `EncounterMarginM`) are in an encounter, off rails.
- **Contact levels:** Ship, Type, Class, Name. Plan.md 3.2d named "smoke" as a level; it was never built.
- **Own smoke:** `ComponentRow.Smoke` (the funnels whose smoke blinds a control block, from shipgen's
  `Powerplant.SmokeReach`) is compiled and hashed, and read by nothing.
- **Funnel facts:** no smoke output per funnel in the export (plan.md "Needed from the designer for sighting").
- **The gun record:** `ShellSystem.Shot` (muzzle-flashes.md F0), each mount fired with its muzzle and bore.
- **The map's smoke**, all display only, on the sea's clock (capped at 8x), blocking nothing (muzzle-flashes.md
  decision 1 and hits-and-fires.md decision 2, logged exceptions):
  - `SmokeBook`: Gaussian puffs whose state is closed form in their age (the jet, the rise, σ_h² growth, the ramp,
    the fog), merged by owner by moments, gone under a peak τ of 0.01, 4,096 at most.
  - `SmokeTrails`: continuous sources laying puffs into it, the rate carried so a plume's strength never depends on
    the spacing.
  - `FunnelSmoke`: 1,500 m²/s at full power for coal × (v/v_max)³ (not under 5%), oil 0.3 of coal by the bunkers'
    tonnes, rising 20 m, a stylised 8 s life (the user, 2026-10-19). The fires' and the magazines' after-smoke go the
    same way.

## The principles

- **Simulation is for crunch, VFX is for fluff, and VFX never misleads** (the user, 2026-10-20). The field is what
  blocks sight. A look may add detail the field doesn't have (noise, a plume's head, a lit edge), never thick smoke
  where the field has none, nor hide smoke that blocks sight.
- **One store, derived views.** The puffs are the only smoke state. The query index, the drawing's τ target and the
  smoke contacts' summaries are rebuilt from them and thrown away.
- **Physical where it decides, rough elsewhere** (as fire's plan): a passive scalar in a prescribed wind,
  turbulence as σ growth, buoyancy as a rise curve. No flow solve.
- **No hard switches:** a funnel's output interpolates on the coal share, the power and the draught; no row by navy or
  type.
- **The sim never reads the display.** The snapshot carries a copy; the drawing rule (decision 3) reads the player's
  picture, never the other way.
- **Soft real time:** nothing allocates in a step or a frame once grown; nothing O(n²) (an index for every pair).
- **Measure small first:** each bench starts at seconds-long sizes, then Jutland.

## The yardstick

- **The research's single trail** (Ȧ = 1,500 m²/s at 10 m/s through the air), from its reference script: vertical τ
  4.0 / 2.0 / 0.75 young / mid / old; crossing it at plume height T 0.003 / 0.05 / 0.22; 500 m along its axis τ 80 /
  20 / 3.7. Z0's tests.
- **Jutland, qualitatively** (the research's sources): looking across one ship's trail a dark band; along a column
  opaque beyond the next ship or two; typical ranges 9-15 kyd with the bearing deciding (Iron Duke's rangefinders at
  times limited to 9,000 yd, a clear channel giving 15,000 yd); with the WSW wind the German smoke over the British
  line; a target lost behind a destroyers' screen.
- **Fire control's history cases** (`fc-history`): Dogger Bank and Jutland name smoke among what the sim lacks. With the
  field on they should move toward the record. A report, never a fit.
- **Smoke on the horizon:** how far smoke was sighted before the ships, by period and fuel (research wanted, below).

## The budget (gate 1, in Z2)

At Jutland (250 ships, the 3-hour run headless):

- **The store** (emit, merge, retire, index): under 0.3 ms a game second in the fine tier; under 16 MiB.
- **The queries:** under 0.5 ms a look on top of today's 270 µs, fire control's readings within it. The fine tier runs
  to 240x, 240 looks a real second: 0.5 ms is an eighth of the sim thread there, store included about a fifth.
- **The drawing:** the field under 0.3 ms GPU at 1080p with every puff on screen (theatre zoom), `-gputime` (the
  muzzle-flash plan's smoke budget); the render thread's CPU under 0.1 ms a frame.
- **Nothing allocated** in a step or a frame once grown.

If Z2's bench can't get there with the cache and the index, the study stops there and says why.

## Decided (the user, 2026-10-20)

1. **A feasibility study.** Built in gated steps; if the field proves good, the 2026-10-19 decision (no simulated smoke
   in the first game) may be reverted. Until then it's off by default (`SimSettings.Smoke`), and benches run both.
2. **The look waits on the field** ("It depends on how the field looks like. Simulation is for crunch, vfx is for
   fluff, even though vfx should never mislead. We can't obviously have fully realistic smoke sim so the final look of
   the smoke field depends on how accurately it's modeled."). Z5 draws the field plainly first, then compares it with
   the stylised plumes side by side; the world view's look (field alone, plumes over it, or plumes for the head only)
   is chosen there.
3. **Enemy smoke is drawn once its maker is held as a contact;** own, allied and peaceful sides' smoke always. A bit
   per emitter, filled from the player's picture, gates each puff in the shader.
4. **Smoke contacts are in this plan** (Z6).

## The model

### A puff

```
origin       Vec3d at birth, the air frame's displacement W(born) subtracted (below)
born         world time, ms (a merged puff's: the age that gives its spread)
z0, zEq, tRise   height at birth, the height it rises to, its time constant
s0h², s0z²   variances at birth; K_h, K_z by its class (gun, funnel, fire, screen)
A0, tLife    extinction area at birth, m², and its decay time
emitter      the ship (entity) or a burst's owner; tint class (coal, oil, powder, chemical, fire)
prev, next   its trail's neighbours (merge candidates in O(1))
```

Its state at age t is closed form, as `SmokeBook`'s is:

- **Where:** the origin carried by the air's displacement since its birth. With the world's one wind, `W(t)` is that
  wind's integral over time as east and north metres; a puff never moves relative to the air, so the sim never steps
  a puff. A wind that changes in time stays exact. One that changes by place (later weather) breaks this; then each
  puff reads the wind at its centre when it's merged and is re-anchored.
- **Height:** z = zEq − (zEq − z0)·e^(−t/tRise).
- **Spread:** σ_h² = s0h² + 2K_h·t, σ_z² = s0z² + 2K_z·t (K_h ≈ 5 m²/s: a 20 m puff is ~135 m across in 30 min).
- **Extinction:** A = A0·e^(−t/tLife).
- **Gone** when its peak vertical τ, A / (2π σ_h²), falls under 0.005, or by the cap.

The sim's puffs are coarser than the drawn ones: σ ≈ 20 m at birth (the plume after the funnel's lee), laid every
~1.5σ of run through the air, each carrying what its source made since the last (the rate exact whatever the step).
Stored as columns with a free list; grown, never shrunk.

### The sources

- **Funnels** (`FunnelRow` in `ShipClass`: where, the top, its share of the plant's gas, the plant's smoke at full
  power by fuel and draught from shipgen's `SmokeK`; placeholders in `DesignerPlaceholders.cs` until the designer
  exports them). Ȧ = full × (P / P_max), never under the banked fires' share, × the coal share between coal's and oil's.
  The map's `FunnelSmoke` then reads these rows instead of its own (one implementation).
- **Screens:** `Command.MakeSmoke(ship, on)`, logged: the funnels at `ScreenGain` times their output (burning rich).
  Chemical floats later.
- **Guns:** a puff a mount fired, from `ShellSystem.Shot`: A from the charge, the propellant's own solids and their
  mass extinction (muzzle-flash research §5; black and brown powder in the 1890s against the smokeless). The propellant's
  facts move from the map's `FlashLook` into the gun's data, read by both.
- **Fires:** a ship's `FireShowMW` × a soot yield, at the burning part's height.
- **Magazines and sinkings:** bursts from the magazine's record (its tier and peak).
- **Only ships off rails smoke**: sight only matters in encounters. A ship coming off rails backfills its trail (the
  smoke's useful life back along her track at her speed, aged as if laid then). The coarse tier lays puffs along its
  kinematic segments.

### Merging

Moment-preserving (A summed; centres and heights A-weighted; second moments plus the spread of the means; the merged
puff carries on from the age that gives its σ), within one emitter's trail, settled puffs only (over 60 s), neighbours
closer than 0.5σ. A rotating slice of trails a tick. Over `SmokeCap` (256k) the oldest age classes merge harder first,
then emitters merge across within a crowded cell. The cost degrades in accuracy, never in time.

### The query

**The index**, per encounter, built in Sightings' per-encounter loop: the puffs inside its box (plus the reach) filed
in the encounter's tangent plane in the air frame, at size levels (cells 64 m × 2^k, k 0-7; a puff at the smallest
level whose cell is at least 6σ_h at the next rebuild), as sorted (level, cell) keys with a small hash from key to run.
The puffs don't move in that frame, so it's rebuilt every `SmokeIndexMs` (10 s) or when the encounter's box changes
much. A line is walked per non-empty level by a 2D DDA three cells wide, each puff once, in a fixed order.

**A puff on a line** (the research's closed form, in whitened space): τ_i = A·e^(−b²/2) / (2π σ_h² σ_z |u'|) ×
½[erf(·) − erf(·)], with b the miss in σ units and u' the line's direction scaled by 1/σ. Then:

- **The earth's curvature:** the line's height at the puff's place along it is h_a + (h_b − h_a)·s/L − s(L − s)/(2kR)
  (4 m down at mid-line over 15 km, 15 m over 30 km; eyes and smoke both sit at 20-40 m, so it counts).
- **Cheap first:** a miss over 3.5σ in plan or height is skipped; a puff crossed whole (both ends over 3σ away) takes
  one exp and no erf. Our own erf (.NET has none), a fixed polynomial.
- **Several heights at once:** the plan-view part once a puff, the vertical Gaussian per height band.
- **Early out** at τ > 6 for a band.

**The pair cache:** τ per band for an (observer, target) pair, either way round (τ is symmetric), refreshed every
`SmokeRefreshMs` (5 s), sooner when a burst or a screen puff landed since in a cell the line crosses. Keyed by time,
so the same in any order.

### Into sighting

`SightLine` gains the smoke's transmission T for up to three bands of what shows over the horizon (from the
silhouette's area-above-height table: hull, upper works, tops). The margin is the best over j of the area above band j
at the contrast × the worst T at or above it. With T = 1 everywhere that's the whole area, today's margin exactly; a
ship smoked to her upper works still shows her tops, as a small, hard target. `GunnerySystem.MarginOf` takes the same
lines, so readings, ranging and the spotter's view of the fall follow. Own smoke comes from the field: an aft control
in its own plume is blinded by the wind, not by a rule (`ComponentRow.Smoke` stays as the designer's warning).

### Weather and the one optical depth (proposed, for the user)

Haze and smoke are already one thing to the eye: an optical depth along the line, τ = σ_bg·d + τ_smoke, and the
contrast falls as e^(−τ). Today σ_bg is uniform (`SightModel.Extinction` of the visibility, or of the light's reach at
night), so its integral is a multiply and costs nothing. A unified field saves nothing against that. It saves against
a *second* non-uniform field: fog banks, squalls and mist patches with their own index, walk and cache. So the
proposal is to unify at the query, not in the store:

- **One function every sight consumer calls**, `Air.Tau(a, b, bands)`: the background analytic (from `IWeather`
  along the line, the mean of the ends while it varies slowly), plus the local features. One walk, one pair cache.
  The looks, fire control's readings, the spotter, and the lights (`GlowMargin`; searchlights, star shell and gun
  flashes later) all take the same τ, so a fire's glow is dimmed by smoke too.
- **Local weather features later, as a short list beside the puffs**, not puffs in the store: fog banks and squalls
  as large, flat, long-lived Gaussians (σ_h kilometres, σ_z tens to hundreds of metres, low), each drifting at its own
  velocity (sea fog sits, squalls go with the steering wind). They're few (tens), so each line tests them all, with no
  index and none of the smoke's merging, cap or retiring. Same closed form, same erf, drawn by the same splat in their
  own tint: a fog bank shows on the table, and what's drawn is what blocks sight.
- **Light stays out of the field.** Night, twilight and a silhouette against a low sun are illumination, not
  scattering: the sight model keeps them (`LightReachM`, C₀). So does the sun's glare on the haze (looking toward the
  sun the haze is brighter and contrast falls faster). That's a per-line factor on σ_bg by the angle to the sun, a
  candidate for Jutland's bearing-dependent visibility beside the smoke. A later weather item.
- **What changes now:** `SightLine` carries τ_local per band beside the visibility; the store's puffs have a class
  (smoke kinds) and the query a source list, so weather can join without a second structure. Nothing else until the
  weather grows past one per scenario (plan.md 3.2, decision 5).
- **One departure to name:** e^(−τ) is Koschmieder's, which assumes the path glows like the horizon sky. That holds
  for haze and roughly for grey smoke. Dark coal smoke glows less (it hides by darkening, more slowly at low τ), and
  sunlit white smoke glows more. Start with e^(−τ) for all, and give each tint an airlight factor only if the
  yardstick asks.
- **The AI's scalar visibility** (`CaptainLayer`'s reach, `TaskLayer`'s search spacing) stays the background's. Smoke is
  local: the AI meets it through its contacts, not by a smaller number.

### Smoke contacts (decision 4)

- **A plume as a target:** per emitter, a summary kept up as it emits and merges: its top (the highest young puff's
  z + 2σ_z), its showing area (the young trail's length × its height where τ > ~0.5), its contrast (coal's dark against
  the sky, oil's faint). The same `SightModel` functions: the horizon on its top, the area, Koschmieder, the threshold.
- **In the picture:** `ContactLevel.Smoke` below Ship, keyed on the true entity as every contact is (plan.md 3.2,
  decision 2). The fix is a bearing and a rough range (a large range sigma), into the same Kalman track. Seeing the ship
  raises it to Ship as now.
- **The reach:** `SightReach` adds the highest plume top in the world, so the encounter range grows to hold ships whose
  smoke can be seen (an eye's horizon plus a 60 m plume's: ~48 km; more for a fleet's cloud). That holds more ships off
  rails; Z6 measures it.
- **The AI** reads a smoke contact as "something there" (shadow, close to look); it must at least not break on one.

### The drawing

- **The snapshot** carries the puffs' records and `W(t)`, copied every `SmokeCopyMs` (250 ms of real time; ~4 MB at
  Jutland, 16 MB/s), not every fill: the state is closed form, so the drawing is exact in between and a new puff shows at
  most a quarter of a second late. With it, the emitters' shown bits (decision 3).
- **On the GPU:** the records in a storage buffer, uploaded when the copy changes; the vertex shader works out each
  puff's centre, σ and A at the frame's time. That's a GLSL twin of the sim's formulas, held to them by a readback test.
- **Accumulate:** each puff one instanced quad, added into a half-resolution RGBA16F target with mips: rgb Σ τ·tint,
  a Σ τ, the vertical τ being A times the plan-view Gaussian. Each goes into the mip where its σ is 1.5-3 texels, so a
  kilometre-wide puff writes a few dozen texels and overdraw stays bounded at theatre zoom. Adding τ needs no sorting
  and is Beer-Lambert's exactly, the sim's vertical τ.
- **Resolve:** one full-screen pass summing the levels (B-spline bicubic), α = 1 − e^(−τ), the tint rgb / a.
- **Two looks on one τ:** the table (Tactical and Operations zoom bands) a chart's wash, maybe with a line where sight
  is blocked at eye height; the world view lit smoke, its puffs lifted up the screen by z·tan(pitch) (the effects
  camera), noise in the air frame (it drifts with the smoke, never swims; multiplicative about 1, so the mean τ holds),
  one tap toward the sun for a lit edge. Shadows on the sea and ships: the VFX pass.
- **A pipeline exception to approve:** the offscreen target and its passes (the muzzle-flash plan reserved it).

## The steps

Each step reports its costs on the `shipgen smoke` bench and leaves the field off by default.

### Z0 The model and the facts
- `SmokeModel` (Sim.Combat, pure): a puff's state at an age, the line integral with the curvature, the erf, the merge's
  moments, the vertical τ.
- `FunnelRow` in `ShipClass` (placeholders flagged); the propellant's smoke facts in the gun's data.
- Tests: the research's single-trail table; its quadrature check (6.0161e-3); a merge keeps A, the centre and the
  second moment; the erf against tabulated values.

### Z1 The store (headless)
- `SmokeSystem` (an `IWorldSystem`): `SimSettings.Smoke` (off), funnels off rails with the backfill, merging, retiring,
  the cap, the hash, the coarse tier's emission along its segments.
- `shipgen smoke [scenario] [--minutes N]`: puffs and MiB over time, by source and age class; the store's ms a game
  second.
- Tests: no allocation once grown; a replay hashes the same; puffs per trail against the research's (~450 for 30 min
  merged).

### Z2 The index and the query (gate 1)
- The per-encounter index in the air frame; the walk; the pair cache with the dirty cells.
- The bench: the research's single trail across and along; Jutland's lines (every look's pairs, small first: a
  squadron, then the full 3 hours); a 40-ship fight in a 5 km box (the pile-up case).
- **Gate 1:** the budget above. If it fails, the study stops and records why.

### Z3 Sighting and fire control read it
- `SightLine`'s bands and the margin through the one optical depth (`Air.Tau`, if agreed); `MarginOf`; `GlowMargin`
  taking the same τ; the scenario test that smoke off changes nothing (hashes as before).
- The yardstick: Jutland's directional visibility and the trails' bands; `fc-history` and `duel` with the field on and
  off, beside the record.

### Z4 The other sources
- Guns (black and brown powder against smokeless), fires, magazines and sinkings; `Command.MakeSmoke` and a dev
  `-smoke=SHIP:on`.
- Benches: a destroyers' screen hides a target from a battle line (the USNI case); Santiago's and the Yalu's powder
  smoke against their records.

### Z5 The drawing (gate 2)
- The snapshot's copy, the shader's twin, the accumulation and resolve; the table's wash and the world view's plain look;
  the drawing rule.
- Captures beside the stylised plumes (`-screenshot`, never desktop grabs); `-gputime` at theatre and ship zooms.
- **Gate 2:** the user looks and picks the world view's look (decision 2), and whether the 2026-10-19 decision is
  reverted.

### Z6 Smoke contacts
- The plume summaries, `ContactLevel.Smoke`, the bearing fix, the reach; the map draws a smoke contact as a bearing
  and a smudge, not a ship.
- The cost of the larger encounter range on the Jutland run.

## What stays out

- A flow solve, eddies, blast tunnels and wakes stirring the smoke: the research's visual-only detail grid, a VFX-pass
  item if ever.
- A wind that varies by place: when the weather has one.
- Fog banks, squalls and the sun's glare on the haze: weather items that join the one optical depth later (proposed
  above), not built here.
- Spotting from aircraft (none in 1890-1925): the vertical τ is there for it.
- Land: smoke drifts over it unchanged (islands are rare at sea).
- Night: smoke lit by fires, flashes and searchlights comes with the night pass.
- Signals through smoke: 5.1e reads the same query when it comes.
- The captains' and admirals' use of screens (command.md 5.1: a task): after the study, if it's taken in.
- A sight fan for the selected ship (the research's per-observer polar map): a UI item if the study is taken in.

## Research wanted (filed in `research/REQUESTS.md` 2026-10-20, "Smoke in the record, 1890-1925")

1. Funnel smoke by plant and fuel (coal natural and forced draught, oil, mixed firing): how dark, how high it rose, how
   long trails lay in a given wind.
2. How far smoke was sighted before the ships, by period, fuel and weather.
3. Screens: how fast destroyers laid funnel smoke and chemical smoke, how long a screen held by wind.
4. Black and brown powder gun smoke at the Yalu and Santiago: how long it hung over a firing ship, what it did to her
   gunnery.

## Open items

- What's drawn of an enemy's smoke laid before she was seen, and after she's forgotten (decision 3's edges).
- `ComponentRow.Smoke`: kept as the designer's warning, or gone with the field.
- The map's display-only smoke exceptions (muzzle-flashes.md decision 1, hits-and-fires.md decision 2) close if the
  field is taken in.
