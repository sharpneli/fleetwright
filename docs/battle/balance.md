# Plan: the balance round

Status: **drafted 2026-10-30**; BL0 (the panel) done the same day; the user's answers the same day ("Decided");
P1-P10 answered the same day.    Updated: 2026-10-30

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
5. **The wrong side on top in the big fights** (the panel, BL0): the record's loser loses more in only 23 of 44 seeds;
   the Jutland's Germans lose more than the British in all 4 (the record: 11 against 14), and the Heligoland Bight's
   British hit 22.6% of their shells.
6. **Big-ship damage is near its anchors.** The open items there are of shape (Tiger's magazines, water per hit),
   not of scale.

## The steps (proposed)

Each step measures against BL0's panel and records before and after here.

| Step | What | Done when |
|---|---|---|
| **BL0** | **The panel. Done 2026-10-30.** `shipgen balance [--seeds 8] [--only ...] [--fc-runs 12] [--out FILE.json]`: the scenarios played to their end in the fine tier (the parity bench's plays, `CoarseParity.PlayMany`), each side's losses beside the record's with a 2x verdict (a ship either way for small numbers), the seeds where the record's loser loses more, how ships went; fc-history judged (`FcHistory.Measure`), the cases and orderings out of band listed. About 6.5 min on 32 threads, the Jutland most of it. The log's row is written by hand; the numbers kept in `docs/battle/balance/` (`bl0.json`). The other yardsticks (`damage-history`, `fire-history`, `dc-history`, `crew-history`, `night-history`) run when a step touches their system. | Reproduces the hand-run baseline: yes, every scenario's numbers identical (the plays inline give the same worlds as on workers); `coarse-parity` and `fc-history` byte-identical after the refactors. |
| **BL1** | **Gunnery to the record.** F7 (spotting by the navy's rules, the straddle calls); then the missing degraders the first cut's battles had, each a physical system with its source: splash confusion (several batteries on one target), the gun platform's motion in a sea, the small fast target (light guns' laying on a turning boat). Systems, approximated where tracing each splash isn't needed (decision 1); point blank stays deadly (decision 2). | `fc-history` mostly in its bands, the rest named; the orderings in; the night cases and close-range duels still brutal. |
| **BL2** | **Cripples and escapes.** F10's judgement on a ship still afloat (worth falling as she's seen done for, a doctrine field for finishing her); falling-out ships turning away and making smoke; striking and abandoning (crew.md's "after"). | The Heligoland Bight and the Dogger Bank's light losses within about twice the record. |
| **BL3** | **The damage leftovers that move a scenario.** Triage of the table above by what BL0-BL2's runs show: the cell-side ruptures, settling past 8-9°, Tiger's magazines, the `yamato` TDS, the user's E8a and crew decisions. The rest stays open with its reason. | Each item done or kept open with a reason. |
| **BL4** | **The matchup refit (PM)** with `OutExponent`, the missing terms, and the widened bands set again. | MatchupTests' even band back to 2, or kept with the user's reason. |
| **BL5** | **The AI's judgement and the ends.** Most of `bugs.md`'s AI items (decision 4), and why fights run to the clock. | Most panel runs end by their own rules. |
| **BL6** | **The scenarios rebalanced** with their knobs, then the coarse sync (bugs.md's drift, `coarse-parity` standard), then the user's playtest. | Each scenario's intended balance stated and met; the coarse panel in its bands. |

## BL1 Gunnery to the record (agreed 2026-10-30)

**What the code has** (read 2026-10-30): the spotter judges each shell of his own salvo over or short with an error of
`SpotOpticK·R²/h` from his height, and nothing else's splashes reach him; the guns lay from a steady platform (the
laying error a fixed `LayMil` by fit, times the crew); `GunneryCombat` is 1 (memory: no lumped factors; removed when
fatigue lands); point blank lays by sight and walks its own fall (F6's bug hunt). The world has a sea already
(`SeaSpectrum`, from the weather's wind with a default 1 m, 12 s swell, drawn by the ocean shader) and every class has
the designer's roll period (`RollPeriodS`, shipgen's IMO formula), but nothing in the sim rolls.

**What the record's cases say is missing** (fc-history's "not modelled" lines): the sea's motion (Tsushima's high
waves, the Orion trial's heavy sea, Bismarck's end in force 8, the practice screens at sea), whose splash (Yellow Sea
and Tsushima's concentration, Rodney beside KGV, many guns on one boat), the stress of battle on human errors (the
research's own calibration used x1.5, x2 in a first battle: `manual-gunnery-research.md` 6.2), and the eye's haze and
smoke (in already since F5 and the smoke field).

**The research to build from:** `research/simulation/ship-motion/` (2026-10-12: roll physics, laying on the roll, the
heavy-weather cases, a phase per ship rather than a roll simulation, its section 7 written for this codebase) and
`manual-gunnery-research.md` 5 (whose splash: 1/(1 + 0.6N)).

**Close range stays deadly by construction (decision 2):** each addition is an angular error or a spotting error. At
point blank the danger space covers the angular errors and nothing is ranged by spots, so the night's and the melee's
brutality is untouched; BL1g checks it with close-range duels and `night-history`.

| Step | What | Check |
|---|---|---|
| **BL1a** | **The sea under the ships. Done 2026-10-30.** The weather gains a swell (height, period, direction) beside the wind sea it already has, the scenario's to set (default the shader's 1 m, 12 s), so the sea drawn and the sea ships roll in are one. Each ship gets a roll: period her class's `RollPeriodS` (her GM falling as she floods lengthens it), amplitude from the research's table by the sea, her encounter period (heading and speed to the waves) and damping, a seeded phase; one sin/cos when read, nothing stepped. Bilge keels from 1895 on every ship as a flagged placeholder until the designer has the fit. | Roll by class and sea against the research's physics: `RollTests` (beam on most, resonance and bilge keels about x0.5, a stiff ship lively, big ships in the swell and small in the wind sea, her own period near resonance), `shipgen roll`. Built as the research's own method (the linear roll over the spectrum) rather than its table: `RollSea` from the weather's `SeaSpectrum`, a few µs a call, no garbage. Against the table our wind-sea rolls run about half (the sea is short-crested and fetch-limited at 150 km: SS7's 7.5 m comes out 4.7 m); in a swell the battleship rolls 4° beam on and 6.7° on the quarter at 15 kn in 3 m at 14 s, a destroyer 15° in a 30 kn wind. Placeholder ζ 0.10 on every ship (the 1895 cut waits for the designer's fit: a class carries no year). The weather's swell is the scenario's (`swell_m`, `swell_s`, `swell_from`) by `Command.SetSea`. |
| **BL1b** | **Laying on a moving platform.** The laying error becomes √(base² + (τ·ω·1000)²) mil, ω the roll (and pitch) rate at the instant, τ by how the guns are laid (a tech row: hand-timed 0.3 s, a director layer by key 0.1 s, later stabilised systems less), so a pointer firing on the roll misses by his reaction time. Roll-timed fire waits for the roll's level: one chance a roll cycle. Heel (a turn, a list from flooding) gives the uncorrected cross-level its deflection error. Each fc-history case gets its record's sea. | Tsushima, the Orion trial, Bismarck's end and the practice cases move toward their bands; calm cases unchanged. |
| **BL1c** | **Whose splash** (fire-control.md F7). Each target keeps a short ring of the splashes near her, by calibre band and time; a spot finds the other same-calibre splashes in its window (other ships', and its own battery's other guns firing independently) and identifies its own with 1/(1 + 0.6N), salvos of 3 or more always. Not identified, the spot is doubtful, or (a share of the time) read from the wrong fall (KGV on Rodney's splashes). Dyed shells, a later fit, would lift it. | Concentration in the scenarios; fc-history's cases with the record's other shooters added. |
| **BL1d** | **Stress on the layers.** The station's duress (crew.md N3b: what has landed at and around it, against its men's nerve) adds to its layers' and rangetakers' errors, as a share of the laying error rising with the duress; green crews in their first fight feel it soonest. This is the research's x1.5 combat factor as a system that reads where the shells fall. Fatigue over hours stays out (memory: GunneryCombat goes when fatigue lands). | Ships under fire shoot worse than ships firing unopposed; practice cases unchanged. |
| **BL1e** | **Local control's judgement** (P4): the judgement preset as local control's computer. | fire-control.md F6's crossing case (12.5% to 21%). |
| **BL1f** | **Wetness, the first part.** The research's wetness index from the sea, heading, speed and freeboard forward: weather-side low casemates and low forward rangefinders out above its thresholds (with hysteresis), the fall-back to the next instrument as now. Coronel's British casemates. | Coronel's casemates silenced on the weather side in its sea; nothing in a calm. |
| **BL1g** | **Fit and check.** `shipgen balance`, `fc-history`, `night-history` and close-range duels; only [EST] numbers moved, each move logged. | fc-history mostly in its bands, the rest named; night and point blank as brutal as before. |

**Out of BL1, named:** fatigue over hours (with `GunneryCombat`'s removal), vibration with speed (no numbers in the
research), stable verticals and fins (later cuts), the captains choosing a course for the sea (BL5's or later), spray
on turret rangefinders at speed (with BL1f if cheap, else later).

**Decided (the user, 2026-10-30):** the steps and their order as above; bilge keels on every ship from 1895 as a
flagged placeholder, with the damping fit planned for the designer (`docs/shipgen/TODO.md`), and the hull model to be
refreshed once ships roll ("the whole hull model is quite old"); wetness in if it's cheap.

## Decided (the user, 2026-10-30)

1. **Gunnery closes the gap with the systems,** not a gameplay exception: "we need the systems in place. We can
   naturally do approximations in the system so that there is no necessary need to trace each individual splash but
   the concept has to be there."
2. **Very close range stays deadly:** "we expect very close range gunnery to still be deadly, and we need it anyhow
   for night fights where the action was usually very brutal." Whatever BL1 adds falls away as the range closes: a
   check of every BL1 step, beside the yardstick.
3. **Twice the record is close enough:** "We are not replicating history, we are using it as a guideline to develop a
   naval combat game after all." The real battles' losses within about 2x of the record, the right side on top in
   most seeds.
4. **The AI's scope:** at least most of `bugs.md`'s AI items go through BL5, not only those the panel shows.

5. **The pending decisions P1-P6** (the user, 2026-10-30):
   - **P1** crew.md decision 11 kept and closed: the German big ships' killed a hit stand.
   - **P2** E8a item 3 kept: the cell-side ruptures stand.
   - **P3 No overload in this release:** "The fight is inspired by Tsushima, doesn't replicate Tsushima. Reality always
     has complex issues which can be hard to replicate in a more general system ... I'm tending towards no to avoid
     feature creep. On the full game we want that." Tsushima's balance is the scenario pass's, with its knobs.
   - **P4 The judgement is local control's computer** (fire-control.md F6's choice), built in BL1.
   - **P5 Scenarios are nudged:** "We are inspired by history in force placements etc, not determined. The only
     absolute (or as absolute as can be in a game) yardsticks are the physics etc. What follows is what follows. And
     we need to think carefully what effects to include because it will then affect every other battle too." So an
     effect goes in for the physics' sake and for every battle, never to make one scenario come out right.
   - **P6** Aims (N1) and wave 2 after this round.
   - **P7** (the scenarios' count, hypotheticals, the prizes' looks) in a session of its own with aims and goals, so
     "destroy all" isn't the only option; after the base game is in shape (the user, 2026-10-30).
   - **P8-P10 as recommended** (the user: "even if you didn't explicitly give any ... I'm not sure either in these
     cases"):
     - **P8** water per heavy hit: waves over holes near the waterline come with BL1's sea (the same sea that moves
       the guns), the captain slowing for holes forward in BL5, the near zone holing a burst space's outside faces in
       BL3; each measured, kept if `damage-history`'s water moves toward the record.
     - **P9** the two restated matchup anchors are set in BL4, after BL1's gunnery has moved them.
     - **P10 a determined day attack** (six boats on four battleships, ordered in) gets two or more boats firing in
       about a third of the runs, at the cost of about half the boats sunk or crippled; checked after BL1, tuned in
       BL5 or BL6 by the boats' run-in and break-off if it misses. Jutland's day attacks had boats fire at heavy cost;
       never getting in makes a flotilla pointless by day, always getting in makes battleships pointless.

## Pending decisions

Gathered from the plans for the user, each with what it is and a recommendation. Those marked **now** can be
answered today; the rest are best answered when their step has measured.

| # | From | The question | Recommended | When |
|---|---|---|---|---|
| P1 | `crew.md` decision 11 | The German big ships lose fewer killed a hit since N1. N6 judged it again: Seydlitz 1.75 (wanted 1-3), Derfflinger 1.28 (wanted "a few"); inside their cases' bands, under G1's average only. | Keep as is, close the decision. | decided (5) |
| P2 | `shells-armour-damage.md` E8a item 3 | Small destroyers capsize more since every hole is kept: a 6″ lyddite burst in a small compartment ruptures a whole cell side (9-15 m²); the rupture capacity was left to the destroyer benches. BL0: the destroyer cases are in their bands (10 × 6″ lost 25%, "lost in many runs"). | Keep as is; reopen only if capsizes still dominate after BL1-BL2. | decided (5) |
| P3 | E8a item 5 | Tsushima's Russian battleships no longer go down for their hits alone: what sank the Borodinos was overload (coal past full load, the belt under water). A scenario's `fuel` stops at a full load. | Let a scenario carry an overload (fuel past full load, a ship's stores), set in Tsushima in BL6. | decided (5) |
| P4 | `fire-control.md` F6 | Local control's computer: a plain rate clock fed by the eye (today) runs a closing rate past the closest approach; the judgement (preset 9) does better crossing (21% against 12.5%), the same elsewhere. | The judgement: it's what a trained officer in local control does, and it keeps point blank deadly (decision 2). | decided (5) |
| P5 | `docs/missions/scenarios.md` question 2 | Real battles as history set them, or nudged for balance where history was a massacre? | Answered by decision 3, read as: history's forces and places, nudged with the scenario's knobs (crews, weather, doctrine, stand-ins) to stay within 2x and give the losing side a game. Confirm the reading. | decided (5) |
| P6 | scenarios.md question 4 | Aims (N1) before wave 2, or the breakouts as straight fights first? | Aims after this round (plan.md's order: balance, then 6.1 aims and wave 2). | decided (5) |
| P7 | scenarios.md questions 1, 3, 5 | How many scenarios for the first release (a dozen, three or four a size?); hypotheticals plausible only or wilder too; the prizes' war's ex-German ships in Kiel's looks or French and Italian trim. | Not this round's; answer when wave 2 is planned. | decided (5) |
| P8 | E8a item 2 | Water per heavy hit sits low in the median: missing are waves over holes near the waterline, a captain slowing for holes forward, and the near zone holing a burst space's outside faces. | Waves over holes with BL1's sea state (the same sea that moves the guns); the captain's slowing in BL5; the outer faces in BL3. Decide after BL1 measures. | decided (5) |
| P9 | `shell-choice.md` A4 | Two matchup anchors restated since battleships fire HE at small ships: "light cruisers with torpedoes take on a battleship" four → five; "destroyers saturate a battleship's defence" eight → twelve. | Set them in BL4: BL1's gunnery (light guns at boats today 3x the record) moves both back. | decided (5) |
| P10 | `bugs.md` Balance | The flotilla scenario's day attack (six boats on four battleships) is beaten off in every seed. What share of determined day attacks should get in? | Decide after BL1, with the light guns at their record; a proposal then. | decided (5) |

## The balance log

| Date | Commit | Change | Panel |
|---|---|---|---|
| 2026-10-30 | `cfe6c165` | BL0's baseline, by hand | the table in BL0 |
| 2026-10-30 | BL0's commit | `shipgen balance`, the default panel (`balance/bl0.json`) | 4 of 14 sides within 2x; the record's loser lost more in 23 of 44 seeds; capsized 59%, foundered 31%, broke in two 10%; fc-history 7 of 23 (15 HIGH, 1 LOW), orderings 3 of 7 |
