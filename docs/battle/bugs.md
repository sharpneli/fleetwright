# Bugs to hunt

Known bugs in the battle module (the sim, the AI, the world tab) that weren't quick to fix where they were found, kept
for a bug-hunting round. A bug found mid-task is fixed on the spot when that's a small, clear job; once it grows into
a bigger one, it goes here and the task goes on. One item each, with what's known; tick off or delete what's fixed,
add what's found.

## Flaky tests

Hunted 2026-10-24: every flaky entry below is closed. The check was seven full DevRelease runs of all three test projects
together, plus one full Debug run of `Fleetwright.Sim.Tests`. Runs 1-4 had one failure in all, the achieved speed (below);
runs 5-7, after its fix, and the Debug run had none.

- [x] One `Fleetwright.Tests` test failed once (173 of 174) on 2026-10-22, right after a game capture, its name hidden
  by `-v q`. Likely `SmokeFieldTwinTests` (smoke Z5, added the day before): it makes a GPU device of its own but wasn't
  in the `Gpu` collection ("one GPU device at a time"), so it ran beside `DesignWorkerTests`' bakers. Put in the
  collection 2026-10-24. The cause isn't proven (the failure wasn't seen again), so if an exe test fails once more,
  catch its name with `--logger "console;verbosity=normal"`.
- [x] `RailsTests.Two_thousand_ships_steam_a_year_in_seconds` (60 s) failed in full Debug runs under the suite's
  parallel load (2026-10-19 to 10-23: 61-78 s, against ~33 s alone; 7.6 s alone in DevRelease). Fixed 2026-10-24:
  moved to `RailsClockTests` in the `WallClock` collection (alone, after the rest), with a 120 s bound in Debug.
  Debug's full run: 29 s.
- [x] `SimHostTests.A_live_run_replays_exactly_headless` (2026-10-20, `Assert.Empty(violations)`). A test bug: the
  reader thread started reading as soon as the host had started, and `Latest()` hands out the buffer's blank
  snapshot (`Sequence` 0, 1850) until the sim thread's first publish. Under load the sim thread starts late, so the
  clock "went back from 1916-05-31 to 1850-01-01" (reproduced by starting the reader first). Fixed 2026-10-24: the
  reader skips `Sequence` 0, as `WaitFor` does, and the assert prints the violations. The game's `WorldSession`
  already waits for the first snapshot.
- [x] `SimHostTests.The_achieved_speed_is_measured`, found by the hunt (637x read of 1,000x under the full run's
  load): a wall-clock measurement, like `The_thread_allocates_nothing_in_play`'s clock reached in 0.8 s. Fixed
  2026-10-24: `SimHostTests` in the `WallClock` collection.
- [x] The allocation tests that failed now and then in full runs: `OrderPathTests` (2026-10-13), `ForceTests`,
  `GroupTests`, `FormationRailsTests`, `CoarseTierTests`, `EncounterTests` (10-13), `PictureTests` and
  `ShipMotionTests` (10-14 morning), `MapCameraTests.Building_the_grid_allocates_nothing` (10-15). Every one failed
  before its project stopped background GC (`Fleetwright.Sim.Tests` 2026-10-14 16:14, 23f71f2b; `Fleetwright.Tests`
  2026-10-16, 8321bd4f; the memory "alloc tests vs background GC"), and none has been seen since. The suspicion of
  short warm-ups (tiering) wasn't needed.
- [ ] Not flaky, but slower: `RailsTests.Two_thousand_ships_steam_a_day_among_the_Pacific_islands_at_the_fine_tiers_top_speed`
  took 12.8 s alone in DevRelease on 2026-10-24, against the ~8 s its summary has from 2026-10-14 (Release; 35-43 s
  in full runs, 2 min in Debug's). Its bound (keeps over 2 x 240x) still has room. Worth a look in the
  profiler sometime: a drift in the fine tick or the slide, or just Release against DevRelease.

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

- [x] A ship seen bow on is hidden by her own funnel smoke (2026-10-22, smoke Z6; fixed the same day, docs/battle/smoke.md
  T1: the lines run from each eye's place to three points along her, `SmokeSightTests` 600 s of 600 in sight as with the
  field off): a Lion-like steaming at 28 kn toward a dreadnought on a 40 km day went in and out of sight from 31 km in.
  Z3's sight lines ended at her middle (and started at the observer's), so her young smoke at her funnels stood on the
  line even when it streamed away from the eye.

## The AI

Both seen in `MatchupPlayTests.A_pack_of_destroyers_presses_its_attack_home_on_a_battleship` (2026-10-16, while
moving underwater shells to a 3-decay reach), which now runs with hits doing no damage so it tests the attack alone.

- [x] A flotilla's attack goes on against a target already going down: the Iron Duke blew up (a magazine, after 21
  4" hits) at 780 s, and the eight V25s still ran in, signalled Fire at a ship no longer there, then turned away. The
  attack should end (or pick the next ship, `Biggest`) once its target is lost or sinking.
  2026-10-21: the attack chooses again once its target is seen going down (`Contact.Lost`, with the guns' held targets:
  `fire-control.md` F10 "Ships seen done for"); the original case isn't re-run (that test now runs without damage).
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

- [ ] A ship keeping station far astern of where she is (a column dropping back into one line,
  `LineShapeTests`) slows by `FollowMaxSpeedDiffKn` and overshoots her station by 1-2 km before closing again at 5 kn
  over the guide: the six columns' line forms in 64 min where the drop back alone takes about 50. Found 2026-10-25
  (docs/map-ui/battle-hud.md P1). To try: the captain easing back up as the gap closes, by her own acceleration.

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
- [ ] `Content/Scenarios/Designs/agincourt.json` builds 313 m and 59,545 t (`shipgen matchup agincourt destroyer`),
  against the real ship's 204 m and ~27,500 t; the next longest of Jutland's classes is Queen Mary-like at 251 m. Seen
  on the launch screen (L3, 2026-10-25): the rows are drawn to one scale, the longest ship of the pick fitting, so at
  Jutland every ship draws at about two thirds of Tsushima's size. Probably her seven twin turrets on the centreline
  stretching the hull; scenario designs are tuned for balance, not tonnage, but 109 m more is
  past that. To hunt: `SHIPGEN_TRACE=1 shipgen design Content/Scenarios/Designs/agincourt.json`, what sets the length.

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

- [x] Quitting while ship art is still baking crashes (`0xC0000005` in `SDL_BeginGPURenderPass`, from
  `ShipArtLibrary.Draw` on the class registry's worker thread). Found 2026-10-14 (command 5.1a) by `-scenario=squadrons
  -screenshot=60`: the screenshot exits at frame 60 while the scenario's classes still bake. Fixed 2026-10-23: the
  art library is disposed before the registry (`using` order), so it freed its bakers under the bakes in flight, and the
  registry's dispose then drained its whole queue. Now the art's dispose unhooks and waits for the draws in flight (a
  reader-writer lock), and the registry's dispose lets what's still queued go unbuilt (failed), finishing only the
  builds in flight.
