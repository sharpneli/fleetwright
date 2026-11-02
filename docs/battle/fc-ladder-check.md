# The fire-control ladder, checked step by step

Started 2026-11-02 at the user's word, after BL1ag (balance.md) found levels 1 and 3 hitting alike: "write a quick plan
to go through the whole ladder like this. Basically check what the improved version should give -> verify that it
actually does. I bet we have lots of issues like these as we just built the stack but didn't do thorough step by step
verification." Step letters **FL**.

## The rule

- Each step up the ladder (a better rangefinder, computer or mount) is checked **where the physics says it must differ**,
  and only there. In ideal conditions an older system may do as well (the user: "In ideal circumstances one is not really
  expecting any difference"). No handicap, no lumped randomness, keeps an old system worse.
- First state what the better system should give and in what situation, from what it is (its mechanism and the record),
  before measuring.
- Measure on the error ladder's rung that holds the mechanism (`GunneryErrors`, balance.md BL1w), then with every error on.
- On its rung the difference must show: if it doesn't, the mechanism is wrong. With every error on it should still show:
  if it doesn't, something else is eating it, most likely a lumped number standing in for physics the chain now has
  (as `salvo_pct` did, BL1ac) or missing physics. Find it, derive it, then measure again.
- Each step's check becomes a test that asserts the difference on its rung and prints the every-error result.

## The bench

`GunneryTests`' duel (BL1ag): one ship's battery at a preset level on another at a range, 16 duels, with the situation's
knobs: her own zigzag (`zigDeg`, `legS`), the opening minutes (`Opening`), `GunneryErrors` per rung. To add as steps need
them: the target's zigzag and steady turn, a crossing or closing target, the sea (swell), smoke and spray on the sights,
haze. QE on QE at 16 kyd unless the step's period fought nearer (the 1905 levels at 6-8 km).

## The steps

| | Step up | What changes | Should give | Where it shows | State |
|---|---|---|---|---|---|
| FL0 | bench | | | | done (BL1ag) |
| FL1 | own turns | `own_turn_kick_m`, a Gaussian a salvo by rate of turn (Dreyer 300 m, Mk 8 150 m, digital 20 m): the reference's lumped stand-in | a helm-free machine keeps its solution through her own turns, losing only what its own course and speed inputs lag: the speed she loses in a turn (`TurnDragFactor`) reaching a hand-fed Forbes log late (Dreyer: "manually fed to Dumaresq"), a pitometer at once (Mk 8); her course by gyro repeat | her own zigzag, every error on: level 3 above level 1 | done 2026-11-02 |
| FL2 | the opening | | a longer base and a better first guess put the first salvo nearer her, so it straddles more often and comes to effect sooner | the first salvo's fall, every error on (the first straddle's median time, 143 v 141 s, hid it) | done 2026-11-02 |
| FL3 | 0 → 1 | the eye (20% of range) → FA 2 m; open sights 1.15 → telescopic 0.73 mil; the same hand plot (its own "eye and hand plot" gone) | ranges found at once, not by long ranging; a tighter pattern | the opening at 6-8 km | done 2026-11-02 |
| FL4 | 1 → 2 | FA → FQ/FT 4 m; hand plot → Dumaresq and Vickers clock; telescopic 0.73 → 0.35 mil | the clock runs the range on between sparse readings from her course and speed | a crossing or closing target, readings 2 a minute; haze that loses readings | done 2026-11-02 |
| FL5 | 2 → 3 | Dumaresq → Dreyer Mk III-V (helm-free, the bearing gear); local laying → director 0.5 mil | through her own turns (FL1); the director's layer above the smoke and spray, one fall to spot | her own zigzag; a seaway; her own and the enemy's smoke, spray at speed. On the way: the catalogue's "Dreyer table Mk I-II" (not helm-free) fits the Mk I only; the Mk I* took her heading automatically and the Mk II probably (own-motion-inputs.md §2): split the row | |
| FL6 | 3 → 4 | Dreyer (rate clock, latency 15 s, bias 1.2%) → AFCT (true course, 8 s, 0.6%); FQ/FT → stereo 5 m; director power drive 0.4 mil | a target's turns re-learned sooner; the bearing rate past the Dreyer's 0.25°/s; less bias to range out | the target's zigzag; a fast crossing near | |
| FL7 | 4 → 5 | AFCT → Ford Mk 8 (3 s, 0.4%); stable vertical, RPC; coated, stabilised O4 | fire at any point of the roll, not on it; the rangefinder in a seaway; and the stable vertical's own error in a turn, the record's "several hundred yards" (North Carolina 1945): its mercury erection cut out while the turn's acceleration lasts, the gyro drifting free some 2-3′ a minute, ~200 yd through a 450° turn at 20-25 kyd, re-erecting after (own-motion-inputs.md §4, recommendation 6) | a seaway, rate of fire in it; long own turns | |
| FL8 | 5 → 6 | radar ranging (stand-in) | ranges through haze and at night | haze, night: radar is out of the first game, so checked last | |
| FL9 | 6 → 7 | Mk 8 → Mk 1A (acceleration, turn detection) | follows a target in a steady turn ("a steady turn cuts hits 2-4x for every straight-line machine", research §5.6) | a target circling | |
| FL10 | 7 → 8 | Mk 1A → digital turn filter | the same, quicker; a zigzag | a target circling and zigzagging | |
| FL11 | side steps | Argo (true course 1912, gyro-mounted rangefinder 8 a minute) v Dreyer; AFCT turn setting v AFCT; Type 92 v AFCT | the Argo through her own turns and in a seaway; the turn setting on a circling target; the Japanese plot per its record | as each says | |

