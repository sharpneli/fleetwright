# Plan: magazines (a 4.5 deepening)
Status: **every question decided** (the user, 2026-10-20: "Decided" below); research arrived (10); V0-V8 done 2026-10-20 (the look next, by its own process)
Updated: 2026-10-20

The fine-tune pass for magazines: how a flash gets to the charges, what happens once they burn, and what the ship
keeps. **This is the one place for magazine work.** Every open item about magazines, hoists, flash doors, the
venting race and the hull at the magazine lives here. The other docs point here and keep only what's already built:
fighting.md 4.5's Magazines bullet, the shells plan's hand-off and its E8a items 1 and 4, bugs.md's mount kind, fire's
B2 heat, damage control's valves, plan.md's propellant need, and the magazine look's tiers.

The research:
- `research/simulation/damage-research/07-magazine-explosions.md` is the core: the venting race (§2.3), the boundaries
  in the order they fail (§2.4), the hull girder (§3), the outcome tiers (§6.3) and the anchors (§6.4).
- `docs/shipgen/research/02_components.md` §1 covers the ammunition train, the propellants, the failure modes (Seydlitz,
  Lion, Derfflinger) and the mitigations (flash doors, interlocks, flooding).
- `docs/battle/10_damage_control_holistic.md` §2.3 is magazines under fire, and `docs/battle/damage/11_blast_confinement.md`
  covers trunks as joins between spaces.
- `research/simulation/damage-research/10-magazine-flash-chain.md` (2026-10-20, the request's answer, with
  `magazine_race_ref.py`): the burn law and its constants, turret weights and the lift, doors and charges by station and
  practice, flooding times, the open guns' paths, and the race run on the anchors.

As in the other plans, the reference code is something to check against, never a second engine.

## The aim

Today a flash is a lottery that ends the ship. The record says it's a chain and a race:

1. **A chain.** Flame goes from where charges lie open (gunhouse, working chamber, hoists, handling room) down to the
   magazine through doors and scuttles, and only as far as they're open.
2. **A race.** Burning propellant makes gas faster as the pressure rises, and the openings let it out. If the vents win,
   the result is a flame column and a burned-out turret, and the ship fights on (Seydlitz, Derfflinger, Lion). If the
   gas wins, the decks, sides and bottom go and the ship breaks at the magazine (Indefatigable, Queen Mary, Invincible).
3. **Choices that move both.** Flash discipline trades rate of fire for risk. Flooding the magazine in time saves her
   (Seydlitz's Heidkamp, Lion's Harvey).

## What there is today (checked 2026-10-20)

- **The sources** are in `Effects.cs`, each one flat roll ending in `MagazineVia`:
  - a burst in a magazine (`In`) or beside one (`Beside`) rolls `MagazineFlashChance` 0.25;
  - a burst in an enclosed mount's gunhouse or barbette (`Turret`) rolls `TurretFlashChance` 0.25;
  - E3's spall among charges (`Spall`) rolls the same chances, times how many pieces got there;
  - E5's hot gas reaching a magazine or handling room (`Gas`), or a burst in a handling room (`Handling`), rolls them too;
  - a torpedo (`Underwater.cs`) rolls `TorpedoMagazineChance` 0.05 when its reach takes in a magazine;
  - a fire's heat (`Fires.Magazines`, fire B2) goes up past a drawn limit.
- **Any of them loses the ship at once:** `DamageSystem.Lose(LossKind.Magazine)`, gone after `MagazineLossMs` (60 s).
  `Casualties.Train` flashes the men in that magazine's mounts' gunhouses, barbettes and handling rooms and in the
  magazine. Nothing between a flash and a lost ship exists: no burned-out turret, no partial hull damage.
- **Enclosed** means `TurretShape.Bb` (`Effects.Enclosed`). Shipgen makes every main-battery mount of 150 mm or more a
  `bb` (`Geometry.MakeTurretType`: `calMm >= 150 ? "bb"`, a switch by calibre). So Chester's open-backed 6″ get a
  turret's flash path.
- **Shield mounts** (checked 2026-10-20): the same switch makes every mount of 76-150 mm a `dp` (the designer calls it
  shielded) and anything smaller `open`; casemates come from the secondaries' position. Then:
  - **No flash goes down from the mount:** a burst or spall in a `dp` or `open` mount isn't a turret source.
  - **Crews sit in steel:** `Casualties.Enclosed` counts `dp` as a gunhouse, for the men's cover and their flash. A
    destroyer's 4″ behind a shield and a 5″/38 gunhouse are the same shape.
  - **A handling room** sits over every main mount's own magazine, a destroyer's shielded guns included
    (`Ordnance.AddHandlingRooms`). A burst or hot gas in it rolls `TurretFlashChance` (V25 under Arethusa's 6″ HE:
    0.17% of hits). Secondaries share a battery magazine, fore and aft, with no handling room.
  - **The ready rounds** burn when the mount is disabled (`ReadyAmmoFireChance` 0.3 × the burst's force): a propellant
    seat and the crew's flash.
  - **That fire doses the mount's magazine** as if down a gunhouse's trunk (`Fires.Magazines`, 300 s with charges
    burning, 1,200 without), for any mount, open or shielded. That's a fake path, like the calibre switch.
- **The ammunition train in the hitbox model:**
  - each mount has its magazine (`MountRow.Magazine`) and its barbette;
  - a handling room sits over each main mount's own magazine, shielded ones included (`RoomKind.Handling`,
    protection-plan H1); secondaries share battery magazines without one;
  - there's no working chamber apart from the barbette, no hoist trunk as a join in the cell graph, and no flash doors.
- **Magazine flooding** is fire's. `Fires.Magazines` floods a threatened magazine on the doctrine's
  `FloodMagazineS` unless smoke cuts the controls off; the sea or the main fills it; a drowned magazine can't go up, and
  its mounts lose their rounds. Damage control's own valves (counterflooding) are separate: one set of valves in the
  story, two in the code (damage-control.md DC4).
- **The supply:** ready-use rounds, then the hoists at `HoistShare` × the reload (`DesignerPlaceholders`, fire-control
  F6d); `CombatStore` keeps each mount's rounds. A magazine has no fill of its own.
- **The charge:** `DesignerPlaceholders.Charge` gives the mass (4000·d³·L/50) and the propellant from the gun block
  (black powder, cordite and kin, single-base, RP C/12). There's no packaging (bag or case). The magazine look burns
  `MagazineFastShare` 0.3 of it.
- **No navy values** since E8b: the flash chances, the torpedo's and fire's limit are one set of numbers for every side.
- **The look** (`docs/visuals/magazine-explosions.md`) draws tier 3 only. Its barbette column is built already, so
  tier 1 is the same event without the fireball. Its `MagazineBlast` has its own stand-in for the race (`PeakBar` 3.0,
  `PressureBar(t)`, the openings' failure times), which is display-only.
- **The benches:**
  - `damage-history` reports how a ship ended and what set the magazine off (by `MagazineVia`), and the turret-minutes lost;
  - `hits` prints magazine losses by cause;
  - `burst` is E5's gas cascade alone.

## What's wrong, against the research

| Gap | Today | The research | Why it matters |
|---|---|---|---|
| **Fake turrets** | every main mount ≥ 150 mm a gunhouse with a barbette | open-backed shields on cruisers' 6″ (Chester, the Towns) | 13% of Chesters lost to a magazine; record none (E8a item 1) |
| **No chain** | one roll per source | gunhouse → working chamber → hoist → handling room → magazine, a door at each step (02 §1.3) | doors and discipline can't matter |
| **No race** | a flash = ship lost | vents vs gas, the knife-edge `P* ∝ (S/A)^(1/(1−α))` (07 §2.3) | no Seydlitz, no Derfflinger C and D (E8a item 4) |
| **No partial outcome** | tiers 0-2 missing | contained, burned out, local breach (07 §6.3) | every flash costs the ship |
| **The hull** | lost whole | breaks at the magazine; an end magazine takes only the end (New Orleans) | where it went up doesn't matter |
| **Neighbours** | one magazine | the secondary magazine beside the main is often the trigger (Hood, Queen Mary, Barham) | — |
| **Two valve sets** | fire's flooding apart from DC's | one set of valves, one order | confusion, two places to tune |
| **No discipline** | — | doors propped, charges stacked: faster and far riskier (02 §1.3, Jutland) | the player's choice the record turns on |

## The principles

- **One gas model.** The race runs on E5's gas: the same boundaries, rupture pressures, vents and cascade
  (`Blast.cs`), fed by a burning source over seconds instead of one burst. There's no second pressure engine (single
  implementation).
- **One magazine state.** Heat, flooding, fill and burning live in one place (a `Magazines` module in Sim.Combat).
  Fire, the hits, torpedoes and damage control all feed it; none keeps its own copy.
- **Physics as design data.** The mount's housing, the charge's packaging, the turret's weight, sprinklers and
  interlocks are the design's. How the crew handles charges is the ship's orders. No navy values (E8b), no switches
  by type or size (no hard switches).
- **History is the yardstick.** The anchors are 07 §6.4 and damage-history's cases. Today's numbers prove only
  refactors.
- **Rough where it doesn't decide.** The race and the chain decide outcomes, so they're physical. The pieces after a
  break, their trim and their floating are the sinking's and the look's; here only "broken, which end" (rough for
  non-core).
- **Allocation-free and dt-proof.** Per-magazine arrays at spawn. A race costs only while it runs (seconds at the fine
  step). A 1 s and a 15 s step agree within the noise for the slow paths (heat, flooding).

## The yardstick

The first game's cases lead (1890-1925); the later ones check the mechanisms.

