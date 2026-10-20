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

- [ ] `SimHostTests.A_live_run_replays_exactly_headless` failed once in a filtered Release run of 73 tests (2026-10-20,
  the AI's baseline) at `Assert.Empty(violations)` (line 57: the snapshots read on another thread broke a rule), and
  passed alone with and without that change. Not a timing limit: a snapshot race the parallel load shows now and then.
  The violations' text wasn't kept; to hunt it, run it under load in a loop and print them.
- [ ] `PictureTests.Two_hundred_ships_look_at_each_other_without_garbage` failed once in a full run (2026-10-14, after
  4.3a) and passed alone and in the next full run. No ship fires in it, so it's not the damage system's; the same
  suspicion as above.
- [ ] `RailsTests.Two_thousand_ships_steam_a_year_in_seconds` (its 60 s limit) failed in two full Debug runs of
  `Fleetwright.Sim.Tests` in a row (2026-10-19, shell contents C4, which touches no rails code) and passed alone both
  times: 33.0 s against 32.3 s on the commit before. A timing test in Debug under the suite's parallel load: the limit
  has little room there. Worth running it in Release, or with the load it meets in mind.
- [ ] `ShipMotionTests.A_tick_allocates_nothing` failed in a full `dotnet test` of every assembly at once (2026-10-14,
  lighting L4, which touches no sim code; once with a second Sim failure) and passed alone three times in three. The
  same suspicion as above.
- [ ] `MapCameraTests.Building_the_grid_allocates_nothing` (`Fleetwright.Tests`) failed once in a full run of that
  assembly (2026-10-15, torpedoes R5, which doesn't touch the grid) and passed alone three times and in the next full
  run. `Fleetwright.Tests` still runs with concurrent GC, which the Sim tests stopped doing for this kind of flake
  (the memory "alloc tests vs background GC"); the likely fix is the same there.

## Damage

- [ ] An AP near miss 1.8 m down still puts 0.8 pieces a burst into a destroyer's upper works (the 16" Mk 8 20 m short at
  15 km, `NearMissTests.AP_bursts_deep_and_throws_no_splinters`; no holes, nobody hurt). Damage/15 §1.1 has the sea
  stopping every piece at that depth. Found 2026-10-19 when the Type 91 lost its diving fuze: its 0.4 s burst 14 m down
  had hidden it. Likely the heaviest pieces (the base plug) and `NearMiss.WaterDragM`'s path up through the water.

- [x] A battleship under medium HE sometimes loses a magazine to fire (about 1.5 %): decided 2026-10-15, rarely is
  right (user); damage control and flash discipline refine it later (`magazines.md`, its yardstick).
- [x] A contact-fuzed shell on armour breaks up instead of bursting on the face (found 2026-10-19, fire.md B5; fixed
  2026-10-20, shells plan E8a: `Terminal.FiresIn`, Shimose on Borodino `Holed` 42% → 2%).
  `Terminal`'s plate step calls a shell between the plate's holing limit and V_L `Holed` and returns before its fuze
  is asked: Mikasa's 12" Shimose (Ijuin fuze, delay 0, starts on any plate since 2026-10-19) on Borodino's belt is
  `Holed` in 43% of hits (a weak burst of pieces, no blast, no fire), where the record has it bursting on the armour
  face (06 §5: "never penetrated armour over about 6 in"; 15 §2.3: "exploded the moment they touched"). A fuze that
  starts on the plate with no delay should burst on the face (`Defeated`, outside) before the shell can hole it. The
  shells plan's, not fire's; it moves blast and men on armoured targets more than fires.
- [x] Every main-battery mount of 150 mm or more is a gunhouse with a barbette (`Geometry.cs`, `kind = calMm >= 150 ?
  "bb"`: a switch by calibre), so a WWI light cruiser's open-backed 6″ (Chester, the Towns) get an enclosed turret's
  flash path to the magazine and their crews sit inside steel (found 2026-10-20, shells plan E8a: Chester lost 13% of
  ships to a magazine under 17 hits of 15 cm, killed 0.38 a hit against the record's 2.0). Fixed 2026-10-20 by
  `magazines.md` V1: a battery's `housing` (barbette, gunhouse, shield), Chester and the Towns on shields.
- [ ] Chester's crews behind open-backed shields still lose only 0.43 killed a hit to 15 cm against the record's 2.0
  (`damage-history`, after `magazines.md` V1 put them in the open). Damage/16 has the legs lost under shields that
  didn't reach the deck: splinters at deck level, likely the casualty model's (E6), not the housing's.
- [ ] Four torpedoes at once on one side capsize `yamato` in about 2 minutes, whatever her tightness: 6,800 t and 21.6°
  of list in the first minute, faster than any counterflooding (found 2026-10-20, damage-control.md DC4; one torpedo
  alone gives 2,450 t and 8.8°). Shinano took four from one spread and listed 10-13°, lasting 7¾ h. Likely her wing and
  TDS cells (water all on one side, deep) against a GM of 1.4 m; the hull and protection's, not damage control's.

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

Seen with `victory_1944` against `dreadnought` at 4.8 km (2026-10-19, the bug hunt that gave ships without a main
battery their heaviest guns as chief, `BatteryRow.Chief`):

- [ ] The Fight band is doctrine's RangeShare (0.6) of the chief guns' reach, whatever they are: Victory's 152s reach
  16.6 km, so she opens from 4 km out to ~10 km, where her light guns hit far less (7.7% against 40% at 4 km) and the
  Dreadnought's 12" are still in reach. A ship of light, quick guns should want the range where her matchup is best,
  not a share of her reach. A design question for the band (the matchup's best range?), not a quick fix.
- [ ] Britain made the Dreadnought out 20 s later than Germany made Victory out (15:30:35 against 15:30:10), and only
  with Victory on side 0; on side 1 she engages at once. Both in 25 km visibility at 4.8 km, where the first look
  should see a dreadnought. Likely the detection's draw (`Sightings.Sight`, its stream by side, entity and look), but
  20 s at that range is long: check the chance per look for a 15 m eye on a big ship at 5 km.
- [ ] Germany's commander judged the Dreadnought outmatched by Victory (2,274 t) and withdrew her, a few minutes in.
  Victory's 152s were hitting at 40% at 4 km, so maybe right; check the judgement's terms before believing it.

Seen in the AI's baseline after the detail passes (2026-10-20, docs/battle/command.md, after 5.1g):

- [x] A commander who declined forgets it as soon as contact drops, and searches back into the enemy he declined:
  `Balance` clears `Declined` when no enemy is held (`CommanderLayer.cs` ~477), the phase goes to Lost, and `Search`
  runs again because seeing the enemy reset `Searched` (~678). Withdraw, lost, search, contact, outmatched, withdraw:
  Japan at Tsushima (seed 1905) every ~35 minutes for 15 hours, which is why that seed ran out of time; the High Seas
  Fleet four times at the Jutland (seed 1916). A decline should hold until the balance turns (his side's strength
  against what he last held), not until the enemy is out of sight.
  Fixed 2026-10-20 (command.md, "After the decline holds"): a decline lasts out of sight, and he remembers the enemy he
  declined (the gravest held since); he accepts again only over the bar against both what he holds and what he
  declined, or fights when caught by what he can't beat. Keeping it out of sight alone wasn't enough: Japan then
  re-accepted against a lone destroyer, the part of the Russian fleet it still held.
- [ ] The same intercept is given again every commander think: the High Seas Fleet "to intercept Valiant" every minute
  15:56-16:24 (seed 1916), "Lion" 19:46-20:03; the Battlecruiser Fleet "B97" 17:03-17:33. `FreeToClose` lets an
  intercept go only for another target, so the task is likely idle or through between thinks (or the target's id
  differs from `Tasks.TargetOf`). Not known which; each re-order may restart the approach.
- [x] No Jutland seed ends by its own rules: `Done` needs every side to have its aims, given up or broken off with every
  unit home, and a side retiring outmatched (Germany, seed 1916) is none of them; ships sinking hours later keep it
  from quiet. Whether a decline that has got away should count as an end is for 5.1f.
  Fixed 2026-10-20: a side declining counts as going home, and its ships count as done home or out of the area (in no
  encounter), as command.md's "The end" has it; ports hundreds of kilometres off no longer hold it open. The Jutland's
  seeds still out of time are fights going on at 04:00 (Britain chasing Germany into the Bight, every ship not home in
  an encounter), which no end should cut off.
- [ ] A commander's search for a lost enemy can take most of a day: in `ScenarioEndTests.Sides_going_home_out_of_touch_end_it_done`
  eight dreadnoughts lose two battlecruisers at 15:26 and give up at 08:23 the next morning (17 hours), and shorter
  `LostSearchLegM` (5 km against 20) barely changes it. The task steams to the datum (the picture's estimate when he
  lost them), forms line abreast at the weather's spacing, then runs the ladder; which part takes the hours isn't known.
- [ ] The matchup has no night: four destroyers against sixteen dreadnoughts are hopeless (confidence 0.03) with
  `--sight 5000` as without, so a flotilla is sent in by night only on a group it could take on by day (a lone
  battleship). The era's night attack on a battle line counted on the dark (short sight for the guns, the boats seen
  late); for the matchup's fine-tune with fire control's night (fighting.md 4.5).

## Shipgen sizing

- [ ] A little weight up a destroyer's mast made her shorter (fire-control.md F4d, 2026-10-18): the crow's nest's 0.7 t
  at ~17 m took `destroyer` from 116.3 m and 2,084 t to 113.6 m and 2,064 t. The sizer is right to move a little (more
  topweight), but the wrong way: with the nest, the layout at 112.3 m (its trim shift -1.62 against -1.47 m) finds
  ~100 m³ more crew space, so the length the crew needed stops short. The bistable layouts the sizing rework left
  (docs, designer-monotonic-rework: model step functions make bistable sizes). To hunt it: `SHIPGEN_TRACE=1 shipgen
  design shipgen/designs/destroyer.json` with and without the nest's weight; which deckhouse or space steps between
  the two passes.
- [ ] `victory_1944`'s two secondary directors stand in tandem on the centreline at the same height, 3 m apart: the
  after one is blind 45° either side of the bow (the forward one's hood), the forward one astern. Casemate batteries
  fight a side each, so their directors belong a side each (as the compiler's `Serves` already prefers); with the
  forward one wrecked she was blind ahead (2026-10-19). The layout's placing of a casemate ship's directors.

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
