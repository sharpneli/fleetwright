# Plan: the smoke field (a feasibility study)
Status: **drafted 2026-10-20**, decisions 1-4 from the user ("Decided" below); weather's one optical depth proposed (its
first part built with Z3); Z0 (the model and the facts), Z1 (the store), Z2 (the query; gate 1 passed) and Z3 (sight and
fire control read it) and Z4 (the other sources) done 2026-10-21, decisions 5-9; Z5 (the drawing) built 2026-10-21, gate 2 the
user's; breaking the field (Z5b-Z5d: elongated puffs and merging across trails, the large eddies, the grain) agreed and
built 2026-10-21, decisions 10-11; **taken in and on by default** 2026-10-21, shorter lived (decisions 12-13)    Updated:
2026-10-21

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
- **The same at any time compression** (the user, 2026-10-21: the core rule). The field steps on the sim's fixed tick;
  its sources lay puffs on a lattice of world times (a source's own spacing, never a frame's or a count of ticks), and
  its caches and index rebuilds fall due by world time, so 1x and the fine tier's fastest give the same field and the
  same hash (`ScenarioTests.A_fight_is_the_same_at_any_fine_speed` holds the world to it today; Z1 runs it with the field
  on). What's drawn of it ages on the world's clock too, as the map's trails do since 2026-10-21. In the coarse tier
  ships move differently (plan.md 2.8, accepted 2026-10-13); the smoke follows their motion on the same lattice and adds
  no difference of its own.

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

Z0's (the user, 2026-10-21):

5. **The five changes to physics** (Z0 below). "The funnel output is fine and we don't need to do forcing yet. We'll
   build the systems first using reasonable values and do tuning when it's completed as a whole": well-fired yields
   only, no forcing, stoking or banked fires, and no tuning toward the research's 1,500 m²/s (or anything else) until
   the field is whole.
6. **The smoke facts from shipgen** (version 49), not placeholders.
7. **The air's temperature over the sea's** as a weather fact (neutral by default).
8. **The map's funnel plumes read the field's output**, with no need to hold today's look.

Gate 1 (the user, 2026-10-21):

9. **Speed over freshness.** "It's fine if the effect of smoke takes longer, battleship engagements take a long time
   after all. We do prefer speed in this case." The index and a pair's answer hold for 15 s of world time each
   (`SmokeIndexMs`, `SmokeRefreshMs`), so new smoke can take up to 30 s to reach a sight line; the budget holds at
   Jutland's peak.