| Case | Record | Wanted |
|---|---|---|
| Seydlitz, Dogger Bank 1915: aft barbette holed, charges in transit, a door open between the aft turrets | both aft turrets burned out, 159 dead, magazines flooded by hand, survived | tier 1 in most runs, both turrets when the door's open |
| Derfflinger C and D, Jutland | burned, didn't explode; 73 of 78 and 80 of 80 dead | tier 1 in most runs |
| Lion Q, Jutland: face-roof joint holed, magazine ordered flooded | charges in the chain lit ~28 min later, masthead flame, the flooded magazine held | tier 0-1; flooded in time in most runs |
| British battlecruisers, Jutland (Indefatigable, Queen Mary, Invincible): ~3 lost in ~58 heavy hits | tier 3, broke at the magazine, ~1,000 dead each | ~5% a heavy hit (1-14% within the count) with Jutland's BCF discipline |
| The same ships on safe discipline | (the Grand Fleet's battleships, stricter) | well under the BCF's rate |
| Thirty 6″ HE on a battleship | Malaya's battery fire came close | about 1.5% of ships (the user's, 2026-10-15); none behind armoured casemates accepted (decision 7) |
| Chester, 17 × 15 cm on open shields | 3 of 10 guns out, no magazine | magazine losses near none |
| Gneisenau 1942 (later cuts): 23 t RP C/32 | no explosion, turret displaced | tier 1 |
| Hood, Mutsu, Vanguard, New Orleans (later cuts) | stern off; cut in two at anchor; midships gone; bow lost and survived | tier 3 aft; tier 3 in calm water; tier 4; end lost, ship afloat |

## The model

### The mount's housing (from the design)

A battery says how its guns are housed: a **gunhouse** (enclosed, with a barbette, a working chamber and hoists in a
trunk), a **shield** (open behind, ammunition up by hoist or whip to the deck, ready rounds by the gun), or **open**.
It replaces the calibre switch, and splits today's `dp`: a 5″/38's gunhouse from a destroyer's shield. Every reader of
"enclosed" takes it from the housing: the flash path, the crews' cover and flash, and fire's trunk to the magazine. The
design key isn't `mount` (the secondaries' position, and fire control's mount already use that word: one word, one
meaning). Working name `housing`.

**The housing is selected, at any calibre** (the user, 2026-10-20). It's the designer's choice for each battery, not
something the calibre decides:
- **Small guns had enclosed gunhouses too.** Examples: the Monmouths' twin 6″ turrets (1901), Fubuki's fully enclosed
  12.7 cm destroyer gunhouses (1928), and the USN's enclosed 5″/38s. Big guns could stand behind shields: the Towns' and
  Chester's 6″.
- **No rule ties the two.** A new battery may start from a suggestion, but the design keeps what was chosen, and nothing
  refuses a combination (limits come from gameplay, never block).
- **The choice is felt three ways:**
  - in the weight, which shipgen weighs from the housing, so a gunhouse is heavier than a shield, and in the cost in
    the campaign;
  - in the crews' cover from splinters and weather;
  - in the flash path to the magazine, which only a gunhouse's trunk has.

### The ammunition train (an enclosed mount)

