# The error sources: bringing the gunnery down

Started 2026-11-03 at the user's word, after the fire-control ladder (fc-ladder-check.md) was checked through: "First we
fixed all the bugs in the shooting so it's now super accurate, thus it's time to start bringing it down. With exact
effects and not ad hoc randomness numbers. Naturally we may approximate some effect with randomness but no general
unsourced multipliers at random places." Step letters **ES**. The user's earlier word for it (2026-11-02): a big session
of error sources and harsher weather, spray, smoke over the guns; increases since are fine, they give something to bring
down. Salvo chasing for evasion comes after (balance.md, after BL1q).

## The rule

- **Each source is a mechanism the sim already has the state for**, or gets: the weather, the sea, the shells' falls, the
  smoke field, the instruments' places on her. Its effect comes out of that state through the physics, never as a
  multiplier on an error. No `k_optic`-style table (fire-control-research.md §2: "spray ×1.5-2, smoke ×2-3"): where the
  research gives one, we take its mechanism and its cases, not its number.
- **Randomness is fine where the world is random**: a gust, a wave's crest, which second a splash stands. It draws from
  the physical quantity's own spread (a wave spectrum, a gust spectrum), not a lumped σ.
- **Each gets its own rung** on the error ladder (`GunneryErrors`, BL1w), so it is measured alone, then with every
  error on (isolate-before-tuning).
- **The record is the check, not the setter** (history-is-the-yardstick, derive-not-set): each step names the cases it
  should move and which way, and reports fc-history and the panel before and after. A step that overshoots the record
  is a mechanism to look at, not a number to turn.
- **Close range stays deadly** (balance.md decision 2): point blank's danger space covers every angular error added here.

## What the chain has today (read 2026-11-03)

