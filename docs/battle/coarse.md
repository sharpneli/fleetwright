# Plan: fighting in the coarse tier
Status: **agreed 2026-10-26** (drafted and answered the same day: "Decided" at the end). J0 (the bench) done
2026-10-26; J1 (the step mode) done 2026-10-27; J2 (guns in coarse) done 2026-10-27 (the speed it makes taken as it is, decision 3); J3 (torpedoes) done 2026-10-27; J4 (dodging) done 2026-10-27; J5 (sighting and smoke) done 2026-10-27, with open fails for the user; J6 (damage at speed) done 2026-10-27; J7 (cheaper fire
control: its (a), the 3 s grid) done 2026-10-27; J8 (the process) done 2026-10-27: **the plan is complete.** The full panel
runs when the coarse tier is released to players; the balancing pass comes later (the user). It takes over `fighting.md` 4.8
(damage in the coarse tier) and 4.9 (coarse fire), and the parts of `plan.md` 2.8 that touch fighting. Those sections
only point here.

The user, 2026-10-26: "Fine tier is the gold standard, coarse tier damage calcs approximate it." The fine tier flat out
makes ~300-470x in the Jutland (`bugs.md`, kept as it is). Real speed in a fleet action has to come from the coarse tier.
This plan covers three things: fighting at speed, a way to watch it a minute at a time (Rule the Waves style), and the
process that keeps coarse matching fine as the fine tier changes.

This brings decision 17 forward (`fighting.md`: the coarse tier after all the fine-tune passes). Fine-tune passes are
still to come, so the process below is what makes building coarse now safe: each later fine pass reaches coarse by
itself, or trips a test that says what to rerun.

## Where it stands

What the coarse tier does in a fight today (code read 2026-10-26):

| System | Coarse today |
|---|---|
| Motion | Kinematic segments (`CoarseMotion`): exact arcs and constant accelerations. The accelerations come from the class's own fine trials, run at load; the turning radius is the fine `Handling`'s. No helm lag, no swing, no speed lost in turns (2.8, "Carried on"). |
| Fire control and firing | Silent. `GunnerySystem.Start` and `Fire` skip any ship not on the fine tier (`BatteryState.Silent`). |
| Shells in flight, the hit query, terminal, damage | Event-shaped already (window, water and end events per shell; the tracer at the window), but no shells are fired. |
| Flooding, fire, damage control | The 1 s damage step runs on wet ships in any tier (4.3b). |
| Torpedoes in flight | Event-shaped analytic tracks; they run in any tier. |
| Captains' fighting | `FallOut`, `OutOfRounds`, `Comb`, `TakeUpStanding` and `Aim` (torpedoes) run only for fine ships. Coarse captains think once a step. |
| Evasion | Doctrine zigzag, 75 s legs (`EvadeLegS`), steered by the captain's thinks. |
| Sighting | A look over the step, once a minute (1.5-3.7 ms a look in the Jutland). |
| Smoke | The field ticks every 60 s in coarse (`SmokeCoarseTickMs`) against 1 s fine. |
| Headless play | `scenario --play` drops to 1x whenever an encounter is open, because guns are silent in coarse. |

**The key fact:** fine fire control doesn't run on the 50 ms ship tick. It measures, tracks and solves on a 1 s grid
(`FireControlMs`). Salvos, spots and each shell's window are scheduled events. A coarse ship's pose is analytic at any
instant (`PoseAt`), as a fine ship's is between ticks. So the fine chain can probably fire from and at coarse ships as
it stands, without tables. At the Jutland's cost split (`docs/profiling.md` 2026-10-26: gunnery 6% of ~2-3.5 ms a game
second, damage about the same), it would cost ~0.2 ms a game second in a big fight. That's a ceiling near 5,000x on its
own, and probably 2,000-4,000x with the coarse world around it. That is to be measured (J2), not trusted.

## The principle: how coarse approximates fine

Each piece of the coarse tier sits on one rung of this ladder. **The rung decides how the piece keeps matching fine.**
Take the highest rung that meets the speed target.

1. **The same code on the coarse clock.** The fine model runs as it is, on coarse ships. Nothing to keep in step: a
   change to fine is a change to coarse. This rung holds the hit query (the one tracer), terminal, damage, flooding,
   fire, damage control, magazines and torpedoes in flight.
2. **The same code at a coarser cadence.** The fine model with a longer step: motion's minute segments, the AI's
   minute thinks, the minute look, the smoke's minute tick, and maybe fire control's grid. Drift is only what the cadence
   costs. The parity bench (below) watches it.
3. **Fitted from fine by a bench.** A cheaper model whose numbers come only from running the fine chain
   (`shipgen coarse-fit`), stored as content, stamped with the fine chain's fingerprint. A kept test fails when the fine
   chain no longer gives that fingerprint and names the command to run. This is the agreed 4.9 design (tables from the
   fine fire). It's used only where rungs 1-2 measured too slow.
4. **Never: hand-set coarse numbers.** No knob that makes coarse "feel right" on its own. A coarse result that's off is
   fixed by moving up a rung or refitting, never by tuning coarse against itself.

The coarse motion's accelerations already work this way (rung 3 fitted at load: each class's fine trials run once),
and that's why they never drifted.

**On damage** (decision 1): a hit isn't approximated. A hit in coarse goes through the same terminal and damage model
as a fine one (rung 1), as 4.9 always said ("damage is the same kind in either tier"). What the user meant by "damage
calcs approximate it" is **damage over time**: the hits a ship takes in aggregate. Coarse lacks fine's precise ability
to dodge shots, most of all for small, agile ships. So that's what coarse approximates, and what the parity bench
watches hardest: how often hits come and when, which comes from motion, the captains' cadence and, only if needed,
fire's statistics.

