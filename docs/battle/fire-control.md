# Plan: fire control (a 4.5 deepening)
Status: **agreed with the user 2026-10-17** ("Decided" below; radar moved out); F0-F6 done 2026-10-18 (F6e moved to F10)    Updated: 2026-10-18

The fine-tune pass for the **Fire control** link of `fighting.md` 4.5 ("the trackers by era ... the clocks' limits ...
the full set of levels, radar as a sensor, night ... splash confusion ... the designer emitting levels and sensors,
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
  error, own-turn kick, first ladder step; since F0 `FireControlPresets` and the battery's `Fit`). Local control is row 0. The matchup (`Matchup.cs`) reads the level too
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
| **Fire discipline** | a salvo whenever the bearing mounts are loaded, so two or three salvos fly at the same uncorrected aim before the first spot | ranging salvos wait for the fall (TOF buzzer) or go as a deliberate ladder, fire for effect on a straddle, half-salvos, two in flight after Jutland (§6, §9); battle rates 30-60% of drill | the time to first hit, shells wasted while ranging, and the magazines that empty in two hours (`command.md` 5.1g) |
| **The bracket** | the ladder keeps only the last sign seen | a bracket found and split, never below a pattern; rate spots with range spots (`computing.md` §2.5) | spots out of order or from a ladder; the rate error a hand plot carries |
| **What the AI knows of its fire** | the matchup's fixed MPI table by level | — (the game's need) | captains can't tell whether their fire is working, or what a range change would buy |
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
  calibrated levels, so "level 3" is a template, not a type.
- **History is the yardstick, not today's model** (the user, 2026-10-17). Hit rates are expected to move, against
  manoeuvring targets a long way: that's the point of the pass. Each step is judged by the historical record below,
  never by how close it stays to the current implementation or to `fire_control_ref.py`. The reference code is a check
  on mechanisms (does a rate clock drift on a crossing target, does a straight-line machine lag a circle), not a target
  for the numbers.
- **Refactors prove themselves, models don't.** A step that only moves structure or data (F0, F3's format move) shows
  identical numbers and hashes, so it's known to be a refactor; a step that changes a model reports its numbers before
  and after against the record, and the "before" is only where the old model stood, never a bound.
- **One physical model.** Rangefinders, tops and radars are hitbox parts: the sprites draw them from `Ship.Hitboxes`,
  damage hits them, sighting compiles its eyes from them.
- **Data first, the designer's tab after** (memory: one UI pass after a plan). Steps add data and model; the Fire control
  tab is the last step.
- **Soft real time.** Trackers are fixed-size structs (at most 6 states), stepped in place: no allocation, no interface
  dispatch in the hot path (a tagged struct and a switch). Today's cost is ~10 µs a battery-second; the budget for the
  pass is 2x that.

## The yardstick: the historical record

The estimates every bench and test is held to, from `dispersion-and-hits.md` §5-6, `early-hit-rates.md`,
`evasion-history.md` and `emergency-eyeball.md`. Counts are poor and often lower bounds, so the bands are wide (×1.5-2)
and the ordering between cases matters as much as any one figure. **The first game's era (1890-1925) leads**; the later
cases stay as secondary checks so the later cuts don't start from nothing.

| Case | Conditions | Record | What it checks |
|---|---|---|---|
| Santiago 1898 | stadimeters, brown powder smoke, a chase at 1-6 kyd | 1.3% all calibres, 3.5% major calibre | the eyeball and early sights |
| Yellow Sea 1904, Japanese 12" | 1.5 m Barr & Stroud, main fire ~5.6 km | 4.7% (all calibres both sides ~1.7%) | early rangefinders |
| Tsushima 1905, Japanese 12" | 1.5 m rangefinders, ≤6.5 kyd, mist | ~9% (6" ~2%) | the same, closer; heavy over light guns |
| Orion and Thunderer 1912 | 9 kyd, heavy sea; independent against director | 15% against 67%; about even in clear weather | director firing |
| Dogger Bank 1915 | 17-20 kyd, own smoke (the Germans), chase | British 1.4%, German ~2.5%; Lion's first hit after ~10 salvos | WWI long range, the time to first hit |
| Jutland 1916 by squadron | haze, smoke, 9-19.5 kyd, both sides manoeuvring | British BCS 1.4%, 5th BS 2.6%, Grand Fleet BS 3.6% (9-12 kyd), 3rd BCS 4.3% (short); German 1st SG 4.0%, battleships 3.0% | the Dreyer era in battle; German above British |
| Falklands 1914 | long, closing, smoke, a long chase | ~7% (counts uncertain) | a stern chase against cruisers |
| Practice against battle | USN 1912-18 practice H = 100/[1 + 0.0007(R - 2000)] (~26% at 6 kyd, 19% at 8 kyd); battle 0.1-0.3 of it before 1910, 0.3-0.5 in WWII | | the combat factor |
| Steady or crippled target | Bismarck's end, 20 → 3 kyd | ~11-12% (Rodney, KGV) | what spotting buys on a target that can't evade |
| An evading target | Komandorski (salvo chasing, 21-14 kyd), Java Sea | 0.4-0.5%; ~200 salvos within 50 yd of Salt Lake City, 4-5 hits | **manoeuvre: 3-10x fewer hits against an energetic evader at long range** (`evasion-history.md` §5) |
| WWII optical and radar | USN 1944 study (practice), LRBP 1932-33, North Cape, Washington at night | 32.7 / 10.5 / 2.7% at 10 / 20 / 30 kyd; 4.4% at ~30 kyd; ≥3%; 12-27% at 8.4 kyd | the later cuts |
| Light guns at fast boats | 5" against a 30-40 kn boat | 0.5-2% a round | small, agile targets |
| Time to first hit | practice | hits from salvo 2-5 (Tennessee salvo 4, 3 min 42 s at 19 kyd) | fire discipline (F6) |

**A record holds only where it was made** (the user, 2026-10-17). Each case is matched at its recorded range,
weather, target and behaviour, and nowhere else: a 1.4% at 16-18 kyd says nothing about 8 kyd. Away from the recorded
distances the hit rate is whatever the model gives, and that's expected to climb steeply as the range closes (the danger
space widens, every error shrinks with the range, rangefinding with its square) and fall further out. What's checked
between and beyond the cases is only the shape: hits rise as the range falls, and the trend follows the USN practice
fit's form, not its level. The bench runs each case at its own range; a sweep in range is for looking, never a band.

**What this predicts against today's model** (to be measured in F1, not assumed): every level today has a
true-course tracker that never loses its rates to own ship's turns, so it's too good against manoeuvring targets and
in own-ship turns, worst for the plot-and-clock era; and firing at every reload spends shells before the bracket is
found. Expect hit rates against zigzagging and circling targets to fall well below today's, toward the evasion cases'
3-10x, and the early levels to fall furthest.

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

## Decided (the user, 2026-10-17)

1. **Fire control as tech blocks, levels as presets** (F3), as the shells and materials went; the enum survives only as
   a preset's label.
2. **Radar later** (F9 moved out): the first game is set in 1890-1925. F3's fits take a radar block when it comes.
3. **Spotting rules and fire discipline are the side's doctrine** (the admiral's, like the loadout), not the design's.
4. **Fire discipline changes rates of fire** (F6): "definitely". Fewer shells while ranging, fire for effect at the
   loading rate once straddling, magazines that last; the matchup's and the scenarios' numbers move, reported.
5. **The AFCT's turn setting** is a computer option anyone may fit (allow silliness), off in the RN templates by default.
6. **Masts and tops come from the design** (F4), with template defaults by type and year, costed in topweight and
   stiffness.
7. **The order:** F0-F2 (the prediction logic on presets, no shipgen change), then F3-F4 (shipgen), F5-F8, then F10-F12.
8. **The hit-chance floor is doctrine** (F6 item 7): a battery holds fire by default while its estimated hits per shell
   are under it; "fire anyway" is an order.
9. **F10's scope** as written: the estimate (now and what if) and "under effective fire" as facts the AI reads; only F6's
   floor uses them in this pass, the tactics in the AI's passes.
10. **Seeing further** (F4): with the masts and tops right, sighting takes the new heights, so taller ships see and are
    seen further; its anchors re-run on them.
11. **History is the yardstick** (the principles, "The yardstick"): hit rates are judged against the historical
    record, not against today's model or the reference code; a large departure, above all against manoeuvring targets,
    is expected and wanted.

## The steps

Each is a session or a few, opens with the usual check of the code, ends with something to run and look at, and
updates this doc and `fighting.md`.

### F0 The fire-control unit (a refactor)

- `GunnerySystem` reads a battery's `FcUnit`, `ComputerRow`, `MountTechRow` and instruments, compiled for now from
  today's data through the placeholder (the level row's numbers moved onto the rows). `GunneryLevels` stays only as the
  presets' source.
- **One target a director:** a director and its computer hold one target; a second battery on another target takes the
  next free control (after control, a top), else local control.
- **Checked:** `duel` and the scenarios identical, hashes unchanged, which proves the refactor (one target a director is
  a model change: its effect reported on its own, against the record).
- **Done 2026-10-18.** What it came to:
  - **The rows** (`Rows.cs`): `OpticsRow` (magnification, readings a minute), `ComputerRow` (latency, q, the priors,
    the residual biases, the own-turn kick, the first ladder step, the salvo-to-salvo error), `MountTechRow` (director
    firing, laying error), together a battery's `FireControlFit` (`BatteryRow.Fit`, beside `Level`, now the preset's
    label); `FcUnitRow` (one a director for now, so a unit's index is its director's) with its `InstrumentRow`s (the
    director's eyes: kind, base, height), and `BatteryRow.Units`. `shipgen weapons` prints each battery's units and fit.
  - **Where the presets went:** the class compile carries no settings, so `SimSettings.GunneryLevels` (and the
    settings file's rows) moved to `FireControlPresets` in `Sim.Ships`, the same numbers, a fit a level; F3 turns them
    into the catalogue's named combinations. The optics' make (magnification, rate) is the battery's fit rather than
    the instrument's, since a director serving two batteries could carry two presets; F3's per-battery fit names its
    optics block anyway, and the instrument keeps what is its own (kind, base, height).
  - **The proof:** duels at every level, with damage, a green crew on a crossing target, Bismarck's secondaries,
    Mikasa's local control and a destroyer, and the squadrons, flotilla and tsushima scenarios' hashes, all identical
    before and after.
  - **A fix found on the way,** committed alone: a director back from an E4 outage timed its first readings at the
    eye's rate (the fit before the switch). Only squadrons moved (it diverges from the first changed reading).
  - **One target a director** (`GunnerySystem.AddSensors`): a battery passes over a unit that a battery of its ship on
    another target holds; with none left it falls to local control, and it takes one again when it's let go (cease,
    a new target, damage). Batteries on the same target share. Where it bites today is batteries of two guns sharing
    directors: Bismarck's 150 mm and 105 mm on a side share one, and Borodino's 12" and 6" turrets are both main
    batteries on the main directors. Only tsushima moved, a few salvos and one hit of 8,023 shells; squadrons and
    flotilla unchanged. Against the record it
    can't be read yet (F1's benches); what it buys is split fire costing a director, as 4.0 noted. The test:
    `GunneryTests.A_director_holds_one_target`.
  - **The cost:** Jutland's two hours, gunnery 20 µs a call, 2.7% of the run.

### F1 The benches first

- `duel` gains target and own policies (steady, zigzag ±amp every period, circle at a rate from a time, chase the last
  splash), weather (visibility, haze, night), `--fit PRESET` and the design's own fit, and prints settle and recovery.
- `shipgen fc-history`: the yardstick's cases as scenarios of their own (the guns, the fit, the range, the weather, the
  target's behaviour as the record describes it: steady, zigzagging, turning away, chasing salvos), each at its recorded
  range, printed beside the record's band. **This is the bench every later step answers to.**
- `shipgen fc-converge`, `fc-levels`: the mechanisms alone (the research's tables 5, 6, 11 and 12: settling, recovery
  from a 45° turn, zigzag and circles), with `fire_control_ref.py` run on the same cases beside them, to check each
  tracker fails where its machine failed. Its hit rates are not targets.
- **Where today's model stands:** all of it run once and recorded here against the record, so the size of the
  departure each step makes is visible. Not a bound.
- **The kept tests** that pin today's numbers (`GunneryTests`: level 3 at 2.5-8%, the pairs within ×2 of the reference)
  are rewritten to the yardstick's bands as the steps that change them land.