## Numbers to audit on the way

Each is checked where its step is, as `own_turn_kick_m` in FL1: is it a physical quantity of the machine, sourced, or a
lumped stand-in for something the chain now models?

- `own_turn_kick_m`: FL1.
- `resid_pct`, `defl_bias_mil`: the residual ballistic bias as the corrections a machine computes (wind, density, drift);
  per machine is right if it says which corrections it has; checked when wind and density come in. Then too: a machine
  whose wind correction was set by hand from the apparent wind (the Ford Mk 1, likely the Dreyer) keeps its old setting
  through a turn until reset; "the later rangekeepers ... need not be corrected for changes in course" (own-motion-inputs.md §4).
- `own_speed_lag_s`: 30 s hand-fed [EST] sits in the sources' 10-30 s; the AFCT family's 3 s "automatic" has no source, one
  weak clue says set by hand (own-motion-inputs.md §1): revisit if its path turns up.
- Drift angle: a ship's track lags her heading in a turn, 1-3.5 kn of her velocity in a hard one, more than the speed lag;
  not in the motion model (`ShipMotion` moves ships along their heading). If it comes in, every machine's own velocity
  (heading times log speed) picks it up with no change to `Reckon`.
- `lay_mil` by mount: a director (0.5 mil) lays worse than a turret's telescopic sights (0.35), and a stable vertical with
  RPC (0.5) worse than a director's power drive (0.4), on a steady platform. Check the sources' split (BL1l) in FL5 and FL7.
