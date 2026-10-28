# Plan: the crew (a 4.5 deepening)
Status: **agreed with the user 2026-10-27**; **replanned from the research and agreed 2026-10-28** (decisions 6-10)
Updated: 2026-10-28

What a ship's men do in a fight: where they stand, what a station can do with the men left at it, who goes up to fill
the gaps, and when there's nobody left to fight her. It builds on E6's casualties (`shells-armour-damage.md`,
`damage/16_casualties.md`), fire's parties (`fire.md` B2) and damage control (`damage-control.md`). Started by the
user's Sverige slugfest (2026-10-27): "Once the crew is gone the ship is de facto dead, now the ship basically becomes
a zombie." The research for N3 is `research/simulation/crews-under-fire.md` (2026-10-28, cited below as CUF).

## The aim

1. **One book of men.** Every man stands somewhere, and a hit counts whoever stands where it bursts. That includes the
   fire parties, which today are a pool of their own.
2. **A station works with the men at it.** A gun with half its crew loads slower, and with nobody it's silent. The same
   goes for directors, the conn, the stokehold and the ammunition supply. A ship with her crew gone is dead in fact, not
   a hull that fires on.
3. **Men go up to fill the gaps.** Reliefs from below and from the disengaged side man the guns again. The toll rises
   for as long as she keeps fighting, and it levels off when she stops fighting, not before.
4. **Men hold, mostly.** Regular crews of every training level almost never left a working gun unordered (CUF summary
   1). Stations empty from casualties, fire and gas, wrecked guns and orders. Leaving on their own is the rare case:
   green, worn crews under heavy HE, at stations hit hard, with no officer.

## What there is today (checked 2026-10-27)

- **Where the men stand** (`Shipgen/Crew.cs` `BattleStations`): gun crews at the mounts (0.4 of a barbetted mount's
  in its barbette), the engineers in the machinery by volume, a command party on the bridge (a quarter aft),
  director crews, two at the steering, and the rest as "repair" over the accommodation and stores by volume. Sverige's
  569 men: guns 186, handling 102, machinery 162, repair 75, command 20, directors 14, torpedoes 8, steering 2.
- **Casualties** (E6, `Sim.Combat/Casualties.cs`): per group (a component's or cell's `BattleCrew`, less
  `ShipCondition.Gone`), from pieces, blast, the lane, spall and flash. The walking wounded come back in 15-45 min.
- **Nothing reads the men left for what she can do.** `Functions.Evaluate` sets `MountWorks`, `DirectorWorks`,
  `Conned` and `PowerCap` from the parts' health alone. `CrewLost` is read by the benches, the snapshot and the card,
  never by the sim. A gunhouse whose crew are all dead fires at its full rate.
- **Fire parties are a second book** (`Repairs.Make`): `Complement × PartyShare` men, made at the first fire, apart
  from the stations' groups. A hit in a working party's section thins it by a flat chance (`FireHitMenChance`,
  `FireHitMenKeep`). E6's pieces never reach them, while the "repair" men in the quarters they came from can still be
  killed a second time.
- **Fall-out** (`FallOutRule`): speed, main guns, list and water. No crew term, and none is wanted (see N5).
- **Crew quality** (`CrewQuality`, `SimSettings.cs`): green, average, trained, elite, one a side, read today only by
  the gunnery rows (`GunneryCrews`: layer and rangetaker error, the battle factor on reload).

## What's wrong, measured

`shipgen hits sverige --shooter sverige --battery 1 --range 3000`: 152 mm HE, 60/40 with common, at 2° of fall.

| Hits a ship | Out of action (of 569) | Killed a hit | Main mounts | Fires burning |
|---|---|---|---|---|
| 25 | 61 | 0.54 | 1.5 of 2 | 2.3 |
| 50 | 74 | 0.38 | 1.1 | 5.6 |
| 100 | 79 | 0.23 | 0.6 | 9.8 |
| 200 | 85 | 0.12 | 0.1 | 17.4 |
| 500 | 90 | 0.06 | 0.0 | 28.8 |

- **The early hits are right.** Her first 25 hits put 2.4 men a hit out of action. Tsesarevich lost 59 to 15 hits and
  Retvizan 48 to 18. Oryol lost 123 to 76 hits at Tsushima. The bench's 79 at 100 hits of 6" alone isn't far off.
- **What comes after isn't.** The ~120 men a 152 mm shell can reach (the bridge, the directors, the 75 mm crews, the
  parties in the flats) are used up by about 50 hits. Then the toll stops, because nobody goes up to replace them. The
  other 450 sit behind 100-200 mm of armour that the shell can't get through. Half the HE bursts outside on the side
  armour, and what gets inside finds empty quarters. That much is physical.
- **And she's a zombie.** With her bridge and directors cleared, 29 fires burning and nobody fighting them in the open,
  her guns still fire at full drill and her parties' men are never in the way of a shell.
- **The record has no 500-hit ship.** Ships pounded that hard (Suvorov, Bismarck) were silent, burning wrecks long
  before. What's wanted is the shape, not a number: **the toll may flatten only once she has stopped fighting.**

## The principles

- **Physics and the record, no guard rules.** A station's work by its men present is drill arithmetic: who lays,
  who rams, who passes. It's not a morale factor and not a lumped "crew quality" knob.
- **No hard switches.** A station's work falls smoothly with its men down to the few it can't do without. "Nobody
  there, nothing done" is a physical step and allowed.
- **Doctrine, not ships.** How a navy fights her men (when the secondaries are manned, who relieves whom) is the side's
  doctrine, set by the scenario from the record (decision 10). How many are kept back for the parties sits with the
  damage-control doctrine, one a period, as E8b set them. Nothing of this is baked into a ship or a period table, and
  there are no per-navy damage values.
- **The captain is the person.** At the level we play, the captain's personality (his temper) is what matters most.
  Officers, crews and their men are abstracted: counts, a few numbers, no personalities (decision 7).
- **Rough where it doesn't decide.** Reliefs move as counts between groups after a walking delay. There are no
  individual men and no paths through the ship.
- **The captain's, not the player's.** Where the men stand is the captain's call by the side's doctrine (decision 2).
  The player sets the doctrine before action, and doesn't manage the men in it.
- **All through where the men stand.** Steadiness acts only by moving men (under cover, or present and not working).
  There's no factor on hits, rates or aim for being under fire (CUF R11: none was found beyond casualties, damage,
  smoke and visibility; and the standing rule against lumped battle factors).
- **What lands near the men, not the ship's hit count.** Lion took 14-16 heavy hits at the Dogger Bank for 1-2 killed
  (CUF §2). A station's duress comes from its own casualties and the bursts around it.
- **Same at any compression.** Reliefs, station shares and duress change on game-second clocks and on hits, the same
  in fine and coarse.
- **No garbage per frame.** Group arrays at spawn. `Functions.Evaluate` runs when the condition changes, as now.

## Decided (the user, 2026-10-27)

