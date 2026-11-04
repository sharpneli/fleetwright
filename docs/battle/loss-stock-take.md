# How ships are lost: the stock-take to come

Written 2026-11-04 at the end of the electrics session, to start a new one from. **Not started.** The user expects some VFX work first; this comes
next when gameplay-affecting work resumes.

## The question

Why are losses so heavy? The user's framing, to keep: **losses in themselves don't tell much; how they happen is the critical part.** The user suspects AI
behaviour, as earlier heavy-loss cases turned out to be. So this starts by finding out, not by tuning, and the first deliverable is a picture of how ships die.

## Where it stands

The balance panel, each column after the step named (`docs/battle/electrics.md` has the steps). Eight seeds, Jutland four, so read direction only.

| Scenario, side (ships) | Record | Before electrics | EL4 | EL4b + EL5 | EL6 |
|---|---:|---:|---:|---:|---:|
| Jutland, Britain (151) | 14 | 15.3 | 17.3 | 22.5 | 26.5 |
| Jutland, Germany (99) | 11 | 41.5 | 44.5 | 41.5 | 50.5 |
| Dogger Bank, Britain (33) | 0 | 6.6 | 6.0 | 7.6 | 9.3 |
| Dogger Bank, Germany (26) | 1 | 9.9 | 9.0 | 9.3 | 9.3 |
| Heligoland, Britain (44) | 0 | 1.1 | 1.0 | 2.3 | 4.4 |
| Heligoland, Germany (24) | 4 | 11.9 | 12.8 | 11.1 | 16.0 |
| Tsushima, Russia (13) | 7 | 4.9 | 5.8 | 3.6 | 4.5 |
| Tsushima, Japan (14) | 0 | 5.1 | 4.8 | 3.6 | 3.4 |
| Sides within twice the record, of 14 | | 6 | 8 | 6 | 7 |

**EL7's panel wasn't read before this note.** Rerun it first, so the baseline is the current code (about 30 min):

```
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- balance --seeds 8 --fc-runs 0 --threads 6
```

Also on the panel: how ships go, last read around capsized 55-60%, foundered 30-40%, broke in two under 10%, so capsizing leads everywhere. Worth asking whether that's
what the record looks like, on its own.

## The leads so far (none of them proven)

1. **A battlecruiser holding off** (`bugs.md`, found 2026-11-04). `AimsTests`' convoy across 12 seeds: in 9 she closes a lone light cruiser to 5-8 km and sinks it; in seeds 1, 3 and
   11 she sits at about 20 km for 40-60 minutes, and in seed 3 the convoy gets away. The same ship in the same situation behaving differently isn't physics. The clearest AI lead. It runs
   the other way from heavy losses, though: it shows the AI's judgement of range is worth examining, not that it's too aggressive.
2. **Cripples.** `balance.md` already has it (about line 99, "BL5 again: the blood inside 4 km is the cripples"): a crippled ship sinks 5-30 minutes later under whoever's in range; in the
   record a cripple was often lost in mist and smoke, left for a better target, or struck. The doctrine field `Targets` was proposed there, and the research request of 2026-11-01 (fire
   distribution and cripples) is in `research/REQUESTS.md`; check whether it's been answered.
3. **The outages were a brake.** Power trips came down to the record's size during the electrics work, and more guns stay in action; EL6 made most of the Great War bloodier on both sides. So some of
   what's showing may be the gunnery gap `error-sources.md` accepted (about 3.5× the record) with less hiding it. A hypothesis, not shown.
4. **Germany at Jutland** loses about 50 of 99 to Britain's 26 of 151; the record has 11 and 14. That's lopsided as well as heavy, and worth its own look: is it their numbers, their position, or how the AI
   commits them?
5. **Damage control's open items** (`damage-control.md` DC9) and why capsizing leads: a hit causes a loss, but what happens between the hit and the loss may be the real story.

## What to build first

A small bench, `shipgen losses SCENARIO [--seeds N]`, so the same check can be rerun after any fix: **a post-mortem for each ship lost.** For the last 30 minutes of each:

- range to whoever hit her, and who that was; her own target and whether she was firing;
- her orders: task, the captain's behaviour, whether she'd fallen out, whether she was trying to leave, and when she could have;
- her state when hit: already crippled? speed, power, list, water, guns left, and how long before the end;
- what killed her: the last hits, flooding after them, how much of the water came from shells against torpedoes.

Then sort the losses into causes and count them by side and scenario. Starting groups, to be revised by what turns up:

| Group | What it looks like |
|---|---|
| Cripple finished off | Slow or stopped well before the end, still under fire |
| Stayed too long | Could have turned away with her speed intact, and didn't |
| Fed in piecemeal | Arrived alone or in small lots against more |
| Point-blank brawl | Inside the range the battle was really fought at (`hit-rate-with-range`) |
| Flooding ran away | Few hits, hours later, damage control losing |
| Torpedoed | Torpedoes the main cause |
| Fair fight | Stood in line, shot at the record's ranges, sank |

**Start with Jutland and the Dogger Bank**, furthest over the record, then compare sides. The existing pieces: `end.Timeline` and `CombatLog` (who hit whom), `scenario --play`'s fell-out and main-guns tallies, `--fire-table` (hits by range band), `ScenarioPlay.PlayToEnd`'s minute callback,
`ai.Tasks.Of`, `ai.Captains.Of`, `ai.Commanders.PhaseOf`. The electrics session's probes weren't kept; a per-seed loop over a scenario is only a few lines (`AimsTests.Play` shows the shape).

## Rules that apply (the user's, in memory)

- Find the mechanism first; don't tune a number to bring losses down (`isolate-before-tuning`, `mild-rules-first-principles`, `physics-is-the-only-yardstick`). Effects enter for every battle, never to fix one.
- More sinkings than history are acceptable until damage control's whole (`no-historical-tuning-without-dc`). AI choices are a leading admiral's judgement with thresholds (`admiral-judgement-not-fixed-rules`); the AI estimates what's true and optimism is doctrine's (`ai-expectation-not-optimism`).
- Read a hit rate beside its range (`hit-rate-with-range`). Time limits are a testing aid; never end a fight in progress (`no-time-cutoffs`).
- A single seed proves little: the convoy test rested on one seed, which went odd after a change of under 1% in displacement, while 11 of 12 seeds still behaved. Sweep seeds early.

## Numbers from the electrics work worth suspecting if power shows up in the post-mortems

All [EST], in `electrics.md` and `settings.json`: `HandChangeoverS` 180 (electro-hydraulic turrets on their brakes; the first to watch, as most trips last 1-5 min), `HandReload` 3, `HandGearShare` 0.5,
`HydraulicListDeg` 10, `TableHandLatency` 1.5, `GyroSettleMin` 10, `TorpedoBlackout` 0.3 and `TorpedoSectionPower` 0.75, `LocalTripMin`'s cut leads at 20-60 min, `GeneratorFloodShare` 0.3. `damage-history` doesn't count
blackouts, so the research's check (at most 2 ship-wide losses from above-water shellfire in about 100 heavy hits) is still open: adding the count there is a small job.
