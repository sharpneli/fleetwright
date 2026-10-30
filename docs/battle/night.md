# Plan: night fighting (a 4.5 deepening)
Status: **drafted 2026-10-29**; **agreed with the user 2026-10-29** (decisions 1-5 at the end); Q0-Q10 done 2026-10-29;
the research arrived 2026-10-29: Q1b, Q0b, Q3b, Q5b added before Q10 (after Q9), done; Q11 done 2026-10-30; Q12a (the check) and Q12b (night doctrine one a period) done 2026-10-30; Q12c (the torpedo officer's judgement), Q12d (the AI's night by the light) and Q12e (whose she is, by the light) done 2026-10-30; the night's hit rates await the user
Updated: 2026-10-30

How ships see, light, shoot and lose each other in the dark. The user asked for it after the crew plan (2026-10-28),
naming searchlights and star shell. It builds on the sighting model (`plan.md` 3.2), fire control's air (`fire-control.md`
F5c), the fires' glow (`fire.md` B2), the smoke field (`smoke.md`), the muzzle flashes (`docs/visuals/muzzle-flashes.md`)
and the commander's night doctrine (`command.md` 5.1d). Step letter **Q** (free in the docs).

## The aim

1. **The dark is a quantity, not a switch.** How far a ship shows at night comes from the light there is: twilight,
   the moon's phase and height, starlight, cloud, and whether she stands against a brighter sky or a moon path. A
   moonless night in the Dover Strait and a full moon over the Baltic play differently, from one model.
2. **Light is something ships make and pay for.** Searchlights, star shell, gun flashes and burning ships light what
   they reach and show where they come from. Illuminating someone shows you to everyone. That the lit ship draws the
   fire is meant to emerge from the physics (the beam is seen from far, the lit target from wherever its light falls),
   not from a targeting rule.
3. **Knowing who's who is the night's hardest problem.** A shape at 1,500 m in the dark may be friend or foe. Holding
   fire to make sure, challenging with a light, and getting it wrong decided Jutland's night more than gunnery did.
4. **The night favours the torpedo, and the AI knows it.** Flotillas attack in the dark from the side away from the
   moon. Heavy ships keep dark, turn away, and light up only when it pays. The matchup judges by night as well as by day
   (`bugs.md`: "the matchup has no night").
5. **Within the game's budgets.** Lights are few (tens) beside ships (hundreds). Nothing is quadratic, nothing
   allocates per frame, and the sim gives the same result at any fine-tier speed. The coarse tier has a cheap version.

## What there is today (checked 2026-10-29)

- **The night is one number.** `SightModel.LightReachM` cuts every sight line to `SightNightM` (5 km) once the sun is
  12° down (`SightNightSunDeg`), ramping geometrically from 500 km at sunset. No moon, no cloud, no starlight. The same
  5 km goes to fire control's margin (F5c), the captains' reach (`CaptainLayer`), the matchup (`MatchupJudge`) and the
  launch screen. `duel --night` gives "no solution" for every preset, as the research says it should without light.
- **The moon exists, but only for the eye.** `Sim.Core/Moon.cs` (position, phase, `Moon.Light`) is read by the
  renderer (the night sea, the flashes' photometry, the smoke's lighting), never by the sim.
- **Silhouettes** against a low sun: `SightLine.Silhouette` within `SightSilhouetteArcDeg` of the sun's bearing. Not
  against the moon or a moonlit sea.
- **Fires show at night** (`fire.md` B2): `SightModel.GlowMargin`, a lamp of 10⁴ cd a MW halfway up her silhouette,
  made out at 2·10⁻⁷ lux (Allard's law) through haze and smoke. This is the one light source the sim has, and the
  pattern the others will follow.
- **Gun flashes are drawn, not seen.** The map's flashes are photometric (`FlashPhotometry`, 10⁶-10⁸ over the night
  sea) and light the sea, the ships and the smoke, but the sim never learns that a ship fired.
- **Searchlights are render clutter** (`Shipgen.Render/Clutter.cs`: `searchlight`, `searchlight_tower`). They're
  placed for looks and carry nothing. `plan.md` lists them under "Needed from the designer for sighting".
- **Three shell kinds:** `ShellKind` is AP, Common and HE. Time fuzes exist (`shell-contents.md` C0, "the fuze on
  time"), so a shell bursting at a set time is already there.
- **Night in the AI:** doctrine's `NightAction` (avoid or accept, for heavy ships) and `NightAttack` (flotillas), both
  keyed on the sun 6° down (`NightSunElevationDeg`; by the sky's light since Q12d, `AiNight`). Torpedo tracks are seen at 2 km by day and 400 m by night
  (`TorpedoSeenNightM`), a hard switch at `TorpedoDayElevationDeg`.
- **Own ships are known exactly.** A side's picture holds only the enemy. Own and allied ships are read from the world,
  so there's no friendly fire and no doubt about who's who.
- **The crew plan** left its searchlight term unbuilt (`crew.md` N3: "lit by a searchlight (green only)", +0.02 a
  salvo, CUF R3), waiting for searchlights.
- **The coarse tier** found night's gap to be the AI's minute cadence in close action, not sighting (`coarse.md` J5),
  and left "a thick, dark night giving little action is fine (searchlights come later)".
- **Scenarios at night:** the Dover Strait 1917 (moonless, 2 against 6), the Jutland's night, Tsushima's, Åland's
  summer night. Their night is today's 5 km.

## What history says (from the research we have)

From `fire-control-research.md` §3 and §12, `fire-control-research/emergency-eyeball.md`,
`fire-control-research/evasion-history.md` and `crews-under-fire.md`. Our research is thin on 1890-1925 (most of it
is WWII), so a request goes in with this plan (below).

- **Night ranges were short.** Jutland's night actions were fought at 700-1,500 yd (torpedo shots and guns alike);
  Tipperary was hit by Westfalen at 1,500-2,000 yd; Southampton fought four cruisers at about 1,500 yd. WWII without
  radar is similar (1,000-4,000 yd), except for Japanese lookouts with big binoculars sighting at 9-16 km in 1942.
  Today's 5 km is long for the era's dark nights.
- **Optics work only on a lit or silhouetted target** (research §3, [INFERRED]). The night's ranging error is ×2-3.
- **Illuminating means being hit.** Every searchlight user in the research's actions drew concentrated fire within
  seconds to minutes (the Jutland cruisers, and in WWII Akatsuki, Hiei, Boise, Atago). Southampton: three men at a
  searchlight killed at once, then "we simply had to have time to reorganize, so we didn't dare show a light".
- **Hits pile up on whatever is visible;** unlit ships in the dark are largely spared.
- **Searchlights and star shell decided the Jutland night.** The German searchlights, star shell and secondaries were
  "highly effective" against the 4th Flotilla (four boats lost). The British had no star shell then [to check].
- **Gun flash and smoke blind the firer** (Tassafaronga), and gun flashes give the firer away (North Cape: Scharnhorst
  aimed at them). Star shell was unreliable: duds, smoke, late arrival.
- **Identification failed as often as aim did.** Ships held fire on ships they couldn't identify, fired on friends, or
  lit recognition lights and were shot.
- **Searchlights' useful range** was about 2,500 yd (WWII USN doctrine); the research's evasion tables use 4 km.
- **Green crews felt being lit** (the Dardanelles trawlers turned back under searchlights and fortress guns; regular
  crews carried on under the same fire).

Cases the research doesn't have yet, all inside the first cut: Port Arthur (8-9 Feb 1904, a torpedo attack on a fleet
at anchor), Tsushima's night (Russian ships that lit their searchlights were found and torpedoed: Navarin, Sissoi
Veliky, Admiral Nakhimov [to check]), the Jutland's night in full (Black Prince lit up and sunk alongside the German
line; the British battleships that saw German ships and didn't fire or report), and the Dover Strait 1917.

## The principles

- **One light model.** Every source (a fire, a gun's flash, a searchlight, a star shell, the moon) is a light with an
  intensity, a place and, for a beam, a direction and spread. Each does two things through the same law: it **shows
  itself** (a point source by Allard's law, as fires do now) and it **lights what it reaches** (illuminance on a
  target, which raises the target's contrast against the night). The fires' `GlowMargin` becomes one case of it.
- **The background is the night's own.** A target's contrast at night is against what's behind her: the night sky, a
  moon path, a burning ship, a star shell beyond her. A ship between you and the light is a silhouette.
- **Interpolate on light, never switch on time of day.** Twilight, the moon and the clouds give an illuminance; the
  reach, the ranging error and the torpedo track's sighting all read that. `TorpedoSeenNightM`'s switch and
  `SightNightM` go (no hard switches).
- **Physics, not guard rules.** No "a lit ship is the priority target" rule. If the record's "illuminating means
  being hit" doesn't come out, the light model or the captains' target choice is wrong, and we measure which.
- **Designs carry the hardware, doctrine carries the use.** Searchlights (how many, how big, where) and star shell in
  the loadout are design facts, from templates by navy and year as everything else is. When to burn them is a
  navy's doctrine for a period (one a period, as damage control's), and the player may overrule.
- **Rough where it doesn't decide.** The searchlight's arc and carbons, star shell's parachute and the challenge's
  codes get rough models with the right effect. Detail goes where outcomes turn: who sees whom first, at what range,
  and who lights up.

## The steps

Each step is built, measured against the yardstick and committed before the next. Q0-Q2 need no new research; Q3 on
want the request's numbers, and go ahead on flagged estimates if it hasn't come back.

### Q0 The yardstick

- **`shipgen night-history`**: the record's night cases as benches on the sim, each with the record's ships, light and
  weather, and what happened: first sighting range, opening range, who lit up first, hits and losses, how long it
  lasted. Start with what we have designs for: the Dover Strait 1917; Jutland night's set pieces (Southampton and the
  2nd LCS against the 4th Scouting Group, the 4th Flotilla against the German battleships, Black Prince); Tsushima's
  night torpedo attacks. The research request fills in the numbers.