1. **Reliefs follow what a reasonable captain orders** (the user: "what would a reasonable captain order?"). The
   default, from the record's practice, **as revised from the research 2026-10-28 (decision 8)**:
   - Within a crew, the next man steps up: that's the crew thinning (N2), not a relief.
   - The conn and the steering are filled before the guns (Reina Cristina drew her steering crews from the guns).
   - First of the reliefs, the men under cover by the captain's order: the crews of guns that can't bear (the
     disengaged side) and of guns not manned by doctrine. They're the ship's ready reserve, and they go back when she
     turns or the guns are wanted.
   - Next, the crews of mounts that are wrecked or jammed for the battle, and those mounts' handling men.
   - Then the idle hands (the hotel staff, the band, the stewards), the repair men above the floor, and the stokers the
     steam asked of them doesn't need: for the supply first, then the guns.
   - The main battery is filled before the secondaries, unless torpedo craft are the threat.
   - A live mount's handling room is kept (HandlingRoomProtected, the default): guns without ammunition are as silent
     as guns without crews. A side's doctrine may instead keep its reserve in the supply (ReserveInSupply, the USN's
     1899 bill): the supply's spare men go first and the idle hands refill the supply. The parties' floor is kept.
   - Reliefs wait for a lull before going into a station under heavy fire, unless a bold captain sends them.

   This is doctrine: the side's doctrine may order it otherwise, and the player picks the doctrine at launch, not the
   men (decision 2). It's open to revision once it plays.
2. **Posture is part of the reliefs: the captain's and the crew's, not the player's** (the user: "I don't think as a
   player I want to care about the decision on that level"). They're one question: where each man stands now. The
   captain keeps men at the stations that are fighting and sends the rest under cover. Those men under cover are the
   reserve the reliefs come from. Built in N3a. N4 is gone. The player's only say is the side's doctrine at launch
   (standing orders, later the difficulty sliders).
3. **Striking colours and abandoning ship are the captain's call, after this plan.** A command step of its own, once a
   ship with nobody left to fight her exists. **And the captains stop shooting at burning wrecks that show no sign of
   life.** If one of her guns fires, she's taken under fire again at once, as happened in the record (Wiesbaden at
   Jutland, Suvorov at Tsushima). This is built here as N5b, because N2 is what makes a ship really fall silent.
