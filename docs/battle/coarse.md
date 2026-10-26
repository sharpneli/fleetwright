# Plan: fighting in the coarse tier
Status: **drafted 2026-10-26**, for the user's answers (the questions at the end). It takes over `fighting.md` 4.8
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

**On damage** (the user's "damage calcs approximate it"): I propose damage isn't approximated at all. A hit in coarse
goes through the same terminal and damage model as a fine one (rung 1), as 4.9 always said ("damage is the same kind in
either tier"). The per-hit cost is small next to fire control. A second damage model would break single implementation
and would drift with every damage pass, and those passes are many. What coarse approximates is how often hits come and
when: motion, cadence and, only if needed, fire's statistics. (Question 1.)

## The parity bench: the process

Coarse's yardstick is the fine tier, never history (the user, 2026-10-26). History stays fine's yardstick
(`history-is-the-yardstick`).

**`shipgen coarse-parity`** runs a fixed panel both ways, many seeds each, and prints fine against coarse with the
bands below. The panel has three layers, cheapest first:

- **Duels** (`FcBench` with each ship's tier asked): one battery on one target across ranges, fire-control levels,
  calibres, visibility and the target's behaviours (steady, zigzag, turn). Compared: hits a gun a minute, time to first
  straddle and first hit, rounds spent. Damage off. This isolates fire from the AI.
- **Fights to the end** (duels with damage on, and `squadrons`): sinkings, time to out of the fight, losses by how
  they went (magazine, capsized, broke in two, foundered), hits by calibre.
- **Scenarios** (`scenario --play --tier fine|coarse`, wave 1 and the Jutland): each side's tonnage share sunk and out
  of the fight, ships sunk, the decision's time, ammunition spent, who declined. This layer includes the AI at a minute's
  cadence.

**Bands** (proposal, question 5): a measure matches when the coarse mean is within ±15% of fine's (absolute floors for
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
as Rule the Waves plays its whole game. Mainly for testing coarse; it might become a way to play.

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

**Proposed controls** (`input.md`'s grammar, question 4): a step button on the clock bar, a key for one step and with
⇧ for five, and the playback speed as a setting (default a minute in 3 s). `-tier=coarse` on the app's command line
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
| **J0** | **The bench.** `coarse-parity` with the panel's three layers; `FcBench` and `scenario --play` take the tier asked (`--tier`); fine baselines recorded; the parity log started. Coarse can't fire yet, so this fixes fine's side and the format. | Fine's panel rerun twice gives the same numbers (determinism); time for the full panel stated. |
| **J1** | **The step mode.** `CoarseHeld`, the step command, the clock bar's step and key, `-tier=coarse`. | Coarse held at 30x and 1,000x give the same hash; a step pauses on the boundary; the motion drawn smooth (a `-screenshot` series mid-step). |
| **J2** | **Guns in coarse.** The silence lifted; the captains' fighting decisions in every tier; `scenario --play` no longer drops to fine. **A gate:** the duel and squadrons parity, and the Jutland's speed with guns, against the target (question 3). | Parity layers 1-2; the switch invariants; no allocation; the Jutland's fighting speed in coarse, measured headless and in the app. |
| **J3** | **Torpedoes in coarse.** Launches at the coarse think, tracks seen at their moment, combing. | Flotilla attacks' hits and combs, fine against coarse. |
| **J4** | **Manoeuvres under fire.** Zigzag as a route; helm lag and speed loss only if J2-J3's parity asks. | Duel parity against a zigzagging and a turning target. |
| **J5** | **Sighting and smoke at a minute,** measured, fixed only if a band fails. | Scenario parity (wave 1), smoke on. |
| **J6** | **Damage over time at speed,** measured; the adaptive step only if it shows. | The damage probe's share in a coarse Jutland; outcomes unchanged at fine's step. |
| **J7** | **Only if J2's gate fails on speed:** fire control's grid longer in coarse, then the sampled salvos (rung 3) with `coarse-fit` and its tripwire. | The tripwire trips on a deliberate fine change; parity layers 1-3 hold. |
| **J8** | **The process in place.** The kept tests in the suite, the full panel's time and how to run it written here, the parity log's first full row, and the rule saved for later sessions. | A fine gunnery change fails the kept tests (tried, then reverted). |

## Questions for the user

1. **Damage is not approximated:** coarse hits go through the same terminal and damage model; coarse approximates only
   how often and when hits come. Agreed? (Recommended. The alternative is a second damage model to keep in step with
   every damage pass.)
2. **Fire starts as the fine chain on coarse ships** (rung 1), and fitted tables (the agreed 4.9 design) are built only
   if J2 measures it too slow. Agreed? (Recommended: no drift to manage until speed demands it.)
3. **The speed target for a fleet action in coarse:** proposal: the Jutland's fighting at 2,000x or more on 2 workers
   (an hour of battle in under 2 s), against ~300x fine today.
4. **The step mode's controls:** a clock-bar button, a key with ⇧ for five steps, a minute in 3 s by default. Which key?
   One step length (a minute), or also 5 and 15 minutes as Rule the Waves has? Testing only, or a way to play as well?
5. **The parity bands:** ±15% on means with absolute floors, the winner's share within 10 points. Tighter or looser?

## The parity log

| Date | Commit | Panel | Worst measures | Result |
|---|---|---|---|---|