- **Done 2026-10-18.** What it came to:
  - **The engine** (`Shipgen.Cli/FcBench.cs`): an `FcCase` (shooter, target, battery, fit, range, bearing, course, speeds,
    a `Policy` each, visibility, start time, combat factor, minutes, crew) run on a fresh world a second at a time, the
    ships steered between the seconds as `engage()` steers them: steady, `zigzag:AMP:PERIOD`, `circle:RATE:FROM` (the
    rudder walked to hold the rate; the circles make good 0.50 and 1.00°/s), `chase:AMP:PERIOD` (steer for the last
    splash within 600 m), `turn:DEG:AT`, crippled. `FcStats` sums the runs: hits, straddles, the first hit by salvo and
    after the first salvo, the track's aim error as it settles (2-4, 4-8, 8+ min) and after the target's manoeuvres
    (0-1, 1-2, 2-4 min), the salvos' mean fall in range after 5 min, the speeds made good. Runs go on threads.
    The one sim change is a reader, `GunnerySystem.TrackAt` (where a battery's track has its target at a time).
  - **`shipgen duel`** is that engine with `--target-policy`, `--own-policy`, `--visibility KM`, `--night` (a December
    night) or `--at ISO`, `--combat X` (1 for practice) and `--fit design|0-8|NAME` (`--level` kept). A steady duel gives
    the old duel's numbers exactly (3.1%, 48 of 1,560). Its aim error is against the target's course and speed, so it
    understates a circling target's; `fc-converge` takes where the target went.
  - **`shipgen fc-converge`** (tables 5 and 12), **`fc-levels`** (tables 6 and 11), **`fc-history`** (the yardstick) with
    `--runs N`; `--ref` runs the reference's same table after (`research/simulation/fc_tables.py`, python with numpy).
    About 10, 13 and 18 s.
  - **Departures from the research's cases:** the levels' table's long-range column is at 22 kyd, since Queen Elizabeth's 20°
    mounts don't reach the reference's 25 kyd (its 15" has 30°); the converge target is a battlecruiser, the only
    25 kn ship of the size; the sim's ship turns as a ship does, the reference's at once. Haze is the visibility only,
    and smoke, splash confusion, the sea and low contrast aren't in the sim yet: each history case names what it misses.

**Where today's model stands** (2026-10-18; not a bound: the size of each later step's departure is read against it).

*Against the record* (`fc-history --runs 24`): 11 of 23 cases in their bands.

| Case | Range | Model | Band | Record |
|---|---|---|---|---|
| Santiago 1898, US 12" (preset 0) | 2.6 km | 14.5% | 1.8-7 | 3.5% |
| Yellow Sea 1904, Japanese 12" | 5.6 km | 10.6% | 2.5-9 | 4.7% |
| Tsushima 1905, Japanese 12" | 4.6 km | 10.8% | 5-15 | ~9% |
| Tsushima 1905, Japanese 6" | 4.6 km | 4.0% | 1-4 | ~2% |
| 1912 trial, director / independent (practice) | 8.2 km | 26.4 / 16.9% | | about even in clear weather |
| Dogger Bank, British BCs | 16.5 km | 2.7%, first hit salvo 10 | 0.7-2.8 | 1.4%, ~10 salvos |
| Dogger Bank, German BCs | 16.5 km | 2.1% | 1.2-5 | ~2.5% |
| Jutland, British 1st/2nd BCS | 15 km | 3.1% | 0.7-2.8 | 1.4% |
| Jutland, 5th BS | 17.5 km | 2.8% | 1.3-5.2 | 2.6% |
| Jutland, Grand Fleet BS | 10.5 km | 7.6% | 1.8-7.2 | 3.6% |
| Jutland, 3rd BCS | 8.2 km | 16.6% | 2.2-8.6 | 4.3% |
| Jutland, German 1st SG | 15 km | 3.8% | 2-8 | 4.0% |
| Jutland, German battleships | 11 km | 6.0% | 1.5-6 | 3.0% |
| Falklands, Invincible | 13.7 km | 3.2% | 3.5-14 | ~7% |
| USN practice 6 / 10 kyd | 5.5 / 9.1 km | 54.1 / 22.8% | 17-39 / 10-23 | 26 / 15% |
| Tennessee-like practice, 19 kyd | 17.4 km | first hit salvo 2 | salvo 2-5 | salvo 4 |
| Bismarck's end, Rodney | 14 km | 40.8% | 6-20 | ~11% |
| Denmark Strait, Bismarck | 20 km | 16.3% | 2.7-11 | ~5.5% |
| Komandorski, on a salvo chaser / steady control | 18.3 km | 0.4 / 1.8% | 0.3-1 | ~0.5% |
| LRBP 1932-33 (practice) | 27.4 km | 2.0% | 2.9-6.6 | 4.4% |
| USN 1944 study 10 / 20 / 30 kyd (practice) | | 47.0 / 7.9 / 2.2% | in all three | 32.7 / 10.5 / 2.7% |
| Light guns at a fast boat | 3 km | 0.5% | 0.5-2 | 0.5-2% |

Read: **too good close in and against steady targets** (Santiago 4x, the 3rd BCS 4x, Rodney 4x, practice at 6 kyd 2x),
where the record's losses are the smoke, the sea, splash confusion and the target's own manoeuvres that the sim doesn't
have yet; **about right at long range in battle** (Dogger Bank, the 5th BS, the German 1st SG), the Dreyer era's
numbers landing near its record largely because they're dominated by the rangefinder and spotting errors at range;
**too low on the long stern chase** (Falklands) and at practice's longest range (LRBP). The orderings: heavy over light
guns (2.7x) in; the Grand Fleet's short range over the BCs' long range (2.4x) in; a steady target over a salvo chaser
at long range (4.2x) in, the cart tracker already losing on its own; practice's fall with the range (2.4x) in. Out:
**Tsushima over the Yellow Sea** (1.0x, want 1.2-4: same machine, nearly the same range), **the Germans over the
British** (1.2x, want 1.5-5: the two sides are the same machine) and **director about even with independent in clear
weather** (1.6x, want 0.7-1.5).

*Against the reference* (the mechanisms; its hit rates are not targets):

- **Table 5, settling and a 45° turn:** the early presets don't drift. The rate held at 240 s is 1.8 kn off for presets
  1-3 against the reference's polar trackers' 5.8-9.9 kn (F2). The eyeball's miss is 385-1,044 yd against the
  reference's 2,800-3,400: the sim's track starts from the navy's picture, so a battery ranging by eye at 20% inherits
  the picture's fixes, which hardly move under readings that poor (a finding for F2's eye prior and F5). The later
  presets floor at 214-240 yd against the reference's 18-121: one 15 ft rangefinder and its bias (1.5 units, ~160 yd at
  15 kyd) at every preset, where the reference gives each level its instruments (turret rangefinders, radar, laser):
  F5's. After the turn the sim's +30 s is smaller (the ship is still turning) and its +120 s larger (the track lags a
  turn that took a minute).
- **Table 12, a circle at 1°/s:** preset 3 matches the reference (1,022 against 1,103 yd at +120 s); the later presets
  keep a standing error (538-882 yd at +120 s) where the reference's turn and acceleration filters (4t, 7, 8) follow
  it down to 19-117 yd: F2's `ct` and `ca`.
- **Table 6, the levels:** day 16 kyd zigzag 0.8, 2.1, 2.7, 3.4, 4.2, 5.6, 6.0, 10.1, 9.9% for presets 0-8 against the
  reference's 0.8, 2.9, 4.9, 7.0, 8.7, 12.5, 10.9, 17.0, 23.3%: about half from preset 2 up (the single biased
  rangefinder again); haze 13 kyd in the same order. Night: no solution for every preset in both (the reference's radar
  levels 6-8 do; radar is F9's).
- **Table 11, target motion:** preset 3 steady, zigzag, circles 4.0, 3.0, 1.0, 1.2% against 7.7, 6.8, 2.6, 1.2%; every
  sim preset loses about two-thirds of its hits to a circle, as a straight-line machine should, where the reference's
  4t and 8 keep theirs.

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
- **Anchors:** turn recovery by tracker within the research's sourced bands (`computing.md` §4, §8: Dreyer and the
  Japanese plot 2-4 min, AFCT and Mk 8 optical 1-2, auto-track 15-30 s, digital 5-15); the record's own-turn cases (USN
  1927-28: 20-30° turns at ~25 kyd put the aim ~400 yd off and nothing hit; California 1941: 212 yd of deflection after
  a 150° turn); against a manoeuvring target the hit rate falls toward the yardstick's evasion cases. As mechanism checks
  against the reference (not targets): a rate clock drifts on a crossing target and a true-course plot doesn't; a
  straight-line machine keeps a standing error on a circle and a turn filter doesn't. No allocation; the cost a
  battery-second.
- **Done 2026-10-18.** What it came to:
  - **`FcTracker`** (`Sim.Combat`): one fixed-size struct for every `TrackerKind` (state and covariance in inline
    arrays, a switch on the kind, nothing allocated), fed `FcReading`s (the time it was taken and arrives, the
    instrument's place, own ship's velocity then, range and bearing with their errors, the same as a point) and asked
    one question, where the target will be at a time. The kinds: **`Judgement`**, the user's addition (2026-10-18: "a
    sailor sighting by eye and thinking about how to lead is also a fire control computer, just a really bad one"):
    a position from the sightings weighed as a man would (the bearing he lays on, the range he guesses), carried on
    along a course and speed guessed from the target's look, never learnt, guessed again every minute; **`RateClock`**
    (two two-state filters, range and true bearing from own ship, constant rates clamped at the clock's limits);
    **`TrueCourse`** (today's `Track`); **`Acceleration`** (a Singer acceleration, 6 states) and **`TurnRate`** (a
    coordinated turn, 5 states), both in a plane tangent at the first reading, with the "target just turned"
    detection. The words were picked to say one thing each: "ladder" is now only the spotting ladder (`fc-ladder`
    became `fc-levels`).
  - **`ComputerRow`** gains the tracker, helm-free, the clock's limits (±18.3 m/s on presets 2-3, ±0.25°/s on 3),
    the acceleration's memory and size, the turn filter's turn-rate and speed noise, the detection, and how often a
    judgement looks again. **The presets** follow the reference: 0-2 plain rate clocks, 3 the helm-free Dreyer, 4-6
    true course, 7 acceleration, 8 turn rate; then, beside the levels and for benches and fits by name (`--fit
    judgement`), **9 judgement**, **10 argo**, **11 table-turn** (the AFCT's turn setting, decision 5) and **12
    japanese-plot**. No design and no local control uses 9-12; local control is still preset 0.
  - **The eye's guess starts every track** (§5.2, the synthetic method): a battery's computer begins at its own first
    reading, with the target's course and speed by eye (the computer's priors), and no longer from the navy's picture.
    F1's finding goes with it: the eyeball's miss in table 5 is now 2,600-3,600 yd against the reference's 2,800-3,400
    (385-1,044 before).
  - **A helm-free clock with latency:** taking own ship's turn off the rates as it happened wasn't enough, since
    readings taken before the turn, still on their way, pulled the rates back. Its rates are now from a reference ship
    (own ship at its last reading, carried on straight); at each reading its prediction is taken from where she
    really is and its rates re-referenced to her velocity then. A plain clock's rates are from the ship as she goes.
  - **A departure from the reference, on physics:** the sim's ships lose way in a hard turn (a fifth of their speed in
    a minute), the reference's keep it, and a turn filter whose speed hardly moves (the reference's 0.05² m²/s³) came
    apart on a slowing circle, worse than a straight-line machine (484 and 810 m against 305 on clean readings). Its
    speed noise is 0.1 m²/s³ (`SpeedQ`); it follows again (36 m), and costs a little on a straight course, as the
    research says the turn models did.
  - **The mechanisms, on clean readings** (`FcTrackerTests`): on a target crossing at 45 kn relative, a rate clock
    holds the motion 4.3 kn off against a true course's 0.9 (the reference: 5.8 against 1.8); on a 1°/s circle the true
    course lags 150 m, the turn filters 14-20 m, the acceleration filter 85 m; own ship turning 45° adds ~70 m (in
    quadrature) to a plain clock's miss and nothing to a helm-free one's.
  - **Benches** (`fc-converge --runs 60`, `fc-levels --runs 20`, `fc-history --runs 24`):
    - *Table 5:* rate error at 240 s, presets 1-3 and the Japanese plot 6.2-7.8 kn against the true-course machines'
      1.5-1.8 (the reference: 5.8-9.9 against 1.5-1.8). The misses settle at 330-430 yd for the clocks, 190-210 for the
      rest: the one 15 ft rangefinder's bias is still every computer's floor (F5).
    - *Recovery after the target's 45° turn,* against the research's bands: Argo 186 s (1.5-2.5 min, in), AFCT 282 s
      (1-2 min), Mk 8 203 s (1-2 min), auto-track 280 s (15-30 s), digital not back within the test's 1.25x (5-15 s);
      the clocks never leave their own drift. All slower than the research, and all read against that ~200 yd floor:
      the metric needs F5's better instruments before it says much.
    - *Own ship turning 45°* (half the runs each way: toward the target a plain clock's stale rates cancel the drift it
      already has, away they add): after it, the plain clocks 370-496 yd against 330-390 before, the helm-free Dreyer
      369-388 against 329-361, the true-course machines +30-100 yd as the range opens.
    - *Table 12, a circle at 1°/s:* at +120 s the digital 244 yd and the auto-track 257, the AFCT's turn setting 546,
      the straight-line machines 589-788 (the reference: 23-107 for the turn models, 380-1,103 for the others).
    - *Table 11, hits:* the straight-line machines lose about two-thirds of their hits to a circle (Dreyer 3.7 to
      0.7%, AFCT 6.2 to 1.9%), the digital keeps most (9.7 to 7.7%), the AFCT's turn setting half (6.0 to 3.0%).
    - *Table 6:* day 16 kyd zigzag judgement 0.6%, eyeball 0.6, rangefinder 2.1, clock 2.6, Dreyer 3.9, Argo 4.2,
      AFCT 4.9, Mk 8 6.0, auto-track 8.6, digital 10.9 (the reference: -, 0.8, 2.9, 4.9, 7.0, 8.4, 8.7, 12.5, 17.0,
      23.3): the order kept, about half the reference from the clock up (the instruments again).
    - *The record:* 10 of 23 in band (11 at F1). Most cases are steady or gently zigzagging targets on near-parallel
      courses, where a rate clock hardly drifts; the Tsushima 6" came into its band, Dogger Bank's British went just
      over (2.8%, the band's top), and the light guns at a fast boat fell under (0.2%: that destroyer's own fit is now a
      rate clock). The orderings as at F1.
  - **Not yet anchored:** the record's own-turn cases (USN 1927-28, California 1941) are of salvos fired through a turn,
    with the own-turn kick and the firing rules; they wait for F6's phases.
  - **A kept test moved:** `FlotillaAttackTests` took the first of twelve seeds whose attack came off and asked that
    two boats fire on the signal; with the new gunnery that seed's signal came late and one boat fired inside two
    minutes. It now holds over every attack seen through: each in phase order, two-thirds or more with two boats on the
    signal (5 of 6 today). `GunneryTests` pass unchanged.
  - **The cost:** Jutland's two hours, gunnery 2.2% of the sim thread, 16.7 µs a call (2.7% and 20 µs at F1); a single
    battery ~24 µs a game second whatever its computer; no allocation (`Fire_control_allocates_nothing`).