4. **Cover is fine-tuned later** (gunhouse ports, conning tower slits, the 0.45). N0 only measures it.
5. **A morale system lite, inside N3** (the user, 2026-10-27: "green crew is more easily scared and goes to cover
   whereas seasoned veterans brave the onslaught"). The men's steadiness under fire is read from the side's existing
   `CrewQuality` (the launch screen's Conditions), per ship later with the difficulty sliders. It's about the men at
   their stations only. A ship breaking off or fleeing is her captain's call, with striking and abandoning after this
   plan. Built in N3b, with the research's numbers, read from the men's experience (decision 6).

## Decided (the user, 2026-10-28, on the research)

6. **Experience and work-up, two numbers in place of `CrewQuality`** (the user: "the crew is great as a fighting crew
   but inexperienced with that particular ship"; crews were moved into new ships, and the campaign will have them
   learn). Both are floats: an enum with an experience bar and a discrete jump felt off, and the rows switched by level
   broke the no-hard-switches rule.
   - **Experience, the men's** (0-3: green 0, average 1, trained 2, elite 3, the names as presets on the whole
     numbers): their years at sea and their battles. It goes with them to another ship. It sets steadiness (N3b) and
     the ceiling of the crew's gunnery.
   - **Work-up, this crew in this ship** (0-1): the drill as a team, her director and her guns' ways, her valves and
     passages. It fills over about six months of work-up, with diminishing returns, and drops when the crew moves (a
     sister keeps much of it).
   - **Gunnery reads both:** level = experience × (0.3 + 0.7 × work-up) [EST: Chester, long-service men some weeks
     into a new ship, shot green to average; CUF §9's "new to the ship: down one for gunnery, none to one for
     steadiness"]. The `GunneryCrews` rows and N3b's nerve rows become anchors at the whole numbers, read between
     them linearly.
   - **Before the campaign:** scenarios give experience per side (the launch screen's Conditions, the picker as now)
     and work-up defaults to 1, worked up; a scenario may lower it for a ship (Chester at Jutland, Tiger at the Dogger
     Bank). The game plays as now.
   - Work-up for the parties and the engines, and the ship's own teething (time in commission: Arethusa's guns
     jammed because she was new), are later (after this plan).
7. **An officer a station, rough** (the user: "at the level we play at the captain is the critical thing whose
   personality matters the most, rest are abstracted"). Present or hit, no person: one of the station's men, hit by
   E6's draw like any of them, a relief from a small pool walking in like any relief.
8. **The relief order revised from the research** (decision 1 as it now stands).
9. **Civilian and committee crews after this plan**, as ship-wide overrides when a scenario needs them: Otranto's
   drifters first (scenarios.md 16).
10. **When the secondaries are manned is a navy's doctrine, not a period's or a ship's** (the user: "we don't want
    period things in ships per se. It should be a navy doctrine, which on our scenarios is set as per the historical
    part and in campaign we'll think of mechanics"). `ManSecondaries` is a field of the side's doctrine
    (`command.md` "Doctrine", `doctrine.json`, a `DoctrineField`): each scenario sets it from the record, the default
    preset holds a middle value, and the campaign's mechanics come later. `ReliefOrder` goes with it, a navy's practice
    as well.

## Decided (the user, 2026-10-28, on N1)

11. **The German big ships' fewer killed a hit after N1 stays for now** (the user: "we'll think about this later once
    all steps are done and effects of duress etc are in"). Judged again against E6's fit at the end of the plan.
12. **Reliefs take only idle hands, the repair parties' among them** (the user: "Only idle hands should matter. So if
    repair crews are mostly idle then captain can grab some, at the risk of having to toss them back for repair work if
    it's needed"). No floor of repair men kept apart: a party standing by, with no fire or water job, is idle hands the
    captain may draw reliefs from (decision 1's "the repair men above the floor" read so), and he sends them back when
    damage control needs them, so a party's men come off the guns for a job (N3a).

## What the research says (2026-10-28)

CUF gathers over 40 cases in six navies, Yalu to Samar, with WWII and land evidence as checks. Against this plan:

**Confirmed.**
- The shape of N3: duress per station, fading; a threshold by quality; a share going to cover; the men under cover as
  the relief reserve; all through where the men stand (CUF "Recommendations").
- Casualties, not flight, empty stations. Southampton's guns stopped "all through lack of flesh and blood", 75% of her
  upper deck hit in 3.5 minutes, morale "splendid" (CUF §1.6). This is N2's job, not N3b's.
- The parties are a source with a cost: Vizcaya drew fire-party men to the guns and lost her fires (CUF §4). N1 and N5.
- Wrecked and idle mounts' crews as reliefs (Zhenyuan, Queen Mary's X turret, Scharnhorst, Lützow), and crews under
  cover as the first reserve (Suvorov's 47 mm crews, Oryol's light guns) (CUF §4). Decision 1.
- Main before secondaries unless torpedo craft: not contradicted.
- No under-fire gunnery factor (CUF R11). Training and method moved hit rates by 2.5-4.5 times; nerve explains none of
  it. The existing `GunneryCrews` rows carry quality.

**Changed.**
- **Quality matters less to staying than decision 5 supposed.** Green crews held to very heavy losses (Vizcaya "until
  there were no men left"; Coronel's reservists). The threshold's spread from green to elite is narrow (2.5 times);
  quality acts mostly through how many go, how soon they come back and the officer (CUF R1, summary 3).
- **Steadiness and gunnery part company.** Kent's reservists held and shot "rotten"; the Spanish held and couldn't hit
  (CUF §9). One `CrewQuality` for both overstates a green crew's flight. Decision 6: experience and work-up.
- **Ordered cover is the big lever, unordered flight the small one.** Rossia cleared her quick-firers at Ulsan and her
  sister Gromoboi didn't: 44 dead against 87 (CUF §1.6). The captain's order (N3a) moves more men than the crew's
  nerve (N3b).
- **The relief order needs four changes** (CUF R9): the conn and steering outrank the guns (Reina Cristina); the handling
  room is a doctrine option, not a law (the USN's 1899 bill kept the reserve in the powder division); stokers go free
  as the steam asked of them falls (Southampton, Blücher); reliefs wait for a lull before going into a station under
  heavy fire (Southampton, the USN's "wait for lull"). Decision 8.
- **The relief pool is small** (Semenov: "there are no spare men on board a warship"), perhaps 40-80 men on a
  battleship under cover (CUF §4). Re-manning runs on three clocks: seconds within a crew, minutes from idle mounts, a
  lull for stokers.

**New.**
- **Untenable spaces** (CUF R7): fire, flash, steam, gas and flooding empty a station whatever the men's quality
  (Laiyuan's broadside "surrounded by flames"; Derfflinger's transmitting station; Emden's engine room). Physical,
  not nerve. In N2.
- **In-crew succession is not a relief** (CUF §4): the next man steps up in seconds and the least skilled duty drops
  (the 1866 thinning tables, 16 men to 6). It's N2's share, falling smoothly.
- **Reliefs from other trades work worse** (Emden's "unskilled chaps"): counted at a weight in N2's share, not as a
  rate factor.
- **The officer** (CUF R4): "the strongest single steadying factor the evidence supports". Present, his men hold
  longer and leave only on order; hit, they waver until another comes. Decision 7, rough.
- **Three states of a shaken crew** (Semenov on Suvorov, CUF §1.6): present but not working until an officer acts
  ("frozen"); a short rout after one blast among a group; hiding until rallied. Men who went on their own aren't in the
  relief pool.
- **Sensitisation** (CUF R2): a station that has once gone over its threshold stands less the next time.
- **When to man the secondaries** as a doctrine field with period defaults (CUF R8; Colossus kept "two starboard 4-in
  guns manned" at Jutland).
- **The last gun outlives the main battery by hours** (Suvorov's 12-pdr, Emden's one gun, Scharnhorst's Caesar; CUF
  §8). The Sverige case's wanted shape.

**Left out, and why.**
- Civilian crews and committee crews (the Dardanelles trawlers, the Otranto drifters, Slava 1917): ship-wide overrides
  outside the station model (CUF R6). Decision 9: after this plan.
- Captains taking ships out (Jiyuan, Nebogatov): the captain's temper and the step after this plan, as decision 3 has it.
- Contagion (CUF R5): no source quantifies it, and the panic literature points the other way. None.
- The blast knocking a crew down for 10-60 s (Colossus): under a salvo's time, not decisive. Not built.
- The straddle and searchlight term for green crews (CUF R3): kept, at half weight, green only. Civilians would take it
  at full weight with decision 9.
- Readiness (Savo's Condition II) and weapon failures in new ships (Arethusa, Tiger): the difficulty sliders' and
  gunnery's, after this plan.
- The research's ask for ship totals of wrecks (N0) is answered in CUF §8, with a warning: killed totals on lost
  ships are dominated by the sinking and the water, so they can't be read as station casualties.

## The yardstick

| Case | Record | Wanted |
|---|---|---|
| E6's per-hit fit (G1-G7, 10 hits a ship) | as in `shells-armour-damage.md` E6 | unchanged within the noise: reliefs and nerve come after the first hits |
| Oryol, Tsushima: battleship, about 74 hits (Campbell) | 43 killed, 80 wounded; "main losses behind armour from splinters entering ports/slits" (Kostenko) | about 100-150 out of action |
| Chester, Jutland: average (new ship), open-backed shields, ~17 × 15 cm in minutes | 29 killed, 49 wounded; four guns out; no recorded flight | about 75-80 out; a small share leaving at most; she still steams and fights |
| Southampton, Jutland night: trained, four cruisers at 1,500 yd | 75% of the upper deck hit in 3.5 min; guns silent from casualties; stokers re-manned them in a lull | silent from casualties, few or none leaving; reliefs only once the fire stops |
| Rossia against Gromoboi, Ulsan (scenario 5): sisters, one cleared her light guns | 44 against 87 killed; 200 against 257 casualties | one navy's doctrine (light guns under cover), Gromoboi's captain bold: about half the dead, about 20% fewer casualties, fewer light-gun rounds on Rossia |
| Kent, Falklands: reservists in casemates, 37 light hits | 4 killed, 12 wounded, most at one gun | about 16 out; no leaving; poor shooting from gunnery quality, not duress |
| Emden, Cocos: trained, fire control wrecked early | fire slackened as the rangefinders went; by ~80 min one gun, an officer and "unskilled chaps" | accuracy falls with control damage first (the F-steps); one gun late, worked by reliefs |
| Suvorov, Tsushima: green, worn, 5 h of HE | short routs after blasts; frozen men restarted by officers; covered light-gun crews used up as reliefs; a 12-pdr firing late | the same states seen; the reserve gone within hours; a last gun late |
| Vizcaya, Santiago: green gunnery, steady crew | the batteries fought until too few men remained; parties drawn to the guns, fires spread | silent from want of men; fires gaining once the parties are drawn |
| Zhenyuan, Queen Mary's X, Samar, Blücher: trained crews holding | no unordered leaving; idle crews to working guns in minutes; Blücher's stokers to the supply once slowed | trained and better: no leaving at a working gun; stokers freed by speed lost |
| Graf Spee: 18 × 6" + 2 × 8" | 36 killed, 60 wounded | in the band |
| Sverige under 500 × 152 mm (the user's case) | — | toll rising for as long as her secondaries fight; the last gun outlives the main battery; silent from want of men before the toll flattens; fires gaining once her parties are spent |

Ship totals of lost ships (CUF §8) are for what still worked late, not for casualties.

### Today (N0, 2026-10-28)

`shipgen crew-history` (20 runs a case; killed and wounded a ship, out of action the median at the end of the fire,
the walking wounded back after 15-45 min). Southampton's and Emden's hit counts aren't in the record (18 and 100
[EST]); Suvorov's 150 [EST] were never counted.

| Case | Record | Today |
|---|---|---|
| Oryol, 74 hits over 5 h | 43 killed, 80 wounded | 30 killed, 75 wounded; 65 out at 5 h; 152 mm mounts 3.2 of 6, 75 mm 9.2 of 20; conned 80% |
| Chester, 17 × 15 cm in 5 min | 29 killed, 49 wounded; four guns out | 6 killed, 17 wounded; 22 out; 4.4 of 5 main mounts working |
| Southampton, 18 × 10.5 cm at 1,500 yd in 3.5 min | 89 casualties; guns silent | 5 killed, 8 wounded; 8 out; 4.5 of 5 mounts |
| Kent, 37 × 10.5 cm over 90 min | 4 killed, 12 wounded | 12 killed, 43 wounded; 38 out at 90 min (31 of them guns' crews), 30 at 2 h |
| Emden, 100 × 6″ over 100 min | 115-134 killed, 56-69 wounded of 316; one gun late, then none | 60 killed, 132 wounded; 146 out; 2.6 of 12 mounts at the end; lost in 75% of runs (she was beached) |
| Suvorov, 150 hits over 5 h | all lost; a 12-pdr each side late | 40 killed, 110 wounded; 90 out of 790; afloat in every run; 152 mm 2.8 of 6, 75 mm 7.3 of 20; conned 35% |
| Sverige, 500 × 152 mm over 2 h | - | 44 killed, 112 wounded; out 81 at 30 min, 89 at 60, 98 at 120, flat from about 60 min while 152 mm mounts 4 of 8 still work (main 1 of 2); the 75 mm all out by 30 min; conned 0% from 60 min; the parties' men 45 at 30 min, 10 at 120 |
| Graf Spee, 18 × 6″ and 2 × 8″ over 80 min | 36 killed, 60 wounded | 19 killed, 25 wounded |

- **The light shells kill too few** (Chester a third of the record, Southampton a seventh, Graf Spee half) and Kent's
  casemates too many (three times). Both are E6's placement (shells-armour-damage.md: "the stance is the fix"), not
  this plan's; the crew steps measure against today's figures, and Kent and Chester are flagged for E6's follow-up.
- **Sverige is the zombie in numbers:** the toll flattens with half her 6″ guns in action, nobody at the conn and the
  parties used up.
- **Emden's casualties are on the record**, but she sinks where Emden was beached; not this plan's (damage control).
- **The hits bench's curve** (`shipgen hits sverige --shooter sverige --battery 1 --range 3000 --per-ship 500 --curve 25`,
  20 ships): 25 hits 60 out, 50 72, 100 77, 200 82, 300 85, 500 89. That's the table in "What's wrong, measured" again.
- **E6's per-hit fit** is damage-history's killed and wounded a hit (20 runs): German ships under British shells 1.25-1.91 /
  2.87-4.55, British under German 0.58-0.92 / 1.81-2.95 (G1 1.94 / 3.00); Chester 0.36 / 1.02 (G6b 2.0 / 2.4); Kent
  0.35 / 1.21 (G6a 0.17 / 0.33); Orel 0.30 (G2a 0.58), Mikasa 0.55 (G2b 1.69); destroyers 1.4-2.8 / 3.6-6.8 (G7 2.2 /
  4.5). The later steps keep these within their noise (reliefs and nerve come after the first hits).
- **Not benched yet:** Rossia against Gromoboi (scenario 5, with N3a's cover doctrine); Vizcaya (no Spanish armoured
  cruiser among the designs, and her case is the parties drawn to the guns, N3a); the crews that held (Zhenyuan, Queen
  Mary's X, Samar, Blücher) are behaviours, N3's.

## The steps

- **N0a Experience and work-up** (decision 6), a small step of its own before the rest. `CrewQuality` goes; a side's
  experience is a float 0-3 and a ship's work-up a float 0-1 (default 1). `GunneryCrews` rows are read between the
  whole numbers by the gunnery level (experience × (0.3 + 0.7 × work-up)). The sim command carries experience in
  thousandths, so replays stay identical. Scenarios, the setup and the launch screen take the names as presets
  ("trained" = 2) or a number; a ship's `"work_up"` in a scenario. Parity: with work-up 1 and the names on whole
  numbers, every bench and hash is unchanged.
  **Done 2026-10-28.** `Experience` (Sim.Core: the names, `TryParse` a name or 0-3, `Name`) in place of the enum;
  `GunnerySystem.SetExperience` (the old `SetCrew`'s code, 3) in thousandths; `ShipSystem.WorkUp` and the store's
  work-up column (thousandths, hashed only off 1); `SimSettings.GunneryLevel` (written experience × (1 − 0.7 × (1 −
  work-up)), the same number, so a worked-up crew's level is its experience exactly) and `GunneryCrewAt` (the rows
  read linearly, exact on the anchors; `GunneryCrew` a struct, no garbage per salvo), the 0.3 as
  `WorkUpGunneryShare`. A scenario's `"crews"` take a name or a number, a ship entry's `"work_up"` a number for all
  of them or by name; the setup writes a number between the names as one; the launch screen's gun crews add the
  halves (0.5, 1.5, 2.5); `duel --crew 1.5 --work-up 0.5`. Parity: coronel, tsushima, dover-strait, aland,
  prizes-war and jutland at 90 min (hashes and replays), the duel at each name and fc-history all identical to before.
  Between: the duel's rate falls with the level (green 0.99, average 1.09, 1.5 1.14, trained 1.22 rounds a gun a
  minute; trained new to the ship, level 0.6, 1.05); its hit counts at 10 runs are noise, as before.
- **N0 Measure.** The hits bench prints, after K hits: men out by station kind, mounts working by battery, directors,
  conned, the parties' men and their jobs. Add a `--curve` line of the out-of-action count every 25 hits. The
  yardstick runs and today's figures are recorded here. Add a timed bench of the yardstick's short cases (Chester,
  Southampton, Kent: a ship under a given fire for minutes), since a hit count alone can't show reliefs or a lull. The
  research ask is answered (CUF §8).
  **Done 2026-10-28.** `BattleStations` (Sim.Combat): each crew group's station (guns, handling, aa, torpedoes,
  machinery, command, directors, steering, air, repair; Shipgen's own names), and a ship's men and men out by station
  (`Tally`). The hits bench prints, after the hits and the settle, the men out of action by station of the men there,
  each battery's mounts working, the directors, the conn, the parties (men at a fire, on a water job, neither) and the
  gun crews turned to; `--curve N` the out-of-action count every N hits. damage-history's timed fire moved into
  `GivenFire` (its output identical before and after), and `shipgen crew-history` runs the yardstick's cases under it:
  the men out through the fire and after, by station, the batteries, the conn and the parties, at a quarter, a half,
  three quarters and the end of the fire and 5, 15 and 30 min after. The research ask is answered by CUF §8 (ship
  totals for ships pounded to wrecks) and the cases above. Today's figures are in "The yardstick". Found on the way
  (bugs.md, Shipgen): a transmitting station's men summed but standing on no cell in 13 designs.
- **N1 One book of men.** Parties are made from the "repair" groups' men, not from `Complement × PartyShare`. A party
  at work stands in its target's cells and is a group there: E6's pieces, blast and spall hit it as anyone. Fire's
  `Hurt` and `Caught` take from that group. `FireHitMenChance` and `FireHitMenKeep` go. Turned-to gun crews (`TurnTo`)
  come from the mounts' groups. The doctrine's `PartyShare` becomes how many of the repair men muster into parties.
  Fire's yardstick re-run (fire.md B5).
  **Done 2026-10-28.**
  - **The book:** a station's men are its battle crew less the gone (`Gone`) and the away (`Away`, new: in a party).
    `Repairs.Make` plans her parties from the repair men there at her first fire (`PartyShare` of them, two to eight
    parties); a party takes its men from the repair groups (each its share, whole men) only when it's first sent, so
    until then they stand by in their quarters and E6 meets them there as before. It musters in the repair space nearest
    its section (`Home`), walks from where it was, and works at its job's place (`Place`: a fire's cell or part, a deck
    fire's cell under it, a boundary's dry side, a cell's holes or pump, the steering room, the part or room under
    repair). The turned-to gun crews are taken from the mounts' men, standing by at the mount that gave most.
  - **E6 meets them where they stand:** `Casualties.Draw` rolls a party's men with its place's risks, after the men who
    live there; its walking wounded walk back to the party. The early-outs that skip an empty space (`Here`: its men and
    the parties there at the pass's time, which `Begin` now takes) let the bursts, lanes, spall and near misses find a
    party in an empty store. Fire's flashover (`Caught`) reaches the party in the room the same way. Smoke (`Hurt`) still
    takes from the party at its fire. `FireHitMenChance` and `FireHitMenKeep` are gone.
  - **`PartyShare`:** the repair men are a median 12% of a complement (8-17% over the designs), the old shares of the
    complement were 0.10 and 0.12, so the new shares of the repair men are 0.85 (1890) and 1.0 (1905 on): the same men on
    the median ship. That leaves no repair men above the floor for N3's reliefs on most ships (decision 1's "the repair
    men above the floor"); reliefs come from the covered crews, the wrecked mounts' men and the stokers first, and the
    doctrine can lower it. Decided (12): no floor; a party standing by is idle hands for the reliefs, sent back for a job.
  - **Found:** `Muster`'s choice is by section and size, not by protection; the standing-by parties of a fired-on ship
    gather there.
  - **The yardsticks (before → after, 20 runs a case):**
    - **fire-history:** Suvorov ablaze 5 → 20% of runs (record: ablaze in 20-40 min, B5's open gap), her killed and
      wounded 106 → 162; Orel's 111 → 164; Santiago unprepared ablaze 70 → 65%, killed and wounded 239 → 186 (her men
      muster out of the burning quarters); the rest unchanged within a run or two.
    - **dc-history:** every case's losses the same but Oslyabya, 0 → 5% (one capsized at 164 min; the record has her
      capsize, the wanted most runs).
    - **crew-history:** Oryol 105 → 157 killed and wounded; Suvorov 150 → 228; Sverige 156 → 207, out of action at 2 h
      98 → 128 and still rising (her parties used up at work: 45 → 19 men at 30 min), lost 0 → 10%; Chester, Southampton,
      Kent, Emden and Graf Spee unchanged within a few men.
    - **damage-history (E6's fit):** the same within the noise for the British ships, the cruisers, the destroyers and the
      later cuts; Orel 0.30 → 0.43 killed a hit (her fires' parties); **the German big ships 7-12% fewer killed a hit**
      (60 runs, the same hits: Jutland's plate count 1.34 → 1.25, Seydlitz 1.91 → 1.75, Dogger Bank's Lion 0.74 → 0.65),
      their men drawn out of the upper quarters into parties working below. Past the noise, against wanted "unchanged";
      the record has the German ships higher still (G1 1.94). Decided (11): kept for now, judged again at the plan's end.
- **N2 Stations work by their men.** A share per station: men present over the need it was sized for (`Crew.GunCrew`,
  `CommandK`, the directors' and the plant's crews).
  - **The share is the crew thinning** (the 1866 tables): the next man steps up at once, the least skilled duty drops,
    so the share falls smoothly. Men of the station's trade count 1. Reliefs from another trade count 0.7 [EST] (CUF
    R9: a crew rebuilt from mixed reliefs loses 10-20% of its rate). Frozen men (N3b) count 0.
  - **Mounts:** the reload stretched by the share. Below the few a gun can't be fought without (layer, loader, rammer),
    it's silent. A barbette's handling men set the supply in the same way.
  - **Directors and eyes:** short-handed, the rangefinder and the plot are slower (the F-steps' sigma), and silent with
    nobody.
  - **Command:** the conn passes to the next manned station (the conning tower, aft control), in 5-10 min when it
    shifts the control (Marlborough 9 min, CUF §4). With none, she isn't conned (`Conned`).
  - **Machinery:** steam by the stokehold's men, with a floor. A boiler room keeps its steam for a while on what's
    already in the fires.
  - **Untenable spaces** (CUF R7): a station whose space has a developed fire, flash, steam, gas or water past the
    men's height holds nobody at work. Its men go to the nearest cover and count as reserve. They go back when the
    space clears. Read from fire's and flooding's own state, never through duress.
  - Read in `Functions.Evaluate` from `Casualties.Men` (it already runs on every hit).
  - **N2a built 2026-10-28 (mounts, supply, directors, eyes).** `BattleStations.Share` (men at work over the group's
    battle crew) and `Mount` (the gun crew's share, 0 below `GunMinMenPerMm` × calibre a barrel, 1 to `GunMinMenMax` 3
    [EST]; the barbette's handling men, 0 below one a barrel). `Evaluate` (now with the settings and the time) keeps
    `MountCrew`, `MountSupply` and `EyeCrew`; a mount works only manned (its lasting state too, so her fall-out reads
    guns silent for want of men), a director or an eye only with somebody there. `CombatStore.Fired` takes the reload
    over the crew's share and the hoists over the supply's (the fine and the coarse tier alike), the readings come at the
    eye's rate times its share. A walking wounded man's return is now a change (`Clear`), so his station counts him
    again. The relief's 0.7 and the frozen 0 wait for N3. Not done: the transmitting station's plot (its rooms carry no
    battery), the tops' lookouts (no men are placed there).
    Measured: the guns lost are mostly lost with their mounts, so the counts move little (Emden's main at 100 min 0.8 →
    0.5 of 2, her 105 mm 1.1 → 0.9 a side; Sverige's the same); damage-history's men unchanged. `LineOfFireTests` now run
    without damage: their friend, shot up by the enemy, fell out of the scene with her guns silent.
  - **N2b-d built 2026-10-28 (the conn, the stokehold, untenable spaces): N2 done.**
    - **The conn** (`Functions.Conn`, `BattleStations.Conn`): forward while a bridge or the conning tower stands and any of
      the command party on her bridge blocks is there; lost, aft control if it stands manned, once the control has
      shifted (`ConnShiftS` 420 s [EST: CUF §4 5-10 min, Marlborough 9]; on the outage clock, `ConnAt`, `ConnShiftMs`),
      not conned meanwhile; else nowhere. `Conned` is still read only by counterflooding by hand and the magazines'
      flooding; what more it costs is the signals' (5.1).
    - **The stokehold** (`BattleStations.Stokehold`): a boiler room's steam × (`StokeholdFloor` 0.3 + the rest by its
      stokers there over its crew) [EST], a drop held `StokeholdHoldS` 600 s on what's in the fires (`Stoke`,
      `StokeUntilMs`, the outage clock); the lasting state at the stokers' own; men back count at once.
    - **Untenable spaces** (`BattleStations.Untenable`, `AtWork`): a developed fire in a station's space (its part's or
      cell's fire space, mapped once a class: `PartSpace`, `CellSpace`) holds nobody at work there; the men aren't
      casualties and still stand where the hits find them (rough), and are back when it's out (a flashover and a fire
      out are now changes of the fire step). Water past the men's height is flooding's own: a room flooded out of action
      takes its function's node. Flash, steam and gas: not modelled as spaces yet.
    - **Measured (20 runs):** crew-history's men the same; Emden lost 70 → 85% of runs, Suvorov conned 35 → 40% at 5 h;
      damage-history's men a hit the same, its losses a run or two either way (the fire step's changes re-work the
      functions more often); dc-history the same but Seydlitz holding 24 kn 10 → 5%. Sverige's conn is 0% from an hour:
      her aft control goes with her upper works.
- **N3a Posture and reliefs: the captain's order** (decisions 1-2). Where each man stands, decided by the captain at
  each think.
  - **Closed up or under cover, per station**, by the side's doctrine field **`ManSecondaries`** (decision 10): Always,
    InRange (a target worth its fire in reach), or TorpedoCraft (with a watch of two guns a side closed up), given
    for casemated and open guns apart. The default preset holds InRange for both [EST]. Each scenario sets it from
    its navy's record (CUF R8 and §5: the Chinese and Spanish crews manned everything; the Russians in 1904-05 kept
    light guns under cover until a torpedo attack; the RN at Jutland manned its casemates in reach and kept a watch
    on its 4-in). A gun that can't bear or is wrecked sends its men under cover. The conn, the bridge, the tops, the
    look-outs, the directors and the stokehold are always manned.
  - **The captain's temper** (`Temper`, launch.md L4) moves the field a step: a timid captain applies the stricter
    rule a step early, a bold one keeps the guns manned (Gromoboi) and sends reliefs sooner.
  - **Under cover is a group** of its own, in the cells the men shelter in (16 §4.9: cover 0.2, prone), so a hit there
    still counts them. The men under cover by order are the reserve the reliefs draw from.
  - **Reliefs** (decisions 1 and 8): when a station falls below its need, men walk to it in the doctrine's order. They stop at the floor the doctrine keeps for the parties (`ReliefFloorShare` 0.25 [EST], CUF
    R9). The stokehold's floor is what the steam asked of it needs: the stokers above it are free, first for the
    supply, then the guns. A relief joins the station's group, so the next hit there finds him. The walking wounded
    come back to their own station as now.
  - **Reliefs wait for a lull** (CUF R9): while a station's duress is over the relief party's nerve, they wait at cover.
    A bold captain sends them anyway, and they take the casualties on the way.
  - **Walking** (CUF R10, all [EST], one set for all periods): 20-60 s between adjacent guns, 1-2 min within a
    section, 3-6 min end to end below the armour deck, 5-15 min for a whole crew to take over another turret and fire.
  - **Where the fields live:** `ManSecondaries` and `ReliefOrder` (HandlingRoomProtected or ReserveInSupply) in the
    side's doctrine, set by the scenario (decision 10); `ReliefFloorShare` in the damage-control doctrine with
    `PartyShare`; the walking times (`ReliefWalkS` per section) a sim setting, the same for everyone (they're the
    ship's passages, not a choice).
  - **Built 2026-10-28.** Sim side (`Sim.Combat/Reliefs.cs`): a mount's crew under cover waits in its shelter (the
    cell under it, `MountShelter`), counted by a hit there; closed up it walks back. Reliefs walk from a station's men, a
    mount's men under cover, or a fire party standing by (decision 12: lent, and taken back by `Repairs.Plan` when the
    party is sent to a job; a party never yet sent lends nobody, its men are still at their stations) to a station
    short of men (`ReliefWalkS` 45 s + `ReliefWalkSectionS` 12 s a section [EST, CUF R10]), on the outage clock. A relief
    at a station counts `BattleStations.ReliefWorth` 0.7 of its own (`In`, the reliefs' share of the casualties there
    booked against them). The captain's orders are commands: `DamageSystem.Posture` (a mask of mounts a ship) and
    `Relieve`.
    The captain's side (`CaptainLayer.Crew.cs`), every `CrewPlanS` 5 s a ship: posture for a ship fighting (the main
    battery, the turrets, torpedo mounts always manned; a wrecked gun's crew under cover, `MountSound` its state without
    its men; the secondaries by `ManCasemates` and `ManOpenGuns`, the captain's temper a step either way at ±0.5); reliefs
    for a ship hurt, fighting or not (the layer stays awake while any ship is, at the rails period between fights): the
    conn and the steering, then the main battery's guns and handling rooms, then the secondaries' (them first with
    torpedo craft in reach), from the supply's spare men first by `ReliefOrder` ReserveInSupply, the men under cover, the
    wrecked guns' crews and handling men, the parties standing by, the stokers the speed asked doesn't need.
    **The lull** was a stand-in until N3b: reliefs waited while a station's men were hit within `ReliefLullS` 120 s
    [EST], unless the captain was bold (gone with N3b: duress against the relief party's nerve). `ReliefFloorShare`
    isn't built (decision 12: no floor).
    Scenarios (decision 10, CUF §5): the RN 1914-16 casemates in range and its open guns for torpedo craft (coronel,
    dogger-bank, heligoland-bight, jutland, running-fight); the Germans' casemates always, open guns for torpedo craft;
    the Russians at Tsushima both for torpedo craft; the rest the default preset (in range).
    **Measured** (crew-history now runs with captains, so the reliefs come in the lull after the fire): Chester,
    Southampton, Kent and Graf Spee the same men; Southampton has 23 reliefs at her guns by 30 min (her stokers off
    watch), Chester 15. Under long fire the toll rises with the reliefs sent into it: Oryol 157 → 181 killed and wounded
    (record 123), Emden 194 → 219 (171-203), Suvorov 228 → 295, Sverige 207 → 427 (out of action at 2 h 128 → 333,
    still rising while her guns are manned). The lull stand-in holds them back a little; N3b's nerve is the real check.
    Cost: Jutland's captains at 2.5 h 499 µs a think (498 without the pass), the longest 16 ms (9); the squadrons
    scenario allocates nothing.
- **N3b Steadiness: the men's nerve** (decision 5). Each station keeps its **duress** (a new word: what has landed at
  and around it, fading), against its **nerve** (the threshold, by the men's experience, decision 6; work-up doesn't
  enter). All numbers [EST] from CUF R1-R4.
  - **Duress** in station units (1.0 = the whole station's crew hit at once):

    | Event | Adds | Half-life |
    |---|---|---|
    | A man of the station killed or wounded | 1/n (n = men at it) | 4 min, 30% of it held 15 min |
    | The station's officer hit | +0.15 | 4 min |
    | Casualties in sight at a neighbouring open station (open stations only) | 0.3 × their share | 4 min |
    | A penetrating hit inside the station | +0.30 | 90 s |
    | Flash or fire in the station | +0.40 while it burns | 2 min after it's out |
    | A near burst within ~15 m, splinters through | +0.08 × 1 open, 0.6 shielded, 0.4 casemate, 0.3 turret or below the armour | 30 s |
    | A straddle on her, or lit by a searchlight (green only) | +0.02 a salvo | 30 s |
    | An officer arrives | −0.15, the return delay reset | — |

    No term for hits elsewhere on the ship, and no contagion (CUF R2, R5).
  - **Nerve and what follows**, by experience (the rows at 0-3, read between them):

    | | Nerve | Share going, open station | Enclosed (turret, casemate, below the armour) | Back after duress falls below 0.6 × nerve | Duress fades |
    |---|---|---|---|---|---|
    | Green | 0.30 | 0.35 | 0.15 | 4-8 min | × 0.67 |
    | Average | 0.45 | 0.20 | 0.08 | 2-4 min | × 1.0 |
    | Trained | 0.60 | 0.10 | 0.04 | 1-2 min | × 1.2 |
    | Elite | 0.75 | 0.05 | 0.02 | 0.5-1 min | × 1.5 |

    The share going rises smoothly with duress (a logistic about the nerve, its width 0.15 × nerve), up to the row's
    share: no switch, and a core that never goes. Men who go on their own shelter at the nearest cover and aren't in
    the relief pool until they come back (Suvorov's hiding men).
  - **Sensitised:** each time a station goes over its nerve, its nerve falls 13% for the rest of the action, half of
    that back over 30 min (CUF R2).
  - **The officer** (decision 7: rough, present or hit, no person): while the station's officer or senior PO is there
    and unhurt, nerve × 1.25 and the share going × 0.5, and his men leave only on his order, in good order (Queen
    Mary's X). Hit, nerve × 0.8 and the return delay × 1.5-2 until a relief officer walks in from the ship's few spare
    officers. A green station (experience under 1, smoothly) with no officer after a penetrating hit or fire has
    0.2-0.4 of its men frozen: present, taking casualties where they stand, not working, until an officer arrives
    (Semenov's "mesmerised" men).
  - **The captain's temper is the order, the men's experience whether they keep to it**: N3a sets where they're
    ordered to stand, N3b who stays.
  - The first check is the direction and the yardstick's shape. Tuning waits for the playtest.
  - **Built 2026-10-28** (`Sim.Combat/Steadiness.cs`; the numbers in `settings.json`, all [EST] as above).
    - **Stations:** every group with a battle crew but the repair men's quarters, set up at spawn. Open: the open and
      shielded mounts and the parts above the deck (the bridge, the directors, the AA). Enclosed: the turrets, the
      casemates, the barbettes, the conning tower and every space in the hull.
    - **Duress** is held in five channels a station, each fading by its own half-life (30 s, 90 s, 2 min, 4 min,
      15 min), faded lazily to the time it's read. The men's experience speeds the fading up (the row's ×). A station's
      casualties add their share of its battle crew (1 = the whole crew, however many are left), 30% of it held 15 min.
      Its officer hit adds 0.15. An open station within `DuressNeighbourM` 15 m of an open one hit takes 0.3 of that
      one's share (a sorted sweep along x, no pairs). A shell's lane through the station adds 0.30, once a hit. A burst
      within 15 m whose pieces reach its men adds 0.08 × its cover (1 open, 0.6 shielded, 0.4 casemate, 0.3 turret or
      inside the hull; near misses too), once a hit. Flash adds 0.40. A fire in its space at any stage short of out adds
      0.40 while it burns, fading for 2 min after. The straddle and searchlight term isn't built: at half weight for
      green men it's +0.01 a salvo with a 30 s half-life, a steady 0.02 against a nerve of 0.30.
    - **Nerve and going:** the rows are read between the whole numbers (`NerveAt`, as `GunneryCrewAt`). The share going
      is the row's most × a logistic in (duress − nerve) / (0.15 × nerve). They come back the row's middle time after
      the duress falls under 0.6 × the nerve (green 6 min, average 3, trained 1.5, elite 0.75; × 1.75 with the officer
      down). Shaken and frozen men aren't at work (`AtWork`, so the mounts' crew, the directors, the conn and the
      stokehold all read it). Rough: they still stand where the hits find them, as an untenable space's men do (N2).
      Their share of a station's casualties comes off their counts. They aren't in the relief pool, and the captain
      sends no reliefs for them: they're still there.
    - **The officer** (decision 7) is the first man of the station's casualty roll (no extra draw). Hit, a relief
      officer walks in from the middle of the ship (the relief walk) while the ship's spare officers last
      (`OfficersSpare` 1% of the battle crew, at least one). After that his station stays without one. His coming
      takes 0.15 off and frees the frozen. Frozen: `FrozenShare` 0.3 × (1 − experience) of the men there, at a station
      with its officer down after a shell through it or a fire in it.
    - **Sensitised:** going over the nerve takes 6.5% off it for good and 6.5% that comes back with a 10 min half-life
      (most of it within 30 min). The station is over again only after it's been calm (under 0.6 × the nerve).
    - **When:** worked out in `Functions.Evaluate` (on a hit, now also one that only killed men or stirred a station:
      before this a hit that killed men and wrecked nothing left the stations' shares unread until the next change, an
      N2 gap). The outage clock looks again every `NerveCheckS` 10 s while a station has men shaken or frozen, and when
      a relief officer is due. A station steady again leaves the list it's kept on.
    - **The reliefs' lull:** a station's duress over the relief party's nerve (the men's row, no officer's) holds them,
      unless the captain is bold (`Steadiness.ReliefNerve`, in place of `ReliefLullS`).
    - `crew-history` prints the men shaken, frozen and the officers out at each sample, and the most shaken and frozen
      at once a run. Tests: `SteadinessTests`.
  - **Measured (2026-10-28, 20 runs; killed and wounded a ship, N3a → N3b, record):**

    | Case | Killed and wounded | Out at the end of the fire | Most shaken at once (max), frozen (max) | Record |
    |---|---|---|---|---|
    | Oryol, green, 5 h | 181 → 180 (43 killed) | 121 | 6.7 (9), 1.7 (8) | 43 killed, 80 wounded |
    | Chester, trained, work-up 0.3 | 23 → 23 | 23 | 0.9 (2), 0 | 29 killed, 49 wounded; no flight |
    | Southampton, trained | 13 → 13 | 9 | 0.1 (1), 0 | 89; few or none leaving |
    | Kent, average | 55 → 58 | 42 | 2.7 (6), 0 | 16; no leaving |
    | Emden, trained | 219 → 236 (lost 85 → 60%) | 176 | 4.2 (7), 0 | 171-203 |
    | Suvorov, green, 5 h | 295 → 287 | 162 | 9.2 (11), 2.8 (5) | all lost |
    | Sverige, trained, 500 hits in 2 h | 427 → 277 | 135 at 30 min, 144, 165, 181 at 2 h | 1.7 (3), 0 | - |
    | Graf Spee | 44 → 48 | 29 | 0.2 (1), 0 | 96 |

    - **The long fights re-judged.** Oryol's killed are now the record's 43, with 121 out at 5 h in the wanted
      100-150. Her wounded (137 against 80) are E6's split, as before. Emden's 236 is 16% over the record's top (219
      before, so within the runs' spread). Fewer of her runs are lost. Suvorov's toll can't be read against an
      all-lost record. Sverige no longer blows up: N3a's reliefs walked into her stations as soon as 2 min passed and
      were killed there (427, out of action 333 at 2 h). Now they wait while a station is over their nerve, and under
      four hits a minute most of her hit stations stay over it, so few walk in during the fire (2-4 there at a time).
      Her toll still rises for as long as she fights (135, 144, 165, 181 at 30, 60, 90, 120 min), with 3 of her 8 6″ mounts
      and one of her 2 main mounts in action at 2 h; her reliefs (40) come in the lull after. That's the wanted shape but for
      "silent from want of men before the toll flattens": her guns aren't silent at 2 h.
    - **Who goes:** green crews' open stations most (Suvorov, Oryol: a few men at a time, out of 790 and 800). Trained
      and average crews hold, a man or two at most, except Kent's average reservists (2.7 at once). Kent's casemates
      take three times the record's casualties (E6, flagged at N0), and so their duress. Frozen men only on the green
      ships.
    - **Unchanged:** E6's per-hit fit (damage-history, 60 runs, before and after alike: Jutland's plate count 1.25,
      Dogger Bank's Lion 0.65), and the rest of the cases within their noise. The full suite passes (no allocation in
      the hit and evaluate paths). The squadrons' hash moves (9C31A3193971F350 → 692F156EB43F7CA1: the men's state is
      hashed), at the same cost and allocations.
- **N5 The end of her.** No new fall-out knob. Fall-out already reads the main guns, and those now go silent for want
  of men. A ship whose parties are spent can't fight her fires, and fire's own model takes her from there: they spread,
  the magazines are threatened and flooded or not. The last gun, fed by reliefs, may fight on for hours after the main
  battery (CUF §8), and that's right.
- **N5b Wrecks with no sign of life** (decision 3; builds the "ships seen done for" item that fire-control.md F10 holds,
  for burning, silent ships; the record of that item stays there).
  - **Silent as seen:** today `SeenFlags.Silent` is read from the truth (`MountsWorking == 0`). It becomes "no gun of
    hers seen firing for a while". The quiet a battery keeps between salvos isn't silence. A salvo of hers that an
    observer is placed to see (the sightings' own judgement, never the drawing's) clears it at once.
  - **Her worth falls** on burning, silent and stopped, as seen, smoothly (no hard switch). At will, the batteries go
    to the next target in reach. With none, the side's doctrine field picks finishing her or ceasing fire.
  - **A sign of life brings the fire back:** her worth recovers as soon as a gun of hers is seen firing.
  - An ordered Engage stands: the player's call, or an admiral's.
  - **Tests:** a silent burning cripple loses a line's fire when another target is in reach; one gun of hers firing
    takes it back within a salvo's time; the rounds saved in a squadrons play, reported.
  - **Built 2026-10-28.**
    - **Silent as seen** (`Sightings.Quiet`): each ship's last salvo is stamped (`CombatStore.LastFired`, fine and coarse
      alike). A contact keeps `QuietMs`, when her guns were last seen firing: she fired since the side's last look (its
      span: a second fine, the coarse step) and she's in sight now. Her coming into sight starts it again too. She's
      silent once she shows damage (burning, low, a list, a trim) and has been quiet `SeenSilentS` 180 s [EST: past a
      heavy battery's slowest salvos and a ranging wait]. The damage condition is the observer's reading: a ship
      unhurt and holding her fire, or not yet opened, isn't silenced. Without it the matchup (which counts a silent
      ship's guns as nothing) would take every ship out of reach for gunless. The truth's "no mount works" is gone; the
      map's marks and the matchup read the seen one.
    - **Done for** (`Contact.DoneFor`, 0-1, from the picture alone): 0 unless burning and silent; then the quiet past
      180 s (over another 180 s) × how near stopped her estimated speed is (none at `DoneForKn` 4 kn [EST]).
    - **Her worth** in the captains' pick at will: her range counts / (1 − 0.95 × done for), up to twenty times as far,
      so any other target in reach comes first, smoothly. With nothing else in reach, doctrine's **`Wrecks`** decides:
      cease (she's no target once `DoneForCease` 0.5 done for) or finish her. The default preset ceases (the user's
      direction); the neutral default (a world told nothing) finishes, as before. On the launch screen's standing
      orders as "Wrecks seen done for". An ordered Engage stands, the player's or an admiral's line.
    - **A sign of life:** a salvo of hers seen restarts her quiet at the next look, so she's neither silent nor done for
      and the pick takes her up again.
    - **Found:** the dev's fire (`DamageSystem.Burn`, `-burn`) didn't mark the ship damaged, so no observer ever saw it
      burning; it does now.
    - **Tests** (`WreckFireTests`): a burning, stopped cripple holding her fire loses the line's fire to a healthy ship
      beyond her 3.8 min after it was first on her (silent at 3 min); when she fires again it's back on her 3 s after her
      first salvo; with nothing else in reach "cease" ceases and "finish" stays on her; an unhurt ship holding her fire
      isn't silent.
    - **The rounds saved in a squadrons play** (8 seeds to the end; `scenario --play` now prints the shells fired and
      those at ships seen done for): 58,007 before, 60,048 after, within the seeds' spread; 17 shells at ships seen done
      for. Nothing measurable is saved there because the case hardly arises: her ships founder or capsize, or keep a gun
      firing (the reliefs, N3), before any is seen burning, silent and stopped. The savings wait for a fight with
      cripples left burning (Wiesbaden's), and for the list and freeboard graded in fire-control.md F10.
    - The squadrons' hash at 30 min: 692F156EB43F7CA1 → 66C709F73E3D1C32 (the pictures hash `QuietMs`); its cost and
      allocations the same. The full suite passes.
- **N6 Fit and check.** The yardstick above. E6's per-hit benches stay as they are. Coarse parity (`CoarseParityTests`),
  no allocation in the hit and evaluate paths, the matchup and the squadrons' hash reported.

## After this plan

- **The captain strikes or abandons** (decision 3): a command step (`command.md`). This is a ship that can't fight,
  with her men alive or dying. Oryol surrendered with most of her crew alive. Captains taking whole ships out (Jiyuan,
  Guangjia at the Yalu) belong there too.
- **Cover's fine-tuning** (decision 4): Oryol's losses came through ports and slits (Kostenko, CUF §2). E6 gives a
  gunhouse's men 0.45 cover; sighting ports and conning tower slits may need their own way in.
- **Civilian and committee crews** (decision 9), as ship-wide overrides when a scenario needs them. Otranto's drifters
  first (scenarios.md 16): civilian nerve 0.10, the straddle term at full weight, taking to the boats when warned
  unless a bold skipper commands. A committee crew (Moon Sound 1917) empties the stations below the waterline when
  flooding is reported, and the captain's orders to them go unobeyed (Slava).
- **The campaign's crews** (decision 6): experience growing with action (most in the first), work-up with months in
  the ship, both wearing (turnover, long voyages, idle fleets: CUF §9), crews moved between ships, and a navy's
  doctrine (decision 10) by the campaign's own mechanics.
- **Work-up beyond the guns** (decision 6): the parties knowing her valves, the engines giving her full power.
- **Readiness and teething** (the difficulty sliders): closing up from a lower condition (Savo, Hercules' men at their
  tea), and a new ship's weapon failures by her time in commission, the ship's and not the crew's (Arethusa, Tiger;
  CUF R11).
- **A gunnery term under fire** only if the playtest asks for one (CUF R11: error × 1.2 green to 1.0 elite on layers,
  halved under director firing, or a spotting penalty from splashes about her). Today's rule stands: none.