- `latency_s`, `q`, the priors: the machine's own assumptions; fine as such, each checked by its step.
- `GunneryOpenS`, `OpenDoubtSteps`: FL2, neither held the opening on the bench (the track settles on its first reading at
  16 kyd for both levels; the turrets' swing held it). Their own check wants a case the swing doesn't cover: a shift.
  FL3: an eye's doubt never comes within two steps, so an eye-ranged battery opens on `GunneryOpenS` alone; opening at
  once instead hit the same (9.4 v 9.9%), its early salvos 970 m off: left.
- The priors (`prior_kn`, `prior_deg`): the eye's first guess at her course and speed, before any reading; tighter on
  every later machine (4/25 the hand plot, 2.5/15 the Dreyer, 2/12 the AFCT). Physical if the instruments that make the
  guess improve (the rangefinder's ×28 on her inclination); check in FL4-FL6 where it's the same eye.
- The captain's ranging record (`RangingRecord`, BL1ae): its cycles count the turrets' first swing from rest as ranging;
  a shift's own swing is its change of bearing over the training rate (a target on the other beam, 90 s at 2°/s).
- `GunneryCombat`: the lumped battle factor, goes when fatigue comes (no-lumped-battle-factors).
- `first_step_m`: doctrine's, per navy's practice; FL3-FL5.

## FL1: own turns (done 2026-11-02)

**Built:** `own_turn_kick_m` gone (retired: a design saved with it still checks). A machine that takes her motion in
(helm-free, or in true motion) works from its own reckoning of her (`GunnerySystem.Reckon`, `Fc.OwnHat*`): her course
by the gyro's repeat motor, geared to the table, so at once (the user, 2026-11-02: "Dreyer table is a mechanical
instrument. Is there any expectation of any sort of lag whatsoever?"); her speed late by the machine's
`own_speed_lag_s`, a first-order lag [EST: 30 s a Forbes log read and set by hand (the Dreyer, the German plot, the
Argo), 20 s the Type 92, 3 s a log fed automatically (the AFCT family, the Mk 8), 2 s the Mk 1A, 1 s digital]; her
place dead reckoned from them. Its readings are taken where it reckons her and its gun orders laid off from where she
is (`Reckoned`, `Ordered`). The ladder's `own` rung scales the lag. A plain clock takes nothing in and is put out by her
turns as before. The officer's worth reads his table's speed off hers over the flight and the plot's latency. The
gyrocompass's own error after a manoeuvre is a research request (`research/REQUESTS.md`, 2026-11-02); if it finds
nothing substantial, this stays.

**Measured** (QE on QE, 16 kyd, 16 duels, her own zigzag 10° either side every 90 s): every error on, level 1 12.1%,
level 3 14.6% (the kick's 10.6%), level 5 18.9%; steady, unchanged. A Yamato weaving 20° every minute at 8 km hits
as with her speed fed exactly (648 v 650 of 969); a Lion in 90° legs every two minutes at 12 km sheds speed and hits
126 against 156 exactly (**not so**, FL2: 8 duels' noise; her speed's lag costs little anywhere). **On the way:** a first try gave the course a 2 s lag [EST] too: the Dreyer's zigzag fell
from 83 to 16% on the readings' rung (2° of bearing at 15 km is 500 m), and at a quarter of the lags it held: a cliff
too sharp for the error, so it looked as if something in the chain amplified a bearing error that size. **Checked (no
amplifier):** that error put back by hand into the readings' bearings, one duel traced salvo by salvo: the aim across
the line off by 50-220 m, where her rate of turn (at most 0.47°/s) times 2 s at 14.6 km is 100-250 m, one for one; the
range untouched, no reading set aside by the gate, no return to ranging. The cliff is the target: broadside to us she is
some 190 m long, so a quarter of the error (65 m) falls along her and the whole (260 m) off her ends. Hits 96 → 24.

**The research** (`research/simulation/ship-motion/own-motion-inputs.md`, 2026-11-02): FL1 stands. The gyrocompass's own
error after a turn (≤ 1.3° at 50-60°N, decaying over hours) turns her course and every bearing together, since the same gyro
fed the Dumaresq and the bearing plot, so only its rate reaches the solution, ≤ 15 m at 15 km: not built. Course "at once" is
sourced for the Dreyer Mk I*-V, the Pollen plotter and the Mk 8. **One exception built:** the Type 92's operator follows the
gyro's pointer by hand ("Follow up compass course as received from the gyrocompass"), so its course is late while the
director's train comes in at once, and its bearings turn by the lag: `own_course_lag_s` (0 everywhere else), 0.5 s [EST, the
user: "I definitely don't have that much latency if I just follow something"]. Measured in the levels' zigzag (readings'
rung): 83.8% with her course exact, 83.8 at 0.5 s, 73.4 at 1 s; every error on 16.8, 17.0, 14.7
(`GunneryTests.The_Type_92_follows_her_course_by_hand_half_a_second_behind`). **Measured again in FL2:** `all=0` sets the
own rung to 0 too, so the 0.5 s run had her inputs exact; with `own=1` on the readings' rung 81.8% exact, 82.1 at 0.5 s,
68.8 at 1 s, every error on 18.3 v 17.2 (the turrets now training at first sight): the conclusion stands. The rest went where each belongs: the stable
vertical in a turn to FL7, the apparent wind to the `resid_pct` audit, the speed lags and the drift angle to the audit list,
the Dreyer Mk I/II split to FL5.

## FL2: the opening (done 2026-11-02)

**Should give:** level 3's 4 m rangefinder reads her range about three times better at 16 kyd than level 1's FA 2 m
(σ some 110 against 360 m a reading), and its Dreyer's priors are tighter: its first salvo falls nearer her, so it
straddles more often and comes to fire for effect in fewer salvos. Neither should wait longer than its track needs.

**Found:** the measure was wrong, not the mechanism. The first straddle's median time over 16 duels (143 v 141 s) sat on
the second salvo for both: level 3's first straddles a little under half the time. The opening's timeline (QE on QE at
16 kyd, her abeam, the turrets at rest fore and aft): sighted at 7 s; the first reading through the plot 15 s later (its
latency); settled at once, both levels (twice its doubt within two first steps of 366 m), so neither `GunneryOpenS` (120
s) nor the settled test held either; the turrets ordered only then, training 88° at 2°/s, 44 s; level 3's director then
waiting up to a roll to fire (research §4.4). First salvos at 76 and 82 s. The falls, every error on, 64 duels:

| | level 1 | level 3 |
|---|---|---|
| first salvo's fall, rms | 349 m | 210 m |
| first salvos straddling | 28% | 44% |
| firing for effect by the third salvo | 86% | 100% |
| hits in the first 5 min | 109 of 1,280 | 130 of 1,280 |

**Built:** the turrets train on her as she's seen (the director's train, or each trainer on his own sight) from first
sight, not when the plot has its first range; their elevation waits for it (`GunnerySystem`'s computer step, before the
first reading). The first salvo 15 s sooner for both (61, 67 s). `GunneryTests.Where_the_levels_differ_level_3_does_better`
measures the first salvo's fall (32 duels): readings' rung 200 v 122 m, every error on 407 v 214 m with 25 v 38%
straddling.