**Dodging is where coarse is weakest.** In fine, a ship spoils the enemy's solution with the track it steers: a
destroyer's zigzag of short legs, its attack run's turns, its tight circle and quick swing. A coarse captain thinks once
a minute, and coarse turns are instant arcs with no helm lag or swing. The fine fire chain shoots at whatever track the
target actually steers, so coarse dodges as well as its track matches the fine one: no better, no worse. The plan's
answer is in that order: make the coarse track match (evasion laid ahead as a route, J4; the helm lag and swing only if
needed), measure it per size and agility of target (the panel's dodging layer), and fit a correction (rung 3) only if
the track can't be made to match.

## The parity bench: the process

Coarse's yardstick is the fine tier, never history (the user, 2026-10-26). History stays fine's yardstick
(`history-is-the-yardstick`).

**`shipgen coarse-parity`** runs a fixed panel both ways, many seeds each, and prints fine against coarse with the
bands below. The panel has five layers, cheapest first:

- **Duels** (`FcBench` at the tier's speed, 300 runs each): one battery on a scripted target. Battleship on battleship
  at 16 kyd (steady, zigzagging, turning, own ship turning) and at 7 km in haze; secondaries on a destroyer (steady and
  zigzagging); a light cruiser on a zigzagging destroyer; a main battery on a zigzagging torpedo boat. Compared: hits a
  gun a minute, rounds a gun a minute, first straddle and first hit after the track. Damage off. This isolates fire from
  the AI.
- **Dodging** (decision 1): played scenarios where the AI's own captains steer the targets. `flotilla` is six torpedo
  boats on an attack run (an opening `"attack"` task) into four dreadnoughts' secondaries. `running-fight` is a light
  cruiser and two large destroyers running and zigzagging from two dreadnoughts. Compared: the hit % on each target
  class (a band each, so a destroyer's drift can't hide in a battleship's average), and the ships that get their
  torpedoes off.
- **Fights to the end**: duels with damage on (300 runs: a battleship on a light cruiser, a light cruiser on a
  zigzagging destroyer), compared on the share out of the fight by the default doctrine's rule, the share going down,
  the minutes to out of the fight and the hits; and `squadrons` played.
- **Scenarios** played to the end (wave 1, Åland and Tsushima; with `--panel full` also Heligoland Bight, Dogger Bank
  and the Jutland). Each side's ships lost, tonnage share lost, shell hits made, hit %, ships firing torpedoes and
  torpedo hits; the exchange (one side's share of the tonnage lost); how often it's decided, and when it ends. This
  layer includes the AI at a minute's cadence.
- **The night** (added 2026-10-30, `night.md` Q11): the night-history bench's scenarios (`night-tsushima`,
  `night-southampton`, `night-4th-flotilla`, `night-black-prince`, `night-12th-flotilla`; the Dover Strait is in the
  scenarios layer), with the scenarios' measures and each side's night ones: beams out and star shell (ship-minutes,
  read each minute), the first salvo's and the first torpedoes' minute, and the share of its hits on ships lit.

Scenarios play 48 seeds each way by default (`--seeds`), many at once (`--threads`); `--only` picks scenarios by file
name, and `--fine-from` reuses a kept fine side when only coarse changed. The kept baselines live in
`docs/battle/coarse-parity/`.

**Bands** (decision 5): a measure matches when the coarse mean is within ±15% of fine's (absolute floors for
small counts), with seeds enough that each mean's standard error is under a third of the band. A scenario's winner's
share must also agree within 10 percentage points. A band never widens silently: a widening is a rule exception, named,
approved and recorded (`design-rule-exceptions`).

**The record:** each full run appends a dated row to "The parity log" at the end of this doc (the commit, the panel's
worst measures, pass or fail), so drift shows over time as `docs/profiling.md`'s log does.

**When it runs:**
- **Always, in the kept tests** (DevRelease, under ~30 s): a small duel panel with tolerance bands, the rung-3
  fingerprint tests (if any rung-3 piece exists), and the coarse invariants (below). A fine change that moves coarse
  past a band fails here.
- **After any change to the fighting chain** (gunnery, shells, terminal, damage, flooding, fire, DC, torpedoes,
  sighting, smoke, the captains' fighting, motion): the full `coarse-parity`, reported with the change, as profiling
  is now. If it fails: refit (rung 3), fix the coarse approximation (rung 2), or ask.
- **Rung 3's tripwire:** a fitted table carries the fingerprint of the fine runs it came from (fixed seeds, the
  settings it reads, the commit). A test reruns those fine cases and compares. On a mismatch it fails with "the fine fire
  changed: run `shipgen coarse-fit`, then `coarse-parity`". The fit is deterministic and regenerated, never edited.

**The coarse invariants** (kept tests, alongside 2.8's):
- coarse at 30x held (the step mode below) and at 1,000x give the same hash, with the same orders at the same times,
  below the rails' snap speed;
- switching fine to coarse to fine mid-engagement moves no pose and loses no engagement, fire-control state or salvo in
  flight;
- no allocation in coarse fire;
- a replay with tier changes gives the same hash.

## Watching a minute at a time (the step mode)

The user, 2026-10-26: a mode that runs a single coarse step (one minute) and animates the ships' movement smoothly,
as Rule the Waves plays its whole game. Built first for testing coarse, but **testing and playing are the same thing in
the end** (decision 4): it's a way to play, so it's built to the UI language and `input.md`'s grammar from the start.
Steps are one minute, the coarse step's own length; no longer steps.

**It's the coarse tier held at a low speed, not a replay:**
- **`World.CoarseHeld`**, the mirror of `FineHeld`: the tiers and the rails go by at least `CoarseMinSpeed` whatever
  speed is asked (`TierSpeed`), so the world at 30x held is the world at 241x. It's set with the speed by one
  `SetSpeed`, so it's logged and replays hold.
- **A step** is a command: resume coarse held at the playback speed, and pause when the clock reaches the next step
  boundary, after that moment's events (the AI has thought, the look is in). Orders given while paused land at the
  boundary and shape the next minute, as in Rule the Waves.
- **The animation comes free:** coarse motion is analytic segments, and the snapshot already carries them. The renderer
  draws them at any speed, so at a minute in ~3 s (20x) ships turn on their arcs, salvos fly, splashes rise and hits
  land at their own moments. Nothing is computed ahead and played back.
- **An encounter rule that pauses ends the step early.** One that slows keeps the hold.

**Controls** (`input.md`'s grammar): a step button on the clock bar, a key for one step, and the playback speed as a
setting (default a minute in 3 s). The key is proposed in J1 against `input.md`'s list and taken in with the user's
nod (`unified-controls`: exceptions proposed, not invented). `-tier=coarse` on the app's command line
starts with coarse held, as `-speed=fine` does fine.

**Why it's built first (J1):** every later step shows its coarse fight in it, and the same scenario can be watched
fine and coarse at the same playback speed.

## The design

### Fire (4.9): the fine chain on coarse ships first

- **Lift the silence.** Fire control, salvos, spots and shells run for coarse ships as they are (rung 1). The fine
  chain reads the shooter's and target's poses, velocities and rates of turn. A kinematic arc gives all three, so the
  only change is where coarse motion's quantities differ: a turn starts at once (no helm lag) and its swing builds with
  no delay. Fire control's "solution disturbed" by a turn follows the coarse track's turns.
- **Switching is free:** a battery's fire-control state is the same struct in both tiers, so an engagement goes on
  across a speed change with nothing to translate.
- **If J2 measures it too slow,** in this order: (a) fire control's grid longer in coarse (rung 2: readings every few
  seconds; the parity bench decides whether it holds); (b) **sampled salvos** (rung 3, the agreed 4.9 design made
  concrete). Salvos are still events at their real times and hits still go through the one tracer, but measure, track
  and solve per second are replaced by a fitted fire-control state per engagement: time to the first straddle drawn
  from the fit, then each salvo's common error (the MPI) drawn from the fitted distribution for its phase, range, time
  of flight, level, visibility and both ships' manoeuvres. Per shell, dispersion comes from the gun's own pattern and the
  fall from the ballistic tables, so geometry, danger space and where on the ship a shell lands stay exact. Only the
  fire-control chain's error statistics are fitted. Salvos keep their clumped hits, which is what decides outcomes (one
  lucky salvo). The NPC wars, when they come, will want (b) anyway.
- **Ammunition, crews, discipline** go on as in fine, since salvos are real.
- **The "guns silent" label goes** from the clock panel. The rumble for fights at speed (decision 4) stays with the UI.

### Damage (4.8): the same model, measured

- Every hit goes through terminal and the damage model (rung 1). That includes near misses (E7), since misses are real
  shells.
- Flooding, fire and DC already run their 1 s step in any tier. J6 measures what it costs in a big coarse fight. **Only
  if it shows:** the one model's step lengthens where it's stable (an adaptive step in the same code). Never a second,
  closed-form model beside it. This replaces 4.8's "integrated over the step in closed form", which would be a second
  model.

### The captains in a fight

- **Fighting decisions run in every tier:** `FallOut`, `OutOfRounds`, `TakeUpStanding` and `Aim` run at the coarse
  think (once a step), as the rest of the captain does.
- **Torpedoes seen at their moment:** a ship can comb in 1-2 minutes' warning, so a think a minute misses most tracks.
  The torpedo watch's own threat test is evaluated at the moment a track comes into a ship's sight. That moment is
  worked out along both analytic tracks when they're laid, and it wakes the captain then (an event, the same test).
- **Evasion as a route:** a zigzag of 75 s legs can't be steered a minute at a time. A coarse captain under fire lays
  the zigzag ahead as a route of legs (coarse motion runs routes exactly), laid again at each think. The track zigzags
  as a fine one does, and the enemy's fire control sees the same turns.
- **The helm lag and a turn's speed loss** (2.8's carried items) come in only if parity asks: a straight run of the lag
  before each coarse turn, and the speed lost as the fine trials give it.

### Sighting and smoke

Both stay at a minute (rung 2) unless the parity bench says otherwise. The risks to measure are a first sighting up to
a minute late, a target lost behind smoke a minute late, and the smoke field laid a minute at a time. If one shows: a look
at the fine cadence only inside an encounter's ships, or the smoke tick shorter while firing. Each is a measured fix, not
a guess.

## Steps

Each step ends with something to run and watch in the step mode, and with the parity bench run where there's fighting.

| Step | What | Checked by |
|---|---|---|
| **J0** | **The bench.** `coarse-parity` with the panel's four layers; `FcBench` and `scenario --play` take the tier asked (`--tier`); fine baselines recorded; the parity log started. Coarse can't fire yet, so this fixes fine's side and the format. | Fine's panel rerun twice gives the same numbers (determinism); time for the full panel stated. |
| **J1** | **The step mode.** `CoarseHeld`, the step command, the clock bar's step and key, `-tier=coarse`. | Coarse held at 30x and 1,000x give the same hash; a step pauses on the boundary; the motion drawn smooth (a `-screenshot` series mid-step). |
| **J2** | **Guns in coarse.** The silence lifted; the captains' fighting decisions in every tier; `scenario --play` no longer drops to fine. **A gate:** the duel and squadrons parity, and the Jutland's speed with guns, against the target (decision 3: 2,000x or more on 2 workers). | The duel, dodging and fights-to-the-end layers; the switch invariants; no allocation; the Jutland's fighting speed in coarse, measured headless and in the app. |
| **J3** | **Torpedoes in coarse.** Launches at the coarse think, tracks seen at their moment, combing. | Flotilla attacks' hits and combs, fine against coarse. |
| **J4** | **Dodging** (decision 1). Zigzag and attack runs laid ahead as routes; helm lag, swing and speed loss in turns only if the dodging layer asks; a fitted correction (rung 3) only if the track can't be made to match. | The dodging layer, per target from torpedo boat to battleship. |
| **J5** | **Sighting and smoke at a minute,** measured, fixed only if a band fails. | Scenario parity (wave 1), smoke on. |
| **J6** | **Damage over time at speed,** measured; the adaptive step only if it shows. | The damage probe's share in a coarse Jutland; outcomes unchanged at fine's step. |
| **J7** | **Only if J2's gate fails on speed:** fire control's grid longer in coarse, then the sampled salvos (rung 3) with `coarse-fit` and its tripwire. | The tripwire trips on a deliberate fine change; the parity layers hold. |
| **J8** | **The process in place.** The kept tests in the suite, the full panel's time and how to run it written here, the parity log's first full row, and the rule saved for later sessions. | A fine gunnery change fails the kept tests (tried, then reverted). |

**J0 done (2026-10-26).**
- **Built:** `shipgen coarse-parity` (`CoarseParity.cs`) with the four layers above. Each measure prints fine's and
  coarse's means with their standard errors, the difference, the band and a verdict:
  - **ok:** inside the band, with both errors under a third of it;
  - **FAIL:** outside the band by more than twice the noise, or one tier has the event and the other never does;
  - **noisy:** with how many times the runs it would need.

  `duel --tier coarse` and `scenario --play --tier coarse` ask 1000x (`CoarseParity.CoarseSpeed`: coarse, under the
  rails' snap speed). `FcBench` takes the asked speed and records a damaged target going down and falling out by the
  default doctrine's rule. `ScenarioPlay`'s seed loop is shared (`Begin`, `PlayToEnd`). Every world in the bench runs
  inline, many at once on threads.
- **The dodging benches:** `running-fight.json` (new) and `flotilla.json`. The flotilla no longer ran in at all: its
  commander withdrew it, outmatched, since 5.1d-g (`bugs.md`, Balance). Scenarios now take an opening
  `"attack"` task (the task layer's own Attack, on the biggest enemy in sight), and the flotilla has one.
- **Time:** the standard panel at 48 seeds takes 10-12 minutes on 32 threads (5,500-6,300 thread-seconds fine, 1,400 coarse
  while coarse is silent). Fine is most of it, so `--fine-from` with the kept baseline makes coarse-only iterations
  about five times cheaper. The full panel's three big scenarios take 8 minutes at 8 seeds on 16 threads (2,200
  thread-seconds fine; the Jutland about 3 minutes a seed). At 48 seeds that's about 45 minutes, a run for big changes.
- **Noise, on fine's side at 48 seeds:**
  - **Duels:** every measure resolves at 300 runs, except hits on a zigzagging destroyer (×2.4-2.7 the runs). The fight
    duels resolve at 300 runs too (100 left a light cruiser on a destroyer unresolved).
  - **Scenarios:** 30-70% of each one's measures resolve. Shell hits made and tonnage shares have heavy tails (one
    magazine, one cruiser caught close) and need 3-12× more seeds. They show as noisy rather than failing, so a real
    drift there shows only once it's large.
- **What coarse already shows with its guns silent:**
  - **Torpedoes already launch in coarse.** Some path isn't held to the fine tier, though the captain's own `Aim` is.
    In the flotilla's attack every coarse boat fires (10.9 torpedo hits a run, half a British battleship lost) against
    none fine; in Tsushima 1.9 Russian ships fire coarse against 0.5 fine. J3 finds that path.
  - **The Jutland's exchange flips on torpedoes alone:** Britain's share of the tonnage lost is 0.82 coarse against
    0.36 fine.
- **Kept:** the fine baseline, `docs/battle/coarse-parity/2026-10-26-standard.json`. Run twice, the panel gave the
  same numbers: the bench is deterministic.

**J1 done (2026-10-27).**
- **The hold:** `World.Held` (`ClockHold`: none, fine, coarse) replaces `FineHeld`, set with the speed by one
  `SetSpeed`. Held coarse, `TierSpeed` is at least `CoarseMinSpeed`. A halt to a speed (an encounter's rule) lets the
  fine hold go but keeps the coarse one: a coarse world is slowed, never taken fine. The hash is unchanged for none and fine.
- **The step:** `Command.Step(speed)` holds coarse at that speed, runs, and sets `World.StepEndMs` to the next minute
  of world time (the coarse step's grid, which the AI's thinks share). The clock's target is capped 1 ms past it. Once
  the clock rests there, after that moment's events, the world pauses. A halt that pauses ends the step early. A pause
  and a resume carry it on; a speed asked ends it. Headless, `AdvanceTo` goes on past the step's pause, as through a
  halt. The snapshot carries the hold and the step's end.
- **The app:** a step button beside pause (brass while coarse is held). The bar reads "a step to 15:31" or
  "coarse, held 20×". The menu's clock has "A step played in" (1, 2, 3, 6 or 12 s; 3 s default). `-tier=coarse` starts
  coarse held, at `-speed=` or 20×. § and ⇧Space keep the coarse hold (the passage gear, back to the step mode). A gear
  or + − lets it go. A new world loaded mid-step takes the step from its start (`WorldSession`).
- **The key: Ctrl+Space** (the user, 2026-10-27; 6 was proposed first). The Space family is now the three ways to run
  the clock: resume, a minute, until it matters. It's in `input.md`'s clock table.
- **Checked:** the squadrons scenario 20 minutes coarse at 1,000×, held at 20× and at 1×, and as 20 steps paced by real
  time all give the same hash (`ScenarioTests.The_coarse_tier_held_is_the_same_at_any_speed_and_in_steps`). A step
  pauses 1 ms past its boundary, and an encounter's pause ends one with coarse kept (`CoarseTierTests`). A `-screenshot`
  series mid-step at 12 m/px (frames 330-800, the key at 300) shows the line moving on along its track between
  15:30:16 and 15:30:38 and pausing at 15:31:00. At that boundary the minute's look found the enemy.
- **For J2:** the guns are still silent in coarse, so a step through a fight shows no fire yet. The "guns silent"
  label stays until then.

**J2 done (2026-10-27).** Its gate passed on parity's duels; on speed, the user took what it makes (decision 3).
- **Built:** the tier gate is gone from `GunnerySystem.Start` and `Fire`. A coarse ship's `ShipState` is its segment's
  pose at the second, so the fine chain (rung 1) fires from and at coarse ships unchanged. `BatteryState.Silent` and
  the "guns silent" labels are gone. The captains' fighting decisions (`FallOut`, `OutOfRounds`, `TakeUpStanding`,
  `Aim` with its torpedoes) run at every tier's think. Only `Comb` stays the fine tick's, until J3's watch sees a track at
  its moment. `scenario --play --tier coarse` already stayed coarse through encounters (J0); the default stays fine.
  `scenario --play` now prints the speed hour by hour.
- **Parity** (the standard panel, coarse against the kept fine baseline; `2026-10-27-standard-j2.json`): 95 ok, 9 FAIL,
  82 noisy, against J0's 37 ok and 139 FAIL. Every clear-weather duel is inside its band, steady, zigzagging, turning
  and own ship turning (hits a gun a minute -6% to -9%). So are the fight duels, most within 12%. The fails, each a later step's:
  - **The haze duel** (7 km in 8 km): coarse +36% hits a gun a minute, +29% rounds. Fine loses the target now and then
    at the edge of the haze ("not seen", the battery waits); coarse, looking once a minute, never does: 84% of the drill's
    rate against 63%. The same cause shows at night: the Dover Strait's hit % is +77% (Britain) and +53% (Germany).
    **J5** (sighting at a minute).
  - **The flotilla's dodging:** hit % on the V25-like boats +44%, Britain's hit % +37%. **J4.**
  - **Torpedoes:** the flotilla's boats fire 1.4 a run coarse against 0.06 fine, and Åland's Finnish torpedo hits are
    +257%; coarse captains don't comb yet. **J3.**
- **Speed (the gate, decision 3: the Jutland's fighting at 2,000× or more on 2 workers): fails.** Headless,
  `scenario jutland --play --tier coarse --workers 2`: the 14 hours in 27.6-34.6 s (about 1,500-1,830× overall; the end
  state the same each run). Hour by hour: 2,140× 1,110-1,130× 1,370-1,790× 1,230-1,590× **560-710×** 940-1,250× 750-940×
  1,610-1,720× 1,870-1,890×, then 2,700-4,400× as the fleets thin. The fighting hours run at 560-1,800×. In the app
  (`-scenario=jutland -nopause -speed=5000 -frametimes`): 969× over the first 4.7 game hours, 408× at the heaviest. The
  whole run's split: damage 28.6% (its 1 s step on the wet ships), gunnery 22.8% and shells 7.9%, smoke 16.7% (the
  minute's merge 10.4%), the AI 15.9% (the commander 11.3%), coarse motion 6.9%, sighting 4.3%. Gunnery and shells
  together are 31%: even free, they would make it about 1.45× faster. The heaviest hour needs about 3×, so J7 (cheaper
  fire control) can't meet the gate alone. Damage (J6's adaptive step), the smoke merge and the commander are as large.
  **The user, 2026-10-27:** "perfectly fine for now", after test games ("I wasn't expecting the shells to be that nice"). The
  target is revisited (decision 3), no lever taken; the split above is kept for when speed comes up again.

**J3 done (2026-10-27).**
- **Launches:** J2 already made them at the coarse think: the captains' own (`Torpedoes`, in `Aim`) and the flotilla
  admiral's signal (`FireOnSignal`). The signal was the path J0 found firing coarse before any of it.
- **Tracks seen at their moment:** a coarse captain thinks once a minute, so his lookouts get a wake of their own. When
  torpedoes enter the water (`TorpedoSystem.Launched`, the AI's hook) and after each captains' think,
  `TorpedoWatch.NextSeen` works out the first moment before the next think that a running torpedo comes within sight
  of a coarse ship that's fighting (in an encounter, or a battery engaged). It follows both tracks ahead: the
  torpedo's run, and the ship's segment, then straight on from its end. It samples every 2 s, then halves to 50 ms. The
  AI wakes then (`AiSystem`'s one pending watch, its time and generation in the hash once used) and runs the think's
  own test (`Comb`) for those ships. A comb's end is a wake too, so she gives her course back on time. A guess the
  ship's next segment makes wrong only moves the wake: the test there is the real one.
- **Combing in every tier:** `Comb` runs at the coarse think and at the watch's wakes.
- **Checked:** `TorpedoEvasionTests`. The coarse captain is ordered to comb at the moment the spread comes into sight
  (108.60 s), and the fine tick at its next second (109.05 s). She gives her course back. Combing buys her as many
  hits saved at 1,000× as at 1× (seeds 1-6: coarse 0 of 24 against 10 holding on; fine 3 against 10).
- **Parity** (`2026-10-27-standard-j3.json`): 95 ok, 10 FAIL, 81 noisy. Åland's Finnish torpedo hits went from +257% to -26%,
  inside the band. **What it shows next:** torpedo hits now run low in coarse across the scenarios (Tsushima's Japanese
  -95%, the prizes' war's French -82%, the Dover Strait's -72% and -54%, Åland's Swedish -38%). A coarse comb turns at once,
  with no helm lag: in the evasion bench she's ordered to comb at the same moment as on the fine tick, but on the
  fine tick she takes 14 s to swing her first 2°. So combing asks for J4's helm lag. Two measures noisy at J2 now fail
  on the same cause: the Dover Strait's German tonnage lost (+103%) and the prizes' war's Italian ships lost (-62%,
  France's torpedo hits -82%).
- **Left for J4:** the flotilla's boats still fire 1.4 times a run coarse against 0.06 fine, hit 30-44% more. Their attack
  run is steered at the think, so coarse holds a minute's leg where fine turns away; J4 lays it ahead as a route.

**J4 done (2026-10-27).**
- **The helm lag:** a coarse turn ran its arc from the moment it was ordered, while the fine tick's heading answers the helm
  late (the rudder put over at its gear's rate, then Nomoto's first-order swing). Now a coarse turn from a steady course
  runs straight on first for `Handling.HelmLagS`: Nomoto's T at the speed plus half the rudder's travel time, the fine
  model's own lag, so no hand-set number. The lead is kept on the ship (`leadEndMs`, `leadSide`: a 180° turn keeps its
  side through float noise), hashed only while set. A ship already swinging (her curvature handed over from fine) has
  no lead. Riders turning together take their guide's.
- **Zigzag legs between thinks:** a coarse captain's think comes once a minute, so a zigzag he set held one leg for the
  whole minute where fine turned at each leg's end. His think now keeps the zigzag's base course and its next leg's
  end, and the torpedo watch's wake (J3's) also wakes at the leg's end to give the next leg. A comb under way lets the
  leg pass unsteered.
- **Tried and reverted: speed lost in turns.** Coarse arcs lost speed as the fine model does in a hard turn
  (`TurnDragFactor`). The coast-slide tests then ended 4.9-5.6 km from fine (0.44-0.46 km without it), and the 50 mm
  re-lay test and the snapshot drawing broke. The cause: a coarse arc always turns on the course rudder's circle, while
  the fine helmsman uses little rudder for small corrections, so coarse paid hard-turn losses fine never does. It needs a
  small-rudder helmsman model in coarse first; left out.
- **A hang found and fixed:** the first panel run hung (two of 432 plays looped). A leg ending during a comb was left
  unsteered without moving on, so the watch woke at the same millisecond forever. Its next leg's end is now always set,
  and the watch counts only leg ends still ahead.
- **Checked:** `CoarseTierTests.A_coarse_turn_keeps_with_the_fine_ticks_heading`. Over a 90° turn coarse keeps with
  fine's heading: a super-dreadnought at 20 kn 6.9° RMS (14.8° at worst, 81 m apart after 3 min), a destroyer at 28 kn
  5.1° RMS (15.3°, 143 m). The comb bench still orders the comb on time (108.60 s coarse, 109.05 s fine).
- **Parity** (`2026-10-27-standard-j4.json`): 92 ok, 6 FAIL, 88 noisy. Torpedo hits came back: the prizes' war's French
  -82% to -18%, Italy's ships lost back in the band, Tsushima's Japanese -95% to -70% (inside the absolute floor). The
  flotilla's hit % on the V25-like boats fell from +44% to +26% (noisy). **Fails left:** the haze duel (+36% hits) and the
  Dover Strait's hit % (+73%, +74%) are J5's: the minute's look holds a target fine loses. The flotilla's boats still
  fire torpedoes 1.29 times a run against 0.06; their run still closes faster coarse. It waits for the speed loss
  above (the small-rudder helmsman), carried open.
- **Speed:** `scenario jutland --play --tier coarse --workers 2` plays the 14 hours in 29.3 s, inside J2's 27.6-34.6 s:
  no measurable cost.

**J5 done (2026-10-27).** The haze duel's fail was the smoke, not the look; the night's is the AI's minute.
- **Found:** coarse ships were never blinded by smoke. Fine with the smoke field off gave coarse's numbers exactly, and
  coarse gave the same numbers with it on or off. A ship's smoke since the last coarse tick wasn't laid until the minute
  tick, by when she'd run ~770 m past it, and her gun puffs weren't filed until then. Her newest smoke, the densest
  there is, never stood on her lines (τ on the duel's line ~0.01 coarse against 0.25-0.45 fine).
- **The young steps** (`SmokeSystem.YoungStep`, rung 2): between the coarse ticks, every `SmokeYoungMs` (3 s) each
  ship's smoke since her last is laid where it was made, and the index's young part filed again (T2's `Fresh`), as the
  fine tier does. The air, the merges, the retiring and the full rebuild keep the minute. 6 s let the haze duel drift
  out (+16% rounds).
- **The lines unwound:** the sight query put the ships into the air's frame as the last tick left it, without its
  drift since. That's under a second in fine, up to a minute (several hundred metres in a breeze) in coarse. Now
  `SmokeSystem.ToAir` unwinds them as a puff laid then is. Fine moves by under a second's drift; its baseline is rerun.
- **The watch** (`Sightings.Watch`, rung 2): fire control reads her being in sight every second, nothing else does
  between the coarse looks. So every `SightWatchMs` (5 s) the engaged batteries' targets are looked for again, the fine
  look's own code on those items only. A target lost behind smoke at the minute's look is found again within seconds,
  as fine finds her: a 15 kn duel at 14 km fired 13% fewer shells coarse, now 2-4%. At 1 s it cost five times as much
  for the same parity.
- **A look covers only the time since she was last looked for:** the minute look's draw took 60 s, so a contact the
  watch had been looking for every few seconds was all but surely found again at each minute. The haze duel's in-sight
  share went from 79% to 71% (fine 61%).
- **The smoke store's free list** grows with the store (the coarse no-allocation test found it doubling late, as puffs
  retired).
- **Tried and dropped:** a watch on every pair out of sight that could be made out before the next look (closing at
  best speed, broadside, clear air). It took most enemies out of sight by day and cost 24% of the Jutland at 1 s.
- **The night (the Dover Strait), measured:** hit % by range band is the same in both tiers (16.6 against 17.1% at
  2-3 km, 5.3 against 4.7% at 3-4 km). Coarse fights closer: 3.7× fine's shells under 1 km, 36% fewer at 3-4 km. A look
  every second alone didn't move it (hit % +73% and +47%). Captains thinking every second took it to +43% and +40%;
  both together to +23% and +28%, still with 3× the shells under 1 km (fine's admirals think every second too). So the
  night's gap is the AI's minute cadence in a close action, not sighting.
- **Checked:** `SmokeSightTests.Between_the_coarse_ticks_her_newest_smoke_is_on_the_lines` (the most τ on a line, mean
  over 4 minutes: fine 0.61, coarse 0.62; 0.05 with the young steps off). `GunneryTests`' coarse duel now sums four
  seeds: one duel's own smoke moves a single seed's salvos by a fifth.
- **Parity** (the standard panel both ways, fine rerun: `2026-10-27-standard-j5.json`): 90 ok, 6 FAIL, 90 noisy. Every
  J4 fail is in or near its band: the haze duel ok (+10% hits and rounds, was +36%), the flotilla's torpedo launches ok
  (0.10 both ways, was 0.06 against 1.29), the Dover Strait's hit % noisy (+29%, +43%, was +73%, +74%). **Six new
  fails,** all noisy at J4 and further the same way:
  - **The prizes' war's French shell hits** (-36%): fine's side moved (237 to 266 since the 2026-10-26 baseline), coarse
    stayed at 169 with the watch or the young steps off. Not J5's.
  - **The prizes' war's Italian ships lost** (-68%) and tonnage share (-72%): 0.54 ships coarse, 0.75 with the watch off,
    0.90 with the young steps off (each ±0.15), against 1.69 fine.
  - **The flotilla's hit % on the G101-like boats** (+41%) and **Britain's hit %** (+40%): +29% with the watch off, +13%
    with the young steps off.
  - **Squadrons' British tonnage share lost** (-44%): -31% and -42% with each off, so neither alone.

  Smoke now stands on coarse's lines, but only the batteries' targets are watched. The AI still sees the picture once a
  minute, and a minute look that falls in smoke loses her for the AI until the next. These and the night are the AI's
  minute cadence (the user's to decide: a step of its own).
- **After J5, with the user (2026-10-27):** the fights stay coarse, so AI fights can run in the background later: no fine
  pockets (a flotilla attack would pull the battle squadrons fine, and the Jutland's heavy hours with them). A thick, dark
  night giving little action is fine (searchlights come later). **Tried: waking the AI at the moment of a sighting**
  (a prototype, reverted). Every pair was looked at each second between the coarse looks, and a new sighting woke the
  captains, then the admirals too. The Dover Strait's hit % coarse against fine: J5 as built +29% and +43%; looks alone
  +83% and +33%; with the captains woken +50% and +41%; with the admirals too +36% and +22%. Each is ±2-3 points at 48
  seeds, so none beats J5 beyond the noise: not built.
- **Speed:** the Jutland coarse plays its 14 hours in 47.8 s against J4's 29.3 s (1,050× overall). Sighting 7.9% (the
  watch), the smoke 27% (its index 12.4%, the merges more puffs), damage 26.5%. Smoke laid every 3 s is the most of it:
  the price of coarse ships being blinded by their smoke at all.
- **The user's guideline from here (2026-10-27):** coarse isn't really for the player. It's for roughly realistic end
  results, and a balancing pass comes later (cursed tricks for the smoke allowed then). J6-J8 go on without chasing
  every fail.

**J6 done (2026-10-27).** The damage step was a fifth to a quarter of the coarse Jutland. Most of that turned out to be a
bug, and the rest was water that moved slowly but stepped every second.
- **Measured** (the coarse Jutland, the parts of `DamageSystem.Step` timed): damage was 28% of 50.3 s, 800k steps at
  17.6 µs. Why steps came every second: a fire burning 395k, the water moving 0.05 t or more 357k; the 30 s settled step
  only 35k.
- **A bug, the same in both tiers: a spent magazine raced again every second** (5.0 s of the 14). Fixed;
  `magazines.md`'s open items have it. The Jutland went 50.3 s to 44.5 s.
- **The water on its own clock** (rung 1: the same model, `ShipCondition.FloodDueMs`). A burning ship's flooding, repairs
  and settling ran on the fire's 1 s step. Now the water keeps its own: due by its cadence, at once when something
  moves it (a hit or damage control wakes the ship; a magazine's valves opened by the fire), or once the hoses have put
  in a settled step's worth (`FloodIdleT`).
- **The steady step** (`FloodSteadyShare`, 1/10,000 of her displacement): 627k of the steps were water that wasn't
  settled, but 61% of those moved under 1/10,000 of her displacement a second. The water's step is now the longest of
  1, 2, 3, 5, 6, 10, 15 or 30 s over which the last step's flows would move no more than that. The flows are bounded
  (into a cell no more than its room, across a join no more than levels it), so a longer step is less exact, never
  unstable. The flows counted are the gross ones (in, out, pumped, across the joins). A first try on the net change left
  Marlborough (one torpedo, `dc-history`) listing 3.8° at 10 h against 0.4° stepping every second. Her water balanced
  against the pumps, so the net change hid the flows. On the gross flows she lists 0.6° (the committed code 0.7°).
- **Outcomes at fine's step** (the yardsticks against everything stepped every second, `FloodIdleMs` 1000):
  `damage-history` agrees case by case. At 100 runs Derfflinger's water is 283 against 268 t, Bismarck lost 37%
  against 32%, Kirishima 21% against 20%. In `dc-history` at 100 runs, Seydlitz has 4,548 against 4,522 t at 4 h, and
  Lützow 5,035 against 5,030 t, lost 24% against 25%. The benches themselves run faster: `damage-history` 75 s to 48 s,
  `dc-history` 106 s to 79 s.
- **Checked:** `FireEffectsTests.A_spent_magazine_is_not_set_off_again`;
  `FloodingTests.A_ship_flooding_slowly_steps_seldom_to_the_same_water` (a super-dreadnought with a 0.3 m² hole, two
  hours on: 770 t against 779 t at 1 s, stepping every 3 s at the end).
- **The coarse Jutland:** damage 13.4% of the run, 9.2 µs a step and 13.5 steps a game second (from 28%, 17.6 µs, 16).
  This seed's fight was a smaller one (8,241 hits against 11,893), and the whole run took 46.7 s; the fight differs by
  seed, so the per-step figures are the measure. The smoke (33%) and gunnery (22%) are now the largest.
- **Parity** (the standard panel both ways, `2026-10-27-standard-j6.json`): 93 ok, 4 FAIL, 89 noisy. No measure on
  either side moved more than three standard errors since J5. The fails are J5's (the flotilla's hit %, squadrons'
  British tonnage share, the prizes' war's Italian losses), and the Dover Strait's German hit % at +48% (J5 +43%,
  noisy).

**J7 done (2026-10-27): its (a), fire control's grid longer in coarse.** The user, before it was skipped: no low-hanging
fruit left behind. Decision 3 had taken J2's speed, so this was a check, not a gate.
- **Measured** (the coarse Jutland, `GunnerySystem`'s parts timed): gunnery was 22% of the run, 10.8 s. 3.0 million
  battery-seconds at 7.7 µs each: the sensors' margins 4.3, the aim and the ballistic solve 2.6, the rest 0.7. Salvos
  were 1.3 s and spots 0.1 s. No hot spot like J6's magazine: the second is spread over what the model needs.
- **The 3 s grid** (`FireControlCoarseMs`, rung 2: the same chain at a coarser cadence). Readings are taken as they
  fall due on the grid, the mounts are ordered every 3 s, and salvos still fire at their own moments (events, as
  before). Every coarse duel and fight to the end held its band at 2 s and at 3 s. At 3 s: the 16 kyd duel -6% hits a
  gun a minute, the haze duel +10%, a light cruiser on a zigzagging destroyer -4%, the fights' shares out of the fight
  +2% and -7%. Gunnery went 206 to 102 µs a game second.
- **The sun's direction** worked out once a moment (`Sun.Direction`, a thread's own cache): every battery asked it every
  second, the same answer for the whole world. It's exact: the Jutland ends in the same state with it.
- **Not built: (b), sampled salvos (rung 3).** There's no need for it at this speed. It stays for the NPC wars, if they
  want it.
- **The coarse Jutland:** 39.2 s for its 14 hours (from J6's 46.7 s, in a bigger fight: 13,144 hits against 8,241).
  Gunnery 12.6%, damage 19.4%, the smoke 32%, the AI 13%, sighting 11%.
- **Parity** (coarse against J6's fine baseline, `2026-10-27-standard-j7.json`): 94 ok, 4 FAIL, 88 noisy. One measure on
  the coarse side moved more than three standard errors: the prizes' war's French ships firing torpedoes, 1.56 to 2.62
  (fine 1.92). Its shell hits and Italian losses came toward fine with it: 173 to 211 hits (fine 246), 0.79 to 1.08
  ships lost (1.71). The fails: the Dover Strait's hit % (+65%, +68%) and Cape Sarych's Ottoman hit % (+75%), the
  night's close fighting (J5); squadrons' German tonnage share (-42%).

**J8 done (2026-10-27): the process in place.**
- **Running the parity process** (the user, 2026-10-27: too slow to run after every change; the coarse tier won't be
  used much for now):

  | When | What | Time (32 threads) |
  |---|---|---|
  | Every change | the kept tests, with the suite (`CoarseParityTests` and the invariants below) | ~15 s |
  | A sync point: the end of a coarse plan step, around a balancing pass | `shipgen coarse-parity` (the standard panel), both ways if fine moved | ~14 min |
  | The same, fine unchanged | `coarse-parity --tier coarse --fine-from docs/battle/coarse-parity/<latest>.json` | ~4 min |
  | A quick look at fire alone | `coarse-parity --layer duels` | 1.5-3 min |
  | The coarse tier released to players | `coarse-parity --panel full` (the Jutland at 48 seeds) | over an hour |

  Each sync point's run adds a row to the parity log below; `--out` keeps it in `docs/battle/coarse-parity/`, and the
  latest both-ways file is the fine baseline. The bench's output is buffered until it ends: start it in the background
  and wait on the process, don't restart it.
- **The kept tests** (`CoarseParityTests`, DevRelease, ~15 s):
  - **The duel panel:** the bench's duels played both ways over 48 fixed seeds, engaged at 90 s once the coarse tier's
    first look has the ships see each other (the panel compares the fire, not the minute's first sighting). The cases:
    a battleship duel at 14 km, secondaries on a zigzagging destroyer at 6 km, a light cruiser on a zigzagging
    destroyer at 8 km. The bands: salvos ±15% (decision 5); hits ±20%, three standard errors of 48 seeds' hits (2-25
    a run). Today: salvos -1%, -1%, -3%; hits 0%, -11%, -15%.
  - **The tier crossed mid-engagement,** fine to coarse and back with shells in the air: neither ship moves, the salvos,
    shells, phase and shells in flight all carry over, and the battery fires on in each tier.
  - **Coarse fire allocates nothing,** a light cruiser on a zigzagging destroyer with damage on.
  - **Already kept:** coarse held at any speed and in steps gives the same world
    (`ScenarioTests.The_coarse_tier_held_is_the_same_at_any_speed_and_in_steps`, the squadrons fight); a replay with
    tier changes gives the same world (`CoarseTierTests`); the crossing moves no pose; no rung-3 piece exists, so no
    fingerprint tests.
- **The check: a fine gunnery change that coarse can't follow,** tried and reverted.
  - A reading's way into the computer counted in ticks (`ArriveMs` as (latency + 2) fire-control ticks, not its
    latency): 3 s longer on fine's 1 s tick, 9 s on coarse's 3 s. The panel failed: the light cruiser's hits -31%.
  - A salvo timed only on every other tick passed. On a 30 s reload the extra wait cost coarse 3-8% of its salvos:
    under the panel's bands, for the full bench to see. The kept tests catch the large drifts, the sync points the rest.
- **Found while building the panel (for the balancing pass): a destroyer turning hard and often dodges coarse fire
  better.**
  - A regular zigzag (60° turns every 75 s) took 40-50% fewer hits coarse than fine, at a 1 s fire-control grid as
    well as 3 s. The bench's zigzag picks a side at random, so it turns half as often: -8 to -13% there.
  - With damage on, a light cruiser on a zigzagging destroyer at 7 km ran -15% to -26% over four seed sets: the
    destroyer, hit and slowed, is a target the fine tick keeps hitting more.
  - The likely cause is J4's carried item: a fine ship loses speed in a hard turn, a coarse one keeps her 30 kn.
- **Not done:** the parity log's first full row (the user, 2026-10-27: a full panel when the coarse tier is released).
  The standard panel at J7 is the last sync point.

## Decided

The user's, 2026-10-26:

1. **A hit is the same in both tiers;** what coarse approximates is damage over time: the aggregate hits a ship takes,
   because coarse lacks fine's precise ability to dodge shots, most of all for smaller, more agile ships. Dodging gets
   its own parity layer and step (J4).
2. **Fire starts as the fine chain on coarse ships** (rung 1); fitted tables only if J2 measures it too slow (J7).
3. **The speed target:** the Jutland's fighting at 2,000x or more on 2 workers, against ~300x fine today.
   **Revisited 2026-10-27** (the user, after test games of J2): what J2 makes is "perfectly fine for now", 560-1,800x in
   the Jutland's fighting hours on 2 workers. Not a gate for J3-J8; speed work waits until play asks for it.
4. **One-minute steps.** There's no difference between testing and a way to play in the end: the step mode is built
   as a way to play.
5. **The parity bands:** ±15% on means with absolute floors, the winner's share within 10 percentage points.

## The parity log

| Date | Commit | Panel | Worst measures | Result |
|---|---|---|---|---|
| 2026-10-26 | 0e08e9f3 (fight duels at 300 runs after it) | standard, 300 runs, 48 seeds: 186 measures, 10 min | coarse silent: every gunnery measure -100%; the flotilla's boats all fire coarse (10.9 torpedo hits a run against 0) | 37 ok, 139 FAIL, 10 noisy: expected before J2. Fine's baseline kept (`2026-10-26-standard.json`); its rerun gave the same numbers |
| 2026-10-26 | 0e08e9f3 | full's big three (Heligoland Bight, Dogger Bank, the Jutland), 8 seeds: 45 measures, 8 min on 16 threads | the Jutland's exchange 0.82 coarse against 0.36 fine on torpedoes alone; ships lost -72% to -98% | 5 ok, 29 FAIL, 11 noisy: expected before J2 (`2026-10-26-full-big-three-8-seeds.json`) |
| 2026-10-27 | J2 (guns in coarse) | standard, coarse only against the 2026-10-26 fine baseline: 186 measures, 4.3 min on 32 threads | the haze duel +36% hits (the minute's look holds a target fine loses); the Dover Strait's hit % +77%; the flotilla's boats fire torpedoes 1.4 a run against 0.06 | 95 ok, 9 FAIL, 82 noisy: the fails J3-J5's (`2026-10-27-standard-j2.json`) |
| 2026-10-27 | J3 (torpedoes in coarse) | standard, coarse only against the 2026-10-26 fine baseline: 186 measures, 4.9 min on 32 threads | torpedo hits low across the scenarios (Japan -95%, France -82%): coarse combs turn with no helm lag; the flotilla's launches 1.4 a run against 0.06 | 95 ok, 10 FAIL, 81 noisy: Åland's torpedo hits fixed; the rest J4's and J5's (`2026-10-27-standard-j3.json`) |
| 2026-10-27 | J4 (dodging) | standard, coarse only against the 2026-10-26 fine baseline: 186 measures, 4.4 min on 32 threads | the Dover Strait's hit % +73% and +74% and the haze duel +36% (J5's); the flotilla's launches 1.29 a run against 0.06 | 92 ok, 6 FAIL, 88 noisy: torpedo hits back in the bands (`2026-10-27-standard-j4.json`) |
| 2026-10-27 | J5 (sighting and smoke) | standard, both ways (fine rerun: the lines unwound): 186 measures, 14 min on 32 threads | the prizes' war's Italian losses -68% (and France's hits -36%, fine's side moved); squadrons' British tonnage share -44%; the flotilla's hit % +41% | 90 ok, 6 FAIL, 90 noisy: J4's fails in or near the bands; the new ones the AI's minute in smoke (`2026-10-27-standard-j5.json`, the new fine baseline) |
| 2026-10-27 | J6 (damage at speed) | standard, both ways (the spent magazine fix and the water's clock reach fine) | squadrons' British tonnage share -44%; the prizes' war's Italian ships lost -54%; the Dover Strait's German hit % +48% | 93 ok, 4 FAIL, 89 noisy: nothing moved over 3 SE since J5 (`2026-10-27-standard-j6.json`, the fine baseline now) |
| 2026-10-27 | J7 (the 3 s fire-control grid in coarse) | standard, coarse only against J6's fine baseline: 186 measures, 3.3 min on 32 threads | the night's hit % (the Dover Strait +65%, +68%; Cape Sarych +75%); squadrons' German tonnage share -42% | 94 ok, 4 FAIL, 88 noisy (`2026-10-27-standard-j7.json`) |
| 2026-10-30 | night.md Q11 (the night layer; the coarse fire watch, a minute's flare) | standard, both ways (fine moved: the crew and night plans), with the night layer: 311 measures, 17.7 min on 32 threads | day gunnery drifted since J7: coarse shell hits -34 to -49% at Cape Sarych, Coronel, the prizes' war and Tsushima (HEAD before Q11 the same against this fine side: not Q11's); the night: Southampton's first salvo +17 min (bugs.md, the enemy's smoke on coarse lines), the 4th Flotilla's British torpedo hits -70% | 151 ok, 31 FAIL, 126 noisy (`2026-10-30-standard-q11.json`, the fine baseline now) |
