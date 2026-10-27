# Plan: the crew (a 4.5 deepening)
Status: **agreed with the user 2026-10-27** ("Decided" below)    Updated: 2026-10-27

What a ship's men do in a fight: where they stand, what a station can do with the men left at it, who goes up to fill
the gaps, and when there's nobody left to fight her. It builds on E6's casualties (`shells-armour-damage.md`,
`damage/16_casualties.md`), fire's parties (`fire.md` B2) and damage control (`damage-control.md`). Started by the
user's Sverige slugfest (2026-10-27): "Once the crew is gone the ship is de facto dead, now the ship basically becomes
a zombie."

## The aim

1. **One book of men.** Every man stands somewhere, and a hit counts whoever stands where it bursts. That includes the
   fire parties, which today are a pool of their own.
2. **A station works with the men at it.** A gun with half its crew loads slower, and with nobody it's silent. The same
   goes for directors, the conn, the stokehold and the ammunition supply. A ship with her crew gone is dead in fact, not
   a hull that fires on.
3. **Men go up to fill the gaps.** Reliefs from below and from the disengaged side man the guns again. The toll rises
   for as long as she keeps fighting, and it levels off when she stops fighting, not before.

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
- **Same at any compression.** Reliefs and station shares change on minute clocks and on hits, the same in fine and
  coarse.
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
   men (decision 2). It's open to revision once it plays.
2. **Posture is part of the reliefs: the captain's and the crew's, not the player's** (the user: "I don't think as a
   player I want to care about the decision on that level"). They're one question: where each man stands now. The
   captain keeps men at the stations that are fighting and sends the rest under cover. Those men under cover are the
   reserve the reliefs come from. Built in N3. N4 is gone. The player's only say is the side's doctrine at launch
   (standing orders, later the difficulty sliders).
3. **Striking colours and abandoning ship are the captain's call, after this plan.** A command step of its own, once a
   ship with nobody left to fight her exists. **And the captains stop shooting at burning wrecks that show no sign of
   life.** If one of her guns fires, she's taken under fire again at once, as happened in the record (Wiesbaden at
   Jutland, Suvorov at Tsushima). This is built here as N5b, because N2 is what makes a ship really fall silent.
4. **Cover is fine-tuned later** (gunhouse ports, conning tower slits, the 0.45). N0 only measures it.

## The yardstick

| Case | Record | Wanted |
|---|---|---|
| E6's per-hit fit (G1-G7, 10 hits a ship) | as in `shells-armour-damage.md` E6 | unchanged within the noise: reliefs come after the first hits |
| Oryol, Tsushima: battleship, 76 hits of all calibres | 43 killed, 80 wounded; "main losses behind armour from splinters entering ports/slits" (Kostenko) | about 100-150 out of action at 76 mixed hits |
| Southampton, Chester, Jutland: open crews under 15 cm | 76-77 killed and wounded to 17-18 hits | unchanged (already crowded stations) |
| Graf Spee: 18 × 6" + 2 × 8" | 36 killed, 60 wounded | in the band |
| Sverige under 500 × 152 mm (the user's case) | — | toll rising for as long as her secondaries fight; silent from want of men before the toll flattens; fires gaining once her parties are spent |

**Research ask (N0):** ship totals for the ships pounded to wrecks (Suvorov, Borodino, Blücher, Bismarck,
Scharnhorst 1943, Hiei, Kirishima): hits, complement, killed and wounded before the end if known, and what still
worked. The aim is the ranges by type, not replicas.

## The steps

- **N0 Measure.** The hits bench prints, after K hits: men out by station kind, mounts working by battery, directors,
  conned, the parties' men and their jobs. Add a `--curve` line of the out-of-action count every 25 hits. The research
  ask above. The yardstick runs and today's figures are recorded here.
- **N1 One book of men.** Parties are made from the "repair" groups' men, not from `Complement × PartyShare`. A party
  at work stands in its target's cells and is a group there: E6's pieces, blast and spall hit it as anyone. Fire's
  `Hurt` and `Caught` take from that group. `FireHitMenChance` and `FireHitMenKeep` go. Turned-to gun crews (`TurnTo`)
  come from the mounts' groups. The doctrine's `PartyShare` becomes how many of the repair men muster into parties.
  Fire's yardstick re-run (fire.md B5).
- **N2 Stations work by their men.** A share per station: men present over the need it was sized for (`Crew.GunCrew`,
  `CommandK`, the directors' and the plant's crews).
  - **Mounts:** the reload stretched by the share. Below the few a gun can't be fought without (layer, loader, rammer),
    it's silent. A barbette's handling men set the supply in the same way.
  - **Directors and eyes:** short-handed, the rangefinder and the plot are slower (the F-steps' sigma), and silent with
    nobody.
  - **Command:** the conn passes to the next manned station (the conning tower, aft control). With none, she isn't
    conned (`Conned`).
  - **Machinery:** steam by the stokehold's men, with a floor. A boiler room keeps its steam for a while on what's
    already in the fires.
  - Read in `Functions.Evaluate` from `Casualties.Men` (it already runs on every hit).
- **N3 Posture and reliefs** (decision 2): where each man stands, decided by the captain at each think.
  - **Closed up or under cover, per station:** a station that's fighting keeps its men at it. One that can't bear, has
    nothing worth its fire (a 75 mm against a battleship at 8 km) or is wrecked sends its men under cover, behind
    plating or below the armour deck (16 §4.9: cover 0.2, prone). They go back when it's needed again, after the walking
    delay below. With the conn, the directors and the stokehold, there's no choice: they're always manned.
  - **Under cover is a group** of its own, in the cells the men shelter in, so a hit there still counts them.
  - **The men under cover are the reserve the reliefs draw from.**

  When a station falls below its need, men walk to it after a delay of minutes, by the sections
  between. They come in the doctrine's order (decision 1): the crews of mounts that can't bear, those of wrecked
  mounts and their handling men, the idle hands, then the repair men above the floor. They stop at the floor
  the doctrine keeps for the parties and the stokehold. A relief joins the station's group, so the next hit there finds
  him. The walking wounded come back to their own station as now. Doctrine fields: `ReliefOrder`, `ReliefFloorShare`,
  `ReliefWalkS` per section, one a period.
- **N5 The end of her.** No new fall-out knob. Fall-out already reads the main guns, and those now go silent for want
  of men. A ship whose parties are spent can't fight her fires, and fire's own model takes her from there: they spread,
  the magazines are threatened and flooded or not.
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

## After this plan

- **The captain strikes or abandons** (decision 3): a command step (`command.md`). This is a ship that can't fight,
  with her men alive or dying. Oryol surrendered with most of her crew alive.
- **Cover's fine-tuning** (decision 4): Oryol's losses came through ports and slits. E6 gives a gunhouse's men 0.45
  cover; sighting ports and conning tower slits may need their own way in.
