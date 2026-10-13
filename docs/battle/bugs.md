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
  None has been seen failing.
