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
| A3 Stop: the side that wants to prevent it | not started | |
| A4 Sink merchants: the raider, the merchants, the hunters | not started | |
| A5 The UI: aims, progress, zones | not started | |
| A6 Benches and balance | not started | |

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
