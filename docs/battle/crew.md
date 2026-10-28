# Plan: the crew (a 4.5 deepening)
Status: **agreed with the user 2026-10-27**; **replanned from the research 2026-10-28, the changes await the user**
("Questions from the research" below)    Updated: 2026-10-28

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
- **Doctrine, not navy.** Who relieves whom and how many are kept back are fields in the side's damage-control
  doctrine, one a period, as E8b set them. No per-navy values.
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
   default, from the record's practice:
   - First, the crews of guns that can't bear (the disengaged side). They're the ship's ready reserve, and they go back
     when she turns.
   - Next, the crews of mounts that are wrecked or jammed for the battle, and those mounts' handling men.
   - Then the idle hands (the hotel staff, the band, the stewards), and the repair men above the floor.
   - The main battery is filled before the secondaries, unless torpedo craft are the threat.
   - A live mount's handling room is never stripped: guns without ammunition are as silent as guns without crews. The
     stokehold and the parties' floor are kept.

   This is doctrine: a period's doctrine may order it otherwise, and the player picks the doctrine at launch, not the
   men (decision 2). It's open to revision once it plays. **The research's revisions are question 3 below.**
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
   plan. Built in N3b, with the research's numbers.

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
  (CUF §9). One `CrewQuality` for both overstates a green crew's flight. Question 1.
- **Ordered cover is the big lever, unordered flight the small one.** Rossia cleared her quick-firers at Ulsan and her
  sister Gromoboi didn't: 44 dead against 87 (CUF §1.6). The captain's order (N3a) moves more men than the crew's
  nerve (N3b).