### F3 Shipgen: fire control as tech blocks

- **The design's `fire_control` reworked:** per battery a fit naming tech blocks (`optics`, `computer`, `mount`)
  from a design-level table, as `ammunition` and `materials` are. The blocks hold `fire-control-designer.md`
  §2's raw values: optics (δ, rigid base, flex exponent, M_max, stereo, coated, gyro-stabilised, mass per m²), computer
  (as `ComputerRow`, plus its mass, crew and power), mount (as `MountTechRow`). A fit is a list of named blocks, so a
  `radar` block (wavelength, peak power, pulse, noise figure, bearing method, ranging circuit, auto-track, splash, set
  mass, antenna mass per m²) slots in with the radar pass without a migration (decision 2).
- **Today's `radar_t`** stays a weight aloft, as `search_radar_t` is, and stops lifting the level: the WWII designs'
  fire control comes from their computer and optics blocks until the radar pass (Bismarck's and the other 1940s
  designs' numbers move, reported).
- **The catalogue:** `shipgen templates` writes `Content/Templates/fire-control.json` and
  `docs/shipgen/fire-control-templates.md` from the research's tiers (O1-O4, R1-R5, C0-C6, M0-M4) and its named systems
  (Dumaresq and Vickers clock, Dreyer Mk I-V, Argo, AFCT, Type 92, Ford Mk 8, Mk 1A, Mk 86); the presets are named
  combinations standing for the reference's levels, as labels, their numbers whatever the record calibrates (F11).
- **`shipgen upgrade`** moves the 71 designs onto blocks: first the same fit in the new format (a format move, proved by
  identical numbers), then a second commit gives each its navy's and year's real fit where the research has it.
- `DesignerPlaceholders.FireControl` and `RangefinderMagnification` are deleted; `ShipDesign.Version` bumps; goldens
  moved deliberately, one per commit.
- **Done 2026-10-18.** What it came to:
  - **The catalogue** (`Shipgen/FireControlTemplates.cs`; `shipgen templates` writes `Content/Templates/fire-control.json`
    and `docs/shipgen/fire-control-templates.md`): optics (by eye, the B&S FA (O1) and FQ/FT (O2), the FQ/FT on the Argo
    mounting, the WWI Zeiss stereo, O3 and its ×23 and ×25 eyepieces, O4, and two radar stand-ins), computers (judgement,
    the eye and hand plot, the hand plot, Dumaresq and Vickers clock, Dreyer Mk I-II and Mk III-V, the German clock and
    plot and the German range clock, the Argo, the AFCT, its turn setting and the AFCC, the German Rechenstelle, the
    Type 92, the Ford Mk 8 and its radar stand-in, the Mk 1A, the digital turn filter) and mounts (open, early
    telescopic and telescopic sights, the director following the pointer, without RPC, with power drive, the stable
    vertical with RPC and with a servo, the digital servo). The presets 0-12 are named fits of one of each, the same
    numbers as before (a refactor: identical duels and hashes). The salvo's deflection wander moved onto the mount
    block, where the research has it.
  - **Design format 6:** `fire_control.BATTERY.fit` names the blocks, `fire_control_tech` holds the design's copies (a
    name it doesn't carry takes the catalogue's); a battery with directors and no fit fires at `rangefinder`; shipgen
    validates both. The format step carries the old placeholder's rule (tonnage, radar, the era's cap), so every older
    design got the blocks of the preset its level was: identical fights (the world hash moved only with the shipgen
    version and the designs' hashes). The level is a label now (`FireControlPresets.LevelOf`, by the computer), read
    by the matchup until F10 and the displays.
  - **Rangefinding reads the optics' make:** the unit of error takes the block's acuity, rigid base and flex exponent
    (two settings went), and a director's eye sights and fixes through its battery's optics at their magnification,
    by the same law (`FireControl.RangefinderUnitM`): the base placeholder went.
  - **Real fits where the research names the machine** (`shipgen fit`, a commit of its own): the German WWI capital
    ships Zeiss stereo with their clock and plot, the cruisers Zeiss with the range clock and no director; Bismarck and
    Deutschland the 1930s Rechenstelle with O3 stereo at ×23 and power drive; Yamato and Nagato the Type 92 plot; KGV
    the AFCT and the AFCC for her secondaries. Everything else keeps the preset its level was, so radar no longer
    lifts anything, and a Dreyer on a 1916 French or American ship is a preset's label, not a claim. The aim isn't to
    reproduce classes 1:1 (the user, 2026-10-18: a general system with historical inspiration, the mechanisms to
    produce them if wished; the fits get more generic later, sizes the player's): the machines the catalogue lacks and
    the typical fits by type and period are a research request (`research/REQUESTS.md`).
  - **Departures from the text above, for the steps that own them:** the computer's mass, crew and power are in its
    block, but shipgen still weighs `computer_t`: the computer's weight goes with the transmitting station as a room
    (F4), so no ship's displacement moved here. The salvo error is still one number (`salvo_pct`); splitting off the
    propellant's share needs each charge's muzzle-velocity spread as data, left for F11. The stadimeter isn't an
    instrument kind yet (the Russians keep the FA, as Oslyabya had): F5, with stereo's haze and contrast gains.
  - **Against the record** (`fc-history --runs 24`): 10 of 23 in band before, 11 with the optics' make (the Grand
    Fleet BS comes in; Tsushima's 12" 9.2 to 8.7% from the FA's 14″), 12 with the real fits (Denmark Strait comes in:
    Bismarck 16.6 to 10.3% on the Rechenstelle instead of a Ford rangekeeper). The German fits hardly move: Zeiss 10″
    at ×23 gives the FQ/FT's 12″ at ×28's unit of error, so the Germans over the British stays at 1.15 (want 1.5-5)
    until F5 gives stereo its own. Squadrons played over 5 seeds: hits 45/83 before, 41/65 after, losses within the
    spread.

### F4 Shipgen: instruments as parts, masts and tops at their heights

- **Rangefinders where they stood:** in a director, in a turret's hood (seeing only its turret's arc, low and wet), on a
  spotting top or foretop, in after control, on the conning tower; each with base, magnification, kind, mounting and
  armour, a hitbox part the sprite draws. Weights from the designer note §3.4 (instrument `mass_k·B²`, open pedestal 3x,
  hoods as plate boxes, director boxes). **A director must fit the deck it
  stands on** (body ≈ B + 0.6 m wide, §7.1): the real limit on long bases.
