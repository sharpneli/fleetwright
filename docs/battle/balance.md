# Plan: the balance round

Status: **drafted 2026-10-30** (BL0's baseline taken the same day); the steps and the questions at the end wait for the
user.    Updated: 2026-10-30

Every deepening since 2026-10-14 kept balancing out of its steps and left it "to the balance pass": the physics first,
sourced, with the combined results judged at the end (memory: realistic, then tune; physics, no guard rules). This is
that pass. It's overdue: the wave 1 scenarios (`docs/missions/scenarios.md`) were balanced on 2026-10-25, and the
crew, coarse and night plans have moved them since.

Step letters **BL** (free in the docs).

## The aim

The scenarios play like their battles in shape, and the yardsticks sit in their bands, so that a playtest alpha is
fun and its results mean something:

1. **Losses of the record's order**, the right side usually on top in the real battles, and a spread across seeds
   that makes a hypothetical worth playing.
2. **Light forces that live to run.** A boat that falls out gets away more often than not, as in the record's mist
   and smoke, instead of being finished by everything in range.
3. **Fights that end** by their own rules (decided, done, quiet), not by the scenario's clock.
4. **The AI's judgement** (the matchup, Fight, declining) fitted to the model as it now stands.
5. **Coarse follows fine** again at the end (`coarse.md`'s sync point).

## The rules

The standing ones, restated for this pass (memory and the plans' own):

- **Physics first.** A gap with the record is closed by the missing mechanism, sourced, before any knob. A knob moves
  only within an `[EST]` value's range.
- **History is the yardstick:** the record's bands at the record's conditions; today's numbers are never a bound. The
  sim is still the ideal case in places (no sea, no fatigue), so a rise over a band is reported, not tuned away.
- **No lumped factors, no navy values:** fatigue, sea and splash confusion come as systems of their own (memory:
  `GunneryCombat` is removed when fatigue lands); nothing in the damage or gunnery path is set by navy.
- **Gameplay departures are named exceptions**, each approved by the user and recorded here (memory: design rule
  exceptions), never a quiet retune.
- **Scenarios balance with their own knobs** (crews, readiness, weather, doctrine, stand-in designs), last, once the
  model is right; scenario designs serve the asymmetry, not the real tonnage.
- **A band moves only with the user's say;** each band widened during the deepenings is set again here or kept with
  its reason.

## What was left to it

Gathered from the plans and `bugs.md` (2026-10-30):

| From | Item |
|---|---|
| `shells-armour-damage.md` "Last: the balance pass" | **PM** the matchup's per-hit estimate refit to the terminal (fuze depth, underwater hits, target angles); MatchupTests' even band 3 back to 2; DamageTests' HE anchor band |
| `shells-armour-damage.md` E8a | open items 2 (water per heavy hit low in the median), 3 (small destroyers' cell sides rupture), 5 (Tsushima's overload, the scenario's) |
| `shells-armour-damage.md`, `shell-contents.md` | the matchup's missing terms: spall, jams, leaks, shock, near misses, the contents as shares |
| `fire-control.md` | F7 (spotting by the navy's rules: the straddle calls, the main limiter), F10's judgement on a ship still afloat, F11 (calibration); "tuning for gameplay waits for the balance pass" |
| `fire.md` B5 | Suvorov and the Yalu's unprepared ships don't burn as recorded; hose water; the clutter's weight |
| `damage-control.md` DC9 | Audacious doesn't capsize; one torpedo and a pre-dreadnought; Lützow at two-thirds; Shinano; counterflood tanks |
| `night.md` | the Dogger Bank's dawn and Coronel's dusk moved the day actions, "the scenarios' balance pass is the place" |
| `coarse.md` | the zigzagging destroyer dodges coarse fire better; the balancing pass's smoke tricks; and bugs.md's coarse day gunnery drift since J7 |
| `bugs.md` Balance | the flotilla's day attack never gets in; `OpenDoubtSteps`; ranging 20 min on an evader; `Matchup.OutExponent` over light ships; light ships die too readily; Coronel's capsizing armoured cruisers |
| `bugs.md` Damage | settling linear past 8-9° of trim; four torpedoes capsize `yamato` in 2 min; the AP near miss's splinters; Chester's casualties |
| `bugs.md` The AI | the Fight band by RangeShare; Victory and the Dreadnought judged; intercepts given again each think; a day's search; a station far astern; a force dropping out of its commander's units |
| The user's pending decisions | E8a's items above; crew.md decision 11 (German big ships' fewer killed a hit); shell-choice.md's two restated anchors; fire-control's "judgement as local control's computer"; scenarios.md questions 1-5 (question 2, nudging history for balance, is this pass's) |

## BL0 The baseline (2026-10-30, HEAD `cfe6c165`)

Fine tier, the AI on every side, `shipgen scenario FILE --play --seeds 8` (the Jutland 4), against wave 1 as built on
2026-10-25 and the record. Ships lost a run, mean (range).

| Scenario | Ships | Lost a run, today | As built 10-25 | The record | Ended |
|---|---|---|---|---|---|
| Dover Strait 1917 | 2 v 6 | 0.6 (0-1) v 0.9 (0-2) | 0.9 v 0.4 | 0 v 2 | time 8 of 8 |
| Coronel 1914 | 3 v 5 | 0.4 (0-2) v 1.4 (0-3) | 0.9 v 0.9 | 2 v 0 | time 6, quiet 1, done 1 |
| Cape Sarych 1914 | 7 v 2 | 0.5 (0-2) v 0.5 (0-1) | 1.2 v 0.5 | 0 v 0 | time 8 |
| The prizes' war 1920 | 6 v 6 | 0.5 (0-3) v 0.2 (0-1) | 1.2 v 1.4 | (hypothetical) | time 8 |
| Heligoland Bight 1914 | 44 v 24 | 3.6 (0-8) v 21.4 (16-24) | 10 v 22 | 0 v 4 | time 5, decided 3 |
| Dogger Bank 1915 | 33 v 26 | 3.2 (0-8) v 6.6 (1-11) | 15 v 14 | 0 v 1 | time 8 |
| Åland 1921 | 9 v 7 | 4.0 (0-8) v 2.1 (0-4) | 4.1 v 2.4 | (hypothetical) | time 5, done 2, quiet 1 |
| Tsushima 1905 | 13 v 14 | 5.6 (2-9) v 3.0 (2-5) | | the Russian line sunk or taken v 3 torpedo boats | time 5, quiet 2, done 1 |
| The Jutland 1916 (4 seeds) | 151 v 99 | 33.0 (27-40) v 47.5 (30-58) | | 14 v 11 | time 4 |

How the losses went, every run above together: capsized 57%, foundered 33%, broke in two 10%; no magazines.

The yardsticks at the same commit:

- **`fc-history`: 7 of 23 cases in their bands**, nearly every miss HIGH, worst close in: Santiago 31% (1.8-7%),
  Tsushima's 12″ 21% (5-15%) and 6″ 8.5% (1-4%), the 3rd BCS 22% (2.2-8.6%), light guns at a fast boat 6.7%
  (0.5-2%). The orderings: the Germans over the British at Jutland 0.83 (want 1.5-5), a steady target over a salvo
  chaser 1.9 (want 3-10). The cases' own "not modelled" lines name what's missing: the sea's motion, splash confusion,
  smoke over the guns, haze on the rangefinders, the target's manoeuvres.
- **`damage-history`:** per hit about where E8 left it. German capital ships now lost in 0% of runs everywhere
  (E8b had Seydlitz 25%, Derfflinger 23%; likely the brass-cased magazines burning out since `magazines.md` V3,
  the record's way: to check); Tiger 30% by her magazines (survived); Indefatigable 5% in 15 min (blew up); the destroyer cases in
  their bands.
- **`fire-history`, `dc-history`, `crew-history`, `night-history`:** as their plans left them (their open items above).

## What the baseline says

1. **Too many hits, not too little armour.** A V1 boat takes twelve 6″ hits on the `hits` bench and none capsizes
   (water 136 t, crippled 76%); yet the Heligoland Bight's Germans lose 16-24 of 24 to about 1,670 British hits a run
   (about 70 a ship). Gunnery is several times the record close in, and lightest of all on what matters most to light
   forces: the light guns at a small, fast, turning target.
2. **Cripples are finished.** The timeline: boats fall out in the first 20 minutes (steering, guns, too slow), then
   sink 5-30 minutes later under whoever is in range. In the record a cripple was often lost to sight in the mist and
   smoke, left behind for a better target, or struck. Our hunters keep shooting what they can see, and a cripple has
   nowhere to hide in 5 km of visibility.
3. **Fights run to the clock.** Most scenarios end on time with both sides apart and nobody home: the commanders'
   "done" doesn't come, or the scenarios' clocks are short of it.
4. **The scenarios have drifted** since 10-25: Coronel flipped to the Germans losing, the prizes' war is nearly
   bloodless over 7 hours, the Dogger Bank fell from 15 v 14 to 3.2 v 6.6 (still several times the record). They
   rebalance last, with their knobs, once the model has settled.
5. **Big-ship damage is near its anchors.** The open items there are of shape (Tiger's magazines, water per hit),
   not of scale.

## The steps (proposed)

Each step measures against BL0's panel and records before and after here.

| Step | What | Done when |
|---|---|---|
| **BL0** | **The panel.** `shipgen balance` runs the scenarios and the yardsticks above and prints one table (losses, hits a side, how ships went, how it ended, each yardstick's in-band count), appended as a dated row to "The balance log" at the end of this doc, as coarse's parity log. | It reproduces the table above. |
| **BL1** | **Gunnery to the record.** F7 (spotting by the navy's rules, the straddle calls); then the missing degraders the first cut's battles had, each a physical system with its source: splash confusion (several batteries on one target), the gun platform's motion in a sea, the small fast target (light guns' laying on a turning boat). Order and scope are question 1. | `fc-history` mostly in its bands, the rest named; the orderings in. |
| **BL2** | **Cripples and escapes.** F10's judgement on a ship still afloat (worth falling as she's seen done for, a doctrine field for finishing her); falling-out ships turning away and making smoke; striking and abandoning (crew.md's "after"). | The Heligoland Bight and the Dogger Bank's light losses within about twice the record. |
| **BL3** | **The damage leftovers that move a scenario.** Triage of the table above by what BL0-BL2's runs show: the cell-side ruptures, settling past 8-9°, Tiger's magazines, the `yamato` TDS, the user's E8a and crew decisions. The rest stays open with its reason. | Each item done or kept open with a reason. |
| **BL4** | **The matchup refit (PM)** with `OutExponent`, the missing terms, and the widened bands set again. | MatchupTests' even band back to 2, or kept with the user's reason. |
| **BL5** | **The AI's judgement and the ends.** The AI bugs that show in the panel, and why fights run to the clock. | Most panel runs end by their own rules. |
| **BL6** | **The scenarios rebalanced** with their knobs, then the coarse sync (bugs.md's drift, `coarse-parity` standard), then the user's playtest. | Each scenario's intended balance stated and met; the coarse panel in its bands. |

## Questions for the user

1. **Gunnery (BL1):** close the hit-rate gap with the missing systems (F7, splash confusion, the sea's motion, the
   small fast target), accepting that it's several steps, or with a named gameplay exception now and the systems
   later? I'd build the systems: F7 is already planned and agreed, and the rest are what the record's cases name.
2. **How close to history** must the real battles come? A proposal: losses within about twice the record, and the
   right side on top in most seeds. This is also scenarios.md question 2 (nudge history for balance where it was a
   massacre).
3. **Scope of the AI (BL5):** the AI bugs that show in the panel only, or the whole of 5.1f's cluster here?
4. **Your pending decisions** (E8a's items, crew decision 11, the shell-choice anchors): settle them as the steps
   reach them, or now?

## The balance log

| Date | Commit | Change | Panel |
|---|---|---|---|
| 2026-10-30 | `cfe6c165` | BL0's baseline, by hand | the table in BL0 |
