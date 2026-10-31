# Plan: the aims

Drafted 2026-10-31 (the user: "the break out, the invasion and commerce raiding should be fine for the first release;
once the systems are in place we'll investigate which exact scenarios we want"). N1 of `docs/missions/scenarios.md`,
the heart of its wave 2, and the first of the objectives (`docs/missions/plan.md`, 6.1). Bombardment was weighed and
dropped: with no shore targets and fire control that only aims at ships, any version now would be a hack.

**The future (the user, 2026-10-31):** these aims are what the global map's invasions will be driven by: if the AI
can run these scenarios, it can work with top-level command goals too. So every rule is general, the aims come as a
higher command would give them (commands), and the commander pursues them by his ordinary rules.

**The user's answers (2026-10-31):** (1) whether a ship counts on entering the zone or after a stay there is the
aim's setting; (2) a scenario isn't all won or all lost: aims score in part and weigh, and getting home can be a
bonus aim beside the main one ("better sometimes to reach home damaged than squeeze the last merchant out of the
enemy"); (3) the aims come now, before the balance round's BL3 and BL4.

## Status

| Step | State | Left |
|---|---|---|
| A0 Investigation | done 2026-10-31 ("A0: what the code says") | |
| A1 Aims as data, their state, the judge | done 2026-10-31 ("A1, as built") | |
| A2 Reach: the side that wants a place | done 2026-10-31 ("A2, as built") | |
| A3 Stop: the side that wants to prevent it | done 2026-10-31 ("A3, as built") | |
| A4 Sink merchants: the raider, the merchants, the hunters | done 2026-10-31 ("A4, as built") | |
| A5 The UI: aims, progress, zones | done 2026-10-31 ("A5, as built") | zones on the launch screen's opening |
| A6 Benches and balance | benches done 2026-10-31 ("A6, as built") | the user's scenario investigation |

## What's there

`Sim.Ai/Commanders/Aims.cs` has one kind, `DestroyEveryone`. The judge (`Sim.Missions/ScenarioEnd.cs`) asks
`Aims.Met` for Decided and Done, and `SideResult.AimsMet` carries it to the result. The commander
(`CommanderLayer.cs`) already does most of what the new aims need: he has a main body the others support (heavy units
join or cover it, scouts scout ahead, flotillas screen); he declines a stronger enemy and goes home; he searches "the
line between the enemy's last estimate and its likely goal" (`command.md`); auxiliaries go home when idle. The tasks
include GoTo, Patrol, Search, Intercept, Cover and Screen. Merchants are auxiliaries (`Roles.Derived`: no main battery)
and steam scenario routes. Striking (`striking.md`) makes a hopeless, cornered ship give up, and a ship given up isn't
`Afloat`, so the aims count her lost.

What's missing: aims other than destroy everyone; a home of the unit's own (a convoy's home is its beach); and
**anything that runs from a warship**. Doctrine's Engage is Fight or CarryOn, and a merchant on her route steams on
into the guns.

## The aims

Three new kinds join `DestroyEveryone`. Each is **Open, Met or Lost**, the state `Aims` reads in one place for the
judge, the commander and the UI, and has a **score** from 0 to 1 (how much of it was done). A side's list is in the
order it's wanted (below), each aim with a **weight** (`"weight"`, 1 if not given); the side's result is its aims'
weighted score.

- **Destroy everyone** scores the share of the enemy's tonnage lost.

- **Reach** (the breakout, the invasion): at least N of the named ships arrive in a zone. A zone is a circle, `"at"`
  and `"km"`. The named ships are a unit's (its name) or a list of units. A ship has arrived from the first time she's
  afloat inside the zone, or with `"stay"` (minutes) once she's been afloat in it that long without leaving (landing
  troops takes hours, and the defender gets his chance at the beach). An arrival stays (a transport sunk after
  landing her troops landed them; Goeben in the Dardanelles got away). Scores arrived / N, at most 1. Lost when it
  can't be met any more (arrived + still afloat < N), or the side's commander has given up. Without named ships it
  means every ship of the side: the raider's way home, `{ "reach": "all", "zone": ..., "of": 1, "weight": 0.5 }`.
  ```json
  "aims": { "Germany": { "reach": "Mittelmeerdivision", "zone": { "at": [40.05, 26.2], "km": 15 }, "of": 1 } }
  ```
- **Sink** (commerce raiding): sink, or make strike, a tonnage of an enemy's merchants (full load, as the results'
  tonnes). The kind is the truth's `Sightings.TypeOf` (merchant), never how a ship was made. Optionally a hunting
  ground, a zone the raider knows to search. Scores the tonnage sunk over the target, at most 1: every merchant counts.
  Lost when the side has given up, or the enemy's merchants afloat can no longer make up the rest.
  ```json
  "aims": { "Germany": { "sink": "merchants", "tonnes": 20000, "where": { "at": [8, 80], "km": 600 } } }
  ```
- **Stop** (the other side of both): the named enemy aim (by its side; its first if it has several) is Lost. Met once
  it is; Lost once it's Met; scores 1 less that aim's score, so three transports of six landed is half a win for each.
  One kind for both counters, so the convoy's escort and the hunters of a raider are the same aim.
  ```json
  "aims": { "Britain": { "stop": "Germany" } }
  ```

Destroy everyone stays the default for a side not listed. The judge's Decided becomes "every side still afloat has
every aim decided" (Met or Lost), so a convoy that lands ends the fight's reason, not the fight: the end is still
Done or Quiet, and never a clock in the middle of a fight (`no-time-cutoffs`).

**Arrivals are state.** The rest is read from the world as it stands, but "arrived" is a latch, so it lives with
the commander (AI state: hashed, snapshotted, replayed), updated at his think, and the judge reads it. Nothing per
frame; a fixed bit per ship, nothing allocated once grown.

**Wanted in order.** The commander pursues his first aim not yet decided; once it's met or given up, the next. A
raider with Sink and then Reach home hunts until she has her tonnage, or until the commander gives up the hunt by
the rules he has (his losses past doctrine's break off, a stronger enemy he declines, his magazines low), and then
makes for home with what she's done: the score keeps her merchants either way, and getting home adds its weight. A
convoy's Reach first and the escort's Reach home second sends the escort home once the troops are ashore.

**Not in this plan:** a deadline (`"by"`), the grade's words (decisive, marginal, defeat: 6.1's, read off the score
later), Hold, Protect a named ship, Destroy narrowed to a group. Each is a kind more of
the same shape when a scenario wants it.

## The commander

The judge is the cheap half. A side's AI must want its aims, or the headless runs that balance a scenario say
nothing. Each behaviour reuses one the commander has.

**Reach (A2).** The reaching units are the main body, whatever their tonnage: the rest of the side take their roles
about them as they do about a battle line now (a convoy's escort screens and covers it, the cruisers scout ahead). A
reaching unit's **home is the zone**: a home per unit, the side's if none, so withdrawing, declining, breaking off and
an idle auxiliary going home all head for the beach or the strait, and only the units not reaching go to the side's
home. Its task when nothing's about is GoTo the zone. When he declines a stronger enemy, the reaching units carry on
for the zone (that's what declining means to them) and the others cover them, by the rules he has. Its ships stop on
arriving (a beach, an anchorage) unless the scenario says otherwise.

**Stop (A3).** He knows what the enemy wants (the scenario's intelligence: a side's brief). With no contact his main
body searches the line from the enemy's last estimate to the zone, as `command.md` has it, so he waits across the
enemy's way; or patrols the zone if he has no estimate. With contact, the enemy group holding the named ships is the
one he sends after first. His captains put the named ships (in their picture: what they take a ship for) ahead of
their rank in the choice of target: a transport over her escort. Against Sink, the raider is what he hunts: his
units search the hunting ground and intercept.

**Sink, and the merchants (A4).**
- *The raider:* with no contact he searches the hunting ground; on a merchant held, he intercepts (the matchup's
  answer is plain) and fights; a warship held, the balance as now: fight if he's the stronger, else decline. His
  captains put merchants ahead of their rank while the aim is Open.
- *The merchants:* **a ship with no main battery runs from an enemy warship in sight**: a captain's rule, by what she
  is, not by side or scenario (`no-provenance-special-cases`): she turns away from the threat at her best speed, and
  back to her route once out of sight for the resume time (`TaskResumeS`). Caught (hopeless, cornered, the enemy near:
  striking's rule), she strikes, and counts lost. Stopping her to sink her by charges and prize crews are striking's
  "Not in this cut".
- *The hunters:* Stop, as above.

## The UI (A5)

Per `docs/ui-language.md`, drawn with `Ui/Pieces.cs`:
- The HUD's aim line (`MapScene.Hud.cs`, now "Destroy everyone") says each aim and how it stands: "Reach the
  Dardanelles: 0 of 1", "Sink 8,200 of 20,000 t of merchants", "Stop Germany's: open".
- A zone is a ring on the map, the side's colour, its name beside it; a hunting ground dashed. Light, as the map's
  marks are.
- The launch screen lists each side's aims under its character; the result says each aim Met or Lost, with its
  numbers.

## A0: what the code says (2026-10-31)

- **The AI doesn't know the aims.** Only the judge reads them (`Scenario.AimsOf`, `ScenarioEnd`). They go to the AI
  as commands at load, as homes and doctrine do (`ScenarioLoader.Apply`, `AiCommand.Home`), so a replay carries them:
  one command an aim (kind, the enemy side it's about, zone packed as a lat/lon, radius, of, stay, tonnes, weight) and
  one a named ship. The AI keeps them (`Commanders/Aims.cs` becomes the book: the aims by side, each ship's arrival,
  each aim's state and score), hashed and filled into the snapshot, so the HUD reads live state and the judge reads
  the AI's book (one implementation; a judge with no AI knows destroy everyone only).
- **Named ships are ships**, resolved at load from the unit's ships, never a group id: units divide and merge, and a
  divided convoy is still the convoy (`no-provenance-special-cases`).
- **Homes are a side's** (`AiSystem.homes`), read in three places: the captain going home and the cripple's escape
  (`CaptainLayer.Fire.cs` `HomeThink`, `Escape`) and the task layer's Withdraw home (`TaskLayer.cs`). One
  `HomeOf(side, ship)` replaces them: the zone of the first open Reach naming her, else her side's.
- **The main body** is the unit with the most capital tonnage, auxiliaries and fallen-out units skipped
  (`CommanderLayer.Command`); the reaching units go first in that choice (auxiliaries too, a convoy being them).
  Heavy units **join** a main body that's a group, putting battleships in its line: about a convoy they **cover** it
  instead, as they already do a ship alone. The balance leaves auxiliaries out, so a convoy's strength is its escort's.
- **Declining** sends every unit home (`Retire`). With an open Reach first, the reaching units' home is the zone, so
  they carry on; the others **cover** the reaching units rather than leave them. The search after a lost contact
  (`Search`, then given up) is skipped for a Reach side; a raider searches its hunting ground rather than where the
  enemy was last held.
- **Stop:** the groups go gravest first (`Assign`); a group holding the kind the enemy's aim needs (its named ships'
  kind as the picture types them; merchants for Sink) goes first. With no contact the main body searches ahead on its
  course (`IdleTask`); for Stop it searches across the line to the zone.
- **Targets:** a battery's pick scores each contact by range to come, the fire on her, her size (secondaries only),
  how done for and whether she still matters (`Pick`). The wanted kind divides that score by a setting, for the main
  battery and the rest alike.
- **Nothing unarmed runs.** Doctrine's Engage is Fight or CarryOn; a merchant on her route steams on. The cripple's
  `Escape` (away from every enemy held near, weighted by range, bent toward home) is the course to reuse: a ship with
  no main battery and an enemy warship held within `FightCompareM` steers it, and goes back to her route or task once
  clear (`TaskResumeS`). Whether striking's "hopeless" reads right for an unarmed ship is A4's check, and so is the
  matchup against an enemy with no guns (`Matchup.cs`: "0 with no guns").
- **The UI:** the HUD's aim text is the scenario's (`MapScene.AimsText`); the result has `AimsMet` only
  (`SideResult`, `SideEnd`, `MapScene.Result.cs`, `ScenarioPlay`). Both take the book's states, scores and progress.
  The launch screen doesn't show aims yet.
- **No scenario changes ending.** Every shipped file is destroy everyone, and the judge's Decided reads the same for
  it.

## A1, as built (2026-10-31)

- `Commanders/Aims.cs`: `AimKind` Reach, Sink, Stop beside destroy everyone; `Aim` with its zone, of, stay, tonnes,
  about and weight; `AimState`; `AimReading` (state, done, of, score). `AimBook` (`AiSystem.Aims`) holds every side's
  aims by command (`AiCommand.Aim`, `AimFigures`, `AimShip`), updated at the commanders' think (once a game minute: a
  stay is measured to the minute, and a zone should be a couple of km across or more so a ship steaming through is
  seen in it), hashed, and filled into the snapshot (`Snapshot.Aims`, `AimView`).
- **Read live**, so the judge's ends for destroy everyone are what they were: only the arrivals (and the stay's
  clocks) and the roster of ships seen are kept. Tonnage lost comes from the roster (a ship gone from the sea is still
  lost), tonnage afloat from the store as it is, so a reading before the first think has everything afloat.
- **Scores:** destroy everyone the share of the enemy's tonnage lost (Coronel's two seeds: Britain 0.29, Germany
  0.28); Reach arrived / of; Sink tonnes / target; Stop 1 less its aim's. `SideResult.Score` and `SideEnd.Score`
  weigh them; the play bench prints it.
- **The format:** a side's aim or list of them (`Scenario.ParseAims`); `ScenarioAim` keeps a Reach's unit names, the
  loader (`ScenarioLoader.Aims`) resolves them to the ships placed under those units at any level, or every ship of
  the side for `"all"`, an `"of"` not given being all of them. A name not in the scenario is an error; fewer ships
  placed than `"of"` (a setup struck them) a warning.
- **The judge:** Decided as before (every side afloat has met every aim). Done takes a side whose aims are all
  decided (met or lost) as done once its ships afloat are out of touch: a convoy landed ends the reason, not the
  fight. A side whose aims are all destroy everyone and met is done at once, as before.
- `AimsTests`: the format and its errors; a Reach met (and Done at once, nobody in touch) and lost; a stay of 3
  minutes met and one of 20 not by a ship passing through, the arrival in the state hash; Sink's tonnage in halves,
  struck counting, a weighted score. The suite: 924 passed.

## A2, as built (2026-10-31)

- **Homes** (`Sim.Ai/Homes.cs`): one place for where a ship goes out of the fight, read by the captain going home,
  the cripple's escape, the task layer's Withdraw and the judge's Done. A ship's home is the zone of her side's first
  open Reach naming her that she hasn't arrived for; else the last zone she arrived at (a landed convoy stays at the
  beach, a breaker in her strait, until another aim wants them); else her side's. There means within half a zone's
  radius (the side's 10 km at most), so a column stopped about its guide lies inside.
- **The commander** (`CommanderLayer`): the aim he pursues is his side's first still open (`AimBook.Pursued`). With a
  Reach, his main body is the unit with the most ships reaching (a convoy's auxiliaries too), and each unit reaching
  goes for the zone (`Reach`): by GoTo while he accepts battle (doctrine's fights on the way, the task resumed after),
  by Withdraw while he declines or has broken off, and in the zone Withdraw stops it. A route the scenario gave it is
  its way there before the enemy's met. His other units take their roles about the main body; heavy units cover a
  convoy rather than join its line; declining or broken off, they cover those reaching rather than go home without
  them (`CoverReaching`); those reaching aren't sent after the enemy (`Assign`); no search back after a lost contact.
  Every aim decided, he goes home (`CommanderReason.AimsDecided`), those at their zones staying.
- **Withdrawals run the cripple's way** (`TaskLayer.Withdraw`, `CaptainLayer.Escape`): away from every enemy held
  near, weighted by range, bent toward home as the nearest falls back, its course given again past `WithdrawTurnDeg`
  (10°), instead of running straight away from the nearest. Found by the breaker's test: a 29-kn light cruiser
  declining two 21-kn dreadnoughts ran from them for five hours past her strait (they chased from between her and
  it); now she goes round and is in at 75 minutes. Every withdrawal changes with it. The balance panel (`shipgen
  balance --seeds 8 --fc-runs 0`), A1 against A2, ships lost a side: Dover 0.4 v 0.9 → 0.4 v 1.0; Coronel 1.3 v 1.1 →
  1.3 v 1.0; Cape Sarych 1.3 v 0.9 → 1.8 v 0.9; the prizes' war 0.9 v 0.6 → 0.8 v 0.5; Åland 4.3 v 1.8 the same;
  Heligoland 4.4 v 14.8 → 4.4 v 15.1; Dogger Bank 6.9 v 6.4 the same; Tsushima 2.6 v 2.3 the same; the Jutland (4
  seeds) 33.8 v 50.0 → 36.8 v 49.3. Within about twice the record: 3 of 14 sides → 5; the record's loser lost more in
  25 of 44 seeds → 24. About the seeds' spread throughout: nothing moved that the balance round must chase.
- Found on the way: a ship stopped coasts on for hours (about 10 km from 8 kn), out of a small zone: arrivals are
  kept, so aims hold; logged in `bugs.md` ("The slide", the last item).
- `AimsTests`: an unopposed convoy with no route sent to its zone, stopped there; the breaker declining and still
  getting in; the escort keeping by the convoy, then home once it's in, the convoy staying. `TaskTests`' withdrawal
  now checks the course (away from the enemy, bent for home), not the run order.

## A3, as built (2026-10-31)

- **What a side wants sunk first** (`AimBook.Wanted`, at each update): for a Stop of a Reach, the kinds (as
  `Sightings.TypeOf` reads a class) of the named ships still on their way, so a convoy's transports or a breaker; for
  a Sink, merchants (A4's raider reads the same); else none. A captain knows a ship by what his navy takes her for (the
  picture's type), never by her name.
- **The commander** (`CommanderLayer`): an enemy group holding a wanted ship goes first in `Assign`, and a unit sent
  after it intercepts its biggest wanted ship. A Stop side's units not on a scenario route take up their roles at once
  (they don't wait to meet the enemy); its main body's idle task is `Across`: a search where the enemy would be now
  steaming for the zone from where he was last held, at the speed held (never past the zone), or the zone itself if
  he's never been held, given again each time it's through. A Stop side doesn't give up a lost enemy (no `GaveUp`) while
  the aim's open: it searches across his way again.
- **The captains, reasonably** (the user: "if a captain is clearly threatened by the escorts he probably wants to
  fight them, but he'll try to go around if he can"; `CaptainLayer.Fire.cs` `Want`, `Threat`, `Around`):
  - A wanted contact counts `WantedShare` (3) times nearer in a battery's pick, the main battery and the rest alike.
  - Any other counts as much nearer by **how much she endangers him**: `1 + (WantedShare - 1) × threat`, the threat
    `1 - exp(-outs / WantedThreatOuts)` (0.15), the outs what `Matchup.Threat` expects her to put out of him over an
    hour's action (`WantedThreatMin`, her guns on him, at most her rounds, by the lines' own fire, `Matchup.Guns`) and a
    salvo of her torpedoes in their reach (`Matchup.Torpedoes`). At 8 km, against a battlecruiser: a dreadnought 0.43
    outs (threat 0.94), a light cruiser 0.03 (0.2), a tramp none. So a clear threat is fought on even terms with the
    transports (the nearer by range to come first) and a weak escort is passed over for them.
  - **Round the escort:** on a wanted target, his fight course is bent away from every other enemy near that endangers
    him, each pushing by her threat over her range squared (as hard as the course pulls at `WantedAvoidM`, 8 km), at
    most `WantedBendDeg` (60°) off it.
  - In the tests' case (a battlecruiser, the tramps 12 km east, the escort 8 km north) with a light cruiser escort she
    shoots the tramps from her first pick; with a dreadnought she shoots the dreadnought, turns round it for the tramps
    (its range opening 8 → 10 km as theirs closes 12 → 9) and shifts to them once they're the nearer.
- Every new rule is gated on a side's aims: the shipped scenarios (destroy everyone) play as before.
- `AimsTests`: the threat's numbers; the pick with each escort and the going round; the Stop side's battlecruiser,
  with no orders, searching the zone, finding the convoy on its way in, sent after the tramps (`CommanderWhy.Contact`):
  the escort came between them, so she fought it at point blank while the tramps were out of reach (made out as
  merchants at 26 km, in reach under 18), then turned her guns on the tramps, and the convoy was stopped (One struck)
  with the escort still afloat.

## A4, as built (2026-10-31)

- **Where a side searches** (`AimBook.SearchZone`, replacing A3's stop zone): across the enemy's way to his Reach's
  zone (A3), or a Sink's hunting ground, the raider's own or, for a Stop of it, the one her hunters know she works. A
  side with one takes up its roles at once (its auxiliaries aside: they keep their routes), and its main body's idle
  task is the search there (legs as long as the zone is wide, 20 to 60 km). Losing the enemy, a raider or her hunters
  search where he was last held as before, and then the ground again rather than give up (the Stop of a Reach skips
  that search: across the way at once).
- **The raider:** her Sink wants merchants first (A3's `Wanted`), so her commander sends her after the group that has
  them and her captain's guns go on them; a stronger warship and she declines by the balance as ever, making for home
  (her way home a Reach after the Sink: `Homes` already sends her to its zone).
- **The merchants run** (`CaptainLayer.Flee`, `FleeThink`): a ship with no main battery and no torpedo tubes (what
  she is: `Sightings.TypeOf` merchant), alone (not in a line: a convoy's admiral takes the line away), meeting an enemy
  warship near (`FightCompareM`; a known foe not taken for a merchant, an unknown counting as one) flees by the
  cripple's way out (`Escape`: away from every enemy near, bent toward her home as they fall back) at flank speed,
  whatever her doctrine, instead of taking up Fight; her light guns fire at will as she goes. Once no warship's been
  near for `TaskResumeS` (5 min) she's back on her route if it has waypoints left, else her course and speed. A task
  she had waits, as for a fight (`TaskLayer`: `Captains.Fleeing`). Any order ends the flight, the player's included.
- **Striking:** a ship with no main battery counts as unable to hurt (`CantHurt`), so a merchant hit and cornered
  gives up by her doctrine. Undamaged she doesn't (the captain's call is for ships hurt; stopping one by a shot across
  her bow and prize crews stay striking's "Not in this cut"). In the tests a tramp hit by a light cruiser sank before
  her call fell due (abandoned as she foundered): physical, left as it is.
- Found on the way: the Stop side's commander re-ordered its tramps home every minute (A3's deployment caught
  auxiliaries, and its side had no home); auxiliaries aren't deployed now, and one whose withdrawal found no home holds
  what it has.
- The shipped scenarios: only the perf benches (`ladder-4-world`, `ladder-5-world-2000`) carry merchants, so the
  battle scenarios meet the flight nowhere; the striking change reaches only ships with no main battery.
- `AimsTests`: the raider, no orders given, searches her ground, finds the three tramps on their routes, sinks her
  tonnage (one, 9,471 t) and gets home; all three tramps ran, the two that got away back on their routes; the hunters
  (an armoured cruiser, no orders) search the ground, come on the raider, she declines (at 40 min) and is home at 130
  with her way home met and her hunt still open.

## A5, as built (2026-10-31)

- **One wording** (`Map/AimWords.cs`), used by every screen: as the scenario gives an aim ("Reach the anchorage with 3
  of Convoy, to stay 30 min", "Sink 25,000 t of merchants in the shipping lane", "Stop Germany", a weight if not 1) and
  as it stands from the snapshot's `AimView`s ("Reach the anchorage: 0 in, 3 wanted", "Sink merchants: 8,200 of
  25,000 t", "Stop Germany: open", then "· met" or "· lost"). A zone may have a `"name"` in the file (`ScenarioAim.ZoneName`;
  a zone's keys are checked now: at, km, name); without one it's named by the units bound for it.
- **The top bar** (`MapScene.Hud.cs`): the player's aims as they stand, in order, and the end's time; made again only
  when a figure changes (`AimWords.Key`, nothing allocated a frame). The briefing says them as given ("YOUR AIMS").
- **The map** (`MapScene.Zones.cs`): every side's zones (a side knows what the enemy's after), a Reach's a ring and a
  hunting ground dashed, in the side's colour (yours, the enemy's; brass watching), light, named above.
- **The launch screen**: Conditions' "The aims" row says them as given. Not yet: the zones on the opening's plan.
- **The result** (`MapScene.Result.cs`): each side's aims as they stood at the end, met green, lost red; the count's AIMS
  column the side's score where aims score in part; the verdict, with no single side all met, goes to a side whose score
  is ahead by `ScoreAhead` (0.25), else a draw (the old tonnage rule for destroy everyone alone).
- Checked by readbacks (`-screenshot -ui`): the convoy bench's map and top bar, the raider's briefing (both aims, the
  dashed ground), the convoy's result, the raider's Conditions tab.

## A6, as built (2026-10-31)

- **Three benches** (`"test": true`, in the launch screen's Tests), each played `shipgen scenario FILE --play --seeds 8`:
  - `test-breakout.json`: two German light cruisers for "the gap" (one through is enough) past two British light
    cruisers and two dreadnoughts warned of it.
  - `test-convoy.json`: four tramps for "the anchorage" (three in, staying 30 min) with two light cruisers and four
    destroyers, against a battlecruiser and two light cruisers.
  - `test-raider.json`: a light cruiser to sink 25,000 t in "the shipping lane", then home (weight 0.5); six tramps on
    routes through it; an armoured cruiser and a light cruiser hunting her.
- **What the first plays found:** the Stop side never saw the enemy. Its zone search was a ladder on from the zone,
  which combed the zone's far side while the enemy came in from the near. The zone search is now sectors through it
  (`SearchPattern.Sector`, a pattern the Search order carries now), out to the day's sight less the zone's radius so
  the zone stays in sight, or to its edge for a ground too wide for that. After that, both benches fought.
- **Set for balance by the scenario's own knobs**, 8 seeds a step (score Britain v Germany):
  - breakout 1.00 v 0.00 (never through) → haze to 12 km, the gap to 15 km: 0.13 v 0.88 (7 of 8 through) → 16 km
    and 12 km: 0.88 v 0.13 → 14 km and 13 km: **0.63 v 0.38, through 3 of 8**. Steep: a couple of km of sight or gap
    decide it, so a real scenario's balance wants the same care.
  - convoy 0.04 v 0.96 (in once in 8) → one battlecruiser instead of two, visibility 14 km: **0.38 v 0.63, in 3 of 8**.
  - raider as written: **0.62 v 0.67** (the raider sank her tonnage in 2 of 8, lost in 1 of 8).
- Next: the user's investigation of which real scenarios get aims (scenarios.md wave 2: the Falklands, Goeben at bay,
  the Lerwick convoy, the Yellow Sea, Dogger Bank's run; Eckerö under Åland).

## Steps

- **A0 Investigation.** Read the files above against this plan; check how auxiliaries on routes behave when a
  hostile is in sight (today they steam on), where a per-unit home goes (`AiSystem.HomeOf`), how the captains rank
  targets (`CaptainLayer.Fire.cs`) and where a named-ship priority enters it; amend the plan; agree it.
- **A1 Aims as data, their state, the judge.** `AimKind` Reach, Sink, Stop and their data (zone, of, stay, weight);
  the scenario format and its errors; `Aims.State` (Open, Met, Lost) and its score; the arrival latch (and the stay's
  clock) in AI state; the judge's Decided, the result's aims and the side's weighted score. Tests: parsing, each
  state's edges and scores, a stay broken by leaving, a replay with arrivals.
- **A2 Reach.** The reaching units as the main body, the per-unit home, GoTo the zone, declining as carrying on, stop
  on arrival; the aims pursued in order. A test: an unopposed convoy arrives and the side's aim is Met; a breaker
  that sees a stronger enemy still makes for her zone; the escort makes for home once the convoy's in.
- **A3 Stop.** The search across the enemy's way, the first group sent after, the captains' priority on named ships.
  A test: the defender's main body finds a convoy on its way, and his ships shoot transports over escorts.
- **A4 Sink.** The raider's search and intercept, the merchants' running, the hunters'. A test: a raider among
  merchants on routes sinks or takes them, and they scatter from her; a raider sighting a stronger cruiser declines
  and, with Reach home after, makes for home with her score.
- **A5 The UI.** As above; checked by `-screenshot -ui` readbacks.
- **A6 Benches and balance.** Three test scenarios (`"test": true`): a breakout, an escorted convoy against a
  squadron, a raider among routes with two hunters. Each played headless (`shipgen scenario FILE --play --seeds N`)
  with the aims' states in the panel's summary, and set for balance with the scenario's own knobs (crews, tempers, the
  weather, the zone's size and place). Then the user's investigation: which real scenarios get them (scenarios.md
  wave 2: the Falklands, Goeben at bay, the Lerwick convoy, the Yellow Sea, Dogger Bank's run; Eckerö under Åland).

## Open questions for the user

None open (the three answered 2026-10-31, at the top).
