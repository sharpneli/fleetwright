# Plan: the balance round

Status: **drafted 2026-10-30**; BL0 (the panel) done the same day; the user's answers the same day ("Decided");
P1-P10 answered the same day; **BL1 reopened 2026-11-01** (the roll, counted once, and the fire's ranges: BL1j-BL1n; BL1t-u, no pulsing
falls under helm, every discipline checked; BL1v-z 2026-11-02, the error ladder and the spots centring the fall).
Updated: 2026-11-02

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
| **BL1b** | **Laying on a moving platform. Done 2026-10-30.** The laying error becomes √(base² + (τ·ω·1000)²) mil, ω the roll (and pitch) rate at the instant, τ by how the guns are laid (a tech row: hand-timed 0.3 s, a director layer by key 0.1 s, later stabilised systems less), so a pointer firing on the roll misses by his reaction time. Roll-timed fire waits for the roll's level: one chance a roll cycle. Heel (a turn, a list from flooding) gives the uncorrected cross-level its deflection error. Each fc-history case gets its record's sea. | fc-history 7 → 13 of 23 in band (Tsushima's 12" 21 → 8.7%, Bismarck's end 57 → 33%, the practice cases in a sea in band; the fast boat 6.7 → 4.5%); the cases given their records' seas [EST]. τ and the cross-level share as tech-row fields with placeholders (0.1 s, a pointer 0.5, a director 0.7, a stable vertical 0.02 s and 0.98), crews' `RollTiming` (green 3, average 2, trained 1, elite 0.8 [EST]); continuous aim when the gear lays 1.1x her peak roll rate; the roll's cost cached per battery by the steps of heading, speed and GM. Turning heel isn't in the sim (the computer's `OwnTurnKick` stands for it). Found on the way: the captains' torpedo list and reaches cache allocated mid-fight (fixed); the flotilla test now runs in a calm (its day attack is P10's). |
| **BL1c** | **Whose splash** (fire-control.md F7). **Built 2026-10-30.** Each target keeps a short ring of the splashes near her, by calibre band and time; a spot finds the other same-calibre splashes in its window (other ships', and its own battery's other guns firing independently) and identifies its own with 1/(1 + 0.6N), salvos of 3 or more always. Not identified, the spot is doubtful, or (a share of the time) read from the wrong fall (KGV on Rodney's splashes). Dyed shells, a later fit, would lift it. | `SplashTests`: alone, a destroyer's independent fire is always told (her own guns are one group to her control officer, research §5: the first build counted them and told only 46%); with three others on the same target 31%; salvos always. Each target keeps a ring of the falls within 1 km of her (when, calibre, whose ship), grown at engage, no garbage in a fall; hits and point blank are told. The window ±3 s, the band ±15%, the 1 km and the misread share 0.3 are [EST]. Still to do in BL1g: fc-history's concentration cases (Yellow Sea, Tsushima, Rodney with KGV) given their other shooters. |
| **BL1d** | **Stress on the layers. Built 2026-10-30.** The station's duress (crew.md N3b: what has landed at and around it, against its men's nerve) adds to its layers' and rangetakers' errors, as a share of the laying error rising with the duress; green crews in their first fight feel it soonest. This is the research's x1.5 combat factor as a system that reads where the shells fall. Fatigue over hours stays out (memory: GunneryCombat goes when fatigue lands). | `Steadiness.Stress`: 1 + `StressGain` (1 [EST]) x min(duress / nerve, 1) on the laying error, the roll timing and a reading's error, by the layer's station (the director's, else the guns') and the rangetaker's; `SteadinessTests`: none unopposed, a shell through a turret hitting a tenth of its men x2.0 green, x1.68 trained, x1.54 elite, easing in the quiet. fc-history is untouched (nobody fires back there); the scenarios carry it. |
| **BL1e** | **Local control's judgement** (P4). **Done 2026-10-30:** `FireControlPresets.Local` takes the judgement's computer (the officer at the guns judging her course and speed, looking again each minute) beside level 0's optics and the battery's own sights. | fire-control.md F6's crossing case (12.5% to 21%) is F6's measure; the suite passes unchanged. |
| **BL1f** | **Wetness, the first part. Done 2026-10-30:** the research's casemate rule (`RollSea.Awash`): a gun is awash under H_s x its side (0.6 weather to 0.35 lee) x its place (2 at the bow, 1 amidships) x (1 + 0.5 (V/25 kn)² x her heading to the sea), its axis as she has settled; awash, it doesn't bear. The wetness index for blinded forward rangefinders and swamped forward turrets waits for a freeboard factor from the designer (with the hull model's refresh, `docs/shipgen/TODO.md`). | `RollTests`: a 3 m casemate under on the weather side in 5 m of sea, not on the lee, a turret never, a bow casemate heading into it, nothing in a calm. |
| **BL1g** | **Fit and check.** Under way 2026-10-30: `scenario --play --fire-table` (shells and hits by calibre, range and point blank) found where the scenarios' losses come from (below). `shipgen balance`, `fc-history`, `night-history` and close-range duels; only [EST] numbers moved, each move logged. | fc-history mostly in its bands, the rest named; night and point blank as brutal as before. |
| **BL1h** | **The moving platform, second part. Built 2026-10-30** (agreed after BL5's closing): `RollSea.Pitch` (the wave slope along her, the share her length follows 3 (sin q - q cos q)/q³ and her beam averages, her pitch resonance at 2.4 √draught s with ζ 0.3 [EST]), `GunnerySystem.Vibration`, the helm's yaw; `RollTests`, `shipgen roll` prints pitch. In a sea state 5 wind sea head on a destroyer pitches 1.4° (the research's inferred 2.5-4°; our fetch-limited sea runs low); at 30 kn into a long swell her pitch rate more than doubles. The sea's own yaw left out. **pitch** from the same spectrum and method as the roll (the hull's pitch response by length against wavelength, the encounter frequency at speed) into the layer's elevation for targets toward the bow and stern; **vibration at speed** on the sights (research §1.1: 1 + a (v / v_max)², a by the gun's support [EST]); **yaw** from the helm and the sea into the trainer's laying. Each fades as she slows and the sea calms. | The light guns' 2-4 km and point-blank rates come down for fast small ships; big ships at battle ranges and a ship lying still unchanged. |

**Out of BL1, named:** fatigue over hours (with `GunneryCombat`'s removal), vibration with speed (no numbers in the
research), stable verticals and fins (later cuts), the captains choosing a course for the sea (BL5's or later), spray
on turret rangefinders at speed (with BL1f if cheap, else later).

**Decided (the user, 2026-10-30):** the steps and their order as above; bilge keels on every ship from 1895 as a
flagged placeholder, with the damping fit planned for the designer (`docs/shipgen/TODO.md`), and the hull model to be
refreshed once ships roll ("the whole hull model is quite old"); wetness in if it's cheap.

### BL1g: where the hits come from (2026-10-30, after BL1f)

`shipgen scenario FILE --play --seeds N --fire-table`, every seed's shells and hits by calibre, range band and point blank:

| | Heligoland Bight (4 seeds) | the Jutland (2 seeds) |
|---|---|---|
| light guns (under 120 mm), 0-2 km, point blank | 2,814 shells, 65% | 5,668 shells, 68% |
| light guns, 2-4 km | 9,382, 17% | 19,097, 21% |
| light guns, 4-6 km | 9,881, 4.5% | 34,765, 6.6% |
| medium (120-200 mm), 0-2 km point blank / 2-4 km | 79% / 16% | 70% / 19% |
| heavy (300 mm and over), 10-15 km / 15 km and over | (none) | 1.0% / 0.7% (the record 1.4-4%) |

- **By day and night, at small ships and large** (the Jutland, 2 seeds, the table split): the light guns at point blank
  hit 79% by day and 74% by night at large ships, 64% and 61% at small; at 2-4 km (not point blank) 25% and 23% at
  large ships, 15% at small. Day is no better than night: the melee itself is the trouble, not the dark.
- **Why the light guns hit so well close in:** a light gun's trajectory is so flat inside 2 km that each mil of
  elevation is about 100 m of range, so the layer's hand decides it. The sights' `lay_mil` is table 1's trained layer
  in a moderate sea (manual-gunnery-research.md: 0.55 mil for a good telescope, continuous aim), a gunlayer's test
  ashore of a steady ship; on a destroyer at 25-30 kn nothing else moves the gun but the roll (BL1b) and the men's
  stress (BL1d). Missing, all physical and all of the sim's state: **pitch** (the same spectrum and method as the roll;
  short, fast ships meet the waves often and pitch hard), **vibration at speed** on the sights (research §1.1:
  k_vib = 1 + a (v / v_max)², a by the gun's support), and **yaw** from the helm and the sea into the trainer's laying.
- **The heavy guns at battle ranges are at or under the record now.** BL1's systems did their work there.
- **The losses are the light forces' melee:** at the Jutland the light guns make about 12,000 of a seed's ~16,000 hits,
  nearly 4,000 of them inside 2 km and as many again at 2-4 km.
- **Point blank is deadly by decision 2;** the question is why light forces close inside 2 km by day so often (the
  AI's: BL5) and whether 65-70% a shell is the deadly decision 2 meant.
- **At 2-4 km the light guns hit 17-21%,** far over anything recorded for them. What's still missing for small, fast
  ships firing at each other: own speed (vibration, spray over the guns, pitch: the research's §1.1-1.3, roll only so
  far), and the target's evasion under fire as the record's boats did it.

### BL5's closing, measured: it's the cripples (2026-10-30)

The fire table's close fire (inside 4 km) tagged by the shooter's and the target's orders (`--fire-table`):

- **Not the captains' own fighting:** at the Heligoland Bight the ships firing close are in their lines (Station) under
  their admirals' Fight, Screen or Scout; captains fighting on their own made about 50 of the close hits.
- **The targets are cripples going home:** 3,174 of the Heligoland Bight's ~4,500 close hits (4 seeds) and 6,772 of the
  Jutland's close hits (2 seeds, day and night) land on ships that fell out and turned for home, mostly within a third of
  the shooter's band (64% a shell there). The forces passing by pour fire into them.
- **Why they draw it:** a captain's target choice (`CaptainLayer.Pick`) weighs a contact by her range, the fire already
  on her, her size and how done for she's seen (`Worth`: only once she's seen low, listing or going down). A cripple
  making for home upright is a full-value target, and usually the nearest.

### BL2 Cripples and escapes (agreed 2026-10-30)

Everything below reads what the sim already has: the picture's contacts carry when her guns were last seen firing
(`Contact.QuietMs`), whether she's seen silent, stopped, low, listing or burning, her estimated course; the smoke field
can be laid by a ship (`SmokeSystem.MakeSmoke`, which no AI orders yet).

| Step | What | Check |
|---|---|---|
| **BL2a** | **What a target is worth, by what's seen of her. Built 2026-10-30:** `CaptainLayer.Matters`: 1 - (1 - Finish) x quiet x away (quiet: her guns unseen for up to `CrippleQuietS` 90 s; away: her own speed opening the range, to `CrippleOpenMs` 4 m/s [EST]), beside `Worth`; doctrine's `Finish`, the default preset's 0.1, the code's 1 (as before). `Worth` grows from "how done for" to "how much she still matters": a contact whose guns are seen firing, or who comes on, counts in full; one gone quiet (her guns not seen for a while) and opening the range counts less, smoothly by how long she's been quiet and how fast she opens; done for, least, as now. A doctrine field (`Finish`, the side's choice like `Wrecks`) sets how much a quiet, running enemy still draws fire: a navy that finishes its cripples against one that fights what fights it. Default: the gunnery officers' habit, fire at what's firing at you. | The scenarios' close hits on ships going home fall; a side ordered to finish still does. |
| **BL2b** | **A cripple covers her retreat with smoke. Built 2026-10-30:** `CaptainLayer.CoverRetreat` (any ship: the user); `FallOutTests`. A ship that falls out and turns for home under fire, with steam to make it, makes funnel smoke (the smoke field, as the user's order does now), stopping when she's out of sight or out of touch. | Her pursuers' sight of her is cut by her smoke in the smoke tests; the panel's "home" hits fall. |
| **BL2c** | **The panel again,** then BL1h (pitch, vibration, yaw). | The Heligoland Bight's and the Dogger Bank's light losses toward twice the record. |

Left for later, named: striking and abandoning (crew.md's "after"), friends covering a cripple, the admirals' tasks
turning from cripples.

**Decided (the user, 2026-10-30):** "Fire what's firing at you, or naturally some other target is more pressing.
Cripples are not interesting when there is something better to do"; and any ship falling out under fire makes smoke if
she can ("any ships may do so naturally"): no rule by type.

### BL1i The air on the sights and the spots (agreed 2026-10-30)

After BL1h the gap is light and medium guns in calm, hazy weather: at the Heligoland Bight (5 km of mist, calm) the
light guns hit 14-23% at 2-4 km, so too many ships fall out early (about 18 German against the record's 4) and the
cripples are then finished; fc-history's misses in calm air say the same (the Jutland's 3rd BCS 22% at 8 km against
2.2-8.6%, the Grand Fleet's 8.8% against 1.8-7.2%, the cases' own "not modelled: haze on the rangefinders; low
contrast; splash confusion").

**What's missing:** the air works on the rangefinders' readings already (fire-control.md F5c: their error grows as the
target's contrast through the air falls toward the eye's threshold, `SightModel.RangingFactor`), but not on the
layer's sight nor on the spotter's eye. A layer laying on a grey shape at the edge of what he can see, and a spotter
judging over or short of a splash low in haze, both do worse as the margin falls, as the rangetaker does.

**Proposed:** the same margin, already worked out for each sensor every second (`MarginOf`), widens the layer's error
and the spotter's by the rangefinders' factor (or a factor of its form: the research's eye-in-haze, research
`fire-control-research.md` §3), nothing at a clear, near target. At night a lit target has its margin from the light,
an unlit one its silhouette's, so the night's point blank stays brutal where it's lit or close, and `night-history`
checks it.

**Decided (the user, 2026-10-30):** build it; "it's also preparing for the future expansions because in situation like
that a radar will be incredibly useful addition and that will be properly deadly as it ought to be." So the air works
on the optical path only (the eye, the telescope, the rangefinder); radar ranging and spotting (F9, a later cut) step
round it.

**Built 2026-10-30:** `SightModel.HazeFactor` (the rangefinders' form: 1 from the clear margin 4 up, (4 / margin) to a
power under it; the layer's `AimHazePower` 0.6, the spotter's `SpotHazePower` 0.8 [EST]) by the battery's best margin
that second. `SplashTests`: a destroyer 4.5 km off is seen at margin 10 in clear air, 3.4 in 6 km of mist, 2.4 at the
mist's edge. It bites at the edge of sight, little inside it: fc-history's haze cases move the right way (the Dogger Bank's
British 4.9 → 3.6%, the 3rd BCS 21.9 → 19.8%, the Germans over the British at Jutland 0.86 → 1.03), 12 of 23 in band;
`night-history` keeps the close night fights brutal (64-84% Britain, 46-59% Germany inside about 2 km) and takes the
12th Flotilla's dawn shooting at 5-7 km down (12 → 4%). Not the answer for the Heligoland Bight's 2-4 km: there the boats
are plainly in sight.

### BL1 reopened: the roll, counted once, and where the fire is (agreed 2026-11-01)

**Why** (the user's playtest, a Yamato-like at the Jutland "missing like no tomorrow" at 7 km): `shipgen duel yamato
dreadnought --range 7000` hit 25% in the default weather, 35% in the Jutland's 8 kn, and 11% in a copy of the Jutland
with Lion and Princess Royal as Yamatos (6-10 km, 51 salvos). The salvos were laid within about 70 m of her and fell
300-800 m off: at 7 km a mil of elevation is about 99 m, so every angular error costs most close in. Found:

1. **The roll takes every wave's whole slope, however short against the hull** (`RollSea.At`). A wave much shorter
   than her beam lifts both sides at once, and one met obliquely and short against her length meets her in many
   phases; the pitch averages over her length and beam (`RollSea.Pitch`), the roll didn't. At speed the overtaken short
   wind sea is met near her roll period: in a 15 kn wind at 20 kn a Yamato rolled 6-7° on most headings (peak rate
   2.6°/s), a Dreadnought 8°, where the record has Forrestal 2.7-4.8° at her worst heading in sea states 3-4
   (research §3.2) and "waves under 400 ft hardly affect a ship of 60 ft beam or more" (USNI 1931). The beam's and the
   length's averages (sin p / p each) bring them to about 1° and 1.5°, a destroyer 9° → 2.7°; a frigate's calibration
   (excitation 0.85, ±12° in SS5) barely moves. The other way, heavy weather already rolls short (BL1a: our wind sea is
   fetch-limited; a Dreadnought 2.2° in "SS7" against the table's 13°), and the averaging takes it lower: it needs its
   calibration with it.
2. **The roll counted twice.** `lay_mil` is the reference's (`fire_control_ref.py`) error in a sea up to state 3 with
   the roll in it (an unstabilised director 1.0 mil against a stabilised one's 0.5, `sea_k` raising only the former),
   and the sights' rows are table 1's moderate-sea column (manual-gunnery-research.md 2.4, 3°, 10 s); BL1b added the
   roll again as its own term. The bases go to a steady platform's, the roll left to BL1b alone.
3. **Fire under helm.** About a quarter of her salvos were fired turning past `OwnTurnYawDegS` (0.2°/s), each with the
   Type 92's ±400 m own-turn kick in range and line; and a salvo far off for line tells the spotter nothing in range.
   The kick is the reference's; where the turns come from is to be measured.

The two fixes take `fc-history` from 12 to 9-10 of 23 in band, nearly all HIGH (the USN practice at 6 kyd 35 → 63%
against 17-39%, Bismarck's end 31 → 48% against 6-20%): BL1b's gain toward the record came partly from the errors.
**Decided (the user, 2026-11-01):** fix the physics, take the yardstick where it goes and look for what's missing
(the cases' own "not modelled" lines) rather than keep an error because it lands near the record; then reassess with
playtesting. And **never read a hit rate without its range**: an AI closing to where it hits hides a weak fire
control behind a record-like rate (a historical 1.something% at Jutland at 500 m means nothing), so the panel reports
where the fire is.

| Step | What | Check |
|---|---|---|
| **BL1j** | **Where the fire is, in the panel.** `shipgen balance` reports each side's shells and hits by gun class (light under 120 mm, medium to 200, heavy from 200) and the true range when fired: the shells' median and quartiles, the hits' median, the share and hit rate in 0-2, 2-4, 4-6, 6-10, 10-15 and 15+ km; and the record's class's median beside the record's ranges (rough [EST] from the standard accounts: Coronel 5-11 km, Cape Sarych 6-8, the Heligoland Bight 4-8, the Dogger Bank 15-18, Tsushima 3-7, the Jutland 8-17; Dover's light guns 0-2): CLOSE, in, FAR. `--out` keeps them. | The baseline before the fixes (`balance/bl1j.json`). |
| **BL1k** | **The roll averaged over the hull,** beam and length, as the pitch is; the heavy-weather calibration with it. **Done 2026-11-01:** `RollSea.At` takes her waterline length and beam, each wave's slope times sin q / q (along her) and sin p / p (across her). A Yamato-like at 20 kn in a 15 kn wind and the 1 m swell 7° → 1.0° at her worst heading, a Dreadnought 8° → 1.5°, a destroyer 9° → 3°; in a calm swell alone little moves. **Heavy weather:** compared like for like (`shipgen roll --ss N --developed`: the research's fully developed seas, where the world's are fetch-limited at 150 km under a gale) the averaging costs little, the waves being long: a Dreadnought in SS7 8.6 → 7.9° beam on, the destroyers in SS5 about 12%. The excitation stays 0.85, a physical share of the slope: the frigate (113 × 12.5 m, 9.5 s) rolls 8.8° beam on in SS5 (10.3° unaveraged: our sea is short-crested where the research's fit was long-crested), between the USN's 8° destroyer limit at about SS5 and the RN frigate's about ±12.5°. The heavy seas' shortfall in the game is the sea's (the fetch; a scenario's swell), not the roll's. | `RollTests` (a big fast ship overtaking a short wind sea rolls 0.3-2°, a destroyer twice that; the frigate 8-13° in SS5); `SmokeSightTests`' fine-against-coarse smoke sampled each second, not each 5 (one seed's 48 samples caught salvo clouds by luck: fine 0.54 → 0.46 → 0.57, coarse 0.64 → 0.88 with the ships firing without waiting on the roll: coarse's gun smoke drifts up, for the coarse sync). Found: the designer's destroyers roll at 11.2-11.7 s (the research's 8.4-8.8 s for real ones: Type 1934, Knox), off resonance in SS5: bugs.md. |
| **BL1l** | **The roll counted once:** the mounts' `lay_mil` to a steady platform's. **Done 2026-11-01**, each row from its own source's split, the research's own form σ = √(base² + (τ·ω)²) with the base a steady platform's (ship-motion-gunnery-research.md summary 8): the gun-laid sights their sight's own error (manual-gunnery-research.md §2.3's model: open 4′ → 1.15 mil, early telescope 2.5′ → 0.73, telescope 1.2′ → 0.35, for both the telescope fired on the roll, 2.0 before, and the continuous one, 0.55), table 1's method and roll left to BL1b; the unstabilised directors the reference's halved by its own split (1.0 against a stabilised 0.5, its sea factor on the former only): follow the pointer and no RPC 1.0 → 0.5, power drive 0.8 → 0.4 (in a calm a hair under the stabilised 0.5, whose stable vertical's own error is in it; in any sea the roll's τ decides). The stable verticals unchanged. τ stays BL1b's (0.1 s a trained layer at the key, [S, USNI 1900]). In the templates and every design's own block (`shipgen templates`; 131 designs; the goldens). | **fc-history 12 → 9 of 23** (14 HIGH, 1 LOW): the pre-director cases rise most (Santiago 30 → 44% against 1.8-7, the Yellow Sea 17 → 26%, Tsushima's 12" 7.6 → 16% against 5-15), USN practice at 6 kyd 35 → 63% (17-39), Bismarck's end 31 → 48% (6-20), the 3rd BCS 20.5% (2.2-8.6); orderings 3 of 7 (practice's fall with range now HIGH). `shipgen duel yamato dreadnought` at 20 kn: 3 km 96%, 5 km 79-82%, **7 km 57% (25% before)**, 10 km 33%, 15 km 12%, 20 km 6%; a Dreadnought on a Dreadnought at 7 km 27.5%. Tests: `Coarse_fire_allocates_nothing` with the smoke field off (a burning target's smoke now grows the index to a new high, by smoke's own "nothing once grown" rule); two `AimsTests` whose last asserts were the old laying's misses: the escort at point blank under a battlecruiser now sinks in 25 min (asserted: the escort first, then the tramps), and the breaker running from two dreadnoughts at 12 km is hit in her first 2 min and finished (asserted: her commander declines, she runs for her zone at her best speed; whether she gets there is the guns'). |
| **BL1m** | **Steadiness under fire:** where the turns while firing for effect come from (station keeping, the admiral's, the captain's), then what follows from it. **Done 2026-11-01.** Measured (`scenario --play --fire-table` now breaks the heavy guns' salvos laid by the sights down by her rate of turn as she fired, `SalvoReport.YawDegS`, and those under helm by what she was doing): at the Dogger Bank (2 seeds) 26% of the heavy shells were fired turning past 0.2°/s, nearly all 0.2-0.5°/s by **ships keeping station in the line** (2,089 shells; the admirals' turns and the captains' own little), hitting 0.9% against 3.7% steady: every such salvo took the computer's whole own-turn kick, a hard switch at `OwnTurnYawDegS`. **Built:** the kick grows with her rate of turn, the computer's `own_turn_kick_m` given at the reference's turn (`OwnTurnKickYawDegS` 1°/s: fire_control_ref.py's turns run at 1°/s and its ships never yaw otherwise): a Dreyer's 300 m is 90 m at 0.3°/s. The Dogger Bank again: under helm at 0.2-0.5°/s 0.9 → 2.6%, steady 6.6% (the fight itself moved: 2 seeds); fc-history 10 of 23, orderings 4 of 7 (the salvo chaser's ordering in, 3.9 against 3-10). **Left, the user's (proposals):** (1) *steady for the guns*: a station keeper whose chief battery fires for effect keeps the leader's course and keeps station on her revolutions within a band, as the record's ships did (`Behaviours.Station`/`Track`: the plan's follow fine-tuning); (2) the plain computers' (Dumaresq and Vickers clock, hand plot, the plain EU-Anzeiger: 47 blocks) back-to-ranging on any yaw past 0.2°/s is the same kind of hard switch: physically the course set on the Dumaresq going stale, degrees turned since it was set, not her rate of turn. The rest of the gap under helm is confounded: ships under helm are often just through a turn, re-tracking. | The fire under helm's share in the scenarios. |
| **BL1n** | **The panel and fc-history again,** logged; then the user's playtest. **Panel done 2026-11-01** (the log's row); the playtest next. | The ranges and the rates read together. |
| **BL1o** | **A plain computer's own course** (the user, 2026-11-01: "definitely on the physical rule"): a Dumaresq has her course set on it by hand, so a plain computer is put out once she has turned `OwnCourseStaleDeg` (5° [EST]; the Mk IV kept the enemy's relative direction "within a few degrees" in a turn, computing.md) from the course last set, which is set again then; not by her rate of turn (`OwnTurnYawDegS` gone). A helm-free machine as before (a turn past 1°/s; since BL1t not put out by her turns at all). **Built 2026-11-01.** A turn put it out shows as ranging salvos until a straddle has her again (a salvo in the air when she's put out may straddle and bring it straight back). | `FireWorthTests`: a light cruiser's Dumaresq firing for effect at 9 km, four minutes of a 3° weave: no ranging salvo (4 seeds); four of 12° turns: 6. |
| **BL1p** | **The gunnery officer's worth of a salvo** (the user, 2026-11-01: "when turning the gunnery officer should estimate if they have good chances of hitting and maybe pare it back a bit until they are again ready to fire for effect"; "lines snaking around and just lobbing shells for little gain. Shell usage should be reasonably optimized"). Before each salvo he judges its chance a shell, from what he knows, in `Matchup.HitChance`'s form (the target's danger space over the aim's error and the pattern): his aim's doubt (inside a closed bracket its width, open his computer's own sigma), her own motion as he feels it (the own-turn kick at her rate of turn, the roll's slip, his laying), the pattern; and the same without her motion, steady. **Pared back:** while the chance now is under `FireWorthSteadyShare` (0.5 [EST]) of steady, one salvo at a time, the next waiting on the last's fall (ranging's pace), until she steadies. **Shells' worth:** what fire for effect could do from here (the aim at the bracket's floor, steady) under `FireWorthFloorPct` (0.5% a shell [EST]), pared back the same way; under a quarter of that, he holds fire (`BatteryState.Holding`: not worth a shell). The floor rises as the magazines empty (over the rounds-left share, from a quarter left down). Point blank and the night's close fights stay brutal: their chances are high. **Built 2026-11-01**, `Matchup.ShellChance` the one model of a shell's chance (the matchup's `HitChance` and the officer's both). **Changed in the building:** "one salvo at a time" binds nothing on a heavy gun (her reload outlasts a salvo's flight and spot), so while her turn spoils the salvo he **holds it for a steadier moment**, up to `FireWorthWaitReloads` (1 [EST]) of her reload, then fires it; one at a time stays for the shells worth little (the quick-firers' lobbing). His estimate first read optimistic far out (a Dreadnought at 14.6 km: 5.6-6.5% against the guns' 2.3%): his aim's doubt now has his spotter's own error in it (k R² / h through the air, the bracket as good as the spots that set it), and it reads 22% at 7 km against the guns' 29.6%, 7.8% at 11 km against 8.4%, 3.6% at 14.6 km against 1.6%: within about 2x, optimistic only far out; the rest with the matchup's estimates in BL4 (memory: the AI's expectation fitted to measured physics). | `FireWorthTests`: a Yamato-like weaving 20° every minute at 8 km, 8 seeds: 684 shells for 194 hits (28%) held for the steadier moments, against 840 for 133 (16%) fired whenever laid (shares 0.6-0.8 do worse: they wait into worse moments); the floor raised past any salvo: `Holding`, no shells. Steady duels unchanged (nothing to pare). |
| **BL1q** | **The captain's steady course for the guns** (the user, 2026-11-01: "it should be a captain's decision if they want to fire for effect or turn"). A station keeper whose chief battery fires for effect holds the leader's course and keeps station on her revolutions while she's within a band of her station (across a quarter of her spacing, along a half [EST]); past it, or with danger near, he turns as now. **Built 2026-11-01** (`CaptainLayer.SteadyForTheGuns`, before the order is given, for Follow and Station): firing for effect (`CombatStore.FiresForEffect`, the gunnery's word each second, O(1)), a correction under `SteadyForGunsDeg` (5° [EST]: at 20 kn about 100 m off the line) isn't steered: he holds his heading and keeps station by the ordered speed. | `FireWorthTests`: a Dreadnought following 600 m astern of one weaving 3° every two minutes, both firing at a third 9 km off: turned 19° in her 1,304 s of fire for effect against 48° in 1,256 s, 591 m astern at the end. |

| **BL1r** | **The secondaries by what their rounds do** (the user's playtest, 2026-11-01: "Heavy weapons feel pretty good now. The floor could be increased to 1% though... Secondaries still love to spam their shells. At 15km it probably doesn't make sense for the 152mm secondaries just to spray all their shots at the enemy battle line"). **Built 2026-11-01:** `FireWorthFloorPct` 0.5 → 1%; doctrine's secondary floor judged by what a round does, not whether it lands (`SecondaryRoundFloor`, was `SecondaryHitFloor` at 1% a shell): the matchup's hit chance times a hit's chance of putting her out (`Matchup.PerRound`'s form). A 6" round on a Dreadnought: 1.8e-5 at 7 km, 1.3e-5 at 8, 7e-6 at 11 (a hit puts her out 0.07-0.09%, a 15"'s 0.7-0.9%); on a destroyer at 6 km 1.3e-3; a 15" round at 18 km 3.3e-4. The user's pick: a secondary on a battle line inside about 8 km by day, so 1.2e-5 [EST]. **The panel then said otherwise** (`balance/bl1r.json` first run): the medium guns fired more, further out (the Jutland's Germans 5,200 → 10,600 shells, 30% at 10-15 km at 0.3%): by the round's worth alone a 6" takes a destroyer at 10 km it hits with under 1% of its shells, which the hit floor had kept off. So **both**: `SecondaryHitFloor` back beside it (1% a shell, its own field again, after `Scuttle`), a round must land often enough *and* do something when it lands. And the heavy guns hit worse at the same ranges (the Jutland's British 10-15 km 2.9 → 1.4%, beyond 15 km 29 → 42% of their shells): a battery the officer paced for economy stopped telling its captain it fired for effect, and he stopped holding course for it; now the phase alone says so, paced or not. The smallest guns come in smoothly by their hit chance close in (a 47 mm round on a Dreadnought 6e-5 at 500 m, 1.8e-5 at 2 km: takes her inside about 2.5 km; a 76 mm inside about 4 km; the point blank 4-5x over the floor, no rule of its own). Found on the way: `NearMiss`'s static `WetFaces` array (40 B) made by its type's initialiser at the process's first near miss, mid-fight: the faces walked by index (`Coarse_fire_allocates_nothing` failed run alone, passed in the suite). | `FireOrderTests`: a 6" secondary leaves a Dreadnought at 11 km, takes her at 7; takes a destroyer at 11 by the round's worth alone, leaves her there by the hit floor, takes her at 6; floors 0, anything in reach. |

| **BL1s** | **One worth for every battery** (the user, 2026-11-01: "think a bit about unifying the secondaries and main battery rules"; "please do, and then test it well"). A round's worth is the gun's and the target's, not the battery's rank: the two floors (`HitFloor` 1% a shell, `RoundFloor` 1.2e-5 a round; renamed from `SecondaryHitFloor`/`SecondaryRoundFloor`, their places kept) apply to main batteries at will too. Checked first by the matchup: a battlecruiser's 13.5" on a battlecruiser hits 2.5% at 18 km and 1.9% at 20 (2.4e-4 a round: the Dogger Bank's opening kept); a light cruiser's 6" on a destroyer 3.0% at 8 km, 1.2% at 11, under 1% past about 12 (the panel's long medium fire). Kept apart, as role not worth: the secondaries' preference for the smallest target, an ordered Engage over both. | `FireOrderTests`: a light cruiser's main leaves a destroyer at 13 km, takes her at 9, takes her at 13 with the floors at 0; a battlecruiser takes another at 19 km, a battleship another at 15; the secondaries' cases as BL1r. |
| **BL1t** | **No pulsing falls under helm** (the user's playtest, 2026-11-01, 16 Bismarcks at the Jutland, snaking: "Sometimes the shots on a target ship pulse, as in go very far, then near it, then very far again... 1km+ overshoots... No gunnery officer worth their salt will accept a teleporting target"). **Found** (a Bismarck on a Bismarck at 15 km, the German discipline, snaking 40° every minute on full rudder): the solution never jumped; every turn past 1°/s sent her back to ranging (`HardTurnYawDegS`, a hard switch on a helm-free machine whose solution follows her turn: nothing in it goes stale), and each return fired the German ladder at its whole 366 m about the bracket, three salvos in a few seconds: short 600-1,000 m, near, over 600-1,000 m, leg after leg (22 of 28 salvos after the first fire for effect ranging; 7 fell past 600 m). **Built 2026-11-01:** a ladder's rungs at the battery's ranging step (`Bracket.Step`, the first step that already scales with the computer's doubt, F6 item 3), never more than the doctrine's `LadderStepM`; a helm-free computer isn't put out by her own turns (`HardTurnYawDegS` gone): what a turn costs is the own-turn kick salvo by salvo (BL1m), which the officer holds for (BL1p); the spots' misses still send her back to ranging when she has really lost the target. A plain computer's stale course (BL1o) and a tracker seeing the target turn put her out as before. The probe: ranging 22 → 5 of ~25 salvos after the first fire for effect, falls past 600 m 7 → 0, rms off the target 491 → 246 m (2 seeds); the opening ladder ±225 m at 15 km. fc-history 9 → 8 of 23: only the German-ladder cases moved (the German battleships 5.5 → 7.1%, band 1.5-6%; the 1st SG 6.2 → 6.0%; Denmark Strait 12.5 → 13.3%), the orderings unmoved (4 of 7): a ladder at its doubt straddles sooner, the base gunnery's own improvement, left for the degraders. | `FireWorthTests`: the snaking Bismarck, 4 seeds: ranging under a quarter of her salvos after the first fire for effect, under 8% fall past 600 m, the ladder's rungs under 366 m. |
| **BL1u** | **Every discipline under a snake** (the user, 2026-11-01: "we want the turn reset to go away. What was done historically?"; "check what the other firing disciplines have to weed out the issues. Fighting will be bloodier until we get to later stages of balancing again"). **The record on own turns** (computing.md §3, fire-control-research.md §5.5, §8): helm-free machines held their solutions through own turns, at the cost of the fall's centre moving (North Carolina 1945 through turns of 450° and 100°, "several hundred yards"; the AFCT "OK", the Dreyer "partial (helm-free)", the Argo "mostly OK"); nothing sends them back to ranging: BL1t's removal stands. The plain clocks lost their rates (BL1o's 5° rule stands). Open: the Type 92 ("poor: plot-based rates must be rebuilt after maneuvers" [UNCERTAIN]) is a helm-free row with a 400 m kick, as fire_control_ref.py's 4j has it; the user's to decide whether it becomes a plain clock. **The sweep** (scratch probe, 3 seeds a cell: ten designs covering every computer kind, each under the five disciplines, steady, shooter snaking and target snaking 40° a minute on full rudder): with a rangefinder and a computer, rn-1914, rn-1917, usn and independent fall past 600 m 0-3 times in about 75 salvos and never far-near-far; the plain clocks range again under their own snake as BL1o means. Two faults: (1) **the German ladder never closed in:** once fire for effect went back to ranging, each group fell at the same ±step about the bracket's middle, its spots narrowing the bracket back to about the step, and only a straddle (rare from a ranging salvo of 2 guns) took it to fire for effect: a steady Dreadnought ranged 35 of 89 salvos after its first fire for effect. **Built 2026-11-01:** once the falls have closed the bracket, the rungs sit inside it at a quarter of its width, so each group halves it (the RN's 400-200-100, the USN's "reverse and halve"; at the floor, the rocking ladder's ±100 yd). The armoured cruiser on German rules, steady: hits 6.6 → 10.3%, the guns' lay jumping over 300 m between salvos 5 → 0 times; under her own snake 3.8 → 7.1%, falls past 600 m 11 → 0; borodino snaking 6.7 → 12.2%. (2) **the post-war radar stand-in ranges by eye** (bugs.md, Balance: `rangefinder_m` 0 on six designs), left for the radar pass or a design fix. Not faults: local control by eye (the 1890s TBD) chases a course guessed tens of degrees off, its open-bracket step 550 m; a Dreadnought's 2-gun ranging salvos spot noisily against a 12" pattern. fc-history 8 of 23 as BL1t (Denmark Strait 13.3 → 16.2%, the German 1st SG 6.0 → 5.9%), orderings 4 of 7. | `FireWorthTests`: German-ladder armoured cruisers 8 km apart, steady, 3 seeds: a later group's step under half the first's, under 20% of salvos ranging after the first fire for effect. |
| **BL1v** | **The spots walk the fall onto her** (the user's playtest, 2026-11-02, squadrons: "Relatively decent clusters but the firing ships just can't get them centered to the target even if both lines are moving at constant speed. The spotting mechanic really should walk basically arbitrary error into target and keep it there, eventually"; "dropping to ladder should only happen when one has clearly lost the range"). **Found** (`scenario --play --salvo-log`, 4 seeds): the main guns' laid error in fire for effect 520 m RMS and 109 m short on average, no better at salvo 30 than salvo 1; the spotter's calls mostly right. The bracket's hold lapsed 120 s after a straddle and `Bracket.Aim` then gave no correction while the bracket held 0, putting the guns back on the bare solution and its ballistic bias; each straddle call reset the bracket to one salvo's correction; three misses in a row (`MissesToRange`) sent fire for effect back to ranging 200-250 times in 4 seeds. **Built 2026-11-02:** firing for effect the spots walk the correction (`GunnerySystem.Walk`): a straddle holds it and halves the step; an over or short steps it from the correction its salvo was fired with, the step doubled on a second miss the same way and halved on a turn, from the bracket's floor (92 m) to the computer's first step (366 m); a spot of a salvo fired before the last step that says what it already answered changes nothing. The range is lost, and the battery ranges again from beyond that salvo's correction, only when a step at full stride still falls the same side (`MissesToRange` gone). The bracket (ranging) keeps what the spots learnt however long ago (`SpotsHoldS` gone). **The rate spot removed** (F6c's RN "down 200, rate down 100", `RateSpotShare`, `FcTracker.CorrectRate`): its "move" was the change across the spot, hundreds of metres when a spot's salvo predated the last step, and it put up to 6 m/s into the clock; taken from the walk's own steps, even only on a run of three, it fed the spots' noise into a rate the computer learns from its readings, 20-60 m of the fall's centre on every bench. **Measured:** the fall's centre (laid plus the engagement's bias) RMS, Lion on Derfflinger, 48 seeds a case, bracket → walk: 16 km parallel 214 → 194 m, 16 km converging 173 → 148, 12 km 149 → 128, 16 km in 14 km visibility 470 → 463. Squadrons, fire for effect: the median laid miss 213 → 204 m at 10-14 km, 159 → 146 under 10 km, the mean 109 → 42 m short, back to ranging 41 times in 4 seeds; the 14+ km ranging salvos laid past 1 km 13.5 → 23.7% (half as many of them): open. fc-history within its noise case by case (Dogger Bank's British 4.8 → 5.2%, the Jutland BCS 5.1 → 4.6%, the German 1st SG 5.2 → 4.5%). **Left (F7):** with an in-line splash judged by whether it hides her hull or is hidden by it (25 m) rather than by depth (k·R²/h, 300-600 m a splash at 15 km), the clean benches didn't move but the hazy one went 461 → 345 m; and the target churn (the captains' switches, the next step). | `FireDisciplineTests`, `FireWorthTests`, `GunneryTests` (level 3 at 16 kyd 8.2%, its band widened to 2.5-10%). |
| **BL1w** | **The error ladder** (the user, 2026-11-02, after BL1v went in circles: "let's try to fix this on a simple way from first principles"; "we have many moving knobs here so we need to isolate the issues"). A fall's place against the target is the computer's error, the ballistic bias, the spots' correction, the salvo-to-salvo error and the shells' dispersion; the spots must take the persistent part off, eventually. **Built 2026-11-02:** `SimSettings.GunneryErrors` scales each error the chain draws (readings, the eye's first guess at her course and speed, the residual bias, laying, salvo-to-salvo, dispersion, spotting; 1 in play, the computer's and the officer's beliefs untouched; the squadrons hash unchanged); `shipgen duel --errors all=0,bias=1 --swell 0 --wind 0,0`; `tools/fc_ladder.py` runs the rungs seed by seed, `tools/salvo_log.py` reads `scenario --play --salvo-log` CSVs. **The baseline** (Lion on Derfflinger, 16 km, both steady at 22 kn on parallel courses, a flat calm, 16 seeds; RMS in range over the last 5 of 15 minutes, m): everything off, the computer exact: laid 145, fall 164 (84 over on average), 12% hits (want about 0 and nearly every salvo); the bias alone: fall 68, 82%; salvo-to-salvo alone: laid 218 (want 0), fall 335; dispersion alone: laid 101; laying alone: 91; spotting alone: 127; the eye's guess alone: 116; the readings alone: the computer 263 (190 of it constant in a run, the instruments' bias; 166 wandering), laid 119; everything: the computer 227, laid 220, fall 299, 6%. **Three faults:** (1) ranging never closes with perfect information: each bound widens by the computer's believed rate doubt (about 5 m/s, 200 m a salvo) as fast as the spots narrow the bracket; (2) the walk chases the salvos' own noise, every call at least 92 m and two alike doubled; (3) the computer doesn't converge on a steady target: the rangetakers' wander (`RangefinderWanderTauS`) to check against the research. Fixed in that order: (2), (1), (3), each with its rung's pass. | the rungs |
| **BL1x** | **The spots' running mean** (BL1w's fault 2, the walk chasing the salvos' noise). **Built 2026-11-02:** firing for effect each spot steps the correction in force down (over) or up (short) by a step that shrinks as the evidence comes in, half the bracket's width when fire for effect began over the count of crossings and straddles (Kesten's rule: calls the same way don't shrink it), never under `SpotStepFloorM` (23 m [EST], a range dial's 25 yd); from the third call the same way in a row it doubles back (two alike are chance, three a target that has moved). The range is lost only after `RangeLostCalls` (4 [EST]) the same way that have walked a whole ranging step. **The rungs** (dispersion on, so a straddle can be called; the last 5 of 15 min; the walk → the mean; the laying's wander within a run, m): dispersion alone 73 → 8, the fall 120 → 71 (the four shells' own mean) and +7 → +3 on average; with salvo-to-salvo 110 → 19, the fall 313 → 271 (the salvo error itself); with the bias 47 → 9, the fall 91 → 73; with laying 50 → 10; with spotting noise 49 → 11, the fall 129 → 102; everything on 111 → 67, the fall about 300 either way (the salvo error and the computer's own wander, fault 3). **Against a zigzagging boat** (`CoarseParityTests`, 48 seeds, fine-tick hits): secondaries at 6 km 629 → 473, a light cruiser at 8 km 739 → 611 (before the growth on three: 452, 512): the walk's noise chasing followed the legs; open, as evasion's own question. Coarse against fine within its band (-15%, -9%). | the rungs; `CoarseParityTests` |
| **BL1y** | **Ranging ends when the fall crosses her** (BL1w's fault 1). **Built 2026-11-02:** ranging goes to fire for effect on a straddle or when an over and a short have been fired within a pattern of each other (`Fc.OverAt`, `ShortAt`, the corrections as fired, on today's solution; the RN's "correct until the fall crosses the target", the USN's "never below the pattern"), from their middle at half their gap; the halving after is the running mean's. The bracket's bounds, widened by the computer's doubt of the rate as they aged (5 m/s, 200 m a salvo), had never closed with perfect information. **The mean's floor** `SpotStepFloorM` 23 → 46 m (50 yd, the RN's corrections in fifties and hundreds): the rangetakers' error wanders over minutes (`RangefinderWanderTauS` 300 s, the user's of 2026-10-18: "the spots walk it out") and at 23 m the mean lagged it (a closing duel's mean fall slid to -47 m); at 92 m it rocked across a target smaller than its step (everything off: 0% hits). **The rungs** (16 km, last 5 of 15 min): everything off 12 → 100% hits, the fall 164 → 4 m; the bias alone 82 → 100%; the eye's guess alone 44 → 100%; the readings alone (dispersion on) the fall 89 → 116 m parallel, 89 → 76 converging; everything on 314 → 295 parallel, 201 → 190 converging. Zigzag duels (`CoarseParityTests`, fine hits): 473 → 465, 611 → 600 (the walk's 629, 739). Squadrons (4 seeds, against BL1v): fire for effect's median laid miss at 10-14 km 204 → 189 m, its hits 6.2 → 7.3%; but its mean fall grows short with time in fire for effect (-75 m in its first salvos, -197 by its fifteenth): the computer's drift in a closing, turning fight, BL1w's fault 3. | the rungs; `FireWorthTests` (the German ladder: a group or two find her) |
| **BL1z** | **The guns follow the computer** (BL1w's fault 3; the user, 2026-11-02: "Decision accepted"). **Found:** with perfect readings a plain rate clock on a crossing target (Lion on Derfflinger, 14 km, her course 220°: crossing at 20 m/s) holds a steady error of about 190 m (fire-control-research.md §5.3, the clocks' drift; `fc-converge`'s presets 2-3, 260-340 yd), its readings pulling it back each time; `Rebase` took every such pull out of the guns (so a reading never moved them: "Sir, the target teleported 200 m", the user, 2026-10-18), and the clock's lag piled up in the laying at about 4 m/s; the spots called short while the correction went down, the range lost again and again. Squadrons' lines converge at 40°: its fire for effect fell shorter the longer it lasted (-75 m in its first salvos, -197 by its fifteenth). **Built 2026-11-02:** `Rebase` gone (`Fc.Shift`, `Salvo.Shift`, `Bracket.Shift` with it): the gun's range is the computer's with the spots' correction on it, as the clock's range cuts moved the guns and the spotting correction was its own dial; the readings' gate (`Plausible`) still sets a wild one aside. **Measured** (14 km, the last 5 of 15 min, the fall RMS; Rebase → none): crossing, everything on 735 → 446 m (its mean -548 → -16), the computer's clock drift alone 311 → 114 (-250 → -4), ranging again 26 → 0%; closing 162 → 169 (hits 26 → 28%); parallel 253 → 281, the rangetakers' 300 s wander now in the lay (its wander within a run 44 → 110 m) for the spots to walk out. Squadrons (4 seeds): fire for effect centred (mean -90 → -14 m; by its age -83, -28, +43, +51, +23, -16), hits 8.5 → 9.8%. Zigzag duels (fine hits): the light cruiser 600 → 719, the secondaries 465 → 393. | `After_a_hit_the_guns_hold_their_lay`: the lay after a hit moves 92 m by eye at 3 km, 89 m at 15 km (bound 60 → 120) |
| **BL1aa** | **Reading errors once per target** (the user, 2026-11-02, after BL1z let the readings move the guns again: "It used to be once per spot and that made it wander like hell"; "once per fresh target"). **Built 2026-11-02:** each instrument draws its errors when it's put on a target, its bias, its man's misjudgement of her range and of her bearing (`Sensor.RangeZ`, `BearingZ`, in sigmas), and holds them for her; a reading's error is those times its sigma now (the range, the air, his stress), never a fresh draw nor a wander (`RangefinderWanderTauS`, `EyeWanderTauS`, `RangefinderFreshCoincidence`, `RangefinderFreshStereo` gone); the eye's misjudgement of her course and speed is drawn once a target too (`Fc.GuessSpeedZ`, `GuessCourseZ`), each new look at her misjudging her the same way. **Measured** (14 km, the last 5 of 15 min, fall RMS, BL1z → this): parallel, readings only 146 → 108 m, everything on 281 → 263 (hits 6.9 → 8.7%); closing 69 → 64, 169 → 161; crossing 266 → 279 and 446 → 381, but its mean -10 → -83 and -16 → -87: there the computer's error grows (-184 → -304 m, a plain clock's drift on a crossing target) and the spots' mean lags it. Zigzag duels (fine hits): secondaries at 6 km 393 → 806, the light cruiser 719 → 702. `SpotStepFloorM` stays 46 m, now for the clock's drift. **Left:** in fire for effect the computer's solution still moves 58 m from salvo to salvo (the spots' correction 10 m; Lion at 15 km, readings only): likely the computer taking its instruments' readings in turn, each with its own error, so it swings between them rather than settling on their mean; the lay after a hit moves 83 m (Lion) and 98 m (Mikasa by eye). | `GivingUpTests`: a salvo in the air as she strikes may still hit (the count taken 30 s later) |
| **BL1ab** | **The plot takes each instrument's offset** (BL1aa's "Left", the user, 2026-11-02: confirm on the ladder first). **Confirmed** (Lion on Derfflinger, 15 km, parallel, 16 seeds, fire for effect after 5 min; the computer's aim, its change salvo to salvo RMS): readings alone 82 m (wander within a run 72 m); every instrument given the same draw 7.6 m, one instrument only 6.7 m. So the computer took its instruments' readings in turn, each with its own error held for the target, and swung toward whichever had cut last. **Built 2026-11-02:** the plot marked each instrument's cuts with its own symbol so the plotter saw how each lay off the others (fire-control-research.md §3.5, the 1918 handbook); each instrument keeps its offset from the computer's line, the running mean of its last `PlotCuts` (5 [INFERRED]) cuts, and a reading goes into the computer less its offset from the instruments' mean, each weighed by its spread (1/σ²): the computer settles on their mean (`GunnerySystem.Plot`, `FcReading.Sensor`). No random draw added. **Measured:** the swing, readings alone 82 → 11 m, everything on 66 → 3 m; Mikasa by eye at 6 km 32 → 8-10 m. The ladder at 14 km (fire for effect, 10-15 min, BL1aa → this): parallel readings alone fall 93 → 17 m (hits 41 → 96%), everything on 263 → 233 (8.7 → 10.3%), the lay's wander 67 → 28; closing 159 → 148 (hits 20.7 → 18.1%); crossing 381 → 280, its mean -87 → -2, the computer's own error 508 → 273. At 18 km parallel everything on: the first 3 min (ranging) fall 497 → 450, hits 3.2 → 5.1%. Squadrons, 12 seeds, heavy guns: 14+ km hits 2.1 → 2.4%, fire for effect laid median 319 → 288 m; 10-14 km 6.5 → 6.2%; fire for effect's mean fall at salvos 6-14 about -35 → -10 m (4 seeds had shown 14+ km down: noise). `Fight_turns_away_when_outmatched` 18 → 19 km (a hit at 150 s took her speed below theirs). | the swing's ladder: `duel --errors all=0,readings=1` on the aim column |
| **BL1ac** | **The salvo error from its parts** (the user, 2026-11-02: `salvo_pct` "feels like ad hoc number... We should derive those numbers"; "no need to go super deep as long as the variables are placed at the correct place"). The reference lumped into `salvo_pct` (and `salvo_defl_mil` in line) "stable-element and servo error, salvo-mean muzzle-velocity scatter, turret-to-turret calibration, generated-range wander" (fire_control_ref.py; fire-control-research.md §0 item 4: laying, roll, servo, salvo-mean muzzle velocity), drawn as one Gaussian at the end of the chain because its own chain had none of them. **Each part, where the sim has it now:** the generated range's wander is the computer's own (its track, the plot: BL1aa-ab, 3-11 m salvo to salvo); the director layer's laying is `lay_mil`'s draw (BL1l), and the trainer's in line; the stable element's and the pointers' lag following the roll, pitch and yaw are BL1b/BL1h's `RollTauS` slips; the salvo-mean muzzle velocity is each shell's own draw about the aim (the round-to-round spread, 0.3% of velocity on mature smokeless, about 0.4% of range a shell, is most of the measured pattern's σ, ~0.5%, and its mean over n shells scatters as σ/√n by itself); each gun's calibration and its layer's follow-the-pointer error (computing.md §5.3: per gun) are inside the measured `pattern_pct` (§7: "gun-to-gun differences from wear and calibration"). So the draw counted them all a second time, and is gone (`ComputerRow.SalvoPct`, `MountTechRow.SalvoDeflMil`, the designs' and templates' keys, the ladder's `salvo` rung; a design saved with them still checks: `FireControlTemplates.Retired`). **Not modelled, small or for later:** the dial transmitter's 25 yd step (predreadnought-tech.md §2.3: about 7 m RMS), the wind's gusts over a shell's flight (with the weather), the charges warming through an action (a drift the spots follow). **Measured** (Lion on Derfflinger, 14 km parallel, 16 seeds, the last 5 of 15 min): calm, fall 233 → 111 m, hits 10.3 → 17.7%; the default sea 20.7%. The panel (`balance/bl1ab.json` → `bl1ac.json`): fc-history 9 → 5 of 23 in band (14 → 18 HIGH: the Grand Fleet BS 12.7 → 20.6%, the 1st/2nd BCS 6.9 → 10.5%, the USN 1944 study at 10 kyd 51 → 68%); the scenarios 4 → 6 of 14 sides within 2x, the record's loser lost more in 20 → 26 of 44; the Jutland's heavy guns British 4.6 → 6.2%, German 2.7 → 4.8%. As the user foresaw ("hits may rise, sea state etc. bring them down later"): the base gunnery now has no lumped degrader, and what is missing (fatigue, weather, smoke on the sights, the record's chaos) comes as its own system. Tests: `GunneryTests`' bands widened (level 3 18%, Lion on Seydlitz 12.9%), the Dumaresq turn test watches the phase (the salvo in the air now straddles her back before a ranging salvo goes), the breaker caught by a hit goes for her zone by GoTo. | the ladder; `balance/bl1ac.json` |
| **BL1ad** | **A shift of target keeps the battery's own ballistics** (the user, 2026-11-02: the captains' cost of switching, "get that part out of the way"; plan agreed the same day). A shift threw everything away (`EngageNow`): the track, the bracket and a fresh draw of the residual ballistic bias. The bias is the guns' and the day's air (wind, density, the powder's lot, drift), not the target's. **Plan:** the residual bias drawn once a ship's battery; an engagement ending in fire for effect leaves its battery what its spots learnt of the bias, its correction taken as a reading of it and shrunk by Bayes against what else is in it (the computer's doubt of her place, the walk's step): kept share σb²/(σb²+σe²), the doubt left σbσe/√(σb²+σe²), in range a share of it, in line an angle (`CombatStore.Carried`); the next engagement opens its bracket about it and its first step doubts only what's left [INFERRED: no source on the RN's spotting correction kept over a shift]. The plot, the clock and the bracket stay the target's. **Built 2026-11-02:** `FireControl.Bracket.Mid` (an open bracket's correction), `CombatStore.CarriedCorrection` (left by `GunnerySystem.Free` when it ends firing for effect, `Learnt`: σe the computer's doubt of her place, its instruments' held error that the plot's mean keeps, 1/Σ 1/σᵢ², and the walk's step), `Fc.CarryR`, `ResidShare`, `DeflDoubtRad` (the first step and the officer's worth read the doubt left). **The ladder** (Lion shifting between two Seydlitz at 15 km on either bow, 24 seeds, after 10 min on the first): the first salvo's fall off the second, the bias and dispersion only, 178 m engaged fresh → 75 m after a shift. With every error on, the computer's own error on the new target is about the bias's size (the correction at the shift 179 m RMS, 142 m of it not the bias, k ≈ 0.72): fire for effect 102 s fresh, 99 s shifted carrying nothing, 91 s carrying it; hits in the first 5 min alike (7.2, 9.3, 9.0%). The first salvo itself isn't the measure with everything on: a shifted battery's turrets already lie near her, so it fires as soon as the track settles, where a fresh one trains round for 40 s. **Found on the way (open, from BL1ac):** level 1 (local control) and level 3 (a director) now hit alike at 16 kyd (96 duels: 15.1, 15.6%; BL1ac 15.2, 16.2): level 3's lead had been mostly its smaller `salvo_pct`; `GunneryTests`' order kept for the eyeball only. | `GunneryTests.A_shift_of_target_keeps_what_the_spots_learnt_of_the_guns` |
| **BL1ae** | **The captain's cost of a shift** (BL1ad's plan). The captain's pick (`CaptainLayer.Fire.Pick`) shifted at a fixed 0.7 of the held target's score (`FireSwitchShare`). **Plan:** he weighs the fire over a horizon `H` = 600 s [EST: ten minutes, in which the ranges change enough to choose again]: he shifts if v_new·(H − T_new) > v_cur·(H − T_left), v the score's inverse, T_new the time to range her (the settling and the ranging salvos at her ranging cycle, the time of flight, the spot's delay and the wait margin; the count and the settling from the battery's own record this battle, before it has one the record's 2-5 salvos, fire-control-research.md §10), T_left what the held target still needs (0 firing for effect). `FireSwitchShare` goes; `FireKeepShare` (the edge of reach) stays. A target it can't fire on is left at once, as before. **Tests:** firing for effect it holds against a slightly better target, shifts for a much better one; ranging, it shifts sooner. **Built 2026-11-02:** `CombatStore.RangingRecord` (the gunnery's word: when the engagement began; at each fire for effect, the track's settling and the ranging in cycles), `GunnerySystem.RangingCycleS` (the flight from the range table, the spot's delay and the wait's margin, or the reload), `CaptainLayer.Fire.RangingS` and the compare in `Pick`; `FireSwitchShare` gone, `FireHorizonS` 600, `RangingOpenPriorS` 60, `RangingCyclesPrior` 3.5. Ranging about 100 s, firing for effect he shifts at about 0.83 of the held one's score (the share was 0.7), ranging at a tie. On the way: `ReachM`'s cache was a plain dictionary filled from the captains' thinks on the workers, a concurrent one now. | `FireOrderTests.A_shift_of_target_is_worth_its_ranging` |
| **BL1af** | **Measure the shifts** (BL1ae's): squadrons 12 seeds through `tools/salvo_log.py` (the median hold, shifts, back to the one before), the Jutland fire table, the panel. Then the user's call on `CrippleQuietS` (90 s: a target "done fighting" flipped the choices; 240 s halved the flips but touches BL2's cripples), read with the cost in. Out of this pass: the admiral's distribution of fire (his group's orders don't go through the pick), a shift within a line keeping the range itself. **Measured 2026-11-02** (squadrons 12 seeds, `salvo_log.py`; BL1ad's share → the cost → the cost with `CrippleQuietS` 240): shifts 967 → 1,161 → 739, back to the one before 323 → 395 → 240, the median hold 226 → 153 → 350 s, hits by range alike. **Why the cost alone shifts more:** of 982 shifts in 2 seeds, 710 came ranging at a score within 0.7 of the held one's, and in 818 the held target's `Matters` was under 0.95: two ships 10.6 and 11.4 km off, the battery flipping between them each minute. A ship under fire turns away (her guns off the bearing, the range opening), so within 90 s she reads half done fighting and the fire leaves her; free of it she comes back and reads fighting again. With 240 s: 982 → 367 shifts in the same seeds. **The panel** (`bl1ac` → `bl1ae` → `bl1af-quiet240`): within 2x 6 → 5 → 4 of 14, the record's loser lost more 26 → 26 → 23 of 44, fc-history's orderings 3 → 5 → 5 of 7; Heligoland's Germans lost 15.1 → 15.9 → 12.0 (the record 4), Dogger's 8.6 → 10.9 → 9.4, the Jutland's 51.0 → 57.8 → 56.0. `CrippleQuietS` 240 s, the user's call (2026-11-02: "toss 240 s in for now"); reading a turn away under fire apart from done fighting is the fuller fix, left. | `balance/bl1ae.json`, `balance/bl1af-quiet240.json` |
| **BL1ag** | **The levels where they must differ** (the user, 2026-11-02, on level 1 and level 3 hitting alike in a steady duel: "In ideal circumstances one is not really expecting any difference, so we should do a test with a situation where difference is expected and not just ram arbitrary numbers"; one of the reasons the project began: games where an older system that would work fine still gets magic randomness). Level 1 (FA 2 m, a hand plot, each gun laying itself) against level 3 (FQ/FT 4 m, the Dreyer Mk III-V, director firing), QE on QE at 16 kyd, 16 duels. **Steady and parallel** (96 duels): 15.1, 15.6%: nothing there for the Dreyer's helm-free feed or its first guess to do, and a director's laying error moves every gun's fall where each layer's own averages out in the salvo. **Own ship zigzagging** 10° either side every 90 s: the readings only, level 1 79 → 15% (its hand plot put out by each turn), level 3 82 → 81% (fed her own course and speed): as it must; every error on, level 1 15.7 → 13.9%, level 3 14.4 → **10.6%**, and without the laying rung level 3 holds 18.3 → 18.1%: the Dreyer's `own_turn_kick_m` (300 m at 1°/s, a Gaussian a salvo, the reference's "Mk 8 several hundred yards"; the hand plot's 0) eats its advantage, a lumped number as `salvo_pct` was. **Opening fire:** the readings only, the first straddle 266 s (level 1) v 193 s (level 3), hits in 5 min 27 v 44%; every error on 141 v 143 s, 9.5 v 8.2% (not yet looked into; perhaps both wait the open's longest, `GunneryOpenS` 120 s). **Built:** `GunneryTests.Where_the_levels_differ_level_3_does_better` asserts both on their rungs; the steady test keeps the eyeball below. **Next:** the whole ladder checked so, step by step (`docs/battle/fc-ladder-check.md`, FL0-FL11; the user, 2026-11-02), from FL1: the own-turn kick from what makes it (the own course and speed fed late in a turn: the gyro repeat, the log; the director's yaw slip is BL1h's already), and the opening with every error on. | `GunneryTests` |

**After the fire control (the user, 2026-11-01):** salvo chasing for evasion: "first we tune how to shoot, then we'll tune what happens if you're being shot at." A captain under fire steering for the last fall, as the record's light cruisers did in the run to the north (BL1l's breaker test; fc-history's steady-against-chaser ordering), once BL1's shooting is settled.

**What chasing can buy, measured (2026-11-01, after BL1u, `scenario jutland.json --play --seeds 2 --fire-table`):** lost 40.5 of 151 British and 46 of 99 German (the record 14 v 11); 7,060 hits a run. Evasion works only where the shell flies long (evasion-research.md: heavy guns beyond about 9 kyd, hits halved; destroyers inside 4 kyd nothing, or worse), and the heavy guns' hits beyond 10 km are about 450 of the 7,060 (6%); on large ships the heavy hits inside 6 km (about 740) outnumber those beyond (about 470). The bulk is close: under-120 mm guns by day at large ships 1,900 hits at 2-4 km (24%), 1,220 point blank inside 2 km, 1,120 at 4-6 km. Inside 4 km the shooters are ships going home through the enemy (day 895, night 556 hits), flotillas gaining bearing in their day attacks (805 + 301), ships whose intercept is done (751), scouts shadowing (683 + 446 + 164), screens on station (676), the night attacks (698). So salvo chasing is right for its own sake (the 10+ km duels, the shadowers, the runners) but takes off a few percent of the blood; the rest is who closes by day (BL5's question again, after BL2, BL5's fixes and striking colours).

### BL5 again: the blood inside 4 km is the cripples (2026-11-01, the user: "Close-range first")

`--fire-table` now also breaks down the fire at ships going home (a captain's Home order): who fires, the range, closing
or not, her speed, whether the shooter had a fighting enemy (in sight of her side, not going home) within the battery's
reach, and each cripple's hits after she turned for home and whether she got there. The Jutland, 2 seeds:

- **211 ships fell out and went home under fire** (about 105 a run of 250); **155 of them were lost** (73%). They took
  **5,381 hits going home**, over a third of all the hits (median 10 a ship, the top quarter 35 or more, the most 309).
- **Why so many fall out:** 115 of the 211 for their main guns (326 main mounts out: guns out 133, gunhouse out of
  action 93, magazine flooded by the sea 47, gunhouse destroyed 25); the rest for speed, list and water.
- **Who fires on them:** mostly ships keeping station in their lines under their admirals' Fight in an attack, closing
  on slow cripples at 2-6 km; then screens, shadowers, ships whose intercept is done.
- **3,332 of the 5,381 (62%) were fired with a fighting enemy in sight within the battery's reach;** 2,049 with none
  (finishing a cripple with nothing better to do: the user's "an important thing", 2026-10-30).
- **Why:** a target's score is her lead over his worth of her (`Worth` by how done for she's seen, BL2a's `Matters`,
  the aims' `Want`). `Matters` is 1 while her guns are seen firing: a cripple going home that fires back at whoever is
  near counts in full, and she's the nearest and the easiest to hit. `Matchup.Threat` (what her guns and torpedoes are
  expected to put out of him) weighs a target only when an aim wants something sunk, and from her class's whole
  armament, not what she has left.

**Proposed:**

1. **A target by what she can still do to him** (the user's "fire at what's firing at you... cripples are not
   interesting when there is something better to do"): `Threat` in every target choice, not only the aims': a fighting
   enemy in reach that can hurt him comes before a cripple making away with a turret left; with nothing else in reach
   the cripple is taken as now. Her threat from what his navy sees of her: the guns seen in her salvos (their flashes),
   her speed, her course away, not her class's full armament.
2. **Fewer cripples to begin with (the root):** about 105 fall-outs a run against the record's handful; over half for
   main guns. A `damage-history`-style check of mounts put out a hit against the record (Jutland's turrets knocked out
   on both sides), then BL3's damage leftovers and BL1's missing degraders (fatigue, smoke over the guns) by what it
   shows.
3. (Alternative to 1) **The admiral's fire distribution:** a line fights the enemy's line ship for ship by its
   admiral's signal, as the record's fleets did; a cripple out of the enemy line drops out of the distribution and is
   left to whoever has nothing better.

**The user (2026-11-01):** "What was done historically? Alternatively we can make this a decision in doctrine, as in
focus on winning the fight vs focus on sinking as much tonnage as possible."

**The record** (the standard accounts; not yet in the research files, a request): the battle fleets' doctrine put the
enemy's fighting line first and left the cripples to the light forces' torpedoes, and the famous lapses were costly.
The Grand Fleet's battle orders distributed fire ship for ship along the enemy line (the run to the south's misread
distribution left Derfflinger unfired on). Dogger Bank: Beatty's signals misread, the battlecruisers turned on the
crippled Blücher and Hipper's three got away, held a blunder. Jutland: Wiesbaden, stopped between the fleets, drew fire
from many passing British ships (often the only target in the mist), and Arbuthnot's Defence and Warrior, closing to
finish her, were caught by the German heavy ships (Defence blew up). Tsushima: the Japanese line shifted from a
Suvorov fallen out burning to the next fighting ships; torpedo boats finished her that evening. Coronel: the squadron
went on after Good Hope; the crippled Monmouth was found and finished by Nürnberg alone.

**Proposed (after the user):** a doctrine field for what the fire is for (`Targets`):
- **Fight** (the default, the record's doctrine): a target by what she can still do to his ship and his line
  (`Matchup.Threat` from what's seen of her: the guns in her salvos, her speed, her course away); a cripple making
  away counts at doctrine's `Finish` share even while she fires back, so the fighting enemy in reach comes first, and
  the cripple is taken when nothing that fights is in reach.
- **Tonnage:** a target by the tonnage his fire can expect to sink a shell (the matchup's per round over her
  displacement): the cripples first, the easy kills, as Arbuthnot did.
Scenarios and players set it per side and per unit, as doctrine does (a flotilla's "tonnage" beside a line's "fight").

**BL5e, built 2026-11-01** (the user: "Please do"): doctrine's `Targets`, **fight** (the default preset's) or
**tonnage**; tonnage takes `Finish` as 1 (every enemy alike, as before), fight takes doctrine's `Finish` (0.1).
"Done fighting" in `CaptainLayer.Matters` is now her guns quiet a while **or her chief battery's turrets seen silent**:
each mount records when it last fired (`MountState.FiredMs`), and each look with her in sight reads the share of her
chief mounts that haven't fired for `SilentMountReloads` (2 [EST]) of their reloads, to whole over as long again
(`Contact.MountsQuiet`), so a cripple firing her last turret back is read as 80% silent, not as fighting. The screen's
nearest enemy reads it the same way. The launch screen's rule "The guns are for": winning the fight, sinking tonnage.
`FireOrderTests`: a dreadnought making away with four of five turrets jammed, firing the fifth: fight takes the one 11 km
off that's still fighting, tonnage the cripple.

**Measured** (the panel, `balance/bl5e.json`, against BL1s): the light forces' fights bleed less, the Heligoland Bight
18.4 → 14.8 ships lost a run (the Germans 14.6 → 12.0, the record 4), the Dogger Bank 17.9 → 14.8, Åland 10.4 → 9.1;
the Jutland 85.8 → 84.2, its hits unmoved (7,122 → 7,092): the fire moved, it didn't shrink. On the Jutland (2 seeds,
`--fire-table`) the fire on ships going home didn't fall: 234 went home (211), 6,395 hits on them (5,381), 4,438 with a
fighting enemy in reach (3,332). The top rows are cripples **seen silent** at 0-4 km, mostly inside 2 km: `Finish` 0.1
counts her range ten times longer, so one at 1 km still comes before a fighting ship at 10 km (the score is linear in
the range). Two readings: the remaining fire on cripples is point blank (a line passing a cripple a kilometre off, as
Wiesbaden was passed), which `Finish` decides (the user's to set); and the blood's size is the number of cripples, about
115 fall-outs a run, the next step's (the user: "Then we'll look at why ships fall out so much").

**Kept mild (the user, 2026-11-01):** "It seems to me that we are trying to force this issue too much. Just have a mild
rule and we'll do first principles investigation on why the ships got to be crippled in the first place. ... damage
actually is brutal. In many cases a surprised cruiser just vanishes in few minutes if a battleship manages to shoot at
it. The question is that why do they do that here in the first place." The split by what's seen (the Jutland, 2 seeds):
of the 4,438 hits on cripples with a fighting enemy in reach, 2,982 went to ones seen silent; a stronger rule (done
fighting held off by not coming on, rather than by her speed making away, so a cripple limping at 5 kn or stopped counts
as surely done) was tried and stopped before it was measured. BL5e stays as built above.

### How ships come to fall out, from first principles (2026-11-01, the user: "why do they do that here in the first place")

`--fire-table` now also follows every hit on a ship still in the fight and, as she falls out, records who landed most
of them (by type and by displacement against hers), the hits' median range, the minutes from her first hit, and what she
was doing when they began (her order, her admiral's, his task) and whether she was firing then.

| Scenario (seeds) | Fall-outs | The biggest rows |
|---|---|---|
| The Jutland (2) | about 270 | destroyers by destroyers at 0-2 km 64 (13 hits each, 11 min), by capital ships at 0-4 km 40, at 6-15 km 26; cruisers by capital ships at 10-15 km 11. **What they were doing: about 110 were destroyers keeping station in a flotilla on a day attack** (about 55 a run); the record's day melee between the lines cost about four (Nestor, Nomad, V27, V29) |
| The Dogger Bank (4) | about 140 | destroyers by capital ships' main guns at 4-15 km 58, under fire 20-40 min, mostly keeping station in a flotilla gaining bearing for an attack, not firing; the record: Blücher alone |
| The Heligoland Bight (4) | about 100 | destroyers by cruisers at 0-10 km 41, by destroyers at 0-6 km 36; flotillas on patrol, screens, the fight's first rush |

Fall-outs with no shell landed while fighting (13 at the Dogger Bank): not a fault; the damage model counted 10 hits on
them, shells aimed at a ship near them (the guns out 16, gunhouses 6 of 39 mounts): one such hit on a three-gun boat
puts her out for her guns.

**What it says:** damage is as brutal as the record has it (a boat falls out to a dozen 4" hits or two heavy ones); what
isn't the record's is **how often the flotillas are where those hits find them by day**: their attacks press into point
blank of the enemy's flotillas and heavy ships, and they gain bearing for half an hour inside the heavy guns' reach. The
next question is the day attack's own course: when a flotilla starts, how near it goes to what, how long it spends
gaining, where it fires and breaks off, and what it meets on the way, against the record's day attacks (fired from 5-8
km, turned away; the destroyers meeting between the lines at the Jutland's 16:30).

**The flotillas traced (2026-11-01, the user: "Please do so that we have more data. My initial hunch is that the
flotilla keeping station should probably take the enemy into account and move bit closer to their own ships, as in keep
screening the force but shift closer for safety").** `--fire-table` now follows each torpedo attack from its gain to
its end (5 s samples: its phase, its guide's nearest to the target, to an enemy heavy ship and to an enemy boat, the
range at the signal, the hits on its boats by phase, how it ended) and every hit on a torpedo craft by what her unit was
doing, with her distance to her own side's nearest heavy ship and to the enemy's (medians):

| Scenario | Her unit doing | Hits | Own heavy | Enemy heavy | Enemy boat |
|---|---|---|---|---|---|
| Jutland (2) | going home, day | 2,731 | 10.0 km | 4.1 | 1.7 |
| | going home, night | 816 | 21.4 | 9.6 | 3.4 |
| | a day attack, gaining | 467 | 2.9 | 6.7 | 2.1 |
| | a day attack, running in | 463 | 3.3 | 4.8 | 1.0 |
| | screening | 265 | 3.5 | 3.6 | 2.6 |
| | a day attack, firing | 140 | 3.9 | 3.8 | 1.6 |
| Dogger Bank (4) | going home | 917 | 8.9 | 3.4 | 13.2 |
| | in her admiral's Fight (task Attack: guns, no torpedo attack begun) | 311 | 8.5 | 5.9 | 20.5 |
| | screening in a Fight | 121 | 6.7 | 2.8 | - |
| Heligoland (4) | going home | 1,486 | 11.1 | 4.0 | 2.3 |
| | on patrol | 378 | 10.4 | 4.1 | 3.6 |
| | screening in a Fight | 237 | 2.8 | 4.4 | 2.8 |

The Jutland's day attacks (2 seeds): 9, with 100 boats; gaining a median 17 min and running in 6, coming within 3.1 km of
an enemy heavy ship and 3.7 km of an enemy boat; 5 of 9 made the signal, at a median 5.0 km; hits on their boats 201
gaining, 213 running in, 177 firing, 15 going away; 3 beaten off, 1 fired and got away (the rest stopped when their
orders changed). At the Dogger Bank no torpedo attack began at all: the flotillas fought their admirals' Fight with
their guns, closing to their 4" guns' band of the battlecruisers.

**What it says:**

1. **Screens sit between the lines.** A screen keeps `ScreenM` (5 km) off its charge on the bearing toward the nearest
   enemy unless it judges itself no match: with the enemy's line 8-10 km off, that's halfway to his guns, where its hits
   came (3.5 km from its own heavies, 3.6 from his). The record's screens in a gun action kept the disengaged bow or the
   van and rear of their line, out of the line of fire, ready to go out against the enemy's attacks (the Grand Fleet's
   battle orders; to source with the request on fire distribution). The user's hunch.
2. **Destroyers fight heavy ships with their guns.** Without a torpedo attack judged worth it, a flotilla in its
   admiral's Fight closes to its guns' band (0.6 of a 4" gun's reach, about 5 km) on whatever its unit fights,
   battlecruisers included.
3. **Day attacks gain bearing for a quarter of an hour inside the enemy's guns,** 3 km from his heavy ships, and run into
   his flotillas (the enemy boat 1 km off as they run in): the two sides' attacks and screens meet between the lines and
   fight it out at point blank. The record's day attacks fired from 5-8 km and turned away; the Jutland's 16:30 melee
   was the one meeting.
4. **Cripples go home from where they fell out,** about 4 km from the enemy's heavy ships, slow, and take the most hits
   of all.

**Proposed:** (1) a screen's station weighs the enemy's guns: with his heavy ships in reach of it, it shifts to its
charge's disengaged bow and closer in, still between its charge and his torpedo craft, and goes out to counter-attack
as now (the user's hunch); (2) a flotilla's own Fight keeps out of heavy ships' guns: it doesn't close a heavy ship to
its guns' band unless attacking; (3) a day attack gains bearing outside the enemy heavy ships' and screens' close reach
(round them, as `Around` bends an aim's course), fires from its long setting and turns away; (4) the cripples left as
they are for now (BL2's smoke, BL5e's targets), measured again after 1-3.

**The user (2026-11-01):** "The proposals are sound. The captains should aim to protect their own battleline with the
exception of a torpedo attack which is the risky manouver, so their position should be far enough to be safe(ish) but at
minimum on the enemys side of the formation"; "if the lines are far enough the destoyers should be further to defend
against enemy torpedo attack. So they try to stay just on the safe side ready to defend, and ready to attack"; "The
destroyers should care more about enemys secondaries in this case. In case of battleline, as in enemy is shooting big guns
at our own battleships, the destoyers don't need to be too scared of them, unless they actually start shooting at the
destroyers."

**BL5f, the screen's station by the enemy's guns. Built 2026-11-01.** A screen's bearing is toward the nearest enemy in
sight, always (O5's disengaged side for a screen judged no match is gone, at the user's word: `ScreenMarginShare` and the
arc's `Away` with it). Its distance off the screened guide is as ordered (`ScreenM` 5 km) at most and `ScreenMinM` (1 km
[EST]) at least, and between them only so far toward the enemy that none of his heavy ships' guns in sight would take one
of its ships as a target by doctrine's own floors (`HitFloor` of its shells landing and `RoundFloor`'s worth a round: a
battery's rule for letting a target be, `CaptainLayer.WorthOn`'s), never past what can be seen there: his secondaries
always, his chief guns only once their falls have come near one of its ships within `UnderBigGunsS` (120 s [EST]: the
splash ring, `GunnerySystem.ChiefFallsNear`); his boats are the screen's to fight and don't push it in. Judged once every
`SafeRangeEveryS` (10 s). `ScreenTests`: four M-class boats screening four dreadnoughts against four more abeam (the
default preset's floors): about 4 km off at 22, 15 and 12 km (his secondaries out of reach), 1.3 km at 8 km, 1.4 km at 15 km
with his big guns on them; always on his side. `AdmiralOrdersTests`: destroyers against dreadnoughts 14 km abeam now
screen on the enemy's side, one division ahead and one astern.

**Measured** (the panel, `balance/bl5f.json`, against BL5e's): **the Dogger Bank worse, 14.8 → 21.1 ships lost a run**
(the British 7.1 → 11.9, the German hits on them 531 → 1,034); the Jutland 84.2 → 82.2; the rest within their noise. The
fire table at the Dogger Bank (4 seeds), each hit on a boat now also by whom her unit's guide fights and which battery
landed it: the screens hug their battlecruisers (1.2 km from their own, 6.4 km from the enemy's: the lines are about 7 km
apart, the record's 15-18) and take **141 hits from capital ships' chief guns** (and 81 from their secondaries); the
flotillas in their admiral's Fight 128 from capital ships' chief guns, most with no target of their own. **The enemy's
main batteries take the destroyers:** on the engaged bow of her own line a boat is inside his big guns wherever she is,
so BL5f's floor (on his side) holds her there. Tried and taken out: a captain's fight band kept out of a target's guns
when his own can't take her (a destroyer told to fight a battlecruiser alone already holds 13 km: nothing to correct).
Not yet built: proposal 3, the day attack gaining round the enemy's reach.

**For the user:** (a) the disengaged side again once the big guns are on the screen (BL5f's floor only while they
aren't), or (b) the chief batteries' targets by the fleets' practice: a battle line's main guns for his heavy ships, his
boats to the secondaries unless they close to attack (the research request on fire distribution); and why the Dogger
Bank's lines close to 7 km.

**The user (2026-11-01):** "b first, let's see a after that if it's even needed."

**BL5g, the main guns for his heavy ships. Built 2026-11-01.** Fighting to win (doctrine's `Targets` fight, the default
preset's), a chief battery counts a contact's range longer by `FireHeavyShare` (1 [EST]) for each step of her type's rank
under a capital ship's (a cruiser one, a destroyer three, a small craft or merchant four), the steps fading as she comes on
by her own course (none at `CrippleOpenMs` toward him): the secondaries' `Sized` rule turned round, so a boat holding off
is left to the secondaries unless she's four times nearer than his line, and one closing to attack is taken as near as she
is. Sinking tonnage, every enemy alike, as before; the code's neutral doctrine is now tonnage (as before), the preset's
fight. Tried first and taken out: weighing each contact by `Matchup.Threat` (what her guns and torpedoes would put out of
him): a 5" destroyer at 7 km rates 0.36 against a dreadnought at 13 km 0.47 (the sim's 12" at 13 km is hardly more
dangerous over an hour), so threat can't say the fleets' practice, which is a doctrine of roles. `FireOrderTests`: a
super-dreadnought with a torpedo-boat destroyer 7 km off and a dreadnought 13 km: fighting to win, the main guns on the
dreadnought and the secondaries on the boat; the boat coming at her, the main guns on the boat; sinking tonnage, on the
nearest. (A 1940s fleet destroyer over 2,500 t is reported as a cruiser, `Sightings.TypeOf`: one step only.)

**Measured** (the panel, `balance/bl5g.json`; ships lost a run, BL5e / BL5f / BL5g): the Dogger Bank 14.8 / 21.1 / 15.2;
the Jutland 84.2 / 82.2 / 77.0 (the British 26.5, now within twice the record's 14); the Heligoland Bight 14.8 / 14.8 /
18.1 (the Germans 12.0 → 16.1, a scenario with a wide spread, 0-24 a run); Tsushima 4.8 / 4.5 / 6.1; Coronel 2.4 / 2.4 /
1.4; 5 of 14 sides within 2x (4). (a), the disengaged side under the big guns, waits on the user's look.

**Is (a) needed?** The Dogger Bank's fire table (4 seeds), hits on boats in a fighting unit by the battery that landed
them, BL5f → BL5g: capital ships' chief guns 325 → 171 (on the screens 141 → 46), their secondaries 132 → 206,
destroyers' guns 218 → 170, cruisers' chief guns 125 → 81. The screens are now hit mostly by enemy boats (153) and capital
ships' secondaries (115) about 3.4 km from the enemy's heavy ships: counter-attacks chasing his boats in under his line,
not the screen's station. So (a) buys little now; a counter-attack that breaks off at BL5f's safe line (`Matchup.LetBeM`)
would be the next small step, beside proposal 3 (the day attack gaining round the enemy's reach).

**The user (2026-11-01):** "Please go with the proposal 3 and see what comes out of it. At the rate we're adding new AI
stuffs in we're going to look at the much delayed AI threading after this."

**BL5h, a day attack gains its bearing out of the enemy's reach. Built 2026-11-01.** In its gain the flotilla's point lies
no nearer the target than the range out of which the enemy's heavy ships in sight let its boats be (BL5f's
`Matchup.LetBeM`, now `AdmiralLayer.SafeRange` for the screen and the attack: their secondaries, their chief guns too once
those fall about any of its boats), and its course is bent along the tangent of the first such circle round any of them
the straight course would cross (`RoundHeavies`; from inside one, out and round toward the point); judged every
`SafeRangeEveryS` (renamed from the screen's own). The run in and the signal are as before: the risky part, the user's.
`MatchupPlayTests`: sixteen boats on a lone Iron Duke gain at 13.9 km at the nearest (9.3 km before) and fire after about
half an hour (the gain's full 15 minutes; the test's window an hour, and `AdmiralOrdersTests`' cruisers-with-boats one).

**Measured** (the panel, `balance/bl5h.json`, against BL5g's): the Jutland 77.0 → 74.2 ships lost a run, the Dogger Bank
15.2 → 14.1, the Heligoland Bight 18.1 → 18.6, the rest unchanged (no day attacks). The Jutland's day attacks (2 seeds,
`--fire-table`), BL5g → BL5h: 9 → 10 attacks; gaining a median 17 → 45 min, running in 6 → 11; nearest an enemy heavy
ship 3.1 → 5.6 km, an enemy boat 3.7 → 4.4; the signal made by 5 of 9 → 3 of 10, at 5.0 → 8.3 km; beaten off 3 → 5;
hits on their boats gaining 201 → 88, running in 213 → 166, firing 177 → 41. The attacks cost half the hits and get in
less often. Found on the way: a gain ran past `AttackGainMaxMs` (45 min median).

**Fixed 2026-11-01** (the user: "Our admirals ought to not have dementia"): the gain's clock was started over each time a
run fell back to gaining (the target turned away: abaft his beam, a stern chase) and each time the target was chosen
again. An attack now keeps its first gain's time (`Admiral.AttackStartMs`): the gain ends once `AttackGainMaxMs` has gone
since it, and past it a target turned away ends the attack (away) instead of another gain; a target lost and another
chosen within `TorpedoTurnAwayMs` keeps the clock, a longer lull is a new attack; done, stopped or ordered anew, it's
cleared. `MatchupPlayTests`: the pack on an Iron Duke at 21 kn turning straight away whenever it runs in: before, three
runs fell back and it gained 2,710 s (45 min, the Jutland's median); now 900 s, one run, given up.

### BL5: who closes inside 1 km (2026-10-30, after the user's playtest)

`--fire-table` now also counts the ship-minutes each ship spends inside 1 km of an enemy, by what she, her admiral and
her task were doing and who was closing whom:

- **The Heligoland Bight (4 seeds, by day):** the British inside 1 km are nearly all screen flotillas on station under
  their admirals' Screen or Fight (about 110 ship-minutes, mostly closing); the Germans inside 1 km are nearly all
  cripples going home (72 of 75 ship-minutes: too slow, guns out, steering, a list), mostly being closed on. The
  screens run the cripples down: a German boat is a torpedo craft to a screen, crippled or not.
- **The Jutland (2 seeds):** the same cripples by day and night, and the flotillas' torpedo attacks: in their gain, run
  and fire phases inside 1 km, by day as by night. The attack runs in to 0.7 of the torpedoes' fast setting (2.7 km for
  the Great War torpedo's 3.9 km) and often comes closer before it fires; the record's day attacks fired from 5-8 km
  (long settings at a line), the close ones were the night's and the dusk's.

**Decided (the user, 2026-10-30), and to build:**

1. **Screens and cripples:** "Finishing cripples is an important thing. So naturally screen should not prioritize them
   but if one is nearby it should be attacked, unless the cripple strikes their colors." A screen goes for the threats
   first; a cripple near enough it takes as it would any target (striking colours isn't in the sim yet: crew.md's
   "after", noted for it).
2. **Day attacks fire from range:** "Very much this. Ships seem to wait until very close until launching despite
   torpedo ranges being much farther. I bet this is a big reason for the cripples."
3. **Cripples run sneakily:** "cripples should try to be bit more sneaky in how they run away."

**As proposed (before the user's answers):**

1. **A screen counter-attacks a threat, not a cripple:** it goes for a torpedo craft closing its charge within her
   torpedoes' reach; one making away, or gone quiet and slow, isn't one (BL2a's reading of a target, for the screen's
   choice), so it keeps its station.
2. **A day attack fires from its reach, not from point blank:** in daylight a flotilla fires at a line from the range
   its torpedoes' settings give a fair chance (the long setting at a line, as the record's day attacks did), turns away
   if the chance passes rather than pressing in, and doesn't gain bearing inside the enemy's secondary guns' close range;
   at night and at dusk it runs in close, as now.
3. **A cripple runs from all the enemy about her, not the nearest:** away from where the enemy is (their bearings
   weighted by nearness), toward home, under her smoke.

### BL5: the commander's judgement and the ends (diagnosed 2026-10-30, at `ef344c41`)

**Why the Heligoland Germans stay, 24 against 44** (one seed, traced by hand: the commander's `Balance` and the
captains' `FightThink`, traces since removed):

1. **The first hour is the flotillas' attack, not the commander.** Ten German boats go between 07:05 and 07:38. Their
   fire table's tags are `admiral Fight / task Patrol Engaged / attack Gain, Run, Fire, Away`: the patrol flotilla meets
   Arethusa and a few boats, takes up Fight by doctrine, and `AdmiralLayer.AttackStep` begins the attack (its matchup
   over the bar on what the guide sees then). After that the attack is "seen through" (its own comment) until
   `AttackBreakOffShare` (0.4) of its boats are gone from the line, while the rest of the 3rd and 1st Flotillas come up
   out of the mist. The boats are on Course or Station orders under the attack, so the captains' own outgunned rule
   (`FightThink`: under the bar, away) never runs for them: no German `FightThink` at all before 07:24. Once one does run
   (07:24 on), the survivors judge 0.03-0.2 and turn away.
2. **The commander can never decline.** `Balance` judges his whole side (`mine`: every top unit, spread over 60 km of
   the Bight) against all he holds, and `ACanRefuse` compares the slowest of each (`Matchup.Slowest`): Germany 21.5 kn (a
   Gazelle somewhere), Britain 25 kn, every think of the run. So "caught by what he can't beat, he fights": from 08:25,
   with the battlecruisers in sight, his confidence is 0.005 and he goes on fighting until the losses' `BreakOffShare`
   breaks him off at 09:02. A slow ship far from the fight pins the whole side.
3. **His confidence through the first hour sits about the bar** (0.4-0.9 against 8 held in 5 km of mist, the side's 10
   pooled as if together), so even a fixed escape test would decline late or not at all. That one is the fog of war and
   the pooling, and probably fine: the flotillas' own judgement (1) is what matters there.
4. In history the light cruisers caught by Beatty (Cöln, Ariadne, Mainz) were caught: 24 kn against 25-27. A "caught"
   rule judged on the ships actually in touch gives the record's shape there.

**Toward the clock (Heligoland, the same seed):** Britain's commander loses contact, searches, meets a straggler,
loses it, searches again (three times: each fresh contact resets `Searched`), and is still searching at 15:00. Germany
is retiring, but a stopped cripple sits in an encounter with British ships that don't finish her (`Finish` 0.1, she's
quiet), so neither "done" (every ship home or in no encounter) nor "quiet" (no encounter open) can come.

**Proposals (open, for the user):**

1. **A flotilla's attack taken up by doctrine is judged again as it goes:** each think, the guide's matchup on what
   it now sees; under the bar by the commander's margin (`CommanderMarginShare`, as `Balance` does) and not yet in its
   Fire phase, it's given up and the flotilla runs, as when beaten off. An attack ordered (by the player, or the
   commander's `SendIn`) is still seen through: whoever ordered it judged.
2. **The commander's "can he get away" judged on his units in touch,** not the slowest ship of his side: the slowest of
   his top units in contact against the enemy near them. Out-of-touch units are free to go home anyway.
3. **Searching ends:** a search counts once per contact lost, not restarted by every straggler seen and lost again (or
   a pursuit given up once the enemy he holds is home); bugs.md's "a day's search" with it.
4. **A stopped ship doesn't hold the end open:** a ship dead in the water, out of the fight and quiet, counts as done
   for "every side done" and doesn't keep an encounter open for "quiet" (until striking colours, crew.md's "after").

**The panel's ends** (`shipgen balance --fc-runs 0` at `ef344c41`, 8 seeds, the Jutland 4): **55 of 68 plays end by
the clock.** Dover Strait time 8 of 8; Coronel time 7, done 1; Cape Sarych time 8; the prizes' war time 8; Åland time
2, decided 1, quiet 2, done 3; Heligoland time 7, decided 1; Dogger Bank time 8; Tsushima time 3, quiet 3, done 2; the
Jutland time 4. The small scenarios (Dover, Cape Sarych, the prizes) end on time with little lost: the sides parting
and nobody giving up, to be read per scenario once proposals 3 and 4 are in. Losses: 7 of 14 sides within 2x; the
record's loser lost more in 26 of 44; Heligoland 3.6 v 19.0, Dogger 3.5 v 9.8, Tsushima 4.5 v 2.4, the Jutland 26.5 v
59.3 (bl5's 47.0 before `ef344c41`'s torpedo chance: 4 seeds, to watch).

**The user's answers (2026-10-31):**

1. **Yes.** And with it a thought for later: "a real captain does not re-evaluate absolutely everything he's doing
   every few seconds or whatnot. Captains have finite speed that they can think with. This is mostly for performance
   reasons though." The captains and admirals think every second today (`CaptainPeriodMs`, `AdmiralPeriodMs`), every
   judgement with them; a coarser interval for the judgements (the reflexes kept at the second) is "The thinking
   interval" below.
2. **As proposed.**
3. **On what he knows, not where the enemy is:** "The ship can't know when the enemy is at home. That would transfer
   information from the enemy to the searcher via some unknown means. It has to be based on what the captain knows."
   So the search ends by search theory on his own picture (built below): the enemy lost at a datum, at the speed he was
   last held at, could be anywhere in a circle that grows with the time since; the searchers sweep their line's width at
   their speed; once the chance of a find in the next `LostSearchAheadMs` falls under `LostSearchMinChance`, he gives up.
4. **No:** "Let's keep those in, and allow it making the results go off rather than monkeypatching a hack. We'd rather
   do the striking colors/abandon ship next." A stopped ship keeps the end open; striking colours and abandoning ship
   (crew.md's "after") come next, after proposals 1-3, then BL3 and BL4.

**The thinking interval (later, the user's 2026-10-31):** each captain's and admiral's judgements (fight or run, the
target, the matchup, an attack's worth) at a slower rate of their own, staggered so not every ship thinks the same
second, the reflexes (the helm, station keeping, a torpedo track seen, the fire) at the second as now. For the AI's
cost first; measured before it's planned.

Next: proposals 1-3 built, then striking colours and abandoning ship (built 2026-10-31: `striking.md`; the ends didn't move, the clock ends are the commanders'), then BL3 and BL4.

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

6. **Point blank's rate comes down by physics, not a number** (the user, 2026-10-30, on BL1g's finding): pitch,
   vibration at speed and yaw as BL1h ("that's exactly how we want to do it instead of adhoc numbers"); first the AI's
   closing (BL5's part: why light forces get inside 2 km by day).

7. **The user's playtest of the Heligoland Bight (2026-10-30):** "Guns seemed really inaccurate at around 2km, maybe
   too inaccurate, but bunch of germans died like flies because they went full 'YOLO' mode and came to less than 1km to
   attempt I don't know what. That's what kills them. That's probably the root cause and guns should not be reduced to
   allow them to do silly things like that." So: no more lowering of the guns' accuracy (BL1 stands, and the 2 km band
   is watched for being too low); the AI's closing inside 1 km is the next thing (BL5).

8. **Long shots only where they have a chance** (the user, 2026-10-30, after the day attacks): "The distance is good, but
   I'm doubting a captain should just yolo the torps away against fast dodging targets like that. For a line of
   battleships it would make sense." A captain fires (and a flotilla's admiral signals) only above a chance of a hit
   from the run's time against the target's speed and what she presents (her length, her line's).

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
| 2026-10-30 | `872a0173` | `shipgen balance`, the default panel (`balance/bl0.json`) | 4 of 14 sides within 2x; the record's loser lost more in 23 of 44 seeds; capsized 59%, foundered 31%, broke in two 10%; fc-history 7 of 23 (15 HIGH, 1 LOW), orderings 3 of 7 |
| 2026-10-30 | `e6feae4a` | BL1b: laying on the roll (`balance/bl1b.json`) | 5 of 14 sides within 2x; the record's loser lost more in 24 of 44 seeds; Heligoland 2.0 v 17.5 (hits 1,660 → 1,169), Dogger 5.6 v 9.0, Jutland 31.5 v 46.5; fc-history 13 of 23 (10 HIGH), orderings 2 of 7 |
| 2026-10-30 | `0992f1a8` | BL1c: whose splash (`balance/bl1c.json`, scenarios only) | 4 of 14 sides within 2x; the record's loser lost more in 27 of 44 seeds; Heligoland 3.1 v 18.8 (British hit % 23.2), Dogger 4.6 v 9.6, Jutland 44.5 v 46.5 (26-65 a seed): within the panel's noise at 8 seeds; most fire is salvos of three or more, or point blank |
| 2026-10-30 | `478e2a75` | BL2a-b: Finish 0.1, a cripple's smoke (`balance/bl2.json`) | 5 of 14 sides within 2x; the record's loser lost more in 21 of 44; Heligoland 2.1 v 18.5, Dogger 4.1 v 9.3, Jutland 32.0 v 48.0: within the noise. The fire table: the cripples going home still draw the close fire (Heligoland ~3,500 hits on them, 4 seeds): most fire back as they run (not quiet, so they matter by the user's rule), and in 5 km of mist each is often the only enemy in sight |
| 2026-10-30 | `7001702f` | BL1h: pitch, vibration, yaw (`balance/bl1h.json`) | 4 of 14 sides within 2x; the record's loser lost more in 26 of 44; Heligoland 2.1 v 16.4, Dogger 4.0 v 6.9, Jutland 33.8 v 50.0; fc-history 13 of 23 (9 HIGH, 1 LOW: the LRBP case 2.8% under 2.9%), orderings 4 of 7. Heligoland's light guns at small ships, point blank 64 → 58%, at 2-4 km 15.4 → 14.4%: little sea there, so little motion |
| 2026-10-30 | `c87b0270` | BL1i + BL5's three: the air on the sights, day attacks from the long setting, cripples' escape, screens past cripples (`balance/bl5.json`) | 6 of 14 sides within 2x; the record's loser lost more in 24 of 44; Heligoland 4.9 v 18.1 (torpedoes fired 215 British, 48 German a run: the day shots now from range), Dogger 4.0 v 9.0, Tsushima 4.5 v 2.4, Jutland 29.0 v 47.0 |
| 2026-10-30 | `ef344c41` | BL5: torpedoes only with a chance; the panel for BL5's ends (by hand, scenarios only) | 7 of 14 sides within 2x; the record's loser lost more in 26 of 44; Heligoland 3.6 v 19.0, Dogger 3.5 v 9.8, Tsushima 4.5 v 2.4, Jutland 26.5 v 59.3; 55 of 68 plays end by the clock |
| 2026-10-31 | `4c7a8777` | BL5 proposals 1-3: a doctrine attack judged again, the escape on the units in touch, the search ended on what he knows (`balance/bl5e.json`, scenarios only) | 4 of 14 sides within 2x; the record's loser lost more in 23 of 44; Heligoland 1.6 v 8.4 (was 3.6 v 19.0), Dogger 3.1 v 8.8, Tsushima 3.4 v 2.4, Jutland 38.3 v 45.8 (4 seeds, 28-47 v 37-53); **40 of 68 plays end by the clock** (was 55): Cape Sarych done 7 of 8, Tsushima done 6, Åland done 8. Åland is now no fight at all (0 shells): Finland's commander, outmatched, holds the Swedes in sight but past `FightCompareM` (30 km), so nobody's in touch and he can get away; Sweden loses him, searches 55 min and gives up. The rule as decided; a fight wanted there is the scenario's (its aims, doctrine) in BL6, or "in touch" read as "in sight" |
| 2026-10-31 | `3de89a4b` | Åland fixed: touch judged on the enemy groups he holds (in sight or not, within `FightCompareM`), not on sight this second (the haze hid the Swedes from the Finnish guide a moment and he "got away" at 15 kn from 22.5); the chase speed leaves out ships seen stopped; the search's datum moves at the held group's speed; Finland's doctrine bold (Aggression +1, the user: the weaker side must fight) | Åland alone, 8 seeds: Finland 4.2 v Sweden 2.0 (as built 10-25: 4.1 v 2.4), 7 of 8 seeds a fight, ended time 3, done 5. Without the doctrine, the touch fix alone: 2.5 v 0.2, three seeds parting in the haze with both searches given up (search theory in 8 km of haze: a force sweeps one ship's width) |
| 2026-10-31 | `3de89a4b` | the panel after the Åland fix (`balance/bl5c.json`, scenarios only) | 6 of 14 sides within 2x; the record's loser lost more in 27 of 44; Åland 4.3 v 2.0; Dover 0.5 v 1.5 (now in); Heligoland 1.5 v 8.4, Dogger 3.1 v 8.8, Tsushima 3.9 v 2.9 (Russia now in), Jutland 33.5 v 45.5; 40 of 68 plays by the clock (Tsushima none: quiet 3, done 5) |
| 2026-10-31 | `f0f12952` | A search's line is its admiral's call (spread abreast if each piece could stand alone or get away; `AdmiralLayer.SearchLine`); Åland in 6 km of haze (`balance/bl5d.json`, scenarios only) | 7 of 14 sides within 2x; the record's loser lost more in 25 of 44; Åland 4.8 v 2.9 (every seed a fight); Heligoland 1.5 v 9.8 (3-20), Dogger 3.1 v 8.8, Tsushima 3.9 v 2.9, Jutland 26.0 v 48.8 (Britain now in); Dover ends time 8 of 8 (was done 1) |
| 2026-10-31 | `197437df` | Striking colours, abandoning and scuttling (`striking.md` SK1-SK5; `balance/sk.json`, scenarios only) | 6 of 14 sides within 2x; the record's loser lost more in 25 of 44; Heligoland 3.1 v 10.1, Dogger 4.4 v 7.4, Tsushima 3.9 v 3.1, Jutland 31.8 v 49.8; 41 of 68 plays by the clock (was 40): the clock ends are the commanders', not stopped cripples. Given up: 7 struck, 32 scuttled, 281 abandoned; abandoning off (A/B) moves the losses within the noise |
| 2026-10-31 | `07ce9df5` | bugs.md AI fixes: the intercept no longer re-given each think; station keeping by the line of relative motion (`balance/ai.json`, scenarios only) | 6 of 14 sides within 2x; the record's loser lost more in 26 of 44; Heligoland 2.9 v 12.4, Dogger 1.3 v 7.0 (Britain 4.4 before), Tsushima 4.0 v 2.4, Jutland 32.8 v 46.3; 42 of 68 plays by the clock (41). Given up: 7 struck, 28 scuttled, 246 abandoned |
| 2026-10-31 | `c16f981d` | No flip-flopping between enemy groups: a closing unit moved only for a group 1.5x graver, closers kept while not hopeless (`balance/ai-hop.json`, scenarios only) | 6 of 14 sides within 2x; the record's loser lost more in 22 of 44 (26: Dover 3 of 8, Tsushima 4 of 8); Heligoland 2.3 v 13.3, Dogger 1.0 v 5.6 (Britain in), Tsushima 4.0 v 2.8, Jutland 32.5 v 52.3 (46.3; 4 seeds); 43 of 68 plays by the clock (42). Given up: 2 taken, 12 struck, 32 scuttled, 282 abandoned |
| 2026-10-31 | (this commit) | The Fight band optimal (bugs.md: each side's best range within its reach, doctrine's the tie-break; closing counted, attrition in slices, the further band at equal speed; `balance/band.json`, scenarios only) | 3 of 14 sides within 2x (6); the record's loser lost more in 25 of 44; Heligoland 4.4 v 14.8, Dogger 6.9 v 6.4 (Britain 1.0 before: German hits 272 to 720), Tsushima 2.6 v 2.3 (Russia LOW, 4.0 before; Japan's hits 1509 to 1039), Cape Sarych 1.3 v 0.9, Jutland 33.8 v 50.0; 42 of 68 by the clock; the panel 652 s (472) |
| 2026-11-01 | (this commit) | BL1j: where the fire is, in the panel (`balance/bl1j.json`, the baseline before BL1k-BL1m) | 5 of 14 sides within 2x; the record's loser lost more in 23 of 44; Heligoland 4.0 v 15.5, Dogger 7.3 v 6.3, Tsushima 2.5 v 2.1, Jutland 41.0 v 49.5; fc-history 12 of 23 (10 HIGH, 1 LOW), orderings 4 of 7. **The heavy guns' fire against the record's ranges:** Coronel 5.5 km (5-11) in, Cape Sarych 5.9 (6-8) CLOSE, the Heligoland Bight 3.6 (4-8) CLOSE, the Dogger Bank 10.5 (15-18) CLOSE, Tsushima 7.7 (3-7) FAR, the Jutland 12.6 (8-17) in; Dover's light guns 1.1 (0-2) in. At the Jutland the British heavy guns fire at a median 13.3 km and hit at a median 6.3: the 6% of their shells inside 6 km make most of the hits |
| 2026-11-01 | (this commit) | BL1n: the panel after BL1k-BL1m, the roll averaged over the hull and counted once, the own-turn kick by rate of turn (`balance/bl1n.json`) | 6 of 14 sides within 2x (5); the record's loser lost more in 30 of 44 (23); Cape Sarych 0.9 (now in), Heligoland 4.8 v 18.0, Dogger 8.4 v 9.5, Tsushima 3.3 v 2.4, Jutland 33.0 v 44.3; fc-history 10 of 23 (13 HIGH, 0 LOW), orderings 4 of 7. **Read by range:** the Jutland's British heavy guns 3.0 → 3.8% at a median 13.3 → 13.6 km, every band up (6-10 km 4.1 → 6.5%, 10-15 km 1.5 → 2.3%); the Germans' 3.2 → 3.3% at 11.8 → 11.9 km. The Jutland's overall rate fell (British 10.2 → 8.1%) because the light guns fired further out (5.7 → 6.1 km, 8 → 5% of their shells inside 2 km): a rate alone would have read it backwards. The Dogger Bank's heavy fire 2.4 → 4.8% (British) at the same 12.7 → 12.1 km, still CLOSE against the record's 15-18 km; the heavy medians otherwise unmoved (Coronel 5.6 in, Cape Sarych 6.0 in, the Heligoland Bight 3.8 CLOSE, Tsushima 7.5 FAR, the Jutland 12.8 in); Dover's light guns 1.1 → 2.2 km (FAR of 0-2) |
| 2026-11-01 | (this commit) | BL1o-q: a plain computer put out by 5° of turn; the gunnery officer's worth of a salvo (held for a steadier moment, one at a time when worth little, held when worth nothing; his estimate with his spotter's error); the captain's steady course for the guns (`balance/bl1q.json`) | 5 of 14 sides within 2x (6); the record's loser lost more in 29 of 44 (30); Heligoland 1.1 v 12.0, Dogger 6.8 v 10.0, Tsushima 1.8 v 2.0, Jutland 32.5 v 48.8; fc-history 10 of 23, orderings 4 of 7 (unchanged: its cases fire steady). **By range:** the Jutland's British heavy guns 3.8 → 4.0% at a median 13.6 → 13.2 km, their shells beyond 15 km 36 → 29% (at 1.0 → 1.1%); the Germans' 3.3 → 3.2% at 11.9 → 12.3 km, 23% of their shells still beyond 15 km at 0.6%. The economy floor (0.5% a shell, the officer reading about 2% there) lets the long fire through: 1% at 15-17 km is the record's own battle-line fire, so the floor is the playtest's to judge. The heavy medians otherwise as BL1n (the Dogger Bank 10.1 km, still CLOSE against 15-18) |
| 2026-11-01 | (this commit) | BL1r: the officer's floor 1% a shell; a secondary's target by both floors (1% of its shells landing, a round's worth 1.2e-5); fire for effect holds the captain's course paced or not (`balance/bl1r.json`) | 4 of 14 sides within 2x (5); the record's loser lost more in 25 of 44 (29); Dogger 9.0 v 8.1, Jutland 33.0 v 43.5; fc-history 9 of 23 (13 HIGH, 1 LOW: within its noise), orderings 4 of 7. **By range:** no medium gun fires past 15 km anywhere; the Jutland's German medium guns 85% of their shells at 4-10 km, 1% at 10-15 km (the broken first pass: 30% at 0.3%); the British medium 23% at 10-15 km at 1.0% (BL1q 28% at 1.7%), the Dogger Bank's 31% at 1.9%: most likely the light cruisers' main 6" at destroyers (a main battery, under the officer's 1% floor only, his estimate optimistic far out). The heavy guns as BL1q: the Jutland's British 4.0% at 13.4 km (10-15 km 2.8%, 15+ 1.0%), the Germans' 3.9% at 10.7 km |
| 2026-11-01 | (this commit) | BL1s: one worth for every battery at will (`HitFloor`, `RoundFloor`) (`balance/bl1s.json`; `night-history` beside it) | 4 of 14 sides within 2x (4); the record's loser lost more in 23 of 44 (25); Heligoland 3.8 v 14.6 (1.1 v 12.0), Dogger 8.9 v 9.0, Jutland 33.8 v 52.0 (33.0 v 43.5); fc-history 9 of 23 (13 HIGH, 1 LOW), orderings 4 of 7 (unmoved: its batteries are engaged, not at will); the panel 451 s (585: fewer shells in the air). **By range, the Jutland:** the light guns (mostly destroyers' and light cruisers' main batteries) British 19.6k → 16.4k shells at 15.5 → 18.5%, median 5.5 → 4.4 km, 6-10 km 40 → 18% of them, 10-15 km 4 → 0%; German 28.0k → 15.3k at 6.2 → 9.9%, 6-10 km 45 → 28%; the British medium 8.4k → 5.9k at 8.6 → 11.0% (10-15 km 23% of them at 2.3%, was 1.0%); the heavy British 17.0k → 12.9k at 4.0 → 3.5%, median 13.4 → 12.2 km, beyond 15 km 37 → 21% (10-15 km 2.8 → 2.0%: four seeds; to watch), the German 14.8k → 15.1k at 3.9 → 3.2%. `night-history`: point blank as brutal as BL1i's (Black Prince 86% and 76% at 0.6 km, the 4th Flotilla 65% and 59% at 1 km, the Dover Strait 65% and 50%; Southampton 50% and 33% at 2.7 km; the 12th Flotilla's dawn 3.7% and 1.8% at 4.6 km) |
| 2026-11-01 | (this commit) | BL5e: doctrine's `Targets` (fight, tonnage); done fighting read from her turrets seen silent (`Contact.MountsQuiet`) (`balance/bl5e.json`) | 4 of 14 sides within 2x (4); the record's loser lost more in 23 of 44 (23); Heligoland 2.8 v 12.0 (3.8 v 14.6), Dogger 7.1 v 7.6 (8.9 v 9.0), Jutland 38.8 v 45.5 (33.8 v 52.0), Åland 6.4 v 2.8 (6.0 v 4.4), Tsushima 1.9 v 2.9 (2.0 v 1.9); fc-history 8 of 23 (14 HIGH, 1 LOW), orderings 4 of 7 |
| 2026-11-02 | (this commit) | BL1ac: the salvo error from its parts, `salvo_pct` gone (`balance/bl1ac.json`; before it at BL1ab, `balance/bl1ab.json`) | 6 of 14 sides within 2x (4); the record's loser lost more in 26 of 44 (20); Heligoland 3.3 v 15.1 (2.4 v 15.9), Dogger 7.1 v 8.6 (7.4 v 10.1), Jutland 33.0 v 51.0 (27.5 v 51.5), Tsushima 5.5 v 3.5 (3.4 v 4.9); fc-history 5 of 23 (9; 18 HIGH), orderings 3 of 7 (4) |
| 2026-11-02 | (this commit) | BL1ad-ae: a shift keeps the battery's ballistics; the captain's cost of a shift, `FireSwitchShare` gone (`balance/bl1ae.json`; with `CrippleQuietS` 240 beside it, `balance/bl1af-quiet240.json`) | 5 of 14 sides within 2x (6); the record's loser lost more in 26 of 44 (26); Heligoland 2.1 v 15.9 (3.3 v 15.1), Dogger 9.1 v 10.9 (7.1 v 8.6), Jutland 29.8 v 57.8 (33.0 v 51.0); fc-history 5 of 23 (5), orderings 5 of 7 (3). At 240 s: 4 of 14, 23 of 44, Heligoland 2.6 v 12.0, Dogger 8.3 v 9.4, Jutland 28.3 v 56.0, orderings 5 of 7 |
| 2026-11-02 | (this commit) | BL1af: `CrippleQuietS` 90 → 240 s (the user), the panel as `balance/bl1af-quiet240.json` | 4 of 14 sides within 2x (5); the record's loser lost more in 23 of 44 (26); Heligoland 2.6 v 12.0 (2.1 v 15.9), Dogger 8.3 v 9.4 (9.1 v 10.9), Jutland 28.3 v 56.0 (29.8 v 57.8); fc-history 5 of 23 (5), orderings 5 of 7 (5); squadrons' median hold 153 → 350 s |
| 2026-11-02 | (this commit) | fc-ladder-check.md FL1 (the own-turn kick for the table's own reckoning) and FL2 (the turrets train at first sight), the panel as `balance/fl2.json` | 8 of 14 sides within 2x (4); the record's loser lost more in 26 of 44 (23); Heligoland 0.6 v 11.4 (2.6 v 12.0), Dogger 10.0 v 10.1 (8.3 v 9.4), Jutland 27.0 v 59.8 (28.3 v 56.0); fc-history 6 of 23 (5), orderings 4 of 7 (5) |
| 2026-11-02 | (this commit) | fc-ladder-check.md FL3: the eye and hand plot gone (level 0 the hand plot with the eye), the judgement (local control's computer) on the hand plot's error budget, the panel as `balance/fl3.json` | 6 of 14 sides within 2x (8); the record's loser lost more in 24 of 44 (26); Heligoland 1.4 v 14.5 (0.6 v 11.4), Dogger 8.1 v 10.4 (10.0 v 10.1), Jutland 23.8 v 44.5 (27.0 v 59.8); fc-history 6 of 23 (6), orderings 4 of 7 (4) |
| 2026-11-02 | (this commit) | fc-ladder-check.md FL4: the hand plot without a course rate or a range clock, the panel as `balance/fl4.json` | 5 of 14 sides within 2x (6); the record's loser lost more in 23 of 44 (24); Heligoland and Dogger as FL3 (no hand plots), Jutland 22.5 v 45.3 (23.8 v 44.5), Tsushima 4.5 v 6.0 (4.3 v 5.1; hit % 14.8, 13.9 from 13.0, 12.2: her lines near parallel, where a steady range is right); fc-history 6 of 23 (6), Santiago 43.1% (42.8), the Yellow Sea 40.3 (42.5), Tsushima 16.8 (18.3), orderings 4 of 7 (4) |
| 2026-11-02 | (this commit) | fc-ladder-check.md FL5: a gun laid by its own layer fires only while he sees her from his sight; the Dreyer Mk I split; the Dreyer's and C12's first guess the Dumaresq's, the panel as `balance/fl5.json` | 7 of 14 sides within 2x (5); the record's loser lost more in 33 of 44 (23); Heligoland 2.4 v 13.6 (1.4 v 14.5), Dogger 7.0 v 10.6 (8.1 v 10.4), Jutland 23.0 v 47.8 (22.5 v 45.3), Tsushima 5.0 v 2.5 (4.5 v 6.0; the record 7 v 0); fc-history 6 of 23 (6), orderings 3 of 7 (4: practice's fall with the range 2.51, just over 2.5) |
| 2026-11-02 | (this commit) | fc-ladder-check.md FL6: the AFCT family's first guess the Dumaresq's, the panel as `balance/fl6.json` | the scenarios as FL5 (none has an AFCT): 7 of 14, 33 of 44; fc-history 6 of 23 (6), Bismarck's end 52.0% (52.7), Denmark Strait 19.3 (20.7), orderings 3 of 7 (3) |
| 2026-11-02 | (this commit) | fc-ladder-check.md FL7: the stable vertical's own tilt in her turns; the Mk 8's first guess the Dumaresq's, the panel as `balance/fl7.json` | the scenarios as FL6: 7 of 14, 33 of 44; fc-history 6 of 23 (6), the USN 1944 study 68.6% (68.6), a steady target over a salvo chaser 2.41 (2.22, want 3-10), orderings 3 of 7 (3) |
