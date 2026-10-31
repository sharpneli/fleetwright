# Plan: the aims

Drafted 2026-10-31 (the user: "the break out, the invasion and commerce raiding should be fine for the first release;
once the systems are in place we'll investigate which exact scenarios we want"). N1 of `docs/missions/scenarios.md`,
the heart of its wave 2, and the first of the objectives (`docs/missions/plan.md`, 6.1). Bombardment was weighed and
dropped: with no shore targets and fire control that only aims at ships, any version now would be a hack.

**The user's answers (2026-10-31):** (1) whether a ship counts on entering the zone or after a stay there is the
aim's setting; (2) a scenario isn't all won or all lost: aims score in part and weigh, and getting home can be a
bonus aim beside the main one ("better sometimes to reach home damaged than squeeze the last merchant out of the
enemy"); (3) the aims come now, before the balance round's BL3 and BL4.

## Status

| Step | State | Left |
|---|---|---|
| A0 Investigation | started 2026-10-31 | |
| A1 Aims as data, their state, the judge | not started | |
| A2 Reach: the side that wants a place | not started | |
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
