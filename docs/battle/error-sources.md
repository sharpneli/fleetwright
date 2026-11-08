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
| Casemates awash | built (BL1f), the research's [INFERRED] sill rule; **ES4**: under the seakeeping law with every gun | `SeaAboard.Green` |
| Smoke on the lines (funnels with forcing and downwash, guns, fires, screens) | built (smoke.md Z0-T4) | `SmokeSystem` |
| A layer firing only while he sees her from his sight | built (FL5), costs almost nothing yet; **ES6**: he lays by his own margin, the smoke flickers | `GunnerySystem.LayerMargin` |
| Whose splash (several ships on one target) | built (BL1c) | `GunnerySystem` spots |
| Stress on the layers and rangetakers | built (BL1d) | `Steadiness.Stress` |
| The residual ballistic bias: wind, density, powder temperature, wear, drift | **one lump**, `resid_pct` and `defl_bias_mil` a machine (2.5% the eye to 0.3% digital), one draw a battery | `Fc.BiasR/BiasD`, fire-control.json |
| Wind in the shell's flight | **none**: the tables have density and Coriolis, no wind | `Ballistics.cs` |
| The weather | wind and visibility in every scenario; **no scenario sets a swell** (all on the default 1 m, 12 s; Coronel's 28 kn gale on it); fc-history's cases have their seas | Content/Scenarios |
| Splash columns hiding the target | **none** (the column is drawn, H = 180 × calibre, and gone from the sim) | splashes.md |
| Spray on her own forward instruments and turrets | **none** (BL1f's second half waited for a freeboard factor; `VerticalRow.Freeboard` now exists); **ES4** built | `SeaAboard.Spray` |
| An unstabilised rangefinder in a seaway | **none**: the gyro mounting's gain is only its fixed reading rate (FL7, FL11); **ES5** built | `FireControl.Held` |

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

## ES0-ES1 The baseline and the record's weather (done 2026-11-03)

**ES0:** the duel bench had the weather already (`duel --wind FROM,KN --swell M`, the tests' `swellHs`); each step adds
its own rung to `--errors` as it lands. The baseline is the panel at c54e255e (`balance/es0.json`): fc-history 6 of 23
(17 HIGH), the scenarios 7 of 14 sides within twice the record, the record's loser losing more in 33 of 44 seeds.

**ES1:** every historical scenario at its record's sea (each file's weather line says where from), the launch screen's
conditions taking the swell (from, height by 0.5 m, period by the second) as they take the wind; the tests' scenarios
left as they were. The panel after (`balance/es1.json`, 8 seeds, Jutland 4; the battles take their own courses):

| | before (1 m swell everywhere) | after |
|---|---|---|
| Tsushima (2 m at 9 s) | Japan 2,258 hits at 14.3%, Russia 558 at 10.5%; lost 5.0 v 2.5 | Japan 1,451 at 10.4%, Russia 921 at 13.2%; 5.3 v 4.5 |
| Coronel (4 m at 12 s under a 28 kn gale) | Germany 462 at 11.3%, Britain 214 at 16.2%; lost 1.6 v 1.0 | Germany 986 at 18.5%, Britain 127 at 14.2%; 1.6 v 0.5 |
| the Jutland (0.5 m) | Britain 3,916 at 12.9%, Germany 2,455 at 8.1% | Britain 5,754 at 15.2%, Germany 2,239 at 8.1% |
| fc-history (its own seas, unchanged) | 6 of 23 | 6 of 23 |

Coronel's rise is the battle's course, not the sea's physics: the same pair in a duel at 7 km (armoured cruiser on
Devonshire) hits 11.9% in a calm and 6.6% in the gale's sea; in the scenario the British casemates are awash and their
fire weak, and the Germans close (their light guns fire 15% of their shells at 2-4 km, against 2% before), where hits
come easily. The record's Coronel was fought at 10-11 km closing to 5; the closing is the AI's (BL5), noted.

## ES2 Splash columns on the line of sight (built 2026-11-03)

**Built:** `SplashColumn` (Sim.Combat), one model for the sim and both drawings (the world view's `SplashRenderer` and
the table's marks now read it): peak 180 calibres, half at a 3° fall and full above 15°, shaft 25 calibres wide, up in
√(2H/g), down by that and a second; its shaft dense through its rise and thinning as it falls, half its water gone at
τ = 1.35 (the drawing's e^−2(τ−1)) [EST: research/vfx/shell-splashes.md, "design estimates from scaling and period
photographs"]. Each target's ring of falls (BL1c) keeps where each came down and its column's peak (none for a hit).
`SplashColumn.Hides`: the columns in front of her whose dense shafts stand over the line from an eye to her top each
cover a band of her, widened to her range; she's hidden while the bands cover her end to end as she lies. Asked for each
rangefinder's cut (a hidden one takes none; the computer runs on) and each gun laid by its own layer (he holds his fire).
`--errors masking=K` scales the columns' size (0 off).

**Generous to the gunner** (decision 3): her masthead is enough to cut or lay on, and only the falls aimed at her count.

**Found:** at their own size the columns hide nothing anyone fires by. A 4" short at 4 km falls at 2-3° and stands about
10 m against a destroyer's 31 m mast, and a boat's shorts scatter 10-70 m across the line; heavy shorts on a stern chase
at 14-22 km hide her now and then from a director's rangefinder, a cut lost that the clock covers (4.8% hits either way).
(*Corrected in ES6:* the masking's rung was 0 in play, the
settings file not naming it; the bench figures here parse `--errors` and had it.) Forty times the size, the layers lose
her 4% of the time (b97 on v25 at 4 km: 32.5 → 30.0%, rate 5.83 → 5.80). The
mechanism stands, mild, as asked; what would make it bite is a rule the record would have to give: that the hull, not the
masthead, is what a layer must see (the target's hull lost behind shorts is what the accounts describe).
`SplashColumnTests` (the geometry: side by side end on, never broadside, overs, rising, thinned, a gap, off),
`GunneryTests.Splash_columns_hide_her_from_the_layers_only_when_they_cover_her`.

## ES3 The day's ballistics, the residual bias taken apart (built 2026-11-03)

The user: "it's good that we get rid of another grabbag error term." `resid_pct` and `defl_bias_mil` are gone (retired
keys, as `salvo_pct` in BL1ac: an old design still checks); every computer says instead what it knows of the day's
ballistics, and the fall's error is the difference, at the salvo's own flight (`BallisticResidual`, Sim.Combat):

- **The wind** (`wind_input`: none, deck or ballistic; `wind_err_kn`, its measuring 1σ each way). The world's wind along
  the shell's path, the sea's profile up it (`SmokeAir.Profile`, the smoke's power 0.11 to 300 m: 1.34-1.45 of the 10 m
  wind over a flight), carries it across by the lag rule W(T − X/V₀cos θ₀) and along exactly in the air's frame (a wind
  from behind is a slower muzzle velocity through the air and the air's carry W·T: the lag rule gives two thirds of
  it). `BallisticsTests`: within 1% across and 2% along of a flight integrated through the wind. A machine with no
  input corrects none, the deck's takes its anemometer's wind (30 m [EST]: short of the wind aloft), the ballistic the
  path's; each off by its measuring.
- **The air's density** (`density_err_pct`) and **the muzzle velocity** (`mv_err_pct`), as the machine has them, through
  the table's own flights (its density and speed rows).
- Drift and the Earth's turning: corrected by every machine (the flights have the Earth's turning, no shell drifts).

Each battery draws its measuring errors once (as the bias was, BL1ad: a shift keeps them), so its fall moves with the
line of fire against the wind as the fight turns. The machine's doubt of all this (the opening's and the carried
correction's σb, point blank's check) comes from the same parts at the range it fires at. `--errors wind=K` is the
wind's rung; the density and muzzle velocity stay on `bias`.

**The sizes** [EST], generous to the gunner, each machine's in the catalogue's note: no wind input before the clocks
(density 1.5%, MV 1%), the deck's from the Dumaresq (3 kn, 1.0%, 0.8%) and the Dreyer's day (2 kn, 0.7, 0.6), interwar
(1.5 kn, 0.4, 0.3), the Ford Mk 8 (1 kn, 0.3, 0.25); the ballistic wind from the 1950s analog (2 kn, 0.3, 0.25) and
the digital (1.5 kn, 0.25, 0.1).

**What the flights say** (the 16"/50, `BallisticsTests`): at 20 kyd a 10 kn wind from behind 46 yd steady, 67 yd on the
sea's profile; 1% of muzzle velocity 335 yd; 1% of density 48 yd; at 30 kyd 150, 493 and 99. The muzzle velocity is
the big part: the research's 2 ft/s a °F of the charge, 55 yd at 40 kyd, is 690 yd a 1%, as ours runs. Its wind,
"10 kn of range wind about 200 yd" at 20 kyd for a 15-16" gun (fire-control-research.md §5.1), is three times what the
flights give a 16" shell; it doesn't say for which shell, and a light one (the 5"/54's "1 kn ≈ 125 yd at 25 kyd") feels
the wind far more. So the Dreyer's sum at 20 kyd: the velocity about 1% of the range, the density and the wind's
residual a tenth of that, under the old 1.2%; across, only the wind's residual (a few yards against the old 2 mil, which
lumped what has no mechanism here).

**Found** (QE on QE at 16 km in a 20 kn wind, 24 duels): the wind's rung costs 13.6 → 12.6% of hits and the first hit
salvo 3 → 5 (the spots walk a steady error out; four times the wind costs no more); the velocity and the density
(bias=0 against 1) 14.7 → 13.6%. The panel (`balance/es3.json` against es1): fc-history 6 of 23 again, its long range
cases a little lower (Falklands 8.1 → 5.9%, the Dogger Bank's Germans 3.2 → 2.2%, the LRBP 3.8 → 3.4%), close in as
before; the orderings 3 → 4 of 7. The scenarios within their seeds' spread (7 of 14 sides within twice the record).
*Corrected in ES6:* the shipped settings' `GunneryErrors` didn't name the wind's rung, so in play (the panel, the
scenarios, the game) it was 0: this panel moved by the density and the velocity alone; the benches above parse
`--errors` over every rung at 1 and had it.

**On the way:** the Dreyer-like refit test takes the AFCT's sizes in place of its residual; three tests' thresholds that
sat inside their own noise widened with their reasons (the shift's carried correction 0.5 → 0.7 of a fresh fall, the
pattern now a larger share; the stable vertical's ratio to two binomial sigmas; the columns' run at their size within
3% of masking off); the fall-out smoke test asks that the line's other ship makes none unless she's going home herself
(the enemy's other guns now hit her). Not built: a hand-set wind left stale through a turn (each machine resolves the
true wind it measured along the moving line), gusts over the flight.

## ES4 Spray and green water (built 2026-11-03)

**The law** (the research's §1.2: Ochi; Nicholson and Price), in place of its wetness index and BL1f's casemate rule:
the sea reaches a point on her when her motion against it there passes the point's height over it, at Rice's rate,
√(m₂/m₀)/2π · exp(−z²/2m₀). `RollSea.Relative` gives m₀ and m₂ at her points from the weather's spectrum (the roll's
own, its directions three to a cell): each wave met at its encounter frequency, the sea's height at the point less her
heave (the wave's mean over her waterplane, through a heave resonance at her pitch's period and damping), her pitch at
x (the waterplane's best fit to the slope, as `Pitch`) and her roll at y (as `At`), each with its phase. A long swell
lifts her with it; a short sea runs up her. On the side a wave meets, what of it she doesn't ride stands against her
(a wall's reflection, by her beam-on share), and her lee is as much sheltered [EST: the wall's limit]. A point's sea is
the mean over a patch of her (`SprayPatch`, 0.05 of her length [EST]: her flare's run, a gunhouse's front). Without it,
waves a few metres long met at 28 kn ran the destroyer's after control wet, 130 m from the stem. Her bow wave stands
over the sea at the stem (0.15 of V²/2g [EST], to nothing 0.15 of her length aft).

**What it wets** (`SeaAboard`, Sim.Combat: one implementation for the gunnery and the bench):
- **Green water on a gun:** the sea at its axis, through its ports, takes the gun out for 10 s [EST]. This is BL1f's
  casemates under the one law; a turret's guns too, in a heavy enough sea.
- **Spray or green water on glass:** an instrument (each eye, by its height), or a layer's sight on the gunhouse roof
  while he lays by it, is blinded for 5 s [EST, wiped] and reads or lays nothing. The sea rising to her flare (half her
  freeboard at the stem, less her bow wave [EST]) throws a sheet. The sheet rises at half her speed (the water her bow
  parts, turned up the flare) plus twice the sea's rate as it rises against her (the jet a flare's impact makes is
  faster than the impact: Wagner) [EST]. The wind over her deck carries it aft; it wets a point it's still as high as
  when it gets there. The sea's rate at a crossing is Rayleigh about √m₂, so the share that reach is the chance it rose
  fast enough.
- **When a point is out:** a wetting clears in its own time, so a point is out 1 − exp(−rate·clear) of the time. Each
  window of that length is drawn whether a wetting fell in it: the same at any compression.
- **Caching:** the variances are kept a ship (`SeaMotion`, made as she spawns) and worked out again on the workers when
  the sea, her heading (10°), speed (0.5 m/s) or GM (5%) moves a step.
- **The rung:** `--errors spray=K` scales the rates (0 a dry ship). `shipgen roll ... --wet` shows each gun's and
  glass's share of the time out by heading and speed.

**The sizes, generous to the gunner** (decision 3), against the record (the research's §1.2 and §5):

| Case | The record | Here (share of the time out) |
|---|---|---|
| Devonshire in Coronel's sea (5.3 m), 12 kn, the sea 60° on her bow | main-deck casemates "closed or useless" | weather-side casemates 40-50%, lee 0-10%; the old rule had the weather side out entirely |
| Bismarck as completed, 28 kn into a sea state 5 head sea | Anton's 10.5 m rangefinder removed for "sea water fogging at high speeds" | Anton's glass 3%, 20% at 32 kn; nothing abaft the beam |
| KGV, 28 kn, sea state 5 | PoW's turret rangefinders drenched (Denmark Strait) | 1%, 8% in sea state 7 |
| A battlecruiser, 28 kn, sea state 7 fully developed (Hs 7.9 m) | Renown's turrets out at 28 kn, fighting at 20 (Lofoten) | A turret's glass 40% (27% at 20 kn), its guns 2% |
| A fleet destroyer, 20 kn into sea state 5 | forward mounts "intermittent" (the research's plausible case) | A mount's sight 40%, the director 1-2%; 67% and 3% at 28 kn |

Each case moves the way the record does, the bigger ships much milder than the record: their glass rarely, their guns
almost never. **The bench** (a destroyer on a destroyer at 4 km, 28 kn into a 30 kn wind and a 3 m swell, 24 duels):
director-fired she fires 8% fewer shells (4,014 → 3,685) at the same 47-48% of hits, 36% fewer with the rung at 4.
Laid by eye she fires 40% fewer (2,494 → 1,491) and hits 35 → 33%; at 4, 61 shells. A blinded glass pauses the fire
more than it spoils the aim.

**The panel** (`balance/es4.json` against es3): fc-history 6 of 23 again (its cases' seas are calm to moderate: Rodney
51.5 → 52.3%, Tsushima's 12" 18.4 → 18.1%), the orderings 4 of 7. Coronel, the one scenario in a heavy sea, moves most:
the British weather casemates now fight part of the time where the old rule shut them, their hits 147 → 259 a seed,
and they lose 2.2 ships against 1.0, the record's 2; the Germans 586 hits at 14.6% (622 at 12.7%). Dover's calm is to
the shell the same; the rest within their seeds' spread (6 of 14 sides within twice the record, against 7).
*Corrected in ES6:* the spray's rung was 0 in play (the settings file didn't name it), so this panel had no sea aboard
at all: the old casemate rule gone and nothing in its place. Coronel's British casemates fought all through, which is
the move above, not the law. ES6's panel is the first with ES4 in play.

**Not built, named:**
- Sheer and flare from the designer (plan.md, "Needed from the designer"). The hull's deck is flat, so every stem is
  as low as amidships: the record's wet ships and dry ships (KGV against Iowa) can't differ yet but by freeboard and
  forecastle.
- Green water running aft along her deck onto a turret's face (Lofoten's flooding through the ports): only the sea at
  the guns' own axis counts.
- Her captain easing speed into a head sea to keep her forward guns, as Renown did at Lofoten: the captain's call
  (BL5), not built.

## ES5 Instruments in a seaway (built 2026-11-03)

**The law** (`FireControl.Held`, Sim.Combat). An unstabilised instrument's line of sight moves with her in elevation by
her roll's share across it and her pitch's along it: level = roll·sin β + pitch·cos β, β the target's bearing off her
bow. Her yaw is the trainer's, as in a director. The man follows by hand a lag behind (`InstrumentFollowS`, 0.5 s
[EST: a fast hand, the gunner's end]), so the line wanders off her by about that lag times the motion's rate: a
first-order follower's residual, 1σ, from the roll's and pitch's own rate spreads (BL1a, BL1h). He holds her while
she's within half the field. The field is an apparent 60° over the magnification (`InstrumentFieldDeg`: the Bismarck
trials' 1.5° at ×40 [SRC], stabilisation-tech.md Q4). A gyro-stabilised mounting (the Argo, a stabilised sight) takes
the motion out; an eye has no field to lose her from.

**What it does:** a rangefinder reads at its catalogue rate times the share of the time it holds her, at least 5% (a
reading every few minutes at worst). The roll and pitch are worked out before the readings now, from the same cache.
`--errors seaway=K` scales the motion (0 a still ship); `shipgen roll ... --held` shows each make's hold on a target
abeam by heading and speed.

**The sizes, generous to the gunner:**

| Case | Held (a target abeam) |
|---|---|
| A dreadnought in our sea state 5, any heading | 100% |
| Devonshire and Scharnhorst in Coronel's sea, beam on | 83-97% |
| A dreadnought beam on to a developed sea state 7 (Hs 7.9 m) | 74-83% |
| A V-class destroyer in the same sea, beam on | 46-59% |
| The Bismarck trials' ±1° oscillation at ×40 (`InstrumentHoldTests`) | 71%: "could lose the target" |

**The bench** (a dreadnought pair at 11 km, a 4 m swell and 30 kn wind on her beam, 24 duels): hits 11.6 → 11.4%;
at four times the motion 11.0%, the first hit at salvo 3 (120 s) instead of salvo 2 (84 s). A reading lost slows the
track's settling, but each man's error is drawn once a target (FL), so the readings he still makes are as good.
**FL11's missing half:** the Argo against the Dreyer table in the same sea doesn't separate (11.5 against 12.0%,
calm 11.9 against 12.9). At the mild size her 9 ft instruments hold her 98% of the time, so the gyro has nothing to
take out.

**The panel** (`balance/es5.json` against es4): the same to the shell in every scenario and fc-history (6 of 23, orderings 4 of 7). At this size a hold over about 97% never moves a reading by a whole second of the fire-control grid, and the scenarios' ships seldom roll hard with their target abeam: Coronel with the rung at 0 and at 1 is the same, at 20 it moves (Monmouth 107 hits against the Germans' run of it, Glasgow afire at 4.6°). The mechanism is in place and mild, as decided; it bites in a heavy beam sea or on a small ship. *Corrected in ES6:*
the seaway's rung was 0 in play (the settings file didn't name it); the 0-against-1 Coronel above compared two zeros.
ES6's panel is the first with it in play.

**Open, for the user:** the catalogue's rates still carry Pollen's "about fivefold" as a calm number: the unstabilised
FQ/FT reads 2 a minute and the same glass on the Argo mounting 8, in a calm as in a gale. Under this law the seaway
does that work, and the rate in a calm would be the man's cut rate, the same on either mounting, with the fivefold an
outcome to check in Pollen's trial seas. Taking it apart raises every unstabilised instrument's calm rate (2 → about
8 for the WWI coincidence makes, the stereo's 4 and the O4's 6 to look at beside them) without a source for the
calm rate itself, so it's left as it was. The seaway's cost comes on top of the calm lump, a double count toward the
gunner's loss.

**Not built, named:** the layers' own telescopes holding her (their laying already fires on the roll or follows it:
BL1b); the readings' error widening with the image's motion (only the rate is lost); the cross-level's rotation of the
image (a coincidence instrument cuts through it at a low line of sight).

