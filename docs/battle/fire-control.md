# Plan: fire control (a 4.5 deepening)
Status: **proposed 2026-10-17, for the user to agree** (questions at the end)    Updated: 2026-10-17

The fine-tune pass for the **Fire control** link of `fighting.md` 4.5 ("the trackers by era ... the clocks' limits ...
the full ladder of levels, radar as a sensor, night ... splash confusion ... the designer emitting levels and sensors,
rangefinders among them as parts ... the spotting constant tuned ... the Earth's turning ... point-blank laying by sight,
and a ladder step scaled to the range"). Its research is `research/simulation/fire-control-research.md` (the synthesis,
with `fire_control_ref.py`), `fire-control-designer.md` (tech values to design to combat numbers, `designer_ref.py`),
`manual-gunnery-research.md`, `evasion-research.md`, and the nine raw notes in `research/simulation/fire-control-research/`
(computing, dispersion and hits, radar, component costs, the pre-dreadnoughts, early hit rates, the eyeball, manoeuvring,
evasion). As in the damage plan, the reference code is something to check against, never a second engine.

## The aim

4.2b gave every link of the chain one model, with the reference's numbers. What it lacks is **the difference between
fire-control systems**, which is the whole point of the era: today a Dreyer table and a Ford rangekeeper are the same
tracker with different noise, a ship has one rangefinder a director, and every spotter stands at the director's eye. This
pass makes each system do what it could and couldn't do, from the physical fit the design carries:

1. **What the ship carries:** fire control as design data, its instruments as parts at their real heights, its computer
   and mounts as tech blocks (shipgen).
2. **How it predicts:** a tracker per kind of computer (rate clock, true-course plot, curved-course predictor, turn
   filter), each failing where its machine failed.
3. **How it fires and corrects:** fire discipline (when to fire, how many guns, ranging against fire for effect) and
   spotting by the navy's rules, from tops tall enough to see the fall.

## What there is today (checked 2026-10-17)