- **The relief order needs four changes** (CUF R9): the conn and steering outrank the guns (Reina Cristina); the handling
  room is a doctrine option, not a law (the USN's 1899 bill kept the reserve in the powder division); stokers go free
  as the steam asked of them falls (Southampton, Blücher); reliefs wait for a lull before going into a station under
  heavy fire (Southampton, the USN's "wait for lull"). Question 3.
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
  longer and leave only on order; hit, they waver until another comes. Question 2.
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
  outside the station model (CUF R6). Question 4.
- Captains taking ships out (Jiyuan, Nebogatov): the captain's temper and the step after this plan, as decision 3 has it.
- Contagion (CUF R5): no source quantifies it, and the panic literature points the other way. None.
- The blast knocking a crew down for 10-60 s (Colossus): under a salvo's time, not decisive. Not built.
- The straddle and searchlight term for green crews (CUF R3): kept, at half weight, green only. Civilians would take it
  at full weight with question 4.
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
| Rossia against Gromoboi, Ulsan (scenario 5): sisters, one cleared her light guns | 44 against 87 killed; 200 against 257 casualties | the covered ship about half the dead, about 20% fewer casualties, fewer light-gun rounds |
| Kent, Falklands: reservists in casemates, 37 light hits | 4 killed, 12 wounded, most at one gun | about 16 out; no leaving; poor shooting from gunnery quality, not duress |
| Emden, Cocos: trained, fire control wrecked early | fire slackened as the rangefinders went; by ~80 min one gun, an officer and "unskilled chaps" | accuracy falls with control damage first (the F-steps); one gun late, worked by reliefs |
| Suvorov, Tsushima: green, worn, 5 h of HE | short routs after blasts; frozen men restarted by officers; covered light-gun crews used up as reliefs; a 12-pdr firing late | the same states seen; the reserve gone within hours; a last gun late |
| Vizcaya, Santiago: green gunnery, steady crew | the batteries fought until too few men remained; parties drawn to the guns, fires spread | silent from want of men; fires gaining once the parties are drawn |
| Zhenyuan, Queen Mary's X, Samar, Blücher: trained crews holding | no unordered leaving; idle crews to working guns in minutes; Blücher's stokers to the supply once slowed | trained and better: no leaving at a working gun; stokers freed by speed lost |
| Graf Spee: 18 × 6" + 2 × 8" | 36 killed, 60 wounded | in the band |
| Sverige under 500 × 152 mm (the user's case) | — | toll rising for as long as her secondaries fight; the last gun outlives the main battery; silent from want of men before the toll flattens; fires gaining once her parties are spent |

Ship totals of lost ships (CUF §8) are for what still worked late, not for casualties.

## The steps

- **N0 Measure.** The hits bench prints, after K hits: men out by station kind, mounts working by battery, directors,
  conned, the parties' men and their jobs. Add a `--curve` line of the out-of-action count every 25 hits. The
  yardstick runs and today's figures are recorded here. Add a timed bench of the yardstick's short cases (Chester,
  Southampton, Kent: a ship under a given fire for minutes), since a hit count alone can't show reliefs or a lull. The
  research ask is answered (CUF §8).
- **N1 One book of men.** Parties are made from the "repair" groups' men, not from `Complement × PartyShare`. A party
  at work stands in its target's cells and is a group there: E6's pieces, blast and spall hit it as anyone. Fire's
  `Hurt` and `Caught` take from that group. `FireHitMenChance` and `FireHitMenKeep` go. Turned-to gun crews (`TurnTo`)
  come from the mounts' groups. The doctrine's `PartyShare` becomes how many of the repair men muster into parties.
  Fire's yardstick re-run (fire.md B5).
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
- **N3a Posture and reliefs: the captain's order** (decisions 1-2). Where each man stands, decided by the captain at
  each think.
  - **Closed up or under cover, per station**, by a doctrine field **`ManSecondaries`**: Always, InRange (a target worth
    its fire in reach), or TorpedoCraft (with a watch of two guns a side closed up). Proposed defaults (CUF R8): 1890
    Always (the open-mount era); 1905 and 1916, casemated batteries InRange and open light guns TorpedoCraft with the
    watch; 1942 and 1944 as 1916 until the later cuts' research. A gun that can't bear or is wrecked sends its men
    under cover. The conn, the bridge, the tops, the look-outs, the directors and the stokehold are always manned.
  - **The captain's temper** (`Temper`, launch.md L4) moves the field a step: a timid captain applies the stricter
    rule a step early, a bold one keeps the guns manned (Gromoboi) and sends reliefs sooner.
  - **Under cover is a group** of its own, in the cells the men shelter in (16 §4.9: cover 0.2, prone), so a hit there
    still counts them. The men under cover by order are the reserve the reliefs draw from.
  - **Reliefs** (question 3 for the revised order): when a station falls below its need, men walk to it in the
    doctrine's order. They stop at the floor the doctrine keeps for the parties (`ReliefFloorShare` 0.25 [EST], CUF
    R9). The stokehold's floor is what the steam asked of it needs: the stokers above it are free, first for the
    supply, then the guns. A relief joins the station's group, so the next hit there finds him. The walking wounded
    come back to their own station as now.
  - **Reliefs wait for a lull** (CUF R9): while a station's duress is over the relief party's nerve, they wait at cover.
    A bold captain sends them anyway, and they take the casualties on the way.
  - **Walking** (CUF R10, all [EST], one set for all periods): 20-60 s between adjacent guns, 1-2 min within a
    section, 3-6 min end to end below the armour deck, 5-15 min for a whole crew to take over another turret and fire.
  - Doctrine fields: `ManSecondaries`, `ReliefOrder` (HandlingRoomProtected or ReserveInSupply), `ReliefFloorShare`,
    `ReliefWalkS` per section, one a period.
- **N3b Steadiness: the men's nerve** (decision 5). Each station keeps its **duress** (a new word: what has landed at
  and around it, fading), against its **nerve** (the threshold, by `CrewQuality`). All numbers [EST] from CUF R1-R4.
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
  - **Nerve and what follows**, by `CrewQuality`:

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
  - **The officer** (question 2): while the station's officer or senior PO is there and unhurt, nerve × 1.25 and the
    share going × 0.5, and his men leave only on his order, in good order (Queen Mary's X). Hit, nerve × 0.8 and the
    return delay × 1.5-2 until a relief officer walks in from the ship's few spare officers. A green station with no
    officer after a penetrating hit or fire has 0.2-0.4 of its men frozen: present, taking casualties where they stand,
    not working, until an officer arrives (Semenov's "mesmerised" men).
  - **The captain's temper is the order, crew quality whether the men keep to it**: N3a sets where they're ordered to
    stand, N3b who stays.
  - The first check is the direction and the yardstick's shape. Tuning waits for the playtest.
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
- **N6 Fit and check.** The yardstick above. E6's per-hit benches stay as they are. Coarse parity (`CoarseParityTests`),
  no allocation in the hit and evaluate paths, the matchup and the squadrons' hash reported.

## Questions from the research (for the user, 2026-10-28)

1. **Steadiness and gunnery from one `CrewQuality`?** The record parts them (Kent, the Spanish: steady, bad shots).
   Recommended: one `CrewQuality` for now. The narrow nerve table above already keeps a green crew mostly at its guns.
   A separate steadiness setting comes with the difficulty sliders, defaulting to the gunnery's.
2. **Officers per station in N3b?** The evidence's strongest steadying factor, and the frozen state hangs on it.
   Recommended: yes, rough. One officer (or senior PO) a station as part of its men, hit by E6's draw as one of them,
   relief officers from a small pool walking in like any relief. A byte a group.
3. **Decision 1's relief order, revised.** Recommended order: (0) the crew thinning, not a relief (N2); (1) the conn and
   steering before the guns (Reina Cristina); (2) the men under cover by order, the disengaged side's among them;
   (3) wrecked and jammed mounts' crews and their handling men; (4) the idle hands, the repair men above the floor and
   the stokers the steam doesn't need; the main battery before the secondaries unless torpedo craft threaten. The
   handling room becomes `ReliefOrder`: HandlingRoomProtected (the default, every period) or ReserveInSupply (the
   reserve waits in the supply and goes first; the idle hands refill the supply). Reliefs wait for a lull.
4. **Civilian and committee crews after this plan?** Recommended: yes, as ship-wide overrides when a scenario needs them.
   The first is Otranto's drifters (scenarios.md 16): civilian nerve 0.10, the straddle term at full weight, taking to
   the boats when warned unless a bold skipper commands. A committee crew (Moon Sound 1917) empties the stations below
   the waterline when flooding is reported.
5. **`ManSecondaries` with the period defaults above?** Recommended: yes. Ulsan (scenario 5) is its check: Rossia and
   Gromoboi are one class, so the same doctrine with two captains' tempers should show the halving.

## After this plan

- **The captain strikes or abandons** (decision 3): a command step (`command.md`). This is a ship that can't fight,
  with her men alive or dying. Oryol surrendered with most of her crew alive. Captains taking whole ships out (Jiyuan,
  Guangjia at the Yalu) belong there too.
- **Cover's fine-tuning** (decision 4): Oryol's losses came through ports and slits (Kostenko, CUF §2). E6 gives a
  gunhouse's men 0.45 cover; sighting ports and conning tower slits may need their own way in.
- **Civilian and committee crews** (question 4), with the scenarios that need them.
- **Readiness and work-up** (the difficulty sliders): closing up from a lower condition (Savo, Hercules' men at their
  tea), and weapon failures in ships under about six months in commission (Arethusa, Tiger; CUF R11).
- **A gunnery term under fire** only if the playtest asks for one (CUF R11: error × 1.2 green to 1.0 elite on layers,
  halved under director firing, or a spotting penalty from splashes about her). Today's rule stands: none.