**On the way:**
- `all=0` sets the own rung to 0 as well: every "all=0,readings=1" run had her own inputs exact. FL1's Type 92 check
  compared exact with exact; measured again (FL1's section), it stands. The levels' zigzag on the readings' rung has the
  own rung on now.
- FL1's Lion (her 30 s hand-fed log costing hits in 90° legs, 126 v 156 every error on) was 8 duels' noise: 138 v 140
  since, 374 v 360 on the readings' rung, 366 with the log two minutes late. Why: the table reckons her readings and her
  gun orders from the same place, so its dead reckoning's drift cancels; what's left is her speed's error now times the
  flight and the latency, bounded by the speed she sheds (1.5-3 m/s at 18 kn, 50-100 m) however late the log.
  `FireWorthTests.Her_own_turns_cost_a_helm_free_table_little` says so.
- The captain's cost of a shift (BL1ae): the battery's ranging record came down 15 s, to about 70 s on the bench; 12 km
  against 13 is a tie at that (8% nearer over his 600 s is 48 s), so `FireOrderTests`' "a little nearer" case is 12.5 km
  now. The record still counts the turrets' first swing as ranging (audit list).
- The bracket: level 3's second salvo falls no nearer than its first (210 → 206 m rms, every error on). Its first step
  (twice its doubt, mostly the 1.2% residual bias, capped at 366 m) moves the fall the step's length on the spot's sign
  alone, however near the first fell: bracket and halve, as doctrine had it. Checked where `first_step_m` is (FL3-FL5).

## FL3: the eye to the rangefinder (done 2026-11-02)

**Should give:** the eye guesses her range to some 20% (1,400 m at 7 km), and a man misjudges a ship the same way all
through, so his readings don't average it out; the FA reads it to some 100 m. Level 0's first salvo should fall far off
and it should range for minutes where level 1 ranges in a salvo or two. Open sights lay to 1.15 mil, the early telescope
to 0.73: some 75 against 50 m in range a gun at 7 km, a wider pattern about the same centre, fewer hits firing for effect.

**Found:** both show, each on its rung (Mikasa on Mikasa at 7 km, 15 kn, 48 duels, 10 minutes):

| | level 0 | level 1 |
|---|---|---|
| readings' rung: the first salvo's fall, rms | 788 m | 174 m |
| readings' rung: ranging salvos; effect at (median) | 3.8; 271 s | 1.8; 131 s |
| laying and pattern's rung: hits | 19.0% | 22.3% |
| every error on: hits | 9.9% | 16.1% |

**On the way:** level 0's computer, the reference's "eye and hand plot", was the hand plot with worse numbers for no
difference in the plot: 20 s latency for 15, the eye's guess at her course and speed 5 kn and 30° for 4 and 25 (the same
eye), the corrections' residual 2.5% and 4 mil for 2% and 3 (the same tables), a 550 m first step for 366 (the same
navy). Gone: level 0 is the hand plot with the eye (with that row level 0 hit 10.2% every error on, without it 9.9%: its
deficit was the eye's, not the numbers'). The judgement (local control's computer) had the same numbers and takes the hand
plot's error budget too, keeping its own (no plot: 5 s; no rate ever learnt). A fit's level label tells the eyeball from
the rangefinder by its instrument now that they share the plot (`FireControlPresets.LevelOf`).
`GunneryTests.Where_the_levels_differ_level_1_ranges_where_the_eye_guesses` asserts both on their rungs.

## FL4: the hand plot to the Dumaresq and clock (done 2026-11-02)

**Should give:** before the Dumaresq "range rate was ignored, or guessed by rule of thumb", and "with no rate aid, the
sight carries that range unchanged" between ranges passed down (manual-gunnery-research.md §3.2); the hand plot's rate
was the trend of its plotted ranges (the "analytic" method, which the synthetic beat in trials: by 1919 the RN held
"range rate could not reliably be taken from the range-plot trend alone", computing.md §2). The Dumaresq makes the rate
from her course and speed as estimated at once, and the Vickers clock runs the range on at it. So against a target
closing across, the plot ranges longer and its held range goes stale between plots; on a parallel course (no rate) no
difference.

**Found:** none in the chain. Both were the same rate clock, the plot starting from the guessed course and speed and
running the range on as the clock does: level 1 had level 2's mechanism, the difference left to its numbers (Dreadnought
on Dreadnought at 11 km behind the same FQ/FT and sights, readings' rung: closing across 68.2 v 69.0%, parallel 69.2 v
70.0).