- **Today's numbers** on it first (all at 5 km), so each step shows what it moved.
- The scenarios with a night (`dover-strait.json`, the Jutland's night run, Tsushima) as the play checks.

**Q0 done (2026-10-29).** `shipgen night-history [--runs N] [--case TEXT]` (`Shipgen.Cli/NightHistory.cs`): each case a
scenario played by the AI on every side on the fine tier, looked at every second while an encounter is open, so the
ranges at the first sighting, salvo and torpedoes are within a second of their moment. The cases are four new test
scenarios (`night-tsushima`, `night-southampton`, `night-4th-flotilla`, `night-black-prince`, under Tests on the launch
screen, to watch by eye in later steps) and the Dover Strait. Each starts its forces 30-45 min from meeting, so the first
sighting is the model's; the record's ranges and outcomes print beside them, with the light at the place (the sun, the
moon, the sight model's reach). 5 seeds a case, 25 plays in 9 s.

**Today** (5 seeds; ranges as means, the salvos' as a median; record in brackets):

| Case | Light at the meeting | First seen | Salvos at | Shells, hits | Lost |
|---|---|---|---|---|---|
| Tsushima 1905 | sun −5° to −13°, moon down | Japan 14 km at once; Russia never | never (the boats never attack) | none | none (Navarin sunk, 3 more torpedoed, 3 Japanese boats sunk) |
| Southampton | sun −8° to −10°, no moon | 11 km, both | 6.6 km for 2 h | GB 1,232 / 119, DE 3,443 / 146 | GB 0 of 4, DE 1.4 of 5 (a few minutes at 800-1,500 yd; Frauenlob) |
| 4th Flotilla | sun −11° to −12°, no moon | GB 4.3, DE 4.9 km | 3.6 km | GB 179 / 26, DE 484 / 65 | GB 1.4 of 10, DE 0 of 6 (1,000-2,000 yd; 3 boats to gunfire, 1 torpedo hit) |
| Black Prince | sun −12°, no moon | 4.5 km, both | 4.5 km | GB 20 / 3, DE 48 / 8 | GB 0.2 of 1 (sunk in minutes at 1,000-1,500 yd) |
| Dover Strait | sun −27°, no moon | 3.9 km, both | 3.5 km | GB 159 / 20, DE 232 / 24 | GB 1.0 of 2, DE 0.4 of 6 (about 600 yd; 2 boats sunk) |

**What it says:**
- **Every night fight opens three to ten times too far out.** In the dark the sight model's 5 km gives first sightings
  of 4-5 km where the record has 0.5-2 km, and the guns open as soon as anyone sees.
- **Twilight is far too generous.** The ramp from sunset to −12° keeps the reach at 27 km with the sun 8° down, so
  Southampton's few minutes at 1,500 yd become a two-hour gun duel at 6.6 km with 4,700 shells. The northern June night
  never gets darker than −12° at Jutland, so the ramp sets most of that night. This is Q1's first job.
- **Black Prince sees the German line at 4.5 km and turns away**, surviving 4 runs in 5. In the record she closed it,
  seemingly taking the ships for friends, and was lit and sunk at close range. The cautious captain's hold on unknowns
  (Q6) and the short night (Q1) are what could bring her in.
- **Tsushima's destroyers never attack** in 5 runs: eight boats against three old battleships aren't worth it to the
  matchup, by night as by day (the `bugs.md` entry "the matchup has no night", now with this case). Q7's.
- Hit rates are day-like (10-16% at 3-5 km), with no light to aim by: the ranging error has no night term yet (Q2, Q4).

### Q1 The dark

- **The night's light at a place:** illuminance from the sun below the horizon (civil, nautical and astronomical
  twilight as a smooth curve), the moon (`Moon.Light`: phase, height, distance; the sim's first use of it), starlight
  and airglow, under a **cloud cover** (new in `WeatherHere`, 0-1, a scenario's `"weather"`; overcast takes most of the
  moon and stars). One function, `NightLight.At(position, time, weather)`, in Sim.Core beside `Light`.
- **What the eye makes of it:** the threshold contrast rises as the light falls (the eye's contrast threshold against
  background luminance, Blackwell's data), and the binoculars' exit pupil matters in the dark (a 7×50 night glass
  against a 6×30, and a rangefinder's dim image). The eye's kit is a design fact (optics block; the lookouts' glasses
  a placeholder until the designer has them).
- **What's behind her:** the night sky's luminance by bearing (brighter toward the moon and toward the twilight
  glow), and the moon path on the water. A ship within the moon's arc is a silhouette, as the sun's is now. The same
  moon makes a target's sunlit-side equivalent: a ship lit by the moon shows on the observer's side.
- **What shows besides the hull:** the bow wave and the wake (foam brightness by speed: a ship at 25 kn shows her
  bow wave from far, one at 8 kn barely; an estimate from the wake model's foam until the research's numbers), and
  funnel sparks and glow from forcing coal fires [EST, research]. Torpedo tracks at night read the same light
  (phosphorescence by the research).
- `SightNightM`, `SightNightSunDeg`'s ramp and `TorpedoSeenNightM` go; their readers (fire control's margin, the
  captains' reach, the matchup, the launch screen's sight) take the new function.
- **Anchors:** Jutland's night sighting ranges (1-2 km in the dark, more against a lit horizon), the Dover Strait's
  moonless point blank, and the 3.2c day anchors unchanged. Tests: a moonless overcast night against a full moon, the
  moon's bearing (a silhouette toward it), the same at 0.5 s and 5 s looks.

**Q1 done (2026-10-29).** The dark is a quantity now:

- **The light** (`Sim.Core/NightLight.cs`): `NightLight.At(place, time, cloud)` gives the horizon sky's luminance (the
  twilight's curve, the one the display already used, now moved here and read by `FlashPhotometry` too; the moon by
  `Moon.Light`, its scattered share in the sky; the stars' and airglow's 10⁻³ lux; all through the cloud, an overcast
  passing 0.15), the moon's bearing and how strongly its path silhouettes a ship toward it, and the **twilight's arch**:
  its light gathered toward the sun's bearing (e^(1.5 cos Δ), forming as the sun goes 0-6° down, spread all round by
  an overcast), so the sky across from the sun is several times darker than toward it. Cloud is in the weather
  (`WeatherHere.Cloud`, a scenario's `"cloud"`, the command, `-weather=...,cloud`, the launch screen's Cloud row).
- **The eye** (`SightModel.DarkOf`): the threshold rises as the sky's luminance through the eye's glass falls, two
  ways by (1 + √(L_k / L)) over its value at 30 cd/m² (Blackwell's shape: flat by day, de Vries-Rose in the dark):
  the large object's threshold (knee 10⁻⁴ cd/m²) and the side of the patch the eye sums over (knee 0.2: Riccò's
  area grows a hundredfold toward night), so small and distant things fade first and nothing falls off a cliff. A
  ship's own contrast goes from the day's grey 0.35 to 0.6 against the sky in its light alone (after sunset, under an
  overcast), on the sky's luminance. The extinction is the visibility's alone now: the dark is the eye's, not the air's.
- **The glass** (`DesignerPlaceholders.Glass`): a 7×50 lookout's glass (0.6 uncoated, fills a dark-adapted pupil),
  the conning tower's naked eye, a rangefinder's 2.5 mm exit pupil at 0.35 (a dim image: night ranging suffers through
  its margin). The pupil follows Moon and Spencer. Placeholders until the designer gives each post its glass.
- **What shows besides the hull**: her bow wave (`BowWaveMargin`: v²/2g × 0.3 high, 0.15 of her length, both sheets bow
  on, at contrast 1.0 [EST]), which finds and holds her and gives fire control a point of aim but never identifies
  her. Funnel sparks and glow go to Q2 (they're a light). Torpedo tracks (`TrackSeenM`) are a thin streak solved on the
  same threshold: 2 km by day, ~1.1 km with the sun 8° down, ~170 m on a moonless night (was 400 m by a switch);
  phosphorescence waits for the research.
- **Gone:** `SightNightM`, the sunset ramp of `SightNightSunDeg`, `TorpedoSeenNightM` and `TorpedoDayElevationDeg`.
  Fire control's margin, the captains' shadowing range (`SeenToM`), the matchup's and the launch screen's sight
  (`LightReachM`, now of the sky) and the night bench read the new light. A fire's glow is held to a threshold that
  rises with the sky as the square root (so by day it barely counts).
- **The day is as it was**: brighter than 30 cd/m² through the glass the dark adds exactly nothing, so Lion sights
  Derfflinger at 25.0 km and KGV Bismarck at 36.2 km as before, and the reference table's day rows are unchanged
  to the last digit (`research/simulation/sighting_ref.py`, brought to the new law, with four night rows).
- **The Dover Strait scenario**: its file already said "calm, overcast, no moon", so it has `"cloud": 1`; its two
  forces passed 1.8 km apart (set for the old 5 km night) and now pass 300 m apart, so they run into each other.

The made-out ranges by a destroyer's 7×50 lookout, side on (`NightSightTests`): a destroyer 0.4 km overcast, 2.0 km
moonless and clear, 6.4 km with the sun 12° down (the mean sky: less across from the twilight, more toward it), 9.8 km
under a full moon 30° up; Lion 0.8, 3.5, 9.6 and 13.6 km.

**The bench after Q1** (5 seeds; Q0's numbers in brackets):

| Case | First seen | Salvos at | Shells, hits | Lost |
|---|---|---|---|---|
| Tsushima | Japan 14 km (14), Russia never | never | none | none |
| Southampton | GB 5.9 (11), DE 9.1 km (11) | 5.4-5.9 km (6.6) | GB 41 / 3 (1,232 / 119), DE 2,377 / 199 (3,443 / 146) | GB 0.4 of 4 (0), DE 0 (1.4) |
| 4th Flotilla | GB 3.0 (4.3), DE 4.3 km (4.9) | 3.6-3.7 km (3.6) | GB 89 / 15, DE 351 / 45 | GB 1.8 of 10 (1.4), DE 0.2 (0) |
| Black Prince | GB 3.7 km in 1 run of 5 (4.5, all), DE 4.7 km (4.5) | 3.7 km (4.5) | GB 14 / 4, DE 450 / 162 | GB 0.6 of 1 (0.2) |
| Dover Strait | 0.3 km both (3.9) | 0.3 km (3.5) | GB 17 / 16, DE 11 / 10 | GB 0.4 of 2 (1.0), DE 0 (0.4) |

**What it says:**
- **The deep dark is right.** The Dover Strait's black overcast night is fought at point blank (the pass's own 300 m;
  the record's ~600 yd), every shell a hit.
- **The twilight decides who sees first.** At Jutland the German line, looking north into the midnight twilight, sees
  first and further (4.3-4.7 km) than the British looking south into the dark (3.0-3.7 km); Black Prince now rarely
  sees the line that sinks her (lost 0.6 of 1, from 0.2). That's the record's shape.
- **Jutland's ranges are still two or three times the record's.** At 56°N in June the sun never sinks past -11.5°:
  the whole night is nautical twilight, a hundred times a moonless night in its mean. The record's 1,000-2,000 yd
  leans on what isn't modelled yet: the night's mist and cloud (the research request now asks for the cases'
  weather and the twilight sky by bearing), challenges and holding fire on unknowns (Q6), searchlights (Q4).
- **Southampton stays a twilight gun duel** (the sun 8-10° down, 0.1-0.5 lux: by the eye's law ships show at several
  km then). Q0 called this Q1's first job; the physics doesn't give the record's few minutes at 1,500 yd from the
  light alone, so it waits for the weather's research and Q6. The record's numbers there are marked [to check] too.
- **Dover's balance moved**: the file's note (the leaders lose about a ship a run, the boats one in two or three)
  no longer holds (0.4 and 0 now, one pass at point blank, then they lose each other). For Q12's scenario pass.

**Open after Q1:** the display doesn't dim its moon and stars by the cloud yet (a look item, Q10); the low sun's
silhouette is still the old switch (-10° to +6°, within 50° of its bearing: left alone for the day's anchors); the
bow wave's, the glass's, the cloud's and the moon's shares are [EST] until the research; the twilight arch's
strength (1.5) is the main knob if the research says Jutland's dark side was darker.

### Q2 Lights as one model; gun flashes and fires

- **`Lights`**, a short world list (pooled) of sources this second: fires (from `ShipCondition.FireShowMW`, as now),
  gun flashes, and later searchlights and star shell. Each has a place, an intensity (cd), a beam (none, or direction
  and spread) and an owner.
- **A source seen:** `GlowMargin` generalised (Allard's law through haze and smoke, against the night's background
  from Q1). A source seen gives a **light contact**: a bearing and a rough range (a large range sigma, as smoke
  contacts do), into the same picture and Kalman track. Seeing the ship raises it as now.
- **A target lit:** for each beam or omni source, the targets inside its reach (through the encounter grid; lights
  are few, so lights × nearby ships, not observers × targets) get the illuminance on them. Their contrast against the
  night background rises in `SightModel.Margin`, for every observer who can see the lit side, friend or foe.
- **Gun flashes in the sim:** a mount firing at night is a light for a moment, its intensity by charge from the
  flashes' photometry (one model with the renderer: the renderer reads the sim's number). The firer shows as a flash
  contact; a broadside every 30 s shows a ship firing much as the record says. Its own flash dazzles her own eyes for
  a few seconds (the eye's adaptation), so a ship firing rapidly sees less.
- **Flashless or night charges** wait for the ammunition system (`muzzle-flashes.md` already has the "night charge"
  as a later choice); the propellant's flash is in the magazines' data, so the hook is there.
- **Anchors:** a broadside's flash made out at 15-30 km on a dark night [EST, research]; fires as now (`fire.md` B2's
  25 km for a 2 MW fire). Tests: a flash contact's bearing within its sigma, the dazzle's recovery, the fire's numbers
  unchanged.

**Q2 done (2026-10-29).** Lights are one model:

- **One flash model.** The glowing gas of a shot (the cores, Mach discs and secondary fireball, and the blackbody
  table) moved from the renderer's `FlashEmitters` into the sim as `Sim.Combat/FlashGlow.cs`; the screens draw through
  it. `MuzzleFlash.EffectiveCd` integrates a mount's salvo and takes it as a steady light by the Blondel-Rey law (IALA's
  rule for flashing lights, a = 0.2 s), as expected over the secondary flash's chance: a 15" twin 2.9·10⁷ cd, a 13.5"
  twin 2.3·10⁷, a 6" 9.6·10⁵, a destroyer's 3" 8.5·10⁴. `CombatStore.Fired` keeps each ship's latest salvo's flash (the
  mounts fired at one moment summed); a class's flashes are worked out when a ship of it is set up.
- **`Lights`** (`Sim/Lights.cs`, the world's): the sources that light what's near them, gathered at each look. Today
  the fires (as before, lamps of 10⁴ cd a MW halfway up her); Q4's searchlights and Q5's flares join it. A ship asks every
  light (lights × ships): the illuminance on her through the haze and the bearing it comes from; the share an eye sees
  is (1 + cos Δ) / 2 of it (light from beyond her lights nothing the eye sees). Lit, her parts shine back at albedo 0.3,
  their contrast against the sky ρE / πL less her own darkness, the better of that and her dark shape: a dreadnought at
  4 km on a moonless night goes from a margin of 0.7 to some 200 with a 5 MW fire 300 m off her on the eye's side.
- **A light seen** (`SightModel.LightMargin`): Allard's law against the sky through the haze, every source the same law;
  the fires' `GlowMargin` is one case of it (unchanged: a 2 MW fire made out to 24 km). A ship not in sight is looked
  for by her lights (`Sightings.LightLook`): her salvo's flash if she fired within the look (seen once, by its chance),
  and her funnels' glow while she forces coal fires (at the rate a ship is; 10 cd a funnel at her rated power, growing as
  the smoke's forcing does [EST]). A light seen is a **light contact** (`ContactLevel.Light`, `LightInSight`): a bearing
  with the eye's error and a rough range (30%), its course and speed unknown, into the same track; seeing her raises it.
  The map shows it as a placeholder spark with the bearing it was taken on, the card as "a light in the dark"
  (the night's marks wait for Q9). A broadside shows to the horizon on a dark night (29 km for a flash 10 m up, the
  plan's 15-30 km); a destroyer's gun 29 km moonless, 16 km with the sun 6° down.
- **Dazzle**: her own salvo leaves her eyes adapted as to 1.2·10⁻⁹ cd/m² a candela of its light (a 15" twin's as deep
  twilight's sky), fading with τ = 1 s [EST]; the eye adapted to L_a needs Cₜ(L_a)·L_a, so in the dark she's all but blind
  for a few seconds (a destroyer at 1 km on a moonless night: margin 1.9, 0.04 just after a 15" twin fires, 0.5 at 3 s,
  back by 8 s). Her eyes take no fresh sighting and no reading while dazzled, but keep the ship they hold where they last
  saw her (a lookout dazzled for three seconds still knows where she was: without that, the Dover Strait's leaders lost
  their targets after one salvo). Fire control's sensors are dazzled the same way.
- **Flashless and night charges** wait for the ammunition system, as planned.
- **Allocation:** two things a step could allocate, found by the flotilla's allocation test once lights changed its
  play: a side's picture is now sized for every ship at sea (it grows with the fleets, not with the contacts), and the
  matchup judge makes its room for every ship and group when they change, not at a unit's first judgement in a step.

**The bench after Q2** (5 seeds; Q1's in brackets):

| Case | First seen | Salvos at | Shells, hits | Lost |
|---|---|---|---|---|
| Southampton | GB 5.7 (5.9), DE 9.1 km (9.1) | 5.4-5.6 km | GB 55 / 1.4 (41 / 3), DE 2,281 / 172 (2,377 / 199) | GB 0.2 of 4 (0.4), DE 0 |
| 4th Flotilla | GB 3.5 (3.0), DE 4.3 km (4.3) | 3.4-3.6 km | GB 171 / 28 (89 / 15), DE 124 / 16 (351 / 45) | GB 0.6 of 10 (1.8), DE 0 (0.2) |
| Black Prince | GB 3.1 km in 4 runs of 5 (3.7 in 1), DE 4.7 km | 3.3-3.7 km | GB 104 / 30 (14 / 4), DE 277 / 120 (450 / 162) | GB 0.6 of 1 (0.6) |
| Dover Strait | 0.3 km both | 0.3 km | GB 22 / 21 (17 / 16), DE 16 / 12 (11 / 10) | GB 0 of 2 (0.4), DE 0.2 of 6 (0) |

**What it says:**
- **Firing gives you away.** Black Prince, who in Q1 rarely saw the line that sank her, now sees it in four runs of five
  by its flashes once it opens on her, and fires back (104 shells, 30 hits; the record has her sunk without a hit on the
  Germans: she closed taking them for friends and was lit before she fired, Q6 and Q4's).
- **Firing in the dark costs the firer.** Dazzled after each salvo, the German line's fire on the 4th Flotilla falls
  from 351 shells and 45 hits to 124 and 16; the flotilla, firing small guns that dazzle little, does better and loses
  0.6 boats of 10 (1.8). The record's four boats lost lean on searchlights and star shell (Q4, Q5).
- **The Dover Strait** keeps its point-blank pass (22 and 16 shells, nearly every one a hit, torpedoes at 200 m).
- Jutland's twilight ranges and Southampton's long duel are as Q1 left them.

**Open after Q2:** the screen draws each salvo's secondary flash by its own draw while the sim takes the expected light
(the sim would have to keep each salvo's draw for the screen to read it; worth it if a capture shows a mismatch);
a salvo's flash lights its firer's surroundings for an instant but lights no other ship in the sim (too brief beside a
fire or a beam; Q4's beams and Q5's flares are steady); a ship backlit against a fire or a flare beyond her is Q5's
(silhouettes against a lit sky); a light's look asks no smoke of the field; the funnel glow, the dazzle and the light
contact's range error are [EST] until the research; no ranging by a flash's report (the sound's delay).

### Q3 Searchlights in the design

- **A searchlight row** (the design's, compiled into `ShipClass`): place and height, mirror diameter (24", 36", 90 cm,
  110 cm, 150 cm), lamp (carbon arc by period: intensity in cd by diameter and year), its shutter or iris, its control
  (local, from a searchlight control position, later director-controlled), and its crew (in the crew book: "bridge,
  tops, look-outs and searchlights stay manned in every period", CUF). Power from the ship's dynamos through the
  electric network (E4's breakers and cable cuts reach it, near-miss blackouts douse it).
- **The designer places them** (decision 2): shipgen sizes and sites them itself, from templates by navy and year (a
  design block like `gun_tech`), on platforms, the funnels, the masts and the bridge, with their weight and power in
  the budgets. No hand is needed: every design gets them from shipgen, and a design format bump migrates the old ones.
  No placeholder phase. **The clutter draws from the rows** (one physical model): the look never places a searchlight
  the sim doesn't have, and the clutter's own `searchlight` and `searchlight_tower` placement goes.
- **Damage:** a searchlight is a part in the hitbox, so splinters, blast and its own guns' shock put it out (the
  record's mantelets burned and broken lamps).
- The designer's UI row waits for the UI pass, as with the damage plan's data steps.

**Q3 done (2026-10-29).** Searchlights are the design's, sited by shipgen:

- **The block** (`searchlights`, design format 14; `SearchlightTemplates`, `docs/shipgen/searchlight-templates.md`,
  `Content/Templates/searchlights.json`): a capital ship's count and mirror, a torpedo craft's mirror, the arc's crater
  (cd/cm²), the beam's spread and the control (local, remote, director), with an optional `count` of her own. Nine
  templates by navy and period [EST until the research request]: Victorian 24-inch, the pre-dreadnoughts' 75 cm (the
  RN's 24-inch), the Great War's 36-inch with plain arcs (the German navy's 110 cm from a control position), the RN's
  remote control after Jutland, the USN's high-intensity arcs from 1917, interwar, the Second War's 150 cm on the
  directors. The format upgrade gave every design its navy's (look.navy) in its plant's year, so a design that gives
  none takes its plant year's (the look decides nothing).
- **Sizing:** the count is the capital count (180 × 27 m of waterplane) in proportion to her waterplane below that, by
  its 0.25 power above, at least one: a destroyer one or two, a light cruiser three or four, a battleship 6-9. The mirror
  goes from the small one (600 m² and under) to the capital one with the logarithm of the waterplane. One light's
  beam (crater × mirror face × 0.9 clear × 0.85 silvered glass: a 36-inch plain arc 8·10⁷ cd, high-intensity 3·10⁸),
  weight (1.2 t a 36-inch, by its mirror's area, booked as equipment), draw (6.5 kW a 36-inch plain arc) and crew
  (three at a 36-inch) come from its mirror [EST]. No dynamo budget exists to book the draw in: it's on the row.
- **Siting** (`Layout.PlaceSearchlights`, the warship layout; carriers, merchants and planing craft place none): sites
  in groups, each best first, taking turns so the lights spread fore and aft, pairs abreast while two or more are
  wanted: the bridge's highest roof (its wings), platforms on the funnels' sides (the searchlight towers, at 0.55 of the
  funnel's height, 0.7 where that's taken), after control's roof, the masts' tops (ahead of a pole that runs through),
  the other roofs; the deck on the centreline only where nothing else has room. Each keeps clear of what stands at its
  height, of the gun sweeps under it and of the masts and tripod legs. No shortfall warnings across the 372 cases.
- **A part:** a `searchlight` hitbox component (its drum and platform: R = 0.5 d + 0.35, d + 0.7 high) with its mirror
  and what carries it. The crew book puts its men at a `searchlights` station (shipgen's, and the sim's
  `BattleStation.Searchlights`), from the torpedo branch's share of the weapons department.
- **In the sim:** `ShipClass.Searchlights` (`SearchlightRow`: part, carrier, place, height over the sea, mirror, beam
  cd, spread, control, kW) and a `FunctionKind.Searchlight` for each: it burns while it and what carries it stand (a
  funnel shot away takes its platform's lights), its power isn't cut (`SearchlightDown`: E4's local, section and
  cascade outages, its own guns' shock, the near misses' breakers and blackouts reach it, as they reach the directors)
  and someone's at it (`ShipCondition.SearchlightWorks`). A shell through one disables it, splinters harm it as an
  open mount (vitals 0.5, a mirror's fragility 100 J), the gas as electrical gear, and its men stand in the open. Its
  hitbox shows in the silhouette, as every part above water does. Q4 reads all this; nothing burns a light yet.
- **One physical model:** the clutter no longer places searchlights of its own; the top view and the profile draw them
  from the hitboxes (a railed platform with the drum, the glass ahead; a funnel's or a mast's drawn over it).
- **What it cost the ships:** capital ships about 0.1% of standard and 15-30 men, destroyers 0.5-1% (the destroyer
  113.6 to 114.7 m long: the sizer's bistable step) and 3-8 men; goldens 326 design cases, 282 cases' SVGs, 61
  sprite.json (ShipDesign.Version 50).
- **Tests:** `SearchlightTests` (Lion's and V25's lights, manned and high, each with its beam; a hit, its funnel's
  loss and a power cut put one out, and the power's return lights it). Three tests anchored on geometry the hulls'
  small growth moved were restated to what their docs say: the coal shot aims along the whole bunker (most aims burst in
  the decks over it), the slow flood's step is its longest in the second hour (not the one at the last instant), and
  the deep AP burst puts "about one" piece into the upper works (1.0, was 0.8: mostly a gun the destroyer's longer hull
  brought into view). The flotilla's allocation test found a latent growth: `MatchupJudge`'s attackers memo resized
  the first time a flotilla made out an attacker's class mid-fight; it now gets its room with the units' (Q2's fix).

**The bench after Q3** (20 seeds a case on both, so the noise is smaller than Q2's five; Q2's code and designs in
brackets):

| Case | First seen | Shells, hits | Lost |
|---|---|---|---|
| Tsushima | Japan 14 km (14), Russia never | none | none |
| Southampton | GB 5.9 (5.9), DE 9.1 km (9.1) | GB 108 / 9 (64 / 10), DE 2,511 / 201 (2,579 / 309) | GB 0.4 of 4 (0.8), DE 0.1 (0.1) |
| 4th Flotilla | GB 3.5 (3.6), DE 4.3 km (4.3) | GB 161 / 42 (101 / 24), DE 276 / 54 (327 / 89) | GB 1.4 of 10 (1.8), DE 0.1 (0) |
| Black Prince | GB 4.4 km in 14 of 20 (3.9 in 9), DE 4.7 km (4.8) | GB 115 / 31 (118 / 30), DE 242 / 68 (219 / 66) | GB 0.2 of 1 (0.2) |
| Dover Strait | 0.3 km both | GB 9 / 8 (12 / 11), DE 9 / 8 (18 / 14) | GB 0.1 of 2 (0.1), DE 0.1 (0.2) |

**What it says:** nothing burns a light yet, so the night is Q2's: what moved is the ships' small growth and chance.
The lights in the silhouette move nothing past the noise (the Jutland cases run both ways at 20 seeds). The one shift
not explained is Southampton's German hits, 12% to 8% of about 2,500 shells; worth a look when Q4's lights change that
fight anyway.

**Open after Q3:** the templates' numbers wait for the research (sizes by navy and year, counts, the arcs' brightness,
spreads, crews, weights); no dynamo or switchboard (the lights' draw sits on the row until the electric network is a
model: E4 reaches them by distance, as it does the directors); the shock gate (`NonPen.Electrics`: director fire
control) means a pre-director ship's lights never lose power to shock, though they ran on dynamos; carriers and
merchants carry none; the look is a placeholder (Q10); signalling by searchlight is `command.md` 5.1e's.

### Q4 Searchlights in the fight

- **State:** off, ready behind the shutter, exposed; a bearing and elevation; trained by hand at a rate.
- **Two uses:** **search** (sweeping an arc for torpedo craft, the 1890s' and Port Arthur's use) and **illuminate**
  (held on a target so the guns can lay and range). The beam lights its target for everyone, and the lamp shows as a
  source from far. Exposing for seconds and shuttering ("flick on, fire, douse") is a tactic the captain can use.
- **The beam in haze:** its own light scattered back at the operator veils the near beam, so the illuminating ship
  sees her target worse than a consort off to the side does. This is why illumination by one ship for another's guns
  worked [EST from the physics; rough].
- **Fire control:** a lit target has a margin, so the director ranges and spots (F5c's factor through the lit
  contrast). The searchlight's own control (by hand, later slaved to the director) sets how well the beam stays on.
- **Dazzle:** a beam on a ship's bridge or director blinds those eyes for its time [EST, rough].
- **Anchors:** useful range 2-4 km by size and haze; the lit ship hit first in the night-history cases. Tests: the
  beam's illuminance by distance and haze, a consort's margin over the operator's, a lamp seen at 20+ km.

**Q4 done (2026-10-29).** Searchlights burn in the fight, on our own estimates (the research request hasn't come back):

- **The beam** (`Sim.Combat/SearchlightModel.cs`): a Gaussian about its axis, its spread the width at half its peak; held
  on a ship by hand it wanders by its control's pointing error (`SearchlightPointingDeg`: local 0.5°, remote 0.35°,
  director 0.15° [EST]), so its mean light is the beam blurred by that (a 36-inch held locally keeps 74% of its peak over
  a σ of 1°). It does four things, each through the haze by Allard's law as every light does:
  - **It lights what it's on**, for every eye that sees her lit side (Q2's `Lights.On`, beams now in it by their profile):
    a 36-inch puts 11 lux on a ship at 2 km in 25 km visibility, 3 lux in 5 km.
  - **Its lamp shows**: to an eye in the beam the mirror's glare, forward of it the front glass's stray light (10⁻⁵ of its
    peak [EST]); both are a point to aim at, as her bow wave is (they find and hold her, and fire control ranges on
    them, but never say what she is). From beside, the beam itself: its first 2 km scatter a share of its light all
    round (`ColumnCd`), a streak that gives her bearing as a light contact. Lion's 36-inch is seen to the horizon (31-36
    km) in its beam, at 14 km 45° off it, its beam from beside at 19 km (25 km visibility; 7.6, 2.8 and 3.8 km in 2 km).
  - **It veils the view down it** (`BackscatterCdM2`): its light scattered back off the haze (`SearchlightBackscatter`,
    0.3 of an even phase function [EST]) along an eye's line from where the line enters the beam to the target. Beside
    the lamp the line enters a few hundred metres out; from a consort a kilometre off, only near the target. So **the
    operator sees worse than a consort**: Lion lighting V25 at 2.5 km in 10 km visibility, a margin of 1.3 to her own
    lookout (0.6 dark), 179 to a consort 1 km off. In thick haze the veil can cost the operator more than the light gives
    (V25 lighting Lion in 10 km: made out lit at 2.5 km, dark at 2.8).
  - **It glares in the eyes it's on** (Stiles and Holladay's disability glare, 10 E/θ² cd/m², θ at least 1°): the lit
    ship sees the lamp and little round it.
- **Reach** (`SearchlightModel.ReachM`, the captains' and the tables' useful range): where her operator's lookout makes
  out the reference ship lit by her biggest light. Lion's 36-inch on a moonless night: 5.2 km in 25 km visibility, 2.8 in
  10, 1.7 in 5, 0.8 in 2 (the record's 2-4 km).
- **The sight line** takes a veil (`SightLine.VeilCdM2`): her contrast falls by L / (L + L_veil) and the eye adapts to
  the sky and the veil together. Sightings and fire control's sensors ask the world's lights for it per eye
  (`Lights.VeilCdM2`: every beam's backscatter on the line, other ships' glare), and for the target's own lamps
  (`Lights.LampsOf`); a light look adds her lamps and beams to her flash and funnel glow.
- **The arcs:** a light can't send its beam where its own ship stands in the way (`SearchlightRow.Blind`, by the eyes'
  rule from the lamp: Lion's bridge lights are blind over 96°, her funnel platforms' over 186°).
- **Orders** (`Sim/SearchlightSystem.cs`, world system 8; `SearchlightSystem.Order`): a ship's lights off, ready (arcs
  struck behind their shutters, `SearchlightStrikeS` 10 s), held on a contact (`SearchlightsOnOne`, 2, of those that bear;
  trained and exposed `SearchlightExposeS` 4 s after the order or the strike; on her while her navy holds her in sight,
  else on where its picture has her) or searching an arc (the lights that bear split it, each sweeping its share at 1.5°/s,
  a light whose share its own upper works block only where they don't). No tick: a beam's bearing at a look is a
  function of the order, the time and the ships, so it's the same at any speed and in either tier. A light burns only
  while it works (Q3's `SearchlightWorks`). The dev's `-searchlight=ship:on:target[:n] | search:centre:half[:n] | ready |
  off`.
- **The captains' first use, to fire** (`CaptainLayer.Lights.cs`): in the dark, in an encounter, the arcs struck; a light
  on the target of her engaged batteries that their fire control can't make out (`CombatStore.Sees`, the gunnery's word),
  chief battery first, within 0.9 of her lights' reach, held until it's lost, seen going down or past 1.2 of it; a ship
  pressing a torpedo attack keeps dark. Doctrine's when to burn them, searching, keeping dark and dousing when lit are Q7's.
- **The map** draws a beam as a placeholder line from the lamp along its bearing, 4 km (Q10's look later): the player's
  own, and the enemy's that their navy has in sight or by its light.
- **`shipgen searchlight [OWN] [TARGET] [--sky CD_M2] [--consort M]`**: a class's lights and arcs, its biggest's light by
  range and haze, its reach, the target lit as the operator and a consort see her, the lamp seen.
- **Tests:** `SearchlightFightTests` (the beam by range and haze and its reach, the consort over the operator, the lamp
  seen past 20 km in its beam, an order struck, exposed and held, lighting her and glaring her eyes, a sweep within its
  arc, the same twice over).

**The bench after Q4** (20 seeds; the same code with the captains lighting nothing in brackets, which is Q3's table to
the shell: a new world system changes nothing by itself):

| Case | Lights first on (runs) | Shells / hits | Its hits on ships showing a light | Lost |
|---|---|---|---|---|
| Southampton | GB 2.8 km (6/20), DE 2.6 km (6/20) | GB 110 / 9.8 (108 / 9.2), DE 2,595 / 245 (2,511 / 201) | GB 1.1 of 9.8, DE 75 of 245 | GB 0.5 of 4 (0.4), DE 0.1 (0.1) |
| 4th Flotilla | GB 2.8 km (12/20), DE 3.7 km (19/20) | GB 186 / 36 (161 / 42), DE 305 / 45 (276 / 54) | GB 29 of 36, DE 23 of 45 | GB 1.4 of 10 (1.4), DE 0.1 (0.1) |
| Black Prince | GB 2.5 km (10/20), DE 3.1 km (11/20) | GB 184 / 46 (115 / 31), DE 308 / 72 (242 / 68) | GB 10 of 46, DE 9 of 72 | GB 0.3 of 1 (0.2) |
| Dover Strait | both 0.3 km (20/20) | GB 806 / 85 (9 / 8), DE 320 / 46 (9 / 8) | GB 66 of 85, DE 40 of 46 | GB 0.4 of 2 (0.1), DE 1.9 of 6 (0.1) |

**What it says:**
- **Illuminating means being hit**, with no rule saying so: the lamp is a point to aim at, the lit ship sees it at
  once. In the 4th Flotilla's fight 79% of the British hits land on German ships showing a light; at Dover 78% and 87%.
- **Lights carry a fight on.** Dover's point-blank pass becomes a duel out to 4-4.5 km (the salvos' median from 0.3 km),
  hundreds of shells, the Germans losing 1.9 boats of 6 (the record's two) and the leaders 0.4 of 2. In the record the
  boats broke off into the dark: dousing and turning away when lit, and keeping dark, are Q7's.
- **Jutland's twilight moves less**: the lights go on at the edge of their reach (2.5-3.7 km, where the record's went on
  at 1,000-2,000 yd with the challenge, Q6), and the losses don't move (the 4th Flotilla 1.4 boats of 10 against the
  record's four, which also want star shell, Q5).
- Tsushima's boats still never attack (Q7's matchup). The bench cost nothing (21 s for the 100 plays, 23 without lights).

**Open after Q4:** every number is [EST] until the research (pointing, backscatter, the stray light, the column, the
glare's constant, strike and expose times, the beam's spread); a beam passes no smoke of the field (its light, its
lamp in a light look; a hull's look does take the lamp's smoke); beams are level (no elevation) and lit as their
expected profile, not a drawn wander; a ship's lights hold one order at a time (one target); the captains light only to
fire, not to search; ranging on a lamp is the rangefinder's own error at that margin (no special case); the crew's term
for being lit is Q8's; the coarse tier's lights are the fine tier's at its looks (Q11 for a cheaper one).

### Q5 Star shell

- **A fourth shell kind, `Star`**: an illuminant on a parachute under a time fuze (C0). Carried by battery, by the
  ammunition template of its navy and year (a share of the light and medium guns' outfit; a navy without it carries
  none: the British at Jutland [to check]).
- **A flare** is a light in `Lights`: it bursts at the fuze's time, burns for its time (tens of seconds) at its
  intensity, falls at its rate and drifts with the wind. Lights from above, it lights a patch of sea for everyone, and
  the firer doesn't show (beyond her gun's flash).
- **Placing it:** beyond the target, so she shows as a silhouette against the lit sea and sky for the firer; short or
  over by the fuze's error and the shell's dispersion. A battery's illuminating task (one gun or mount fires star
  shell, the rest fire at what it shows; a spread of several to cover the uncertainty), keeping a flare up while the
  target is wanted.
- **Failures:** duds and fuze error by the template's quality; smoke and haze hide the flare.
- **Anchors:** the German use against the 4th Flotilla; WWII's North Cape ("like a chandelier") as a later-cut check.
  Tests: a flare's light on the sea by height and time, the silhouette from the firer's side, its fall and drift at
  any time step.

**Q5 done (2026-10-29).** Star shell, on our own estimates (the research hasn't come back):

- **The data** (design format 15): a fourth shell block kind, `star`, with its candle (`star`: its share of the shell's mass,
  cd a kg, burn time, fall rate) and a `time` fuze (its `scatter` the setting's error as a share, its `blind` the duds).
  `ShellTemplates` has a star shell for each family that had one [EST]: the Germans' 1911 family (the Leuchtgeschoss of
  the 8.8, 10.5 and 15 cm: 3·10⁴ cd a kg of a 15% candle, 25 s at 6 m/s, the fuze 1.5% out, 15% duds; a 15 cm round
  2·10⁵ cd), the Russian 1911, the 1920s' families (RN 1927, US 1921, JP 1925: 5·10⁴ cd a kg, 30 s) and the Second War's
  (8·10⁴, 45 s at 8 m/s). Not the RN 1912 family (the British had none at Jutland) nor the French 1910 or the generic 1912
  standard. **Who carries it goes by the battery's shell family, not the hull's year:** Nassau-1916's secondaries fire
  the DE 1911 family, so Westfalen has star shell at Jutland. The format upgrade gave each light gun (under 160 mm) whose
  shells are such a family 5% star shell [EST], its other shells scaled to make room; 59 designs (the German ships of the
  Great War in the scenarios, the later designs of the other families). Standard displacements moved by 0.005-0.02%.
  `ShellKind.Star`, `Fuze.Time`; the shell type carries its flare (cd, burn, fall). The magazine order never fires it
  (last in the order, skipped by `NextLoad`); the captains' shell choice, the matchup and the strip's shell menu leave it
  out; the card counts it after a star; the designer's family switch takes a battery's star shell to the new family's, or
  drops it with its share to the rest. Goldens: 372 design cases, 39 SVG cases and 37 sprite.json, the hulls of the designs whose magazines changed moving by a hair (ShipDesign.Version 51).
- **The flare** (`Sim/StarShellSystem.cs`, world system 9): a ship's illuminating task gives one mount the job, a working
  one with star rounds that bears, of the lightest battery that has them. It leaves its battery's salvos
  (`CombatStore.Illuminating`; the rest fire at what it shows) and keeps a flare up: the next shell goes so it bursts as
  the last has 3 s to burn, once the mount is loaded and laid. **The aim:** where the picture has her at the burst, then
  600 m beyond along the line of fire and 300 m up, so she stands dark against the sea it lights for the firer. The gun
  is laid so the range table's flight passes through that height there (on its way up close in: a flat gun at 2.5 km
  can't come down through 300 m that soon), the time fuze set to that moment; the laying's 2 mil and the fuze's error on
  it, a share of duds, and no burst past the fuze's longest setting (25 s [EST]), which bounds the reach. A flare bursts,
  burns, falls at its parachute's rate and drifts with the wind; it flies and bursts by the range table alone (no splash,
  no hit). Its light is nobody's: the firer shows only by her gun's flash.
- **What a flare does**, each through the haze: it **lights what's near** for every eye (`Lights.On`, as a fire); it
  **glares** in eyes looking near it (Q4's law; glare that can't reach a hundredth of the sky isn't worked out); and it
  **lights the sea behind a ship** between it and the eye (`Lights.BacklightCdM2`: its light on the sea under the line
  past her, shining back at 0.1 [EST: diffuse and the glitter path]), so the background brightens and she's a silhouette
  against it by its share (`Sightings.Backlit`, in the looks and fire control's sensors alike). Nassau-1916 lighting a
  destroyer 2.5 km off: flares 550-590 m beyond her and 300 m up, a flare up 79 s of 90, 0.5 lux on her, the sea behind
  her 0.13 cd/m² (400 times a moonless sky).
- **The captains** (`CaptainLayer.Lights.cs`, Q4's rule): the target his guns can't see is lit with star shell where she
  carries it and the target is within its reach (`StarShellSystem.ReachM`), else with his searchlights; the star task
  stops with the night, the encounter, or the target.
- **The map** draws a flare as a placeholder disc (Q10 later); `-starshell=ship:target` (0 stops) the dev's.
- **Tests:** `StarShellTests` (a German 1916 ship's star shell, its 15 cm's 2·10⁵ cd; a contact lit from beyond, the mount
  taken from its battery, she lit and the sea behind her lit for the firer; a flare's fall and drift the same at a 1 s and
  a 0.25 s step).

**The bench after Q5** (20 seeds; the same code with star shell's reach set to none in brackets):

| Case | German star shell | Shells / hits | Salvos' median | Lost |
|---|---|---|---|---|
| Southampton | from 8.3 km at 21 min (20/20), 293 ship-min | GB 97 / 21 (105 / 13), DE 2,553 / 235 (2,862 / 320) | GB 5.1 (5.4), DE 5.3 km (5.2) | GB 0.5 of 4 (0.7), DE 0.3 of 5 (0.1) |
| 4th Flotilla | from 6.1 km at 29 min (20/20), 157 ship-min | GB 61 / 10 (209 / 44), DE 692 / 77 (405 / 77) | GB 3.6 (3.6), DE 4.9 km (3.6) | GB 1.2 of 10 (1.6), DE 0.1 (0.1) |
| Black Prince | from 4.7 km at 31 min (20/20), 58 ship-min | GB 51 / 11 (175 / 42), DE 398 / 92 (319 / 87) | GB 5.1 (3.8), DE 3.9 km (3.8) | GB 0.7 of 1 (0.3) |
| Dover Strait | from 0.3 km at 15 min (20/20), 90 ship-min | GB 747 / 83 (941 / 98), DE 282 / 27 (328 / 53) | GB 4.4 (4.7), DE 4.3 km (3.9) | GB 0.3 of 2 (0.5), DE 1.9 of 6 (2.1) |

**What it says:**
- **The Germans use it, the British can't.** Star shell goes up in every German run (the British 1912 family has none),
  and the British fire far less back: the flares show the Germans what they're shooting at without showing the British
  who fired, as at Jutland.
- **Black Prince comes closer to the record:** lost in 0.7 runs of 1 (from 0.3), her own fire down from 42 hits to 11
  (the record: sunk in minutes with no hits on the Germans).
- **It opens the fight further out.** Its reach is the guns' and the fuze's, so the Germans light the 4th Flotilla's
  boats from 6 km and Southampton's squadron from 8 km. Their hit rate halves at the longer range and the boats still
  lose only 1.2 of 10 (the record's four). The record's lights went up at 1,000-2,000 yd once the challenge failed.
  Holding fire until then is Q6's (unknowns, the challenge), and when to burn star shell is doctrine's (Q7).
- The bench takes 33 s (26 s with star shell off: more fighting, the flares' looks a little).

**Open after Q5:** every number is [EST] until the research (who had star shell when, its candle, burn, fall, the fuze's
error, duds and longest setting, the share carried, the sea's reflectance); a flare's light passes no smoke of the field;
the sky round a flare isn't lit (its haze); the star shell's flight draws nothing on the map; a ship lights one contact
at a time with one mount (no spread of several); the captains use it as soon as a target's beyond their guns' sight and
within its reach (Q7's doctrine decides when); the rounds a scenario's older ships carried by date (a refit's) are the
design's; the coarse tier's flares are the fine tier's at its looks (Q11).

### Q6 Friend or foe

- **Today** (checked 2026-10-29): a look runs only for each ship against each side **at war** with her
  (`Sightings`: "for each ship and each side at war with it in its encounter"). Own and allied ships are never looked
  at; the AI and the snapshot read them from the world, exactly and for free. No CPU goes into seeing friends.
- **Seen isn't known.** At night, a ship made out but not identified is an **unknown** contact. An enemy below the
  type's margin is one already; that costs nothing new, and hold fire on unknowns works on it alone.
- **No friendly fire for now** (decision 1). Own and allied ships stay known from the world, as today, at no cost.
  What the fear of it did still plays: a captain who can't tell what he sees may hold his fire, so the cost of caution
  (an enemy let pass, a late first salvo) is real while the friend he feared never appears. A later pass can add own
  ships as contacts (a bounded look: only in the dark, only within the night's reach, outside one's own group), if
  playtesting wants it.
- **The challenge:** a lamp signal (the night's challenge and reply). It shows the challenger (a light in `Lights`).
  A friend answers, and a foe who knows the challenge may answer it too (the Germans at Jutland knew the British
  challenge [to check]). Recognition lights are the same mechanism.
- **Hold fire:** doctrine says whether a ship fires on an unknown contact, challenges first, or holds fire and
  reports. Most of the era's navies held fire. That's what kept Jellicoe's battleships silent while German ships passed.
  Within the doctrine, the captain's temper sets how readily he opens on an unknown (captains are the people with
  personality): a cautious one waits for the type or the challenge's answer, a bold one fires on a shape where no
  friend is expected.

**Q6 done (2026-10-29).** Friend or foe, on our own estimates (the research hasn't come back):

- **A known foe** (`Contact.Foe`): her navy knows her for an enemy once it has made out her type (her colours, her build,
  as it knew her side before), or once she **gives herself away** (`Sightings.GivesAway`), seen in sight or by her lights:
  her salvo fired within the look at one of its ships or a friend's, her searchlights held on one, or a challenge of hers
  (none is its own). Her guns at others, her star shell (nobody's light) and her answer to a challenge say nothing. Below
  that she's an unknown. The map's side for a contact follows it. Own ships stay known from the world (decision 1).
- **The challenge** (`Sim/ChallengeSystem.cs`, world system 10): the bridge's signal lamp held on the contact for 8 s
  (a beam of 3·10⁴ cd, 10° wide [EST]), a light in `Lights` like any other, so it shows the challenger to whoever it
  reaches, the contact first. The reply is awaited 20 s after it. Unanswered, she's a foe, whether she saw it or not.
  Answered (`ChallengeSystem.Answer`, the same lamp held back), she's taken for a friend (`Contact.FriendMs`) for
  10 min [EST], unless she gives herself away first. One challenge a ship at a time; a side doesn't ask one contact twice
  at once. `-challenge=ship:target` is the dev's.
- **Doctrine** (`Unknowns`, after `Wrecks`; the launch screen's "Ships not made out"): **fire** on an unknown (as before,
  the code's default), **challenge** her first (the "default" preset), or **hold** fire until she's made out or gives
  herself away. Within it the captain's temper: a bold one (his aggression a over 0) fires anyway where no friend of his
  but his own squadron is within 5 km × (1 − a) of her (`UnknownFriendM` [EST]; `LineOfFire.FriendNear`), so at a = 1 on
  any shape. **`AnswerChallenge`**, a side's share of the enemy's challenges it can answer with his reply (0 by default):
  the challenged captain gives it once his navy has seen the challenger since it began, drawn once a challenge.
- **The captains** (`CaptainLayer.Fire.cs`, `Opens`): fire at will and torpedoes at will take only a contact they'd open
  on (a foe, an unknown by doctrine and temper, never one taken for a friend); with Challenge, the nearest unknown in the
  widest reach is challenged. An ordered Engage fires regardless (the player's orders beat doctrine). Searchlights and
  star shell follow the guns' targets (Q4, Q5), so nothing is lit before it's known.
- **By day** the challenge costs little: a contact is made out to her type at about the range she's first seen. Dogger
  Bank, Coronel and the Heligoland Bight played 8 seeds each with the old "fire" against the new "challenge" moved within
  the seeds' spread (Germany's losses 11.6 and 12.0 of 26, 1.1 and 1.1 of 5, 20 and 19 of 24).
- **Tests:** `FriendOrFoeTests` (an unanswered challenge makes her a foe and shows the challenger; a foe who answers is
  taken for a friend until she lights the challenger; by day a ship seen firing on one's own is a foe before her type is
  made out; the captains fire at 9 s, challenge and fire at 38 s, hold, or, bold, fire unless a friend is near her).

**The bench after Q6** (20 seeds; Q5's in brackets):

| Case | Challenges | First seen | Shells / hits | Torpedoes, hits | Lost |
|---|---|---|---|---|---|
| Southampton | DE at 9.1 km (20/20), 3.8 a run; GB never | GB 8.9 (5.9), DE 9.1 km (9.1) | GB 165 / 16 (97 / 21), DE 2,945 / 310 (2,553 / 235) | GB 0.4, 0 (1.7, 0.3) | GB 0.5 of 4 (0.5), DE 0.2 (0.3) |
| 4th Flotilla | DE at 6.1 km (20/20), 4.8 a run; GB 3 runs of 20 | GB 5.7 (3.4), DE 4.3 km (4.3) | GB 72 / 18 (61 / 10), DE 699 / 110 (692 / 77) | GB 3.5, 0.8 (1.4, 0.2) | GB 2.3 of 10 (1.2), DE 0.1 (0.1) |
| Black Prince | DE at 4.7 km (20/20), 1.0 a run; GB 1 run | GB 4.5 in 18 of 20 (4.3 in 12), DE 4.7 km | GB 58 / 21 (51 / 11), DE 372 / 81 (398 / 92) | none | GB 0.7 of 1 (0.7), DE 0.1 (0) |
| Dover Strait | DE at 0.6 km (11/20); GB never (the Germans made out at once) | 0.5 and 0.3 km | GB 760 / 89 (747 / 83), DE 241 / 30 (282 / 27) | GB 8.7, 1.3 (8.4, 0.9) | GB 0.3 of 2 (0.3), DE 2.1 of 6 (1.9) |

**What it says:**
- **Challenging gives you away.** The Germans challenge first in every Jutland case, and their lamp is seen far: the
  British pick them up at 8.9 km at Southampton (5.9 before) and 5.7 km against the 4th Flotilla (3.4), and know them for
  foes at once. The British, who challenge only what they see first, hardly ever get to.
- **The 4th Flotilla comes closer to the record.** Seeing the line earlier, the boats fire 3.5 torpedoes a run (1.4) for
  0.8 hits (the record's one, Rostock), and lose 2.3 boats of 10 (1.2; the record's four).
- **The challenge is made where the shape is first seen**, so its range is the night's sight: 4.7-9 km in Jutland's
  twilight, where the record's challenges came at 1,000-2,000 yd. That's Q1's open twilight (the weather's research),
  and when a ship challenges at all, or keeps dark and closes, is doctrine's (Q7).
- **Black Prince** is as Q5 left her (lost 0.7 of 1): the Germans challenge her at 4.7 km and she, seeing it, knows them
  for foes and turns away. The record's closing on ships she took for friends wants her not to see their challenge (or
  the Germans not to make it) until much closer, Q7's.
- The bench took 26 s, as before.

**Open after Q6:** every number is [EST] until the research (the lamp, the signal and reply times, the trust, the
friend radius); `AnswerChallenge` is 0 everywhere (the Germans at Jutland may have known the British challenge [to check]);
captains challenge at any range they see a shape (Q7: within a doctrine's range, or keep dark and shadow); the map
draws no challenge lamp and doesn't mark an unknown apart from a ship not made out (Q9, Q10); recognition lights
(shown all round, by doctrine) aren't modelled; the admirals and the matchup still count unknowns as enemies (no friendly fire,
decision 1); own ships as contacts (a bounded look in the dark) wait for playtesting to want them.

### Q7 Captains, admirals and doctrine at night

- **Doctrine rows** (one a period, as damage control's): searchlights (never; to search for torpedo craft; to fire;
  only when fired upon), star shell (to find, to fire), darkened ship, hold fire on unknowns, challenge, and the
  existing `NightAction` and `NightAttack`.
- **Captains:** keep dark; douse and turn away when lit; fire at a light; turn away from a torpedo attack in the dark
  (TorpedoWatch reads Q1's light). A flotilla's approach from the dark side, with the moon behind its target.
- **Admirals:** night formations (close order, each ship following the next ahead's stern by her wake) and losing touch
  in the dark: a ship that can't see her next ahead falls out of formation and must find it again (one look a ship,
  at her next ahead only: a station-keeping check, not a sighting of friends). Collisions in
  close order wait for 4.11, which this makes more urgent.
- **The commander:** the matchup at night (`bugs.md`'s entry: the defence's guns reach only the night's sight, and the
  boats are seen late), so flotillas are sent in on battle lines by night when worth it.
- The night's change of task stays keyed on the doctrine's sun elevation (the AI's judgment, not a physical switch).

**Built in four slices** (the user, 2026-10-29: tuning passes once every piece is in, so each slice is measured, not
tuned): **Q7a** lights by doctrine and when lit, **Q7b** the flotilla's dark side, **Q7c** the matchup at night (the
commander), **Q7d** the admirals at night (close order, losing touch).

**Q7a done (2026-10-29).** Lights by doctrine, and what a ship does when lit:

- **Doctrine rows** (after `AnswerChallenge`; the launch screen's rows too): **`Searchlights`** never / when fired on
  (once she's under fire) / to fire (Q4's: on what the guns can't see) / search (and, with nothing to light, sweeping
  `SearchHalfDeg` 45° either side of the nearest contact held; trained round again only once it's drawn half the arc off);
  **`StarShell`** never / to fire (Q5's) / to find (and, with nothing to fire at, the nearest contact in its reach held but
  not in sight or not made out); **`ChallengeM`**, the range within which an unknown is challenged (farther off he keeps
  dark; Q6's open item); **`WhenLit`** fight on / turn away. The code's defaults are what Q4-Q6 did; the "default" preset
  takes to fire, to fire, 2,500 m and turn away [EST]. Presets one a period wait for the tuning pass.
- **Lit** (`CaptainLayer.TurnFromLight`): a ship counts herself lit once the others' beams and the flares on her
  (`Lights.LitBy`: not the fires) are `LitShare` 10 times the night sky's light [EST]. Turning away, she puts her lights out,
  turns stern to the light and holds that course until `LitTurnAwayS` 120 s after she was last lit [EST], her guns still
  firing; then her order's course again. Not while combing a track, pressing a torpedo attack or under the conn. The comb
  and this are one held course now (`Captain.HeldUntilMs`, `HeldFor`: Combing or Lit); the card says "lit: lights out,
  turned away".
- **Not built:** a darkened ship (navigation lights aren't modelled: every ship is darkened); firing at a gun's flash alone
  (fire control lays on lamps and bow waves, not a flash: a later fire-control item).
- **Tests:** `NightDoctrineTests` (lit, turn away to 279° with her lights out or fight on; searchlights never, to fire,
  search; challenged within 2.5 km, not within 1 km).

**The bench after Q7a** (20 seeds; Q6's in brackets):

| Case | First seen | First salvo, salvos' median | Shells / hits | Torpedoes, hits | Lost |
|---|---|---|---|---|---|
| Southampton | GB 5.8 (8.9), DE 9.1 km | DE 5.1 (8.6), 3.3 km (5.4) | GB 73 / 22 (165 / 16), DE 2,588 / 775 (2,945 / 310) | GB 3.5, 0.1 (0.4, 0); DE 6.8, 0.8 | GB 1.1 of 4 (0.5), DE 0.2 (0.2) |
| 4th Flotilla | GB 3.4 (5.7), DE 4.3 km | DE 4.3, 3.7 km | GB 79 / 18 (72 / 18), DE 518 / 66 (699 / 110) | GB 3.3, 0.3 (3.5, 0.8); DE 8.7 | GB 2.0 of 10 (2.3), DE 0.1 |
| Black Prince | GB 2.8 in 10 of 20, DE 4.7 km | DE 2.9 km in 8 of 20 (4.1, all), 2.8 km | GB 58 / 45 (58 / 21), DE 154 / 47 (372 / 81) | DE 2.4, 0.6 | GB 0.2 of 1 (0.7) |
| Dover Strait | 0.5 and 0.3 km | 0.4 and 0.3 km, GB 1.1 (4.4), DE 3.2 km | GB 110 / 65 (760 / 89), DE 118 / 33 (241 / 30) | GB 4.9, 1.8 (8.7, 1.3); DE 8.3, 1.8 | GB 0.7 of 2 (0.3), DE 1.8 of 6 (2.1) |

**What it says:**
- **The Germans challenge only close now** (within 2.5 km), so they no longer give themselves away at 9 km: the British
  pick them up at Q5's ranges again, and the Germans open at 5.1 km (8.6) at Southampton, once she's made out.
- **Turning away from the light ends the long night duels.** The Dover Strait's 4 km gun duel (Q4) is a close fight again
  (the British salvos' median 1.1 km, the record's point blank), with the Germans losing 1.8 boats of 6 (the record's two).
  At Southampton the British, lit by star shell, turn away and fire little (73 shells), while the Germans hit 30% of 2,588
  at a 3.3 km median: the turn-away is a straight run with a flare kept over her.
- **Black Prince now gets away** (lost 0.2 of 1): the Germans hold their fire on her past 2.5 km and never challenge
  her; in 8 runs of 20 there's a fight, at about 1 km. The record's
  blunder wants her to take them for friends, which needs own ships as contacts (decision 1) or her own hold-fire.
- These are measurements for the tuning pass, not targets met.

**Q7b done (2026-10-29).** The flotilla's dark side: in the dark (as the captains judge it, `SightModel.DarkOf`), a
flotilla attacking together gains its point on the side of the target's bow that has him against the brighter sky as the
boats look back at him from it (`AdmiralLayer.DarkSide`): the moon's path behind him if one side has it, else a twilight
sky `AttackSkyRatio` 1.5 times the other side's or more [EST]; else the side it's on, as by day. The boats there have the
darker sky behind them as he looks at them. `AdmiralLayer.AttackOf` gives the side. Test: the moon in the WSW, a ship
heading north, attacked from her starboard bow; the twilight's glow in the SE, from her port bow; an even sky, either.
**The bench** is Q7a's to the noise: in these cases flotillas seldom attack together by night, as the matchup rarely
sends them in on a line (Q7c's).

**Q7c done (2026-10-29).** The matchup at night, and the commander's flotillas waiting for it:

- **Each class made out at its own range** (`MatchupLight`, in place of the one sight distance: the visibility and the
  night sky's luminance; `SightModel.MadeOutM`, the one made-out range `SeenToM` now calls for the reference ship). In the
  dark a class is made out at the better of her hull (her side-on silhouette, `MatchupProfile.SideAreaM2`, at the night's
  contrast) and her bow wave at her best speed (Q1's white water, at the foam's contrast); by day as far as the guns
  reach, as before. A gun fires only on a class within that range: the defence opens on a run-in where the first attacker
  is made out (a destroyer far closer than a battleship), and every phase's fire stops past it. The lines fight no further
  off than **both** make the other out: a line firing shows itself by its flashes, and the sim can't lay a gun on a flash
  yet, so a gunfight from the edge of the other's sight isn't judged to go unanswered.
- **A line's torpedoes at will go by the captains' rule**: the fast setting within `TorpedoFireShare` of its range
  (`CaptainLayer.FastSetting`). The matchup had let a line fire its slow setting at will, which no captain does; at night,
  with the lines closer, that made standing off and firing long torpedoes look better than any attack. The day's
  matchup tests are unchanged by it.
- **Keeping touch for the night** (`CommanderLayer.Retire`, `CommanderWhy.AwaitNight`): a side declining battle whose
  doctrine attacks by night keeps any unit that would attack the enemy (its guide an attacker against his target as his
  navy holds him, whatever its role) shadowing him instead of going home, while the night falls within
  `CommanderNightWaitS` 90 min [EST]; such a flotilla is free to be sent in once it's dark and worth it, and a unit in
  touch fights as its captains judge. Tsushima's destroyers did exactly this.
- `shipgen matchup A B --sky CD_M2 [--vis M]` judges in a night sky and prints each class's made-out range.
- **The numbers it gives** (`shipgen matchup`, four destroyers against one dreadnought, 30 km visibility): confidence 1.05
  by day; 0.88 in Jutland's midnight twilight (5·10⁻³ cd/m²: the boats made out at 9.7 km, the defence opening closer and
  hitting better than by day); 1.01 at 10⁻³; 21 on a moonless night (3·10⁻⁴: made out at 2.1 km, the dreadnought at
  3.1 km). Four V25s against sixteen Iron Dukes: 0.004 in twilight, 21 on a black overcast night.
- **Tests restated:** `CommanderTests`' two night tests ran in Jutland's June midnight, which by Q1's light is a twilight
  in which a destroyer is made out at 10 km and the attack isn't worth it; they now run on the Dover Strait's moonless
  April night, the avoid test's battleship 4 km off (2 km from the flotilla), and the declining flotilla shadows at
  dusk (it went home before) and is sent in as the sun passes −6°. `MatchupTests`' night case reads a moonless sky.

**The bench after Q7c** (20 seeds; Q7a-b's in brackets):

| Case | First salvo, salvos' median | Shells / hits | Torpedoes, hits | Lost |
|---|---|---|---|---|
| Tsushima | RU 4.3 km in 14 of 20 (never), JP 6.6 km in 14 | RU 1,323 / 267, JP 1,341 / 192 (none) | JP 2.5 from 1.0 km, 0.8 (none) | RU 0.1 of 3 (0), JP 1.9 of 8 (0) |
| Southampton | GB 4.1, DE 5.1 km; 4.2 and 3.4 km | GB 121 / 30 (73 / 22), DE 2,945 / 781 (2,602 / 782) | GB 4.2, 0.1; DE 7.7, 0.7 | GB 1.2 of 4 (1.1), DE 0.2 (0.2) |
| 4th Flotilla | GB 2.9, DE 4.1 km; 3.1 km both | GB 433 / 148 (80 / 18), DE 1,311 / 294 (519 / 67) | GB 16.1, 2.5 (3.5, 0.4); DE 16.8, 1.2 | GB 6.5 of 10 (2.0), DE 0.7 of 6 (0.1) |
| Black Prince | GB 2.1, DE 2.8 km in 18 of 20 (8); 1.1 and 1.7 km | GB 73 / 58 (58 / 45), DE 330 / 150 (154 / 47) | DE 10.2, 2.1 (2.4, 0.6) | GB 0.9 of 1 (0.2) |
| Dover Strait | 0.4 and 0.3 km; 1.4 and 3.3 km | GB 120 / 59 (110 / 65), DE 158 / 40 (118 / 33) | GB 1.7, 0.6; DE 7.7, 1.4 | GB 0.8 of 2 (0.7), DE 1.1 of 6 (1.8) |

**What it says:**
- **The flotillas go in by night.** The 4th Flotilla attacks the German line in every run (16 torpedoes, 2.5 hits; the
  record's one, Rostock) and loses 6.5 boats of 10 (the record's four, Sparrowhawk to a collision); the Germans lose 0.7
  ships of 6 (the record's one, Rostock, torpedoed).
- **Tsushima's boats attack at last**, in 14 runs of 20: they keep touch at dusk and close after dark, firing 2.5 torpedoes
  a run from 1 km for 0.8 hits, and lose 1.9 of 8 (the record's three); the Russians lose 0.1 of 3 (the record's Navarin,
  three more torpedoed, lost next morning). The guns do most of it on both sides (1,300 shells each).
- **Black Prince is lost again** (0.9 of 1, as in the record), the German line torpedoing her (2.1 hits) and hitting her
  150 times at 1.7 km; she hits back 58 times (the record: none).
- **Open for the user (roles):** Tsushima's Japanese destroyer divisions are scouts by role, not a flotilla
  (`Roles.Count`: a boat is an attacker against her own side's heaviest class at sea, which for a side of destroyers alone
  is a destroyer). So the commander never sends them in as a flotilla; here they attack as units in touch, their
  captains judging it. Measuring against the heaviest at sea of any side would make them a flotilla; it's the user's
  approved rule (admirals-orders.md, 2026-10-24), so it's asked, not changed.

**Q7d done (2026-10-29).** The admirals' lines at night:

- **Close order** (`AdmiralLayer.CloseUp`; doctrine **`NightSpacingShare`**, after `WhenLit`, 1 by the code's default,
  the "default" preset's 0.5 [EST]; the launch screen's "Lines at night"): with the sun more than 6° down at his guide, a
  squadron's admiral closes its spacing to that share of the day's, by the group's own formation command (so its rails
  and its stations agree: the sim stays the same at any speed), and opens it to the day's again when the sun's back
  over it, unless its spacing was changed meanwhile (the player's then).
- **Losing touch** (`CaptainLayer.SeesAhead`): a ship in line ahead keeps station only on a next ahead she makes out. In
  the dark her bridge lookout looks at the other's hull as it shows on its bearing (end on from astern) or her wake's white
  water (taken as her bow wave seen bow on [EST]: the wake model's foam isn't in sight yet), at the distance between them:
  one look a ship at her next ahead only, a station-keeping check, not a sighting of friends (decision 1). Not made out,
  she holds the course and speed she had ("lost touch with the next ahead" on the card) until she sees her again, then
  steers back onto the line. Collisions in close order wait for 4.11.
- **Tests:** `NightLineTests` (a squadron closes from 600 to 300 m at dusk and opens out at dawn; under the Dover Strait's
  black overcast a destroyer 600 m astern loses touch, one 150 m astern keeps station).

**The bench after Q7d** (20 seeds; Q7c's in brackets):

| Case | Shells / hits | Torpedoes, hits | Lost |
|---|---|---|---|
| Tsushima | RU 725 / 139 (1,323 / 267), JP 834 / 114 (1,341 / 192); a fight in 12 of 20 (14) | JP 1.5, 0.7 (2.5, 0.8) | RU 0.1 of 3 (0.1), JP 1.2 of 8 (1.9) |
| Southampton | GB 110 / 45 (121 / 30), DE 2,155 / 528 (2,945 / 781) | GB 4.2, 0.3; DE 7.4, 0.7 | GB 0.8 of 4 (1.2), DE 0.8 of 5 (0.2) |
| 4th Flotilla | GB 389 / 88 (433 / 148), DE 1,081 / 160 (1,311 / 294) | GB 7.5, 1.0 (16.1, 2.5); DE 15.1, 0.8 | GB 4.8 of 10 (6.5), DE 0.2 of 6 (0.7) |
| Black Prince | GB 130 / 98 (73 / 58), DE 281 / 103 (330 / 150) | DE 9.8, 1.6 (10.2, 2.1) | GB 0.8 of 1 (0.9) |
| Dover Strait | GB 129 / 53 (120 / 59), DE 85 / 34 (158 / 40) | GB 2.6, 0.3; DE 7.0, 1.9 | GB 1.1 of 2 (0.8), DE 0.8 of 6 (1.1) |

**What it says:** close order and losing touch move every case by about the seeds' spread, and the record's shapes hold:
the 4th Flotilla now one torpedo hit and 4.8 boats lost a run (the record's one and four), Southampton a German cruiser
lost (Frauenlob), Black Prince sunk in four runs of five.

**Q7 as a whole** (Q6's bench in brackets, the record after it):
- **4th Flotilla:** the boats attack every run, 7.5 torpedoes for 1.0 hits (3.5, 0.8; Rostock's one), 4.8 boats lost of 10
  (2.3; four).
- **Southampton:** the British hold fire and turn from the German flares; 0.8 cruisers lost a side (GB 0.5, DE 0.2; GB 0,
  DE Frauenlob).
- **Black Prince:** sunk in 0.8 runs of 1 (0.7; sunk), her hits on the line 98 (21; none).
- **Dover Strait:** a close fight (salvos' median 1.7-1.9 km; Q4-Q6's 4 km duel), 0.8 German boats of 6 (2.1; two).
- **Tsushima:** a night attack at last, in 12 runs of 20 (never; the record's Navarin sunk and three more torpedoed), the
  boats losing 1.2 of 8 (three).
- The bench takes 36 s for the 100 plays (26 s at Q6): more fighting, the night matchup's few µs more a judgement.
- **The day actions hold** (8 seeds each, against Q6's): Germany loses 10.8 of 26 at the Dogger Bank (11.6, 12.0), 18.5 of
  24 in the Heligoland Bight (20, 19), 1.0 of 5 at Coronel (1.1); Coronel, fought into the dusk, sees fewer German hits
  (491 from about 640), the dark's matchup and lights at work there.

**Open after Q7:** every number is [EST] until the research; presets one a period (the doctrine rows' values by navy and
year) wait for the tuning pass; the roles question above (a side of destroyers alone); firing at a gun's flash alone (fire
control lays on lamps and bow waves only), which also keeps the matchup's night gunfights mutual; a ship turned away from
a light doesn't zigzag or make smoke; searching sweeps toward the nearest contact only; the wake's foam isn't a sight of
its own (the station-keeping look borrows the bow wave's); collisions (4.11) for close order; the coarse tier runs all
of it at its thinks (Q11).

### Q8 The crew at night

- Searchlight crews in the crew book (Q3). The searchlight term for green crews (`crew.md` N3's unbuilt row, CUF R3).
- A ship firing rapidly in the dark dazzles herself (Q2). Fatigue after a day's fighting stays its own later system
  (no lumped factors).

**Q8 done (2026-10-29).** Two of its three items were already in: the searchlights' men at their own station
(`BattleStation.Searchlights`, Q3) and a ship's own flashes dazzling her (Q2). What was left is crew.md N3b's last row,
the strain of being found and fired on (CUF R2-R3), built for straddles and lights together as the row has it:

- **A salvo down about her** (`GunnerySystem`, as each salvo's fall is judged; `DamageSystem.Salvo`) that straddled her,
  or fell while she was lit (the others' beams and flares on her `LitShare` times the night sky's light, the same test
  her captain turns away by, Q7a), adds duress at every station on the burst's 30 s channel (`Steadiness.Pressed`):
  `DuressStraddle` 0.04 (CUF R2's full weight, a civilian crew's) × `DuressStraddleGreen` 0.5 × her greenness (1 less her
  men's experience: all of it for green men, none from average up; no sourced case of trained crews breaking before
  being hit, R3). Who stays is worked out again at once.
- **What it amounts to:** 0.02 a salvo with a 30 s half-life, so a green crew under a salvo every 20-30 s carries a
  steady 0.02-0.04 against a nerve of 0.30: by itself it never sends men away, it brings a station that's been hit nearer
  its edge. crew.md's N3b note had it halved twice (0.01); CUF R3's green weight is half of R2's 0.04, so 0.02.
- **Not here:** civilian crews at full weight (crew.md decision 9, after that plan); the coarse tier's fire (it judges no
  straddles: Q11).
- **Test:** `NightDoctrineTests.A_green_crew_feels_being_lit_and_straddled` (0.02 a station lit or straddled; none in
  the dark wide of her, none for trained men).
- **The bench** is Q7d's to the shot: none of its cases has a green crew (the term reads nothing for trained men).

### Q9 The player's hand

- Orders, in the input grammar: searchlights (burn, douse, by doctrine), illuminate a contact with star shell or a
  beam (an armed verb), hold fire or weapons free, challenge. The player's orders beat doctrine.
- What the map shows: lights as marks (a beam's bearing, a flare's patch), flash and light contacts, unknowns, "you're
  showing a light" on the card. Mockup first, then the port, per the UI process. No popups.

**The controls (proposed and decided with the user, 2026-10-29; mockup next):** a setting, and two orders by hand.

- **Darkened, the strict silent setting:** a doctrine field (`Darkened`, on/off) a unit's, set on the launch screen as
  a standing order and in the fight by the strip's flag (`UnitOrders.Doctrine`, the player's). Strict: no searchlights
  (not when fired on, not to light the guns' target), no star shell by doctrine, no challenge lamp, and **her guns fire
  only once she's fired upon** (the user: destroyers creeping in the dark to the best ambush). Once fired on, she fights
  by her orders; her lights stay dark. Star shell ordered by hand still goes (the player's own act; a flare shows only
  the gun's flash).
- **Beams on, forced:** the arcs burn and are exposed whenever it's dark (an order given at dusk holds through the night),
  held on her guns' target (the chief battery's first: Engage… picks what she lights), else sweeping the arc about the
  nearest contact. Beats the captain's doctrine, his douse when lit and keeping dark in a torpedo attack. Doctrine's turn
  away when lit still steers her: the conn (C) is the player's way to hold her course (the user: that option is there
  already). The line's illuminator: one ship with Beams on, engaging the target the rest fire at.
- **Darken and Beams on** are two flags, never both on; neither lit is the captain's doctrine, a lit one clicked again
  hands it back (as Make smoke and the held Shells ▾).
- **Star shell…, an armed order:** the key, then a right-click on a contact (an enemy or an unknown) **or on the sea**
  (a point to light, for torpedo craft suspected but not held: new sim work, the user wants it now; the task today takes
  only a contact). One mount keeps a flare up (Q5's task) until the order's given again, the contact's lost or sunk, or
  she's out of its reach; held by hand as `ShellHeld` is, so the captain doesn't cancel it. Given to a unit, one ship
  fires: the nearest carrying star shell with the target in reach.
- **A blanket of star shell (later; the user, 2026-10-29):** an admiral, or a captain, may order a spread of flares
  over an area (several ships or mounts, the flares laid side by side) rather than one over a contact. The doctrine and
  its numbers (how many, how far apart, who fires) likely come with the research results; the sea-point order above is
  its single-flare case.
- **No challenge by hand** (the user): challenging stays doctrine's.
- **In the strip** (strip.md's GUNS pane, no new pane): row 2 Shells ▾ `R` · Star shell… `⇧R`; row 3 Torpedoes at…
  `⇧E` · Darken `⇧X` · Beams on `⇧G`; the title note "dark" / "beams on Derfflinger" / "star shell on V27" while one is
  set; the enemy pane gains Star shell on her `⇧R`. Greyed with the reason where it can't be given (no searchlights, no
  star shell aboard, out of its reach). The shifted keys are free.

**The mockup (2026-10-29):** `docs/map-ui/night.html` (`png/night-mock-1.png` to `-4.png`; N toggles the notes, 1-4 the
state), drawn over the game's Dover Strait at 23:50 (Broke firing on G42 at 300 m, her beam on), the dark states over
the same frame with the beam, the fire lines and G42's marks painted out. The ships' state is made up where a state
needs it (Broke has no star shell). Four states:

1. **Beams on by hand:** the GUNS pane's rows become Fire at will · Engage… · Hold fire; Shells ▾ `R` · Torpedoes… `⇧E`
   (what's loaded to its tooltip; the card's TORPEDOES has it); the night row Star shell… `⇧R` · Dark `⇧X` · Beams `⇧G`
   (the short labels fit the pane's 234 px as it is). A beam as a mark: a narrow wedge of the light's colour from the lamp
   to its reach, fading, a thin line to the ship it's held on; **lit** as a dashed ring of the light's colour about any
   ship in a beam or under a flare. The card's chip "showing a light: beams" by "in action"; a spark by her name in the rail.
2. **Star shell armed on the sea:** the status bar's amber pick, the cursor's verb ("Broke: STAR SHELL · the sea here ·
   1.1 km"), the flare's patch ghosted where it would burst; the reach as a dashed ring while armed. **A light seen** as a
   spark where it's reckoned with its bearing dashed (the placeholder disc goes), labelled with what showed.
3. **Darkened:** Dark lit, Fire at will stays lit (the order she'll fight by once fired on), the note "dark: holding fire
   till fired on"; the card's chip "dark", the rail's DOING "dark · holding". **An unknown** (Q6: made out, not known for
   friend or foe) as APP-6's quatrefoil in the unknown's cream, filled in sight, the enemy's diamond once known for a foe;
   the rail's ENEMY groups unknowns first.
4. **The unknown selected:** the ENEMY pane says she's lit and by what; ORDERS AGAINST HER gains Star shell on her `⇧R`
   beside the torpedoes. A flare burning: a four-pointed star, the sea it lights as a faint disc, its seconds left.

**Questions on the mockup, answered (the user, 2026-10-29):** the three buttons are what's wanted; (1) the row as mocked;
(2) the quatrefoil (my call: apart from the diamond at a glance); (3) the marks on the table only, the visuals pass makes
the close-in look; (4) as mocked: Dark lets her shoot back once fired upon, it avoids the first detection.

**Q9 done (2026-10-29).** The player's hand at night, as mocked:

- **Darkened** (`DoctrineField.Darkened`, last; the launch screen's "Darkened at night"; the strip's Dark `⇧X` sets the
  unit's): in the dark (`CaptainLayer.InDark`, Q1's test) no searchlights or star shell by doctrine, no challenge lamp
  (an unknown held, not challenged), and the guns hold (`Captain.Holding`) until she or a ship of her squadron is fired
  upon (an enemy battery on her: `FiredUpon`), then fight by their orders for `DarkenedFreeS` 600 s after the last time
  [EST] and hold again. Torpedoes at will go as ever (the ambush is the point); the player's own Engage fires; an
  admiral's distributed Engage holds. By day nothing changes.
- **Beams** (`AiCommand.Beams`, the player's to every ship down a unit's tree; `Captain.BeamsHeld`): the arcs burn in the
  dark against doctrine, Darkened, the douse when lit and keeping dark in an attack: held on the guns' targets (the chief
  battery's first, seen or not, at any range), else the one lit while she's held, else sweeping about the nearest contact,
  or about her bow with none. Doctrine's turn away when lit still steers her (the conn holds her course). Beams and Dark
  never both lit: one clears the other.
- **Star shell…** (`AiCommand.StarShell`; `UnitOrders.StarShell` on a contact, `StarShellAt` on a point; armed by `⇧R`,
  given by a right-click on a contact or the sea; `⇧R` again while one of the player's burns stops it): to a ship, hers;
  to a unit, one ship of its tree, the nearest that carries star shell with the target in reach (`AiSystem.Nearest`).
  `StarShellSystem.IlluminateAt`: a point's task bursts straight over it (no `StarBeyondM`) and ends only when stopped or
  with no star mount left. Her captain leaves the player's task be (`Captain.StarHeld`), and clears it once the task's
  gone. **Changed for every task:** one whose mounts don't bear now waits for one to (she turns, the target draws round)
  instead of ending; it ends when no working mount has star rounds (a task dropped at once had left a player's order
  silently undone).
- **The strip** (`MapScene.Strip.cs`): GUNS row 2 Shells ▾ · Torpedoes… (the count in its tooltip), row 3 Star shell… ·
  Dark · Beams, each greyed with its reason (no star shell aboard, no searchlights); the note her lights first ("dark:
  holding fire till fired on", "beams on G42", "beams searching", "star shell over the sea") else the battery's line; the
  figure "dark, holding" in amber. The enemy pane's Star shell on her `⇧R`, from the ship you had. The cursor's verb
  "STAR SHELL on X · 1.2 km" / "STAR SHELL · the sea here", the patch it would light ghosted under the cursor. The
  strip's keys now read the key event's own Shift (scripted keys reach them).
- **The card** a chip "showing a light: beams / star shell" in the lamp's colour (`Theme.Lamp`, `ChipKind.Lamp`), "dark"
  plain, where her state chips leave room; the lit reason now reads "lit: turned away" (her lights stay on under Beams).
  **The rail** a spark by the name of a ship showing a light, "dark · holding" in DOING; ENEMY's unknowns grouped first
  with the quatrefoil.
- **The map** (table only, faded out over the world view's boundary; the world view keeps Q4/Q5's placeholders until
  Q10): a beam as a widening wedge fading to its reach, a dashed lit ring about a ship the player's beams hold; a flare
  as a four-pointed star over the sea it lights to 1 lux [EST] (`FlareLitLux`), lit rings on every ship under it; a light
  seen as a spark (the disc went); an unknown as APP-6's quatrefoil (`SymbolShape.Quatrefoil`, the symbol shader's shape 5).
- **The snapshot** (`ShipsView.BeamOn`, `StarOn`, `Night` as `NightFlags`), filled by the searchlight and star shell
  systems and the captains.
- **Tests:** `NightControlTests` (darkened: no fire, lights or challenge in 90 s, fires back once fired upon, lights still
  out; beams by hand light the engaged destroyer against "never", stay on her once the guns hold, out back to doctrine,
  sweep with no target; a point of the sea lit by a German 1916 ship, flares 19 m off it at 293 m, her captain leaving it
  be, stopped; a squadron's star shell from its nearer ship). The sim suite 874 passed, the game's 207.
- **Captures:** `docs/map-ui/png/night-game-beams.png`, `-dark.png`, `-star.png`, `-armed.png`.

**Changed after playtest (the user, 2026-10-29):**

- **Star shell by hand is one shell.** The player's order (a contact or the sea, from the strip or the enemy pane)
  fires a single star shell and ends (`StarShellSystem` task `Once`: `Illuminate`/`IlluminateAt` with `once`); the button
  lights only while it's still to fire (`NightFlags.StarHeld` and a task in `StarOn`), and `⇧R` again before then calls
  it off. Doctrine's tasks still keep a flare up. Keeping one up by hand is the blanket of star shell's later work.
- **Beams by hand go to one ship or one squadron.** The strip's Beams goes to the selected ship at her own level (or
  with Alt), else to her squadron, never to a force over it (`MapScene.BeamsUnit`): at the default level, a ship in a
  force's line had lit every squadron of the force.
- **Beams by hand burn out of a fight too**, as agreed above ("whenever it's dark"): the captains' lights ran only in
  the fire pass (an encounter or engaged), so beams ordered before contact lit nobody. `CaptainLayer.HeldLightsPass` sees
  to the player's beams and star shell flags of ships outside it, at the layer's think (the rails period, 60 s, out of a
  fight; at once when the order's given). Tests: `Beams_by_hand_burn_out_of_a_fight`, the star shell test now one flare
  and done in 12 s.

**Open after Q9:** a long night note widens the GUNS pane (as a long battery line already does); the flare's seconds
left aren't in the snapshot (the mockup's "flare · 40 s"); the lit ring for an own ship in an enemy beam isn't drawn (a
beam's target isn't in the snapshot; the wedge shows it); the challenge lamp isn't marked; a blanket of star shell
(above) waits for the research.

### The research folded in (arrived 2026-10-29)

`research/simulation/night-fighting.md` (eight strands in `night-fighting/`, `night_ref.py` the sight law against the
record) checked the plan's [EST]s. What it changes, agreed with the user 2026-10-29 ("let's get these things wrapped in"),
as four steps before Q10, and items for Q12's tuning pass:

- **Q1b The eye and the sky.** Q1's law is the wrong shape, not the wrong scale: its large-object threshold hardly rises
  in the dark (×1.4 against Blackwell's ×15 at a 10⁻³ cd/m² sky through a 7×50) while its summation grows far too much,
  so the two cancel at the black overcast and drift ×2-5 long into twilight. In its place Blackwell's shape (Crumey 2014's
  fits to C∞ and Riccò's area, as ratios over 30 cd/m²) and a field factor easing as the fourth root below 1.8·10⁻³ cd/m²
  at the eye (the research's fit to five anchors, [INFERRED]); a ship's night contrast 0.9 against the sky (0.6). The
  sky in the same step, since a brighter sky alone would lengthen the night: the twilight's arch a glow over a floor
  (Koomen et al. 1952, k ≈ 3.2; toward/away 11 at −6° to −9°, 6 at −12°, 3 at −15°, already 4-5 before sunset; was
  e^(1.5 cos Δ) at 20, formed only 0-6° down), the starlit horizon 2.5 times its E/π, the daylight table's ends (0°
  750, +5° 4,500, +10° 11,000 lux; −15° and −18° the sun's alone, the stars no longer counted twice). `sighting_ref.py`
  takes the new law and the C# reference rows follow it. Expect the bench's first sightings to fall ×2-4; the
  searchlight's and the matchup's reaches follow without retuning (re-checked against the record, not fitted).
- **Q0b The bench to the record.** The Jutland cases' forces meet on collision courses with the Germans 65° off the
  British bow at Southampton (225°), 65° at the 4th Flotilla (245°), 80° at Black Prince (260°); the record has them on the
  starboard bow (Castor, Ambuscade, Petard) to the starboard beam (Southampton, Dublin, Tipperary, Spitfire), the British
  looking a little more toward the glow (Nottingham "silhouetted against the evening sky … while we remained in darkness").
  (The research's "W to NW" is its summary; its own table is bow to beam.) Courses and speeds set so the meetings come
  on the beam; visibility 8 km (the night's mist: "three to four miles" at daylight);
  the record's lines corrected (Dover at point blank, not 600 yd; Southampton's five German ships; the 4th Flotilla's ~14
  torpedoes for 1 hit; Tsushima's 54 torpedoes and 32 linked mines for 4-6 hits, 3 boats lost); a dawn case (the 12th
  Flotilla at 01:45, the sun −6°, torpedoes at 1,700-3,500 yd); the bench counts the hits on lit and on dark ships of the
  same side (the record: lit ships fired on in seconds, dark ones in the same line left alone) and torpedoes per hit
  (the record under way 1 in 9-17; no German night torpedo known to hit at Jutland).
- **Q3b Searchlights to the record.** Templates: the RN's twin 24-inch to mid-1915, then 36-inch; Russia's six 75 cm with
  remote control (1898-1905); the German navy on the plain arc throughout (no high-intensity; its 110 cm unconfirmed,
  kept [EST]); plain-arc spread 2.5-3°; the high-intensity draw 18-19 kW. **Ready** is the arc burning behind a closed
  iris, trained dark on the contact (the German method, the USN's 20 min behind the shutter): exposing takes about a
  second (4 s); striking from cold stays 10 s.
- **Q5b Star shell to the record.** Its own lower velocity and range table (15 cm Lg 500 m/s against the HE's 835, 10.5
  cm 450, 8.8 cm 650); the German 1916 candle burning ~15 s, not 25, unless parachuted; an RN star shell from 1917
  (issued to all ships during 1917); destroyers' and light cruisers' outfits 10-20% star shell, big ships' secondaries
  2-6% (was 5% for all). The dazzle (Q2) 2-7 s, not 1 (flash-blindness recovery to a lit target). Funnel flare also from
  oil-fired boats at high power (Dover and Schouwen 1917: enough to fight by at close range), not only forced coal.
- **For Q12 (tuning):** doctrine presets one a period from the research's table (RN 1914-16: lights only when fired on,
  no star shell, challenge and hold, the battle line keeps dark and doesn't challenge; RN 1917-18: lights to fire, star
  shell, screens fire without challenging; Germany 1914-16: trained dark then light and fire together, star shell to
  find, recognition lights; Russia 1904-05: lights to search; Japan 1904-05: attack after moonset, from two sides, to
  ≤600 yd); an unanswered challenge marks a foe but doesn't fire the guns (the British line held fire on wrong replies);
  a partial or echoed reply gives doubt, not trust (Castor's two right signs, Flirt's echo, Lynx's copied reply);
  `AnswerChallenge` partial for the Germans from about 22:00; `ChallengeM` ~2,000 m for light forces, none for a hiding
  battle line or a raider; turning away when lit by the unit's role (small craft and cruisers drawing torpedoes turned
  away, lit big ships seldom did); phosphorescence as a sight cue on a scenario's flag (tracks "clearly seen" in 1904);
  raiders and flotillas choosing the moonless hours (the commander's wait for night). Collisions (4.11) more pressing:
  two of Jutland's eleven night losses.
- **Not found by the research** (stay [EST]): German searchlight sizes by class, the star shell's candle and dud rate,
  the "burst beyond the target" rule before WWII, Makarov, Japanese written doctrine, gun flash distances.

**Q1b done (2026-10-29).** The eye and the sky as the research has them:

- **The eye** (`SightModel.DarkOf`): Crumey's fits to Blackwell (`BlackwellCInf`, `RiccoArea`) as ratios over 30 cd/m²,
  the floor's eased by the field factor below `SightDarkFieldCdM2` 1.8·10⁻³ as its `SightDarkFieldPower` ¼ (the two knees
  `SightDarkCdM2` and `SightDarkSummationCdM2` went). `SightContrastNight` 0.9. Brighter than 30 cd/m² through the glass
  the dark adds exactly nothing, as before: Lion sights Derfflinger at 25.0 km and KGV Bismarck at 36.2 km, and
  `sighting_ref.py`'s day rows hold to the last digit (its night rows and the C# test's follow the new law; `night_ref.py`
  keeps Q1's law for its comparison and prints what it did).
- **A silhouette never lowers her contrast** (`InherentContrast`): in twilight she's already near black against the sky
  (0.9 over the silhouette's 0.8), so a low sun or the moon behind her shows as the sky's luminance on that bearing, not
  as contrast. **The moon's path** is that now (`NightSky.MoonPathCdM2`, falling off to none `SightSilhouetteArcDeg`
  either side of the moon's bearing in `Toward`), as the twilight's glow is; the moon's share went from the contrast
  silhouettes in the looks and fire control's sensors.
- **The sky** (`NightLight`): the twilight a floor of `NightTwilightFloor` 1 × E/π (Sacramento Peak's absolute
  luminances against our table's illuminance: its own site's E was about half Schlyter's, so its "5 × E/π" is our 1-3)
  and a glow toward the sun over it, e^(3.2 (cos Δ − 1)) (`NightTwilightArch` 3.2, was the von Mises 1.5), as strong as
  Koomen's toward/away less one by the sun's depth (`NightLight.GlowOf`: 10.5 at −6°, 5.4 at −12°, 2.1 at −15°, none
  at −18°, 2.9-4.3 before sunset; `NightTwilightArchDeg` went); the starlit horizon `NightStarHorizon` 2.5 × E/π; the
  daylight table's ends (0° 750, +5° 4,500, +10° 11,000 lux, −15° 2·10⁻⁴ and −18° 10⁻⁵ the sun's alone).
  `ClearSkyCdM2` takes the settings. The display reads the same table: its dusk and dawn are a little darker below the
  sun's −12° and the hour round sunset a little different (one table for sim and screen, as Q1 set it).
- **Made out by a destroyer's 7×50 lookout, side on** (`NightSightTests`, Q1's in brackets): a destroyer 0.6 km
  overcast (0.4), 0.9 moonless and clear (2.0), 2.5 with the sun 12° down (6.4), 3.5 under a full moon 30° up (9.8); Lion
  1.1, 1.6, 3.9, 5.2 km (0.8, 3.5, 9.6, 13.6). The record's bands: 0.2-0.6, 0.5-2, 0.55-1.8 across from Jutland's glow
  and 2-3.2 toward it.
- **Restated to the record** (tests whose setups leaned on Q1's longer night): the moonless destroyer's band 0.5-2 km
  (was 1-3.5); a fast boat's bow wave about as far as her dark shape (her hull's contrast 0.9 brought it level, was
  before it); a torpedo track on a moonless night 30-700 m (51 m now, was ~170: no phosphorescence yet, Q12's); in
  twilight she's seen further toward the glow than across from it (was: silhouetted at −4°); the director ranges best in
  civil twilight, worse by −7°, and nothing at 10 km by −9° (was a dusk at −10° that still ranged at 10 km). The night
  tests' ships moved within a moonless night's sight (0.75-0.9 km, the beams', flares' and darkened ships' tests), and the
  challenge tests to a full moon at their 1.9-2.2 km (on a moonless night a ship is seen only within a kilometre, where
  the challenge's own lamp lights her enough to make her out); the challenged destroyer is darkened in the captains'
  test (under the moon she sees the challenger and fires, giving herself away: the record's pattern). Sim 876, shipgen
  59, game 207 passed.

**The bench after Q1b** (20 seeds; Q9's in brackets):

| Case | First seen | Salvos' median | Shells / hits | Torpedoes, hits | Lost |
|---|---|---|---|---|---|
| Tsushima (from dusk, −5°) | RU 12 km in 18 of 20 (5.0 in 11), JP 14 km | 2.5, 3.1 km | RU 393 / 92 (725 / 139), JP 466 / 101 (834 / 114) | JP 4.1 from 0.9 km, 0.8 (1.5, 0.7) | RU 0.1 of 3, JP 0.9 of 8 (1.2) |
| Southampton | GB 1.8 km in 4 of 20 (6.1, all), DE 6.5 (9.2) | 1.2 km (3.1-3.6) | GB 12 / 8 (140 / 54), DE 43 / 30 (1,876 / 459) | GB 1.3, 0.6; DE 0.8, 0.1 | GB 0 of 4 (0.6), DE 0.1 of 5 (0.9) |
| 4th Flotilla | GB 1.2 (3.7), DE 1.4 km (4.3) | 1.0-1.1 km (3.5-3.9) | GB 252 / 182 (403 / 89), DE 542 / 264 (1,020 / 152) | GB 25.8 from 0.6 km, 8.3 (8.2, 1.2); DE 7.5, 2.4 | GB 7.3 of 10 (5.0), DE 1.2 of 6 (0.2) |
| Black Prince | GB 1.1 (3.5), DE 1.2 km (4.7) | 1.2-1.8 km (1.2-2.3) | GB 80 / 61 (109 / 86), DE 128 / 60 (294 / 114) | DE 4.7, 1.8 | GB 0.3 of 1 (0.8) |
| Dover Strait | 0.4 and 0.3 km (0.5, 0.3) | 1.4, 2.5 km | GB 128 / 64 (125 / 52), DE 107 / 39 (84 / 33) | GB 2.8, 0.7; DE 7.1, 1.3 | GB 0.8 of 2 (1.0), DE 1.4 of 6 (0.8) |

**What it says:**
- **Jutland's night is fought at the record's ranges.** The 4th Flotilla and Black Prince first see the Germans at
  1.1-1.4 km (the record's 1,000-2,000 yd; 3.5-4.7 km before) and fight at about a kilometre, the guns hitting half to
  three quarters of their shells (Westfalen's 92 × 15 cm and 45 × 8.8 cm into Tipperary in five minutes).
- **Night torpedoes now hit far too often**: the 4th Flotilla fires 26 from 0.6 km for 8.3 hits a run (the record about 14
  for one), the Germans 7.5 for 2.4 (none known to hit). At these ranges a track is seen at 50 m, too late to comb, and the
  torpedo's aim has no night term (the target's course and speed from a short track in the dark, the boats firing at
  speed). A model item for Q12, measured here; the flotilla's 7.3 boats lost of 10 (the record's four) goes with it.
- **Southampton fights in 4 runs of 20**: the Germans see the British at 6.5 km toward the glow and hold their fire past
  their challenge range; the British, across from it, see them only at 1.8 km. Q0b's geometry (the beam) and visibility
  (8 km) are the next step for that case.
- **Tsushima starts at dusk** (the sun −5°): both sides see each other at 12-14 km while it's light, then lose touch in
  the dark; the Japanese boats attack from 0.9 km.
- **The day actions** (8 seeds, the fine tier, before → after, ± a standard error): the Heligoland Bight within the noise;
  Coronel, fought into the dusk, British ships lost 0.6 → 1.1 (±0.3), German 0.9 → 0.5 (the record: two and none); the
  Dogger Bank, opening in a winter dawn's twilight with the sun never over 13°, British hits 1,062 → 761 (±100) and ships
  lost 7.8 → 10.8 (±1.4). Measured, not tuned: the scenarios' balance pass is the place for it.
- The bench took 29 s for its 100 plays (34 s before).

**Q0b done (2026-10-29).** The bench to the record:

- **The meetings on the beam:** Southampton's squadron at 15 kn on 160° and the 4th Scouting Group at 16 kn on 140°, the
  Germans starting 7.6 km on her starboard beam (250°); the 4th Flotilla at 15 kn on 180° and the German line at 16 kn on
  160°, 7.6 km on her beam (270°), the light cruisers kept where they were beside it; Black Prince as she was (80° off her
  bow, the record's "blundered into"). Visibility 8 km in the three, the night's mist.
- **A dawn case**, `night-12th-flotilla` (the 12th Flotilla's leader and seven M boats steaming south, the II Squadron's
  five Deutschlands and Hessen on 120° from their starboard bow, 01:00 GMT, meeting at about 01:45 with the sun −7° and
  rising).
- **The record's lines** as the research has them (Tsushima's torpedoes and mines, Southampton's five ships and her four
  hot minutes, the 4th Flotilla's 14 torpedoes for one hit, Black Prince's 750-1,500 yd and Campbell's two hits, the Dover
  Strait at point blank with the Germans firing first).
- **The hits on ships lit** (by others' beams and flares past `LitShare`, as her captain judges it) beside the hits on
  ships showing their own light.

**The bench after Q0b** (20 seeds; Q1b's in brackets; Tsushima and the Dover Strait unchanged):

| Case | First seen | Salvos' median | Shells / hits (on ships lit) | Torpedoes, hits | Lost |
|---|---|---|---|---|---|
| Southampton | GB 5.6 (1.8 in 4 of 20), DE 4.9 km (6.5), every run | 1.7-1.8 km (1.2) | GB 79 / 55 (20), DE 567 / 315 (71) | GB 13 from 2.8 km, 3.0; DE 7.5, 1.1 | GB 0.9 of 4 (0), DE 1.6 of 5 (0.1) |
| 4th Flotilla | GB 1.3 (1.2), DE 1.9 km (1.4) | 0.9-1.1 km | GB 198 / 147 (81), DE 447 / 241 (127) | GB 26.8 from 0.8 km, 9.1 (8.3); DE 6.2, 1.9 | GB 7.0 of 10 (7.3), DE 1.3 of 6 (1.2) |
| Black Prince | GB 0.8 (1.1), DE 0.9 km (1.2) | 0.9-1.1 km | GB 91 / 75 (40), DE 71 / 35 (13) | DE 4.7 from 0.6 km, 1.9 | GB 0.1 of 1 (0.3) |
| 12th Flotilla (new) | GB 8.7 km in 7 of 20, DE 8.8 km | 4.9-5.9 km | GB 29 / 4, DE 424 / 19 | GB 0.5 from 1.8 km, 0 | GB 0.2 of 8, DE 0 of 6 |

**What it says:**
- **Southampton is the record's fight now**: every run, the British seeing first (5.6 km against 4.9: they look a little
  more toward the glow), fire from 2.4-2.6 km and a median of 1.8 km (the record's 1,500-2,000 yd), the Germans' star
  shell up in every run; a German cruiser and more lost a run (Frauenlob), but also nearly one British (none), and the
  British torpedoes 13 for 3 hits (one for one).
- **Black Prince now gets off** (lost 0.1 of 1): she and the line see each other at 0.8-0.9 km and the Germans fire 71
  shells (she 91, hitting 83%). Her blunder (closing on ships taken for friends, the Germans lighting her first) is Q12's
  doubt and challenge.
- **The dawn's attack hardly comes:** at −7° to −5° the boats and the line see each other at 8.7 km (the visibility), the
  Germans open at 6.6 km, and the flotilla fires torpedoes in 7 runs of 20 from 1.8 km; the record attacked at
  1,700-3,500 yd. The flotilla's night attack is keyed to the sun 6° down (`NightSunElevationDeg`, the AI's judgment), so
  it ends just as the record's began; the dawn's mist may also have been thicker than 8 km ("no more than two miles",
  Malaya). For Q12.
- **On lit ships**: at the 4th Flotilla more than half the hits on each side land on ships lit (GB 81 of 147, DE 127 of
  241); at the Dover Strait 46 of 64 British.
- **Night torpedoes stay the outlier** (the 4th Flotilla 27 for 9 hits, Southampton 13 for 3): Q1b's open item.
- The bench takes 38 s for 120 plays.

**Q3b done (2026-10-29).** Searchlights to the record:

- **Trained dark** (`SearchlightSystem`): Ready may name a contact; the lights are trained on her dark
  (`SearchlightTrainS` 3 s from the order or the strike), and lit, she's lit when the iris opens (`SearchlightExposeS`
  1 s, was 4 for training and exposing together); a light ready on another, or none, trains first (4 s). The captains
  keep their struck lights trained on the chief battery's target, else any battery's, held in the picture
  (`CaptainLayer.NightLights`). Test: `Lights_trained_dark_on_a_ship_expose_at_once` (1.0 s against 4.0).
- **The templates** (`SearchlightTemplates`, `searchlight-templates.md`): every plain arc 1.6·10⁴ cd/cm² and 3° (was
  1.2-1.6·10⁴, 1.8-3°); Victorian six 90 cm on the continental ships and a new **RN Victorian** of 24-inch; the
  pre-dreadnoughts' 75 cm by Russia's, six in pairs with remote control, 40 cm on the boats; a new **RN dreadnought
  (1910)**, twin 24-inch mountings hand-worked by voice pipe, for the RN's plant years 1910-1915 (its 36-inch from 1916,
  controlled from 1917); the German navy's plain arcs, trained dark from a control position (its 110 cm still [EST]); the
  USN's high-intensity arcs from 1915, remote, 1.8°. The notes say [SRC] or [EST] for each.
- **Design format 16** (`DesignFormat.SearchlightsToRecord`): a block still exactly as format 14 or 15 wrote a template
  takes its navy's in its plant's year from the new table; one changed by hand stays. 130 design files upgraded; goldens:
  326 design cases, 79 SVG cases, 17 sprite.json (`ShipDesign.Version` 52). Lion, Iron Duke and the RN's light cruisers
  of 1910-15 now carry 24-inch lights (3.6·10⁷ cd), as they were built (Jellicoe's "very powerful (much more so than
  ours)" of the German lights), a few tonnes lighter.
- **Reach** (`shipgen searchlight`, the reference ship lit, a lookout's 7×50, moonless): Lion's 24-inch 4.6 km in clear
  air, 2.5 in 10 km, 1.5 in 5 km (her 36-inch 5.1, 2.8, 1.7); Helgoland's 75 cm 4.4, 2.4, 1.5. The record's plain 36-inch:
  2.7 km lit well, 3.7 with glasses.
- **Restated:** Lion's lights in `SearchlightTests` are 24-inch (were 36-inch); the consort test's destroyer at 2.0 km (was
  2.5, where the 24-inch no longer lets her own lookout make her out in 10 km haze).
- **The bench** moves within its seeds' spread (Southampton GB 1.3 of 4 lost, DE 1.9 of 5; the 4th Flotilla GB 6.6 of 10,
  DE 1.1 of 6; Black Prince 0.2 of 1); the lights first go on at 0.6-1.1 km at Jutland, the record's 150-2,500 yd.

**Q5b done (2026-10-29).** Star shell, the dazzle and the funnel flare to the record:

- **A reduced charge** (`ammunition.NAME.star.velocity_share`, 1 when absent): a star round's muzzle velocity is the
  battery's rule's times it, so it flies its own range table (each shell type has its own already). The German and
  Russian 1911 star shells 0.6 (the 15 cm Lg's 500 m/s against the HE's 835; the 10.5 cm's 450, the 8.8 cm's 650): a
  15 cm star round leaves at about 520 m/s.
- **The burn** of the 1911 rounds 15 s (was 25: "lasting quite 15 seconds"; the fall 6 m/s kept, one "opened up like a
  parachute"); the candle, the fuze's error and the duds stay [EST], not found.
- **The share** (`ShellTemplates.StarShareOf`): a main battery's 15% (the destroyers' and light cruisers', the night's
  illuminators), a secondary's 4% (was 5% for both; the USN's outfits, 15-20% and 2-6%).
- **Design format 17** (`DesignFormat.StarShellToRecord`): star blocks still as format 15 copied them take the new
  numbers, batteries still at its 5% the new share (the others scaled); hand-made ones stay. 130 design files upgraded;
  goldens: 326 design cases, 38 SVG cases, 33 sprite.json (`ShipDesign.Version` 53).
- **The dazzle** (`SightDazzleS` 2.5 s, was 1): a destroyer at her threshold 1 km off on a moonless night is lost after
  her 15" twin fires, still dimmed at 8 s, back by 15 s (the flash-blindness record's 15-30 s to a silhouette near the
  threshold; a lit target, brighter than the dazzle, isn't touched). `Her_own_flash_dazzles_her_for_a_few_seconds`
  restated (was back by 8 s).
- **Funnel flare on oil too** (`SmokeSources.FunnelGlowCd`): a forced oil-fired boat flares as a coal-fired one does (the
  Dover Strait 1917, Schouwen: fought by each other's flare).
- **Not done:** an RN star shell from 1917 wants an RN 1917 shell family (the Greenboys), which there isn't; the RN 1912
  family stays without one (none at Jutland).

**The bench after Q5b** (20 seeds; Q3b's in brackets):

| Case | German star shell | Shells / hits | Torpedoes, hits | Lost |
|---|---|---|---|---|
| Southampton | 91 ship-min (149) | GB 71 / 49, DE 414 / 193 (492 / 257) | GB 15.7, 4.6; DE 6.3, 0.9 | GB 0.8 of 4 (1.3), DE 2.0 of 5 (1.9) |
| 4th Flotilla | 76 ship-min (120) | GB 221 / 151, DE 368 / 197 (434 / 241) | GB 24.6, 7.5 (23.2, 7.9); DE 6.0, 1.8 | GB 5.5 of 10 (6.6), DE 1.1 of 6 |
| Black Prince | 20 ship-min (49) | GB 70 / 63, DE 64 / 36 | DE 4.7, 1.4 | GB 0 of 1 (0.2) |
| Dover Strait | 8 ship-min (14) | GB 92 / 46, DE 65 / 31 | GB 1.8, 0.3; DE 6.0, 1.2 | GB 0.8 of 2, DE 0.8 of 6 (1.1) |

**What it says:** the shorter burns halve the German star shell's time aloft and the German hits fall by a fifth; the
4th Flotilla's losses come down to 5.5 boats of 10 (the record's four); Black Prince is never lost now (the record: lost
with all hands), her blunder being the challenge's and the doubt's (Q12).

**Open for Q12 from the research steps:** night torpedoes hit three to eight times the record's rate (their aim has no
night term; tracks seen at 50 m); Black Prince's blunder (doubt, the partial reply); the dawn's attack (the AI's night
keyed to the sun 6° down); doctrine presets one a period; turning away when lit by role; phosphorescence; the moonless
hours for raiders.

### Q10 The look

**Agreed 2026-10-29 in `docs/visuals/night-lights.md`** (Q10a-Q10f; decisions 1-5 there): the
nights dark again (one "night eye" from `NightLight.At` replaces the amplified night light), the night's own light
desaturated while lights keep their colour, flares and beams in the shared light block (a high point light, a spot
light), beams scattered in the haze and smoke, star shell with its halo and trail through the effects camera, the
moon under the cloud, funnel glow and challenge lamps, night ticks, phosphorescence (the look now, the sim's cue
later: a sighting cue for a glowing bow wave and wake on a scenario's `phosphorescence`). Placeholders (a line for a beam, a disc for a flare) until it.

**Q10 done (2026-10-29)**, as `docs/visuals/night-lights.md` records it: Q10a the dark (one night eye from
`NightLight.At`, the amplification gone, Purkinje, night ticks), Q10b lights in the block (flares as high point lights,
beams as spot lights), Q10c beams in the air (`BeamRenderer`, lit haze and smoke), Q10d star shell (`FlareRenderer`
through the effects camera), Q10e the moon under the cloud, funnel glow, challenge lamps, small lights keeping their
colour, phosphorescence as a look; Q10f's first pass (the eye's response to artificial light, `LightKnee` 20; the star's
glow at 0.3) and the beams over a hull (glow at half, thinned over the ship held) approved by the user. Open: finer
tuning for ordinary displays from playtest feedback (the levels were set on the user's OLED); the sim's phosphorescence
cue is Q12's. Q1b's sky reaches the look only through the shared daylight table (the night eye reads `SunLux` and the
stars, not the sky's luminance by bearing).

### Q11 The coarse tier

Lights in coarse's minute look, cheap: a light's on/off and its illuminance per minute, star shell as a minute's
flare. The parity bench gets night cases. (Night's coarse gap is the AI's minute cadence, `coarse.md` J5; this pass
doesn't change that.)

**Q11 done (2026-10-30).** The user: keep it simple, coarse results need only be statistically roughly right (humans
don't play at that tier). Read first: everything night already ran in coarse (the lights gathered at each look, the
searchlights' orders and star shell's ticks on world time, the captains' night pass at every tier's think), so Q11 was
measuring and mending what the minute cadence breaks.

- **The bench:** `coarse-parity`'s fifth layer, `night` (`coarse.md` "The parity bench"): the five `night-*` scenarios
  with the scenarios' measures, and each side's beams and star shell (ship-minutes), first salvo and torpedoes (minutes),
  hits on ships lit (share). 48 seeds both ways in ~75 s.
- **A minute's flare** (`StarShellSystem.Gather`): a look takes the flares that burned within its span beyond a fine
  look's (`dt - SightingMs`): the coarse look the minute's, the 5 s watch its 4 s, a fine look only what burns now (no
  change there). Burnt flares are kept a coarse step for it. As a gun's flash already showed over the look's span.
  The 4th Flotilla's British beams came back to fine's (22 → 14 ship-minutes, fine 14), German hits 125 → 132 (fine 198),
  Southampton's German star shell 53 → 73 (fine 96).
- **The coarse fire watch** (`AiSystem.WatchFire`, rung 2 as J3's torpedo watch): a coarse captain's think that finds a
  torpedo target but can't fire (tubes not bearing, a friend on the track) puts her on a watch, and a flotilla's signal
  to fire puts its boats on it; the same tests run every `CaptainPeriodMs` until that layer's next think, as fine's
  thinks do. A boat thrown out of the line (hit, falling out) fires as she turns away, the record's way. The 4th
  Flotilla's British boats firing 2.7 → 4.1 (fine 6.9); Black Prince's German 1.6 → 2.2 (fine 2.6, in the band).
- **Tried and dropped:** the ready beams on the watch (exposed the moment the target's in reach): German beams 30% → 48%
  of fine's, their hits unmoved, Tsushima's Japanese beams overshot further. A sub-threshold glimpse held only a fine
  look's chance in the long look: no effect (below a margin of 1 the rate is nil, so there are no glimpses).
- **Fixed on the way:** `Lights.AddFlare` wrote past its array when a flare was the 17th light (`flare[Add(...)]` read
  the array before `Add` grew it).
- **Left, measured** (the standard panel, `coarse.md`'s log 2026-10-30): Southampton's first salvo 17 min late in coarse.
  The enemy's minutes-old smoke doesn't reach coarse's sight lines (`bugs.md`), and at night it decides a margin near 1:
  coarse holds Stettin in sight, the commander counts her as in contact and orders no intercept. With the field off the
  tiers agree. The 4th Flotilla's British torpedo hits still -70%, German hits -33%, German beams -70%: the minute's
  thinks in a melee lasting a few minutes (J5's). The day's gunnery drift the panel found is older than Q11 (`bugs.md`).
- **For Q12:** the night's AI timelines are knife-edged in fine too: an unidentified contact held in sight makes the
  commander "in contact" while the captains hold fire on unknowns, a standoff only lost sight breaks (Southampton's fine
  first salvo goes 25 → 45 min with the smoke field off).

### Q12 Fit and check

Against `night-history` and the night scenarios. Restated anchors and what moved, for the user.

**Q12a, the check (2026-10-30).** The user: do Q12 and see how well it matches; the coarse fixes come later, with
coarse's day drift (`bugs.md`) in the same pass. `shipgen night-history --runs 40` (the fine tier, 240 plays in 66 s),
after Q12b's doctrine (below; before it in brackets where it moved):

| Case | The record | The sim |
|---|---|---|
| Tsushima | attacks at 600 yd and less; 54 torpedoes and 32 mines for 4-6 hits; ships showing lights found and torpedoed, the dark ones untouched; 3 boats lost | boats fire from 1.0 km, 1.4 torpedoes a run for 0.3 hits (2.8 for 0.5); the Russians sweep from the start (230 ship-min), 48 of the Japanese 104 hits on ships lit; boats lost 0.8 of 8 |
| Southampton | five ships on the beam at ~1,500 yd; the hot part 4 min of 15; Southampton's one torpedo hits Frauenlob; no British ship lost; Dublin 8 hits, München 3, Elbing 1 | first fire 2.2-2.5 km, salvos at a median 1.5-1.9 km; the British 12.9 torpedoes for 2.9 hits; the Germans 503 shells for 255 hits (385 / 188); lost GB 0.7 of 4 (1.0), DE 1.5 of 5 (2.0) |
| 4th Flotilla | lit at 1,500-2,000 yd, wrecked in five minutes; ~14 torpedoes at 800-1,000 yd for 1 hit; three boats to gunfire (Sparrowhawk to a collision) | first seen 1.3-1.9 km, fire from 1.0-1.3 km; 27.8 torpedoes from 0.8 km for 9.0 hits; lost GB 6.4 of 10 (5.7), DE 1.4 of 6 |
| Black Prince | blundered into the line at 750-1,500 yd, taking them for friends; lit and sunk in some 15 min with all hands; perhaps 2 hits on Rheinland | seen both ways at 0.9 km; her challenge answered 0.9 a run (0); she fires 123 shells for 106 hits (73 / 62); lost 0.3 of 1 (0.2) |
| 12th Flotilla (dawn) | 17 torpedoes at 1,700-3,500 yd for 1 hit (Pommern); "too light for searchlights", mist | the boats see the line in 9 runs of 40 at 8.4 km; 0.1 torpedoes a run; the Germans open at 6.6 km |
| Dover Strait | point blank, the Germans firing first; G42 and G85 sunk; none British | seen at 0.3-0.4 km both ways; lost GB 0.7 of 2, DE 0.8 of 6 |

**What it says:**
- **Seeing matches.** First sightings and fighting ranges are the record's in every Jutland case (0.9-1.9 km), the
  Dover Strait at point blank, Southampton at a median 1.5-1.9 km. Lit ships draw the fire (the 4th Flotilla's Germans
  111 of their 213 hits on ships lit; Tsushima's on the Russians' sweeping ships).
- **Torpedoes hit three to nine times the record's rate** (the 4th Flotilla 1 in 3 against 1 in 14; Southampton 1 in 4.4
  against 1 for 1; Black Prince's Germans 2.7 of 6.0 against none known at Jutland). Not a night term: the day's anchor
  (`fighting.md` 4.6, a spread of 4 at a steady QE at 2.5 km) hits with 36% of its torpedoes, and the night's 0.8 km gives
  the same. The aim has no error beyond the picture's estimate (and 11% duds): a model item for the torpedo's fire
  control, day and night, to decide with the user.
- **Gunfire at light ships runs high where the fight runs long**: Southampton's Germans 255 hits over the fight against
  the record's dozen or so in four hot minutes (the record's count is thin: Dublin 8, Southampton's 89 casualties).
- **Black Prince isn't sunk**: her challenge now meets the German reply (taken for friends, the record's blunder), but she
  still fires first and hard at 0.7 km, and the Germans' fire is brief (90 shells, their beams 2 ship-minutes; the record:
  Thüringen's searchlights and four battleships' fire).
- **The dawn's attack doesn't come**: the flotilla's night attack is keyed to the sun 6° down (`NightSunElevationDeg`),
  a switch on time of day the principles rule out, and the dawn's mist was thinner than the record's ("three to four
  miles", Malaya's "no more than two").

**Q12b, night doctrine one a period (2026-10-30).** The research's table (`d-identification-and-doctrine.md` §3) as
presets over the default (`Content/Sim/doctrine.json`; a preset's `"from"` now takes an earlier one's fields first,
`DoctrinePresets.Parse`): `rn-1914` (searchlights when fired on, no star shell, challenge and hold, at 2,000 m),
`rn-1917` (lights and star shell to fire, screens fire on unknowns), `german-1914` (trained dark then light and fire,
star shell to find, challenge at 2,000 m), `russia-1904` (lights to search, fire on unknowns; no challenge). On the
night benches, the Dover Strait (`rn-1917` against `german-1914`), Tsushima's two scenarios and the Jutland; the Germans
at the Jutland answer 0.4 of the British challenges (`AnswerChallenge`: Corbett's "first two signs correct", the
research's 0.3-0.5 from about 22:00). Japan 1904-05's written doctrine wasn't found: the default. Only night fields
differ, so the day doesn't move. Sim 877, game 207 passed.

**Left for Q12's next steps** (each a model choice for the user): the torpedo's aim error (day and night: the target's
course and speed as a director or an eye judges them, firing while turning or under fire); why a light ship lit at
1.5-2 km takes the fire of a whole long fight (the hot four minutes: turning away by role, the research's item); Black
Prince (doubt from the partial reply, her guns held until made out); the AI's night by the light, not the sun's angle,
with the dawn case's visibility to the record; phosphorescence as a sight cue; the moonless hours for raiders. The user
(2026-10-30): the torpedo's aim first, then the rest in that order.

**Q12c, the torpedo officer's judgement (2026-10-30).** Read first: the aim took the side's track for the target's
course and speed, and by the launch that track is good (a probe: 0.2-1.6 m/s off at 2.5 km, after the mounts trained),
so a spread went where a perfect solution sends it: 33-36% of torpedoes hit from 0.8 to 2.5 km, about the most a fan
of four can. The record's officers had no plot: the torpedo director took the target's course from her inclination and
her speed from her bow wave, by eye.

- **The judgement** (`TorpedoSystem.Judged`): the track gives where she is; her course and speed are the track's off by
  one draw a spread (`Pending.CourseZ`, `SpeedZ`), 1-sigma 15° and 1.5 m/s (`TorpedoJudgeCourseSigmaDeg`,
  `TorpedoJudgeSpeedSigmaMs`: the middle of `computing.md` §2.1's ±10-20° and ±2-4 kn by eye [EST]), grown by
  √(1 + (4 / margin)²) on the margin her shape was made out at the last sighting (`Contact.Shape`, new, hashed; 4 the
  class's margin, `TorpedoJudgeMargin`; floored at 0.3, a ship seen by her lights alone) and by the crew's experience as
  the gun crews' operators (`GunneryCrew.Operator`: green twice a trained crew's). A day target at 2.5 km is made out
  at a margin of about 5 (the errors ×1.25); the night's launches at a median 2.1 (×2.1: course 31°, speed 6 kn).
- **Firing while turning** (`TorpedoFireTimingS` 0.5 s [EST: a reaction]): each torpedo's course errs by her rate of
  turn times the moment it leaves the tube against the sight's coming on, beside the gyro's 1°.
- **Under fire: no term.** `crews-under-fire.md` (finding 8) found no measurable effect on accuracy beyond casualties,
  damage, smoke and the light, which reach the eye and the crews already (the user's "under fire" is those).
- **The dev's launches** (`-torpedo`, `Launch(truth: true)`) stay aimed at the truth.

| Measure | Before | After | The record |
|---|---|---|---|
| a spread of 4 at QE at 15 kn, beam on (`shipgen torpedo`): 0.8 / 2.5 / 3.5 / 6 / 8 km | 33 / 36 / 33 / 21-28 / 24% | 24 / 18 / 3.4 / 4.9 / 0% | close: often; far, at a ship that turns: a few % |
| at 18 kn: 1 / 2 / 3 / 5 / 8 km | 31 / - / 23 / 19 / -% | 18.5 / 15 / 6.6 / 5.7 / 0% | |
| Tsushima, Japanese torpedoes | 1.4 for 0.3 (21%) | 1.3 for 0.2 (15%) | 54 for 4-6 (7-11%; mines aside) |
| Southampton, British | 12.9 for 2.9 (22%) | 12.2 for 2.0 (16%) | 1 for 1 |
| 4th Flotilla, British | 27.8 for 9.0 (32%) | 24.8 for 5.0 (20%) | ~14 for 1 (7%) |
| Black Prince, German | 6.0 for 2.7 (45%) | 5.5 for 1.3 (24%) | none known to hit at the Jutland's night |
| Dover Strait, German | 6.0 for 1.2 (20%) | 6.2 for 0.8 (13%) | (G42, G85 sunk; Broke's torpedoes missed) |

(`night-history --runs 40`; the dip at 3.5 km is the setting: beyond the fast setting's reach the slow one and a run
twice as long. Past 3 km the solution's error grows with the run, so a lone target is seldom hit; a line gives the misses
a second ship to find.)

**What it says:** the day comes into the record's shape (close often, far seldom). The night falls by a third to a
half but stays about twice the record at the 4th Flotilla: at 0.8 km the judged aim is off by some 200 m against a
fan 300 m wide, and 70% of the night's hits are on the ship aimed at. A larger judgement error isn't sourced (it would
need ~3×); what's left is the targets' own turning (the German line turned away as the boats came in) and the boats'
haste under fire, which the record tells in words, not numbers. The British at Southampton fire 12 torpedoes where the
record has Southampton's one: the light cruisers' fire discipline, not the aim.

- **Not done, for the user:** the matchup's torpedo curve (`MatchupTorpedoHitM` 900, `MatchupTorpedoHitMax` 0.4: 35% at
  3 km) is the old bench's; refitted to the new one (350, 0.2) destroyers stop being taken for a threat to a battleship,
  so the roles, the flotillas' attacks and the turn-away (14 tests) go with it. Left as the AI's belief, optimistic as
  the navies' pre-war estimates were, until the user decides: refit it with the attacker threshold, or keep it.
- A test's force attack (`A_force_told_to_attack_hands_it_to_its_divisions`) needed 90 minutes, not 40, for its task to
  end: the dreadnought no longer sinks in the first attack, so the boats come round again.
- Sim 877 passed (a smoke cost test flaky under load, passes alone), game 207 passed.

**Q12d, the AI's night by the light (2026-10-30).** The AI's night (doctrine's `NightAction` and `NightAttack`, the
commander's night coming, night's close order) was the sun 6° down, a switch on the clock. Now `AiNight.At`: the sky at
the place darker than `NightSkyCdM2` 3 cd/m² under the weather's cloud (`NightLight.At`; a clear sky's with the sun 6°
down is 2.8, so a clear night falls as before; under overcast about 15-20 min sooner, at the sun ~2.5° down). One
helper for the four callers; `NightSunElevationDeg` is gone.

- **Not on how far a ship is seen:** a threshold on the reference ship's reach (`SightModel.SeenToM`, light and haze
  together) would make the day's misty battles night (Heligoland Bight 5 km, Cape Sarych and Åland 8 km) and their
  commanders avoid action. The mist reaches the AI through what it sees and its matchups.
- **The dawn's mist** (`night-12th-flotilla`): 8 → 6 km (about three miles: the record's "three to four miles" at
  daylight, Malaya's "no more than two"). The boats now see the line in 40 runs of 40 at 5.3 km (9 of 40 at 8.4 km);
  the Germans see them first at 6.8 km and open at 5.0 km.
- **The dawn's attack still doesn't come** (0.3 torpedoes a run, from 3.6 km). It's night to the AI throughout; the
  British commander withdraws the flotilla as outmatched on sighting the line, because the attack isn't worth it by the
  matchup (`FlotillaAttacks.Worth`): in that half-light (sky 0.5 cd/m², 6 km of mist) the line makes an M class out at
  7.0 km, so the run in from there to 2.7 km costs 1.7 boats of 8 by its reckoning, and its confidence is 0.21, under the
  commander's bar (`shipgen matchup m_class:7 deutschland_1906:5 --sky 0.5 --vis 6000`; in the dark, 0.01 cd/m², 209).
  The record's boats went in by their orders (to attack the battle fleet by night), and the Germans doubted and held
  their fire (Q12's doubt, next). Doctrine's `NightAttack` is yes or no, and its flotillas still clear the day's bar:
  whether a night attack goes in against the odds (a bar of its own) is a doctrine choice for the user.
- **The other cases** barely move (Tsushima's Japanese first salvo 5.8 → 5.2 km, 1.0 torpedoes a run against 1.3).
- **Cost:** the admirals' think 31.6 → 34.9 µs (the 4th Flotilla's play): the sky asked once a group, no cache.
- Sim 877, game 207 passed (`CommanderTests`' dusk case asserts the sun under 5° down, not the old setting).

**Lit ships turning away by role: measured, not built (2026-10-30).** Its premise (Q12a: Southampton's fight runs long)
doesn't hold. Played headless (`shipgen scenario night-southampton.json --minutes 120 --every 2`), the fight has the
record's shape: some 250 shells in the four hot minutes (21:56-22:00), a second burst to 22:12, then quiet, about 16
minutes in all against the record's 15. What's high is the hit rate: the Germans 255 hits of 513 shells at a median
1.9 km, against the record's 26 (Southampton 18 `[UNCERTAIN]`, Dublin 8). Turning lit ships away by role wouldn't touch
it, and the research's direction (lit small craft turned away, big ships seldom: `night-fighting.md` §5.4 point 5) would
keep Southampton fighting longer if anything. Left as written (every ship's default "turn away") until the hit rate
is decided.

**The night's hit rates, for the user.** Every night case hits at 40-87% of shells fired at 0.6-2 km (Southampton's
Germans 47%, the British 73%; the 4th Flotilla's British 80%, the Germans 58%; Black Prince 87%, her Germans 59%).
The record has no rate but Southampton's (about 26 hits where the sim makes 255 from as many shells) and says observation
was "nearly impossible" for the searchlights' glare and the guns' flash (`emergency-eyeball.md` §2). A fire-control item
(local control by eye at night: the flash, the glare, laying on a lit target), not a night-lights one: to decide.

**Q12e, whose she is, by the light (2026-10-30).** The user's Black Prince step: she holds her guns until she makes them
out. A contact was known for a foe once her type was made out ("her colours, her build"), even in the dark, where no
colour is seen. Now the margin whose she is is made out at runs from the type's (2) by day to the class's (4) in the
dark, on the log of the look's sky across the eye's mesopic range (`SightColoursCdM2` 5 to 0.005 cd/m², CIE 191:
colour fading between): by day as before, at night her silhouette's class tells it (`Sightings.SideMargin`). The
record's Port Arthur ("boats showing lights were naturally taken for our own") and Black Prince taking the Germans for
her own.

- **Black Prince isn't sunk still** (0.1 of 1). She holds her fire now until the Germans give themselves away, which
  they do first: star shell "to find" at 0.9 km, then their fire at 0.7 km (hers at 0.6). From there she fires 100
  shells for 87 hits and the Germans 77 for 45; their beams 2 ship-minutes in 19 runs of 40 (the record: Thüringen's
  searchlight on her and four battleships' fire for some 15 minutes). The rest is the night's hit rates (above) and the
  German volume. The partial reply as doubt adds nothing to it: answered, she's already held as a friend
  (`TakenForFriend`, her guns held), which is what doubt does.
- **Moved:** Tsushima's Russian hits 100 → 72 (their sweeping ships take the boats for unknowns longer), the Japanese
  85 → 77; the 4th Flotilla's British torpedoes 24.7 → 27.2 for 6.1 hits; the others within their noise.
- Sim 877, game 207 passed.

## Decided (the user, 2026-10-29)

1. **No friendly fire for now.** Own ships stay known (no CPU spent on seeing friends, as today). Captains may still be
   cautious about opening fire on unidentified targets for fear of it, by doctrine and temper (Q6); the friend just
   never turns out to be there.
2. **Searchlights are placed by the designer.** Shipgen produces the data itself (no human needs to intervene); no
   placeholder phase (Q3).
3. **Star shell is a fourth `ShellKind`** in the same magazines and outfits (Q5).
4. **Cloud cover in the weather now,** one per scenario like the visibility (Q1).
5. **The order as written:** Q0-Q2 first (the dark, and the lights we already have: fires and flashes), then
   searchlights, star shell, friend or foe, the AI.

## Not here

- **Radar** (`fire-control.md` F9, the cut after 1925), aircraft flares, night vision devices.
- **Signalling by searchlight and lamp** for orders: `command.md` 5.1e (signals). Q6's challenge uses the same lamp.
- **Collisions and ramming** (4.11): night makes them likelier, and Q7's night formations will want them.
- **Fatigue** as its own system; **flashless propellant** with the ammunition system.
- **Local weather** (fog banks, squalls): the weather features after the smoke field.

## Research request

Arrived 2026-10-29 as `research/simulation/night-fighting.md` (folded in above, after Q9). Filed in
`research/REQUESTS.md` (2026-10-29, "Night fighting, 1890-1925"): searchlights by navy and year (sizes,
intensity, positions, control, useful ranges, doctrine), star shell (who had it when, by calibre, intensity, burn time,
fall rate, use), night glasses and lookouts, the night cases' ranges and outcomes, challenges and recognition, flash and
funnel glow, wake and bow wave by night.