- **Masts by kind** (pole, tripod, tower or pagoda, cage, lattice) with a top as a part: the design gives the heights,
  templates the defaults by type and year, within the research's bands (battleship masthead 45-55 m, Dreadnought's top
  ~28-30 m, Bismarck's foretop ~31 m, destroyers 25-30 m). Directors may stand in a top. Topweight counts as t·m
  (the director at 30 m is the cruiser's real cost); the mast's kind is the stiffness `M_vib` reads.
- **The transmitting station** becomes a room below the armour (crew, power, the computer's mass), so damage can find it.

**Seeing further: sighting on the new heights** (the user, 2026-10-17: taller ships see further). The sighting model
(3.2c, `SightModel`) already works from heights: each eye's horizon (3.86√h km), how much of the target the horizon
hides, and its silhouette's area above that, masts included. So taller eyes see further and taller ships are seen
further by the geometry alone, once the parts are right. What this step makes right:

- **The eyes** (`Sight`): a lookout in each top at the top's floor + 1.5 m, as the directors' eyes are, instead of 1 m
  under a placeholder masthead (`MastLookoutUnderTopM` goes, and the funnel + 6 m rule); a director or rangefinder in a
  top is an eye at its height; a bare pole mast with no top keeps a masthead lookout only where the design puts one.
  Tops stand above most of the ship, so their blind sectors shrink to the masts and funnels round them.
- **The silhouettes:** masts by kind (a pole, a tripod's three legs, a tower's bulk), tops, and the instruments on them
  enter the side-on and end-on areas above each metre, so a battleship's top shows over the horizon before her funnels,
  and a tower mast more than a pole. Identification counts masts as today; their kinds become one more cue later.
- **Who looks:** `Sightings` lets each side's 6 nearest observers look with their 4 highest eyes that bear: tops come
  first by that rule already. Checked that a ship's best eye isn't crowded out by several eyes at one height.
- **The reach of the world's checks:** `EncounterRangeM` (50 km) was set to cover the longest masthead-to-masthead
  sighting with masts at ~24 m. With battleship tops at 40-55 m, top to top is 3.86(√50 + √50) ≈ 55 km, past it. The
  encounter range (and `SightMaxM`, 60 km) is derived from the tallest eye and the tallest silhouette among the classes
  at sea, horizon to horizon plus the margin, instead of a fixed number, and the cost of the larger encounters measured
  on the Jutland (ships kept off rails longer).
- **Re-calibrated, not just moved:** 3.2c's anchors (the Jutland's battlecruisers ~24-26 km in haze, Denmark Strait
  ~35 km in clear air) were fitted with eyes ~10-25 m too low. They're run again on the real heights through
  `sighting_ref.py` beside the model; if the record now comes out long, the haze's extinction or the threshold contrast is
  re-fitted (a visibility still means "a reference ship made out at it"), never the heights. The picture's fixes and fire
  control's `Sees` read the same eyes, so the rangefinders in tops see as far as their lookouts.
- **To look at:** `shipgen weapons` and a new `sight` listing print each class's eyes (kind, height, blind sectors) and
  its silhouette's top; on the map, the selected ship's horizon ring from its highest eye (dev).
- **Tests:** taller eyes see further on the same target in clear air, a hull-down battleship is made out by her top before
  her hull, the anchors within their bands, the encounter range covering the tallest pair, hashes moved and reported.

**The cut** (2026-10-18): F4a masts and tops from the design; F4b directors in tops and the rangefinders as parts; F4c
the transmitting station; F4d sighting on the new heights.

- **F4a done 2026-10-18** (the user: the drawing follows the design, looks only colour; heights by a rule a design may
  override). A design's `masts` (`docs/shipgen/README.md`) give each mast's kind and top; heights left out follow the
  ship (`MastPlan`: the top's floor at 2.25 times the highest gun's height over the water, a topmast over it). Tops are
  hitbox parts (`top`), mast weights go by kind (tripod 3 poles, cage and lattice 4, tower 8, each top's tier ~2 t), the
  plan and profile draw the kind's legs and struts as dressing and the tops from their hitboxes, and the looks' mast
  shapes went (the shipped warships' masts were written from what their looks drew, so they look as before, the tops
  now at their heights). The sim's lookouts stand in a mast's top (its floor + 1.5 m), pulled forward from F4d since
  topmasts now reach 40 m. Standard displacement: median 0.0 %, at most +2.5 % (Fubuki's two-tier tops). Tops at 29-31 m
  on Lion, Nagato, Seydlitz, in the research's band; low on Dreadnought (22 m against 28-30) and Yamato (26.5 against
  ~40), the hull model's turrets standing lower than the real ships'. Those are defaults: the designer gets a knob for
  the heights later (the user, 2026-10-18; F12). fc-history 11 of 23 in band (12 before: the Grand Fleet BS 7.1 to 7.3%,
  over its band's 7.2); squadrons and Tsushima over 5 seeds within the spread.
- **F4b so far (2026-10-18):**
  - *Directors in tops:* a mast's `director` names a battery one of whose directors stands on its top's roof
    (`FireControl.PlaceInTops`), the top growing to hold its hood. The 33 Great War capital ships put their main
    director in the foretop (Iron Duke's eye 23 to 31 m): fc-history 13 of 23, the Grand Fleet and German battleships in,
    director over independent 1.79 to 1.51.
  - *Turret rangefinders by design* (the user: physical, by design): a battery's `rangefinder_m` puts one in each
    gunhouse, the hitbox's hood spanning the base; the fixed bar every armoured turret had goes, so turrets pack closer
    (median 0.0 %, -1 to -4 % standard where the bars set the spacing). The 50 capital ships from the Great War on carry
    them (2.7 m under 15", 4.6 m from it, later the director's base). In the sim each is an eye blind outside its turret's
    arcs, and a battery in local control reads through every working one (and its best layer's eye last). The Great War
    cases sit at their bands' edges: fc-history 11 of 23 (the Grand Fleet BS 7.2 and German battleships 6.0% step over).
    The flotilla's daylight attack gets in less often (5 of 12 seeds, 7 of 8 before).
  - *A bug found on the way:* the path bender's leg wasn't made ready with a class's other scratch, so a first glance
    allocated mid-fight (fixed alone).
  - *Control positions' rangefinders:* `fire_control.aft_rangefinder_m` and `conning_tower_rangefinder_m` put a hood
    (a block of role rangefinder) on after control's roof and the conning tower's; in the sim each is an eye and a
    fire-control unit with no director, after the main battery's directors: one more instrument when the battery is engaged
    alone, the control a second target takes before local control, and when only positions are left the guns lay by their
    own sights on the station's solution. The Great War capital ships with after control carry a 2.7 m set (the conning
    tower's sits under the bridge's levels in this layout, so none). A second instrument lifts the Great War cases a
    little (German 1st SG 3.9 to 4.7%, British BCS 3.2 to 3.6%): fc-history 10 of 23.
  - *Weights and fit (F4b-3):* the instruments' weights follow designer §3.4 (three times the bare instrument, a 6 mm
    hood box). The directors' own weights were already calibrated to sourced directors (a Mk 37 about 20 t, a Mk 51 1.7 t,
    Bismarck's 10.5 m about 45 t: docs/shipgen/README.md), so they stay: §3.4's box carries a flat 8 t of WWII gear that
    would overweigh a 1914 director sight. A director fitting its deck (§7.1) already holds: it takes only a roof spot its
    whole footprint fits, the rangefinder's arms included, and the build fails when one finds none.
- **F4b done 2026-10-18.**
- **F4c done 2026-10-18** (the transmitting station). Each battery with directors gets a room of kind
  `transmitting_station` holding its computer: the block's mass weighs there, its crew stand there at battle stations,
  and the old `computer_t` goes (design format 7; it was the director's share of a plotting room, low in the hull). It
  lies in the deck space under the protective deck, 15 m² and 3 m² a man [EST], 0.4 B wide and at least a section long.
  It takes the free stretch nearest the conning tower, between the vitals' ends, that no magazine, handling room,
  machinery or earlier station fills (`FireControl.PlaceStations`). Its cells give way only to those rooms (priority 6,
  permeability 0.85). Where there's no free stretch it shares the cell of the station before it, still under the armour.
  - *Where they land:* all 139 stations of the shipped and scenario designs are under their ships' armour, except on the
    unarmoured destroyers and carriers. 13 share a cell: later ships with three director batteries, where the handling
    rooms' bulkheads swallow the stations' own (KGV's secondary and HA stations, over B turret's handling room).
    Dreadnought's sits under her conning tower, in a 337 m³ cell under the 49 mm deck, 10 men and 4 t.
  - *Departures:* research 02 §4 has the station "below the waterline and inside the armored belt". The deck space
    under the protective deck straddles the waterline on most ships, and the spaces wholly below it are the magazines'
    and the machinery's sections (a short magazine takes its whole section's cell). Tried below the waterline: 61 of
    139 stations lost their cells to the secondaries' magazines. The size is an estimate. The computer's power waits for
    the power networks.
  - *Numbers:* standard displacement median +1 t; the fuzz designs whose random `computer_t` ran to 500 t a director
    lose it (one -30 %). Bismarck -56 t, KGV -22 t, Dreadnought +5 t. fc-history 10 of 23 in band, as after F4b
    (director over independent 1.58 to 1.57). Squadrons and Tsushima over 5 seeds within the spread.
  - *Tests:* the weight moved the destroyer's layout 0.7 m, which tipped her subdivision from 18 sections to 17.
    The 10-hit destroyer test's 60-ship sample then drew 22 % lost against a 20 % band, but 600 ships give 13.2 % and
    4.8 % (12 and 6.8 before), so the sample grew to 200 ships (14.5 and 5 %). The band is unchanged. The near-miss world
    run's destroyer now goes down under the salvos' hits (15 hits and 40 near misses, 26 and 31 before); the test takes
    that as damage taken.
- **F4d done 2026-10-18** (sighting on the new heights). F4 is complete.
  - *The eyes:* a lookout stands in each mast's top (since F4a); the masthead placeholder (`MastLookoutUnderTopM`) went,
    so a bare mast has no lookout aloft. A design gives one with a new top kind, `crows_nest`: a lookout's barrel 0.75 m
    in radius, its roof 1 m under the masthead by default, 0.7 t. `destroyer` carries one on her foremast. The WWII and
    later ships whose masts carry no top (Bismarck's, KGV's, Littorio's) see from their towers' directors and bridges,
    as they did.
  - *The silhouettes:* masts by their kind instead of their hitbox's 1.4 m column: a pole 0.8 m, a tripod 1.2 m side on
    and 1.6 end on (its struts), a cage or lattice 1.5, a tower 3.6 [EST], a topmast's 0.4 m pole over the top (or over
    0.6 of a bare mast). The raster went from 0.5 to 0.25 m so a pole isn't rounded up to a metre; its tables are
    summed by rows (the old loop was O(cells × metres)).
  - *The reach:* `SightReach` (Sim) is the tallest eye's horizon and the tallest silhouette's together among the classes
    that have come into the world. Encounters hold opposing ships within it, and the looks reach as far. The fixed
    `EncounterRangeM` (50 km) and `SightMaxM` (60 km) went (`EncounterRangeMinM`, 0, is a floor). When a taller class
    comes, every opposing pair is solved again at once, as when sides go to war. A destroyer alone reaches 36.2 km.
    With Derfflinger (her director in the foretop at 37.8 m, her masthead 47.6 m) the reach is 50.3 km, so the Jutland's
    encounters are about as wide as they were. A look costs 83 µs a game second on the Jutland bench (128 before: a
    pre-dreadnought world's reach is shorter still).
  - *Re-calibrated, not just moved:* the anchors are now the record's own ships' median first sightings, closing at
    30 kn on a 45° aspect, with their 4 highest eyes (`SightModel.FirstSightingM`). On the real tops and the directors'
    optics, the old haze had Lion first sight Derfflinger at 30 km in Jutland's 25 km visibility (the record: ~24).
    The haze is re-fitted, not the heights. The reference ship that defines a visibility is now 3,000 m² side on
    (about a capital ship's; Lion's is 2,965) instead of 1,000. Lion first sights Derfflinger at 25.0 km; Prince of
    Wales (KGV) sights Bismarck at 36.2 km in clear air (the record: ~35, the horizons' limit). `sighting_ref.py` and
    its table moved with it.
  - *To look at:* `shipgen sight DESIGN... [--from DESIGN]` lists each class's eyes (kind, part, height, horizon,
    rangefinder, blind arc) and silhouette by height, how far a lookout makes it out, its first sighting closing at
    30 kn, and the world's reach. On the map, the dev `horizon` checkbox rings the selected ship with the sea's
    horizon from her highest eye, and fainter, her reach to a ship as tall as she is (not checked by capture: it needs
    a ship selected).
  - *Numbers:* fc-history 9 of 23 in band (10 after F4c): Denmark Strait's Bismarck 10.3 to 12.2% (band 2.7-11), the
    Germans over the British 1.30 to 1.41 (want 1.5-5). Tsushima over 5 seeds: Russia's hits 413 to 296, its losses 5.0
    to 4.6 ships (11,530 t, 22,789 before); Japan's hits 522 to 501. Squadrons within the spread. The Jutland played
    once is a different battle: the forces sight each other later, the light craft's early melee doesn't happen, and
    the fight runs longer in the fine tier (197 s to play against 107 s). Britain loses 10 ships (29), Germany 31 (27)
    but 98 kt (163). Per game second the sim costs the same.
  - *Tests:* new: taller eyes see further and a battleship's top shows with her hull under the horizon; lookouts in
    tops and crows' nests, none on a bare mast; the reach follows the tallest eye and top at sea; the record's anchors
    on the real ships. Moved: the 200-ship looks test's lines 25 to 20 km apart (Dreadnought in 30 km visibility was at
    the edge); the encounter's radius is half the derived range; the friend on the torpedo track 1 km to 600 m along it
    (a later first sighting led the aim 13° and put her 220 m off); the flotilla test's seed 2 to 7 (6 of 12 seeds
    attack now, 5 after F4b).
  - *A bug found:* the crow's nest's 0.7 t made the destroyer 2.6 m shorter (the sizer's bistable layouts;
    docs/battle/bugs.md).

### F5 Sensors proper

- Every instrument a sensor with its own rate, bias and bearing error; the tracker takes all of them, so random error
  falls as 1/√N and each instrument's bias stays until a spot takes it off (§3.5).
- **Effective base and magnification** (designer §3.1): long bases deliver less, magnification limited by the air
  (haze from the weather) and by vibration (mounting stiffness, height); stereo better in haze, coincidence worse end-on
  and at low contrast; dusk and night multiply the error and need the target in sight (3.2's light).
- **Crew:** the side's quality stays the rangetakers' multiplier; the stereo operator pool waits for the navy layer.
- **Anchors:** the designer note's base study (D6: the knee by range, the eye at 10-20% of a 4.6 m set's hits, 15 m
  +20-30% at 20 kyd), its arrangement study (D8: a second instrument +7-10%), and §3.3's per-reading errors.

**The cut** (2026-10-18): F5a every instrument a sensor; F5b the instruments' make (kinds, the stadimeter, effective
magnification from the mounting); F5c the air (the ranging error from the sighting model's contrast: haze, light,
small and hull-down targets; stereo against coincidence); F5d the anchors' bench (D6, D8).

- **A bug found first** (fixed alone, 2026-10-18): a rate clock took each reading's range and bearing from the
  instrument that made it, but keeps one state for the ship, so on a ship with directors fore and aft their bearings
  (tens of mils apart close in) read as a bearing rate. Every cut now reaches the computer from the ship's reference
  point, as the tables' parallax correction did. fc-history 10 of 23 (9 before); director over independent 1.57 to 1.40
  (in). Tsushima over 5 seeds: Russia's hits 296 to 394 (the Borodinos' two directors), Japan's 501 to 475.
- **F5a done 2026-10-18** (every instrument a sensor). Under its directors a battery reads every working instrument of
  every unit it holds (not only each unit's first), and every working gunhouse rangefinder of its mounts reports to the
  computer too, longest base first, each through its turret's arcs (the transmitting station plotted every
  instrument's cuts; the German ranges were averaged by a machine), up to 8 a battery (4 before; its readings' queue 16).
  Each keeps its own bias, so the computer's falls as the instruments add, and a spot takes off what's left. Each
  reading's bearing error is its post's, one law with the picture's fixes (`SightModel.BearingSigmaRad`: a director
  0.7 mil, a gunhouse 1.5, a control position's rangefinder 1, designer §3.1; the director's was 0.5 in the picture and
  `RangefinderBearingMil`'s 1 in fire control, a setting gone). A director ranging by eye keeps its sight's bearing.
  - *Numbers:* fc-history 10 of 23 in band, as after the bug fix: the Great War capital ships gain from their turret
    sets (Jutland's British BCS 3.5 to 3.9%, the Grand Fleet BS 7.6 to 8.7%, Dogger Bank's Germans 2.7 to 3.4%), the
    haze that held their rangefinders back being F5c's. The orderings as before (the Germans over the British 1.14,
    want 1.5-5). Squadrons over 5 seeds: Britain's hits 52 to 31, Germany's 110 to 103, losses within the spread.
    Tsushima: Russia's hits 394 to 421, Japan's 475 to 624. The Jutland's gunnery costs 33 µs a call, as before.
  - *Tests:* `Fight_turns_away_when_outmatched` judged her at 5 minutes; the battleships now slow her under their
    turrets' ranges at about 4 (she then can't refuse them, and fights, as the test's comment had it), so it takes her
    turn away when it comes.
