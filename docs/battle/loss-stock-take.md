# How ships are lost: the stock-take to come

Written 2026-11-04 at the end of the electrics session, to start a new one from. **Started 2026-11-04: the bench is built and the first findings are under "What the
bench found", which replaces this note's leads where they differ.** Nothing has been tuned.

## What the bench found (2026-11-04)

`shipgen losses dogger-bank jutland --seeds 4 --threads 6 [--spare RUNGS] [--out DIR]` (`LossBench.cs`, about 10 min at this size): a post-mortem for each loss, the causes counted, where the
fire went. `--spare` is a counterfactual: those hits do nothing (`DamageSystem.Spared`, a bench's, never set in play), the same seeds, torpedoes always counting. Four seeds, and Jutland's baseline
spreads 66-79 lost and the Dogger Bank's 13-29, so read direction and big steps only.

**Lost a seed:**

| Hits that do nothing | Jutland, Britain / Germany | capital / cruiser / destroyer | kt | Dogger Bank, Britain / Germany | capital / cruiser / destroyer | kt |
|---|---|---|---:|---|---|---:|
| None (the code as it stands) | 24.8 / 47.5 | 14.0 / 17.2 / 41.0 | 585 | 10.0 / 10.0 | 4.0 / 3.5 / 12.5 | 164 |
| On ships going home (`home`) | 19.5 / 29.5 | 12.8 / 14.2 / 22.0 | 491 | 4.3 / 8.3 | 2.0 / 2.2 / 8.2 | 87 |
| All but 1 shell in 3.5 (`gap:3.5`) | 15.5 / 38.5 | 4.5 / 15.2 / 34.2 | 257 | 9.0 / 7.8 | 0.5 / 1.8 / 14.5 | 54 |
| Both (`home+gap:3.5`) | 22.0 / 27.3 | 9.0 / 13.8 / 26.5 | 376 | 4.8 / 3.5 | 0.2 / 2.0 / 6.0 | 33 |
| Boats' guns on larger ships (`boat-guns`) | 28.3 / 47.8 | 13.0 / 17.0 / 46.0 | 554 | 6.8 / 10.3 | 3.2 / 2.2 / 11.5 | 127 |
| On boats while screening (`screen`) | 26.8 / 42.0 | 11.8 / 19.2 / 37.8 | 524 | 5.8 / 8.5 | 4.0 / 4.2 / 6.0 | 153 |
| The record | 14 / 11 | | | 0 / 1 | | |

**What it says:**

1. **Two causes do different jobs.** Fire on ships going home decides the **small ships** (destroyers lost 41 -> 22, Germany's 27 -> 11.5). Capital ships hardly move under it, so most of those lost were already lost when they
   turned for home. The gunnery gap decides the **big ones** (capital ships 14 -> 4.5, the Dogger Bank's 4 -> 0.5; the tonnage more than halves), and destroyers hardly move under it.
2. **They don't add up.** Both together lose as many ships at Jutland as `home` alone, and more capital ships than `gap` alone (9 against 4.5). A ship spared goes on firing: shells a seed go 78,000 -> about
   119,000 under either, and ships with a torpedo hit 10.5 -> 18.5 with both. Spared hits are a crude stand-in: they show what those hits decide, not what a fix would give.
3. **Jutland's floor is about 49 lost a seed whichever way,** twice the record. What's left is the third finding:
4. **Slow deaths.** 23-45% of losses had no hit in their last 20 minutes: 17-22 a seed at Jutland, a median 67-96 minutes after the last hit. The destroyers among them end near 1,200 t of water, GM about -0.1 m, lolled
   13-19 degrees, at 2-3 kn, mostly over the 25 degree capsize line. **This looks like why capsizing leads.** Not yet tested by a counterfactual. In the record, slow losses were perhaps 5 of 25 (Warrior, Lutzow, Wiesbaden, Elbing, Rostock: from memory, to source).
5. **Half of all hits land on ships going home** (52% at both battles, from about 40% of the shells), and ships fall out in numbers: **136 of 250 fall out a seed at Jutland**, 68 of them for guns, of whom
   29% are lost, against 80% of those out for speed, 74% steering and 91% list. Nearly no ship that never fell out was lost (1-4%).

**What it isn't:**

- **The lines closing.** Fighting capital ships keep about 16 km from the nearest enemy heavy ship at Jutland. 52% of hits land inside 4 km on 12% of the shells, but those are boats and cripples, not the line.
- **Damage per hit,** as far as this reads. Capital ships lost take a median 50 heavy hits (141 of all sizes) and about 11,700 t, some 240 t a heavy hit, the record's size (Seydlitz about 5,300 t on 21, Lutzow about 8,300 t on 24: rough, from memory, to source). Heavy hits
  run about 2,000 a seed at Jutland against the record's roughly 220 (also from memory, to source). Isolated, 6 heavy hits lose up to 16% of destroyers after 8 hours and 12 destroyer-gun hits none (`hits m_class`, `hits v25`).
- **The boats' 4-inch guns on big ships,** though they're a third of all Jutland's hits (2,711 a seed; 80-90% at 0-2 km): taking them away moves nothing but some tonnage. Odd behaviour, maybe, not what kills.
- **Magazines.** None of 369 losses (a magazine reached 0 times in about 9,800 heavy hits, against about 2 set off per 100 in `damage-history`'s battlecruiser cases on "1916 BCF"). Not a bug: Jutland's British side is on the plain "1916" drill (`magazines.md`: per-squadron doctrine
  is the scenarios' later step). The record's biggest losses simply don't happen this way here. Heavy gunfire has to flood a ship instead, which may be part of why it takes 50 hits.
- **Screens at Jutland,** where `screen` barely moves the count, though it halves the Dogger Bank's destroyers (12.5 -> 6): 32 of the 41 destroyers lost at Jutland are *first* struck on a screen, but that's where boats spend their time.

**The starting groups need revising.** "Cripple finished off" counts 51% of Jutland's losses but overstates capital ships, as `home` shows. Read the counterfactual for that, not the group.

**Next rungs, in order** (the user's call):

1. **Why so many fall out,** the root under cripples. `FallOutGunsShare` off for one run: do ships out for guns do better staying in line? A doctrine override, no code. Half of the fall-outs are this.
2. **The slow deaths:** a counterfactual where water stops rising 20 minutes after the last hit, then one flooded destroyer traced room by room (Badger: pumps at 100%, about 1.5 t/min for 6 hours). Is that seepage real, and should anyone come alongside or tow?
3. **What's left of the gunnery gap.** At Jutland, capital against capital: 6-10 km 18.6%, 10-15 km 6.1%; `error-sources.md` accepted about 3.5x. These are the numbers to hold beside it.

## The slow deaths: why doesn't her crew stop the leak? (2026-11-04)

The user: a ship with her crew, not under fire and not otherwise disabled, should stop leaks slow enough to take six hours to sink her. `losses` now splits the sea coming in by what could stop it, each minute (printed for the slow deaths, no hit in 40 min or more), and
`--follow SEED:NAME,...` traces a ship cell by cell every 10 min, her parties too, into DIR/trace.txt. `--dc redo,slow` turns on two rungs (settings `RepairRedoLeft` and `SafeOutOfActionKn`, **off by default: Dogger Bank losses identical seed for seed with them off**).

**What the traces show.** Indomitable, Dogger Bank seed 1915: 14 heavy hits, the last at +48 min; then home at 23 kn for 4.7 hours, no enemy within 30 km, water about 700 -> 8,300 t through 2.4 m2 of holes. At +260 min one of her biggest leaks is 40 t/min through a hole 2.3 m *above* the waterline: the bow's ram lifts the sea over it, and slowing would stop it. A patch there has crept to `left 0.96` (above 15 kn, 15% back toward open every 10 min), and:

1. **Work that creeps loose is never redone.** `Best` only looks at `HoleOpenM2`, which the job empties for good. A code gap. `RepairRedoLeft` 0.5 redoes it.
2. **Nothing slows a ship for flooding unless a boundary is at risk.** `Safe` reads bulkhead loads only, and a Home captain rings what she can make with nobody near. `SafeOutOfActionKn` 12 asks for that once `FireTurnToS` has passed since her last hit, less if the ram would still reach a hole clear of the sea.
3. **Parties stand idle.** Free 36-41% of the time before giving up, in the three traced, with the water rising. A job takes one party (`Taken`), whatever its size.

**The rungs, lost a seed (4 seeds; Jutland's baseline spreads 66-79):**

| Rung | Jutland, Britain / Germany | capital / cruiser / destroyer | kt | Dogger Bank, Britain / Germany | capital / cruiser / destroyer | kt |
|---|---|---|---:|---|---|---:|
| None | 24.8 / 47.5 | 14.0 / 17.2 / 41.0 | 585 | 10.0 / 10.0 | 4.0 / 3.5 / 12.5 | 164 |
| `redo` | 22.5 / 43.8 | 13.2 / 18.5 / 34.5 | 580 | 9.5 / 10.0 | 3.8 / 5.0 / 10.8 | 161 |
| `slow` | 31.0 / 52.0 | 12.5 / 25.0 / 45.5 | 595 | 6.0 / 10.0 | 2.5 / 3.0 / 10.5 | 110 |
| `redo,slow` | 36.0 / 48.2 | 13.8 / 21.0 / 49.5 | 594 | 3.8 / 9.5 | 2.2 / 3.0 / 8.0 | 95 |

**What it says:**

- **`redo` is right and small:** within the noise. Keep it for what it is, a gap closed.
- **`slow` helps where nobody's chasing and hurts where someone is:** the Dogger Bank's tonnage falls a third, Jutland gets worse (72 -> 83 lost, mostly cruisers). A long night battle has enemies about after a quiet half hour, so a slowed ship gets caught. Her last hit is the wrong test: it wants the enemy's whereabouts, which damage control doesn't know and her captain does.
- **Most of the water isn't small leaks.** In the slow deaths, the sea through holes a party could reach and hasn't worked on is 0-2% for destroyers and 2-26% for cruisers; 13-40% for capital ships (the median ship's far less: a few big flows). Destroyers' is **mat-sized and beyond, about 95%** (holes of 0.3 m2 and up; 8-12 t/min). So the user's rule meets the mat.
- **The mat is the choke.** 1,800 man-minutes (`RepairJobManMin`: 09 §4's 90 min x 20 men, a big ship's) times the holes, by one party of at most 16 men, at skill 0.85, /1.6 in the dark, /(1 + list/10): **a little over 2 hours at best, about 7 at 10 degrees of list in the dark, for each hole.** A destroyer doesn't have 20 men to spare, and a mat for her side is not a battleship's. She also needs to be at 5 kn with the hole within 2 m of the waterline. Nothing but a mat touches a hole over 0.3 m2.

**Next, none of it built (the user's call):**

1. **Idle parties join a job** already taken, up to its crew x `RepairCrewsAJob`: no new number, and the mat gets its 20 men on a big ship.
2. **A mat by the ship's size,** so a destroyer's takes less than a battleship's: the research's 90 min x 20 men is a large ship's. Worth a research request, with what crews did about a hole of 1-5 m2 (shoring from inside, stuffing, abandoning the space and shoring its boundary).
3. **Slow by the captain's judgement:** a Home captain with no enemy near keeps to what damage control asks, and runs when one's about. That's `slow` with the right test.
4. Then the ladder again, alone and together. Giving up is a question too: Goshawk went from fighting her water to abandoned when her plant went out.

## The question

Why are losses so heavy? The user's framing, to keep: **losses in themselves don't tell much; how they happen is the critical part.** The user suspects AI
behaviour, as earlier heavy-loss cases turned out to be. So this starts by finding out, not by tuning, and the first deliverable is a picture of how ships die.

## Where it stands

The balance panel, each column after the step named (`docs/battle/electrics.md` has the steps). Eight seeds, Jutland four, so read direction only.

| Scenario, side (ships) | Record | Before electrics | EL4 | EL4b + EL5 | EL6 | EL7 |
|---|---:|---:|---:|---:|---:|---:|
| Jutland, Britain (151) | 14 | 15.3 | 17.3 | 22.5 | 26.5 | 24.8 |
| Jutland, Germany (99) | 11 | 41.5 | 44.5 | 41.5 | 50.5 | 47.5 |
| Dogger Bank, Britain (33) | 0 | 6.6 | 6.0 | 7.6 | 9.3 | 8.8 |
| Dogger Bank, Germany (26) | 1 | 9.9 | 9.0 | 9.3 | 9.3 | 12.5 |
| Heligoland, Britain (44) | 0 | 1.1 | 1.0 | 2.3 | 4.4 | 3.9 |
| Heligoland, Germany (24) | 4 | 11.9 | 12.8 | 11.1 | 16.0 | 17.6 |
| Tsushima, Russia (13) | 7 | 4.9 | 5.8 | 3.6 | 4.5 | 3.9 |
| Tsushima, Japan (14) | 0 | 5.1 | 4.8 | 3.6 | 3.4 | 3.3 |
| Sides within twice the record, of 14 | | 6 | 8 | 6 | 7 | 7 |

The EL7 column is the code as of this note. To rerun it (about 30 min):

```
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- balance --seeds 8 --fc-runs 0 --threads 6
```

Also on the panel: how ships go, last read around capsized 55-60%, foundered 30-40%, broke in two under 10%, so capsizing leads everywhere. Worth asking whether that's
what the record looks like, on its own.

## The leads so far (as written before the bench; see above)

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

## What to build first (built: `LossBench.cs`)

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