- **`GunnerySystem`** (`Sim/GunnerySystem.cs`): per engaged battery a fire-control state, up to 4 sensors (its directors'
  eyes, or a mount's layer by eye), a reading queue with the level's latency, one `Track` (a constant-velocity Kalman
  filter on the sphere, world frame), the solve through the range table (`FireControl.Solve`, the ship's motion in, per
  gun from its own muzzle), salvo error, dispersion, and the RN ladder on over/short spots.
- **The level** is `FireControlLevel` 0-8 (`Rows.cs`), read by `DesignerPlaceholders.FireControl` from the director
  count, the best rangefinder and the computer and radar tonnage, **capped by the look's era**. Its numbers are one row
  of `SimSettings.GunneryLevels` (the reference's `LEVELS`: latency, q, priors, residual bias, laying, salvo-to-salvo
  error, own-turn kick, first ladder step). Local control is row 0. The matchup (`Matchup.cs`) reads the level too
  (`MatchupMpiPct`).
- **Shipgen's fire control** (`Shipgen/FireControl.cs`): per battery (main, secondary, AA) a count of directors, one
  rangefinder base, the hood's armour, radar and computer tonnes. Directors stand on the highest free superstructure roof,
  hood 2.2 m, eye 1.5 m over its floor; the computer is a weight at 0.3 of the depth, not a room. Masts are the funnels'
  top + 6 m (`LayoutParts.MastAboveFunnel`), with no top as a part.
- **The eyes** (3.2a, `Sight`): mastheads (1 m under the top, placeholder), directors with their rangefinder, bridge,
  aft control, conning tower; blind sectors from the parts round them.
- **The bench:** `shipgen duel` (steady target, a level override). No zigzag, circle or chase; no weather.

## What's wrong, against the research

| Gap | Today | The research | Why it matters |
|---|---|---|---|
| **One tracker for every level** | a true-course (cart) filter, helm-free by construction | polar rate clocks drift on a crossing target and lose their rates when own ship turns; true-course plots don't; only post-war filters follow a turn (§5.3-5.6) | Pollen against Dreyer, the AFCT, the Mk 8, circling as evasion: table 11's 2-4x against straight-line machines all vanish |
| **Clock limits** | none | Dreyer ±1,200 yd/min, ±15°/min (§5.3) | close, fast targets beat the early tables |
| **One rangefinder a director** | QE has one 15 ft | QE 5 x 15 ft + 1 x 9 ft, Lion 4 x 9 ft, Bismarck ~15, Iowa 9 (§3.5) | averaging and redundancy: +7-10% hits each, and one hit no longer blinds the ship |
| **Spotting height** | the director's eye: Dreadnought 20.4 m, Bismarck 23.9 m | tops at 28-35 m, towers 40 m (h_spot 35-40 in `LEVELS`) | σ = k·R²/h: at 15 kyd 540 m from 21 m against 320 m from 35 m. Most spots today are noise (4.2b's "settled beyond the plan") |
| **Masts** | funnel + 6 m on every ship, 19.4 m over the deck on a destroyer and on Bismarck | battleship mastheads 45-55 m, destroyers 25-30 m; foretops and foretop directors (Iron Duke, Bismarck's 31 m) | spotting, the horizon, the sighting model's lookouts |
| **The level by tonnage and era** | a paint scheme picks the ceiling | the designer note: tech values, no years; levels become presets (§3.3: tech-built fits reproduce the levels within ~3%) | the shells and materials already left "by period" for "physics as numbers in the design" |
| **Instrument kinds** | coincidence only, magnification from base | eye, stadimeter, coincidence, stereo, radar, laser; effective base and magnification (haze, vibration, mast stiffness) | stereo in haze, coincidence end-on, light pole masts shake |
| **Fire discipline** | a salvo whenever the bearing mounts are loaded | ranging salvos wait for the fall (TOF buzzer), fire for effect on a straddle, half-salvos, two in flight after Jutland (§6, §9); battle rates 30-60% of drill | the time to first hit, and the magazines that empty in two hours (`command.md` 5.1g) |
| **Spotting rules** | the RN ladder for everyone | USN bracket-and-halve never below a pattern, the rocking ladder, the German three-salvo ladder, magnitude spots at half gain with a dead band (§9) | navies differ; radar spots need the magnitude rule |
| **Whose splash** | each battery spots as if alone (decision 9) | 1/(1 + 0.6·N); dyed shells; KGV ranging on Rodney's splashes | concentration's cost; the eyeball ship's advantage goes |
| **Radar, night** | radar is a tonnage that lifts the level | a sensor with an envelope, range-only sets needing optical bearing, splash spotting by range or both (§3.6) | blind fire, North Cape, Surigao; no solution at night without light |
| **Point blank** | the 550 m first step and the eye's 20% close in | inside the danger space laying by sight, a step scaled to the range | level 0 tops out at 60% at 1 km |
| **One target a director** | two batteries may read one director on two targets | one gunnery officer, one target (§5.2) | split fire costs a director (4.0's note) |

## The principles

- **Physics as numbers in the design, never by period** (`docs/shipgen/DECISIONS.md`; the damage plan's first principle).
  The combat model sees only the numbers of `fire-control-designer.md` §3 and §6, never a tech name or a year.
- **Templates like the machinery's and the shells'.** A tech block of physical numbers with a name for a label, written
  to a catalogue by `shipgen templates` from the research's tables; a design carries the blocks it uses; the tech tree
  filters them later. **The levels become presets**: named combinations of blocks that reproduce the reference's
  calibrated ladder, so "level 3" is a template, not a type.
- **Parity first, then change.** Each phase opens by moving the data or the structure without changing a number (hashes
  and goldens hold), then changes behaviour one step at a time, with the benches' numbers before and after.
- **One physical model.** Rangefinders, tops and radars are hitbox parts: the sprites draw them from `Ship.Hitboxes`,
  damage hits them, sighting compiles its eyes from them.
- **Data first, the designer's tab after** (memory: one UI pass after a plan). Steps add data and model; the Fire control
  tab is the last step.
- **Soft real time.** Trackers are fixed-size structs (at most 6 states), stepped in place: no allocation, no interface
  dispatch in the hot path (a tagged struct and a switch). Today's cost is ~10 µs a battery-second; the budget for the
  pass is 2x that.

## The contracts

What moves from the settings' level row onto the class and the design (`fire-control-designer.md` §6):

- **`InstrumentRow`** (`Sim.Ships`, one per sensor part): its component, where it stands and how high, its kind (eye,
  stadimeter, coincidence, stereo, radar, laser), base and magnification, the mounting's stiffness (turret 70, tower 60,
  deck 55, tripod 50, pole 32), readings a minute, bearing σ, its blind sectors (from `Sight`, or its turret's arcs), and
  for radar its envelope (σ = a + b·R, max range on a battleship, min range, range-only, splash spotting none, range or
  both, its reach). Which control it serves: a director, a top, a turret, after control.
- **`ComputerRow`** (per fire-control unit): the tracker (`polar`, `cart`, `ca`, `ct`), helm-free, latency, q, the
  priors, the clock limits, own-turn kick, wander (the computer's share of salvo-to-salvo error), residual bias and
  deflection bias, manoeuvre detection and its filter numbers; which corrections it makes (the Earth's turning: the later
  machines only). Where it is: the transmitting station, a room.
- **`MountTechRow`** (per battery): director firing, remote power control or follow-the-pointer, stabilised (a stable
  vertical: no sea penalty once ship motion is in), laying error.
- **`FcUnit`** (per class: a director or control position with its computer): its instruments, the batteries it can
  control, one target at a time.
- **Propellant** (already the design's, with the shells): the round-to-round muzzle velocity spread, so salvo error is
  assembled as `√((S·mv/√n)² + wander²)` (§3.3) instead of one number by level.
- **Doctrine** (the side's, `DoctrineField`, as the loadout is the admiral's): the spotting rule, ranging and rapid
  fire, half-salvos, salvos in flight while ranging, dyed shells.

## The steps

Each is a session or a few, opens with the usual check of the code, ends with something to run and look at, and
updates this doc and `fighting.md`.

### F0 Parity: the fire-control unit behind the old numbers

- `GunnerySystem` reads a battery's `FcUnit`, `ComputerRow`, `MountTechRow` and instruments, compiled for now from
  today's data through the placeholder (the level row's numbers moved onto the rows). `GunneryLevels` stays only as the
  presets' source.
- **One target a director:** a director and its computer hold one target; a second battery on another target takes the
  next free control (after control, a top), else local control.
- **Checked:** `duel` and the scenarios identical, hashes unchanged (except where one-target-a-director bites, reported).

### F1 The benches first

- `duel` gains target and own policies (steady, zigzag ±amp every period, circle at a rate from a time, chase the last
  splash), weather (visibility, haze, night), `--fit PRESET` and the design's own fit, and prints settle and recovery.
- `shipgen fc-ladder`, `fc-converge`, `fc-history`: the research's tables 5 (settling, recovery from a 45° turn), 6 (the
  ladder by weather), 11 and 12 (zigzag and circles) and §11 (the history cases), the same cases run through
  `fire_control_ref.py` beside them, as 4.2b did.
- **The baseline:** today's model through all of it, recorded here, so every later step has a before.

### F2 The trackers by computer (the prediction logic)

- **`polar`** (Dumaresq, Vickers clock, Dreyer, the Japanese plot): range and range rate, bearing and bearing rate in
  the relative frame, constant rates, clamped at the clock's limits. **Not helm-free:** own ship's turns and speed changes
  show as the target's motion and are re-learned; **helm-free** (Dreyer Mk III on): fed exactly. It drifts on a crossing
  target by geometry, as the machines did.
- **`cart`** (Argo, AFCT, Ford, Mk 1A): today's `Track`, true course and speed on a straight line.
- **`ca`** (1950s analog predictors): the cart plus a decaying (Singer) target acceleration.
- **`ct`** (digital; the AFCT's turn setting if chosen): speed, heading and rate of turn; follows a steady circle.
- **Manoeuvre detection** (`ca`, `ct`): two large surprises in a row open the filter (the Mk 1A's "target just turned").
- **The eye-estimate prior** (§5.2): course and speed by eye seed the track, as the synthetic method did; the picture's
  estimate floors it as today.
- **Anchors:** settled rate error and turn recovery by tracker within the research's bands (Dreyer and plot 2-4 min,
  AFCT and Mk 8 optical 1-2, auto-track 15-30 s, digital 5-15); the circle's standing error (AFCT ~700 yd, Mk 8 with
  radar ~300, digital ~20); Pollen beats Dreyer by about 3x in settled rate. No allocation; the cost a battery-second.

### F3 Shipgen: fire control as tech blocks

- **The design's `fire_control` reworked:** per battery a fit naming tech blocks (`optics`, `computer`, `mount`,
  `radar`) from a design-level table, as `ammunition` and `materials` are. The blocks hold `fire-control-designer.md`
  §2's raw values: optics (δ, rigid base, flex exponent, M_max, stereo, coated, gyro-stabilised, mass per m²), computer
  (as `ComputerRow`, plus its mass, crew and power), mount (as `MountTechRow`), radar (wavelength, peak power, pulse,
  noise figure, bearing method, ranging circuit, auto-track, splash, set mass, antenna mass per m²).
- **The catalogue:** `shipgen templates` writes `Content/Templates/fire-control.json` and
  `docs/shipgen/fire-control-templates.md` from the research's tiers (O1-O4, R1-R5, C0-C6, M0-M4) and its named systems
  (Dumaresq and Vickers clock, Dreyer Mk I-V, Argo, AFCT, Type 92, Ford Mk 8, Mk 1A, Mk 86); the presets are named
  combinations matching `LEVELS`.
- **`shipgen upgrade`** moves the 71 designs onto blocks that reproduce their level today (parity), then a second commit
  gives each its navy's and year's real fit where the research has it.
- `DesignerPlaceholders.FireControl` and `RangefinderMagnification` are deleted; `ShipDesign.Version` bumps; goldens
  moved deliberately, one per commit.

### F4 Shipgen: instruments as parts, masts and tops at their heights

- **Rangefinders where they stood:** in a director, in a turret's hood (seeing only its turret's arc, low and wet), on a
  spotting top or foretop, in after control, on the conning tower; each with base, magnification, kind, mounting and
  armour, a hitbox part the sprite draws. Weights from the designer note §3.4 (instrument `mass_k·B²`, open pedestal 3x,
  hoods as plate boxes, director boxes, the radar's antenna aloft and its set low). **A director must fit the deck it
  stands on** (body ≈ B + 0.6 m wide, §7.1): the real limit on long bases.
- **Masts by kind** (pole, tripod, tower or pagoda, cage, lattice) with a top as a part: the design gives the heights,
  templates the defaults by type and year, within the research's bands (battleship masthead 45-55 m, Dreadnought's top
  ~28-30 m, Bismarck's foretop ~31 m, destroyers 25-30 m). Directors may stand in a top. Topweight counts as t·m
  (the director at 30 m is the cruiser's real cost); the mast's kind is the stiffness `M_vib` reads.
- **The transmitting station** becomes a room below the armour (crew, power, the computer's mass), so damage can find it.
- **Sighting gets the real heights:** the masthead placeholder `MastLookoutUnderTopM` and the funnel + 6 m rule go;
  tops and their instruments are eyes. Sighting reach and the pictures move (reported, hashes moved).

### F5 Sensors proper

- Every instrument a sensor with its own rate, bias and bearing error; the tracker takes all of them, so random error
  falls as 1/√N and each instrument's bias stays until a spot takes it off (§3.5).
- **Effective base and magnification** (designer §3.1): long bases deliver less, magnification limited by the air
  (haze from the weather) and by vibration (mounting stiffness, height); stereo better in haze, coincidence worse end-on
  and at low contrast; dusk and night multiply the error and need the target in sight (3.2's light).
- **Crew:** the side's quality stays the rangetakers' multiplier; the stereo operator pool waits for the navy layer.
- **Anchors:** the designer note's base study (D6: the knee by range, the eye at 10-20% of a 4.6 m set's hits, 15 m
  +20-30% at 20 kyd), its arrangement study (D8: a second instrument +7-10%), and §3.3's per-reading errors.

### F6 Firing and spotting by the navy's rules

- **Fire discipline:** ranging salvos wait for the fall (the TOF buzzer) unless doctrine allows two in flight; fire for
  effect on a straddle at the loading rate; half-salvos alternating; the salvo's ripple time per mount (the record
  already takes one, `muzzle-flashes.md`). Rates of fire in battle come out of this, not a factor.
- **Spotting:** the spotter is the battery's highest working eye that sees the fall (a top now, F4), `σ = k·R²/h` with k
  tuned against NavPers' "500 yd obvious at 12 kyd, barely at 19 kyd from 120 ft" once the heights are real.
- **The rules as doctrine:** the RN bracket (400, 200, 100 yd), the USN bracket-and-halve never below a pattern and the
  rocking ladder after a straddle, the German ladder of partial salvos; magnitude spots (radar, later aircraft) at half
  gain with a dead band of half a pattern.
- **Whose splash:** from the shell system's splashes near the target within a few seconds of one's own, by calibre:
  identification 1/(1 + 0.6·N), dyed shells lower; a salvo of 3 or more is identified (`manual-gunnery-research.md`
  §5). The eyeball ship loses the advantage decision 9 gave it.
- **Point blank:** inside the range where the danger space exceeds the range error, lay by sight; the first ladder step
  scales with the range.
- **Anchors:** time and salvos to first straddle and hit (§10: practice 2-5 salvos, Lion ~10, Bismarck 5); the
  1912 Thunderer-Orion trial (director ~4.5x the hits of independent laying in poor visibility); Santiago and Tsushima
  for the pre-dreadnoughts (`early-hit-rates.md`).

### F7 Damage reaches fire control

The research's hooks (§13.2), on the parts F4 made: a director lost hands the unit to the next control with that
position's height and instruments; the transmitting station lost drops the computer to a hand plot (`polar`, the eye's
prior, a long latency); fire-control circuits cut put the turrets in local control; a top hit lowers the spotter; a
turret's rangefinder lost leaves that turret in local control without a range. E4's timed outages (gyros, rangefinders
out of adjustment, power) map onto the new rows.

### F8 Radar and night

Radar as a sensor (§3.6, designer §3.2): its envelope from the set's physical numbers, capped by the 4/3-earth horizon
and scaled by the target's size to the ¼; range-only sets need an optical bearing, so no solution at night unless the
target is lit; splash spotting by range or both within its reach; fragility (its own guns' blast, its aerial hit). The
first cut of the game (1890-1925) has none, so this step could go later; the data's shape is there from F3.

### F9 The quick measure

The matchup's `MatchupMpiPct` by level goes: each battery gets a profile of its fire-control error by range, worked out
analytically from its own rows (rangefinders, computer, salvo error) and checked against `duel`, so the matchup now and
coarse fire later (4.9) read the design's fire control, not a level.

### F10 Calibration

The whole chain against the record (§11, `dispersion-and-hits.md` §5): Tsushima, Jutland by squadron, Dogger Bank,
Denmark Strait, the 1932 LRBP, the 1944 study, North Cape, Guadalcanal, Komandorski. Kept tests with wide bands, the
ladder's order, local control at 60-70% of director fire by day. The record's combat factor (1.5) stays one number;
tuning for gameplay waits for the balance pass, as the damage plan's did.

### F11 The designer's Fire control tab

Mockup, then port, as the other tabs (`docs/designer-ui/`). The fit per battery from the catalogue, instruments
placed on the plan and profile, mast and top heights, and the sizing rule as a live readout ("unit of error at 15 kyd 62
m, your salvo's spread 73 m: adequate", designer §5) with the curve for the ship's own guns.

## What stays out

- **Ship motion** (`ship-motion-gunnery-research.md` §7.7: the roll's phase, firing on the roll, wetness taking out
  forward mounts and turret rangefinders, cross-level, vibration, the sea): its own 4.5 deepening, best taken right after
  this one. This pass gives it the instruments' heights and kinds and the mounts' `stabilised`.
- **Smoke** (the smoke field), **searchlights and star shell** (the night pass), **aircraft spotting** (no aircraft),
  **AA fire control**.
- **Evasion as captains' behaviour** (chasing salvos, the Isaacs mixed strategy: `evasion-research.md`): the bench's
  policies here, the captains' in the evasion deepening.
- **Wind and weather in the shells' flight,** and so computed corrections for them: the residual bias stays the
  computer's number.
- **Choosing the shell by target** (the AI's pass), **coarse fire** (4.9, after all the fine passes).

## Questions for the user

1. **Fire control as tech blocks, levels as presets** (F3), as the shells and materials went? Recommended: yes; the
   enum survives only as a preset's label.
2. **Radar in this pass (F8) or later?** The first cut has no radar. Recommended: the data's shape in F3, the model as
   the plan's last model step, cheap from the reference, so the later cuts don't need a second pass.
3. **Spotting rules and fire discipline as the side's doctrine** (the admiral's, like the loadout), not the design's?
   Recommended: doctrine.
4. **Fire discipline changes rates of fire** (F6): fewer shells, later first hits for slow systems, magazines that last.
   It moves the matchup and every scenario's numbers. Wanted?
5. **The AFCT's turn setting** is unconfirmed in the sources (§14). Recommended: a computer option anyone may fit (allow
   silliness), off in the RN templates by default.
6. **Masts and tops from the design** (F4) with template defaults by type and year, costed in topweight and stiffness,
   rather than a rule from the ship's size? Recommended: from the design.
7. **Order:** F0-F2 first (the prediction logic on presets, no shipgen change), then F3-F4 (shipgen), F5-F7, then F8-F11.
   Or shipgen first?
