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
| FL2 | the opening | | a longer base and a better first guess open and straddle sooner | the first 5 min, every error on (on the readings rung 193 v 266 s already; every error on 143 v 141 s): first suspect the open's longest wait, `GunneryOpenS` 120 s | |
| FL3 | 0 → 1 | the eye (20% of range) → FA 2 m; eye and hand plot → hand plot; open sights 1.15 → telescopic 0.73 mil | ranges found at once, not by long ranging; a tighter pattern | the opening at 6-8 km | |
| FL4 | 1 → 2 | FA → FQ/FT 4 m; hand plot → Dumaresq and Vickers clock; telescopic 0.73 → 0.35 mil | the clock runs the range on between sparse readings from her course and speed | a crossing or closing target, readings 2 a minute; haze that loses readings | |
| FL5 | 2 → 3 | Dumaresq → Dreyer Mk III-V (helm-free, the bearing gear); local laying → director 0.5 mil | through her own turns (FL1); the director's layer above the smoke and spray, one fall to spot | her own zigzag; a seaway; her own and the enemy's smoke, spray at speed | |
| FL6 | 3 → 4 | Dreyer (rate clock, latency 15 s, bias 1.2%) → AFCT (true course, 8 s, 0.6%); FQ/FT → stereo 5 m; director power drive 0.4 mil | a target's turns re-learned sooner; the bearing rate past the Dreyer's 0.25°/s; less bias to range out | the target's zigzag; a fast crossing near | |
| FL7 | 4 → 5 | AFCT → Ford Mk 8 (3 s, 0.4%); stable vertical, RPC; coated, stabilised O4 | fire at any point of the roll, not on it; the rangefinder in a seaway | a seaway, rate of fire in it | |
| FL8 | 5 → 6 | radar ranging (stand-in) | ranges through haze and at night | haze, night: radar is out of the first game, so checked last | |
| FL9 | 6 → 7 | Mk 8 → Mk 1A (acceleration, turn detection) | follows a target in a steady turn ("a steady turn cuts hits 2-4x for every straight-line machine", research §5.6) | a target circling | |
| FL10 | 7 → 8 | Mk 1A → digital turn filter | the same, quicker; a zigzag | a target circling and zigzagging | |
| FL11 | side steps | Argo (true course 1912, gyro-mounted rangefinder 8 a minute) v Dreyer; AFCT turn setting v AFCT; Type 92 v AFCT | the Argo through her own turns and in a seaway; the turn setting on a circling target; the Japanese plot per its record | as each says | |

## Numbers to audit on the way

Each is checked where its step is, as `own_turn_kick_m` in FL1: is it a physical quantity of the machine, sourced, or a
lumped stand-in for something the chain now models?

- `own_turn_kick_m`: FL1.
- `resid_pct`, `defl_bias_mil`: the residual ballistic bias as the corrections a machine computes (wind, density, drift);
  per machine is right if it says which corrections it has; checked when wind and density come in.
- `lay_mil` by mount: a director (0.5 mil) lays worse than a turret's telescopic sights (0.35), and a stable vertical with
  RPC (0.5) worse than a director's power drive (0.4), on a steady platform. Check the sources' split (BL1l) in FL5 and FL7.
- `latency_s`, `q`, the priors: the machine's own assumptions; fine as such, each checked by its step.
- `GunneryOpenS`, `OpenDoubtSteps`: FL2.
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
126 against 156 exactly. **On the way:** a first try gave the course a 2 s lag [EST] too: the Dreyer's zigzag fell
from 83 to 16% on the readings' rung (2° of bearing at 15 km is 500 m), and at a quarter of the lags it held: a cliff
too sharp for the error, so something in the chain amplifies a bearing error that size (next: the amplifier).