**Built:** a computer's `course_rate` and `range_clock` (true from the Dumaresq on, false on the hand plot; the designs'
own hand plot blocks have them). Without a course rate its rates start at none, as doubtful as her speed and ours
together, and come from the readings alone (`FcTracker.Start`); without a clock the range passed at each plot stays on the
sights till the next, the trainers on her bearing (`GunnerySystem.Predict`). Measured (15 and 18 kn, 32 duels):

| | hand plot | Dumaresq and clock |
|---|---|---|
| closing across, readings' rung: hits; first fall; effect at | 59.2%; 367 m; 147 s | 69.0%; 165 m; 99 s |
| parallel, readings' rung: hits | 70.8% | 70.0% |
| closing across, every error on: hits | 14.8% | 16.3% |

The step's other parts each on its rung, behind the clock: the FQ/FT's first salvo nearer than the FA's (151 v 103 m rms
on the readings' rung); the telescope's 0.35 mil over the early one's 0.73 a little (14.5 v 15.0% firing for effect: some
27 against 56 m in range a gun at 11 km beside a pattern of some 200). `GunneryTests.Where_the_levels_differ_level_2_runs_the_range_on`.

**On the way:** the hand plot's priors (4 kn, 25°) now set only what it doesn't use (its guess goes unread without a
Dumaresq); the judgement still leads by its guess (the rule of thumb). Haze losing readings, the row's other place, is
the same mechanism with sparser readings: not measured apart.
