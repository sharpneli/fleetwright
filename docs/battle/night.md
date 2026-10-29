# Plan: night fighting (a 4.5 deepening)
Status: **drafted 2026-10-29**; **agreed with the user 2026-10-29** (decisions 1-5 at the end); Q0 and Q1 done 2026-10-29
Updated: 2026-10-29

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
  keyed on the sun 6° down (`NightSunElevationDeg`). Torpedo tracks are seen at 2 km by day and 400 m by night
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

### Q8 The crew at night

- Searchlight crews in the crew book (Q3). The searchlight term for green crews (`crew.md` N3's unbuilt row, CUF R3).
- A ship firing rapidly in the dark dazzles herself (Q2). Fatigue after a day's fighting stays its own later system
  (no lumped factors).

### Q9 The player's hand

- Orders, in the input grammar: searchlights (burn, douse, by doctrine), illuminate a contact with star shell or a
  beam (an armed verb), hold fire or weapons free, challenge. The player's orders beat doctrine.
- What the map shows: lights as marks (a beam's bearing, a flare's patch), flash and light contacts, unknowns, "you're
  showing a light" on the card. Mockup first, then the port, per the UI process. No popups.

### Q10 The look

A visuals plan of its own (`docs/visuals/night-lights.md`) when Q4-Q5 are in: searchlight beams as light scattered in
the haze and smoke (the beam visible from the side), the lit patch on the sea and on the target (a spot light in the
shared light block), star shell flares with their smoke trails and the sea lit under them, the moon path on the water
(the sea's shader has the moon). Placeholder looks until then: a line for a beam, a disc for a flare. Realistic first.

### Q11 The coarse tier

Lights in coarse's minute look, cheap: a light's on/off and its illuminance per minute, star shell as a minute's
flare. The parity bench gets night cases. (Night's coarse gap is the AI's minute cadence, `coarse.md` J5; this pass
doesn't change that.)

### Q12 Fit and check

Against `night-history` and the night scenarios. Restated anchors and what moved, for the user.

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

Filed in `research/REQUESTS.md` (2026-10-29, "Night fighting, 1890-1925"): searchlights by navy and year (sizes,
intensity, positions, control, useful ranges, doctrine), star shell (who had it when, by calibre, intensity, burn time,
fall rate, use), night glasses and lookouts, the night cases' ranges and outcomes, challenges and recognition, flash and
funnel glow, wake and bow wave by night.