- **F5b done 2026-10-18** (the instruments' make). An optics block's kind (coincidence, stereo), top magnification,
  coatings and gyro mounting now reach the sim (`OpticsRow`), and an instrument's kind is its make's (`shipgen weapons`
  lists the German sets as stereo). Each post knows what it stands on (`EyeRow.Support`, from the part under its
  floor: a mast's top by the mast's kind, the conning tower, a gunhouse, else a roof). A reading's unit of error takes
  the useful magnification (`FireControl.Magnification`, designer §3.1): the clear air's shimmer (×60) and the
  support's shake cap it, M_eff = M/√(1 + (M/M_lim)²), the stiffness (turret 70, tower 60, roof 55, tripod and lattice
  50, cage 40 [EST], pole 32) falling as √(25 m/h) above 25 m and half again higher on a gyro mounting, normalised so
  ×28 on a tower is ×28; past the glass's top magnification the image dims. The picture's fixes take the same law.
  Lion's foretop director reads at ×26.1 (tripod, 34 m), Derfflinger's at ×19.8 of ×23 (pole, 38 m), the turrets' at
  ×28.6. `shipgen sight` prints each rangefinder's kind, useful magnification and support.
  - *Numbers:* as the research says, a few per cent at most: fc-history 10 of 23, every case within its runs' noise
    (Dogger Bank's Germans 3.4 to 3.2%, the German battleships 6.6 to 6.9%). Squadrons over 5 seeds: Britain lost 1.6 of
    4 (0.4 after F5a; 0.8 at F4d, from 0 to 3), hits 38 and 108; Tsushima: Russia's hits 463, Japan's 482.
  - *Tests:* new, a foretop director on her tripod, gunhouse sets on their turrets, the kinds by make, and the useful
    magnification by support. `Fight_with_no_escape_fights` ran 8 minutes and read the range to a ship that had sunk in
    them (Dreadnought under four Queen Elizabeths): it judges her closing while both are afloat.
  - *Left:* the stadimeter. It is a second make beside a battery's rangefinders (the reference's level 1: the FA and a
    stadimeter), and a fit has one optics block; it waits for instruments of more than one make on a battery, with the
    typical fits the research request asks for. Spray on low instruments (designer §3.1) waits for the sea state the
    ship-motion pass brings; vibration at speed (the Falklands' stereo sets) has no numbers yet.
- **F5c done 2026-10-18** (the air). The haze reaches the rangefinders through the sighting model's own contrast: an
  instrument's margin on the target (`SightModel.Margin` through its make's magnification: the haze's extinction, the
  light's reach at dusk and night, the target's size, aspect and what of her shows over the horizon, a silhouette
  against a low sun) sets its factor (`SightModel.RangingFactor`): 1 from a margin of 4 up (a capital ship at 15 km in
  clear air), under it (4 / margin) to a power, 0.8 for coincidence (the half-images' edges) and 0.4 for stereo (whole
  images fused), coated glass taking 0.8 of the excess [EST: the powers give the reference's Jutland factors, a stereo
  set's 1.4 and a coincidence set's 2.25 on a low-contrast target]. A reading's error and its instrument's bias take
  the factor, its bearing the square root (fire_control_ref.py's k_optic). An instrument whose margin is under 1 takes
  no reading and doesn't see the fall: the director's or turret's own optics, a layer's binoculars (×7), not only the
  side's picture. The picture's fixes take the same factor. At Jutland the British sets read at k 1.8 (BCs, 12.5 km in
  16 km), 2.0 (3rd BCS) and 2.1-2.3 (Grand Fleet), the German stereo at 1.25; Dogger Bank's clear air at 1.0.
  - *Numbers:* fc-history 10 of 23. Haze costs the Jutland cases 7-13% of their hits (designer D8: 15%), British BCS
    3.9 to 3.4%, Grand Fleet BS 8.5 to 7.4%, German battleships 6.9 to 6.3%, German 1st SG 4.4 to 4.1%: the Germans
    over the British 1.13 to 1.21 (want 1.5-5). Close in the rangefinders aren't the miss that matters (the 3rd BCS
    17.0 to 16.6%): what's left there is fire discipline and spotting (F6, F7), smoke and splash confusion. Squadrons
    over 5 seeds: Britain lost 1.2 of 4, hits 32 and 87; Tsushima: Russia's hits 490, Japan's 686.
  - *Cost:* margins are looked at only for a reading due, or until one sensor sees: a battery 24 µs a game second (31
    looking at every sensor every second), the Jutland's gunnery 35 µs a call.
  - *Tests:* the air's factor in clear air, haze, at dusk and past the night's reach, stereo under coincidence, coated
    under bare.
- **F5d done 2026-10-18** (the anchors). F5 is complete.
  - *The bench:* `shipgen fc-sensors [--runs N]`, the designer note's cruiser study (§4) on the sim: the heavy cruiser
    (9 x 8"/55) rebuilt with one director of a given base, magnification and kind and nothing else ranging unless asked
    (after control's and the turrets' sets), against a cruiser at 30 kn on a parallel course, steady or zigzagging ±25°
    every 150 s, clear (35 km) or haze (25 km), practice's errors, 15 minutes; the variants share seeds, so close
    arrangements can tie shell for shell. About 7 minutes at 24 runs. A narrow field slows the readings past ×30
    (designer §3.1: ×√(30/M)), which no catalogue block reaches.
  - *D6, hits against base* (relative to 4.57 m; the research after the slash), the keeper: eye 18/14, 17/10, 13/10,
    7/11 at 10, 15, 20, 25 kyd; 2.74 m 93/91 to 75/79; 15 m 114/110, 121/119, 125/128, 136/140. The table's 15 m
    109/107 to 146/137, the plot's 118/107 to 112/119. The knee and the rise with range are the research's: the eye at
    10-25% of a 4.57 m set (the research's 10-20%), 15 m +20-25% at 15-20 kyd (+19-28%). The plot at 25 kyd is noisy (4.5
    hits a run).
  - *D8, the arrangements* at 18 kyd, table tech, zigzag, clear / haze against the research: three 4.57 m turret sets
    +31/+22% (the research +9/+16% for one; +7-10% an instrument); 8.1 m in the director +13/+9% (+12/+22%); 4.57 m
    director and 8.1 m turrets +25/+20%; ×15 -19/-21% (-15/-13%); ×35 -4/-2% (0/+2%); ×50 -20/-11% (-12/-3%). After
    control's set doesn't fit this cruiser's after control roof, even at 2.74 m (shipgen leaves it off), so that row
    is empty. **A departure:** coincidence instead of stereo costs 0/-3% here against the research's -16/-11%: at 18 kyd
    in 25 km the margin is ~2.5 and the AFCT's track isn't bound by one instrument's random error; the research's
    clear-air penalty comes mostly from its readings' rate.
  - *Absolute hits* sit above the research's (the keeper's 4.57 m 151 / 53 / 19 / 7 against 78 / 27 / 11 / 5): the sim
    is still too good close in and against steady targets, the fire discipline and spotting of F6-F7.
  - *Tests:* one reading's error against the research's table 2 (the FQ2 9 ft and FT24 15 ft at 10 and 20 kyd) and
    Lienau's Zeiss 3 m figures. The bench is too slow for a kept test; it's run, like fc-history, when a step changes
    the sensors.

### F6 Fire discipline: finding the bracket, then hitting

**What goes wrong today.** A battery fires whenever its bearing mounts are loaded and laid, from the first second the
track opens. The placeholder 6" reloads in ~13 s against a ~20 s flight at 10 km, the 4" in ~7 s: two or three salvos
are in the air at the same uncorrected aim before the first one's spot is in, and heavy guns at 18-20 km fire their
second before the first falls. Those salvos carry the same error, their spots say the same thing, and the shells are
gone. The spot itself is right (each is applied against the correction its salvo was fired with, so they don't pile up);
what's wrong is firing salvos that can't carry anything new. The research's rule: a ranging salvo is information
(§9, `computing.md` §2.5; pyramiding happens "when the salvo interval < TOF + spotting time").

**A battery's phase** (per fire-control unit, world state, hashed, shown in the strip and the snapshot):

1. **Opening.** The track settles (today's latency + `GunneryOpenS`, later the tracker's own uncertainty below a
   doctrine's bound); the guns are loaded and laid, and wait.
2. **Ranging: finding the bracket.** Each salvo must carry something new, in one of the doctrine's two ways:
   - **Single salvos, each waiting for the last one's fall** (the RN in 1914-16, the USN ladder spotted salvo by
     salvo): the next goes when the spot is in (fired + time of flight + the spot's delay, the TOF buzzer), on the
     corrected aim. Loading goes on meanwhile; the guns wait loaded. The interval comes out near TOF + spot + the
     director's cycle, as New York's 66 s (31 s of it loading) did.
   - **A ladder in flight,** several salvos before the first falls, each deliberately offset by the step (the German
     ladder, successive turrets ~400 yd apart; the RN's two salvos in flight after Jutland; Bismarck's three partial
     salvos 2 s apart at three ranges): their falls bracket the target in one flight time, at the cost of shells.
   - **Partial salvos:** enough guns to spot a wild shot (3, better 4 or more: `navalgazing Spotting`), the rest held
     loaded; groups alternate, so the spotter gets falls twice as often.
3. **The bracket kept in correction space.** Each spotted salvo says "the target is beyond (or short of) the correction
   this salvo was fired with, plus its ladder offset". The battery keeps the bracket those spots make, a low and a high,
   instead of today's "last sign" (`FireControl.Ladder`), so spots that arrive out of order or from one ladder all fit:
   - no bracket yet: the step beyond the side seen; a bracket: into it by the doctrine's rule (the RN's 400, 200, 100 yd;
     the USN's halving, never below a pattern);
   - **the bracket isn't hard:** the spotter misjudges and the target moves, so a spot against it widens it, and it
     widens with time by the track's rate uncertainty (the truth drifts in range as the rate error carries it);
   - **spots one way in a row correct the rate,** not only the range: the RN's "down 200, rate down 100" (a rate
     correction about half the range correction) goes into the tracker's range rate. The plot-based machines needed it
     most; it's where a hand plot's ±5-8 kn shows.
   - **the first step scales** with what the computer expects its own error to be (the track's covariance and the
     residual bias over the time of flight), within the doctrine's step; point blank (F7) has none.
4. **Fire for effect.** On the spotter's straddle (his judgement, not the truth: a misread straddle fires for effect
   on a wrong mean until the spots say so), the battery fires as fast as it loads and lays: full salvos, or alternating
   halves to keep the spotter fed. The spots keep correcting at the doctrine's gain: the USN's deflection spots, with an
   add-spot of a pattern every 3-4 salvos to check for overs; the rocking ladder (+100, 0, -100 yd) against a target that
   manoeuvres.
5. **Back to ranging** when the doctrine's run of salvos (2-3) isn't a straddle, the tracker sees the target turn
   (F2's detection, or the spots drifting one way), own ship turns hard (every turn for a machine that isn't helm-free),
   or the target changes. A shift of target starts at opening, from the new contact's track.
