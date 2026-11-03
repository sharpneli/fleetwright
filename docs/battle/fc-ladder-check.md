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
| FL5 | 2 → 3 | Dumaresq → Dreyer Mk III-V (helm-free, the bearing gear); local laying → director 0.5 mil | through her own turns (FL1); the director's layer above the smoke and spray, one fall to spot | her own zigzag; a seaway; her own and the enemy's smoke, spray at speed. On the way: the catalogue's "Dreyer table Mk I-II" (not helm-free) fits the Mk I only; the Mk I* took her heading automatically and the Mk II probably (own-motion-inputs.md §2): split the row | done 2026-11-02 (the smoke and spray's size to the error sources' session) |
| FL6 | 3 → 4 | Dreyer (rate clock, latency 15 s, bias 1.2%) → AFCT (true course, 8 s, 0.6%); FQ/FT → stereo 5 m; director power drive 0.4 mil | a target's turns re-learned sooner; the bearing rate past the Dreyer's 0.25°/s; less bias to range out | the target's zigzag; a fast crossing near | done 2026-11-02 |
| FL7 | 4 → 5 | AFCT → Ford Mk 8 (3 s, 0.4%); stable vertical, RPC; coated, stabilised O4 | fire at any point of the roll, not on it; the rangefinder in a seaway; and the stable vertical's own error in a turn, the record's "several hundred yards" (North Carolina 1945): its mercury erection cut out while the turn's acceleration lasts, the gyro drifting free some 2-3′ a minute, ~200 yd through a 450° turn at 20-25 kyd, re-erecting after (own-motion-inputs.md §4, recommendation 6) | a seaway, rate of fire in it; long own turns | done 2026-11-02 (the rangefinder in a seaway to the error sources' session) |
| FL8 | 5 → 6 | radar ranging (stand-in) | ranges through haze and at night | haze, night: radar is out of the first game, so checked last | skipped for now (the user, 2026-11-02: no radar yet) |
| FL9 | 6 → 7 | Mk 8 → the curved-course analog (acceleration, turn detection; the catalogue's "Mk 1A computer" till FL9) | follows a target in a steady turn ("a steady turn cuts hits 2-4x for every straight-line machine", research §5.6) | a target circling | done 2026-11-03 (the walk on a circle to the salvo chasing) |
| FL10 | 7 → 8 | the curved-course analog → digital turn filter | the same, quicker; a zigzag; fooled by a weave (evasion-research.md P4) | a target circling, zigzagging and weaving | done 2026-11-03 |
| FL11 | side steps | Argo (true course 1912, gyro-mounted rangefinder 8 a minute) v Dreyer; AFCT turn setting v AFCT; Type 92 v AFCT | the Argo through her own turns and in a seaway; the turn setting on a circling target; the Japanese plot per its record | as each says | done 2026-11-03 (the Argo's seaway to the error sources' session) |

## Numbers to audit on the way

Each is checked where its step is, as `own_turn_kick_m` in FL1: is it a physical quantity of the machine, sourced, or a
lumped stand-in for something the chain now models?

- `own_turn_kick_m`: FL1.
- `resid_pct`, `defl_bias_mil`: the residual ballistic bias as the corrections a machine computes (wind, density, drift);
  per machine is right if it says which corrections it has; checked when wind and density come in. Then too: a machine
  whose wind correction was set by hand from the apparent wind (the Ford Mk 1, likely the Dreyer) keeps its old setting
  through a turn until reset; "the later rangekeepers ... need not be corrected for changes in course" (own-motion-inputs.md §4).
  **Done in error-sources.md ES3 (2026-11-03):** both gone, each machine's wind input (none, deck, ballistic) and its
  errors in the wind, the density and the muzzle velocity in their place. The hand-set wind's staleness through a turn
  isn't built: each machine sets the true wind it measured, resolved along the line as the line moves.
- `own_speed_lag_s`: 30 s hand-fed [EST] sits in the sources' 10-30 s; the AFCT family's 3 s "automatic" has no source, one
  weak clue says set by hand (own-motion-inputs.md §1): revisit if its path turns up.
- Drift angle: a ship's track lags her heading in a turn, 1-3.5 kn of her velocity in a hard one, more than the speed lag;
  not in the motion model (`ShipMotion` moves ships along their heading). If it comes in, every machine's own velocity
  (heading times log speed) picks it up with no change to `Reckon`.
- `lay_mil` by mount: a director (0.5 mil) lays worse than a turret's telescopic sights (0.35), and a stable vertical with
  RPC (0.5) worse than a director's power drive (0.4), on a steady platform. Check the sources' split (BL1l) in FL5 and FL7.
  FL5: the director's 0.5 is the reference's 1.0 halved by its own split; the research's one director figure, "about
  0.5-1 mil with ×12 trainer and pointer optics" (fire-control-research.md, inferred from the Mk 38), doesn't argue it
  lower, and the layers' lead in clear air fits the record: kept. FL7: the stable vertical's 0.5 against the power
  drive's 0.4 in a calm matters little (its lead is firing anywhere in the roll, 14% more shells even in a calm) and
  has no source either way: kept.
- An unstabilised rangefinder in a seaway: the gyro mounting's gain is a fixed reading rate (fire-control-research.md
  §3.5: Pollen's mounting "raised range-taking about fivefold"; 2 against 8 a minute, 4 against 6), the same in a calm as
  in a gale, and nothing slows an unstabilised instrument's readings or widens them as she rolls. Its law wants a source:
  the error sources' session (FL7).
- `latency_s`, `q`, the priors: the machine's own assumptions; fine as such, each checked by its step.
- `GunneryOpenS`, `OpenDoubtSteps`: FL2, neither held the opening on the bench (the track settles on its first reading at
  16 kyd for both levels; the turrets' swing held it). Their own check wants a case the swing doesn't cover: a shift.
  FL3: an eye's doubt never comes within two steps, so an eye-ranged battery opens on `GunneryOpenS` alone; opening at
  once instead hit the same (9.4 v 9.9%), its early salvos 970 m off: left.
- The priors (`prior_kn`, `prior_deg`): the eye's first guess at her course and speed, before any reading; tighter on
  every later machine (4/25 the hand plot, 2.5/15 the Dreyer, 2/12 the AFCT). Physical if the instruments that make the
  guess improve (the rangefinder's ×28 on her inclination); check in FL4-FL6 where it's the same eye. Done to the AFCT:
  the Dumaresq's 3 kn and 20° for every machine from it through the AFCT family (FL5, FL6), the same eye behind ×28;
  the Argo and the Type 92 (FL11: the Dumaresq's now), the Mk 8 on (FL7, FL9, FL10) still their own. FL7: the Mk 8 too; its radar
  stand-in row keeps 2 and 12 till FL8. FL9: the curved-course analog's 2 and 12 stay with the radar stand-in it's fitted
  behind (FL8).
- `accel_sigma_ms2`: the curved-course analog's allowed acceleration; FL9 (0.1 → 0.25, a capital ship's in a hard turn).
- `turn_q`, `speed_q`: the digital filter's; FL10 (`turn_q` 5e-7 → 2e-5, a capital ship's helm; `speed_q` 0.1 checks). The AFCT
  turn setting's 1e-7 (a hand-set rate) is FL11's.
- `latency_s`: the Dreyer's 15 s and the Mk 8's 2-3 s are the research's (evasion-research.md, "about 15 s for a Dreyer table, 2-3 s for a Mk 8"), the AFCT's 8 s
  between them; on a steady target it's what's left between the Dreyer and the AFCT besides the residual (FL6).
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

## FL5: the Dumaresq to the Dreyer, the layers to the director (done 2026-11-02)

**Should give:** the Dreyer from the Mk I* takes her course from the gyro and keeps its solution through her turns,
where the Dumaresq, its course set by hand, is put out by each (FL1's mechanism, now between these two). The director: in
good visibility nothing over a good layer at his own telescope, perhaps less (the trial repeated in good visibility:
"Orion (individual aim) did much better and apparently beat Thunderer", emergency-eyeball.md); in smoke and a heavy sea
all ("Thunderer 26 of 39
rounds, Orion 4 of 27", the 1912 trial at 9,000 yd; "smoke over the guns is the main reason individual aim collapsed").

**Found:** her turns as they should (QE on QE at 16 kyd behind the same FQ/FT and director, 10° either side every 90 s,
readings' rung: the Dumaresq 9.7%, the Dreyer 78.0%; every error on 10.3, 16.2). The guns in clear air at 9,000 yd
(behind the Dreyer, readings, laying and pattern): the layers 57.3%, the director 47.5%; every error on 54.1, 44.7. The
director fires all its guns at one instant of the roll, his error common to the salvo, at 0.5 mil to the telescope's
0.35 (audit list): the record's good-visibility result, if wider. **Missing:** a gun laid by its own layer fired on what
the control top saw; nothing asked whether the man at the turret's sight could see her.

**Built:** a gun laid by its own layer fires only while he makes her out from his sight, on the gunhouse's roof as local
control takes him, by eye through his telescope, through the smoke there (`GunnerySystem.LayerSees`; asked only in smoked
air: in clear air a sight lower down sees what the top does). It runs (24 duels, 8 km, a 4 m/s wind across: layers blind
in 4% of their checks, 9% with a destroyer making smoke between) but costs almost nothing yet (2,176 against 2,168
shells): the smoke field never lies dense and low over her guns for long, and spray on the turrets' sights isn't in the
chain (ship-motion-gunnery-research.md summary 1: spray and smoke, not vibration). Both are the error sources' session's
(the user, 2026-11-02: a big session of error sources and harsher weather next); the 1912 trial is its check.

**On the way:** the catalogue's "Dreyer table Mk I-II" is "Dreyer table Mk I" (her course by handwheel); the Mk I* and Mk II
were helm-free (own-motion-inputs.md §2), as the Mk III-V row, which says so. The Dreyer's and the German C12's first
guess at her course and speed was tighter than the Dumaresq's (2.5 kn and 15° against 3 and 20) with the same FQ/FT and
the same eye, the Dreyer holding a Dumaresq: the Dumaresq's now (the catalogue and 51 designs' own blocks). The residual
biases (1.5% against 1.2) wait for the wind (audit list). `GunneryTests.Where_the_levels_differ_level_3_keeps_its_solution_through_her_turns`.

## FL6: the Dreyer to the AFCT (done 2026-11-02)

**Should give:** the AFCT tracks her true course and speed, the Dreyer her range and bearing at constant rates clamped at
±1,200 yd/min and ±15°/min: on a fast target crossing close, past the bearing gear and with the range rate changing
through her passing, the Dreyer drifts where the AFCT follows; on her zigzag, both straight-course machines, the AFCT
re-learns sooner by its shorter latency (8 s against 15); steady, alike but for what the machines are (the latency, the
residual bias the officer expects of his). The long-base stereo (5 m, 4 readings a minute) over the FQ/FT, and the
director's power drive (0.4 mil) over following the pointer (0.5), a little each on their rungs.

**Found** (QE behind the same FQ/FT and director; the readings and the priors, 24 duels):

| | the Dreyer | the Dreyer with the AFCT's latency and residuals | the AFCT |
|---|---|---|---|
| a destroyer at 30 kn passing at 4 km | 38.9% | 60.4% | 73.5% |
| the same at 5 km | 55.8% | 66.0% | 77.4% |
| her zigzag, 20° either side every 75 s at 14 km | 17.6% | 20.6% | 20.6% |
| steady at 14 km (readings alone) | 67.0% | 74.8% | 74.7% |

The rate clock and the true course are one filter on a steady target and on a zigzag; the tracker's own part is the fast
crossing (60 against 74%). Every error on, the destroyer passing: 33.1 against 48.7%. Passing another battleship at 4 km
the Dreyer's limit hardly shows: the bearing it loses (some 120 m over the flight and latency) lies along her 190 m.
The stereo over the FQ/FT 70.1 → 75.7% on the readings' rung at 16 kyd, the power drive 21.2 → 21.4 on the laying's.

**On the way:** the AFCT family's first guess (2 kn and 12°) was tighter than the Dreyer's with the same eye behind the
same ×28: the Dumaresq's 3 and 20 now, the AFCT, the AFCC, the turn setting and the German Rechenstelle (the catalogue and
7 designs' own blocks). `GunneryTests.Where_the_levels_differ_level_4_follows_a_fast_crossing`.

## FL7: the AFCT to the Mk 8 and the stable vertical (done 2026-11-02)

**Should give:** the stable vertical fires anywhere in the roll, "tilt-corrected" (ship-motion-gunnery-research.md), where
the director's layer fires on it at the key: in a seaway the director waits for her roll and lays worse as it grows, the
vertical neither. The Mk 8 follows a target more closely than the AFCT by its shorter latency (2-3 s against 8) and its
readier manoeuvre allowance, alike on a steady one. The O4 a little over the O3. And the vertical's own error in her long
turns (own-motion-inputs.md §4): its mercury erection "cannot distinguish between gravity and acceleration forces", so
it was cut out in a turn and the gyro "free to drift until the acceleration disappeared", some 3′ a minute; a 450° turn
of 4-5 minutes leaves 12-15′, about 200 yd at 20-25 kyd (North Carolina 1945: "several hundreds of yards at battle
ranges", aiming at the sun and moon).

**Found** (QE refitted; 24 duels): the mount as it should, behind the O3 and the AFCT at 16 km:

| hits a run (of shells) | the director, power drive | the stable vertical |
|---|---|---|
| calm | 282 (1,876) | 318 (2,148) |
| a 2 m swell abeam | 271 | 324 |
| a 4 m swell abeam | 250 (1,868) | 317 (2,144) |

The director's 15.0% a shell falls to 13.4 as she rolls more, the vertical's holds at 14.8; even in a calm the director
waits for what roll there is (14% fewer shells). The computer: steady alike (the AFCT 76.2%, the Mk 8 75.2), a destroyer
at 30 kn passing at 4 km 83.6 against 90.3. The O4 over the O3 at 20 kyd, 58.8 → 61.0% on the readings' rung (48 duels:
a rangefinder man's misjudgement, drawn once a target, spreads the duels wide). **Missing:** the vertical's own error.

**Built:** the stable vertical's tilt (`GunnerySystem.Vertical`, `Fc.SvTilt*`; the tally's `VerticalTiltRad`): cut out
while her turn's acceleration (speed times rate of turn) passes `StableVerticalCutMs2` [EST 0.05 m/s², the research's],
drifting one way drawn each turn at `StableVerticalDriftArcminMin` [EST 2.5: the research's 2-3, the free gyro of 1921 "three
minutes of arc per minute of time"], erected again after over `StableVerticalErectS` [EST 120 s: "over minutes"]; along
the line of fire it lays the guns' elevation off, across it cants their trunnions; the own rung's. A director lays to the
horizon and has none. Measured, QE's 450° at 18 kn with 20° of helm (7.2 minutes): 17.6′ at its end; the falls after it
44 m rms against 45 with the vertical exact, the spots walking the drift out as it grows, some 36 m of range a minute at
20 kyd (North Carolina's verdict: "no more than would be desirable when using a rocking ladder"; commanders "might
maneuver freely"). Kept a battery's, not its ship's: a shift of target starts it level (a small omission).

**On the way:** the Mk 8's first guess (2 kn and 12°) the Dumaresq's 3 and 20, the same eye (the catalogue and 11
designs' own blocks); its radar stand-in row keeps its own till FL8. An unstabilised rangefinder in a seaway isn't in the
chain (audit list). `GunneryTests.Where_the_levels_differ_level_5_fires_anywhere_in_the_roll`.

## FL9: the Mk 8 to the curved-course analog (done 2026-11-03)

**Should give:** the Mk 8 takes her on a straight line (NavPers Ch. 19-F: "a target changing course or speed gives
inaccurate gun orders until rate control catches up"), so on a steady circle it chases a velocity that keeps turning and
never converges; the curved-course predictor carries her acceleration and projects along her curve (Sperry's target course
predictor, filed 1954: "from a straight line to a parabola, using smoothed measured acceleration"). The research's model
(§5.6, table 12): the Mk 8 with radar 230-300 yd off over a 25 s flight at 1°/s, level 7 about 100. On a straight target
a little worse for the extra states (level 7 settles at 1.7 kn against 1.0); on a zigzag, between.

**Found** (QE refitted behind the same O4 and stable vertical, QE abeam at 16 km circling at 18 kn with 15° of helm,
some 0.96°/s, her speed falling to 15 kn; the salvos from a minute after the helm went over; 24 duels):

| | the Mk 8 | the curved-course analog | the digital turn filter (FL10) |
|---|---|---|---|
| exact readings: the falls' error along the line, rms | 304 m | 172 m (σa 0.1); 129 (0.25) | 65 m |
| every error on: hits while she circles | 1.6% | 4.5% (σa 0.1); 6.0% (0.25) | 9.5% (10.7 since FL10) |
| every error on, behind the auto-track stand-in | 3.4% | 8.3% | |
| steady, readings' rung | 98.5% | 98.9% | |
| her zigzag (20° every 75 s at 14 km), readings' rung | 25.7% | 32.9% | |

The step shows on its rung and with every error on; behind the radar stand-in both, the research's levels 6 and 7 (3.5
and 7.9% at 1°/s) within the duels' noise. The steady cost the research found doesn't show at these readings.

**On the way:**
- The catalogue's "Mk 1A computer" was the 1950s machine: the reference's level 7 is "Auto-track radar, analog (Mk 56/68,
  1955)", and the research has the Ford Mk 1A, the WWII Mk 37's computer, as a straight-line machine with the "target just
  turned" button (§5.6). Renamed "Curved-course analog computer (1950s)" (the catalogue, the 8 post-war designs' own
  blocks, 36 goldens); a design saved with the old name keeps its own block. The Mk 1A itself, a true-course machine for
  the WWII dual-purpose batteries, isn't in the catalogue.
- Its allowed acceleration (`accel_sigma_ms2`) was 0.1 m/s² 1σ, unsourced; a circling QE pulls 0.14, so she sat at 1.4σ
  and the filter was slow to believe her. Now a capital ship's in a hard turn, her speed times her rate of turn: 0.25, the
  reference's own default ("a 25 kn ship turning 1°/s ~ 0.22"). The memory (`accel_tau_s`, 20 s) is the reference's and
  stays: 40 s followed the circle a little closer, but nothing sources it.
- **"Target just turned" never fires** at a surface target here (with and without it alike, on the circle and on 40°
  zigzag legs): two 99.9% surprises in a row need an innovation her turn never makes between readings this noisy. The
  real button was a man's: the director's crew saw her turn (her inclination, her heel, her wake), as the eye guesses her
  course to its prior (12-20°). A turn seen by eye at its inclination error is a candidate for the button's model; not
  built.
- **The walk on a circle** (for the salvo chasing): a straight-line machine's falls swing as she circles (the Mk 8's from
  160 m short to 450 m over and back, every 6 minutes at 1°/s) and the walk can't follow. Its step is the opening
  bracket's half-width over its count, floored at 46 m, and grows back to that half-width at most, so fire for effect
  moves the aim 46 m a spot at most, about 100-140 m a minute, where the Mk 8's error grows about 120 m a salvo; and the
  falls off her line as she turns end-on go doubtful (no range spot), which breaks the three calls alike its growth and
  the range-lost rule wait for. The deflection is walked on doubtful calls too, at half the error a salvo, and lags its
  turning error by some 80-160 m. Spotting can't remove a standing error (research §5.6 summary 4), but a swinging one it
  could partly follow: the salvo chasing's work, where evasion lives.

## FL10: the curved-course analog to the digital turn filter (done 2026-11-03)

**Should give:** the digital filter carries her rate of turn (a coordinated turn: speed, heading and rate), so it follows
a steady circle where the analog's decaying acceleration lags: the research's model 18-30 yd on a 1°/s circle against
about 100, hits on the circle near steady (§5.6 table 11: 21.5-24.4 against 24.2%). A zigzag's turns learnt and shed
sooner (a ±30°/120 s zigzag costs it a third at 20 kyd, the Mk 8 half: evasion-research.md table 4). And its own weakness:
a weave fools it, "it over-extrapolates every turn" (P4; table 4: 15.1 → 6.9% at 20 kyd), where a straight-line machine
averages a weave out (7.1 → 6.7). Steady, a little worse for the extra states.

**Found** at the reference's turn-rate noise (`turn_q` 5e-7 rad²/s³ [INFERRED]): the circle as it should, the rest not
(QE refitted behind the same O4 and stable vertical, QE abeam at 16 km at 18 kn, 24 duels, the readings' rung):

| | the curved-course analog | digital, `turn_q` 5e-7 | digital, 2e-5 |
|---|---|---|---|
| steady | 95.8% | 91.0% | 94.0% |
| her circle (15° of helm, ~0.96°/s): the falls rms; hits | 150 m; 0.0% | 68 m; 38.0% | 48 m; 56.8% |
| her zigzag, ±30° every 2 min | 14.2% | 7.3% | 19.2% |
| her weave, ±30° on a 90 s sine | 30.0% | 41.4% | 17.3% |
| every error on: steady; circle; zigzag; weave | 16.8; 6.8; 13.0; 13.9% | 14.5; 11.4; 7.7; 14.4% | 16.6; 12.2; 13.0; 12.9% |

The Mk 8 on the weave 58.0% (the readings' rung): a straight-line machine averages it out, as the research has it. At
5e-7 the filter learnt a turn and shed it so slowly that it fell behind the Mk 8 on the zigzag (7.3 against 9.8%), kept
turning her in its prediction after she steadied, and wasn't fooled by the weave because it never followed one.

**Built:** `turn_q` from what it stands for, how fast a ship's rate of turn can change: the rate hard over makes, squared,
over the time her yaw takes to answer it. QE at 18 kn settles near 1.3°/s (0.023 rad/s) over Nomoto's T = T′L/U = 1.375 ×
189 / 9.26 = 28 s: 0.023² / 28 = 1.9e-5, so 2e-5, a capital ship's (as FL9's σa). `speed_q` the same way checks: she loses
a quarter of her speed (2.4 m/s at 18 kn) in a minute's hard turn, 2.4² / 60 ≈ 0.1, the row's. With it the profile is the
research's: the circle followed, the zigzag ahead of the analog, the weave its weakness, steady alike. No shipped design
fits the digital filter. `GunneryTests.Where_the_levels_differ_level_8_follows_her_turns` (behind the level's digital
ranging stand-in too, printed: the circle every error on 7.4 against 11.3%).

**On the way:**
- Its price: noise when she does nothing new. On the trackers' own bench (`FcTrackerTests`: clean readings every 2 s, 10 m,
  a 25 kn target circling at 1°/s from a step) it holds a straight course to 73 m where it did to 32, and the circle to
  47 where it did to 20, still a third of the true-course machine's 150. In the duels the noise sits under the spotting
  and the pattern: steady, the two alike from 420 s on (98.2 against 97.9%), the old one behind only in the opening
  (77.7 against 87.2% over 180-420 s), where a filter that slow keeps its first error for minutes.
- A destroyer's helm makes 3°/s over some 7 s (3.7e-4): one number a machine is tuned for a capital ship's turns, so it
  learns a destroyer's late. A filter per target class, or adaptive (the 1980s IMM), is the next machine's.
- "Target just turned" never fires here either (FL9), so the filter opens up on nothing but its own noise.
- The weave fools it less with every error on (12.9 against steady 16.6%, the research's 15.1 → 6.9 at 20 kyd): QE at
  18 kn turns about 1.3°/s hard over and a ±30° sine on 90 s asks 2.1°/s, so she weaves less than asked.

## FL11: the side steps

### The Argo beside the Dreyer (done 2026-11-03)

**Should give:** Pollen's Argo clock is a true-course plotter with a variable-speed clock, "better on rapidly changing
range rate than the Vickers clock" (computing.md §3), so on a fast crossing it follows where the Dreyer's constant-rate
clock drifts (FL6's mechanism, a decade earlier); its gyro mounting reads four times as often (8 a minute against 2,
research §3.5), a tighter hold everywhere; through her own turns alike, both helm-free (FL1); and in a seaway the
mounting's real gain, a rangefinder that holds her in the roll.

**Found** (QE refitted behind the same director, the readings' rung, 24 duels):

| | the Dreyer | the Argo | the Argo clock, plain FQ/FT | the Dreyer on the Argo mounting |
|---|---|---|---|---|
| a destroyer passing at 4 km at 30 kn | 38.9% | 83.2% | 67.3% | 54.5% |
| steady at 14 km | 61.8% | 71.3% | 67.1% | 69.6% |
| her own zigzag (10°/90 s at 16 kyd) | 78.2% | 78.3% | 79.6% | 77.1% |

Every error on, the destroyer passing 32.9 against 53.6%. Both parts as they should, each on its own. **Missing:** the
seaway. The mounting's gain is only its reading rate: nothing makes an unstabilised instrument's readings slower or worse
as she rolls (audit list), so in a seaway the Argo leads by no more than in a calm. The error sources' session's.

**On the way:** its first guess (2.5 kn and 15°) was tighter than the Dumaresq's with the same eye behind the same ×28:
the Dumaresq's 3 and 20 now (no design fits it; steady 69.3 → 71.3, within the noise).
`GunneryTests.The_Argo_follows_a_changing_range_rate_the_Dreyer_doesnt`.

### The AFCT's turn setting beside the plain table (done 2026-11-03)

**Should give:** a hand-set enemy rate of turn ([UNCERTAIN] in the record: a forum report citing Friedman, "the only
big-gun computer that could handle a target in a constant turn"; an option anyone may fit, decision 5), so on a steady
circle a standing error becomes a transient, the rate set as the plot shows her curve (research §5.6: 4.0 → 7.1% at
0.5°/s, 2.9 → 7.4 at 1°/s; its error 520 yd a minute into the circle, 120 after four); alike steady and zigzagging, but
settling more slowly than the plain table ("the price of handling the circle", §5.4).

**Found** (QE refitted behind the O3 and the power drive at 16 km, 24 duels):

| | the AFCT | the turn setting |
|---|---|---|
| her circle (15° of helm), the readings' rung: the falls rms | 397 m | 151 m |
| her circle, every error on: hits; at 7° of helm (~0.5°/s) | 2.4%; 2.9% | 8.4%; 8.6% |
| steady, the readings' rung (48 duels): 180-420 s; from 420 s | 82.6%; 97.7% | 56.4%; 97.7% |
| her zigzag (±30°/120 s), every error on | 6.1% | 4.8% |

As the research has it, the circle and the slower settling both. Its rate adapts at 1e-7 rad²/s³, a man's hand: at the
digital filter's 2e-5 it's a digital filter at the AFCT's latency (the circle 12.3%, a weave 12.5 against 15.2), not a
setting, so it stays. **On the way:** the opening's cost is partly the turn filter's first doubt of her rate of turn,
1°/s 1σ for every turn filter (`FcTracker.Start`, the reference's): a hand-set rate starting at 0.1°/s (as if set at
nought till he sees her turn) opens 65.1% instead of 56.4, the rest the extra states. Not changed: no source for a man's
first judgement of her rate of turn. `GunneryTests.The_turn_setting_follows_her_circle_the_plain_table_doesnt`.

### The Japanese plot beside the AFCT (done 2026-11-03)

**Should give:** the Type 92 Shagekiban takes its range rate from a range-rate plot and its deflection set by hand from
her speed (the Sokutekiban's), "target speed the only target-motion input" (computing.md §3): a constant-rate clock,
helm-free by a man following the gyro's pointer (FL1). So on a fast crossing it drifts where the AFCT's true course
follows (FL6's mechanism); elsewhere near the table. Its record is thin ("no specifications found", "poor [UNCERTAIN]:
plot-based rates must be rebuilt after manoeuvres", settling 2-3 minutes against 1-2); the research's model has it near
the AFCT and a little below (8.4 against 9.4% on a zigzagging target at 16 kyd, 3.2 and 3.2 steady at 25 kyd).

**Found** (QE refitted behind the same O3 ×25 and power drive, so only the computer differs; 24 duels):

| | the AFCT | the Type 92 |
|---|---|---|
| steady at 14 km, the readings' rung | 75.1% | 72.8% |
| a destroyer passing at 4 km at 30 kn, every error on | 58.9% | 47.1% |
| her zigzag (20°/75 s at 14 km), the readings' rung (48 duels) | 18.6% | 19.6% |
| her own zigzag (10°/90 s at 16 kyd), the readings' rung | 79.4% | 78.7% |
| her circle (15° of helm at 16 km), every error on | 2.5% | 1.8% |

Its record as the research has it. Behind its preset's own director (no RPC, 0.5 mil) a little lower again. Its 15 s
latency against the AFCT's 8 moves her zigzag under three points either way (each at the other's: 18.6 → 18.6, 19.6 →
21.4): the plot's rates rebuilt after her turns as quickly as the table's. A 24-duel run had it 21.6 against 17.7 there,
the noise.

**On the way:** its first guess (2 kn and 12°) was tighter than the Dumaresq's with the same eye: the Dumaresq's 3 and 20
now (the catalogue, `nagato` and `yamato`'s own blocks, their goldens; FL1's Type 92 check 81.2, 81.2, 73.3 with it).
`GunneryTests.The_Japanese_plot_hits_near_the_table_and_drifts_on_a_fast_crossing`.

The ladder's checked through. What it left for others: the walk on a circling target (FL9, the salvo chasing); the seaway's
toll on an unstabilised rangefinder (FL7, FL11, the error sources' session); "target just turned" by eye (FL9, FL10);
every turn filter's first doubt of her rate of turn (FL11); the radar step (FL8) with the radar.