## ES6 Smoke over the guns (built 2026-11-03)

**The diagnosis first, as planned.** The user's word from play: "the smoke is kinda dense, so one either shoots with
clarity or doesn't shoot at all." Every fire-control margin through smoke, counted over a 3 h Jutland and Coronel
(temporary instrumentation), against the same line without it:

| Jutland, of lines clear without smoke (margin ≥ 4) | lost (< 1) | the cost band (1-4) | no cost (≥ 4) |
|---|---|---|---|
| Instruments (rangefinders, directors) | 63% | 21% | 16% |
| Layers' own sights | 8% | 45% | 47% |

So the field itself isn't binary in contrast. The response is.
- **The mean only:** the field's puffs are the plume's ensemble mean, so a line is steadily open, steadily thinned or
  steadily shut. The drawing's grain (Z5d) breaks it up; the sim had no gaps.
- **Lost readings rarely cost the aim:** a battery fires while any one sensor sees her, and each man's error is drawn
  once a target. Smoke turned into fewer shells, not worse ones.
- **The local layer laid by the director's view:** a gun laid by its own layer took his error from the battery's best
  margin, often a foretop above the smoke, not from his own sight through it. The 1912 trial is exactly that difference:
  Orion's layers in the smoke, Thunderer's director above it.

**Built:**
- **The flicker** (`SmokeModel.Flicker`): a turbulent plume's depth along a line wanders about its mean as its eddies
  pass. The guns' lines take the field's depth times a lognormal draw about 1 (the mean holds), spread
  `SmokeFlickerLn` 0.8. One state lasts `SmokeFlickerS` 4 s [EST both: a path-integrated concentration's fluctuation
  of order its mean near the source, eddies of tens of metres in a few m/s of wind]. It's drawn per eye (or layer's
  sight), pair and window from the world's stream, the same at any compression. Through smoke that shuts her on the
  mean she's glimpsed (τ 1.5 times the shutting depth: some 30% of the windows), and thin smoke loses her at times.
  Firing and readings happen in the glimpses, at the glimpse's contrast.