Breaking the field (the user, 2026-10-21, after the first look: "think about how we could break the smoke field bit
more, and think about potentially merging the blobs earlier into bigger ones"):

10. **The large eddies' meander is the sim's** (Z5c), not the drawing's alone: the gaps it opens are gaps sight looks
    through.
11. **Trails merge across each other** ("Smoke is smoke", Z5b): a fleet's old smoke becomes one cloud. Across sides
    too ("let's not keep artificial separation between sides"): a merged puff is drawn by its trail's rule (decision 3),
    so own smoke merged into an unseen enemy's trail can drop from the player's map, and the user takes that.

Taken in (the user, 2026-10-21, after Z5b-Z5d and a few test matches: "It looks awesome. You may enable it by default"):

12. **The field is on by default** (`Smoke` true; `--no-smoke` for the CLI's benches, `-smokefield=0` for the game).
    The 2026-10-19 decision (no simulated smoke in the first game) is reverted. The look stays as it is now.
13. **Shorter lived, for the cost** ("The drifting smoke, while realistic, doesn't seem to serve too much of a tactical
    purpose so we can relax the realism in this case for performance"): every puff fades, `SmokeLifeS` (below,
    "Shorter lived").

## The model

Z0 (2026-10-21, decision 5) makes five parts of this physical where the draft has tuning values: the funnels' output, the
rise, downwash, the spread and the sea under the smoke. The detail is in Z0 below; where they differ, Z0 holds.

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
  facts are already the gun's data (`PropellantRow`, Sim.Ships) and `MuzzleFlash.SmokeM2` (Sim.Combat) is the puff's A,
  read by the map today (checked 2026-10-21): nothing to move.
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

### Weather and the one optical depth (proposed, for the user; its first part built with Z3)

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
  weather grows past one per scenario (plan.md 3.2, decision 5). *Built with Z3* (2026-10-21): `SightLine.Smoke` and
  the contrast through σ·d + τ in `SightModel`; the rest (local features, the sun's glare) stays proposed.
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

### Z0 The model and the facts (done 2026-10-21)

Nothing in Z0 runs in a battle: it's the pure model every later step calls, the facts it needs from the design and the
weather, and their tests. It's also where the field becomes a good one or not. The store, the index and the drawing
(Z1-Z5) only carry what Z0's formulas say; the formulas are cheap to get right now and dear later, when every bench
reads them. So five parts of the draft's model go from tuning values to physics, each still closed form in a puff's
age (nothing is stepped):

1. **The output from the fuel burnt** (in place of one 1,500 m²/s). Ȧ = ṁ_fuel × Y × ε: the fuel rate from shipgen's
   own SFC and part-load curve at the power she's making (`PowerAt` of her speed through the water), Y the particles
   made per kg of fuel, ε their mass extinction, m²/g. Y interpolates on the coal share and the load: a well-fired
   cruise up to forcing at full power, worse again at overload; oil well fired a fraction of coal's, burning rich (a
   screen) as much as coal's [EST until request 1]. Split among the funnels by the boilers each serves. The check that
   matters: a 17 MW coal plant burns ~4.5 kg/s; at a 1% yield (hand-fired bituminous coal's particulate) and ε ≈ 5 m²/g
   that's ~225 m²/s, and the research's 1,500 m²/s (itself INFERRED) asks for ~7%, dense black forcing. Z0 builds the
   formula, reports both, and the record (request 1) picks the yield; until then the default is the research's, so
   the yardstick table holds.
2. **The rise from the funnel's gas** (in place of zEq, tRise). Briggs: the buoyancy flux F = g·V̇·(T_g − T_a)/(π T_g)
   from the funnel's gas flow at the power (its exit area, the gas's speed and temperature, all shipgen's); the rise
   grows as 1.6 F^⅓ x^⅔ / U with x = U_rel·t (closed form in age) to its final rise (neutral 38.7 F^⅗ / U, stable
   2.6 (F / (U s))^⅓), U the wind relative to the ship at birth. One curve, z0 + ΔH·r(t / t_R), for every source:
   funnels and fires take ΔH and t_R from Briggs (a fire's F from its MW), gun puffs from the muzzle-flash research (0.6
   λ_f over 3 + 0.05 λ_f s, as `SmokeBook`). For a sense of it: F ≈ 90 m⁴/s³ a dreadnought's funnel at full power,
   ΔH ≈ 30 m in a 20 m/s relative wind and ≈ 60 m in 10 (neutral). This is the research's "smoke height" risk: whether
   smoke passes over the 20-40 m eyes or through them is decided here.
3. **Downwash.** When the gas leaves slower than 1.5 times the relative wind it can't clear the funnel's lee: the plume
   starts lower by 2r(1.5 − w/U) (Briggs' stack-tip downwash), never below the upper works under it. The gas's speed
   falls with the load, so a cruising coal ship's smoke streams low aft, and a ship at speed into the wind smokes her
   own after positions. Colossus' and Nagato's trouble comes from the field, not from a rule (`SmokeReach`).
4. **The spread from the air's turbulence** (in place of K_h = 5). Taylor's: σ² = s0² + 2σ_v²T_L²(t/T_L − 1 + e^(−t/T_L)),
   growing as σ_v·t at first (as the map's puffs do today) and as 2K·t later, K = σ_v²T_L; horizontal and vertical each
   with their own σ_v, σ_w. These come from the wind and one new weather fact, the air's temperature over the sea's
   (K): warm air over a cold sea (the North Sea in May) is stable, and trails lie thin, low and long; cold air over a
   warm sea spreads and lifts them. Interpolated on it, no stability classes (no hard switches). The defaults give the
   research's K_h ≈ 5 m²/s in neutral air at 8 m/s [INFERRED] until request 1. A merged puff's age is found from its σ
   by a fixed few Newton steps (Taylor's isn't inverted in closed form).
5. **The sea under it.** Smoke doesn't go into the sea: each puff's vertical Gaussian is reflected at the surface (an
   image at −z), in the line integral and in the column. Without it an old, low puff loses up to half its smoke on a
   low sight line.

Kept from the draft: the line integral exact for a sloped line (the whitened 3D form; the research script's is flat,
the cost the same), the curvature, the cheap-first culls and the crossed-whole shortcut, our own erf, the merge's
moments, the vertical τ.

**What Z0 builds:**

- `SmokeModel` (Sim.Combat, static, pure, nothing allocated): `Erf`; `Spread` (Taylor); `Rise` and `Briggs` (ΔH, t_R,
  the downwash); a puff's state at an age; `TauLine(a, b)` with the image and the curvature, and its per-band form;
  `TauColumn` (the vertical τ); `Merge` (the moments, the age back from σ). Its inputs a small `SmokeAir`: σ_v, σ_w,
  T_L and s from `WeatherHere`, and the wind at plume height (the 10 m wind on a sea profile, ^0.11).
- **The facts from the design**, from shipgen rather than placeholders, since its plant model already holds them:
  `Powerplant.SmokeYield(fuel, draught, load)` beside `SmokeK`; the plant report adding the gas's temperature; each
  funnel's exit area (the funnel plan sizes them). In the class: `FunnelRow` gains its exit area and its share of the
  gas; a class `SmokeFacts` (the fuel rate over the power's share by SFC and part load, the yield, the gas's
  temperature and speed at full). Shipgen version 49: every class recompiles and the hashes move; no design changes.
- **The weather:** `WeatherHere.AirOverSeaK` (0, neutral, the default: no scenario changes); `-weather=` takes it fourth.
- **One implementation:** the map's `FunnelSmoke` reads the class's Ȧ a funnel instead of its own 1,500 × (v/v_max)³.
  Its look keeps a gain so Jutland's plumes look as approved (captures before and after), and the stylised 8 s life
  stays: the drawing's look is Z5's.
- **The bench** `shipgen smoke facts [design...]`: per class, Ȧ a funnel at 10 kn, cruise, full and overload; F, ΔH and
  the downwash at relative winds of 5, 10 and 20 m/s; the trail's three τ (column, across at plume height, 500 m
  along) at 1, 5 and 30 min in neutral and stable air; and the coal dreadnought at full against 1,500 m²/s.

**Tests** (`SmokeModelTests`):

- The erf against tabulated values (under 2e-7), odd, its limits.
- A puff's line τ against numerical quadrature: the research's case (6.0161e-3), a sloped line, a line over the
  curvature, a puff near the sea (with its image).
- The research's single-trail table from the line source's closed forms (column λ/(√(2π)σ_h), across λ/(√(2π)σ_z),
  along λL/(2πσ_hσ_z)): a trail of puffs at the laying spacing gives 4.0 / 2.0 / 0.75, T 0.003 / 0.05 / 0.22 and 80 / 20
  / 3.7 within 2% with the sea far below, which also shows the 1.5σ spacing loses nothing.
- The image: a puff on the sea's column holds its A once.
- A merge keeps A, the centre and the second moments, and the age it's given reproduces its σ.
- Taylor's limits (σ_v·t early, 2K·t late); Briggs against a textbook case; no downwash at w ≥ 1.5U.
- The facts: every shipped design compiles with funnel shares summing to 1; Ȧ rises with the power; oil under coal.
- Nothing allocated; the line τ's cost per puff timed (the number gate 1 builds on).

**Built** (decisions 5-8): `SmokeModel`, `SmokeAir`, `AirFrame`, `SmokePuff` (Sim.Combat, `SmokeModel.cs`) and
`SmokeSources.Funnel` (`SmokeSources.cs`, the one funnel source: the map's `FunnelSmoke` now draws it, knobs `gain,
every, life, spare, on`); shipgen's `Powerplant.SmokeM2PerKg`, `GasM3PerKg` and the plant report's `gas_temp_k`,
`gas_m3_per_kg`, `smoke_m2_per_kg`, `fuel_shares`, `fuel_kg_s` (version 49, 326 golden cases, the report only);
`FunnelRow.Share` and `ExitAreaM2`, `ShipClass.Smoke` and `FuelKgS`; `WeatherHere.AirOverSeaK` (scenarios'
`air_over_sea_k`, `-weather=`'s fourth value, `Command.SetWeather`'s B in mK); `shipgen smoke facts`; 20 tests
(`SmokeModelTests`).

**Departures from the plan above:**

- **The air's frame is a rotation of the sphere,** not east and north metres: the wind's drift as a rotation about
  the axis that carries the reference point along it, composed step by step, so a puff drifts exactly for hours and a
  turning wind stays exact. A theatre's breadth from the reference point its direction is out by the angle it spans
  (~1° a 100 km).
- **A merged puff's age is the A-weighted mean,** not a Newton solve: Taylor's growth is convex in age, so what the mean
  age leaves of the variance is never negative and goes into its s0².
- **The rising plume spreads as it rises** (Pasquill's Δh/3.5), as well as by the air's turbulence.
- **The research's quadrature check (6.0161e-3) isn't reproducible** (its script isn't in the repo and the case isn't
  stated); the tests check the line integral against our own quadrature of the full 3D Gaussian and its image instead,
  level, sloped, over the curvature and on the sea, within 0.5%.
- **No mixed firing:** shipgen's plants burn one fuel; the coal share waits for shipgen to have one.
- **Gun smoke needed nothing moved** (`MuzzleFlash.SmokeM2` is already the sim's).

**What came out** (`shipgen smoke facts`, neutral air, Jutland's 8 kn wind; reasonable values, untuned, decision 5):

| | Dreadnought (coal) | Queen Elizabeth (oil) | Fleet destroyer (oil) |
| --- | --- | --- | --- |
| Smoke at full power, the ship | 342 m²/s (0.23 of the research's 1,500) | 74 m²/s | 40 m²/s |
| At 10 kn | 63 m²/s | 9 m²/s | 1 m²/s |
| Gas out at full, at 10 kn | 9.1, 1.7 m/s | 13.1, 1.6 m/s | 14.7, 0.3 m/s |
| Rise at full, relative wind 10 / 20 m/s | 81 / 41 m | 115 / 57 m | 57 / 28 m |
| Her trail after 1 min: column τ, T across at its height | 0.60, 0.54 | 0.10, 0.90 | 0.05, 0.95 |
| After 30 min: σ_h, σ_z | 76 m, 133 m | 78 m, 139 m | 73 m, 96 m |

- A coal battleship's trail at speed is a grey band (half the light through it at a minute old), oil ships' a haze.
  Along a column it's opaque: 500 m along the dreadnought's minute-old trail, τ 5.8.
- **The plumes rise over most eyes:** 40-115 m at speed, against eyes at 20-40 m. This is the research's "smoke height"
  risk, live; downwash brings them down only a few metres (cruising coal ships most). Mild stability (+2 K at 8 kn, L ≈
  200 m) doesn't cap the funnels' rise; it thins σ_z by about a third at 30 min.
- A puff on a line costs ~130 ns in Debug (gate 1 builds on it, Z2).

Left for the tuning when the field is whole (decision 5): the yields (the record, request 1), forcing and stoking,
whether the ship's own wake and superstructure lee hold her smoke lower than Briggs' chimney formulas (likely: they
were made for stacks on land, in a wind that isn't a ship's own), and the turbulence's constants.

### Z1 The store (done 2026-10-21)

**Built:** `SmokeSystem` (Sim, registered seventh), behind `SimSettings.Smoke` (off), its keys `Smoke*` in
settings.json; `shipgen smoke run [SCENARIO] [--minutes N] [--every N] [--speed X]`; `SmokeSystemTests` (7), and
`ScenarioTests.A_fight_is_the_same_at_any_fine_speed` with the field on and off.

- **A tick on a lattice of world time:** every `SmokeTickMs` (1 s) in the fine tier, `SmokeCoarseTickMs` (60 s) in the
  coarse (the coarse lays the same smoke in fewer, wider puffs: cheap, as the user asked of it). It starts when a ship is
  off rails and stops when no trail is left; the air's frame is carried on, each ship's smoke since her last tick laid, a
  slice of the trails walked.
- **One trail a ship,** not one a funnel: her funnels' plumes summed, the rest A-weighted, their spread over her length
  in the puff's σ (the research's count was a trail a ship; the plumes join within a few lengths). A tick's run through
  the air is cut into puffs at most `SmokeSpacingM` (20 m) long, each laid where she was then; a longer run (the coarse
  tier's) widens them along it.
- **Her plume is worked out every `SmokePlumeMs` (10 s)** of world time, not every tick: a boiler's output follows the
  power over tens of seconds, and the funnels' physics was most of the laying's cost.
- **Off rails only;** coming off, she lays her trail back `SmokeBackfillS` (30 min) along her course at her speed, aged
  as if laid then, in chunks a quarter of their age (a few dozen puffs, not thousands). A ship stopped keeps her trail
  and lays nothing; one going down makes none; a trail no ship feeds ages out and is forgotten.
- **Merging at 2σ, not the plan's 0.5σ:** Gaussians 2σ apart along a line ripple ~1.4% (3σ apart ~22%), so a trail
  merged to 1-2σ spacing is as good for sight as one laid every metre. At 0.5σ a trail grew without end in light airs;
  at 1σ it halved; at 2σ it halved again. Each trail walked every `SmokeMergeEveryMs` (30 s); the walk works on each
  puff's plan view (`SmokeModel.Plan`: centre, σ_h, A) and does the full state only for a merge. Over `SmokeCap`, twice
  as far and every trail.
- **The world hash** now holds the weather's air-over-sea temperature (Z0 left it out).

**The bench** (`shipgen smoke run`, a world of water, the clock held through contact, 1x):

| | Squadrons (9 ships), 20 min | Jutland (250 ships), 10 min | Jutland, 60 min | Jutland, 3 hours |
| --- | --- | --- | --- | --- |
| Puffs (a trail) | 2,427 (270) | 23,073 (95) | 31,858 (131) | 48,129 (193) |
| Memory | 0.3 MiB | 2.5 MiB | 2.5 MiB | 5.0 MiB |
| The field a game second | 0.028 ms | 0.094 ms | 0.108 ms | 0.10-0.14 ms |

- **Inside the store's budget** (0.3 ms a game second, 16 MiB) at Jutland through all three hours, with room: the count
  grows by ~1,300 puffs every 10 minutes after the first hour, a fifth of the cap at the end.
- Laying and merging cost about the same; the first minutes' backfill (every ship coming off rails at once) costs ~0.6 ms
  a game second for a few minutes.
- Retiring depends on the light: destroyers' and oil ships' thin trails go within the hour; a coal battleship's trail
  still holds a column τ over 0.005 hours later in Jutland's 8 kn (as the record's haze would have it).
- The research's ~450 puffs a trail for 30 minutes merged: ours 100-270 (merging at 2σ, one trail a ship).

**Tests:** off by default; ships on rails lay none (a spawned ship lays until the rails take her, then her trail only
ages); a ship's trail astern and back along her course (~10 km for half an hour at 18 kn into the drift); trails bounded
(one ship: 228 puffs after an hour, 287 after two); the same world at 1x and 240x, and on replay; nothing allocated over
half an hour of six ships smoking once grown.

**Open, for the steps after:** what the snapshot carries (Z5's); `ComponentRow.Smoke` still unread; the index and the
query (Z2, gate 1).

### Z2 The index and the query (done 2026-10-21; gate 1 passed, decision 9)

**Built:** `SmokeSystem.Query.cs`, `SmokeModel.TauGrid` and `LinePuff`; sighting asks the query for each observer it
tries (`Sightings.Sight`) and doesn't use the answer yet (Z3). `shipgen smoke run` reports the lines and
`shipgen smoke pileup` the pile-up case; tests: the index finds what summing every puff finds (12 pairs, all through
smoke, to 1e-9), the same world at 1x and 240x with lines asked (the squadrons fight), nothing allocated over half an
hour of an engaged column.

- **The index,** rebuilt every `SmokeIndexMs` (15 s, decision 9) of world time at a tick: each puff's state then, readied for
  lines (`LinePuff`: its σs and normalisations worked once), in trail order, in chunks of `SmokeChunk` (8) with a
  bounding sphere and a height range each. **Not the plan's per-encounter 2D grid:** the chunks are filed in a 3D hash
  grid in the air's frame (Earth-centred, so no projection, anywhere on the globe), at levels `SmokeGridM` (2 km) ·
  2^k, each chunk at the smallest level as wide as its sphere, in at most eight cells. Smoke lies along trails and a
  trail is already in order, so the chunks are tight without sorting anything.
- **The rebuild lays the store out again in trail order** (the arrays swapped): the walks run in memory order after,
  which was most of the rebuild's cost.
- **A query is a pair:** the observer's highest four eyes to the target's three bands (`SmokeBands` of her top, a
  placeholder Z3 sets), twelve optical depths. Its chord goes into the air's frame once and walks each level's cells
  (Amanatides and Woo in 3D); chunks are skipped by their sphere and their height range, puffs by their reach; the rest
  summed, stopping once all twelve are past τ 6.
- **The pair cache** holds an answer for `SmokeRefreshMs` (15 s, not the plan's 5: decision 9) of world time; pairs not asked for
  a minute are let go at the rebuild. World state, hashed. The plan's dirty cells come with Z4's bursts and screens.
- **Tried and dropped:** whole-trail spheres (at Jutland every line met every trail: ~1,750 chunk tests a line); a
  one-level 2 km grid (old puffs' chunks filed in hundreds of cells: the rebuild 1.7 ms a game second); our own exp
  (3.7 ns against Math.Exp's 2.8; the sim uses Math.Exp everywhere).
- **Benching:** a headless run lasts a second or two of wall time and never leaves the JIT's first tier (a query timed
  at 16 µs cold, 1 µs optimised). The numbers here are with `DOTNET_TieredCompilation=0`, the steady state a long game
  reaches. A profile's "waiting for GC" time was the sampler's own suspensions: the run collects nothing.

**Gate 1 at Jutland** (3 hours, a world of water, the clock held through contact, ms a game second), first with the
index and the answers held 10 s:

| | 0-2 h | 2½ h | The main fleets' action (2½-3 h) | Budget |
| --- | --- | --- | --- | --- |
| Store (lay, merge, retire) | 0.11-0.14 | 0.14 | 0.14 | 0.3 with the index |
| Index | 0.13-0.25 | 0.26 | 0.27 | |
| Queries | 0.01-0.09 | 0.15 | 0.62 (51 worked out of 512 asked; 12 µs, 63 puffs summed each) | 0.5 |
| **Field total** | **0.25-0.47** | **0.55** | **1.04** | **0.8** |
| The whole sim, field off / on | | | 2.02 / 3.10 | |

- **Under budget for the battle but its last half hour,** and over by ~1.3x there: queries 0.62 against 0.5, store and
  index 0.41 against 0.3. At the worst moment 240x still holds: the sim thread's 485 ms a real second becomes 744.
- **The pile-up** (2 × 20 circling in a 5 km box): 0.11 ms a game second all told. Sighting stops asking once a held
  contact is ranged, so lines stay few however the smoke overlaps.
- **Memory:** 5.5 MiB at three hours.
- **Fire control's readings** don't ask yet (Z3); they look along the same observer-target pairs, so mostly from the
  cache.
- **The user's call (decision 9):** both held 15 s. The main fleets' action then costs store 0.14, index 0.18 and
  queries 0.41 ms a game second (34 lines worked out of 512 asked), **0.74 in all against 0.8: gate 1 passes.** The
  whole sim there 2.77 ms a game second (2.02 with the field off). Most of the battle stays under 0.35.
- **The chunks' spheres are 25 m wider** than their puffs' reach: their test is against the chord between the ships,
  which dips under the sea's curve (18 m mid-way on 30 km), and a puff at the edge of its reach mustn't be left out
  (found by the index test: 2e-6 off before).
- **Left for later, if it's ever needed:** an adaptive refresh (clear and opaque lines held longer), the dirty cells
  for bursts and screens (Z4).

### Z3 Sighting and fire control read it (done 2026-10-21)

**Built:** `SightLine.Smoke` (a `SmokeTau`: the depths to the target's three bands) and `SightModel.Margin` through it;
`SmokeModel.PairEye` and `EyeTau` (an eye's depths from a pair's rows); the looks (`Sightings.Sight`), fire control's
readings and the spotter (`GunnerySystem.MarginOf`) and a fire's glow (`GlowMargin`) all read the one pair's answer.
`--smoke` turns the field on for the benches (`duel`, `fc-history`, `scenario`, any command reading the settings);
`duel --wind FROM,KN`; `smoke run` reports the looks' lines by direction and whose smoke is on them. Tests
(`SmokeSightTests`): no smoke is the margin as it was, smoke on the hull leaves her tops, smoke lying high leaves her
hull, an eye reads its row or between two, a glow dimmed, a chase down the wind lost in its smoke (the looks and the
guns).

- **The one optical depth, its first part** (the proposal above, what it says changes now): the contrast falls as
  e^−(σd + τ_smoke). No `Air.Tau` function of its own: the background stays the analytic multiply in `SightModel`, the
  smoke's part the one walk and cache (`SmokeSystem.Tau`) every consumer reads.
- **The margin through the bands:** her height in three slabs about the band heights (`SmokeBands` 0.2 / 0.5 / 0.85 of
  her top; edges half-way between, the waterline and her top); the eye makes out the best of any run of slabs (the whole
  ship, her upper works and tops, her tops, her hull under smoke lying high), its area at the contrast through the worst
  depth on it. The plan's "area above band j at the worst T at or above it" left a hull under high smoke unseen; the runs
  hold that case and the plan's. With no smoke it's today's margin, by the same path, bit for bit.
- **An eye's depths:** the pair's four rows are the observer's eyes when she has four or fewer, else her highest, her
  lowest and two evenly between (by index); an eye between two rows reads between them by height. The looks try her four
  highest eyes that bear, fire control its instruments wherever they stand, so a row per eye index (Z2's) left a lower
  eye with none. Rows for real eyes are exact.
- **Fire control and the spotter** ask the same pair (observer, target) as the looks, so from the cache: in the chase
  test 1,190 asked over ten minutes, 80 worked out.
- **Smoke off changes nothing:** squadrons, 30 min, the hash EA2179573189244F before and after.
- **Untuned (decision 5):** the field's output and plume as Z0 left them; no factor here.

**The yardstick, with the field on and off:**

- **Jutland, the looks' lines** (`smoke run`, 3 h, the clock held through contact), by the observer's side (0 the
  British) and her look against the wind (WSW, 8 kn): the share of lines with τ over 1 at the target's middle band, and of
  those her highest eye makes out without the smoke, the share lost to it:

  | | 1 h | 2 h | 2½ h | 3 h (the main fleets' action) |
  | --- | --- | --- | --- | --- |
  | British, across the wind | 27%, lost 30% | 72%, 50% | 87%, - | 82%, 96% |
  | British, up it | | 74%, 17% | 95%, 60% | 0% (5 lines) |
  | Germans, across | 3%, 35% | 8%, 33% | 28%, 40% | 75%, 82% |
  | Germans, up it | | | 4%, 44% | 62%, 98% |

  Whose smoke, on a sample of 40 smoky lines: mostly **the target's side's** (41% at 1 h, 68-86% from 1½ to 2½ h, 48%
  at 3 h), the observer's own 4-27% (the most at 1 h and 3 h), her own side's 3-19%. A fleet's own cloud screens it, and the
  wind decides whose cloud lies where: the record's bearing-dependent visibility, in direction. In size it's well over
  the record at the climax: almost every line made out without the smoke is lost with it, where the record has ranges
  of 9-15 kyd and clear channels.
- **Jutland, the fight** (`scenario --no-land`, 3 h): 1,099 hits from 17,671 shells with the field, 1,704 from 23,170
  without (35% fewer hits, 24% fewer shells); the same three ships lost (B109 for Cordelia).
- **`fc-history`** (12 runs a case; the cases' winds put the smoke where their records say, in each case's frame; with
  the field off they change nothing). *Corrected in Z4:* at 40 runs a case only the German BCs' case moves (to 0.0%); the
  other moves here are within run-to-run noise (Z4's table):

  | Case (the record) | Off | On |
  | --- | --- | --- |
  | Dogger Bank, British BCs (1.4%: the Germans' smoke on them) | 4.5% | 3.8% |
  | Dogger Bank, German BCs (~2.5%: their own smoke) | 1.6% | **0.0%** (never made out) |
  | Jutland, British 1st/2nd BCS (1.4%) | 3.7% | 3.0% |
  | Jutland, British 5th BS (2.6%) | 2.8% | 2.7% |
  | Jutland, German 1st SG (4.0%) | 4.3% | 4.5% |
  | Falklands, Invincible (~7%: own smoke down the range) | 4.6% | 5.5% |
  | Cases in their bands | 8 of 23 | 7 of 23 |
  | The Germans over the British at Jutland (BCs), want 1.5-5 | 1.17 | 1.50 |

  The cases fired at targets astern into their own smoke go blind (the German BCs at Dogger Bank, firing aft with the
  wind from ahead); the Falklands' 10 kn breeze doesn't reach the line: at 25 kn a ship outruns her smoke, so it can't
  blow down a range forward of the beam (its rise is the runs' spread).
- **`duel`** (super-dreadnoughts, 16 kyd, 20 runs): beam on in any wind, the same to the shot (each trail streams astern
  of its ship, off the line: τ 0, so the same draws); a chase with the target 10° on the bow, 4.2% either way (the line
  meets her trail only near her); dead ahead at 10 km down the wind (the chase test), never in sight and no salvo.
- **The cost at Jutland's climax** (`DOTNET_TieredCompilation=0`): store 0.145, index 0.19, queries 0.49 ms a game
  second (39 lines worked out of 587 asked: the lost contacts keep every observer looking), 0.82 in all against the
  gate's 0.8; the looks with them 1.08 (0.83 at Z2). The whole sim in the fight 101 µs a fine tick with the field, 78
  without (`scenario`); the slowest minute 118x against 142x.

**For the tuning pass** (decision 5, when the field is whole): the climax's blindness and the astern cases' are the
outcome to bring toward the record. The candidates, all physical: the funnels' output (the research's 1,500 m²/s is
4x ours, so not that way), the young plume's σ and its rise (a trail at the eyes' height or over them), Koschmieder's
glow for dark coal smoke (the airlight factor above), and an eye's choice among her posts (an aft control in her own
plume while the foretop is clear).

### Z4 The other sources (done 2026-10-21)

**Built:** each source its own trail a ship (`SmokeSystem.SourceKind`: funnels, guns, fires, a burst), merged and
retired along it as the funnels' are:

- **Guns** (`SmokeSystem.Gun`, from `GunnerySystem` beside `ShellSystem.Fired`; `SmokeSources.Gun`): a puff a mount
  fired, its charges' solids (`MuzzleFlash.SmokeM2`, a share by the barrels that fired), carried along the bore to the
  jet's end (2.5 of the expected fireball's length past the Mach disk), a core 0.35 λ_f across, rising 0.6 λ_f over
  3 + 0.05 λ_f s: the map's gun puffs' numbers (muzzle-flash research §5). Black powder's solids (0.56 of the charge at
  1.5 m²/g) against smokeless (0.001-0.004 at 3): a Devastation's 12" mount makes ~80,000 m² a salvo, a dreadnought's
  ~6,000. The water fog is left out (gone in seconds, before a line could read it).
- **Fires** (`SmokeSources.Fire`, laid each tick as one plume a ship like her funnels'): the map's fire smoke moved
  into the sim (one implementation; `FireEffects` draws it, its `yield`, `growyield` and `level` knobs replaced by a
  `smoke` gain): soot from the heat burnt (18 MJ/kg, a yield from a growing fire's 0.03 to a developed one's 0.08,
  charges' 0.02, 8,700 m²/kg), leaving from the flames' tips (Heskestad, from `ShipSystem.FireOpening`), rising by
  Briggs from its convected heat (0.7 of it). 10 MW developed: 387 m²/s, rising 57 m in 8 m/s. Steam stays the
  drawing's (gone in seconds). No backfill: a fire's smoke starts when it shows.
- **Magazines** (`SmokeSystem.Magazine`, from `DamageSystem.Blew`; `SmokeSources.Fireball`): the fireball's soot at
  once (column τ 8 over σ a quarter of its diameter, centred 0.35 D over the deck, lifting D/2 over two lives:
  `MagazineBlast.BodyAt`'s), from the venting race's propellant (else the room's event's); then the wreck's fires,
  15,000 m²/s times the event's scale squared, fading over 40 s, laid each tick for two minutes. The map's after-smoke
  reads the same numbers (`SmokeSources.WreckSmokeM2s`, `WreckSmokeFadeS`; its `firesmoke` knob a gain, `firefade`
  gone). `MagazineBlast.Magazine` picks the room for both (one implementation).
- **Sinkings** other than by a magazine add nothing: a ship going down stops her funnels' smoke, and her fires' with
  it.
- **Screens** (`SmokeSystem.MakeSmoke(ship, on)`, the dev's `-smoke=SHIP[:on|off]`): her funnels burn her fuel rich,
  `SmokeScreenM2PerKg` (600 m² a kg, some 8% of the fuel as soot [EST, request 3]) where her plant makes less: an oil
  destroyer ×57, a coal ship ×12. The command takes her off rails (smoke is laid off rails). The snapshot carries the
  gain (`ShipsView.SmokeGain`) and the map's funnel plumes draw it (checked by a capture: the screening destroyer's
  plume shows where her consort's hardly does). They keep their stylised 8 s life (2026-10-19), so a screen draws as a
  denser short plume, not the bank the field holds: drawing the field is Z5's. The dev's `-smokefield` turns the field
  on in the game (settings.json's `Smoke` stays off). Chemical floats later.
- **Not built: the dirty cells.** Decision 9 (speed over freshness) covers bursts and screens too: new smoke reaches a
  line within 30 s, a magazine's cloud and a screen as a funnel's trail.

**Tests:** every source lays its own trail (the engaged world with guns firing, a ship on fire, a screen and a magazine
going up), a screen burns rich (×12 on a coal ship, to 2%), a powder salvo's cloud blinds the line for a minute and
clears; the same world at any fine speed and a replay, now with every source going.

**The yardstick:**

- **A destroyers' screen** (`shipgen smoke screen`, the USNI case: three dreadnoughts, a target of theirs 13 km east, four
  destroyers of hers between, 5 km off the line at 25 kn, the wind from the east at 5 m/s, 10 min): the target in the
  line's sight 592 s of 600 with the field off and on, **0 s behind the screen** (τ 3.7 on the guide's line at the end);
  the screening destroyers themselves in sight half the time (99% without).
- **Powder smoke** (no Santiago or Yalu ship fires black powder in our designs: `connecticut`, the Santiago case's
  shooter, is a 1904 design with smokeless; only the ironclads' blocks do, Devastation, Inflexible, Duilio): two
  Devastations at 3 km, 10 kn, 20 runs: a wind across their course or ahead changes nothing (she steams out of her own
  cloud in under a minute, and new smoke reaches a line within 30 s, decision 9); **a wind from astern at her own speed
  keeps the cloud with her: hits 8.8% → 4.5%**, and at 15 kn of wind it outruns her (7.0%). The test sees the line opaque
  (τ ~180) for a minute after each salvo. The record (request 4) asks how long it hung over a firing ship; the cases
  for it need period designs.
- **`fc-history`, 40 runs a case** (Z3's 12-run figures were within run-to-run noise, so these replace them):

  | Case | Off | On (every source) |
  | --- | --- | --- |
  | Dogger Bank, British BCs (record 1.4%) | 4.8% | 4.4% |
  | Dogger Bank, German BCs (~2.5%; firing astern into their own smoke) | 1.7% | **0.0%** |
  | Jutland, British 1st/2nd BCS (1.4%) | 3.4% | 3.9% |
  | Jutland, British 5th BS (2.6%) | 2.7% | 2.7% |
  | Jutland, Grand Fleet BS (3.6%) | 9.8% | 10.6% |
  | Jutland, British 3rd BCS (4.3%) | 21.2% | 20.6% |
  | Jutland, German 1st SG (4.0%) | 4.1% | 4.1% |
  | Jutland, German battleships (3.0%) | 7.2% | 7.1% |
  | Falklands, Invincible (~7%) | 5.1% | 4.6% |

  The duels hardly move: two ships at speed outrun their smoke, and a line meets a trail only at its ship's end, unless
  it runs along it (the astern case, the chase test). The record's smoke at these battles was the fleets' (the next
  item), not a single pair's.
- **Jutland, the fight** (`scenario --no-land --smoke`, 3 h, one run each): 1,363 hits from 17,516 shells with every
  source (Z3's funnels only: 1,099 from 17,671; the field off: 1,704 from 23,170): 20-35% fewer hits with the field, the
  runs diverging as fights do.
- **The cost at Jutland** (`smoke run`, `DOTNET_TieredCompilation=0`, alone on the machine): 280-290 trails (guns,
  fires and bursts beside the funnels), 43-47k puffs. The main fleets' action: store 0.16, index 0.20, queries 0.36 ms
  a game second, **0.72 in all against 0.8**; at 2½ h 0.73. The gun puffs cost little: a few a minute a ship, merged
  along her guns' trail.

### Z5 The drawing (gate 2; built 2026-10-21, the look the user's)

**The pipeline exception, approved** (the user, 2026-10-21): an offscreen half-resolution RGBA16F target with mips, its
accumulation and collapse passes before the main pass and a resolve in it. Logged in muzzle-flashes.md's exceptions.

**Built** (`Map/Smoke/SmokeFieldRenderer.cs`, `SmokeFieldLook.cs`; `Fleetwright.Sim/SmokeCopy.cs`; shaders `smokefield`,
`smokecollapse`, `smokeresolve`, `smokestate.comp`, `Include/smokefield.glsl`):

- **The copy:** the sim's thread copies every puff (`SmokeRecord`, 64 bytes: its offset from an anchor in the air's
  frame, its birth, rise, spreads, area, decay, tint and the drawing rule's flag) when the field has changed and the
  last copy is `SmokeCopyMs` (250 ms) of real time old, into a triple buffer of its own (a copy outlives the
  snapshots). Not every fill: 43k puffs at Jutland take 1.85 ms on the sim's thread, at most four times a real second.
  The air's frame and its drift go with it.
- **Exact between copies:** a puff's state is closed form in its age and the air's frame a rotation at a steady rate,
  so the render thread carries the frame on to its own frame's time (`SmokeCopy.FrameAtMs`) and the vertex shader works
  each puff out there (`smokefield.glsl`, the GLSL twin of `SmokeModel.State`). Only a puff laid since the copy shows
  late, by at most a quarter of a second of real time.
- **The accumulation:** each puff one quad into the level where its σ is 1.5 to 3 texels (never under a level-0 texel:
  a smaller one is widened, its area kept), reaching out to a column τ of 0.005, adding τ and τ × its tint (a plain sum,
  Beer-Lambert's, no sorting). Only the levels the puffs fall in this frame are drawn (the copy carries its least and
  greatest σ, the greatest carried on by the turbulence's growth): three at theatre zoom, six or seven close in.
- **The collapse:** top down at half resolution, each level its own texels plus the B-spline bicubic of the collapsed
  level above, in two chains taken in turn (a pass never reads the texture it draws), so the full-resolution resolve
  reads one level, not every one. (Summing every level per pixel cost spikes of 5 ms at ship zoom.)
- **The resolve,** in the main pass after the magazines' clouds and before the flashes: α = 1 − e^(−τ), the tint the
  depth-weighted mean, lit as the gun smoke is (the sky's and the sea's ambient, the sun and the moon on a dome seen from
  above), premultiplied.
- **Two looks on one τ:** on the table it lies flat; in the world view each puff is lifted its height up the screen (the
  effects camera's tan 45°, by the world view's share). Plain: no noise, no lit edge, no shadows yet.
- **The drawing rule** (decision 3): a flag a trail at the copy: the player's and allied or peaceful sides' smoke always,
  an enemy's once her maker is held as a contact, everyone's in the dev's truth. The open item stands: smoke laid
  before she was seen shows once she is, and goes when she's forgotten.
- **Knobs** `-smokelook=on,gain,lift,edge,parts,levels` for side-by-side captures; the stylised plumes go off with
  `-funnels=on:0 -flash=parts:7 -fires=parts:3` and the field with `-smokelook=on:0`. `-smokefield` turns the field on
  in the game. `FLEETWRIGHT_GPU_DEBUG=1` creates the GPU device with SDL's debug layer (Vulkan's validation when
  installed): it found the build's one device loss (a storage buffer declared to SDL as a storage texture).

**Tests:** the shader works a puff out as the sim does (`SmokeFieldTwinTests`, on the GPU: 500 puffs from every
source, 37.5 s after the copy, within 2e-7 of `SmokeModel.State`), enemy smoke drawn once she's held (in fog not, in
sight and in truth all), and the copy carries every puff. Smoke off: the hash as before.

**The cost** (DevRelease, 1440p, 8x MSAA, `-gputime`, 250 dreadnoughts with their trails, 1x so the runs match; three
runs each, the field on against off, p50): about +0.1 to +0.3 ms GPU at 40, 10 and 1.5 m/px against the 0.3 ms budget,
no spikes left (the p95 within 0.3 ms of off). The render thread's CPU: the frame's view a few microseconds, the copy's
upload (2.7 MB at Jutland) at most four times a second. 19 MB of targets at 1440p (the accumulation and two collapse
chains).

**The captures for gate 2** (a fight of three dreadnoughts a side 11 km apart, a destroyer of one side screening, the
wind from the west, 8x, so the stylised plumes' clock and the world's agree):
`world-field` (the field alone), `world-plumes` (the stylised plumes alone), `world-both`, `table-field`,
`table-plumes`, at 1.5 and 15 m/px. What they show:

- **The gun smoke agrees in place:** the field's puffs sit where the stylised ones do (carried along the bore, drifting
  with the wind). Plain, they're smooth white discs; the stylised ones billow.
- **The funnels' smoke is the field's to show:** the stylised plumes live 8 s (2026-10-19); the field's trails reach
  kilometres downwind, lifted, thinning with age. On the table the stylised smoke fades out by design, so there the
  field is the only picture of the smoke: each fleet's trails as grey bands.
- **Beads:** a trail's older stretches show as beads where they were laid in long pieces (the backfill's, a coarse
  tick's) and haven't merged yet: a puff stands for a stretch, the drawing shows a Gaussian. Smoothing them is a
  drawing matter (stretching a quad along its trail) or the store's (narrower pieces), for after the gate.
- **A screen stops when its ship goes on rails** (outside any encounter): smoke is laid off rails only.

**The height's drift** (the user, 2026-10-21, after the first captures: "the height dependent drift would be good"): the
air's frame moves at the steering height's wind (50 m); each puff now drifts along the wind against it by the sea's
profile at its settled height (`SmokeAir.ShearAt`, `SmokePuff.ShearMs`): in 8 m/s at 10 m, −0.9 m/s at 20 m, +0.8 at
100 m, so a gun's smoke at 15 m falls 1.2 km behind a funnel's at 100 m in ten minutes. Closed form still (the centre
plus its drift times its age along the wind: `SmokeModel.Centre`; the shader's the same); a merge keeps the centroid and
carries on at the A-weighted drift. The rise's own lag (the drift of its lower heights while it climbs, tens of metres)
is left out, and the drift's way is the current wind's (the weather is steady; a wind that turns would want it kept from
birth). The wind's way is worked once a tick at the frame's reference point and taken onto each puff's tangent
(`SmokeAir.WindDir`): `Earth.Forward` a puff cost the index 0.07 ms a game second. Neighbours laid at different plume
heights now drift apart and merge less: Jutland's climax holds ~54k puffs (43k before), store 0.19, index 0.28, queries
0.31 ms a game second, 0.78 against 0.8; the copy 2.2 ms. A trail at one height is still a straight line: the variation
the user asked for next is another matter.

**The wind's wander and gusts** (the user, 2026-10-21: "some variation would be good", trying both of the cheap
physical ones): the smoke's air takes the weather's wind with a slow swing of its way (up to `SmokeWindWanderDeg` 12°
over some `SmokeWindWanderS` 20 min) and speed (`SmokeWindSpeedShare` 10%), and gusts of speed (`SmokeGustShare` 15% over
some `SmokeGustS` 90 s): each a smooth signal of world time (three sines, their phases from the world's seed), so the
same at any speed and on replay. Only the smoke reads it (the weather stays as given; smoke off changes nothing). The air
frame integrates a wind that turns exactly, so a trail laid under an earlier wind keeps its way: an hour's trail on a
straight course strays ~310 m from a line (54 m in a steady wind). The gusts change each plume's rise as it's worked
out, so a trail's puffs settle at different heights and drift apart by the height's drift.

- **The height's drift follows a turning wind:** each puff counts its drift from a time of its own
  (`SmokePuff.ShearFromS`); the store's walk folds the drift so far into its origin (`SmokeModel.Reanchor`) every 30 s,
  and the copy for the drawing folds it in up to the copy (the shader carries it on from there).
- **What shows:** gentle curves over kilometres on the table; near the ships, a few minutes' trail stays nearly
  straight, and the gusts' wiggle is tens of metres (too small to see at the table's zooms). A backfilled trail (laid
  at once when a ship comes off rails) is laid under the wind of that moment, so it meets the live trail at a slight
  kink. Variation at the small scales would be the drawing's (noise fixed in the air's frame, not yet built).

**Gate 2, the user's call:** the world view's look (decision 2: the field alone, the stylised plumes over it, or the
plumes for the head only, the young smoke near the ships, and the field for the rest), and whether the 2026-10-19
decision (no simulated smoke in the first game) is reverted.

### Breaking the field (Z5b-Z5d, agreed 2026-10-21, decisions 10-11)

What the first captures show, and why: every puff is round and spread only by the surface layer's small eddies
(Taylor with σ_v ≈ 0.5 m/s and T_L = 20 s: diffusion after a minute, so a 30-minute trail is a smooth band ~280 m
wide); the wind's wander and gusts are the same everywhere, so they bend every trail alike and break none; and merging
round puffs along a line makes beads, so merging was held back (settled 60 s, centres within 2σ). Three steps, each
with side-by-side captures and the bench's cost, the field still off by default:

- **Z5b Elongated puffs, merged earlier, further and across trails.** A puff's plan view becomes a full 2×2
  covariance, kept as σ_h² (round, what the turbulence adds to) plus σ_a² more along a unit axis: any 2×2 covariance
  is that (its smaller eigenvalue round, the difference along the larger's axis), so a merge keeps the second moments
  exactly and a stretch of trail merges into one long ellipse instead of a bead. Closed form still: Σ(t) = Σ₀ +
  Taylor(t)·I. A laid piece is long along her way through the air (its length²/12 along, not /24 each way: the
  backfill's and the coarse tier's beads go). The sight query's whitened integral takes the 2×2 inverse; the drawing
  an oriented quad. Then merging: settled at 20 s, neighbours within 2σ *along the line between them*, the merged
  ellipse capped (its long σ at most 4 times its short, and `SmokeMergeLongM` unless the short is already wider). And
  across trails (decision 11): every minute, puffs over two minutes old filed in a grid by size, each merged with a
  close one of another trail by the same rule, any side's (decision 11).
- **Z5c The large eddies (the sim's, decision 10).** Boundary-layer and mesoscale eddies, 1.5-12 km, which the puffs'
  Taylor spread doesn't hold: they make a real plume snake and fall apart into clumps. A smooth, divergence-free
  displacement field in the air's frame: eight transverse waves on the reference tangent plane, wavelengths spread
  log-even over `SmokeEddyM`, ways and phases drawn from the world's seed, each turning over in its wavelength over
  `SmokeEddySpeedMs`, its strain `SmokeEddyStrain`. A puff is offset by its own share of it, η·[f(key, t) − f(key,
  born)], with η the air's σ_v at its birth against neutral 8 m/s air's (the turbulence that makes the eddies), so young
  smoke stays at the ship and puffs laid close together move together: a trail snakes, and where the flow stretches it
  neighbours part by more than their σ and it breaks. A merge keeps the centroid exactly (the key and η A-weighted, the
  origin set so it's where the two were). Closed form at any speed and on replay; the drawing's twin evaluates the same
  modes. [EST: ~300 m of meander at saturation in 8 m/s; research request filed.]
- **Z5d The drawing's grain.** The plan's noise in the air's frame, multiplicative about 1 (the mean τ holds), its
  octaves by each puff's size in world metres (no swimming as the zoom changes), in the accumulation: the gun smoke's
  smooth discs and the big ellipses get billows. Below the sim's resolution, so fluff that doesn't mislead.

**Z5b built** (2026-10-21): `SmokePuff.Along2`/`Axis` (the stretch), `SmokeModel.MergedPlan` (the merge's moments,
cheap enough to try first), the sight integrals with the plan's 2×2 inverse (`TauBands` now `TauGrid`'s, one
implementation), `SmokeSystem.Cross`, a 80-byte `SmokeRecord` and oriented quads. Settings: `SmokeSettleS` 20,
`SmokeMergeStretch` 4, `SmokeMergeLongM` 300 (unless at most twice as long as wide), `SmokeCrossEveryMs` 60 s,
`SmokeCrossAgeS` 120. Two changes the bench asked for:

- **A trail lays by its run, not by the tick:** every ship laid a puff a second whatever the spacing, and with merging
  at 20 s nearly every one was merged (most of the store's cost). Now a piece is laid once she's run `SmokeSpacingM`
  (40 m) through the air or `SmokeLayMaxMs` (4 s) has passed: a third of the puffs laid, the same smoke (a piece's
  length is its stretch, exact).
- **`SmokeChunk` 8 → 4:** eight merged ellipses span far more trail than eight small puffs did, and a line tested
  twice the puffs (652 against 330 at the climax); at four, a query costs what it did.

**The bench** (Jutland, 3 hours, `DOTNET_TieredCompilation=0`, half-hour rows; before → after):

| | 60 min | 120 min | 180 min (the climax) |
| --- | --- | --- | --- |
| Puffs | 32,663 → 20,140 | 41,584 → 23,239 | 47,033 → 23,321 |
| Store and index, ms a game s | 0.330 → 0.301 | 0.446 → 0.380 | 0.478 → 0.422 (merging 0.167 of it) |
| Index | 0.173 → 0.130 | 0.247 → 0.170 | 0.267 → 0.190 |
| A query worked out | 7.9 → 5.7 µs | 12.6 → 10.7 µs | 15.5 → 16.1 µs |

Half the puffs; the field about as dear at the climax (the merging's work grew as the index's shrank). Merging across
trails takes a sixth more puffs away at 90 minutes for ~0.02 ms. A trail now grows as √t (its length over its puffs'
width) where round merging let beads grow without end: one ship 108 puffs after an hour, 162 after two (the test now
asks that the second hour adds less than the first). The captures (`world`/`table` at 6 m/px, the gate-2 fight): the
destroyer's screen and the battleships' trails no longer bead; still smooth bands, which Z5c is for.

**Z5c built** (2026-10-21): `SmokeEddies` (Sim.Combat; drawn by the store when the air's frame is first set, carried by
`SmokeAir.Eddies`), a puff's `Key`, `Eddy` (its share) and `Phases` (cos and sin of each mode's phase where it samples
them, fixed with the key: its offset at any time then takes no trigonometry, the clock's turn worked once a time); a
96-byte `SmokeRecord` (the copy's place leaves the eddies out, the shader adds them at its frame's time:
smokefield.glsl's `fieldEddy`, its phases from k·anchor in double on the CPU). `SmokeEddyShortM` 1,500,
`SmokeEddyLongM` 12,000, `SmokeEddyStrain` 0.1 (0 turns them off), `SmokeEddySpeedMs` 2.

- **What it does** (tests): a puff is where it's laid, ~20 m off a minute later, ~430 m an hour later; one laid 20 m and
  2 s away stays within 3 m of it; the field's divergence is nil (1e-7 against strains of 0.19); a merge keeps the
  centroid. One ship's hour-long trail strays 280 m from a line in a steady wind's eddies (54 m without them). The GPU
  twin holds the offsets to 0.2 mm.
- **The look:** trails snake over kilometres and vary in strength along them; at the table's zooms a few merged
  ellipses show as short straight pieces where the snake bends (a puff moves whole; Z5d's grain is for that).
- **The cost** (Jutland, the climax's half hour, ms a game s): merging 0.167 → 0.217, index 0.190 → 0.221, queries
  about as before; the whole field ~1.18 against 1.07 before Z5b and 1.11 after it. 22,498 puffs. The first try (eight
  cosines a puff each time) cost twice that. Merging across trails matters more now: without it, 36,800 puffs (the eddies
  bring trails together). Walking the trails every minute instead of 30 s saved little and held more puffs.

**Z5d built** (2026-10-21): in the accumulation (smokefield.frag), each puff's τ times 1 + g·n, n value noise on a
plane fixed in the air's frame (the large eddies' plane; the anchor's place on it wrapped in double to a 64 km period,
so the grain stays put from copy to copy and never swims as the view moves), three octaves round the puff's own short σ
(8 m and up, doubling; weighted in the vertex shader by their distance from it in octaves, so the grain coarsens
smoothly as the puff grows and doesn't change with the zoom). `-smokelook=grain:G` (0.6; 0 plain). The mean τ holds
(clipped at 0 only where g·n < −1).

- **The look:** the gun smoke's discs get billowed edges; the trails break into clumps and patches on the table and in
  the world view, over the eddies' snaking.
- **The cost:** within the run-to-run noise on a shared machine (1440p, 8x MSAA, `-gputime`, p50 1.54 → 1.56 ms at
  1.5 m/px). The first version (four octaves, their weights and an integer modulo per fragment) cost +0.9 ms close in.
- **Still the plain look's:** no lit edge, no shadows (the VFX pass); the grain doesn't evolve in time (the puffs move
  through it at the eddies' and the height's drift, ≤ 1 m/s).

**Across sides** (decision 11, 2026-10-21): the side check is gone from `SmokeSystem.Cross`.

### Shorter lived and on by default (decisions 12-13, 2026-10-21)

- **The fade:** every puff's extinction decays as e^(−age / `SmokeLifeS`), 600 s, on top of its spread (the puff's own
  decay time, already in the model and the shader for bursts, now set on every source; a merge A-weights it), and it's
  retired as before once its peak τ is under 0.005. Half its smoke is gone in 7 minutes, a trail is gone in about half an
  hour (before: a coal battleship's trail held for hours). A screen kept up still hides (`smoke screen`: four destroyers
  screening, the target in sight 90 s of 30 minutes against none without the fade; their own lines 29% clear against 24%).
  The backfill lays only what hasn't faded already. Jutland, the climax's half hour (field and its queries, ms a game
  second; puffs):

| `SmokeLifeS` | none (physics) | 1,200 s | **600 s** | 300 s |
| --- | --- | --- | --- | --- |
| Field (store, merging, index) | 0.48 | 0.35 | **0.28** | 0.22 |
| Queries | 0.58 | 0.42 | **0.30** | 0.17 |
| Puffs | 22,892 | 14,925 | **10,177** | 7,511 |
| The drawing's copy, ms a copy | 1.99 | 1.46 | **0.86** | 0.69 |

  Halved at 600 s (1.06 → 0.58 ms a game second, against gate 1's 0.8); 300 s would halve it again but cut screens to
  minutes. The looks see further at the climax (down the wind: 77% of the lines over τ 1 before, 65% at 600 s).
- **Idle:** while no ship lays smoke (all on rails, or none afloat), the field ticks on the coarse tier's minute: what's
  left still fades, merges and is retired, and the air's frame is carried; once it's gone, it stops. A ship coming off
  rails wakes it at the fine tick (the coarse tick already due is let go). So a world whose ships are all on rails keeps
  one event a minute for at most half an hour.
- **On by default:** `Smoke` true in settings.json and `SimSettings`. The CLI's benches take `--no-smoke` (and `--smoke`),
  the game `-smokefield=0`. Tests of other systems' own rules in clear air (pictures, deployment, captains, commanders,
  fire orders, fall-back, encounters' and forces' allocations) run with it off; its part in sight is `SmokeSightTests`'.
  Smoke's store grows with its trails (allowed: its own test holds it allocation-free once grown).

Left out: the funnels' unsteady output (stoking and firing cycles: clumps along a trail), by decision 5; shear
dispersion (vertical spread times the wind's shear stretching old smoke downwind), a candidate if old smoke still looks
too round.

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
