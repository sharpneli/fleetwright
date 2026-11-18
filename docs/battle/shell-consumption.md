# Shell consumption: firing what the record fired

Agreed with the user 2026-11-18. Steps SC1-SC4.

**The complaint (the user):** "Historically ships didn't really shoot as much as they did here which is the reason why our
ships run dry so fast and why the fights are so much deadlier." And: "in cases where I have my usual congaline of doom and
let's say an enemy cruiser gets hit in the engine room and slows down while others run that single enemy gets everyone and
their dog shooting at it. It's a hilarious tier of ammunition wasting." The rules are to be explicit (the user: "better to
have these as more explicit vs the existing system where range was shifted").

## The record (the research companion, 2026-11-18)

- Averaged over a whole action, main batteries fired about a quarter to a sixth of their loading rate: **0.25-0.5 rounds a
  gun a minute**. A ship engaged all day ends with about half her ammunition; only a Falklands-style chase empties her
  (Inflexible 661 12", Invincible 513 over five hours; Gneisenau fired till she was dry).
- Bismarck's last battle, the most one-sided: Rodney 380 16", KGV 339 14" of 100 a gun, about 0.4 a gun a minute at a
  crippled target. Dogger Bank: Lion 243 of 880 (28%); the British 1,150 heavy shells, the Germans 988
  (`research/simulation/ship-motion/target-relative-speed.md`).
- Practice: New York 1930-31 66 s between salvos against 31 s loading; Idaho 1942 84 s against 24 s
  (`fire-control-research/computing.md` §2.5): the spotting cycle, not the loading, set the pace.
- What held it down: slow fire until on target (each salvo waiting on the last's fall); rapid fire earned by a straddle
  and lost after misses; no spot, no shooting (smoke, mist, glimpses, own turns, target shifts all checked fire and forced
  a new ranging); ammunition a command concern (after Heligoland Bight, an order to take more care).
- Fire distribution: a ship fired at her opposite number. A whole force on one ship is remembered as a mistake: Blücher at
  the Dogger Bank (Beatty's misread signal), Wiesbaden at Jutland.

## The baseline (2026-11-18, `scenario --play --seeds 2 --ammo`, HEAD `52411d9c`)

Capital ships' chief batteries:

| | outfit fired | rounds a gun a minute, the action | while firing | under 1/3 left | dry |
|---|---|---|---|---|---|
| Dogger Bank, Britain | 74% | 0.38 | 0.56 | 7 of 10 | 2 |
| Dogger Bank, Germany | 74% | 0.28 | 0.53 | 6 of 8 | 5 |
| Tsushima, Japan | 74% | 0.28 | 0.93 | 13 of 20 | 8 |
| Tsushima, Russia | 65% | 0.23 | 0.64 | 7 of 14 | 0 |
| Jutland, Britain | 15% | 0.40 | 0.61 | 0 of 90 | 0 |
| Jutland, Germany | 42% | 0.23 | 0.56 | 12 of 54 | 1 |
| Squadrons, Germany | 83% | 0.72 | 0.83 | 8 of 10 | 3 |

- **The rate is mostly in band.** Fire for effect is not fire at the loading rate: a gun's cycle there is 1.4-1.5 drill
  reloads (median; the officer's worth, the roll, the settle, the hoists), and over an action the capital ships fire
  0.23-0.40 a gun a minute (squadrons' 0.72 the exception).
- **The fights are long** (BL3's finding): the Dogger Bank runs to its 7:10 clock against the record's three-hour chase.
- **Targets churn:** at the Dogger Bank a battery takes a new target every 10 salvos (578 in 2 seeds), and ranging on a new
  target is 5,269 of its ~14,300 chief shells (37%); ranging again after holding the range only 1,145. Of 1,735 chief
  shifts, 1,259 were "another is better" (the captain's choice), 136 lost from sight, 105 wrecks, 96 out of reach.
- **The pile-on:** squadrons put 31% of their chief shells on a target 4-5 ships were firing at (Dogger Bank 15%). They hit
  well (7-8%): the waste is not misses but shells into a ship already done for. Two causes: each other battery on her made
  her count only 25% further off (`FireCrowdShare`), and a salvo of 3 or more is always told from the others' splashes
  (`SalvoIdentifiedShells`), so the heavy guns pay nothing for crowding.
- The spots are seldom the leak: fire for effect's are 84-92% seen and told.

## The steps

### SC1 The fire spread by each ship's share; why targets churn

- **Built 2026-11-18.** A captain at will weighs a target's worth by **his share of what's done to her**: she can be put
  out of the fight once, so with N other ships' batteries of his kind on her his fire's worth there is 1/(N + 1)
  (`CaptainLayer.OwnShare`), beside her done-for worth, how much she matters and how much his side's aim wants her. It
  replaces `FireCrowdShare`'s range shift (0.25 longer a battery). `FireOrderTests.A_line_at_will_spreads_its_fire_by_its_share_not_all_on_the_nearest`:
  four battleships, dreadnoughts at 7 and 10 km: two on each (by the old shift, three on the near one).
- **Why batteries leave a target** is counted (`CaptainLayer.ChiefShifts`, `OtherShifts` by `ShiftWhy`: order, lost,
  foul, wreck, reach, bears, better) and printed by `--ammo`.
- **Measured** (`--ammo`, the same 2 seeds as the baseline):
  - *The pile-on is gone.* Squadrons' chief shells at a target 4-5 ships fire on 31% → 16%, on one alone 40% → 64%; the
    Dogger Bank's at 4 or more 15% → 5%. Shifts by "another is better" at the Dogger Bank 1,259 → 727, ranging on a new
    target 5,269 → 3,661 shells.
  - *The volume hardly moves:* the shells go elsewhere rather than unfired. Dogger Bank capitals: Britain 74% → 72% of
    the outfit, Germany 74% → 81% (dry 5 → 4); Tsushima's Japanese 74% → 68% (dry 8 → 6); squadrons' British 43% → 63%.
  - *Where batteries leave a target now:* at the Dogger Bank still mostly "another is better" (727 of 1,007); at the
    Jutland lost from sight leads (4,379; better 4,078; wrecks 1,725): the glimpses the record had.
- **The balance panel** (`balance --seeds 8`, `balance/sc1-before.json` against `balance/sc1.json`): 8 → 9 of 14 sides
  within about twice the record, the record's loser losing more in 28 → 27 of 44; fc-history unchanged (one ship). Fire
  spread rather than piled on does more: the Dogger Bank's British losses 2.6 → 1.0 (record 0), German 6.4 → 7.4 (1);
  Tsushima's Japanese 6.9 → 6.0 (0); but **the Jutland's German losses 27.8 → 37.5 a seed** (record 11), British 19.8 →
  21.8 (14), British heavy shells 6,925 → 8,969 a run and their hits 1,974 → 3,276. Less waste per shell makes the volume
  that's left deadlier: SC3 and SC4 are what cut the volume, and the gunnery's accepted 3.5x over the record
  (error-sources.md) stays the other half of the deadliness.

### SC2 Spotting under concentration (research first)

A salvo's splashes confused with another ship's salvo landing about her at the same moment, not only single shells (Tiger
on Lion's at the Dogger Bank). `SalvoIdentifiedShells` came from the research (manual-gunnery-research.md §5); check it
before changing it.

### SC3 Ammunition as a command concern

The officer's worth floor rises as the magazines empty from `FireWorthLowRounds` (a quarter left today); the research has
ships firing below about a third only while straddling or close in.

### SC4 Rapid fire earned

Firing for effect after a miss, the next salvo waits on the corrected salvo's spot (slow fire: "held back to apply spots");
at the loading rate only while straddling. Mostly the medium guns: a heavy gun's spot cycle is about its reload.

Not here: how long the sides fight (BL5's, the commanders' judgement); no fight in progress is cut short.

## Measuring

`shipgen scenario X --play --seeds N --ammo`: per side and type the outfit fired, rounds a gun a minute over each ship's
first to last minute of fire and over the minutes her chief guns were firing, ships under a third and dry; shells by
phase; a gun's cycle in fire for effect over the drill reload; targets taken and the ranging they cost; why batteries left
a target; shells by the ships firing on her target; fire for effect's spots.
