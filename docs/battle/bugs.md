# Bugs to hunt

Known bugs in the battle module (the sim, the AI, the world tab) that weren't quick to fix where they were found, kept
for a bug-hunting round. A bug found mid-task is fixed on the spot when that's a small, clear job; once it grows into
a bigger one, it goes here and the task goes on. One item each, with what's known; tick off or delete what's fixed,
add what's found.

## Flaky tests

- [ ] `OrderPathTests.Two_thousand_ships_on_plain_orders_cost_the_AI_nothing` failed once in four full runs of
  `Fleetwright.Sim.Tests` (Release, 2026-10-13, during terrain plan S3), and passes on its own. The failure message
  wasn't caught. It's one of the allocation tests (`GC.GetAllocatedBytesForCurrentThread` over an hour of 2,000 ships
  on plain orders must be zero), it has no land, and the terrain code returns early without it, so it's likely not
  S1-S3's. The suspicion is the same as 2.5's (`docs/battle/plan.md`, "Learnt"): under parallel test load a path's
  first runs (the JIT, tiering) allocate, and its warm-up (`Run(w, 120)` untimed) may be too short for tiering to
  have settled before the measured hour. To hunt it: run the suite in a loop until it fails and keep the message
  (the bytes and where), then compare a longer warm-up, `DOTNET_TieredCompilation=0`, and the test alone under load.
- [ ] The other allocation tests warm up the same way and may share it: `ForceTests` (line ~400), `GroupTests`
  (~189), `FormationRailsTests` (~443), `RandomStreamTests` (~78), `SlideTests.Sliding_allocates_nothing_and_costs_little`.
  Seen failing since (2026-10-13, Release, on the commit before the coarse turn-together change and after it alike):
  `FormationRailsTests.Contacts_and_snapped_searches_allocate_nothing`, `CoarseTierTests.Coarse_ships_in_an_encounter_allocate_nothing`
  and `EncounterTests.Two_hundred_ships_in_one_fight_are_one_encounter_without_garbage`, each now and then (running
  just the allocation tests, 1 run in 4 had two failures), each passing alone. Same suspicion as above.

- [ ] `PictureTests.Two_hundred_ships_look_at_each_other_without_garbage` failed once in a full run (2026-10-14, after
  4.3a) and passed alone and in the next full run. No ship fires in it, so it's not the damage system's; the same
  suspicion as above.
- [ ] `ShipMotionTests.A_tick_allocates_nothing` failed in a full `dotnet test` of every assembly at once (2026-10-14,
  lighting L4, which touches no sim code; once with a second Sim failure) and passed alone three times in three. The
  same suspicion as above.