- **Each local layer lays by his own margin:** a battery laid gun by gun takes the RMS of its firing layers' haze factors
  (their own sights through the smoke, FL5's check now kept as a margin), the battery's best margin where the smoke
  wasn't asked. A director's layer lays by the director's, as before.
- **The rung:** `--errors smoke=K` scales the depth the guns see (0: they see as if there were none; the looks and the
  drawing keep the field).

**Found on the way, a bug in play since ES2:** the settings file merges over the defaults only at the top level, and the
generated reader sets a key a nested object leaves out to zero. The shipped `GunneryErrors` named the seven old rungs,
so masking (ES2), wind (ES3), spray (ES4) and seaway (ES5) were 0 in every panel, scenario and game; the benches parse
`--errors` over all rungs at 1, which is why they showed each effect. Fixed in the file, and
`Settings_file_names_every_key_and_is_strict` now checks every key inside each record-valued setting (a dictionary's
entries stay the file's). ES2-ES5's panel notes above are corrected; this panel is the first with them all in play.

**The check** (fc-history, 24 runs a case, the guns' smoke at 0, 1 and 3):

| Case (the record) | 0 | 1 | 3 |
|---|---|---|---|
| Dogger Bank, British BCs (1.4%) | 10.9% | 10.8% | 9.2% |
| Dogger Bank, German BCs (~2.5%, firing astern into their own smoke) | 3.7% | 2.6% | 3.0%, first hit 472 s |
| Jutland, 5th BS (2.6%) | 6.5% | 6.1% | 4.5% |
| Jutland, German 1st SG (4.0%) | 8.9% | 7.7% | 7.3% |
| Falklands, Invincible (~7%) | 8.2% | 7.4% | 6.8% |
| Cases in their bands | 6 of 23 | 8 of 23 | 9 of 23 |

Graded now, shell by shell, where it was on or off. The flicker's own share (0 against 0.8 at smoke 1) is mixed
within the noise, with one clear sign: the German BCs firing astern get their first hit at 330 s, not 460 s, firing in
glimpses where the mean shut her. **The duels** (a dreadnought pair with her own smoke blown toward the target, Orion
at the 1912 trial's 12 kn, a Lion at 27 kn) meet no smoke on their lines at all. She outruns her funnel smoke, it
rises (Lion's 6 m in the 20 m to her foretop at 28 kn, the field's T1 test's τ 0.4-0.5 aft of the funnels), and a
smokeless salvo's cloud drifts off the line as she steams on. Physical: a single ship's own smoke over her guns wants a
slow ship and a wind across her, and fleets' smoke is the field's main case.

**The panel** (`balance/es6.json` against es5), the first with masking, wind, spray, seaway and smoke all in play:
fc-history 7 of 23 (6), the orderings 4 of 7; the scenarios 6 of 14 sides within twice the record. Moved most:
- **Coronel:** the Germans 586 → 403 hits, 14.6 → 9.9% (the sea aboard their casemates and glass, now in play); they
  lose 1.1 ships to the British 1.4 (the record 0 and 2).
- **Jutland:** the Germans 2,826 → 1,909 hits, 10.4 → 6.7% (firing through the British smoke cloud to leeward), the
  British 3,920 → 4,919.
- **Heligoland:** the British 1,253 → 905 hits.
- **Dogger Bank:** both sides more hits (800 → 1,204, 676 → 875), firing in glimpses where the mean shut her.
- **Tsushima's Japanese 6"** into its band (4.6 → 2.2%); Dover's few hits within their noise.

**The 1912 trial against the record** (fc-history's own pair; record: director 67%, independent 15%): 39.9% against
45.0%, the wrong way. The case's wind (from 060, the target to the east) blows her smoke away from the target, so
nothing reaches Orion's layers. With it from the other beam (bench, 16 duels), her independent fire loses 18-27% of
its shells and some accuracy (43.0 → 37.8-41.9%), the director a little, but never the record's collapse. The wind of
13 November 1912 isn't in the record; the case's geometry is the user's call. In the case's 3 m sea the director does
worse than independent fire without any smoke (35.7 against 43.0%): the seaway's balance between them is wrong way
round in heavy weather, for ES7.

**Not built, named:**
- The looks (contacts) keep the mean; only the guns flicker. A contact is held through glimpses already.
- Her funnel gas drawn down over her decks in a strong wind across (the field's downwash is 2.5-5 m), and the hot gas's
  shimmer over a top behind a funnel (Lion's "completely unworkable"): neither has a source for its size.
- The flicker's spread falling as a plume ages and mixes: one spread for all smoke.

## ES7 Fit and check (built 2026-11-03; the gap accepted for now)

The user: "get these into the bands, but do prefer upper parts of the bands"; then, on the diagnosis below, option 2:
"we'll accept the gap for now. We'll revisit and think about it later... crew fatigue and just general stress of
combat will lower it a bit, but that comes later."

**Isolated first** (the ladder on the Jutland BCs' case, Lion on Derfflinger at 15 km, both zigzagging, 16 km
visibility): with every error off, 19% (the zigzag alone, whole salvos hitting or missing); the pattern alone 11%; every
error on 8.9%. The chain's errors cost little beyond the pattern, and its MPI error (352 m, 2.5% of the range) is
the research's own Dreyer-level reference (3%), whose hit rate (6.6%) is itself some five times the record. That
reference came within twice the record only through a lumped `combat = 1.5` on its errors and a lumped salvo-to-salvo
error, both gone here (BL1ac, `GunneryCombat` 1). The crews hardly move it (9.1% trained, 7.3% green).

**Built, two physical parts:**
- **Dispersion by the flight** (`FireControl.Dispersion` with the flight's sensitivities, `GunnerySystem.Spread`): each
  shell's scatter is its muzzle velocity's and its jump's (`DispersionJumpMil` 0.5 [EST]), each through the flight
  it's fired on. A flat trajectory turns a milliradian into far more range (in vacuum 2 cot 2θ a radian: 1.6% of the
  range a mil at 3.5°, 0.3% at 15°), so close in the pattern is a larger share of the range than the battle ranges it's
  quoted at; the design's `pattern_pct` holds at `DispersionReferenceDeg` (10° [EST]), taken apart there. Across the
  line as before. The officer's worth of a salvo reads the same spread.
- **Each salvo's charges** (`SalvoMvPct` 0.4%, `--errors charges=K`): one salvo's charges share their lot, their warmth
  and the guns' heat, so its mean velocity moves salvo to salvo beyond the round-to-round spread over √n, and no spot
  takes it out: the research's "floor" (fire-control-research.md §0 item 4; the USN measured MPI error, about 1% of
  the range a salvo for WWII optical practice, and between 1920 and 1945 dispersion fell 66% where MPI error fell only
  23%). Sized to that measurement, not to a hit rate: the 1944 study's MPI error 0.57 → 0.89% of the range at 10 kyd,
  0.73 → 0.92% at 20, 1.44 → 1.48% at 30.

**The 1912 trial's wind** moved to the disengaged beam (from 270, the target to the east), blowing her smoke down the
range: the record doesn't give it, and that's the one way "smoke over the guns" can be (the user's leave to pick).
Director 29.3%, independent 34.7% (the record 67 and 15): the smoke now reaches Orion's layers, but independent fire
still leads (see ES6: in a 3 m sea the director does worse even without smoke).

**Where it stands** (`balance/es7.json`; the record taken as each band's geometric middle):

| Case | Model | Record | × |
|---|---|---|---|
| Santiago 1898, US 12" | 42.8% | 3.5% | 12.1 |
| Yellow Sea 1904, Japanese 12" | 34.7% | 4.7% | 7.3 |
| Jutland, British 1st/2nd BCS | 10.2% | 1.4% | 7.3 |
| Dogger Bank, British BCs | 8.8% | 1.4% | 6.3 |
| Jutland, British 3rd BCS | 27.2% | 4.3% | 6.3 |
| Jutland, Grand Fleet BS | 17.5% | 3.6% | 4.9 |
| Bismarck's end, Rodney | 45.8% | 11% | 4.2 |
| Light guns at a fast boat | 3.8% | 1.0% | 3.8 |
| Jutland, German battleships | 10.6% | 3.0% | 3.5 |
| Denmark Strait, Bismarck | 15.8% | 5.4% | 2.9 |
| Jutland, British 5th BS | 7.1% | 2.6% | 2.7 |
| USN practice, 6 kyd | 65.7% | 26% | 2.6 |
| Jutland, German 1st SG | 9.2% | 4.0% | 2.3 |
| USN practice, 10 kyd | 30.0% | 15% | 2.0 |
| USN 1944 study, 10 kyd | 54.8% | 33% | 1.7 |
| Tsushima, Japanese 12" | 13.3% | 8.7% | 1.5 (in) |
| Dogger Bank, German BCs | 3.2% | 2.5% | 1.3 (in) |
| USN 1944 study, 20 kyd | 14.0% | 11% | 1.3 (in) |
| USN 1944 study, 30 kyd | 3.0% | 2.7% | 1.1 (in) |
| Falklands, Invincible | 7.4% | 7.0% | 1.1 (in) |
| Tsushima, Japanese 6" | 1.4% | 2.0% | 0.7 (in) |
| Komandorski, salvo chaser | 0.3% | 0.5% | 0.6 (in) |
| LRBP 1932-33, 30 kyd | 2.6% | 4.4% | 0.6 (LOW) |

Over the cases, the median is 3.7 times the record and the geometric mean 3.5; 7 of 23 in their bands. Practice runs
about twice the record, combat three to seven times, the period's earliest close fights the most. The scenarios: 6 of
14 sides within twice the record's losses, the record's loser losing more in 33 of 44 seeds; their sides hit 9-16% of
their heavy and medium shells (Heligoland's British 32%, Dover's destroyers 46-56% at point blank).

**Why the rest isn't here** (for the revisit): not the chain, which sits at the research's reference; but how the
record's fire was made, which a steady 20-minute duel against stand-in ships lacks. Targets seen through mist in short
windows, each window opening with ranging; the period's ships and powder (Santiago's brown powder smoke, the stand-in a
1906 design); the practice screens the practice records were fired at (the cases fire at a dreadnought); and, the
user's word, crew fatigue and the stress of combat as their own systems. One fault seen on the way: at 30 kyd the
chain's own tracking error is already over the LRBP's measured 313 yd, so its case runs low.

**On the way:** `AimsTests`' raider case runs without damage (the escort's fire took every mount of hers at once, the
light guns too, and she turned away with none: logged in bugs.md); two thresholds widened with their reasons (the
shift's carried correction 0.75 of a fresh fall, the power drive's 0.95 of the pointer's). The AI's matchup taken up after (below).

**The matchup's judgement** (the user, 2026-11-03: "whip up the matchup judgement now"). `Matchup.HitChance` reads the
pattern by its flight (`FireControl.Dispersion` with the guns' table: one implementation for the gunnery and the
matchup, tabulated by range step in `MatchupGun`), and its MPI by level is refit to the chain's own as measured (the AI
expecting what the physics gives: QE on QE at 16 kyd against a zigzag, the salvos' mean fall after 5 min, 24 duels a
level: 647, 350, 271, 246, 197, 168, 162, 128, 132 m, so `MatchupMpiPct` [4.4, 2.4, 1.9, 1.7, 1.35, 1.15, 1.1, 0.9, 0.9]
from the research table's [10.9, 4.9, 3.7, 2.9, 2.3, 2.0, 1.8, 1.3, 0.9]; the salvo's charges are in it, the line's mils
still §12.2's). Against the duel bench, a dreadnought pair at 6, 10 and 15 km: 22.4 / 7.1 / 2.5% before, 31.2 / 10.9 /
4.0% now, the duel 46.6 / 15.9 / 5.0% (within 1.5x, from 2x); Iron Duke on a V25 about half the duel's (from a third).
Doctrine's `RoundFloor` 1.2e-5 → 3e-5, keeping the user's pick (a 6" secondary takes a battle line inside about 8 km:
3.0e-5 there now). The 8" destroyer (O7c) now stands off out of the dreadnought's reach with her gun rather than sending
an attacker in; her test asks for either, the matchup's choice. **The panel** (`balance/es7-matchup.json` against es7):
6 of 14 sides within twice the record again; the record's loser lost more in 26 of 44 seeds (33); the fights end with
fewer lost (the Jutland 24.0 v 51.0 → 15.2 v 41.5, Heligoland 2.8 v 17.8 → 1.1 v 11.9, the record 1 v 4 there; Tsushima
5.2 v 3.4 → 4.9 v 5.1, against the record's 7 v 0); fc-history unchanged (its duels have no AI): commanders expecting
more of their guns from further out judge and break off differently, and the record's loser no longer reliably loses
more. For the revisit with the gap.

**A shell's scatter is its gun's** (2026-11-08, the user: "go ahead"; found checking the dev salvo's spread).
`FireControl.Dispersion` took the USNI ratio at the guns *firing*, so the fewer fired the wider each shell went: one gun
σ = 3.3% / 1.6 / 0.798 · 0.85 ≈ 2.2% of the range, a 4-gun ranging or half salvo 1.4x an 8-gun one, one twin mount in
independent fire 2x (the research's `sigma_D(R, n)` does the same: research/REQUESTS.md). The ratio is now taken at the
quoted pattern's own salvo, `PatternReferenceGuns` 8 [EST], and each shell's σ is its gun's whatever the salvo (the
USN's 1944 expected patterns: 3 guns 1.0% of the range, 9 guns 1.9%). The salvo's `PatternM` (4σ: the bracket's floor,
the straddle's half) follows. The dev's "fire at" now fires the guns too (`FireControl.Dispersion` by its flight, the
mounts laid on the point at once; fighting.md), where it had a flat 0.8% of the range for every gun.
**The panel** (`balance/pattern-n-before.json`, `pattern-n.json`, the two run side by side): fc-history 5 of 23 in
their bands (7), the orderings 3 of 7 (4); most cases hit more, ranging and half salvos no longer widened: Santiago
43.1 → 58.9%, Dogger Bank's British 8.9 → 13.5, Jutland's 3rd BCS 27.0 → 35.9, German battleships 10.8 → 13.7, Denmark
Strait 18.1 → 21.7; full salvos alike (the USN 1944 study 55.5 → 55.4, Rodney 49.7 both). The scenarios: 5 of 14 sides
within twice the record (5); the record's loser lost more in 25 of 44 seeds (30). The gap to the record widens a little
further: for the revisit with the rest.

## Decided (the user, 2026-11-03)

1. **The order** as above.
2. **The record's weather**, every seed; the launch screen's conditions let the player change it, as they do the
   visibility, the wind, the cloud and the start (the sea beside the wind: L5's "left for later").
3. **Estimates where a size isn't sourced, generous to the gunner:** mild effects for now, flagged [EST]; what matters is
   that each mechanism is in place. Each is checked by turning it up strong for a bench run (it must show on its rung),
   then set back mild.