6. **The rate in fire for effect** comes from the parts, not a factor laid on top: the mount's drill reload; a battle
   loading factor from the crew (fatigue, heat) and the supply (ready-use rounds first, then the hoists: the Majestics'
   70 s from ready rounds against 100 s after, Delhi's 5"/38 25 against 15 rounds a minute); the director's cycle (the
   layer's settle; firing on the roll when ship motion comes); the doctrine's ceiling (deliberate fire at long range,
   rapid close in). The research's battle rates, 30-60% of drill and USN main batteries at 30-84 s a salvo, are the check.
7. **Ammunition as a choice:** the doctrine holds fire, unless ordered, while the battery's own estimate of its hits
   per shell (F10) is under a floor, and keeps a reserve of the magazine; firing on regardless is an order (the
   secondaries at the edge of their reach, `fighting.md` 4.2c's "left as it is").
8. **Pyramiding as a crew's error:** a trained crew applies each spot against the correction its salvo carried, as now;
   a green one sometimes adds it to the correction in force instead, a chance by crew quality (the TOF buzzer is the
   cure, so it's a fit too).
9. **Independent fire** for quick-firers close in: each gun at its own rate, spotted by its own layer, with whose splash
   it is (F7) as the cost; salvo fire beyond the range the doctrine sets (the pre-dreadnoughts' shift at 5-6 km).

The doctrine picks among these per navy and year as data (the side's, decision 3); defaults from the research: the RN
of 1914 single salvos and the 400-200-100 bracket, of 1917 two in flight; the German ladder; the USN ladder with the
pattern floor and the rocking ladder; the IJN's ladder.

- **The bench:** `duel --trace` prints each salvo's phase, its offset and the bracket after its spot; the summary
  gives salvos and shells to the first straddle, the rate in fire for effect, and shells per hit.
- **Anchors:** salvos and time to first straddle and hit (§10: practice hits from salvo 2-5, Tennessee's first hit on
  salvo 4 at 3 min 42 s, Lion ~10 salvos at Dogger Bank, Bismarck's fifth on Hood); hits per gun a minute (the WWI
  ~0.165, 1940's 0.75-0.84 for a 14" battery at 12.7 kyd); the salvo intervals above; ammunition: a battle line's
  magazines last the hours of a Jutland, not two hours at the loading rate (`command.md` 5.1g).

**The cut** (2026-10-18): F6a the phases and the bracket (items 1-5, single salvos); F6b the side's discipline
(the ranging ways, alternating halves, the presets by navy, the scenario's and the bench's); F6c the rate spot, the
first step from the computer's own doubt, pyramiding as a green crew's error (items 3 and 8); F6d the rate of fire
from the parts and independent fire (items 6 and 9); F6e ammunition as a choice (item 7), moved to F10 (the user,
2026-10-18).

- **F6a done 2026-10-18** (the phases and the bracket).
  - *The phases* (`FirePhase`, world state, hashed, in the snapshot; the strip says "ranging" or "fire for effect"):
    opening as before (the track's start + `GunneryOpenS`); ranging, each salvo a partial one (`RangingGunsShare` of
    the guns that bear, at least `RangingGunsMin`, 4; the mounts taken from where the last left off, so the groups
    alternate) fired when the last one's spot is in (`InFlight`; or 15 s past its flight and the spot's delay, a spot
    that never came); fire for effect on the spotter's straddle, full salvos as the guns load and lay, as before.
    Back to ranging after 3 spots in a row without a straddle (the bracket opened the way they all pointed, if they
    did), on own ship's turn (any past 0.2°/s for a plain rate clock or a judgement, past 1°/s for a computer that
    takes own ship's motion in) and when the tracker sees the target turn (`FcTracker.Turns`, the curved-course and
    turn filters' detection); those two also open the bracket round its middle to the first step.
  - *The bracket* (`FireControl.Bracket`): each spot bounds the correction the target needs, against the correction
    its salvo was fired with, so spots in any order fit. Open on a side, the next salvo goes the first step beyond
    the bound (the computer's `FirstStepM`, renamed from `Ladder0M`); closed, into its middle: the RN's 400-200-100
    and the USN's halving are both this, and the first two spots give exactly the old ladder's corrections. Never
    narrower than a pattern (`BracketFloorPatterns`, and 92 m), so in fire for effect an over or short moves the aim
    half a pattern from the salvo's, as the old ladder's floor did. Each bound widens at the computer's own doubt of
    the range rate along the line of fire (`FcTracker.RateSigma`, from each tracker's covariance; a judgement's never
    learnt guess), and a spot against a bound drops it.
  - *The bench:* `duel --trace` gives each salvo's phase and its spot (over, short, straddle, not seen), the bracket
    after it and the next correction; the summary adds the first straddle's salvo and the shells to it, the ranging
    salvos' share of the shells and their interval, the interval in fire for effect, shells a hit, and the spotter's
    calls against the truth.
  - *What it found: the spotter's straddle call.* Spotted as the reference does (each splash judged over or short
    with its own error of k·R²/h and a common half of it; "straddle" if any over and any short), the spotter calls
    most falls straddles at long range, and most of those calls are wrong. Queen Elizabeth on a QE at 8, 12, 16 and 20
    kyd: he called 63, 64, 74 and 81% of the falls he saw straddles, and 81, 47, 31 and 25% of those were (he missed
    almost no true one). With a ~400 m error a splash at 16 kyd, four or eight splashes nearly always include an over
    and a short, and "can't tell" comes out as "straddle". So ranging ends on the first or second salvo (16 kyd: the
    first straddle on salvo 3 after 16 shells, ranging 5% of the shells) and fire for effect holds a mean that's off,
    corrected only by the run of misses. F6's discipline can't show what it buys until the spot says what a spotter
    could see (F7's "what a spot says": splashes in line with the target judged by whether they hide its waterline or
    are hidden by it, the rest by depth, and "doubtful" when he can't tell).
  - *Numbers* (fc-history `--runs 24`): 10 of 23 in band, as before; every case within its runs' noise (Dogger Bank's
    British 3.2 to 3.1%, the 5th BS 2.6 to 2.3%, the German 1st SG 4.1 to 4.5%); the first hits a salvo or two later at
    long range (the 5th BS salvo 7 to 11, Dogger Bank's British 10 to 12). Squadrons over 20 seeds: Britain's hits 35
    and 35, Germany's 92 to 122, losses 1.4 to 1.2 British ships and 0 to 0.1 German. Tsushima over 5 seeds: Russia's
    hits 490 to 359, Japan's 686 to 498, losses 4.4 to 3.8 Russian and 3.2 to 3.0 Japanese ships.
  - *Cost:* a battery 27 µs a game second (26 before); nothing allocated (`Fire_control_allocates_nothing`).
- **F6b done 2026-10-18** (the side's discipline, decision 3).
  - *The disciplines* (`SimSettings.FireDisciplines`, a `FireDiscipline` each; a side's by `GunnerySystem.SetDiscipline`,
    hashed, the first by default; a scenario's `"fire_discipline": { "Germany": "german" }`, checked by the loader):
    salvos in flight while ranging (1: each waits for the last one's fall), a ladder's step (with one, each group of
    that many goes at once, stepped about the bracket's aim, and the next group waits for all their spots; without,
    they roll), a ranging salvo's share of the guns that bear and its least (the shares are of the guns that bear,
    loaded or not), alternating halves in fire for effect, and the spots in a row without a straddle that send it back.
    The presets from the research (computing.md §2.5, §9): **rn-1914** single half-salvos, at least 4 guns, halves in
    fire for effect; **rn-1917** the same with two in flight; **german** the ladder, three salvos 366 m (400 yd) apart
    of a quarter of the guns, at least 2 (successive turrets), halves; **usn** single half-salvos and full salvos in fire
    for effect. The IJN's, the Russians' and the other navies' aren't in the research (a request,
    `research/REQUESTS.md`), so Tsushima's sides take the first.
  - *Halves by mounts:* firing for effect in halves, the larger half of the bearing mounts and the smaller in turn
    (five turrets: three, then two), each half a reload after the other. Shared by guns, a half of an odd turret count
    couldn't be made, and Seydlitz's fifth and fourth turrets waited for a third: 3,170 shells in a duel became 5,064
    when every gun fired at its reload.
  - *The bench:* `duel --discipline NAME` (the summary's first line names it; the trace shows a ladder's step), and
    fc-history gives each case its navy's (the British rn-1914, Rodney rn-1917, the Germans german, the Americans usn,
    the Japanese the first), printed with the case.
  - *Numbers* (fc-history `--runs 24`): **15 of 23 in band** (10 after F6a). The Great War cases fall, most into their
    bands: Dogger Bank's British 3.1 to 2.5% (first hit salvo 17, late against the record's ~10, a half-salvo counted as
    a salvo), the Jutland BCS 3.3 to 2.7%, the Grand Fleet BS 7.8 to 6.7%, the German battleships 6.6 to 5.8%, the
    German 1st SG 4.5 to 3.3%, Dogger Bank's Germans 2.9 to 1.7%; the Yellow Sea 10.7 to 8.6%; Denmark Strait 11.7 to
    10.5%. The halves give the spotter falls twice as often, and his corrections (half a pattern a spot, against
    today's noisy calls) chase the salvo error twice as often: the research's "full corrections chase the random salvo
    error" at work, on a spot model F7 is to redo. The USN's cases (full salvos) don't move. Orderings: the Germans
    over the British 1.37 to 1.20 (want 1.5-5), a steady target over a salvo chaser 3.75 to 2.75 (want 3-10), director
    over independent 1.61 to 1.58. Disciplines side by side, Lion on Derfflinger at 15 km: rn-1914 4.0%, rn-1917 4.8%,
    usn 5.0%. Squadrons over 20 seeds: Britain's hits 35 to 29, Germany's 122 to 63 (the ladder's partial salvos),
    losses 1.2 to 1.1 British ships and 0.1 German. Tsushima over 5 seeds: Russia's hits 359 to 343, Japan's 498 to
    427, losses 3.8 to 4.2 Russian ships and 3.0 Japanese.
  - *Tests:* the German ladder's first group stepped -366, 0, +366 and its first two together, then the halves of
    three and two turrets alternating; Bismarck's 150 mm halves 5.9 s apart (a reload of 11.2 s). The turret test
    checks no salvo is more than the forward guns (partial salvos now).
  - *Cost:* 27 µs a battery-second, as F6a.
- **F6c done 2026-10-18** (the rate spot, the first step, pyramiding).
  - *The rate spot* (the discipline's `RateSpotShare`: the RN's 0.5, none for the Germans and the USN, whose manual held
    spotting during rate corrections the same as pyramiding): when two spots in a row move the aim the same way, the
    range rate takes that share of the move a minute ("down 200, rate down 100", computing.md §2.5), set on the machine
    as its operator would (`FcTracker.CorrectRate`: a rate clock's range rate, a plot's velocity, a turn filter's speed
    and heading, a judgement's guess until it looks again). The bracket stays as it is: the rate's error was what moved
    the needed correction, and it's taken off at its source.
  - *The first step* (`FirstStep`): twice the error the computer expects of its own solution over the time of flight
    (its doubt of the target's position and rate along the line of fire, `FcTracker.PositionSigma` and `RateSigma`, and
    its residual bias), never more than its own first step (the RN's 400 yd) nor under the bracket's floor; set when
    ranging opens and each time it goes back. At the opening it's mostly the 366 m cap; a settled Queen Elizabeth goes
    back to ranging at 17.5 kyd with 266 m.
  - *Pyramiding* (the crew's `Pyramid`: green 0.25, average 0.1, trained 0.03, elite 0 [EST]; "a common error", NO&G
    18C): the chance a spot is put on the correction in force rather than the one its salvo was fired with, range and
    deflection both. Lion on Derfflinger at 15 km over 48 runs: trained 3.9%, green 3.5% (the green crew's rangetakers
    and layers worse too). **Left:** the time-of-flight buzzer as a computer's fit (the cure): only the Ford Mk 8 has one
    in the research, a WWII machine, so it waits for a computer block's field and the designs' copies refreshed.
  - *Numbers* (fc-history `--runs 24`): 13 of 23 in band (15 at F6b). The rate spot lifts the British: the Jutland
    BCS 2.7 to 3.2% (over the band's 2.8), the Grand Fleet BS 6.7 to 7.0%, the 3rd BCS 14.3 to 14.7%, the Falklands 2.3
    to 2.5%; the German cases within their noise or lower (the battleships 5.8 to 4.8%), so the Germans over the British
    fall to 0.98 (want 1.5-5): the record's British losses are in what the sim doesn't have yet (smoke, the British
    silhouetted, splash confusion), not in their machine. A steady target over a salvo chaser 2.75 to 3.20 (in).
    Squadrons over 20 seeds: Britain's hits 29 to 42, Germany's 63 to 78, losses as before. Tsushima over 5 seeds:
    Russia's hits 343 to 279, Japan's 427 to 505, losses 4.2 Russian and 3.0 to 2.6 Japanese ships.
  - *Tests:* a rate spot of 1 m/s moves every kind of computer's prediction a minute on by 60 m along the line of fire.
- **F6d done 2026-10-18** (the rate of fire from its parts, independent fire).
  - *The supply:* a mount fires from its ready-use rounds (shipgen's `ReadyRounds`: a 4" 26 a gun, a 6" 15, a 12" 6, a
    15" 4) at the reload, and once they're gone as the hoists bring them, a round a gun every `MountRow.SupplyS`, the
    ready-use rounds filling back at that rate while it fires slower (`CombatStore.Fired`). The hoists' interval is the
    reload times `DesignerPlaceholders.HoistShare`, 1.5 [EST from the Majestics' 70 and 100 s and HMS Delhi's 25 and 15
    rounds a minute]. A Queen Elizabeth turret fires 12 salvos at its 39.6 s reload, then one each 59.4 s.
  - *The crew:* the battle's factor on the drill reload (`GunneryCrew.Load`: green 1.5, average 1.35, trained 1.2, elite
    1.1 [EST]: the loaders' fatigue, heat and fumes).
  - *The layers:* after a spot's correction the next salvo waits `LayerSettleS`, 7 s, for the layers to lay on again
    (New York's 10 s at the director, the USN's secondaries every 7-10 s); salvos at unchanged settings go as the guns
    load (Bismarck's ladder 2 s apart). A first try spaced every director's salvos 7 s and broke the German ladder.
  - *Independent fire:* inside a discipline's `IndependentWithinM` a quick-firing battery (reload `QuickFirerReloadS`,
    16 s, or less) fires each mount as it loads and lays, each fall spotted on its own, with no wait for the spots and no
    battery-wide settle (each layer his own); salvo fire beyond. The preset **independent**: quick-firers independent
    within 5.5 km (the pre-dreadnoughts' shift at 5-6 km), the heavy guns by single salvos and full salvos in fire for
    effect; salvo firing came in 1905-08 (manual-gunnery-research.md §5), so fc-history's Santiago, Yellow Sea and
    Tsushima cases and the Tsushima scenario's sides take it. One ship alone, Mikasa's 6" at 4.6 km: independent 4.1%,
    salvos 5.9% (the research's table 7 has them about even: here each gun's lone fall is spotted as a salvo of one, and
    whose splash it is, the cost the research gives independent fire, is F7's).
  - *Rates* (rounds a gun a minute from the first salvo, against the drill's; the duel's new line): Queen Elizabeth's
    15" at 16 kyd 79% over 15 minutes and 71% over an hour (1.08 a minute: her 100 rounds a gun last about 1.5 hours of
    unbroken fire, 66 minutes before F6); Seydlitz's 11" on the ladder 73%; Mikasa's 6" 57% in salvos, 77% independent;
    Lion's 4" in local control 32% (halves every 4.6 s held by the layers' settle). The research's battle rates, 30-60% of
    drill, include what a duel doesn't have (sight lost, smoke, turns, shifts of target). **Left:** the doctrine's ceiling
    (deliberate fire at long range, rapid close in) and the crew's fatigue over the hour: no numbers yet (the research
    request on fire discipline asks).
  - *Numbers* (fc-history `--runs 24`): 12 of 23 (13 at F6c). The pre-1905 cases on the new preset: the Yellow Sea 8.3
    to 11.1% (over its band: full salvos in fire for effect and no rate spot), Tsushima's 12" 8.4 to 10.1%, its 6" 3.7 to
    2.8%; the Great War cases within their noise (Dogger Bank's British 2.5 to 2.1%, the Jutland BCS 3.2 to 3.7%, the
    Grand Fleet BS 7.0 to 8.0%). Squadrons over 20 seeds: Britain's hits 42 to 41, Germany's 78 to 60, losses 1.2 to 0.8
    British ships and 0.1 German. Tsushima over 5 seeds: Russia's hits 279 to 270, Japan's 505 to 454, losses 4.2 to 4.6
    Russian and 2.6 Japanese ships.
  - *Tests:* a turret fires its ready rounds at the reload, then at the hoists' interval. `FlotillaAttackTests` takes
    24 seeds (12): with the new rate seeds 1-12 had 2 of 4 attacks with two boats on the signal, seeds 1-48 22 of 27.
  - *Cost:* 26-27 µs a battery-second.
- **F6e (ammunition as a choice): moved to F10** (the user, 2026-10-18: "it suits there"). Its floor reads the
  battery's own estimate of its hits a shell (decision 8), which is F10's `FireEstimate`; a stand-in here would be a
  second implementation of it. F6 is complete without it.
- **Short range: a bug hunt (2026-10-18).** The user, playing Tsushima, saw Russian cruisers charge the Japanese line
  and torpedo it, missed again and again at what was nearly point blank, and a gun fire the other way. Benched as a
  cruiser (Aurora) charging Mikasa, head on, on a diagonal, zigzagging in and crossing close by. What was wrong:
  - *The bracket was a correction to a solution that moved under it.* A reading that jumped the computer's solution
    (an eye's range, ±20% once a minute) left the spots' corrections where they were: at 965 m the computer was about
    right and the correction in force -1,375 m, so the salvo was laid 400 m behind the ship, and the gun swung round and
    fired the other way. Now each reading's (and each judgement's new look's) move of the solution in range moves the
    bracket and the correction in force back (`Rebase`), and a salvo in the air is spotted against what its correction
    means now. And the solution stands while it lies inside the bracket (`Bracket.Aim`): the spots take over only when
    they say it's wrong, so a computer that takes a good range in isn't held to old corrections.
  - *The line was the computer's.* A battery laid its present bearing from its track, a reading's latency stale (20 s
    for the eye): on the diagonal 100-230 m off in deflection at 1.5-2.5 km. The layers laid their own line on the
    target through their sights, the computer giving the sights the lead (speed across) and the range. Now the line is
    the target as the layers see her now, with the computer's lead over the time of flight (`Aim`); a battery that sees
    nothing lays where its computer says.
  - *Point blank* (F7's item, taken in here): where the danger space of her side (her freeboard over the fall's angle,
    at her present range) covers twice the error of laying at her present range unspotted (the battery's first
    instrument or the layer's eye, and the computer's residual ballistic bias), the guns lay at that present range on
    the middle of her side, fire for effect at once, and their falls don't move the bracket. Mikasa's 12" with the FA
    are point blank on Aurora to about 3 km, her 6" by eye to about 1 km.
  - *The eye was untrained.* `EyeRangeShare` was 0.20, the research's untrained number, given to a trained crew (the
    crews' Operator multiplies it, green ×2): now 0.125 (§3.1: "10-15% practised").
  - Two small ones on the way: a ranging salvo short of its guns at the instant (one turret off its order by the
    tolerance) waits for the next second rather than going with half; and the duel prints hits by the range they were
    fired at.
  - *Numbers,* Aurora charging a stopped Mikasa at 20 kn (24 runs; hits by the range fired at, before and after):
    head on, 12" under 1 km 27 to 51%, 6" 30 to 44%; zigzagging in, 12" under 1 km 17 to 60%, 6" 9 to 43%, 6" at 1-2 km 1
    to 22%; on a diagonal, 12" at 1-2 km 31 to 48%, 6" 3.5 to 19%; crossing at 1.5 km at 22 kn, 12" 12 to 47%, 6" 2.3 to
    16%. **Left:** the 6" in local control still track with a plain rate clock fed by the eye, which runs a closing rate
    past the closest approach; the judgement (preset 9) does better crossing (21% against 12.5%) and the same elsewhere,
    a choice of local control's computer for the user.
  - *The record* (fc-history `--runs 24`): 8 of 23 in band (12 at F6d). Better laying lifts nearly every case, several
    just over their bands' tops (the Jutland BCS 3.1%, top 2.8; the Grand Fleet BS 8.3%, 7.2; the German battleships
    6.1%, 6.0; Denmark Strait 11.6%, 11; Tsushima's 6" 4.8%, 4; the light guns at a fast boat 3.2%, 2), the record's
    losses being in what's still missing (smoke, the sea, splash confusion: F7 and the ship-motion pass). Squadrons
    over 20 seeds: Britain's hits 41 to 54, Germany's 60 to 75, losses 0.8 to 1.1 British ships and 0.1 German. Tsushima
    over 5 seeds: Russia's hits 270 to 465 and losses 4.6 to 5.6 ships, Japan's 454 to 749 and 2.6 to 3.6.
  - *Cost:* 31 µs a battery-second (27): the line and the point-blank test each second, two predictions a reading.
  - *A follow-up the same day* (the user asked how anything could miss inside 1 km): the "0-1 km" band above was the
    range the guns were laid at, the computer's, not hers: in the crossing she never came inside 1.2 km. The duel now
    bins by her true range, and the trace marks a salvo laid point blank (P) and gives her true range. And the eye's
    0.125 hadn't taken: `Content/Sim/settings.json` held 0.2 and overrode the code's default, so every number above was
    at 0.2. Now set there. fc-history 8 of 23 still (Santiago 17.0 to 20.3%, Tsushima's 6" 4.8 to 4.9%); Tsushima over 5
    seeds: Russia's losses 5.6 to 5.4 ships, Japan's 3.6 to 3.8. Inside 1 km by her true range: the steady target at
    800 m, Mikasa's 12" 64.5% and 6" 49.5%; head on 56% and 53%; a close crossing (closest approach ~500 m) 69% and 60%.
    **What's left there is the layer's elevation error,** not prediction: Mikasa's 6", with no fit of their own, lay as
    the eyeball's open sights, 3 mil, ×1.5 in battle, 3.6 m at 800 m against Aurora's 3.4 m side, so half the shells
    pass over or fall short; and point blank doesn't take them (her danger space 291 m against twice the eye's 200 m).
    The catalogue's sights carry the reference's levels' 3, 2.5 and 2 mil, where the gunlayer study
    (manual-gunnery-research.md table 1, moderate sea) gives the whole elevation error as 3.7 mil firing on the roll
    with open sights but 1.2 mil for continuous aim with an early telescope (Tsushima's) and 0.8 with a good one: a
    calibration of the sights, and of what a battery with no fit lays with, for the user.
  - *The sights* (the user, 2026-10-18: "go ahead", the units kept consistent, a field in the design rather than a
    year deduced). The sight blocks a gun lays by itself now carry the gunlayer study's whole elevation error a shot
    (manual-gunnery-research.md table 1, a moderate sea, less the gun's own 2′ its pattern has, for a trained layer, in
    mil): open sights fired on the roll 3.1, a telescope fired on the roll 2.0 (new: heavy guns, continuous aim working
    only to about 9.2″), an early telescope with continuous aim 0.85, a good one 0.55 (3, 2.5 and 2 before: the
    reference's levels' numbers in mil beside notes in arcminutes). Every angle a block gives is in mil but the optics'
    acuity, in arcseconds. A battery names its guns' own sights in `fire_control.BATTERY.sights` (open sights if not
    given): what they lay by in local control, or with no director at all, where they took the eyeball's open sights.
    `shipgen fit --main-sights NAME` sets it; the shipped designs name theirs by gun and period (open sights before
    continuous aim, heavy guns on the roll, medium guns an early telescope in 1899-1905 and a good one after), and a fit
    whose mount slot held a sight block by its old level takes the same. Goldens moved (the design echo), ShipDesign.Version
    38.
  - *Numbers:* inside 1 km by her true range, Mikasa's 6" on Aurora at 800 m steady 81% (49.5% before), on the close
    crossing 80% (60%), head on 65% (53%); her 12" (on the roll) 64-74%. fc-history 8 of 23 (the pre-dreadnought cases
    climb further over their bands: Tsushima's 6" 6.9%, the Yellow Sea 14.3%; director over independent comes in at
    1.13). The user, 2026-10-18: guns this accurate are right for the case the sim has, a flat sea in clear air; mist and
    waves are to come. Tests: `FlotillaAttackTests`' replay takes the first of 24 seeds whose boats get their torpedoes
    away (seed 7 no longer did, the secondaries hitting close in); the weapon facts test reads a local battery's sights.
  - *Walking the fall* (the user, 2026-10-18: "a gunlayer is not going to always fire randomly within errors, they are
    also walking the shots and if one hits they keep firing at that point"). Point blank, the sight's setting error is
    drawn once as she comes into the band and kept, and the layers walk their own falls onto her: they read how far over
    or short and off the line each fell (k·R²/h from their guns' height: a few metres at 800 m), hold the lay on a hit,
    and on a miss take half of it off, as magnitude spots go (a dead band of half a pattern, so the gun's own scatter
    isn't chased). The walked correction is a share of her range (the errors it takes off are), so one walked in at 2 km
    isn't four times too big at 500 m. And the band also opens where the layers read their own falls finely enough to
    walk them (their spotting error under a quarter of her danger space), not only where the danger space covers the
    error outright: the 6" by eye are point blank to about 2 km on a cruiser. Mikasa's 6" on Aurora, by her true range:
    steady at 800 m 88% (81% before the walk), 1.5 km 60%, 2.5 km 35%; head on under 1 km 71% (65%); a close crossing 90%
    (80%); zigzagging in 62% under 1 km and 36% at 1-2 km. Her 12" (a telescope on the roll, 3 mil in battle) 61-74%
    under 1 km. fc-history unchanged (8 of 23: nothing in it is that close). Tsushima over 5 seeds: Russia loses 4.8 ships
    and Japan 3.8. A test: Mikasa's 6" at 800 m lay point blank and hit with 83% of their shells.
  - *No teleporting* (the user, 2026-10-18: "Sir, the target teleported 200 meters!" — the officer carries on hitting;
    spotting walks out a steady error, and doesn't work against a large uncorrelated one). After a salvo that hit, the
    next one's laid point (the aim with its correction, against where she went: the duel's new `laid` column) moved
    67 m on average and up to 706 m (Mikasa's 6" at 3 km), 192 m (Lion at 15 km), 184 m (Queen Elizabeth at 14 km).
    Four changes:
    - *The eye's error wanders, it isn't redrawn* (`EyeRangeShare` 0.075, `EyeRangeBiasShare` 0.10, `EyeWanderTauS`
      300 s [EST]): the research's 12.5% for one practised estimate split into a man's own bias, the same every time, and
      a wander about it that changes over minutes (√(0.10² + 0.075²) = 12.5%; battle and the crew multiply the wander).
      It was a fresh 19% each minute on top of the 10% bias. A rangefinder's error stays fresh each reading (the
      instrument's setting).
    - *The spots lay the guns while they hold her* (`SpotsHoldS`, 120 s after a straddle or a hit [EST]): the bracket's
      aim, its computer's steps taken back off it (Rebase), so a new range never moves the guns; the "solution stands
      inside the bracket" rule only when the spots are stale.
    - *A lone over or short among straddles holds the aim*: firing for effect, a spot moves the bracket only when the one
      before was the same way (the spots of the salvos in the air arrived between two salvos and each moved the aim
      half a pattern, chasing the scatter: the research's full corrections chasing the random salvo error).
    - *A plausibility gate* (`ReadingGateSigmas`, 3): a reading more than 3 sigma off the computer's own prediction for
      its time is set aside, unless the next is too (then she really moved, or the old picture was wrong). And point
      blank comes in at its test and goes out only past a margin, a layer back in within a minute keeping his setting.
    After: 18 m on average at 3 km (13 of 219 over 100 m, the corrections of two spots the same way), 5 m at 1.5 km,
    14 m at 15 km (Lion), 38 m at 14 km (Queen Elizabeth). Hit rates rise where the noise was chased: fc-history **11
    of 23** (8): the Grand Fleet BS 8.4 to 6.4%, the German battleships 6.1 to 5.5%, Denmark Strait 11.5 to 7.1% come into
    their bands; director about even with independent 1.05, heavy guns over the 6" 2.04 (both in). Close in, Mikasa's 6"
    on Aurora: a close crossing 93%, head on under 1 km 73%, on a diagonal at 1-2 km 47%, steady at 3 km 26%. Squadrons
    over 20 seeds: Britain's hits 48 to 63, Germany's 81 to 97, losses as before. Tsushima over 5 seeds: Russia's hits
    921 and losses 5.6 ships, Japan's 1,286 and 3.8. Cost 32 µs a battery-second. Tests: after a hit the lay moves 19 m on
    average at 3 km and 10 m at 15 km (`After_a_hit_the_guns_hold_their_lay`); the fall-out test's enemy holds her fire
    (a hit on the second ship put her out too, under its any-damage doctrine).
  - *The rangetaker's error wanders too* (the user, 2026-10-18: "given a single operator and target at constant range
    they are not going to report wildly different errors per reading"; a fresh error each reading would be a different
    man at each, and the same man reads all through a fight). A rangefinder's reading now takes the eye's model: its
    bias (`RangefinderBiasUnits` 1.5 units, as before) and a wander about it with its own time constant
    (`RangefinderWanderTauS` 300 s [EST]), the wander's size the fresh error's before (crew × combat × unit: 1.5 units
    trained), so one reading's error is unchanged and only its correlation in time is new. The track no longer
    averages it away; the spots walk it out. Before and after on one build (the old behaviour is the time constant at
    zero, the same draws): fc-history **11 of 23** (10): the British BCs at Jutland 3.4 to 2.2% and at Dogger Bank 3.1
    to 2.1% come into their bands (Lion's first hit on salvo 20, later than the record's ~10), the German 1st SG 3.0
    to 3.7% and the German battleships 5.3 to 6.8% (now over their band), so the Germans over the British go 0.88 to
    1.64 (in); the Grand Fleet's range over the BCs' 1.59 to 2.93; Tsushima over the Yellow Sea 0.91 to 1.01 (still
    low). Duels: Lion at 15 km 4.7 to 4.5% (the track's aim error 167 to 215 m at 2-4 min, the first hit 222 to 343 s);
    super_dreadnought at 18 km on fit 4 2.3 to 3.8% (its fresh error kept moving the solution). The user, 2026-10-18:
    hit rates over the record are fine here, the sim being the ideal case (fresh crews, no sea, brilliant visibility);
    mist, rain and sea will bring them down.
  - *Coincidence and stereo differ* (the user, 2026-10-18: "having stereo sets work a bit differently from the
    coincidence ones is an interesting gameplay mechanic and thus makes the choice actually matter"; good enough for
    the first release, a research-backed refinement later). A coincidence set's halves are brought into line afresh each
    reading (a vernier setting the eye scatters on, the image shaking as it's caught), so half its man's error (of the
    sigma, `RangefinderFreshCoincidence` 0.5 [EST]) is fresh again and the wander √0.75 of it; a stereo set's fused
    picture holds his error (`RangefinderFreshStereo` 0: all wander, unchanged). One reading's error is the same for
    both; the coincidence set's readings scatter, the stereo set's drift. fc-history 11 of 23 (the stereo cases
    unchanged to the shell): the British BCs at Jutland 2.2 to 2.5%, the Grand Fleet 6.6 to 5.8%, Falklands 3.1 to
    1.9%; the Germans over the British 1.64 to 1.46 (just under the band's 1.5). Lion at 15 km 4.5% as before, the
    track's aim error 215 to 184 m at 2-4 min and the first hit back to 222 s (salvo 3).
  - *The bearing's maths checked* (the user, 2026-10-18: "a slight hunch that bearing error in the prediction or
    elsewhere is a bit wonky in some cases"). Checked and right: a reading's fix and its covariance (`Track.Reading`:
    the range's variance along the line of sight, the bearing's times the range across it, R12 = (along - across)·sin·cos),
    the true-course track's Kalman update, the rate clock (its start's range and bearing rates from the relative
    velocity, the bearing's two-state filter on q/R², the helm-free re-reference, `ClockPoint`'s velocity), the
    coordinated turn and the Singer model, the gate, and the deflection's sign from spot to lay. How a bearing's error
    reaches the guns: while the battery sees her, the layers lay on her as she is and the computer gives the lead, so a
    reading's bearing error only acts through the track's velocity. Fixed:
    - *The lead was straight* (`Aim`): her present place plus the track's velocity now times the time of flight, while
      the range took the track's own prediction; a turn filter's arc was lost from the line (½·v·ω·T², 70 m at 14 km on
      a 1°/s circle, the trace's across error 65-76 m). The lead is now the track's own motion over the flight (where it
      has her at the fall less where it has her now). Duels at 14 km, 20 runs: on a circle the turn-setting table 2.6 to
      3.1%, the digital filter 4.6 to 5.2%; the straight-line trackers as before.
    - *The deflection spot's dead band* was a quarter of the range's pattern, 2.5 times the line's own (40 m at 15 km,
      an end-on target's beam): now a quarter of the pattern across. Over 100 runs it makes no difference (Lion 4.4
      against 4.6%, a stern chase 1.9 against 1.8%, head on 3.1 against 3.2%): the line isn't the limiter.
    fc-history 11 of 23 both before and after these two (the British 1st/2nd BCS 2.5 to 3.0%, just over their band; the
    German battleships 6.8 to 5.8%, now in; Komandorski's control 1.2 to 0.9%).
    Left as they are, for the user: a bearing's error is fresh each reading (the range's now wanders) and isn't
    multiplied by the crew or by battle as the range's is; the deflection correction is metres, not mils, and isn't
    rebased when the solution's lead moves (the range's is); the straddle the report counts takes the target's length
    across the line whatever her aspect; a spot over or short is taken from a fall however far off the line.
  - *The line in mils, and line first* (the user, 2026-10-18: "deflection correction should be in mils ... it
    naturally should be very small but we want the behaviour to track"; "let's also correct line first"). The line's
    correction is an angle now (the spot's metres over the salvo's range; laid as that times the range), so one learnt
    at 15 km is right at 10. Line first: a splash is judged over or short only in line with her, against her hull
    (hidden behind it or in front of it), within her width across the line of sight (her length and beam by her aspect)
    and `SpotLineMarginM` (25 m [EST]: a heavy splash's column); a salvo with none in line is doubtful (a new spot,
    `Sign.Doubtful`): the bracket, the phase and the rate spot are left alone, and only its line is corrected. Doubtful
    spots in the traces at 14 km: 1 in 28 on a broadside target, 2 in 14 on one going away, 1 in 17 at 45°. fc-history
    11 of 23 (the moves within its noise: Dogger Bank's British 2.6 to 2.1%, the Grand Fleet 5.6 to 6.7%, the Germans
    over the British 1.32 to 1.15). Squadrons over 20 seeds, line first against none (the same build, the margin
    unbounded): Britain's hits 60 and 60, Germany's 86 and 96.
  - *A bug found on the way: Fight never closed on a runner.* The captain's Fight turned a fixed 40° in from the beam
    whatever the enemy did: at 22.5 kn on a 12 kn runner that closed 0.3 kn, and `Fight_closes_to_its_band` (14.0 km
    against a limit of 15.6) ended at 18.6 km once the falls moved. Beyond the band he now turns in further when the
    angle wouldn't close by `FightMinCloseKn` (3 kn [EST]) at the speed she makes, as far as a stern chase. Squadrons
    over 10 seeds: Britain's hits 54 to 79 and Germany's 60 to 158, Germany losing 0.3 ships (0), two seeds ending
    quiet (none).
  - *A bug found on the way* (committed alone): a row of light AA mounts along each beam blinded the directors on
    their deck, so Bismarck's 150 mm never took a reading and never fired; light AA no longer blinds a post (45 classes'
    eyes move, the WWI scenarios' hashes don't).
  - *Tests:* `FireDisciplineTests`: the bracket takes spots in any order (the 400-200-100 by halving, an older spot
    changes nothing, the floor, a spot against a bound, the straddle, the widening); Bismarck's 150 mm range with
    partial salvos 24 s apart (the flight and the spot, over twice the 11 s reload) and then fire for effect at the
    reload.

### F7 Spotting by the navy's rules

- **Spotting:** the spotter is the battery's highest working eye that sees the fall (a top now, F4), `σ = k·R²/h` with k
  tuned against NavPers' "500 yd obvious at 12 kyd, barely at 19 kyd from 120 ft" once the heights are real.
- **What a spot says:** by eye over, short or straddling in range and the deflection to a mil (F6's bracket takes
  them); a measured miss (radar, later aircraft) as a magnitude, corrected at half gain with a dead band of half a
  pattern (full corrections chase the random salvo error, §9).
- **Whose splash:** from the shell system's splashes near the target within a few seconds of one's own, by calibre:
  identification 1/(1 + 0.6·N), dyed shells lower; a salvo of 3 or more is identified (`manual-gunnery-research.md`
  §5). The eyeball ship loses the advantage decision 9 gave it.
- **Point blank:** inside the range where the danger space exceeds the range error (`emergency-eyeball.md` §6: heavy
  guns under ~4-5 km, the 5"/38 under ~2.5-3 km), no ranging: lay by sight on the waterline and fire for effect.
  **Done early, in F6's short-range bug hunt (2026-10-18)**, with the line by sight at every range.
- **Anchors:** the 1912 Thunderer-Orion trial (director ~4.5x the hits of independent laying in poor visibility, about
  even in clear weather); optical spotting falling off beyond ~18 kyd; Santiago and Tsushima for the pre-dreadnoughts
  (`early-hit-rates.md`).

### F8 Damage reaches fire control

The research's hooks (§13.2), on the parts F4 made: a director lost hands the unit to the next control with that
position's height and instruments; the transmitting station lost drops the computer to a hand plot (`polar`, the eye's
prior, a long latency); fire-control circuits cut put the turrets in local control; a top hit lowers the spotter; a
turret's rangefinder lost leaves that turret in local control without a range. E4's timed outages (gyros, rangefinders
out of adjustment, power) map onto the new rows.

### F9 Radar (moved out: a later pass, decision 2)

The first game is set in 1890-1925, so radar waits for the cut that needs it. Kept here as that pass's list: radar as a
sensor (§3.6, designer §3.2), its envelope from the set's physical numbers, capped by the 4/3-earth horizon and scaled
by the target's size to the ¼; range-only sets needing an optical bearing, so no solution at night unless the target is
lit; splash spotting by range or both within its reach; fragility (its own guns' blast, its aerial hit). F3's fits take
a `radar` block without a migration. Night for optics alone (the target lit or not, the error multiplied) is F5's.

### F10 What the captain knows: estimated hit chances (scaffolding)

The captains and admirals today know whom they can shoot (`Matchup`, by a level's fixed MPI table) but not how well
it's going, or how well it would go somewhere else. This step gives them the contract and a first model, and uses it
only where this plan needs it (the ammunition floor below, F6's item 7, moved here from F6e); the tactics that read it
are the AI's passes (`command.md`).

- **One estimate, `FireEstimate`** (`Sim.Combat`, pure, allocation-free): from a battery's guns and rate, its fire
  control's error budget, the gun's dispersion, the range table's fall, and the target's hitting space (danger space
  plus presented depth, presented width: §8.1), the per-shell hit chance by §8.3's closed form, the chance of at least
  one hit a salvo, expected hits a minute, and the time to the first straddle still to come. The error budget comes
  from the battery's own state: while ranging, the bracket's width and the salvos still needed; in fire for effect, the
  salvo-to-salvo error, the residual drift and the track's rate error over the time of flight. One implementation:
  the matchup's guns measure becomes a call of it, and coarse fire (4.9) will be.
- **Two ways to ask it:**
  - **now:** the gunnery system fills each engaged battery's estimate every fire-control tick, beside its phase, and the
    AI reads it as it reads `CombatStore.Target` today;
  - **what if:** the same function for a range, an aspect and a state that aren't the present one (settled, ranging
    afresh, own ship turning), so a captain can ask what closing to 12 km would buy, or what turning away costs his own
    fire. The matchup's `MatchupMpiPct` by level goes: its profile comes from the class's own rows.
- **Only what the side knows:** the target's size and aspect from the picture (the class it's taken for, by type until
  it's made out), never the truth; its own fire control's error as its computer believes it, which can be wrong (a
  misread straddle shows as confidence).
- **The other way round, under fire:** the same estimate for the enemy's batteries as the picture holds them (the class
  taken for and its fit), and the evidence at own ship: splashes from the shell system within a pattern of her, enemy
  straddles seen, hits taken, by shooter where it's known. A captain gets "under effective fire from X" as a fact, so the
  evasion doctrine (today's zigzag, the chase later) and Fight's turn-away can read it instead of counting guns.
- **Ammunition as a choice** (F6 item 7, from F6e; decision 8): the doctrine holds a battery's fire, unless it's
  ordered, while its own estimate of its hits per shell is under a floor (a side's doctrine field, by the target's
  worth later), and keeps a reserve of the magazine; firing on regardless is an order (`FireOrder.Engage` is one,
  fire at will is doctrine; the secondaries at the edge of their reach, `fighting.md` 4.2c's "left as it is"). The
  battery shows it holds (a state beside its phase), and takes the target up again when the estimate clears the floor
  (closing range, a settled track). The reserve's share has no source yet (the research request on fire discipline).
  Anchors: a battle line's magazines last the hours of a Jutland, not 1.5 hours of unbroken fire (F6d's rate);
  `command.md` 5.1g's Tsushima, where both sides' battleships emptied their main magazines in 1.5-2 hours.
- **What it enables, later and not here:** the range band a ship fights at from the hits each side gets there; target
  choice by expected effect per shell; when to open fire; turning away when out-hit; an admiral's fire distribution by
  value rather than counting from the van.
- **Tests:** the estimator calibrated against itself: over `duel` runs across levels, ranges and target policies, the
  estimated hits per salvo against the hits that came, within a band, and the estimate's ordering always right; the
  matchup's numbers before and after (reported); no allocation; its cost a battery-second; a battery holds fire under
  the floor at will and fires on an order; rounds left after a Jutland-length play against the record.

### F11 Calibration

The whole chain against the yardstick (`fc-history`), the first game's cases first: Santiago, the Yellow Sea, Tsushima,
the 1912 trial, Dogger Bank, the Falklands, Jutland by squadron; then the later ones as checks. Kept tests with the
yardstick's bands: the cases' ordering (Tsushima over the Yellow Sea, the Germans over the British at Jutland, a
steady target over an evading one by 3-10x at long range), director fire over independent laying, local control at
60-70% of director fire by day. The record's combat factor (1.5) stays one number;
tuning for gameplay waits for the balance pass, as the damage plan's did.

### F12 The designer's Fire control tab

Mockup, then port, as the other tabs (`docs/designer-ui/`). The fit per battery from the catalogue, instruments
placed on the plan and profile, mast and top heights (a knob each, the user 2026-10-18; today's defaults are
MastPlan's rule), and the sizing rule as a live readout ("unit of error at 15 kyd 62
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
- **Radar** (F9, decision 2): the cut after 1925.