- [ ] `MapCameraTests.Building_the_grid_allocates_nothing` (`Fleetwright.Tests`) failed once in a full run of that
  assembly (2026-10-15, torpedoes R5, which doesn't touch the grid) and passed alone three times and in the next full
  run. `Fleetwright.Tests` still runs with concurrent GC, which the Sim tests stopped doing for this kind of flake
  (the memory "alloc tests vs background GC"); the likely fix is the same there.

## Damage

- [x] A battleship under medium HE sometimes loses a magazine to fire (about 1.5 %): decided 2026-10-15, rarely is
  right (user); damage control and flash discipline refine it later (fighting.md, the later passes' Magazines).

## Balance and calibration

- [ ] The flotilla scenario's day attack (six boats ordered against four battleships) is beaten off before it fires
  together in half the runs: 4 of 8 seeds both before and after the destroyer crunch's lighter boats (2026-10-15),
  boats falling out for steering and speed and the line dropping under the break-off share. `FlotillaAttackTests`
  takes the first of seeds 1-12 that gets in. 2026-10-16 (the signal fixed): 17 of seeds 1-30 get in, and 15 of those
  fire two boats or more on the signal. Decide whether half is the balance wanted. 2026-10-18 (target choice and
  the opening, fire-control.md F6): the line now opens on a boat within a minute instead of two, spreads its fire over
  the boats and takes the closing ones first, and the secondaries no longer flick to each unknown contact: none of
  seeds 1-24 gets in (all beaten off in Gain or Run). Accepted for now (user); `FlotillaAttackTests`'s attack test is
  skipped until the tuning (the boats' break-off share, their run-in, or the line's fire against them).
- [ ] Opening fire once the track is good enough to range from (2026-10-18, `OpenDoubtSteps` 2) moves Lion's lay after
  a hit at 15 km from under 50 m to 52 m on average (`FireDisciplineTests`, bound now 60): she opens on a less settled
  track. Tune `OpenDoubtSteps` (1 waits for about twice the readings) against fc-history's time to first hit.
- [ ] A battery ranges on an evading cruiser for 20 minutes without a straddle (Tsushima 2026-10-18: Shikishima on
  Aurora, 23 salvos from 11 to 4 km) and never shifts to a target it could hit: the spotter's calls (F7) and target
  choice by estimated effect (F10).
- [ ] `Matchup.OutExponent` scales a hit's effect by 1/displacement, so it overstates what a hit does to a light ship
  against the fine model: a 6" HE hit on the V25 puts her out 10.2 % by the matchup, 6.0 % by `shipgen hits` (it was
  about 8 % against 7.2 % on the old, twice-too-heavy V25). The AI judges destroyers more fragile than they are;
  `MatchupTests` allows up to 11 % until it's recalibrated.

## The AI

Both seen in `MatchupPlayTests.A_pack_of_destroyers_presses_its_attack_home_on_a_battleship` (2026-10-16, while
moving underwater shells to a 3-decay reach), which now runs with hits doing no damage so it tests the attack alone.

- [ ] A flotilla's attack goes on against a target already going down: the Iron Duke blew up (a magazine, after 21
  4" hits) at 780 s, and the eight V25s still ran in, signalled Fire at a ship no longer there, then turned away. The
  attack should end (or pick the next ship, `Biggest`) once its target is lost or sinking.
- [x] On the Fire signal only one boat of eight launches (4 torpedoes), though five to seven have 3-6 tubes bearing
  for the whole 80 s window (`TorpedoSystem.Ready`). Not yet known which check in `CaptainLayer.FireOnSignal` stops
  the rest: likely `TorpedoFoul` (own boats on the track: the line is still swinging through its turn together), or
  the guide (crippled, 7-11 kn, turning on her own) dragging the line. Seen before this change too.
  2026-10-16 (docs/shipgen/protection-plan.md H2): it is `TorpedoFoul`. In the flotilla scenario's seed 1 the two
  healthy boats were foul for 58 and 56 of their checks in the Fire window (and not ready for the rest), with two of
  their friends crippled and lying in the line (power 0, 1,000 t of water; before the magazines went under the
  waterline such boats mostly blew up). Holding fire through a friend is right; what's missing is the admiral's side:
  make the signal only with the tracks clear, or leave the crippled boats out of the line first.
  FlotillaAttackTests counts a fouled signal as beaten off.
  Fixed 2026-10-16, and not the crippled boats (they had fallen out and lay well off every track): the tubes trained
  across from their rest after the turn, too late for the Fire window; a launch dropped when its tubes no longer bore
  counted as the signal answered; and the track's check held the boats steaming beside the shooter as on it. All three
  in docs/battle/command.md 5.1b piece 4. FlotillaAttackTests now asks two boats or more to fire on the signal.

## Shipgen sizing

- [ ] A little weight up a destroyer's mast made her shorter (fire-control.md F4d, 2026-10-18): the crow's nest's 0.7 t
  at ~17 m took `destroyer` from 116.3 m and 2,084 t to 113.6 m and 2,064 t. The sizer is right to move a little (more
  topweight), but the wrong way: with the nest, the layout at 112.3 m (its trim shift -1.62 against -1.47 m) finds
  ~100 m³ more crew space, so the length the crew needed stops short. The bistable layouts the sizing rework left
  (docs, designer-monotonic-rework: model step functions make bistable sizes). To hunt it: `SHIPGEN_TRACE=1 shipgen
  design shipgen/designs/destroyer.json` with and without the nest's weight; which deckhouse or space steps between
  the two passes.

## The slide

- [ ] A ship can be trapped in a bay that opens away from its course: decision 7's "boxed in, it follows the coast
  round" doesn't hold. Found 2026-10-14 by the islands rails test's layout at 1,000 ships (not the 2,000 the test runs):
  a destroyer spawned at 1.842, -158.980 on 86° at 15 kn, routed to 2.250, -152.994, meets Kiritimati. On the 1:10M land
  the atoll's west side is a pocket about 7 by 15 km, open only to the north (1.84-1.93°N, 157.41-157.55°W). At 1x the
  ship goes in, meets its east shore, slides, is released once its course is clear over the short look-ahead (30 s,
  ~230 m), goes back on rails on 86° (its side forgotten), comes off again at the east shore, and repeats every ~2.5 h:
  still there after 36 h, a backstop a lap (20). Likely the release: inside a pocket the course clears for the
  look-ahead long before the ship is out, so it never keeps one side long enough to follow the coast round. To hunt it:
  the spawn above alone, at 1x and at 1,000x (does coarse escape?); then a release that also asks the course clear past
  the pocket (rails' own `LandClear` horizon), or keeps the side until the ship has passed the point it first met the
  coast. A test of it would bound how long any ship of the islands test stays off rails.

## The app

- [ ] Dev ships placed with `-ships=...@lat,lon` didn't show in `-screenshot` captures at `-speed=1` or `2` (frames 120-900),
  at any zoom, while the same command at `-speed=10` showed them from frame ~130 (2026-10-15, torpedoes R4). Not chased:
  whether they hadn't spawned yet (their classes compiling) or were drawn nowhere is unknown. To hunt: the same run
  with `-ui` and F2's ship count.

- [ ] Quitting while ship art is still baking crashes (`0xC0000005` in `SDL_BeginGPURenderPass`, from
  `ShipArtLibrary.Draw` on the class registry's worker thread). Found 2026-10-14 (command 5.1a) by `-scenario=squadrons
  -screenshot=60`: the screenshot exits at frame 60 while the scenario's classes still bake. The engine is disposed under
  a bake in flight. To hunt it: have the exit wait for (or cancel) the registry's bakes before the device goes.