The stations are the gunhouse, the working chamber (in the barbette), the hoist trunk, the handling room and the
magazine. Each has:
- **charges lying open**: in transit by the drill, more by the discipline (stacked in the handling room, cases opened);
- **a door or scuttle below it**: shut, or open a share of the time (the hoist's cycle, the discipline), and failed
  when struck (an interlock that failed, 02 §1.3).

Charges lit at a station burn there: the station's men flash (`Casualties.Flash`) and a propellant seat starts its
fire (fire B's seat). The flame goes down one station past each open door, a roll per door, and stops at a shut one.
Every source above lights the station it reaches: a burst in the gunhouse lights the gunhouse, spall in the working
chamber lights that, gas in a handling room lights that. A burst among the magazine's charges lights the magazine.

The hoist trunk is the train's own links, not a new join in the cell graph (as built in V2): E5's hot gas reaching a
handling room lights its station, and fire's dose down an enclosed mount's trunk (B2) stays. It's one link for flash,
gas and fire.

**A shield or open mount** (decision 4):
- **No flash goes down from the gun.** Its hoist ends in a scuttle at the deck, and an open gun vents its flash to the
  air. Its ready rounds burn on deck, as today: fire's seat and the crew's flash.
- **Its train starts below the deck:** the handling room or passage the charges come up through, then the magazine. A
  burst, spall or gas there lights the charges in transit, and the flame goes down by that door, as an enclosed mount's
  does.
- **Fire reaches the magazine only the ways fire goes:** through the decks, by heat (fire B2). Today's dose "down its
  turret's trunk" stays for gunhouses only.
- **Ammunition passages and casemate batteries are stations** where the design has them: charges on their way along the
  ship, with doors, like a handling room (Defence's passages, Malaya's battery).

### The venting race (a magazine alight)

Once a magazine burns:
- **gas made** `∝ S·β·P^α`. S is the burning surface: the fill (rounds left, from `CombatStore`) × the packaging
  (bags all at once, cases one by one) × the propellant's sensitivity, × (1 − flooded share).
- **gas out** `∝ A_vent·P`. A_vent is the open chain (the trunk, the barbette), then each boundary E5's cascade breaks.
- **The gunhouse lifts** at its revolving weight × g over the barbette's area (07 §2.2: about 0.13 MPa for a 15″). It's
  a big vent: the flame column.
- **Integrated over seconds** (07 §6.2's ODE, a few ms steps while it runs, then off). Where the pressure tops out
  gives the tier:

| Tier | When | What's left |
|---|---|---|
| 0 contained | flooded or sprinkled before the gunhouse lifts | the turret out, its men by the flash, a fire |
| 1 burned out | the gunhouse lifts and the pressure stays under the decks' and sides' | the turret destroyed, the train's men dead, a heavy fire; she fights on |
| 2 local | some decks or sides go; the girder holds | E5's holes into the flooding; she may founder later |
| 3 severed | the girder at the station breaks (below) | lost, broken in two at the magazine |
| 4 two groups | the race reaches another magazine (hot gas into it, as E5's) | lost, the middle gone |

A neighbour magazine reached by the cascade's hot gas is lit, and its race joins: Hood's 4″ to her 15″.

The propellant's β and α aren't in the sources (07 §7). They're fitted to the anchors [EST], one row per propellant
kind in the explosives' catalogue beside the fillers. `MagazineFastShare` goes; the look reads the burned share from
the race.

### The hull at the magazine

The cascade's broken boundaries at the magazine's station give `d`, the share of the girder's section lost (the deck,
sides and bottom plate there, from the cells' boxes and plates). The check is 07 §3.1: d under ~0.5 holds in calm
water, 0.5-0.8 breaks in a seaway (the weather's sea), over 0.8 breaks at anchor. An end magazine (under ~0.12 L from
an end) opens that end to the sea and the ship may live (New Orleans). A broken ship is `LossKind.BrokeInTwo`: she goes
down fast, as the record has it (decision 3). The pieces' own floating and the look of the break come later, with the
sinking and the VFX (07 §4, `sinking-vfx-research.md`).

### Flash discipline (a doctrine row)

A row in the side's doctrine, by period, as damage control's (decision 2): **safe**, **drill** and **rate first**.
- **safe:** doors shut, interlocks kept, charges only in transit.
- **drill:** the period's practice.
- **rate first:** doors propped, charges stacked in the handling room and working chamber, cases opened (the BCF at
  Jutland).

Rate first gives the handling room's stack as more ready rounds and a shorter hoist cycle (`HoistShare` by discipline,
fire-control F6d). It also gives more open charges and doors open more of the time. A scenario picks a side's doctrine
as it does damage control's; per-ship orders can come later. Felt both ways: the rate in `duel`, the risk in
`damage-history`.

### Flooding and sprinkling (one set of valves)

The magazine's flooding moves out of `Fires.Magazines` into the magazines module, on damage control's valves. It's
ordered by:
- fire's heat threat (B2's rule, unchanged);
- a flash in the train above it (Harvey: the order, then the valves' time);
- the doctrine.

The controls are cut by smoke or fire over them (as today) or by lost power (Yamato). Sprinklers, where the design has
them, wet the charges in a fraction of the time. The water lowers S (Lion's held), and a drowned magazine can't go up.
The mounts lose their rounds, as today.

**The valves can fail (the user, 2026-11-05: "the flash triggered order definitely should be able to fail"; built from research `11-magazine-flooding.md`).** The research is plain about where: of 13 battle flashes that stopped short, none records an order that failed, even with whole turret crews dead, because the gunner, the executive officer or the bridge gave it too. Every recorded failure is at the valves (Liberté, Glatton, Benedetto Brin, Imperatritsa Mariya in part). So it's built there:

- **The order** still follows every flash that stops short: its walk drawn, median 120 s, 30-360 s (`MagazineOrderSigma`, `MagazineOrderRangeS`) [EST 11 §2.2].
- **At the valves** (`Reach`, `Fires.ValvesCut`): a smoky space over the magazine, where its station stands, gives one try: through with 0.4 in 3-15 min (Seydlitz), else driven back until it clears, then the walk again (Liberté) [EST 11 rec. 3]. The fire's own order goes the same way, where it used to stand refused.
- **The fill** (`Fill`): one flood a magazine, against the falling head, 20 min to full (`FireMagazineFloodS` 1,200 s: the USN's 1917 rule; Saint Louis 30; Leonardo da Vinci over 20) [SRC 11 §2]. Half at 9.1 min, full at 20.0, 2.20 x on Lion's Q, as the note's physics has it. What stands over the waterline fills from the main. Note 11 reads the old fill's slowing as Torricelli by chance; it was the upper cell's valve draining into the lower and its flow stopping when that was full: one valve a cell is gone.
- **The card** says ordered, getting through smoke, cut off by smoke, then the share filling. Tests: `Men_driven_back_from_the_valves_go_again_when_the_smoke_clears`, `A_magazine_fills_in_the_rules_time_slowing_as_it_rises`.

**What came out** (`losses`, 4 seeds at Jutland and the Dogger Bank, 8 at Coronel and Tsushima; every order, the fire's too):

| A seed | Valves opened | Through smoke | Driven back | Met smoke | Lost to a magazine: before, now |
|---|---|---|---|---|---|
| Jutland | 39.0 | 0.0 | 1.3 | 3% | 5.25, 5.3 |
| Dogger Bank | 13.0 | 0.0 | 0.3 | 2% | 0.3 now |
| Tsushima | 28.3 | 1.4 | 4.0 | ~17% | 0.9 now |
| Coronel | 4.6 | 1.5 | 3.5 | ~60% | 1.1 now (45% of losses, 45% before) |

- **It falls where the record has it:** Coronel's battery fires cut the valves off most, a dreadnought's turret flash almost never. On note 11's yardstick (a stopped-short train flooded in 80% of runs or more) Jutland, the Dogger Bank and Tsushima pass; Coronel's fires are the Good Hope kind. So **a battleship's turret flash still nearly always floods**, as the record has it.
- **Set-offs barely move:** nearly all come by a hit, where flooding has no time. At Jutland 14 of 16 a seed by a hit.
- Coronel's British cruisers broken 0.5 a seed (1.0 before), Germany's capital ships 0.6 (0.25 before): counts of 4-5 in 8 seeds, not read as a change. Asama class still blowing (Japan's cruisers 0.8 a seed).

**Not built from note 11:** sprinklers by navy and year (USN 1917, RN 1918; corrects note 10), periods' own fill times (900-1,800 s), a second or remote valve station as a design option, keys in harbour and dry dock, the countermand (Liberté's Garnier), `damage-history`'s flooding yardstick.

**A dry layer can go up (the user, 2026-11-05: "as long as there is a dry layer it should go boom"; and, on a bare chance of no order: "let's not add artificial things").** A fire's heat now sets off any magazine short of full (`MagazineFullShare` 0.995), where it used to stop at half (`FireDrownShare`, which still says when a space itself stops burning). Hits and flashes needed no change: the race already burns the dry share of the charges (`MagazineRace`). So what water buys, until it's full, is a smaller fire. Leonardo da Vinci's case: flooding "almost completed", her top layer went.

What it did (`losses`, same seeds as above; set off by a fire, a seed):

| | Before | Dry layer | Broken by a magazine, a seed: before, now |
|---|---|---|---|
| Jutland | 2.4 | 2.4 | 5.4, 6.7 |
| Dogger Bank | 0 | 0 | 0.3, 0.3 |
| Coronel | 1.0 | 1.4 | 1.1, 1.1 |
| Tsushima | 2.0 | 3.4 | 0.9, 0.9 |

The extra set-offs are small fires: Tsushima's by a fire went Contained 0.1, BurnedOut 0.8 to 0.8, 1.5, with those that break a ship unchanged. So it shows where fires meet part-flooded magazines, and the dry layer is often too small to take her. Jutland's rise isn't this rule: its fire set-offs didn't move and the rise is all hits, which the rule doesn't touch (`heavy hits` 1,835 to 1,995 a seed: the run diverged, four seeds).

## The steps

Each step shows `damage-history`'s magazine lines before and after (the cases above, by tier and cause). The order
follows the dependencies: the fake turrets first, because the chain means nothing while light cruisers have barbettes.

- **V0. The bench.**
  - `damage-history` reports tiers (only 3 today), turrets burned out, and stations reached in the train.
  - `shipgen magazine DESIGN [--mount N] [--discipline D] [--flooded S]` lights one magazine or station, and prints the
    chain, the race's pressure by the ms, what broke, d, and the tier. It's the race alone, like `burst`.
  - Records today's numbers here.
  - **Done 2026-10-20.** What it came to:
    - **The trains are class data** (`ShipClass.Train(mount)`, `Trains.cs`), top first. An enclosed mount
      (`ShipClass.Enclosed`, a gunhouse on a barbette or a light one) has gunhouse > working chamber (its barbette) >
      handling room > magazine. A casemate has casemate > magazine. A shield's train starts at its handling room, or at
      the magazine without one. `StationOfPart` and `StationOfRoom` find a part's or room's place in it.
    - **`shipgen magazine DESIGN...`** lists each magazine (tonnes, volume, its place against the waterline, the mounts
      it feeds) and each mount's train. It already shows V1's problem: V25's 88 mm and Chatham's 6″ come out as gunhouses
      with working chambers. Seydlitz's 88 mm magazines list 0 m³ (a stowage quirk, not this plan's).
    - **`damage-history`** has a magazines line: magazines set off per 100 hits (and per 100 hits on plate over 6″), the
      tiers (all severed until V3), and charges lit in a train per 100 hits with the stations each (0 until V2).
      `EffectsResult.Stations` carries the count.
    - **The sim is unchanged:** the same runs as before, case by case.
  - **Today's numbers** (`damage-history`, 40 runs a case; "lost" is the share of runs lost to a magazine):

    | Case | Lost | Set off per 100 hits | How |
    |---|---|---|---|
    | Jutland: British APC on Seydlitz (the count) | 28% | 1.58 | turret 11 |
    | Seydlitz, Jutland, 21 heavy hits | 25% | 1.32 | turret 10 |
    | Derfflinger, Jutland | 23% | 0.97 | turret 9 |
    | Lion, Jutland | 8% | 0.60 | turret 3 |
    | Tiger, Jutland | 10% | 0.59 | turret 3, spall 1 |
    | Indefatigable, Jutland | 3% | 0.50 | turret 1 |
    | Warspite, Jutland | 5% | 0.34 | turret 2 |
    | Lion, Dogger Bank | 10% | 0.65 | turret 3, spall 1 |
    | Chester, Jutland | 8% | 0.45 | turret 3 (its fake barbettes) |
    | Kent, the Falklands | 0% | 0 | |
    | Thirty 6″ HE on a battleship | 0% | 0 | |
    | Destroyer under 6″ lyddite | 5% | 0.51 | handling room 2 |
    | Orel, Tsushima | 3% | 0.05 | turret 1 |
    | Mikasa, Tsushima | 8% | 0.20 | turret 3 |
    | Yalu (Inflexible for Dingyuan) | 10% | 0.05 | turret 4 |
    | Bismarck 1941 | 93% | 0.62 | turret 36, spall 1 |
    | South Dakota 1942 | 25% | 1.09 | turret 9, spall 1 |
    | Kirishima 1942 | 13% | 0.69 | turret 5 |
    | Samar 1944 (a destroyer) | 3% | 0.83 | handling room 1 |
    | WWII destroyer, 10 × 5″ | 13% | 1.30 | handling room 5 |
    | WWII destroyer, 40 × 5″ | 28% | 0.91 | handling room 11 |
- **V1. The housing.**
  - `housing` on batteries (gunhouse, shield, open); shipgen's turret types take it, and the calibre switch goes.
  - The designs say theirs: Chester and the Towns shield, the destroyers' guns shield, the rest as they are, checked
    one by one.
  - Every reader of "enclosed" takes the housing (`Effects.Enclosed`, `Casualties.Enclosed`, fire's trunk dose in
    `Fires.Magazines`); shield and open mounts lose the trunk dose (decision 4).
  - The designer's battery tables (main and secondary) let it be picked for any battery at any calibre (one cell, no
    new pane); its weight follows in the ledger.
  - Goldens move, so `ShipDesign.Version` bumps. Closes bugs.md's item and E8a item 1.
  - **Done 2026-10-20.** What it came to:
    - **`housing`** on main and secondary batteries: `barbette` (an armoured turret on a barbette, the drawing's `bb`),
      `gunhouse` (a light enclosed one, `dp`) or `shield` (a shield on a pedestal, open behind, `open`). There's no
      separate "open" (unshielded): the sim has no difference between them yet. A casemate is the secondary's position,
      not a housing. `Housings.cs` holds the words, the shapes, the check and `Suggest`: the old calibre rule kept only
      as a suggestion for a battery that doesn't say.
    - **Design format 12** writes each battery's housing as its calibre gave it, so every design builds as before; then
      the shields were set by hand, design by design:
      - the Great War's destroyers (abdiel, b97, flotilla_leader, g101, k_acasta, l_laforey, m_class, r_class,
        scout_cruiser, v1, v25, ikazuchi, tb_destroyer);
      - the light cruisers (arethusa, aurora, cambrian, caroline, the Towns, wiesbaden, gazelle, graudenz, pillau,
        stettin, karlsruhe, light_cruiser);
      - the 3-4″ on the dreadnoughts and battlecruisers (dreadnought, agincourt, bellerophon, colossus, kaiser, neptune,
        st_vincent, lord_nelson, kongo, colorado, invincible, indefatigable, lion, queen_mary, battlecruiser, erebus,
        engadine);
      - the 88 mm on the German battlecruisers and the armoured cruisers (seydlitz, derfflinger, moltke, von_der_tann,
        devonshire, duke_of_edinburgh, minotaur, warrior, armoured_cruiser);
      - also gangut's and kent's secondaries, bouvet's 100 mm, and q_ship's and tramp_1914's guns.
      Kept as gunhouses: dante's 120 mm and bouvet's 138.6 mm (both in turrets), and the treaty-era and later ships.
    - **The weight:** a shield's armour is the front half of the ring, with no roof and no rear [EST], and no barbette.
      Displacements move by tens of tonnes (dreadnought 24,862 to 24,843 t, light_cruiser 6,411 to 6,361 t).
      Borodino's +8 t is the sizer's knock-on.
    - **The sim:**
      - `ShipClass.Enclosed` (a barbette or a gunhouse) is the one reader for the crews' cover and flash
        (`Casualties`) and for fire's trunk dose (`Fires.Magazines`). A shield's burning ready charges no longer dose
        its magazine (decision 4).
      - The flat turret roll in `Effects` stays a barbette's until V2's train gives every gunhouse its doors. Giving a
        gunhouse the flat 0.25 at once put the WWII destroyer's losses past `DamageTests`' band (22% against 20%).
    - **The designer:**
      - a HOUSING column in both battery tables (a combo; a casemate shows its position);
      - `main{i}_housing` and `sec{i}_housing` knobs (for `-steps=`);
      - the secondaries' mount tip names the housing;
      - the gun block tip's "dp" now comes from a gunhouse. Before, it read the position and was never set.
    - **Goldens:**
      - all 372 design cases change (the design echo), with SVGs and sprites for the redrawn guns;
      - ShipDesign.Version 46, noted in `shipgen/golden/README.md`;
      - `BakeTests` skips a turret type Python never drew for that design;
      - `mono-check` goes from 37 reversals and 23 lumps to 38 and 21: the sizer's bistable layouts reshuffle
        (Kongo's main-calibre reversal moved from 350→356 to 340→350), not a new monotonicity break.
    - **The numbers** (`damage-history`, 40 runs; V0 → V1):
      - Chester lost to a magazine 8% → 3%, the one left a burst in a handling room, V2's to door;
      - killed a hit 0.38 → 0.43 against the record's 2.0: her crews are now in the open, so what's left is casualties',
        not this plan's;
      - the rest move within the noise (the capital ships' 3-4″ lost their barbettes, which changes the hit geometry
        and the draws): the Jutland count 28 → 18%, Derfflinger 23 → 13%, Lion 8 → 13%, Lion at Dogger Bank 10 → 5%,
        Indefatigable 3 → 0%, Kirishima 13 → 10%, the destroyer under lyddite 5 → 8%; the rest unchanged.
- **V2. The train.**
  - The stations and their doors as class rows (from the mount, barbette, handling-room and magazine links). A shield
    or open mount's train starts at its handling room (decision 4).
  - Ammunition passages and casemate batteries as stations where the design has them; until shipgen emits passages,
    a casemate battery's own space is its station.
  - The hoist trunk as a join for gas and fire.
  - Every source lights its station; the flame goes down by the doors. `TurretFlashChance` goes;
    `MagazineFlashChance` stays as a burst among the magazine's charges.
  - Until V3, a magazine reached is lost as today.
  - **Done 2026-10-20.** What it came to:
    - **`Magazines.Light`** (`Sim.Combat/Magazines.cs`): charges lit at a station burn there, and the flame goes up and
      down the train past each door that stands open, a roll a door. Each station it reaches burns its charges:
      `StationCharges` 1 a gun [SRC: Brown, about 8 in a twin turret's train in action], the men's flash (a turret's
      train; a casemate's as a battery's), and a propellant seat (fire's B1) in the part or the room's first cell. A
      flame reaching the magazine sets it off (lost until V3). `EffectsResult.Stations` counts the stations lit.
    - **The sources:**
      - a burst in a gunhouse, its working chamber or a casemate lights its station with `StationLightChance` (0.25
        [EST], the old turret roll's);
      - a mount's ready charges set burning at a station light it without a roll (a shield's are on deck: none);
      - E5's hot gas in a handling room, or a burst there, lights the handling room, a shield's too (decision 4);
      - E3's spall lights the station it reaches;
      - a burst among a magazine's charges keeps `MagazineFlashChance`.
      `TurretFlashChance` and `Effects.MagazineUnder` are gone.
    - **The doors** (`StationDoorOpen`, the drill's until V5): 0.1 below a gunhouse, a working chamber and a handling
      room [EST, 10 §3.3: each opens as the cage passes, 5-15% of the time]; a casemate's 0.02, its hoist and its
      passage's magazine door in series (10 §3.3, §5: Kent's flash stopped at the shut magazine door). From the
      gunhouse the flame reaches the magazine 0.2% of the time, from the working chamber 1%, from a handling room 10%,
      from a casemate 2% (`shipgen magazine --runs`).
    - **Every reader of "enclosed" takes `ShipClass.Enclosed`:** a light gunhouse now has its train, and its burst's
      gas is held in it (`PartGas`'s vent share), as a barbette turret's is.
    - **The trunk** is the train's links, not a cell-graph edge (the model above).
    - **The research** (10, arrived during V0-V1) set the doors and charges. A first cut with the casemate's door at 0.1
      lost a QE to her casemates' magazine in `FireOrderTests` within four minutes: the record's casemate fires
      (Malaya, Kent, König, Moltke) never reached one.
    - **Tests:**
      - `DamageTests`' broken-shell case counts charges lit in the train (0.05-0.5 of turret hits, the light chance's
        0.25), not magazines reached;
      - `FireOrderTests` stops its watch when the shooter herself is lost, as it did for the target.
    - **The numbers** (`damage-history`, 40 runs; V1 → V2):
      - magazines set off by hits go to almost nothing in drill: the Jutland cases, Lion, Tiger, Warspite, Chester and
        the destroyers 0% (from 3-28%); Bismarck 93 → 13% (lost 40%, mostly foundering now); Orel 3% (a fire later);
      - the charges burn in the turrets instead: flashes per case (40 runs) the Jutland count 13, Seydlitz 17,
        Derfflinger 8, Lion 5, Bismarck 135, killing about 20-35 each, the German turrets' record (E8a item 4) in
        everything but the turret's own end, which is V3's tier 1;
      - charges lit in a train 0.3-1.8 per 100 hits on the capital ships, 2-4 on the WWII destroyer;
      - the British battlecruisers' losses (Jutland's ~5% a heavy hit) wait for V5's rate first, their doors propped,
        and V3's race. Until then the magazine risk sits under the record for every side: end-goal rules, the plan
        restores it.
- **V3. The race.**
  - The ODE on E5's cascade, the gunhouse lift, tiers 0-4, the neighbour link.
  - The packaging (decision 1), the turret's revolving weight from shipgen's weights, and the fill from the rounds.
  - The ship is lost only at tiers 3-4. Tier 1 destroys the mount and lights the fire's seat; tier 2 leaves its holes.
  - The charge as design data: the shells plan's deferred `charge` (`{kg, propellant, packaging}`), read by the
    muzzle flashes too, replacing `DesignerPlaceholders.Charge`.
  - The record gains the tier, the burned share and the boundaries' failure times for the look. `MagazineBlast`'s
    stand-ins (`PeakBar`, `PressureBar`, `MagazineFastShare`) read those instead (one model).
  - Closes E8a item 4.
  - **Done 2026-10-20.** What it came to:
    - **The packing is gun-block data** (decision 1): `packaging` on `gun_tech` blocks (bag, clarkson, brass, tank),
      design format 13 writing each block's by its propellant (double-base in Clarkson cases, single-base in tanks,
      diglycol and later in brass). The catalogue gains "…, double-base, brass-cased" blocks for the Victorian, Great
      War, Treaty and Second War periods, and the 21 German designs fire them. The charge's mass stays
      `DesignerPlaceholders.Charge`'s; it now carries the packing.
    - **The burn law** on `PropellantRow` (10 §1.1): β, α above and below the knee, impetus, density, surface per kg,
      web and grain form. Double-base is cordite MD's (a cord), single-base and black powder as sourced, and diglycol,
      triple-base and LOVA cooler kin in tubes [INFERRED].
    - **The lift:** each mount's revolving weight from Shipgen's `MountWeights` (`MountRow.RevolvingT`), over its
      barbette's bore (`ShipClass.LiftKPa`, `BoreM2`).
    - **The race** (`Magazines.SetOff`, `MagazineRace.cs`): magazine_race_ref.py's mass balance on one gas volume.
      - The volume is the magazine's spaces, and its handling room's once the scuttle gives at 40 kPa.
      - Open charges light within 0.5 s; packaged ones light one from the next with R by packing (bag: all open;
        Clarkson 4, brass 1.4, tank 1.2 [EST]). The flooded share never lights. Cohorts of lit charges are kept on
        the stack.
      - The ways out: the spaces' own vents; each enclosed mount's trunk (0.05 of the bore) once its roof gives at
        0.4 of the lift; a shield's or casemate's hoist (0.25 m²) at the scuttle's pressure; then the research's
        table for the magazine's boundaries (`MagazineGiveKPa`, `MagazineGiveM2`).
      - A magazine across the bulkhead lights when it gives.
      - The tier comes from the girder share (deck 0.35, sides 0.45, bottom 0.2; severed at 0.8) and the gunhouses
        vented. A Lion magazine runs in 2-13 ms and allocates nothing.
    - **The approved exception** (the user, 2026-10-20): the race's boundaries go by the research's table (10 §6: the
      bulkhead to the next space 250 kPa and a 4 m² tear, the armoured deck 700 kPa and 10 m², the sides 1,000 kPa and
      60 m², the bottom 2,000 kPa and 60 m²), not by E5's rules on the ship's own plating. Run on her plating, the
      race never reached the girder. Lion's magazines sit inside 7-11 mm decks and bulkheads that give at 0.35-0.45
      MPa, the gas spreads into the ship faster than it builds, and every packing plateaus at 0.4-0.5 MPa. Five
      versions of the spreading were tried: tears as vents, a fill model, two zones, the second zone loaded, a panel's
      delay. V4 or V8 may come back to it.
    - **All set-offs go through the race:** a hit's, a torpedo's and fire's heat (B2). That pulls V7's first bullet in.
      - Tiers 3-4 lose the ship as before; the blast record now carries the tier and the propellant burned by the
        girder's failure and a second after (`FastKg`).
      - Tiers 0-2 leave her afloat. The magazines alight are spent (their mounts out of ammunition), and the vented
        gunhouses and barbettes are destroyed and burning. The trains' men are flashed (`Casualties.Train` moved here
        from `Effects`; a contained fire flashes only the magazine's men). Holes open to the sea where the sides or
        bottom gave.
      - The dev's `-sink=SHIP:magazine` still sets one off outright.
    - **Not done here:**
      - `MagazineBlast`'s stand-ins (`PeakBar`, `PressureBar`, `MagazineFastShare`) still drive the look. The record
        carries what they need, for the look's step.
      - The charge's mass as design data waits for an ammunition step.
    - **The bench:** `shipgen magazine --race [--flooded S] [--packaging P] [--trace]` sets each magazine off on a fresh
      ship. `damage-history` reports set-offs by tier.
    - **Lion's magazines on the research's anchors:**

      | Lion's stow | Peak | Tier | Research (10 §6) |
      |---|---|---|---|
      | bags alone (lids off) | ~1,000 kPa | severed (two groups where a neighbour lit) | tier 3 at 1,003 kPa |
      | Clarkson cases (as designed) | 340-560 kPa | burned out | tier 1 at 273 kPa |
      | brass cases | 110-180 kPa | burned out | tier 0-1 (Gneisenau, Seydlitz) |
      | bags, 95% flooded | 40 kPa | contained, a twentieth burned | tier 0 from ~95% |

      Seydlitz as designed burns out (46-158 kPa). The V25 and Chatham's shields are contained.
    - **Tests:**
      - `MagazineRaceTests`: the anchors, a neighbour magazine across the bulkhead, and nothing allocated;
      - `DamageTests`' two record tests give their magazine R 1000, so it severs as their cases want.
    - **The numbers** (`damage-history`, 40 runs; V2 → V3): almost no change, as V2's drill doors seldom let a flame
      reach a magazine. The few that do now burn out: Bismarck's four (brass), Orel's one contained. Ships lost to a
      magazine stay near none until V5's rate first props the BCF's doors and opens their lids.
- **V4. The hull at the magazine.** `d`, the girder check with the sea, the end case, `BrokeInTwo`.
  - **Done 2026-10-20.** What it came to:
    - **The check** (`Magazines.Girder`; 07 §3.1): the girder breaks when the moment at the magazine passes what's
      left of its capacity, K·(1 − d) ≤ sin(π·x/L)·(still + wave·sea + whip).
      - `MagazineGirder` [2.5, 0.35, 0.6, 0.35] [EST] is fitted to 07's bands: amidships in calm water it breaks past
        d ≈ 0.75, in a seaway past ≈ 0.5; at a quarter of the length past ≈ 0.84 calm and ≈ 0.69 in a seaway; near
        the ends hardly ever.
      - d is V3's (the deck 0.35, the sides 0.45, the bottom 0.2).
      - The station is the magazines' tonnage-weighted middle along the hull's cells.
      - The sea is the wind at the ship over `MagazineSeawayMs`, 15 m/s, a moderate gale [EST].
      - `RaceResult` carries `Station`, `GirderLoad` (1 or more: broken) and `EndLost`.
    - **The end case:** a magazine within `MagazineEndShare` (0.12) of an end that loses `MagazineEndD` (0.8) of the
      section without breaking her takes the end off. Every space beyond it toward that end is opened to the sea at
      its cut (a quarter of its section), and the flooding decides whether she lives (New Orleans). Gangut's Y with
      bags is the case: her stern open, her girder at 0.42 of what's left.
    - **The loss stays `LossKind.Magazine`**, not `BrokeInTwo`: it's the look's event, and the record's tier says
      severed. The pieces apart are the sinking pass's (07 §4).
    - **Lion with bags alone:**

      | Magazine | Station | Calm water | A seaway |
      |---|---|---|---|
      | Q (midships) | 0.53 | severed (load 1.39) | severed (2.59) |
      | A, B (forward) | 0.79-0.81 | breached (0.80-0.84), her deck and sides gone | two groups (1.49-1.57) |
      | Y (aft) | 0.15 | breached (0.63) | two groups (1.17) |

      A breached magazine leaves about 30 m² of side and the deck over it open, so she founders fast unless her
      subdivision holds.
    - **Bench:** `shipgen magazine --race --sea S` gives each magazine's station, the girder's load and an end lost.
    - **Tests:** `MagazineRaceTests` covers amidships breaking in calm water, the quarter breaking only in a seaway,
      Gangut's stern lost with her afloat, and the neighbour magazine's two groups in a seaway.
    - **For V8:** Jutland's light airs (about 4 m/s, 0.27 of a seaway) put an aft magazine at 0.2 of the length right
      on the edge (load ≈ 1.0). The battlecruisers' record, three broken in about 58 hits, is the check.
- **V5. Flash discipline.**
  - A doctrine row by period (decision 2), picked by the scenario with the side's doctrine, and the supply.
  - Jutland's British side on drill. The BCF's rate first would need a doctrine of its own for that squadron, which is
    for the scenario to choose.
  - **Done 2026-10-20.** What it came to:
    - **`FlashDisciplines`** (settings), each a row [EST, 10 §3]:

      | Discipline | Doors open (gunhouse, chamber, casemate, handling) | Charges a station, a gun | Lids off | Hoists |
      |---|---|---|---|---|
      | safe | 0, 0, 0, 0 | 0.5 | 0 | ×1.7 |
      | drill | 0.1, 0.1, 0.02, 0.1 | 1 | 0 | ×1.5 |
      | rate first | 1, 1, 0.5, 1 | 2 | 1 | ×1.0 |

      Rate first's lids are off in the magazine itself, as 10 §6.2's BCF anchor has it. 10 §8 leaves open whether
      they came off there or only in the handing room (Friedman against Brown); V8 or a researcher may move it.
    - **A doctrine row names its discipline** (`DamageControlDoctrine.Flash`): 1890-1916 drill, 1942 and 1944 safe.
      The new **"1916 BCF"** row is 1916's with rate first, a side's to choose: a scenario's `"damage_control"`, or the
      benches' battlecruiser cases (Indefatigable, Tiger). Jutland's British side stays on 1916. Per-squadron doctrine
      is the scenarios' later step.
    - **Each ship carries it** (`ShipCondition.Flash`, read with her side's orders). `Magazines.Discipline` falls back
      to the default doctrine's for a ship whose side hasn't been read: a bench's, a test's.
      - The train's doors and its stations' charges read it, and so does the race: its open charges are those in the
        scuttles plus the lids-off share.
      - The hoists' sustained supply goes as its HoistShare over drill's 1.5: `CombatStore.Fired`'s supply factor,
        from `DamageSystem.FlashOf(side)` in the gunnery.
    - **Lion at Jutland's sea** (0.27 of a seaway), one magazine set off:
      - drill or safe: every magazine burns out, 340-510 kPa;
      - rate first: about 1,000 kPa. Q severs her (load 1.72), the forward group breaks her as two groups (1.04-1.15),
        and A alone (0.99) and the aft group (0.78) breach her.
    - **Rate of fire:** a Lion turret's 30 salvos take 1,188 s on rate first against 1,485 s on drill (the hoists past
      the ready rounds).
    - **Bench:** `shipgen magazine --doctrine NAME` for both `--runs` and `--race`.
    - **Tests:** rate first severs Lion where drill burns out, its flame from the gunhouse always reaches the
      magazine, and its hoists are faster.
    - **The numbers** (`damage-history`, 40 runs):
      - Tiger on the BCF's doctrine loses 8% of runs to a magazine (one breached, one severed, two as two groups;
        record: she lived);
      - Indefatigable's case still loses none in 40 × 5 hits (record: 3 battlecruisers in ~58 hits);
      - the rest as V3.
      How often a hit lights a turret's charges (`StationLightChance`) is V8's to fit.
- **V6. One set of valves.**
  - The flooding moved and ordered as above.
  - Sprinklers as design data (a fit by period, in the outfit block).
  - Power and controls cut.
  - **Done 2026-10-20.** What it came to:
    - **`Magazines.Valves`**, stepped every damage step before the flooding (`Fires.Magazines` keeps the heat and
      the order, and its `Fill` is gone).
      - An ordered magazine's valves open at the order's time and fill it in `FireMagazineFloodS` (10 min), from the
        sea under the waterline, else its section's main.
      - This follows damage control's valve rule (DC4): at the valves' rate while she's conned; by hand, a third of it
        after `CounterfloodHandS` (3 min), when she isn't.
      - Its water is let in on purpose (`Counterflooded`), so the pumps leave it. A first cut let the pumps take it
        back out: Lion's Q sixth tier stood at 120 of 381 m³ after a quarter of an hour.
    - **The orders:**
      - fire's heat threat, B2's rule unchanged (`Magazines.Order` at once: the threat's own clock was the decision);
      - a flash in a train that didn't reach the magazine (`Magazines.Light`): its magazine ordered
        `MagazineOrderS` later (2 min, research 10 §4.2's 1-5 min), unless her doctrine never floods one.
    - **Sprinklers:** `outfit.magazine_sprinklers`, an outfit setting with the period's templates (interwar and
      post-Savo true, the earlier ones false: research 10 §4.1 finds none before the 1930s USN), read into
      `ShipClass.MagazineSprinklers`.
      - An ordered magazine's stow is wetted to `MagazineSprinkleShare` (0.95) in `MagazineSprinkleS` (60 s) while
        its section's main has water (`ShipCondition.MagazineWet`).
      - A wet magazine can't cook off, and the race counts the wetted share as flooded.
    - **Controls cut:**
      - smoke over the magazine still blocks fire's order (B2);
      - by hand when she isn't conned;
      - no main, no sprinkling.
      - Lost power stopping remote controls (Astoria) waits for a later cut's remote valves: the first game's are
        worked by hand.
    - **Lion's Q** (`MagazineRaceTests`): a flash stopped at shut doors orders her magazine flooded; a quarter of an
      hour on, set off with every lid off, it burns out at 41 kPa, 2.7 t burned (dry: severed).
    - **Fire's yardstick moved toward the record:** `fire-history`'s Santiago cruisers, unorganised, lost 65% (a
      magazine cooked off at ~100 min) and now 0%. Their magazines still go up from the fire, but now burn out (V3).
      fire.md B5 had "the record has them burned out, not blown up".
    - **Goldens:** the outfit echo gains `magazine_sprinklers` (254 cases), ShipDesign.Version 48.
- **V7. Every other source into the race.**
  - Fire's heat limit lights the magazine instead of losing the ship.
  - A torpedo's gas and shock reaching the magazine light it (`TorpedoMagazineChance` goes): Pommern.
  - Magazines going up as she sinks or capsizes (Barham, Yamato, Kongō) wait for the sinking pass (decision 5).
  - **Done 2026-10-20.** What it came to:
    - **Fire's heat** was already in the race from V3 (`DamageSystem`'s fire step: `Fires.Step` sets it off, the race
      decides, the ship lives unless it severs).
    - **The torpedo** (`Underwater.Apply`): the nearest magazine within the reach is lit as by a burst among its charges,
      `MagazineFlashChance`, whole in the half of the reach the explosion wrecks and fading to none at the reach (no
      switch at the edge). A TDS that holds keeps it out, as before. `TorpedoMagazineChance` is gone. The race runs on
      the magazine as it is at the moment of the hit: the hole is under water, so it vents no gas.
    - **The bench** (`shipgen torpedo DESIGN --shooter v25 --n 400 --settle 60`), a magazine lit a hit, before -> after:
      super_dreadnought and iron_duke 0% -> 0% (their wings keep the reach off them); lion 0.5% -> 0.8%, dreadnought
      0.3% -> 0.8%; destroyer 1.5% -> 9.5%, light_cruiser 3.5% -> 11%. **None lost by it:** the destroyer's brass
      charges burn out (41 kPa) and the light cruiser's Clarkson-cased 6" are contained (180-250 kPa; `shipgen magazine
      light_cruiser --race`). Pathfinder (1914, one torpedo, the forward magazine, gone in minutes) and Pommern want more
      than the race of the charges: V8's open item.
    - `dc-history` unchanged: none of its torpedoes reaches a magazine.
- **V8. The yardstick.**
  - The propellants' β and α fitted.
  - The cases above, scenario runs (squadrons, jutland, tsushima), and the user's 1.5%.
  - What's left goes to open items here.
  - **Done 2026-10-20.** What it came to:
    - **A station lights by its charges** (`Magazines.LightChance`): each charge lying there lights with
      `StationLightChance` 0.5, the station if any does, 1 - (1 - q)^n, n the discipline's charges a gun times the
      mount's guns. A twin on drill 0.75, on safe 0.5, on rate first 0.94. It was a flat 0.25 [EST]; the record's
      turret and barbette bursts of 1914-16 lit about 9 in 12 (Seydlitz, Derfflinger C and D, Lion Q, the three
      battlecruisers, Blücher, Malaya's battery; not Princess Royal's or Tiger's X). A burst, its spall (times
      `SpallFlashChance`) and its gas in a handling room all read it.
    - **A bench bug:** `shipgen hits` read `damage_control`, so `--damage-control` never reached the ship (every run on
      the default doctrine). Fixed.
    - **The propellants' β and α stay the research's** (10 §1.1), and Clarkson's R stays 4: the cases split as the
      record does without a refit (Lion's own lids-on burn out, the BCF's rate first severs, German brass burns out).
    - **The yardstick** (`damage-history`, 400 runs where noted):

      | Case | Now | Record |
      |---|---|---|
      | Indefatigable, 5 × 11″, "1916 BCF" (400) | lost 14% of runs, 2.75% a hit | ~5% a hit (1-14%) |
      | Tiger, 18 × 11″, "1916 BCF" (200) | lost 24% of runs, 1.3% a hit | lived; the BCF lost 3 of 7 |
      | Seydlitz, Derfflinger, Lion at Jutland (drill) | 0.4-0.9 trains lit a ship, none lost | Seydlitz's, Derfflinger's C and D, Lion's Q burned; none lost |
      | Lion at Dogger Bank, Chester, Kent | nothing | nothing |
      | 30 × 6″ HE on Iron Duke (400) | none | about 1.5% wanted; decision 7 accepts none |
      | Bismarck's brass, 1942 doctrine (Gneisenau's anchor) | every magazine burns out, 250-425 kPa | burned out |

      `shipgen hits indefatigable --shooter von_der_tann --range 14500` in calm water: on "1916 BCF" 2.75% of hits set
      off a magazine and 2.1% lose her; on drill 0.03%; on safe none.
    - **Scenario runs** (`scenario --play --seeds N`, before -> after V8; no ship lost to a magazine in either):
      - squadrons, 20 seeds: lost 8 (3 foundered, 5 capsized) -> 7 (capsized); main mounts still out at the end behind
        magazines flooded by order 15 -> 46 (the flash stopped short orders its magazine flooded, V6).
      - Tsushima, 4 seeds: Russia lost 3.0 -> 4.0 a seed, Japan 3.8 -> 3.5.
      - Jutland, 3 seeds: lost 91 -> 60 a seed (Britain 34 -> 20, Germany 57 -> 40), hits made 8,800 -> 5,200. The old
        rule at a flat 0.75 gives 74 and 6,900 (56-88 by seed): the drop is the turret fires being three times as
        common (each costs a turret its crew and often its magazine), not the new rule's own doing. The record:
        14 British and 11 German lost; every run is still far over it.
      - Found on the way: ships fall out for their main guns mostly on temporary blackouts from near misses
        (docs/battle/shells-armour-damage.md E7's fix, open). `scenario --play` prints the main-guns tally now, and each
        seed's losses by how they went.
    - **Tests:** the broken-shell turret test's range moved to the twin's 0.75.
- **Then the look:** tier 1 (the column without the fireball) and tier 4 (two fireballs) in
  `docs/visuals/magazine-explosions.md`, by its own process.

## What stays out

- Bombs (Arizona): their shapes and fuzes aren't researched (the shells plan's "What stays out").
- Torpedo warheads and ready ammunition on deck as explosives (Mikuma, Furutaka, Quincy). They're fire's propellant
  seats today; a warhead's detonation is a later cut's (deck torpedoes and Long Lances are WWII).
- Unstable propellant and accidents (Iéna, Liberté, Bulwark, Vanguard): peacetime, not a battle's.
- The pieces as floating bodies (07 §4): with the sinking and the look.
- Magazines going up after the ship is lost (Barham, Yamato, Kongō): the sinking pass's, eye candy once she's lost
  (decision 5).
- Uptakes and vent trunks as joins: Hood's vent through the engine-room vents wants them. They're E5's open item,
  added here only if the yardstick needs them.

## Research wanted (filed in `research/REQUESTS.md` 2026-10-20, "Magazines: the flash chain and the venting race")

- Burn-rate law (β, α) for the period's propellants (cordite MD, RP C/12, single-base, black powder), or ranges to fit
  within.
- Turret revolving weights and barbette diameters by calibre and period (07 §7). Shipgen's turret weights may do.
- Flash doors, scuttles, interlocks and handling practice: ranges by type and period (general system, not replicas),
  and how often doors stood open in drill.
- Sprinklers and magazine flooding times by period (10 §2.3 has some).
- Flash at open and shielded guns: did a flash ever go down an open mount's hoist to its magazine? Ammunition passages
  and casemate batteries as the path instead (Defence, Malaya). Decision 4 stands on our notes until this is checked.

## Decided (the user, 2026-10-20)

1. **The charge's propellant and packaging are gun-block data.** Bags or brass cases are a design choice, not a navy
   value. It changes little now; in the campaign it has an economic cost, as design choices do.
2. **Flash discipline is a doctrine row** by period, for now. Per-ship orders can come later.
3. **The hull girder is in now (V4).** Tier 3 means the ship breaks and sinks fast; the look of the break comes later.
4. **Shield and open mounts don't flash down their hoists** (the recommendation, taken 2026-10-20). Their ready
   charges burn on deck. Fire reaches the magazine only the ways fire goes. Their train starts at the handling room
   below the deck, and ammunition passages and casemate batteries are stations with doors. Today's trunk dose from a
   burning mount goes for them. The record as far as our notes go:
   - the danger at an open gun was its ready charges (Chester and Southampton at Jutland; Kent's casemate at the
     Falklands, 10 of her 14 casualties from one cordite fire);
   - the path below ran along passages and batteries (Defence's passages, Malaya's battery: charges on trolleys, ~5 min,
     65 dead);
   - there's no known case of a flash straight down an open mount's hoist [UNCERTAIN: in the research request].
5. **Magazines going up as a ship sinks** wait for the sinking pass: by then she's lost anyway, so it's eye candy.
6. **The race's boundaries go by the research's table** (2026-10-20, an approved exception to "one gas model"): run on
   the ship's own plating, the gas spread through her thin bulkheads and never loaded the girder (V3).
7. **Armoured casemates keep medium HE off the magazines** (2026-10-20): thirty 6″ HE on Iron Duke lose no ship in
   400 runs, against the 1.5% once wanted. Accepted: "it means the armouring is working". A ship with thinner
   casemates or open batteries can still lose one.

## Too safe: why nothing blows up (2026-11-05)

**Built at the user's word, 2026-11-05: recommendations 1-3 below.** A unit or ship entry's own `"damage_control"` (Jutland's three battlecruiser squadrons on "1916 BCF", Lion on "1916": her gunner kept the rules, 10 §3.1), drill's `DoorOpen` [1, 1, 0.02, 0.1], and Clarkson's `MagazinePackageR` 32 [EST, until the research answers]. The ladder below was measured before them, each rung by a settings file.

**What they gave** (`losses`, 4 seeds): 7 of 340 losses to a magazine, from 0 of 348. All at Jutland (1.75 a seed), all battlecruisers on rate first: Queen Mary twice, Tiger twice, Indefatigable, New Zealand, Princess Royal. Lion, the 5th Battle Squadron, the Grand Fleet and the Germans lose none to one; the Dogger Bank none. Jutland's lost a seed: Britain 21 / Germany 47, within the noise. Heavy hits are 1,687 a seed, so against the record's 3 battlecruisers in about 220 heavy hits (from memory), the rate a hit on the BCF's ships stays the check, not this count.

### Can a magazine fire be survived? (the user, 2026-11-05: "once it burns it's go time; flooding is performed to avoid fires reaching magazines, not to stop the fire already there")

**Flooding is already prevention only.** The race reads the flooded share once, at the flash, and runs out in seconds; the valves take minutes (`MagazineOrderS` 120 s, `FireMagazineFloodS` 600 s). Nothing floods a race under way.

**But most magazines alight still leave her afloat.** Jutland, a seed, on the fixes above (`losses`' new lines; ships with a magazine set off, those it took):

| | Ships set off | Taken by it | The races, 4 seeds |
|---|---:|---:|---|
| Britain, capital (Clarkson) | 6.3 | 1.8 | contained 12, burned out 8, breached 23, two groups 7 |
| Britain, cruiser (Clarkson) | 0.8 | 0 | contained 4 |
| Britain, destroyer (Clarkson) | 1.0 | 0 | contained 4 |
| Germany, capital (brass) | 4.0 | 0 | contained 1, burned out 42 |
| Germany, cruiser (brass) | 2.8 | 0 | contained 13 |
| Germany, destroyer (brass) | 2.3 | 0 | contained 10 |

A German capital ship set off burns out nearly 3 magazines and fights on, which nothing in the record supports. **Who survives, and on what:**

1. **Brass and tanks** (R 1.4, 1.2). Their anchors are Gneisenau and Boise, both 1942 (10 §6.2), and both cooler propellants than the first game's. Seydlitz, Derfflinger and Lion are not cases of a magazine alight: the train burned and the magazine was flooded or shut first (10 §4.3). Against brass surviving in the Great War, from memory: Pommern and Frauenlob. So in 1890-1925 no case is known here of a dry magazine alight that left the ship afloat. Boise is also the one case of the sea stopping a fire already there, 1942, through her own shell hole.
2. **Small stows,** Clarkson's too: `MagazineGiveKPa` is one table for every ship, so a light cruiser's 1-5 t stops at 250 kPa, "contained", with her hull untouched. This is cap 4 below.
3. **A single midships magazine on a capital ship:** Lion's Q, Iron Duke's Q (27-29 t, 700-807 kPa, her deck gone, she floats).

### The physics built (the user, 2026-11-05: yes to both; "see where we end up in the physics side vs reality before tweaking the rate")

**Built, not tuned.** No rate was set; every number below is what came out.

1. **R is packing times the propellant's sensitivity** (`MagazinePackageR` 32, 28, 24 on cordite; `PropellantRow.Sensitivity`). Double-base and early single-base 1; diglycol, triple-base, LOVA and the new stabilised single-base 0.05 (10 §1.2's about 20 times), so 1942's fits stand: 28 x 0.05 = 1.4 Gneisenau, 24 x 0.05 = 1.2 Boise. Design format 20: Treaty and Second War blocks on single-base fire `stabilised single-base`. The catch: 10 §1.3 has German brass main charges surviving a fore charge beside them (Lützow's B), and 28 makes brass hardly better than Clarkson in a magazine alight. To the research (`REQUESTS.md`, 2026-11-05).
2. **A magazine's boundaries give at its own steel** (`Magazines.GiveMm`, `GiveKPa`): the table for Lion's main magazines, `MagazineGiveMm` [8, 27, 73, 39], scaled by plate, as E5's rupture is. `shipgen magazine --race` prints the steel. Decision 6's table stays the base.

**Three follow-ons the measuring forced, none asked for by name:**

3. **What a boundary opens goes with its area** (`GiveM2`, `MagazineGiveFaceM2`). A destroyer's fixed 4 m² tear let her gas out, "contained", in a hull where 1.3 t of cordite is about 340 kPa over her whole volume, well past her plates.
4. **A side torn under the waterline is no way out until the deck has opened one** (07 §2.4, 10 §6 "backed by water") [EST]. Else a thin side saved her by venting first: even loose bags stopped severing Lion's Q.
5. **All her section gone near an end takes the end, not the ship,** whatever the bottom did (`Girder`). The end case had stopped happening.

**A bug of the earlier fix, found by measuring:** drill's "flash doors don't hold" read the chamber's door, and a wing turret sharing a magazine has no handling room, so nothing stood between her chamber and her charges: Nassau and Helgoland lost 4.4% of heavy hits to a magazine against König's 0.3%. The door into a magazine is now always the magazine's own.

**Where it ends up against the record** (the record from memory, to source):

| A heavy hit, % | Set off | Lost to it | |
|---|---:|---:|---|
| Lion, Queen Mary, Iron Duke (drill) | 0.13-0.17 | 0.1 | Grand Fleet, about 50 heavy hits, none |
| Indefatigable (drill) | 0.23 | 0.2 | |
| Rate first (`damage-history`: Indefatigable, Tiger) | 1.9-2.0 | 1.3-1.6 | ~5% (1-14%) |
| Minotaur, Warrior, Duke of Edinburgh | 0.2-0.3 | 0.1 | Defence, Black Prince: perhaps 2 in 50 |
| Seydlitz, König, Derfflinger, Nassau, Helgoland (RP C/12) | 0.2-0.5 | 0.1-0.5 | about 100 heavy hits, none; 0.1-0.5 expected at this rate |
| Mikasa | 0.3 | 0.1 | |
| Bismarck (cool, brass) | 0.6 | 0 | burned out, as Gneisenau |

| Jutland, a seed | Now | The record |
|---|---:|---:|
| Heavy hits | 1,702 | about 220 |
| Lost to a magazine | 5.25 (7% of 72) | 5-7 (20-28% of 25) |
| Battlecruisers | 3.25 | 3 |
| British armoured cruisers | 0 | 2 |
| German capital ships | 2.0 | 0 by gunfire |
| Ships with a magazine set off, of them lost any way | 11.6, 10.2 (88%) | |

The count looks right and isn't: it's 5 on about 8x the record's heavy hits. **At the record's 220 hits this would be about 0.7, so the rate a hit is a seventh to a tenth of the record's overall**: rate first about 2.5x under, armoured cruisers 10x or more, German capital ships about right.

**"Once it burns" now nearly holds:** 88% of the ships set off at Jutland are lost, by one way or another. Of German capital ships set off, the race itself now breaks 2.0 of 3.0 a seed, against 0 of 4 on fixes 1-3, and 2.8 are lost any way. Those not broken go by the sea through what she opened.

**Misses to look at, none fixed:**

- **Armoured cruisers** still don't break: their magazines burn and breach, and they sink later. Passages are what's missing (cap 4, 10 §5).
- **Santiago's unorganised cruisers** (`fire-history`), a magazine by a fire: 0% -> 45% of runs. fire.md B5 has the record "burned out, not blown up", though Oquendo's and Vizcaya's, from memory, did go up. Wants sourcing before anything moves.
- **Kirishima** 10% of runs to a magazine, Warspite 3%, the Yalu ironclad 5% (the record, from memory: none).
- **Killed a flash** 46-67 at Jutland's German cases against 75-80 a turret: closer, not there.
- **The side's steel is uneven:** Lion's A 73 mm and her Q 35, Queen Mary's 30, from where the belt's cells reach at a magazine's depth. It decides a lot: worth a look in the design of belts, not here.
- **The hits themselves** (2-4.4 lit per 100 heavy hits, record about 4): fine. The reach (0.1 door on drill) and the record's 3 in 58 BCF hits are where the rest of the gap lies.

**The panel** (`balance --seeds 8 --fc-runs 0`): 7 of 14 sides within twice the record, from 5 on fixes 1-3 (Coronel moved in, read as noise). Jutland 25.8 / 46.3, Dogger Bank 7.3 / 11.0. How ships go at Jutland: capsized 55%, foundered 31%, magazine 7%, broke in two 7%. `coarse-parity` not run: this isn't a balancing pass, and `CoarseParityTests` pass.

**The rate stays as it comes out** until the user has read this (the user: first see where the physics ends up).

### Ammunition passages (the user, 2026-11-05: "we build the physics and see what comes out of it")

**Built, nothing tuned.** Closes cap 4's missing half and V2's "where the design has them".

- **From the geometry, not a flag or a type:** a mount standing beyond its magazine's stretch by more than her own radius gets a passage (`Ordnance.AddPassages`): a room (`RoomKind.Passage`) from the magazine to the farthest such mount, from the magazine's crown to the deck over it. It owns no cell: it shares those it runs through on the centreline (`Room.Shared`, `Cell.Also`), so it carves up no boiler room, weighs nothing yet and draws nothing. `shipgen magazine` lists it in the trains. ShipDesign.Version 57.
- **A station of her train** (`StationKind.Passage`): gunhouse > chamber > passage > magazine; a casemate > passage > magazine; a shield's starts there. Charges in transit by the discipline.
- **Lit by a burst, its splinters or its gas there,** by the share of the space it takes up (`Magazines.Exposed`: its 2 m of the space's width). The charges burn at one place along it, not all of it: one space's men flash and one seat starts [EST].
- **The doors:** a casemate's hoist 0.2, then the magazine's own door 0.1: the same 2% in series as before, now with Kent's stop in the passage between them.
- **Defence's chain:** magazines on passages that meet, or that a passage runs over, form a group (`ShipClass.PassageGroup`), and light one from the next when the gas breaks through, at the pressure the bulkhead gives (07 §2.4 groups them) [EST: no door roll].

**Not built:** a stack in a passage on rate first (Bulwark's 20 a passage; "secondary cordite even worse" in the BCF, 10 §3.1), flame up the other mounts on her passage, passages off the centreline, their own bulkheads or weight.

**What came out** (`losses`, 4 seeds at Jutland and the Dogger Bank, 8 at Coronel and Tsushima; the record from memory):

| | Before | With passages | The record |
|---|---|---|---|
| Coronel, magazine's share of losses | 0% | 45% | Good Hope |
| Good Hope, Monmouth lost to a magazine | none | 3 of 8, 5 of 8 | Good Hope yes; Monmouth capsized, a turret blown off |
| Coronel, Britain lost a seed | 1.3 | 2.0 | 2 |
| Scharnhorst, Gneisenau lost to a magazine | none | 1 of 8 each | none |
| Asama class at Tsushima (Azuma, Izumo, Tokiwa) | | 7 in 24 ship-battles | none |
| Russia at Tsushima, broken by a magazine | | 0 (tanks: 6 of 9 breached) | Borodino |
| Jutland, lost to a magazine a seed | 5.25 | 5.25 | 5-7 |
| Battlecruisers, Jutland | 3.25 | 3.75 | 3 |
| Armoured cruisers, Jutland | 0 | **0** (10 lost in 4 seeds, by water; 3 races, all breached) | 2 |
| German capital ships, Jutland | 2.0 | 1.0 | 0 by gunfire |
| Seydlitz's case, `damage-history`, lost to a magazine | 0 of 40 | 3 of 40 | none |
| Santiago by fire | 45% | 55% | "burned out"; to source |

The rate a heavy hit hardly moves (armoured cruisers 0.1-0.2%; Minotaur's set-offs 0.3 -> 0.47%): passages change **what a magazine alight does** to a ship with small stows far more than how often one lights. So they gave Good Hope her explosion where the fire is short range and the hits many, and not Defence, who takes few hits here before she's already sinking.

**Jutland's total went from 72 to 91 lost a seed. Read it as noise, not as passages:** one seed (1918) went from 46 to 131; the others are 70 -> 89, 89 -> 71, 83 -> 72; and no destroyer has a passage, yet destroyers are most of the rise. The seeds' spread is 46-131 on these runs, far wider than the 66-79 `loss-stock-take.md` first read: four seeds of Jutland can't show a difference under about 25. The panel: 6 of 14 sides within twice the record, from 7 (Jutland's Britain out on that seed).

The user, from play: no magazine detonation in ages, even under silly debug shooting. "I think the current rates are way too low." Measured, it's worse than low: **on drill, a ship of the Great War can't be lost to a magazine at all,** and drill is what every side of every scenario and every dev ship is on.

**Four caps, in the order a flash meets them:**

1. **Nobody has the other row.** "1916 BCF" is the only doctrine with rate first, and no scenario names it. Per-squadron doctrine was left as the scenarios' later step (V5). It's a side's `damage_control` today.
2. **A shut door always holds.** The research's door ratings (10 §3.3, §7: pre-1916 flash doors fail at 20-40 kPa, "too weak to resist the pressure of a burning charge" [SRC: Brown]; a watertight magazine door about 100 kPa) were never built. `DoorOpen` is the only thing that passes a flame: 0.1 x 0.1 x 0.1 from a gunhouse.
3. **A lids-on magazine can't reach the girder.** Severing needs the sides to give, at 1,000 kPa. `shipgen magazine --race` over `shipgen/designs`' 72 on drill, each magazine set off alone: 259 burned out, 88 contained, 27 breached (the deck only, about 700 kPa: Devastation, and Nagato to Yamato, the treaty ships and later) and 6 two groups (only the Duilio, the Inflexible and the Coastal Battleship of Doom), the same in calm water and a full seaway. Lion, Queen Mary, Indefatigable, Invincible, Iron Duke and Tiger all burn out, 285-562 kPa. Clarkson's R 4 decides it, and 10 §6.2 and §8 call R "the most uncertain constant here" (4-8), fitted to a "Lion dry" case it marks as debated.
4. **A small stow can't either.** Armoured cruisers (Minotaur, Warrior, Duke of Edinburgh) burn 8-30 t, so even with every lid off most of their magazines stop at 700 kPa or less. The record's Defence went along her passages from magazine to magazine (10 §5), and shipgen emits no passages. The boundary table (decision 6) is one set of pressures for every ship, a destroyer's side the same 1,000 kPa as a battleship's.

**And one that hides what does happen:** a race she survives leaves no record and no look. `Blew` is only called from tier 3 up, so a burned-out magazine, tonnes of propellant in a column, shows as an ordinary fire (the look's tiers 1 and 4 are still to do).

**What's right already.** Hits that light charges in a train: 2-4.4 per 100 heavy hits in `damage-history`, against the record's rough 4 (V8's 9 lit, in about 220 heavy hits at Jutland; rough). So the flash is about the right size; what comes after it isn't. The per-hit rate on rate first is also near its anchor: `hits lion --shooter derfflinger --range 14000 --n 2000`, 2.35% of hits set off a magazine and 1.7% lose her, against ~5% (1-14%).

**A correction to `loss-stock-take.md`:** "a magazine reached 0 times" read the log's `HitEffect.Magazine`, which is set only when she's lost. In fact about 6 are set off a Jutland seed, and none takes the ship.

**The ladder.** `damage-history` 40 runs a case and `hits`, 3,000 hits; `losses`, 4 seeds, whose new line counts the races by tier. "Whole train" is drill's `DoorOpen` at [1, 1, 0.02, 0.1]: only the magazine's own door, and a casemate's, stop a flame. R is Clarkson's `MagazinePackageR`. Both are settings, so each rung is a settings file. Only armour-side cases are listed.

| A heavy hit, % lost to a magazine (set off) | Now | Whole train | Whole train + R 32 | Whole train + R 64 | Rate first, now |
|---|---:|---:|---:|---:|---:|
| Lion | 0 (0) | 0 (0.17) | 0.10 (0.17) | 0.10 (0.17) | 1.7 (2.35) |
| Queen Mary | 0 (0.03) | 0 (0.17) | 0.10 (0.17) | 0.10 (0.17) | |
| Indefatigable | 0 (0) | 0 (0.23) | 0.10 (0.23) | 0.20 (0.23) | 2.1 (2.75, V8) |
| Iron Duke | 0 (0) | 0 (0.13) | 0.10 (0.13) | 0.10 (0.13) | |
| Minotaur | 0 (0.23) | 0 (2.80) | 0 (2.80) | 0 (2.80) | |
| Mikasa | 0 (0.10) | 0 (0.30) | 0.20 (0.30) | 0.30 (0.30) | |
| Seydlitz, Derfflinger, Kaiser (brass) | 0 (0-0.10) | 0 (0.23-1.67) | 0 | 0 | |

| A seed | Hits lighting a train | Set off: contained / burned out / breached / severed or two groups | Ships lost to a magazine | Lost, side by side |
|---|---:|---|---:|---|
| Jutland, now | 124 | 3.3 / 3.0 / 0 / 0 | **0** | Britain 24 / Germany 48 |
| Jutland, whole train + R 32 | 92 | 4.5 / 23.3 / 2.3 / 0.3 | **0.25** (Tiger, once) | 24 / 53 |
| Jutland, Britain's whole side on "1916 BCF", settings as now | 94 | 10.8 / 8.8 / 4.0 / 2.3 | **2.25**, all British capital ships | 33 / 48 |
| Dogger Bank, now | 12.5 | 1.3 / 0 / 0 / 0 | **0** | 7 / 8 |
| Dogger Bank, whole train + R 32 | 15.5 | 1.8 / 2.5 / 0.5 / 0 | **0** | 8 / 9 |
| Dogger Bank, Britain on "1916 BCF" | 11.3 | 2.5 / 0 / 0.3 / 0.8 | **0.75** | 6 / 8 |
| The record (Jutland; from memory, to source) | about 9 | | about 5-7 of 25 lost: Indefatigable, Queen Mary, Invincible, Defence, Black Prince; perhaps Pommern and Frauenlob by torpedo | 14 / 11 |

Four seeds and the panel's spread: read direction only. Jutland's total losses don't move beyond the noise on any rung. Heavy hits run about 9x the record's (the gunnery gap, `error-sources.md`), so judge these by the rate a hit, never by matching a battle's count.

**What the rungs do to the anchors** (`damage-history`, 40 runs):

- Whole train moves **killed a flash** toward the record: Seydlitz 23 -> 45, Derfflinger 24 -> 40, Lion 32 -> 61, Warspite 32 -> 69, the record about 75-80 a turret (Seydlitz, Derfflinger: 10 §3.3-§4.3). The flames do run the train in the record.
All on whole train + R 32:

- The German cases burn out, none lost, as the record has it. Lion at both battles sets nothing off. Warspite loses 1 of 40 runs (record: none). The Yalu ironclad 2 of 40 (black powder, doors new to it).
- **The rung's worst miss: Kirishima,** 20 x 16" at 8 km: lost 20% -> 40% of runs, 4 of 40 to a magazine (record, from memory: no magazine explosion).
- `fire-history`: one of its nine cases goes from none lost to 1 of 20, to a magazine by a hit; the fires themselves still don't set off a ship. `torpedo` doesn't move (no loss to a magazine on lion, dreadnought, light_cruiser or m_class; 10.8% of hits on a light cruiser still set one off, so V8's open item stands).

**The evidence on R, from memory and to source (`research/REQUESTS.md`, 2026-11-05):** ships lost to a magazine alight with no enemy, presumably lids on: Natal, Vanguard, Mikasa 1905, Tsukuba, Kawachi; with single-base, Iéna and Liberté; Bulwark had charges lying out. Against that, no case is known here of a dry 1900-1918 cordite magazine alight that burned out with the ship afloat. Burned-out magazines in the notes are German brass (the train, in fact, not the magazine), Gneisenau 1942 and Boise 1942. If that holds, Clarkson's R is far above 4-8, and tanks of early single-base (Borodino at Tsushima, by our propellant "tank" R 1.2) may be too.

**Recommended order, the user's call:**

1. **Doctrine by squadron:** a unit's `damage_control` in a scenario, the BCF on rate first at Jutland. Small, and it's the record's biggest single part.
2. **Doors:** whole train on drill's row, then door ratings as data if the row proves too blunt. Also closes a casualty gap.
3. **R,** with the research. Without it, drill stays at zero whatever the doors do, and with it a dreadnought's rate is about 0.1% a heavy hit (1 hit in 1,000). What drill's rate should be is the open question: the record's Grand Fleet took about 50 heavy hits and lost none, and Lion's Q was one door from it.
4. **Thin ships:** passages as stations and magazines joined along them (V2's "where the design has them"), and the boundary table scaled by the ship. A plan step of its own.
5. **The look:** tier 1 (the column without the fireball) and tier 2. On the whole-train rung that's about 23-26 events a Jutland seed nobody sees today.

The benches: `losses` prints the magazines line; `shipgen magazine --race` no longer stops at a magazine feeding no gun.

## Open items (from the other docs, folded in)

- E8a item 1 (Chester's fake turrets): V1. E8a item 4 (German turrets burn out): V3.
- Fighting.md 4.5's Magazines list (the venting race, the flash chain, propellant by navy, flooding as an order, the
  hull broken, flash discipline, medium HE stays rare): V2-V8. Propellant by navy is now the packaging (decision 1).
- The shells plan's seams: E2's and E3's flash sources and E5's gas are the train's first links (V2). E5's trunks are
  the hoist trunk (V2). E6 kills by flash per station (V2).
- Fire B2's magazine heat and flooding: V6 and V7. Damage control's two valve sets: V6.
- The magazine look's tiers 1 and 4: after V3 (`docs/visuals/magazine-explosions.md` decision 1).
- bugs.md's mount kind: V1.
- **Left after V8** (2026-10-20):
  - A torpedo or mine into a magazine (Pathfinder, Pommern, Pallada; Petropavlovsk and Hatsuse by mines) lights it, but
    the race of the charges alone burns out or holds there. The warhead breaking the packages, or the shell room's
    filler going sympathetically, would be what took them. Research first.
  - A burst among a magazine's own charges still rolls the flat `MagazineFlashChance` 0.25 [EST]; the stow's charges
    in their packages could light by count as a station's do.
  - Rate first's stack: the research has 20-40% of a magazine's rounds in the handing room and working chamber, lids
    off; ours is 2 charges a station a gun. And whether the BCF's lids came off in the magazine or only in the handing
    room (Friedman against Brown, 10 §8).
  - Jutland's British side is on 1916's drill, so its battlecruisers rarely blow up in the scenario. Per-squadron
    doctrine (the BCF on rate first) is the scenarios' later step.
  - `shipgen magazine` prints shipgen's room volumes, which are 0 m³ for a secondary magazine sharing a main one's
    cells. The race reads the cells' gas, so only the print is off.
- **Fixed 2026-10-27 (found in `coarse.md` J6): a spent magazine was raced again every second.** Fire's heat
  (`Fires.Magazines`) never looked at whether a magazine was spent. A race she lived through leaves the lit magazines
  destroyed, but their heat stayed past its limit. So the race ran again at each damage step until she blew or the
  fire died, and its aftermath ran with it: the trains' men flashed again, casualties drawn again, the vented gunhouses
  lit again. In a coarse Jutland that was 4,641 races, 5 s of the run. Now a destroyed magazine has nothing left to go
  up (`FireEffectsTests.A_spent_magazine_is_not_set_off_again`). The damage benches barely move.
