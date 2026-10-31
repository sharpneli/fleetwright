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

Next: proposals 1-3 built, then striking colours and abandoning ship, then BL3 and BL4.

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
