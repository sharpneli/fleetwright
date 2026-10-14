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