| Source | State | Where |
|---|---|---|
| The roll on the laying, firing on the roll or continuous aim, cross-level | built (BL1a-b) | `RollSea`, `GunnerySystem` laying |
| Pitch and the helm's yaw on the laying | built (BL1h) | `RollSea.Pitch` |
| Vibration at speed on sights and rangefinders | built (BL1h), the research's [INFERRED] `1 + a(v/v_max)²` | `GunnerySystem.Vibration` |
| A rangefinder's error by base, magnification, the support's stiffness, the air's shimmer, haze on the target's contrast | built (F5) | `FireControl.RangefinderUnitM`, `SightModel.RangingFactor` |
| Casemates awash | built (BL1f), the research's [INFERRED] sill rule | `RollSea.Awash` |
| Smoke on the lines (funnels with forcing and downwash, guns, fires, screens) | built (smoke.md Z0-T4) | `SmokeSystem` |
| A layer firing only while he sees her from his sight | built (FL5), costs almost nothing yet | `GunnerySystem.LayerSees` |
| Whose splash (several ships on one target) | built (BL1c) | `GunnerySystem` spots |
| Stress on the layers and rangetakers | built (BL1d) | `Steadiness.Stress` |
| The residual ballistic bias: wind, density, powder temperature, wear, drift | **one lump**, `resid_pct` and `defl_bias_mil` a machine (2.5% the eye to 0.3% digital), one draw a battery | `Fc.BiasR/BiasD`, fire-control.json |
| Wind in the shell's flight | **none**: the tables have density and Coriolis, no wind | `Ballistics.cs` |
| The weather | wind and visibility in every scenario; **no scenario sets a swell** (all on the default 1 m, 12 s; Coronel's 28 kn gale on it); fc-history's cases have their seas | Content/Scenarios |
| Splash columns hiding the target | **none** (the column is drawn, H = 180 × calibre, and gone from the sim) | splashes.md |
| Spray on her own forward instruments and turrets | **none** (BL1f's second half waited for a freeboard factor; `VerticalRow.Freeboard` now exists) | |
| An unstabilised rangefinder in a seaway | **none**: the gyro mounting's gain is only its fixed reading rate (FL7, FL11) | `Rate` |

## The steps (proposed)

| Step | What | Should move | Sources |
|---|---|---|---|
| **ES0** | **The bench and the baseline.** The duel bench gains the weather (wind, swell) and a rung each for the new sources (`weather`, `masking`, `spray`); fc-history and the panel at HEAD as the baseline, since the ladder moved them. | nothing | |
| **ES1** | **The weather to the record.** Each scenario gets its record's sea beside its wind (swell height, period, direction; the wind sea already follows the wind): Coronel's gale, the Falklands' "placid", Jutland's calm and haze, the Dogger Bank's, Tsushima's high waves, and so on, each tagged [SRC] or [EST]. Harsher weather where the record had it, nothing else changed. | the scenarios fought in a sea (Coronel, Tsushima): fewer hits by BL1's roll and pitch, which already exist | the battles' accounts; ship-motion/heavy-weather-cases.md |
| **ES2** | **Splash columns on the line of sight.** A fall's column stands in the sim for its life (its height by calibre, as splashes.md draws it; its rise and fall from the same source), and a column between an instrument or a layer and the target that stands over her as he sees her hides her: no cut, no lay, no fire from that sight while it stands. Shorts mask her most, several ships' shorts more (the concentration's cost beside BL1c's whose-splash). The spotter still sees the column itself. | the melee's light guns at 2-4 km (a 4" column is some 18 m, a destroyer's sight line 5-8 m), concentration on one target, independent fire against salvos | splashes.md's column; the record of shorts masking targets (to collect) |
| **ES3** | **Wind in the shell's flight, and what each machine knows of it.** The world's wind (a profile up the trajectory, its gusts) moves every shell in flight; each machine corrects by the wind it has: measured on deck as the apparent wind less her own motion, set by hand at intervals or fed, surface only or aloft. Then `resid_pct` and `defl_bias_mil` are taken apart into what's left: the wind the machine doesn't know, the day's density and powder temperature by what it measures, the guns' wear by calibration, drift and the earth's turn by whether it corrects them. Each part its own field on the machine, sourced; the lump gone (as `salvo_pct`, BL1ac). | the first salvo's fall and the walk's job; worse to windward and in a gale; a hand-set wind stale through her turns (the audit list's apparent-wind item) | computing.md (1 kn ≈ 125 yd at 25 kyd, 1% density ≈ 135 yd); fire-control-research.md §5's correction sizes; the LRBP's 313 yd first-salvo error at 27-32 kyd as the check |
| **ES4** | **Spray and green water on her own instruments and guns.** The seakeeping law the research cites (Ochi; Nicholson and Price): water reaches a point on her when her motion relative to the sea there (heave, pitch and roll from the sea spectrum we have, against the wave) passes its height above the water, at a rate the spectrum gives; at speed the bow throws its sheet higher. Each wetting blinds the glass it reaches until it clears; green water over a forward turret stops it laying. BL1f's casemate rule redone under the same law, so one model wets every gun and glass. | forward turret rangefinders at 25-30 kn into a sea (Denmark Strait, Lofoten: Renown slowed to 20 kn), destroyers' forward mounts; nothing in a calm or with the sea astern | ship-motion-gunnery-research.md §1.2 and its cases as the check; the designer's freeboard and sheer |
| **ES5** | **Instruments in a seaway.** An unstabilised rangefinder or telescope moves with her; the man follows the target in it by hand, and a cut or a lay needs her held in the field and steady. From the roll and pitch rates at the instrument (already drawn) against its field of view and a man's following: the share of time he holds her, hence the readings' rate and the time to lay. The gyro mounting (Argo) and a stabilised sight take the motion out. The Bismarck trials' limits (alignment held to 3-4° of roll, gone at 6°; a ±1° oscillation lost the target in a 1.5° field at ×40) as the check. | the Argo's lead in a seaway (FL11's missing half), every instrument in a gale | stabilisation-tech.md (the Bismarck trials); fire-control-research.md §3.5 (Pollen "about fivefold") |
| **ES6** | **Smoke over the guns.** The field has the funnels and the guns, and the layers' check (FL5) asks it, but it costs almost nothing: find why against the record's cases before changing anything (the 1912 Thunderer-Orion trial, "smoke over the guns is the main reason individual aim collapsed"; Lion's spotting top behind the fore funnel "completely unworkable at high speed"; Invincible's turrets "seriously obstructed by smoke" at the Falklands), then fix the mechanism that's missing (the guns' own smoke near the muzzle, her funnel gas drawn down over the decks, the wind over her). | local laying in smoke, Lion's original layout, coal ships at full power | smoke.md's model; the cases above; predreadnought-tech.md (a smokeless 13" round hid a point 4 miles off for 5 s) |
| **ES7** | **Fit and check.** fc-history, the panel, night-history, close-range duels; the record's cases each step named. | fc-history toward its bands, the orderings | |

**Out, named:** fatigue over hours (with `GunneryCombat`'s removal, its own system); own-gun blast shaking a rangefinder
(the research's +50% for 5 s is unsourced); a rangefinder's bias drifting with the sun's heating on one side (Lienau's
"primary cause", no figures); the gyrocompass's error after a turn (FL1: at most 15 m at 15 km); a low target's hull lost
in the troughs; coal ships' rate of fire at full power (stokers from the guns' crews, qualitative). Each one where its
source or system lands.

## Decided (the user, 2026-11-03)

1. **The order** as above.
2. **The record's weather**, every seed; the launch screen's conditions let the player change it, as they do the
   visibility, the wind, the cloud and the start (the sea beside the wind: L5's "left for later").
3. **Estimates where a size isn't sourced, generous to the gunner:** mild effects for now, flagged [EST]; what matters is
   that each mechanism is in place. Each is checked by turning it up strong for a bench run (it must show on its rung),
   then set back mild.
